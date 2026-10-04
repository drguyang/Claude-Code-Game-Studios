# 评审原件 —— interaction-system story-007(interaction_kinds 烘焙接线 · NR-1)

- **对象**:`unity/Assets/Editor.Tools.Bake/InteractionKindBinder.cs`(新)· `InteractionKindBaker.cs`(新)· `InteractionKindCookedWriter.cs`(新)· `Gameplay.Presentation/InteractionKindCookedCodec.cs`(新)· `InteractionKindTable.cs`(改)· `DataBakeMenu.cs`(改)· `unity/Assets/Tests/EditMode/Interaction/interaction_kinds_bake_test.cs`(新,19 测)
- **权威**:GDD `design/gdd/interaction-system.md` §数据契约 `4-DC-1…6`(`:1023-1028`)· 资产表 `:1006` · AC-4-15 装载半边 / AC-4-17(单源)· ADR-014 §二/§三/§五
- **轮次**:**单轮**(承用户「评审只做一轮」)
- **日期**:2026-10-04
- **方法**:双代理并行(结构侧 = lead-programmer · QA 侧 = qa-lead)+ 主会话独立变异复核
- **实跑基线**:过滤套件 `unity/Logs/interaction-s007-final.xml` = **135 / 132 passed / 0 failed / 3 skipped**(exit 0);全 EditMode `unity/Logs/editmode-s007-full.xml` = **2339 / 2295 passed / 0 failed / 1 inconclusive / 43 skipped**

> ⚠️ **评审方法说明(诚实登记)**:结构侧代理交付完整报告;QA 侧代理交付报告后**触及回合上限**,
> 其首份报告已到手并逐条采纳(下表即其原文判定)。修复轮**不再重评**(承「评审只做一轮」)——
> 下表「修复」列是**修复动作的落点**,不是二次评审结论;修复的**可证伪性**由变异日志独立承担。

---

## 一、原判定

### 结构侧(lead-programmer)—— **CHANGES REQUIRED**

| id | 严重度 | 判定 | 落点 | 修复 |
| --- | --- | --- | --- | --- |
| **F-1** | **BLOCKING** | **4-DC-4 ③ 是首轮实现自造判据** —— `ValidatePositionKeyCoRegistration`(「用 `SlotLinearKey` 的表须也登记 `StructureId`」)**不见于任何权威件**:GDD `:1026` 的 4-DC-4 只有 ①(`∈ 枚举`)与 ②(`SlotLinearKey` 项须有 `W/H/D`);且它**实现不了自己的 doc-comment**(W/H/D 逐行独立 ⇒ 无 `StructureId` 行时不存在「缺 W/H/D 的受害行」) | `InteractionKindTable.cs:313-326` | **已删**:删 ③ + 调用点 + `invalid_position_key_mismatch*`/`legal_position_key_ok*` 四夹具 + 两条测试。4-DC-4 判据面收回 ①②(与 GDD 一字对齐)。见 D-2 |
| F-2 | MAJOR | **4-DC-4 ② 在装载路径上结构性不可达** —— 首轮把维度表 `W/H/D` 注入**每一行** ⇒ 「未登记」与「登记为某值」在产物里不可区分(静默);且维度表取 0 会被 4-DC-1 先拦 ⇒ ② 只由 story-006 的**合成 C# 夹具**驱动(测试侧重实现,非生产接缝) | `InteractionKindBinder.cs:190-191` | **已修**:`BindRows` 的 W/H/D 注入**只对 `SlotLinearKey` 行**,且允许行内 `world_w/h/d` 自报(缺省回落维度表);新夹具 `invalid_slotlinear_row_zero_dimension.json` + `test_ac415loader_dc4SlotLinearKeyMissingDimensionHardFails`。**MUT-B′**(删 ② 判据体)⇒ 恰该测试红 |
| F-3 | MINOR | `ConfigVersion` 测试的 `.Replace("\"world_w\": 32", …)` **无前置守卫** —— 种子被重格式化即静默空转、**假原因**转红 | `interaction_kinds_bake_test.cs:239` | **已修**:补 `Assert.AreNotEqual(seedDims, dims, "替换须真改到源文本")` 前置断言(同 QA F-3) |
| F-4 | MINOR | `invalid_dc4_illegal_stable_id_source.json` 是**绑定层**拒绝(`ReadEnum`),非 4-DC-4 ① ⇒ 4-DC-4 ① 端到端零覆盖 | `:151-158` | **登记 NOT-RUN**:① 类型面恒真、结构性不可达(见 §三) |
| F-5 | MINOR | 无变异日志落盘(six-006 有 `s006-mut*.xml`)⇒ §Implementation Notes ⑤ 的承重宣称**只有断言无演示** | — | **已补**:MUT-A(删 `Validate` 调用 ⇒ 7/7 负夹具红)· MUT-B′ · MUT-F7 全落盘 `unity/Logs/` |

### QA 侧(qa-lead)—— **REJECT**(**无 BLOCKING 安全洞** —— 无任何路径让违 4-DC 的表静默烘出)

| id | 严重度 | 判定 | 修复 |
| --- | --- | --- | --- |
| **F-0** | **MAJOR** | 「六类违例夹具」实为 **7**;第 7 项 `invalid_position_key_mismatch.json` 触的是**首轮自造的 4-DC-4 ③**(GDD 无此条),且 §Implementation Notes ③ 要求记 Deviations 而 Completion Notes 仍 `—` | **已修**:同结构侧 F-1(删 ③ + 其夹具,计数回 15 夹具/11 负);Completion Notes + Deviations 全补 |
| **F-3** | **MAJOR** | 同结构 F-3 —— ConfigVersion 测试无前置守卫,一次格式化即假红 | **已修**(同结构 F-3) |
| **F-6** | **MAJOR** | (C)「同源两次烘焙逐位一致」证的是**同进程**,而故事文自述**跨会话**字节一致 ⇒ 过度宣称 | **已修(范围订正)**:测试内注记 + §已知未闭登记「跨会话」半边 **NOT-RUN** |
| **F-7** | **MAJOR** | (D) 对**字面量** `3,32,16,8` 校验,非产物烘出的那组 ⇒ 改 `interaction_kinds_dimensions.json` 后测试仍绿,却校验一张**从未被烘过**的表(期望值来源在测试内 = 空转) | **已修**:世界维度四元**随产物落盘**(writer/reader 头部扩展 + `InteractionKindDataSet.RInteract/WorldW/H/D`),(D) 改读 `ds.*`。**MUT-F7**(读方丢维度 ⇒ 全 0)⇒ 恰 (D) 红 |
| F-1 | MINOR | `invalid_dc4_illegal_stable_id_source` 是**绑定层**拒绝(非 4-DC-4 ①)⇒ 4-DC-4 ① 零端到端覆盖 | **登记 NOT-RUN**(测试已诚实注明) |
| F-5 | MINOR | 仓根 5 级 `[CallerFilePath]` 回溯**正确**(与 ItemDatabase 先例一致);`SeedKinds/SeedDims` 失败抛**裸 IO 异常**(误导但非静默) | **登记**:非静默,不阻塞 |
| F-8 | MINOR | 4-DC-6 对 `ForageSpot`(`OQ-17-3` 未裁 · `duration_owner_system_id:17` 占位)**在正向绿里被部分放行** | **登记 NOT-RUN** |
| F-9 | MINOR | `DataBakeMenu.BakeInteractionKinds` 仅 `[MenuItem]` 可达,无自动化覆盖 | **登记**:生产调用点由测试直呼 `BakeFromRepo` **同一台机器**承重 |

**QA 侧独立复核确认(bring-forward)**:
- XML 逐条解析:135 元素,3 条 Skip **全属 story-004/005 的 NOT-RUN 登记**(`test_ac413_*` / `test_f43_*` / `test_ac417_*`),**无 story-007 测试被静默跳过**。
- 除 F-1 外,**每一份负夹具都红在其名所指的那一条子句**(逐条对 `Bind` 的错误次序追过):dc1 下/上界 · dc2 缺项 · dc3 平局 · dc5 `routes_to:999` · dc6 suppress+None · unknown-key 拼错 · bad enum · int 编码枚举 · float token ⇒ **无假通过**。
- 正向 `test_ac415loader_repoSeedBakesSuccessfully` **承重**(断言 10 行 + 产物 > 20 B,驱动真 `BakeFromRepo`)。
- `test_ac415loader_roundTripPreservesRowsInOrder` **承重**(11 字段 × 10 行,唯一能捕 writer/reader 字段序漂移的测试)。

---

## 二、修复轮(主会话 —— 不重评,只落修复)

| 修复 | 形态 | 依据 | 可证伪证据 |
| --- | --- | --- | --- |
| **删自造判据 ③** | `InteractionKindTable.cs` 删 `ValidatePositionKeyCoRegistration` + 调用点;删四夹具两测试 | 结构 F-1 / QA F-0 | 全 EditMode 2339/2295/0/43 无回归 |
| **4-DC-4 ② 接生产接缝** | `BindRows` 只对 `SlotLinearKey` 行注入 W/H/D,允许行内自报 | 结构 F-2 | **MUT-B′**:删 ② 判据体 ⇒ 恰 `test_..._dc4SlotLinearKeyMissingDimensionHardFails` 红 |
| **维度随产物落盘** | `InteractionKindCookedWriter.Write(...)` 载荷头加 `rInteract/W/H/D`;`InteractionKindCookedCodec` 读回;`InteractionKindDataSet` 扩四字段 | QA F-7 | **MUT-F7**:读方丢维度 ⇒ 恰 (D) 红(`unity/Logs/s007-mut-f7.xml`) |
| **ConfigVersion 测试前置守卫** | 补 `Assert.AreNotEqual(seedDims, dims)` | 结构 F-3 / QA F-3 | 守卫失败即前置于真断言 |
| **跨会话范围订正** | (C) 测试注记「同进程」+ §已知未闭登记 | QA F-6 | — |
| **NOT-RUN 显式登记** | story §已知未闭五条 | QA F-4/F-8/§3 · 结构 F-4 | — |

### 变异证明(落盘)

| 变异 | 动作 | 结果 |
| --- | --- | --- |
| **MUT-A** | 删 `InteractionKindBinder.Bind` 里的 `Validate(...)` 调用 | **7/7** 4-DC 负夹具转红 ⇒ 校验器**真在装载路径上承重** |
| **MUT-B′** | 删 `ValidateStableIdSource` 的 ② 判据体 | 恰 1 红(`test_..._dc4SlotLinearKeyMissingDimensionHardFails`) |
| **MUT-F7** | 读方把落盘维度读成 0 | 恰 1 红(`test_ac415loader_legalTablePassesValidatorAfterRoundTrip`)· `unity/Logs/s007-mut-f7.xml` |

---

## 三、复跑绿(修复轮后)

```
过滤套件  DaYiJingCheng.Tests.Interaction     135 / 132 passed / 0 failed / 3 skipped   exit 0
全套件    EditMode                            2339 / 2295 passed / 0 failed / 1 inconclusive / 43 skipped
```

- 3 条 Interaction skip = story-004/005 的 NOT-RUN 机检(`test_ac413_*` / `test_f43_*` / `test_ac417_*`),**非** story-007 测试。
- 1 条 inconclusive = `Audio.SettingsExposureTest.test_monoOption_existsWithValidDefault`(先存音频探针,与本故事无关)。
- 43 条 skip 全为先存 NOT-RUN 登记(gamepad 走查 / VR / `OQ` 阻塞项),已逐条核。

**复跑命令**:
```bash
unity test unity --mode EditMode --filter "DaYiJingCheng.Tests.Interaction" \
  --output "$PWD/unity/Logs/interaction-s007-final.xml"
unity test unity --mode EditMode --output "$PWD/unity/Logs/editmode-s007-full.xml"
```

---

## 四、未闭登记(NOT-RUN —— 禁借绿)

1. **4-DC-4 ①**(`StableIdSource ∈ 枚举`)端到端:类型面恒真、结构性不可达(QA F-1)。
2. **跨会话逐位一致**(AC-4-15 装载(C) 的跨会话分句):只证同进程(QA F-6)。
3. **4-DC-6 `DurationOwnerSystemId` 未登记半边**:输入源是编译期常量,无 JSON 注入点。
4. **`schema_version` 两文件交叉一致性**:无夹具,且维度表缺 `schema_version` 时守卫短路。
5. **聚合多错一次抛出**(Aggregate then throw)纪律:无夹具证明「不首个错即停」。
6. **4-DC-6 对 `ForageSpot`/`Container`**(`OQ-17-3` 未裁):种子占位值在正向绿里被部分放行(QA F-8)。
7. **ADR-014 §三 其余绑定层规则**:`"3/4"` 日期强制负例 / 非对象根 / 缺必填键 / u32 域 / bool 类型 —— 均无夹具(QA §3)。

> ⚠️ 以上为**覆盖缺口,非安全洞** —— 所有**已覆盖**子句的夹具各红在己,`Validate` 调用点在
> `errors.Count == 0` 时**必跑**(对已覆盖子句无短路)。缺的是**未覆盖子句**的端到端证明,已逐条显式登记,不得读作已证。
