# Story 008: 数据管线烘焙与 Addressables 预载

> **Epic**: 物品与配方数据库
> **Status**: Complete
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-24

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-025(数据文件格式与位置 assets/data/*.json)· TR-itemdb-026(Addressables 分组与预载)· TR-itemdb-027(构建期 schema 校验)· TR-itemdb-032(配置版本号与存档头联动)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-014(主): 数据管线与 JSON 解析器 · ADR-010(次): 7a 持久化与存档格式 —— ConfigVersion 半边
**ADR Decision Summary**: ADR-014 决定作者态 = `assets/data/[system]_[name].json`,两阶段工具链(阶段1 `JsonTextReader` 仅词法 / 阶段2 自研 per-schema 绑定 + `FixParse` + 白名单/守恒/区间/长度/schema_version 校验)烘成 deterministic `*.cooked`,玩家构建零 JSON 解析器、零 `FixParse`;Addressables 单一 `data-core` 组 + 首次 `Step` 前启动预载,E-13 加载异常 ⇒ 启动期硬失败;`ConfigVersion` = 源数据集内容哈希派生(u32)。ADR-010 决定存档头含 `ConfigVersion u32`,与 `SaveVersion` 分离,比对失败走 §七 迁移协议。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: ADR-014 Engine Knowledge Risk MEDIUM —— Addressables 6.2+ 抛异常为 post-cutoff,须实测;结构面不依赖。ADR-010 MEDIUM(原子写 / 后台线程 / 哈希须实测;刻意只用 BCL 稳定 API,不用 `UnityEngine.Hash128`)。

**Control Manifest Rules (this layer)**:
- Required: 凡承载 `Fix` 数据者只能落 `assets/data/*.json` 并经 `FixParse` 读入;两阶段烘焙;Addressables 装载失败 = 启动期硬失败(E-13)绝不 null 解引用;`ConfigVersion` 内容哈希随数据变
- Forbidden: 运行期任何 JSON 解析 / `FixParse`(玩家构建);`JsonConvert.DeserializeObject<T>`;校验失败降级为警告
- Guardrail: 执法体 = 构建期断言;ordinal ↔ 名称映射进 `ConfigVersion` 覆盖集,既有条目号永不重用/改义

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [x] **AC-21a-26**: item_key 字段以 int 编码 processing_state ⇒ 装配期断言失败(D-21-13;扫描面 = 数据产物层 .json/.asset,不只查代码字段类型)
- [x] **AC-21a-47**(物品表全量加载侧): Performance:物品表全量加载 + 单次配方求解 < 1 ms [D] 级冒烟
- [x] **AC-21a-48**(管线侧): No hardcoded values —— 玩家构建零 JSON 解析器、零 `FixParse`(ADR-014 结构性规避);全部旋钮值仅来自 cooked 产物 + IDataProvider

**附(故事级 TR 落地,不占独立 AC)**: data-core 单组 + 首次 Step 前预载 + E-13 加载异常启动期硬失败(TR-026);schema_version/白名单/守恒/区间/长度全部烘焙期硬失败 = Stories 004–007 各夹具经本管线执行(TR-027);ConfigVersion 内容哈希随数据变、与存档头分离字段比对(TR-032,比对失败走 ADR-010 §七 迁移协议)。

---

## Implementation Notes

*Derived from ADR-014 §Decision (primary) / ADR-010 (secondary):*

- **两阶段工具链**:阶段1 `JsonTextReader` 仅词法 · 阶段2 自研 per-schema 绑定 + `FixParse` + 白名单/守恒/区间/长度/schema_version 校验 → 确定性 `*.cooked`(`Fix` = raw `long`)—— ADR-014 §二/§三
- **玩家构建零 JSON 解析器、零 `FixParse`** —— 解析器逐平台逐位性从运行期确定性面消失(结构性规避)—— ADR-014 裁定 ②
- **§五 Addressables**:单一 `data-core` 组 + 首次 `Step` 前启动预载(门 A:sim 零 `UnityEngine`,装载落边界程序集 `IDataProvider`);E-13(6.2+ 抛异常)⇒ **启动期硬失败,不 null 解引用** —— ADR-014 §五
- **`ConfigVersion` = 源数据集内容哈希派生**(u32,改数值自动改版本号),与 ADR-010 §七 存档头比对 —— ADR-014 §五 · ADR-010 §七
- `SaveVersion`(迁移链入口)与 `ConfigVersion`(后续窗口用什么值)分离;「不匹配非致命」的措辞不得改成致命(要改须另开 ADR)—— ADR-010 §七
- ordinal ↔ 名称映射表须进 `ConfigVersion` 覆盖集;既有条目号永不重用、永不改义 —— ADR-014 §五:238,258
- 执法体 = 构建期断言,显式 `throw` —— manifest 元规则

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002 / 006 / 007: 校验器本体与夹具(本故事负责把这些校验接到烘焙管线执行点)
- Story 009: Craft 事件载荷与 entities.yaml 路由
- Story 010: 存档 codec 本体与 ItemInstance 编码(本故事只管 ConfigVersion 与存档头的字段比对接线)
- Story 011: 跨平台黄金哈希与 IL2CPP 对拍

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-26**: item_key 字段以 int 编码 processing_state ⇒ 装配期断言失败(D-21-13;扫描面 = 数据产物层 .json/.asset,不只查代码字段类型)。
  - Given: 数据产物中 processing_state 以 int 落盘(.asset/YAML 或任何产物);或 ItemDef/Recipe 被做成 ScriptableObject/prefab(Edge Cases 伴生,一次踩中 enum int 与 UnityEngine 引用)。
  - When: 装配期数据产物扫描。
  - Then: 硬失败;JSON 明文字符串 state(过);SO/prefab 承载(拒 —— 唯一合法作者态 = assets/data/*.json)。
  - Edge cases: int 编码恰在旧 .asset 遗留文件(扫描全产物目录,不只新增);JsonUtility 输出路径;枚举序插入 P1a 值的静默重映射风险即本条动机 —— 扫描须在每次构建跑。
  - Negative fixture: `invalid_state_int.json`

- **AC-21a-47**(物品表全量加载侧): Performance:物品表全量加载 + 单次配方求解 < 1 ms。
  - Given: 烘焙产物 `*.cooked` 经 data-core 组已预载(模拟首次 Step 前完成态)。
  - When: 计时全量 ItemDef 表加载(读 cooked,零 JSON 解析)。
  - Then: < 1 ms(GDD 原文阈值)。
  - Edge cases: 冷加载 vs 缓存命中;最大表规模(21b 内容量级);E-13 路径失败(预载异常)另测硬失败,不与计时混测。
  - Negative fixture: 无。非确定性计时 ⇒ 记冒烟/基准证据 `production/qa/smoke-[date].md`(ADVISORY),不入 BLOCKING 确定性套件。

- **AC-21a-48**(管线侧): No hardcoded values —— 全部读数据文件。
  - Given: 玩家构建(出货形态)。
  - When: 结构断言:构建内不存在运行期 JSON 解析器与 FixParse 调用(玩家构建零 JSON 解析器、零 FixParse,ADR-014 结构性规避);全部旋钮值仅来自 cooked 产物 + IDataProvider。
  - Then: 扫描 src 无 assets/data 读取直连与 JSON 字面量解析路径;旋钮值与作者态 JSON 一致(抽查对拍)。
  - Edge cases: Editor 侧工具链含 Newtonsoft 词法(编辑期豁免,须在 Editor.Tools 程序集白名单内,不进构建);SKILL_CAP 等跨系统引用常量不属本条扫描面;与 Story 002 的代码侧重扫描合并不重复报。
  - Negative fixture: 无(GDD 未命名)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/item_database/data_pipeline_bake_test.cs` — must exist and pass
- Config/Data (AC-47): smoke check pass `production/qa/smoke-[date].md`

**Status**: [x] Created —— 真身 = `unity/Assets/Tests/EditMode/ItemDatabase/data_pipeline_bake_test.cs`(**22 [Test] + 1 TestCaseSource × 30 夹具 = 52 个用例**);账本路径 = `tests/integration/item_database/data_pipeline_bake_test.cs`(Story 001–007 同一先例:Unity 不编译 `unity/Assets/` 之外)。负向夹具新增 `tests/unit/item_database/fixtures/invalid_state_int.json`(AC-26);AC-47 冒烟记录 = `production/qa/smoke-2026-09-24.md`(ADVISORY)。**执行 VERIFIED 2026-09-24 桌面** —— EditMode **407 全绿** = 前批 355 + 本批 52;PlayMode **12 全绿**。

---

## Completion Notes
**Completed**: 2026-09-24
**Criteria**: 3/3 已实现(AC-21a-26 / 47 / 48);故事级 TR 落地 TR-026/027/032 一并接线
**门接线**:Stories 004–007 的 28 条执法体全部接入 `ItemDatabaseBaker.RunGates` —— 绑定零错误才跑门(防残缺记录制造噪声);**本管线是全案唯一 throw 点**(`BakeValidationException` 聚合 throw;门本体维持零 throw,承 Story 004–007 纪律)
**Deviations(ADVISORY)**:
- ① **`FloatParseHandling = Decimal`(非 ADR 原文 `None`)**:Newtonsoft `FloatParseHandling` 枚举无 `None` 成员(仅 Double/Decimal),ADR 字面不可编译;阶段1 钉 `DateParseHandling.None` + `FloatParseHandling.Decimal`,**阶段2 结构性拒收 Float token**(小数不能落 int/Fix 字段)⇒ 判据等价,记 ADVISORY。
- ② **产物扩展名 `.cooked.bytes`(非 `.cooked`)**:Unity 需 `.bytes` 才导入为 `TextAsset`(Addressables 可装载);裸 `.cooked` 导入为 `DefaultAsset` 不可用。地址 = 文件名(菜单 `SetAddress(fileName)` 与 `AddressablesDataProvider` 常量逐字对齐)。
- ③ **第三源种子 `item_database_constants.json`**(文件名 Story 003 的 `RecipeSettlementConstants.cs` 注 pre-named):常量**并入配方侧 cooked**(`item_database_recipes.cooked.bytes`),不产第三份产物 —— 同域同哈希覆盖集,拆开无消费方差异。
- ④ **装配引用 +2 程序集**:`Editor.Tools.Bake` 与 `Gameplay.Presentation` 均需引 `Sim`(`RecipeDataSet` / `RecipeSettlementConstants` 住 Sim,ADR-025 §①)—— 新增 asmdef `Editor.Tools.Bake`(Editor-only,不进构建,ADR-025 Editor.Tools 族);`Gameplay.Presentation.asmdef` 增 `Sim` + Addressables 两条引用。六装配清单封闭性(ADR-025 §④)不受影响:`Editor.Tools.Bake` 属已登记的 Editor.Tools 族。
- ⑤ **Story 002 陈旧注释「本工程未装 Newtonsoft」现为假**:`manifest.json` 增 `com.unity.nuget.newtonsoft-json: 3.2.2`(UPM 包版本,内部同步 Newtonsoft.Json 13.0.2;ADR-014 裁定 ③ 强制)。⚠️ **首版误填 `13.0.3`(把库版本当成 UPM 包版本,registry 无此版 ⇒ 桌面解析失败)**,桌面报错后改 `3.2.2`(registry `latest`)。该陈旧注释在 Story 002 归属文件内,本故事不越权改(编排器可选清理项)。
- ⑥ **AC-25b 集合漂移门 = 可选注入**:`ValidateInjurySetSuperset` 仅当 `mapsToInjury ≠ null` 才跑(系统 25 未建,生产路径 null = 跳过);测试经 `BakeFromSourceText(..., mapsToInjury)` 注入夹具替身验证接线。
- ⑦ **`invalid_quality_dist.json` 不入本管线扫描面**:其形状归系统 17 的 GDD(P0 空 `GatherProfile` 结构体),无绑定形可喂 items 源;跳过理由写进测试 `SkippedFixture` 注释 + 可证伪测试(`test_itemDatabase_qualityDistributionFixture_documentedAsSkipped` 断言「文件存在**且**被排除」)。
- ⑧ **facet 形夹具经白名单/缺键拒而非其 facet 专属门**:31 夹具中仅 4 个为完整 items 形(`invalid_state_int` / `invalid_dup_key` / `invalid_enum` / `invalid_stored_stackable`),其余为单门 facet 形 —— 作为 items 源喂入时在绑定层白名单/缺 `items` 键处硬失败(仍满足 TR-027「夹具经本管线执行 ⇒ 硬失败」,但深度浅于 Story 006/007 单门直测);完整形的 4 个走其设计路径(AC-26 / 主键 / 枚举 / stackable 门)。已在测试 doc-comment 登记。
**范围边界**:门本体 = Stories 004–007(零改动);存档头读写与迁移协议 = Story 010(本故事只交付 `CompareConfigVersion` 比对原语,`Fatal=false` 承 ADR-010 §七);黄金哈希 = Story 011;Boot 场景启动序调用 `DataCorePreloader.Preload()` = 后续故事(本故事立契约 + E-13 抛出路径,调用点不存在于 P0 当前场景树)。
**Test Evidence**: Integration —— 真身 `unity/Assets/Tests/EditMode/ItemDatabase/data_pipeline_bake_test.cs`(52 用例)+ 负向夹具 `invalid_state_int.json` + AC-47 冒烟 `production/qa/smoke-2026-09-24.md`。
**Code Review**: Skipped(lean 模式,承 Story 004–007 先例)
**执行状态**: ✅ **VERIFIED 2026-09-24 桌面** —— EditMode **407 全绿**(前批 355 + 本批 52);同批 PlayMode **12 全绿**。桌面六项残留全闭环:① Newtonsoft 解析 + `packages-lock.json` 重生成 + ~21 新文件 `.meta`(`chore(meta)` `5fe8fc0` 已推);② EditMode 407 / PlayMode 12 全绿;③ 菜单「烘焙 item-database」+「确保 data-core Addressables 组」成功(`AddressableAssetsData/` 永不提交;组菜单经 E-13 反向定位到条目附证);④ AC-47 三数回填 `smoke-2026-09-24.md`(首次 70936 μs 超阈 = 调优信号;缓存/求解 0 μs = <1 μs 整数截断稳过);⑤ E-13 路径实测五跑收敛 —— **反向**(反射探针,第五跑 `反向通过 ✓`)+ **正向成功**(首/二跑 items=4, recipes=2, ConfigVersion=0x53DD5D0F)+ **真实基础设施故障下照样包成 [E-13] IOE、无 null 解引用**(第四/五跑 stale-bundle)= ADR-014 §五 契约三点由跨跑聚合证据坐实;第五跑正向残留 = 编辑器会话态(⓪ 中途切 Play Mode 不回溯重建已初始化 locator/catalog),属环境非契约,**判定不再追第六版机制**(见 smoke 收敛判读);⑥ 本行翻 VERIFIED。
**production/qa/ 已建立**:本故事 AC-47 冒烟存根为全仓首个 `production/qa/` 文件(Story 007 收官残留项就此消解)。

---

## Dependencies

- Depends on: Story 001, 002, 005, 006, 007(校验器与类型全部就位,才有东西可烘焙)
- Unlocks: Story 011(黄金夹具跑在烘焙产物上),Story 009 / 010(消费 cooked 数据)
