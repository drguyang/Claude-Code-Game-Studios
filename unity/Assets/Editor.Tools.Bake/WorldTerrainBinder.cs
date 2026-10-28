// `world_terrain.json`(地形表 · ADR-022 §三导出契约)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(导出契约:TERRAIN_TABLE + terrain_id 表 + slopeLimit / stepOffset)
//   GDD `world-and-ecozones.md` **F-6-2**(`K_TERRAIN_MAX := max over terrain_id of K_speed`;
//     空表 ⇒ 装载期硬失败)· **AC-6-08**(每行三列 > 0;`K_speed == 1` **恰好一行**)
//   ADR-014 §三(白名单 / 未知键 = 构建失败)· §四(聚合 throw;`Fix` 只接受字符串)
//   ADR-015 §一之补(`slopeLimit` / `stepOffset` 的真源 = 关卡工具;系统 1 只是执行者)
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldBuildSlotsBinder` 同款头注)。
//
// ⚠️ **六条域门**(任一违例 ⇒ 聚合 `BakeValidationException`):
//   ① `terrain_table` 为空 —— F-6-2 的 `max over 空集` **无定义** ⇒ 装载期硬失败
//      (这一条同时是 AC-5 的"空表硬失败"的**第一道**;读侧 codec 有第二道,纵深)
//   ② `terrain_id` < 0 或重复 —— 重复会让「取哪一行」成为隐式规则
//   ③ 任一行 `k_speed ≤ 0` / `k_accel ≤ 0` / `k_decel ≤ 0` —— AC-6-08。
//      ⚠️ 「走不动」不是用速度 0 表达的(`walkable = false` 才是):速度 0 会让 F-6-1 的
//      隧穿断言**恒绿**(永不跨格 ⇒ 永不违反),那是静默失效
//   ④ `k_speed == 1` 的行**不恰好一行** —— 零行没有基准可参照,多行则"哪一行是基准"成为隐式规则
//   ⑤ `cell_terrain_id` 长度 ≠ 格总数 / 含表里不存在的 id —— 后者在运行期会取出越界行
//   ⑥ `slope_limit_deg_x100` / `step_offset_mm` 超出 `LatticeParamQuantization` 的合法域
//
// ⚠️ **行序 = `terrain_id` 升序**:声明序在作者重排 `terrain_table[]` 时不承载语义,
//    而 cooked 的行序决定 `cell_terrain_id` 解析出的下标 ⇒ 必须由**数据**定序,不能由书写顺序定。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // Fix / LatticeParamQuantization

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_terrain.json</c>(地形表)的阶段 2 绑定器。</summary>
    public static class WorldTerrainBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_terrain.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys =
        {
            "schema_version", "level_id", "world_size", "terrain_table",
            "cell_terrain_id", "slope_limit_deg_x100", "step_offset_mm",
        };

        /// <summary>行对象白名单。</summary>
        internal static readonly string[] RowKeys =
            { "terrain_id", "name", "k_speed", "k_accel", "k_decel" };

        /// <summary>`TERRAIN_TABLE` 的一行(绑定产物;已排序、已校验)。</summary>
        public readonly struct TerrainRow
        {
            /// <summary>地貌材质 id(≥ 0;表内唯一)。</summary>
            public readonly int TerrainId;

            /// <summary>作者可读名(ASCII;仅用于报文与作者态可读性 —— **不进 cooked**)。</summary>
            public readonly string Name;

            /// <summary>速度档位乘数(`> 0`;基准地貌 = 1)。</summary>
            public readonly int KSpeed;

            /// <summary>加速乘数(`raw > 0`;归 1 的 F-1-3 消费)。</summary>
            public readonly Fix KAccel;

            /// <summary>减速乘数(`raw > 0`;归 1 的 F-1-3 消费)。</summary>
            public readonly Fix KDecel;

            /// <summary>构造一行。</summary>
            public TerrainRow(int terrainId, string name, int kSpeed, Fix kAccel, Fix kDecel)
            {
                TerrainId = terrainId; Name = name; KSpeed = kSpeed; KAccel = kAccel; KDecel = kDecel;
            }
        }

        /// <summary>绑定结果(已通过全部域门)。</summary>
        public readonly struct BindResult
        {
            /// <summary>`TERRAIN_TABLE`(按 `terrain_id` 升序 ⇒ 下标即 `cell_terrain_id` 的解析值)。</summary>
            public readonly IReadOnlyList<TerrainRow> Rows;

            /// <summary>声明范围 X(格数;须与几何产物相等,由装载期断言)。</summary>
            public readonly int Width;
            /// <summary>声明范围 Y(格数)。</summary>
            public readonly int Height;
            /// <summary>声明范围 Z(格数)。</summary>
            public readonly int Depth;

            /// <summary>每格的**表内下标**(已把 `terrain_id` 解析成 `Rows` 的下标)。</summary>
            public readonly IReadOnlyList<int> CellSpeedIndex;

            /// <summary>`slopeLimit` 的量化值(0.01 度)。</summary>
            public readonly int SlopeLimitDegX100;

            /// <summary>`stepOffset` 的量化值(毫米)。</summary>
            public readonly int StepOffsetMm;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(
                IReadOnlyList<TerrainRow> rows, int width, int height, int depth,
                IReadOnlyList<int> cellSpeedIndex, int slopeLimitDegX100, int stepOffsetMm,
                uint schemaVersion)
            {
                Rows = rows; Width = width; Height = height; Depth = depth;
                CellSpeedIndex = cellSpeedIndex;
                SlopeLimitDegX100 = slopeLimitDegX100; StepOffsetMm = stepOffsetMm;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 六条域门(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
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
                DaYiJingCheng.Gameplay.Presentation.WorldTerrainCookedCodec.ExpectedSchemaVersion,
                errors);

            ClinicMachineSchema.ReadRequiredString(root, "level_id", "根", FileName, errors, out _);

            int width = 0, height = 0, depth = 0;
            bool sizeOk = TryReadWorldSize(root, out width, out height, out depth, errors);
            long total = sizeOk ? (long)width * height * depth : 0;

            // ① – ④ 表
            List<TerrainRow> rows = TryReadTable(root, errors);

            // ⑤ 每格 terrain_id(需要表已就绪才解析)
            IReadOnlyList<int> cellIndex = null;
            if (sizeOk && errors.Count == 0)
                cellIndex = TryReadCellTerrainIds(root, rows, total, errors);

            // ⑥ 控制器参数量化值
            int slopeLimit = 0, stepOffset = 0;
            TryReadControllerParams(root, out slopeLimit, out stepOffset, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(
                rows, width, height, depth, cellIndex, slopeLimit, stepOffset, schemaVersion);
        }

        /// <summary>`world_size` —— `[W,H,D]` 三个 ≥ 1 的整数。</summary>
        private static bool TryReadWorldSize(
            JsonNode root, out int width, out int height, out int depth, List<string> errors)
        {
            width = height = depth = 0;

            if (!root.TryGet("world_size", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add(
                    $"{FileName}:缺 world_size 数组 —— 没有它,`cell_terrain_id` 的长度无从判断" +
                    "(也不能与几何产物对账)");
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
                if (item.Kind != JsonNodeKind.Integer || item.Int < 1 || item.Int > int.MaxValue)
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
                    $"{FileName}:world_size = ({comp[0]},{comp[1]},{comp[2]}) 格总数 {cells} 超出 int 域");
                return false;
            }

            width = comp[0]; height = comp[1]; depth = comp[2];
            return true;
        }

        /// <summary>`terrain_table` 的解析与域门 ①–④。返回**已按 terrain_id 升序**的行集。</summary>
        private static List<TerrainRow> TryReadTable(JsonNode root, List<string> errors)
        {
            var rows = new List<TerrainRow>();

            if (!root.TryGet("terrain_table", out JsonNode table) || table.Kind != JsonNodeKind.Array)
            {
                // 域门 ①:缺数组与空表是**同一件事的两个写法**,报到同一条判据上。
                errors.Add(
                    $"{FileName}:terrain_table 缺失或不是数组 —— F-6-2 的 " +
                    "`K_TERRAIN_MAX := max over terrain_id of K_speed`,max over **空集无定义** ⇒ 硬失败。" +
                    "不得用「全 walkable 无地形」代替");
                return rows;
            }

            if (table.Items.Count == 0)
            {
                errors.Add(
                    $"{FileName}:terrain_table 为空表 —— F-6-2 的 `max over 空集` **无定义** ⇒ 硬失败" +
                    "(这是 AC-5 的第一道门;读侧 codec 有第二道,纵深防御)");
                return rows;
            }

            var seenIds = new HashSet<int>();
            int baselineRows = 0;

            for (int i = 0; i < table.Items.Count; i++)
            {
                JsonNode row = table.Items[i];
                string where = $"terrain_table[{i}]";
                if (row.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                    continue;
                }

                ClinicMachineSchema.RejectUnknownKeys(row, RowKeys, where, FileName, errors);

                // terrain_id —— 域门 ② 的「≥ 0」半边
                if (!row.TryGet("terrain_id", out JsonNode idNode)
                    || idNode.Kind != JsonNodeKind.Integer)
                {
                    errors.Add($"{FileName}:{where}.terrain_id 须为 JSON 整数" +
                               (row.TryGet("terrain_id", out JsonNode bad)
                                    ? $"(实得 {bad.Kind}={bad.RawText})" : "(缺字段)"));
                    continue;
                }
                if (idNode.Int < 0 || idNode.Int > int.MaxValue)
                {
                    errors.Add($"{FileName}:{where}.terrain_id = {idNode.Int} 须在 [0, {int.MaxValue}]");
                    continue;
                }
                int terrainId = (int)idNode.Int;

                if (!ClinicMachineSchema.ReadRequiredString(
                        row, "name", where, FileName, errors, out string name))
                    continue;

                if (!ClinicMachineSchema.ReadRequiredPositiveInt(
                        row, "k_speed", where, FileName, errors, out long kSpeed))
                    continue;
                if (kSpeed > int.MaxValue)
                {
                    errors.Add($"{FileName}:{where}.k_speed = {kSpeed} 超出 int 域");
                    continue;
                }

                if (!ClinicMachineSchema.ReadRequiredFix(
                        row, "k_accel", where, FileName, errors, out Fix kAccel))
                    continue;
                if (!ClinicMachineSchema.ReadRequiredFix(
                        row, "k_decel", where, FileName, errors, out Fix kDecel))
                    continue;

                // 域门 ③:`Fix` 侧的 > 0(正整数那两列已由 ReadRequiredPositiveInt 拦住)
                if (kAccel.Raw <= 0)
                {
                    errors.Add($"{FileName}:{where}.k_accel raw = {kAccel.Raw} 须 > 0(AC-6-08)");
                    continue;
                }
                if (kDecel.Raw <= 0)
                {
                    errors.Add($"{FileName}:{where}.k_decel raw = {kDecel.Raw} 须 > 0(AC-6-08)");
                    continue;
                }

                // 域门 ② 的「唯一」半边
                if (!seenIds.Add(terrainId))
                {
                    errors.Add(
                        $"{FileName}:{where}.terrain_id = {terrainId} 重复 —— 同一 id 两行会让" +
                        "「取哪一行」成为隐式规则(静默失序)");
                    continue;
                }

                if (kSpeed == 1) baselineRows++;
                rows.Add(new TerrainRow(terrainId, name, (int)kSpeed, kAccel, kDecel));
            }

            // 域门 ④:`K_speed == 1`(基准地貌)恰好一行。
            if (rows.Count > 0 && baselineRows != 1)
                errors.Add(
                    $"{FileName}:terrain_table 中 k_speed == 1(基准地貌)须**恰好一行**,实际 = {baselineRows} —— " +
                    "零行 ⇒ 没有基准可参照;多行 ⇒ 「哪一行是基准」成为隐式规则");

            // 行序由**数据**定(terrain_id 升序),不由书写顺序定 —— `cell_terrain_id` 解析出的
            // 下标依赖它,而作者重排表不应改变 cooked 字节流。
            rows.Sort((a, b) => a.TerrainId.CompareTo(b.TerrainId));
            return rows;
        }

        /// <summary>`cell_terrain_id` —— 长度须恰为格总数,每项须在表内(域门 ⑤)。</summary>
        private static IReadOnlyList<int> TryReadCellTerrainIds(
            JsonNode root, List<TerrainRow> rows, long total, List<string> errors)
        {
            if (!root.TryGet("cell_terrain_id", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add(
                    $"{FileName}:缺 cell_terrain_id 数组 —— 表只说「有哪些地貌」," +
                    "说不出「这一格是哪种地貌」;缺了它,`TerrainCell.KSpeedIdx` 就没有来源");
                return null;
            }

            if (n.Items.Count != total)
            {
                errors.Add(
                    $"{FileName}:cell_terrain_id 长度 = {n.Items.Count},须等于 world_size 的格总数 {total} —— " +
                    "长度不齐 = 地表与几何不是同一张图纸");
                return null;
            }

            var idToIndex = new Dictionary<int, int>(rows.Count);
            for (int i = 0; i < rows.Count; i++) idToIndex[rows[i].TerrainId] = i;

            var indices = new int[n.Items.Count];
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.Integer ||
                    item.Int < int.MinValue || item.Int > int.MaxValue)
                {
                    errors.Add(
                        $"{FileName}:cell_terrain_id[{i}] 须为 i32 整数(实得 {item.Kind}={item.RawText})");
                    return null;
                }
                if (!idToIndex.TryGetValue((int)item.Int, out int index))
                {
                    errors.Add(
                        $"{FileName}:cell_terrain_id[{i}] = {item.Int} 不在 terrain_table " +
                        "{" + JoinedIds(rows) + "} 内 —— 运行期查表会取出越界行");
                    return null;
                }
                indices[i] = index;
            }

            return Array.AsReadOnly(indices);
        }

        /// <summary>`slope_limit_deg_x100` / `step_offset_mm`(域门 ⑥)。</summary>
        private static void TryReadControllerParams(
            JsonNode root, out int slopeLimit, out int stepOffset, List<string> errors)
        {
            slopeLimit = 0; stepOffset = 0;

            if (TryReadInt(root, "slope_limit_deg_x100", out long slope, errors))
            {
                if (slope < int.MinValue || slope > int.MaxValue)
                    errors.Add($"{FileName}:slope_limit_deg_x100 = {slope} 超出 int 域");
                else if (!LatticeParamQuantization.IsValidSlopeLimit((int)slope))
                    errors.Add(
                        $"{FileName}:slope_limit_deg_x100 = {slope} 超出 " +
                        $"(0, {LatticeParamQuantization.SlopeLimitMaxUnits}] —— " +
                        $"量子 = 0.01 度(1 度 = {LatticeParamQuantization.SlopeLimitUnitsPerDegree} 单位)");
                else
                    slopeLimit = (int)slope;
            }

            if (TryReadInt(root, "step_offset_mm", out long step, errors))
            {
                if (step < int.MinValue || step > int.MaxValue)
                    errors.Add($"{FileName}:step_offset_mm = {step} 超出 int 域");
                else if (!LatticeParamQuantization.IsValidStepOffset((int)step))
                    errors.Add(
                        $"{FileName}:step_offset_mm = {step} 超出 " +
                        $"[0, {LatticeParamQuantization.StepOffsetMaxUnits}] —— " +
                        $"量子 = 1 mm(1 m = {LatticeParamQuantization.StepOffsetUnitsPerMeter} 单位)");
                else
                    stepOffset = (int)step;
            }
        }

        /// <summary>读一个必填整数(缺席 / 非整数 ⇒ 记错并返回 false)。</summary>
        private static bool TryReadInt(JsonNode root, string key, out long value, List<string> errors)
        {
            value = 0;
            if (!root.TryGet(key, out JsonNode n))
            {
                errors.Add($"{FileName}:缺 {key} —— ADR-015 §一之补:" +
                           "`slopeLimit` / `stepOffset` 的真源是关卡工具,系统 1 无权填值");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{FileName}:{key} 须为 JSON 整数(实得 {n.Kind}={n.RawText}) —— " +
                           "它是**量化后的**整数,不是引擎侧的 float");
                return false;
            }
            value = n.Int;
            return true;
        }

        /// <summary>表内 id 的升序文本(报文用)。</summary>
        private static string JoinedIds(List<TerrainRow> rows)
        {
            var ids = new List<string>(rows.Count);
            for (int i = 0; i < rows.Count; i++) ids.Add(rows[i].TerrainId.ToString());
            return string.Join(", ", ids);
        }
    }
}
