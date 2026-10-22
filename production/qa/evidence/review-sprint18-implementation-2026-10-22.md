# 代码评审原件 —— Sprint 18 实现(7 件)

**日期**:2026-10-22
**评审技能**:`/code-review` · **模式** `lean`
**对象 commit**:`ad7316c`(S18-2)· `fc1705d`(S18-1)
**评审人**:编排方 + `unity-specialist` + `qa-tester`(并行 spawn,各自草稿落盘)
**草稿溯源**:`production/qa/evidence/tmp-review-s18-unity-specialist.md`(46 行,28 条判定)
· `production/qa/evidence/tmp-review-s18-qa-tester.md`(124 行,A–H 八节)

> ⚠️ **草稿保留不删(偏离 `code-review` skill Phase 7b ③ 的默认「合并后删除」)**,理由:
> 本件 §二 只收 5 条 Required,两位专家的 **28 条判定表 / A–H 八节**里另有一批 PASS 项与
> Suggestions 证据;**删草稿会让那部分判定失去可回溯来源**,与「原文不删、就地加注」纪律相抵。
> 与 `qa-signoff-sprint-18-2026-10-22.md` 保留 `tmp-qa-…-qa-lead.md` 同一处置。
**初判**:`CHANGES REQUIRED` —— **零架构违规**、零门 A 破损、零 ADR 硬冲突;五条 Required 全部为局部改动量级。

> 本件按 `coding-standards.md §Review Evidence Standards` 落盘:BLOCKING 判定须有原件,
> 含**原判定 → 修复落点 → 验证命令**。`CHANGES REQUIRED` 属 BLOCKING 判定。

---

## 一、范围(7 件)

| # | 文件 | 归属 |
|---|---|---|
| 1 | `unity/Assets/Sim/EntityDeathGates.cs` | S18-1 |
| 2 | `unity/Assets/Sim/FoldedPredicate.cs` | S18-2 |
| 3 | `unity/Assets/Sim/HistoryFolder.cs` | S18-2 |
| 4 | `unity/Assets/Sim/FoldRow.cs` | S18-2 |
| 5 | `unity/Assets/Sim.Contracts/IFoldedPredicate.cs` | S18-2 |
| 6 | `unity/Assets/Sim.Contracts/ActorClass.cs` | S18-1 |
| 7 | `unity/Assets/Sim.Codec/SaveService.cs` | 本轮仅新增可选构造形参;余为既有代码 |

---

## 二、判定汇总

| ID | 严重度 | 判定 | 落点 |
|---|---|---|---|
| **R-1** | 中(标准 FAIL) | `Fold` 单方法 83 行、近似圈复杂度 14–18(两种计法均 >10)⇒ 破「≤40 行」与「<10」两条 | `Sim/HistoryFolder.cs:69-151` |
| **R-2** | 中(证据完整性) | 测试注释声称「文件末尾有 AST 式断言」守卫规则九 —— **该断言从未存在**;全仓 `Editor.Tools.Gates` 亦无规则九门 ⇒ AC ⑥ 实为 NOT-RUN | `Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs:248` |
| **R-3** | 中(文档) | 头注与 `SelfLimited` 主注仍引 2026-10-21 **已作废**的旧式 `∧ ¬IsCombatant`,与同文件 `:129` 散文相反;GDD `:1197-1203` 明令「不得照旧式写」 | `Sim/EntityDeathGates.cs:5` 与 `:124` |
| **R-4** | 低-中(行为) | `LethalityOf(entry) && !IsCombatant(...)` 的短路使 `ActorClass.Unspecified` 在**非致死条目**下静默返回 `false`,击穿同文件 `:85-90` 的 fail-loud 契约 | `Sim/EntityDeathGates.cs:120` |
| **R-5** | 低(文档计数) | 第九抽象点落地后,「八抽象点」计数未刷 | `docs/architecture/adr-025-contract-assembly-manifest.md:112` · `Editor.Tools.Gates/AssemblyGates.cs:807` |

**门 A 与装配边界(全会话实测 PASS)**:`Sim.asmdef:4,13` 引用集恰 = `Sim.Contracts` 且
`noEngineReferences: true`;`Sim.Contracts.asmdef:4,13` 引用集 `[]`;`SaveService` 只见 Contracts 抽象点;
四个实现文件 `TryGetPayload` / `new PayloadRef` 零命中(ADR-031 唯一解码路径成立)。

**ADR 符合性**:ADR-025 §①(甲案 + 构造函数注入 + `Checkpoint` 签名不动)、ADR-010 §二(折叠行六字段与顺序)、
ADR-026 §⑦(条件 ④ 落动作侧)、ADR-032 ④a/④b/⑤/⑥ —— 全部 COMPLIANT。

---

## 三、逐条:原判定 → 修复落点 → 验证命令

### R-1 —— `Fold` 超长 + 圈复杂度超限

- **原判定**:`HistoryFolder.cs:69-151`,`Fold` 单方法 **83 行**(非空非注释 67)、近似圈复杂度 **14–18**。
- **修复落点**:拆为四个方法(签名与行为逐字不变):
  | 方法 | 行号(修后) | 规模 | 圈复杂度 |
  |---|---|---|---|
  | `Fold`(守卫 + 编排) | `:69-89` | 21 行 | ≈6 |
  | `Collect`(收 onset / outcome) | `:94-129` | 36 行 | ≈8 |
  | `BuildRows`(产行 + 确定性排序) | `:135-161` | 27 行 | ≈3 |
  | `Erase`(抹除,减 `SkillGrown`) | `:166-179` | 14 行 | ≈4 |
- **验证命令**:
  ```bash
  python3 - <<'PY'   # 逐方法统计(见 §四 复算脚本)
  ```
  判据:四方法均 ≤40 行且 CC <10 —— 实测 21/36/27/14 行、≈6/8/3/4。
  行为不变的判据 = 同一批 29 条折叠测仍全绿(见 §四 回归)。

### R-2 —— AC ⑥ 引用了不存在的断言

- **原判定**:`entity_death_gates_test.cs:248` 写「真正的规则九守卫是**结构性**的:见本文件末尾的 AST 式断言(禁子串)」。
  实测:`wc -l` = **258 行**,文件在 `:258` 结束,**不存在该断言**;`grep -rn 规则九 unity/Assets/Editor.Tools.Gates/` **零命中**
  ⇒ 规则九无任何机械门,AC ⑥ 只能算 NOT-RUN。
- **修复落点**:删掉该虚假声明,改为如实登记 —— 测试内注释(`:246-251`)+ 文件头「验收范围」块
  (`:11-19`)双处写明:**AC ⑥ = NOT-RUN,唯一证据 = 人工读源码;补机械门归 Tooling 层**。
  **未新增断言** —— 裸子串 grep 会撞上 `EntityDeathGates.cs:35` 注释里的同名文本,假绿风险高于收益。
- **验证命令**:
  ```bash
  grep -n "AST 式断言" unity/Assets/Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs   # 期望:仅 2 处,均为「从未存在 / 已删」否定句
  grep -rn "规则九" unity/Assets/Editor.Tools.Gates/                                                # 期望:零命中(缺口仍存在,已登记)
  wc -l unity/Assets/Tests/EditMode/DiseaseSimulation/entity_death_gates_test.cs                    # 文件行数
  ```

### R-3 —— 引用了已作废公式

- **原判定**:`EntityDeathGates.cs:5` 与 `:124` 写 `SelfLimited = self_limit ∧ ¬IsCombatant`;
  GDD `disease-simulation.md:1162-1210` 已于 **2026-10-21** 就地订正为
  `[self_limit ∨ IsCombatant] ∧ ¬战伤破例`,并逐字写「实现时不得照旧式写」。实现本体(`:143-161`)与订正式**等价**,故为文档级缺陷。
- **修复落点**:`:5-9`(头注)与 `:124-133`(`SelfLimited` 主注)改引订正式,并注明旧式作废缘由;
  测试文件头 `:5` 同款订正。
- **验证命令**:
  ```bash
  # 旧式**整句**应零命中(残存的 `∧ ¬IsCombatant` 只在「已作废」注 / LethalFor 原式里)
  grep -rn "SelfLimited(entity, d) = self_limit(d) ∧ ¬IsCombatant" unity/Assets/    # 期望:零命中
  grep -c "∨ IsCombatant" unity/Assets/Sim/EntityDeathGates.cs                      # 期望 ≥2(实测 2)
  ```

### R-4 —— `LethalFor` 短路击穿 fail-loud

- **原判定**:`return LethalityOf(entry) && !IsCombatant(actorClass);` —— 非致死条目令 `IsCombatant` 不被调用,
  `Unspecified` 静默得 `false`;而同文件 `:85-90` 声明「`Unspecified` ⇒ 抛,不猜」。
- **修复落点**:`EntityDeathGates.cs:129` 改为 `return !IsCombatant(actorClass) && LethalityOf(entry);`,
  并在 XML 注(`:121-125`)里写明次序理由。
- **行为影响面**:仅 `Unspecified` + 非致死条目(原返回 `false` ⇒ 现抛)。全仓零测试断言旧行为(已 grep 核)。
- **补的断言**(修复生效的判据,非事后追加):`test_lethalFor_unspecifiedFailsLoud_evenOnNonLethalEntry`
  (`entity_death_gates_test.cs:104-118`),致死与非致死两条路径**都**要求抛。
- **验证命令**:
  ① 全量回归 `4419 / 4383 / 0 / 36`(见 §四),新断言 Passed;
  ② **变异反证**:把 `:129` 回退为 `LethalityOf(entry) && !IsCombatant(actorClass)` ⇒
  `testFilter=EntityDeathGatesTest` 单 fixture 跑得 `21 / 20 / 1 / 0`,
  **恰 1 红**(`test_lethalFor_unspecifiedFailsLoud_evenOnNonLethalEntry`,
  消息「非致死条目 + Unspecified 同样须抛 —— Expected `<ArgumentOutOfRangeException>` But was: null」),
  **零附带红**;还原后 SHA-256 逐字节一致(`8aca6a6d…5d92`,变异前后同值)。
  产物 `unity/Logs/mut-r4-editmode-20261022.{xml,log}`。

### R-5 —— 抽象点计数未刷

- **原判定**:`adr-025:112` 与 `AssemblyGates.cs:807` 仍写「八抽象点」,而 `IFoldedPredicate`(S18-2)
  在 `IFoldedPredicate.cs:1` 自陈「第九抽象点」并已进 ADR-025 成员行。
- **修复落点**:`adr-025:112` → 「**九抽象点**(… · **S18-2 的 `IFoldedPredicate`**)」;
  `AssemblyGates.cs:807` 的错误串 → 「**抽象点**/SimEvent/VitalsDto」(`IFoldedPredicate` 尚未进该门黑名单,
  带数字会立刻再次失准 —— 计数改为不写数字)。
- **验证命令**:
  ```bash
  grep -n "九抽象点" docs/architecture/adr-025-contract-assembly-manifest.md        # 期望 1 命中(实测 1)
  grep -rn "八抽象点" docs/architecture/adr-025-contract-assembly-manifest.md unity/Assets/Editor.Tools.Gates/  # 期望零命中(实测 0)
  ```
  ⚠️ **残留(未修,如实)**:`adr-005:35` · `architecture.md:439,561,1222` 仍写「八抽象点」——
  属 ADR-031 §V-10 遗留的**计数订正批**,不在本件 R-5 面内。

---

## 四、修复轮结账(已落 / 已证 双列)

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| **R-1** | ✅ | ✅ | `Sim/HistoryFolder.cs:69-179` | ① 规模复算:四方法 21/36/27/14 行、CC≈6/8/3/4;② 同一批 29 条折叠测全绿(§四 回归三元组) |
| **R-2** | ✅ | ✅ | `entity_death_gates_test.cs:11-19,246-251` | `grep -n "AST 式断言"` ⇒ **2 处,均为否定句**(原虚假声明已删);`grep -rn 规则九 Editor.Tools.Gates/` **零命中**(缺口仍存在,已登记为 NOT-RUN) |
| **R-3** | ✅ | ✅ | `Sim/EntityDeathGates.cs:5-9,124-133` · 测试 `:5` | `grep -c "∨ IsCombatant"` ≥2;旧式仅存于「已作废」注内 |
| **R-4** | ✅ | ✅ | `Sim/EntityDeathGates.cs:129` · 测试 `:104-118` | ① 全量回归新断言 Passed;② **变异反证**:`:129` 回退旧判序 ⇒ 单 fixture `21/20/1/0`,**恰 1 红且为新断言、零附带红**,还原 SHA-256 一致(`mut-r4-editmode-20261022.xml`) |
| **R-5** | ✅ | ✅ | `adr-025:112` · `AssemblyGates.cs:807` | 上述两条 grep;`八抽象点` 在该两文件零命中 |

**回归三元组(修复后全量 EditMode)**:截至 `2026-10-22 10:19 (+0800)` · 批次 `review-s18-r4-fix-20261022`
· commit `ad7316c` + 评审修复件(**未提交**,含 R-1…R-5 与 R-4 的专钉断言)。

| 套件 | 结果(XML) | 对照基线(同带三元组) | 差集 |
|---|---|---|---|
| EditMode | `4419 / 4383 / 0 / 0 / 36` | `4418 / 4382 / 0 / 36` @ `s18-2-editmode-commit-20261022` · `ad7316c` | **新增 1 / 消失 0**(全名集逐项比对);`Δtotal = Δpassed = 1` = 新增条数 |

- 新增的 1 条 = `EntityDeathGatesTest.test_lethalFor_unspecifiedFailsLoud_evenOnNonLethalEntry`(R-4 专钉)。
- `error CS` = **0**;failed = 0;inconclusive = 0;skipped 36(与基线同名同数)。
- 两个目标 fixture 全绿:`EntityDeathGatesTest` **21 / 21**、`FoldedPredicateTest` **29 / 29**。
- 「消失 0」为**独立判据**(全名 `Counter` 差集),不是从 `total` 相减推断。
- ⚠️ 本批次跑在**未提交树**上;commit 字段如实写成 `ad7316c + 未提交`,不冒充干净树。
- **中间轮留档**:补断言**之前**的 `review-s18-r1-fix-20261022` 批(`4418 / 4382 / 0 / 36`,
  新增 0 / 消失 0)证明 R-1…R-3 / R-5 的改动**零回归**,但**不**构成 R-4 的生效证据 ——
  该轮跑在补断言之前。两轮分开记账,不合并。

---

## 五、未修项(Suggestions,如实保留)

| # | 项 | 落点 | 去向 |
|---|---|---|---|
| S-1 | `IFoldedPredicate` 未进 `AudioForbiddenContractTypes`(ADR-031 §⑤ 先例) | `AssemblyGates.cs:417-424` | sprint-19 |
| S-2 | `Fold` 的双源未断言(谓词自带流视图 × 入参 `history`);`IsIdentity` 会把「折了零行」报成恒等 | `HistoryFolder.cs:82-88` | sprint-19(须先裁 `OQ-7a-10`) |
| S-3 | `FoldRow.Outcome` 用 `int` 而非 `PatientOutcome`,构造期不拒 0 | `FoldRow.cs:45,50` | 7a 往返轮 |
| S-4 | 一次 `Folded(p)` 最多 5–6 趟全量扫;全量折叠 O(P·(H+C)),P∝H 时退化 O(H²) | `FoldedPredicate.cs:89,112,143,157` | 7a 往返轮 |
| S-5 | 重复 onset 静默取首条 vs 重复 outcome 抛 —— 姿态不对称 | `HistoryFolder.cs:99-100` vs `:108-111` | 随 `OQ-7a-10` |
| S-6 | `IFoldedPredicate` `<returns>` 称「四条件全部成立」而 ④ 在接口外 | `IFoldedPredicate.cs:28` | 文档批 |
| S-7 | `SaveService` 可空注入无 fail-fast | `SaveService.cs:47,59` | 装配层 |
| S-8 | `FoldedPredicate.cs:14-15`「任一病种**不属于**豁免类」与代码方向相反 | `FoldedPredicate.cs:14-15` | 文档批 |

**另记(评审发现、非本 sprint 引入)**:`ISaveService`(`Sim.Codec/ISaveService.cs:20-27`)只有
`SaveBytes` / `LoadBytes`,而 ADR-010 `:325` 已声明 `Checkpoint` / `SaveOnExit` / `Load`;
`folded_predicate_test.cs:503` 断言的实为**方法名集** `{LoadBytes, SaveBytes}`,**不是** ADR 写的
「参数集恰等于 `{SaveSlot}`」⇒ 该断言护的是「接口不增方法」,强度弱于 `SaveService.cs:39-42` 的转述。

---

## 六、未及核查项(禁借绿)

| # | 项 | 来源 |
|---|---|---|
| 1 | IL2CPP 矩阵 / CI 常驻 job(`TD-ci-01` EXTERNAL) | 与本件零交集,未跑 |
| 2 | `Sim.Contracts` 全装配成员面 diff(只核了新增文件) | unity-specialist 草稿 §未及 |
| 3 | `PatientOutcomeChanged` / `DiseaseOnset` 在 `entities.yaml` 的 `stream:` 路由 | 同上 |
| 4 | ADR-032 全文(只读 ④/⑤/⑥ 与附录 A 命中行) | 同上 |
| 5 | 证伪轮原件与变异落点 / SHA-256 还原判据 | qa-tester 草稿 §未及 |
| 6 | `CaseStreamQuery.HasOpenCase` 本体覆盖 | 同上 |
| 7 | 全名集差集「消失 0」的独立复算 | ✅ **本轮已做**(`Counter` 差集:新增 0 / 消失 0) |

---

## 七、溯源

| 项 | 出处 |
|---|---|
| 初判来源 | 编排方静态评审 + `unity-specialist`(28 条)+ `qa-tester`(A–H 八节) |
| 权威件 | GDD `persistence-service.md:314-330` · `disease-simulation.md:1153-1230` · ADR-010 / 025 / 026 / 031 / 032 |
| 回归产物 | `unity/Logs/review-s18-r1-editmode-20261022.{xml,log}` |
| 关联件 | `production/qa/qa-signoff-sprint-18-2026-10-22.md`(签核判「已落/已证」双列,**结论不受本件推翻**) |
