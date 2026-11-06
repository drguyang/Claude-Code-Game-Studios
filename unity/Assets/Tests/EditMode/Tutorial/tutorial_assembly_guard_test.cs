// S25-1 · tutorial-002 —— 48 的**装配护栏**判据(AC-48-04 引用面 / AC-48-11 asmdef 白名单)
//
// 权威来源:
//   · `design/gdd/tutorial-and-onboarding.md` **AC-48-04**(交付物不引 sim 类型,递归反射)
//     · **AC-48-11**(程序集引用白名单不含声明 `IEventSink` / `IEventAuthority` 的程序集,
//       载体 = **编译期**,非 grep)· 规则七(48 不写三流)
//   · `docs/architecture/adr-025` §①(装配清单)
//   · Story: production/epics/tutorial-onboarding/story-002-…
//
// ⚠️ 本件两半,强度不同,勿混读:
//   ① **引用面**(AC-48-04)= 反射扫描 —— 会红的门,负向夹具证明它**真的会红**;
//   ② **引用集**(AC-48-11)= 读**已编译产物**的 `GetReferencedAssemblies()` + 读 asmdef 源。
//      前者是编译后的事实(不是 grep 源文本),后者钉住「声明面的意图」。
//      两者必须**同时**成立:源声明对但产物多引一支(或反之)都会在这里分叉。
//
// ⚠️ 负向夹具住**测试命名空间**(承 `readonly_boundary_test` 同型先例):测试装配引用了
//    `Sim.Contracts`,所以能写出「故意漏的 DTO」;生产装配引用不到它 ⇒ 负例只能在这里。
//    这也正是本门**非空洞**的证明:若把扫描器改成恒返回空,三条负例同时转红。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.UI.Tutorial;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_assembly_guard_test
    {
        private const string TutorialAsm = "Gameplay.UI.Tutorial";
        private const string UiAsm = "Gameplay.UI";

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string AsmdefPath(string asmName)
            => Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.UI", "Tutorial", asmName + ".asmdef");

        private static string ReadAsmdef(string path)
        {
            Assert.That(File.Exists(path), Is.True, $"缺 asmdef:`{path}`");
            return File.ReadAllText(path);
        }

        private static IReadOnlyList<string> ReferencesOf(string asmdefJson)
        {
            var m = Regex.Match(asmdefJson, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            Assert.That(m.Success, Is.True, "asmdef 里没有 references 数组 —— 结构变了,本判据须同步");
            return Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\"")
                        .Select(x => x.Groups[1].Value).ToList();
        }

        private static Assembly TutorialAssembly => typeof(TutorialProjection).Assembly;

        // ══════════════ AC-48-04:引用面(递归反射)══════════════

        [Test]
        public void test_ac48_04_exportedSurface_hasNoSimTypes()
        {
            // Arrange:扫描面必须非空 —— 空面 ⇒ 循环零次 ⇒ 判据恒绿(那正是假绿本身)
            Type[] exported = TutorialAssembly.GetExportedTypes();
            Assert.That(exported, Is.Not.Empty,
                "48 装配公开面为空 —— 扫描面零输入,本判据会空转通过");
            Assert.That(exported, Does.Contain(typeof(TutorialProjection)),
                "投影入口必须在本装配的公开面内(否则扫的是别的装配)");

            // Act
            IReadOnlyList<string> violations = TutorialSurfaceGate.ScanAssembly(TutorialAssembly);

            // Assert
            Assert.That(violations, Is.Empty,
                "48 的公开面引用了 sim 类型(AC-48-04):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac48_04_negativeFixture_nestedGenericFix_isCaught()
        {
            // 嵌套泛型参数:`List<Fix>` 的 Fix 必须被递归扫到(字段 → 泛型实参)。
            IReadOnlyList<string> violations = TutorialSurfaceGate.Scan(typeof(LeakyNestedGeneric));

            Assert.That(violations, Is.Not.Empty, "嵌套泛型里的 Fix 未被抓住 —— 递归面破了(假绿)");
            Assert.That(violations.Any(v => v.Contains("Fix")), Is.True,
                "违例行须点名被命中的类型:\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac48_04_negativeFixture_ctorParameterSimEvent_isCaught()
        {
            // 构造参数不在 GetMethods 内 —— 漏扫会成假绿面。
            IReadOnlyList<string> violations = TutorialSurfaceGate.Scan(typeof(LeakyCtorParam));

            Assert.That(violations, Is.Not.Empty, "构造函数参数里的 SimEvent 未被抓住");
            Assert.That(violations.Any(v => v.Contains("SimEvent")), Is.True);
        }

        [Test]
        public void test_ac48_04_positiveControl_cleanType_passes()
        {
            // 正控:干净的 DTO 必须**零违例** —— 否则「门恒红」也能让上面两条负例通过。
            Assert.That(TutorialSurfaceGate.Scan(typeof(CleanFixtureDto)), Is.Empty);
        }

        [Test]
        public void test_ac48_04_nullRoot_isRed()
        {
            Assert.That(TutorialSurfaceGate.Scan(null), Is.Not.Empty,
                "null 入口必须报违例 —— 拒以空集冒充绿");
        }

        [Test]
        public void test_ac48_04_scanAssembly_null_isRed()
        {
            Assert.That(TutorialSurfaceGate.ScanAssembly(null), Is.Not.Empty,
                "null 装配必须报违例 —— 拒以空集冒充绿");
        }

        // ⚠️ **未覆盖分支(如实登记)**:`ScanAssembly` 的「公开面为空」守卫**今日无法触达** ——
        //    工程里没有零公开类型的程序集,而造一个动态空装配会让本件依赖 `AssemblyBuilder`
        //    的运行时行为(多一份不确定性)。⇒ 该分支记 **零输入可达**(承 ADR-035 N-4 的同款纪律),
        //    不声称「已兑现的守卫」;真要验它,须在**能造空装配**的轮次补一条(归测试基建)。

        // ══════════════ AC-48-11:程序集引用白名单 ══════════════

        [Test]
        public void test_ac48_11_forbiddenAssembly_isSimContracts()
        {
            // 先把「靶子是谁」钉死:`IEventSink` / `IEventAuthority` 声明在 Sim.Contracts。
            // 该断言若红,说明契约搬家了 —— 那时 §① 清单与本门都要重读,不能默默放行。
            Assembly sinkAsm = typeof(IEventSink).Assembly;
            Assembly authorityAsm = typeof(IEventAuthority).Assembly;

            Assert.That(sinkAsm.GetName().Name, Is.EqualTo("Sim.Contracts"));
            Assert.That(authorityAsm, Is.SameAs(sinkAsm),
                "IEventSink 与 IEventAuthority 必须在同一装配(白名单只有一条)");
        }

        [Test]
        public void test_ac48_11_asmdefWhitelist_isExactlyPresentation()
        {
            IReadOnlyList<string> refs = ReferencesOf(ReadAsmdef(AsmdefPath(TutorialAsm)));

            CollectionAssert.AreEqual(new[] { "Gameplay.Presentation" }, refs,
                "48 的 asmdef 引用白名单须**恰等于** { Gameplay.Presentation } —— " +
                "多一支(尤其 Sim.Contracts)即 AC-48-11 违例;少一支则投影拿不到内容表");
        }

        [Test]
        public void test_ac48_11_compiledReferences_excludeTheEventSinkAssembly()
        {
            // 编译**之后**的事实:产物引用的装配集里不得出现 Sim.Contracts / Sim / Sim.Codec。
            var refs = TutorialAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

            Assert.That(refs, Is.Not.Empty, "引用集为空 —— 读面失效,本判据会空转通过");
            Assert.That(refs, Does.Contain("Gameplay.Presentation"),
                "正控:作品装配必须引用边界层(否则「什么都不引」也能让下面的断言通过)");

            foreach (string forbidden in new[] { "Sim.Contracts", "Sim", "Sim.Codec" })
                Assert.That(refs, Does.Not.Contain(forbidden),
                    $"48 的编译产物引用了「{forbidden}」—— AC-48-11;引用一旦存在," +
                    "`IEventSink` 就能被写进源码(编译期结构性失效)");
        }

        [Test]
        public void test_ac48_11_whyNotGameplayUI_thatAssemblyDoesReferenceContracts()
        {
            // 本判据记录**落点选择的理由**(TR-tutorial-003 就近解释):
            // `Gameplay.UI` 引用了 Sim.Contracts ⇒ 48 若住那里,AC-48-11 在编译后不可能成立。
            // 若将来 Gameplay.UI 摘掉了该引用,本断言转红 ⇒ 逼人对「48 是否该并回」重新表态,
            // 而不是让历史理由静默腐坏。
            IReadOnlyList<string> uiRefs = ReferencesOf(
                ReadAsmdef(Path.Combine(RepoRoot, "unity", "Assets", "Gameplay.UI", "Gameplay.UI.asmdef")));

            Assert.That(uiRefs, Does.Contain("Sim.Contracts"),
                "Gameplay.UI 不再引用 Sim.Contracts —— 48 分装配的历史理由失效,须重读 ADR-025 §①");
        }

        // ══════════════ 负向 / 正控夹具(仅测试面)══════════════

        /// <summary>负例:嵌套泛型里的 `Fix`。</summary>
        private sealed class LeakyNestedGeneric
        {
            // ReSharper disable once NotAccessedField.Local —— 反射扫描不执行代码,字段无需被读
            public List<Fix> Values;
        }

        /// <summary>负例:构造参数是 `SimEvent`(<c>GetMethods</c> 不返回构造器)。</summary>
        private sealed class LeakyCtorParam
        {
            public LeakyCtorParam(SimEvent e) { }
        }

        /// <summary>正控:不含任何禁引类型的普通 DTO。</summary>
        private sealed class CleanFixtureDto
        {
            public int AuthorPlayerId;
            public string KindName;
            public readonly long Tick;
        }
    }
}
