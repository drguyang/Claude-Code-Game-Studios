// ADR-010 §三 义务 13 —— 第五条真实迁移:v6 → v7(在配置版本段之后插入**空**分册态段)。
//
// 权威来源:
//   docs/architecture/adr-010-persistence-save-format.md §一 / §三 义务 13 · §七(逐版本脚本,
//     WorldSeed 与已发生事件逐位不变)
//   docs/architecture/adr-037-save-v3-config-version-segment.md §①(「插在头部之后」的理由)
//   `ISaveMigration` 契约(输入 / 输出都是**已剥掉校验和**的完整存档字节)
//
// ── 本步做的唯一一件事 ──────────────────────────────────────────────
// 在**配置版本段之后**(= 事件段之前)插入 8 字节的空段(`volumeCount = 0` / `pageCount = 0`),
// 再把版本号写成 7。其余字节**逐位不动** ⇒ 尾部锚定不变量通过。
//
// ── 为什么插在这里而不是末尾 ───────────────────────────────────────────
// `SaveMigrationChain.TailBody` 按「从末尾算的字节」比对正文 —— 尾部追加会让**每一条**
// 既有迁移的断言失效(ADR-034 §⑥ⓑ / ADR-037 §① 都踩过这个)。插在头部之后,
// 尾部(事件段 | 折叠段 | 载荷段)字节完全不变。
//
// ── 为什么插在**配置版本段之前**(2026-11-08 实测改判,原稿写「配置版本段之后」)──
// `SaveMigrationChain` 的「已发生事件原样保留」断言按**末尾等长**取窗(`TailBody`)。
// 实测:插在配置版本段**之后**时,末尾窗口必然横跨插入点 ⇒ 该不变式**在几何上不可能成立**
// (实测报「迁移脚本 FromVersion=6 改变了事件正文」,而正文一个字节都没动)。
// 插在**头部之后、配置版本段之前**时,插入点落在「新版本的头部区」内 ⇒ 尾部窗口完全不重叠,
// 不变式自然成立 —— 与 ADR-037 的 V2ToV3 是同一个位置。
// ⇒ v7 段序 = `头部 | 分册态段 | 配置版本段 | 事件段 | 折叠段 | 载荷段`。
//
// ── 为什么写空段而不是「补一个估计值」 ────────────────────────────────
// v6 及更早的分册态**从未被记录过**。补任何页码 / tick 都是**伪造**,而伪造的后果是
// 玩家第一次读档就看到一个「好像上次翻到了第 3 页」的状态 —— 它没发生过。
// ⇒ `count = 0` = 空分册(合法语义,新世界本来就这样),不是「未知」也不是「损坏」。
// 分册正文本来就不在本段里(读时从病例流投影,F-39.2),所以**没有信息丢失**。

using System;
using System.Buffers.Binary;
using System.IO;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>v6 → v7:在配置版本段之后插入空分册态段(ADR-010 义务 13)。</summary>
    public sealed class SaveMigrationV6ToV7 : ISaveMigration
    {
        /// <summary>本步能迁移的源版本。</summary>
        public uint FromVersion => 6;

        /// <summary>目标版本(恒 = <see cref="FromVersion"/> + 1)。</summary>
        public uint ToVersion => 7;

        /// <summary>
        /// 在配置版本段之后插入 <see cref="CasebookSegment.EmptySize"/> 字节并把版本号写成 7。
        /// </summary>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="InvalidOperationException">长度不足 / 版本号不是 6 / magic 不符 / 配置版本段不可解。</exception>
        public byte[] Migrate(byte[] dataWithoutChecksum)
        {
            if (dataWithoutChecksum == null) throw new ArgumentNullException(nameof(dataWithoutChecksum));

            uint magic = unchecked((uint)ReadInt32Le(dataWithoutChecksum, 0));
            if (magic != SaveHeader.Magic)
                throw new InvalidOperationException(
                    $"v6→v7 迁移:magic 0x{magic:X8} ≠ 0x{SaveHeader.Magic:X8} —— 不是本仓存档");

            uint version = unchecked((uint)ReadInt32Le(dataWithoutChecksum, SaveHeaderCodec.SaveVersionOffsetFixed));
            if (version != FromVersion)
                throw new InvalidOperationException(
                    $"v6→v7 迁移:版本号 {version} ≠ {FromVersion} —— 本步只处理 v{FromVersion}");

            int insertAt = SaveHeaderCodec.FixedSizeFor(FromVersion);   // 100 = v6 头部末尾
            if (dataWithoutChecksum.Length < insertAt)
                throw new InvalidOperationException(
                    $"v6→v7 迁移:存档仅 {dataWithoutChecksum.Length} 字节 < 头部定长 {insertAt}");

            if (insertAt > dataWithoutChecksum.Length)
                throw new InvalidOperationException(
                    $"v6→v7 迁移:插入点 {insertAt} 超出存档长度 {dataWithoutChecksum.Length}");

            int extra = CasebookSegment.EmptySize;
            var result = new byte[dataWithoutChecksum.Length + extra];
            Array.Copy(dataWithoutChecksum, 0, result, 0, insertAt);
            // 两个 count 前缀都写 0 = 空分册(见文件头「为什么写空段」)
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(result, insertAt, 4), 0);
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(result, insertAt + 4, 4), 0);
            Array.Copy(dataWithoutChecksum, insertAt, result, insertAt + extra,
                       dataWithoutChecksum.Length - insertAt);

            WriteInt32Le(result, SaveHeaderCodec.SaveVersionOffsetFixed, unchecked((int)ToVersion));
            return result;
        }

        private static int ReadInt32Le(byte[] data, int offset)
            => data[offset]
               | (data[offset + 1] << 8)
               | (data[offset + 2] << 16)
               | (data[offset + 3] << 24);

        private static void WriteInt32Le(byte[] data, int offset, int value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}
