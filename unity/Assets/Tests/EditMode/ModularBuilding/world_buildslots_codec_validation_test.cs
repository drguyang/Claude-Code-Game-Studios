// `world_buildslots.cooked.bytes` 读方 codec 的**值域**负例夹具(2026-10-28 代码评审 **A-22**)。
//
// 立此件缘由:读方此前对 `allowed_mask` 只拒 `== 0`,`baked_initial` 又把任意非零收成 `true`
// —— **结构字段严、值域松**,是同一缺口的两个半边:
//   · `allowed_mask = -1`(全位置位)⇒ `BuildSlot` 接受**任何**模块 ⇒ 「Shell 槽位能装 Decor」;
//   · `allowed_mask = 128` / 256 ⇒ 位落在 `SlotType` 现有位之外 ⇒ 该格**任何真实模块都不匹配**,
//     而作者侧看不出错(字节流「解析成功」)。
//
// ⚠️ **判据打报文关键词(字面量 `allowed_mask` / `baked_initial` / `行数`)**,不打异常类型
//   —— 否则任何一处 `InvalidDataException` 都能让夹具变绿(承 `TD-clinic-01` 判别力纪律)。
//
// ⚠️ **本件自造字节**:不依赖盘上产物,也就不怕「产物还没重烘」。20 字节头按
//   `Sim.Contracts.CookedFormat` 的登记布局写(magic / fmt / schema / config / payloadLen)。
//
// ⚠️ 不写盘(`FsWriteBoundaryGate` 无关)。

using System;
using System.IO;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Presentation;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    /// <summary>槽位骨架读方:值域上界 / 布尔严格性 / 空表。</summary>
    [TestFixture]
    public class world_buildslots_codec_validation_test
    {
        // ══════════════════ 一 · `allowed_mask` 值域(双侧)══════════════════

        /// <summary>`allowed_mask = 0` ⇒ 拒(该格谁也不匹配 = 静默死格)。</summary>
        [Test]
        public void test_worldBuildSlots_codec_allowedMaskZero_isRejected()
            => AssertRejected(BuildCooked(allowedMask: 0, bakedInitial: 0), "allowed_mask");

        /// <summary>`allowed_mask = -1`(全位置位)⇒ 拒。</summary>
        /// <remarks><b>判别力</b>:把校验改回「只拒 `== 0`」⇒ 本条红 —— 这正是 A-22 修复前的形态。</remarks>
        [Test]
        public void test_worldBuildSlots_codec_allowedMaskNegative_isRejected()
            => AssertRejected(BuildCooked(allowedMask: -1, bakedInitial: 0), "allowed_mask");

        /// <summary>`allowed_mask = 128`(超出现有最高位 `Shell = 1 &lt;&lt; 6 = 64`)⇒ 拒。</summary>
        /// <remarks><b>判别力</b>:删掉上界比较(只留 `&lt;= 0`)⇒ 本条红。</remarks>
        [Test]
        public void test_worldBuildSlots_codec_allowedMaskBeyondEnumBits_isRejected()
            => AssertRejected(BuildCooked(allowedMask: 128, bakedInitial: 0), "allowed_mask");

        /// <summary>上界本身(`127`,含 Shell 位)⇒ **放行**(证明上一条不是「值域恒拒」)。</summary>
        [Test]
        public void test_worldBuildSlots_codec_allowedMaskAtCeiling_isAccepted()
        {
            WorldBuildSlotsDataSet data =
                WorldBuildSlotsCookedCodec.Read(BuildCooked(allowedMask: 127, bakedInitial: 0));

            Assert.That(data.Rows.Count, Is.EqualTo(1));
            Assert.That(data.Rows[0].AllowedMask, Is.EqualTo(127), "掩码须原样保留,不做裁剪");
            Assert.That(data.Rows[0].BakedInitial, Is.False);
        }

        /// <summary>单个合法位(`64` = Shell)⇒ 放行(逐位的正向半边)。</summary>
        [Test]
        public void test_worldBuildSlots_codec_singleBitMask_isAccepted()
        {
            WorldBuildSlotsDataSet data =
                WorldBuildSlotsCookedCodec.Read(BuildCooked(allowedMask: 64, bakedInitial: 1));

            Assert.That(data.Rows[0].AllowedMask, Is.EqualTo(64), "`1 << 6` = 64");
            Assert.That(data.Rows[0].BakedInitial, Is.True);
        }

        /// <summary>
        /// 读方的掩码上界**由 `SlotType` 派生**,当前 = `127`(`1 &lt;&lt; 7) - 1`)。
        /// </summary>
        /// <remarks>⚠️ 本条红**不一定是缺陷**:它更可能是「枚举长出了第 7 个类型」——
        /// 那时须同时确认写方契约与头槽 schema 版本是否同步演进,而不是把期望值一改了事。</remarks>
        [Test]
        public void test_worldBuildSlots_codec_maxAllowedMask_isCurrentEnumCeiling()
        {
            Assert.That(WorldBuildSlotsCookedCodec.MaxAllowedMask, Is.EqualTo(127),
                "`SlotType` 当前最大枚举值 = 6 ⇒ 位域上界 = (1 << 7) - 1 = 127;" +
                "红在此处通常意味着枚举已扩展 —— 请复核写读两端的契约,不要只改期望值");
        }

        // ══════════════════ 二 · `baked_initial` 严格布尔 ══════════════════

        /// <summary>`baked_initial = 2` ⇒ 拒(非 0/1 意味着损坏或 bool→int 编码漂移)。</summary>
        /// <remarks><b>判别力</b>:把校验改回 `!= 0` 收成 true ⇒ 本条红。</remarks>
        [Test]
        public void test_worldBuildSlots_codec_bakedInitialNotBoolean_isRejected()
            => AssertRejected(BuildCooked(allowedMask: 32, bakedInitial: 2), "baked_initial");

        /// <summary>`baked_initial = -1` ⇒ 拒(负数不得被当作 `true`)。</summary>
        [Test]
        public void test_worldBuildSlots_codec_bakedInitialNegative_isRejected()
            => AssertRejected(BuildCooked(allowedMask: 32, bakedInitial: -1), "baked_initial");

        // ══════════════════ 三 · 空表 ══════════════════

        /// <summary>`row_count = 0` ⇒ 拒(空骨架让 `PlaceableChecker` 恒 `SlotNotFound`)。</summary>
        /// <remarks><b>判别力</b>:删掉 `rowCount &lt; 1` 那条 ⇒ 本条红。</remarks>
        [Test]
        public void test_worldBuildSlots_codec_zeroRows_isRejected()
            => AssertRejected(BuildCooked(allowedMask: 32, bakedInitial: 0, rowCount: 0), "行数");

        // ══════════════════ 辅助:自造合法头 + 可控行 ══════════════════

        /// <summary>
        /// 造一份结构完整、只让**行内值**可控的 cooked 字节
        /// (头:magic `DYJC` / fmt 1 / schema 1 / config `0xDEADBEEF` / 载荷长度)。
        /// </summary>
        private static byte[] BuildCooked(int allowedMask, int bakedInitial, int rowCount = 1)
        {
            const int headerSize = 20;
            int payloadLen = 16 + rowCount * 20;   // 4×i32 头 + 行 × 5×i32
            var bytes = new byte[headerSize + payloadLen];

            bytes[0] = (byte)'D';
            bytes[1] = (byte)'Y';
            bytes[2] = (byte)'J';
            bytes[3] = (byte)'C';
            WriteU32(bytes, 4, 1u);                  // formatVersion
            WriteU32(bytes, 8, 1u);                  // schemaVersion
            WriteU32(bytes, 12, 0xDEADBEEFu);        // configVersion
            WriteU32(bytes, 16, (uint)payloadLen);   // payloadLength

            int o = headerSize;
            WriteI32(bytes, ref o, 16);              // world_size_x
            WriteI32(bytes, ref o, 1);               // world_size_y
            WriteI32(bytes, ref o, 16);              // world_size_z
            WriteI32(bytes, ref o, rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                WriteI32(bytes, ref o, 1 + i);       // x
                WriteI32(bytes, ref o, 0);           // y
                WriteI32(bytes, ref o, 1);           // z
                WriteI32(bytes, ref o, allowedMask);
                WriteI32(bytes, ref o, bakedInitial);
            }

            return bytes;
        }

        private static void WriteU32(byte[] b, int offset, uint value)
        {
            b[offset] = (byte)(value & 0xFF);
            b[offset + 1] = (byte)((value >> 8) & 0xFF);
            b[offset + 2] = (byte)((value >> 16) & 0xFF);
            b[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static void WriteI32(byte[] b, ref int offset, int value)
        {
            WriteU32(b, offset, unchecked((uint)value));
            offset += 4;
        }

        /// <summary>断言读方**抛** `InvalidDataException`,且报文点名指定字面量判据。</summary>
        private static void AssertRejected(byte[] bytes, string expectedKeyword)
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => WorldBuildSlotsCookedCodec.Read(bytes));
            Assert.That(ex.Message.IndexOf(expectedKeyword, StringComparison.Ordinal) >= 0, Is.True,
                $"报文须点名判据「{expectedKeyword}」(否则任何 throw 都能让本夹具变绿):\n" + ex.Message);
        }
    }
}
