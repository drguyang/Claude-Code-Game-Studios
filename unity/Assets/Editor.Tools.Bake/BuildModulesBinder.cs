// `build_modules.json`(23 模块化建造的**模块目录**)的阶段 2 绑定器。
//
// 权威来源:
//   GDD design/gdd/modular-building.md `:88`(规则一:模块目录 = `module_id` → 占用格集 / 槽位类型约束 /
//     朝向集合 / 变体集合,归 **21a** 拥有)· `:90`(评审 B5:目录校验**必须**声明 `(0,0) ∈ OccupiedCells_local`)
//     · `:355`(`TypeOK(SlotType(anchor), m.module_type)` —— `module_type` 是 **SlotType 枚举**,非字符串词汇)
//     · `:507`(`cost(m)` 旋钮:int ≥ 1;可返还模块须 `cost ≥ ⌈1/R⌉`)
//     · `:411`(`Refund(m) := ⌊cost(m) × DEMOLISH_REFUND_RATIO⌋`)
//   GDD design/gdd/item-database.md `:859`(「与 `cost(m)` 同源」—— 本表是该同源的另一半)
//   ADR-014 §三/§四(阶段 2:白名单 · 唯一入口 · 聚合 throw)
//   ADR-015 §三(`WorldPos = (i32,i32,i32)` 单一整数格 —— 占用格偏移的坐标系)
//   `TD-itemdb-01`(本件是其兑现件)
//
// ⚠️ **2026-10-21 用户裁定(甲案)—— 六字段而非 story-001:45 的五字段**:
//   卡面写 `module_id / module_type / OccupiedCells_local / cost / refundable`,本件另加 **`furn_type`**。
//   **理由是一条实测的三方不一致**:`module_id` 在载荷与 registry 里是 **i32**
//   (`entities.yaml:2106` / `WorldPayloads.cs:114` / `StructureKinds.cs:44`),而 24 的 `FURN_TYPES`
//   与 `IPlacedStructureReadView.GetModuleId():string` 是**字符串词汇** —— 两套表示不可互推。
//   `furn_type` 是**跨层桥**:`null` = 非家具件(外壳 / 装饰),`"bed"` 等 = 24 闭集里的成员。
//   ⚠️ **不要**把 `module_type`(SlotType 枚举)与 `furn_type`(24 词汇)混为一谈 —— 同名不同物。
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `ItemDatabaseFurnitureBake` / `DelayTable53Bake`
//   同款头注):EditMode 测试可直驱,菜单层才负责落盘与组注册。
//
// ⚠️ **四条"不能靠作者自觉"的域**,各由一条硬门守:
//   ① `(0,0,0) ∈ occupancy_local` —— 锚点格自身计入占用(评审 B5;否则出现「锚点可走、结构在其上」)
//   ② `module_id` 唯一且 > 0 —— 重复会让「取哪一行」成为隐式规则
//   ③ `refundable = true ⇒ cost ≥ ⌈1/R⌉` —— P0 `R = 1` 时恒满足,但门必须在(否则 P1a 改 R 时静默失效)
//   ④ `module_type` ∈ `SlotType` 枚举名 —— 拼错会被当成 `None`,而 `None` 让所有槽位恒不匹配

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>build_modules.json</c>(模块目录)的阶段 2 绑定器。</summary>
    public static class BuildModulesBinder
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "build_modules.json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys = { "schema_version", "_note", "modules" };

        /// <summary>行对象白名单。</summary>
        /// <remarks>🔁 <b>2026-10-26(TD-inventory-01 前置):六字段 → 七字段,补 <c>build_item_key</c></b>。
        /// 它补的是「该模块消耗**哪个** <c>build_part</c>」—— 此前 23 只说「需要 module_id 对应的
        /// build_part 条目」而**无任何数据把 module_id 映到物品**(全库零引用),20 的建造扣料
        /// (fold 的 <c>StructurePlaced</c> 支)因此无法定位扣哪一栈。</remarks>
        internal static readonly string[] RowKeys =
            { "module_id", "module_type", "furn_type", "build_item_key", "occupancy_local", "cost", "refundable" };

        /// <summary>槽位类型名 → 枚举值(<c>BuildSlotCatalog.SlotType</c> 的**镜像**,不是第二真源)。
        /// <para>⚠️ 本表**刻意不含 `None`** —— `None` 是「无效 / 未分配」哨兵,不是合法的模块类型。
        /// 让作者写 `None` = 让该模块对所有槽位恒不匹配(静默失效),故直接拒。</para>
        /// <para>⚠️ 这是**镜像**而非引用:`Editor.Tools.Bake` 引用 `Sim` 是允许的(编辑期程序集),
        /// 但此处刻意用字符串名而不是 `SlotType` 值 —— 作者态数据里出现 CLR 枚举序数会在
        /// 枚举重排时静默改语义(承 ADR-014 §四「`Fix` 写字符串」同款理由)。</para></summary>
        internal static readonly string[] SlotTypeNames =
            { "Bed", "Table", "Cabinet", "Plot", "Decor", "Shell" };

        /// <summary>`DEMOLISH_REFUND_RATIO` 的 Q16.16 raw(P0 恒 = 1,规则九;P1a 才 < 1)。</summary>
        /// <remarks>⚠️ 本件**不读** 23 的旋钮真源(该旋钮今日无作者态载体)—— 它是**具名常数**,
        /// 与 `RefundCalculator` 的 P0 默认同值。P1a 改 `R` 时须同改两处并由对账门钉住
        /// (登记于本件 `<remarks>`,不在此处开第二真源)。</remarks>
        public const long DemolishRefundRatioRaw = 65536L;

        /// <summary>一行模块。</summary>
        public readonly struct ModuleRow
        {
            /// <summary>模块 id(**i32**;与 `StructurePlacedPayload.ModuleId` 同型)。</summary>
            public readonly int ModuleId;

            /// <summary>槽位类型(23 的 `SlotType` 枚举值;来自作者态字符串名)。</summary>
            public readonly int ModuleType;

            /// <summary>槽位类型名(保留原文 —— 错误报文与跨侧对账用)。</summary>
            public readonly string ModuleTypeName;

            /// <summary>24 的 `FURN_TYPES` 词汇;**`null` = 非家具件**。</summary>
            public readonly string FurnType;

            /// <summary>占用格(相对锚点的整数偏移;`(0,0,0)` 必在其中)。</summary>
            public readonly IReadOnlyList<(int X, int Y, int Z)> OccupancyLocal;

            /// <summary>建造消耗的物品(21a 物品表的 **ordinal**;`category = build_part`)。
            /// <para>🔁 <b>2026-10-26 新增</b>(TD-inventory-01 前置)。⚠️ 数量**不**在此处 ——
            /// 数量 = <see cref="Cost"/>(<c>modular-building.md:429</c> 已定 `cost(m)` = 建造材料数)。
            /// 存两份会让「改一处忘改另一处」变成静默双扣/少扣。</para></summary>
            public readonly int BuildItemKey;

            /// <summary>建造材料数(int ≥ 1;`F-23-3` 的 `cost(m)`)。</summary>
            public readonly int Cost;

            /// <summary>是否可返还(`F-23-3` 的 `refundable`)。</summary>
            public readonly bool Refundable;

            /// <summary>构造一行(测试 / 物化用)。</summary>
            public ModuleRow(
                int moduleId, int moduleType, string moduleTypeName, string furnType,
                IReadOnlyList<(int, int, int)> occupancyLocal, int cost, bool refundable,
                int buildItemKey)
            {
                ModuleId = moduleId;
                ModuleType = moduleType;
                ModuleTypeName = moduleTypeName;
                FurnType = furnType;
                OccupancyLocal = occupancyLocal;
                Cost = cost;
                Refundable = refundable;
                BuildItemKey = buildItemKey;
            }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>行集(声明序;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<ModuleRow> Rows;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<ModuleRow> rows, uint schemaVersion)
            {
                Rows = rows; SchemaVersion = schemaVersion;
            }
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

            // ⚠️ **per-product 期望值**(2026-10-26):本表 schema 2 起行尾带 `build_item_key`,
            // 不再等同全局默认 1。用默认重载会让源表 schema=2 与「期望 1」对撞 ⇒ 烘焙恒拒。
            // 期望值的**单一出处**是读方 codec 的常量(与 `ItemDatabaseBinder` 用法一致)。
            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(
                root, FileName,
                DaYiJingCheng.Gameplay.Presentation.BuildModulesCookedCodec.ExpectedSchemaVersion, errors);

            // ⚠️ 早 throw 豁免点(同 24 三姊妹 binder):无 modules 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("modules", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 modules 数组");
                throw new BakeValidationException(errors);
            }

            var rows = new List<ModuleRow>();
            var seenIds = new HashSet<int>();
            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                ModuleRow? row = ParseRow(rowsNode.Items[i], i, seenIds, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add(
                    $"{FileName}:modules 为空表 —— 空模块目录让放置判定 ⑤(`module_id ∈ 目录`)恒失败" +
                    "(任何放置都被拒 = 建造系统整条静默失效),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        /// <summary>逐行解析(白名单 + 四条域门)。</summary>
        private static ModuleRow? ParseRow(
            JsonNode row, int i, HashSet<int> seenIds, List<string> errors)
        {
            string where = $"modules[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(row, RowKeys, where, FileName, errors);

            bool ok = true;

            int moduleId = 0;
            if (!TryReadModuleId(row, where, out moduleId, errors)) ok = false;

            string moduleTypeName = null;
            int moduleType = 0;
            if (!TryReadModuleType(row, where, out moduleType, out moduleTypeName, errors)) ok = false;

            // `furn_type`:可为 null(非家具件),但**不可缺席** —— 「没写」与「明写非家具」不同,
            // 前者是作者遗漏,后者是设计声明(承 ReadRequiredString 的同款理由)。
            string furnType = null;
            if (!TryReadFurnType(row, where, out furnType, errors)) ok = false;

            var occupancy = new List<(int, int, int)>();
            if (!TryReadOccupancy(row, where, occupancy, errors)) ok = false;

            int buildItemKey = 0;
            if (!TryReadBuildItemKey(row, where, out buildItemKey, errors)) ok = false;

            int cost = 0;
            if (!TryReadCost(row, where, out cost, errors)) ok = false;

            bool refundable = false;
            if (!TryReadRefundable(row, where, out refundable, errors)) ok = false;

            // ③ `refundable ⇒ cost ≥ ⌈1/R⌉`(EC-23-08;P0 R=1 时自动满足,但门必须在)
            if (ok && refundable)
            {
                long minCost = (65536L + DemolishRefundRatioRaw - 1) / DemolishRefundRatioRaw;
                if (cost < minCost)
                {
                    errors.Add(
                        $"{FileName}:{where}(module_id={moduleId}) refundable=true 但 cost={cost} < " +
                        $"⌈1/R⌉={minCost} —— 可返还模块的返还额会被截成 0(EC-23-08);" +
                        "要么抬高 cost,要么置 refundable=false(消耗性件须显式声明,不是 bug)");
                    ok = false;
                }
            }

            if (!ok) return null;

            if (!seenIds.Add(moduleId))
            {
                errors.Add(
                    $"{FileName}:{where} module_id={moduleId} 重复 —— 模块目录每 id 恰一行" +
                    "(重复会让「取哪一行」成为隐式规则 = 静默失序)");
                return null;
            }

            return new ModuleRow(
                moduleId, moduleType, moduleTypeName, furnType, occupancy, cost, refundable, buildItemKey);
        }

        /// <summary>`module_id` —— 正整数(载荷同型 i32)。</summary>
        private static bool TryReadModuleId(JsonNode row, string where, out int moduleId, List<string> errors)
        {
            moduleId = 0;
            if (!row.TryGet("module_id", out JsonNode n))
            {
                errors.Add($"{FileName}:{where} 缺 module_id");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{FileName}:{where}.module_id 须为 JSON 整数(实得 {n.Kind}={n.RawText}) —— " +
                    "它与 StructurePlacedPayload.ModuleId(i32)同型,写成字符串会让两层无法对接");
                return false;
            }
            if (n.Int <= 0 || n.Int > int.MaxValue)
            {
                errors.Add($"{FileName}:{where}.module_id={n.Int} 须在 (0, {int.MaxValue}] —— 非正 id 与「未分配」不可分");
                return false;
            }
            moduleId = (int)n.Int;
            return true;
        }

        /// <summary>`module_type` —— `SlotType` 枚举**名**(字符串),拒 `None`。</summary>
        private static bool TryReadModuleType(
            JsonNode row, string where, out int moduleType, out string name, List<string> errors)
        {
            moduleType = 0;
            name = null;
            if (!ClinicMachineSchema.ReadRequiredString(
                    row, "module_type", where, FileName, errors, out string raw))
                return false;

            name = raw;
            for (int i = 0; i < SlotTypeNames.Length; i++)
            {
                if (string.Equals(SlotTypeNames[i], raw, StringComparison.Ordinal))
                {
                    // 枚举值 = 名字表下标 + 1(SlotType 的 None = 0,故 Bed = 1)
                    moduleType = i + 1;
                    return true;
                }
            }

            errors.Add(
                $"{FileName}:{where}.module_type=\"{raw}\" 不在 SlotType 枚举名闭集 " +
                "{" + string.Join(", ", SlotTypeNames) + "} 内 —— " +
                "拼错会被读成 None,而 None 让所有槽位恒不匹配(静默失效)");
            return false;
        }

        /// <summary>`furn_type` —— 字符串或 **null**;缺席 = 作者遗漏(拒)。</summary>
        private static bool TryReadFurnType(JsonNode row, string where, out string furnType, List<string> errors)
        {
            furnType = null;
            if (!row.TryGet("furn_type", out JsonNode n))
            {
                errors.Add(
                    $"{FileName}:{where} 缺 furn_type —— 「没写」与「明写 null」不同:" +
                    "前者是作者遗漏,后者是「本模块不是家具件」的设计声明");
                return false;
            }
            if (n.Kind == JsonNodeKind.Null) return true;
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add(
                    $"{FileName}:{where}.furn_type 须为 JSON 字符串或 null(实得 {n.Kind}={n.RawText}) —— " +
                    "它是 24 的 FURN_TYPES 词汇,与 i32 的 module_id 是两套表示");
                return false;
            }
            if (string.IsNullOrEmpty(n.Str))
            {
                errors.Add($"{FileName}:{where}.furn_type 为空串 —— 非家具件请明写 null");
                return false;
            }
            furnType = n.Str;
            return true;
        }

        /// <summary>`build_item_key` —— **JSON 整数**(21a 物品表的 ordinal;非负)。
        /// <para>⚠️ 与 `module_id` 一样是 i32 载荷形,写成字符串会让两层无法对接。</para>
        /// <para>⚠️ 本字段**只做域校验**;「该 ordinal 在物品表里存在且 <c>category = build_part</c>」
        /// 是跨表门,归 <c>DataBakeMenu.BakeBuildModulesCore</c>(与 `furn_type ⊆ FURN_TYPES` 同位置、
        /// 同理由:跨表判据只有在能同时看见两张表的调用点才成立,放进 Binder 会变成
        /// 「表在哪都不知道还想判外键」)。</para>
        /// </summary>
        private static bool TryReadBuildItemKey(
            JsonNode row, string where, out int buildItemKey, List<string> errors)
        {
            buildItemKey = 0;
            if (!row.TryGet("build_item_key", out JsonNode n))
            {
                errors.Add(
                    $"{FileName}:{where} 缺 build_item_key —— 没有它就不知道该模块消耗哪个 " +
                    "build_part 物品(20 的建造扣料无法定位扣哪一栈)");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{FileName}:{where}.build_item_key 须为 JSON 整数(实得 {n.Kind}={n.RawText}) —— " +
                    "它是物品表的 ordinal(i32 载荷形),写成字符串会让两层无法对接");
                return false;
            }
            if (n.Int < 0 || n.Int > int.MaxValue)
            {
                errors.Add(
                    $"{FileName}:{where}.build_item_key={n.Int} 须在 [0, {int.MaxValue}] —— " +
                    "负号在物品表里是「未赋值 ordinal」的哨兵,不是合法键");
                return false;
            }
            buildItemKey = (int)n.Int;
            return true;
        }

        /// <summary>`occupancy_local` —— `[[x,y,z], …]`;须含 `(0,0,0)`(评审 B5)。</summary>
        private static bool TryReadOccupancy(
            JsonNode row, string where, List<(int, int, int)> occupancy, List<string> errors)
        {
            if (!row.TryGet("occupancy_local", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:{where} 缺 occupancy_local 数组");
                return false;
            }

            if (n.Items.Count == 0)
            {
                errors.Add($"{FileName}:{where}.occupancy_local 为空 —— 不占格的模块无法参与占用互斥");
                return false;
            }

            bool ok = true;
            var seen = new HashSet<(int, int, int)>();
            for (int k = 0; k < n.Items.Count; k++)
            {
                JsonNode cell = n.Items[k];
                if (cell.Kind != JsonNodeKind.Array || cell.Items.Count != 3)
                {
                    errors.Add(
                        $"{FileName}:{where}.occupancy_local[{k}] 须为 [x, y, z] 三元数组" +
                        $"(实得 {cell.Kind} / {cell.Items?.Count ?? 0} 项)");
                    ok = false;
                    continue;
                }

                var xyz = new int[3];
                bool cellOk = true;
                for (int c = 0; c < 3; c++)
                {
                    JsonNode comp = cell.Items[c];
                    if (comp.Kind != JsonNodeKind.Integer ||
                        comp.Int < int.MinValue || comp.Int > int.MaxValue)
                    {
                        errors.Add(
                            $"{FileName}:{where}.occupancy_local[{k}][{c}] 须为 i32 整数" +
                            $"(实得 {comp.Kind}={comp.RawText}) —— WorldPos = (i32,i32,i32)(ADR-015 §三)");
                        cellOk = false;
                        break;
                    }
                    xyz[c] = (int)comp.Int;
                }
                if (!cellOk) { ok = false; continue; }

                var key = (xyz[0], xyz[1], xyz[2]);
                if (!seen.Add(key))
                {
                    errors.Add(
                        $"{FileName}:{where}.occupancy_local[{k}] = ({key.Item1},{key.Item2},{key.Item3}) 重复" +
                        " —— 重复格会让占用计数虚高");
                    ok = false;
                    continue;
                }
                occupancy.Add(key);
            }

            if (ok && !seen.Contains((0, 0, 0)))
            {
                errors.Add(
                    $"{FileName}:{where}.occupancy_local 不含 (0,0,0) —— " +
                    "锚点格自身必须计入占用(评审 B5;否则出现「锚点可走、结构在其上」)");
                ok = false;
            }

            return ok;
        }

        /// <summary>`cost` —— int ≥ 1(`F-23-3` 的 `cost(m)`;`modular-building.md:507`)。</summary>
        private static bool TryReadCost(JsonNode row, string where, out int cost, List<string> errors)
        {
            cost = 0;
            if (!row.TryGet("cost", out JsonNode n))
            {
                errors.Add($"{FileName}:{where} 缺 cost —— `F-23-3` 的 `cost(m)` 是拆除返还的唯一输入");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{FileName}:{where}.cost 须为 JSON 整数(实得 {n.Kind}={n.RawText}) —— " +
                    "cost 是 int 计数,不在 Fix 解析集(ADR-006 D-21-17)");
                return false;
            }
            if (n.Int < 1 || n.Int > int.MaxValue)
            {
                errors.Add($"{FileName}:{where}.cost={n.Int} ∉ [1, {int.MaxValue}] —— 域 ≥ 1(modular-building.md:507)");
                return false;
            }
            cost = (int)n.Int;
            return true;
        }

        /// <summary>`refundable` —— JSON 布尔(必填;缺字段 = 作者遗漏)。</summary>
        private static bool TryReadRefundable(
            JsonNode row, string where, out bool refundable, List<string> errors)
        {
            refundable = false;
            if (!row.TryGet("refundable", out JsonNode n))
            {
                errors.Add(
                    $"{FileName}:{where} 缺 refundable —— 缺字段 = 构建失败" +
                    "(默认 true 会让消耗性件被误判可返还,EC-23-08)");
                return false;
            }
            if (n.Kind != JsonNodeKind.Boolean)
            {
                errors.Add(
                    $"{FileName}:{where}.refundable 须为 JSON 布尔(实得 {n.Kind}={n.RawText}) —— " +
                    "用 1/0 会让「忘了写」与「写了 false」不可分");
                return false;
            }
            refundable = n.Bool;
            return true;
        }
    }
}
