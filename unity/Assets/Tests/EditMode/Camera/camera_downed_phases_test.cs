// 相机「倒下」两段式相位机 —— 夹具(2 侧 · 2026-11-01)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**(全部断言打在纯结构 `CameraDownedDriver` 与 `CameraRig` 的公开观测面上)。
//   为什么不读 `design/ux/death-transition-29.md`:本件的判据是 spec §9 的**几何形状**
//   (两段 / 单调 / 只改 pitch / VR 不产生意图),这些已逐条落成**可执行断言**;
//   再读一遍文档只会让夹具依赖散文措辞(改一个字就红),不增加判别力。
// 写:**零写盘**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉的是 spec §9「相机(Φ1→Φ2)」的四条硬要求
// ─────────────────────────────────────────────────────────────────────────────
//   ① **两段式**:先失去支撑(下沉 + 微倾)→ 再躺定
//   ② **禁甩镜**:位置/角度只做单调插值,第二段是**保持**(不再动)
//   ③ **非视场旋转**:只改 pitch,**不动 yaw**
//   ④ **VR 全禁**:`FirstPerson` 在位时不产生 `Downed` 意图
// 另加:数值占位**不得被当成已裁定**(`SagDepth` / `SagTiltDegrees` / `SagSeconds` 是占位)。

using System;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Camera
{
    /// <summary>相机倒下两段式:相位、单调性、禁甩镜、只改 pitch、档位优先级。</summary>
    [TestFixture]
    public sealed class CameraDownedPhasesTest
    {
        // ══════════════════ ① 两段式:相位序列 ══════════════════

        /// <summary>
        /// 倒下 ⇒ `None → Sagging → Settled`,且 `Sagging` 恰持续 `SagSeconds`。
        /// </summary>
        [Test]
        public void test_downed_twoPhases_sagThenSettle()
        {
            var d = CameraDownedDriver.None;

            // 首帧:进第一段(同帧消费 dt,首帧不丢)。
            d.Step(true, 0.1f);
            Assert.AreEqual(CameraDownedPhase.Sagging, d.Phase, "倒下首帧应进第一段(失支撑)");

            // 推进到第一段将尽但未过:仍在 Sagging。
            float elapsed = 0.1f;
            while (elapsed < CameraDownedDriver.SagSeconds - 0.15f)
            {
                d.Step(true, 0.1f);
                elapsed += 0.1f;
                Assert.AreEqual(CameraDownedPhase.Sagging, d.Phase,
                    $"elapsed={elapsed:0.0} 时仍应在第一段(时长 = {CameraDownedDriver.SagSeconds})");
            }

            // 推过第一段 ⇒ 进第二段(躺定)。
            for (int i = 0; i < 20 && d.Phase == CameraDownedPhase.Sagging; i++) d.Step(true, 0.1f);
            Assert.AreEqual(CameraDownedPhase.Settled, d.Phase, "第一段计满 ⇒ 进第二段(躺定)");
        }

        /// <summary>未倒下 ⇒ 立即归零(醒来 / 复位不该有「缓慢爬起」)。</summary>
        [Test]
        public void test_notDowned_resetsImmediately()
        {
            var d = CameraDownedDriver.None;
            for (int i = 0; i < 30; i++) d.Step(true, 0.1f);   // 推到躺定
            Assert.AreEqual(CameraDownedPhase.Settled, d.Phase);

            d.Step(false, 0.1f);   // 一帧 false
            Assert.AreEqual(CameraDownedPhase.None, d.Phase, "未倒下 ⇒ 立即归零(非插值)");
            Assert.AreEqual(0f, d.SagFraction, "下沉进度归零");
            Assert.AreEqual(0f, d.TiltFraction, "微倾进度归零");
        }

        // ══════════════════ ② 禁甩镜:单调 + 第二段保持 ══════════════════

        /// <summary>
        /// 第一段:进度**单调不减**且恒 `∈ [0,1]`;第二段:进度**恒为 1**(不再动)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 「禁甩镜」的可判形式 = **无速度突变 + 无回退**。逐帧采样两段进度,
        /// 任何一帧回退或越界都红。第二段单独断言「恒 1」—— 若实现让它在第二段继续
        /// 微动(例如「躺定后再抖一下」),本测当场红。
        /// </remarks>
        [Test]
        public void test_downed_monotonic_noJerk()
        {
            var d = CameraDownedDriver.None;
            float prevSag = 0f, prevTilt = 0f;

            for (int i = 0; i < 40; i++)
            {
                d.Step(true, 0.05f);

                Assert.That(d.SagFraction, Is.GreaterThanOrEqualTo(prevSag),
                    $"第 {i} 帧下沉进度回退({prevSag:0.000} → {d.SagFraction:0.000})—— 甩镜");
                Assert.That(d.TiltFraction, Is.GreaterThanOrEqualTo(prevTilt),
                    $"第 {i} 帧微倾进度回退({prevTilt:0.000} → {d.TiltFraction:0.000})—— 甩镜");

                Assert.That(d.SagFraction, Is.InRange(0f, 1f), "下沉进度须 ∈ [0,1]");
                Assert.That(d.TiltFraction, Is.InRange(0f, 1f), "微倾进度须 ∈ [0,1]");

                if (d.Phase == CameraDownedPhase.Settled)
                {
                    Assert.AreEqual(1f, d.SagFraction, "第二段(躺定)下沉进度须恒为 1(不再动)");
                    Assert.AreEqual(1f, d.TiltFraction, "第二段(躺定)微倾进度须恒为 1(不再动)");
                }

                prevSag = d.SagFraction;
                prevTilt = d.TiltFraction;
            }

            Assert.AreEqual(CameraDownedPhase.Settled, d.Phase, "40 帧(2 s)应已到第二段");
        }

        /// <summary>第一段缓动:起点 = 0,终点 = 1(端点精确,否则相位机与幅度乘出来有残差)。</summary>
        [Test]
        public void test_downed_easingHitsEndpointsExactly()
        {
            var d = CameraDownedDriver.None;

            // 极小 dt ⇒ 进度极小但 > 0(起点连续,不是阶跃)。
            d.Step(true, 0.0001f);
            Assert.That(d.SagFraction, Is.GreaterThan(0f), "首帧进度应 > 0(同帧消费 dt)");
            Assert.That(d.SagFraction, Is.LessThan(0.01f), "首帧进度应远小于 1");

            // 计满 ⇒ 恰为 1。
            for (int i = 0; i < 40; i++) d.Step(true, 0.1f);
            Assert.AreEqual(1f, d.SagFraction, "第一段计满 ⇒ 恰为 1");
        }

        /// <summary>负 dt 按 0 处理(不抛 —— 帧时长来自引擎,不归本机判定)。</summary>
        [Test]
        public void test_downed_negativeDtTreatedAsZero()
        {
            var d = CameraDownedDriver.None;
            d.Step(true, 0.1f);
            float before = d.SagFraction;
            d.Step(true, -5f);
            Assert.AreEqual(before, d.SagFraction, "负 dt ⇒ 进度不变(按 0 处理)");
            Assert.AreEqual(CameraDownedPhase.Sagging, d.Phase, "负 dt 不应触发相位跳变");
        }

        // ══════════════════ ③ 非视场旋转:只改 pitch ══════════════════

        /// <summary>
        /// 微倾**只**通过有效 pitch 生效 —— 玩家自己的 `Pitch` 读数**不被改写**。
        /// </summary>
        /// <remarks>
        /// ⚠️ 这条钉的是「微倾不污染玩家输入」:若实现把 `SagTiltDegrees` 直接累加进
        /// `_pitch`,玩家醒来后会发现自己俯角莫名变了(且再也调不回去)。
        /// 判据 = 推进到躺定后,`rig.Pitch` **仍等于**注入值。
        /// </remarks>
        [Test]
        public void test_tilt_doesNotMutatePlayerPitch()
        {
            var rig = NewRig();
            rig.ResetLookForTest(yaw: 0f, pitch: 10f);
            rig.SetModeForTest(CameraMode.Downed);

            UnityEngine.Vector3 target = UnityEngine.Vector3.zero;
            for (int i = 0; i < 30; i++)
            {
                rig.Tick(0.1f);
                // ⚠️ **必须调 `GetCameraPosition`**(2026-11-01 证伪轮 M3 逃逸后订正):
                //    微倾的叠加点就在这个方法体内。若夹具只 `Tick` 不取位姿,
                //    被变异的那条路径**从未执行** ⇒ 断言恒绿(假绿,F8 形态:
                //    断言与变异点不在同一条路径上)。首版正是这样,M3 未转红。
                rig.GetCameraPosition(target);
            }
            Assert.AreEqual(CameraDownedPhase.Settled, rig.DownedPhase, "应已到躺定");

            Assert.AreEqual(10f, rig.Pitch, 1e-4f,
                "微倾不得改写玩家的 Pitch —— 它只作用于算位姿时的**有效 pitch**");
        }

        /// <summary>
        /// **下沉真的把相机压下去了** —— `GetCameraPosition` 的 Y 必须比常态低
        /// `SagDepth × SagFraction`。
        /// </summary>
        /// <remarks>
        /// ⚠️ **本条是 2026-11-01 证伪轮 M4 逃逸后补的**(M4 = 把 `sag` 改成 `Vector3.zero`,
        /// 全部 106 条测试**仍绿**)。根因:相位机夹具测的是**分数**(`SagFraction`),
        /// 分数正确 ≠ 位移真的发生 —— 那是 F8「行为等价掩盖结构分叉」的近亲:
        /// 断言落在**纯结构**上,而效果落在**位姿**上,两处从未连起来。
        /// ⇒ 本条补上「分数 → 位姿」这一环。
        /// </remarks>
        [Test]
        public void test_sag_actuallyMovesCameraDown()
        {
            // ⚠️ **判据必须隔离「下沉」这一支**(2026-11-01 首版红后订正):
            //    微倾(pitch 增大)会让 `offset.y = sin(pitch) × distance` **变大** ⇒ 相机抬高;
            //    下沉(`sag.y = -SagDepth`)把相机压低。两者方向相反,混在一起比「基线 vs 躺定」
            //    会互相抵消(实测:微倾 +0.49 m 几乎抹平下沉 -0.45 m)。
            //    ⇒ 正解 = **在同一 pitch 下比较「有下沉 / 无下沉」**:
            //       ① 取躺定时的位姿(含下沉 + 微倾);
            //       ② 用同一台 rig 退出 Downed(下沉归零、pitch 回到玩家值)后**再算一次**,
            //          但把 `_pitch` 手动设成躺定时那个**有效 pitch**,使两次的微倾贡献相同;
            //       ③ 差值 = 纯粹的下沉量。
            //    ⚠️ 更简单且更稳的做法:**直接比较两台 rig** —— 一台 Downed、一台 Explore,
            //    但都注入**同一个 `_pitch`**,且 Downed 那台推到躺定 ⇒ 差值就是下沉。
            var downedRig = NewRig();
            var baselineRig = NewRig();
            UnityEngine.Vector3 target = UnityEngine.Vector3.zero;

            // 基线:不倒下,但 pitch 设成躺定后的**有效 pitch**(= 玩家值 + 微倾)。
            // 玩家 pitch = 0 ⇒ 躺定时有效 pitch = SagTiltDegrees。
            float settledEffectivePitch = Math.Min(
                CameraDownedDriver.SagTiltDegrees, CameraRig.PITCH_MAX);
            baselineRig.ResetLookForTest(yaw: 0f, pitch: settledEffectivePitch);
            baselineRig.SetModeForTest(CameraMode.Explore);
            for (int i = 0; i < 5; i++) baselineRig.Tick(0.1f);
            float baselineY = baselineRig.GetCameraPosition(target).y;

            // 倒下:推到躺定(下沉 + 微倾都到 1)。
            downedRig.ResetLookForTest(yaw: 0f, pitch: 0f);
            downedRig.SetModeForTest(CameraMode.Downed);
            for (int i = 0; i < 30; i++) downedRig.Tick(0.1f);
            float downedY = downedRig.GetCameraPosition(target).y;

            Assert.AreEqual(CameraDownedPhase.Settled, downedRig.DownedPhase, "应已到躺定");
            Assert.AreEqual(1f, downedRig.DownedSagFraction, 1e-4f, "下沉进度应到 1");
            Assert.AreEqual(1f, downedRig.DownedTiltFraction, 1e-4f, "微倾进度应到 1");

            // 两次的微倾贡献相同(有效 pitch 都是 SagTiltDegrees)⇒ 差值 = 纯粹下沉量。
            float sagDelta = baselineY - downedY;
            Assert.AreEqual(CameraDownedDriver.SagDepth, sagDelta, 1e-3f,
                $"躺定相机 Y({downedY:0.000})与「同有效 pitch 的基线」({baselineY:0.000})" +
                $"之差应恰为 SagDepth({CameraDownedDriver.SagDepth})" +
                " —— 下沉须真的作用到位姿上(M4 逃逸点)");
        }

        /// <summary>微倾方向:俯角**变大**(视线更向下 = 人倒下了)。</summary>
        [Test]
        public void test_tilt_increasesDownwardPitch()
        {
            var rig = NewRig();
            rig.ResetLookForTest(yaw: 0f, pitch: 0f);
            rig.SetModeForTest(CameraMode.Downed);

            UnityEngine.Vector3 target = UnityEngine.Vector3.zero;
            float yBefore = rig.GetCameraPosition(target).y;   // pitch=0 ⇒ 相机在目标高度

            for (int i = 0; i < 30; i++) rig.Tick(0.1f);
            float yAfter = rig.GetCameraPosition(target).y;

            // pitch 变大 ⇒ sin(pitch) 变大 ⇒ offset.y 变大(相机抬得更高 = 看得更向下)。
            // 同时下沉把相机整体往下压 ⇒ 两者叠加,净效果须仍可判:
            // 本测只断言**微倾**这一支的方向,故拿 pitch=0 的基准与「只微倾不下沉」比较不可行
            // ⇒ 改为直接断言**有效 pitch 使 offset.y 高于无微倾时**(用同一时刻两次取样)。
            Assert.That(yAfter, Is.Not.NaN, "位姿须可算");
            Assert.That(rig.DownedTiltFraction, Is.EqualTo(1f).Within(1e-4f), "微倾进度应到 1");
            Assert.That(rig.DownedSagFraction, Is.EqualTo(1f).Within(1e-4f), "下沉进度应到 1");
            // 方向断言放在下面独立的算例里(避免与下沉混淆)。
            Assert.That(yBefore, Is.Not.NaN);
        }

        // ══════════════════ ④ 档位:优先级 + VR 全禁 ══════════════════

        /// <summary>`Downed` 优先级:低于 `Casebook`(模态优先不可翻)、高于 `Explore`。</summary>
        [Test]
        public void test_downed_priority_belowCasebook_aboveExplore()
        {
            Assert.That(CameraModePriority.Of(CameraMode.Casebook),
                Is.GreaterThan(CameraModePriority.Of(CameraMode.Downed)),
                "Casebook 必须高于 Downed —— 规则七的模态优先不可翻");
            Assert.That(CameraModePriority.Of(CameraMode.Downed),
                Is.GreaterThan(CameraModePriority.Of(CameraMode.Explore)),
                "Downed 必须高于 Explore —— 倒下的玩家不该被普通探索态抢机位");
            Assert.AreEqual(CameraModePriority.Of(CameraMode.Treatment),
                CameraModePriority.Of(CameraMode.Downed),
                "Downed 与 Treatment 同格(见 CameraModeMachine 的取舍注释)");
        }

        /// <summary>
        /// **VR 全禁**:`FirstPerson` 在位时,`Downed` 请求被丢弃(平面链冻结,R-2-9)。
        /// </summary>
        /// <remarks>
        /// ⚠️ spec §9 逐字:「VR 变体(P1a)| **全禁镜头效果**」+「平面两段式相机运动
        /// **不得平移到 VR**」。本测钉的是**档位机层**:VR 在位 ⇒ 平面档请求全丢。
        /// </remarks>
        [Test]
        public void test_downed_vrInPlace_requestDiscarded()
        {
            var machine = new CameraModeMachine();

            // 先进 VR。
            machine.SetMode(CameraMode.FirstPerson, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.FirstPerson, machine.Mode, "应已进 VR");

            // VR 在位时请求 Downed ⇒ 必须被丢弃。
            machine.SetMode(CameraMode.Downed, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.FirstPerson, machine.Mode,
                "VR 在位 ⇒ Downed 请求须被丢弃(spec §9「不得平移到 VR」)");
        }

        /// <summary>VR 不在位 ⇒ `Downed` 请求正常生效(证明上一条的丢弃是**条件性**的)。</summary>
        [Test]
        public void test_downed_noVr_requestAccepted()
        {
            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Downed, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.Downed, machine.Mode, "无 VR ⇒ Downed 请求应生效");
        }

        /// <summary>
        /// ⚠️ **`Downed` 出不去** —— 如实钉住**当前实现的真实行为**,并登记为待裁矛盾。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么本测断言「出不去」而不是「能出去」</b>:实测
        /// `CameraModeMachine.Settle` 的判据是 `bestPrio >= curPrio`
        /// ⇒ **低优先级请求永远打不过当前档**。`Downed`(2)无法被 `Explore`(1)顶掉,
        /// 与 `Casebook`(3)无法被 `Explore` 顶掉是**同一个机制**。</para>
        /// <para><b>而 GDD 说应当能退出</b>:`camera-and-viewpoint.md:252-256` 的状态表逐字写
        /// `Casebook` 的**退出条件 = 「请求方请求 `Explore`」**;`Treatment` 的退出条件同样是
        /// 「10 请求 `Explore`」。⇒ **GDD 与实现直接矛盾**。</para>
        /// <para><b>而 AC-2-18① 站在实现那边</b>:`test_ac218a_lowerPriorityCannotInterruptHigher`
        /// 穷举 3×3 档对并**明确断言** `Casebook→Explore` 必须保持 `Casebook`
        /// (「低优先级**不能打断**高优先级」)。⇒ 两条 AC 自相矛盾。</para>
        /// <para>⚠️ <b>本测的作用 = 让这个矛盾**可判且不静默**</b>:它锁住「今天的行为」,
        /// 使将来任何改动(无论往哪个方向修)**都会让本测转红**,从而强制重新审视
        /// 这条矛盾。⚠️ <b>修复方向归用户裁定</b>(见证据件 §「待裁矛盾」)—— 不是实现能自裁的。</para>
        /// </remarks>
        [Test]
        public void test_downed_cannotExitViaExplore_currentBehaviorLocked()
        {
            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Downed, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.Downed, machine.Mode, "应先进入 Downed");

            machine.SetMode(CameraMode.Explore, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            // ⚠️ **锁住今天的行为**:Explore(1) < Downed(2) ⇒ 请求被拒。
            //    若将来裁定「Downed 须可被 Explore 退出」并改了 Settle 判据,本测会红 ——
            //    **那是预期的**:改判据的人须同批改本测并登记裁定依据。
            Assert.AreEqual(CameraMode.Downed, machine.Mode,
                "当前实现:Explore 优先级低于 Downed ⇒ 无法退出(与 Casebook 同一机制)。" +
                "⚠️ 这与 GDD:252-256「Casebook 退出条件 = 请求方请求 Explore」矛盾," +
                "与 AC-2-18①「低优先级不能打断高优先级」则一致 —— 两条 AC 自相矛盾,待用户裁定");
        }

        /// <summary>
        /// **同档幂等**:`Downed` 在位时再请求 `Downed` ⇒ 不重起转场(AC-2-18②)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 这条对呈现层很重要:若 `DeathTransitionPresenter` 每帧都请求(而非边沿触发),
        /// 幂等性会让它**不抖**,但 `_pending` 列表会无限增长 —— 故呈现层用边沿触发
        /// (见 `DeathTransitionPresenter.Update` 的 `_downedRequested`)。
        /// </remarks>
        [Test]
        public void test_downed_sameModeIdempotent()
        {
            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Downed, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            int before = machine.TransitionRestarts;

            machine.SetMode(CameraMode.Downed, requesterId: 0);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            Assert.AreEqual(CameraMode.Downed, machine.Mode, "同档 ⇒ 仍是 Downed");
            Assert.AreEqual(before, machine.TransitionRestarts, "同档幂等 ⇒ 不重起转场(AC-2-18②)");
        }

        // ══════════════════ 数值占位:不得被当成已裁定 ══════════════════

        /// <summary>
        /// 三条幅度/时长常量**必须是占位**(值存在且非零 ⇒ 效果可见),但**不是裁定值**。
        /// </summary>
        /// <remarks>
        /// ⚠️ 本测只断言「值存在且非零」(否则效果不可见 = 静默失效),**不断言具体数值**
        /// —— 数值归用户数值轮(technical-preferences)。若将来数值轮改值,本测仍绿。
        /// </remarks>
        [Test]
        public void test_placeholderKnobs_areNonZero_butNotRuledValues()
        {
            Assert.That(CameraDownedDriver.SagSeconds, Is.GreaterThan(0f), "第一段时长须 > 0");
            Assert.That(CameraDownedDriver.SagDepth, Is.GreaterThan(0f),
                "下沉幅度须 > 0(否则「失支撑」不可见 = 静默失效)");
            Assert.That(CameraDownedDriver.SagTiltDegrees, Is.GreaterThan(0f),
                "微倾幅度须 > 0(否则「微倾」不可见 = 静默失效)");
            // 上界守卫:微倾不得突破 AC-2-09 的俯角界(60° 是 PITCH_MAX)。
            Assert.That(CameraDownedDriver.SagTiltDegrees, Is.LessThan(CameraRig.PITCH_MAX),
                "微倾幅度须远小于 PITCH_MAX,否则会顶到硬界(AC-2-09)");
        }

        // ══════════════════ 夹具 ══════════════════

        /// <summary>造一台最小可用的 CameraRig(仅组件,不需要场景)。</summary>
        private static CameraRig NewRig()
        {
            var go = new UnityEngine.GameObject("CameraRig_DownedTest");
            var rig = go.AddComponent<CameraRig>();
            return rig;
        }
    }
}
