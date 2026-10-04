// patient-ai Story 001 —— `Map(position, trend, prev)`(GDD F-13.1)与分析核心。
//
// 权威来源:
//   GDD design/gdd/patient-ai.md §Formulas F-13.1(行为分档 + SymptomTier + 滞回)
//     · §States 一(三值转移表)· §States 一-ter(Terminal 锁存)
//     · §Core Rules 三(行为分化**只**来自 position / trend;`signs[]` 永不进决策)
//   ADR-016 §一 补注(13 住边界层)· ADR-027(13 零写)· TR-patient-001/003/004
//
// ⚠️ **本文件是 13 的决策核心**,全部输入恰 = `(position, trend, prev)` ——
//    AC-13-A5 的反射白名单断言(signs[] 不得进 `Map()`)以此文件为判据对象。
// ⚠️ **滞回是承重件,不是打磨项**(GDD R3)。没有滞回会怎样:`position` 在 `SEEK_MIN` 附近
//    漂移时,病人**逐 tick 在 Idle / Seeking 间翻转** —— 门口的病人反复转身再转身。
//    **而它静默**:无报错、AC 只要不比轨迹就全绿。这与 27 的 EC-27-23(`acc` 未冻致周期震荡)**同型**。

using System;

namespace DaYiJingCheng.Gameplay.PatientAI
{
    /// <summary>`Map()` 的输入 —— **恰** `(position, trend, prev)` 三项(AC-13-A5)。
    /// <para>⚠️ 刻意**无 `signs[]` 字段** —— 行为分化只来自 `position` / `trend`
    /// (GDD 规则三-bis:`signs[]` 只喂表现材质,永不进 `Map()`)。</para></summary>
    public readonly struct BehaviorInput
    {
        /// <summary>9 的原始量 0–1(`VitalsDto.Position`;13 **不夹取** —— 夹取 = 篡改 9 的真值)。</summary>
        public readonly float Position;
        /// <summary>带符号趋势(`VitalsDto.Trend`;缺失期按 0 处理)。</summary>
        public readonly float Trend;
        /// <summary>上一 tick 的行为态(滞回用);首拍 = <see cref="BehaviorState.Idle"/>。</summary>
        public readonly BehaviorState Prev;

        public BehaviorInput(float position, float trend, BehaviorState prev)
        {
            Position = position; Trend = trend; Prev = prev;
        }
    }

    /// <summary>症状表现档(只表现,不决策 —— GDD F-13.1 `SymptomTier`,满射无未定义分支)。</summary>
    public enum SymptomTier
    {
        /// <summary>`position < MILD_MIN ∧ trend ≤ 0`。</summary>
        Recover = 0,
        /// <summary>`MILD_MIN ≤ position < SEEK_MIN`(或 `< MILD_MIN ∧ trend > 0`)。</summary>
        Mild = 1,
        /// <summary>`position ≥ SEEK_MIN`。</summary>
        Severe = 2
    }

    /// <summary>13 的行为决策核心 —— `Map` / `SymptomTier` / `Terminal` 三式(F-13.1 / F-13.1 一-ter)。
    /// <para><b>纯函数</b> —— 无字段无状态,全部输入走形参(AC-13-B3 可重建)。</para></summary>
    public static class BehaviorMap
    {
        // ═══════════════════════════════════════════════════════════
        //  F-13.1 行为分档(滞回承重)
        // ═══════════════════════════════════════════════════════════

        /// <summary>`BehaviorState = Map(position, trend, prev)`(GDD F-13.1 逐字落地)。
        /// <para><b>滞回</b>:转出阈值 = 进入阈值 − `HYST`,且**转出依赖 `prev`** ——
        /// 这正是「`Map` 有记忆」的落点。</para>
        /// <example><code>var s = BehaviorMap.Map(new BehaviorInput(0.5f, 0.1f, BehaviorState.Idle), bands, knowsClinic: true);</code></example>
        /// </summary>
        /// <param name="input">`(position, trend, prev)`。</param>
        /// <param name="bands">13 自有分档阈值(`BEHAVIOR_BAND_*`)。</param>
        /// <param name="knowsClinic">`KnowsClinic(p)` —— 见 F-13.2。`Seeking` 的两处分支都需其为真。</param>
        public static BehaviorState Map(in BehaviorInput input, in BehaviorBands bands, bool knowsClinic)
        {
            float p = input.Position;
            // ⚠️ `trend` 刻意**入签名但不参与分支**(F-13.1 字面):`Map` 只读 `position` 与 `prev`。
            //    13 侧读它只为把「F-13.1 首行声明 trend 是决策面输入」这件事留成**可读的**形状 ——
            //    若日后有人把 trend 拿去改 BehaviorState,AC-13-A5 的输入白名单会先红。
            _ = input.Trend;

            // ① 撑不住(无滞回 —— 进入即躺;承 GDD 转移表「position ≥ COLLAPSE_MIN」)
            if (p >= bands.CollapseMin) return BehaviorState.Bedridden;

            // ② 滞回:不急着起身(躺下后 position 需回落到 COLLAPSE_MIN − HYST 以下)
            if (input.Prev == BehaviorState.Bedridden && p >= bands.CollapseMin - bands.Hyst)
                return BehaviorState.Bedridden;

            // ③ 去看病(进入阈值 + 知识判据)
            if (p >= bands.SeekMin && knowsClinic) return BehaviorState.Seeking;

            // ④ 滞回:不急着放弃(已在 Seeking 的病人,回落幅度须超 HYST 才回 Idle)
            if (input.Prev == BehaviorState.Seeking && p >= bands.SeekMin - bands.Hyst && knowsClinic)
                return BehaviorState.Seeking;

            // ⑤ 默认:低频巡游
            return BehaviorState.Idle;
        }

        // ═══════════════════════════════════════════════════════════
        //  F-13.1 SymptomTier(满射,无未定义分支;只表现,不决策)
        // ═══════════════════════════════════════════════════════════

        /// <summary>`SymptomTier(position, trend)` —— **只表现**,不进 `BehaviorState`。
        /// <para>⚠️ 四分支**满射**(GDD 字面):`Severe` / `Mild` / `Recover` / `Mild`(兜底)。
        /// 「态由档 + `KnowsClinic` 推出,档不写死在态的分支里」—— GDD F-13.1 注。</para></summary>
        public static SymptomTier Tier(float position, float trend, in BehaviorBands bands)
        {
            if (position >= bands.SeekMin) return SymptomTier.Severe;
            if (position >= bands.MildMin) return SymptomTier.Mild;
            if (trend <= 0f) return SymptomTier.Recover;
            return SymptomTier.Mild;   // position < MILD_MIN ∧ trend > 0
        }

        // ═══════════════════════════════════════════════════════════
        //  Terminal 锁存位(GDD §States 一-ter)
        // ═══════════════════════════════════════════════════════════

        /// <summary>`Terminal(p)` 的**单次求值**(置位判据)。
        /// <para>⚠️ 本方法**不锁存** —— 锁存由 <see cref="PatientBehavior"/> 持有;
        /// 本方法只是「此刻是否该置位」。**一旦置位不再重新求值**(GDD 字面)。</para></summary>
        public static TerminalFlag EvaluateTerminal(float position, float trend, in BehaviorBands bands)
        {
            // 痊愈锁存:RECOVER_MAX 与 DEATH_BAND_MIN 之间是活着的病程,只在两端置位
            if (position <= bands.RecoverMax && trend <= 0f)
                return new TerminalFlag(true, TerminalCause.Recovered);
            if (position >= bands.DeathBandMin)
                return new TerminalFlag(true, TerminalCause.Deceased);
            return TerminalFlag.None;
        }
    }
}
