// 权威来源:design/gdd/skeuomorphic-ui.md AC-42-F1②(存档位界面手柄走查)
//   · design/ux/save-slots-7b.md
//   · production/epics/skeuomorphic-ui/story-012-save-slots.md
//
// 设计说明:
//   · 存档槽位项 = 拟物元件(纸面);空槽 = 空行有格线无字(AC-42-B5)。
//   · 存档元数据归 7a/7b;42 只渲染。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using UnityEngine.UIElements;

    /// <summary>存档槽位项(AC-42-F1②)—— 逐槽渲染单元。</summary>
    public sealed class SaveSlotItem : VisualElement
    {
        /// <summary>槽位序号(1 起)。</summary>
        public int SlotIndex { get; }

        /// <summary>是否为空槽(空槽渲染格线无字)。</summary>
        public bool IsEmpty { get; set; } = true;

        /// <summary>创建存档槽位项。</summary>
        public SaveSlotItem(int slotIndex)
        {
            SlotIndex = slotIndex;
            AddToClassList(IsEmpty ? "empty-row" : "save-slot-item");
        }
    }
}
