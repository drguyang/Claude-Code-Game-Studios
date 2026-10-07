// patient-ai(13)Story 001 —— 体征只读消费与行为映射(VitalsDto 取数、双维状态与滞回)
//
// 登记落点: tests/unit/patient-ai/behavior_map_test.cs
// 真身落点: unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs
//
// 权威来源:
//   GDD design/gdd/patient-ai.md —— §Core Rules 一/三/三-bis/十二 · §States 一/一-bis/一-ter/二
//     · §Formulas F-13.1 / F-13.5 / F-13.6 / F-13.8 · §Tuning 一/三 · AC-13-A1/A2/A3/A5/B1/B5/C5/D4/D5/D6
//   ADR-016 §一 补注(13 决策住边界层)· ADR-027(13 零写)· ADR-018 §一(44 只触发)
//
// ⚠️ **反空转三件**(承 interaction story-001 的纪律):
//   ① 负夹具必须**与正测共用同一台扫描机器**(否则「夹具红」不蕴含「真断言红」);
//   ② 每条结构断言配一个**影子类型注入 ⇒ 必红且点名**的负夹具;
//   ③ 断言须触底到**具体产物 / 字段**(非「只要抛了就算」的空转形态)。
//
// ⚠️ **本故事的承重面 = 「13 的决策输入恰 (position, trend, prev)」**:
//   AC-13-A5 要求 `signs[]` **永不进 `Map()`** —— 判据 = **反射断言**(不是 grep):
//   `BehaviorInput` 的字段集恰 ⊆ {Position, Trend, Prev},`Map` 的方法签名不含 signs。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;   // OpCodes(IL 写入点扫描 —— F-3/M1 修复)
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Gameplay.Presentation.Audio;   // PatientCueEmit 住 44 前缀(b5①)
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PatientAI
{
    public class BehaviorMapTest
    {
        private static readonly BehaviorBands Bands = BehaviorBands.Default;
        private static readonly CueIntervals Intervals = CueIntervals.Default;

        // ═══════════════════════════════════════════════════════════
        //  AC-13-B1 —— `Map(position, trend, prev)` 输出唯一确定(真值表 + 滞回成对输入)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13b1_enterThresholds_mapToExpectedStates()
        {
            // Arrange / Act / Assert:三个进入阈值,各自恰好落进对应态(边界值 = 阈值本身)
            Assert.AreEqual(BehaviorState.Idle,
                Map(Bands.SeekMin - 0.01f, BehaviorState.Idle, knowsClinic: true),
                "position < SEEK_MIN ⇒ Idle");
            Assert.AreEqual(BehaviorState.Seeking,
                Map(Bands.SeekMin, BehaviorState.Idle, knowsClinic: true),
                "position 恰 = SEEK_MIN ⇒ Seeking(≥ 是进入条件)");
            Assert.AreEqual(BehaviorState.Bedridden,
                Map(Bands.CollapseMin, BehaviorState.Idle, knowsClinic: true),
                "position 恰 = COLLAPSE_MIN ⇒ Bedridden");
        }

        [Test]
        public void test_ac13b1_knowsClinicGate_blocksSeekingWithoutKnowledge()
        {
            // 知识判据是 Seeking 的**必要**条件(F-13.1 两处分支都带 KnowsClinic)
            Assert.AreEqual(BehaviorState.Idle,
                Map(0.60f, BehaviorState.Idle, knowsClinic: false),
                "position ≥ SEEK_MIN 但不知道医馆 ⇒ Idle(不盲目前往)");
        }

        [Test]
        public void test_ac13b1_hysteresisUpStep_requiresCrossingBelowBandMinusHyst()
        {
            // ⚠️ 本测试 = AC-13-B1「滞回带内/带外**成对**输入」的承重半边。
            // Bedridden 出档须回落到 COLLAPSE_MIN − HYST 以下 —— 带内不动,带外才动。
            float insideBand = Bands.CollapseMin - 0.01f;             // 带内(> COLLAPSE_MIN − HYST)
            float outsideBand = Bands.CollapseMin - Bands.Hyst - 0.01f; // 带外

            Assert.AreEqual(BehaviorState.Bedridden,
                Map(insideBand, BehaviorState.Bedridden, knowsClinic: true),
                "带内:已 Bedridden 且 position 仍 ≥ COLLAPSE_MIN − HYST ⇒ 保持 Bedridden(不急着起身)");
            Assert.AreNotEqual(BehaviorState.Bedridden,
                Map(outsideBand, BehaviorState.Bedridden, knowsClinic: true),
                "带外:回落超 HYST ⇒ 出档(滞回是转出阈值,不是进入阈值)");
        }

        [Test]
        public void test_ac13b1_hysteresisDownStep_keepsSeekingInsideBand()
        {
            float insideBand = Bands.SeekMin - 0.01f;
            float outsideBand = Bands.SeekMin - Bands.Hyst - 0.01f;

            Assert.AreEqual(BehaviorState.Seeking,
                Map(insideBand, BehaviorState.Seeking, knowsClinic: true),
                "带内:已 Seeking 且 position ≥ SEEK_MIN − HYST ⇒ 保持 Seeking(不急着放弃)");
            Assert.AreEqual(BehaviorState.Idle,
                Map(outsideBand, BehaviorState.Seeking, knowsClinic: true),
                "带外:回落超 HYST ⇒ 回 Idle");
        }

        [Test]
        public void test_ac13b1_deterministic_sameInputsSameOutput()
        {
            // 唯一确定性:同 (position, trend, prev) 跑两遍必须同输出
            foreach (float p in new[] { 0f, 0.19f, 0.20f, 0.44f, 0.45f, 0.74f, 0.75f, 0.99f, 1f })
            foreach (var prev in new[] { BehaviorState.Idle, BehaviorState.Seeking, BehaviorState.Bedridden })
            {
                var a = BehaviorMap.Map(new BehaviorInput(p, 0.1f, prev), Bands, true);
                var b = BehaviorMap.Map(new BehaviorInput(p, 0.1f, prev), Bands, true);
                Assert.AreEqual(a, b, $"position={p} prev={prev} 两次调用须同输出");
            }
        }

        [Test]
        public void test_ac13b1_noJitter_acrossDriftingSequence()
        {
            // ⚠️ 滞回的**行为**判据(TC-1):position 单调恶化下的状态序列不得来回翻转。
            var seq = new[] { 0.30f, 0.55f, 0.78f, 0.85f, 0.70f, 0.68f, 0.66f };
            var states = new List<BehaviorState>();
            var prev = BehaviorState.Idle;
            foreach (float p in seq) states.Add(prev = BehaviorMap.Map(new BehaviorInput(p, -0.01f, prev), Bands, true));

            // 回落后(0.85 → 0.70 → 0.68 → 0.66)不得一次翻回 Seeking(0.70 在 0.75−0.10=0.65 之上)
            Assert.AreEqual(BehaviorState.Bedridden, states[^1],
                "带内回落不得翻档(滞回生效;hard-cut 会在此翻回 Seeking = 门口反复转身)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-B5 —— `SessionState` 三判据(唯一入口 / 幂等 / 加载后重置)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13b5_examSession_isIdempotent()
        {
            var b = new PatientBehavior();
            b.ApplyExamSession(true);
            Assert.AreEqual(SessionState.InTreatment, b.Session, "active=true ⇒ InTreatment");

            b.ApplyExamSession(true);   // 重复同值
            Assert.AreEqual(SessionState.InTreatment, b.Session, "重复 true 须幂等(不叠加)");

            b.ApplyExamSession(false);
            Assert.AreEqual(SessionState.None, b.Session, "active=false ⇒ None");
            b.ApplyExamSession(false);
            Assert.AreEqual(SessionState.None, b.Session, "重复 false 须幂等");
        }

        [Test]
        public void test_ac13b5_resetForLoad_clearsToNone()
        {
            var b = new PatientBehavior();
            b.ApplyExamSession(true);
            b.ResetForLoad();
            Assert.AreEqual(SessionState.None, b.Session, "加载后须重置 None(AC-13-B5 ③ / EC-13-05)");
        }

        [Test]
        public void test_ac13b5_resetForLoad_negativeFixture()
        {
            // ⚠️ **m2 修复(2026-10-04 评审)**:原 `resetForLoad_clearsToNone` **无负夹具** ——
            //    若断言方向写反(如等价地断言 `InTreatment`),它照样「绿」,无人发现。
            //    本夹具证**判据方向**:置 InTreatment 后**不**调 `ResetForLoad` ⇒ 仍 InTreatment;
            //    调了 ⇒ None。两侧对照,判据非恒真。
            var b = new PatientBehavior();
            b.ApplyExamSession(true);
            Assert.AreEqual(SessionState.InTreatment, b.Session,
                "夹具备置阶段:未重置前须仍是 InTreatment(否则上方断言可能恒真)");
            b.ResetForLoad();
            Assert.AreEqual(SessionState.None, b.Session,
                "重置后须翻 None —— 与未重置态对照,证判据方向承重");
        }

        [Test]
        public void test_ac13b5_sessionWriterIsUnique_reflection()
        {
            // ⚠️ **F-3 / M1 修复(2026-10-04 评审)**:原断言名为「写入者唯一」,
            //    实只断言 `Session` 的 setter 私有 —— 只证**访问级**,不证**写入点计数**。
            //    类内新增第三处 `Session = ...`(如自行推断会诊态)时原断言**仍绿**。
            //    现改为 **IL 层写入点扫描**:扫 `PatientBehavior` 闭包内**每个方法体**,
            //    找出对 `Session` 后备字段(`<Session>k__BackingField`)做 `stfld` 的方法,
            //    断言写入点集 ⊆ {`ApplyExamSession`, `ResetForLoad`}(唯二合法写者)。
            var writers = ScanSessionWriters(typeof(PatientBehavior));
            // ⚠️ `set_Session`(私有 setter)**本身就是**后备字段的写者 —— 它是**机械写手**,
            //    两条合法语义写者(`ApplyExamSession` / `ResetForLoad`)都**经它**。
            //    判据 = 语义写者集恰 = {Apply, Reset};`set_Session` 是必经的机械通道,单列。
            CollectionAssert.AreEquivalent(
                new[] { "ApplyExamSession", "ResetForLoad", "set_Session" }, writers,
                "AC-13-B5 ①:`Session` 的写入者须恰 = {ApplyExamSession, ResetForLoad}(+机械 setter)—— " +
                "新增第三语义写点(自行推断会诊态)= 第四来源(规则六)。实测写入点:\n" +
                string.Join("\n", writers));
        }

        [Test]
        public void test_ac13b5_sessionWriterScan_catchesThirdWriter_negativeFixture()
        {
            // ⚠️ 负夹具(**同一台机器**):影子类型**在第三个方法里也写 `Session` 后备字段**
            //    ⇒ 扫描器**必须**抓到第三个写点。若无此夹具,扫描器写错(如 token 解析失败静默 false)
            //    也同样「绿」—— 空转。
            var writers = ScanSessionWriters(typeof(ShadowSessionWriter));
            Assert.IsTrue(writers.Contains("ApplyExamSession") && writers.Contains("SmuggledWriter"),
                $"AC-13-B5 ① 负夹具失败:影子类型的第三写点未被抓到(空转)。实测: [{string.Join(",", writers)}]");
            Assert.IsFalse(writers.Contains("ResetForLoad"),
                "影子刻意不给 ResetForLoad 写点 ⇒ 扫描器须如实不报它(机器正确、根不同)");
        }

        /// <summary>`Session` 后备字段的 IL 写入点扫描(F-3/M1 修复)。
        /// <para>⚠️ **两条写径都计入**(实测教训,2026-10-04):
        /// ① **直接写** —— `stfld` / `ldflda` 命中后备字段 `<Session>k__BackingField`(机械 setter 走此径);
        /// ② **经 setter 写** —— `call`/`callvirt` 命中 `set_Session`(语义写者 `ApplyExamSession` /
        /// `ResetForLoad` 走此径 —— C# 编译器把属性赋值编成对 setter 的 `call`,**不**直接 `stfld`)。
        /// 原版只认 ①,故生产侧只扫出 `set_Session`,漏掉两个语义写者(首轮实测坐实)。</para>
        /// <para>⚠️ **与正测同一台机器** —— 传入不同的 <paramref name="type"/> 即换根,
        /// 机制不变(承 `boundary_discipline_test.cs:502-505` 纪律)。</para>
        /// <returns>写 `Session` 的方法名列表(去重排序)。</returns></summary>
        private static List<string> ScanSessionWriters(Type type)
        {
            var writers = new List<string>();
            // 自动属性 `Session { get; private set; }` 的后备字段名(C# 编译器约定)
            const string backingField = "<Session>k__BackingField";
            const BindingFlags fb = BindingFlags.Public | BindingFlags.NonPublic |
                                    BindingFlags.Instance | BindingFlags.Static |
                                    BindingFlags.DeclaredOnly;
            foreach (var m in type.GetMethods(fb))
            {
                MethodBody body;
                try { body = m.GetMethodBody(); } catch { continue; }
                if (body == null) continue;
                byte[] il;
                try { il = body.GetILAsByteArray(); } catch { continue; }
                if (il == null || il.Length == 0) continue;

                for (int i = 0; i < il.Length; i++)
                {
                    ushort op = il[i];
                    if (op == 0xFE) { i++; if (i >= il.Length) break; op = (ushort)(0xFE00 | il[i]); }

                    bool isFieldOp = op == (ushort)OpCodes.Stfld.Value || op == (ushort)OpCodes.Ldflda.Value;
                    bool isCallOp  = op == (ushort)OpCodes.Call.Value   || op == (ushort)OpCodes.Callvirt.Value;
                    if (!isFieldOp && !isCallOp) continue;
                    if (i + 4 >= il.Length) break;
                    int token = BitConverter.ToInt32(il, i + 1);
                    i += 4;

                    string member;
                    try
                    {
                        member = isFieldOp
                            ? (m.Module.ResolveField(token)?.Name ?? string.Empty)
                            : (m.Module.ResolveMethod(token)?.Name ?? string.Empty);
                    }
                    catch { continue; }

                    // ① 直接写后备字段;② 经 `set_Session` 写
                    if (member == backingField || member == "set_Session") { writers.Add(m.Name); break; }
                }
            }
            writers.Sort();
            return writers;
        }

        [Test]
        public void test_ac13b5_sessionOverridesBehaviorStateInView()
        {
            // 会诊态**覆盖** Map():躺床上接受查体的病人报 InTreatment(不是 Collapsed)
            Assert.AreEqual(PresentPatientState.InTreatment,
                ViewStateMap.ViewState(BehaviorState.Bedridden, SessionState.InTreatment, SeekingPhase.EnRoute),
                "会诊优先级高于 Collapsed(GDD §States 二 优先级顺序)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C5 —— `ViewState()` 与 §States 二 枚举表逐字一致(真值表叉乘)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13c5_viewStateCrossProduct_matchesPriorityTable()
        {
            // 真值表叉乘 (BehaviorState × SessionState × SeekingPhase) 的全部合法组合,
            // 逐条对照 GDD §States 二 的优先级顺序:InTreatment > Collapsed > AwaitingCare > Present
            foreach (BehaviorState beh in new[] { BehaviorState.Idle, BehaviorState.Seeking, BehaviorState.Bedridden })
            foreach (SessionState ses in new[] { SessionState.None, SessionState.InTreatment })
            foreach (SeekingPhase ph in new[] { SeekingPhase.EnRoute, SeekingPhase.AtClinic })
            {
                var expected =
                    ses == SessionState.InTreatment                ? PresentPatientState.InTreatment :
                    beh == BehaviorState.Bedridden                 ? PresentPatientState.Collapsed :
                    (beh == BehaviorState.Seeking && ph == SeekingPhase.AtClinic) ? PresentPatientState.AwaitingCare :
                                                                     PresentPatientState.Present;
                Assert.AreEqual(expected, ViewStateMap.ViewState(beh, ses, ph),
                    $"叉乘组合 ({beh}, {ses}, {ph}) 须匹配 §States 二 优先级表");
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Terminal 闩锁 —— 进入终态后 `Map` 不再改出(单调)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13b1_nanPosition_degradesToIdleNotCrash()
        {
            // ⚠️ 病态输入:position = NaN(9 侧缺陷,非 13 该修的)。
            //    NaN 与任何阈值比较皆 false ⇒ 全链落到默认分支 = Idle / Recover。
            //    **这是刻意的 fail-safe**(不崩、不误报重病),但**必须被锁住** ——
            //    否则日后有人把比较改成 `!(p < CollapseMin)` 形态,NaN 会突然变成 Bedridden。
            float nan = float.NaN;
            Assert.AreEqual(BehaviorState.Idle,
                BehaviorMap.Map(new BehaviorInput(nan, 0f, BehaviorState.Idle), Bands, true),
                "NaN position ⇒ Idle(NaN 与阈值比较皆 false)");
            Assert.AreEqual(SymptomTier.Recover, BehaviorMap.Tier(nan, 0f, Bands),
                "NaN position ⇒ Recover 档");
            Assert.IsFalse(BehaviorMap.EvaluateTerminal(nan, 0f, Bands).Latched,
                "NaN 不得误置终态(既不痊愈也不死亡 —— 畸形输入不得产出终局判定)");
        }

        [Test]
        public void test_ac13d4_nanIntensity_degradesToZeroWithinByteRange()
        {
            // ClampByte(NaN):Math.Round(NaN)=NaN,两个越界判定皆 false ⇒ (byte)NaN = 0。
            // 须**仍落在 [0,255]**(不得静默回绕到任意值)。
            byte i = PatientCue.Intensity(float.NaN);
            Assert.That((int)i, Is.InRange(0, 255), $"Intensity(NaN) 须 ∈ [0,255],实为 {i}");
            // 正/负无穷:必须夹到端点(不是未定义)
            Assert.AreEqual(255, PatientCue.Intensity(float.PositiveInfinity), "+∞ ⇒ 255");
            Assert.AreEqual(0, PatientCue.Intensity(float.NegativeInfinity), "-∞ ⇒ 0");
        }

        [Test]
        public void test_ac13b1_terminalLatch_recoversDoNotResurrect()
        {
            // 痊愈锁存:position ≤ RECOVER_MAX ∧ trend ≤ 0 ⇒ 置位;之后 position 回升也不复活
            var b = new PatientBehavior();
            b.Step(Bands.RecoverMax - 0.01f, -0.5f, Bands, knowsClinic: true);
            Assert.IsTrue(b.Terminal.Latched, "痊愈线以下 + trend ≤ 0 ⇒ Terminal 置位");
            Assert.AreEqual(TerminalCause.Recovered, b.Terminal.Cause, "成因须为 Recovered");

            var frozen = b.Current;
            b.Step(0.80f, +0.9f, Bands, knowsClinic: true);   // 回升假输入
            Assert.AreEqual(frozen, b.Current, "终态后 position 回升不得改出(闩锁,单调)");
        }

        [Test]
        public void test_ac13b1_terminalLatch_deathIsMonotonic()
        {
            var b = new PatientBehavior();
            b.Step(Bands.DeathBandMin + 0.01f, +0.5f, Bands, knowsClinic: true);
            Assert.IsTrue(b.Terminal.Latched, "死亡带 ⇒ Terminal 置位");
            Assert.AreEqual(TerminalCause.Deceased, b.Terminal.Cause, "成因须为 Deceased");

            var frozen = b.Current;
            b.Step(0.30f, -0.9f, Bands, knowsClinic: true);
            Assert.AreEqual(frozen, b.Current, "死亡后 position 回落不得改出");
        }

        [Test]
        public void test_ac13b1_middleBand_doesNotLatch()
        {
            // RECOVER_MAX 与 DEATH_BAND_MIN 之间是活着的病程 —— 中间永不复位
            var b = new PatientBehavior();
            b.Step(0.50f, -0.5f, Bands, knowsClinic: true);
            Assert.IsFalse(b.Terminal.Latched, "中间带(0.50)不得置位终态");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-A5 —— `signs[]` 消费面白名单(**反射断言**,不是 grep)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13a5_behaviorInputFields_areExactlyPositionTrendPrev()
        {
            // ⚠️ 本断言是 13 的承重面:`Map()` 的输入集**恰 ⊆ {position, trend, prev}**
            var fields = typeof(BehaviorInput).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                              .Select(f => f.Name).OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(new[] { "Position", "Prev", "Trend" }, fields,
                "AC-13-A5:BehaviorInput 字段集须恰 = {Position, Trend, Prev} —— " +
                "任何新增字段(尤其 signs)都会让 signs[] 悄悄进决策");
        }

        [Test]
        public void test_ac13a5_trendIsInertInMap_branching()
        {
            // ⚠️ 本断言是 AC-13-A5「Map 的输入恰 (position, trend, prev) 且 trend **不参与分支**」
            //    的**行为判据** —— 前两条只锁**类型形状**(字段集 / 签名),锁不住「有人日后把
            //    trend 拿去改分支」这一步。此处扫遍 (position × prev × knowsClinic),对每个组合
            //    喂 trend 的极值(负向 / 零 / 正向),输出必须**逐位相同**。
            //    (MUT 证明:在 `Map` 里给 Seeking 分支加 `&& input.Trend >= -1f` ⇒ 本断言红。)
            foreach (float p in new[] { 0.05f, 0.19f, 0.20f, 0.30f, 0.44f, 0.45f, 0.60f,
                                       0.65f, 0.74f, 0.75f, 0.85f, 0.99f })
            foreach (var prev in new[] { BehaviorState.Idle, BehaviorState.Seeking, BehaviorState.Bedridden })
            foreach (bool knows in new[] { true, false })
            {
                var baseOut = BehaviorMap.Map(new BehaviorInput(p, 0f, prev), Bands, knows);
                // ⚠️ **m1 修复(2026-10-04 评审)**:原扫描只喂**极端标量**(±1/±10),
                //    漏掉「只在**带符号邻域**内触发的分支」(如 `Trend > 0 && Trend < 0.01`)。
                //    现补**带符号邻域球**:极小正/负(±1e-6/±1e-3/±0.01)+ ±0.5 + 极端值,
                //    两侧对称扫 —— 任何「只在某侧小区间为真」的 trend 分支都会被本断言抓到。
                foreach (float trend in new[]
                         {
                             1e-6f, -1e-6f, 1e-3f, -1e-3f, 0.01f, -0.01f, 0.5f, -0.5f,
                             1f, -1f, 10f, -10f,
                             float.Epsilon, -float.Epsilon,   // 最小正/负正规值边界
                         })
                    Assert.AreEqual(baseOut,
                        BehaviorMap.Map(new BehaviorInput(p, trend, prev), Bands, knows),
                        $"AC-13-A5:trend={trend} 改变了 Map 输出(position={p} prev={prev} knows={knows})—— " +
                        "trend 只进签名,不得进分支");
            }
        }

        [Test]
        public void test_ac13a5_signsNeverInMapSignature()
        {
            // Map / Tier / EvaluateTerminal 的签名不得出现 sign / Sign 词面
            foreach (var m in new[]
                     {
                         typeof(BehaviorMap).GetMethod("Map"),
                         typeof(BehaviorMap).GetMethod("Tier"),
                         typeof(BehaviorMap).GetMethod("EvaluateTerminal"),
                     })
            {
                Assert.IsNotNull(m, "方法须存在");
                var sig = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))
                          + "->" + m.ReturnType.Name;
                Assert.IsFalse(sig.Contains("Sign") || sig.Contains("sign"),
                    $"AC-13-A5:{m.Name} 的签名不得含 signs({sig})");
            }
        }

        [Test]
        public void test_ac13a5_signsDoNotChangeBehaviorOutput()
        {
            // ⚠️ 行为判据(TC-3):喂不同的 signs[] 通道位,`Map` 的输出必须**完全相同**。
            //    ⚠️ 本测试的**反空转**点:它不是「构造两个相同输入」的空转,而是**经
            //    PatientBehaviorDirector.Step 走真路径**(它读 VitalsDto),证明 DTO 的
            //    signs 字段确实被忽略。
            var a = new RecordingVitalsQuery(new VitalsDto(0.60f, 0.1f, signChannelMask: 0, signCount: 0));
            var b = new RecordingVitalsQuery(new VitalsDto(0.60f, 0.1f, signChannelMask: 0xFF, signCount: 9));
            var da = new PatientBehaviorDirector(a, Bands);
            var db = new PatientBehaviorDirector(b, Bands);

            var id = new PatientId(7);
            da.Step(id, knowsClinic: true);
            db.Step(id, knowsClinic: true);

            Assert.AreEqual(da.For(id).Current, db.For(id).Current,
                "AC-13-A5:signs[] 不同不得改变行为输出(signs 只喂材质)");
        }

        // ── S7 删除(2026-10-07 边界评估轮):原 `test_ac13a5_signsConsumedOnlyByMaterial` ──
        // 守的 `PatientBehaviorDirector.Material` + `PatientMaterial` 已随孤儿半迁移件删除
        // (零生产调用方)。AC-13-A5「signs 唯一消费点」由 presentation_projection_test 的
        // `MaterialTable.Material` 断言承载;「signs 不改行为」由上方
        // `test_ac13a5_signsDoNotChangeBehaviorOutput` 承载 —— 语义不减弱。

        [Test]
        public void test_ac13a5_reflectionScanner_catchesSignsLeak_negativeFixture()
        {
            // ⚠️ 负夹具:影子类型把 signs 塞进决策输入 ⇒ 白名单扫描器**必须**抓到且点名。
            //    若无此夹具,`test_ac13a5_behaviorInputFields_areExactlyPositionTrendPrev`
            //    的登记表写错了也无人发现(空转)。
            var violations = ScanFieldsForToken(
                typeof(ShadowBehaviorInputWithSigns),
                new[] { "Sign", "sign" });
            Assert.IsNotEmpty(violations,
                "AC-13-A5 负夹具失败:注入带 signs 的决策输入后扫描器仍绿 = 空转(假绿)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-A1 / A2 / A3 —— 零写 / 唯一取数入口 / 不重建九态
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13a1_zeroEventSinkInTypeGraph()
        {
            var violations = ScanClosureForNames(new[] { "IEventSink", "IEventAuthority", "SimEvent" });
            Assert.IsEmpty(violations,
                "AC-13-A1:13 的类型图中不得有写通道类型(零三流写入):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13a1_scannerCatchesSinkLeak_negativeFixture()
        {
            var violations = ScanClosureForNames(
                new[] { "IEventSink" }, new[] { typeof(ShadowPatientWithSink) });
            Assert.IsNotEmpty(violations, "AC-13-A1 负夹具失败:注入 IEventSink 字段后未红 = 空转");
            Assert.IsTrue(string.Join("\n", violations).Contains("IEventSink"), "违例须点名 IEventSink");
        }

        [Test]
        public void test_ac13a1_scanRootCoversAudioBridgeNamespace_b1()
        {
            // ⚠️ **B1 修复(2026-10-04 评审)**:原生产扫描根只含 `...PatientAI` 前缀,
            //    而唯一 44 桥 `PatientCueEmit` 住 `...Presentation.Audio` ⇒ 桥在面外 ⇒
            //    `test_ac13a1_zeroEventSinkInTypeGraph` **阴性恒真**(新增写三流代码在桥内也永不红)。
            //    本断言**锁根枚举** —— 桥类型**必须**在 `ProductionScanSeeds()` 内,
            //    否则「全类型零写」纸面化(删根枚举项 ⇒ 本断言红)。
            var seeds = ProductionScanSeeds().ToList();
            var bridge = seeds.FirstOrDefault(t => t.Name == "PatientCueEmit");
            Assert.IsNotNull(bridge,
                "AC-13-A1 扫描根须含 44 发射桥 `PatientCueEmit`(否则桥内写三流代码不被抓 = 阴性恒真)");
            Assert.AreEqual("DaYiJingCheng.Gameplay.Presentation.Audio", bridge.Namespace,
                "桥须住 44 前缀命名空间(b5① 逃逸谓词),且该命名空间须被根枚举覆盖");
        }

        [Test]
        public void test_ac13a2_vitalsOnlyViaQuery_directorHoldsIVitalsQuery()
        {
            // 取数唯一入口 = `IVitalsQuery`(AC-13-A2)。`PatientBehaviorDirector` 的构造
            // 参数集恰 = {IVitalsQuery, BehaviorBands} —— 无第二取数来源、无门面绕行。
            var ctor = typeof(PatientBehaviorDirector).GetConstructors().Single();
            // ⚠️ `in BehaviorBands` 的参数类型是 `BehaviorBands&`(by-ref)—— 比对前须剥掉 `&`/`@` 记号
            var names = ctor.GetParameters()
                            .Select(p => p.ParameterType.Name.TrimEnd('&'))
                            .OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(new[] { "BehaviorBands", "IVitalsQuery" }, names,
                "AC-13-A2:取数唯一入口 = IVitalsQuery;构造参数集恰 = {BehaviorBands, IVitalsQuery}");
        }

        [Test]
        public void test_ac13a2_vitalsProducedOnlyByIVitalsQuery_sourceClosure()
        {
            // ⚠️ **B2 修复(2026-10-04 评审)**:上方只证「构造收了 IVitalsQuery」——
            //    不证「`VitalsDto` 的**全部来源**恰 = `IVitalsQuery.GetVitals()` 一处」。
            //    本断言扫 13 侧**全部公开面**:任何**产出 `VitalsDto` 的公开方法/属性**
            //    必须**属于 `IVitalsQuery` 接口**(唯一合法来源)——
            //    若新增第二取数点(如 `Director.GetVitalsRaw()` 或门面旁路),本断言红。
            var producible = new List<string>();
            foreach (var t in ProductionScanSeeds())
            {
                if (!ShouldExpandMembers(t)) continue;
                const BindingFlags fb = BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.Static |
                                        BindingFlags.DeclaredOnly;
                foreach (var m in t.GetMethods(fb))
                {
                    if (m.ReturnType == typeof(VitalsDto))
                        producible.Add(t.FullName + "." + m.Name);
                }
                foreach (var p in t.GetProperties(fb))
                {
                    if (p.PropertyType == typeof(VitalsDto))
                        producible.Add(t.FullName + "." + p.Name);
                }
            }
            // 13 侧本身**零**产出 `VitalsDto` —— 它只**消费**;唯一声明产出者 = `IVitalsQuery`
            // (接口住 `Sim.Contracts`,不在 13 程序集内,故不进 ScanSeeds)。
            Assert.IsEmpty(producible,
                "AC-13-A2:`VitalsDto` 只能经 `IVitalsQuery.GetVitals()` 取得 —— " +
                "13 侧任何类型自力产出 `VitalsDto` 即第二取数来源(违唯一入口):\n" +
                string.Join("\n", producible));
        }

        [Test]
        public void test_ac13a2_sourceClosure_catchesSecondSource_negativeFixture()
        {
            // ⚠️ 负夹具(**同一台机器**,换根而已):影子类型自力产出 `VitalsDto`
            //    ⇒ 来源闭包扫描器**必须**抓到。若无此夹具,上方扫描器写错(如取错 BindingFlags)
            //    也同样「绿」—— 空转。
            var producible = new List<string>();
            foreach (var m in typeof(ShadowVitalsSource).GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                         BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m.ReturnType == typeof(VitalsDto)) producible.Add(m.Name);
            }
            Assert.IsNotEmpty(producible,
                "AC-13-A2 负夹具失败:注入自力产出 `VitalsDto` 的影子类型后扫描器仍绿 = 空转(假绿)");
        }

        [Test]
        public void test_ac13a2_stepActuallyReadsVitals()
        {
            // ⚠️ 反空转:证明 `Step` **真读** `IVitalsQuery`(否则上方「持了接口」是纸面判据)
            var spy = new RecordingVitalsQuery(new VitalsDto(0.60f, 0f, 0, 0));
            var d = new PatientBehaviorDirector(spy, Bands);
            d.Step(new PatientId(1), knowsClinic: true);
            Assert.AreEqual(1, spy.CallCount, "Step 须真调一次 GetVitals(取数唯一入口承重)");
        }

        [Test]
        public void test_ac13a3_noNineStateEnum_noDiseaseId()
        {
            // 不重建九态:类型图中不得出现 InjuryState / CRITICAL / disease_id
            var violations = ScanClosureForNames(
                new[] { "InjuryState", "disease_id", "DiseaseId", "diseaseId", "ThresholdTransition" });
            Assert.IsEmpty(violations,
                "AC-13-A3:13 不得重建九态 / 引 disease_id:\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13a3_bandsAreTheOwnTable()
        {
            // 行为分档只用 `BEHAVIOR_BAND_*` 自有表 —— 硬约束自洽(GDD §Tuning 一)
            Assert.IsEmpty(BehaviorBands.Validate(Bands),
                "默认样例须满足硬约束(MILD_MIN < SEEK_MIN < COLLAPSE_MIN < DEATH_BAND_MIN < 1)");
            // 负例:破约束的表必须被 `Validate` 抓到
            var broken = new BehaviorBands(0.5f, 0.4f, 0.75f, 0.1f, 0.1f, 0.9f);
            Assert.IsNotEmpty(BehaviorBands.Validate(broken),
                "MILD_MIN > SEEK_MIN 破硬约束 ⇒ Validate 须报错(否则区间校验空转)");
        }

        [Test]
        public void test_ac13a3_validateChecksSeekBelowDeathBand_m3()
        {
            // ⚠️ **m3 修复(2026-10-04 评审)**:GDD §Tuning 一 明写 `SEEK_MIN < DEATH_BAND_MIN`
            //    为独立约束。构造 `SEEK_MIN ≥ DEATH_BAND_MIN` 的破表,`Validate` 须**点名**该约束。
            //    ⚠️ 该破表其余链仍自洽(MILD<SEEK=0.95, SEEK=0.95>COLLAPSE ⇒ 亦命中另一条),
            //    故本断言只要求**至少一条点名 SEEK/DEATH**,不要求「仅此一条」。
            var broken = new BehaviorBands(0.20f, 0.95f, 0.75f, 0.10f, 0.10f, 0.90f);
            var errs = BehaviorBands.Validate(broken);
            Assert.IsTrue(errs.Any(e => e.Contains("SEEK_MIN") && e.Contains("DEATH_BAND_MIN")),
                "SEEK_MIN ≥ DEATH_BAND_MIN 须被独立点名(GDD §Tuning 一)。实测:\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_ac13a3_directorConstructorRejectsBrokenBands_f2()
        {
            // ⚠️ **F-2 修复(2026-10-04 评审)**:`Validate` 此前**只有测试调用者** ——
            //    破表经构造进生产会**静默生效**。现构造期硬校验:破表 ⇒ `ArgumentException`。
            //    本断言是「生产调用者存在」的可证伪判据(删构造内 `Validate` 调用 ⇒ 本断言红)。
            var broken = new BehaviorBands(0.5f, 0.4f, 0.75f, 0.1f, 0.1f, 0.9f);   // MILD_MIN > SEEK_MIN
            Assert.Throws<ArgumentException>(
                () => new PatientBehaviorDirector(new RecordingVitalsQuery(default), broken),
                "破约束分档表进构造须硬失败(否则生产表可破而无人知 —— F-2)");

            // 反空转:合法表**不得**被拒(否则上条「凡输入皆抛」也能绿)
            Assert.DoesNotThrow(
                () => new PatientBehaviorDirector(new RecordingVitalsQuery(default), Bands),
                "合法分档表不得被拒(反空转 —— 证上条非「恒抛」)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-D4 / D5 / D6 —— 夹取 / 零 PRNG / 按档不按值
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13d4_intensityClampsWithinByteRange()
        {
            // ⚠️ AC-13-D4:任意 position(**含越界极端值**)⇒ Intensity 恒 ∈ [0,255]
            foreach (float p in new[] { -10f, -0.5f, 0f, 0.5f, 1f, 1.5f, 10f })
            {
                byte i = PatientCue.Intensity(p);
                Assert.That((int)i, Is.InRange(0, 255), $"position={p} ⇒ Intensity 须 ∈ [0,255]");
            }
        }

        [Test]
        public void test_ac13d4_outOfRangeDoesNotSilentlyWrap()
        {
            // ⚠️ 承重:原稿 `round(1.5×255)=383` 塞进 byte ⇒ 静默回绕 127(重病看起来像轻病)。
            //    夹取后必须是 255,**不是** 127。
            Assert.AreEqual(255, PatientCue.Intensity(1.5f),
                "越界 1.5 须夹到 255 —— 若得 127 = 静默回绕(AC-13-D4 的失败形态)");
            Assert.AreEqual(0, PatientCue.Intensity(-1.0f), "越界负值须夹到 0");
        }

        [Test]
        public void test_ac13d4_emitSeam_forwardsClampedIntensityTo44()
        {
            // ⚠️ 承重:夹取须**在真发射路径上**生效(否则 `Intensity` 单测绿、`Emit` 仍回绕 = 空转)。
            var sink = new RecordingCueSink();
            PatientCueEmit.Emit(sink, patientId: 3, kind: (int)CueKind.Cough,
                                position: 1.5f, cellX: 2, cellY: 0, cellZ: 5);
            Assert.AreEqual(1, sink.Calls.Count, "Emit 须真转发一次 AudioCueDto");
            var cue = sink.Calls[0];
            Assert.AreEqual(255, cue.Intensity, "越界 position=1.5 ⇒ 转发强度须夹到 255(非回绕 127)");
            Assert.AreEqual((int)CueKind.Cough, cue.Cue, "语义种类透传");
            Assert.AreEqual(3, cue.Source, "source = PatientId.Value");
            Assert.AreEqual(2, cue.Cell.X, "格 X 透传"); Assert.AreEqual(5, cue.Cell.Z, "格 Z 透传");
            Assert.IsFalse(cue.Looped, "一次性 cue 须 Looped=false(AC-13-D2/D3)");
        }

        [Test]
        public void test_ac13d2_emitNeverSignalsStateChange()
        {
            // ⚠️ AC-13-D2:13 的 cue 通道**只有 Emit** —— 无 BeginLoop / EndLoop / SetTier 调用点。
            //    死亡 = 呼吸层停止(AC-13-D3),不是一次性播报。
            var sink = new RecordingCueSink();
            PatientCueEmit.Emit(sink, patientId: 9, kind: (int)CueKind.Groan, position: 0.5f, cellX: 0, cellY: 0, cellZ: 0);
            Assert.IsEmpty(sink.LoopCalls, "13 不得触碰 BeginLoop / EndLoop(状态变化语义)");
            Assert.IsEmpty(sink.TierCalls, "13 不得触碰 SetTier(档位切换归 44 依 cue 自决)");
        }

        [Test]
        public void test_ac13d5_zeroPrngInTypeGraph()
        {
            // 13 代码路径零 PRNG 调用点(AC-13-D5):类型图不得出现 Random
            var violations = ScanClosureForNames(
                new[] { "System.Random", "UnityEngine.Random", "Random" });
            Assert.IsEmpty(violations,
                "AC-13-D5:13 禁 PRNG(去同步靠 PatientId 派生相位):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac13d5_phaseIsPureFunctionOfId()
        {
            // 相位是纯函数:同 id 同 tier 两次结果相同;不同 id 至少有一个不同
            var id = new PatientId(42);
            int a = PatientCue.Phase(id, SymptomTier.Severe, Intervals);
            int b = PatientCue.Phase(id, SymptomTier.Severe, Intervals);
            Assert.AreEqual(a, b, "相位须是纯函数(同 id 两次同值)");

            var phases = new HashSet<int>();
            for (int i = 0; i < 20; i++) phases.Add(PatientCue.Phase(new PatientId(i), SymptomTier.Severe, Intervals));
            Assert.Greater(phases.Count, 1, "不同 id 的相位须真错开(否则多病人叠成一声)");
        }

        [Test]
        public void test_ac13d6_cueIntervalIsStepFunctionOfTier()
        {
            // ⚠️ AC-13-D6:同一档内扫 position,输出**恰好一个**间隔值(反幻想机械护栏)
            var seen = new HashSet<int>();
            for (float p = Bands.SeekMin; p < 1.0f; p += 0.01f)   // Severe 档全扫
            {
                var tier = BehaviorMap.Tier(p, 0.1f, Bands);
                Assert.AreEqual(SymptomTier.Severe, tier, $"position={p} 须在 Severe 档");
                seen.Add(Intervals.For(tier));
            }
            Assert.AreEqual(1, seen.Count,
                "AC-13-D6:Severe 档内 CUE_INTERVAL 须恒定(玩家数不出连续值);实测 " +
                string.Join(",", seen));
        }

        [Test]
        public void test_ac13d6_intervalsAreOrderedAcrossTiers()
        {
            Assert.IsEmpty(CueIntervals.Validate(Intervals), "默认样例须满足档间有序");
            var broken = new CueIntervals(severe: 100, mild: 40, recover: 40);
            Assert.IsNotEmpty(CueIntervals.Validate(broken),
                "Severe > Mild 反序 ⇒ Validate 须报错(否则有序性空转)");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-13-C1 —— `IPresentPatients` 载荷不含禁入字段(走真守卫)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac13c1_presentPatientCarriesNoDiagnosticOrVitals()
        {
            // ⚠️ 载荷字段集恰 = {Id, Cell, State} —— 无 disease_id / position / trend / signs
            var fields = typeof(PresentPatient).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                               .Select(f => f.Name).OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(new[] { "Cell", "Id", "State" }, fields,
                "AC-13-C1:PresentPatient 载荷须恰 = {Id, Cell, State} " +
                "(不含 disease_id / position / trend / signs[])");
        }

        [Test]
        public void test_ac13c1_noDiseaseIdInViewEnum()
        {
            // 枚举成员名也不得带病种语义
            foreach (var n in Enum.GetNames(typeof(PresentPatientState)))
                Assert.IsFalse(n.Contains("Disease") || n.Contains("Diagnos") || n.Contains("Symptom"),
                    $"AC-13-C1:枚举成员「{n}」不得带诊断语义");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描器(与 interaction story-001 同款纪律:正测与夹具**共用一台机器**)
        // ═══════════════════════════════════════════════════════════

        private static BehaviorState Map(float p, BehaviorState prev, bool knowsClinic)
            => BehaviorMap.Map(new BehaviorInput(p, 0f, prev), Bands, knowsClinic);

        /// <summary>扫描 13 生产命名空间的类型图,点名禁入名。
        /// <para>⚠️ <paramref name="roots"/> 非 null ⇒ 夹具路径(**同一台机器**,换根而已)——
        /// 若正测走 A 机器、夹具走 B 机器,则「夹具红」**不蕴含**「真断言红」。
        /// ⚠️ **B1/M2 修复(2026-10-04 评审)**:生产根 = <see cref="ProductionScanSeeds"/>
        /// (13 前缀 **∪** 44 桥前缀)—— 原实现只取 `...PatientAI` 一个前缀,
        /// 44 桥(`PatientCueEmit`)在面外 ⇒ `Assert.IsEmpty` **阴性恒真**。</para>
        /// <para>⚠️ **归一化口径订正(M2)**:本扫描器用**朴素全名 `Contains`**,
        /// **不**等同生产 `PresentationDtoGuard.NormalizeMemberName`(去 `_`/`-`、取
        /// `<Prop>k__BackingField`)—— 原注释自称「同款」为**不实**,现如实声明的差异:
        /// 本机对**带下划线/连字符的变体名**(如 `disease_DiagnosisMask`)会**漏报**,
        /// 该缺口已登记(NOT-RUN,归 story 003 收紧或改用 DtoGuard)。</para></summary>
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

        /// <summary>生产扫描根 —— 13 侧**全部**驻留命名空间(AC-13-A1/A3 的「全类型」半边)。
        /// <para>⚠️ **B1/M2 修复(2026-10-04 评审)**:原根只取 `...PatientAI` 一个前缀,
        /// 而 13 的**唯一 44 发射桥** `PatientCueEmit.cs` 按 b5① 逃逸谓词**必须**住
        /// `...Gameplay.Presentation.Audio` ⇒ 桥**不在**原扫描面内 ⇒ `Assert.IsEmpty` **阴性恒真**。
        /// 现根 = 13 前缀 **∪** 44 桥前缀 —— 两处任一新增写三流代码都进面内。
        /// <para>⚠️ **本方法是正测与夹具**共用的同一台机器**(夹具经 <c>ScanClosureForNames(.., roots)</c>
        /// 换根,机器不变)—— 承 `boundary_discipline_test.cs:502-505` 已立成文的纪律。</para></summary>
        private static IEnumerable<Type> ProductionScanSeeds()
        {
            // 13 侧驻留命名空间(白名单式枚举 —— 新命名空间须**显式**加入,免静默出面)
            var nsPrefixes = new[]
            {
                "DaYiJingCheng.Gameplay.PatientAI",              // 13 生产
                "DaYiJingCheng.Gameplay.Presentation.Audio",     // 44 发射桥(PatientCueEmit)
            };
            return typeof(PatientBehaviorDirector).Assembly.GetTypes()
                .Where(t => t.Namespace != null &&
                            nsPrefixes.Any(p => t.Namespace == p ||
                                                t.Namespace.StartsWith(p + ".", StringComparison.Ordinal)))
                .ToList();
        }

        /// <summary>命名空间剪枝(与 `PresentationDtoGuard.ShouldExpandMembers` 同口径)。
        /// <b>默认展开</b>;仅引擎 / BCL / 第三方 ns 跳过自有成员 —— 刻意**不用**项目白名单
        /// (反向白名单 = 白名单外项目类型成叶子 = 假绿)。</summary>
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

        /// <summary>字段名词面扫描(AC-13-A5 负夹具的机器;与正测的白名单断言同一判据面)。</summary>
        private static List<string> ScanFieldsForToken(Type t, string[] tokens)
        {
            var hits = new List<string>();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                foreach (var tok in tokens)
                    if (f.Name.Contains(tok)) hits.Add($"[AC] {t.Name}.{f.Name} —— 命中「{tok}」");
            return hits;
        }

        // ═══════════════════════════════════════════════════════════
        //  夹具(假件 + 影子类型;住测试装配,不污染生产命名空间)
        // ═══════════════════════════════════════════════════════════

        private sealed class RecordingVitalsQuery : IVitalsQuery
        {
            private readonly VitalsDto _dto;
            public int CallCount { get; private set; }
            public RecordingVitalsQuery(VitalsDto dto) { _dto = dto; }
            public VitalsDto GetVitals(PatientId p) { CallCount++; return _dto; }
        }

        /// <summary>记录型 44 cue 假件 —— 分别记录 Emit / 循环 / 档位三条通道,
        /// 让「13 只走一次性通道」成为**可证伪**的判据(而非「没看到就算没调」)。</summary>
        private sealed class RecordingCueSink : IAudioCueSink
        {
            public readonly List<AudioCueDto> Calls = new List<AudioCueDto>();
            public readonly List<string> LoopCalls = new List<string>();
            public readonly List<string> TierCalls = new List<string>();
            public void Emit(in AudioCueDto cue) => Calls.Add(cue);
            public AudioCueHandle BeginLoop(in AudioCueDto cue) { LoopCalls.Add("BeginLoop"); return default; }
            public void EndLoop(AudioCueHandle handle) => LoopCalls.Add("EndLoop");
            public void SetTier(TierSource source) => TierCalls.Add("SetTier");
        }

        private sealed class ShadowBehaviorInputWithSigns { public int SignChannelMask; }
        private sealed class ShadowPatientWithSink { public IEventSink Sink; }

        /// <summary>负夹具(B2):自力产出 `VitalsDto` 的「第二取数来源」影子类型。
        /// <para>⚠️ 本型**只**服务 `test_ac13a2_sourceClosure_catchesSecondSource_negativeFixture`
        /// —— 它证「来源闭包扫描器非空转」,不证生产面合规。</para></summary>
        private sealed class ShadowVitalsSource
        {
            public VitalsDto GetVitalsDirect(int id) => default;
        }

        /// <summary>负夹具(F-3/M1):**在第三个方法里偷偷写 `Session`** 的影子类型。
        /// <para>⚠️ 与生产 `PatientBehavior` **同一形态**(自动属性 + 后备字段),
        /// 故走**同一台机器**(`ScanSessionWriters`)。`SmuggledWriter` = 第三写点,
        /// 扫描器**必须**抓到它 —— 否则原 F-3 的「判据降级」未被真正修复。</para></summary>
        private sealed class ShadowSessionWriter
        {
            public SessionState Session { get; private set; } = SessionState.None;
            public void ApplyExamSession(bool active) => Session = active ? SessionState.InTreatment : SessionState.None;
            public void ResetForLoad() { /* 影子刻意不给这第二写点 —— 见断言 */ }
            public void SmuggledWriter() { Session = SessionState.InTreatment; }   // ⚠️ 第三写点(违规样本)
        }
    }
}
