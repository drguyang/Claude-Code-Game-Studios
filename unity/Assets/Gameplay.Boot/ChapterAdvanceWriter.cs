// 章节推进的**写者侧端口** —— B1-b(2026-11-10 用户裁定「新增 ChapterAdvanced 事件」)
//
// 权威:
//   design/gdd/reviews/technique-unlock-decision-sheet-2026-11-10.md §2 B1(B1-b)
//   design/registry/entities.yaml 的 `SimEvent.Kind.ChapterAdvanced` 条目(payload / constraint)
//   ADR-005 §一 唯一真源 —— 章是**状态**,状态从事件流重构,不做存档头字段
//   ADR-029 —— 唯一编码路径 = IPayloadEncoder
//
// ── 为什么这条路要存在 ────────────────────────────────────────────────────
// `HookCasePolicy.MaxPerChapter = 1` 的「每章」在 2026-11-09 之前**运行期不存在**
// (全仓零章节状态 ⇒ 钩子配额退化成「整局 1 例」,T5a 已如实登记)。
// 本类是那个「章」的唯一写入点;读侧由 `HookCaseLedger.CurrentChapter()` 从流重建。
//
// ⚠️ **本类落地 ≠ 每章 1 例已生效**:P0 尚无叙事系统发这条事件 ⇒ 章号恒 0,
//    钩子配额仍是整局 1 例。解除条件 = 叙事轮接上生产者(奇遇宿主)。
//    这条不得在证据里写成「每章 1 例已实现」。
//    🔁 **2026-11-10(T20)已接**:生产调用点 = `PatternRecognitionDriver.OnTickEdge`
//    (每条新 `PatternRecognized` 推进一章,章号 = 已识别同源模式条数)。上句的
//    「零生产者 / 章号恒 0」**不再成立**;「叙事轮接上生产者」这条**仍未做**(机制先行)。
//
// ⚠️ 本类是**唯一**写 `ChapterAdvanced` 的地方。任何第二处 `Append(ChapterAdvanced)`
//    都算违规 —— 那会让「谁推进的章」失去单点(同 `TechniqueUnlockWriter` 的口径)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>把「进入下一章」变成一条病史流事件。</summary>
    public sealed class ChapterAdvanceWriter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;

        /// <param name="sink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="events">事件流只读视图(判当前章 + 去重)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public ChapterAdvanceWriter(IEventSink sink, IPayloadEncoder encoder,
                                    Func<IReadOnlyList<SimEvent>> events, IPayloadDecoder decoder)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>
        /// 当前章号(<c>0</c> = 从未推进 —— 序章 / 第一章之前)。
        /// <para>判据 = 流里**最后一条** <see cref="EventKind.ChapterAdvanced"/> 的
        /// <c>Chapter</c>;空流 ⇒ 0。载荷存的是**绝对量**,故不需要累加,也不需要
        /// 「一条都不能丢」这个前提。</para>
        /// </summary>
        public int CurrentChapter()
        {
            IReadOnlyList<SimEvent> events = _events();
            if (events == null) return 0;

            int chapter = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.ChapterAdvanced) continue;
                ChapterAdvancedPayload p = _decoder.Decode<ChapterAdvancedPayload>(e.Kind, e.Payload);
                chapter = p.Chapter;
            }
            return chapter;
        }

        /// <summary>
        /// 推进到 <paramref name="chapter"/> 章。
        /// </summary>
        /// <param name="chapter">推进**之后**的章号(绝对量,≥1)。</param>
        /// <param name="sourceId">推进来源(奇遇 / 剧情节点 id;不参与判定)。</param>
        /// <param name="tick">当前 tick(<see cref="ITickProvider.CurrentTick"/>)。</param>
        /// <returns><c>true</c> = 真的写了一条;<c>false</c> = 已在该章(no-op)。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="chapter"/> &lt; 1,
        /// 或 <paramref name="tick"/> 为负。</exception>
        /// <exception cref="InvalidOperationException"><paramref name="chapter"/> **倒退**
        /// (小于当前章)—— 倒退会让钩子配额凭空多出一格,而配额正是防刷判据。</exception>
        public bool TryAdvance(int chapter, int sourceId, long tick)
        {
            if (chapter < 1)
                throw new ArgumentOutOfRangeException(nameof(chapter), chapter,
                    "章号是绝对量且 ≥ 1 —— 0 是「从未推进」的哨兵,不能作为推进目标(写进去会与空流不可区分)");
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            int current = CurrentChapter();
            if (chapter < current)
                throw new InvalidOperationException(
                    $"章号倒退:{current} → {chapter} —— 章号单调不减(registry constraint);" +
                    "倒退会让「本章已注入几例」的计数重新开始,等于凭空多出一格配额");
            if (chapter == current) return false;

            // ⚠️ 允许**跳章**(chapter > current + 1):玩家可能跳过一整章的内容。
            //    不强制 +1 —— 强制会让「跳章」变成一次静默失败(事件写不进去而调用方以为成功)。
            var payload = new ChapterAdvancedPayload(chapter, sourceId);

            // Patient = None(-1):章推进是玩家 / 世界级事实,与 ADR-007 五支同格。
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.ChapterAdvanced,
                                      _encoder.Encode(EventKind.ChapterAdvanced, payload)));
            return true;
        }
    }
}
