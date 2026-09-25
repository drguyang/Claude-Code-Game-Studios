// Story 001 · AC-3-A4② 的载体装置 —— 给分析器 DLL 打 RoslynAnalyzer label
//
// 权威来源:
//   AC-3-A4②(BLOCKING)Roslyn 分析器在编译期拒绝任何 UnityEngine.Input 符号引用。
//   unity-specialist 预检(2026-09-25):RoslynAnalyzer 是 .meta 的 labels: 键,
//     AssetDatabase.SetLabels 在 batchmode 可用 —— 绝不手写 .meta,由 Unity 自己生成。
//
// 用法(batch,由调用方执行一次,先于 EditMode 测试):
//   unity build <project> --target StandaloneLinux64 \
//     --executeMethod DaYiJingCheng.EditorTools.Gates.RoslynAnalyzerLabel.SetLabel
// 亦可菜单触发:大医精诚/Validation/Label Legacy Input Analyzer
//
// ⚠️ 运行前置:tools/analyzers/build.sh 已产出
//    unity/Assets/Editor.Tools.Analyzers/LegacyInputAnalyzer.dll(缺失即抛,batch 非零退出)。
// ⚠️ label 打完后 Unity 会重编译;同一 batch 会话内后续 executeMethod(测试)在重编译后运行。

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>把 LegacyInputAnalyzer.dll 标记为 Roslyn 分析器(幂等:已有 label 则跳过)。
    /// 失败一律 <c>throw</c>(batch 需要非零退出码,不能只打日志)。</summary>
    public static class RoslynAnalyzerLabel
    {
        /// <summary>分析器 DLL 在工程内的唯一落点(ADR-025 清单封闭性:此目录不建 asmdef)。</summary>
        public const string AnalyzerAssetPath = "Assets/Editor.Tools.Analyzers/LegacyInputAnalyzer.dll";

        /// <summary>Unity 分析器 label(官方约定;RoslynAnalyzer 即全部机制,无第二步骤)。</summary>
        public const string RoslynAnalyzerLabelName = "RoslynAnalyzer";

        [MenuItem("大医精诚/Validation/Label Legacy Input Analyzer")]
        public static void SetLabel()
        {
            // 相对路径以工程根为基准;文件存在性用工程内相对路径判断(batch 的 CWD = 工程根)。
            if (!File.Exists(AnalyzerAssetPath))
                throw new FileNotFoundException(
                    "分析器 DLL 不存在 —— 先跑 tools/analyzers/build.sh:" + AnalyzerAssetPath);

            AssetDatabase.ImportAsset(AnalyzerAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AnalyzerAssetPath);
            if (asset == null)
                throw new InvalidOperationException("AssetDatabase 加载失败(DLL 未被导入为资产):" + AnalyzerAssetPath);

            var labels = AssetDatabase.GetLabels(asset) ?? Array.Empty<string>();
            if (labels.Contains(RoslynAnalyzerLabelName))
            {
                Debug.Log($"[RoslynAnalyzerLabel] 已带 {RoslynAnalyzerLabelName} label,幂等跳过:{AnalyzerAssetPath}");
                return;
            }

            AssetDatabase.SetLabels(asset, new[] { RoslynAnalyzerLabelName });
            AssetDatabase.ImportAsset(AnalyzerAssetPath, ImportAssetOptions.ForceUpdate);

            var after = AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AnalyzerAssetPath))
                        ?? Array.Empty<string>();
            if (!after.Contains(RoslynAnalyzerLabelName))
                throw new InvalidOperationException("SetLabels 后复查失败 —— label 未生效:" + AnalyzerAssetPath);

            Debug.Log($"[RoslynAnalyzerLabel] 已打 {RoslynAnalyzerLabelName} label:{AnalyzerAssetPath}");
        }
    }
}
#endif
