// 技法解锁载体 —— 判据夹具(2026-11-09 · T3 · ADR-043)
//
// 权威:docs/architecture/adr-043-technique-unlock-kind.md §7 的 V-1/V-2/V-4/V-5/V-6/V-7
//
// 本文件要证明:
//   ① **路由正确** —— 新 Kind 落病史流(A1 的唯一流别,kindgen 已断言;此处留运行期锚);
//   ② **载荷往返** —— 三字段经 Encode → 解码逐值相等(写方 / 读方未分叉);
//   ③ **折叠正确** —— 空流 ⇒ false;写过 ⇒ true;异 actor / 异技法 ⇒ false;
//   ④ **重复授予 = no-op** —— 流长度不变(D6 的唯一性,有界性的基础);
//   ⑤ **闭集门** —— 不在处置轴里的技法 id ⇒ fail-loud(不写静默死行);
//   ⑥ **起步集** —— 空流写一次,非空流 no-op(D9 的幂等);
//   ⑦ **不参与折叠** —— 本件不触碰 7a,该条由持久化侧夹具守(V-7,见证据件)。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate`。

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Gameplay.Boot;

namespace DaYiJingCheng.Tests.SkillSystem
{
    [TestFixture]
    public sealed class TechniqueUnlockTest
    {
        private sealed class RecordingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private sealed class Harness
        {
            public readonly InMemoryBlobPool Pool = new InMemoryBlobPool();
            public readonly PayloadEncoder Encoder;
            public readonly PayloadDecoder Decoder;
            public readonly RecordingSink Sink = new RecordingSink();
            public readonly TechniqueUnlockProjection Query;
            public readonly TechniqueUnlockWriter Writer;

            public Harness(params int[] knownTechniques)
            {
                Encoder = new PayloadEncoder(Pool);
                Decoder = new PayloadDecoder(Pool);
                Query = new TechniqueUnlockProjection(() => Sink.Events, Decoder);
                Writer = new TechniqueUnlockWriter(Sink, Encoder, Query,
                    new HashSet<int>(knownTechniques));
            }
        }

        // ── ① 路由 ────────────────────────────────────────────────────────
        [Test]
        public void test_routing_techniqueUnlocked_goesToHistoryStream()
        {
            Assert.That(StreamRouting.Of(EventKind.TechniqueUnlocked), Is.EqualTo(StreamId.History),
                "ADR-043 §D1:解锁是玩家级成长事实,与 SkillGrown 同流(不是世界流)");
        }

        // ── ② 载荷往返 ────────────────────────────────────────────────────
        [Test]
        public void test_payload_roundTripsThroughPool()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            var payload = TechniqueUnlockEmitter.EmitUnlock(7, 1, 42);
            PayloadRef r = encoder.Encode(EventKind.TechniqueUnlocked, payload);
            TechniqueUnlockedPayload back =
                decoder.Decode<TechniqueUnlockedPayload>(EventKind.TechniqueUnlocked, r);

            Assert.That(back.ActorId, Is.EqualTo(7));
            Assert.That(back.TechniqueId, Is.EqualTo(1));
            Assert.That(back.SourceId, Is.EqualTo(42));
        }

        [Test]
        public void test_emitter_rejectsNegativeIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockEmitter.EmitUnlock(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockEmitter.EmitUnlock(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockEmitter.EmitUnlock(0, 0, -1));
        }

        // ── ③ 折叠 ────────────────────────────────────────────────────────
        [Test]
        public void test_projection_emptyStream_isFalse()
        {
            var h = new Harness(0, 1);
            Assert.That(h.Query.IsUnlocked(7, 0), Is.False, "空流 ⇒ 未解锁");
        }

        [Test]
        public void test_projection_afterUnlock_isTrueOnlyForThatPair()
        {
            var h = new Harness(0, 1);
            Assert.That(h.Writer.TryUnlock(7, 1, 42, 0), Is.True);

            Assert.That(h.Query.IsUnlocked(7, 1), Is.True, "写过的 (actor, technique) 须为 true");
            Assert.That(h.Query.IsUnlocked(7, 0), Is.False, "同 actor 的**另一招**不受影响");
            Assert.That(h.Query.IsUnlocked(8, 1), Is.False, "**另一个 actor** 不受影响(解锁跟人走)");
        }

        // ── ④ 重复授予 = no-op ───────────────────────────────────────────
        /// <summary>
        /// <para><b>判别力</b>:删掉写者里的 `if (_query.IsUnlocked(...)) return false;` ⇒ 流长度变 2,
        /// 本条红。这条是有界性(D10)与「重复内容不报错」两条的唯一交点。</para>
        /// </summary>
        [Test]
        public void test_writer_duplicateGrant_isNoOp()
        {
            var h = new Harness(0, 1);
            Assert.That(h.Writer.TryUnlock(7, 1, 42, 0), Is.True);
            Assert.That(h.Writer.TryUnlock(7, 1, 43, 10), Is.False, "重复授予须返回 false");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1), "重复授予**不得**写第二条(D6)");
        }

        // ── ⑤ 闭集门 ──────────────────────────────────────────────────────
        [Test]
        public void test_writer_unknownTechnique_failsLoud()
        {
            var h = new Harness(0, 1);
            var ex = Assert.Throws<ArgumentException>(() => h.Writer.TryUnlock(7, 99, 1, 0));
            Assert.That(ex.Message, Does.Contain("闭集"), "报文须点名「不在处置轴闭集内」");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(0), "拒收时不得留下半条事件");
        }

        // ── ⑥ 起步集 ──────────────────────────────────────────────────────
        [Test]
        public void test_bootstrap_emptyStream_writesStartingSetOnce()
        {
            var h = new Harness(0, 1);
            int n = TechniqueUnlockBootstrap.Ensure(h.Sink.Events, h.Writer, 7, 0);

            Assert.That(n, Is.EqualTo(TechniqueUnlockBootstrap.StartingTechniques.Length));
            Assert.That(h.Sink.Events.Count, Is.EqualTo(n));
            foreach (int id in TechniqueUnlockBootstrap.StartingTechniques)
                Assert.That(h.Query.IsUnlocked(7, id), Is.True, $"起步集须含技法 {id}");
        }

        [Test]
        public void test_bootstrap_nonEmptyStream_isNoOp()
        {
            var h = new Harness(0, 1);
            Assert.That(h.Writer.TryUnlock(7, 1, 42, 0), Is.True);   // 流已非空 = 读档形态

            int n = TechniqueUnlockBootstrap.Ensure(h.Sink.Events, h.Writer, 7, 5);
            Assert.That(n, Is.EqualTo(0), "非空流 ⇒ 不是新世界 ⇒ 不重发");
            Assert.That(h.Sink.Events.Count, Is.EqualTo(1), "流长度不变");
        }

        // ── 事件头形状 ────────────────────────────────────────────────────
        [Test]
        public void test_writtenEvent_headerIsWorldScoped()
        {
            var h = new Harness(0, 1);
            h.Writer.TryUnlock(7, 1, 42, 123);

            SimEvent e = h.Sink.Events[0];
            Assert.That(e.Patient.Value, Is.EqualTo(PatientId.None.Value),
                "解锁是玩家级事实 ⇒ Patient 恒 None(与 ADR-007 五支同格)");
            Assert.That(e.Tick, Is.EqualTo(123), "tick 取调用方给的当前 tick");
        }
    }
}
