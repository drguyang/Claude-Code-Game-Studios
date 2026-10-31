// 权威来源:production/epics/skeuomorphic-ui/story-012-save-slots.md
//   · design/ux/save-slots-7b.md(§交互表:「载入 = 无 SimEvent · `ISaveService.Load(SaveSlot)`」)
//   · design/gdd/save-slot-ui.md(7b —— 槽位语义 · 规则三「读档即锁」)
//   · ADR-013 §四 Key Interfaces · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)
//
// 设计说明:
//   · 存档位界面 = 平面拟物 UI
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件
//
// 🔁 **2026-10-31 载入接线轮**:本屏此前是**空壳**(只建一个纸面容器,零槽位行、零动作)。
//   `BootRoot.RequestLoad` 因此「通电待信」—— 有方法、零调用方。本轮补到:
//   ① `SetSlots` 逐槽建行;② 行的**确认**动作经 `ISaveSlotCommandSink` 交给装配层
//   (42 **不**判定能不能读、**不**碰 `SaveService`)。
//   ⚠️ **仍未做的**:行内文字(存档时刻 / 医师名 / 空槽格线)与「读档即锁」的呈现形
//   (`save-slots-7b.md:114` 的折角双叠)—— 那些要有 7a 的槽位元数据源才画得出,
//   本轮只做到「行在、能按、按下有去向」。**不得**据此判 UI 走查通过。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System.Collections.Generic;
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
        private readonly VisualElement _slotHost;
        private readonly List<SaveSlotItem> _rows = new List<SaveSlotItem>();
        private readonly List<uint> _slots = new List<uint>();
        private ISaveSlotCommandSink _sink;

        /// <summary>创建存档位界面。</summary>
        public SaveSlotsScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            _slotHost = new VisualElement { name = "slot-list" };
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.SaveSlots;

        /// <summary>当前画出的槽位行数(观测面;0 = 尚未收到槽位清单)。</summary>
        public int SlotRowCount => _rows.Count;

        /// <summary>取某一行(观测面;越界 ⇒ <c>null</c>)。</summary>
        public SaveSlotItem RowAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rows.Count ? _rows[rowIndex] : null;

        /// <summary>构建存档位界面 UI。</summary>
        private void BuildUI()
        {
            _slotHost.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(_slotHost);
        }

        /// <summary>
        /// 注入载入意图出口(实现方住装配层;42 不反向引用装配层)。
        /// <para>⚠️ 未注入 ⇒ 行照常画、确认动作**留空**(不崩、不造动作)—— 与
        /// <c>CasebookScreen.BindCommandSink</c> 同纪律:缺写通道是未接线的诚实形态。</para>
        /// </summary>
        public void BindCommandSink(ISaveSlotCommandSink sink) => _sink = sink;

        /// <summary>
        /// 按槽位清单重建行(清单由装配层给 —— 42 不自己发明槽位数)。
        /// </summary>
        /// <param name="slotSeqs">槽位序号清单。null / 空 ⇒ 清空全部行。</param>
        public void SetSlots(IReadOnlyList<uint> slotSeqs)
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].RemoveFromHierarchy();
            _rows.Clear();
            _slots.Clear();

            if (slotSeqs == null) return;

            var host = _slotHost;
            for (int i = 0; i < slotSeqs.Count; i++)
            {
                uint slotSeq = slotSeqs[i];
                var row = new SaveSlotItem((int)slotSeq);
                int rowIndex = _rows.Count;
                // 两条输入路径都落到同一个 `Activate` —— 手柄经官方焦点桥给
                // `NavigationSubmitEvent`(ADR-011:不自实现焦点算法),指针给 `ClickEvent`。
                row.RegisterCallback<NavigationSubmitEvent>(_ => Activate(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => Activate(rowIndex));
                // 焦点顺序 = 行序(AC-7b-04:`slot_seq` 升序,不可重排)。
                // ⚠️ 不自己实现焦点算法 —— tabIndex + focusable 交给官方 `FocusController`
                //    (ADR-013「不自实现焦点算法」);本屏只定**顺序**。
                row.tabIndex = rowIndex;
                _rows.Add(row);
                _slots.Add(slotSeq);
                host.Add(row);
            }
        }

        /// <summary>
        /// 打开时应落焦的第一行(<c>null</c> = 没有行)。
        /// <para>由装配方(42 驱动)在开屏后请求聚焦 —— 游戏没有指针,「开了屏但焦点悬空」
        /// 等于手柄玩家一步也走不了。</para>
        /// </summary>
        public VisualElement FirstFocusable => _rows.Count > 0 ? _rows[0] : null;

        /// <summary>
        /// 确认某一行(两条输入路径的唯一汇合点)。
        /// <para>只**转达意图**;能不能读、读完什么状态,全归装配层与 7a。</para>
        /// </summary>
        /// <param name="rowIndex">行序号(0 起)。越界 ⇒ 忽略(不抛:越界来自输入竞态,不是编程错误)。</param>
        public void Activate(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _slots.Count) return;
            _sink?.TryLoad(_slots[rowIndex]);
        }
    }
}
