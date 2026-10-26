# Story 001: P0 品种闭集与采集数据装载校验

> **Epic**: 采集
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-26

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

- [x] **AC-17-01b**: `GIVEN` P0 品种集,`WHEN` 与 21a `item_key` 表比对,`THEN` = {柳树皮, 毛地黄, 金鸡纳树皮, 止血草} 四者(规则一)。✅ **前置已解除**(21a 三品种行 2026-10-26 落地并重烘);夹具走 21a **真 binder** 绑定三表后双向比对
- [x] **AC-17-08b**: `GIVEN` `RegrowWindow` / `Σw` / `CapTable` 的非法值(`≤ 0` / 全零 / 长度 ≠ 61 / 越界),`WHEN` 装载,`THEN` **硬失败**(显式 `throw`,非 `Assert`)(F-17-1 / F-17-3 装载期校验)。✅ Binder 8 条 + Sim 16 条 + `Σw` 4 条,每条守卫各有夹具
- [x] **AC-17-16**: `GIVEN` `SEASON_MULT[]`,`WHEN` 装载,`THEN` 表长 = 5 的 `SEASONS_PER_YEAR`(不等 ⇒ 装载期硬失败,非越界读)。✅ Binder 4 条 + Sim 3 条
- [x] **AC-17-14**: `GIVEN` `CapTable[]`,`WHEN` 单测,`THEN` 长度 61 · 单调不减 · `CapTable[0] ≥ 1` · 各值 ≤ `MAX_QUALITY`(防「升级变差」+ 防 `clamp` 抛异常)。✅ Binder 5 条 + Sim 5 条(**两侧各一套**,证伪轮 M1/M2/M3 曾暴露 Sim 侧零覆盖,已补)。⚠️ **2026-10-26 补强**:「各值 ≤ `MAX_QUALITY`」这条此前**只在合成常量下被验过**(真实 `MAX_QUALITY=5` 而 `cap_table` 最大只有 2 ⇒ 判据恒不触发);现由 `test_w3_maxQuality_isReadFrom21a_notHardcoded` 用**收紧的 21a 常量**(1)钉住 — 见证伪轮第四轮 W1/W2
- [x] **AC-17-05c** [L]: `GIVEN` 21a 交付的 `quality_character[]`,`WHEN` 人工走查,`THEN` 取值在本系统语义成立(无「陈放」「虫蛀」类采后属性)。✅ **走查已执行并签核(2026-10-26)**:20/20 词语义判据通过,主创 / 医学顾问 + QA 复核均 Approved(§六)· GDD 注⑥
  - **2026-10-26 更新:走查已执行 + 已签核,语义判据 20/20 通过,AC 记绿。**
    (本条初版写的是「但 AC 仍不记绿」—— 签核完成后已兑现,原文保留于下方沿革)
    走查件 = `production/qa/evidence/foraging-05c-2026-10-26.md`。
    执行中发现 **两处真实缺陷并已修复**:
    ① **15/20 个词原无用户审定出处** —— 21a 原本只有 `willow_bark` 一行,本轮补行时
    `foxglove`/`cinchona_bark`/`yarrow` 的 15 词由 AI 自拟;用户 2026-10-26 当场逐词审定
    (foxglove 全保留 · cinchona 改 q5 · yarrow 改 q1)。
    ② **两处采后/错位语义**:`cinchona_bark` q5「皮厚**油润**纹密」可读作陈放析油 ⇒ 改
    「皮厚**纹密色赤**」;`yarrow` q1「茎**断花落**」的「落」指向离株散落态、与注⑦
    「采前可读线索」冲突 ⇒ 改「茎**倒花萎**」。改后 21a 已重烘(`items=1289 B`,`cfg=0x6AAC8AD8`)。
    ✅ **签核已完成(2026-10-26)**:走查件 §六 由 **dr_guyang** 以主创 / 医学顾问
    与 QA 复核双重身份签 Approved。⇒ **本 AC 已勾,记绿。**
    ⚠️ **口径**:签署人同一人兼任两角色(单人开发形态,`coding-standards.md` 明许),
    **不构成第三方独立复核** —— 将来引用本件时不得称其为「独立复核」。
  - **改词后回归(2026-10-26)**:全量 EditMode `total=5174` · `passed=5139` · **`failed=0`** ·
    `skipped=35`(`unity/Logs/w-05c-full.xml`),与 S20-3 收口基线**逐位相同**(本轮未增删测试);
    漂移门 `drift[item_database_items.cooked.bytes]` = **Passed**(现烘 ≡ 入库产物)。
    ⚠️ **回归绿 ≠ 本 AC 记绿**(记绿的依据是 §六 签核,不是回归)—— 回归只证
    「改词没打破既有判据」,不证「词义被人工认可」。

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

> **QA plan 对齐(2026-10-25 /qa-plan sprint-20)** —— 证据件:`unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs`(账本树 `tests/unit/foraging/` 并列);估 ~12 条。四负面夹具须**具名 throw**(非 Debug.Assert);权重 int 非 Fix(D-21-17),`Fix` 字段 JSON 写字符串(FixParse);`SEASONS_PER_YEAR` 为烘焙期常量。**AC-17-01b 挂 21a item_key 行 ⇒ `Ignore("BLOCKED-BY: 21a item_key 行未落地")` 显式挂起,禁记绿**;**AC-17-05c [L] = 人工走查件**(ADVISORY,证据落 `production/qa/evidence/foraging-05c-<date>.md`,主创签核;回写完成 ≠ 走查通过,GDD 注⑥)。QA plan 全文:`production/qa/qa-plan-sprint-20-2026-10-25.md`。

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
  - ✅ **执行记录(2026-10-26)**:走查已跑并签核 ⇒ 本 AC 已勾。实际被查词表 **20 档**
    (4 品种 × 5,非本 Given 行写的单品种 5 档 —— 21a 于 2026-10-26 起有四品种行),
    其中 2 词因采后 / 离株语义改词。详见 `production/qa/evidence/foraging-05c-2026-10-26.md`。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs` — must exist and pass
- [L](AC-17-05c): 人工走查证据 `production/qa/evidence/foraging-05c-<date>.md`(ADVISORY 性质,执行归实现/走查轮)

**Status**: [x] Created and passing —— `unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs` · **44/44 passed**(`Logs/foraging-supp.xml`,2026-10-26)
**账本树并列**:`tests/unit/foraging/`(账本约定路径;工程主体在 `unity/Assets/Tests/EditMode/Foraging/`)
**证伪轮**:三轮共 35 条变异 —— 第一轮 M1–M15(补件前,抓出 3 条真缺口)+
第二轮 M1/M2/M3/M16–M27(15 条,补件后全红)+ 第三轮 B1–B5(5 条,Binder 分支拆分)。
逐条反证见 `production/qa/evidence/impl-s20-3-foraging001-2026-10-26.md` §4.2 / §4.5
**AC-17-05c [L]**:`production/qa/evidence/foraging-05c-2026-10-26.md` ——
✅ **已执行 + 已签核(2026-10-26)**:20/20 词语义判据通过;GDD 前提「词集经用户审定」
补齐(15 词当场审定);2 词改词并重烘。§六 由 dr_guyang 以主创 / 医学顾问 + QA 复核
双重身份签 Approved ⇒ **记绿**。⚠️ 同一人兼任两角色,**非第三方独立复核**。
改词后全量回归 `5174/5139/0/35`(`unity/Logs/w-05c-full.xml`),漂移门 Passed。

---

## Dependencies

- Depends on: item-database story-001(`FixParse` / 舍入契约,Complete)、item-database story-002(schema 类型,Complete);`world_resources.json` 作者面 = 关卡工具(ADR-022,编辑期,不构成运行时阻塞);数值轮(表内容,机制不阻塞)
- Unlocks: Story 002(抽取管线消费装载后的表)、Story 004(余量求值消费 `SEASON_MULT` / `RegrowWindow`)

---

## Completion Notes

**Completed**: 2026-10-26
**Criteria**: **5/5 passing**(AC-17-01b / 08b / 14 / 16 / 05c)—— AC-17-05c 于 2026-10-26
补做人工走查并签核(20/20 语义判据通过 · 15 词补审定 · 2 词改词重烘)

### 交付面(10 文件 —— 含 W-3 甲案补烘的 4 项)

| # | 文件 | 性质 |
|---|---|---|
| ① | `assets/data/foraging_constants.json` | 新增(作者态,17 自家调参表) |
| ② | `unity/Assets/Sim/Foraging/ForagingConstants.cs` | 新增(Sim 侧运行期视图 + 15 条构造期拒收) |
| ③ | `unity/Assets/Sim/Foraging/QualityDistributionShape.cs` | 新增(`Σw ≥ 1` 形状校验器) |
| ④ | `unity/Assets/Editor.Tools.Bake/ForagingConstantsSchema.cs` | 新增(键白名单 + 读取辅助) |
| ⑤ | `unity/Assets/Editor.Tools.Bake/ForagingConstantsBinder.cs` | 新增(唯一构建期校验点) |
| ⑥ | `unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs` | 新增(46 测 = 44 + 2 条 M-4) |
| ⑦ | `unity/Assets/Editor.Tools.Bake/ForagingConstantsCookedWriter.cs` | 新增(W-3: cooked 编码器) |
| ⑧ | `unity/Assets/Editor.Tools.Bake/ForagingConstantsBaker.cs` | 新增(W-3: 烘焙入口,跨表读 21a `MAX_QUALITY`) |
| ⑨ | `unity/Assets/Tests/EditMode/Foraging/foraging_bake_wiring_test.cs` | 新增(W-3: 6 条接线夹具 —— 5 条初版 + 1 条跨表耦合假绿修复) |
| ⑩ | `unity/Assets/DataCooked/foraging_constants.cooked.bytes` | 新增(**入库产物**,352 B) |

**另改 2 文件**:`DataBakeMenu.cs`(产物名常量 + MenuItem)、
`cooked_drift_all_artifacts_test.cs`(`BakedArtifacts` +1、`pinnedDeclared` 24 → 25)。

### 测试证据

- **夹具级**:`52/52/0/0`(`Logs/w-rebuild.xml`,2026-10-26)
  —— 44 既有 + 2 条 M-4 + **6 条 W-3**(5 条初版 + 1 条证伪轮抓出的假绿修复件
  `test_w3_maxQuality_isReadFrom21a_notHardcoded`)
- **全量回归**:**`5174/5139/0/35`**(`Logs/w-final-full.xml`,XML `03:04:52Z`–`03:06:36Z`)
  —— 基线 `5121/5086/0/35`(`f7-full.xml`,`09a75cc`),
  **增量 +53 = 52 条新夹具 + 1 条 `TestCaseSource` 展开**,`failed=0` 零回归
  ⚠️ 上一读数 `5173/5138/0/35` 是**补件前**的(51 条夹具时),**不得当作现值**
- **证伪轮**:第一轮 M1–M15 + 第二轮 15 条 + 第三轮 5 条 + **第四轮 W1–W7** ——
  逐条反证见 `production/qa/evidence/impl-s20-3-foraging001-2026-10-26.md`
  §4.2 / §4.5 / **§4.4.9**

> ⚠️ **第四轮首跑 W1/W2 双双 `red=0`(假绿),已修夹具后转红**(2026-10-26)。
> 根因:真实 `MAX_QUALITY` 恰为 5,而 17 的 `cap_table` 最大值只有 2 ⇒
> 「写死 5」与「真读 21a」产物**逐位相同**,原夹具集合对该属性零判别力。
> 修法:新增跨表耦合夹具 —— 把 21a 的 `max_quality` 改成 1(17 的表一字不动),
> 使 `cap_table` 的 2 成为违例。这是 S12-4 Falsifiability Gate 抓出的**第五条假绿形态**。

#### Falsifiability Gate 判据表(S12-4 模板 · 第四轮 W1–W7)

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 定义域收窄(跨表耦合) | `Read21aMaxQuality` 真读 + 喂进 `Bind` | 停用 `Read21aMaxQuality`(写死 5)⇒ `test_w3_maxQuality_isReadFrom21a_notHardcoded` 红 |
| 定义域收窄(跨表耦合) | `Bind(json, maxQuality)` 用真值驱动校验 | 改成 `Bind(json, 5)` ⇒ 同上转红 |
| 阈值边界(头部契约) | magic = `'D'` | 改 `'X'` ⇒ `test_w3_cookedHeader_matchesCookedFormat` 红 |
| 阈值边界(头部契约) | offset 4 = `CookedFormat.FormatVersion` | 改 `99u` ⇒ 同上红 |
| 阈值边界(头部契约) | offset 12 = `configVersion` | 改 `0u` ⇒ 同上红 |
| 阈值边界(头部契约) | offset 16 = `payload.Length` | 改 `+1` ⇒ 同上红 |
| 一次性/同源 | Baker 与 Menu 的产物名同值 | 改 `_v2` 后缀 ⇒ `test_w3_cookedFileName_matchesMenuConstant` 红 |

**Gate 结果 = PASS**(7/7 转红,`SUMMARY: 7 / 7 turned red`,SHA-256 逐字节还原)。
⚠️ **首跑 2/7 转红(W1/W2 `red=0`),修夹具后才 7/7** —— 两个数字都须记录,见证据件 §4.4.9。

### 证伪轮抓出的真缺口(本卡最重要的过程记录)

**第一轮 M1/M2/M3 三条 RED=0** —— 不是变异打错靶,是**真缺口**:
`test_ac1714_*` 三条测的是 **Binder 侧**,而这三条守卫在 **Sim 侧构造器**。
`ForagingConstants` 15 条守卫只有 3 条有 Sim 侧夹具。

**修法**:补 17 条夹具(Sim 13 + Binder 4),第二轮 M1/M2/M3 **全部转为 RED=1**。
**M10 另暴露一处分支缺口**:`df.Raw <= 0 || df.Raw > Fix.OneRaw` 是合取判据,
停用整条只有上界分支转红 ⇒ 下界分支零覆盖;补 `test_ac1708b_decayFloorZero_rejected`,
第三轮 B1/B2 按分支拆开各验一条。

> ⚠️ **这条教训值得记**:「两侧都测」写在文件头注释里,但**注释不是执行体**。
> 只有证伪轮把它变成了可观测事实 —— 与 `coding-standards.md` §「某件有门」的自述须给
> `file:line` + 可跑命令是同一条纪律。

### Deviations

- **ADVISORY · TD-foraging-01**:GDD `:541` / `:711` 的 `CapTable` 公式与散文互斥
  (公式只产出 `{1,2}`,散文承诺值域到 5 且四切点)。用户 2026-10-26 裁定「照字面式落地」,
  已登债册(P0 面 **N**),归数值轮修。
- **ADVISORY · 数据通道缺口**:`quality_distribution` 在 21a 四行 `gather_profile` 中**全不存在**,
  21a binder 拒收其任何字段 ⇒ **既没写进去也没读出来**。本件补了 17 侧形状校验器
  (`QualityDistributionShape`),但生产数据侧仍无字段可校验 —— 缺口归 21a 的形状落地轮。
  ⚠️ **不得**因「没有数据」删掉该校验器:`Σw ≥ 1` 是 GDD `:306-309` 点名的**唯一**防线。
- **卡面笔误已订正**:Note 1 把 `quality_distribution` 列进「17 自家调参表」;
  用户裁定「按 GDD:住 21a」,卡面已就地加注。

### ⚠️ 待裁缺口:烘焙接线不存在 → **✅ 已裁并补齐(甲案,2026-10-26)**

> **原文不删,就地加注**:以下实测是**补齐前**的状态,保留作过程记录。
> 用户裁定 **甲案 —— 本 story 内补齐**,已于同日实施完毕,见 §「W-3 补齐」与证据件 §四之四。

补齐前的实测:`ForagingConstantsBinder` **没有 Baker 调用它**,`DataBakeMenu` 里**零 `Foraging` 注册**,
`unity/Assets/DataCooked/` 下**无 `foraging*` 产物**,三个新类**零生产调用方**(只被夹具调用)。

ADR-014 的链路 = 作者态 JSON → 词法 → **Binder 校验** → **Baker 写 `*.cooked`** → `IDataProvider`。
补齐前本件只完成**阶段 2(Binder)**,缺 **Baker + MenuItem 注册**两环。
全案 24 个 Binder 中,本表是**唯一只有 Binder 没有 Baker 的**。

~~**为什么不擅自补**:卡面无「Files to Create」节;补 Baker + MenuItem + `.cooked` 是独立接线工作,
且 `.cooked` 入库即成为须随源表同步的派生物。⇒ **须裁**:(甲)本 story 内补齐 /(乙)另立 story /(丙)登债册。~~

> ⚠️ 该缺口**当时不影响**已交付判据(AC-17-08b / 14 / 16 判的是「装载期校验行为」),
> 但**补齐后**判据更强了一层:现在连「校验在**生产烘焙路径**上真的被调用」也有夹具钉住
> (`test_w3_badSourceTable_bakerRejects` + 证伪轮 W1)。

### W-3 补齐(甲案实施,2026-10-26)

| 项 | 内容 |
|---|---|
| 新增文件 | `ForagingConstantsCookedWriter.cs`(编码器)· `ForagingConstantsBaker.cs`(烘焙入口)· `foraging_bake_wiring_test.cs`(5 条夹具) |
| 新增产物 | `unity/Assets/DataCooked/foraging_constants.cooked.bytes` = **352 B**,`ConfigVersion = 0x1CFDA66D` |
| 菜单注册 | `DataBakeMenu.BakeForagingConstants()` —— `[MenuItem("大医精诚/数据管线/烘焙 foraging_constants(17 采集调参表)")]` |
| 漂移门登记 | `cooked_drift_all_artifacts_test.BakedArtifacts` +1(带现烘委托)、`pinnedDeclared` **24 → 25** |
| 跨表联合校验 | Baker **从 21a 读 `MAX_QUALITY`**(`ItemDatabaseBinder.BindAll` 真 binder),使「17 的表 × 21a 的常量」在生产路径上被联合校验 |

**落盘实测**:`MAX_QUALITY = 5(读自 21a)` —— 是**读出来的**,不是写死的。

### 评审 W-1 / W-2 不修(用户裁定,登记不隐藏)

| 项 | 判定 | 处置 | 理由 |
|---|---|---|---|
| **W-1** 构造器 95 行、CC≈16 | FAIL(WARNING) | **不修** | 「≤40 行 / CC<10」**不是项目硬规定** —— `coding-standards.md` / `control-manifest.md` 零命中,仅 `/code-review` skill 的通用清单。全仓同类:`DiseaseRegistryBaker` 397 行、`ClinicMachineTablesBake` 322 行 |
| **W-2** `Bind` 约 150 行 | FAIL(WARNING) | **不修** | 同上。且拆分会让 15 条守卫行号全变 ⇒ 35 条证伪轮锚点失效,须整轮重跑 |

> ⚠️ 这是**已知的形态偏差**,不是「没问题」。若将来升为硬规定,两条须重新评估。

### 评审四条 MINOR 全修

| # | 修法 | 钉它的夹具 |
|---|---|---|
| **M-1** | `QualityDistributionShape.cs:40` 注释「四条」→「**五条**判据」并逐条列出 | 纯注释,读数证明(5 条 `throw new ArgumentException`) |
| **M-2** | 真实表测试改用 `Read21aMaxQuality()`(走 21a 真 binder);`MaxQuality` 常量加注「合成夹具专用」 | `test_realTable_loadsAndSatisfiesAllShapeInvariants` |
| **M-3** | `:105` catch 过滤器改单 `catch (ArgumentException ex)`(后者派生自前者) | `test_qualityDistribution_*` 4 条 |
| **M-4** | 5 个 `ReadRequiredXxx` 的缺键分支改**静默**(承 `InputAxisTuningBinder.BindRequiredFix:93`),缺键由 `RequiredKeys` 循环统一报一次 | **新增 2 条**:`test_m4_missingRequiredKey_reportedExactlyOnce` / `test_m4_missingMultipleKeys_allReportedInOnePass` |

⚠️ **M-4 有真实风险面**:调用方若不先跑 `RequiredKeys` 检查,缺键会**静默丢失**。
已核实 Binder 的 8 个 `ReadRequired*` 调用都在 `RequiredKeys` 循环**之后**(`:117-120`),
且 8 个 `if (ReadRequired...)` 各自独立、**不短路**,「一次报全」不受影响。

### Code Review

✅ 已完成 —— `lead-programmer`,**APPROVED WITH SUGGESTIONS**(零 BLOCKING)。
8 条硬约束中 7 条 PASS、1 条 FAIL(W-1/W-2,已裁定不修)。
评审结论与逐条订正见证据件 §四之四。
⚠️ 评审自陈未及项:未独立重跑测试(数字引自卡面)、GDD F-17-1/2/3 未逐行读、
control-manifest Foundation 规则未读原文。
⚠️ **评审的「甲案理由③」(下游 story-002/004 经 IDataProvider 消费 cooked)经实测不成立** ——
story-002/003/004/005 **零命中** `cooked` / `IDataProvider`。甲案仍成立,但理由是
「生产路径可证伪」而非「下游已依赖」。

### 收口判定(2026-10-26 · /story-done · lean 模式)

**Verdict = COMPLETE**(2026-10-26 签核后更新;收口当时的判定为 `COMPLETE WITH NOTES`,
因 AC-17-05c 尚未签核 —— 原文保留于下「未通过项与不记绿声明」段)

> **为何可从 WITH NOTES 升为 COMPLETE**:`/story-done` 的三条 COMPLETE 条件
> ① 全部 AC 通过(**5/5**)② 无 blocking 偏差(3 条 ADVISORY,已登记)
> ③ Falsifiability Gate = PASS(**7/7**)—— 三条件当前全部满足。
> 升格的**唯一**依据是 §六 签核完成;测试/代码未再变更。

| 项 | 结果 |
|---|---|
| Acceptance Criteria | **5/5 passing**(AC-17-01b / 08b / 14 / 16 / **05c**)—— 05c 于 2026-10-26 补做并签核 |
| Test Evidence(Logic) | ✅ 两文件齐:`foraging_data_load_test.cs`(46)+ `foraging_bake_wiring_test.cs`(6) |
| Falsifiability Gate | ✅ **PASS** —— `7 / 7 turned red`;⚠️ 首跑仅 2/7(见下) |
| 偏差 | 无 BLOCKING;3 条 ADVISORY(已登记于 §Deviations) |
| Code Review | ✅ `lead-programmer` APPROVED WITH SUGGESTIONS(用户 2026-10-26 确认) |
| 评审报告原件 | ✅ **已落盘** —— `production/qa/evidence/review-foraging-001-2026-10-26.md`(同日补落,含原判定 → 修复落点 → 验证命令三列 + 双列结账 + Branch 列) |
| Manifest 时效 | ✅ 本卡 `2026-10-15` = 当前 `control-manifest.md` `:5` **2026-10-15**,不过期 |

**未通过项与不记绿声明(收口当时,2026-10-26 早)**:
- **AC-17-05c [L]**(人工走查 `quality_character[]` 语义)→ **NOT-RUN**,
  `production/qa/evidence/foraging-05c-*.md` **不存在**。数据回写已完成(2026-09-25)
  ≠ 走查已执行。**本卡不因此项记绿,亦不因回写借绿。**

> 🔁 **同日后续(2026-10-26,原文不删)**:该 NOT-RUN **已被补做并签核**。
> 走查件 `production/qa/evidence/foraging-05c-2026-10-26.md` 现已存在,结果:
> - **语义判据 20/20 通过**;GDD 前提「词集经用户审定」**此前不成立**,本轮补齐 15 词审定;
> - **抓到 2 处真实缺陷并修复**(`cinchona_bark` q5「油润」→「纹密色赤」;
>   `yarrow` q1「茎断花落」→「茎倒花萎」),21a 已重烘;
> - 改词后全量回归 **`5174/5139/0/35`** · 漂移门 **Passed** · 零回归;
> - **§六 已签**:dr_guyang 以主创 / 医学顾问 + QA 复核双重身份 Approved(2026-10-26)。
>
> ⇒ **该阻塞已解除,AC-17-05c 已记绿,本卡 AC 计数 4/5 → 5/5。**
> ⚠️ 签署人同一人兼任两角色 —— 满足 AC 的「人工走查 + 签核」要求,但**不构成
> 第三方独立复核**,将来引用时不得如此措辞。

**证伪轮首跑 2/7 的如实记录**:
W1/W2 首跑 `red=0` 是**真实的假绿夹具**(BLOCKING 级),已按 S12-4 修夹具后第二跑 7/7 转红。
⚠️ **两个数字都须保留** —— 只记「7/7」会把修完的结果当成一开始就有的结果。
详见 `production/qa/evidence/impl-s20-3-foraging001-2026-10-26.md` §4.4.9。

**测试证据三元组**:
`2026-10-26 03:04:52Z–03:06:36Z`(XML UTC) · 批次 **S20-3 收口** · commit **本次提交**
全量 `5174 / 5139 / 0 / 35` · 基线 `5121 / 5086 / 0 / 35`(`09a75cc`)· **+53 = 52 夹具 + 1 `TestCaseSource` 展开**
复算:`grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity/Logs/w-final-full.xml | head -1`

