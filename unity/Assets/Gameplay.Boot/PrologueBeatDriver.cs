// 序章节拍驱动 —— D1 的骨架(2026-11-13)。
//
// 权威来源:
//   `design/levels/prologue-beat-script.md`(22 拍 · 每拍的触发与完成判据)
//   `design/gdd/content/campaign-arc.md` §序章(2026-11-13 重写版)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类补的是什么
// ─────────────────────────────────────────────────────────────────────────────
// 此前项目里**没有任何「玩家走到某处 ⇒ 播一段叙事」的机制**:
//   · 48 教学链(`TutorialTrigger`)只吃**事件流事实**(`predicateKind` = `EventKind` 成员名),
//     空间与时间都不在它的谓词闭集里;
//   · 52 随机事件是**掷骰**驱动 ⇒ 不可复现,不能承载脚本节拍(重放会得到另一条剧情)。
// ⇒ 序章里靠「走到某处」的 8 拍(钟楼刻字 / 告示 / 登记簿 / 那一夜 …)没有任何载体。
// 本类就是那个载体:**条件(空间 / 前置拍)⇒ 一次性动作(一行字幕)**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 边界(三条硬线)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不写三流**:只读玩家格与已完成集,输出字幕 —— 承 ADR-019 §三(只读消费者)。
// ② **不进 sim 程序集**:`Sim` / `Sim.Contracts` 引用集一字不改(门 A 不受影响)。
// ③ **只管「该不该显示」**:不冻结输入、不切相机、不推进时间 —— 那些归 N3(过场演出)。
//    本类若开始管这些,就会长成第二个 `SkeuoRuntimeDriver`,而两者的时序不久即分家。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;   // ISubtitleSink / SubtitleLineData
using DaYiJingCheng.Sim.Contracts;               // WorldPos

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一个叙事节拍:条件成立时**一次性**播一行字幕。</summary>
    public readonly struct PrologueBeat
    {
        /// <summary>节拍 id(与节拍脚本的编号一致,如 `P1-02`;非空、全局唯一)。</summary>
        public readonly string Id;

        /// <summary>触发条件(**每 tick 求值一次**;不得有副作用 —— 会被反复调用)。</summary>
        public readonly Func<bool> When;

        /// <summary>前置节拍 id(`null` / 空 = 无前置)。任一前置未触发 ⇒ 本拍**不求值**。
        /// <para>🔁 2026-11-13(乙):由单值改为集合。单值时 `P1-05`(神父走)只要求
        /// 「读过登记簿」,于是玩家还没看台阶与告示,神父就走了 —— 真包实测的顺序是
        /// 开场 → 登记簿 → 神父走 → 台阶刻字 → 告示。多前置让「三样都看过才发生」可表达。</para></summary>
        public readonly string[] Requires;

        /// <summary>是否要求**玩家按下交互键**(E)才触发。
        /// <para>🔁 2026-11-13(乙):脚本 §第一段 的三拍完成判据都是「读毕」,而实现只做
        /// 区域进入 ⇒ 玩家只是路过就把「登记簿」读了。按 E 之后才算读过。</para></summary>
        public readonly bool RequiresInteract;

        /// <summary>要播的字幕;空串 = 本拍只有状态(不显示)。</summary>
        public readonly string Narration;

        /// <summary>
        /// 触发时要执行的**副作用**(N2 剧情脚本事件 · 2026-11-14);`null` = 纯台词拍。
        /// <para>字幕之外的「动作」(人群出现 / 入包 / 推进时间)走这里 —— 它们在结构上
        /// 不可能表达成 `EventKind`(不是玩家事件),也不可能表达成字幕。</para>
        /// <para>⚠️ **执行时机 = 标记已触发之前,且先于字幕**:动作抛异常 ⇒ 本拍**不算触发**,
        /// 下一 tick 会重试(大声失败,不是静默吞掉)。若反过来先标记再执行,一次异常就让
        /// 该拍永久丢失,而画面上已经播出了「事情发生了」的字幕 —— 那是最坏的一种不一致。</para>
        /// <para>⚠️ **只能执行一次**:驱动在 `_fired` 闩锁下调用,重复触发不会发生。</para>
        /// </summary>
        public readonly Action Effect;

        /// <summary>构造(单前置;保留给「只依赖一拍」的常见形态)。</summary>
        public PrologueBeat(string id, Func<bool> when, string narration, string requires = null,
                            bool requiresInteract = false, Action effect = null)
            : this(id, when, narration,
                   string.IsNullOrEmpty(requires) ? null : new[] { requires }, requiresInteract, effect)
        {
        }

        /// <summary>构造(多前置)。</summary>
        /// <param name="id">节拍 id。</param>
        /// <param name="when">触发条件(无副作用)。</param>
        /// <param name="narration">字幕。</param>
        /// <param name="requires">前置节拍 id 集(全部完成才求值;`null` = 无前置)。</param>
        /// <param name="requiresInteract">是否要求本拍窗口内有一次交互按键。</param>
        /// <param name="effect">触发时的副作用(`null` = 纯台词拍;见 <see cref="Effect"/>)。</param>
        public PrologueBeat(string id, Func<bool> when, string narration, string[] requires,
                            bool requiresInteract = false, Action effect = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("节拍 id 不得为空", nameof(id));
            Id = id;
            When = when ?? throw new ArgumentNullException(nameof(when));
            Narration = narration ?? string.Empty;
            Requires = requires;
            RequiresInteract = requiresInteract;
            Effect = effect;
        }
    }

    /// <summary>
    /// 序章节拍驱动:每 tick 推进一格,条件成立即播一行字幕;一条在播时不叠加。
    /// </summary>
    /// <remarks>
    /// <para>⚠️ **为什么串行**(一条在播时不触发下一拍):`ISubtitleSink.Show` 是**替换**语义,
    /// 同时触发两拍会让前一句来不及被读到就消失。串行也让「字幕顺序」与节拍表顺序**恒等**,
    /// 走查时读日志就能还原玩家看到过的顺序。</para>
    /// <para>⚠️ **为什么每个 tick 只放一拍**:同一 tick 放两拍会退化成「后一拍覆盖前一拍」,
    /// 与串行约束自相矛盾。</para>
    /// </remarks>
    public sealed class PrologueBeatDriver
    {
        /// <summary>一行字幕的默认停留时长(tick;20 Hz 下 = 5 s)。</summary>
        public const int DefaultNarrationTicks = 100;

        /// <summary>旁白的说话人栏(P0 无配音,留空)。</summary>
        public const string NarratorSpeaker = "";

        private readonly IReadOnlyList<PrologueBeat> _beats;
        private readonly Func<WorldPos> _playerCell;
        private readonly ISubtitleSink _subtitles;
        private readonly int _narrationTicks;
        private readonly HashSet<string> _fired = new HashSet<string>(StringComparer.Ordinal);
        private long _lastTick = -1;
        private string _showingId;
        private long _hideAtTick = -1;
        private bool _interactPending;

        /// <summary>前置是否全部完成(`Requires` 为空 ⇒ true)。</summary>
        private bool RequiresMet(in PrologueBeat beat)
        {
            if (beat.Requires == null) return true;
            for (int i = 0; i < beat.Requires.Length; i++)
                if (!_fired.Contains(beat.Requires[i])) return false;
            return true;
        }

        /// <summary>构造。</summary>
        /// <param name="beats">节拍表(声明序 = 字幕的优先序)。</param>
        /// <param name="playerCell">玩家当前逻辑格(每 tick 读;不得缓存 —— 玩家会走)。</param>
        /// <param name="subtitles">字幕接收口(42 侧 `SubtitleLineView`)。</param>
        /// <param name="narrationTicks">单行停留 tick 数(默认 100 = 5 s)。</param>
        public PrologueBeatDriver(IReadOnlyList<PrologueBeat> beats,
                                  Func<WorldPos> playerCell,
                                  ISubtitleSink subtitles,
                                  int narrationTicks = DefaultNarrationTicks)
        {
            _beats = beats ?? throw new ArgumentNullException(nameof(beats));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            _subtitles = subtitles ?? throw new ArgumentNullException(nameof(subtitles));
            if (narrationTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(narrationTicks), narrationTicks,
                    "停留时长须 > 0 tick —— 0 会让字幕在同一个 tick 里显示又清空(玩家永远看不见)");
            _narrationTicks = narrationTicks;
        }

        /// <summary>已触发的拍数。</summary>
        public int FiredCount => _fired.Count;

        /// <summary>节拍表的总拍数。</summary>
        public int TotalCount => _beats.Count;

        /// <summary>诊断读数:`已触发/总拍数 show=在播的拍`(不带 `-tutorialdiag` 时无人调用)。</summary>
        public string Summary =>
            FiredCount.ToString() + "/" + TotalCount + " show=" + (_showingId ?? "-");

        /// <summary>当前正在显示字幕的拍 id(`null` = 无字幕)。</summary>
        public string ShowingId => _showingId;

        /// <summary>玩家格(供节拍表的条件闭包使用;与驱动内部同一份来源)。</summary>
        public WorldPos PlayerCell => _playerCell();

        /// <summary>某拍是否已触发。</summary>
        public bool HasFired(string id) => _fired.Contains(id);

        /// <summary>玩家按下了交互键(由输入层在**按下沿**调用)。
        /// <para>语义 = **只活一个 tick**:本 tick 求值时用掉就清,过期作废。
        /// 这样「在远处按了 E,再走过来」不会补触发 —— 那是把按键缓存成意图,
        /// 与 `F-4.2`(模态期意图不排队)同一条纪律。</para></summary>
        public void NotifyInteract() => _interactPending = true;

        /// <summary>tick 边沿推进。</summary>
        /// <exception cref="ArgumentOutOfRangeException">`tick` 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (tick == _lastTick) return;
            _lastTick = tick;

            // 交互按键只在本 tick 有效(见 `NotifyInteract`)。
            bool interact = _interactPending;
            _interactPending = false;

            // ① 到点清字幕 —— 清完**本 tick 不再播新拍**:同一 tick 里 Hide + Show
            //    会让字幕先空一帧再出现(闪烁),而且「一拍一 tick」的节奏会被清屏那拍吃掉。
            if (_showingId != null && tick >= _hideAtTick)
            {
                _subtitles.Hide();
                _showingId = null;
                _hideAtTick = -1;
                return;
            }

            // ② 一条在播 ⇒ 本 tick 不触发新拍(替换语义会让前一句看不见)
            if (_showingId != null) return;

            // ③ 按声明序找第一个满足条件的未触发拍;本 tick 只放一拍
            for (int i = 0; i < _beats.Count; i++)
            {
                PrologueBeat beat = _beats[i];
                if (_fired.Contains(beat.Id)) continue;
                if (!RequiresMet(beat)) continue;
                if (beat.RequiresInteract && !interact) continue;
                if (!beat.When()) continue;

                // N2(2026-11-14):先做动作,再做字幕;动作抛 ⇒ 本拍不标记触发(见 Effect 注释)。
                beat.Effect?.Invoke();

                _fired.Add(beat.Id);
                if (!string.IsNullOrEmpty(beat.Narration))
                {
                    _subtitles.Show(new SubtitleLineData(NarratorSpeaker, beat.Narration));
                    _showingId = beat.Id;
                    _hideAtTick = tick + _narrationTicks;
                }
                return;
            }
        }
    }
}
