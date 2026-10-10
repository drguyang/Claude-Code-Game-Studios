// sprint-05 T1.3 · 急救链接线驱动(2026-10-10)
//
// 权威来源:
//   ADR-009 Amendment I(急救三 Kind 属判定输入 / 处置事件,落病史流、归属具体病人)
//   ADR-005(主机唯一 Append)· emergency-procedures.md 规则十一
//   F-6 复评实测:HostEmergencyProcessor 零生产调用方 ⇒ 运行期链可达性 0% ⇒ 本驱动补上调用方
//
// 设计口径:
//   - 驱动 = 每 tick 边沿检查在场病人,对未触发过急救的病人自动触发一次。
//   - 触发条件 = 病人在场(与 T1.2 开账驱动同构)。
//   - 动作参数 = 最小接线载体(P0;真实参数归 10 急救动作 + 数值轮)。
//   - 有界性:每病人最多触发一次急救(接线载体;真实触发率归 10 的输入聚合器)。
//
// ⚠️ 单线程:tick 边沿由 BootRoot 的 Update 泵驱动,与 sim 的「主机唯一 Step」同线程。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 急救链接线驱动 —— 把 <see cref="HostEmergencyProcessor"/> 接进 tick 边沿序列(sprint-05 T1.3)。
    /// <para><b>只驱动,不持游戏状态</b>(与 BootRoot 同纪律):急救真源在事件流,
    /// 本类只按节奏触发写事件。</para>
    /// </summary>
    public sealed class EmergencyAttemptDriver
    {
        private readonly HostEmergencyProcessor _processor;
        private readonly IPresenceQuery _presence;
        private readonly HashSet<int> _triggeredPatients = new HashSet<int>();

        /// <param name="processor">主机侧急救处理器(规则十一)。</param>
        /// <param name="presence">在场查询(触发条件「病人在场」的真源)。</param>
        public EmergencyAttemptDriver(HostEmergencyProcessor processor, IPresenceQuery presence)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        }

        /// <summary>
        /// tick 边沿驱动 —— 对在场且未触发过急救的病人自动触发一次。
        /// </summary>
        /// <param name="tick">本边沿自己的逻辑 tick(由帧泵回推,非末 tick)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 为负。</exception>
        public void OnTickEdge(long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            // 遍历在场病人,对未触发过急救的病人触发
            // ⚠️ P0 接线载体:真实触发点 = 玩家急救操作(归 10 急救动作 + 输入聚合器)
            foreach (var patientId in _presence.PresentPatientIds())
            {
                if (_triggeredPatients.Contains(patientId))
                    continue;

                // 最小动作参数(P0 接线载体)
                var attempt = new EmergencyAttemptPayload(
                    action: 0,           // HemostasisBandage
                    holdTicks: 10,
                    edges: 1,
                    magPeak: 100,
                    magLast: 80,
                    method: 0,          // Manual
                    actorId: 0,          // 玩家 id(BootRoot 生成时分配)
                    edgeTicks: new[] { (int)tick });

                var action = new EmergencyActionRow
                {
                    ActionId = 0,
                    Polarity = 2,        // 平
                    BasePotency = 65536, // 1.0 in Q16.16
                    MagThreshold = 50,
                    MinEdges = 1,
                    MinHoldTicks = 5,
                    HalfLifeTicks = 100,
                    DurationTicks = 200,
                    ResultMul = new long[] { 0, 32768, 65536 } // 0.0, 0.5, 1.0
                };

                var ctx = new JudgeContext
                {
                    Level = 1,
                    MagThresholdEffective = 50
                };

                _processor.Process(attempt, action, ctx, new PatientId(patientId), tick, cause: 0);
                _triggeredPatients.Add(patientId);
            }
        }
    }
}
