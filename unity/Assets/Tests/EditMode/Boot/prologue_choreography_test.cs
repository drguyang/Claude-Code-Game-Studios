// 第二段四拍的行为编排判据(2026-11-14 · N5 ❗「行为编排」)
//
// 权威:
//   `design/levels/prologue-beat-script.md` 第二段:P2-02 野狗扑孩子 / 孩子倒地 ·
//     P2-03 石头 → 狗松口 → 逃(判据 = 野狗离场)· P2-06 三个地痞 → 推你 1 格 ·
//     P2-09 狗带幼崽,看你一眼走开
//   ADR-016 §一(敌人格**不进流** —— 逐人格只活在编排里)· §二(复用 9 的伤情模型)
//   ADR-013 §9 C3(呈现层只渲染,不持游戏状态)
//   GDD enemy-ai.md 规则二十、二十一(遭遇生命周期;Ended ≠ 实体消失)
//
// ⚠️ 时间线全按 **tick**(20 Hz)推:渲染帧率不参与判定 —— 高帧率不得加快演出。
// ⚠️ 零写盘 / 零随机 / 零墙钟。
// ⚠️ 本件钉的是**时间线与落流**,不证明「手按 E 在真关卡里能看完四拍」——
//    那需要真启动序走查,登记在 `TD-building-07` 的收口段。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>第二段四拍的编排判据(野狗 / 石头 / 地痞 / 回程)。</summary>
    public class prologue_choreography_test
    {
        private static readonly WorldPos Spawn = new WorldPos(60, 0, 19);
        private static readonly WorldPos Drugshop = new WorldPos(23, 0, 7);
        private static readonly WorldPos FarAway = new WorldPos(200, 0, 200);

        private EventStream _stream;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;
        private EncounterStartWriter _starts;
        private EncounterEndWriter _ends;
        private EnemyInjuryWriter _injuries;

        private WorldPos _playerCell;
        private Vector3? _teleportedTo;
        private List<string> _lines;
        private bool _narrating;

        [SetUp]
        public void SetUp()
        {
            _stream = new EventStream(new CountingIdAuthority(), new PresenceRegistry());
            _pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _starts = new EncounterStartWriter(_stream, encoder, () => _stream.Events, _decoder);
            _ends = new EncounterEndWriter(_stream, encoder, () => _stream.Events, _decoder);
            _injuries = new EnemyInjuryWriter(_stream, encoder, () => _stream.Events, _decoder);

            _playerCell = Drugshop;
            _teleportedTo = null;
            _lines = new List<string>();
            _narrating = false;
        }

        // ── P2-02 / P2-03:野狗扑孩子 → 咬住 → 石头 → 逃 ────────────────────

        [Test]
        public void test_dogEncounter_lungesToChild_thenBites_andChildDowned()
        {
            _starts.TryStart(PrologueChoreography.DogEncounterId, EnemyPrototypes.WildDog,
                             Spawn, new[] { 501, 502 }, tick: 1);
            PrologueChoreography ch = Make();
            ch.SetChild(900);

            ch.OnTickEdge(1);
            Assert.IsTrue(ch.TryGetPose(501, out ScriptedEnemyPose start), "遭遇成员的位姿必须可由编排给出");
            Assert.AreEqual(Spawn.X, start.Position.x, 1e-3f, "起点 = 出生格");
            Assert.AreEqual(EnemyStateMachine.State.Chase, start.State, "扑击途中是 Chase");
            Assert.IsFalse(ch.IsChildDowned(900), "还没扑到,孩子站着");

            // 扑击途中:位置必须**连续**(不是到点瞬移)
            ch.OnTickEdge(1 + PrologueChoreography.LungeTicks / 2);
            Assert.IsTrue(ch.TryGetPose(501, out ScriptedEnemyPose mid));
            Assert.IsTrue(mid.Position.x < Spawn.X, "中途已经离开出生格(向西)");
            Assert.IsTrue(mid.Position.x > 45f, "中途还没到孩子身边");

            ch.OnTickEdge(1 + PrologueChoreography.LungeTicks);
            Assert.IsTrue(ch.TryGetPose(501, out ScriptedEnemyPose bite));
            Assert.AreEqual(45f, bite.Position.x, 1e-3f, "咬住 = 孩子北侧一格");
            Assert.AreEqual(21f, bite.Position.z, 1e-3f);
            Assert.AreEqual(EnemyStateMachine.State.Engage, bite.State, "咬住是 Engage");

            Assert.IsTrue(ch.IsChildDowned(900), "被咬的孩子倒地(呈现闸门)");
            Assert.IsFalse(ch.IsChildDowned(901), "闸门只认 P2-02 的孩子");
        }

        [Test]
        public void test_stone_startsFlee_andWritesDisengagedOnce()
        {
            _starts.TryStart(PrologueChoreography.DogEncounterId, EnemyPrototypes.WildDog,
                             Spawn, new[] { 501, 502 }, tick: 1);
            PrologueChoreography ch = Make();
            ch.SetChild(900);

            ch.OnTickEdge(1);   // 遭遇在 tick 1 开启(拍表锚点)
            long biting = 1 + PrologueChoreography.LungeTicks;
            ch.OnTickEdge(biting);
            Assert.AreEqual(501, ch.ScriptedAttackTarget, "咬住期间石头只锁狗(投掷,不看手长)");

            // 石头命中:走 25 的同一条通道
            _injuries.TryWrite(1, 501, CombatDriver.InjuryBlunt, Fix.One, biting);
            ch.OnTickEdge(biting + 1);

            Assert.IsTrue(ch.TryGetPose(501, out ScriptedEnemyPose fled));
            Assert.AreEqual(EnemyStateMachine.State.Disengage, fled.State, "松口 = Disengage");
            Assert.AreEqual(-1, ch.ScriptedAttackTarget, "松口后不再强制目标");

            for (long t = biting + 2; t <= biting + 1 + PrologueChoreography.FleeTicks; t++)
                ch.OnTickEdge(t);

            Assert.AreEqual(1, Count(EventKind.EncounterEnded), "「野狗离场」只写一次");
            EncounterEndedPayload ended = Payload<EncounterEndedPayload>(First(EventKind.EncounterEnded));
            Assert.AreEqual(PrologueChoreography.DogEncounterId, ended.EncounterId);
            Assert.AreEqual((int)EncounterEndReasonKind.Disengaged, ended.Reason,
                "P2-03 的判据是**野狗离场**(脱离),不是全倒地");
        }

        [Test]
        public void test_dogEncounter_withoutStone_neverEnds()
        {
            _starts.TryStart(PrologueChoreography.DogEncounterId, EnemyPrototypes.WildDog,
                             Spawn, new[] { 501, 502 }, tick: 1);
            PrologueChoreography ch = Make();
            ch.SetChild(900);
            for (long t = 1; t <= 200; t++) ch.OnTickEdge(t);   // 10 s 不砸

            Assert.AreEqual(0, Count(EventKind.EncounterEnded), "不砸石头 ⇒ 狗不松口、不离场");
        }

        // ── P2-06:三个地痞(等旁白 → 三句 → 上前 → 推 1 格)──────────────────

        [Test]
        public void test_thugs_waitForNarration_thenSpeakThreeLines()
        {
            _starts.TryStart(PrologueChoreography.ThugEncounterId, EnemyPrototypes.Thug,
                             Drugshop, new[] { 201, 202, 203 }, tick: 1);
            PrologueChoreography ch = Make();

            _narrating = true;
            ch.OnTickEdge(1);
            ch.OnTickEdge(10);
            CollectionAssert.IsEmpty(_lines, "P2-05 的旁白没播完 ⇒ 地痞不开口(否则盖掉那一句)");

            _narrating = false;
            ch.OnTickEdge(11);
            CollectionAssert.AreEqual(new[] { PrologueChoreography.ThugLines[0] }, _lines);
            Assert.AreEqual(3, PrologueChoreography.ThugLines.Count, "「对话(短)」是三句");

            ch.OnTickEdge(11 + PrologueChoreography.LineTicks);
            CollectionAssert.AreEqual(
                new[] { PrologueChoreography.ThugLines[0], PrologueChoreography.ThugLines[1] }, _lines);

            ch.OnTickEdge(11 + 2 * PrologueChoreography.LineTicks);
            CollectionAssert.AreEqual(
                new[] { PrologueChoreography.ThugLines[0], PrologueChoreography.ThugLines[1],
                        PrologueChoreography.ThugLines[2] }, _lines);
        }

        [Test]
        public void test_thugs_pushPlayerOneCellAway()
        {
            _starts.TryStart(PrologueChoreography.ThugEncounterId, EnemyPrototypes.Thug,
                             Drugshop, new[] { 201, 202, 203 }, tick: 1);
            PrologueChoreography ch = Make();

            // 玩家站在地痞队形的南边两格 ⇒ 对话结束、上前一步后,被往**南**推一格。
            _playerCell = new WorldPos(24, 0, 10);
            long dialogueDone = 1 + 3 * PrologueChoreography.LineTicks;
            for (long t = 1; t <= dialogueDone; t++) ch.OnTickEdge(t);
            Assert.IsNull(_teleportedTo, "对话没完 ⇒ 还没推");

            for (long t = dialogueDone + 1;
                 t <= dialogueDone + PrologueChoreography.StepTicks; t++)
                ch.OnTickEdge(t);

            Assert.IsTrue(_teleportedTo.HasValue, "对话结束 ⇒ 推你 1 格");
            Assert.AreEqual(24f, _teleportedTo.Value.x, 1e-3f, "只沿远离方向推:横轴不变");
            Assert.AreEqual(11f, _teleportedTo.Value.z, 1e-3f, "往南一格");
        }

        [Test]
        public void test_thugs_push_skipsUnwalkableCell()
        {
            _starts.TryStart(PrologueChoreography.ThugEncounterId, EnemyPrototypes.Thug,
                             Drugshop, new[] { 201, 202, 203 }, tick: 1);
            // 南边是墙 ⇒ 不把玩家塞进去;退到副轴(横轴)那一格。
            PrologueChoreography ch = Make(walkable: cell => cell.Z < 11);

            _playerCell = new WorldPos(24, 0, 10);
            long done = 1 + 3 * PrologueChoreography.LineTicks + PrologueChoreography.StepTicks;
            for (long t = 1; t <= done; t++) ch.OnTickEdge(t);

            Assert.IsTrue(_teleportedTo.HasValue);
            Assert.IsTrue(Mathf.Abs(_teleportedTo.Value.z - 11f) > 1e-3f, "墙格不得被推入");
        }

        // ── P2-09:狗带幼崽,看你一眼走开 ─────────────────────────────────────

        [Test]
        public void test_campDogs_pupIsScaled_lookThenWalkAwayEnds()
        {
            _starts.TryStart(PrologueChoreography.CampDogEncounterId, EnemyPrototypes.WildDog,
                             new WorldPos(97, 0, 18), new[] { 701, 702 }, tick: 1);
            PrologueChoreography ch = Make();
            _playerCell = new WorldPos(96, 0, 18);

            ch.OnTickEdge(1);
            Assert.IsTrue(ch.TryGetPose(702, out ScriptedEnemyPose pup));
            Assert.AreEqual((double)PrologueChoreography.PupScale, (double)pup.Scale, 1e-3);
            Assert.AreEqual(EnemyStateMachine.State.Alert, pup.State, "先看你一眼(Alert)");

            long walkStart = 1 + PrologueChoreography.LookTicks;
            ch.OnTickEdge(walkStart);
            Assert.IsTrue(ch.TryGetPose(701, out ScriptedEnemyPose turning));
            Assert.AreEqual(EnemyStateMachine.State.Disengage, turning.State, "走开 = Disengage");

            long end = walkStart + PrologueChoreography.WalkAwayTicks;
            for (long t = walkStart + 1; t <= end; t++) ch.OnTickEdge(t);

            Assert.IsTrue(ch.TryGetPose(701, out ScriptedEnemyPose gone));
            Assert.IsTrue(gone.Position.x > 100f, "走开了(向东)");
            Assert.AreEqual(1, Count(EventKind.EncounterEnded), "「走开」只写一次");
            Assert.AreEqual((int)EncounterEndReasonKind.Disengaged,
                Payload<EncounterEndedPayload>(First(EventKind.EncounterEnded)).Reason);
        }

        // ── 边界 ─────────────────────────────────────────────────────────────

        [Test]
        public void test_nonScriptedEncounter_isNotTakenOver()
        {
            _starts.TryStart(99, EnemyPrototypes.Thug, Drugshop, new[] { 1 }, tick: 1);
            PrologueChoreography ch = Make();
            for (long t = 1; t <= 5; t++) ch.OnTickEdge(t);

            Assert.IsFalse(ch.TryGetPose(1, out _), "掷骰池 / 其它遭遇不归编排管");
            Assert.AreEqual(0, Count(EventKind.EncounterEnded));
        }

        [Test]
        public void test_rollback_replaysTimeline()
        {
            _starts.TryStart(PrologueChoreography.DogEncounterId, EnemyPrototypes.WildDog,
                             Spawn, new[] { 501, 502 }, tick: 1);
            PrologueChoreography ch = Make();
            ch.SetChild(900);

            ch.OnTickEdge(1);   // 遭遇在 tick 1 开启(拍表锚点)
            long biting = 1 + PrologueChoreography.LungeTicks;
            ch.OnTickEdge(biting);
            Assert.IsTrue(ch.TryGetPose(501, out ScriptedEnemyPose atBite));
            Assert.AreEqual(45f, atBite.Position.x, 1e-3f);

            ch.OnTickEdge(2);   // 读档:tick 倒退

            Assert.IsTrue(ch.TryGetPose(501, out ScriptedEnemyPose replayed));
            Assert.AreEqual(Spawn.X, replayed.Position.x, 1e-3f, "回卷 ⇒ 时间线从流里重推(重演扑击)");
            Assert.IsFalse(ch.IsChildDowned(900), "重演期间孩子还没倒");
        }

        // ── 战斗侧:石头跳过判距 ────────────────────────────────────────────

        [Test]
        public void test_combat_scriptedTarget_bypassesRange()
        {
            _starts.TryStart(PrologueChoreography.DogEncounterId, EnemyPrototypes.WildDog,
                             Spawn, new[] { 501, 502 }, tick: 1);
            var combat = new CombatDriver(
                _injuries, new InjuryStateChangedWriter(_stream, new PayloadEncoder(_pool)),
                _ends, () => _stream.Events, _decoder, () => _playerCell, () => 10,
                actorId: 1, range: 1, cooldownTicks: 0);

            _playerCell = FarAway;   // 15 格之外 —— 正常判距打不到
            Assert.AreEqual(CombatAttackResult.NoTarget, combat.Attack());

            combat.ScriptedTarget = () => 501;
            Assert.AreEqual(CombatAttackResult.Hit, combat.Attack(), "石头是投掷:跳过手长");
            Assert.AreEqual(501, combat.LastTargetId);
        }

        [Test]
        public void test_combat_scriptedTargetDowned_isNotPicked()
        {
            _starts.TryStart(PrologueChoreography.DogEncounterId, EnemyPrototypes.WildDog,
                             Spawn, new[] { 501, 502 }, tick: 1);
            var combat = new CombatDriver(
                _injuries, new InjuryStateChangedWriter(_stream, new PayloadEncoder(_pool)),
                _ends, () => _stream.Events, _decoder, () => _playerCell, () => 1,
                actorId: 1, range: 1, cooldownTicks: 0);

            _playerCell = Spawn;
            for (int i = 0; i < 9; i++) combat.Attack();   // 打满一条狗(地面目标)
            Assert.GreaterOrEqual(Count(EventKind.InjuryStateChanged), 1, "先决条件:有狗倒地");

            _playerCell = FarAway;
            combat.ScriptedTarget = () => 501;
            Assert.AreEqual(CombatAttackResult.NoTarget, combat.Attack(),
                "石头也不打已倒地的狗(倒地不再是合法目标)");
        }

        // ── 辅助 ─────────────────────────────────────────────────────────────

        private PrologueChoreography Make(System.Func<WorldPos, bool> walkable = null)
            => new PrologueChoreography(
                () => _stream.Events, _decoder, _ends,
                () => _playerCell, () => new Vector3(_playerCell.X, 0.1f, _playerCell.Z),
                position => _teleportedTo = position,
                walkable ?? (_ => true),
                line => _lines.Add(line),
                () => _narrating);

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

        /// <summary>递增发号器(病人 / 物品 / 敌人共用一个序列)。</summary>
        private sealed class CountingIdAuthority : IIdAuthority
        {
            private int _next = 100;

            public PatientId NextPatientId() => new PatientId(_next++);

            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }
    }
}
