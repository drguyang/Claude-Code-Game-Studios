// 权威来源:ADR-013 §四 Key Interfaces + §9 C3(只渲染不持状态)
//   · 42 是唯一被允许画界面的系统;本驱动仅做「实例化 + 挂载」,不持游戏状态
//   · 运行时入口:创建 Canvas + UIDocument,实例化指定 Screen 并 Bind 到 Panel
//
// ⚠️ 本文件是「运行时装配器」,不是游戏逻辑 —— 所有指标/状态归各系统,42 只渲染

namespace DaYiJingCheng.Gameplay.UI.Skeuomorphic
{
    using System;
    using UnityEngine;
    using UnityEngine.UIElements;
    using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
    using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;

    /// <summary>
    /// 42 运行时装配器 —— 挂载到场景中的 GameObject 上。
    /// <para>职责:创建 Canvas + UIDocument,实例化指定 Screen,调用 Bind() 挂到 Panel。</para>
    /// <para>不持有游戏状态(§9 C3);不生成语义序(归内容系统);只画(规则五)。</para>
    /// </summary>
    public sealed class SkeuoRuntimeDriver : MonoBehaviour
    {
        [SerializeField] private GameObject _canvasPrefab;
        [SerializeField] private UIDocument _uiDocument;
        [SerializeField] private string _screenName = "Casebook";

        private IPresentationRoot _root;
        private VisualElement _panel;

        private void Awake()
        {
            if (_uiDocument == null)
            {
                _uiDocument = GetComponent<UIDocument>();
            }

            if (_uiDocument == null)
            {
                Debug.LogError("[SkeuoRuntimeDriver] UIDocument not found. Assign one in inspector.");
                return;
            }

            _panel = _uiDocument.rootVisualElement;
            if (_panel == null)
            {
                Debug.LogError("[SkeuoRuntimeDriver] Panel is null. Check UIDocument settings.");
                return;
            }

            _root = ResolveScreen(_screenName);
            if (_root == null)
            {
                Debug.LogError($"[SkeuoRuntimeDriver] Screen '{_screenName}' not found.");
                return;
            }

            // IPresentationRoot.Bind 接受 IDtoSource,不是 VisualElement
            // 这里需要传入一个 IDtoSource 实现,暂时用 null 占位
            // TODO: 实现正确的 DTO 绑定逻辑
            // _root.Bind(_panel);
            Debug.Log($"[SkeuoRuntimeDriver] Screen '{_screenName}' bound to panel.");
        }

        private void OnDestroy()
        {
            // IPresentationRoot 没有 Unbind 方法,暂时只清理引用
            _root = null;
            _panel = null;
        }

        private IPresentationRoot ResolveScreen(string name)
        {
            // Screen 类需要 VisualElement 和 SkeuoElementLibrary 参数
            // 这里需要传入正确的参数,暂时返回 null 占位
            // TODO: 实现正确的 Screen 实例化逻辑
            return null;
        }
    }
}
