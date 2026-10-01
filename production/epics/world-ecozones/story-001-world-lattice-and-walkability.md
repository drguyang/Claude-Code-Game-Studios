# Story 001: 世界格与可走性基础 —— LATTICE_SIZE / K_TERRAIN_MAX

> **Epic**: 世界与生态区
> **Status**: Complete — AC-6-01/04/05/07①/08 全绿(42 例);AC-6-07 正半(几何装载 binding)与 AC-6-06 后半按登记落点推 Story 002 / 装载 epic(见 Completion Notes 残留)
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-10-01

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

- [x] `WorldPos` 全仓唯一定义在 `Sim.Contracts`(整数三分量),所有系统(1/6/23/27/13/25)共用同一类型;不存在第二套坐标或 chunk 局部系(AC-6-01…03)
- [x] 防隧穿关系 `LATTICE_SIZE ≥ SPEED_MAX × MAX_DT × SAFETY_MARGIN` 作为**装载期硬断言**存在:任一因子 ≤ 0 或 `SAFETY_MARGIN ≤ 1` ⇒ 构建/装载期 `throw`(F-6-1,AC-6-04(a))
- [x] `SAFETY_MARGIN == 1` 取等号即失败(零余量点是 1,不是 F-6-1 的取等号)(AC-6-05)
- [x] `K_TERRAIN_MAX == max(K_speed 表)`,且**由代码派生**而非手填常量:改表 ⇒ `K_TERRAIN_MAX` 自动跟随(值级可证伪守卫:夹具抬一行 `K_speed`,派生量与 F-6-1 下界同步随动)(F-6-2,**AC-6-07** ① 值级;② 源头白名单归 CI 分析器,见 Out of Scope)
- [x] K_speed 表为空(或 null)⇒ 装载期 `throw`(`max` over 空集无定义 —— F-6-2 硬失败;AC-6-08 的「基准唯一」只是**间接**蕴含非空,GDD :549 明示须显式断言)
- [x] 逻辑层几何(生态区多边形 / POI 格 / 资源点 / 导航格 / 建造槽)只以**整形量**承载,零 `float`/`double`/`Vector*`/UnityEngine 入类型图(AC-6-01 正面白名单 + AC-6-02;烘焙产物 `world_*.cooked` 的装载真源见 Implementation Note ①)
- [x] **GDD AC-6-08 追加(BLOCKING)**:K_speed 表**每行** `K_speed` / `K_accel` / `K_decel` **全 > 0** + `K_speed == 1`(基准地貌)**恰好一行**。机械执行点:「不可通行」只能走 `walkable = false`,不得表达为速度档 0 —— 后者会静默绕过 1 的隧穿检测(速度 0 ⇒ 永不跨格 ⇒ 断言恒绿)。

## Implementation Notes

1. 数据结构:`WorldPos`(住 `Sim.Contracts`,承 ADR-025 清单)、`TerrainCell { walkable: bool, kSpeedIdx: int }`、`WorldLatticeParams` + `TerrainSpeedRow` + `WorldGeometry`(均住 `Sim`,门 A 内)。
2. F-6-1 判定写成 `displacement = SPEED_MAX × MAX_DT`(int64,m/s × ms = mm)与 `LATTICE_SIZE / SAFETY_MARGIN` 比较 —— **不预乘** `SAFETY_MARGIN`(免 2^93 回绕成假绿),整除向下与 `> L/M` 精确等价(取等号方向一致,GDD :506)。
3. 关卡工具(ADR-022)产出 `world_geometry.json` / `world_terrain.json` → ADR-014 两阶段烘焙;本 story 只做**读侧**与断言,不做导出工具。
4. `SPEED_MAX` / `MAX_DT` 的归属:量来自 1 玩家控制器与数值轮 —— 本 story 不另起裸值入口(见下方修订 ②)。
5. 数值(LATTICE_SIZE 具体格长、K_speed 各行)归用户数值轮;story 只建派生与合法性机制。

> **⚠️ 2026-10-01 实现期修订(Impl Note 1 / 4 —— 落地时按 GDD 现裁口径就地订正)**
> ① **`IWorldGeometry` 未立**:ADR-014 §五 的装载真源是 <code>IDataProvider</code>(已在 `Sim.Contracts`,
> 「装载失败 = 启动期硬失败,无错误返回通道」),本 story 的读侧直接由其承载,不另开第二装载接口。
> ② **`SPEED_MAX` 不由本 story「消费声明值」,按 GDD :511 展开式派生**
> `SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX`(GDD 原文:「不写出来,读者看不出 6 的表锁着 6 自己的
> 几何常量」,且 :529 明写「K_TERRAIN_MAX / K_CONTEXT_MAX 抬高的是 LATTICE_SIZE 的下界」)。
> 故 `WorldLatticeParams` 收 `speedModeMax`(归 1)+ `kContextMax`(归 24)+ K_speed 表,**自行派生
> `SpeedMax` 后校验 F-6-1**。Impl Note 4 的「不重定义」按 GDD `:511` 读作「不另起一个与展开式矛盾的
> 裸值入口」,而不是「把展开式拆开藏在调用方」。
> ③ **单位口径(2026-10-01 二轮评审后就地重订 —— 原口径「长度以格计」量纲不成立)**:
> 「格边长以格计」是循环定义(1 世界格 = 1 格 ⇒ `LATTICE_SIZE` 恒 1);且 ADR-015 §三 的
> 「空间位置一律整数格」约束的是**坐标**(`WorldPos` 分量),不是**格边长**这个非空间配置量。
> 现按 GDD F-6-1 表头的物理量纲取值:`LATTICE_SIZE` = 整数**毫米**·`SPEED_MODE_MAX` = m/s ·
> `MAX_DT` = ms · `SAFETY_MARGIN` / `K_*` = 无量纲。
> ⇒ 本式只需**一次量纲恒等式** `m/s × ms = mm`(m·ms/s = 10⁻³ m)⇒ 断言内**无换算常数**。
> 这是刻意的:原实现有一个 `MsPerSecond = 1000` 桥,它若被抄漏(或两侧单位各自漂移),
> 右端就静默差 1000 倍 = **静默失守**;消除桥 = 消除该失效类
> (回归守卫 `test_worldLattice_dimensionIsInexpensiveToGetWrong_guardedByBoundaryFixture`:
> 恰取下界过 / 差 10× 红 / 差 1000× 红)。
> ⚠️ `SpeedModeMax` 取 `int` m/s ⇒ 量子 = 1 m/s;**调用方(1 / 数值轮)须向上取整**(2.5 ⇒ 3)
> 使下界偏保守,绝不可向下取整。若数值轮需亚 m/s 精度,须升 `Fix` 并按 ADR-005 Amendment G
> 的 hi/lo 手工 128 位中间乘执行(禁 `Int128` / `BigInteger`)—— 该升级**未发生前**按 1 m/s 量子口径。

## Out of Scope

- [Story 002]: `EcozoneOf(cell)` 多边形查询(消费本 story 的几何加载)
- [Story 003]: POI 定义/状态
- [Story 004]: chunk 激活与流式
- 关卡工具导出器与 C1–C6 一致性检查本体(归 ADR-022 Tooling epic)
- 视觉层 Unity Terrain 资产(纯表现,不进 sim)
- **`AC-6-09` 的 `slopeLimit` / `stepOffset` 量化一致性**:断言对象是 `CharacterController` 的 float 参数
  与逻辑层整数量子,归**视觉层/引擎侧装载**(Visual-Feel),且其真源 = ADR-022 关卡工具导出;
  不属本 story 的纯逻辑面。

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 合法参数(LATTICE_SIZE ≥ F-6-1 下界,基准表 `{1,2,5}` ⇒ 下界 = 50×100×2 = 10000 mm) | 装载 | 通过 |
| TC-2 | SAFETY_MARGIN=1(等于而非大于) | 装载 | `throw`(硬边界,消息点名) |
| TC-3 | K_speed 表抬到 `{1,3,7}` | 读 K_TERRAIN_MAX | == 7(派生随动,非手填) |
| TC-4 | K_speed 表 = ∅(或 null) | 装载 | `throw` |
| TC-5 | 载荷类型的类型图含 `float`/`double`/`Vector*`/UnityEngine | 反射扫描 | 断言失败(负面夹具) |
| TC-6 | 极端坐标 (2^31−1, ·, ·) 参与量程断言 | 校验 | 构建期 `throw`(W 超安全区) |

**Edge cases**: 负 LATTICE_SIZE;SAFETY_MARGIN 恰好为 1(须拒);int32 分量差平方溢出(承 enemy F-27-1 的 int64 纪律,此处只做断言对象登记)。

> **TC 落地面(2026-10-01 · 双代理评审后修订版)**:全部 6 条 + 4 条 edge cases 均成一等测试,
> 合计 **42 例全绿**(EditMode filter `WorldLatticeTest`;命名空间 `DaYiJingCheng.Tests.WorldEcozones`
> 合计 **73 例全绿**;`WorldTest` 8 例 · `AssemblyBoundaryTest` 24 例 · `ModularBuilding` 命名空间 53 例同批复跑全绿)。
> - TC-1 ⇒ `test_worldLattice_validParams_constructs` + `_boundaryEquality_passes`(取等号仍过,GDD :506)
> - TC-2 ⇒ `_safetyMarginEqualsOne_throws`;TC-4 ⇒ `_kSpeedTableEmpty_throws` + `_kSpeedTableNull_throws`
> - TC-3 ⇒ `_kTerrainMax_followsTableChange` + **`_latticeBoundTracksTerrainTable`**(EC-6-14:抬 `K_speed`
>   或 `K_CONTEXT_MAX` ⇒ F-6-1 下界同步抬升,原尺寸即红 —— 证明「6 的表锁着 6 的几何常量」有执行点)
> - TC-5 的**负面夹具**经反射递归扫描落成**正面白名单**(GDD :1154 明示负存在断言不可判定):
>   `test_logicLayerTypes_noFloatDoubleVectorOrLocalFrame` 枚举 AC-6-01 点名的**五类面**
>   (`SimEvent` 全部 34 个具名载荷 + `WorldPos` 形状 + POI_DEF/导航格/建造槽/生态区多边形的登记类型),
>   断言无 float/double/`Vector*`/UnityEngine;**门本身可证伪** —— 判据函数与白名单共用,
>   负面夹具 `FloatFieldFixture` / `VectorChunkOffsetFixture` + 对照夹具 `UintFieldFixture` 走同一组谓词
>   (`test_typeGraphScan_negativeFixtures_reportRed`),否则「扫描器在扫什么」无人能证。
> - TC-6 ⇒ `_extentMaxInt_throws` + `_extentConstant_isBoundaryValue`(2^29 是**满足** `3×(2W)² < 2^63`
>   的最大 2 的幂:2^30 不满足 —— ±1 差分 + 值级断言,不靠注释里的常数自证)
>
> **⚠️ TC-3 与 AC-6-08 的冲突已显式登记**:原 TC-3 表 `{2,5,3}` 无 `K_speed = 1` 行 ⇒ 按 AC-6-08
> 「基准地貌恰好一行」会被** baseline 规则拒收**。落地等价表改为 `{1,3,7}`(保留「改表 ⇒ max 随动」
> 的判据本意),并**另立一等测试钉死该冲突**
> (`test_worldLattice_storyTc3Table_isRejectedByBaselineRule`)—— 表的原值不改写、不静默替换。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/WorldEcozones/world_lattice_test.cs` — must exist and pass
**Status**: ✅ 2026-10-01 创建并通过(首版 33 例,提交 `97f62e5`)→ **二轮评审修复后 42 例全绿**
(filter `WorldLatticeTest`,log `unity/Logs/we015.log`);命名空间 `DaYiJingCheng.Tests.WorldEcozones`
合计 **73 例全绿**;`WorldTest` 8 例 · `AssemblyBoundaryTest` 24 例 · `DaYiJingCheng.Tests.ModularBuilding`
53 例同批复跑全绿(装载签名与只读暴露变更的回归面)。
驱动:`unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath unity -testFilter <name>`
(承 CLAUDE.md「Unity Debugging (CLI First)」)。

## Completion Notes

- **评审结论(2026-10-01 · 二轮 · 双代理:technical-director 架构一致性评审 = **REJECT** +
  unity-specialist 代码评审 = **APPROVE-WITH-COMMENTS**)**:TD 记 2 BLOCKING + 3 MAJOR,
  unity-specialist 记 4 MAJOR。逐条处置如下(**全部关闭**,无残留 blocking):

  | # | 级别 | 缺陷 | 处置 |
  |---|---|---|---|
  | TD-B1 | BLOCKING | F-6-1 的量纲错误:右端差 1000 倍 ⇒ **静默失守** | 单位口径重订为 mm / m/s / ms,消桥(见 Impl Note ③);`_dimensionIsInexpensiveToGetWrong_guardedByBoundaryFixture` 钉 10×/1000× 必红 |
  | TD-B2 | BLOCKING | AC-6-01 的反射扫描不可证伪(扫的是「坏类型不存在」负面断言) | 扩面到 AC-6-01 点名的五类面(含 34 个具名载荷),并把谓词提为共用函数让负面夹具走**同一组门** |
  | TD-M3 | MAJOR | AC-6-08 原文是**三列** `K_speed`/`K_accel`/`K_decel`,实现只有一列 | 立 `TerrainSpeedRow { KSpeed:int, KAccel:Fix, KDecel:Fix }`,三列逐一 > 0 |
  | TD-M4 | MAJOR | AC-6-06 的主体是**原点 / LATTICE_SIZE 同源**,被错实现成空表规则 | 立 `LogicalLayerOrigin` 常量 + `ValidateOriginConsistency`(两侧读**同一实体**,非两份相等字面量) |
  | US-M1 | MAJOR | `KSpeedTable` 以 `IReadOnlyList` 声明但底层仍是调用方可见数组 ⇒ 派生量与表可脱钩 | `Array.AsReadOnly` 封装 + 行为化断言(实测不返回数组、类型名含 `ReadOnly`) |
  | US-M2 | MAJOR | `MaxWorldHalfExtent = 1<<29` 无值级依据 | 差分夹具 + 值级断言(`12 × W² ≤ long.MaxValue`;2^30 不满足 ⇒ 2^29 确为最大可行 2 的幂) |
  | TD-M5 | MAJOR | AC-6-07 的「载荷面来自 `world_*.cooked`」无装载执行点(无 `world_geometry.cooked` binding) | **登记为 follow-up**,不阻塞:本 story 只做读侧契约(见下) |
  | (j) | MAJOR | 首版 8 项缺陷(F-6-1 裸参 / 1000× 等) | 首轮已修,提交 `97f62e5` |

- **残留(实现期义务,不阻塞本 story —— 均已登记落点)**:
  - **AC-6-07 的正半(几何装载)**:`IDataProvider` 当前**无** `world_geometry.cooked` /
    `world_terrain.cooked` 的装载方法 ⇒ AC-6-07「几何来自烘焙产物」在运行期**无执行点**,
    `WorldGeometry` 只能由测试自建数组构造。落点 = Story 002 首个真实几何消费者落地的同一批
    (或 6 的装载 epic),届时补 `IDataProvider` binding + 正面对拍。
  - **AC-6-06 的「`LATTICE_SIZE` 与运行期同源」后半**:`LogicalLayerOrigin` 已立,
    但 `LatticeSizeMm` 的**烘焙侧**读数入口尚不存在(同上一项同一落点)。
  - **AC-6-07 ② 源头白名单**:GDD :1193 的 Roslyn / IL 分析器判据属**独立 CI 工具(非 UTF)**,
    与 `AC-6-03` / `AC-6-22` 同一落点;① 值级判据已由本 story 落地。
  - **`AC-6-01` 的登记面随动**:扫描集当前 = AC-6-01 点名的五类面 + `SimEvent` 全部具名载荷。
    各系统 GDD 若新增载荷类型,须同批入扫(判据已参数化,加类型即扩面,不改断言)。
  - **`AC-6-09`**(`slopeLimit` / `stepOffset` 量化一致)→ Out of Scope,归 ADR-022 关卡工具 + 1 的执行点。
  - **`ValidateWorldExtent` 的入口侧调用**只到 `WorldGeometry` 装载;ADR-015 §一 的生态区 /
    导航格装载入口接入时须同样调用。

