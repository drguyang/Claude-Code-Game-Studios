// U1 spike 批 · ADR-013 假设 6 探针 —— 双触发 / 焦点可达性的自动化一半(判据①③)。
//
// 权威来源:ADR-013 §6.6(四判据)· 卡 production/u1-spike-checklist.md §5.2。
//   判据① 每次输入恰好一次焦点移动 —— 本探针记:nav 事件按帧计数,同帧 ≥2 =
//          疑似双触发(红字 WARNING);nav 总数 vs 焦点跳变总数的对账在 OnDisable 汇总。
//   判据③ 手柄无指针仍可达全部控件 —— 探针把当前焦点写进 status 标签(可见),
//          9 个按钮走查 + 手感(高亮是否清晰)仍归【桌面】人工四问。
//   判据②④(空间选邻居质量 / 单一 UI/Navigate 来源)不归本探针,见卡 §5.2。
//
// USS 由本探针在 Start 注入(UXML 不带 <Style>,单一注入点避免重复)。
// 本类住 Editor 程序集:spike 场景仅在编辑器 Play 用,不进任何构建。

#if UNITY_EDITOR
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DaYiJingCheng.EditorTools.Spike
{
    /// <summary>挂在 SpikeAssump6 的 UIDocument 物体上;Setup 菜单自动挂载。</summary>
    [DisallowMultipleComponent]
    internal sealed class U1FocusProbe : MonoBehaviour
    {
        UIDocument _doc;
        VisualElement _root;
        Label _status;

        int _navCount;
        int _navThisFrame;
        int _lastNavFrame = -1;
        int _doubleFrameCount;
        int _focusChanges;
        VisualElement _lastFocused;
        bool _warnedMissingRoot;

        IEnumerator Start()
        {
            _doc = GetComponent<UIDocument>();

            // UIDocument 的 root 可能晚一帧才就绪;最多等 2 秒。
            float t0 = Time.realtimeSinceStartup;
            while ((_root = _doc != null ? _doc.rootVisualElement : null) == null)
            {
                if (Time.realtimeSinceStartup - t0 > 2f)
                {
                    if (!_warnedMissingRoot)
                    {
                        Debug.LogWarning("[U1-A6] UIDocument.rootVisualElement 2s 未就绪 —— 检查 panelSettings / visualTreeAsset 赋值");
                        _warnedMissingRoot = true;
                    }
                    yield break;
                }
                yield return null;
            }

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(U1SpikePaths.UssAssump6);
            if (uss != null) _root.styleSheets.Add(uss);

            _status = _root.Q<Label>("status");

            // TrickleDown:nav 事件 target = 焦点元素,root 层先于冒泡看到 → 计数不漏。
            _root.RegisterCallback<NavigationMoveEvent>(OnNavMove, TrickleDown.TrickleDown);

            // 初始焦点,给手柄走查一个起点。
            var first = _root.Q<Button>("btn-0-0");
            if (first != null) first.Focus();

            Debug.Log("[U1-A6] 探针就绪 —— 每按一次方向键/摇杆,Console 应恰有一条 [U1-A6] nav 行;同帧两条 = 疑似双触发");
        }

        void OnNavMove(NavigationMoveEvent e)
        {
            int frame = Time.frameCount;
            if (frame != _lastNavFrame)
            {
                _lastNavFrame = frame;
                _navThisFrame = 0;
            }
            _navThisFrame++;
            _navCount++;

            var focused = (e.target as VisualElement)?.name ?? "<null>";
            Debug.Log($"[U1-A6] nav#{_navCount} dir={e.direction} frame={frame} target={focused}");

            if (_navThisFrame >= 2)
            {
                _doubleFrameCount++;
                Debug.LogWarning($"[U1-A6] 疑似双触发:frame={frame} 同帧第 {_navThisFrame} 次 NavigationMoveEvent —— 判据① 失败候选,记下复现步骤");
            }
        }

        void Update()
        {
            if (_root == null) return;

            var focused = _root.panel != null ? _root.panel.focusController?.focusedElement as VisualElement : null;
            if (!ReferenceEquals(focused, _lastFocused))
            {
                if (_lastFocused != null || focused != null)
                {
                    _focusChanges++;
                    Debug.Log($"[U1-A6] focus#{_focusChanges} → {(focused != null ? focused.name : "<none>")} frame={Time.frameCount}");
                }
                _lastFocused = focused;
            }

            if (_status != null)
                _status.text = $"nav={_navCount} focus={_focusChanges} 焦点={(focused != null ? focused.name : "-")}  双触发帧={_doubleFrameCount}";
        }

        void OnDisable()
        {
            // Play 停止时的对账汇总 —— 判据① 的核心证据行。
            Debug.Log($"[U1-A6] 汇总:nav 事件={_navCount} · 焦点跳变={_focusChanges} · 双触发帧={_doubleFrameCount}\n" +
                      $"[U1-A6] 判读:{(_doubleFrameCount == 0 && _navCount == _focusChanges ? "无同帧双触发且 nav↔focus 1:1 —— 判据① 观察通过(手感四问仍须人工)" : "nav↔focus 不等或有双触发帧 —— 判据① 失败候选,回报卡 §6")}");
        }
    }
}
#endif
