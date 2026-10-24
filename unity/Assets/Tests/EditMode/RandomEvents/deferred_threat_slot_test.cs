// S19-4 / TD-randomevents-01 —— DeferredThreatSlot 流态载体 + 两 Kind 读写 + 假断言撤换。
//
// 卡面四判据(sprint-19 §S19-4):
//   ① `DeferSlots` 由 `List<int>` 改为**从流重构**(不保留第二真源)
//   ② `EventBudget.ValidateReconstructable` 的「验证非空」假断言删除并换真判据
//   ③ `WriterExistenceGate` 两条豁免(ThreatDeferred / ThreatDeferralCleared)同批撤除
//   ④ 夹具:主机迁移后延迟威胁不丢不重(重放同一段流两次,`DeferSlots` 逐元素相同)
//
// 权威:ADR-007 §三(承重条款)/ §四(Patient = None)/ entities.yaml 两 Kind constraint /
//   GDD random-events.md 规则六之三 / ADR-029 §③ + ADR-031 §②(编码解码抽象点)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.RandomEvents
{
    public class DeferredThreatSlotTest
    {
        private const int DeferMax = 5;

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private RecordingSink _sink;
        private DeferredThreatSlotWriter _writer;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _sink = new RecordingSink();
            _writer = new DeferredThreatSlotWriter(_sink, _encoder);
        }

        private sealed class RecordingSink : IEventSink
        {
            public int AppendCount;
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) { AppendCount++; Events.Add(e); }
        }

        /// <summary>夹具流:入槽 slot0(key7)→ 入槽 slot1(key9)→ 补发清 slot0。
        /// 折叠后期望槽集 = {1:9}。</summary>
        private List<SimEvent> BuildStream()
        {
            var w = new DeferredThreatSlotWriter(_sink, _encoder);
            Assert.AreEqual(DeferredThreatWriteResult.Applied, w.TryDefer(10, 0, 7, DeferMax));
            Assert.AreEqual(DeferredThreatWriteResult.Applied, w.TryDefer(11, 1, 9, DeferMax));
            Assert.AreEqual(DeferredThreatWriteResult.Applied,
                w.TryClear(12, 0, DeferredThreatSlotWriter.ReasonReissued, DeferMax));
            return _sink.Events;
        }

        private static void AssertSlotsEqual(Dictionary<int, int> actual, params int[] slotKeyPairs)
        {
            Assert.AreEqual(slotKeyPairs.Length / 2, actual.Count, "槽位数");
            for (int i = 0; i < slotKeyPairs.Length; i += 2)
            {
                Assert.IsTrue(actual.TryGetValue(slotKeyPairs[i], out int key),
                    $"slot {slotKeyPairs[i]} 应挂起");
                Assert.AreEqual(slotKeyPairs[i + 1], key, $"slot {slotKeyPairs[i]} 的 eventKey");
            }
        }

        private SimEvent RawDeferEvent(long tick, int slot, int key)
        {
            // 绕过写者域校验直接编码(折叠侧防御的流损坏形态须这么构造 —— 这正是它的职责面)
            var p = _encoder.Encode(EventKind.ThreatDeferred, new ThreatDeferredPayload(slot, key));
            return new SimEvent(tick, PatientId.None, -1, EventKind.ThreatDeferred, p);
        }

        private SimEvent RawClearEvent(long tick, int slot, int reason)
        {
            var p = _encoder.Encode(EventKind.ThreatDeferralCleared, new ThreatDeferralClearedPayload(slot, reason));
            return new SimEvent(tick, PatientId.None, -1, EventKind.ThreatDeferralCleared, p);
        }

        // ══════════ 判据 ①:从流重构(读路径)══════════

        [Test]
        public void test_deferSlots_foldDeferOccupiesSlot()
        {
            var state = EventBudget.ReconstructDeferSlots(
                new List<SimEvent> { RawDeferEvent(10, 0, 7) }, _decoder, DeferMax);
            AssertSlotsEqual(state.DeferSlots, 0, 7);
        }

        [Test]
        public void test_deferSlots_foldClearReleasesSlot()
        {
            var state = EventBudget.ReconstructDeferSlots(BuildStream(), _decoder, DeferMax);
            AssertSlotsEqual(state.DeferSlots, 1, 9);   // slot0 已补发清,slot1 仍挂起
        }

        [Test]
        public void test_deferSlots_emptyStream_yieldsEmptySlots()
        {
            var state = EventBudget.ReconstructDeferSlots(new List<SimEvent>(), _decoder, DeferMax);
            Assert.AreEqual(0, state.DeferSlots.Count, "空流 ⇒ 空槽(无垃圾初值)");
        }

        [Test]
        public void test_deferSlots_bothClearReasons_releaseSlot()
        {
            // 折叠对 reason 不分支(补发/作废都清槽)—— reason 是 51 回放读数,不影响槽集形状
            var stream = new List<SimEvent>
            {
                RawDeferEvent(10, 0, 7),
                RawDeferEvent(11, 1, 9),
                RawClearEvent(12, 0, DeferredThreatSlotWriter.ReasonReissued),   // 补发
                RawClearEvent(13, 1, DeferredThreatSlotWriter.ReasonVoided),     // 作废
            };
            var state = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);
            Assert.AreEqual(0, state.DeferSlots.Count, "两 reason 均应清槽");
        }

        [Test]
        public void test_deferSlots_unrelatedKinds_skipped()
        {
            var rolled = _encoder.Encode(EventKind.EventRolled, new EventRolledPayload(1, 2, 3L, 4));
            var unrelated = new SimEvent(10, PatientId.None, -1, EventKind.EventRolled, rolled);
            var stream = new List<SimEvent>
            {
                unrelated, RawDeferEvent(11, 0, 7), unrelated,
            };
            var state = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);
            AssertSlotsEqual(state.DeferSlots, 0, 7);   // 无关 Kind 被跳过(不解码、不干扰)
        }

        [Test]
        public void test_deferSlots_replayTwice_identicalSlotElements()
        {
            // 判据 ④:主机迁移后延迟威胁不丢不重 —— 同一段流重放两次,槽集逐元素相同
            var stream = BuildStream();
            var a = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);
            var b = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);

            Assert.AreEqual(a.DeferSlots.Count, b.DeferSlots.Count, "重放次数不影响槽位数");
            foreach (var kv in a.DeferSlots)
            {
                Assert.IsTrue(b.DeferSlots.TryGetValue(kv.Key, out int key),
                    $"重放后 slot {kv.Key} 不丢");
                Assert.AreEqual(kv.Value, key, $"重放后 slot {kv.Key} 的 eventKey 不变");
            }
        }

        // ══════════ 折叠防御:三类流损坏 fail-loud ══════════

        [Test]
        public void test_deferSlots_slotOutOfRange_throws()
        {
            var state = new BudgetState();
            Assert.Throws<InvalidDataException>(
                () => EventBudget.FoldDeferEvent(state, RawDeferEvent(10, DeferMax, 42), _decoder, DeferMax),
                "slot == deferMax 越界 ⇒ 拒收");
            Assert.Throws<InvalidDataException>(
                () => EventBudget.FoldDeferEvent(state, RawDeferEvent(10, -1, 42), _decoder, DeferMax),
                "负 slot 越界 ⇒ 拒收");
        }

        [Test]
        public void test_deferSlots_doubleOccupy_throws()
        {
            // 一占一释配对(entities.yaml constraint):同槽二次挂起 = 流损坏
            var stream = new List<SimEvent> { RawDeferEvent(10, 0, 7), RawDeferEvent(11, 0, 8) };
            Assert.Throws<InvalidDataException>(
                () => EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax));
        }

        [Test]
        public void test_deferSlots_clearUnoccupied_throws()
        {
            var stream = new List<SimEvent> { RawClearEvent(10, 0, DeferredThreatSlotWriter.ReasonVoided) };
            Assert.Throws<InvalidDataException>(
                () => EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax));
        }

        // ══════════ 判据 ①(机械面):直改面已拆 ══════════

        [Test]
        public void test_budget_deferSlots_noDirectMutationPath()
        {
            var methods = typeof(BudgetState).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name).ToHashSet();
            Assert.IsFalse(methods.Contains("AddToDefer"), "AddToDefer 直改面已删(第二真源)");
            Assert.IsFalse(methods.Contains("ClearDefer"), "ClearDefer 直改面已删(第二真源)");

            var field = typeof(BudgetState).GetField("DeferSlots");
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(Dictionary<int, int>), field.FieldType,
                "槽集 = slotIndex→eventKey 字典(payload 的 slot_index 显式寻址);List<int> 形态已退役");
        }

        // ══════════ 判据 ②:真判据替换假断言 ══════════

        [Test]
        public void test_validateReconstructable_trueWhenFoldMatches()
        {
            var stream = BuildStream();
            var state = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);
            Assert.IsTrue(EventBudget.ValidateReconstructable(state, stream, _decoder, DeferMax));
        }

        [Test]
        public void test_validateReconstructable_falseWhenDrift()
        {
            var stream = BuildStream();
            var drifted = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);

            drifted.DeferSlots[1] = 999;   // 同槽不同 key ⇒ 漂移
            Assert.IsFalse(EventBudget.ValidateReconstructable(drifted, stream, _decoder, DeferMax),
                "槽值漂移必须被判出(原假断言「验证非空」判不出任何漂移)");

            var phantom = EventBudget.ReconstructDeferSlots(stream, _decoder, DeferMax);
            phantom.DeferSlots[4] = 1;     // 幻影槽 ⇒ 漂移
            Assert.IsFalse(EventBudget.ValidateReconstructable(phantom, stream, _decoder, DeferMax));
        }

        // ══════════ 写者:写路径 + 事件头契约 ══════════

        [Test]
        public void test_writer_defer_appendsHistoryEventWithNonePatient()
        {
            var r = _writer.TryDefer(10, 0, 7, DeferMax);
            Assert.AreEqual(DeferredThreatWriteResult.Applied, r);
            Assert.AreEqual(1, _sink.AppendCount);

            var e = _sink.Events[0];
            Assert.AreEqual(EventKind.ThreatDeferred, e.Kind);
            Assert.AreEqual(PatientId.None, e.Patient, "世界级事件 Patient = None(ADR-007 §四,必须值)");
            Assert.AreEqual(-1, e.Seq, "Seq = -1 未发号哨兵(EventStream.Append 发号)");
            Assert.AreEqual(StreamId.History, StreamRouting.Of(e.Kind), "落病史流(entities.yaml constraint)");

            var back = _decoder.Decode<ThreatDeferredPayload>(e.Kind, in e.Payload);
            Assert.AreEqual(0, back.SlotIndex);
            Assert.AreEqual(7, back.EventKey);
        }

        [Test]
        public void test_writer_clear_appendsWithReason()
        {
            var r = _writer.TryClear(20, 3, DeferredThreatSlotWriter.ReasonVoided, DeferMax);
            Assert.AreEqual(DeferredThreatWriteResult.Applied, r);
            Assert.AreEqual(1, _sink.AppendCount);

            var e = _sink.Events[0];
            Assert.AreEqual(EventKind.ThreatDeferralCleared, e.Kind);
            Assert.AreEqual(PatientId.None, e.Patient);
            Assert.AreEqual(StreamId.History, StreamRouting.Of(e.Kind));

            var back = _decoder.Decode<ThreatDeferralClearedPayload>(e.Kind, in e.Payload);
            Assert.AreEqual(3, back.SlotIndex);
            Assert.AreEqual(DeferredThreatSlotWriter.ReasonVoided, back.Reason);
        }

        [Test]
        public void test_writer_reasonConstants_reissuedZeroVoidedOne()
        {
            // reason 序数 = 落地约定(本件首定;registry 未写序数)—— 机械断言钉住常量,
            // 若将来 registry 补序数,此处与 registry 同批改(CareApplied/CarePhase 三处权威先例)
            Assert.AreEqual(0, DeferredThreatSlotWriter.ReasonReissued, "补发 = 0");
            Assert.AreEqual(1, DeferredThreatSlotWriter.ReasonVoided, "作废 = 1");
        }

        // ══════════ 写者:零写入分支(具名结果码)══════════

        [Test]
        public void test_writer_defer_slotOutOfRange_noWrite()
        {
            Assert.AreEqual(DeferredThreatWriteResult.SlotOutOfRange, _writer.TryDefer(10, -1, 7, DeferMax));
            Assert.AreEqual(DeferredThreatWriteResult.SlotOutOfRange,
                _writer.TryDefer(10, DeferMax, 7, DeferMax), "slot == deferMax 越界");
            Assert.AreEqual(0, _sink.AppendCount, "拒收 ⇒ 零写入,不静默");
        }

        [Test]
        public void test_writer_clear_slotOutOfRange_noWrite()
        {
            Assert.AreEqual(DeferredThreatWriteResult.SlotOutOfRange,
                _writer.TryClear(10, DeferMax, DeferredThreatSlotWriter.ReasonVoided, DeferMax));
            Assert.AreEqual(0, _sink.AppendCount);
        }

        [Test]
        public void test_writer_defer_negativeEventKey_noWrite()
        {
            Assert.AreEqual(DeferredThreatWriteResult.InvalidEventKey, _writer.TryDefer(10, 0, -1, DeferMax));
            Assert.AreEqual(0, _sink.AppendCount);
        }

        [Test]
        public void test_writer_clear_invalidReason_noWrite()
        {
            Assert.AreEqual(DeferredThreatWriteResult.InvalidReason, _writer.TryClear(10, 0, 2, DeferMax));
            Assert.AreEqual(DeferredThreatWriteResult.InvalidReason, _writer.TryClear(10, 0, -1, DeferMax));
            Assert.AreEqual(0, _sink.AppendCount);
        }

        // ══════════ 端到端闭环:写者 → 流 → 折叠 ══════════

        [Test]
        public void test_endToEnd_writeThenFold_reconstructMatchesState()
        {
            var state = new BudgetState();
            var stream = BuildStream();
            foreach (var e in stream)
                EventBudget.FoldDeferEvent(state, e, _decoder, DeferMax);

            AssertSlotsEqual(state.DeferSlots, 1, 9);
            Assert.IsTrue(EventBudget.ValidateReconstructable(state, stream, _decoder, DeferMax),
                "生产写者产出的流,其折叠结果必须通过重构验证");
        }

        // ══════════ 判据 ③:门豁免同批撤除 ══════════

        [Test]
        public void test_gate_threatKinds_notInExemptTable()
        {
            var f = typeof(DaYiJingCheng.EditorTools.Gates.WriterExistenceGate).GetField("ExemptKinds",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "豁免表字段在盘(门结构)");
            var exempt = (IEnumerable<(string Kind, string Reason)>)f.GetValue(null);
            var names = exempt.Select(x => x.Kind).ToHashSet();
            Assert.IsFalse(names.Contains("ThreatDeferred"),
                "写者已落(Sim/DeferredThreatSlotWriter.cs),豁免必须同批撤(判据①豁免=红)");
            Assert.IsFalse(names.Contains("ThreatDeferralCleared"), "同上");
        }
    }
}
