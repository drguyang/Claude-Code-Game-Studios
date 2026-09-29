// 权威来源:production/epics/skeuomorphic-ui/story-012-save-slots.md
//   · design/ux/save-slots-7b.md
//   · ADR-013 §四 Key Interfaces
//
// 设计说明:
//   · 存档位界面 = 平面拟物 UI
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 存档位界面(7b)——平面拟物 UI。
    /// <para>ModalId.SaveSlots,手柄走查。</para>
    /// </summary>
    public sealed class SaveSlotsScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建存档位界面。</summary>
        public SaveSlotsScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.SaveSlots;

        /// <summary>构建存档位界面 UI。</summary>
        private void BuildUI()
        {
            var slotList = new VisualElement { name = "slot-list" };
            slotList.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(slotList);
        }
    }
}
