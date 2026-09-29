// 权威来源:ADR-013 §四 Key Interfaces + §9 C3(只渲染不持状态)
//   · production/epics/skeuomorphic-ui/story-004-data-boundary.md
//
// 设计说明:
//   · 平面栈实现:持有 VisualElement 根 + 焦点门状态
//   · 不缓存 DTO 副本(铁律:只渲染不持状态)
//   · Bind() 只存储源引用,每次渲染时从源读取最新值

namespace DaYiJingCheng.Gameplay.Presentation.Skeuomorphic
{
    using DaYiJingCheng.Sim.Contracts;
    using UnityEngine.UIElements;

    /// <summary>
    /// 平面栈呈现根实现——UI Toolkit 平面拟物 UI 的根节点。
    /// <para>铁律:只渲染,永不持有游戏状态(§9 C3)。</para>
    /// </summary>
    public class PresentationRoot : IPresentationRoot
    {
        private readonly VisualElement _root;
        private IDtoSource _source;
        private bool _focusGateActive;

        /// <summary>创建平面栈呈现根。</summary>
        public PresentationRoot(VisualElement root)
        {
            _root = root;
        }

        /// <inheritdoc/>
        public void SetFocusGate(bool active)
        {
            _focusGateActive = active;
            // 焦点门状态变更时通知 UI 层
            _root?.EnableInClassList("focus-active", active);
        }

        /// <inheritdoc/>
        public void Bind(IDtoSource source)
        {
            _source = source;
            // 绑定数据源,不缓存副本
        }

        /// <summary>获取当前绑定的数据源(只读)。</summary>
        public IDtoSource Source => _source;

        /// <summary>焦点门是否活跃。</summary>
        public bool IsFocusGateActive => _focusGateActive;

        /// <summary>获取根 VisualElement。</summary>
        public VisualElement Root => _root;
    }
}
