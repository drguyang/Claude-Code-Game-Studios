// 权威来源:
//   AC-20-15(BLOCKING,design/gdd/inventory-and-items.md:852):
//     「GIVEN 全部 20 的输出 DTO,WHEN **递归**反射扫描,THEN 无 `float` 字段(门 A · 与 AC-44-B1 同型)」
//   TR-inventory-012(tr-registry.yaml:3753):「全部 20 的输出 DTO 递归反射扫描无 float」
//   ADR-006 §Decision 一 / §Decision 二(定点域边界契约 —— 浮点不得出现在 sim 面)
//   ADR-025 §①(`Sim` / `Sim.Contracts` 的引用集 = BCL,零引擎引用)
//
// ⚠️ **本件为什么存在(2026-10-26 story-003 前置勘察实测)**:
//   story-003 卡面 `Engine Notes` 与 Implementation Note 6 都点名「`PresentationDtoGuard`
//   递归反射扫描复用 case-system 先例件」。**实测该件不是 float 门** ——
//   `PresentationDtoGuard.cs:41` 的判据是 `DiagnosticTokens = { "diseas", "diagnos", "symptom" }`,
//   扫的是**疾病语义**。用它在 AC-20-15 上记绿 = 本项目已抓过五次的
//   「自造断言顶替 AC 语义层」。⇒ 卡面载体的措辞是错的,本门是新写的判据本体。
//
// ⚠️ **扫描面 = 目录,不是命名空间前缀**(2026-10-26 实测裁定):
//   20 的 `.cs` 落 `Assets/Sim/ItemDatabase/` 与 `Assets/Sim.Contracts/ItemDatabase/`,
//   但它们的 namespace 是**扁平**的 `DaYiJingCheng.Sim` / `DaYiJingCheng.Sim.Contracts`
//   —— 与其它子系统共用。按 namespace 前缀扫会**误伤**:
//   `Sim.Contracts/VitalsDto.cs:35/37` 有 `float Position` / `float Trend`,那是
//   ADR-013 §三 定死的**唯一合法浮点出口**(13 的呈现边界),**不属于** 20 的输出 DTO。
//   ⇒ 扫描面必须以**源目录**界定。见 <see cref="EnumerateTypeFullNames"/>。
//
// ⚠️ **两层判据,各有各的不可替代性**:
//   层 1 · **递归反射**(类型面):判「DTO 的**字段类型**是否为 float/double/decimal」。
//            覆盖泛型实参、数组 / 集合元素类型、基类链私有字段、属性后备字段、方法签名。
//            这是 AC-20-15 原文要的那一层(「无 `float` 字段」)。
//   层 2 · **Cecil IL**(值面):判「方法体里有没有 `conv.r4` / `conv.r8` / `ldc.r4` / `ldc.r8`」,
//            外加局部变量 / 字段 / 签名的浮点类型面。反射看不见 IL ——
//            常量折叠、库返回值经 `conv.r4` 落栈这类**非字面量浮点**只有 IL 面抓得到。
//            先例 = `EcozoneIntegerGates.cs:59`(`AC-6-22` 的同一形状)。
//    ⚠️ 两层**都跑**才算绿:任一层红 ⇒ 红。缺一层即按 `NOT-RUN` 登记。
//
// ⚠️ **两层共用同一份类型清单** —— 层 1 的类型由调用方按 `EnumerateTypeFullNames` 的结果
//   从装配反射取得,层 2 按**同一清单**在 IL 面过滤。两层面不同 = 可以各绿而语义分叉
//   (原双真源失效模式的同型)。
//
// ⚠️ **递归纪律**(照 `PresentationDtoGuard` 的既有教训,逐条兑):
//   · `visited` 集合是**唯一**的环 / 重复终止条件 —— 不得用「程序集白名单」当终止条件
//     (否则项目嵌套类型成叶子 = 假绿);
//   · BCL / 引擎类型不展开其自有成员,但**仍展开泛型实参**(`List<T>` 的 T 必须被扫);
//   · 字段与属性都扫(自动属性的 `<Prop>k__BackingField` 与属性名各算一路);
//   · 基类沿 `DeclaredOnly` 链逐层展开(基类私有字段也抓);
//   · 方法签名(参数类型 / 返回类型 / 泛参)一并扫;
//   · 深度护栏 64 层(超限即**红**,不静默截断)。
//
// ⚠️ **不做的事**:不扫 `Fix`(`Fix` 是 Q16.16 raw `long` 的整数定点型,`Fix.ToFloat()` 是
//   唯一合法浮点出口且只允许在 `Gameplay.Presentation` 调用 —— 20 的 DTO 里出现 `Fix` 字段
//   **不算**违 AC-20-15)。本门判的是 `float` / `double` / `decimal` 三种 CLR 浮点类型。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>AC-20-15 的双层门:20 的全部输出 DTO 递归反射 + IL 扫描零浮点。
    /// <para>EditMode 门(不进构建);`Scan*` 返回错误列表(不 throw),由调用方决定抛 / 聚合 ——
    /// 照 `ConservationGates` / `PresentationDtoGuard` 的既有形态。</para>
    /// </summary>
    public static class InventoryDtoFloatGate
    {
        /// <summary>20 的实现落点(相对工程根 `unity/`)。**扫描面 = 这两个目录下的全部 .cs**。</summary>
        public static readonly string[] ScanDirsRelative =
        {
            "Assets/Sim/ItemDatabase",
            "Assets/Sim.Contracts/ItemDatabase",
        };

        /// <summary>层 1 / 层 2 的禁用类型(CLR 三种浮点;`decimal` 一并禁 ——
        /// 它同样破 ADR-006 的定点边界,且其运算非跨平台逐位确定)。</summary>
        private static readonly Type[] ForbiddenClrTypes =
        {
            typeof(float), typeof(double), typeof(decimal),
        };

        /// <summary>层 2 的禁用 IL 指令(AC-6-22 同款四个)。</summary>
        private static readonly OpCode[] ForbiddenFloatOpcodes =
        {
            OpCodes.Conv_R4, OpCodes.Conv_R8,
            OpCodes.Ldc_R4, OpCodes.Ldc_R8,
        };

        /// <summary>层 2 的浮点类型全名(局部变量 / 字段 / 签名面)。</summary>
        private static readonly string[] ForbiddenFloatFullNames =
            { "System.Single", "System.Double", "System.Decimal" };

        private const int MaxDepth = 64;

        /// <summary>**具名**浮点出口白名单(类型全名 + 成员名)。
        /// <para>⚠️ 为什么必须是「具名方法」而不是「豁免整个 `Fix` 类型」:
        /// `Fix` 是 Q16.16 raw `long` 的整数定点型,`Fix.ToFloat()` 是 ADR-006 / ADR-025 定死的
        /// **唯一合法浮点出口**(下游 `Gameplay.Presentation` 渲染才调用,不参与 sim 位运算)。
        /// 若图省事把 `Fix` 整个列为「非项目类型不展开」,那么**将来给 `Fix` 加一个 `float` 字段
        /// 或第二个 float 返回方法也不会被抓** —— 白名单会随类型一起静默扩大。
        /// ⇒ 只放行**这一个成员**,`Fix` 的字段面 / 其余方法面照扫。
        /// </para>
        /// <para>白名单是**显式枚举**的:新增第二处合法出口必须改本数组(评审可见),
        /// 不得用前缀 / 正则放宽。</para></summary>
        private static readonly HashSet<string> AllowedFloatMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "DaYiJingCheng.Sim.Contracts.Fix::ToFloat",
        };

        // ─────────────────── 扫描面枚举(两层共用单一真源) ───────────────────

        /// <summary>源目录里声明的全部顶层类型全名(<c>namespace.Type</c>)。
        /// <para>实现 = 逐 <c>.cs</c> 读 `namespace X` 与 `public (readonly )?(struct|class|enum|interface) Y`。
        /// 若某文件同时声明多个类型,逐个收(本仓 20 侧当前每文件一型,但**不假设**)。</para>
        /// <para>⚠️ 正则解析的失败模式是**静默少收** ⇒ 由两层各自的「零命中即红」兜底:
        /// 收到 0 个类型 ⇒ 红;层 2 在装配里找不到某类型 ⇒ 红(<c>surfaceMismatch</c>)。</para>
        /// </summary>
        /// <param name="unityProjectRoot">工程根(含 <c>Assets/</c>)。</param>
        /// <returns>类型全名清单(去重、序数排序 —— 稳定序便于逐位比对)。</returns>
        public static List<string> EnumerateTypeFullNames(string unityProjectRoot)
        {
            var names = new List<string>();
            foreach (string rel in ScanDirsRelative)
            {
                string dir = Path.Combine(unityProjectRoot, rel);
                if (!Directory.Exists(dir)) continue;   // 由调用方的目录守卫单独报红
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    names.AddRange(ExtractTypeFullNames(File.ReadAllText(file)));
            }
            names.Sort(StringComparer.Ordinal);
            return names.Distinct(StringComparer.Ordinal).ToList();
        }

        /// <summary>单文件源码 → 类型全名清单(纯函数;供测试直驱,反空转)。</summary>
        /// <param name="source">C# 源文本。</param>
        /// <returns>该文件内 <c>public</c> 顶层类型的全名(无 namespace ⇒ 只有类型名)。</returns>
        public static List<string> ExtractTypeFullNames(string source)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(source)) return result;

            Match ns = Regex.Match(source, @"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline);
            string prefix = ns.Success ? ns.Groups[1].Value + "." : "";

            // ⚠️ **必须允许前导空白**(2026-10-26 首跑实测):本仓的类型声明都在
            //   `namespace X {` 块内、缩进 4 格。初版锚 `^public`(顶格)⇒ 20 侧收到 **0 个类型**,
            //   两层同时静默变空 —— 靠「零命中即红」两条兜底才暴露,否则就是满绿的空门。
            //
            // ⚠️ 因此本正则**不区分顶层与嵌套**:嵌套类型也会被收成 `namespace.OuterType`
            //   之外的全名。当前的失败模式是**可见的** —— 层 2 在装配里按全名找不到它,
            //   由 `MissingTypeNames` 报红(「清单解析错 / 编译未成功」),不会静默漏扫。
            //   若将来 20 侧真出现嵌套 DTO,层 1 的递归已覆盖其成员;此处只需把清单改准。
            MatchCollection types = Regex.Matches(
                source, @"^\s*public\s+(?:readonly\s+|sealed\s+|static\s+|abstract\s+|partial\s+)*" +
                        @"(?:struct|class|enum|interface)\s+(\w+)",
                RegexOptions.Multiline);

            foreach (Match m in types) result.Add(prefix + m.Groups[1].Value);
            return result;
        }

        /// <summary>扫描面前的目录守卫(目录缺失 = 红 —— 20 的实现面被搬走时门须同步改口径)。</summary>
        /// <param name="unityProjectRoot">工程根。</param>
        /// <returns>违例清单(空 = 两个目录都在)。</returns>
        public static List<string> CheckScanDirsExist(string unityProjectRoot)
        {
            var errs = new List<string>();
            foreach (string rel in ScanDirsRelative)
            {
                string dir = Path.Combine(unityProjectRoot, rel);
                if (!Directory.Exists(dir))
                    errs.Add($"[AC-20-15] 扫描目录不存在:{dir} —— 20 的实现落点被移?" +
                             "门须同步改口径,不得静默变绿");
            }
            return errs;
        }

        // ───────────────────────── 门链入口 ─────────────────────────

        /// <summary>门链入口 —— 双层全跑(供 <c>AssemblyGates.RunAll</c> 调用)。
        /// <para><b>为什么需要本入口</b>:AC-20-15 是 **BLOCKING** 级判据。
        /// 若本门只挂 `TestDriven`,它就落在「无强制点」档 —— 而
        /// `GateWiringManifestTest` 的封闭集判据明确禁止把 BLOCKING 判据判成 `TestDriven`
        /// (该表自己的规矩:「若某条升格为 BLOCKING,须先给它找强制点」)。
        /// ⇒ 接进 `RunAll`(菜单 / reload / 构建前三处共用)才是真强制点。</para>
        /// <para><b>与测试夹具的关系**:本入口只做「真实面」扫描;负面夹具(假 DTO / 假类型)
        /// 仍住测试装配,不在此调用 —— 门链不该依赖测试装配里的类型。</para>
        /// </summary>
        /// <param name="unityProjectRoot">工程根(<c>unity/</c>;相对路径按调用方 cwd 解析)。
        /// null / 空 ⇒ 用 <c>Directory.GetCurrentDirectory()</c>。</param>
        /// <returns>违例清单(空 = 两层都干净)。</returns>
        public static List<string> RunAll(string unityProjectRoot = null)
        {
            var errs = new List<string>();
            string root = string.IsNullOrEmpty(unityProjectRoot)
                ? Directory.GetCurrentDirectory()
                : unityProjectRoot;

            // ① 扫描面守卫:目录被搬走 ⇒ 红(不静默变绿)。
            errs.AddRange(CheckScanDirsExist(root));
            if (errs.Count > 0) return errs;   // 面都没了,后续扫描无意义

            // ② 按源目录枚举类型清单(两层共面)。
            List<string> typeNames = EnumerateTypeFullNames(root);
            if (typeNames.Count == 0)
            {
                errs.Add("[AC-20-15] 源目录枚举出 **零个类型** —— 扫描面失效,拒以空集冒充绿。");
                return errs;
            }

            // ③ 层 1:递归反射。类型从**已加载装配**解析(门链处在编辑器域,两侧装配均已加载)。
            int resolved = 0;
            foreach (string fullName in typeNames)
            {
                Type t = ResolveLoadedType(fullName);
                if (t == null) continue;
                resolved++;
                errs.AddRange(ScanTypes(t));
            }
            if (resolved == 0)
                errs.Add("[AC-20-15] 清单里的类型**一个都解析不到** —— 装配未加载或清单分派错," +
                         "拒以空集冒充绿。");

            // ④ 层 2:Cecil IL 扫两侧装配。
            string simDll = AssemblyGates.ScriptAssemblyPath("Sim");
            string contractsDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts");
            errs.AddRange(ScanIl(simDll, typeNames, out _));
            errs.AddRange(ScanIl(contractsDll, typeNames, out _));

            // ⑤ 两层共面:源目录声明的类型必须都能在编译产物里找到。
            errs.AddRange(MissingTypeNames(typeNames, simDll, contractsDll));
            return errs;
        }

        /// <summary>在**已加载**的装配里按全名解析类型(两侧都试;解析不到返回 null)。</summary>
        private static Type ResolveLoadedType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                Type t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        // ───────────────────────── 层 1 · 递归反射 ─────────────────────────

        /// <summary>层 1:递归扫描一个 DTO 根类型,返回全部违例(空 = 干净)。
        /// <para>递归规则见文件头。null 入口 = **红**(拒以空集冒充绿)。</para>
        /// </summary>
        /// <param name="dtoRoot">DTO 根类型(非 null)。</param>
        /// <returns>违例清单(每条点名**路径** —— 便于直接定位到字段)。</returns>
        public static IReadOnlyList<string> ScanTypes(Type dtoRoot)
        {
            var errs = new List<string>();
            if (dtoRoot == null)
            {
                errs.Add("[AC-20-15] dtoRoot == null —— 拒以空集冒充绿。");
                return errs;
            }

            var visited = new HashSet<Type>();
            Visit(dtoRoot, dtoRoot.Name, 0);
            return errs;

            void Visit(Type t, string path, int depth)
            {
                if (t == null) return;
                if (depth > MaxDepth)
                {
                    errs.Add($"[AC-20-15] 深度护栏触发于 {path}(depth > {MaxDepth})—— " +
                             "类型树异常深,拒绝静默截断;请拆 DTO 或上报。");
                    return;
                }
                if (t.IsByRef || t.IsPointer)
                {
                    Visit(t.GetElementType(), path + "&/*", depth + 1);
                    return;
                }
                if (t.IsArray)
                {
                    Visit(t.GetElementType(), path + "[]", depth + 1);
                    return;
                }

                // 泛型:展开全部实参(无论自身是否已访问 —— `List<A>` 与 `List<B>` 是不同实参)。
                if (t.IsGenericType)
                    foreach (Type arg in t.GetGenericArguments())
                        Visit(arg, path + "<" + arg.Name + ">", depth + 1);

                // 判据:类型本身即浮点。
                if (ForbiddenClrTypes.Contains(t))
                {
                    errs.Add($"[AC-20-15] {path} 的类型是 {t.FullName} —— " +
                             "20 的输出 DTO 禁浮点字段(门 A / ADR-006 定点边界;" +
                             "decimal 亦禁 —— 其运算非跨平台逐位确定)");
                    return;
                }

                if (!visited.Add(t)) return;       // 环 / 重复的唯一终止条件
                if (IsNonProjectType(t)) return;   // BCL / 引擎类型不展开自有成员(泛参已展开)

                // 基类链逐层(DeclaredOnly ⇒ 每层只扫自己声明的成员,基类私有字段也抓)。
                // ⚠️ **每层都要过 `IsNonProjectType`**(2026-10-26 首跑实测):初版只在入口判一次,
                //   基类链一旦走到 `System.Enum` / `System.ValueType` 就展开 BCL 自有成员 ——
                //   `IConvertible.ToSingle()` 这类返回 `float` 的 BCL 方法成了假违例源。
                //
                // ⚠️ **本 break 是「枚举不展开」这条属性的唯一承重守卫**(2026-10-26 证伪轮订正):
                //   原先这里还有一条 `if (t.IsEnum) return;` 在前,看似双保险 —— 实测它**不承重**:
                //   枚举自身声明的成员是常量(int)与 `value__`(int)、`HasFlag`/`CompareTo`
                //   (参数是枚举自身),**无一返回 float**;真正的浮点源是基类 `System.Enum` 的
                //   `IConvertible.ToSingle/ToDouble/ToDecimal`,而那已被本行的 break 拦住。
                //   ⇒ 两条守卫只承担**一个**属性 ⇒ 拆任一条都不可观测(M14 两轮全绿逃逸的真相)。
                //   **已删冗余的 `IsEnum` 早返回**,让这条属性只有一个承重守卫,判据才可证伪。
                for (Type cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (IsNonProjectType(cur)) break;   // 到了 BCL / 引擎层,其上不再有本项目的字段
                    ScanMembers(cur, path + ":" + cur.Name, depth);
                }

                void ScanMembers(Type declaring, string memberPath, int d)
                {
                    const BindingFlags flags = BindingFlags.DeclaredOnly |
                                               BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static;

                    foreach (FieldInfo f in declaring.GetFields(flags))
                        Visit(f.FieldType, memberPath + "." + f.Name, d + 1);

                    foreach (PropertyInfo p in declaring.GetProperties(flags))
                        Visit(p.PropertyType, memberPath + "." + p.Name, d + 1);

                    foreach (MethodInfo m in declaring.GetMethods(flags))
                    {
                        if (m.IsSpecialName) continue;   // 属性 / 事件访问器已由上面覆盖
                        if (IsAllowedFloatMember(declaring.FullName, m.Name)) continue;   // Fix.ToFloat 唯一出口
                        Visit(m.ReturnType, memberPath + "." + m.Name + "()", d + 1);
                        foreach (ParameterInfo pi in m.GetParameters())
                            Visit(pi.ParameterType, memberPath + "." + m.Name + "(" + pi.Name + ")", d + 1);
                    }
                }
            }
        }

        /// <summary>该成员是否在**具名浮点出口白名单**内(见 <see cref="AllowedFloatMembers"/>)。
        /// <para><c>declaring</c> 用 <see cref="Type.FullName"/> 而非 <c>AssemblyQualifiedName</c>
        /// —— 后者带版本号,跨装配对不齐。</para></summary>
        private static bool IsAllowedFloatMember(string declaringFullName, string memberName)
            => declaringFullName != null
               && AllowedFloatMembers.Contains(declaringFullName + "::" + memberName);

        /// <summary>BCL / 引擎类型判定 —— 只用于「不展开其自有成员」,不用于终止递归
        /// (泛参在调用前已展开)。</summary>
        private static bool IsNonProjectType(Type t)
        {
            string ns = t.Namespace;
            if (ns == null) return false;
            return ns == "System"
                || ns.StartsWith("System.", StringComparison.Ordinal)
                || ns == "UnityEngine"
                || ns.StartsWith("UnityEngine.", StringComparison.Ordinal)
                || ns == "UnityEditor"
                || ns.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        // ───────────────────────── 层 2 · Cecil IL ─────────────────────────

        /// <summary>层 2:对一个已编译装配做 IL 扫描,只判**清单内**的类型
        /// (<see cref="EnumerateTypeFullNames"/> 的结果 —— 与层 1 同一份面)。
        /// <para>扫四面:① 方法体指令(禁四个浮点 opcode)② 局部变量类型
        /// ③ 字段类型 ④ 方法签名(返回 / 参数)。</para>
        /// <para>产物缺失 / 清单为空 / 清单内某类型在装配里找不到 ⇒ **红**
        /// (拒以空集冒充绿 —— 面被搬走时门须同步改口径,而不是悄悄变绿)。</para>
        /// </summary>
        /// <param name="dllPath">装配路径(通常 = <c>Library/ScriptAssemblies/&lt;name&gt;.dll</c>)。</param>
        /// <param name="typeFullNames">要扫的类型全名集(不在本装配内的忽略;由调用方用
        /// <see cref="MissingTypeNames"/> 做跨装配差集检查)。</param>
        /// <param name="scannedTypeCount">实际扫到的类型数(形状守卫:为 0 说明扫描面空了)。</param>
        /// <returns>违例清单(空 = 干净)。</returns>
        public static List<string> ScanIl(string dllPath, IReadOnlyCollection<string> typeFullNames,
                                          out int scannedTypeCount)
        {
            scannedTypeCount = 0;
            var errs = new List<string>();

            if (!File.Exists(dllPath))
            {
                errs.Add($"[AC-20-15] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿。");
                return errs;
            }
            if (typeFullNames == null || typeFullNames.Count == 0)
            {
                errs.Add("[AC-20-15] 类型清单为空 —— 拒以空集冒充绿(关了就永远绿)。");
                return errs;
            }

            var wanted = new HashSet<string>(typeFullNames, StringComparer.Ordinal);

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // 与 EcozoneIntegerGates / AssemblyGates 同款:Deferred 才不触 Resolve。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                foreach (TypeDefinition type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    if (!wanted.Contains(type.FullName)) continue;
                    scannedTypeCount++;

                    // ③ 字段类型面
                    foreach (FieldDefinition f in type.Fields)
                        if (IsFloatTypeRef(f.FieldType))
                            errs.Add($"[AC-20-15·IL] 字段 {type.FullName}::{f.Name} 的类型是 " +
                                     $"{f.FieldType.FullName} —— 禁浮点字段(门 A)");

                    foreach (MethodDefinition method in type.Methods)
                    {
                        // 具名浮点出口白名单(与层 1 同一份 `AllowedFloatMembers`:
                        // `Fix.ToFloat` 的方法体里必然有 `conv.r4`,不豁免则任何含 `Fix` 的
                        // 装配恒红 —— 两层共用单一真源,不得各写一份豁免)。
                        if (IsAllowedFloatMember(type.FullName, method.Name)) continue;

                        // ④ 签名面
                        if (IsFloatTypeRef(method.ReturnType))
                            errs.Add($"[AC-20-15·IL] 方法 {type.FullName}::{method.Name} 的返回类型是 " +
                                     $"{method.ReturnType.FullName} —— 禁浮点签名(门 A)");
                        foreach (ParameterDefinition pi in method.Parameters)
                            if (IsFloatTypeRef(pi.ParameterType))
                                errs.Add($"[AC-20-15·IL] 方法 {type.FullName}::{method.Name} 的参数 " +
                                         $"「{pi.Name}」是 {pi.ParameterType.FullName} —— 禁浮点签名(门 A)");

                        if (!method.HasBody) continue;

                        // ② 局部变量面
                        foreach (VariableDefinition v in method.Body.Variables)
                            if (IsFloatTypeRef(v.VariableType))
                                errs.Add($"[AC-20-15·IL] 方法 {type.FullName}::{method.Name} 的局部变量 " +
                                         $"V_{v.Index} 是 {v.VariableType.FullName} —— 禁浮点局部(门 A)");

                        // ① 指令面
                        foreach (Instruction ins in method.Body.Instructions)
                            if (ForbiddenFloatOpcodes.Contains(ins.OpCode))
                                errs.Add($"[AC-20-15·IL] 方法 {type.FullName}::{method.Name} 出现浮点指令 " +
                                         $"「{ins.OpCode}」(IL_{ins.Offset:X4})—— 门 A 禁浮点算术;" +
                                         "非字面量浮点(常量折叠 / 库返回值转浮点)只有 IL 面抓得到");
                    }
                }
            }

            if (scannedTypeCount == 0)
                errs.Add($"[AC-20-15·IL] 「{Path.GetFileName(dllPath)}」内**零类型命中** —— " +
                         "该装配不含清单中任何类型(清单分派错?),拒以空集冒充绿");

            return errs;
        }

        /// <summary>清单里有、但两个装配里都找不到的类型名(空 = 面一致)。
        /// <para>⚠️ 这是**两层共面**的守卫:源目录正则收到但编译产物没有 ⇒ 说明清单解析错
        /// 或编译未成功;两者都必须红(否则层 2 会「扫了个不存在的面」而静默变绿)。</para>
        /// </summary>
        /// <param name="typeFullNames">源目录枚举出的类型清单。</param>
        /// <param name="dllPaths">待查的装配路径集。</param>
        /// <returns>缺失类型全名清单(空 = 全部找到)。</returns>
        public static List<string> MissingTypeNames(IReadOnlyCollection<string> typeFullNames,
                                                    params string[] dllPaths)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            var errs = new List<string>();

            foreach (string dllPath in dllPaths)
            {
                if (!File.Exists(dllPath))
                {
                    errs.Add($"[AC-20-15] 编译产物缺失「{dllPath}」—— 跨装配差集无法求值");
                    continue;
                }
                using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
                {
                    ReadingMode = ReadingMode.Deferred,
                    InMemory = true,
                }))
                    foreach (TypeDefinition t in AssemblyGates.AllTypes(asm.MainModule))
                        found.Add(t.FullName);
            }

            foreach (string name in typeFullNames)
                if (!found.Contains(name))
                    errs.Add($"[AC-20-15] 源目录声明了 `{name}` 但编译产物里找不到 —— " +
                             "清单解析错 / 编译未成功;两层共面的前提不成立");
            return errs;
        }

        /// <summary>TypeReference 是否浮点(含 by-ref / 指针 / 数组元素 / 泛型实参递归)。</summary>
        private static bool IsFloatTypeRef(TypeReference tr)
        {
            if (tr == null) return false;

            var g = tr as GenericInstanceType;
            if (g != null)
                foreach (TypeReference arg in g.GenericArguments)
                    if (IsFloatTypeRef(arg)) return true;

            var byRef = tr as ByReferenceType;
            if (byRef != null) return IsFloatTypeRef(byRef.ElementType);

            var ptr = tr as PointerType;
            if (ptr != null) return IsFloatTypeRef(ptr.ElementType);

            var arr = tr as ArrayType;
            if (arr != null) return IsFloatTypeRef(arr.ElementType);

            return ForbiddenFloatFullNames.Contains(tr.FullName);
        }
    }
}
