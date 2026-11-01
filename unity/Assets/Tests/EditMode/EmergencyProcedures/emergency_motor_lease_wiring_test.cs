// story-011 —— 移动压制租约的**实例层**夹具:10 到底有没有碰 `LeaseSource.Emergency` 那一位。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/**` 全部生产 `.cs`(**仅** `test_s11_05_...` 一条,源面扫描唯一构造点)。
// 写:**零写盘**。其余各测全在内存里跑(真 `MotorLease` + 合成流)。
//
// ── 本文件替代了什么 ──────────────────────────────────────────────────────────
// `AC-10-13` 此前由 `modal_phase_test.cs` 的 4 条断言顶替 —— 它们断的是
// `ModalPhaseEvaluator` **自己返回的 bool**,工厂里写死、改坏生产也不会红(F1)。
// 那两个布尔载体已于 2026-11-02 退役(story-011 R-11.5)。
//
// ── 本文件测什么 / 不测什么 ────────────────────────────────────────────────────
// ✅ 测:**哪一位**被置/清、**几条出口**都清了、**别人的位**没被碰、唯一构造点。
// ❌ 不测:**玩家是否真的走不动** —— 那是 L2 真机层
//    (`Tests/PlayMode/EmergencyProcedures/emergency_motor_suppression_wiring_test.cs`)。
//
// 权威来源:
//   `design/gdd/emergency-procedures.md` 规则九 `:374` · 规则六之甲 `:724` · `AC-10-13` `:1056`
//   `design/gdd/player-controller-and-movement.md` 轴 4 `:271`(per-source 位图 + OR 聚合)
//   `production/epics/emergency-procedures/story-011-motor-suppression-lease-wiring.md`

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Presentation.Player;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.EmergencyProcedures
{
    /// <summary>
    /// story-011 L1:`MotorLease` 的 `LeaseSource.Emergency` 位 —— 置位 / 清位 / 不越界。
    /// </summary>
    [TestFixture]
    internal sealed class EmergencyMotorLeaseWiringTest
    {
        private const int Actor = 7;
        private const int ActionOrdinal = 0;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private EventStream _stream;
        private MotorLease _lease;
        private EmergencyChannelDriver _driver;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new PresentPresence());
            _tick = 100;

            var processor = new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder);
            var manual = new EmergencyManualDriver(processor, new PresentPresence());
            var channel = new EmergencyDirectReadChannel(null, 65536, 0, 65536);

            _lease = new MotorLease();
            _driver = new EmergencyChannelDriver(channel, manual, () => _tick);
            _driver.BindMotorLease(_lease);
        }

        // ══════════════ S11-01 开始置位 ══════════════

        [Test]
        public void test_s11_01_arm_acquiresEmergencyBit()
        {
            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency), "前置:动作开始前 10 的位须为空");

            _driver.Arm(ActionOrdinal, new PatientId(1));

            Assert.IsTrue(_lease.HasLease(LeaseSource.Emergency),
                "动作开始 ⇒ 置 `LeaseSource.Emergency`(规则九)");
            Assert.IsTrue(_lease.IsSuppressed, "聚合位随之变 true(OR 语义)");
        }

        /// <summary>
        /// ⚠️ **本 fixture 最危险的一条**:`Acquire` 若提到 `_channel.Arm` **之前**,
        /// 抛出的那一轮会留下一个**没有动作的压制位** —— 而没有任何东西会去清它 ⇒ 玩家永远动不了。
        /// 档位意图漏了只是相机不对,这里漏了是卡死。
        /// </summary>
        [Test]
        public void test_s11_01_armOnArmed_throws_andDoesNotAcquire()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));
            _driver.EndAction(Actor);              // 先干净地结束一轮 ⇒ 位应已清
            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency), "前置:上一轮已清位");

            // 再造一次「已 Armed 时二次 Arm」:先 Arm,然后不结束直接再 Arm
            _driver.Arm(ActionOrdinal, new PatientId(1));
            _lease.Release(LeaseSource.Emergency);   // 人为清掉,以便观察「抛出的那一轮是否重新置位」

            Assert.Throws<InvalidOperationException>(
                () => _driver.Arm(ActionOrdinal, new PatientId(1)),
                "非 Idle 态二次 Arm 由通道抛(story-007 既有行为)");

            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency),
                "抛出的那一轮**不得**留下压制位 —— 置位必须在 `_channel.Arm` 成功之后(S11-01 负例)");
        }

        // ══════════════ S11-02 三条结束出口清位 ══════════════

        [Test]
        public void test_s11_02_endAction_withSamples_releasesBit()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));
            _tick++;
            _driver.FeedForTest(axis: 1f, newlyPressed: true);

            _driver.EndAction(Actor);

            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency), "正常结束 ⇒ 清位(规则九)");
            Assert.IsFalse(_lease.IsSuppressed, "聚合位回到 false");
        }

        /// <summary>
        /// ⚠️ 零样本签约是**合法**的一次动作起止 —— 流上零事件,但压制**必须**清掉。
        /// 把 `Release` 写成以「有事件」为条件,本条即红。
        /// </summary>
        [Test]
        public void test_s11_02_endAction_zeroSamples_stillReleasesBit()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            bool applied = _driver.EndAction(Actor);

            Assert.IsFalse(applied, "零样本 ⇒ 零事件(F-10.5)");
            Assert.IsFalse(_stream.Events.Any(e => e.Kind == EventKind.EmergencyTreatmentApplied),
                "前置:本用例确实走了零样本路径");
            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency),
                "**零事件 ≠ 不清位** —— 动作结束了,压制就必须撤掉");
        }

        [Test]
        public void test_s11_02_skipAction_releasesBit()
        {
            // 跳过路径:甲案后由 SkipAction 经办(不 Arm,所以本来就没置位 —— 这里先置一位模拟
            // 「跳过发生在动作中」的防御形态,确保 Release 真的被执行而不是恰好没置位)
            _lease.Acquire(LeaseSource.Emergency);

            _driver.SkipAction(new PatientId(1), Actor);

            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency),
                "跳过入口结束 ⇒ 清位(规则九「含跳过」)");
        }

        // ══════════════ S11-03 两条中止出口清位 ══════════════

        [Test]
        public void test_s11_03_abortAction_releasesBit()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));
            Assert.IsTrue(_lease.HasLease(LeaseSource.Emergency), "前置:已置位");

            _driver.AbortAction();

            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency), "停手中止 ⇒ 清位(规则六之甲)");
        }

        [Test]
        public void test_s11_03_tickWatchdogAbort_releasesBit()
        {
            _driver.Arm(ActionOrdinal, new PatientId(1));

            _driver.OnTickEdge(_tick + ModalPhaseEvaluator.AbortIdleTicks);

            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency),
                "tick 看门狗中止走同一条清位路径(不重复实现也不遗漏)");
        }

        // ══════════════ S11-04 只碰自己那一位 ══════════════

        [Test]
        public void test_s11_04_onlyTouchesEmergencyBit_neverOthers()
        {
            _lease.Acquire(LeaseSource.Self);      // 4
            _lease.Acquire(LeaseSource.Combat);    // 25

            _driver.Arm(ActionOrdinal, new PatientId(1));
            _driver.EndAction(Actor);
            _driver.Arm(ActionOrdinal, new PatientId(1));
            _driver.AbortAction();

            Assert.IsTrue(_lease.HasLease(LeaseSource.Self),
                "4 的位**全程不得被 10 碰**(三位互不知晓,AC-1-23)");
            Assert.IsTrue(_lease.HasLease(LeaseSource.Combat),
                "25 的位**全程不得被 10 碰**");
            Assert.IsFalse(_lease.HasLease(LeaseSource.Emergency), "10 自己的位在结束时已清");
        }

        // ══════════════ 绑定观测面(EditMode 半边;生产启动序半边见 L2)══════════════

        [Test]
        public void test_bindMotorLease_reflectsBinding()
        {
            var channel = new EmergencyDirectReadChannel(null, 65536, 0, 65536);
            var manual = new EmergencyManualDriver(
                new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder),
                new PresentPresence());
            var unbound = new EmergencyChannelDriver(channel, manual, () => _tick);

            Assert.IsFalse(unbound.HasMotorLease, "未绑定 ⇒ HasMotorLease = false(漏绑的观测面)");
            unbound.BindMotorLease(_lease);
            Assert.IsTrue(unbound.HasMotorLease, "绑定后 ⇒ true");
        }

        [Test]
        public void test_unboundLease_noThrow_andNoSideEffect()
        {
            var channel = new EmergencyDirectReadChannel(null, 65536, 0, 65536);
            var manual = new EmergencyManualDriver(
                new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder),
                new PresentPresence());
            var unbound = new EmergencyChannelDriver(channel, manual, () => _tick);

            Assert.DoesNotThrow(() => unbound.Arm(ActionOrdinal, new PatientId(1)),
                "未绑租约不得抛 —— 逻辑层 / EditMode 是合法形态");
            Assert.DoesNotThrow(() => unbound.EndAction(Actor));
            Assert.IsFalse(_lease.IsSuppressed, "未绑定时不会误改任何别的租约实例");
        }

        // ══════════════ S11-05 唯一构造点 ══════════════

        /// <summary>
        /// `new MotorLease()` 在生产树里**恰一处**(`PlayerController` 的字段初始化)。
        /// 两份位图 = 两个真源 ⇒ 4 / 25 / 10 各压各的,聚合位永远对不上。
        /// </summary>
        [Test]
        public void test_s11_05_motorLease_hasExactlyOneProductionConstruction()
        {
            var hits = new List<string>();
            foreach (var path in Directory.EnumerateFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').Contains("/Tests/"))
                    continue;
                string text = File.ReadAllText(path);
                if (text.Contains("new MotorLease()"))
                    hits.Add(path.Replace('\\', '/'));
            }

            Assert.That(hits, Has.Count.EqualTo(1),
                "`new MotorLease()` 的生产构造点须**恰一处**(1 的租约真源)。\n" +
                "命中:" + string.Join(", ", hits));
            Assert.That(hits[0], Does.EndWith("Gameplay.Presentation/Player/PlayerController.cs"),
                "唯一构造点必须是 `PlayerController`(GDD 1 轴 4:租约归 1)");
        }

        // ══════════════ S11-08 写面收敛 + 白名单落位 ══════════════

        /// <summary>
        /// 三条结束出口的清位都经 `ReleaseMotorSuppression()` 单一私有方法(**不三写**);
        /// 且 10 的**位操作**一律走 `Gameplay.Presentation` 的写面 —— 驱动里**零** `MotorLease` 成员调用。
        /// <para>⚠️ **后半是 `AC-1-23` 的机械门逼出来的**(2026-11-02 实测):
        /// 那把 IL 扫描按**调用点所在程序集**判白名单(`Gameplay.Boot` 不在其中)。
        /// 本条把「为什么不能直接调」也钉成源面判据,免得下次有人图省事又搬回驱动。</para>
        /// </summary>
        [Test]
        public void test_s11_08_writeSurface_isConfinedToAllowedAssembly()
        {
            const string driverPath = "Assets/Gameplay.Boot/EmergencyChannelDriver.cs";
            Assert.IsTrue(File.Exists(driverPath), $"源文件不存在:{driverPath}");
            string driver = File.ReadAllText(driverPath);

            Assert.AreEqual(1, CountOccurrences(driver, "private void AcquireMotorSuppression()"),
                "置位须有恰一处私有 helper");
            Assert.AreEqual(1, CountOccurrences(driver, "private void ReleaseMotorSuppression()"),
                "清位须有恰一处私有 helper");
            Assert.GreaterOrEqual(CountOccurrences(driver, "ReleaseMotorSuppression();"), 3,
                "三条结束出口(EndAction / AbortAction / SkipAction)都须调到 helper");

            // 驱动里**不得**直接碰 LeaseSource / 位操作 —— 写面在 Gameplay.Presentation。
            Assert.AreEqual(0, CountOccurrences(driver, ".Acquire(LeaseSource."),
                "驱动不得直接 `MotorLease.Acquire` —— AC-1-23 的门按程序集判,`Gameplay.Boot` 不在白名单");
            Assert.AreEqual(0, CountOccurrences(driver, ".Release(LeaseSource."),
                "驱动不得直接 `MotorLease.Release` —— 同上");

            // 写面必须真的落在白名单内的程序集。
            Assert.AreEqual("Gameplay.Presentation",
                typeof(EmergencyMotorSuppression).Assembly.GetName().Name,
                "10 的写面须住 `Gameplay.Presentation`(AC-1-23 白名单的宿主程序集注释原文)");
            Assert.AreEqual(0, CountOccurrences(
                    File.ReadAllText("Assets/Gameplay.Presentation/Player/EmergencyMotorSuppression.cs"),
                    "new MotorLease"),
                "写面不得自己造租约 —— 唯一实例归 1(S11-05)");
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }

        // ══════════════ 夹具内辅助类型 ══════════════

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class PresentPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 1;
            public bool IsPresentAt(WorldPos cell) => true;
            public IReadOnlyCollection<int> PresentPatientIds() => new[] { 1 };
        }
    }
}
