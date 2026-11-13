// 相机摇臂**接线**夹具(2026-11-13)。
//
// 钉的是**接缝**,不是求解器本身(story 004 的 `camera_arm_solver_test` 已覆盖后者):
//   ① 未绑定 ⇒ `EffectiveArmLength` 等于序列化臂长(接线前后行为逐位一致 —— 62 处 EditMode
//      夹具直调 `CameraRig`,任何默认行为变化都会波及它们);
//   ② 绑定 ⇒ 臂长由求解器给,且 `CameraRig` 的序列化字段对齐到档位参数表(单一真源);
//   ③ 绑定后一次 `Step` 命中 ⇒ 有效臂长**立即**缩短(AC-2-14① 瞬时收缩)。
//
// 🗝 立此件的缘由:2026-11-13 查出 `CameraArmSolver` / `CameraEvaluationDriver` **全仓只有
//    测试在构造**,生产相机从不查询物理 ⇒ 「越肩第三人称」只实现摆位、没实现避障。

using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    /// <summary>固定返回一次命中的查询桩。</summary>
    internal sealed class FixedHitQuery : IArmCollisionQuery
    {
        private readonly bool _hit;
        private readonly float _distance;
        public int Calls { get; private set; }

        public FixedHitQuery(bool hit, float distance)
        {
            _hit = hit;
            _distance = distance;
        }

        public (bool hit, float distance) Cast(Vector3 origin, Vector3 dir, float radius, float maxDist)
        {
            Calls++;
            return (_hit, _hit ? _distance : 0f);
        }
    }

    [TestFixture]
    public class camera_rig_arm_wiring_test
    {
        private GameObject _go;
        private CameraRig _rig;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CameraRigTest");
            _rig = _go.AddComponent<CameraRig>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        /// <summary>未绑定 ⇒ 有效臂长 = 序列化臂长(接线不改变既有行为)。</summary>
        [Test]
        public void test_unbound_effectiveArmLengthIsSerializeField()
        {
            Assert.That(_rig.EffectiveArmLength, Is.EqualTo(5f).Within(1e-4f),
                "未绑定摇臂时必须维持原序列化臂长 —— 任何变化都会波及 62 处直调本类的夹具");
        }

        /// <summary>绑定 ⇒ 臂长真源搬到档位参数表,且第一次命中即瞬时缩短。</summary>
        [Test]
        public void test_bound_armLengthComesFromSolver_andShortensOnHit()
        {
            var p = new CameraArmParams { ArmLen = 4f, CamRadius = 0.3f, CamMinDist = 0.5f };
            var q = new FixedHitQuery(hit: true, distance: 2.0f);
            var solver = new CameraArmSolver(p, q);

            _rig.BindArmSolver(solver, p);
            Assert.That(_rig.EffectiveArmLength, Is.EqualTo(4f).Within(1e-4f),
                "绑定后臂长真源 = 档位参数表(否则求值用 4、读数显示 5 = 两份真源)");

            solver.Step(Vector3.zero, Vector3.forward, 0.016f);

            Assert.That(q.Calls, Is.EqualTo(1), "一次 Step 恰一次查询(AC-2-27①)");
            Assert.That(_rig.EffectiveArmLength, Is.EqualTo(2.0f - 0.3f).Within(1e-3f),
                "命中 2 m − CAM_RADIUS 0.3 = 1.7 m;且收缩是**瞬时**的(AC-2-14①)");
        }

        /// <summary>未命中 ⇒ 臂长回到**满档**(主用例:背后无墙 ⇒ 越肩取景保持)。
        /// <para>⚠️ 满档 = `ARM_LEN − CAM_RADIUS`(4 − 0.3 = 3.7),**不是** `ARM_LEN` ——
        /// 求解器对射线结果一律扣掉碰撞半径(AC-2-14 的 `d_block` 式),
        /// 未命中只是「不额外缩短」。首版断言写 4.0 被这条抓出来。</para></summary>
        [Test]
        public void test_bound_noHit_keepsFullArm()
        {
            var p = new CameraArmParams { ArmLen = 4f, CamRadius = 0.3f, CamMinDist = 0.5f };
            var q = new FixedHitQuery(hit: false, distance: 0f);
            var solver = new CameraArmSolver(p, q);
            _rig.BindArmSolver(solver, p);

            solver.Step(Vector3.zero, Vector3.forward, 0.016f);

            Assert.That(_rig.EffectiveArmLength, Is.EqualTo(4f - 0.3f).Within(1e-3f),
                "未命中 ⇒ 满档 = ARM_LEN − CAM_RADIUS");
        }
    }
}
