// patient-ai Story 001 —— 13 的行为分档阈值表(`BEHAVIOR_BAND_*`,GDD F-13.1 / §Tuning 一)。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.1(COLLAPSE_MIN / SEEK_MIN / MILD_MIN /
//     RECOVER_MAX / HYST / DEATH_BAND_MIN)· §Tuning Knobs 一(硬约束 + 安全范围)
//   ADR-014 §二(`assets/data/ai_patient.json` 两阶段烘焙;安全范围 = 区间校验位点)
//   TR-patient-004(13 不重建九态,分档只用自有 BEHAVIOR_BAND_*)
//
// ⚠️ **本表是 13 自己的呈现分档,与 9 的九态阈值(`CRITICAL` / `COMA` / `DEATH_THRESHOLD`)
//    是两个集合**(GDD 规则三 + F-13.1 注)。13 只说「看起来撑不住了」,不说「这是危殆」。
//    两集合**不要求相等** —— 对齐关系登记为 `OQ-13-2`(跨系统阈值对齐,归数值轮)。
//
// ⚠️ **数值归用户**(项目铁律:数值用户自己调)。下方默认值是**合法区间内的样例**,
//    只为让机制可跑;真值随数值轮落 `assets/data/ai_patient.json`。
//    ⚠️ 本故事**不**接 ADR-014 烘焙管线(story 003 落 `MaterialTable` / `CUE_INTERVAL`
//    的烘焙时再一并接)—— 现为**编译期常量**,硬约束由 `Validate()` 断言(见下)。

using System;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>13 的行为分档阈值(`BEHAVIOR_BAND_*`)—— 值域 [0,1] 的呈现分档线。
    /// <para><b>硬约束</b>(GDD §Tuning 一):<c>MILD_MIN &lt; SEEK_MIN &lt; COLLAPSE_MIN
    /// &lt; DEATH_BAND_MIN &lt; 1</c> · <c>RECOVER_MAX &lt; MILD_MIN</c> · <c>0 &lt; HYST &lt; SEEK_MIN</c>。
    /// 破了不是「难玩」,是**状态机自相矛盾**(两个转出条件同时为真)⇒ <see cref="Validate"/> 断言。</para>
    /// </summary>
    public readonly struct BehaviorBands
    {
        /// <summary>「有点不对劲」分档下限(仅 `SymptomTier` 用,无滞回)。</summary>
        public readonly float MildMin;
        /// <summary>「去看病」分档下限(**进入**阈值)。</summary>
        public readonly float SeekMin;
        /// <summary>「撑不住」分档下限(**进入**阈值)。</summary>
        public readonly float CollapseMin;
        /// <summary>**痊愈完成线**(终态锁存;`&lt; MildMin`)。</summary>
        public readonly float RecoverMax;
        /// <summary>**滞回带宽度**(转出阈值 = 进入阈值 − HYST)。</summary>
        public readonly float Hyst;
        /// <summary>死亡锁存带下限(`position` 最高带)。</summary>
        public readonly float DeathBandMin;

        public BehaviorBands(float mildMin, float seekMin, float collapseMin,
                             float recoverMax, float hyst, float deathBandMin)
        {
            MildMin = mildMin; SeekMin = seekMin; CollapseMin = collapseMin;
            RecoverMax = recoverMax; Hyst = hyst; DeathBandMin = deathBandMin;
        }

        /// <summary>GDD §Tuning 一「样例合法集」—— **不是真值**,数值轮由用户改。
        /// <para>⚠️ 真值落 `assets/data/ai_patient.json` 是 story 003 的义务;本表仅为让
        /// story 001 的真值表可跑 —— 所有断言只依赖**硬约束**,不依赖具体数值。</para></summary>
        public static BehaviorBands Default => new BehaviorBands(
            mildMin: 0.20f, seekMin: 0.45f, collapseMin: 0.75f,
            recoverMax: 0.10f, hyst: 0.10f, deathBandMin: 0.90f);

        /// <summary>硬约束校验(GDD §Tuning 一「须在烘焙校验期断言」)。
        /// <para>返回错误列表(空 = 合法);调用方决定 throw / 聚合 —— 照
        /// `PresentationDtoGuard` 先例(不引 NUnit)。</para></summary>
        public static System.Collections.Generic.List<string> Validate(in BehaviorBands b)
        {
            var errs = new System.Collections.Generic.List<string>();

            // 单调链:MILD_MIN < SEEK_MIN < COLLAPSE_MIN < DEATH_BAND_MIN < 1
            if (!(b.MildMin < b.SeekMin))
                errs.Add($"[F-13.1] MILD_MIN({b.MildMin}) 须 < SEEK_MIN({b.SeekMin})");
            if (!(b.SeekMin < b.CollapseMin))
                errs.Add($"[F-13.1] SEEK_MIN({b.SeekMin}) 须 < COLLAPSE_MIN({b.CollapseMin})");
            if (!(b.CollapseMin < b.DeathBandMin))
                errs.Add($"[F-13.1] COLLAPSE_MIN({b.CollapseMin}) 须 < DEATH_BAND_MIN({b.DeathBandMin})");
            if (!(b.DeathBandMin < 1f))
                errs.Add($"[F-13.1] DEATH_BAND_MIN({b.DeathBandMin}) 须 < 1(死亡带须可达)");

            // ⚠️ **m3 修复(2026-10-04 评审)**:GDD §Tuning 一 明写 `SEEK_MIN < DEATH_BAND_MIN`
            //    为**独立**硬约束 —— 原表只校验传递链(`SEEK_MIN < COLLAPSE_MIN < DEATH_BAND_MIN`),
            //    该条虽被蕴含,但直报缺项会让「改坏 SEEK_MIN」时错误信息不点名真正越界的约束。
            //    补为独立项(与 GDD 逐条对齐,免「校验表 ≠ GDD 约束表」)。
            if (!(b.SeekMin < b.DeathBandMin))
                errs.Add($"[F-13.1] SEEK_MIN({b.SeekMin}) 须 < DEATH_BAND_MIN({b.DeathBandMin})");

            // 下界:> 0(三档下限都须为正 —— 否则 0 值病人被误判进症状档)
            if (!(b.MildMin > 0f))
                errs.Add($"[F-13.1] MILD_MIN({b.MildMin}) 须 > 0");
            if (!(b.SeekMin > 0f))
                errs.Add($"[F-13.1] SEEK_MIN({b.SeekMin}) 须 > 0");
            if (!(b.CollapseMin > 0f))
                errs.Add($"[F-13.1] COLLAPSE_MIN({b.CollapseMin}) 须 > 0");

            // 痊愈锁存线:0 < RECOVER_MAX < MILD_MIN(破了 ⇒ 痊愈/发病间抖动)
            if (!(b.RecoverMax > 0f && b.RecoverMax < b.MildMin))
                errs.Add($"[F-13.1] RECOVER_MAX({b.RecoverMax}) 须 ∈ (0, MILD_MIN({b.MildMin})) —— " +
                         "否则病人在痊愈与发病间抖动");

            // 滞回:0 < HYST < SEEK_MIN(= 0 ⇒ 逐 tick 翻转;≥ SEEK_MIN ⇒ Seeking 永不转出)
            if (!(b.Hyst > 0f))
                errs.Add($"[F-13.1] HYST({b.Hyst}) 须 > 0 —— = 0 ⇒ 逐 tick 翻转(EC-13-01)");
            if (!(b.Hyst < b.SeekMin))
                errs.Add($"[F-13.1] HYST({b.Hyst}) 须 < SEEK_MIN({b.SeekMin}) —— " +
                         "否则 Seeking 永不转出(病人赖在门口)");

            return errs;
        }
    }
}
