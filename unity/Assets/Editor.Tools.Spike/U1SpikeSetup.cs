// U1 spike 批 —— 一键装置(S1 / S3 / S4 + 假设 6 场景)
//
// 权威来源:
//   ADR-023 §Validation S1 / S3 / S4(三点均 NOT-RUN,本菜单是其执行装置);
//   ADR-013 §6.6 假设 6(半可信 → 本批实测);S-4 补强(bundle refcount 观测);
//   production/u1-spike-checklist.md §4 / §5(判据正文,本脚本不改判据,只把
//     原手工建场景步骤脚本化 —— 用户 2026-09-22 裁定,记忆 feedback-scripted-spikes)。
//
// 菜单:
//   DaYi/Spike/Setup U1 Spikes    —— 建 4 个临时场景 + Cube prefab + 假设 6 装置,
//                                    标 Addressable、切 Existing Build、BuildPlayerContent。
//   DaYi/Spike/Teardown U1 Spikes —— 删全部 Spike* 临时资产 + 摘 Addressable 条目 + 重建 catalog。
//
// ⚠️ 临时资产路径全部以 Spike 开头,已由 .gitignore `unity/Assets/Scenes/Spike*` 拦下,
//    永不提交;AddressableAssetsData/ 会由本菜单首次生成 —— **保留、不提交**(归 ADR-014 正式批)。
// ⚠️ 若干 Addressables 编辑期 API 属 2.10 线,集群无 Unity 无法编译验证 —— 编译判定唯一归【桌面】;
//    失败则回报 Console 红行,按实际签名就地修(风险面见卡 §8)。

#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace DaYiJingCheng.EditorTools.Spike
{
    /// <summary>U1 spike 全部路径 / Addressable key 的单一出处(setup、teardown、测试约定共用)。</summary>
    internal static class U1SpikePaths
    {
        public const string SceneS1 = "Assets/Scenes/SpikeS1_Temp.unity";
        public const string SceneA1 = "Assets/Scenes/SpikeS4_A1.unity";
        public const string SceneA2 = "Assets/Scenes/SpikeS4_A2.unity";
        public const string SceneB = "Assets/Scenes/SpikeS4_B.unity";
        public const string PrefabCube = "Assets/Scenes/SpikeCube.prefab";
        public const string SceneAssump6 = "Assets/Scenes/SpikeAssump6.unity";
        public const string UxmlAssump6 = "Assets/Scenes/SpikeAssump6.uxml";
        public const string UssAssump6 = "Assets/Scenes/SpikeAssump6.uss";
        public const string PanelAssump6 = "Assets/Scenes/SpikeAssump6_Panel.asset";

        public const string KeyS1 = "U1_SpikeS1";
        public const string KeyA1 = "U1_SpikeS4A1";
        public const string KeyA2 = "U1_SpikeS4A2";
        public const string KeyB = "U1_SpikeS4B";
        public const string KeyCube = "U1_SpikeCube";

        public static readonly string[] AssetPaths =
        {
            SceneS1, SceneA1, SceneA2, SceneB, PrefabCube,
            SceneAssump6, UxmlAssump6, UssAssump6, PanelAssump6,
        };

        public static readonly (string path, string key)[] Addressables =
        {
            (SceneS1, KeyS1),
            (SceneA1, KeyA1),
            (SceneA2, KeyA2),
            (SceneB, KeyB),
            (PrefabCube, KeyCube),
        };
    }

    /// <summary>一键生成 / 清理 U1 spike 临时装置。幂等:已存在的资产跳过创建。</summary>
    internal static class U1SpikeSetup
    {
        const string Assump6Uxml =
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n" +
            "    <ui:VisualElement name=\"container\" class=\"container\">\n" +
            "        <ui:Label name=\"status\" text=\"U1 假设6 焦点导航 spike — 方向键/摇杆移动焦点\" class=\"status\" />\n" +
            "        <ui:VisualElement name=\"grid\" class=\"grid\">\n" +
            "            <ui:VisualElement class=\"row\">\n" +
            "                <ui:Button name=\"btn-0-0\" text=\"0,0\" />\n" +
            "                <ui:Button name=\"btn-0-1\" text=\"0,1\" />\n" +
            "                <ui:Button name=\"btn-0-2\" text=\"0,2\" />\n" +
            "            </ui:VisualElement>\n" +
            "            <ui:VisualElement class=\"row\">\n" +
            "                <ui:Button name=\"btn-1-0\" text=\"1,0\" />\n" +
            "                <ui:Button name=\"btn-1-1\" text=\"1,1\" />\n" +
            "                <ui:Button name=\"btn-1-2\" text=\"1,2\" />\n" +
            "            </ui:VisualElement>\n" +
            "            <ui:VisualElement class=\"row\">\n" +
            "                <ui:Button name=\"btn-2-0\" text=\"2,0\" />\n" +
            "                <ui:Button name=\"btn-2-1\" text=\"2,1\" />\n" +
            "                <ui:Button name=\"btn-2-2\" text=\"2,2\" />\n" +
            "            </ui:VisualElement>\n" +
            "        </ui:VisualElement>\n" +
            "    </ui:VisualElement>\n" +
            "</ui:UXML>\n";

        const string Assump6Uss =
            ".container {\n" +
            "    flex-grow: 1;\n" +
            "    background-color: rgb(20, 20, 24);\n" +
            "    padding: 24px;\n" +
            "}\n" +
            "\n" +
            ".status {\n" +
            "    color: rgb(200, 220, 255);\n" +
            "    font-size: 18px;\n" +
            "    margin-bottom: 16px;\n" +
            "}\n" +
            "\n" +
            ".grid {\n" +
            "    flex-direction: column;\n" +
            "}\n" +
            "\n" +
            ".row {\n" +
            "    flex-direction: row;\n" +
            "}\n" +
            "\n" +
            "Button {\n" +
            "    width: 140px;\n" +
            "    height: 64px;\n" +
            "    margin: 6px;\n" +
            "    background-color: rgb(50, 50, 60);\n" +
            "    color: rgb(220, 220, 220);\n" +
            "    font-size: 20px;\n" +
            "}\n" +
            "\n" +
            "Button:focus {\n" +
            "    background-color: rgb(255, 200, 60);\n" +
            "    color: rgb(20, 20, 20);\n" +
            "    border-left-width: 5px;\n" +
            "    border-color: rgb(255, 255, 255);\n" +
            "}\n";

        [MenuItem("DaYi/Spike/Setup U1 Spikes")]
        static void Setup()
        {
            var sw = Stopwatch.StartNew();
            int created = 0, skipped = 0;

            try
            {
                var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

                // ── 1. 假设 6 静态资产(UXML / USS / PanelSettings)──
                created += WriteTextAsset(U1SpikePaths.UxmlAssump6, Assump6Uxml) ? 1 : 0;
                skipped += File.Exists(U1SpikePaths.UxmlAssump6) ? 1 : 0;
                created += WriteTextAsset(U1SpikePaths.UssAssump6, Assump6Uss) ? 1 : 0;
                skipped += File.Exists(U1SpikePaths.UssAssump6) ? 1 : 0;
                if (!File.Exists(U1SpikePaths.PanelAssump6))
                {
                    var ps = ScriptableObject.CreateInstance<PanelSettings>();
                    ps.name = "SpikeAssump6_Panel";
                    TryAssignTheme(ps);
                    AssetDatabase.CreateAsset(ps, U1SpikePaths.PanelAssump6);
                    created++;
                }
                else skipped++;

                // ── 2. 四个临时场景 ──
                created += CreateTempScene(U1SpikePaths.SceneS1, scene =>
                {
                    var m = new GameObject("Marker");
                    SceneManager.MoveGameObjectToScene(m, scene);
                }) ? 1 : 0;
                if (File.Exists(U1SpikePaths.SceneS1)) skipped += 0; // CreateTempScene 内部已计 skipped
                created += CreateTempScene(U1SpikePaths.SceneA1, scene =>
                {
                    var m = new GameObject("Marker");
                    SceneManager.MoveGameObjectToScene(m, scene);
                }) ? 1 : 0;
                created += CreateTempScene(U1SpikePaths.SceneA2, scene =>
                {
                    var m = new GameObject("Marker");
                    SceneManager.MoveGameObjectToScene(m, scene);
                }) ? 1 : 0;
                created += CreateTempScene(U1SpikePaths.SceneB, scene =>
                {
                    var a = new GameObject("RootA");
                    var b = new GameObject("RootB");
                    SceneManager.MoveGameObjectToScene(a, scene);
                    SceneManager.MoveGameObjectToScene(b, scene);
                    b.SetActive(false);
                }) ? 1 : 0;

                // ── 3. Cube prefab(S3 InstantiateAsync 载体)──
                if (!File.Exists(U1SpikePaths.PrefabCube))
                {
                    var go = new GameObject("SpikeCube");
                    try
                    {
                        PrefabUtility.SaveAsPrefabAsset(go, U1SpikePaths.PrefabCube);
                        created++;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(go);
                    }
                }
                else skipped++;

                // ── 4. 假设 6 场景(EventSystem + InputSystemUIInputModule + UIDocument + 探针)──
                if (!File.Exists(U1SpikePaths.SceneAssump6))
                {
                    CreateAssump6Scene();
                    created++;
                }
                else skipped++;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // ── 5. 标 Addressable(key = 单一出处 U1SpikePaths)──
                foreach (var (path, key) in U1SpikePaths.Addressables)
                    EnsureAddressableEntry(settings, path, key);

                // ── 6. Play Mode 切 Existing Build(bundle refcount 观测前提)──
                ConfigurePlayMode(settings);

                // ── 7. BuildPlayerContent ──
                // ⚠️ 内容构建是**编辑期** API:住 AddressableAssetSettings,且签名是 `void`
                //    (2.10.3 实读:`public static void BuildPlayerContent(out AddressablesPlayerBuildResult result)`),
                //    成功/失败只能读 `result.Error`。运行期静态类 Addressables 上没有此方法。
                var buildSw = Stopwatch.StartNew();
                AddressableAssetSettings.BuildPlayerContent(out var result);
                buildSw.Stop();
                if (!string.IsNullOrEmpty(result?.Error))
                    Debug.LogError($"[U1] BuildPlayerContent 失败: {result.Error}");
                else
                    Debug.Log($"[U1] BuildPlayerContent 完成,耗时 {buildSw.ElapsedMilliseconds} ms");

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                sw.Stop();

                Debug.Log($"[U1] Setup 完成:新建 {created} · 跳过已存在 {skipped} · 总耗时 {sw.ElapsedMilliseconds} ms\n" +
                          "[U1] 下一步:① Test Runner(PlayMode)跑 U1SceneSpikesTest 三条 → 结果自动写 Logs/u1_spike_results.txt\n" +
                          "[U1]           ② 手柄腿:打开 " + U1SpikePaths.SceneAssump6 + " 按 Play(判据见卡 §5.2)\n" +
                          "[U1]           ③ 跑完菜单 DaYi/Spike/Teardown U1 Spikes 清临时资产");
            }
            catch (Exception e)
            {
                Debug.LogError($"[U1] Setup 异常:{e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("DaYi/Spike/Teardown U1 Spikes")]
        static void Teardown()
        {
            int removedEntries = 0, deletedAssets = 0;
            try
            {
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    foreach (var (path, _) in U1SpikePaths.Addressables)
                    {
                        var guid = AssetDatabase.AssetPathToGUID(path);
                        if (string.IsNullOrEmpty(guid)) continue;
                        var entry = settings.FindAssetEntry(guid);
                        if (entry == null) continue;
                        settings.RemoveAssetEntry(guid);
                        removedEntries++;
                    }
                    EditorUtility.SetDirty(settings);
                }

                foreach (var path in U1SpikePaths.AssetPaths)
                {
                    if (!File.Exists(path)) continue;
                    if (AssetDatabase.DeleteAsset(path)) deletedAssets++;
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (settings != null)
                {
                    var buildSw = Stopwatch.StartNew();
                    AddressableAssetSettings.BuildPlayerContent(out var result);
                    buildSw.Stop();
                    if (!string.IsNullOrEmpty(result?.Error))
                        Debug.LogError($"[U1] Teardown 后 catalog 重建失败:{result.Error}");
                    else
                        Debug.Log($"[U1] catalog 重建完成,{buildSw.ElapsedMilliseconds} ms");
                }

                Debug.Log($"[U1] Teardown 完成:摘除条目 {removedEntries} · 删除资产 {deletedAssets}\n" +
                          "[U1] AddressableAssetsData/ 刻意保留(本地工作配置,**不要提交**,归 ADR-014 正式批);" +
                          "结果文件 Logs/u1_spike_results.txt 保留。");
            }
            catch (Exception e)
            {
                Debug.LogError($"[U1] Teardown 异常:{e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ────────────────────────── helpers ──────────────────────────

        static bool WriteTextAsset(string path, string content)
        {
            if (File.Exists(path)) return false;
            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
            return true;
        }

        /// <summary>加性建场景 → 落盘 → 关闭;不触碰用户当前打开的场景(Boot)。</summary>
        static bool CreateTempScene(string path, Action<Scene> populate)
        {
            if (File.Exists(path)) return false;
            EditorUtility.DisplayProgressBar("[U1] Setup", $"创建 {Path.GetFileName(path)}", 0.5f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var prev = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);
                populate(scene);
                if (!EditorSceneManager.SaveScene(scene, path))
                    Debug.LogError($"[U1] SaveScene 失败:{path}");
            }
            finally
            {
                if (prev.IsValid()) SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(scene, true);
            }
            return true;
        }

        static void CreateAssump6Scene()
        {
            EditorUtility.DisplayProgressBar("[U1] Setup", "创建 SpikeAssump6 场景", 0.9f);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var prev = SceneManager.GetActiveScene();
            try
            {
                SceneManager.SetActiveScene(scene);

                // EventSystem:仅 InputSystemUIInputModule(禁 StandaloneInputModule 并存 —— 卡 §5.1)。
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                SceneManager.MoveGameObjectToScene(es, scene);
                var module = es.GetComponent<InputSystemUIInputModule>();
                var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                if (actions != null)
                {
                    module.actionsAsset = actions; // 判据④:焦点只经官方 UI/Navigate,单一 actions 来源
                    Debug.Log("[U1] InputSystemUIInputModule.actionsAsset = InputSystem_Actions(UI map)");
                }
                else
                {
                    Debug.LogWarning("[U1] 未找到 Assets/InputSystem_Actions.inputactions —— module 保持默认 action 引用,判据④须在 Console 复核");
                }

                var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(U1SpikePaths.PanelAssump6);
                var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(U1SpikePaths.UxmlAssump6);
                if (panel == null || uxml == null)
                {
                    Debug.LogError("[U1] PanelSettings / UXML 资产缺失,假设 6 场景不完整");
                    return;
                }

                var ui = new GameObject("UIDocument", typeof(UIDocument));
                SceneManager.MoveGameObjectToScene(ui, scene);
                var doc = ui.GetComponent<UIDocument>();
                doc.panelSettings = panel;
                doc.visualTreeAsset = uxml;
                ui.AddComponent<U1FocusProbe>(); // 双触发 / 焦点计数探针(判据①③的自动化一半)

                if (!EditorSceneManager.SaveScene(scene, U1SpikePaths.SceneAssump6))
                    Debug.LogError($"[U1] SaveScene 失败:{U1SpikePaths.SceneAssump6}");
            }
            finally
            {
                if (prev.IsValid()) SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void EnsureAddressableEntry(AddressableAssetSettings settings, string path, string key)
        {
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[U1] 取不到 guid,跳过标 Addressable:{path}");
                return;
            }

            // ⚠️ 2.10.3 已无 `AddAssetEntry(guid, address, groupName)`(引擎参考件里的 3 参形态系旧线)。
            //    现行入口 = CreateOrMoveEntry(guid, group):已存在则返回既有条目并归位到该组。
            var group = settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogError($"[U1] 无 DefaultGroup —— Addressables 初始化不完整,跳过 {path}");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null)
            {
                Debug.LogError($"[U1] CreateOrMoveEntry 返回 null:{path}");
                return;
            }

            if (entry.address != key)
            {
                entry.address = key;
                Debug.Log($"[U1] 已标 Addressable / 地址改写 {path} → key={key} (group={group.name})");
            }
            else
            {
                Debug.Log($"[U1] 已标 Addressable:{path} → key={key} (group={group.name})");
            }
        }

        /// <summary>Play Mode 切到 Existing Build(bundle 层才存在,S3 判据 2/3 的前提)。
        /// builder 名单逐条打进 Console;找不到 Existing 字样则保持原样并给出手动指引。</summary>
        static void ConfigurePlayMode(AddressableAssetSettings settings)
        {
            // ⚠️ DataBuilders 的声明类型是 List<ScriptableObject>(2.10.3 实读),
            //    `Name` 是 IDataBuilder 成员 ⇒ 必须走 GetDataBuilder(i)(返回 IDataBuilder),不能直接点 Name。
            int count = settings.DataBuilders.Count;
            int current = settings.ActivePlayModeDataBuilderIndex;
            int existing = -1;
            for (int i = 0; i < count; i++)
            {
                var name = settings.GetDataBuilder(i)?.Name ?? "<null>";
                Debug.Log($"[U1] DataBuilder[{i}] = {name}" + (i == current ? "  ← 当前" : ""));
                if (existing < 0 && name.IndexOf("Existing", StringComparison.OrdinalIgnoreCase) >= 0)
                    existing = i;
            }
            if (existing >= 0)
            {
                if (current != existing)
                {
                    settings.ActivePlayModeDataBuilderIndex = existing;
                    Debug.Log($"[U1] Play Mode → DataBuilder[{existing}](Existing Build)—— S3 bundle refcount 观测前提");
                }
                else Debug.Log("[U1] Play Mode 已是 Existing Build");
            }
            else
            {
                Debug.LogWarning("[U1] 未找到名含 \"Existing\" 的 DataBuilder,保持当前 Play Mode。" +
                                 "若当前 = Use Asset Database,S3 的 bundle 计数将恒 0(判据 2/3 记 N/A)—— " +
                                 "手动:Window > Asset Management > Addressables > Groups → Play Mode Script 切 Use existing build");
            }
        }

        static void TryAssignTheme(PanelSettings ps)
        {
            // 默认主题可能藏在 Packages 里;两段搜索都试,找不到只警告不阻断
            // (无主题时按钮仍可聚焦,焦点高亮由我们自己的 USS :focus 提供;文字渲染风险见卡 §8)。
            foreach (var folders in new[]
                     {
                         new[] { "Assets" },
                         new[] { "Packages" },
                     })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:ThemeStyleSheet", folders))
                {
                    var p = AssetDatabase.GUIDToAssetPath(guid);
                    var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(p);
                    if (theme == null) continue;
                    ps.themeStyleSheet = theme;
                    Debug.Log($"[U1] PanelSettings 主题 ← {p}");
                    return;
                }
            }
            Debug.LogWarning("[U1] 未找到 ThemeStyleSheet —— 若假设 6 场景按钮无文字,手动 Create > UI Toolkit > Panel Settings 一份替换后重开场景");
        }
    }
}
#endif
