// 主菜单的意图出口实现(2026-11-01 · `design/ux/main-menu-42.md`)。
//
// 权威来源:
//   · `design/ux/main-menu-42.md §7`(三条输入路径 —— 键盘 Submit / 手柄南键 / 指针 Click
//     —— 必须汇到**同一个** `Activate()`;各写一份判定 = 将来必然分叉)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)+ ADR-025 §①(42 看不见 Boot)
//   · `save-slots-7b.md:189`(「继续诊治」= 存档位的两条正式入口之一)
//
// ── 本类在一串什么里 ──────────────────────────────────────────────
// 启动序(World 装载之前)──▶ `BootRoot.PresentMainMenuAsync`
//   ──▶ `SkeuoRuntimeDriver.ShowSceneLevelScreen("MainMenu", hasSave)`
//   ──▶ `MainMenuScreen`(一张纸,**固定四行**:继续 / 新起 / 设置 / 退出)
//   ──▶ 玩家确认 ──▶ `MainMenuScreen.ActivateRow(i)` ──▶ **本类**
//   ──▶ 继续 / 新起 ⇒ `BootRoot.OnMainMenuContinue`(放行启动序)
//   ──▶ 设置 ⇒ 装配层开 `ModalId.SettingsShell`(42 不自己换屏)
//
// ⚠️ **本类不判定任何东西**:
//   不查存档目录(`hasContinueSave` 由装配层从 7a 取,`main-menu-42.md §10` 所有者纪律)、
//   不决定「继续到哪一份」(那一档在开屏前已由 `SaveService.TryGetMostRecentSave` 定下)。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;

    /// <summary>主菜单四行的装配层实现(42 → 装配层的单向调用)。</summary>
    public sealed class DriverMainMenuCommandSink : IMainMenuCommandSink
    {
        private readonly Action _continue;
        private readonly Action _newWorld;
        private readonly Action _openSettings;
        private readonly Action _quit;

        /// <summary>构造。</summary>
        /// <param name="continueAction">「继续诊治」(= 最近游玩的世界)。</param>
        /// <param name="newWorld">「新起一局」(<c>null</c> ⇒ 行照画、按了留空)。</param>
        /// <param name="openSettings">「设置」(<c>null</c> ⇒ 同上)。</param>
        /// <param name="quit">「退出游戏」(<c>null</c> ⇒ 同上)。</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="continueAction"/> 为 null —— 行照画、按了没反应,是「静默空转」,
        /// 本类拒绝在构造期接受它。</exception>
        public DriverMainMenuCommandSink(Action continueAction,
                                         Action newWorld = null,
                                         Action openSettings = null,
                                         Action quit = null)
        {
            _continue = continueAction ?? throw new ArgumentNullException(nameof(continueAction));
            _newWorld = newWorld;
            _openSettings = openSettings;
            _quit = quit;
        }

        /// <inheritdoc/>
        public void Continue() => _continue();

        /// <inheritdoc/>
        public void NewWorld() => _newWorld?.Invoke();

        /// <inheritdoc/>
        public void OpenSettings() => _openSettings?.Invoke();

        /// <inheritdoc/>
        public void Quit() => _quit?.Invoke();
    }
}
