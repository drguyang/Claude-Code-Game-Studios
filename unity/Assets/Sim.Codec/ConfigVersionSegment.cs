// ADR-037 §② —— 存档 v3 的**配置版本段**编解码。
//
// 权威来源:docs/architecture/adr-037-save-v3-config-version-segment.md
//   §① 段位置(紧跟头部,所有既有段之前)· §② 段形状与规范形 · §④ 「未知」≠「无差异」
//
// 段形状：`count:i32 + count × { addrLen:i32, addr:utf8[addrLen], version:u32 }`
//
// ── 三条硬判据（解码侧执行，不是靠写侧自觉）──────────────────────────────
// 1. **规范形唯一**：地址按 `StringComparer.Ordinal` **严格升序**。乱序 / 重复 ⇒ 拒收。
//    理由同 ADR-034 §② 的「升序是确定性要求」：同态存档在两台机器上必须逐字节相同，
//    否则任何字节级对拍都无从成立。写侧 `Encode` 负责排序，读侧 `Decode` 负责**验**。
// 2. **`count = 0` 是合法且有意义的值** = 「本档不含逐份版本」（v2 迁上来的旧档）。
//    它不是损坏，也不是「无差异」—— 语义由消费侧 `ConfigVersionDiff.HasPerArtifactData` 承载。
// 3. **不静默容错**：负 count / addrLen ≤ 0 / 越界 / 非 UTF-8 可解 → 抛 `InvalidDataException`。
//    空集冒充「读到了」是本类最坏的失败模式（把「不知道」写成「没事」）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>存档 v3 配置版本段的编解码（纯函数，零 I/O）。</summary>
    public static class ConfigVersionSegment
    {
        /// <summary>每条目的定长部分：<c>addrLen:i32</c> + <c>version:u32</c>（不含地址字节）。</summary>
        public const int EntryFixedSize = 4 + 4;

        /// <summary>
        /// 编码一段。**按地址 Ordinal 升序**写出（与输入序无关）。
        /// </summary>
        /// <param name="items"><c>null</c> 或空集 ⇒ 写出 <c>count = 0</c>（合法语义：未知）。</param>
        /// <exception cref="ArgumentException">地址重复（重复会让「哪一张表」产生两个答案）。</exception>
        public static byte[] Encode(IReadOnlyList<ArtifactConfigVersion> items)
        {
            int count = items?.Count ?? 0;
            if (count == 0)
            {
                var empty = new CodecWriter(4);
                empty.WriteInt32LittleEndian(0);
                return empty.ToArray();
            }

            var sorted = new ArtifactConfigVersion[count];
            for (int i = 0; i < count; i++)
            {
                var it = items[i];
                // ⚠️ 地址重复不在这里「顺手去重」—— 去重会让写侧的错误静默消失。
                sorted[i] = it;
            }
            Array.Sort(sorted, (a, b) => StringComparer.Ordinal.Compare(a.Address, b.Address));
            for (int i = 1; i < sorted.Length; i++)
            {
                if (StringComparer.Ordinal.Equals(sorted[i - 1].Address, sorted[i].Address))
                    throw new ArgumentException(
                        $"配置版本段地址重复「{sorted[i].Address}」—— 同一张表只能有一个版本号",
                        nameof(items));
            }

            int total = 4;
            var encoded = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                encoded[i] = Encoding.UTF8.GetBytes(sorted[i].Address);
                if (encoded[i].Length == 0)
                    throw new ArgumentException(
                        $"配置版本段地址为空（第 {i} 条）—— 空地址的归因不可读", nameof(items));
                total += EntryFixedSize + encoded[i].Length;
            }

            var w = new CodecWriter(total);
            w.WriteInt32LittleEndian(count);
            for (int i = 0; i < count; i++)
            {
                w.WriteInt32LittleEndian(encoded[i].Length);
                w.WriteBytes(encoded[i]);
                w.WriteInt32LittleEndian(unchecked((int)sorted[i].Version));
            }
            return w.ToArray();
        }

        /// <summary>
        /// 从 <paramref name="data"/> 的 <paramref name="offset"/> 处读一段。
        /// </summary>
        /// <param name="consumed">实际消耗的字节数（含 count 前缀）—— 供调用方推进游标。</param>
        /// <exception cref="InvalidDataException">负 count / 越界 / 非升序 / 重复 / 空地址。</exception>
        public static IReadOnlyList<ArtifactConfigVersion> Decode(byte[] data, int offset, out int consumed)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset > data.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var r = new CodecReader(new ReadOnlySpan<byte>(data, offset, data.Length - offset));
            if (r.Remaining < 4)
                throw new InvalidDataException(
                    $"配置版本段缺失：偏移 {offset} 处仅剩 {r.Remaining} 字节（段头 4 字节）");

            int count = r.ReadInt32LittleEndian();
            if (count < 0)
                throw new InvalidDataException($"配置版本段 count={count} 为负 —— 已损坏");
            // 每条至少 EntryFixedSize ⇒ 分配前先按剩余字节复验（防损坏 count 触发巨额分配）
            if ((long)count * EntryFixedSize > r.Remaining)
                throw new InvalidDataException(
                    $"配置版本段 count={count} 至少需 {count * (long)EntryFixedSize} 字节，" +
                    $"仅剩 {r.Remaining} —— 已损坏");

            var items = new ArtifactConfigVersion[count];
            string prev = null;
            for (int i = 0; i < count; i++)
            {
                if (r.Remaining < EntryFixedSize)
                    throw new InvalidDataException(
                        $"配置版本段第 {i} 条头不足 {EntryFixedSize} 字节（仅剩 {r.Remaining}）—— 已损坏");

                int addrLen = r.ReadInt32LittleEndian();
                if (addrLen <= 0)
                    throw new InvalidDataException(
                        $"配置版本段第 {i} 条 addrLen={addrLen} —— 空地址或负长度，已损坏");
                if (addrLen > r.Remaining)
                    throw new InvalidDataException(
                        $"配置版本段第 {i} 条地址长 {addrLen} > 剩余 {r.Remaining} —— 已损坏");

                string address = Encoding.UTF8.GetString(r.ReadBytes(addrLen));
                uint version = unchecked((uint)r.ReadInt32LittleEndian());

                // 规范形校验：严格升序（重复一律拒收，含「相邻相等」）
                if (prev != null && StringComparer.Ordinal.Compare(prev, address) >= 0)
                    throw new InvalidDataException(
                        $"配置版本段地址非严格升序：「{prev}」→「{address}」" +
                        "（规范形唯一是 ADR-037 §② 的判据；乱序/重复说明写侧或字节已漂移）");

                items[i] = new ArtifactConfigVersion(address, version);
                prev = address;
            }

            consumed = r.Position;
            return items;
        }
    }
}
