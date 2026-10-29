// Story 001 · clinic-machine —— ADJ_TABLE(assets/data/clinicmachine_adj.json)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-014 §三(阶段 2 绑定 + 聚合 throw)· §四(Fix 字段必须字符串)
//   GDD design/gdd/clinic-machine.md:
//     · F-24-1 公共定义 —— w(a,b) 无序对、可负;烘焙期硬门 **∀a,b: w(a,b) == w(b,a)**,
//       不等 = 构建失败(AC-24-06;「无序对」不是作者纪律,是门)
//     · O-24-5 —— n_max × TIER_MAX < 2^31 且 |W_MIN| <= W_MAX
//   OQ-24-5(2026-10-07)n_max = 64 · OQ-24-6(2026-09-25)W 域 = [-1/4, +1/4]
//
// ⚠️ 对称性检查在**绑定后**做(卡面 Implementation Note 3):遍历全部 (a,b) 对,失败消息含表名与键对。
// ⚠️ **单向缺项视为不对称**(卡面 QA AC-2 Edge):只有 (a,b) 无 (b,a) ⇒ 拒(镜像 / 旋转后的同一布局
//   会算出不同分 = 与方向相关的静默失败)。
// ⚠️ 本件只登记 **有向条目**;`w(a,b) = w(b,a)` 的等价性由门保证,故作者须双向各写一行(种子即如此)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>clinicmachine_adj.json</c> 的阶段 2 绑定器(AC-24-06 对称门 + O-24-5 界)。</summary>
    public static class ClinicMachineAdjBinder
    {
        /// <summary>源文件名(单一出处)。</summary>
        public const string FileName = "clinicmachine_adj.json";

        /// <summary>一条有向邻接权重 <c>w(a,b)</c>。</summary>
        public readonly struct AdjPair
        {
            /// <summary>左端家具类型 id。</summary>
            public readonly string A;

            /// <summary>右端家具类型 id。</summary>
            public readonly string B;

            /// <summary>权重(∈ [W_MIN, W_MAX];可负)。</summary>
            public readonly Fix W;

            /// <summary>构造一条。</summary>
            public AdjPair(string a, string b, Fix w) { A = a; B = b; W = w; }
        }

        /// <summary>绑定结果。</summary>
        public readonly struct BindResult
        {
            /// <summary>有向条目集(声明序;编码时另行全序排序)。</summary>
            public readonly IReadOnlyList<AdjPair> Pairs;

            /// <summary>单房间格数上界(24 自有;OQ-24-5 已裁 = 64)。</summary>
            public readonly long NMax;

            /// <summary>W 域下界(须 &lt; 0)。</summary>
            public readonly Fix WMin;

            /// <summary>W 域上界(须 &gt; 0 且 |W_MIN| &lt;= W_MAX)。</summary>
            public readonly Fix WMax;

            /// <summary>源 schema_version。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(
                IReadOnlyList<AdjPair> pairs, long nMax, Fix wMin, Fix wMax, uint schemaVersion)
            {
                Pairs = pairs; NMax = nMax; WMin = wMin; WMax = wMax; SchemaVersion = schemaVersion;
            }
        }

        /// <summary>绑定 + AC-24-06 对称门 + O-24-5 两条界(聚合后一次 throw)。</summary>
        /// <exception cref="BakeValidationException">任一违例(聚合)。</exception>
        public static BindResult Bind(string json)
        {
            var errors = new List<string>();

            JsonNode root = ClinicMachineSchema.BeginTable(
                json, ClinicMachineSchema.AdjRootKeys, FileName, errors, out uint schemaVersion);

            ReadRootScalars(root, errors,
                out long nMax, out Fix wMin, out bool hasWMin, out Fix wMax, out bool hasWMax);

            // ⚠️ 早 throw 豁免点:无 pairs 数组 ⇒ 行层校验结构上不可达。
            if (!root.TryGet("pairs", out JsonNode rowsNode) || rowsNode.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{FileName}:缺 pairs 数组");
                throw new BakeValidationException(errors);
            }

            var pairs = new List<AdjPair>(rowsNode.Items.Count);
            // 键 = "a\u0001b"(Ordinal;单射 —— 类型 id 不含 \u0001,构建期另拒控制字符)
            var seen = new Dictionary<string, long>(StringComparer.Ordinal);

            for (int i = 0; i < rowsNode.Items.Count; i++)
            {
                AdjPair? pair = ParsePair(
                    rowsNode.Items[i], i, hasWMin, wMin, hasWMax, wMax, seen, errors);
                if (pair.HasValue) pairs.Add(pair.Value);
            }

            AssertTableWide(pairs, seen, nMax, wMin, hasWMin, wMax, hasWMax, errors);

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(pairs, nMax, wMin, wMax, schemaVersion);
        }

        /// <summary>根层三个标量(<c>n_max</c> / <c>w_min</c> / <c>w_max</c>);缺失或坏 token 落 errors。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 <see cref="Bind"/> 50 行 &gt; 40;债册 `TD-clinic-14` / 评审 S16)。
        /// **三个字段的读取次序与报文原文逐字未变**。</remarks>
        private static void ReadRootScalars(
            JsonNode root, List<string> errors,
            out long nMax, out Fix wMin, out bool hasWMin, out Fix wMax, out bool hasWMax)
        {
            nMax = 0;
            ClinicMachineSchema.ReadRequiredPositiveInt(root, "n_max", "根", FileName, errors, out nMax);

            wMin = Fix.Zero;
            hasWMin = ClinicMachineSchema.ReadRequiredFix(root, "w_min", "根", FileName, errors, out wMin);

            wMax = Fix.Zero;
            hasWMax = ClinicMachineSchema.ReadRequiredFix(root, "w_max", "根", FileName, errors, out wMax);
        }

        /// <summary>表级三门:非空 → O-24-5 两条界 → AC-24-06 对称(**此次序是契约**)。</summary>
        /// <remarks>🔁 <b>2026-10-29 拆分</b>(原 <see cref="Bind"/> 50 行 &gt; 40;债册 `TD-clinic-14` / 评审 S16)。
        /// 三门**不可重排** —— 聚合 throw 的报文次序是 ADR-014 确定性面的一部分(`TD-clinic-06` 同族关注点)。</remarks>
        private static void AssertTableWide(
            List<AdjPair> pairs, Dictionary<string, long> seen,
            long nMax, Fix wMin, bool hasWMin, Fix wMax, bool hasWMax, List<string> errors)
        {
            if (pairs.Count == 0 && errors.Count == 0)
                errors.Add($"{FileName}:pairs 为空 —— 空邻接表让 adj_sum 恒 0(静默中性),拒");

            // O-24-5:两条界(参数化 —— 边界值可直驱)
            if (hasWMin && hasWMax && nMax > 0)
                ClinicMachineSchema.AssertOverflowBounds(
                    nMax, ClinicMachineSchema.TierMax, wMin.Raw, wMax.Raw, errors);

            // AC-24-06:对称门(**绑定后**遍历全部对)
            if (pairs.Count > 0)
                AssertSymmetry(pairs, seen, errors);
        }

        /// <summary>逐行解析(白名单 + W 域 + 重复键)。</summary>
        private static AdjPair? ParsePair(
            JsonNode row, int i, bool hasWMin, Fix wMin, bool hasWMax, Fix wMax,
            Dictionary<string, long> seen, List<string> errors)
        {
            string where = $"pairs[{i}]";
            if (row.Kind != JsonNodeKind.Object)
            {
                errors.Add($"{FileName}:{where} 须为对象(实得 {row.Kind})");
                return null;
            }

            ClinicMachineSchema.RejectUnknownKeys(
                row, ClinicMachineSchema.AdjRowKeys, where, FileName, errors);

            bool ok = ClinicMachineSchema.ReadRequiredString(
                row, "a", where, FileName, errors, out string a);
            ok &= ClinicMachineSchema.ReadRequiredString(
                row, "b", where, FileName, errors, out string b);

            // D9:建键前拒控制字符 —— Key(a,b) = a + U+0001 + b 的单射性前提。
            // 原注释声称「构建期另拒控制字符」但零实现;评审 D9 后补上(现在注释为真)。
            if (ok)
            {
                ok &= ClinicMachineSchema.RejectControlChars(a, $"{where}.a", FileName, errors);
                ok &= ClinicMachineSchema.RejectControlChars(b, $"{where}.b", FileName, errors);
            }

            if (!ClinicMachineSchema.ReadRequiredFix(
                    row, "w", where, FileName, errors, out Fix w))
                ok = false;
            else if (hasWMin && hasWMax)
            {
                // W 域 [w_min, w_max](OQ-24-6:[−1/4, +1/4])
                if (w.Raw < wMin.Raw || w.Raw > wMax.Raw)
                {
                    errors.Add(
                        $"{FileName}:{where}(w(\"{a ?? "?"}\",\"{b ?? "?"}\")) w raw={w.Raw} " +
                        $"∉ W 域 [{wMin.Raw}, {wMax.Raw}] —— F-24-1 / OQ-24-6(W 域 = [-1/4, +1/4])");
                }
            }

            if (!ok) return null;

            // 自对(a == b)合法(同类相邻),且与自身对称 —— 无需特判分支。

            string key = Key(a, b);
            if (seen.ContainsKey(key))
            {
                errors.Add(
                    $"{FileName}:{where} 重复键 (\"{a}\",\"{b}\") —— ADJ_TABLE 每有向对恰一行" +
                    "(重复会让「取哪一行」成为隐式规则 = 静默失序)");
                return null;
            }
            seen[key] = w.Raw;

            return new AdjPair(a, b, w);
        }

        /// <summary>AC-24-06:遍历全部 (a,b) 断言 <c>w(a,b) == w(b,a)</c>。</summary>
        private static void AssertSymmetry(
            List<AdjPair> pairs, Dictionary<string, long> seen, List<string> errors)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                AdjPair p = pairs[i];
                string mirrorKey = Key(p.B, p.A);

                if (!seen.TryGetValue(mirrorKey, out long mirrorRaw))
                {
                    errors.Add(
                        $"{FileName}:不对称 —— 单向缺项 w(\"{p.A}\",\"{p.B}\")={p.W.Raw} 存在,但 " +
                        $"w(\"{p.B}\",\"{p.A}\") 缺失。AC-24-06 / F-24-1:ADJ_TABLE 是无序对," +
                        "单向缺项会让镜像 / 旋转后的同一布局算出不同分(与方向相关的静默失败)");
                    continue;
                }

                if (mirrorRaw != p.W.Raw)
                {
                    errors.Add(
                        $"{FileName}:不对称 w(\"{p.A}\",\"{p.B}\") raw={p.W.Raw} ≠ " +
                        $"w(\"{p.B}\",\"{p.A}\") raw={mirrorRaw} —— AC-24-06 / F-24-1:对称性由门保证," +
                        "非作者纪律(2026-09-17 复核,阻塞 3/6)");
                }
            }
        }

        /// <summary>有向键(单射;分隔符 U+0001 不在类型 id 字符集内 —— 绑定期另拒控制字符)。</summary>
        private static string Key(string a, string b) => a + "\u0001" + b;
    }
}
