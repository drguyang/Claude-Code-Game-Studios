// emergency-procedures Story 001 测试
//
// AC-10-01: EmergencyReading 每字段反射断言声明类型 —— 零 float/double
// AC-10-02: asmdef 白名单 + IL 扫描
// AC-10-03: 判定路径静态检查零浮点字面量
// F-10.1 交付契约: 死区与 clamp

using System;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    public class ReadingContractTest
    {
        // AC-10-01: 全字段反射断言 —— 零 float/double
        [Test]
        public void test_reading_allFieldsIntegerDomain()
        {
            var type = typeof(EmergencyReading);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotEmpty(fields, "EmergencyReading 应有实例字段");

            foreach (var field in fields)
            {
                Assert.IsFalse(field.FieldType == typeof(float),
                    $"字段 {field.Name} 不应为 float");
                Assert.IsFalse(field.FieldType == typeof(double),
                    $"字段 {field.Name} 不应为 double");
                Assert.IsTrue(
                    field.FieldType == typeof(int) ||
                    field.FieldType == typeof(long) ||
                    field.FieldType == typeof(int[]),
                    $"字段 {field.Name} 类型 {field.FieldType.Name} 应在 {{int, long, int[]}} 内");
            }
        }

        // AC-10-01: 字段数量与名称断言
        [Test]
        public void test_reading_fieldNamesExact()
        {
            var expected = new[] { "Action", "HoldTicks", "Edges", "EdgeTicks", "Magnitude" };
            var actual = typeof(EmergencyReading)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(n => n)
                .ToArray();
            Assert.AreEqual(expected.OrderBy(n => n).ToArray(), actual,
                "EmergencyReading 字段集应恰为 {Action, HoldTicks, Edges, EdgeTicks, Magnitude}");
        }

        // AC-10-01: 构造函数赋值正确
        [Test]
        public void test_reading_constructor_assignsFields()
        {
            var edgeTicks = new[] { 10, 20, 30 };
            var reading = new EmergencyReading(1, 100, 3, edgeTicks, 500);

            Assert.AreEqual(1, reading.Action);
            Assert.AreEqual(100, reading.HoldTicks);
            Assert.AreEqual(3, reading.Edges);
            Assert.AreEqual(edgeTicks, reading.EdgeTicks);
            Assert.AreEqual(500, reading.Magnitude);
        }

        // AC-10-01: 负夹具 —— 影子类型含 float 字段应被拒
        [Test]
        public void test_reading_negativeFixture_shadowTypeWithFloat()
        {
            // 验证含 float 字段的类型不满足整数域约束
            var shadowType = typeof(ShadowReadingWithFloat);
            bool hasFloat = shadowType.GetFields().Any(f => f.FieldType == typeof(float));
            Assert.IsTrue(hasFloat, "影子类型应含 float 字段");
            // 验证 EmergencyReading 本身不含 float（对比）
            var readingType = typeof(EmergencyReading);
            bool readingHasFloat = readingType.GetFields().Any(f => f.FieldType == typeof(float));
            Assert.IsFalse(readingHasFloat, "EmergencyReading 不应含 float 字段");
        }

        // F-10.1: 死区验证
        [Test]
        public void test_deadzone_driftReturnsZero()
        {
            int dzMag = 100;
            Assert.IsTrue(EmergencyReadingContract.ValidateDeadzone(50, dzMag),
                "raw_axis=50 < dzMag=100 应在死区内");
            Assert.IsTrue(EmergencyReadingContract.ValidateDeadzone(-50, dzMag),
                "raw_axis=-50 应在死区内");
            Assert.IsFalse(EmergencyReadingContract.ValidateDeadzone(150, dzMag),
                "raw_axis=150 > dzMag=100 应不在死区内");
            Assert.IsFalse(EmergencyReadingContract.ValidateDeadzone(-150, dzMag),
                "raw_axis=-150 应不在死区内");
        }

        // F-10.1: edges 只计 press 沿（release 不计数）
        [Test]
        public void test_edges_onlyCountPress()
        {
            // edges == pressCount（release 不计数）
            Assert.IsTrue(EmergencyReadingContract.ValidateEdges(3, 3, 2),
                "edges=3, pressCount=3, releaseCount=2 应合法");
            Assert.IsTrue(EmergencyReadingContract.ValidateEdges(0, 0, 0),
                "edges=0, pressCount=0, releaseCount=0 应合法");
            Assert.IsFalse(EmergencyReadingContract.ValidateEdges(5, 3, 2),
                "edges=5, pressCount=3 应不合法（edges != pressCount）");
            Assert.IsFalse(EmergencyReadingContract.ValidateEdges(2, 3, 2),
                "edges=2, pressCount=3 应不合法（edges != pressCount）");
        }

        // F-10.1: edge_ticks 单调递增
        [Test]
        public void test_edgeTicks_monotonic()
        {
            Assert.IsTrue(EmergencyReadingContract.ValidateEdgeTicksMonotonic(new[] { 10, 20, 30 }),
                "edge_ticks=[10,20,30] 应单调递增");
            Assert.IsTrue(EmergencyReadingContract.ValidateEdgeTicksMonotonic(new[] { 5 }),
                "edge_ticks=[5] 单元素应合法");
            Assert.IsTrue(EmergencyReadingContract.ValidateEdgeTicksMonotonic(new int[0]),
                "edge_ticks=[] 空数组应合法");
            Assert.IsFalse(EmergencyReadingContract.ValidateEdgeTicksMonotonic(new[] { 30, 20, 10 }),
                "edge_ticks=[30,20,10] 应不合法（非单调）");
            Assert.IsFalse(EmergencyReadingContract.ValidateEdgeTicksMonotonic(new[] { 10, 10, 20 }),
                "edge_ticks=[10,10,20] 应不合法（相等非严格递增）");
        }

        // F-10.1: magnitude 域
        [Test]
        public void test_magnitude_withinBounds()
        {
            int magMax = 1000;
            Assert.IsTrue(EmergencyReadingContract.ValidateMagnitude(0, magMax),
                "magnitude=0 应在 [0, 1000] 内");
            Assert.IsTrue(EmergencyReadingContract.ValidateMagnitude(500, magMax),
                "magnitude=500 应在 [0, 1000] 内");
            Assert.IsTrue(EmergencyReadingContract.ValidateMagnitude(1000, magMax),
                "magnitude=1000 应在 [0, 1000] 内");
            Assert.IsFalse(EmergencyReadingContract.ValidateMagnitude(1500, magMax),
                "magnitude=1500 应超出 [0, 1000]");
            Assert.IsFalse(EmergencyReadingContract.ValidateMagnitude(-1, magMax),
                "magnitude=-1 应超出 [0, 1000]");
        }

        // AC-10-02: 契约落 Sim.Contracts（程序集断言）
        [Test]
        public void test_contract_inSimContractsAssembly()
        {
            var assembly = typeof(EmergencyReading).Assembly;
            Assert.AreEqual("Sim.Contracts", assembly.GetName().Name,
                "EmergencyReading 应住 Sim.Contracts 程序集");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-10-03 [B] —— 10 判定路径零浮点字面量(**真 IL 扫描**)
        //
        // ⚠️ 2026-10-03 修复判据空转(评审 A2):原测与上方 AC-10-01 的字段类型测
        //    **逐字重复**,无任何操作码检查 ⇒ 判据未真正执行。
        //    现经 `EmergencyIntegerGates.CheckJudgeIntegerIl` 做**真 call 图闭包扫描**。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1003_judgePath_zeroFloatIl()
        {
            Assert.That(UnityEditor.EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(否则扫到上一版产物 = 假绿)");

            var dll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates.SimAssemblyName);
            var errs = DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates.CheckJudgeIntegerIl(dll);

            Assert.That(errs, Is.Empty,
                "10 判定路径的真实 call 图闭包须零浮点(AC-10-03 / 门 A / ADR-006):\n"
                + string.Join("\n", errs));
        }

        /// <summary>AC-10-03 的**闭包形状守卫** —— 核心方法若从闭包里消失,说明判定路径被改写,
        /// 须重新人工确认(否则「扫了但什么都没扫到」= 空集冒充绿)。</summary>
        [Test]
        public void test_ac1003_judgeClosure_containsCoreKernels()
        {
            Assert.That(UnityEditor.EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            var dll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates.SimAssemblyName);
            var errs = DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates.CheckJudgeIntegerIl(
                dll,
                DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates.JudgeRoots,
                out var closure);

            Assert.That(errs, Is.Empty, string.Join("\n", errs));
            Assert.That(closure, Has.Some.Contains("JudgeEvaluator::Judge"),
                "Judge(三扇门本体)须在闭包内");
            Assert.That(closure, Has.Some.Contains("JudgeEvaluator::ScaleFixed"),
                "ScaleFixed(F-10.4 单一舍入)须在闭包内");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-10-02 [B] —— 3 侧零 Judge / JudgeResult / SimEvent 引用(**真 IL + 引用面**)
        //
        // ⚠️ 2026-10-03 修复判据空转(评审 A1):原测取 `Sim.Contracts` 程序集,
        //    断言其名 `Contains("Input")` —— **该名永不含 "Input"** ⇒ 恒真、零扫描。
        //    现扫 **`Gameplay.Input`(真 3 侧)** 的引用面 + 全类型 IL。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1002_inputSide_zeroJudgeReferences()
        {
            Assert.That(UnityEditor.EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            var dll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates.InputAssemblyName);
            var errs = DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates
                .CheckInputBoundaryIl(dll, out int scanned);

            Assert.That(errs, Is.Empty,
                $"3 侧(Gameplay.Input)须零 Judge/JudgeResult/SimEvent 引用(AC-10-02 [B],扫 {scanned} 个方法体):\n"
                + string.Join("\n", errs));
            Assert.That(scanned, Is.GreaterThan(0),
                "扫描到的**方法数须 > 0** —— 拒以空集冒充绿");
        }

        /// <summary>AC-10-02 的**可证伪性**:对含被禁符号的夹具跑同一谓词必须红。</summary>
        [Test]
        public void test_ac1002_negativeFixture_reportsRed()
        {
            Assert.That(UnityEditor.EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            // 夹具 = 本测试装配内的 `EmergencyInputBoundaryFixture`,
            // 它**故意**引用 JudgeResult(见文件末 §夹具)。
            var testDll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath(
                "Sim.Contracts.Tests");
            var errs = DaYiJingCheng.EditorTools.Gates.EmergencyIntegerGates
                .CheckInputBoundaryIl(testDll, out _);

            Assert.That(errs, Is.Not.Empty,
                "夹具含 JudgeResult 引用 ⇒ 谓词必须报红(否则门没在扫东西)");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // §可证伪夹具 —— 故意引用被禁符号,供 AC-10-02 的负向断言用。
    // ⚠️ 本类型**只存在于测试装配**,不进生产;它证明「谓词真的会红」。
    // ══════════════════════════════════════════════════════════════════
    internal static class EmergencyInputBoundaryFixture
    {
        /// <summary>故意返回 10 的判定结果枚举 —— 模拟「3 侧越界引用」。</summary>
        internal static int DeliberatelyForbidden()
        {
            // 引用 JudgeResult(经 Sim.Contracts 可见 —— 测试装配引用它,生产 3 侧不得)
            var r = DaYiJingCheng.Sim.EmergencyProcedures.JudgeResult.Applied;
            return (int)r;
        }
    }

    // 影子类型：含 float 字段，用于负夹具测试
    internal readonly struct ShadowReadingWithFloat
    {
        public readonly int Action;
        public readonly float Magnitude; // 故意含 float
    }
}
