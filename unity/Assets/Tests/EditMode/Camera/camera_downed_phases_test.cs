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
            // 🔁 **2026-11-01 简化**(微倾方向修正后):初版要用**两台 rig** 隔离微倾,
            //    因为那时微倾通过 pitch 把相机**抬高**、与下沉抵消(实测 -0.45 m 被 +0.49 m 抹平)。
            //    现微倾只动**视线目标**、不动相机位置 ⇒ 下沉是位置唯一的贡献者 ⇒
            //    单台 rig 即可判,差值应**恰等于** `SagDepth`。
            var rig = NewRig();
            rig.ResetLookForTest(yaw: 0f, pitch: 0f);
            UnityEngine.Vector3 target = UnityEngine.Vector3.zero;

            rig.SetModeForTest(CameraMode.Explore);
            for (int i = 0; i < 5; i++) rig.Tick(0.1f);
            float baselineY = rig.GetCameraPosition(target).y;

            rig.SetModeForTest(CameraMode.Downed);
            for (int i = 0; i < 30; i++) rig.Tick(0.1f);
            float downedY = rig.GetCameraPosition(target).y;

            Assert.AreEqual(CameraDownedPhase.Settled, rig.DownedPhase, "应已到躺定");
            Assert.AreEqual(1f, rig.DownedSagFraction, 1e-4f, "下沉进度应到 1");

            float sagDelta = baselineY - downedY;
            Assert.AreEqual(CameraDownedDriver.SagDepth, sagDelta, 1e-3f,
                $"躺定相机 Y({downedY:0.000})与常态({baselineY:0.000})之差应**恰为** " +
                $"SagDepth({CameraDownedDriver.SagDepth}) —— 微倾不再参与位置,故差值必须精确" +
                "(M4 逃逸点:下沉须真的作用到位姿上)");
        }

        /// <summary>微倾方向:俯角**变大**(视线更向下 = 人倒下了)。</summary>
        [Test]
        public void test_tilt_dropsLookTarget_doesNotRaiseCamera()
        {
            // 🔁 **2026-11-01 重写**(图形走查发现微倾方向做反后):
            //    初版微倾做进「有效 pitch」⇒ `offset.y = sin(pitch)×distance` 让相机**抬高**,
            //    与下沉**反向抵消**(实测地平线只上移 2.6% 屏高)。首版本测只断 `Is.Not.NaN`
            //    —— **空洞正控**(F4),什么都没测。
            //    现微倾走「视线目标下移」⇒ 本测钉三条:
            //      ① 视线目标**向下移**(微倾生效);
            //      ② 相机位置**不被微倾抬高**(方向正确);
            //      ③ 常态(未倒下)时 `GetLookTarget` **恒等于** target(零偏移,不污染常态)。
            var rig = NewRig();
            rig.ResetLookForTest(yaw: 0f, pitch: 0f);
            UnityEngine.Vector3 target = new UnityEngine.Vector3(0f, 1f, 0f);

            // ③ 常态:视线目标 = target 本身(微倾未生效)。
            rig.SetModeForTest(CameraMode.Explore);
            UnityEngine.Vector3 lookBefore = rig.GetLookTarget(target);
            Assert.AreEqual(target.y, lookBefore.y, 1e-5f,
                "常态下 `GetLookTarget` 须**恒等于** target(否则会污染正常取景)");

            // 推进到躺定。
            rig.SetModeForTest(CameraMode.Downed);
            for (int i = 0; i < 30; i++) rig.Tick(0.1f);
            Assert.AreEqual(1f, rig.DownedTiltFraction, 1e-4f, "微倾进度应到 1");

            UnityEngine.Vector3 lookAfter = rig.GetLookTarget(target);

            // ① 视线目标**向下移**(y 变小)。
            Assert.That(lookAfter.y, Is.LessThan(lookBefore.y),
                $"躺定时视线目标 y({lookAfter.y:0.000})须**低于**常态({lookBefore.y:0.000})" +
                " —— 微倾 = 视线向下(spec §9「先失去支撑」)");

            // ② 相机位置**不被微倾抬高**:躺定时相机 y 须低于「同 pitch 的常态」。
            //    (下沉在压、微倾不再抬 ⇒ 净效果一定是往下。)
            var baselineRig = NewRig();
            baselineRig.ResetLookForTest(yaw: 0f, pitch: 0f);
            baselineRig.SetModeForTest(CameraMode.Explore);
            for (int i = 0; i < 5; i++) baselineRig.Tick(0.1f);
            float baselineY = baselineRig.GetCameraPosition(target).y;
            float downedY = rig.GetCameraPosition(target).y;

            Assert.That(downedY, Is.LessThan(baselineY),
                $"躺定时相机 y({downedY:0.000})须低于常态({baselineY:0.000})" +
                " —— 微倾**不得**把相机抬高(那会与下沉反向抵消)");
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
        /// **甲案**(2026-11-01 用户裁定):`Downed` 可被**同一请求方**的 `Explore` 退出。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么这样才对</b>:`camera-and-viewpoint.md:256` 逐字写
        /// 「退出条件 = 请求方请求 `Explore`(**只能由请求方退出**)」;`:768` 的 `O-12` 同款。
        /// ⇒ 「同一请求方」是 GDD 的**明文语义**,甲案是它的直接实现。</para>
        /// <para>⚠️ <b>本测的前身是 `..._currentBehaviorLocked`</b>(断言「出不去」),
        /// 那是 2026-11-01 首版把**矛盾**锁成可判的临时形态。用户裁定甲案后,它翻成正面断言 ——
        /// 这正是那条「锁住现状」测试**发挥作用的方式**:它逼着改判据的人同批改它。</para>
        /// </remarks>
        [Test]
        public void test_downed_sameRequester_canExitViaExplore()
        {
            const int Requester = 29;   // 29 = 死亡过渡(生产接线用的号)

            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Downed, Requester);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.Downed, machine.Mode, "应先进入 Downed");
            Assert.AreEqual(Requester, machine.CurrentRequesterId, "须记下请求方");

            // 同一请求方发 Explore ⇒ **可以**降档(甲案)。
            machine.SetMode(CameraMode.Explore, Requester);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            Assert.AreEqual(CameraMode.Explore, machine.Mode,
                "同一请求方的 Explore 须能退出 Downed(GDD:256「只能由请求方退出」)");
        }

        /// <summary>
        /// **甲案的边界**:`Downed` **不能**被**别的请求方**的 `Explore` 顶掉。
        /// </summary>
        /// <remarks>
        /// ⚠️ 这条是上一条的**负控**,也是甲案「为什么限定同一请求方」的理由:
        /// 若放开成「任意 Explore 可降档」,39 的脉案会被 10 的收尾请求顶掉(反之亦然)
        /// —— 那会让两个系统互抢机位,正是 `O-12` 要防的。
        /// </remarks>
        [Test]
        public void test_downed_differentRequester_cannotDowngrade()
        {
            const int Owner = 29;      // 开的人
            const int Other = 39;      // 别的人

            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Downed, Owner);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.Downed, machine.Mode);

            machine.SetMode(CameraMode.Explore, Other);   // 别人想关
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            Assert.AreEqual(CameraMode.Downed, machine.Mode,
                "**非**同一请求方的 Explore 不得降档 —— 否则两个系统会互抢机位(O-12)");
        }

        /// <summary>
        /// 甲案对 **`Casebook`** 同样生效:39 开、39 关 ⇒ 能退出(这是矛盾的另一半)。
        /// </summary>
        /// <remarks>
        /// ⚠️ 本条是甲案**真正的动机**:`Casebook` 卡死才是 GDD:256 最直接违反的那一处。
        /// `Downed` 只是同一机制的第二个受害者。
        /// </remarks>
        [Test]
        public void test_casebook_sameRequester_canExitViaExplore()
        {
            const int CasebookRequester = 39;

            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Casebook, CasebookRequester);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);
            Assert.AreEqual(CameraMode.Casebook, machine.Mode);

            machine.SetMode(CameraMode.Explore, CasebookRequester);   // 39 自己关
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            Assert.AreEqual(CameraMode.Explore, machine.Mode,
                "39 开的脉案须能被 39 自己关掉(GDD:256「只能由请求方退出」)");
        }

        /// <summary>
        /// 甲案**不削弱** AC-2-18①:`Casebook` 仍不能被**别的**请求方打断。
        /// </summary>
        [Test]
        public void test_casebook_differentRequester_stillCannotInterrupt()
        {
            var machine = new CameraModeMachine();
            machine.SetMode(CameraMode.Casebook, 39);
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            machine.SetMode(CameraMode.Treatment, 10);   // 10 想抢脉案的机位
            machine.Settle(dt: 1f, transitionDuration: 0.0001f);

            Assert.AreEqual(CameraMode.Casebook, machine.Mode,
                "低优先级**仍不能**打断高优先级(AC-2-18① 不被甲案削弱)");
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
