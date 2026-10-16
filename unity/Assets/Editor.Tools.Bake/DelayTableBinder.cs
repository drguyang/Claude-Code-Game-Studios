// S13 · story-003 —— 53 延迟表(assets/data/53_delay_table.json)的阶段 2 绑定器(唯一构建期校验点)。
//
// 权威来源:
//   ADR-014 §三(阶段 2 per-schema 绑定:未知键白名单硬失败 · 校验聚合 throw · 禁数值经 double 中转)
//   GDD design/gdd/medical-consequences.md F-53.2 `:268-294`
//     · 定义域**恰 = 回响轴三值**(`:285-291`「定义域收窄,不是破约束」)
//     · `DelayTicks > 0` 硬约束(AC-53-06 承 AC-37-25 ①;≤0 构建期 `throw`)
//   卡面 story-003 QA AC-2 三类构建期拒收:
//     ① 定义域内某值 ≤ 0;② 表缺行(全覆盖);③ 定义域外作 key(结局轴 / NoEcho —— schema 错)
//   键序裁定(用户 2026-10-16):**定义域用白名单单机制** —— binder 只允许
//     { residual, recurrence, trial_history },其余一律同一条规则判 schema 错
//     (含 no_echo / patient_lives / patient_loses,**不另设分支**)
//
// ⚠️ 本件 = 延迟表的**唯一构建期校验点**;Sim 侧 `DelayTable53.Table` 构造器是同判据的纵深防御
//   (防「绕过 Bake 直构表」),两侧口径须一致。
//
// ⚠️ 白名单名 ↔ `Outcome53.Outcome` ordinal 的映射**在本件内以枚举符号直用**
//   (Editor.Tools.Bake 引用 Sim ⇒ 零字符串副本第二真源;与 OutcomeTableBinder 同纪律)。
//
// ⚠️ 零浮点(承 AC-53-03 / ADR-006 边界):delay_ticks 须为 Integer token;Float token
//   结构性硬失败(词法层**接受** Float,拒收发生在类型检查 —— 同 OutcomeTable N-9 口径)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Consequences;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`53_delay_table.json` 的阶段 2 绑定器(三条构建期拒收,聚合 throw)。
    /// <para>⚠️ public:其 <see cref="BindResult"/> 经 <see cref="DelayTable53Bake.BakeOutput"/>
    /// 公开承载(容器可访问性须 ≥ 成员,CS0050/51/52 —— 同 OutcomeTableBinder 教训)。</para></summary>
    public static class DelayTableBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败)──

        private static readonly string[] RootKeys = { "schema_version", "rows" };
        private static readonly string[] RowKeys = { "outcome", "delay_ticks" };

        /// <summary>定义域白名单(恰 3 员)—— 名字 ↔ 枚举成员**成对**登记,零漂移。
        /// <para>⚠️ 白名单外的任何 key(含 <c>no_echo</c> / <c>patient_lives</c> /
        /// <c>patient_loses</c>)走**同一条**拒收规则(用户裁定 2026-10-16:单机制,不另设分支)。</para>
        /// <para>⚠️ <c>internal</c>(非 private):<see cref="DelayTable53Bake.Materialize"/> 的
        /// **跨侧定义域等值断言**(B-W3)须读本表,与 Sim 侧 <c>DelayTable53.DomainOutcomes</c> 逐项比对。</para></summary>
        internal static readonly (string Name, Outcome53.Outcome Outcome)[] DomainRows =
        {
            ("residual", Outcome53.Outcome.Residual),
            ("recurrence", Outcome53.Outcome.Recurrence),
            ("trial_history", Outcome53.Outcome.TrialHistory),
        };

        /// <summary>绑定结果(供写方落盘 / 测试直用 / Sim 侧表物化)。</summary>
        public readonly struct BindResult
        {
            /// <summary>Outcome 枚举 → 延迟 tick 数(恒 &gt; 0)。</summary>
            public readonly IReadOnlyDictionary<Outcome53.Outcome, long> Rows;

            /// <summary>源 schema_version(= 写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果(写方 / 测试直用)。</summary>
            public BindResult(IReadOnlyDictionary<Outcome53.Outcome, long> rows, uint schemaVersion)
            {
                Rows = rows; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + 三条构建期拒收。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。</summary>
        /// <exception cref="BakeValidationException">词法失败 / 三条校验任一(聚合)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"53_delay_table.json 阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"53_delay_table.json 根须为对象(实得 {root.Kind})" });

            RejectUnknownKeys(root, RootKeys, "根", errors);

            uint schemaVersion = ReadSchemaVersion(root, errors);

            // ⚠️ 此处**早 throw**:无 rows 数组 ⇒ 无行可解析,行层校验不可能进行。
            // 已聚合的根层错误(未知键 / schema_version)随 errors 一并报出 ——
            // 这是「一次报全部」口径的**唯一豁免点**(行层错误在结构上不可达)。
            if (!root.TryGet("rows", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add("53_delay_table.json 缺 rows 数组");
                throw new BakeValidationException(errors);
            }

            var rows = ParseRows(rowsNode, errors);

            // 拒收 ②:全覆盖(白名单三支全在;缺行 = 该回响静默不发,不可观察)。
            var missing = new List<string>();
            for (int i = 0; i < DomainRows.Length; i++)
                if (!rows.ContainsKey(DomainRows[i].Outcome))
                    missing.Add(DomainRows[i].Name);
            if (missing.Count > 0)
                errors.Add("覆盖校验失败:缺 outcome 行 " + string.Join(", ", missing) +
                    " —— 定义域(回响轴)内三支须全在(卡面 AC-2 Edge「表缺 outcome 行 = 也红」;" +
                    "缺行会让该回响静默不发 = 静默失败)");

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        /// <summary>逐行解析(白名单 + delay_ticks &gt; 0;重复行拒收)。</summary>
        private static Dictionary<Outcome53.Outcome, long> ParseRows(JsonNode rowsNode, List<string> errors)
        {
            var rows = new Dictionary<Outcome53.Outcome, long>();
            for (int i = 0; i < rowsNode.Items.Count; i++)
                ParseRow(rowsNode.Items[i], i, rows, errors);
            return rows;
        }

        /// <summary>解析单行并登记(单行解析体拆出 —— 使 <see cref="ParseRows"/> 不超行数标准)。</summary>
        private static void ParseRow(
            JsonNode row, int i, Dictionary<Outcome53.Outcome, long> rows, List<string> errors)
        {
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"rows[{i}] 须为对象(实得 {row.Kind})");
                return;
            }
            RejectUnknownKeys(row, RowKeys, $"rows[{i}]", errors);

            if (!row.TryGet("outcome", out JsonNode nameNode) || nameNode.Kind != JsonNodeKind.String)
            {
                errors.Add($"rows[{i}] 缺字符串 outcome(实得 " +
                           $"{(nameNode == null ? "缺失" : nameNode.Kind.ToString())})");
                return;
            }

            // 拒收 ③:定义域白名单单机制 —— 域外一律 schema 错(结局轴 / NoEcho 无特例分支)。
            Outcome53.Outcome outcome;
            if (!TryDomain(nameNode.Str, out outcome))
            {
                errors.Add($"rows[{i}] outcome「{nameNode.Str}」∉ 定义域(F-53.2 `:285-291`:" +
                           "定义域恰 = 回响轴三值 " + string.Join("/", DomainNames()) +
                           ";结局轴不是 key(9 结算、53 只读)+ NoEcho 无痕不回响" +
                           "(`:259` / `:317`)⇒ 收进本表即 schema 错,非「给它一个值」)");
                return;
            }

            if (!row.TryGet("delay_ticks", out JsonNode ticksNode))
            {
                errors.Add($"rows[{i}] 缺 delay_ticks(必填 —— 拼写错误会静默丢字段 ⇒ 硬失败)");
                return;
            }
            long ticks = ReadDelayTicks(i, nameNode.Str, ticksNode, errors);
            if (ticks == long.MinValue) return;

            if (rows.ContainsKey(outcome))
                errors.Add($"rows[{i}]:outcome「{nameNode.Str}」重复(定义域内每支恰一行)");
            else
                rows[outcome] = ticks;
        }

        /// <summary>拒收 ①:delay_ticks 须为正整数(≤0 构建期 throw;Float 结构性拒收)。</summary>
        /// <returns>非法时返回 <see cref="long.MinValue"/> 哨兵(错误已记,由调用方 continue)。</returns>
        private static long ReadDelayTicks(int i, string outcomeName, JsonNode node, List<string> errors)
        {
            if (node.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"rows[{i}]「{outcomeName}」delay_ticks 须为整数" +
                           $"(实得 {node.Kind}={node.RawText})—— 浮点字面量结构性硬失败" +
                           "(ADR-006 边界 / AC-53-03;tick 计数是整数,20 Hz)");
                return long.MinValue;
            }
            if (node.Int <= 0)
            {
                errors.Add($"rows[{i}]「{outcomeName}」delay_ticks={node.Int} ≤ 0 —— AC-53-06 /" +
                           " F-53.2 `:293` 硬约束:定义域内每一条须 > 0" +
                           "(= 0 会让后果与结案同 tick ⇒ 可被归因,破规则六不可归因)");
                return long.MinValue;
            }
            return node.Int;
        }

        /// <summary>白名单名 → 枚举成员(序数比较;域外 ⇒ false)。</summary>
        private static bool TryDomain(string name, out Outcome53.Outcome outcome)
        {
            for (int i = 0; i < DomainRows.Length; i++)
                if (string.Equals(DomainRows[i].Name, name, StringComparison.Ordinal))
                {
                    outcome = DomainRows[i].Outcome;
                    return true;
                }
            outcome = default;
            return false;
        }

        private static string[] DomainNames()
        {
            var names = new string[DomainRows.Length];
            for (int i = 0; i < DomainRows.Length; i++) names[i] = DomainRows[i].Name;
            return names;
        }

        private static uint ReadSchemaVersion(JsonNode root, List<string> errors)
        {
            if (!root.TryGet("schema_version", out JsonNode n))
            {
                errors.Add("53_delay_table.json 缺 schema_version");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer || n.Int < 0 || n.Int > uint.MaxValue)
            {
                errors.Add($"schema_version 须为 u32 域整数(实得 {n.Kind}={n.RawText})");
                return 0;
            }
            uint v = (uint)n.Int;
            // 与期望值比对(承 OutcomeTableBinder R-4 教训:只做域检查形同虚设 ——
            // 读方 CookedCodec.OpenChecked 正是与 CookedFormat.SchemaVersion 比对,
            // 构建期不拦 ⇒ 错误表一路烘到运行期才炸)。
            if (v != CookedFormat.SchemaVersion)
            {
                errors.Add($"schema_version={v} ≠ 期望 {CookedFormat.SchemaVersion}" +
                           "(源表 schema 与运行时头契约不一致 ⇒ 构建期硬失败)");
                return 0;
            }
            return v;
        }

        private static void RejectUnknownKeys(
            JsonNode obj, string[] allowed, string where, List<string> errors)
        {
            foreach (string key in obj.Keys)
            {
                bool ok = false;
                for (int i = 0; i < allowed.Length; i++)
                    if (string.Equals(allowed[i], key, StringComparison.Ordinal)) { ok = true; break; }
                if (!ok)
                    errors.Add($"{where} 未知键「{key}」[ADR-014·unknown-key]" +
                               "(拼写错误会静默丢字段 ⇒ 构建期硬失败)");
            }
        }
    }
}
