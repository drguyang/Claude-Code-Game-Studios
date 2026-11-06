// S25-4c · tutorial-004 —— 48 的**链上游**:事件流 → 投影 → 边沿 → 调度 → 翻译层。
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` F-48.1(两相:Completion 读时 / Fire 边沿)· 规则四(零持久化)
//   · `adr-009` §一 Q1(引导进度 = 派生态,不进流)· `adr-016` §三(禁读表现态位置做判定)
//   · `adr-019` §三(边界层只读消费者)
//
// ═══ 本类补的是哪一环(此前**整条链没有生产驱动**)═══════════════════════════
//   `EventStream.Events`
//     → `TutorialSnapshotBuilder`(边界层解码,KIND 名 + 作者轴)
//     → `TutorialProjection.Project`(六步完成度,单调读时)
//     → `TutorialTrigger.Evaluate`(首真边沿,闩住不回退)
//     → `TutorialDispatcher.Dispatch`(有序媒体意图)
//     → `TutorialMediaDriver.Play`(44 cue + 42 字幕)
// 前四段此前**各自零生产调用点**(只有夹具);本类即那个调用方。
//
// ═══ 三个刻意的取舍 ═════════════════════════════════════════════════════════
// ① **播种在构造期,不在首个 tick**:构造时按**当时已有的流**投影一次并 `Seed`(只置饱和、**不 Fire**)。
//    读档场景下,存档里已完成的步因此不会被重播(F-48.1 的「冷播种不重燃」);新局流为空,播下无物。
//    ⚠️ **不放在首个 tick 边沿**:那样「tick 0 内写入的事件」会被当成「读档既有进度」而**永不发火**
//    —— 是一个静默漏播面。
// ② **增量游标 + 指纹**:每 tick 只解**新事件**(`_cursor` 之后),不整体重扫。控制清单明禁
//    「每帧全量重扫三流」;`TutorialStreamFeed.Facts` 是零缓存适配器(每次访问全量重建),
//    故本类自持只增长的事实表。
//    ⚠️ **流被整体替换的检出** = 比对「上次处理过的最后一条事件」的四元组(见 `Fingerprint`)。
//    不等 ⇒ 视为**换了世界/读了档** ⇒ 整体重建 + 重新播种(否则旧世界的进度会串进新世界)。
//    同世界装配重建(`CompositionRoot.Assemble` 重跑)时本类是新实例,天然干净。
// ③ **不持游戏状态**:事实表是**只读输入的缓存**(三流本身仍唯一真源),闩在 `TutorialTrigger`,
//    本类不写流、不存档、不进 sim。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一个 tick 边沿的结果(可观测面;测试与走查都靠它,不靠日志)。</summary>
    public readonly struct TutorialChainTick
    {
        private static readonly ReadOnlyCollection<string> NoSteps =
            Array.AsReadOnly(Array.Empty<string>());

        /// <summary>本 tick 新发火的步 id(空表 = 无新完成)。</summary>
        public readonly IReadOnlyList<string> FiredStepIds;

        /// <summary>本 tick 真的发出去的口述条数(= `Emit` 次数)。</summary>
        public readonly int NarrationsPlayed;

        /// <summary>本 tick 是否检出「事件流被整体替换」(⇒ 已重建 + 重新播种,本 tick 不发火)。</summary>
        public readonly bool StreamWasReplaced;

        public TutorialChainTick(IReadOnlyList<string> firedStepIds, int narrationsPlayed, bool streamWasReplaced)
        {
            FiredStepIds = firedStepIds ?? NoSteps;
            NarrationsPlayed = narrationsPlayed;
            StreamWasReplaced = streamWasReplaced;
        }

        /// <summary>本 tick 是否有步发火。</summary>
        public bool AnyFired => FiredStepIds.Count > 0;
    }

    /// <summary>48 的**生产链驱动**(一实例 = 一名玩家 + 一个世界)。
    /// <para>每 tick 边沿跑一次 `Project → Evaluate → Dispatch → Play`。</para></summary>
    public sealed class TutorialChainDriver
    {
        private readonly EventStream _stream;
        private readonly IPayloadDecoder _decoder;
        private readonly TutorialTrigger _trigger;
        private readonly TutorialDispatcher _dispatcher;
        private readonly TutorialMediaDriver _media;
        private readonly Func<Int3> _sourceCell;
        private readonly IReadOnlyList<TutorialStepRow> _steps;
        private readonly List<TutorialEventFact> _facts = new List<TutorialEventFact>();
        private readonly CachedFeed _feed;

        private int _cursor;
        private bool _hasFingerprint;
        private long _lastTick;
        private long _lastSeq;
        private int _lastKind;
        private int _lastPatient;
        private int _seedCount;

        /// <summary>构造 + **立即播种**(按当前流投影一次,只置饱和不发火)。</summary>
        /// <param name="stream">三流合并视图(唯一真源;本类只读)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="content">48 内容表(`dataProvider.Load&lt;TutorialContentTable&gt;()`)。</param>
        /// <param name="playerId">本机玩家 id(机制 A 的 `player_id`;**不得为负**)。</param>
        /// <param name="media">翻译层(44 cue + 42 字幕)。</param>
        /// <param name="sourceCell">口述的空间化锚点(P0 无师父实体 ⇒ 由装配层给)。</param>
        /// <param name="checkpointWritten">7a 的「已存过档」回执;**null = 该缝未接**(`archive` 步将永不完结)。</param>
        public TutorialChainDriver(
            EventStream stream,
            IPayloadDecoder decoder,
            TutorialContentTable content,
            int playerId,
            TutorialMediaDriver media,
            Func<Int3> sourceCell,
            Func<bool> checkpointWritten = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _media = media ?? throw new ArgumentNullException(nameof(media));
            _sourceCell = sourceCell ?? throw new ArgumentNullException(nameof(sourceCell));

            _steps = content.Steps;
            _trigger = new TutorialTrigger(playerId, _steps);
            _dispatcher = new TutorialDispatcher(playerId, content);   // 空表在这里就抛
            _feed = new CachedFeed(_facts, checkpointWritten);

            // ⚠️ 播种口:**构造期**,见文件头 §取舍 ①
            Seed();
        }

        /// <summary>本驱动归属的玩家 id。</summary>
        public int PlayerId => _trigger.PlayerId;

        /// <summary>事实表当前条数(只增长;换流时重建)。</summary>
        public int FactCount => _facts.Count;

        /// <summary>构造期播种时读到的已完成步数(观测面,用于走查归因)。</summary>
        public int SeededCompletedSteps => _seedCount;

        /// <summary>
        /// tick 边沿:取新事件 → 投影 → 判边沿 → 调度 → 投递。
        /// </summary>
        /// <param name="tick">当前逻辑 tick(仅用于报文;本类不以此为时间源)。</param>
        public TutorialChainTick OnTickEdge(long tick)
        {
            bool replaced = DetectStreamReplacement();
            if (replaced) Seed();

            var newEvents = new List<SimEvent>();
            IReadOnlyList<SimEvent> all = _stream.Events;
            for (int i = _cursor; i < all.Count; i++) newEvents.Add(all[i]);

            if (newEvents.Count > 0)
            {
                SimEvent[] slice = newEvents.ToArray();
                IReadOnlyList<TutorialEventFact> fresh = TutorialSnapshotBuilder.Build(slice, _decoder);
                for (int i = 0; i < fresh.Count; i++) _facts.Add(fresh[i]);
                _cursor = all.Count;
                RememberLast(all);
            }

            if (replaced) return new TutorialChainTick(null, 0, streamWasReplaced: true);

            TutorialProgressView view = TutorialProjection.Project(_feed, _steps, PlayerId);
            IReadOnlyList<string> fired = _trigger.Evaluate(view);
            // ⚠️ 不返回 `default`:那会让 `FiredStepIds` 为 null(绕过构造器,F9 同族)
            if (fired.Count == 0) return new TutorialChainTick(null, 0, streamWasReplaced: false);

            IReadOnlyList<TutorialMediaIntent> intents = _dispatcher.Dispatch(fired);
            TutorialPlaybackResult played = _media.Play(intents, _sourceCell());
            return new TutorialChainTick(fired, played.NarrationsPlayed, streamWasReplaced: false);
        }

        /// <summary>按**当前**流重建事实表并播种(不 Fire)。</summary>
        private void Seed()
        {
            // ⚠️ 次序是判据的一部分:**先建事实表,再投影**。反过来的话,播种读到的是空表
            //    ⇒ 读档后把「已完成的步」全部当成未完成 ⇒ 一进游戏就把整段教学重播一遍。
            _facts.Clear();

            IReadOnlyList<SimEvent> all = _stream.Events;
            if (all.Count > 0)
            {
                IReadOnlyList<TutorialEventFact> facts = TutorialSnapshotBuilder.Build(all, _decoder);
                for (int i = 0; i < facts.Count; i++) _facts.Add(facts[i]);
            }
            _cursor = all.Count;
            RememberLast(all);

            TutorialProgressView view = TutorialProjection.Project(_feed, _steps, PlayerId);
            _trigger.Seed(view);

            _seedCount = 0;
            for (int i = 0; i < view.Steps.Count; i++) if (view.Steps[i].Completed) _seedCount++;
        }

        /// <summary>流是否被整体替换(变短,或末条事件与记忆中的不一是同一条)。</summary>
        private bool DetectStreamReplacement()
        {
            IReadOnlyList<SimEvent> all = _stream.Events;
            if (all.Count < _cursor) return true;
            if (!_hasFingerprint) return false;
            return !Matches(all, _cursor - 1);
        }

        private bool Matches(IReadOnlyList<SimEvent> all, int index)
        {
            if (index < 0 || index >= all.Count) return false;
            SimEvent e = all[index];
            return e.Tick == _lastTick && e.Seq == _lastSeq
                && (int)e.Kind == _lastKind && e.Patient.Value == _lastPatient;
        }

        private void RememberLast(IReadOnlyList<SimEvent> all)
        {
            if (all.Count == 0)
            {
                _hasFingerprint = false;
                _lastTick = _lastSeq = 0;
                _lastKind = _lastPatient = 0;
                return;
            }

            SimEvent e = all[all.Count - 1];
            _lastTick = e.Tick;
            _lastSeq = e.Seq;
            _lastKind = (int)e.Kind;
            _lastPatient = e.Patient.Value;
            _hasFingerprint = true;
        }

        /// <summary>把「只增长的事实表」包成 48 要的只读输入面。</summary>
        private sealed class CachedFeed : ITutorialProjectionFeed
        {
            private readonly List<TutorialEventFact> _facts;
            private readonly Func<bool> _checkpointWritten;

            public CachedFeed(List<TutorialEventFact> facts, Func<bool> checkpointWritten)
            {
                _facts = facts;
                _checkpointWritten = checkpointWritten;
            }

            public IReadOnlyList<TutorialEventFact> Facts => _facts;

            public bool HasCheckpointWritten => _checkpointWritten != null && _checkpointWritten();
        }
    }
}
