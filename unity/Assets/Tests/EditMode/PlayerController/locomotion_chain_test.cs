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
using DaYiJingCheng.Sim.Contracts;   // Fix(AC-1-06 的派生量判据用)
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
            // AC-1-18(BLOCKING):「跨格那一帧断言 `v_target` 用的是**旧格**的乘数
            // (注入两格乘数不同的地貌,断言切换发生在**下一帧**)」。
            //
            // 🔴 **2026-10-03 第二轮评审前置 #4 修复** —— 初版**未真测跨格**:
            //    注释称「第二帧: 跨到格 1」,但实际调用传的是 `currentCellIndex = 0`
            //    (**与第一帧同一索引**)⇒ 所谓「跨格那帧」只是同索引调了两次,
            //    「用旧格乘数」是**重言式**。第三帧才传 1 ⇒ 从未模拟「跨格发生的那一帧」。
            //
            // 真实跨格夹具 = 让**位置**真的越过格边界(经 `CellTransitionDetector.CellFromPosition`),
            // 由位置派生格索引 ⇒ 这不是测试自己挑索引,而是位置驱动。
            var evaluator = new LocomotionEvaluator();
            var basis = new YawBasis(new Vector3(0, 0, 1), new Vector3(1, 0, 0));
            var kTerrainSpeeds = new[] { 1f, 0.5f };   // 格 0 = 1.0,格 1 = 0.5

            // ── 第 1 帧:起点在格 0 内(x = 0.4)──
            Vector3 posF1 = new Vector3(0.4f, 0f, 0f);
            int cellF1 = CellIndexOf(posF1);
            Assert.AreEqual(0, cellF1, "夹具前提:第 1 帧位置应落在格 0");
            float vTargetF1 = evaluator.ComputeVTarget(1f, kTerrainSpeeds, cellF1, basis);
            Assert.AreEqual(5f, vTargetF1, 0.001f, "格 0 ⇒ 乘数 1.0");

            // ── 第 2 帧:**位置真的跨过格边界**(x = 1.6 已进格 1)──
            //    但按 F-1-3 求值次序,v_target **先于** Move / 位置更新 ⇒ 本帧仍用**帧起始格**。
            Vector3 posF2 = new Vector3(1.6f, 0f, 0f);
            int cellAtFrameStartF2 = cellF1;                    // 帧起始格 = 上一帧末格
            int cellAfterMoveF2 = CellIndexOf(posF2);           // Move 后的新格
            Assert.AreEqual(1, cellAfterMoveF2,
                "夹具前提:第 2 帧的 Move **确实**把玩家送进了格 1(真跨格,非重言)");

            float vTargetF2 = evaluator.ComputeVTarget(1f, kTerrainSpeeds, cellAtFrameStartF2, basis);
            Assert.AreEqual(5f, vTargetF2, 0.001f,
                "跨格那一帧 v_target 仍用**旧格(格 0)**乘数 —— 切换发生在下一帧(AC-1-18 判据本体)");

            // ── 第 3 帧:帧起始格已是格 1 ⇒ 这才切到新乘数 ──
            float vTargetF3 = evaluator.ComputeVTarget(1f, kTerrainSpeeds, cellAfterMoveF2, basis);
            Assert.AreEqual(2.5f, vTargetF3, 0.001f,
                "下一帧才应用新格(格 1)乘数 —— 「切换发生在下一帧」");
        }

        /// <summary>
        /// 由**位置**派生格索引(非测试自选索引)—— 跨格夹具的驱动源。
        /// 与生产 `CellTransitionDetector.CellFromPosition` 同源,避免测试自造第二套格算法。
        /// </summary>
        private static int CellIndexOf(Vector3 position)
        {
            // 沿 +x 单轴演示;格边长取 CanonicalLattice = 1 m(ADR-015 §三)
            return CellTransitionDetector.CellFromPosition(position).X;
        }

        // ══════════ AC-1-06a: 差分神谕(抓手填常数) ══════════

        [Test]
        public void test_ac106a_differentialOracle()
        {
            // ⚠️ 2026-10-03 重定(评审 A3):原测**只查 `SpeedWalk > 0`** —— 而实测
            //    1 侧**零 `K_TERRAIN_MAX`/`K_CONTEXT_MAX` 概念**(它们是入参,1 是消费者),
            //    `DeriveMaxSpeed` 全库不存在 ⇒ 原测**无物可查**,非单纯偷懒。
            //
            //    按 story `:19` 的**所有者反转**(约束对象 = `LATTICE_SIZE`,归 6),
            //    判据面在 **6 侧**。本测(住**测试装配**,已引 `Sim`)做两件事:
            //    ① 验 1 侧的**接口形态** —— `kTerrain` 是**入参**,1 不自持派生量;
            //    ② 验 6 侧的**差分神谕** —— 独立第二路径扫表求 max,与 `WorldLatticeParams.KTerrainMax` 比对。

            // ① 接口形态:1 的公开面不得出现 K_TERRAIN_MAX / K_CONTEXT_MAX 之类派生量
            var evalType = typeof(LocomotionEvaluator);
            foreach (var f in evalType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                Assert.IsFalse(f.Name.Contains("KTerrainMax") || f.Name.Contains("KContextMax")
                               || f.Name.Contains("K_TERRAIN") || f.Name.Contains("K_CONTEXT"),
                    $"1 不得自持派生量字段:{f.Name}(F-1-1a 的所有者反转)");
            foreach (var pr in evalType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                Assert.IsFalse(pr.Name.Contains("KTerrainMax") || pr.Name.Contains("KContextMax"),
                    $"1 不得自持派生量属性:{pr.Name}");

            // ② 差分神谕(6 侧真派生):独立第二路径扫表求 max,与被测的 KTerrainMax 比对
            var table = new[]
            {
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(1, Fix.One, Fix.One),
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(3, Fix.One, Fix.One),
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(7, Fix.One, Fix.One),
            };
            // **独立第二路径**:直接扫数组求 max(不复用被测代码的求值 helper)
            int oracle = 0;
            foreach (var r in table) if (r.KSpeed > oracle) oracle = r.KSpeed;
            Assert.AreEqual(7, oracle, "独立路径 oracle 前提");

            var p = new DaYiJingCheng.Sim.World.WorldLatticeParams(
                latticeSizeMm: 20000, speedModeMax: 5, kContextMax: 2,
                maxDtMs: 100, safetyMargin: 2, kSpeedTable: table);

            Assert.AreEqual(oracle, p.KTerrainMax,
                "差分神谕:6 侧派生的 KTerrainMax 须 == 独立路径扫表的 max");
            Assert.AreEqual(5 * oracle * 2, p.SpeedMax,
                "SPEED_MAX = SPEED_MODE_MAX × K_TERRAIN_MAX × K_CONTEXT_MAX(GDD :511)");
        }

        // ══════════ AC-1-06b: 变异性 + 双向 ══════════

        [Test]
        public void test_ac106b_variabilityBidirectional()
        {
            // ⚠️ 2026-10-03 重定(评审 A3):原测**只查 `SpeedWalk > 0`** ⇒ 判据空转。
            //    真要求 = **变异性 + 双向**:抬表的一行 ⇒ 派生上界随动 ⇒ 装载期下界断言变红;
            //    删该行 ⇒ 恢复绿(排除「永久红断言冒充」)。
            //    实现面在 6 侧(`WorldLatticeParams` 构造的 F-6-1 守卫),本测(测试装配)验它。

            // 基线:小格 + 小表 ⇒ 通过
            var baseTable = new[]
            {
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(1, Fix.One, Fix.One),
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(2, Fix.One, Fix.One),
            };
            Assert.DoesNotThrow(() => new DaYiJingCheng.Sim.World.WorldLatticeParams(
                    latticeSizeMm: 10000, speedModeMax: 5, kContextMax: 2,
                    maxDtMs: 100, safetyMargin: 2, kSpeedTable: baseTable),
                "基线表 + 10 m 格 ⇒ 通过");

            // 变异性:注入一行 K_speed := 现上界 × 3.5(7 vs 2)⇒ 上界抬 ⇒ 同一格边长必红
            var mutatedTable = new[]
            {
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(1, Fix.One, Fix.One),
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(2, Fix.One, Fix.One),
                new DaYiJingCheng.Sim.World.TerrainSpeedRow(7, Fix.One, Fix.One),   // 注入行
            };
            Assert.Throws<ArgumentException>(() => new DaYiJingCheng.Sim.World.WorldLatticeParams(
                    latticeSizeMm: 10000, speedModeMax: 5, kContextMax: 2,
                    maxDtMs: 100, safetyMargin: 2, kSpeedTable: mutatedTable),
                "注入抬上界的行 ⇒ 装载期下界断言须红(变异性)");

            // 双向:删掉该注入行 ⇒ 恢复绿(排除永久红断言冒充)
            Assert.DoesNotThrow(() => new DaYiJingCheng.Sim.World.WorldLatticeParams(
                    latticeSizeMm: 10000, speedModeMax: 5, kContextMax: 2,
                    maxDtMs: 100, safetyMargin: 2, kSpeedTable: baseTable),
                "删除注入行 ⇒ 恢复绿(双向)");
        }

        // ══════════ AC-1-06c: AST 派生初始化判据 ══════════

        [Test]
        public void test_ac106c_astDerivationCheck_notRun()
        {
            // ⚠️ 2026-10-03 **降级为 NOT-RUN**(评审 A3)——
            //    AC-1-06c 要求「初始化式须为**派生调用**,数字字面量 = 构建失败」,
            //    其判据载体 = Roslyn 分析器。
            //    但 **ADR-024 §⑤ 明令本仓不引 Roslyn analyzer**
            //    (「生成器 = 编辑期 .NET 控制台工具 … Roslyn analyzer 的引入须另行照准」),
            //    且原测自陈「完整版需要 Roslyn 分析器」。
            //
            //    ⇒ **无载体** ⇒ 不得以空转断言冒充(原测只查 `SpeedWalk > 0`)。
            //    判据面在 6 侧:`AC-6-07` 的**值级可证伪守卫**已覆盖「派生而非手填」
            //    (改表的一行 ⇒ `KTerrainMax` 随动;见 `world_lattice_test.cs`)。
            Assert.Ignore(
                "NOT-RUN: AC-1-06c 的 AST 初始化式判据需 Roslyn analyzer," +
                "而 ADR-024 §⑤ 明令不引;判据面在 6 侧的 AC-6-07 值级守卫");
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
