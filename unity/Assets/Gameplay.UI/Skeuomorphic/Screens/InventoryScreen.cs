// 权威来源:production/epics/skeuomorphic-ui/story-013-inventory-container.md
//   · design/ux/inventory-container-20.md
//   · ADR-013 §四 Key Interfaces
//
// 设计说明:
//   · 库存容器 = 翻页制(≤12 件/屏)
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 库存容器(20)——翻页制 ≤12 件/屏。
    /// <para>ModalId.InventoryContainer,手柄走查。</para>
    /// </summary>
    public sealed class InventoryScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建库存容器。</summary>
        public InventoryScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.InventoryContainer;

        /// <summary>构建库存容器 UI。</summary>
        private void BuildUI()
        {
            var itemGrid = new VisualElement { name = "item-grid" };
            itemGrid.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(itemGrid);

            var pagination = new VisualElement { name = "pagination" };
            pagination.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(pagination);
        }
    }
}
