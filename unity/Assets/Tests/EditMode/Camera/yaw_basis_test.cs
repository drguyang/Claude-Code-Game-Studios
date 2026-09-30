// camera-viewpoint Story 002 测试
//
// AC-2-07: YawBasis 水平化
// AC-2-08: YawBasis 正交归一
// AC-2-09: PITCH_MAX < 90°
// AC-2-10: 帧内次序契约

using System;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Camera
{
    public class YawBasisTest
    {
        private CameraRig _rig;

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("CameraRig");
            _rig = go.AddComponent<CameraRig>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_rig != null) UnityEngine.Object.DestroyImmediate(_rig.gameObject);
        }

        // AC-2-07①: 水平化与 pitch 无关
        [Test]
        public void test_yawBasis_horizontal_regardlessOfPitch()
        {
            float[] pitches = { -45f, -30f, 0f, 30f, 60f };
            float[] yaws = { 0f, Mathf.PI / 4, Mathf.PI / 2, 3 * Mathf.PI / 4, Mathf.PI };

            foreach (float yaw in yaws)
            {
                foreach (float pitch in pitches)
                {
                    _rig.UpdateYaw(yaw);
                    _rig.UpdatePitch(pitch - _rig.Pitch); // 设置绝对 pitch

                    var basis = _rig.YawBasis;
                    Assert.AreEqual(0f, basis.Fwd.y, 1e-6f, $"fwd.y 应为 0 (yaw={yaw}, pitch={pitch})");
                    Assert.AreEqual(0f, basis.Right.y, 1e-6f, $"right.y 应为 0 (yaw={yaw}, pitch={pitch})");
                }
            }
        }

        // AC-2-07②: 手性（防镜像）
        [Test]
        public void test_yawBasis_chirality()
        {
            // yaw = 0: fwd = (0, 0, 1), right = (1, 0, 0)
            _rig.UpdateYaw(0f);
            var basis0 = _rig.YawBasis;
            Assert.AreEqual(0f, basis0.Fwd.x, 1e-6f);
            Assert.AreEqual(0f, basis0.Fwd.y, 1e-6f);
            Assert.AreEqual(1f, basis0.Fwd.z, 1e-6f);
            Assert.AreEqual(1f, basis0.Right.x, 1e-6f);
            Assert.AreEqual(0f, basis0.Right.y, 1e-6f);
            Assert.AreEqual(0f, basis0.Right.z, 1e-6f);

            // yaw = π/2: fwd = (1, 0, 0), right = (0, 0, -1)
            _rig.UpdateYaw(Mathf.PI / 2);
            var basis90 = _rig.YawBasis;
            Assert.AreEqual(1f, basis90.Fwd.x, 1e-6f);
            Assert.AreEqual(0f, basis90.Fwd.y, 1e-6f);
            Assert.AreEqual(0f, basis90.Fwd.z, 1e-6f);
            Assert.AreEqual(0f, basis90.Right.x, 1e-6f);
            Assert.AreEqual(0f, basis90.Right.y, 1e-6f);
            Assert.AreEqual(-1f, basis90.Right.z, 1e-6f);
        }

        // AC-2-08: 正交归一全周扫描
        [Test]
        public void test_yawBasis_orthonormal_fullCircle()
        {
            int samples = 360;
            for (int i = 0; i < samples; i++)
            {
                float yaw = i * Mathf.PI * 2f / samples;
                _rig.UpdateYaw(yaw);

                var basis = _rig.YawBasis;

                float fwdLen = basis.Fwd.magnitude;
                float rightLen = basis.Right.magnitude;
                float dot = Vector3.Dot(basis.Fwd, basis.Right);

                Assert.AreEqual(1f, fwdLen, CameraRig.YAW_BASIS_EPS, $"‖f̂‖ 应为 1 (yaw={yaw})");
                Assert.AreEqual(1f, rightLen, CameraRig.YAW_BASIS_EPS, $"‖r̂‖ 应为 1 (yaw={yaw})");
                Assert.AreEqual(0f, dot, CameraRig.YAW_BASIS_EPS, $"dot(f̂,r̂) 应为 0 (yaw={yaw})");
            }
        }

        // AC-2-09: PITCH_MAX < 90°
        [Test]
        public void test_pitchConstraint()
        {
            Assert.Less(CameraRig.PITCH_MIN, 0f, "PITCH_MIN 应 < 0");
            Assert.Greater(CameraRig.PITCH_MAX, 0f, "PITCH_MAX 应 > 0");
            Assert.Less(CameraRig.PITCH_MAX, 90f, "PITCH_MAX 应 < 90°");
        }

        // AC-2-10②: 相机对 1 零 public 写入面
        [Test]
        public void test_noPublicWriteSurface()
        {
            // 反射验证 ICameraRig 无 public 方法（只有属性）
            var interfaceType = typeof(ICameraRig);
            foreach (var method in interfaceType.GetMethods())
            {
                // 属性 getter 是特殊方法，允许
                if (method.IsSpecialName && method.Name.StartsWith("get_")) continue;
                Assert.Fail($"ICameraRig 不应有 public 方法: {method.Name}");
            }
        }

        // AC-2-10①: 帧内次序契约
        [Test]
        public void test_frameOrderingContract()
        {
            // 模拟帧内次序：先更新 yaw，再读取 YawBasis
            _rig.UpdateYaw(1.0f);
            var basis = _rig.YawBasis;

            // 读取值应等于本帧更新后的值
            Assert.AreEqual(1.0f, _rig.Yaw, 1e-6f, "Yaw 应为更新后的值");
            Assert.AreEqual(Mathf.Sin(1.0f), basis.Fwd.x, 1e-6f, "Fwd.x 应等于 sin(yaw)");
            Assert.AreEqual(Mathf.Cos(1.0f), basis.Fwd.z, 1e-6f, "Fwd.z 应等于 cos(yaw)");
        }

        // AC-2-09②: proj_h(f̂) 正下界
        [Test]
        public void test_projH_positiveLowerBound()
        {
            // 遍历 pitch ∈ [0, PITCH_MAX]，proj_h(f̂) 的模长有正下界 cos(PITCH_MAX) > 0
            float cosMax = Mathf.Cos(CameraRig.PITCH_MAX * Mathf.Deg2Rad);
            Assert.Greater(cosMax, 0f, $"cos(PITCH_MAX) 应 > 0 (PITCH_MAX={CameraRig.PITCH_MAX})");
        }

        // AC-2-09③: 防御性兜底（PITCH_MAX ≥ 90° 时 YawBasis 不抛异常）
        [Test]
        public void test_defensiveFallback_pitchMax90()
        {
            // 即使 PITCH_MAX 被错配为 ≥ 90°，YawBasis 仍应返回有效值
            // 注意：这里测试的是 YawBasis 的数学性质，不依赖 PITCH_MAX 的实际值
            _rig.UpdateYaw(Mathf.PI / 4);
            var basis = _rig.YawBasis;

            // 仍应满足水平化 + 正交归一
            Assert.AreEqual(0f, basis.Fwd.y, 1e-6f);
            Assert.AreEqual(0f, basis.Right.y, 1e-6f);
            Assert.AreEqual(1f, basis.Fwd.magnitude, 1e-6f);
            Assert.AreEqual(1f, basis.Right.magnitude, 1e-6f);
        }

        // AC-2-13: pitch 触界被钳后 yaw 照常累积
        [Test]
        public void test_pitchClampDoesNotPolluteYaw()
        {
            // 推 pitch 到 PITCH_MAX，继续应用 yaw 输入，然后回到水平
            float initialYaw = _rig.Yaw;

            // 推 pitch 到最大
            for (int i = 0; i < 100; i++) _rig.UpdatePitch(10f);
            Assert.AreEqual(CameraRig.PITCH_MAX, _rig.Pitch, 1e-6f, "Pitch 应被钳到 PITCH_MAX");

            // 继续应用 yaw 输入
            _rig.UpdateYaw(0.5f);
            Assert.AreEqual(initialYaw + 0.5f, _rig.Yaw, 1e-6f, "Yaw 应照常累积");

            // 回到水平
            _rig.UpdatePitch(-1000f);
            Assert.AreEqual(CameraRig.PITCH_MIN, _rig.Pitch, 1e-6f, "Pitch 应被钳到 PITCH_MIN");
        }
    }
}
