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

        /// <summary>`VitalsDto.Trend` 是否**可用**(即 9 侧真的给出了非恒 0 的趋势)。
        /// <para>🔁 **2026-10-29(`TD-audio-025` · 用户裁定甲案)**:由 <c>const false</c> 改为
        /// **按数据配置判定**的静态属性。</para>
        /// <para><b>原状</b>:`const TrendWired = false` —— 因 `Sim/ProgressionEvaluator.ComputeTrend`
        /// 是恒 0 的 tripwire 占位,而 `EvaluateTerminal` 的痊愈判据 `position ≤ RECOVER_MAX ∧ trend ≤ 0`
        /// 在 trend 恒 0 时**恒真** ⇒ 每个新出现病人首拍即锁存「痊愈」⇒ 呼吸层永不启动
        /// (2026-10-18 实机取证:`tick=2` 起 `term=True emit=False breath=None`)。</para>
        /// <para><b>为什么从 `const` 改成属性</b>:甲案把 9 侧喂给 13 的量从**中心差分**
        /// (恒 0 的占位)换成**后向差分**(`ComputeTrendBackward`,已实现),但该函数的输出
        /// **仍取决于 `W` / `TREND_EPS` 两个数值** —— 两者为 0 时它照样返回 0。
        /// ⇒ 硬编码 `true` 会在数值未填时**重现同一个故障**;硬编码 `false` 又会在数值填好后
        /// **仍然停用**痊愈锁存(需要人再改一次代码)。两者都不对。
        /// **正解 = 门跟着「数据是否配置」走**:`W > 0` ⇒ 趋势真的是算出来的 ⇒ 痊愈判据可用。</para>
        /// <para>⚠️ <b>为什么必须显式成一个开关</b>:`EvaluateTerminal` 要回答「position 低 = 痊愈
        /// **还是**刚发病」,这两者**只能靠 trend 区分**。trend 恒 0 时判据恒真 ⇒ 误锁。
        /// **禁用 ≠ 误判**:误判会把「刚发病」当成「已痊愈」,而「已痊愈」是**终局**,
        /// 一旦锁存不可逆。死亡锁存不受影响(它只依赖 position)。</para>
        /// <para>⚠️ **接线后的动作**已由本属性自动完成(数值一填即生效);
        /// 但复跑 13 全套时 `test_ac13b1_terminalLatch_recoversDoNotResurrect`
        /// 的 NOT-RUN 哨兵会转红,届时须把它改回**真断言**(禁删哨兵)。</para></summary>
        public static bool TrendWired { get; private set; } = false;

        /// <summary>由装配层告知「趋势量现在是否可用」(`W` 已配置 ⇒ <c>true</c>)。
        /// <para>⚠️ 只在**装配期**调一次(`CompositionRoot`);运行期不变 —— 它不是状态,是配置投影。</para>
        /// <para>⚠️ 本方法**只能**把门从 false 打开,不能反向关 —— 防止运行期某处误调把已生效的
        /// 痊愈判据悄悄停用(那是静默行为变更)。</para></summary>
        /// <param name="wired">`W`(趋势窗口 tick)&gt; 0 ⇒ 趋势量可用。</param>
        public static void ConfigureTrendWired(bool wired)
        {
            if (wired) TrendWired = true;
        }

        /// <summary>**仅供测试**复位到默认态(门关)。
        /// <para>⚠️ 存在的理由:`ConfigureTrendWired` 只开不关(防运行期静默停用),
        /// 而 EditMode 夹具要在同一进程里验「门开」与「门关」两种形态 ⇒ 需要一个复位口。
        /// **生产代码不得调用** —— 它会让已生效的痊愈判据被悄悄停用。</para></summary>
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void ResetTrendWiredForTest() => TrendWired = false;

        /// <summary>`Terminal(p)` 的**单次求值**(置位判据)。
        /// <para>⚠️ 本方法**不锁存** —— 锁存由 <see cref="PatientBehavior"/> 持有;
        /// 本方法只是「此刻是否该置位」。**一旦置位不再重新求值**(GDD 字面)。</para>
        /// <para>⚠️ 痊愈半边受 <see cref="TrendWired"/> 门控(见其注);死亡半边恒开。</para></summary>
        public static TerminalFlag EvaluateTerminal(float position, float trend, in BehaviorBands bands)
        {
            // 痊愈锁存:RECOVER_MAX 与 DEATH_BAND_MIN 之间是活着的病程,只在两端置位。
            // ⚠️ `TrendWired` 门:trend 不可用 ⇒ 本判据**不可用**(position 低既可能是痊愈,
            //    也可能是刚发病)—— 见 TrendWired 注。**不得**在此处用 `trend <= 0` 的
            //    「恒真」形态绕过该门。
            if (TrendWired && position <= bands.RecoverMax && trend <= 0f)
                return new TerminalFlag(true, TerminalCause.Recovered);
            if (position >= bands.DeathBandMin)
                return new TerminalFlag(true, TerminalCause.Deceased);
            return TerminalFlag.None;
        }
    }
}
