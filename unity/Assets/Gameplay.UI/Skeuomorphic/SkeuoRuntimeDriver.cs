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

            _root.Bind(_panel);
            Debug.Log($"[SkeuoRuntimeDriver] Screen '{_screenName}' bound to panel.");
        }

        private void OnDestroy()
        {
            _root?.Unbind();
            _root = null;
            _panel = null;
        }

        private IPresentationRoot ResolveScreen(string name)
        {
            switch (name)
            {
                case "Casebook": return new CasebookScreen();
                case "SaveSlots": return new SaveSlotsScreen();
                case "Inventory": return new InventoryScreen();
                case "Settings": return new SettingsScreen();
                case "Tutorial": return new TutorialScreen();
                case "ClinicPanel": return new ClinicPanelScreen();
                case "PaperCloseup": return new PaperCloseupScreen();
                default: return null;
            }
        }
    }
}
