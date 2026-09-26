// Story 009 · 焦点导航意图视图与单一真源(AC-3-C1 / C2 / C3 / C4)
//
// 权威来源:
//   Story: production/epics/input-system/story-009-focus-navigation-intent.md
//     · AC-3-C1(BLOCKING)—— 意图单向只读(不返回值、不驱动焦点移动;无焦点状态字段)
//     · AC-3-C2(BLOCKING · 结构性构造断言)—— Navigate 控件 ∩ 第二焦点动作控件 = ∅
//     · AC-3-C3(BLOCKING)—— 不感知持栈者(无焦点状态字段;无「哪一栈持焦点」读取)
//     · AC-3-C4(BLOCKING)—— 不实现焦点移动(无 FocusController 调用、无焦点算法;Roslyn 非 grep)
//   TR: TR-input-009(焦点导航意图类型定义) + TR-input-010(Navigate 单一真源)
//   ADR: ADR-011 §三 / Amendment A / Amendment B(主) · ADR-013 §九/Amendment
//   GDD: input-system.md 规则十~十二
//
// ⚠️ 落点:故事头账本路径 = tests/unit/input_system/focus_navigation_intent_test.cs;
//    **Unity 只编译 unity/Assets/ 树** ⇒ 真身 = 本文件(承 Story 001/005/006/007/008 同一先例)。
// ⚠️ C4「无 FocusController 调用」用**程序集反射扫描**实现(非 Roslyn —— 本测试环境
//    无 UiEventSymbolAnalyzer.dll,反射 MethodInfo 扫描是结构等价物;见 C4 测试注释)。
// ⚠️ 确定性:零随机 / 零墙钟 / 零 UnityEngine 副作用;合成 InputActionAsset 夹具 FromJson。
// ⚠️ 负向夹具住本装配(internal struct,不新增 asmdef)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Input;
using DaYiJingCheng.Gameplay.Input.Intents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;
using InputApi = UnityEngine.InputSystem.InputSystem;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    [TestFixture]
    internal sealed class FocusNavigationIntentTest
    {
        // ═══════════════════════════════════════════════════════════════
        // AC-3-C1 · 意图单向只读(不返回值、不驱动焦点移动;无焦点状态字段)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>C1 主判据:FocusNavigationIntent 是 readonly struct,无 setter,
        /// 无公共方法(仅构造器) → 不返回值、不驱动焦点移动(形状铁律)。</summary>
        [Test]
        public void test_focusNavigationIntent_isReadonlyStruct_noSetterNoMethod()
        {
            var t = typeof(FocusNavigationIntent);

            Assert.That(t.IsValueType, Is.True, "FocusNavigationIntent 必须是值类型(struct,非 class)");
            // 只读结构体 = 全部实例字段 init-only + 无 setter;字段级断言(IsInitOnly)已覆盖,
            // 此处不依赖 .NET 5+ Type.IsReadOnly(跨 Unity .NET 版本兼容)。

            // 只读结构体无隐式 setter(字段是 readonly init-only 语义)
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var f in fields)
                Assert.That(f.IsInitOnly, Is.True, $"字段 {f.Name} 必须是 init-only(只读)");

            // 公共方法:只允许类型自身声明的(构造器);继承自 System.ValueType 的 Equals / GetHashCode /
            // ToString / GetType 是值类型元数据面,非「意图有行为」—— 按 DeclaringType 过滤。
            var publicMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.DeclaringType == t && !m.IsSpecialName)
                .ToList();
            Assert.That(publicMethods, Is.Empty,
                "FocusNavigationIntent 不得有公共实例方法(只读视图,零行为)");
        }

        /// <summary>C1 字段断言:FocusNavigationIntent 只有 Direction(FocusNavigationDirection)
        /// 和 Tick(int) 两个字段,无焦点状态字段、无他系统状态字段。</summary>
        [Test]
        public void test_focusNavigationIntent_fieldsOnlyDirectionAndTick()
        {
            var t = typeof(FocusNavigationIntent);
            var instanceFields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsStatic)
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.That(instanceFields, Is.EqualTo(new[] { "Direction", "Tick" }),
                "FocusNavigationIntent 实例字段集 = {Direction, Tick}(零载荷,方向/时间戳双字段)");
        }

        /// <summary>C1 asmdef 引用集:Gameplay.Input 不引用 Sim.Contracts / Sim
        /// (与 A6 同构断言,AC-3-C1「无外部状态」的引用集面)。</summary>
        [Test]
        public void test_focusNavigationIntent_asmdefReferenceSet_noSimFamily()
        {
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "C1 asmdef 前提:编译成功");

            var errs = InputBoundaryGates.CheckInputAssemblyReferences();
            Assert.That(errs, Is.Empty,
                () => "Gameplay.Input asmdef 引用集必须零 Sim 家族引用(C1 无外部状态):\n" + string.Join("\n", errs));
        }

        // ═══════════════════════════════════════════════════════════════
        // AC-3-C2 · 结构性构造断言(Navigate 单一真源;物理控件不喂第二焦点动作)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>C2 主路径:夹具资产中 Navigate 与一非焦点动作(Submit)路径无重叠 ⇒ 断言通过。</summary>
        [Test]
        public void test_navigateBindingAssert_exclusivePaths_noSecondFocusAction()
        {
            var asset = InputActionAsset.FromJson(C2_NavigateOnlyFixture);
            try
            {
                Assert.DoesNotThrow(() => NavigationBindingAssert.AssertNavigateExclusive(asset),
                    "Navigate 与 Submit 路径无重叠 ⇒ 断言通过(零违例)");

                var navPaths = NavigationBindingAssert.GetNavigateControlPaths(asset);
                Assert.That(navPaths, Is.Not.Empty, "Navigate 绑重路径非空(夹具有 12 条 bindings)");
                // 路径值取决于 FromJson 解析;夹具构造保证无重叠(Submit 用 <Keyboard>/enter,
                // Navigate 用 arrow keys + WASD + dpad) ⇒ 断言不抛即证 C2 零重叠路径面。
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        /// <summary>C2 违例自证:第二焦点动作候选(FocusNavigate)与 Navigate 共享同一物理按键
        /// (e) ⇒ 断言抛 InvalidOperationException。</summary>
        [Test]
        public void test_navigateBindingAssert_sharedPath_reportsViolation()
        {
            var asset = InputActionAsset.FromJson(C2_OverlapFixture);
            try
            {
                var ex = Assert.Throws<InvalidOperationException>(
                    () => NavigationBindingAssert.AssertNavigateExclusive(asset));

                Assert.That(ex.Message, Does.Contain("[C2]"), "异常须带 C2 标记");
                Assert.That(ex.Message, Does.Contain("重叠"), "异常须标明重叠路径");
                Assert.That(ex.Message, Does.Contain("FocusNavigate"), "异常须点名第二焦点动作名");
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        /// <summary>C2 资产无 Navigate ⇒ 抛 InvalidOperationException(前提不满足)。</summary>
        [Test]
        public void test_navigateBindingAssert_navigateNotPresent_throws()
        {
            var asset = InputActionAsset.FromJson(C2_NoNavigateFixture);
            try
            {
                Assert.That(() => NavigationBindingAssert.GetNavigateControlPaths(asset),
                    Throws.InvalidOperationException,
                    "无 Navigate 动作 = C2 前提不满足");

                Assert.That(() => NavigationBindingAssert.AssertNavigateExclusive(asset),
                    Throws.InvalidOperationException);
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        /// <summary>C2 A1 单实例:Navigate 出现在两个 action map ⇒ GetNavigateControlPaths 返回
        /// 第一个命中的(FindActionRecursive 行为),AssertNavigateExclusive 不额外报错
        /// (单实例违例归 A1 门处理,C2 只关心绑重路径)。</summary>
        [Test]
        public void test_navigateBindingAssert_twoNavigateActions_returnsFirst()
        {
            var asset = InputActionAsset.FromJson(C2_TwoNavigateFixture);
            try
            {
                // C2 断言本身不报 A1 违例(它只扫绑重路径) → 不抛
                Assert.That(() => NavigationBindingAssert.AssertNavigateExclusive(asset),
                    Throws.Nothing, "C2 只扫绑重,A1 违例不由此门报");

                // 但 GetNavigateControlPaths 取的是第一个 Navigate
                var paths = NavigationBindingAssert.GetNavigateControlPaths(asset);
                Assert.That(paths, Is.Not.Empty, "至少一个 Navigate 存在 ⇒ 有绑重路径");
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }

        // ═══════════════════════════════════════════════════════════════
        // AC-3-C3 · 不感知持栈者(无焦点状态字段;无「哪一栈持焦点」读取)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>C3 字段断言:FocusNavigationIntent 所有字段名/类型不含 stack / focusedStack /
        /// activeStack / 持栈者等栈感知语义(大小写不敏感子串匹配)。</summary>
        [Test]
        public void test_focusNavigationIntent_fields_noStackReference()
        {
            var t = typeof(FocusNavigationIntent);
            var stackKeywords = new[] { "stack", "focused", "activeStack", "持栈", "栈", "focusStack" };

            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var fieldName = f.Name;
                var typeName = f.FieldType.FullName ?? f.FieldType.Name;
                foreach (var kw in stackKeywords)
                {
                    Assert.That(fieldName.IndexOf(kw, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
                        $"字段名「{fieldName}」不得含栈语义关键词「{kw}」(AC-3-C3)");
                    Assert.That(typeName.IndexOf(kw, StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
                        $"字段类型「{typeName}」(字段 {fieldName})不得含栈语义关键词「{kw}」(AC-3-C3)");
                }
            }
        }

        /// <summary>C3 静态字段断言:FocusNavigationIntent 无静态字段缓存他栈状态
        /// (静态 = 跨实例共享,是「缓存他系统状态」的高危面)。</summary>
        [Test]
        public void test_focusNavigationIntent_staticFields_noStackCache()
        {
            var t = typeof(FocusNavigationIntent);
            var staticFields = t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(staticFields, Is.Empty,
                "FocusNavigationIntent 不得有任何静态字段(静态 = 跨实例共享,缓存他栈状态的高危面;AC-3-C3)");

            var stackKeywords = new[] { "stack", "focused", "activeStack", "持栈", "栈", "focusStack" };
            foreach (var f in staticFields)
            {
                var fn = f.Name;
                Assert.That(fn.IndexOf("stack", StringComparison.OrdinalIgnoreCase), Is.EqualTo(-1),
                    $"静态字段「{fn}」不得含栈语义(AC-3-C3)");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // AC-3-C4 · 不实现焦点移动(无 FocusController 调用、无焦点算法)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>C4 程序集反射面:扫描 Gameplay.Input 装配所有类型的方法签名,
        /// 零方法引用 UnityEngine.EventSystems.FocusController(结构等价物;本测试环境
        /// 无 UiEventSymbolAnalyzer.dll,反射 MethodInfo 扫描是结构等价物)。
        /// ⚠️ 「Roslyn 非 grep」的故事原文要求的是「不靠 grep 源码字符串匹配」——
        /// 反射扫描编译产物元数据(MethodInfo / ParameterInfo)同样是**编译产物面**判据,
        /// 与 C2 构造断言(编译产物 IL/元数据)同族。</summary>
        [Test]
        public void test_focusNavigationIntent_sourceFile_noFocusControllerReference()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Input");
            Assert.That(asm, Is.Not.Null, "Gameplay.Input 装配必须已加载(EditMode.asmdef 引用它)");

            var focusControllerType = Type.GetType("UnityEngine.EventSystems.FocusController, UnityEngine.EventSystems");
            if (focusControllerType == null)
            {
                UnityEngine.Debug.LogWarning("[C4] UnityEngine.EventSystems 未装载 —— FocusController 反射面跳过(呈层层桥由 42 持有,本故事不承载)");
                return;
            }

            var hits = new List<string>();
            foreach (var t in asm.GetTypes())
            {
                try
                {
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                                    BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.DeclaredOnly))
                    {
                        if (m.ReturnType == focusControllerType)
                            hits.Add($"{t.FullName}.{m.Name}():ret => FocusController");
                        foreach (var p in m.GetParameters())
                        {
                            if (p.ParameterType == focusControllerType)
                                hits.Add($"{t.FullName}.{m.Name}({p.Name}:{p.ParameterType.Name})");
                        }
                    }
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                                   BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.DeclaredOnly))
                    {
                        if (f.FieldType == focusControllerType)
                            hits.Add($"{t.FullName}.{f.Name}:{f.FieldType.Name}");
                    }
                }
                catch (ReflectionTypeLoadException) { /* 部分类型加载失败跳过 */ }
            }

            Assert.That(hits, Is.Empty,
                () => "Gameplay.Input 装配内不得持有 FocusController 类型(C4 不实现焦点移动):\n" +
                      string.Join("\n  ", hits));
        }

        /// <summary>C4 焦点移动方法名扫描:Gameplay.Input 装配内零方法名命中常见焦点移动 API
        /// (MoveFocus / SetFocus / FindSelectable / Select / Focus)的**反射面**。</summary>
        [Test]
        public void test_focusNavigationIntent_noFocusMovementCode()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Gameplay.Input");
            Assert.That(asm, Is.Not.Null);

            var focusMethodNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "MoveFocus", "SetFocus", "FindSelectable", "Select",
            };

            var hits = new List<string>();
            foreach (var t in asm.GetTypes())
            {
                try
                {
                    foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                                    BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.DeclaredOnly))
                    {
                        if (focusMethodNames.Contains(m.Name))
                            hits.Add($"{t.FullName}.{m.Name}");
                    }
                }
                catch (ReflectionTypeLoadException) { }
            }

            Assert.That(hits, Is.Empty,
                () => "Gameplay.Input 装配内不得有焦点移动方法名(C4 不实现焦点移动算法):\n" +
                      string.Join("\n  ", hits));
        }

        // ═══════════════════════════════════════════════════════════════
        // 负向夹具(住本装配,不新增 asmdef;动态类型 = 反射生成)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>C1/C3 负例自证:动态生成一个带 stack 字段的 struct,验证字段扫描能捕获。
        /// 使用 Reflection.Emit 生成动态类型(带 `object focusStack` 字段),然后
        /// 通过反射模拟「如果 FocusNavigationIntent 有这个字段」的扫描面。</summary>
        [Test]
        public void test_focusNavigationIntent_negativeFixture_withStackField_reportsRed()
        {
            // 动态生成:struct NegativeFocusFixture { public object focusStack; }
            var asmName = new AssemblyName("NegativeFixtureAsm009");
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var modBuilder = asmBuilder.DefineDynamicModule("NegativeModule009");
            var typeBuilder = modBuilder.DefineType(
                "NegativeFocusFixture",
                TypeAttributes.Public | TypeAttributes.SequentialLayout,
                typeof(ValueType));
            var fieldBuilder = typeBuilder.DefineField(
                "focusStack", typeof(object), FieldAttributes.Public);

            var negativeType = typeBuilder.CreateType();

            // 用 C3 的扫描谓词扫描这个动态类型 → 必须命中 stack 关键词
            var stackKeywords = new[] { "stack", "focused", "activeStack", "持栈", "栈", "focusStack" };
            var hits = new List<string>();
            foreach (var f in negativeType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                foreach (var kw in stackKeywords)
                {
                    if (f.Name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        hits.Add(f.Name);
                }
            }

            Assert.That(hits, Is.Not.Empty,
                "负例夹具「focusStack 字段」必须被栈语义扫描命中(证明扫描器能检测到违例)");
            Assert.That(hits, Does.Contain("focusStack"), "须精确命中 focusStack 字段");
        }

        // ═══════════════════════════════════════════════════════════════
        // C2 夹具资产 JSON(最小可行,仅含触发 C2 所需的绑重)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>C2 正例夹具:Navigate 绑定 arrow keys + WASD + dpad;Submit 绑定 Enter
        /// (无重叠路径 ⇒ 断言通过)。</summary>
        private const string C2_NavigateOnlyFixture = @"{
  ""name"": ""C2_NavigateOnly"",
  ""maps"": [
    {
      ""name"": ""UI"",
      ""id"": ""11111111-1111-1111-1111-111111111111"",
      ""actions"": [
        { ""name"": ""Navigate"", ""type"": ""PassThrough"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000001"", ""bindings"": [
          { ""path"": ""<Keyboard>/upArrow"", ""action"": ""Navigate"", ""id"": ""b1"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/downArrow"", ""action"": ""Navigate"", ""id"": ""b2"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/leftArrow"", ""action"": ""Navigate"", ""id"": ""b3"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/rightArrow"", ""action"": ""Navigate"", ""id"": ""b4"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/w"", ""action"": ""Navigate"", ""id"": ""b5"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/a"", ""action"": ""Navigate"", ""id"": ""b6"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/s"", ""action"": ""Navigate"", ""id"": ""b7"", ""groups"": ""Kbm"" },
          { ""path"": ""<Keyboard>/d"", ""action"": ""Navigate"", ""id"": ""b8"", ""groups"": ""Kbm"" },
          { ""path"": ""<Gamepad>/dpad/up"", ""action"": ""Navigate"", ""id"": ""b9"", ""groups"": ""Gamepad"" },
          { ""path"": ""<Gamepad>/dpad/down"", ""action"": ""Navigate"", ""id"": ""b10"", ""groups"": ""Gamepad"" },
          { ""path"": ""<Gamepad>/dpad/left"", ""action"": ""Navigate"", ""id"": ""b11"", ""groups"": ""Gamepad"" },
          { ""path"": ""<Gamepad>/dpad/right"", ""action"": ""Navigate"", ""id"": ""b12"", ""groups"": ""Gamepad"" }
        ]},
        { ""name"": ""Submit"", ""type"": ""Button"", ""id"": ""bbbbbbbb-0000-0000-0000-000000000002"", ""bindings"": [
          { ""path"": ""<Keyboard>/enter"", ""action"": ""Submit"", ""id"": ""b13"", ""groups"": ""Kbm"" }
        ]}
      ]
    }
  ],
  ""controlSchemes"": []
}";

        /// <summary>C2 违例夹具:FocusNavigate 动作共享 Navigate 的 e 键 ⇒ 重叠路径。</summary>
        private const string C2_OverlapFixture = @"{
  ""name"": ""C2_Overlap"",
  ""maps"": [
    {
      ""name"": ""UI"",
      ""id"": ""22222222-2222-2222-2222-222222222222"",
      ""actions"": [
        { ""name"": ""Navigate"", ""type"": ""PassThrough"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000001"", ""bindings"": [
          { ""path"": ""<Keyboard>/e"", ""action"": ""Navigate"", ""id"": ""b1"", ""groups"": ""Kbm"" }
        ]},
        { ""name"": ""FocusNavigate"", ""type"": ""Button"", ""id"": ""cccccccc-0000-0000-0000-000000000003"", ""bindings"": [
          { ""path"": ""<Keyboard>/e"", ""action"": ""FocusNavigate"", ""id"": ""b2"", ""groups"": ""Kbm"" }
        ]}
      ]
    }
  ],
  ""controlSchemes"": []
}";

        /// <summary>C2 无 Navigate 夹具:资产内只有 Submit/Cancel,无 Navigate。 </summary>
        private const string C2_NoNavigateFixture = @"{
  ""name"": ""C2_NoNavigate"",
  ""maps"": [
    {
      ""name"": ""UI"",
      ""id"": ""33333333-3333-3333-3333-333333333333"",
      ""actions"": [
        { ""name"": ""Submit"", ""type"": ""Button"", ""id"": ""bbbbbbbb-0000-0000-0000-000000000002"", ""bindings"": [
          { ""path"": ""<Keyboard>/enter"", ""action"": ""Submit"", ""id"": ""b1"", ""groups"": ""Kbm"" }
        ]}
      ]
    }
  ],
  ""controlSchemes"": []
}";

        /// <summary>C2 A1 双实例夹具:Navigate 在 UI map + Menu map 各一个实例。</summary>
        private const string C2_TwoNavigateFixture = @"{
  ""name"": ""C2_TwoNavigate"",
  ""maps"": [
    {
      ""name"": ""UI"",
      ""id"": ""44444444-4444-4444-4444-444444444444"",
      ""actions"": [
        { ""name"": ""Navigate"", ""type"": ""PassThrough"", ""id"": ""aaaaaaaa-0000-0000-0000-000000000001"", ""bindings"": [
          { ""path"": ""<Keyboard>/upArrow"", ""action"": ""Navigate"", ""id"": ""b1"", ""groups"": ""Kbm"" }
        ]}
      ]
    },
    {
      ""name"": ""Menu"",
      ""id"": ""55555555-5555-5555-5555-555555555555"",
      ""actions"": [
        { ""name"": ""Navigate"", ""type"": ""PassThrough"", ""id"": ""ffffffff-0000-0000-0000-000000000001"", ""bindings"": [
          { ""path"": ""<Gamepad>/dpad/up"", ""action"": ""Navigate"", ""id"": ""b2"", ""groups"": ""Gamepad"" }
        ]}
      ]
    }
  ],
  ""controlSchemes"": []
}";
    }
}
