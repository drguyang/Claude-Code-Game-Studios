// T8(2026-11-09)—— 手册纸 ↔ 技法绑定:管线五层 + 缺页判定。
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2(钩子病例的载体 = 手册缺页)
//      · ADR-043 §D5(技法 id 空间 = `disease_action_axis.json` 的 action id)
//      · ADR-014(作者态 JSON → 两阶段烘焙)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读 `assets/data/48_tutorial_content.json`(经生产 binder + 生产 cooked 往返)与
//   `assets/data/disease_action_axis.json`(经生产 `TutorialTechniqueIdSource`)。
// 为什么必须读真表:本件判的正是「**这两份真表之间**的引用是否成立」——
//   内联一份假表只能证明本夹具自己写的谓词(假绿形态 F1)。
// ⚠️ **本文件零写盘**:唯一涉及临时路径的那条(`test_productionSource_missingFile_isNotRun`)
//   用的是**不存在**的目录名,只读不写 ⇒ `FsWriteBoundaryGate` 的 R1 / R2 均不触发。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件要证明的五件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 缺省 = 「不教任何技法」,且与「教 0 号技法」在数据层可分;
//   ② 悬空技法 id = **构建期硬失败**(不是运行期静默全缺页);
//   ③ 轴缺席 ⇒ 该半边 **NOT-RUN**(不抛、不借绿),与「查过没问题」结构可分;
//   ④ 布局往返:写方与读方同批改,`teach_technique` 逐值不丢;
//   ⑤ 缺页判定:未解锁才缺、已解锁不缺、纯知识页永不缺、查询未注入必须抛。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public sealed class tutorial_paper_technique_test
    {
        // ════════════════════════════════════════════════════════════════
        //  夹具
        // ════════════════════════════════════════════════════════════════

        /// <summary>技法 id 闭集替身(可显式「未就绪」)。</summary>
        private sealed class FakeTechniqueIds : ITutorialTechniqueIdSource
        {
            private readonly int[] _ids;
            public FakeTechniqueIds(params int[] ids) { _ids = ids; }
            public bool TryGetTechniqueIds(out IReadOnlyCollection<int> ids)
            {
                ids = _ids;
                return _ids != null;
            }
        }

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

        private static string Seed()
            => File.ReadAllText(Path.Combine(RepoRoot(), TutorialContentSchema.DataDirName,
                                             TutorialContentSchema.SourceFileName));

        /// <summary>
        /// 在真种子的**唯一那张纸**上注入 <c>teachTechnique</c>。
        /// <para>⚠️ 锚点取 `illustrationKey` 那一行(种子内唯一且稳定),不解析 JSON ——
        /// 用一个手写最小解析器改种子会引入第二解析器(第二真源),那正是 ADR-014 要消灭的形态。</para>
        /// </summary>
        private static string SeedWith(string teachTechniqueLiteral)
        {
            const string anchor = "\"illustrationKey\": \"paper_wangzhen_01_v1\",";
            string seed = Seed();
            Assert.That(seed.Contains(anchor), Is.True,
                "夹具前提不成立:种子里找不到插图键锚点 —— 种子结构变了,本夹具须同步复核(不是改锚点就完事)");
            return seed.Replace(anchor, anchor + "\n      \"teachTechnique\": " + teachTechniqueLiteral + ",");
        }

        // ════════════════════════════════════════════════════════════════
        //  ① 缺省语义
        // ════════════════════════════════════════════════════════════════

        /// <summary>不写 <c>teachTechnique</c> ⇒ <c>NoTechnique</c>(-1),且与「教 0 号技法」可分。</summary>
        [Test]
        public void test_absentTeachTechnique_defaultsToNoTechnique()
        {
            var bound = TutorialContentBake.BindForTest(Seed(), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12));

            Assert.That(bound.Papers.Count, Is.EqualTo(7), "A4 起 7 张(第 0 张仍是不教技法的望诊四要)");
            Assert.That(bound.Papers[0].TeachTechnique, Is.EqualTo(TutorialContentSchema.NoTechnique),
                "缺省必须是 -1(不教任何技法)—— 用 0 当哨兵会让它与「教止血包扎」不可分");
            Assert.That(TutorialContentSchema.NoTechnique, Is.Not.EqualTo(0),
                "哨兵不得等于任何合法技法 id(0 = 止血包扎 是合法值)");
        }

        /// <summary>写成 0(教止血包扎)⇒ 与缺省**不同值**,证明哨兵没吞掉合法 id。</summary>
        [Test]
        public void test_explicitZero_isDistinctFromAbsent()
        {
            var bound = TutorialContentBake.BindForTest(SeedWith("0"), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12));

            Assert.That(bound.Papers[0].TeachTechnique, Is.EqualTo(0));
            Assert.That(bound.Papers[0].TeachTechnique,
                Is.Not.EqualTo(TutorialContentSchema.NoTechnique),
                "0 是合法技法 id,不得被读成「不教」");
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 悬空 / 非法值 = 构建期硬失败
        // ════════════════════════════════════════════════════════════════

        /// <summary>技法 id 不在轴里 ⇒ 拒收,且报文点名「悬垂键」。</summary>
        [Test]
        public void test_danglingTechniqueId_isRejected()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialContentBake.BindForTest(SeedWith("99"), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12)));

            Assert.That(string.Join(";", ex.Errors), Does.Contain("悬垂键"),
                "报文须点明这是悬垂键 —— 说成「非法值」会让人去查格式而不是查轴");
        }

        /// <summary>-1 之外的其他负数不是 id ⇒ 拒收。</summary>
        [Test]
        public void test_negativeOtherThanSentinel_isRejected()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialContentBake.BindForTest(SeedWith("-2"), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12)));

            Assert.That(string.Join(";", ex.Errors), Does.Contain("-2"));
        }

        /// <summary>写成字符串 ⇒ 拒收(与「缺失」是两件事)。</summary>
        [Test]
        public void test_wrongType_isRejected()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialContentBake.BindForTest(SeedWith("\"0\""), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12)));

            Assert.That(string.Join(";", ex.Errors), Does.Contain("teachTechnique"));
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 轴缺席 = NOT-RUN(不借绿)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 未注入技法 id 来源 ⇒ 该半边 NOT-RUN:不抛,且 <c>TechniqueIdTableReady == false</c>。
        /// <para>反证:把「未就绪」当「全部悬空」⇒ 本条仍绿但下一条(悬空拒收)会误杀真数据;
        /// 把「未就绪」当「全部合法」⇒ 本条的 ready 位会变 true ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_sourceAbsent_isNotRunNotFailure()
        {
            var bound = TutorialContentBake.BindForTest(SeedWith("99"), null, null, techniqueIds: null);

            Assert.That(bound.TechniqueIdTableReady, Is.False,
                "轴缺席 ⇒ 该半边 NOT-RUN;ready=true 会把「没查」说成「查过没问题」");
            Assert.That(bound.Papers[0].TeachTechnique, Is.EqualTo(99),
                "未就绪时**不校验但照常绑定** —— 数据不因缺表而丢失");
        }

        /// <summary>真仓根 + 真轴 ⇒ 该半边真跑过(ready = true)。</summary>
        [Test]
        public void test_sourcePresent_isReady()
        {
            var baked = TutorialContentBake.BakeFromRepo(RepoRoot());
            Assert.That(baked.Bound.TechniqueIdTableReady, Is.True,
                "真轴上这条半边必须真跑 —— 否则「校验通过」四个字没有主语");
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 生产来源:读的是 9 的处置轴,不是第二份词表
        // ════════════════════════════════════════════════════════════════

        /// <summary>真轴 id 集 = {0,1,2,3,10,11,12}(与 T7 后的 `actions` 段逐值相等)。</summary>
        [Test]
        public void test_productionSource_readsAxisActionIds()
        {
            var src = new TutorialTechniqueIdSource(RepoRoot());
            Assert.That(src.TryGetTechniqueIds(out IReadOnlyCollection<int> ids), Is.True);

            var sorted = new List<int>(ids);
            sorted.Sort();
            Assert.That(sorted, Is.EqualTo(new[] { 0, 1, 2, 3, 10, 11, 12 }),
                "技法 id 空间 = 9 的处置轴 action 闭集(ADR-043 §D5);不一致 = 有人另立了第二份词表");
        }

        /// <summary>`actions` 零条 ⇒ 抛(零条闭集会让每条 teachTechnique 都判悬空 = 假绿面)。</summary>
        [Test]
        public void test_productionSource_emptyActions_throws()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialTechniqueIdSource.ParseIds("{\"actions\": []}", "夹具"));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("零条"));
        }

        /// <summary>文件不存在 ⇒ 返回 false(NOT-RUN),**不抛** —— 与「损坏」结构可分。</summary>
        [Test]
        public void test_productionSource_missingFile_isNotRun()
        {
            // 刻意用**不存在**的目录:不建目录、不写盘 —— `File.Exists` 对不存在的路径恒 false,
            // 正好就是「文件缺席」这一条。⇒ 本文件零写盘调用(`FsWriteBoundaryGate` 的 R2 不触发)。
            string absentDir = Path.Combine(Path.GetTempPath(),
                "d8_notrun_" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(absentDir), Is.False, "夹具前提:该目录不得存在");

            var src = new TutorialTechniqueIdSource(absentDir);
            Assert.That(src.TryGetTechniqueIds(out IReadOnlyCollection<int> ids), Is.False);
            Assert.That(ids, Is.Null);
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 布局往返(写方与读方同批改)
        // ════════════════════════════════════════════════════════════════

        /// <summary><c>teach_technique</c> 经 cooked 往返逐值不丢(含哨兵与合法 id 两种)。</summary>
        [Test]
        public void test_roundTrip_preservesTeachTechnique()
        {
            var baked = TutorialContentBake.BakeFromJson(SeedWith("3"), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12));

            Assert.That(TutorialContentTable.TryDecode(baked.Cooked, out var table, out string error), Is.True, error);
            Assert.That(table.Papers.Count, Is.EqualTo(7), "A4 起 7 张");
            Assert.That(table.Papers[0].TeachTechnique, Is.EqualTo(3),
                "写方 / 读方字段序不一致 ⇒ 本条红(布局漂移的唯一机械兜现)");

            var bakedSentinel = TutorialContentBake.BakeFromJson(Seed(), null, null, new FakeTechniqueIds(0, 1, 2, 3, 10, 11, 12));
            Assert.That(TutorialContentTable.TryDecode(bakedSentinel.Cooked, out var t2, out string e2), Is.True, e2);
            Assert.That(t2.Papers[0].TeachTechnique, Is.EqualTo(-1));
            Assert.That(t2.Papers[0].TeachesTechnique, Is.False);
        }

        /// <summary>源 schemaVersion 与常量不一致 ⇒ 拒收(版本号不是装饰)。</summary>
        [Test]
        public void test_schemaVersionMismatch_isRejected()
        {
            string bad = Seed().Replace("\"schemaVersion\": 2,", "\"schemaVersion\": 1,");
            var ex = Assert.Throws<BakeValidationException>(
                () => TutorialContentBake.BindForTest(bad, null, null, techniqueIds: null));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("schemaVersion"));
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑥ 缺页判定(纯函数)
        // ════════════════════════════════════════════════════════════════

        private static TutorialPaperRow Paper(string id, int teachTechnique)
            => new TutorialPaperRow(id, "observe", "标题", "正文", "4:3", null, teachTechnique);

        /// <summary>技法未解锁 ⇒ 该页缺。</summary>
        [Test]
        public void test_missing_marksLockedTechniquePage()
        {
            var ids = TutorialPaperMissing.Compute(new[] { Paper("p1", 0) }, _ => false);
            Assert.That(ids, Is.EqualTo(new[] { "p1" }));
        }

        /// <summary>技法已解锁 ⇒ 该页不缺。</summary>
        [Test]
        public void test_missing_skipsUnlockedTechniquePage()
        {
            var ids = TutorialPaperMissing.Compute(new[] { Paper("p1", 0) }, _ => true);
            Assert.That(ids, Is.Empty);
        }

        /// <summary>纯知识页(未绑定技法)永不缺 —— 否则整本手册会全白。</summary>
        [Test]
        public void test_missing_skipsPaperWithoutTechnique()
        {
            var ids = TutorialPaperMissing.Compute(
                new[] { Paper("p1", TutorialContentSchema.NoTechnique) }, _ => false);
            Assert.That(ids, Is.Empty, "「望诊四要」这类页不该因为没解锁技法而消失");
        }

        /// <summary>只判被锁的那几页,按输入序、去重。</summary>
        [Test]
        public void test_missing_returnsOnlyLockedOnesInOrder()
        {
            var papers = new[]
            {
                Paper("p_knowledge", -1),
                Paper("p_locked_a", 10),
                Paper("p_open", 0),
                Paper("p_locked_b", 12),
            };
            var unlocked = new HashSet<int> { 0 };
            var ids = TutorialPaperMissing.Compute(papers, unlocked.Contains);

            Assert.That(ids, Is.EqualTo(new[] { "p_locked_a", "p_locked_b" }));
        }

        /// <summary>查询未注入 ⇒ 抛(默认「全部未解锁」会把未接线渲染成「整本被撕光」)。</summary>
        [Test]
        public void test_missing_nullQuery_throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => TutorialPaperMissing.Compute(new[] { Paper("p1", 0) }, null));
            Assert.That(ex.ParamName, Is.EqualTo("isTechniqueUnlocked"));
        }

        /// <summary>无纸 ⇒ 空集(空集上其余判据恒绿,故单列一条证明它不抛)。</summary>
        [Test]
        public void test_missing_nullPapers_isEmpty()
        {
            Assert.That(TutorialPaperMissing.Compute(null, _ => false), Is.Empty);
            Assert.That(TutorialPaperMissing.Compute(Array.Empty<TutorialPaperRow>(), _ => false), Is.Empty);
        }
    }
}
