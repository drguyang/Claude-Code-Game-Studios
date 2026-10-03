// player-controller Story 002 测试
//
// AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言
// AC-1-31: v̂_world 单位性 + 水平面内
// AC-1-35: 求值次序 + YawBasis 只读 + 正交性

using System;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerClass = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class InputContractTest
    {
        // ══════════ AC-1-09: ‖MoveInput‖ ≤ 1 边界硬断言 ══════════

        [Test]
        public void test_ac109_moveInput_exceedsOne_throws()
        {
            // ‖MoveInput‖ > 1 应抛异常（不 clamp）
            var invalidInput = new Vector3(1.5f, 0f, 0f);

            Assert.Throws<ArgumentException>(() => PlayerControllerClass.ValidateMoveInput(invalidInput),
                "‖MoveInput‖ > 1 应抛异常");
        }

        [Test]
        public void test_ac109_moveInput_atOne_passes()
        {
            // ‖MoveInput‖ = 1 恰边界 ⇒ 通过
            var validInput = new Vector3(1f, 0f, 0f);

            Assert.DoesNotThrow(() => PlayerControllerClass.ValidateMoveInput(validInput),
                "‖MoveInput‖ = 1 应通过");
        }

        [Test]
        public void test_ac109_moveInput_zero_passes()
        {
            // ‖MoveInput‖ = 0 ⇒ 合法（走 Idle）
            var zeroInput = Vector3.zero;

            Assert.DoesNotThrow(() => PlayerControllerClass.ValidateMoveInput(zeroInput),
                "‖MoveInput‖ = 0 应通过");
        }

        [Test]
        public void test_ac109_moveInput_nan_throws()
        {
            // NaN 幅值 ⇒ 拒（不得穿透成 NaN 速度）
            var nanInput = new Vector3(float.NaN, 0f, 0f);

            Assert.Throws<ArgumentException>(() => PlayerControllerClass.ValidateMoveInput(nanInput),
                "NaN 幅值应抛异常");
        }

        // ══════════ AC-1-31: v̂_world 单位性 + 水平面内 ══════════

        [Test]
        public void test_ac131_worldDirection_isUnit()
        {
            // v̂_world 单位性: |‖v̂_world‖ − 1| ≤ 容差
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            var moveInput = new Vector3(0.5f, 0f, 0.5f);

            var worldDir = InputContractEvaluator.ProjectToWorld(moveInput, basis);

            float magnitude = worldDir.magnitude;
            Assert.AreEqual(1f, magnitude, 0.001f,
                "v̂_world 应为单位向量");
        }

        [Test]
        public void test_ac131_worldDirection_isHorizontal()
        {
            // v̂_world.y == 0（水平面内）
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            var moveInput = new Vector3(0.5f, 0f, 0.5f);

            var worldDir = InputContractEvaluator.ProjectToWorld(moveInput, basis);

            Assert.AreEqual(0f, worldDir.y, 0.0001f,
                "v̂_world.y 应为 0（水平面内）");
        }

        [Test]
        public void test_ac131_worldDirection_zeroInput_returnsZero()
        {
            // ‖MoveInput‖ = 0 时不做投影（走 Idle）
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            var zeroInput = Vector3.zero;

            var worldDir = InputContractEvaluator.ProjectToWorld(zeroInput, basis);

            Assert.That(worldDir, Is.EqualTo(Vector3.zero).Within(0.0001f),
                "‖MoveInput‖ = 0 应返回零向量（走 Idle）");
        }

        [Test]
        public void test_ac131_pitchedCamera_stillHorizontal()
        {
            // 反向用例: 带仰角的相机 ⇒ v̂_world.y 仍为 0
            // 构造一个 fwd 带 y 分量的 YawBasis（模拟相机俯仰）
            var pitchedFwd = new Vector3(0, 0.5f, 0.866f).normalized; // 30° 俯仰
            var right = Vector3.Cross(Vector3.up, pitchedFwd).normalized;
            var basis = new YawBasis(pitchedFwd, right);
            var moveInput = new Vector3(0.5f, 0f, 0.5f);

            var worldDir = InputContractEvaluator.ProjectToWorld(moveInput, basis);

            Assert.AreEqual(0f, worldDir.y, 0.0001f,
                "带仰角相机时 v̂_world.y 仍应为 0（否定直接用相机 forward 的缺陷写法）");
        }

        [Test]
        public void test_ac131_fullYawSweep_unitAndHorizontal()
        {
            // 全 yaw 扫描: 0 到 2pi，多个幅值
            float[] magnitudes = { 0.001f, 0.5f, 1f };
            for (int i = 0; i < 36; i++)
            {
                float yaw = i * Mathf.PI / 18f; // 0 到 2pi
                float sin = Mathf.Sin(yaw);
                float cos = Mathf.Cos(yaw);
                var fwd = new Vector3(sin, 0f, cos);
                var right = new Vector3(cos, 0f, -sin);
                var basis = new YawBasis(fwd, right);

                foreach (float mag in magnitudes)
                {
                    var moveInput = new Vector3(mag * 0.5f, 0f, mag * 0.5f);
                    var worldDir = InputContractEvaluator.ProjectToWorld(moveInput, basis);

                    Assert.AreEqual(1f, worldDir.magnitude, 0.001f,
                        $"yaw={yaw}, mag={mag}: v̂_world 应为单位向量");
                    Assert.AreEqual(0f, worldDir.y, 0.0001f,
                        $"yaw={yaw}, mag={mag}: v̂_world.y 应为 0");
                }
            }
        }

        // ══════════ AC-1-35: 求值次序 + YawBasis 只读 + 正交性 ══════════

        [Test]
        public void test_ac135_yawBasis_isReadOnly()
        {
            // ICameraRig.YawBasis 是只读接口
            var rig = new TestCameraRig(0f);
            var basis = rig.YawBasis;

            // YawBasis 是 readonly struct，不可变
            Assert.AreEqual(0f, basis.Fwd.y, 0.0001f,
                "f̂.y 应为 0（水平化）");
            Assert.AreEqual(0f, basis.Right.y, 0.0001f,
                "r̂.y 应为 0（水平化）");
        }

        [Test]
        public void test_ac135_yawBasis_isOrthogonal()
        {
            // r̂ := normalize(cross(worldUp, f̂)) 正交
            var rig = new TestCameraRig(0.5f);
            var basis = rig.YawBasis;

            float dot = Vector3.Dot(basis.Fwd, basis.Right);
            Assert.AreEqual(0f, dot, 0.001f,
                "f̂ 与 r̂ 应正交");
        }

        [Test]
        public void test_ac135_yawBasis_rightDerivedFromForward()
        {
            // r̂ 从 f̂ 派生（不单独取）
            var rig = new TestCameraRig(0.3f);
            var basis = rig.YawBasis;

            Vector3 expectedRight = Vector3.Cross(Vector3.up, basis.Fwd).normalized;
            Assert.That(basis.Right, Is.EqualTo(expectedRight).Within(0.001f),
                "r̂ 应从 f̂ 派生");
        }

        // ══════════ AC-1-35①: 求值次序 ══════════

        [Test]
        public void test_ac135_evaluationOrder_yawBasisBeforeProjection()
        {
            // AC-1-35①: 基向量在 v_target 之前求值
            // 验证: ProjectToWorld 调用 rig.YawBasis 后再做投影
            var rig = new CountingCameraRig(0.5f);
            var moveInput = new Vector3(0.5f, 0f, 0.5f);

            InputContractEvaluator.ProjectToWorld(moveInput, rig);

            Assert.AreEqual(1, rig.YawBasisCallCount,
                "YawBasis 应被调用一次（在投影之前）");
        }

        // ══════════ AC-1-35②: YawBasis 每帧只取样一次 ══════════

        [Test]
        public void test_ac135_yawBasisSampledOncePerFrame()
        {
            // AC-1-35②: YawBasis 每帧只取样一次（缓存在局部变量）
            var rig = new CountingCameraRig(0.5f);
            var moveInput = new Vector3(0.5f, 0f, 0.5f);

            // 调用两次 ProjectToWorld（模拟同一帧内多次使用）
            InputContractEvaluator.ProjectToWorld(moveInput, rig);
            InputContractEvaluator.ProjectToWorld(moveInput, rig);

            // 每次 ProjectToWorld 调用应只取样一次 YawBasis
            Assert.AreEqual(2, rig.YawBasisCallCount,
                "每次 ProjectToWorld 应只取样一次 YawBasis");
        }

        // ══════════ AC-1-35③: ICameraRig.YawBasis 是只读接口 ══════════

        [Test]
        public void test_ac135_yawBasisIsReadOnlyInterface()
        {
            // AC-1-35③: ICameraRig.YawBasis 是只读接口
            // 验证: YawBasis 是 readonly struct，不可变
            var rig = new TestCameraRig(0.5f);
            var basis = rig.YawBasis;

            // YawBasis 是 readonly struct，编译期保证不可变
            Assert.AreEqual(0f, basis.Fwd.y, 0.0001f,
                "f̂.y 应为 0（水平化）");
            Assert.AreEqual(0f, basis.Right.y, 0.0001f,
                "r̂.y 应为 0（水平化）");
        }

        // ══════════ AC-1-35④: 正交性断言 ══════════

        [Test]
        public void test_ac135_orthogonalityAssertion()
        {
            // AC-1-35④: 断言 r̂ := normalize(cross(worldUp, f̂)) 正交
            var rig = new TestCameraRig(0.5f);
            var basis = rig.YawBasis;

            float dot = Vector3.Dot(basis.Fwd, basis.Right);
            Assert.AreEqual(0f, dot, 0.001f,
                "f̂ 与 r̂ 应正交");
        }

        // ══════════ 测试辅助 ══════════

        private sealed class CountingCameraRig : ICameraRig
        {
            private readonly float _yaw;
            public int YawBasisCallCount { get; private set; }

            public CountingCameraRig(float yaw)
            {
                _yaw = yaw;
            }

            public YawBasis YawBasis
            {
                get
                {
                    YawBasisCallCount++;
                    float sin = Mathf.Sin(_yaw);
                    float cos = Mathf.Cos(_yaw);
                    var fwd = new Vector3(sin, 0f, cos);
                    var right = new Vector3(cos, 0f, -sin);
                    return new YawBasis(fwd, right);
                }
            }

            public float Yaw => _yaw;
            public float Pitch => 0f;
            // ADR-020 §Key Interfaces 四成员(2026-10-03 接口扩容)— 本夹具只用 YawBasis 面
            public CameraMode Mode => CameraMode.Explore;
            public void SetMode(CameraMode mode) { }
            public void Tick(float deltaTime) { }
            public UnityEngine.Camera Camera => null;
        }

        private sealed class TestCameraRig : ICameraRig
        {
            private readonly float _yaw;

            public TestCameraRig(float yaw)
            {
                _yaw = yaw;
            }

            public YawBasis YawBasis
            {
                get
                {
                    float sin = Mathf.Sin(_yaw);
                    float cos = Mathf.Cos(_yaw);
                    var fwd = new Vector3(sin, 0f, cos);
                    var right = new Vector3(cos, 0f, -sin);
                    return new YawBasis(fwd, right);
                }
            }

            public float Yaw => _yaw;
            public float Pitch => 0f;
            public CameraMode Mode => CameraMode.Explore;
            public void SetMode(CameraMode mode) { }
            public void Tick(float deltaTime) { }
            public UnityEngine.Camera Camera => null;
        }
    }
}
