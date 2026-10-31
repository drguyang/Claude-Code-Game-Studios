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
        /// <remarks>
        /// 🔁 2026-10-31:补 `focusable = true` —— 此前行**不可获焦**,
        /// 于是 42 的焦点桥(官方 `FocusController`,ADR-011/ADR-013)根本走不到存档位,
        /// 手柄玩家按不动任何一行。<c>tabIndex</c> 由 <c>SaveSlotsScreen</c> 按行序赋。
        /// ⚠️ 尺寸不在这里给:`min-height` 归 USS(`.empty-row` / `.save-slot-item`
        /// 同一声明块,AB-3 的 44 下限)—— 零高的元素即使可获焦也点不中。
        /// </remarks>
        public SaveSlotItem(int slotIndex)
        {
            SlotIndex = slotIndex;
            AddToClassList(IsEmpty ? "empty-row" : "save-slot-item");
            focusable = true;
        }
    }
}
