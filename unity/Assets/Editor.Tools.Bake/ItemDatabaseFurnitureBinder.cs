// 权威来源:
//   GDD design/gdd/clinic-machine.md `:297`(家具目录 FURN_TABLE 归 **21a**)
//     · `:161-162`(S 成员判据 = `type(c) ∈ FURN_TYPES`;`tier(c) ∈ [0, TIER_MAX]`,0 = 无档)
//     · `:188`(`e_env(type)` ∈ [−C_max, C_max],可负 = 污染 / 劣化)
//     · `:330`(`e_env` 量级:正 ∈ [1/16,1/8] / 负 ∈ [−1/4,−1/8] 为**案卷建议**,定值归内容轮)
//   OQ-24-2(2026-09-25)`TIER_MAX = 3` —— **声明家 = 本表**(`tier_max` 根字段)
//   OQ-24-5 / OQ-24-6(2026-10-07)`n_max = 64` ⇒ `C_max = 1/n_max = 1/64`
//   ADR-014 §三/§四(阶段 2:白名单 · `FixParse` 唯一入口 · `Fix` 字段写字符串 · 聚合 throw)
//   `TD-clinic-01` / 路线图 §三 G-8(`O-24-1` 的兑现件)
//
// ⚠️ **本件是 FURN_TYPES 闭集的唯一生产来源**。24 侧(`ClinicMachineRoomBinder`)只**消费**它:
//   烘焙期经 `IFurnTypeSource` 注入取闭集,运行期经 `ClinicFurnitureTraitsSource` 取逐件属性。
//   24 侧**不得**再写一份类型清单(两处真源 = 二者漂移时无判据)。
//
// ⚠️ **三条"不能靠作者自觉"的域**,各由一条硬门守:
//   ① `tier ∈ [0, tier_max]` —— 越界 = 构建失败(不是钳制;钳制会把作者错误静默吞掉)
//   ② `|e_env| ≤ C_max = 1/64` —— 越界拒收。⚠️ `C_max` 与 24 的 `n_max` **耦合**(`n·C_max ≤ 1`),
//      改一侧必须同改另一侧;本处是具名常数,不是从 24 的数据里读回来的(那会成反向依赖)
//   ③ `tier_max` 与 24 的 `ClinicMachineSchema.TierMax` **对账** —— 在 `ClinicMachineTablesBake`
//      注入时执行(本件只声明,不对账:21a 不认识 24)
//
// ⚠️ 空表拒收:空 `FURN_TYPES` 会让**全部**房间谓词恒不匹配(整馆恒 `ROOM_NONE`),
//   与 24 三表 binder 的空表纪律同源。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>item_database_furniture.json</c>(家具目录 = <c>FURN_TABLE</c>)的阶段 2 绑定器。</summary>
    public static class ItemDatabaseFurnitureBinder
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 菜单 / 测试共用)。</summary>
        public const string FileName = "item_database_furniture.json";

        /// <summary>`e_env` 的绝对值上界 `C_max = 1/64`(OQ-24-5 / OQ-24-6:与 `n_max = 64` 耦合,
        /// `n·C_max ≤ 1` ⇒ 家具堆叠的总环境贡献永不超过 1)。</summary>
        public static readonly Fix EnvContributionMax = new Fix(Fix.OneRaw / 64);

        /// <summary>一行家具。</summary>
        public readonly struct FurnRow
        {
            /// <summary>家具类型 id(=`FURN_TYPES` 的成员;与 ROOM_TABLE 判据语言同一词空间)。</summary>
            public readonly string Type;

            /// <summary>档位 ∈ `[0, tier_max]`(0 = 无档,F-24-2 不进 `equip_score`)。</summary>
            public readonly int Tier;

            /// <summary>单件环境贡献 ∈ `[−C_max, C_max]`,**可负**(污染 / 劣化)。</summary>
            public readonly Fix EEnv;

            /// <summary>构造一行(测试 / 物化用)。</summary>
            public FurnRow(string type, int tier, Fix eEnv)
            {
                Type = type; Tier = tier; EEnv = eEnv;
            }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>行集(声明序;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<FurnRow> Rows;

            /// <summary>`TIER_MAX` 的声明值(OQ-24-2 = 3;24 侧以 `ClinicMachineSchema.TierMax` 对账)。</summary>
            public readonly long TierMax;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(IReadOnlyList<FurnRow> rows, long tierMax, uint schemaVersion)
            {
                Rows = rows; TierMax = tierMax; SchemaVersion = schemaVersion;
            }

            /// <summary>类型闭集(`FURN_TYPES`)—— 供 24 侧 <c>IFurnTypeSource</c> 注入。</summary>
            public IReadOnlyCollection<string> Types()
            {
                var set = new List<string>(Rows.Count);
                for (int i = 0; i < Rows.Count; i++) set.Add(Rows[i].Type);
                return set;
            }
        }

        /// <summary>绑定 + 三条域门(任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>)。</summary>
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

            // ⚠️ 键白名单**引用** 24 侧同一份(ADR-024 §⑥ 禁另抄):`tier_max` 已并入该清单。
            ClinicMachineSchema.RejectUnknownKeys(
                root, ClinicMachineSchema.FurnRootKeys, "根", FileName, errors);

            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(root, FileName, errors);
            long tierMax = ReadTierMax(root, errors);

            // ⚠️ 早 throw 豁免点(同三姊妹 binder):无 furn 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("furn", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 furn 数组");
                throw new BakeValidationException(errors);
            }

            var rows = new List<FurnRow>();
            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                FurnRow? row = ParseRow(rowsNode.Items[i], i, tierMax, seenTypes, errors);
                if (row.HasValue) rows.Add(row.Value);
            }

            if (rows.Count == 0 && errors.Count == 0)
                errors.Add(
                    $"{FileName}:furn 为空表 —— 空 FURN_TYPES 让全部房间谓词恒不匹配" +
                    "(整馆恒 ROOM_NONE = 整表静默失效),拒");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, tierMax, schemaVersion);
        }

        /// <summary>读根上的 `tier_max`(必填;OQ-24-2 已裁 3,越出该值须走数值轮改裁定)。</summary>
        private static long ReadTierMax(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("tier_max", out JsonNode node) || node.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{FileName}:缺 tier_max 或非整数 —— `TIER_MAX` 的**声明家是本表**" +
                    "(OQ-24-2 = 3);24 侧 `ClinicMachineSchema.TierMax` 与它逐值对账," +
                    "不在这里声明就等于没有真源");
                return ClinicMachineSchema.TierMax;
            }

            long tierMax = node.Int;
            if (tierMax < 1)
            {
                errors.Add($"{FileName}:tier_max = {tierMax} < 1 —— 档位上限至少 1(F-24-2 的档位和需有档可计)");
                return ClinicMachineSchema.TierMax;
            }

            if (tierMax != ClinicMachineSchema.TierMax)
                errors.Add(
                    $"{FileName}:tier_max = {tierMax} ≠ 24 侧 ClinicMachineSchema.TierMax = " +
                    $"{ClinicMachineSchema.TierMax} —— 两侧各自为政会让 equip 侧的档位域分裂" +
                    "(OQ-24-2 已裁 = 3;改值须走数值轮并同改两侧)");

            return tierMax;
        }

        /// <summary>逐行解析(白名单 + 三条域门)。</summary>
        private static FurnRow? ParseRow(
            JsonNode row, int i, long tierMax, HashSet<string> seenTypes, List<string> errors)
        {
            string where = $"furn[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(
                row, ClinicMachineSchema.FurnRowKeys, where, FileName, errors);

            bool ok = ClinicMachineSchema.ReadRequiredString(
                row, "type", where, FileName, errors, out string type);

            bool tierOk = TryReadTier(row, where, type, tierMax, out int tier, errors);
            ok &= tierOk;

            Fix eEnv = Fix.Zero;
            if (ClinicMachineSchema.ReadRequiredFix(
                    row, "e_env", where, FileName, errors, out Fix parsedEEnv))
            {
                eEnv = parsedEEnv;
                if (eEnv.Raw > EnvContributionMax.Raw || eEnv.Raw < -EnvContributionMax.Raw)
                {
                    errors.Add(
                        $"{FileName}:{where}(type=\"{type ?? "?"}\") e_env raw={eEnv.Raw} 越出 " +
                        $"[−C_max, C_max] = ±{EnvContributionMax.Raw}(C_max = 1/n_max = 1/64," +
                        "OQ-24-5 / OQ-24-6 的耦合 n·C_max ≤ 1)—— 越界拒收,不钳制");
                }
            }
            else
            {
                ok = false;
            }

            if (!ok) return null;

            if (!seenTypes.Add(type))
            {
                errors.Add(
                    $"{FileName}:{where} type「{type}」重复 —— 家具目录每类型恰一行" +
                    "(重复会让「取哪一行」成为隐式规则 = 静默失序;也使 FURN_TYPES 闭集大小失真)");
                return null;
            }

            return new FurnRow(type, tier, eEnv);
        }

        /// <summary>`tier` —— int 计数(ADR-006 D-21-17:不在 `Fix` 解析集),域 `[0, tier_max]`。</summary>
        /// <remarks>⚠️ 三种失败各报**具名**错误,不静默返回 false —— 静默会把「作者写错档位」
        /// 退化成「这一件没有档」,而后者是**合法值**。</remarks>
        private static bool TryReadTier(
            JsonNode row, string where, string type, long tierMax, out int tier, List<string> errors)
        {
            tier = 0;
            if (!row.TryGet("tier", out JsonNode node))
            {
                errors.Add($"{FileName}:{where}(type=\"{type ?? "?"}\") 缺 tier —— 缺字段 = 构建失败(O-24-1)");
                return false;
            }

            if (node.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{FileName}:{where}(type=\"{type ?? "?"}\") tier 的 token 种类 = {node.Kind} " +
                    "—— 须为 JSON 整数(tier 是 int 计数,不在 Fix 解析集,ADR-006 D-21-17)");
                return false;
            }

            if (node.Int < 0 || node.Int > tierMax)
            {
                errors.Add(
                    $"{FileName}:{where}(type=\"{type ?? "?"}\") tier={node.Int} ∉ [0, tier_max={tierMax}] " +
                    "—— 越界拒收,不钳制(钳制会把作者的档位错误静默吞成 0 或顶档)");
                return false;
            }

            tier = (int)node.Int;
            return true;
        }
    }
}
