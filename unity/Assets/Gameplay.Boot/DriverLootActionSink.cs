// 拾取面板的意图出口实现(2026-11-14 · ADR-013 §十-H Amendment G / `ModalId.Loot`)。
//
// 链路:
//   E 按下沿(选中 Drop)──▶ `InteractionPressRouter` ──▶ 打开 `ModalId.Loot`
//     ──▶ `LootScreen` 逐行「拾取」/ 末行「全部拾取」──▶ **本类**
//     ──▶ `DropClaimOrchestrator.TryClaimInstance` ──▶ `DropClaimed` 进世界流
//
// ⚠️ **本类不判定任何游戏状态**:
//   「能不能拿」全在 20 的编排面(`CanClaim` 的三段闸 + 归属面 + 判距);
//   本类只做「按屏上的选择逐件转交」与「拿完之后把列表刷新一遍」。
//   ⇒ 42 侧仍然零判定(ADR-013 §9 C3),本类住装配层也是 ADR-025 §① 的方向要求。
//
// ⚠️ **批量不是第二个判据**:`ClaimAll` 逐个调**同一个** `TryClaimInstance` ——
//   不另写「全部可拿」的合取。被拒的那件(已归属 / 无锚 / 出半径)**原地不动**,
//   刷新后仍留在列表里,玩家看得见「为什么没拿走」。
//
// ⚠️ **空列表即合屏**:拿完之后若地上再无可拾,本类关掉模态 —— 一个零行的拾取屏
//   是死界面(焦点无处可落)。
//
// ⚠️ 屏 / 合屏两件事走**委托**(`screenOf` / `close`),不直接持 `SkeuoRuntimeDriver`:
//   这样 EditMode 夹具能喂一个真 `LootScreen` + 一个计数闭包,把「逐件 / 批量 / 合屏」
//   三条判据测在**生产实现体**上,而不是绕开它另写一份。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
    using DaYiJingCheng.Sim;
    using DaYiJingCheng.Sim.Contracts;

    /// <summary>拾取面板的装配层实现(42 → 装配层的单向调用)。</summary>
    public sealed class DriverLootActionSink : ILootActionSink
    {
        private readonly DropClaimOrchestrator _drops;
        private readonly int _actorId;
        private readonly Func<WorldPos> _cellOf;
        private readonly Func<long> _tickOf;
        private readonly Func<IReadOnlyList<LootRow>> _rowsOf;
        private readonly Func<LootScreen> _screenOf;
        private readonly Action _close;

        /// <summary>构造。</summary>
        /// <param name="drops">拾取编排面(非 null —— 唯一结算面)。</param>
        /// <param name="actorId">玩家 id(20 的归属面)。</param>
        /// <param name="cellOf">玩家**经流确立格**来源(禁表现态连续位置)。</param>
        /// <param name="tickOf">当前逻辑 tick 来源(`DropClaimed` 的事件头)。</param>
        /// <param name="rowsOf">「此刻地上可拾行」的来源(拿完后重取,列表随之收缩)。</param>
        /// <param name="screenOf">当前拾取屏来源(<c>null</c> = 屏已不在 ⇒ 只结算不刷新)。</param>
        /// <param name="close">合屏动作(<c>null</c> = 未接线 ⇒ 不主动合屏)。</param>
        /// <exception cref="ArgumentNullException">drops / cellOf / tickOf / rowsOf 为 null。</exception>
        public DriverLootActionSink(DropClaimOrchestrator drops, int actorId,
                                    Func<WorldPos> cellOf, Func<long> tickOf,
                                    Func<IReadOnlyList<LootRow>> rowsOf,
                                    Func<LootScreen> screenOf, Action close)
        {
            _drops = drops ?? throw new ArgumentNullException(nameof(drops));
            if (actorId < 0) throw new ArgumentOutOfRangeException(nameof(actorId), actorId, "玩家 id 不得为负");
            _actorId = actorId;
            _cellOf = cellOf ?? throw new ArgumentNullException(nameof(cellOf));
            _tickOf = tickOf ?? throw new ArgumentNullException(nameof(tickOf));
            _rowsOf = rowsOf ?? throw new ArgumentNullException(nameof(rowsOf));
            _screenOf = screenOf;
            _close = close;
        }

        /// <inheritdoc/>
        public void Claim(long instanceId)
        {
            _drops.TryClaimInstance(_actorId, _tickOf(), _cellOf(), instanceId);
            Refresh();
        }

        /// <inheritdoc/>
        public void ClaimAll()
        {
            // 先取**快照**:逐件结算会让投影变化,边遍历边重取 = 漏拿。
            IReadOnlyList<LootRow> listed = _rowsOf();
            for (int i = 0; i < listed.Count; i++)
                _drops.TryClaimInstance(_actorId, _tickOf(), _cellOf(), listed[i].InstanceId);
            Refresh();
        }

        private void Refresh()
        {
            LootScreen screen = _screenOf?.Invoke();
            if (screen == null) return;

            IReadOnlyList<LootRow> rows = _rowsOf();
            if (rows.Count == 0)
            {
                _close?.Invoke();
                return;
            }
            screen.Present(rows);
        }
    }
}
