# 代码评审原件 —— Sprint 18 收口后新增件（8 件盲区补评）

**日期**:2026-10-24
**评审技能**:`/code-review` · 模式 `lean`
**评审对象 commit**:`4e005bd`(sprint-18 末个生产提交,干净树)
**补评缘由**:`review-sprint18-implementation-2026-10-22.md`(最后提交 `08049fb` @ 10-22 11:15)
的评审范围只覆盖 **7 件**,而其后 `5f40b59`(13:55)/ `7014630`(13:55)/ `3bb2df8`(15:34)
又落了 8 件生产与测试代码,**从未被任何评审覆盖**。本件补此盲区。

> **盲区坐实(可复算)**:在 `review-sprint18-implementation-2026-10-22.md` 内
> `grep -c` 六个关键符号,命中数**全为 0**:
> ```
> PlayerDiedWriter 0 · RuleNineGate 0 · DeathCandidateConsumer 0
> IDeathCandidateSource 0 · InjuryStateChangedWriter 0 · DiseaseConstantRegistryGate 0
> ```

---

## 一、范围（8 件，约 1,981 行）

| # | 文件 | 行数 | 落地提交 | 时点 |
|---|---|---|---|---|
| 1 | `unity/Assets/Sim.Contracts/IDeathCandidateSource.cs` | 72 | `5f40b59` | 10-22 13:55 |
| 2 | `unity/Assets/Sim/DeathCandidateConsumer.cs` | 109 | `5f40b59` | 10-22 13:55 |
| 3 | `unity/Assets/Sim/PlayerDiedWriter.cs` | 129 | `5f40b59` | 10-22 13:55 |
| 4 | `unity/Assets/Sim/InjuryStateChangedWriter.cs` | 116 | `5f40b59` | 10-22 13:55 |
| 5 | `unity/Assets/Editor.Tools.Gates/RuleNineGate.cs` | 590 | `7014630` | 10-22 13:55 |
| 6 | `unity/Assets/Editor.Tools.Gates/DiseaseConstantRegistryGate.cs` | 199 | `3bb2df8` | 10-22 15:34 |
| 7 | `unity/Assets/Tests/EditMode/DeathRespawn/death_candidate_writers_test.cs` | 301 | `5f40b59` | 10-22 13:55 |
| 8 | `unity/Assets/Tests/EditMode/DiseaseSimulation/rule_nine_gate_test.cs` | 465 | `7014630` | 10-22 13:55 |

**初判**:`APPROVED WITH SUGGESTIONS` —— **零架构违规**、零门 A 破损、零 ADR 硬冲突。
两条 WARNING 均为「判据强度」问题（不是代码缺陷），一条 INFO 为可读性。

---

## 二、判定清单

| ID | 级别 | 判定 | 落点 |
|---|---|---|---|
| **W-1** | ⚠️ WARNING | **AC ⑦ 的「29 侧注入点」在生产树零构造点** —— `DeathCandidateConsumer` 只有构造器声明（`:60`），无任何 `new` 调用；`IDeathCandidateSink` 在生产树零实现类。AC ⑦ 声称「29 侧注入点可用」，「可用」= 可编译可测，但**不可达** | `DeathCandidateConsumer.cs:60` · `IDeathCandidateSource.cs:57` |
| **W-2** | ⚠️ WARNING | **三条写者生产构造点全部为零** —— `new PlayerDiedWriter` / `new InjuryStateChangedWriter` 在生产树零命中。仅测试装配（`CompositionRoot.Assemble()`）构造它们 | `PlayerDiedWriter.cs:1` · `InjuryStateChangedWriter.cs:1` |
| **S-1** | 💡 SUGGESTION | `DiseaseConstantRegistryGate.ExtractBlock` 锚串硬编码 **两个空格**（`^  - name: `），把「缩进深度」当结构判据。当前 `entities.yaml` 恰好是两空格（六块实测 `1563/1580/1599/1619/1654/1759`），但若 YAML 被整体重排（如挪进嵌套层或改四空格），门会**报「缺块」**——失败方向是响亮 false red（可接受），但会掩盖真实原因 | `DiseaseConstantRegistryGate.cs:180` |
| **S-2** | 💡 SUGGESTION | `RuleNineGate.CheckIl` 的 `DiseaseKeyLoadedWithin` 用**字段名**（`fr.Name == "DiseaseKey"`）而非字段类型锚定，与同文件 `KindLoadedWithin`（锚 `FieldType`）标准不一致。当前全仓 `DiseaseKey` 唯一，暂无假红，但这是同文件内两种锚定哲学的并存 | `RuleNineGate.cs:539-549` |
| **P-1** | ✅ PASS | 三道门（`RuleNineGate` / `DiseaseConstantRegistryGate` / `WriterExistenceGate` 撤豁免）**均已接线**，且在 `RunMenu` + `BuildGate` 双路径生效 | 见 §三 |

---

## 三、接线核验（P-1 的落点）

| 门 | `RunMenu` 路径 | `BuildGate` 路径 | manifest 登记 |
|---|---|---|---|
| `RuleNineGate.RunAll` | `AssemblyGates.cs:126` | `AssemblyGates.cs:1276` | `gate_wiring_manifest_test.cs:106` / `:120` / `:247` |
| `DiseaseConstantRegistryGate.RunAll` | `AssemblyGates.cs:149`（在 `RunAll()` 内，菜单 / reload / 构建前三处共用） | 同左（经 `AssemblyGates.RunAll()`） | `gate_wiring_manifest_test.cs:87` / `:202` |

**复算命令**：
```bash
grep -rn "RuleNineGate" unity/Assets --include='*.cs' | grep -v '/Tests/' | grep -v "RuleNineGate.cs:"
grep -rn "DiseaseConstantRegistryGate" unity/Assets --include='*.cs' | grep -v '/Tests/' | grep -v "DiseaseConstantRegistryGate.cs:"
```
**实测输出**：前者 `AssemblyGates.cs:126` / `:1276` 两处真接线 + `FsWriteBoundaryGate.cs:125`
一处注释引用（非接线）；后者 `AssemblyGates.cs:149` 一处。

> ⚠️ **本件不主张「门真会红」已证** —— 那是 `rule_nine_gate_test.cs` 的 16 条注入测试
> 与 `DiseaseConstantRegistryGateTest` 11 条在管；且「门真跑了」须走**探针轮**
> （承 `CLAUDE.md` §构建前门），本件未跑探针轮，故 §三 只证「接线点在源码里」。

---

## 四、W-1 详述：AC ⑦「注入点可用」的语义缺口

**AC 原文**（`sprint-18.md:165`，签核件 ⑦ 行）：
> **`IDeathCandidateSource` + 29 侧注入点** | `test_deathCandidateSource_interfaceExistsInContracts` /
> `_hasNoHealthReadingMember` / `_deathCandidate_doesNotEnterAnyStream` / `_deathCandidateConsumer_implementsSource`
> | **新增** `Sim.Contracts/IDeathCandidateSource.cs` + `Sim/DeathCandidateConsumer.cs` | ✅

**实测**：
```
$ grep -rn "new DeathCandidateConsumer" unity/Assets --include='*.cs' | grep -v '/Tests/'
（空）
$ grep -rn "IDeathCandidateSink" unity/Assets --include='*.cs' | grep -v '/Tests/'
IDeathCandidateSource.cs:57   （仅接口声明）
DeathCandidateConsumer.cs:27  （仅注释：生产装配归 9 / 29 的装配轮）
```

**判定**：AC ⑦ 的四条测试全部是**结构性断言**（接口是不是接口 / 方法签名对不对 / 有没有生命值成员 /
类有没有实现接口），**没有一条测「注入点在生产装配里可达」**。
`DeathCandidateConsumer.cs:27` 自己写明了「生产装配归 9 / 29 的装配轮」——
⇒ **这是如实登记的已知缺口，不是隐瞒**，但 AC ⑦ 的 ✅ 容易被读成「注入点已可用」。

**为什么是 WARNING 而非 BLOCKING**：
- 同格先例 `CaseOpenWriter` 也是「有写者、未接线」且已被 `WriterExistenceGate` 接受（见该门注释）；
- 装配归 9 / 29 的实现轮，不在 S18-1 的裁决面内；
- 但 AC ⑦ 的措辞「注入点可用」与实测「零构造点」之间有语义落差，**须在签核件加注**。

**建议落点**（不要求改代码）：在 `qa-signoff-sprint-18-2026-10-22.md` 的 ⑦ 行加一句
「注入点**已实现且可测**，生产装配归 9 / 29 装配轮（`DeathCandidateConsumer.cs:27` 自陈）」。

---

## 五、W-2 详述：三条写者的构造点

**实测**：
```
$ grep -rn "new PlayerDiedWriter" unity/Assets --include='*.cs' | grep -v '/Tests/'
（空）
$ grep -rn "new InjuryStateChangedWriter" unity/Assets --include='*.cs' | grep -v '/Tests/'
（空；仅 EntityDeathGates.cs:50 的注释提及「自身零生产构造点」）
```

**判定**：与 W-1 同族。`InjuryStateChangedWriter` 的「零生产构造点」已被
`EntityDeathGates.cs:50` 的注释**主动登记**，属于诚实的已知缺口。

**ADR 合规**：ADR-029 §③（`IPayloadEncoder` 是唯一编码路径）+ ADR-009 §二（按 Kind 路由）
在两个写者里**均正确落实** —— 二者都经 `_encoder.Encode(EventKind.X, payload)` 而非手搓
`PayloadRef`。**零架构违规**。

**`WriterExistenceGate` 同批撤豁免**（`WriterExistenceGate.cs` 内 `PlayerDied` / `InjuryStateChanged`
两条已标 ✅ 撤表）：这是**正确的**，因为门判的是「写者存在性」而非「接线」。
该门注释也明写「有写者 ≠ 已接线」。

---

## 六、S-1 详述：`ExtractBlock` 的两空格锚

```csharp
// DiseaseConstantRegistryGate.cs:180
string head = @"^  - name: " + Regex.Escape(constantName) + @"[ \t]*\r?$";
```

**实测当前缩进**（六块全部恰两空格）：
```
1563:  - name: CRITICAL_THRESHOLD
1580:  - name: COMA_THRESHOLD
1599:  - name: DEATH_THRESHOLD
1619:  - name: TREATMENT_WINDOW
1654:  - name: RECOVER_THRESHOLD
1759:  - name: CARE_GAP
```

**复算 D2（六字段齐备性）**：
```bash
python3 -c "
import re
src=open('design/registry/entities.yaml',encoding='utf-8').read()
for n in ['CRITICAL_THRESHOLD','COMA_THRESHOLD','DEATH_THRESHOLD','TREATMENT_WINDOW','RECOVER_THRESHOLD','CARE_GAP']:
    m=re.search(r'^  - name: '+re.escape(n)+r'[ \t]*\r?\$',src,re.M)
    rest=src[m.end():]; nx=re.search(r'^  - name: ',rest,re.M)
    blk=src[m.start():m.end()+(nx.start() if nx else len(rest))]
    miss=[f for f in ['status','source','referenced_by','value','unit','constraint']
          if not re.search(r'^[ \t]+'+re.escape(f)+r':[ \t]*',blk,re.M)]
    print(n, '缺:', miss if miss else '无')
"
```
**实测输出**：六块**全部「缺: 无」** ⇒ 真工程绿。

**判定**：门本身工作正常，且文件头已**主动登记**两条盲区（文本级非 AST / `value:` 占位值照样绿）。
S-1 只是提醒：`^  -` 的两个空格是**未声明的隐式契约**，建议在文件头盲区清单里补一句。

---

## 七、S-2 详述：同文件内两种锚定哲学

| 方法 | 锚定方式 | 抗改名能力 |
|---|---|---|
| `KindLoadedWithin`（`:523`） | `fr.FieldType?.FullName == DiseaseKindFullName` | ✅ 字段改名不影响 |
| `DiseaseKeyLoadedWithin`（`:539`） | `fr.Name == "DiseaseKey"` | ❌ 字段改名即静默失效 |

**为什么 `Kind` 必须锚类型**：`RuleNineGate.cs:519-521` 已写明 —— 真工程有
`ClinicRoomClauseKind` / `EventKind` 两处同名 `Kind` 字段，按名字锚会假红。
**为什么 `DiseaseKey` 可以锚名字**：全仓唯一，无同名冲突。

**判定**：不是缺陷，但两者并存无说明。建议在 `DiseaseKeyLoadedWithin` 的 doc 里补一句
「本方法锚名字而非类型 —— 全仓 `DiseaseKey` 唯一；若日后出现同名，须改为类型锚」。

---

## 八、标准符合性

| 检查项 | 结果 | 说明 |
|---|---|---|
| 公共 API 有 doc 注释 | ✅ | 8 件全部有；`RuleNineGate` 注释密度最高（含假红 / 假绿记录） |
| 圈复杂度 < 10 | ✅ | 最高为 `RuleNineGate.ScanMethodIl`（约 7） |
| 方法 ≤ 40 行 | ⚠️ | 最长 `RuleNineGate.ScanMethodIl`（`:453-511`）实测 **59 行** —— **超 40 行界**。属 IL 遍历的自然长度，且已拆出 `KindLoadedWithin` / `DiseaseKeyLoadedWithin` / `IsStringEqualityCall` / `TryGetI4` / `IsComparisonOp` 五个子方法；仍按标准记一条偏离，不作「已达标」处理 |
| 依赖注入（非单例） | ✅ | 三条写者 + consumer 全部构造器注入，且全部 null 守卫 |
| 配置值来自数据文件 | ✅ | 常量住 `entities.yaml`，由 `DiseaseConstantRegistryGate` 守 |
| 系统暴露接口 | ✅ | `IDeathCandidateSource` / `IDeathCandidateSink` 双接口分离（消费面 / 生产面） |

---

## 九、架构 / SOLID

**架构**：
- ✅ 依赖方向正确：`Sim` → `Sim.Contracts`（门 A 内侧），`Editor.Tools.Gates` 单向依赖两者
- ✅ 无循环依赖
- ✅ 层分离：`Editor.Tools.Gates` 只在 `#if UNITY_EDITOR` 内，不进构建
- ✅ 跨系统通信走接口（`IDeathCandidateSource`），不直接引用具体类

**SOLID**：
- ✅ SRP：三个写者各管一个 Kind；`DeathCandidateConsumer` 只做「判定 + 转发」
- ✅ OCP：新增 Kind 只需加写者，不动既有
- ✅ ISP：`IDeathCandidateSource`（消费）/ `IDeathCandidateSink`（生产）分离，无胖接口
- ✅ DIP：consumer 依赖 `PlayerDiedWriter` 具体类 —— ⚠️ 轻微偏离，但 `PlayerDiedWriter` 是
  同程序集内的叶子类，无多实现需求，**可接受**

---

## 十、正向观察

1. **`RuleNineGate` 的判据演进记录是范本** —— 文件头完整记录了两次**实测假红**
   （`ClinicRoomClauseKind` / `EventKind` 同名尾巴；`R1_CHECK_31` 的异常码 31 被当成
   `DiseaseKind` 成员），并写明「假红与假绿同属静默失败」。这是本项目假绿治理的最佳实践。

2. **`rule_nine_gate_test.cs` 的 IL 注入路线否决记录** —— 首版想用 Cecil 注入，实测
   测试装配无 Cecil 引用（111 条 CS0234），改走「把违例写成测试源码里的真方法，让编译器
   生成 IL」。**这比 Cecil 注入更强**：IL 是真编译器产出，且夹具不进 `Assets/Sim`
   ⇒ 源码半对它失明而 IL 半抓住，**这正是 IL 半独立价值的直接证明**。

3. **`DiseaseConstantRegistryGate` 主动登记了自己的 false green** ——
   「`value:` 字段写个新占位数值它照样绿」，并明写「不得把本门绿读成『数值已裁定』」。
   主动暴露盲区而非掩盖，符合本项目「禁借绿」纪律。

4. **`DeathCandidateConsumer` 的三层分离** —— 消费面（接口）/ 判据（私有纯函数）/
   写入（委托写者）三层清晰，且注释明确「本类不直接 `Append`，写者仍是唯一写入口」。

---

## 十一、修复轮结账表

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| W-1 | ✗ | ✗ | `DeathCandidateConsumer.cs:27` | **本轮不修** —— 装配归 9 / 29 实现轮；建议仅在签核件加注（见 §四） |
| W-2 | ✗ | ✗ | `PlayerDiedWriter.cs` / `InjuryStateChangedWriter.cs` | **本轮不修** —— 同上；`EntityDeathGates.cs:50` 已自陈 |
| S-1 | ✗ | ✗ | `DiseaseConstantRegistryGate.cs:180` | **本轮不修** —— 建议在文件头盲区清单补一句（见 §六） |
| S-2 | ✗ | ✗ | `RuleNineGate.cs:539` | **本轮不修** —— 建议在 doc 补一句（见 §七） |
| P-1 | — | ✅ | `AssemblyGates.cs:126/149/1276` | `grep -rn "RuleNineGate" unity/Assets --include='*.cs' \| grep -v '/Tests/' \| grep -v "RuleNineGate.cs:"` ⇒ 2 处真接线（`:126` / `:1276`）+ 1 处注释；`DiseaseConstantRegistryGate` ⇒ 1 处（`:149`） |

> ⚠️ **本表如实登记：四条判定均 `Landed=✗ / Proven=✗`。**
> 它们是**评审发现**，不是**本轮已修**。承 `code-review` skill Phase 8b：
> 一行两列皆 ✅ 才算结清；本件只结清 P-1（接线核验）。
> **W-1 / W-2 的处置归签核件加注轮**（那是 ③ 的范围）。

---

## 十二、未及核查项（如实列出，禁借绿）

| # | 项 | 原因 |
|---|---|---|
| 1 | **探针轮**（门真会红 / 门真被调用） | 须独占 Unity 工程跑真构建，本轮未跑；`sprint-18.md` 记有 2026-10-22 的探针轮记录，但**那是 `RuleNineGate` 的，不是 `DiseaseConstantRegistryGate` 的** |
| 2 | `DiseaseConstantRegistryGate` 的**注入级反证** | 未核其 11 条测试是否含「改坏 yaml ⇒ 转红」的真反证（本轮只核了真工程绿） |
| 3 | `RuleNineGate` 在**真构建**中的 `scriptCompilationFailed` 分支 | 只在源码读到 `:127` 的守卫，未实测「编译失败时门拒扫」 |
| ~~4~~ | ~~`PlayerDiedWriter` 的 `Seq` 发号真源~~ | ✅ **已核（本轮补）**：写者传 `Seq = -1` 未发号哨兵（`PlayerDiedWriter.cs:123-124`），发号者是 `EventStream.Append`（`EventStream.cs:119-132`，每 tick 复位、按 `(Tick, Patient)` 递增）。**不是** `IIdAuthority` —— 测试的 `Seq >= 0` 断言测的是「入流后已补号」，语义正确 |

---

## 十三、Verdict

**`APPROVED WITH SUGGESTIONS`**

- **零 ARCHITECTURAL VIOLATION**
- **零 ADR 硬冲突**（ADR-005 / 009 / 020 / 024 / 029 全部符合）
- **两条 WARNING（W-1 / W-2）不是代码缺陷，是 AC 措辞与实测的语义落差** ——
  两处均已被代码注释**主动登记**，属诚实缺口
- **两条 SUGGESTION（S-1 / S-2）**为可读性改进，非阻塞
- **P-1 接线核验通过** —— 三道门双路径接入，manifest 同批登记

**唯一须跟进的动作**：在 `qa-signoff-sprint-18-2026-10-22.md` 的 AC ⑦ / ⑧ 行
加注「注入点已实现且可测，生产装配归 9 / 29 装配轮」—— 这属 ③ 的范围。

---

## 附：本件的可复算性

本件所有判定均可当场重跑。最小复算集：

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 盲区坐实
grep -c "PlayerDiedWriter\|RuleNineGate\|DeathCandidateConsumer\|IDeathCandidateSource\|InjuryStateChangedWriter\|DiseaseConstantRegistryGate" \
  production/qa/evidence/review-sprint18-implementation-2026-10-22.md   # 期望 0

# ② W-1 / W-2：生产树零构造点
grep -rn "new DeathCandidateConsumer\|new PlayerDiedWriter\|new InjuryStateChangedWriter" unity/Assets --include='*.cs' | grep -v '/Tests/'
# 实测输出（唯一一行是注释，不是构造点）：
#   unity/Assets/Sim/EntityDeathGates.cs:50://  · 但 `InjuryStateChangedWriter` **自身零生产构造点** —— `new InjuryStateChangedWriter(...)`
# ⇒ 零真构造点

# ③ P-1：门接线
grep -rn "RuleNineGate" unity/Assets --include='*.cs' | grep -v '/Tests/' | grep -v "RuleNineGate.cs:"
# 实测输出（3 行，其中 1 行是 FsWriteBoundaryGate 的注释引用）：
#   FsWriteBoundaryGate.cs:125   （注释，非接线）
#   AssemblyGates.cs:126         ← RunMenu 路径
#   AssemblyGates.cs:1276        ← BuildGate 路径
grep -rn "DiseaseConstantRegistryGate" unity/Assets --include='*.cs' | grep -v '/Tests/' | grep -v "DiseaseConstantRegistryGate.cs:"
# 实测输出：AssemblyGates.cs:149（在 RunAll() 内，菜单 / reload / 构建前三处共用）

# ④ S-1：六常量块缩进
grep -nE "^  - name: (CRITICAL_THRESHOLD|COMA_THRESHOLD|DEATH_THRESHOLD|TREATMENT_WINDOW|RECOVER_THRESHOLD|CARE_GAP)$" design/registry/entities.yaml
# 期望：6 行，全部两空格

# ⑤ 40 行界
sed -n '453,511p' unity/Assets/Editor.Tools.Gates/RuleNineGate.cs | wc -l   # 期望 59
```
