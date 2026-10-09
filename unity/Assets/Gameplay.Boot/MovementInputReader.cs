// M2 接线轮阶段 1 尾 · 普通移动的输入读取(2026-10-09)
//
// 权威来源:
//   ADR-011 §一(action-based 官方路线 —— **本文件是该路线的过渡最小面**,见下方登记)
//   ADR-011 §二(急救动作 = 独立直读通道,**不经过本文件** —— 本文件零 Emergency 面)
//   AC-3-A4(Legacy Input Manager 零引用 —— 本文件只用 UnityEngine.InputSystem.*,
//     不出现 UnityEngine.Input 符号;LegacyInputAnalyzer(DY0001)编译期执法)
//   input-system GDD 规则二(Move 动作消费者 = 系统 1)
//
// ⚠️ 复用面登记(2026-10-09 · 本轮偏离,见任务报告):
//   工程已有单一动作资产 `Assets/InputSystem_Actions.inputactions`(含 Player/Move,K&M + 手柄 + XR
//   三套绑重),但 **runtime 装载路径从未交付** —— InputService 自陈「资产装载路径(Boot/Addressables
//   装载归后续故事,本类只消费已装载实例)」,全库零 `InputActionAsset` 运行期装载点(仅 Editor spike
//   经 AssetDatabase)。造装载器 = Addressables 新条目(本地 gitignored 配置)+ GDD 规则五
//   Load→Enable + overrides 装载,是独立故事的面,超出本轮「最小喂入」边界 ⇒ 本轮**直读设备**,
//   把动作资产复用登记为遗留项(不改 input-system epic 已交付代码)。
//   改键 overrides 生效前置 = 该装载器落地;在此之前键位是出厂绑重的等价形状(K&M WASD/方向键)。

using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 普通移动输入读取 —— 每帧把当前设备的移动轴折成一个 <see cref="Vector2"/>(原始轴,
    /// 不做死区 / 曲线 —— 那两段归 F-3.1 装载面,见 <see cref="MovementFeed.ToMoveInput"/> 头注)。
    /// <para>零状态、零分配、可 EditMode 直调(无设备 / 无输入 ⇒ 返回零向量,不抛)。</para>
    /// <para><b>急救通道不在此</b>:Emergency 动作走 3 的直读通道(ADR-011 §二),
    /// 本文件不 FindAction、不 Enable、不 Disable 任何动作。</para>
    /// </summary>
    public static class MovementInputReader
    {
        /// <summary>读取本帧移动轴(x = 左右,y = 前后);无设备 / 无输入 ⇒ (0, 0)。</summary>
        /// <remarks>键盘优先(含 WASD 与方向键的并集),键盘全零才落手柄左摇杆 —— 两路同推时
        /// 叠加会把 ‖·‖ 顶过 1,而 AC-1-09 是硬断言;**择一**避免叠加越界(防叠加),
        /// 且键盘支**出入口径向钳制**(W+D 对角 ‖·‖ = √2 > 1)—— 双保险,恒出单位圆。</remarks>
        public static Vector2 ReadMoveAxis()
        {
            Vector2 kb = ReadKeyboard();
            if (kb.x != 0f || kb.y != 0f)
            {
                // 出口径向钳制:W+D 等正交组合会产出 (±1, ±1)(‖·‖ = √2 > 1),
                // 与「读取面输出须在单位圆内」契约矛盾 ⇒ 超模长才径向归一(非逐分量 ——
                // 逐分量钳制会让斜向行程与正向不等,违 F-3.1 Ⓐ ①)。
                return kb.sqrMagnitude <= 1f ? kb : kb.normalized;
            }

            return ReadGamepadLeftStick();
        }

        private static Vector2 ReadKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return Vector2.zero;

            float x = 0f;
            float y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;

            // 对向同按 ⇒ 抵消为 0(不产生 (±2, 0) 的越界轴)。
            // ⚠️ 正交两键(W+D)会产出 (1, 1),‖·‖ = √2 > 1 —— 本支**不**在这里压,
            // 由 ReadMoveAxis 出口的径向钳制统一处理(择一防叠加 + 出口钳制双保险)。
            return new Vector2(x, y);
        }

        private static Vector2 ReadGamepadLeftStick()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null) return Vector2.zero;

            Vector2 stick = pad.leftStick.ReadValue();
            // 摇杆读值本就在单位圆内;再钳一次只是防驱动异常值顶破 AC-1-09 的上游前提。
            return stick.sqrMagnitude <= 1f ? stick : stick.normalized;
        }
    }
}
