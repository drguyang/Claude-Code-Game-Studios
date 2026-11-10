// 同源检测与章节推进的**装配层驱动** —— T20(2026-11-10 用户裁定「先接同源检测,再挂章」)
//
// 权威:
//   GDD `case-system.md` 规则八(同源检测 · 三例 ⇒ 模式成立)· F-37.1 / F-37.1b
//   ADR-008 §三(载荷冻结三元组)· §五(盐 = WorldSeed 派生)
//   `PatternRecognitionWriter`(判据与写入的唯一落点)· `ChapterAdvanceWriter`(章的唯一写入点)
//   `HookCasePolicy.MaxPerChapter`(「每章 1 例」的读方;章不来,配额就永远锁在 1)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件补的两个「零生产调用点」
// ─────────────────────────────────────────────────────────────────────────────
//   ① `PatternRecognitionWriter.TryRecognize` —— 改前全仓**零生产构造点**(只有夹具)。
//      ⇒ 37 规则八(三例同源 ⇒ 世界级 `PatternRecognized`)在运行期**从未运行**,
//        而 53 的整条后果链与 39 的「图样」判定都以它为前提。
//   ② `ChapterAdvanceWriter.TryAdvance` —— 改前零生产者 ⇒ 章号恒 0 ⇒
//      `HookCasePolicy.MaxPerChapter = 1` 在运行期等于「整局 1 例」。
//
// ─────────────────────────────────────────────────────────────────────────────
// 两个口径(都是本件就地裁的,写在这里以免被读成 GDD 原文)
// ─────────────────────────────────────────────────────────────────────────────
//   · **何时检测**:流里出现**新的 `CaseClosed`** 时。同源检测的候选集就是已结案病例
//     (F-37.1),没有新结案 ⇒ 结果不可能变 ⇒ 零扫描(与 `TechniqueUnlockDriver` 的
//     「零规则零扫描」同款省法)。首次调用恒扫一遍(读档后自愈:历史结案会被重新评估,
//     写者内的 fires-once 门保证不重复写)。
//   · **章 = 已识别的同源模式条数**(1 = 第一次识别同源之后)。绝对量、从流可重建、
//     单调不减 ⇒ 与 `ChapterAdvanceWriter` 的约束(禁倒退)天然相容。
//     ⚠️ 「一章 = 一个被识破的图样」是**机制口径**,不是叙事口径;叙事宿主到位后
//        若改由剧情推进,只需换掉本件的 `TryAdvance` 调用点(写者与读侧不动)。
//
// ⚠️ 本件**不判**:某三例算不算同源(`PatternDetector` 的纯函数)、章该不该推进(读侧
//    `HookCasePolicy`)、玩家看不看得见(39 / 42)。它只做「有新结案 ⇒ 请写者判一次」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>每个 tick 边沿:有新结案 ⇒ 同源检测 ⇒ 按已识别条数推进章。</summary>
    public sealed class PatternRecognitionDriver
    {
        private readonly EventStream _stream;
        private readonly PatternRecognitionWriter _recognizer;
        private readonly ChapterAdvanceWriter _chapters;
        private readonly IPayloadDecoder _decoder;

        /// <summary>上次扫描时流里的 <c>CaseClosed</c> 条数。
        /// <para>⚠️ 初值 <c>-1</c> 只是「从未扫过」的**意图表达** —— 实测(证伪轮 M2)：
        /// 任何 ≠ 当前条数的初值都会让首轮触发扫描 ⇒ 该哨兵**不是承重件**。真正的语义保证是
        /// 写者的 fires-once(`PatternDetector.NextEmissions`)，本字段只负责省扫描。</para></summary>
        private int _lastCaseClosedCount = -1;

        /// <param name="stream">事件流(读面;生产 = 主机唯一流实例)。</param>
        /// <param name="recognizer">同源检测写者(唯一 Append 落点;fires-once 门在它内部)。</param>
        /// <param name="chapters">章节推进写者(唯一 Append 落点)。</param>
        /// <param name="decoder">载荷解码器(读 `PatternRecognized` 的病种位,供 `sourceId`)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public PatternRecognitionDriver(EventStream stream, PatternRecognitionWriter recognizer,
                                        ChapterAdvanceWriter chapters, IPayloadDecoder decoder)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _chapters = chapters ?? throw new ArgumentNullException(nameof(chapters));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>
        /// 一个 tick 边沿。
        /// </summary>
        /// <param name="tick">本 tick —— 写入一律用它(源事件可能在很早的 tick,照抄会让
        /// `EventStream.Append` 因 Tick 回退抛,与 `TechniqueUnlockDriver` 同款理由)。</param>
        /// <returns>本 tick **新推进的章数**(0 = 没有新同源模式)。</returns>
        public int OnTickEdge(long tick)
        {
            IReadOnlyList<SimEvent> events = _stream.Events;
            int count = events.Count;

            int closes = 0;
            for (int i = 0; i < count; i++)
                if (events[i].Kind == EventKind.CaseClosed) closes++;

            if (closes != _lastCaseClosedCount)
            {
                _lastCaseClosedCount = closes;
                // 写者内部逐案求值 + fires-once(fires-once 真源 = PatternDetector.NextEmissions)
                _recognizer.TryRecognize(tick);
            }

            // 章 = 已识别的同源模式条数(重数一次:上面可能刚写了几条)。
            events = _stream.Events;
            int patterns = 0;
            SimEvent? last = null;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != EventKind.PatternRecognized) continue;
                patterns++;
                last = events[i];
            }

            int advanced = 0;
            int current = _chapters.CurrentChapter();
            while (current < patterns)
            {
                current++;
                if (_chapters.TryAdvance(current, SourceIdOf(last), tick)) advanced++;
            }
            return advanced;
        }

        /// <summary>
        /// `sourceId` = 最近一条 `PatternRecognized` 的病种 id(位序 <c>DiseaseId − 1</c> 的最低位 + 1)。
        /// <para>它**不参与任何判定**(写者的形参注释已如此声明),只给流留一条「因哪个病种推进」的线索。
        /// 解不出来 ⇒ 0(不抛 —— 这里是痕迹字段,不是判据)。</para>
        /// </summary>
        private int SourceIdOf(SimEvent? e)
        {
            if (!e.HasValue) return 0;
            try
            {
                PatternRecognizedPayload p =
                    _decoder.Decode<PatternRecognizedPayload>(e.Value.Kind, e.Value.Payload);
                ulong bits = p.MemberDiseaseSet.Bits;
                for (int bit = 0; bit < 64; bit++)
                    if (((bits >> bit) & 1UL) != 0UL) return bit + 1;   // 位序契约:bit = DiseaseId − 1
            }
            catch (Exception)
            {
                return 0;
            }
            return 0;
        }
    }
}
