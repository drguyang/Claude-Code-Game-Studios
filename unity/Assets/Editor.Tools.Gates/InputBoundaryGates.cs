// Story 006 · 3 意图边界的构建期/编辑期门(AC-3-A6 · A7 · B3)
//
// 权威来源:
//   AC-3-A6(BLOCKING)—— asmdef 引用集白名单:输入程序集不引用任何声明 IEventSink / SimEvent
//     的程序集(构建失败);3 只产意图闭集 {InteractIntent, EmergencyIntent,
//     FocusNavigationIntent, EmergencyReading}(白名单外新增类型须过本门)。
//   AC-3-A7(BLOCKING)—— 载荷可达类型闭包**递归**零 float/double(与 PresentationDtoGuard 同构;
//     B3 只看顶层抓不到结构体字段 —— 必须递归进字段/数组元素/泛型参数/基类链)。
//   AC-3-B3(BLOCKING)—— 判定结果全整数:int/Fix/bool/枚举/long(tick 计数);float 经别名
//     (System.Single)同拒;内嵌结构体字段同递归;3 侧无 `FixParse.Parse(`(判定由 10 直接构造,
//     不经字符串)。SimEvent 由 10 直接构造,不经 Parse(string)。
//   AC-3-B4(聚合,实现 = Gameplay.Input/EmergencyAggregator.cs;B4 的「恰一条/零条」断言
//     由测试侧在 3 边界断言,本文件不重复)。
//   ADR-011 §二 / Amendment A / Amendment B · ADR-006(整数域纪律)·
//     ADR-009 §七(意图事件三段式第一段)· ADR-024 §①(载荷真源)。
//   I2「无外部状态意图源」机械判据 = 与 AC-A6 同构(引用集 + 类型持有面)。
//
// 落点:Editor.Tools.Gates(编辑期门,不进构建;AD-022 §① 同构)。scan 键单一出处:
//   输入程序集名 Gameplay.Input(AssemblyGates.Manifest 已登记)、交物闭集 = 四意图
//   类型(本文件常量,不用字面量复制于测试)。
//
// ⚠️ 门与测试的分工:
//   · 门的纯判定式(CheckInputReferenceSet / CheckPayloadClosure / CheckEmergencyReadingFields /
//     CheckInputSourceText)都是**纯函数**,负例夹具直接喂合成输入 —— 测试装配内
//     IL 负例(如音频 IlScanNegativeFixture)本故事不复制:内容物(意图 struct)全整数,
//     负例经反射 / 合成文本注入即可证真(真 IL 边界已在 b5 先例兑现)。
//   · 「IEventSink.Append 不可达」= A6 的一侧(源文本):任何 Gameplay.Input 源树
//     .cs 出现 `IEventSink` / `.Append(` 以 SimEvent 为对象 ⇒ 红(调用点可达性退化面 =
//     出现 token 即红,偏安全)。
//   · A7 扫描根 = Sim.Contracts 全部 *Payload struct(register 真源 34 支)+ 四意图类型。
//
// 假绿防护同 b5 三处:scriptCompilationFailed 拒扫 / asmdef·产物缺失红 / 扫描键 0 命中 WARN。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using DaYiJingCheng.Sim.Contracts;   // Fix(typeof(Fix) 叶子白名单;B3 允许定点叶子)

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>3 输入边界的构建期门(AC-3-A6 / A7 / B3;见文件头)。</summary>
    public static class InputBoundaryGates
    {
        // ── 扫描键(单一出处;测试引用本常量,不复制字面量)──
        /// <summary>扫描目标:输入程序集名(ADR-025 §① 登记;AssemblyGates.Manifest 成员)。</summary>
        public const string InputAssemblyName = "Gameplay.Input";

        /// <summary>交出物闭集(AC-3-A6「3 只产意图」原文点名的四件 + AC-3-B4
        /// 「聚合为**一条**上行」的产物形状 —— **两条 BLOCKING AC 各自的交出物**,
        /// 分住两面:前四件是 A6 点名的意图/读数,第五件是 B4 的上行整数记录
        /// (10 在其边界映射为 <c>EmergencyAttemptPayload</c> 并补 Method / ActorId)。
        /// 全局限定名 —— 反射 / asmdef 双面共用;漏登 B4 那一件 = 上行形状逃出闭集断言面
        /// (第二真源)。</summary>
        public static readonly string[] DeliveredIntentTypes =
        {
            "DaYiJingCheng.Gameplay.Input.Intents.InteractIntent",          // A6
            "DaYiJingCheng.Gameplay.Input.Intents.EmergencyIntent",         // A6
            "DaYiJingCheng.Gameplay.Input.Intents.FocusNavigationIntent",   // A6
            "DaYiJingCheng.Gameplay.Input.Intents.EmergencyReading",        // A6
            "DaYiJingCheng.Gameplay.Input.Intents.AggregatedEmergency",     // B4
        };

        /// <summary>声明 IEventSink / SimEvent 的程序集(A6 禁引集合)。
        /// ⚠️ 谓词按「声明面」取装配:SimEvent.cs / Abstractions.cs(IEventSink 等七抽象点)
        /// 现住 **Sim.Contracts**(b1b 落位);Sim 引用 IEventSink 但它"使用"而非"声明"。
        /// GDD 措辞「声明这些类型的程序集」⇒ 集合 = {Sim.Contracts} 单元素;为防未来
        /// 迁移(抽象点搬 Sim)与实现面漂移,同时禁 Sim(3 引用 Sim = 写能力传播面)。</summary>
        public static readonly string[] InputForbiddenAssemblies = { "Sim", "Sim.Contracts", "Sim.Codec" };

        /// <summary>A6 引用集白名单外的工程内允许项 —— 空集开头;若日后再有工程内装配
        /// 需被 3 引用,须先过 ADR-025 装配轮再放行(与 b5 登记集同纪律)。</summary>
        public static readonly string[] InputRefRegistered = { };

        /// <summary>B3 断言允许的字段叶子类型(int / long / bool / 枚举 / Fix / int[] ——
        /// tick 计数 / edge 沿 / 枚举序号 / Q16.16 定点)。</summary>
        public static readonly Type[] EmergencyReadingAllowedLeaves =
        {
            typeof(int), typeof(long), typeof(bool), typeof(Fix),
        };

        /// <summary>B3「3 侧无 FixParse.Parse(」—— 扫描键 = 禁调标注(单一出处)。</summary>
        public const string FixParseParseMarker = "FixParse.Parse(";

        // ── A6:引用集白名单(declared ∪ compiled,与 b5 同款并集送检)──
        // 判序:黑名单(无条件红)→ 引擎白名单 → BCL → 登记集 → 其余 = 漂移红(要求先回写
        // ADR-025 再放行)。「声明 IEventSink/SimEvent 的程序集」= InputForbiddenAssemblies。
        /// <summary>对输入程序集的实际引用集送检(纯函数;负例夹具直接喂合成引用集)。</summary>
        public static List<string> CheckInputReferenceSet(IEnumerable<string> refs)
        {
            var errs = new List<string>();
            var actual = (refs ?? Enumerable.Empty<string>())
                         .Where(r => !string.IsNullOrEmpty(r))
                         .Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();
            var actualText = string.Join(", ", actual);
            var registeredText = string.Join(", ", InputRefRegistered);

            foreach (var r in actual)
            {
                if (InputForbiddenAssemblies.Contains(r))
                {
                    errs.Add($"[A6] {InputAssemblyName} 引用「{r}」—— 声明 IEventSink / SimEvent " +
                             "的程序集禁引(AC-3-A6 构建失败):3 不持有写流 / 事件能力。" +
                             $"实际集=[{actualText}];ADR-025 登记集=[{registeredText}]。");
                    continue;
                }
                if (AssemblyGates.IsEngineRef(r) || AssemblyGates.IsBclRef(r)) continue;
                if (InputRefRegistered.Contains(r)) continue;
                errs.Add($"[A6] {InputAssemblyName} 引用「{r}」∉ 登记集 ∪ 引擎白名单 ∪ BCL —— " +
                         "引用集漂移(AC-3-A6)。新增引用须先过 ADR-025 装配轮再放行。" +
                         $"实际集=[{actualText}];ADR-025 登记集=[{registeredText}]。");
            }
            return errs;
        }

        /// <summary>实际引用集(end-to-end 入口):asmdef 声明面 ∪ 编译产物引用表(并集 —
        /// asmdef 缓存/解析失败侧由编译面兜);asmdef 缺失 / 产物缺失 = 红(拒以空集冒充绿)。</summary>
        public static List<string> CheckInputAssemblyReferences()
        {
            var errs = new List<string>();
            if (EditorUtility.scriptCompilationFailed)
            {
                errs.Add("[A6] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }

            var asmdefPath = $"Assets/{InputAssemblyName}/{InputAssemblyName}.asmdef";
            var dllBase = AssemblyGates.ScriptAssemblyPath(InputAssemblyName);
            var declared = AssemblyGates.ReadDeclaredReferences(asmdefPath);
            if (File.Exists(asmdefPath) && declared.Count == 0 &&
                !Regex.Match(File.ReadAllText(asmdefPath), "\"references\"").Success)
                errs.Add($"[A6] 「{asmdefPath}」无 references 键 —— 解析面异常,拒以空集冒充绿。");
            var compiled = File.Exists(dllBase)
                ? AssemblyGates.ReadCompiledReferenceNames(dllBase)
                : new List<string> { "<产物缺失>" };   // ① 就产物缺失已另行报红,这里不让引用面静默为空

            var refErrs = CheckInputReferenceSet(declared.Concat(compiled));
            errs.AddRange(refErrs);
            return errs;
        }

        // ── A6 交出物闭集:Intents 子命名空间的公开类型 ⊆ 四意图(白名单外新增 = 红)──
        // 口径:交出物 = Intents 子命名空间的公开类型(「3 只产意图」的输出边界);
        // 根命名空间的公开类型 = 输入系统的**自身基础设施 API**(InputService /
        // BindingsStore / SchemaHash —— Story 001/003/004/005 逐故事建成,非交出物,
        // 不进本闭集面)。意图形态的「影类型」若被塞进根命名空间,由源文本层
        // CheckInputSourceText 的 SimEvent 禁名 + 本门的存在性断言兜。
        private static IEnumerable<Type> PublicInputRootTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == InputAssemblyName);
            if (asm == null)
            {
                yield break;   // 装配缺失 → 由引用集/源文本面各自报;此处不重复红
            }
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
            foreach (var t in types)
            {
                if (!t.IsPublic && !t.IsNestedPublic) continue;
                if (string.IsNullOrEmpty(t.Namespace)) continue;
                if (t.Namespace == "DaYiJingCheng.Gameplay.Input.Intents")
                    yield return t;
            }
        }

        /// <summary>交出物闭集断言:Intents 子命名空间公开类型 ⊆ 四意图;四件全存在。</summary>
        public static List<string> CheckDeliveredIntentClosure()
        {
            var errs = new List<string>();
            var delivered = DeliveredIntentTypes.ToHashSet();
            foreach (var t in PublicInputRootTypes())
            {
                if (delivered.Contains(t.FullName)) continue;
                errs.Add($"[A6] {InputAssemblyName} Intents 子命名空间新增公开类型「{t.FullName}」" +
                         "∉ 交出物闭集 —— AC-3-A6「3 只产意图」白名单外新增须过本门。");
            }
            var found = PublicInputRootTypes().Select(t => t.FullName).ToHashSet();
            foreach (var expected in DeliveredIntentTypes)
                if (!found.Contains(expected))
                    errs.Add($"[A6] 交出物「{expected}」不存在 —— 四意图缺失,闭集断言对空集 = " +
                             "假绿(AC-3-A6)。");
            return errs;
        }

        // ── A7:载荷可达闭包递归零浮点(与 PresentationDtoGuard 同构)──
        // 谓词 = float/double 叶子(含别名 System.Single 系);array/byref/泛型实参/基类链全
        // 递归;接口字段 / 多态载荷 = 容器字段类型为接口时,接口自身不展开(引擎 / BCL 面),
        // 但其实现类型不经本键可达 —— 与 DtoGuard 同当:容器字段类型本身非 float/double
        // 即绿(三层纪律:① 预案经注释声明,② 深度护栏,③ visited = 唯一终止)。
        //
        // ⚠️ **只扫数据面(字段 / 属性),不扫方法返回与 ctor 参数**(2026-09-26 评审修复)。
        //   理由 = 「可达类型闭包」指载荷**携带**的数据类型,不是类型上挂的行为。方法返回
        //   值会把库侧的合法出口扫进来:`Fix.ToFloat():float` 是 ADR-006 §五钉死的**唯一**
        //   浮点出口(ADR-025 QQ-03 另立构建期调用点白名单守住它),把它判红 = 把「受控的
        //   唯一出口」误判成「载荷携带浮点」,真树恒红且不可修。
        //   ctor 参数同理:它是**输入**不是携带物;若某类型把 float 存进字段,字段面已红。
        //   故事 QA 的负例面(嵌套 struct / List<T> / 数组元素 / 基类字段 / 接口字段)
        //   **全部是字段面**,不受此收窄影响。
        /// <summary>递归扫描类型树的叶子上 float/double(纯函数;负例夹具直接喂类型)。
        /// 数据面 = 字段 + 属性(不含方法返回值 / ctor 参数,见上方 ⚠️)。</summary>
        public static List<string> CheckPayloadClosure(Type root)
        {
            var errs = new List<string>();
            if (root == null)
            {
                errs.Add("[A7] 扫描根 null —— 拒以空集冒充绿(AC-3-A7)。");
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
                    errs.Add($"[A7] 深度护栏触发于 {path}(depth > 64)—— 类型树异常深," +
                             "拒绝静默截断;请拆载荷或上报(AC-3-A7)。");
                    return;
                }
                if (t.IsArray || t.IsByRef || t.IsPointer)
                {
                    Visit(t.GetElementType(), path + "[]", depth + 1);
                    return;
                }
                if (!visited.Add(t)) return;

                if (IsFloatLeaf(t))
                {
                    errs.Add($"[A7] 载荷可达闭包出现浮点叶子「{t.FullName}」(路径 {path})—— " +
                             "SimEvent 载荷零 float/double(AC-3-A7递归,与 PresentationDtoGuard 同构;" +
                             "float 经别名 System.Single 同拒)。");
                    return;
                }

                if (!ShouldExpandMembers(t))
                {
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
                    if (!ShouldExpandMembers(cur)) break;
                    foreach (var f in cur.GetFields(fb))
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                    foreach (var p in cur.GetProperties(fb))
                        Visit(p.PropertyType, path + "." + p.Name, depth + 1);
                }
            }
        }

        /// <summary>A7 浮点叶子判据(float / double;别名 Single 系含)。</summary>
        public static bool IsFloatLeaf(Type t)
            => t == typeof(float) || t == typeof(double) ||
               t.FullName == "System.Single" || t.FullName == "System.Double";

        /// <summary>A7 全扫描根:Sim.Contracts 的 *Payload struct(真源 34 支)+ 四意图。
        /// 返回 (红错, 命中数)—— 命中 0 = WARN(根空 = 平凡成立,由调用方转 WARN)。</summary>
        public static List<string> CheckAllPayloadClosures(out int roots)
        {
            roots = 0;
            var errs = new List<string>();
            var rootsSet = new HashSet<Type>();

            var contracts = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Sim.Contracts");
            if (contracts == null)
            {
                errs.Add("[A7] Sim.Contracts 装配未加载 —— 扫描根不可得,拒以空集冒充绿(AC-3-A7)。");
                return errs;
            }
            Type[] types;
            try { types = contracts.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            foreach (var t in types)
            {
                if (t.IsNested || t.IsAbstract) continue;
                if (!t.Name.EndsWith("Payload", StringComparison.Ordinal)) continue;
                if (t == typeof(PayloadRef)) continue;      // 载荷引用是 header 场,非载荷本体
                rootsSet.Add(t);
            }

            var inputAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == InputAssemblyName);
            if (inputAsm == null)
            {
                errs.Add($"[A7] {InputAssemblyName} 装配未加载 —— 四意图扫描根不可得,拒以空集" +
                         "冒充绿(AC-3-A7)。");
                return errs;
            }
            try
            {
                foreach (var t in inputAsm.GetTypes())
                {
                    if (!t.IsPublic && !t.IsNestedPublic) continue;
                    if (string.IsNullOrEmpty(t.Namespace)) continue;
                    if (!t.Namespace.StartsWith("DaYiJingCheng.Gameplay.Input.Intents", StringComparison.Ordinal))
                        continue;
                    rootsSet.Add(t);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var t in ex.Types)
                {
                    if (t == null || (!t.IsPublic && !t.IsNestedPublic)) continue;
                    if (t.Namespace == null) continue;
                    if (!t.Namespace.StartsWith("DaYiJingCheng.Gameplay.Input.Intents", StringComparison.Ordinal))
                        continue;
                    rootsSet.Add(t);
                }
            }

            roots = rootsSet.Count;
            foreach (var root in rootsSet.OrderBy(r => r.FullName, StringComparer.Ordinal))
                errs.AddRange(CheckPayloadClosure(root));
            return errs;
        }

        /// <summary>本门全部判定一次跑完(菜单 / 测试共用):A6 引用集 + A6 闭集 + A7 全根
        /// 闭包 + B3 源文本。返回红错;roots = A7 扫描根数(0 = WARN 由调用方处理)。
        /// ⚠️ B3 的 <see cref="CheckReadingFieldLeaves(Type)"/> 不入 RunAll —— 本装配
        /// (Editor.Tools.Gates)**不引用 Gameplay.Input**,<c>typeof(EmergencyReading)</c> 在此不可用;
        /// 该断言由测试装配(引用 Gameplay.Input)驱动,把 <c>typeof(EmergencyReading)</c> 喂给
        /// 纯函数(见 intent_boundary_test.cs)。</summary>
        public static List<string> RunAll(out int roots)
        {
            var errs = new List<string>();
            errs.AddRange(CheckInputAssemblyReferences());     // A6 引用集
            errs.AddRange(CheckDeliveredIntentClosure());      // A6 交出物闭集
            errs.AddRange(CheckAllPayloadClosures(out roots)); // A7 全载荷闭包
            errs.AddRange(CheckInputSourceText());             // B3 源文本 + SimEvent 禁名
            return errs;
        }

        // ── B3:EmergencyReading 字段类型断言 + 3 侧源文本禁调 ──
        /// <summary>断言一个类型的所有字段叶子 ∈ {int,long,bool,枚举,Fix} ∪ int[](递归进
        /// 内嵌结构体字段 —— 承 A7 纪律)。纯函数;负例夹具直接喂 <c>float magnitude</c> 类型。</summary>
        public static List<string> CheckReadingFieldLeaves(Type root)
        {
            var errs = new List<string>();
            if (root == null)
            {
                errs.Add("[B3] 扫描根 null —— 拒以空集冒充绿(AC-3-B3)。");
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
                    errs.Add($"[B3] 深度护栏触发于 {path}(depth > 64)—— AC-3-B3。");
                    return;
                }
                if (t.IsArray)
                {
                    Visit(t.GetElementType(), path + "[]", depth + 1);
                    return;
                }
                if (t.IsByRef || t.IsPointer)
                {
                    Visit(t.GetElementType(), path, depth + 1);
                    return;
                }
                if (!visited.Add(t)) return;
                if (IsFloatLeaf(t))
                {
                    errs.Add($"[B3] 判定读数字段叶子出现浮点「{t.FullName}」(路径 {path})—— " +
                             "全整数(EmergencyReading 字段 ∈ int/Fix/bool/枚举/long;AC-3-B3;" +
                             "float 经别名 System.Single 同拒)。");
                    return;
                }
                if (IsAllowedReadingLeaf(t)) return;   // int/long/bool/枚举/Fix = 合格叶子
                // 其余类型(结构体 / 类)→ 递归进字段
                const BindingFlags fb = BindingFlags.DeclaredOnly |
                                         BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    foreach (var f in cur.GetFields(fb))
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                }
            }
        }

        /// <summary>B3 合格叶子:int / long / bool / 枚举 / Fix(tick 计数 · 沿计数 · 序数 · Q16.16)。</summary>
        public static bool IsAllowedReadingLeaf(Type t)
            => EmergencyReadingAllowedLeaves.Contains(t) || t.IsEnum;

        /// <summary>B3 的调用点扫描:Gameplay.Input 源树出现 <c>FixParse.Parse(</c> = 红
        /// (判定结果由 10 直接构造,不经字符串;ADR-011 Amendment A / AC-3-B3)。</summary>
        public static List<string> CheckInputSourceText()
        {
            var errs = new List<string>();
            const string root = "Assets/" + InputAssemblyName;
            if (!Directory.Exists(root))
            {
                errs.Add($"[B3] 源扫描面缺失「{root}」—— 拒以空集冒充绿(AC-3-B3)。");
                return errs;
            }
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var src = File.ReadAllText(f);
                var text = AssemblyGates.StripCommentsPreserveStrings(src);
                if (text.IndexOf(FixParseParseMarker, StringComparison.Ordinal) >= 0)
                    errs.Add($"[B3] {f} 出现「{FixParseParseMarker}」—— 3 侧判定结果构造路径" +
                             "禁经字符串(ADR-011 Amendment A / AC-3-B3;判定由 10 直接构造)。");
                var simEventHit = Regex.Matches(text, @"\bSimEvent\b");
                if (simEventHit.Count > 0)
                    errs.Add($"[B3] {f} 源文本出现「SimEvent」—— 3 零 SimEvent(AC-3-A6;" +
                             "注释剥离后仍命中 = 代码 / 字符串 / nameof 面)。");
            }
            return errs;
        }

        // 展开资格与 PresentationDtoGuard 同款(引擎 / BCL 命名空间跳过自有成员,
        // 泛型实参照走;非引擎类型永不被当叶子跳过)。
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
#endif