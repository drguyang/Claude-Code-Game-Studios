// `TD-build-02` 解除动作 ②(用户 2026-11-05 裁定「按②再修 TD-build-02(正式构建入口)」)。
//
// 病因:全仓**零**正式构建入口 —— 唯一一处 `BuildPipeline.BuildPlayer` 是探针 CLI
//   (`CostTableProbeBuildCli`,其头注自陈「本类不是产品的一部分」,产物落 `/tmp`)。
//   构建入口不存在 ⇒ 「打包后才发作」的缺陷(TD-build-03 就是这么潜伏的)**永远**只在
//   玩家手上有包的时候才暴露。
//
// 本类 = 正式入口的**单一执行体**:菜单(见 `PlayerBuildMenu`)与 CI
//   (`.github/workflows/player-build.yml`)与本地 batch **调的是同一个方法**,
//   不存在「菜单一套、CI 另一套」的第二实现。
//
// ⚠️ **场景清单取 `EditorBuildSettings`,不另立第二份**(承 ADR-023 §三场景制:
//   `Boot.unity` / `MainMenu.unity` / `World.unity` 的登记处就是 `EditorBuildSettings`,
//   再写一份清单 = 第二真源,两者必然分叉且谁也不报错)。
//
// ⚠️ **空场景清单 = 失败**,不是「建一个空包」。零场景的包能构建成功,但运行起来什么都没有 ——
//   那正是本项目反复抓过的「假绿」形态。
//
// 用法(本地 batch;仓库 CLAUDE.md 的 CLI 约定):
//   cd unity && "$UNITY" -batchmode -quit -nographics -projectPath "$PWD" \
//     -executeMethod DaYiJingCheng.EditorTools.Bake.PlayerBuildCli.Build \
//     -logFile Logs/player-build.log
//   ⚠️ `-projectPath` **必须**指向 `unity/`(仓库根**看起来**像工程但没有 `Assets/`)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>正式 player 构建入口(菜单 / CI / batch 三者共用同一执行体)。</summary>
    public static class PlayerBuildCli
    {
        /// <summary>产物根目录(**相对 `unity/` 工程根**)。
        /// <para>⚠️ 不能放 `Library/`:Unity 直接拒(`The 'Library' directory is an internal work
        /// directory of Unity and projects should not be built inside it.`,2026-10-30 实测)。
        /// `builds/` 已被 `.gitignore:40` 的 `builds/` 规则覆盖(该规则无斜杠前缀 ⇒ 匹配任意层级);
        /// 复算:`git check-ignore -v unity/builds/x` 应命中。</para></summary>
        public const string OutputRootRelative = "builds";

        /// <summary>本机主目标的 RID 目录名(与 ADR-012 矩阵格的叫法一致)。</summary>
        public const string Linux64Rid = "Linux-x64";

        /// <summary>batch 入口 —— Release。成功 `Exit(0)`;构建失败 `Exit(1)`;无场景 `Exit(2)`。</summary>
        public static void Build() => BuildAndExit(development: false);

        /// <summary>batch 入口 —— Development(带 profiler / 调试符号)。</summary>
        public static void BuildDevelopment() => BuildAndExit(development: true);

        private static void BuildAndExit(bool development)
        {
            int exitCode;
            try
            {
                BuildReport report = BuildPlayer(development);
                exitCode = report != null && report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (InvalidOperationException e)
            {
                // 无场景清单 / 产物路径不可用 —— 归「输入不成立」,与「构建跑失败」分开编码。
                Debug.LogError("[player-build] " + e.Message);
                exitCode = 2;
            }
            catch (Exception e)
            {
                Debug.LogError("[player-build] 构建抛异常:" + e);
                exitCode = 1;
            }

            Debug.Log($"[player-build] Exit {exitCode}");
            EditorApplication.Exit(exitCode);
        }

        /// <summary>执行一次构建并返回报告(**不退出编辑器** —— 菜单路径用它)。</summary>
        /// <exception cref="InvalidOperationException">`EditorBuildSettings` 里没有启用的场景。</exception>
        public static BuildReport BuildPlayer(bool development)
        {
            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
                throw new InvalidOperationException(
                    "`EditorBuildSettings` 里没有任何**启用**的场景 —— 拒绝构建空包。" +
                    "场景清单的唯一登记处就是 EditorBuildSettings(File → Build Settings)," +
                    "本类刻意不另立第二份清单。");

            string outputDir = Path.Combine(ProjectRoot(), OutputRootRelative, Linux64Rid);
            Directory.CreateDirectory(outputDir);
            string executableName = SanitizeFileName(Application.productName);
            string outputPath = Path.Combine(outputDir, executableName + ".x86_64");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            Debug.Log($"[player-build] 开始构建 → {outputPath}");
            Debug.Log($"[player-build] 场景数 = {scenes.Length}:" + Environment.NewLine + "  "
                      + string.Join(Environment.NewLine + "  ", scenes));
            Debug.Log($"[player-build] 形态 = {(development ? "Development" : "Release")}"
                      + $";product = {Application.productName};company = {Application.companyName}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[player-build] Result: {summary.result}"
                      + $";错误数 = {summary.totalErrors}"
                      + $";耗时 = {summary.totalTime}"
                      + $";产物 = {summary.outputPath}"
                      + $";输出大小 = {summary.totalSize} B");

            if (summary.result != BuildResult.Succeeded)
                Debug.LogError($"[player-build] 构建未成功:Result = {summary.result}");

            return report;
        }

        /// <summary>取 `EditorBuildSettings` 里**启用**的场景(唯一真源,不另立清单)。</summary>
        public static string[] CollectScenes()
        {
            var list = new List<string>();
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
            if (all != null)
                foreach (EditorBuildSettingsScene s in all)
                    if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                        list.Add(s.path);
            return list.ToArray();
        }

        /// <summary>`unity/` 工程根的绝对路径(`Application.dataPath` 的父目录)。</summary>
        public static string ProjectRoot()
            => Directory.GetParent(Application.dataPath)?.FullName
               ?? throw new InvalidOperationException("解析不出工程根(Application.dataPath 异常)");

        /// <summary>把产品名变成合法文件名 —— 只剔除路径分隔符与空白,不改大小写。</summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "player";
            char[] bad = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '\t' }).ToArray();
            string cleaned = new string(name.Select(c => bad.Contains(c) ? '-' : c).ToArray()).Trim('-');
            return string.IsNullOrEmpty(cleaned) ? "player" : cleaned;
        }
    }
}
