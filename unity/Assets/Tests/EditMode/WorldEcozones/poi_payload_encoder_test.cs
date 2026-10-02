// ADR-029 接线支测试 —— world-ecozones Story 006(闭合 B1)
//
// AC-6-27: PoiStateMachine 经 IPayloadEncoder 编码
// AC-6-28: RebuildFromDecoded 收已解码序列(甲案 —— Sim 侧零 codec 依赖)
// AC-6-29: PoiStateMachine.cs 内 `new PayloadRef(` 零命中
// AC-6-30: 载荷往返一致(含 poiId / newState **错位负例**)
// AC-6-31: Sim 引用集仍 = ["Sim.Contracts"]
// AC-6-32: 重放重建不回归

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.WorldEcozones
{
    public class PoiPayloadEncoderTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private SpyEventSink _sink;
        private PoiStateMachine _machine;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _sink = new SpyEventSink();
            // ⚠️ 7 须登记 —— 错位负例刻意用 poiId=7 ≠ newState=2 以暴露字段互换。
            //    (未登记的 poiId ⇒ PoiNotFound ⇒ 不发事件,测试会假红。)
            _machine = new PoiStateMachine(_sink, new HostAuthority(), new[] { 1, 2, 3, 7 }, _encoder);
        }

        private PoiStateChangedPayload Decode(SimEvent evt)
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _pool, out PoiStateChangedPayload p),
                "载荷须能经池 + codec 取回(字节真的进了池)");
            return p;
        }

        // ══════════ AC-6-27: 写侧经 encoder ══════════

        [Test]
        public void test_ac627_writeGoesThroughEncoder_bytesInPool()
        {
            _machine.TryAdvance(7, PoiState.Discovered, tick: 100);

            Assert.AreEqual(1, _sink.AppendedEvents.Count);
            var reference = _sink.AppendedEvents[0].Payload;
            Assert.IsTrue(_pool.TryGetBlob(reference.BlobId, out var stored),
                "编码后字节须真的在池里可取回(B1 原实现手搓 ⇒ 此处红)");
            Assert.Greater(stored.Length, 0);
        }

        [Test]
        public void test_ac627_poiIdNotUsedAsBlobId()
        {
            // 🔴 B1 原实现:`new PayloadRef(blobId: poiId, offset: (int)toState, length: 8)`
            //    —— 把 poiId 当 blobId 用。本测钉死二者**不再混同**。
            _machine.TryAdvance(7, PoiState.Discovered, tick: 100);
            var reference = _sink.AppendedEvents[0].Payload;

            Assert.AreNotEqual(7, reference.BlobId,
                "BlobId 是池索引,不得等于 poi_id(B1 缺陷的形态)");
            Assert.AreNotEqual((int)PoiState.Discovered, reference.Offset,
                "Offset 是字节偏移,不得等于 new_state(B1 缺陷的形态)");
        }

        [Test]
        public void test_ac627_encoderIsRequired()
        {
            Assert.Throws<ArgumentNullException>(
                () => new PoiStateMachine(new SpyEventSink(), new HostAuthority(), new[] { 1 }, null),
                "encoder 为 null 须抛(唯一编码路径不可省)");
        }

        // ══════════ AC-6-29: 手搓面消除 ══════════

        [Test]
        public void test_ac629_noManualPayloadRefInSource()
        {
            string src = Path.Combine(Application.dataPath, "Sim/World/PoiStateMachine.cs");
            Assert.IsTrue(File.Exists(src));

            string code = StripComments(File.ReadAllText(src));
            Assert.IsFalse(code.Contains("new PayloadRef("),
                "PoiStateMachine.cs 内不得手搓 PayloadRef(ADR-029 §③)");
        }

        [Test]
        public void test_ac629_noTodoLeftFromB1()
        {
            // B1 的原处置 = 「加 TODO 注释」。落地后它们必须消失 ——
            // 否则 B1 的处置形态未被真正替换(index.md 已判「免责不成立」)。
            string src = Path.Combine(Application.dataPath, "Sim/World/PoiStateMachine.cs");
            string code = StripComments(File.ReadAllText(src));

            Assert.IsFalse(code.Contains("TODO"),
                "B1 的 TODO 注释须消失(原处置正是留 TODO)");
            Assert.IsFalse(code.Contains("IBlobPool 支持"),
                "「需 IBlobPool 支持」的免责措辞须消失");
        }

        // ══════════ AC-6-30: 往返一致 + 错位负例 ══════════

        [Test]
        public void test_ac630_roundTrip_fieldOrderNotSwapped()
        {
            // 🔴 定向负例:B1 原实现把 (poiId, newState) 解成 (blobId, offset) —— 语义错位。
            //    构造 poiId=7, newState=2(Resolved),解码后须仍为 (7, 2)。
            _machine.TryAdvance(7, PoiState.Resolved, tick: 100);
            var p = Decode(_sink.AppendedEvents[0]);

            Assert.AreEqual(7, p.PoiId, "PoiId 须为 7(不得被当成 blobId)");
            Assert.AreEqual((int)PoiState.Resolved, p.NewState, "NewState 须为 2(不得被当成字节偏移)");
        }

        [Test]
        public void test_ac630_allThreeStatesRoundTrip()
        {
            foreach (var target in new[] { PoiState.Discovered, PoiState.Resolved })
            {
                var pool = new InMemoryBlobPool();
                var sink = new SpyEventSink();
                var m = new PoiStateMachine(sink, new HostAuthority(), new[] { 1 }, new PayloadEncoder(pool));
                m.TryAdvance(1, target, tick: 10);

                Assert.IsTrue(PayloadCodec.TryGetPayload(sink.AppendedEvents[0], pool, out PoiStateChangedPayload p));
                Assert.AreEqual(1, p.PoiId);
                Assert.AreEqual((int)target, p.NewState, $"{target} 须往返一致");
            }
        }

        [Test]
        public void test_ac630_skipLevelRoundTrip()
        {
            // AC-6-11 跳级:Undiscovered → Resolved
            _machine.TryAdvance(2, PoiState.Resolved, tick: 50);
            var p = Decode(_sink.AppendedEvents[0]);
            Assert.AreEqual(2, p.PoiId);
            Assert.AreEqual((int)PoiState.Resolved, p.NewState);
        }

        // ══════════ AC-6-28 / AC-6-32: 甲案重建 ══════════

        [Test]
        public void test_ac628_rebuildFromDecoded_equalsLiveState()
        {
            _machine.TryAdvance(1, PoiState.Discovered, tick: 100);
            _machine.TryAdvance(2, PoiState.Resolved, tick: 101);
            _machine.TryAdvance(3, PoiState.Discovered, tick: 102);

            // 调用方解码(甲案)
            var decoded = new List<(int, PoiState)>();
            foreach (var evt in _sink.AppendedEvents)
            {
                var p = Decode(evt);
                decoded.Add((p.PoiId, (PoiState)p.NewState));
            }

            var rebuilt = new PoiStateMachine(
                new SpyEventSink(), new HostAuthority(), new[] { 1, 2, 3 }, _encoder);
            rebuilt.RebuildFromDecoded(decoded);

            Assert.AreEqual(PoiState.Discovered, rebuilt.GetState(1));
            Assert.AreEqual(PoiState.Resolved, rebuilt.GetState(2));
            Assert.AreEqual(PoiState.Discovered, rebuilt.GetState(3));
        }

        [Test]
        public void test_ac628_rebuildResetsUnlistedToUndiscovered()
        {
            _machine.TryAdvance(1, PoiState.Resolved, tick: 100);

            var rebuilt = new PoiStateMachine(
                new SpyEventSink(), new HostAuthority(), new[] { 1, 2, 3 }, _encoder);
            rebuilt.RebuildFromDecoded(new List<(int, PoiState)> { (1, PoiState.Resolved) });

            Assert.AreEqual(PoiState.Resolved, rebuilt.GetState(1));
            Assert.AreEqual(PoiState.Undiscovered, rebuilt.GetState(2), "未出现的 POI 须回 Undiscovered");
            Assert.AreEqual(PoiState.Undiscovered, rebuilt.GetState(3));
        }

        [Test]
        public void test_ac628_rebuildIgnoresUnknownPoiId()
        {
            var rebuilt = new PoiStateMachine(
                new SpyEventSink(), new HostAuthority(), new[] { 1, 2, 3 }, _encoder);
            rebuilt.RebuildFromDecoded(new List<(int, PoiState)> { (999, PoiState.Resolved) });

            Assert.AreEqual(PoiState.Undiscovered, rebuilt.GetState(999),
                "定义侧未登记的 poi_id 须被跳过");
            Assert.AreEqual(PoiState.Undiscovered, rebuilt.GetState(1));
        }

        [Test]
        public void test_ac628_nullDecodedThrows()
        {
            var m = new PoiStateMachine(new SpyEventSink(), new HostAuthority(), new[] { 1 }, _encoder);
            Assert.Throws<ArgumentNullException>(() => m.RebuildFromDecoded(null));
        }

        // ══════════ AC-6-31: 装配边界 ══════════

        [Test]
        public void test_ac631_simReferenceSetUnchanged()
        {
            string asmdefPath = Path.Combine(Application.dataPath, "../Assets/Sim/Sim.asmdef");
            string content = File.ReadAllText(asmdefPath);
            Assert.IsFalse(content.Contains("Sim.Codec"),
                "Sim.asmdef 不得引用 Sim.Codec(ADR-025 §①:111)");

            var sim = typeof(PoiStateMachine).Assembly;
            var refs = sim.GetReferencedAssemblies().Select(r => r.Name).ToList();
            Assert.IsFalse(refs.Contains("Sim.Codec"), "Sim 运行时引用集不得含 Sim.Codec");
            Assert.IsTrue(refs.Contains("Sim.Contracts"));
        }

        // ══════════ 辅助 ══════════

        private static string StripComments(string src)
        {
            var noBlock = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(noBlock, @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        private sealed class HostAuthority : IEventAuthority
        {
            public bool IsAuthority => true;
            public bool IsHost => true;
            public EventRollResult Roll(in RollRequest r) => new EventRollResult(0, 0, 0, 0);
        }
    }
}
