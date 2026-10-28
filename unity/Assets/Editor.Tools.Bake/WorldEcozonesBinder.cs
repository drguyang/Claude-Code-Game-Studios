// `world_ecozones.json`(生态区多边形 · ADR-022 §三导出契约)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(导出契约)· §四 **C4**(多边形合法性 = 硬失败)· §二(唯一作者 = 关卡工具)
//   GDD `world-and-ecozones.md` **F-6-3**(开集语义 + 裁决序)· **AC-6-19**(五类退化)
//     · **EC-6-7**(烘焙期硬失败,不进装机产物)
//   ADR-014 §三(白名单 / 未知键 = 构建失败)· §四(聚合 throw)
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldGeometryBinder` 同款头注)。
//
// ⚠️ **程序集引用边 `Editor.Tools.Bake → Editor.Tools.Level` 是本 story 新增的**
//    (ADR-025 §① 装配清单里的一条**已登记**的边):本件复用关卡工具的 **C4 检查器**
//    (`EcozonePolygonCheck`),使「导出侧」与「烘焙侧」跑的是**同一个**检查器而非两份实现
//    ⇒ 手写 `world_ecozones.json`(违反 ADR-022 §二)也逃不过 C4。
//    方向 = Bake → Level,**无环**(`Editor.Tools.Level` 不引用本程序集)。
//    ⚠️ asmdef 是**严格 JSON**,不能写注释 —— 理由只能落在代码里(本节即其落点)。
//
// ⚠️ **门分两层,刻意不合并**:
//   ① **结构门**(本文件)—— schema / 白名单 / 类型 / 长度 / id 范围。它们回答「这份 JSON 是不是
//      `world_ecozones` 的形状」。
//   ② **几何门**(`EcozonePolygonCheck`,住在 `Editor.Tools.Level`)—— C4 五项。它回答
//      「这组多边形本身合法吗」。
//   两层的**报文前缀不同**(`world_ecozones:` vs `world_ecozones:` + 几何项名),
//   因为「形状不对」与「形状对但几何非法」要能分开诊断。
//   ⚠️ 几何门与**导出器**用的是**同一个**检查器,不是两份实现 —— 手写该 JSON(违反 ADR-022 §二)
//      也逃不过 C4,这是「唯一作者」这条规则在**烘焙侧**的可证伪落点。

using System;
using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Level;   // EcozoneZoneSpec / EcozoneSource / EcozonePolygonCheck(C4)
using DaYiJingCheng.Sim.Contracts;       // WorldPos

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_ecozones.json</c>(生态区多边形)的阶段 2 绑定器。</summary>
    public static class WorldEcozonesBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_ecozones.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys = { "schema_version", "level_id", "zones" };

        /// <summary>行对象白名单。</summary>
        internal static readonly string[] ZoneKeys = { "ecozone_id", "name", "vertices" };

        /// <summary>绑定结果(已通过结构门 + C4)。</summary>
        public readonly struct BindResult
        {
            /// <summary>生态区行集(按 `ecozone_id` 升序)。</summary>
            public readonly IReadOnlyList<EcozoneZoneSpec> Zones;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<EcozoneZoneSpec> zones, uint schemaVersion)
            {
                Zones = zones; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 结构门 + C4(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 结构门 / C4 失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 根须为对象(实得 {root.Kind})" });

            ClinicMachineSchema.RejectUnknownKeys(root, RootKeys, "根", FileName, errors);

            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(
                root, FileName,
                DaYiJingCheng.Gameplay.Presentation.WorldEcozonesCookedCodec.ExpectedSchemaVersion,
                errors);

            ClinicMachineSchema.ReadRequiredString(root, "level_id", "根", FileName, errors, out _);

            var zones = new List<EcozoneZoneSpec>();

            if (!root.TryGet("zones", out JsonNode zonesNode) || zonesNode.Kind != JsonNodeKind.Array)
            {
                errors.Add(
                    $"{FileName}:缺 zones 数组 —— 生态区集合是本产物的全部内容;缺了它" +
                    "`EcozoneOf` 会恒回 None,而那是 story-003 要消灭的状态");
                throw new BakeValidationException(errors);
            }

            for (int i = 0; i < zonesNode.Items.Count; i++)
            {
                EcozoneZoneSpec? zone = ParseZone(zonesNode.Items[i], i, errors);
                if (zone.HasValue) zones.Add(zone.Value);
            }

            // 结构门全过之后才跑几何门 —— 形状都不对时跑 C4 只会产出误报。
            if (errors.Count == 0)
            {
                var source = new EcozoneSource(zones);
                if (EcozonePolygonCheck.TryCheck(source, out List<string> c4))
                    errors.AddRange(c4);
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            // 行序由**数据**定(id 升序),不由书写顺序定 —— cooked 的行序是产物的一部分。
            zones.Sort((a, b) => a.Id.CompareTo(b.Id));
            return new BindResult(zones, schemaVersion);
        }

        /// <summary>解析一个生态区行(白名单 + 类型 + 顶点二元组)。</summary>
        private static EcozoneZoneSpec? ParseZone(JsonNode row, int i, List<string> errors)
        {
            string where = $"zones[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(row, ZoneKeys, where, FileName, errors);

            // ecozone_id —— i32,允许负值**只为了让 C4 报出「≥ 0」那条**(不在这里重复判)
            if (!row.TryGet("ecozone_id", out JsonNode idNode) || idNode.Kind != JsonNodeKind.Integer
                || idNode.Int < int.MinValue || idNode.Int > int.MaxValue)
            {
                errors.Add(
                    $"{FileName}:{where}.ecozone_id 须为 i32 整数" +
                    (row.TryGet("ecozone_id", out JsonNode bad) ? $"(实得 {bad.Kind}={bad.RawText})" : "(缺字段)"));
                return null;
            }

            if (!ClinicMachineSchema.ReadRequiredString(row, "name", where, FileName, errors, out string name))
                return null;

            if (!row.TryGet("vertices", out JsonNode vNode) || vNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:{where} 缺 vertices 数组([[x, z], ...])");
                return null;
            }

            var vertices = new WorldPos[vNode.Items.Count];
            for (int v = 0; v < vNode.Items.Count; v++)
            {
                JsonNode pair = vNode.Items[v];
                if (pair.Kind != JsonNodeKind.Array || pair.Items.Count != 2)
                {
                    errors.Add(
                        $"{FileName}:{where}.vertices[{v}] 须为 `[x, z]` **二元组**" +
                        "(GDD F-6-3 §四:y 不参与判定 ⇒ 顶点不带 y。)");
                    return null;
                }
                if (!TryReadI32(pair.Items[0], out int px) || !TryReadI32(pair.Items[1], out int pz))
                {
                    errors.Add($"{FileName}:{where}.vertices[{v}] 的 x / z 须为 i32 整数");
                    return null;
                }
                // y 恒 0:它不参与任何判定(`EcozonePolygon` 只读 X / Z),写 0 只是形状对齐。
                vertices[v] = new WorldPos(px, 0, pz);
            }

            return new EcozoneZoneSpec((int)idNode.Int, name, vertices);
        }

        /// <summary>读一个 i32(越域 / 非整数 ⇒ false)。</summary>
        private static bool TryReadI32(JsonNode n, out int value)
        {
            value = 0;
            if (n.Kind != JsonNodeKind.Integer || n.Int < int.MinValue || n.Int > int.MaxValue)
                return false;
            value = (int)n.Int;
            return true;
        }
    }
}
