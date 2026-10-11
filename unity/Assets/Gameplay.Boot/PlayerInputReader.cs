// sprint-07 S2 · 玩家交互输入读取(E = Interact · F = Emergency · 2026-10-11)
//
// 权威来源:
//   ADR-011 §一(action-based 官方路线 —— 本文件是**直读设备的过渡最小面**,同
//     MovementInputReader 登记:动作资产 runtime 装载 = 独立故事面,尚未交付)
//   input-system GDD 规则二(Interact = 系统 4 · Emergency = 10 急救直读)
//   sprint-07 §2 S2.2 / S2.5(按 E 近病人 → 查体 · 按 F → 急救手动链)
//
// ⚠️ 语义与 MovementInputReader 对齐:**直读设备、不碰动作资产** ——
//   · Interact = Keyboard <E> 或 Gamepad <buttonNorth>(inputactions 绑重等价形状);
//   · Emergency = Keyboard <F> 或 Gamepad <buttonWest>;
//   · 边沿语义:按下沿(wasPressed)才消费 —— 持键重复按下不重复触发(查体/急救是
//     「一次交互一次动作」,不是持续行为);
//   · 零状态、零分配、可 EditMode 直调(无设备 ⇒ not-pressed,不抛)。
//
// ⚠️ 绑定仅服务**单机本地最小路径**;多机走 ADR-001 第二 QoS / 10 的直读通道
//   (本文件零 Emergency 动作语义 —— 只读「F 是否被按下」,判定归 BootRoot)。

using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 玩家交互输入读取 —— 每帧把 Interact(E)/Emergency(F) 的**按下沿**折成布尔。
    /// <para>承 <see cref="MovementInputReader"/> 同构:直读设备、零动作资产依赖、
    /// 无设备返回「未按下」不抛(batch / EditMode 可靠)。</para>
    /// <para><b>不持状态</b> —— 本类是纯函数读面;「上一帧是否已消费」由调用方
    /// (BootRoot Update)持有,此处只读当前设备位。</para>
    /// </summary>
    public static class PlayerInputReader
    {
        /// <summary>Interact 按下读入(当前按住;边沿化 = 调用方逐帧比对 wasPressed)。
        /// <para>键盘 = <c>E</c>(inputactions Interact 绑重);手柄 = <c>buttonNorth</c>(Y/△)。</para></summary>
        public static bool InteractPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonNorth.isPressed;
        }

        /// <summary>Emergency 按下沿(<c>F</c> 或手柄 <c>buttonWest</c> ——
        /// X/□,绑重等价形状,承 inputactions Emergency)。</summary>
        public static bool EmergencyPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.fKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonWest.isPressed;
        }
    }
}