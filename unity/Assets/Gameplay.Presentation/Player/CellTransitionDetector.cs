// ADR-020 §四 —— 跨格检测的静态工具(取格算符 / dt 钳位)。
//
// 权威来源:
//   ADR-020 §四 —— 玩家位移 = 纯表现态,唯一投影 = ActorCellEntered
//   GDD player-controller-and-movement.md —— F-1-1b / R6(FloorToInt 权威)/ EC-1…EC-8
//
// 🔁 **2026-10-25(TD-player-03 收敛轮 · 用户裁定甲案)**:本类原含**实例状态机**
//   (归并算子 + 折返检测 + tick 边沿提交,`OnPositionSample` / `OnTickEdge` + 三状态字段)
//   —— 那是「跨格状态机」的**第二份实现**(平行于 `PlayerController` 内联路径),且是
//   **功能子集** + 带着已被内联修掉的缺陷形态(载荷 tick 读帧末 `CurrentTick`,
//   multi-tick 帧内事件头错位;内联走边沿自有 tick,BCD-码-2 修复)。实例状态机本轮
//   **删除**,行为规格由 `cell_transition_test.cs` 迁到内联路径(唯一实现);
//   历史形态见 git 与本文件的删除注。
//
// 保留面(静态工具,**被内联路径消费** —— 取格已单一真源):
//   - `CellFromPosition`:FloorToInt 三维取格(R6 权威;`PlayerController.OnPositionSample`
//     / `OnUplinkSample` 均走本入口,**禁用** `GetCell()` 的 RoundToInt —— PRE-S19-C 约束 ②)
//   - `ComputeDisplacement`:dt 钳位不累积(AC-1-16)

using UnityEngine;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 跨格检测静态工具(FloorToInt 取格 / dt 钳位)—— 状态机本体住
    /// <see cref="PlayerController"/> 内联路径(唯一实现,TD-player-03 收敛轮裁定甲案)。
    /// </summary>
    public static class CellTransitionDetector
    {
        /// <summary>
        /// 位置 → 整数格(AC-1-07: 使用 Mathf.FloorToInt,三维)。
        /// </summary>
        public static WorldPos CellFromPosition(Vector3 position)
        {
            return new WorldPos(
                Mathf.FloorToInt(position.x),
                Mathf.FloorToInt(position.y),
                Mathf.FloorToInt(position.z));
        }

        /// <summary>
        /// 计算位移(AC-1-16: dt 钳位不累积)。
        /// </summary>
        public static float ComputeDisplacement(float speed, float dt, float maxDt)
        {
            float clampedDt = Mathf.Min(dt, maxDt);
            return speed * clampedDt;
        }
    }
}
