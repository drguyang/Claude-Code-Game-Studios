// S2 · PAGES_MAX spike 探针(019-b · sprint-09 · 2026-10-14)
//
// 判据原文(story-019-texture-binding.md:216 · AC-42-C9):
//   实测**图集页数 ≤ PAGES_MAX**;超限 => 构建期冲突并给出溢出告警
//
// ⚠️ 用户裁定(2026-10-14)= **实测倒推**:真打包 ⇒ 实测页数 N ⇒ PAGES_MAX = N + 余量。
//    本探针**只报实测值,不预置、不写入、不改生产打包路径**(禁填占位值,2026-10-04 J 裁)。
//
// ▍第一个结构性发现(静态扫描 UIElementsModule.dll 所得,须由本探针实测坐实或推翻)
//   Unity 6.3 的 UI Toolkit 图集 = **运行期 DynamicAtlas**(PanelSettings.m_DynamicAtlasSettings
//   含 min/maxAtlasSize + customFilter),**不是构建期静态打包**。若坐实,则 AC-42-C9 原文
//   的「构建期页数统计」与引擎实际机制**不是一回事** —— PAGES_MAX 的语义须先行裁定
//   (这是 sprint-09 立项时登记的待裁项)。本探针的设计即为此:先把「页数到底可观不可观、
//   在哪一层可观、溢出如何告警」三件事测出来,再谈冻结哪个数。
//
// ▍三问判据(可证伪)
//   Q1 可观测性:运行期能否读到图集页数 / atlas size / 溢出事件?(能 → 记路径;不能 → 记不可达)
//   Q2 页数 N:18 张贴图(16 张五族 + Brass 焦点环 + casebook_paper_base;15×1024² +
//      2×2048² + 1×64² = 24.1 MP)真打进去 ⇒ 实测页数 N = ?
//   Q3 溢出告警:超 maxAtlasSize 时引擎是 throw / warn / 静默降级?(决定 C9 的「告警」面怎么写)
//
// ⚠️ 引擎面:UI Toolkit 图集行为 post-cutoff(ADR-013 Knowledge Risk HIGH,§6.6 假设 6
//    spike 未跑)⇒ 本探针的实测输出**同时**是 ADR-013 §6.6 假设 6 的结清输入。

#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace DaYiJingCheng.EditorTools.Spike
{
    /// <summary>图集页数实测探针(S2 · 019-b)。只读,零写入,零生产路径改动。</summary>
    public static class AtlasPageProbe
    {
        private const string LogTag = "[AtlasPageProbe]";

        /// <summary>菜单入口(人工跑)与 batch 入口(”——executeMethod“)共用。</summary>
        [MenuItem("大医精诚/Spike/Probe Atlas Pages (019-b)")]
        public static void Run()

        /// <summary>
        /// 编辑器内取样入口(S2 · 用户裁定 2026-10-14「数据最全」路径)。
        /// <para>batch 模式下 <c>UIDocument.panel</c> 恒 null ⇒ 页数 / 溢出告警读不到
        /// (probe3 实测 <c>PAGE_COUNT|status=PANEL_NULL</c>)。本方法改走**编辑器渲染面**:
        /// 建临时场景 + UIDocument(挂抬门槛后的内存面板 + 18 张图),用户在编辑器里 Play,
        /// 由运行期组件在渲染真实发生后回读页数并打 Console。</para>
        /// <para>⚠️ 临时资产全部以 <c>AtlasProbe*</c> 开头,已由 .gitignore
        /// <c>unity/Assets/Scenes/AtlasProbe*</c> 拦下,永不提交;生产 PanelSettings 零改动
        /// (内存 clone 抬门槛)。</para>
        /// </summary>
        [MenuItem("大医精诚/Spike/Setup Atlas Page Probe (019-b · 编辑器取样)")]
        public static void SetupEditorSample()
        {
            const string ScenePath = "Assets/Scenes/AtlasProbeSample.unity";
            const int Raised = 1024;

            var src = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
            if (src == null)
            {
                Debug.LogError($"{LogTag} 生产 PanelSettings 缺失");
                return;
            }

            // 内存 clone 抬门槛(零落盘 —— 这是资产文件,不是场景内实例;
            // 真正落盘的是一份 PanelSettings 副本资产,路径 AtlasProbe* 已被 gitignore)
            var probePanel = UnityEngine.Object.Instantiate(src);
            var das = probePanel.dynamicAtlasSettings;
            das.maxSubTextureSize = Raised;
            probePanel.dynamicAtlasSettings = das;
            string panelPath = "Assets/Scenes/AtlasProbe_Panel.asset";
            AssetDatabase.CreateAsset(probePanel, panelPath);

            // 临时场景 + UIDocument + 回读组件
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var go = new GameObject("AtlasProbeDoc");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);

            // 18 张挂面板根
            var root = doc.rootVisualElement;
            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D",
                new[] { "Assets/Gameplay.UI/Skeuomorphic/Textures" }))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (tex == null) continue;
                var el = new VisualElement();
                el.style.backgroundImage = new StyleBackground(tex);
                el.style.width = tex.width;
                el.style.height = tex.height;
                root.Add(el);
                added++;
            }

            // 回读者:真渲染后(约 1s)读页数打 Console
            go.AddComponent<AtlasPageReader>();

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
                scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LogTag} EDITOR_SAMPLE_READY|scene={ScenePath}|panel={panelPath}|" +
                      $"raised={Raised}|elements={added}|" +
                      $"NEXT=Play 一次,页数将由 AtlasPageReader 打 Console");
        }

        /// <summary>拆入口(原 Run 主体留给 batch 用;菜单只指向它)。</summary>
        public static void RunBatch() => Run();
        {
            Debug.Log($"{LogTag} === S2 PAGES_MAX spike 起 ===");

            ProbePanelSettings();
            ProbeDynamicAtlasApi();
            ProbeTextureInventory();
            ProbeSubTextureEligibility();   // 第二问:改冻「入图集资格」后的实测面
            ProbeRaisedThreshold1024();     // 第三问:抬门槛到 1024 后的页数实测

            Debug.Log($"{LogTag} === 终 ===");
        }

        /// <summary>Q1 前半:PanelSettings 的图集设置面(静态配置,必可读)。</summary>
        private static void ProbePanelSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:PanelSettings");
            Debug.Log($"{LogTag} PanelSettings 资产数 = {guids.Length}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
                if (panel == null) continue;

                // DynamicAtlasSettings 是 public struct;直接读(零反射)
                var das = panel.dynamicAtlasSettings;
                Debug.Log($"{LogTag} PANEL_SETTINGS|path={path}|min={das.minAtlasSize}|" +
                          $"max={das.maxAtlasSize}|activeWhen={GetActiveWhen(das)}");
            }
        }

        private static string GetActiveWhen(DynamicAtlasSettings das)
        {
            // 该位枚举名随版本可能变;反射取值但**不假设枚举成员名** —— 取不到就报 unknown
            try
            {
                Type t = typeof(DynamicAtlasSettings);
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.Name.Contains("ActiveWhen") || p.Name.Contains("activeWhen"))
                        return $"{p.Name}={p.GetValue(das)}";
                }
                return "unknown(no ActiveWhen property)";
            }
            catch (Exception e)
            {
                return $"reflect-fail:{e.GetType().Name}";
            }
        }

        /// <summary>Q1 后半:运行期动态图集的**内部**页数 / 溢出是否可达(反射探路,失败即记不可达)。</summary>
        private static void ProbeDynamicAtlasApi()
        {
            // 已知(静态扫描 dll 所得)相关类型名候选;逐一探是否存在、有哪些页数/尺寸成员
            string[] candidates =
            {
                "UnityEngine.UIElements.DynamicAtlasCore",
                "UnityEngine.UIElements.DynamicAtlasPage",
                "UnityEngine.UIElements.RenderTreeAtlas",
                "UnityEngine.UIElements.UIR.AtlasAllocator",
            };

            foreach (string name in candidates)
            {
                Type t = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(name);
                    if (t != null) break;
                }
                if (t == null)
                {
                    Debug.Log($"{LogTag} TYPE_ABSENT|{name}");
                    continue;
                }

                var members = new System.Text.StringBuilder();
                foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                                      BindingFlags.Instance | BindingFlags.Static))
                {
                    string n = m.Name.ToLowerInvariant();
                    if (n.Contains("page") || n.Contains("atlas") || n.Contains("alloc"))
                        members.Append(m.Name).Append("|");
                }
                Debug.Log($"{LogTag} TYPE_PRESENT|{name}|members={members}");
            }

            // 引擎是否提供官方溢出告警面(spike 的核心未知之一)
            Type settingsType = typeof(DynamicAtlasSettings);
            Debug.Log($"{LogTag} DynamicAtlasSettings 公开属性 = " +
                      string.Join(",", Array.ConvertAll(
                          settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance),
                          p => p.Name)));
        }

        /// <summary>Q2 输入真值:18 张贴图的实际像素量(页数由引擎 allocator 决定,此处报输入规模)。</summary>
        private static void ProbeTextureInventory()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D",
                new[] { "Assets/Gameplay.UI/Skeuomorphic/Textures" });

            long totalPixels = 0;
            int count = 0;
            var big = new System.Text.StringBuilder();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                count++;
                totalPixels += (long)tex.width * tex.height;
                if (tex.width > 1024 || tex.height > 1024)
                    big.Append($"{tex.width}x{tex.height}:{System.IO.Path.GetFileName(path)}|");
            }

            Debug.Log($"{LogTag} TEXTURE_INVENTORY|count={count}|totalPixels={totalPixels}|" +
                      $"totalMP={totalPixels / 1e6:F2}|oversize1024={big}");

            // 理论下界:RGBA8 未压缩字节数(4 B/px)—— 用于与 maxAtlasSize 反推的页数下界对照
            long bytes = totalPixels * 4L;
            Debug.Log($"{LogTag} BYTES_RGBA8={bytes}({bytes / 1024 / 1024} MB,未压缩下界)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 第二问(用户裁定 2026-10-14):改冻「入图集资格」
        //
        // 第一问坐实:Unity 6.3 图集 = 运行期 DynamicAtlas,**引擎里没有「构建期页数」这个量**
        //   ⇒ AC-42-C9 原文「构建期页数统计」指向的对象不存在,「页数上限」语义落空。
        // 裁定后 TR-skeuoui-011 第②半的语义改为「可入图集的单图上限」——
        //   冻结对象 = **哪些图进得去图集**(门槛 = DynamicAtlasSettings.maxSubTextureSize)。
        //
        // 本方法实测:① maxSubTextureSize 真实值;② 18 张逐张的入图集资格(过/不过);
        //   ③ 不过的图怎么办(独立贴图 / 须降档)—— 这才是可判的冻结面。
        // ⚠️ 只报实测,不写入、不改生产路径(禁占位值,2026-10-04 J 裁)。
        // ══════════════════════════════════════════════════════════════════════
        private static void ProbeSubTextureEligibility()
        {
            // ① 门槛真值(两份 PanelSettings 各读一次,防不同面板不同值)
            string[] panelGuids = AssetDatabase.FindAssets("t:PanelSettings");
            foreach (string guid in panelGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
                if (panel == null) continue;

                var das = panel.dynamicAtlasSettings;
                Debug.Log($"{LogTag} THRESHOLD|panel={path}|maxSubTextureSize={das.maxSubTextureSize}|" +
                          $"minAtlasSize={das.minAtlasSize}|maxAtlasSize={das.maxAtlasSize}");
            }

            // ② 逐张资格判定(判据 = 引擎面能读到的 maxSubTextureSize;取首个面板的门槛)
            var first = AssetDatabase.LoadAssetAtPath<PanelSettings>(
                AssetDatabase.GUIDToAssetPath(panelGuids[0]));
            if (first == null) return;
            int limit = first.dynamicAtlasSettings.maxSubTextureSize;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D",
                new[] { "Assets/Gameplay.UI/Skeuomorphic/Textures" });

            int eligible = 0, ineligible = 0;
            var over = new System.Text.StringBuilder();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                // 资格判据:两维均 ≤ 门槛 ⇒ 可入图集(超过 ⇒ 引擎走独立贴图,不进 atlas)
                bool ok = tex.width <= limit && tex.height <= limit;
                if (ok) eligible++;
                else
                {
                    ineligible++;
                    over.Append($"{tex.width}x{tex.height}:{System.IO.Path.GetFileName(path)}|");
                }
            }

            Debug.Log($"{LogTag} ELIGIBILITY|limit={limit}|eligible={eligible}|ineligible={ineligible}");
            Debug.Log($"{LogTag} INELIGIBLE_LIST={over}");
            Debug.Log($"{LogTag} FROZEN_CANDIDATE|可入图集单图上限=maxSubTextureSize={limit}" +
                      $"|须走独立贴图={ineligible} 张");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 第三问(用户裁定 2026-10-14 = 抬门槛到 1024 再实测)
        //
        // 第二问实测 maxSubTextureSize = **64**(当前配置值)⇒ 18 张里 17 张不合格。
        // 但 64 是**当前配置**,不是打包实测 —— 全案纸纹/墨迹都是 1024²,门槛 64 等于
        // 图集只装 64² 焦点环,16 张五族切图全走独立贴图 ⇒ 图集收益(调用批次)为零。
        // 用户裁定:抬门槛到 1024(覆盖 15 张 1024²;2048² 两张仍独立),实测此时页数 N。
        //
        // ⚠️ 生产 PanelSettings **一个字不改** —— 本方法在内存里 clone 一份抬门槛,
        //    零落盘;页面计数走运行期 DynamicAtlasCore 反射读真值。
        // ⚠️ 只报实测,不写入、不改生产路径(禁占位值,2026-10-04 J 裁)。
        // ══════════════════════════════════════════════════════════════════════
        private static void ProbeRaisedThreshold1024()
        {
            const int Raised = 1024;

            string srcPath = "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset";
            var src = AssetDatabase.LoadAssetAtPath<PanelSettings>(srcPath);
            if (src == null)
            {
                Debug.LogError($"{LogTag} 生产 PanelSettings 缺失:{srcPath}");
                return;
            }

            // 内存 clone(零落盘):Object.Instantiate 出 ScriptableObject 副本后改门槛
            var probe = UnityEngine.Object.Instantiate(src);
            var das = probe.dynamicAtlasSettings;
            das.maxSubTextureSize = Raised;      // 只改内存副本
            probe.dynamicAtlasSettings = das;

            Debug.Log($"{LogTag} RAISED|src={srcPath}|newMaxSubTextureSize={Raised}|" +
                      $"minAtlasSize={das.minAtlasSize}|maxAtlasSize={das.maxAtlasSize}");

            // 建 UIDocument 真跑一帧 —— 引擎分配器只在实际渲染时分配页
            var go = new GameObject("AtlasProbeDoc");
            try
            {
                var doc = go.AddComponent<UIDocument>();
                doc.panelSettings = probe;

                // 把 18 张贴图全挂进面板根(引擎按可见性/实际使用决定是否入图集)
                var guids = AssetDatabase.FindAssets("t:Texture2D",
                    new[] { "Assets/Gameplay.UI/Skeuomorphic/Textures" });

                var root = doc.rootVisualElement;
                int added = 0;
                foreach (string guid in guids)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (tex == null) continue;

                    var el = new VisualElement();
                    el.style.backgroundImage = new StyleBackground(tex);
                    el.style.width = tex.width;
                    el.style.height = tex.height;
                    root.Add(el);
                    added++;
                }

                Debug.Log($"{LogTag} RENDER_SETUP|panelAttached={probe != null}|elementsAdded={added}");

                // ⚠️ batch 模式下 UIDocument 不自动渲染 —— 手动触发一次布局 + 一帧
                //    (EditorApplication.QueuePlayerLoopUpdate 在 batch 不可用;
                //     改用 UpdateAllViews 强制刷新,再读页数)
                TryRefreshAndCount(doc, Raised);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>反射读运行期 DynamicAtlasCore 的页数真值(拿不到就报不可达,不猜)。</summary>
        private static void TryRefreshAndCount(UIDocument doc, int raised)
        {
            try
            {
                // 面板 → panelDebug? 已知可达链:doc.panel(VisualElement.panel) →
                //   内部 m_Atlas(DynamicAtlasCore)→ m_Allocator → 页集合
                var panelProp = typeof(UIDocument).GetProperty("panel",
                    BindingFlags.Public | BindingFlags.Instance);
                object panel = panelProp?.GetValue(doc, null);

                if (panel == null)
                {
                    Debug.Log($"{LogTag} PAGE_COUNT|status=PANEL_NULL(batch 下 UIDocument 可能未 attach)" +
                              $"|raised={raised}");
                    return;
                }

                // 面板内部 atlas 字段(动态图集在 UIR 侧的 Renderer)
                var rendererField = panel.GetType().GetField("m_Renderer",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (rendererField == null)
                {
                    Debug.Log($"{LogTag} PAGE_COUNT|status=NO_RENDERER_FIELD|" +
                              $"panelType={panel.GetType().FullName}|raised={raised}");
                    return;
                }

                object renderer = rendererField.GetValue(panel);
                if (renderer == null)
                {
                    Debug.Log($"{LogTag} PAGE_COUNT|status=RENDERER_NULL(需真渲染一帧)|raised={raised}");
                    return;
                }

                var atlasField = renderer.GetType().GetField("m_Atlas",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (atlasField == null)
                {
                    Debug.Log($"{LogTag} PAGE_COUNT|status=NO_ATLAS_FIELD|" +
                              $"rendererType={renderer.GetType().FullName}|raised={raised}");
                    return;
                }

                object atlas = atlasField.GetValue(renderer);
                if (atlas == null)
                {
                    Debug.Log($"{LogTag} PAGE_COUNT|status=ATLAS_NULL|raised={raised}");
                    return;
                }

                // DynamicAtlasCore 的页集合(页 = 分配器分配出来的 AtlasPage 实例)
                var pagesField = atlas.GetType().GetField("m_Pages",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (pagesField == null)
                {
                    // 找不到 m_Pages 就列出候选成员名(便于下一轮按实际名读)
                    var names = new System.Text.StringBuilder();
                    foreach (FieldInfo f in atlas.GetType().GetFields(
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                        names.Append(f.Name).Append("|");
                    Debug.Log($"{LogTag} PAGE_COUNT|status=NO_PAGES_FIELD|" +
                              $"atlasType={atlas.GetType().FullName}|fields={names}|raised={raised}");
                    return;
                }

                var pages = pagesField.GetValue(atlas) as System.Collections.ICollection;
                Debug.Log($"{LogTag} PAGE_COUNT|raised={raised}|atlasType={atlas.GetType().FullName}|" +
                          $"pageCount={pages?.Count ?? -1}|maxAtlasSize=" +
                          $"{atlas.GetType().GetField("m_MaxAtlasSize", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(atlas)}");
            }
            catch (Exception e)
            {
                Debug.Log($"{LogTag} PAGE_COUNT|status=EXCEPTION|type={e.GetType().Name}|msg={e.Message}" +
                          $"|raised={raised}");
            }
        }
    }
}
#endif
