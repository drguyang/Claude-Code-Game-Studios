// 权威来源:production/epics/skeuomorphic-ui/story-014-settings-shell.md
//   · design/ux/settings-shell-42.md
//   · ADR-013 §四 Key Interfaces + AC-42-G2
//
// 设计说明:
//   · 设置界面壳 = 平面拟物 UI
//   · 条目语义归 44(音频总线/混音参数),42 只提供壳与焦点
//   · 继承 PresentationRoot,实现 IModalState

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 设置界面壳(42)——条目语义归 44。
    /// <para>ModalId.SettingsShell,手柄走查 + 目视零按键提示浮层。</para>
    /// </summary>
    public sealed class SettingsScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建设置界面壳。</summary>
        public SettingsScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.SettingsShell;

        /// <summary>构建设置界面壳 UI。</summary>
        /// <remarks>
        /// <para>🔁 **2026-11-04(`TD-skeuo-11`)**:此前只往根上塞两个**裸 `Paper`** ——
        /// 无文字、无列宽、无容器 ⇒ 屏上是两条**横贯全屏的空白纸带**(图形走查
        /// `sv_02_settings.png` 实测)。本版补上壳的结构:列宽契约(`.pause-column`,
        /// 与暂停壳 / 存档册 / 炮制屏同一块变量)、页框(`.paper-page`)、标题与行形态。</para>
        /// <para>⚠️ <b>条目内容仍空,且是有意的</b>:P0 的条目集 = 44 的七总线 + 独听
        /// (`settings-shell-42.md` §5.2),总线名是「世界内译名」,归 narrative + 49 轮;
        /// 42 **只出壳与焦点,不自造条目文案**(ADR-013 §9 C3)。⇒ 本屏今日是
        /// 「有标题、有两行位置的壳」,不是「设置做完了」—— 债 `TD-skeuo-11` 的内容半边仍 open。</para>
        /// </remarks>
        private void BuildUI()
        {
            var host = new VisualElement { name = "settings-host" };
            host.AddToClassList("pause-column");   // 与其余模态同一列宽契约(宽走 var,不写字面 px)

            var page = _library.Create(SkeuoElement.Paper);
            page.name = "settings-page";
            page.AddToClassList("paper-page");     // 页框只给页级容器(行不挂 —— TD-skeuo-08 的分层)

            page.Add(new Label("调治") { name = "settings-title" });

            // 两个行位(P0 = 音量 / mono 的**位置**;文案归 44,故今日空行但形态是行)。
            var entries = new VisualElement { name = "settings-entries" };
            for (int i = 0; i < 2; i++)
            {
                var row = new VisualElement { name = "settings-entry-" + i };
                row.AddToClassList("ruled");
                entries.Add(row);
            }
            page.Add(entries);
            host.Add(page);
            _root.Add(host);
        }
    }
}
