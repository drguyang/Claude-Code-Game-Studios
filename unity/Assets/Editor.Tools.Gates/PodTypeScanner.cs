// 权威来源:Story 002 AC-21a-27 —— ItemInstance 闭集纯 POD 静态断言
//          GDD:design/gdd/item-database.md §Schema E(递归类型图三类漏检:① [SerializeReference]/
//          接口/抽象/object 字段可装任意子类 ② Unity 序列化器乐意存引用 ③ 嵌套时引用从父级渗入)
//          违例夹具:unity/Assets/Tests/EditMode/ItemDatabase/invalid_instance_unity_ref.cs(测试程序集)
//
// ⚠️ 落点(unity-specialist 约束①):反射扫描 = 编辑期职责 ⇒ 住 Editor.Tools.Gates,不进玩家构建;
//    契约类型住 Sim.Contracts(分家)。
// ⚠️ **零 UnityEngine 引用**(unity-specialist 约束②):禁 typeof(SerializeReference) ——
//    一律按 FullName 字符串判(CustomAttributeData / 继承链前缀)。扫描器只用 BCL 反射,
//    被扫类型来自任意程序集(测试程序集的夹具类型能见引擎,扫描器本体不用见)。

using System;
using System.Collections.Generic;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>闭集 POD 类型图静态扫描器(AC-21a-27 执行体)。
    /// <para>违例判据(递归走全类型图,任一命中即错):
    /// ① 字段类型(数组取元素 / <c>Nullable&lt;T&gt;</c> 展开)在 <c>UnityEngine.*</c> 命名空间
    /// 或其**继承链**上(覆盖 SO / Sprite / GameObject 及项目内引擎子类);
    /// ② 字段带 <c>[UnityEngine.SerializeReference]</c>(按属性 FullName 判 —— 可装任意子类);
    /// ③ 字段类型是**接口 / 抽象类 / object**(可装任意子类,直判违例且**不递归**);
    /// ④ 被扫类型本身在 UnityEngine 继承链上。</para>
    /// <para>递归纪律:visited 防环;数组解元素类型(<c>long[]</c> 合法);<c>Nullable&lt;T&gt;</c> 展开;
    /// **泛型容器解实参、实参自调完整判据链**(F2 + F7,2026-09-23 双评审:<c>List&lt;Sprite&gt;</c> 一层
    /// 与 <c>Dictionary&lt;string, List&lt;Sprite&gt;&gt;</c> 任意深度嵌套均命中 —— 内层容器实参若不回环
    /// 会被「基座类型不递归」分支静默漏检);基类链收集字段(止于 object / UnityEngine)。</para>
    /// <example><c>PodTypeScanner.FindForbiddenReferences(typeof(ItemInstance))</c> ⇒ 空(通过);
    /// 扫违例夹具 <c>typeof(InvalidInstanceUnityRef)</c> ⇒ ≥2 条(直接 + 嵌套)。</example></summary>
    public static class PodTypeScanner
    {
        private const string UnityNamespacePrefix = "UnityEngine.";
        private const string SerializeReferenceAttributeFullName = "UnityEngine.SerializeReference";

        /// <summary>从 <paramref name="root"/> 出发递归扫类型图,返回违例描述。</summary>
        /// <param name="root">待扫类型(如 <c>typeof(ItemInstance)</c> 或测试程序集的违例夹具)。</param>
        /// <returns>错误列表;空 = 类型图零引擎引用、零禁形字段。</returns>
        /// <exception cref="ArgumentNullException">root 为 null。</exception>
        public static IReadOnlyList<string> FindForbiddenReferences(Type root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var violations = new List<string>();
            var visited = new HashSet<Type>();
            Walk(root, "root", visited, violations);
            return violations;
        }

        private static void Walk(Type type, string path, HashSet<Type> visited, List<string> violations)
        {
            // ④ 被扫类型本身在引擎继承链上(项目内 SO 子类直接当根扫的情形)。
            if (IsUnityEngineType(type))
            {
                violations.Add($"{path}:类型 {Describe(type)} 在 UnityEngine 继承链上(AC-21a-27)");
                return;
            }

            if (!visited.Add(type))
                return;

            foreach (FieldInfo field in CollectInstanceFields(type))
            {
                string fieldPath = path + "." + field.Name;

                // ② [SerializeReference] 按 FullName 判(零引擎引用约束)。
                foreach (CustomAttributeData attr in field.GetCustomAttributesData())
                {
                    if (string.Equals(attr.AttributeType.FullName,
                            SerializeReferenceAttributeFullName, StringComparison.Ordinal))
                    {
                        violations.Add($"{fieldPath}:带 [UnityEngine.SerializeReference]" +
                            " —— 可装任意子类携带引用,禁形(AC-21a-27)");
                    }
                }

                Type fieldType = Unwrap(field.FieldType);
                ScanFieldType(fieldType, fieldPath, visited, violations);
            }
        }

        /// <summary>单个(已 Unwrap 的)字段/泛型实参类型判据链 —— F2 立骨架、F7 修回环:
        /// 引擎 → 接口/抽象/object 直判 → 原语跳过 → **泛型则对每个实参自调本方法**(任意深度
        /// 嵌套容器 <c>Dictionary&lt;string, List&lt;Sprite&gt;&gt;</c> / <c>List&lt;List&lt;GameObject&gt;&gt;</c>
        /// 均命中 —— 否则内层 <c>List&lt;Sprite&gt;</c> 实参在 F2 版链中被
        /// <c>IsNonProjectSystemType(System.Collections.Generic)</c> 跳过、不再进泛型分支 ⇒ 静默漏检)
        /// → 基座类型跳过 → 项目类型 Walk。</summary>
        private static void ScanFieldType(
            Type fieldType, string fieldPath, HashSet<Type> visited, List<string> violations)
        {
            // ① 引擎类型字段(含继承链)—— 不递归进引擎命名空间。
            if (IsUnityEngineType(fieldType))
            {
                violations.Add($"{fieldPath}:字段类型 {Describe(fieldType)}" +
                    " 是 UnityEngine 引用(AC-21a-27)");
                return;
            }

            // ③ 接口 / 抽象 / object:直判违例,不递归(子类可塞引擎引用 —— GDD 三类漏检①)。
            if (fieldType.IsInterface || fieldType.IsAbstract || fieldType == typeof(object))
            {
                violations.Add($"{fieldPath}:字段类型 {Describe(fieldType)} 是" +
                    " 接口/抽象/object —— 可装任意子类携带引擎引用,禁形(AC-21a-27)");
                return;
            }

            if (fieldType == typeof(string) || fieldType.IsEnum || fieldType.IsPrimitive)
                return;

            // 泛型容器解实参(F2)—— 实参自调本方法(F7 回环):嵌套容器/嵌套引擎/嵌套抽象全覆盖;
            // 容器壳本身(System.Collections.*)无契约面,不 Walk。
            if (fieldType.IsGenericType)
            {
                foreach (Type arg in fieldType.GetGenericArguments())
                    ScanFieldType(Unwrap(arg), fieldPath, visited, violations);
                return;
            }

            if (IsNonProjectSystemType(fieldType))
                return;

            Walk(fieldType, fieldPath, visited, violations);
        }

        /// <summary>收集实例字段(含 private),沿**项目侧**基类链上溯 —— 止于 object / 引擎基类
        /// (引擎基类字段不属本契约面,字段类型侧已拦)。</summary>
        private static IEnumerable<FieldInfo> CollectInstanceFields(Type type)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (Type current = type; current != null && current != typeof(object);
                 current = current.BaseType)
            {
                if (IsUnityEngineType(current))
                    yield break;

                foreach (FieldInfo field in current.GetFields(flags))
                {
                    if (field.IsStatic)
                        continue;
                    yield return field;
                }
            }
        }

        /// <summary>数组 → 元素类型(<c>long[]</c> → <c>long</c>;<c>Sprite[]</c> → <c>Sprite</c>)。</summary>
        private static Type Unwrap(Type type)
        {
            if (type.IsArray)
            {
                Type element = type.GetElementType();
                if (element != null)
                    return Unwrap(element);
            }

            if (Nullable.GetUnderlyingType(type) is Type underlying)
                return Unwrap(underlying);

            return type;
        }

        /// <summary>类型本身或任一基类在 <c>UnityEngine.*</c> 命名空间(按 FullName 前缀,零引擎引用)。</summary>
        private static bool IsUnityEngineType(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                string fullName = current.FullName;
                if (fullName != null &&
                    (fullName.StartsWith(UnityNamespacePrefix, StringComparison.Ordinal) ||
                     string.Equals(fullName, "UnityEngine", StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>System.* / mscorlib 等基座类型不递归(DateTime 等无契约面字段)。
        /// 项目类型命名空间以 DaYiJingCheng 起,测试夹具同族。</summary>
        private static bool IsNonProjectSystemType(Type type)
        {
            string ns = type.Namespace;
            if (string.IsNullOrEmpty(ns))
                return false;

            return ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ||
                   ns == "Mono" || ns.StartsWith("Mono.", StringComparison.Ordinal) ||
                   ns == "Unity" || ns.StartsWith("Unity.", StringComparison.Ordinal);
        }

        private static string Describe(Type type) => type.FullName ?? type.Name;
    }
}
