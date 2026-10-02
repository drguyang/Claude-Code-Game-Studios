# Story 002: EcozoneOf 整数几何查询 —— 开集 / 正则化 / min(id) 仲裁

> **Epic**: 世界与生态区
> **Status**: Complete — AC-6-19②③⑤ / AC-6-20 / AC-6-21 / AC-6-22 全绿(25 例);AC-6-19①④(重叠/零面积)按登记落点推 ADR-014 阶段 2 + ADR-022 C4;EC-6-8 与 AC-6-20 的闭集/开集矛盾登记为 D-6-1(见 Completion Notes)
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-01

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

- [x] 内部格返回对应 ecozone id;外部格返回 `−1`(AC-6-19) → `test_ecozone_internalCell_returnsId` / `test_ecozone_externalCell_returnsNone`
- [x] 边界点(恰在多边形边上/顶点上)经正则化后判定确定且**与遍历方向无关** —— 性质测试:同一边界格重复查询逐位相同(AC-6-20) → `test_ecozone_pointOnEdge_goesToBoundaryArbitration` / `test_ecozone_vertexCell_boundaryNotInterior` / `test_ecozone_pureFunction_doubleRun_identical`
  ⚠️ **AC-6-20 的「边界 = 内部」文案按 F-6-3 §一/§三 的开集口径实现**(边界 → Boundary 仲裁,非 Interior)—— 原文自相矛盾,登记 **D-6-1**(见 Completion Notes)
- [x] 重叠/相邻多边形:同格命中两区 ⇒ 恒返回 min(id);打乱输入顺序结果不变(交换律,AC-6-21) → `test_ecozone_minId_independentOfRegistrationOrder` / `test_ecozone_singleValue_sharedCorner_minId` / `test_ecozone_boundaryArbitration_minId`
- [x] `EcozoneOf` 为纯函数:同 (cell, 烘焙数据版本) 离线重放与运行期结果逐位相同(承三源不变量) → `test_ecozone_pureFunction_doubleRun_identical`
- [x] y 分量对结果零影响:同 (x,z) 任意 y ⇒ 同结果(AC-6-22 侧的投影口径) → `test_ecozone_yComponent_noInfluence` / `test_ecozone_flatAndSteepPolygons_sameResult`
- [x] 递归反射断言:查询路径零 `float`(整数射线法实现,中点/加法链无除法截断歧义) → `test_ecozone_il_noFloatOps_noYReads`(call 图闭包 IL 扫描,`EcozoneIntegerGates`)

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

**Status**: ✅ **VERIFIED 2026-10-01 · Unity 6.3.24f1 EditMode batch(【超算】)**

| Fixture | 用例数 | 结果 |
|---|---|---|
| `EcozoneQueryTest`(本 story 交付) | 25 | ✅ 25/25 |
| `WorldLatticeTest`(Story 001 回归) | 42 | ✅ 42/42 |
| `PoiStateMachineTest`(Story 003 回归) | 13 | ✅ 13/13 |
| `ChunkActivationTest`(Story 004 回归) | 7 | ✅ 7/7 |
| `AssemblyBoundaryTest`(装配门回归) | 24 | ✅ 24/24 |
| **合计** | **111** | **0 failed** |

- 编译错误 `error CS` = **0**(3 批运行各自核实;本批新增文件零 warning)
- 证据落盘:`production/qa/evidence/world-ecozones/story-002-ecozone-query-testresults-2026-10-01.xml`
- 运行口径:`-batchmode -nographics -runTests -testPlatform EditMode -testFilter 'DaYiJingCheng.Tests.Unit.Audio.AssemblyBoundaryTest|DaYiJingCheng.Tests.WorldEcozones'`;log `unity/Logs/we002_final.log`
- ⚠️ **非「桌面绿」**:本次为【超算】batchmode 结果;`[L]` 手感 / 视觉项不适用本 story(纯逻辑)

---

## Dependencies

**Depends on**: Story 001(WorldPos / 几何加载)· ADR-022 工具产出的 `world_ecozones`(夹具可由测试数据替身)
**Unlocks**: 系统 5 Story 002(EcozoneOf 抽池)· 13 的 HomeRegion · 27/13 共享的生态区语义 · Story 004(发现门的区归属)

---

## Completion Notes

**交付物**:

| 文件 | 动作 |
|---|---|
| `unity/Assets/Sim/World/EcozoneRegistry.cs` | **全量重写**(开集 + Boundary 层 + min(id) 排序仲裁 + 量程守卫 + Register 加载期校验) |
| `unity/Assets/Editor.Tools.Gates/EcozoneIntegerGates.cs`(+`.meta`) | **新增**(AC-6-22 的 IL 执行点:call 图闭包零浮点 + 零 y 读取) |
| `unity/Assets/Editor.Tools.Gates/AssemblyGates.cs` | `EffectiveNamespace` 私有 → 公开(被 `EcozoneIntegerGates` 复用) |
| `unity/Assets/Tests/EditMode/WorldEcozones/ecozone_query_test.cs` | **全量重写**(25 例,含 IL 负面夹具) |

---

### 一、评审结论(2026-10-01 · 双代理)

| 代理 | 结论 |
|---|---|
| `technical-director`(架构一致性) | **REJECT** → 2 BLOCKING(TD-B1 / TD-B2)+ 3 MAJOR(TD-M3…M5) |
| `unity-specialist`(代码) | **APPROVE-WITH-COMMENTS** → 4 MAJOR(US-M1…M4)+ 4 MINOR(US-m5…m8) |

**全部 9 条 MAJOR + 2 条 BLOCKING 已关闭**;MINOR 中 1 条关闭、3 条登记为残留。逐条处置:

| # | 级别 | 缺陷 | 处置 |
|---|---|---|---|
| TD-B1 | BLOCKING | **读 y**:原实现有一道「取顶点 y 的 min/max」预滤门,与 F-6-3「y 不参与」直接冲突 —— 同一 (x,z) 在 y=±5 时返回 `NONE`,把山丘上的 POI / 13 的 `HomeRegion` 静默抹掉;且使 AC-6-22 的「该函数不含 y 读取」**恒不可达** | 预滤门**删除**。GDD 四处独立表述(:573 / :636-638 / :1367 + story Forbidden)均定 y 不参与 ⇒ 不是裁取舍问题。`test_ecozone_yComponent_noInfluence`(y ∈ {−5,0,7,1000,−1000})+ `test_ecozone_flatAndSteepPolygons_sameResult` 钉住 |
| TD-B2 | BLOCKING | **缺 Boundary 层**:原实现用裸 PNPOLY ⇒ 边界点被判为「在内部」(差分实测 **32.4% 的边界点被错分**),贴边街区每格归属随机;`AC-6-20④`(单值)不可达 | 正则化三层落地:`Contains` = 非边界 ∧ 奇偶(开集)· `IsBoundary` = 精确整数叉积 = 0 ∧ 落包围盒 · `EcozoneOf` = Interior 命中 → min(id),否则 min(boundary_hits).id,否则 `NONE`(`EcozoneRegistry.cs:87-115` / `:286-310`) |
| TD-M3 | MAJOR | **静默覆盖**:`Register` 对重复 id 直接 `dict[id] = poly`,结果取决于注册顺序 ⇒ 三源不变量破坏(同一 id 两份几何) | 重复 id **抛 ArgumentException**;`test_ecozone_register_rejectsDuplicateId` 钉住 |
| TD-M4 | MAJOR | **id 序未定义**:`Dictionary` 遍历序实现相关 ⇒ 「min(id)」在不同 runtime 可能给出不同结果 | 立 `SortedIds()`(惰性排序 + Register 作废),`EcozoneOf` 用**排序 + 命中即停**实现 min;`GetAllEcozoneIds()` 显式按升序返回。`test_ecozone_minId_independentOfRegistrationOrder` / `test_ecozone_getAllIds_isSortedAscending` |
| TD-M5 | MAJOR | **无 int64 溢出边界**:满量程 i32 下叉积因子达 2^32 ⇒ 中间积可回绕(ADR-012 F7:IL2CPP 有符号溢出是 UB) | `EcozoneOf` 入口量程守卫 `|x|/|z| ≤ MaxWorldHalfExtent(2^29)` ⇒ 因子 ≤ 2^30、中间积 ≤ 2^60;两侧各自先转 `long` 再乘。`test_ecozone_extentGuard_throwsBeyondSafeRange` |
| US-M1 | MAJOR | **原代码用「y 范围预滤 + PNPOLY」两段式**,奇偶核只对非边界点有定义但无边界前置 | 重写为 `Contains` 边界先判再走奇偶(`:92-98`),次序固定进注释与测试 |
| US-M2 | MAJOR | 射线法用「除以斜率」常见重构 ⇒ 一旦变浮点即破 ADR-006 边界 | 结构性排除:`IsInterior` 用**交叉相乘免除法**(`:144-148`),`IsOnBoundarySegment` 用**叉积 = 0 判共线**(`:163-169`)—— 零除法 |
| US-M3 | MAJOR | 退化多边形(顶点 < 3 / 重合相邻顶点 / 自交)装载期**无校验**,射线法对其无定义 ⇒ 静默错判 | `Register` 做线性 + O(V²) 校验,三类全抛;5 个 rejection 测试 + 1 个 accepts-sharing-vertex |
| US-M4 | MAJOR | AC-6-22 的 IL 判据**无执行点**(「递归反射断言」原是空承诺) | 新增 `EcozoneIntegerGates`(见下)+ 3 个测试 |
| US-m5 | MINOR | IL 判据第一版扫整个 `DaYiJingCheng.Sim.World` 命名空间 ⇒ 把 **Story 004 的 ChunkTopology / ChunkActivator**(合法读 y)判成**假红** | 重写为**真实 call 图闭包**(BFS 沿 call / callvirt / newobj / jmp,只收本装配内方法);根 = `EcozoneOf` + `Register`。⚠️ 记档:**判据过宽与判据过窄同属静默失败** |
| US-m6 | MINOR | IL 判据只看四个浮点 opcode 不充分(常量折叠后可以 `ldc.i4 + conv.r4` 或元数据 token 出现) | 加**类型面**判据:`float`/`double` 的参数 / 返回 / 局部(Cecil `Body.Variables` 等) |
| US-m7 | MINOR | IL 判据若扫不到东西会**假绿** | 三重防假绿:产物缺失 / 根类型或根方法找不到 / 闭包为空 ⇒ **一律记红**;`test_ecozone_ilClosure_containsCoreKernels` 守卫**闭包形状**(核心核从闭包消失 ⇒ 查询路径被改写 ⇒ 重红) |
| US-m8 | MINOR | 负面夹具若与生产类型同名会产生 CS0708/CS1061,且易被误读成生产侧命中 | 负面夹具命名 `FixtureEcozoneRegistry`(置于测试程序集),断言的是**夹具根自身**在闭包内 |

---

### 二、EC-6-9(射线穿顶点)的落地

GDD 只给了方向「半开区间」,未给**轴约定**。本实现取的约定(`EcozoneRegistry.cs:118-151`):

- 射线 = **+X**,横轴 = **Z**
- 半开:仅一端 z **严格** `>` `p.Z` 才进入候选
- 交点须**严格**在 `p.X` 之前(`ahead`,交叉相乘判)
- ⇒ 共享顶点的两条边**天然不会同时计入**,无需 EC-6-9 之外的邻边配对查表

验证:**独立 Python 整数绕数参考实现**(Sunday winding number,精确 `Fraction`)11,175 个非边界点差分 = **0 不匹配**;另跑 3,839 内部 / 3,236 边界 / 50,561 外部三类点分桶核对无错分。专用夹具 `test_ecozone_rayThroughVertex_noDoubleCount`(U 形缺口多边形 id=7)。

### 三、残留(实现期义务,不阻塞本 story)

#### D-6-1(**待用户裁决** —— GDD 自相矛盾,实现已按一側落地)

GDD 有**两处互斥**的边界语义,且都自称 BLOCKING:

| 出处 | 原文口径 |
|---|---|
| `F-6-3` §首注(`:563-566`)· §一(`:596-614`)· §三(`:618-622`)+ `AC-6-21` 叙述(`:612`) | **开集** —— 「点恰在边上/顶点上:**不算任何区的 `Interior`**」,归 `min(boundary_hits).id` |
| `EC-6-8`(`:742-744`)· `AC-6-20` 标题与夹具(`:1267-1271`) | **闭集** —— 「判定为**内部**(闭集语义),且归属唯一(F-6-3)」 |

**关键**:F-6-3 §首注**逐字记录了** 2026-09-16 首轮评审把原闭集版判为**自相矛盾**并重写为开集的理由(「闭集语义对共享边的两个多边形同时成立 ⇒ 返回两个 `ecozone_id`,原 `AC-6-20④` 单值不可达」)。而 `EC-6-8` / C 组 `AC-6-20` 是**那一轮之前的原文,未被同批改写** —— 即 **B 组已被修订、C 组与 EC-6-8 是陈旧残留**。

**本实现取开集侧**(F-6-3 §一/§三 + 修订注),理由:(a) 它是较新的显式修订,且自带反证;(b) 闭集侧无 min(id) 裁决序,在共享边情形**必然返回两值**、连自己的「归属唯一」都达不到。**但这是「择一」不是「和解」** —— 登记为 **D-6-1** 待用户裁,裁后须回改 GDD 的 `EC-6-8` + C 组 `AC-6-20` 文案。

**注意**:C 组 `AC-6-20` 的 4 个夹具中,③(射线穿顶点不双重计数)与 ④(任一格不得返回两个 id)在**开集口径下同样成立且已被本 story 覆盖**;① ② 的「均判内部」在开集下改为「判 Boundary → 走仲裁」。

#### 其他残留

- **`AC-6-19①④`**(内部重叠 / 零面积多边形)按 GDD :1261 明写**判据 = ADR-014 管线校验器**、**不是运行期实测**(EC-6-7)⇒ 本 story 不实现,落点 = **ADR-014 阶段 2 + ADR-022 C4**。运行期只做「信任但不盲信」的廉价入参卫生(Impl Note 5)。
  ⚠️ 测试夹具用的是**合法贴边/共享顶点**(AC-6-20 明确允许),**不是**内部重叠 —— 后者加载期即应失败。
- **共线边自交是已知漏检面**:`ValidateNoSelfIntersection` 用「真交叉」(四叉积全非零 ∧ 双向严格异号)⇒ 共线重叠 / 端点接触**不进**失败集。如实登记为**充分不必要**的简化,完整校验归 ADR-022 C4(`EcozoneRegistry.cs:226-230`)。
- **`EcozonePolygon.Vertices` 是 `public readonly WorldPos[]`** —— `readonly` 只锁引用,**数组内容运行期可变**(US-m8 之后未处理的 MINOR)。影响面:注册后调用方改数组 ⇒ 查询结果与烘焙数据脱钩。因当前**生产侧零消费者**(仅 `WeatherRoll.cs:5` 一句注释引用),暂记不修;首个真实消费者落地的同一批改为 `IReadOnlyList` / 私有拷贝。
- **`OQ-6-9`(垂直分层)未动**:P0 单层;若 P1b 判需分层,须**先**扩 F-6-3 + `AC-6-19` 再回改本文件,**不得**就地加 y 条件分支。
- **GDD 有两条 `AC-6-19`**(B 组 `:1261` 5 夹具 / C 组 `:1269` 3 夹具,编号重复、夹具集不同)⇒ 建议 GDD 侧合并为 `AC-6-19`(B 组全集)+ `AC-6-19b`(C 组),与 D-6-1 同批处理。
- **生产侧装载 binding 仍缺**:`IDataProvider` 当前无 `world_ecozones.cooked` 读数入口(与 Story 001 残留的 `AC-6-07` 正半**同一落点**)⇒ 6 的装载 epic 落几何 binding 时同批补。
- **`IEventSink` 侧零改动**:本 story 是纯查询(派生态),无新 `Kind`、不动 `entities.yaml`。
