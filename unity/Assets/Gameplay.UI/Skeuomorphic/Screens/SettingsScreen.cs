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
        private void BuildUI()
        {
            var entries = new VisualElement { name = "settings-entries" };
            entries.Add(_library.Create(SkeuoElement.Paper)); // 音量
            entries.Add(_library.Create(SkeuoElement.Paper)); // mono
            _root.Add(entries);
        }
    }
}
