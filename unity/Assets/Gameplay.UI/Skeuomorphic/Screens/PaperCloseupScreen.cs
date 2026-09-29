// 权威来源:production/epics/skeuomorphic-ui/story-016-paper-closeup.md
//   · design/ux/paper-closeup-48.md
//   · ADR-013 §四 Key Interfaces
//
// 设计说明:
//   · 教学纸近景 = 世界内单张纸近景(走近摊纸)
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 教学纸近景(48)——世界内单张纸近景。
    /// <para>ModalId.PaperCloseup48,走近摊纸触发。</para>
    /// </summary>
    public sealed class PaperCloseupScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建教学纸近景。</summary>
        public PaperCloseupScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.PaperCloseup48;

        /// <summary>构建教学纸近景 UI。</summary>
        private void BuildUI()
        {
            var paperContent = new VisualElement { name = "paper-content" };
            paperContent.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(paperContent);
        }
    }
}
