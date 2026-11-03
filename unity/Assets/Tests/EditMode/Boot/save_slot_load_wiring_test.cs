// 7b 存档位 → 7a 载入的**可达性**判据(2026-10-31 · TD-persist-04 裁定② 的收尾)。
//
// 权威:
//   design/ux/save-slots-7b.md §交互(「载入 = 无 SimEvent · `ISaveService.Load(SaveSlot)`」)
//   design/gdd/save-slot-ui.md 规则三 / 规则四
//   ADR-013 §9 C3(42 只渲染)· ADR-025 §①(装配方向不可倒)
//
// ── 本件钉的是什么(以及**不**钉什么)────────────────────────────────
// 钉:① 42 的确认动作**真的有去向**(`ISaveSlotCommandSink` → `SaveSlotCommandRouter`
//       → `LoadSequencer` → `SaveService.Load`),一次完整的读档把事件流恢复回来;
//     ② 未接线 ⇒ 返回 false 且不抛(不崩、不造动作);
//     ③ 载入失败 ⇒ 返回 false **且观察者收到异常**(不是静默吞);
//     ④ 行号 → 槽位序号的映射正确(错位 = 按 3 号读 1 号,最坏的静默错读)。
//
// **不**钉:行的**输入事件**本身(`NavigationSubmitEvent` / `ClickEvent` 的派发)。
//   那需要真 panel 的输入分发,EditMode 无输入栈 ⇒ 如实 `NOT-RUN`。本件驱动的是两条
//   输入路径**共同的汇合点** `SaveSlotsScreen.Activate`(事件回调只是它的薄适配)。
//
// 写盘边界:全部落 `Path.GetTempPath()`。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>存档位载入接线:42 确认 → 路由 → 序列器 → 读档。</summary>
    [TestFixture]
    public sealed class SaveSlotLoadWiringTest
    {
        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private AnchorCheckpointTrigger _trigger;
        private CheckpointFallback _fallback;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_slotwire_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            _trigger = new AnchorCheckpointTrigger(
                () => true, FuncNarrativeContextQuery.AlwaysIn,
                new[] { EventKind.DiseaseOnset }, onFired: () => { });
            _fallback = new CheckpointFallback(() => { }, FuncNarrativeContextQuery.AlwaysIn);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // ══════════════ 夹具 ══════════════

        private FoldedPredicate PredicateOf() => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.Case),
            diseaseLookup: _ => null,
            decoder: _decoder);

        /// <summary>往指定槽位写一份**可读**的存档(事件 = 一条 DiseaseOnset)。</summary>
        private void WriteSlot(uint slotSeq, long tick)
        {
            var ev = new SimEvent(tick, PatientId.None, -1, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset, new DiseaseOnsetPayload(tick, 3, 5, 7L, 0L)));
            var events = new List<SimEvent> { ev };
            byte[] unsigned = SaveComposer.Compose(
                events, PredicateOf(), _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: 9U, tick: tick,
                timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), blobPool: _pool);
            File.WriteAllBytes(Path.Combine(_testDir, $"save_{slotSeq}.bin"),
                               SaveService.AddChecksum(unsigned));
        }

        private LoadSequencer NewSequencer()
        {
            IReadOnlyList<SimEvent> pending = null;
            var svc = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => _stream.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                compose: _ => throw new InvalidOperationException("本夹具不写盘"),
                writeScheduler: action => action(), blobRestoreSink: _pool);
            return new LoadSequencer(svc, _trigger, _fallback);
        }

        // ══════════════ ① 可达性:确认动作真的有去向 ══════════════

        [Test]
        public void test_saveSlotSink_reachesLoad_andRestoresStream()
        {
            WriteSlot(3, 42L);
            var router = new SaveSlotCommandRouter(NewSequencer());

            bool ok = router.TryLoad(3);

            Assert.That(ok, Is.True, "已接线 + 槽位存在 ⇒ 必须返回 true(界面据此给反馈)");
            Assert.That(_stream.Count, Is.EqualTo(1),
                "读档必须真的把事件流恢复回来 —— 只返回 true 而不读 = 「通电待信」没打通");
            Assert.That(_stream.Events[0].Tick, Is.EqualTo(42L), "恢复的必须是那一份档的内容");
        }

        [Test]
        public void test_saveSlotSink_rowIndexMapsToSlotSeq_notOffsetByOne()
        {
            WriteSlot(3, 11L);
            WriteSlot(4, 33L);

            var sink = new CapturingSink();
            SaveSlotsScreen screen = NewScreen(sink);
            screen.SetSlots(new uint[] { 3u, 4u });

            screen.Activate(1);          // 第二行 = 槽 4

            Assert.That(sink.Calls.Count, Is.EqualTo(1));
            Assert.That(sink.Calls[0], Is.EqualTo(4u),
                "行号 → 槽位序号必须逐行对应;错位 = 按 3 号读 1 号,最坏的静默错读");
        }

        // ══════════════ ② 未接线:不崩、不造动作 ══════════════

        [Test]
        public void test_unwiredRouter_returnsFalse_withoutThrowing()
        {
            var router = new SaveSlotCommandRouter(sequencer: null);
            Assert.That(router.IsWired, Is.False);
            Assert.DoesNotThrow(() => { bool ok = router.TryLoad(1); Assert.That(ok, Is.False); },
                "未接线是合法的部分装配形态(无存档目录),不是错误态 —— 但绝不能说「读成功」");
        }

        [Test]
        public void test_screen_withoutSink_rendersRowsAndDoesNotThrow()
        {
            SaveSlotsScreen screen = NewScreen(sink: null);
            screen.SetSlots(new uint[] { 1u, 2u });

            Assert.That(screen.SlotRowCount, Is.EqualTo(2), "行照常画(缺写通道不影响呈现)");
            Assert.DoesNotThrow(() => screen.Activate(0), "缺写通道 ⇒ 按了留空,不崩");
        }

        // ══════════════ ③ 失败:返回 false **且**留记录 ══════════════

        [Test]
        public void test_failedLoad_returnsFalse_andNotifiesObserver()
        {
            var failures = new List<(uint slot, Exception ex)>();
            var router = new SaveSlotCommandRouter(NewSequencer(), (s, e) => failures.Add((s, e)));

            bool ok = router.TryLoad(2);       // 槽 2 从未写过

            Assert.That(ok, Is.False, "读不到 ⇒ 不得报成功");
            Assert.That(failures.Count, Is.EqualTo(1), "失败必须留记录(静默吞才是禁止形态)");
            Assert.That(failures[0].slot, Is.EqualTo(2u), "记录须点名是哪一槽");
            Assert.That(failures[0].ex, Is.Not.Null);
        }

        [Test]
        public void test_screen_activateOutOfRange_isIgnored()
        {
            var sink = new CapturingSink();
            SaveSlotsScreen screen = NewScreen(sink);
            screen.SetSlots(new uint[] { 1u });

            Assert.DoesNotThrow(() => screen.Activate(5), "越界来自输入竞态,不是编程错误 ⇒ 忽略即可");
            Assert.That(sink.Calls, Is.Empty, "越界不得落到任何槽位");
        }

        // ══════════════ 工具 ══════════════

        private static SaveSlotsScreen NewScreen(ISaveSlotCommandSink sink)
        {
            var root = new VisualElement();
            var screen = new SaveSlotsScreen(root, new SkeuoElementLibrary());
            if (sink != null) screen.BindCommandSink(sink);
            return screen;
        }

        private sealed class CapturingSink : ISaveSlotCommandSink
        {
            public readonly List<uint> Calls = new List<uint>();
            /// <summary>落笔被请求的次数(2026-11-01 契约加 `TrySave()` 后补)。</summary>
            public int SaveCalls;
            public bool TryLoad(uint slotSeq) { Calls.Add(slotSeq); return true; }
            public bool TrySave() { SaveCalls++; return true; }
        }
    }
}
