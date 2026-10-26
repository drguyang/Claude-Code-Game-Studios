// S20-12 · inventory story-004 —— `inventory_constants.json` 的**阶段 2 绑定器**(AC-20-13 的构建期执行体)。
//
// 权威来源:
//   ADR-014 §三(阶段 2:已知键白名单 · schema_version · 聚合 throw)· §五(装载期硬失败纪律,E-13 同款)
//   ADR-006 §Decision 一 D-21-17(`weight` / `stack_max` / cap 同族 = int 计数,**不是** Fix)
//   GDD design/gdd/inventory-and-items.md
//     · :283 / §Tuning Knobs :719 —— `CARRY_CAP` 值 60(2026-10-06 代拍,OQ-20-2 结案,值可调)
//     · :296-298 —— BL-6① `CARRY_CAP` **单源所有者 = 20**(52 只读)
//     · 边例 :624 —— `CARRY_CAP` 未定值 ⇒ 装载期硬失败(禁 null 兜底 / 默认容量)
//     · AC-20-13 · AC-20-21 · AC-20-24
//   story production/epics/inventory-items/story-004-carryload-and-cap-adjudication.md
//
// ⚠️ **本件是 `inventory_constants.json` 的唯一构建期校验点**(与 `ForagingConstantsBinder` 同口径:
//   Sim 侧 `CarryCapacity` 的构造期拒收是**纵深防御**,不是第二真源 —— 两侧判据逐条同源)。
//
// ⚠️ **AC-20-13 的五条负路径全在此落地**(2026-10-26 用户裁定「改测『装载期校验』的负路径」):
//   ① 缺键(null 兜底会被静默当成 0)  ② cap = 0  ③ cap < 0
//   ④ cap 越 int 域  ⑤ cap 非 int token(字符串 `"60"` / 浮点 `60.0` / `null`)
//   另加 ⑥ **Fix 口径误用**(JSON 里写 `"3/4"` 这类定点字面量)—— 见 D-21-17 邻域纪律。
//   ⚠️ 五条都必须**各抛一条具名错误**,不得合并成一句「carry_cap 非法」:
//      合并会让「数据没填」与「数据填错」在装配期日志里不可分。
//
// ⚠️ **本件同样拒「代码里写死 cap」**:`Sim/` 与 `Sim.Contracts/` 下出现
//   `CarryCap` 类常量字面量 ⇒ 由 `CarryCapExistenceGate` 的 AC-20-24 半边抓(不在本件)。
//
// ⚠️ **本类不抛裸 Exception**:错误进列表,聚合后一次 `BakeValidationException`(既有门零散 throw 纪律)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`inventory_constants.json` 的键白名单与读取辅助(阶段 2 绑定器的词表半边)。</summary>
    public static class InventoryConstantsSchema
    {
        /// <summary>源文件名(单一出处;烘焙装载器 / 门 / 测试共用)。</summary>
        public const string FileName = "inventory_constants.json";

        /// <summary>源文件所在目录(相对仓根)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>顶层键白名单(ADR-014 §三:未知键 = 硬失败)。
        /// <para>⚠️ **刻意只有 carry_cap 一个旋钮键** —— `W_MIN` 是 `min{ItemDef.weight}` 的派生量,
        /// 真源 = `item_database_items.json`;写进本表 = 第二容量真值(AC-20-24 禁止)。
        /// 「视觉容量」「剩余空间」类键同禁(BL-27:slack 由 42 自算)。</para>
        /// </summary>
        internal static readonly string[] RootKeys =
        {
            "schema_version", "_note", "carry_cap",
        };

        /// <summary>除 <c>schema_version</c> / <c>_note</c> 外全部必填。</summary>
        internal static readonly string[] RequiredKeys =
        {
            "carry_cap",
        };
    }

    /// <summary>`inventory_constants.json` 的阶段 2 绑定器(AC-20-13 的构建期执行体)。</summary>
    public static class InventoryConstantsBinder
    {
        /// <summary>源文件名(转发自 schema,单一出处)。</summary>
        public const string FileName = InventoryConstantsSchema.FileName;

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>`CARRY_CAP`(已校验 &gt; 0)。</summary>
            public readonly int CarryCap;

            /// <summary>源 schema_version(写方头槽的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造。</summary>
            public BindResult(int carryCap, uint schemaVersion)
            {
                CarryCap = carryCap; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>从仓根绑定(读源 → 阶段 1 词法 → 阶段 2 绑定 + 校验)。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/data/</c> 的那一层)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="repoRoot"/> 为 null。</exception>
        /// <exception cref="BakeValidationException">源缺失 / 词法 / 任一校验失败(聚合)。</exception>
        public static BindResult BindFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = System.IO.Path.Combine(
                repoRoot, InventoryConstantsSchema.DataDirName, FileName);
            if (!System.IO.File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}" });

            return Bind(System.IO.File.ReadAllText(path));
        }

        /// <summary>从 JSON 串绑定(负向夹具入口;与 <see cref="BindFromRepo"/> 同一路径)。</summary>
        /// <param name="json">源文件全文。</param>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 阶段1 词法失败:{lexErr}" });

            if (root == null || root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"{FileName} 根须为对象(实得 {root?.Kind})" });

            RejectUnknownKeys(root, errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);

            // 必填键存在性(一次性报全,便于一轮修完)。
            foreach (string key in InventoryConstantsSchema.RequiredKeys)
                if (!root.TryGet(key, out _))
                    errors.Add($"{FileName}:缺必填键 \"{key}\" —— " +
                               "CARRY_CAP 未定值时装载必须硬失败,不得以默认容量兜底(AC-20-13)");

            int carryCap = ReadCarryCap(root, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);
            return new BindResult(carryCap, schemaVersion);
        }

        /// <summary>拒收白名单外的键(ADR-014 §三)。</summary>
        private static void RejectUnknownKeys(JsonNode root, List<string> errors)
        {
            var allowed = new HashSet<string>(InventoryConstantsSchema.RootKeys, StringComparer.Ordinal);
            foreach (string key in root.Keys)
                if (!allowed.Contains(key))
                    errors.Add($"{FileName}:未登记键 \"{key}\" —— ADR-014 §三 白名单外,烘焙期硬失败。" +
                               "⚠️ 若它想表达「剩余空间 / 视觉容量」,那是 AC-20-24 明令禁止的第二容量真值");
        }

        /// <summary>`schema_version` 域校验(须为 u32 域整数,且与运行时头契约一致)。</summary>
        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode sv))
            {
                errors.Add($"{FileName}:缺 schema_version");
                return 0u;
            }
            if (sv.Kind != JsonNodeKind.Integer || sv.Int < 0 || sv.Int > uint.MaxValue)
            {
                errors.Add($"{FileName}:schema_version 须为 u32 域整数(实得 {sv.Kind}={sv.RawText})");
                return 0u;
            }
            if ((uint)sv.Int != CookedFormat.SchemaVersion)
            {
                errors.Add($"{FileName}:schema_version={(uint)sv.Int} ≠ 期望 {CookedFormat.SchemaVersion} " +
                           "(源表 schema 与运行时头契约不一致 ⇒ 构建期硬失败)");
                return 0u;
            }
            return (uint)sv.Int;
        }

        /// <summary>`carry_cap` 的五条负路径 + 值域(AC-20-13 的全部判据面)。</summary>
        /// <returns>合法时返回 cap;**已登记错误时返回 0**(调用方不得用——错误会在聚合后 throw)。</returns>
        private static int ReadCarryCap(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("carry_cap", out JsonNode n))
            {
                // 缺失已由 RequiredKeys 循环登记 ⇒ 此处静默返回,避免同一缺键报两遍
                // (承 ForagingConstantsSchema.ReadRequiredInt 同款口径)。
                return 0;
            }

            // ⑤ 非 int token(字符串 / 浮点 / 布尔 / null / 对象 / 数组)。
            if (n.Kind != JsonNodeKind.Integer)
            {
                if (n.Kind == JsonNodeKind.String)
                    errors.Add(
                        $"{FileName}:carry_cap 须为 JSON **整数** token,实得字符串 {n.RawText} —— " +
                        "cap 是 int 计数口径(D-21-17,与 weight / stack_max 同族),**不是 Fix**;" +
                        "写 \"3/4\" 这类定点字面量须改走 int(AC-20-13 边例)");
                else if (n.Kind == JsonNodeKind.Float)
                    errors.Add(
                        $"{FileName}:carry_cap 须为 JSON 整数 token,实得浮点 {n.RawText} —— " +
                        "浮点承载 cap 会让「恰等界」比较不可判(AC-20-13 边例)");
                else
                    errors.Add(
                        $"{FileName}:carry_cap 须为 JSON 整数 token(实得 {n.Kind})—— " +
                        "null / bool / 对象 / 数组均无容量语义(AC-20-13)");
                return 0;
            }

            // ④ 越 int 域(阶段 1 的 Int 是 long ⇒ 这里是真的越界判据,不是死码)。
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{FileName}:carry_cap = {n.Int} 越出 int 域 [{int.MinValue}, {int.MaxValue}] " +
                           "—— 容量是 int 计数口径(D-21-17);越界值不得静默截断(AC-20-13)");
                return 0;
            }

            int cap = (int)n.Int;

            // ② cap = 0 / ③ cap < 0:与「未定值」同一种失败(无容量语义),
            //    但**分开报**——装配期要能一眼区分「填了 0」与「填了负数」。
            if (cap == 0)
            {
                errors.Add($"{FileName}:carry_cap = 0 —— 零容量即「一件都装不下」,与未定值不可分;" +
                           "装载必须硬失败(AC-20-13)");
                return 0;
            }
            if (cap < 0)
            {
                errors.Add($"{FileName}:carry_cap = {cap} < 0 —— 容量不可能为负;" +
                           "装载必须硬失败(AC-20-13)");
                return 0;
            }

            return cap;
        }
    }
}
