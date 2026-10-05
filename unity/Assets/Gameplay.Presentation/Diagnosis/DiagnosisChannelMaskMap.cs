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
    /// <para>序数 → 掩码位是**多对一**的合法降级(两序数不可映同一位);反向对五体格通道**一一对应**。</para>
    /// <para>⚠️ 两处刻意的不全等(显式登记,不静默 —— 见 <see cref="AssertConsistent"/>):</para>
    /// <list type="bullet">
    /// <item><c>History</c>(序数 5)= 病史,是**非体格通道**(GDD 2026-09-14 裁定:通道外第二类证据)
    /// ⇒ 无掩码位(映 0,不入 mask)。</item>
    /// <item><c>Wound</c>(掩码 <c>1&lt;&lt;5</c>)= 伤口,归 **9 / 25 侧**(2026-09-17 起第六条),
    /// R-8.1 词表无对应序数标签 ⇒ **9 侧独有**,本侧无入口。</item>
    /// </list></summary>
    public static class DiagnosisChannelMaskMap
    {
        /// <summary>序数 → 掩码位的映射(索引 = <see cref="SignChannel"/> 序数)。
        /// <para>体格五通道各映唯一一位;<c>History</c> 映 0(非体格通道,不入 mask)。</para></summary>
        private static readonly int[] MaskByOrdinal =
        {
            ChannelMask.Complexion,   // FaceColor(0) → 面色
            ChannelMask.Voice,        // Voice(1)     → 语声
            ChannelMask.Posture,      // Posture(2)   → 姿态
            ChannelMask.Breathing,    // Breathing(3) → 呼吸
            ChannelMask.Palpation,    // Touch(4)     → 触感(⚠️ 序数 4 ≡ 掩码 Posture=4 同形不同义)
            0,                            // History(5)   → 无掩码位(非体格通道)
        };

        /// <summary>体格五通道的掩码位全集(R-8.1 侧应恰好映满这五位)。</summary>
        private static readonly int[] PhysicalBits =
        {
            ChannelMask.Complexion, ChannelMask.Voice, ChannelMask.Posture,
            ChannelMask.Breathing, ChannelMask.Palpation,
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

        /// <summary>掩码位 → R-8.1 通道序数(体格五通道的反向)。<c>Wound</c> / 0 无对应 ⇒ <c>null</c>。</summary>
        /// <param name="maskBit">AC-21 掩码位(单一位)。</param>
        /// <returns>对应序数;无对应(9 侧独有位 / 0)⇒ <c>null</c>。</returns>
        public static SignChannel? OrdinalOf(int maskBit)
        {
            for (int i = 0; i < MaskByOrdinal.Length; i++)
                if (MaskByOrdinal[i] != 0 && MaskByOrdinal[i] == maskBit)
                    return (SignChannel)i;
            return null;
        }

        /// <summary>
        /// <b>构建期双向断言</b>(Note 6 原文「五通道+病史恰好映满、零悬空掩码位、零重复位」)。
        /// <para>逐条:</para>
        /// <list type="number">
        /// <item>体格五通道恰好映满 <see cref="PhysicalBits"/>(不多不少);</item>
        /// <item>五体格位**两两互异**(零重复位 —— 否则两个通道塌成一位,静默丢通道);</item>
        /// <item>体格位**两两互异**且 = 五个已知位(零悬空:无第五个之外的位被体格通道占用);</item>
        /// <item>反向一一对应(每个体格位恰由一个序数映得);</item>
        /// <item><c>Wound</c> 位 = **唯一**悬空掩码位(9 侧独有,显式登记 —— 不是缺陷);</item>
        /// <item><c>History</c> 映 0(非体格通道,显式登记 —— 不是缺陷)。</item>
        /// </list>
        /// </summary>
        /// <exception cref="ArgumentException">任一不满足(构建期硬失败)。</exception>
        public static void AssertConsistent()
        {
            // ① 五通道恰好映满已知五位
            var physical = new HashSet<int>();
            for (int i = 0; i < PhysicalBits.Length; i++) physical.Add(PhysicalBits[i]);

            var mapped = new HashSet<int>();
            var seenOrdinals = new HashSet<int>();
            for (int ordinal = 0; ordinal < MaskByOrdinal.Length; ordinal++)
            {
                int bit = MaskByOrdinal[ordinal];
                if (bit == 0) continue;   // History(无位)在此跳过,单独在第 ⑥ 条判
                seenOrdinals.Add(ordinal);

                // ② 零重复位:两个序数不可映同一位
                if (!mapped.Add(bit))
                    throw new ArgumentException(
                        $"Note6·通道映射 位 0x{bit:X} 被多个序数映射 —— 零重复位(两通道塌成一位 = 静默丢通道)");

                // ③ 零悬空:体格位须在已知五位集内
                if (!physical.Contains(bit))
                    throw new ArgumentException(
                        $"Note6·通道映射 序数 {ordinal} 映到未知位 0x{bit:X} —— 零悬空(体格通道不得占用非登记位)");
            }

            // ① 五通道恰满:映射到的位集 == 已知五位集
            if (mapped.Count != PhysicalBits.Length || !mapped.SetEquals(physical))
                throw new ArgumentException(
                    $"Note6·通道映射 体格位集 {Dump(mapped)} ≠ 已知五位 {Dump(physical)} —— " +
                    "五通道须恰好映满(多一位 = 新造通道;少一位 = 通道丢失)");

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

            // ⑤ Wound 位 = 唯一悬空掩码位(9 侧独有;显式登记,不是缺陷)
            int wound = ChannelMask.Wound;
            if (physical.Contains(wound))
                throw new ArgumentException(
                    "Note6·通道映射 Wound 位竟被体格通道占用 —— 该位归 9 侧(伤口,2026-09-17 起)," +
                    "R-8.1 词表无对应序数标签");
            if (OrdinalOf(wound) != null)
                throw new ArgumentException(
                    "Note6·通道映射 Wound 位竟有 R-8.1 序数反向 —— 该位应 9 侧独有");

            // ⑥ History 映 0(非体格通道;显式登记,不是缺陷)
            if (MaskOf(SignChannel.History) != 0)
                throw new ArgumentException(
                    "Note6·通道映射 History(非体格通道)竟映到非零位 —— 病史是通道外第二类证据," +
                    "不得借体格位(否则新造通道)");
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
