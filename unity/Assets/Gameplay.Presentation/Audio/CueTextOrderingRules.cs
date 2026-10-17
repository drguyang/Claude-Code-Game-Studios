// story-016 · AC-44-E5(无提示音铁律 · 语义面)的**文本先后判据**。
//
// 权威来源:GDD design/gdd/audio-system.md:66-78 规则二黑名单第 4 条
//   「用音频表达本来就该由玩家读数得出的结论」;ADR-018 §六(AC-44-09 BLOCKING)。
//
// ⚠️ 为什么住在 `Gameplay.Presentation`(44 的装配)而不是测试里:
//   PlayMode 走查(`StateReportWalkthroughProbe`)与 EditMode 纯判据夹具要调**同一条**判据。
//   判据若只活在 PlayMode 测试装配里,EditMode 够不着它,就只剩「走查人肉眼判」——
//   这正是 story-016 首版 E5 的形态(写不出夹具)。⇒ 判据本体住生产装配,两件夹具各测一面:
//     EditMode  ← 判据器自身的判别力(注入文本);
//     PlayMode  ← 判据器对**真源**字幕的结论(读真 `audio_events.json`)。
//
// ⚠️ 本类**不做音频判定**,只做「文字与声音的先后」:语声 cue 的字幕若以结论句式起头、
//   或含状态结论词形,即等于音频在给玩家**已经能读出的结论**加提示音。它证明得了
//   「字幕没有越位」,**证明不了**「声音本身不是在报状态」—— 后者只有耳朵能给(见 PlayMode 件头注)。
// ⚠️ 零 Unity 依赖(纯 C#),可被 EditMode 直驱。

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>E5 的文本先后判据(纯函数)。</summary>
    public static class CueTextOrderingRules
    {
        /// <summary>状态结论词形(黑名单第 4 条)。故意保守:只收「已出结论」句式,不收症状描述。</summary>
        public static readonly IReadOnlyList<string> StatusConclusionPatterns = new[]
        {
            "确诊", "病危", "已退热", "好转", "恶化", "康复", "不治", "无救", "危在旦夕", "命数已尽",
            "判定成功", "判定失败", "过关", "失败",
        };

        /// <summary>陈述式起头(可闻事实)→ 声音随后 = 合法;其余一律视为结论式。</summary>
        public static readonly IReadOnlyList<string> StatementHeads = new[]
        {
            "咳", "喘", "喊", "叫", "呻吟", "嘶", "哼", "叹",
        };

        /// <summary>
        /// 该 cue **凭什么**不是状态播报(GDD :66-78 规则二白名单的归属三选一 + 替代读法锚点)。
        /// <para>⚠️ 这是**归属三选一**,不是音频判定 —— 走查人据此把「行为反馈 / 状态播报」
        /// 之争落到无分歧的读法上;读法本身错 = 走查人须在记录里反驳,机器半不替人判。</para>
        /// </summary>
        /// <param name="cue">cue 名。</param>
        /// <param name="category">事件表 <c>whitelist_category</c>。</param>
        /// <param name="subtitle">字幕(可空)。</param>
        public static string Reason(string cue, string category, string subtitle)
        {
            string basis = $"{cue}: whitelist_category = 「{category}」∈ 白名单;替代读法 = 有(manifest §四)";

            bool physiological = cue.StartsWith("BreathLayer_", StringComparison.Ordinal) ||
                                 cue == "Voice_Cough_Damp" ||
                                 cue == "WorldBreath_Baseline";
            if (physiological)
                return "physiology:" + basis + ";症状的连续变化本身即病人状态,不是「宣告」状态变化";

            if (string.IsNullOrEmpty(subtitle))
                return "player_action:" + basis + ";该 cue 不承载字幕 —— 玩家凭声音描述动作,无结论被读出";

            return "world_ambience:" + basis + ";该 cue 承载字幕「" + subtitle + "」—— 声音先于文字,音形为可闻事实";
        }

        /// <summary>字幕拆词(空白与句读均为分隔;省略号亦断词)。</summary>
        public static List<string> Split(string text)
        {
            var parts = new List<string>();
            if (string.IsNullOrEmpty(text)) return parts;
            foreach (string p in Regex.Split(text, "[\\s…、，,。！!？?]+"))
                if (p.Length > 0) parts.Add(p);
            return parts;
        }

        /// <summary>词语是否含状态结论词形。</summary>
        public static bool ContainsConclusion(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (string p in StatusConclusionPatterns)
                if (text.IndexOf(p, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>首词是否为陈述式(可闻事实)。</summary>
        public static bool IsStatementHead(string firstWord)
        {
            if (string.IsNullOrEmpty(firstWord)) return false;
            foreach (string head in StatementHeads)
                if (firstWord.StartsWith(head, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// 文本先后判定:非空、非循环行字幕,须①以陈述式起头 ②任一词形不含结论词形。
        /// </summary>
        /// <param name="cue">cue 名(诊断)。</param>
        /// <param name="subtitle">字幕原文(空 ⇒ 无判定)。</param>
        /// <param name="loopsWithSubtitle">带字幕的**循环行**清单(其文本是连续性标注,不承载先后语义)。</param>
        public static List<string> TextOrderingProblems(
            string cue, string subtitle, IReadOnlyList<string> loopsWithSubtitle)
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(subtitle)) return problems;
            if (loopsWithSubtitle != null)
                foreach (string loop in loopsWithSubtitle)
                    if (string.Equals(loop, cue, StringComparison.Ordinal)) return problems;

            List<string> parts = Split(subtitle);
            if (parts.Count == 0)
            {
                problems.Add($"[E5 · 顺序] 「{cue}」subtitle_text 拆不出词形:「{subtitle}」");
                return problems;
            }

            if (!IsStatementHead(parts[0]))
                problems.Add($"[E5 · 顺序] 「{cue}」首个词形以结论句式起头:「{parts[0]}」—— 文字先于声音陈述结论");

            foreach (string p in parts)
                if (ContainsConclusion(p))
                    problems.Add($"[E5 · 顺序] 「{cue}」含状态结论词形「{p}」—— 音频在播报该结论");

            return problems;
        }
    }
}
