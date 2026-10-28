// `world_resources.json`(资源点 · ADR-022 §三导出契约)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(导出契约)· ADR-014 §三/§四(白名单 / 聚合 throw)
//   GDD `foraging.md`(17 采集消费资源点)
//   `production/epics/level-tool/story-004-world-poi-and-resources.md`(AC-2)
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldPoiBinder` 同款头注)。
//
// ⚠️ **`ecozone_id` 只做类型 / 值域检查,不校验存在性**(story-004 §Implementation Notes)。
//    存在性是**跨数据集**判据,归 S6 的 C 系列 —— 在这里顺手加一条会让「单独重导一个数据集」
//    变得不可能(见 `ResourceSource.cs` 文件头)。
//
// ⚠️ **`level_size` 是同 `WorldPoiBinder` 的越界判据载体**(理由同)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.EditorTools.Level;   // ResourceSpec / ResourceSource
using DaYiJingCheng.Sim.Contracts;       // WorldPos

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_resources.json</c>(资源点)的阶段 2 绑定器。</summary>
    public static class WorldResourcesBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_resources.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys =
            { "schema_version", "level_id", "level_size", "resources" };

        /// <summary>行对象白名单。</summary>
        internal static readonly string[] RowKeys = { "resource_id", "cell", "ecozone_id" };

        /// <summary>绑定结果(已通过结构门 + 越界门 + 同格门)。</summary>
        public readonly struct BindResult
        {
            /// <summary>资源点集(按 `(resource_id, x, y, z, ecozone_id)` 升序)。</summary>
            public readonly IReadOnlyList<ResourceSpec> Resources;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>声明尺寸(越界门用;不进 cooked)。</summary>
            public readonly int SizeX, SizeY, SizeZ;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<ResourceSpec> resources, uint schemaVersion,
                              int sx, int sy, int sz)
            {
                Resources = resources; SchemaVersion = schemaVersion;
                SizeX = sx; SizeY = sy; SizeZ = sz;
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
                DaYiJingCheng.Gameplay.Presentation.WorldResourcesCookedCodec.ExpectedSchemaVersion,
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

            var rows = new List<ResourceSpec>();

            if (!root.TryGet("resources", out JsonNode arr) || arr.Kind != JsonNodeKind.Array)
            {
                errors.Add(
                    $"{FileName}:缺 resources 数组 —— 资源点集是本产物的全部内容;" +
                    "缺了它 `ResourceGrid` 会全 -1");
                throw new BakeValidationException(errors);
            }

            for (int i = 0; i < arr.Items.Count; i++)
            {
                ResourceSpec? spec = ParseRow(arr.Items[i], i, errors);
                if (spec.HasValue) rows.Add(spec.Value);
            }

            if (errors.Count == 0)
            {
                var probe = new ResourceSource(rows);   // 顺带跑 id ≥ 0 / 同格不重复 / ecozone 值域
                try { probe.ValidateAgainstLevel(sx, sy, sz); }
                catch (InvalidOperationException ex) { errors.Add(ex.Message); }
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            var sorted = new List<ResourceSpec>(rows);
            sorted.Sort((a, b) =>
            {
                int c = a.ResourceId.CompareTo(b.ResourceId); if (c != 0) return c;
                c = a.Cell.X.CompareTo(b.Cell.X); if (c != 0) return c;
                c = a.Cell.Y.CompareTo(b.Cell.Y); if (c != 0) return c;
                c = a.Cell.Z.CompareTo(b.Cell.Z); if (c != 0) return c;
                return a.EcozoneId.CompareTo(b.EcozoneId);
            });
            return new BindResult(sorted, schemaVersion, sx, sy, sz);
        }

        /// <summary>解析一行资源点(白名单 + 类型 + 格三元组 + 生态区倾向)。</summary>
        private static ResourceSpec? ParseRow(JsonNode row, int i, List<string> errors)
        {
            string where = $"resources[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(row, RowKeys, where, FileName, errors);

            // resource_id —— i32,允许负值**只为了让 ResourceSource 报出「≥ 0」那条**
            if (!row.TryGet("resource_id", out JsonNode idNode) || !TryReadI32(idNode, out int resId))
            {
                errors.Add($"{FileName}:{where}.resource_id 须为 i32 整数");
                return null;
            }

            if (!row.TryGet("cell", out JsonNode cellNode)
                || cellNode.Kind != JsonNodeKind.Array || cellNode.Items.Count != 3
                || !TryReadI32(cellNode.Items[0], out int cx)
                || !TryReadI32(cellNode.Items[1], out int cy)
                || !TryReadI32(cellNode.Items[2], out int cz))
            {
                errors.Add($"{FileName}:{where}.cell 须为 `[x, y, z]` **三元组** i32");
                return null;
            }

            // ecozone_id —— 缺省 = 无倾向(`-1`)。显式写出比依赖默认值更好,但缺省是合法的。
            int ecozoneId = ResourceSpec.NoEcozone;
            if (row.TryGet("ecozone_id", out JsonNode ecoNode) && !TryReadI32(ecoNode, out ecozoneId))
            {
                errors.Add($"{FileName}:{where}.ecozone_id 须为 i32 整数(-1 = 无倾向)");
                return null;
            }

            return new ResourceSpec(resId, new WorldPos(cx, cy, cz), ecozoneId);
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
