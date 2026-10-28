// `world_geometry.json`(世界几何 · ADR-022 §三导出契约)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(导出契约)· §二(唯一作者 = 关卡工具)· ADR-015 §三(`WorldPos` 整数格 /
//     格边长以**整数毫米**表达)· ADR-014 §三(根 / 行白名单;未知键 = 构建失败)· §四(聚合 throw)
//   GDD `world-and-ecozones.md` F-6-1(`LATTICE_SIZE`)· AC-6-08(可走性用 `walkable` 表达,不用速度 0)
//   `production/epics/level-tool/story-002-world-geometry-and-terrain.md` AC-1 / AC-5
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldBuildSlotsBinder` 同款头注):
//    EditMode 测试可直驱,菜单层才负责落盘与组注册。
//
// ⚠️ **四条域门**(任一违例 ⇒ 聚合 `BakeValidationException`):
//   ① 声明尺寸非法(任一轴 < 1 / 格总数超 int 域)
//   ② `LATTICE_SIZE` ≤ 0 —— F-6-1 的隧穿不等式是 `SPEED_MAX × MAX_DT × SAFETY ≤ LATTICE_SIZE`,
//      左端恒正 ⇒ 右端为 0 时不等式**恒假但无人求值**(没有世界就没有 Step),退化面是静默的
//   ③ `walkable` 长度 ≠ `W×H×D`,或含非 0/1 值 —— 长度不齐 = 地表与几何不是同一张图纸
//   ④ **全不可走** —— 与「全可走」是同一个静默失败的两个方向:全世界都走不到时,
//      `IsEffectivelyWalkable` 恒 false,「哪里走不了」这一类判据同样没有观察面,
//      而症状(玩家卡在原地)会被误诊为控制器故障
//
// ⚠️ 报文前缀固定 `world_geometry:`,各门再点自己的判据词 —— 测试按**字面量**断言关键词。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_geometry.json</c>(世界几何)的阶段 2 绑定器。</summary>
    public static class WorldGeometryBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_geometry.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys =
            { "schema_version", "level_id", "world_size", "lattice_size_mm", "walkable" };

        /// <summary>绑定结果(已通过全部域门)。</summary>
        public readonly struct BindResult
        {
            /// <summary>声明范围 X(格数)。</summary>
            public readonly int Width;
            /// <summary>声明范围 Y(格数)。</summary>
            public readonly int Height;
            /// <summary>声明范围 Z(格数)。</summary>
            public readonly int Depth;

            /// <summary>格边长 `LATTICE_SIZE`(整数毫米;F-6-1)。</summary>
            public readonly int LatticeSizeMm;

            /// <summary>每格可走性(0/1;索引 = `(x*H + y)*D + z`)。</summary>
            public readonly IReadOnlyList<int> Walkable;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(int width, int height, int depth, int latticeSizeMm,
                              IReadOnlyList<int> walkable, uint schemaVersion)
            {
                Width = width; Height = height; Depth = depth;
                LatticeSizeMm = latticeSizeMm; Walkable = walkable; SchemaVersion = schemaVersion;
            }

            /// <summary>世界总格数。</summary>
            public int TotalCells => Width * Height * Depth;
        }

        /// <summary>绑定 + 四条域门(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
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

            // 期望值**单一出处** = 读方 codec 的常量(承 WorldBuildSlotsBinder 同款口径)。
            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(
                root, FileName,
                DaYiJingCheng.Gameplay.Presentation.WorldGeometryCookedCodec.ExpectedSchemaVersion,
                errors);

            // `level_id` 是人读标签(装配期不按它选关卡 —— 多关卡装载归后续轮)。
            // 仍**必填**:缺了它,一份 world_geometry 产物说不出自己属于哪个关卡。
            ClinicMachineSchema.ReadRequiredString(
                root, "level_id", "根", FileName, errors, out _);

            // ① 声明尺寸
            int width = 0, height = 0, depth = 0;
            bool sizeOk = TryReadWorldSize(root, out width, out height, out depth, errors);
            long total = sizeOk ? (long)width * height * depth : 0;

            // ② LATTICE_SIZE
            int latticeSizeMm = 0;
            bool latticeOk = TryReadLatticeSize(root, out latticeSizeMm, errors);

            // ③ / ④ 可走性数组
            IReadOnlyList<int> walkable = null;
            if (sizeOk) walkable = TryReadWalkable(root, total, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);

            // `latticeOk` / `walkable != null` 在 errors 为空时必然为真(上面已 throw),
            // 这里不做 `if (x == null) throw` 的冗余分支 —— 那会造出不可达的失败通道。
            return new BindResult(width, height, depth, latticeSizeMm, walkable, schemaVersion);
        }

        /// <summary>`world_size` —— `[W,H,D]` 三个 ≥ 1 的整数。</summary>
        private static bool TryReadWorldSize(
            JsonNode root, out int width, out int height, out int depth, List<string> errors)
        {
            width = height = depth = 0;

            if (!root.TryGet("world_size", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 world_size 数组 —— 没有声明范围就判不出 walkable 长度对不对");
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
                if (item.Kind != JsonNodeKind.Integer ||
                    item.Int < 1 || item.Int > int.MaxValue)
                {
                    errors.Add(
                        $"{FileName}:world_size[{c}] 须为 [1, {int.MaxValue}] 的整数" +
                        $"(实得 {item.Kind}={item.RawText})");
                    return false;
                }
                comp[c] = (int)item.Int;
            }

            long cells = (long)comp[0] * comp[1] * comp[2];
            if (cells > int.MaxValue)
            {
                errors.Add(
                    $"{FileName}:world_size = ({comp[0]},{comp[1]},{comp[2]}) 格总数 {cells} 超出 int 域 —— " +
                    "一维索引公式 (x*H+y)*D+z 不可表示");
                return false;
            }

            width = comp[0]; height = comp[1]; depth = comp[2];
            return true;
        }

        /// <summary>`lattice_size_mm` —— 正整数(域门 ②)。</summary>
        private static bool TryReadLatticeSize(JsonNode root, out int latticeSizeMm, List<string> errors)
        {
            latticeSizeMm = 0;
            if (!ClinicMachineSchema.ReadRequiredPositiveInt(
                    root, "lattice_size_mm", "根", FileName, errors, out long value))
                return false;
            if (value > int.MaxValue)
            {
                errors.Add($"{FileName}:lattice_size_mm = {value} 超出 int 域(毫米)");
                return false;
            }
            latticeSizeMm = (int)value;
            return true;
        }

        /// <summary>`walkable` —— 长度须恰为 `W×H×D`,每项 ∈ {0,1},且**不得全 0**(域门 ③/④)。</summary>
        private static IReadOnlyList<int> TryReadWalkable(JsonNode root, long total, List<string> errors)
        {
            if (!root.TryGet("walkable", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add(
                    $"{FileName}:缺 walkable 数组 —— 可走性是本产物的**全部内容**;" +
                    "缺了它,运行期只能合成「全可走」,而那是本 story 要消灭的状态(AC-4)");
                return null;
            }

            if (n.Items.Count != total)
            {
                errors.Add(
                    $"{FileName}:walkable 长度 = {n.Items.Count},须等于 world_size 的格总数 {total} —— " +
                    "长度不齐 = 地表与几何不是同一张图纸");
                return null;
            }

            var values = new int[n.Items.Count];
            int walkableCount = 0;
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.Integer || (item.Int != 0 && item.Int != 1))
                {
                    errors.Add(
                        $"{FileName}:walkable[{i}] 须为 0 或 1(实得 {item.Kind}={item.RawText}) —— " +
                        "非 0/1 会让读方猜「非零即真」,而 0b10 之类的位值是真值域里的假值");
                    return null;
                }
                values[i] = (int)item.Int;
                if (values[i] == 1) walkableCount++;
            }

            // 域门 ④:全不可走。与「全可走」同一个静默失败的两个方向。
            if (walkableCount == 0)
            {
                errors.Add(
                    $"{FileName}:walkable 全为 0 —— **没有任何一格可通行**。" +
                    "`IsEffectivelyWalkable` 会恒 false,「哪里走不了」这条判据同样零观察面," +
                    "而症状(玩家卡在出生点)会被误诊为控制器故障");
                return null;
            }

            return Array.AsReadOnly(values);
        }
    }
}
