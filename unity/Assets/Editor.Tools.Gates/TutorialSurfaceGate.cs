// 权威来源:
//   `design/gdd/tutorial-and-onboarding.md` **AC-48-04** —— 48 的**交付物**类型不得引用
//     `SimEvent` / `PatientState` / `Fix`(含嵌套泛型参数),载体 = **递归**反射扫描;
//     §规则七 判据段(2026-09-19 修订)点名「asmdef 白名单 + IL 扫描断言」,与 44 的
//     `AC-44-B1` 同型。
//   `docs/architecture/adr-013` §三 F7 —— `PresentationDtoGuard` 的递归形制(本类照它写)。
//   `docs/architecture/adr-019` §三 —— 51 同构先例(边界层只读消费者)
//   Story: production/epics/tutorial-onboarding/story-002-read-only-projection-and-assembly-guards.md
//
// 落点:`Editor.Tools.Gates`(EditMode 门,不进构建;照 `PresentationDtoGuard` 先例 =
//   `public static class` + 返回错误列表,本类自身不引 NUnit、不 throw / 不决定处置)。
//
// 与 `PresentationDtoGuard` 的分工(刻意不合并):
//   · 那一支管**字段语义**(`disease` / `diagnos` / `symptom` 不得进呈现层 DTO,AC-37-15);
//   · 本支管**类型引用**(sim 类型不得出现在 48 的公开面,AC-48-04)。
//   合并会把两套词表与两套递归语义缠在一起 —— 一处放宽就同时放宽两面。
//
// 递归纪律(逐条承 `PresentationDtoGuard`,理由同源):
//   · **visited 集合是唯一的环 / 重复终止条件** —— 不得用「程序集白名单」当终止(否则
//     嵌套类型成叶子 = 假绿);
//   · 引擎 / BCL 类型不展开自有成员,**但仍展开泛型实参**(`List<Fix>` 必须被扫到);
//   · 字段与**属性**都扫(自动属性的背字段回推属性名);
//   · 方法签名(参数类型 / 返回类型)· **构造函数参数**一并扫(`GetMethods` 不返回构造器);
//   · 基类沿 `DeclaredOnly` 链逐层展开;接口实现一并扫;
//   · 深度护栏 64 层 —— 超限即断并**报错行**,不静默截断。
//
// ⚠️ **`PatientState` 今日全库不存在**(2026-11-06 实测 `grep -rn "class PatientState\|struct PatientState"`
//    零命中;只有表现层的 `PresentPatientState` 枚举)。⇒ 该条是**零输入可达的规则**:
//    它守着一条 GDD 点名的类型,而今天没有任何代码能满足「命中」这一侧。
//    如实登记,不当作「已兑现的守卫」(承 ADR-035 N-4 的同款纪律)。按**简单名精确匹配**
//    而非子串 —— 否则 `PresentPatientState` 会被误判(它恰是 48 合法可读的呈现态)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>48 程序集公开面的递归反射扫描:sim 类型禁入(AC-48-04)。
    /// <para>扫描器**不 throw**、不引 NUnit —— 调用方决定聚合还是抛(照 `PresentationDtoGuard`)。</para></summary>
    public static class TutorialSurfaceGate
    {
        /// <summary>48 的只读投影装配名(`ADR-025 §①` 2026-11-06 行)。</summary>
        public const string TutorialAssemblyName = "Gameplay.UI.Tutorial";

        /// <summary>
        /// 门链入口(`AssemblyGates.RunAll` 调用 —— 菜单 / 脚本 reload / 构建前门三处共用)。
        /// <para>⚠️ 装配按**名字**取,不 `typeof(...)`:48 装配编译失败时本门所在装配仍要能编过,
        /// 否则「门自己也编不过」= 连报错的机会都没有。</para>
        /// </summary>
        public static List<string> RunAll()
        {
            var errs = new List<string>();
            System.Reflection.Assembly asm;
            try
            {
                asm = System.Reflection.Assembly.Load(TutorialAssemblyName);
            }
            catch (Exception ex)
            {
                // 装配缺失 / 改名 ⇒ 判据无法成立。**不静默跳过**(那会让 AC-48-04 悄然消失)。
                errs.Add($"[AC-48-04] 找不到装配「{TutorialAssemblyName}」—— 48 的引用面判据无法成立:" +
                         $"{ex.GetType().Name}: {ex.Message}");
                return errs;
            }

            errs.AddRange(ScanAssembly(asm));
            return errs;
        }

        /// <summary>禁引类型的**全名**(`SimEvent` / `Fix` 住 `Sim.Contracts`)。</summary>
        private static readonly string[] ForbiddenFullNames =
        {
            "DaYiJingCheng.Sim.Contracts.SimEvent",
            "DaYiJingCheng.Sim.Contracts.Fix",
        };

        /// <summary>禁引类型的**简单名**(GDD 点名但今日全库无此类型 —— 见文件头)。</summary>
        private static readonly string[] ForbiddenSimpleNames = { "PatientState" };

        /// <summary>扫描某个类型树(起点通常是 48 的公开入口类型)。空列表 = 干净。</summary>
        /// <param name="root">起点类型;null ⇒ 一条违例(拒绝以空集冒充绿)。</param>
        public static IReadOnlyList<string> Scan(Type root)
        {
            var errs = new List<string>();
            if (root == null)
            {
                errs.Add("[TutorialSurfaceGate] root == null —— 拒以空集冒充绿(AC-48-04)。");
                return errs;
            }

            var visited = new HashSet<Type>();
            Visit(root, root.Name, 0);
            return errs;

            void Visit(Type t, string path, int depth)
            {
                if (t == null) return;
                if (depth > 64)
                {
                    errs.Add($"[TutorialSurfaceGate] 深度护栏触发于 {path}(depth > 64)—— 类型树异常深," +
                             "拒绝静默截断;请拆类型或上报。");
                    return;
                }
                if (t.IsArray || t.IsByRef || t.IsPointer)
                {
                    Visit(t.GetElementType(), path + "[]", depth + 1);
                    return;
                }
                if (t.IsGenericParameter) return;
                if (!visited.Add(t)) return;

                string verdict = ForbiddenReason(t);
                if (verdict != null)
                    errs.Add($"[TutorialSurfaceGate] 类型「{t.FullName}」(路径 {path}){verdict}" +
                             " —— AC-48-04。");

                if (!ShouldExpandMembers(t))
                {
                    if (t.IsGenericType)
                        foreach (var a in t.GetGenericArguments())
                            Visit(a, path + "<" + a.Name + ">", depth + 1);
                    return;
                }

                // 基类链:34 的 DTO 常继承 `PresentationRoot` / 值类型;
                // 逐层 DeclaredOnly 才能抓到基类私有字段(同 PresentationDtoGuard)。
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (!ShouldExpandMembers(cur)) break;

                    foreach (var i in cur.GetInterfaces())
                        Visit(i, path + " : " + i.Name, depth + 1);

                    const BindingFlags fb = BindingFlags.DeclaredOnly |
                                             BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.Public | BindingFlags.NonPublic;

                    foreach (var f in cur.GetFields(fb))
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);

                    foreach (var p in cur.GetProperties(fb))
                        Visit(p.PropertyType, path + "." + p.Name, depth + 1);

                    foreach (var m in cur.GetMethods(fb))
                    {
                        foreach (var prm in m.GetParameters())
                            Visit(prm.ParameterType, path + "." + m.Name + "(" + prm.Name + ")", depth + 1);
                        Visit(m.ReturnType, path + "." + m.Name + ":ret", depth + 1);
                    }

                    // 构造函数不在 GetMethods 内 —— 漏扫构造参数会成假绿面。
                    foreach (var c in cur.GetConstructors(fb))
                        foreach (var prm in c.GetParameters())
                            Visit(prm.ParameterType, path + ".#ctor(" + prm.Name + ")", depth + 1);
                }
            }
        }

        /// <summary>
        /// 扫描一个程序集的**公开面**(`GetExportedTypes`)。
        /// <para>⚠️ 公开面为空 ⇒ **一条违例**(空扫描面 = 判据恒绿,正是假绿本身 ——
        /// 承 `readonly_boundary_test` 2026-10-30 的同款教训)。</para>
        /// </summary>
        /// <param name="assembly">目标程序集(通常 `typeof(TutorialProjection).Assembly`)。</param>
        public static IReadOnlyList<string> ScanAssembly(Assembly assembly)
        {
            var errs = new List<string>();
            if (assembly == null)
            {
                errs.Add("[TutorialSurfaceGate] assembly == null —— 拒以空集冒充绿(AC-48-04)。");
                return errs;
            }

            Type[] exported;
            try { exported = assembly.GetExportedTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                errs.Add("[TutorialSurfaceGate] 类型装载失败,判据无法成立(拒以空集冒充绿):" +
                         string.Join(" | ", ex.LoaderExceptions.Select(e => e?.Message)));
                return errs;
            }

            if (exported.Length == 0)
            {
                errs.Add($"[TutorialSurfaceGate] 程序集「{assembly.GetName().Name}」公开面为空 —— " +
                         "扫描面零输入 ⇒ 本判据会空转通过(假绿)。");
                return errs;
            }

            foreach (var t in exported)
                errs.AddRange(Scan(t));
            return errs;
        }

        /// <summary>逐条判违例;命中返回原因串,否则 null。</summary>
        private static string ForbiddenReason(Type t)
        {
            string full = t.FullName;
            for (int i = 0; i < ForbiddenFullNames.Length; i++)
                if (string.Equals(full, ForbiddenFullNames[i], StringComparison.Ordinal))
                    return "是 AC-48-04 点名的 sim 类型";

            string simple = t.Name;
            // 泛型类型的 Name 带 `\`N` 后缀(`List\`1`)—— 取反引号前段再比。
            int tick = simple.IndexOf('`');
            if (tick > 0) simple = simple.Substring(0, tick);

            for (int i = 0; i < ForbiddenSimpleNames.Length; i++)
                if (string.Equals(simple, ForbiddenSimpleNames[i], StringComparison.Ordinal))
                    return "是 AC-48-04 点名的 sim 类型";

            return null;
        }

        /// <summary>成员展开资格 —— **默认展开**,只跳过引擎 / BCL 命名空间的自有成员
        /// (同 `PresentationDtoGuard`:反向白名单会让白名单外的类型成叶子 = 假绿)。</summary>
        private static bool ShouldExpandMembers(Type t)
        {
            var ns = t.Namespace;
            if (string.IsNullOrEmpty(ns)) return true;
            return !(ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ||
                     ns == "UnityEngine" || ns.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                     ns == "UnityEditor" || ns.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
                     ns.StartsWith("Unity.", StringComparison.Ordinal) ||
                     ns.StartsWith("Microsoft.", StringComparison.Ordinal));
        }
    }
}
