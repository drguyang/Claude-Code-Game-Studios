// 模态开合 —— **视觉走查**夹具(42 · 2026-11-04 · `TD-skeuo-03` 结案轮)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:**零读盘**。存档位行由夹具内的 `SaveSlotView` 直接构造(不读真实存档目录 ——
//    本件判的是**屏**长什么样,不是「你的档在不在」)。
// 写:**仅图形模式写截图** ⇒ `unity/Logs/shots/`(gitignored 指定暂存区)。具名:
//    `modal_00_world.png` · `modal_01_pause.png` · `modal_02_saveslots.png` ·
//    `modal_03_backtopause.png` · `modal_04_world_restored.png`。
//    ⚠️ **batch 结构性零写盘**:`ScreenCapture` 依赖 game view backbuffer,
//    `WaitForEndOfFrame` 在无渲染帧的 batch 下永不触发(2026-11-01 实测);
//    故截图路径全程由 `Application.isBatchMode` 守卫。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么要有这一件(与机械判据的分工)
// ─────────────────────────────────────────────────────────────────────────────
// `modal_toggle_closure_test`(EditMode)证明**判据**对了:闭集 9 员都走「有模态 ⇒ 关」
// 这一条规则。`modal_focus_wiring_test`(PlayMode)证明**焦点**回到了进入前载体。
// 两件都是数值判据 —— 它们证明不了「关掉存档册之后,玩家眼前**看起来**回到了暂停壳」
// 而不是「屏变了但画面还是白的/空的」。
// ⇒ 本件把那条链**走一遍并留下五帧**,供人(或视觉模型)判读。
//
// ⚠️ **本件不断言观感**(那是 `[L]` 人工面);它只断言**链本身跑通了**
//    (`CurrentModal` 每一步如实),否则截出来的图无意义。

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>打开 / 关掉模态的那条链 —— 图形模式留五帧(供人看)。</summary>
    [TestFixture]
    public sealed class ModalToggleVisualWalkthrough
    {
        private const float RootWaitSeconds = 5f;

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private bool _panelSettingsOwned;
        private GameObject _cameraGo;
        private Camera _camera;
        private RenderTexture _target;
        private SkeuoRuntimeDriver _driver;
        private ModalInputDriver _input;
        private bool _held;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _go = new GameObject("ModalToggleWalkthrough");
            // 🔴 **2026-11-04 首版踩的坑**:裸 `CreateInstance<PanelSettings>()` 没有 theme、
            //    也没有任何样式 ⇒ 面板画出来是**纯黑**(五帧哈希完全相同,首轮实测)。
            //    必须用**生产面板资产** + 九份生产 USS,否则「看起来对不对」根本无从判读。
            // ⚠️ 同 `casebook_reading_inline_test` 的守卫:`AssetDatabase` 在 player 里不存在
            //    (CS0234),整段与调用点都要 `#if UNITY_EDITOR`。
#if UNITY_EDITOR
            _panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
#endif
            if (_panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettingsOwned = true;
            }

            // 🔴 **2026-11-04 第三处坑(又是看图才发现的)**:生产面板是 `ScaleWithScreenSize`
            //    + 参考分辨率 1920,而本夹具的 Game 视图只有 593×429 CSS ⇒ 整块 UI 被缩到 0.31 倍,
            //    15px 的行字落到屏上只剩 ~5px —— 放大看是一团糊块,据此判「字发虚」是**错判**。
            //    (暂停壳的字看着还行,只是因为它吃的是主题 28px 默认字号。)
            //    ⇒ 走查用**运行时副本**改成 `ConstantPixelSize` + scale 1,让形态按 1:1 呈现。
            //    ⚠️ 必须 `Instantiate` 一份副本:直接改生产资产会被存盘污染(它在 `Assets/` 下)。
            if (!_panelSettingsOwned)
            {
                _panelSettings = UnityEngine.Object.Instantiate(_panelSettings);
                _panelSettingsOwned = true;
            }
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = 1f;

            // 🔴 **2026-11-04 第四处坑(承上一条)**:即使 1:1,Game 视图只有 593×429 ——
            //    比生产参考分辨率 1920×1080 窄三倍多。用这个窗口判「纸页够不够大」「行会不会
            //    压到框线」会得出**与生产无关**的结论(实测:6 行 + 页脚在 429px 高下必然溢出)。
            //    ⇒ 把面板直接渲到 1920×1080 的 RenderTexture,再 ReadPixels 存盘:
            //    判的就是生产比例下的形态,且不再依赖 Game 视图尺寸 / `ScreenCapture`。
            _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _panelSettings.targetTexture = _target;

            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _go.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > RootWaitSeconds)
                {
                    Assert.Fail("[夹具红] 面板 5s 未就绪 —— panelSettings 未生效");
                    yield break;
                }
                yield return null;
            }

            AttachProductionStyleSheets(_doc.rootVisualElement);

            // 🔴 **2026-11-04 第二处坑(看图才发现的)**:场景里**没有相机** ⇒ 每帧没人清屏
            //    ⇒ UI Toolkit 的绘制**逐帧累积**在 backbuffer 上 ⇒ 截出来是「上一屏 + 这一屏」
            //    的重影(首轮 `modal_03` 上同时看到暂停卡片与存档册的横线,而夹具断言
            //    `CurrentModal == Pause` 是绿的 —— 逻辑对、图会骗人)。
            //    加一台清屏相机,图才读得出真实层叠关系。
            _cameraGo = new GameObject("WalkthroughCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            _camera.cullingMask = 0;   // 不画任何物体:本件只看 UI
            // 🔴 **2026-11-04 补**:上面的「清屏相机」当初**没指向 RenderTexture** ⇒ 它清的是
            //    Game 视图,RT 从没被清过 ⇒ UI 绘制逐帧累积。本文件首轮的五帧看着还行只是
            //    因为每屏占同一块区域、后画的盖住先画的;换页 / 换屏尺寸一变就露馅
            //    (新走查件实测到「两张册页卡叠在一起」)。相机与面板必须**同一个目标**。
            // ⚠️ batch 下没有图形设备:相机渲 RT 会打 `RenderTexture.Create failed` 并使整条变红。
            if (!Application.isBatchMode) _camera.targetTexture = _target;

            // 存档位给足行数(10 行 ⇒ 2 页),让「册页 / 页缘 / 页码」在图上可见。
            // ⚠️ 首轮我喂的是 `SaveSlotView.Unbound` —— 那是**行字空白**的形态
            //    (42 不知道这一槽有没有档时,纸线格完整但一个字都不写),截出来全是空行,
            //    看图只会得出「屏坏了」的错结论。要判读就喂**有档**的视图。
            var views = new List<SaveSlotView>();
            for (uint seq = 1; seq <= 10; seq++)
            {
                // 槽 1 = 退出保存 / 槽 2 = 自动存档 / 3+ = 手动(与 TD-persist-17 的槽位身份一致)
                views.Add(new SaveSlotView(seq, known: true, hasSave: true,
                                           tick: 36000L * seq, dayNumber: seq, isNight: seq % 2 == 1));
            }
            IReadOnlyList<SaveSlotView> fixedViews = views;
            _driver.SaveSlotViewProvider = () => fixedViews;

            // 走**真**开合链路(与生产同一条):toggle → ModalInputDriver → DriverModalOpener。
            _held = false;
            _input = new ModalInputDriver(() => _held, new DriverModalOpener(_driver));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_cameraGo != null) UnityEngine.Object.Destroy(_cameraGo);
            if (_target != null) { _target.Release(); UnityEngine.Object.Destroy(_target); }
            // ⚠️ 只销毁**自己造的**面板设置:从 `AssetDatabase` 取到的是**生产资产**,
            //    `Destroy` 它虽被 Unity 拒绝(「Destroying assets is not permitted」),
            //    但会打红字并污染日志 —— 那不是本夹具该有的输出。
            if (_panelSettingsOwned && _panelSettings != null)
                UnityEngine.Object.Destroy(_panelSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator walkthrough_openPause_thenSaveSlots_thenToggleBack()
        {
            // ⓪ 世界:先让一个元素获焦(它是「进入前载体」—— 关到底时焦点要回到它)
            var carrier = new VisualElement { name = "world-carrier" };
            carrier.focusable = true;
            carrier.style.width = 120;
            carrier.style.height = 40;
            _doc.rootVisualElement.Add(carrier);
            carrier.Focus();
            yield return null;

            Assert.That(Focused(), Is.SameAs(carrier), "[前置] 世界载体须真的获焦");
            yield return Shot("modal_00_world");

            // ① 按 toggle ⇒ 暂停壳
            yield return Press();
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause), "① 第一次按 ⇒ 暂停壳");
            yield return Shot("modal_01_pause");

            // ② 从暂停壳开存档位(生产是壳里的「存档位」行 ⇒ 换屏,同一条 OpenModal)
            Assert.That(_driver.OpenModal(ModalId.SaveSlots), Is.True);
            yield return null;
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.SaveSlots), "② 册已摊开");
            DumpTree(_doc.rootVisualElement, 0);
            yield return Shot("modal_02_saveslots");

            // ③ 按 toggle ⇒ **回暂停壳**(不是回世界、也不是什么都不发生)
            yield return Press();
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.Pause),
                "③ 关册 ⇒ 回上一层(暂停壳);旧实现会把它「换屏」成暂停壳 —— " +
                "结果看着一样,但那是**必然**而不是**回栈**,两者在别的屏上会分叉");
            yield return Shot("modal_03_backtopause");

            // ④ 再按 ⇒ 回世界,且焦点回到进入前载体
            yield return Press();
            Assert.That(_driver.CurrentModal, Is.EqualTo(ModalId.None), "④ 再按 ⇒ 世界");
            Assert.That(Focused(), Is.SameAs(carrier),
                "④ 焦点须回**进入前载体**(P-02)—— 图上看就是那个元素的焦点环回来了");
            yield return Shot("modal_04_world_restored");
        }

        // ── 装置 ──────────────────────────────────────────────────────────

        /// <summary>九份生产拟物样式表(与 `Boot.unity` 预摆里拖的那一组同名同序)。</summary>
        private static readonly string[] StyleSheetPaths =
        {
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoMarks.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoImplement.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss",
        };

        /// <summary>把生产样式表挂到面板根(编辑器腿;player 里由 `#if` 排除)。</summary>
        /// <remarks>⚠️ 方法体也要 `#if` —— 只守调用点不够:`UnityEditor` 在 player 里不存在,
        /// 未守卫的方法体照样 CS0234(`casebook_reading_inline_test` 同一坑)。</remarks>
        private static void AttachProductionStyleSheets(VisualElement panelRoot)
        {
#if UNITY_EDITOR
            foreach (string path in StyleSheetPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !panelRoot.styleSheets.Contains(sheet))
                    panelRoot.styleSheets.Add(sheet);
            }
#endif
        }

        private VisualElement Focused()
            => _doc.rootVisualElement.panel.focusController.focusedElement as VisualElement;

        /// <summary>
        /// 把树与**解析后**的样式打出来(常驻诊断)。
        /// <para>⚠️ **为什么要留**:截图只能看出「颜色不对」,**看不出为什么**。2026-11-04 就是靠这行
        /// 打印 `backgroundImage` 的真值(`img=d_console.warnicon` 而不是某张纸纹)才定位到
        /// 「USS 的 `url("guid:…")` 恒不解析」这个根因 —— 光看图只会得出「水印 / 焦点环坏了」。
        /// 成本 = 每次走查 ~21 行日志,收益 = 下次形态异常时能一眼看到解析后的真值。</para>
        /// </summary>
        private static void DumpTree(VisualElement el, int depth)
        {
            var rs = el.resolvedStyle;
            string cls = el.ClassListContains("paper") ? "paper" : string.Join(".", el.GetClasses());
            var bi = rs.backgroundImage;
            string img = bi.texture != null ? bi.texture.name
                       : bi.sprite != null ? "sprite:" + bi.sprite.name
                       : bi.renderTexture != null ? "rt" : "none";
            Debug.Log($"[TREE] {new string(' ', depth * 2)}{(string.IsNullOrEmpty(el.name) ? "(anon)" : el.name)}"
                      + $" [{cls}] bound={el.worldBound} bg={rs.backgroundColor} img={img}"
                      + $" borderTop={rs.borderTopColor}({rs.borderTopWidth})"
                      + $" slice=({rs.unitySliceLeft},{rs.unitySliceTop},{rs.unitySliceRight},{rs.unitySliceBottom})");
            for (int i = 0; i < el.hierarchy.childCount; i++) DumpTree(el.hierarchy[i], depth + 1);
        }

        private IEnumerator Press()
        {
            _held = true;
            _input.OnFrame();
            yield return null;
            _held = false;
            _input.OnFrame();
            yield return null;
        }

        private IEnumerator Shot(string name)
        {
            if (Application.isBatchMode) yield break;   // batch 结构性无帧 ⇒ 不请求
            yield return new WaitForEndOfFrame();       // 让本帧的 UI 绘制落进 RT
            yield return null;

            string dir = Path.Combine(Application.dataPath, "..", "Logs", "shots");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".png");

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            Debug.Log($"[MODAL] 已存帧 {_target.width}x{_target.height} ⇒ {path}");
        }
    }
}
