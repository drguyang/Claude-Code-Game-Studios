// `world_buildslots.json`(建造槽位骨架 · ADR-022 §三导出契约)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(整数格 + 槽位约束 + **初始占用标记 `BakedInitial`**)· §二(唯一作者 = 关卡工具)
//   ADR-015 §三(`WorldPos = (i32,i32,i32)` 单一整数格)
//   ADR-014 §三(根对象 / 行对象白名单;未知键 = 构建失败)· §四(聚合 throw)
//   ADR-024 §⑥(错误报文须点名判据,便于夹具断言「哪条门拒的」)
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `BuildModulesBinder` 同款头注):
//    EditMode 测试可直驱,菜单层才负责落盘。
//
// ⚠️ **五条域门**(任一违例 ⇒ 聚合 `BakeValidationException`):
//   ① 空表 —— 空槽位目录让 `PlaceableChecker.Check` 恒 `SlotNotFound`(建造系统静默失效)
//   ② 重复格 `(x,y,z)` —— 让「取哪一条」成为隐式规则
//   ③ 类型名不在闭集内(含 `None` —— 它是「未分配」哨兵,让所有模块恒不匹配)
//   ④ 格超出 `world_size` 声明范围 —— 越界格运行期永远不可达
//   ⑤ `baked_initial` 缺席 / 非布尔 —— 「没写」与「明写 false」不同(前者是作者遗漏)
//
// ⚠️ 报文前缀固定 `world_buildslots:`(各门再点自己的判据词)—— 测试按**字面量**断言关键词。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_buildslots.json</c>(建造槽位骨架)的阶段 2 绑定器。</summary>
    public static class WorldBuildSlotsBinder
    {
        /// <summary>源文件名(单一出处:导出器 / 烘焙 / 菜单 / 测试共用)。</summary>
        public const string FileName = "world_buildslots.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys =
            { "schema_version", "level_id", "world_size", "slots" };

        /// <summary>行对象白名单。</summary>
        /// <remarks>🔁 <b>2026-10-28(schema 1→2)</b>:新增 `module_id` —— 开档既有家具的模块 id。
        /// ⚠️ 必须与 `LevelExporter` 的写出字段**逐字对应**:白名单比写方窄 ⇒ 真种子烘焙当场
        /// 报「未知键」;比写方宽 ⇒ 旧产物能混过。</remarks>
        internal static readonly string[] RowKeys =
            { "cell", "slot_type", "baked_initial", "module_id" };

        /// <summary>槽位类型名 → 枚举序数(Sim `SlotType` 的镜像;**不含 `None`**)。</summary>
        /// <remarks>⚠️ 作者态写**字符串名**而非枚举序数:枚举重排会静默改语义(承 ADR-014 §四
        /// 「`Fix` 写字符串」同款理由)。序数 = 名字表下标 + 1(`SlotType.None = 0`)。
        /// <para>⚠️ **2026-10-28 代码评审 A-21 修复**:上面的「序数 = 下标 + 1」已作废 ——
        /// 手写表与 `Sim` 枚举之间**没有任何门**,插值即静默错配。现名与序数全部来自
        /// <see cref="SlotTypeVocabulary"/>(派生自 `Sim.World.SlotType`)。原文不删,留作闭环记录。</para></remarks>
        internal static string[] SlotTypeNames => SlotTypeVocabulary.Names;

        /// <summary>一行槽位。</summary>
        public readonly struct SlotRow
        {
            /// <summary>格 x(i32)。</summary>
            public readonly int X;
            /// <summary>格 y(i32)。</summary>
            public readonly int Y;
            /// <summary>格 z(i32)。</summary>
            public readonly int Z;

            /// <summary>槽位类型枚举序数(`SlotType`;1…6)。</summary>
            public readonly int TypeOrdinal;

            /// <summary>槽位类型名(原文;错误报文与跨侧对账用)。</summary>
            public readonly string TypeName;

            /// <summary>初始占用标记(`BakedInitial`;ADR-022 §三)。</summary>
            public readonly bool BakedInitial;

            /// <summary>开档既有家具的模块 id(`BakedInitial == false` 时为 -1)。</summary>
            public readonly int ModuleId;

            /// <summary>构造一行(测试 / 物化用)。</summary>
            public SlotRow(int x, int y, int z, int typeOrdinal, string typeName, bool bakedInitial,
                           int moduleId = -1)
            {
                X = x; Y = y; Z = z;
                TypeOrdinal = typeOrdinal; TypeName = typeName; BakedInitial = bakedInitial;
                ModuleId = moduleId;
            }

            /// <summary>允许类型位掩码(`1 &lt;&lt; TypeOrdinal`;运行时 `BuildSlot` 的形)。</summary>
            public int AllowedMask => 1 << TypeOrdinal;
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>行集(声明序;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<SlotRow> Rows;

            /// <summary>声明范围 X(格数)。</summary>
            public readonly int Width;
            /// <summary>声明范围 Y(格数)。</summary>
            public readonly int Height;
            /// <summary>声明范围 Z(格数)。</summary>
            public readonly int Depth;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<SlotRow> rows, int width, int height, int depth, uint schemaVersion)
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

            // 期望值**单一出处** = 读方 codec 的常量(与 BuildModulesBinder 同款口径)。
            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(
                root, FileName,
                DaYiJingCheng.Gameplay.Presentation.WorldBuildSlotsCookedCodec.ExpectedSchemaVersion,
                errors);

            // ① 声明范围
            int width = 0, height = 0, depth = 0;
            bool sizeOk = TryReadWorldSize(root, out width, out height, out depth, errors);

            // ② 槽位数组(早 throw 豁免点:无数组 ⇒ 行层校验结构上不可达)
            if (!root.TryGet("slots", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 slots 数组 —— 空骨架让 PlaceableChecker 恒 SlotNotFound");
                throw new BakeValidationException(errors);
            }

            var rows = new List<SlotRow>();
            var seenCells = new HashSet<(int, int, int)>();
            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                SlotRow? row = ParseRow(rowsNode.Items[i], i, sizeOk, width, height, depth,
                                        seenCells, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rowsNode.Items.Count == 0 && errors.Count == 0)
                errors.Add(
                    $"{FileName}:slots 为空表 —— 空槽位骨架让放置判定恒 SlotNotFound" +
                    "(任何放置都被拒 = 建造系统整条静默失效),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, width, height, depth, schemaVersion);
        }

        /// <summary>`world_size` —— `[W,H,D]` 三个 ≥ 1 的整数(声明范围;域门 ④ 的判据)。</summary>
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
                errors.Add(
                    $"{FileName}:world_size 须为 [W, H, D] 三元数组(实得 {n.Items.Count} 项)");
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

        /// <summary>逐行解析(白名单 + 域门 ②/③/④/⑤)。</summary>
        private static SlotRow? ParseRow(
            JsonNode row, int i, bool sizeOk, int width, int height, int depth,
            HashSet<(int, int, int)> seenCells, List<string> errors)
        {
            string where = $"slots[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(row, RowKeys, where, FileName, errors);

            bool ok = true;

            // `cell` —— [x,y,z] i32 三元组
            int x = 0, y = 0, z = 0;
            if (!TryReadCell(row, where, out x, out y, out z, errors)) ok = false;

            // `slot_type` —— 枚举名字符串(拒 None)
            int typeOrdinal = 0;
            string typeName = null;
            if (!TryReadSlotType(row, where, out typeOrdinal, out typeName, errors)) ok = false;

            // `baked_initial` —— JSON 布尔,必填
            bool bakedInitial = false;
            if (!TryReadBakedInitial(row, where, out bakedInitial, errors)) ok = false;

            // `module_id` —— schema 2:布尔与 id 的**一致性**(两侧都拒)
            int moduleId = -1;
            if (!TryReadModuleId(row, where, bakedInitial, out moduleId, errors)) ok = false;

            if (!ok) return null;

            // 域门 ④ 格超出声明范围
            if (sizeOk && (x < 0 || x >= width || y < 0 || y >= height || z < 0 || z >= depth))
            {
                errors.Add(
                    $"{FileName}:{where} 格 ({x},{y},{z}) 超出声明范围 " +
                    $"[0,{width})×[0,{height})×[0,{depth}) —— 越界格在运行期永远不可达");
                return null;
            }

            // 域门 ② 重复格
            if (!seenCells.Add((x, y, z)))
            {
                errors.Add(
                    $"{FileName}:{where} 格 ({x},{y},{z}) 重复 —— 槽位骨架每格恰一条" +
                    "(重复会让「取哪一条」成为隐式规则 = 静默失序)");
                return null;
            }

            return new SlotRow(x, y, z, typeOrdinal, typeName, bakedInitial, moduleId);
        }

        /// <summary>`cell` —— `[x, y, z]` 三元 i32 数组。</summary>
        private static bool TryReadCell(JsonNode row, string where, out int x, out int y, out int z,
                                        List<string> errors)
        {
            x = y = z = 0;
            if (!row.TryGet("cell", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:{where} 缺 cell 数组([x, y, z])");
                return false;
            }
            if (n.Items.Count != 3)
            {
                errors.Add(
                    $"{FileName}:{where}.cell 须为 [x, y, z] 三元数组(实得 {n.Items.Count} 项) —— " +
                    "WorldPos = (i32,i32,i32)(ADR-015 §三)");
                return false;
            }

            var comp = new int[3];
            for (int c = 0; c < 3; c++)
            {
                JsonNode item = n.Items[c];
                if (item.Kind != JsonNodeKind.Integer ||
                    item.Int < int.MinValue || item.Int > int.MaxValue)
                {
                    errors.Add(
                        $"{FileName}:{where}.cell[{c}] 须为 i32 整数(实得 {item.Kind}={item.RawText})");
                    return false;
                }
                comp[c] = (int)item.Int;
            }

            x = comp[0]; y = comp[1]; z = comp[2];
            return true;
        }

        /// <summary>`slot_type` —— `SlotType` 枚举**名**(字符串),拒 `None`。</summary>
        private static bool TryReadSlotType(
            JsonNode row, string where, out int ordinal, out string name, List<string> errors)
        {
            ordinal = 0;
            name = null;
            if (!ClinicMachineSchema.ReadRequiredString(
                    row, "slot_type", where, FileName, errors, out string raw))
                return false;

            name = raw;
            // 序数 = **CLR 枚举值**(不是表下标 + 1):运行期 `BitMask.Single(t) => 1 << (int)t`,
            // 只有取枚举值才能与之同源(2026-10-28 评审 A-21)。
            if (SlotTypeVocabulary.TryGetOrdinal(raw, out ordinal))
                return true;

            errors.Add(
                $"{FileName}:{where}.slot_type=\"{raw}\" 不在 SlotType 闭集 " +
                "{" + SlotTypeVocabulary.ClosedSetText() + "} 内 —— " +
                "None 是「未分配」哨兵,拼错同样让该格对所有模块恒不匹配(静默失效)");
            return false;
        }

        /// <summary>`baked_initial` —— JSON 布尔(必填;缺字段 = 作者遗漏)。</summary>
        private static bool TryReadBakedInitial(
            JsonNode row, string where, out bool bakedInitial, List<string> errors)
        {
            bakedInitial = false;
            if (!row.TryGet("baked_initial", out JsonNode n))
            {
                errors.Add(
                    $"{FileName}:{where} 缺 baked_initial —— 「没写」与「明写 false」不同:" +
                    "前者是作者遗漏,后者是「本槽位开档无既有家具」的设计声明(ADR-022 §三)");
                return false;
            }
            if (n.Kind != JsonNodeKind.Boolean)
            {
                errors.Add(
                    $"{FileName}:{where}.baked_initial 须为 JSON 布尔(实得 {n.Kind}={n.RawText}) —— " +
                    "用 1/0 会让「忘了写」与「写了 false」不可分");
                return false;
            }
            bakedInitial = n.Bool;
            return true;
        }

        /// <summary>
        /// `module_id` —— 开档既有家具的模块 id(schema 2 新增)。
        /// <para>规则:**`baked_initial = true` ⇒ 须为 ≥ 1 的整数;`false` ⇒ 须为 JSON `null`
        /// (或整数字段缺席)。两侧都拒 —— 任一方向的自相矛盾都是静默缺陷:
        /// 前者会让运行期「声明了占用却算不出足迹」,后者会让读者以为「有家具但没占」。</para>
        /// </summary>
        /// <remarks>⚠️ 报文字面量 `module_id` / `baked_initial` 是夹具的判据锚点(禁自指)。</remarks>
        private static bool TryReadModuleId(
            JsonNode row, string where, bool bakedInitial, out int moduleId, List<string> errors)
        {
            moduleId = -1;
            bool present = row.TryGet("module_id", out JsonNode n);

            // 缺字段按 null 处理(与显式 null 等价 —— 都是「本格无既有家具」)。
            bool isNull = !present || n.Kind == JsonNodeKind.Null;

            if (bakedInitial)
            {
                if (isNull)
                {
                    errors.Add(
                        $"{FileName}:{where} baked_initial = true 但 module_id 为 " +
                        (present ? "null" : "缺字段") +
                        " —— 开档既有家具必须指明是哪件家具(运行期按它算足迹并发 structure_id," +
                        "GDD 规则三);说不出的占用 = 静默缺陷");
                    return false;
                }
                if (n.Kind != JsonNodeKind.Integer)
                {
                    errors.Add(
                        $"{FileName}:{where}.module_id 须为 JSON 整数(实得 {n.Kind}={n.RawText}) —— " +
                        "模块 id 是 i32(entities.yaml 的载荷形),写字符串会让读者以为它是名字");
                    return false;
                }
                if (n.Int < 1 || n.Int > int.MaxValue)
                {
                    errors.Add(
                        $"{FileName}:{where}.module_id = {n.Int} 须在 [1, {int.MaxValue}] —— " +
                        "≤ 0 是哨兵位(`-1` = 无既有家具),不是合法模块 id");
                    return false;
                }
                moduleId = (int)n.Int;
                return true;
            }

            // baked_initial = false:只接受 null / 缺席;写了具体 id 就是矛盾。
            if (!isNull)
            {
                errors.Add(
                    $"{FileName}:{where} baked_initial = false 却写了 module_id = " +
                    (n.Kind == JsonNodeKind.Integer ? n.Int.ToString() : $"{n.Kind}={n.RawText}") +
                    " —— 二者矛盾(非初始占用的槽位没有既有家具,运行期按空地处理);" +
                    "要声明开档家具请同时把 baked_initial 置 true");
                return false;
            }

            return true;
        }
    }
}
