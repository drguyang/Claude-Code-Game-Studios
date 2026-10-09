// M2 接线轮阶段 1 尾 · 输入 → PlayerController 采样 → 跨格事件运行期可达(2026-10-09)
//
// 权威来源:
//   ADR-020 §一(移动 = CharacterController,唯一位移写入点)· §四(玩家位移 = 纯表现态,
//     唯一 sim 投影 = ActorCellEntered;连续位置永不写流)· §五(相机/控制器只渲染不持状态)
//   ADR-011 §二(输入是意图源;**本文件只接普通移动** —— 急救 <50 ms 直读通道不经过此处)
//   AC-1-09(‖MoveInput‖ ≤ 1 硬断言 —— 本文件在喂入前先把轴归一,不把越界输入递给 Move)
//   player GDD F-1-2(SPEED_MODE × ‖MoveInput‖ —— 本文件只给方向,手感数值在 LocomotionConfig)
//
// 落点理由:BootRoot.Update 是薄 MonoBehaviour 壳(只搬运时间与引用),**喂入次序与帧泵**
//   的可测身住在本静态类 —— EditMode 直接调 <see cref="PumpFrame"/> 即可驱动真装配袋,
//   不依赖键盘硬件、不依赖场景、不依赖 Boot 启动序(Addressables 那两步与移动无关)。
//
// 帧内次序(不可改):**Move(表现态位移)→ OnPositionSample(采样判格)→ Advance → OnTickEdge
//   (边沿提交)**。先采样后边沿 ⇒ 本帧位移在本帧可达的最近一次 tick 边沿被提交;
//   次序颠倒 ⇒ 跨格事件恒晚一 tick 才可见(表现与流撕裂)。

using DaYiJingCheng.Gameplay.Presentation.Player;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 移动输入喂入与帧泵 —— 「输入向量 → 表现态位移 → 位置采样 → tick 边沿提交」的唯一生产次序。
    /// <para>纯静态、零状态(不持有玩家 / tick 驱动 / 场景引用)⇒ EditMode 可直调(可测性要求)。</para>
    /// <para><b>不进 sim 程序集、不写三流</b>:本类产出的唯一 sim 投影是
    /// <see cref="PlayerController.OnTickEdge"/> 内的 <c>ActorCellEntered</c>(ADR-020 §四)。</para>
    /// </summary>
    public static class MovementFeed
    {
        /// <summary>输入轴 → <c>Move</c> 入参(水平面;Y ≡ 0 —— 重力由控制器内部处理)。</summary>
        /// <remarks>
        /// 归一是 <b>径向</b> 的(承 GDD input-system F-3.1 Ⓐ ①:逐分量钳制会让斜向行程与
        /// 正向不等 ⇒ 斜走更快);模长 ≤ 1 的轴原样通过(摇杆轻推的行程保留)。
        /// 非有限输入(NaN / ∞)归零 —— 与 F-3.1 ④ 同一口径,防污染整条速度链。
        /// ⚠️ F-3.1 的死区 + smoothstep 曲线(AxisProcessor)是**手感层的另一段**,
        /// 其常量走 ADR-014 装载面 —— 尚无运行期装载器,本文件不内置其种子值(登记见任务报告)。
        /// </remarks>
        /// <param name="axis">原始输入轴(x = 左右,y = 前后)。</param>
        /// <returns>‖·‖ ≤ 1 的水平移动向量。</returns>
        public static Vector3 ToMoveInput(Vector2 axis)
        {
            // 非有限防护:NaN/∞ 进 magnitude 会把整条速度链染污(F-3.1 ④ 同口径)。
            if (float.IsNaN(axis.x) || float.IsNaN(axis.y)
                || float.IsInfinity(axis.x) || float.IsInfinity(axis.y))
            {
                return Vector3.zero;
            }

            float m = axis.magnitude;
            if (m <= 1f)
                return new Vector3(axis.x, 0f, axis.y);
            if (m <= 0f) // m == 0 已被上一支捕获;此支防 0/0 之外的退化
                return Vector3.zero;

            Vector2 n = axis / m;
            // 出口二次钳制(float32 舍入):axis / m 的模长可略 > 1(超额 ~1e-7 量级、
            // 千分级概率)而 AC-1-09 是**严格 > 1f 即 throw** ⇒ 归一后按实际模长再缩一次
            // (仍是径向,不改方向)。
            float n2 = n.sqrMagnitude;
            if (n2 > 1f)
                n /= Mathf.Sqrt(n2);
            return new Vector3(n.x, 0f, n.y);
        }

        /// <summary>单帧喂入:位移一次 → 立刻采样当前位置(次序见文件头)。</summary>
        /// <param name="player">已 <see cref="PlayerController.Initialize"/> 的玩家控制器。</param>
        /// <param name="moveAxis">原始输入轴(内部经 <see cref="ToMoveInput"/> 归一)。</param>
        public static void ProcessMovementFrame(PlayerController player, Vector2 moveAxis)
        {
            if (player == null) return;

            // 表现态位移(ADR-020 §四:唯一写点 = CharacterController.Move)
            player.Move(ToMoveInput(moveAxis));

            // 采样即判格(不提交 —— 提交只在 tick 边沿,见 OnTickEdge)
            player.OnPositionSample(player.transform.position);
        }

        /// <summary>整帧泵 = 生产 <c>BootRoot.Update</c> 的可测身:喂入 → tick 推进 → 逐边沿提交。</summary>
        /// <param name="player">已初始化的玩家控制器。</param>
        /// <param name="tickDriver">tick 驱动器(墙钟 → 整 tick;测试可注入受控 delta 使边沿确定性)。</param>
        /// <param name="moveAxis">原始输入轴。</param>
        /// <param name="deltaSeconds">本帧逻辑时间(生产传 <c>Time.unscaledDeltaTime</c>)。</param>
        public static void PumpFrame(PlayerController player, SimTickDriver tickDriver,
                                     Vector2 moveAxis, double deltaSeconds)
        {
            if (player == null) return;

            ProcessMovementFrame(player, moveAxis);

            if (tickDriver == null) return;
            int steps = tickDriver.Advance(deltaSeconds);
            for (int i = 0; i < steps; i++)
            {
                player.OnTickEdge();
            }
        }
    }
}
