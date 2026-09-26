// Story 009 · C2 焦点导航绑重构造断言(Navigate 单一真源 · AC-3-C2)
//
// 权威来源:
//   Story: production/epics/input-system/story-009-focus-navigation-intent.md
//     · AC-3-C2(BLOCKING · 结构性构造断言) —— 喂 Navigate 的物理控件不得喂任何第二焦点动作
//   TR: TR-input-010(Navigate 单一真源 + 单栈门)
//   ADR: ADR-011 §三 / Amendment A / Amendment B · ADR-013 §九/Amendment
//   GDD: input-system.md 规则十(Navigate 单一真源) · 规则十一 · 规则十二
//
// 形状口径:
//   C2 = 构造断言而非行为断言:扫 .inputactions 绑重数据,焦点控件的物理路径 ∉ 第二焦点
//   动作的绑重路径。这条在数据层可静态验证,无需跑游戏、无需知道焦点载体是谁。
//   C2 依赖 A1 前半条单实例(Navigate 在资产内恰一个 InputAction 实例)。
//
// 已知第二焦点动作名称集合(大小写不敏感匹配):Navigate / FocusNavigate / UINavigate /
//   MenuNavigate。这是「第二焦点动作」的候选池 —— 任何动作名命中本集合即视为
//   第二焦点动作候选,其绑重与 Navigate 绑重的物理路径交集 = 违例。
// ⚠️ 名称集是**项目配置项**——新增焦点动作名须同步本集合(承 AC-C2 补注)。
//
// 设备布局前缀集合(剥除后比较设备相对路径):
//   Keyboard / Gamepad / XRController / XRControllerVive / XRControllerOculusTouch /
//   XRControllerOpenVR / XRControllerWMR / XRControllerHandedness / XRControllerValveIndex.
//   Unity Input System 的标准 XR 布局前缀;任何 &lt;布局名&gt;/... 形态的绑定
//   剥除 &lt;...&gt; 后比较剩余段。
//
// 落点:Gameplay.Input 根命名空间(非 Intents 子空间——本类是 C2 构造断言基础设施,
//   与 BindingQuery 同族)。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>C2 焦点导航绑重构造断言(AC-3-C2 · TR-input-010):确保喂 <c>Navigate</c> 的
    /// 物理控件路径不喂任何第二焦点动作。<b>结构性断言,零运行时副作用</b>。</summary>
    /// <remarks>
    /// <para>断言逻辑:从 <c>.inputactions</c> 资产提取 Navigate 绑重的**设备相对路径**,
    /// 与每个第二焦点动作候选的绑重设备相对路径做集合差;差集非空 = 违例。</para>
    /// <para>设备相对路径 = 剥除 <c>&lt;布局名&gt;</c> 前缀后的控制路径(例:
    /// <c>&lt;Keyboard&gt;/e</c> → <c>e</c>; <c>&lt;Gamepad&gt;/dpad/up</c> → <c>dpad/up</c>)。
    /// 同一物理按键在不同布局下视为同一控件(例:手柄 A 键在 XRController 与 Gamepad
    /// 布局下 = 不同布局前缀但同一条物理控制路径)。</para>
    /// <para>复合绑重的 part 路径(isPartOfComposite = true)同样参与比较(剥除设备前缀后)。
    /// 复合本体路径(如 <c>2DVector</c>)无设备前缀 ⇒ 不参与比较(它不指代具体物理控件)。</para>
    /// </remarks>
    public static class NavigationBindingAssert
    {
        // ── 配置项(单一出处;新增焦点动作名须同步本集合)──
        /// <summary>已知焦点导航动作名称集合(大小写不敏感)。
        /// ⚠️ 新增第二焦点动作名须同步本集合(AC-3-C2「第二焦点动作」的候选池)。</summary>
        public static readonly HashSet<string> KnownFocusActionNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Navigate",
            "FocusNavigate",
            "UINavigate",
            "MenuNavigate",
        };

        /// <summary>已知设备布局前缀集合(用于剥除设备前缀取设备相对路径)。
        /// 覆盖 Unity Input System 标准布局 + XR 变体。</summary>
        public static readonly HashSet<string> KnownLayoutPrefixes = new(StringComparer.Ordinal)
        {
            "Keyboard", "Gamepad",
            "XRController", "XRControllerVive", "XRControllerOculusTouch",
            "XRControllerOpenVR", "XRControllerWMR", "XRControllerHandedness",
            "XRControllerValveIndex",
        };

        // ── 核心谓词──

        /// <summary>从动作资产提取 Navigate 绑重的**设备相对控制路径**(剥除 &lt;布局名&gt;
        /// 前缀后的路径段)。Navigate 不在资产内 = 抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        /// <param name="asset">全案唯一 .inputactions 资产(Story 001 规则一)。</param>
        /// <returns>Navigate 绑重的设备相对路径列表(去重,Ordinal 序)。</returns>
        /// <exception cref="ArgumentNullException">asset 为 null。</exception>
        /// <exception cref="InvalidOperationException">资产内无 Navigate 动作。</exception>
        public static IReadOnlyList<string> GetNavigateControlPaths(InputActionAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset), "Navigate 绑重查询要求注入动作资产(Story 001 规则一)。");

            var navigateAction = FindActionRecursive(asset, "Navigate");
            if (navigateAction == null)
                throw new InvalidOperationException(
                    "输入资产内无 Navigate 动作(AC-3-C2 前提:Navigate 必须在资产内恰一个实例)。");

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in navigateAction.bindings)
            {
                if (b.isComposite && !b.isPartOfComposite) continue; // 复合本体(如 2DVector)无设备前缀,跳过
                var relative = StripLayoutPrefix(b.path);
                if (!string.IsNullOrEmpty(relative))
                    paths.Add(relative);
            }

            return paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>从动作资产提取第二焦点动作候选绑重的**设备相对控制路径**。
        /// 搜索动作名命中 <see cref="KnownFocusActionNames"/> 集合(大小写不敏感)
        /// 且**不是** Navigate 自身的动作。</summary>
        /// <param name="asset">全案唯一 .inputactions 资产。</param>
        /// <param name="secondFocusActionName">第二焦点动作名称(用于报错定位;若资产内无此名称动作 = 空列表,
        /// 非违例 —— 只是该候选不存在)。</param>
        /// <returns>该动作绑重的设备相对路径列表(去重,Ordinal 序)。</returns>
        public static IReadOnlyList<string> GetSecondFocusActionPaths(InputActionAsset asset, string secondFocusActionName)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset), "第二焦点动作绑重查询要求注入动作资产。");
            if (string.IsNullOrEmpty(secondFocusActionName))
                return Array.Empty<string>();

            // 跳过 Navigate 自身(它不在第二焦点动作集合,但防御性排除)
            if (secondFocusActionName.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();

            var action = FindActionRecursive(asset, secondFocusActionName);
            if (action == null)
                return Array.Empty<string>(); // 动作不存在 = 空列表,非违例

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in action.bindings)
            {
                if (b.isComposite && !b.isPartOfComposite) continue;
                var relative = StripLayoutPrefix(b.path);
                if (!string.IsNullOrEmpty(relative))
                    paths.Add(relative);
            }

            return paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        /// <summary>端到端断言:Navigate 绑重与所有已知第二焦点动作绑重的设备相对路径交集为空。
        /// 违例 = 抛 <see cref="InvalidOperationException"/>(带详情:重叠路径 + 冲突动作名)。</summary>
        /// <param name="assetPath">.inputactions 资产文件路径(用于错误消息定位)。</param>
        /// <exception cref="ArgumentNullException">assetPath 为 null/空。</exception>
        /// <exception cref="InvalidOperationException">Navigate 不存在,或重叠路径非空。</exception>
        public static void AssertNavigateExclusive(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentNullException(nameof(assetPath), "AssertNavigateExclusive 要求非空资产路径。");

            if (!System.IO.File.Exists(assetPath))
                throw new InvalidOperationException($"输入资产文件不存在「{assetPath}」—— 无法执行 C2 构造断言。");

            var asset = InputActionAsset.FromJson(System.IO.File.ReadAllText(assetPath));
            try
            {
                AssertNavigateExclusive(asset);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        /// <summary>端到端断言(内存资产版):Navigate 绑重与所有已知第二焦点动作绑重的
        /// 设备相对路径交集为空。违例 = 抛 <see cref="InvalidOperationException"/>。</summary>
        /// <param name="asset">已加载的 InputActionAsset(FromJson 或 Editor 加载)。</param>
        /// <exception cref="ArgumentNullException">asset 为 null。</exception>
        /// <exception cref="InvalidOperationException">Navigate 不存在,或重叠路径非空。</exception>
        public static void AssertNavigateExclusive(InputActionAsset asset)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset), "AssertNavigateExclusive 要求非空动作资产。");

            var navigatePaths = GetNavigateControlPaths(asset);
            var overlap = new List<(string Path, string ActionName)>();

            foreach (var candidateName in KnownFocusActionNames)
            {
                if (candidateName.Equals("Navigate", StringComparison.OrdinalIgnoreCase)) continue;
                var candidatePaths = GetSecondFocusActionPaths(asset, candidateName);
                foreach (var p in candidatePaths)
                {
                    if (navigatePaths.Contains(p))
                        overlap.Add((p, candidateName));
                }
            }

            if (overlap.Count > 0)
            {
                var detail = string.Join("\n  ", overlap.Select(o =>
                    $"[{o.Path}] → 同时喂 Navigate 与 {o.ActionName}"));
                throw new InvalidOperationException(
                    $"[C2] Navigate 绑重与第二焦点动作存在 {overlap.Count} 条重叠物理路径:\n  {detail}\n" +
                    "AC-3-C2:喂 Navigate 的物理控件不得喂任何第二焦点动作(结构性构造断言)。");
            }
        }

        // ── 内部工具──

        /// <summary>递归搜索 InputActionAsset 内指定名称的动作(跨所有 action map)。</summary>
        private static UnityEngine.InputSystem.InputAction FindActionRecursive(InputActionAsset asset, string name)
        {
            foreach (var map in asset.actionMaps)
            {
                var action = map.FindAction(name, throwIfNotFound: false);
                if (action != null) return action;
            }
            return null;
        }

        /// <summary>剥除控制路径的设备布局前缀: <c>&lt;Keyboard&gt;/e</c> → <c>e</c>;
        /// <c>&lt;Gamepad&gt;/dpad/up</c> → <c>dpad/up</c>;无前缀 = 原样返回(非空串)。</summary>
        private static string StripLayoutPrefix(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            // 无设备前缀(如 2DVector / * 掩码) = 不指代具体物理控件,返回空串(调用方跳过)
            if (path[0] != '<') return string.Empty;
            int close = path.IndexOf('>');
            if (close < 0 || close >= path.Length - 1) return string.Empty;
            string layoutCandidate = path.Substring(1, close - 1);
            // 确认是已知设备布局前缀(不是任意 &lt;...&gt; 标签路径)
            if (KnownLayoutPrefixes.Contains(layoutCandidate))
                return path.Substring(close + 1); // 斜杠后的设备相对路径
            // 非已知设备前缀(如 &lt;TestDevice&gt;) = 不参与比较
            return string.Empty;
        }
    }
}
#endif
