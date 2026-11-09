// 病例投放资格(技法门硬 + 等级门软)—— 判据夹具(2026-11-09 · T4)
//
// 权威:design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.1 / §5.2(用户 2026-11-09 裁定)
//
// 本文件要证明:
//   ① **技法门是硬的** —— 声明了 N≥1 门技法且都没解锁 ⇒ 拒不投放(用户裁定的核心那条);
//   ② **∃ 语义** —— 多门技法里只要有**一门**解锁就投放(不是「全都得会」);
//   ③ **等级门是软的** —— `min_skill_level` 高于玩家等级 ⇒ 拒;等于 ⇒ 过;
//   ④ **数据未覆盖 ⇒ 放行并计数** —— 且 `IsUndeclared` 单独可读(它是要归零的观测面);
//   ⑤ **钩子病例** —— 技法和等级都没过也放行,且 `IsHookCase` 为真(T5 据此打标);
//   ⑥ **未接线 / 玩家未就绪** —— 两种「无从判定」各有一个具名判定,不当成「不可投放」;
//   ⑦ **两路出生共用同一判据** —— 驱动侧(按行)与 52 侧(按病种 id)真的拦得住。
//
// ⚠️ 本文件**只读**仓库文件(不写盘)⇒ 不触发 `FsWriteBoundaryGate`。
// ⚠️ 没有自指判据(F3):所有期望值都是字面量,不引用被测类的常量或方法名。

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
    /// <summary>T4:未解锁的病例不入选,未覆盖的不静默消失。</summary>
    [TestFixture]
    public sealed class PatientDrawEligibilityTest
    {
        // ── 构造件 ────────────────────────────────────────────────────────────

        private static DiseaseRegistryEntry Row(int id, string key, int minSkillLevel = 0)
            => new DiseaseRegistryEntry
            {
                DiseaseId = id,
                DiseaseKey = key,
                MinSkillLevel = minSkillLevel,
            };

        /// <summary>用 (病种 key, 技法 id) 对建索引 —— 与烘焙器产出的行同形。</summary>
        private static TechniqueDrawIndex Index(params (string Key, int Action)[] relations)
        {
            var rows = new List<TreatableByCookedRow>();
            foreach ((string key, int action) in relations)
                rows.Add(new TreatableByCookedRow(key, action, "symptomatic"));
            return new TechniqueDrawIndex(rows);
        }

        private static PatientDrawDecision Decide(
            DiseaseRegistryEntry row, TechniqueDrawIndex index,
            int level = 0, params int[] unlocked)
        {
            var set = new HashSet<int>(unlocked);
            return PatientDrawEligibility.Decide(
                row, index, level, techniqueId => set.Contains(techniqueId));
        }

        // ── ① 技法门 = 硬门 ───────────────────────────────────────────────────

        /// <summary>
        /// 声明了技法、一门都没解锁 ⇒ **拒收**。这是用户 2026-11-09 裁定
        /// 「未开启前不会出现相应病例」的唯一执行体。
        /// <para>反证:删掉 `if (!anyUnlocked) return NoUnlockedTechnique;`(或把该分支改成
        /// 放行)⇒ 本条红,且红在 `Admitted` 变成 true。</para>
        /// </summary>
        [Test]
        public void test_declaredTechniquesNoneUnlocked_isRejected()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X"), Index(("DIS_X", 12)));

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.NoUnlockedTechnique));
            Assert.That(d.Admitted, Is.False, "一门都不会 ⇒ 不得投放");
            Assert.That(d.DiseaseId, Is.EqualTo(5), "判定须回带病种 id(日志与夹具据此对账)");
        }

        // ── ② ∃ 语义 ──────────────────────────────────────────────────────────

        /// <summary>三门技法里只要**一门**解锁就投放(不是「全都会」)。</summary>
        [Test]
        public void test_anySingleUnlockedTechnique_admits()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X"), Index(("DIS_X", 10), ("DIS_X", 11), ("DIS_X", 12)),
                                           unlocked: new[] { 11 });

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.Admissible));
            Assert.That(d.Admitted, Is.True);
        }

        /// <summary>解锁的是**别人家的**技法 ⇒ 不投放(技法集必须属于该病种)。</summary>
        [Test]
        public void test_unlockedTechniqueOfAnotherDisease_doesNotAdmit()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X"), Index(("DIS_X", 12), ("DIS_Y", 10)),
                                           unlocked: new[] { 10 });

            Assert.That(d.Admitted, Is.False, "10 只声明给 DIS_Y,不得拿来投放 DIS_X");
        }

        // ── ③ 等级门 = 软门 ───────────────────────────────────────────────────

        /// <summary>`min_skill_level` 高于玩家等级 ⇒ 拒(即便技法已解锁)。</summary>
        [Test]
        public void test_minSkillLevelAbovePlayerLevel_isRejected()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X", minSkillLevel: 5),
                                           Index(("DIS_X", 12)), level: 4, unlocked: new[] { 12 });

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.LevelTooLow));
            Assert.That(d.Admitted, Is.False);
        }

        /// <summary>等级**等于**门槛 ⇒ 过(`≤` 不是 `<`;差一位是最常见的差一错误)。</summary>
        [Test]
        public void test_minSkillLevelEqualsPlayerLevel_admits()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X", minSkillLevel: 5),
                                           Index(("DIS_X", 12)), level: 5, unlocked: new[] { 12 });

            Assert.That(d.Admitted, Is.True, "门槛是 `≤` —— 等于须过");
        }

        /// <summary>两道门都不过时报**技法**原因 —— 归因须指向硬门(顺序是判据的一部分)。</summary>
        [Test]
        public void test_bothGatesFail_reportsTechniqueReason()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X", minSkillLevel: 9),
                                           Index(("DIS_X", 12)), level: 0);

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.NoUnlockedTechnique),
                "技法门是硬门,先判;归因指它,不指等级");
        }

        // ── ④ 数据未覆盖 ──────────────────────────────────────────────────────

        /// <summary>
        /// 轴里没有这个病种 ⇒ **放行**,且 `IsUndeclared` 可读。
        /// <para>理由见 `PatientDrawEligibility` 头注「三态而不是两态」:今日伤情 7 行
        /// 在 `treatable_by` 里零命中,若这里也拒,世界会一个病人都没有。</para>
        /// </summary>
        [Test]
        public void test_undeclaredDisease_admitsButIsCountable()
        {
            PatientDrawDecision d = Decide(Row(2, "INJ_STAB"), Index(("DIS_X", 12)));

            Assert.That(d.Admitted, Is.True);
            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.Undeclared));
            Assert.That(d.IsUndeclared, Is.True, "这是要归零的观测面(T7 补数据后应为 0)");
        }

        /// <summary>空 key(产物损坏形态)⇒ 不炸,按未覆盖处理。</summary>
        [Test]
        public void test_emptyDiseaseKey_treatedAsUndeclared()
        {
            PatientDrawDecision d = Decide(Row(2, ""), Index(("DIS_X", 12)));

            Assert.That(d.Admitted, Is.True);
            Assert.That(d.IsUndeclared, Is.True);
        }

        // ── ⑤ 钩子病例 ────────────────────────────────────────────────────────

        /// <summary>
        /// 钩子病例:技法没解锁、等级也不够 ⇒ **照样放行**,且 `IsHookCase` 为真。
        /// <para>反证:去掉 `if (asHookCase) return HookCase;` ⇒ 本条红在 `IsHookCase == false`。</para>
        /// </summary>
        [Test]
        public void test_hookCase_bypassesBothGatesAndIsMarked()
        {
            PatientDrawDecision d = PatientDrawEligibility.Decide(
                Row(9, "DIS_HOOK", minSkillLevel: 9), Index(("DIS_HOOK", 12)),
                playerPrescriptionLevel: 0, isTechniqueUnlocked: _ => false,
                playerBound: true, asHookCase: true);

            Assert.That(d.Admitted, Is.True, "钩子病例的目的是「看得懂、治不了、知道缺哪门」");
            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.HookCase));
            Assert.That(d.IsHookCase, Is.True, "调用方须能打标(T5 的计数与收场靠它)");
        }

        // ── ⑥ 无从判定 ────────────────────────────────────────────────────────

        /// <summary>`index == null`(装配层拿不到轴产物)⇒ `NotWired` 并放行。</summary>
        [Test]
        public void test_nullIndex_isNotWiredAndAdmits()
        {
            PatientDrawDecision d = Decide(Row(5, "DIS_X"), null);

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.NotWired));
            Assert.That(d.Admitted, Is.True, "未接线 ≠ 不可投放;两者混读会让装配缺失看起来像设计");
        }

        /// <summary>玩家 id 未绑定(启动期)⇒ `PlayerNotReady` 并放行。</summary>
        [Test]
        public void test_playerNotBound_isPlayerNotReadyAndAdmits()
        {
            PatientDrawDecision d = PatientDrawEligibility.Decide(
                Row(5, "DIS_X"), Index(("DIS_X", 12)), 0, _ => false, playerBound: false);

            Assert.That(d.Verdict, Is.EqualTo(PatientDrawVerdict.PlayerNotReady));
            Assert.That(d.Admitted, Is.True);
        }

        /// <summary>谓词为 null ⇒ 构造期就炸(装配漏传须可见,不得静默放行)。</summary>
        [Test]
        public void test_nullPredicate_failsLoud()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => PatientDrawEligibility.Decide(Row(5, "DIS_X"), Index(("DIS_X", 12)), 0, null));
        }

        // ── ⑦ 两路出生共用同一判据 ────────────────────────────────────────────

        private sealed class RecordingSink : IEventSink
        {
            public readonly List<SimEvent> Events = new List<SimEvent>();
            public void Append(in SimEvent e) => Events.Add(e);
        }

        /// <summary>驱动:首格被拒 ⇒ 轮到第二个;全表被拒 ⇒ 一拍都不产病人。</summary>
        [Test]
        public void test_driver_skipsRejectedDiseaseAndSpawnsAdmissibleOne()
        {
            var pool = new InMemoryBlobPool();
            var sink = new RecordingSink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, new PayloadEncoder(pool), 12345UL);
            var presence = new PresenceRegistry();

            DiseaseRegistryEntry blocked = Row(7, "DIS_LOCKED");
            DiseaseRegistryEntry open = Row(8, "INJ_OPEN");
            TechniqueDrawIndex index = Index(("DIS_LOCKED", 12));
            var unlocked = new HashSet<int>();

            var driver = new PatientAppearedDriver(
                spawner, presence, new[] { blocked, open },
                admit: row => PatientDrawEligibility.Decide(
                    row, index, 0, t => unlocked.Contains(t)).Admitted);

            driver.OnTickEdge(0);
            driver.OnTickEdge(60);

            // 两拍都投放**同一个可用户者**(上限哨是 2 人,被拒的那格永不补位)。
            Assert.That(driver.SpawnedCount, Is.EqualTo(2), "两拍各投一次,两拍都只能是 8 号");
            Assert.That(sink.Events.Count, Is.EqualTo(2));

            var decoder = new PayloadDecoder(pool);
            for (int i = 0; i < sink.Events.Count; i++)
            {
                DiseaseOnsetPayload p =
                    decoder.Decode<DiseaseOnsetPayload>(EventKind.DiseaseOnset, sink.Events[i].Payload);
                Assert.That(p.DiseaseId, Is.EqualTo(8), "被拒的 7 号**不得**出现在流的任何一条里");
            }
        }

        /// <summary>驱动:全表被拒 ⇒ 零病人、零事件(不抛、不写半条)。</summary>
        [Test]
        public void test_driver_noAdmissibleDisease_spawnsNobody()
        {
            var pool = new InMemoryBlobPool();
            var sink = new RecordingSink();
            var spawner = new PatientSpawner(new IdAuthority(), sink, new PayloadEncoder(pool), 12345UL);

            var driver = new PatientAppearedDriver(
                spawner, new PresenceRegistry(), new[] { Row(7, "DIS_LOCKED") },
                admit: row => PatientDrawEligibility.Decide(
                    row, Index(("DIS_LOCKED", 12)), 0, _ => false).Admitted);

            driver.OnTickEdge(0);
            driver.OnTickEdge(60);
            driver.OnTickEdge(120);

            Assert.That(driver.SpawnedCount, Is.EqualTo(0));
            Assert.That(sink.Events.Count, Is.EqualTo(0), "拒收不得留下半条事件");
        }

        /// <summary>52 路:`CanSpawnPatient` 的投放资格谓词真的会把该 key 判 false。</summary>
        [Test]
        public void test_eventPoolCanSpawnPatient_appliesAdmission()
        {
            // 两条医疗条目,唯一差别是病种外键(7 = 已声明且未解锁 / 8 = 未声明)。
            var entries = new List<EventPoolEntryData>
            {
                new EventPoolEntryData(2001, 100, 1, 0, 0, PoolHandler.Medical, 0, false, 7),
                new EventPoolEntryData(2002, 100, 1, 0, 0, PoolHandler.Medical, 0, false, 8),
            };
            var pool = new EventPoolDataSet(entries, 1, 1, 2, 1, 0u, 1u);

            TechniqueDrawIndex index = Index(("DIS_LOCKED", 12));
            var rowsById = new Dictionary<int, DiseaseRegistryEntry>
            {
                [7] = Row(7, "DIS_LOCKED"),
                [8] = Row(8, "INJ_OPEN"),
            };
            System.Func<int, bool> admit =
                id => PatientDrawEligibility.Decide(rowsById[id], index, 0, _ => false).Admitted;

            System.Func<int, bool> canSpawn = EventPoolPatientSpawner.CanSpawnPatient(pool, admit);

            Assert.That(canSpawn(2001), Is.False, "病种 7 有技法声明且未解锁 ⇒ 不得产病人");
            Assert.That(canSpawn(2002), Is.True, "病种 8 未声明 ⇒ 放行(数据未覆盖 ≠ 不可投放)");
        }
    }
}
