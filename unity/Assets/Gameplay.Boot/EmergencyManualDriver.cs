// sprint-06 G4 · 急救手动入口驱动(2026-10-11)
//
// 权威来源:
//   production/sprints/sprint-06.md §2 G4.1/G4.2 —— 移除自动触发(零输入零事件)
//   ADR-009 Amendment I(急救三 Kind 属判定输入 / 处置事件,落病史流、归属具体病人)
//   ADR-005(主机唯一 Append)· emergency-procedures.md 规则十一
//   sprint-05 T1.3 原 `EmergencyAttemptDriver` 的 `_triggeredPatients` 自动分支
//     **已删除**(P0 接线载体退役)—— 触发权归玩家,本驱动只提供手动入口。
//
// 设计口径:
//   - 驱动 = **手动入口容器**(零 tick 边沿自动逻辑):玩家对在场病人完成急救意图
//     (42 候选列表落笔 / 直读通道 End 的翻译)→ `TryManual` → HostEmergencyProcessor.Process
//     → EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流。
//   - **零输入观察窗 ⇒ 病史流零 EmergencyAttempt**(判据 G4.1;本类无任何隐式触发)。
//   - actorId(施予者)与 method(Manual / Skip)由调用方传入 —— 与 PrescribeIntent 同构;
//     BootRoot 生成玩家后经此传入。
//   - 最小动作表 = 一张行(止血包扎 · action 0;真实动作表归 21a/10 烘焙轮)。
//
// ⚠️ 单线程:tick 由 BootRoot 泵驱动,本驱动只在玩家意图发生时被调用。

using System;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一次手动急救的回执(只读;失败原因不回传 —— 零提示纪律 AC-11-12 同族)。</summary>
    public readonly struct EmergencyManualResult
    {
        /// <summary>是否走完 Process(双事件入流)。</summary>
        public readonly bool Applied;

        public EmergencyManualResult(bool applied)
        {
            Applied = applied;
        }
    }

    /// <summary>
    /// 急救手动入口驱动 —— 把玩家的一次急救意图接进 <see cref="HostEmergencyProcessor"/>
    /// (规则十一)。住装配层(可引 Sim.EmergencyProcedures + Sim.Codec 系)。
    /// </summary>
    public sealed class EmergencyManualDriver
    {
        private readonly HostEmergencyProcessor _processor;
        private readonly IPresenceQuery _presence;

        /// <summary>最小动作表(action 0 = 止血包扎;真实表归 10/21a 烘焙轮)。</summary>
        private static readonly EmergencyActionRow BandageRow = new EmergencyActionRow
        {
            ActionId = 0,
            Polarity = 2,        // 平
            BasePotency = 65536, // 1.0 in Q16.16
            MagThreshold = 50,
            MinEdges = 1,
            MinHoldTicks = 5,
            HalfLifeTicks = 100,
            DurationTicks = 200,
            ResultMul = new long[] { 0, 32768, 65536 }, // Missed/AppliedWeak/Applied
        };

        /// <param name="processor">主机侧急救处理器(规则十一)。</param>
        /// <param name="presence">在场查询 —— 急救前置:病人须在场
        /// (C3-M1;与处方侧 <c>PrescribeFlow</c> 1c 对称)。</param>
        public EmergencyManualDriver(HostEmergencyProcessor processor, IPresenceQuery presence)
        {
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        }

        /// <summary>
        /// 一次手动急救:构造判定输入 → <see cref="HostEmergencyProcessor.Process"/>
        /// (→ EmergencyAttempt + EmergencyTreatmentApplied 双事件入病史流)。
        /// </summary>
        /// <param name="patientId">被施救病人(须在场 —— C3-M1 前置;不在场 ⇒
        /// 返回 <c>Applied = false</c>,零事件)。</param>
        /// <param name="method">结算路径(0 = Manual · 1 = Skip —— 序数承既有测试与 GDD 声明)。</param>
        /// <param name="actorId">施予者(玩家)id。</param>
        /// <param name="tick">当前 tick。</param>
        /// <returns>回执(Applied = 双事件已入流;不在场 = false)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">tick / method / actorId 非法。</exception>
        public EmergencyManualResult TryManual(PatientId patientId, int method, int actorId, long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");
            if (method < 0 || method > 1)
                throw new ArgumentOutOfRangeException(nameof(method), method,
                    "method ∈ {0 = Manual, 1 = Skip}(GDD 规则十一 method 两值闭集)");
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actorId 走 ADR-006 Amendment B 计数器 id 空间,不得为负");

            // C3-M1:在场前置(与处方侧 PrescribeFlow 1c 对称)—— 不在场病人
            // 不得接收双事件(归因到不存在对象);调用方(InteractionInputDriver)
            // 只选在场目标,此检查堵「离场/未出现 id 直调」的路径。
            if (!_presence.IsPresent(patientId))
                return new EmergencyManualResult(false);

            // 判定输入(最小形态:单沿 + 满幅度 ⇒ 稳度门 edges≤1 直通 Applied;判定归主机)
            var attempt = new EmergencyAttemptPayload(
                action: 0,           // HemostasisBandage
                holdTicks: 10,
                edges: 1,
                magPeak: 100,
                magLast: 80,
                method: method,
                actorId: actorId,
                edgeTicks: new[] { (int)tick });

            var ctx = new JudgeContext
            {
                Level = 1,
                MagThresholdEffective = 50,
            };

            _processor.Process(attempt, BandageRow, ctx, patientId, tick, cause: 0);
            return new EmergencyManualResult(true);
        }
    }
}
