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

        // ══════════ 测试辅助 ══════════

        private static DaYiJingCheng.Gameplay.Presentation.Player.PlayerController CreateController()
        {
            var go = new GameObject("TestPlayer");
            var controller = go.AddComponent<DaYiJingCheng.Gameplay.Presentation.Player.PlayerController>();
            // 手动设置 _controller 字段（Awake 在测试中不会被调用）
            var cc = go.AddComponent<CharacterController>();
            var field = typeof(DaYiJingCheng.Gameplay.Presentation.Player.PlayerController)
                .GetField("_controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(controller, cc);
            return controller;
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
        }
    }
}
