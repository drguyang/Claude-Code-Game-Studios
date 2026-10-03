# TD 架构一致性评审 — camera-viewpoint Story 004(出臂与收缩)

- **日期**: 2026-10-03
- **评审人**: technical-director
- **Gate**: TD-ARCHITECTURE
- **对象**: `unity/Assets/Gameplay.Presentation/Camera/CameraArmSolver.cs`
- **测试**: `unity/Assets/Tests/EditMode/CameraViewpoint/camera_arm_solver_test.cs`(16 例)
- **故事**: `production/epics/camera-viewpoint/story-004-arm-geometry-and-retraction.md`(Status: Complete)
- **结论**: **REJECT** —— 抽象层落点大体正确,但交付件有 3 处 BLOCKING 正确性缺陷 + 1 处判据空转,且被测试约定"遮盖"(借绿形态)。故事不应维持 Complete。

## 0. 核心六问裁决表

| # | 问题 | 裁决 |
|---|------|------|
| 1 | `IArmCollisionQuery` 落地正误 + 生产路径真接 `Physics.SphereCast` | **落点对 · 生产未接线**(CONCERNS) |
| 2 | `CameraArmParams` 是否成第二份档位定义 | **不复制档位枚举(好)**;但**组 5 七项被拆散到三处**(CONCERNS) |
| 3 | F-2-3 订正①(`r̂` 替 `f̂ × CAM_RADIUS`)逐字落地 | **①逐字落地 ✅**;但 **订正②(`ViewDir` 手性)判据缺 + 符号疑似反转**(BLOCKING) |
| 4 | F-2-4 五行顺序 + 每帧重解 `d_block` | **顺序对、无跨帧 `d_target` ✅**;但 **`false → CAM_MIN_DIST` 反了 GDD 主用例**(BLOCKING) |
| 5 | AC-2-25②「无第二处档位分支」是否真在扫 | **在扫代码(非注释)**;但**子串判据、只扫单文件** ⇒ 弱判据(CONCERNS) |
| 6 | 与 001 / 005 的接缝 | 001 ✅(无回归);005 **组 5 单一源未结构落地 + solver 悬空**(CONCERNS) |

---

## 1. `IArmCollisionQuery` 抽象落地(问 1)

**裁决:抽象方向正确 · 生产侧未接线。**

- **该抽象是必要的且是正确落点**。`Physics.SphereCast` 是 `UnityEngine` 静态 API,**不可拦截**,若相机直调则 AC-2-27①「每帧查询数 == 1」无法被计数包装断言。`CountingArmQuery`(在 `CameraModeMachine.cs:154`)正是为此存在 —— 这条与 ADR-005 的"抽象点"纪律、ADR-011 的"可注入"取向同源,**符合项目一贯做法**。
- **但生产路径根本没有 `Physics.SphereCast` 实现**。全仓 grep:无任何 `PhysicsArmQuery : IArmCollisionQuery` / 无 `Physics.SphereCast(` 调用(仅注释提及)。`CameraArmSolver` / `CountingArmQuery` **在生产侧零实例化**(只有测试 `new`)。
  - 后果:本缝当前只是**可测缝**,不是**生产缝**。AC-2-27① 的计数**对着真查询不可执行**,这是 story 005 的判据前置 → 必须在 005 落地前补 `PhysicsArmQuery`(显式 `QueryTriggerInteraction.Ignore`)。
  - 故事 Completion Notes 已诚实登记该 NOT-RUN(不借绿)✅,但对"完整契约"的完成度判断偏乐观:接线是**本故事** Control Manifest 的 Required 条目("投射原点 = 肩位、方向 = `ê_view`、与几何同源")的一半,另一半(调用点)交付了,接线未交付。

**判定:CONCERNS(方向对,接线欠账须在 005 前清)。**

## 2. `CameraArmParams` 与档位闭集(问 2)

**裁决:不构成"第二份档位定义";但它暴露了组 5 单一源未落地。**

- **好消息(问 2 的直接答案)**:`CameraArmParams` **不含任何 `CameraMode` 字段 / 无 per-mode 表 / 无 `switch(mode)`**,是**平铺的单套参数包**。它**不复制档位枚举** ⇒ 与 005 的 `CameraModeMachine` **不构成双重档位定义**。这一半是对的。
- **坏消息**:AC-2-25② 明写"**七项**的档位取值全部来自组 5 的表"。实际七项被**拆到三个类**,且**没有任何"组 5 表"对象存在**:
  - `CameraArmParams`(004):`ARM_LEN` / `SHOULDER_LATERAL` / `FOV_v`
  - `CameraModeMachine`(005):`PITCH_CASEBOOK`(property 硬编码 `45f`,`CameraModeMachine.cs:146`)
  - `AnchorFollowConfig`(003):`ANCHOR_RESPONSE`(`AnchorFollower.cs:27`)
  - "`Look` 是否驱动" / "锚是否跟随":**不是数据**,而是 `IsFrozen => Mode == Casebook` 的派生行为(005)
  - ⇒ AC-2-25② 的字面承诺("七项全部来自组 5 的表")**在现结构下不可满足**;每个故事把参数撒进自己的类,正是"悄悄改动换档只改参数的结构承诺"(R-2-1)的**缓慢漂移通道**。
- **建议**:在 005 结转前,把组 5 定义为**单一 `CameraModeParams` 表对象**(mode → 七项),004/003 的类改为**读该表**,而非各自持默认值。

**判定:CONCERNS(不双写枚举 ✔,但单一参数源未成立)。**

## 3. F-2-3 订正① / 订正② 与 F-2-4 五行(问 3、4)

### 3.1 订正①(`r̂` 替 `f̂ × CAM_RADIUS`)—— **逐字落地 ✅**

`Shoulder()`(`CameraArmSolver.cs:105-111`):
```
anchor + Vector3.up * _p.ShoulderHeight + basis.Right * _p.ShoulderLateral
```
`r̂ = basis.Right`,**不再含 `CAM_RADIUS`** ⇒ 解耦成立。反空转夹具 `test_ac225a_shoulderUnaffectedByCamRadius`(radius 0.3 vs 5.0,肩位不变)存在且有效(仅断言 x/z;y 不依赖 radius,可接受)。**这一格是干净的。**

### 3.2 🔴 BLOCKING — F-2-4 第①行 `false → CAM_MIN_DIST` **反转了 GDD 的主用例**

GDD F-2-4 首行逐字:"`d_raw := SphereCast(...).distance` **// 未命中 ⇒ `d_raw := ARM_LEN`**"。
实现(`CameraArmSolver.cs:148`):
```csharp
float dRaw = hit ? dist : _p.CamMinDist;   // ← 未命中 ⇒ CAM_MIN_DIST,与 GDD 相反
```
- `Physics.SphereCast` 在**两种**情形返回 `false`:(a)起点球重叠(**罕见**);(b)沿 `ê_view` 在 `ARM_LEN` 内**无碰撞(未命中)** —— **开放世界中最常见的情形**。
- 故事把 (b) 也判为 `CAM_MIN_DIST`(0.5 m)。⇒ **玩家背后 4 m 内无墙时(常态),臂长恒塌到 0.5 m ≈ 实质第一人称**,**直接违反 ADR-020 §三 越肩裁定**与 F-2-3 的本意。
- 实现注释的自我辩护"真未命中 ⇒ 臂长最短,但那要求**球半径内完全无几何**,极罕见"**是事实错误**:`SphereCast` 返回 false 的条件是**射线(no hit along the ray)**内无命中,而非"球体内无几何"。
- **更严重:测试约定把这条主导路径整个掩盖了**,构成借绿形态 —— 所有"无遮挡/几何远"夹具都用 `Hit = true; Distance = 10f` 表达(见 `camera_arm_solver_test.cs:56,77,121,213`),**从无任何测试走 `Hit=false` 的"未命中→应回 ARM_LEN"路径**。`test_ec21_originOverlap_fallsBackToMinDist` 用 `Hit=false` 但它**无法与真未命中区分**,因此该例既证明不了 EC-2-1,也顺带"绿"了 bug 路径。

**这是本故事的头号缺陷。** 保守语义的方向选错了:安全的一侧应是 `false → ARM_LEN`(远),把"起点重叠穿模"这一罕见退化交给**另裁**(故事自己指出 `CheckSphere` 需第二次查询、与 AC-2-27① 冲突 —— 但那才是需要用户裁的取舍,而不是把常态用例牺牲掉)。

### 3.3 🔴 BLOCKING — `ViewDir` 手性符号疑似反转 + **零测试覆盖**

GDD 钉死:`ê_view := R(yaw,pitch) × ê_back`,R = **先绕世界 +Y 转 yaw,再绕该局部右轴转 pitch**(`camera-and-viewpoint.md:438`)。以 `yaw=0` 取 Rodrigues 逐字求:
- `f̂=(0,0,1)`, `r̂=(1,0,0)`, `ê_back=-f̂=(0,0,-1)`
- `r̂ × ê_back = (0,1,0)`;`ê_view = ê_back cosθ + (r̂×ê_back) sinθ = (0, **+sinθ**, −cosθ)`
- ⇒ **GDD 构造给出 `ê_view.y = +sin(pitch)`**(正 pitch=俯 ⇒ 相机在肩**上方**)。与 003 的 `CameraRig.GetCameraPosition`(`:216-220`,`offset.y = +sinPitch·d`)一致。

实现(`CameraArmSolver.cs:124`):
```csharp
Vector3 dir = back * Mathf.Cos(pitchRad) - Vector3.up * Mathf.Sin(pitchRad);  // y = −sinθ
```
⇒ **符号相反**(相机移到肩**下方**)。同一函数内 `Vector3 right = basis.Right;`(`:122`)**声明后未使用** —— 恰说明作者**没有真的"绕局部右轴"旋转**,而是写了一个 ad-hoc 公式。

**同时:整个 R(yaw,pitch) 半边零覆盖** —— grep 全测试目录 `ViewDir` **0 命中**;`Step` 在所有用例中只被喂 `Vector3.forward`(`camera_arm_solver_test.cs:51,73,94,…`),**从未用 `ViewDir` 的真实输出**。故事 Implementation Notes 明确要求的夹具("`yaw/pitch` 四角组合断言 `shoulder + ê_view×d` 视线确实穿过碰撞体"),**不存在**。⇒ **订正②("斜视角贴墙却不收缩"的守门)未落地任何判据**,且现有符号很可能就是 GDD 点名违例的表现。

### 3.4 F-2-4 五行顺序 + 每帧重解 —— **✅(顺序与无状态面成立)**

`Step()`(`:134-161`):①`d_raw` → ②`clamp(d_raw−R, MIN, ARM)` → ③④ 分支。顺序照抄 ✅。无 `wasBlocked` / `blockedThisFrame` / `d_target` 跨帧标志 ✅。`CurrentDistance` 作为回弹积分变量合法(AC-2-16 订正口径)✅。AST 面(注释剥离后的子串扫描,`:247-251`)对本文件有效(诚实,非注释误报)。

**判定:3.1 ✅ / 3.2 🔴 / 3.3 🔴 / 3.4 ✅。**

## 4. AC-2-25② 扫描 / AC-2-15① 判据 / AC-2-25④ / EC-2-1(问 5)

- **AC-2-25②(`test_ac225b_noSecondModeBranch_inArmPath`,`:296`)**:**在扫代码**(剥离 `//` 后判 `contains`),**不是只扫注释** —— 比空转好。但:
  - 判据是**子串 `contains`**,不是故事 Implementation Notes 自称的 **AST**;仅覆盖**两个确切拼写**(`switch (` & `Mode` 同现 / `if (_mode` / `if (mode ==`)。
  - **只扫 `CameraArmSolver.cs` 单文件**,而 AC 原文要求"**程序集内**无第二处档位分支"。`if (m == CameraMode.Treatment) armLen = …` 之类旁路**不被捕获**(不会命中 `if (mode ==`/`if (_mode`)。
  - ⇒ 弱判据。**CONCERNS。**(必须与 3.2 协同:若 3.2 改成 `if (mode …)` 形态补回退,该扫描立刻失去保护力。)

- **AC-2-15① `test_ac215a_maskEqualsWhitelist_notSuperset`(`:142`)—— 🔴 BLOCKING(判据空转 + 生产默认即违例形态)**:
  - 测试用 `P()` helper,**helper 内部把 `CamCollideMask = 0b1010`**(`:26`),再与**同级字面量** `ExpectedWhitelist = 0b1010`(`:147`)比较 ⇒ **拿夹具字面量比夹具字面量**,**未测任何生产值**。
  - 故事 Implementation Notes 明令"测试从**同一登记处**算期望掩码,**不写第二个常量**(同源纪律)";实现**恰好写了第二个常量**。
  - 生产默认 `CameraArmParams.CamCollideMask = 0`(`:47`)—— **空掩码 ⇒ 永不收缩 ⇒ 穿墙**,**正是本 AC 亲自写下的负向夹具形态**。测试因 helper 覆写而**永远看不到它**。
  - ⇒ AC-2-15① 的判据**不存在**;且交付件默认值**落在**该 AC 要防的失败形态上。

- **AC-2-25④ `test_ac225d_armLenPositive`(`:312`)**:断言 `P().ArmLen > 0`,而 `P()` 硬编码 `arm=4f` ⇒ **测的是夹具字面量**,不是生产装载。`CameraArmParams` **没有** `ARM_LEN > 0` 的装载期守卫(对照:`ValidateNearClipChain` 有)。⇒ AC-2-25④ 的"装载期断言"**未实现**,判据空转。**CONCERNS / 近 BLOCKING。**

- **EC-2-1 保守语义**:3.2 已述 —— 在现有 `false→MIN` 下,`test_ec21` 与"真未命中"不可区分,无法证明 EC-2-1 已守。**并入 3.2。**

## 5. 接缝(001 / 005 / 003)

- **001(接缝 ✅ 无回归)**:`"ink_edge"` 硬编码**已删**,全仓仅存于 `CameraRig.cs:106` 的**解释性注释**(记录 B3 修复),无残留语义键;注入缝 `SetEffectSemanticsFrom8` 在位。**一致,无回归。**
- **005(接缝 ⚠️)**:
  - **组 5 单一源未结构落地**(见 §2)—— 七项分散三处,005 的 `PITCH_CASEBOOK` 是硬编码 property,004 未消费它。
  - **solver 悬空**:`CameraRig` 仍持 `[SerializeField] float _distance = 5f` + `GetCameraPosition`(`:28,210`),**完全不经 `CameraArmSolver`**。⇒ 存在**暂时的第二份臂长源**(`_distance`,且非组 5 数据)与**第二条机位路径**。在 005 接线前,这是活的漂移面。
  - AC-2-27③(单一调用点)无 `Tick` 消费方 —— 归 005,可接受为欠账。
- **003(接缝 ⚠️)**:`dt` 的 `MAX_DT` 同源钳位**未在 `Step` 入口体现** —— `Step(…, dt)` 直接用外部 `dt`,无 `MaxDtMs` 钳位。故事 Out of Scope 说"回弹支用的 `dt` 钳位值来自 003 的同源 `MAX_DT` 路径";实现依赖**调用方**已钳。可接受(职责外),但在 005 接线时必须确认调用方确实走 003 的钳位路径,否则回弹步长可被掉帧放大。
- **ADR-014 一致性**:`CameraArmParams` 内嵌默认数值(ArmLen=4f 等)。作为"形状载体 + 注入缝"可接受,但**无任何烘焙装载 API**,且 ADR-014 明令"臂参数不得硬编码" —— 默认值的去留须在接线轮明确(建议:默认仅测试用,生产 `null` 即拒绝启动)。

## 6. 结论与放行条件

**Gate 裁决:`TD-ARCHITECTURE` = REJECT。**

**做得对的**(不重开):订正①(`r̂`)逐字落地且反空转夹具有效;F-2-4 五行顺序 + 无跨帧 `d_target` 成立;`IArmCollisionQuery` 抽象方向正确且必要;001 接缝无回归;注释剥离式扫描诚实(不误报注释);NOT-RUN 登记不借绿。

**必须修(REJECT 理由)**:

1. 🔴 **B1 — F-2-4 第①行语义反转**:`false → ARM_LEN`(回声 GDD 主用例),或在**用户另裁**下引入 `CheckSphere`/等价手段区分"重叠 vs 未命中";**不得**以"保守"为名牺牲常态用例。**配套:补一条真走 `Hit=false` 且期望 `ARM_LEN` 的夹具**,否则全绿是遮盖。
2. 🔴 **B2 — `ViewDir` 手性**:按 GDD Rodrigues(绕局部右轴)重写;补故事已要求但缺失的"`yaw/pitch` 四角 + GT 几何对拍"夹具;删未用局部量 `right` 或改为真用它。
3. 🔴 **B3 — AC-2-15① 判据空转 + 生产默认违例**:期望掩码须从**同一登记处**计算(不写字面量);**生产默认 `CamCollideMask` 不得为 0**(须为登记白名单或"未装载即拒绝启动")。
4. 🟡 **B4 — AC-2-25④ 装载期守卫缺失**:`ARM_LEN > 0` 须有装载期断言(与 `ValidateNearClipChain` 同族);测试不得以夹具字面量自证。

**放行(可延后但须登记)**:
- C1 `PhysicsArmQuery` 生产接线 + `CountingArmQuery` 实例化(005 前必须)。
- C2 组 5 单一参数表对象(005 结转前;否则 R-2-1 结构承诺持续漂移)。
- C4 `CameraRig._distance` 第二臂长源收敛到 `CameraArmParams`(005 接线轮)。
- C5 `ValidateNearClipChain` 接入生产装载;`CameraArmParams` 默认值去留。

**结论**:故事 **Status: Complete 应改回 In Progress**;4/4 AC 中 **AC-2-14 / AC-2-15① / AC-2-16 / AC-2-25②④** 的判据**实际未成立**(空转或遮盖),AC-2-14①②③ 逻辑成立但因 B1 的 `false` 路径未测,其"无遮挡"分支**未验**。
