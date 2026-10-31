// b2-decl 声明面门的 EditMode 判据(TD-gate-04 · sprint-21 `S21-5` 收口 · 2026-10-31)
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件补的是什么
// ══════════════════════════════════════════════════════════════════════════
// `CheckGateA`(b2 门 A)与 AC-24-07 白名单测都读 `GetReferencedAssemblies()` ——
// **编译产物元数据**。C# 只为**实际使用**的程序集写 `AssemblyRef` ⇒ 两条判据对
// 「asmdef 声明了、源码没用到」这一形态**结构性盲**。
//
// 实测出处(S20-1 证伪轮 F1 · 2026-10-25):只向 `Sim.asmdef` 注入 `"Sim.Codec"`
// 声明(不动任何源码)⇒ 重编译后 b2 门与 AC-24-07 **9/9 仍全绿**。
//
// 本文件的 `test_f1Counterexample_declaredButUnused_isCaught` 用**内存注入**复现
// 同一条反例:取真的 `Sim.asmdef` 文本、真的 `Sim` 使用集,只把 JSON 的 `references`
// 加一条 ⇒ 断言差集**必须**报出 `Sim.Codec`。
// ⚠️ 它不写盘(承 `.claude/rules/test-standards.md` 第 3 条;`FsWriteBoundaryGate` R2 面亦零触发)。
//
// ══════════════════════════════════════════════════════════════════════════
// 为什么必须覆盖 GUID 形
// ══════════════════════════════════════════════════════════════════════════
// 实测 4 支装配的 `references` 用 `GUID:<32 hex>` 记(`Sim.Contracts.Tests` /
// `Gameplay.Tests` / `SkeuomorphicUI.Tests` / `Casebook.Tests`)。
// 只认名字形的解析器在这 4 支上**全盲**,而失明的症状是**全绿** —— 与 TD-gate-04
// 本身是同一种失效形态。故 `test_parseRefs_guidForm_*` 两条是必需的,不是补充。
//
// ══════════════════════════════════════════════════════════════════════════
// 📖 只读外部夹具声明(test-standards 具名声明义务)
// ══════════════════════════════════════════════════════════════════════════
// 读仓库内文件(均为**只读**,零写盘):
//   · `Assets/Sim/Sim.asmdef`
//   · `Assets/Sim.Contracts/Sim.Contracts.asmdef`
//   · `Assets/Sim.Codec/Sim.Codec.asmdef`
// **为什么必须读真表而不是内联字符串**:被测的正是「真 asmdef 的声明面 ↔ 真装配的
// 元数据面」是否一致。内联一份假 JSON 会让本测退化成「测我的解析器」(那已有
// `test_parseRefs_*` 覆盖),并**失去**「今天真树双向一致」这个判据 —— 而后者才是
// 门不会一落地就红的证据。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>b2-decl 声明面门(<see cref="AssemblyGates.DiffDeclaredVsUsed"/> /
    /// <see cref="AssemblyGates.ParseDeclaredRefs"/>)的判别力判据。</summary>
    [TestFixture]
    public class RefDeclarationGateTest
    {
        private static readonly HashSet<string> Project =
            new HashSet<string> { "Sim", "Sim.Contracts", "Sim.Codec", "Gameplay.UI" };

        // ══════════════════ 纯函数 · 双向差集 ══════════════════

        /// <summary>声明 ∖ 使用 被检出 —— 即 TD-gate-04 的那一面。</summary>
        [Test]
        public void test_diffDecl_declaredOnly_isDetected()
        {
            var d = AssemblyGates.DiffDeclaredVsUsed(
                new[] { "Sim.Contracts", "Sim.Codec" }, new[] { "Sim.Contracts" }, Project);

            Assert.That(d.DeclaredOnly, Is.EqualTo(new[] { "Sim.Codec" }),
                "声明了 Sim.Codec 却没用到 ⇒ 必须落进 DeclaredOnly(否则本门对 TD-gate-04 无判别力)");
            Assert.That(d.UsedOnly, Is.Empty);
        }

        /// <summary>使用 ∖ 声明 被检出 —— 本门自身解析漏的 fail-closed 面。</summary>
        [Test]
        public void test_diffDecl_usedOnly_isDetected()
        {
            var d = AssemblyGates.DiffDeclaredVsUsed(
                new[] { "Sim.Contracts" }, new[] { "Sim.Contracts", "Sim.Codec" }, Project);

            Assert.That(d.UsedOnly, Is.EqualTo(new[] { "Sim.Codec" }),
                "用了却没声明 ⇒ 只可能是解析漏,必须报(否则「解析全失败」会静默全绿)");
            Assert.That(d.DeclaredOnly, Is.Empty);
        }

        /// <summary>BCL / 包程序名不得进差集(<paramref name="projectAssemblies"/> 过滤面)。</summary>
        [Test]
        public void test_diffDecl_nonProjectNames_areFilteredOut()
        {
            var d = AssemblyGates.DiffDeclaredVsUsed(
                System.Array.Empty<string>(),
                new[] { "System", "mscorlib", "netstandard", "UnityEngine", "Unity.Addressables" },
                Project);

            Assert.That(d.UsedOnly, Is.Empty, "非工程自有装配不在本条裁决面");
            Assert.That(d.DeclaredOnly, Is.Empty);
        }

        // ══════════════════ 纯函数 · references 解析 ══════════════════

        /// <summary>名字形:工程内的收下,包 / 引擎的滤掉。</summary>
        [Test]
        public void test_parseRefs_nameForm_keepsOnlyProjectAssemblies()
        {
            const string json = "{ \"name\": \"X\", \"references\": [ \"Sim.Contracts\", \"Unity.Addressables\" ] }";

            var names = AssemblyGates.ParseDeclaredRefs(
                json, new Dictionary<string, string>(), Project, out var unresolved);

            Assert.That(names, Is.EqualTo(new[] { "Sim.Contracts" }));
            Assert.That(unresolved, Is.Empty);
        }

        /// <summary>GUID 形已解析 ⇒ 交出装配名(4 支真装配用这种形)。</summary>
        [Test]
        public void test_parseRefs_guidForm_resolvesToName()
        {
            const string json = "{ \"references\": [ \"GUID:1d216fb8dbd4d7d60a90ce0016c7611a\" ] }";
            var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
                { ["1d216fb8dbd4d7d60a90ce0016c7611a"] = "Gameplay.UI" };

            var names = AssemblyGates.ParseDeclaredRefs(json, map, Project, out var unresolved);

            Assert.That(names, Is.EqualTo(new[] { "Gameplay.UI" }), "GUID 必须还原成装配名,否则 4 支装配全盲");
            Assert.That(unresolved, Is.Empty);
        }

        /// <summary>GUID 解析不了 ⇒ 进 unresolved,**不得静默丢弃**(丢弃 = 解析失败伪装成「没声明」)。</summary>
        [Test]
        public void test_parseRefs_unresolvedGuid_isReportedNotSwallowed()
        {
            const string json = "{ \"references\": [ \"GUID:ffffffffffffffffffffffffffffffff\" ] }";

            var names = AssemblyGates.ParseDeclaredRefs(
                json, new Dictionary<string, string>(), Project, out var unresolved);

            Assert.That(names, Is.Empty);
            Assert.That(unresolved, Has.Count.EqualTo(1).And.Contain("GUID:ffffffffffffffffffffffffffffffff"),
                "还原不了的 GUID 必须交出来 —— 静默丢弃会让「声明面失明」看起来是全绿");
        }

        /// <summary>空数组 / 无 references 字段 ⇒ 空集,不抛。</summary>
        [Test]
        public void test_parseRefs_emptyOrMissing_yieldsEmpty()
        {
            Assert.That(AssemblyGates.ParseDeclaredRefs("{ \"references\": [] }", null, Project, out var u1), Is.Empty);
            Assert.That(u1, Is.Empty);
            Assert.That(AssemblyGates.ParseDeclaredRefs("{ \"name\": \"NoRefs\" }", null, Project, out var u2), Is.Empty);
            Assert.That(u2, Is.Empty);
        }

        // ══════════════════ 端到端 · 真装配真文本 ══════════════════

        /// <summary>
        /// **TD-gate-04 的反例,内存复现**(对应 S20-1 证伪轮 F1):
        /// 取真 `Sim.asmdef` 文本 + 真 `Sim` 使用集,只往 `references` 加一条 `Sim.Codec`
        /// ⇒ 差集必须报出它。这是本条债「能不能被抓到」的直接判据。
        /// </summary>
        [Test]
        public void test_f1Counterexample_declaredButUnused_isCaught()
        {
            string path = Path.Combine(Application.dataPath, "Sim", "Sim.asmdef");
            Assert.That(File.Exists(path), Is.True, $"真 asmdef 未找到:{path}");

            var simAsm = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Sim");
            Assert.That(simAsm, Is.Not.Null, "装配「Sim」未加载");
            var sim = simAsm.GetReferencedAssemblies().Select(r => r.Name);
            var clean = AssemblyGates.ParseDeclaredRefs(
                File.ReadAllText(path), new Dictionary<string, string>(), AssemblyGates.DeclStrictSet, out _);

            var baseline = AssemblyGates.DiffDeclaredVsUsed(clean, sim, AssemblyGates.DeclStrictSet);
            Assert.That(baseline.DeclaredOnly, Is.Empty, "真 asmdef 今天不该有「声明未使用」");
            Assert.That(baseline.UsedOnly, Is.Empty);

            // F1 形态:只在声明面加一条,源码一字不动。
            string mutated = File.ReadAllText(path)
                .Replace("\"references\": [ \"Sim.Contracts\" ]",
                         "\"references\": [ \"Sim.Contracts\", \"Sim.Codec\" ]");
            Assert.That(mutated, Does.Contain("Sim.Codec"), "注入未生效 ⇒ 本测空转");

            var declaredMut = AssemblyGates.ParseDeclaredRefs(
                mutated, new Dictionary<string, string>(), AssemblyGates.DeclStrictSet, out _);
            var diff = AssemblyGates.DiffDeclaredVsUsed(declaredMut, sim, AssemblyGates.DeclStrictSet);

            Assert.That(diff.DeclaredOnly, Is.EqualTo(new[] { "Sim.Codec" }),
                "F1 反例未被抓到 ⇒ b2-decl 对 TD-gate-04 无判别力(编译产物面本就看不见这一条)");
        }

        /// <summary>真树今天双向一致:门不会一落地就红(否则是规格错,不是实现错)。</summary>
        [Test]
        public void test_realTree_noDeclaredOnly_noUsedOnly()
        {
            var asmdir = new Dictionary<string, string>
            {
                ["Sim"] = "Sim/Sim.asmdef",
                ["Sim.Contracts"] = "Sim.Contracts/Sim.Contracts.asmdef",
                ["Sim.Codec"] = "Sim.Codec/Sim.Codec.asmdef",
            };

            foreach (var kv in asmdir)
            {
                string path = Path.Combine(Application.dataPath, kv.Value);
                Assert.That(File.Exists(path), Is.True, $"真 asmdef 未找到:{path}");

                var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == kv.Key);
                Assert.That(asm, Is.Not.Null, $"装配「{kv.Key}」未加载");

                var declared = AssemblyGates.ParseDeclaredRefs(
                    File.ReadAllText(path), new Dictionary<string, string>(),
                    AssemblyGates.DeclStrictSet, out var unresolved);

                Assert.That(unresolved, Is.Empty, $"「{kv.Key}」有解析不了的引用(声明面对它失明)");
                var diff = AssemblyGates.DiffDeclaredVsUsed(
                    declared, asm.GetReferencedAssemblies().Select(r => r.Name), AssemblyGates.DeclStrictSet);
                Assert.That(diff.DeclaredOnly, Is.Empty, $"「{kv.Key}」声明了却没用:见报文");
                Assert.That(diff.UsedOnly, Is.Empty, $"「{kv.Key}」用了却没声明:见报文");
            }
        }
    }
}
