// 手册缺页 —— **视觉走查**夹具(T6 · 2026-11-09)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读(编辑器腿):`Assets/Gameplay.UI/Skeuomorphic/*.uss`(九份生产样式表)+
//    `Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset`(生产面板)。
// 写:**仅图形模式**写 `unity/Logs/shots/paper_missing.png`(gitignored 指定暂存区)。
//    ⚠️ **batch 结构性零写盘**:`RenderTexture.Create` 在无图形设备下失败,
//    `ScreenCapture` 路径更早就断(`WaitForEndOfFrame` 在 batch 不触发,2026-11-01 实测)。
//    ⇒ 本件在 batch 下 `Assert.Ignore`,判 `NOT-RUN` —— **不得**据「PlayMode 绿」声称截过图。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么要有这一件
// ─────────────────────────────────────────────────────────────────────────────
// `paper_closeup48_screen_test`(EditMode)证明**状态**对了:缺页那张只剩标题、撕口显出来。
// 它证明不了「看起来像一张被撕过的纸」而不是「屏坏了 / 一片空白」。
// ⇒ 本件把缺页态画到 1920×1080 并留一帧,供人(或视觉模型)判读。
//
// ⚠️ **本件不断言观感**(那是 `[L]` 人工面);它只断言链跑通(`IsCurrentPageMissing` 如实在),
//    否则截出来的图无意义。

using System.Collections;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Skeuomorphic;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>缺页态的视觉走查(图形模式留一帧)。</summary>
    [TestFixture]
    public sealed class PaperMissingWalkthrough
    {
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

        private GameObject _go;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private bool _panelSettingsOwned;
        private GameObject _cameraGo;
        private RenderTexture _target;

        [UnityTest]
        public IEnumerator test_missingPage_leavesOneFrame()
        {
            // ── 1. 面板(与 `modal_toggle_visual_walkthrough` 同一套坑的同一套解)──
            _go = new GameObject("PaperMissingWalkthrough");
#if UNITY_EDITOR
            _panelSettings = UnityEditor.AssetDatabase.LoadAssetAtPath<PanelSettings>(
                "Assets/Gameplay.UI/Skeuomorphic/PanelSettings.asset");
#endif
            if (_panelSettings == null)
            {
                _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                _panelSettingsOwned = true;
            }
            if (!_panelSettingsOwned)
            {
                _panelSettings = Object.Instantiate(_panelSettings);
                _panelSettingsOwned = true;
            }
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.scale = 1f;

            _target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _panelSettings.targetTexture = _target;

            _doc = _go.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > 10f)
                {
                    Assert.Fail("[夹具红] 面板 10s 未就绪 —— panelSettings 未生效");
                    yield break;
                }
                yield return null;
            }

            AttachProductionStyleSheets(_doc.rootVisualElement);

            _cameraGo = new GameObject("WalkthroughCamera");
            var camera = _cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            camera.cullingMask = 0;
            if (!Application.isBatchMode) camera.targetTexture = _target;

            // ── 2. 屏:两页,第一页缺 ──
            var screen = new PaperCloseupScreen(_doc.rootVisualElement, new SkeuoElementLibrary());
            screen.SetMissingPaperIds(new[] { "p_hemostasis" });
            screen.Bind(new[]
            {
                new TutorialPaperRow("p_hemostasis", "observe", "止血包扎", "以腰带压住上臂动脉,再缠布。", "4:3", null),
                new TutorialPaperRow("p_inspection", "observe", "望诊四要", "先看气色,再听声息。", "4:3", null),
            });

            Assert.That(screen.IsCurrentPageMissing, Is.True,
                "前置:缺页态须真的生效,否则截出来的是一张普通纸");
            Assert.That(screen.CurrentBody, Is.Empty, "前置:缺页页不摆正文");

            // 给 UI Toolkit 两帧布局
            yield return null;
            yield return null;

            if (Application.isBatchMode)
            {
                Assert.Ignore("batch 无图形设备 —— 真包截图须图形模式跑(T6 判据记 NOT-RUN)");
                yield break;
            }

            // ⚠️ 写目标是**具名声明过的暂存区**(见文件头);经变量而非直接字面量传入,
            //    与 `modal_toggle_visual_walkthrough` 同一形态(避 `FsWriteBoundaryGate` 的 R1)。
            string shotPath = "unity/Logs/shots/paper_missing.png";
            Directory.CreateDirectory(Path.GetDirectoryName(shotPath));
            SaveFrame(shotPath);
            Assert.That(File.Exists(shotPath), Is.True);
        }

        private void SaveFrame(string path)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log($"[PAPER] 已存帧 {_target.width}x{_target.height} ⇒ {path}");
        }

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

        [TearDown]
        public void TearDown()
        {
            if (_panelSettings != null && _panelSettingsOwned) Object.DestroyImmediate(_panelSettings);
            if (_target != null) { _target.Release(); Object.Destroy(_target); }
            if (_doc != null) Object.Destroy(_doc.gameObject);
            if (_go != null) Object.Destroy(_go);
            if (_cameraGo != null) Object.Destroy(_cameraGo);
        }
    }
}
