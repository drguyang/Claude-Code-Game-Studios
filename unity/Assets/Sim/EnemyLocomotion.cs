// ADR-016 §二/§五 · GDD enemy-ai.md F-27-4 —— 敌人逐 tick 整数推进算子(27 的 sim 侧,决策不进流)。
//
// 权威来源:
//   GDD enemy-ai.md F-27-4(:672-697)—— 推进公式的权威形态:
//     `acc := acc + ENEMY_SPEED; while acc ≥ FIX_ONE: acc −= FIX_ONE; cell := Path[cursor+1]`
//     —— **沿整数路径逐格推进**(while 循环),非直线逼近。
//   GDD enemy-ai.md F-27-4 `Moving(e)` 定义 —— **`acc` 只在 Moving 为真的 tick 前进;
//     Moving 转假时 acc 保持,不归零**(否则 Engage/Alert 站定攒满 acc,脱离首 tick 瞬移一格
//     = F-27-3 同型穿墙错误)。
//   ADR-016 §二 —— 决策不进流、效果进流:本算子是**纯函数**,只算派生态(LogiPose/acc/cursor),
//     **零 IEventSink 依赖**;跨格的「效果」(ActorCellEntered 等)由调用方按流契约另行写。
//   ADR-016 §九 —— 冻结时同时冻结 acc(调用方以 Moving=false 表达,本算子保持 acc)。
//
// S19-3 判据 ②:本文件零 `Transform` / `Vector3` / `UnityEngine` 引用(源码判);
// 判据②的反射判与「效果进流」夹具见 `logi_pose_test.cs`。
//
// ⚠️ `EnemyPathing.StepAccumulator` 是**直线逼近**(每溢出一格,朝目标 Sign 方向一步),
//   语义是「朝目标走」的简化步进,不是 F-27-4 的「沿路径走」。本算子按 F-27-4 权威形态
//   实现沿路径版本;两者并存不合并(S19-3 不改既有在盘件,合并与否归 TD-player-03
//   同型的后续收敛轮)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 敌人逐 tick 整数推进(GDD F-27-4 权威形态:沿路径 while 循环 + acc 冻结纪律)。
    /// </summary>
    public static class EnemyLocomotion
    {
        /// <summary>定点 1.0(Fix.OneRaw;本文件显式引用常量,避免对 Fix 布局产生新依赖面)。</summary>
        public const long FixOne = 1L << DaYiJingCheng.Sim.Contracts.Fix.FractionalBits;

        /// <summary>朝向字节语义:0..5 = +X / −X / +Y / −Y / +Z / −Z(与 EnemyPathing 邻接顺序一致)。</summary>
        public static byte FacingBetween(WorldPos from, WorldPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            int dz = to.Z - from.Z;
            if (dx > 0) return 0;
            if (dx < 0) return 1;
            if (dy > 0) return 2;
            if (dy < 0) return 3;
            if (dz > 0) return 4;
            if (dz < 0) return 5;
            return 0; // 同格 —— 无方向信息,保持 +X(调用方在无下一格时应保持原 Facing,见 Advance)
        }

        /// <summary>
        /// 逐 tick 推进(GDD F-27-4)。
        /// </summary>
        /// <param name="pose">当前逻辑位姿(in —— 派生态真值,本算子不改写)。</param>
        /// <param name="acc">定点累加器余数(ref —— **住敌人实体**,跨 tick 存续;
        /// 约束:`0 ≤ acc &lt; FixOne` 恒成立,调用方保证)。</param>
        /// <param name="path">整数路径(自 `EnemyPathing.FindPath`;含起点格)。空路径 = 原地。</param>
        /// <param name="pathCursor">路径游标(ref,积分量,跨 tick 存续;指向已到达格的下标)。</param>
        /// <param name="moving">本 tick 是否推进位移(GDD `Moving(e)`:¬Frozen ∧ ¬Down ∧
        /// State ∈ {Patrol, Chase, Flank, Disengage}。**false ⇒ acc 保持不累加不归零**)。</param>
        /// <param name="speed">定点格速 `ENEMY_SPEED`(格/tick;`0 &lt; v ≤ ENEMY_SPEED_MAX` 归调用方保证)。</param>
        /// <returns>推进后的逻辑位姿(新值;原 `pose` 不改写 —— readonly struct 语义)。</returns>
        public static LogiPose Advance(
            in LogiPose pose,
            ref Fix acc,
            IReadOnlyList<WorldPos> path,
            ref int pathCursor,
            bool moving,
            Fix speed)
        {
            if (!moving)
            {
                // F-27-4 冻结分支:acc 不累加、不清零(规则十:冻结期 acc 冻住)。
                // Facing 亦保持(无新方向信息)。
                return pose;
            }

            acc = new Fix(acc.Raw + speed.Raw);
            var cell = pose.Cell;
            byte facing = pose.Facing;

            // while 循环(权威形态):一次大步长可跨多格 —— 每溢出一次恰好前进一格。
            while (acc.Raw >= FixOne)
            {
                int next = pathCursor + 1;
                if (next >= path.Count)
                {
                    // 路径耗尽:不再前进。余数按 F-27-4 语义处理 ——
                    // 权威公式在 |Path|−1 处 cursor 不再前进,acc 的溢出余量无下格可消费;
                    // 此处把 acc 钳回溢出前余数不可得(公式无此定义),按「到达终点后
                    // acc 归零」收口(与 ValidateAccumulator 的 [0,1) 不变式一致,
                    // 且防解冻首 tick 吐累积量)。调用方在路径耗尽后应置 moving=false。
                    acc = Fix.Zero;
                    break;
                }

                acc = new Fix(acc.Raw - FixOne);
                pathCursor = next;
                cell = path[next];
                // DirFrom(cell, Path[min(cursor+1, |Path|−1)]):朝向取「路径上下一格」方向;
                // 终点格(无下一格)时保持上一段方向。
                int peek = next < path.Count - 1 ? next + 1 : -1;
                if (peek >= 0)
                {
                    facing = FacingBetween(cell, path[peek]);
                }
            }

            return new LogiPose(cell, facing);
        }
    }
}
