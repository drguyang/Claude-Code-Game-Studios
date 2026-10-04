// patient-ai Story 001 —— 咳嗽 / 呻吟 cue 发射率(GDD F-13.5)+ `Intensity` 产物侧夹取。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.5(CueInterval / Emit / ClampByte / phase)
//     · §Tuning 三(`CUE_INTERVAL[*]` 档间有序)· §Edge Cases 一(13 不夹取 position)
//   ADR-018 §一/§四(44 只触发不持状态;无提示音铁律)· AC-13-D4/D5/D6 · TR-patient-018/019
//
// ⚠️ **两条分隔的纪律,都承重**:
//   ① `ClampByte`(R5)—— `position` 的越界(>1)是 **9 的缺陷**,13 **不回写**(§Edge Cases 一);
//      但「不回写」**不等于**「把越界值原样塞进 byte」:原稿 `round(1.5×255)=383` 塞进 `byte`
//      ⇒ **静默回绕成 127**(383 − 256),重病病人的强度**看起来像轻病** —— 畸形输入产出
//      畸形表现,且不报错。修法 = **在 13 的边界上产物侧夹取**(AC-13-D4)。
//   ② 去同步**不用 PRNG**(R7)—— 靠 `PatientId` 派生的**纯函数相位**(AC-13-D5);
//      cue 间隔**按档取值**,不按 `position` 连续插值(AC-13-D6,反幻想机械护栏)。
//
// ⚠️ **本文件 = 13 命名空间侧**(2026-10-04 拆分):
//   ① **13 的 cue 纯函数核**(`PatientCue`:`ClampByte` / `Intensity` / `Phase`)住 13 命名空间 ——
//      输入是 `PatientId` / `SymptomTier`,是 13 的**调度数值面**。
//   ② **13 → 44 的发射桥**(`PatientCueEmit`)与 **cue 调度数值面**(`CueKind` / `CueIntervals`)
//      住 44 前缀命名空间 —— 前者含 `AudioCueDto` / `IAudioCueSink` 两个 44 契约 token,
//      受 AC-44-B1 ① 逃逸谓词约束;后两者是发射桥的签名类型,已按 Story-004 `FilterRamp.State`
//      先例登记进 44 的 `EntryAllowlist`(它们不持游戏状态、不进 sim、不经三流 —— 违字面非违本意)。
//   用户裁定 2026-10-04:取「调参面归 44 命名空间 + 扩白名单」路线,理由 = 13 的 cue 档表本就是
//   44 事件的**作者态参数面**;13 侧只有 `PatientCue` 纯函数核留在本命名空间。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>13 的 cue **纯函数核**(F-13.5)—— `ClampByte` / `Intensity` / `Phase`。
    /// <para>⚠️ **零 PRNG**(AC-13-D5):去同步相位由 `hash32(PatientId)` 派生。
    /// **不持状态、不选素材、不混音**(ADR-018 §一:13 只发 cue id + 强度)。</para>
    /// <para>⚠️ **本类型刻意住 13 命名空间**(2026-10-04)—— 它是 13 的**调度数值面**,
    /// 输入是 `PatientId` / `SymptomTier`,与 44 的渲染参数面无关;若住进 44 前缀,
    /// 44 的入口白名单扫描器(AC-44-B3)会把它算作「44 的公开入口」并要求其签名 ⊆
    /// 44 渲染参数白名单 —— 而它本就不是 44 的入口。**住 44 前缀的是发射桥
    /// <see cref="DaYiJingCheng.Gameplay.Presentation.Audio.PatientCueEmit"/>
    /// 与 cue 调度数值面(`CueKind` / `CueIntervals`)。**</para></summary>
    public static class PatientCue
    {
        /// <summary>产物侧边界夹取(GDD F-13.5 `ClampByte`;**常量,不是旋钮**)。
        /// <para>⚠️ 这一步是 `position` 越界的**唯一**处理 —— `position` 本身照旧不夹
        /// (断言仍报 9 的缺陷),但**发出去的 byte 必须在 `[0,255]`**(AC-13-D4)。</para>
        /// <para>⚠️ 断言 `round(1.5 × 255) = 383` 类输入**不静默回绕** —— 383 → 255(不是 127)。</para></summary>
        public static byte ClampByte(float x)
        {
            float r = (float)Math.Round(x, MidpointRounding.AwayFromZero);
            if (r < 0f) return 0;
            if (r > 255f) return 255;
            return (byte)r;
        }

        /// <summary>`Emit` 的强度 —— `ClampByte(round(position × 255))`(GDD F-13.5 字面)。</summary>
        public static byte Intensity(float position) => ClampByte(position * 255f);

        /// <summary>去同步相位(F-13.5 ③)—— `hash32(PatientId) mod CUE_INTERVAL[tier]`。
        /// <para>⚠️ **纯函数、可重建、无需 PRNG** —— 多病人同屋时把各自首拍错开,避免叠成一声。
        /// 人耳分不出「真随机相位」与「按 id 错开的确定相位」(GDD 字面)。</para></summary>
        public static int Phase(PatientId id, SymptomTier tier, in CueIntervals intervals)
        {
            int interval = intervals.For(tier);
            uint h = Hash32(id.Value);
            return (int)(h % (uint)interval);
        }

        /// <summary>32 位整数哈希(纯函数,无 PRNG)—— 用于去同步相位。
        /// <para>⚠️ **刻意不用 `System.Random`**(AC-13-D5);FNV-1a 32 的整数变体,
        /// 与 `SplitMix64` 同族但更小(这里只要「确定性错开」,不要统计性质)。</para></summary>
        private static uint Hash32(int value)
        {
            unchecked
            {
                uint h = 2166136261u;              // FNV-1a 偏移基
                uint v = (uint)value;
                for (int i = 0; i < 4; i++)
                {
                    h ^= (v >> (i * 8)) & 0xFFu;
                    h *= 16777619u;               // FNV-1a 质数
                }
                return h;
            }
        }
    }

    /// <summary>cue 语义种类(咳嗽 / 呻吟)—— 语义归 13;**素材变体归 44**(ADR-018 §一)。</summary>
    public enum CueKind
    {
        Cough = 0,
        Groan = 1
    }

    /// <summary>`CUE_INTERVAL[*]` —— **按症状档**取的 cue 间隔(分段常函数,不插值)。
    /// <para>⚠️ **档间有序**、**档内固定** —— 这正是反幻想要的:「他咳得更凶了」可以是判断依据,
    /// 「他的值从 0.62 涨到 0.71」不可以(GDD F-13.5 ①)。</para>
    /// <para>⚠️ 数值归用户(数值轮);本表为**合法样例**,只承「档间有序」这一硬约束。</para>
    /// <para>⚠️ **住 44 前缀命名空间,并已登记进 44 的 `EntryAllowlist`**(2026-10-04)——
    /// 本类型是 13 的 cue 调度数值面,不持游戏状态 / 不进 sim / 不经三流,
    /// 与 Story-004 `FilterRamp.State` 同性质(同「扩允许面」A 案口径)。</para></summary>
    public readonly struct CueIntervals
    {
        /// <summary>重档间隔(tick;`&gt; 0` 且 `< Mild`)。</summary>
        public readonly int Severe;
        /// <summary>轻档间隔(tick;`&gt; Severe`)。</summary>
        public readonly int Mild;
        /// <summary>恢复档间隔(tick;`≥ Mild` —— 恢复期不得比轻档更密)。</summary>
        public readonly int Recover;

        public CueIntervals(int severe, int mild, int recover)
        {
            Severe = severe; Mild = mild; Recover = recover;
        }

        /// <summary>合法样例(数值轮由用户改)。</summary>
        public static CueIntervals Default => new CueIntervals(severe: 40, mild: 90, recover: 120);

        /// <summary>硬约束(GDD §Tuning 三):`0 < Severe < Mild ≤ Recover`。</summary>
        public static System.Collections.Generic.List<string> Validate(in CueIntervals c)
        {
            var errs = new System.Collections.Generic.List<string>();
            if (!(c.Severe > 0))
                errs.Add($"[F-13.5] CUE_INTERVAL[Severe]({c.Severe}) 须 > 0");
            if (!(c.Severe < c.Mild))
                errs.Add($"[F-13.5] CUE_INTERVAL[Severe]({c.Severe}) 须 < [Mild]({c.Mild}) —— " +
                         "否则「重档比轻档疏」,方向不可辨");
            if (!(c.Mild <= c.Recover))
                errs.Add($"[F-13.5] CUE_INTERVAL[Mild]({c.Mild}) 须 ≤ [Recover]({c.Recover}) —— " +
                         "否则「快好了咳得更凶」");
            return errs;
        }

        /// <summary>按档取间隔(**分段常函数** —— 档内任意 `position` 取同一值,AC-13-D6)。</summary>
        public int For(SymptomTier tier)
        {
            switch (tier)
            {
                case SymptomTier.Severe: return Severe;
                case SymptomTier.Mild: return Mild;
                default: return Recover;
            }
        }
    }
}
