// ADR-035 T1·S9 —— **构建前门的探针轮载体**(2026-10-30 立)。
//
// 权威来源:
//   CLAUDE.md §构建前门(BuildGate):「绿 = 没报错,**不等于**门跑了」⇒ 确认一个
//     `IPreprocessBuildWithReport` 门真的在生效,**必须**跑探针轮(插无条件失败项 ⇒ 构建 Failure)
//   ADR-035 §Migration Plan **S9** / §Validation **V-5 / V-6 / V-7**(字面都是「构建 Failure」)
//
// ⚠️ **为什么需要它**:全仓**零** `BuildPipeline.BuildPlayer` 调用(CI 只跑测试)⇒
//   `IPreprocessBuildWithReport` 今天**没有任何执行者**,门的接线只是「写好了」,
//   从未被构建真正调用过。本类给出**一条可复跑的本机构建命令**,让 S9 的判据有宾语。
//
// ⚠️ **本类不是产品的一部分**:产物落 `Library/CostTableProbeBuild/`(gitignored),
//   不碰任何作者态资产、不改设置。它的唯一用途 = 让构建门有东西可跑。
//
// ⚠️ 用法(仓库 CLAUDE.md 的 CLI 约定):
//   cd unity && "$UNITY" -batchmode -quit -nographics -projectPath "$PWD" \
//     -executeMethod DaYiJingCheng.EditorTools.Bake.Cli.CostTableProbeBuildCli.Build \
//     -logFile Logs/probe-build.log
//   ⚠️ `-projectPath` **必须**指向 `unity/`(仓库根**看起来**像工程但没有 `Assets/`,踩过坑)。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Bake.Cli
{
    /// <summary>构建前门探针轮的最小 player 构建入口。</summary>
    public static class CostTableProbeBuildCli
    {
        /// <summary>产物目录(系统临时目录下的一次性子目录)。
        /// <para>⚠️ **不能放工程内**:首版落在 `unity/Library/…`,Unity 直接拒 ——
        /// <c>ArgumentException: Invalid build path … The 'Library' directory is an internal work
        /// directory of Unity and projects should not be built inside it.</c>(实测,非预判)。
        /// 改用临时目录后:不污染仓库、不需要 `.gitignore` 条目。</para></summary>
        public const string OutputSubDirName = "dyjc-costtable-probe";

        /// <summary>batchmode 入口。</summary>
        /// <remarks>
        /// <para>失败 ⇒ <see cref="EditorApplication.Exit"/> 非零(batch 下 `-executeMethod` 抛异常
        /// 也非零,但显式 Exit 让判定不依赖「Unity 会不会把异常当失败」)。</para>
        /// <para>输出恒打 `Result: <Succeeded|Failed>` —— CI / 探针读日志用同一判据串。</para>
        /// </remarks>
        public static void Build()
        {
            string outputDir = Path.Combine(Path.GetTempPath(), OutputSubDirName);
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "CostTableProbe.x86_64");

            string[] scenes = CollectScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[CostTableProbeBuild] 构建场景为空 —— 无法形成可判定的构建");
                EditorApplication.Exit(2);
                return;
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneLinux64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            Debug.Log("[CostTableProbeBuild] 开始构建 → " + outputPath
                      + ";场景数 = " + scenes.Length);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log("[CostTableProbeBuild] Result: " + summary.result
                      + ";错误数 = " + summary.totalErrors
                      + ";耗时 = " + summary.totalTime);
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>取 `EditorBuildSettings` 里启用的场景(不另立第二份场景清单)。</summary>
        private static string[] CollectScenes()
        {
            var list = new List<string>();
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
            if (all != null)
                foreach (EditorBuildSettingsScene s in all)
                    if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                        list.Add(s.path);
            return list.ToArray();
        }
    }
}
