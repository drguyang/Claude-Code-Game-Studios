// S25-2 · tutorial-003 —— 48 的两相触发引擎(**完成**是读时谓词,**引发**是边沿)。
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` 规则四 / **F-48.1**
//       完成(单调,读时计算):`Completion_s(p) ⟺ Predicate_s(Proj(p))`
//       引发(边沿,进程内闩锁):`FireAdvance_s(p)@t ⟺ Completion_s(p) 在 t 首次为真`
//       (冷启动:闩锁从载入流播种 —— 已成立判据**永不发火**)
//     · F-48.2(六谓词**彼此独立**:可跳步 / 可回头 / **不补播**)
//     · 规则四「为什么必须派生」③:老玩家读档回到第一幕**不会重播**教过的内容
//   · `docs/architecture/adr-009` §一 Q1 —— 引导进度是**派生态**:闩是**进程内易失**的,
//     不进流、不存档、不进 `SimEvent`(承 `AC-48-03` 的零落盘)
//   · Story: `production/epics/tutorial-onboarding/story-003-anchor-completion-trigger-engine.md`
//
// ═══ 两条口,刻意分家(Implementation Notes 2)═══════════════════════════════════
//   `Seed(view)`     —— **播种口**:冷启动(读档完成 → 首帧前)用当前投影值把闩**直接置饱和**,
//                       视同「已发生过」,**不跑 Fire 路径**。共用会重燃 ⇒ 两口禁合并。
//   `Evaluate(view)` —— **增量口**:首真边沿才产出**媒体调度意图**(step id 列表)。
//   判据(可跑):往播种口投一个「已完成」的视图 ⇒ `FireCount` 恒 0;
//   往增量口投同一视图 ⇒ 该步恰 Fire 一次(测试 `tutorial_trigger_engine_test`)。
//
// ═══ 什么**不是**本类的职责 ═══════════════════════════════════════════════
//   · 不做投影(`Proj` 归 `TutorialProjection`,S25-1);
//   · 不做调度(口述 / 示范 / 纸的次序与时序归 Story 004);
//   · **不设门**(规则五 / 零设门):本类**不产出任何**「解锁 / 禁用 / 拦住」的查询或副作用,
//     `Evaluate` 的唯一输出是 step id 列表。设门守卫本体 = Story 005 的反射断言。
//
// ⚠️ **闩不可序列化**:本类**无** `[SerializeField]`、无公开可写字段、不实现 `ISerializable`;
//    它的状态**只活在这个进程里** —— 切场景 / 读档 / 重启都从流重新播种(F-48.1 冷启动)。
// ⚠️ **不轮询流**:本类**不收**事件流、不收帧、不收 `Time.*`;调用方按 tick 边沿把**已算好的投影**
//    喂进来(Control Manifest 本条:禁每帧全量重扫三流)。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;

namespace DaYiJingCheng.Gameplay.UI.Tutorial
{
    /// <summary>
    /// 48 的**两相触发引擎**(一实例 = 一名玩家)。零持久化、零门、只产出媒体调度意图。
    /// <para>⚠️ 一相只有一相:`F-48.1` 里**会发火的只有 `Anchor`** —— `Completion` 是单调读时谓词
    /// (不出事件)。故 AC 里「(step, phase) 至多 Fire 一次」在此退化为「每步至多 Fire 一次」;
    /// 若将来 GDD 增出第二个发火相,再加相位维,不预先虚构。</para>
    /// </summary>
    /// <summary>两相触发引擎 —— **internal**(AC-48-17):`HasFired` / `FireCount` 等价于
    /// 「这一步完成了吗」的查询;玩法系统不该拿它设门。消费方 = `Gameplay.Boot` 的链驱动。</summary>
    internal sealed class TutorialTrigger
    {
        private static readonly ReadOnlyCollection<string> NoFired =
            Array.AsReadOnly(Array.Empty<string>());

        private readonly int _playerId;
        private readonly ReadOnlyCollection<TutorialStepRow> _steps;
        private readonly bool[] _latched;      // 进程内闩 —— 唯一的状态,且**永不复位**
        private int _fireCount;

        /// <summary>构造一名玩家的触发引擎。</summary>
        /// <param name="playerId">玩家 id(与投影的 `PlayerId` 必须一致 —— 串玩家 = fail-loud)。</param>
        /// <param name="steps">内容表的步行(步 id 闭集;顺序 = 内容表序,引擎**不排序**)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="steps"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="playerId"/> &lt; 0。</exception>
        public TutorialTrigger(int playerId, IReadOnlyList<TutorialStepRow> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps),
                "未给出步定义 —— 空闭集 ⇒ 永不发火(静默失效)");
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId,
                    "player_id 为负 —— 它是「无作者轴」的哨兵(TutorialEventFact.NoAuthor)");

            _playerId = playerId;
            var copy = new TutorialStepRow[steps.Count];
            for (int i = 0; i < steps.Count; i++) copy[i] = steps[i];
            _steps = Array.AsReadOnly(copy);
            _latched = new bool[copy.Length];
        }

        /// <summary>本引擎归属的玩家 id。</summary>
        public int PlayerId => _playerId;

        /// <summary>步数(内容表闭集大小)。</summary>
        public int StepCount => _steps.Count;

        /// <summary>本进程内累计发火次数(单调;观测面)。</summary>
        public int FireCount => _fireCount;

        /// <summary>
        /// **播种口**(冷启动):把当前投影里**已成立**的步直接置饱和,视同已发生 —— **不跑 Fire 路径**。
        /// </summary>
        /// <param name="view">刚由**载入流**算出的投影。</param>
        /// <exception cref="ArgumentOutOfRangeException">视图的玩家与引擎不符(串了玩家 ⇒ 会把别人的进度播给我)。</exception>
        /// <remarks>
        /// ⚠️ 与 <see cref="Evaluate"/> **禁共用**:共用 = 「已学过」被当成「刚学会」⇒ 老玩家读档重播。
        /// ⚠️ 允许重复调用(幂等:只置 true,从不发火)。
        /// </remarks>
        public void Seed(TutorialProgressView view)
        {
            RequireSamePlayer(view);

            for (int i = 0; i < _steps.Count; i++)
                if (view.IsCompleted(_steps[i].Id)) _latched[i] = true;
        }

        /// <summary>
        /// **增量口**:完成判据出现**首真边沿**的步 ⇒ 产出媒体调度意图(step id),并把闩置饱和。
        /// </summary>
        /// <param name="view">本次 tick 边沿上算出的投影(顺序无关 —— 判定只问布尔)。</param>
        /// <returns>**本次新发火**的步 id(无 ⇒ 空列表)。调用方把它交给媒体调度(Story 004)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">视图的玩家与引擎不符。</exception>
        /// <remarks>
        /// **边沿定义**:同一步在**一次进程生命周期内**至多发火一次(`_latched` 只进不退)。
        /// 事件回灌 / 重放 / tick 乱序都不会二次发火 —— 判定只看「已闩与否 × 当前是否成立」。
        /// **不补播**(F-48.2):跳过某步后又回头补做,只发该步自己的火,不连带追播前步。
        /// </remarks>
        public IReadOnlyList<string> Evaluate(TutorialProgressView view)
        {
            RequireSamePlayer(view);

            List<string> fired = null;
            for (int i = 0; i < _steps.Count; i++)
            {
                if (_latched[i]) continue;                       // 已发过 ⇒ 永不重燃
                string stepId = _steps[i].Id;
                if (!view.IsCompleted(stepId)) continue;         // 未成立 ⇒ 不发火

                _latched[i] = true;                              // 先闩再记 —— 单线程 tick 语义
                _fireCount++;
                (fired ??= new List<string>(1)).Add(stepId);
            }

            return fired == null ? NoFired : fired.AsReadOnly();
        }

        /// <summary>某一步在本进程内是否已发过火(观测面;未知名 ⇒ false)。</summary>
        public bool HasFired(string stepId)
        {
            if (string.IsNullOrEmpty(stepId)) return false;
            for (int i = 0; i < _steps.Count; i++)
                if (string.Equals(_steps[i].Id, stepId, StringComparison.Ordinal))
                    return _latched[i];
            return false;
        }

        private void RequireSamePlayer(TutorialProgressView view)
        {
            if (view.PlayerId == _playerId) return;
            throw new ArgumentOutOfRangeException(nameof(view), view.PlayerId,
                $"投影属于玩家 {view.PlayerId},而本引擎属于玩家 {_playerId} —— " +
                "串玩家会把别人的进度当成自己的播出去(AC-48-09 的反面)");
        }
    }
}
