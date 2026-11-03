// AC-7a-14 末句 · ADR-010 §七 —— 「`ConfigVersion` 不匹配 ⇒ 已发生事件不受影响、
// **仅记录并供排查**」的**执行体**钉子(2026-10-31 补 AC 执行体轮)。
//
// 权威:
//   design/gdd/persistence-service.md:581-586(AC-7a-14 原文)
//   ADR-010 §七(「存档头记 `ConfigVersion` 以便排查回放不符」;**不匹配非致命**;
//     并明写「若要改成致命 = 推翻本 § 与 ADR-007 Guidelines 5,须另开 ADR」)
//   ADR-014 §五(当前 `ConfigVersion` = 源文本集内容哈希派生)
//
// ── 本件钉的是哪一半(以及**不**钉哪一半)────────���───────────────────────
// 钉:① 读档成功后**必然**发生一次比对;② 比对的两个输入分别是**档头里的值**与
//     **当前产物的值**(不是同一个值的两份拷贝);③ 不匹配**不改变任何行为**
//     (已发生事件逐字段不变);④ 读档失败 ⇒ 一条记录都不留;⑤ 记录器未装配 ⇒ 不抛。
// **不**钉:③′ 「**哪一张表**变了」—— 档头只有一个组合后的 u32,从它推不出逐表差异。
//     要做逐表差异须让存档携带逐份版本(布局升级 `SaveVersion` v3,候选方案 D),
//     属独立裁决。⚠️ 因此本件**不得**被引作「差异集 = 被改的那张表」的证据。
//
// ⚠️ **生产可达性**(禁借绿):本 AC 的执行体挂在 `LoadSequencer.Load` 上,而生产触发点
//    `BootRoot.RequestLoad` **今日零调用方**(裁定② 通电待信)。⇒ 本件证明的是
//    「**读档路径一旦被调用即记录**」,**不是**「读档时已经记录过」。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>AC-7a-14:读档时的 `ConfigVersion` 比对与记录(非致命)。</summary>
    [TestFixture]
    public sealed class ConfigVersionDriftOnLoadTest
    {
        private const long Seed = 0x5EEDL;

        /// <summary>写进存档头的版本(「旧配置」)。</summary>
        private const uint SavedConfigVersion = 0x11112222U;

        /// <summary>当前产物的版本(「新配置」)—— 刻意取一个与上者不同的值。</summary>
        private const uint CurrentConfigVersion = 0x33334444U;

        /// <summary>DiseaseOnset 载荷里的病种 id(读回后逐字段核对用)。</summary>
        private const int DiseaseId = 3;

        /// <summary>DiseaseOnset 载荷里的病人 id。</summary>
        private const int PatientIdValue = 5;

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
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_cfgdrift_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            // 与 `load_sequencer_test` 同款:触发器 / 兜底与本件判据无关,只为满足构造。
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

        // ══════════════════════════════════════════════════════════════════
        // 夹具装配
        // ══════════════════════════════════════════════════════════════════

        private SimEvent OnsetEvent(long tick)
            => new SimEvent(tick, PatientId.None, -1, EventKind.DiseaseOnset,
                _encoder.Encode(EventKind.DiseaseOnset,
                    new DiseaseOnsetPayload(tick, DiseaseId, PatientIdValue, Seed, 0L)));

        private FoldedPredicate PredicateOf() => new FoldedPredicate(
            historyStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.History),
            caseStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.Case),
            diseaseLookup: _ => null,
            decoder: _decoder);

        /// <summary>写侧:把**指定档头版本**的存档写进槽 1。</summary>
        private void WriteSlotOne(uint headerConfigVersion)
        {
            var svc = new SaveService(
                _testDir, new SaveCodec(),
                folded: PredicateOf(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => SaveComposer.Compose(
                    _stream.Events, PredicateOf(), _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: headerConfigVersion, tick: 20L,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: _stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
            svc.Checkpoint(new SaveSlot(1));
        }

        /// <summary>读侧:独立实例(写过的槽即锁 ⇒ 读侧不可与写侧共用台账)。</summary>
        /// <param name="recorder">记录面;null = 不装配记录能力(成对缺席)。</param>
        /// <param name="currentConfigVersion">当前产物版本提供者;null = 同上。</param>
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
                    worldSeed: 1UL, configVersion: SavedConfigVersion, tick: 20L,
                    timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L),
                    existingFoldedRows: _stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(), blobRestoreSink: _pool);
        }

        private LoadSequencer NewSequencer(List<ConfigVersionCheck> captured,
                                           uint currentConfigVersion = CurrentConfigVersion,
                                           List<ConfigVersionDiff> diffs = null)
            => new LoadSequencer(
                NewLoadSideService(), _trigger, _fallback,
                // ADR-037:快照形式(组合值 + 逐份)。本夹具不给逐份 ⇒ 差异只能是「未知」。
                currentConfigVersion: () => new ConfigVersionSnapshot(currentConfigVersion, null),
                configVersionRecorder: (c, d) => { captured.Add(c); diffs?.Add(d); });

        // ══════════════════════════════════════════════════════════════════
        // ① 正控:不匹配 ⇒ 恰好一次记录,两个版本号各自如实
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_mismatchedConfigVersion_recordsBothVersions()
        {
            _stream.Append(OnsetEvent(5));
            WriteSlotOne(SavedConfigVersion);

            var captured = new List<ConfigVersionCheck>();
            NewSequencer(captured).Load(new SaveSlot(1));

            Assert.That(captured.Count, Is.EqualTo(1),
                "成功读档必须**恰好**记录一次 —— 0 次 = AC 末句无执行体;多次 = 记录面被重复驱动");

            ConfigVersionCheck check = captured[0];
            Assert.That(check.SaveHeaderConfigVersion, Is.EqualTo(SavedConfigVersion),
                "记录里的『存档侧』必须取自档头,不是当前值");
            Assert.That(check.CurrentConfigVersion, Is.EqualTo(CurrentConfigVersion),
                "记录里的『当前侧』必须取自注入的当前产物版本");
            Assert.That(check.Match, Is.False, "两值不同 ⇒ Match 必须为 false");
            Assert.That(check.Banner(), Is.Not.Empty, "不匹配必须产出标注(ADR-010 §七「供排查」)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 非致命:不匹配 ⇒ 已发生事件逐字段不变(ADR-010 §七 前半句)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_mismatchedConfigVersion_isNonFatal_eventsUnaffected()
        {
            _stream.Append(OnsetEvent(5));
            WriteSlotOne(SavedConfigVersion);

            var captured = new List<ConfigVersionCheck>();
            Assert.DoesNotThrow(
                () => NewSequencer(captured).Load(new SaveSlot(1)),
                "不匹配**非致命**(ADR-010 §七)—— 读档不得因版本不同而抛");

            Assert.That(captured.Count, Is.EqualTo(1), "前提:本次确实走了「不匹配」分支");

            Assert.That(_stream.Count, Is.EqualTo(1), "已发生事件条数不得因版本不匹配而变");
            SimEvent e = _stream.Events[0];
            Assert.That(e.Kind, Is.EqualTo(EventKind.DiseaseOnset), "事件类型被改写");
            Assert.That(e.Tick, Is.EqualTo(5L), "事件 tick 被改写");

            var p = _decoder.Decode<DiseaseOnsetPayload>(e.Kind, e.Payload);
            Assert.That(p.DiseaseId, Is.EqualTo(DiseaseId), "载荷 disease_id 被改写");
            Assert.That(p.PatientId, Is.EqualTo(PatientIdValue), "载荷 patient_id 被改写");
            Assert.That(p.PatientSeed, Is.EqualTo(Seed), "载荷 patient_seed 被改写(逐位 — 不得重算)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 负控:一致 ⇒ 仍记录一次,但 Match=true 且无标注
        //    (只做 ① 不做本条的夹具,对「恒报不匹配」的实现毫无判别力)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_matchingConfigVersion_recordsMatchWithoutBanner()
        {
            _stream.Append(OnsetEvent(5));
            WriteSlotOne(SavedConfigVersion);

            var captured = new List<ConfigVersionCheck>();
            NewSequencer(captured, currentConfigVersion: SavedConfigVersion).Load(new SaveSlot(1));

            Assert.That(captured.Count, Is.EqualTo(1), "一致也必须记录一次(「一致」本身是要留痕的事实)");
            Assert.That(captured[0].Match, Is.True, "两值相同 ⇒ Match 必须为 true");
            Assert.That(captured[0].Banner(), Is.Empty, "一致时不得产出标注(否则日志洪泛且误导)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 原子序:读档失败 ⇒ 一条记录都不留
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_loadFailure_recordsNothing()
        {
            var captured = new List<ConfigVersionCheck>();
            LoadSequencer sequencer = NewSequencer(captured);

            // 🔁 2026-10-31:从未写过 ⇒ 现在是 `SaveNotFoundException`(空槽 ≠ 损坏)。
            Assert.Throws<SaveNotFoundException>(() => sequencer.Load(new SaveSlot(2)),
                "前提:槽 2 从未写过 ⇒ 读档在 LoadBytes 处以 SaveNotFoundException 失败");

            Assert.That(captured.Count, Is.EqualTo(0),
                "读档失败却留下记录 ⇒ 排查者会以为这份档已经装进去了");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 未装配记录面:不抛,且**事实仍被交出**(由 SaveService 属性承载)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_noRecorderWired_loadSucceedsAndExposesHeaderVersion()
        {
            _stream.Append(OnsetEvent(5));
            WriteSlotOne(SavedConfigVersion);

            SaveService loadSide = NewLoadSideService();
            var sequencer = new LoadSequencer(loadSide, _trigger, _fallback);   // 成对缺席

            Assert.DoesNotThrow(() => sequencer.Load(new SaveSlot(1)),
                "未装配记录面 ⇒ 不比对、不抛(记录是可选义务,不是读档前置)");
            Assert.That(loadSide.LastLoadedConfigVersion, Is.EqualTo(SavedConfigVersion),
                "无论是否装配记录面,成功读档都必须交出档头版本 —— 否则记录面永远无从比对");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 半对配置 ⇒ 构造即抛(不许静默退化成「不记录」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_halfPairConfiguration_throwsAtConstruction()
        {
            SaveService loadSide = NewLoadSideService();

            Assert.Throws<ArgumentException>(() => new LoadSequencer(
                    loadSide, _trigger, _fallback,
                    currentConfigVersion: () => new ConfigVersionSnapshot(CurrentConfigVersion, null)),
                "只给提供者 ⇒ 比对了却没处记 ⇒ 必须构造期拒收");

            Assert.Throws<ArgumentException>(() => new LoadSequencer(
                    loadSide, _trigger, _fallback,
                    configVersionRecorder: (_, __) => { }),
                "只给记录面 ⇒ 没有当前版本可比 ⇒ 必须构造期拒收");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑦⑧ 记录面自身的分类行为(生产 = 引擎日志,此处 = 内存集合)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a14_driftRecord_matchingCheckAddsNoDriftAndNoLog()
        {
            var lines = new List<string>();
            var record = new ConfigVersionDriftRecord(lines.Add);

            record.Record(new ConfigVersionCheck(SavedConfigVersion, SavedConfigVersion),
                          ConfigVersionDiff.Unknown);

            Assert.That(record.DriftCount, Is.EqualTo(0), "一致不得计漂移");
            Assert.That(lines, Is.Empty, "一致不得出日志(否则每次读档一行噪声)");
            Assert.That(record.LastCheck.HasValue, Is.True, "一致也要留痕(LastCheck 供开发者回看)");
            Assert.That(record.LastCheck.Value.Match, Is.True);
        }

        [Test]
        public void test_ac7a14_driftRecord_mismatchLogsBothVersions()
        {
            var lines = new List<string>();
            var record = new ConfigVersionDriftRecord(lines.Add);

            record.Record(new ConfigVersionCheck(SavedConfigVersion, CurrentConfigVersion),
                          ConfigVersionDiff.Unknown);

            Assert.That(record.DriftCount, Is.EqualTo(1), "不匹配必须计一次");
            Assert.That(lines.Count, Is.EqualTo(1), "一次不匹配 ⇒ 恰好一行日志");
            Assert.That(lines[0], Does.Contain("无法归因"),
                "无逐份数据 ⇒ 日志必须明写「无法归因」,不得让读者以为已查清是哪张表");

            // 判据 = 两个版本号**各自**出现在日志里(十进制,与 Banner 的实现一致)。
            Assert.That(lines[0], Does.Contain(SavedConfigVersion.ToString()),
                "日志缺存档侧版本号 ⇒ 排查时不知道该拿哪个版本去重算");
            Assert.That(lines[0], Does.Contain(CurrentConfigVersion.ToString()),
                "日志缺当前侧版本号 ⇒ 看不出差在哪一端");
        }
    }
}
