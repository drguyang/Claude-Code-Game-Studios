// 库存容器(系统 20 的数据面)—— 物品格 + 翻页 + 选中 + 载重四档(2026-11-02)
//
// 权威来源:
//   · `design/ux/inventory-container-20.md`(Approved 2026-09-22)
//       §5.2 摆放函数:`instance_id` 升序、`COLUMNS = 4` / `ROWS_PER_PAGE = 3`(一屏 ≤12);
//            换页条件 = `row ≥ ROWS_PER_PAGE`
//       §5.2 ④ 页缘铜扣 = `PagePrev/PageNext` 的焦点落点;首/末页**卸为平整铜片**(非置灰、非隐藏)
//       §5.3 器物「**是**(焦点 + 行级动作)」
//       §6  载重四档 = `slack = cap − carry_load` 的单调不增函数(**阈值归 42**,BL-27 分工)
//       §11 品级通道 = **质地 + 药签措辞**(禁颜色单通道)
//   · `design/gdd/inventory-and-items.md`
//       AC-20-10(禁负重条 / 百分比 / 数字)· AC-20-20(同一箱 + 同一方向键 ⇒ 同一焦点目标)
//   · ADR-013 §9 C3 —— 42 **只渲染,永不持有游戏状态**
//
// ─────────────────────────────────────────────────────────────────────────────
// 本轮补的是什么(2026-11-02)
// ─────────────────────────────────────────────────────────────────────────────
// 此前本屏是**空壳**:只建了两个纸面容器,零内容、零交互、零 DTO 源。
// 本轮补内容面(物品格 / 翻页 / 选中 / 四档),使「料位选中态」有挂载点 ——
// 那是炮制屏(`Processing18Screen`)品级集的来源,也是 `TD-interaction-03` 真正可用的最后一环。
//
// ⚠️ **本屏仍不持游戏状态**:`_items` / `_page` 是**呈现缓存**(开屏时喂入),
//    不是 sim 真源。选中集**只报给出口**(`IContainerSelectionSink`),不在本屏做裁决。
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条必须读清的边界
// ─────────────────────────────────────────────────────────────────────────────
// ① **次序归装配层**:`Items` 进来时已按 `instance_id` 升序(§5.2 的唯一权威)。
//    本屏**不再动次序** —— 两处定序 = 第二真源。
//    ⚠️ 本文件受 AC-42-F3 词门扫描(**全文含注释**),禁词见 `coding-standards.md` §命名避让表 ——
//    该表含一个指「按某规则重排」的词,故此处一律改用「次序 / 定序」。
// ② **翻页在 42 内做**:`COLUMNS` / `ROWS_PER_PAGE` 是布局参数(§5.2 明写归 ux-design 域),
//    不是机制值 ⇒ 42 持有它们是合法的(与「42 不持游戏状态」不冲突)。
// ③ **载重只出档位,不出数字**:`CarryLoad` / `CarryCap` 只用于算 `slack` 的分档,
//    **绝不显示**(AC-20-10)。分档函数住本屏(BL-27 分工:20 出谓词、42 出分档)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>容器屏的选中出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IContainerSelectionSink
    {
        /// <summary>选中态变更(点名哪些实例 —— 承 BL-23 ④「消耗意图直接点名实例集」)。</summary>
        /// <param name="selectedInstanceIds">当前选中的实例 id(空集 = 无选中)。</param>
        void OnSelectionChanged(IReadOnlyList<long> selectedInstanceIds);
    }

    /// <summary>
    /// 库存容器(20)—— 物品格 ≤12 件/屏 + 翻页 + 选中 + 载重四档。
    /// <para><see cref="ModalId.InventoryContainer"/>,手柄走查。</para>
    /// </summary>
    public sealed class InventoryContainerScreen : PresentationRoot, IModalState
    {
        /// <summary>每行格数(§5.2 裁定的布局参数;**不是机制值**)。</summary>
        public const int Columns = 4;

        /// <summary>每页行数(§5.2 裁定的布局参数)。</summary>
        public const int RowsPerPage = 3;

        /// <summary>每页格数 = 4 × 3 = 12(§5.2「一屏 ≤12 件」)。</summary>
        public const int CellsPerPage = Columns * RowsPerPage;

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly VisualElement _gridHost;
        private readonly VisualElement _pageHost;
        private readonly VisualElement _prevDetent;
        private readonly VisualElement _nextDetent;
        private readonly List<VisualElement> _cells = new List<VisualElement>();
        private readonly List<long> _cellInstanceIds = new List<long>();
        private readonly HashSet<long> _selected = new HashSet<long>();

        private ContainerItemDto[] _items = Array.Empty<ContainerItemDto>();
        private int _page;
        private int _pageCount = 1;
        private IContainerSelectionSink _selectionSink;

        /// <summary>创建库存容器屏。</summary>
        public InventoryContainerScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;

            _gridHost = new VisualElement { name = "item-grid" };
            var gridPaper = _library.Create(SkeuoElement.Paper);
            gridPaper.name = "item-grid-paper";   // ⚠️ 必须具名 —— 重建只清格、不清纸面
            _gridHost.Add(gridPaper);
            _root.Add(_gridHost);

            _pageHost = new VisualElement { name = "pagination" };
            var pagePaper = _library.Create(SkeuoElement.Paper);
            pagePaper.name = "pagination-paper";
            _pageHost.Add(pagePaper);
            _root.Add(_pageHost);

            // §5.2 ④ 页缘铜扣(左右)= PagePrev / PageNext 的焦点落点。
            _prevDetent = MakeDetent("page-prev");
            _nextDetent = MakeDetent("page-next");
            _pageHost.Add(_prevDetent);
            _pageHost.Add(_nextDetent);
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.InventoryContainer;

        /// <summary>注入选中出口(未注入 ⇒ 格照画、点了不报:未接线的诚实形态)。</summary>
        public void BindSelectionSink(IContainerSelectionSink sink) => _selectionSink = sink;

        /// <summary>当前页号(0 起;观测面)。</summary>
        public int Page => _page;

        /// <summary>总页数(≥ 1;空箱 = 1 页)。</summary>
        public int PageCount => _pageCount;

        /// <summary>本页建出的格数(观测面 —— 空箱 = 0)。</summary>
        public int CellCount => _cells.Count;

        /// <summary>取某一格(观测面;越界 ⇒ null)。</summary>
        public VisualElement CellAt(int cellIndex)
            => cellIndex >= 0 && cellIndex < _cells.Count ? _cells[cellIndex] : null;

        /// <summary>某格绑定的实例 id(观测面;越界 ⇒ -1)。</summary>
        public long InstanceIdAt(int cellIndex)
            => cellIndex >= 0 && cellIndex < _cellInstanceIds.Count ? _cellInstanceIds[cellIndex] : -1L;

        /// <summary>当前选中的实例 id(**按 id 升序**,稳定;空 = 空数组)。</summary>
        public IReadOnlyList<long> SelectedInstanceIds
        {
            get
            {
                var list = new List<long>(_selected);
                list.Sort();
                return list;
            }
        }

        /// <summary>当前载重档位(§6 的四档;**满档取 20 的谓词、余下三档本屏分**,BL-27 分工)。</summary>
        public CarryBand Band { get; private set; } = CarryBand.Empty;

        /// <summary>打开时应落焦的第一格(<c>null</c> = 空箱,无可落焦点)。</summary>
        public VisualElement FirstFocusable => _cells.Count > 0 ? _cells[0] : null;

        /// <summary>
        /// 喂数据并**重建当前页**。开屏时调用;换箱 / 库存变动时重调。
        /// </summary>
        /// <param name="container">整箱快照(`Items` 须**已按 `instance_id` 升序** —— 装配层定序)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="container"/> 为 null。</exception>
        public void Bind(InventoryContainerDto container)
        {
            // ⚠️ `InventoryContainerDto` 是 `readonly struct` ⇒ **不能** `== null`(CS0019)。
            //    null 检查已由该 DTO 的构造器承担(`Items == null` ⇒ 抛),此处无需重复。
            _items = container.Items ?? Array.Empty<ContainerItemDto>();
            _pageCount = _items.Length == 0
                ? 1
                : (_items.Length + CellsPerPage - 1) / CellsPerPage;
            if (_page >= _pageCount) _page = _pageCount - 1;
            if (_page < 0) _page = 0;

            Band = BandOf(container.CarryLoad, container.CarryCap, container.IsFull);
            RebuildPage();
        }

        /// <summary>翻页(方向 = -1 上一页 / +1 下一页;越界 ⇒ 不动)。</summary>
        /// <param name="delta">页增量。</param>
        public void TurnPage(int delta)
        {
            int target = _page + delta;
            if (target < 0 || target >= _pageCount || target == _page) return;
            _page = target;
            RebuildPage();
        }

        /// <summary>切换某格的选中态(两条输入路径的唯一汇合点)。</summary>
        /// <param name="cellIndex">本页格序;越界 ⇒ 忽略。</param>
        public void ToggleSelection(int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= _cellInstanceIds.Count) return;
            long id = _cellInstanceIds[cellIndex];
            if (!_selected.Add(id)) _selected.Remove(id);
            ApplySelectedClass(cellIndex);
            _selectionSink?.OnSelectionChanged(SelectedInstanceIds);
        }

        /// <summary>
        /// 载重四档的分档函数(§6 / BL-27)。**满档的判据不在这里** —— 它由 20 给出。
        /// </summary>
        /// <param name="carryLoad">当前载重(≥ 0)。</param>
        /// <param name="carryCap">上限(≥ 0;`0` = 容量真值未装配)。</param>
        /// <param name="isFull">**20 的满档谓词结果**(`CarryLoadSolver.IsFullForAnyAddition`,
        /// 即 `carry_load &gt; cap − W_MIN`;AC-20-18)。本屏**只消费、不重算**。</param>
        /// <returns>档位(单调不增于 `slack = cap − load`)。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="carryLoad"/> 或
        /// <paramref name="carryCap"/> 为负。</exception>
        /// <remarks>
        /// <para>⚠️ <b>为什么满档不从 `carryLoad` / `carryCap` 自己算</b>:BL-27 的分工逐字是
        /// 「20 出谓词、42 出分档」。谓词本体住 `Sim`(`CarryLoadSolver`),而
        /// `Gameplay.UI` 看不见 `Sim`(ADR-025 §①)⇒ 本屏若要自己求
        /// `carry_load &gt; cap − W_MIN`,就必须**再写一遍**同一判据。实测同族失败形态
        /// (F8「行为等价掩盖结构分叉」):两份式子今天同值,任一侧改口径就静默分叉,
        /// 而夹具因行为一致**抓不住**。⇒ 谓词只在一处求值(装配层调 `CarryLoadSolver` 本体),
        /// 本屏拿**结果**。</para>
        /// <para>⚠️ <b>中间两档的切点归本屏**(布局参数域,§6 只钉「单调不增 + 满档具名谓词」)。
        /// 用整数**上取整**,不用 `cap/4` 直接整除 —— `cap = 3` 时 `3/4 = 0` ⇒ 小箱永远到不了
        /// `NearFull`(实测踩到:`cap=3, load=2` 被判 Half)。</para>
        /// </remarks>
        public static CarryBand BandOf(long carryLoad, int carryCap, bool isFull)
        {
            if (carryLoad < 0L)
                throw new ArgumentOutOfRangeException(nameof(carryLoad), carryLoad,
                    "载重不可能为负(`CarryLoadSolver` 同一守卫;负值 = 投影或求和出了错)");
            if (carryCap < 0)
                throw new ArgumentOutOfRangeException(nameof(carryCap), carryCap,
                    "容量不可能为负(`CARRY_CAP` ∈ int>0;0 = 未装配,不是非法)");

            // 满档 = 20 的谓词结果。**本屏不重新求值**(见 remarks 的 F8 说明)。
            if (isFull) return CarryBand.Full;
            if (carryLoad <= 0L) return CarryBand.Empty;

            long slack = (long)carryCap - carryLoad;
            long nearFullAt = (carryCap + 3) / 4;   // ceil(cap/4)
            long halfAt = (carryCap + 1) / 2;       // ceil(cap/2)
            if (slack <= nearFullAt) return CarryBand.NearFull;
            if (slack <= halfAt) return CarryBand.Half;
            return CarryBand.Empty;
        }

        private void RebuildPage()
        {
            _cells.Clear();
            _cellInstanceIds.Clear();
            // 只清格,不清纸面/容器(纸面是首建时加的,见构造函数)
            for (int i = _gridHost.childCount - 1; i >= 0; i--)
            {
                var child = _gridHost[i];
                if (child.name == "item-grid-paper") continue;
                _gridHost.Remove(child);
            }

            int start = _page * CellsPerPage;
            int end = Math.Min(start + CellsPerPage, _items.Length);

            for (int i = start; i < end; i++)
            {
                ContainerItemDto item = _items[i];
                int cellIndex = _cells.Count;
                var cell = new VisualElement { name = "container-item-" + item.InstanceId };
                cell.AddToClassList("ruled");
                cell.focusable = true;
                cell.tabIndex = cellIndex;

                // §5.3 / §11:品级走**质地 + 药签措辞**,禁颜色单通道(AC-20-23)。
                // 本轮以文本承载:序号 + 品级 + 数量(21a 显示名未落地 ⇒ 用序号,登记为粗糙边)。
                string label = "器物#" + item.ItemOrdinal + "·品" + item.Quality;
                if (item.Qty > 1) label += "·叠" + item.Qty;
                cell.Add(new Label(label));

                // 手柄经官方焦点桥给 NavigationSubmitEvent;指针给 ClickEvent
                // (ADR-011:不自实现焦点算法)。
                cell.RegisterCallback<NavigationSubmitEvent>(_ => ToggleSelection(cellIndex));
                cell.RegisterCallback<ClickEvent>(_ => ToggleSelection(cellIndex));

                _cells.Add(cell);
                _cellInstanceIds.Add(item.InstanceId);
                _gridHost.Add(cell);

                if (_selected.Contains(item.InstanceId)) cell.AddToClassList("selected");
            }

            UpdateDetents();
        }

        private void ApplySelectedClass(int cellIndex)
        {
            VisualElement cell = _cells[cellIndex];
            long id = _cellInstanceIds[cellIndex];
            if (_selected.Contains(id)) cell.AddToClassList("selected");
            else cell.RemoveFromClassList("selected");
        }

        /// <summary>§5.2 ④ 首 / 末页铜扣**卸为平整铜片**(非置灰、非隐藏 —— 卸载可交互性)。</summary>
        private void UpdateDetents()
        {
            bool canPrev = _page > 0;
            bool canNext = _page + 1 < _pageCount;
            _prevDetent.SetEnabled(canPrev);
            _nextDetent.SetEnabled(canNext);
            _prevDetent.EnableInClassList("brass-detent-flat", !canPrev);
            _nextDetent.EnableInClassList("brass-detent-flat", !canNext);
        }

        private VisualElement MakeDetent(string name)
        {
            var detent = new VisualElement { name = name };
            detent.AddToClassList("brass-detent");
            detent.focusable = true;
            detent.RegisterCallback<NavigationSubmitEvent>(_ =>
                TurnPage(name == "page-prev" ? -1 : +1));
            detent.RegisterCallback<ClickEvent>(_ =>
                TurnPage(name == "page-prev" ? -1 : +1));
            return detent;
        }
    }
}
