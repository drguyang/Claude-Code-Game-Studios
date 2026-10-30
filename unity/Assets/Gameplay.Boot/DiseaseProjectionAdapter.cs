// 53 的 D 路真值适配器 —— 把 9 的 F4 九态投影成 `IDiseaseProjection`(ADR-036 D1 · S1 · 2026-10-30)
//
// 权威:ADR-036 §Decision D1(9 扩投影形状 · 实现住边界层 · 53 只经接口取值)
//       · ADR-036 §Risks R1(痊愈判据在、生产调用点在、公开访问器在;但今日被数值门挡着)
//       · ADR-036 §Risks R4(次序陷阱:`GetOutcome` 只给**当前态**)
//
// 🔁 **2026-10-30 二轮(用户裁定「9 加状态转移历史」)**:初版本适配器只接受
//    `tick == LastTick`。实现 driver 时实测该口径**接不上** ——
//    `ConsequenceInputAggregator.Aggregate` 按 `close.Tick` 取 D(`:305`),而延迟窗内
//    每条历史结案的 tick 都早于当前 ⇒ 只允许当前 tick 会让第二 tick 起全部抛。
//    现改为:**`tick ≤ LastTick` 一律可答**(经 `DiseaseVitalsService.TryGetOutcomeAt`
//    的状态转移历史),**只有未来 tick 抛**。
//
// ⚠️ **为什么住 `Gameplay.Boot` 而不是 `Sim`**:`Sim` 的引用集恰 = {BCL, `Sim.Contracts`}
//    (ADR-025 §① / 门 A)⇒ 它读不到 9 的实现;而本类是「9 的实现 → 53 的接口」的**唯一缝合点**,
//    天然属于装配层(`Gameplay.Boot` 已是引擎侧装配层,不受门 A 约束)。
//
// ⚠️ **今日痊愈位恒 false(必须连读)**:9 的痊愈支受 `RecoveryWindowInput.IsConfigured` 门控
//    (`F4OutcomeEvaluator.cs:97`),而 `disease_registry.json` 的 `trend_window_ticks = 0`
//    / `trend_eps = 0`(数值轮未裁)⇒ 痊愈支不求值 ⇒ `InjuryState.Recovered` 生产上不可达。
//    本适配器**如实转发**这一事实 —— 它不发明「痊愈」,也不把「未求值」伪装成
//    除「未痊愈」之外的第三种状态(接口只有两态)。⇒「S1 已落」≠「`Recovered` 6 格已可用」。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 9 的病程投影 → 53 的 <see cref="IDiseaseProjection"/>。
    /// <para>唯一取值路径 = <see cref="DiseaseVitalsService.TryGetOutcomeAt"/>;本类**不读**事件流、
    /// 不持状态、不做缓存(承 ADR-016 §一 三源不变量:输入只来自 9 的已求值状态)。</para>
    /// </summary>
    public sealed class DiseaseProjectionAdapter : IDiseaseProjection
    {
        private readonly DiseaseVitalsService _vitals;

        /// <summary>构造适配器。</summary>
        /// <param name="vitals">9 的体征服务(其 <see cref="DiseaseVitalsService.OnTickEdge"/> 须先于
        /// 本适配器所属的驱动运行 —— 见 ADR-036 §Risks R4)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="vitals"/> 为 null。</exception>
        public DiseaseProjectionAdapter(DiseaseVitalsService vitals)
        {
            _vitals = vitals ?? throw new ArgumentNullException(nameof(vitals));
        }

        /// <summary>取该病人在 <paramref name="tick"/> 时刻的病程投影。</summary>
        /// <param name="patient">病人 id。</param>
        /// <param name="tick">求值时刻。**可早于当前 tick**(迟发后果要按结案 tick 回溯)。</param>
        /// <param name="snapshot">有投影时写出快照。</param>
        /// <returns><c>true</c> = 有投影(该时刻病人已建档并求值过);<c>false</c> = 无投影(正常退化)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="tick"/> 晚于当前 tick —— **未来时刻**今天无从作答,静默给「此刻」
        /// 会把「还没发生」答成「已经如此」(ADR-036 §Risks R4)。
        /// </exception>
        /// <remarks>
        /// ⚠️ **本方法不是纯函数**(读 9 的已求值态与转移历史)—— 那是刻意的:
        /// <see cref="IDiseaseProjection"/> 是**边界**,不是 53 的纯函数面。承 ADR-016 §一:
        /// 输入仍只来自「事件流 + 版本化数据 + 二者纯函数」这条链(9 的态本身就是该链的产物)。
        /// </remarks>
        public bool TryProject(PatientId patient, long tick, out DiseaseProjectionSnapshot snapshot)
        {
            if (!_vitals.TryGetOutcomeAt(patient, tick, out InjuryState state))
            {
                snapshot = default;
                return false;   // 该时刻未建档 / 未驱动到 = 无投影(正常退化,非错误)
            }

            snapshot = new DiseaseProjectionSnapshot(
                tick, recovered: state == InjuryState.Recovered);
            return true;
        }
    }
}
