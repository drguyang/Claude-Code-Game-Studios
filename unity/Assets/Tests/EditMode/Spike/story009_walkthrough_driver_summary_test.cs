// story-009 走查驱动器汇总面护栏(S12-2 · 2026-10-15)
//
// 打的不是游戏逻辑,是**走查驱动器自己的判定面** —— 它给 S12-10 人工走查供可信汇总,
// 若它自己会「零断言却报全绿」,那份走查结论就是假绿。
//
// 判据锚(承 qa-plan-sprint-12):
//   ① 注入恰好 1 条失败 ⇒ passed == false(逐条求真)
//   ② 空腿 ⇒ passed == false(S12-2 新加的 fail-loud 闸)
//   ③ 全绿 ⇒ passed == true
//   ④ 全红 ⇒ passed == false
// 反证 = 删 Finish 的空腿闸 ⇒ ② 立即转红(空集 All 恒真)。

using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Spike;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    [TestFixture]
    public sealed class Story009WalkthroughDriverSummaryTest
    {
        // ── 经由反射取 internal 面(IVT 已由 Editor.Tools.Spike/AssemblyInfo.cs 声明,
        //    但 LegResult/Check/Finish 均 internal ⇒ 直连类型名即可编译通过;
        //    此处仍反射是为了让「测试在编译期依赖 internal 形状」这件事显式可见,
        //    一旦生产侧改签名,测试立刻以 MissingMethodException 失败,而非静默空跑。)

        private static object NewLeg(string leg)
        {
            var asm = typeof(Story009WalkthroughDriver).Assembly;
            var t = asm.GetType("DaYiJingCheng.EditorTools.Spike.Story009WalkthroughDriver+LegResult");
            Assert.IsNotNull(t, "LegResult 类型必须存在(生产侧改名 ⇒ 本测试立即失败,禁静默)");
            var inst = System.Activator.CreateInstance(t);
            t.GetField("leg").SetValue(inst, leg);
            t.GetField("checks").SetValue(inst, new List<string>());
            t.GetField("passed").SetValue(inst, false); // 生产初值
            t.GetField("OkCount").SetValue(inst, 0);   // S12-2 判定真源(整数累计,不经字符串)
            t.GetField("FailCount").SetValue(inst, 0);
            return inst;
        }

        // Check/Finish 住在嵌套类 WalkHost 内(private static,Story009WalkthroughDriver:397/417),
        // 不是外层类 —— 反射必须指向 "Story009WalkthroughDriver+WalkHost"。
        private static readonly System.Type HostType =
            typeof(Story009WalkthroughDriver).Assembly
                .GetType("DaYiJingCheng.EditorTools.Spike.Story009WalkthroughDriver+WalkHost");

        private static void Check(object leg, bool ok, string what)
        {
            Assert.IsNotNull(HostType, "WalkHost 类型必须存在(生产侧改名 ⇒ 本测试立即失败,禁静默)");
            var m = HostType.GetMethod("Check",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(m, "Check(bool,string) 必须存在");
            m.Invoke(null, new[] { leg, ok, what });
        }

        private static void Finish(object leg)
        {
            Assert.IsNotNull(HostType, "WalkHost 类型必须存在");
            var m = HostType.GetMethod("Finish",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(m, "Finish(LegResult) 必须存在");
            m.Invoke(null, new[] { leg });
        }

        private static bool Passed(object leg) => (bool)leg.GetType().GetField("passed").GetValue(leg);
        private static int Counter(object leg, string field) => (int)leg.GetType().GetField(field).GetValue(leg);
        private static List<string> Checks(object leg) => (List<string>)leg.GetType().GetField("checks").GetValue(leg);

        // ① 逐条求真:注入 1 条失败 ⇒ 汇总必须报不通过
        [Test]
        public void test_oneFailure_summaryReportsNotPassed()
        {
            var leg = NewLeg("A");
            Check(leg, true, "A0 可见");
            Check(leg, false, "A2 按住 F 后列表应隐藏");
            Finish(leg);
            Assert.IsFalse(Passed(leg), "存在 1 条失败 ⇒ 该腿不得判通过(否则人工走查拿到假绿)");
        }

        // ①-b **S12-2 真 bug 回归**:`All(c => c.StartsWith("✅"))` 在 Mono 下会把
        //     `"🔴 …"` 误判为通过(实测见 production/qa/evidence/fix-s12-2-*.md)。
        //     本测试专钉:一条失败 ⇒ 必须不通过。若有人把判据改回字符串前缀,本条转红。
        [Test]
        public void test_emojiPrefixJudgment_singleFailStillNotPassed()
        {
            var leg = NewLeg("A");
            Check(leg, false, "🔴 这条是失败(用于钉死字符串前缀误判)");
            Finish(leg);
            Assert.IsFalse(Passed(leg),
                "一条 🔴 失败 ⇒ 不得通过。旧判据 StartsWith(\"✅\") 在 Mono 下误判为 true —— 本测试即其回归");
            Assert.AreEqual(0, Counter(leg, "OkCount"));
            Assert.AreEqual(1, Counter(leg, "FailCount"));
        }

        // ② S12-2 新闸:空腿(一条断言都没跑)⇒ 不得判通过。
        //    这是本件核心:All() 对空集恒真,删闸即转红。
        [Test]
        public void test_emptyLeg_summaryReportsNotPassed()
        {
            var leg = NewLeg("A");
            // 不调任何 Check,直接 Finish —— 模拟结构改动后 early-exit 跑到 Check 之前
            Finish(leg);
            Assert.IsFalse(Passed(leg), "空腿 = 一步都没跑 ⇒ 绝不能算通过");
            Assert.IsTrue(Checks(leg).Count > 0, "空腿应被显式补一条红(原因须在 Console/JSON 可见)");
            StringAssert.Contains("零断言", Checks(leg)[0]);
        }

        // ③ 全绿 ⇒ 通过(反向:不能把闸写成「永远不通过」)
        [Test]
        public void test_allPass_summaryReportsPassed()
        {
            var leg = NewLeg("B");
            Check(leg, true, "B1 目标正确");
            Check(leg, true, "B3 双事件");
            Finish(leg);
            Assert.IsTrue(Passed(leg), "全部断言通过 ⇒ 该腿应判通过");
        }

        // ④ 全红 ⇒ 不通过
        [Test]
        public void test_allFail_summaryReportsNotPassed()
        {
            var leg = NewLeg("C");
            Check(leg, false, "C1 直投不应抛异常");
            Finish(leg);
            Assert.IsFalse(Passed(leg));
        }

        // ⑤ 收腿必须登记进 _results —— 否则 JSON 少腿,汇总行数与实际执行不符
        [Test]
        public void test_finish_registersLegForJson()
        {
            var leg = NewLeg("B");
            Check(leg, true, "B1");
            Finish(leg);
            // _results 是 private static,经反射读取验证登记发生(Listener 面由 Unity 侧回归覆盖)
            var f = typeof(Story009WalkthroughDriver).GetField("_results",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(f, "_results 字段必须存在");
            var list = (System.Collections.ICollection)f.GetValue(null);
            Assert.IsNotNull(list);
            Assert.Greater(list.Count, 0, "Finish 后必须登记该腿,否则 JSON 不含本次结果");
        }
    }
}
