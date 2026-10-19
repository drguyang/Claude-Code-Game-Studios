// sprint-06 G4.1 · 急救手动化 —— 自动触发移除后的**零输入观察窗**判据(EditMode · 2026-10-11)
//
// 判据(可证伪 · sprint-06 §2 G4.1):
//   ① 零输入观察窗:驱动在**无玩家急救意图**下对在场病人跑若干 tick 边沿
//      ⇒ 病史流**零 EmergencyAttempt**(自动触发分支必须已删除);
//   ② 手动入口:调用 TryManual ⇒ EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流
//      (真 HostEmergencyProcessor · 病人归因 · 流路由 History);
//   ③ Skip 路径:method = 1 ⇒ 载荷 method = Skip(跳过在流上可判别,R-6);
//   ④ 越界 / 负值 fail-loud(method 闭集 / tick / actorId)。
//
// ⚠️ 红线(sprint-06 §2 G4.3 qa-lead 注):**旧 `emergency_attempt_wiring_test` 断言
//   「启动即自动两事件」= 假绿** —— 本测与其反向:自动触发已移除,零输入必须零事件。

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input;   // IEmergencyDeviceReadiness(cause 判据的 3 侧契约)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;   // FeelLatencyEvaluator(降级序数的 10 侧真源)
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="EmergencyManualDriver"/> 判据(G4.1/G4.2)。</summary>
    internal sealed class EmergencyManualDriverTest
    {
        private CompositionRootServices _bag;
        private EmergencyManualDriver _driver;
        private PatientId _patient;

        [SetUp]
        public void SetUp()
        {
            _bag = CompositionRoot.Assemble();
            _driver = _bag.EmergencyManualDriver;

            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
            _bag.VitalsService.OnTickEdge(0);
        }

        [Test]
        public void test_zeroInput_window_emitsNoEmergencyAttempt()
        {
            // 零输入观察窗:Advance 若干 tick + 显式调退役壳 OnTickEdge(2026-10-12 C3-T5 强化:
            //   原实现只 Advance 未调任何 OnTickEdge ⇒ 自动触发若被写回 EditMode 面,该测恒绿;
            //   现显式泵壳的 OnTickEdge ×20 —— 壳内不得再有任何自动触发分支)。
            for (int i = 0; i < 20; i++)
            {
                _bag.TickDriver.Advance(0.05d);
                _bag.EmergencyAttemptDriver.OnTickEdge(_bag.TickDriver.CurrentTick);
            }

            CollectionAssert.DoesNotContain(_bag.Stream.Events.Select(e => e.Kind),
                EventKind.EmergencyAttempt,
                "零输入观察窗 ⇒ 病史流零 EmergencyAttempt(自动触发已移除 —— G4.1 判据)");
            CollectionAssert.DoesNotContain(_bag.Stream.Events.Select(e => e.Kind),
                EventKind.EmergencyTreatmentApplied,
                "零输入 ⇒ 零处置事件(双事件都不发)");
        }

        [Test]
        public void test_manualEntry_writesBothEvents_intoHistory()
        {
            int before = _bag.Stream.Count;

            var result = _driver.TryManual(_patient, method: 0, actorId: 9001, tick: 5);

            Assert.IsTrue(result.Applied, "手动入口走完 Process(主机上下文)");
            Assert.AreEqual(before + 2, _bag.Stream.Count, "一次手动 ⇒ 恰两条流事件");
            Assert.AreEqual(EventKind.EmergencyAttempt, _bag.Stream.Events[before].Kind, "① EmergencyAttempt");
            Assert.AreEqual(EventKind.EmergencyTreatmentApplied, _bag.Stream.Events[before + 1].Kind,
                "② EmergencyTreatmentApplied");

            // 流路由(纯函数):急救 Kind 落病史流
            Assert.AreEqual(StreamId.History, StreamRouting.Of(EventKind.EmergencyAttempt));
            Assert.AreEqual(StreamId.History, StreamRouting.Of(EventKind.EmergencyTreatmentApplied));

            // 病人归因(批次 E):两事件头 Patient 须 = 被施救病人
            Assert.AreEqual(_patient, _bag.Stream.Events[before].Patient, "attempt 归属病人");
            Assert.AreEqual(_patient, _bag.Stream.Events[before + 1].Patient, "applied 归属病人");
        }

        [Test]
        public void test_manualSkip_methodSurvivesIntoPayload()
        {
            _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 6);

            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p), "applied 载荷解码");
            Assert.AreEqual(1, p.Method, "method = Skip(跳过在流上可判别 —— R-6 / ADR-019)");

            SimEvent attempt = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyAttempt);
            Assert.IsTrue(PayloadCodec.TryGetPayload(attempt, _bag.BlobPool,
                out EmergencyAttemptPayload ap), "attempt 载荷解码");
            Assert.AreEqual(1, ap.Method, "attempt 载荷 method = Skip");
            Assert.AreEqual(9001, ap.ActorId, "actor_id = 施予者");
        }

        [Test]
        public void test_invalidMethod_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: 2, actorId: 9001, tick: 7),
                "method 闭集 {0,1},越界 = 编程错误 fail-loud");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: -1, actorId: 9001, tick: 7),
                "method 负值 fail-loud");
        }

        [Test]
        public void test_invalidTickAndActor_throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: 0, actorId: 9001, tick: -1),
                "tick<0 fail-loud");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _driver.TryManual(_patient, method: 0, actorId: -1, tick: 7),
                "actorId 负值 fail-loud");
        }

        // ══════════ TD-epic-01 第八片 · cause 判据(GDD 规则十二 / AC-10-21)══════════

        /// <summary>夹具替身:把 3 的读数固定成给定值(真实现 = EmergencyDeviceReadiness)。</summary>
        private sealed class FixedReadiness : IEmergencyDeviceReadiness
        {
            public bool IsAnalogChannelAbsent { get; set; }
        }

        [Test]
        public void test_cause_deviceReadinessNotBound_defaultsToPlayerSelection()
        {
            // 未注入(组合根默认形态)⇒ 夹具默认「玩家选择」—— 显式记录的默认,不是判据。
            Assert.AreEqual(EmergencyManualDriver.CausePlayerSelection, _driver.ResolveCause(),
                "未注入设备读数 ⇒ cause = 玩家选择(显式夹具默认,见 EmergencyManualDriver 文件头注)");

            _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 9);
            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p));
            Assert.AreEqual(EmergencyManualDriver.CausePlayerSelection, p.Cause,
                "未注入 ⇒ 流上 cause = 0(玩家选择),不是降级");
        }

        [Test]
        public void test_cause_keyboardOnlyPath_writesDegraded()
        {
            // AC-10-21:纯键鼠(无模拟量通道)⇒ cause = 降级。
            // 判别力:写侧退回硬编码 cause:0 ⇒ 本条红(降级在流上不可见)。
            _driver.BindDeviceReadiness(new FixedReadiness { IsAnalogChannelAbsent = true });

            _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 10);

            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p));
            Assert.AreEqual(EmergencyManualDriver.CauseDegraded, p.Cause,
                "键鼠路径 ⇒ 流上 cause = 1(降级;AC-10-21 的无障碍核心读数)");
            Assert.AreEqual(FeelLatencyEvaluator.GetKeyboardFallbackCause(), p.Cause,
                "与 10 侧 GetKeyboardFallbackCause() 同值(不另立第二套序数)");
        }

        [Test]
        public void test_cause_analogChannelPresent_writesPlayerSelection()
        {
            // 有模拟量通道(手柄 / XR 在场)⇒ 玩家主动跳过 ⇒ cause = 玩家选择。
            // 判别力:若把判据写成「永远降级」或「键鼠在场即降级」,本条红。
            _driver.BindDeviceReadiness(new FixedReadiness { IsAnalogChannelAbsent = false });

            _driver.TryManual(_patient, method: 1, actorId: 9001, tick: 11);

            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p));
            Assert.AreEqual(EmergencyManualDriver.CausePlayerSelection, p.Cause,
                "有模拟量通道 ⇒ cause = 0(玩家选择;与降级强制分开)");
        }

        [Test]
        public void test_bindDeviceReadiness_isOnceOnly()
        {
            _driver.BindDeviceReadiness(new FixedReadiness());
            Assert.Throws<InvalidOperationException>(
                () => _driver.BindDeviceReadiness(new FixedReadiness()),
                "重复注入 = 两个真源 ⇒ fail-loud");
            Assert.Throws<ArgumentNullException>(
                () => new EmergencyManualDriver(
                        new HostEmergencyProcessor(_bag.Stream, _bag.IdAuthority, _bag.Encoder),
                        _bag.Presence)
                    .BindDeviceReadiness(null),
                "null 读数 fail-loud");
        }

        // ══════════ TD-epic-01 第九片 · 纯键鼠 MAG_MAX 回填(AC-10-21 幅度半边)══════════

        /// <summary>幅度门<b>必失败</b>的动作行:`MagThreshold` 高到 `MagPeak = 0` 必挂,
        /// 节奏门宽松到必过(`MinEdges = 1` · `MinHoldTicks = 0`)⇒ 结果只由幅度门决定。</summary>
        private static readonly EmergencyActionRow MagGateRow = new EmergencyActionRow
        {
            ActionId = 0,
            Polarity = 2,
            BasePotency = 65536,
            MagThreshold = 400,
            MinEdges = 1,
            MinHoldTicks = 5,   // ⚠️ 不得为 0:节奏门是 `edges >= MinEdges || hold >= MinHoldTicks`,
                                //    MinHoldTicks = 0 时 hold = 0 也恒过 ⇒ 节奏门永不失败(夹具无判别力)
            HalfLifeTicks = 100,
            DurationTicks = 200,
            ResultMul = new long[] { 0, 32768, 65536 }, // Missed/AppliedWeak/Applied
        };

        /// <summary>同一行,`ResultMul` 换成 GDD F-10.4 裁定的三档(0.25 / 0.5 / 1.0),
        /// 让 Missed 可观测(`ResultMul[Missed] = 0` 时 Missed 的 potency 也是 0,与「没执行」不可分)。</summary>
        private static readonly EmergencyActionRow MagGateRowGddMuls = new EmergencyActionRow
        {
            ActionId = 0,
            Polarity = 2,
            BasePotency = 65536,
            MagThreshold = 400,
            MinEdges = 1,
            MinHoldTicks = 5,   // ⚠️ 不得为 0:节奏门是 `edges >= MinEdges || hold >= MinHoldTicks`,
                                //    MinHoldTicks = 0 时 hold = 0 也恒过 ⇒ 节奏门永不失败(夹具无判别力)
            HalfLifeTicks = 100,
            DurationTicks = 200,
            ResultMul = new long[] { 16384, 32768, 65536 }, // F-10.4: 0.25 / 0.5 / 1.0
        };

        /// <summary>零幅度读数(键鼠上 Axis 恒 0 ⇒ 通道夹取后即此值)。</summary>
        private static EmergencyAttemptPayload ZeroMagAttempt(int edges = 1)
            => new EmergencyAttemptPayload(
                action: 0, holdTicks: 10, edges: edges,
                magPeak: 0, magLast: 0, method: 0, actorId: 9001,
                edgeTicks: Enumerable.Range(0, edges).Select(i => i).ToArray());

        private EmergencyActionResult JudgeWith(EmergencyAttemptPayload attempt, bool analogChannelAbsent,
                                                EmergencyActionRow row = null)
        {
            var processor = new HostEmergencyProcessor(_bag.Stream, _bag.IdAuthority, _bag.Encoder);
            processor.Process(attempt, row ?? MagGateRow,
                new JudgeContext { Level = 1, MagThresholdEffective = 400 },
                _patient, tick: 20, cause: analogChannelAbsent ? 1 : 0, analogChannelAbsent: analogChannelAbsent);
            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p));
            return new EmergencyActionResult(p.DrugPotency.Raw, p.ActorId);
        }

        /// <summary>结算载荷里能观测到的东西(只看 potency:Applied = 1.0 · AppliedWeak = 0.5 · Missed = 0.25)。</summary>
        private readonly struct EmergencyActionResult
        {
            public readonly long Potency;
            public readonly int ActorId;
            public EmergencyActionResult(long potency, int actorId)
            { Potency = potency; ActorId = actorId; }
        }

        [Test]
        public void test_ac1021_zeroMag_keyboardFallback_passesMagGate()
        {
            // AC-10-21:纯键鼠 ⇒ 幅度门**恒过**(magnitude 按 MAG_MAX 判)。
            // 判别力:去掉 `analogChannelAbsent` 分支 ⇒ 结果 Missed ⇒ potency 0.25 ⇒ 本条红。
            var r = JudgeWith(ZeroMagAttempt(), analogChannelAbsent: true);
            Assert.AreEqual(65536L, r.Potency,
                "键鼠回退 ⇒ 幅度门按 MAG_MAX 判过(Applied 1.0),而不是 Missed(0.25)");
        }

        [Test]
        public void test_ac1021_zeroMag_analogChannel_stillMissed()
        {
            // 反向对照:有模拟量通道时同一份零幅度读数**必挂**幅度门 ⇒ Missed(0.25)。
            // 判别力:若把豁免写成「无条件 MAG_MAX」⇒ 本条红(手柄压平也被判过)。
            var r = JudgeWith(ZeroMagAttempt(), analogChannelAbsent: false, row: MagGateRowGddMuls);
            Assert.AreEqual(16384L, r.Potency,
                "有模拟量通道 ⇒ 幅度门照评(Missed = 0.25 档,不被回填救活)");
        }

        [Test]
        public void test_ac1021_rhythmGate_stillEvaluatedUnderKeyboard()
        {
            // GDD Edge Cases「只走节奏 + 稳度双门」:回填只改幅度,**节奏门照评**
            //   —— 构造一条 `edges = 0`(节奏必挂)的读数,回填后仍须 Missed。
            // 判别力:若回填顺手把节奏门也放过(或直接返回 Applied)⇒ 本条红。
            var rhythmFail = new EmergencyAttemptPayload(
                action: 0, holdTicks: 0, edges: 0, magPeak: 0, magLast: 0,
                method: 0, actorId: 9001, edgeTicks: Array.Empty<int>());
            var r = JudgeWith(rhythmFail, analogChannelAbsent: true, row: MagGateRowGddMuls);
            Assert.AreEqual(16384L, r.Potency,
                "键鼠回退只豁免幅度门;节奏门(edges ≥ MIN_EDGES 或 hold ≥ MIN_HOLD)照评 ⇒ Missed");
        }

        [Test]
        public void test_ac1021_fallbackReading_keepsRealTimingOnlyMagIsFilled()
        {
            // 回填形状:节奏 / 稳度输入**原样透传**,只有 magnitude 被置 MAG_MAX。
            // 判别力:若回填改用无参重载(写死 100 / 3 / {10,20,30})⇒ 本条红。
            var real = FeelLatencyEvaluator.CreateKeyboardFallbackReading(
                action: 4, holdTicks: 37, edges: 2, edgeTicks: new[] { 5, 33 });
            Assert.AreEqual(4, real.Action);
            Assert.AreEqual(37, real.HoldTicks);
            Assert.AreEqual(2, real.Edges);
            CollectionAssert.AreEqual(new[] { 5, 33 }, real.EdgeTicks);
            Assert.AreEqual(FeelLatencyEvaluator.MAG_MAX, real.Magnitude);
        }

        [Test]
        public void test_ac1021_payloadKeepsRealMagPeak_notFilled()
        {
            // 回填**不写回载荷**:`EmergencyAttempt` 里仍是通道真采样(0),不是 MAG_MAX。
            // 判别力:若把回填挪到写侧(改 attempt.MagPeak)⇒ 本条红。
            JudgeWith(ZeroMagAttempt(), analogChannelAbsent: true);
            SimEvent attemptEvent = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyAttempt);
            Assert.IsTrue(PayloadCodec.TryGetPayload(attemptEvent, _bag.BlobPool,
                out EmergencyAttemptPayload recorded));
            Assert.AreEqual(0, recorded.MagPeak,
                "载荷记玩家真给了什么(0);MAG_MAX 只是求值侧的回填,不进流(F-10.4 判定输入/输出分离)");
        }

        [Test]
        public void test_ac1021_productionChain_tryAggregated_fillsOnKeyboard()
        {
            // 端到端(生产链路):`TryAggregated`(直读通道交出物的唯一入口)在键鼠设备读数下
            //   ⇒ 零幅度也过幅度门。判别力:若驱动没把 `IsAnalogChannelAbsent` 传下去
            //   (只传 cause,或两者混用)⇒ 本条红。
            _driver.BindDeviceReadiness(new FixedReadiness { IsAnalogChannelAbsent = true });
            var agg = new DaYiJingCheng.Gameplay.Input.Intents.AggregatedEmergency(
                action: 0, holdTicks: 10, edges: 1, edgeTicks: new[] { 0 },
                magPeak: 0, magLast: 0);

            var result = _driver.TryAggregated(agg, actorId: 9001, _patient, tick: 30);

            Assert.IsTrue(result.Applied);
            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p));
            Assert.AreEqual(65536L, p.DrugPotency.Raw,
                "生产链路(通道交出物)在键鼠下也走幅度门豁免 ⇒ Applied 1.0");
            Assert.AreEqual(EmergencyManualDriver.CauseDegraded, p.Cause,
                "同一判据同时驱动 cause(两者在注入后恒同源)");
        }

        [Test]
        public void test_ac1021_legacyOverload_doesNotInferFromCause()
        {
            // 6 参兼容入口**不承载幅度门判据** —— 即便 `cause = 降级` 也照常按 MagPeak 判。
            // 判别力(证伪轮 M-m-8 首轮 GREEN 后补):若兼容入口从 `cause` 反推判据
            //   ⇒ 本条红。理由:causality 是「结算输出」,判据是「判定输入」的输入 ——
            //   用前者反推后者 = 第二真源,且 `cause` 在设备读数未注入时只是默认值。
            var processor = new HostEmergencyProcessor(_bag.Stream, _bag.IdAuthority, _bag.Encoder);
            processor.Process(ZeroMagAttempt(), MagGateRowGddMuls,
                new JudgeContext { Level = 1, MagThresholdEffective = 400 },
                _patient, tick: 40, cause: EmergencyManualDriver.CauseDegraded);   // 6 参重载

            SimEvent applied = _bag.Stream.Events.Last(e => e.Kind == EventKind.EmergencyTreatmentApplied);
            Assert.IsTrue(PayloadCodec.TryGetPayload(applied, _bag.BlobPool,
                out EmergencyTreatmentAppliedPayload p));
            Assert.AreEqual(16384L, p.DrugPotency.Raw,
                "6 参兼容入口不带判据 ⇒ 零幅度仍 Missed(0.25);不得从 cause 反推出豁免");
        }

        [Test]
        public void test_ac1021_unboundDeviceReadiness_doesNotFill()
        {
            // 未注入设备读数 ⇒ `cause` 落到夹具默认(玩家选择),但幅度门**不得**跟着豁免 ——
            //   那是 `IsAnalogChannelAbsent` 而非 `ResolveCause()`。
            Assert.IsFalse(_driver.IsAnalogChannelAbsent,
                "未注入 ⇒ 判据回答「不知道就不回填」(宁严勿猜),不是「有通道」");
            Assert.AreEqual(EmergencyManualDriver.CausePlayerSelection, _driver.ResolveCause());
        }

        [Test]
        public void test_outOfPresence_rejectsWithoutEvents()
        {
            // C3-M1 负测:不在场病人 ⇒ 零事件 + Applied=false(与处方侧 1c 对称;
            //   SetUp 的在场病人之外,造一个已发号但未 AddPresent 的病人 id)。
            int before = _bag.Stream.Count;
            var ghost = new PatientId(9999);

            var result = _driver.TryManual(ghost, method: 0, actorId: 9001, tick: 8);

            Assert.IsFalse(result.Applied, "不在场 ⇒ 回执 false(不写流)");
            Assert.AreEqual(before, _bag.Stream.Count,
                "不在场急救 ⇒ 病史流零新事件(EmergencyAttempt/EmergencyTreatmentApplied 都不发)");
        }
    }
}
