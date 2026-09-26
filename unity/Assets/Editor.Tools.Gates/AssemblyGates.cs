// U0-b b2 · b3 · b4 · b5 —— 四条构建期断言(装配级)
//
// 权威来源:
//   b2 = ADR-017 §二(门 A 白名单,构建失败级)· ADR-025 §① 注
//        「`Sim` 引用集期望 = {BCL, Sim.Contracts}」—— ⚠️ 不是「恰 = 两元素」:
//        ADR-025 RC-5 已订正,没有任何 asmdef 字段能产出 BCL-only 引用集,
//        netstandard/mscorlib 等基座程序集必然出现 ⇒ 判据 = 「**不含任何引擎程序集,
//        且工程内程序集侧 ⊆ {Sim.Contracts}**」。
//   b3 = ADR-025 §④(未登记 asmdef = 构建失败;清单封闭性)
//   b4 = ADR-025 §② 甲案(ToFloat() 调用点白名单:Sim 内出现 = 构建失败)
//   b5 = AC-44-B1(audio Story 001;44 音频模块装配边界三段 —— 见下方 b5 段头注)
//
// 落点理由:三条都是**编辑期**断言,住 Editor.Tools 族(不进构建,门 A 不约束 ——
// ADR-022 §① 同构)。触发面 = ① Unity 编译后自动刷新(ReloadAssemblyPostProcessor,
//   但 2026-09-26 降噪后 reload **只跑 b3/b2** 这两个 O(1) 级面)
//   ② 手动菜单项「大医精诚/Validation/Run Assembly Gates」(全门)
//   ③ **构建前 fail-fast**(BuildGate : IPreprocessBuildWithReport,2026-09-26 评审 G7 新增
//     —— AC-3-A6/A7/B3 与 b5 字面写的是「构建失败」,只有日志红行 + EditMode 测试两道
//     证据时,门在真出问题时仍会放构建过去)
//   ④ CI 侧由 ADR-012 矩阵的 Editor 格跑 EditMode 断言(归 CI 故事,见 README)。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using UnityEditor;
using UnityEditor.Build;          // BuildFailedException(构建前门 fail-fast · 2026-09-26)
using UnityEditor.Build.Reporting;  // IPreprocessBuildWithReport / BuildReport(同上)
using UnityEditor.Compilation;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>构建期装配门。任一失败 = Debug.LogError + 编译日志红行(不打断编辑器,
    /// CI 以日志红行为失败判据;Unity Build 时由 OnPostProcess 升级为 throw)。
    /// 2026-09-26:internal → public(承 b5/audio Story 001;照 ConservationGates 先例 ——
    /// EditMode 测试经 public 面直调检查器,免 InternalsVisibleTo)。</summary>
    public static class AssemblyGates
    {
        // ── b3 的登记清单(ADR-025 §① 七装配 ∪ 测试族两装配 ∪ Editor.Tools 族)──
        // Editor.Tools 族拆 Level/Kindgen 两个具名装配是卡 §0.1 的落地形(ADR-025 表记「族」);
        // Gates(U0-b)+ Spike(U1 spike 批)+ Bake(Story 008 数据管线)同属该族追加 ——
        // 族内增员 = 改本清单 **且同批回写 ADR-025 §① 族行**(2026-09-26 审查 Required-5:
        // 原「只改本清单」与 §④「新 asmdef 须追加进表」字面冲突 = 封闭性自证闭环;
        // Gates/Spike/Bake 三支已于 2026-09-26 回写 ADR-025 §① —— 两处自此一致)。
        // Gameplay.Input = story-001 B1 拆装增员(ADR-025 §① 2026-09-25 已载;清单 2026-09-25 补登)。
        private static readonly HashSet<string> Manifest = new HashSet<string>
        {
            "Sim", "Sim.Contracts", "Sim.Codec",
            "Gameplay.Presentation", "Gameplay.UI", "Gameplay.Input",
            "Editor.Tools.Level", "Editor.Tools.Kindgen", "Editor.Tools.Gates",
            "Editor.Tools.Spike", "Editor.Tools.Bake",
            "Sim.Contracts.Tests", "Gameplay.Tests",
        };

        // ── b4 的白名单:允许调 ToFloat() 的装配(ADR-025 §② 甲案 = {Sim.Codec, Gameplay.*})──
        private static readonly string[] ToFloatWhitelistPrefixes =
            { "Sim.Codec", "Gameplay.Presentation", "Gameplay.UI", "Gameplay.Tests" };

        [MenuItem("大医精诚/Validation/Run Assembly Gates")]
        private static void RunMenu()
        {
            var errs = RunAll();
            CheckToFloatCallsites(errs);
            var b5Errs = RunAudioBoundary(out var b5Warns);
            errs.AddRange(b5Errs);
            foreach (var w in b5Warns) Debug.LogWarning(w);
            // 2026-09-26(评审 G7):3 的意图边界门此前**零调用方** —— 四道门只被 EditMode
            // 测试驱动,AC-3-A6/A7 字面的「构建失败」无任何强制点。接进菜单与 reload 钩子。
            var inputErrs = InputBoundaryGates.RunAll(out var inputRoots);
            if (inputRoots == 0)
                Debug.LogWarning("[InputBoundaryGates] A7 扫描根为 0 —— 扫描面丢失(假绿面),请查装配加载。");
            errs.AddRange(inputErrs);
            foreach (var e in errs) Debug.LogError(e);
            Debug.Log(errs.Count == 0
                ? "[AssemblyGates] b2/b3/b4/b5 + 3 意图边界门 全过"
                : $"[AssemblyGates] {errs.Count} 条失败(见红行)");
        }

        public static List<string> RunAll()
        {
            var errs = new List<string>();
            CheckManifestClosure(errs);      // b3
            CheckGateA(errs);                // b2
            return errs;
        }

        // ═══ b3 装配封闭性:工程内 asmdef 名集合 ⊆ Manifest;Manifest 成员全存在 ═══
        // ⚠️ 程序集名取 asmdef **JSON 的 name 字段**,不取文件名 —— 实测两装配文件名
        //   (EditMode.asmdef / PlayMode.asmdef)与其声明名(Sim.Contracts.Tests /
        //   Gameplay.Tests)不一致;按文件名比对 = b3 自身误报。
        private static void CheckManifestClosure(List<string> errs)
        {
            // ⚠️ 扫描面 = Assets/ **之内**(工程自有装配)。
            // AssetDatabase.FindAssets("t:asmdef") 实测连 Packages/ 下解析出的包内
            // asmdef 一并吐回(Addressables 依赖图带进 ~100 支)⇒ 必须按路径前缀过滤。
            // ADR-025 §④ 的「未登记 asmdef = 构建失败」管的是本项目 asmdef,
            // 第一方包(Embedded)当前不存在;若日后引入,须连同清单口径一起裁。
            var found = new HashSet<string>();
            foreach (var f in Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories))
            {
                var json = File.ReadAllText(f);
                var m = System.Text.RegularExpressions.Regex.Match(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) found.Add(m.Groups[1].Value);
            }
            foreach (var extra in found.Where(f => !Manifest.Contains(f)))
                errs.Add($"[b3] 未登记 asmdef「{extra}」—— 清单封闭性 = 构建失败(ADR-025 §④)。" +
                         "新增装配须先回写 ADR-025 §① 表。");
            foreach (var missing in Manifest.Where(m => !found.Contains(m)))
                errs.Add($"[b3] 清单成员「{missing}」在工程内不存在 —— 装配被删/改名?");
        }

        // ═══ b2 门 A:Sim / Sim.Contracts / Sim.Codec 三装配的引用集零引擎程序集 ═══
        private static readonly string[] GateAAssemblies = { "Sim", "Sim.Contracts", "Sim.Codec" };

        private static void CheckGateA(List<string> errs)
        {
            foreach (var asmName in GateAAssemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                              .FirstOrDefault(a => a.GetName().Name == asmName);
                if (asm == null)
                {
                    // 编译失败时本断言不可用 —— 红在编译器,不重复报。
                    continue;
                }
                var refs = asm.GetReferencedAssemblies().Select(r => r.Name).ToList();
                // Sim 侧的工程内允许集 = {Sim.Contracts}(Sim)或 ∅(Contracts)/ +Sim.Contracts(Codec)
                foreach (var r in refs.Where(r =>
                             r.StartsWith("UnityEngine") || r.StartsWith("UnityEditor") ||
                             r.StartsWith("Unity.") ))
                {
                    if (r.StartsWith("UnityEngine.TestRunner") || r.StartsWith("UnityEditor.TestRunner"))
                        continue;   // UTF 注入豁免(仅测试装配会出现;门 A 三装配本不该见到)
                    errs.Add($"[b2] 门 A 装配「{asmName}」引用了引擎程序集「{r}」—— 构建失败" +
                             "(ADR-017 §二:noEngineReferences 声明之外的隐式解析也算违例)。");
                }
                if (asmName == "Sim")
                {
                    var nonBcl = refs.Where(r =>
                        !r.StartsWith("System") && !r.StartsWith("netstandard") &&
                        r != "mscorlib" && r != "Mono").Except(new[] { "Sim.Contracts" });
                    foreach (var r in nonBcl)
                        errs.Add($"[b2] Sim 引用集出现 BCL∪{{Sim.Contracts}} 之外的「{r}」—— " +
                                 "期望引用集白名单违例(ADR-025 §①)。");
                }
            }
        }

        // ═══ b4 ToFloat() 调用点扫描(Sim 装配源文件内出现 = 违例)═══
        // 口径:源文本级扫描。**刻意不用 Roslyn**(ADR-024 §⑤ 同款口径「不引 analyzer」);
        // 命中 = `.ToFloat(` 出现在 Sim/Gameplay.Presentation 之外装配目录下。
        // 已知漏报面:注释与字符串字面量(误报方向,偏安全);`Fix x; x.ToFloat()` 经
        // 变量名任意 ⇒ 必须带点前缀匹配,不做纯标识符匹配(会漏 this.x.ToFloat() 的反向)。
        private static void CheckToFloatCallsites(List<string> errs)
        {
            var asmDirs = new Dictionary<string, string>
            {
                { "Sim", "Assets/Sim" },
                { "Sim.Codec", "Assets/Sim.Codec" },
                { "Gameplay.Presentation", "Assets/Gameplay.Presentation" },
                { "Gameplay.UI", "Assets/Gameplay.UI" },
            };
            foreach (var kv in asmDirs)
            {
                if (!Directory.Exists(kv.Value)) continue;
                foreach (var f in Directory.GetFiles(kv.Value, "*.cs", SearchOption.AllDirectories))
                {
                    // .g.cs 生成物同样在扫描面内(白名单按装配不按文件)。
                    var lines = File.ReadAllLines(f);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        int idx = lines[i].IndexOf(".ToFloat(", StringComparison.Ordinal);
                        if (idx < 0) continue;
                        bool allowed = kv.Key == "Gameplay.Presentation" || kv.Key == "Gameplay.UI"
                                    || ToFloatWhitelistPrefixes.Any(p => kv.Key.StartsWith(p));
                        if (!allowed)
                            errs.Add($"[b4] {f}:{i + 1} 装配「{kv.Key}」内出现 ToFloat() 调用 —— " +
                                     "Sim/Sim.Codec 侧 = 构建失败(ADR-025 §② 甲案白名单)。");
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // b5 —— 44 音频模块装配边界(AC-44-B1 三段 · audio Story 001)
        // ══════════════════════════════════════════════════════════════════════
        // 权威来源:
        //   · AC-44-B1(design/gdd/audio-system.md:924 起;2026-09-25 二轮 F1 重写 ——
        //     用户裁定 = 「黑名单 + IL 扫描」路线,否决 44 单开 asmdef、否决先摘 Presentation→Sim)
        //   · ADR-018 §一/§二(44 只触发/只渲染;唯一游戏状态入口 = AudioCueDto)
        //   · ADR-025 §①(装配登记集)· ADR-017 §三(表现层 DOTS 触发前不引入)
        //   · control-manifest 音频块 Forbidden:「44 引用 Sim/Sim.Codec 类型(含 IEventSink)」
        //   · Story: production/epics/audio-system/story-001-assembly-boundary-dto.md
        //
        // 三段:
        //   ① 44 类型级 metadata 扫描(读编译产物 Library/ScriptAssemblies/<asm>.dll)
        //      + ①b 源文本层(using / nameof / 反射字符串形态 —— metadata 层抓不到未使用
        //      using 与纯 ldstr;故事 QA「在 44 命名空间加 using ⇒ 必红」只有源文本层能兑现)。
        //   ② 装配引用集 ⊆ (ADR-025 §① 登记集 ∪ 引擎白名单 ∪ 既有基线)—— 新增漂移 = 红。
        //   ③ DOTS 黑名单(Unity.Entities|Burst|Jobs|Mathematics)—— 无条件红,不受基线豁免。
        //
        // ⚠️ ① 的实现路线说明:GDD 注曾写「System.Reflection.Metadata 读编译产物」——
        //    该程序集在本工程**编译不过**(unity-4.8-api + netstandard ref 均无该 DLL,2026-09-26
        //    核实);改走 Mono.Cecil 1.11.6(com.unity.nuget.mono-cecil,已钉 manifest 直接依赖;
        //    此前已是 csproj 既有 HintPath 传递依赖)—— 判据等价:所有 TypeRef/MemberRef 语义
        //    = 引用类型能解析回「定义装配 + 命名空间 + 类型名」,Cecil 逐 token 给出,
        //    **不引 Roslyn**(承 b4 / ADR-024 §⑤ 同口径)。
        //
        // 假绿防护三处(任一 = 红,不许静默过):
        //   · EditorUtility.scriptCompilationFailed ⇒ 拒扫陈旧产物(读上一版 = 假绿);
        //   · 产物 / asmdef 缺失 ⇒ 红(不以空集冒充绿);
        //   · 44 前缀 0 命中 ⇒ WARN(44 实现类型尚不存在,① 对空集平凡成立;
        //     扫描键 = 下方 AudioModuleNamespacePrefix 常量,实现期 0 命中转 FAIL)。
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>b5 扫描键:44 模块命名空间前缀(单一出处)。全库现有 Gameplay.Presentation
        /// 文件均在根命名空间,44 子命名空间尚无实现类型 ⇒ 本前缀是**约定**:
        /// 44 新类型必须住此之下,否则 ① 扫不到 —— **由 CheckAudioEscapeText 逃逸子段兜**
        /// (含44 契约 token 却未声明前缀的文件 = 红;原「②③ + B3/B4 反射兜底」说法为假,
        /// B3/B4 同键于前缀,2026-09-26 代码审查 Required-1 订正)。</summary>
        public const string AudioModuleNamespacePrefix = "DaYiJingCheng.Gameplay.Presentation.Audio";

        /// <summary>44 实现所在装配(b5 扫描面;ADR-025 §① 登记)。</summary>
        public const string PresentationAssemblyName = "Gameplay.Presentation";

        /// <summary>① 类型级黑名单 —— 装配(AC 字面:「零 TypeRef/MemberRef 解析到
        /// Sim/Sim.Codec 定义的任何类型」)。</summary>
        public static readonly string[] AudioForbiddenAssemblies = { "Sim", "Sim.Codec" };

        /// <summary>① 类型级黑名单 —— 契约内的 sim 权限 / 上游查询类型。
        /// ⚠️ IEventSink / IEventAuthority / SimEvent / VitalsDto 等现住 **Sim.Contracts**
        /// (b1b 落位;契约对 44 是合法依赖)⇒ 装配黑名单兜不住它们,须按**类型名**补足:
        /// 抽象点 = 写通道 / 掷骰 / tick / 发号 / 体征查询 / 传送,对 44 全是「订阅 sim 真值」
        /// 旁路(ADR-018 §一禁;VitalsDto = 2026-09-18 幽灵入口删除,AC-B3「不含 VitalsDto 族」;
        /// control-manifest Forbidden「44 引用 Sim/Sim.Codec 类型(含 IEventSink)」同此意图)。</summary>
        public static readonly string[] AudioForbiddenContractTypes =
        {
            "IEventSink", "IEventAuthority", "ITickProvider", "IIdAuthority",
            "IVitalsQuery", "ITeleportCommandSink", "SimEvent", "VitalsDto",
        };

        /// <summary>②③ 引用黑名单 —— **无条件红,不受基线豁免**。
        /// Sim.Codec:44 不做编码;DOTS 家族:ADR-017 §三,触发前表现层同样不引入。</summary>
        public static readonly string[] AudioRefBlacklist =
        {
            "Sim.Codec",
            // ADR-017 §三(2026-09-26 代码审查 Required-3 补三支):触发前表现层同样不引入
            // DOTS 家族 —— 原四支漏 Unity.Collections / Unity.Physics / Unity.Transforms,
            // 且 ② 的 `Unity.` 引擎白名单会让它们直接过门(§三判定对象点名 Unity Physics)。
            "Unity.Entities", "Unity.Burst", "Unity.Jobs", "Unity.Mathematics",
            "Unity.Collections", "Unity.Physics", "Unity.Transforms",
        };

        /// <summary>② ADR-025 §① 对本装配登记的工程内依赖。
        /// 新增引用 = 红,要求**先回写 ADR-025 §①** 再放行。</summary>
        public static readonly string[] AudioRefRegistered = { "Sim.Contracts" };

        /// <summary>② 既有基线快照(**手写常量,非机器快照**)—— 只放行当前树已存在的
        /// 工程债,防新漂移而不动既有债。⚠️ "Sim" = Presentation→Sim 工程债(L4 数据装载
        /// 合法用途:AddressablesDataProvider / CookedCodec / DataCorePreloader)——
        /// **摘除归装配轮,另账**(Story 001 Out of Scope)。基线项 = WARN 不红。</summary>
        public static readonly string[] AudioRefBaseline = { "Sim" };

        /// <summary>编译产物路径:只认 Library/ScriptAssemblies(编辑器程序集)。
        /// ⚠️ 不用 Bee/artifacts、PlayerScriptAssemblies、ManagedStripped 副本
        /// (陈旧 / 玩家侧 / 裁剪面,扫它们 = 假绿或误报)。
        /// ⚠️ 2026-09-26 修:原式少了 Library 段(unity/Assets/.. = unity/ 而非 unity/Library/),
        /// 首跑两测「产物缺失」假红 —— dataPath/.. 后必须接 Library。</summary>
        public static string ScriptAssemblyPath(string assemblyName)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "ScriptAssemblies",
                                              assemblyName + ".dll"));

        /// <summary>命名空间前缀判定(扫描键;嵌套类型的 Namespace 取最外层,天然命中)。</summary>
        public static bool IsInNamespacePrefix(string ns, string prefix)
            => !string.IsNullOrEmpty(ns) &&
               (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal));

        // ── ① 核心:对编译产物做 44 命名空间前缀下的类型级 metadata 扫描 ────────
        // 谓词 = 纯函数(dllPath + nsPrefix ⇒ 错误列表 + 命中数)—— 负例端到端注入:
        // 对**测试装配**的产物(内含44 前缀 fixture)跑同一谓词,真 IL 必红。
        // 覆盖面:签名(字段 / 属性 / 方法返回与参数 / 基类 / 接口 / 泛型实参 / 特性 /
        // 泛型约束)+ 方法体指令操作数(call / callvirt / newobj / ldtoken / ldfld … 的
        // TypeRef / MemberRef / MethodSpec / TypeSpec 全部解析回定义装配)。
        // 已知漏报面:纯字符串反射(「Type.GetType("DaYiJingCheng.Sim.X")」)—— 由源文本层
        // CheckAudioSourceText 兜(双层缺一不可,story QA 两条边各管一层)。
        public static List<string> CheckAudioAssemblyIl(string dllPath, string nsPrefix,
                                                        out int matchedTypeCount)
        {
            matchedTypeCount = 0;
            var errs = new List<string>();
            if (!File.Exists(dllPath))
            {
                errs.Add($"[b5①] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿(AC-44-B1 ①)。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // ⚠️ 必须 Deferred(实测 2026-09-26):Immediate 会**急切**读取特性构造参数 ——
                // ReadCustomAttributeEnum → CheckedResolve → 要求程序集解析器能解析
                // netstandard 等 —— Unity 编辑器与独立进程都会 AssemblyResolutionException。
                // Deferred 下本门只碰 AttributeType / 签名 / 方法体指令,零 Resolve 调用。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                var selfName = asm.Name.Name;
                foreach (var type in AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    if (!IsInNamespacePrefix(EffectiveNamespace(type), nsPrefix)) continue;
                    matchedTypeCount++;
                    var from = $"{selfName}::{type.FullName}";

                    VerdictTypeRef(from, type.BaseType, selfName, errs);
                    foreach (var ii in type.Interfaces)
                        VerdictTypeRef(from, ii.InterfaceType, selfName, errs);
                    if (type.HasGenericParameters)
                        foreach (var gp in type.GenericParameters)
                            foreach (var c in gp.Constraints)
                                VisitTypeRefs(from, c.ConstraintType, selfName, errs);
                    foreach (var ca in type.CustomAttributes)
                        VerdictTypeRef(from, ca.AttributeType, selfName, errs);

                    foreach (var f in type.Fields)
                    {
                        VisitTypeRefs(from, f.FieldType, selfName, errs);
                        foreach (var ca in f.CustomAttributes)
                            VerdictTypeRef(from, ca.AttributeType, selfName, errs);
                    }

                    foreach (var m in type.Methods)
                    {
                        VisitTypeRefs(from, m.ReturnType, selfName, errs);
                        foreach (var p in m.Parameters)
                            VisitTypeRefs(from, p.ParameterType, selfName, errs);
                        if (m.HasGenericParameters)
                            foreach (var gp in m.GenericParameters)
                                foreach (var c in gp.Constraints)
                                    VisitTypeRefs(from, c.ConstraintType, selfName, errs);
                        foreach (var ca in m.CustomAttributes)
                            VerdictTypeRef(from, ca.AttributeType, selfName, errs);
                        if (!m.HasBody) continue;

                        foreach (var instr in m.Body.Instructions)
                        {
                            switch (instr.Operand)
                            {
                                case TypeReference tr:
                                    VisitTypeRefs(from, tr, selfName, errs);
                                    break;
                                case MethodReference mr:
                                    // MethodSpec / GenericInstanceMethod 皆为 MethodReference 子类
                                    VerdictTypeRef(from, mr.DeclaringType, selfName, errs);
                                    VisitTypeRefs(from, mr.ReturnType, selfName, errs);
                                    foreach (var p in mr.Parameters)
                                        VisitTypeRefs(from, p.ParameterType, selfName, errs);
                                    if (mr is GenericInstanceMethod gim)
                                        foreach (var ga in gim.GenericArguments)
                                            VisitTypeRefs(from, ga, selfName, errs);
                                    break;
                                case FieldReference fr:
                                    VerdictTypeRef(from, fr.DeclaringType, selfName, errs);
                                    VisitTypeRefs(from, fr.FieldType, selfName, errs);
                                    break;
                                case CallSite cs:
                                    // 2026-09-26 代码审查 Required-2:calli 的 operand 是 CallSite
                                    // (实现 IMethodSignature,**非** MethodReference 子类)—— 原 case
                                    // 落空 = 漏判;C#9 `delegate*` 函数指针会发 calli。
                                    VisitTypeRefs(from, cs.ReturnType, selfName, errs);
                                    foreach (var cp in cs.Parameters)
                                        VisitTypeRefs(from, cp.ParameterType, selfName, errs);
                                    // ⚠️ 本版 Cecil(0.11.x 系)的 CallSite 无 HasGenericParameters/
                                    // GenericParameters 面(CS1061 实测)—— calli 泛型签名忽略面
                                    // 登记于此;本仓 44 代码不用 delegate* 泛型形,休眠。
                                    break;
                                // 字符串 / 分支 / 变量 / 立即数:无 TypeRef 面 —— ldstr 形态归源文本层
                            }
                        }

                        // 2026-09-26 代码审查 Required-2:catch/filter 的异常类型(CatchType)
                        // 是独立 TypeRef 面 —— `catch (Sim.X)` 整面原漏扫。
                        foreach (var eh in m.Body.ExceptionHandlers)
                            if (eh.CatchType != null)
                                VisitTypeRefs(from, eh.CatchType, selfName, errs);
                    }
                }
            }
            return errs;
        }

        /// <summary>读编译产物的 AssemblyRef 表(② 的实际引用面;与 asmdef 声明面并集送检)。</summary>
        public static List<string> ReadCompiledReferenceNames(string dllPath)
        {
            if (!File.Exists(dllPath)) return new List<string>();
            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // ⚠️ 必须 Deferred(实测 2026-09-26):Immediate 会**急切**读取特性构造参数 ——
                // ReadCustomAttributeEnum → CheckedResolve → 要求程序集解析器能解析
                // netstandard 等 —— Unity 编辑器与独立进程都会 AssemblyResolutionException。
                // Deferred 下本门只碰 AttributeType / 签名 / 方法体指令,零 Resolve 调用。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                return asm.MainModule.AssemblyReferences.Select(r => r.Name).ToList();
            }
        }

        // 递归展开数组 / byref / 指针 / pinned / 泛型实参后逐一裁决
        // (List<VitalsDto> 这类成员的违例在**实参**上,不展开 = 漏报)。
        private static void VisitTypeRefs(string from, TypeReference t, string selfName,
                                          List<string> errs, int depth = 0)
        {
            if (t == null || depth > 32) return;   // 深度护栏:元数据类型图无环,32 层远超实际
            VerdictTypeRef(from, t, selfName, errs);
            if (t is ArrayType arr) VisitTypeRefs(from, arr.ElementType, selfName, errs, depth + 1);
            else if (t is ByReferenceType br) VisitTypeRefs(from, br.ElementType, selfName, errs, depth + 1);
            else if (t is Mono.Cecil.PointerType pt) VisitTypeRefs(from, pt.ElementType, selfName, errs, depth + 1);
            else if (t is PinnedType pin) VisitTypeRefs(from, pin.ElementType, selfName, errs, depth + 1);
            else if (t is GenericInstanceType git)
            {
                foreach (var a in git.GenericArguments)
                    VisitTypeRefs(from, a, selfName, errs, depth + 1);
            }
            else if (t is IModifierType imod)
            {
                // 2026-09-26 代码审查 Required-2:modreq/modopt 的**修饰符类型**本身须访
                // (如 `Sim.X modreq(…)`)—— 只递归 ElementType 会漏修饰符侧。
                VisitTypeRefs(from, imod.ModifierType, selfName, errs, depth + 1);
                if (t is TypeSpecification spec0 && spec0.ElementType != null &&
                    !ReferenceEquals(spec0.ElementType, t))
                    VisitTypeRefs(from, spec0.ElementType, selfName, errs, depth + 1);
            }
            else if (t is TypeSpecification ts && ts.ElementType != null &&
                     !ReferenceEquals(ts.ElementType, t))
                VisitTypeRefs(from, ts.ElementType, selfName, errs, depth + 1);
            // 忽略面登记(2026-09-26 审查):FunctionPointerType 仅经 ReturnType 间接覆盖;
            // CustomAttribute 的 **构造参数 blob**(如 [Attr(typeof(Sim.X))])在 Deferred 下
            // 不读 ⇒ metadata 不判,由源文本层兜(全名必出现);ExportedType(TypeForwardedTo)
            // 暂落 selfName(本仓未用转发,休眠)。
        }

        /// <summary>① 的单一裁决点:一个类型引用是否触黑名单(装配 ∪ 契约禁名)。
        /// 输入全为值类型 ⇒ 负例夹具可直接喂合成行(纯函数)。</summary>
        public static string VerdictAudioReference(string fromType, string refAssembly,
                                                   string refNamespace, string refName)
        {
            if (AudioForbiddenAssemblies.Contains(refAssembly))
                return $"[b5①] {fromType} 引用「{refAssembly}」定义的类型「{refNamespace}.{refName}」—— " +
                       "零 TypeRef/MemberRef 解析到 Sim/Sim.Codec(AC-44-B1 ①,ADR-018 §一)。";
            if (AudioForbiddenContractTypes.Contains(refName) && IsSimFamilyNamespace(refNamespace))
                return $"[b5①] {fromType} 引用契约内 sim 权限类型「{refNamespace}.{refName}」—— " +
                       "六抽象点/SimEvent/VitalsDto 禁入 44(AC-44-B1 ①「含 IEventSink/IEventAuthority/SimEvent」," +
                       "ADR-018 §一;契约合法但对 44 是订阅 sim 真值的旁路)。";
            return null;
        }

        private static bool IsSimFamilyNamespace(string ns)
            => !string.IsNullOrEmpty(ns) &&
               (ns == "DaYiJingCheng.Sim" || ns.StartsWith("DaYiJingCheng.Sim.", StringComparison.Ordinal));

        private static void VerdictTypeRef(string from, TypeReference t, string selfName,
                                           List<string> errs)
        {
            if (t == null) return;
            var v = VerdictAudioReference(from, ScopeAssemblyName(t, selfName),
                                          t.Namespace ?? "", t.Name);
            if (v != null) errs.Add(v);
        }

        // 解析引用类型所属装配名(不 Resolve —— 只沿 Scope 走,免去程序集解析器)。
        private static string ScopeAssemblyName(TypeReference t, string selfName)
        {
            while (true)
            {
                if (t is GenericInstanceType git) { t = git.ElementType; continue; }
                if (t is TypeSpecification ts && ts.ElementType != null &&
                    !ReferenceEquals(ts.ElementType, t)) { t = ts.ElementType; continue; }
                break;
            }
            var scope = t.Scope;
            // ⚠️ 本包 Cecil(1.11.6):外部程序集的 IMetadataScope 实现 = AssemblyNameReference
            //    (无 AssemblyNameScope 类型 —— 已核实 dll 字符串表)。
            if (scope is AssemblyNameReference an) return an.Name;
            if (scope is ModuleDefinition md) return md.Assembly?.Name?.Name ?? selfName;
            return selfName;   // null / ModuleReference(同模块或 P/Invoke)→ 视为本装配
        }

        private static IEnumerable<TypeDefinition> AllTypes(ModuleDefinition module)
        {
            foreach (var t in module.Types)
            {
                yield return t;
                foreach (var n in NestedTypes(t)) yield return n;
            }
        }

        private static IEnumerable<TypeDefinition> NestedTypes(TypeDefinition t)
        {
            foreach (var n in t.NestedTypes)
            {
                yield return n;
                foreach (var nn in NestedTypes(n)) yield return nn;
            }
        }

        // 嵌套类型的 Namespace 字段可能为空 —— 沿 DeclaringType 回取(扫描键不因此漏嵌套类)。
        private static string EffectiveNamespace(TypeDefinition t)
        {
            while (string.IsNullOrEmpty(t.Namespace) && t.DeclaringType != null)
                t = t.DeclaringType;
            return t.Namespace ?? "";
        }

        // ── ②③ 装配引用集断言 ───────────────────────────────────────────────
        // 判序:黑名单(无条件红)→ 引擎白名单 → BCL → ADR-025 登记集 → 既有基线(WARN)
        // → 其余 = 新增漂移红。错误文本三段并列:实际集 / 基线 / ADR-025 §① 登记集。
        public static List<string> CheckPresentationReferenceSet(IEnumerable<string> refs,
                                                                out List<string> warnings)
        {
            warnings = new List<string>();
            var errs = new List<string>();
            var actual = (refs ?? Enumerable.Empty<string>())
                         .Where(r => !string.IsNullOrEmpty(r))
                         .Distinct().OrderBy(r => r, StringComparer.Ordinal).ToList();
            var actualText = string.Join(", ", actual);
            var baselineText = string.Join(", ", AudioRefBaseline);
            var registeredText = string.Join(", ", AudioRefRegistered);

            foreach (var r in actual)
            {
                if (AudioRefBlacklist.Any(b => r == b || r.StartsWith(b + ".", StringComparison.Ordinal)))
                {
                    var isDots = r.StartsWith("Unity.", StringComparison.Ordinal);
                    var why = isDots
                        ? "DOTS 家族(AC-44-B1 ③,ADR-017 §三)"
                        : "本装配引用黑名单(AC-44-B1 ②)";
                    errs.Add($"[{(isDots ? "b5③" : "b5②")}] Gameplay.Presentation 引用「{r}」∈ 黑名单({why})—— " +
                             "无条件红,不受基线豁免。" +
                             $"实际集=[{actualText}];基线=[{baselineText}];ADR-025 §① 登记集=[{registeredText}]。");
                    continue;
                }
                if (IsEngineRef(r) || IsBclRef(r)) continue;
                if (AudioRefRegistered.Contains(r)) continue;
                if (AudioRefBaseline.Contains(r))
                {
                    warnings.Add($"[b5-W] 引用「{r}」= 既有工程债(基线放行,WARN 不红)—— " +
                                 "Presentation→Sim 摘除归装配轮(Story 001 Out of Scope,另账)。");
                    continue;
                }
                errs.Add($"[b5②] Gameplay.Presentation 引用「{r}」不在 ADR-025 §① 登记集 ∪ " +
                         "引擎白名单 ∪ 既有基线 —— 引用集漂移(AC-44-B1 ②)。新增引用须先回写 " +
                         $"ADR-025 §① 再放行。实际集=[{actualText}];基线=[{baselineText}];" +
                         $"ADR-025 §① 登记集=[{registeredText}]。");
            }
            return errs;
        }

        // 引擎白名单:UnityEngine.* / UnityEditor.* / Unity.* 官方包(URP / Addressables /
        // Unity.ResourceManager 等 —— GDD AC-B1 ② 字面列举项皆在此前缀下)。
        // public(2026-09-26):InputBoundaryGates 复用(A6 引用集判据同族;b4/README 另
        // 有「编辑期封装不 public」注记,公开判定式属 b5 先例,二处互不冲突)。
        public static bool IsEngineRef(string r)
            => r == "UnityEngine" || r == "UnityEditor" ||
               r.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
               r.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
               r.StartsWith("Unity.", StringComparison.Ordinal);

        // BCL:netstandard / mscorlib / System* / Mono(核 —— ⚠️ 不含 "Mono." 前泛化:
        // Mono.Cecil 之类第三方库不得借 BCL 面溜进引用集)。
        // public(2026-09-26):InputBoundaryGates 复用(A6 引用集判据同族;与 IsEngineRef 同批公开)。
        public static bool IsBclRef(string r)
            => r.StartsWith("System", StringComparison.Ordinal) ||
               r == "netstandard" || r == "mscorlib" || r == "Mono";

        /// <summary>读 asmdef 的 references 数组;GUID: 形态经 AssetDatabase 解析为装配名
        /// (b3 教训:文件名 / GUID ≠ 装配名,一律取 asmdef JSON 的 name 字段)。
        /// 解析失败的 GUID 以原样入集 —— 漂移红会如实显示该 GUID,不静默丢。</summary>
        public static List<string> ReadDeclaredReferences(string asmdefPath)
        {
            var result = new List<string>();
            if (!File.Exists(asmdefPath)) return result;
            var json = File.ReadAllText(asmdefPath);
            var m = Regex.Match(json, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            if (!m.Success) return result;
            foreach (Match e in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
            {
                var token = e.Groups[1].Value;
                if (!token.StartsWith("GUID:", StringComparison.Ordinal))
                {
                    result.Add(token);
                    continue;
                }
                var guid = token.Substring("GUID:".Length);
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) &&
                    path.EndsWith(".asmdef", StringComparison.Ordinal) && File.Exists(path))
                {
                    var nm = Regex.Match(File.ReadAllText(path), "\"name\"\\s*:\\s*\"([^\"]+)\"");
                    result.Add(nm.Success ? nm.Groups[1].Value : token);
                }
                else result.Add(token);
            }
            return result;
        }

        // ── ①b 源文本层(b4 同格:不引 Roslyn,文本级)───────────────────────
        // 抓 using / nameof / 反射字符串形态(故事 QA「加 using ⇒ 必红」的兑现面)。
        // 注释剥离(文档性提及不算 —— 承 schema_types_primary_key_test 先例);
        // **字符串保留**(「Type.GetType("DaYiJingCheng.Sim.X")」正是命中面)。
        // 已知漏报面:逐字串 @"…" 内引号配对的朴素处理可能误判边界(误报方向偏红,
        // 且44 代码不用逐字串 —— 若实现期踩到,升级扫描器,不降判据)。
        public static List<string> CheckAudioSourceText(string fileLabel, IEnumerable<string> lines)
        {
            var errs = new List<string>();
            var text = StripCommentsPreserveStrings(string.Join("\n", lines));
            var raw = text.Split('\n');
            var nsPattern = new Regex(@"DaYiJingCheng\.Sim\b(?!\.Contracts)");
            for (var i = 0; i < raw.Length; i++)
            {
                var line = raw[i];
                var nsHit = nsPattern.Match(line);
                if (nsHit.Success)
                    errs.Add($"[b5①] {fileLabel}:{i + 1} 源文本出现 Sim/Sim.Codec 命名空间引用" +
                             $"(「{nsHit.Value}」)—— using / nameof / 反射字符串形态同此命中" +
                             "(AC-44-B1 ①;故事 QA「加 using ⇒ 必红」)。");
                foreach (var name in AudioForbiddenContractTypes)
                {
                    if (!Regex.IsMatch(line, $@"\b{Regex.Escape(name)}\b")) continue;
                    errs.Add($"[b5①] {fileLabel}:{i + 1} 源文本出现禁入类型名「{name}」—— " +
                             "44 源禁引(AC-44-B1 ①「含 IEventSink/IEventAuthority/SimEvent」)。");
                }
            }
            return errs;
        }

        /// <summary>①b 逃逸谓词(2026-09-26 代码审查 Required-1):文件**含 44 契约类型
        /// token 却未声明 44 前缀命名空间** ⇒ 红(代码躲进根命名空间 = 四层扫描同键失配,
        /// 「字面绿 + 实质违」)。token 检查在剥注释后的文本上(文档性提及不算)。
        /// 公开纯函数,测试以合成文本直接喂。</summary>
        public static List<string> CheckAudioEscapeText(string fileLabel, string src)
        {
            var errs = new List<string>();
            var text = StripCommentsPreserveStrings(src);
            var hasDecl = Regex.IsMatch(text,
                @"namespace\s+" + Regex.Escape(AudioModuleNamespacePrefix) + @"\b");
            if (hasDecl) return errs;   // 声明了前缀 ⇒ 归 CheckAudioSourceText 管
            if (!Regex.IsMatch(text, @"\b(AudioCueDto|IAudioCueSink|AudioCueHandle|TierSource)\b"))
                return errs;
            errs.Add($"[b5①] {fileLabel} 引用 44 契约类型(AudioCueDto/IAudioCueSink/AudioCueHandle/TierSource)" +
                     $"却未声明前缀命名空间「{AudioModuleNamespacePrefix}」—— 代码躲进根命名空间," +
                     "四层扫描同键失配 = 逃逸(AC-44-B1 ①;2026-09-26 代码审查 Required-1)。");
            return errs;
        }

        /// <summary>①b 对 44 源文件树跑文本扫描。扫描面 = Gameplay.Presentation 下声明了
        /// 44 命名空间的 .cs(L4 既有文件在根命名空间且不含 44 契约 token ⇒ 由逃逸谓词放行;
        /// 含 token 不声明 ⇒ 逃逸红)。</summary>
        public static List<string> CheckAudioSourceFiles()
        {
            var errs = new List<string>();
            const string root = "Assets/" + "Gameplay.Presentation";   // 字面拼接避免 analyzer 误读
            if (!Directory.Exists(root))
            {
                errs.Add($"[b5①] 源扫描面缺失「{root}」—— 拒以空集冒充绿(AC-44-B1 ①)。");
                return errs;
            }
            var declPattern = new Regex(
                @"namespace\s+" + Regex.Escape(AudioModuleNamespacePrefix) + @"\b");
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var src = File.ReadAllText(f);
                if (!declPattern.IsMatch(src))
                {
                    // 2026-09-26 代码审查 Required-1(前缀逃逸 · BLOCKING):引用 44 契约类型
                    // 却**未声明 44 前缀**的文件 = 代码躲进根命名空间(IL/源文本/B3/B4 四层同键
                    // 于前缀,不设此段则「字面绿 + 实质违」)。WARN「0 命中」只盖空集,兜不住
                    // 逃逸 —— 本段是唯一兜底;头注「②③+B3/B4 兜底」的旧说法已废。
                    var escape = CheckAudioEscapeText(f, src);
                    errs.AddRange(escape);
                    continue;
                }
                errs.AddRange(CheckAudioSourceText(f, src.Replace("\r\n", "\n").Split('\n')));
            }
            return errs;
        }

        // 注释剥离 + 字符串/字符字面量保留的状态机(见 CheckAudioSourceText 头注)。
        // public(2026-09-26):InputBoundaryGates 的 CheckInputSourceText **复用同款状态机**
        // (两处各自 private 复制 45 行 = 双源腐化;b5 先例同格);public 而非 internal 是
        // 因 EditMode 测试装配(Sim.Contracts.Tests)经 public 面复现负例夹具的判定
        // (internal 面需另立 InternalsVisibleTo,为一条注释状态机不值得 —— 承
        // 本文件 :35「测试经 public 面直调检查器」口径)。
        public static string StripCommentsPreserveStrings(string text)
        {
            var sb = new StringBuilder(text.Length);
            var i = 0;
            var inStr = false;
            var inChar = false;
            var inBlock = false;
            while (i < text.Length)
            {
                var c = text[i];
                if (inBlock)
                {
                    if (c == '*' && i + 1 < text.Length && text[i + 1] == '/')
                    { inBlock = false; i += 2; continue; }
                    if (c == '\n') sb.Append('\n');   // 保行结构(行号不错位)
                    i++;
                    continue;
                }
                if (inStr)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < text.Length) { sb.Append(text[i + 1]); i += 2; continue; }
                    if (c == '"') inStr = false;
                    i++;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < text.Length) { sb.Append(text[i + 1]); i += 2; continue; }
                    if (c == '\'') inChar = false;
                    i++;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                { while (i < text.Length && text[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
                { inBlock = true; i += 2; continue; }
                if (c == '"') { inStr = true; sb.Append(c); i++; continue; }
                if (c == '\'') { inChar = true; sb.Append(c); i++; continue; }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // ── b5 编排:① + ①b + ② + ③ 一次跑完 ───────────────────────────────
        /// <summary>返回红错(构建失败级);warnings = WARN(基线工程债 / 44 零命中)。
        /// 测试与菜单 / 编译钩子共用同一入口。</summary>
        public static List<string> RunAudioBoundary(out List<string> warnings)
        {
            warnings = new List<string>();
            var errs = new List<string>();
            if (EditorUtility.scriptCompilationFailed)
            {
                errs.Add("[b5] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }

            var dll = ScriptAssemblyPath(PresentationAssemblyName);
            errs.AddRange(CheckAudioAssemblyIl(dll, AudioModuleNamespacePrefix, out var matched));
            if (matched == 0)
                warnings.Add($"[b5-W] 44 命名空间前缀「{AudioModuleNamespacePrefix}」在 " +
                             $"{PresentationAssemblyName} 内 0 命中 —— 44 实现类型尚不存在,① 对空集" +
                             "平凡成立(扫描键 = AudioModuleNamespacePrefix;实现期 0 命中转 FAIL)。");

            var asmdefPath = $"Assets/{PresentationAssemblyName}/{PresentationAssemblyName}.asmdef";
            if (!File.Exists(asmdefPath))
            {
                errs.Add($"[b5②] asmdef 缺失「{asmdefPath}」—— 声明引用面不可读,拒以空集冒充绿。");
            }
            else
            {
                var declared = ReadDeclaredReferences(asmdefPath);
                if (declared.Count == 0 && !Regex.Match(File.ReadAllText(asmdefPath),
                        "\"references\"").Success)
                    errs.Add($"[b5②] 「{asmdefPath}」无 references 键 —— 解析面异常,拒以空集冒充绿。");

                var compiled = File.Exists(dll)
                    ? ReadCompiledReferenceNames(dll)
                    : new List<string> { "<产物缺失>" };   // ① 已就此报红,此处不让引用面静默为空
                var union = declared.Concat(compiled).ToList();
                var refErrs = CheckPresentationReferenceSet(union, out var refWarns);
                errs.AddRange(refErrs);
                warnings.AddRange(refWarns);
            }

            errs.AddRange(CheckAudioSourceFiles());
            return errs;
        }

        [InitializeOnLoadMethod]
        private static void RegisterReloadHook() => EditorApplication.delayCall += () =>
        {
            // 2026-09-26 降噪(代码审查 Required-7c):域重载**只跑 b3/b2**(程序集名集合级,
            // O(1) 级);b4 源扫 / b5 Cecil+引用集全门**移出 reload 路径** —— 归
            // 菜单(RunMenu)· EditMode 测试 · CI(ADR-012 Editor 格)。理由:原每次域重载
            // 全跑 ⇒ 线性扫描成本 + 每刷固定 2 条 WARN(baseline Sim / 44 零命中)刷屏。
            // 编译失败时 b2/b3 自身有「程序集加载不到则跳过」口径,不重复报。
            var errs = RunAll();
            foreach (var e in errs) Debug.LogError(e);
        };

        // 2026-09-26(评审 G7):**构建前强制点** —— 3 的意图边界门与 b5 的处置事件/引用集门
        // 在此合并 fail-fast。AC-3-A6 / A7 / B3 字面的后果是「构建失败」;此前只有日志红行
        // 与 EditMode 测试两道证据,真出问题时**构建仍会成功**(门形同虚设)。挂
        // IPreprocessBuildWithReport ⇒ 违规 = 构建中止,红行进构建日志(CI 判失败)。
        // 载荷/A6/b5 面跑在此处的理由:这三面是 O(N 文件 × 类型树) 级,放 reload 会刷屏;
        // 构建期跑一次是它们本来的位置。b3 清单面(O(1))仍在 reload 路径。
        private sealed class BuildGate : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                var errs = new List<string>();
                errs.AddRange(AssemblyGates.RunAll());
                AssemblyGates.CheckToFloatCallsites(errs);
                errs.AddRange(AssemblyGates.RunAudioBoundary(out _));
                errs.AddRange(InputBoundaryGates.RunAll(out var roots));
                if (roots == 0)
                    errs.Add("[InputBoundaryGates] A7 扫描根为 0 —— 扫描面丢失(假绿面),构建中止。");
                if (errs.Count == 0) return;
                foreach (var e in errs) Debug.LogError(e);
                throw new BuildFailedException(
                    $"[AssemblyGates] 构建前门失败 {errs.Count} 条(装配封闭性 / ToFloat 白名单 / " +
                    "44 边界门 / 3 意图边界门)—— 见构建日志红行。");
            }
        }
    }
}
#endif
