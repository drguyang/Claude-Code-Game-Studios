// `world_poi.json`(POI **定义** · ADR-022 §三导出契约)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(导出契约)· §六(边界表:「POI **状态**:✖ 不碰」)
//   ADR-021 §①(定义 = 派生态,进烘焙逻辑层;状态 = 模拟态,进世界流,唯一写者 = 6)
//   ADR-014 §三(白名单 / 未知键 = 构建失败)· §四(聚合 throw)
//   `production/epics/level-tool/story-004-world-poi-and-resources.md`(AC-1)
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldEcozonesBinder` 同款头注)。
//
// ⚠️ **`level_size` 是「越界判据」的载体,不是装饰**:JSON 里每条 POI 都带格坐标,
//    若不带声明尺寸,「这一格在不在世界里」只有在**运行期填格**时才会暴露(下标越界
//    或静默落到别的格)。带上它,越界在**烘焙期**就是硬失败(EC-6-7 的口径)。
//
// ⚠️ **本文件不出现 `PoiState`**(story-004 AC-6 的机械判据覆盖整个工具程序集)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Level;   // PoiSpec / PoiSource
using DaYiJingCheng.Sim.Contracts;       // WorldPos

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_poi.json</c>(POI 定义)的阶段 2 绑定器。</summary>
    public static class WorldPoiBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_poi.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys = { "schema_version", "level_id", "level_size", "pois" };

        /// <summary>行对象白名单。</summary>
        internal static readonly string[] RowKeys = { "poi_id", "cell", "type", "guard_ref" };

        /// <summary>绑定结果(已通过结构门 + 越界门)。</summary>
        public readonly struct BindResult
        {
            /// <summary>POI 定义集(按 `poi_id` 升序)。</summary>
            public readonly IReadOnlyList<PoiSpec> Pois;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>声明尺寸(越界门用;不进 cooked —— 几何产物已有尺寸)。</summary>
            public readonly int SizeX, SizeY, SizeZ;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<PoiSpec> pois, uint schemaVersion, int sx, int sy, int sz)
            {
                Pois = pois; SchemaVersion = schemaVersion; SizeX = sx; SizeY = sy; SizeZ = sz;
            }
        }

        /// <summary>绑定 + 结构门 + 越界门(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 结构门 / 越界(聚合后一次 throw)。</exception>
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
                DaYiJingCheng.Gameplay.Presentation.WorldPoiCookedCodec.ExpectedSchemaVersion,
                errors);

            ClinicMachineSchema.ReadRequiredString(root, "level_id", "根", FileName, errors, out _);

            int sx = 0, sy = 0, sz = 0;
            if (!root.TryGet("level_size", out JsonNode sizeNode)
                || sizeNode.Kind != JsonNodeKind.Array || sizeNode.Items.Count != 3
                || !TryReadI32(sizeNode.Items[0], out sx)
                || !TryReadI32(sizeNode.Items[1], out sy)
                || !TryReadI32(sizeNode.Items[2], out sz))
            {
                errors.Add($"{FileName}:缺 level_size([x, y, z] 三个 i32)—— 它是越界判据的载体");
            }
            else if (sx < 1 || sy < 1 || sz < 1)
            {
                errors.Add($"{FileName}:level_size = [{sx}, {sy}, {sz}] 每轴须 ≥ 1");
            }

            var pois = new List<PoiSpec>();

            if (!root.TryGet("pois", out JsonNode rows) || rows.Kind != JsonNodeKind.Array)
            {
                errors.Add(
                    $"{FileName}:缺 pois 数组 —— POI 定义集是本产物的全部内容;缺了它" +
                    "`PoiStateMachine` 的 `_poiIdSet` 会零成员,`PoiStateChanged` 永不发出");
                throw new BakeValidationException(errors);
            }

            for (int i = 0; i < rows.Items.Count; i++)
            {
                PoiSpec? spec = ParseRow(rows.Items[i], i, errors);
                if (spec.HasValue) pois.Add(spec.Value);
            }

            // 越界门:结构门全过之后才跑(形状都不对时跑它只会产出误报)。
            if (errors.Count == 0)
            {
                var probe = new PoiSource(pois);   // 顺带跑 id 唯一 / ≥ 0 / 同格不重复
                try { probe.ValidateAgainstLevel(sx, sy, sz); }
                catch (InvalidOperationException ex) { errors.Add(ex.Message); }
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            var sorted = new List<PoiSpec>(pois);
            sorted.Sort((a, b) => a.PoiId.CompareTo(b.PoiId));
            return new BindResult(sorted, schemaVersion, sx, sy, sz);
        }

        /// <summary>解析一行 POI 定义(白名单 + 类型 + 格三元组)。</summary>
        private static PoiSpec? ParseRow(JsonNode row, int i, List<string> errors)
        {
            string where = $"pois[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(row, RowKeys, where, FileName, errors);

            // poi_id —— i32,允许负值**只为了让 PoiSource 报出「≥ 0」那条**(不在这里重复判)
            if (!row.TryGet("poi_id", out JsonNode idNode) || !TryReadI32(idNode, out int poiId))
            {
                errors.Add($"{FileName}:{where}.poi_id 须为 i32 整数");
                return null;
            }

            if (!row.TryGet("cell", out JsonNode cellNode)
                || cellNode.Kind != JsonNodeKind.Array || cellNode.Items.Count != 3
                || !TryReadI32(cellNode.Items[0], out int cx)
                || !TryReadI32(cellNode.Items[1], out int cy)
                || !TryReadI32(cellNode.Items[2], out int cz))
            {
                errors.Add($"{FileName}:{where}.cell 须为 `[x, y, z]` **三元组** i32" +
                           "(POI 是三维格上的点 —— 与生态区多边形的 `[x, z]` 二元组不同)");
                return null;
            }

            if (!row.TryGet("type", out JsonNode typeNode) || !TryReadI32(typeNode, out int type))
            {
                errors.Add($"{FileName}:{where}.type 须为 i32 整数(取值闭集归 6 的 GDD)");
                return null;
            }

            if (!row.TryGet("guard_ref", out JsonNode guardNode) || !TryReadI32(guardNode, out int guardRef))
            {
                errors.Add($"{FileName}:{where}.guard_ref 须为 i32 整数(守卫**档位**,不是敌人 id)");
                return null;
            }

            return new PoiSpec(poiId, new WorldPos(cx, cy, cz), type, guardRef);
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
