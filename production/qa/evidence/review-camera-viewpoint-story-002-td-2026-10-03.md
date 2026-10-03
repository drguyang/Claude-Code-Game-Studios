# TD 独立评审 — camera-viewpoint Story 002(yaw-basis-hard-delivery)

- **评审对象**: `production/epics/camera-viewpoint/story-002-yaw-basis-hard-delivery.md`(Status: Complete)
- **实现**: `unity/Assets/Gameplay.Presentation/Camera/ICameraRig.cs` · `CameraRig.cs` · 消费方 `Player/InputContractEvaluator.cs` · `CameraArmSolver.cs`
- **测试**: `unity/Assets/Tests/EditMode/Camera/yaw_basis_test.cs`(9 测)+ 1 侧 `Tests/EditMode/PlayerController/input_contract_test.cs`(接缝面)
- **相关 ADR**: `docs/architecture/adr-020-player-controller-and-camera.md` Amendment B(`ICameraRig.YawBasis` 水平化正交基;单向:1 读 2 的基)
- **评审日期**: 2026-10-03
- **评审者**: technical-director(五步循环第 2 步 · 独立评审)
- **声明**: 本报告评的是**当下代码**(2026-10-03 工作树,HEAD `782de44`),不是评审时点的声称。

**总裁决: [TD-REVIEW-STORY-002]: REJECT(不应维持 Complete)** —— 见文末「转 Complete 前置」。

---

## AC 逐条判决表

| AC | 要点 | 判决 | 依据 |
|---|---|---|---|
| AC-2-07①(BLOCKING) | 水平化与 pitch 无关(精确 0) | **CONCERNS** | 构造式 y 写死 `0f` ✓;但测试容差 `1e-6f` 挖空「精确 0」抓错力(见 F1);yaw 夹具累积漂移(见 F6) |
| AC-2-07②(BLOCKING) | 手性防镜像 | **PASS(带缺口)** | 两锚点断言正确(`yaw=0 ⇒ r̂=(1,0,0)`;`yaw=π/2 ⇒ f̂=(1,0,0) ∧ r̂=(0,0,-1)`);QA 规格的**全周逐分量 `cross(worldUp,f̂)` 等式**未落 2 侧(1 侧仅有单点等式) |
| AC-2-08(BLOCKING) | 正交归一 + `YAW_BASIS_EPS` 唯一字面量 | **CONCERNS** | 断言本体 ✓(EPS 单一定义实测 1 处);采样 360 < 规格 4096、回绕点未显式、累积 yaw 非均匀格;**「1 侧回指符号」判据未落**——1 侧 `AC-1-31` 用自有字面量 `0.001f`(见 F5) |
| AC-2-09①(BLOCKING) | 装载期断言(注入表值 + 违例组) | **FAIL → 须记 INCONCLUSIVE** | `PITCH_MIN/MAX` 是硬编码 `const`,无装载校验器、无违例组(90°/91°)、无缺键拒启 —— 数值留白口径下本就不得记绿,但 Completion Notes 记成已交付(见 F3) |
| AC-2-09②(BLOCKING) | `proj_h` 正下界遍历 | **FAIL(空转判据)** | 测试只断言 `cos(PITCH_MAX) > 0`(常量三角检查),未遍历 `pitch ∈ [0, PITCH_MAX]`、未算 `‖proj_h(f̂)‖`(见 F4) |
| AC-2-09③(BLOCKING) | 防御性兜底(错配 ≥90° 不抛) | **WEAK/PARTIAL** | `const` 不可注入违例值,测试自认「不依赖 PITCH_MAX 实际值」,实为在正常值上复测 07/08;构造式无奇异性由代码结构性保证,但负向夹具(实现加 clamp ⇒ 红)缺失 |
| AC-2-10①(BLOCKING) | 帧内次序探针契约 | **FAIL** | 测试为「同一线程先更新后读取」退化版(自认简化);**相位 A/B 二选一的决定性代码注释全仓缺失**;探针消费方不存在(见 F2) |
| AC-2-10②(BLOCKING) | 相机对 1 零 public 写入面 | **PARTIAL** | 接口半:反射扫 `ICameraRig` 仅 getter ✓;消费方半:1 引用集不含 `CameraRig` 写 API —— **人工核验成立但无守卫测试**(未来 1 侧调 `UpdateYaw` 仍绿);负向夹具(`SetYaw` ⇒ 红)缺失 |

---

## 架构一致性专节(🔴)

### 1. YawBasis 契约形状(构造式 + 派生一致性)

- **构造式逐字落地 ✓**:`CameraRig.cs:62-66` —— `fwd = (sin yaw, 0f, cos yaw)` / `right = (cos yaw, 0f, -sin yaw)`,
  y 字面 `0f`,**无事后归一路径** ✓。与 ADR-020 Amendment B / F-2-2 构造式逐字一致。
- **派生一致性(`r̂` ← `f̂`)**:**2 侧只测了两个锚点**(`yaw_basis_test.cs:55-76`),
  QA 规格的**全周逐分量 `normalize(cross(worldUp, f̂))` 等式未落 2 侧测试**;
  1 侧 `input_contract_test.cs:180` 有单点派生等式(`yaw=0.3`),且其 fixture 用 `Cross(up, fwd).normalized`
  由 f̂ 派生 r̂ —— 接缝上「派生可重构」有单点覆盖,但 **2 侧全周派生校验缺位**(story-001 `AC-1-31` 的上游证成面)。
  判:**形状对、判据面窄**(MEDIUM,不改判 07② 本体 —— AC 原文承认两锚点与全周等式「⇔ 等价」,
  但镜像的第三形态 `cross(f̂, up)` 只被 `yaw=0` 锚点捕获,全周等式才有完整抓错力)。
- **接口形状 ✓**:`YawBasis` 返回 `readonly struct`(`ICameraRig.cs:19-29`,字段亦 `readonly`),
  无 `Transform`/`Camera` 句柄透传 ✓;`AC-2-10②` 的 API 形状判据成立。

### 2. 单向依赖(1 读 2 的基,2 不反读 1)

- **代码级 ✓**:`grep -rn "Player|MoveInput" Gameplay.Presentation/Camera/*.cs` —— **零命中**;
  1 侧只经 `ICameraRig.YawBasis` 消费(`InputContractEvaluator.cs:31`),且 `ProjectToWorld` 以**基向量入参**
  的形态可完全 fake(1 侧测试全部用 `TestCameraRig`/`CountingCameraRig` 假件,不引用 `CameraRig` 具体类)。
- **测试级 ✓**:2 侧测试(`yaw_basis_test`)不 import Player 侧类型;两向无环。
- **但**:「1 的引用集不含相机写 API」这条**只有本评审的人工核验,没有守卫测试**(见 F5b)——
  同程序集(`Gameplay.Presentation` 同住 1 与 2)下,1 侧未来写 `rig.UpdateYaw(...)` 会**编译通过且全绿**。

### 3. 与 story-001 的接缝(档位双源 · EPS 单一出处)

- **档位双源 ✓(已修)**:`CameraRig.cs:34-41` —— 2026-10-03 修复注释明载:初版自持 `_mode` 与
  `CameraModeMachine` 双写入点并存 ⇒ 现**一切经 `CameraModeMachine`**(意图制唯一写入点),`SetModeForTest`
  走 `SetMode(requesterId)+Settle`。本评审核验:`CameraRig` 无独立档位字段 ✓。
  **YawBasis 与档位解耦**:任何档位下 `YawBasis` 均只由 `_yaw` 构造(不读 `_modeMachine`)⇒ 与 story-001 的
  档位状态机自洽,无新双源。
- **`YAW_BASIS_EPS` 单一出处 ✓(本评审 grep 实测)**:全仓定义恰 1 处
  (`CameraRig.cs:44`,`public const float YAW_BASIS_EPS = 1e-5f`),消费方 `yaw_basis_test.cs` +
  `anchor_follow_orbit_test.cs`(均引用符号,非字面量)。**QA 规格要求的「唯一定义点 grep/AST 守卫测试」不存在** ——
  当前唯一性靠人肉维持(见 F5)。

### 4. `YAW_BASIS_EPS` 等常量归属(定义/消费/第二处定义)

| 常量 | 定义点 | 消费方 | 问题 |
|---|---|---|---|
| `YAW_BASIS_EPS = 1e-5f` | `CameraRig.cs:44`(唯一 ✓) | 002/003 测试引用符号 ✓ | ① **1 侧 `AC-1-31` 未回指符号** —— `input_contract_test.cs` 对同一量(单位性残差)用自有字面量 `0.001f` / `0.0001f`,正是 AC-2-08 订正要防的「调一处另一处静默失效」;② 取值 `1e-5` 无用户裁定记录(组 5b 留白);③ 无唯一性守卫测试 |
| `PITCH_MIN = -45f` / `PITCH_MAX = 60f` | `CameraRig.cs:47-48`(硬编码 `const`) | 002 测试 / `UpdatePitch` 钳制 / `ResetLookForTest` | **非「装载期断言的数据表值」** —— 无注入缝、无违例组、无缺键拒启 ⇒ `AC-2-09①` 的机制面整体缺位(见 F3)。故事明言数值归用户(组 6),当前值是实现者自填 |

**漂移源结论**:第二处**定义**不存在;第二处**容差字面量**(1 侧 `0.001f`)已存在 —— 名字不同、语义同一
(对 `‖f̂‖=1` 类量的浮点容差),漂移已实际发生。

---

## 新发现(含严重度)

| # | 严重度 | 发现 |
|---|---|---|
| F1 | 🔴 HIGH | **AC-2-07① 测试容差挖空判据**:QA 规格明写「y 逐点**精确 0,非容差** —— 出现 ~1e-8 即证明走了事后水平化路径(负向夹具)」,而 `yaw_basis_test.cs:47-48` 用 `Assert.AreEqual(0f, ..., 1e-6f)` ⇒ 事后水平化残留(1e-8 级)**照过**。判据的抓错力(本 AC 的头号失效模式)被自己的容差放行。当前实现是字面 `0f` 所以绿得对 —— 但**回归防线是假的**。修法:容差改 `0f`(或 ≤1e-9)。 |
| F2 | 🔴 HIGH | **AC-2-10①(BLOCKING)双缺**:① 相位 A/B 二选一的**决定性代码注释全仓缺失**(grep `相位\|EC-2-14` 于 `Gameplay.Presentation/Camera/` 仅中 `CameraModeMachine` 的 ADR-011③(档位 Tick,非 yaw 更新));② 探针消费方测试不存在 —— `test_frameOrderingContract` 是同线程「先 `UpdateYaw` 后读」退化版,恒真。Completion Notes **自认简化版**,即 BLOCKING AC 未交付而 Status=Complete。 |
| F3 | 🔴 HIGH | **AC-2-09① 机制缺位却记交付**:`PITCH_MIN/MAX` 为硬编码 `const`,QA 规格的注入表值三组(违例 A/B/C)+ 空值组(缺键拒启/INCONCLUSIVE)**全部不存在**;`test_pitchConstraint` 只断言 const 关系。故事留白口径本要求「真表半边记 INCONCLUSIVE **不得借绿**」,Completion Notes 却列「装载期断言常量」为已交付 —— **借绿**。另违 `coding-standards.md`「Gameplay values must be data-driven」(数值归用户 ≠ 机制免建)。 |
| F4 | 🟠 MED | **AC-2-09② 判据空转**:`test_projH_positiveLowerBound` 只算 `cos(PITCH_MAX) > 0` 一次 —— 不遍历 `pitch ∈ [0, PITCH_MAX]`、不构造 3D forward、不求 `‖proj_h(f̂)‖`。对 `const 60°` 恒真,与被测代码零耦合。 |
| F5 | 🟠 MED | **EPS 单一出处无守卫 + 1 侧已漂移**:QA 规格的「grep `YAW_BASIS_EPS` 定义==1 处,第二处字面量=红」测试不存在;1 侧 `AC-1-31` 已用自有 `0.001f`(≈100× 于 `1e-5`)。跨 Epic 接缝(1 的 `AC-1-31` 回指本符号)未兑现。 |
| F5b | 🟠 MED | **AC-2-10② 只测了两个允许判据的前半**:接口反射 ✓;但「1 的引用集不含相机写 API」无守卫测试(同程序集下 1 侧调 `CameraRig.UpdateYaw` 将静默编译通过);负向夹具(`ICameraRig` 加 `SetYaw` ⇒ 红)缺失。 |
| F6 | 🟠 MED | **测试夹具累积 bug**:`yaw_basis_test.cs` 在循环内调 `_rig.UpdateYaw(yaw)` **增量累积**(07① 内层每次 pitch 迭代都加一次 yaw;08 全周扫描实际走 `k(k+1)/2` 三角数格 mod 2π)—— 非设计的均匀采样格;07① 的「pitch 独立性」只在 `yaw=0` 那轮被干净隔离。绿得对但测的不是规格写的夹具(应 `ResetLookForTest(yaw,…)` 绝对定位)。 |
| F7 | 🟡 LOW | **AC-2-08 采样不足**:规格 ≥4096 点 + 回绕点 `2π⁻→0⁺` 显式;实测 360(且如 F6 非均匀)。回绕语义由 `Mathf.Repeat` 结构性保证,未被显式探针覆盖。 |
| F8 | 🟡 LOW | **`YAW_BASIS_EPS = 1e-5` 取值未经用户裁定**(组 5b 明文「取值归用户」,故事留白)—— 需补裁定或在故事标注「实现占位」。 |
| F9 | 🟡 LOW | **接口形状超出 ADR Amendment B 草图**:ADR `Key Interfaces` 的 `ICameraRig` 无 `Yaw`/`Pitch` getter,实现多导出两标量(1 侧现零消费,仅测试用);与故事 Forbidden 条「本接口**只**导出离散基向量」字面冲突。非写入面,不破只读纪律 —— 但属「引用却无登记」类形状漂移,建议在 story 001/002 的接缝注记里补一句归属。 |
| F10 | 🟡 LOW | **故事文件卫生**:两个 `## Completion Notes` 标题(前者仍写「Pending / 待实现」,后者写 Complete),`Test Evidence` 复选框仍 `[ ] Pending` 但 Status=Complete —— 审计面自相矛盾。 |

---

## 测试实跑(本会话亲自取证)

- **命令**:`Unity -batchmode -nographics -runTests -testPlatform EditMode -projectPath unity -testFilter "DaYiJingCheng.Tests.Camera"`(编辑器已关,`UnityLockfile` 不存在,批跑独占 ✓)
- **结果**:`unity/TestResults-639266528753107310.xml` —— **total 73 / passed 68 / failed 0 / skipped 5**(日志 `unity/Logs/td-story002-yawbasis.log`,退出码 0)
- **`YawBasisTest` 9/9 全 Passed**;5 项 skipped 均属 `CameraArmSolverTest` / `CameraObligationsReconciliationTest`(非本故事面,既有 [Ignore])。
- **判读**:9 测绿证明**当前实现的数学本体正确**;但如上 F1–F6,多条判据是**弱化/空转/未建**形态 —— 绿不等于 AC 交付。

---

## 转 Complete 前置(按优先级)

1. **F1 必修**:`yaw_basis_test.cs:47-48` 容差改精确 0(恢复「1e-8 残差 ⇒ 红」抓错力)—— 一行修复,不修则 AC-2-07① 的负向判据形同虚设。
2. **F2 必修(AC-2-10① BLOCKING)**:在 `CameraRig`/驱动点落**相位 A/B 选定注释**(EC-2-14 明文义务);补探针消费方测试(至少:跨两次驱动-读取序列,断言第 k 次读 == 第 k 次更新后的值;负向:读先于更新 ⇒ 红)。做不到则 **Status 回退 In Progress**,不得带 BLOCKING 缺口挂 Complete。
3. **F3 必须改判记账(二选一,须用户/评审确认)**:① 建注入缝 + 违例/空值夹具,AC-2-09① 真交付;或 ② 按故事既定留白口径,把 `AC-2-09①`(及②的真表半边)在故事里**显式记 INCONCLUSIVE、不计入 Complete 的已交付面**。现状「借绿」必须消除。
4. **F4 修**:`test_projH_positiveLowerBound` 改为遍历 `pitch ∈ [0, PITCH_MAX]` 求 `‖proj_h‖` 下界 == `cos(PITCH_MAX)`。
5. **F5 兑现**:加「`YAW_BASIS_EPS` 全仓定义 == 1 处」grep/AST 守卫测试;给 1 侧 `AC-1-31` 开对账项(改引符号),归玩家控制器 Epic 接缝清单。
6. **F5b 兑现**:加 1 侧引用集扫描守卫(反射/AST:Player 编译面无 `CameraRig` 具名引用或无写方法调用)。
7. **F6/F7 修**:夹具改 `ResetLookForTest` 绝对定位;08 采样提至 ≥4096 + 显式回绕点。
8. **F8**:向用户补要 `YAW_BASIS_EPS` 取值裁定(或标注占位)。
9. **F10**:清理重复 Completion Notes、勾 Test Evidence 复选框。

**不构成前置**(判为可接受偏差):构造式本体、单向依赖、档位双源修复、EPS 单一定义点现状 ——
这四项本评审核验均成立。
