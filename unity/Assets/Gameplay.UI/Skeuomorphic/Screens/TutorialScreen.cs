// 权威来源:production/epics/skeuomorphic-ui/story-015-tutorial.md
//   · design/ux/tutorial-48.md
//   · ADR-013 §四 Key Interfaces
//
// 设计说明:
//   · 教学界面 = 纸堆翻页
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 教学界面(48)——纸堆翻页。
    /// <para>ModalId.Tutorial,手柄走查 + 目视零按键提示浮层。</para>
    /// </summary>
    public sealed class TutorialScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建教学界面。</summary>
        public TutorialScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Tutorial;

        /// <summary>构建教学界面 UI。</summary>
        private void BuildUI()
        {
            var paperStack = new VisualElement { name = "paper-stack" };
            paperStack.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(paperStack);
        }
    }
}
