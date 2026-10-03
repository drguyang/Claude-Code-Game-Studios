# QA Lead 独立评审 — camera-viewpoint Story 002 (yaw-basis-hard-delivery)

- **评审对象**: `production/epics/camera-viewpoint/story-002-yaw-basis-hard-delivery.md`(Status: Complete)
- **实现**: `unity/Assets/Gameplay.Presentation/Camera/ICameraRig.cs` · `CameraRig.cs`
- **测试**: `unity/Assets/Tests/EditMode/Camera/yaw_basis_test.cs`(9 条,归属本 story — 文件头注释自证 AC-2-07/08/09/10)
- **评审日期**: 2026-10-03
- **评审者**: qa-lead(五步循环第 2 步 · 独立评审)
- **声明**: 本报告评的是**当下代码**(2026-10-03 工作树,commit 782de44 之后),不是 story Completion Notes 的声称。
- **亲测证据**: 2026-10-03 本人 batch 跑 EditMode(`-testFilter DaYiJingCheng.Tests.Camera`)——
  73 用例 / 68 Passed / **0 Failed** / 5 Skipped;其中 `YawBasisTest` **9/9 Passed**。
  日志 `unity/Logs/qa-story002-review.log`,结果 `unity/TestResults-639266529735453620.xml`。
  (注:story 声称的「1461/1489」XML 未在 `unity/Logs/` 找到 —— **未验证**;TD 今日的
  `td-story002-yawbasis.log` 被截断且 `/tmp/td-story002-yawbasis.xml` 不存在 —— **未验证**。)

## AC 逐条判决表

| AC | 要点 | 判决 | 依据 |
|---|---|---|---|
| AC-2-07① | 水平化 y==0 与 pitch 无关(注入夹具,**精确 0 非容差**) | ⚠️ 部分 | 测试遍历 5 pitch × 5 yaw 但断言容差 1e-6(`yaw_basis_test.cs:47-48`);spec 明写「出现 ~1e-8 即红」,1e-6 容差下 1e-8 残差**照样绿** ⇒ 负夹具(事后水平化形态)抓不到。测试本身 9/9 绿(亲测) |
| AC-2-07② | 手性防镜像(两锚点 + 全周 cross 等式) | ⚠️ 部分 | 两锚点(yaw=0 / π/2)逐分量断言存在且绿(`:55-76`);spec Then 的**全周逐分量 `normalize(cross(worldUp,f̂))` 等式未实现** |
| AC-2-08 | 正交归一全周 ≥4096 采样 + EPS 唯一字面量 | ⚠️ 部分 | 采样 **360 < 4096**(`:82`);回绕点 `2π⁻→0⁺` 未显式。`YAW_BASIS_EPS` 唯一定义点 **亲验通过**(全仓 grep 唯 `CameraRig.cs:44`);但「第二处字面量 ⇒ 夹具红」的**源扫描判据不存在**;测试引用符号 ✓ |
| AC-2-09①②③ | 俯角界:装载期断言 / proj_h 正下界 / 防御兜底 | ❌ 不通过 | ① `PITCH_MIN/MAX` 是编译期 `const`(`CameraRig.cs:47-48`),**无数据表、无装载期路径、违例组 A/B/C + 空值组注入夹具全缺**;② `test_projH_positiveLowerBound` 只断言 `cos(PITCH_MAX)>0`(`:142-143`)—— 对常量的恒真算术,**未遍历 pitch 求 proj_h 模长**;③ `test_defensiveFallback_pitchMax90` 注释自认「不依赖 PITCH_MAX 的实际值」(`:151`),**从未构造 PITCH_MAX≥90 错配**,实为 AC-2-07/08 在单点的复读 |
| AC-2-10① | 帧内次序契约(探针消费方) | ❌ 不通过 | `test_frameOrderingContract` = `UpdateYaw(1.0f)` 后读回断言等于刚写入的值(`:128-134`)—— **写后读重言式(失效型 3:断言直传入参)**;无探针消费方、无帧内多时点、无跨帧因果、无负夹具(相位落在消费方之后 ⇒ 红)。Completion Notes **自认「简化版,完整版需探针消费方」而 Status=Complete** |
| AC-2-10② | 相机对 1 零 public 写入面(反射) | ⚠️ 部分 | `test_noPublicWriteSurface` 扫 `ICameraRig`:加 setter(`set_*`)会红 ✓、加 `SetYaw` 方法会红 ✓;但**未扫 `YawBasis` struct 公有字段 readonly 形状**(spec edge case),**未扫 assembly 级** —— `CameraRig` 公有类带 `UpdateYaw/ResetLookForTest/SetModeForTest` 等写方法,若 1 的引用集可见该类即暴露(spec 判据是「1 可见面」非仅接口) |

## 判据有效性专节(🔴 八型排查)

| # | 位置 | 失效型 | 判决 | 可复现命令 |
|---|---|---|---|---|
| 1 | `yaw_basis_test.cs:125-135` AC-2-10① | **型 3 断言直传入参(无因果)** | ❌ 真问题 | 读该 10 行:`UpdateYaw(1.0f)` → 断言 `Yaw==1.0f`;无任何次序/帧要素 |
| 2 | `yaw_basis_test.cs:147-160` AC-2-09③ | **型 5 语义假设当事实(空转)** | ❌ 真问题 | `grep -n "不依赖 PITCH_MAX" yaw_basis_test.cs` —— 自认不构造错配条件 |
| 3 | `yaw_basis_test.cs:138-144` AC-2-09② | **恒真常量断言(停存在层)** | ❌ 真问题 | 断言对象是 `Mathf.Cos(const)>0`,与被测实现零交互 |
| 4 | `yaw_basis_test.cs:47-48` AC-2-07① | **容差吞掉负夹具**(spec 要精确 0) | ⚠️ 真问题 | spec:「出现 ~1e-8 即证明事后水平化 ⇒ 红」;测试 `1e-6f` 容差下 1e-8 绿 |
| 5 | `yaw_basis_test.cs:82` AC-2-08 | 采样数 360 < spec 4096 | ⚠️ 真问题 | `grep -n "samples = " yaw_basis_test.cs` |
| 6 | `yaw_basis_test.cs:55-76` AC-2-07② | 判据不完整(全周 cross 等式缺) | ⚠️ 真问题 | spec Then 三段只落了锚点段 |
| 7 | AC-2-09 注入夹具组 | **判据缺失**(违例 A/B/C、空值组零测试) | ❌ 真问题 | `grep -n "90f\|91f\|违例\|空值" yaw_basis_test.cs` → 0 命中 |
| 8 | 1 侧回指纪律 | 判据缺失 + 现存违例 | ⚠️ 真问题 | spec:「1 的测试引用符号本身」;实测 `input_contract_test.cs:73` AC-1-31 用**自有字面量 `0.001f`** —— 回指夹具不存在,违例已发生(跨 Epic,归 1 侧) |
| — | 相位落点代码注释(EC-2-14 硬义务) | 义务缺失 | ⚠️ 真问题 | `grep -rn "onAfterUpdate\|相位" unity/Assets/Gameplay.Presentation/Camera/` —— CameraRig/ICameraRig **无「二选一钉死」注释** |
| — | 手性/构造式本体 | — | ✓ 亲验 | 构造式逐字落地(`CameraRig.cs:62-68`),9/9 绿;镜像负夹具未实测突变 —— **未验证** |

## 新发现(含严重度)

1. **[S2] AC-2-10① BLOCKING AC 无真实判据** — 次序契约测试为写后读重言式,探针消费方、相位钉死注释、负夹具三者皆缺;story 自认简化版仍标 Complete。Logic 型 BLOCKING ⇒ 按门禁**阻塞 Complete**。
2. **[S2] AC-2-09 三段整体塌陷** — const 化使装载期/注入夹具路径结构性不存在;②③ 为恒真/空转。spec QA 用例(违例组注入 + 错误串点名)零实现。
3. **[S3] AC-2-07① 容差 1e-6 吞噬 spec 明确要抓的 1e-8 残差** — 负夹具失效。
4. **[S3] AC-2-08 采样 360 < 4096;回绕点未显式;EPS 唯一性无源扫描守卫**。
5. **[S3] AC-2-10② 只扫接口不扫结构** — `YawBasis` 字段 readonly 形状与 assembly 级可见面未覆盖。
6. **[S3] 1 侧回指违例**:`input_contract_test.cs` AC-1-31 用 `0.001f` 自有字面量,`YAW_BASIS_EPS` 回指纪律在 1 侧已破(归玩家控制器 Epic,互指登记缺失)。
7. **[S4] story 文件缺陷**:两个重复的 `## Completion Notes` 段;Test Evidence 段仍写 `[ ] Pending — story not yet implemented` 与 Status=Complete 矛盾;「1461/1489」运行证据文件未找到(未验证)。

## 转 Complete 前置

1. **[BLOCKING]** 重写 AC-2-10①:探针消费方(记录帧号/时序/读值)、契约窗口内读值 == 本帧更新值、跨帧因果、负夹具(相位落后 ⇒ 红);并按 EC-2-14 在代码注释钉死相位二选一。
2. **[BLOCKING]** 补 AC-2-09 注入夹具:违例组 A/B/C(90°/91°/无仰视)+ 空值组 ⇒ 拒绝启动或 INCONCLUSIVE;③ 须在构造错配条件下仍满足 07/08;② 须遍历 pitch 求 proj_h 下界。const 若为终态设计,须先裁决偏差(spec 要数据表装载期)再改判,不得以 const 静默替换。
3. **[S3]** 07① 断言改**精确 0**(delta=0);08 采样 ≥4096 + 显式回绕点;07② 补全周 cross 等式;10② 补 `YawBasis` 字段 readonly 断言。
4. **[S3]** 登记 1 侧回指违例(`0.001f`)给玩家控制器 Epic;补「全仓第二处 EPS 字面量 ⇒ 红」的源扫描夹具。
5. **[S4]** 清理重复 Completion Notes、矛盾的 Test Evidence Pending 行。
6. 修完复跑:本人可复跑 `-testFilter DaYiJingCheng.Tests.Camera`(编辑器关闭时),0 红方可复议。
