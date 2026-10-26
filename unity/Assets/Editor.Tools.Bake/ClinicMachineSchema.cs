// Story 001 · clinic-machine —— 24 三表烘焙 schema 的共享面(白名单 / 共用校验 / O-24-5 / O-24-4)。
//
// 权威来源:
//   ADR-014 §三(阶段 2 per-schema 绑定:已知键白名单 · FixParse · schema_version · 聚合 throw)
//         §四(Fix 字段必须是 JSON 字符串,禁数值 token 中转)
//   卡面 production/epics/clinic-machine/story-001-baked-tables-schema-gates.md
//   GDD design/gdd/clinic-machine.md:
//     · 规则三 / F-24-3 —— K_speed 值域 (0,1] 与 K_CONTEXT_MAX 表派生(禁裸常数)
//     · F-24-1 公共定义 —— ADJ_TABLE 对称性 = 烘焙硬门;W 域与 |W_MIN| <= W_MAX
//     · F-24-2 —— n_max x TIER_MAX < 2^31(int32 溢出门)
//     · O-24-4 —— ENV_MOD_MIN < 0(21a 常量,本 story 只断言表值)
//     · O-24-5 —— 四项烘焙断言
//     · 规则二 —— ROOM_TABLE 判据语言(邻接子句 + 方向确定性)
//   OQ-24-1(2026-09-25)K_speed 档集 = {1, 7/8, 3/4}
//   OQ-24-2(2026-09-25)TIER_MAX = 3(EQUIP_MOD_CAP / ENV_MOD_MIN / ENV_MOD_MAX 同批)
//   OQ-24-5(2026-10-07)n_max = 64 · OQ-24-6(2026-09-25 / 2026-10-07)W 域 = [-1/4, +1/4]
//
// ⚠️ TierMax 是 **21a 的旋钮**(OQ-24-2 已裁 = 3),其声明家 = 21a 家具目录扩展(O-24-1)。
//   本件刻意**不把 TIER_MAX 回声进 24 的数据文件**(在 24 的数据里再写一份 = 第二真源 ——
//   协调方 2026-10-20 裁决 1);以具名 C# 常数承接,O-24-1 落地后须与 21a 家具目录对账,
//   不得各自为政(已登记残余)。
//
// ⚠️ 本件**不含任何 `FURN_TYPES` 闭集声明**(哪怕测试样本命名为它)—— 闭集来源 = 21a(O-24-1),
//   本 story **不伪造**(协调方 2026-10-20 裁决 2)。闭集来源经 `IFurnTypeSource` **注入参数**;
//   缺席时由 `ClinicMachineRoomBinder.BindResult.FurnMembershipEnforced == false` **显式自陈**
//   (缺口哨兵:缺席可观测,而非静默;O-24-1 落地后该哨兵转红,不得当回归修掉)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>ROOM_TABLE 谓词的子句类型闭集(GDD 规则二判据语言)。
    /// <para>AC-24-12 的**已实现半边**:闭集外的一律走**同一条**拒收规则(不另设分支)。</para>
    /// <para>⚠️ 另半边(子句引用的 <c>type ∈ FURN_TYPES</c>)**未实现** —— 闭集来源 = 21a
    /// 家具目录扩展(<c>O-24-1</c>),实测零落点 ⇒ 挂 <c>BLOCKED-BY-O-24-1</c> / <c>TD-clinic-01</c>。</para></summary>
    public enum ClinicClauseKind
    {
        /// <summary>「<c>type</c> 的数量 ≥ <c>min</c>」(如「床 ≥ 2」)。</summary>
        MinCount = 0,

        /// <summary>「存在 <c>type</c>」(如「有手术台」)。</summary>
        HasType = 1,

        /// <summary>「<c>type</c> 在 <c>direction</c> 上邻接 <c>neighbor_type</c> 的数量 ≥ <c>min</c>」
        /// (如「手术台邻接无菌柜 ≥ 1」;方向确定性同 <c>adj_sum</c> 的 +x/+y 单向查法)。</summary>
        AdjacencyMinCount = 2,
    }

    /// <summary>邻接子句的方向闭集(GDD 规则二 2026-09-17 复核:方向确定性)。
    /// <para>取值 = <c>adj_sum</c> 的基向量 <c>(1,0)</c> / <c>(0,1)</c>(ADR-015 §五);
    /// 只查 +x / +y 两方向,每条边恰好计数一次。</para></summary>
    public enum ClinicClauseDirection
    {
        /// <summary>不适用(非邻接子句)。</summary>
        None = 0,

        /// <summary>+x 方向。</summary>
        PlusX = 1,

        /// <summary>+y 方向。</summary>
        PlusY = 2,
    }

    /// <summary>家具类型闭集的**来源接缝**(AC-24-08 / AC-24-12 的挂标半边)。
    /// <para>生产路径当前无实现者 —— 闭集由 21a 家具目录扩展提供(<c>O-24-1</c>,实测零落点)。</para>
    /// <para>⚠️ 本接口的存在**不构成**该半边通过:它只是让「闭集从哪来」成为**显式注入**,
    /// 而非在生产路径里自造一份。测试可用**测试内局部替身**证明接缝通电(替身是协作对象,
    /// 不是声明的闭集);生产路径缺席时由缺口哨兵自陈未强制。</para></summary>
    public interface IFurnTypeSource
    {
        /// <summary>尝试取得家具类型闭集。</summary>
        /// <param name="types">成功时为闭集;失败时为 null。</param>
        /// <returns>true = 来源可用(成员资格校验须执行);false = 来源缺席(当前生产态)。</returns>
        bool TryGetTypes(out IReadOnlyCollection<string> types);
    }

    /// <summary>24 三表烘焙 schema 的共享校验面(全部**聚合后一次 throw**,不是 <c>Debug.Assert</c>)。</summary>
    public static class ClinicMachineSchema
    {
        /// <summary><c>TIER_MAX</c> —— **21a 的旋钮**(OQ-24-2 2026-09-25 已裁 = 3)。
        /// <para>⚠️ 其声明家 = 21a 家具目录扩展(<c>O-24-1</c>);本处**不是**第二真源 ——
        /// 24 的数据文件里不写它(协调方 2026-10-20 裁决 1)。<c>O-24-1</c> 落地后须与 21a 对账。</para></summary>
        public const long TierMax = 3L;

        /// <summary><c>2^31</c> —— F-24-2 的 int32 溢出门上界(乘积须**严格小于**此值)。</summary>
        public const long Int32Limit = 2147483648L;

        /// <summary><c>2^31 − 1</c> —— 乘积的允许上确界(恰等于此值通过)。</summary>
        public const long Int32Max = 2147483647L;

        /// <summary><c>CONTEXT_TABLE.K_speed</c> 档集字面量(OQ-24-1 2026-09-25 已裁,恰 3 员)。
        /// <para>⚠️ 档集外(含 <c>0</c> / 负 / <c>5/4</c>)走**同一条** schema 错 —— 单机制,不另设分支。</para></summary>
        public static readonly IReadOnlyList<string> KSpeedLiterals = new[] { "1", "7/8", "3/4" };

        /// <summary>档集对应的 raw(由字面量经 <see cref="FixParse"/> 派生 —— 零手抄常量,单一出处)。</summary>
        private static readonly long[] KSpeedRaws = DeriveRaws(KSpeedLiterals);

        /// <summary><c>CONTEXT_TABLE</c> 根键白名单(ADR-014 §三:未知键 = 烘焙期硬失败)。</summary>
        internal static readonly string[] ContextRootKeys = { "schema_version", "_note", "contexts" };

        /// <summary><c>CONTEXT_TABLE</c> 行键白名单。</summary>
        internal static readonly string[] ContextRowKeys = { "room_type", "context_id", "k_speed", "base_env" };

        /// <summary><c>ADJ_TABLE</c> 根键白名单。</summary>
        internal static readonly string[] AdjRootKeys = { "schema_version", "_note", "n_max", "w_min", "w_max", "pairs" };

        /// <summary><c>ROOM_NAMES</c> 根键白名单(2026-10-26 裁定 ①-C)。</summary>
        internal static readonly string[] RoomNamesRootKeys = { "schema_version", "_note", "rooms" };

        /// <summary><c>ROOM_NAMES</c> 行键白名单。</summary>
        internal static readonly string[] RoomNamesRowKeys = { "room_type", "id" };

        /// <summary><c>CONTEXT_NAMES</c> 根键白名单(2026-10-26 裁定 ②-C)。</summary>
        internal static readonly string[] ContextNamesRootKeys = { "schema_version", "_note", "contexts" };

        /// <summary><c>CONTEXT_NAMES</c> 行键白名单。</summary>
        internal static readonly string[] ContextNamesRowKeys = { "context_id", "id" };

        /// <summary><c>ADJ_TABLE</c> 行键白名单。</summary>
        internal static readonly string[] AdjRowKeys = { "a", "b", "w" };

        /// <summary><c>ROOM_TABLE</c> 根键白名单。</summary>
        internal static readonly string[] RoomRootKeys = { "schema_version", "_note", "rooms" };

        /// <summary><c>ROOM_TABLE</c> 房间行键白名单。</summary>
        internal static readonly string[] RoomRowKeys = { "room_type", "clauses" };

        /// <summary>各子句类型的键白名单(按 <see cref="ClinicClauseKind"/> 序数索引)。</summary>
        internal static readonly string[][] ClauseKeysByKind =
        {
            new[] { "kind", "type", "min" },                                     // MinCount
            new[] { "kind", "type" },                                            // HasType
            new[] { "kind", "type", "neighbor_type", "direction", "min" },       // AdjacencyMinCount
        };

        /// <summary>子句类型字面量(按 <see cref="ClinicClauseKind"/> 序数索引;与枚举**成对**登记,零漂移)。</summary>
        internal static readonly string[] ClauseKindLiterals = { "min_count", "has_type", "adjacency_min_count" };

        /// <summary>FURN_TABLE 的字段形状(AC-24-08 的**已实现半边** —— schema 形状)。
        /// <para>⚠️ 只有**字段与 token 种类**的声明,**不含任何类型闭集值**(闭集 = 21a / O-24-1)。</para></summary>
        internal static readonly string[] FurnRootKeys = { "schema_version", "_note", "tier_max", "furn" };

        /// <summary>FURN_TABLE 行键白名单(O-24-1 的三字段)。</summary>
        internal static readonly string[] FurnRowKeys = { "type", "tier", "e_env" };

        /// <summary>判断 <c>K_speed</c> 的 raw 是否落在档集内。</summary>
        public static bool IsAllowedKSpeed(long raw)
        {
            for (int i = 0; i < KSpeedRaws.Length; i++)
                if (KSpeedRaws[i] == raw) return true;
            return false;
        }

        /// <summary>档集的展示文本(错误消息确定性:字面量序,不用集合迭代序)。</summary>
        public static string KSpeedLiteralsText()
        {
            var parts = new string[KSpeedLiterals.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = KSpeedLiterals[i];
            return string.Join(" / ", parts);
        }

        /// <summary>O-24-5 的两条界断言(F-24-2 int32 溢出门 + F-24-1 int64 界健全性)。
        /// <para>**参数化**是刻意设计:边界值(恰 <c>2^31 − 1</c>)必须可直驱,否则该边界不可证伪。</para>
        /// </summary>
        /// <param name="nMax">单房间格数上界(24 自有,<c>clinicmachine_adj.json</c> 的 <c>n_max</c>)。</param>
        /// <param name="tierMax">档位上限(21a 旋钮,生产路径传 <see cref="TierMax"/>)。</param>
        /// <param name="wMinRaw">W 域下界 raw(<c>w_min</c>)。</param>
        /// <param name="wMaxRaw">W 域上界 raw(<c>w_max</c>)。</param>
        /// <param name="errors">聚合错误列表。</param>
        public static void AssertOverflowBounds(long nMax, long tierMax, long wMinRaw, long wMaxRaw, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            // ① F-24-2:n_max × TIER_MAX < 2^31(equip_score × 65536 的 int32 溢出防护)
            if (nMax <= 0)
            {
                errors.Add($"O-24-5:n_max={nMax} 须为正整数(单房间格数上界)");
            }
            else if (tierMax <= 0)
            {
                errors.Add($"O-24-5:TIER_MAX={tierMax} 须为正整数");
            }
            else
            {
                // 先防 long 乘法回绕(承 ADR-012 的 F7 纪律:不让溢出决定判决)
                long product;
                if (nMax > long.MaxValue / tierMax) product = long.MaxValue;
                else product = nMax * tierMax;

                if (product >= Int32Limit)
                {
                    errors.Add(
                        $"O-24-5 溢出界失败:n_max × TIER_MAX = {nMax} × {tierMax} = {product} " +
                        $">= 2^31({Int32Limit}) —— 违反 F-24-2 的 int32 溢出门" +
                        $"(equip_score × 65536 会在 int32 溢出;允许上确界 = {Int32Max})");
                }
            }

            // ② F-24-1 / OQ-24-6:W_MIN < 0 < W_MAX 且 |W_MIN| <= W_MAX(否则 Φ 定义不健全)
            if (!(wMinRaw < 0))
            {
                errors.Add(
                    $"O-24-5 / OQ-24-6:W_MIN raw={wMinRaw} 须 < 0 —— W 域 = [-1/4, +1/4]," +
                    "下界非负会让「负向邻接」无位可放(F-24-1 的可负语义)");
            }
            if (!(wMaxRaw > 0))
            {
                errors.Add(
                    $"O-24-5 / OQ-24-6:W_MAX raw={wMaxRaw} 须 > 0 —— W 域 = [-1/4, +1/4]");
            }

            long absMin = wMinRaw < 0 ? -wMinRaw : wMinRaw;
            if (absMin > wMaxRaw)
            {
                errors.Add(
                    $"O-24-5 界健全性失败:|W_MIN| = {absMin} > W_MAX = {wMaxRaw} —— " +
                    "F-24-1 的 int64 界 max|env_score| <= |base| + n·C_max + 2n·Φ 依赖 " +
                    "Φ := max(|W_MIN|, W_MAX);|W_MIN| > W_MAX 时原界低估(2026-09-17 复核订正)");
            }
        }

        /// <summary>O-24-4:断言 21a 的 <c>ENV_MOD_MIN &lt; 0</c>(承重前提)。</summary>
        /// <remarks>若 <c>ENV_MOD_MIN &gt;= 0</c>,则「满污染但已匹配房间」的钳制结果 = <c>0</c>
        /// = 恰等于 <c>ROOM_NONE</c> 的中性值 ⇒ 9 / 21a 无法区分「空 / 中性 / 最差」(GDD F-24-1 注)。</remarks>
        public static void AssertEnvModMinNegative(Fix envModMin, List<string> errors)
        {
            if (errors == null) throw new ArgumentNullException(nameof(errors));
            if (!(envModMin.Raw < 0))
            {
                errors.Add(
                    $"O-24-4 失败:ENV_MOD_MIN raw={envModMin.Raw} 须 < 0 —— " +
                    "否则 21a F1 的钳制下界与 ROOM_NONE 的中性值 0 不可区分" +
                    "(9 / 21a 无法判「空 / 中性 / 最差」;GDD F-24-1 承重前提)");
            }
        }

        /// <summary>AC-24-08 的**已实现半边**:FURN_TABLE 的字段形状断言
        /// (字段存在 + token 种类 ∈ {整数域 / Fix 字符串} + <c>tier</c> 域)。
        /// <para>⚠️ **不是** AC-24-08 通过:该 AC 挂 <c>BLOCKED-BY-O-24-1</c>(闭集来源 = 21a 家具目录)。
        /// 本方法只承载「schema 形状」半边,且**不含任何类型闭集值**(协调方 2026-10-20 裁决 2)。</para>
        /// <para>⚠️ GL 与 <c>e_env</c> 的量级上界(<c>C_max = 1/8</c>,2026-10-21 用户裁定)归
        /// FURN_TABLE 内容轮(O-24-1),本处不预置 —— 否则是在 24 侧替 21a 定内容域。</para>
        /// </summary>
        /// <param name="json">FURN_TABLE 形状的 JSON 文本(生产路径当前无此文件;由测试驱动的样本提供)。</param>
        /// <exception cref="BakeValidationException">词法失败 / 字段缺失 / token 种类不符 / tier 越界(聚合)。</exception>
        public static void AssertFurnTableShape(string json)
        {
            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(new List<string> { $"FURN_TABLE 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"FURN_TABLE 根须为对象(实得 {root.Kind})" });

            var errors = new List<string>();
            const string fileName = "FURN_TABLE";

            RejectUnknownKeys(root, FurnRootKeys, "根", fileName, errors);
            ReadSchemaVersion(root, fileName, errors);

            // `tier_max` 可选(缺省 = 本类的具名常数):它是 **21a 声明家** 的字段,
            // 真文件 `item_database_furniture.json` 必带;形状夹具则可省。声明后按声明值校行。
            long declaredTierMax = TierMax;
            if (root.TryGet("tier_max", out JsonNode tierMaxNode) && tierMaxNode.Kind == JsonNodeKind.Integer)
                declaredTierMax = tierMaxNode.Int;

            if (!root.TryGet("furn", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{fileName}:缺 furn 数组");
                throw new BakeValidationException(errors);
            }

            // D1:空表与三姊妹 binder 同纪律(Context:104 / Adj:116 / Room:135)——
            // 空家具目录会让所有房间谓词恒不匹配(整表静默失效),拒。
            if (rowsNode.Items.Count == 0)
            {
                errors.Add($"{fileName}:furn 为空表 —— 空表让所有房间谓词恒不匹配(整表静默失效),拒");
                throw new BakeValidationException(errors);
            }

            // D2:重复 type 与三姊妹 binder 同纪律 —— 每家具类型恰一行。
            var seenTypes = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                JsonNode row = rowsNode.Items[i];
                string where = $"furn[{i}]";
                if (row.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{fileName}:{where} 须为对象(实得 {row.Kind})");
                    continue;
                }

                RejectUnknownKeys(row, FurnRowKeys, where, fileName, errors);

                // type —— 字符串(家具类型 id;闭集值归 21a,本处只校 token 种类)
                if (!row.TryGet("type", out JsonNode typeNode) || typeNode.Kind != JsonNodeKind.String ||
                    string.IsNullOrEmpty(typeNode.Str))
                {
                    errors.Add($"{fileName}:{where}.type 缺失或非非空字符串" +
                               $"({(typeNode == null ? "缺失" : typeNode.Kind.ToString())})");
                }
                else if (!seenTypes.Add(typeNode.Str))
                {
                    errors.Add(
                        $"{fileName}:{where}.type「{typeNode.Str}」重复 —— 家具目录每类型恰一行" +
                        "(重复会让「取哪一行」成为隐式规则 = 静默失序)");
                }

                // tier —— int 计数(ADR-006 D-21-17:不在 Fix 解析集),域 [0, TIER_MAX]
                if (!row.TryGet("tier", out JsonNode tierNode))
                {
                    errors.Add($"{fileName}:{where} 缺 tier —— 缺字段 = 构建失败(O-24-1)");
                }
                else if (tierNode.Kind != JsonNodeKind.Integer)
                {
                    errors.Add($"{fileName}:{where}.tier 须为整数字面量(实得 {tierNode.Kind}={tierNode.RawText})" +
                               " —— tier 是 int 计数,移出 Fix 解析集(D-21-17)");
                }
                else if (tierNode.Int < 0 || tierNode.Int > declaredTierMax)
                {
                    errors.Add($"{fileName}:{where}.tier={tierNode.Int} ∉ [0, TIER_MAX={declaredTierMax}]" +
                               " —— GDD F-24-2 档位域(0 = 无档)");
                }

                // e_env —— Fix 字符串(ADR-014 §四:禁数值 token)
                ReadRequiredFix(row, "e_env", where, fileName, errors, out _);
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);
        }

        /// <summary>schema_version 读取 + 与运行时头契约比对(只做域检查形同虚设 —— 承 OutcomeTableBinder R-4)。</summary>
        /// <remarks>本重载用**全局默认** <see cref="CookedFormat.SchemaVersion"/>。
        /// ⚠️ **产物一旦有 per-product 版本(如 `build_modules` 现为 2),必须改用下面的显式重载** ——
        /// 否则源表 schema 抬到 2 而本处仍比 1 ⇒ 烘焙恒拒。per-product 先例见
        /// `CookedCodec.ExpectedItemSchemaVersion`。</remarks>
        internal static uint ReadSchemaVersion(JsonNode root, string fileName, List<string> errors)
            => ReadSchemaVersion(root, fileName, CookedFormat.SchemaVersion, errors);

        /// <summary>schema_version 读取 + 与**本产物期望值**比对(per-product 口径)。</summary>
        internal static uint ReadSchemaVersion(
            JsonNode root, string fileName, uint expected, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode n))
            {
                errors.Add($"{fileName}:缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"{fileName}:schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            uint v = (uint)n.Int;
            if (v != expected)
            {
                errors.Add(
                    $"{fileName}:schema_version={v} ≠ 期望 {expected} " +
                    "(源表 schema 与运行时头契约不一致 ⇒ 构建期硬失败)");
                return 0;
            }
            return v;
        }

        /// <summary>读取必填字符串(空串亦拒 —— 「写了但没值」不是「缺席」)。</summary>
        internal static bool ReadRequiredString(
            JsonNode obj, string key, string where, string fileName, List<string> errors, out string value)
        {
            value = null;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{fileName}:{where} 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{fileName}:{where}.{key} 须为 JSON 字符串(实得 {n.Kind}={n.RawText})");
                return false;
            }
            if (string.IsNullOrEmpty(n.Str))
            {
                errors.Add($"{fileName}:{where}.{key} 为空串 —— 「写了但没值」不是「缺席」,拒");
                return false;
            }
            value = n.Str;
            return true;
        }

        /// <summary>读取必填 Fix(**只接受 JSON 字符串**;数值 token 结构性硬拒,ADR-014 §四)。</summary>
        internal static bool ReadRequiredFix(
            JsonNode obj, string key, string where, string fileName, List<string> errors, out Fix value)
        {
            value = Fix.Zero;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{fileName}:{where} 缺 {key}");
                return false;
            }
            if (n.Kind == JsonNodeKind.Null)
            {
                errors.Add($"{fileName}:{where}.{key} 为 null —— 必填(ADR-014 §四)");
                return false;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add(
                    $"{fileName}:{where}.{key} 须为 JSON 字符串 Fix 字面量(实得 {n.Kind}={n.RawText}) —— " +
                    "数值 token 会被词法器转成 CLR 基元,浮点即已在内存(ADR-014 §四 / ADR-006)");
                return false;
            }
            try
            {
                value = FixParse.Parse(n.Str);
                return true;
            }
            catch (FormatException ex)
            {
                errors.Add($"{fileName}:{where}.{key}=\"{n.Str}\" FixParse 失败:{ex.Message}");
                return false;
            }
            catch (DivideByZeroException ex)
            {
                errors.Add($"{fileName}:{where}.{key}=\"{n.Str}\" FixParse 失败:{ex.Message}");
                return false;
            }
        }

        /// <summary>读取必填正整数(ADR-006 D-21-17:int 计数不在 Fix 解析集,合法 JSON 整数 token)。</summary>
        internal static bool ReadRequiredPositiveInt(
            JsonNode obj, string key, string where, string fileName, List<string> errors, out long value)
        {
            value = 0;
            if (!obj.TryGet(key, out JsonNode n))
            {
                errors.Add($"{fileName}:{where} 缺 {key}");
                return false;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add(
                    $"{fileName}:{where}.{key} 须为整数字面量(实得 {n.Kind}={n.RawText}) —— " +
                    "int 计数移出 Fix 解析集(D-21-17)");
                return false;
            }
            if (n.Int <= 0)
            {
                errors.Add($"{fileName}:{where}.{key}={n.Int} 须为正整数");
                return false;
            }
            value = n.Int;
            return true;
        }

        /// <summary>未知键 = 烘焙期硬失败(ADR-014 §三;拼写错误会静默丢字段)。</summary>
        internal static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, string fileName, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{fileName}:{where} 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }

        /// <summary>拒含控制字符的标识符字符串(ADJ 键的单射性前提,D9)。
        /// <para>ADJ 有向键 = <c>a + U+0001 + b</c>。若 <c>a</c> 或 <c>b</c> 自身含控制字符,
        /// 不同的 <c>(a,b)</c> 可拼出同一个键 ⇒ 静默错配。**必须在建键前校验**,
        /// 不能只写在注释里(评审 D9:原注释声称「构建期另拒控制字符」但零实现)。</para></summary>
        internal static bool RejectControlChars(
            string value, string fieldPath, string fileName, List<string> errors)
        {
            if (string.IsNullOrEmpty(value)) return true;

            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i]))
                {
                    errors.Add(
                        $"{fileName}:{fieldPath} 含控制字符 U+{(int)value[i]:X4}(下标 {i}) —— " +
                        "ADJ 键 = a + U+0001 + b,单射性要求类型 id 不含控制字符(D9)");
                    return false;
                }
            }

            return true;
        }

        /// <summary>子句类型字面量 → 枚举(闭集外 ⇒ false;不另设分支)。</summary>
        internal static bool TryClauseKind(string literal, out ClinicClauseKind kind)
        {
            for (int i = 0; i < ClauseKindLiterals.Length; i++)
            {
                if (string.Equals(ClauseKindLiterals[i], literal, StringComparison.Ordinal))
                {
                    kind = (ClinicClauseKind)i;
                    return true;
                }
            }
            kind = ClinicClauseKind.MinCount;
            return false;
        }

        /// <summary>方向字面量 → 枚举(闭集外 ⇒ false;GDD 规则二的方向确定性)。</summary>
        internal static bool TryClauseDirection(string literal, out ClinicClauseDirection dir)
        {
            switch (literal)
            {
                case "+x": dir = ClinicClauseDirection.PlusX; return true;
                case "+y": dir = ClinicClauseDirection.PlusY; return true;
                default: dir = ClinicClauseDirection.None; return false;
            }
        }

        private static long[] DeriveRaws(IReadOnlyList<string> literals)
        {
            var raws = new long[literals.Count];
            for (int i = 0; i < raws.Length; i++)
                raws[i] = FixParse.Parse(literals[i]).Raw;
            return raws;
        }
    }
}
