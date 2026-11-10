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

        /// <summary>
        /// 暂停壳开合键(<c>Escape</c> / 手柄 <c>startButton</c>)。
        /// <para>🔁 2026-10-31 模态开启器轮新增,原名 <c>SaveSlotsTogglePressed</c>(它是**临时入口**,
        /// 直开存档位)。同日夜 **ADR-013 §十-C Amendment C** 立暂停壳为第八员后改为**暂停壳开合键** ——
        /// 这才是 `save-slots-7b.md:189` 要的路径:Escape → 暂停壳 → 「存档位」行 → 7b。
        /// 存档位不再是独立按键的落点,而是暂停壳里的一行。</para>
        /// <para>取 Escape 是因为 <c>UI/Cancel</c> 就绑在它上面(它同时也是模态的退出键),
        /// 开合同键 = 不新增一套绑重,也不与 Interact/E、Emergency/F 冲突。</para>
        /// <para>⚠️ 与两条既有读取同款:直读设备(ADR-011 §一 过渡最小面),动作资产侧待独立 story。</para>
        /// </summary>
        public static bool PauseTogglePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.escapeKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.startButton.isPressed;
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

        /// <summary>
        /// 急救**动作选择**键(B2-b · 2026-11-10 用户裁定「按键切换」)。
        /// <para>键盘 <c>1</c>–<c>4</c> / 手柄方向键 <b>上=0 · 右=1 · 下=2 · 左=3</b>
        /// ⇒ 返回 <c>EmergencyAction</c> ordinal。没按 ⇒ <c>-1</c>。</para>
        /// <para>⚠️ 返回的是「**此刻按住**的键」,不是边沿 —— 边沿化归调用方
        /// (与 <see cref="InteractPressed"/> 同款:本类不持状态)。</para>
        /// <para>为什么选这几组键:WASD 是 1 的移动面、E/F/Escape 已各有所属
        /// (Interact / Emergency / 暂停壳),数字键与方向键是全键盘手柄两端都有的
        /// **零冲突**位置。⚠️ 这是过渡最小面(ADR-011 §一):动作资产侧的正式绑定
        /// 待独立 story,届时本方法的读面整体迁到 InputAction。</para>
        /// </summary>
        public static int EmergencyActionPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.isPressed) return 0;
                if (kb.digit2Key.isPressed) return 1;
                if (kb.digit3Key.isPressed) return 2;
                if (kb.digit4Key.isPressed) return 3;
            }

            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                if (pad.dpad.up.isPressed) return 0;
                if (pad.dpad.right.isPressed) return 1;
                if (pad.dpad.down.isPressed) return 2;
                if (pad.dpad.left.isPressed) return 3;
            }
            return -1;
        }

        /// <summary>
        /// **转诊 / 送医**键(C3-a · 2026-11-10 用户裁定「接进输入层」)。
        /// <para>键盘 <c>R</c> / 手柄 <c>buttonEast</c>(B/○)。</para>
        /// <para>为什么是 <c>R</c>:<c>Refer</c> 的首字母,且与 WASD 移动、E/F 交互、
        /// 数字键选动作、Escape 暂停壳**全部不冲突**。</para>
        /// </summary>
        public static bool ReferPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonEast.isPressed;
        }

        /// <summary>
        /// **掏出怀里的手册**(C1-c / T18 甲 · 2026-11-10)—— 手册近景的开屏键。
        /// <para>键盘 <c>M</c> / 手柄 <c>selectButton</c>(View / Back)。</para>
        /// <para>为什么是 <c>M</c>:Manual 的首字母;现占用的是 E(交互)/ F(急救)/
        /// R(转诊)/ Escape(暂停壳)/ 1–4(选动作)/ WASD(移动),<c>M</c> 与它们**零冲突**,
        /// 且手柄侧 <c>selectButton</c> 是唯一还空着的系统键(start 已归暂停壳)。</para>
        /// <para>⚠️ 本方法只给**按住**态,边沿化与「什么时候才允许开」归
        /// <see cref="PaperCloseupToggleDriver"/>(与 <see cref="PauseTogglePressed"/> 同一分工)。</para>
        /// </summary>
        public static bool ManualPressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.mKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.selectButton.isPressed;
        }

        /// <summary>
        /// **结案**键(T19 甲 · 2026-11-10)—— 把当前开着的病例收口。
        /// <para>键盘 <c>C</c> / 手柄 <c>leftShoulder</c>。</para>
        /// <para>为什么是 <c>C</c>:Close 的首字母;现占用 E(交互)/ F(急救)/ R(转诊)/
        /// M(手册)/ Escape(暂停壳)/ 1–4(选动作)/ WASD(移动),<c>C</c> 与它们零冲突。</para>
        /// <para>⚠️ 本方法只给**按住**态;边沿化归 <see cref="InteractionInputDriver"/>
        /// (与转诊键同一分工)。⚠️ 按下即「玩家自述已处置」—— 证据那半边仍由 37 的写者查流。</para>
        /// </summary>
        public static bool CloseCasePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.cKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.leftShoulder.isPressed;
        }
    }
}