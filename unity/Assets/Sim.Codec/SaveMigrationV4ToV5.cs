// ADR-038 Amendment A —— 第三条真实迁移:v4 → v5(在头部末尾追加季 / 天气四值)。
//
// 权威来源:docs/architecture/adr-038-time-params-externalization.md §D3 + Amendment A
//   ADR-010 §七(逐版本脚本 · WorldSeed 与已发生事件逐位不变)
//   `ISaveMigration` 契约(输入/输出都是**已剥掉校验和**的完整存档字节)
//
// ── 本步做的唯一一件事 ──────────────────────────────────────────────
// 在 **v4 头部末尾**(`SaveHeaderCodec.FixedSizeV4` = 60)插入 32 字节:
// `252000 / 5 / 6000 / 100`,再把版本号写成 5。其余字节**逐位不动** ⇒ 尾部锚定不变量通过。
//
// ── 为什么写这四个值是**记录事实**而非伪造 ────────────────────────────
// 2026-11-03 用户裁定:季长 = 7 日 = 252000 tick · 一年 5 季 · 天气块 6000 tick ·
// 强度上界 100。v4 时代(同日之前)的每次写档用的都是 `5_time.json` / `5_weather.json`
// 的同一组值 ⇒ 把它们补进 v4 档就是**还原当时的事实**。今日零真实 v4 存档
// (ADR-030 R3 同族事实),该路径在实现前无输入 —— 与 `SaveMigrationV3ToV4` 同口径。

using System;
using System.Buffers.Binary;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>v4 → v5:在 v4 头部末尾追加季 / 天气四值(ADR-038 Amendment A)。</summary>
    public sealed class SaveMigrationV4ToV5 : ISaveMigration
    {
        /// <summary>迁移时写入的季长(2026-11-03 用户裁定 = 7 日)。</summary>
        public const long LegacyTicksPerSeason = 252000L;

        /// <summary>迁移时写入的一年季数(2026-11-03 裁定;与 17 的 `SEASON_MULT` 表长相等)。</summary>
        public const long LegacySeasonsPerYear = 5L;

        /// <summary>迁移时写入的天气块长(2026-11-03 裁定 = 5 分钟实时)。</summary>
        public const long LegacyWeatherBlockTicks = 6000L;

        /// <summary>迁移时写入的天气强度上界(2026-11-03 裁定)。</summary>
        public const long LegacyIntensityMax = 100L;

        /// <summary>本步能迁移的源版本。</summary>
        public uint FromVersion => 4;

        /// <summary>本步**不编辑正文**(`TD-persist-24` 甲案)。</summary>
        /// <remarks>⚠️ 插在旧头部末尾的 32 B 被**新版本的头部定长吸收**(`FixedSizeFor` 60 → 92),
        /// 故正文长度不变 —— 声明 `None`,不是 `(0, +32)`。</remarks>
        public BodyEdit BodyEdit => BodyEdit.None;

        /// <summary>目标版本(恒 = <see cref="FromVersion"/> + 1)。</summary>
        public uint ToVersion => 5;

        /// <summary>在 <see cref="SaveHeaderCodec.FixedSizeV4"/> 处插入 32 字节并把版本号写成 5。</summary>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="InvalidOperationException">长度不足 / 版本号不是 4 / magic 不符。</exception>
        public byte[] Migrate(byte[] dataWithoutChecksum)
        {
            if (dataWithoutChecksum == null) throw new ArgumentNullException(nameof(dataWithoutChecksum));

            int insertAt = SaveHeaderCodec.FixedSizeV4;
            if (dataWithoutChecksum.Length < insertAt)
                throw new InvalidOperationException(
                    $"v4→v5 迁移:存档仅 {dataWithoutChecksum.Length} 字节 < 头部定长 {insertAt}," +
                    "无法插入季 / 天气参数");

            uint magic = unchecked((uint)ReadInt32Le(dataWithoutChecksum, 0));
            if (magic != SaveHeader.Magic)
                throw new InvalidOperationException(
                    $"v4→v5 迁移:magic 0x{magic:X8} ≠ 0x{SaveHeader.Magic:X8} —— 不是本仓存档");

            uint version = unchecked((uint)ReadInt32Le(dataWithoutChecksum, SaveHeaderCodec.SaveVersionOffsetFixed));
            if (version != FromVersion)
                throw new InvalidOperationException(
                    $"v4→v5 迁移:版本号 {version} ≠ {FromVersion} —— 本步只处理 v{FromVersion}");

            int extra = SaveHeaderCodec.SeasonWeatherBytes;
            var result = new byte[dataWithoutChecksum.Length + extra];
            Array.Copy(dataWithoutChecksum, 0, result, 0, insertAt);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt, 8), LegacyTicksPerSeason);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt + 8, 8), LegacySeasonsPerYear);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt + 16, 8), LegacyWeatherBlockTicks);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt + 24, 8), LegacyIntensityMax);
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
