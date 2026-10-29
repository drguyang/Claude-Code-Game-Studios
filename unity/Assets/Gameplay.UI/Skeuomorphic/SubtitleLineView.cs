// 语声字幕的**呈现元件**(`AC-44-15` `[L]` 半 · 42 侧 · 2026-10-29)。
//
// ── 权威来源 ────────────────────────────────────────────────────────────────
//   · `design/gdd/skeuomorphic-ui.md` **E-12b**(`:997-1000`)—— 「字幕 = 文本呈现,
//     走元件层文本组件(F2 文本贴合 + USS 主题变量);**42 不持有语声状态、不判何时显示**」
//   · `design/accessibility-requirements.md:94-95`(字幕默认开 · 说话人识别「名字前置」·
//     颜色区分仅作假若**非颜色差异同时存在**)
//   · `production/epics/skeuomorphic-ui/story-026-subtitle-rendering.md`
//
// ── 设计边界(逐条对齐 E-12b)─────────────────────────────────────────────────
// ① **不判时机**:无 `Timer` / `duration` / `autoHide` 类成员 —— 由 `ISubtitleSink`
//    的调用方(上游发射点)决定何时 `Show` / `Hide`。
// ② **不持语声状态**:不记「上一条说了什么」用于业务判断;`_current` 只是**渲染所需**的
//    当前行(与 `AudioBusEntry` 的「只持壳不持值」同款纪律)。
// ③ **说话人前置 + 非颜色差异**:说话人名放在正文**之前**;视觉区分同时用
//    **字重 / 方括号**(不单靠颜色 —— 色-only 铁律)。
//
// ── 为什么是 UI Toolkit 而不是 UGUI ─────────────────────────────────────────
// ADR-013:平面拟物 UI(脉案 / 存档位 / 纸质地图 / 教学)**走 UI Toolkit**;
// UGUI world canvas 只补世界空间与 VR。字幕是**屏幕空间平面 UI** ⇒ UI Toolkit。

using UnityEngine.UIElements;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>屏幕底部的一行字幕(P0 最小呈现;49 可细化规格)。</summary>
    /// <remarks>⚠️ 本类**实现** <see cref="ISubtitleSink"/>,故它是 42 侧唯一字幕接收口的落地件。</remarks>
    public sealed class SubtitleLineView : VisualElement, ISubtitleSink
    {
        /// <summary>USS 类名(主题变量挂点;字号不写死 —— 走 `textScale`)。</summary>
        public const string UssClass = "subtitle-line";

        /// <summary>说话人名的 USS 类(字重差异的载体,非颜色)。</summary>
        public const string SpeakerUssClass = "subtitle-speaker";

        /// <summary>P0 行数上限(超出截断;见 story-026 AC-3)。</summary>
        public const int MaxLines = 2;

        private readonly Label _label;
        private string _currentSpeaker = string.Empty;
        private string _currentText = string.Empty;

        /// <summary>构造元件(空字幕态;默认隐藏 —— 由调用方 `Show` 才可见)。</summary>
        public SubtitleLineView()
        {
            AddToClassList(UssClass);
            _label = new Label();
            _label.AddToClassList(SpeakerUssClass);   // 样式挂在同一 Label 上,靠 USS 区分片段
            Add(_label);
            Hide();                                    // 初始不可见(上游未说「显示」)
        }

        /// <summary>当前说话人名(空 = 无说话人)。</summary>
        public string CurrentSpeaker => _currentSpeaker;

        /// <summary>当前字幕正文。</summary>
        public string CurrentText => _currentText;

        /// <summary>当前是否可见(渲染态,**不是**业务判断)。</summary>
        public bool IsShowing => _label.text != null && resolvedStyle.display != DisplayStyle.None;

        /// <inheritdoc/>
        public void Show(in SubtitleLineData line)
        {
            _currentSpeaker = line.SpeakerName;
            _currentText = line.Text;

            // 说话人前置 + **非颜色差异**(方括号 + 字重);空说话人 ⇒ 只出正文。
            _label.text = string.IsNullOrEmpty(line.SpeakerName)
                ? Truncate(line.Text)
                : "【" + line.SpeakerName + "】" + Truncate(line.Text);

            style.display = DisplayStyle.Flex;
        }

        /// <inheritdoc/>
        public void Hide()
        {
            _label.text = string.Empty;
            style.display = DisplayStyle.None;
        }

        /// <summary>P0 行数上限的落点:超过 <see cref="MaxLines"/> 行的文本截断。
        /// <para>⚠️ 截断按**换行符**计行(不是按显示宽度 —— 后者要布局信息,那是 49 的细化面)。</para></summary>
        private static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string[] lines = text.Split('\n');
            if (lines.Length <= MaxLines) return text;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < MaxLines; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(i == MaxLines - 1 ? lines[i] + "…" : lines[i]);
            }
            return sb.ToString();
        }
    }
}
