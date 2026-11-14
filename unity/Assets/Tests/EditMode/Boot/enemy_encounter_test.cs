// 27 遭遇实体 + 最小战斗闭环的判据(2026-11-14)
//
// 权威:
//   `design/registry/entities.yaml` —— EncounterStarted(world/52)·
//     EncounterEnded(world/27)· EnemyInjuryOnset(world/25)· InjuryStateChanged(world/9)
//   `design/gdd/combat-and-weapon-lines.md` F-25-1 / F-25-2(判定 → magnitude)
//   `design/gdd/enemy-ai.md` 规则二十、二十一(生命周期 / Ended ≠ 实体消失)
//   ADR-016 §二(敌人复用 9 的伤情模型;id 经 `IIdAuthority` 与病人共空间)
//   ADR-009 §七(意图事件 + 主机当下判距)
//   `design/levels/prologue-beat-script.md` §3.1(PA-04 兵痞 1 / PA-05 野狗 2)
//
// ── 读作者态真表的具名声明(`.claude/rules/test-standards.md` 要求)──────────
//   `test_combat_injuryIdMatchesRegistry` 读 `assets/data/disease_registry.json`
//   (经 `MedConsRepoPaths.RepoRoot`)。**必须读真表**:判据是「`CombatDriver.InjuryBlunt`
//   这个字面量与注册表 id 15 的 key 是同一件东西」—— 内联一份期望值会退化成
//   「测试和自己比」,注册表改号时两边一起动,红不了。
//
// ⚠️ 零写盘 / 零随机 / 零墙钟。
// ⚠️ 本件钉的是**机制**,不证明「手按 E 在真关卡里能打完」—— 那需要真启动序走查,
//    登记在 `TD-building-07` 的收口段。

using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using DaYiJingCheng.Tests.MedConsequences;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>遭遇写者 / 花名册投影 / 战斗驱动 / 击败型奇遇的机制判据。</summary>
    public class enemy_encounter_test
    {
        private static readonly WorldPos Cell = new WorldPos(23, 0, 7);
        private static readonly WorldPos FarAway = new WorldPos(200, 0, 200);

        private EventStream _stream;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EncounterStartWriter _starts;
        private EncounterEndWriter _ends;
        private EnemyInjuryWriter _injuries;
        private InjuryStateChangedWriter _states;
        private AdventureWriter _adventureWriter;

        private WorldPos _playerCell;
        private long _now;

        [SetUp]
        public void SetUp()
        {
            _stream = new EventStream(new CountingIdAuthority(), new PresenceRegistry());
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _starts = new EncounterStartWriter(_stream, _encoder, () => _stream.Events, _decoder);
            _ends = new EncounterEndWriter(_stream, _encoder, () => _stream.Events, _decoder);
            _injuries = new EnemyInjuryWriter(_stream, _encoder, () => _stream.Events, _decoder);
            _states = new InjuryStateChangedWriter(_stream, _encoder);
            _adventureWriter = new AdventureWriter(_stream, _encoder, () => _stream.Events, _decoder,
                                                   TestDropWriter.Create(_stream, _pool));
            _playerCell = Cell;
            _now = 1;   // 遭遇在 tick 1 开启 ⇒ 首击不得早于它(BCD-码-3)
        }

        // ── 写者:去重 / 发号 / 入参 ──────────────────────────────────────

        [Test]
        public void test_startWriter_writesOnce_andDeduplicatesByEncounterId()
        {
            Assert.AreEqual(EncounterStartResult.Applied,
                _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 101 }, tick: 1));
            Assert.AreEqual(EncounterStartResult.AlreadyStarted,
                _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 999 }, tick: 2),
                "同 encounter_id 重复开 = no-op(读档重放 / 驱动重试都走这条)");

            Assert.AreEqual(1, Count(EventKind.EncounterStarted), "遭遇开启恰一条");

            EncounterStartedPayload p = Payload<EncounterStartedPayload>(First(EventKind.EncounterStarted));
            Assert.AreEqual(1, p.EncounterId);
            Assert.AreEqual(EnemyPrototypes.Thug, p.ProtoId);
            Assert.AreEqual(Cell, p.SpawnCell);
            CollectionAssert.AreEqual(new[] { 101 }, p.ActorIds);
        }

        [Test]
        public void test_startWriter_patientHeaderCarriesMaxActorId()
        {
            // ADR-006 Amendment B 的高水位只扫 `SimEvent.Patient`;一支遭遇带 N 个 actor
            // 而事件头只有一个 Patient 位 ⇒ 必须取 max,否则读档后小号会被重发。
            Assert.AreEqual(EncounterStartResult.Applied,
                _starts.TryStart(7, EnemyPrototypes.WildDog, Cell, new[] { 301, 305, 302 }, tick: 1));

            Assert.AreEqual(305, First(EventKind.EncounterStarted).Patient.Value,
                "Patient 头 = 成员 id 的最大值(不是第一个)");
        }

        [Test]
        public void test_startWriter_rejectsInvalidArguments_withZeroWrites()
        {
            Assert.AreEqual(EncounterStartResult.RejectInvalidArgument,
                _starts.TryStart(1, 0, Cell, new int[0], tick: 1), "空成员表");
            Assert.AreEqual(EncounterStartResult.RejectInvalidArgument,
                _starts.TryStart(1, 0, Cell, new[] { -1 }, tick: 1), "负 actor id");
            Assert.AreEqual(EncounterStartResult.RejectInvalidArgument,
                _starts.TryStart(-1, 0, Cell, new[] { 1 }, tick: 1), "负遭遇 id");
            Assert.AreEqual(EncounterStartResult.RejectInvalidArgument,
                _starts.TryStart(1, 0, Cell, new[] { 1 }, tick: -1), "负 tick");

            Assert.AreEqual(0, Count(EventKind.EncounterStarted), "四条非法入参一律零写入");
        }

        [Test]
        public void test_endWriter_deduplicates_andRejectsOutOfRangeReason()
        {
            Assert.AreEqual(EncounterEndResult.Applied,
                _ends.TryEnd(1, EncounterEndReasonKind.AllDowned, tick: 5));
            Assert.AreEqual(EncounterEndResult.AlreadyEnded,
                _ends.TryEnd(1, EncounterEndReasonKind.Disengaged, tick: 6),
                "一支遭遇至多一条 End(GDD 规则二十一)");

            Assert.AreEqual(EncounterEndResult.RejectInvalidArgument,
                _ends.TryEnd(2, (EncounterEndReasonKind)3, tick: 7), "reason 闭集 {0,1,2}");

            Assert.AreEqual(1, Count(EventKind.EncounterEnded));
            EncounterEndedPayload p = Payload<EncounterEndedPayload>(First(EventKind.EncounterEnded));
            Assert.AreEqual(1, p.EncounterId);
            Assert.AreEqual(EnemyRoster.ReasonAllDowned, p.Reason, "载荷 reason 与花名册判据同值");
        }

        [Test]
        public void test_injuryWriter_doseSeqCountsPerTargetPerTick()
        {
            Fix mag = new Fix(1000);
            _injuries.TryWrite(actorId: 1, targetId: 201, injuryId: 15, mag, tick: 3);
            _injuries.TryWrite(actorId: 1, targetId: 201, injuryId: 15, mag, tick: 3);
            _injuries.TryWrite(actorId: 1, targetId: 202, injuryId: 15, mag, tick: 3);
            _injuries.TryWrite(actorId: 1, targetId: 201, injuryId: 15, mag, tick: 4);

            int[] seqs = new int[4];
            int n = 0;
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == EventKind.EnemyInjuryOnset)
                    seqs[n++] = Payload<EnemyInjuryOnsetPayload>(events[i]).DoseSeq;

            Assert.AreEqual(4, n);
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 0 }, seqs,
                "dose_seq 按 (target, tick) 计数 —— 不同目标 / 不同 tick 各自从 0 起(F-25-7)");
        }

        [Test]
        public void test_injuryWriter_rejectsNonPositiveMagnitude()
        {
            Assert.AreEqual(EnemyInjuryWriteResult.RejectInvalidArgument,
                _injuries.TryWrite(1, 201, 15, Fix.Zero, tick: 1), "0 伤量不是一次命中");
            Assert.AreEqual(0, Count(EventKind.EnemyInjuryOnset));
        }

        // ── 花名册投影 ───────────────────────────────────────────────────

        [Test]
        public void test_roster_projectsMembers_andAccumulatesHurt()
        {
            _starts.TryStart(1, EnemyPrototypes.WildDog, Cell, new[] { 201, 202 }, tick: 1);
            _injuries.TryWrite(1, 202, 15, new Fix(1000), tick: 2);
            _injuries.TryWrite(1, 202, 15, new Fix(500), tick: 3);

            IReadOnlyList<EnemyActor> rows = EnemyRoster.Build(_stream.Events, _decoder);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(201, rows[0].ActorId, "按 ActorId 升序");
            Assert.AreEqual(202, rows[1].ActorId);
            Assert.AreEqual(EnemyPrototypes.WildDog, rows[1].ProtoId);
            Assert.AreEqual(1, rows[1].EncounterId);
            Assert.AreEqual(Cell, rows[1].Cell, "出生格来自 EncounterStarted(敌人格不进流)");
            Assert.AreEqual(1500L, rows[1].Hurt.Raw, "Σ magnitude");
            Assert.AreEqual(0L, rows[0].Hurt.Raw);
            Assert.IsFalse(rows[0].Downed);
            Assert.AreEqual(2, EnemyRoster.Alive(rows).Count);
        }

        [Test]
        public void test_roster_marksAllMembersDowned_whenEncounterEndedAllDowned()
        {
            // `EncounterEnded(AllDowned)` 是收场事实;成员若因某条状态转移漏写,
            // 花名册仍须把整支判为倒地(防「收场了但还有人站着」的静默不一致)。
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 401, 402 }, tick: 1);
            _ends.TryEnd(1, EncounterEndReasonKind.AllDowned, tick: 9);

            IReadOnlyList<EnemyActor> rows = EnemyRoster.Build(_stream.Events, _decoder);
            Assert.IsTrue(rows[0].Downed);
            Assert.IsTrue(rows[1].Downed);
            Assert.AreEqual(0, EnemyRoster.Alive(rows).Count);
        }

        // ── 战斗闭环 ─────────────────────────────────────────────────────

        [Test]
        public void test_combat_noTargetInRange_writesNothing()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 201 }, tick: 1);
            _playerCell = FarAway;

            CombatDriver combat = MakeCombat();
            Assert.AreEqual(CombatAttackResult.NoTarget, combat.Attack());
            Assert.AreEqual(0, Count(EventKind.EnemyInjuryOnset), "手长外无目标 ⇒ 零事件");
        }

        [Test]
        public void test_combat_cooldownSuppressesSecondHit()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 201 }, tick: 1);
            CombatDriver combat = MakeCombat(cooldownTicks: 10);

            _now = 1;
            Assert.AreEqual(CombatAttackResult.Hit, combat.Attack());
            _now = 6;
            Assert.AreEqual(CombatAttackResult.OnCooldown, combat.Attack(), "冷却未到 ⇒ 不出手");
            _now = 11;
            Assert.AreEqual(CombatAttackResult.Hit, combat.Attack());

            Assert.AreEqual(2, Count(EventKind.EnemyInjuryOnset), "被抑制的那一次零写入");
        }

        [Test]
        public void test_combat_downsAfterNineHits_andEndsEncounter()
        {
            // 算账(GDD F-25-2 · CP 今日 = 0 ⇒ magnitude = MAG_FLOOR = 1/20):
            //   3277 raw/击;COMA = 29491 raw ⇒ 8 击 = 26216 < 29491,9 击 = 29493 ≥ 29491。
            Assert.AreEqual(3277L, CombatMagnitude.MAG_FLOOR, "MAG_FLOOR = 1/20(Q16.16 raw)");

            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 201 }, tick: 1);
            CombatDriver combat = MakeCombat(cooldownTicks: 0);

            for (int i = 0; i < 8; i++)
            {
                _now = 1 + i;
                Assert.AreEqual(CombatAttackResult.Hit, combat.Attack(), $"第 {i + 1} 击");
            }
            Assert.AreEqual(0, Count(EventKind.InjuryStateChanged), "8 击未越昏迷阈");
            Assert.AreEqual(0, Count(EventKind.EncounterEnded));

            _now = 9;
            Assert.AreEqual(CombatAttackResult.Hit, combat.Attack(), "第 9 击越阈");

            Assert.AreEqual(1, Count(EventKind.InjuryStateChanged), "倒地转移恰一条(不是每击一条)");
            InjuryStateChangedPayload st = Payload<InjuryStateChangedPayload>(
                First(EventKind.InjuryStateChanged));
            Assert.AreEqual(201, st.ActorId);
            Assert.AreEqual((int)InjuryState.Coma, st.NewState, "生命归零 = INJ_COMA(ADR-016 §二)");

            Assert.AreEqual(1, Count(EventKind.EncounterEnded), "成员全倒地 ⇒ 收场恰一条");
            Assert.AreEqual(EnemyRoster.ReasonAllDowned,
                Payload<EncounterEndedPayload>(First(EventKind.EncounterEnded)).Reason);
        }

        [Test]
        public void test_combat_neverWritesDeceased()
        {
            // 25 §三:玩家的攻击不可能产生死亡状态 —— 打 20 击也只到昏迷。
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 201 }, tick: 1);
            CombatDriver combat = MakeCombat(cooldownTicks: 0);
            for (int i = 0; i < 20; i++)
            {
                _now = 1 + i;
                combat.Attack();
            }

            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != EventKind.InjuryStateChanged) continue;
                Assert.AreNotEqual((int)InjuryState.Deceased,
                    Payload<InjuryStateChangedPayload>(events[i]).NewState,
                    "任何一条状态转移都不得是 Deceased");
            }
            Assert.AreEqual(1, Count(EventKind.EncounterEnded), "倒地后目标不再可打 ⇒ 不重复收场");
        }

        // ── 编排驱动 ─────────────────────────────────────────────────────

        [Test]
        public void test_prologueDriver_firesOnceInsideRect_andMintsDistinctIds()
        {
            var ids = new CountingIdAuthority(start: 100);
            var drv = new PrologueEncounterDriver(_starts, ids,
                PrologueEncounterDriver.PrologueTwo, () => Cell);

            drv.OnTickEdge(1);
            drv.OnTickEdge(2);   // 还在矩形里 —— 闩锁必须挡住

            Assert.AreEqual(1, Count(EventKind.EncounterStarted), "一支遭遇一局只开一次");
            CollectionAssert.AreEqual(new[] { 1 }, drv.StartedIds);
            CollectionAssert.AreEqual(new[] { 100 },
                Payload<EncounterStartedPayload>(First(EventKind.EncounterStarted)).ActorIds,
                "敌人 id 经 IIdAuthority 铸造");
        }

        [Test]
        public void test_prologueDriver_doesNothingOutsideEveryRect()
        {
            var drv = new PrologueEncounterDriver(_starts, new CountingIdAuthority(),
                PrologueEncounterDriver.PrologueTwo, () => FarAway);

            for (int t = 1; t <= 3; t++) drv.OnTickEdge(t);

            Assert.AreEqual(0, Count(EventKind.EncounterStarted));
            Assert.AreEqual(0, drv.StartedIds.Count);
        }

        [Test]
        public void test_prologueDriver_reloadDoesNotDuplicateEncounter()
        {
            // 读档:tick 回退 ⇒ 闩锁清空(驱动不持持久状态);重复由**写者**的
            // encounter_id 去重兜住 —— 这是「闩锁是缓存、去重是真判据」的现场。
            var drv1 = new PrologueEncounterDriver(_starts, new CountingIdAuthority(),
                PrologueEncounterDriver.PrologueTwo, () => Cell);
            drv1.OnTickEdge(50);
            Assert.AreEqual(1, Count(EventKind.EncounterStarted));

            var drv2 = new PrologueEncounterDriver(_starts, new CountingIdAuthority(),
                PrologueEncounterDriver.PrologueTwo, () => Cell);
            drv2.OnTickEdge(1);   // 时间倒流

            Assert.AreEqual(1, Count(EventKind.EncounterStarted), "重新开出的尝试被写者去重挡下");
        }

        // ── 击败型奇遇(PA-04 / PA-05)────────────────────────────────────

        [Test]
        public void test_adventure_completesAfterOneThugDowned()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 201 }, tick: 1);
            CombatDriver combat = MakeCombat(cooldownTicks: 0);
            for (int i = 0; i < 9; i++) { _now = 1 + i; combat.Attack(); }

            AdventureDriver adv = MakeAdventure();
            adv.OnTickEdge(20);

            Assert.AreEqual(1, adv.CountOf(3), "PA-04 击败兵痞 1 名");
            Assert.AreEqual(1, Count(EventKind.AdventureAccepted), "首次命中即接取");
            Assert.AreEqual(1, Count(EventKind.AdventureCompleted));
            Assert.AreEqual(1, Count(EventKind.DropSpawned), "完成时掉落组件恰一次");
        }

        [Test]
        public void test_adventure_wildDogCountsPerActor_notPerEncounter()
        {
            // 一支遭遇里两只野狗:倒地一只不够(要求 2),第二只倒地才完成。
            _starts.TryStart(2, EnemyPrototypes.WildDog, Cell, new[] { 301, 302 }, tick: 1);
            CombatDriver combat = MakeCombat(cooldownTicks: 0);
            AdventureDriver adv = MakeAdventure();

            for (int i = 0; i < 9; i++) { _now = 1 + i; combat.Attack(); }
            adv.OnTickEdge(10);   // 接取写在这里 ⇒ 后续出手 tick 必须 ≥ 10
            Assert.AreEqual(1, adv.CountOf(4), "第一只倒地");
            Assert.AreEqual(0, Count(EventKind.AdventureCompleted), "要求 2 ⇒ 还差一只");
            Assert.AreEqual(0, Count(EventKind.EncounterEnded), "同遭遇还有成员站着");

            for (int i = 9; i < 18; i++) { _now = 2 + i; combat.Attack(); }   // 11..19
            adv.OnTickEdge(40);
            Assert.AreEqual(2, adv.CountOf(4));
            Assert.AreEqual(1, Count(EventKind.AdventureCompleted));
            Assert.AreEqual(1, Count(EventKind.EncounterEnded), "两只都倒地 ⇒ 收场");
        }

        [Test]
        public void test_adventure_defeatDoesNotLeakIntoGatherAdventures()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 201 }, tick: 1);
            CombatDriver combat = MakeCombat(cooldownTicks: 0);
            for (int i = 0; i < 9; i++) { _now = 1 + i; combat.Attack(); }

            AdventureDriver adv = MakeAdventure();
            adv.OnTickEdge(20);

            Assert.AreEqual(0, adv.CountOf(0), "PA-01 是采集型,不受击败影响");
            Assert.AreEqual(0, adv.CountOf(1));
            Assert.AreEqual(0, adv.CountOf(2));
            Assert.AreEqual(0, adv.CountOf(4),
                "兵痞(proto 0)不得计入 PA-05(proto 1)—— 击败计数按原型分账");
        }

        // ── 注册表绑定 ───────────────────────────────────────────────────

        [Test]
        public void test_combat_injuryIdMatchesRegistry()
        {
            string path = Path.Combine(MedConsRepoPaths.RepoRoot, "assets", "data", "disease_registry.json");
            Assert.IsTrue(File.Exists(path), $"真表不存在: {path}");

            string text = File.ReadAllText(path);
            int id = CombatDriver.InjuryBlunt;
            string key = ExtractKey(text, id);

            Assert.AreEqual("INJ_BLUNT", key,
                $"CombatDriver.InjuryBlunt = {id} 必须指向注册表的 INJ_BLUNT 行 —— 改号只改常量会静默打错伤情");
        }

        // ── 辅助 ─────────────────────────────────────────────────────────

        /// <summary>从真表里取 `"id": <paramref name="id"/>` 那一行的 `"key"`(零依赖解析)。</summary>
        private static string ExtractKey(string json, int id)
        {
            string marker = "\"id\": " + id;
            int at = json.IndexOf(marker, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, $"注册表里没有 id = {id} 的行");
            int keyAt = json.IndexOf("\"key\":", at, System.StringComparison.Ordinal);
            Assert.Greater(keyAt, at, "该行没有 key 字段");
            int open = json.IndexOf('"', keyAt + 6);
            int close = json.IndexOf('"', open + 1);
            return json.Substring(open + 1, close - open - 1);
        }

        private CombatDriver MakeCombat(int cooldownTicks = 0)
            => new CombatDriver(_injuries, _states, _ends, () => _stream.Events, _decoder,
                                () => _playerCell, () => _now,
                                actorId: 1, range: 2, cooldownTicks: cooldownTicks);

        private AdventureDriver MakeAdventure()
            => new AdventureDriver(() => _stream.Events, AdventureSteps.PrologueFive,
                                   _adventureWriter, () => _playerCell, actorId: 1, _decoder);

        private T Payload<T>(SimEvent e) where T : struct
            => _decoder.Decode<T>(e.Kind, e.Payload);

        private int Count(EventKind kind)
        {
            int n = 0;
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == kind) n++;
            return n;
        }

        private SimEvent First(EventKind kind)
        {
            IReadOnlyList<SimEvent> events = _stream.Events;
            for (int i = 0; i < events.Count; i++)
                if (events[i].Kind == kind) return events[i];
            Assert.Fail($"流里没有 {kind}");
            return default;
        }

        /// <summary>递增发号器(病人 / 物品共用一个序列 —— 本件的敌人 id 也走 `NextPatientId`)。</summary>
        private sealed class CountingIdAuthority : IIdAuthority
        {
            private int _next;

            public CountingIdAuthority(int start = 100) => _next = start;

            public PatientId NextPatientId() => new PatientId(_next++);

            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }
    }
}
