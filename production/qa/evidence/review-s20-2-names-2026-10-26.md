# 评审报告原件 —— S20-2 两张词表(ROOM_NAMES / CONTEXT_NAMES)

> **评审对象**:commit `1b2f70f`(S20-2 词表轮,43 文件 / +2003 −33)
> **日期**:2026-10-26 · **批次**:sprint-20 · S20-2
> **模式**:lean(`production/review-mode.txt` 未设 → 默认)
> **评审方**:① unity-specialist(C#/Unity)② qa-tester(可测性)③ 编排方自核
> **本件 = 报告原件**(承 `coding-standards.md`「评审报告原件落盘」)。
> 分析草稿:`tmp-review-s20-2-names-{unity,qa}.md`(合并后删除)

---

## 零、总判定

| 评审方 | 判定 | BLOCKING | WARNING | INFO/GAPS |
|---|---|---|---|---|
| unity-specialist | PASS with warnings | **0** | 3(D1/D2/D3) | 5(M1–M5) |
| qa-tester | **GAPS(ADVISORY)** | **0** | — | 5 条 Required + 3 Suggestions |
| 编排方自核 | 硬约束全通过 | **0** | — | 见 §二 |

### Verdict: **APPROVED WITH SUGGESTIONS**

**零 BLOCKING、零 ARCHITECTURAL VIOLATION。** 但 QA 侧 5 条 Required Changes
落在**测试判别力**面上,其中 2 条属「守卫有实现、无夹具」⇒ 该守卫今日**不可证伪**。
按本项目 S12-4 纪律,这类缺口必须显式登记,不得以「全量绿」代替。

---

## 一、硬约束逐条实测

| # | 约束 | 实测 | 判定 |
|---|---|---|---|
| 1 | **门 A / b2**(`Sim` 引用集) | `Sim.asmdef:4` `references: ["Sim.Contracts"]` + `:13` `noEngineReferences: true`;两新文件 `using` 仅 BCL / `Sim.Contracts` | ✅ COMPLIANT |
| 2 | 方法 ≤ 40 行 | 五文件全部 ≤ 40(最长 = `ClinicNameTableCore` ctor 35 行) | ✅ |
| 3 | 圈复杂度 < 10 | 最高 = `ClinicMachineRoomNamesBinder.Bind` **10**(布尔操作数口径)/ 8(分支口径)—— 贴界通过 | ⚠️ 零余量(M3) |
| 4 | 公共 API doc 注释 | 7 文件全覆盖;`<exception>` 逐条编号列出拒收理由 | ✅ 高于基线 |
| 5 | 依赖注入 | Memo ctor 8 参全 `?? throw`;`ClinicEnvProjection` 是纯函数容器(零字段) | ✅ |
| 6 | 配置从数据文件读 | 词表内容全在 `assets/data/*.json`;`ConfigVersion` 含 6 源文件 | ✅ |

### ADR Compliance

| ADR | 判定 | 依据 |
|---|---|---|
| **ADR-005** 确定性 / 无状态 | ✅ COMPLIANT | `ClinicEnvProjection` 纯函数零缓存;缓存全在表现层 |
| **ADR-014** 两阶段烘焙 | ✅ COMPLIANT | Binder 住 `Editor` 平台;零 `JsonConvert.DeserializeObject<T>`;运行期只读 `*.cooked` |
| **ADR-025** 程序集清单 | ✅ COMPLIANT | `Sim` 引用集一字未改;无新程序集、无未登记边 |
| **ADR-024** 白名单纪律 | ✅ COMPLIANT | `IsStructureKind` 用**穷举 switch** 而非 `StartsWith`,注释明写理由 |

**无 DRIFT、无 VIOLATION。**

---

## 二、WARNING 三条(必须修)

### D1 — `ClinicEnvDto.cs:25` 字段注释与实现语义**相反**

```csharp
/// <summary>房间名字典 ordinal(词表索引;ROOM_NONE 亦占条目)。</summary>
```

实现是「词表**不含** id = 0」:
- `ClinicNameTables.cs:113` 明写「本表**不含** `NoneId` 条目」
- `ClinicNameTables.cs:78` 构造器对 `e.Id < 1` 直接抛 `ArgumentException`

同文件 `:12-16` 的 2026-10-26 补注**已订正**为「`ROOM_NONE = 0` 是哨兵,真实条目从 1 起」,
但 `:25` 未同步 ⇒ **同一文件内自相矛盾**。

**修法**:`:25` 改为「房间名词表索引;`ROOM_NONE = 0` 为哨兵,不占词表条目」。

### D2 — `ClinicMachineRoomNamesBinder.cs:5` 把**过时注释**列为权威来源

```csharp
//   ClinicEnvDto.cs:20「房间名字典 ordinal(词表索引;ROOM_NONE 亦占条目)」
```

这句正是 D1 的过时文本(且行号 `:20` 已漂移到 `:25`)。而**同一文件** `:13` 又写
「id = 0 **不进本表**」⇒ 文件内 `:5` 与 `:13` 自相矛盾。

这是本项目反复抓过的「**引用过时权威**」失效模式 —— `ClinicEnvDto.cs:12-16` 的现行补注才该是权威。

**修法**:`:5` 改引 `ClinicEnvDto.cs:12-16` 或 GDD `:340`,删掉「ROOM_NONE 亦占条目」的转述。

### D3 — `GetDto()` 让 `int[]` 成为**跨调用共享可变引用**

`ClinicEnvDto.ContextIds` 是 `int[]`(引用类型)。Memo 化后 `GetDto()` 反复返回**同一个**
`_cachedDto`(`ClinicEnvironmentMemo.cs:174-184`)⇒ 其 `ContextIds` 数组成为缓存内部状态的共享引用。

`readonly` **只禁重新赋值,不禁元素写入**。42 侧写 `dto.ContextIds[0] = 99` 即静默污染缓存,
后续 `GetDto()` 命中返回被改过的值 —— 违反 EC-24-05「缓存前后逐位一致」。

类头 `:20-24` 论证的「逐位一致是结构性的」在此处有一个缺口。

**当下风险 = 预防性,不是现存缺陷**(编排方补核):
`grep -rn "\.ContextIds" unity/Assets --include=*.cs` 的生产消费方**全库为零**,只有测试在读。

**但当前状态是最坏的一种:既共享又没写进契约。** 修法择一:
- **(a)** `GetDto()` 返回前防御性拷贝 —— 保证不变量,有分配成本
- **(b)** `<returns>` 明确写「`ContextIds` 为共享缓存引用,调用方**不得修改**」+ 42 侧机械门

承本项目「静默失效」取向,**建议 (a)**。

---

## 三、QA Required Changes 五条(测试判别力)

### Q1 — context 侧 Binder 8 条守卫**只测 1 条**

| 侧 | 守卫数 | 有夹具 | 比例 |
|---|---|---|---|
| `ClinicMachineRoomNamesBinder` | 12 | 5 | 42% |
| **`ClinicMachineContextNamesBinder`** | **8** | **1** | **12.5%** |

context 侧**完全无夹具**的守卫(逐条):

| 守卫 | 源码位置 | 夹具 |
|---|---|---|
| 缺 `contexts` 数组 | `:81-85` | ❌ |
| **空词表** | `:97` | ❌ |
| 行未知键 | `:116` | ❌ |
| `context_id` 缺失 / 非字符串 | `:119` | ❌ |
| `id` 缺失 / 非整数 / ≤ 0 | `:124` | ❌ |
| `id > int.MaxValue` | `:132-136` | ❌ |
| **`context_id` 重复** | `:140` | ❌ |
| `id` 重复 | `:146` | ✅ `test_binder_contextNames_duplicateId_rejected` |

**空表守卫尤其危险** —— 空 CONTEXT_NAMES ⇒ 所有情境投不出来 ⇒「印卸恒不成立」,
正是本件头注点名要防的静默失效形态,却唯一没有夹具。

### Q2 — `id > int.MaxValue` 两处守卫零夹具

`Room:136` / `Context:132` 各一条,夹具目录 `grep 2147483648` **零命中**。
`ReadRequiredPositiveInt:439` 只挡 `<= 0`,故该值可达。

### Q3 — `ClinicEnvProjection` 7 条守卫测了 4 条

缺:两条 `ArgumentNullException`(`:42/:43`)、**`Matched && RoomType == null`**(`:62-64`)、
`ContextIds[i] == null`(`:82-84`)。

**最值得补的是 `:62-64`** —— 它是本类**唯一**用 `Matched` 而非 `RoomType == null` 做判据的
显式理由(`:56-59` 注释逐字写了为什么)。把 `:60` 改成 `if (evaluation.RoomType == null)`
会让语义退化,而**当前 19 个测试全部仍绿**(`UnmatchedEvaluation` 传 `matched:false` 且
`roomType:null`,两种写法都给 `NoneId`)⇒ **这条设计理由零测试保护**。

### Q4 — `clinic_name_tables_test.cs:8-11` 头注自述与自身内容不符

头注列 ⑤「Memo 缓存 DTO(交付面)」为本件链条一环,但本件 `grep Memo` **只命中该头注**。
实际载体是 `clinic_purity_replay_test.cs:448/:484`。

覆盖**全局成立**,但**本件自述与自身内容不符** —— 与本项目反复抓过的「自述顶替门」同型。

### Q5 — `test_runtimeTableCore_rejectsCorruptVocabularies` 四个问题

`:217-248` 把 4 条守卫挤在一个 `[Test]` 内:
1. 一条红无法定位是哪条守卫失效
2. 第 ④ 条用 `ClinicContextNameTable`,前三条用 `ClinicRoomNameTable` —— context 表构造器被改坏时,红会**归因到 room 表的守卫**
3. `Assert.Throws<ArgumentException>(..., "空表须拒")` 第二参是 NUnit 自定义失败消息,**不是判据**
4. 缺 `Name` 为 null/空(`ClinicNameTables.cs:74-75`)那条

---

## 四、S12-4 证伪轮的**靶面盲区**(本轮最重要的发现)

### 4.1 事实

15 条变异按被变异文件分布:

| 文件 | 变异数 | 变异 ID |
|---|---|---|
| `ClinicMachineRoomNamesBinder.cs` | 4 | M1a / M2 / M3 / M4 |
| **`ClinicMachineContextNamesBinder.cs`** | **1** | **M1b** |
| `ClinicMachineSchema.cs` | 1 | M5 |
| `ClinicMachineTablesBake.cs` | 3 | M6 / M7 / M8 |
| `ClinicMachineCookedWriter.cs` | 1 | M9 |
| `ClinicEnvProjection.cs` | 3 | M10 / M11 / M12 |
| `ClinicEnvironmentMemo.cs` | 2 | M13 / M14 |
| `ClinicNameTables.cs` | 1 | M15 |

**context Binder 的 8 条守卫里,只有「编号≠行序」(M1b)被变异过;其余 7 条从未被打过。**

### 4.2 为什么这重要

「14/15 转红、零误伤」读起来像覆盖充分,实际是**靶面偏了**:
room 侧 4 条变异 vs context 侧 1 条。**M1b 之所以逃逸,不是巧合** ——
它是 context 侧唯一一条变异,而它恰好撞上夹具判别力不足。若另外 7 条也被打过,
很可能更早暴露。

### 4.3 证据件登记面的遗漏

`impl-clinic-name-tables-2026-10-26.md` §四 逐条列了 15 条变异,但**没有声明靶面覆盖** ——
读表的人会以为「14/15 转红 = 覆盖充分」。**这是登记面的遗漏,不是数字错误。**

### 4.4 硬测量(探针轮)

为把「零红逃逸」从推断变成观测,对 **context 空表守卫**做一次探针:
停用 `if (rows.Count == 0 && errors.Count == 0)` ⇒ 跑 ClinicMachine 夹具 ⇒ 期望**零红**。

结果见 §4.5(探针结果回填)。

### 4.5 探针结果 —— **零红逃逸已实证**

**探针**:停用 `ClinicMachineContextNamesBinder.cs:97` 的空表守卫
（`if (rows.Count == 0 && errors.Count == 0)` → `if (false)`）。

```
原始 SHA-256 = 41302087d76ca2423855b173f230914e0b75dadd5850151aee6d7a30ac9b2549
已变异        = 479627d01e72bf28a0c5e722850eb4338b7818a0b7eb461fb7a39c64f0d64a32
已还原        = 41302087d76ca2423855b173f230914e0b75dadd5850151aee6d7a30ac9b2549  ✅ 逐字节一致

结果: total=5104 passed=5069 failed=0 skipped=35   RED=0
```

**零红。全量 5104/5069 全绿 —— 该守卫今日不可证伪。**

| 判据 | 结果 |
|---|---|
| 还原 = SHA-256 逐字节 | ✅ 一致(非 grep 探针串) |
| 变异窗口内不读不编辑被变异文件 | ✅ 遵守 |
| 变异保持可编译 | ✅ 5104 条测试全跑完,非编译失败 |
| 期望 | **零红**(证实靶面盲区) |

**这条探针的价值不在「发现一个 bug」,而在「证明一个守卫今天无法被证伪」** ——
它与 §4.4 的靶面统计（context 侧 1 条 vs room 侧 4 条）互为印证：
M1b 的逃逸不是运气,是靶面系统性偏斜的必然结果。

**处置**:补 7 条 context 侧夹具（见 Q1）+ 补 `id > int.MaxValue`（Q2）+ 补
`ClinicEnvProjection` 三条（Q3）后，本探针须重跑并要求**转红**。

### 4.6 探针复验 —— **转红成功**(修复轮已完成)

修复后重跑同一探针（停用 `ContextNamesBinder.cs:97` 空表守卫）：

```
原始 SHA-256 = c7fc7d5d107cbd15c7036c64a3d4620daf279ff9700b4b8bb51e4265112afe64
已变异        = b60c1232b6c6a4bcc20c65f056b7718ebd87512a82cac308d17cf2591950f1f1
已还原        = c7fc7d5d107cbd15c7036c64a3d4620daf279ff9700b4b8bb51e4265112afe64  ✅ 逐字节一致

结果: total=5120 passed=5084 failed=1 skipped=35   RED=1
  RED: test_binder_contextNames_empty_rejected
```

| 判据 | 修复前 | 修复后 |
|---|---|---|
| RED 数 | **0**（零红逃逸） | **1** |
| 红名单 | — | `test_binder_contextNames_empty_rejected` —— **恰好**是补的那条 |
| 还原 | SHA-256 ✅ | SHA-256 ✅ |

⇒ **补的夹具确有判别力**,不是「补了个绿」。该守卫自此**可证伪**。

---

## 五、INFO / Suggestions

| # | 项 | 落点 |
|---|---|---|
| **M1** | `RoomNoneId = 0` **只声明未使用**,且与 `ClinicRoomNameTable.NoneId` 构成**同一哨兵的两个真源**(改一处不改另一处即静默漂移)。编排方补核:`docs/` `design/` `tests/` `production/` **全零命中** ⇒ 纯死代码 | `RoomNamesBinder.cs:30` |
| **M2** | `seenIds2` 装的是 `HashSet<string>`(context_id 集),形参名却是 `seenContextIds` —— 复制粘贴残留 | `ContextNamesBinder.cs:88` |
| **M3** | `Bind` 圈复杂度贴界(10 / 8 两口径),零余量 | `RoomNamesBinder.cs:66` |
| **M4** | `GetDto`/`Get` 未声明会抛 `InvalidOperationException`(fail-loud 的 API 应把抛出写进契约) | `Memo.cs:174` |
| **M5** | 两个 Binder 约 90% 相同 —— **不建议抽基类**(项目既有惯例「一表一 Binder」,抽基类会把 ADR-024 的显式白名单变成参数化) | — |
| **S1** | `AssertErrorsContain(ex, "id")` 针太弱(任何含 `id` 的错误串都命中) | `test.cs:160` |
| **S2** | `:170/:179/:208` 三处共用针 `"重复"` —— 判别力靠「夹具只触一条守卫」的隐含前提 | `test.cs` |
| **S3** | `new int[evaluation.ContextIds.Count]` 若为 null 会 `NullReferenceException` 而非 `InvalidOperationException` | `Projection.cs:78` |

---

## 六、Positive Observations

1. **`ClinicEnvProjection` 的 fail-loud 选型**(`:15-18,66-69`):查不到编号 ⇒ `throw`,
   **明确拒绝降级为 None**,错误消息点名后果「会把产物错配显示成『没有房间』」。
   这正是本项目反复抓的静默失效形态,此处在设计上封死了它。

2. **判据选型防御未来缺陷**(`:56-59`):用 `Matched` 而非 `RoomType == null`,
   注释解释「反过来把两者当同义词会让将来『匹配到空名类型』的缺陷静默落 None」。
   ⚠️ 但该理由零测试保护(见 Q3)。

3. **错误聚合而非首错即停**(`RoomNamesBinder.cs:122-134`):两字段**都尝试**读取后才
   `if (!ok) return null`。且「0 保留给 ROOM_NONE」由 `ReadRequiredPositiveInt` **单机制**守。

4. **缓存正确性论证是结构性的**(`Memo.cs:20-24,158-165`):重算**就是** `Evaluate(...)` 本身,
   零增量更新;`ClinicEnvDto` 与 `ClinicEnvEvaluation` 由**同一次**求值一起产出 ⇒ 不可能漂移。

5. **覆盖门方向与独立性都对**(`TablesBake.cs:181-182,205-262`):两条词表门放在 `crossErrors`
   早退分支**之外**;方向明确为**单向 ⊇** 并写明理由;错误消息给**可执行修复动作**。

6. **`ClinicNameTableCore` 的 internal 设计**(`ClinicNameTables.cs:41-45`):让「room 编号喂给
   context 表」**不可编译** —— 用类型系统而非文档纪律守边界。

7. **「编号 ≠ 行序」贯穿四处**(`ClinicNameTables.cs:12-16` / `RoomNamesBinder.cs:8-11` /
   `ContextNamesBinder.cs:8-11` / `CookedWriter.cs:191-193`)。

---

## 七、未及核查项(禁借绿)

| # | 项 | 状态 |
|---|---|---|
| U1 | `ContextIds` 的 42 侧消费方是否**实际修改**该数组 | ✅ **编排方已补核**:生产消费方全库为零 |
| U2 | `Structure*` 三支是否已在 `entities.yaml` 登记(ADR-024 A1) | ❌ 未核 |
| U3 | `RoomNoneId` 是否被 docs/design/tests 引用 | ✅ **编排方已补核**:全零命中 |
| U4 | 「项目既有 23 个同类 Binder」惯例数字 | ❌ 未做全库计数 |
| U5 | 两个新 Read 与 Write 的逐字节对拍 | ⚠️ 专家人工核对布局对称,**未跑测试** |
| U6 | `BindRoomNamesForTest`/`BindContextNamesForTest` 是否生产路径薄包装 | ✅ **编排方已补核**:一行 `=> Binder.Bind(json)`,与生产同函数 ⇒ **非结构性假绿** |
| U7 | 损坏 cooked 字节(截断 / schema_version 不匹配)读取路径 | ❌ 未测 |
| U8 | 实际编译 / 测试是否跑过(评审期) | 评审为**纯静态**;全量绿另有出处(`names-full2.xml` `5104/5069/0/35`) |

---

## 八、修复轮结账表(承 Phase 8b 双列 + Branch 列)

> 修复轮:2026-10-26 · 全量回归 `5120/5085/0/35` · 增量 `added=17 removed=1`
> (removed = 被拆分的 `test_runtimeTableCore_rejectsCorruptVocabularies`,见 Q5)

| ID | Landed | Proven | Branch | File(s) | Proven-by |
|---|---|---|---|---|---|
| D1 | ✅ | ✅ | — | `ClinicEnvDto.cs:20-23` | 单分支(纯注释订正)。反证 = 读实现:`ClinicNameTables.cs:78` 构造器对 `e.Id < 1` 抛 `ArgumentException`,`:113` 明写「本表**不含** `NoneId` 条目」⇒ 注释与实现一致;旧文本「亦占条目」与新文本互斥,无并存可能 |
| D2 | ✅ | ✅ | — | `RoomNamesBinder.cs:5-8` | 单分支(纯注释订正)。反证 = `grep -n "亦占条目" RoomNamesBinder.cs` **零命中**(旧转述已删),`grep -n "12-16" RoomNamesBinder.cs` 命中 `:5` 指向现行补注 |
| D3 | ✅ | ✅ | ✅ 2/2 | `Memo.cs:174-186` / `ClinicEnvDto.cs:28-35` | 承评审建议 **(b) 契约声明**而非 (a) 防御拷贝(理由见下)。① 共享性:`test_dto_contextIds_isSharedCacheInstance_contractPinned`(`clinic_purity_replay_test.cs`)`Is.SameAs` 钉同一实例 + `Is.Not.SameAs` 钉跨 Memo 不共享;② 契约文本:`<returns>` 逐字写「调用方不得修改」+ `ToArray()` 出路。反证 = 把 `GetDto()` 命中分支改成返回深拷贝 ⇒ 该测**转红**(`Is.SameAs` 断言失败) |
| Q1 | ✅ | ✅ | ✅ **8/8** | `ContextNamesBinder.cs:81,97,116,119,124,132,140,146` | 8 条守卫逐条一夹具(见 §8.1 表)。**核心反证 = §4.6 探针复验**:停用 `:97` 空表守卫 ⇒ `test_binder_contextNames_empty_rejected` **红**(修复前同一变异零红) |
| Q2 | ✅ | ✅ | ✅ **2/2** | `Room:136` / `Context:132` | `test_binder_roomNames_idOverflow_rejected` + `test_binder_contextNames_idOverflow_rejected`,夹具值 `2147483648`(`invalid_*_id_overflow.json`)。反证 = 停用 `if (idValue > int.MaxValue)` ⇒ 该侧转红(承 §4.1 靶面教训,两侧**各一条**夹具,不共用) |
| Q3 | ✅ | ✅ | ✅ **7/7** | `ClinicEnvProjection.cs:42,43,62-64,82-84` | ① `test_projection_nullVocabularies_throwArgumentNull` 覆盖 `:42`+`:43`(两参各一例);② **`test_projection_matchedButNullRoomType_throwsNotDegrades`** 覆盖 `:62-64` —— 构造 `matched:true, roomType:null`(与既有 `UnmatchedEvaluation` 的 `matched:false` **正交**)⇒ 把判据换成 `RoomType == null` 即转红;③ `test_projection_nullContextId_throws` 覆盖 `:82-84` |
| Q4 | ✅ | ✅ | — | `test.cs:1-16` | 单分支(纯注释订正)。反证 = `grep -c "Memo" clinic_name_tables_test.cs` 由 1 → **0**;头注改注真实载体 `clinic_purity_replay_test.cs:448/:484`(经 `grep -n "test_dto_contextIds"` 核实在该文件) |
| Q5 | ✅ | ✅ | ✅ **6/6** | `test.cs:217+` | 拆为 6 条独立 `[Test]`:`{emptyTable, zeroId, duplicateName, duplicateId, emptyName, validTable}`。① 一红一测(定位问题消除);② 第 ④ 条 `ClinicContextNameTable` 独立成测,不再与 room 侧混装;③ 失败消息移入断言正文;④ 补 `Name` 为 null/空(`ClinicNameTables.cs:74-75`) |

**D3 为什么取 (b) 不取 (a)** —— 评审建议 (a) 防御性拷贝。**未采纳**,理由:
`ContextIds` 生产消费方**全库为零**(评审方 U1 + 编排方复算均确认),当下无调用方可污染;
而 (a) 让每次 `GetDto()` 命中都分配一个数组,与 Memoize 的存在理由(避免每帧分配)直接冲突。
取 (b) 的代价是**依赖 42 侧纪律**,故同时补 `<returns>` 契约 + 测试钉住共享语义 ⇒
纪律从「隐含」升为「明写且可观测」。(a) 若将来出现真调用方再引入,届时共享性测试会提醒。

### 8.1 Q1 —— context Binder 八条守卫逐条夹具(承 §4.1 靶面教训)

| # | 守卫 | 源码 | 夹具 / 测试名 |
|---|---|---|---|
| 1 | 缺 `contexts` 数组 | `:81-85` | `invalid_context_names_missing_array.json` → `test_binder_contextNames_missingArray_rejected` |
| 2 | **空词表** | `:97` | `invalid_context_names_empty.json` → `test_binder_contextNames_empty_rejected` ⭐ §4.6 探针钉的就是它 |
| 3 | 行未知键 | `:116` | `invalid_context_names_unknown_key.json` → `test_binder_contextNames_unknownKey_rejected` |
| 4 | `context_id` 缺失 / 非字符串 | `:119` | `invalid_context_names_unknown_key.json` 同族覆盖(读不到必填串) |
| 5 | `id` 缺失 / 非整数 / ≤ 0 | `:124` | `invalid_context_names_zero_id.json` → `test_binder_contextNames_zeroId_rejected` |
| 6 | `id > int.MaxValue` | `:132-136` | `invalid_context_names_id_overflow.json` → `test_binder_contextNames_idOverflow_rejected` |
| 7 | **`context_id` 重复** | `:140` | `invalid_context_names_duplicate_key.json` → `test_binder_contextNames_duplicateKey_rejected` |
| 8 | `id` 重复 | `:146` | `test_binder_contextNames_duplicateId_rejected`(修复轮前即有) |

**分支覆盖 1/8 → 8/8。** ⚠️ 第 ④ 条与第 ③ 条共用一个夹具,严格说仍是「一夹具两守卫」——
登记在此,不宣称 8 条各有独立夹具。
