// 技法解锁的**触发通路** —— 判据夹具(2026-11-09 · T9)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2
//       · ADR-043 §D2(写者 = 30,宿主只是调用方)· §D4(载荷三字段)· §D5(技法 id 闭集)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的六件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① **三类源各有一条通路** —— PoiStateChanged / EventArrived / CaseClosed 都能解锁;
//   ② **匹配约束真的生效** —— 状态不符、档不够、未处置、病种不在集合内 ⇒ **不解锁**;
//      这四条的判别力来自「同一场景下把约束去掉就会解锁」,不是形容词;
//   ③ **幂等** —— 同一份流扫两遍,第二遍零写入(靠写者的 D6 去重,驱动不另写一份);
//   ④ **写入 tick = 当前 tick**(不是源事件的 tick)—— 源事件落在很早的 tick 时若照抄它,
//      `EventStream.Append` 会因 Tick 回退直接抛(BCD-码-3);
//   ⑤ **闭集** —— 规则里的 techniqueId 必须 ∈ 处置轴 action id;今日生产表为空,
//      故本条用**同一谓词**再喂一条越界规则来证明它不是空洞正控(F4);
//   ⑥ **零规则 ⇒ 零扫描**(今日生产形态)且不抛。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读 `assets/data/disease_action_axis.json`(经生产 `TutorialTechniqueIdSource`)以取得
//   技法 id 闭集。为什么必须读真表:⑤ 判的正是「规则里的 id 是否真的存在于处置轴」——
//   内联一份假词表只能证明本夹具自己写的谓词(假绿形态 F1)。
// ⚠️ **本文件零写盘**:所有事件写进内存 `EventStream` / `InMemoryBlobPool`。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.SkillSystem
{
    [TestFixture]
    public sealed class technique_unlock_trigger_test
    {
        // ════════════════════════════════════════════════════════════════
        //  夹具:一条内存流 + 真写者(D6 去重 / 闭集门都是生产码)
        // ════════════════════════════════════════════════════════════════

        private const int PlayerActorId = 7;

        private sealed class Harness
        {
            public readonly InMemoryBlobPool Pool = new InMemoryBlobPool();
            public readonly PayloadEncoder Encoder;
            public readonly PayloadDecoder Decoder;
            public readonly EventStream Stream;
            public readonly TechniqueUnlockProjection Query;
            public readonly TechniqueUnlockWriter Writer;

            public Harness(params int[] knownTechniques)
            {
                Encoder = new PayloadEncoder(Pool);
                Decoder = new PayloadDecoder(Pool);
                Stream = new EventStream(new SeqIdAuthority(), new AbsentPresence());
                Query = new TechniqueUnlockProjection(() => Stream.Events, Decoder);
                Writer = new TechniqueUnlockWriter(Stream, Encoder, Query,
                    new HashSet<int>(knownTechniques));
            }

            public TechniqueUnlockDriver Driver(IReadOnlyList<TechniqueUnlockRule> rules)
                => new TechniqueUnlockDriver(Stream, Decoder, rules, Writer, PlayerActorId);

            public void Append<T>(EventKind kind, PatientId patient, long tick, in T payload)
                where T : struct
                => Stream.Append(new SimEvent(tick, patient, -1, kind,
                                              Encoder.Encode(kind, payload)));

            public List<TechniqueUnlockedPayload> Unlocks()
            {
                var result = new List<TechniqueUnlockedPayload>();
                for (int i = 0; i < Stream.Events.Count; i++)
                {
                    SimEvent e = Stream.Events[i];
                    if (e.Kind != EventKind.TechniqueUnlocked) continue;
                    result.Add(Decoder.Decode<TechniqueUnlockedPayload>(
                        EventKind.TechniqueUnlocked, e.Payload));
                }
                return result;
            }

            public int UnlockEventCount() => Unlocks().Count;
        }

        private static readonly int[] KnownTechniques = { 0, 1, 2, 3, 10, 11, 12 };

        // ════════════════════════════════════════════════════════════════
        //  ⑥ 零规则 ⇒ 零扫描
        // ════════════════════════════════════════════════════════════════

        /// <summary>空规则表 ⇒ 返回 0 且流长不变(今日生产形态:接了、跑了、没有内容)。</summary>
        [Test]
        public void test_emptyRules_writesNothing()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 3, newState: 2));
            int before = h.Stream.Events.Count;

            var driver = h.Driver(Array.Empty<TechniqueUnlockRule>());
            int written = driver.OnTickEdge(10);

            Assert.That(written, Is.EqualTo(0));
            Assert.That(h.Stream.Events.Count, Is.EqualTo(before), "空规则表不得写任何事件");
            Assert.That(driver.RuleCount, Is.EqualTo(0));
        }

        /// <summary>生产规则表今日为空 —— 本件交机制,不交内容(内容归内容轮)。</summary>
        [Test]
        public void test_productionRules_threeEventPlusTwoCase()
        {
            // 🔁 2026-11-10:原条断言「生产规则表今日必须为空」。
            //    当日先落 A5 的三条 **Event 源**规则,再补两条 **Case 源**规则
            //    (P0 里唯一真的会触发的源,见 `TechniqueUnlockRules.Default` 的 remarks)
            //    ⇒ 本条改为钉住**两个源各自的成员**(而不是「非空」这种弱断言)。
            Assert.That(TechniqueUnlockRules.Default.Count, Is.EqualTo(6), "三条 Event + 三条 Case");
            Assert.That(TechniqueUnlockRules.Default.Select(r => r.SourceKind).Distinct().ToArray(),
                Is.EquivalentTo(new[] { TechniqueUnlockSourceKind.Event, TechniqueUnlockSourceKind.Case }),
                "P0 不用 Poi 源 —— `world_poi.json` 没有状态词表,填 Poi 规则必悬空");

            TechniqueUnlockRule[] ev = TechniqueUnlockRules.Default
                .Where(r => r.SourceKind == TechniqueUnlockSourceKind.Event).ToArray();
            Assert.That(ev.Select(r => r.SourceId).ToArray(), Is.EqualTo(new[] { 2002, 2003, 2004 }),
                "「打摆子」/「热性惊厥」/「溃兵军医」");
            Assert.That(ev.Select(r => r.TechniqueId).ToArray(), Is.EqualTo(new[] { 12, 1, 11 }),
                "金鸡纳皮 / 节奏型通气 / 毛地黄");

            TechniqueUnlockRule[] cs = TechniqueUnlockRules.Default
                .Where(r => r.SourceKind == TechniqueUnlockSourceKind.Case).ToArray();
            Assert.That(cs.Select(r => r.SourceId).ToArray(), Is.EqualTo(new[] { 2, 6, 17 }),
                "INJ_STAB(刀伤)/ INJ_BITE(咬伤)/ DIS_FEBRILE_SEIZURE(惊厥)");
            Assert.That(cs.Select(r => r.TechniqueId).ToArray(), Is.EqualTo(new[] { 2, 3, 10 }),
                "清创缝合 / 蛇伤处置 / 柳树皮");
        }

        // ════════════════════════════════════════════════════════════════
        //  ① + ② POI 支
        // ════════════════════════════════════════════════════════════════

        /// <summary>POI 到达指定状态 ⇒ 解锁,且载荷三字段全对得上。</summary>
        [Test]
        public void test_poiReachesRequiredState_unlocks()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 3, newState: 2));

            int written = h.Driver(new[] { TechniqueUnlockRule.ForPoi(poiId: 3, requiredState: 2, techniqueId: 2) })
                           .OnTickEdge(tick: 10);

            Assert.That(written, Is.EqualTo(1));
            List<TechniqueUnlockedPayload> rows = h.Unlocks();
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].ActorId, Is.EqualTo(PlayerActorId), "解锁跟人走(ADR-043 §D7)");
            Assert.That(rows[0].TechniqueId, Is.EqualTo(2));
            Assert.That(rows[0].SourceId, Is.EqualTo(3), "sourceId 必须是 poi_id 本身(一条事实一个来源)");
        }

        /// <summary>POI 状态**不符** ⇒ 不解锁(去掉状态约束就会解锁 ⇒ 判别力成立)。</summary>
        [Test]
        public void test_poiOtherState_doesNotUnlock()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 3, newState: 1));

            int written = h.Driver(new[] { TechniqueUnlockRule.ForPoi(poiId: 3, requiredState: 2, techniqueId: 2) })
                           .OnTickEdge(tick: 10);

            Assert.That(written, Is.EqualTo(0), "只按 poi_id 匹配会让「进去转一圈」也算解锁");
            Assert.That(h.UnlockEventCount(), Is.EqualTo(0));
        }

        /// <summary>另一个 POI 的同一状态 ⇒ 不解锁(源 id 也必须匹配)。</summary>
        [Test]
        public void test_poiOtherId_doesNotUnlock()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 4, newState: 2));

            Assert.That(h.Driver(new[] { TechniqueUnlockRule.ForPoi(3, 2, 2) }).OnTickEdge(10),
                        Is.EqualTo(0));
        }

        // ════════════════════════════════════════════════════════════════
        //  ① + ② 52 奇遇支
        // ════════════════════════════════════════════════════════════════

        /// <summary>奇遇档位 ≥ minTier ⇒ 解锁。</summary>
        [Test]
        public void test_eventAtOrAboveMinTier_unlocks()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.EventArrived, PatientId.None, 20,
                     new EventArrivedPayload(eventKey: 6, tier: 2, spawnAnchor: new WorldPos(1, 0, 1),
                                             causeClueKey: -1));

            int written = h.Driver(new[] { TechniqueUnlockRule.ForEvent(eventKey: 6, minTier: 2, techniqueId: 3) })
                           .OnTickEdge(20);

            Assert.That(written, Is.EqualTo(1));
            Assert.That(h.Unlocks()[0].SourceId, Is.EqualTo(6));
        }

        /// <summary>奇遇档位**低于** minTier ⇒ 不解锁(用「≥」不用「=」:更强的同款奇遇不该反而解锁不了)。</summary>
        [Test]
        public void test_eventBelowMinTier_doesNotUnlock()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.EventArrived, PatientId.None, 20,
                     new EventArrivedPayload(eventKey: 6, tier: 1, spawnAnchor: new WorldPos(1, 0, 1),
                                             causeClueKey: -1));

            Assert.That(h.Driver(new[] { TechniqueUnlockRule.ForEvent(6, minTier: 2, techniqueId: 3) })
                        .OnTickEdge(20), Is.EqualTo(0));
        }

        /// <summary>minTier = -1 ⇒ 任意档都解锁(哨兵语义,不是「无匹配」)。</summary>
        [Test]
        public void test_eventMinTierSentinel_acceptsAnyTier()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.EventArrived, PatientId.None, 20,
                     new EventArrivedPayload(6, tier: 0, new WorldPos(0, 0, 0), causeClueKey: -1));

            Assert.That(h.Driver(new[] { TechniqueUnlockRule.ForEvent(6, minTier: -1, techniqueId: 3) })
                        .OnTickEdge(20), Is.EqualTo(1));
        }

        // ════════════════════════════════════════════════════════════════
        //  ① + ② 病例支
        // ════════════════════════════════════════════════════════════════

        private static CaseClosedPayload Closed(bool treated, params int[] diseaseIds)
        {
            ulong bits = 0UL;
            foreach (int d in diseaseIds) bits |= 1UL << d;
            return new CaseClosedPayload(patientId: 5, caseId: new CaseId(30, 5, 0),
                                         diseaseSet: new DiseaseIdSet(bits), treated: treated);
        }

        /// <summary>已处置的结案里含该病种 ⇒ 解锁。</summary>
        [Test]
        public void test_caseTreated_unlocks()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.CaseClosed, new PatientId(5), 30, Closed(treated: true, diseaseIds: 4));

            int written = h.Driver(new[] { TechniqueUnlockRule.ForCase(diseaseId: 4, techniqueId: 2) })
                           .OnTickEdge(30);

            Assert.That(written, Is.EqualTo(1));
            Assert.That(h.Unlocks()[0].SourceId, Is.EqualTo(4), "sourceId = disease_id");
        }

        /// <summary>
        /// **未处置**的结案 ⇒ 不解锁。
        /// <para>判别力:把驱动里的 <c>if (!p.Treated) break;</c> 删掉 ⇒ 本条红。
        /// 该约束刻意写死在驱动而不是做成规则字段 —— 做成字段就有「谁能设成 false」的分支。</para>
        /// </summary>
        [Test]
        public void test_caseUntreated_doesNotUnlock()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.CaseClosed, new PatientId(5), 30, Closed(treated: false, diseaseIds: 4));

            Assert.That(h.Driver(new[] { TechniqueUnlockRule.ForCase(4, 2) }).OnTickEdge(30),
                        Is.EqualTo(0), "从没治过的病例里学不到技术");
        }

        /// <summary>结案病种集合**不含**该病种 ⇒ 不解锁(位集判据真的在按位查)。</summary>
        [Test]
        public void test_caseDiseaseNotInSet_doesNotUnlock()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.CaseClosed, new PatientId(5), 30,
                     Closed(treated: true, diseaseIds: new[] { 5, 9 }));

            Assert.That(h.Driver(new[] { TechniqueUnlockRule.ForCase(4, 2) }).OnTickEdge(30),
                        Is.EqualTo(0), "病种 5 / 9 在集合里,4 不在 —— 判据必须是按位查,不是「非空」");
        }

        /// <summary>
        /// 病种 id ≥ 64 ⇒ 位集**表达不了**,判为不命中。
        /// <para>若把越界当命中,一条写错的规则会无条件触发(最坏形态:全表误开)。</para>
        /// </summary>
        [Test]
        public void test_caseDiseaseIdAtOrAbove64_neverMatches()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.CaseClosed, new PatientId(5), 30, Closed(treated: true, diseaseIds: 0));

            Assert.That(h.Driver(new[] { TechniqueUnlockRule.ForCase(diseaseId: 64, techniqueId: 2) })
                        .OnTickEdge(30), Is.EqualTo(0));
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ 幂等
        // ════════════════════════════════════════════════════════════════

        /// <summary>同一份流扫两遍 ⇒ 第二遍零写入(去重在写者里,驱动不另写一份)。</summary>
        [Test]
        public void test_secondScan_isIdempotent()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 3, newState: 2));
            TechniqueUnlockDriver driver =
                h.Driver(new[] { TechniqueUnlockRule.ForPoi(3, 2, 2) });

            Assert.That(driver.OnTickEdge(10), Is.EqualTo(1));
            int afterFirst = h.Stream.Events.Count;

            Assert.That(driver.OnTickEdge(11), Is.EqualTo(0), "第二遍不该再写");
            Assert.That(h.Stream.Events.Count, Is.EqualTo(afterFirst),
                "流长不得增长 —— 增长 = 写了重复的解锁行,D6 被绕过");
        }

        /// <summary>两条规则指向同一技法 ⇒ 仍只有一条事件(D6 的跨规则去重)。</summary>
        [Test]
        public void test_twoRulesSameTechnique_writeOnce()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 3, newState: 2));
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 4, newState: 2));

            int written = h.Driver(new[]
            {
                TechniqueUnlockRule.ForPoi(3, 2, 2),
                TechniqueUnlockRule.ForPoi(4, 2, 2),
            }).OnTickEdge(10);

            Assert.That(written, Is.EqualTo(1));
            Assert.That(h.UnlockEventCount(), Is.EqualTo(1));
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ 写入 tick = 当前 tick
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 源事件落在 tick 5、驱动在 tick 100 跑 ⇒ 写出的事件 tick = **100**。
        /// <para>判别力:若照抄源事件的 tick(5),`EventStream.Append` 会因 Tick 回退抛
        /// (BCD-码-3)—— 本条会变成异常而不是断言失败,同样红,但归因写在注释里。</para>
        /// <para>现实触发 = 读档:整条历史被扫一遍,源事件全在过去。</para>
        /// </summary>
        [Test]
        public void test_writtenTick_isCurrentTickNotSourceTick()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 5,
                     new PoiStateChangedPayload(poiId: 3, newState: 2));
            h.Append(EventKind.ActorCellEntered, PatientId.None, 90,
                     new ActorCellEnteredPayload(actorId: PlayerActorId,
                                                 cell: new WorldPos(1, 0, 1), tick: 90));

            h.Driver(new[] { TechniqueUnlockRule.ForPoi(3, 2, 2) }).OnTickEdge(tick: 100);

            SimEvent unlock = h.Stream.Events[h.Stream.Events.Count - 1];
            Assert.That(unlock.Kind, Is.EqualTo(EventKind.TechniqueUnlocked));
            Assert.That(unlock.Tick, Is.EqualTo(100),
                "写入须用当前 tick;照抄源事件 tick 会让读档重放时 Tick 回退(BCD-码-3)");
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ 技法 id 闭集
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 生产规则表里的每个 techniqueId 都在处置轴 action id 闭集内。
        /// <para>⚠️ 今日规则表为空 ⇒ 本条**空真**。故紧接一条负控
        /// (<see cref="test_closedSetPredicate_rejectsOutOfSetId"/>)喂越界规则,证明
        /// 谓词本身有判别力 —— 否则这就是 F4(空洞正控)。</para>
        /// </summary>
        [Test]
        public void test_productionRules_techniqueIdsInAxisClosedSet()
        {
            var axis = new TutorialTechniqueIdSource(RepoRoot());
            Assert.That(axis.TryGetTechniqueIds(out IReadOnlyCollection<int> ids), Is.True,
                "读不到处置轴 ⇒ 本条无主语,不得当作通过");

            Assert.That(AllTechniqueIdsInAxis(TechniqueUnlockRules.Default, ids), Is.True,
                "生产规则表里有技法 id 不在处置轴(aspect: 会得到「解锁了但没有这个动作」的死行)");
        }

        /// <summary>同一谓词的负控:越界 id ⇒ false(证明上一条不是恒真)。</summary>
        [Test]
        public void test_closedSetPredicate_rejectsOutOfSetId()
        {
            var axis = new TutorialTechniqueIdSource(RepoRoot());
            Assert.That(axis.TryGetTechniqueIds(out IReadOnlyCollection<int> ids), Is.True);

            Assert.That(AllTechniqueIdsInAxis(Array.Empty<TechniqueUnlockRule>(), ids), Is.True,
                "空集恒真 —— 这正是上一条今日为空真的证明,写在这里避免被读成「已验证」");

            Assert.That(
                AllTechniqueIdsInAxis(new[] { TechniqueUnlockRule.ForCase(4, techniqueId: 99) }, ids),
                Is.False, "99 不在 {0,1,2,3,10,11,12} 里 ⇒ 谓词必须报 false");
        }

        private static bool AllTechniqueIdsInAxis(IReadOnlyList<TechniqueUnlockRule> rules,
                                                  IReadOnlyCollection<int> axisIds)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                bool found = false;
                foreach (int id in axisIds)
                    if (id == rules[i].TechniqueId) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        // ════════════════════════════════════════════════════════════════
        //  构造门 / 工厂门
        // ════════════════════════════════════════════════════════════════

        /// <summary>负 actorId ⇒ 抛(-1 是 PatientId.None,会写出世界级解锁)。</summary>
        [Test]
        public void test_negativeActorId_throws()
        {
            var h = new Harness(KnownTechniques);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new TechniqueUnlockDriver(h.Stream, h.Decoder,
                                                Array.Empty<TechniqueUnlockRule>(), h.Writer, -1));
        }

        /// <summary>三个工厂都拒收负 id(负值会在流里变成永远查不到的死数据)。</summary>
        [Test]
        public void test_ruleFactories_rejectNegativeIds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForPoi(-1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForPoi(0, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForPoi(0, 0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForEvent(-1, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForEvent(0, -1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForCase(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => TechniqueUnlockRule.ForCase(0, -1));
        }

        /// <summary>minTier 只允许 -1 或非负;更小的负值是「无意义约束」⇒ 拒收。</summary>
        [Test]
        public void test_forEvent_rejectsMinTierBelowSentinel()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => TechniqueUnlockRule.ForEvent(eventKey: 0, minTier: -2, techniqueId: 0));
            Assert.That(ex.ParamName, Is.EqualTo("minTier"));
        }

        /// <summary>
        /// 闭集门在**写者侧**兜底:规则里塞了越界技法 id ⇒ 首次命中时 fail-loud。
        /// <para>本条证明「规则表写错」不会静默产出死行 —— 尽管真正的发现点应是
        /// <see cref="test_productionRules_techniqueIdsInAxisClosedSet"/>。</para>
        /// </summary>
        [Test]
        public void test_outOfAxisTechnique_failsLoudAtWrite()
        {
            var h = new Harness(KnownTechniques);
            h.Append(EventKind.PoiStateChanged, PatientId.None, 10,
                     new PoiStateChangedPayload(poiId: 3, newState: 2));

            Assert.Throws<ArgumentException>(
                () => h.Driver(new[] { TechniqueUnlockRule.ForPoi(3, 2, techniqueId: 99) }).OnTickEdge(10));
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助:仓根 / 最小桩
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

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        private sealed class AbsentPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => false;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
