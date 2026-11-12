// ADR-042 —— 第四条真实迁移:v5 → v6(在头部末尾追加落笔时刻)。
//
// 权威来源:docs/architecture/adr-042-save-v6-saved-at.md
//   ADR-010 §七(逐版本脚本 · WorldSeed 与已发生事件逐位不变)
//   `ISaveMigration` 契约(输入/输出都是**已剥掉校验和**的完整存档字节)
//
// ── 本步做的唯一一件事 ──────────────────────────────────────────────
// 在 **v5 头部末尾**(`SaveHeaderCodec.FixedSizeV5` = 92)插入 8 字节 `0`,
// 再把版本号写成 6。其余字节**逐位不动** ⇒ 尾部锚定不变量通过。
//
// ── 为什么写 0 而不是「补一个估计值」 ─────────────────────────────────
// 落笔时刻**从未被记录过**(v5 及更早的头部没有这个字段,文件系统时间也不是
// 落笔时刻 —— 拷贝 / 备份 / 恢复都会改它)。补任何值都是**伪造**,
// 而伪造的后果是册子上每一行都标着一个「看起来发生过、其实没发生过」的时刻。
// ⇒ 0 = 未知;呈现层对 0 一律不显示(ADR-042 §D4,与 ADR-037「未知 ≠ 无差异」同纪律)。

using System;
using System.Buffers.Binary;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>v5 → v6:在 v5 头部末尾追加落笔时刻的「未知」占位(ADR-042)。</summary>
    public sealed class SaveMigrationV5ToV6 : ISaveMigration
    {
        /// <summary>迁移时写入的落笔时刻 —— <b>0 = 未知</b>(不伪造,见文件头)。</summary>
        public const long LegacySavedAtUtcTicks = 0L;

        /// <summary>本步能迁移的源版本。</summary>
        public uint FromVersion => 5;

        /// <summary>本步**不编辑正文**(`TD-persist-24` 甲案)。</summary>
        /// <remarks>⚠️ 插在旧头部末尾的 8 B 被**新版本的头部定长吸收**(`FixedSizeFor` 92 → 100),
        /// 故正文长度不变 —— 声明 `None`,不是 `(0, +8)`。</remarks>
        public BodyEdit BodyEdit => BodyEdit.None;

        /// <summary>目标版本(恒 = <see cref="FromVersion"/> + 1)。</summary>
        public uint ToVersion => 6;

        /// <summary>在 <see cref="SaveHeaderCodec.FixedSizeV5"/> 处插入 8 字节并把版本号写成 6。</summary>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="InvalidOperationException">长度不足 / 版本号不是 5 / magic 不符。</exception>
        public byte[] Migrate(byte[] dataWithoutChecksum)
        {
            if (dataWithoutChecksum == null) throw new ArgumentNullException(nameof(dataWithoutChecksum));

            int insertAt = SaveHeaderCodec.FixedSizeV5;
            if (dataWithoutChecksum.Length < insertAt)
                throw new InvalidOperationException(
                    $"v5→v6 迁移:存档仅 {dataWithoutChecksum.Length} 字节 < 头部定长 {insertAt}," +
                    "无法插入落笔时刻");

            uint magic = unchecked((uint)ReadInt32Le(dataWithoutChecksum, 0));
            if (magic != SaveHeader.Magic)
                throw new InvalidOperationException(
                    $"v5→v6 迁移:magic 0x{magic:X8} ≠ 0x{SaveHeader.Magic:X8} —— 不是本仓存档");

            uint version = unchecked((uint)ReadInt32Le(dataWithoutChecksum, SaveHeaderCodec.SaveVersionOffsetFixed));
            if (version != FromVersion)
                throw new InvalidOperationException(
                    $"v5→v6 迁移:版本号 {version} ≠ {FromVersion} —— 本步只处理 v{FromVersion}");

            int extra = SaveHeaderCodec.SavedAtBytes;
            var result = new byte[dataWithoutChecksum.Length + extra];
            Array.Copy(dataWithoutChecksum, 0, result, 0, insertAt);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt, 8), LegacySavedAtUtcTicks);
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
