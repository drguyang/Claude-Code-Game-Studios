// 教学口述的**文案跨表门**:48 内容表 `narration[].text` ↔ 44 事件表 `rows[].subtitle_text`。
//
// 权威来源:
//   `design/gdd/tutorial-and-onboarding.md` **AC-48-15** —— 「教学口述复用 `AC-44-15` 字幕管线;
//     教学口述零字幕是禁项」
//   `AC-44-15` —— 字幕文本的**单一真源 = 44 事件表**
//   ADR-014 §三 —— 作者态数据外部化 + 构建期校验
//   `TD-tutorial-01`(债册,2026-11-07 立;用户同日裁定**甲案**:对齐两表 + 补跨表校验)
//
// ── 立门缘由(实测,不是预防性洁癖)───────────────────────────────────────
// 2026-11-06 收口轮实测:同一句口述在两处各写一份,而**四条全部不一致**——
//   48 内容表:`先看看这个人。气色、神态,都在脸上。`
//   44 事件表:`喘着气,气色、神态,都在脸上。`(配音资产就是按这一句生成的)
// 运行期字幕**只读事件表**(`TutorialMediaDriver` S25-4b 裁定),48 那份仍被
// `TutorialDispatcher` 装进 `TutorialMediaIntent.Text`,而该字段**零显示消费方**。
// 后果不是「文案不统一」这么轻:改 48 的文案**不会**改变字幕与配音 ——
// 直觉上「内容归 48」,照直觉改 = **静默无效**。而没有任何东西会红。
//
// 用户 2026-11-07 裁定**甲案**(乙案 = 删 48 的 `text`、改烘焙 schema,代价大):
// 把两表对齐到**事件表**(配音的事实),并立本门防止再次分叉。
//
// ── 判据(三条 + 三个守卫)────────────────────────────────────────────────
//   C1 **缺行** —— 48 的每条口述其 `cueId` 必须在事件表里有行。缺 ⇒ 红。
//        (这是「零字幕口述」的结构性形态:没有行就没有 `subtitle_text`。)
//   C2 **无字幕** —— 命中行的 `subtitle_text` 缺失(null)/ 空串 ⇒ 红
//        (`AC-48-15` 的禁项;S25-4b 的字幕投递在此形态下会退化成空串上屏)。
//   C3 **不一致** —— 两处文本逐字不同 ⇒ 红(报文**同时给出两边原文**,便于一眼定位)。
//   守卫 G1 48 口述 0 行 ⇒ 红(空集上 C1/C3 恒绿 —— 空洞正控)。
//   守卫 G2 事件表 0 行 ⇒ 红(同上;两者都是「判据不可执行」而非「通过」)。
//   守卫 G3 事件表 cue **重复** ⇒ 红(字幕取哪一行无定义 —— 字典投影会静默取最后一行)。
//
//   **方向**:只查「48 → 44」这一向。事件表里有字幕文本而 48 不引用它**合法**
//   (病人语声 / 环境音等本就有字幕),不得反过来报红。
//
// ── 读的是哪一份字节(如实登记盲区)───────────────────────────────────────
// 本门读**烘焙产物**(`DataCookedDisk.ReadAll` + 两个 codec),与运行期**同一份字节**,
// 不另写 JSON 解析器(第二解析器 = 第二真源)。
// ⇒ **盲区**:改了源 JSON 却忘了重烘时,本门看的是旧产物(两边都旧,可能仍自洽)⇒ 可能绿。
//    该形态由既有判据兜:`cooked_drift_all_artifacts_test`(源 ↔ 产物漂移)。**两者互补**。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// 48 口述文案 ↔ 44 字幕文本的跨表一致性门(`TD-tutorial-01` 的执行体)。
    /// <para>判据本体 = <see cref="Check"/>(纯函数,测试直接喂合成行);
    /// <see cref="RunAll"/> 从烘焙产物取数,供门链调用。</para>
    /// </summary>
    public static class TutorialNarrationSubtitleCrossRefGate
    {
        /// <summary>构建期入口 —— 经 <c>DataCookedDisk</c> 读两份烘焙产物。</summary>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll()
        {
            TutorialContentTable content;
            try
            {
                byte[] bytes = DataCookedDisk.ReadAll(TutorialContentTable.CookedFileName);
                if (!TutorialContentTable.TryDecode(bytes, out content, out string error))
                    return new List<string>
                    {
                        "[48↔44] 48 内容表解码失败(判据不可执行,非「通过」):" + error,
                    };
            }
            catch (Exception ex)
            {
                return new List<string>
                {
                    "[48↔44] 48 内容表产物装载失败(判据不可执行,非「通过」):" + ex.Message,
                };
            }

            AudioEventDataSet audio;
            try
            {
                audio = AudioEventCookedCodec.Read(
                    DataCookedDisk.ReadAll(AudioEventDataSet.CookedFileName));
            }
            catch (Exception ex)
            {
                return new List<string>
                {
                    "[48↔44] 44 事件表产物装载失败(判据不可执行,非「通过」):" + ex.Message,
                };
            }

            return Check(content.Narration, audio);
        }

        /// <summary>判据本体(纯函数,零 I/O)。</summary>
        /// <param name="narration">48 内容表的口述行。</param>
        /// <param name="audio">44 事件表(运行期只读形)。</param>
        public static List<string> Check(
            IReadOnlyList<TutorialNarrationRow> narration, AudioEventDataSet audio)
        {
            var errs = new List<string>();

            // ── 守卫 G1 / G2(空集上 C1/C3 恒绿 —— 空洞正控)──
            if (narration == null || narration.Count == 0)
            {
                errs.Add("[48↔44][G1] 48 内容表口述 **0 行** —— 判据在空集上恒绿,拒绝放行。");
                return errs;
            }
            if (audio.RowCount == 0)
            {
                errs.Add("[48↔44][G2] 44 事件表 **0 行** —— 判据在空集上恒绿,拒绝放行。");
                return errs;
            }

            // ── 事件表按 cue 建索引(同时抓重复:G3)──
            var byCue = new Dictionary<string, AudioEventRow>(StringComparer.Ordinal);
            var duplicated = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < audio.RowCount; i++)
            {
                AudioEventRow row = audio.Row(i);
                if (string.IsNullOrEmpty(row.Cue)) continue;   // 空 cue 归 44 自己的形状门
                if (!byCue.TryAdd(row.Cue, row)) duplicated.Add(row.Cue);
            }
            foreach (string cue in duplicated.OrderBy(c => c, StringComparer.Ordinal))
                errs.Add($"[48↔44][G3] 事件表 cue「{cue}」**重复** —— 字幕取哪一行无定义" +
                         "(字典投影会静默取最后一行),拒绝放行。");

            // ── C1 / C2 / C3 ──
            foreach (TutorialNarrationRow n in narration)
            {
                if (string.IsNullOrEmpty(n.CueId))
                {
                    errs.Add($"[48↔44][C1] 48 口述「{n.Id}」没有 cueId —— 无法与事件表对账。");
                    continue;
                }

                if (!byCue.TryGetValue(n.CueId, out AudioEventRow row))
                {
                    errs.Add($"[48↔44][C1] 48 口述「{n.Id}」的 cue「{n.CueId}」在事件表里**没有行** —— " +
                             "运行期派发该 cue 时不会有字幕(AC-48-15『零字幕口述是禁项』)。");
                    continue;
                }

                if (row.SubtitleText == null)
                {
                    errs.Add($"[48↔44][C2] 事件表 cue「{n.CueId}」**没有** `subtitle_text` 字段 —— " +
                             "教学口述必须有字幕(AC-48-15)。");
                    continue;
                }
                if (row.SubtitleText.Length == 0)
                {
                    errs.Add($"[48↔44][C2] 事件表 cue「{n.CueId}」的 `subtitle_text` 是**空串** —— " +
                             "等于「无字幕口述」(AC-48-15 的禁项)。");
                    continue;
                }

                if (!string.Equals(row.SubtitleText, n.Text, StringComparison.Ordinal))
                {
                    errs.Add($"[48↔44][C3] cue「{n.CueId}」两处文案**不一致** —— " +
                             $"48 内容表「{n.Text}」vs 事件表(配音的事实)「{row.SubtitleText}」;" +
                             "运行期字幕只读事件表 ⇒ 改 48 那份**不会生效**(TD-tutorial-01)。");
                }
            }

            return errs;
        }
    }
}
