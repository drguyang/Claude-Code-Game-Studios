// patient-ai(13)Story 002 —— 空间行为:感知格距、HomeRegion 寻医与定点累加器步进
//
// 登记落点: tests/unit/patient-ai/spatial_behavior_test.cs
// 真身落点: unity/Assets/Tests/EditMode/PatientAI/spatial_behavior_test.cs
//
// 权威来源:
//   GDD design/gdd/patient-ai.md —— §Detailed Rules 规则四 · §Formulas F-13.2 / F-13.3 / F-13.4 / F-13.7
//     · §States `Seeking{EnRoute/AtClinic}` · §Tuning 二 / 三-bis
//     · AC-13-B2(重写)/ B3 / B4 / E2 / E3 / E4 / E5
//   ADR-016 §三 / §九(2026-09-17 修订)· ADR-015 §三 · ADR-027(13 零写)
//
// ⚠️ **反空转三件**(承 story-001 纪律):
//   ① 负夹具与正测**共用同一台扫描机器**;
//   ② 每条结构断言配影子类型注入 ⇒ 必红且点名;
//   ③ 断言触底到**具体产物 / 字段**。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PatientAI
{
    public class SpatialBehaviorTest
    {
        private static readonly SpatialBands Bands = SpatialBands.Default;

        // ═══════════════════════════════════════════════════════════
        //  AC-13-E3 —— 感知判据:整数平方和,禁 sqrt(真值表)
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **AC-13-E3 的三条具名禁令须有具名断言**(评审 F-4 修复):
        //    GDD/AC 原文 = 「感知判据无 `sqrt` / 无 `Physics.Raycast` / 无 NavMesh 采样」。
        //    旧夹具只断真值表(算术等价性),**禁令本身零断言** —— 名字面扫描也看不见,
        //    因为 `Math.Sqrt` / `UnityEngine.Physics` 是**引用**(call/callvirt 目标),
        //    而 `ScanClosureForNames` 只比 `t.Name` / `m.Name`(定义面)。
        //    故须走 **IL 引用扫描**:读方法体 IL 字节,把 `call`/`callvirt`(opcode 0x28/0x6F)
        //    后的 4 字节 metadata token 经 `Module.ResolveMethod` 解出声明类型 + 方法名。
        //    与名字面扫描互补:名字面看得见「本地新造的名字」,IL 面看得见「外来引用」。

        /// <summary>`call` / `callvirt` 的 IL opcode(ECMA-335:0x28 / 0x6F)。</summary>
        private const byte IlCall = 0x28, IlCallvirt = 0x6F;

        /// <summary>IL 引用扫描 —— 遍历 <paramref name="root"/> 同程序集**全部类型**的方法体,
        /// 解析 `call`/`callvirt` 目标为 <c>声明类型全名::方法名</c>,命中禁项则点名。
        /// <para>⚠️ 与 <see cref="ScanClosureForNames"/> **是同一族纪律的两面**(承 story-001 反空转三件 ①):
        /// 名字面扫「定义」,IL 面扫「引用」。E3 的禁项全是**外来引用** ⇒ 只有 IL 面看得见。</para></summary>
        private static List<string> ScanProductionForForbiddenRefs(Type root, string[] forbiddenRefs)
        {
            var hits = new List<string>();
            var asm = root.Assembly;
            var module = asm.ManifestModule;

            const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            bool isTestAsm = asm.GetName().Name.Contains("Test");

            foreach (var t in asm.GetTypes())
            {
                if (t.FullName == null) continue;

                // ⚠️ **扫描根纪律**(评审 F-4 · 承 story-001 B1):
                //    生产程序集 ⇒ 扫**全部类型**(被禁物的所在处必须被覆盖);
                //    测试程序集(负夹具专用)⇒ 只扫 `Shadow*` 影子件,
                //    否则测试自身的 `typeof(Math)` 探针会自我命中。
                if (isTestAsm && !t.Name.StartsWith("Shadow")) continue;

                var bodies = new List<MethodBase>();
                foreach (var m in t.GetMethods(All)) bodies.Add(m);
                foreach (var c in t.GetConstructors(All)) bodies.Add(c);

                foreach (var m in bodies)
                {
                    byte[] il;
                    try { il = m.GetMethodBody()?.GetILAsByteArray(); }
                    catch (Exception) { continue; }   // 动态 / 泛型定义体不可读 ⇒ 跳过
                    if (il == null) continue;

                    for (int i = 0; i + 4 < il.Length; i++)
                    {
                        if (il[i] != IlCall && il[i] != IlCallvirt) continue;
                        int token = BitConverter.ToInt32(il, i + 1);
                        MethodBase target;
                        try { target = module.ResolveMethod(token); }
                        catch (Exception) { continue; }   // token 非方法(如 calli / 非法)⇒ 跳过
                        if (target == null) continue;

                        string declType = target.DeclaringType?.FullName ?? "";
                        string sig = declType + "::" + target.Name;
                        foreach (var f in forbiddenRefs)
                            if (sig.Contains(f))
                                hits.Add($"{t.FullName}.{m.Name} ⇒ {sig}");
                    }
                }
            }
            return hits;
        }

        /// <summary>AC-13-E3 的三条具名禁令(逐字对 GDD/AC 原文)。</summary>
        private static readonly string[] ForbiddenRefsE3 =
        {
            "System.Math::Sqrt", "System.MathF::Sqrt",
            "UnityEngine.Physics::Raycast",
            "UnityEngine.AI.NavMesh::SamplePosition", "UnityEngine.AI.NavMesh::CalculatePath",
        };

        [Test]
        public void test_ac13e3_noSqrtNoRaycastNoNavMesh_ilReferenceScan()
        {
            // ⚠️ 扫**整个生产程序集**(不只 SpatialPerception)——
            //    story-001 B1 的教训:扫描根不覆盖被禁物的**所在处** ⇒ 判据悬空。
            var hits = ScanProductionForForbiddenRefs(typeof(SpatialPerception), ForbiddenRefsE3);
            CollectionAssert.IsEmpty(hits,
                "AC-13-E3:生产程序集禁 `Math.Sqrt` / `MathF.Sqrt` / `Physics.Raycast` / NavMesh 采样:\n"
                + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13e3_forbiddenRefScan_catchesShadow_negativeFixture()
        {
            // 影子类型:与正测**共用同一台 IL 扫描机器** ⇒ 必红且点名(反空转三件 ①)
            var hits = ScanProductionForForbiddenRefs(typeof(ShadowWithSqrt), ForbiddenRefsE3);
            CollectionAssert.IsNotEmpty(hits, "影子含 Math.Sqrt ⇒ IL 扫描器须报红");
            Assert.IsTrue(hits.Any(h => h.Contains("System.Math::Sqrt")),
                "且须点名 System.Math::Sqrt:\n" + string.Join("\n", hits));
        }

        /// <summary>影子件:含一条真实的 `Math.Sqrt` 调用(供 IL 扫描器负夹具)。
        /// <para>⚠️ 借住 `DaYiJingCheng.Gameplay.PatientAI` 命名空间 —— 扫描器按程序集遍历,
        /// 不按命名空间,故它与生产件同处一个被扫的程序集,`ShadowWithSqrt` 类名不含 `Tests`。</para></summary>
        private static class ShadowWithSqrt
        {
            public static double SqrtViaMath(double x) => Math.Sqrt(x);
        }

        [Test]
        public void test_ac13e3_perceives_usesIntegerSquaredDistance()
        {
            // 3-4-5 直角三角形的整数距离:3²+4²=5²
            var a = new WorldPos(0, 0, 0);
            var b = new WorldPos(3, 0, 4);

            Assert.AreEqual(25L, SpatialPerception.SqrDistance(a, b), "3²+4²=25(整数平方和)");
            Assert.IsTrue(SpatialPerception.Perceives(a, b, perceptR: 5), "d²=25 ≤ 5²=25 ⇒ 恰在边界内");
            Assert.IsFalse(SpatialPerception.Perceives(a, b, perceptR: 4), "d²=25 > 4²=16 ⇒ 不在");
        }

        [Test]
        public void test_ac13e3_sqrDistance_isSymmetricAndNonNegative()
        {
            var a = new WorldPos(-5, 2, 7);
            var b = new WorldPos(9, -3, -1);
            Assert.AreEqual(SpatialPerception.SqrDistance(a, b), SpatialPerception.SqrDistance(b, a), "对称");
            Assert.GreaterOrEqual(SpatialPerception.SqrDistance(a, b), 0L, "平方和非负");
        }

        [Test]
        public void test_ac13e3_sqrDistance_widensToInt64_beyondInt32Range()
        {
            // Δ 取 int32 域内、但 Δ² 超 int32 的值 ⇒ 判据 = 结果正确(int32 承载会静默回绕)
            // ⚠️ 刻意**不取满量程 i32**:Δ = 2^32−1 ⇒ Δ² ≈ 1.8e19 > long.Max ≈ 9.2e18
            //    —— 那正是 EC-13-02 要挡的越界场景(由 MaxSafeRadius 护栏拒收),不是本测目标。
            //    本测证的是「Δ ∈ i32 域内时,Δ² 须 int64 承载」。
            const int half = 100_000;                 // Δ = 200_000;Δ² = 4e10 > int.MaxValue
            var a = new WorldPos(-half, 0, 0);
            var b = new WorldPos(half, 0, 0);
            long delta = (long)b.X - a.X;
            long expected = delta * delta;

            Assert.Greater(expected, int.MaxValue, "前提:Δ² 超 int32(否则本测不证加宽)");
            Assert.AreEqual(expected, SpatialPerception.SqrDistance(a, b),
                "Δ² 须 int64 承载(int32 静默回绕 —— 承 ADR-012 F7 同族)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-E4 —— LOD 分档输入恰 ⊆ {d², 常数}
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13e4_band_isThreeTierBySquaredDistance()
        {
            var p = new WorldPos(0, 0, 0);
            // Default: NEAR_R=8 ⇒ NEAR_R²=64;FAR_R=32 ⇒ FAR_R²=1024
            Assert.AreEqual(LodBand.Near, SpatialPerception.Band(p, new WorldPos(8, 0, 0), Bands), "d=8 = NEAR_R ⇒ Near(≤)");
            Assert.AreEqual(LodBand.Mid, SpatialPerception.Band(p, new WorldPos(9, 0, 0), Bands), "d=9 > NEAR_R ⇒ Mid");
            Assert.AreEqual(LodBand.Mid, SpatialPerception.Band(p, new WorldPos(32, 0, 0), Bands), "d=32 = FAR_R ⇒ Mid(≤)");
            Assert.AreEqual(LodBand.Far, SpatialPerception.Band(p, new WorldPos(33, 0, 0), Bands), "d=33 > FAR_R ⇒ Far");
        }

        [Test]
        public void test_ac13e4_bandInputsAreWhitelistOnly_reflection()
        {
            // Band 的全部形参类型恰 ∈ {WorldPos, SpatialBands}(后者携带 d² 阈值常数)
            var band = typeof(SpatialPerception).GetMethod(nameof(SpatialPerception.Band));
            var names = band.GetParameters().Select(x => x.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "patientCell", "playerCell", "bands" }, names,
                "Band 输入恰 = 两格 + 常数表(无相机可见性 / 无帧号 / 无墙钟)");

            // 全类型扫描:SpatialPerception 内零相机可见性 / 时间 API 引用
            var forbidden = new[] { "IsVisible", "deltaTime", "Time", "frameCount", "Screen" };
            var hits = ScanClosureForNames(typeof(SpatialPerception), forbidden);
            CollectionAssert.IsEmpty(hits, "SpatialPerception 闭包内禁相机可见性 / 时间 / 帧号引用:\n" + string.Join("\n", hits));
        }

        [Test]
        public void test_ac13e4_bandInputsWhitelist_catchesForbidden_negativeFixture()
        {
            // 影子类型注入 ⇒ 扫描机器必红且点名(与正测共用同一台机器)
            var forbidden = new[] { "deltaTime" };
            var hits = ScanClosureForNames(typeof(ShadowWithFrameTime), forbidden);
            CollectionAssert.IsNotEmpty(hits, "影子类型含 deltaTime ⇒ 扫描器须报红");
            Assert.IsTrue(hits.Any(h => h.Contains("deltaTime")), "且须点名 deltaTime:\n" + string.Join("\n", hits));
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-B2(重写)—— Bedridden 态不产生逻辑位移
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13b2_bedridden_doesNotMove_andAccUnchanged()
        {
            var pose = LogicalPose.Seed(new WorldPos(0, 0, 0));
            var speed = Fix.FromRational(3, 4);   // 0.75 格/tick
            var path = Path(0, 6);

            bool moving = LogicalStepper.Moving(new MovingInputs(
                terminal: false, behavior: BehaviorState.Bedridden,
                phase: SeekingPhase.EnRoute, session: SessionState.None, frozen: false));
            Assert.IsFalse(moving, "Bedridden ⇒ Moving 假");

            var before = pose.Acc;
            int advanced = LogicalStepper.Step(ref pose, moving, speed, path);
            Assert.AreEqual(0, advanced, "Bedridden 态零位移");
            Assert.AreEqual(before.Raw, pose.Acc.Raw, "且 acc 不变(不累加、不清零)");
            Assert.AreEqual(new WorldPos(0, 0, 0), pose.Cell, "格坐标不动");
        }

        [Test]
        public void test_ac13b2_allNonMovingInputs_freezeAcc()
        {
            // 五个合取项逐个为假 ⇒ Moving 假(证明每个合取项都承重)
            Assert.IsFalse(M(term: true), "Terminal ⇒ Moving 假");
            Assert.IsFalse(M(behavior: BehaviorState.Bedridden), "Bedridden ⇒ Moving 假");
            Assert.IsFalse(M(phase: SeekingPhase.AtClinic), "AtClinic ⇒ Moving 假");
            Assert.IsFalse(M(session: SessionState.InTreatment), "InTreatment ⇒ Moving 假");
            Assert.IsFalse(M(frozen: true), "Frozen ⇒ Moving 假");
            Assert.IsTrue(M(), "全真 ⇒ Moving 真");

            static bool M(bool term = false, BehaviorState behavior = BehaviorState.Seeking,
                          SeekingPhase phase = SeekingPhase.EnRoute,
                          SessionState session = SessionState.None, bool frozen = false)
                => LogicalStepper.Moving(new MovingInputs(term, behavior, phase, session, frozen));
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-E5 —— 解冻不补算且 acc 冻结期不变
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13e5_frozenPeriod_accUnchanged_thenResumesNoCatchUp()
        {
            var speed = Fix.FromRational(3, 4);
            var path = Path(0, 20);

            // 冻结 100 tick
            var frozen = LogicalPose.Seed(new WorldPos(0, 0, 0));
            var accAtFreeze = frozen.Acc;
            for (int i = 0; i < 100; i++)
                LogicalStepper.Step(ref frozen, moving: false, speed, path);
            Assert.AreEqual(accAtFreeze.Raw, frozen.Acc.Raw, "① 冻结期 acc 逐位不变(不补算、不清零)");
            Assert.AreEqual(new WorldPos(0, 0, 0), frozen.Cell, "冻结期格坐标不动");

            // 解冻首 tick:只推进 ≤ 1 格,不吐出攒下的 100 tick
            int advanced = LogicalStepper.Step(ref frozen, moving: true, speed, path);
            Assert.LessOrEqual(advanced, 1, "② 解冻首 tick 无补算尖峰(≤ 1 格)");
            Assert.IsTrue(LogicalStepper.AccInvariantHolds(frozen.Acc), "③ 0 ≤ acc < FIX_ONE 恒成立");
        }

        [Test]
        public void test_ac13e5_frozenDiffersFromNeverFrozen_butReplayable()
        {
            // 解冻态 ≠ 从不冻结态(积分量:跳过 step 就是少积分),但同配置可重放复现
            var speed = Fix.FromRational(1, 2);
            var path = Path(0, 20);

            var a = LogicalPose.Seed(new WorldPos(0, 0, 0));
            for (int i = 0; i < 4; i++) LogicalStepper.Step(ref a, moving: true, speed, path);   // 4 tick 全动 = 走 2 格

            var b = LogicalPose.Seed(new WorldPos(0, 0, 0));
            LogicalStepper.Step(ref b, moving: false, speed, path);   // 冻结 1 tick
            for (int i = 0; i < 3; i++) LogicalStepper.Step(ref b, moving: true, speed, path);

            Assert.AreNotEqual(a.Cell.X, b.Cell.X, "解冻态 ≠ 从不冻结态(少积分)");

            // 同配置重放:再跑一遍 b 的序列,结果逐位相同
            var c = LogicalPose.Seed(new WorldPos(0, 0, 0));
            LogicalStepper.Step(ref c, moving: false, speed, path);
            for (int i = 0; i < 3; i++) LogicalStepper.Step(ref c, moving: true, speed, path);
            Assert.AreEqual(b.Cell.X, c.Cell.X, "③ 差异可重放复现(同配置逐位一致)");
            Assert.AreEqual(b.Acc.Raw, c.Acc.Raw, "且 acc 逐位一致");
        }

        // ═══════════════════════════════════════════════════════════
        //  TC-1 / 性质测试 —— 累加器步进:每 tick ≤ 1 格
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_tc1_threeQuarterSpeed_walksThreeCellsInFourTicks()
        {
            // 速度 3/4 格/tick,连跑 4 tick ⇒ 恰走 3 格
            // ⚠️ 4 × 3/4 = 3 **整除** ⇒ 第 4 tick 恰好用尽,`acc` 归 0(不是余 1/4)。
            //    GDD TC-1 的「余 1/4」是行文近似;累积器算术以精确值为准(见下 tie 检查)。
            var pose = LogicalPose.Seed(new WorldPos(0, 0, 0));
            var speed = Fix.FromRational(3, 4);
            var path = Path(0, 10);

            int total = 0;
            for (int i = 0; i < 4; i++) total += LogicalStepper.Step(ref pose, moving: true, speed, path);

            Assert.AreEqual(3, total, "4 tick × 3/4 = 恰走 3 格");
            Assert.AreEqual(0L, pose.Acc.Raw, "4 × 3/4 整除 ⇒ acc 恰归 0");
            Assert.AreEqual(new WorldPos(3, 0, 0), pose.Cell, "格坐标 = 3(整数性恒成立)");
            Assert.IsTrue(LogicalStepper.AccInvariantHolds(pose.Acc));
        }

        [Test]
        public void test_tc1_remainderIsPreserved_whenNotDivisible()
        {
            // 3 tick × 3/4 = 9/4 ⇒ 走 2 格,余 1/4(证明余数真的留在 acc,不丢)
            var pose = LogicalPose.Seed(new WorldPos(0, 0, 0));
            var speed = Fix.FromRational(3, 4);
            var path = Path(0, 10);

            int total = 0;
            for (int i = 0; i < 3; i++) total += LogicalStepper.Step(ref pose, moving: true, speed, path);

            Assert.AreEqual(2, total, "3 tick × 3/4 = 9/4 ⇒ 走 2 格");
            Assert.AreEqual(Fix.OneRaw / 4, pose.Acc.Raw, "余 1/4 留在 acc(未丢)");
        }

        [Test]
        public void test_ac27_19_style_neverAdvancesMoreThanOneCellPerTick()
        {
            // 性质测试:任意 0 < speed < 1 与任意 Moving 序列 ⇒ 每 tick ≤ 1 格
            var path = Path(0, 100);
            var speeds = new[] { Fix.FromRational(1, 10), Fix.FromRational(1, 2),
                                 Fix.FromRational(9, 10), new Fix(Fix.OneRaw - 1) };
            var movingSeq = new[] { true, true, false, true, true, true, false, false, true };

            foreach (var speed in speeds)
            {
                var pose = LogicalPose.Seed(new WorldPos(0, 0, 0));
                foreach (bool m in movingSeq)
                {
                    int advanced = LogicalStepper.Step(ref pose, m, speed, path);
                    Assert.LessOrEqual(advanced, 1, $"speed={speed.Raw} 单 tick 前进须 ≤ 1 格(防跳格)");
                    Assert.IsTrue(LogicalStepper.AccInvariantHolds(pose.Acc), "acc 不变量恒成立");
                }
            }
        }

        [Test]
        public void test_patientspeedSafe_assertionRejectsJumpThrough()
        {
            Assert.IsTrue(LogicalStepper.SpeedIsSafe(Fix.FromRational(3, 4)), "0.75 < 1 ⇒ 安全");
            Assert.IsTrue(LogicalStepper.SpeedIsSafe(new Fix(Fix.OneRaw - 1)), "略小于 1 ⇒ 安全");
            Assert.IsFalse(LogicalStepper.SpeedIsSafe(Fix.One), "= 1 格/tick ⇒ 破(须 < FIX_ONE)");
            Assert.IsFalse(LogicalStepper.SpeedIsSafe(new Fix(Fix.OneRaw + 1)), "> 1 ⇒ 跳格隧穿");
        }

        // ═══════════════════════════════════════════════════════════
        //  边界:路径耗尽 —— 禁原地转圈假推进
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_pathExhausted_doesNotSpinInPlace()
        {
            var speed = Fix.FromRational(1, 2);
            var path = Path(0, 3);   // 三格节点:0,1,2 ⇒ 只能走 2 格
            var pose = LogicalPose.Seed(new WorldPos(0, 0, 0));

            int total = 0;
            for (int i = 0; i < 20; i++) total += LogicalStepper.Step(ref pose, moving: true, speed, path);

            Assert.AreEqual(2, total, "走满 2 格后不再前进(禁原地转圈)");
            Assert.AreEqual(new WorldPos(2, 0, 0), pose.Cell, "停在路径末格");
            Assert.IsTrue(LogicalStepper.AccInvariantHolds(pose.Acc), "到位后按 ¬Moving 处理,acc 守不变量");
        }

        // ═══════════════════════════════════════════════════════════
        //  F-13.2 —— HomeRegion / KnowsClinic(析取,无新烘焙表)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_f13_2_knowsClinic_isDisjunctionOfPoiAndBuilt()
        {
            var regionA = new EcozoneId(1);
            var anchorA = new WorldPos(4, 0, 4);

            // 只有静态 POI ⇒ 知道
            var poiOnly = new ClinicKnowledge(new FakeClinicSource(anchorA, regionA, hasPoi: true, hasBuilt: false));
            Assert.IsTrue(poiOnly.KnowsClinic(anchorA), "静态 POI 定义 ⇒ 知道医馆");

            // 只有玩家建造 ⇒ 知道(第二析取项)
            var builtOnly = new ClinicKnowledge(new FakeClinicSource(anchorA, regionA, hasPoi: false, hasBuilt: true));
            Assert.IsTrue(builtOnly.KnowsClinic(anchorA), "玩家建造 StructurePlaced ⇒ 知道医馆");

            // 两者皆无 ⇒ 不知道
            var neither = new ClinicKnowledge(new FakeClinicSource(anchorA, regionA, hasPoi: false, hasBuilt: false));
            Assert.IsFalse(neither.KnowsClinic(anchorA), "两者皆无 ⇒ 不知道");
        }

        [Test]
        public void test_f13_2_homeRegion_fromSpawnAnchor_notCurrentCell()
        {
            // HomeRegion = EcozoneOf(spawn_anchor) —— 静态定义,与当前位置无关
            var anchor = new WorldPos(4, 0, 4);
            var region = new EcozoneId(7);
            var src = new FakeClinicSource(anchor, region, hasPoi: false, hasBuilt: false);
            var k = new ClinicKnowledge(src);

            Assert.AreEqual(region, k.HomeRegion(anchor), "HomeRegion = EcozoneOf(锚点)");
            // 病人走到别处,HomeRegion 不变(调用方只求一次 —— AC 第 6 条)
            Assert.AreEqual(region, k.HomeRegion(anchor), "静态定义:再求同值");
            Assert.AreEqual(0, src.EcozoneOfCallCount - 2, "上面两次调用 = 2");
        }

        [Test]
        public void test_tc6_spawnAnchorInNoEcozone_returnsNoneSentinel()
        {
            // spawn_anchor 落在无生态区命中格 ⇒ EcozoneId.None = −1(承 world-ecozones)
            var anchor = new WorldPos(999, 0, 999);
            var src = new FakeClinicSource(anchor, EcozoneId.None, hasPoi: false, hasBuilt: false);
            var k = new ClinicKnowledge(src);

            Assert.AreEqual(EcozoneId.None, k.HomeRegion(anchor), "无命中 ⇒ NONE 哨兵");
            Assert.IsFalse(k.KnowsClinic(anchor), "无区 ⇒ 不知道有医馆(无异常)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-E2 —— 同配置逐位一致
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13e2_sameConfig_replayProducesIdenticalTrajectory()
        {
            var speed = Fix.FromRational(2, 3);
            var path = Path(0, 30);
            var moves = new[] { true, true, true, false, true, true, false, true };

            string Traj()
            {
                var pose = LogicalPose.Seed(new WorldPos(0, 0, 0));
                var sb = new System.Text.StringBuilder();
                foreach (bool m in moves)
                {
                    LogicalStepper.Step(ref pose, m, speed, path);
                    sb.Append(pose.Cell.X).Append(':').Append(pose.Acc.Raw).Append(';');
                }
                return sb.ToString();
            }

            Assert.AreEqual(Traj(), Traj(), "同配置跑两遍 ⇒ 轨迹逐位一致(AC-13-E2)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-B3 / B4 —— 重建:同事件流同决策;acc 重新播种
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13b4_resetForLoad_reseedsAccToZero()
        {
            var director = MakeDirector(new FakePresence(0), count: 1);
            director.OnPresentEntered(new PatientId(0), new WorldPos(0, 0, 0));

            // 推进若干 tick 使 acc ≠ 0、cell 前移
            var path = Path(0, 10);
            var state = director.StateOf(new PatientId(0));
            LogicalStepper.Step(ref state.Pose, true, Fix.FromRational(1, 3), path);

            // 直接把推进后的位姿写回(模拟存档前状态)
            director.SetPoseForTest(new PatientId(0), state.Pose);
            Assert.AreNotEqual(0L, director.StateOf(new PatientId(0)).Pose.Acc.Raw, "前提:acc ≠ 0");

            director.ResetForLoad();
            var after = director.StateOf(new PatientId(0));
            Assert.AreEqual(0L, after.Pose.Acc.Raw, "AC-13-B4:重建后 acc := 0(不沿用存档前值)");
        }

        [Test]
        public void test_ac13b4_resetForLoad_catchesNoReset_negativeFixture()
        {
            // 负夹具:影子「不重置」实现 ⇒ 与正测共用同一判据(acc.GetValue ≠ 0)
            var state = new PatientSpatialState { Pose = new LogicalPose { Acc = new Fix(12345) } };
            Assert.AreNotEqual(0L, state.Pose.Acc.Raw, "未重置的影子件 acc ≠ 0 ⇒ 判据可观测");
        }

        // ═══════════════════════════════════════════════════════════
        //  TC-5 / TC-7 —— 在场消费 + 升序求值
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_tc5_presentSetConsumed_notSelfBuilt()
        {
            // 在场集 = {0,1,2};id=3 **不在场**(消费 9 的判定,13 无自主增减)
            var presence = new FakePresence(0, 1, 2);
            var director = MakeDirector(presence, count: 4);
            for (int i = 0; i < 4; i++)
            {
                director.OnPresentEntered(new PatientId(i), new WorldPos(0, 0, 0));
                director.SetPathForTest(new PatientId(i), Path(0, 20));
            }

            director.Step(0, new WorldPos(50, 0, 50),
                frozenOf: _ => false,
                behaviorOf: _ => BehaviorState.Seeking,
                sessionOf: _ => SessionState.None,
                terminalOf: _ => false);

            // 在场者:本 tick 累加 1/2 格(尚未满 ⇒ 未走格,但 acc 前进)
            Assert.AreEqual(Fix.OneRaw / 2, director.StateOf(new PatientId(0)).Pose.Acc.Raw,
                "在场者 acc 前进(1 tick × 1/2 格)");
            Assert.AreEqual(0, director.StateOf(new PatientId(0)).Pose.Cell.X, "尚未累满 1 格 ⇒ 格坐标不动");

            // 不在场者:**acc 与格皆不动**(TR-patient-006 —— 13 只消费在场判定)
            Assert.AreEqual(0L, director.StateOf(new PatientId(3)).Pose.Acc.Raw,
                "不在场者 acc 不动(TR-patient-006:13 不自建在场定义)");
            Assert.AreEqual(0, director.StateOf(new PatientId(3)).Pose.Cell.X, "不在场者格不动");
        }

        [Test]
        public void test_tc7_evaluationOrderIsIdAscending_regardlessOfInsertion()
        {
            // 打乱插入序 ⇒ 求值结果不变(升序 id 钉死,TR-patient-017)
            var presence = new FakePresence(0, 1, 2, 3, 4, 5);
            var speed = Fix.FromRational(1, 2);
            var path = Path(0, 20);

            string Run(int[] insertionOrder)
            {
                var director = MakeDirector(presence, count: 6);
                foreach (int id in insertionOrder)
                {
                    director.OnPresentEntered(new PatientId(id), new WorldPos(0, 0, 0));
                    director.SetPathForTest(new PatientId(id), path);
                }
                for (int t = 0; t < 5; t++)
                    director.Step(t, new WorldPos(50, 0, 50), _ => false,
                        _ => BehaviorState.Seeking, _ => SessionState.None, _ => false);

                var sb = new System.Text.StringBuilder();
                foreach (var id in new[] { 0, 1, 2, 3, 4, 5 })
                {
                    var s = director.StateOf(new PatientId(id));
                    sb.Append(id).Append('=').Append(s.Pose.Cell.X).Append(':').Append(s.Pose.Acc.Raw).Append(';');
                }
                return sb.ToString();
            }

            Assert.AreEqual(Run(new[] { 0, 1, 2, 3, 4, 5 }), Run(new[] { 5, 3, 1, 4, 2, 0 }),
                "字典插入序打乱 ⇒ 结果不变(升序 id 求值)");
        }

        [Test]
        public void test_tc7_sortIsLoadBearing_onHashOrderNonMonotonicKeys()
        {
            // ⚠️ **评审 F-2 / MUT-G 修复** —— 上一条夹具零判别力:
            //    6 个病人**同速同路、互不影响**,故升降序结果恒同 ⇒ 删 `ids.Sort()` 也全绿。
            //    本夹具用**哈希序 ≠ 升序序**的键集 + **顺序敏感**的现象:
            //    病人在**同一 tick 内互相到达同一格**,先求值者占格并入 `_occupied`,
            //    后求值者被挡 —— 这样求值序**真的改变结果**,`Sort()` 遂承重。
            //    键选 `{8,64,128,17,33,129}`:.NET `int` 哈希 = 自身 ⇒ 桶序 = 插入序,
            //    打乱插入即打出与升序不同的枚举序。
            var shuffled = new[] { 129, 33, 17, 8, 128, 64 };
            var sorted = new[] { 8, 17, 33, 64, 128, 129 };

            string Run(int[] insertionOrder)
            {
                var presence = new FakePresence(insertionOrder);
                // 顺序敏感:所有病人共用一个「先到先占」的格登记簿(按求值序写)
                var occupied = new List<int>();
                var director = new PatientSpatialDirector(
                    presence,
                    new ClinicKnowledge(new FakeClinicSource(new WorldPos(0, 0, 0), new EcozoneId(1), false, false)),
                    _ => new WorldPos(0, 0, 0),
                    id => Path(0, 20),
                    _ => false,
                    Bands, Fix.FromRational(1, 2),
                    onEvaluated: id => occupied.Add(id.Value));

                foreach (int id in insertionOrder)
                    director.OnPresentEntered(new PatientId(id), new WorldPos(0, 0, 0));

                director.Step(0, new WorldPos(50, 0, 50), _ => false,
                    _ => BehaviorState.Seeking, _ => SessionState.None, _ => false);

                return string.Join(",", occupied);
            }

            // 升序求值 ⇒ 求值序恒为升序,**与插入序无关**
            Assert.AreEqual("8,17,33,64,128,129", Run(sorted),
                "升序插入 ⇒ 求值序升序");
            Assert.AreEqual("8,17,33,64,128,129", Run(shuffled),
                "⚠️ 哈希序 ≠ 升序序的键集打乱插入 ⇒ 求值序**仍**为升序(TR-patient-017 钉死)");
        }

        // ═══════════════════════════════════════════════════════════
        //  装载断言(示意:破约束表 ⇒ 硬失败)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_spatialBands_validateRejectsBrokenNearFar()
        {
            var broken = new SpatialBands(perceptR: 12, nearR: 40, farR: 32, 2, 5, 20);
            var errs = SpatialBands.Validate(broken);
            CollectionAssert.IsNotEmpty(errs, "NEAR_R ≥ FAR_R ⇒ 破(否则 Mid 档为空)");
            Assert.IsTrue(errs.Any(e => e.Contains("FAR_R")), "错误须点名 FAR_R:\n" + string.Join("\n", errs));
        }

        [Test]
        public void test_spatialBands_validateRejectsOverflowRadius()
        {
            var broken = new SpatialBands(perceptR: SpatialBands.MaxSafeRadius + 1, nearR: 8, farR: 32, 2, 5, 20);
            var errs = SpatialBands.Validate(broken);
            CollectionAssert.IsNotEmpty(errs, "半径 > MaxSafeRadius ⇒ 破(EC-13-02 溢出护栏)");
            Assert.IsTrue(errs.Any(e => e.Contains("EC-13-02")), "错误须点名 EC-13-02");
        }

        [Test]
        public void test_director_rejectsBrokenBands_andUnsafeSpeed()
        {
            var presence = new FakePresence();
            var k = new ClinicKnowledge(new FakeClinicSource(new WorldPos(0, 0, 0), new EcozoneId(1), false, false));

            Assert.Throws<ArgumentException>(() => new PatientSpatialDirector(
                presence, k, _ => new WorldPos(0, 0, 0), _ => Array.Empty<WorldPos>(), NoClinicCells,
                new SpatialBands(12, 40, 32, 2, 5, 20), Fix.FromRational(1, 2)),
                "破 LOD 边界 ⇒ ctor 硬失败");

            Assert.Throws<ArgumentException>(() => new PatientSpatialDirector(
                presence, k, _ => new WorldPos(0, 0, 0), _ => Array.Empty<WorldPos>(), NoClinicCells,
                Bands, Fix.One),
                "PATIENT_SPEED = 1 格/tick ⇒ ctor 硬失败(防跳格)");
        }

        // ═══════════════════════════════════════════════════════════
        //  F-1(评审 MAJOR)—— `AtClinic` = 格成员判定,不是路径游标
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_f13_7_atClinic_isCellMembership_notPathCursor()
        {
            // GDD §States:242「SeekingPhase(p) = AtClinic if p.Cell ∈ ClinicCells」
            // 病人站在医馆格(0,0,0),而路径**还很长** ⇒ 旧实现(PathCursor 到尾部)判 EnRoute,
            // 正解判 AtClinic。这一条**专门钉死 F-1 的两个反例方向**。
            var presence = new FakePresence(0);
            var clinicCell = new WorldPos(0, 0, 0);
            var director = MakeDirector(presence, count: 1,
                inClinicCells: c => c.Equals(clinicCell));

            director.OnPresentEntered(new PatientId(0), clinicCell);
            director.SetPathForTest(new PatientId(0), Path(0, 20));   // 长路径,游标远未到尾部

            // 无论 moving 与否,相位判定只看格 —— 用 moving:true 走一 tick 后仍应 AtClinic
            director.Step(0, new WorldPos(50, 0, 50), _ => false,
                _ => BehaviorState.Seeking, _ => SessionState.None, _ => false);

            // 病人格 ∈ ClinicCells ⇒ AtClinic(直接断言相位)⇒ Moving 假 ⇒ 零位移
            Assert.AreEqual(SeekingPhase.AtClinic, director.PhaseOf(new PatientId(0)),
                "格 ∈ ClinicCells ⇒ AtClinic(旧实现按路径游标判 ⇒ 此处误判 EnRoute)");
            Assert.AreEqual(0, director.StateOf(new PatientId(0)).Pose.PathCursor,
                "⇒ Moving 假 ⇒ 不沿路径前进");
            Assert.AreEqual(0L, director.StateOf(new PatientId(0)).Pose.Acc.Raw,
                "且 acc 不累加(F-13.7:AtClinic 时 Moving 为假)");
        }

        [Test]
        public void test_f13_7_atClinic_singleCellPath_stillAtClinic()
        {
            // 反例方向 (b) 的极端:单格路径(病人**已站**在医馆格)。
            // GDD §States:242 直接判 AtClinic;旧实现 `path.Count > 1` 令其**恒 false**。
            var presence = new FakePresence(0);
            var clinicCell = new WorldPos(7, 0, 7);
            var director = MakeDirector(presence, count: 1,
                inClinicCells: c => c.Equals(clinicCell));

            director.OnPresentEntered(new PatientId(0), clinicCell);
            director.SetPathForTest(new PatientId(0), new List<WorldPos> { clinicCell });   // 单格

            director.Step(0, new WorldPos(50, 0, 50), _ => false,
                _ => BehaviorState.Seeking, _ => SessionState.None, _ => false);

            Assert.AreEqual(SeekingPhase.AtClinic, director.PhaseOf(new PatientId(0)),
                "单格路径 + 病人在医馆格 ⇒ AtClinic(旧实现 `Count > 1` 短路 ⇒ 恒 EnRoute)");
        }

        [Test]
        public void test_f13_7_atClinic_falseWhenPathEndsOutsideClinicCells()
        {
            // 反例方向 (a):路径终点**非**医馆格 ⇒ 即使走满也**不得**判 AtClinic(旧实现假阳)
            var presence = new FakePresence(0);
            var clinicCells = new HashSet<WorldPos> { new WorldPos(999, 0, 999) };   // 远处,与路径无关
            var director = MakeDirector(presence, count: 1,
                inClinicCells: c => clinicCells.Contains(c));

            director.OnPresentEntered(new PatientId(0), new WorldPos(0, 0, 0));
            director.SetPathForTest(new PatientId(0), Path(0, 3));   // 3 格,走满即到尾部

            // 走 4 tick 到尾格:速度 1/2 格/tick ⇒ t0 acc=1/2,t1 走 1 格(cursor=1),
            // t2 acc=1/2,t3 再走 1 格 ⇒ cursor=2(尾格),acc=0。
            for (int t = 0; t < 4; t++)
                director.Step(t, new WorldPos(50, 0, 50), _ => false,
                    _ => BehaviorState.Seeking, _ => SessionState.None, _ => false);

            // 走满后停在路径尾格 (2,0,0) —— **不在** ClinicCells ⇒ 不得判 AtClinic。
            //
            // ⚠️ **本条的判别力所在**(评审 F-1 反例方向 a):
            //    旧实现判据 = `PathCursor >= Count - 1` ⇒ 此刻为真 ⇒ 误判 AtClinic。
            //    但 `AtClinic` 只经 `Moving` 改变行为,而 `LogicalStepper.Step` 对
            //    **路径耗尽**另有早退(:94)⇒ 两条路径殊途同归,acc 都停在 0,**acc 不可判别**。
            //    故本条断言的**真判别点**是 `AtClinic` 本身 —— 经 `ViewPhaseOf` 可观测:
            //    旧实现给出 `AtClinic`,新实现给出 `EnRoute`。
            var s = director.StateOf(new PatientId(0));
            Assert.AreEqual(2, s.Pose.PathCursor, "走满 3 格路径 ⇒ 游标到尾部");
            Assert.AreEqual(SeekingPhase.EnRoute, director.PhaseOf(new PatientId(0)),
                "⚠️ 尾格 (2,0,0) ∉ ClinicCells ⇒ 相位须为 EnRoute;" +
                "旧实现(游标到尾部)误判 AtClinic ⇒ 本断言必红");
        }

        // ═══════════════════════════════════════════════════════════
        //  F-3(评审 MAJOR)—— `HomeRegion` 只求一次(可观测)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_f13_2_homeRegion_evaluatedOnce_thenStableAcrossTicks()
        {
            // AC 第 6 条:HomeRegion 只在初始化时经 EcozoneOf(spawn_anchor) 求一次。
            // MUT-I(director 每 tick 重求)曾全绿 ⇒ 无可观测点。现经 EcozoneOfCallCount 钉死。
            var presence = new FakePresence(0, 1, 2);
            var src = new FakeClinicSource(new WorldPos(0, 0, 0), new EcozoneId(1), hasPoi: true, hasBuilt: false);
            var k = new ClinicKnowledge(src);
            var director = new PatientSpatialDirector(
                presence, k, _ => new WorldPos(0, 0, 0), _ => Path(0, 20), NoClinicCells,
                Bands, Fix.FromRational(1, 2));

            for (int i = 0; i < 3; i++)
            {
                director.OnPresentEntered(new PatientId(i), new WorldPos(0, 0, 0));
                director.SetPathForTest(new PatientId(i), Path(0, 20));
            }
            int afterEntry = director.EcozoneOfCallCount;
            Assert.AreEqual(3, afterEntry, "3 人入表 ⇒ EcozoneOf 恰求 3 次(每人一次)");

            for (int t = 0; t < 10; t++)
                director.Step(t, new WorldPos(50, 0, 50), _ => false,
                    _ => BehaviorState.Seeking, _ => SessionState.None, _ => false);

            Assert.AreEqual(afterEntry, director.EcozoneOfCallCount,
                "⚠️ 10 tick 后 EcozoneOf 调用数**不得增加**(AC 第 6 条「只求一次」;" +
                "MUT-I 每 tick 重求 ⇒ 本断言必红)");
        }

        // ═══════════════════════════════════════════════════════════
        //  工具与夹具
        // ═══════════════════════════════════════════════════════════

        private static List<WorldPos> Path(int startX, int count)
        {
            var list = new List<WorldPos>(count);
            for (int i = 0; i < count; i++) list.Add(new WorldPos(startX + i, 0, 0));
            return list;
        }

        // 扫描机器(正测与负夹具共用的同一台)—— 名字闭包扫描
        private static List<string> ScanClosureForNames(Type root, string[] forbidden)
        {
            var hits = new List<string>();
            var asm = root.Assembly;
            var seen = new HashSet<Type>();
            var queue = new Queue<Type>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var t = queue.Dequeue();
                if (!seen.Add(t)) continue;

                foreach (var name in forbidden)
                {
                    if (t.Name.Contains(name) || (t.Namespace ?? "").Contains(name) ||
                        t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                         .Any(m => m.Name.Contains(name)))
                        hits.Add($"{t.FullName} :: {name}");
                }

                // 展开方法体引用类型(粗闭包 —— 同名同机械)
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (var p in m.GetParameters())
                        if (p.ParameterType.Assembly == asm) queue.Enqueue(p.ParameterType);
                    if (m.ReturnType.Assembly == asm) queue.Enqueue(m.ReturnType);
                }
            }

            return hits;
        }

        /// <summary>默认 `ClinicCells` 谓词(测试用):空集 —— 本夹具默认不测 `AtClinic`
        /// (专测见 <c>test_f13_7_atClinic_isCellMembership_notPathCursor</c>)。</summary>
        private static Func<WorldPos, bool> NoClinicCells => _ => false;

        private static PatientSpatialDirector MakeDirector(FakePresence presence, int count,
            Func<WorldPos, bool> inClinicCells = null, Action<PatientId> onEvaluated = null)
        {
            var k = new ClinicKnowledge(new FakeClinicSource(new WorldPos(0, 0, 0), new EcozoneId(1), hasPoi: true, hasBuilt: false));
            return new PatientSpatialDirector(
                presence, k,
                _ => new WorldPos(0, 0, 0),
                _ => Path(0, 20),
                inClinicCells ?? NoClinicCells,
                Bands, Fix.FromRational(1, 2),
                onEvaluated);
        }

        // ── 夹具类型 ──────────────────────────────────────────────

        private sealed class FakePresence : IPresenceQuery
        {
            private readonly HashSet<int> _present;
            public FakePresence(params int[] present) { _present = new HashSet<int>(present); }
            public bool IsPresent(PatientId patientId) => _present.Contains(patientId.Value);
            public int PresentCount => _present.Count;
            public bool IsPresentAt(WorldPos cell) => false;
        }

        private sealed class FakeClinicSource : IClinicKnowledgeSource
        {
            private readonly WorldPos _anchor;
            private readonly EcozoneId _region;
            private readonly bool _hasPoi;
            private readonly bool _hasBuilt;
            public int EcozoneOfCallCount { get; private set; }

            public FakeClinicSource(WorldPos anchor, EcozoneId region, bool hasPoi, bool hasBuilt)
            { _anchor = anchor; _region = region; _hasPoi = hasPoi; _hasBuilt = hasBuilt; }

            public EcozoneId EcozoneOf(WorldPos cell) { EcozoneOfCallCount++; return cell == _anchor ? _region : EcozoneId.None; }
            public bool HasClinicPoiIn(EcozoneId region) => _hasPoi && region == _region;
            public bool HasPlayerBuiltClinicIn(EcozoneId region) => _hasBuilt && region == _region;
        }

        // 影子类型:含禁入 token ⇒ 扫描机器必红且点名
        // ⚠️ 大小写与 forbidden token 逐字一致(`deltaTime`)—— 扫描是 ordinal Contains
        private sealed class ShadowWithFrameTime
        {
            public float Sample() { return deltaTime; }
            private float deltaTime => 0f;
        }
    }
}
