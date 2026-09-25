// Story 001 · AC-3-A4② —— Legacy Input 零引用门(Roslyn 分析器)
//
// 权威来源:
//   story-001-action-asset-identity.md AC-3-A4②(BLOCKING):按**语义符号**拒绝任何
//     UnityEngine.Input 静态符号引用(全限定 / using 导入 / using static / 别名 /
//     方法组转换 / typeof 六形态全覆盖),Error 级 ⇒ 命中即编译失败;
//     对 InputAction.ReadValue 等 Input System 合法写法零误报。
//   control-manifest.md Forbidden 表 · Legacy Input 行;ADR-011 §Decision 二。
//
// 构建纪律(unity-specialist 预检 2026-09-25):
//   · 源码必须住 Assets 外 —— 本文件依赖 Microsoft.CodeAnalysis,若进 Assets 会被 Unity
//     编译进工程程序集(依赖不存在 ⇒ 工程编译炸)。构建产物 DLL 才进 Assets。
//   · 只对 Unity 捆绑的 Microsoft.CodeAnalysis(.CSharp).dll(DotNetSdkRoslyn,4.3.x)编译 ——
//     NuGet 新版(4.8+)会静默不被 Unity 宿主加载。
//   · 不建 asmdef(ADR-025 清单封闭性:未登记 asmdef = 构建失败);DLL 走
//     Assets/Editor.Tools.Analyzers/ + RoslynAnalyzer label。
//
// 排除策略:引擎与包源(Library/PackageCache、Packages/、BuiltInPackages)不挂门 ——
//   那些是 Unity 自己编译的引擎内容,包内 UnityEngine.InputSystem / InputForUI 与
//   ENABLE_LEGACY_INPUT_MANAGER 防御性代码满地(实证 15 处),挂了会炸包编译。
//   门只约束本仓自写源码(Assets/ 下的工程代码)。

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DaYiJingCheng.EditorTools.Analyzers
{
    /// <summary>编译期 Legacy Input 零引用门:凡引用 UnityEngine.Input 类型符号即报 DY0001(Error)。</summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class LegacyInputAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>本分析器的诊断 ID(测试按此断言,勿改)。</summary>
        public const string DiagnosticId = "DY0001";

        private const string Category = "Input";
        private const string LegacyInputMetadataName = "UnityEngine.Input";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Legacy Input Manager reference forbidden",
            messageFormat: "Legacy Input Manager symbol '{0}' is referenced - forbidden by control-manifest (use Input System / InputAction instead)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "AC-3-A4② zero-reference gate: any semantic reference to UnityEngine.Input " +
                         "(fully-qualified, using-import, using-static, alias, method-group, or typeof form) " +
                         "fails compilation. Engine/package sources (Library/PackageCache, Packages, BuiltInPackages) are excluded.");

        /// <summary>本分析器唯一可报的诊断(DY0001 · Error 级 ⇒ 编译失败)。</summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
            => ImmutableArray.Create(Rule);

        /// <summary>注册:按操作种类(调用/属性/字段/事件引用)捕获一切对 UnityEngine.Input 成员的语义引用。</summary>
        public override void Initialize(AnalysisContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // 编译起始:解析 UnityEngine.Input 类型符号;解析不到(纯 BCL 编译,如分析器自测样例之外的场景)则无事可做。
            context.RegisterCompilationStartAction(startContext =>
            {
                INamedTypeSymbol inputType = startContext.Compilation
                    .GetTypeByMetadataName(LegacyInputMetadataName);
                if (inputType == null)
                    return;

                startContext.RegisterOperationAction(
                    opCtx => AnalyzeOperation(opCtx, inputType),
                    OperationKind.Invocation,        // Input.GetKey(...) / Input.GetAxis(...)
                    OperationKind.PropertyReference, // Input.mousePosition / Input.anyKeyDown
                    OperationKind.FieldReference,    // Input.mousePosition 等(容错)
                    OperationKind.EventReference,    // Input.* 事件(容错)
                    OperationKind.MethodReference,   // 方法组转换:Func<string,float> f = Input.GetAxis(2026-09-25 复核 W3 补漏)
                    OperationKind.TypeOf);           // typeof(UnityEngine.Input)(类型符号引用)
            });
        }

        private static void AnalyzeOperation(OperationAnalysisContext ctx, INamedTypeSymbol inputType)
        {
            // typeof(UnityEngine.Input) —— 类型符号本身的引用(成员判定不适用:该情形下
            // 「被引用符号」就是 inputType,而非其成员)。
            // ⚠️ 必须取 ITypeOfOperation.TypeOperand:TypeOperand 才是 typeof 的操作数类型;
            // 读 .Type 会命中继承的 IOperation.Type = typeof 表达式自身的求值类型(System.Type),
            // 与 inputType 恒不相等 ⇒ 分支静默失效(2026-09-25 探针实证)。
            if (ctx.Operation is ITypeOfOperation typeOf)
            {
                ITypeSymbol typeOperand = typeOf.TypeOperand;
                if (typeOperand != null && SymbolEqualityComparer.Default.Equals(
                        typeOperand.OriginalDefinition, inputType.OriginalDefinition)
                    && !IsExcludedTree(ctx.Operation.Syntax.SyntaxTree))
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Rule, ctx.Operation.Syntax.GetLocation(), typeOperand.Name + " (typeof)"));
                }
                return;
            }

            ISymbol member;
            switch (ctx.Operation)
            {
                case IInvocationOperation invocation:
                    member = invocation.TargetMethod;
                    break;
                case IMethodReferenceOperation methodRef: // 方法组:var f = Input.GetAxis;(W3)
                    member = methodRef.Method;
                    break;
                case IPropertyReferenceOperation propertyRef:
                    member = propertyRef.Property;
                    break;
                case IFieldReferenceOperation fieldRef:
                    member = fieldRef.Field;
                    break;
                case IEventReferenceOperation eventRef:
                    member = eventRef.Event;
                    break;
                default:
                    return;
            }

            INamedTypeSymbol containing = member?.ContainingType;
            if (containing == null)
                return;

            // 语义判定(非文本):ContainingType 恰为 UnityEngine.Input。
            // SymbolEqualityComparer 走 OriginalDefinition —— 泛型/数组化成员(若有)不改判。
            if (!SymbolEqualityComparer.Default.Equals(
                    containing.OriginalDefinition, inputType.OriginalDefinition))
                return;

            // 包/引擎源排除(见文件头注释)。
            if (IsExcludedTree(ctx.Operation.Syntax.SyntaxTree))
                return;

            string display = containing.Name + "." + (member.Name ?? "?");
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, ctx.Operation.Syntax.GetLocation(), display));
        }

        private static bool IsExcludedTree(SyntaxTree tree)
        {
            string path = tree.FilePath ?? string.Empty;
            path = path.Replace('\\', '/');

            // 引擎/包源 —— 排除(见文件头注释)。路径形态同时兜绝对与相对两种
            // (2026-09-25 复核 W4:原实现的前置斜杠要求在相对路径形态下会漏排)。
            if (path.Contains("Library/PackageCache/")
                || path.Contains("BuiltInPackages/"))
                return true;

            // 一方源(Assets/ 下)永不排除 —— 即使形如 Assets/Packages/com.xxx 的
            // 内嵌 vendored 源也归门管(必须先于下面的 /Packages/ 判定)。
            if (path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.Contains("/Assets/"))
                return false;

            // 工程内嵌包(<project>/Packages/…)与相对路径 Packages/… —— 引擎内容,不挂门。
            return path.StartsWith("Packages/", StringComparison.Ordinal)
                || path.Contains("/Packages/");
        }
    }
}
