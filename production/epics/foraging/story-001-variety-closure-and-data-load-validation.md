# Story 001: P0 品种闭集与采集数据装载校验

> **Epic**: 采集
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/foraging.md`(规则一 · F-17-1/F-17-3 装载期校验 · UI Requirements 词表 · AC A/B 组装载行)
**Requirement**: TR-foraging-007(采集资源点数据的 IDataProvider 驻留/切片策略,⚠️ partial —— OQ-17-7 未裁)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线与 JSON 解析器 · ADR-006(次): 定点域边界数据契约 · ADR-015(次): 世界几何单一整数格
**ADR Decision Summary**: 作者态 `assets/data/*.json` → 构建期两阶段烘焙(阶段1 仅词法、阶段2 自研绑定 + FixParse + 白名单/区间/schema_version 校验)→ `*.cooked`;运行期只读烘焙产物、零 JSON 解析器;`Fix` 字段 JSON 写字符串;装载失败 = 启动期硬失败(E-13:Addressables 6.2+ 抛异常,不 null 解引用);资源点/节点定义住确定性整数逻辑层(单一 `WorldPos` 格)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 判据本体为纯 C# 装载校验(LOW);抬到 MEDIUM 的唯一面 = Addressables 6.2+ `IDataProvider` 装载边界的 post-cutoff 抛异常行为(承 ADR-014 §五,须实测但不影响裁决形状)。

**Control Manifest Rules (this layer)**:
- Required: 全部 `Fix` 字段经 `FixParse` 解析(JSON 字符串形如 `"3/4"`);`weight` / `qty` 计数为 `int`;非法输入(表长 / Σw / `RegrowWindow` / `CapTable`)⇒ 显式 `throw`(非 `Debug.Assert`);`ConfigVersion` 由源数据集内容哈希派生(ADR-014 §五)
- Forbidden: 运行期读 JSON 文本;float/double 字面量进入调参表或解析路径;`JsonConvert.DeserializeObject<T>`;null 兜底 / 默认表值
- Guardrail: 表长契约 = 硬失败非越界读(`SEASON_MULT` 长度必须 = `SEASONS_PER_YEAR`;`CapTable` 长度必须 = 61 = `SKILL_CAP+1`);数值内容归用户数值轮,本故事只钉**形状与校验**

---

## Acceptance Criteria

*From GDD `design/gdd/foraging.md`, scoped to this story:*

- [ ] **AC-17-01b**: `GIVEN` P0 品种集,`WHEN` 与 21a `item_key` 表比对,`THEN` = {柳树皮, 毛地黄, 金鸡纳树皮, 止血草} 四者(规则一)。⚠️ 21a `item_key` 行归实现轮,**在该行落地之前本条不得记绿**(数据侧前置 = 外抛,非本件缺陷)
- [ ] **AC-17-08b**: `GIVEN` `RegrowWindow` / `Σw` / `CapTable` 的非法值(`≤ 0` / 全零 / 长度 ≠ 61 / 越界),`WHEN` 装载,`THEN` **硬失败**(显式 `throw`,非 `Assert`)(F-17-1 / F-17-3 装载期校验)
- [ ] **AC-17-16**: `GIVEN` `SEASON_MULT[]`,`WHEN` 装载,`THEN` 表长 = 5 的 `SEASONS_PER_YEAR`(不等 ⇒ 装载期硬失败,非越界读)
- [ ] **AC-17-14**: `GIVEN` `CapTable[]`,`WHEN` 单测,`THEN` 长度 61 · 单调不减 · `CapTable[0] ≥ 1` · 各值 ≤ `MAX_QUALITY`(防「升级变差」+ 防 `clamp` 抛异常)
- [ ] **AC-17-05c** [L]: `GIVEN` 21a 交付的 `quality_character[]`,`WHEN` 人工走查,`THEN` 取值在本系统语义成立(无「陈放」「虫蛀」类采后属性)。✅ 数据回写已完成(2026-09-25,P0 词集零采后词,语义筛已过);**走查执行仍未跑,禁借回写记绿**(GDD 注⑥)

---

## Implementation Notes

*Derived from ADR-014 Implementation Guidelines:*

1. 作者态输入三个面:`gather_profile`(住 21a `item-database.md` Schema C,本故事只**消费**不定义)、`world_resources.json`(资源节点定义,54 关卡工具作者,ADR-022 §三导出契约)、17 自家调参表(`assets/data/foraging_*.json`:`quality_distribution` / `CapTable` / `SEASON_MULT` / `DECAY_FLOOR` 等)。三者均走 ADR-014 两阶段烘焙 ⇒ 玩家构建零解析器。
2. 运行期经边界程序集 `IDataProvider` 读 `*.cooked`(`Fix` = raw `long`);门 A 约束:sim 程序集 `"noEngineReferences": true`,装载落边界侧(ADR-025 清单)。
3. 装载期校验清单(逐条具名异常,非裸 `Exception`):Σw ≥ 1(`quality_distribution` 权重全零拒收);`RegrowWindow > 0`;`CapTable` 形状四断(AC-17-14);`SEASON_MULT` 表长 = `SEASONS_PER_YEAR`;`season_index` 消费口径 = 5 的 F-5.2(本件不重定义)。
4. `quality_distribution` 权重是 **int**(非 Fix);`SEASON_MULT` / `DECAY_RATE` 等是 `Fix` ⇒ JSON 写字符串。承 ADR-006 D-21-17 口径(`weight`/计数 int 移出 Fix 解析集)。
5. 草木灰节点(火堆灰烬)= 非植物品种,走同三条 Kind(规则一注),本故事只需节点定义可承载「灰烬节点」类目,不为其开品种位。
6. 数值全未裁不阻塞本故事:校验器以形状与合法域为准;差分发货验证待数值轮。
7. OQ-17-7(资源点全图驻留 vs chunk 切片)**未裁** ⇒ 本故事按「驻留」最小实现(骨架全图 + `data-core` 预载,同 ADR-015 §四 对导航格的处置),切片决策外抛登记,不私设第二真源。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002:`CDFWalk` / `SplitMix64` / `QualityCap` 抽取管线(消费本故事装载的表,不做装载)
- Story 004:`EffCapacity` / `YieldDecay` 的**求值**(本故事只校验 `RegrowWindow` 等输入形状)
- Story 005:失败收敛与动作整合
- item-database epic:`gather_profile` schema 与烘焙主执行(承 item-database story-001/002 已建 `FixParse` + schema 类型)
- 关卡工具(54):`world_resources.json` 的作者与导出(ADR-022,不在运行期故事面)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-17-01b**: P0 品种闭集比对。
  - Given: 21a `item_key` 表已含 4 条目(柳树皮/毛地黄/金鸡纳树皮/止血草);17 侧 P0 品种常量表。
  - When: 单测比对两集合并 / 差集。
  - Then: 完全相等,且 P0 集 ⊆ `category = raw_material` 行;草木灰不以「品种」身份出现在该集合(节点类目 ≠ 品种)。
  - Edge cases: 第 5 条目混入 ⇒ 失败;21a 行未落地 ⇒ 本测试以 `Ignore("BLOCKED-BY: 21a item_key 行未落地")` 显式挂起,**禁记绿**。
- **AC-17-08b**: 非法表值装载硬失败(四类负面夹具各一)。
  - Given: `invalid_regrow_zero.json` / `invalid_sumw_zero.json` / `invalid_captable_len60.json` / `invalid_cap_over_max.json` 合成夹具(不碰真实表)。
  - When: 走装载路径(烘焙期阶段 2 校验 + 运行期防御断言双侧)。
  - Then: 每例 `throw` 具名异常并终止(非告警、非 `Debug.Assert` 可跳过)。
  - Edge cases: `CapTable[0] = 0` ⇒ 失败;`CapTable[0] = 1` ⇒ 过;长度 62 ⇒ 失败;Σw = 1 单档 ⇒ 过。
- **AC-17-16**: 季节表长契约。
  - Given: `SEASON_MULT` 长度 = `SEASONS_PER_YEAR ± 1` 两例 + 正例。
  - When: 装载。
  - Then: 正例过;两负例硬失败;越界读路径不存在(索引前查表长)。
  - Edge cases: `SEASONS_PER_YEAR` 为烘焙期常量,禁运行期可变。
- **AC-17-14**: `CapTable` 四性质。
  - Given: 正表 + 单调破坏 / 越 `MAX_QUALITY` 破坏两负夹具。
  - When: 单测逐断言。
  - Then: 长度 61 · `CapTable[i] ≤ CapTable[i+1]` · `CapTable[0] ≥ 1` · `≤ MAX_QUALITY`;负夹具装载即拒。
  - Edge cases: 全表恒等(平,合法);60 级 > 0 级(升,合法)。
- **AC-17-05c** [L]: 采后词语义走查。
  - Given: `quality_character[]` 五档词表(枯脆细碎/皮薄色暗/条匀皮厚/条肥色正/皮厚丝丰)。
  - When: 人工走查语义(「本系统 = 采集当下可得属性」判据)。
  - Then: 无采后属性词;证据归档 `production/qa/evidence/foraging-05c-<date>.md`(主创签核)。
  - Edge cases: 走查未跑 ⇒ 本 AC 保持未勾;回写完成 ≠ 走查通过(禁借绿,GDD 注⑥)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs` — must exist and pass
- [L](AC-17-05c): 人工走查证据 `production/qa/evidence/foraging-05c-<date>.md`(ADVISORY 性质,执行归实现/走查轮)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: item-database story-001(`FixParse` / 舍入契约,Complete)、item-database story-002(schema 类型,Complete);`world_resources.json` 作者面 = 关卡工具(ADR-022,编辑期,不构成运行时阻塞);数值轮(表内容,机制不阻塞)
- Unlocks: Story 002(抽取管线消费装载后的表)、Story 004(余量求值消费 `SEASON_MULT` / `RegrowWindow`)

---

## Completion Notes

*(placeholder — to be filled at story completion)*
