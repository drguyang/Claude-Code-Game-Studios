// 权威来源:ADR-014 §三(阶段 2 绑定的可测接缝:白名单/闭集/类型校验 + FixParse 换算出口)
//          · GDD design/gdd/diagnosis-system.md R-8.1/R-8.2(七字段 schema)+ AC-8-32/8-33/8-34
//          · Story 002(本件 = `DiagnosisSignTableValidator` 的**唯一调用点** ——
//            删掉 Bind() 里的 Validate(...) ⇒ 违例夹具静默烘出 ⇒ 测试负例转红)
//
// ⚠️ 枚举一律**按明文字符串**读入并映射(ADR-014 §三 / 21a AC-21a-26 先例「禁 int 编码的 state」):
//    channel / reveal_by / polarity 的中文明文 → 前缀侧 byte 枚举;P1a 值(舌/脉/情志/体质/时序、
//    望/闻/切)**刻意不在映射表内** —— 未命中即红,并按 P1a 名单**叠打 AC-8-33 tag**。
//
// ⚠️ `neg_weight` 的 `FixParse` 换算在**本件**(Editor 侧,不受 D-FIX 约束)完成:
//    JSON 字符串 → Fix → raw `long?` 存行。前缀侧只持 raw(DiagnosisSignTable.cs 头注)。
//
// ⚠️ 全部错误**聚合**后一次抛出(与 InteractionKindBinder 同纪律);校验器仅当
//    绑定本身无错时跑 —— 否则断言的是半成型行集,错误重复且难读。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using DaYiJingCheng.Sim.Contracts;
// ⚠️ CS0104 消歧:`Sim.Contracts.SignChannel` 是 AC-21 的通道**位掩码静态类**
//    (VitalsDto.SignChannelMask 用),与本表 R-8.1 的通道**枚举**同名但不同物 ——
//    本文件的 `SignChannel` 一律指枚举(位掩码投影归 F-8.x / story-003 的映射层)。
using SignChannel = DaYiJingCheng.Gameplay.Presentation.Diagnosis.SignChannel;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>`diagnosis_signs.json` 的阶段 2 绑定器:**唯一**调用
    /// <see cref="DiagnosisSignTableValidator"/> 的地方。</summary>
    internal static class DiagnosisSignBinder
    {
        // ── 已知键白名单(ADR-014 §三:未知键 = 烘焙期硬失败,防拼写错误静默丢字段)──

        private static readonly string[] RootKeys = { "schema_version", "signs" };

        /// <summary>R-8.1 七字段(序 = GDD 表序)。</summary>
        private static readonly string[] RowKeys =
        {
            "sign_id", "display_词", "channel", "reveal_by",
            "tier_named", "polarity", "neg_weight",
        };

        // ── 中文明文 → 枚举映射(闭集;未命中即红)──

        private static readonly Dictionary<string, SignChannel> ChannelMap =
            new Dictionary<string, SignChannel>(StringComparer.Ordinal)
            {
                { "面色", SignChannel.FaceColor },
                { "语声", SignChannel.Voice },
                { "姿态", SignChannel.Posture },
                { "呼吸", SignChannel.Breathing },
                { "触感", SignChannel.Touch },
                { "问诊(非体格通道)", SignChannel.History },
            };

        /// <summary>P1a 通道(AC-8-33 禁入 P0)—— 命中即叠打 AC-8-33·channel tag。</summary>
        private static readonly string[] P1aChannels = { "舌", "脉", "情志", "体质", "时序" };

        private static readonly Dictionary<string, RevealMethod> RevealMap =
            new Dictionary<string, RevealMethod>(StringComparer.Ordinal)
            {
                { "视诊", RevealMethod.Inspection },
                { "触诊", RevealMethod.Palpation },
                { "叩诊", RevealMethod.Percussion },
                { "听诊", RevealMethod.Auscultation },
                { "问诊", RevealMethod.Inquiry },
            };

        /// <summary>P1a 手段(AC-8-33 禁入 P0)—— 命中即叠打 AC-8-33·reveal_by tag。</summary>
        private static readonly string[] P1aReveals = { "望", "闻", "切" };

        private static readonly Dictionary<string, SignPolarity> PolarityMap =
            new Dictionary<string, SignPolarity>(StringComparer.Ordinal)
            {
                { "阳性体征", SignPolarity.Positive },
                { "阴性体征", SignPolarity.Negative },
            };

        /// <summary>绑定结果(行集 + schema 版本,供写方落盘)。</summary>
        public readonly struct BindResult
        {
            /// <summary>校验通过后的词条行集(序 = 源 JSON 序)。</summary>
            public readonly List<SignLexemeRow> Rows;

            /// <summary>源 <c>schema_version</c>(u32 域;随产物头落盘)。</summary>
            public readonly uint SchemaVersion;

            public BindResult(List<SignLexemeRow> rows, uint schemaVersion)
            {
                Rows = rows;
                SchemaVersion = schemaVersion;
            }
        }

        /// <summary>
        /// 绑定 + 校验。任一违例 ⇒ 聚合 <see cref="BakeValidationException"/>。
        /// </summary>
        /// <param name="signsJson">`diagnosis_signs.json` 原文。</param>
        public static BindResult Bind(string signsJson)
        {
            var errors = new List<string>();

            // ── 阶段 1:词法(复用仓库唯一入口,零 JsonConvert / JObject)──
            if (!JsonStage1Lexer.TryParse(signsJson, out JsonNode root, out string lexErr))
            {
                errors.Add($"diagnosis_signs.json 阶段1 词法失败:{lexErr}");
                throw new BakeValidationException(errors);
            }

            uint schemaVersion = 0;
            var rows = new List<SignLexemeRow>();

            if (root.Kind != JsonNodeKind.Object)
            {
                errors.Add($"diagnosis_signs.json 根须为对象(实得 {root.Kind})");
            }
            else
            {
                RejectUnknownKeys(root, RootKeys, "diagnosis_signs.json 根", errors);

                if (!root.TryGet("schema_version", out JsonNode sv))
                    errors.Add("diagnosis_signs.json 缺 schema_version");
                else if (sv.Kind != JsonNodeKind.Integer || sv.Int < 0 || sv.Int > uint.MaxValue)
                    errors.Add($"diagnosis_signs.json schema_version 须为 u32 域整数(实得 {sv.Kind}={sv.RawText})");
                else
                    schemaVersion = (uint)sv.Int;

                if (!root.TryGet("signs", out JsonNode signsArr))
                    errors.Add("diagnosis_signs.json 缺 signs 数组");
                else if (signsArr.Kind != JsonNodeKind.Array)
                    errors.Add($"diagnosis_signs.json signs 须为数组(实得 {signsArr.Kind})");
                else
                    BindRows(signsArr, rows, errors);
            }

            // ── ★ 校验器调用点(story 002 承重面;AC-8-32/33/34 + TR-diag-014 由此进装载路径)──
            //    仅当绑定本身无错时跑 —— 否则断言的是半成型行集。
            if (errors.Count == 0)
            {
                if (rows.Count == 0)
                {
                    // 空词表静默出货 = 以空集冒充绿(本仓反假绿纪律)—— 硬失败。
                    errors.Add("signs 为空表 —— 空词表静默出货违背「拒以空集冒充绿」纪律 " +
                               "[R-8.1·signs-empty]");
                }
                else
                {
                    try
                    {
                        DiagnosisSignTableValidator.Validate(rows);
                    }
                    catch (Exception ex)
                    {
                        errors.Add("词表校验:" + ex.Message);
                    }
                }
            }

            if (errors.Count > 0)
                throw new BakeValidationException(errors);

            return new BindResult(rows, schemaVersion);
        }

        // ── 行集 ────────────────────────────────────────────────────────────

        private static void BindRows(JsonNode arr, List<SignLexemeRow> rows, List<string> errors)
        {
            for (int i = 0; i < arr.Items.Count; i++)
            {
                JsonNode node = arr.Items[i];
                string where = $"signs[{i}]";
                if (node.Kind != JsonNodeKind.Object)
                {
                    errors.Add($"{where} 须为对象(实得 {node.Kind})");
                    continue;
                }
                RejectUnknownKeys(node, RowKeys, where, errors);

                string signId = ReadSignId(node, where, errors);
                string[] words = ReadDisplayWords(node, where, errors);
                SignChannel channel = ReadChannel(node, where, errors);
                RevealMethod[] reveal = ReadRevealBy(node, where, errors);
                int tier = ReadTierNamed(node, where, errors);
                SignPolarity polarity = ReadPolarity(node, where, errors);
                long? negWeight = ReadNegWeight(node, where, errors);

                rows.Add(new SignLexemeRow(signId, words, channel, reveal, tier, polarity, negWeight));
            }
        }

        // ── 读件(严格:类型不符即记错,绝不隐式转换)──

        private static string ReadSignId(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.sign_id";
            if (!obj.TryGet("sign_id", out JsonNode n))
            {
                errors.Add($"{label} 缺失 [AC-8-32·sign_id]");
                return null;
            }
            if (n.Kind != JsonNodeKind.String || string.IsNullOrEmpty(n.Str))
            {
                errors.Add($"{label} 须为非空字符串(实得 {n.Kind}) [AC-8-32·sign_id]");
                return null;
            }
            return n.Str;
        }

        /// <summary>三档呈现词:数组恰 3 槽;元素 = null 或非空字符串(空串禁 —— 与阴性形态同构)。</summary>
        private static string[] ReadDisplayWords(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.display_词";
            if (!obj.TryGet("display_词", out JsonNode n))
            {
                errors.Add($"{label} 缺失 [AC-8-32·display_词]");
                return null;
            }
            if (n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{label} 须为数组(实得 {n.Kind}) [AC-8-32·display_词]");
                return null;
            }
            if (n.Items.Count != 3)
            {
                errors.Add($"{label} 须恰 3 槽(粗/中/细,实得 {n.Items.Count}) [AC-8-32·display_词]");
                return null;
            }
            var words = new string[3];
            for (int s = 0; s < 3; s++)
            {
                JsonNode slot = n.Items[s];
                if (slot.Kind == JsonNodeKind.Null)
                {
                    words[s] = null;   // 空档显式 null(F-8.2 回退可表达;禁空串)
                    continue;
                }
                if (slot.Kind != JsonNodeKind.String)
                {
                    errors.Add($"{label}[{s}] 须为字符串或 null(实得 {slot.Kind}) [AC-8-32·display_词]");
                    return null;
                }
                if (slot.Str.Length == 0)
                {
                    errors.Add($"{label}[{s}] 为空串 —— 空档须用 null(空串与阴性形态在呈现层同构)" +
                               " [AC-8-32·display_词]");
                    return null;
                }
                words[s] = slot.Str;
            }
            return words;
        }

        private static SignChannel ReadChannel(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.channel";
            if (!obj.TryGet("channel", out JsonNode n))
            {
                errors.Add($"{label} 缺失 [AC-8-32·channel]");
                return default;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label} 须为明文字符串枚举(实得 {n.Kind};禁 int 编码) [AC-8-32·channel]");
                return default;
            }
            if (ChannelMap.TryGetValue(n.Str, out SignChannel ch))
                return ch;

            // 未命中闭集:基础 tag + P1a 叠 tag
            string p1a = IsP1a(P1aChannels, n.Str) ? " [AC-8-33·channel]" : "";
            errors.Add($"{label}=\"{n.Str}\" ∉ 五通道∪病史闭集 [AC-8-32·channel]{p1a}");
            return default;
        }

        private static RevealMethod[] ReadRevealBy(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.reveal_by";
            if (!obj.TryGet("reveal_by", out JsonNode n))
            {
                errors.Add($"{label} 缺失 [AC-8-32·reveal_by]");
                return null;
            }
            if (n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{label} 须为数组(实得 {n.Kind}) [AC-8-32·reveal_by]");
                return null;
            }
            if (n.Items.Count == 0)
            {
                errors.Add($"{label} 须非空(至少一法可揭示) [AC-8-32·reveal_by]");
                return null;
            }
            var methods = new RevealMethod[n.Items.Count];
            bool ok = true;
            for (int i = 0; i < n.Items.Count; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.String)
                {
                    errors.Add($"{label}[{i}] 须为明文字符串枚举(实得 {item.Kind}) [AC-8-32·reveal_by]");
                    ok = false;
                    continue;
                }
                if (RevealMap.TryGetValue(item.Str, out RevealMethod m))
                {
                    methods[i] = m;
                    continue;
                }
                string p1a = IsP1a(P1aReveals, item.Str) ? " [AC-8-33·reveal_by]" : "";
                errors.Add($"{label}[{i}]=\"{item.Str}\" ∉ P0 五法闭集 [AC-8-32·reveal_by]{p1a}");
                ok = false;
            }
            return ok ? methods : null;
        }

        private static int ReadTierNamed(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.tier_named";
            if (!obj.TryGet("tier_named", out JsonNode n))
            {
                errors.Add($"{label} 缺失 [AC-8-32·tier_named]");
                return 0;
            }
            if (n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{label} 须为整数(实得 {n.Kind};禁 float/字符串) [AC-8-32·tier_named]");
                return 0;
            }
            if (n.Int < int.MinValue || n.Int > int.MaxValue)
            {
                errors.Add($"{label}={n.Int} 超出 int 域 [AC-8-32·tier_named]");
                return 0;
            }
            return (int)n.Int;   // 值域 {1}∪SLOT_BOUNDS 由校验器判(依赖 DiagnosisTuning)
        }

        private static SignPolarity ReadPolarity(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.polarity";
            if (!obj.TryGet("polarity", out JsonNode n))
            {
                errors.Add($"{label} 缺失 [AC-8-32·polarity]");
                return default;
            }
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label} 须为明文字符串枚举(实得 {n.Kind}) [AC-8-32·polarity]");
                return default;
            }
            if (PolarityMap.TryGetValue(n.Str, out SignPolarity p))
                return p;
            errors.Add($"{label}=\"{n.Str}\" ∉ {{阳性体征, 阴性体征}} [AC-8-32·polarity]");
            return default;
        }

        /// <summary>`neg_weight`:JSON 字符串 Fix 字面量 → FixParse → raw long?(ADR-014 §三:
        /// Fix 字段在 JSON 里写字符串;非字符串 token / 解析失败 = <c>ADR-014·fix-string</c>)。
        /// 缺失 / 显式 null ⇒ null(非空性由校验器按极性判)。</summary>
        private static long? ReadNegWeight(JsonNode obj, string where, List<string> errors)
        {
            string label = $"{where}.neg_weight";
            if (!obj.TryGet("neg_weight", out JsonNode n))
                return null;
            if (n.Kind == JsonNodeKind.Null)
                return null;
            if (n.Kind != JsonNodeKind.String)
            {
                errors.Add($"{label} 须为字符串 Fix 字面量(实得 {n.Kind}={n.RawText})" +
                           " [ADR-014·fix-string]");
                return null;
            }
            try
            {
                return FixParse.Parse(n.Str).Raw;
            }
            catch (Exception ex)
            {
                // catch 整型 Exception(非仅 FormatException):`"1/0"` 抛 DivideByZeroException
                // (FixParse.FromRatio)、超域等抛 FormatException —— 任一都须聚合成
                // ADR-014·fix-string(结构侧评审 S-M1:FormatException 单抓会漏 /0 逃逸聚合)。
                errors.Add($"{label}=\"{n.Str}\" 解析失败:{ex.Message} [ADR-014·fix-string]");
                return null;
            }
        }

        // ── 公共小件 ────────────────────────────────────────────────────────

        private static bool IsP1a(string[] p1aSet, string value)
        {
            foreach (string s in p1aSet)
                if (string.Equals(s, value, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>未知键 = 硬失败(ADR-014 §三,防拼写错误静默丢字段)。</summary>
        private static void RejectUnknownKeys(JsonNode obj, string[] allowed, string where, List<string> errors)
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
