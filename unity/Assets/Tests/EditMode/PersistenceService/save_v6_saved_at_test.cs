// ADR-042 · 存档 v6(落笔时刻进档头)判据 —— 2026-11-06
//
// 判据(可证伪):
//   V-1 头部往返:`SavedAtUtcTicks` 写进去、读回来逐位相等;
//   V-2 迁移 v5→v6:插入点 = 92、写入 8 个零、版本号 = 6、**正文逐位不动**;
//       且「v5 档的时间是 0 = 未知」——不许伪造;
//   V-3 **生产装配**真的接上墙钟:经 `CompositionRoot.Assemble` 落一档,
//       探针读回的 `SavedAtUtcTicks` **非零**(= 组版闭包里的 `DateTime.UtcNow.Ticks`)。
//
// ⚠️ V-3 为什么必须是「真装配」:这条腿的全部风险是「形参有默认值 0 ⇒ 调用方忘了传」。
//   夹具直接调 `SaveComposer.Compose` 只能证明「传了就有」,证明不了**生产传了**。
//   同理,它不能靠 grep 源码充当判据(那是自述顶替门,F6)。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>ADR-042 存档 v6 判据。</summary>
    public sealed class SaveV6SavedAtTest
    {
        private static readonly long SampleTicks =
            new DateTime(2026, 11, 5, 23, 41, 7, DateTimeKind.Utc).Ticks;

        // ══════════════════════════════════════════════════════════════════
        // V-1 头部往返
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_v6_header_roundTripsSavedAt_ticks()
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(
                worldSeed: 0x5EEDUL, configVersion: 7U, tick: 1234L, snapshotOffset: 0UL,
                ticksPerDay: 36000L, nightStart: 27000L, nightSpan: 9000L,
                ticksPerSeason: 252000L, seasonsPerYear: 5L,
                weatherBlockTicks: 6000L, intensityMax: 100L,
                savedAtUtcTicks: SampleTicks);

            Assert.AreEqual(SaveHeaderCodec.FixedSizeV6, header.Length,
                "v6 头部定长 = 92 + 8 = 100");

            // 补校验和槽 ⇒ 完整形态,再逐字段读回。
            byte[] body = { 0, 0, 0, 0 };
            var full = new byte[header.Length + body.Length];
            Array.Copy(header, full, header.Length);
            Array.Copy(body, 0, full, header.Length, body.Length);
            byte[] signed = SaveService.AddChecksum(full);

            SaveHeader h = SaveHeaderCodec.Read(signed);
            Assert.AreEqual(SaveHeader.CurrentSaveVersion, h.SaveVersion);
            Assert.AreEqual(SampleTicks, h.SavedAtUtcTicks, "落笔时刻须逐位往返");
            Assert.AreEqual(100L, h.IntensityMax, "既有 v5 字段不受影响(追加是末尾,不是插队)");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-2 迁移 v5 → v6
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_v5ToV6_insertsZeroAt92_bumpsVersion_keepsBodyByteForByte()
        {
            // 造一份 v5 头:v6 头的前 92 字节就是 v5 头(新字段在末尾),再改版本号。
            byte[] v6Header = SaveHeaderCodec.WriteWithoutChecksum(
                0x5EEDUL, 7U, 1234L, 0UL, 36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L,
                SampleTicks);
            var v5 = new byte[SaveHeaderCodec.FixedSizeV5];
            Array.Copy(v6Header, v5, v5.Length);
            v5[SaveHeaderCodec.SaveVersionOffsetFixed] = 5;

            // 正文(迁移链按「从末尾算」比对,必须逐位不变)
            var tail = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03 };
            var input = new byte[v5.Length + tail.Length];
            Array.Copy(v5, input, v5.Length);
            Array.Copy(tail, 0, input, v5.Length, tail.Length);

            byte[] output = new SaveMigrationV5ToV6().Migrate(input);

            Assert.AreEqual(input.Length + 8, output.Length, "只多了 8 字节");
            Assert.AreEqual(6u, SaveMigrationChain.ReadSaveVersion(output), "版本号须写到 6");
            Assert.AreEqual(SaveHeaderCodec.FixedSizeV6, SaveHeaderCodec.FixedSizeFor(6u),
                "v6 的正文起点 = 100");

            // ⚠️ 版本号那 4 字节**必须**改(5 → 6)—— 其余头部字节逐位不动。
            int verAt = SaveHeaderCodec.SaveVersionOffsetFixed;
            for (int i = 0; i < v5.Length; i++)
            {
                if (i >= verAt && i < verAt + 4) continue;
                Assert.AreEqual(input[i], output[i], $"头部第 {i} 字节不得改动(版本号以外)");
            }

            for (int i = 0; i < 8; i++)
                Assert.AreEqual(0, output[SaveHeaderCodec.FixedSizeV5 + i],
                    $"插入的 8 字节必须全是 0(= 未知),第 {i} 个不是 —— 补任何值都是伪造");

            for (int i = 0; i < tail.Length; i++)
                Assert.AreEqual(tail[i], output[SaveHeaderCodec.FixedSizeV6 + i],
                    $"正文第 {i} 字节须逐位不动(迁移链的尾部锚定不变量)");
        }

        [Test]
        public void test_v5ToV6_rejectsWrongSourceVersion()
        {
            byte[] v5 = new byte[SaveHeaderCodec.FixedSizeV5];
            // magic 正确但版本号是 4 ⇒ 本步不该接手(链的每一级只处理自己那一版)
            v5[0] = 0x44; v5[1] = 0x59; v5[2] = 0x4A; v5[3] = 0x51;   // "DYJQ" 小端
            v5[SaveHeaderCodec.SaveVersionOffsetFixed] = 4;

            Assert.Throws<InvalidOperationException>(() => new SaveMigrationV5ToV6().Migrate(v5),
                "版本号不符必须显式失败,不得按目标布局解析");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-3 生产装配真的接上了墙钟
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_productionAssembly_checkpoint_writesNonZeroSavedAt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dyjc-v6-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                CompositionRootServices bag = CompositionRoot.Assemble(
                    dataProvider: DiskBackedDataProvider.Create(), saveBasePath: dir);

                long before = DateTime.UtcNow.Ticks;
                bag.SaveService.Checkpoint(new SaveSlot(3));
                bag.SaveService.WriteQueue.WaitForIdle();
                long after = DateTime.UtcNow.Ticks;

                SaveSlotSummary summary = SaveSlotProbe.Probe(dir, 3u);
                Assert.AreEqual(SaveSlotState.Occupied, summary.State, "前置:档确实写出来了");
                Assert.Greater(summary.SavedAtUtcTicks, 0L,
                    "生产装配必须真的把墙钟写进档头 —— 0 会让册子上那一行没有时间" +
                    "(这正是「形参默认 0 ⇒ 调用方忘了传」这条风险的判据)");
                Assert.That(summary.SavedAtUtcTicks, Is.InRange(before, after),
                    "写进去的必须是**这一次落笔**的时刻,不是别的常数");
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }
    }
}
