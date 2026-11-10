// T20 · `case_pattern.json` 的**阶段 2 绑定器**(37 的同源检测旋钮;TR-case-031 的构建期执行体)。
//
// 权威来源:
//   ADR-014 §三(阶段 2:已知键白名单 · schema_version · 聚合 throw)· §五(装载期硬失败,E-13)
//   GDD `design/gdd/case-system.md:480` —— `PATTERN_THRESHOLD | int | ≥ 2 | 调参旋钮 | P0 = 3 |
//     进 ConfigVersion 覆盖集`;`:869` 低侧理由「2 = 两例即同源,太廉价,归纳退化成猜测」
//   `TR-case-031`(改值可追溯;**不拒载** —— ADR-010 §七 语义:已发生事件不受影响)
//
// ⚠️ **本件与 `PatternRecognitionWriter` 构造器的守卫同源,不是第二真源**:
//   写者拒 `patternThreshold < 2`(防线在运行期入口),本件拒同一域(防线在构建期)。
//   两处都拒同一个值 = 纵深防御;两处判据不同 = 第二真源。**改域必须同改两处。**
//
// ⚠️ 本件同样拒「把阈值写死进代码」:值只有本表一处(与 `InventoryConstantsBinder` 同纪律)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`case_pattern.json` 的键白名单(阶段 2 绑定器的词表半边)。</summary>
    public static class CasePatternSchema
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "case_pattern.json";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>顶层键白名单(ADR-014 §三:未知键 = 硬失败)。</summary>
        internal static readonly string[] RootKeys =
        {
            "schema_version", "_note", "pattern_threshold",
        };

        /// <summary>除 <c>schema_version</c> / <c>_note</c> 外全部必填。</summary>
        internal static readonly string[] RequiredKeys =
        {
            "pattern_threshold",
        };

        /// <summary>阈值的下界(GDD `:480` 安全范围;`< 2` ⇒ 构建期拒绝)。</summary>
        public const int MinThreshold = 2;
    }

    /// <summary>`case_pattern.json` 的阶段 2 绑定器。</summary>
    public static class CasePatternBinder
    {
        /// <summary>源文件名(转发自 schema,单一出处)。</summary>
        public const string FileName = CasePatternSchema.FileName;

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>`PATTERN_THRESHOLD`(已校验 ≥ 2)。</summary>
            public readonly int PatternThreshold;

            /// <summary>源 schema_version(写方头槽的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造。</summary>
            public BindResult(int patternThreshold, uint schemaVersion)
            {
                PatternThreshold = patternThreshold; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>从仓根绑定(读源 → 阶段 1 词法 → 阶段 2 绑定 + 校验)。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的那一层)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        /// <exception cref="BakeValidationException">源缺失 / 词法 / 任一校验失败(聚合)。</exception>
        public static BindResult BindFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = System.IO.Path.Combine(repoRoot, CasePatternSchema.DataDirName, FileName);
            if (!System.IO.File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}" });

            return Bind(System.IO.File.ReadAllText(path));
        }

        /// <summary>从 JSON 串绑定(负向夹具入口;与 <see cref="BindFromRepo"/> 同一路径)。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 阶段1 词法失败:{lexErr}" });

            if (root == null || root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 根须为对象(实得 {root?.Kind})" });

            RejectUnknownKeys(root, errors);
            uint schemaVersion = ReadSchemaVersion(root, errors);

            foreach (string key in CasePatternSchema.RequiredKeys)
                if (!root.TryGet(key, out _))
                    errors.Add($"{FileName}:缺必填键 \"{key}\" —— " +
                               "阈值未定值时装载必须硬失败,不得以默认值兜底");

            int threshold = ReadThreshold(root, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);
            return new BindResult(threshold, schemaVersion);
        }

        private static void RejectUnknownKeys(JsonNode root, List<string> errors)
        {
            var allowed = new HashSet<string>(CasePatternSchema.RootKeys, StringComparer.Ordinal);
            foreach (string key in root.Keys)
                if (!allowed.Contains(key))
                    errors.Add($"{FileName}:未登记键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败");
        }

        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode sv))
            {
                errors.Add($"{FileName}:缺 schema_version");
                return 0u;
            }
            if (sv.Kind != JsonNodeKind.Integer || sv.Int < 0 || sv.Int > uint.MaxValue)
            {
                errors.Add($"{FileName}:schema_version 须为 u32 域整数(实得 {sv.Kind}={sv.RawText})");
                return 0u;
            }
            if ((uint)sv.Int != CookedFormat.SchemaVersion)
            {
                errors.Add($"{FileName}:schema_version={(uint)sv.Int} ≠ 期望 {CookedFormat.SchemaVersion} " +
                           "(源表 schema 与运行时头契约不一致 ⇒ 构建期硬失败)");
                return 0u;
            }
            return (uint)sv.Int;
        }

        /// <summary>`pattern_threshold` 的四条负路径(缺失 / 非整数 token / 越 int 域 / &lt; 2)。</summary>
        private static int ReadThreshold(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("pattern_threshold", out JsonNode n))
                return 0;   // 缺失已由 RequiredKeys 循环登记

            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(n.Kind == JsonNodeKind.String
                    ? $"{FileName}:pattern_threshold 须为 JSON **整数** token,实得字符串 {n.RawText} —— " +
                      "阈值是 int 计数口径(D-21-17),不是 Fix"
                    : $"{FileName}:pattern_threshold 须为 JSON 整数 token(实得 {n.Kind})");
                return 0;
            }

            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{FileName}:pattern_threshold = {n.Int} 越出 int 域 —— 不得静默截断");
                return 0;
            }

            int threshold = (int)n.Int;
            if (threshold < CasePatternSchema.MinThreshold)
            {
                errors.Add($"{FileName}:pattern_threshold = {threshold} < {CasePatternSchema.MinThreshold} —— " +
                           "GDD :480 安全范围 ≥ 2(1 = 单例即同源,归纳退化成猜测,且 anchor_case 无「跨过阈值的那一例」语义)");
                return 0;
            }

            return threshold;
        }
    }
}
