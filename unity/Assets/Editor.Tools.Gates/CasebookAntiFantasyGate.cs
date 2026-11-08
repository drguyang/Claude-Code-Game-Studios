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

        /// <summary>39 独占的 UXML(AC-39-06a 的资产面)。</summary>
        public const string CasebookUxmlRelativePath =
            "unity/Assets/Gameplay.UI/Skeuomorphic/Screens/Casebook39.uxml";

        /// <summary>构建期入口:按名载入 `Gameplay.UI` 并扫 39 命名空间的公开面。</summary>
        /// <returns>违例串列表(空 = 全过)。</returns>
        public static List<string> RunAll()
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
        {
            var errs = new List<string>();
            var scanned = 0;

            foreach ((string where, string text) in texts ?? Enumerable.Empty<(string, string)>())
            {
                scanned++;
                if (string.IsNullOrEmpty(text)) continue;
                foreach (string bad in TextBlacklist)
                {
                    if (text.IndexOf(bad, StringComparison.Ordinal) >= 0)
                        errs.Add($"[AC-39-06a] {label} 的「{where}」出现反幻想禁用词「{bad}」:「{text.Trim()}」");
                }
            }

            if (scanned == 0)
                errs.Add($"[AC-39-06a] {label} 的文本扫描面为 **0 条** —— 判据不可执行(非「通过」)。");

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
