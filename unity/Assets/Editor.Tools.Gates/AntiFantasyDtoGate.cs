// story-005 · AC-17-09 —— 呈现层 DTO 的**反幻想**递归类型扫描。
//
// 权威来源:
//   design/gdd/foraging.md AC-17-09(BLOCKING):
//     「GIVEN 全部呈现层 DTO,WHEN **递归 type-surface 反射扫描**,THEN 无任何
//      「原始品级 / 本可采到」字段(AC-37-15 同型;**非按名 grep**)」
//   GDD 规则四 注 · §Edge Cases「`QualityCap` 截断后 `raw_quality` 丢失」:
//     17 **不呈现**「你本可采到更高档」—— 那是把被截断的信息偷偷还给玩家
//   story production/epics/foraging/story-005-action-atomicity-failure-convergence.md
//     Implementation Note 4(`GatherAffordanceDto` 的形状:整数格邻域布尔 / 节点类,**无余量数字**)
//
// ── 为什么单独一个类,不复用 `PresentationDtoGuard` ────────────────────────
// 后者判的是 **disease 语义**(`diseas`/`diagnos`/`symptom`),本类判的是 **反幻想**
// (`raw_quality`/`本可采到`/`余量数字`)。二者是**不同判决**,不是同一判决的两组参数:
// 若把词表做成运行期可传参数,那 42 那道门的判据就能被调用方改宽 —— 门就成了摆设。
// **递归遍历**的形态两者一致(字段 / 属性 / 构造参数 / 嵌套类型 / 泛型实参),但词表各自持有。
//
// ── 强制点(2026-10-31 补)─────────────────────────────────────────────────
// `RunAll()` 从**编译产物**载入 `Sim.Contracts` 的 DTO 类型并逐个扫描,接进
// `AssemblyGates.RunAll` ⇒ 有构建期强制点(不是只靠 EditMode 夹具)。
// 取「按名载入」而不是「直接 typeof」:本程序集**不引用** `Sim.Contracts`,
// 门与被判对象之间无编译期耦合 —— DTO 改名/删除会让门报「类型不存在」,
// 而不是悄悄扫了个空集。
//
// ⚠️ **残余(如实登记)**:扫描面是「类型面的**名字**」—— 值级泄漏(例如把原始品级乘 1
//   塞进一个叫 `Intensity` 的字段)抓不到。那条只能靠 17 侧「不产出该值」的结构保证
//   (见 `NodeRemaining` 只出剩余量、不出「本可采到」)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>17 呈现 DTO 的反幻想递归类型扫描(AC-17-09)。</summary>
    public static class AntiFantasyDtoGate
    {
        /// <summary>
        /// 禁名 token(归一化后子串判)。
        /// </summary>
        /// <remarks>
        /// 来源逐条:① `rawquality` —— GDD 规则四「原始品级」;② `qualitycap*` 的值形态;
        /// ③ `potential` / `could*` / `would*` —— 「本可采到」;④ `remaining` ——
        /// Implementation Note 4 逐字「**无余量数字**」(余量是主机裁决的输入,不是呈现物)。
        /// </remarks>
        public static readonly string[] ForbiddenTokens =
        {
            "rawquality", "qualityraw", "potential", "couldharvest", "couldget",
            "wouldhave", "wouldyield", "remaining",
        };

        /// <summary>
        /// 递归扫描 <paramref name="dtoRoot"/> 的类型树,返回全部违例(空 = 干净)。
        /// </summary>
        /// <param name="dtoRoot">DTO 根类型。</param>
        /// <exception cref="ArgumentNullException"><paramref name="dtoRoot"/> 为 null ——
        /// **不返回空集**:空集会被读成「干净」。</exception>
        public static IReadOnlyList<string> Scan(Type dtoRoot)
        {
            if (dtoRoot == null)
                throw new ArgumentNullException(nameof(dtoRoot),
                    "dtoRoot == null —— 拒以空集冒充绿(AC-17-09)");

            var errs = new List<string>();
            var visited = new HashSet<Type>();
            Visit(dtoRoot, dtoRoot.Name, 0);
            return errs;

            void Visit(Type t, string path, int depth)
            {
                if (t == null) return;
                if (depth > 64)
                {
                    errs.Add($"[AntiFantasy] 深度护栏触发于 {path} —— 拒绝静默截断");
                    return;
                }
                if (t.IsArray || t.IsByRef || t.IsPointer)
                {
                    Visit(t.GetElementType(), path + "[]", depth + 1);
                    return;
                }
                if (!visited.Add(t)) return;

                if (HasForbiddenToken(t.Name))
                    errs.Add($"[AntiFantasy] 类型「{t.FullName}」(路径 {path})名携带反幻想语义");

                // 引擎 / BCL 只走泛型实参(与 PresentationDtoGuard 同口径)
                if (t.Namespace != null && (t.Namespace.StartsWith("System", StringComparison.Ordinal)
                                            || t.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal)
                                            || t.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal)))
                {
                    if (t.IsGenericType)
                        foreach (Type a in t.GetGenericArguments()) Visit(a, path + "<" + a.Name + ">", depth + 1);
                    return;
                }

                const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Instance
                                      | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                for (Type cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (cur.Namespace != null && cur.Namespace.StartsWith("System", StringComparison.Ordinal))
                        break;

                    foreach (FieldInfo f in cur.GetFields(fb))
                    {
                        if (HasForbiddenToken(f.Name))
                            errs.Add($"[AntiFantasy] {path}.{f.Name} —— 字段名携带反幻想语义(AC-17-09)");
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                    }
                    foreach (PropertyInfo p in cur.GetProperties(fb))
                    {
                        if (HasForbiddenToken(p.Name))
                            errs.Add($"[AntiFantasy] {path}.{p.Name} —— 属性名携带反幻想语义(AC-17-09)");
                        Visit(p.PropertyType, path + "." + p.Name, depth + 1);
                    }
                    foreach (ConstructorInfo c in cur.GetConstructors(fb))
                        foreach (ParameterInfo prm in c.GetParameters())
                        {
                            if (HasForbiddenToken(prm.Name))
                                errs.Add($"[AntiFantasy] {path}.#ctor({prm.Name}) —— 参数名携带反幻想语义");
                            Visit(prm.ParameterType, path + ".#ctor(" + prm.Name + ")", depth + 1);
                        }
                    foreach (MethodInfo m in cur.GetMethods(fb))
                    {
                        if (m.IsSpecialName) continue;   // 属性访问器已由上面的 PropertyInfo 覆盖
                        foreach (ParameterInfo prm in m.GetParameters())
                        {
                            if (HasForbiddenToken(prm.Name))
                                errs.Add($"[AntiFantasy] {path}.{m.Name}({prm.Name}) —— 参数名携带反幻想语义");
                            Visit(prm.ParameterType, path + "." + m.Name + "(" + prm.Name + ")", depth + 1);
                        }
                        Visit(m.ReturnType, path + "." + m.Name + ":ret", depth + 1);
                    }
                }
            }
        }

        /// <summary>归一化(去下划线、小写)后做子串判。</summary>
        public static bool HasForbiddenToken(string memberName)
        {
            if (string.IsNullOrEmpty(memberName)) return false;
            var sb = new StringBuilder(memberName.Length);
            foreach (char c in memberName)
                if (c != '_') sb.Append(char.ToLowerInvariant(c));
            string n = sb.ToString();
            foreach (string tok in ForbiddenTokens)
                if (n.Contains(tok)) return true;
            return false;
        }

        /// <summary>被本门判决的 DTO 全名(17 的呈现面)。</summary>
        public static readonly string[] GuardedDtoTypeNames =
        {
            "DaYiJingCheng.Sim.Contracts.GatherAffordanceDto",
            "DaYiJingCheng.Sim.Contracts.AudioCueDto",
        };

        /// <summary>
        /// 生产入口:从 `Sim.Contracts` 编译产物载入 DTO 类型并扫描。空集 = 绿。
        /// </summary>
        /// <param name="contractsAssemblyPath">`Sim.Contracts.dll` 路径
        /// (`AssemblyGates.ScriptAssemblyPath("Sim.Contracts")`)。</param>
        /// <remarks>⚠️ 类型缺失 ⇒ 报错,**不**跳过 —— 否则「门开着但没扫到东西」会以绿的形式留下。</remarks>
        public static List<string> RunAll(string contractsAssemblyPath)
        {
            var errs = new List<string>();
            if (string.IsNullOrEmpty(contractsAssemblyPath) || !File.Exists(contractsAssemblyPath))
            {
                errs.Add($"[AC-17-09] 编译产物缺失「{contractsAssemblyPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }

            Assembly asm = Assembly.LoadFrom(contractsAssemblyPath);
            foreach (string fullName in GuardedDtoTypeNames)
            {
                Type t = asm.GetType(fullName, throwOnError: false);
                if (t == null)
                {
                    errs.Add($"[AC-17-09] 找不到 DTO 类型「{fullName}」—— 改名 / 删除?门须同步改口径,不得静默跳过");
                    continue;
                }
                foreach (string e in Scan(t)) errs.Add(e);
            }
            return errs;
        }
    }
}