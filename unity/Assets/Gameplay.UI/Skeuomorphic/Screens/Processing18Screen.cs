// 炮制屏(系统 18 的数据面 · `ModalId.Processing` 第九员)—— 甲案(2026-11-02)
//
// 权威来源:
//   · `design/ux/processing-18.md`(本屏的 **per-screen UX spec**,2026-11-06 成文 · Status = Draft)
//       §6.1 三种空态(空纸 + 一行**不可聚焦**的状态注,措辞按成因)
//       §6.2 屏级状态(候选 / 二段态 / 已落火 / 空态 / 被拒 / 未绑定)
//       §7  二段确认 + 焦点序铁律(`Recipe.Ordinal` 升序、只增删不重排)+ 开屏落焦 + 重绑保持
//       §8  屏内零 `Append`(点火意图 → 主机裁决 → 世界流 `Craft`)
//   · `design/gdd/processing.md` UI-18.2 / 规则八 / `AC-18-10b`(不可发起 ⇒ **不出现,非灰置**)
//   · ADR-013 §十-E(第九员增员)· §9 C3(42 只渲染,永不持有游戏状态)
//
// 本屏做的事(2026-11-06 收口后):
//   ① 只建**可发起**的配方行(`AC-18-10b` 结构性成立:没有「灰置」这个可表达状态);
//   ② **二段确认**(用户 2026-11-06 裁定):第一次 Submit 只把该行推进落火态(`Ignite` 零调用),
//      方向键 / 焦点移开即撤销;第二次 Submit 才发意图,且恰一次;
//   ③ 三种空态出**状态注**(不可聚焦):炉忙 / 案上无料 / 无方可起;
//   ④ **开屏落焦**第一行、重绑按 `Recipe.Ordinal` 还原(=`TD-skeuo-20` / `TD-skeuo-21` 的同一条修法);
//   ⑤ 点火被接受后**留在屏内**:候选清零 + 状态注「炉上有药,火未熄」(用户 2026-11-06 裁定)。
//
// ⚠️ **本屏仍不持游戏状态**:候选 / 选中 / 落火态全是**呈现缓存**,开屏时喂入;
//    屏只发**意图**,判定与落流归主机(ADR-013 §9 C3 / ADR-005)。
// ⚠️ **不画进度**:等待期进度住世界器物状态(`skeuomorphic-ui.md:1259`),本屏零进度条 / 零倒计时。
//
// ── 命名避让(承 `coding-standards.md` §命名避让表)────────────────────────────
// 本文件受 `AC-42-F3` 词门扫描(**全文含注释**)⇒ 连注释里也不得出现那张表列出的词形。
// ⚠️ **不要在这里复述那张表** —— 复述本身就是命中(2026-11-06 实测:本块首版逐字抄了
// 九个词,`test_ac42f3_componentLibrary_noForbiddenItems` 当场转红)。需要指回时写「见避让表」。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using System;
    using System.Collections.Generic;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>炮制屏的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IProcessingCommandSink
    {
        /// <summary>点火:以当前选中的配方发起一炉。</summary>
        /// <returns>是否真的发起(<c>false</c> = 被上游拒绝,本屏不改任何视觉状态)。</returns>
        bool Ignite(int recipeOrdinal);

        /// <summary>关屏(ESC / 返回)。</summary>
        void Close();
    }

    /// <summary>炮制屏(<c>ModalId.Processing</c>)—— 平面拟物 UI,手柄走查。</summary>
    /// <remarks>
    /// ⚠️ 行可获焦并以官方焦点桥接收 `NavigationSubmitEvent`(ADR-011:不自实现焦点算法);
    /// 指针路径给 `ClickEvent`,两条输入在 <see cref="Activate"/> 汇合。
    /// </remarks>
    public sealed class Processing18Screen : PresentationRoot, IModalState
    {
        /// <summary>二段态的行内后缀(落火在即;不靠颜色,`UI-18.2`)。</summary>
        public const string ArmedSuffix = " ── 落火";

        /// <summary>空态:本玩家已有一炉在烧。</summary>
        public const string NoteFurnaceBusy = "炉上有药,火未熄";

        /// <summary>空态:选中集为空(玩家还没点料)。</summary>
        public const string NoteNoMaterial = "案上无料";

        /// <summary>空态:有料,但没有能起手的方子。</summary>
        public const string NoteNoRecipe = "无方可起";

        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly VisualElement _host;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<int> _rowRecipeIds = new List<int>();
        private readonly List<Label> _rowLabels = new List<Label>();
        private readonly List<string> _rowBaseText = new List<string>();

        private IProcessingCommandSink _sink;
        private IReadOnlyList<Recipe> _recipes;
        private IProcessingAdmissionQuery _admission;
        private ISingleFurnaceQuery _furnace;
        private int _actorId;
        private IReadOnlyList<int> _consumedQualities;
        private VisualElement _paper;
        private Label _note;
        private int _selectedOrdinal = -1;
        private int _armedRowIndex = -1;
        private bool _firedThisOpen;

        /// <summary>创建炮制屏。</summary>
        public Processing18Screen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            _host = new VisualElement { name = "processing-list" };
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Processing;

        /// <summary>注入意图出口(未注入 ⇒ 行照画、按了留空:未接线的诚实形态)。</summary>
        public void BindCommandSink(IProcessingCommandSink sink) => _sink = sink;

        /// <summary>打开时应落焦的第一行(<c>null</c> = 无候选,无可落焦点)。</summary>
        public VisualElement FirstFocusable => _rows.Count > 0 ? _rows[0] : null;

        /// <summary>当前建出来的行数(观测面 —— `AC-18-10b` 的判据面;空态 = 0)。</summary>
        public int RowCount => _rows.Count;

        /// <summary>取某一行(观测面;越界 ⇒ null)。</summary>
        public VisualElement RowAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rows.Count ? _rows[rowIndex] : null;

        /// <summary>某一行绑定的配方 id(观测面;越界 ⇒ -1)。</summary>
        public int RecipeOrdinalAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rowRecipeIds.Count ? _rowRecipeIds[rowIndex] : -1;

        /// <summary>当前选中的配方 ordinal(<c>-1</c> = 未选中)。</summary>
        public int SelectedRecipeOrdinal => _selectedOrdinal;

        /// <summary>处于落火态的行序(<c>-1</c> = 无)。</summary>
        public int ArmedRowIndex => _armedRowIndex;

        /// <summary>处于落火态的行所绑定的配方 ordinal(<c>-1</c> = 无)。</summary>
        public int ArmedRecipeOrdinal
            => _armedRowIndex >= 0 && _armedRowIndex < _rowRecipeIds.Count
                ? _rowRecipeIds[_armedRowIndex] : -1;

        /// <summary>状态注元素(观测面;无注 ⇒ <c>null</c>)。</summary>
        public VisualElement StatusNote => _note;

        /// <summary>状态注文本(观测面;无注 ⇒ 空串)。</summary>
        public string StatusNoteText => _note != null ? _note.text : string.Empty;

        /// <summary>
        /// 喂数据并**重建候选行**(打开屏时调用;也可在料位变化时重调)。
        /// </summary>
        /// <param name="recipes">21a 配方表(**全表**,不是子集 —— 过滤归本屏)。</param>
        /// <param name="admission">准入查询(`O-18-R6` 的接缝)。</param>
        /// <param name="actorId">当前玩家。</param>
        /// <param name="consumedQualities">**被点名实例**的品级集(空集合法 ⇒ 全部不可发起)。</param>
        /// <param name="furnace">单炉查询 —— **只用于空态选词**(「炉上有药」);可缺省。
        /// 点火是否可行仍由 <paramref name="admission"/> 判,本屏不自己合取。</param>
        /// <exception cref="ArgumentNullException">前三项任一为 null。</exception>
        /// <remarks>
        /// ⚠️ <b>过滤在这里做,判据不在</b>:本屏只问 <see cref="IProcessingAdmissionQuery.IsAdmissible"/>,
        /// **不自己合取**「等级 / 品质 / 炉忙」。自己合取 = `AC-18-20` 的第二实现。
        /// </remarks>
        public void Bind(IReadOnlyList<Recipe> recipes,
                         IProcessingAdmissionQuery admission,
                         int actorId,
                         IReadOnlyList<int> consumedQualities,
                         ISingleFurnaceQuery furnace = null)
        {
            // 重建会重造行 ⇒ 焦点只能按 ordinal 记(元素引用届时已脱离 panel)。`TD-skeuo-20` 同一条教训。
            int focusedOrdinal = FocusedOrdinal();

            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _admission = admission ?? throw new ArgumentNullException(nameof(admission),
                "未注入准入查询 —— 拒绝把「全部配方都列出来」当作可用(那是 AC-18-10b 的反面)");
            _consumedQualities = consumedQualities ?? throw new ArgumentNullException(
                nameof(consumedQualities));
            _actorId = actorId;
            _furnace = furnace;
            _firedThisOpen = false;

            Rebuild();
            FocusAfterRebuild(focusedOrdinal);
        }

        /// <summary>确认某一行(两条输入路径的唯一汇合点)。</summary>
        /// <param name="rowIndex">行序(0 起);越界 ⇒ 忽略。</param>
        /// <remarks>
        /// **二段语义**(`design/ux/processing-18.md` §7,用户 2026-11-06 裁定):
        /// 第一次调用只把该行推进落火态;再次调用同一行才发点火意图。
        /// 落火**不可撤销**(料在点火 tick 即真实消耗,`OQ-18-3`),故二段是必要的缓冲。
        /// </remarks>
        public void Activate(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowRecipeIds.Count) return;

            if (_armedRowIndex != rowIndex)
            {
                Disarm();
                _armedRowIndex = rowIndex;
                ApplyRowText(rowIndex);
                return;   // 第一段:**零意图**
            }

            int ordinal = _rowRecipeIds[rowIndex];
            _selectedOrdinal = ordinal;
            bool accepted = _sink != null && _sink.Ignite(ordinal);
            if (!accepted) return;   // 被拒 ⇒ 屏上零变化(可感面在世界侧,UI-18.2)

            // 点火被接受 ⇒ 留在屏内:候选清零 + 状态注。
            _firedThisOpen = true;
            Rebuild();
        }

        /// <summary>关屏(ESC / 返回)。</summary>
        public void RequestClose() => _sink?.Close();

        // ── 构建 ─────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            _rows.Clear();
            _rowRecipeIds.Clear();
            _rowLabels.Clear();
            _rowBaseText.Clear();
            _armedRowIndex = -1;
            _note = null;
            if (_paper != null) _paper.Clear();   // 只清行 / 注,不清纸面 / 列容器
            _selectedOrdinal = -1;

            // 首建:纸面 + 窄列只建一次(`_host` 挂进 `_root` 之后不再重建)。
            if (_paper == null)
            {
                _paper = _library.Create(SkeuoElement.Paper);
                _paper.name = "processing-paper";
                _host.AddToClassList("pause-column");
                _host.Add(_paper);
                _root.Add(_host);
            }

            if (_firedThisOpen)
            {
                // 刚点火的一屏:炉已着火 ⇒ 候选清零,注为「炉上有药」。
                ShowNote(NoteFurnaceBusy);
                return;
            }

            // ⚠️ 唯一过滤点:只建 `IsAdmissible == true` 的行。
            //    「不可发起 ⇒ 不建行」而不是「建了再灰置」—— AC-18-10b 逐字。
            for (int i = 0; i < _recipes.Count; i++)
            {
                Recipe recipe = _recipes[i];
                if (!_admission.IsAdmissible(in recipe, _actorId, _consumedQualities)) continue;

                int rowIndex = _rows.Count;
                int recipeOrdinal = recipe.Ordinal;
                var row = new VisualElement { name = "processing-recipe-" + recipeOrdinal };
                row.AddToClassList("ruled");
                row.focusable = true;
                row.tabIndex = rowIndex;
                // 文案 = 配方的稳定标识(`RecipeId`)。⚠️ 21a 表**无** `DisplayName` 字段
                // ⇒ 玩家看到的将是 `herb_soup` 这类键名,不是「麻黄汤」——
                // 已知粗糙边,归 21a 的显示名轮 / 42 的文案轮(`OQ-PR-1`)。
                var label = new Label(recipe.RecipeId);
                row.Add(label);

                // 手柄经官方焦点桥给 NavigationSubmitEvent;指针给 ClickEvent
                // (ADR-011:不自实现焦点算法)。
                row.RegisterCallback<NavigationSubmitEvent>(_ => Activate(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => Activate(rowIndex));
                // 焦点离开 ⇒ 撤销二段态(安全侧;方向键 / 指针移开都走这条)。
                row.RegisterCallback<FocusOutEvent>(_ =>
                {
                    if (_armedRowIndex == rowIndex) Disarm();
                });

                _rows.Add(row);
                _rowRecipeIds.Add(recipeOrdinal);
                _rowLabels.Add(label);
                _rowBaseText.Add(recipe.RecipeId);
                _paper.Add(row);
            }

            if (_rows.Count == 0) ShowNote(EmptyNote());
        }

        /// <summary>空态选词 —— 三种成因**互斥且全覆盖**(`design/ux/processing-18.md` §6.1)。</summary>
        /// <remarks>优先序:炉忙 &gt; 无料 &gt; 无方。只陈述**当前状态**,不陈述系统、不给操作指引。</remarks>
        private string EmptyNote()
        {
            if (_furnace != null && _furnace.IsBusy(_actorId)) return NoteFurnaceBusy;
            if (_consumedQualities == null || _consumedQualities.Count == 0) return NoteNoMaterial;
            return NoteNoRecipe;
        }

        private void ShowNote(string text)
        {
            var note = new Label(text) { name = "processing-note" };
            note.AddToClassList("pause-save-hint");   // 族内既有的「注」排版(小一号);不新增 USS 类
            note.focusable = false;                   // 状态注不是落点 —— 不得抢焦点
            note.pickingMode = PickingMode.Ignore;    // 指针也不该命中它
            _note = note;
            _paper.Add(note);
        }

        private void ApplyRowText(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowLabels.Count) return;
            _rowLabels[rowIndex].text = _rowBaseText[rowIndex] + ArmedSuffix;
        }

        private void Disarm()
        {
            int i = _armedRowIndex;
            _armedRowIndex = -1;
            if (i < 0 || i >= _rowLabels.Count) return;
            _rowLabels[i].text = _rowBaseText[i];
        }

        // ── 焦点 ─────────────────────────────────────────────────────────────

        /// <summary>当前焦点所在行的配方 ordinal(焦点不在本屏的行上 ⇒ <c>-1</c>)。</summary>
        /// <remarks>⚠️ `focusController.focusedElement` 的静态类型是 `Focusable`
        /// (`VisualElement` 是它的子类)—— 必须先判类型再比对引用。</remarks>
        private int FocusedOrdinal()
        {
            if (_rows.Count == 0) return -1;
            VisualElement probe = _rows[0];
            if (probe.panel == null) return -1;

            Focusable current = probe.panel.focusController?.focusedElement;
            if (!(current is VisualElement element)) return -1;

            for (int i = 0; i < _rows.Count; i++)
                if (ReferenceEquals(_rows[i], element)) return _rowRecipeIds[i];
            return -1;
        }

        /// <summary>
        /// 重建之后落焦点:优先回到**重建前那一行**的配方,否则落第一行;都不行 ⇒ 不落。
        /// <para>⚠️ 空态 / 元素未挂 panel ⇒ 不落 —— 悬空 `Focus()` 会把焦点扔到看不见的元素上。</para>
        /// </summary>
        private void FocusAfterRebuild(int preferredOrdinal)
        {
            VisualElement target = null;
            if (preferredOrdinal >= 0)
            {
                for (int i = 0; i < _rowRecipeIds.Count; i++)
                {
                    if (_rowRecipeIds[i] != preferredOrdinal) continue;
                    target = _rows[i];
                    break;
                }
            }
            if (target == null) target = FirstFocusable;
            if (target == null || target.panel == null) return;

            target.Focus();
        }
    }
}
