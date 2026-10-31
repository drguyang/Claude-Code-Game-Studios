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
//   ──▶ `MainMenuScreen`(一张纸、一行)
//   ──▶ 玩家确认(Enter / 空格 / 手柄南键 / 点击)──▶ `MainMenuScreen.Activate()`
//   ──▶ **本类.Continue** ──▶ `BootRoot.OnMainMenuContinue`(放行启动序)
//   ──▶ World additive 装载 ──▶ 启动序完成 ──▶ 「继续最近档」读档(若有档)
//
// ⚠️ **本类不判定任何东西**:
//   不查存档目录(`hasContinueSave` 由装配层从 7a 取,`main-menu-42.md §10` 所有者纪律)、
//   不决定「继续到哪一份」(那一档在开屏前已由 `SaveService.TryGetMostRecentSave` 定下)。

namespace DaYiJingCheng.Gameplay.Boot
{
    using System;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;

    /// <summary>主菜单唯一那一行的装配层实现(42 → 装配层的单向调用)。</summary>
    public sealed class DriverMainMenuCommandSink : IMainMenuCommandSink
    {
        private readonly Action _continue;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">
        /// 回调为 null —— 行照画、按了没反应,是「静默空转」,本类拒绝在构造期接受它。</exception>
        public DriverMainMenuCommandSink(Action continueAction)
        {
            _continue = continueAction ?? throw new ArgumentNullException(nameof(continueAction));
        }

        /// <inheritdoc/>
        public void Continue() => _continue();
    }
}
