// ADR-010 §一 —— 存档头部编解码(写形 + 读回)。
//
// 权威来源:
//   ADR-010 §一 —— 头部字段序(黄金夹具钉死):
//     magic "DYJQ"(4B)· SHA256(32B)· SaveVersion(u32)· WorldSeed(u64)
//     · ConfigVersion(u32)· tick(i64)· SnapshotOffset(u64)
//   ADR-010 §四 —— 校验和覆盖 checksum 字段之后的全部字节
//   ADR-007 §二 —— WorldSeed 归存档头
//   ADR-014 §五 —— ConfigVersion 进存档头
//
// 本文件补的缺口(S19-1 判据 ⑦,2026-10-24 实测):
//   `SaveHeader` 此前**只有构造器**(写形),全仓零读回方法
//   (`grep -rn "SaveHeader" unity/Assets --include=*.cs | grep -v Tests/` ⇒ 6 行,零读回)
//   ⇒ `Load` 的「头部解析」段无可调之物。本类即该段。
//
// ⚠️ 与 `SaveService.AddChecksum` 的分工:
//   本类**只组头部字节**,不含校验和计算(checksum 覆盖范围是「其后全部字节」,
//   须等正文排完才可算)⇒ 调用序 = WriteBody → AddChecksum → (落盘) → Read。

using System;
using System.IO;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档头部编解码。显式小端,字段序由 ADR-010 §一 钉死。</summary>
    public static class SaveHeaderCodec
    {
        /// <summary>
        /// 头部定长(不含校验和,因为校验和由 <see cref="SaveService.AddChecksum"/> 插入):
        /// magic 4 + saveVersion 4 + worldSeed 8 + configVersion 4 + tick 8 + snapshotOffset 8。
        /// </summary>
        public const int FixedSize = 4 + 4 + 8 + 4 + 8 + 8;

        /// <summary>头部含校验和的完整长度 = <see cref="FixedSize"/> + 32。</summary>
        public const int HeaderSize = FixedSize + 32;

        /// <summary>
        /// <b>不含校验和</b>形态下 `SaveVersion` 的字节偏移 = 紧跟 magic 之后。
        /// <para>🔁 2026-10-25 `AC-7a-14` 迁移链新增:链的每一级都须**真的**把版本号写成
        /// `v+1` —— 否则下一级查表会 miss,而 miss 的表现是「迁移失败」,
        /// 与「忘了升版本号」这个**真实缺陷**在症状上不可分。链在每级之后读这个位置自检。</para>
        /// </summary>
        public const int SaveVersionOffsetFixed = 4;

        /// <summary>
        /// <b>不含校验和</b>形态下 `WorldSeed` 的字节偏移 = magic(4) + saveVersion(4)。
        /// <para>🔁 2026-10-25 `AC-7a-14` 迁移链新增:迁移链要在**迁移前后各读一次**
        /// `WorldSeed` 并断言逐位不变,而这一读**不能经 <see cref="Read"/>** ——
        /// 那个方法吃的是完整存档,且一旦头部字段序在某个历史版本里不同就会解错。
        /// 迁移链工作在**已剥掉校验和**的形态,按**固定字节位置**读才是版本无关的。</para>
        /// <para>⚠️ 这个位置是 ADR-010 §一 的黄金夹具面 —— 改头部字段序须同步改本常量,
        /// 且改字段序本身就是一次版本升级。</para>
        /// </summary>
        public const int WorldSeedOffsetFixed = 4 + 4;

        /// <summary>`WorldSeed` 字段的字节长度(u64)。</summary>
        public const int WorldSeedSize = 8;

        /// <summary>
        /// 组头部字节(**不含校验和槽**,形如 `AddChecksum` 的入参):
        /// magic(4) + saveVersion(4) + worldSeed(8) + configVersion(4) + tick(8) + snapshotOffset(8)。
        /// <para>调用方把它与正文拼接后交 <see cref="SaveService.AddChecksum"/>。</para>
        /// </summary>
        public static byte[] WriteWithoutChecksum(ulong worldSeed, uint configVersion,
                                                  long tick, ulong snapshotOffset)
        {
            var w = new CodecWriter(FixedSize);
            w.WriteInt32LittleEndian(unchecked((int)SaveHeader.Magic));
            w.WriteInt32LittleEndian(unchecked((int)SaveHeader.CurrentSaveVersion));
            w.WriteUInt64LittleEndian(worldSeed);
            w.WriteInt32LittleEndian(unchecked((int)configVersion));
            w.WriteInt64LittleEndian(tick);
            w.WriteUInt64LittleEndian(snapshotOffset);
            return w.ToArray();
        }

        /// <summary>
        /// 从完整存档字节(含校验和槽)读回头部。
        /// <para><b>严格</b>:magic 不符 / 长度不足 / 尾部无法归入已知段 ⇒ 抛
        /// <see cref="InvalidDataException"/>。**禁返回 default** ——
        /// 静默返回零值头会让「读了个坏档」伪装成「读了个空档」。</para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> 为 null。</exception>
        /// <exception cref="InvalidDataException">magic 不符或长度不足。</exception>
        public static SaveHeader Read(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < HeaderSize)
                throw new InvalidDataException(
                    $"存档过短:{data.Length} 字节 < 头部 {HeaderSize} 字节");

            var r = new CodecReader(data);
            uint magic = unchecked((uint)r.ReadInt32LittleEndian());
            if (magic != SaveHeader.Magic)
                throw new InvalidDataException(
                    $"存档 magic 不符:0x{magic:X8} ≠ 0x{SaveHeader.Magic:X8}(\"DYJQ\")");

            // 校验和槽(32B)—— 本方法只跳过,验证归 SaveService.ValidateChecksum
            r.ReadBytes(32);

            uint saveVersion = unchecked((uint)r.ReadInt32LittleEndian());
            ulong worldSeed = r.ReadUInt64LittleEndian();
            uint configVersion = unchecked((uint)r.ReadInt32LittleEndian());
            long tick = r.ReadInt64LittleEndian();
            ulong snapshotOffset = r.ReadUInt64LittleEndian();

            // ⚠️ 校验和槽不进 SaveHeader 的构造器(它不接受),逐字段回填
            return new SaveHeader(worldSeed, configVersion, tick, snapshotOffset)
            {
                SaveVersion = saveVersion,
            };
        }

        /// <summary>只读 magic,不做其余解析(装配层快速判「这个文件是不是我们的存档」)。</summary>
        public static bool HasValidMagic(byte[] data)
            => data != null && data.Length >= 4
               && unchecked((uint)BitConverter.ToInt32(data, 0)) == SaveHeader.Magic;
    }
}
