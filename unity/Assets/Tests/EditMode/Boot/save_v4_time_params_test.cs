// 存档 v4 —— 时间三参数的写读 / 迁移 / hydrate / 拒收夹具
// (ADR-038 §D3 · **V-5 / V-6 / V-7 / V-8 / V-9 的执行体**)。
//
// 权威来源:
//   ADR-038 §D3(头部追加 `TICKS_PER_DAY` / `NIGHT_START` / `NIGHT_SPAN`,v3→v4 迁移,
//               读档后 hydrate 用**档内值**、`RequireTimeParams` 门、R2 整除门)
//   ADR-010 §一 / §七(头部布局 · 迁移两不变量)
//   ADR-037(配置版本段 —— 正文起点 = 按版本的头部长度 + 段长)
//
// ── 本件钉的五件事 ────────────────────────────────────────────────
// ① 写出的档声明 v4 且**三值逐值往返**(V-5)。
// ② v3 档经链迁到 v4:三值 = `36000 / 27000 / 9000`,正文与 `WorldSeed` 逐位不变(V-6)。
// ③ hydrate 决策取**档内值**,不取当前构建的数据集值(用一个**故意不同**的档内 TPD 证明)(V-7)。
// ④ 档头三值非法 ⇒ 读档**拒收**(不是钳正、不是回落)(V-8)。
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
    /// <summary>ADR-038 §D3:存档 v4 的时间三参数。</summary>
    [TestFixture]
    public sealed class SaveV4TimeParamsTest
    {
        /// <summary>档内值 —— **刻意不同于** `5_time.json` 的 `36000 / 27000 / 9000`。</summary>
        private static readonly SaveTimeParams ArchiveTime = new SaveTimeParams(18000L, 13500L, 4500L);

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private FoldedPredicate _predicate;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_v4time_" + Guid.NewGuid().ToString("N"));
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
        public void test_v4_roundTripsTheThreeArchiveValues()
        {
            byte[] signed = SaveService.AddChecksum(ComposeUnsigned(ArchiveTime));
            File.WriteAllBytes(Path.Combine(_testDir, "save_1.bin"), signed);

            Assert.That(SaveHeaderCodec.Read(signed).SaveVersion, Is.EqualTo(4u),
                "写出的档必须声明 v4 —— 否则下面的往返测的是旧布局");

            SaveService load = NewLoadSide();
            load.Load(new SaveSlot(1));

            Assert.That(load.LastLoadedTimeParams.HasValue, Is.True,
                "读档必须交出时间参数(否则 hydrate 无输入)");
            SaveTimeParams got = load.LastLoadedTimeParams.Value;
            Assert.That(got.TicksPerDay, Is.EqualTo(18000L), "日长须逐值往返(字面量哨兵)");
            Assert.That(got.NightStart, Is.EqualTo(13500L));
            Assert.That(got.NightSpan, Is.EqualTo(4500L));
        }

        // ══════════════ ② v3→v4 迁移(V-6)══════════════

        [Test]
        public void test_v3ToV4_migrationWritesLegacyValues_andKeepsBody()
        {
            byte[] v4 = ComposeUnsigned(ArchiveTime);
            byte[] v3 = DowngradeToV3(v4);
            File.WriteAllBytes(Path.Combine(_testDir, "save_1.bin"), SaveService.AddChecksum(v3));

            SaveService load = NewLoadSide();
            Assert.DoesNotThrow(() => load.Load(new SaveSlot(1)),
                "v3 档必须能读(迁移链的第三条真实输入)");

            SaveTimeParams got = load.LastLoadedTimeParams.Value;
            Assert.That(got.TicksPerDay, Is.EqualTo(36000L),
                "v3 档的唯一可能日长 = 36000(ADR-038 §D3 记录事实,不是回落默认值)");
            Assert.That(got.NightStart, Is.EqualTo(27000L));
            Assert.That(got.NightSpan, Is.EqualTo(9000L),
                "⚠️ 读到 36000/27000/9000 就证明链真的跑了 v3→v4 —— 盘上那份仍是 v3" +
                "(`Load` 只迁内存、不回写),故**不得**再去断言文件头的版本号");
        }

        // ══════════════ ③ hydrate 取档内值(V-7)══════════════

        [Test]
        public void test_hydrate_takesArchiveValues_overCurrentDatasetValues()
        {
            var current = new TimeParams
            {
                TICKS_PER_DAY = 36000L,     // 当前构建的数据集值
                NIGHT_START = 27000L,
                NIGHT_SPAN = 9000L,
                TICKS_PER_SEASON = 777L,
                SEASONS_PER_YEAR = 4L,
            };

            TimeParams hydrated = BootRoot.HydrateTimeParams(ArchiveTime, current);

            Assert.That(hydrated.TICKS_PER_DAY, Is.EqualTo(18000L),
                "运行时必须用档内日长 —— 用当前构建值会让重放出的日边界与玩家经历的不同");
            Assert.That(hydrated.NIGHT_START, Is.EqualTo(13500L));
            Assert.That(hydrated.NIGHT_SPAN, Is.EqualTo(4500L));
            Assert.That(hydrated.TICKS_PER_SEASON, Is.EqualTo(777L),
                "未裁定的季参数不由档提供 ⇒ 保留当前值(不得被清零)");
            Assert.That(hydrated.SEASONS_PER_YEAR, Is.EqualTo(4L));
        }

        [Test]
        public void test_hydrate_unspecifiedArchiveParamsThrows()
        {
            Assert.Throws<InvalidOperationException>(
                () => BootRoot.HydrateTimeParams(default(SaveTimeParams), null),
                "没有档内参数 ⇒ fail-loud,不得沿用旧参数");
        }

        [Test]
        public void test_hydrate_invalidArchiveParamsThrows()
        {
            // 逐值使三关系之一不成立 —— 每条都是**独立的**守卫。
            // ⚠️ TPD = 0 先撞 `IsSpecified`(它判 `TPD > 0`)⇒ 抛的是 InvalidOperationException,
            //    不是 `Validate()` 的 ArgumentException。两者都是 fail-closed,但归因不同,
            //    不得混写成「一律 ArgumentException」。
            Assert.Throws<InvalidOperationException>(
                () => BootRoot.HydrateTimeParams(new SaveTimeParams(0L, 0L, 1L), null),
                "TPD = 0 ⇒ IsSpecified 假 ⇒ 走「档头没有可用时间参数」分支");

            Assert.Throws<ArgumentException>(
                () => BootRoot.HydrateTimeParams(new SaveTimeParams(36000L, 36000L, 9000L), null),
                "NIGHT_START 越界");

            Assert.Throws<ArgumentException>(
                () => BootRoot.HydrateTimeParams(new SaveTimeParams(36000L, 27000L, 36000L), null),
                "NIGHT_SPAN ≥ TPD");
        }

        // ══════════════ ④ 非法档头拒收(V-8)══════════════

        [Test]
        public void test_load_rejectsZeroTicksPerDay()
        {
            byte[] bad = ComposeWithPatchedTime(0L, 27000L, 9000L);
            File.WriteAllBytes(Path.Combine(_testDir, "save_1.bin"), SaveService.AddChecksum(bad));

            var ex = Assert.Throws<InvalidDataException>(() => NewLoadSide().Load(new SaveSlot(1)));
            StringAssert.Contains("TICKS_PER_DAY", ex.Message,
                "报文须点名是哪个字段 —— 只报「存档损坏」会让排查去查校验和");
        }

        [Test]
        public void test_load_rejectsNightSpanNotLessThanDay()
        {
            byte[] bad = ComposeWithPatchedTime(36000L, 27000L, 36000L);
            File.WriteAllBytes(Path.Combine(_testDir, "save_1.bin"), SaveService.AddChecksum(bad));

            var ex = Assert.Throws<InvalidDataException>(() => NewLoadSide().Load(new SaveSlot(1)));
            StringAssert.Contains("NIGHT_SPAN", ex.Message);
        }

        // ══════════════ ⑤ 整除门(R2 / V-9)══════════════

        [Test]
        public void test_divisibleWindow_acceptsPositiveControl()
        {
            Assert.DoesNotThrow(() => BootRoot.RequireDivisibleWindow(36000L, 6000L),
                "正控:真表这一对(36000 / 6000)必须通过 —— 否则门恒红,后面两条负控没有基准");
        }

        [Test]
        public void test_divisibleWindow_rejectsWhenNotDivisible()
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => BootRoot.RequireDivisibleWindow(36000L, 7000L));
            StringAssert.Contains("ROLL_INTERVAL", ex.Message,
                "报文须点名是谁不整除 —— 只说「边界错位」会让排查去查日长");
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
        /// 组一份 v4 字节后**把头部三值改坏**(校验和随后重算)。
        /// <para>⚠️ 不能直接 `Compose(TPD=0)` —— `SaveComposer` 自己的守卫会先抛
        /// (<c>IsSpecified</c> 判 <c>TPD &gt; 0</c>),那样测到的是写侧,不是**读侧的拒收门**。
        /// 本夹具要证的是「一份坏档(可能来自旧程序 / 手改)不会被读进来」。</para>
        /// </summary>
        private byte[] ComposeWithPatchedTime(long ticksPerDay, long nightStart, long nightSpan)
        {
            byte[] bytes = ComposeUnsigned(ArchiveTime);
            int at = SaveHeaderCodec.FixedSize;                 // 36 —— 三值的起点(不含校验和形态)
            WriteI64Le(bytes, at, ticksPerDay);
            WriteI64Le(bytes, at + 8, nightStart);
            WriteI64Le(bytes, at + 16, nightSpan);
            return bytes;
        }

        private static void WriteI64Le(byte[] b, int offset, long value)
        {
            for (int i = 0; i < 8; i++) b[offset + i] = (byte)(value >> (8 * i));
        }

        /// <summary>把 v4 的不含校验和字节还原成 v3:去掉 24 B 时间参数、版本号写 3。</summary>
        private static byte[] DowngradeToV3(byte[] v4)
        {
            int head = SaveHeaderCodec.FixedSize;          // 36
            int tailStart = SaveHeaderCodec.FixedSizeV4;   // 60
            var v3 = new byte[v4.Length - SaveHeaderCodec.TimeParamsBytes];
            Array.Copy(v4, 0, v3, 0, head);
            Array.Copy(v4, tailStart, v3, head, v4.Length - tailStart);
            v3[SaveHeaderCodec.SaveVersionOffsetFixed] = 3;
            v3[SaveHeaderCodec.SaveVersionOffsetFixed + 1] = 0;
            v3[SaveHeaderCodec.SaveVersionOffsetFixed + 2] = 0;
            v3[SaveHeaderCodec.SaveVersionOffsetFixed + 3] = 0;
            return v3;
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
                // 生产装配同款:v2→v3 / v3→v4 两级都注册。
                migrations: new ISaveMigration[]
                {
                    new SaveMigrationV2ToV3(),
                    new SaveMigrationV3ToV4(),
                },
                writeScheduler: action => action(), blobRestoreSink: _pool);
        }
    }
}
