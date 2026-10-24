// 权威来源:
//   ADR-014 §五 —— 「`ConfigVersion` 由源数据集内容哈希派生(对 `assets/data/` 源文本集求哈希
//     后截断 u32)」⇒ **全局单一值**,不是逐产物各一份
//   ADR-010 §七 —— 存档头 `ConfigVersion` 与当前产物比对;**不匹配非致命**,仅记录
//   AC-51-D7(`design/gdd/telemetry-analytics.md:621`,BLOCKING)
//   `TD-epic-01-f`(本件兑现其解除前置 ①「源头真值来源落地」)
//
// ═══════════════════════════════════════════════════════════════════════════════
// 为什么需要本类(2026-10-24 `TD-epic-01-f` 修复轮)
// ═══════════════════════════════════════════════════════════════════════════════
// **实测的分歧**:22 个 cooked 产物**各自**按「自己的源」派生 `ConfigVersion`
// (`DiseaseRegistryBaker.cs:88-98` 明确论证了为何不能把别的产物的源塞进来 ——
//  那会造成「版本号对不上本产物字节」)。实测 6 个产物头 offset 12:
//   `ed98bda4` / `c63a53de` / `5050f947` / `ca75d16a` / `ffc55f35` / `e5bbf87a` —— **各不相同**。
//
// 而 ADR-014 §五 的字面是「对 `assets/data/` **源文本集**求哈希」= **一个值**。
// 存档头只能写一个 u32 ⇒ **必须先有「全局组合值」这个中间层**。
//
// **用户 2026-10-24 裁定**:取**全局组合值**(合 ADR 字面)—— 而不是「取单一主数据集」
// (那会让其余 21 份数据变了存档头不变 ⇒ AC-51-D7 只盖住 1/22,仍是大半假绿)。
//
// ═══════════════════════════════════════════════════════════════════════════════
// 组合口径(逐字)
// ═══════════════════════════════════════════════════════════════════════════════
// 取**全部** `DataCoreAddresses`(22 条),按 **address 的 Ordinal 排序**,
// 依次喂 `(address, configVersion 的 4 字节 little-endian)` 进 FNV-1a 32 ——
// 与 `ConfigVersionUtility.DeriveConfigVersion` **同一算法、同一偏移/质数**,
// 只是输入从「文件名 + 文本内容」换成「address + 产物头的 u32 字节」。
//
// ⚠️ **排序是判据的一部分**:`DataCoreAddresses` 的数组序是**装载序**,会随接线轮次变化
//    (`:180-232` 的注释记了 7 → 8 → 9 → 11 → 15 → 16 → 19 → 20 → 21 → 22 的历次追加)。
//    若按数组序组合,「接线顺序调整」会改变全局值而**数据一字未动** —— 那是假变更。
//    ⇒ 按 address 的 Ordinal 排序,使组合值只依赖「有哪些产物 + 各自版本」。
//
// ⚠️ **刻意不读产物载荷**:只读 20 字节头里的 offset 12。理由 = 成本(22 份载荷全读一遍
//    在启动路径上不可接受),且逐产物的 `ConfigVersion` **已经是**其载荷内容的函数
//    (每个烘焙器用自己那份源算的)⇒ 组合它们等价于覆盖全部源,无需重读字节。
//
// ⚠️ **本类不做 I/O**:字节由调用方(边界装配层)取好传入 —— 与 `ConfigVersionGate`
//    只收两个 u32 而同一条边界纪律(51 只负责比对与标注,不负责取数)。
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation
{
    /// <summary>
    /// 把**全部** data-core 产物的逐份 <c>ConfigVersion</c> 组合成**一个**全局值
    /// (ADR-014 §五 的「源文本集」口径 · 用户 2026-10-24 裁定)。
    /// <para><b>纯函数</b>:同输入同输出,零 I/O、零墙钟、零静态可变状态。</para>
    /// <para>⚠️ 本类是 <c>AC-51-D7</c> 的**源头真值提供者** —— 存档头写它、51 拿它比对。
    /// 它不负责「比对」也不负责「标注」(那是 <c>Telemetry.ConfigVersionGate</c>)。</para>
    /// </summary>
    public static class DataCoreConfigVersion
    {
        /// <summary>FNV-1a 32 的偏移基(与 <c>ConfigVersionUtility</c> 同值 —— 同一算法族)。</summary>
        private const uint FnvOffset = 2166136261u;

        /// <summary>FNV-1a 32 的质数(同上)。</summary>
        private const uint FnvPrime = 16777619u;

        /// <summary>
        /// 从「address → 该产物 20 字节头」的组合派生全局 <c>ConfigVersion</c>。
        /// <para><b>排序 = address 的 <see cref="StringComparer.Ordinal"/></b>,
        /// 与传入字典的枚举序无关(见文件头「排序是判据的一部分」)。</para>
        /// </summary>
        /// <param name="cookedHeaders">
        /// 每个产物 address → 该产物的**头部字节**(至少 <see cref="HeaderSize"/> 字节)。
        /// 只需头 20 字节,不需全量载荷 —— 调用方**不必**读完整个产物。
        /// </param>
        /// <returns>全局组合 <c>ConfigVersion</c>(u32)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="cookedHeaders"/> 为 null。</exception>
        /// <exception cref="ArgumentException">
        /// 空集,或某个 value 短于 <see cref="HeaderSize"/> 字节,或魔数不符。
        /// <para>⚠️ **空集 = 抛,不是返回 0** —— 返回 0 会让「一个产物都没装载」与
        /// 「装载了但版本恰为 0」不可分,而前者是装配缺陷(ADR-014 §五 E-13 启动期硬失败面)。</para>
        /// </exception>
        public static uint Combine(IReadOnlyDictionary<string, byte[]> cookedHeaders)
        {
            if (cookedHeaders == null) throw new ArgumentNullException(nameof(cookedHeaders));
            if (cookedHeaders.Count == 0)
                throw new ArgumentException(
                    "产物头集合为空 —— 全局 ConfigVersion 无从派生" +
                    "(空集不等于「版本 0」:前者是装配缺陷,ADR-014 §五 E-13 启动期硬失败面)",
                    nameof(cookedHeaders));

            // 排序键与喂入键都是 address 本身(Ordinal)⇒ 组合值只依赖
            // 「有哪些产物 + 各自版本」,与接线顺序 / 字典枚举序无关。
            var addresses = new List<string>(cookedHeaders.Keys);
            addresses.Sort(StringComparer.Ordinal);

            uint hash = FnvOffset;
            foreach (string address in addresses)
            {
                byte[] header = cookedHeaders[address];
                if (header == null)
                    throw new ArgumentException(
                        $"产物头为 null:address = '{address}'", nameof(cookedHeaders));
                if (header.Length < HeaderSize)
                    throw new ArgumentException(
                        $"产物头短于 {HeaderSize} 字节:address = '{address}',实长 = {header.Length}" +
                        "(头部布局见 Sim.Contracts/CookedFormat.cs)",
                        nameof(cookedHeaders));

                VerifyMagic(address, header);

                // 喂 address(UTF-8)+ 0x00 分隔 + 该产物的 configVersion 四字节 LE
                // —— 与 ConfigVersionUtility 的「文件名 + 0x00 + 内容」同形。
                hash = Fnv1a(hash, address);
                hash = Fnv1aByte(hash, 0x00);
                hash = Fnv1aByte(hash, header[ConfigVersionOffset]);
                hash = Fnv1aByte(hash, header[ConfigVersionOffset + 1]);
                hash = Fnv1aByte(hash, header[ConfigVersionOffset + 2]);
                hash = Fnv1aByte(hash, header[ConfigVersionOffset + 3]);
            }
            return hash;
        }

        /// <summary>
        /// 从**单个**产物头里取出该产物自己的 <c>ConfigVersion</c>(offset 12,u32 LE)。
        /// <para>供装配层逐份取数用;组合走 <see cref="Combine"/>。</para>
        /// </summary>
        /// <param name="header">产物头部字节(至少 <see cref="HeaderSize"/>)。</param>
        /// <returns>该产物自己的 <c>ConfigVersion</c>。</returns>
        /// <exception cref="ArgumentException">长度不足或魔数不符。</exception>
        public static uint ReadProductConfigVersion(byte[] header)
        {
            if (header == null)
                throw new ArgumentException("产物头为 null", nameof(header));
            if (header.Length < HeaderSize)
                throw new ArgumentException(
                    $"产物头短于 {HeaderSize} 字节,实长 = {header.Length}", nameof(header));
            VerifyMagic("<单个产物>", header);
            return (uint)(header[ConfigVersionOffset]
                        | (header[ConfigVersionOffset + 1] << 8)
                        | (header[ConfigVersionOffset + 2] << 16)
                        | (header[ConfigVersionOffset + 3] << 24));
        }

        /// <summary>头部固定字节数(与 <c>CookedFormat.HeaderSize</c> 同值)。
        /// <para>⚠️ 本类**不引用** <c>Sim.Contracts/CookedFormat.cs</c> 的常量? —— 引得到
        /// (`Gameplay.Presentation` 已引 `Sim.Contracts`),此处复刻只为让本文件自足可读;
        /// 两处同值由 `data_core_config_version_test` 钉住(改一处不改另一处即红)。</para></summary>
        public const int HeaderSize = 20;

        /// <summary><c>configVersion</c> 在头部的偏移(<c>CookedFormat</c> 逐字:offset 12)。</summary>
        public const int ConfigVersionOffset = 12;

        /// <summary>产物魔数(<c>CookedFormat.Magic</c> 逐字)。</summary>
        private const string Magic = "DYJC";

        private static void VerifyMagic(string address, byte[] header)
        {
            for (int i = 0; i < 4; i++)
            {
                if (header[i] != (byte)Magic[i])
                    throw new ArgumentException(
                        $"产物魔数不符:address = '{address}',实得 " +
                        $"{header[0]:X2}{header[1]:X2}{header[2]:X2}{header[3]:X2},期望 44594A43(\"DYJC\")",
                        nameof(header));
            }
        }

        private static uint Fnv1a(uint hash, string text)
        {
            // UTF-8 逐字节喂入 —— 不用 Encoding.UTF8.GetBytes 以避开一次分配。
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c < 0x80)
                {
                    hash = Fnv1aByte(hash, (byte)c);
                }
                else
                {
                    // 非 ASCII 走标准 UTF-8 编码(产物 address 今日全 ASCII,
                    // 但不得因此假定 —— 非 ASCII 走通用路径保证确定性)。
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text.Substring(i, 1));
                    for (int k = 0; k < bytes.Length; k++) hash = Fnv1aByte(hash, bytes[k]);
                }
            }
            return hash;
        }

        private static uint Fnv1aByte(uint hash, byte b)
        {
            unchecked
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return hash;
        }
    }
}
