// sprint-08 S1.0 · 病名册词表(37 Judgment 词表)运行期类型(2026-10-12)
//
// 权威来源:
//   case-system.md D-37-C(词表 = case_judgment_lexicon,ADR-014 烘焙,版本化 ordinal)·
//   ADR-008 §三(lexicon_id u16 值域;lexicon_id=0 ∧ freehand="" = 空栏)·
//   AC-37-35(词表与病种注册表禁逐条一一对应 —— 构建期检查)·
//   ADR-014 §三(阶段 2 白名单/闭集校验;构建期硬失败)·
//   casebook-39.md §5.2(病名册 = 本词表的「已见过」过滤视图;OQ-CB-1 甲:指认通道)
//
// ⚠️ lexicon_id = 0 是**保留哨兵**(空栏,ADR-008)—— 真条目从 1 起;绑定期拒 0。
// ⚠️ disease_ids 为空数组 = **孤儿条目**(不关联任何病种)—— 它们是 AC-37-35
//   非一一对应的结构保证,也是「见过此名但病种未入册」的内容表达位。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Casebook
{
    /// <summary>单条病名册词条行(`<c>case_judgment_lexicon.json</c>` 的一行)。
    /// <para><see cref="DiseaseIds"/> 为**多对一**可表达(一条词可关联多病种);空 = 孤儿条目。</para></summary>
    public readonly struct JudgmentLexemeRow
    {
        /// <summary>版本化 ordinal(u16 域;**≥1** —— 0 保留为空栏哨兵,ADR-008)。append-only。</summary>
        public readonly int LexiconId;

        /// <summary>呈现文本(病名;非空 —— 空名在病名册上不可指认)。</summary>
        public readonly string Text;

        /// <summary>关联病种 id 集(9 的病种注册表外键;可空 = 孤儿条目;行内无重复)。</summary>
        public readonly int[] DiseaseIds;

        public JudgmentLexemeRow(int lexiconId, string text, int[] diseaseIds)
        {
            LexiconId = lexiconId;
            Text = text;
            DiseaseIds = diseaseIds ?? Array.Empty<int>();
        }
    }

    /// <summary>词表烘焙产物的运行期数据集(行集 + 头部版本)。
    /// <para>⚠️ <b>struct(2026-10-12 S1.1 件 D 改)</b>:原 sealed class 无法经
    /// <c>IDataProvider.Load&lt;T&gt;() where T : struct</c> 装载(该约束是 ADR-014 §五 的
    /// 装载面契约,两个 sibling 数据集 <c>DiagnosisSignDataSet</c> / <c>RecipeDataSet</c>
    /// 皆 struct)⇒ 词表须同形。字段改 public field(object initializer 兼容),
    /// 语义零变化(只承载行集 + 两版本号)。</para></summary>
    public struct JudgmentLexiconDataSet
    {
        /// <summary>词条行集(源 JSON 序;append-only ordinal 递增)。</summary>
        public IReadOnlyList<JudgmentLexemeRow> Rows;

        /// <summary>源数据集内容哈希派生的 ConfigVersion(u32;ADR-014 §五)。</summary>
        public uint ConfigVersion;

        /// <summary>源 <c>schema_version</c> 回声(u32)。</summary>
        public uint SchemaVersion;
    }

    /// <summary>词表校验器(AC-37-35 结构半 + ordinal/形状闭集;构建期硬失败)。
    /// <para>每条校验独立判据(违例各红);调用点唯一 = <c>Editor.Tools.Bake.JudgmentLexiconBinder.Bind</c>。</para></summary>
    public static class JudgmentLexiconValidator
    {
        /// <summary>单行 <c>disease_ids</c> 上限(u8 承载 —— 与 <c>JudgmentLexiconCookedWriter</c>
        /// 的 <c>(byte)disease_count</c> 同源)。超界 = 静默截断(256 → 0)⇒ 硬失败。</summary>
        public const int MaxDiseaseIdsPerRow = 255;

        /// <summary>AC-37-35 词表侧必要条件:词表**不得**是「逐条单链且互异」的双射候选。
        /// <para>若每行恰一个 disease_id 且全体互异,则词表与**任意**同基数注册表都可完美配对
        /// ⇒ 拒。孤儿条目或多对一链接的存在使其结构性不可能双射。</para>
        /// <para>⚠️ 与真实病种注册表的**全量交叉检查**归 disease_registry.json 烘焙落地轮
        /// (登记 NOT-RUN —— 注册表现为代码内联 fallback,非数据文件,交叉检查无输入)。</para></summary>
        public static void Validate(IReadOnlyList<JudgmentLexemeRow> rows)
        {
            if (rows == null || rows.Count == 0)
                throw new InvalidOperationException("词表为空表 —— 拒以空集冒充绿 [lexicon-empty]");

            var seenIds = new HashSet<int>();
            int singleLinked = 0;
            var linkedDiseases = new HashSet<int>();

            for (int i = 0; i < rows.Count; i++)
            {
                JudgmentLexemeRow row = rows[i];
                string where = $"entries[{i}](lexicon_id={row.LexiconId})";

                if (row.LexiconId < 1)
                    throw new InvalidOperationException(
                        $"{where}:lexicon_id ≥ 1(0 保留为空栏哨兵,ADR-008)[lexicon-zero]");
                if (!seenIds.Add(row.LexiconId))
                    throw new InvalidOperationException(
                        $"{where}:lexicon_id 重复 —— ordinal 唯一 [lexicon-dup]");
                if (i > 0 && row.LexiconId <= rows[i - 1].LexiconId)
                    throw new InvalidOperationException(
                        $"{where}:lexicon_id 须严格递增(append-only ordinal,D-21-13)" +
                        $" —— 上一行 {rows[i - 1].LexiconId} [lexicon-order]");
                if (string.IsNullOrEmpty(row.Text))
                    throw new InvalidOperationException(
                        $"{where}:text 非空 [lexicon-text]");
                if (row.DiseaseIds.Length > MaxDiseaseIdsPerRow)
                    throw new InvalidOperationException(
                        $"{where}:disease_ids 数 {row.DiseaseIds.Length} > {MaxDiseaseIdsPerRow}" +
                        "(u8 承载上界;超界会静默截断)[lexicon-disease-count]");

                if (row.DiseaseIds.Length == 1)
                {
                    singleLinked++;
                    if (!linkedDiseases.Add(row.DiseaseIds[0]))
                        throw new InvalidOperationException(
                            $"{where}:同一病种被多行单链 —— 与「行内多链」表达位重复," +
                            "应合并为一行多链 [lexicon-multi-row-link]");
                }
                else if (row.DiseaseIds.Length > 1)
                {
                    var rowSeen = new HashSet<int>();
                    foreach (int d in row.DiseaseIds)
                    {
                        if (d < 1)
                            throw new InvalidOperationException(
                                $"{where}:disease_id ≥ 1 [lexicon-disease-id]");
                        if (!rowSeen.Add(d))
                            throw new InvalidOperationException(
                                $"{where}:disease_ids 行内重复 {d} [lexicon-disease-dup]");
                    }
                }

                foreach (int d in row.DiseaseIds)
                    if (d < 1)
                        throw new InvalidOperationException(
                            $"{where}:disease_id ≥ 1 [lexicon-disease-id]");
            }

            // AC-37-35 词表侧:全行单链且互异 ⇒ 双射候选,拒
            if (singleLinked == rows.Count && linkedDiseases.Count == rows.Count)
                throw new InvalidOperationException(
                    "词表全部条目单链且病种互异 —— 双射候选,违 AC-37-35" +
                    " (须含孤儿条目或多对一链接)[AC-37-35]");
        }
    }
}
