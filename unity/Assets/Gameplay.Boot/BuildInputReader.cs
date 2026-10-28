// S20-19 · 42 建造发起方接线 —— 建造按键读取(设备级过渡最小面)
//
// 权威来源:
//   ADR-011 §一(action-based 官方路线 —— 本文件与 `PlayerInputReader` **同构**:
//     直读设备的过渡最小面;动作资产 runtime 装载归独立故事面,尚未交付)
//   input-system GDD 规则二(本批新增 `Build` / `BuildPlace` / `BuildRotate` / `BuildRemove`
//     四行动作,绑重形状见 `InputSystem_Actions.inputactions`)
//   input-system GDD 规则三(三套绑重同表共存 —— 动作资产侧由
//     `action_asset_identity_test` 断言;本文件只覆盖 K&M / Gamepad 的**设备读**)
//
// ⚠️ 语义与 `PlayerInputReader` 对齐:**直读设备、不碰动作资产** ——
//   · `Build`        = Keyboard <B>          / Gamepad <start>
//   · `BuildPlace`   = Keyboard <Enter>      / Gamepad <buttonEast>(B / ○)
//   · `BuildRotate`  = Keyboard <R>          / Gamepad <rightShoulder>(RB / R1)
//   · `BuildRemove`  = Keyboard <Delete>     / Gamepad <leftShoulder>(LB / L1)
//   · 边沿语义:调用方(驱动)逐帧比对「上帧按住位」;本类**零状态**、零分配。
//   · 无设备 ⇒ 恒 false,不抛(batch / EditMode 可靠)。
//
// ⚠️ **XR 组不在本文件的读面**:P0 无 VR 建造(technical-preferences:VR 仅急救动作)。
//   动作资产里 XR 组的绑重**存在**(规则三要求三组同表),但**无人读** ——
//   这与 `Sprint` 的 Adrift 状态同格,已在 `skeuomorphic-ui.md` §建造交互 登记。
//
// ⚠️ **键位与既有动作的冲突(如实登记,不是已解决)**:
//   · `Enter` 与 UI map 的 `Submit` 同键。建造**不是模态**(ADR-013 §十 闭集 7 员不变),
//     但**本批没有「是否有模态摊开」的聚合读面**(`IModalState` 是逐屏自述,无聚合器)
//     ⇒ 建造模式开着且同时摊开脉案 / 出诊箱时,`Enter` 会**同时**被 UI `Submit` 与
//     建造放置消费。**这是挂标,不是已修复** —— 修法 = 42 的模态聚合器落地轮
//     (`sprint-20` §十 S20-19「挂标不记绿」#9 / `skeuomorphic-ui.md` §三之二 四)。
//     ⚠️ 本批**没有**任何夹具能断「模态恒 None」:那个读面不存在。
//     `build_controller_test` 的 B-7 夹具断的是**源码面**「建造链条零 `IModalState`/`ModalId` 记号」
//     —— 它证明「建造不占模态」,**不证明**「双触发不会发生」。两者不可互替。
//   · `R` 与调试快捷键无冲突(全仓无 `rKey` 消费者,2026-10-28 实测)。

using UnityEngine;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 建造按键读取 —— 每帧把四个建造动作的**按住位**折成布尔。
    /// <para>承 <see cref="PlayerInputReader"/> 同构:直读设备、零动作资产依赖、
    /// 无设备返回「未按下」不抛。</para>
    /// <para><b>不持状态</b> —— 「上一帧是否已消费」由 <see cref="BuildInputDriver"/> 持有。</para>
    /// </summary>
    public static class BuildInputReader
    {
        /// <summary>建造模式开关(`B` / 手柄 `start`)。</summary>
        public static bool TogglePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.bKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.startButton.isPressed;
        }

        /// <summary>放置确认(`Enter` / 手柄 `buttonEast` = B / ○)。</summary>
        public static bool PlacePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.enterKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.buttonEast.isPressed;
        }

        /// <summary>幽灵朝向循环(`R` / 手柄 `rightShoulder` = RB / R1)。</summary>
        public static bool RotatePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.rightShoulder.isPressed;
        }

        /// <summary>拆除(`Delete` / 手柄 `leftShoulder` = LB / L1)。</summary>
        public static bool RemovePressed()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.deleteKey.isPressed)
                return true;
            Gamepad pad = Gamepad.current;
            return pad != null && pad.leftShoulder.isPressed;
        }
    }
}
