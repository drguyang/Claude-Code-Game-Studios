// Story 007 · AC-3-B2② —— 直读通道 UI 事件符号零引用门(Roslyn 分析器)
//
// 权威来源:
//   story-007-direct-read-channel.md AC-3-B2②(BLOCKING):**Roslyn 拒 UI 事件符号**
//     (编译期拒,非 grep)—— 测试源含 UI 事件 API 引用(EventSystem / NavigationMoveEvent
//     处理订阅)⇒ 以本分析器编译必须失败且诊断指向该引用;合法 Input System 写法通过。
//   QA Test Cases AC-3-B2②:全限定名与 using 短名两种写法均拒;别名/反射字符串不算
//     (编译期符号面);负例 = 只 `using UnityEngine.EventSystems` 不触符号 ⇒ 不误报。
//   control-manifest / story Control Manifest Rules:Forbidden「直读程序集引用
//     UI Toolkit / UnityEngine.UI / EventSystem(AC-B2①,构建失败)」—— 本分析器是该
//     禁令的**源码符号面**(B2① 是 asmdef 引用集结构面;二者同属「不穿 42」三道门)。
//   ADR-011 §二:直读通道独立于 42 UI 栈(方差不是算术)。
//
// 为什么 B2①(asmdef 门)不够、必须有本分析器(两门的分工,2026-09-26 grounding):
//   1. UI Toolkit 运行期住**引擎内置模块预编译 DLL**(UnityEngine.UIElementsModule.dll /
//      UnityEngine.UIModule.dll)—— 引用集闭包只读 Assets/ 树 asmdef,引擎自动引用面
//      **在 asmdef 图里不可见** ⇒ `using UnityEngine.UIElements` 不改任何 asmdef 却能编译;
//      只有源码级符号门能拦。
//   2. `InputSystemUIInputModule`(UnityEngine.InputSystem.UI)住 **Unity.InputSystem**
//      装配 —— Gameplay.Input 引用 Unity.InputSystem 是合法的(要 InputAction),但引其中的
//      UI 桥 = 把输入重新接回 UI 事件栈;装配级门按构造就分不出「InputAction 合法 /
//      UI 桥非法」,必须按符号判。
//   3. 源码级门给出**指向该引用的行列位置**(编译器报错直指病灶),asmdef 门只能红在门本身。
//
// 作用域(⚠️ 与 DY0001 的关键差异 —— DY0001 是全 Assets 工程级禁令,本门**不是**):
//   本门只约束**直读程序集的源码**(路径含 `Assets/Gameplay.Input/` —— 与 B2① 的 root
//   = Gameplay.Input 同一根)。理由:42 UI 栈、Editor spike(Tools.Spike 的
//   U1SpikeSetup / U1FocusProbe)按设计就要引 UI 事件符号,工程级禁令会把合法 UI 代码
//   编译成红(引擎 / 包源排除同理,但那是另一维度:本门是「谁」的维度,DY0001 是「什么」的维度)。
//   负例面见测试:作用域外(如临时目录根)的同一样例必须编译通过。
//
// 构建纪律(同 LegacyInputAnalyzer,unity-specialist 预检 2026-09-25):
//   · 源码住 Assets 外(依赖 Microsoft.CodeAnalysis,进 Assets 会炸工程编译);
//   · 只对 Unity 捆绑 Microsoft.CodeAnalysis(.CSharp).dll(DotNetSdkRoslyn,4.3.x)编译;
//   · 产物 unity/Assets/Editor.Tools.Analyzers/UiEventSymbolAnalyzer.dll +
//     RoslynAnalyzer label(经 RoslynAnalyzerLabel.SetLabel,含 PluginImporter 校正)。
//
// 排除策略:本门无「包/引擎源排除」维度 —— 作用域白名单(Assets/Gameplay.Input/)已把
//   引擎 / 包 / 他装配全部排除在外;反向地,作用域内**永不**排除(与 DY0001 的
//   「Assets/ 永不排除」同理)。

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DaYiJingCheng.EditorTools.Analyzers
{
    /// <summary>编译期直读通道 UI 事件符号零引用门:直读程序集源码内引用 UI 栈类型符号即报
    /// DY0002(Error)。作用域 = 路径含 <c>Assets/Gameplay.Input/</c>(与 B2① 同根)。</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UiEventSymbolAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>本分析器的诊断 ID(测试按此断言,勿改)。</summary>
        public const string DiagnosticId = "DY0002";

        private const string Category = "Input";

        /// <summary>直读程序集源码路径键(B2① 的 root = Gameplay.Input;路径 contains 判定,
        /// 同时命中真工程绝对路径与测试夹具的 <c>…/Assets/Gameplay.Input/…</c> 相对形态)。</summary>
        private const string ScopePathKey = "Assets/Gameplay.Input/";

        /// <summary>禁引命名空间闭集(精确整串匹配,非前缀 —— <c>UnityEngine.UI</c> 与
        /// <c>UnityEngine.UIElements</c> 互不吞)。四条的分工见文件头「为什么 B2① 不够」。</summary>
        private static readonly HashSet<string> ForbiddenNamespaces = new HashSet<string>(StringComparer.Ordinal)
        {
            "UnityEngine.EventSystems",    // EventSystem 栈(AC 点名的「EventSystem」)
            "UnityEngine.UI",              // uGUI 组件 / onClick 事件(与 B2① 装配 UnityEngine.UI 对应)
            "UnityEngine.UIElements",      // UI Toolkit(含 NavigationMoveEvent;引擎模块面 asmdef 不可见)
            "UnityEngine.InputSystem.UI",  // InputSystemUIInputModule 桥(同装配内的 UI 回路由)
        };

        /// <summary>注册的操作种类(表达式静态类型可落在禁引类型的全部常规形态 +
        /// 成员引用面)。显式列举而非全枚举:None/未实现 kinds 会在 Initialize 期抛,
        /// 面外的冷僻 kinds(动态 / 服务定位)在直读通道里是不可达的构造。</summary>
        private static readonly OperationKind[] Kinds =
        {
            OperationKind.Invocation,          // ExecuteEvents.Execute(...) 等静态调用
            OperationKind.ObjectCreation,      // new EventSystem()
            OperationKind.PropertyReference,   // EventSystem.current
            OperationKind.FieldReference,
            OperationKind.EventReference,
            OperationKind.MethodReference,     // 方法组转换(同 DY0001 W3 补漏)
            OperationKind.LocalReference,      // 局部变量静态类型即禁引类型
            OperationKind.ParameterReference,
            OperationKind.InstanceReference,
            OperationKind.Conversion,          // EventSystem es = null;… as EventSystem
            OperationKind.Binary,              // e == null(操作数各自另有 LocalReference 面)
            OperationKind.SimpleAssignment,
            OperationKind.CompoundAssignment,
            OperationKind.TypeOf,              // typeof(UnityEngine.UIElements.NavigationMoveEvent)
            OperationKind.IsType,              // x is EventSystem
            OperationKind.DefaultValue,        // default(EventSystem)
            OperationKind.ArrayCreation,        // new EventSystem[1]
            OperationKind.ConditionalAccess,
            OperationKind.Coalesce,
            OperationKind.Tuple,
        };

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "UI event-stack symbol reference forbidden in direct-read channel",
            messageFormat: "UI event-stack symbol '{0}' is referenced in the direct-read channel sources - forbidden by AC-3-B2② (use the Input System direct-read path, never the 42 UI event stack)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "AC-3-B2② compile-time gate: any semantic reference (fully-qualified, using short name, " +
                         "typeof, member access, conversion, inheritance) to EventSystem / uGUI / UI Toolkit / " +
                         "InputSystem.UI symbols fails compilation when it appears in the direct-read assembly " +
                         "sources (Assets/Gameplay.Input/). UI code outside that scope is unaffected.");

        /// <summary>本分析器唯一可报的诊断(DY0002 · Error 级 ⇒ 编译失败)。</summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        /// <summary>注册:操作面(表达式类型 + 成员符号)+ 符号面(继承 / 实现接口 ——
        /// 基类型引用不产生 operation,须在类型符号上补)。</summary>
        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterOperationAction(AnalyzeOperation, Kinds);
            context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        }

        // ── 操作面:类型引用落在表达式静态类型 / typeof 操作数 / 成员所属类型 ──

        private static void AnalyzeOperation(OperationAnalysisContext ctx)
        {
            if (!IsInDirectReadScope(ctx.Operation.Syntax.SyntaxTree))
                return;

            // typeof / is —— 类型操作数本身(与 DY0001 同款:TypeOperand 才是操作数类型;
            // ITypeOfOperation.Type = typeof 表达式的求值类型 System.Type,读它分支恒失效)。
            if (ctx.Operation is ITypeOfOperation typeOf)
            {
                ReportIfGated(ctx, typeOf.TypeOperand, typeOf.TypeOperand?.Name + " (typeof)");
                return;
            }
            if (ctx.Operation is IIsTypeOperation isType)
            {
                ReportIfGated(ctx, isType.TypeOperand, isType.TypeOperand?.Name + " (is)");
                return;
            }

            // 成员符号面:表达式求值类型可能不是禁引类型(如返回 bool 的静态调用
            // ExecuteEvents.Execute),但**所属类型**在禁引命名空间内 —— 成员判定兜住。
            string memberDisplay = MemberDisplay(ctx.Operation);
            if (memberDisplay != null)
            {
                Report(ctx, memberDisplay);
                return;   // 已定位到成员引用,不再叠加类型面诊断(一处引用恰一条)
            }

            // 表达式静态类型面:局部变量 / 参数 / 转换 / 默认值等「只有类型没有成员」的形态。
            ReportIfGated(ctx, ctx.Operation.Type, ctx.Operation.Type?.Name);
        }

        /// <summary>成员引用的所属类型 ∈ 禁引命名空间 ⇒ 返回显示名;否则 null。
        /// 覆盖调用 / 方法组 / 属性 / 字段 / 事件五形态(同 DY0001 的 Operation 面)。</summary>
        private static string MemberDisplay(IOperation op)
        {
            ISymbol member;
            switch (op)
            {
                case IInvocationOperation invocation: member = invocation.TargetMethod; break;
                case IMethodReferenceOperation methodRef: member = methodRef.Method; break;
                case IPropertyReferenceOperation propertyRef: member = propertyRef.Property; break;
                case IFieldReferenceOperation fieldRef: member = fieldRef.Field; break;
                case IEventReferenceOperation eventRef: member = eventRef.Event; break;
                default: return null;
            }

            INamedTypeSymbol containing = member?.ContainingType;
            if (containing == null || !IsForbiddenNamespace(FullNamespace(containing)))
                return null;
            return containing.Name + "." + (member.Name ?? "?");
        }

        private static void ReportIfGated(OperationAnalysisContext ctx, ITypeSymbol type, string display)
        {
            if (GatedTypeName(type) == null || display == null)
                return;
            Report(ctx, display);
        }

        private static void Report(OperationAnalysisContext ctx, string display)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Operation.Syntax.GetLocation(), display));
        }

        // ── 符号面:继承 / 实现接口的基类型引用不产生 operation,在类型符号上补 ──

        private static void AnalyzeNamedType(SymbolAnalysisContext ctx)
        {
            if (!(ctx.Symbol is INamedTypeSymbol type) || type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Interface)
                return;
            if (type.Locations.Length == 0 || !IsInDirectReadScope(type.Locations[0].SourceTree))
                return;

            // 基类型链(含接口)逐个查命名空间;链上任一命中 ⇒ 该类型声明引用了 UI 栈。
            for (INamedTypeSymbol b = type.BaseType; b != null; b = b.BaseType)
                if (IsForbiddenNamespace(FullNamespace(b)))
                {
                    DeclareReport(ctx, type, b.Name);
                    return;
                }
            foreach (INamedTypeSymbol i in type.Interfaces)
                if (IsForbiddenNamespace(FullNamespace(i)))
                {
                    DeclareReport(ctx, type, i.Name);
                    return;
                }
        }

        private static void DeclareReport(SymbolAnalysisContext ctx, INamedTypeSymbol type, string baseDisplay)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Rule, type.Locations[0], type.Name + " : " + baseDisplay));
        }

        // ── 类型 / 命名空间工具 ──

        /// <summary>类型(或其类型实参、数组元素)的任一部分落在禁引命名空间 ⇒ 返回该部分类型名;
        /// 否则 null。递归进 TypeArguments —— <c>List&lt;EventSystem&gt;</c> 的类型实参引用同样是引用。</summary>
        private static string GatedTypeName(ITypeSymbol type)
        {
            switch (type)
            {
                case null:
                    return null;
                case IArrayTypeSymbol array:
                    return GatedTypeName(array.ElementType);
                case INamedTypeSymbol named:
                    if (IsForbiddenNamespace(FullNamespace(named)))
                        return named.Name;
                    foreach (ITypeSymbol arg in named.TypeArguments)
                    {
                        string hit = GatedTypeName(arg);
                        if (hit != null)
                            return hit;
                    }
                    return null;
                default:
                    return null;
            }
        }

        private static bool IsForbiddenNamespace(string ns)
            => ns != null && ForbiddenNamespaces.Contains(ns);

        /// <summary>类型所属命名空间全名(global namespace → 空串)。手拼而非
        /// ToDisplayString —— 显示格式随 SymbolDisplayFormat 漂移,字符串判据要稳定拼接。</summary>
        private static string FullNamespace(INamedTypeSymbol type)
        {
            INamespaceSymbol ns = type?.ContainingNamespace;
            if (ns == null || ns.IsGlobalNamespace)
                return string.Empty;

            var parts = new List<string>();
            for (INamespaceSymbol cur = ns; cur != null && !cur.IsGlobalNamespace; cur = cur.ContainingNamespace)
                parts.Add(cur.Name);
            var sb = new StringBuilder();
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (sb.Length > 0) sb.Append('.');
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        /// <summary>作用域判定:路径含 <c>Assets/Gameplay.Input/</c>(直读程序集源 ——
        /// 与 B2① root 同根;反斜杠归一化后同时命中绝对 / 相对形态)。
        /// 引擎 / 包 / 他装配的源天然不在作用域内 —— 本门无独立的包排除维度。</summary>
        private static bool IsInDirectReadScope(SyntaxTree tree)
        {
            string path = tree?.FilePath ?? string.Empty;
            path = path.Replace('\\', '/');
            return path.Contains(ScopePathKey, StringComparison.Ordinal);
        }
    }
}
