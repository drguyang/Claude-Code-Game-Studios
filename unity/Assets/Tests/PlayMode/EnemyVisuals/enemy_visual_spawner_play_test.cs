// 敌人可见实体的 PlayMode 判据(「完整可见」轮 · 2026-11-14)
//
// 权威:
//   GDD enemy-ai.md 规则二十二(六态可读 / 归零 = 不动了 + 呼吸层)
//   GDD skeuomorphic-ui.md V-10(读数条 · 黄铜面片 · 恒定屏幕尺寸)
//   ADR-013 §9 C3(呈现层只渲染;teardown 由驱动显式销毁)
//
// ⚠️ 钉的是「运行期真的建出了可见物体、teardown 真的清干净」——
//    不证明真关卡里看得见(那需要启动序走查,`TD-build-07` 收口段)。

using System.Collections;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Enemies;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.PlayMode
{
    /// <summary>敌人视觉实体的运行期生命周期。</summary>
    public class enemy_visual_spawner_play_test
    {
        private static readonly WorldPos Cell = new WorldPos(23, 0, 7);

        private EventStream _stream;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EncounterStartWriter _starts;
        private ComposedEnemyVisualFactory _factory;
        private EnemyVisualSpawner _spawner;

        [SetUp]
        public void SetUp()
        {
            _stream = new EventStream(new CountingIdAuthority(), new PresenceRegistry());
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _starts = new EncounterStartWriter(_stream, _encoder, () => _stream.Events, _decoder);

            _factory = new ComposedEnemyVisualFactory();
            _spawner = new EnemyVisualSpawner(
                () => _stream.Events, _decoder, _factory,
                new EnemyVitalsQuery(() => EnemyRoster.Build(_stream.Events, _decoder)),
                () => Camera.main);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner?.DestroyAll();
            _factory?.Dispose();
        }

        [UnityTest]
        public IEnumerator Test_encounterSpawnsVisibleObjects_andTeardownClearsThem()
        {
            _starts.TryStart(1, EnemyPrototypes.Thug, Cell, new[] { 701, 702 }, tick: 1);

            _spawner.OnTickEdge(1);
            yield return null;

            Assert.AreEqual(2, _spawner.VisualCount);
            Assert.IsTrue(_spawner.TryGetVisual(701, out GameObject visual));
            Assert.IsNotNull(visual);
            Assert.IsNotNull(visual.GetComponent<EnemyPoseRig>());
            Assert.IsNotNull(visual.GetComponentInChildren<Renderer>(), "视觉根下必须有可见网格");

            Transform billboard = GameObject.Find("EnemyVitals_701")?.transform;
            Assert.IsNotNull(billboard, "读数面片真的进了场景");
            Assert.IsNotNull(billboard.GetComponent<Canvas>());
            Assert.AreEqual(RenderMode.WorldSpace, billboard.GetComponent<Canvas>().renderMode);

            _spawner.DestroyAll();
            yield return null;

            Assert.AreEqual(0, _spawner.VisualCount);
            Assert.IsNull(GameObject.Find("EnemyVitals_701"), "teardown 后面片不残留");
            Assert.IsNull(GameObject.Find("EnemyVisuals"), "teardown 后根节点不残留");
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
