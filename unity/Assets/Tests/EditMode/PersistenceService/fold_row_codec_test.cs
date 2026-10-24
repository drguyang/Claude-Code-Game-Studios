// `FoldRowCodec` 的布局钉与定义域守卫 —— TD-persist-03 动作 ⑤ 测试面。
//
// 权威来源:
//   Sim/FoldRow.cs —— 六字段逐字次序(ADR-010 §二);「按位置读折叠行的下游」
//   Gameplay.Boot/FoldRowCodec.cs —— 段布局 [count:i32][36B × count],outcome ∈ {1,2}
//   docs/tech-debt-register.md TD-persist-03(动作 ⑤;S19-1 判据 B-2/B-5 兑现)
//
// 黄金字节:字段序改动 = 布局漂移,读旧档即静默错位 —— 本文件按位置硬钉。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class FoldRowCodecTest
    {
        private static FoldRow Sample() =>
            new FoldRow(onset: 0x1122334455667788L,
                        diseaseId: 0x0A0B0C0D,
                        patientId: 0x01020304,
                        patientSeed: 0x7FFE0000AAAA5555L,
                        outcome: (int)PatientOutcome.Died,
                        tEnd: 0x5544332211AABB00L);

        // ══════════════════ 往返与黄金序 ══════════════════

        [Test]
        public void test_foldrowcodec_roundtrip_byteExact()
        {
            // Arrange
            var rows = new[] { Sample(), new FoldRow(1L, 2, 3, 4L, (int)PatientOutcome.Recovered, 5L) };

            // Act
            byte[] bytes = FoldRowCodec.EncodeSegment(rows);
            System.Collections.Generic.IReadOnlyList<FoldRow> back = FoldRowCodec.DecodeSegment(bytes);

            // Assert
            Assert.AreEqual(2, back.Count);
            Assert.AreEqual(rows[0], back[0], "逐字段相等(FoldRow 是 IEquatable 值)");
            Assert.AreEqual(rows[1], back[1]);
        }

        [Test]
        public void test_foldrowcodec_fieldOrder_pinnedByGoldenBytes()
        {
            // 布局钉:首行 = onset(i64 LE)开头。各字段值互不相同 ⇒ 字节序错位必红。
            // ADR-010 §二 逐字:onset · diseaseId · patientId · patientSeed · outcome · tEnd。
            byte[] bytes = FoldRowCodec.EncodeSegment(new[] { Sample() });

            Assert.AreEqual(4 + FoldRowCodec.RowSize, bytes.Length,
                "count 前缀 4 字节 + 定长行 36 字节");
            Assert.AreEqual(0x88, bytes[4], "onset 的最低有效字节在前(小端)—— 字段序第一");
            // diseaseId 紧随 onset 的 8 字节 ⇒ offset 12
            Assert.AreEqual(0x0D, bytes[12], "diseaseId 最低字节在 offset 12(onset 之后)");
            // patientId 紧随 diseaseId 的 4 字节 ⇒ offset 16
            Assert.AreEqual(0x04, bytes[16], "patientId 最低字节在 offset 16");
            // patientSeed 8 字节 ⇒ offset 20
            Assert.AreEqual(0x55, bytes[20], "patientSeed 最低字节在 offset 20");
            // outcome 4 字节 ⇒ offset 28;Died = 2
            Assert.AreEqual(2, bytes[28], "outcome 在 offset 28(Died = 2)");
            // tEnd 8 字节 ⇒ offset 32
            Assert.AreEqual(0x00, bytes[32], "tEnd 最低字节在 offset 32(样例 tEnd 末字节 = 0x00)");
        }

        // ══════════════════ 定义域守卫 ══════════════════

        [Test]
        public void test_foldrowcodec_outcomeZero_rejectedOnDecode()
        {
            // 0 = 「未终态」哨兵 —— 折叠行撒谎且无法回退(ADR-032 ②),解码期即拒。
            var bad = new FoldRow(1L, 2, 3, 4L, outcome: 0, tEnd: 5L);
            byte[] bytes = FoldRowCodec.EncodeSegment(new[] { bad });
            // 手工把 outcome 字节(offset 4 + 28 = 32)改成 0:
            bytes[4 + 28] = 0;
            bytes[5 + 28] = 0;
            bytes[6 + 28] = 0;
            bytes[7 + 28] = 0;

            var ex = Assert.Throws<InvalidDataException>(
                () => FoldRowCodec.DecodeSegment(bytes),
                "outcome=0 是哨兵值,静默进 sim = 折叠行撒谎");
            StringAssert.Contains("outcome", ex.Message);
        }

        [Test]
        public void test_foldrowcodec_outcomeUnknown_rejectedOnDecode()
        {
            // 未知 outcome(3)= 档损坏 / 跨版本布局漂移,同样拒收。
            var bad = new FoldRow(1L, 2, 3, 4L, outcome: 3, tEnd: 5L);
            byte[] bytes = FoldRowCodec.EncodeSegment(new[] { bad });

            Assert.Throws<InvalidDataException>(() => FoldRowCodec.DecodeSegment(bytes));
        }

        [Test]
        public void test_foldrowcodec_negativeCount_rejected()
        {
            // count = -1(全 0xFF)—— 截断/损坏档的最常见形态。
            byte[] bytes = { 0xFF, 0xFF, 0xFF, 0xFF };
            var ex = Assert.Throws<InvalidDataException>(() => FoldRowCodec.DecodeSegment(bytes));
            StringAssert.Contains("为负", ex.Message);
        }

        [Test]
        public void test_foldrowcodec_countBeyondCapacity_rejected()
        {
            // count 大到剩余字节装不下 ⇒ 防超大分配(与 SimEventListCodec 同款上界校验)。
            byte[] bytes = { 0x0A, 0x00, 0x00, 0x00 }; // count = 10,零行字节
            var ex = Assert.Throws<InvalidDataException>(() => FoldRowCodec.DecodeSegment(bytes));
            StringAssert.Contains("上界", ex.Message);
        }

        [Test]
        public void test_foldrowcodec_unconsumedRemainder_rejected()
        {
            // 段未读尽 = 布局与 count 不符 ⇒ 抛(防「长度写错但前 N 行对」的静默错读)。
            var rows = new[] { Sample() };
            byte[] good = FoldRowCodec.EncodeSegment(rows);
            byte[] padded = new byte[good.Length + 1];
            Array.Copy(good, padded, good.Length);

            var ex = Assert.Throws<InvalidDataException>(() => FoldRowCodec.DecodeSegment(padded));
            StringAssert.Contains("未读尽", ex.Message);
        }

        [Test]
        public void test_foldrowcodec_emptySegment_roundtripsAsZeroRows()
        {
            byte[] bytes = FoldRowCodec.EncodeSegment(new FoldRow[0]);
            Assert.AreEqual(4, bytes.Length, "空段 = count 前缀 4 字节");
            Assert.AreEqual(0, FoldRowCodec.DecodeSegment(bytes).Count);
        }
    }
}
