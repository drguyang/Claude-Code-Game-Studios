// M2 接线轮阶段 2 · 批次 D —— b7 写者存在性门测试(EditMode)。
//
// 门逻辑:entities.yaml 每支带 `author:` 的 Kind ∈ 已写者集(扫描面内
//   `Encode(EventKind.X` 生产写入点)∪ 具名豁免表;否则红。
//
// 本文件覆盖:
//   ① 正向:真工程(真 yaml + 真扫描面)零红错;
//   ② 解析面:author 条目数非零 + `CaseOpened` / `ResourceHarvested` 在册且**不在豁免表**;
//   ③ 行为级负向(工程外探针,不污染工程):
//        a. 声明了 author、扫描面内无写者、不在豁免表 ⇒ 红(缺写者被抓);
//        b. 补上写者 ⇒ 该 Kind 转绿(扫描体真在扫,不是恒红);
//        c. 只有注释提到写法 ⇒ 不算写者(剥注释在跑);
//   ④ 豁免纪律:已写者仍挂豁免表 ⇒ 红(豁免 = 红);
//   ⑤ 陈旧豁免:registry 已无该 Kind ⇒ 红;yaml 缺失 / 零 author / 零写者 ⇒ 红(不冒充绿);
//   ⑥ 结构:扫描面含 Sim / Presentation / Boot 三面、无重复、不含 Sim.Codec / Tests;
//           豁免表每条带登记日期。
//
// ⚠️ 探针 yaml 刻意只声明 1–2 支 Kind ⇒ 真表的 24 条豁免在探针上全属「陈旧」,
//    故探针断言一律用 `Any(...)` 具名匹配,不做整表计数(整表计数只在真工程面)。
//
// 权威:ADR-024 §①(registry 真源)· ADR-005(主机唯一 Append)· ADR-029 §③(唯一编码路径)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using WriterGate = DaYiJingCheng.EditorTools.Gates.WriterExistenceGate;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary>b7 写者存在性门测试。</summary>
    [TestFixture]
    public sealed class WriterExistenceGateTest
    {
        // ══════════ ① 正向:真工程零红错 ══════════

        [Test]
        public void test_b7_realRepo_authoredKindsAllCovered_green()
        {
            var errs = WriterGate.RunAll();
            Assert.IsEmpty(errs, "b7 红行:\n" + string.Join("\n", errs));
        }

        // ══════════ ② 解析面 ══════════

        [Test]
        public void test_b7_registry_authorsParsed_caseOpenedAndResourceHarvestedUnwaived()
        {
            var parseErrs = new List<string>();
            var authored = WriterGate.ParseAuthoredKinds(WriterGate.EntitiesYamlPath(), parseErrs);

            Assert.IsEmpty(parseErrs, "解析须无错:\n" + string.Join("\n", parseErrs));
            Assert.GreaterOrEqual(authored.Count, 30,
                "author 条目数下限(ADR-024 A1 后 = 35)—— 归零/骤减 = 解析面丢失(假绿面);" +
                "实测 " + authored.Count);

            var kinds = authored.Select(a => a.Kind).ToList();
            CollectionAssert.Contains(kinds, "CaseOpened", "registry 在册");
            CollectionAssert.Contains(kinds, "ResourceHarvested", "registry 在册");

            var waived = WaivedKinds();
            Assert.IsFalse(waived.Contains("CaseOpened"),
                "本批刚落地的写者不得挂豁免(豁免 = 红)");
            Assert.IsFalse(waived.Contains("ResourceHarvested"),
                "本批刚落地的写者不得挂豁免(豁免 = 红)");

            var written = ScanRealDirs();
            Assert.IsTrue(written.Contains("CaseOpened"), "CaseOpened 生产写者须在扫描面内");
            Assert.IsTrue(written.Contains("ResourceHarvested"), "ResourceHarvested 生产写者须在扫描面内");
        }

        // ══════════ ③ 行为级负向:工程外探针(照 b6 先例)══════════

        [Test]
        public void test_b7_probe_missingWriter_isRed_thenWriterMakesItGreen()
        {
            string tmp = Path.Combine(Path.GetTempPath(),
                "b7_writer_probe_" + Guid.NewGuid().ToString("N"));
            string srcDir = Path.Combine(tmp, "src");
            Directory.CreateDirectory(srcDir);
            string yaml = Path.Combine(tmp, "entities.yaml");
            try
            {
                // 探针 registry:两支 author 声明(Kind 名借用真名,豁免表不含二者)
                File.WriteAllText(yaml,
                    "  - name: SimEvent.Kind.CaseOpened\n" +
                    "    stream: case\n" +
                    "    author: \"probe 37\"\n" +
                    "  - name: SimEvent.Kind.PoiStateChanged\n" +
                    "    stream: world\n" +
                    "    author: \"probe 6\"\n");

                // 种子写者(Kind 未在探针 yaml 声明)—— 让扫描体先有 1 个写者,
                // 绕开「零写者 = 假绿面」护栏,单独一条测试守该护栏(见 ⑤)。
                WriteProbeWriter(srcDir, "Seed.cs", "DiseaseOnset");

                // ── a. 两支声明的 Kind 都无写者 ⇒ 两支都红(缺写者被抓)──
                var errs = WriterGate.Check(yaml, new[] { srcDir });
                Assert.IsTrue(HasMissingWriterError(errs, "CaseOpened"),
                    "CaseOpened 缺写者须红。实测:\n" + string.Join("\n", errs));
                Assert.IsTrue(HasMissingWriterError(errs, "PoiStateChanged"),
                    "PoiStateChanged 缺写者须红。实测:\n" + string.Join("\n", errs));

                // ── b. 给 CaseOpened 补一个生产写者 ⇒ 它转绿,只剩 PoiStateChanged 红 ──
                WriteProbeWriter(srcDir, "CaseWriter.cs", "CaseOpened");
                errs = WriterGate.Check(yaml, new[] { srcDir });
                Assert.IsFalse(HasMissingWriterError(errs, "CaseOpened"),
                    "补写者后 CaseOpened 不得再报缺写者。实测:\n" + string.Join("\n", errs));
                Assert.IsTrue(HasMissingWriterError(errs, "PoiStateChanged"),
                    "PoiStateChanged 仍须红(未补写者)。实测:\n" + string.Join("\n", errs));

                // ── c. 删掉写者文件、只留注释 ⇒ 红(剥注释在跑,文档引用不自伤也不借绿)──
                File.Delete(Path.Combine(srcDir, "CaseWriter.cs"));
                File.WriteAllText(Path.Combine(srcDir, "Doc.cs"),
                    "// 只有注释:e.Encode(EventKind.CaseOpened, default);\n" +
                    "class Doc { }\n");
                errs = WriterGate.Check(yaml, new[] { srcDir });
                Assert.IsTrue(HasMissingWriterError(errs, "CaseOpened"),
                    "注释引用不得被计为写者。实测:\n" + string.Join("\n", errs));
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        [Test]
        public void test_b7_probe_waivedKindWithWriter_isRed()
        {
            // 「已存在写者的 Kind 绝不豁免」——用豁免表内的真名 Craft 做探针:
            // 声明 author + 有写者 ⇒ 判据 ① 红。
            string tmp = Path.Combine(Path.GetTempPath(),
                "b7_waiver_probe_" + Guid.NewGuid().ToString("N"));
            string srcDir = Path.Combine(tmp, "src");
            Directory.CreateDirectory(srcDir);
            string yaml = Path.Combine(tmp, "entities.yaml");
            try
            {
                File.WriteAllText(yaml,
                    "  - name: SimEvent.Kind.Craft\n" +
                    "    stream: world\n" +
                    "    author: \"probe 18\"\n");
                WriteProbeWriter(srcDir, "CraftWriter.cs", "Craft");

                var errs = WriterGate.Check(yaml, new[] { srcDir });
                Assert.IsTrue(errs.Any(e => e.Contains("「Craft」") && e.Contains("豁免")),
                    "Craft 有写者却挂豁免 ⇒ 判据 ① 红(豁免 = 红)。实测:\n"
                    + string.Join("\n", errs));
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        [Test]
        public void test_b7_probe_staleWaiver_isRed()
        {
            // registry 删了某 Kind,豁免表没同步 ⇒ 陈旧豁免红。
            // 探针 yaml 只留一支有写者的 Kind(其余真 Kind 全部视为已删)。
            string tmp = Path.Combine(Path.GetTempPath(),
                "b7_stale_probe_" + Guid.NewGuid().ToString("N"));
            string srcDir = Path.Combine(tmp, "src");
            Directory.CreateDirectory(srcDir);
            string yaml = Path.Combine(tmp, "entities.yaml");
            try
            {
                File.WriteAllText(yaml,
                    "  - name: SimEvent.Kind.CaseOpened\n" +
                    "    stream: case\n" +
                    "    author: \"probe 37\"\n");
                WriteProbeWriter(srcDir, "CaseWriter.cs", "CaseOpened");

                var errs = WriterGate.Check(yaml, new[] { srcDir });
                var stale = errs.Where(e => e.Contains("陈旧豁免")).ToList();
                Assert.IsNotEmpty(stale,
                    "24 条豁免在探针 registry 全属陈旧,至少报 1 条。实测:\n"
                    + string.Join("\n", errs));
                Assert.IsTrue(stale.Any(e => e.Contains("SkillGrown")),
                    "须点名被删 Kind(SkillGrown 为例)");
                Assert.IsFalse(HasMissingWriterError(errs, "CaseOpened"),
                    "有写者的 CaseOpened 不该报缺写者(该条错误缺席 = 判据没串味)");
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        [Test]
        public void test_b7_probe_emptyInputs_isRed_notFakeGreen()
        {
            // 假绿防护三连:① yaml 不存在;② yaml 零 author;③ 扫描面零写者。
            string tmp = Path.Combine(Path.GetTempPath(),
                "b7_empty_probe_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            string srcDir = Path.Combine(tmp, "src");
            Directory.CreateDirectory(srcDir);
            try
            {
                // ① 文件不存在
                var errs = WriterGate.Check(Path.Combine(tmp, "nope.yaml"), new[] { srcDir });
                Assert.IsTrue(errs.Any(e => e.Contains("不可读")),
                    "yaml 缺失须红(不以空集冒充绿)。实测:\n" + string.Join("\n", errs));

                // ② 零 author 条目
                string empty = Path.Combine(tmp, "entities.yaml");
                File.WriteAllText(empty, "# 空\n");
                errs = WriterGate.Check(empty, new[] { srcDir });
                Assert.IsTrue(errs.Any(e => e.Contains("0 支")),
                    "解析出 0 支须红(假绿面)。实测:\n" + string.Join("\n", errs));

                // ③ 扫描面零写者(有 author、目录在、但一个写入点都没有)
                File.WriteAllText(empty,
                    "  - name: SimEvent.Kind.CaseOpened\n" +
                    "    stream: case\n" +
                    "    author: \"probe 37\"\n");
                errs = WriterGate.Check(empty, new[] { srcDir });
                Assert.IsTrue(errs.Any(e => e.Contains("假绿面")),
                    "零写者扫描面须红(假绿面)。实测:\n" + string.Join("\n", errs));
            }
            finally
            {
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            }
        }

        // ══════════ ⑥ 结构断言(扫描面 / 豁免表)══════════

        [Test]
        public void test_b7_scanDirs_containsWriterFaces_excludesCodecAndTests()
        {
            // BCD-码-5(2026-10-10)收紧:原断言只钉 3 面 ⇒ 从数组删 Input / UI / Contracts
            // 任一面不红(评审原话「测试只断言 ≥3 面」)。现**六面全断言**:
            // 删任一面 = 该面写者退回零门 = 本测红。
            var dirs = ScanDirsField();
            Assert.IsNotEmpty(dirs, "扫描面数组须存在且非空(显式登记,不得内联魔法值)");
            Assert.AreEqual(dirs.Length, dirs.Distinct().Count(),
                "扫描面不得有重复目录: " + string.Join(", ", dirs));

            CollectionAssert.Contains(dirs, "Assets/Sim", "Sim = 主写者宿主面(b6 同款)");
            CollectionAssert.Contains(dirs, "Assets/Sim.Contracts",
                "契约面(BCD-码-5 扩面:SkillGrownEmitter 等已住此 —— 删 = 契约面写者退回零门)");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.Presentation",
                "表现层写者面(ActorCellEntered 两写者在此)");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.Boot", "装配同型扩面(防组合根侧写者不可见)");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.Input",
                "输入装配面(BCD-码-5 收紧:原「≥3 面」断言下删此面不红)");
            CollectionAssert.Contains(dirs, "Assets/Gameplay.UI",
                "UI 装配面(BCD-码-5 收紧:原「≥3 面」断言下删此面不红)");

            Assert.IsFalse(dirs.Any(d => d.Contains("Sim.Codec")),
                "Sim.Codec 是编码器分派体,不是写者面");
            Assert.IsFalse(dirs.Any(d => d.Contains("Tests")),
                "测试桩不构成生产写者");
        }

        [Test]
        public void test_b7_waiverTable_everyEntryHasDatedReason()
        {
            var waivers = WaiverEntries();
            Assert.IsNotEmpty(waivers, "豁免表须显式存在(当前基线 = 24 条)");
            foreach (var (kind, reason) in waivers)
            {
                Assert.IsNotEmpty(kind, "豁免条目不得空 Kind");
                Assert.IsNotEmpty(reason, $"豁免「{kind}」须附理由 + 归属轮");
                StringAssert.Contains("2026-10", reason,
                    $"豁免「{kind}」须带登记日期(否则成无期债务):{reason}");
            }
        }

        [Test]
        public void test_b7_waiverTable_noOverlapWithWrittenKinds()
        {
            // 判据 ① 的直接断言(与 RunAll 同判据、不同入口:防门体被绕过)
            var written = ScanRealDirs();
            var overlap = WaivedKinds().Where(written.Contains).ToList();
            Assert.IsEmpty(overlap,
                "已写者不得挂豁免(豁免 = 红),违例:" + string.Join(", ", overlap));
        }

        // ══════════ 辅助 ══════════

        /// <summary>在探针目录写一个只含单 Kind 写入点的生产形态文件。</summary>
        private static void WriteProbeWriter(string dir, string fileName, string kind)
        {
            File.WriteAllText(Path.Combine(dir, fileName),
                "using DaYiJingCheng.Sim.Contracts;\n" +
                "class " + Path.GetFileNameWithoutExtension(fileName) + " {\n" +
                "  void M(IPayloadEncoder e) {\n" +
                "    var r = e.Encode(EventKind." + kind + ", default(CaseOpenedPayload));\n" +
                "  }\n" +
                "}\n");
        }

        /// <summary>是否含「Kind「X」…写者缺失」形态的红行。</summary>
        private static bool HasMissingWriterError(List<string> errs, string kind)
            => errs.Any(e => e.Contains($"「{kind}」") && e.Contains("写者缺失"));

        private static string[] ScanDirsField()
        {
            var f = typeof(WriterGate).GetField("WriterScanDirs",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "扫描面数组字段须存在");
            return (string[])f.GetValue(null);
        }

        private static (string Kind, string Reason)[] WaiverEntries()
        {
            var f = typeof(WriterGate).GetField("ExemptKinds",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "豁免表字段须存在(即使为空也须显式)");
            return ((string Kind, string Reason)[])f.GetValue(null);
        }

        private static HashSet<string> WaivedKinds()
            => new HashSet<string>(WaiverEntries().Select(w => w.Kind));

        private static HashSet<string> ScanRealDirs()
            => WriterGate.ScanWrittenKinds(ScanDirsField(), new List<string>());
    }
}
