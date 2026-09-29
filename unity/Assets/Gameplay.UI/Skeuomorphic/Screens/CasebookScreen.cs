// 权威来源:production/epics/skeuomorphic-ui/story-011-casebook-rendering.md
//   · design/ux/casebook-39.md
//   · ADR-013 §四 Key Interfaces
//
// 设计说明:
//   · 脉案页 = 五通道区(面色/语声/呼吸/触感/病名) + 两栏(左栏体征/右栏诊断)
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 脉案页(39)——五通道区 + 两栏布局。
    /// <para>ModalId.Casebook,手柄走查 + 目视零按键提示浮层。</para>
    /// </summary>
    public sealed class CasebookScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建脉案页。</summary>
        public CasebookScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Casebook;

        /// <summary>构建脉案页 UI。</summary>
        private void BuildUI()
        {
            // 五通道区
            var channels = new VisualElement { name = "five-channels" };
            channels.Add(_library.Create(SkeuoElement.Paper)); // 面色
            channels.Add(_library.Create(SkeuoElement.Paper)); // 语声
            channels.Add(_library.Create(SkeuoElement.Paper)); // 呼吸
            channels.Add(_library.Create(SkeuoElement.Paper)); // 触感
            channels.Add(_library.Create(SkeuoElement.Paper)); // 病名
            _root.Add(channels);

            // 两栏布局
            var columns = new VisualElement { name = "two-columns" };
            var leftColumn = _library.Create(SkeuoElement.Paper);  // 体征
            var rightColumn = _library.Create(SkeuoElement.Paper); // 诊断
            columns.Add(leftColumn);
            columns.Add(rightColumn);
            _root.Add(columns);
        }
    }
}
