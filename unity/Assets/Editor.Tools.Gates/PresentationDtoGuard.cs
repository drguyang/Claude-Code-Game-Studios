// 权威来源:
//   ADR-013 §三 F7(AC-37-15:`PresentationDtoGuard` 递归反射扫描的接口草图 —— 此前**全库
//     只有规格没有实现**,2026-09-26 audio Story 001 首落;协调方已核对:ADR-013 只有
//     `public static class PresentationDtoGuard { AssertNoDiseaseId(Type) }` 一行草图)
//   ADR-018 §一 / §二(44 与 42 同构:同一守卫覆盖 `AudioCueDto` —— AC-44-B2;AC-37-15 一致性)
//   control-manifest 跨切约束 2(`disease_id` 与任何诊断语义不得进呈现层 DTO ⇒ 落地 =
//     PresentationDtoGuard **递归**反射扫描,**非 grep**)
//   Story: production/epics/audio-system/story-001-assembly-boundary-dto.md AC-44-B2
//
// 落点:Editor.Tools.Gates(EditMode 门,不进构建;照 ConservationGates.cs 先例 =
//   `public static class` + `IReadOnlyList<string>` 错误列表形态,由调用方决定 throw /
//   聚合 —— 本类自身不引 NUnit)。
//
// 递归纪律(2026-09-26 unity-specialist 审查要点,逐条兑现):
//   · **visited 集合是唯一的环 / 重复终止条件** —— 不得用「程序集白名单」当递归终止
//     (否则项目嵌套类型成叶子 = 假绿);
//   · 引擎 / BCL 类型不展开其自有成员(其字段由引擎 / 运行库定义,不可能携带本项目
//     disease 语义),**但仍展开泛型实参** —— `List<Inner>` 的 Inner 必须被扫到;
//   · 字段与**属性**都扫(自动属性的 `<Prop>k__BackingField` 回推属性名);
//   · 名称归一化 = 去 `<>…k__BackingField` 装饰 + 去下划线 / 连字符 + 小写,子串判
//     「disease」(covers `disease_id` / `diseaseId` / `_diseaseId` / `DiseaseSnapshot`);
//   · **类型名**同样归一化判(字段叫 `snapshot` 但类型是 `DiseaseIdSet` 也抓);
//   · 基类沿 DeclaredOnly 链逐层展开(基类私有字段也抓);方法签名(参数名 / 参数类型 /
//     返回类型)一并扫;
//   · 深度护栏 64 层(正常 DTO 树远低;超限即断,不静默截断 —— 断在红行里可见)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>呈现层 DTO 递归扫描:疾病 / 诊断语义字段禁入
    /// (AC-37-15 · AC-44-B2 · ADR-013 §三 · ADR-018 §一)。
    /// EditMode 门;`Scan` 返回错误列表(不 throw),`AssertNoDiseaseId` 提供草图签名形态。</summary>
    public static class PresentationDtoGuard
    {
        // 2026-09-26 代码审查 Required-4:control-manifest 跨切约束 2 = 「disease_id **与任何
        // 诊断语义**」—— 单 "disease" 词面下 `diagnosisId` / `symptom` 假绿。词面集三元,
        // 归一化后子串判。
        private static readonly string[] DiagnosticTokens = { "diseas", "diagnos", "symptom" };

        private static bool HasDiagnosticToken(string normalizedName)
            => DiagnosticTokens.Any(t => normalizedName.Contains(t));

        /// <summary>递归扫描 <paramref name="dtoRoot"/> 的类型树,返回全部违例(空 = 干净)。
        /// 递归规则见文件头;null 入口 = 红(拒以空集冒充绿)。</summary>
        public static IReadOnlyList<string> Scan(Type dtoRoot)
        {
            var errs = new List<string>();
            if (dtoRoot == null)
            {
                errs.Add("[DtoGuard] dtoRoot == null —— 拒以空集冒充绿(AC-37-15)。");
                return errs;
            }

            var visited = new HashSet<Type>();
            Visit(dtoRoot, dtoRoot.Name, 0);
            return errs;

            void Visit(Type t, string path, int depth)
            {
                if (t == null) return;
                if (depth > 64)
                {
                    errs.Add($"[DtoGuard] 深度护栏触发于 {path}(depth > 64)—— 类型树异常深," +
                             "拒绝静默截断;请拆 DTO 或上报。");
                    return;
                }
                if (t.IsArray || t.IsByRef || t.IsPointer)
                {
                    Visit(t.GetElementType(), path + "[]", depth + 1);
                    return;
                }
                if (!visited.Add(t)) return;   // 环 / 重复的唯一终止条件

                // 类型名携带 disease 语义(如 DiseaseIdSet)= 违例本体
                var typeName = NormalizeMemberName(t.Name);
                if (HasDiagnosticToken(typeName))
                    errs.Add($"[DtoGuard] 类型「{t.FullName}」(路径 {path})名携带 disease 语义" +
                             " —— AC-37-15 / AC-44-B2。");

                if (!ShouldExpandMembers(t))
                {
                    // 引擎 / BCL:自有成员不展开(见文件头),泛型实参 / 元素类型照走
                    if (t.IsGenericType)
                        foreach (var a in t.GetGenericArguments())
                            Visit(a, path + "<" + a.Name + ">", depth + 1);
                    return;
                }

                const BindingFlags fb = BindingFlags.DeclaredOnly |
                                         BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (!ShouldExpandMembers(cur)) break;   // 基类落到引擎 / BCL(ValueType / Object)即停链

                    foreach (var f in cur.GetFields(fb))
                    {
                        var nm = NormalizeMemberName(f.Name);
                        if (HasDiagnosticToken(nm))
                            errs.Add($"[DtoGuard] {path}.{f.Name} —— 字段名携带 disease 语义" +
                                     "(含私有 / 自动属性背字段;AC-37-15 / AC-44-B2)。");
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                    }
                    foreach (var p in cur.GetProperties(fb))
                    {
                        var nm = NormalizeMemberName(p.Name);
                        if (HasDiagnosticToken(nm))
                            errs.Add($"[DtoGuard] {path}.{p.Name} —— 属性名携带 disease 语义" +
                                     "(AC-37-15 / AC-44-B2)。");
                        Visit(p.PropertyType, path + "." + p.Name, depth + 1);
                    }
                    foreach (var m in cur.GetMethods(fb))
                    {
                        foreach (var prm in m.GetParameters())
                        {
                            var nm = NormalizeMemberName(prm.Name);
                            if (HasDiagnosticToken(nm))
                                errs.Add($"[DtoGuard] {path}.{m.Name}({prm.Name}) —— 参数名携带" +
                                         " disease 语义(AC-37-15 / AC-44-B2)。");
                            Visit(prm.ParameterType, path + "." + m.Name + "(" + prm.Name + ")",
                                  depth + 1);
                        }
                        Visit(m.ReturnType, path + "." + m.Name + ":ret", depth + 1);
                    }
                    // 构造函数不被 GetMethods 返回 —— 单独扫(GetMethods 漏构造参数会成假绿面)
                    foreach (var c in cur.GetConstructors(fb))
                        foreach (var prm in c.GetParameters())
                        {
                            var nm = NormalizeMemberName(prm.Name);
                            if (HasDiagnosticToken(nm))
                                errs.Add($"[DtoGuard] {path}.#ctor({prm.Name}) —— 参数名携带" +
                                         " disease 语义(AC-37-15 / AC-44-B2)。");
                            Visit(prm.ParameterType, path + ".#ctor(" + prm.Name + ")", depth + 1);
                        }
                }
            }
        }

        /// <summary>ADR-013 §三 草图签名:有违例即 throw(供 CI / 单测一行式调用)。</summary>
        public static void AssertNoDiseaseId(Type dtoRoot)
        {
            var errs = Scan(dtoRoot);
            if (errs.Count > 0)
                throw new InvalidOperationException(
                    "[PresentationDtoGuard] disease 语义禁入呈现层 DTO(AC-37-15 / AC-44-B2):\n" +
                    string.Join("\n", errs));
        }

        /// <summary>成员展开资格 —— **默认展开**,只有引擎 / BCL 命名空间跳过自有成员
        /// (它们的字段由引擎 / 运行库定义,不可能携带本项目 disease 语义)。
        /// ⚠️ 2026-09-26 定稿:刻意**不用**「项目命名空间白名单」当展开条件(反向白名单 =
        /// 白名单外的项目类型成叶子 = 假绿;全局命名空间 / 第三方 ns 的 DTO 会静默漏扫)。
        /// 这只决定「展不展开成员」,**不**作为违例豁免 —— 非引擎类型永不被当叶子跳过。</summary>
        private static bool ShouldExpandMembers(Type t)
        {
            var ns = t.Namespace;
            if (string.IsNullOrEmpty(ns)) return true;   // 全局命名空间:默认展开(防假绿)
            return !(ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ||
                     ns == "UnityEngine" || ns.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                     ns == "UnityEditor" || ns.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
                     ns.StartsWith("Unity.", StringComparison.Ordinal) ||
                     ns.StartsWith("Microsoft.", StringComparison.Ordinal));
        }

        /// <summary>名称归一化:`&lt;Prop&gt;k__BackingField` 取 Prop;去 `_` `-`;小写。
        /// 归一化后子串判「disease」⇒ `disease_id` / `diseaseId` / `_diseaseId` 全命中。</summary>
        private static string NormalizeMemberName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var a = name.IndexOf('<');
            var b = name.IndexOf('>');
            if (a >= 0 && b > a) name = name.Substring(a + 1, b - a - 1);
            return name.Replace("_", "").Replace("-", "").ToLowerInvariant();
        }
    }
}
