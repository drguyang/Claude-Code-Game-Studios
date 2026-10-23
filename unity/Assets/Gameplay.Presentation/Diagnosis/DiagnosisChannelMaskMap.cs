// 权威来源:
//   GDD design/gdd/diagnosis-system.md 规则二(三级结构:通道归 9 拥有)· §R-8.1(`channel` 字段)
//   ADR-005(通道位整数存储)· disease-simulation.md AC-21(通道位唯一定义 · 构建期查位冲突)
//   Story 003 Implementation Note 6(承 story-002 结构侧评审 S-MAJOR —— 通道同名不同物)
//
// ⚠️ **本文件 = Note 6 义务的兑现**:`Diagnosis.SignChannel`(R-8.1 的通道**序数标签** 0..5)
//   与 `Sim.Contracts.SignChannel`(AC-21 的通道**位掩码静态类** `1<<n`)同名不同物 ——
//   数值同形陷阱:本枚举 `Touch=4` ≡ 掩码 `Posture=4`;`FaceColor=0` ≡ 空掩码;
//   `History=5` 无对应位;掩码第六位 `Wound` 在本侧无序数标签。
//   ⇒ **未立映射前禁 cast / 禁当掩码位用**(Note 6 原文);本文件即那条映射 + 双向断言。
//
// ⚠️ 住前缀命名空间(受 DiagnosisBoundaryGates 扫描键约束)。
//    `Sim.Contracts.SignChannel`(位掩码)**不是** D-TREF 命中面 —— 它住 `Sim.Contracts`
//    (非 sim 内部);D-FIX 只禁 `Fix` 族,不禁本静态类。故本文件引用它是合法的。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
// ⚠️ CS0104 消歧:两侧同名 —— 本文件一律用别名,`SignChannel` 指**枚举**(R-8.1 序数)。
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;
// ⚠️ 另一侧同名物:AC-21 位掩码静态类(Sim.Contracts)。别名 ChannelMask 消歧。
using ChannelMask = DaYiJingCheng.Sim.Contracts.SignChannel;

namespace DaYiJingCheng.Gameplay.Presentation.Diagnosis
{
    /// <summary>R-8.1 通道**序数** ↔ AC-21 通道**位掩码**的映射表 + 双向断言(Note 6)。
    /// <para>序数 → 掩码位是**多对一**的合法降级(两序数不可映同一位);反向对六个体格通道**一一对应**。</para>
    /// <para>⚠️ 三处刻意的不全等(显式登记,不静默 —— 见 <see cref="AssertConsistent"/>):</para>
    /// <list type="bullet">
    /// <item><c>History</c>(序数 5)= 病史,是**非体格通道**(GDD 2026-09-14 裁定:通道外第二类证据)
    /// ⇒ 无掩码位(映 0,不入 mask)。</item>
    /// <item><c>Subjective</c>(序数 7)= 主观族,是**非体格通道**(2026-10-23 起,R3.6 豁免类)
    /// ⇒ 无掩码位(映 0,不入 mask)。**这是刻意的**:主观族不构成客观查体所见,
    /// `VitalsDto.SignChannelMask` 不得因它置位,否则 R3.6 的「客观族中性 / 查体全正常」当场破功。</item>
    /// <item><c>Wound</c>(序数 6 ↔ 掩码 <c>1&lt;&lt;5</c>)= 伤口。**2026-10-23 起 8 侧也有了序数标签** ——
    /// 此前该位只有 9 侧有(8 侧无入口),这正是 `INJ_*` 五行 `signs[]` 无从填写的根因。
    /// 它承载的是**体格所见**,故映真位、入 <see cref="PhysicalBits"/>。</item>
    /// </list></summary>
    public static class DiagnosisChannelMaskMap
    {
        /// <summary>序数 → 掩码位的映射(索引 = <see cref="SignChannel"/> 序数)。
        /// <para>体格六通道各映唯一一位;<c>History</c> / <c>Subjective</c> 映 0(非体格通道,不入 mask)。</para></summary>
        private static readonly int[] MaskByOrdinal =
        {
            ChannelMask.Complexion,   // FaceColor(0)  → 面色
            ChannelMask.Voice,        // Voice(1)      → 语声
            ChannelMask.Posture,      // Posture(2)    → 姿态
            ChannelMask.Breathing,    // Breathing(3)  → 呼吸
            ChannelMask.Palpation,    // Touch(4)      → 触感(⚠️ 序数 4 ≡ 掩码 Posture=4 同形不同义)
            0,                            // History(5)    → 无掩码位(非体格通道)
            ChannelMask.Wound,        // Wound(6)      → 伤口(2026-10-23 起 8 侧有标签)
            0,                            // Subjective(7) → 无掩码位(非体格通道)
        };

        /// <summary>体格六通道的掩码位全集(R-8.1 侧应恰好映满这六位)。</summary>
        private static readonly int[] PhysicalBits =
        {
            ChannelMask.Complexion, ChannelMask.Voice, ChannelMask.Posture,
            ChannelMask.Breathing, ChannelMask.Palpation, ChannelMask.Wound,
        };

        /// <summary>R-8.1 通道序数 → AC-21 掩码位(唯一合法转换路径;禁直接 cast)。
        /// <para><c>History</c> 返回 0(无位)。</para></summary>
        /// <param name="channel">R-8.1 通道枚举。</param>
        /// <returns>掩码位(0 = 无对应位)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">序数越界(闭集外 = 产物损坏)。</exception>
        public static int MaskOf(SignChannel channel)
        {
            int ordinal = (byte)channel;
            if (ordinal < 0 || ordinal >= MaskByOrdinal.Length)
                throw new ArgumentOutOfRangeException(nameof(channel),
                    $"通道序数 {ordinal} ∉ [0, {MaskByOrdinal.Length - 1}] —— 闭集外,拒收");
            return MaskByOrdinal[ordinal];
        }

        /// <summary>掩码位 → R-8.1 通道序数(体格六通道的反向)。0 ⇒ <c>null</c>。</summary>
        /// <param name="maskBit">AC-21 掩码位(单一位)。</param>
        /// <returns>对应序数;无对应(闭集外位 / 0)⇒ <c>null</c>。</returns>
        public static SignChannel? OrdinalOf(int maskBit)
        {
            for (int i = 0; i < MaskByOrdinal.Length; i++)
                if (MaskByOrdinal[i] != 0 && MaskByOrdinal[i] == maskBit)
                    return (SignChannel)i;
            return null;
        }

        /// <summary>
        /// <b>构建期双向断言</b>(Note 6 原文「五通道+病史恰好映满、零悬空掩码位、零重复位」;
        /// 2026-10-23 扩为六体格通道 + 两个非体格通道)。
        /// <para>逐条:</para>
        /// <list type="number">
        /// <item>体格六通道恰好映满 <see cref="PhysicalBits"/>(不多不少);</item>
        /// <item>体格位**两两互异**(零重复位 —— 否则两个通道塌成一位,静默丢通道);</item>
        /// <item>体格位 = 六个已知位(零悬空:无第六个之外的位被体格通道占用);</item>
        /// <item>反向一一对应(每个体格位恰由一个序数映得);</item>
        /// <item><b>悬空掩码位归零</b> —— 自 2026-10-23 起 <c>Wound</c> 已由 8 侧
        /// <see cref="SignChannel.Wound"/> 认领,六个体格位全部有主。⚠️ 本条**不是**「删掉旧检查」:
        /// 旧检查守的是「Wound 必须悬空」,现在守的是「**没有位再悬空**」——
        /// 将来若 9 侧再加位而 8 侧不补序数标签,本条即红。</item>
        /// <item><c>History</c> 与 <c>Subjective</c> 均映 0(两个非体格通道,显式登记 —— 不是缺陷)。</item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentException">任一不满足(构建期硬失败)。</exception>
        public static void AssertConsistent()
        {
            // ① 六体格通道恰好映满已知六位
            var physical = new HashSet<int>();
            for (int i = 0; i < PhysicalBits.Length; i++) physical.Add(PhysicalBits[i]);

            var mapped = new HashSet<int>();
            var seenOrdinals = new HashSet<int>();
            for (int ordinal = 0; ordinal < MaskByOrdinal.Length; ordinal++)
            {
                int bit = MaskByOrdinal[ordinal];
                if (bit == 0) continue;   // History / Subjective(无位)在此跳过,单独在第 ⑥ 条判
                seenOrdinals.Add(ordinal);

                // ② 零重复位:两个序数不可映同一位
                if (!mapped.Add(bit))
                    throw new ArgumentException(
                        $"Note6·通道映射 位 0x{bit:X} 被多个序数映射 —— 零重复位(两通道塌成一位 = 静默丢通道)");

                // ③ 零悬空:体格位须在已知六位集内
                if (!physical.Contains(bit))
                    throw new ArgumentException(
                        $"Note6·通道映射 序数 {ordinal} 映到未知位 0x{bit:X} —— 零悬空(体格通道不得占用非登记位)");
            }

            // ① 六体格通道恰满:映射到的位集 == 已知六位集
            if (mapped.Count != PhysicalBits.Length || !mapped.SetEquals(physical))
                throw new ArgumentException(
                    $"Note6·通道映射 体格位集 {Dump(mapped)} ≠ 已知六位 {Dump(physical)} —— " +
                    "体格通道须恰好映满(多一位 = 新造通道;少一位 = 通道丢失)");

            // ④ 反向一一对应
            foreach (int bit in PhysicalBits)
            {
                int hits = 0;
                for (int i = 0; i < MaskByOrdinal.Length; i++)
                    if (MaskByOrdinal[i] == bit) hits++;
                if (hits != 1)
                    throw new ArgumentException(
                        $"Note6·通道映射 位 0x{bit:X} 被 {hits} 个序数映得 —— 须恰一(双向一一对应)");
            }

            // ⑤ 悬空掩码位归零 —— 9 侧每加一个位,8 侧必须补一个序数标签。
            //    ⚠️ 本条取代 2026-09-17 ~ 2026-10-23 的旧检查「Wound = 唯一悬空位」。
            //    旧检查的方向是反的:它要求 Wound **必须**悬空(8 侧无入口),
            //    而那正是 INJ_* 五行 signs[] 无从填写的根因。现在 Wound 已认领,
            //    新判据是「没有任何位再悬空」—— 更严,不是更松。
            int allMaskBits = ChannelMask.Complexion | ChannelMask.Voice | ChannelMask.Posture
                            | ChannelMask.Breathing | ChannelMask.Palpation | ChannelMask.Wound;
            var orphans = new List<int>();
            for (int bitIndex = 0; bitIndex < 31; bitIndex++)
            {
                int bit = 1 << bitIndex;
                if ((allMaskBits & bit) == 0) continue;   // 闭集外的位不查(那是 9 侧扩展面)
                if (OrdinalOf(bit) == null) orphans.Add(bit);
            }
            if (orphans.Count != 0)
                throw new ArgumentException(
                    $"Note6·通道映射 掩码位 {Dump(new HashSet<int>(orphans))} 无 R-8.1 序数标签 —— " +
                    "9 侧新增通道位时 8 侧须同步补序数标签(悬空位 = 该通道的体征词无处安放," +
                    "INJ_* 五行 signs[] 全空的根因)");

            // ⑥ 两个非体格通道映 0(显式登记,不是缺陷)
            if (MaskOf(SignChannel.History) != 0)
                throw new ArgumentException(
                    "Note6·通道映射 History(非体格通道)竟映到非零位 —— 病史是通道外第二类证据," +
                    "不得借体格位(否则新造通道)");
            if (MaskOf(SignChannel.Subjective) != 0)
                throw new ArgumentException(
                    "Note6·通道映射 Subjective(非体格通道)竟映到非零位 —— 主观族不构成客观查体所见," +
                    "映非零位会让「查体全正常」(R3.6)破功");
        }

        private static string Dump(HashSet<int> bits)
        {
            var parts = new List<string>();
            foreach (int b in bits) parts.Add($"0x{b:X}");
            parts.Sort(StringComparer.Ordinal);
            return "{" + string.Join(",", parts) + "}";
        }
    }
}
