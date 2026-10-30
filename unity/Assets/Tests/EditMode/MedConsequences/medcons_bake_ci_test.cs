// `TD-medcons-11` —— 53 三表的 CLI / CI 烘焙入口(`MedConsTableBakeRunner`)
//
// 权威:`tools/level/run_ci.sh` 的三态退出码约定 · `LevelCiRunner` 的同构先例
//       · `TD-ci-02`(batchmode **只执行第一条** `-executeMethod`,其余静默丢弃且退出码仍 0)
//       · ADR-014 §五(`ConfigVersion` = 各自的内容哈希 ⇒ 半套新半套旧会静默成立)
//
// ── 本文件钉什么 ─────────────────────────────────────────────────────────────
// ① **入口存在且是单一入口** —— 三张表由**一个** `-executeMethod` 内部循环跑完
//    (不是三条命令;后者只会跑第一条,`TD-ci-02`)。
// ② **退出码三态可分**:干净 ⇒ 0;源数据违规 ⇒ 1(且点名违规项);链跑不起来 ⇒ 2。
// ③ **全成功才落盘** —— 任一张失败时磁盘**一个字节都不动**。
//    这一条是**证伪 M1 实测暴露的**:初版「边烘边写」,第二张失败时第一张**已被覆盖**,
//    磁盘真处在混合态,而代码只打印不阻止 —— **打印不是阻止**。
// ④ **确定性** —— 同一份源烘两次,产物逐字节相同。
//
// ── 全程在临时目录(不碰仓库)──────────────────────────────────────────────────
// 夹具走 `Run(repoRoot, cookedDir, out report)` 这条**可注入路径**:自建仓根副本
// (只拷 `assets/data/53_*.json` 三份源)与产物目录,全在 `Path.GetTempPath()` 下。
// ⇒ 不违反 `.claude/rules/test-standards.md`「测试不得依赖文件系统外部状态」,
// 中途崩溃也不会把仓库留在脏状态。
//
// ⚠️ 进程级那条(`tools/bake/run_ci.sh`)**不在本文件**:它要起真 Unity 进程,
//    由**真跑**验证(见债册证据段),不在此伪造。

using System;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>`TD-medcons-11` —— 53 三表烘焙 CI 入口。</summary>
    [TestFixture]
    public sealed class MedConsBakeCiTest
    {
        private string _root;      // 临时根
        private string _repo;      // 假仓根(含 assets/data/)
        private string _cooked;    // 假产物目录

        /// <summary>
        /// 沙箱要复制的源文件。
        /// <para>⚠️ **`disease_registry.json` 必须在内**:`CostTable53Bake` 除读自己的表,
        /// 还要读它做**跨表断言**(「永不致死」:`injury_id` 须落在注册表的伤情词表里)。
        /// 只拷三张 53 表 ⇒ 第三张失败、退出码 1 —— 本夹具首轮就是这么红的。</para>
        /// </summary>
        private static readonly string[] SourceFiles =
        {
            "53_outcome_table.json", "53_delay_table.json", "53_cost_table.json",
            "disease_registry.json",
        };

        /// <summary>真实仓根(只用于**读**源文件做副本;不写它)。</summary>
        private static string RealRepoRoot()
            => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));

        [SetUp]
        public void CreateSandbox()
        {
            _root = Path.Combine(Path.GetTempPath(), "medcons-bake-" + Guid.NewGuid().ToString("N"));
            _repo = Path.Combine(_root, "repo");
            _cooked = Path.Combine(_root, "cooked");
            Directory.CreateDirectory(Path.Combine(_repo, "assets", "data"));
            Directory.CreateDirectory(_cooked);

            foreach (string f in SourceFiles)
            {
                string from = Path.Combine(RealRepoRoot(), "assets", "data", f);
                File.Copy(from, Path.Combine(_repo, "assets", "data", f), overwrite: true);
            }
        }

        [TearDown]
        public void DestroySandbox()
        {
            if (_root != null && Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }

        private string CookedPath(string name) => Path.Combine(_cooked, name);

        // ══════════════════════════════════════════════════════════════════
        // ① 单一入口覆盖三张表
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 入口的表清单**恰**是三张 53 表(与 `DataBakeMenu` 的常量同一出处,不复制字面量)。
        /// <para>漏一张 ⇒ 该表永不烘焙,而 CI 报绿 —— 这正是 `TD-ci-02` 的形态。</para>
        /// </summary>
        [Test]
        public void test_entry_coversExactlyTheThree53Tables()
        {
            Assert.AreEqual(3, MedConsTableBakeRunner.TableNames.Length,
                "53 恰三张表;新增表须同批扩 TableNames **与** BakeOne 的 switch");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    DataBakeMenu.OutcomeTableCookedAssetName,
                    DataBakeMenu.DelayTableCookedAssetName,
                    DataBakeMenu.CostTableCookedAssetName,
                },
                MedConsTableBakeRunner.TableNames,
                "表名须与 DataBakeMenu 的常量同源(复制字面量会漂)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 退出码三态
        // ══════════════════════════════════════════════════════════════════

        /// <summary>干净源 ⇒ 退出码 0,报文逐表列出产物大小,且三份产物**真的落盘**。</summary>
        [Test]
        public void test_cleanSource_exitGreen_writesAllThree()
        {
            int code = MedConsTableBakeRunner.Run(_repo, _cooked, out string report);

            Assert.AreEqual(MedConsTableBakeRunner.ExitGreen, code, "干净源 ⇒ 0\n报告:\n" + report);
            foreach (string n in MedConsTableBakeRunner.TableNames)
            {
                Assert.That(report, Does.Contain(n), $"报文须逐表列出({n} 缺失 = 静默跳过)");
                Assert.IsTrue(File.Exists(CookedPath(n)), $"产物须真的落盘:{n}");
                Assert.Greater(new FileInfo(CookedPath(n)).Length, 0, $"{n} 不得为空文件");
            }
        }

        /// <summary>
        /// 源数据违规 ⇒ 退出码 **1**,报文点名违规项,且**磁盘一个字节都没动**。
        /// <para><b>本条是 `TD-medcons-11` 的核心判据</b>(「命令存在」≠「命令会红」),
        /// 同时钉住证伪 M1 暴露的半套写入问题。</para>
        /// </summary>
        [Test]
        public void test_invalidSource_exitViolation_namesIt_andTouchesNoFile()
        {
            // ⚠️ **失败轮前必须把产物目录清空**。
            // 首版是「先跑干净轮建基线,再逐字节比对」—— **证伪 M2 全绿**才发现它零判别力:
            // 同一份源烘出的字节与基线**逐字节相同** ⇒ 「边烘边写」与「全成功才落盘」
            // 在磁盘上**看不出差别**(真实值同构型假存活)。
            // ⇒ 改为「清空目录 → 失败轮 → 断言目录**仍为空**」:「没写」= 文件不存在。
            Directory.CreateDirectory(_cooked);
            foreach (string n in MedConsTableBakeRunner.TableNames)
                if (File.Exists(CookedPath(n))) File.Delete(CookedPath(n));

            // residual 的 delay_ticks 改成 0 ⇒ binder 拒收(AC-53-06:定义域内每条 > 0)
            string src = Path.Combine(_repo, "assets", "data", "53_delay_table.json");
            string good = File.ReadAllText(src);
            string mutated = good.Replace("\"delay_ticks\": 600", "\"delay_ticks\": 0");
            Assert.AreNotEqual(good, mutated, "前提到位:替换须真的改到东西(否则本测空转)");
            File.WriteAllText(src, mutated);

            int code = MedConsTableBakeRunner.Run(_repo, _cooked, out string report);

            Assert.AreEqual(MedConsTableBakeRunner.ExitViolation, code, "源数据违规 ⇒ 1");
            Assert.That(report, Does.Contain("delay_ticks=0"),
                "报文须点名**具体**违规项(只说『失败』读者无从下手)");
            Assert.That(report, Does.Contain("一个字节都没动"),
                "失败时磁盘须原封不动 —— 半套新半套旧会让存档头校验静默通过");

            foreach (string n in MedConsTableBakeRunner.TableNames)
            {
                Assert.IsFalse(File.Exists(CookedPath(n)),
                    $"{n} 在**失败轮**里被写出 —— 「全成功才落盘」失效" +
                    "(边烘边写会让磁盘处在半套新半套旧,而存档头只记一个 ConfigVersion ⇒ 静默通过)");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 确定性
        // ══════════════════════════════════════════════════════════════════

        /// <summary>同一份源烘两次 ⇒ 三份产物**逐字节**相同(ADR-012 / ADR-014 deterministic)。</summary>
        [Test]
        public void test_sameSourceTwice_byteIdentical()
        {
            Assert.AreEqual(MedConsTableBakeRunner.ExitGreen,
                MedConsTableBakeRunner.Run(_repo, _cooked, out _), "前置:第一轮须成功");

            byte[][] first = MedConsTableBakeRunner.TableNames
                .Select(n => File.ReadAllBytes(CookedPath(n))).ToArray();

            Assert.AreEqual(MedConsTableBakeRunner.ExitGreen,
                MedConsTableBakeRunner.Run(_repo, _cooked, out _), "第二轮须成功");

            for (int i = 0; i < first.Length; i++)
            {
                string n = MedConsTableBakeRunner.TableNames[i];
                CollectionAssert.AreEqual(first[i], File.ReadAllBytes(CookedPath(n)),
                    $"{n} 两轮烘焙结果不同 —— 产物不 deterministic");
            }
        }
    }
}
