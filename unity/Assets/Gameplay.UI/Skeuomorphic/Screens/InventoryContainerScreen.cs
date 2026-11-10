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
    using UnityEngine;
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
        // 🔁 2026-11-10(T22 批二):载重四档的**几何**载体(§6:全走几何堆高,禁条/禁色梯度)
        private readonly VisualElement _rim;    // 沿口:近满起可见
        private readonly VisualElement _lid;    // 半盖:满档才出现(合盖受阻)
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

            // 🔁 **2026-11-10(T22 批二)补壳**:此前把格区 / 翻页两块纸面**直接挂在面板根上**
            // ⇒ 屏上是两条**横贯全屏的空白纸带**(真包 20:42 实测 —— 与 `TD-skeuo-11`
            // 记录的设置壳同型)。现按其余模态屏的同一块契约收口:
            // 列宽(`.pause-column`)+ 页框(`.paper-page`)+ 标题,内容住页里。
            // ⚠️ `.paper-page` 的页框**只给页级容器**(ADR-013 §十-G Amendment F)——
            //    内层 `item-grid-paper` 仍是 `.paper`(无框底纹),不然每行套一圈框。
            var host = new VisualElement { name = "inventory-host" };
            host.AddToClassList("pause-column");

            var page = _library.Create(SkeuoElement.Paper);
            page.name = "inventory-page";
            page.AddToClassList("paper-page");
            page.Add(new Label("药箱") { name = "inventory-title" });

            _gridHost = new VisualElement { name = "item-grid" };
            var gridPaper = _library.Create(SkeuoElement.Paper);
            gridPaper.name = "item-grid-paper";   // ⚠️ 必须具名 —— 重建只清格、不清纸面
            _gridHost.Add(gridPaper);
            page.Add(_gridHost);

            _pageHost = new VisualElement { name = "pagination" };
            var pagePaper = _library.Create(SkeuoElement.Paper);
            pagePaper.name = "pagination-paper";
            _pageHost.Add(pagePaper);
            page.Add(_pageHost);

            host.Add(page);
            _root.Add(host);

            _rim = new VisualElement { name = "box-rim" };
            _rim.AddToClassList("box-rim");
            _rim.style.height = 2f;                              // 一条沿口(几何,不是色带)
            _rim.style.backgroundColor = new Color(0.28f, 0.20f, 0.13f);
            _rim.style.display = DisplayStyle.None;
            _gridHost.Add(_rim);

            _lid = new VisualElement { name = "box-lid" };
            _lid.AddToClassList("box-lid");
            _lid.style.height = 10f;                             // 半盖压下来那一截
            _lid.style.marginTop = -6f;                          // 与格区重叠 = 顶住(不是盖满)
            _lid.style.backgroundColor = new Color(0.20f, 0.14f, 0.09f);
            _lid.style.display = DisplayStyle.None;
            _gridHost.Add(_lid);

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

            // 🔁 **2026-11-06(`TD-skeuo-20`)**:重建**之前**先记下焦点在哪一格(按 `instance_id`)。
            // 病因:本屏此前**只有** `FirstFocusable` 属性,**没有任何地方消费它** ——
            //   `SkeuoRuntimeDriver` 只对主菜单 / 存档位 / 暂停壳三处显式聚焦(它们各自调),
            //   其余模态屏谁聚焦谁自己负责,而本屏两者都没有 ⇒ 开屏后
            //   `panel.focusController.focusedElement == null`,键盘 / 手柄玩家的
            //   **第一次 `Return`(导航提交)落空**,必须先按一次方向键。
            // 实测(真包 2026-11-06 11:52):第一次 `Return` 行无变化;`Down` + `Return` 才有金色边框。
            long focusedInstanceId = FocusedInstanceId();

            Band = BandOf(container.CarryLoad, container.CarryCap, container.IsFull);
            RebuildPage();          // 内含 ApplyBandGeometry
            FocusAfterRebuild(focusedInstanceId);
        }

        /// <summary>当前焦点所在格的 `instance_id`(焦点不在本屏的格上 ⇒ <c>-1</c>)。</summary>
        /// <remarks>⚠️ `focusController.focusedElement` 的静态类型是 `Focusable`
        /// (`VisualElement` 是它的子类)—— 必须先判类型再比对引用。</remarks>
        private long FocusedInstanceId()
        {
            VisualElement probe = FirstFocusable;
            if (probe == null || probe.panel == null) return -1L;

            Focusable current = probe.panel.focusController?.focusedElement;
            if (!(current is VisualElement element)) return -1L;

            for (int i = 0; i < _cells.Count; i++)
                if (ReferenceEquals(_cells[i], element)) return _cellInstanceIds[i];
            return -1L;
        }

        /// <summary>
        /// 重建之后落焦点(`TD-skeuo-20`):优先回到**重建前那一件物品**的格,否则落第一格。
        /// <para>⚠️ 为什么按 `instance_id` 而不是按元素引用:本屏每次 `Bind` 都**重造格**
        /// (`RebuildPage`),旧元素已脱离 panel ⇒ 按引用还原必然落空。按 id 还原才
        /// 「换箱重绑不把玩家读到一半的位置拽走」。</para>
        /// <para>⚠️ 空箱 / 元素未挂 panel ⇒ 不落(悬空 `Focus()` 会把焦点扔到看不见的元素上)。</para>
        /// </summary>
        private void FocusAfterRebuild(long preferredInstanceId)
        {
            VisualElement target = null;
            if (preferredInstanceId >= 0L)
            {
                for (int i = 0; i < _cellInstanceIds.Count; i++)
                {
                    if (_cellInstanceIds[i] != preferredInstanceId) continue;
                    target = _cells[i];
                    break;
                }
            }
            if (target == null) target = FirstFocusable;
            if (target == null || target.panel == null) return;

            target.Focus();
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
        /// <para>🔁 <b>2026-11-10(T22 批二)把中间两档的切点改回 spec 的整数式</b>:
        /// 原实现用 `slack ≤ ceil(cap/4)` / `≤ ceil(cap/2)` 两道**相对阈值** —— 那是实现自造,
        /// `inventory-container-20.md` §6(Approved)从来没有 3/4 那道界。该 spec 是**逐档一条整数式**
        /// (空 = `CarryLoad = 0` · 半满 = `2×CarryLoad ≤ cap` · 近满 = 剩下的,直到 20 的谓词
        /// 把最顶一档拿走)⇒ 现实现逐字用那两条,**中间不再有第二道阈值**。
        /// 实测差异:`cap=10 / load=6` 旧实现报 `Half`,spec 报 `NearFull`。</para>
        /// </remarks>
        public static CarryBand BandOf(long carryLoad, int carryCap, bool isFull)
        {
            if (carryLoad < 0L)
                throw new ArgumentOutOfRangeException(nameof(carryLoad), carryLoad,
                    "载重不可能为负(`CarryLoadSolver` 同一守卫;负值 = 投影或求和出了错)");
            if (carryCap < 0)
                throw new ArgumentOutOfRangeException(nameof(carryCap), carryCap,
                    "容量不可能为负(`CARRY_CAP` ∈ int>0;0 = 未装配,不是非法)");

            // 判据序 = §6 的自上而下首个命中(该表 2026-09-22 /ux-review 轮补的序声明)
            // —— **序**是函数良定的一部分,不是实现细节,故逐条对上那四行的顺序。
            if (isFull) return CarryBand.Full;      // 满:20 的具名谓词(本屏不重算)

            // 容量真值缺席(`cap == 0`)⇒ 恒空档,与载重无关(AC-20-13 禁默认容量;
            // 旧式子 `slack = -5 ≤ 0` 会把「不知道」报成「快满了」—— 夹具手算时当场撞到)。
            if (carryCap == 0) return CarryBand.Empty;

            if (carryLoad <= 0L) return CarryBand.Empty;   // 空:箱底可见

            // 半满:§6 的整数式 `2 × CarryLoad ≤ cap`(无浮点、无取整)
            if (2L * carryLoad <= carryCap) return CarryBand.Half;

            return CarryBand.NearFull;                     // 近满:剩下的,直到 20 的谓词接手
        }

        /// <summary>沿口是否可见(近满与满可见;<see cref="CarryBand.Half"/> 及以下不可见)。</summary>
        public bool IsRimVisible => _rim.style.display != DisplayStyle.None;

        /// <summary>半盖是否出现(只有满档 —— §6「合盖受阻(半盖顶住)」)。</summary>
        public bool IsLidVisible => _lid.style.display != DisplayStyle.None;

        /// <summary>当前格的竖直间隙(像素;越小 = 堆得越高)。观测面,供夹具比四档的几何差异。</summary>
        public float CellGapPx { get; private set; }

        /// <summary>
        /// 让四档在屏上**看得出区别**,且只用几何(§6:「四档做成颜色梯度」= 禁;「全走几何堆高」)。
        /// </summary>
        /// <remarks>
        /// <para>四档的几何语汇:空 = 器物零落(间隙最大)· 半满 = 仍留空隙 · 近满 = 挤到沿口
        /// (间隙归零 + 沿口出现)· 满 = 高出沿口且**半盖顶住**(间隙为负 + 半盖出现)。</para>
        /// <para>⚠️ <b>本方法不读载重</b> —— 它只读 <see cref="Band"/>,而 `Band` 的唯一来源是
        /// `BandOf`(满档又是 20 的谓词)。这里若再算一次 `slack`,就是 BL-27 禁的第二份判据。</para>
        /// </remarks>
        private void ApplyBandGeometry()
        {
            switch (Band)
            {
                case CarryBand.Empty:    CellGapPx = 6f;  break;
                case CarryBand.Half:     CellGapPx = 3f;  break;
                case CarryBand.NearFull: CellGapPx = 0f;  break;
                default:                 CellGapPx = -3f; break;   // Full:高出沿口
            }

            _rim.style.display = Band == CarryBand.NearFull || Band == CarryBand.Full
                ? DisplayStyle.Flex : DisplayStyle.None;
            _lid.style.display = Band == CarryBand.Full ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _cells.Count; i++)
                _cells[i].style.marginTop = CellGapPx;
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
            ApplyBandGeometry();   // 翻页重造格 ⇒ 几何须跟着回来(否则翻一页就掉回「空箱的零落」)
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
