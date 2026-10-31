// 模态开合的输入驱动(2026-10-31 · 模态开启器轮)。
//
// 权威来源:
//   · ADR-013 §十(`ModalId` 闭集 + `IModalState`;42 是模态真源)
//   · ADR-011 §一(输入 action-based;本条取「设备直读过渡最小面」,与 `PlayerInputReader` 同纪律)
//   · design/ux/save-slots-7b.md:189(正式入口 = 暂停壳 / 主菜单,今日未成屏)
//   · design/gdd/interaction-system.md 规则七(模态期交互意图被丢弃 —— 由 4 的门读 42 的开合)
//
// ── 为什么单开一个驱动,而不并进 `InteractionInputDriver` ──────────────
// 后者把「E 查体 / F 急救」翻译成 **sim 侧的调用**(ExamineDriver / EmergencyChannelDriver);
// 本类是**纯呈现层开关**(只调 42 的 `OpenModal` / `CloseModal`,零 sim 参与)。
// 混在一起会让「查体驱动」多出一个与 sim 无关的职责,也会让它的边沿状态机
// (interactHeld / emergencyHeld)多一个无关分支。
//
// ⚠️ **边沿化在此**:`PlayerInputReader.SaveSlotsTogglePressed()` 是纯读面(按住为 true),
//    「上一帧按住」由本类持有 —— 与 `InteractionInputDriver` 同款(持键不重复触发)。

using System;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>模态开合驱动:按键沿 → 42 的 `OpenModal` / `CloseModal`。</summary>
    public sealed class ModalInputDriver
    {
        private readonly Func<bool> _togglePressed;
        private readonly ModalOpener _opener;
        private bool _held;

        /// <summary>输入读面 + 打开面(均为注入 —— 夹具可直驱,不必碰设备)。</summary>
        /// <param name="togglePressed">开合键是否按住(生产 = `PlayerInputReader.SaveSlotsTogglePressed`)。</param>
        /// <param name="opener">42 侧的开关面(生产 = `SkeuoRuntimeDriver` 的适配)。</param>
        /// <exception cref="ArgumentNullException">任一为 null(静默空转 = 按键没反应且无记录)。</exception>
        public ModalInputDriver(Func<bool> togglePressed, ModalOpener opener)
        {
            _togglePressed = togglePressed ?? throw new ArgumentNullException(nameof(togglePressed));
            _opener = opener ?? throw new ArgumentNullException(nameof(opener));
        }

        /// <summary>至今成功开 / 关的次数(观测面;夹具据此断言「一次按下 = 恰好一次动作」)。</summary>
        public int OpenCount { get; private set; }

        /// <summary>同上,关闭侧。</summary>
        public int CloseCount { get; private set; }

        /// <summary>每帧调用一次。</summary>
        public void OnFrame()
        {
            bool pressed = _togglePressed();
            bool rising = pressed && !_held;
            _held = pressed;
            if (!rising) return;

            if (_opener.IsSaveSlotsOpen)
            {
                _opener.Close();
                CloseCount++;
            }
            else
            {
                _opener.OpenSaveSlots();
                OpenCount++;
            }
        }
    }

    /// <summary>
    /// 开合面的**唯一生产实现**:把 <see cref="UI.Skeuomorphic.SkeuoRuntimeDriver"/> 的
    /// `OpenModal` / `CloseModal` 收成三项。
    /// <para>⚠️ 适配器必须住装配层:42 侧看不见 `ModalOpener` 这个 Boot 侧契约
    /// (ADR-025 §① 装配方向不可倒),而本层两件都看得见(<c>CasebookCommandRouter</c> 同型)。</para>
    /// </summary>
    public sealed class DriverModalOpener : ModalOpener
    {
        private readonly UI.Skeuomorphic.SkeuoRuntimeDriver _driver;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">driver 为 null。</exception>
        public DriverModalOpener(UI.Skeuomorphic.SkeuoRuntimeDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc/>
        public bool IsSaveSlotsOpen => _driver.CurrentModal == UI.Skeuomorphic.ModalId.SaveSlots;

        /// <inheritdoc/>
        public void OpenSaveSlots() => _driver.OpenModal(UI.Skeuomorphic.ModalId.SaveSlots);

        /// <inheritdoc/>
        public void Close() => _driver.CloseModal();
    }

    /// <summary>
    /// 开合面(42 侧 `SkeuoRuntimeDriver` 的**窄接口**)。
    /// <para>⚠️ 刻意不把整个 `SkeuoRuntimeDriver` 交给驱动:后者是 `MonoBehaviour`,
    /// 夹具里造不出来;窄接口让「按下 ⇒ 开 / 再按 ⇒ 关」这条判据不依赖引擎对象。</para>
    /// </summary>
    public interface ModalOpener
    {
        /// <summary>当前是否开着存档位模态。</summary>
        bool IsSaveSlotsOpen { get; }

        /// <summary>打开存档位模态。</summary>
        void OpenSaveSlots();

        /// <summary>关闭当前模态。</summary>
        void Close();
    }
}
