// S17-3 · story-001 —— 48 教学内容 schema 与烘焙校验的 EditMode 测试。
//
// 覆盖:AC-1(六项校验各有必失败夹具)· AC-2(种子 17 条过闸 + 确定性)· AC-3(运行期零解析器)
//       · AC-4(步 id 闭集恰 6 员)· QA 回填 5 条增量(之八 / 长度成对 / ConfigVersion /
//       Count>0 守卫 / Checkpoint 例外)。
//
// ⚠️ 本文件**只读**仓库文件(`assets/data/48_tutorial_content.json` 是本 story 的正当输入),
//   零写盘、零随机种子、零时间依赖(FsWriteBoundaryGate R1/R2 均不触)。
// ⚠️ 三条外部词表(病名 / cueId / clipKey)在生产路径**均未就绪** ⇒ 相关 AC 子项记 **NOT-RUN**;
//   本文件用**注入替身**证明接缝通电(缺表时**不得**借绿 —— 见 test_dinf_* 与 NOT-RUN 断言)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using DaYiJingCheng.EditorTools.Bake;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    /// <summary>48 教学内容 schema / 六项校验 / 确定性 / 程序集引用面。</summary>
    [TestFixture]
    public sealed class tutorial_content_validation_test
    {
        // ════════════════════════════════════════════════════════════════
        //  夹具(注入替身;生产路径当前均无实现者)
        // ════════════════════════════════════════════════════════════════

        /// <summary>病名闭集替身。</summary>
        private sealed class FakeDiseaseNames : ITutorialDiseaseNameSource
        {
            private readonly List<string> _names;
            private readonly bool _ready;
            public FakeDiseaseNames(bool ready, params string[] names)
            {
                _ready = ready; _names = new List<string>(names ?? Array.Empty<string>());
            }
            public bool TryGetDiseaseNames(out IReadOnlyCollection<string> names)
            {
                names = _ready ? (IReadOnlyCollection<string>)_names : null;
                return _ready;
            }
        }

        /// <summary>cueId / clipKey 表替身。</summary>
        private sealed class FakeMediaKeys : ITutorialMediaKeySource
        {
            private readonly string[] _cues;
            private readonly string[] _clips;
            public FakeMediaKeys(string[] cues, string[] clips) { _cues = cues; _clips = clips; }
            public bool TryGetCueIds(out IReadOnlyCollection<string> cueIds)
            {
                cueIds = _cues;
                return _cues != null;
            }
            public bool TryGetClipKeys(out IReadOnlyCollection<string> clipKeys)
            {
                clipKeys = _clips;
                return _clips != null;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  仓根 / 种子 读取
        // ════════════════════════════════════════════════════════════════

        private static string RepoRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "assets", "data",
                                                 TutorialContentSchema.SourceFileName)))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            Assert.Fail("找不到仓根(须含 assets/data/" + TutorialContentSchema.SourceFileName + ")");
            return null;
        }

        private static string SeedPath()
            => Path.Combine(RepoRoot(), TutorialContentSchema.DataDirName, TutorialContentSchema.SourceFileName);

        private static string Seed() => File.ReadAllText(SeedPath());

        /// <summary>单点替换夹具(唯一命中;不命中即失败 —— 防「变异打错靶」)。</summary>
        private static string Mutate(string json, string oldValue, string newValue)
        {
            int idx = json.IndexOf(oldValue, StringComparison.Ordinal);
            Assert.GreaterOrEqual(idx, 0, "夹具前提:种子须含被替换片段 → " + oldValue);
            Assert.AreEqual(idx, json.LastIndexOf(oldValue, StringComparison.Ordinal),
                "夹具前提:被替换片段须唯一 → " + oldValue);
            return json.Substring(0, idx) + newValue + json.Substring(idx + oldValue.Length);
        }

        /// <summary>断言烘焙以 <paramref name="checkTag"/> 开头的错误硬失败(且带条目定位)。</summary>
        private static BakeValidationException AssertThrowsTagged(string json, string checkTag,
            ITutorialDiseaseNameSource disease = null, ITutorialMediaKeySource media = null)
        {
            var ex = Assert.Throws<BakeValidationException>(() =>
                TutorialContentBinder.Bind(json, disease, media));
            bool found = false;
            for (int i = 0; i < ex.Errors.Count; i++)
                if (ex.Errors[i].StartsWith(checkTag, StringComparison.Ordinal)) { found = true; break; }
            Assert.IsTrue(found, $"须报 {checkTag} 类错误;实得:\n" + string.Join("\n", ex.Errors));
            return ex;
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-2 · 种子 17 条 + 依赖表未就绪的如实登记
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_seed_content_bakes_andHasSeventeenItems()
        {
            var bound = TutorialContentBinder.Bind(Seed());

            Assert.AreEqual(6, bound.Steps.Count, "六步(观察并入 ① 锚,闭集恰 6 员)");
            Assert.AreEqual(4, bound.Narration.Count, "P0 种子口述 4 条");
            Assert.AreEqual(6, bound.Demonstrations.Count, "P0 种子示范 6 条");
            Assert.AreEqual(1, bound.Papers.Count, "P0 种子纸 1 条");
            Assert.AreEqual(17, bound.ContentItemCount, "GDD 内容债口径:6 + 4 + 6 + 1 = 17");
            Assert.AreEqual(1u, bound.SchemaVersion, "schemaVersion = 1");
        }

        [Test]
        public void test_seed_content_diseaseTableMissing_isNotRun()
        {
            // ⚠️ AC 第 4 条(种子过全部 6 项)的**第 ④ 项**记 NOT-RUN —— 病名闭集词表未就绪。
            var bound = TutorialContentBinder.Bind(Seed(), diseaseNames: null);

            Assert.AreEqual(TutorialDiseaseScanOutcome.TableNotReady, bound.DiseaseScan.Outcome,
                "生产路径缺病名闭集词表 ⇒ 第 ④ 项 NOT-RUN(不得用弱化词表凑绿)");
            Assert.AreEqual(0, bound.DiseaseScan.HitCount, "未执行 ⇒ 零命中,但「零命中」≠「通过」");
            Assert.IsFalse(TutorialContentBake.AllSixChecksExecuted(bound),
                "⚠️ 烘焙成功 ≠ 六项全跑过 —— 本断言把两者分开");
        }

        [Test]
        public void test_seed_content_mediaKeyTablesMissing_areNotReady()
        {
            var bound = TutorialContentBinder.Bind(Seed(), diseaseNames: null, mediaKeys: null);

            Assert.IsFalse(bound.CueIdTableReady, "44 cueId 表未就绪(teach_* 四键尚未登记)⇒ ② 外部半边 NOT-RUN");
            Assert.IsFalse(bound.ClipKeyTableReady, "13 clipKey 表未就绪 ⇒ ② 外部半边 NOT-RUN");
        }

        [Test]
        public void test_seed_content_withAllTablesInjected_passesAllSix()
        {
            // 注入三张「就绪」的替身(内容取种子里的实际键)⇒ 六项真跑过且全绿。
            // ⚠️ 这是**接缝通电证明**,不是「生产路径六项全过」的宣称 —— 生产路径三表均缺席。
            var bound = TutorialContentBinder.Bind(Seed(),
                new FakeDiseaseNames(true, "疟疾", "伤寒"),   // 词表在但种子文本零命中
                new FakeMediaKeys(
                    new[] { "Narration_TeachObserve_01", "Narration_TeachJudge_01", "Narration_TeachClose_01", "Narration_TeachArchive_01" },
                    new[] { "mentor_pulse_exam", "mentor_open_notebook", "mentor_bandage_demo",
                            "mentor_close_case", "mentor_open_ledger", "mentor_place_structure" }));

            Assert.AreEqual(TutorialDiseaseScanOutcome.Clean, bound.DiseaseScan.Outcome);
            Assert.AreEqual(TutorialDiseaseScanOutcome.Clean, bound.InstructionScan.Outcome);
            Assert.IsTrue(bound.CueIdTableReady);
            Assert.IsTrue(bound.ClipKeyTableReady);
        }

        // ════════════════════════════════════════════════════════════════
        //  C-1(2026-10-21)· ② 外部半边的**生产来源**通电证明
        //
        //  ⚠️ 上面 test_seed_content_withAllTablesInjected_passesAllSix 打的是 FakeMediaKeys
        //     ⇒ 它只证明**接缝**通,不证明 `TutorialMediaKeySource` 这个生产实现能读出真表。
        //     生产来源若读错字段名(如 `cue` 写成 `cueId`),上面那条**照样绿** ——
        //     这正是「扫不到被测装配」的形态。本节补的是生产来源自己的断言。
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_mediaKeySource_productionTables_areReady()
        {
            // 走**默认注入**路径:BakeFromRepo 未显式传 mediaKeys ⇒ 内部 new TutorialMediaKeySource(repoRoot)。
            var output = TutorialContentBake.BakeFromRepo(RepoRoot());
            var bound = output.Bound;

            Assert.IsTrue(bound.CueIdTableReady,
                "44 cue 表就位 ⇒ ② 外部半边真跑过(teach_* 四键已登记在 assets/data/audio_events.json)");
            Assert.IsTrue(bound.ClipKeyTableReady,
                "13 clipKey 表就位 ⇒ ② 外部半边真跑过(assets/data/48_demonstration_clips.json)");

            // 就绪 ≠ 零悬垂。烘焙未抛异常已蕴含零悬垂,但这里显式断言计数,
            // 使「表就绪」与「引用全命中」两件事各自可读。
            Assert.AreEqual(4, bound.Narration.Count, "前置:种子口述 4 条均须被逐条查过");
            Assert.AreEqual(6, bound.Demonstrations.Count, "前置:种子示范 6 条均须被逐条查过");
        }

        [Test]
        public void test_mediaKeySource_productionTables_containSeedKeys()
        {
            // 生产来源直接读:闭集须**真含**种子引用的键(而非仅仅「非空」)。
            var source = new TutorialMediaKeySource(RepoRoot());

            Assert.IsTrue(source.TryGetCueIds(out IReadOnlyCollection<string> cues), "44 表须可读");
            Assert.IsTrue(source.TryGetClipKeys(out IReadOnlyCollection<string> keys), "13 表须可读");

            string[] seedCues = { "Narration_TeachObserve_01", "Narration_TeachJudge_01", "Narration_TeachClose_01", "Narration_TeachArchive_01" };
            string[] seedKeys =
            {
                "mentor_pulse_exam", "mentor_open_notebook", "mentor_bandage_demo",
                "mentor_close_case", "mentor_open_ledger", "mentor_place_structure",
            };

            var cueSet = new HashSet<string>(cues);
            var keySet = new HashSet<string>(keys);
            for (int i = 0; i < seedCues.Length; i++)
                Assert.IsTrue(cueSet.Contains(seedCues[i]), "44 表缺种子 cueId:" + seedCues[i]);
            for (int i = 0; i < seedKeys.Length; i++)
                Assert.IsTrue(keySet.Contains(seedKeys[i]), "13 表缺种子 clipKey:" + seedKeys[i]);
        }

        [Test]
        public void test_mediaKeySource_emptyTable_throws_notSilentlyNotRun()
        {
            // 判别力对照:空表若被静默当成「未就绪」,则一张写坏的表会让该半边永远 NOT-RUN
            // 而无人察觉 —— 那是假绿。生产解析器须硬失败。
            Assert.Throws<BakeValidationException>(() =>
                TutorialMediaKeySource.ParseCueIds("{\"rows\": []}"), "零 cue 表须抛,不得静默 NOT-RUN");
            Assert.Throws<BakeValidationException>(() =>
                TutorialMediaKeySource.ParseClipKeys("{\"clips\": []}"), "零 clipKey 表须抛,不得静默 NOT-RUN");
        }

        [Test]
        public void test_mediaKeySource_absentFile_isNotReady_notThrow()
        {
            // 缺席 ≠ 损坏:指向一个不含两张表的仓根 ⇒ 半边返回 false(记 NOT-RUN),不抛。
            var source = new TutorialMediaKeySource(Path.Combine(RepoRoot(), "assets", "data", "__absent__"));
            Assert.IsFalse(source.TryGetCueIds(out _), "文件缺席 ⇒ 该半边 NOT-RUN(文档化语义)");
            Assert.IsFalse(source.TryGetClipKeys(out _), "文件缺席 ⇒ 该半边 NOT-RUN(文档化语义)");
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-2 / AC-3 · 确定性 + ConfigVersion(内容哈希)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_bake_twice_isByteIdentical()
        {
            var a = TutorialContentBake.BakeFromJson(Seed());
            var b = TutorialContentBake.BakeFromJson(Seed());

            CollectionAssert.AreEqual(a.Cooked, b.Cooked, "同源两次烘焙须逐位一致(ADR-014 §二)");
            Assert.AreEqual(a.ConfigVersion, b.ConfigVersion, "同源两次 ConfigVersion 相等");
        }

        [Test]
        public void test_bake_fromRepo_matchesFromJson()
        {
            string json = Seed();
            var a = TutorialContentBake.BakeFromRepo(RepoRoot());
            var b = TutorialContentBake.BakeFromJson(json);

            CollectionAssert.AreEqual(a.Cooked, b.Cooked,
                "仓根装载器与串入口须同一编码路径(否则菜单与测试判定两台机器)");
        }

        [Test]
        public void test_configVersion_changingOneValue_changesHash()
        {
            string json = Seed();
            uint before = TutorialContentBake.DeriveConfigVersion(json);

            // 改**一个字符**(171 字夹具之外的最小改动,不动结构)
            string mutated = Mutate(json, "望诊四要", "望诊四要。");
            uint after = TutorialContentBake.DeriveConfigVersion(mutated);

            Assert.AreNotEqual(before, after, "改一个数值/字符 ⇒ ConfigVersion 必变(ADR-014 §五)");
            Assert.AreEqual(before, TutorialContentBake.DeriveConfigVersion(json), "原文本哈希不受影响");
        }

        [Test]
        public void test_cooked_header_carriesSchemaAndConfigVersion()
        {
            var output = TutorialContentBake.BakeFromJson(Seed());

            Assert.GreaterOrEqual(output.Cooked.Length, 20, "须含 20 字节头");
            Assert.AreEqual((byte)'D', output.Cooked[0]);
            Assert.AreEqual((byte)'Y', output.Cooked[1]);
            Assert.AreEqual((byte)'J', output.Cooked[2]);
            Assert.AreEqual((byte)'C', output.Cooked[3]);
            Assert.AreEqual(1u, ReadU32(output.Cooked, 8), "schemaVersion 只住头槽(offset 8)");
            Assert.AreEqual(output.ConfigVersion, ReadU32(output.Cooked, 12), "configVersion @ offset 12");
            Assert.AreEqual((uint)(output.Cooked.Length - 20), ReadU32(output.Cooked, 16), "payloadLength @ 16");
        }

        [Test]
        public void test_stepClosure_isExactlySixInGddOrder()
        {
            // 写方按本闭集序编码(而非源文件序)⇒ 闭集本身必须与 GDD 规则三的步序一致。
            Assert.AreEqual(6, TutorialContentSchema.StepCount);
            Assert.AreEqual(6, TutorialContentSchema.StepIds.Count);
            CollectionAssert.AreEqual(
                new[] { "observe", "judge", "treat", "close", "archive", "manage" },
                TutorialContentSchema.StepIds,
                "闭集序 = GDD 规则三的六步序(写方行序依赖它;改序 = 改字节流 ⇒ 须有意识改)");

            // 种子仓的步序与闭集序一致(不同则说明作者已调整段落顺序 —— 字节流仍按闭集序)
            var bound = TutorialContentBinder.Bind(Seed());
            Assert.AreEqual(6, bound.Steps.Count);
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-1 ① · JSON 合法
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_check1_truncatedJson_throws()
        {
            string json = Seed();
            string broken = json.Substring(0, json.Length / 2);
            var ex = Assert.Throws<BakeValidationException>(() => TutorialContentBinder.Bind(broken));
            StringAssert.Contains("[①]", ex.Errors[0], "词法失败须以 ① 打头(定位到检查项)");
        }

        [Test]
        public void test_check1_unknownKey_throws()
        {
            string json = Mutate(Seed(), "\"schemaVersion\": 1,", "\"schemaVersion\": 1,\n  \"extra\": 0,");
            var ex = AssertThrowsTagged(json, "[①]");
            bool named = false;
            for (int i = 0; i < ex.Errors.Count; i++)
                if (ex.Errors[i].IndexOf("extra", StringComparison.Ordinal) >= 0) { named = true; break; }
            Assert.IsTrue(named, "未知键错误须点名该键(ADR-014 §三)");
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-1 ② · 引用完整性(悬垂 / 孤儿 / 外部键)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_check2_danglingNarrationReference_throws()
        {
            string json = Mutate(Seed(), "\"narration\": [\"n_close_1\"]", "\"narration\": [\"n_close_404\"]");
            var ex = AssertThrowsTagged(json, "[②]");
            StringAssert.Contains("n_close_404", string.Join("\n", ex.Errors), "须点名悬垂 id");
        }

        [Test]
        public void test_check2_orphanNarration_throws()
        {
            string json = Mutate(Seed(), "\"narration\": [\"n_judge_1\"],", "\"narration\": [],");
            var ex = AssertThrowsTagged(json, "[②]");
            StringAssert.Contains("n_judge_1", string.Join("\n", ex.Errors), "须点名孤儿条目");
        }

        [Test]
        public void test_check2_danglingCueId_throwsWhenTableReady()
        {
            string json = Mutate(Seed(), "\"cueId\": \"Narration_TeachClose_01\"", "\"cueId\": \"teach_404\"");
            var media = new FakeMediaKeys(
                new[] { "Narration_TeachObserve_01", "Narration_TeachJudge_01", "Narration_TeachClose_01", "Narration_TeachArchive_01" },
                null);
            var ex = AssertThrowsTagged(json, "[②]", null, media);
            StringAssert.Contains("teach_404", string.Join("\n", ex.Errors), "须点名悬垂 cueId");
        }

        [Test]
        public void test_check2_danglingCueId_isNotCheckedWhenTableMissing()
        {
            // ⚠️ 反向夹具:表缺席时**不得**静默判绿 —— 但也不得凭空报错。
            //    判据 = 外部半边 NOT-RUN(cueIdTableReady == false),由调用方登记。
            // 🔁 2026-10-21(评审 T4):原版喂的是**未变异**的种子 ⇒ 里面根本没有悬空 cueId,
            //    「表缺席时不检查悬空」这条语义**无事可证**(空转夹具)。现改成注入真悬空 id,
            //    这样若实现把「表缺席」误写成「跳过整条 ②」以外的行为(例如漏掉 NOT-RUN 标记,
            //    或反过来在缺表时凭空报悬空),本测都会红。
            string json = Mutate(Seed(), "\"cueId\": \"Narration_TeachClose_01\"", "\"cueId\": \"teach_404\"");
            var bound = TutorialContentBinder.Bind(json, null, new FakeMediaKeys(null, null));

            Assert.IsFalse(bound.CueIdTableReady, "表缺席 ⇒ 该半边 NOT-RUN(不是「通过」)");
            Assert.AreEqual(4, bound.Narration.Count,
                "前置:悬空 cueId **不阻断**绑定(外部半边的缺席不阻断其余五项)");
            bool danglingSurvived = false;
            for (int i = 0; i < bound.Narration.Count; i++)
                if (bound.Narration[i].CueId == "teach_404") { danglingSurvived = true; break; }
            Assert.IsTrue(danglingSurvived, "前置:悬空 id 须真的进了种子(否则本测仍是空转)");
        }

        [Test]
        public void test_check2_danglingClipKey_throwsWhenTableReady()
        {
            string json = Mutate(Seed(), "\"clipKey\": \"mentor_pulse_exam\"", "\"clipKey\": \"mentor_404\"");
            var media = new FakeMediaKeys(null, new[] { "mentor_pulse_exam" });
            var ex = AssertThrowsTagged(json, "[②]", null, media);
            StringAssert.Contains("mentor_404", string.Join("\n", ex.Errors), "须点名悬垂 clipKey");
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-1 / AC-2 ③ · 长度边界**成对**
        // ════════════════════════════════════════════════════════════════

        private const string SeedObserveText = "\"text\": \"先看看这个人。气色、神态,都在脸上。\"";

        [Test]
        public void test_check3_text170_passes()
        {
            string json = Mutate(Seed(), SeedObserveText,
                "\"text\": \"" + new string('字', 170) + "\"");
            var bound = TutorialContentBinder.Bind(json);
            Assert.AreEqual(170, bound.Narration[0].Text.Length, "前提:恰 170 字");
        }

        [Test]
        public void test_check3_text171_throws()
        {
            string json = Mutate(Seed(), SeedObserveText,
                "\"text\": \"" + new string('字', 171) + "\"");
            var ex = AssertThrowsTagged(json, "[③]");
            StringAssert.Contains("171", string.Join("\n", ex.Errors), "错误须含实得长度");
        }

        [Test]
        public void test_check3_titleTwoLines_passes()
        {
            string json = Mutate(Seed(), "\"title\": \"望诊四要\"", "\"title\": \"望\\n诊\"");
            var bound = TutorialContentBinder.Bind(json);
            Assert.AreEqual(2, TutorialContentSchema.LineCount(bound.Papers[0].Title), "前提:恰 2 行");
        }

        [Test]
        public void test_check3_titleThreeLines_throws()
        {
            string json = Mutate(Seed(), "\"title\": \"望诊四要\"", "\"title\": \"望\\n诊\\n四\"");
            AssertThrowsTagged(json, "[③]");
        }

        [Test]
        public void test_check3_body171_throws()
        {
            string json = Mutate(Seed(), "\"body\": \"望神、望色、望形、望态。\"",
                "\"body\": \"" + new string('字', 171) + "\"");
            AssertThrowsTagged(json, "[③]");
        }

        [Test]
        public void test_check3_body170_passes()
        {
            // 🔁 2026-10-21(评审 T4):原只有 171 的负例(与 text 对称性缺口)——
            //   上限判据必须**成对**(170 过 / 171 拒),否则 off-by-one 往「过宽」方向漂时无夹具可红。
            string json = Mutate(Seed(), "\"body\": \"望神、望色、望形、望态。\"",
                "\"body\": \"" + new string('字', 170) + "\"");
            var bound = TutorialContentBinder.Bind(json);
            Assert.AreEqual(170, bound.Papers[0].Body.Length, "前提:恰 170 字");
        }

        [Test]
        public void test_check3_nonSelfContainedText_throws()
        {
            string json = Mutate(Seed(), SeedObserveText,
                "\"text\": \"上文说过了。\"");
            var ex = AssertThrowsTagged(json, "[③]");
            StringAssert.Contains("不自含", string.Join("\n", ex.Errors), "自含判据与长度同属 ③");
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-1 ④ / ⑤ · 词表扫描(结构化计数)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_check4_diseaseNameHit_throws()
        {
            // 词表注入「望色」= 种子纸正文里真实出现 → 命中 1 处
            string json = Seed();
            var source = new FakeDiseaseNames(true, "望色");
            var ex = AssertThrowsTagged(json, "[④]", source);
            StringAssert.Contains("1 处命中", string.Join("\n", ex.Errors),
                "失败须由**计数**派生并报出计数");
        }

        [Test]
        public void test_check4_hitCountIsStructured_notPrefixJudged()
        {
            // 同一口径:命中数 = 命中词条数;增词条即增计数(不是「有没有标记字符」)
            string json = Seed();
            var one = TutorialContentBinder.ScanForTokens(
                "望神、望色、望形、望态。", new[] { "望色" }, "p", "body");
            var two = TutorialContentBinder.ScanForTokens(
                "望神、望色、望形、望态。", new[] { "望色", "望形" }, "p", "body");

            Assert.AreEqual(1, one.Count);
            Assert.AreEqual(2, two.Count, "命中数随命中词条单调增长 —— 真值不经过字符串判真假");
            Assert.AreEqual("望色", two[0].Token, "Hit 须报出**撞了哪个词**");
        }

        [Test]
        public void test_check5_explicitInstruction_pressE_throws()
        {
            string json = Mutate(Seed(), SeedObserveText, "\"text\": \"按 E 继续。\"");
            var ex = AssertThrowsTagged(json, "[⑤]");
            StringAssert.Contains("按", string.Join("\n", ex.Errors), "须点名撞的词条");
        }

        [Test]
        public void test_check5_explicitInstruction_englishEnglish_throws()
        {
            string json = Mutate(Seed(), SeedObserveText, "\"text\": \"Press here to continue.\"");
            AssertThrowsTagged(json, "[⑤]");
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-1 ⑥ / AC-4 · predicateKind 白名单 + Checkpoint 例外 + id 闭集
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_check6_unknownPredicateKind_throws()
        {
            string json = Mutate(Seed(), "\"predicateKind\": \"CaseOpened\"", "\"predicateKind\": \"Foo\"");
            var ex = AssertThrowsTagged(json, "[⑥]");
            StringAssert.Contains("Foo", string.Join("\n", ex.Errors), "须点名非法 Kind");
        }

        [Test]
        public void test_check6_checkpointOnArchive_passes()
        {
            var bound = TutorialContentBinder.Bind(Seed());
            TutorialStep archive = FindStep(bound, "archive");
            Assert.AreEqual(TutorialContentSchema.CheckpointPredicateLiteral, archive.PredicateKind,
                "种子:archive 步挂字面 Checkpoint(ADR-010 §六 例外)");
        }

        [Test]
        public void test_check6_checkpointOnNonArchiveStep_throws()
        {
            string json = Mutate(Seed(), "\"predicateKind\": \"CaseClosed\"", "\"predicateKind\": \"Checkpoint\"");
            var ex = AssertThrowsTagged(json, "[⑥]");
            StringAssert.Contains("archive", string.Join("\n", ex.Errors),
                "错误须指明例外只允许挂在 archive 步");
        }

        [Test]
        public void test_check6_observeAndJudgeShareAnchor_isLegal()
        {
            // AC-4 Edge:observe 与 judge 共锚(同 CaseOpened)合法
            string json = Mutate(Seed(), "\"predicateKind\": \"JudgmentRecorded\"",
                "\"predicateKind\": \"CaseOpened\"");
            var bound = TutorialContentBinder.Bind(json);
            Assert.AreEqual("CaseOpened", FindStep(bound, "observe").PredicateKind);
            Assert.AreEqual("CaseOpened", FindStep(bound, "judge").PredicateKind);
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-1 · 字段级**负例**（🔁 2026-10-21 评审 T3）
        //  T3 原判：`order` / `actor` / `aspectRatio` / `illustrationKey` **零负例**
        //  —— 四个字段的判据只被正向种子验过 ⇒ 判据写反 / 写丢都不会红。
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_check1_orderFloatLiteral_throws()
        {
            // `order` 只认 JsonNodeKind.Integer(ADR-014 §三:禁数值经浮点中转)
            string json = Mutate(Seed(),
                "\"cueId\": \"Narration_TeachObserve_01\",\n      \"order\": 1,",
                "\"cueId\": \"Narration_TeachObserve_01\",\n      \"order\": 1.5,");
            var ex = AssertThrowsTagged(json, "[①]");
            StringAssert.Contains("order", string.Join("\n", ex.Errors), "错误须点名 order");
        }

        [Test]
        public void test_check1_actorOutsideClosedSet_throws()
        {
            string json = Mutate(Seed(),
                "\"id\": \"d_pulse_1\",\n      \"step\": \"observe\",\n      \"actor\": \"mentor\",",
                "\"id\": \"d_pulse_1\",\n      \"step\": \"observe\",\n      \"actor\": \"pupil\",");
            var ex = AssertThrowsTagged(json, "[①]");
            string joined = string.Join("\n", ex.Errors);
            StringAssert.Contains("pupil", joined, "错误须点名违规值");
            StringAssert.Contains("mentor", joined, "错误须列出闭集成员");
        }

        [Test]
        public void test_check1_aspectRatioMalformed_throws()
        {
            string json = Mutate(Seed(), "\"aspectRatio\": \"4:3\"", "\"aspectRatio\": \"4x3\"");
            var ex = AssertThrowsTagged(json, "[①]");
            StringAssert.Contains("aspectRatio", string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_check1_aspectRatioZeroSegment_throws()
        {
            // 「两段正整数」的另一半:0 不是正数 ⇒ 拒(只测格式会漏掉这条)
            string json = Mutate(Seed(), "\"aspectRatio\": \"4:3\"", "\"aspectRatio\": \"4:0\"");
            AssertThrowsTagged(json, "[①]");
        }

        [Test]
        public void test_check1_illustrationKeyMissing_throws()
        {
            // 键缺失 = 拼写错误的典型后果;校验须硬失败而非静默丢字段
            string json = Mutate(Seed(),
                "      \"illustrationKey\": \"paper_wangzhen_01_v1\",\n", "");
            var ex = AssertThrowsTagged(json, "[①]");
            StringAssert.Contains("illustrationKey", string.Join("\n", ex.Errors));
        }

        [Test]
        public void test_ac4_sevenSteps_throws()
        {
            const string anchor = "      \"papers\": []\n    }\n  ],\n  \"narration\": [";
            const string withExtra =
                "      \"papers\": []\n    },\n" +
                "    {\n      \"id\": \"extra\",\n      \"title\": \"x\",\n" +
                "      \"predicateKind\": \"Craft\",\n      \"note\": \"n\",\n" +
                "      \"narration\": [],\n      \"demonstrations\": [],\n      \"papers\": []\n" +
                "    }\n  ],\n  \"narration\": [";
            string json = Mutate(Seed(), anchor, withExtra);

            var ex = AssertThrowsTagged(json, "[⑥]");
            StringAssert.Contains("7", string.Join("\n", ex.Errors), "错误须报实得条数");
        }

        [Test]
        public void test_ac4_missingStep_throws()
        {
            string json = Mutate(Seed(), "\"id\": \"manage\"", "\"id\": \"manag\"");
            var ex = AssertThrowsTagged(json, "[⑥]");
            StringAssert.Contains("manage", string.Join("\n", ex.Errors), "须点名缺失的闭集成员");
        }

        [Test]
        public void test_ac4_duplicateStepId_throws()
        {
            string json = Mutate(Seed(), "\"id\": \"manage\"", "\"id\": \"observe\"");
            var ex = AssertThrowsTagged(json, "[⑥]");
            StringAssert.Contains("重复", string.Join("\n", ex.Errors), "重复 id 须单独报出");
        }

        // ════════════════════════════════════════════════════════════════
        //  🔁 QA 回填 #1 · Cross-Cutting 之八(culture 钉住)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_dinf_ordinalScan_isCultureIndependent()
        {
            Thread thread = Thread.CurrentThread;
            CultureInfo savedCulture = thread.CurrentCulture;
            CultureInfo savedUi = thread.CurrentUICulture;
            try
            {
                thread.CurrentCulture = new CultureInfo("tr-TR");
                thread.CurrentUICulture = new CultureInfo("tr-TR");

                // 我们的判据:显式 OrdinalIgnoreCase ⇒ 'I' / 'i' 恒等同
                var hits = TutorialContentBinder.ScanForTokens(
                    "CLICK HERE", new[] { "click" }, "id", "f");
                Assert.AreEqual(1, hits.Count, "显式 OrdinalIgnoreCase ⇒ tr-TR 下仍命中");

                // 对照:culture-sensitive 路径在 tr-TR 下把 'I' 折成 'ı' ⇒ 同一输入不命中。
                // 本断言钉住「两种判据确实会分叉」——若哪天有人换回默认重载,上面那条就变成运气。
                string naiveLower = "CLICK HERE".ToLower();
                Assert.IsFalse(naiveLower.Contains("click"),
                    "对照夹具:tr-TR 下 culture-sensitive 的 ToLower 不命中(故我们的命中**只**能来自 Ordinal)");
            }
            finally
            {
                thread.CurrentCulture = savedCulture;
                thread.CurrentUICulture = savedUi;
            }
        }

        [Test]
        public void test_dinf_scanResult_isIdenticalAcrossCultures()
        {
            Thread thread = Thread.CurrentThread;
            CultureInfo savedCulture = thread.CurrentCulture;
            CultureInfo savedUi = thread.CurrentUICulture;
            string[] cultures = { "", "zh-CN", "en-US", "tr-TR", "ja-JP" };
            var observed = new List<string>();
            try
            {
                for (int i = 0; i < cultures.Length; i++)
                {
                    thread.CurrentCulture = new CultureInfo(cultures[i]);
                    thread.CurrentUICulture = new CultureInfo(cultures[i]);

                    var hits = TutorialContentBinder.ScanForTokens(
                        "望神、望色 CLICK", new[] { "望色", "click", "foo" }, "p", "body");
                    var parts = new List<string>();
                    for (int h = 0; h < hits.Count; h++) parts.Add(hits[h].ToString());
                    observed.Add(hits.Count + "|" + string.Join(";", parts));
                }
            }
            finally
            {
                thread.CurrentCulture = savedCulture;
                thread.CurrentUICulture = savedUi;
            }

            for (int i = 1; i < observed.Count; i++)
                Assert.AreEqual(observed[0], observed[i],
                    $"扫描结果须与 culture 无关(差异出现在 {cultures[i]})");
        }

        // ════════════════════════════════════════════════════════════════
        //  AC-3 · 运行期零 JSON 解析器(含 Count > 0 守卫)
        // ════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac3_runtimeAssemblies_haveNoJsonParser()
        {
            var forbidden = new[] { "Newtonsoft.Json", "System.Text.Json" };

            // ⚠️ 只取**运行期侧**程序集(48 的 `.cooked` 消费者所在面);
            //    编辑期工具集(Editor.Tools.Bake)被 includePlatforms:["Editor"] 隔离,不在本扫描面。
            var runtimeNames = new[]
            {
                "Sim", "Sim.Contracts", "Gameplay.Presentation", "Gameplay.Boot", "Telemetry",
            };
            var scanned = new List<Assembly>();
            for (int i = 0; i < runtimeNames.Length; i++)
            {
                try { scanned.Add(Assembly.Load(new AssemblyName(runtimeNames[i]))); }
                catch (Exception) { /* 未加载 = 不在本测试进程的扫描面,跳过 */ }
            }

            // 🔁 回填 #4:先断言「扫描面非空」,否则「不含禁用项」在空集下恒真(借绿)
            Assert.Greater(scanned.Count, 0, "前提:运行期程序集扫描集非空");

            var refs = new List<string>();
            for (int i = 0; i < scanned.Count; i++)
            {
                var names = scanned[i].GetReferencedAssemblies();
                for (int j = 0; j < names.Length; j++) refs.Add(names[j].Name);
            }
            Assert.Greater(refs.Count, 0, "前提:引用集非空(空集下『不含禁用品』恒真)");

            for (int f = 0; f < forbidden.Length; f++)
                CollectionAssert.DoesNotContain(refs, forbidden[f],
                    $"运行期程序集不得引用 {forbidden[f]}(AC 第 5 条:48 只读 .cooked)");
        }

        [Test]
        public void test_ac3_editorAssemblySeesParser_scannerIsDiscriminating()
        {
            // 判别力对照:同一「查引用集」手法在**编辑期**程序集上**能**看见 JSON 解析器
            // ⇒ 上面那条的绿不是「扫描器看不见任何东西」的结果。
            var bakeAsm = typeof(TutorialContentBake).Assembly;
            var names = bakeAsm.GetReferencedAssemblies();
            Assert.Greater(names.Length, 0, "前提:编辑期工具程序集引用集非空");

            bool seesParser = false;
            for (int i = 0; i < names.Length; i++)
                if (names[i].Name.StartsWith("Newtonsoft", StringComparison.Ordinal) ||
                    names[i].Name.StartsWith("Unity.Newtonsoft", StringComparison.Ordinal))
                { seesParser = true; break; }

            Assert.IsTrue(seesParser,
                "编辑期工具程序集应能看到 Newtonsoft(阶段 1 词法器依赖它)⇒ 扫描器有判别力");
        }

        // ════════════════════════════════════════════════════════════════
        //  小件
        // ════════════════════════════════════════════════════════════════

        private static TutorialStep FindStep(TutorialContentBinder.BindResult bound, string id)
        {
            for (int i = 0; i < bound.Steps.Count; i++)
                if (string.Equals(bound.Steps[i].Id, id, StringComparison.Ordinal)) return bound.Steps[i];
            Assert.Fail("缺步:" + id);
            return default;
        }

        private static uint ReadU32(byte[] bytes, int offset)
            => (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) |
                      (bytes[offset + 3] << 24));
    }
}
