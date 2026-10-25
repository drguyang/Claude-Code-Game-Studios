// 读档缝合序列器 —— LoadSequencer 的行为钉子(TD-persist-04 义务② 接线轮,2026-10-25 三裁全取推荐)。
//
// 钉的安全属性:
//   ① **义务② 判据**:Load 成功 ⇒ 锚点游标归零(行为观测:已推进的游标在 Load 后
//      再 Drain 同一批流,**再次触发** = 游标确被 ResetCursor;不 Reset = 游标停在
//      流尾,永不重触发 —— AnchorCheckpointTrigger.cs:31 头注义务的运行时面);
//   ② **裁定③**:Load 成功 ⇒ 兜底计数清零(读档点 = 「刚做过 checkpoint」);
//   ③ **原子序**:Load 抛(槽位不存在 / Gating 拒收)⇒ 缝合侧**零改动** ——
//      游标不动(Drain 不重触发)、兜底计数不清零。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。
// 双 SaveService 先例:写过的槽即锁(SaveSlotLedger 读档即锁语义)⇒ 读侧须独立实例
// (save_replay_roundtrip_test 同款)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    public class LoadSequencerTest
    {
        private const long Seed = 0x5EEDL;

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private int _anchorFired;
        private AnchorCheckpointTrigger _trigger;
        private CheckpointFallback _fallback;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_loadseq_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            _anchorFired = 0;
            // 夹具自建触发器:checkpoint 闭包恒 true(隔离写盘变量);锚点集 = 单 DiseaseOnset
            // (取它而非 JudgmentRecorded:后者载荷需 freehand 具名重载,而 Drain 只按 Kind
            // 匹配 —— 白名单成员的 Kind 身份与 GDD 生产语义解耦,夹具用任意 Kind 均可)。
            _trigger = new AnchorCheckpointTrigger(
                () => true,
                FuncNarrativeContextQuery.AlwaysIn,
                new[] { EventKind.DiseaseOnset },
                onFired: () => _anchorFired++);
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

        /// <summary>白名单锚点事件(Drain 只按 Kind 匹配,载荷不参与判定)。</summary>
        private SimEvent AnchorEvent(long tick)
            => new SimEvent(tick, PatientId.None, -1, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, 3, 5, Seed, 0L)));

        /// <summary>折叠谓词:无折叠行(本夹具的存档无被折病人,隔离折叠变量)。</summary>
        private FoldedPredicate PredicateOf() => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.Case),
            diseaseLookup: _ => null,
            decoder: _decoder);

        private SaveService NewLoadSideService()
        {
            IReadOnlyList<SimEvent> pending = null;
            return new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => _stream.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                compose: _ => SaveComposer.Compose(
                    _stream.Events, PredicateOf(), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    existingFoldedRows: _stream.FoldedRows),
                // 夹具与生产的唯一差别:同步内联(断言确定;生产 = Task.Run)。
                writeScheduler: action => action());
        }

        /// <summary>写侧:同一流的组版 + Checkpoint(槽 1)。</summary>
        private void WriteSlotOne()
        {
            var svc = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => SaveComposer.Compose(
                    _stream.Events, PredicateOf(), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: 20L,
                    existingFoldedRows: _stream.FoldedRows),
                writeScheduler: action => action());
            svc.Checkpoint(new SaveSlot(1));
        }

        /// <summary>①+② 正路径:Load 成功 ⇒ 游标归零(再 Drain 重触发)+ 兜底清零 + 流已恢复。</summary>
        [Test]
        public void test_loadSequencer_load_resetsCursorAndFallback()
        {
            _stream.Append(AnchorEvent(5));
            WriteSlotOne();
            var loadSide = NewLoadSideService();

            // 预推进游标:Drain 命中锚点 ⇒ 触发(游标 0 → 流尾 1)。
            _trigger.Drain(_stream.Events);
            Assert.AreEqual(1, _anchorFired, "预推进:游标消费锚点,首 Drain 触发一次");
            _fallback.OnTicks(500);
            Assert.AreEqual(500, _fallback.TicksSinceLastFire, "预置非零兜底计数");

            // 清流(模拟读档前的世界差异 —— Restore 换内容,不是 Append 变长):
            // 读档后恢复闭包把流内容替换回存档时刻。
            var sequencer = new LoadSequencer(loadSide, _trigger, _fallback);
            sequencer.Load(new SaveSlot(1));

            Assert.AreEqual(1, _stream.Count, "恢复闭包:重放把存档时刻的事件带回流内");
            Assert.AreEqual(0, _fallback.TicksSinceLastFire,
                "裁定③:Load 成功 ⇒ 兜底计数清零(读档点 = 刚做过 checkpoint)");

            // 义务② 判据的行为观测:Load 后再 Drain **同一批流** ⇒ 锚点**再次触发**
            // (游标若未被 ResetCursor,停流尾 = 永不重触发)。
            _trigger.Drain(_stream.Events);
            Assert.AreEqual(2, _anchorFired,
                "义务②:ResetCursor 生效 —— 已消费的锚点在读档后重新可触发(游标归零的行为面)");
        }

        /// <summary>③ 原子序:Load 抛(槽不存在)⇒ 游标不动、兜底计数不清零。</summary>
        [Test]
        public void test_loadSequencer_loadFails_seamsUntouched()
        {
            _stream.Append(AnchorEvent(5));
            WriteSlotOne();
            var loadSide = NewLoadSideService();

            _trigger.Drain(_stream.Events);   // 游标推进至流尾(onFired=1)
            _fallback.OnTicks(500);

            var sequencer = new LoadSequencer(loadSide, _trigger, _fallback);
            Assert.Throws<SaveCorruptException>(
                () => sequencer.Load(new SaveSlot(9)),
                "不存在的槽位 ⇒ Load 抛(Verifying 两级皆缺 = Corrupt 态)");

            Assert.AreEqual(500, _fallback.TicksSinceLastFire,
                "原子序:Load 抛 ⇒ 兜底计数不清零");
            _trigger.Drain(_stream.Events);
            Assert.AreEqual(1, _anchorFired,
                "原子序:Load 抛 ⇒ 游标未被 ResetCursor(再 Drain 同批不重触发)");
            Assert.AreEqual(1, _stream.Count, "Load 抛 ⇒ 流内容未被恢复闭包改动");
        }

        /// <summary>fallback 可 null(部分装配合法态):Load 正常,游标缝合照常。</summary>
        [Test]
        public void test_loadSequencer_nullFallback_loadStillSeamsCursor()
        {
            _stream.Append(AnchorEvent(5));
            WriteSlotOne();
            var loadSide = NewLoadSideService();

            _trigger.Drain(_stream.Events);
            var sequencer = new LoadSequencer(loadSide, _trigger);   // fallback 缺省 null
            Assert.DoesNotThrow(() => sequencer.Load(new SaveSlot(1)));

            _trigger.Drain(_stream.Events);
            Assert.AreEqual(2, _anchorFired, "null fallback ⇒ 只跳过清零步,游标缝合照常");
        }

        /// <summary>构造守卫:saveService / anchorTrigger 为 null ⇒ 具名 ArgumentNullException。</summary>
        [Test]
        public void test_loadSequencer_ctor_nullGuards()
        {
            Assert.Throws<ArgumentNullException>(
                () => new LoadSequencer(null, _trigger, _fallback));
            Assert.Throws<ArgumentNullException>(
                () => new LoadSequencer(NewLoadSideService(), null, _fallback));
        }
    }
}
