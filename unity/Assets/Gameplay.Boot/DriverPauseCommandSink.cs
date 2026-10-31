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
// ⚠️ **两个动作都不判定任何游戏状态**:
//   `Resume` 只合上暂停壳(「继续」要不要恢复 tick / 解冻世界,归世界侧;
//   今日世界侧在暂停期本就没有冻结机制 —— 见 Amendment C 的「未做」段);
//   `OpenSaveSlots` 只是换屏,不读存档、不碰 `SaveService`。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;

    /// <summary>暂停壳两行的装配层实现(42 → 装配层的单向调用)。</summary>
    public sealed class DriverPauseCommandSink : IPauseCommandSink
    {
        private readonly SkeuoRuntimeDriver _driver;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">driver 为 null(静默空转 = 行按了没反应)。</exception>
        public DriverPauseCommandSink(SkeuoRuntimeDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <inheritdoc/>
        public void Resume() => _driver.CloseModal();

        /// <inheritdoc/>
        public void OpenSaveSlots() => _driver.OpenModal(ModalId.SaveSlots);
    }
}
