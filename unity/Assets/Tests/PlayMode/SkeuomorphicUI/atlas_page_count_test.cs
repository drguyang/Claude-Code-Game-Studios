// S2 · PAGES_MAX spike 实测门(019-b · 2026-10-14)
//
// ▍病因(为什么需要本门)
//   `skeuomorphic-ui.md:1150` 明写 PAGES_MAX「由 spike 定(F6),留白」;既有估值
//   (TA 5–7 页 / AD 3 页)**均未做实际打包测算**,2026-10-04 J 裁明令禁填占位值
//   (占位值 = 第二真源)。AC-42-C9 原文写「构建期页数统计」—— 而 Unity 6.3 的
//   UI Toolkit 图集是**运行期 DynamicAtlas**(PanelSettings.dynamicAtlasSettings),
//   **引擎里没有「构建期页数」这个量** ⇒ 原判据指向的对象不存在。
//
// ▍本门实测确证的引擎结构(2026-10-14 · 反射逐层实证,非文档推测)
//   `PanelSettings` → `panelRenderer`(UIRepaintUpdater)→ `renderTreeManager`
//   (RenderTreeManager)→ `<atlas>k__BackingField`(DynamicAtlas)
//
//   `DynamicAtlas` 的**全部**字段(实测):
//     m_Database(Dictionary<Texture,TextureInfo>)· m_PointPage · m_BilinearPage ·
//     m_ColorSpace · m_Panels · m_MinAtlasSize · m_MaxAtlasSize · m_MaxSubTextureSize ·
//     m_ActiveFilters · m_CustomFilter · textureRegistry
//
//   `DynamicAtlasPage` 的**全部**字段(实测):textureId · atlas · format · filterMode ·
//     minSize · maxSize · m_1Padding · m_2Padding · m_Allocator · m_Blitter ·
//     m_CurrentSize · disposed
//   ⇒ **无页链 / 无页列表**。
//
//   ⚠️ **结论(推翻 GDD F5 的模型假设)**:真模型 = **固定 2 页**
//     (Point 一页 + Bilinear 一页),各带一个分配器,从 `minSize` 长到 `maxSize`。
//     **不存在** F5 假设的「N 页 = ceil(Σ面积 / A_page)」这种可增长的页集合。
//     ⇒ `Pages_frame` 由构造**恒 ≤ 2**;真正的约束量是**单页瓦片容量**。
//
// ▍batch 下的可测边界(18 轮取证 · 2026-10-14)
//   batchmode PlayMode 下**纹理永不注册进图集**(`m_Database` 恒 count=0)——
//   逐层排除:① 非渲染面问题(挂 RenderTexture 目标无效);② 非设备问题
//   (`device=UIRenderDevice` 非 null);③ 非回调禁用问题(强制
//   `EnableRenderingAndInputCallbacks` + `RepaintPanel` + `RenderRootTree` 均无效);
//   ④ 页纹理 `<atlas>k__BackingField` 恒空 ⇒ **需要真实 GPU 绘制 pass**。
//   ⇒ **「实测页数 N」在 batch 下结构性不可测**;本门只断言可确证量。
//   真机/编辑器实测页数 = 后续轮(登记,不阻塞本冻结 —— 容量模型已足以定 PAGES_MAX)。
//
// ▍冻结口径(用户 2026-10-14 裁定 = 实测倒推)
//   冻结值由实测得出,不由任何预估值填充。**禁占位值**。

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>
    /// S2 PAGES_MAX spike:图集容量模型与入集资格实测(019-b)。
    /// </summary>
    internal sealed class atlas_page_count_test
    {
        // ── 定稿贴图清单(18 张 · 2026-10-14 盘库)─────────────────────────────
        // **不走 Resources.Load** —— 定稿贴图不住 Resources/。走文件系统读字节 +
        // Texture2D.LoadImage:① 不依赖 importer 配置;② PNG 原尺寸 = 引擎内尺寸。
        private const string TexRoot = "Gameplay.UI/Skeuomorphic/Textures/";

        private static readonly string[] FiveFamilies =
        {
            "border_paper-final.png",   "border_scroll-final.png",
            "ink_dot-final.png",        "ink_dry-final.png",
            "ink_light-final.png",      "ink_wet-final.png",
            "paper_aged-final.png",     "paper_burnt_edge-final.png",
            "paper_hemp-final.png",     "paper_xuan-final.png",
            "scroll_cap-final.png",     "scroll_knot-final.png",
            "scroll_rod-final.png",     "seal_red-final.png",
            "seal_surface-final.png",   "ui_icons_sprite-final.png",
        };

        private const int RaisedThreshold = 1024;   // 用户 2026-10-14 裁定:抬门槛到 1024 再实测

        private GameObject _go;
        private PanelSettings _panelSettings;
        private UIDocument _doc;

        [TearDown]
        public void TearDown()
        {
            if (_doc != null) Object.Destroy(_doc);
            if (_panelSettings != null) Object.Destroy(_panelSettings);
            if (_go != null) Object.Destroy(_go);
            _doc = null; _panelSettings = null; _go = null;
        }

        /// <summary>内存面板(零落盘)+ 18 张贴图挂满根,等真 panel 就绪。</summary>
        private IEnumerator SetUpWith18Textures()
        {
            _go = new GameObject("AtlasPageCount_UnderTest");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            var das = _panelSettings.dynamicAtlasSettings;
            das.maxSubTextureSize = RaisedThreshold;
            _panelSettings.dynamicAtlasSettings = das;

            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null)
            {
                if (Time.realtimeSinceStartup - t0 > 2f)
                {
                    Assert.Fail("[夹具红] rootVisualElement 2s 未就绪");
                    yield break;
                }
                yield return null;
            }

            var root = _doc.rootVisualElement;
            int attached = 0;
            foreach (string name in AllTextureNames())
            {
                var tex = LoadPng(name);
                if (tex == null) { Debug.Log($"[AtlasPageCount] MISSING|{name}"); continue; }
                var el = new VisualElement { name = "tex-" + name };
                el.style.backgroundImage = new StyleBackground(tex);
                el.style.width = tex.width;
                el.style.height = tex.height;
                root.Add(el);
                attached++;
            }
            Debug.Log($"[AtlasPageCount] ATTACHED|count={attached}|threshold={RaisedThreshold}");
            for (int i = 0; i < 30; i++) yield return null;
        }

        private static IEnumerable<string> AllTextureNames()
        {
            foreach (string n in FiveFamilies) yield return n;
            yield return "Casebook/casebook_paper_base-final.png";
            yield return "Brass/focus_brass_2px-final.png";
        }

        private static Texture2D LoadPng(string relativeName)
        {
            string path = System.IO.Path.Combine(Application.dataPath, TexRoot + relativeName);
            if (!System.IO.File.Exists(path)) return null;
            byte[] bytes;
            try { bytes = System.IO.File.ReadAllBytes(path); }
            catch (System.IO.IOException) { return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            return tex;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ① 主判据:图集容量模型(实测 —— 冻结件的数值真源)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_atlasCapacityModel_measured()
        {
            yield return SetUpWith18Textures();

            var root = _doc.rootVisualElement;
            Assert.IsNotNull(root?.panel, "[夹具红] panel 为 null");

            object atlas = FindAtlas(root.panel);
            Assert.IsNotNull(atlas,
                "[结构红] panel → panelRenderer → renderTreeManager → atlas 反射链断裂" +
                "(见 TRACE 行真字段名)");

            // 实测三旋钮 + 页模型
            int minA = GetIntProp(atlas, "minAtlasSize");
            int maxA = GetIntProp(atlas, "maxAtlasSize");
            int subA = GetIntProp(atlas, "maxSubTextureSize");
            object pointPage = GetField(atlas, "m_PointPage");
            object bilinPage = GetField(atlas, "m_BilinearPage");

            // 每页瓦片数 = (maxAtlasSize / maxSubTextureSize)² —— 单页可容的满尺寸图数
            int perPage = subA > 0 ? (maxA / subA) * (maxA / subA) : -1;

            Debug.Log($"[AtlasPageCount] CAPACITY|minAtlasSize={minA}|maxAtlasSize={maxA}|" +
                      $"maxSubTextureSize={subA}|tilesPerPage={perPage}|" +
                      $"pointPage={(pointPage != null ? "present" : "null")}|" +
                      $"bilinearPage={(bilinPage != null ? "present" : "null")}|" +
                      $"pageModel=Fixed2(Point+Bilinear)");

            // ── 断言(全部为实测可确证量)─────────────────────────────────────
            Assert.AreEqual(2, PageCountOf(atlas),
                "[结构红] 页数 ≠ 2 —— 引擎模型若变(出现页链)则本冻结件失效,须重测");
            Assert.AreEqual(4096, maxA, "[实测红] maxAtlasSize ≠ 4096(实测值,变了须重核)");
            Assert.AreEqual(64, minA, "[实测红] minAtlasSize ≠ 64(实测值,变了须重核)");
            Assert.AreEqual(RaisedThreshold, subA,
                "[实测红] maxSubTextureSize ≠ 抬升后的 1024 —— 门槛未生效");
            Assert.AreEqual(16, perPage,
                "[实测红] 每页瓦片数 ≠ 16 —— 4096²/1024² 的容量模型变了");
        }

        /// <summary>
        /// 页数 = Point + Bilinear 两页非空计数(引擎固定 2 页模型)。
        /// 注:这不是「已分配页」,是「页槽位」—— DynamicAtlasPage 无页链(实测字段表)。
        /// </summary>
        private static int PageCountOf(object atlas)
        {
            int n = 0;
            if (GetField(atlas, "m_PointPage") != null) n++;
            if (GetField(atlas, "m_BilinearPage") != null) n++;
            return n;
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 入图集资格分布(门槛 1024)
        // ══════════════════════════════════════════════════════════════════════
        [UnityTest]
        public IEnumerator test_raisedThreshold1024_eligibilityDistribution()
        {
            yield return SetUpWith18Textures();

            int eligible = 0, oversized = 0, total = 0;
            var sb = new System.Text.StringBuilder();
            foreach (string name in AllTextureNames())
            {
                var tex = LoadPng(name);
                if (tex == null) { sb.Append($"{name}=LOAD_NULL;"); continue; }
                total++;
                int maxDim = Mathf.Max(tex.width, tex.height);
                bool ok = maxDim <= RaisedThreshold;
                if (ok) eligible++; else oversized++;
                sb.Append($"{name}={tex.width}x{tex.height}:{(ok ? "in" : "OUT")};");
            }

            Debug.Log($"[AtlasPageCount] ELIGIBILITY|threshold={RaisedThreshold}|" +
                      $"total={total}|eligible={eligible}|oversized={oversized}|{sb}");

            // 实测口径(2026-10-14 重生轮更新 —— 超规 2 张已重生 1024²,AC-42-C9 残留结清):
            // 合格 18 = 五族 16 张(含 ui_icons_sprite 1024² 重生版)
            //          + casebook_paper_base 1024²(频域补偿降采样,纤维 std 7.59 保持)
            //          + brass 64²(用户明裁最小豁免,art-bible §7.4);
            // 超规 0(2026-10-14 之前 = 2:ui_icons_sprite / casebook_paper_base 均 2048²)。
            Assert.AreEqual(18, total, "[夹具红] 18 张应全部可加载");
            Assert.AreEqual(18, eligible,
                "[资格红] 合格数 ≠ 18 —— 有贴图超 1024² 回归或清单变更,须重核");
            Assert.AreEqual(0, oversized,
                "[资格红] 超规数 ≠ 0 —— 有贴图回退到 >1024²(AC-42-C9 门已冻结,不抬门槛)");
        }

        // ── 反射链(字段名由 2026-10-14 实测纠正,保留候选名以自愈)─────────────
        private static object FindAtlas(object panel)
        {
            foreach (string fieldName in new[]
            { "panelRenderer", "m_PanelRenderer", "renderer", "m_Renderer" })
            {
                object renderer = GetField(panel, fieldName);
                if (renderer == null) continue;

                foreach (string aName in new[] { "renderTreeManager", "m_Atlas", "atlas" })
                {
                    object val = GetField(renderer, aName);
                    if (val == null) continue;
                    if (aName == "renderTreeManager")
                    {
                        var atlasProp = val.GetType().GetProperty("atlas",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        object at = atlasProp?.GetValue(val);
                        if (at != null) return at;
                    }
                    else return val;
                }
            }
            Debug.Log("[AtlasPageCount] TRACE|atlasNotFound");
            return null;
        }

        private static object GetField(object o, string name)
        {
            var f = o?.GetType().GetField(name, BindingFlags.NonPublic |
                BindingFlags.Public | BindingFlags.Instance);
            if (f == null) return null;
            try { return f.GetValue(o); } catch { return null; }
        }

        private static int GetIntProp(object o, string name)
        {
            var p = o?.GetType().GetProperty(name, BindingFlags.NonPublic |
                BindingFlags.Public | BindingFlags.Instance);
            if (p == null) return -1;
            try { return (int)p.GetValue(o); } catch { return -1; }
        }
    }
}
