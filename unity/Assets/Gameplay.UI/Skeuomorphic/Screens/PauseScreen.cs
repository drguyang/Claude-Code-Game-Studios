// 暂停壳(系统 42 自身)—— 存档位界面的**正式入口**。
//
// 权威来源:
//   · `design/ux/save-slots-7b.md:189`(存档位正式入口 = 暂停壳 / 主菜单「继续诊治」)
//   · ADR-013 **§十-C Amendment C**(2026-10-31 用户裁定 A:`ModalId.Pause` 第八员)
//   · ADR-013 §9 C3(42 只渲染,永不持有游戏状态)—— 本屏只发**意图**,不判定
//
// ⚠️ 本轮的形状 = **最小可操作壳**:两行(继续 / 存档位),零文字排版细节、零存档元数据。
//   它兑现的是「玩家有一个可发现、可聚焦、可按下的入口」,不是「暂停菜单做完了」。
//   排版 / 图标 / 「设置」「退出」等项归 42 的暂停壳 UX 轮(尚无 spec)。
//
// ⚠️ 行用 `.ruled` 类:与脉案行同一声明块 ⇒ 44px 下限 + 格线,不需要新样式表规则
//   (AB-3 的焦点落点高半已由该块保证)。

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using UnityEngine.UIElements;

    /// <summary>暂停壳的意图出口(实现方住装配层;42 不反向引用 Boot)。</summary>
    public interface IPauseCommandSink
    {
        /// <summary>继续(合上暂停壳)。</summary>
        void Resume();

        /// <summary>打开存档位界面(7b 的正式入口)。</summary>
        void OpenSaveSlots();
    }

    /// <summary>暂停壳(ModalId.Pause)—— 平面拟物 UI,手柄走查。</summary>
    public sealed class PauseScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;
        private readonly VisualElement _host;
        private readonly VisualElement[] _rows;

        /// <summary>创建暂停壳。</summary>
        public PauseScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            _host = new VisualElement { name = "pause-list" };
            _rows = new VisualElement[2];
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.Pause;

        /// <summary>注入意图出口(未注入 ⇒ 行照画、按了留空:未接线的诚实形态)。</summary>
        public void BindCommandSink(IPauseCommandSink sink) => _sink = sink;

        private IPauseCommandSink _sink;

        /// <summary>打开时应落焦的第一行。</summary>
        public VisualElement FirstFocusable => _rows[0];

        /// <summary>行数(观测面)。</summary>
        public int RowCount => _rows.Length;

        /// <summary>取某一行(观测面;越界 ⇒ null)。</summary>
        public VisualElement RowAt(int rowIndex)
            => rowIndex >= 0 && rowIndex < _rows.Length ? _rows[rowIndex] : null;

        /// <summary>确认某一行(两条输入路径的唯一汇合点)。</summary>
        /// <param name="rowIndex">0 = 继续,1 = 存档位;越界 ⇒ 忽略。</param>
        public void Activate(int rowIndex)
        {
            if (rowIndex == 0) _sink?.Resume();
            else if (rowIndex == 1) _sink?.OpenSaveSlots();
        }

        private void BuildUI()
        {
            _host.Add(_library.Create(SkeuoElement.Paper));
            _root.Add(_host);

            for (int i = 0; i < _rows.Length; i++)
            {
                int rowIndex = i;
                var row = new VisualElement { name = i == 0 ? "pause-resume" : "pause-save-slots" };
                row.AddToClassList("ruled");
                row.focusable = true;
                row.tabIndex = i;
                // 手柄经官方焦点桥给 NavigationSubmitEvent;指针给 ClickEvent
                // (ADR-011:不自实现焦点算法)。
                row.RegisterCallback<NavigationSubmitEvent>(_ => Activate(rowIndex));
                row.RegisterCallback<ClickEvent>(_ => Activate(rowIndex));
                _rows[i] = row;
                _host.Add(row);
            }
        }
    }
}
