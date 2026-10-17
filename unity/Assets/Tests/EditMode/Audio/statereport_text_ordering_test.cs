// story-016 · AC-44-E5 语义面 —— 文本先后的**纯判据**夹具(EditMode,可直接跑、不碰工程外状态)。
//
// 权威来源:GDD design/gdd/audio-system.md:66-78 规则二黑名单第 4 条
//   (「用音频表达本来该由玩家读数得出的结论」)。
// 被测物:`CueTextOrderingRules.TextOrderingProblems` / `ContainsConclusion`
//   —— PlayMode 走查在**判定文本先后**时调的就是这两条,不是走查人肉眼判的。
//
// ⚠️ 本件证明的是**判定器**有判别力,不是「现有字幕都合法」(后者由 PlayMode 走查件
//   对真源跑出来证明)。两者分开记,不互相顶替。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Audio
{
    /// <summary>E5 文本先后判据的判别力(命中形态:定义域收窄 + 共存互斥)。</summary>
    [TestFixture]
    public class StateReportTextOrderingTest
    {
        private static readonly string[] NoLoops = new string[0];

        [Test]
        public void textOrdering_statementHead_producesNoProblem()
        {
            List<string> problems = CueTextOrderingRules.TextOrderingProblems(
                "Voice_Cough_Damp", "咳…咳咳…", NoLoops);
            Assert.That(problems, Is.Empty,
                "以「咳」起头的字幕是可闻事实 —— 声音随后,不构成播报");
        }

        /// <summary>
        /// ⚠️ 输入刻意用**结论句 + 无结论词形**(「肺热」不在 `StatusConclusionPatterns` 内)——
        /// 否则结论词形扫描会顺手兜住本夹具,「首词须陈述式」这条守卫**删了也不会红**(首版即此假绿,
        /// 2026-10-17 证伪轮 E5-1 实测:7 passed / 0 failed)。
        /// </summary>
        [Test]
        public void textOrdering_conclusionHead_isRejected()
        {
            List<string> problems = CueTextOrderingRules.TextOrderingProblems(
                "Voice_Cough_Damp", "肺热已退…咳", NoLoops);
            Assert.That(problems, Is.Not.Empty, "以结论句式起头的字幕必须先红(仅首词守卫能抓到)");
            Assert.That(problems[0], Does.Contain("首个词形"));
        }

        [Test]
        public void textOrdering_conclusionAnywhere_includingMidword_isRejected()
        {
            List<string> problems = CueTextOrderingRules.TextOrderingProblems(
                "Voice_Cough_Damp", "咳…病危…", NoLoops);
            Assert.That(problems, Is.Not.Empty, "结论词形出现在后续词形同样命中(黑名单不限于首词)");
        }

        [Test]
        public void textOrdering_loopRowWithSubtitle_isExempt()
        {
            List<string> problems = CueTextOrderingRules.TextOrderingProblems(
                "BreathLayer_Base_Calm", "病危 喘息", new[] { "BreathLayer_Base_Calm" });
            Assert.That(problems, Is.Empty, "循环行的文本是连续性标注,不承载「先后」语义");
        }

        [Test]
        public void textOrdering_emptySubtitle_producesNoProblem()
        {
            Assert.That(CueTextOrderingRules.TextOrderingProblems("Page_Turn", null, NoLoops), Is.Empty);
            Assert.That(CueTextOrderingRules.TextOrderingProblems("Page_Turn", "", NoLoops), Is.Empty);
        }

        [Test]
        public void containsConclusion_negativeForAcousticDescriptions()
        {
            Assert.That(CueTextOrderingRules.ContainsConclusion("马蹄声由远及近"), Is.False);
            Assert.That(CueTextOrderingRules.ContainsConclusion("鸟群惊飞"), Is.False);
        }
    }
}
