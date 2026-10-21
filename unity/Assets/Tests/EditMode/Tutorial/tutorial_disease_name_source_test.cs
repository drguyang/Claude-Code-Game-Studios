// S17-3 · story-001 —— 校验规则 ④ 的**生产**病名闭集来源(`TutorialDiseaseNameSource`)测试。
//
// 立测缘由(2026-10-21 修整轮):该接缝此前**生产路径零实现者** ⇒ 第 ④ 项恒为 `TableNotReady`,
//   AC-48-13 只能记 NOT-RUN。本文件证明三件事,缺一不可:
//     ① **真表接上之后 ④ 真跑**(`AllSixChecksExecuted == true` 且 `Outcome == Clean`);
//     ② **真表有判别力** —— 把病名注入教学内容 ⇒ ④ 必须命中(否则「零命中」只是没扫);
//     ③ **缺席与损坏可分** —— 文件不存在 ⇒ false(NOT-RUN,不阻断);损坏 / 空表 ⇒ 硬失败
//        (空表若静默放行,扫描对任何文本都零命中 = **假绿**)。
//
// ⚠️ 本文件**零写盘**、零随机、零时间依赖:损坏 / 空表两条判据走 `ParseNames(json)` 的内存入口,
//   不落临时文件(FsWriteBoundaryGate R1/R2 均不触;亦不违 `.claude/rules/test-standards.md`)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    /// <summary>病名闭集来源:真表读取、判别力、缺席 / 损坏的区分。</summary>
    [TestFixture]
    public sealed class tutorial_disease_name_source_test
    {
        // ════════════════════════════════════════════════════════════════
        //  夹具
        // ════════════════════════════════════════════════════════════════

        /// <summary>本文件路径 ⇒ 仓根(向上 5 级:<c>unity/Assets/Tests/EditMode/Tutorial/</c>)。</summary>
        private static string RepoRoot([CallerFilePath] string thisFile = "")
        {
            var dir = new DirectoryInfo(Path.GetDirectoryName(thisFile));
            for (int i = 0; i < 5 && dir != null; i++) dir = dir.Parent;
            Assert.IsNotNull(dir, "仓根解析失败(向上 5 级)");
            Assert.IsTrue(
                File.Exists(Path.Combine(dir.FullName, "assets", "data", TutorialContentSchema.SourceFileName)),
                "仓根须含 assets/data/" + TutorialContentSchema.SourceFileName + " → " + dir.FullName);
            return dir.FullName;
        }

        /// <summary>真实词表里**应当出现**的全部显示名(GDD disease-simulation.md §R3 的 8 病 12 名)。</summary>
        private static readonly string[] ExpectedDisplayNames =
        {
            "风湿热", "心衰", "风湿性心脏病", "麻疹", "伤寒", "肠伤寒",
            "细菌性痢疾", "痢疾", "间日疟", "疟疾", "破伤风", "神经衰弱",
        };

        /// <summary>作业种子文本(只读)。</summary>
        private static string Seed()
            => File.ReadAllText(Path.Combine(RepoRoot(), TutorialContentSchema.DataDirName,
                                             TutorialContentSchema.SourceFileName));

        // ════════════════════════════════════════════════════════════════
        //  ① 真表读取(正向半边)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_realTable_isReady_andPinsTwelveDisplayNames()
        {
            var source = new TutorialDiseaseNameSource(RepoRoot());

            // Arrange done; Act
            bool ready = source.TryGetDiseaseNames(out IReadOnlyCollection<string> names);

            // Assert
            Assert.IsTrue(ready, "真表须就绪(否则第 ④ 项仍是 NOT-RUN → AC-48-13 无从执行)");
            Assert.IsNotNull(names);
            Assert.AreEqual(ExpectedDisplayNames.Length, names.Count,
                "显示名条数与 GDD §R3 不符 —— 实得:" + string.Join(" / ", names));
            foreach (string expected in ExpectedDisplayNames)
                CollectionAssert.Contains((System.Collections.ICollection)names, expected,
                    "真表缺显示名:" + expected);
        }

        [Test]
        public void test_realTable_dedupesButKeepsEveryDistinctName()
        {
            IReadOnlyCollection<string> names = TutorialDiseaseNameSource.ParseNames(
                "{\"diseases\":[" +
                "{\"diseaseId\":\"A\",\"displayNames\":[\"伤寒\",\"肠伤寒\"]}," +
                "{\"diseaseId\":\"B\",\"displayNames\":[\"伤寒\"]}]}");

            Assert.AreEqual(2, names.Count, "重复显示名须去重(实得:" + string.Join(" / ", names) + ")");
            CollectionAssert.AreEqual(new[] { "伤寒", "肠伤寒" },
                (System.Collections.ICollection)names, "去重须保序");
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 缺席 vs 损坏(两种形态必须可分)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_missingFile_returnsFalse_andDoesNotThrow()
        {
            // 刻意指向一个不可能存在的目录 ⇒ 不触盘写、不依赖外部状态
            string absentRoot = Path.Combine(Path.GetTempPath(), "__48_no_such_repo_root__");
            Assert.IsFalse(Directory.Exists(Path.Combine(absentRoot, TutorialContentSchema.DataDirName)),
                "夹具前提:" + absentRoot + " 下不应有 assets/data");

            var source = new TutorialDiseaseNameSource(absentRoot);
            bool ready = source.TryGetDiseaseNames(out IReadOnlyCollection<string> names);

            Assert.IsFalse(ready, "缺表 ⇒ 返回 false(NOT-RUN),不得抛");
            Assert.IsNull(names);
        }

        [Test]
        public void test_corruptJson_throwsRatherThanDegradesToNotRun()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialDiseaseNameSource.ParseNames("{ this is not json "));
            StringAssert.StartsWith("[④]", ex.Errors[0], "错误须带第 ④ 项标签");
            StringAssert.Contains("不合法", ex.Errors[0]);
        }

        [Test]
        public void test_emptyTable_throwsRatherThanScanningNothing()
        {
            // 空表 = 扫描对任何文本恒零命中 = 假绿。必须硬失败,不得静默 NOT-RUN / 静默 Clean。
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialDiseaseNameSource.ParseNames("{\"diseases\":[]}"));
            StringAssert.Contains("为空", ex.Errors[0], "错误须点名「空表」而不是笼统的格式错");
        }

        [Test]
        public void test_missingDiseasesArray_throws()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialDiseaseNameSource.ParseNames("{\"schemaVersion\":1}"));
            StringAssert.Contains("diseases", ex.Errors[0]);
        }

        [Test]
        public void test_displayNameNotString_throws()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialDiseaseNameSource.ParseNames("{\"diseases\":[{\"displayNames\":[1]}]}"));
            StringAssert.Contains("displayNames[0]", ex.Errors[0], "错误须定位到具体条目");
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 接上真表后 ④ 真跑 · 且真有判别力
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_seedWithRealTable_check4IsExecutedAndClean()
        {
            var bound = TutorialContentBake.BindForTest(
                Seed(), new TutorialDiseaseNameSource(RepoRoot()));

            Assert.AreEqual(TutorialDiseaseScanOutcome.Clean, bound.DiseaseScan.Outcome,
                "种子教学内容不得出现病名;实得 " + bound.DiseaseScan.HitCount + " 处命中");
            Assert.AreEqual(0, bound.DiseaseScan.HitCount);
            Assert.IsTrue(TutorialContentBake.AllSixChecksExecuted(bound),
                "六项校验须**全部真跑过**(④ 仍 NOT-RUN ⇒ 不得声称六项全过)");
        }

        [Test]
        public void test_seedWithRealTable_injectedDiseaseName_hits()
        {
            // 判别力判据:真表若只是「返回一个词表但扫描没接上」,本测会绿着骗人 ⇒ 必须命中。
            // 命中即**硬失败**(AC 的意图),故断言面 = 抛出的错误报文,而不是「成功后看计数」。
            const string Original = "先看看这个人。气色、神态,都在脸上。";
            const string WithDisease = "先看看这个人。气色、神态,伤寒的样子,都在脸上。";
            string json = Seed();
            int idx = json.IndexOf(Original, StringComparison.Ordinal);
            Assert.GreaterOrEqual(idx, 0, "夹具前提:种子须含该句 → " + Original);
            Assert.AreEqual(idx, json.LastIndexOf(Original, StringComparison.Ordinal),
                "夹具前提:该句须唯一(防变异打错靶)");
            json = json.Substring(0, idx) + WithDisease + json.Substring(idx + Original.Length);

            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialContentBake.BindForTest(json, new TutorialDiseaseNameSource(RepoRoot())),
                "注入「伤寒」后必须**硬失败** —— 若 Bind 仍成功,说明真表未真正参与扫描");

            string joined = string.Join("\n", ex.Errors);
            StringAssert.Contains("[④]", joined, "失败须来自第 ④ 项");
            StringAssert.Contains("伤寒", joined, "命中项须点名词条「伤寒」");
            StringAssert.Contains("n_observe_1", joined, "命中项须点名条目 id");
            StringAssert.Contains("字典 12 条", joined,
                "扫描字典须是真表的 12 个显示名(空表 / 弱表都会改变这个数)");
        }

        [Test]
        public void test_bakeFromRepo_withoutExplicitSource_stillRunsCheck4()
        {
            // 「有仓根 ⇒ ④ 必跑」：默认注入而非默认空。若有人把 `BakeFromRepo` 的默认改回 null，
            // `cooked_drift_all_artifacts_test` 那条调用链就会静默退化成 NOT-RUN 而无人察觉。
            TutorialContentBake.BakeOutput baked = TutorialContentBake.BakeFromRepo(RepoRoot());

            Assert.IsTrue(TutorialContentBake.AllSixChecksExecuted(baked.Bound),
                "`BakeFromRepo(repoRoot)` 不传来源时第 ④ 项仍须执行(默认注入病名表)");
            Assert.AreEqual(TutorialDiseaseScanOutcome.Clean, baked.Bound.DiseaseScan.Outcome);
        }

        [Test]
        public void test_nullSource_stillTableNotReady_notBorrowedGreen()
        {
            // 反向半边:没有来源时 ④ 仍是 NOT-RUN。若这条转绿,说明「缺席」被悄悄当成了「通过」。
            var bound = TutorialContentBake.BindForTest(Seed(), null);

            Assert.AreEqual(TutorialDiseaseScanOutcome.TableNotReady, bound.DiseaseScan.Outcome);
            Assert.IsFalse(TutorialContentBake.AllSixChecksExecuted(bound),
                "缺表时不得声称六项全过(借绿禁令)");
        }
    }
}
