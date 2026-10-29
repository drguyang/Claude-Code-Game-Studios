// 42 侧字幕元件的判据(`AC-44-15` `[L]` 半 · story-026 · 2026-10-29)。
//
// ── 权威来源 ────────────────────────────────────────────────────────────────
//   · `design/gdd/skeuomorphic-ui.md` **E-12b**(`:997-1000`)—— 「42 不持有语声状态、不判何时显示」
//   · `design/accessibility-requirements.md:94-95`(字幕默认开 · 说话人识别「名字前置」·
//     颜色区分仅作假若**非颜色差异同时存在**)
//   · `production/epics/skeuomorphic-ui/story-026-subtitle-rendering.md`
//
// ── 判据面 ──────────────────────────────────────────────────────────────────
// ① 元件真的能渲染文本(不是「字段存在」);
// ② 说话人**前置**且**非颜色差异**(方括号 —— 承色-only 铁律);
// ③ **无时机逻辑**(反射断言:不含 Timer / duration / autoHide 类成员);
// ④ 行数上限真的截断;
// ⑤ 初始不可见(上游未说「显示」前不得自己冒出来)。

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    /// <summary>`SubtitleLineView` 的元件级判据。</summary>
    public sealed class SubtitleLineViewTest
    {
        /// <summary>**Show 后文本真的呈现** —— 核心判据。
        /// <para>⚠️ 判据面 = `Label.text` 与 `display`,不是「我调过 Show」。</para></summary>
        [Test]
        public void test_show_rendersText()
        {
            var view = new SubtitleLineView();
            view.Show(new SubtitleLineData("病人", "咳…咳咳…"));

            Assert.That(view.CurrentText, Is.EqualTo("咳…咳咳…"));
            Assert.That(view.style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "Show 后须可见 —— 仍为 None = 元件没渲染");
            Assert.That(view.Q<Label>().text, Does.Contain("咳…咳咳…"),
                "Label 文本须含正文(否则「渲染了」只是内部字段)");
        }

        /// <summary>**说话人前置** —— `accessibility-requirements.md:95`「名字前置」。
        /// <para>⚠️ 且须**非颜色差异**:本元件用**方括号**(形状),不靠颜色区分说话人。</para></summary>
        [Test]
        public void test_show_putsSpeakerBeforeText()
        {
            var view = new SubtitleLineView();
            view.Show(new SubtitleLineData("老者", "哼了一声。"));

            string rendered = view.Q<Label>().text;

            Assert.That(rendered, Does.StartWith("【老者】"),
                $"说话人须在正文**之前**且带方括号(非颜色差异)—— 实得「{rendered}」");
            Assert.That(rendered.IndexOf("老者", StringComparison.Ordinal),
                Is.LessThan(rendered.IndexOf("哼了一声", StringComparison.Ordinal)),
                "说话人名须出现在正文之前(名字前置)");
        }

        /// <summary>**无说话人 ⇒ 只出正文**(不得留空方括号)。</summary>
        [Test]
        public void test_emptySpeaker_rendersTextOnly()
        {
            var view = new SubtitleLineView();
            view.Show(new SubtitleLineData(string.Empty, "风声渐紧。"));

            Assert.That(view.Q<Label>().text, Is.EqualTo("风声渐紧。"),
                "无说话人 ⇒ 不得出现空的「【】」");
        }

        /// <summary>**42 不判时机** —— 反射断言:元件类型不含 `Timer` / `duration` / `autoHide`
        /// 类成员。
        /// <para>⚠️ 这是 `E-12b` 逐字要求「42 不持有语声状态、**不判何时显示**」的机械判据。
        /// 若日后有人加一个 `autoHideSeconds`,本测会红。</para></summary>
        [Test]
        public void test_noTimingMembers_reflection()
        {
            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                                   | BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.DeclaredOnly;

            string[] forbidden = { "Timer", "Duration", "AutoHide", "Timeout", "Expiry", "Seconds" };

            foreach (MemberInfo m in typeof(SubtitleLineView).GetMembers(All))
            {
                foreach (string bad in forbidden)
                {
                    Assert.That(m.Name.IndexOf(bad, StringComparison.OrdinalIgnoreCase), Is.LessThan(0),
                        $"元件不得含「{bad}」类成员(`{m.Name}`)—— " +
                        "E-12b:42 不判何时显示,时机归 44");
                }
            }
        }

        /// <summary>**行数上限真的截断**(P0 至多 2 行,超出以省略号结尾)。</summary>
        [Test]
        public void test_truncatesBeyondMaxLines()
        {
            var view = new SubtitleLineView();
            view.Show(new SubtitleLineData("病人", "第一行\n第二行\n第三行\n第四行"));

            string rendered = view.Q<Label>().text;

            Assert.That(rendered, Does.Contain("第一行"));
            Assert.That(rendered, Does.Contain("第二行"));
            Assert.That(rendered, Does.Not.Contain("第三行"), "超出 MaxLines 的行须被截掉");
            Assert.That(rendered, Does.EndWith("…"), "截断须有省略号(不得静默丢字)");
        }

        /// <summary>**未 Show 前不可见** —— 元件不得自己冒出来。
        /// <para>⚠️ 这条钉的是「42 不判何时显示」的另一面:它连「初始就显示」都不做。</para></summary>
        [Test]
        public void test_initiallyHidden()
        {
            var view = new SubtitleLineView();

            Assert.That(view.style.display.value, Is.EqualTo(DisplayStyle.None),
                "构造后须不可见 —— 由上游 `Show` 才显示");
            Assert.That(view.Q<Label>().text, Is.Empty, "构造后文本须为空");
        }

        /// <summary>**Hide 清空且不可见**。</summary>
        [Test]
        public void test_hide_clearsAndHides()
        {
            var view = new SubtitleLineView();
            view.Show(new SubtitleLineData("病人", "咳…"));
            view.Hide();

            Assert.That(view.style.display.value, Is.EqualTo(DisplayStyle.None), "Hide 后须不可见");
            Assert.That(view.Q<Label>().text, Is.Empty, "Hide 后文本须清空(不得留残影)");
        }

        /// <summary>**实现 `ISubtitleSink`** —— 42 侧唯一接收口的落地件。</summary>
        [Test]
        public void test_implementsSubtitleSink()
        {
            Assert.That(typeof(ISubtitleSink).IsAssignableFrom(typeof(SubtitleLineView)), Is.True,
                "`SubtitleLineView` 须实现 `ISubtitleSink`(否则上游无法投递)");
        }
    }
}
