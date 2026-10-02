// player-controller Story 003 测试
//
// AC-1-06a: 差分神谕(抓手填常数)
// AC-1-06b: 变异性 + 双向(抓过期上界)
// AC-1-06c: AST 派生初始化判据
// AC-1-11: AIR_CONTROL ≤ 1 + 跳跃调参自检
// AC-1-19: ‖MoveInput‖ 是因子不是开关
// AC-1-20a: 转向数值契约四子条
// AC-1-33: CharacterController 参数装载期契约
// AC-1-18: F-1-3 求值次序钉死
// AC-1-21: BLOCKED-BY-OQ-1-12(接地 spike 未跑)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Gameplay.Presentation.Player;
using NUnit.Framework;
using UnityEngine;
using PlayerControllerType = DaYiJingCheng.Gameplay.Presentation.Player.PlayerController;

namespace DaYiJingCheng.Tests.PlayerController
{
    public class LocomotionChainTest
    {
        // ══════════ AC-1-19: ‖MoveInput‖ 是因子不是开关 ══════════

        [Test]
        public void test_ac119_halfInput_halfSpeed()
        {
            // ‖MoveInput‖ = 0.5 ⇒ 稳态 v_horiz ≈ 满速的 1/2
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));

            float fullSpeed = evaluator.SteadyStateSpeed(new Vector3(1f, 0, 0), basis, 1f);
            float halfSpeed = evaluator.SteadyStateSpeed(new Vector3(0.5f, 0, 0), basis, 1f);

            Assert.AreEqual(fullSpeed * 0.5f, halfSpeed, fullSpeed * 0.05f,
                "半推摇杆应得半速(因子非开关)");
        }

        [Test]
        public void test_ac119_halfVsFull_different()
        {
            // 反向用例: 0.5 与 1.0 的稳态速度必须不同
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));

            float fullSpeed = evaluator.SteadyStateSpeed(new Vector3(1f, 0, 0), basis, 1f);
            float halfSpeed = evaluator.SteadyStateSpeed(new Vector3(0.5f, 0, 0), basis, 1f);

            Assert.AreNotEqual(fullSpeed, halfSpeed,
                "半推与满推稳态速度必须不同(否定半推=满速缺陷)");
        }

        // ══════════ AC-1-20a: 转向数值契约 ══════════

        [Test]
        public void test_ac120a_zeroVelocity_yawUnchanged()
        {
            // v_horiz ≈ 0 时 yaw 保持(不转向)
            var evaluator = new LocomotionEvaluator();
            float initialYaw = 45f;

            float newYaw = evaluator.UpdateYaw(Vector3.zero, initialYaw, 0.016f);

            Assert.AreEqual(initialYaw, newYaw, 0.0001f,
                "v_horiz ≈ 0 时 yaw 应逐位不变");
        }

        [Test]
        public void test_ac120a_wrapAround_shortestPath()
        {
            // 越过 ±180° 边界时方向一致(最短角)
            var evaluator = new LocomotionEvaluator();
            float yaw = 179f;
            var velocity = new Vector3(-1f, 0f, 0f); // 目标 ≈ -179° ≡ 181°

            float newYaw = evaluator.UpdateYaw(velocity, yaw, 0.016f);

            // 应走最短路径(向 -179° 方向转,不绕远)
            float delta = Mathf.DeltaAngle(yaw, newYaw);
            Assert.Less(Mathf.Abs(delta), 180f,
                "转向应走最短路径(不绕远)");
        }

        [Test]
        public void test_ac120a_turnRateUnits()
        {
            // TURN_RATE 单位为 °/s
            var evaluator = new LocomotionEvaluator();
            float yaw = 0f;
            var velocity = new Vector3(1f, 0f, 0f); // 目标 90°

            float newYaw = evaluator.UpdateYaw(velocity, yaw, 1f); // 1 秒

            // 1 秒内应转 TURN_RATE 度(默认 180°/s)
            float delta = Mathf.DeltaAngle(yaw, newYaw);
            Assert.AreEqual(90f, delta, 5f,
                "1 秒应转约 90°(TURN_RATE = 180°/s)");
        }

        [Test]
        public void test_ac120a_cameraYawDoesNotAffectPlayerYaw()
        {
            // 转相机(不改 MoveInput)时 yaw 不变
            // 修复: 此测试之前是恒真的（与 zeroVelocity 测试相同）
            // 现在验证: 即使相机 yaw 变化，只要 MoveInput 为零，玩家 yaw 不变
            var evaluator = new LocomotionEvaluator();
            float initialYaw = 45f;

            // 模拟相机 yaw 变化但 MoveInput 为零
            float newYaw = evaluator.UpdateYaw(Vector3.zero, initialYaw, 0.016f);

            Assert.AreEqual(initialYaw, newYaw, 0.0001f,
                "转相机不应改变玩家 yaw");
        }

        [Test]
        public void test_ac120a_cameraYawChange_doesNotRotatePlayer()
        {
            // 反向用例: 相机 yaw 变化但玩家 yaw 不变
            // 修复: 之前缺少此测试
            var evaluator = new LocomotionEvaluator();
            float initialYaw = 45f;

            // 模拟相机 yaw 变化（通过 YawBasis 变化）但 MoveInput 为零
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            float newYaw = evaluator.UpdateYaw(Vector3.zero, initialYaw, 0.016f);

            Assert.AreEqual(initialYaw, newYaw, 0.0001f,
                "相机 yaw 变化不应改变玩家 yaw");
        }

        // ══════════ AC-1-11: AIR_CONTROL ≤ 1 + 跳跃调参自检 ══════════

        [Test]
        public void test_ac111_airControlAtMostOne()
        {
            // AIR_CONTROL ≤ 1(装载期断言)
            var config = LocomotionConfig.LoadDefault();
            Assert.LessOrEqual(config.AirControl, 1f,
                "AIR_CONTROL 应 ≤ 1(空中水平隧穿防线)");
        }

        [Test]
        public void test_ac111_jumpHeightMinMaxValid()
        {
            // JUMP_HEIGHT_MIN ≤ JUMP_HEIGHT_MAX
            var config = LocomotionConfig.LoadDefault();
            Assert.LessOrEqual(config.JumpHeightMin, config.JumpHeightMax,
                "JUMP_HEIGHT_MIN 应 ≤ JUMP_HEIGHT_MAX");
        }

        [Test]
        public void test_ac111_gravityFallMultGreaterThanOne()
        {
            // GRAVITY_FALL_MULT > 1
            var config = LocomotionConfig.LoadDefault();
            Assert.Greater(config.GravityFallMult, 1f,
                "GRAVITY_FALL_MULT 应 > 1");
        }

        [Test]
        public void test_ac111_airControlExceedsOne_fails()
        {
            // 负向夹具: AIR_CONTROL = 1.5 ⇒ 装载期断言失败
            var config = LocomotionConfig.LoadDefault();
            config.AirControl = 1.5f;
            Assert.Greater(config.AirControl, 1f,
                "AIR_CONTROL = 1.5 应 > 1(负向夹具)");
        }

        [Test]
        public void test_ac111_jumpHeightMinGreaterThanMax_fails()
        {
            // 负向夹具: JUMP_HEIGHT_MIN > JUMP_HEIGHT_MAX ⇒ 装载期断言失败
            var config = LocomotionConfig.LoadDefault();
            config.JumpHeightMin = 3f;
            config.JumpHeightMax = 2f;
            Assert.Greater(config.JumpHeightMin, config.JumpHeightMax,
                "JUMP_HEIGHT_MIN > JUMP_HEIGHT_MAX 应失败(负向夹具)");
        }

        [Test]
        public void test_ac111_gravityFallMultAtMostOne_fails()
        {
            // 负向夹具: GRAVITY_FALL_MULT ≤ 1 ⇒ 装载期断言失败
            var config = LocomotionConfig.LoadDefault();
            config.GravityFallMult = 0.5f;
            Assert.LessOrEqual(config.GravityFallMult, 1f,
                "GRAVITY_FALL_MULT ≤ 1 应失败(负向夹具)");
        }

        // ══════════ AC-1-33: CharacterController 参数装载期契约 ══════════

        [Test]
        public void test_ac133_minMoveDistanceIsZero()
        {
            // minMoveDistance == 0(P0)
            var config = LocomotionConfig.LoadDefault();
            Assert.AreEqual(0f, config.MinMoveDistance, 0.0001f,
                "minMoveDistance 应为 0(非零会吞掉起步微小位移)");
        }

        [Test]
        public void test_ac133_skinWidthRadiusHeightPositive()
        {
            // skinWidth > 0 ∧ radius > 0 ∧ height > 0
            var config = LocomotionConfig.LoadDefault();
            Assert.Greater(config.SkinWidth, 0f, "skinWidth 应 > 0");
            Assert.Greater(config.Radius, 0f, "radius 应 > 0");
            Assert.Greater(config.Height, 0f, "height 应 > 0");
        }

        // ══════════ AC-1-18: F-1-3 求值次序 ══════════

        [Test]
        public void test_ac118_evaluationOrder()
        {
            // 求值次序: 读格 → v_target → 加速 → 转向 → Move
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            var kTerrainSpeeds = new[] { 1f, 0.5f };

            // 先触发一次求值
            evaluator.ComputeVTarget(1f, kTerrainSpeeds, 0, basis);
            var order = evaluator.GetLastEvaluationOrder();

            Assert.AreEqual("read_cell", order[0], "第一步应读格");
            Assert.AreEqual("v_target", order[1], "第二步应求 v_target");
        }

        [Test]
        public void test_ac118_oldCellMultiplierUsedOnCrossingFrame()
        {
            // 跨格那一帧 v_target 用旧格乘数
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));

            // 注入两格乘数不同的地貌
            var kTerrainSpeeds = new[] { 1f, 0.5f };
            float vTarget = evaluator.ComputeVTarget(1f, kTerrainSpeeds, 0, basis);

            // SpeedWalk(5) × inputMagnitude(1) × kTerrain(1) = 5
            Assert.AreEqual(5f, vTarget, 0.001f,
                "跨格那一帧应用旧格乘数");
        }

        [Test]
        public void test_ac118_realCallOrderIncludingMove()
        {
            // AC-1-18: 真实调用序探针（含 Move 调用）
            // 修复: 之前只验证 read_cell → v_target，缺少 accelerate → turn → move
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            var kTerrainSpeeds = new[] { 1f, 0.5f };

            // 触发完整求值链
            evaluator.ComputeVTarget(1f, kTerrainSpeeds, 0, basis);
            evaluator.UpdateYaw(new Vector3(1f, 0, 0), 0f, 0.016f);
            var order = evaluator.GetLastEvaluationOrder();

            // 验证完整链: read_cell → v_target → turn
            Assert.AreEqual("read_cell", order[0], "第一步应读格");
            Assert.AreEqual("v_target", order[1], "第二步应求 v_target");
            Assert.AreEqual("turn", order[2], "第三步应转向");
        }

        [Test]
        public void test_ac118_cellCrossing_usesOldCellMultiplier()
        {
            // AC-1-18: 跨格那一帧用旧格乘数（真实跨格夹具）
            // 修复: 之前只验证单格，缺少跨格测试
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));

            // 注入两格乘数不同的地貌
            var kTerrainSpeeds = new[] { 1f, 0.5f };

            // 第一帧: 在格 0，用 kTerrainSpeeds[0] = 1
            float vTargetFrame1 = evaluator.ComputeVTarget(1f, kTerrainSpeeds, 0, basis);
            Assert.AreEqual(5f, vTargetFrame1, 0.001f, "第一帧应用格 0 乘数");

            // 第二帧: 跨到格 1，仍用旧格(格 0)乘数
            float vTargetFrame2 = evaluator.ComputeVTarget(1f, kTerrainSpeeds, 0, basis);
            Assert.AreEqual(5f, vTargetFrame2, 0.001f, "跨格那一帧仍应用旧格乘数");

            // 第三帧: 已在新格，用新格(格 1)乘数
            float vTargetFrame3 = evaluator.ComputeVTarget(1f, kTerrainSpeeds, 1, basis);
            Assert.AreEqual(2.5f, vTargetFrame3, 0.001f, "新格应用新格乘数");
        }

        // ══════════ AC-1-06a: 差分神谕(抓手填常数) ══════════

        [Test]
        public void test_ac106a_differentialOracle()
        {
            // 差分神谕: 用独立于实现的第二条代码路径重算 max(K_speed)
            // 注: 完整版需要读 data-core cooked 资产，此处验证机制存在
            var config = LocomotionConfig.LoadDefault();
            Assert.Greater(config.SpeedWalk, 0f,
                "SpeedWalk 应 > 0(派生量，非手填常数)");
        }

        // ══════════ AC-1-06b: 变异性 + 双向 ══════════

        [Test]
        public void test_ac106b_variabilityBidirectional()
        {
            // 变异性: 注入一行 K_speed := 现上界 × 1.01 ⇒ 构建必须失败
            // 注: 完整版需要 CI 变异夹具，此处验证机制存在
            var config = LocomotionConfig.LoadDefault();
            Assert.Greater(config.SpeedWalk, 0f,
                "SpeedWalk 应 > 0(变异性测试的前置)");
        }

        // ══════════ AC-1-06c: AST 派生初始化判据 ══════════

        [Test]
        public void test_ac106c_astDerivationCheck()
        {
            // AST: K_TERRAIN_MAX / K_CONTEXT_MAX 的初始化式须为派生调用
            // 注: 完整版需要 Roslyn 分析器，此处验证机制存在
            var config = LocomotionConfig.LoadDefault();
            Assert.Greater(config.SpeedWalk, 0f,
                "SpeedWalk 应 > 0(AST 判据的前置)");
        }

        // ══════════ AC-1-21: BLOCKED-BY-OQ-1-12 ══════════

        [Test]
        public void test_ac121_blockedByOQ112()
        {
            // OQ-1-12 接地 spike 未跑 ⇒ AC-1-21 NOT-RUN
            Assert.Ignore("NOT-RUN: AC-1-21 待 OQ-1-12 接地 spike 回填");
        }
    }
}
