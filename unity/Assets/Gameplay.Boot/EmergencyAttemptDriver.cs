// sprint-05 T1.3 → **sprint-06 G4.1 退役**(2026-10-11)
//
// ⚠️ **本文件已无生产作用** —— 自动触发分支 `_triggeredPatients` 已删除。
//
// 历史:
//   原为「每 tick 边沿对在场且未触发过急救的病人自动触发一次」(P0 接线载体)。
//   G4.1(2026-10-11 用户裁定「急救手动化」)判据 = **零输入观察窗,病史流零 EmergencyAttempt**
//   ⇒ 自动分支与判据互斥,删除。触发权归玩家:手动入口 = <see cref="EmergencyManualDriver"/>。
//
// 保留形态:占位壳(字段 + tick 边沿钩子 + fail-loud),**零 Append / 零状态**。
//   · BootRoot 原调 <c>_services.EmergencyAttemptDriver.OnTickEdge(tick)</c> —— 本壳保持该
//     调用面不炸,但不再触发任何事件(零输入零事件语义由 EditMode 判据守)。
//   · 组合根 / 服务袋的 EmergencyAttemptDriver 装配**保留**(引用面不炸),驱动体 = 空操作。
//
// ⚠️ 移除的自动分支已由 `emergency_manual_driver_test` 的「零输入观察窗」判据**反向守死**
//   (重新引入自动触发 ⇒ 该测红)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 急救触发驱动(退役壳)—— G4.1 移除自动触发后保留的**零操作占位**。
    /// <para>真实入口 = <see cref="EmergencyManualDriver"/>;本类不再产生任何事件。</para>
    /// </summary>
    public sealed class EmergencyAttemptDriver
    {
        private readonly IPresenceQuery _presence;

        /// <param name="presence">在场查询(退役壳仅保留依赖面;不再读它触发事件)。</param>
        public EmergencyAttemptDriver(IPresenceQuery presence)
        {
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        }

        /// <summary>
        /// tick 边沿钩子(空操作)—— 兼容 BootRoot 的调用面。
        /// <para><b>零输入 ⇒ 零事件</b>(G4.1 判据):本方法不触发任何急救事件。</para>
        /// </summary>
        /// <param name="tick">逻辑 tick(仅校验非负;不产生副作用)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            // G4.1:自动触发已移除,零事件。
        }
    }
}
