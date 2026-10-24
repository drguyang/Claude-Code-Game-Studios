// ADR-020 §Key Interfaces —— 玩家移动抽象(S19-2 · TD-player-02 兑现)。
//
// 权威来源:
//   ADR-020 §Key Interfaces:396-402 —— 四成员权威签名(签名齐备,本件只誊写)
//   ADR-025 §②/§③ —— IPlayerMotor / ICameraRig 落位 = Gameplay.Presentation(非 Contracts)
//   sprint-19.md §S19-2 判据 ①②③④ + PRE-S19-C 裁定(用户 2026-10-24「只读边沿」)
//
// 落位(ADR-025 §② 裁定 ①′):
//   `IPlayerMotor` 住 `Gameplay.Presentation` —— 与 `ICameraRig` 同装配(判据 ④)。
//   `WorldPos` 经 `Sim.Contracts` 可见(该程序集是唯一跨界类型源);接口本体不进
//   `Sim.Contracts`,因为它携带 `Vector3`(表现态)—— 进 Contracts 会把引擎类型
//   带进门 A 程序集的邻居,违其「整数域」取向。
//
// ⚠️ 只读契约(ADR-020 §Key Interfaces 逐字「不引用 sim,不进流」):
//   本接口是 2(相机)等表现层消费者的**只读上游**;Append 主路径(`ActorCellEntered`)
//   归 `PlayerController.OnTickEdge`(ADR-020 §四),`ConsumeCellChanged` **不夺**它 ——
//   只观察已提交的边沿(PRE-S19-C 用户裁定「只读边沿」)。

using UnityEngine;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Player
{
    /// <summary>
    /// 移动输入(ADR-020 §Key Interfaces 的 <c>MoveInput</c>;ADR-011 动作映射的供给形状)。
    /// <para>薄包装:承载水平移动意图向量(‖·‖ ≤ 1,AC-1-09 语义沿用)。
    /// 独立成类型而非裸 <c>Vector3</c>,使 <see cref="IPlayerMotor.Tick"/> 的签名与
    /// ADR-020 权威形状逐字一致,并给「输入意图」一个具名身份(与表现态 <c>Vector3</c> 区分)。</para>
    /// </summary>
    public readonly struct MoveInput
    {
        /// <summary>水平移动意图(相机相对方向;‖·‖ ≤ 1,零向量 = 无输入)。</summary>
        public readonly Vector3 Value;

        public MoveInput(Vector3 value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// 玩家移动抽象 —— 表现层只读上游(ADR-020 §Key Interfaces 权威四成员)。
    /// </summary>
    /// <para>⚠️ <b>因果绑定,非调用次序</b>(S19-2 实施期加注,PRE-S19-C 约束 ③):
    /// GDD <c>player-controller-and-movement.md:128</c> 的「跨格时发一条世界流事件」
    /// 读作 —— Append 是<b>因</b>(<c>OnTickEdge</c> 主路径),边沿可观察是<b>果</b>
    /// (<see cref="ConsumeCellChanged"/>);不是「消费者调用 → 才发事件」的调用次序。
    /// 今日零消费者(2 的 story-003 只读 <c>Position</c>)⇒ 本接口<b>只落形状</b>,
    /// 不为「有调用点」而发明消费者。</para>
    public interface IPlayerMotor
    {
        /// <summary>
        /// 每帧求值入口 —— 移动 + 重力 + 坠落重置(实现 = <c>PlayerController.Move</c> 同路)。
        /// </summary>
        void Tick(float deltaTime, in MoveInput input);

        /// <summary>
        /// 当前整数格(ADR-015)。<b>实现必须走 <c>CellFromPosition</c>(FloorToInt)</b>,
        /// 与 <c>ActorCellEnteredPayload.Cell</c> 同源;禁用 <c>GetCell()</c>(RoundToInt,
        /// 负坐标 / 半格处取值不同 —— PRE-S19-C 约束 ②)。
        /// </summary>
        WorldPos Cell { get; }

        /// <summary>连续位置 —— 表现态,永不写流(ADR-020 §四)。</summary>
        Vector3 Position { get; }

        /// <summary>
        /// 消费一次「已提交的跨格边沿」(PRE-S19-C 用户裁定:只读边沿,无出参)。
        /// <para>真值 = 自上次消费以来恰发生一次提交边沿(边沿锁存器置位);
        /// 消费即清锁存器。与 Append 主路径的 <c>_hasPending</c> 完全不相交 ——
        /// 消费本边沿<b>不</b>影响事件是否发出(事件已在其自己的提交点发出)。</para>
        /// </summary>
        bool ConsumeCellChanged();
    }
}
