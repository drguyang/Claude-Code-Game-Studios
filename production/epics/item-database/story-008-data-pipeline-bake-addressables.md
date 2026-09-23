# Story 008: 数据管线烘焙与 Addressables 预载

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: (set by /dev-story when implementation begins)

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

- [ ] **AC-21a-26**: item_key 字段以 int 编码 processing_state ⇒ 装配期断言失败(D-21-13;扫描面 = 数据产物层 .json/.asset,不只查代码字段类型)
- [ ] **AC-21a-47**(物品表全量加载侧): Performance:物品表全量加载 + 单次配方求解 < 1 ms [D] 级冒烟
- [ ] **AC-21a-48**(管线侧): No hardcoded values —— 玩家构建零 JSON 解析器、零 `FixParse`(ADR-014 结构性规避);全部旋钮值仅来自 cooked 产物 + IDataProvider

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

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001, 002, 005, 006, 007(校验器与类型全部就位,才有东西可烘焙)
- Unlocks: Story 011(黄金夹具跑在烘焙产物上),Story 009 / 010(消费 cooked 数据)
