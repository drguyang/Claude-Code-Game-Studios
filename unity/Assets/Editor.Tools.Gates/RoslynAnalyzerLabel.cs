// Story 001 · AC-3-A4② 的载体装置 —— 给分析器 DLL 打 RoslynAnalyzer label
//              + 校正 PluginImporter 配置(2026-09-25 复核 W1)
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
//
// PluginImporter 校正(W1,幂等):validateReferences → false(否则 MonoManager 每次域重载
//   报「Unable to resolve reference System.private.CoreLib…」error 级日志)·
//   isExplicitlyReferenced → true(Auto Reference 关,本 DLL 不再出现在各程序集 -r: 列表)。
//   字段经 SerializedObject 读写(与 AC-3-A4① 载体同一手法),由 Unity 自己写回 .meta —— 不手改文本。

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>把 LegacyInputAnalyzer.dll 标记为 Roslyn 分析器,并校正其 PluginImporter 配置
    /// (全部幂等:已有 label / 已校正则跳过)。失败一律 <c>throw</c>(batch 需要非零退出码,不能只打日志)。</summary>
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

            ConfigurePluginImporter();

            var labels = AssetDatabase.GetLabels(asset) ?? Array.Empty<string>();
            if (labels.Contains(RoslynAnalyzerLabelName))
            {
                Debug.Log($"[RoslynAnalyzerLabel] 已带 {RoslynAnalyzerLabelName} label,幂等跳过:{AnalyzerAssetPath}");
                return;
            }

            // SetLabels 是整体替换 —— 与既有 label 取并集,不吞掉别人打的标(2026-09-25 复核 N3)。
            AssetDatabase.SetLabels(asset, labels
                .Concat(new[] { RoslynAnalyzerLabelName })
                .Distinct()
                .ToArray());
            AssetDatabase.ImportAsset(AnalyzerAssetPath, ImportAssetOptions.ForceUpdate);

            var after = AssetDatabase.GetLabels(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AnalyzerAssetPath))
                        ?? Array.Empty<string>();
            if (!after.Contains(RoslynAnalyzerLabelName))
                throw new InvalidOperationException("SetLabels 后复查失败 —— label 未生效:" + AnalyzerAssetPath);

            Debug.Log($"[RoslynAnalyzerLabel] 已打 {RoslynAnalyzerLabelName} label:{AnalyzerAssetPath}");
        }

        /// <summary>校正 PluginImporter:关引用校验(消 MonoManager 域重载报错)、关 Auto Reference
        /// (本 DLL 不进各程序集引用列表)。字段名与 .meta 序列化名一致;读不到即抛(引擎布局变更须重估)。</summary>
        private static void ConfigurePluginImporter()
        {
            var importer = AssetImporter.GetAtPath(AnalyzerAssetPath) as PluginImporter;
            if (importer == null)
                throw new InvalidOperationException("PluginImporter 获取失败:" + AnalyzerAssetPath);

            var so = new SerializedObject(importer);
            so.Update();
            bool changed = false;

            SerializedProperty validate = FindPropertyDeep(so, "validateReferences");
            if (validate == null)
            {
                Debug.LogError("[RoslynAnalyzerLabel] PluginImporter 序列化属性全量:" + DumpPropertyNames(so));
                throw new InvalidOperationException(
                    "序列化字段 validateReferences 不存在 —— 引擎序列化布局变更,W1 校正判据需重估");
            }
            if (validate.boolValue)
            {
                validate.boolValue = false;
                changed = true;
            }

            SerializedProperty explicitRef = FindPropertyDeep(so, "isExplicitlyReferenced");
            if (explicitRef == null)
            {
                Debug.LogError("[RoslynAnalyzerLabel] PluginImporter 序列化属性全量:" + DumpPropertyNames(so));
                throw new InvalidOperationException(
                    "序列化字段 isExplicitlyReferenced 不存在 —— 引擎序列化布局变更,W1 校正判据需重估");
            }
            if (!explicitRef.boolValue)
            {
                explicitRef.boolValue = true;
                changed = true;
            }

            if (!changed)
            {
                Debug.Log("[RoslynAnalyzerLabel] PluginImporter 已是目标配置(validateReferences=0, AutoReference=off),幂等跳过");
                return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.ImportAsset(AnalyzerAssetPath, ImportAssetOptions.ForceUpdate);

            // 复查:重读 .meta 序列化字段,未生效即抛
            var reimporter = AssetImporter.GetAtPath(AnalyzerAssetPath) as PluginImporter;
            var reSo = new SerializedObject(reimporter);
            reSo.Update();
            bool validateOk = FindPropertyDeep(reSo, "validateReferences") is SerializedProperty v && !v.boolValue;
            bool autoRefOk = FindPropertyDeep(reSo, "isExplicitlyReferenced") is SerializedProperty e && e.boolValue;
            if (!validateOk || !autoRefOk)
                throw new InvalidOperationException(
                    "PluginImporter 校正复查失败(validateReferences 应为 false、isExplicitlyReferenced 应为 true):" + AnalyzerAssetPath);

            Debug.Log("[RoslynAnalyzerLabel] PluginImporter 已校正(validateReferences=0, AutoReference=off):" + AnalyzerAssetPath);
        }

        /// <summary>按叶子名深度查找序列化属性 —— .meta YAML 键(如 <c>validateReferences</c>)与
        /// SerializedObject 属性名(实测为 <c>m_ValidateReferences</c>,2026-09-25 batch 实证)不同形,
        /// 故同时匹配原名与 <c>m_</c> + 首字母大写变体(忽略大小写)。</summary>
        private static SerializedProperty FindPropertyDeep(SerializedObject so, string leafName)
        {
            string mName = "m_" + char.ToUpperInvariant(leafName[0]) + leafName.Substring(1);
            try
            {
                SerializedProperty it = so.GetIterator();
                if (it == null)
                    return null;
                if (MatchesLeaf(it, leafName, mName))
                    return it.Copy();
                bool enterChildren = true;
                while (it.Next(enterChildren))
                {
                    enterChildren = true;
                    if (MatchesLeaf(it, leafName, mName))
                        return it.Copy();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[RoslynAnalyzerLabel] 属性遍历异常:" + ex.Message);
            }
            return null;
        }

        private static bool MatchesLeaf(SerializedProperty prop, string leafName, string mCamelName)
        {
            string n = prop.name;
            return string.Equals(n, leafName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, mCamelName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>失败诊断:列出 SerializedObject 全部属性路径(用于引擎布局变更时定位新名)。</summary>
        private static string DumpPropertyNames(SerializedObject so)
        {
            var names = new System.Collections.Generic.List<string>();
            try
            {
                SerializedProperty it = so.GetIterator();
                if (it != null)
                {
                    names.Add(it.name);
                    bool enterChildren = true;
                    while (it.Next(enterChildren))
                    {
                        enterChildren = true;
                        names.Add(it.name);
                    }
                }
            }
            catch (Exception ex)
            {
                names.Add("<遍历异常:" + ex.Message + ">");
            }
            return string.Join(", ", names);
        }
    }
}
#endif
