// 10 侧移动压制写面(story-011 · 2026-11-02)
//
// ── 为什么要有这一层(不是多余的包装)──────────────────────────────────────────
// `AC-1-23` 的机械执行体(`Tests/EditMode/PlayerController/motor_lease_test.cs` 的
// `test_ac123_callSitesSubsetOfWhitelist`)对**调用点所在的程序集**做白名单:
//
//     AllowedCallerAssemblies = { Gameplay.Presentation, Gameplay.Input,
//                                 Sim, Sim.Contracts, Sim.Codec }
//     注释原文:「Gameplay.Presentation —— 4 交互 / 10 急救 / 25 战斗的宿主程序集」
//
// 而 10 的运行期驱动 `EmergencyChannelDriver` 住 **`Gameplay.Boot`(装配层)**。
// 直接在那里调 `MotorLease.Acquire/Release` 会被该门判红 —— **2026-11-02 实现轮实测命中**。
//
// 两种处置:
//   · **甲(本文件)**:把 10 的写面下放到 `Gameplay.Presentation` —— 白名单**原样保留**,
//     且与 `ModalGate.SetMotorSuppression`(4 侧入口)形成对称结构。
//   · 乙:把 `Gameplay.Boot` 加进白名单 —— **否决**。装配层什么都装得下,一旦进白名单,
//     任何未来在 Boot 里写的代码都能压住玩家,门就失去了「只有那三个系统能压」这句判据。
//     **门红是因为位置错了,不是因为门太严。**
//
// ── 纪律(承 `MotorLease` 头注)────────────────────────────────────────────────
//   · 只碰 `LeaseSource.Emergency` 那一位 —— 不读、不清别人的位;
//   · 幂等 —— 位语义,重复 Acquire 不计数;
//   · **无到期** —— 压制期由 10 自己的相位规则说了算,本类零计时器。
//
// 权威来源:
//   `design/gdd/emergency-procedures.md` 规则九 `:374`
//   `design/gdd/player-controller-and-movement.md` 轴 4 `:271` / `OQ-4-13` / `AC-1-23`
//   `production/epics/emergency-procedures/story-011-motor-suppression-lease-wiring.md`

using System;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 10 急救动作的移动压制**写面**(`LeaseSource.Emergency`)。
    /// <para>⚠️ 住 `Gameplay.Presentation` 是 `AC-1-23` 白名单的**要求**,不是习惯 ——
    /// 见文件头注(门按调用点所在程序集判)。</para>
    /// </summary>
    public static class EmergencyMotorSuppression
    {
        /// <summary>
        /// 置位 / 清位 10 的压制。**只碰 <see cref="LeaseSource.Emergency"/> 那一位**。
        /// </summary>
        /// <param name="lease">1 的租约真源(`PlayerController.MotorLease`)。</param>
        /// <param name="acquire">true = 动作开始置位;false = 动作结束(含跳过 / 中止)清位。</param>
        /// <exception cref="ArgumentNullException"><paramref name="lease"/> 为 null。
        /// ⚠️ 「未绑定」的容错由**调用方**负责(10 的驱动在判空后跳过调用)——
        /// 本类与姊妹件 <c>ModalGate.SetMotorSuppression</c> 同为 fail-loud,不静默吞 null。</exception>
        public static void Set(MotorLease lease, bool acquire)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            if (acquire)
                lease.Acquire(LeaseSource.Emergency);
            else
                lease.Release(LeaseSource.Emergency);
        }
    }
}
