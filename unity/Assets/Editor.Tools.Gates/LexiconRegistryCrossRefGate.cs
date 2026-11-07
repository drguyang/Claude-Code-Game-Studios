// 病名册词表 ↔ 病种注册表 的**跨表外键门**(2026-11-07 立 · 承真包走查)。
//
// 权威来源:
//   ADR-014 §三 —— 作者态数据外部化 + 构建期校验
//   `design/gdd/casebook-39.md` §5.2 / §12 —— 病名册 = 词表「已见过」过滤视图,
//     反查的键是 `disease_ids`,即 **9 的病种注册表外键**
//   `JudgmentLexiconValidator` 的自陈缺口(就地加注处)—— 「与真实病种注册表的全量
//     交叉检查归 disease_registry.json 烘焙落地轮(登记 NOT-RUN)」;
//     注册表已于 2026-10-22(B1)落地 ⇒ 该登记义务**今日可执行**,本门即它的执行体
//
// ── 立门缘由(为什么不能只靠人看)───────────────────────────────────────
// 2026-11-07 真包走查实测:`case_judgment_lexicon.json` 里「伤寒」的外键是 `[1]`,
// 而 `disease_registry.json` 的 id 段是 **2..17 —— 没有 1**。后果不是「少一条词条」:
// **整本病名册在生产里恒空**(三环反查的第三环 `pointable` 恒 0),
// 教学链 `judge` 步因此**永远不可能发火**,而**构建、烘焙、全部夹具当时都是绿的**。
// 根因是 `JudgmentLexiconBinder.ReadDiseaseIds` 只校验「是数组 / 元素是整数」
// (形状),**不比对注册表**(跨表语义)—— 悬空外键静默烘进产物。
//
// 时间线可核:词表烘于 `6223ca13`(2026-10-12),当时生产只有组合根内联 fallback 注册表
// (`DiseaseId = 1`)⇒ `1` 看起来是对的;2026-10-22 B1 接真表(id 从 2 起)后,
// 这个 `1` **当场悬空**,而没有任何东西报错。
//
// ── 判据(两条 + 两条空集守卫)────────────────────────────────────────────
//   C1 悬空外键 —— 词表每行的 `disease_ids` 逐值必须 ∈ 注册表 id 集。缺 ⇒ 红。
//   C2 方向 —— **只查这一向**。注册表里有病种没有对应词条**合法**(孤儿条目是设计内形态,
//      AC-37-35 的双射候选拒还**依赖**它),不得反过来报红。
//   守卫 G1 词表 0 行 ⇒ 红(空集上 C1 恒绿 —— 空洞正控)。
//   守卫 G2 注册表 0 行 ⇒ 红(同理;两者都是「判据不可执行」而非「通过」)。
//
// ── 读的是哪一份字节(如实登记盲区)───────────────────────────────────────
// 本门读**烘焙产物**(`DataCookedDisk.LoadJudgmentLexicon` / `LoadDiseaseRegistry`)
// —— 与运行期**同一个 codec、同一份字节**,不另写 JSON 解析器(第二解析器 = 第二真源)。
// ⇒ **盲区**:改了源 JSON 但忘了重烘时,本门看的是旧产物(仍是自洽的旧数据)⇒ 可能绿。
//    该形态由另一条既有判据兜:`cooked_drift_all_artifacts_test`(源 ↔ 产物漂移)。
//    **两者互补,缺一不可** —— 本门的绿**不得**被读成「源数据也一致」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using DaYiJingCheng.Sim;   // DiseaseRegistryEntry

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// 病名册词表 ↔ 病种注册表的跨表外键门(ADR-014 阶段 2 的跨表补充)。
    /// <para>判据本体 = <see cref="RunAll(IReadOnlyList{JudgmentLexemeRow}, IReadOnlyList{DiseaseRegistryEntry})"/>;
    /// 无参重载从烘焙产物取数,供门链调用。</para>
    /// </summary>
    public static class LexiconRegistryCrossRefGate
    {
        /// <summary>构建期入口 —— 经 <c>Application.dataPath</c> 读两份烘焙产物。</summary>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll()
        {
            JudgmentLexiconDataSet lexicon;
            DiseaseRegistryDataSet registry;
            try
            {
                lexicon = DataCookedDisk.LoadJudgmentLexicon();
            }
            catch (Exception ex)
            {
                return new List<string>
                {
                    "[LexiconCrossRef] 词表产物装载失败(判据不可执行,非「通过」):" + ex.Message,
                };
            }
            try
            {
                registry = DataCookedDisk.LoadDiseaseRegistry();
            }
            catch (Exception ex)
            {
                return new List<string>
                {
                    "[LexiconCrossRef] 病种注册表产物装载失败(判据不可执行,非「通过」):" + ex.Message,
                };
            }

            return RunAll(lexicon.Rows, registry.Rows);
        }

        /// <summary>
        /// 判据本体(**纯函数**,不碰文件系统 —— 夹具可直接喂违例数据驱动同一台机器)。
        /// </summary>
        /// <param name="lexiconRows">词表行集(烘焙产物序)。</param>
        /// <param name="registryRows">病种注册表行集(烘焙产物序)。</param>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll(IReadOnlyList<JudgmentLexemeRow> lexiconRows,
                                          IReadOnlyList<DiseaseRegistryEntry> registryRows)
        {
            var errs = new List<string>();

            // ── 守卫:空集上判据恒绿(空洞正控)⇒ 显式转红 ──
            if (lexiconRows == null || lexiconRows.Count == 0)
            {
                errs.Add("[LexiconCrossRef] 词表 0 行 —— 空集上无物可查,判据恒真(非「通过」)");
                return errs;
            }
            if (registryRows == null || registryRows.Count == 0)
            {
                errs.Add("[LexiconCrossRef] 病种注册表 0 行 —— 无被引方,悬空外键无从判定(非「通过」)");
                return errs;
            }

            var known = new HashSet<int>();
            for (int i = 0; i < registryRows.Count; i++)
                known.Add(registryRows[i].DiseaseId);

            for (int i = 0; i < lexiconRows.Count; i++)
            {
                JudgmentLexemeRow row = lexiconRows[i];
                int[] ids = row.DiseaseIds;
                if (ids == null) continue;                 // 孤儿条目(合法形态,非缺字段)

                for (int j = 0; j < ids.Length; j++)
                {
                    if (known.Contains(ids[j])) continue;
                    errs.Add(
                        $"[LexiconCrossRef] lexicon_id={row.LexiconId}「{row.Text}」的 " +
                        $"disease_ids[{j}]={ids[j]} 在病种注册表里不存在 —— " +
                        "悬空外键(case_judgment_lexicon.json ↔ disease_registry.json)。" +
                        "后果不是少一条词条:整本病名册会在生产里恒空(反查第三环 pointable=0)。" +
                        "修法 = 改词表外键指真实病种 id,重烘,禁止把注册表改成迁就这个 id");
                }
            }

            return errs;
        }
    }
}
