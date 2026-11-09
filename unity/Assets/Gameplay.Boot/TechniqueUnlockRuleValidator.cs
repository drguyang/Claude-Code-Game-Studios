// 技法解锁规则的**值域校验**(T12 · 2026-11-09)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2
//       · T9 的 `TechniqueUnlockRule`(三类源)与 `TechniqueUnlockRules.Default`
//       · T9 证据件 §6「未做」表最后一条:三类 id 的值域**今日无任何门兜**
//
// ── 补的是什么洞 ──────────────────────────────────────────────────────────
// T9 的规则行有三个源 id,各自的词表归不同系统:
//   · `Poi`   → `poi_id`    ∈ `world_poi.json`(6 的地盘)
//   · `Event` → `event_key` ∈ `52_event_pool.json`(52 的地盘)
//   · `Case`  → `disease_id` ∈ `disease_registry.json`(9 的地盘)
// 写错一个 ⇒ 规则**永不命中**,而且**静默**:没有报错、没有日志,表现就是「这局没学到东西」。
// 技法 id 那一侧 T9 已有门(写者侧闭集 + 用例);这三侧此前一条都没有。
//
// ── 为什么是「校验器」而不是「构建期门」 ──────────────────────────────────
// 规则表今日住**代码常量**(`TechniqueUnlockRules.Default`),不是烘焙产物
// ⇒ 没有构建期阶段可挂。判据挂在夹具上,与 T9 的技法闭集判据同格。
// 若将来规则表外部化成 baked artifact,本校验器**原样复用**在 binder 里(它已是纯函数)。
//
// ── ⚠️ 今日零规则的后果:空真 ────────────────────────────────────────────
// `Default` 为空 ⇒ 本校验器返回零违例是**空真**,不是「查过没问题」。
// 判据侧必须用**负控**证明它有牙(手艺标准 §假绿形态 F4 空洞正控)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>规则表对三张真表的引用校验(纯函数,零 I/O)。</summary>
    public static class TechniqueUnlockRuleValidator
    {
        /// <summary>
        /// 逐条校验规则里的源 id 是否存在于对应词表。
        /// </summary>
        /// <param name="rules">规则表(可空集 —— 空集返回空,但那是**空真**,见类头注)。</param>
        /// <param name="poiIds">合法 `poi_id` 集(`world_poi.json`)。</param>
        /// <param name="eventKeys">合法事件键集(`52_event_pool.json`)。</param>
        /// <param name="diseaseIds">合法病种 id 集(`disease_registry.json`,含伤情)。</param>
        /// <returns>违例描述(空 = 无违例)。每条都带规则下标与源种类,便于定位。</returns>
        /// <exception cref="ArgumentNullException">任一入参为 null。</exception>
        public static IReadOnlyList<string> Validate(
            IReadOnlyList<TechniqueUnlockRule> rules,
            IReadOnlyCollection<int> poiIds,
            IReadOnlyCollection<int> eventKeys,
            IReadOnlyCollection<int> diseaseIds)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (poiIds == null) throw new ArgumentNullException(nameof(poiIds));
            if (eventKeys == null) throw new ArgumentNullException(nameof(eventKeys));
            if (diseaseIds == null) throw new ArgumentNullException(nameof(diseaseIds));

            var violations = new List<string>();
            for (int i = 0; i < rules.Count; i++)
            {
                TechniqueUnlockRule rule = rules[i];
                switch (rule.SourceKind)
                {
                    case TechniqueUnlockSourceKind.Poi:
                        if (!Contains(poiIds, rule.SourceId))
                            violations.Add(
                                $"规则[{i}] Poi 源 id={rule.SourceId} 不在 world_poi 里 —— " +
                                "这条规则永不命中(静默:没有报错,表现只是「这局没学到东西」)");
                        break;

                    case TechniqueUnlockSourceKind.Event:
                        if (!Contains(eventKeys, rule.SourceId))
                            violations.Add(
                                $"规则[{i}] Event 源 key={rule.SourceId} 不在 52_event_pool 里 —— " +
                                "这条规则永不命中(静默)");
                        break;

                    case TechniqueUnlockSourceKind.Case:
                        if (!Contains(diseaseIds, rule.SourceId))
                            violations.Add(
                                $"规则[{i}] Case 源 disease_id={rule.SourceId} 不在 disease_registry 里 —— " +
                                "这条规则永不命中(静默)");
                        break;

                    default:
                        violations.Add($"规则[{i}] 源种类 {rule.SourceKind} 不在闭集内(枚举值被写坏)");
                        break;
                }
            }
            return violations;
        }

        private static bool Contains(IReadOnlyCollection<int> set, int value)
        {
            foreach (int candidate in set)
                if (candidate == value) return true;
            return false;
        }
    }
}
