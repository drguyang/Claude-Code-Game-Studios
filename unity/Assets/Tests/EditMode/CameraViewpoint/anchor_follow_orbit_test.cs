// camera-viewpoint Story 003 测试 —— F-2-1 锚跟随(半隐式 + 子步)/ dt 钳位 / yaw-pitch 解耦
//
// AC-2-11: 锚跟随过冲有界(带容差的不变量)
// AC-2-12: dt 尖峰钳位(位移预算面)+ MAX_DT 单一来源
// AC-2-13: yaw/pitch 解耦(pitch 触界不串 yaw)
// EC-2-4:  超距吸附
// EC-2-5:  unscaled dt(不引入冻结分支)
//
// 权威来源: GDD F-2-1 · R-2-3 · 组 1/5b/5c/6 · EC-2-4/5

using System;
using DaYiJingCheng.Gameplay.Presentation.Camera;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.CameraViewpoint
{
    public class AnchorFollowOrbitTest
    {
        /// <summary>合法 K_speed 基准表(含基准地貌 1;与 world_lattice_test 同形)。</summary>
        private static TerrainSpeedRow[] Table() => new[]
        {
            new TerrainSpeedRow(1, Fix.FromRational(3, 2), Fix.FromRational(3, 2)),
            new TerrainSpeedRow(2, Fix.One, Fix.One),
            new TerrainSpeedRow(5, Fix.FromRational(4, 3), Fix.One),
        };

        /// <summary>系统 1 的装载常量(MaxDtMs 的**唯一来源实体**)。</summary>
        private static WorldLatticeParams Lattice(int maxDtMs = 100) =>
            new WorldLatticeParams(
                latticeSizeMm: 20000, speedModeMax: 5, kContextMax: 2,
                maxDtMs: maxDtMs, safetyMargin: 2, kSpeedTable: Table());

        /// <summary>
        /// 夹具配置 —— **经单一来源工厂**取 `MaxDtMs`(不再自持字面量)。
        /// </summary>
        private static AnchorFollowConfig Cfg(float omega = 8f, float snap = 50f)
        {
            var cfg = AnchorFollowConfig.FromWorldLattice(Lattice());
            cfg.AnchorResponse = omega;
            cfg.TeleportSnapDist = snap;
            cfg.AnchorOvershootEps = 1e-2f;
            cfg.AnchorSpeedMax = 1000f;
            return cfg;
        }

        // ══════════════ AC-2-11:过冲有界(带容差)══════════════

        [Test]
        public void test_ac211_noOvershoot_beyondEps()
        {
            // 阶跃输入:玩家瞬移到吸附距离**以内** ⇒ 平滑趋近,过冲 ≤ EPS
            var cfg = Cfg();
            var f = new AnchorFollower(cfg);
            var target = new Vector3(10f, 0f, 0f);   // 距原点 10 < snap 50

            float worst = float.MaxValue;
            for (int i = 0; i < 600; i++)
            {
                f.Step(target, 1f / 60f);
                // e_k := dot(P_player − anchor, v_anchor) —— 趋近方向不反转
                float e = Vector3.Dot(target - f.Anchor, f.Velocity);
                worst = Mathf.Min(worst, e);
            }

            Assert.GreaterOrEqual(worst, -cfg.AnchorOvershootEps,
                $"过冲量须 ≤ ANCHOR_OVERSHOOT_EPS(实得最差 {worst};" +
                "AC-2-11 的容差形态 —— 离散积分末段允许极小负值)");
        }

        [Test]
        public void test_ac211_convergesToTarget()
        {
            var f = new AnchorFollower(Cfg());
            var target = new Vector3(5f, 0f, 0f);
            for (int i = 0; i < 600; i++) f.Step(target, 1f / 60f);

            Assert.Less(Vector3.Distance(f.Anchor, target), 0.05f, "须收敛到目标");
        }

        [Test]
        public void test_ac211_stabilityHolds_atHugeOmega()
        {
            // ω 超常大 ⇒ 须有界(不 NaN / 不 Infinity)
            var cfg = Cfg(omega: 1e4f);
            var f = new AnchorFollower(cfg);
            var target = new Vector3(1f, 0f, 0f);

            for (int i = 0; i < 300; i++) f.Step(target, 1f / 60f);

            Assert.IsFalse(float.IsNaN(f.Anchor.x) || float.IsInfinity(f.Anchor.x),
                "ω=1e4 不得发散");
            Assert.Less(Mathf.Abs(f.Anchor.x), 10f, $"ω=1e4 下锚位须有界(实得 {f.Anchor.x})");
        }

        [Test]
        public void test_ac211_semiImplicitOrder_notForwardEuler()
        {
            // 🔴 **反空转夹具(2026-10-03 修正)**:
            //    ⚠️ 初版用「ω=1e4 发散」作判据 —— **实测无效**:
            //    子步机制保证 `ω·h ≤ 0.5`,而该条件下**前向欧拉也稳定**
            //    (实算:ω=1e4,dt=1/60 ⇒ n=334,h≈5e-5,ω·h≈0.499)
            //    ⇒ **稳定性区分不出两种积分器**;要区分需 `ω·h > 1`,而那要求**子步失效**,
            //    与组 5c 矛盾。**这是 story 原文「写反 ⇒ 大 ω 发散」的隐含前提失效。**
            //
            //    ⇒ 改用**首帧位移**判据(两行顺序的**直接**后果):
            //    半隐式「先更新速度」⇒ 首帧即动;前向欧拉「先更新位置(用旧速度=0)」⇒ **首帧位移为零**。
            //    差异量级 `1e-2 ~ 1e-1`(ω=8 时 diff≈1.8e-2),远高于浮点噪声 ⇒ 可测。
            var cfg = Cfg(omega: 8f);
            var f = new AnchorFollower(cfg);
            var target = new Vector3(10f, 0f, 0f);

            f.Step(target, 1f / 60f);

            Assert.Greater(f.Anchor.magnitude, 1e-3f,
                "半隐式「先更新速度」⇒ **首帧即有位移**;" +
                "若首帧位移为 0 = 前向欧拉(先更新位置,用的是旧速度 0)");
        }

        // ══════════════ AC-2-12:dt 尖峰钳位(位移预算面)══════════════

        [Test]
        public void test_ac212a_dtSpike_clampedToBudget()
        {
            // 注入 dt > MAX_DT 的一帧 ⇒ 单帧位移 ≤ ANCHOR_SPEED_MAX × MAX_DT
            var cfg = Cfg(snap: 1e6f);          // 关掉吸附,隔离钳位面
            var f = new AnchorFollower(cfg);
            var target = new Vector3(100f, 0f, 0f);

            f.Step(target, dtSeconds: 10f);      // 巨大 dt(远超 MAX_DT=0.1s)
            float singleFrameDisp = f.Anchor.magnitude;

            float budget = cfg.AnchorSpeedMax * (cfg.MaxDtMs / 1000f);
            Assert.LessOrEqual(singleFrameDisp, budget,
                $"单帧位移须 ≤ ANCHOR_SPEED_MAX × MAX_DT(实得 {singleFrameDisp},预算 {budget})");
        }

        [Test]
        public void test_ac212a_hugeDt_equalsClampedDt()
        {
            // 差分重算:真 dt=10s 的一帧 ≡ dt=MAX_DT 的一帧(**不要求逐位**,带容差)
            var cfg = Cfg(snap: 1e6f);
            var fHuge = new AnchorFollower(cfg);
            var fClamped = new AnchorFollower(cfg);
            var target = new Vector3(50f, 0f, 0f);

            fHuge.Step(target, 10f);
            fClamped.Step(target, cfg.MaxDtMs / 1000f);

            Assert.AreEqual(fClamped.Anchor.x, fHuge.Anchor.x, cfg.AnchorOvershootEps,
                "大 dt 的一帧须等价于 dt := MAX_DT(AC-2-12①;不要求逐位)");
        }

        [Test]
        public void test_ac212b_maxDt_singleSource_notSecondDefinition()
        {
            // AC-2-12②:MAX_DT 须与系统 1 的装载常量**同源**(读同一实体),2 不得自定义第二份。
            // 🔴 **2026-10-03 评审修复(B1)**:初版只断言「字段可写 + 两注入互异 + 另一类型有
            //    int 字段」—— **没有一条能证伪「2 侧自造第二份」**(AC 自陈的负向夹具会通过)。
            //    现判据分三面:

            // ① **真来源面**:装载常量改值 ⇒ 2 侧消费值**随动**(同源 = 读同一实体)
            var cfg100 = AnchorFollowConfig.FromWorldLattice(Lattice(maxDtMs: 100));
            var cfg150 = AnchorFollowConfig.FromWorldLattice(Lattice(maxDtMs: 150));
            Assert.AreEqual(100, cfg100.MaxDtMs, "须读系统 1 的 MaxDtMs(100)");
            Assert.AreEqual(150, cfg150.MaxDtMs,
                "装载常量改值 ⇒ 2 侧消费值须**随动**(单一来源的机械含义:读同一实体)");
            Assert.AreNotEqual(cfg100.MaxDtMs, cfg150.MaxDtMs, "不同装载 ⇒ 不同值(非同源则恒同)");

            // ② **消费点真读该字段**:钳位行为随装载常量变化
            //    (若实现改读数第二份常量,下面两行的位移预算会与装载值脱钩 ⇒ 红)
            cfg100.TeleportSnapDist = 1e6f; cfg100.AnchorSpeedMax = 1000f;
            cfg150.TeleportSnapDist = 1e6f; cfg150.AnchorSpeedMax = 1000f;
            var f100 = new AnchorFollower(cfg100);
            var f150 = new AnchorFollower(cfg150);
            var target = new Vector3(100f, 0f, 0f);
            f100.Step(target, dtSeconds: 10f);
            f150.Step(target, dtSeconds: 10f);
            Assert.Greater(f150.Anchor.magnitude, f100.Anchor.magnitude,
                "消费值须真来自装载常量:MaxDtMs 大 ⇒ 单帧位移预算大(若为第二份常量则两者相同 ⇒ 红)");

            // ③ **负向夹具(反空转)**:`AnchorFollowConfig` 内**不得**存在 `MAX_DT` 的
            //    **数值字面量默认**(复制值形态 —— GDD 组 6 点名的静默失配)。
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "AnchorFollower.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.IsFalse(
                System.Text.RegularExpressions.Regex.IsMatch(code, @"MaxDtMs\s*=\s*\d"),
                "AnchorFollowConfig 内不得有 MaxDtMs 的字面量默认(2 侧第二份常量);" +
                "唯一装载路径 = FromWorldLattice(WorldLatticeParams.MaxDtMs)");

            // 机械前提:WorldLatticeParams.MaxDtMs 存在且为 int 毫秒(同量纲)
            var f = typeof(WorldLatticeParams).GetField("MaxDtMs");
            Assert.IsNotNull(f, "WorldLatticeParams.MaxDtMs 须存在(单一装载常量)");
            Assert.AreEqual(typeof(int), f.FieldType, "MAX_DT 为 int(毫秒)");
        }

        // ══════════════ AC-2-13:yaw / pitch 解耦 ══════════════

        [Test]
        public void test_ac213_pitchClamped_yawStillAccumulates()
        {
            // 先压到底(pitch → PITCH_MAX),保持 Look.y 继续施压,再抬回水平
            // ⇒ 净 yaw 变化 = 施加的 Look.x 积分(容差 YAW_BASIS_EPS)
            var go = new GameObject("rigOrbit");
            var rig = go.AddComponent<CameraRig>();

            const float Dt = 1f / 60f;

            // 压到底(经 GDD 式 lookY × SENS_Y × dt;默认 pitch=30 ⇒ 需足够帧数到 PITCH_MAX)
            for (int i = 0; i < 400; i++) rig.ApplyOrbit(lookX: 0f, lookY: 100f, dtSeconds: Dt);
            Assert.AreEqual(CameraRig.PITCH_MAX, rig.Pitch, 1e-3f, "pitch 须压到 PITCH_MAX");

            float yawBefore = rig.Yaw;
            const float Dx = 0.1f;
            const int N = 10;

            // pitch 已钳死,继续施压 + 施加 yaw
            for (int i = 0; i < N; i++) rig.ApplyOrbit(lookX: Dx, lookY: 100f, dtSeconds: Dt);
            float yawAfter = rig.Yaw;

            // 🔴 **2026-10-03 评审修复(A1)**:期望值须**由规格推导** ——
            //    GDD F-2-2 输入侧式 `yaw += Look.x × SENS × dt`;初版期望 `Dx × N`
            //    恰好等于「丢弃 dt 与 SENS」的实现 ⇒ **把非规格实现钉死**
            //    (按规格补回 dt/SENS 后该测反而变红)。
            float expected = Dx * CameraRig.LOOK_SENS_X * Dt * N;
            Assert.AreEqual(expected, yawAfter - yawBefore, CameraRig.YAW_BASIS_EPS,
                "pitch 触界被钳后 yaw 须**照常累积**且 = Look.x × SENS × dt × N(AC-2-13 解耦, 规格式)");

            UnityEngine.Object.DestroyImmediate(go);
        }

        // ══════════════ EC-2-4:超距吸附 ══════════════

        [Test]
        public void test_ec24_beyondSnap_teleportsAndZeroesVelocity()
        {
            var cfg = Cfg(snap: 50f);
            var f = new AnchorFollower(cfg);
            // 先给一点速度
            f.Step(new Vector3(10f, 0f, 0f), 1f / 60f);

            var far = new Vector3(100f, 0f, 0f);   // 距锚 > 50 ⇒ 吸附
            f.Step(far, 1f / 60f);

            Assert.AreEqual(far.x, f.Anchor.x, 1e-5f, "超距须**直接吸附**");
            Assert.AreEqual(0f, f.Velocity.magnitude, 1e-6f, "吸附时 v := 0(不做平滑追赶)");
        }

        [Test]
        public void test_ec24_withinSnap_doesNotTeleport()
        {
            var cfg = Cfg(snap: 50f);
            var f = new AnchorFollower(cfg);
            var near = new Vector3(10f, 0f, 0f);
            f.Step(near, 1f / 60f);

            Assert.Less(f.Anchor.magnitude, 10f, "吸附距离**以内** ⇒ 平滑趋近,不瞬移");
        }

        // ══════════════ EC-2-5:unscaled dt(无冻结分支)══════════════

        [Test]
        public void test_ec25_noFreezeBranch_onZeroTimeScale()
        {
            // EC-2-5 A 段:timeScale ≠ 1 时相机照常以 unscaledDeltaTime 跑;
            // **不得**引入「暂停时冻结积分」分支。
            // 判据:同一 dt 序列 ⇒ 与 timeScale 无关(Step 只收 dt,不读 timeScale)。
            var f1 = new AnchorFollower(Cfg());
            var f2 = new AnchorFollower(Cfg());
            var target = new Vector3(20f, 0f, 0f);

            for (int i = 0; i < 100; i++)
            {
                f1.Step(target, 1f / 60f);
                f2.Step(target, 1f / 60f);   // 相同 dt ⇒ 相同结果(无 timeScale 分支)
            }
            Assert.AreEqual(f1.Anchor.x, f2.Anchor.x, 1e-6f,
                "相同 dt 序列 ⇒ 相同结果(Step 不读 timeScale,无冻结分支)");

            // 机械前提:AnchorFollower 内不得出现 timeScale 引用
            string src = System.IO.Path.Combine(Application.dataPath,
                "Gameplay.Presentation", "Camera", "AnchorFollower.cs");
            string code = System.Text.RegularExpressions.Regex.Replace(
                System.IO.File.ReadAllText(src), @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            Assert.IsFalse(code.Contains("timeScale"),
                "EC-2-5:AnchorFollower 不得引用 timeScale(否则有冻结分支的入口)");
        }

        // ══════════════ 组 5c:n/h 是派生量非旋钮 ══════════════

        [Test]
        public void test_group5c_substepCount_isDerived_notTunable()
        {
            var cfg = Cfg(omega: 8f);
            var f = new AnchorFollower(cfg);

            // n := ceil(dt / (0.5/ω));ω=8 ⇒ maxH=0.0625 ⇒ dt=0.1 ⇒ n=2
            Assert.AreEqual(2, f.SubstepCount(0.1f), "n 须由公式派生");
            Assert.AreEqual(1, f.SubstepCount(0.01f), "小 dt ⇒ n=1(退化为半隐式欧拉)");

            // 机械前提:配置类内**不得**有 n / h 旋钮(组 5c:把它们当旋钮 = 允许 ω·h > 0.5)
            foreach (var fld in typeof(AnchorFollowConfig).GetFields())
                Assert.IsFalse(fld.Name == "SubstepCount" || fld.Name == "SubstepH" ||
                               fld.Name == "N" || fld.Name == "H",
                    $"组 5c:n/h 是派生量,不得为旋钮(实得字段 {fld.Name})");
        }
    }
}
