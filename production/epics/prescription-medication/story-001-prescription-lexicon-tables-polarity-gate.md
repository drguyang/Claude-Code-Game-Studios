# Story 001: 处方表与本草词表 —— 双表 polarity 硬门

> **Epic**: 处方用药
> **Status**: Complete ✅ 2026-10-06
> **Layer**: Foundation
> **Type**: Config-Data
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/prescription-and-medication.md`(规则四 作者态表 · 规则五 9 注册表=判定真源,11 表=镜像 · 规则八 呈现=古籍功效词 · §11-DC 表 DC-1…DC-7 · OQ-11-2)
**Requirement**: TR-prescription-004(作者态表 = 药→`action_id:int` + 呈现措辞键;21a 的 ItemDef 无 action_id —— 临床语义不住物品目录)· TR-prescription-005/016(polarity 双表一致性构建期硬门;判定真源归 9,11 表按「action→单一 polarity」收窄声明)· TR-prescription-011(indications/contraindications 只呈现不拦不扣;病种 id 不进呈现层)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(两阶段烘焙;`polarity_11(a)==polarity_9(a,d)` ∀(a,d) 的执行体 = **ADR-014 阶段 2**,AC-11-07/DC-4)· ADR-024(Kind 已登记,写者=11)· ADR-013(呈现层零 `disease_id` ⇒ 词表输出 = 古籍功效词,`PresentationDtoGuard` 递归)· ADR-006(`FixParse` 唯一入口,Fix 字段 JSON 写字符串)
**ADR Decision Summary**: 主键 `item_key` → `action_id`(处置 id 枚举序数)+ `polarity`;词表 `assets/data/materia_lexicon.json` 每味药恰一条功效词,零自动匹配;P0 逐药核过无跨极性(柳树皮对症/金鸡纳对因/毛地黄对症;**草木灰 2026-09-20 扩容行须实核**);三条判据条件性不可执行时**显式登记不静默**(11-DC 注)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(数据/校验面;管线复用 disease-simulation story 003 阶段 2 载体)
**Engine Notes**: 烘焙工具 Editor.Tools 族不进构建;运行期只见 `*.cooked`。

**Control Manifest Rules (this layer)**:
- Required: DC-1…DC-7 逐条构建期校验(throw 级);`item_key ⊂ ItemDef` 闭集(DC-1);`polarity ∈ {causal, symptomatic}`(DC-3);词表每药恰一条(DC-5);`0 < DOSE_BASE ≤ 65536×hi`(DC-7)
- Forbidden: 运行期读 JSON 文本;`JsonConvert.DeserializeObject<T>`;Fix 字段写数字字面量;`action_id`/`polarity` 手填进 21a(临床语义不住物品目录)
- Guardrail: **DC-2 半定悬置显式登记** —— `action_id` 闭集 = 处置注册表全值,取决于 `OQ-11-2`(归属已裁归 11,枚举定值待数值/内容轮)⇒ 该子句 NOT-RUN,禁借绿;**DC-6 依赖 9 侧 `NOISE_BAND_9` 常量**(BL-2,O-11→9)⇒ NOT-RUN

---

## Acceptance Criteria

*From GDD `design/gdd/prescription-and-medication.md`, scoped to this story:*

- [ ] **AC-11-02**[A] BLOCKING:零重定义 —— `drug_profile` 全字段(`drug_potency`/`half_life`/`axis_offset_by_quality[]`/`quality_axis`/`dose_range`)不在 11 的任何表出现;11 表只有 `item_key→action_id+polarity` 与措辞键(外键存在性断言)
- [ ] **AC-11-07**[A] BLOCKING:双表 polarity 构建期交叉硬门 —— `polarity_11(a) == polarity_9(a,d)` ∀(a,d);不一致 ⇒ **throw**(非 warning);夹具:同一 action 在 9 表两病种跨极性 ⇒ 红;草木灰行实核用例(2026-09-20 扩容)
- [ ] **AC-11-20**[A] BLOCKING:本草词表存在性 —— 每味药**恰一条**古籍功效词(DC-5);零自动匹配逻辑(不出现「遍历 indications[] 比对」的代码路径,承 TR-prescription-001 的 11 侧);词表缺药 ⇒ 构建失败
- [ ] **DC-1**[A]:处方表 `item_key` ⊂ `item-database` 的 `ItemDef` 闭集(外键闭合,构建期)
- [ ] **DC-3**[A]:`polarity ∈ {causal, symptomatic}` 二值闭合(枚举序数,无第三值/无空)
- [ ] **DC-7**[A]:`0 < DOSE_BASE ≤ 65536 × hi`(量纲健全;`DOSE_BASE` 值归数值轮,不等式形状即判)
- [ ] **DC-2**[A] **NOT-RUN(BLOCKED-BY-OQ-11-2 枚举定值)**:校验机制 + 负夹具以影子枚举证明可跑;证据文件头显式写 NOT-RUN
- [ ] **DC-6**[A] **NOT-RUN(BLOCKED-BY-O-11→9 `NOISE_BAND_9` 未立,BL-2)**:可感知地板的构建期机制化(联动 story 002 的 AC-11-19)
- [ ] **呈现零病种 id**[A]:`materia_lexicon.cooked` 与处方表进呈现层的字段集经 `PresentationDtoGuard` 递归 ⇒ 无 `disease_id`/indications 病种键外露(功效词 = 烘焙期转出的字符串,病种 id 止步于构建期)
- [ ] **烘焙确定性**[A]:同源集双跑字节相等;中文功效词 UTF-8 稳定序

---

## Implementation Notes

*Derived from 规则四/五/八 + 11-DC:*

1. 两文件:`assets/data/prescription_actions.json`(主键 `item_key`)与 `assets/data/materia_lexicon.json`(主键 `item_key`,恰一条);`OQ-11-2` 归属已由本 GDD 裁归 11 —— 枚举 `EmergencyAction`/处置 id 的类型来源仍随 DC-2 悬置登记。
2. 交叉校验的两侧读入均在**阶段 2**(9 的 `disease_registry.json` + 11 的 `prescription_actions.json` 同 build 上下文);实现落点复用 disease epic story 003 管线,新增一条跨文件规则(规则号对齐 DC-4)。
3. `indications[]/contraindications[]` 留在 **9 的注册表侧**(判定真源),11 只借道烘焙期转出功效词;**不**复制进 11 表(否则违 AC-11-02 零重定义)。
4. 负夹具每条 DC 一件「只违该条」;DC-2/DC-6 两件以影子依赖证明机制,不静默跳过。
5. 词表内容(每味药的功效措辞)是**叙事内容**,归 `/design-review` 流程与医学身份纪律(记忆库:医学身份错才改身份层)—— 本 story 只建承载与校验。

## Out of Scope

- [Story 002]: F-11.1/F-11.2 求值(读 cooked,不校验)
- [Story 003]: 载荷构造与写流
- [Story 005]: 词表的呈现元件(42 侧纸面)
- `drug_profile` 字段本身的 schema(item-database epic 21a,已交付)

## QA Test Cases

*Written at story creation(lean mode).*

- **跨极性红**: 合成 action `A` 在 9 表两病种分别 causal/symptomatic ⇒ throw(AC-11-07)。
- **零重定义**: 扫描 11 表字段集 ∌ `drug_potency`/`half_life`/`axis_offset`(AC-11-02 机械半边)。
- **词表唯一性**: 某药两条/零条 ⇒ 构建失败(DC-5)。
- **外键**: `item_key="unknown_herb"` ⇒ 红(DC-1);词表漏药 ⇒ 红(AC-11-20)。
- **DOSE_BASE 边界**: `DOSE_BASE=0` 或 `>65536×hi` ⇒ 红(DC-7)。
- **DTO 洁净**: 呈现字段递归扫无病种键(TR-prescription-011 半边)。

## Test Evidence

**Story Type**: Config-Data
**Required evidence**: `unity/Assets/Tests/EditMode/PrescriptionMedication/prescription_tables_test.cs` — must exist and pass(DC-2/DC-6 子项 NOT-RUN 须在证据文件头显式写出)
**Status**: [x] Done 2026-10-06 — 真身 `unity/Assets/Tests/EditMode/PrescriptionMedication/prescription_tables_test.cs`(12 测);评审原件 `production/qa/evidence/review-prescription-story-001-2026-10-06.md`

---

## Dependencies

- Depends on: item-database epic(21a `ItemDef`/`drug_profile` 闭集)、disease-simulation epic story 003(阶段 2 管线 + 9 的注册表 = polarity 真源侧)、story 002 of disease(kindgen,处置 id 若走 Kind 面)
- Unlocks: Story 002(求值读表)、Story 003(载荷字段来源)、Story 004(域检查的 `dose_range`/换算份数)、Story 005(词表呈现)

## Completion Notes
