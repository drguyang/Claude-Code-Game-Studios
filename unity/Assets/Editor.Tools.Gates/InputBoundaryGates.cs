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
//   · 「IEventSink / SimEvent 禁名」= A6 的零事件面(源文本):任何 Gameplay.Input 源树
//     .cs 出现任一禁名 token ⇒ 红(注释剥离后;偏安全,非可达性精确分析)。
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
using Mono.Cecil;                          // B3 IL 面(构建期强制点 · 2026-09-26 S1)
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
        /// tick 计数 / edge 沿 / 枚举序号 / Q16.16 定点)。
        /// ⚠️ 2026-09-26(评审 S8):本白名单是**闭合**的 —— 无符号窄化类型
        /// (`byte`/`short`/`uint`/`ulong`)、`char`、`Nullable&lt;T&gt;` 一律**不在**内。
        /// B3 原文只列 int/Fix/bool/枚举/long,收窄(见 CheckReadingFieldLeaves ⚠️)后
        /// 「不合格即红」才真正成立 ⇒ 日后给读数加 `uint tick` 之类会撞上一条红。
        /// 新增叶子类型须**先回写 AC-3-B3 口径**,不得在实现侧单方面放宽。</summary>
        public static readonly Type[] EmergencyReadingAllowedLeaves =
        {
            typeof(int), typeof(long), typeof(bool), typeof(Fix),
        };

        /// <summary>B3 的 IL 面(构建期强制点)按**全名**认的合格叶子 —— 与上面的
        /// <see cref="EmergencyReadingAllowedLeaves"/> 同一集合的第二形态。
        /// ⚠️ 枚举在 IL 面上**不逐个登记**:判据是「本程序集内定义的类型递归展开后
        /// 落到这几个基元之一」—— 枚举展开得到其基元(int)⇒ 天然合格。故此处只列
        /// 基元 + <c>Fix</c>,不随枚举表增长(枚举表是数据不是判据)。</summary>
        public static readonly string[] ReadingAllowedLeafFullNames =
        {
            "System.Int32", "System.Int64", "System.Boolean",
            "DaYiJingCheng.Sim.Contracts.Fix",
        };

        /// <summary>B3 IL 面的浮点全名(别名同拒,承 reflection 面 IsFloatLeaf)。</summary>
        public static readonly string[] ReadingFloatFullNames =
        {
            "System.Single", "System.Double",
        };

        /// <summary>B3「3 侧无 FixParse.Parse(」—— 扫描键 = 禁调标注(单一出处)。</summary>
        public const string FixParseParseMarker = "FixParse.Parse(";

        /// <summary>B3 / A6 源文本禁名(单一出处;测试不复写字面量)。3 零事件面 ——
        /// 构造事件的写入通道(IEventSink)与事件本体(SimEvent)两头都在名单上。
        /// ⚠️ 2026-09-26 评审 G2:原只扫 <c>SimEvent</c> 一个 token,而文件头宣称
        /// 「出现 IEventSink / .Append( 以 SimEvent 为对象 ⇒ 红」—— 头与实现不一致,
        /// 且 <c>s.Append(PayloadRef.Of(7))</c> 这类**不出现 SimEvent 字面**的写法
        /// (故事 QA 负例的字面场景)会全绿通过。补齐两 token 后两头都断。</summary>
        public static readonly string[] InputForbiddenSourceTokens = { "IEventSink", "SimEvent" };

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
            if (!File.Exists(asmdefPath))
            {
                errs.Add($"[A6] asmdef 缺失「{asmdefPath}」—— 扫描面不存在,拒以空集冒充绿" +
                         "(与 b5 的 asmdef 缺失专属红行同口径;评审 S10)。");
                return errs;
            }
            var dllBase = AssemblyGates.ScriptAssemblyPath(InputAssemblyName);
            var declared = AssemblyGates.ReadDeclaredReferences(asmdefPath);
            if (declared.Count == 0 &&
                !Regex.Match(File.ReadAllText(asmdefPath), "\"references\"").Success)
                errs.Add($"[A6] 「{asmdefPath}」无 references 键 —— 解析面异常,拒以空集冒充绿。");
            if (!File.Exists(dllBase))
            {
                // ⚠️ 2026-09-26(评审 S10):原式塞一个「<产物缺失>」哨兵进引用集,靠
                // 漂移红分支兜底 —— 但那条红读起来像「引用集漂移」,实则是**编译产物不存在**,
                // 且注释所称「已另行报红」全装配 grep 确认**不存在**。改为专属红行 + 早退。
                errs.Add($"[A6] 编译产物缺失「{dllBase}」—— 拒扫(读不到引用面 = 假绿);" +
                         "确认本装配已编译(Editor.Tools.Gates 不引 Gameplay.Input," +
                         "故本门须读其产物元数据)。");
                return errs;
            }
            var compiled = AssemblyGates.ReadCompiledReferenceNames(dllBase);

            errs.AddRange(CheckInputReferenceSet(declared.Concat(compiled)));
            return errs;
        }

        // ── A6 引用集**传递闭包**(间接引用;故事 QA 边界例「加一层 shim ⇒ 必红」)──
        // ⚠️ **2026-09-26 评审 G3**:上面的 CheckInputReferenceSet 只看**直接**引用集,
        //   于是 `Gameplay.Input → Some.Shim → Sim.Contracts` 全绿 —— 而 AC-3-A6 原文是
        //   「不引用任何声明 IEventSink / SimEvent 的程序集」,**间接引用同样算引用**。
        //   缺的不是断言而是能力,故本节补一条真正的传递闭包游走(工程内装配图 = 读全树
        //   asmdef 的 name → references 建边;引擎 / BCL 面是叶,图里没有边 = 无穿透)。
        //   纯函数 Predicate:负例夹具喂合成图,真树喂真图。
        /// <summary>工程内装配引用图(读全 Assets 树 asmdef;同名后者覆盖前者 = 重复即歧义,
        /// 以 Ordinal 序最后一个为准并在漂移红里如实显示)。引擎 / BCL 不建边。</summary>
        public static Dictionary<string, List<string>> BuildProjectReferenceGraph()
        {
            var graph = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (!Directory.Exists("Assets")) return graph;
            foreach (var asmdef in Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories))
            {
                string name;
                try { name = ReadDeclaredAssemblyName(asmdef); }
                catch (IOException) { continue; }   // 并发导入期读到半文件 = 缺边,由闭包红兜
                if (string.IsNullOrEmpty(name)) continue;
                graph[name] = AssemblyGates.ReadDeclaredReferences(asmdef);
            }
            return graph;
        }

        private static string ReadDeclaredAssemblyName(string asmdefPath)
        {
            var m = Regex.Match(File.ReadAllText(asmdefPath), "\"name\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>从 <paramref name="root"/> 出发的引用**传递闭包**(不含 root 自身;
        /// 纯函数 —— 负例夹具喂合成图,证明「一层 shim 即红」不是纸面承诺)。</summary>
        public static HashSet<string> ReferenceClosure(
            IDictionary<string, List<string>> graph, string root)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (graph == null) return seen;
            var frontier = new Stack<string>();
            if (graph.ContainsKey(root)) frontier.Push(root);
            while (frontier.Count > 0)
            {
                var cur = frontier.Pop();
                if (!graph.TryGetValue(cur, out var refs)) continue;   // 图外 = 引擎/BCL 叶
                foreach (var r in refs)
                {
                    if (string.IsNullOrEmpty(r) || !seen.Add(r)) continue;
                    if (graph.ContainsKey(r)) frontier.Push(r);        // 仅工程内装配继续展开
                }
            }
            return seen;
        }

        /// <summary>A6 传递闭包判定(纯函数):闭包 ∩ 禁引集合 ≠ ∅ ⇒ 红(间接引用同禁)。</summary>
        public static List<string> ReferenceClosureViolations(
            IDictionary<string, List<string>> graph, string root)
        {
            var errs = new List<string>();
            var closure = ReferenceClosure(graph, root);
            foreach (var hit in closure.Where(h => InputForbiddenAssemblies.Contains(h))
                                       .OrderBy(h => h, StringComparer.Ordinal))
                errs.Add($"[A6] {InputAssemblyName} **间接**引用「{hit}」—— 该装配引用图上从 " +
                         $"{root} 传递可达(闭包 = [{string.Join(", ", closure.OrderBy(c => c, StringComparer.Ordinal))}]);" +
                         "AC-3-A6 禁引程序集直接与间接同禁,一层 shim 不得绕过。");
            return errs;
        }

        /// <summary>A6 传递闭包断言(端到端):真工程装配图上跑 <see cref="ReferenceClosureViolations"/>。</summary>
        public static List<string> CheckInputReferenceClosure()
            => ReferenceClosureViolations(BuildProjectReferenceGraph(), InputAssemblyName);

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

        /// <summary>交出物闭集判定(**纯函数** —— 负例夹具直接喂候选类型集;
        /// 2026-09-26 评审 G4:原实现把「扫哪些类型」和「怎么判」焊在一起,两条红路径
        /// (白名单外新增 / 登记集缺失)从未被驱动过 —— 尤其**缺失**分支是唯一防
        /// 「闭集对空集断言 = 假绿」的护栏,没测 = 护栏形同虚设)。</summary>
        /// <param name="candidates">Intents 子命名空间的公开类型(调用方负责筛面)。</param>
        public static List<string> DeliveredIntentClosureViolations(IEnumerable<Type> candidates)
        {
            var errs = new List<string>();
            var found = (candidates ?? Enumerable.Empty<Type>())
                .Where(t => t != null)
                .Select(t => t.FullName)
                .ToHashSet();
            foreach (var name in found.Where(n => !DeliveredIntentTypes.Contains(n))
                                        .OrderBy(n => n, StringComparer.Ordinal))
                errs.Add($"[A6] {InputAssemblyName} Intents 子命名空间新增公开类型「{name}」" +
                         "∉ 交出物闭集 —— AC-3-A6「3 只产意图」白名单外新增须过本门。");
            foreach (var expected in DeliveredIntentTypes)
                if (!found.Contains(expected))
                    errs.Add($"[A6] 交出物「{expected}」不存在 —— 登记件缺失,闭集断言对空集 = " +
                             "假绿(AC-3-A6)。");
            return errs;
        }

        /// <summary>交出物闭集断言(端到端):Intents 子命名空间公开类型 vs 登记集。</summary>
        public static List<string> CheckDeliveredIntentClosure()
            => DeliveredIntentClosureViolations(PublicInputRootTypes());

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
                    // ⚠️ **跳过 const 字段**(评审 S7):const 是**编译期字面量**,使用点被内联,
                    //   值不进任何实例的载荷 —— 一支无害的 `const float Scale` 让整族载荷恒红
                    //   = 判据不可修(与 Fix.ToFloat 那次同型)。static **可变**字段仍扫(它确是
                    //   运行期数据,是浮点藏身处)。
                    foreach (var f in cur.GetFields(fb))
                    {
                        if (f.IsLiteral) continue;
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                    }
                    foreach (var p in cur.GetProperties(fb))
                        Visit(p.PropertyType, path + "." + p.Name, depth + 1);
                }
            }
        }

        /// <summary>A7 浮点叶子判据(float / double;别名 Single 系含)。</summary>
        public static bool IsFloatLeaf(Type t)
            => t == typeof(float) || t == typeof(double) ||
               t.FullName == "System.Single" || t.FullName == "System.Double";

        /// <summary>A7 载荷扫描根的**判据说明**(实现在 CheckAllPayloadClosures;2026-09-26 评审 S6)。</summary>
        public const string PayloadRootDerivation =
            "扫描根 = Sim.Contracts 全部 struct(减显式键型豁免)+ Gameplay.Input.Intents 交出物;" +
            "**不按 `*Payload` 命名约定派生** —— ADR-024 §① 的载荷真源是 entities.yaml 的 34 支 " +
            "SimEvent.Kind.*,命名约定只是它的影子;影子漂移会让 A7 漏扫一支载荷而全绿。";

        /// <summary>A7 载荷侧扫描根的**显式豁免**类型名(键型 + header 引用,逐条点名,
        /// 不用命名约定 —— 要豁免的东西必须能被人逐行读出来)。</summary>
        public static readonly string[] PayloadScanExcludeTypeNames =
        {
            "DaYiJingCheng.Sim.Contracts.PayloadRef",   // 载荷引用是 header 场,非载荷本体
            "DaYiJingCheng.Sim.Contracts.CaseId",       // 键型三元组:作为字段被扫,不单独立根
            "DaYiJingCheng.Sim.Contracts.DiseaseIdSet", // 键型 bitmask:同上
            // ⚠️ 呈现层 DTO —— **不是**豁免理由写着「呈现层所以不管」,而是它们
            // **不是 SimEvent 载荷**:A7 的 AC 是「SimEvent 载荷可达闭包零浮点」。
            // VitalsDto 是 ADR-005/ADR-012 :97 钉死的**全案唯一 float 出口**(呈现层读,
            // sim 禁读回)—— 它带 float 是**裁决本体**,不是违例;若不显式豁免,
            // 收成「全 struct」判据后它会恒红,而红它 = 把唯一合法出口判成违例
            // = 判据不可修(与 Fix.ToFloat 那次同型)。
            "DaYiJingCheng.Sim.Contracts.VitalsDto",
            "DaYiJingCheng.Sim.Contracts.AudioCueHandle",
            "DaYiJingCheng.Sim.Contracts.AudioCueDto",
            "DaYiJingCheng.Sim.Contracts.WorldPosLatest",
        };

        /// <summary>A7 全扫描根(S6:已收成「全 struct」判据,不再依赖 `*Payload` 命名约定)。
        /// 返回 (红错, 命中数)—— 命中 0 = WARN(根空 = 平凡成立,由调用方转 WARN)。</summary>
        public static List<string> CheckAllPayloadClosures(out int roots)
            => CheckAllPayloadClosures(out roots, out _);

        /// <summary>同 <see cref="CheckAllPayloadClosures(out int)"/>,另交出实际根集(评审 S6:
        /// 只交计数时「根数 30 / 实际应 39」这类**面缩小**仍可能全绿;集合面才闭)。
        /// <paramref name="rootFullNames"/> = 实际参与扫描的根类型全名(已排序)。</summary>
        public static List<string> CheckAllPayloadClosures(out int roots,
                                                            out List<string> rootFullNames)
        {
            roots = 0;
            rootFullNames = new List<string>();
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

            // ⚠️ 2026-09-26(评审 S6):此处**曾**按 `t.Name.EndsWith("Payload")` 命名约定派生
            // 根。ADR-024 §① 的载荷真源是 `entities.yaml` 的 34 支 `SimEvent.Kind.*`,命名
            // 约定是它的**影子**,不是它本身 —— 两张表各自维护,漂移无声:A7 会漏扫一支载荷
            // 而**全绿**(这正是「假绿」最贵的一种:门在跑、面在缩小、报告说通过)。
            // 收成「全部 struct」判据:Sim.Contracts 载荷层**没有**任何 struct 不是载荷,
            // 所以「全 struct 减键型」与「34 支」是**同一集合**,但判据不再依赖命名 ——
            // 将来登记一支不叫 `XxxPayload` 的载荷,本面自动纳入,不必回写第二张表。
            // `PayloadScanExcludeTypeNames` 是**显式**豁免(键型 + header 引用),
            // 刻意用全名而非命名约定 —— 要豁免的东西必须逐条点名。
            var exclude = new HashSet<string>(PayloadScanExcludeTypeNames, StringComparer.Ordinal);
            foreach (var t in types)
            {
                if (t.IsNested || t.IsAbstract) continue;
                if (!t.IsValueType) continue;
                if (exclude.Contains(t.FullName)) continue;
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
            var ordered = rootsSet.OrderBy(r => r.FullName, StringComparer.Ordinal).ToList();
            rootFullNames = ordered.Select(r => r.FullName).ToList();
            foreach (var root in ordered)
                errs.AddRange(CheckPayloadClosure(root));
            return errs;
        }

        /// <summary>本门全部判定一次跑完(菜单 / 测试 / 构建前门共用)。
        /// A6 直接引用集 + A6 传递闭包 + A6 交出物闭集 + A7 全根闭包 + **B3 IL 面**
        /// + B3 源文本。返回红错;roots = A7 扫描根数(0 = WARN 由调用方处理)。
        /// ⚠️ reflection 面的 <see cref="CheckReadingFieldLeaves(Type)"/> 仍**不入**
        /// RunAll —— 本装配(Editor.Tools.Gates)**不引用 Gameplay.Input**,该面上
        /// `typeof(EmergencyReading)` 不可编译。B3 的构建期强制点由**读 IL**的
        /// <see cref="CheckReadingFieldsIl"/> 承担(不产生程序集引用 ⇒ A6 边不破,
        /// 2026-09-26 评审 S1);reflection 面作为**第二道**由测试装配驱动,两面同判据
        /// (见 IsIlNonExpandableScope 与 ShouldExpandMembers 的逐条对应注记)。</summary>
        public static List<string> RunAll(out int roots)
        {
            var errs = new List<string>();
            errs.AddRange(CheckInputAssemblyReferences());     // A6 直接引用集
            errs.AddRange(CheckInputReferenceClosure());      // A6 传递闭包(间接引用 · G3)
            errs.AddRange(CheckDeliveredIntentClosure());      // A6 交出物闭集
            errs.AddRange(CheckAllPayloadClosures(out roots)); // A7 全载荷闭包
            errs.AddRange(CheckReadingFieldsIl(                // B3 字段全整数 · IL 面(S1)
                AssemblyGates.ScriptAssemblyPath(InputAssemblyName), DeliveredIntentTypes, out _));
            errs.AddRange(CheckInputSourceText());             // B3 源文本 + 零事件面
            return errs;
        }

        // ── B3 的 **IL 面**(构建期强制点;2026-09-26 评审 S1)──
        //
        // 为什么要再开一面:S1 的根因是「reflection 面读不到 `Gameplay.Input` 的类型」——
        // 门装配刻意不引被门对象(A6 自己禁这条边),于是 AC-3-B3 的字段全整数只由
        // EditMode 测试驱动,**构建期零强制点**:把 `EmergencyReading.Magnitude` 从 int
        // 改成 float,`unity build` 照样成功。同批 A6/A7 都上了 IPreprocessBuild,
        // 唯独 B3 没有 = 判据强度不对等。
        //
        // 修法不是给门加一条引用(A6 禁令一旦破,整条边界自证失效),而是**读 IL**:
        // Cecil 打开 Gameplay.Input.dll,按 `DeliveredIntentTypes` 全名取类型,对字段
        // 做与 reflection 面**同判据**的叶子判定(合格叶子 / 浮点 / 不可展开 = 非合格)。
        // 门读的是**产物元数据**,不产生任何程序集引用 ⇒ A6 的编译期边不被触碰。
        //
        // 已知漏报面:属性不在本面(读数字段一律用字段,reflection 面覆盖属性,两面并集
        // = 字段 + 属性的全数据面);`const` 字面量**照判**(const 是载荷的类型面信息,
        // 收窄 Instance 的理由见 reflection 面 S7 连带注记 —— 两面都判,口径一致)。
        /// <summary>B3 IL 面:对 <paramref name="dllPath"/> 内 <paramref name="typeFullNames"/>
        /// 逐个做字段叶子闭合判定(纯函数 —— 负例夹具喂自造产物)。
        /// <paramref name="matched"/> = 实际在产物中找到的类型数(0 = 扫描面丢失 ⇒ 调用方红)。</summary>
        public static List<string> CheckReadingFieldsIl(string dllPath, IReadOnlyList<string> typeFullNames,
                                                        out int matched)
        {
            var errs = new List<string>();
            matched = 0;
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                errs.Add($"[B3][IL] 编译产物缺失「{dllPath ?? "<null>"}」—— 扫描面不存在," +
                         "拒以空集冒充绿 = 假绿(AC-3-B3 构建期强制点)。");
                return errs;
            }
            if (typeFullNames == null || typeFullNames.Count == 0)
            {
                errs.Add("[B3][IL] 目标类型名集为空 —— 拒以空集冒充绿(AC-3-B3)。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // 与 AssemblyGates.CheckAudioAssemblyIl 同款:Deferred 才不触 Resolve,
                // 否则 netstandard 等程序集解析失败(见该处实测注记 2026-09-26)。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                var wanted = new HashSet<string>(typeFullNames, StringComparer.Ordinal);
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.FullName == "<Module>") continue;
                    if (!wanted.Contains(type.FullName)) continue;
                    matched++;
                    errs.AddRange(ReadingTypeIlViolations(type, type.FullName));
                }
            }

            if (matched == 0)
                errs.Add($"[B3][IL] 产物「{dllPath}」内未找到任何登记交出物类型 —— 扫描面丢失," +
                         "拒以空集冒充绿(AC-3-B3 构建期强制点)。");
            return errs;
        }

        /// <summary>B3 IL 面的单类型判定(递归进字段 / 数组元素 / 基类链;纯函数)。
        /// 与 reflection 面 CheckReadingFieldLeaves 同判据,故两面结论可互为对照。</summary>
        private static List<string> ReadingTypeIlViolations(TypeDefinition type, string path)
        {
            var errs = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in AllFields(type))
                VisitRef(f.FieldType, path + "." + f.Name, 0);
            return errs;

            void VisitRef(TypeReference tr, string p, int depth)
            {
                if (tr == null) return;
                if (depth > 64)
                {
                    errs.Add($"[B3][IL] 深度护栏触发于 {p}(depth > 64)—— AC-3-B3。");
                    return;
                }
                if (tr.IsArray || tr.IsByReference || tr.IsPointer)
                {
                    VisitRef(tr.GetElementType(), p + "[]", depth + 1);
                    return;
                }
                if (!visited.Add(tr.FullName)) return;

                var full = tr.FullName;
                if (ReadingFloatFullNames.Contains(full))
                {
                    errs.Add($"[B3][IL] 判定读数字段叶子出现浮点「{full}」(路径 {p})—— " +
                             "全整数(字段 ∈ int/Fix/bool/枚举/long;AC-3-B3;float 经别名同拒)。");
                    return;
                }
                if (ReadingAllowedLeafFullNames.Contains(full)) return;   // 合格叶子
                if (IsIlNonExpandableScope(full))
                {
                    // 不可展开面(BCL / 引擎)= 非合格叶子 ⇒ 红(白名单闭合,同 reflection 面 ⚠️)
                    errs.Add($"[B3][IL] 判定读数字段叶子「{full}」∉ {{int,long,bool,枚举,Fix}}"+
                             $"(路径 {p})—— 全整数白名单**闭合**(AC-3-B3);string / object / " +
                             "引擎类型不得作为读数字段。");
                    return;
                }
                if (tr.HasGenericParameters)
                {
                    // ⚠️ Deferred 模式禁 Resolve(TypeReference)—— 真树里闭集五件**零泛型**,
                    // 故此面不展开泛型实参;命中即记红(不静默绿),把「闭集件用泛型」显红出来。
                    errs.Add($"[B3][IL] 判定读数字段类型「{full}」含泛型实参(路径 {p})—— " +
                             "AC-3-B3 闭集五件为非泛型具体类型;泛型实参无法在 Deferred 模式" +
                             "下判定(禁 Resolve),故此处显红而非静默放行。");
                    return;
                }
                if (tr is TypeDefinition def)
                {
                    // ⚠️ `p` 必须**带上字段名**递归(2026-09-26 修):原写法 `p + "."` 把每个
                    // 成员的路径都压成 `<路径>.`,多层嵌套时红行指向一堆同名 `..` —— 报错指不
                    // 到具体字段,而这份报错的**用途**就是让人照着改字段。
                    foreach (var f in AllFields(def)) VisitRef(f.FieldType, p + "." + f.Name, depth + 1);
                    return;
                }
                if (tr.Scope is AssemblyNameReference) return;   // 第三方程序集面,不由本门裁决
            }
        }

        /// <summary>IL 面的「不可展开程序集面」判定 —— 与 reflection 面
        /// ShouldExpandMembers 的命名空间判定**逐条对应**(两面须同答案,否则同一份载荷
        /// 在 reflection 面绿、IL 面红,或反之,日志无法解释)。</summary>
        private static bool IsIlNonExpandableScope(string fullName)
            => fullName == "System" || fullName.StartsWith("System.", StringComparison.Ordinal) ||
               fullName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
               fullName.StartsWith("UnityEditor", StringComparison.Ordinal) ||
               fullName.StartsWith("Unity.", StringComparison.Ordinal) ||
               fullName.StartsWith("Microsoft.", StringComparison.Ordinal);

        private static IEnumerable<FieldDefinition> AllFields(TypeDefinition t)
        {
            for (var cur = t; cur != null; cur = cur.BaseType as TypeDefinition)
                foreach (var f in cur.Fields) yield return f;
        }

        // ── B3:EmergencyReading 字段类型断言 + 3 侧源文本禁调 ──
        /// <summary>断言一个类型的所有字段叶子 ∈ {int,long,bool,枚举,Fix} ∪ int[](递归进
        /// 内嵌结构体字段 —— 承 A7 纪律)。纯函数;负例夹具直接喂 <c>float magnitude</c> 类型。
        /// ⚠️ **不可展开的容器按「非合格叶子 ⇒ 红」处理**(2026-09-26 评审 G5):本门与 A7
        ///   的纪律不同 —— A7 只问「有没有浮点」(引擎 / BCL 类型不展开即绿),而 B3 问的是
        ///   **白名单闭合**(字段只能是整数域叶子)。原实现直接展开一切、包括 <c>string</c> /
        ///   <c>object</c> 的内部字段,于是 <c>struct R { string Name; }</c> 零红行 ——
        ///   白名单自己说 string 不合格,门却放行 = 自相矛盾。收窄后:<c>string</c> / 引擎类型
        ///   走「不可展开」分支 ⇒ 非合格叶子 ⇒ 红。</summary>
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
                if (!ShouldExpandMembers(t))
                {
                    // 不可展开面(BCL / 引擎)= 非合格叶子 ⇒ 红(白名单闭合,见方法头 ⚠️)
                    errs.Add($"[B3] 判定读数字段叶子「{t.FullName}」∉ {{int,long,bool,枚举,Fix}}" +
                             $"(路径 {path})—— 全整数白名单**闭合**(AC-3-B3);string / 引擎 / " +
                             "object 等非整数域类型不得作为读数字段。");
                    return;
                }
                // 其余类型(结构体 / 类)→ 递归进字段
                const BindingFlags fb = BindingFlags.DeclaredOnly |
                                         BindingFlags.Instance | BindingFlags.Static |
                                         BindingFlags.Public | BindingFlags.NonPublic;
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (!ShouldExpandMembers(cur)) break;
                    foreach (var f in cur.GetFields(fb))
                        Visit(f.FieldType, path + "." + f.Name, depth + 1);
                }
            }
        }

        /// <summary>B3 合格叶子:int / long / bool / 枚举 / Fix(tick 计数 · 沿计数 · 序数 · Q16.16)。</summary>
        public static bool IsAllowedReadingLeaf(Type t)
            => EmergencyReadingAllowedLeaves.Contains(t) || t.IsEnum;

        /// <summary>B3 的单文件源文本判定(**纯函数** —— 负例夹具直接喂合成文本,
        /// 门与测试共用同一份判定代码,不存在「为真而真」的空间;2026-09-26 评审 G2)。
        /// 判据:① 注释剥离后出现任一 <see cref="InputForbiddenSourceTokens"/> 即红;
        /// ② 出现 <see cref="FixParseParseMarker"/> 即红(判定结果不经字符串)。</summary>
        /// <param name="src">源文件全文。</param>
        /// <param name="fileLabel">报错时点名的文件标识(端到端面传真实路径)。</param>
        public static List<string> SourceTextViolations(string src, string fileLabel)
        {
            var errs = new List<string>();
            var text = AssemblyGates.StripCommentsPreserveStrings(src ?? string.Empty);
            if (text.IndexOf(FixParseParseMarker, StringComparison.Ordinal) >= 0)
                errs.Add($"[B3] {fileLabel} 出现「{FixParseParseMarker}」—— 3 侧判定结果构造路径" +
                         "禁经字符串(ADR-011 Amendment A / AC-3-B3;判定由 10 直接构造)。");
            foreach (var token in InputForbiddenSourceTokens)
            {
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(token)}\b"))
                    errs.Add($"[B3] {fileLabel} 源文本出现「{token}」—— 3 零事件面" +
                             "(AC-3-A6 零 SimEvent / 禁写通道 IEventSink;注释剥离后仍命中 = " +
                             "代码 / 字符串 / nameof 面)。");
            }
            return errs;
        }

        /// <summary>B3 的调用点扫描(Gameplay.Input 源树逐文件送
        /// <see cref="SourceTextViolations"/>;目录缺失 = 红,拒以空集冒充绿)。</summary>
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
                errs.AddRange(SourceTextViolations(File.ReadAllText(f), f));
            return errs;
        }

        // 展开资格与 PresentationDtoGuard 同款(引擎 / BCL 命名空间跳过自有成员,
// 泛型实参照走;非引擎类型永不被当叶子跳过)。
        // ⚠️ **A7 与 B3 对本判据的回答不同(2026-09-26 评审 G1/G5,刻意不对称)**:
        //   A7 只问「闭包里有没有浮点」—— 不可展开面里**没有** float ⇒ 绿(引擎 / BCL
        //   类型自身不由本门裁决;这与 PresentationDtoGuard 同当)。故事 QA 边界例
        //   「接口字段 / 多态载荷」正落在这一格:`IFace Field;` 判绿,因为接口的
        //   实现面不经本键可达 —— 需展开实现面才能断,代价 = 要对全树接口做实现枚举
        //   (与 DtoGuard 相同的成本,当初据此不采)。**选择 = 显式记录,不是遗漏**;
        //   `test_payloadClosure_interfaceField_isGreen_byDeclaredScope` 把该选择钉住,
        //   日后若要改面(扩为实现枚举),本测会红提醒同步改决策记录。
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