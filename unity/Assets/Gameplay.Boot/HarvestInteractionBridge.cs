// S22-9(2026-11-01)· BootRoot 侧的**采集意图桥**
//
// ── 它解的是什么 ────────────────────────────────────────────────────────────
// BootRoot 是 MonoBehaviour:装配 lambda 写在它体内 ⇒ EditMode 不可直测。本桥把
// 「E 按下沿先问一次采集」这段装配逻辑独立成类,使**接线本身有夹具**:
//   · driver 未装配(`HarvestDriver == null`,fake provider / 数据缺失)⇒ **放行**查体
//     (诚实降级 —— 与 `ConsequenceDriver` 的 null 语义同格,不得静默吞交互);
//   · 选择器选中采集点 ⇒ 返回 true(按键归采集链,**闸门拒绝也算消费** ——
//     否则拒采的同时又查体 = 一次按键两个动作);
//   · 未选中(无候选 / 目标不是采集点)⇒ 返回 false,查体照常。
//
// ── 意图构造的三个取数口径(承 4 的文档)──────────────────────────────────────
//   · `PlayerCell` = **经流确立的格**(`PlayerController.LastCommittedCell`,ADR-020 §四
//     读方半边)—— 不是 `transform.position` 派生的表现态格,也不是 pending;
//   · `Pressed` = 恒 true:本方法只在 E **按下沿**被调用(边沿判定住
//     `InteractionInputDriver`,本桥不持边沿位 —— 单一边沿所有者);
//   · `Tick` = `ITickProvider.CurrentTick`(与整 tick 对齐)。
//
// 模态门**不在此**:E 分支整体被 `InteractionInputDriver` 的 `allowed` 前置
// (模态开 ⇒ 分支不进 ⇒ 采集与查体同被抑制,F-4.2;本桥不重复判,避免两份「什么算挡路」)。

using System;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 把「玩家按了交互」翻译成一次采集裁决,并回报该次按键是否被采集链**消费**。
    /// <para>由 <c>InteractionInputDriver</c> 的 E 按下沿调用(claim 钩子);
    /// BootRoot 装配它并注入三个读数委托。</para>
    /// </summary>
    public sealed class HarvestInteractionBridge
    {
        private readonly Func<HarvestInteractionDriver> _driver;
        private readonly Func<WorldPos> _playerCell;
        private readonly Func<long> _tick;
        private readonly int _actorId;

        /// <summary>构造桥。</summary>
        /// <param name="driver">采集驱动的**延迟**读数(未装配 ⇒ null;桥每次调用重取,
        /// 不持快照 —— 装配时序由 BootRoot 保证,晚到不炸)。</param>
        /// <param name="playerCell">玩家**经流确立格**读数(ADR-020 §四)。</param>
        /// <param name="tick">当前逻辑 tick 读数。</param>
        /// <param name="actorId">玩家 id(20 的库存归因面)。</param>
        /// <exception cref="ArgumentNullException">任一委托为 null。</exception>
        public HarvestInteractionBridge(
            Func<HarvestInteractionDriver> driver,
            Func<WorldPos> playerCell,
            Func<long> tick,
            int actorId)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _actorId = actorId;
        }

        /// <summary>
        /// E 按下沿调用:发起一次采集裁决。
        /// </summary>
        /// <returns>true = 采集链**消费**了本次交互(选择器选中了采集点,无论闸门收拒 ——
        /// 调用方不得再触发查体);false = 未消费(未装配 / 无候选 / 目标不是采集点),
        /// 查体照常。<b>false 不是失败</b> —— 是「这次交互不属于采集」的事实回报。</returns>
        public bool ClaimInteract()
        {
            HarvestInteractionDriver driver = _driver();
            if (driver == null) return false;   // 未装配 ⇒ 诚实放行(不吞交互、不抛)

            var intent = new InteractIntent(_playerCell(), true, _tick());
            return driver.TryHarvest(in intent, _actorId).Attempted;
        }
    }
}
