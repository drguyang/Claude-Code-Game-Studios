// 正式构建入口的**菜单面**(`TD-build-02` 解除动作 ②)。
//
// ⚠️ 本文件**不含构建逻辑** —— 它只是 `PlayerBuildCli.BuildPlayer` 的两个调用点。
//   菜单、本地 batch、CI 三处调的是**同一个方法**;任何一处「自己也写一遍
//   BuildPipeline.BuildPlayer」都会变成第二实现,而两套构建参数必然分叉且不报错。
//
// ⚠️ 菜单路径**不得**调用 `EditorApplication.Exit` —— 那会直接杀掉编辑器进程
//   (探针 CLI 之所以能 Exit,是因为它只在 `-batchmode` 下被调用)。区分方式是**分开两个方法**,
//   不是靠运行时判 `Application.isBatchMode`(那样菜单在 batch 里被误调时会静默不退)。

using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>「大医精诚 / Build」菜单 —— 正式 player 构建入口。</summary>
    public static class PlayerBuildMenu
    {
        /// <summary>产物目录(绝对路径)。</summary>
        private static string OutputDir()
            => System.IO.Path.Combine(PlayerBuildCli.ProjectRoot(),
                                      PlayerBuildCli.OutputRootRelative,
                                      PlayerBuildCli.Linux64Rid);

        /// <summary>Release 出包。</summary>
        [MenuItem("大医精诚/Build/出包 · Linux x64 (Release)", false, 200)]
        public static void BuildRelease() => BuildAndReport(development: false);

        /// <summary>Development 出包(带 profiler)。</summary>
        [MenuItem("大医精诚/Build/出包 · Linux x64 (Development)", false, 201)]
        public static void BuildDevelopment() => BuildAndReport(development: true);

        /// <summary>打开产物目录(不存在就提示,不隐式创建空目录)。</summary>
        [MenuItem("大医精诚/Build/打开产物目录", false, 220)]
        public static void RevealOutput()
        {
            string dir = OutputDir();
            if (!System.IO.Directory.Exists(dir))
            {
                EditorUtility.DisplayDialog("还没有产物",
                    "产物目录还不存在:\n" + dir + "\n\n先跑一次「出包 · Linux x64」。", "好");
                return;
            }
            EditorUtility.RevealInFinder(dir);
        }

        private static void BuildAndReport(bool development)
        {
            BuildReport report;
            try
            {
                report = PlayerBuildCli.BuildPlayer(development);
            }
            catch (InvalidOperationException e)
            {
                // 输入不成立(典型:EditorBuildSettings 里没有启用的场景)
                Debug.LogError("[player-build][菜单] " + e.Message);
                EditorUtility.DisplayDialog("构建未开始", e.Message, "好");
                return;
            }
            catch (Exception e)
            {
                Debug.LogError("[player-build][菜单] 构建抛异常:" + e);
                EditorUtility.DisplayDialog("构建抛异常", e.Message + "\n\n详见 Console。", "好");
                return;
            }

            BuildSummary s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                Debug.Log($"[player-build][菜单] 成功 → {s.outputPath}({s.totalSize} B,{s.totalTime})");
                EditorUtility.DisplayDialog("出包成功",
                    "产物:\n" + s.outputPath + "\n\n大小:" + s.totalSize + " B"
                    + "\n耗时:" + s.totalTime, "好");
            }
            else
            {
                Debug.LogError($"[player-build][菜单] 失败:Result = {s.result};错误数 = {s.totalErrors}");
                EditorUtility.DisplayDialog("出包失败",
                    "Result = " + s.result + "\n错误数 = " + s.totalErrors + "\n\n详见 Console。", "好");
            }
        }
    }
}
