// 区域触发驱动(N1 · 2026-11-14)。
//
// 权威:`design/levels/prologue-beat-script.md` §1(N1 区域触发器)
//
// ═══ 它回答的两个问题(刻意分开)** ════════════════════════════════════════
//   `IsInside(id)`   —— **此刻**在不在里面。给「须按 E 才完成」的节拍用:
//                       玩家的语义是「站在这儿按一下」,不是「曾经路过一下」。
//   `HasEntered(id)` —— **进过没有**(闩锁,只置不清)。给「第一次踏进这片区域就
//                       做一件不可撤销的事」用(奥斯定:难民人群的出现、任何一次性演出)。
//   ⚠️ 两者**不可互相替代**:一次性闩锁不回答「此刻」,时刻判定不回答「进过」。
//      把一次性触发写成 `IsInside` ⇒ 玩家只要站着不动就每 tick 重触发;
//      把「按 E」写成 `HasEntered` ⇒ 玩家走开十秒再回来按 E 也算数。
//
// ═══ 边沿语义 ═════════════════════════════════════════════════════════════
//   `OnTickEdge` 逐格比对**上一 tick 的成员集**:进入(∉→∈)报一次 `onEnter`,
//   离开(∈→∉)报一次 `onExit`。**不是**电平判定 —— 待在里面不动不重复报。
//   ⚠️ 同一 tick 重复调用直接返回(与 `PrologueBeatDriver` 的 tick 去重同源):
//      一个 tick 内被调两次会让「离开再进入」被误读成一次进入。
//
// ═══ 边界(承 `PrologueBeatDriver` 与 ADR-019 §三)══════════════════════════
//   · **不写三流**:只读玩家格、只回调。
//   · **不进 sim**:纯 C#,零 `UnityEngine` 依赖(坐标是 `WorldPos` 整数格)。
//   · **不持游戏状态**:表是构造期给的只读集合,驱动只持「谁在里面 / 谁进过」两张位图。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 按 tick 比对玩家格与区域表的驱动器。<b>只读 + 回调</b>,不写三流。
    /// </summary>
    public sealed class RegionTriggerDriver
    {
        private readonly RegionTrigger[] _triggers;
        private readonly Func<WorldPos> _playerCell;
        private readonly Action<string> _onEnter;
        private readonly Action<string> _onExit;

        /// <summary>此刻在区域内的 id 集(每 tick 与上一 tick 比对后更新)。</summary>
        private readonly HashSet<string> _inside = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>进过区域的 id 集(只置不清)。</summary>
        private readonly HashSet<string> _entered = new HashSet<string>(StringComparer.Ordinal);

        private long _lastTick = -1;

        /// <summary>构造。</summary>
        /// <param name="triggers">区域表(**声明序** = 同 tick 内的触发顺序)。</param>
        /// <param name="playerCell">玩家当前格读取器(每 tick 现读;返回 `default` 即「无玩家」)。</param>
        /// <param name="onEnter">进入回调(可空;参数为触发器的 `Id`)。</param>
        /// <param name="onExit">离开回调(可空;参数为触发器的 `Id`)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="triggers"/> 或 <paramref name="playerCell"/> 为空。</exception>
        /// <exception cref="ArgumentException">表里出现**重复 id**。</exception>
        public RegionTriggerDriver(RegionTrigger[] triggers,
                                   Func<WorldPos> playerCell,
                                   Action<string> onEnter = null,
                                   Action<string> onExit = null)
        {
            if (triggers == null) throw new ArgumentNullException(nameof(triggers));
            if (playerCell == null) throw new ArgumentNullException(nameof(playerCell));

            // 重复 id = 后一条静默遮蔽前一条(回调只报 id,分不出是谁)⇒ 构造期就拒。
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < triggers.Length; i++)
            {
                if (!seen.Add(triggers[i].Id))
                    throw new ArgumentException(
                        "区域触发器 id 重复:\"" + triggers[i].Id + "\" —— 回调按 id 报信,重复会让两条互相遮蔽",
                        nameof(triggers));
            }

            _triggers = (RegionTrigger[])triggers.Clone();   // 防调用方事后改数组
            _playerCell = playerCell;
            _onEnter = onEnter;
            _onExit = onExit;
        }

        /// <summary>区域表(只读视图;声明序)。</summary>
        public IReadOnlyList<RegionTrigger> Triggers => _triggers;

        /// <summary>最近一次 `OnTickEdge` 的 tick(未跑过 = -1)。</summary>
        public long LastTick => _lastTick;

        /// <summary>此刻在区域内的数量(诊断用)。</summary>
        public int InsideCount => _inside.Count;

        /// <summary>进过区域的数量(诊断用)。</summary>
        public int EnteredCount => _entered.Count;

        /// <summary>
        /// 推进一个 tick:比对玩家格与区域表,进/出各报一次。
        /// </summary>
        /// <param name="tick">当前 tick(为负拒收;**同 tick 重复调用直接返回**)。
        /// tick 变小 = **读档**,不是错误 —— 见备注。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        /// <remarks>
        /// ⚠️ **tick 变小 = 读档,不抛。** 本驱动不持权威态(见类首边界三条),而存档把 sim tick
        /// 拨回去是**合法动作**(2026-11-14 实测:`adr041_bak_rescue_walkthrough` 读档后 tick 70 → 21)。
        /// 处置 = **重定基**:清成员集(「上一 tick 在谁里面」跨过一次读档已无意义)、
        /// **保** `_entered` 闩锁(一次性演出已经播过,读档不得重播)、就本次 tick 正常比对。
        /// <para>与 `PrologueBeatDriver` 同一口径 —— 它对 tick 变小也是静默接受、`_fired` 不清。
        /// 两者都刻意**不**照抄 sim 侧 `DiseaseVitalsService` 的「时间倒流即抛」:那条守的是
        /// 权威态,这里没有权威态可守。</para>
        /// </remarks>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (tick == _lastTick)
                return;                                  // 同 tick 去重(见类首「边沿语义」)

            if (tick < _lastTick)
            {
                // 读档:成员集作废,一次性闩锁保留(理由见 remarks)。
                _inside.Clear();
            }

            _lastTick = tick;

            WorldPos c = _playerCell();

            // 声明序遍历 —— 同 tick 内多个区域同时命中时,回调次序 = 表序(可复现)。
            for (int i = 0; i < _triggers.Length; i++)
            {
                RegionTrigger t = _triggers[i];
                bool now = t.Contains(c.X, c.Z);
                bool was = _inside.Contains(t.Id);

                if (now == was) continue;                // 电平没变 ⇒ 无边沿

                if (now)
                {
                    _inside.Add(t.Id);
                    bool already = _entered.Contains(t.Id);
                    if (!already) _entered.Add(t.Id);

                    // `Once` 只压**回调**,不压 `HasEntered` ——
                    //   「进过」是事实,与「要不要再报一次」无关。
                    if (!t.Once || !already)
                        _onEnter?.Invoke(t.Id);
                }
                else
                {
                    _inside.Remove(t.Id);
                    _onExit?.Invoke(t.Id);
                }
            }
        }

        /// <summary>**此刻**是否在该区域内(id 不存在 ⇒ false)。</summary>
        public bool IsInside(string id) => !string.IsNullOrEmpty(id) && _inside.Contains(id);

        /// <summary>是否**进过**该区域(只置不清;id 不存在 ⇒ false)。</summary>
        public bool HasEntered(string id) => !string.IsNullOrEmpty(id) && _entered.Contains(id);

        /// <summary>诊断串:`inside=N entered=M/total @tick=T`。</summary>
        public string Summary =>
            "inside=" + _inside.Count + " entered=" + _entered.Count + "/" + _triggers.Length +
            " @tick=" + _lastTick;
    }
}
