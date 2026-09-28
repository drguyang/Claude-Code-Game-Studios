# Story 002: EcozoneOf 整数几何查询 —— 开集 / 正则化 / min(id) 仲裁

> **Epic**: 世界与生态区
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(§Formulas F-6-3 `EcozoneOf(cell)` —— 开集判定 + 正则化 + 边界 min(id) 仲裁 · 射线法全整数 · NONE=−1 哨兵 · y 不参与(x/z 平面)· C 组 AC-6-19…22 · §R-6 5 消费侧的全局默认档)
**Requirement**: TR-timeweather-002 / TR-timeweather-011(5 侧消费:生态区气候属性由 6 定义、未命中取全局默认、不得假设全图可达)· TR-worldeco-001(定义 = 派生态,加载期确定性重建)(TR 登记 slug:`timeweather` / `worldeco`)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主): 单一整数格 + 烘焙逻辑层;ADR-022: 关卡工具(多边形作者与 C4 合法性检查的工具侧)
**ADR Decision Summary**: 生态区多边形是版本化烘焙整数数据(派生态),运行期只加载;多边形合法性(C4)在关卡工具侧硬失败,6 运行期**不修补数据**;`EcozoneOf` 是全案(5 / 13 / 27 / 52)唯一的「格 → 生态区」函数,单一定义点。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 射线法(ray-casting)全整数求值,int64 中间量防溢出;不触及任何引擎 API。

**Control Manifest Rules (this layer)**:
- Required: 多边形按**开集**判定 + 边界情形经**正则化**;同时落入多个区域 ⇒ **min(id) 仲裁**(稳定决胜键);未命中 ⇒ 返回 `ECOZONE_NONE = −1`(消费方各自决定默认档,6 不返回「最近区」)
- Forbidden: 浮点几何(`Mathf` / `Vector2` / 射线 float 版);运行期对非法多边形的静默修补;`y` 分量参与判定(世界为 x/z 平面投影,承 F-6-3)
- Guardrail: 全部中间量 int64;查询为纯函数(同 cell 同数据 ⇒ 同结果),供重放;`EcozoneOf` 不得假设全图可达(未驻留 chunk 的多边形照常可查,chunk 驻留只影响流式不影响正确性)

---

## Acceptance Criteria

*From GDD `design/gdd/world-and-ecozones.md`, scoped to this story:*

- [ ] 内部格返回对应 ecozone id;外部格返回 `−1`(AC-6-19)
- [ ] 边界点(恰在多边形边上/顶点上)经正则化后判定确定且**与遍历方向无关** —— 性质测试:同一边界格重复查询逐位相同(AC-6-20)
- [ ] 重叠/相邻多边形:同格命中两区 ⇒ 恒返回 min(id);打乱输入顺序结果不变(交换律,AC-6-21)
- [ ] `EcozoneOf` 为纯函数:同 (cell, 烘焙数据版本) 离线重放与运行期结果逐位相同(承三源不变量)
- [ ] y 分量对结果零影响:同 (x,z) 任意 y ⇒ 同结果(AC-6-22 侧的投影口径)
- [ ] 递归反射断言:查询路径零 `float`(整数射线法实现,中点/加法链无除法截断歧义)

---

## Implementation Notes

1. 输入 = `world_ecozones.cooked` 的整数多边形集(顶点为整数格坐标);空间索引(格→候选区)住边界层实现细节,不影响纯函数语义。
2. 射线法判定的奇偶计数用整数加法链;「点恰在边上」的判定先于奇偶计数(正则化次序固定,写进代码注释与测试)。
3. min(id) 仲裁是**规则不是巧合**:查询收集全部命中区再取 min,禁「先命中的赢」的短路实现。
4. 消费方接线(5 的气候档、13 的 `HomeRegion=EcozoneOf(spawn_anchor(p))`)在各自 epic;本 story 交付查询本体与哨兵语义。
5. 多边形合法性(C4:自交/退化)由关卡工具 CI 硬失败保证;6 运行期对已加载数据**信任但不盲信** —— 可加廉价的加载期校验,失败=硬失败。

---

## Out of Scope

- [Story 001]: 格与可走性(本 story 消费其加载)
- [Story 003]: POI(与生态区多边形正交)
- [Story 004]: chunk 激活(`EcozoneOf` 与驻留无关是其前提)
- 关卡工具的多边形绘制与 C4 检查本体(ADR-022 Tooling)
- 5 的气候属性表(值归数值轮与 5 的 epic)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 单矩形区 (0,0)-(9,9) | 查 (4,4) / (10,4) | 返回区 id / −1 |
| TC-2 | 点恰在边中点与顶点 | 各查 100 次 | 结果恒定且一致(正则化确定性) |
| TC-3 | 两区重叠覆盖格 c(id=3, id=1) | 查 c(并打乱数据顺序) | 恒返回 id=1(min 仲裁) |
| TC-4 | 同 (x,z) 改 y ∈ {−5,0,7} | 查询 | 三者同结果 |
| TC-5 | 随机 10^5 格 × 双跑 | 比对 | 逐位相同(纯函数) |
| TC-6 | 自交多边形负面夹具 | 加载 | 加载期硬失败(不静默) |

**Edge cases**: 空多边形集(全图 −1,合法);单顶点退化区(加载拒收);int64 溢出压力(极端坐标 × 加法链,承 Story 001 的量程断言)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/WorldEcozones/ecozone_query_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

**Depends on**: Story 001(WorldPos / 几何加载)· ADR-022 工具产出的 `world_ecozones`(夹具可由测试数据替身)
**Unlocks**: 系统 5 Story 002(EcozoneOf 抽池)· 13 的 HomeRegion · 27/13 共享的生态区语义 · Story 004(发现门的区归属)

---

## Completion Notes
