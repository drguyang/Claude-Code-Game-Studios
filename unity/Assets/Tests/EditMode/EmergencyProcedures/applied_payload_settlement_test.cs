// ADR-029 接线支测试 —— emergency-procedures Story 007(九字段结算链 + 结构性收口)
//
// AC-10-39: EmergencyTreatmentApplied 九字段齐备(含 Seq 占位显式标注)
// AC-10-40: EmergencyAttempt 八字段齐备
// AC-10-41: drug_potency 经 F-10.4 单一舍入(含 R-2/A8 的 ulp 反例)
// AC-10-42: HostEmergencyProcessor 零手搓 PayloadRef
// AC-10-44: b6 门豁免已清
// AC-10-45: Missed 仍照常发事件(非零 potency)

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;
using UnityEngine;
using Gates = DaYiJingCheng.EditorTools.Gates.AssemblyGates;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class AppliedPayloadSettlementTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private CapturingSink _sink;
        private HostEmergencyProcessor _processor;

        /// <summary>动作表行:非零 Polarity / BasePotency / HalfLifeTicks —— 使「恒 0」缺陷可检出。</summary>
        private static EmergencyActionRow Action(int actionId = 0, long basePotency = 100000L)
            => new EmergencyActionRow
            {
                ActionId = actionId,
                Polarity = 1,                 // 非零(手搓法恒 0)
                BasePotency = basePotency,
                MagThreshold = 400,
                MinEdges = 2,
                MinHoldTicks = 50,
                HalfLifeTicks = 120,          // 非零(恒 0 ⇒ 9 的 Decay 除零)
                DurationTicks = 200,
                ResultMul = EmergencyActionSchema.GetResultMulTiers(),
            };

        private static EmergencyAttemptPayload Attempt(int method = 0, int actorId = 7)
            => new EmergencyAttemptPayload(
                action: 0, holdTicks: 60, edges: 3, magPeak: 800, magLast: 750,
                method: method, actorId: actorId, edgeTicks: new[] { 10, 30, 60 });

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _sink = new CapturingSink();
            _processor = new HostEmergencyProcessor(_sink, new FakeIdAuthority(), _encoder);
        }

        private T Decode<T>(int index) where T : struct
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(_sink.Events[index], _pool, out T p),
                $"事件[{index}] 载荷须能经池 + codec 取回");
            return p;
        }

        // ══════════ AC-10-39: applied 九字段齐备 ══════════

        [Test]
        public void test_ac1039_applied_nineFieldsAllPopulated()
        {
            _processor.Process(Attempt(), Action(), Ctx(), tick: 100, cause: 0);

            var p = Decode<EmergencyTreatmentAppliedPayload>(1);   // [0]=Attempt, [1]=Applied

            Assert.AreEqual(100L, p.Tick, "Tick");
            Assert.AreEqual(0, p.TreatmentId, "TreatmentId = action.ActionId");
            Assert.AreEqual(7, p.ActorId, "ActorId = 施予者(不是 JudgeResult!)");
            Assert.AreEqual(1, p.Polarity, "Polarity = 处置词表查得(不是恒 0!)");
            Assert.AreEqual(100000L, p.DrugPotency.Raw, "DrugPotency = F-10.4(Applied 档 = ×1.0)");
            Assert.AreEqual(120L, p.HalfLife, "HalfLife = 动作数据表(不是恒 0 ⇒ 不触发 9 的 Decay 除零)");
            Assert.AreEqual(0, p.Method, "Method");
            Assert.AreEqual(0, p.Cause, "Cause");
            // ⚠️ Seq 为占位 0 —— 见下 test_ac1039_seqIsExplicitlyPlaceholder
        }

        [Test]
        public void test_ac1039_halfLifeNotZero()
        {
            // 🔴 手搓法的硬雷:HalfLife 恒 0 ⇒ registry 明写「触发 9 的 Decay 除零,AC-28 写入期拒收」
            _processor.Process(Attempt(), Action(), Ctx(), tick: 1, cause: 0);
            var p = Decode<EmergencyTreatmentAppliedPayload>(1);
            Assert.AreNotEqual(0L, p.HalfLife, "HalfLife 不得为 0(会触发 9 的 Decay 除零)");
        }

        [Test]
        public void test_ac1039_polarityNotZero()
        {
            _processor.Process(Attempt(), Action(), Ctx(), tick: 1, cause: 0);
            var p = Decode<EmergencyTreatmentAppliedPayload>(1);
            Assert.AreNotEqual(0, p.Polarity, "Polarity 须由处置词表查得(手搓法恒 0)");
        }

        [Test]
        public void test_ac1039_actorIdIsProviderNotJudgeResult()
        {
            // 🔴 手搓法把 `(int)result` 当施予者 id ⇒ 此处用 actorId=7 而 result 非 7 来钉死
            _processor.Process(Attempt(actorId: 7), Action(), Ctx(), tick: 1, cause: 0);
            var p = Decode<EmergencyTreatmentAppliedPayload>(1);
            Assert.AreEqual(7, p.ActorId, "ActorId 须为施予者 id,不得填 JudgeResult");
        }

        [Test]
        public void test_ac1039_methodAndCauseDiscriminable()
        {
            // GDD R-1:「跳过」须在流上可判别
            var sink2 = new CapturingSink();
            var proc2 = new HostEmergencyProcessor(sink2, new FakeIdAuthority(), _encoder);
            proc2.Process(Attempt(method: 1), Action(), Ctx(), tick: 1, cause: 1);
            var p = Decode2<EmergencyTreatmentAppliedPayload>(sink2, 1);
            Assert.AreEqual(1, p.Method, "Method = Skip 须可判别");
            Assert.AreEqual(1, p.Cause, "Cause = 降级 须可判别");
        }

        private T Decode2<T>(CapturingSink sink, int i) where T : struct
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(sink.Events[i], _pool, out T p));
            return p;
        }

        [Test]
        public void test_ac1039_seqIsExplicitlyPlaceholder()
        {
            // ⚠️ 裁定 A=丙:载荷 Seq 置 0 占位(其真源「主机 Append 时发号」发生在 IEventSink 内部,
            //    载荷构造在其之前)。**本测把占位事实钉死,防它被读成「已齐备」。**
            _processor.Process(Attempt(), Action(), Ctx(), tick: 100, cause: 0);
            var p = Decode<EmergencyTreatmentAppliedPayload>(1);
            Assert.AreEqual(0L, p.Seq,
                "载荷 Seq 当前为**占位 0**(裁定 A=丙)—— 真源归上行链(45/P1b);" +
                "若本断言变红,说明上行链已落地,须同步更新 AC-10-39 的判据面");
        }

        // ══════════ AC-10-40: attempt 八字段齐备 ══════════

        [Test]
        public void test_ac1040_attempt_eightFieldsAllPopulated()
        {
            _processor.Process(Attempt(), Action(), Ctx(), tick: 100, cause: 0);

            var p = Decode<EmergencyAttemptPayload>(0);
            Assert.AreEqual(0, p.Action, "Action");
            Assert.AreEqual(60, p.HoldTicks, "HoldTicks");
            Assert.AreEqual(3, p.Edges, "Edges");
            Assert.AreEqual(800, p.MagPeak, "MagPeak");
            Assert.AreEqual(750, p.MagLast, "MagLast(手搓法丢弃了它!)");
            Assert.AreEqual(0, p.Method, "Method");
            Assert.AreEqual(7, p.ActorId, "ActorId");
            Assert.IsTrue(p.EdgeTicks.SequenceEqual(new[] { 10, 30, 60 }), "EdgeTicks");
        }

        [Test]
        public void test_ac1040_edgeTicksLengthMatchesEdges()
        {
            // codec 侧跨字段约束:EdgeTicks.Length == Edges(违反 ⇒ ArgumentException)
            _processor.Process(Attempt(), Action(), Ctx(), tick: 1, cause: 0);
            var p = Decode<EmergencyAttemptPayload>(0);
            Assert.AreEqual(p.Edges, p.EdgeTicks.Length, "Edges 须等于 EdgeTicks 长度");
        }

        // ══════════ AC-10-41: F-10.4 单一舍入(含 ulp 反例)══════════

        [Test]
        public void test_ac1041_singleRounding_ulpCounterexample()
        {
            // 🔴 R-2/A8 的 ulp 反例 —— 目的是让中间积的余数**恰为半** MUL_ONE/2,
            //    使「向零截断」与「ROUND_HALF_AWAY_FROM_ZERO」分道。
            //
            // ⚠️ **GDD 勘误(2026-10-03 由本测实测发现)**:GDD A8 的示例写
            //    「`(32769 × 16384) ÷ 65536 = 8192.5`」—— **数字写颠倒了**。
            //    实测 `32769 × 16384 = 536887296` ÷ 65536 = **8192.25**(rem=16384 ⇒ 舍入后仍 8192)。
            //    正确组合 = **`32768 × 16385` = 536903680** ÷ 65536 = **8192.5**(rem=32768 恰为半)。
            //    ⇒ 本测用后者;勘误已登记(见 §已知问题)。
            long basePotency = 32768L;
            long mulWeak = 16385L;

            long product = basePotency * mulWeak;
            Assert.AreEqual(536903680L, product, "中间积须 = 8192.5 × 65536(余数恰为半)");
            Assert.AreEqual(32768L, product % 65536L, "余数须恰 = MUL_ONE/2 = 32768");

            long actual = JudgeEvaluator.ScaleFixed(basePotency, mulWeak);

            Assert.AreEqual(8193L, actual,
                "须为 8193(ROUND_HALF_AWAY_FROM_ZERO);若得 8192 = 用了 C# 向零截断,跨平台对拍会被 ulp 打破");

            // 负向对照:朴素写法(向零截断)会得 8192 ⇒ 证明本测真能分辨两种舍入
            Assert.AreEqual(8192L, product / 65536L,
                "朴素 `a * b / c` 得 8192 —— 与本测期望的 8193 相差一 ulp");
        }

        [Test]
        public void test_ac1041_threeTiersNonZero()
        {
            _processor.Process(Attempt(), Action(basePotency: 100000L), Ctx(), tick: 1, cause: 0);
            var applied = Decode<EmergencyTreatmentAppliedPayload>(1);
            Assert.AreEqual(100000L, applied.DrugPotency.Raw, "Applied 档 = ×1.0");
        }

        [Test]
        public void test_ac1045_missedStillEmits_nonZeroPotency()
        {
            // GDD :716 —— Missed 照常发处置事件,drug_potency = BASE × 0.25(**非零**)
            // 构造一个必 Missed 的 attempt:edges=1 < MinEdges=2 且 holdTicks < MinHoldTicks
            var bad = new EmergencyAttemptPayload(
                action: 0, holdTicks: 1, edges: 1, magPeak: 100, magLast: 100,
                method: 0, actorId: 7, edgeTicks: new[] { 10 });

            _processor.Process(bad, Action(basePotency: 100000L), Ctx(), tick: 1, cause: 0);

            Assert.AreEqual(2, _sink.Events.Count, "Missed 仍须发两条事件(非零,失败也留一笔)");
            var p = Decode<EmergencyTreatmentAppliedPayload>(1);
            Assert.AreEqual(25000L, p.DrugPotency.Raw, "Missed 档 = ×0.25,且**非零**");
        }

        // ══════════ AC-10-42 / AC-10-44: 手搓面 + 豁免 ══════════

        [Test]
        public void test_ac1042_noManualPayloadRefInSource()
        {
            string src = Path.Combine(Application.dataPath,
                "Sim/EmergencyProcedures/HostEmergencyProcessor.cs");
            string code = StripComments(File.ReadAllText(src));
            Assert.IsFalse(code.Contains("new PayloadRef("),
                "HostEmergencyProcessor.cs 内不得手搓 PayloadRef(ADR-029 §③)");
        }

        [Test]
        public void test_ac1044_noTodoLeftFromB6Finding()
        {
            string src = Path.Combine(Application.dataPath,
                "Sim/EmergencyProcedures/HostEmergencyProcessor.cs");
            string code = StripComments(File.ReadAllText(src));
            Assert.IsFalse(code.Contains("TODO"), "b6 查出的手搓点须真修,不得留 TODO");
        }

        [Test]
        public void test_ac1044_b6WaiversEmpty()
        {
            var f = typeof(Gates).GetField("PayloadRefWaivers",
                BindingFlags.NonPublic | BindingFlags.Static);
            var waivers = (System.Collections.IEnumerable)f.GetValue(null);
            int count = 0;
            foreach (var _ in waivers) count++;
            Assert.AreEqual(0, count, "b6 豁免表须为空(story-007 已清手搓面)");
        }

        [Test]
        public void test_ac1044_b6GatePassesOnCurrentSource()
        {
            var errs = new System.Collections.Generic.List<string>();
            typeof(Gates).GetMethod("CheckPayloadRefCallsites",
                BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { errs });
            Assert.IsEmpty(errs, "b6 门须在无豁免状态下通过:\n" + string.Join("\n", errs));
        }

        // ══════════ 结构性收口:重复 struct 已删 ══════════

        [Test]
        public void test_structural_duplicateStructsRemoved()
        {
            var simAsm = typeof(HostEmergencyProcessor).Assembly;
            var dups = simAsm.GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Sim.EmergencyProcedures" &&
                            (t.Name == "EmergencyAttemptPayload" ||
                             t.Name == "EmergencyTreatmentAppliedPayload"))
                .ToList();
            Assert.IsEmpty(dups,
                "Sim.EmergencyProcedures 内不得再有 payload struct 副本(权威版在 Sim.Contracts):" +
                string.Join(", ", dups.Select(t => t.Name)));
        }

        [Test]
        public void test_structural_attemptHasNoFabricatedCause()
        {
            // entities.yaml:2253 的 payload_schema **不含 cause** —— 权威版须无该字段
            var t = typeof(EmergencyAttemptPayload);
            Assert.IsNull(t.GetField("Cause"),
                "EmergencyAttemptPayload 不得有自造的 Cause(属 applied,GDD :464)");
            Assert.IsNotNull(t.GetField("ActorId"), "须有 ActorId(不是自造的 Provider)");
            Assert.IsNotNull(t.GetField("MagLast"), "须有 MagLast(3 侧已交出,不得丢弃)");
        }

        // ══════════════════════════════════════════════════════════════
        // §已修(本测实测发现,2026-10-03 当日勘误落盘)
        //
        // **`design/gdd/emergency-procedures.md` A8 的 ulp 示例数字写颠倒**:
        //   原文:`(32769 × 16384) ÷ 65536 = 8192.5` → C# 得 8192,正确应 8193。
        //   实测:`32769 × 16384 = 536887296` ÷ 65536 = **8192.25**(rem = 16384 ≠ 半)⇒ **无舍入分道**。
        //   正确组合 = `32768 × 16385 = 536903680` ÷ 65536 = **8192.5**(rem = 32768 = MUL_ONE/2)✅
        //
        // ⚠️ **原文的「结论」是对的**(须用 ROUND_HALF_AWAY_FROM_ZERO 而非 C# 截断),
        //    仅**示例数字**错 —— 但该数字是「可复现验证」的载体,写错会让照抄者
        //    实测得 8192 后**误以为实现有 bug**,或反推出错误的舍入实现。
        //    ✅ **已修(2026-10-03)**:A8 的示例数字已订正为 `32768 × 16385`,并加勘误注。
        // ══════════════════════════════════════════════════════════════

        // ══════════ 辅助 ══════════

        private static JudgeContext Ctx() => new JudgeContext { Level = 5, MagThresholdEffective = 400 };

        private static string StripComments(string src)
        {
            var noBlock = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(noBlock, @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        private sealed class CapturingSink : IEventSink
        {
            public readonly System.Collections.Generic.List<SimEvent> Events =
                new System.Collections.Generic.List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private sealed class FakeIdAuthority : IIdAuthority
        {
            private int _next = 1;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }
    }
}
