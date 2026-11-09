// 病种 → 「能治它的技法」的反向索引(T4 · 2026-11-09)
//
// 权威来源:
//   design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.1(技法门 = 硬门)
//   ADR-043 §D5 —— 技法 id 空间 = 处置轴 `disease_action_axis.json` 的 action id
//   GDD disease-simulation.md `D-9-K` —— treatable_by 的单一真源是**轴**,不是注册表产物
//
// ── 为什么需要本类(而不是直接用 `ITreatableByLookup`)──────────────────────
//   `ITreatableByLookup.TryGetPolarity(diseaseKey, actionId, out polarity)` 是
//   **正向**查询(给一对,问在不在)。病例投放要的是**反向**查询(给一个病种,问
//   有哪些技法能治它)—— 「∃ t : t 已解锁 ∧ t ∈ treatable_by(d)」是一次集合交。
//   用正向接口做这件事需要遍历全部 action(闭集),而闭集在运行期没有注册表以外的
//   枚举面;直接遍历 `axis.TreatableBy` 才是它本来的形状。
//
// ── 为什么住 `Gameplay.Boot` ──────────────────────────────────────────────
//   与 `DiseaseActionAxisTreatableByLookup` 同格:同时可见
//   `Gameplay.Presentation`(烘焙行类型)与 `Sim`(`DiseaseRegistryEntry`)。`Sim` 够不着
//   轴产物(门 A),故此表只能由装配层建、以谓词形式喂给消费方。
//
// ── 「未声明」与「声明为空」在运行期**不可区分**(2026-11-09 实测,如实登记)─────
//   本类**做不到**区分它们,因为产物也做不到:
//   `treatable_by` 只以**行**的形式存在(`TreatableByCookedRow`),作者态写
//   `"DIS_TETANUS": []` 与「完全不写这个 key」烘出来的行集**逐字节相同**。
//   ⇒ 两者在本类里都是 `IsDeclared == false`。
//   这不是本类的取舍,是**数据形状缺口**:要区分,须让烘焙器另存一份「声明过但为空」
//   的 key 集(T7 若需要,登记在那里)。今日两行的实际影响相同(TETANUS /
//   NEURASTHENIA 目前都无可投放技法),故本件不改产物、不伪造区分。
//   ⚠️ 本缺口**不影响**技法门的核心判据 —— 「声明了 N≥1 门且都没解锁」是能表达的,
//   而那正是用户裁定的「未开启前不出现」所依赖的那一半。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// `treatable_by` 的**反向**索引:病种 key → 能治它的处置(技法)id 集。
    /// <para>构造期一次建成,运行期只读(T4 的技法门逐次病人出现时查一次)。</para>
    /// </summary>
    public sealed class TechniqueDrawIndex
    {
        private static readonly int[] NoTechniques = Array.Empty<int>();

        private readonly Dictionary<string, int[]> _byDiseaseKey;

        /// <summary>已声明处置关系的病种数(观测面;不含未声明者)。</summary>
        public int DeclaredDiseaseCount => _byDiseaseKey.Count;

        /// <summary>
        /// 从处置轴烘焙行集建索引。
        /// </summary>
        /// <param name="rows">`DiseaseActionAxisDataSet.TreatableBy`。</param>
        /// <exception cref="ArgumentNullException"><paramref name="rows"/> 为 null。
        /// <para>⚠️ null 与空集**不是**同一状态:null = 未接线(装配层拿不到轴产物),
        /// 空集 = 轴里一条处置关系都没有。前者由调用方决定「不拦」,后者是一条
        /// 真实的「所有病种都不可投放」—— 本类不与调用方抢这个判断。</para></exception>
        public TechniqueDrawIndex(IReadOnlyList<TreatableByCookedRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            // 重复 (病种, 处置) 行**不在此拒收** —— 那是 `DiseaseActionAxisTreatableByLookup`
            // 的既有判据(构造期 fail-loud),两者消费同一次装载的同一个轴。本类若再抛一次,
            // 同一条数据缺陷会有两处各自措辞的报错,反而更难归因。此处按 HashSet 去重,
            // **不静默改变判定**(同一对重复 ⇒ 集合语义下逐位相同)。
            var sets = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++)
            {
                TreatableByCookedRow row = rows[i];
                // 空 key = 产物损坏(烘焙期 R1 / DC 校验已拒)。跳过而不抛:本类只服务
                // 投放门,键坏了会让**所有**病都落进「未声明」分支 —— 那是可见的(世界没病人),
                // 不比抛异常更隐蔽。真正的形状校验归 codec / binder,不归运行期索引。
                if (string.IsNullOrEmpty(row.DiseaseKey)) continue;

                if (!sets.TryGetValue(row.DiseaseKey, out HashSet<int> actions))
                {
                    actions = new HashSet<int>();
                    sets.Add(row.DiseaseKey, actions);
                }
                actions.Add(row.ActionId);
            }

            _byDiseaseKey = new Dictionary<string, int[]>(sets.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<int>> kv in sets)
            {
                var arr = new int[kv.Value.Count];
                kv.Value.CopyTo(arr);
                // 序无关判据,但固定成升序:诊断日志与夹具断言不必依赖哈希序。
                Array.Sort(arr);
                _byDiseaseKey.Add(kv.Key, arr);
            }
        }

        /// <summary>
        /// 该病种是否在轴里**声明过**处置关系(哪怕声明为空数组)。
        /// </summary>
        public bool IsDeclared(string diseaseKey)
            => !string.IsNullOrEmpty(diseaseKey) && _byDiseaseKey.ContainsKey(diseaseKey);

        /// <summary>
        /// 该病种声明过的处置 id(升序;未声明或声明为空 ⇒ 空数组,**不抛**)。
        /// </summary>
        public IReadOnlyList<int> TechniquesOf(string diseaseKey)
            => !string.IsNullOrEmpty(diseaseKey) && _byDiseaseKey.TryGetValue(diseaseKey, out int[] arr)
                ? arr
                : NoTechniques;
    }
}
