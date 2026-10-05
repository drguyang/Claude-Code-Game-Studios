// diagnosis-system Story 001 —— 8 诊断模块装配边界门(编辑期断言;不进构建)。
//
// 权威来源:
//   Story: production/epics/diagnosis-system/story-001-boundary-vitalsdto-float-guardrails.md
//     · AC-8-1 静态半(8 程序集零 `Publish` 调用点 / 零 `IEventSink` 写入面)
//     · AC-8-2(引用面 = 零 sim 内部类型;浮点入口 = `Fix` 族仅 `ToFloat`)
//     · AC-8-3 静态半(字段零跨调用累积量 / 零 tick 类型持久字段 —— 反射面在测试侧)
//     · AC-8-4 8 侧(零 8→11 数据边)· 铁律③(零持久化 API 调用点)
//     · 铁律④(`EmitGrowth` 唯一出口形状 —— IL 调用点恰 = 1)
//     · AC-8-6(G-1 超越函数零调用点;Sqrt 2026-09-23 订正同禁)
//     · G-2 / AC-8-3 四类合法输入(零 RNG / 帧钟 / 墙钟)· TR-diag-019/020 前置
//   GDD design/gdd/diagnosis-system.md 边界五条铁律 · F-8.6 · AC-8-1…6
//   ADR-025 §①(8 落 Gameplay.Presentation,L4 边界层)· control-manifest 本层 Forbidden 行
//
// 机制(复用 AssemblyGates b5 已趟平的路线,承 audio story-001):
//   · Cecil `ReadingMode.Deferred` + `InMemory = true` —— 零 Resolve,只碰签名 / 指令操作数;
//   · `scriptCompilationFailed` 假绿防护 + 产物缺失红 + 扫描键 0 命中红;
//   · 源文本层(剥注释保字符串)兜 IL 的漏报面(using / nameof / 反射字符串),逃逸谓词
//     兜「代码躲进根命名空间」的四层同键失配(audio Required-1 同型);
//   · 谓词均为纯函数(dllPath / 行集 ⇒ 错误列表),负例夹具可端到端注入真 IL。
//   ⚠️ 各域门各持自己的谓词(音频黑名单 ≠ 8 黑名单),遍历纪律同款 —— 承
//   InputBoundaryGates B3 先例;`ScopeAssemblyName` / `VisitTypeRefs` 因 b5 对应件为
//   private,此处**独立实现**(不为一条遍历破坏封装),语义与 b5 逐行对齐
//   (含 b5 Required-2 的 modreq/modopt 修饰符侧 —— 2026-10-05 评审补回;差异一处:
//   深度超限本门**落红**,b5 行为未核,以本门纪律为准)。
//
// 扫描键单一出处 = `DiagnosisModuleNamespacePrefix`:8 的实现类型必须住其下,否则四层
//   (IL 类型 / IL 调用 / 源文本 / 逃逸)同键失配 —— 由 `CheckSourceEscape` 兜底。
//   ⚠️ **预期纪律(2026-10-05 评审登记)**:8 的**消费者**(调用 DiagnosisVitalsFacade /
//   DiagnosisGrowthExit 的呈现路由)亦须住前缀 —— 前缀外引用 8 公开 API 触发 [D-ESC],
//   非误报而是设计意图(与 audio 同形;全树现零命中,story-006 若把呈现路由放前缀外,
//   依红行迁入,勿读作误伤)。另:**构造性绕行已登记非本批缺陷** —— `typeof(Fix)`(ldtoken
//   只查装配名)与按名反射可绕 IL/源双层 D-FIX,8 侧无动机,列 11 / 005 轮复查项。
//
// 分工:本门(Cecil + 源文本)挂菜单(RunMenu)+ 构建前门(BuildGate);字段累积量扫描 /
//   位宽(double)扫描 / DtoGuard 枚举为**反射面**,落 EditMode 测试(承 audio B3/B4 分工)。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using UnityEditor;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>diagnosis-system(8)的装配边界门 —— AC-8-1/8-2/8-3 静态半 + AC-8-4 +
    /// 铁律③④ + AC-8-6 的编辑期强制点(story-001)。</summary>
    public static class DiagnosisBoundaryGates
    {
        /// <summary>8 实现类型的命名空间前缀(扫描键单一出处;实现类型必须住其下)。</summary>
        public const string DiagnosisModuleNamespacePrefix =
            "DaYiJingCheng.Gameplay.Presentation.Diagnosis";

        private const string GrowthExitTypeName = "DiagnosisGrowthExit";
        private const string FacadeTypeName = "DiagnosisVitalsFacade";
        private const string QueryInterfaceName = "IVitalsQuery";
        private const string GrowthEmitterFullName =
            "DaYiJingCheng.Sim.Contracts.SkillSystem.SkillGrownEmitter";

        /// <summary>AC-8-2:`Fix` 族前缀(StartsWith 判 —— 亦盖 <c>FixParse</c> 与嵌套类型;
        /// 8 的浮点入口仅 <c>ToFloat</c>,其余成员访问 / 解析 / 原始常量全红)。</summary>
        private const string FixFamilyPrefix = "DaYiJingCheng.Sim.Contracts.Fix";

        /// <summary>① 装配黑名单:8 零 sim 内部类型(AC-8-2)。</summary>
        private static readonly string[] ForbiddenAssemblies = { "Sim", "Sim.Codec" };

        /// <summary>AC-8-6a / G-1:libm 超越函数宿主类型。
        /// ⚠️ <c>Sqrt</c> 依 2026-09-23 订正(原「Sqrt 许可」作废 —— 返回 double,8 侧
        /// 曲线走预计算定表)。
        /// ⚠️ <c>UnityEngine.Mathf</c> 2026-10-05 评审补入:G-4 禁 double ⇒ 作者有强动机
        /// 选返回 float 的 <c>Mathf</c> 而非 <c>Math</c> —— 漏它 = G-1「族禁」被
        /// 「邀请式绕行」实质击穿(同装配 <c>Camera/CameraRig.cs</c> 已在用 <c>Mathf.Sin</c>)。</summary>
        private static readonly string[] LibmTypes = { "System.Math", "System.MathF", "UnityEngine.Mathf" };

        /// <summary>G-1「等超越函数」成员集:幂 / 指对数 / 开方 + 三角双曲全集
        /// (AC 字面举 Pow/Exp/Log/Cbrt,Sin/Cos/Atan… 同属 libm 超越函数,一并禁;
        /// Abs/Min/Max/Floor/Round 非超越函数,不在禁列)。</summary>
        private static readonly string[] LibmMethods =
        {
            "Pow", "Exp", "Log", "Log2", "Log10", "Cbrt", "Sqrt",
            "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2",
            "Sinh", "Cosh", "Tanh",
        };

        /// <summary>铁律③ 持久化 / 文件 IO 宿主(「8 什么都没存」;`Sim.Codec` 由装配黑名单
        /// 兜,`System.IO.Directory` 与 File 同族补入)。</summary>
        private static readonly string[] PersistenceTypes =
        {
            "UnityEngine.PlayerPrefs", "UnityEngine.JsonUtility",
            "System.IO.File", "System.IO.FileStream", "System.IO.StreamWriter",
            "System.IO.BinaryWriter", "System.IO.Directory",
        };

        /// <summary>G-2 / AC-8-3 四类合法可变输入之外的时钟与 RNG
        /// (承 audio D7 名单;四类 = QueryLevel / VitalsDto / SimEvent 阈值 / 当前 tick)。
        /// ⚠️ 2026-10-05 评审补 <c>DateTimeOffset</c> / <c>Stopwatch</c>(与 audio 名单的
        /// 偏离以本注为准);<c>Environment.TickCount</c> 走 VerdictMethod 名键特判,
        /// 不入本表(<c>System.Environment</c> 整类型入表会误伤 GetEnvironmentVariable 等)。</summary>
        private static readonly string[] ClockTypes =
        {
            "UnityEngine.Random", "System.Random",
            "UnityEngine.Time", "System.DateTime",
            "System.DateTimeOffset", "System.Diagnostics.Stopwatch",
        };

        // ── 编排:① IL 层 + ② 源文本层(含逃逸谓词)一次跑完 ────────────────────
        /// <summary>返回红错(构建失败级);warnings 预留(本门当前无 WARN 级发现)。
        /// 菜单 / 构建前门 / EditMode 测试共用同一入口。</summary>
        public static List<string> RunAll(out List<string> warnings)
        {
            warnings = new List<string>();
            var errs = new List<string>();
            if (EditorUtility.scriptCompilationFailed)
            {
                errs.Add("[D-0] EditorUtility.scriptCompilationFailed = true ⇒ 拒扫陈旧产物" +
                         "(假绿防护:读上一版 DLL = 假绿;红在编译器,本门只声明不可判)。");
                return errs;
            }

            var dll = AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName);
            errs.AddRange(CheckDiagnosisIl(dll, DiagnosisModuleNamespacePrefix,
                                           out var matched, out var emitCalls));
            // 2026-10-05 评审订正:产物缺失时 CheckDiagnosisIl 已红 —— matched == 0
            // 是其连带结果,不再叠报「扫描键 0 命中」(真因在上一条,叠报误导排障)。
            if (!File.Exists(dll))
            {
                // 缺失红已由 CheckDiagnosisIl 产出;emitCalls 失配同理不可判,跳过。
            }
            else if (matched == 0)
                errs.Add($"[D-0] 扫描键「{DiagnosisModuleNamespacePrefix}」在 " +
                         $"{AssemblyGates.PresentationAssemblyName} 内 0 命中 —— 8 实现类型缺失" +
                         " / 躲进其他命名空间,扫描面丢失(story-001 已落类型 ⇒ 空集即红," +
                         "不作 WARN)。");
            else if (emitCalls != 1)
                errs.Add($"[D-EXIT] 8 内 SkillGrownEmitter.EmitGrowth 调用点 = {emitCalls}" +
                         $"(须恰 = 1 且在 {GrowthExitTypeName})—— 铁律④「存在唯一出口形状」" +
                         "(TR-diag-005;调用语义归 story 005)。");

            errs.AddRange(CheckSourceFiles());
            return errs;
        }

        // ── ① IL 层:8 前缀下的类型签名 + 方法体指令裁决 ────────────────────────
        /// <summary>对编译产物做 8 命名空间前缀下的 metadata 扫描。谓词纯函数 ——
        /// 对**测试装配**产物(内含 8 前缀负例夹具)跑同一入口,真 IL 必红。
        /// <para>覆盖面:签名(基类 / 接口 / 泛型约束 / 特性 / 字段 / 方法返回与参数)+
        /// 方法体指令操作数(call / callvirt / newobj / ldsfld / ldstr 除外 …)的
        /// TypeRef / MemberRef / MethodSpec / TypeSpec / CallSite 全面;
        /// catch 类型独立扫(承 audio Required-2)。
        /// 已知漏报面:纯字符串反射 —— 由源文本层 <see cref="CheckSourceText"/> 兜,
        /// 双层缺一不可。</para></summary>
        /// <param name="dllPath">编译产物绝对路径。</param>
        /// <param name="nsPrefix">扫描键(命名空间前缀)。</param>
        /// <param name="matchedTypeCount">前缀下命中类型数(0 ⇒ 扫描面丢失)。</param>
        /// <param name="emitGrowthCallsites">前缀下 <c>SkillGrownEmitter.EmitGrowth</c>
        /// 调用点总数(须恰 = 1,铁律④)。</param>
        public static List<string> CheckDiagnosisIl(string dllPath, string nsPrefix,
                                                    out int matchedTypeCount,
                                                    out int emitGrowthCallsites)
        {
            matchedTypeCount = 0;
            emitGrowthCallsites = 0;
            var errs = new List<string>();
            if (!File.Exists(dllPath))
            {
                errs.Add($"[D-0] 编译产物缺失「{dllPath}」—— 扫描面不存在,拒以空集冒充绿" +
                         "(AC-8-2)。");
                return errs;
            }

            using (var asm = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters
            {
                // ⚠️ 必须 Deferred(实测承 b5):Immediate 会急切读取特性构造参数并要求
                // 程序集解析器可解析 netstandard —— 编辑器与独立进程都会
                // AssemblyResolutionException。Deferred 下本门零 Resolve 调用。
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
            }))
            {
                var selfName = asm.Name.Name;
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    if (!AssemblyGates.IsInNamespacePrefix(
                            AssemblyGates.EffectiveNamespace(type), nsPrefix)) continue;
                    matchedTypeCount++;
                    var from = $"{selfName}::{type.FullName}";

                    // ── 签名面 ──
                    VisitTypeRefs(from, type.BaseType, selfName, errs);
                    foreach (var ii in type.Interfaces)
                        VisitTypeRefs(from, ii.InterfaceType, selfName, errs);
                    if (type.HasGenericParameters)
                        foreach (var gp in type.GenericParameters)
                            foreach (var c in gp.Constraints)
                                VisitTypeRefs(from, c.ConstraintType, selfName, errs);
                    foreach (var ca in type.CustomAttributes)
                        VisitTypeRefs(from, ca.AttributeType, selfName, errs);

                    foreach (var f in type.Fields)
                    {
                        VisitTypeRefs(from, f.FieldType, selfName, errs);
                        foreach (var ca in f.CustomAttributes)
                            VisitTypeRefs(from, ca.AttributeType, selfName, errs);
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
                            VisitTypeRefs(from, ca.AttributeType, selfName, errs);
                        if (!m.HasBody) continue;

                        foreach (var instr in m.Body.Instructions)
                        {
                            switch (instr.Operand)
                            {
                                case TypeReference tr:
                                    VisitTypeRefs(from, tr, selfName, errs);
                                    break;
                                case MethodReference mr:
                                    VerdictMethod(from, type, mr, selfName, errs,
                                                  ref emitGrowthCallsites);
                                    break;
                                case FieldReference fr:
                                    VerdictField(from, fr, selfName, errs);
                                    break;
                                case CallSite cs:
                                    // calli 的 operand 是 CallSite(非 MethodReference 子类)
                                    // —— 承 audio Required-2 漏判教训。
                                    VisitTypeRefs(from, cs.ReturnType, selfName, errs);
                                    foreach (var cp in cs.Parameters)
                                        VisitTypeRefs(from, cp.ParameterType, selfName, errs);
                                    break;
                            }
                        }

                        foreach (var eh in m.Body.ExceptionHandlers)
                            if (eh.CatchType != null)
                                VisitTypeRefs(from, eh.CatchType, selfName, errs);
                    }
                }
            }
            return errs;
        }

        // ── 类型面裁决(装配黑名单 ∪ IEventSink 禁名)──────────────────────────
        private static void VerdictTypeRef(string from, TypeReference t, string selfName,
                                           List<string> errs)
        {
            if (t == null) return;
            var asm = ScopeAssemblyName(t, selfName);
            if (ForbiddenAssemblies.Contains(asm))
                errs.Add($"[D-TREF] {from} 引用「{asm}」定义的类型「{t.FullName}」—— " +
                         "8 零 sim 内部类型(AC-8-2)。");
            var name = t.Name;
            var ns = t.Namespace ?? "";
            if (name == "IEventSink" && IsSimFamilyNamespace(ns))
                errs.Add($"[D-TREF] {from} 引用写通道契约 IEventSink —— " +
                         "8 零写入面(铁律② / AC-8-1;持有即可能写,类型面直接禁)。");
            // ⚠️ IVitalsQuery 的**类型**引用合法(依赖注入持有)—— 门面收口断在调用点
            // GetVitals(见 VerdictMethod [D-FACADE]),不在类型面。
        }

        // ── 调用面裁决(每条谓词对应一条 AC 字面)────────────────────────────────
        private static void VerdictMethod(string from, TypeDefinition enclosingType,
                                          MethodReference mr, string selfName,
                                          List<string> errs, ref int emitGrowthCallsites)
        {
            var declaring = mr.DeclaringType;
            var dFull = declaring?.FullName ?? "";
            var name = mr.Name;

            // AC-8-1 静态守门:8 程序集零 Publish 调用点(不限目标类型 —— 名字即守门键)
            if (name.StartsWith("Publish", StringComparison.Ordinal))
                errs.Add($"[D-PUB] {from} 调用「{dFull}::{name}」—— 8 程序集零 Publish " +
                         "调用点(AC-8-1 静态守门;45 联机发布面禁入 8)。");

            // 铁律③:零持久化 / IO API 调用点
            if (Array.IndexOf(PersistenceTypes, dFull) >= 0)
                errs.Add($"[D-PERSIST] {from} 调用持久化 / IO API「{dFull}::{name}」—— " +
                         "8 零持久化(铁律③「8 什么都没存」)。");

            // AC-8-6a / G-1:libm 超越函数零调用点
            if (Array.IndexOf(LibmTypes, dFull) >= 0 && Array.IndexOf(LibmMethods, name) >= 0)
                errs.Add($"[D-G1] {from} 调用 libm 超越函数「{dFull}::{name}」—— " +
                         "G-1 禁(AC-8-6);8 侧曲线走预计算定表(2026-09-23 订正:Sqrt 同禁)。");

            // G-2 / AC-8-3:时钟与 RNG(合法可变输入仅四类)
            if (Array.IndexOf(ClockTypes, dFull) >= 0)
                errs.Add($"[D-CLK] {from} 调用时钟 / RNG「{dFull}::{name}」—— " +
                         "G-2 禁随机;四类合法可变输入之外的时基源(AC-8-3)。");

            // 2026-10-05 评审补:Environment.TickCount / TickCount64 墙钟 —— 名键特判
            // (System.Environment 整类型入 ClockTypes 会误伤非时基成员)
            if (dFull == "System.Environment" &&
                (name == "TickCount" || name == "TickCount64"))
                errs.Add($"[D-CLK] {from} 调用墙钟「{dFull}::{name}」—— " +
                         "G-2 / AC-8-3 四类合法可变输入之外的时基源。");

            // AC-8-2:浮点入口 = Fix 族仅 ToFloat(属性 / 运算符 / 解析全走本判)
            if (dFull.StartsWith(FixFamilyPrefix, StringComparison.Ordinal) && name != "ToFloat")
                errs.Add($"[D-FIX] {from} 访问定点域成员「{name}」({dFull})—— " +
                         "8 的浮点入口仅 ToFloat(AC-8-2;8 不做定点算术)。");

            // 铁律④:SkillGrownEmitter.EmitGrowth 唯一调用点在 DiagnosisGrowthExit
            if (dFull == GrowthEmitterFullName && name == "EmitGrowth")
            {
                emitGrowthCallsites++;
                if (!(enclosingType != null && enclosingType.Name == GrowthExitTypeName &&
                      AssemblyGates.IsInNamespacePrefix(
                          AssemblyGates.EffectiveNamespace(enclosingType),
                          DiagnosisModuleNamespacePrefix)))
                    errs.Add($"[D-EXIT] {from} 直接调用 SkillGrownEmitter.EmitGrowth —— " +
                             $"8 内唯一调用点在 {GrowthExitTypeName}" +
                             "(铁律④ / TR-diag-005「存在唯一出口形状」)。");
            }

            // 门面收口:IVitalsQuery.GetVitals 调用仅限 DiagnosisVitalsFacade
            if (declaring != null && declaring.Name == QueryInterfaceName &&
                name == "GetVitals")
            {
                if (!(enclosingType != null && enclosingType.Name == FacadeTypeName &&
                      AssemblyGates.IsInNamespacePrefix(
                          AssemblyGates.EffectiveNamespace(enclosingType),
                          DiagnosisModuleNamespacePrefix)))
                    errs.Add($"[D-FACADE] {from} 调用 IVitalsQuery.GetVitals —— " +
                             $"8 的取数唯一入口是 {FacadeTypeName}.Read" +
                             "(control-manifest「输入只进门面」;绕开 = 门面死代码)。");
            }

            // 调用引用携带的类型面(声明类型 / 返回 / 参数 / 泛型实参)
            if (declaring != null) VisitTypeRefs(from, declaring, selfName, errs);
            VisitTypeRefs(from, mr.ReturnType, selfName, errs);
            foreach (var p in mr.Parameters)
                VisitTypeRefs(from, p.ParameterType, selfName, errs);
            if (mr is GenericInstanceMethod gim)
                foreach (var ga in gim.GenericArguments)
                    VisitTypeRefs(from, ga, selfName, errs);
        }

        // ── 字段访问面裁决(Fix 族字段 / 时钟字段)──────────────────────────────
        private static void VerdictField(string from, FieldReference fr, string selfName,
                                         List<string> errs)
        {
            var dFull = fr.DeclaringType?.FullName ?? "";
            if (dFull.StartsWith(FixFamilyPrefix, StringComparison.Ordinal))
                errs.Add($"[D-FIX] {from} 访问定点域字段「{fr.Name}」({dFull})—— " +
                         "8 的浮点入口仅 ToFloat(AC-8-2)。");
            if (Array.IndexOf(ClockTypes, dFull) >= 0)
                errs.Add($"[D-CLK] {from} 读取时钟 / RNG 字段「{dFull}::{fr.Name}」—— " +
                         "G-2 / AC-8-3 四类合法可变输入之外的时基源。");

            if (fr.DeclaringType != null) VisitTypeRefs(from, fr.DeclaringType, selfName, errs);
            VisitTypeRefs(from, fr.FieldType, selfName, errs);
        }

        // ── 类型引用遍历(泛型 / 数组 / byref / 指针 / 修饰符递归;与 b5 语义对齐)──
        private static void VisitTypeRefs(string from, TypeReference t, string selfName,
                                          List<string> errs, int depth = 0)
        {
            if (t == null) return;
            // 深度护栏:超限**红行可见**(2026-10-05 评审订正 —— 原静默 return 与
            //「超限不静默」注释相抵;承 PresentationDtoGuard 超限落红的纪律。
            // 元数据类型图到不了 48 层,触达即异常,拒静默截断。)
            if (depth > 48)
            {
                errs.Add($"[D-0] {from} 类型引用图深度 > 48 —— 遍历截断," +
                         "拒静默(触达即元数据异常,红行可见)。");
                return;
            }
            VerdictTypeRef(from, t, selfName, errs);
            if (t is GenericInstanceType git)
            {
                foreach (var ga in git.GenericArguments)
                    VisitTypeRefs(from, ga, selfName, errs, depth + 1);
                VisitTypeRefs(from, git.ElementType, selfName, errs, depth + 1);
                return;
            }
            // modreq / modopt 的**修饰符类型本身须访**(b5 Required-2 已修之漏判,
            // 2026-10-05 评审补入 —— 只递归 ElementType 会漏修饰符侧,携
            // modreq(Sim.*) 的 IL 可静默绿)。IModifierType 亦是 TypeSpecification,
            // 下方通用分支同时递归被修饰类型,两侧都走。
            if (t is IModifierType imod && imod.ModifierType != null &&
                !ReferenceEquals(imod.ModifierType, t))
                VisitTypeRefs(from, imod.ModifierType, selfName, errs, depth + 1);
            if (t is TypeSpecification ts && ts.ElementType != null &&
                !ReferenceEquals(ts.ElementType, t))
                VisitTypeRefs(from, ts.ElementType, selfName, errs, depth + 1);
        }

        /// <summary>解析引用类型所属装配名(不 Resolve —— 只沿 Scope 走)。
        /// ⚠️ 与 b5 私有件 `AssemblyGates.ScopeAssemblyName` 语义逐行对齐(该件为
        /// private,此处独立实现 —— 承 InputBoundaryGates 同型分工)。</summary>
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
            if (scope is AssemblyNameReference an) return an.Name;
            if (scope is ModuleDefinition md) return md.Assembly?.Name?.Name ?? selfName;
            return selfName;   // null / ModuleReference → 视为本装配
        }

        private static bool IsSimFamilyNamespace(string ns)
            => !string.IsNullOrEmpty(ns) &&
               (ns == "DaYiJingCheng.Sim" ||
                ns.StartsWith("DaYiJingCheng.Sim.", StringComparison.Ordinal));

        // ── ② 源文本层:兜 IL 漏报面(注释剥离 / 字符串保留)────────────────────
        /// <summary>对声明了 8 前缀的源文件跑逐行谓词。剥注释(文档性提及不算 ——
        /// 承 schema_types / audio 先例);**保留字符串**(反射字符串正是命中面)。</summary>
        public static List<string> CheckSourceText(string fileLabel, IEnumerable<string> lines)
        {
            var errs = new List<string>();
            var text = AssemblyGates.StripCommentsPreserveStrings(string.Join("\n", lines));
            var raw = text.Split('\n');
            var rules = new (string Tag, Regex Pattern, string Why)[]
            {
                ("[D-TREF]", new Regex(@"DaYiJingCheng\.Sim\b(?!\.Contracts)"),
                 "sim 内部命名空间(AC-8-2 零 sim 内部类型;using / nameof / 反射字符串同此命中)"),
                ("[D-TREF]", new Regex(@"\bIEventSink\b"),
                 "写通道契约(铁律② / AC-8-1 零写入)"),
                ("[D-PUB]", new Regex(@"\bPublish\w*\s*\("),
                 "Publish 调用点(AC-8-1 静态守门)"),
                ("[D-G1]", new Regex(
                    @"\b(?:System\.)?(?:Math|MathF|Mathf)\s*\.\s*(?:Pow|Exp|Log|Log2|Log10|Cbrt|Sqrt|Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Sinh|Cosh|Tanh)\b"),
                 "libm 超越函数(AC-8-6 / G-1;Sqrt 2026-09-23 订正同禁;" +
                 "Mathf 2026-10-05 评审补 —— G-4 禁 double 下的邀请式绕行面)"),
                ("[D-PERSIST]", new Regex(@"\bPlayerPrefs\b|\bJsonUtility\b|\bSystem\.IO\b"),
                 "持久化 / 文件 IO(铁律③)"),
                ("[D-CLK]", new Regex(
                    @"\bUnityEngine\.Random\b|\bSystem\.Random\b|\bUnityEngine\.Time\b|Time\.(?:deltaTime|realtimeSinceStartup|unscaledDeltaTime|time|frameCount)\b|Date(?:Time|TimeOffset)\.(?:Now|UtcNow|Today)\b|\bStopwatch\.(?:StartNew|GetTimestamp)\b|Environment\.(?:TickCount64|TickCount)\b"),
                 "时钟 / RNG(G-2 · AC-8-3 四类合法可变输入之外的时基源;" +
                 "DateTimeOffset / Stopwatch / TickCount 2026-10-05 评审补)"),
                ("[D-FIX]", new Regex(@"\bFixParse\b|\.OneRaw\b|\.ZeroRaw\b"),
                 "定点域解析 / 原始常量(浮点入口仅 ToFloat,AC-8-2;OneRaw/ZeroRaw 为 const,IL 不可见)"),
                ("[D-11]", new Regex(@"(?i)\bprescri"),
                 "8→11 数据边(铁律① / AC-8-4 本 story 断 8 侧;病名不给 11)"),
            };
            for (var i = 0; i < raw.Length; i++)
                foreach (var (tag, pattern, why) in rules)
                {
                    var m = pattern.Match(raw[i]);
                    if (m.Success)
                        errs.Add($"{tag} {fileLabel}:{i + 1} 命中「{m.Value}」—— {why}。");
                }
            return errs;
        }

        /// <summary>逃逸谓词:文件含 8 类型 token(`Diagnosis[A-Z]…`)却未声明 8 前缀
        /// 命名空间 ⇒ 红(代码躲进根命名空间 = 四层扫描同键失配,「字面绿 + 实质违」)。
        /// token 检查在剥注释后的文本上(文档性提及不算)。公开纯函数,测试以合成文本直喂。</summary>
        public static List<string> CheckSourceEscape(string fileLabel, string src)
        {
            var errs = new List<string>();
            var text = AssemblyGates.StripCommentsPreserveStrings(src);
            if (Regex.IsMatch(text,
                    @"namespace\s+" + Regex.Escape(DiagnosisModuleNamespacePrefix) + @"\b"))
                return errs;   // 声明了前缀 ⇒ 归 CheckSourceText 管
            if (!Regex.IsMatch(text, @"\bDiagnosis[A-Z]\w*"))
                return errs;
            errs.Add($"[D-ESC] {fileLabel} 含 8 类型 token「Diagnosis[A-Z]…」却未声明前缀" +
                     $"命名空间「{DiagnosisModuleNamespacePrefix}」—— 代码躲进其他命名空间," +
                     "四层扫描同键失配 = 逃逸(audio Required-1 同型)。");
            return errs;
        }

        /// <summary>对 8 源文件树跑文本扫描。扫描面 = Gameplay.Presentation 下的 .cs:
        /// 声明 8 前缀 ⇒ 逐行谓词;否则 ⇒ 逃逸谓词放行 / 拦截。</summary>
        public static List<string> CheckSourceFiles()
        {
            var errs = new List<string>();
            const string root = "Assets/" + "Gameplay.Presentation";   // 字面拼接防误读
            if (!Directory.Exists(root))
            {
                errs.Add($"[D-0] 源扫描面缺失「{root}」—— 拒以空集冒充绿(AC-8-2)。");
                return errs;
            }
            var declPattern = new Regex(
                @"namespace\s+" + Regex.Escape(DiagnosisModuleNamespacePrefix) + @"\b");
            var declCount = 0;
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var src = File.ReadAllText(f);
                if (!declPattern.IsMatch(src))
                {
                    errs.AddRange(CheckSourceEscape(f, src));
                    continue;
                }
                declCount++;
                errs.AddRange(CheckSourceText(f, src.Replace("\r\n", "\n").Split('\n')));
            }
            if (declCount == 0)
                errs.Add($"[D-0] 扫描面内 0 个文件声明前缀命名空间" +
                         $"「{DiagnosisModuleNamespacePrefix}」—— 扫描键失配(8 源文件缺失 /" +
                         " 躲进其他命名空间),拒以空集冒充绿。");
            return errs;
        }
    }
}
#endif
