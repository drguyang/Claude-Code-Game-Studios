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
//
// 🔁 **2026-11-01 落笔轮**:补上 7b 交互表的**另一行**「存盘(落笔)」——
//   槽位行之后追加一行**新行**(`save-slot-new`),确认即 `ISaveSlotCommandSink.TrySave()`
//   (不带槽号:目标槽 = 台账的 `NextWritableSeq`,归 7a)。
//   ⚠️ 新行**不**计入 `SlotRowCount` / `RowAt`(`slot_seq` 升序的那一面不变)。
//   ⚠️ 仍未做:落笔成功后的「新行即第 1 焦点」与行内墨色由淡入浓 —— 前者要槽位清单
//   **在落笔后重取**(清单源今日是 `BootRoot.ManualSaveSlots` 常量,归 7b 数值轮),
//   后者要行内元数据源。两者**都不在本轮面内**,不借绿。
//
//   🔁 **2026-11-04 订正(上一段的三条,原文不删,逐条对过代码)**:
//     ✅「落笔后清单重取」**已做** —— `SkeuoRuntimeDriver.OnTickEdge` 里 `_saveSlotsDirty`
//        → `SetSaveSlotRows(null)` → 重走 `SaveSlotViewProvider`(`BootRoot.cs:1012`);
//     ✅「新行即第 1 焦点」**已做** —— 同一段在重画后 `saveSlots.WritableRow.Focus()`;
//     🔴「清单源是 `BootRoot.ManualSaveSlots` 常量」**这句是错的** ——
//        `BootRoot.BuildSaveSlotViews`(`:1860-1900`)枚举「退出保存 / 自动保存 / 手动区间」,
//        每槽经 `SaveSlotProbe.Probe` **真读盘**(占用态 / tick / 该档自己的时间参数)。
//        常量注入是 2026-11-01 的形态,已被 `TD-persist-15/16/17` 三笔改掉,注释没跟。
//     ⚠️「行内墨色由淡入浓」**仍未做**(`save-slot-writable` 今日只有 USS 的下边线,无浓淡动画)。
//
// 🔁 **2026-11-04 转册轮**:补上 `§5.1` 的「每页 6 行,超出转册续页」——
//   ① `PageSize = 6`,行只画**当前页那一段窗口**;② 鱼尾页码(`save-slot-page`);
//   ③ 两条页缘热区(`save-slot-prev` / `save-slot-next`),指针点 + 手柄 Submit 都能翻,
//   不可翻时**不占位**也退出焦点遍历;④ 翻页后焦点重置为该页首行(`§7.1`);
//   ⑤ 「留档」新行只出现在**末页**。
//   ✅ **2026-11-04 两条补齐(上一版的「未做」已消解)**:
//     · `§5.1`「文本缩放 150% ⇒ 页容量降为 4 行」—— 已落:`TextScale` 属性 + `PageCapacity`
//       (默认 1 ⇒ 6 行,行为不变)。⚠️ **数值归 49**(`TextScaleHook`),42 只消费;
//       装配层未注入前保持 100%,不早进 `OQ-SS-2` 裁定的 P2 验收面。
//     · `§7.1`「开册首焦点 = 高水位行**所在页**的首行」—— 已落:`SetSlotViews` 按
//       `HighWaterPage()` 定起始页;空册仍回首页。焦点本身仍由装配方 `FirstFocusable` 拿,
//       而它只含**当前页**的行 ⇒ 自然落在该页首行。
//   ⚠️ 页码的数字形态在 spec 里是**本地化素材档**,今日是「a / b」纯文本替身。

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
        /// <summary>
        /// 每页容量(`save-slots-7b.md` §5.1 的 OQ-SB-2:每页 6 行,超出转册续页)。
        /// <para>100% 文本缩放下的容量;放大到 <see cref="ZoomThreshold"/> 时降为
        /// <see cref="PageSizeZoomed"/>。实际容量一律读 <see cref="PageCapacity"/>。</para>
        /// </summary>
        public const int PageSize = 6;

        /// <summary>
        /// 放大后的页容量(§5.1 / 可访问性表:「文本缩放 75–150% ⇒ 页容量 6 行降为 4 行,
        /// 溢出转册续页,**不滚动**」)。
        /// </summary>
        public const int PageSizeZoomed = 4;

        /// <summary>
        /// 触发降档的缩放阈值(`150%`)。**含等号**:规格写的就是「150% ⇒ 4 行」。
        /// </summary>
        public const float ZoomThreshold = 1.5f;

        /// <summary>
        /// 当前文本缩放系数(1 = 100%)。
        /// <para>⚠️ **数值归 49**(`TextScaleHook` 的持有者);42 只**消费**。
        /// 本类不去读全局单例 —— 装配层把 49 的值灌进来即可(`OQ-SS-2`:无障碍档位验收
        /// 随 49 落,P0 不早进架)。未注入 ⇒ 保持 1,行为与降档前完全一致。</para>
        /// </summary>
        public float TextScale { get; set; } = 1f;

        /// <summary>当前缩放下的页容量(6 或 4)。</summary>
        public int PageCapacity => TextScale >= ZoomThreshold ? PageSizeZoomed : PageSize;

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        /// <summary>册(定宽居中的列):页缘 | 纸页 | 页缘。</summary>
        private readonly VisualElement _book;
        /// <summary>纸页本体 —— **行是它的子级**(不是兄弟)。</summary>
        private readonly VisualElement _slotHost;
        private readonly List<SaveSlotItem> _rows = new List<SaveSlotItem>();
        private readonly List<uint> _slots = new List<uint>();
        /// <summary>**全部**槽位视图(跨页保留;当前页只是它的一段窗口)。</summary>
        private readonly List<SaveSlotView> _views = new List<SaveSlotView>();
        private readonly Label _pageMark;

        /// <summary>页角注:「为什么不能」(7b §11)。空串 = 无。</summary>
        private readonly Label _notice;
        private readonly VisualElement _prevEdge;
        private readonly VisualElement _nextEdge;
        private ISaveSlotCommandSink _sink;
        /// <summary>「新行」(落笔入口;<c>null</c> = 尚未收到槽位清单)。</summary>
        private SaveSlotItem _writableRow;
        private int _pageIndex;
        /// <summary>处于二段态的**页内**行号(<c>-1</c> = 无;ADR-041 §D5)。</summary>
        private int _armedRowIndex = -1;
        /// <summary>已排进调度器、尚未执行的翻页(同帧连按只认一次)。</summary>
        private bool _turnPending;

        /// <summary>处于二段态的行号(观测面;<c>-1</c> = 没有任何一行展开)。</summary>
        public int ArmedRowIndex => _armedRowIndex;

        /// <summary>创建存档位界面。</summary>
        public SaveSlotsScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            _book = new VisualElement { name = "save-book" };
            _book.AddToClassList("save-book");

            // 🔁 **2026-11-04(`TD-skeuo-07`)**:纸页由「行的**兄弟**」改为「行的**父亲**」。
            //    旧形态下 `.paper` 是一个空容器,高度只等于自己的内边距 ⇒ 纸塌成顶部一条细带,
            //    行浮在没有底色的世界上;而焦点环尺寸 = 行尺寸、行铺满面板 ⇒ 环被拉成横贯全宽的铜带
            //    (与 `.pause-column` 2026-10-31 那条走查缺陷同一个病)。
            _slotHost = _library.Create(SkeuoElement.Paper);
            _slotHost.name = "slot-list";
            // 页框(`.paper-page`,`SkeuoPaper.uss`)—— 只有「页 / 卡」级容器挂;
            // **行不挂**(行贴边是正确形态,挂上会给每行套一圈框)。
            _slotHost.AddToClassList("paper-page");
            _slotHost.AddToClassList("save-page");

            // 鱼尾上的页码(`§5.1` 封面区的「当前页码」)—— 零字的物理载体。
            // ⚠️ 数字形态在 spec 里是**本地化素材档**;今日是「a / b」的纯文本替身。
            _pageMark = new Label { name = "save-slot-page" };
            _pageMark.AddToClassList("save-slot-page");

            // 🔁 2026-11-05 字号轮:**「为什么不能」改挂页角注**(7b §11 的原定落点:
            //   「页角注含可操作信息(『为什么不能』)⇒ **禁止自动消失**,保持到焦点移动」)。
            //   病因:上一轮把这句话拼在**行文字**上;行字 15→24px 后整行超宽
            //   ⇒ 实机 `12 · 第壹日 昼 · 已锁 · 推进一次剧情后可启封` **硬裁在页框外**
            //   (1600×900 实跑实拍)。24px 下行的可用宽度 ≈ 15.6 字,装不下这句。
            _notice = new Label { name = "save-slot-notice" };
            _notice.AddToClassList("save-slot-notice");

            // 页缘热区(`§5.2`):指针点页缘、手柄把焦点移到页缘行再 Submit。
            // ⚠️ **不是按钮元件** —— 7b §5.2 明写「零按钮元件」,页缘是纸面本体。
            _prevEdge = new VisualElement { name = "save-slot-prev" };
            _prevEdge.AddToClassList("save-slot-edge");
            _nextEdge = new VisualElement { name = "save-slot-next" };
            _nextEdge.AddToClassList("save-slot-edge");

            // 两条输入路径汇到同一出口(与槽位行同纪律:不自实现焦点算法)
            _prevEdge.RegisterCallback<NavigationSubmitEvent>(_ => PreviousPage());
            _prevEdge.RegisterCallback<ClickEvent>(_ => PreviousPage());
            _nextEdge.RegisterCallback<NavigationSubmitEvent>(_ => NextPage());
            _nextEdge.RegisterCallback<ClickEvent>(_ => NextPage());
            // 🔁 **2026-11-06(`TD-skeuo-19` 缺口 A)**:滚轮翻页 —— `save-slots-7b.md:256`
            //    的交互表从订立起就写着「点页缘 / **滚轮**」,而全库零 `WheelEvent` 实现。
            //    挂**册**而不是挂页缘:滚轮是「在册子上滚」的意图,玩家不会先把指针
            //    精确停在一条窄页缘上。滚轮语义不走「不可翻就不占位」那套(它不是焦点件)。
            _book.RegisterCallback<WheelEvent>(OnWheel);
            _prevEdge.focusable = true;
            _nextEdge.focusable = true;

            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.SaveSlots;

        /// <summary>当前页序号(0 起)。</summary>
        public int CurrentPage => _pageIndex;

        /// <summary>总页数(空册也算一页)。</summary>
        /// <remarks>🔁 **2026-11-06**:页数里**含「留档」新行**(它只出现在末页,但确实占一格)。
        /// 旧算法只数槽位行 ⇒ 槽位行恰好填满一页时,新行被塞进那一页的第 7 行(页容量 6),
        /// 纸页硬撑出一行 —— 4 笔手动档即触达,是既存缺陷。空册 = 1 页(新行自己占那一页)。</remarks>
        public int PageCount => (_views.Count + 1 + PageCapacity - 1) / PageCapacity;

        /// <summary>还有上一页。</summary>
        public bool HasPreviousPage => _pageIndex > 0;

        /// <summary>还有下一页。</summary>
        public bool HasNextPage => _pageIndex + 1 < PageCount;

        /// <summary>鱼尾页码元件(观测面)。</summary>
        public Label PageMark => _pageMark;

        /// <summary>前一页的页缘热区(观测面)。</summary>
        public VisualElement PreviousEdge => _prevEdge;

        /// <summary>后一页的页缘热区(观测面)。</summary>
        public VisualElement NextEdge => _nextEdge;

        /// <summary>当前页的槽位行数(观测面;0 = 尚未收到槽位清单,或本页为空)。</summary>
        public int SlotRowCount => _rows.Count;

        /// <summary>「新行」—— 落笔入口(观测面;<c>null</c> = 尚未收到槽位清单)。</summary>
        /// <remarks>⚠️ 它**不是**槽位行:不计入 <see cref="SlotRowCount"/> / <see cref="RowAt"/>,
        /// 因为它没有(也不该有)槽位号 —— 目标槽由 7a 的台账决定。</remarks>
        public SaveSlotItem WritableRow => _writableRow;

        /// <summary>取某一行(观测面;越界 ⇒ <c>null</c>)。</summary>
        public SaveSlotItem RowAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rows.Count ? _rows[rowIndex] : null;

        /// <summary>构建存档位界面 UI。</summary>
        private void BuildUI()
        {
            // 册的层次(`save-slots-7b.md` §5.2):页缘 | **纸页(行都在页里)** | 页缘。
            // ⚠️ 页缘是纸页的**兄弟**、不是页内的行 —— 它是纸面本体,不是按钮元件(§5.2「零按钮元件」)。
            _book.Add(_prevEdge);
            _book.Add(_slotHost);
            _book.Add(_nextEdge);
            // 页码由 `UpdateChrome` 每次重排时移到页脚(`BringToFront`),故此处不入树。
            _root.Add(_book);
        }

        /// <summary>
        /// 注入载入意图出口(实现方住装配层;42 不反向引用装配层)。
        /// <para>⚠️ 未注入 ⇒ 行照常画、确认动作**留空**(不崩、不造动作)—— 与
        /// <c>CasebookScreen.BindCommandSink</c> 同纪律:缺写通道是未接线的诚实形态。</para>
        /// </summary>
        public void BindCommandSink(ISaveSlotCommandSink sink) => _sink = sink;

        /// <summary>
        /// 按槽位号清单重建行(**未绑定**形态)。
        /// <para>⚠️ 只给了槽号 = 42 不知道哪一槽有档(7b §6「未绑定」:纸线格完整、行字空白)。
        /// 想画出「有档 / 空槽」的差异须走 <see cref="SetSlotViews"/>。</para>
        /// </summary>
        /// <param name="slotSeqs">槽位序号清单。null / 空 ⇒ 清空全部行。</param>
        public void SetSlots(IReadOnlyList<uint> slotSeqs)
        {
            if (slotSeqs == null) { SetSlotViews(null); return; }

            var views = new List<SaveSlotView>(slotSeqs.Count);
            for (int i = 0; i < slotSeqs.Count; i++)
                views.Add(SaveSlotView.Unbound(slotSeqs[i]));
            SetSlotViews(views);
        }

        /// <summary>
        /// 按**带状态的**槽位清单重建行(装配层每次开册前探一次)。
        /// </summary>
        /// <param name="views">槽位视图清单。null / 空 ⇒ 清空全部行。</param>
        /// <param name="followWritablePage">
        /// <c>true</c> = 翻到**「留档」行所在页**(写盘落定后的重画);<c>false</c> = 按 §7.1 落高水位页(开册)。
        /// <para>🔁 **2026-11-06 用户实测**:「存档第六行后,留档去了第二页,但界面还留在第一页」——
        /// 第 6 格恰好填满一页时,「留档」被挤到下一页;写盘后的重画若不跟过去,
        /// 玩家眼前那一页**没有可写的地方**,还以为存丢了。</para>
        /// </param>
        public void SetSlotViews(IReadOnlyList<SaveSlotView> views, bool followWritablePage = false)
        {
            _views.Clear();
            if (views != null) _views.AddRange(views);
            _pageIndex = followWritablePage ? WritablePage() : HighWaterPage();
            // 排进调度器的翻页若因**合屏 / 换源**没执行,标记必须归零 ——
            // 否则下次开册的第一下边界键会被当成「已排过」而静默丢掉。
            _turnPending = false;
            RenderPage();
        }

        /// <summary>「留档」行永远在**末页**(它排在全部槽位行之后)⇒ 它所在页 = 末页。</summary>
        private int WritablePage() => PageCount - 1;

        /// <summary>
        /// 画**当前页**:只建本页那一段窗口的行;「留档」新行只出现在**末页**。
        /// <para>⚠️ 行随页重建 ⇒ <see cref="SlotRowCount"/> / <see cref="RowAt"/> 都是**页内**面。</para>
        /// </summary>
        /// <summary>
        /// §7.1 开册首焦点:高水位行**所在页**的页码;空册 ⇒ 首页(0)。
        /// <para>高水位行 = **最后一个有档的行**(视图按 `slot_seq` 升序,手动槽单调分配
        /// ⇒ 最后一个 `HasSave` 即台账高水位)。</para>
        /// </summary>
        private int HighWaterPage()
        {
            for (int i = _views.Count - 1; i >= 0; i--)
            {
                if (_views[i].HasSave)
                    return i / PageCapacity;
            }
            return 0;
        }

        private void RenderPage()
        {
            // 缩放变化会让容量变小 ⇒ 旧页码可能越界;此处夹紧,不抛
            if (_pageIndex >= PageCount) _pageIndex = PageCount - 1;
            if (_pageIndex < 0) _pageIndex = 0;

            for (int i = 0; i < _rows.Count; i++) _rows[i].RemoveFromHierarchy();
            _rows.Clear();
            _slots.Clear();
            // 行随页重建 ⇒ 二段态作废(旧行连同它的两枚墨字一起被拆掉)。
            _armedRowIndex = -1;
            // 页角注同样作废:它陈述的是**某一行**的理由,行都不在了,理由不该留着。
            SetNotice(string.Empty);
            if (_writableRow != null) { _writableRow.RemoveFromHierarchy(); _writableRow = null; }

            int start = _pageIndex * PageCapacity;
            int end = System.Math.Min(start + PageCapacity, _views.Count);
            for (int i = start; i < end; i++)
            {
                SaveSlotView view = _views[i];
                var row = new SaveSlotItem((int)view.SlotSeq);
                row.Apply(view);
                int rowIndex = _rows.Count;   // **页内**行号
                // 两条输入路径都落到同一个 `Activate` —— 手柄经官方焦点桥给
                // `NavigationSubmitEvent`(ADR-011:不自实现焦点算法),指针给 `ClickEvent`。
                row.RegisterCallback<NavigationSubmitEvent>(_ => Activate(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => Activate(rowIndex));
                // 🔁 **2026-11-06(`TD-skeuo-19` 缺口 B)**:行边界上的上下移 = 翻页。
                //    挂在**行**上、TrickleDown 先看:只有「已经到本页最上 / 最下还继续走」
                //    才拦(此时 `HandleEdgeMove` 返回 true);中间行原样 return false,
                //    事件继续走官方焦点桥 —— 本屏仍**不自己实现焦点算法**(ADR-013)。
                //    ⚠️ 拦下后**必须重落焦**(`StepPage` 里做):不能让焦点留在被拆掉的旧行上。
                //    `PreventDefault` 是否真能挡住本版的默认焦点动作 —— **未验证**(夹具只跑
                //    用户回调阶段),归真包按键注入走查;若挡不住,默认动作的落点会盖掉重落焦。
                row.RegisterCallback<NavigationMoveEvent>(evt =>
                {
                    if (!IsEdgeMove(rowIndex, evt.direction)) return;
                    evt.StopPropagation();
                    evt.PreventDefault();
                    // ⚠️ 翻页**不在派发途中做**:它会拆掉正在收事件的那一行,而焦点系统与
                    //    默认动作阶段还拿着这个 target —— PlayMode 实测「当场翻页」会把焦点
                    //    丢成悬空(`FocusedRowIndex` = −1)。⇒ 推到本轮派发之后执行。
                    TurnAfterDispatch(evt.direction == NavigationMoveEvent.Direction.Up ? -1 : +1);
                }, TrickleDown.TrickleDown);
                // 焦点顺序 = 行序(AC-7b-04:`slot_seq` 升序,不可重排)。
                // ⚠️ 不自己实现焦点算法 —— tabIndex + focusable 交给官方 `FocusController`
                //    (ADR-013「不自实现焦点算法」);本屏只定**顺序**。
                row.tabIndex = rowIndex;
                _rows.Add(row);
                _slots.Add(view.SlotSeq);
                _slotHost.Add(row);
            }

            // 「留档」新行 = 末行之后(§5.2);跨页时它只属于**末页**。
            if (!HasNextPage) BuildWritableRow(_slotHost);

            UpdateChrome();
        }

        /// <summary>更新页码与两条页缘的可见性 / 焦点序。</summary>
        private void UpdateChrome()
        {
            _pageMark.text = (_pageIndex + 1) + " / " + PageCount;
            // 页角注(「为什么不能」)在页码**上方**,同为页脚小字。
            if (_notice.parent != _slotHost) _slotHost.Add(_notice);
            _notice.BringToFront();
            // 页码落在**页脚**:每次重排后把它移到子级末尾(行会不断追加在它前面)。
            // ⚠️ 用 `BringToFront` 而不是 `Add` —— 同一个元素重复 `Add` 会报「已有父级」。
            if (_pageMark.parent != _slotHost) _slotHost.Add(_pageMark);
            _pageMark.BringToFront();

            // 「还有下一页」由**纸边厚度**传达(§5.2),不由图标传达 ——
            // 不可翻时页缘**不占位**:① `display = None` 退出布局(不占纸面);
            // ② `focusable = false` 退出焦点遍历。
            // ⚠️ 两条**分开写**是有原因的:`display` 在文档里只说「是否参与布局」,
            //    没有任何一处说它会让元素退出焦点遍历 —— 靠它推断是**未验证的断言**。
            //    `focusable` 才是「能否接收焦点」的权威开关。
            _prevEdge.style.display = HasPreviousPage ? DisplayStyle.Flex : DisplayStyle.None;
            _nextEdge.style.display = HasNextPage ? DisplayStyle.Flex : DisplayStyle.None;
            _prevEdge.focusable = HasPreviousPage;
            _nextEdge.focusable = HasNextPage;

            int tab = _rows.Count + (_writableRow != null ? 1 : 0);
            _prevEdge.tabIndex = tab++;
            _nextEdge.tabIndex = tab;
        }

        /// <summary>翻到下一页(不可翻 ⇒ <c>false</c>,不抛)。</summary>
        public bool NextPage()
        {
            if (!HasNextPage) return false;
            _pageIndex++;
            RenderPage();
            FocusFirstRowOfPage();
            return true;
        }

        /// <summary>
        /// **行边界上的上下移 = 翻页**(`TD-skeuo-19` 缺口 B;用户 2026-11-06 裁定
        /// 「键盘和手柄的上下键移动到本页最上和最下就能进行翻页」)。
        /// <para>键鼠的方向键与手柄的 D-pad 都是**导航移动**,不是独立动作 ⇒ 不需要读
        /// `PagePrev` / `PageNext`,也就不需要在装配层再加一个输入驱动(那是把简单事做复杂)。</para>
        /// <para>语义:焦点在**首行**再向上 ⇒ 前一页;在**末行**再向下 ⇒ 后一页。
        /// 中间行照常移焦(本方法 <c>return false</c>,事件继续走官方焦点桥)。
        /// 到页边界 ⇒ 同样 <c>false</c>(没得翻 = 不动,与页缘热区同一纪律)。</para>
        /// </summary>
        /// <param name="rowIndex">当前焦点所在的**页内**行号(0 起)。</param>
        /// <param name="direction">导航方向。</param>
        /// <returns>是否真的翻了页。</returns>
        public bool HandleEdgeMove(int rowIndex, NavigationMoveEvent.Direction direction)
            => IsEdgeMove(rowIndex, direction)
                ? StepPage(direction == NavigationMoveEvent.Direction.Up ? -1 : +1)
                : false;

        /// <summary>
        /// **「留档」行上的向上 = 翻回前一页**(2026-11-06 用户实测:末页只剩它一格时,
        /// 向上走回不到前一页的末行)。
        /// <para>为什么单独一条:它**不在** <see cref="_rows"/> 里,行边界判据对它无从生效;
        /// 而它在竖轴上确实是**末端那一档**,所以「上面还有没有别的东西」= 本页有没有槽位行。</para>
        /// <para>本页还有槽位行 ⇒ 交给官方焦点桥(它会落到最后一行),本方法返回 false。</para>
        /// </summary>
        public bool HandleWritableEdgeMove(NavigationMoveEvent.Direction direction)
        {
            if (!IsWritableEdgeMove(direction)) return false;
            return StepPage(-1);
        }

        /// <summary>「留档」行上的这一下该不该由本屏翻页(只有「本页没有槽位行且还有前一页」)。</summary>
        private bool IsWritableEdgeMove(NavigationMoveEvent.Direction direction)
            => direction == NavigationMoveEvent.Direction.Up
               && _rows.Count == 0
               && HasPreviousPage;

        /// <summary>这一下是不是**页边界上的往外走**(是 ⇒ 该翻页;不是 ⇒ 放它继续走焦点桥)。</summary>
        private bool IsEdgeMove(int rowIndex, NavigationMoveEvent.Direction direction)
        {
            if (rowIndex < 0 || rowIndex >= _rows.Count) return false;
            // ⚠️ **必须是「有页可翻」才算边界** —— 否则拦下来又没有下一页时,焦点被卡在末行、
            //    永远走不到它下面的「留档」新行(真包实测:单页册上 `Down` 从末行按不动)。
            if (direction == NavigationMoveEvent.Direction.Up && rowIndex == 0) return HasPreviousPage;
            if (direction == NavigationMoveEvent.Direction.Down && rowIndex == _rows.Count - 1)
                return HasNextPage;
            return false;
        }

        /// <summary>
        /// 把翻页推到**本轮事件派发结束之后**再执行(见注册处的长注释)。
        /// <para>用屏根的调度器:它随屏入面板,派发结束时必然还在;同一帧连按只认一次。</para>
        /// </summary>
        private void TurnAfterDispatch(int direction)
        {
            if (_turnPending) return;
            _turnPending = true;
            _root.schedule.Execute(() =>
            {
                _turnPending = false;
                StepPage(direction);
            }).ExecuteLater(0);
        }

        /// <summary>
        /// 焦点所在的**页内**行号(观测面;<c>-1</c> = 焦点不在本页的槽位行上)。
        /// <para>「留档」新行算作 <see cref="SlotRowCount"/>(即行数之后的一位)——
        /// 它在这条**竖轴**上确实排在末行下面,滚轮/方向键从它往上走要落到末行。</para>
        /// </summary>
        public int FocusedRowIndex
        {
            get
            {
                if (!(_book.panel?.focusController?.focusedElement is VisualElement focused))
                    return -1;

                if (focused is SaveSlotItem item)
                {
                    int i = _rows.IndexOf(item);
                    if (i >= 0) return i;
                    if (_writableRow != null && ReferenceEquals(item, _writableRow))
                        return _rows.Count;
                }
                return -1;
            }
        }

        /// <summary>
        /// 滚轮(**缺口 A**;spec §7 交互表「点页缘 / 滚轮」)。
        /// <para>🔁 **2026-11-06 用户追加裁定**:「鼠标的滚轮也能移动焦点上下,也能进行翻页」——
        /// 滚轮**不是**「一格一页」,而是与上下键**同一条竖轴**:一格 = 一行,
        /// 到本页最上 / 最下再滚 = 翻页。两者共用 <see cref="HandleEdgeMove"/> 的边界判据。</para>
        /// <para>方向:UI Toolkit 的约定是**向上滚 = <c>deltaY &gt; 0</c>**(与浏览器 / 文本滚动一致)。</para>
        /// <para>焦点不在本页行上(未入面板 / 落在页缘或二段态的墨字上)⇒ 从滚来的那一端进入:
        /// 向下滚落**首行**,向上滚落**末行**。整页无可停之处 ⇒ <c>return false</c> 不消费。</para>
        /// <para>⚠️ 「末行」**含「留档」新行** —— 它是这条竖轴的**端点**,不是附属物:
        /// 末页只剩它一格时(槽位行恰好填满前一页),从它向上滚必须能翻回前一页的末行
        /// (2026-11-06 用户实测抓到:旧实现只看槽位行,`_rows.Count == 0` 直接 no-op)。</para>
        /// </summary>
        /// <param name="deltaY">滚轮位移(向下为负)。</param>
        /// <returns>是否消费了这次滚动(移动了焦点或翻了页)。</returns>
        public bool HandleWheel(float deltaY) => HandleWheel(deltaY, FocusedRowIndex);

        /// <param name="focusedRowIndex">起点(<see cref="FocusedRowIndex"/> 的显式形态,便于夹具直调)。</param>
        /// <inheritdoc cref="HandleWheel(float)"/>
        public bool HandleWheel(float deltaY, int focusedRowIndex)
        {
            if (deltaY == 0f) return false;   // 某些设备静止时也发零位移事件

            // 空册(一条槽位都没有)= 可导航的东西一件没有 ⇒ 不消费(与「空页翻不动」同一纪律)。
            // ⚠️ 判据是 `_views`,不是 `_rows` —— 「本页没有槽位行」不等于「册子是空的」:
            //    末页只剩「留档」一行时 `_rows.Count` 也是 0,而那时**必须**能向上翻回前一页。
            if (_views.Count == 0) return false;

            int slots = _rows.Count;
            bool hasWritable = _writableRow != null;
            int total = slots + (hasWritable ? 1 : 0);   // 竖轴总档位(槽位行 + 留档行)
            if (total == 0) return false;

            bool up = deltaY > 0f;
            if (focusedRowIndex < 0 || focusedRowIndex >= total)
                focusedRowIndex = up ? total : -1;   // 焦点悬空 ⇒ 从滚来的那一端进入

            if (up)
            {
                if (focusedRowIndex == 0) return StepPage(-1);
                FocusSlot(focusedRowIndex - 1);
                return true;
            }

            if (focusedRowIndex == total - 1) return StepPage(+1);   // 已在轴的末端
            FocusSlot(focusedRowIndex + 1);
            return true;
        }

        /// <summary>把焦点落到竖轴上的第 <paramref name="slot"/> 档(槽位行或「留档」行)。</summary>
        private void FocusSlot(int slot)
        {
            if (slot >= 0 && slot < _rows.Count) { _rows[slot].Focus(); return; }
            if (slot == _rows.Count) _writableRow?.Focus();
        }

        /// <summary>滚轮事件入口:处理了才消费掉(空页 ⇒ 让事件继续往上走)。</summary>
        private void OnWheel(WheelEvent evt)
        {
            if (evt == null) return;
            if (!HandleWheel(evt.delta.y)) return;
            evt.StopPropagation();
            evt.PreventDefault();
        }

        /// <summary>
        /// 翻一页并落到**移动来的那一端**(<paramref name="direction"/> = +1 后一页 / -1 前一页)。
        /// <para>⚠️ 落点与 <see cref="NextPage"/> 的「页内焦点重置为该页首行」**不同**,是有意的:
        /// 若向下翻页后焦点落在首行,再向上滚一格就立刻翻回去 —— 册子会卡在两页之间来回跳,
        /// 永远滚不进第二页。落到「移动来的那一端」后,整本册子是**一条连续的竖轴**。</para>
        /// <para>页缘热区 Submit 仍走 <see cref="NextPage"/> / <see cref="PreviousPage"/>
        /// (那是「我要翻页」的明确意图,落首行符合 §7.1)。</para>
        /// </summary>
        private bool StepPage(int direction)
        {
            if (direction < 0)
            {
                if (!HasPreviousPage) return false;
                _pageIndex--;
            }
            else
            {
                if (!HasNextPage) return false;
                _pageIndex++;
            }
            RenderPage();
            FocusPageEnd(fromTop: direction > 0);
            return true;
        }

        /// <summary>把焦点落到某一行(越界 / 未入面板时 <c>Focus()</c> 是空操作,不抛)。</summary>
        private void FocusRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rows.Count) return;
            _rows[rowIndex].Focus();
        }

        /// <summary>翻到上一页(不可翻 ⇒ <c>false</c>,不抛)。</summary>
        public bool PreviousPage()
        {
            if (!HasPreviousPage) return false;
            _pageIndex--;
            RenderPage();
            FocusFirstRowOfPage();
            return true;
        }

        /// <summary>翻页后把焦点重置为该页首行(§7.1「页内焦点重置为该页首行」)。</summary>
        private void FocusFirstRowOfPage() => FocusPageEnd(fromTop: true);

        /// <summary>翻页后落焦:本页首行或末行(无槽位行时退到「留档」新行)。</summary>
        private void FocusPageEnd(bool fromTop)
        {
            VisualElement target = null;
            if (_rows.Count > 0)
                target = fromTop ? (VisualElement)_rows[0] : (VisualElement)_rows[_rows.Count - 1];
            else if (_writableRow != null)
                target = _writableRow;
            // 未接入面板时 `Focus()` 是空操作(屏单元夹具即此形态),故无需判空面板。
            target?.Focus();
        }

        /// <summary>
        /// 建「新行」(7b:202 的「空行 / 末行后 Submit」)。
        /// <para>⚠️ 它**永远**画出来 —— 能不能落笔由装配层判(怀表两行会拒),本屏不判。
        /// 7b §6 的「可看不可落」正是这个形态:行在、按了被拒、零弹窗。</para>
        /// </summary>
        private void BuildWritableRow(VisualElement host)
        {
            _writableRow = new SaveSlotItem(-1) { name = "save-slot-new" }.AsWritableRow();
            // 两条输入路径汇到同一出口(与槽位行同纪律)
            _writableRow.RegisterCallback<NavigationSubmitEvent>(_ => ActivateWritable());
            _writableRow.RegisterCallback<ClickEvent>(_ => ActivateWritable());
            // 「留档」行上的向上:本页还有槽位行 ⇒ 交给官方焦点桥(落到最后一行);
            // 本页只剩它一格 ⇒ 翻回前一页的末行(`StepPage(-1)` 的落点)。
            _writableRow.RegisterCallback<NavigationMoveEvent>(evt =>
            {
                if (!IsWritableEdgeMove(evt.direction)) return;
                evt.StopPropagation();
                evt.PreventDefault();
                TurnAfterDispatch(-1);
            }, TrickleDown.TrickleDown);
            _writableRow.tabIndex = _rows.Count;   // 焦点顺序 = 行序,新行在最后
            host.Add(_writableRow);
        }

        /// <summary>
        /// 打开时应落焦的第一行(<c>null</c> = 没有行)。
        /// <para>由装配方(42 驱动)在开屏后请求聚焦 —— 游戏没有指针,「开了屏但焦点悬空」
        /// 等于手柄玩家一步也走不了。</para>
        /// </summary>
        public VisualElement FirstFocusable
            => _rows.Count > 0 ? _rows[0] : (VisualElement)_writableRow;

        /// <summary>
        /// 确认某一行(两条输入路径的唯一汇合点)。
        /// <para>只**转达意图**;能不能读、读完什么状态,全归装配层与 7a。</para>
        /// </summary>
        /// <param name="rowIndex">行序号(0 起)。越界 ⇒ 忽略(不抛:越界来自输入竞态,不是编程错误)。</param>
        public void Activate(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _slots.Count) return;

            SaveSlotView view = ViewOfRow(rowIndex);

            // 焦点已移到这一行 ⇒ 上一行留下的页角注作废(7b §11「保持到焦点移动」)。
            // ②-b 的已锁支会在下面重新写它。
            SetNotice(string.Empty);

            // ① **未绑定**(装配层没探到状态)⇒ 不声称任何事,维持旧的「尝试载入」路径。
            if (!view.Known)
            {
                _sink?.TryLoad(_slots[rowIndex]);
                return;
            }

            // ② **空槽** ⇒ 直接落笔(ADR-041 §D1;7b spec `:228` 的「空行 Submit」)。
            if (!view.HasSave)
            {
                DisarmRow();
                _sink?.TrySaveAt(_slots[rowIndex]);
                return;
            }

            // ②-b **已锁** ⇒ **照常进二段态**,但「重写」灰置(🔁 2026-11-06 用户裁定)。
            //   病因(旧形态):已锁槽按下去**屏上零反应** —— 异常只进日志、行上只多一句
            //   页角注;而「已锁」在 7a 里锁的其实是**读**,和玩家的直觉(「这页现在动不了」)拧着。
            //   ⇒ 现口径:锁 = 挡覆写、不挡读。二段态照出,启封可用、重写灰置不可获焦。
            //   ⚠️ 行上的「已锁」文字与页角注**都已删除** —— 状态只走整行灰底(用户明确要求)。

            // ③ **已有档** ⇒ 行内二段态(§D5):第一次 Submit 只出示两枚墨字,不动作。
            if (_armedRowIndex == rowIndex) return;   // 墨字已摊开 ⇒ 由它们接手
            ArmRow(rowIndex, view);
        }

        /// <summary>页角注(「为什么不能」)。空串 = 无话可说。</summary>
        /// <remarks>7b §11:该位**禁止自动消失**,保持到焦点移动 ⇒ 只在
        /// 「换行 / 翻页 / 重画」三处清空,不做定时清除。</remarks>
        public string NoticeText => _notice?.text ?? string.Empty;

        private void SetNotice(string text)
        {
            if (_notice == null) return;
            _notice.text = text ?? string.Empty;
            // 空注**不占位**:一句都没有时不留一条空白带(否则每页无端多一行高度)。
            _notice.style.display = string.IsNullOrEmpty(_notice.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        /// <summary>第 <paramref name="rowIndex"/> 行(页内)对应的视图。</summary>
        private SaveSlotView ViewOfRow(int rowIndex)
        {
            int index = _pageIndex * PageCapacity + rowIndex;
            return index >= 0 && index < _views.Count ? _views[index] : default;
        }

        /// <summary>
        /// 进入某行的二段态并落焦(默认落「启封」= 安全侧;已锁槽落「重写」)。
        /// <para>⚠️ 无指针形态:焦点必须**落到其中一枚墨字上**,否则手柄玩家按完第一下就卡住。</para>
        /// </summary>
        private void ArmRow(int rowIndex, in SaveSlotView view)
        {
            DisarmRow();
            SaveSlotItem row = RowAt(rowIndex);
            if (row == null) return;

            uint seq = _slots[rowIndex];
            row.EnterChoice(
                onOpen: () => { DisarmRow(); _sink?.TryLoad(seq); },
                onRewrite: () => { DisarmRow(); _sink?.TrySaveAt(seq); },
                // 🔁 2026-11-06(ADR-042 同批):灰置的是「重写」(已锁 = 不许盖回去)。
                rewriteDisabled: view.IsLocked);

            _armedRowIndex = rowIndex;

            // 落焦恒在「启封」= 安全侧:它是现在唯一恒可执行的那一枚。
            VisualElement focus = row.OpenChoice;
            if (focus != null && focus.panel != null) focus.Focus();
        }

        /// <summary>合回二段态(幂等;翻页 / 重画 / 选了别的行都会走它)。</summary>
        private void DisarmRow()
        {
            if (_armedRowIndex < 0) return;
            RowAt(_armedRowIndex)?.ExitChoice();
            _armedRowIndex = -1;
        }

        /// <summary>
        /// 确认**新行**(落笔)。两条输入路径的唯一汇合点(与 <see cref="Activate"/> 同纪律:
        /// 各写一份判定 = 将来必然分叉)。
        /// <para>⚠️ 不传槽号 —— 目标槽是 7a 台账的 `NextWritableSeq`(42 不持有)。</para>
        /// </summary>
        public void ActivateWritable() => _sink?.TrySave();
    }
}
