// 存档 v5 —— 5 的运行期七参数(时间三 + 季二 + 天气二)的写读 / 迁移 / hydrate / 拒收夹具
// (ADR-038 §D3 + **Amendment A** · V-5 / V-6 / V-7 / V-8 / V-9 的执行体)。
//
// 权威来源:
//   ADR-038 §D3(v4 记时间三值)· Amendment A(2026-11-03 用户裁定:同时进存档 v5,
//                追加 `TICKS_PER_SEASON / SEASONS_PER_YEAR / WEATHER_BLOCK_TICKS / INTENSITY_MAX`)
//   ADR-010 §一 / §七(头部布局 · 迁移两不变量)
//   ADR-037(配置版本段 —— 正文起点 = 按版本的头部长度 + 段长)
//
// ── 本件钉的五件事 ────────────────────────────────────────────────
// ① 写出的档声明 v5,且**七值逐值往返**(V-5)。
// ② v4 档经链迁到 v5:季/天气四值 = 用户裁定值,正文与 `WorldSeed` 逐位不变(V-6);
//    v3 档一路迁到 v5 同样成立(三级链)。
// ③ hydrate 决策取**档内七值**,不取当前构建的数据集值(V-7)。
// ④ 档头七值任一关系非法 ⇒ 读档**拒收**(不是钳正、不是回落)(V-8)。
// ⑤ 档内日长不被 `ROLL_INTERVAL` 整除 ⇒ 拒收(R2 / V-9)。
//
// 写盘边界:落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。
// 本文件**不读**仓库内作者态文件。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>ADR-038 Amendment A:存档 v5 的季 / 天气四值。</summary>
    [TestFixture]
    public sealed class SaveV5RuntimeParamsTest
    {
        /// <summary>档内七值 —— **全部刻意不同于**当前构建的数据集值,以证 hydrate 取的是档内值。</summary>
        private static readonly SaveTimeParams ArchiveTime =
            new SaveTimeParams(18000L, 13500L, 4500L, 126000L, 4L, 3000L, 50L);

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private FoldedPredicate _predicate;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_v5time_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new FakeIdAuthority(), new FakePresenceQuery());
            _predicate = new FoldedPredicate(
                historyStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.History),
                caseStream: () => SaveComposer.FilterByStream(_stream.Events, StreamId.Case),
                diseaseLookup: _ => null,
                decoder: _decoder);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // ══════════════ ① 写读往返(V-5)══════════════

        [Test]
        public void test_v5_roundTripsAllSevenValues()
        {
            byte[] signed = SaveService.AddChecksum(ComposeUnsigned(ArchiveTime));
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), signed);

            Assert.That(SaveHeaderCodec.Read(signed).SaveVersion,
                Is.EqualTo(SaveHeader.CurrentSaveVersion),
                "写出的档必须声明**当前**版本 —— 否则下面的往返测的是旧布局" +
                "(🔁 2026-11-06 ADR-042:v5 → v6,本条随之从字面 5 改为跟随常量)");

            SaveService load = NewLoadSide();
            load.Load(new SaveSlot(4));

            Assert.That(load.LastLoadedTimeParams.HasValue, Is.True,
                "读档必须交出运行期参数(否则 hydrate 无输入)");
            SaveTimeParams got = load.LastLoadedTimeParams.Value;
            Assert.That(got.TicksPerDay, Is.EqualTo(18000L), "日长须逐值往返(字面量哨兵)");
            Assert.That(got.NightStart, Is.EqualTo(13500L));
            Assert.That(got.NightSpan, Is.EqualTo(4500L));
            Assert.That(got.TicksPerSeason, Is.EqualTo(126000L), "季长须随档走(Amendment A)");
            Assert.That(got.SeasonsPerYear, Is.EqualTo(4L));
            Assert.That(got.WeatherBlockTicks, Is.EqualTo(3000L), "天气块长须随档走(Amendment A)");
            Assert.That(got.IntensityMax, Is.EqualTo(50L));
        }

        // ══════════════ ② 迁移(V-6)══════════════

        [Test]
        public void test_v4ToV5_migrationWritesAdjudicatedSeasonAndWeather()
        {
            byte[] v4 = DowngradeToV4(ComposeUnsigned(ArchiveTime));
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(v4));

            SaveService load = NewLoadSide();
            Assert.DoesNotThrow(() => load.Load(new SaveSlot(4)), "v4 档必须能读");

            SaveTimeParams got = load.LastLoadedTimeParams.Value;
            Assert.That(got.TicksPerDay, Is.EqualTo(18000L), "档内三值原样保留");
            Assert.That(got.TicksPerSeason, Is.EqualTo(252000L),
                "v4 档的唯一可能季长 = 252000(ADR-038 Amendment A 记录事实,不是回落默认值)");
            Assert.That(got.SeasonsPerYear, Is.EqualTo(5L));
            Assert.That(got.WeatherBlockTicks, Is.EqualTo(6000L));
            Assert.That(got.IntensityMax, Is.EqualTo(100L));
        }

        [Test]
        public void test_v3ToV5_migrationChainsThreeSteps()
        {
            // v3 → v4(补时间三值)→ v5(补季/天气四值),正文与 WorldSeed 由链逐级断言。
            byte[] v3 = DowngradeToV3(ComposeUnsigned(ArchiveTime));
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(v3));

            SaveService load = NewLoadSide();
            Assert.DoesNotThrow(() => load.Load(new SaveSlot(4)), "v3 档必须能一路迁到 v5");

            SaveTimeParams got = load.LastLoadedTimeParams.Value;
            Assert.That(got.TicksPerDay, Is.EqualTo(36000L), "v3 档的时间三值来自 V3ToV4 的事实常量");
            Assert.That(got.TicksPerSeason, Is.EqualTo(252000L));
            Assert.That(got.SeasonsPerYear, Is.EqualTo(5L));
        }

        // ══════════════ ③ hydrate 取档内值(V-7)══════════════

        [Test]
        public void test_hydrate_takesArchiveValues_overCurrentDatasetValues()
        {
            TimeParams hydrated = BootRoot.HydrateTimeParams(ArchiveTime);

            Assert.That(hydrated.TICKS_PER_DAY, Is.EqualTo(18000L),
                "运行时必须用档内日长 —— 用当前构建值会让重放出的日边界与玩家经历的不同");
            Assert.That(hydrated.NIGHT_START, Is.EqualTo(13500L));
            Assert.That(hydrated.NIGHT_SPAN, Is.EqualTo(4500L));
            Assert.That(hydrated.TICKS_PER_SEASON, Is.EqualTo(126000L),
                "季长同样取自档内(Amendment A)");
            Assert.That(hydrated.SEASONS_PER_YEAR, Is.EqualTo(4L));
        }

        [Test]
        public void test_hydrate_unspecifiedArchiveParamsThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => BootRoot.HydrateTimeParams(default(SaveTimeParams)),
                "没有档内参数 ⇒ fail-loud,不得沿用旧参数");

            Assert.Throws<InvalidOperationException>(
                () => BootRoot.HydrateTimeParams(
                    new SaveTimeParams(36000L, 27000L, 9000L, 0L, 5L, 6000L, 100L)),
                "季长 = 0 视为「没给值」⇒ 同一个 fail-loud 分支");
        }

        [Test]
        public void test_hydrate_invalidArchiveParamsThrows()
        {
            // 逐值使某个关系不成立 —— 每条都是**独立的**守卫。
            Assert.Throws<ArgumentException>(
                () => BootRoot.HydrateTimeParams(new SaveTimeParams(36000L, 36000L, 9000L, 252000L, 5L, 6000L, 100L)),
                "NIGHT_START 越界");

            Assert.Throws<ArgumentException>(
                () => BootRoot.HydrateTimeParams(new SaveTimeParams(36000L, 27000L, 36000L, 252000L, 5L, 6000L, 100L)),
                "NIGHT_SPAN ≥ TPD");

            // ⚠️ 0 值属于「没给值」(IsSpecified 假)⇒ InvalidOperationException,
            //    不是关系错 ⇒ 见上方 unspecified 用例,不在这里重复。
            Assert.Throws<ArgumentException>(
                () => BootRoot.HydrateTimeParams(new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 1L, 6000L, 100L)),
                "SEASONS_PER_YEAR = 1(轴退化)");
        }

        // ══════════════ ④ 非法档头拒收(V-8)══════════════

        [Test]
        public void test_load_rejectsZeroTicksPerDay()
        {
            byte[] bad = ComposeWithPatched(ticksPerDay: 0L);
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(bad));

            var ex = Assert.Throws<InvalidDataException>(() => NewLoadSide().Load(new SaveSlot(4)));
            StringAssert.Contains("TICKS_PER_DAY", ex.Message,
                "报文须点名是哪个字段 —— 只报「存档损坏」会让排查去查校验和");
        }

        [Test]
        public void test_load_rejectsZeroSeasonLength()
        {
            byte[] bad = ComposeWithPatched(ticksPerSeason: 0L);
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(bad));

            var ex = Assert.Throws<InvalidDataException>(() => NewLoadSide().Load(new SaveSlot(4)));
            StringAssert.Contains("TICKS_PER_SEASON", ex.Message);
        }

        [Test]
        public void test_load_rejectsSingleSeasonYear()
        {
            byte[] bad = ComposeWithPatched(seasonsPerYear: 1L);
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(bad));

            var ex = Assert.Throws<InvalidDataException>(() => NewLoadSide().Load(new SaveSlot(4)));
            StringAssert.Contains("SEASONS_PER_YEAR", ex.Message);
        }

        [Test]
        public void test_load_rejectsZeroWeatherBlock()
        {
            byte[] bad = ComposeWithPatched(weatherBlockTicks: 0L);
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(bad));

            var ex = Assert.Throws<InvalidDataException>(() => NewLoadSide().Load(new SaveSlot(4)));
            StringAssert.Contains("WEATHER_BLOCK_TICKS", ex.Message);
        }

        // ══════════════ ⑤ 整除门(R2 / V-9)══════════════

        [Test]
        public void test_hydrate_mismatchedRollInterval_throws()
        {
            var ex = Assert.Throws<InvalidDataException>(() =>
                BootRoot.HydrateTimeParams(ArchiveTime, rollInterval: 7000L));
            StringAssert.Contains("ROLL_INTERVAL", ex.Message);
        }

        [Test]
        public void test_hydrate_matchingRollInterval_accepts()
        {
            Sim.TimeParams hydrated = BootRoot.HydrateTimeParams(ArchiveTime, rollInterval: 6000L);
            Assert.That(hydrated.TICKS_PER_DAY, Is.EqualTo(18000L));
        }

        [Test]
        public void test_loadThenHydrate_mismatchedRollInterval_throws_endToEnd()
        {
            // 端到端(决策层):存档 → SaveService.Load → 档内七值 → hydrate(52 的 roll)。
            // ⚠️ 仍未覆盖的是 `BootRoot.Start` 里那一行启动期调用本身(见证据 §未做)。
            byte[] signed = SaveService.AddChecksum(ComposeUnsigned(ArchiveTime));
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), signed);

            SaveService load = NewLoadSide();
            load.Load(new SaveSlot(4));

            Assert.Throws<InvalidDataException>(() =>
                BootRoot.HydrateTimeParams(load.LastLoadedTimeParams.Value, rollInterval: 7000L));
        }

        [Test]
        public void test_crossDatasetWindow_mismatchedViaProvider_throws()
        {
            var ex = Assert.Throws<InvalidDataException>(() =>
                BootRoot.RequireCrossDatasetWindow(
                    new TimeParams { TICKS_PER_DAY = 36000L, NIGHT_START = 27000L, NIGHT_SPAN = 9000L },
                    new FakeDataProvider(rollInterval: 7000L)));
            StringAssert.Contains("整除", ex.Message);
        }

        [Test]
        public void test_crossDatasetWindow_matchingViaProvider_accepts()
        {
            Assert.DoesNotThrow(() => BootRoot.RequireCrossDatasetWindow(
                new TimeParams { TICKS_PER_DAY = 36000L, NIGHT_START = 27000L, NIGHT_SPAN = 9000L },
                new FakeDataProvider(rollInterval: 6000L)));
        }

        [Test]
        public void test_crossDatasetWindow_unknownInputs_doNotThrow()
        {
            // 「不知道」不得被读成「不合规」,也不得被读成「已校验」—— 三种输入都只是跳过。
            Assert.DoesNotThrow(() => BootRoot.RequireCrossDatasetWindow(null, null));
            Assert.DoesNotThrow(() => BootRoot.RequireCrossDatasetWindow(
                new TimeParams { TICKS_PER_DAY = 36000L, NIGHT_START = 27000L, NIGHT_SPAN = 9000L },
                provider: null));
            Assert.DoesNotThrow(() => BootRoot.RequireCrossDatasetWindow(
                new TimeParams { TICKS_PER_DAY = 36000L, NIGHT_START = 27000L, NIGHT_SPAN = 9000L },
                new FakeDataProvider(rollInterval: 0L)));
        }

        [Test]
        public void test_divisibleWindow_acceptsPositiveControl()
        {
            Assert.DoesNotThrow(() => BootRoot.RequireDivisibleWindow(36000L, 6000L),
                "正控:真表这一对(36000 / 6000)必须通过");
        }

        [Test]
        public void test_divisibleWindow_rejectsWhenNotDivisible()
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => BootRoot.RequireDivisibleWindow(36000L, 7000L));
            StringAssert.Contains("ROLL_INTERVAL", ex.Message);
        }

        [Test]
        public void test_resolveRollInterval_nullProvider_isZeroNotAGuess()
        {
            Assert.That(BootRoot.ResolveRollInterval(null), Is.EqualTo(0L),
                "provider 为 null ⇒ 0 = 「不知道」,调用方跳过检查;**不得**回落真表的 6000");
        }

        // ══════════════ 夹具工具 ══════════════

        private byte[] ComposeUnsigned(SaveTimeParams time)
            => SaveComposer.Compose(
                _stream.Events, _predicate, _decoder, new SaveCodec(),
                worldSeed: 0x1122334455667788UL, configVersion: 7u, tick: 42L,
                timeParams: time, existingFoldedRows: _stream.FoldedRows, blobPool: _pool);

        /// <summary>
        /// 组一份 v5 字节后**把头部七个值中的某一个改坏**(校验和随后重算)。
        /// <para>⚠️ 不能直接 `Compose(TPD=0)` —— `SaveComposer` 的 `IsSpecified` 会先抛,
        /// 那样测到的是写侧,不是**读侧的拒收门**。</para>
        /// </summary>
        private byte[] ComposeWithPatched(
            long? ticksPerDay = null, long? ticksPerSeason = null,
            long? seasonsPerYear = null, long? weatherBlockTicks = null)
        {
            byte[] bytes = ComposeUnsigned(ArchiveTime);
            int at = SaveHeaderCodec.FixedSize;              // 36 —— 不含校验和形态下七值的起点
            if (ticksPerDay.HasValue) WriteI64Le(bytes, at + 0, ticksPerDay.Value);
            if (ticksPerSeason.HasValue) WriteI64Le(bytes, at + 24, ticksPerSeason.Value);
            if (seasonsPerYear.HasValue) WriteI64Le(bytes, at + 32, seasonsPerYear.Value);
            if (weatherBlockTicks.HasValue) WriteI64Le(bytes, at + 40, weatherBlockTicks.Value);
            return bytes;
        }

        private static void WriteI64Le(byte[] b, int offset, long value)
        {
            for (int i = 0; i < 8; i++) b[offset + i] = (byte)(value >> (8 * i));
        }

        /// <summary>v6 字节 → v5(去掉落笔时刻 8 B,版本号写 5)。</summary>
        /// <remarks>
        /// 🔁 **2026-11-06(ADR-042)**:`Compose` 现在出 v6。所有「降级到旧版」的夹具都必须
        /// **逐级往回剥** —— 直接按 v5 的偏移去剥会把落笔时刻那 8 B 留在正文头上,
        /// 症状是「尾随 8 字节未被任何字段消费」(实测踩到)。
        /// </remarks>
        private static byte[] DowngradeToV5(byte[] v6)
        {
            int head = SaveHeaderCodec.FixedSizeV5;           // 92
            var v5 = new byte[v6.Length - SaveHeaderCodec.SavedAtBytes];
            Array.Copy(v6, 0, v5, 0, head);
            Array.Copy(v6, SaveHeaderCodec.FixedSizeV6, v5, head, v6.Length - SaveHeaderCodec.FixedSizeV6);
            WriteVersion(v5, 5);
            return v5;
        }

        /// <summary>v6 字节 → v4(去掉落笔时刻 8 B + 季/天气 32 B,版本号写 4)。</summary>
        private static byte[] DowngradeToV4(byte[] v6)
        {
            byte[] v5 = DowngradeToV5(v6);
            int head = SaveHeaderCodec.FixedSizeV4;           // 60
            var v4 = new byte[v5.Length - SaveHeaderCodec.SeasonWeatherBytes];
            Array.Copy(v5, 0, v4, 0, head);
            Array.Copy(v5, SaveHeaderCodec.FixedSizeV5, v4, head, v5.Length - SaveHeaderCodec.FixedSizeV5);
            WriteVersion(v4, 4);
            return v4;
        }

        /// <summary>v5 字节 → v3(再去掉时间三值 24 B)。</summary>
        private static byte[] DowngradeToV3(byte[] v5)
        {
            byte[] v4 = DowngradeToV4(v5);
            int head = SaveHeaderCodec.FixedSize;             // 36
            var v3 = new byte[v4.Length - SaveHeaderCodec.TimeParamsBytes];
            Array.Copy(v4, 0, v3, 0, head);
            Array.Copy(v4, SaveHeaderCodec.FixedSizeV4, v3, head, v4.Length - SaveHeaderCodec.FixedSizeV4);
            WriteVersion(v3, 3);
            return v3;
        }

        private static void WriteVersion(byte[] b, uint version)
        {
            int at = SaveHeaderCodec.SaveVersionOffsetFixed;
            b[at] = (byte)version;
            b[at + 1] = (byte)(version >> 8);
            b[at + 2] = (byte)(version >> 16);
            b[at + 3] = (byte)(version >> 24);
        }

        /// <summary>只提供 52 数据集的假 provider —— 让启动期交叉校验可注入「不整除」的输入。</summary>
        private sealed class FakeDataProvider : IDataProvider
        {
            private readonly long _rollInterval;

            public FakeDataProvider(long rollInterval) => _rollInterval = rollInterval;

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                if (typeof(TDataSet) == typeof(RandomEventsDataSet))
                    return (TDataSet)(object)new RandomEventsDataSet { RollInterval = _rollInterval };
                throw new NotSupportedException(
                    $"夹具只提供 52 数据集,被问的是 {typeof(TDataSet).Name}");
            }
        }

        private SaveService NewLoadSide()
        {
            IReadOnlyList<SimEvent> pending = null;
            return new SaveService(
                _testDir, new SaveCodec(),
                folded: _predicate,
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: events => pending = events,
                foldedRowSink: bytes => _stream.Restore(
                    pending ?? throw new InvalidOperationException("折叠段先于事件段到达"),
                    FoldRowCodec.DecodeSegment(bytes)),
                compose: _ => ComposeUnsigned(ArchiveTime),
                // 生产装配同款:v2→v3 / v3→v4 / v4→v5 三级都注册。
                migrations: new ISaveMigration[]
                {
                    new SaveMigrationV2ToV3(),
                    new SaveMigrationV3ToV4(),
                    new SaveMigrationV4ToV5(),
                    // ADR-042:链末端多一级,不补则旧档在 v5→v6 断掉
                    new SaveMigrationV5ToV6(),
                },
                writeScheduler: action => action(), blobRestoreSink: _pool);
        }
    }
}
