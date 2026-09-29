// 权威来源:production/epics/skeuomorphic-ui/story-006-clinic-panel-refresh.md
//   · design/ux/clinic-panel-24.md
//   · ADR-013 §四 Key Interfaces + AC-42-F5(刷新延迟契约)
//
// 设计说明:
//   · 医馆面板 = 乘子/情境摘要的只读数据源
//   · 继承 PresentationRoot,实现 IModalState
//   · 使用 SkeuoElementLibrary 创建元件

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens
{
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 医馆面板(24)——乘子/情境摘要。
    /// <para>ModalId.ClinicPanel,刷新延迟契约归 42(AC-42-F5)。</para>
    /// </summary>
    public sealed class ClinicPanelScreen : PresentationRoot, IModalState
    {
        private readonly VisualElement _root;
        private readonly SkeuoElementLibrary _library;

        /// <summary>创建医馆面板。</summary>
        public ClinicPanelScreen(VisualElement root, SkeuoElementLibrary library) : base(root)
        {
            _root = root;
            _library = library;
            BuildUI();
        }

        /// <inheritdoc/>
        public ModalId Modal => ModalId.ClinicPanel;

        /// <summary>构建医馆面板 UI。</summary>
        private void BuildUI()
        {
            var multiplierDisplay = _library.Create(SkeuoElement.Paper);
            _root.Add(multiplierDisplay);

            var situationSummary = _library.Create(SkeuoElement.Paper);
            _root.Add(situationSummary);
        }

        /// <summary>
        /// 刷新延迟契约(AC-42-F5)—— Structure* 事件 Append 后**下一帧**刷新。
        /// <para>本方法只置脏标;真正的重画发生在下一帧的 <see cref="ApplyDeferredRefresh"/>。
        /// 不在同一帧内立即刷新(同一帧刷新 = 破 AC-42-F5 的判据)。</para>
        /// </summary>
        public void Refresh()
        {
            _refreshPending = true;
        }

        /// <summary>下一帧执行的延迟刷新(由帧循环调用;重读源数据,不缓存副本)。</summary>
        public void ApplyDeferredRefresh()
        {
            if (!_refreshPending)
                return;
            _refreshPending = false;
            // 重读绑定源并重画 —— 42 只读不持状态,此处无缓存字段
            BuildUI();
        }

        private bool _refreshPending;
    }
}
