# Story 001: 世界格与可走性基础 —— LATTICE_SIZE / K_TERRAIN_MAX

> **Epic**: 世界与生态区
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(§Detailed Rules R-6 单一整数格 · §Formulas F-6-1 `LATTICE_SIZE ≥ SPEED_MAX×MAX_DT×SAFETY_MARGIN` · F-6-2 `K_TERRAIN_MAX = max K_speed`(空表硬失败,禁手填)· A 组 AC-6-01…09)
**Requirement**: TR-worldeco-001 的几何侧根 —— `WorldPos=(i32,i32,i32)` 单一整数格为地形/建造/掉落/导航/房间格共用(承 ADR-015 §三);6 消费 ADR-022 关卡工具作者产出的烘焙逻辑层数据,不自建几何
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主): 世界几何(手工烘焙固定世界 · 单一整数格);ADR-006: 定点域边界
**ADR Decision Summary**: 运行期只加载确定性整数逻辑层,纯视觉层(Terrain,float)采样禁入 sim(破 ADR-006 边界);建造槽位与地形共用同一套格;`Fix` 只用于非空间模拟量,空间量一律整数格。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯整数数据结构 + 烘焙装载,门 A 程序集(`Sim`/`Sim.Contracts`),不触及 post-cutoff API;EditMode 可测。

**Control Manifest Rules (this layer)**:
- Required: `WorldPos` 分量 `int`(i32);`LATTICE_SIZE` / `SPEED_MAX` / `MAX_DT` / `SAFETY_MARGIN` 全部 > 0 且 `SAFETY_MARGIN > 1` 为装载期硬断言;`K_TERRAIN_MAX` 由 K_speed 表**派生**(max),非手填
- Forbidden: `float` / `double` 出现在逻辑格几何;chunk 局部坐标系(ADR-015 §四 —— chunk 只服务流式/脏块,非坐标原点);视觉层 Terrain 采样进 sim
- Guardrail: K_speed 表为空 ⇒ 装载期 `throw`(硬失败,非运行期兜底);世界坐标量程 `W` 溢出 int64 安全区 ⇒ 构建期 `throw`(承 ADR-016 / enemy F-27-1 同型断言对象 = 量程,非半径)

---

## Acceptance Criteria

*From GDD `design/gdd/world-and-ecozones.md`, scoped to this story:*

- [ ] `WorldPos` 全仓唯一定义在 `Sim.Contracts`(整数三分量),所有系统(1/6/23/27/13/25)共用同一类型;不存在第二套坐标或 chunk 局部系(AC-6-01…03)
- [ ] 防隧穿关系 `LATTICE_SIZE ≥ SPEED_MAX × MAX_DT × SAFETY_MARGIN` 作为**装载期硬断言**存在:任一因子 ≤ 0 或 `SAFETY_MARGIN ≤ 1` ⇒ 构建/装载期 `throw`(F-6-1,AC-6-04)
- [ ] `K_TERRAIN_MAX == max(K_speed 表)`,且**由代码派生**而非手填常量:改表 ⇒ `K_TERRAIN_MAX` 自动跟随(可证伪守卫:塞一个更大的 K_speed 进夹具表,断言 max 更新)(F-6-2,AC-6-05)
- [ ] K_speed 表为空 ⇒ 装载期 `throw`(空表硬失败,禁退化为 0 或 1)(AC-6-06)
- [ ] 逻辑层几何(生态区多边形 / POI 格 / 资源点 / 导航格 / 建造槽)全部来自 `world_*.cooked` 烘焙产物;6 的运行期零几何生成、零视觉层采样(AC-6-07)
- [ ] 全静态断言 `WorldPos` / 几何 struct 类型集无 `float`/`double`(递归反射,AC-6-08/09)

---

## Implementation Notes

1. 数据结构:`WorldPos`(住 `Sim.Contracts`,承 ADR-025 清单)、`TerrainCell { walkable: bool, kSpeedIdx: int }`、几何加载入口 `IWorldGeometry`。
2. `LATTICE_SIZE` 与速度上限的乘积用 int64/checked 求值(防 32 位溢出),断言消息点名四个因子与实测值。
3. 关卡工具(ADR-022)产出 `world_geometry.json` / `world_terrain.json` → ADR-014 两阶段烘焙;本 story 只做**读侧**与断言,不做导出工具。
4. `SPEED_MAX` / `MAX_DT` 的归属:量来自 1 玩家控制器与数值轮 —— 本 story 消费其声明值,不重定义。
5. 数值(LATTICE_SIZE 具体格长、K_speed 各行)归用户数值轮;story 只建派生与合法性机制。

---

## Out of Scope

- [Story 002]: `EcozoneOf(cell)` 多边形查询(消费本 story 的几何加载)
- [Story 003]: POI 定义/状态
- [Story 004]: chunk 激活与流式
- 关卡工具导出器与 C1–C6 一致性检查本体(归 ADR-022 Tooling epic)
- 视觉层 Unity Terrain 资产(纯表现,不进 sim)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 合法参数(LATTICE=4, SPEED_MAX×MAX_DT=3, SAFETY_MARGIN=1.5) | 装载 | 通过 |
| TC-2 | SAFETY_MARGIN=1(等于而非大于) | 装载 | `throw`(硬边界,消息点名) |
| TC-3 | K_speed 表 = {2,5,3} | 读 K_TERRAIN_MAX | == 5;塞 {2,7,3} ⇒ == 7 |
| TC-4 | K_speed 表 = ∅ | 装载 | `throw` |
| TC-5 | WorldPos 夹具分量含 float 的反面类型 | 反射扫描 | 断言失败(负面夹具) |
| TC-6 | 极端坐标 (2^31−1, ·, ·) 参与量程断言 | 校验 | 构建期 `throw`(W 超安全区) |

**Edge cases**: 负 LATTICE_SIZE;SAFETY_MARGIN 恰好为 1(须拒);int32 分量差平方溢出(承 enemy F-27-1 的 int64 纪律,此处只做断言对象登记)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/WorldEcozones/world_lattice_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: ADR-025 `Sim.Contracts` 程序集(含 `WorldPos`)· ADR-014 烘焙装载管线(`data-core` 预载)
**Unlocks**: Story 002(多边形查询)· Story 003(POI 格)· Story 004(chunk)· 13/27 的整数导航格消费

---

## Completion Notes
