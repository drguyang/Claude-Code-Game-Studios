// 39 脉案的**反幻想三条「绝不」**能力面守卫(AC-39-05 / AC-39-06a / AC-39-08)。
//
// 权威来源:
//   design/gdd/casebook.md 规则四(三条绝不)· **AC-39-05**(无按病种分组/排序/检索/计数的能力)
//     · **AC-39-06a**(本地化键闭集 + USS 文本黑名单)· **AC-39-08**(零归纳 / 零聚合呈现)
//     · AC-39-09(病名栏空白零催促 —— `[L]`,本门不代签)
//   docs/architecture/adr-013-skeuomorphic-ui-framework.md §9 C3(42 只渲染,永不持有游戏状态)
//   docs/architecture/adr-009-world-state-event-boundary.md(`disease_id` 保密语义 = 防御纵深)
//   production/epics/casebook/story-004-anti-fantasy-capability-guards.md
//
// ── 为什么是「能力面」而不是「呈现面」────────────────────────────────────
// 「就算有人偷加,UI 也不显示」**不是达标**。归纳是**玩家的动作**,不是界面的功能(AC-39-08)。
// 只要 39 的公开面上还有一个「按病种数一数」的方法,这条规则就退化成口头承诺。
// ⇒ 判据落在**公开 API 面**,不是落在「界面上有没有画红点」。
//
// ── 判据(两条)────────────────────────────────────────────────────────
//   R1 **病种维度零出现**:39 的公开类型 / 成员 / 参数 / 返回值里不得出现
//      `diseas` / `diagnos` / `symptom`。**这一条把 AC-39-05 整个包住** ——
//      拿不到病种,就无从「按病种分组 / 排序 / 检索 / 计数」。
//   R2 **聚合 / 催促词**:公开标识符不得出现
//      `unopened` / `undone` / `pending` / `remind` / `alert` / `badge` / `notify` /
//      `similar` / `samesource`(「未结 N」「提醒」「同源」的实现形态)。
//
// ⚠️ **词表刻意不含 `sort` / `group` / `count`** —— 它们在本模块有**合法**用法:
//    `CaseSortKey`(按 patient/tick/seq 排序,不按病种)· `CaseGroup`(同一**病人**的串接组,
//    GDD 规则三明文允许)· `CaseSheet.Count`(册内例数,不是「未结 N」)。
//    把这些词一律拉黑会造出一条**永远红**的门 —— 那不是严谨,是没人会去修的噪声。
//
// ⚠️ 本门只扫 **39 的公开面**。42 的元件库、8 的词表不在此面内(各归各的 AC)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>AC-39-05 / AC-39-06a / AC-39-08 的执行体。</summary>
    public static class CasebookAntiFantasyGate
    {
        /// <summary>39 的命名空间(与 `CaseListOrdering` 同一出处)。</summary>
        public const string CasebookNamespace = "DaYiJingCheng.Gameplay.UI.Casebook";

        /// <summary>R1:病种维度词面(归一化后**子串**判)。任一命中即违例。</summary>
        public static readonly string[] DiseaseTokens = { "diseas", "diagnos", "symptom" };

        /// <summary>R2:聚合 / 催促 / 同源词面(归一化后子串判)。</summary>
        public static readonly string[] AggregationTokens =
        {
            "unopened", "undone", "pending", "remind", "alert", "badge", "notify",
            "similar", "samesource", "sameorigin",
        };

        /// <summary>文本面黑名单(中文词族)—— AC-39-06a 的「同源 / 相似 / 未结 N / 提醒」。</summary>
        public static readonly string[] TextBlacklist =
        {
            "同源", "相似", "同症", "未结", "提醒", "催促", "待办", "红点", "徽章",
        };

        /// <summary>
        /// **按键提示**词族(规则八 / `AC-8-44` 的 39 侧执行体,2026-11-08 · S26-4)。
        /// <para>规则八要的是「零按键提示浮层」:纸上**不许出现**「按 E 查看」「点击落笔」这类字。
        /// 此前全仓只有两条半判据 —— 元件**名**里的 `hint` / `tooltip` token(PlayMode 树遍历),
        /// 和 AC-39-06a 的反幻想词表;一段写着「按 E 开册」的 Label 文字**两处都抓不到**。</para>
        /// <para>⚠️ 词表刻意短:只收**明确指向输入**的词。`按` 单字会误伤「按时」这类正常中文,
        /// 但 39 的文案面(闭集 + UXML)今天只有 13 个词,且闭集由本门把着 —— 真撞上时
        /// 处置是**改文案**或**把该词从表里删掉并说明**,不是把判据放宽。</para>
        /// </summary>
        public static readonly string[] HintTokens =
        {
            "按 E", "按E", "按键", "按 ", "点击", "按下", "键位", "手柄按",
            "press ", "Press ", "click to", "key hint", "button prompt",
        };

        /// <summary>焦点环的声明序铁律(GDD 规则七 2026-10-06 订正面;前七项固定,其后折边 / 页缘)。</summary>
        public static readonly string[] FocusIronLaw =
        {
            "channel-complexion", "channel-voice", "channel-posture", "channel-breathing",
            "channel-palpation", "channel-inquiry", "channel-disease-name",
        };

        /// <summary>39 独占的 UXML(AC-39-06a 的资产面)。</summary>
        public const string CasebookUxmlRelativePath =
            "unity/Assets/Gameplay.UI/Skeuomorphic/Screens/Casebook39.uxml";

        /// <summary>39 的本地化文案键闭集(AC-39-06a 的作者态数据,ADR-014 管线)。</summary>
        public const string CasebookTextRelativePath = "assets/data/casebook_text.json";

        /// <summary>键名形:`cb` 前缀 + ASCII 驼峰。闭集里不许出现别的形状。</summary>
        public const string KeyPattern = @"^cb[A-Z][A-Za-z0-9]*$";

        /// <summary>构建期入口:扫 39 命名空间的公开面 + 作者态文案闭集与 UXML。</summary>
        /// <param name="root">Unity 工程目录(与 `AssemblyGates` 其余门的 `RunAll(".")` 同口径,
        /// 其父目录即仓库根)。</param>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll(string root = ".")
        {
            var errs = new List<string>();
            errs.AddRange(ScanAssemblySurface());
            errs.AddRange(ScanAuthoringSurface(root));
            return errs;
        }

        private static List<string> ScanAssemblySurface()
        {
            Assembly asm;
            try
            {
                asm = Assembly.Load("Gameplay.UI");
            }
            catch (Exception ex)
            {
                // 装配缺失 / 改名 ⇒ 判据无法成立。**不静默跳过**(那会让本门悄然消失)。
                return new List<string>
                {
                    $"[AC-39-05] 找不到装配「Gameplay.UI」—— 反幻想守卫无法成立:" +
                    $"{ex.GetType().Name}: {ex.Message}",
                };
            }

            Type[] types = asm.GetTypes()
                .Where(t => t.Namespace == CasebookNamespace)
                .ToArray();

            return ScanPublicSurface(types, "39");
        }

        /// <summary>
        /// 作者态面:**文案键闭集** + **UXML 文本**。
        /// <para>2026-11-08(S26-3 续):此前只有 EditMode 测试读这两个资产,**构建期门不读**
        /// ⇒ AC-39-06a 的资产半边在真构建里没有执行体。本方法补上构建期强制点。</para>
        /// </summary>
        public static List<string> ScanAuthoringSurface(string root = ".")
        {
            var errs = new List<string>();
            string projectDir = Path.GetFullPath(root ?? ".");
            string repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));

            // ── ① 键闭集 ──
            string textPath = Path.Combine(repoRoot, CasebookTextRelativePath.Replace('/', Path.DirectorySeparatorChar));
            List<(string key, string text)> entries;
            if (!File.Exists(textPath))
            {
                errs.Add($"[AC-39-06a] 找不到文案键闭集「{textPath}」—— 闭集判据没有宾语(非「通过」)");
                entries = new List<(string, string)>();
            }
            else
            {
                entries = ParseTextKeys(File.ReadAllText(textPath), errs);
            }
            errs.AddRange(ScanTextKeySet(entries, "casebook_text.json"));

            // ── ② UXML 文本(黑名单 + **必须 ∈ 闭集值集**)──
            string uxmlPath = Path.Combine(repoRoot, CasebookUxmlRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(uxmlPath))
            {
                errs.Add($"[AC-39-06a] 找不到 39 的 UXML「{uxmlPath}」—— 文本面判据无法成立(非「通过」)");
                return errs;
            }

            List<(string where, string text)> texts = ExtractUxmlTexts(File.ReadAllText(uxmlPath));
            string uxmlText = File.ReadAllText(uxmlPath);
            errs.AddRange(ScanTexts(texts, "Casebook39.uxml"));
            errs.AddRange(ScanHintTexts(texts, "Casebook39.uxml"));                       // 规则八(AC-8-44)
            errs.AddRange(ScanHintTexts(entries.Select(e => (e.key, e.text)), "casebook_text.json"));
            errs.AddRange(ScanHintAttributes(uxmlText, "Casebook39.uxml"));               // tooltip / alt 旁路
            errs.AddRange(ScanFocusDeclarationOrder(uxmlText, "Casebook39.uxml"));        // 规则七铁律序
            errs.AddRange(ScanUxmlAgainstClosedSet(
                texts, entries.Select(e => e.text).ToList(), "Casebook39.uxml"));
            return errs;
        }

        /// <summary>
        /// 解析闭集 JSON(ADR-014 §三:Newtonsoft **仅作词法器**,绑定层自写)。
        /// 解析失败也进 <paramref name="errs"/> —— 坏的闭集文件不得静默退化成空集。
        /// </summary>
        public static List<(string key, string text)> ParseTextKeys(string json, List<string> errs)
        {
            var list = new List<(string, string)>();
            if (errs == null) throw new ArgumentNullException(nameof(errs));

            Newtonsoft.Json.Linq.JToken root;
            try
            {
                root = Newtonsoft.Json.Linq.JToken.Parse(json);
            }
            catch (Exception ex)
            {
                errs.Add($"[AC-39-06a] casebook_text.json **解析失败** —— " +
                         $"{ex.GetType().Name}: {ex.Message}");
                return list;
            }

            if (!(root["keys"] is Newtonsoft.Json.Linq.JArray keys))
            {
                errs.Add("[AC-39-06a] casebook_text.json 缺 `keys` 数组 —— 闭集不可判(非「空集即通过」)");
                return list;
            }

            foreach (Newtonsoft.Json.Linq.JToken t in keys)
            {
                string k = (string)t["key"];
                string v = (string)t["text"];
                if (k == null || v == null)
                {
                    errs.Add($"[AC-39-06a] casebook_text.json 有条目缺 `key` / `text`:{t.ToString(Newtonsoft.Json.Formatting.None)}");
                    continue;
                }
                list.Add((k, v));
            }
            return list;
        }

        /// <summary>闭集自身的判据:空集 / 键形 / 重复 / 空文案 / 黑名单与病种词族。</summary>
        public static List<string> ScanTextKeySet(
            IEnumerable<(string key, string text)> entries, string label)
        {
            var errs = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var pattern = new System.Text.RegularExpressions.Regex(KeyPattern);
            int scanned = 0;

            foreach ((string key, string text) in entries ?? Enumerable.Empty<(string, string)>())
            {
                scanned++;
                if (!pattern.IsMatch(key))
                    errs.Add($"[AC-39-06a] {label} 的键「{key}」形状不合法(须 `cb` + 驼峰 —— " +
                             "闭集是**可枚举的有限集**,键名形状是它可被机械比对的前提)");

                if (!seen.Add(key))
                    errs.Add($"[AC-39-06a] {label} 的键「{key}」**重复** —— 重复键 = 两条真源");

                if (string.IsNullOrWhiteSpace(text))
                {
                    errs.Add($"[AC-39-06a] {label} 的键「{key}」文案为空 —— 空文案在屏上是「没做好」,不是「不给提示」");
                    continue;
                }

                foreach (string bad in TextBlacklist)
                    if (text.IndexOf(bad, StringComparison.Ordinal) >= 0)
                        errs.Add($"[AC-39-06a] {label} 的键「{key}」文案含禁用词「{bad}」:「{text}」");

                CheckName(key, $"{label} 的键「{key}」", errs);
                CheckName(text, $"{label} 的键「{key}」文案", errs);
            }

            if (scanned == 0)
                errs.Add($"[AC-39-06a] {label} 的键闭集为 **0 条** —— 判据不可执行(非「通过」)。" +
                         "空闭集会让下面「UXML 文案 ∈ 闭集」恒假,也会让门看起来全绿。");

            return errs;
        }

        /// <summary>
        /// UXML 的每个非空 <c>text=</c> 都必须 ∈ 闭集的值集。
        /// <para>这是「闭集」这件事真正有牙齿的地方:没有它,闭集只是一份自说自话的清单 ——
        /// 在 UXML 里直接写一段新字面量照样绕过。AC-39-06a 的 2026-09-19 注
        /// (「字符串扫描测不到 UXML 元素 ⇒ 本地化 key 闭集 + USS `text=` 黑名单」)问的就是这一条。</para>
        /// </summary>
        public static List<string> ScanUxmlAgainstClosedSet(
            IEnumerable<(string where, string text)> texts,
            ICollection<string> allowedTexts, string label)
        {
            var errs = new List<string>();
            var allowed = new HashSet<string>(allowedTexts ?? new List<string>(), StringComparer.Ordinal);
            int scanned = 0;

            foreach ((string where, string text) in texts ?? Enumerable.Empty<(string, string)>())
            {
                scanned++;
                if (string.IsNullOrEmpty(text)) continue;   // 空 text 是「这里还不该有字」,合法
                if (allowed.Contains(text)) continue;
                errs.Add($"[AC-39-06a] {label} 的「{where}」写了「{text}」,但该串**不在键闭集里** —— " +
                         "自由新增的字面量 = 绕开闭集;要显示它,先给它一个键");
            }

            if (scanned == 0)
                errs.Add($"[AC-39-06a] {label} 的文本扫描面为 **0 条** —— 判据不可执行(非「通过」)。");

            return errs;
        }

        /// <summary>
        /// 公开面扫描(纯函数,给定类型集 —— 测试用它喂合成类型做负夹具)。
        /// </summary>
        public static List<string> ScanPublicSurface(IEnumerable<Type> types, string label)
        {
            var errs = new List<string>();
            var scanned = 0;

            foreach (Type t in (types ?? Enumerable.Empty<Type>()).Where(t => t != null))
            {
                scanned++;
                CheckName(t.Name, $"{label} 的公开类型「{t.Name}」", errs);

                foreach (Type arg in GenericArgs(t))
                    CheckName(arg.Name, $"{label} 的类型「{t.Name}」的泛型实参", errs);

                foreach (MemberInfo m in PublicDeclaredMembers(t))
                {
                    CheckName(m.Name, $"{label} 的公开成员「{t.Name}.{m.Name}」", errs);

                    if (m is MethodInfo mi)
                    {
                        CheckName(mi.ReturnType?.Name, $"{label} 的「{t.Name}.{mi.Name}」返回类型", errs);
                        foreach (ParameterInfo p in mi.GetParameters())
                        {
                            CheckName(p.Name, $"{label} 的「{t.Name}.{mi.Name}」参数名", errs);
                            CheckName(p.ParameterType?.Name, $"{label} 的「{t.Name}.{mi.Name}」参数类型", errs);
                        }
                    }
                    else if (m is PropertyInfo pi)
                    {
                        CheckName(pi.PropertyType?.Name, $"{label} 的「{t.Name}.{pi.Name}」属性类型", errs);
                    }
                    else if (m is FieldInfo fi)
                    {
                        CheckName(fi.FieldType?.Name, $"{label} 的「{t.Name}.{fi.Name}」字段类型", errs);
                    }
                }
            }

            if (scanned == 0)
                errs.Add($"[AC-39-05] {label} 的扫描面为 **0 个类型** —— 判据不可执行(非「通过」)。" +
                         "装配载入失败 / 命名空间搬家都会走到这里。");

            return errs;
        }

        /// <summary>
        /// 文本面扫描(AC-39-06a):给定若干段用户可见文本,命中黑名单词族即违例。
        /// </summary>
        public static List<string> ScanTexts(IEnumerable<(string where, string text)> texts, string label)
            => ScanTextsWithTokenTable(texts, label, TextBlacklist, "[AC-39-06a]", "反幻想禁用词");

        /// <summary>
        /// **按键提示**扫描(规则八 / `AC-8-44` 的 39 侧):同一条循环,换词表。
        /// <para>⚠️ 与 `ScanTexts` **共用一个执行体** —— 两张表若各写一份循环,迟早一边改了另一边没改。</para>
        /// </summary>
        public static List<string> ScanHintTexts(IEnumerable<(string where, string text)> texts, string label)
            => ScanTextsWithTokenTable(texts, label, HintTokens, "[AC-8-44]", "按键提示词");

        /// <summary>
        /// 提示的**旁路**:UXML 的 <c>tooltip=</c> / <c>alt=</c> 属性。
        /// <para>规则八禁的是「浮层 / 提示条」,而 tooltip 正好是**不写进 text、却在悬停时冒出来**的那一种 ——
        /// 现有扫描(元件名 token + 文本词表)对它完全失明。此处一律拒收:**39 的纸上不挂 tooltip / alt**。</para>
        /// </summary>
        public static List<string> ScanHintAttributes(string uxml, string label)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(uxml))
            {
                errs.Add($"[AC-8-44] {label} 的 UXML 为空 —— 属性判据不可执行(非「通过」)。");
                return errs;
            }

            foreach (string attr in new[] { "tooltip=", "alt=" })
            {
                int at = 0;
                while (true)
                {
                    int i = uxml.IndexOf(attr, at, StringComparison.Ordinal);
                    if (i < 0) break;
                    int line = 1 + uxml.Take(i).Count(ch => ch == '\n');
                    errs.Add($"[AC-8-44] {label} 第 {line} 行出现 `{attr}` 属性 —— " +
                             "提示不得走侧面通道(规则八禁的是「冒出来的提示」,不是「只禁可见标签」)");
                    at = i + attr.Length;
                }
            }
            return errs;
        }

        /// <summary>
        /// 焦点环的**声明序**(规则七铁律的物理保证)。
        /// <para>判据 = UXML 里带 <c>focusable="true"</c> 的元素,**按文档出现顺序**的前七项恰为铁律序。
        /// 声明序即 UI Toolkit 的默认焦点序(未显式设 `tabIndex` 时)——所以这一条能自动化,
        /// 不必等手柄走查;走查证的是「运行时真的按这个序走」,两件事不同。</para>
        /// <para>其后的 折边 / 前案 / 后案 **不在铁律面内**(GDD 只钉前七项),但必须仍在环上。</para>
        /// </summary>
        public static List<string> ScanFocusDeclarationOrder(string uxml, string label)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(uxml))
            {
                errs.Add($"[AC-8-23] {label} 的 UXML 为空 —— 焦点序判据不可执行(非「通过」)。");
                return errs;
            }

            var order = new List<(string name, int line)>();
            int at = 0;
            while (true)
            {
                int i = uxml.IndexOf('<', at);
                if (i < 0) break;
                int end = uxml.IndexOf('>', i);
                if (end < 0) break;
                string tag = uxml.Substring(i, end - i + 1);
                at = end + 1;

                if (tag.IndexOf("focusable=\"true\"", StringComparison.Ordinal) < 0) continue;
                int ni = tag.IndexOf("name=\"", StringComparison.Ordinal);
                if (ni < 0) continue;
                int ns = ni + "name=\"".Length;
                int ne = tag.IndexOf('"', ns);
                if (ne < 0) continue;
                order.Add((tag.Substring(ns, ne - ns), 1 + uxml.Take(i).Count(ch => ch == '\n')));
            }

            if (order.Count < FocusIronLaw.Length)
            {
                errs.Add($"[AC-8-23] {label} 的可聚焦元素只有 {order.Count} 个,少于铁律的 " +
                         $"{FocusIronLaw.Length} 项 —— 焦点环缺员(非「通过」)");
                return errs;
            }

            for (int i = 0; i < FocusIronLaw.Length; i++)
            {
                if (order[i].name == FocusIronLaw[i]) continue;
                errs.Add($"[AC-8-23] {label} 第 {order[i].line} 行的焦点位 {i + 1} 是「{order[i].name}」," +
                         $"铁律要求「{FocusIronLaw[i]}」—— 焦点顺序**永不重排**(规则七;重排会让肌肉记忆失效)");
            }
            return errs;
        }

        private static List<string> ScanTextsWithTokenTable(
            IEnumerable<(string where, string text)> texts, string label,
            string[] tokens, string tag, string kindLabel)
        {
            var errs = new List<string>();
            var scanned = 0;

            foreach ((string where, string text) in texts ?? Enumerable.Empty<(string, string)>())
            {
                scanned++;
                if (string.IsNullOrEmpty(text)) continue;
                foreach (string bad in tokens)
                {
                    if (text.IndexOf(bad, StringComparison.Ordinal) >= 0)
                        errs.Add($"{tag} {label} 的「{where}」出现{kindLabel}「{bad}」:「{text.Trim()}」");
                }
            }

            if (scanned == 0)
                errs.Add($"{tag} {label} 的文本扫描面为 **0 条** —— 判据不可执行(非「通过」)。");

            return errs;
        }

        /// <summary>从 UXML 里抽取 `text="…"` 的字面量(资产面文本源)。</summary>
        public static List<(string where, string text)> ExtractUxmlTexts(string uxml)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(uxml)) return result;

            int at = 0;
            while (true)
            {
                int i = uxml.IndexOf("text=\"", at, StringComparison.Ordinal);
                if (i < 0) break;
                int start = i + "text=\"".Length;
                int end = uxml.IndexOf('"', start);
                if (end < 0) break;
                result.Add(($"text#{result.Count}", uxml.Substring(start, end - start)));
                at = end + 1;
            }
            return result;
        }

        /// <summary>扫 39 的 UXML 资产(目录不存在 ⇒ 报错,不静默)。</summary>
        public static List<string> ScanUxmlFile(string path, string label)
        {
            if (!File.Exists(path))
            {
                return new List<string>
                {
                    $"[AC-39-06a] 找不到 39 的 UXML「{path}」—— 文本面判据无法成立(非「通过」)",
                };
            }
            return ScanTexts(ExtractUxmlTexts(File.ReadAllText(path)), label);
        }

        // ── 内部 ──

        private static void CheckName(string identifier, string where, List<string> errs)
        {
            if (string.IsNullOrEmpty(identifier)) return;
            string lower = identifier.ToLowerInvariant();

            foreach (string token in DiseaseTokens)
            {
                if (lower.Contains(token))
                {
                    errs.Add($"[AC-39-05] {where} 命中**病种维度**词「{token}」—— " +
                             "39 拿不到病种,才谈得上「绝不按病种分组 / 检索 / 计数」");
                    return;
                }
            }

            foreach (string token in AggregationTokens)
            {
                if (lower.Contains(token))
                {
                    errs.Add($"[AC-39-08] {where} 命中**聚合 / 催促**词「{token}」—— " +
                             "归纳是玩家的动作,不是界面的功能");
                    return;
                }
            }
        }

        private static IEnumerable<Type> GenericArgs(Type t)
        {
            if (t == null || !t.IsGenericType) yield break;
            foreach (Type a in t.GetGenericArguments()) yield return a;
        }

        private static IEnumerable<MemberInfo> PublicDeclaredMembers(Type t)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (MemberInfo m in t.GetMethods(Flags))
            {
                if (m is MethodInfo mi && (mi.IsSpecialName || mi.IsPrivate)) continue;
                yield return m;
            }
            foreach (PropertyInfo p in t.GetProperties(Flags)) yield return p;
            foreach (FieldInfo f in t.GetFields(Flags)) yield return f;
            foreach (EventInfo e in t.GetEvents(Flags)) yield return e;
        }
    }
}
