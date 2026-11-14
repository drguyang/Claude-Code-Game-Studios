// 拾取面板(`ModalId.Loot` 第十一员)—— 2026-11-14 用户裁定
//
// 形态来源(用户当日原话):
//   「掉落捡起时有物品栏,点击单个进入背包,物品栏下方有全部捡取可以批量进入背包」
//   ⇒ 规格件 = `design/ux/loot-42.md`(与代码同批落盘,未经 `/ux-review`)。
//
// 架构权威:
//   · ADR-013 §9 C3 —— 42 **只渲染,永不持有游戏状态**:本屏不判「这件拿不拿得动」,
//     也不判「距玩家多远」;它只把**装配层给的**行画出来,并把「点了哪一件」报回去。
//   · ADR-013 §十-H Amendment G —— 本屏的闭集成员资格(第十一员)。
//   · ADR-009 §七 —— 拾取 = 意图事件 + 主机当下判距 + 宽容半径。判距**不在本屏**。
//
// ⚠️ **列表范围 = 拾取半径内的地面实例**(用户 2026-11-14 选定):与按 E 的判距同源,
//    ⇒ 列出来的每一条都点得动(不会出现「列了但结算被距判拒」的错配)。
//
// ⚠️ 本屏**不发流事件**:逐件与批量都只是**意图**,经 `ILootActionSink` 交给装配层,
//    由 20 的拾取编排面结算(`DropClaimed` 的唯一写入面)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>拾取面板的一行(呈现侧数据 —— 装配层已把显示名与数量拼好)。</summary>
    /// <remarks>⚠️ `Label` 由装配层给,本屏**不查物品表**(那会把 21 的数据面拖进 42)。</remarks>
    public readonly struct LootRow
    {
        /// <summary>地面实例 id(21 的 `ItemInstanceId` 空间;逐件意图就靠它点名)。</summary>
        public readonly long InstanceId;

        /// <summary>行文字(如「门板 ×1」)。</summary>
        public readonly string Label;

        /// <summary>构造一行。</summary>
        public LootRow(long instanceId, string label)
        {
            if (instanceId < 0) throw new ArgumentOutOfRangeException(nameof(instanceId), instanceId, "实例 id 不得为负");
            InstanceId = instanceId;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>拾取面板的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface ILootActionSink
    {
        /// <summary>拿取**被点名的那一件**(归属判定全在装配层 / 20)。</summary>
        void Claim(long instanceId);

        /// <summary>拿取当前列出的**全部**(实现方自行决定是否逐个结算)。</summary>
        void ClaimAll();
    }

    /// <summary>拾取面板(<c>ModalId.Loot</c>)—— 平面拟物 UI,键鼠点击 + 手柄焦点两条路。</summary>
    public sealed class LootScreen : PresentationRoot, IModalState
    {
        private const string RowNamePrefix = "loot-row-";
        private const string TakeAllName = "loot-take-all-row";
        private const string EmptyName = "loot-empty";

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly VisualElement _listHost;
        private readonly Label _empty;
        private readonly VisualElement _takeAllRow;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<long> _rowInstanceIds = new List<long>();

        private LootRow[] _items = Array.Empty<LootRow>();
        private ILootActionSink _sink;

        /// <summary>创建拾取面板。</summary>
        /// <param name="root">模态挂载根。</param>
        /// <param name="library">拟物元件库。</param>
        public LootScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;

            // 与其余模态同一块契约:列宽 + 页框(`.paper-page` 只给页级容器)。
            var host = new VisualElement { name = "loot-host" };
            host.AddToClassList("pause-column");

            var page = _library.Create(SkeuoElement.Paper);
            page.name = "loot-page";
            page.AddToClassList("paper-page");
            page.Add(new Label("拾取") { name = "loot-title" });

            _listHost = new VisualElement { name = "loot-list" };
            var listPaper = _library.Create(SkeuoElement.Paper);
            listPaper.name = "loot-list-paper";   // ⚠️ 具名 —— 重建只清行、不清纸面
            _listHost.Add(listPaper);
            page.Add(_listHost);

            _empty = new Label(string.Empty) { name = EmptyName };
            _empty.style.display = DisplayStyle.None;
            page.Add(_empty);

            _takeAllRow = new VisualElement { name = TakeAllName };
            _takeAllRow.AddToClassList("ruled");
            _takeAllRow.Add(new Label("全部拾取") { name = "loot-take-all-label" });
            page.Add(_takeAllRow);

            host.Add(page);
            _root.Add(host);

            // 手柄经官方焦点桥给 NavigationSubmitEvent;指针给 ClickEvent
            // (ADR-011:不自实现焦点算法)。
            _takeAllRow.RegisterCallback<NavigationSubmitEvent>(_ => ActivateTakeAll());
            _takeAllRow.RegisterCallback<ClickEvent>(_ => ActivateTakeAll());
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Loot;

        /// <summary>绑定意图出口(装配层在开屏前调;未绑 ⇒ 点了不报,诚实形态)。</summary>
        public void Bind(ILootActionSink sink) => _sink = sink;

        /// <summary>当前列出的行数(观测面)。</summary>
        public int RowCount => _rows.Count;

        /// <summary>当前列出的实例 id(按装配层给的次序;空 = 空数组)。</summary>
        public IReadOnlyList<long> ListedInstanceIds => _rowInstanceIds.ToArray();

        /// <summary>第 <paramref name="index"/> 行的实例 id(越界 ⇒ -1)。</summary>
        public long InstanceIdAt(int index)
            => index >= 0 && index < _rowInstanceIds.Count ? _rowInstanceIds[index] : -1L;

        /// <summary>第 <paramref name="index"/> 行的行文字(越界 ⇒ 空串)。</summary>
        public string RowLabelAt(int index)
            => index >= 0 && index < _rows.Count
                ? _rows[index].Q<Label>()?.text ?? string.Empty
                : string.Empty;

        /// <summary>「全部拾取」是否可落焦(<c>false</c> = 地上无物,屏只留空态一行)。</summary>
        public bool IsTakeAllActive => _rows.Count > 0;

        /// <summary>打开时应落焦的第一处(<c>null</c> = 空集,无可落焦点)。</summary>
        public VisualElement FirstFocusable => _rows.Count > 0 ? _rows[0] : null;

        /// <summary>
        /// 喂行并重建列表(开屏时调;每拿一件后由装配层重调 —— 列表随投影收缩)。
        /// </summary>
        /// <param name="rows">当前地面实例集(次序 = 装配层给的次序;本屏不再定序)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="rows"/> 为 null。</exception>
        public void Present(IReadOnlyList<LootRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            // ⚠️ 重建**之前**记下焦点落在哪一件(按实例 id),重建后原位回焦。
            long focusedId = -1L;
            VisualElement focused = _root.focusController?.focusedElement as VisualElement;
            if (focused != null && focused.name != null && focused.name.StartsWith(RowNamePrefix, StringComparison.Ordinal))
            {
                int idx = _rows.IndexOf(focused);
                if (idx >= 0 && idx < _rowInstanceIds.Count) focusedId = _rowInstanceIds[idx];
            }

            for (int i = _listHost.childCount - 1; i >= 0; i--)
            {
                VisualElement child = _listHost[i];
                if (child.name == "loot-list-paper") continue;
                _listHost.Remove(child);
            }
            _rows.Clear();
            _rowInstanceIds.Clear();

            _items = new LootRow[rows.Count];
            for (int i = 0; i < rows.Count; i++) _items[i] = rows[i];

            for (int i = 0; i < _items.Length; i++)
            {
                LootRow item = _items[i];
                int rowIndex = _rows.Count;
                var row = new VisualElement { name = RowNamePrefix + item.InstanceId.ToString() };
                row.AddToClassList("ruled");
                row.focusable = true;
                row.tabIndex = rowIndex;
                row.Add(new Label(item.Label));
                row.RegisterCallback<NavigationSubmitEvent>(_ => ActivateRow(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => ActivateRow(rowIndex));

                _rows.Add(row);
                _rowInstanceIds.Add(item.InstanceId);
                _listHost.Add(row);
            }

            bool hasRows = _rows.Count > 0;
            _empty.text = hasRows ? string.Empty : "地上没有可拾的物品";
            _empty.style.display = hasRows ? DisplayStyle.None : DisplayStyle.Flex;
            _takeAllRow.focusable = hasRows;
            _takeAllRow.SetEnabled(hasRows);

            if (focusedId >= 0)
            {
                int again = _rowInstanceIds.IndexOf(focusedId);
                if (again >= 0) _rows[again].Focus();
            }
        }

        /// <summary>
        /// 第 <paramref name="index"/> 行被激活(Submit / Click)。
        /// <para>⚠️ **公开**是因为 UI 事件在 EditMode 里发不起来(无 `PanelSettings` ⇒
        /// `panel == null` ⇒ `SendEvent` 不派发)。判据因此直调本方法;**事件绑定本身**
        /// 只有真包走查能验 —— 这条限制写在 `loot_screen_test` 的文件头,不当成已覆盖。</para>
        /// </summary>
        public void ActivateRow(int index)
        {
            if (index < 0 || index >= _rowInstanceIds.Count) return;
            _sink?.Claim(_rowInstanceIds[index]);
        }

        /// <summary>「全部拾取」行被激活(Submit / Click)。空集时**不报**(没有可拿的)。</summary>
        public void ActivateTakeAll()
        {
            if (_rows.Count == 0) return;
            _sink?.ClaimAll();
        }

        /// <summary>
        /// 把焦点落到指定实例那一行(按 E 选中的那一件);不在行集里 ⇒ 落第一行,空集 ⇒ 不落。
        /// </summary>
        /// <returns>是否找到了该实例。</returns>
        public bool FocusInstance(long instanceId)
        {
            int index = _rowInstanceIds.IndexOf(instanceId);
            if (index < 0)
            {
                if (_rows.Count == 0) return false;
                _rows[0].Focus();
                return false;
            }
            _rows[index].Focus();
            return true;
        }
    }
}
