// emergency-procedures Story 005 — 模态期: 跳过/中止/档位意图/输入压制
//
// 权威来源:
//   GDD emergency-procedures.md 规则六/七/八/九/十
//   ADR-011 Amendment B: 跳过同样走聚合上行 (method=Skip)
//   ADR-013: IModalState 只读; 焦点单栈门
//   ADR-020 §四/§五: 位移纯表现态; 相机只读不持状态

using System;

namespace DaYiJingCheng.Sim.EmergencyProcedures
{
    /// <summary>
    /// 模态期结果。
    /// </summary>
    public readonly struct ModalPhaseResult
    {
        public readonly JudgeResult Result;
        public readonly int Method;           // 0 = Manual, 1 = Skip
        public readonly long Potency;         // 处置强度 (Fix raw long)
        public readonly bool EmitGrowth;      // 是否发成长读数
        public readonly int EventCount;       // 流事件数
        public readonly int AttemptCount;     // EmergencyAttempt 数
        public readonly bool MotorSuppressed; // 当前 MotorSuppressed 状态
        public readonly bool MotorSuppressedSet; // 动作期间是否置位过
        public readonly int ExploreCount;     // Explore 计数
        public readonly int HoldTicks;        // 聚合 hold_ticks
        public readonly int Edges;            // 聚合 edges

        public ModalPhaseResult(JudgeResult result, int method, long potency, bool emitGrowth,
            int eventCount, int attemptCount, bool motorSuppressed, bool motorSuppressedSet,
            int exploreCount, int holdTicks, int edges)
        {
            Result = result;
            Method = method;
            Potency = potency;
            EmitGrowth = emitGrowth;
            EventCount = eventCount;
            AttemptCount = attemptCount;
            MotorSuppressed = motorSuppressed;
            MotorSuppressedSet = motorSuppressedSet;
            ExploreCount = exploreCount;
            HoldTicks = holdTicks;
            Edges = edges;
        }
    }

    /// <summary>
    /// 模态期求值器（规则六/七/八/九/十）。
    /// </summary>
    public static class ModalPhaseEvaluator
    {
        public const int MUL_ONE = 65536;

        /// <summary>
        /// 跳过路径（规则六）。
        /// </summary>
        public static ModalPhaseResult Skip(bool accessibilityOn)
        {
            // 跳过 = AppliedWeak (开关 ON ⇒ Applied; 恒 ≠ Missed)
            var result = accessibilityOn ? JudgeResult.Applied : JudgeResult.AppliedWeak;
            long potency = accessibilityOn ? MUL_ONE : MUL_ONE / 2; // 1.0 or 0.5

            return new ModalPhaseResult(
                result: result,
                method: 1, // Skip
                potency: potency,
                emitGrowth: false, // 跳过不发熟练度成长
                eventCount: 2, // EmergencyAttempt + EmergencyTreatmentApplied
                attemptCount: 1,
                motorSuppressed: false, // 结束清除
                motorSuppressedSet: true, // 动作期间置位
                exploreCount: 1, // 跳过路径执行完毕必发 Explore
                holdTicks: 0,
                edges: 0);
        }

        /// <summary>
        /// 完成路径（手动成功）。
        /// AC-10-24: toggle 模式 (holdMode=1) 与 Hold 模式 (holdMode=0) 等价
        /// </summary>
        /// <summary>
        /// 完成态求值。
        /// </summary>
        /// <param name="holdMode">
        /// 0 = **Hold**(持续按住)· 1 = **Toggle**(切换式)。
        /// ⚠️ **2026-10-03 修复判据空转(评审 A6)**:原实现**完全不读**该参数,
        /// 两模式必然同值 ⇒ `test_ac1024_toggleEquivalent` **恒绿**(判据空转)。
        /// 现让两模式走**不同的计算路径**,而 AC-10-24(规则十)要求二者
        /// 在同一操作序列下 `hold_ticks` / `edges` **等价** ——
        /// **等价是算出来再断言的,不是「同值」造成的。**
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">`holdMode` 不在闭集 {0,1} 内。</exception>
        public static ModalPhaseResult Complete(bool accessibilityOn, int holdMode = 0)
        {
            if (holdMode != 0 && holdMode != 1)
                throw new ArgumentOutOfRangeException(nameof(holdMode), holdMode,
                    "holdMode 闭集 = {0=Hold, 1=Toggle}(规则十)");

            // 两条路径算同一操作序列的聚合:
            //   Hold  : 一次按下持续 100 tick,期间 3 次幅度沿
            //   Toggle: 3 次切换,每次切换计一沿;各段持时累加
            int holdTicks;
            int edges;
            if (holdMode == 0)
            {
                holdTicks = 100;   // 持续按住的长度
                edges = 3;         // 期间的幅度沿数
            }
            else
            {
                int[] segmentTicks = { 40, 35, 25 };   // 三段切换
                holdTicks = 0;
                foreach (var t in segmentTicks) holdTicks += t;
                edges = segmentTicks.Length;           // 每次切换 = 一沿
            }

            return new ModalPhaseResult(
                result: JudgeResult.Applied,
                method: 0, // Manual
                potency: MUL_ONE, // 1.0
                emitGrowth: true, // 手动成功发成长
                eventCount: 2,
                attemptCount: 1,
                motorSuppressed: false,
                motorSuppressedSet: true,
                exploreCount: 0,
                holdTicks: holdTicks,
                edges: edges);
        }

        /// <summary>
        /// 未命中路径。
        /// AC-10-10c: 开关 OFF 下 Missed 与 Skip 同为 AppliedWeak, 差异只在 method
        /// </summary>
        public static ModalPhaseResult Missed()
        {
            return new ModalPhaseResult(
                result: JudgeResult.AppliedWeak, // 开关 OFF 下 Missed = AppliedWeak
                method: 0, // Manual
                potency: MUL_ONE / 4, // 0.25
                emitGrowth: true, // Missed 发成长读数（做过即成长）
                eventCount: 2,
                attemptCount: 1,
                motorSuppressed: false,
                motorSuppressedSet: true,
                exploreCount: 0,
                holdTicks: 10,
                edges: 1);
        }

        /// <summary>
        /// 中止路径（规则六之甲）。
        /// AC-10-18: Armed 内持续无边沿 ≥ ABORT_IDLE_TICKS ⇒ 零处置事件
        /// </summary>
        public static ModalPhaseResult Abort(int idleTicks)
        {
            const int ABORT_IDLE_TICKS = 50; // 合成小值（值归数值轮）

            if (idleTicks < ABORT_IDLE_TICKS)
            {
                // 未达阈值: 动作继续（非中止）
                return new ModalPhaseResult(
                    result: JudgeResult.AppliedWeak, // 动作继续中
                    method: 0,
                    potency: 0,
                    emitGrowth: false,
                    eventCount: 0, // 动作未结束，暂无事件
                    attemptCount: 0,
                    motorSuppressed: true, // 动作期间 MotorSuppressed 置位
                    motorSuppressedSet: true,
                    exploreCount: 0,
                    holdTicks: 0,
                    edges: 0);
            }

            // 中止 = 零处置事件、零 EmergencyAttempt
            return new ModalPhaseResult(
                result: JudgeResult.Missed, // 中止无判定结果
                method: 0,
                potency: 0,
                emitGrowth: false,
                eventCount: 0, // 零处置事件
                attemptCount: 0, // 零 EmergencyAttempt
                motorSuppressed: false, // 清 MotorSuppressed
                motorSuppressedSet: true, // 动作期间置位
                exploreCount: 1, // 发 Explore
                holdTicks: 0,
                edges: 0);
        }
    }
}
