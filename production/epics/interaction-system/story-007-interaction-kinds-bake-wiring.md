# Story 007: `interaction_kinds.json` 烘焙接线 —— 装载路径落地 / 校验器调用点 / Addressables 进组

> **Epic**: 交互系统
> **Status**: In Progress
> **Layer**: Feature
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-04

## Context

**GDD**: `design/gdd/interaction-system.md`(§数据契约 `4-DC-1…6` · 规则一 路由表 · Tuning `R_INTERACT` 区间)
**Requirement**: TR-interaction-014(四级消歧 + `KindPriority` 全序烘焙表,缺项 = 构建期硬失败)
**AC**: AC-4-15(契约侧已签 —— 本故事补其**装载**半边)

**ADR Governing Implementation**: ADR-014 §二/§三/§五(两阶段烘焙:阶段 1 词法 · 阶段 2 per-schema 绑定 + 白名单/区间/闭集校验;产物 = `*.cooked`;`data-core` 组)· ADR-025 §①(`Gameplay.Presentation` 装配登记集)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(Addressables 6.2+ / `AssetDatabase` 编辑器面 post-cutoff,桌面首跑须实测;判据本身为纯 C# 绑定 + 校验)

---

## 为什么有这个故事

story-006 交付了 `InteractionKindTableValidator`(六条 `4-DC` 校验)+ 13 个承重夹具,但**没有任何烘焙路径调用它** ——
结构侧评审 F-1 记的正是这件事:`grep -rln InteractionKindTableValidator` 只命中类自身与测试,
ADR-014 阶段 2 的位点(`DataBakeMenu.cs`)只烘 item-database。
⇒ **AC-4-15 的「WHEN 阶段 2 烘焙 THEN throw」不可满足**,因为那条路径不存在。
story-006 已把这个缺口显式登记为 **NR-1(禁借绿)**,并把装载器/烘焙接线归本故事。
本故事**只补装载路径** —— 校验器的判据与承重夹具**一字不重做**(那是 story-006 已签的面)。

---

## Acceptance Criteria

- [ ] **AC-4-15-装载(A)** —— `GIVEN` `assets/data/interaction_kinds.json`(作者态)与 `assets/data/interaction_kinds_dimensions.json`(世界维度 `W/H/D`,4-DC-4 ② 的输入),`WHEN` 阶段 2 烘焙,`THEN` ①阶段 1 词法(`JsonStage1Lexer`,零 `JsonConvert`/`JObject`)→ ②逐 schema 绑定(已知键白名单,未知键 = 硬失败)→ ③调 `InteractionKindTableValidator.Validate` → ④任一违例 = 聚合硬失败;全绿 ⇒ 写出 deterministic `interaction_kinds.cooked`
- [ ] **AC-4-15-装载(B)** —— `GIVEN` 上述**六类违例各一份 JSON 夹具**(与 story-006 的 C# 夹具**同判据、不同载体**:证明校验器**真的接在装载路径上**),`WHEN` 烘焙,`THEN` 每条 ⇒ **构建期硬失败**(`BakeValidationException`);且**合法夹具 + 仓库真种子**须**真通过**(反空转门:证这条路径不是恒拒)
- [ ] **AC-4-15-装载(C)** —— `GIVEN` `interaction_kinds.cooked`,`WHEN` 逐字节两次烘焙 / 跨会话烘焙,`THEN` **逐位一致**(ADR-014 §二:产物 deterministic);`ConfigVersion` = 源数据集内容哈希派生(u32,FNV-1a 32),改名/改值/改维度任一 ⇒ 版本变
- [ ] **AC-4-15-装载(D)** —— `GIVEN` 契约行集,`WHEN` 写出后**回读**(写读两端严格镜像 `CookedFormat`),`THEN` 行序 / 字段逐项等于输入;且**回读所得行集喂回校验器仍通过**(闭路)

---

## Implementation Notes

### 落点(仓库既有先例)

| 件 | 落点 | 依据 |
|---|---|---|
| 契约行 → cooked 编码器 | `unity/Assets/Editor.Tools.Bake/InteractionKindCookedWriter.cs`(新) | 与 `ItemDatabaseBaker` 同目录;`Editor.Tools.Bake` 已引 `Gameplay.Presentation` |
| 阶段 2 绑定器 | `unity/Assets/Editor.Tools.Bake/InteractionKindBinder.cs`(新) | 参照 `ItemDatabaseBinder` 形状;**含校验器调用点** |
| cooked → 契约行读方 | `unity/Assets/Gameplay.Presentation/InteractionKindCookedCodec.cs`(新) | 与 `CookedCodec.cs` 同装配(边界层);回读面 |
| 产物落盘 + 进组 | `unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs`(增菜单项) | 复用既有 `EnsureDataCoreGroup` 扫 `*.cooked.bytes` 的机制 |
| 作者态真种子 | `assets/data/interaction_kinds.json` · `assets/data/interaction_kinds_dimensions.json`(新) | GDD §数据契约表 |
| 六类违例夹具 | `tests/unit/interaction/fixtures/invalid_*.json`(新;仓库根,**不进 unity/Assets 树**) | 承 `item-database` 夹具先例 |
| 测试 | `unity/Assets/Tests/EditMode/Interaction/interaction_kinds_bake_test.cs`(新) | 真身落 Assets 树(承 story-006 先例) |

### 关键纪律

- **① 词法器复用,不新写**:`JsonStage1Lexer.TryParse` 是仓库唯一的阶段 1 入口,
  `DateParseHandling = None` / `FloatParseHandling = Decimal` 的 pin 已在其中(ADR-014 §三 RC-7)。
  禁 `JsonConvert.DeserializeObject<T>` / `JObject.Parse`(**由 grep/IL 守卫守**,ADR-014 §Validation)。
- **② `Fix` 承载字段不适用于本 schema** —— `interaction_kinds.json` 全是纯 `int` / 枚举 / `bool`
  (`KindPriority` / `RoutesTo` / `W/H/D` / `DurationOwnerSystemId`),**零 `Fix`**。
  故本故事**不涉 `FixParse`**;`r_interact` 是 GDD 声明的固定常量,住 JSON **数字**(ADR-014 §二 收窄口径)。
  ⚠️ 但**仍须拒 float token**:`KindPriority` 等字段见到 `Float` token = 硬失败(结构化,不靠数值转换)。
- **③ W/H/D 的载体**:GDD §数据契约表把 `W/H/D` 列为 `SlotLinearKey` 项**同表登记**的字段。
  `KindContractRow` 却有 10 行的统一形状 ⇒ 若把 `W/H/D` 放进每行,9 行要填无意义的 0
  (那会把「未登记」与「登记为 0」混为一谈,是静默失效的同族)。⇒ 大部分行从**共享维度表**
  `interaction_kinds_dimensions.json`(`world_w/h/d` + `r_interact`)取维度;**但 `SlotLinearKey`
  行可**在行内自报 `world_w/h/d`**(缺省回落维度表)—— 这是让 **4-DC-4 ② 在装载路径上可达**的
  必要条件(见 Deviations D-1:维度表取 0 会被 4-DC-1 先拦,② 无从独立承重)。
  ⚠️ 本项**不是**「形状裁定」,而是 GDD `:1006` 字面的直接落实 —— 首轮误记,评审 F-2 订正。
- **④ 枚举按明文字符串读入并映射**(ADR-014 §三:21a AC-21a-26「禁 int 编码的 state」先例)——
  `StableIdSource` / `DurationOwner` / `IntentUplink` 皆**明文**,非 ordinal。
  `kind` 例外:GDD §数据契约表明写主键 `kind : int`(`InteractableKind` 枚举序数)⇒ 按 `int` 读,
  但绑定后**仍走 `4-DC-2` 闭集校验**(序数超界 / 缺项即红)。
- **⑤ 校验器调用点是本故事的承重面**:删掉 `InteractionKindBinder` 里的 `Validate(...)` 调用,
  合法夹具会**静默烘出违例产物** ⇒ 负夹具(matcher **不自己判** `4-DC`,只断言「烘焙期抛」)必须转红。
  ⚠️ 这正是 story-006 F-1 的「死代码」判据的**反面** —— 本故事让校验器**真的在路径上承重**。
- **⑥ 属性路由**:GDD 把 `r_interact` 同时锁 4 的选择与 6 的「已发现」触发于**同一烘焙字段**
  (`AC-4-17`)。本故事**只保证单源单值**(产物里就一个);6 侧的消费是系统 6 Epic 的事,
  **不在本故事面内**,不得借绿。

---

## Out of Scope

- **story-006 的校验器判据与 C# 夹具**(已签;本故事只接线,不改判据)
- **真表取值** —— `KindPriority` / `R_INTERACT` 的实际数值(数值轮;本故事落**合法样例**并注明)
- **运行期消费** —— 系统 6 的 latch/幂等/判距复验/唯一 `Append`;`ImmutableInteractionKindTable` 运行期只读表
- **系统 42 的呈现层 / 系统 44 的音频**
- **`OQ-17-3`** —— `ForageSpot`(17)/`Container`(20)的真实时长登记(未裁)

---

## QA Test Cases

- **AC-4-15-装载(A/B)**: 绑定 + 校验接线
  - Given: 合法夹具 + 仓库真种子 + 六类违例夹具各一份
  - When: 阶段 2 烘焙
  - Then: 合法 ⇒ 通过并出产物;违例 ⇒ 各类 `BakeValidationException`
  - Edge: 未知键 / `Float` token / 枚举明文拼错 / `kind` 序数超界

- **AC-4-15-装载(C)**: 确定性 + 版本派生
  - Given: 同一批源文本
  - When: 两次烘焙 / 改一字节再烘
  - Then: 逐位一致 / `ConfigVersion` 变

- **AC-4-15-装载(D)**: 闭路回读
  - Given: cooked 字节
  - When: `InteractionKindCookedCodec` 回读
  - Then: 行序与字段逐项相等,且回读行集喂回校验器仍通过

---

## Test Evidence

**Story Type**: Config-Data
**Required evidence**:
- Config/Data: `tests/unit/interaction/interaction_kinds_bake_test.cs` — must exist and pass(真身 `unity/Assets/Tests/EditMode/Interaction/`)
- Config/Data: `tests/unit/interaction/fixtures/invalid_*.json` — 11 份负夹具(仓库根)

**Status**: [x] Complete(2026-10-04 —— 单轮评审 + 修复轮;**135/132/0/3 绿**;
NOT-RUN 项见 §已知未闭。⚠️ 不得借绿:NOT-RUN 分句**未证**,只是显式登记)

---

## Dependencies

- Depends on: Story 006(`InteractionKindTableValidator` 六条校验 + `KindPriorityTable` 单一真源)
- Depends on: `Editor.Tools.Bake`(`JsonStage1Lexer` / `CookedWriter` / `ConfigVersionUtility` / `BakeValidationException`)
- Unlocks: 运行期 `ImmutableInteractionKindTable`(后续故事)· 系统 6 的 `R_INTERACT` 消费半边

---

## Completion Notes

### 评审轮(单轮 · 2026-10-04)

- **结构侧(lead-programmer)**:CHANGES REQUIRED —— F-1 BLOCKING / F-2 MAJOR / F-3–F-5 MINOR
- **QA 侧(qa-lead)**:REJECT(无 BLOCKING 安全洞 —— 无任何路径让违 4-DC 的表静默烘出)——
  F-0 / F-3 / F-6 / F-7 MAJOR,F-1 / F-5 / F-8 / F-9 MINOR。
  报告原件:`production/qa/evidence/review-interaction-story-007-2026-10-04.md`

**QA 侧修复轮(同轮内关闭)**:

| 判定 | 内容 | 落点 |
|---|---|---|
| F-0 MAJOR | 「六类违例夹具」实为 7,第 7 项(`invalid_position_key_mismatch`)触的是**首轮自造的 4-DC-4 ③** | 同结构侧 F-1:**删除 ③ 及四夹具**,计数回 15 夹具(见 D-2) |
| F-3 MAJOR | `ConfigVersion` 测试的 `.Replace("\"world_w\": 32", …)` **无前置守卫** —— 种子一旦被重格式化即**假原因**转红 | 补 `Assert.AreNotEqual(seedDims, dims, "替换须真改到源文本")` 前置断言 |
| F-6 MAJOR | (C) 证的是**同进程**两次烘焙逐位一致,**跨会话**半边未测 | 测试内注记范围订正 + §已知未闭登记 NOT-RUN |
| F-7 MAJOR | (D) 对**字面量** `3,32,16,8` 校验,非产物烘出的那组 ⇒ 改种子维度后仍绿却校验从未烘过的表 | **维度四元随产物落盘**(writer/reader 头部扩展)· (D) 改读 `ds.RInteract/WorldW/H/D`。MUT-F7(读方丢维度)⇒ 恰 1 红 |
| F-1 MINOR | `invalid_dc4_illegal_stable_id_source` 是**绑定层**拒绝(非 4-DC-4 ①),4-DC-4 ① 端到端零覆盖 | 测试已诚实注明;**① 类型面恒真、结构性不可达**,登记 NOT-RUN |
| F-5 MINOR | 仓根 5 级回溯**正确**(与 ItemDatabase 先例一致);`SeedKinds/SeedDims` 失败抛裸 IO 异常 | 登记为已知(非静默),不阻塞 |
| F-8 MINOR | 4-DC-6 对 `ForageSpot`(`OQ-17-3` 未裁)在**正向绿**里被部分放行 | §已知未闭登记 NOT-RUN |
| F-9 MINOR | `DataBakeMenu.BakeInteractionKinds` 仅 `[MenuItem]` 可达,无自动化覆盖 | 登记为已知;生产调用点由测试直呼 `BakeFromRepo` 同一台机器承重 |

**复跑绿(修复轮后)**:过滤套件 **135 / 132 passed / 0 failed / 3 skipped**(exit 0);
全 EditMode **2339 / 2295 passed / 0 failed / 1 inconclusive / 43 skipped**(无回归;
另 3 条 Interaction skip 与 1 条 inconclusive 均为先存 NOT-RUN/探针)。
变异:见 `unity/Logs/s007-mut-f7.xml`(F-7 修复的可证伪性)。

### Deviations

- **D-1(`SlotLinearKey` 行的 W/H/D 行内载体)** —— GDD `:1026` 的 4-DC-4 ② 字面是
  「`SlotLinearKey` 项须同表登记 `W`/`H`/`D`」,`:1006` 把 `W/H/D` 列为该行的字段。
  首轮实现把维度表的 `W/H/D` **注入每一行** ⇒ ①其余 9 行携带无意义的编造值;
  ②**4-DC-4 ② 在装载路径上结构性不可达**(维度表取 0 ⇒ `min(W,H,D)−1 < r_interact` ⇒
  4-DC-1 先拦),该判据只由 story-006 的**合成 C# 夹具**驱动 —— 即测试侧重实现,非生产接缝。
  评审 F-2 命中。**修复**:`SlotLinearKey` 行可自报 `world_w/h/d`(缺省回落维度表),
  新增 `invalid_slotlinear_row_zero_dimension.json` 夹具 + `test_..._dc4SlotLinearKeyMissingDimensionHardFails`,
  MUT-B′(删 ② 判据体)⇒ 该测试独立转红 ⇒ ② 真正接在生产接缝上。
  ⚠️ **W/H/D 是本表世界维度,不是保存在存档里的逐行模拟量**(证明:4-DC-1 同一份 W/H/D 必须与
  `r_interact` 互相约束 ⇒ W/H/D 是**表级**量,进 `ConfigVersion` 覆盖集,不进存档头迁移面)。

- **D-2(4-DC-4 ③ 已删除 —— 首轮自造判据)** —— 首轮实现新增了
  `ValidatePositionKeyCoRegistration`「用 `SlotLinearKey` 的表须也登记 `StructureId`」。
  评审 F-1 命中:该规则**不见于任何权威件** —— GDD `:1026` 的 4-DC-4 只有 ①(`∈ 枚举`)与
  ②(`SlotLinearKey` 项须有 `W/H/D`)两条;`StructureId` 与 `SlotLinearKey` **同表共存**是
  种子的**偶然性质**,不是被检查的不变量。且该判据**实现不了自己的 doc-comment**
  (W/H/D 逐行独立 ⇒ 无 `StructureId` 行时不存在「缺 W/H/D 的受害行」)。
  ⇒ **删除 ③ 及 `ValidatePositionKeyCoRegistration`**,4-DC-4 判据面收回 ①②(与 GDD 一字对齐),
  连带删 `invalid_position_key_mismatch*` / `legal_position_key_ok*` 四夹具与两条测试。
  ⚠️ 本项是**首轮实现自造的判据**,非 story-006 签约面;删除后 story-006 的 13 条 C# 夹具**全绿**
  (全 EditMode 2339/2295/0/43 无回归)。

### 已知未闭(显式登记,禁借绿)

- 4-DC-6 的 `DurationOwnerSystemId` 「归属方未登记」半边:输入源 `RoutedSystems.Registered`
  是**编译期常量**,无 JSON 注入点 ⇒ **端到端夹具不可构造**。story-006 的 C# 夹具承重,
  本故事的装载路径侧登记为 **NOT-RUN**。
- `schema_version` 两文件交叉一致性:检查存在(`Binder.cs`)但**无夹具**
  (且维度表缺 `schema_version` 时 `ReadU32` 返回 0 ⇒ 守卫短路)⇒ 登记 **NOT-RUN**。
- 聚合多错一次抛出(`Aggregate then throw`)的纪律:无夹具证明「不首个错即停」⇒ 登记 **NOT-RUN**。
- **4-DC-4 ①**(`StableIdSource ∈ 枚举`)端到端:**类型面恒真、结构性不可达** —— 任何编译器可接受
  的装载路径只能产生枚举内值(枚举拼错在**绑定期**即被 `ReadEnum` 拒)。story-006 的 C# 夹具承重,
  本故事装载路径侧登记 **NOT-RUN**(承 QA F-1)。
- **跨会话逐位一致**(AC-4-15 装载(C) 的「跨会话」分句):本故事只证**同进程**两次烘焙逐位一致;
  两次独立编辑器会话的字节一致未测 ⇒ 登记 **NOT-RUN**(承 QA F-6)。
- **4-DC-6 对 `ForageSpot` / `Container`**(`OQ-17-3` 未裁):种子中该行 `suppress=true` +
  `duration_owner_system_id:17`(占位值)**在正向「烘焙成功」测试内**被部分放行 ⇒ 登记 **NOT-RUN**(承 QA F-8)。
- **ADR-014 §三 其余绑定层规则**无夹具(`schema_version` 两文件交叉一致性已列上;
  另含 ADR 强制的 `"3/4"` 日期强制负例、非对象根、缺必填键、u32 域、bool 类型)⇒ 登记 **NOT-RUN**(承 QA §3)。
