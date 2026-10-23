// `ITreatableByLookup` 的生产实现(2026-10-23 立)—— 从处置轴烘焙产物造查表。
//
// 权威来源:
//   GDD disease-simulation.md F1 取舍 4(`:896`)—— 「成员与极性**同查轴行** —— 轴为准」
//   GDD disease-simulation.md `D-9-K`(2026-10-10 已裁:极性双源优先级 = 轴为准)
//   GDD disease-simulation.md `D-9-P`(treatable_by 门结构性缺位;装载面接通后按轴查)
//   GDD disease-simulation.md `D-9-L` —— 极性序数契约 `Symptomatic = 0 · Causal = 1`
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts} ⇒ 解码器与查表都归装配层
//
// ── 为什么本类住装配层(`Gameplay.Boot`)而不是 `Gameplay.Presentation` ─────────
//   它**消费** `DiseaseActionAxisCookedCodec` 的解码形(`Gameplay.Presentation`),
//   并**实现** `Sim.Contracts` 的接口。装配层是唯一同时可见两者的地方 ——
//   与 `PayloadEncoder` / `PayloadDecoder` 的落点同理由(ADR-029 乙案 / ADR-031 §②)。
//   ⚠️ 若放进 `Gameplay.Presentation`,消费方 `Sim` 仍然够不着(门 A),只是把
//   同一段胶水挪远;放在装配层则与「构造点只在本装配可见 Sim.Codec」的既有纪律一致。
//
// ── 为什么不做成「烘进注册表产物」(2026-10-23 用户裁定)──────────────────────
//   `disease_action_axis.json` 的 `treatable_by` 段是**单一真源**。再烘一份进
//   `disease_registry.cooked` 会让两者可分叉 —— 而分叉正是 `D-9-K` 要消灭的形态。
//   ⇒ 注册表产物**不携带** treatable_by;运行期由本类查轴。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 把 <see cref="DiseaseActionAxisDataSet.TreatableBy"/> 的扁平行集转成
    /// <c>(病种, 处置) → 极性</c> 的只读查表(<see cref="ITreatableByLookup"/> 生产实装)。
    /// </summary>
    /// <remarks>
    /// <para><b>极性序数</b>取 GDD <c>D-9-L</c> 的全案唯一契约:
    /// <c>symptomatic = 0 · causal = 1</c>。⚠️ 这与 <c>ProgressionEvaluator.PolarityCausal</c>
    /// 是**同一个数**,但两者**不是**同一处定义 —— 本类把轴里的**字符串**
    /// 翻成序数,而 <c>Sim</c> 侧只认序数(它读不到轴)。翻译点唯一在本类,
    /// 故序数若将来变更,红的是本类的表而不是散落各处。</para>
    ///
    /// <para><b>未知极性串</b>:烘焙期已由 9-DC-2 拒收(只允许 <c>causal</c> / <c>symptomatic</c>,
    /// 见 <c>DiseaseActionAxisBinder</c>),故运行期遇到第三个值是**产物被篡改或跨版本**。
    /// 本类在此 fail-loud 抛 —— 静默跳过会让「该处置不可用」与「产物坏了」不可区分。</para>
    ///
    /// <para><b>重复行</b>:同一 <c>(病种, 处置)</c> 出现两次 = 轴自相矛盾。烘焙期未拦
    /// (该文件的两阶段校验只查成员性与极性词表),故本类在构造期 fail-loud。
    /// 若两次极性相同,静默取后者是「看起来能跑」的最坏形态。</para>
    /// </remarks>
    public sealed class DiseaseActionAxisTreatableByLookup : ITreatableByLookup
    {
        /// <summary>极性序数「对症」(GDD <c>D-9-L</c>)。</summary>
        public const int PolaritySymptomatic = 0;

        /// <summary>极性序数「对因」(GDD <c>D-9-L</c>;与 <c>ProgressionEvaluator.PolarityCausal</c> 同值)。</summary>
        public const int PolarityCausal = 1;

        /// <summary>未命中时写出的哨兵(不是合法极性;见 <see cref="ITreatableByLookup"/>)。</summary>
        public const int PolarityNone = -1;

        // key = (diseaseKey, actionId) 的复合键。用 ValueTuple 而非字符串拼接:
        //   拼接键在病种 key 含分隔符时会**静默撞键**,而 ValueTuple 的比较是逐字段的。
        private readonly Dictionary<(string DiseaseKey, int ActionId), int> _byPair;

        /// <summary>
        /// 从已解码的轴产物构造查表。
        /// </summary>
        /// <param name="axis">处置轴数据集(<c>DiseaseActionAxisDataSet</c>,由 <c>IDataProvider</c> 装载)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="axis"/> 的
        /// <c>TreatableBy</c> 为 null(装配层应传已解码的数据集,不是 default)。</exception>
        /// <exception cref="InvalidOperationException">极性串不在闭集内 / 同一 (病种, 处置) 重复声明。</exception>
        public DiseaseActionAxisTreatableByLookup(DiseaseActionAxisDataSet axis)
        {
            List<TreatableByCookedRow> rows = axis.TreatableBy;
            if (rows == null)
                throw new ArgumentNullException(
                    nameof(axis),
                    "DiseaseActionAxisDataSet.TreatableBy 为 null —— 装配层传的是 default 而非已解码数据集;" +
                    "treatable_by 查表拒绝以空表继续(空表 = 「所有处置都不可用」,与「未接线」不同)");

            _byPair = new Dictionary<(string, int), int>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                TreatableByCookedRow row = rows[i];

                int polarity;
                switch (row.Polarity)
                {
                    case "causal":      polarity = PolarityCausal;      break;
                    case "symptomatic": polarity = PolaritySymptomatic; break;
                    default:
                        throw new InvalidOperationException(
                            $"轴产物 treatable_by[{i}] 的 polarity=\"{row.Polarity}\" ∉ " +
                            "{causal, symptomatic} —— 9-DC-2 应在烘焙期拒收;产物被篡改或跨版本");
                }

                var key = (row.DiseaseKey, row.ActionId);
                if (_byPair.TryGetValue(key, out int existing))
                    throw new InvalidOperationException(
                        $"轴产物 treatable_by 重复声明 ({row.DiseaseKey}, action={row.ActionId}):" +
                        $"极性 {existing} 与 {polarity} 冲突 —— 同一 (病种, 处置) 只能有一个极性");

                _byPair.Add(key, polarity);
            }
        }

        /// <summary>查表行数(诊断 / 测试观测面)。</summary>
        public int Count => _byPair.Count;

        /// <inheritdoc />
        public bool TryGetPolarity(string diseaseKey, int actionId, out int polarity)
        {
            if (diseaseKey != null && _byPair.TryGetValue((diseaseKey, actionId), out polarity))
                return true;

            polarity = PolarityNone;
            return false;
        }
    }
}
