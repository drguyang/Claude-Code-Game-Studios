// sprint-08 S1.1 件 C · 病名册条目源(已见集 → 词表反查 → lexicon_id 列表)(2026-10-12)
//
// 权威来源:
//   sprint-08 S1 计划 §2 件 C(甲案 30 novelty 最小链 —— 用户 2026-10-12 裁定)
//   casebook-39.md §5.2 / §10 / §12 —— 病名册 = 词表「已见过」过滤视图;
//     条目按**遇见序**;零分类 / 零搜索 / 零自动补全 / 零体征线索(AC-8-43/8-44)
//   AC-37-15 —— disease_id **不得进呈现层**;本件只出 `lexicon_id` 列表(守密)
//   ADR-014 §三 —— 词表 = `case_judgment_lexicon`(S1.0 烘焙,版本化 ordinal)
//   OQ-CB-1 甲 —— 键鼠与手柄同走病名册指认(统一 `lexicon_id` 通道)
//
// ⚠️ **守密的落点**:本件在装配层读 `disease_ids`(词表外键)+ 已见病种集,
//   反查后**只把 `lexicon_id` 交出去**(件 E 的 `SeenLexiconIds`)—— 42 侧永不见
//   disease_id(AC-37-15 呈现层 DTO 递归守卫覆盖)。
//
// ⚠️ **序 = 词表序(append-only ordinal 递增)**:条目按 lexicon_id 升序输出,
//   与「遇见序」的关系:词表本身按作者态遇见序排定 ordinal(S1.0 `case_judgment_lexicon.json`
//   的 entries 序),故 lexicon_id 升序 ≡ 词表序。⚠️ 登记:若未来词表 author 态重排 ordinal,
//   本序随之变 —— 归词表作者态纪律(S1.0 §AC-37-35 域),非本件自定。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Casebook;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 病名册条目源 —— 把「已见病种集」经词表 `disease_ids` 反查成可指认的 `lexicon_id` 列表。
    /// <para><b>条目集 = 词表中 `disease_ids ∩ 已见集 ≠ ∅` 的行</b>(casebook-39 §5.2);
    /// 孤儿条目(`disease_ids` 空)恒不入册(无病种可关联 ⇒ 不可由「见过」触发)。</para>
    /// <para>纯函数:零状态、零缓存(每次读都重算 —— 零第二真源,承 ADR-013 §9 C3)。</para>
    /// </summary>
    public sealed class LexiconBookSource
    {
        private readonly IReadOnlyList<JudgmentLexemeRow> _rows;

        /// <param name="rows">词表行集(S1.0 烘焙产物;装配层经 provider / 磁盘装载)。</param>
        public LexiconBookSource(IReadOnlyList<JudgmentLexemeRow> rows)
        {
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        }

        /// <summary>词表总行数(观测面;含孤儿条目)。</summary>
        public int TotalRows => _rows.Count;

        /// <summary>
        /// 某玩家已见病种集 ⇒ 可指认的 `lexicon_id` 升序列表(词表序)。
        /// </summary>
        /// <param name="seenDiseaseIds">已见病种 id 集(升序去重;来自 <c>SeenDiseaseProjector.SeenDiseaseIds</c>)。</param>
        /// <returns>可指认条目 id 升序数组;空 = 无已见病(病名册空)。</returns>
        /// <remarks>复杂度 O(行数 × 单行病种数);词表规模小(37 行量级),线性扫即可。</remarks>
        public int[] PointableLexiconIds(int[] seenDiseaseIds)
        {
            if (seenDiseaseIds == null || seenDiseaseIds.Length == 0)
                return Array.Empty<int>();

            var seen = new HashSet<int>(seenDiseaseIds);
            var ids = new List<int>();
            for (int i = 0; i < _rows.Count; i++)
            {
                JudgmentLexemeRow row = _rows[i];
                int[] diseaseIds = row.DiseaseIds;
                if (diseaseIds == null) continue;

                for (int j = 0; j < diseaseIds.Length; j++)
                {
                    if (seen.Contains(diseaseIds[j]))
                    {
                        ids.Add(row.LexiconId);
                        break;   // 该行已命中,不重复加入(多对一:一条词可关联多病种)
                    }
                }
            }

            // 词表序(lexicon_id 升序)—— 防御性排序:词表 author 态若乱序仍输出稳定序
            ids.Sort();
            return ids.ToArray();
        }

        /// <summary>按 lexicon_id 取词条文本(病名;供指认落笔后的显示 / 测试读面)。
        /// <para>⚠️ 只回文本,**不回 disease_ids**(守密:调用方拿不到病种外键)。</para></summary>
        /// <param name="lexiconId">词条 id(≥ 1;0 = 空栏哨兵)。</param>
        /// <returns>词条文本;未找到 ⇒ <c>null</c>。</returns>
        public string TextOf(int lexiconId)
        {
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].LexiconId == lexiconId)
                    return _rows[i].Text;
            return null;
        }
    }
}
