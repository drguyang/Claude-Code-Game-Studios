// 手册近景的内容宿主 —— 判据夹具(T10 · 2026-11-09)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2(钩子病例的载体 = 手册缺页)
//       · ADR-013 §9 C3(42 只渲染)· ADR-043 §D7(解锁查询 = 病史流折叠,同源可重建)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的五件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① **缺页判据接上了**:未解锁技法的那一页缺、已解锁的不缺、纯知识页永不缺;
//   ② **真的按 actor 问**:换一个 actor 就换一套缺页集(否则查询只被问了一次就缓存了);
//   ③ **真的现算**:同一个宿主,查询的后备集合变了 ⇒ 下一次 `Build()` 跟着变(零缓存);
//   ④ **两个 fail-loud**:查询未注入在**构造期**抛;玩家 id 未绑定在 `Build` 抛且报文点名该值;
//   ⑤ **空纸不崩**:空集 ⇒ 空缺页集(空集上其余判据恒绿,故单列一条证明它不抛)。
//
// ⚠️ 本文件**零仓库文件读取、零写盘** —— 纸条目内联构造(它们不是「真表的一致性」判据,
//   那是 T8 的 `tutorial_paper_technique_test` 的范围)。`FsWriteBoundaryGate` 不触发。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public sealed class paper_closeup_presenter_test
    {
        // ════════════════════════════════════════════════════════════════
        //  夹具
        // ════════════════════════════════════════════════════════════════

        /// <summary>可变的解锁查询替身 —— 「现算」那条判据需要它在两次 Build 之间变。</summary>
        private sealed class FakeUnlocks : ITechniqueUnlockQuery
        {
            private readonly Dictionary<int, HashSet<int>> _byActor = new Dictionary<int, HashSet<int>>();

            public HashSet<int> For(int actorId)
            {
                if (!_byActor.TryGetValue(actorId, out HashSet<int> set))
                {
                    set = new HashSet<int>();
                    _byActor[actorId] = set;
                }
                return set;
            }

            public bool IsUnlocked(int actorId, int techniqueId) => For(actorId).Contains(techniqueId);
        }

        private static TutorialPaperRow Paper(string id, int teachTechnique)
            => new TutorialPaperRow(id, "observe", "标题", "正文", "4:3", null, teachTechnique);

        private static PaperCloseupPresenter Presenter(
            IReadOnlyList<TutorialPaperRow> papers, ITechniqueUnlockQuery unlocks, int actorId)
            => new PaperCloseupPresenter(papers, unlocks, () => actorId);

        // ════════════════════════════════════════════════════════════════
        //  ① 缺页判据接上了
        // ════════════════════════════════════════════════════════════════

        /// <summary>未解锁技法的那一页缺。</summary>
        [Test]
        public void test_lockedTechniquePage_isMissing()
        {
            var unlocks = new FakeUnlocks();
            var snapshot = Presenter(new[] { Paper("p_stop", 0) }, unlocks, actorId: 7).Build();

            Assert.That(snapshot.MissingPaperIds, Is.EqualTo(new[] { "p_stop" }));
            Assert.That(snapshot.Papers.Count, Is.EqualTo(1), "纸条目要一起交出去,不只交缺页");
        }

        /// <summary>已解锁 ⇒ 不缺(去掉「已解锁跳过」⇒ 本条的期望集变非空 ⇒ 红)。</summary>
        [Test]
        public void test_unlockedTechniquePage_isNotMissing()
        {
            var unlocks = new FakeUnlocks();
            unlocks.For(7).Add(0);

            var snapshot = Presenter(new[] { Paper("p_stop", 0) }, unlocks, actorId: 7).Build();
            Assert.That(snapshot.MissingPaperIds, Is.Empty);
        }

        /// <summary>纯知识页(不教技法)永不缺 —— 否则整本手册会全白。</summary>
        [Test]
        public void test_knowledgePage_neverMissing()
        {
            var unlocks = new FakeUnlocks();
            var snapshot = Presenter(new[] { Paper("p_wangzhen", -1) }, unlocks, actorId: 7).Build();
            Assert.That(snapshot.MissingPaperIds, Is.Empty,
                "「望诊四要」这类页不该因为没解锁技法而消失");
        }

        /// <summary>只判被锁的那几页,按**输入序**。</summary>
        [Test]
        public void test_onlyLockedPagesInInputOrder()
        {
            var unlocks = new FakeUnlocks();
            unlocks.For(7).Add(0);

            var papers = new[]
            {
                Paper("p_knowledge", -1),
                Paper("p_locked_a", 10),
                Paper("p_open", 0),
                Paper("p_locked_b", 12),
            };
            var snapshot = Presenter(papers, unlocks, actorId: 7).Build();

            Assert.That(snapshot.MissingPaperIds, Is.EqualTo(new[] { "p_locked_a", "p_locked_b" }));
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 真的按 actor 问
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 同一个宿主、同一张表,换 actor ⇒ 缺页集不同。
        /// <para>判别力:若 presenter 把 `IsUnlocked` 的结果缓存在构造期(或忽略了 actorId),
        /// 两条断言会给出同一个集合 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_missingSetDependsOnActor()
        {
            var unlocks = new FakeUnlocks();
            unlocks.For(7).Add(2);      // 玩家 7 会技法 2
            // 玩家 8 什么都不会

            var papers = new[] { Paper("p_a", 2), Paper("p_b", 3) };

            var asPlayer7 = new PaperCloseupPresenter(papers, unlocks, () => 7).Build();
            var asPlayer8 = new PaperCloseupPresenter(papers, unlocks, () => 8).Build();

            Assert.That(asPlayer7.MissingPaperIds, Is.EqualTo(new[] { "p_b" }));
            Assert.That(asPlayer8.MissingPaperIds, Is.EqualTo(new[] { "p_a", "p_b" }),
                "另一个玩家什么都没学会 ⇒ 两页都缺");
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 真的现算(零缓存)
        // ════════════════════════════════════════════════════════════════

        /// <summary>查询的后备集合变了 ⇒ 下一次 `Build()` 跟着变(承 AC-53-06 零跨 tick 态)。</summary>
        [Test]
        public void test_build_recomputesEveryTime()
        {
            var unlocks = new FakeUnlocks();
            var presenter = Presenter(new[] { Paper("p_a", 2) }, unlocks, actorId: 7);

            Assert.That(presenter.Build().MissingPaperIds, Is.EqualTo(new[] { "p_a" }));

            unlocks.For(7).Add(2);      // 玩家刚刚学会

            Assert.That(presenter.Build().MissingPaperIds, Is.Empty,
                "缓存会让「刚学会」在本次会话里不生效,读档后才会突然对 —— 静默且难查");
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 两个 fail-loud
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 查询未注入 ⇒ **构造期**抛,且点名校验形参。
        /// <para>为什么必须抛:默认「全部未解锁」会把「系统没接上」渲染成「整本手册被撕光」;
        /// 默认「全部已解锁」会渲染成「完好」—— 后者更坏,玩家不会发现少了一页。</para>
        /// </summary>
        [Test]
        public void test_nullQuery_throwsAtConstruction()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new PaperCloseupPresenter(new[] { Paper("p_a", 0) }, null, () => 7));
            Assert.That(ex.ParamName, Is.EqualTo("unlocks"));
        }

        /// <summary>玩家 id 未绑定(-1)⇒ `Build` 抛,且报文里能看到那个值。</summary>
        [Test]
        public void test_unboundActor_throwsWithValue()
        {
            var presenter = Presenter(new[] { Paper("p_a", 0) }, new FakeUnlocks(), actorId: -1);

            var ex = Assert.Throws<InvalidOperationException>(() => presenter.Build());
            Assert.That(ex.Message, Does.Contain("-1"),
                "报文须带上实际值 —— 只说「未绑定」会让人去找绑定点而不是找调用时序");
        }

        /// <summary>纸条目为 null ⇒ 构造期抛(空纸不是「还没读到」,是数据没接上)。</summary>
        [Test]
        public void test_nullPapers_throwsAtConstruction()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new PaperCloseupPresenter(null, new FakeUnlocks(), () => 7));
            Assert.That(ex.ParamName, Is.EqualTo("papers"));
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 空纸不崩
        // ════════════════════════════════════════════════════════════════

        /// <summary>空集 ⇒ 空缺页集,不抛。</summary>
        [Test]
        public void test_emptyPapers_yieldsEmptyMissing()
        {
            var presenter = Presenter(Array.Empty<TutorialPaperRow>(), new FakeUnlocks(), actorId: 7);
            var snapshot = presenter.Build();

            Assert.That(snapshot.Papers, Is.Empty);
            Assert.That(snapshot.MissingPaperIds, Is.Empty);
            Assert.That(presenter.PaperCount, Is.EqualTo(0));
        }

        /// <summary>`PaperCount` = 纸条目数(不是缺页数 —— 两个量不能混)。</summary>
        [Test]
        public void test_paperCount_countsPapersNotMissing()
        {
            var papers = new[] { Paper("a", 0), Paper("b", 0), Paper("c", -1) };
            Assert.That(Presenter(papers, new FakeUnlocks(), 7).PaperCount, Is.EqualTo(3));
        }
    }
}
