// 炮制屏(系统 18 的数据面 · `ModalId.Processing` 第九员)—— 甲案(2026-11-02)
//
// 权威来源:
//   · `design/gdd/processing.md` UI-18.3(`:664`「42 的屏闭集须补入炮制交互」/ `O-18-R6`)
//   · `design/gdd/processing.md` AC-18-10b(`:732`「不可发起的配方 **不出现(非灰置)**」)
//   · `design/gdd/processing.md` 规则八(选料 → 点火 → 起货 三步)
//   · ADR-013 §十(闭集增员须 Amendment)—— 本轮取 **Amendment D**(第九员)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发**意图**,不判定
//
// ⚠️ **本轮的形状 = 最小可操作屏**:候选行(只列可发起的)+ 选中 + 点火。
//   它兑现的是「玩家有一个可发现、可聚焦、可按下的炮制入口」,
//   **不是**「炮制面板做完了」。缺的(如实登记):
//     · 无 `design/ux/processing-18.md` spec ⇒ 排版 / 文案 / 焦点序**均未经 UX 评审**
//     · 无起货(完成态)呈现 —— 规则八说完成是纯派生,起货由 20 从 `Craft` 派生
//     · 无料位显示(「这一剂要哪几味」)—— 那要 20 的选中态,归 4 的交互轮
//
// ⚠️ **`AC-18-10b` 的判据在这里怎么成立**:本屏**只把 `IsAdmissible == true` 的行建出来**,
//   不可发起的不建行(不是建了再灰置)。⇒ 「不出现(非灰置)」在**结构上**成立 ——
//   没有「灰置态」这个可表达状态。
//
// ⚠️ 行用 `.ruled` 类(与脉案行同一声明块 ⇒ 44px 下限 + 格线,AB-3 焦点落点高半已保证),
//   纸面用 `.paper`,窄列用 `.pause-column` —— **零新增 USS 规则**(42 的 C4/C5 门不会红)。

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
        /// <summary>点火:以当前选中配方发起一炉。</summary>

        /// <returns>是否真的发起(<c>false</c> = 被上游拒绝,本屏不改任何视觉状态)。</returns>
        bool Ignite(int recipeOrdinal);

        /// <summary>关屏(ESC / 返回)。</summary>
        void Close();
    }

    /// <summary>炮制屏(`ModalId.Processing`)—— 平面拟物 UI,手柄走查。</summary>
    public sealed class Processing18Screen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly VisualElement _host;
        private readonly List<VisualElement> _rows = new List<VisualElement>();
        private readonly List<int> _rowRecipeIds = new List<int>();

        private IProcessingCommandSink _sink;
        private IReadOnlyList<Recipe> _recipes;
        private IProcessingAdmissionQuery _admission;
        private int _actorId;
        private IReadOnlyList<int> _consumedQualities;
        private VisualElement _paper;
        private int _selectedOrdinal = -1;

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

        /// <summary>当前建出来的行数(观测面 —— `AC-18-10b` 的判据面)。</summary>
        public int RowCount => _rows.Count;

        /// <summary>取某一行(观测面;越界 ⇒ null)。</summary>
        public VisualElement RowAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rows.Count ? _rows[rowIndex] : null;

        /// <summary>某一行绑定的配方 id(观测面;越界 ⇒ -1)。</summary>
        public int RecipeOrdinalAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rowRecipeIds.Count ? _rowRecipeIds[rowIndex] : -1;

        /// <summary>当前选中的配方 ordinal(<c>-1</c> = 未选中)。</summary>
        public int SelectedRecipeOrdinal => _selectedOrdinal;

        /// <summary>
        /// 喂数据并**重建候选行**(打开屏时调用;也可在料位变化时重调)。
        /// </summary>
        /// <param name="recipes">21a 配方表(**全表**,不是子集 —— 过滤归本屏)。</param>
        /// <param name="admission">准入查询(`O-18-R6` 的接缝)。</param>
        /// <param name="actorId">当前玩家。</param>
        /// <param name="consumedQualities">**被点名实例**的品级集(空集合法 ⇒ 全部不可发起)。</param>
        /// <exception cref="ArgumentNullException">任一引用参数为 null。</exception>
        /// <remarks>
        /// ⚠️ <b>过滤在这里做,判据不在</b>:本屏只问 <see cref="IProcessingAdmissionQuery.IsAdmissible"/>,
        /// **不自己合取**「等级 / 品质 / 炉忙」。自己合取 = AC-18-20 的第二实现。
        /// </remarks>
        public void Bind(IReadOnlyList<Recipe> recipes,
                         IProcessingAdmissionQuery admission,
                         int actorId,
                         IReadOnlyList<int> consumedQualities)
        {
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _admission = admission ?? throw new ArgumentNullException(nameof(admission),
                "未注入准入查询 —— 拒绝把「全部配方都列出来」当作可用(那是 AC-18-10b 的反面)");
            _consumedQualities = consumedQualities ?? throw new ArgumentNullException(
                nameof(consumedQualities));
            _actorId = actorId;
            Rebuild();
        }

        /// <summary>确认某一行(两条输入路径的唯一汇合点)。</summary>
        /// <param name="rowIndex">行序(0 起);越界 ⇒ 忽略。</param>
        public void Activate(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowRecipeIds.Count) return;
            _selectedOrdinal = _rowRecipeIds[rowIndex];
            _sink?.Ignite(_selectedOrdinal);
        }

        /// <summary>关屏(ESC / 返回)。</summary>
        public void RequestClose() => _sink?.Close();

        private void Rebuild()
        {
            _rows.Clear();
            _rowRecipeIds.Clear();
            if (_paper != null) _paper.Clear();   // 只清行,不清纸面/列容器
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
                // 文案 = 配方的**显示名**(21a 表提供);不在此翻译 / 不在此改写。
                // 文案 = 配方的**稳定标识**(`RecipeId` 是 string 键)。⚠️ 21a 表**无** `DisplayName` 字段
                // ⇒ 玩家看到的将是 `herb_soup` 这类键名,不是「麻黄汤」。这是本轮**已知的粗糙边**,
                // 归 21a 的显示名轮 / 42 的文案轮(不在本屏内翻译)。
                row.Add(new Label(recipe.RecipeId));
                // 手柄经官方焦点桥给 NavigationSubmitEvent;指针给 ClickEvent
                // (ADR-011:不自实现焦点算法)。
                row.RegisterCallback<NavigationSubmitEvent>(_ => Activate(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => Activate(rowIndex));

                _rows.Add(row);
                _rowRecipeIds.Add(recipeOrdinal);
                _paper.Add(row);
            }
        }
    }
}
