// `CameraSetModeRequesterGate` 的自检夹具(2026-11-02)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/**` 全部生产 `.cs`(经 `CameraSetModeRequesterGate.RunAll(".")`)——
//   **必须读真树**,否则那条只证明「扫一个我造的目录」,证不了生产码干净。
// 写:**零写盘**。其余各测都走 `CheckSource(path, text)` 纯函数喂**内联源码**,
//   不造临时目录 —— 比落盘更快,也不会碰 `FsWriteBoundaryGate` 的扫描面。
//
// ── 为什么这么拆 ──────────────────────────────────────────────────────────
// 一条门若从没红过,「它有门」就只是自述(假绿形态 **F6**)。这里每条**禁止形态**各一测,
// 让它**必须红**;再加两条**不得误报**的(注释 / 合法双参 / `_modeMachine` 豁免),
// 防门变成「凡出现就红」的噪音机 —— 那种门最后会被人加白名单加死。
//
// ⚠️ **本夹具不测「传错系统号」**(`rig.SetMode(mode, 7)` 里 7 是不是真号)——
//   那需要一张系统号表,门内已在文件头「已知边界」登记,**不得读成已覆盖**。

using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    /// <summary>`CameraSetModeRequesterGate`:真树清洁 + 四种禁止形态会红 + 三种合法形态不误报。</summary>
    [TestFixture]
    public sealed class CameraSetModeRequesterGateTest
    {
        private const string NoViolation = "class X { void M() { } }";

        // ══════════════ 真树 ══════════════

        [Test]
        public void test_productionTree_zeroRed()
        {
            var errs = CameraSetModeRequesterGate.RunAll(".");
            Assert.That(errs, Is.Empty,
                "生产码出现单参 `SetMode` 或 `requesterId: -1` —— 甲案的「同一请求方可降档」" +
                "判据会恒假并**静默失效**(ADR-020 Amendment C ② / AC-20-16)。\n" +
                string.Join("\n", errs));
        }

        // ══════════════ 必须红的两种形态 ══════════════

        [Test]
        public void test_singleArgCall_isReported()
        {
            var errs = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { void M() { rig.SetMode(CameraMode.Explore); } }");
            Assert.That(errs, Is.Not.Empty,
                "单参 `SetMode(mode)` 没被报 —— 扫描器是死的(R1 失效)");
        }

        [Test]
        public void test_explicitMinusOne_isReported()
        {
            var positional = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { void M() { rig.SetMode(CameraMode.Explore, -1); } }");
            Assert.That(positional, Is.Not.Empty,
                "`SetMode(mode, -1)` 没被报 —— 它与单参重载**语义等价**,R2 必须抓住它");

            var named = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { void M() { rig.SetMode(CameraMode.Explore, requesterId: -1); } }");
            Assert.That(named, Is.Not.Empty,
                "具名形 `requesterId: -1` 没被报 —— R2 只认位置实参会漏掉这条静默路径");
        }

        // ══════════════ 不得误报的三种形态 ══════════════

        [Test]
        public void test_twoArgRealRequester_isNotReported()
        {
            var errs = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { void M() { rig.SetMode(CameraMode.Downed, 29); } }");
            Assert.That(errs, Is.Empty,
                "合法的双参调用被报 —— 门太宽,会把正确实现判红\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_multiLineCallWithRealRequester_isNotReported()
        {
            // 真树里的实际形态(`BootRoot.cs:564`):跨行的三元表达式 + 具名实参。
            var errs = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { void M() {\n" +
                "    rig.SetMode(downed ? CameraMode.Downed : CameraMode.Explore,\n" +
                "                 requesterId: 29);\n" +
                "} }");
            Assert.That(errs, Is.Empty,
                "跨行的合法双参调用被报 —— 实参切分或行号计算有问题\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_modeMachineReceiver_isExempt()
        {
            // `CameraRig.SetMode(mode)`(兼容重载)的实现体必须给机器层一个值,
            // 那里 `-1` 是**正确的** —— 唯一豁免。
            var errs = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { void M() { _modeMachine.SetMode(mode, requesterId: -1); } }");
            Assert.That(errs, Is.Empty,
                "`_modeMachine` 接收者的豁免失效 —— 兼容重载的实现体会把门弄红\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_commentAndStringMentions_areNotReported()
        {
            var comment = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "// 生产不得写 rig.SetMode(mode)\n" +
                "class X { }");
            Assert.That(comment, Is.Empty,
                "注释里的提及被当成违规 —— 剥离没生效,本门的文档会把自己弄红");

            var literal = CameraSetModeRequesterGate.CheckSource("Probe.cs",
                "class X { string s = \"rig.SetMode(mode)\"; }");
            Assert.That(literal, Is.Empty,
                "字符串字面量里的提及被当成违规 —— 剥离没生效");
        }

        // ══════════════ 判别力的自证:合法样本确实走完了判据 ══════════════

        [Test]
        public void test_noCallAtAll_isNotReported()
        {
            // 负控的负控:连 `.SetMode(` 都没有时不得报 —— 防「凡文件即红」的退化解。
            var errs = CameraSetModeRequesterGate.CheckSource("Probe.cs", NoViolation);
            Assert.That(errs, Is.Empty);
        }
    }
}
