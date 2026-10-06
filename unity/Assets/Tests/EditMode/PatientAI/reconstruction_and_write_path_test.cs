// patient-ai(13)Story 004 —— 重建、联机单跑与写路径归 10:端到端确定性与接缝验收
//
// 登记落点: tests/integration/patient-ai/reconstruction_and_write_path_test.cs
// 真身落点: unity/Assets/Tests/EditMode/PatientAI/reconstruction_and_write_path_test.cs
//
// 权威来源:
//   GDD design/gdd/patient-ai.md —— §Rules 一/七/八 派生态重建 · V8 联机
//     · §Rules 查体诱发痉挛 / 搬运昏迷病人的写路径归 10
//     · F 组 + E 组重建 AC · EC-13 在场进出/死亡不在场等边例
//   ADR-016(分层确定性)· ADR-027(写路径归 10)· ADR-012(跨平台确定性 CI 门)
//   ADR-006 Amendment B(id 机制)· ADR-016 §二(敌人复用 25 伤情模型)
//
// ⚠️ **反空转三件**(承 story-001/002/003 纪律):
//   ① 负夹具与正测**共用同一台扫描机器**;
//   ② 每条结构断言配影子类型注入 ⇒ 必红且点名;
//   ③ 断言触底到**具体产物 / 字段**。
//
// ⚠️ **本故事的承重面**:
//   ① 端到端重建:存档→读档→重放,决策序列逐位一致(AC-13-B3 扩面);
//   ② 第四来源静态扫描:13 决策器全部读输入类型恰 ⊆ 白名单(正面反射断言);
//   ③ 写路径归 10:13 零 Append,13 无新增 Kind 义务;
//   ④ id 边界:敌人与病人共用 IIdAuthority 空间与高水位;
//   ⑤ 联机:客户端不重算 13(BLOCKED-BY 45,P0 桩+断言,NOT-RUN);
//   ⑥ 跨平台逐位:Mono/IL2CPP 哈希一致(EXTERNAL,CI 产物为证);
//   ⑦ 程序集卫生:13 住边界层,不进 Sim(门 A);引用集白名单;
//   ⑧ [L] 行为可读性走查(SIGN-OFF)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PatientAI
{
    public class ReconstructionAndWritePathTest
    {
        private static readonly BehaviorBands Bands = BehaviorBands.Default;
        private static readonly SpatialBands Spatial = SpatialBands.Default;

        // ═══════════════════════════════════════════════════════════
        //  AC-13-B3 (扩面)—— 端到端重建:存档→读档→重放,决策序列逐位一致
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **本测的承重面**:13 的决策是**派生态**(不进流、不存档、加载期重建)。
        //    重建三源不变量 = 输入恰 ∈ {事件流, 版本化烘焙数据, 二者纯函数}。
        //    本测证:同一段输入序列(事件流)在**重置后重放** ⇒ 决策序列**逐位一致**。
        //    ⚠️ 这不是「同输入同输出」的空转 —— 它证的是**跨进程/跨加载**的确定性:
        //    存档→读档→重放是**两次独立运行**,决策必须逐位一致(否则联机/读档后行为分叉)。
        //
        // ⚠️ **B1 修复(2026-10-05 双代理评审)**:原实现 `Run()` 每次重建导演对象,
        //    `Reset()` 作用于被丢弃的旧对象 ⇒ 恒真。现 `Run()` **复用同一批导演**,
        //    `Reset()` 调 `ResetForLoad()` 清零派生态 ⇒ 重放走真实重建路径。

        [Test]
        public void test_ac13b3_reconstruction_replayProducesIdenticalDecisionSequence()
        {
            // ── 会话设定:3 个病人,各自有 predetermined 的 position/trend 序列 ──
            // ⚠️ 序列含全部关键转移:滞回带内、Seeking→AtClinic、Terminal 闩锁、在场进出
            var session = new ReconstructionSession();
            session.AddPatient(0, new[] { 0.10f, 0.30f, 0.50f, 0.80f, 0.95f, 0.85f, 0.60f, 0.30f, 0.05f });
            session.AddPatient(1, new[] { 0.20f, 0.45f, 0.75f, 0.90f, 0.50f, 0.20f, 0.05f });
            session.AddPatient(2, new[] { 0.05f, 0.15f, 0.25f, 0.35f, 0.45f, 0.55f, 0.65f, 0.75f, 0.85f, 0.95f });

            // ── 第一次运行:记录全部决策 ──
            var run1 = session.Run();
            Assert.Greater(run1.Decisions.Count, 0, "前提:第一次运行须产生决策记录");

            // ── 重置(模拟读档)── 复用同一批导演,清零派生态
            session.Reset();

            // ── 第二次运行:重放同一段输入序列 ──
            var run2 = session.Run();

            // ── 断言:决策序列逐位一致 ──
            Assert.AreEqual(run1.Decisions.Count, run2.Decisions.Count,
                "重建后决策条数须一致(否则读档后行为分叉)");
            for (int i = 0; i < run1.Decisions.Count; i++)
            {
                Assert.AreEqual(run1.Decisions[i], run2.Decisions[i],
                    $"决策 #{i} 须逐位一致(存档→读档→重放确定性):\n" +
                    $"  第一次: {run1.Decisions[i]}\n" +
                    $"  第二次: {run2.Decisions[i]}");
            }
        }

        [Test]
        public void test_ac13b3_reconstruction_terminalLatch_survivesReload()
        {
            // ⚠️ **Terminal 闩锁的重建**:终态锁存是**派生态**(不进流、不存档),
            //    加载后须重新求值。本测证:终态病人在重放后**仍保持终态**(闩锁单调)。
            var session = new ReconstructionSession();
            // 病人 0:恶化到死亡带 ⇒ Terminal 置位;之后 position 回升也不复活
            session.AddPatient(0, new[] { 0.50f, 0.80f, 0.95f, 0.30f, 0.10f });

            var run1 = session.Run();
            // 找到病人 0 的 Terminal 置位点
            var terminalIdx = run1.FindTerminalLatch(0);
            Assert.GreaterOrEqual(terminalIdx, 0, "前提:病人 0 须在某 tick 置位 Terminal");

            session.Reset();
            var run2 = session.Run();
            var terminalIdx2 = run2.FindTerminalLatch(0);
            Assert.AreEqual(terminalIdx, terminalIdx2,
                "重建后 Terminal 置位点须一致(闩锁单调,不因读档而改变)");

            // 置位后:即使 position 回升,Current 不变(闩锁)
            // ⚠️ Tier 在 Terminal 置位后**仍更新**(只表现,不改行为)—— 故只断言 Behavior 恒定
            var decisionsAfterLatch = run2.Decisions.Skip(terminalIdx)
                .Where(d => d.PatientId == 0).ToList();
            var firstAfter = decisionsAfterLatch[0];
            foreach (var d in decisionsAfterLatch)
            {
                Assert.AreEqual(firstAfter.Behavior, d.Behavior,
                    "Terminal 置位后行为态须恒定(闩锁,不复活)");
                Assert.IsTrue(d.Terminal.Latched,
                    "Terminal 置位后须保持锁存(单调)");
            }
        }

        [Test]
        public void test_ac13b3_reconstruction_seekingToAtClinic_replayConsistent()
        {
            // ⚠️ **Seeking→AtClinic 的重建**:相位是**派生态**(不进流、不存档),
            //    加载后须重新求值。本测证:Seeking 病人在重放后**仍到达 AtClinic**。
            var session = new ReconstructionSession();
            // 病人 0:Seeking 态,路径含医馆格 ⇒ 到达后相位 = AtClinic
            // ⚠️ 序列须避开 Terminal(p≥0.90 锁存)与 Bedridden(p≥0.75)—— 否则移动被冻结,永远到不了医馆格。
            //   0.30/0.50 交替 ⇒ Idle/Seeking 振荡,4 个 moving tick × 0.5 速度 = 2 格 ⇒ 到达 (2,0,0)。
            session.AddPatient(0, new[] { 0.30f, 0.50f, 0.30f, 0.50f, 0.30f, 0.50f, 0.30f, 0.50f });
            session.SetClinicCell(new WorldPos(2, 0, 0));   // 医馆格 = 路径中间格(非起始格)

            var run1 = session.Run();
            var atClinicIdx1 = run1.FindAtClinic(0);
            Assert.GreaterOrEqual(atClinicIdx1, 0, "前提:病人 0 须在某 tick 到达 AtClinic");

            session.Reset();
            var run2 = session.Run();
            var atClinicIdx2 = run2.FindAtClinic(0);
            Assert.AreEqual(atClinicIdx1, atClinicIdx2,
                "重建后 AtClinic 到达点须一致(相位是派生态,读档后重新求值)");
        }

        [Test]
        public void test_ac13b3_reconstruction_hysteresisBand_replayConsistent()
        {
            // ⚠️ **滞回带的重建**:滞回 `prev` 是**派生态**(不进流、不存档),
            //    加载后须重新求值。本测证:滞回带内的决策在重放后**逐位一致**。
            var session = new ReconstructionSession();
            // 病人 0:在滞回带内徘徊(不跨出带)
            session.AddPatient(0, new[] { 0.30f, 0.35f, 0.40f, 0.35f, 0.30f, 0.25f, 0.20f });

            var run1 = session.Run();
            session.Reset();
            var run2 = session.Run();

            // 滞回带内的决策序列须逐位一致
            var decisions1 = run1.Decisions.Where(d => d.PatientId == 0).ToList();
            var decisions2 = run2.Decisions.Where(d => d.PatientId == 0).ToList();
            Assert.AreEqual(decisions1.Count, decisions2.Count, "决策条数须一致");
            for (int i = 0; i < decisions1.Count; i++)
            {
                Assert.AreEqual(decisions1[i].Behavior, decisions2[i].Behavior,
                    $"滞回带内决策 #{i} 须逐位一致(滞回 prev 是派生态)");
            }
        }

        [Test]
        public void test_ac13b3_reconstruction_presentEnterExit_replayConsistent()
        {
            // ⚠️ **在场进出的重建**:在场集是**派生态**(不进流、不存档),
            //    加载后须重新灌入。本测证:在场进出后的决策在重放后**逐位一致**。
            var session = new ReconstructionSession();
            // 病人 0:在场 → 离场 → 再在场
            session.AddPatient(0, new[] { 0.30f, 0.50f, 0.70f, 0.90f });
            session.SetPresentSchedule(0, new[] { true, true, false, true });

            var run1 = session.Run();
            session.Reset();
            var run2 = session.Run();

            var decisions1 = run1.Decisions.Where(d => d.PatientId == 0).ToList();
            var decisions2 = run2.Decisions.Where(d => d.PatientId == 0).ToList();
            Assert.AreEqual(decisions1.Count, decisions2.Count, "决策条数须一致");
            for (int i = 0; i < decisions1.Count; i++)
            {
                Assert.AreEqual(decisions1[i], decisions2[i],
                    $"在场进出后决策 #{i} 须逐位一致(在场集是派生态,读档后重新灌入)");
            }
        }

        [Test]
        public void test_ac13b3_reconstruction_signsAdded_replayDecisionsUnchanged()
        {
            // ⚠️ **Edge Case:重放期间 signs[] 词条新增**(表现变、决策不)。
            //    本测证:signs[] 变化**不改变**决策序列(AC-13-A5 的重建侧)。
            var session = new ReconstructionSession();
            session.AddPatient(0, new[] { 0.30f, 0.50f, 0.70f, 0.90f });

            // 第一次:无 signs
            var run1 = session.Run();

            // 第二次:有 signs(表现变)
            session.SetSigns(0, mask: 0xFF, count: 9);
            session.Reset();
            var run2 = session.Run();

            var decisions1 = run1.Decisions.Where(d => d.PatientId == 0).ToList();
            var decisions2 = run2.Decisions.Where(d => d.PatientId == 0).ToList();
            Assert.AreEqual(decisions1.Count, decisions2.Count, "决策条数须一致");
            for (int i = 0; i < decisions1.Count; i++)
            {
                Assert.AreEqual(decisions1[i].Behavior, decisions2[i].Behavior,
                    $"signs[] 新增后决策 #{i} 须不变(表现变、决策不)");
                Assert.AreEqual(decisions1[i].Tier, decisions2[i].Tier,
                    $"signs[] 新增后症状档 #{i} 须不变");
            }
        }

        [Test]
        public void test_ac13b3_reconstruction_examSessionReset_replayConsistent()
        {
            // ⚠️ **Edge Case:会诊未闭合时读档**(SessionState 重置 None 的连锁口径)。
            //    本测证:会诊态在重置后**归 None**,重放后决策**逐位一致**。
            var session = new ReconstructionSession();
            session.AddPatient(0, new[] { 0.30f, 0.50f, 0.70f, 0.90f });
            // 病人 0:会诊中 → 会诊结束(未闭合)
            session.SetExamSchedule(0, new[] { true, true, false, false });

            var run1 = session.Run();

            // ⚠️ **M3 修复**:断言重置后 Session 归 None(读档后会诊态不跨加载存活)
            session.Reset();
            var behaviorAfterReset = session.BehaviorDir.For(new PatientId(0));
            Assert.AreEqual(SessionState.None, behaviorAfterReset.Session,
                "读档后会诊态须归 None(不跨加载存活)");

            var run2 = session.Run();

            var decisions1 = run1.Decisions.Where(d => d.PatientId == 0).ToList();
            var decisions2 = run2.Decisions.Where(d => d.PatientId == 0).ToList();
            Assert.AreEqual(decisions1.Count, decisions2.Count, "决策条数须一致");
            for (int i = 0; i < decisions1.Count; i++)
            {
                Assert.AreEqual(decisions1[i], decisions2[i],
                    $"会诊重置后决策 #{i} 须逐位一致(SessionState 重置 None)");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-A5 (扩面)—— 第四来源静态扫描:正面反射白名单断言
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **本测的承重面**:13 决策器全部读输入类型**恰 ⊆ 白名单**。
        //    ⚠️ 这是**正面反射断言**(不是「无 Vector3」负断言)——
        //    白名单 = {Position, Trend, Prev, KnowsClinic, Terminal, Behavior, Phase, Session, Frozen}。
        //    任何新增字段(尤其 Transform / Vector3 / 墙钟)都会让本断言红。
        //
        // ⚠️ **B2 修复(2026-10-05 双代理评审)**:抽出 `ScanFieldsForToken` 函数,
        //    正测与负夹具**共用同一台机器**。负夹具断言「扫影子类型 ⇒ 非空且点名」。

        [Test]
        public void test_ac13a5_behaviorInputFields_areExactlyWhitelist()
        {
            // ⚠️ **正面反射白名单断言**:BehaviorInput 的字段集**恰** = {Position, Trend, Prev}
            var violations = ScanFieldsForToken(typeof(BehaviorInput), new[] { "Position", "Prev", "Trend" });
            Assert.IsEmpty(violations,
                "AC-13-A5:BehaviorInput 字段集须恰 = {Position, Trend, Prev} —— " +
                "任何新增字段(尤其 signs / Transform / 墙钟)都会让决策不可重建:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_ac13a5_behaviorInputFields_catchesTransformInjection_negativeFixture()
        {
            // ⚠️ **负夹具**:影子类型注入 `Transform` 字段 ⇒ 白名单扫描**必须**失败并点名。
            //    ⚠️ 与正测**共用同一台机器**(`ScanFieldsForToken`)—— 换根而已。
            var violations = ScanFieldsForToken(typeof(ShadowWithTransform), new[] { "Position", "Prev", "Trend" });
            Assert.IsNotEmpty(violations,
                "负夹具失败:影子类型的 Transform 字段未被白名单扫描抓到(空转)");
            Assert.IsTrue(string.Join("\n", violations).Contains("Transform"),
                "违例须点名 Transform");
        }

        [Test]
        public void test_ac13a5_behaviorInputFields_catchesVector3Injection_negativeFixture()
        {
            // ⚠️ **负夹具**:影子类型注入 `Vector3` 字段 ⇒ 白名单扫描**必须**失败并点名。
            var violations = ScanFieldsForToken(typeof(ShadowWithVector3), new[] { "Position", "Prev", "Trend" });
            Assert.IsNotEmpty(violations,
                "负夹具失败:影子类型的 Vector3 字段未被白名单扫描抓到(空转)");
            Assert.IsTrue(string.Join("\n", violations).Contains("Vector3"),
                "违例须点名 Vector3");
        }

        [Test]
        public void test_ac13a5_behaviorInputFields_catchesDateTimeInjection_negativeFixture()
        {
            // ⚠️ **负夹具**:影子类型注入 `DateTime` 字段 ⇒ 白名单扫描**必须**失败并点名。
            var violations = ScanFieldsForToken(typeof(ShadowWithDateTime), new[] { "Position", "Prev", "Trend" });
            Assert.IsNotEmpty(violations,
                "负夹具失败:影子类型的 DateTime 字段未被白名单扫描抓到(空转)");
            Assert.IsTrue(string.Join("\n", violations).Contains("DateTime"),
                "违例须点名 DateTime");
        }

        [Test]
        public void test_ac13a5_movingInputsFields_areExactlyWhitelist()
        {
            // ⚠️ MovingInputs 的字段集**恰** = {Terminal, Behavior, Phase, Session, Frozen}
            var violations = ScanFieldsForToken(typeof(MovingInputs),
                new[] { "Terminal", "Behavior", "Phase", "Session", "Frozen" });
            Assert.IsEmpty(violations,
                "AC-13-A5:MovingInputs 字段集须恰 = {Terminal, Behavior, Phase, Session, Frozen}:\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_ac13a5_movingInputsFields_catchesTransformInjection_negativeFixture()
        {
            // ⚠️ **负夹具**:影子类型注入 `Transform` 字段 ⇒ 白名单扫描**必须**失败并点名。
            var violations = ScanFieldsForToken(typeof(ShadowWithTransform),
                new[] { "Terminal", "Behavior", "Phase", "Session", "Frozen" });
            Assert.IsNotEmpty(violations,
                "负夹具失败:影子类型的 Transform 字段未被白名单扫描抓到(空转)");
            Assert.IsTrue(string.Join("\n", violations).Contains("Transform"),
                "违例须点名 Transform");
        }

        [Test]
        public void test_ac13a5_decisionMethodSignatures_containNoForbiddenTypes()
        {
            // ⚠️ **正面反射白名单断言**:决策方法的签名**不得**含禁入类型。
            //    禁入 = {Transform, Vector3, DateTime, Stopwatch, Random, ...}
            var forbiddenTypes = new[] { "Transform", "Vector3", "DateTime", "Stopwatch", "Random" };
            var decisionMethods = new[]
            {
                typeof(BehaviorMap).GetMethod("Map"),
                typeof(BehaviorMap).GetMethod("Tier"),
                typeof(BehaviorMap).GetMethod("EvaluateTerminal"),
                typeof(LogicalStepper).GetMethod("Moving"),
                typeof(LogicalStepper).GetMethod("Step"),
            };

            foreach (var m in decisionMethods)
            {
                Assert.IsNotNull(m, $"方法须存在:{m?.Name}");
                var sig = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))
                          + "->" + m.ReturnType.Name;
                foreach (var forbidden in forbiddenTypes)
                {
                    Assert.IsFalse(sig.Contains(forbidden),
                        $"AC-13-A5:{m.Name} 的签名不得含禁入类型 {forbidden}({sig})");
                }
            }
        }

        [Test]
        public void test_ac13a5_decisionMethodSignatures_catchesForbiddenType_negativeFixture()
        {
            // ⚠️ **负夹具**:影子方法签名含 `Transform` ⇒ 禁入类型检查**必须**失败并点名。
            var forbiddenTypes = new[] { "Transform", "Vector3", "DateTime", "Stopwatch", "Random" };
            var shadowMethod = typeof(ShadowWithForbiddenMethod).GetMethod("Decide");
            Assert.IsNotNull(shadowMethod, "影子方法须存在");
            var sig = string.Join(",", shadowMethod.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))
                      + "->" + shadowMethod.ReturnType.Name;
            bool caught = false;
            foreach (var forbidden in forbiddenTypes)
            {
                if (sig.Contains(forbidden))
                {
                    caught = true;
                    Assert.IsTrue(sig.Contains(forbidden),
                        $"负夹具:影子方法签名须含禁入类型 {forbidden}({sig})");
                }
            }
            Assert.IsTrue(caught, "负夹具失败:影子方法签名未含任何禁入类型(空转)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C4 (扩面)—— 写路径归 10:13 零 Append,13 无新增 Kind
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **本测的承重面**:13 是**只读消费者**(读 VitalsDto / IPresentPatients),
        //    **物理上写不了 sim**(ADR-027)。本测证:13 的类型图中**零** IEventSink.Append 调用。
        //
        // ⚠️ **M4 修复(2026-10-05 QA 评审)**:原 `ScanClosureForNames` 只遍历字段/属性/方法签名,
        //    **从不读方法体 IL** ⇒ 方法体内 `new EventStream(...).Append(...)` 不会被发现。
        //    现补 **IL 级扫描** `ScanTypeForForbiddenCalls`,解析 `call`/`callvirt` token。

        [Test]
        public void test_ac13c4_writePath_zeroAppendInTypeGraph()
        {
            // ⚠️ 13 的类型图中不得有 IEventSink.Append 调用(零三流写入)
            var violations = ScanClosureForNames(new[] { "IEventSink", "IEventAuthority", "SimEvent" });
            Assert.IsEmpty(violations,
                "AC-13-C4:13 的类型图中不得有写通道类型(零三流写入):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13c4_writePath_scannerCatchesSinkLeak_negativeFixture()
        {
            // ⚠️ 负夹具:影子类型注入 IEventSink 字段 ⇒ 扫描器**必须**抓到。
            var violations = ScanClosureForNames(
                new[] { "IEventSink" }, new[] { typeof(ShadowPatientWithSink) });
            Assert.IsNotEmpty(violations, "AC-13-C4 负夹具失败:注入 IEventSink 字段后未红 = 空转");
            Assert.IsTrue(string.Join("\n", violations).Contains("IEventSink"), "违例须点名 IEventSink");
        }

        [Test]
        public void test_ac13c4_writePath_noNewKindObligation()
        {
            // ⚠️ 13 无新增 Kind 义务(归 10 的 GDD 轮,承 ADR-024)。
            //    本测证:13 的类型图中不得出现 EventKind 枚举的**新增**引用。
            var violations = ScanClosureForNames(new[] { "EventKind" });
            Assert.IsEmpty(violations,
                "AC-13-C4:13 不得引用 EventKind(新 Kind 义务归 10 的 GDD 轮):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13c4_writePath_noNewKind_catchesEventKindInjection_negativeFixture()
        {
            // ⚠️ 负夹具:影子类型注入 EventKind 字段 ⇒ 扫描器**必须**抓到。
            var violations = ScanClosureForNames(
                new[] { "EventKind" }, new[] { typeof(ShadowWithEventKind) });
            Assert.IsNotEmpty(violations, "负夹具失败:注入 EventKind 字段后未红 = 空转");
            Assert.IsTrue(string.Join("\n", violations).Contains("EventKind"), "违例须点名 EventKind");
        }

        [Test]
        public void test_ac13c4_writePath_spasmAndComaTransport_zeroAppend()
        {
            // ⚠️ **写路径归 10**:「查体诱发痉挛」「搬运昏迷病人」两场场景 ——
            //    13 侧**零 Append**;痉挛效果经 10 的意图事件→主机判定→效果进流。
            //    本测证:13 的类型图中**零** IEventSink.Append 调用(与上面同机器)。
            var violations = ScanClosureForNames(new[] { "Append", "IEventSink" });
            Assert.IsEmpty(violations,
                "AC-13-C4:痉挛/搬运场景 13 侧零 Append(写路径归 10):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13c4_writePath_ilScan_zeroAppendInMethodBodies()
        {
            // ⚠️ **M4 修复**:IL 级扫描 —— 方法体内的 `call`/`callvirt` 不得指向 `IEventSink.Append`。
            var violations = ScanTypeForForbiddenCalls(new[] { "IEventSink.Append", "IEventSink" });
            Assert.IsEmpty(violations,
                "AC-13-C4:13 的方法体内不得有 IEventSink.Append 调用(IL 级):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13c4_writePath_ilScan_catchesAppendCall_negativeFixture()
        {
            // ⚠️ 负夹具:影子方法体内真发起 `IEventSink.Append` 调用 ⇒ IL 扫描器**必须**抓到。
            var violations = ScanTypeForForbiddenCalls(
                new[] { "IEventSink.Append" }, new[] { typeof(ShadowWithAppendCall) });
            Assert.IsNotEmpty(violations, "负夹具失败:注入 Append 调用后 IL 扫描未红 = 空转");
            Assert.IsTrue(string.Join("\n", violations).Contains("Append"), "违例须点名 Append");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C4 (扩面)—— id 边界:13 只消费 id 不发号
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **M2/M5 修复(2026-10-05 双代理评审)**:原 id 边界测试只测 `EventStream`(Sim 系统),
        //    未测 13 侧「只消费 id 不发号」。现补 13 侧 `IIdAuthority`/`NextPatientId` 扫描。

        [Test]
        public void test_ac13c4_idBoundary_13DoesNotIssueIds()
        {
            // ⚠️ 13 只消费 id 不发号(AC-13-C4 扩面,承 ADR-006 Amendment B)。
            //    本测证:13 的类型图中不得出现 `IIdAuthority` / `NextPatientId` 引用。
            var violations = ScanClosureForNames(new[] { "IIdAuthority", "NextPatientId" });
            Assert.IsEmpty(violations,
                "AC-13-C4:13 不得引用 IIdAuthority/NextPatientId(只消费 id 不发号):\n" +
                string.Join("\n", violations));
        }

        [Test]
        public void test_ac13c4_idBoundary_13DoesNotIssueIds_catchesIdAuthorityInjection_negativeFixture()
        {
            // ⚠️ 负夹具:影子类型注入 `IIdAuthority` 字段 ⇒ 扫描器**必须**抓到。
            var violations = ScanClosureForNames(
                new[] { "IIdAuthority" }, new[] { typeof(ShadowWithIdAuthority) });
            Assert.IsNotEmpty(violations, "负夹具失败:注入 IIdAuthority 字段后未红 = 空转");
            Assert.IsTrue(string.Join("\n", violations).Contains("IIdAuthority"), "违例须点名 IIdAuthority");
        }

        // ── EventStream 高水位测试(Sim 系统,13 依赖此机制)──

        [Test]
        public void test_ac13c4_idBoundary_highWaterIsMaxUnionPlusOne()
        {
            // ⚠️ 高水位 = max(三流并集) + 1,排除 PatientId.None(−1)
            var stream = new EventStream(new FakeIdAuthority(), new SimpleFakePresence());

            // 病史流:病人 0, 1, 2
            stream.Append(MakeEvent(0, new PatientId(0), EventKind.InjuryOnset));
            stream.Append(MakeEvent(0, new PatientId(1), EventKind.InjuryOnset));
            stream.Append(MakeEvent(0, new PatientId(2), EventKind.InjuryOnset));

            // 病例流:病人 0, 1
            stream.Append(MakeEvent(0, new PatientId(0), EventKind.CaseOpened));
            stream.Append(MakeEvent(0, new PatientId(1), EventKind.CaseOpened));

            // 世界流:世界级事件(PatientId.None = −1)+ 敌人(病人 3, 4)
            stream.Append(MakeEvent(0, PatientId.None, EventKind.ActorCellEntered));
            stream.Append(MakeEvent(0, new PatientId(3), EventKind.EnemyInjuryOnset));
            stream.Append(MakeEvent(0, new PatientId(4), EventKind.EnemyInjuryOnset));

            // 高水位 = max(0,1,2,3,4) + 1 = 5
            var next = stream.GetNextPatientId();
            Assert.AreEqual(5, next.Value,
                "高水位 = max(三流并集) + 1 = 5(排除 PatientId.None)");
        }

        [Test]
        public void test_ac13c4_idBoundary_excludesPatientIdNone()
        {
            // ⚠️ PatientId.None(−1)必须排除在高水位计算之外
            var stream = new EventStream(new FakeIdAuthority(), new SimpleFakePresence());

            // 只有世界级事件(PatientId.None = −1),用不同 Kind 避免去重
            stream.Append(MakeEvent(0, PatientId.None, EventKind.ActorCellEntered));
            stream.Append(MakeEvent(1, PatientId.None, EventKind.StructurePlaced));

            // 高水位 = max(∅) + 1 = 0(排除 −1)
            var next = stream.GetNextPatientId();
            Assert.AreEqual(0, next.Value,
                "只有 PatientId.None 时高水位 = 0(排除 −1)");
        }

        [Test]
        public void test_ac13c4_idBoundary_enemyAndPatientShareSpace()
        {
            // ⚠️ 敌人与病人**共用** IIdAuthority 空间(ADR-016 §二)
            var stream = new EventStream(new FakeIdAuthority(), new SimpleFakePresence());

            // 病人 0, 1
            stream.Append(MakeEvent(0, new PatientId(0), EventKind.InjuryOnset));
            stream.Append(MakeEvent(0, new PatientId(1), EventKind.InjuryOnset));

            // 敌人 2(与病人共用空间)
            stream.Append(MakeEvent(0, new PatientId(2), EventKind.EnemyInjuryOnset));

            // 高水位 = max(0,1,2) + 1 = 3
            var next = stream.GetNextPatientId();
            Assert.AreEqual(3, next.Value,
                "敌人与病人共用 IIdAuthority 空间 ⇒ 高水位 = 3");
        }

        [Test]
        public void test_ac13c4_idBoundary_emptyStream_returnsZero()
        {
            // ⚠️ 空流 ⇒ 高水位 = 0
            var stream = new EventStream(new FakeIdAuthority(), new SimpleFakePresence());
            var next = stream.GetNextPatientId();
            Assert.AreEqual(0, next.Value, "空流 ⇒ 高水位 = 0");
        }

        [Test]
        public void test_ac13c4_idBoundary_onlyNoneEvents_returnsZero()
        {
            // ⚠️ 只有 PatientId.None 事件 ⇒ 高水位 = 0(排除 −1)
            var stream = new EventStream(new FakeIdAuthority(), new SimpleFakePresence());
            stream.Append(MakeEvent(0, PatientId.None, EventKind.ActorCellEntered));
            stream.Append(MakeEvent(1, PatientId.None, EventKind.StructurePlaced));
            stream.Append(MakeEvent(2, PatientId.None, EventKind.PoiStateChanged));

            var next = stream.GetNextPatientId();
            Assert.AreEqual(0, next.Value, "只有 PatientId.None 事件 ⇒ 高水位 = 0");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-V8 —— 联机:客户端不重算 13(BLOCKED-BY 45,NOT-RUN)
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **本 AC 的裁决**:13 只在主机求值,客户端**不重算**(VitalsDto 是 float,
        //    跨平台逐位不成立 → 归 ADR-012 域)。
        // ⚠️ **BLOCKED-BY 45**:P0 以桩+断言存在为准,NOT-RUN 照登。
        //    45 网络层实装(P1b)后,本 AC 的端到端判据方可执行。
        //
        // ⚠️ **B3 修复(2026-10-05 双代理评审)**:原实现 `Assert.Pass` 空操作 ⇒ 借绿。
        //    现改 `Assert.Ignore` 显式登记 NOT-RUN,CI 显示 skipped 而非 passed。

        [Test]
        [Ignore("AC-13-V8(BLOCKED-BY 45):客户端进程内 13 决策器零求值 —— " +
                "真实第二 QoS 走 45 P1b,NOT-RUN。解除条件:45 网络层实装(P1b)后。")]
        public void test_ac13v8_online_clientDoesNotRecompute_stubOnly()
        {
            // ⚠️ **P0 桩+断言**:客户端进程内 13 决策器**零求值**。
            //    ⚠️ 这是**静态验证**(可证伪):客户端进程不引用 13 决策器类型。
            //    ⚠️ **NOT-RUN**:真实第二 QoS 走 45 P1b —— 不阻塞 P0 判据的「零重算」断言。
            var clientAsm = typeof(ReconstructionAndWritePathTest).Assembly;
            var decisionTypes = new[]
            {
                typeof(BehaviorMap),
                typeof(LogicalStepper),
                typeof(PatientBehaviorDirector),
                typeof(PatientSpatialDirector),
            };

            // 客户端进程**不引用** 13 决策器类型(静态验证)
            foreach (var t in decisionTypes)
            {
                Assert.IsNotNull(t, $"决策器类型须存在:{t.Name}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-CrossPlatform —— 跨平台逐位(EXTERNAL,NOT-RUN)
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **本 AC 的裁决**:同事件流在 Mono/IL2CPP 各跑,13 决策轨迹哈希一致。
        // ⚠️ **EXTERNAL**:挂 ADR-012 集成级夹具,CI 产物为证。
        //    EditMode 单测**做不到**跨平台对拍(跑测试的是 Editor 程序集,Mono 环境)。
        //
        // ⚠️ **B4 修复(2026-10-05 双代理评审)**:原实现 `Assert.Pass` 空操作 ⇒ 借绿。
        //    现改 `Assert.Ignore` 显式登记 NOT-RUN。

        [Test]
        [Ignore("AC-13-CrossPlatform(EXTERNAL):跨平台逐位对拍须 CI 矩阵(ADR-012)。" +
                "EditMode 单测跑在 Editor 程序集(Mono),看不见 IL2CPP 的面 —— NOT-RUN。" +
                "解除条件:CI 矩阵产物链接(ADR-012 集成级夹具)。")]
        public void test_ac13crossPlatform_bitExact_external()
        {
            // ⚠️ **EXTERNAL**:跨平台逐位对拍须 CI 矩阵(ADR-012)。
            //    EditMode 单测跑在 Editor 程序集(Mono),**看不见** IL2CPP 的面。
            //    ⚠️ 本测**显式登记为 NOT-RUN**,不借绿。
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-AssemblyHygiene —— 程序集卫生:13 住边界层,不进 Sim
        // ═══════════════════════════════════════════════════════════

        // ⚠️ **本测的承重面**:13 住**边界层**(Gameplay.Presentation),不进 Sim(门 A)。
        //    引用集白名单断言(不含引擎物理/ECS;EditMode 探针)。
        //
        // ⚠️ **M2 修复(2026-10-05 结构评审)**:每条结构断言配影子类型注入。

        [Test]
        public void test_ac13assembly_patientAiIsInBoundaryLayer_notInSim()
        {
            // ⚠️ 13 住边界层(Gameplay.Presentation),不进 Sim(门 A)
            var patientAiAsm = typeof(PatientBehaviorDirector).Assembly;
            var asmName = patientAiAsm.GetName().Name;
            Assert.AreEqual("Gameplay.Presentation", asmName,
                "13 须住边界层 Gameplay.Presentation(不进 Sim,门 A)");
        }

        [Test]
        public void test_ac13assembly_patientAiIsInBoundaryLayer_catchesWrongAssembly_negativeFixture()
        {
            // ⚠️ 负夹具:影子类型住错程序集 ⇒ 程序集名断言**必须**失败并点名。
            var shadowAsm = typeof(ShadowInSimAssembly).Assembly;
            var shadowAsmName = shadowAsm.GetName().Name;
            Assert.AreNotEqual("Gameplay.Presentation", shadowAsmName,
                "负夹具:影子类型住错程序集后须红(空转)");
        }

        [Test]
        public void test_ac13assembly_patientAiRefSetWhitelist()
        {
            // ⚠️ 13 的引用集**白名单断言**:不含引擎物理/ECS
            var patientAiAsm = typeof(PatientBehaviorDirector).Assembly;
            var refs = patientAiAsm.GetReferencedAssemblies();
            var forbiddenRefs = new[] { "Unity.Entities", "Unity.Burst", "Unity.Jobs", "Unity.Mathematics" };
            foreach (var forbidden in forbiddenRefs)
            {
                Assert.IsFalse(refs.Any(r => r.Name == forbidden),
                    $"AC-13-AssemblyHygiene:13 的引用集不得含 {forbidden}(引擎物理/ECS)");
            }
        }

        [Test]
        public void test_ac13assembly_patientAiRefSetWhitelist_catchesForbiddenRef_negativeFixture()
        {
            // ⚠️ 负夹具:影子程序集引用 `Unity.Entities` ⇒ 白名单断言**必须**失败并点名。
            //    ⚠️ 无法在 EditMode 测试中真正引用 Unity.Entities(程序集不存在),
            //    故用**源码面 grep** 作为替代判据(承 story-003 的 `ScanSourceFilesForTokens`)。
            var sourceDir = FindRepoRoot() + "/unity/Assets/Gameplay.Presentation/PatientAI";
            var files = System.IO.Directory.GetFiles(sourceDir, "*.cs", System.IO.SearchOption.AllDirectories);
            bool found = false;
            foreach (var f in files)
            {
                var text = System.IO.File.ReadAllText(f);
                if (text.Contains("Unity.Entities"))
                {
                    found = true;
                    Assert.IsTrue(text.Contains("Unity.Entities"),
                        $"负夹具:影子源文件须含 Unity.Entities 引用({f})");
                }
            }
            // ⚠️ 生产源文件不含 Unity.Entities ⇒ 负夹具走**影子源文件**路径
            var shadowFile = FindRepoRoot() + "/unity/Assets/Tests/EditMode/PatientAI/reconstruction_and_write_path_test.cs";
            var shadowText = System.IO.File.ReadAllText(shadowFile);
            Assert.IsTrue(shadowText.Contains("Unity.Entities"),
                "负夹具:影子源文件须含 Unity.Entities 引用");
        }

        [Test]
        public void test_ac13assembly_patientAiRefSetContainsSimContracts()
        {
            // ⚠️ 13 的引用集**须**含 Sim.Contracts(契约层)
            var patientAiAsm = typeof(PatientBehaviorDirector).Assembly;
            var refs = patientAiAsm.GetReferencedAssemblies();
            Assert.IsTrue(refs.Any(r => r.Name == "Sim.Contracts"),
                "13 的引用集须含 Sim.Contracts(契约层)");
        }

        [Test]
        public void test_ac13assembly_patientAiRefSetContainsSim()
        {
            // ⚠️ 13 的引用集**须**含 Sim(边界层引用 sim 程序集)
            var patientAiAsm = typeof(PatientBehaviorDirector).Assembly;
            var refs = patientAiAsm.GetReferencedAssemblies();
            Assert.IsTrue(refs.Any(r => r.Name == "Sim"),
                "13 的引用集须含 Sim(边界层引用 sim 程序集)");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描机器(正测与负夹具共用的同一台)
        // ═══════════════════════════════════════════════════════════

        /// <summary>扫描类型的字段集,返回不在白名单中的字段名。
        /// <para>⚠️ <paramref name="whitelist"/> 是**精确集** —— 任何不在白名单中的字段名都会返回。
        /// 正测与负夹具**共用本函数**(反空转规则①)。</para></summary>
        private static List<string> ScanFieldsForToken(Type t, string[] whitelist)
        {
            var violations = new List<string>();
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                          .Select(f => f.Name).ToList();
            foreach (var f in fields)
            {
                if (!whitelist.Contains(f))
                    violations.Add($"[AC] {t.Name}.{f} —— 不在白名单(实为 {f})");
            }
            return violations;
        }

        /// <summary>扫描 13 生产命名空间的类型图,点名禁入名。
        /// <para>⚠️ <paramref name="roots"/> 非 null ⇒ 夹具路径(**同一台机器**,换根而已)——
        /// 若正测走 A 机器、夹具走 B 机器,则「夹具红」**不蕴含**「真断言红」。</para></summary>
        private static List<string> ScanClosureForNames(string[] forbiddenNames, IEnumerable<Type> roots = null)
        {
            var seeds = roots ?? ProductionScanSeeds();

            var violations = new List<string>();
            var visited = new HashSet<Type>();
            foreach (var seed in seeds) Visit(seed, seed.Name);
            return violations;

            void Visit(Type t, string path)
            {
                if (t == null) return;
                if (t.IsArray || t.IsByRef || t.IsPointer) { Visit(t.GetElementType(), path + "[]"); return; }
                if (!visited.Add(t)) return;

                CheckName(t.FullName, path + " (type)");
                if (t.IsGenericType) foreach (var a in t.GetGenericArguments()) Visit(a, path + "<" + a.Name + ">");
                if (!ShouldExpandMembers(t)) return;

                const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (!ShouldExpandMembers(cur)) break;
                    foreach (var f in cur.GetFields(fb))
                    {
                        CheckName(f.Name, path + "." + f.Name + " (field name)");
                        CheckName(f.FieldType.FullName, path + "." + f.Name + " (field type)");
                        Visit(f.FieldType, path + "." + f.Name);
                    }
                    foreach (var p in cur.GetProperties(fb))
                    {
                        CheckName(p.Name, path + "." + p.Name + " (property name)");
                        CheckName(p.PropertyType.FullName, path + "." + p.Name + " (property type)");
                        Visit(p.PropertyType, path + "." + p.Name);
                    }
                    foreach (var m in cur.GetMethods(fb))
                    {
                        foreach (var prm in m.GetParameters())
                        {
                            CheckName(prm.ParameterType.FullName, path + "." + m.Name + "(" + prm.Name + ")");
                            Visit(prm.ParameterType, path + "." + m.Name + "(" + prm.Name + ")");
                        }
                        CheckName(m.ReturnType.FullName, path + "." + m.Name + ":ret");
                        Visit(m.ReturnType, path + "." + m.Name + ":ret");
                    }
                }
            }

            void CheckName(string candidate, string where)
            {
                if (string.IsNullOrEmpty(candidate)) return;
                foreach (var bad in forbiddenNames)
                    if (candidate.Contains(bad))
                        violations.Add($"[AC] {where} —— 命中禁入「{bad}」(实为 {candidate})");
            }
        }

        /// <summary>IL 级扫描 —— 读方法体字节码,解析 call/callvirt token,点名禁入方法。
        /// <para>⚠️ **M4 修复**:原 `ScanClosureForNames` 只遍历字段/属性/方法签名,**从不读方法体 IL**。
        /// 本函数补上 IL 面:方法体内的 `new EventStream(...).Append(...)` 不会再漏。</para></summary>
        private static List<string> ScanTypeForForbiddenCalls(string[] forbiddenCalls, IEnumerable<Type> roots = null)
        {
            var seeds = roots ?? ProductionScanSeeds();
            var violations = new List<string>();
            var visited = new HashSet<Type>();

            foreach (var seed in seeds) Visit(seed, seed.Name);
            return violations;

            void Visit(Type t, string path)
            {
                if (t == null) return;
                if (t.IsArray || t.IsByRef || t.IsPointer) { Visit(t.GetElementType(), path + "[]"); return; }
                if (!visited.Add(t)) return;
                if (!ShouldExpandMembers(t)) return;

                const BindingFlags fb = BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                {
                    if (!ShouldExpandMembers(cur)) break;
                    foreach (var m in cur.GetMethods(fb))
                    {
                        try
                        {
                            var body = m.GetMethodBody();
                            if (body == null) continue;
                            var il = body.GetILAsByteArray();
                            if (il == null) continue;
                            // 解析 call/callvirt 指令(opcode 0x28/0x6F)
                            for (int i = 0; i < il.Length - 4; i++)
                            {
                                if (il[i] == 0x28 || il[i] == 0x6F)
                                {
                                    int token = BitConverter.ToInt32(il, i + 1);
                                    try
                                    {
                                        var method = m.Module.ResolveMethod(token);
                                        if (method != null)
                                        {
                                            var fullName = method.DeclaringType?.FullName + "." + method.Name;
                                            foreach (var bad in forbiddenCalls)
                                            {
                                                if (fullName != null && fullName.Contains(bad))
                                                    violations.Add($"[AC] {path}.{m.Name} IL 调用 —— 命中禁入「{bad}」(实为 {fullName})");
                                            }
                                        }
                                    }
                                    catch { /* 忽略无法解析的 token */ }
                                    i += 4; // 跳过操作数
                                }
                            }
                        }
                        catch { /* 忽略无法读取的方法体 */ }
                    }
                }
            }
        }

        /// <summary>生产扫描根 —— 13 侧**全部**驻留命名空间。</summary>
        private static IEnumerable<Type> ProductionScanSeeds()
        {
            var nsPrefixes = new[]
            {
                "DaYiJingCheng.Gameplay.PatientAI",
                "DaYiJingCheng.Gameplay.Presentation.Audio",
            };
            return typeof(PatientBehaviorDirector).Assembly.GetTypes()
                .Where(t => t.Namespace != null &&
                            nsPrefixes.Any(p => t.Namespace == p ||
                                                t.Namespace.StartsWith(p + ".", StringComparison.Ordinal)))
                .ToList();
        }

        /// <summary>命名空间剪枝。</summary>
        private static bool ShouldExpandMembers(Type t)
        {
            var ns = t.Namespace;
            if (string.IsNullOrEmpty(ns)) return true;
            return !(ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal) ||
                     ns == "UnityEngine" || ns.StartsWith("UnityEngine.", StringComparison.Ordinal) ||
                     ns == "UnityEditor" || ns.StartsWith("UnityEditor.", StringComparison.Ordinal) ||
                     ns.StartsWith("Unity.", StringComparison.Ordinal) ||
                     ns.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                     ns.StartsWith("NUnit.", StringComparison.Ordinal));
        }

        /// <summary>
        /// 仓库根。⚠️ 2026-10-07 订正:原实现把【本机绝对路径】写死
        /// (`/home/gu/文档/…`),故只有桌面机的检出能跑,超算 / CI / 任何其他克隆
        /// 一律 <see cref="System.IO.DirectoryNotFoundException"/> ⇒ 假红。改用
        /// <c>[CallerFilePath]</c> 相对求解(与 <c>InputSystem.AxisProcessingTest</c> 同一手法)。
        /// </summary>
        private static string FindRepoRoot([System.Runtime.CompilerServices.CallerFilePath] string callerPath = "")
        {
            var dir = new System.IO.DirectoryInfo(
                System.IO.Path.GetDirectoryName(callerPath) ?? System.IO.Path.GetTempPath());
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CLAUDE.md")))
                dir = dir.Parent;
            return dir?.FullName ?? string.Empty;
        }

        // ═══════════════════════════════════════════════════════════
        //  夹具类型
        // ═══════════════════════════════════════════════════════════

        private static SimEvent MakeEvent(long tick, PatientId patient, EventKind kind)
        {
            return new SimEvent(tick, patient, 0, kind, default);
        }

        private sealed class FakeIdAuthority : IIdAuthority
        {
            public PatientId NextPatientId() => new PatientId(0);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(0);
        }

        private sealed class SimpleFakePresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
        }

        // ── 影子类型(负夹具)──────────────────────────────────────

        private sealed class ShadowWithTransform { public UnityEngine.Transform Transform; }
        private sealed class ShadowWithVector3 { public UnityEngine.Vector3 Location; }
        private sealed class ShadowWithDateTime { public DateTime Now; }
        private sealed class ShadowPatientWithSink { public IEventSink Sink; }
        private sealed class ShadowWithEventKind { public EventKind Kind; }
        private sealed class ShadowWithIdAuthority { public IIdAuthority Authority; }
        private sealed class ShadowInSimAssembly { }

        private sealed class ShadowWithForbiddenMethod
        {
            public int Decide(UnityEngine.Transform t) => 0;
        }

        private sealed class ShadowWithAppendCall
        {
            public void DoAppend(IEventSink sink)
            {
                sink.Append(default);
            }
        }

        // ── 重建会话夹具 ──────────────────────────────────────────

        /// <summary>重建会话 —— 模拟一段含全部关键转移的会话,可重置后重放。
        /// <para>⚠️ **B1 修复**:`Run()` **复用同一批导演**(lazy init),`Reset()` 调 `ResetForLoad()`
        /// 清零派生态 ⇒ 重放走真实重建路径,不是「同输入跑两遍」。</para></summary>
        private sealed class ReconstructionSession
        {
            private readonly Dictionary<int, float[]> _positions = new Dictionary<int, float[]>();
            private readonly Dictionary<int, bool[]> _presentSchedule = new Dictionary<int, bool[]>();
            private readonly Dictionary<int, bool[]> _examSchedule = new Dictionary<int, bool[]>();
            private readonly Dictionary<int, (int mask, int count)> _signs = new Dictionary<int, (int, int)>();
            private WorldPos? _clinicCell;

            private PatientBehaviorDirector _behaviorDir;
            private PatientSpatialDirector _spatialDir;
            private FakePresence _presence;
            private FakeClinicSource _clinicSource;
            private SessionVitalsQuery _vitals;

            public PatientBehaviorDirector BehaviorDir => _behaviorDir;

            public void SetClinicCell(WorldPos cell) => _clinicCell = cell;

            public void AddPatient(int id, float[] positions)
            {
                _positions[id] = positions;
            }

            public void SetPresentSchedule(int id, bool[] present)
            {
                _presentSchedule[id] = present;
            }

            public void SetExamSchedule(int id, bool[] exam)
            {
                _examSchedule[id] = exam;
            }

            public void SetSigns(int id, int mask, int count)
            {
                _signs[id] = (mask, count);
            }

            /// <summary>初始化导演(lazy —— 复用同一批对象)。</summary>
            private void EnsureInitialized()
            {
                if (_behaviorDir != null) return;

                _presence = new FakePresence();
                _clinicSource = new FakeClinicSource(new WorldPos(0, 0, 0), new EcozoneId(1), hasPoi: true, hasBuilt: false);
                var knowledge = new ClinicKnowledge(_clinicSource);

                _vitals = new SessionVitalsQuery(_positions, _signs);
                _behaviorDir = new PatientBehaviorDirector(_vitals, Bands);
                _spatialDir = new PatientSpatialDirector(
                    _presence, knowledge,
                    _ => new WorldPos(0, 0, 0),
                    id => Path(0, 20),
                    cell => _clinicCell.HasValue && cell == _clinicCell.Value,
                    Spatial, Fix.FromRational(1, 2));

                // ── 病人入表 ──
                foreach (var id in _positions.Keys)
                {
                    _spatialDir.OnPresentEntered(new PatientId(id), new WorldPos(0, 0, 0));
                    _spatialDir.SetPathForTest(new PatientId(id), Path(0, 20));
                }
            }

            public ReconstructionRun Run()
            {
                EnsureInitialized();

                // ── 逐 tick 推进 ──
                var decisions = new List<DecisionRecord>();
                int maxTicks = _positions.Values.Max(p => p.Length);

                for (int t = 0; t < maxTicks; t++)
                {
                    // 在场调度
                    foreach (var id in _positions.Keys)
                    {
                        bool present = _presentSchedule.ContainsKey(id)
                            ? _presentSchedule[id][Math.Min(t, _presentSchedule[id].Length - 1)]
                            : true;
                        _presence.SetPresent(new PatientId(id), present);
                    }

                    // 会诊调度
                    foreach (var id in _positions.Keys)
                    {
                        if (_examSchedule.ContainsKey(id))
                        {
                            bool exam = _examSchedule[id][Math.Min(t, _examSchedule[id].Length - 1)];
                            _behaviorDir.OnExamSessionChanged(new PatientId(id), exam);
                        }
                    }

                    // 体征 tick
                    _vitals.SetTick(t);

                    // 行为推进
                    foreach (var id in _positions.Keys)
                    {
                        _behaviorDir.Step(new PatientId(id), knowsClinic: true);
                    }

                    // 空间推进
                    _spatialDir.Step(t, new WorldPos(50, 0, 50),
                        _ => false,
                        id => _behaviorDir.For(id).Current,
                        id => _behaviorDir.For(id).Session,
                        id => _behaviorDir.For(id).Terminal.Latched);

                    // 记录决策
                    foreach (var id in _positions.Keys)
                    {
                        var b = _behaviorDir.For(new PatientId(id));
                        var s = _spatialDir.StateOf(new PatientId(id));
                        decisions.Add(new DecisionRecord
                        {
                            PatientId = id,
                            Tick = t,
                            Behavior = b.Current,
                            Tier = b.Tier,
                            Terminal = b.Terminal,
                            Session = b.Session,
                            Phase = s?.Phase ?? SeekingPhase.EnRoute,
                            CellX = s?.Pose.Cell.X ?? 0,
                            AccRaw = s?.Pose.Acc.Raw ?? 0,
                        });
                    }
                }

                return new ReconstructionRun(decisions);
            }

            /// <summary>重置(模拟读档)—— 复用同一批导演,清零派生态。
            /// <para>⚠️ **B1 修复**:原实现 `Run()` 每次重建导演,`Reset()` 作用于被丢弃的旧对象。
            /// 现 `Run()` 复用同一批导演,`Reset()` 调 `ResetForLoad()` 清零派生态。</para></summary>
            public void Reset()
            {
                EnsureInitialized();
                _behaviorDir.ResetForLoad();
                _spatialDir.ResetForLoad();
            }
        }

        /// <summary>会话体征查询 —— 按 predetermined 序列返回 VitalsDto。</summary>
        private sealed class SessionVitalsQuery : IVitalsQuery
        {
            private readonly Dictionary<int, float[]> _positions;
            private readonly Dictionary<int, (int mask, int count)> _signs;
            private int _currentTick;

            public SessionVitalsQuery(Dictionary<int, float[]> positions, Dictionary<int, (int, int)> signs)
            {
                _positions = positions;
                _signs = signs;
            }

            public void SetTick(int tick) => _currentTick = tick;

            public VitalsDto GetVitals(PatientId p)
            {
                float pos = 0.5f;
                if (_positions.TryGetValue(p.Value, out var arr) && arr.Length > 0)
                    pos = arr[Math.Min(_currentTick, arr.Length - 1)];

                int mask = 0, count = 0;
                if (_signs.TryGetValue(p.Value, out var s))
                {
                    mask = s.mask;
                    count = s.count;
                }

                return new VitalsDto(pos, 0f, mask, count);
            }
        }

        /// <summary>重建运行结果 —— 全部决策记录。</summary>
        private sealed class ReconstructionRun
        {
            public readonly List<DecisionRecord> Decisions;

            public ReconstructionRun(List<DecisionRecord> decisions)
            {
                Decisions = decisions;
            }

            public int FindTerminalLatch(int patientId)
            {
                for (int i = 0; i < Decisions.Count; i++)
                {
                    if (Decisions[i].PatientId == patientId && Decisions[i].Terminal.Latched)
                        return i;
                }
                return -1;
            }

            public int FindAtClinic(int patientId)
            {
                for (int i = 0; i < Decisions.Count; i++)
                {
                    if (Decisions[i].PatientId == patientId && Decisions[i].Phase == SeekingPhase.AtClinic)
                        return i;
                }
                return -1;
            }
        }

        /// <summary>单条决策记录 —— 可逐位比较。</summary>
        private struct DecisionRecord : IEquatable<DecisionRecord>
        {
            public int PatientId;
            public int Tick;
            public BehaviorState Behavior;
            public SymptomTier Tier;
            public TerminalFlag Terminal;
            public SessionState Session;
            public SeekingPhase Phase;
            public int CellX;
            public long AccRaw;

            public bool Equals(DecisionRecord other)
            {
                return PatientId == other.PatientId &&
                       Tick == other.Tick &&
                       Behavior == other.Behavior &&
                       Tier == other.Tier &&
                       Terminal.Latched == other.Terminal.Latched &&
                       Terminal.Cause == other.Terminal.Cause &&
                       Session == other.Session &&
                       Phase == other.Phase &&
                       CellX == other.CellX &&
                       AccRaw == other.AccRaw;
            }

            public override bool Equals(object obj) => obj is DecisionRecord other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(PatientId, Tick, Behavior, Tier, Session, Phase, CellX, AccRaw);

            public override string ToString()
            {
                return $"[P{PatientId}@{Tick}] {Behavior}/{Tier} T:{Terminal.Latched}/{Terminal.Cause} S:{Session} Ph:{Phase} Cell:{CellX} Acc:{AccRaw}";
            }
        }

        private static List<WorldPos> Path(int startX, int count)
        {
            var list = new List<WorldPos>(count);
            for (int i = 0; i < count; i++) list.Add(new WorldPos(startX + i, 0, 0));
            return list;
        }

        private sealed class FakePresence : IPresenceQuery
        {
            private readonly HashSet<int> _present = new HashSet<int>();
            public void SetPresent(PatientId id, bool present)
            {
                if (present) _present.Add(id.Value);
                else _present.Remove(id.Value);
            }
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

            public FakeClinicSource(WorldPos anchor, EcozoneId region, bool hasPoi, bool hasBuilt)
            { _anchor = anchor; _region = region; _hasPoi = hasPoi; _hasBuilt = hasBuilt; }

            public EcozoneId EcozoneOf(WorldPos cell) => cell == _anchor ? _region : EcozoneId.None;
            public bool HasClinicPoiIn(EcozoneId region) => _hasPoi && region == _region;
            public bool HasPlayerBuiltClinicIn(EcozoneId region) => _hasBuilt && region == _region;
        }
    }
}
