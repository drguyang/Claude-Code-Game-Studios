// ADR-037 §⑤ —— 本仓**第一条**真实迁移:v2 → v3(插入空的配置版本段)。
//
// 权威来源:docs/architecture/adr-037-save-v3-config-version-segment.md §⑤
//   ADR-010 §七(逐版本脚本 · WorldSeed 与已发生事件逐位不变)
//   `ISaveMigration` 契约(输入/输出都是**已剥掉校验和**的完整存档字节)
//   `ISaveMigration` 文件头的预言:「下一次真实脚本的落点 = `CurrentSaveVersion`
//   第一次升到 3 的那一轮(届时 v2→v3 有真输入)」—— 本轮即该轮。
//
// ── 本步做的唯一一件事 ──────────────────────────────────────────────
// 在**头部定长之后**(`SaveHeaderCodec.FixedSize`)插入 4 字节 `count = 0`,
// 再把版本号写成 3。其余字节**逐位不动**。
//
// ── 为什么插入点恒为 `FixedSize` ─────────────────────────────────────
// 头部是定长的(`magic + saveVersion + worldSeed + configVersion + tick + snapshotOffset`),
// 与版本无关;正文从 `FixedSize` 开始。链工作在**已剥掉校验和**的形态,
// 所以「含校验和的 68 字节」那个数在本类里**不出现** —— 用错会把插入点推到正文内部,
// 而那只会在**读档时**以事件段解析失败的形式暴露(反证轮 M-V3 钉的就是这一处)。
//
// ── 为什么写 `count = 0` 而不是补算 ────────────────────────────────
// v2 档**从未记录**逐份版本,信息不存在(与 ADR-034 §⑥ 拒绝 v1→v2 同型)。
// 伪造一份「按当前产物算」的逐份版本更坏:它会让**每一次读旧档**都被报成「配置完全一致」。
// 空段的正确语义 = **未知**(ADR-037 §④),消费侧据此报「本档早于 v3,无法归因」。

using System;
using System.IO;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>v2 → v3:插入空配置版本段(ADR-037 §⑤)。</summary>
    public sealed class SaveMigrationV2ToV3 : ISaveMigration
    {
        /// <summary>本步能迁移的源版本。</summary>
        public uint FromVersion => 2;

        /// <summary>本步在正文**开头**插入 4 字节(<c>count = 0</c> 的配置版本段;`TD-persist-24` 甲案)。</summary>
        public BodyEdit BodyEdit => new BodyEdit(0, 4);

        /// <summary>目标版本(恒 = <see cref="FromVersion"/> + 1,链会自检)。</summary>
        public uint ToVersion => 3;

        /// <summary>
        /// 在 <see cref="SaveHeaderCodec.FixedSize"/> 处插入 <c>count = 0</c> 并把版本号写成 3。
        /// </summary>
        /// <param name="dataWithoutChecksum">v2 存档(**不含**校验和槽)。</param>
        /// <exception cref="ArgumentNullException">入参为 null。</exception>
        /// <exception cref="InvalidOperationException">
        /// 长度不足头部定长 / 版本号不是 2 / 头部 magic 不符 —— 一律抛,不得「返回原字节当作成功」。
        /// </exception>
        public byte[] Migrate(byte[] dataWithoutChecksum)
        {
            if (dataWithoutChecksum == null) throw new ArgumentNullException(nameof(dataWithoutChecksum));

            int fixedSize = SaveHeaderCodec.FixedSize;
            if (dataWithoutChecksum.Length < fixedSize)
                throw new InvalidOperationException(
                    $"v2→v3 迁移:存档仅 {dataWithoutChecksum.Length} 字节 < 头部定长 {fixedSize}," +
                    "无法插入配置版本段");

            uint magic = unchecked((uint)ReadInt32Le(dataWithoutChecksum, 0));
            if (magic != SaveHeader.Magic)
                throw new InvalidOperationException(
                    $"v2→v3 迁移:magic 0x{magic:X8} ≠ 0x{SaveHeader.Magic:X8} —— 不是本仓存档");

            uint version = unchecked((uint)ReadInt32Le(dataWithoutChecksum, SaveHeaderCodec.SaveVersionOffsetFixed));
            if (version != FromVersion)
                throw new InvalidOperationException(
                    $"v2→v3 迁移:版本号 {version} ≠ {FromVersion} —— 本步只处理 v{FromVersion}");

            // ① 插入 4 字节 count=0(头部之后、正文之前)
            var result = new byte[dataWithoutChecksum.Length + 4];
            Array.Copy(dataWithoutChecksum, 0, result, 0, fixedSize);
            WriteInt32Le(result, fixedSize, 0);
            Array.Copy(dataWithoutChecksum, fixedSize, result, fixedSize + 4,
                       dataWithoutChecksum.Length - fixedSize);

            // ② 版本号写成 3(链会读这个位置自检「每级真的把版本号写成 v+1」)
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
