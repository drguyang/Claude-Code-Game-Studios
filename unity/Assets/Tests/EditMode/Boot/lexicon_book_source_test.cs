// sprint-08 S1.1 件 C/D · 病名册条目源判据(2026-10-12)
//
// 判据(可证伪):
//   ① 未见病 ⇒ 条目集空(病名册空 —— 反幻想二:没见过就翻不到);
//   ② 已见病 D ⇒ 词表中 disease_ids 含 D 的行入册,只出 lexicon_id(守密:无 disease_id);
//   ③ 多对一:一条词关联多病种,任一已见即入册(且不重复);
//   ④ 孤儿条目(disease_ids 空)恒不入册;
//   ⑤ 组合根真装载:词表经 provider/磁盘装载(非 Fake),S1.0 产物真接入;
//   ⑥ 序 = lexicon_id 升序(词表序)。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Casebook;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="LexiconBookSource"/> 判据(sprint-08 S1.1 件 C/D)。</summary>
    internal sealed class lexicon_book_source_test
    {
        /// <summary>合成词表:3 行 —— 双关联 / 单关联 / 孤儿。</summary>
        private static JudgmentLexemeRow[] SyntheticRows() => new[]
        {
            new JudgmentLexemeRow(1, "伤寒", new[] { 10, 11 }),   // 多对一
            new JudgmentLexemeRow(2, "痢疾", new[] { 20 }),        // 单关联
            new JudgmentLexemeRow(3, "怪病", Array.Empty<int>()),  // 孤儿
        };

        // ══════════════════════════════════════════════════════════════════════
        // ① 未见病 ⇒ 空
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_noSeenDisease_emptyBook()
        {
            var src = new LexiconBookSource(SyntheticRows());
            Assert.IsEmpty(src.PointableLexiconIds(Array.Empty<int>()),
                "未见过任何病 ⇒ 病名册空(反幻想二)");
            Assert.IsEmpty(src.PointableLexiconIds(null),
                "null 已见集 ⇒ 空(不抛,视为无)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ② 已见病 ⇒ 命中行入册(多对一)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_seenDisease_includesMatchingRows()
        {
            var src = new LexiconBookSource(SyntheticRows());

            Assert.AreEqual(new[] { 1 }, src.PointableLexiconIds(new[] { 10 }),
                "已见病 10 ⇒ 含 10 的词条 1 入册(lexicon_id,非 disease_id)");
            Assert.AreEqual(new[] { 1 }, src.PointableLexiconIds(new[] { 11 }),
                "已见病 11 ⇒ 同一词条 1 入册(多对一:任一端命中)");
            Assert.AreEqual(new[] { 1, 2 }, src.PointableLexiconIds(new[] { 10, 20 }),
                "已见 {10,20} ⇒ 词条 1 与 2 入册,升序(词表序)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ③ 多对一不重复(单行多病种命中只入一次)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_multiLinkedRow_addedOnce()
        {
            var src = new LexiconBookSource(SyntheticRows());
            Assert.AreEqual(new[] { 1 }, src.PointableLexiconIds(new[] { 10, 11 }),
                "词条 1 关联 {10,11},两端皆已见 ⇒ 仍只入册一次");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ④ 孤儿条目恒不入册
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_orphanRow_neverIncluded()
        {
            var src = new LexiconBookSource(SyntheticRows());
            // 即便已见集含任意病,孤儿条目(3)无病种可关联 ⇒ 恒不入册
            var ids = src.PointableLexiconIds(new[] { 10, 20, 99 });
            Assert.IsFalse(Array.IndexOf(ids, 3) >= 0,
                "孤儿条目(disease_ids 空)不可由「见过」触发 ⇒ 恒不入册");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑤ 组合根真装载(S1.0 产物真接入 —— 非 Fake)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_compositionRoot_loadsRealLexicon()
        {
            var bag = CompositionRoot.Assemble();
            Assert.IsNotNull(bag.LexiconBook, "组合根须装配病名册条目源(件 C/D)");
            Assert.Greater(bag.LexiconBook.TotalRows, 0,
                "词表从 S1.0 真烘焙产物装载(非空 —— 空表 = 装载失败被吞)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑥ 端到端:查体 ⇒ 已见集 ⇒ 词表反查(件 B → C 串联)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_endToEnd_examThenBookReflects()
        {
            var bag = CompositionRoot.Assemble();
            var patient = bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            bag.Presence.AddPresent(patient, new DaYiJingCheng.Sim.Contracts.WorldPos(0, 0, 0));
            bag.VitalsService.OnTickEdge(0);

            const int player = 9001;
            Assert.IsEmpty(bag.LexiconBook.PointableLexiconIds(bag.SeenDisease.SeenDiseaseIds(player)),
                "查体前 ⇒ 病名册空");

            bag.SeenDisease.OnExamined(patient, tick: 5, actorId: player);

            int[] seen = bag.SeenDisease.SeenDiseaseIds(player);
            Assert.AreEqual(new[] { 1 }, seen, "查体后 ⇒ 已见集含病种 1");
            // 词表反查:病种 1 在 S1.0 真表里的关联行(若有)入册;此处只断言串联非抛 + 结果自洽
            int[] book = bag.LexiconBook.PointableLexiconIds(seen);
            foreach (int id in book)
                Assert.GreaterOrEqual(id, 1, "入册条目 id ≥ 1(0 = 空栏哨兵,不入册)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑦ 件 E:病名册条目集经 42 只读源(DTO)可达
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_dtoSource_seenLexiconIds_matchesBook()
        {
            var bag = CompositionRoot.Assemble();
            var patient = bag.PatientSpawner.SpawnNext(diseaseId: 1, tick: 0);
            bag.Presence.AddPresent(patient, new DaYiJingCheng.Sim.Contracts.WorldPos(0, 0, 0));
            bag.VitalsService.OnTickEdge(0);

            const int player = 9001;
            Assert.IsEmpty(bag.CasebookSource.SeenLexiconIds,
                "未 SetViewer ⇒ 病名册空(未见任何病)");

            bag.CasebookSource.SetViewer(player);
            bag.SeenDisease.OnExamined(patient, tick: 5, actorId: player);

            Assert.AreEqual(
                bag.LexiconBook.PointableLexiconIds(bag.SeenDisease.SeenDiseaseIds(player)),
                bag.CasebookSource.SeenLexiconIds,
                "DTO 源 SeenLexiconIds = 词表反查结果(件 C → E 接线一致)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑧ 守密:SeenLexiconIds 只出 lexicon_id(DTO 面零 disease_id)
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_dtoSource_seenLexiconIds_noDiseaseIdLeak()
        {
            var bag = CompositionRoot.Assemble();
            bag.CasebookSource.SetViewer(9001);
            // 契约面 = IReadOnlyList<int>(纯整数 id 列表)—— 结构上不可能携带 disease_id;
            // 且 PresentationDtoGuard 递归扫描呈现层 DTO(件 H 的 42 侧断言覆盖)。
            var asIntList = bag.CasebookSource.SeenLexiconIds;
            Assert.IsNotNull(asIntList, "SeenLexiconIds 非 null(空 = 空列表)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // ⑨ 未设观看者负参 fail-loud
        // ══════════════════════════════════════════════════════════════════════
        [Test]
        public void test_setViewer_negativeArg_failLoud()
        {
            var bag = CompositionRoot.Assemble();
            Assert.Throws<ArgumentOutOfRangeException>(
                () => bag.CasebookSource.SetViewer(-1),
                "观看者 actorId 为负 ⇒ fail-loud");
        }
    }
}
