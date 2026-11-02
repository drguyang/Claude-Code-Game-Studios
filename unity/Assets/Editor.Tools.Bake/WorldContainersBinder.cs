// `world_containers.json`(开档容器物品实例 · 2026-11-02 `AC-4-22`)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §二(唯一作者 = 关卡工具)· §三(导出契约;本数据集为**加性第 8 类**)
//   ADR-015 §三(`WorldPos = (i32,i32,i32)` 单一整数格)
//   ADR-014 §三(根对象 / 行对象白名单;未知键 = 构建失败)· §四(聚合 throw)
//   GDD `interaction-system.md` 规则二(`Container` 的 `StableId = instance_id`)
//
// ⚠️ **五条域门**(任一违例 ⇒ 聚合 `BakeValidationException`):
//   ① 空表 —— 空表与「未声明」在产物上不可分
//   ② `instance_id` 重复 —— 它是 F-4.1 的第三键,重复即「无胜者」
//   ③ 同格两条 —— 「这一格是哪一个容器」成为隐式规则
//   ④ 格超出 `world_size` 声明范围 —— 越界容器运行期永远选不中
//   ⑤ `instance_id` 为负 —— 负值是事件侧「无实例」哨兵,不得占用
//
// ⚠️ 本件刻意不碰 `UnityEngine` / Addressables(承 `WorldBuildSlotsBinder` 同款头注)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_containers.json</c>(开档容器实例)的阶段 2 绑定器。</summary>
    public static class WorldContainersBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_containers.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys =
            { "schema_version", "level_id", "world_size", "containers" };

        /// <summary>行对象白名单。</summary>
        internal static readonly string[] RowKeys = { "instance_id", "cell" };

        /// <summary>一行容器实例。</summary>
        public readonly struct ContainerRow
        {
            /// <summary>物品实例 id(≥ 0;与掉落共用 `IIdAuthority` 空间)。</summary>
            public readonly long InstanceId;

            /// <summary>格 x(i32)。</summary>
            public readonly int X;
            /// <summary>格 y(i32)。</summary>
            public readonly int Y;
            /// <summary>格 z(i32)。</summary>
            public readonly int Z;

            /// <summary>构造一行。</summary>
            public ContainerRow(long instanceId, int x, int y, int z)
            {
                InstanceId = instanceId; X = x; Y = y; Z = z;
            }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>行集(声明序;编码时另行按 id 全序排序)。</summary>
            public readonly IReadOnlyList<ContainerRow> Rows;

            /// <summary>声明范围 X(格数)。</summary>
            public readonly int Width;
            /// <summary>声明范围 Y(格数)。</summary>
            public readonly int Height;
            /// <summary>声明范围 Z(格数)。</summary>
            public readonly int Depth;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<ContainerRow> rows, int width, int height, int depth,
                              uint schemaVersion)
            {
                Rows = rows; Width = width; Height = height; Depth = depth; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 五条域门(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
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
                DaYiJingCheng.Gameplay.Presentation.WorldContainersCookedCodec.ExpectedSchemaVersion,
                errors);

            int width = 0, height = 0, depth = 0;
            bool sizeOk = TryReadWorldSize(root, out width, out height, out depth, errors);

            if (!root.TryGet("containers", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 containers 数组 —— 空表与「未声明」不可分");
                throw new BakeValidationException(errors);
            }

            var rows = new List<ContainerRow>();
            var seenIds = new HashSet<long>();
            var seenCells = new HashSet<(int, int, int)>();
            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                ContainerRow? row = ParseRow(rowsNode.Items[i], i, sizeOk, width, height, depth,
                                             seenIds, seenCells, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rowsNode.Items.Count == 0 && errors.Count == 0)
                errors.Add(
                    $"{FileName}:containers 为空表 —— 空表与「未声明」不可分,且开档无柜," +
                    "`AC-4-22` 的柜半边将永远不可达,拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, width, height, depth, schemaVersion);
        }

        /// <summary>`world_size` —— `[W,H,D]` 三个 ≥ 1 的整数。</summary>
        private static bool TryReadWorldSize(
            JsonNode root, out int width, out int height, out int depth, List<string> errors)
        {
            width = height = depth = 0;

            if (!root.TryGet("world_size", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 world_size 数组 —— 没有声明范围就无法判「格是否越界」");
                return false;
            }
            if (n.Items.Count != 3)
            {
                errors.Add($"{FileName}:world_size 须为 [W, H, D] 三元数组(实得 {n.Items.Count} 项)");
                return false;
            }

            var comp = new int[3];
            for (int c = 0; c < 3; c++)
            {
                JsonNode item = n.Items[c];
                if (item.Kind != JsonNodeKind.Integer)
                {
                    errors.Add(
                        $"{FileName}:world_size[{c}] 须为 JSON 整数(实得 {item.Kind}={item.RawText})");
                    return false;
                }
                if (item.Int < 1 || item.Int > int.MaxValue)
                {
                    errors.Add($"{FileName}:world_size[{c}] = {item.Int} 须在 [1, {int.MaxValue}]");
                    return false;
                }
                comp[c] = (int)item.Int;
            }

            width = comp[0]; height = comp[1]; depth = comp[2];
            return true;
        }

        /// <summary>逐行解析(白名单 + 域门 ②–⑤)。</summary>
        private static ContainerRow? ParseRow(
            JsonNode row, int i, bool sizeOk, int width, int height, int depth,
            HashSet<long> seenIds, HashSet<(int, int, int)> seenCells, List<string> errors)
        {
            string where = $"containers[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(row, RowKeys, where, FileName, errors);

            bool ok = true;

            long instanceId = 0;
            if (!row.TryGet("instance_id", out JsonNode idNode) || idNode.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{FileName}:{where}.instance_id 须为 JSON 整数");
                ok = false;
            }
            else
            {
                instanceId = idNode.Int;
                if (instanceId < 0)
                {
                    errors.Add(
                        $"{FileName}:{where}.instance_id = {instanceId} —— 须 ≥ 0" +
                        "(负值是事件侧「无实例」哨兵,不得占用)");
                    ok = false;
                }
            }

            int x = 0, y = 0, z = 0;
            if (!TryReadCell(row, where, out x, out y, out z, errors)) ok = false;

            if (!ok) return null;

            if (!seenIds.Add(instanceId))
                errors.Add(
                    $"{FileName}:{where}.instance_id = {instanceId} 重复 —— " +
                    "它是 F-4.1 的第三键,重复即「无胜者」");

            if (!seenCells.Add((x, y, z)))
                errors.Add(
                    $"{FileName}:{where} 格 ({x},{y},{z}) 出现两条 —— " +
                    "同格两个容器会让「这一格是哪一个」成为隐式规则");

            if (sizeOk && (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= depth))
                errors.Add(
                    $"{FileName}:{where} 格 ({x},{y},{z}) 超出声明范围 " +
                    $"{width}×{height}×{depth} —— 越界容器运行期永远选不中");

            return new ContainerRow(instanceId, x, y, z);
        }

        /// <summary>`cell` —— `[x,y,z]` i32 三元组。</summary>
        private static bool TryReadCell(
            JsonNode row, string where, out int x, out int y, out int z, List<string> errors)
        {
            x = y = z = 0;
            if (!row.TryGet("cell", out JsonNode cell) || cell.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:{where}.cell 须为 [x, y, z] 三元数组");
                return false;
            }
            if (cell.Items.Count != 3)
            {
                errors.Add($"{FileName}:{where}.cell 须为三元数组(实得 {cell.Items.Count} 项)");
                return false;
            }

            var comp = new int[3];
            for (int c = 0; c < 3; c++)
            {
                JsonNode item = cell.Items[c];
                if (item.Kind != JsonNodeKind.Integer)
                {
                    errors.Add(
                        $"{FileName}:{where}.cell[{c}] 须为 JSON 整数(实得 {item.Kind}={item.RawText})");
                    return false;
                }
                if (item.Int < int.MinValue || item.Int > int.MaxValue)
                {
                    errors.Add($"{FileName}:{where}.cell[{c}] = {item.Int} 超出 i32");
                    return false;
                }
                comp[c] = (int)item.Int;
            }
            x = comp[0]; y = comp[1]; z = comp[2];
            return true;
        }
    }
}
