// ADR-038 §D3 —— 本仓**第二条**真实迁移:v3 → v4(在头部末尾插入三个时间参数)。
//
// 权威来源:docs/architecture/adr-038-time-params-externalization.md §D3
//   ADR-010 §七(逐版本脚本 · WorldSeed 与已发生事件逐位不变)
//   `ISaveMigration` 契约(输入/输出都是**已剥掉校验和**的完整存档字节)
//   `ISaveMigration` 文件头的预言:「下一次真实脚本的落点 = `CurrentSaveVersion`
//   第一次升到 4 的那一轮(届时 v3→v4 有真输入)」—— 本轮即该轮。
//
// ── 本步做的唯一一件事 ──────────────────────────────────────────────
// 在**旧头部定长之后**(`SaveHeaderCodec.FixedSize` = 36,即 v3 头部末尾、
// 配置版本段之前)插入 24 字节:`36000 / 27000 / 9000`,再把版本号写成 4。
// 其余字节**逐位不动** ⇒ `SaveMigrationChain.TailBody` 的尾部锚定不变量通过。
//
// ── 为什么写 36000 / 27000 / 9000 是**记录事实**而非伪造 ─────────────
// 本仓历史上只存在过两个日长:`480`(占位,**从未出货**)与 `36000`(2026-11-03 用户裁定)。
// 今日零真实存档(`ADR-030 R3` 同族事实)⇒ 任何 v3 档都只可能用 36000 写下。
// 写「未知」再在运行期补算反而更坏:那会引入第三态,而该路径今日不可达。
//
// ── 为什么插入点恒为 `FixedSize` ─────────────────────────────────────
// v3 的头部仍为 36 字节(`FixedSize` **没有**改);v4 多出的 24 B 是本步插进来的。
// 链工作在**已剥掉校验和**的形态 ⇒ 这里绝不出现「含校验和的 68 / 92」那类数 ——
// 用错会把插入点推到正文内部,而那只会在**读档时**以事件段解析失败的形式暴露。

using System;
using System.Buffers.Binary;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>v3 → v4:在旧头部末尾插入时间三参数(ADR-038 §D3)。</summary>
    public sealed class SaveMigrationV3ToV4 : ISaveMigration
    {
        /// <summary>迁移时写入的日长(**本仓历史上唯一可能的值**,见文件头)。</summary>
        public const long LegacyTicksPerDay = 36000L;

        /// <summary>迁移时写入的夜起点。</summary>
        public const long LegacyNightStart = 27000L;

        /// <summary>迁移时写入的夜长。</summary>
        public const long LegacyNightSpan = 9000L;

        /// <summary>本步能迁移的源版本。</summary>
        public uint FromVersion => 3;

        /// <summary>目标版本(恒 = <see cref="FromVersion"/> + 1,链会自检)。</summary>
        public uint ToVersion => 4;

        /// <summary>
        /// 在 <see cref="SaveHeaderCodec.FixedSize"/> 处插入 24 字节并把版本号写成 4。
        /// </summary>
        /// <param name="dataWithoutChecksum">v3 存档(**不含**校验和槽)。</param>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 长度不足 / 版本号不是 3 / magic 不符 —— 一律抛,不得「返回原字节当作成功」。
        /// </exception>
        public byte[] Migrate(byte[] dataWithoutChecksum)
        {
            if (dataWithoutChecksum == null) throw new ArgumentNullException(nameof(dataWithoutChecksum));

            int insertAt = SaveHeaderCodec.FixedSize;
            if (dataWithoutChecksum.Length < insertAt)
                throw new InvalidOperationException(
                    $"v3→v4 迁移:存档仅 {dataWithoutChecksum.Length} 字节 < 头部定长 {insertAt}," +
                    "无法插入时间参数");

            uint magic = unchecked((uint)ReadInt32Le(dataWithoutChecksum, 0));
            if (magic != SaveHeader.Magic)
                throw new InvalidOperationException(
                    $"v3→v4 迁移:magic 0x{magic:X8} ≠ 0x{SaveHeader.Magic:X8} —— 不是本仓存档");

            uint version = unchecked((uint)ReadInt32Le(dataWithoutChecksum, SaveHeaderCodec.SaveVersionOffsetFixed));
            if (version != FromVersion)
                throw new InvalidOperationException(
                    $"v3→v4 迁移:版本号 {version} ≠ {FromVersion} —— 本步只处理 v{FromVersion}");

            int extra = SaveHeaderCodec.TimeParamsBytes;
            var result = new byte[dataWithoutChecksum.Length + extra];
            Array.Copy(dataWithoutChecksum, 0, result, 0, insertAt);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt, 8), LegacyTicksPerDay);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt + 8, 8), LegacyNightStart);
            BinaryPrimitives.WriteInt64LittleEndian(
                new Span<byte>(result, insertAt + 16, 8), LegacyNightSpan);
            Array.Copy(dataWithoutChecksum, insertAt, result, insertAt + extra,
                       dataWithoutChecksum.Length - insertAt);

            // 版本号写成 4(链会读这个位置自检「每级真的把版本号写成 v+1」)
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
