// 权威来源:ADR-019 §五(P0 切面 = 重算器 + 调试视图 + 导出)·
//   production/epics/telemetry-analytics/story-004-f3-skip-rate.md(AC-51-B8…B9)
//   · story-005-f4-skill-growth.md(AC-51-B10…B11 · D2)· story-006-f5-difficulty-curve.md(AC-51-B12…B14)
//   · story-007-f6-f7-session-rhythm.md(AC-51-E…)
//
// ⚠️ 本件是 TD-epic-01 **F3–F7 接线判据**的夹具(2026-10-19 第六片):
//   F3–F7 的谓词面此前只在 EditMode 里被**自造输入**驱动;`TelemetryReporter` 的还原器
//   (事件流 → 谓词面输入)一条都没接线 ⇒ 生产里算不出来。本夹具全部用
//   **真 EventStream + 真 PayloadEncoder/Decoder + 真 InMemoryBlobPool** 驱动生产代码。
//
// ⚠️ 口径来源(禁自行发明,逐条见 `TelemetryReporter` 的 F3–F7 段注):
//   · F3 还原 = `EmergencyAttempt.method`(10 GDD 规则十二:Played ⇔ Manual · Skipped ⇔ Skip);
//     `cause` 住结算事件且本类**不猜**(见下 `test_wired_f3_causeNotInvented`);
//   · F4 还原 = `SkillGrown`;`Level = LevelNotGrown(-1)` ⇒ 「不可得」≠ 0(AC-51-D2);
//   · F5 半开窗 `[t_k, t_k+W)`;三流分列(流别派生自 `StreamRouting.Of`,不另抄白名单);
//   · F6/F7 退化(n<2)⇒ 不可定义 ≠ 0。

using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class F3ToF7WiringTest
    {
        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        private IPayloadDecoder _decoder;
        private EventStream _stream;
        private TelemetryReporter _reporter;
        private long _tailTick;

        private const int MethodManual = 0;   // 10 GDD 规则十二:Played ⇔ Manual
        private const int MethodSkip = 1;     // Skipped ⇔ Skip

        private static IReadOnlyList<DiseaseRegistryEntry> Registry()
            => new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } };

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            _reporter = new TelemetryReporter(_stream, _decoder, Registry());
            _tailTick = 0;
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }

        // ── 写流辅助(真编码器 + 真 Append)──────────────────────────────

        private void AppendRaw(EventKind kind, object payload, long? tick = null)
        {
            PayloadRef r;
            switch (kind)
            {
                case EventKind.EmergencyAttempt:          r = _encoder.Encode(kind, (EmergencyAttemptPayload)payload); break;
                case EventKind.EmergencyTreatmentApplied: r = _encoder.Encode(kind, (EmergencyTreatmentAppliedPayload)payload); break;
                case EventKind.SkillGrown:                r = _encoder.Encode(kind, (SkillGrownPayload)payload); break;
                default: throw new System.ArgumentException("本夹具不写该 Kind: " + kind);
            }
            _stream.Append(new SimEvent(tick ?? _tailTick++, new PatientId(1), -1, kind, r));
        }

        private void AppendAttempt(int method)
        {
            AppendRaw(EventKind.EmergencyAttempt,
                new EmergencyAttemptPayload(action: 1, holdTicks: 10, edges: 0, magPeak: 0,
                    magLast: 0, method: method, actorId: 7, edgeTicks: new int[0]));
        }

        /// <summary>指定 tick / 病人 / 施予者的意图(关联判据用)。</summary>
        private void AppendAttemptAt(long tick, int patientId, int actorId, int method)
        {
            PayloadRef r = _encoder.Encode(EventKind.EmergencyAttempt,
                new EmergencyAttemptPayload(action: 1, holdTicks: 10, edges: 0, magPeak: 0,
                    magLast: 0, method: method, actorId: actorId, edgeTicks: new int[0]));
            _stream.Append(new SimEvent(tick, new PatientId(patientId), -1, EventKind.EmergencyAttempt, r));
        }

        /// <summary>指定 tick / 病人 / 施予者的结算。</summary>
        /// <param name="tick">事件头 tick(流序,须非降;载荷 tick 与它同值 = 主机写路径的形状)。</param>
        private void AppendTreatmentAt(long tick, int patientId, int actorId, int method, int cause)
        {
            PayloadRef r = _encoder.Encode(EventKind.EmergencyTreatmentApplied,
                new EmergencyTreatmentAppliedPayload(tick, treatmentId: 1, actorId: actorId,
                    polarity: 1, drugPotency: Fix.One, halfLife: 10, method: method, cause: cause, seq: 0));
            _stream.Append(new SimEvent(tick, new PatientId(patientId), -1,
                EventKind.EmergencyTreatmentApplied, r));
        }

        private void AppendTreatment(int method, int cause, long tick)
        {
            AppendRaw(EventKind.EmergencyTreatmentApplied,
                new EmergencyTreatmentAppliedPayload(tick, treatmentId: 1, actorId: 7, polarity: 1,
                    drugPotency: Fix.One, halfLife: 10, method: method, cause: cause, seq: 0),
                tick);
        }

        private void AppendGrowth(int skillId, int noveltyClass, int level, long tick)
        {
            AppendRaw(EventKind.SkillGrown,
                new SkillGrownPayload(actorId: 7, patientId: -1, skillId: skillId, objectId: 1,
                    noveltyClass: noveltyClass, level: level),
                tick);
        }

        // ══════════════ F3 ══════════════

        /// <summary>F3 正向:3 次意图(1 Manual / 2 Skip)⇒ Skip/Opportunity = 2/3。
        /// <para>识别力:`EmergencyAttempt` 不还原 ⇒ 分母 0;Method 反判 ⇒ 1/3;
        /// 把结算事件也当意图 ⇒ 分母虚高。</para></summary>
        [Test]
        public void test_wired_f3_skipOverOpportunity()
        {
            AppendAttempt(MethodManual);
            AppendAttempt(MethodSkip);
            AppendAttempt(MethodSkip);

            string report = _reporter.SkipRateReport(out int skipped, out int _);

            Assert.That(skipped, Is.EqualTo(0));
            StringAssert.Contains("F3 跳过率 = 2 / 3", report);
        }

        /// <summary>F3 负向:只有结算事件、没有意图事件 ⇒ Opportunity = 0(分母 0,不是 0/1)。
        /// <para>`NotOffered` 恒不在流上(10 GDD:「缺席推定」);但结算事件也**不能**顶替意图。</para></summary>
        [Test]
        public void test_wired_f3_settlementIsNotAnOpportunity()
        {
            AppendTreatment(MethodSkip, cause: 0, tick: 1);

            string report = _reporter.SkipRateReport(out int skipped, out int _);

            Assert.That(skipped, Is.EqualTo(0));
            StringAssert.Contains("F3 跳过率 = 分母 0", report);
        }

        /// <summary>F3 自陈(已收窄 · 第八片):`cause` 住结算事件,**不经结算全序关联前一律 null**。
        /// <para>本用例:跳过意图存在,但结算属于**另一病人** ⇒ 关联不上 ⇒ 「玩家选择」仍 0/1,
        /// 且「跳过·未关联」= 1/1(可观测:关联面有洞,不是猜成 0 也不猜成 1)。</para>
        /// <para>判别力:把任何「同名 / 最近 / 同 tick」的宽松规则当关联 ⇒ 本条红 —— 那正是「猜」的形态。</para></summary>
        [Test]
        public void test_wired_f3_causeNotInvented()
        {
            AppendAttemptAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip);
            // 同 tick、同施予者,但**不同病人**的结算 —— 不属于上面那笔意图,不得贴。
            AppendTreatmentAt(tick: 1, patientId: 2, actorId: 7, method: MethodSkip, cause: 0 /* 玩家选择 */);

            string report = _reporter.SkipRateReport(out _, out int unattributed);

            Assert.That(unattributed, Is.EqualTo(1), "跳过贴不上结算 ⇒ 计未关联");
            StringAssert.Contains("F3 跳过·玩家选择 = 0 / 1", report);
            StringAssert.Contains("F3 跳过·降级 = 0 / 1", report);
            StringAssert.Contains("F3 跳过·未关联 = 1 / 1", report);
        }

        // ══════════ F3 · cause 关联(第八片)══════════

        /// <summary>F3 关联正向:同病人同施予者的结算贴回其意图;两笔跳过各带不同 cause ⇒
        /// 玩家选择 / 降级两个分子**分开报**(AC-51-B8),未关联 = 0。
        /// <para>判别力:关联停用(全 null)⇒ 玩家选择 = 0/2;关联错配(把降级贴给第一笔)⇒ 1/0。</para></summary>
        [Test]
        public void test_wired_f3_causeAssociated_twoMoleculesSeparate()
        {
            AppendAttemptAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip);
            AppendTreatmentAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip, cause: 1 /* 降级 */);
            AppendAttemptAt(tick: 5, patientId: 1, actorId: 7, method: MethodSkip);
            AppendTreatmentAt(tick: 5, patientId: 1, actorId: 7, method: MethodSkip, cause: 0 /* 玩家选择 */);

            string report = _reporter.SkipRateReport(out _, out int unattributed);

            Assert.That(unattributed, Is.EqualTo(0));
            StringAssert.Contains("F3 跳过率 = 2 / 2", report);
            StringAssert.Contains("F3 跳过·玩家选择 = 1 / 2", report);
            StringAssert.Contains("F3 跳过·降级 = 1 / 2", report);
            StringAssert.Contains("F3 跳过·未关联 = 0 / 2", report);
        }

        /// <summary>F3 关联定序:同键两笔未结算意图 + 两笔结算 ⇒ 按**结算全序**一一对应
        /// (第一笔结算贴第一笔意图),不得把两笔都贴到后一笔、也不得把第一笔标成已结算。
        /// <para>⚠️ 判别力构造(2026-10-19 证伪轮 M-c-6 修正):两笔意图必须**结算路径不同**
        /// (Manual / Skip)。若两笔同为 Skip、两笔结算各给一个 cause,则「贴第一笔」与「贴第二笔」
        /// 在报告上读数**完全相同**(两个分子各 1),夹具对定序毫无判别力 —— 首版即此形态,
        /// 变异 M-c-6(取最近而非最早)仍绿,故已改。报告的两个 cause 分子只数 Skipped,
        /// 所以 cause 落在 Manual 笔还是 Skip 笔上会改变读数。</para>
        /// <para>判别力:关联取「最近一笔」⇒ B 得降级、A 得玩家选择 ⇒ 玩家选择 0/2 · 降级 1/2(本条红)。</para></summary>
        [Test]
        public void test_wired_f3_associationIsSettlementOrder()
        {
            // 流按 tick 非降(EventStream 硬不变量)⇒ 定序样本须跨 tick 交叠:
            //   t1 意图 A(Manual)→ t2 意图 B(Skip)→ t3 结算(降级)→ t4 结算(玩家选择)。
            // 正解(未结算的**最早**者):A ← 降级 · B ← 玩家选择 ⇒ 玩家选择 1/2 · 降级 0/2
            // 错解(未结算的**最近**者):B ← 降级 · A ← 玩家选择 ⇒ 玩家选择 0/2 · 降级 1/2
            AppendAttemptAt(tick: 1, patientId: 1, actorId: 7, method: MethodManual);
            AppendAttemptAt(tick: 2, patientId: 1, actorId: 7, method: MethodSkip);
            AppendTreatmentAt(tick: 3, patientId: 1, actorId: 7, method: MethodManual, cause: 1 /* 降级 */);
            AppendTreatmentAt(tick: 4, patientId: 1, actorId: 7, method: MethodSkip, cause: 0 /* 玩家选择 */);

            string report = _reporter.SkipRateReport(out _, out int unattributed);

            Assert.That(unattributed, Is.EqualTo(0), "两笔意图各得一笔结算");
            StringAssert.Contains("F3 跳过·玩家选择 = 1 / 2", report);
            StringAssert.Contains("F3 跳过·降级 = 0 / 2", report);
        }

        /// <summary>F3 关联不吃「多出来的结算」:意图已结算后再来一笔同键结算 ⇒ 不覆盖已贴的 cause,
        /// 也不把它贴给任何别的意图(多余结算 = 流异常的可观测面,不是可押注的证据)。</summary>
        [Test]
        public void test_wired_f3_extraSettlement_doesNotOverwrite()
        {
            AppendAttemptAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip);
            AppendTreatmentAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip, cause: 1 /* 降级 */);
            AppendTreatmentAt(tick: 2, patientId: 1, actorId: 7, method: MethodSkip, cause: 0 /* 玩家选择 */);

            string report = _reporter.SkipRateReport(out _, out int unattributed);

            Assert.That(unattributed, Is.EqualTo(0));
            StringAssert.Contains("F3 跳过·降级 = 1 / 1", report);
            StringAssert.Contains("F3 跳过·玩家选择 = 0 / 1", report);
        }

        /// <summary>F3 关联枚举越界:cause 序数不在 {0,1} ⇒ 不猜名,计未关联(不进任一分子)。</summary>
        [Test]
        public void test_wired_f3_causeEnumOutOfRange_notGuessed()
        {
            AppendAttemptAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip);
            AppendTreatmentAt(tick: 1, patientId: 1, actorId: 7, method: MethodSkip, cause: 7);

            string report = _reporter.SkipRateReport(out _, out int unattributed);

            Assert.That(unattributed, Is.EqualTo(1), "越界 cause ⇒ 不计入任一分子");
            StringAssert.Contains("F3 跳过·玩家选择 = 0 / 1", report);
            StringAssert.Contains("F3 跳过·降级 = 0 / 1", report);
        }

        /// <summary>F3 关联不吃结算数:Played 实例也带结算,但其结算**不得**被算成跳过机会或跳过数;
        /// 且结算本身不产生实例(实例只来自意图)。</summary>
        [Test]
        public void test_wired_f3_settlements_neverBecomeInstances()
        {
            AppendAttemptAt(tick: 1, patientId: 1, actorId: 7, method: MethodManual);
            AppendTreatmentAt(tick: 1, patientId: 1, actorId: 7, method: MethodManual, cause: 0);
            AppendTreatmentAt(tick: 2, patientId: 1, actorId: 7, method: MethodSkip, cause: 1); // 孤结算

            string report = _reporter.SkipRateReport(out _, out int unattributed);

            Assert.That(unattributed, Is.EqualTo(0), "无跳过意图 ⇒ 未关联恒 0(孤结算不计)");
            StringAssert.Contains("F3 跳过率 = 0 / 1", report);
        }

        /// <summary>F3 损坏:用 SkillGrown 编码却按 EmergencyAttempt 入流 ⇒ 解码失败计数,不当 Manual/null。</summary>
        [Test]
        public void test_wired_f3_corruptPayload_countedNotZeroed()
        {
            PayloadRef bad = _encoder.Encode(EventKind.SkillGrown,
                new SkillGrownPayload(7, -1, 1, 1, 0, 1));
            _stream.Append(new SimEvent(0, new PatientId(1), -1, EventKind.EmergencyAttempt, bad));

            string report = _reporter.SkipRateReport(out int skipped, out int _);

            Assert.That(skipped, Is.EqualTo(1), "损坏载荷须被计数");
            StringAssert.Contains("F3 跳过率 = 分母 0", report);
        }

        // ══════════════ F4 ══════════════

        /// <summary>F4 正向:三个新颖度各 1 ⇒ 守恒成立;△Level = 3−1 = 2。
        /// <para>识别力:NoveltyClass 分档反 ⇒ N_new/N_repeat 错;Level 直取(-1 当值)⇒ ΔLevel 错。</para></summary>
        [Test]
        public void test_wired_f4_noveltyAndDeltaLevel()
        {
            AppendGrowth(skillId: 2, noveltyClass: 0 /* first */, level: 1, tick: 10);
            AppendGrowth(skillId: 2, noveltyClass: 1 /* repeat */, level: 2, tick: 11);
            AppendGrowth(skillId: 2, noveltyClass: 2 /* stale */, level: 3, tick: 12);
            AppendGrowth(skillId: 9, noveltyClass: 0, level: 5, tick: 13);  // 别的技能,不入窗

            string report = _reporter.SkillGrowthReport("2", growthTicks: null, out int skipped);

            Assert.That(skipped, Is.EqualTo(0));
            StringAssert.Contains("F4 Practice = 3", report);
            StringAssert.Contains("F4 N_new = 1", report);
            StringAssert.Contains("F4 N_repeat = 1", report);
            StringAssert.Contains("F4 N_stale = 1", report);
            StringAssert.Contains("F4 NoveltyConserves = 1", report);
            StringAssert.Contains("F4 ΔLevel = 2", report);
        }

        /// <summary>F4 边界(AC-51-D2):未升级哨兵 `LevelNotGrown = -1` ⇒ **不可得**,不报 0 / 不报 −2。</summary>
        [Test]
        public void test_wired_f4_levelSentinel_isUnavailableNotEmptyZero()
        {
            AppendGrowth(skillId: 2, noveltyClass: 0, level: SkillGrownPayload.LevelNotGrown, tick: 10);
            AppendGrowth(skillId: 2, noveltyClass: 0, level: 3, tick: 11);

            string report = _reporter.SkillGrowthReport("2", null, out _);

            StringAssert.Contains("F4 ΔLevel = 不可得", report);
        }

        /// <summary>F4 窗口:成长窗口由调用方给 ⇒ 窗外的成长不进 Practice(窗口参数归用户调)。</summary>
        [Test]
        public void test_wired_f4_windowExcludesOutOfRange()
        {
            AppendGrowth(skillId: 2, noveltyClass: 0, level: 1, tick: 10);
            AppendGrowth(skillId: 2, noveltyClass: 0, level: 1, tick: 50);

            string report = _reporter.SkillGrowthReport("2", (from: 5, to: 20), out _);

            StringAssert.Contains("F4 Practice = 1", report);
        }

        // ══════════════ F5 ══════════════

        /// <summary>F5 半开窗(AC-51-B12):窗 [0,10) 的边界 —— tick 0 与 tick 9 计入,tick 10 不计入。
        /// <para>识别力:改闭区间 ⇒ 密度 3;忽略 tick ⇒ 全错。</para></summary>
        [Test]
        public void test_wired_f5_halfOpenWindow_noDoubleCount()
        {
            // 三条病史流事件(任意病史 Kind —— F5 只读事件头,不看载荷)
            AppendTreatment(MethodManual, 0, tick: 0);
            AppendTreatment(MethodManual, 0, tick: 9);
            AppendTreatment(MethodManual, 0, tick: 10);

            string report = _reporter.DifficultyCurveReport(firstWindowTick: 0, step: 10, window: 10, out int skipped);

            Assert.That(skipped, Is.EqualTo(0));
            // ⚠️ 序列长度 = `f(t_0, S, Tick_max)`、**与 W 无关** —— 实测(2026-10-19):
            //    Tick_max = 10, S = 10 ⇒ 窗只 t_0=0 与 t_0+S=10 两窗(`[0,10)` `[10,20)`)。
            //    第三条(tick 10)在 `[0,10)` **不计**,在 `[10,20)` **恰计入一次** ⇒ [2,1]。
            //    判别力:改成闭区间 ⇒ [3,…](tick 10 被双计);tick 错位 ⇒ [1,2]。
            StringAssert.Contains("F5 History 密度序列 = [2,1]", report);
        }

        /// <summary>F5 三流分列(AC-51-B13):病史 2 条 + 世界 1 条 ⇒ History 窗 = 2、World 窗 = 1、
        /// Case 窗 = 0 —— **不合并**。
        /// <para>识别力:三流合并成一条 ⇒ History 窗 = 3 且 World 窗 = 3。</para></summary>
        [Test]
        public void test_wired_f5_threeStreamsSeparate()
        {
            AppendTreatment(MethodManual, 0, tick: 0);
            AppendTreatment(MethodManual, 0, tick: 5);
            // 世界流一条:DropSpawned(载荷形状不参与 F5 计数,但仍须可编解码 —— 用真载荷)
            PayloadRef w = _encoder.Encode(EventKind.DropSpawned,
                new DropSpawnedPayload(instanceId: 1, spawnAnchor: default(WorldPos), itemKey: 1, qty: 1));
            _stream.Append(new SimEvent(6, PatientId.None, -1, EventKind.DropSpawned, w));

            string report = _reporter.DifficultyCurveReport(0, 10, 10, out _);

            // 长度 = 窗数(Tick_max=6, S=10 ⇒ 一窗 `[0,10)`);三流各一条**独立**序列
            StringAssert.Contains("F5 History 密度序列 = [2]", report);
            StringAssert.Contains("F5 World 密度序列 = [1]", report);
            StringAssert.Contains("F5 Case 密度序列 = [0]", report);
            Assert.That(report, Does.Not.Contain("F5 History 密度序列 = [3]"),
                "三流合并 ⇒ History 窗 = 3(本条防合并)");
        }

        /// <summary>F5 覆盖记账(AC-51-B13):步长 S > 窗宽 W ⇒ **未覆盖区间存在** ⇒
        /// Host 无法把「未测得」读成「测得 0」。判别力:覆盖判据反 / 窗口闭区间 ⇒ 窗口并集覆盖全区间 ⇒ 转红。</summary>
        [Test]
        public void test_wired_f5_gapLeavesCoverageShort()
        {
            AppendTreatment(MethodManual, 0, tick: 0);
            AppendTreatment(MethodManual, 0, tick: 40);

            // W=10, S=30 ⇒ 窗 [0,10) 与 [30,40);tickMin=0, SpanTicks = Tick_max − Tick_min = 40
            string report = _reporter.DifficultyCurveReport(firstWindowTick: 0, step: 30, window: 10, out _);

            StringAssert.Contains("F5 History WindowCoverage = (20, 40)", report);
        }

        /// <summary>F5 参数退化硬失败(AC-51-B14):W &lt; 1 ⇒ 抛(不静默取默认值)。</summary>
        [Test]
        public void test_wired_f5_degenerateWindow_throws()
        {
            AppendTreatment(MethodManual, 0, tick: 0);

            Assert.Throws<System.ArgumentException>(
                () => _reporter.DifficultyCurveReport(0, 5, 0, out _));
        }

        // ══════════════ F6 / F7 ══════════════

        /// <summary>F6 会话跨度:F6 还原面 = 三流合流 ⇒ 跨度跨流(decode 面不看 Kind 之外的东西)。</summary>
        [Test]
        public void test_wired_f6_spanAcrossStreams()
        {
            AppendTreatment(MethodManual, 0, tick: 10);
            AppendGrowth(skillId: 2, noveltyClass: 0, level: 1, tick: 40);

            string report = _reporter.SessionRhythmReport(gapThreshold: 10, out int skipped);

            Assert.That(skipped, Is.EqualTo(0));
            StringAssert.Contains("F6 SessionSpan = 30", report);
        }

        /// <summary>F7 事件 &lt; 2 ⇒ **不可定义**(不是 0 —— 退化 ≠ 零值)。</summary>
        [Test]
        public void test_wired_f7_singleEvent_reportsUndefined()
        {
            AppendTreatment(MethodManual, 0, tick: 10);

            string report = _reporter.SessionRhythmReport(10, out _);

            StringAssert.Contains("F7 Δ 节律 = 不可定义", report);
            Assert.That(report, Does.Not.Contain("F7 Δ_median"), "不可定义时不得报中位数");
        }

        /// <summary>F7 上中位数(禁插值):Δ = {10, 20, 30, 50} ⇒ 上中位数 = 30(偶数个不取平均);
        /// MAD = median(|Δ−30|) = {20,10,0,20} 排序 {0,10,20,20} ⇒ 上中位数 20;GapCount(阈 20) = 1。</summary>
        [Test]
        public void test_wired_f7_upperMedianAndMad()
        {
            AppendTreatment(MethodManual, 0, tick: 0);
            AppendTreatment(MethodManual, 0, tick: 10);
            AppendTreatment(MethodManual, 0, tick: 30);
            AppendTreatment(MethodManual, 0, tick: 60);
            AppendTreatment(MethodManual, 0, tick: 110);

            string report = _reporter.SessionRhythmReport(gapThreshold: 20, out _);

            // 口径(2026-10-19 用户裁定「口径 1」):Δ_median = 排序后第 ⌈m/2⌉ 项(1-indexed),
            //   m = Δ 项数 = 4 ⇒ 第 2 项 ⇒ sorted{10,20,30,50}[idx=1] = **20**。
            // ⚠️ 本条注释此前把 1-indexed 项号 `⌈m/2⌉ = 2` 直接当 0-indexed 下标用(漏减 1),
            //   因此误判「生产式得 30、与 GDD 不符」并把偏差登记为 TD-epic-01 遗留项。
            //   实测:三个候选口径是 **20(上中位数,正确)/ 25(插值)/ 30(误按事件数 n=5 取第 3 项)**,
            //   生产式自始即取 20;错的是 GDD 公式里的符号 `n`(已订正为 `m`)与这条注释。
            //   判别力由 `f6_f7_session_rhythm_test::test_f7_upperMedian_differsFromInterpolationAndEventCount` 承担。
            StringAssert.Contains("F7 Δ_median = 20", report);
            StringAssert.Contains("F7 Δ_MAD = 10", report);
            // min/max 同行输出:整行断言(`Δ_min = 10` 单独匹配会命中 `Δ_max = 10` 的前缀)
            StringAssert.Contains("F7 Δ_min = 10  Δ_max = 50", report);
            StringAssert.Contains("F7 GapCount = 2", report);
        }
    }
}
