// 钩子病例的判定 —— 判据夹具(2026-11-09 · T5a)
//
// 权威:design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.2(用户 2026-11-09 裁定)
//
// 本文件要证明:
//   ① **可诊断性**(规则 2)—— 只有主观 / 病史通道的体征**不算**看得见;
//   ② **配额**(规则 1)—— 章内配额满 ⇒ 不再放行;P0 = 1;
//   ③ **只有「技法全锁」可改判**(规则 3)—— 等级不足**不**因钩子而放行;
//   ④ **缺哪几门技法可回带** —— 呈现层据此说「你缺的是止血包扎」;
//   ⑤ **账本从事件流重建** —— onset 在解锁**之前**才算钩子,解锁之后不算;
//        未声明的病种不算(它是「数据未覆盖」,不是「缺技法」);
//   ⑥ **驱动真的会把钩子病例放出来**,且走查读数带 `hook=1` / 缺技法列表。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate`。
// ⚠️ 无自指判据(F3):期望值全为字面量。

using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>T5a:钩子病例的准入判据 + 配额账本 + 驱动接线。</summary>
    [TestFixture]
    public sealed class HookCasePolicyTest
    {
        private const int Actor = 7;

        // ── 构造件 ────────────────────────────────────────────────────────────

        private static DiseaseRegistryEntry Row(int id, string key,
                                                int[] channels = null)
        {
            var e = new DiseaseRegistryEntry { DiseaseId = id, DiseaseKey = key };
            if (channels != null)
            {
                var signs = new SignEntry[channels.Length];
                for (int i = 0; i < channels.Length; i++)
                    signs[i] = new SignEntry("sign" + i, channels[i], new Fix(Fix.OneRaw / 5));
                e.Signs = signs;
            }
            return e;
        }

        private static TechniqueDrawIndex Index(params (string Key, int Action)[] relations)
        {
            var rows = new List<TreatableByCookedRow>();
            foreach ((string key, int action) in relations)
                rows.Add(new TreatableByCookedRow(key, action, "symptomatic"));
            return new TechniqueDrawIndex(rows);
        }

        // ── ① 可诊断性 ────────────────────────────────────────────────────────

        [Test]
        public void test_diagnosable_physicalChannel_isTrue()
        {
            Assert.That(HookCasePolicy.IsDiagnosable(Row(5, "DIS_X", new[] { SignChannels.Face })), Is.True);
            Assert.That(HookCasePolicy.IsDiagnosable(Row(5, "DIS_X", new[] { SignChannels.Wound })), Is.True);
        }

        [Test]
        public void test_diagnosable_onlySubjectiveOrHistory_isFalse()
        {
            Assert.That(HookCasePolicy.IsDiagnosable(
                Row(5, "DIS_X", new[] { SignChannels.Subjective })), Is.False,
                "主观族不驱动任何可见体征(8 的 MaskOf 对这两位返回 0)⇒ 玩家看不成「这是什么病」");
            Assert.That(HookCasePolicy.IsDiagnosable(
                Row(5, "DIS_X", new[] { SignChannels.History })), Is.False);
            Assert.That(HookCasePolicy.IsDiagnosable(
                Row(5, "DIS_X", new[] { SignChannels.Subjective, SignChannels.History })), Is.False,
                "两条非体格体征也不构成可诊断 —— 判据是「有体格通道」,不是「条数够」");
        }

        [Test]
        public void test_diagnosable_noSigns_isFalse()
        {
            Assert.That(HookCasePolicy.IsDiagnosable(Row(5, "DIS_X")), Is.False, "signs 为 null ⇒ 不可诊断");
            Assert.That(HookCasePolicy.IsDiagnosable(
                Row(5, "DIS_X", new int[0])), Is.False, "signs 为空 ⇒ 不可诊断");
        }

        [Test]
        public void test_diagnosable_mixedChannels_isTrue()
        {
            Assert.That(HookCasePolicy.IsDiagnosable(
                Row(5, "DIS_X", new[] { SignChannels.Subjective, SignChannels.Breath })), Is.True,
                "只要有一条体格通道就够");
        }

        // ── ② 配额 ────────────────────────────────────────────────────────────

        [Test]
        public void test_quotaFreeAndDiagnosable_allows()
        {
            Assert.That(HookCasePolicy.AllowsInjection(
                Row(5, "DIS_X", new[] { SignChannels.Face }), injectedInChapter: 0), Is.True);
        }

        [Test]
        public void test_quotaFull_rejects()
        {
            Assert.That(HookCasePolicy.AllowsInjection(
                Row(5, "DIS_X", new[] { SignChannels.Face }), injectedInChapter: 1), Is.False,
                "P0 每章 1 例 ⇒ 已注入 1 例即不再放行");
        }

        [Test]
        public void test_quotaFreeButNotDiagnosable_rejects()
        {
            Assert.That(HookCasePolicy.AllowsInjection(
                Row(5, "DIS_X", new[] { SignChannels.Subjective }), injectedInChapter: 0), Is.False,
                "配额没满也不够 —— 看不见的病不配做钩子(那是双重挫败)");
        }

        [Test]
        public void test_negativeInjected_failsLoud()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => HookCasePolicy.AllowsInjection(Row(5, "DIS_X"), injectedInChapter: -1));
        }

        // ── ③④ 与投放门的组合 ────────────────────────────────────────────────

        [Test]
        public void test_decide_techniqueLocked_withHookAllowed_isMarked()
        {
            PatientDrawDecision d = PatientDrawEligibility.Decide(
                Row(5, "DIS_X"), Index(("DIS_X", 12)), 0, _ => false,
                hookAllowed: true);

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.HookCase));
            Assert.That(d.Admitted, Is.True);
            Assert.That(d.IsHookCase, Is.True, "钩子病例必须能被调用方识别");
        }

        [Test]
        public void test_decide_techniqueLocked_withoutHook_isRejected()
        {
            PatientDrawDecision d = PatientDrawEligibility.Decide(
                Row(5, "DIS_X"), Index(("DIS_X", 12)), 0, _ => false);

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.NoUnlockedTechnique));
            Assert.That(d.Admitted, Is.False);
        }

        [Test]
        public void test_decide_levelTooLow_isNotUpgradedByHook()
        {
            DiseaseRegistryEntry gated = Row(5, "DIS_X");
            gated.MinSkillLevel = 9;

            PatientDrawDecision d = PatientDrawEligibility.Decide(
                gated, Index(("DIS_X", 12)), 0, _ => true, hookAllowed: true);

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.LevelTooLow),
                "等级是同一门技法内部的难易档,不是「缺一门知识」⇒ 钩子不改判它");
            Assert.That(d.Admitted, Is.False);
        }

        [Test]
        public void test_decide_reportsMissingTechniques()
        {
            PatientDrawDecision d = PatientDrawEligibility.Decide(
                Row(5, "DIS_X"), Index(("DIS_X", 10), ("DIS_X", 12)), 0,
                t => t == 10, hookAllowed: false);

            Assert.That(d.Admitted, Is.True, "10 已解锁 ⇒ 投放");
            Assert.That(d.MissingTechniques, Is.Empty, "投放时不报缺口");
        }

        [Test]
        public void test_decide_hookCase_carriesMissingTechniques()
        {
            PatientDrawDecision d = PatientDrawEligibility.Decide(
                Row(5, "DIS_X"), Index(("DIS_X", 10), ("DIS_X", 12)), 0,
                _ => false, hookAllowed: true);

            Assert.That(d.IsHookCase, Is.True);
            Assert.That(d.MissingTechniques, Is.EqualTo(new[] { 10, 12 }),
                "规则 3:必须能说出缺的是哪几门技法(呈现层据此写「缺页」)");
        }

        // ── ⑤ 账本 ────────────────────────────────────────────────────────────

        private sealed class Sink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        private sealed class Ledger
        {
            public readonly Sink Sink = new Sink();
            public readonly InMemoryBlobPool Pool = new InMemoryBlobPool();
            public readonly PayloadEncoder Encoder;
            public readonly HookCaseLedger Cursor;

            public Ledger(TechniqueDrawIndex index, params DiseaseRegistryEntry[] rows)
            {
                Encoder = new PayloadEncoder(Pool);
                var decoder = new PayloadDecoder(Pool);
                var byId = new Dictionary<int, DiseaseRegistryEntry>();
                foreach (DiseaseRegistryEntry r in rows) byId[r.DiseaseId] = r;
                Cursor = new HookCaseLedger(() => Sink.Events, decoder, index, byId);
            }

            public void Unlock(long tick, int technique, int actor = Actor)
            {
                var p = new TechniqueUnlockedPayload(actor, technique, 1);
                Sink.Events.Add(new SimEvent(tick, PatientId.None, -1,
                    EventKind.TechniqueUnlocked, Encoder.Encode(EventKind.TechniqueUnlocked, p)));
            }

            public void Onset(long tick, int diseaseId, int patientId)
            {
                var p = new DiseaseOnsetPayload(tick, diseaseId, patientId, 0L, 0L);
                Sink.Events.Add(new SimEvent(tick, new PatientId(patientId), -1,
                    EventKind.DiseaseOnset, Encoder.Encode(EventKind.DiseaseOnset, p)));
            }

            /// <summary>章推进(B1-b · 2026-11-10)。</summary>
            public void Chapter(long tick, int chapter)
            {
                var p = new ChapterAdvancedPayload(chapter, 0);
                Sink.Events.Add(new SimEvent(tick, PatientId.None, -1,
                    EventKind.ChapterAdvanced, Encoder.Encode(EventKind.ChapterAdvanced, p)));
            }
        }

        // ── ⑤b 章切分(B1-b · 2026-11-10)──────────────────────────────────────

        /// <summary>无 <c>ChapterAdvanced</c> ⇒ 当前章 = 0(「从未推进」的哨兵)。</summary>
        [Test]
        public void test_ledger_currentChapter_withoutChapterEvents_isZero()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Onset(10, 5, 100);
            Assert.That(l.Cursor.CurrentChapter(), Is.EqualTo(0));
        }

        /// <summary>当前章 = 流里**最后一条** <c>ChapterAdvanced</c>(载荷是绝对量,故取最后一条)。</summary>
        [Test]
        public void test_ledger_currentChapter_takesLatestAbsoluteValue()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Chapter(5, 1);
            l.Chapter(50, 2);
            Assert.That(l.Cursor.CurrentChapter(), Is.EqualTo(2));
        }

        /// <summary>
        /// 章内口径:先前章注入的钩子**不算进**本章(否则每推进一次配额就被历史吃掉)。
        /// <para>判别力:若实现把章比较写反 / 漏写,本章会数到 2(全局值)。</para>
        /// </summary>
        [Test]
        public void test_ledger_chapterScope_excludesEarlierChapters()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Onset(10, 5, 100);      // 章 0 的钩子
            l.Chapter(20, 1);
            l.Onset(30, 5, 101);      // 章 1 的钩子

            Assert.That(l.Cursor.CountHookCases(Actor), Is.EqualTo(2), "全局口径仍是 2");
            Assert.That(l.Cursor.CountHookCasesInCurrentChapter(Actor), Is.EqualTo(1),
                "章 1 内只有 1 例 —— 「每章 1 例」要的是这个数,不是整局");
        }

        /// <summary>
        /// **载体就位 ≠ 行为生效**:今日无生产者写 `ChapterAdvanced` ⇒ 章恒 0 ⇒
        /// 章内口径与全局口径**逐值相同**(即以「整局」代替「本章」)。
        /// <para>这条不是「缺口已修」的证明,恰恰相反 —— 它把「今日两者相同」钉住,
        /// 将来叙事轮接上生产者后本条**应当**转红,那时才可以说「每章 1 例真的生效了」。</para>
        /// </summary>
        [Test]
        public void test_ledger_chapterScope_withoutProducer_equalsGlobal()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Onset(10, 5, 100);
            l.Onset(20, 5, 101);

            Assert.That(l.Cursor.CountHookCasesInCurrentChapter(Actor),
                        Is.EqualTo(l.Cursor.CountHookCases(Actor)),
                        "无章节事件 ⇒ 章恒 0 ⇒ 两口径相同(今日如实状态)");
        }

        [Test]
        public void test_ledger_onsetBeforeUnlock_countsAsHook()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Onset(10, 5, 100);      // 此刻 12 未解锁 ⇒ 钩子
            Assert.That(l.Cursor.CountHookCases(Actor), Is.EqualTo(1));
        }

        [Test]
        public void test_ledger_onsetAfterUnlock_isNotHook()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Unlock(10, 12);
            l.Onset(20, 5, 100);
            Assert.That(l.Cursor.CountHookCases(Actor), Is.EqualTo(0),
                "解锁在先 ⇒ 这是普通病例(前向扫描天然给出这个语义)");
        }

        [Test]
        public void test_ledger_lateUnlockDoesNotRetroactivelyUncount()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Onset(10, 5, 100);      // 钩子
            l.Unlock(90, 12);         // 后解锁
            l.Onset(100, 5, 101);     // 普通

            Assert.That(l.Cursor.CountHookCases(Actor), Is.EqualTo(1),
                "后解锁不得把先前那例数丢 —— 否则配额凭空多出一格");
        }

        [Test]
        public void test_ledger_undeclaredDisease_isNotHook()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(2, "INJ_STAB"));
            l.Onset(10, 2, 100);

            Assert.That(l.Cursor.CountHookCases(Actor), Is.EqualTo(0),
                "轴里没声明的病种走 Undeclared 分支,不是「缺技法」");
        }

        [Test]
        public void test_ledger_otherActorUnlockDoesNotCount()
        {
            var l = new Ledger(Index(("DIS_X", 12)), Row(5, "DIS_X"));
            l.Unlock(10, 12, actor: 99);   // 别人的解锁
            l.Onset(20, 5, 100);

            Assert.That(l.Cursor.CountHookCases(Actor), Is.EqualTo(1),
                "解锁按 actor 区分(ADR-043 D7);别人的解锁不算");
        }

        // ── ⑥ 驱动接线 ────────────────────────────────────────────────────────

        [Test]
        public void test_driver_admitsHookCase_whenQuotaFree()
        {
            var pool = new InMemoryBlobPool();
            var sink = new Sink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, new PayloadEncoder(pool), 12345UL);
            DiseaseRegistryEntry blocked = Row(7, "DIS_LOCKED", new[] { SignChannels.Face });
            TechniqueDrawIndex index = Index(("DIS_LOCKED", 12));

            var driver = new PatientAppearedDriver(
                spawner, new PresenceRegistry(), new[] { blocked },
                admit: row =>
                {
                    bool hook = HookCasePolicy.AllowsInjection(row, injectedInChapter: 0);
                    return PatientDrawEligibility.Decide(row, index, 0, _ => false, hookAllowed: hook);
                });

            driver.OnTickEdge(0);

            Assert.That(driver.SpawnedCount, Is.EqualTo(1),
                "配额未满且可诊断 ⇒ 技法全锁的病种仍作为钩子出现");
            DiseaseOnsetPayload p = new PayloadDecoder(pool)
                .Decode<DiseaseOnsetPayload>(EventKind.DiseaseOnset, sink.Events[0].Payload);
            Assert.That(p.DiseaseId, Is.EqualTo(7));
        }

        [Test]
        public void test_driver_rejectsHookCase_whenQuotaFull()
        {
            var pool = new InMemoryBlobPool();
            var sink = new Sink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, new PayloadEncoder(pool), 12345UL);
            DiseaseRegistryEntry blocked = Row(7, "DIS_LOCKED", new[] { SignChannels.Face });
            TechniqueDrawIndex index = Index(("DIS_LOCKED", 12));

            var driver = new PatientAppearedDriver(
                spawner, new PresenceRegistry(), new[] { blocked },
                admit: row =>
                {
                    bool hook = HookCasePolicy.AllowsInjection(row, injectedInChapter: 1);
                    return PatientDrawEligibility.Decide(row, index, 0, _ => false, hookAllowed: hook);
                });

            driver.OnTickEdge(0);

            Assert.That(driver.SpawnedCount, Is.EqualTo(0), "配额已满 ⇒ 一例都不放");
            Assert.That(sink.Events.Count, Is.EqualTo(0));
        }
    }
}
