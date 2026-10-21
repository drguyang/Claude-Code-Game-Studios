// `PlayerAssemblyGate` 的判据(`TD-testasm-01` · 2026-10-21)。
//
// ══════════════════════════════════════════════════════════════════════════
// 为什么这个门必须有测试 —— 它防的是「8 天没人发现」
// ══════════════════════════════════════════════════════════════════════════
// 2026-10-13 起 4 个 PlayMode 文件裸用 `UnityEditor.AssetDatabase`,
// 使 IL2CPP player 构建**结构性失败 8 天**。期间:
//   · 全量 EditMode 回归**每次都绿**(`Sim.Contracts.Tests` 的 `includePlatforms: ["Editor"]`,
//     那 4 件根本不在 EditMode 树里);
//   · 唯一的发现途径是「跑一次 IL2CPP 构建」= 数十分钟。
// ⇒ 本门的价值全在**没人跑构建时也红**。若它的测试只断言「现在零红」,
//   那与「跑一次构建看结果」是同一种滞后判据 —— 必须证明它**抓得住**那个形态。
//
// ══════════════════════════════════════════════════════════════════════════
// 判据分三组(缺一即不成立)
// ══════════════════════════════════════════════════════════════════════════
// ① **负向纯函数**(`Scan` 直喂源码,不碰文件系统)——
//    裸用 ⇒ 红 / 守卫内 ⇒ 绿 / 注释里 ⇒ 绿 / `#else` 分支 ⇒ 绿(那是编辑器侧)。
//    ⚠️ 「注释里 ⇒ 绿」这一条是**实测教训**:首轮普查 `grep -rln 'UnityEditor'`
//    得 6 个文件,其中 `direct_read_channel_phase_test.cs` 的唯一命中在注释里 ——
//    若判据写成「扫到就报」会**误报**。本组钉住「零误报」。
// ② **正向真树** —— 扫真 `Assets/Tests/PlayMode`,要求 **零红**。
//    ⚠️ 这条**不是**同义反复:它同时钉住「那 4 件确实修好了」。
// ③ **非空守卫** —— 扫描面必须真的扫到文件。空树时本门恒真 = 假绿。
//
// ⚠️ 本文件**只读文件系统**(经 `PlayerAssemblyGate.RunAll`),不写任何文件
//    —— 承 `.claude/rules/test-standards.md` 第 3 条 + `FsWriteBoundaryGate` 的 R2 判据。

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>
    /// `PlayerAssemblyGate` 的三组判据:负向纯函数 / 正向真树 / 非空守卫。
    /// </summary>
    [TestFixture]
    public class PlayerAssemblyGateTest
    {
        private static string UnityProjectRoot() => GateChainSource.ProjectRoot(Application.dataPath);

        // ══════════════════════════════════════════════════════════════════════
        // 一、负向纯函数夹具(直接喂源码,不碰文件系统)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>裸用 `UnityEditor.X` 且零守卫 ⇒ **必红**(这正是那 4 件的形态)。</summary>
        [Test]
        public void test_unguardedAssetDatabaseUse_isRed()
        {
            const string src = @"
using UnityEditor;
class C { void M() { AssetDatabase.LoadAssetAtPath<object>(""a""); } }";
            List<PlayerAssemblyGate.Violation> v = PlayerAssemblyGate.Scan("x.cs", src);
            Assert.That(v.Count, Is.EqualTo(1), "裸用 UnityEditor 必须红");
            Assert.That(v[0].Line, Is.EqualTo(2), "行号须指向 using UnityEditor 那一行");
        }

        /// <summary>限定名形态(`UnityEditor.AssetDatabase.X`)无 using ⇒ 同样必红。</summary>
        [Test]
        public void test_fullyQualifiedEditorCall_isRed()
        {
            const string src = "class C { void M() { UnityEditor.AssetDatabase.Refresh(); } }";
            List<PlayerAssemblyGate.Violation> v = PlayerAssemblyGate.Scan("x.cs", src);
            Assert.That(v.Count, Is.EqualTo(1), "限定名形态不得逃逸");
        }

        /// <summary>整段包在 `#if UNITY_EDITOR` 内 ⇒ **必绿**(这是本门要放行的正解)。</summary>
        [Test]
        public void test_guardedByUnityEditorIf_isGreen()
        {
            const string src = @"
class C { void M() {
#if UNITY_EDITOR
    UnityEditor.AssetDatabase.Refresh();
#endif
} }";
            Assert.That(PlayerAssemblyGate.Scan("x.cs", src), Is.Empty,
                "守卫内的调用是 player 构建的正解,不得报");
        }

        /// <summary>
        /// `#if !UNITY_EDITOR … #else <裸用> #endif` ⇒ **必绿**。
        /// <para>`#else` 分支 = 编辑器侧活跃区,那正是 `AssetDatabase` 该待的地方。
        /// 本门的保守处理是「少报」,不会把合法的 `#else` 判红。</para>
        /// </summary>
        [Test]
        public void test_elseBranchOfNegatedGuard_isGreen()
        {
            const string src = @"
class C { void M() {
#if !UNITY_EDITOR
    return;
#else
    UnityEditor.AssetDatabase.Refresh();
#endif
} }";
            Assert.That(PlayerAssemblyGate.Scan("x.cs", src), Is.Empty,
                "`#else` 是编辑器侧 —— 必须放行");
        }

        /// <summary>
        /// **注释里的 `UnityEditor` ⇒ 必绿**。
        /// <para>实测由来:`direct_read_channel_phase_test.cs:56` 的唯一命中是
        /// 「规避 UnityEditor 资产加载依赖」这句注释 —— 首轮 `grep` 普查把它算进去了。
        /// 判据若写「扫到就报」,这里会**误报**。</para>
        /// </summary>
        [Test]
        public void test_unityEditorMentionedInComment_isGreen()
        {
            const string src = @"
class C { void M() {
    // 不依赖 UnityEditor 资产加载
    /* UnityEditor.AssetDatabase.Refresh() 是编辑器专有 */
    int x = 0;
} }";
            Assert.That(PlayerAssemblyGate.Scan("x.cs", src), Is.Empty,
                "注释里的提及不是代码引用 —— 误报会让人不信这个门");
        }

        /// <summary>
        /// **字符串字面量里的 `UnityEditor.X` ⇒ 必绿**。
        /// <para>实测由来:修好那 4 件后,`Assert.Inconclusive("[NOT-RUN in player] 本测依赖
        /// UnityEditor.AssetDatabase…")` 这类**说明串**会被裸词判据当成引用 ⇒ **4 条误报**。
        /// 串里的 `UnityEditor.X` 不是代码,在 player 里也不会 CS0234。</para>
        /// </summary>
        [Test]
        public void test_unityEditorInsideStringLiteral_isGreen()
        {
            const string src = @"
class C { void M() {
    Assert.Inconclusive(""[NOT-RUN in player] 本测依赖 UnityEditor.AssetDatabase"");
} }";
            Assert.That(PlayerAssemblyGate.Scan("x.cs", src), Is.Empty,
                "字符串字面量里的提及不是代码引用");
        }

        /// <summary>守卫**只覆盖一段**,另一段的裸用仍必红(文件级「有守卫」不得逃逸)。</summary>
        [Test]
        public void test_guardCoversOnlyItsOwnBlock_laterUseStillRed()
        {
            const string src = @"
class C {
#if UNITY_EDITOR
    void A() { UnityEditor.AssetDatabase.Refresh(); }
#endif
    void B() { UnityEditor.AssetDatabase.Refresh(); }
}";
            List<PlayerAssemblyGate.Violation> v = PlayerAssemblyGate.Scan("x.cs", src);
            Assert.That(v.Count, Is.EqualTo(1), "守卫外的裸用不得被同文件的守卫掩护");
            Assert.That(v[0].Line, Is.EqualTo(6), "红行须是守卫**外**那一行");
        }

        /// <summary>
        /// `#if UNITY_EDITOR … #else <裸用> #endif` ⇒ **必红**。
        /// <para>与上一条 `#if !UNITY_EDITOR` 的 `#else` 恰好相反 —— 这一条钉的是**极性**。
        /// 写反的后果是把 player 侧当编辑器侧放行(漏报),而那 4 件的裸用正是落在 player 侧。</para>
        /// </summary>
        [Test]
        public void test_elseBranchOfPositiveGuard_isRed()
        {
            const string src = @"
class C { void M() {
#if UNITY_EDITOR
    return;
#else
    UnityEditor.AssetDatabase.Refresh();
#endif
} }";
            List<PlayerAssemblyGate.Violation> v = PlayerAssemblyGate.Scan("x.cs", src);
            Assert.That(v.Count, Is.EqualTo(1),
                "`#if UNITY_EDITOR` 的 `#else` = **player 侧** ⇒ 裸用必须报");
            Assert.That(v[0].Line, Is.EqualTo(6));
        }

        /// <summary>零引用 ⇒ 零违规(空输入不得抛)。</summary>
        [Test]
        public void test_noEditorReference_isEmpty()
        {
            Assert.That(PlayerAssemblyGate.Scan("x.cs", "class C { int x; }"), Is.Empty);
            Assert.That(PlayerAssemblyGate.Scan("x.cs", ""), Is.Empty);
        }

        // ══════════════════════════════════════════════════════════════════════
        // 二、正向真树(钉住「那 4 件确实修好了」)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 真 `Assets/Tests/PlayMode` 树 ⇒ **零红**。
        /// <para>⚠️ 这条同时是那 4 个既有文件的**回归钉** —— 它们若被还原成裸用,这里红。</para>
        /// </summary>
        [Test]
        public void test_realPlayModeTree_zeroRed()
        {
            List<string> errs = PlayerAssemblyGate.RunAll(UnityProjectRoot());
            Assert.That(errs, Is.Empty,
                "PlayMode 树里出现未守卫的 UnityEditor 引用 —— IL2CPP player 会构建失败:\n  " +
                string.Join("\n  ", errs));
        }

        /// <summary>
        /// **非空守卫**:扫描面必须真的扫到文件。
        /// <para>承 D5 / D6 的非空守卫口径 —— 空树时 `RunAll` 恒返回空 = **假绿**。
        /// 本测独立于门内的 `scanned == 0` 判据(两条互为冗余,刻意保留)。</para>
        /// </summary>
        [Test]
        public void test_scanSurface_isNotEmpty()
        {
            string dir = Path.Combine(UnityProjectRoot(), PlayerAssemblyGate.ScanDirRelative);
            Assert.That(Directory.Exists(dir), Is.True, $"扫描目录不存在:{dir}");
            int n = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
            Assert.That(n, Is.GreaterThan(0),
                "扫描面为空时本门恒真 —— 那是假绿,不是通过");
        }

        /// <summary>
        /// 那 4 个**原始肇事文件**仍在树里,且现在**零违规**。
        /// <para>⚠️ 逐文件点名,而不是只靠全树零红 —— 全树零红在「文件被删」时也绿,
        /// 而删文件不是修好。本测钉住「修了,不是删了」。</para>
        /// </summary>
        [Test]
        public void test_fourOriginalOffenders_stillExistAndAreClean()
        {
            string root = UnityProjectRoot();
            string[] offenders =
            {
                "Assets/Tests/PlayMode/SkeuomorphicUI/sdf_font_render_path_probe_test.cs",
                "Assets/Tests/PlayMode/SkeuomorphicUI/skeuo_stylesheet_mount_probe_test.cs",
                "Assets/Tests/PlayMode/SkeuomorphicUI/sdf_font_coverage_probe_test.cs",
                "Assets/Tests/PlayMode/SkeuomorphicUI/casebook_reading_inline_test.cs",
            };

            foreach (string rel in offenders)
            {
                string full = Path.Combine(root, rel);
                Assert.That(File.Exists(full), Is.True,
                    $"{rel} 不在树里 —— 删文件不是修好(TD-testasm-01 的 4 个原始肇事件须在册)");

                List<PlayerAssemblyGate.Violation> v =
                    PlayerAssemblyGate.Scan(rel, File.ReadAllText(full));
                Assert.That(v, Is.Empty,
                    $"{rel} 仍有未守卫的 UnityEditor 引用(第 {v.FirstOrDefault()?.Line} 行)");
            }
        }
    }
}
