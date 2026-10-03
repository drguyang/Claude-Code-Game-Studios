// camera-viewpoint Story 004 测试 —— F-2-3 出臂 / F-2-4 收缩回弹 / 掩码与裁剪面
//
// AC-2-14: 收缩瞬时 / 回弹阻尼(非对称)
// AC-2-15: 碰撞掩码(恰好等于白名单)+ 近裁剪面不等式 + QueryTriggerInteraction.Ignore
// AC-2-16: 收缩无状态、每帧重算
// AC-2-25: 肩位常量几何 + 档位参数表闭集
//
// 权威来源: GDD F-2-3 / F-2-4 · R-2-3/4/6 · 组 5/6/7 · EC-2-1/2/3

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    public class CameraArmSolverTest
    {
        private static CameraArmParams P(float arm = 4f, float radius = 0.3f,
                                        float minDist = 0.5f, float recover = 4f) =>
            new CameraArmParams
            {
                ArmLen = arm, ShoulderLateral = 0.6f, ShoulderHeight = 1.5f, FovV = 60f,
                CamRadius = radius, CamMinDist = minDist, NearClip = 0.1f,
                RecoverSpeed = recover,
                // ⚠️ 掩码**不在此硬编码** —— 取生产默认(登记白名单),避免「夹具字面量自证」。
                CamCollideMask = CameraArmParams.DefaultCollideMask,
            };

        private static YawBasis Basis(float yaw = 0f)
            => new YawBasis(new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)),
                            new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw)));

        /// <summary>可编程碰撞缝。</summary>
        private sealed class FakeQuery : IArmCollisionQuery
        {
            public bool Hit;
            public float Distance;
            public int Calls;
            public QueryTriggerInteraction LastTrigger;
            public (bool, float) Cast(Vector3 o, Vector3 d, float r, float maxDist)
            { Calls++; return (Hit, Distance); }
        }

        // ══════════════ AC-2-14:收缩瞬时 / 回弹阻尼 ══════════════

        [Test]
        public void test_ac214a_retractionIsInstantaneous_sameFrame()
        {
            var q = new FakeQuery { Hit = true, Distance = 1.0f };   // 遮挡距离 1.0
            var s = new CameraArmSolver(P(arm: 4f), q);
            var sh = Vector3.zero; var dir = Vector3.forward;

            // ⚠️ 2026-10-03:「无遮挡」在此缝下**不可表达为 hit=false** ——
            //    GDD 的保守语义下 `false` ⇒ 回退 CAM_MIN_DIST(与「起点重叠」不可区分,见实现注释)。
            //    ⇒ 用「命中但距离 ≥ ARM_LEN + CAM_RADIUS」表达「几何远到不收缩」。
            q.Hit = true; q.Distance = 10f;    // d_block = clamp(10-0.3, .5, 4) = 4
            s.Step(sh, dir, 1f / 60f);
            Assert.AreEqual(4f, s.CurrentDistance, 1e-5f, "几何远 ⇒ 臂长 = ARM_LEN");

            // 遮挡突减 ⇒ **同一帧**即为 d_block(无插值)
            q.Hit = true; q.Distance = 1.0f;
            s.Step(sh, dir, 1f / 60f);

            float expected = Mathf.Clamp(1.0f - 0.3f, 0.5f, 4f);   // = 0.7
            Assert.AreEqual(expected, s.CurrentDistance, 1e-5f,
                "收缩路径**无插值** ⇒ 同一帧即为 d_block(AC-2-14①)");
        }

        [Test]
        public void test_ac214a_retractionUnaffectedByRecoverSpeed()
        {
            // 双值对照:RECOVER_SPEED = 0.5 vs 50.0 ⇒ 收缩帧的 d **逐位相等**
            var sh = Vector3.zero; var dir = Vector3.forward;

            float Run(float recover)
            {
                var q = new FakeQuery { Hit = true, Distance = 10f };   // 几何远 ⇒ 臂 = ARM_LEN
                var s = new CameraArmSolver(P(recover: recover), q);
                s.Step(sh, dir, 1f / 60f);
                q.Hit = true; q.Distance = 1.0f;    // 遮挡
                s.Step(sh, dir, 1f / 60f);
                return s.CurrentDistance;
            }

            Assert.AreEqual(Run(0.5f), Run(50.0f), 1e-6f,
                "收缩帧的 d 不得受 RECOVER_SPEED 影响(抓「收缩也走 lerp」的实现)");
        }

        [Test]
        public void test_ac214b_recoveryBoundedByRecoverSpeed()
        {
            var q = new FakeQuery { Hit = true, Distance = 1.0f };
            var s = new CameraArmSolver(P(recover: 4f), q);
            var sh = Vector3.zero; var dir = Vector3.forward;
            const float Dt = 1f / 60f;

            s.Step(sh, dir, Dt);
            float dBefore = s.CurrentDistance;

            // ⚠️ 2026-10-03:「遮挡解除」须用「命中**远处**」表达 ——
            //    保守语义下 `Hit=false` ⇒ 回退 CAM_MIN_DIST(**更短**),不是解除。
            q.Distance = 10f;
            s.Step(sh, dir, Dt);

            float delta = s.CurrentDistance - dBefore;
            Assert.LessOrEqual(delta, 4f * Dt + 1e-6f,
                "回弹 |Δd| 须 ≤ RECOVER_SPEED × dt(AC-2-14②)");
            Assert.Greater(delta, 0f, "遮挡解除 ⇒ 须回弹");
        }

        [Test]
        public void test_ac214c_asymmetry_retractionDeltaGreaterThanRecovery()
        {
            // 同一几何夹具下,收缩帧的 |Δd| **>** 回弹帧的 |Δd|(序断言,非数值)
            var sh = Vector3.zero; var dir = Vector3.forward;
            const float Dt = 1f / 60f;

            // ⚠️ 2026-10-03:回弹段须有**足够距离**否则 Δd 被 d_block 卡住
            //    (上一版 d_block=0.7 而回弹上限 4×1/60=0.067 ⇒ 两者都 0.067,序断言失败)。
            //    ⇒ 用「命中远处」表达臂伸满,再突减到中等遮挡,使回弹不受 d_block 限制。
            var q = new FakeQuery { Hit = true, Distance = 10f };   // 臂伸满 4
            var s = new CameraArmSolver(P(arm: 4f, recover: 4f), q);
            s.Step(sh, dir, Dt);
            float dFull = s.CurrentDistance;

            q.Distance = 3.0f;                  // d_block = clamp(3-0.3,.5,4) = 2.7 ⇒ 收缩 Δd = 1.3
            s.Step(sh, dir, Dt);
            float retractDelta = Mathf.Abs(s.CurrentDistance - dFull);

            q.Distance = 10f;                   // 遮挡解除 ⇒ 回弹,上限 4×Dt = 0.067
            float dAtRetract = s.CurrentDistance;
            s.Step(sh, dir, Dt);
            float recoverDelta = Math.Abs(s.CurrentDistance - dAtRetract);

            Assert.Greater(retractDelta, recoverDelta,
                $"非对称:收缩帧 |Δd|({retractDelta}) 须 > 回弹帧 |Δd|({recoverDelta})(AC-2-14③)");
        }

        // ══════════════ AC-2-15:掩码 / 裁剪面 / Trigger ══════════════

        [Test]
        public void test_ac215a_maskEqualsWhitelist_notSuperset()
        {
            // ⚠️ 判据 = **相等**,非包含;且期望值须自**登记处**取,不得另写字面量。
            // 🔴 **2026-10-03 评审修复(B3)**:初版用 `P()`(工厂内硬编码 0b1010)比同级字面量
            //    0b1010 ⇒ **夹具字面量自证**,从未读生产值;且生产默认恰为 `0`(空掩码 =
            //    该 AC 要防的穿墙形态),测试因工厂覆写而永远看不到它。
            //    现:① 期望取**登记常量**;② 断言**生产默认**(未覆写)即合法;
            //    ③ 补**两个真负夹具**(空掩码 / 含角色层)证明守卫可证伪。
            var p = P();
            Assert.AreEqual(CameraArmParams.DefaultCollideMask, p.CamCollideMask,
                "掩码须**恰好等于**登记白名单(AC-2-15①;期望取自登记处,非夹具字面量)");
            Assert.AreEqual(0, p.CamCollideMask & CameraArmParams.CharacterLayerMask,
                "掩码不得含角色层(否则贴墙时相机把玩家顶穿画面;EC-2-2)");
            Assert.AreNotEqual(0, p.CamCollideMask,
                "掩码**不得为空**(空掩码 ⇒ 永不收缩 ⇒ 穿墙)");

            // ① 生产默认(未经夹具覆写)须通过守卫 —— 厂默认即违例形态的回归
            new CameraArmParams().ValidateCollideMask();

            // ② 负向夹具:空掩码 ⇒ 红
            var empty = P(); empty.CamCollideMask = 0;
            var exEmpty = Assert.Throws<ArgumentException>(() => empty.ValidateCollideMask());
            StringAssert.Contains("空", exEmpty.Message, "空掩码须点名诊断");

            // ③ 负向夹具:含角色层 ⇒ 红
            var withChar = P();
            withChar.CamCollideMask = CameraArmParams.DefaultCollideMask |
                                      CameraArmParams.CharacterLayerMask;
            Assert.Throws<ArgumentException>(() => withChar.ValidateCollideMask(),
                "含角色层须红(AC-2-15① 的负向夹具)");
        }

        [Test]
        public void test_ac215b_nearClipChain_holds()
        {
            P().ValidateNearClipChain();   // 合法 ⇒ 不抛
        }

        [Test]
        public void test_ac215b_nearClipChain_violationThrows()
        {
            // 违反 NEAR_CLIP ≤ CAM_MIN_DIST − CAM_RADIUS ⇒ 抛,且错误串点名
            var bad = P(minDist: 0.5f, radius: 0.3f);
            bad.NearClip = 0.9f;           // 0.9 > 0.2
            var ex = Assert.Throws<ArgumentException>(() => bad.ValidateNearClipChain());
            StringAssert.Contains("NEAR_CLIP", ex.Message);

            // 违反 CAM_MIN_DIST ≥ CAM_RADIUS ⇒ 抛
            var bad2 = P(minDist: 0.2f, radius: 0.3f);
            Assert.Throws<ArgumentException>(() => bad2.ValidateNearClipChain());
        }

        [Test]
        public void test_ac215c_triggerInteraction_isIgnore()
        {
            // AC-2-15③:QueryTriggerInteraction 须为 Ignore(触发体不顶相机)
            // ⚠️ 真实现(Physics.SphereCast)须显式传 Ignore —— 默认值随重载不同 ⇒ 静默差异。
            //    判据 = 源码级:实现内出现 SphereCast 时须同现 QueryTriggerInteraction.Ignore。
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera");
            bool sawSphereCast = false, sawIgnore = false;
            foreach (var f in System.IO.Directory.GetFiles(src, "*.cs",
                         System.IO.SearchOption.AllDirectories))
            {
                // ⚠️ 2026-10-03:**须剥注释** —— 否则实现注释里提到的 "SphereCast"
                //    (记录 Unity 契约)会被误判为「真接线」⇒ 判据空转。
                string code = System.Text.RegularExpressions.Regex.Replace(
                    System.IO.File.ReadAllText(f), @"//.*?$", "",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                if (code.Contains("SphereCast")) sawSphereCast = true;
                if (code.Contains("QueryTriggerInteraction.Ignore")) sawIgnore = true;
            }
            // P0:真 SphereCast 未接线(经 IArmCollisionQuery 缝)⇒ 显式 NOT-RUN
            if (!sawSphereCast)
            {
                Assert.Ignore("NOT-RUN: 真 Physics.SphereCast 未接线(经 IArmCollisionQuery 缝)" +
                              "⇒ QueryTriggerInteraction.Ignore 判据不可执行;不借绿");
                return;
            }
            Assert.IsTrue(sawIgnore, "出现 SphereCast 处须显式传 QueryTriggerInteraction.Ignore");
        }

        // ══════════════ AC-2-16:无跨帧遮挡状态 ══════════════

        [Test]
        public void test_ac216_geometryAppears_nextFrameRetracts()
        {
            var q = new FakeQuery { Hit = true, Distance = 10f };
            var s = new CameraArmSolver(P(arm: 4f, minDist: 0.5f), q);
            var sh = Vector3.zero; var dir = Vector3.forward;

            s.Step(sh, dir, 1f / 60f);
            Assert.AreEqual(4f, s.CurrentDistance, 1e-5f, "先无遮挡(几何远)");

            // 几何体**在收缩之后**出现 ⇒ **下一帧**即命中并收缩(无需额外状态)
            q.Hit = true; q.Distance = 0.6f;   // d_block = clamp(0.6-0.3, 0.5, 4) = 0.5
            s.Step(sh, dir, 1f / 60f);

            Assert.AreEqual(0.5f, s.CurrentDistance, 1e-5f,
                "几何体出现后**单帧内**收敛到 CAM_MIN_DIST(AC-2-16)");
        }

        [Test]
        public void test_ac216_noCrossFrameOcclusionState()
        {
            // 判据精确表述:**不得存在跨帧保存的「命中结果 / 遮挡标志 / d_target」**;
            // `CurrentDistance`(回弹积分变量)是**允许的**。
            var t = typeof(CameraArmSolver);
            foreach (var f in t.GetFields(System.Reflection.BindingFlags.NonPublic |
                                          System.Reflection.BindingFlags.Instance))
            {
                var n = f.Name.ToLowerInvariant();
                Assert.IsFalse(n.Contains("blocked") || n.Contains("hittest") ||
                               n.Contains("wasblock") || n.Contains("dtarget") ||
                               n.Contains("occlu"),
                    $"AC-2-16:不得有跨帧遮挡状态字段(实得 {f.Name})");
            }

            // 机械前提:每帧**从头**解 d_block ⇒ 源码内不得出现跨帧分支
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraArmSolver.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.IsFalse(code.Contains("wasBlocked") || code.Contains("blockedThisFrame"),
                "F-2-4:每帧从头解 d_block,不得跨帧缓存遮挡");
        }

        // ══════════════ AC-2-25:肩位常量几何 / 档位闭集 ══════════════

        [Test]
        public void test_ac225a_shoulderIsConstantGeometry_notDynamic()
        {
            // 越肩偏移**无动态解算**:只读常量 + basis,不读速度/时间/场景
            var q = new FakeQuery();
            var p = P();
            var s = new CameraArmSolver(p, q);
            var anchor = new Vector3(1f, 0f, 2f);
            var basis = Basis(0.7f);

            var shoulder = s.Shoulder(anchor, basis);

            // 期望 = anchor + up×H + right×L(纯常量几何)
            var expected = anchor + Vector3.up * p.ShoulderHeight + basis.Right * p.ShoulderLateral;
            Assert.AreEqual(expected.x, shoulder.x, 1e-6f);
            Assert.AreEqual(expected.y, shoulder.y, 1e-6f);
            Assert.AreEqual(expected.z, shoulder.z, 1e-6f);
        }

        [Test]
        public void test_ac225a_shoulderUnaffectedByCamRadius()
        {
            // 🔴 **反空转夹具(订正①)**:改 `CAM_RADIUS` 而 `SHOULDER_LATERAL` 不变
            //    ⇒ 肩位**纹丝不动**(原形态 `f̂ × CAM_RADIUS × SHOULDER_LATERAL` 下会静默移动取景)
            var basis = Basis(0.3f);
            var anchor = new Vector3(0f, 0f, 0f);

            var p1 = P(radius: 0.3f);
            var p2 = P(radius: 5.0f);      // 碰撞半径大改
            var s1 = new CameraArmSolver(p1, new FakeQuery());
            var s2 = new CameraArmSolver(p2, new FakeQuery());

            var a = s1.Shoulder(anchor, basis);
            var b = s2.Shoulder(anchor, basis);

            Assert.AreEqual(a.x, b.x, 1e-6f, "CAM_RADIUS 变化**不得**影响肩位(订正①)");
            Assert.AreEqual(a.z, b.z, 1e-6f, "CAM_RADIUS 变化不得影响肩位");
        }

        [Test]
        public void test_ac225b_noSecondModeBranch_inArmPath()
        {
            // AC-2-25② 的静态半边:臂计算/收缩函数体内**零档位分支**
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "CameraArmSolver.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);

            Assert.IsFalse(code.Contains("switch (") && code.Contains("Mode"),
                "臂计算路径不得有 switch(mode) —— 机器是**同一台**,档位只改参数(R-2-1)");
            Assert.IsFalse(code.Contains("if (_mode") || code.Contains("if (mode =="),
                "臂计算路径不得有档位分支(AC-2-25②)");
        }

        [Test]
        public void test_ac225c_orderingRelations()
        {
            // 序关系(**非数值断言**):ARM_LEN_TREATMENT < ARM_LEN_EXPLORE ∧
            // SHOULDER_LATERAL_TREATMENT < SHOULDER_LATERAL_EXPLORE
            // ⚠️ 数值留白 ⇒ 按「取值一旦存在即被守住」口径:
            //    此处用**注入的档位表**验序关系形状;真表缺键 ⇒ NOT-RUN(AC-2-21 EXTERNAL)。
            var explore = new CameraArmParams { ArmLen = 4f, ShoulderLateral = 0.6f };
            var treatment = new CameraArmParams { ArmLen = 2f, ShoulderLateral = 0.3f };

            Assert.Less(treatment.ArmLen, explore.ArmLen, "ARM_LEN_TREATMENT < ARM_LEN_EXPLORE");
            Assert.Less(treatment.ShoulderLateral, explore.ShoulderLateral,
                "SHOULDER_LATERAL_TREATMENT < SHOULDER_LATERAL_EXPLORE");

            // 真表在库前,该序关系只对注入表成立 ⇒ 显式标注
            Assert.Ignore("NOT-RUN(部分):序关系已对**注入表**成立;真档位表(组 5)未在库 ⇒ " +
                          "AC-2-21(EXTERNAL)覆盖「取值是否存在」;不借绿");
        }

        // ══════════════ F-2-4 未命中语义:false ⇒ ARM_LEN(GDD 主用例)══════════════

        [Test]
        public void test_f24_miss_returnsArmLen_openWorldNoOcclusion()
        {
            // 🔴 **2026-10-03 评审修复(B1 · 用户裁定「按 GDD 主用例改」)**:
            //    GDD F-2-4 主规则 = 「**未命中 ⇒ d_raw := ARM_LEN**」。
            //    开放世界最常见情形(背后 4 m 内无墙)⇒ SphereCast 返回 false ⇒ 臂长须为 **ARM_LEN**(远),
            //    而非初版的 CAM_MIN_DIST(0.5 m,≈ 贴脸第一人称,破坏 ADR-020 §三 越肩)。
            var q = new FakeQuery { Hit = false, Distance = 0f };
            var s = new CameraArmSolver(P(arm: 4f), q);
            s.Step(Vector3.zero, Vector3.forward, 1f / 60f);

            // d_raw := ARM_LEN(4.0)⇒ d_block = clamp(4.0 − CAM_RADIUS(0.3), 0.5, 4) = 3.7
            Assert.AreEqual(3.7f, s.CurrentDistance, 1e-5f,
                "未命中 ⇒ d_raw := ARM_LEN ⇒ d_block = ARM_LEN − CAM_RADIUS(GDD F-2-4 主用例;" +
                "≠ CAM_MIN_DIST ⇒ 开放世界无遮挡时不塌成贴脸)");
            Assert.Greater(s.CurrentDistance, 1f,
                "未命中须给**远**臂长(初版 false ⇒ CAM_MIN_DIST 的回归守卫)");
        }

        [Test]
        public void test_f24_missThenHit_retractsToBlockedDistance()
        {
            // 未命中(臂伸满)后遇到几何 ⇒ 单帧收缩到 d_block(证明 false 不是被当作「贴到最近」)
            var q = new FakeQuery { Hit = false, Distance = 0f };
            var s = new CameraArmSolver(P(arm: 4f), q);
            s.Step(Vector3.zero, Vector3.forward, 1f / 60f);
            Assert.AreEqual(3.7f, s.CurrentDistance, 1e-5f, "先:未命中 ⇒ ARM_LEN − CAM_RADIUS");

            q.Hit = true; q.Distance = 1.0f;   // 遮挡出现
            s.Step(Vector3.zero, Vector3.forward, 1f / 60f);
            Assert.AreEqual(0.7f, s.CurrentDistance, 1e-5f,   // clamp(1.0−0.3, 0.5, 4)
                "后:命中 1.0 ⇒ 单帧收缩到 d_block(false 路径不得干扰命中路径)");
        }

        // ══════════════ F-2-3 订正②:ê_view 手性(R(yaw,pitch),绕局部右轴)══════════════

        [Test]
        public void test_ac225_viewDir_matchesRodrigues_positivePitch()
        {
            // 🔴 **2026-10-03 评审修复(B2)**:GDD F-2-3 订正② 钉死 ê_view = R(yaw,pitch)×ê_back,
            //    R = 绕世界 +Y 转 yaw 后绕**局部右轴**转 pitch;正 pitch = 俯 ⇒ ê_view.y = +sinθ
            //    (与 003 的 GetCameraPosition 的 +sinPitch 一致)。初版符号相反(y = −sinθ)。
            var basis = Basis(0f);          // yaw=0 ⇒ f̂=(0,0,1), r̂=(1,0,0)
            var s = new CameraArmSolver(P(), new FakeQuery());

            var v0 = s.ViewDir(basis, 0f);
            Assert.AreEqual(0f, v0.y, 1e-6f, "pitch=0 ⇒ 水平(零参考)");
            Assert.AreEqual(-1f, v0.z, 1e-6f, "ê_view 与 f̂ 反向(z:−cos0=−1)");

            var v30 = s.ViewDir(basis, 30f);
            float sin30 = Mathf.Sin(30f * Mathf.Deg2Rad);
            float cos30 = Mathf.Cos(30f * Mathf.Deg2Rad);
            Assert.AreEqual(sin30, v30.y, 1e-6f,
                "正 pitch(俯)⇒ ê_view.y = +sinθ(GDD 构造;初版 −sinθ = 相机移到肩下方,B2)");
            Assert.AreEqual(-cos30, v30.z, 1e-6f, "ê_view.z = −cosθ");
            Assert.AreEqual(0f, v30.x, 1e-6f, "yaw=0 ⇒ x 分量 0");

            // 负 pitch(仰)⇒ y 为负(仰望天空)
            var vNeg = s.ViewDir(basis, -30f);
            Assert.Less(vNeg.y, 0f, "负 pitch(仰)⇒ ê_view.y < 0");
        }

        [Test]
        public void test_ac225_viewDir_handnessUnderYaw_rotatesWithLocalRightAxis()
        {
            // 换 yaw ⇒ 视线须**绕世界 +Y** 随之旋转(而非仅翻转 x/z),且恒为单位向量、y 只由 pitch 定
            var s = new CameraArmSolver(P(), new FakeQuery());
            foreach (var yaw in new[] { 0f, Mathf.PI / 3f, Mathf.PI, 4f * Mathf.PI / 3f })
            {
                var v = s.ViewDir(Basis(yaw), 25f);
                Assert.AreEqual(1f, v.magnitude, 1e-5f, $"ê_view 须为单位向量(yaw={yaw})");
                float expectedY = Mathf.Sin(25f * Mathf.Deg2Rad);
                Assert.AreEqual(expectedY, v.y, 1e-5f,
                    $"ê_view.y 只由 pitch 定,与 yaw 无关(yaw={yaw})");
            }
        }

        // ══════════════ AC-2-25④:ARM_LEN > 0 装载期守卫(B4)══════════════

        [Test]
        public void test_ac225d_armLenPositive_planeNotFirstPerson()
        {
            // ARM_LEN > 0(= 0 即实质第一人称,平面禁用 —— F-2-3 失效模式)
            // 🔴 **2026-10-03 评审修复(B4)**:初版只对**夹具字面量**自证;现断言**生产默认**
            //    经装载期守卫,且**负向夹具**(ARM_LEN = 0)必红。
            new CameraArmParams().ValidateArmLen();   // 生产默认须合法

            var zero = P(arm: 0f);
            Assert.Throws<ArgumentException>(() => zero.ValidateArmLen(),
                "ARM_LEN = 0 ⇒ 装载期守卫须拒(AC-2-25④ 的负夹具)");

            var neg = P(arm: -1f);
            Assert.Throws<ArgumentException>(() => neg.ValidateArmLen(), "ARM_LEN < 0 同样非法");
        }
    }
}
