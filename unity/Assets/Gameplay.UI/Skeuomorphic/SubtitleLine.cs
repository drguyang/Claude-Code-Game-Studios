// 语声字幕的**呈现接收口**(`AC-44-15` 的 `[L]` 半边 · 42 侧落点 · 2026-10-29)。
//
// ── 权威来源 ────────────────────────────────────────────────────────────────
//   · `design/gdd/skeuomorphic-ui.md` §Edge Cases 四 **E-12b**(`:997-1000`)
//   · `design/accessibility-requirements.md:94-95`(字幕默认开 · 说话人识别 · 名字前置)
//   · `design/gdd/audio-system.md` **AC-44-15**(`[L]` 半:病人语声与口述有字幕,呈现归 42)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)
//
// ── 本件的边界(逐字承 E-12b)────────────────────────────────────────────────
// **「42 不持有语声状态、不判何时显示」** —— 语义 / 时机归 44,42 只渲染文本。
// 故本件**没有**「多久后自动隐藏」/「要不要显示」的逻辑:
//   · `Show` / `Hide` 由**上游发射点**调(它知道何时说、说多久);
//   · 42 只负责「把这一行画出来」。
//
// ── 为什么字幕**不**经 44 转发(结构性理由)──────────────────────────────────
// `AC-44-15` 原文逐字:「cue → 44 播音(**维持「只入不出」,44 不新增出向口**),
// 文本 → 42 呈现(**文本本体 = 44 维护的事件表 `subtitle_text` 字段,上游发射点解析**)」。
// ⇒ 44 的 `IAudioCueSink` 只有入向方法(ADR-018 §② 逐字)。文本**从上游直投 42**,
// 不经 44 —— 这是已裁的架构形状,不是本件的取舍。
//
// ── 与 49 的关系 ────────────────────────────────────────────────────────────
// `OQ-42-8` 原裁「具体要求归 49(P2)」。用户 2026-10-29 裁定**提前实现**。
// ⇒ 本件给出 **P0 最小呈现**(见 `SubtitleLineView`),**不解除** 49 的细化权。

using System;

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    /// <summary>一行字幕的**数据形状**(纯文本 + 说话人;**无病种标识** —— AC-6)。</summary>
    /// <remarks>⚠️ 本 DTO 受 `PresentationDtoGuard` 递归扫描覆盖:字段里**不得**出现
    /// `disease_id` / 病种枚举 / 任何 sim 侧标识(`AC-37-15` 同族纪律)。
    /// 说话人是**人名词串**(如「病人」「老者」),不是实体 id。</remarks>
    public readonly struct SubtitleLineData
    {
        /// <summary>说话人显示名(`accessibility-requirements.md:95`「名字前置」)。空串 = 无说话人。</summary>
        public readonly string SpeakerName;

        /// <summary>字幕正文(来自事件表 `subtitle_text`,上游发射点解析后传入)。</summary>
        public readonly string Text;

        public SubtitleLineData(string speakerName, string text)
        {
            SpeakerName = speakerName ?? string.Empty;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>42 侧的**唯一字幕接收口**(上游发射点调它)。
    /// <para>⚠️ **没有 `ShouldShow` / `IsVisible` 之类的反问** —— 42 不判时机(`E-12b`)。
    /// 上游说显示就显示,说隐藏就隐藏。</para></summary>
    public interface ISubtitleSink
    {
        /// <summary>显示一行字幕(替换当前行;P0 至多 2 行,见 `SubtitleLineView`)。</summary>
        void Show(in SubtitleLineData line);

        /// <summary>隐藏字幕(清空当前行)。</summary>
        void Hide();
    }
}
