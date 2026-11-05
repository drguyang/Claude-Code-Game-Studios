// 暂停壳的意图出口实现(2026-10-31 · ADR-013 §十-C Amendment C)。
//
// 权威来源:
//   · `design/ux/save-slots-7b.md:189`(正式入口 = 暂停壳 / 主菜单「继续诊治」)
//   · ADR-013 §十-C(**Pause = 闭集第八员**)+ §9 C3(42 只渲染,永不持有游戏状态)
//   · ADR-025 §①(装配方向不可倒:42 看不见 Boot,故本实现必须住装配层)
//
// ── 本类在一串什么里 ──────────────────────────────────────────────
// Escape ──▶ `ModalInputDriver` ──▶ `DriverModalOpener.OpenPause`
//        ──▶ `SkeuoRuntimeDriver.OpenModal(ModalId.Pause)` ──▶ `PauseScreen`
//        ──▶ 行「存档位」 ──▶ **本类.OpenSaveSlots** ──▶ `OpenModal(ModalId.SaveSlots)`
//        ──▶ 存档位行 ──▶ `SaveSlotCommandRouter.TryLoad` ──▶ `LoadSequencer.Load`
//
// 🔁 **2026-11-04 第三行「调治」**(用户裁定):`OpenSettings` = 换屏到 `ModalId.SettingsShell` ——
//    该屏此前**生产上零开屏点**(`TD-skeuo-04`),本行是它的唯一入口。
//
// 🔁 **2026-11-05 第四行「退出游戏」**(用户裁定):`Quit` = 装配层注入的 `Application.Quit()`,
//    与主菜单第四行**逐字同构**;退出保存走既有的 `Application.wantsToQuit` 钩子,本类不写盘。
//
// ⚠️ **四个动作都不判定任何游戏状态**:
//   `Resume` 只合上暂停壳(世界冻结/解冻归 `BootRoot` 的帧泵闸,不归本类);
//   `OpenSaveSlots` 只是换屏,不读存档、不碰 `SaveService`;
//   `Quit` 只把意图转给装配层注入的委托(null ⇒ 留空)。
//   ⚠️ 上一版此处写「今日世界侧在暂停期本就没有冻结机制」—— 那句随 2026-11-01
//   ADR-013 §十-C Amendment D(挡路模态摊开 ⇒ 真停 sim tick)已过期,原文留档。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;

    /// <summary>暂停壳四行的装配层实现(42 → 装配层的单向调用)。</summary>
    public sealed class DriverPauseCommandSink : IPauseCommandSink
    {
        private readonly SkeuoRuntimeDriver _driver;
        private readonly Action _quit;
        private readonly Func<bool> _requestSave;

        /// <summary>构造。</summary>
        /// <param name="driver">拟物 UI 驱动(非 null)。</param>
        /// <param name="quit">
        /// 退出动作(2026-11-05 第四行)。**可 null** —— null ⇒ 行照画、按了留空
        /// (与其它「未接线的诚实形态」同纪律:不造假动作、不崩)。
        /// <para>⚠️ 退出是**破坏性**动作,本类不自己调 `Application.Quit()`:
        /// `UnityEngine` 依赖留在装配层,42 侧程序集面不变(ADR-025 §①)。</para>
        /// </param>
        /// <exception cref="ArgumentNullException">driver 为 null(静默空转 = 行按了没反应)。</exception>
        /// <param name="requestSave">
        /// 一键落笔(2026-11-05 第二行「保存」)。**可 null** —— null ⇒ 恒返回 <c>false</c>,
        /// 屏上如实显示「未能落笔」而不是假装成功。
        /// </param>
        public DriverPauseCommandSink(SkeuoRuntimeDriver driver, Action quit = null,
                                      Func<bool> requestSave = null)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _quit = quit;
            _requestSave = requestSave;
        }

        /// <inheritdoc/>
        public void Resume() => _driver.CloseModal();

        /// <inheritdoc/>
        public void OpenSaveSlots() => _driver.OpenModal(ModalId.SaveSlots);

        /// <inheritdoc/>
        public void OpenSettings() => _driver.OpenModal(ModalId.SettingsShell);

        /// <inheritdoc/>
        public bool RequestSave() => _requestSave != null && _requestSave();

        /// <inheritdoc/>
        public void Quit() => _quit?.Invoke();
    }
}
