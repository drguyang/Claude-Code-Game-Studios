// 「掏出怀里的手册」—— 手册近景(`ModalId.PaperCloseup48`)的**开屏入口**(T18 甲 · 2026-11-10)
//
// 权威来源:
//   `design/gdd/reviews/technique-unlock-decision-sheet-2026-11-10.md` §3 C1(手册无开屏者)
//   ADR-013 §十-B(`PaperCloseup48` = 闭集第七员)· §9 C3(42 只渲染,永不持有游戏状态)
//   ADR-011 §一(输入:设备直读过渡最小面,与 `PlayerInputReader` 同纪律)
//   `PaperCloseupPresenter.TryPresent`(内容宿主:**先算后开**,缺页集由它算)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件补的是什么断点
// ─────────────────────────────────────────────────────────────────────────────
//   T10 交付 `PaperCloseupPresenter` 时,`TryPresent` 的**生产调用方为零** ——
//   `grep -rn "TryPresent" unity/Assets --include=*.cs | grep -v /Tests/` 只命中定义与注释。
//   ⇒ 手册近景、A4 的七张纸、`TutorialPaperMissing` 算出来的缺页集 ——
//     整套东西在游戏里**一次都打不开**。缺页机制「齐全但不触发」。
//
//   本件把它接上:`M`(手柄 `selectButton`)→ 开屏并喂数据;**再按一次 / 按 ESC 关**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条判据(每条都对应一种「看起来对、其实错」的写法)
// ─────────────────────────────────────────────────────────────────────────────
//   ① **只在无模态时开**:别的模态摊着时按 M **什么都不做**。
//      与 ESC 不同 —— ESC 的规则是「有模态就关」(TD-skeuo-03 结案轮收敛的那条),
//      而 M 是**打开某一样东西**的键;让它「关掉别人再开自己」= 玩家按一次 M
//      会丢掉他正看着的屏,而且是在他没要求的情况下。
//   ② **手册开着时 M = 关**:同一根手指按第二次应当收回去,不该再开一次(见 ③)。
//   ③ **只有上升沿算一次**:与 `ModalInputDriver` / `InteractionInputDriver` 同款 ——
//      本类持「上一帧按住」,按住十帧只开一次。
//
// ⚠️ 开屏被拒(`TryPresent` 返回 false,如更高优先级模态抢先)**不计入 OpenCount** ——
//    计数是「真的开了几次」,不是「按了几次」。
//
// ⚠️ 本类**不持任何游戏状态**:只知道「该不该开」,开什么内容归 `PaperCloseupPresenter`
//    (先算后开:玩家 id 未绑定时 `Build()` 会抛,那时屏还没摊开)。

using System;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>手册近景的开合驱动(`M` / 手柄 `select`)—— 边沿化 + 只认「无模态时开」。</summary>
    public sealed class PaperCloseupToggleDriver
    {
        private readonly Func<bool> _pressed;
        private readonly Func<ModalId> _currentModal;
        private readonly Func<bool> _tryPresent;
        private readonly Action _close;
        private bool _held;

        /// <summary>构造(四个面全部注入 —— 夹具可直驱,不必碰设备与 UI 树)。</summary>
        /// <param name="pressed">开合键是否**按住**(生产 = `PlayerInputReader.ManualPressed`)。</param>
        /// <param name="currentModal">42 侧的当前模态(生产 = `SkeuoRuntimeDriver.CurrentModal`)。</param>
        /// <param name="tryPresent">开屏并喂数据(生产 = `() => PaperCloseups?.TryPresent(ui) ?? false`)。
        /// 返回 <c>false</c> = 开屏被拒。</param>
        /// <param name="close">关屏(生产 = `SkeuoRuntimeDriver.CloseModal`)。</param>
        /// <exception cref="ArgumentNullException">任一面为 null(静默空转 = 按键没反应且无记录)。</exception>
        public PaperCloseupToggleDriver(Func<bool> pressed, Func<ModalId> currentModal,
                                        Func<bool> tryPresent, Action close)
        {
            _pressed = pressed ?? throw new ArgumentNullException(nameof(pressed));
            _currentModal = currentModal ?? throw new ArgumentNullException(nameof(currentModal));
            _tryPresent = tryPresent ?? throw new ArgumentNullException(nameof(tryPresent));
            _close = close ?? throw new ArgumentNullException(nameof(close));
        }

        /// <summary>至今**真的开出来**的次数(观测面)。</summary>
        public int OpenCount { get; private set; }

        /// <summary>同上,关闭侧。</summary>
        public int CloseCount { get; private set; }

        /// <summary>每帧调用一次(与 `ModalInputDriver.OnFrame` 同一节奏)。</summary>
        public void OnFrame()
        {
            bool pressed = _pressed();
            bool rising = pressed && !_held;
            _held = pressed;
            if (!rising) return;

            ModalId current = _currentModal();

            // ② 手册开着 ⇒ 收回去。
            if (current == ModalId.PaperCloseup48)
            {
                _close();
                CloseCount++;
                return;
            }

            // ① 别的模态摊着 ⇒ 不抢(见文件头判据①)。
            if (current != ModalId.None) return;

            if (_tryPresent()) OpenCount++;
        }
    }
}
