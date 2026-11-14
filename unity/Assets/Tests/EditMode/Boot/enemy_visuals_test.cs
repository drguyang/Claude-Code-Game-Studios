// 敌人最小可见实体(「完整可见」轮 · 2026-11-14)的 EditMode 判据
//
// 权威:
//   GDD enemy-ai.md 规则二十二(六态可读 / 归零 = 不动了 + 呼吸层 / 倒地野兽可取材)
//   GDD skeuomorphic-ui.md V-10(读数条 · 黄铜侧蚀刻刻度;值读既有 VitalsDto)
//   ADR-005(IVitalsQuery = 唯一浮点出口)
//   ADR-013 §9 C3(呈现层只渲染,不持游戏状态)
//
// ⚠️ 这些判据钉的是**机制**(投影 / 工厂 / 位姿 / 读数),不证明「真关卡里看得见」——
//    后者需要启动序走查(`TD-build-07` 收口段)。
// ⚠️ 零写盘 / 零随机 / 零墙钟。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Enemies;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.MedConsequences;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>敌人可见实体的机制判据(投影 / 复合查询 / 工厂 / 位姿 / 读数面片)。</summary>
    public class enemy_visuals_test
    {
        private static readonly WorldPos Cell = new WorldPos(23, 0, 7);

        private EventStream _stream;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EncounterStartWriter _starts;
        private EncounterEndWriter _ends;
        private EnemyInjuryWriter _injuries;

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
        }

        // ── VitalsDto 投影 ────────────────────────────────────────────────

        [Test]
        public void test_projection_clampsPosition_andLeavesTrendZero()
        {
            VitalsDto negative = EnemyVitalsProjection.ToVitals(new Fix(-5000));
            Assert.AreEqual(0f, negative.Position, "负伤量 ⇒ 0(不返回负读数)");
            Assert.AreEqual(0f, negative.Trend, "敌人无带符号趋势");

            VitalsDto full = EnemyVitalsProjection.ToVitals(new Fix(999999));
            Assert.AreEqual(1f, full.Position, "超过满值 ⇒ 饱和到 1");

            VitalsDto half = EnemyVitalsProjection.ToVitals(
                new Fix(EnemyVitalsProjection.FullPositionRaw / 2));
            Assert.AreEqual(0.5f, half.Position, 1e-4f);
        }

        [Test]
        public void test_projection_bandIsMonotonicInHurt()
        {
            InjuryState healthy = EnemyVitalsProjection.BandOf(Fix.Zero);
            InjuryState hurt = EnemyVitalsProjection.BandOf(new Fix(40000));
            InjuryState fatal = EnemyVitalsProjection.BandOf(new Fix(200000));

            Assert.AreEqual(InjuryState.Healthy, healthy);
            Assert.GreaterOrEqual((int)hurt, (int)healthy, "伤量越大档位不得回退");
            Assert.GreaterOrEqual((int)fatal, (int)hurt);
        }

        // ── 复合 IVitalsQuery ─────────────────────────────────────────────

        [Test]
        public void test_compositeQuery_dispatchesByRosterMembership()
        {
            _starts.TryStart(1, EnemyPrototypes.WildDog, Cell, new[] { 201 }, tick: 1);
            var patients = new StubVitalsQuery(new VitalsDto(0.11f, 0f, 0, 0));

            var composite = new CompositeVitalsQuery(
                patients,
                new EnemyVitalsQuery(() => EnemyRoster.Build(_stream.Events, _decoder)),
                () => EnemyRoster.Build(_stream.Events, _decoder));

            Assert.AreEqual(0f, composite.GetVitals(new PatientId(201)).Position,
                "敌人 id ⇒ 走敌人投影(无伤 ⇒ 0),不得落到病人侧");

            Assert.AreEqual(0.11f, composite.GetVitals(new PatientId(999)).Position, 1e-6f,
                "非敌人 id ⇒ 落到病人侧");
            Assert.AreEqual(1, patients.Calls, "病人侧只被非敌人 id 调用");
        }

        [Test]
        public void test_enemyVitalsQuery_unknownId_throws()
        {
            var query = new EnemyVitalsQuery(() => new List<EnemyActor>());
            Assert.Throws<KeyNotFoundException>(() => query.GetVitals(new PatientId(404)),
                "未知 id 必须 fail-loud —— 全零 = 把「不是敌人」伪装成「健康」");
        }

        // ── 视觉 spawner ─────────────────────────────────────────────────

        [Test]
        public void test_spawner_emptyStream_buildsNothing()
        {
            var factory = new ComposedEnemyVisualFactory();
            var spawner = MakeSpawner(factory);
            try
            {
                spawner.OnTickEdge(1);
                Assert.AreEqual(0, spawner.VisualCount, "无遭遇 ⇒ 场上无敌人");
            }
            finally
            {
                spawner.DestroyAll();
                factory.Dispose();
            }
        }

        [Test]
        public void test_spawner_createsOneVisualPerMember_withPoseRig()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 301, 302 }, tick: 1);
            var factory = new ComposedEnemyVisualFactory();
            var spawner = MakeSpawner(factory);
            try
            {
                spawner.OnTickEdge(1);

                Assert.AreEqual(2, spawner.VisualCount);
                Assert.IsTrue(spawner.TryGetVisual(301, out GameObject visual));
                Assert.IsNotNull(visual);
                Assert.AreEqual(new Vector3(Cell.X, Cell.Y, Cell.Z), visual.transform.position,
                    "位置来自 EncounterStarted 的出生格");
                Assert.IsNotNull(visual.GetComponent<EnemyPoseRig>(), "视觉根挂位姿桥");
                Assert.IsTrue(spawner.TryGetRig(301, out _));
            }
            finally
            {
                spawner.DestroyAll();
                factory.Dispose();
            }
        }

        [Test]
        public void test_spawner_encounterEnded_keepsVisuals_downed()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 401 }, tick: 1);
            _ends.TryEnd(1, EncounterEndReasonKind.AllDowned, tick: 5);

            var factory = new ComposedEnemyVisualFactory();
            var spawner = MakeSpawner(factory);
            try
            {
                spawner.OnTickEdge(5);
                spawner.OnTickEdge(6);

                Assert.AreEqual(1, spawner.VisualCount,
                    "收场 ≠ 实体消失(GDD 规则二十二):视觉仍在场");
                Assert.IsTrue(spawner.TryGetRig(401, out EnemyPoseRig rig));
                Transform poseRoot = rig.transform.Find("PoseRoot");
                Assert.IsNotNull(poseRoot);
                // 倒地姿态 = 人形侧卧(z 轴 90°)
                Assert.AreEqual(90f, Mathf.DeltaAngle(0f, poseRoot.localEulerAngles.z), 0.5f);
            }
            finally
            {
                spawner.DestroyAll();
                factory.Dispose();
            }
        }

        [Test]
        public void test_spawner_destroyAll_isIdempotent()
        {
            _starts.TryStart(1, EnemyPrototypes.WildDog, Cell, new[] { 501 }, tick: 1);
            var factory = new ComposedEnemyVisualFactory();
            var spawner = MakeSpawner(factory);
            try
            {
                spawner.OnTickEdge(1);
                spawner.DestroyAll();
                Assert.AreEqual(0, spawner.VisualCount);
                Assert.DoesNotThrow(spawner.DestroyAll, "teardown 幂等");
            }
            finally
            {
                factory.Dispose();
            }
        }

        // ── 位姿桥:六态各一档 ────────────────────────────────────────────

        [Test]
        public void test_poseRig_sixStatesSetDistinctLimbPoses()
        {
            var factory = new ComposedEnemyVisualFactory();
            try
            {
                GameObject visual = factory.Create(MakePose(EnemyStateMachine.State.Patrol));
                var rig = visual.GetComponent<EnemyPoseRig>();
                Transform armL = rig.transform.Find("PoseRoot/ArmL");

                var seen = new HashSet<float>();
                foreach (EnemyStateMachine.State state in new[]
                         {
                             EnemyStateMachine.State.Patrol, EnemyStateMachine.State.Alert,
                             EnemyStateMachine.State.Chase, EnemyStateMachine.State.Flank,
                             EnemyStateMachine.State.Engage, EnemyStateMachine.State.Disengage,
                         })
                {
                    rig.Apply(MakePose(state), 0.05f);
                    seen.Add(Mathf.Round(Mathf.DeltaAngle(0f, armL.localEulerAngles.z)));
                }

                Assert.AreEqual(6, seen.Count, "六态各一档姿态(禁两态同姿的假绿)");

                factory.Destroy(visual);
            }
            finally
            {
                factory.Dispose();
            }
        }

        [Test]
        public void test_poseRig_downedOverridesState()
        {
            var factory = new ComposedEnemyVisualFactory();
            try
            {
                GameObject visual = factory.Create(MakePose(EnemyStateMachine.State.Patrol));
                var rig = visual.GetComponent<EnemyPoseRig>();
                Transform poseRoot = rig.transform.Find("PoseRoot");

                rig.Apply(MakePose(EnemyStateMachine.State.Engage, downed: true), 0.05f);
                Assert.AreEqual(90f, Mathf.DeltaAngle(0f, poseRoot.localEulerAngles.z), 0.5f,
                    "倒地优先于六态(倒地后零状态转移)");

                rig.Apply(MakePose(EnemyStateMachine.State.Patrol, downed: true), 0.05f);
                Assert.AreEqual(90f, Mathf.DeltaAngle(0f, poseRoot.localEulerAngles.z), 0.5f,
                    "换六态不改变倒地姿态");

                factory.Destroy(visual);
            }
            finally
            {
                factory.Dispose();
            }
        }

        [Test]
        public void test_poseRig_salvageGlow_onlyForDownedBeast()
        {
            var factory = new ComposedEnemyVisualFactory();
            try
            {
                GameObject beast = factory.Create(MakePose(EnemyStateMachine.State.Patrol, proto: 1));
                var beastRig = beast.GetComponent<EnemyPoseRig>();
                Renderer beastInk = beast.transform.Find("PoseRoot/Body_Ink").GetComponent<Renderer>();

                beastRig.Apply(MakePose(EnemyStateMachine.State.Patrol, proto: 1), 0.05f);
                Assert.IsFalse(beastInk.enabled, "存活野兽不显示可取材微光");

                beastRig.Apply(MakePose(EnemyStateMachine.State.Patrol, proto: 1, downed: true), 0.05f);
                Assert.IsTrue(beastInk.enabled, "倒地野兽显示墨色轮廓微光");

                GameObject human = factory.Create(MakePose(EnemyStateMachine.State.Patrol, proto: 0));
                var humanRig = human.GetComponent<EnemyPoseRig>();
                Renderer humanInk = human.transform.Find("PoseRoot/Torso_Ink").GetComponent<Renderer>();
                humanRig.Apply(MakePose(EnemyStateMachine.State.Patrol, proto: 0, downed: true), 0.05f);
                Assert.IsFalse(humanInk.enabled, "人形恒不可取材");

                factory.Destroy(beast);
                factory.Destroy(human);
            }
            finally
            {
                factory.Dispose();
            }
        }

        [Test]
        public void test_poseRig_fadeIn_reachesFullScale()
        {
            var factory = new ComposedEnemyVisualFactory();
            try
            {
                GameObject visual = factory.Create(MakePose(EnemyStateMachine.State.Patrol));
                var rig = visual.GetComponent<EnemyPoseRig>();

                rig.Apply(MakePose(EnemyStateMachine.State.Patrol, alpha: 0f), 0.05f);
                Assert.Less(visual.transform.localScale.x, 1f, "出生瞬间未完全呈现");

                rig.Apply(MakePose(EnemyStateMachine.State.Patrol, alpha: 1f), 0.05f);
                Assert.AreEqual(1f, visual.transform.localScale.x, 1e-4f, "淡入完成 = 原尺寸");

                factory.Destroy(visual);
            }
            finally
            {
                factory.Dispose();
            }
        }

        // ── 读数面片:离散蚀刻槽 ─────────────────────────────────────────

        [Test]
        public void test_billboard_litTickCount_followsPosition()
        {
            var billboard = new EnemyVitalsBillboard(1);
            try
            {
                billboard.Apply("轻伤", 0f, false);
                Assert.AreEqual(0, CountLit(billboard), "0 读数 ⇒ 零槽点亮");

                billboard.Apply("轻伤", 0.5f, false);
                Assert.AreEqual(Mathf.CeilToInt(0.5f * EnemyVitalsBillboard.TickSlots), CountLit(billboard),
                    "点亮槽数 = ⌈Position × 槽数⌉(离散刻痕,不是连续填充)");

                billboard.Apply("轻伤", 1f, false);
                Assert.AreEqual(EnemyVitalsBillboard.TickSlots, CountLit(billboard));
            }
            finally
            {
                billboard.Destroy();
            }
        }

        [Test]
        public void test_bar_hasNineDiscreteSlots()
        {
            Assert.AreEqual(9, EnemyVitalsBillboard.TickSlots);
            var bar = new EnemyVitalsBar(1);
            try
            {
                bar.WorldPosition = new Vector3(1f, 2f, 3f);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), bar.WorldPosition);
                Assert.DoesNotThrow(() => bar.RenderReading(0.5f, "轻伤", false));
                Assert.DoesNotThrow(() => bar.FaceCamera(null), "无相机 ⇒ 不抛(保持上一帧姿态)");
            }
            finally
            {
                bar.Destroy();
            }
        }

        // ── 夹具 ─────────────────────────────────────────────────────────
        private EnemyVisualSpawner MakeSpawner(IEnemyVisualFactory factory)
        {
            return new EnemyVisualSpawner(
                () => _stream.Events, _decoder, factory,
                new EnemyVitalsQuery(() => EnemyRoster.Build(_stream.Events, _decoder)),
                () => null);
        }

        private static EnemyPose MakePose(EnemyStateMachine.State state, int proto = 0,
                                          bool downed = false, float alpha = 1f)
            => new EnemyPose(1, proto, Vector3.zero, 0f, state, downed,
                             downed && proto == 1, alpha, 1f, InjuryState.Healthy);

        private static int CountLit(EnemyVitalsBillboard billboard)
        {
            int lit = 0;
            foreach (Image image in billboard.GetType()
                         .GetField("_etches", System.Reflection.BindingFlags.NonPublic
                                               | System.Reflection.BindingFlags.Instance)
                         ?.GetValue(billboard) as Image[] ?? new Image[0])
                if (image != null && image.color.a > 0.6f && image.color.r < 0.2f)
                    lit++;
            return lit;
        }

        /// <summary>病人侧桩(只用于证明复合门面的分派 —— 不模拟真实体征)。</summary>
        private sealed class StubVitalsQuery : IVitalsQuery
        {
            private readonly VitalsDto _value;
            public int Calls { get; private set; }

            public StubVitalsQuery(VitalsDto value) { _value = value; }

            public VitalsDto GetVitals(PatientId p)
            {
                Calls++;
                return _value;
            }
        }

        /// <summary>递增发号器(病人 / 敌人共用一个序列)。</summary>
        private sealed class CountingIdAuthority : IIdAuthority
        {
            private int _next = 100;

            public PatientId NextPatientId() => new PatientId(_next++);

            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }
    }
}
