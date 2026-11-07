// ADR-037 —— 存档 v3 配置版本段(逐份 `ConfigVersion`)的判据。
//
// 权威:docs/architecture/adr-037-save-v3-config-version-segment.md
//   §① 段位置(紧跟头部)· §② 规范形(严格升序)· §④ 未知 ≠ 无差异 · §⑤ v2→v3 迁移
//
// ── 本件钉的四件事 ─────────────────────────────────────────────
// ① **规范形唯一**:同一集合只有一种字节表示;乱序 / 重复一律拒收(读侧验,不是靠写侧自觉)。
// ② **差异集 = 被改的那一张表**(V-2):改一份产物头 ⇒ `Compare` 只报它。
// ③ **「未知」不得冒充「无差异」**(V-4):v2 迁上来的档 `HasPerArtifactData = false`。
// ④ **v2→v3 迁移逐位保留正文**(V-3):迁移产物与「同一份 v3 直接写出」的字节**完全相同**。
//
// 写盘边界:落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>ADR-037:配置版本段的编解码 · 差异集 · v2→v3 迁移。</summary>
    [TestFixture]
    public sealed class SaveV3ConfigSegmentTest
    {
        private const uint Combined = 0xAABBCCDDU;

        private string _testDir;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private FoldedPredicate _predicate;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_v3cfg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
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

        // ══════════════ ① 段编解码(V-1)══════════════

        [Test]
        public void test_adr037_segment_roundTripIsCanonicalOrder()
        {
            var items = new[]
            {
                new ArtifactConfigVersion("z_data", 3u),
                new ArtifactConfigVersion("a_data", 1u),
                new ArtifactConfigVersion("m_data", 2u),
            };

            byte[] bytes = ConfigVersionSegment.Encode(items);
            var decoded = ConfigVersionSegment.Decode(bytes, 0, out int consumed);

            Assert.That(consumed, Is.EqualTo(bytes.Length), "解码必须恰好读尽整段");
            Assert.That(decoded.Count, Is.EqualTo(3));
            Assert.That(decoded[0].Address, Is.EqualTo("a_data"), "编码序 = address 的 Ordinal 升序");
            Assert.That(decoded[2].Address, Is.EqualTo("z_data"));

            // 规范形:同集合再编一次 ⇒ 逐字节相同(字节级对拍的前提)
            var shuffled = new[]
            {
                new ArtifactConfigVersion("m_data", 2u),
                new ArtifactConfigVersion("z_data", 3u),
                new ArtifactConfigVersion("a_data", 1u),
            };
            CollectionAssert.AreEqual(bytes, ConfigVersionSegment.Encode(shuffled),
                "输入序不同但集合相同 ⇒ 字节必须相同(否则同态存档跨机器对不上)");
        }

        [Test]
        public void test_adr037_segment_emptyIsCountZero()
        {
            byte[] bytes = ConfigVersionSegment.Encode(null);
            Assert.That(bytes.Length, Is.EqualTo(4), "空段 = 4 字节 count 前缀");
            Assert.That(ConfigVersionSegment.Decode(bytes, 0, out int consumed).Count, Is.EqualTo(0));
            Assert.That(consumed, Is.EqualTo(4));
        }

        [Test]
        public void test_adr037_segment_rejectsUnsortedBytes()
        {
            // 手工造「降序」段:B 在前、A 在后 —— 写侧绝不会产出,读侧必须拒收。
            byte[] bytes = BuildRawSegment(("B_data", 2u), ("A_data", 1u));

            var ex = Assert.Throws<InvalidDataException>(
                () => ConfigVersionSegment.Decode(bytes, 0, out _),
                "非升序的段必须拒收 —— 规范形不唯一 = 字节级对拍失去意义");
            Assert.That(ex.Message, Does.Contain("严格升序"));
        }

        [Test]
        public void test_adr037_segment_encodeRejectsDuplicateAddress()
        {
            var items = new[]
            {
                new ArtifactConfigVersion("a_data", 1u),
                new ArtifactConfigVersion("a_data", 2u),
            };
            Assert.Throws<ArgumentException>(() => ConfigVersionSegment.Encode(items),
                "同一张表两个版本号 ⇒ 归因会产生两个答案,必须在写侧就拒");
        }

        // ══════════════ ② 差异集(V-2)══════════════

        [Test]
        public void test_adr037_diff_reportsExactlyTheChangedTable()
        {
            var saved = new[]
            {
                new ArtifactConfigVersion("a_data", 1u),
                new ArtifactConfigVersion("b_data", 2u),
                new ArtifactConfigVersion("c_data", 3u),
            };
            var current = new[]
            {
                new ArtifactConfigVersion("a_data", 1u),
                new ArtifactConfigVersion("b_data", 999u),   // ← 只有这一张被改
                new ArtifactConfigVersion("c_data", 3u),
            };

            ConfigVersionDiff diff = ConfigVersionDiff.Compare(saved, current);

            Assert.That(diff.HasPerArtifactData, Is.True);
            Assert.That(diff.Changed.Count, Is.EqualTo(1), "恰好一张表变了");
            Assert.That(diff.Changed[0], Is.EqualTo("b_data"), "且必须是那一张");
        }

        [Test]
        public void test_adr037_diff_countsAddedAndRemoved()
        {
            var saved = new[] { new ArtifactConfigVersion("a_data", 1u) };
            var current = new[] { new ArtifactConfigVersion("b_data", 1u) };

            ConfigVersionDiff diff = ConfigVersionDiff.Compare(saved, current);

            Assert.That(diff.Changed, Is.EquivalentTo(new[] { "a_data", "b_data" }),
                "新增与移除都算差异 —— 只看 current 一侧会漏掉被删掉的那张表");
        }

        // ══════════════ ③ 未知 ≠ 无差异(V-4)══════════════

        [Test]
        public void test_adr037_diff_distinguishesUnknownFromEmpty()
        {
            ConfigVersionDiff unknown = ConfigVersionDiff.Unknown;
            Assert.That(unknown.HasPerArtifactData, Is.False, "未知:没有逐份数据");
            Assert.That(unknown.HasChanges, Is.False, "未知不得被读成「有变化」…");

            var same = new[] { new ArtifactConfigVersion("a_data", 1u) };
            ConfigVersionDiff noChange = ConfigVersionDiff.Compare(same, same);
            Assert.That(noChange.HasPerArtifactData, Is.True, "…但也不得被读成「没变化」");
            Assert.That(noChange.HasChanges, Is.False, "逐份全等才是真的无差异");

            // 两者的 `HasChanges` 都是 false ⇒ 判「是否真的没问题」**必须**先看 HasPerArtifactData
            Assert.That(unknown.HasPerArtifactData == noChange.HasPerArtifactData, Is.False,
                "这两种状态在数据面上必须可分,否则调用方无从区分");
        }

        [Test]
        public void test_adr037_diff_nullInputThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ConfigVersionDiff.Compare(null, Array.Empty<ArtifactConfigVersion>()));
            Assert.Throws<ArgumentNullException>(() => ConfigVersionDiff.Compare(Array.Empty<ArtifactConfigVersion>(), null));
        }

        // ══════════════ ④ 写读往返 + 迁移(V-3)══════════════

        [Test]
        public void test_adr037_composeWritesArtifacts_andLoadReturnsThem()
        {
            var artifacts = new[]
            {
                new ArtifactConfigVersion("b_data", 7u),
                new ArtifactConfigVersion("a_data", 5u),
            };
            // 🔁 2026-11-08(ADR-010 义务 13 起必须补):本件原来组的是**正文极短**的档
            //    (段 12 B + 空事件段 12 B = 24 B)。v7 的分册态段插在「配置版本段之后」,
            //    而 `SaveMigrationChain` 的「正文末尾等长」断言按**末尾 N 字节**取窗 ——
            //    当正文长度 < 插入偏移(这里是 112)时,这个窗口会**盖住插入点**,
            //    于是「正文没被改动」这条不变式在几何上就不成立。
            //    ⚠️ 这是**断言形态的已知局限**(tail-anchored 启发式),不是迁移改坏了正文:
            //    真实存档的正文远大于插入偏移。⇒ 本夹具把正文补到真实量级。
            for (int i = 0; i < 8; i++)
            {
                _stream.Append(new SimEvent(
                    100L + i, PatientId.None, i, EventKind.DiseaseOnset,
                    _encoder.Encode(EventKind.DiseaseOnset,
                        new DiseaseOnsetPayload(100L + i, diseaseId: 1, patientId: i,
                                                patientSeed: 1234L + i, seq: i))));
            }

            byte[] signed = SaveService.AddChecksum(ComposeUnsigned(artifacts));

            SaveService loadSide = NewLoadSide();
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), signed);
            loadSide.Load(new SaveSlot(4));

            Assert.That(loadSide.LastLoadedArtifactConfigVersions, Is.Not.Null,
                "读档必须交出逐份版本(否则「哪张表变了」永远不可判)");
            Assert.That(loadSide.LastLoadedArtifactConfigVersions.Count, Is.EqualTo(2));
            Assert.That(loadSide.LastLoadedArtifactConfigVersions[0].Address, Is.EqualTo("a_data"),
                "解码后仍是升序(规范形)");
            Assert.That(loadSide.LastLoadedArtifactConfigVersions[0].Version, Is.EqualTo(5u));
            Assert.That(loadSide.LastLoadedConfigVersion, Is.EqualTo(Combined), "组合值仍写在头部");
        }

        [Test]
        public void test_adr037_migrationV2ToV3_roundTripsToIdenticalV3Bytes()
        {
            byte[] v3 = ComposeUnsigned(null);      // 无逐份 ⇒ 段 count=0
            byte[] v2 = StripSegmentToV2(v3);       // 去掉段、版本号改 2

            // ⚠️ 目标版本写 **3**(不是 `CurrentSaveVersion`)—— 本件测的是 ADR-037 那一步;
            //    写当前版本会把 v3→v4 的 24 B 也算进来,断言就成了「v2 直接迁到 v4」。
            byte[] migrated = SaveMigrationChain.Migrate(
                v2, 3u, new ISaveMigration[] { new SaveMigrationV2ToV3() });

            CollectionAssert.AreEqual(v3, migrated,
                "v2→v3 必须产出与「同一份 v3 直接写出」**逐字节相同**的结果 —— " +
                "正文一个字节都不许动,版本号与空段是唯一差异");
        }

        [Test]
        public void test_adr037_loadV2File_migratesAndReportsUnknown()
        {
            byte[] v2 = StripSegmentToV2(ComposeUnsigned(null));

            SaveService loadSide = NewLoadSide();
            File.WriteAllBytes(Path.Combine(_testDir, "save_4.bin"), SaveService.AddChecksum(v2));

            Assert.DoesNotThrow(() => loadSide.Load(new SaveSlot(4)),
                "v2 档必须能读(迁移链第一条真实输入)");
            Assert.That(loadSide.LastLoadedArtifactConfigVersions.Count, Is.EqualTo(0),
                "v2 档没有逐份版本 ⇒ 段为空");
            Assert.That(loadSide.LastLoadedConfigVersion, Is.EqualTo(Combined), "组合值照旧交回");
        }

        // ══════════════ 夹具工具 ══════════════

        /// <summary>
        /// 组一份 **v3 布局**的不含校验和字节。
        /// <para>⚠️ ADR-038 §D3 + Amendment A 之后 `Compose` 出的是 **v5**
        /// (头部多 24 B 时间三值 + 32 B 季/天气四值,共 56 B)。
        /// 本夹具是 ADR-037 的 v3 专用件 ⇒ 组完之后**去掉**这 56 B 并把版本号写回 3,
        /// 否则 `StripSegmentToV2` 与「v3 字节逐位相等」这类断言全部错位。</para>
        /// </summary>
        private byte[] ComposeUnsigned(IReadOnlyList<ArtifactConfigVersion> artifacts)
        {
            byte[] v4 = SaveComposer.Compose(
                _stream.Events, _predicate, _decoder, new SaveCodec(),
                worldSeed: 1UL, configVersion: Combined, tick: 20L,
                timeParams: new SaveTimeParams(36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L), existingFoldedRows: _stream.FoldedRows, blobPool: _pool,
                artifactConfigs: artifacts);

            int head = SaveHeaderCodec.FixedSize;          // 36 = v3 头
            // 🔁 2026-11-08(ADR-010 义务 13):v7 在配置版本段之后多一个**分册态段**(8 B)。
            //    先把它摘掉,下面「按 v6 头切尾」的算术才成立 —— 否则它会被当成正文的一部分
            //    一起带进 v3 档,`StripSegmentToV2` 只摘配置版本段,分册态段就留在 v2 里。
            v4 = StripCasebookSegment(v4);
            // 🔁 2026-11-06(ADR-042):`Compose` 现在出 **v6**(再 +8 B 落笔时刻)⇒ 尾巴起点跟着抬到 100,
            //    去掉的字节数 = 24 + 32 + 8。只改一处不改另一处 ⇒ 正文错位 8 字节、读成「段损坏」。
            int tailStart = SaveHeaderCodec.FixedSizeV6;   // 100 = v6 头之后(24 + 32 + 8 都去掉)
            var v3 = new byte[v4.Length - (SaveHeaderCodec.TimeParamsBytes
                                          + SaveHeaderCodec.SeasonWeatherBytes
                                          + SaveHeaderCodec.SavedAtBytes)];
            System.Array.Copy(v4, 0, v3, 0, head);
            System.Array.Copy(v4, tailStart, v3, head, v4.Length - tailStart);
            v3[SaveHeaderCodec.SaveVersionOffsetFixed] = 3;
            v3[SaveHeaderCodec.SaveVersionOffsetFixed + 1] = 0;
            v3[SaveHeaderCodec.SaveVersionOffsetFixed + 2] = 0;
            v3[SaveHeaderCodec.SaveVersionOffsetFixed + 3] = 0;
            return v3;
        }

        /// <summary>摘掉 v7 的**分册态段**(8 B,紧跟配置版本段)。</summary>
        /// <remarks>插入点用 `ConfigVersionSegment.Decode` 算,不硬编码 —— 逐份版本段长度取决于写侧给了几条。</remarks>
        private static byte[] StripCasebookSegment(byte[] v7)
        {
            int cutAt = SaveHeaderCodec.FixedSizeV6;   // 100 = v6 头部末尾;分册态段紧跟其后
            int extra = CasebookSegment.EmptySize;
            var stripped = new byte[v7.Length - extra];
            Array.Copy(v7, 0, stripped, 0, cutAt);
            Array.Copy(v7, cutAt + extra, stripped, cutAt, v7.Length - cutAt - extra);
            return stripped;
        }

        /// <summary>把 v3 的**不含校验和**字节还原成 v2:去掉段、版本号写 2。</summary>
        private static byte[] StripSegmentToV2(byte[] v3)
        {
            ConfigVersionSegment.Decode(v3, SaveHeaderCodec.FixedSize, out int segmentBytes);
            var v2 = new byte[v3.Length - segmentBytes];
            Array.Copy(v3, 0, v2, 0, SaveHeaderCodec.FixedSize);
            Array.Copy(v3, SaveHeaderCodec.FixedSize + segmentBytes, v2, SaveHeaderCodec.FixedSize,
                       v3.Length - SaveHeaderCodec.FixedSize - segmentBytes);
            v2[SaveHeaderCodec.SaveVersionOffsetFixed] = 2;      // u32 LE,高位恒 0
            v2[SaveHeaderCodec.SaveVersionOffsetFixed + 1] = 0;
            v2[SaveHeaderCodec.SaveVersionOffsetFixed + 2] = 0;
            v2[SaveHeaderCodec.SaveVersionOffsetFixed + 3] = 0;
            return v2;
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
                compose: _ => ComposeUnsigned(null),
                // ADR-037 §⑤ / ADR-038 §D3:链必须注册 v2→v3 **与** v3→v4,
                // 否则旧档在链里断掉(生产装配同款;目标版本 = 当前 4)。
                migrations: new ISaveMigration[]
                {
                    new SaveMigrationV2ToV3(),
                    new SaveMigrationV3ToV4(),
                    new SaveMigrationV4ToV5(),
                    // ADR-042:链末端多一级,不补则旧档在 v5→v6 断掉
                    new SaveMigrationV5ToV6(),
                    // ADR-010 义务 13(2026-11-08):再多一级(v6 → v7 插空分册态段)
                    new SaveMigrationV6ToV7(),
                },
                writeScheduler: action => action(), blobRestoreSink: _pool);
        }

        /// <summary>手工拼一段(用于造「写侧绝不会产出」的非法字节)。</summary>
        private static byte[] BuildRawSegment(params (string addr, uint version)[] entries)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(entries.Length);
                foreach ((string addr, uint version) in entries)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(addr);
                    w.Write(bytes.Length);
                    w.Write(bytes);
                    w.Write(version);
                }
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
