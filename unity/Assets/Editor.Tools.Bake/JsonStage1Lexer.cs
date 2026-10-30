// 权威来源:ADR-014 §三(阶段1 = JsonTextReader 仅词法;禁 JsonConvert / JObject / JToken)
//          · Story 008(自研 DOM:token 种类按结构记录,Integer 与 String 永不混淆)
//
// ⚠️ DateParseHandling = None:日期串不得被读成 DateTime(作者态只有字符串语义)。
// ⚠️ FloatParseHandling = Decimal —— **✅ 2026-10-30 已与 ADR-014 对齐(`TD-clinic-17` 结案)**。
//    此处原写「**ADVISORY 偏差** —— ADR-014 原文钉 None,但 Newtonsoft 只有 { Decimal, Double }」,
//    即把「代码偏离 ADR」当成需要登记的偏差。**实测查明错的是 ADR,不是代码**:
//    Newtonsoft 的 `FloatParseHandling` 枚举**确实只有 `Double` / `Decimal` 两成员**(权威出处 =
//    该包的 `Newtonsoft.Json.xml`,`grep "F:Newtonsoft.Json.FloatParseHandling"` 恰两行),
//    ADR-014 的 `None` **字面不可实现** ⇒ 已由该 ADR 就地订正为 `Decimal`(两处)。
//    ⇒ 本文件与 ADR **现已一致**,不再有偏差可登记。
//    float 路径的**行为拒收**仍由阶段 2 承担
//    (`Fix` 字段必须 String token、int 字段必须 Integer token —— 任何 Float token 结构性硬失败)。
// ⚠️ Integer vs Float 按 token 种类结构化区分,AC-26(int 编码 processing_state)不依赖数值转换。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>阶段1 产物节点(token 种类结构化记录)。</summary>
    internal sealed class JsonNode
    {
        /// <summary>token 种类(Object/Array/String/Integer/Float/Boolean/Null)。</summary>
        public JsonNodeKind Kind;

        /// <summary>对象属性(插入序保序 —— 确定性错误顺序,不用 Dictionary)。</summary>
        public List<KeyValuePair<string, JsonNode>> Props;

        /// <summary>数组元素(保序)。</summary>
        public List<JsonNode> Items;

        /// <summary>字符串值(Kind = String)。</summary>
        public string Str;

        /// <summary>整数值(Kind = Integer)。</summary>
        public long Int;

        /// <summary>布尔值(Kind = Boolean)。</summary>
        public bool Bool;

        /// <summary>标量原文(Integer 十进制 / Float 十六进制不变的 invariant 形 / String 值 / true|false)。
        /// 供 raw-string 门(ValidateStackWeight / ValidateMaxQuality)直读。</summary>
        public string RawText;

        /// <summary>键集(保序);非对象返回空。</summary>
        public IEnumerable<string> Keys
        {
            get
            {
                if (Props == null) yield break;
                foreach (KeyValuePair<string, JsonNode> p in Props) yield return p.Key;
            }
        }

        /// <summary>取属性;缺失返回 false。</summary>
        public bool TryGet(string key, out JsonNode node)
        {
            if (Props != null)
            {
                for (int i = 0; i < Props.Count; i++)
                {
                    if (string.Equals(Props[i].Key, key, StringComparison.Ordinal))
                    {
                        node = Props[i].Value;
                        return true;
                    }
                }
            }
            node = null;
            return false;
        }
    }

    /// <summary>阶段1 token 种类(结构化,不依赖 Newtonsoft 转换结果)。</summary>
    internal enum JsonNodeKind
    {
        /// <summary>JSON 对象。</summary>
        Object,

        /// <summary>JSON 数组。</summary>
        Array,

        /// <summary>JSON 字符串。</summary>
        String,

        /// <summary>JSON 整数字面量(无小数点/指数)。</summary>
        Integer,

        /// <summary>JSON 浮点字面量(含小数点或指数)。</summary>
        Float,

        /// <summary>JSON 布尔。</summary>
        Boolean,

        /// <summary>JSON null。</summary>
        Null,
    }

    /// <summary>阶段1 词法器:JsonTextReader 读 token 流 → 自研 DOM。**不解析语义**,
    /// 不做数值域转换,不做日期解析。</summary>
    /// <example>
    /// <code>
    /// if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string error))
    ///     errors.Add($"阶段1 词法失败:{error}");
    /// </code>
    /// </example>
    internal static class JsonStage1Lexer
    {
        /// <summary>解析一段 JSON 文本为 DOM。</summary>
        /// <param name="json">源文本(可为 null/空 ⇒ 失败)。</param>
        /// <param name="root">成功时为根节点。</param>
        /// <param name="error">失败时为可读错误(含行/列)。</param>
        /// <returns>true = 词法成功(不代表语义合法)。</returns>
        public static bool TryParse(string json, out JsonNode root, out string error)
        {
            root = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "输入为空";
                return false;
            }

            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    // ✅ 2026-10-30 起与 ADR-014 一致(`TD-clinic-17` 结案):ADR 已订正为 Decimal。
                    // Decimal = 「不走 double」的最接近取值(28-29 位有效数字 vs double 的 15-17);
                    // float 路径的行为拒收在阶段 2(见文件头)。
                    reader.FloatParseHandling = FloatParseHandling.Decimal;
                    reader.CloseInput = false;

                    if (!reader.Read())
                    {
                        error = "输入无任何 JSON token";
                        return false;
                    }

                    SkipComments(reader);
                    root = ReadValue(reader);

                    // 尾随内容检查(注释除外)。
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.Comment)
                        {
                            continue;
                        }
                        error = $"根节点后存在尾随内容(token={reader.TokenType} @ 行{reader.LineNumber} 列{reader.LinePosition})";
                        return false;
                    }

                    return true;
                }
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>读当前位置的值(调用前 reader 已停在值的起始 token)。</summary>
        private static JsonNode ReadValue(JsonTextReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonToken.StartObject:
                {
                    var node = new JsonNode { Kind = JsonNodeKind.Object, Props = new List<KeyValuePair<string, JsonNode>>() };
                    bool closed = false;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.Comment) continue;
                        if (reader.TokenType == JsonToken.EndObject) { closed = true; break; }
                        if (reader.TokenType != JsonToken.PropertyName)
                            throw new JsonReaderException($"对象内期待属性名,实得 {reader.TokenType}(@ 行{reader.LineNumber})");

                        string key = (string)reader.Value;
                        int keyLine = reader.LineNumber;

                        // ⚠️ 重复键拒收(TD-medcons-10 · 2026-10-29):`Props` 是 List,**同键可
                        //    出现两次**;下游各 binder 有的线性取首、有的建 Dictionary 取后
                        //    ⇒ 「哪一份生效」取决于**读者实现**而非 JSON 文本 —— 静默丢字段,
                        //    且改一个 binder 的取法就能让同一份数据换含义。JSON 本身要求
                        //    名字唯一(RFC 8259 §4),重复即作者态错误,须在**词法层**当场红
                        //    (与上方「对象未闭合」同理:报错要落在坏掉的地方)。
                        for (int i = 0; i < node.Props.Count; i++)
                        {
                            if (node.Props[i].Key == key)
                            {
                                throw new JsonReaderException(
                                    $"对象内属性 \"{key}\" 重复(首次出现于本对象内,重复 @ 行{keyLine})" +
                                    "—— 同名键会让「取首 / 取后」两种 binder 得不同值,是静默丢字段的入口");
                            }
                        }

                        if (!reader.Read())
                            throw new JsonReaderException($"属性 \"{key}\" 后无值(@ 行{reader.LineNumber})");
                        SkipComments(reader);
                        JsonNode child = ReadValue(reader);
                        node.Props.Add(new KeyValuePair<string, JsonNode>(key, child));
                    }
                    // ⚠️ 循环退出有**两条**路径:EndObject(合法)与 Read() 返回 false(输入耗尽)。
                    //    不区分二者 ⇒ 截断的 JSON 被当成完整对象收下,词法层静默放行
                    //    (2026-10-22 实测:`{ "a": 1, ` 通过阶段1,错误一路飘到绑定层报「缺字段」,
                    //    报错指向的地方不是坏掉的地方)。
                    if (!closed)
                        throw new JsonReaderException("对象未闭合 —— 输入在 EndObject 前终止(@ 行" + reader.LineNumber + ")");
                    return node;
                }

                case JsonToken.StartArray:
                {
                    var node = new JsonNode { Kind = JsonNodeKind.Array, Items = new List<JsonNode>() };
                    bool closed = false;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.Comment) continue;
                        if (reader.TokenType == JsonToken.EndArray) { closed = true; break; }
                        SkipCommentsBacktrack(reader);
                        node.Items.Add(ReadValue(reader));
                    }
                    if (!closed)
                        throw new JsonReaderException("数组未闭合 —— 输入在 EndArray 前终止(@ 行" + reader.LineNumber + ")");
                    return node;
                }

                case JsonToken.String:
                    return new JsonNode
                    {
                        Kind = JsonNodeKind.String,
                        Str = (string)reader.Value,
                        RawText = (string)reader.Value,
                    };

                case JsonToken.Integer:
                {
                    // 正常范围为 long;超长整数 Newtonsoft 会给出 BigInteger(经 ToString 显形)。
                    string digits = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                    if (!long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
                        throw new JsonReaderException($"整数超出 int64 域:\"{digits}\"(@ 行{reader.LineNumber})");
                    return new JsonNode
                    {
                        Kind = JsonNodeKind.Integer,
                        Int = value,
                        RawText = digits,
                    };
                }

                case JsonToken.Float:
                    // FloatParseHandling.Decimal ⇒ Value 是 decimal;原始字面量的浮点形以 invariant 还原。
                    return new JsonNode
                    {
                        Kind = JsonNodeKind.Float,
                        RawText = ((decimal)reader.Value).ToString(CultureInfo.InvariantCulture),
                    };

                case JsonToken.Boolean:
                    return new JsonNode
                    {
                        Kind = JsonNodeKind.Boolean,
                        Bool = (bool)reader.Value,
                        RawText = (bool)reader.Value ? "true" : "false",
                    };

                case JsonToken.Null:
                    return new JsonNode { Kind = JsonNodeKind.Null, RawText = "null" };

                default:
                    throw new JsonReaderException($"意外 token {reader.TokenType}(@ 行{reader.LineNumber})");
            }
        }

        /// <summary>跳过注释 token(当前位)。</summary>
        private static void SkipComments(JsonTextReader reader)
        {
            while (reader.TokenType == JsonToken.Comment)
            {
                if (!reader.Read()) throw new JsonReaderException("输入在注释后终止");
            }
        }

        /// <summary>数组元素前的注释:若当前已是值起始 token 则不动;若还没读值则推进。
        /// StartArray 循环里 Read() 已把我们带到元素或 EndArray —— 元素位偶发 Comment,
        /// 此处把 Comment 消化掉;若消化后是 EndArray 则回不去,由调用方判 EndArray 失败……
        /// 故只在 Comment 时继续读,直到非 Comment;调用方在进入本方法前已保证非 EndArray。</summary>
        private static void SkipCommentsBacktrack(JsonTextReader reader)
        {
            while (reader.TokenType == JsonToken.Comment)
            {
                if (!reader.Read()) throw new JsonReaderException("数组在注释后终止");
            }
        }
    }
}
