// 权威来源:GDD 规则十(五步流程)· F-11.4(恒 Applied)· F-11.5(成长门 GateHit)
//          · §Edge Cases(库存不足 / 对象离场 / dose_range 空 / 混堆 / K_difficulty 缺失 / 同 tick 两剂)
//          · ADR-005 / ADR-009 §七 / ADR-020 §四 AC-20-03 / ADR-026 / ADR-029 §③
//          · OQ-11-13(已裁 2026-10-06)· OQ-11-10 / D-21-29(已裁 2026-10-06)
//
// 测试 PrescribeFlow.Prescribe / EvaluateGateHit / DeriveKDifficulty。
//
// NOT-RUN 声明(禁借绿 —— 六处):
// - AC-11-16 正式对拍:K_difficulty 代入方已裁(OQ-11-13),但 30 侧 EmitGrowth 实现未落地
//   ⇒ 本 story 只交付「调用点形状 + 缺参硬失败机制半边」;正式对拍 BLOCKED-BY-30 实现。
// - AC-11-15 三格子句:BLOCKED-BY-ADR-012(CI 矩阵未激活);本文件只证 Mono 侧自洽。
// - 换算表真源:21a 未落 C# 字段(影子 schema;BLOCKED-BY-OQ-11-10 产出方)。
// - 省料数值:21a EFF 唯一出处 + 30 拥映射,值归数值轮(BL-6)⇒ 夹具用合成表。
// - 非主机传输半边:BLOCKED-BY-45(网络 epic);本文件只证本地零 Append 分支。
// - 同 tick 两剂「各得不同 Seq」(Edge Cases / TR-prescription-012):生产件对两笔同 tick 事件
//   均写 `Seq = 0` 占位(与 10 的 HostEmergencyProcessor 同现状),**发号权在主机 Append**
//   ⇒ 归 45 / 7a;本文件不覆盖,亦不由测试名暗示已覆盖(QA m6)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Prescription;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PrescriptionMedication
{
    /// <summary>载荷解码探针(测试侧最小实现,不依赖 Sim.Codec —— EditMode 装配引用面见 asmdef)。</summary>
    internal sealed class CapturingEncoder : IPayloadEncoder
    {
        public readonly List<EventKind> Kinds = new List<EventKind>();
        public readonly List<object> Payloads = new List<object>();

        public PayloadRef Encode<T>(EventKind kind, in T payload) where T : struct
        {
            Kinds.Add(kind);
            Payloads.Add(payload);
            return new PayloadRef(Kinds.Count, 0, 0);
        }
    }

    /// <summary>事件 sink 探针(主机唯一 Append 的落点)。</summary>
    internal sealed class SpySink : IEventSink
    {
        public readonly List<SimEvent> Events = new List<SimEvent>();
        public void Append(in SimEvent e) => Events.Add(e);
    }

    /// <summary>在场查询桩。</summary>
    internal sealed class StubPresence : IPresenceQuery
    {
        public bool Present = true;
        public bool IsPresent(PatientId patientId) => Present;
        public int PresentCount => Present ? 1 : 0;
        public bool IsPresentAt(WorldPos cell) => Present;
        public IReadOnlyCollection<int> PresentPatientIds() => Present ? new[] { 0 } : Array.Empty<int>();
    }

    /// <summary>换算表桩(影子 schema —— 21a 产出方落位后仅换实现)。
    /// <para>⚠️ 只查表,**不做乘法** —— `portions = dose × portions_per_dose` 住 11(结构评审 m1)。</para></summary>
    internal sealed class StubConversion : IPortionsConversion
    {
        public int PerDose = 1;               // portions_per_dose(空 ⇒ 1 恒等)
        public readonly List<ItemKey> Calls = new List<ItemKey>();

        public int PortionsPerDose(ItemKey itemKey)
        {
            Calls.Add(itemKey);
            return PerDose;
        }
    }

    /// <summary>库存桩 —— 记录扣减次数,可注入失败(并发夹具)。</summary>
    internal sealed class StubStore : IPortionsStore
    {
        public int Available = int.MaxValue;
        public bool ConsumeSucceeds = true;
        public int ConsumeCalls;
        public int LastPortions;
        public int PeekQuality = 1;

        public int LastHasPortions;                 // QA M4:验货实参须可断言(原仅 Available 比较)
        public bool HasPortions(int playerId, ItemKey itemKey, int portions)
        {
            LastHasPortions = portions;
            return portions <= Available;
        }

        public int PeekLowestQuality(int playerId, ItemKey itemKey, int portions)
            => portions <= 0 ? PrescribeFlow.NoQuality : PeekQuality;

        public bool ConsumePortions(int playerId, ItemKey itemKey, int portions)
        {
            ConsumeCalls++;
            LastPortions = portions;
            if (!ConsumeSucceeds) return false;
            Available -= portions;
            return true;
        }
    }

    /// <summary>技能桩 —— 等级可注入;EmitGrowth 记录调用(可注入缺参硬失败)。</summary>
    internal sealed class StubSkills : ISkillGrowthPort
    {
        public int Level;
        public bool KDifficultyMissing;                 // true ⇒ 模拟「派生不出 K_difficulty」
        public int EmitCalls;
        public int LastObjectId;
        public int LastNovelty;
        public Fix LastKDifficulty;
        public long LastTick;
        public PatientId LastPatient;

        public int QueryLevel(int actorId, int skillId) => Level;

        public void EmitGrowth(int actorId, int skillId, int objectId, int noveltyHint,
                               Fix kDifficulty, long tick, PatientId patientId)
        {
            EmitCalls++;
            LastObjectId = objectId; LastNovelty = noveltyHint;
            LastKDifficulty = kDifficulty; LastTick = tick; LastPatient = patientId;
        }
    }

    [TestFixture]
    public class PrescribeFlowTest
    {
        // ── 夹具 ────────────────────────────────────────────────────────────

        private const int DoseBase = 65536;                       // 1.0 Q16.16(数值归用户轮)
        private static readonly ItemKey Drug = new ItemKey("salicylic_acid", ProcessingState.Raw);

        private SpySink _sink;
        private CapturingEncoder _encoder;
        private StubConversion _conversion;
        private StubStore _store;
        private StubPresence _presence;
        private StubSkills _skills;
        private PrescribePorts _ports;

        [SetUp]
        public void SetUp()
        {
            _sink = new SpySink();
            _encoder = new CapturingEncoder();
            _conversion = new StubConversion();
            _store = new StubStore();
            _presence = new StubPresence();
            _skills = new StubSkills();
            _ports = new PrescribePorts(_conversion, _store, _presence, _skills, DoseBase);
        }

        private static DrugProfile MakeProfile(DoseRange? range, long potencyRaw = 65536L, long halfLifeRaw = 131072L)
        {
            return new DrugProfile
            {
                DoseRange = range,
                DrugPotency = new Fix(potencyRaw),
                HalfLife = new Fix(halfLifeRaw),
                AxisOffsetByQuality = new[] { new Fix(0L), new Fix(-16384L) },
            };
        }

        private static PrescriptionEntry Entry(int unlockLevel = 0)
            => new PrescriptionEntry(actionId: 1, polarity: PrescriptionPolarity.Symptomatic, unlockLevel: unlockLevel);

        private static PrescribeRequest Request(DrugProfile profile, int dose = 2, int quality = 1, bool isHost = true)
            => new PrescribeRequest(Drug, Entry(), actorId: 7, patientId: new PatientId(3),
                                    selectedDose: dose, profile: profile, selectedQuality: quality, isHost: isHost);

        private PrescribeOutcome Run(in PrescribeRequest req, long tick = 100L)
            => PrescribeFlow.Prescribe(req, tick, _ports, _sink, _encoder);

        // ── AC-11-04:库存不足整体拒绝 ─────────────────────────────────────────

        [Test]
        public void test_prescribe_insufficientStock_rejectsWholly()
        {
            // Arrange:缺 1 份
            _store.Available = 1;                       // portions = 2 × 1 = 2 > 1
            var req = Request(MakeProfile(new DoseRange(1, 5)));

            // Act
            var outcome = Run(req);

            // Assert:零事件、零成长、零扣减
            Assert.IsFalse(outcome.Applied, "库存不足必须整体拒绝");
            Assert.AreEqual(0, _sink.Events.Count, "拒绝路径零事件");
            Assert.AreEqual(0, _skills.EmitCalls, "拒绝路径零成长");
            Assert.AreEqual(0, _store.ConsumeCalls, "拒绝路径不触扣减");
            Assert.AreEqual(1, _store.Available, "库存零变化");
        }

        [Test]
        public void test_prescribe_insufficientStock_zeroConversionAndStoreCalls()
        {
            // 拒绝路径**不进入②**:换算表与库存扣减面只被①的验货触及,扣减零调用
            _store.Available = 0;
            Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.AreEqual(0, _store.ConsumeCalls);
        }

        [Test]
        public void test_prescribe_exactStock_accepted()
        {
            // 边界:恰有 2 份 ⇒ 进入
            _store.Available = 2;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(2, outcome.Portions);
            Assert.IsNotNull(outcome.TreatmentEvent, "主机成功路径必带事件回执(n1)");
            Assert.AreEqual(1, _sink.Events.Count);
        }

        // ── AC-11-05:给错药三效果(11 读不到 treatable_by ⇒ 结构上无分支)───────

        [Test]
        public void test_prescribe_wrongDrug_eventStillEmitted_gateUnchanged()
        {
            // 11 读不到病种级布尔 ⇒ 「给错药」在 11 侧**不是**可表达的输入。
            // 三效果:① 事件照发;② 零惩罚路径(见 AC-11-10 断言);③ 成长门不因它而判。
            _skills.Level = 0;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.IsTrue(outcome.Applied, "恒 Applied:流程内零失败分支");
            Assert.AreEqual(1, _sink.Events.Count, "事件照发");
            Assert.IsTrue(outcome.GateHit, "门只读域内合法 ∧ 已解锁,与病种无关");
            Assert.AreEqual(1, _skills.EmitCalls);
        }

        // ── AC-11-06①:禁忌不拦不扣 ──────────────────────────────────────────

        [Test]
        public void test_prescribe_contraindicationsPresent_zeroInterception()
        {
            // contraindications[] 命中不影响流程(判断归 9 的和式,TR-prescription-011)
            var profile = MakeProfile(new DoseRange(1, 5));
            profile.Contraindications = new[] { "some_disease" };
            var outcome = Run(Request(profile));

            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(1, _sink.Events.Count);
            Assert.AreEqual(2, _store.LastPortions, "扣减量不受禁忌影响");
        }

        // ── AC-11-17:空 dose_range ⇒ 整剂 ────────────────────────────────────

        [Test]
        public void test_prescribe_emptyDoseRange_wholeDosePath()
        {
            // dose_range 空 ⇒ dose := 1,dose_potency = drug_potency,零报错零 clamp
            var profile = MakeProfile(null, potencyRaw: 49152L);
            var req = new PrescribeRequest(Drug, Entry(), 7, new PatientId(3),
                                           selectedDose: 99, profile: profile,
                                           selectedQuality: 1, isHost: true);

            var outcome = Run(req);

            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(1, _conversion.Calls.Count, "换算表恰查一次");
            Assert.AreEqual(1, _store.LastPortions, "整剂(dose := 1)× per-dose 1 ⇒ 1 份");

            var payload = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];
            Assert.AreEqual(49152L, payload.DrugPotency.Raw, "整剂 ⇒ dose_potency = drug_potency");
        }

        [Test]
        public void test_prescribe_gateHit_doseOutOfRangeIsFalse()
        {
            // ⚠️ QA m2:原稿只测 `doseRange == null` 的恒真支 ⇒ `doseLegal == false` 分支**永不执行**
            //    (Prescribe 在步骤① 已对越界 dose 短路)。直接调用侧须显式锁死该分支。
            var entry = Entry(unlockLevel: 0);
            bool hit = PrescribeFlow.EvaluateGateHit(new DoseRange(1, 5), effectiveDose: 6, entry, 7, _skills);
            Assert.IsFalse(hit, "越界 dose ⇒ 域内合法性假 ⇒ GateHit 假");

            bool lo = PrescribeFlow.EvaluateGateHit(new DoseRange(1, 5), effectiveDose: 1, entry, 7, _skills);
            bool hi = PrescribeFlow.EvaluateGateHit(new DoseRange(1, 5), effectiveDose: 5, entry, 7, _skills);
            Assert.IsTrue(lo && hi, "边界值(恰为 min / max)合法");
        }

        [Test]
        public void test_prescribe_emptyDoseRange_gateHitIsTrue()
        {
            // AC-11-17 的「另判点」:整剂路径下域内合法性 = 恒真(显式锁定)
            var entry = Entry(unlockLevel: 0);
            bool hit = PrescribeFlow.EvaluateGateHit(null, effectiveDose: 1, entry, actorId: 7, _skills);
            Assert.IsTrue(hit, "空域 ⇒ 域内合法性恒真,不得静默吞掉整类药的养成");
        }

        [Test]
        public void test_prescribe_emptyDoseRange_stillGrows()
        {
            var outcome = Run(new PrescribeRequest(Drug, Entry(), 7, new PatientId(3), 99,
                                                   MakeProfile(null), 1, true));
            Assert.IsTrue(outcome.GateHit);
            Assert.AreEqual(1, _skills.EmitCalls);
        }

        // ── 混堆确定性:结果 = f(多重集),与顺序无关 ───────────────────────────

        [Test]
        public void test_prescribe_mixedStack_qualityIsLowest()
        {
            _store.PeekQuality = 1;                       // 被耗集合含品级 {1, 2}(= 偏移表长度)
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5))));

            var payload = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];
            // quality 只经 F-11.2 影响 half_life;最低档 = 1 ⇒ 偏移表首项 = 0
            Assert.AreEqual(131072L, payload.HalfLife, "取最低档 ⇒ 用偏移表首项");
            Assert.AreEqual(2, outcome.Portions, "portions = dose 2 × portions_per_dose 1");
        }

        [Test]
        public void test_prescribe_mixedStack_portSeamExposesNoSequence()
        {
            // 判据 = **反射断言**(非 grep,承 ADR-020 §四 AC-20-03 先例):
            // 11 与 20 的接缝**结构上不可能**表达「列表顺序」—— 端口的方法签名里
            // 零数组 / 零 IEnumerable / 零 IList。
            // ⚠️ 结构评审 m6:本断言证的只是「**11 侧接口面不传 / 不取序列**」;
            //    **被耗集合的选择策略**(哪几份被扣)归 20,不归 11 —— 见双方 Dependencies。
            foreach (var t in new[] { typeof(IPortionsStore), typeof(IPortionsConversion) })
            {
                foreach (var m in t.GetMethods())
                {
                    Assert.IsFalse(IsSequenceLike(m.ReturnType),
                        $"{t.Name}.{m.Name} 返回值暴露序列 —— 11 会因此可读列表序");
                    foreach (var p in m.GetParameters())
                        Assert.IsFalse(IsSequenceLike(p.ParameterType),
                            $"{t.Name}.{m.Name}({p.Name}) 形参暴露序列 —— 11 会因此可读列表序");
                }
            }
        }

        private static bool IsSequenceLike(Type t)
            => t.IsArray || (t != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(t));

        [Test]
        public void test_prescribe_mixedStack_deterministic_sameInput()
        {
            // ⚠️ 结构评审 m6:本测为**文档性**(桩恒返常量 ⇒ 生产件只消费一个 int 标量,
            //    构造不出让本测失败的变异)。真判据是 `mixedStack_portSeamExposesNoSequence`。
            // 同一多重集(⇒ 同一 PeekLowestQuality 标量)⇒ 载荷逐字段相等(结果 = f(多重集))
            var profile = MakeProfile(new DoseRange(1, 5));
            _store.PeekQuality = 2;
            Run(Request(profile));
            var first = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];

            SetUp();
            _store.PeekQuality = 2;
            Run(Request(profile));
            var second = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];

            Assert.AreEqual(first.DrugPotency.Raw, second.DrugPotency.Raw);
            Assert.AreEqual(first.HalfLife, second.HalfLife);
            Assert.AreEqual(first.Polarity, second.Polarity);
            Assert.AreEqual(first.TreatmentId, second.TreatmentId);
        }

        [Test]
        public void test_prescribe_lowerQuality_shorterHalfLife()
        {
            // 品级越低 ⇒ 偏移越负 ⇒ 退得越快(F-11.2 单调性,11 侧只透传)。
            // ⚠️ 品级来自 20 的 **PeekLowestQuality**,11 不自选实例集。
            var profile = MakeProfile(new DoseRange(1, 5));
            _store.PeekQuality = 1;
            Run(Request(profile));
            long q1 = ((DrugTreatmentAppliedPayload)_encoder.Payloads[0]).HalfLife;

            SetUp();
            _store.PeekQuality = 2;
            Run(Request(profile));
            long q2 = ((DrugTreatmentAppliedPayload)_encoder.Payloads[0]).HalfLife;

            Assert.Less(q2, q1, "品级 2 的偏移为负 ⇒ 半衰期更短");
        }

        [Test]
        public void test_prescribe_qualityAlwaysFromStore_notFromRequest()
        {
            // 11 不使用请求里自带的 quality 字段(它只是呈现回执);真源 = 20 的验货标量
            _store.PeekQuality = 2;
            var req = new PrescribeRequest(Drug, Entry(), 7, new PatientId(3), 2,
                                           MakeProfile(new DoseRange(1, 5)), selectedQuality: 1, isHost: true);
            Run(req);
            // offsets[1] = -16384 ⇒ 131072 - 16384 = 114688(品级 2,而非请求里的 1)
            Assert.AreEqual(114688L, ((DrugTreatmentAppliedPayload)_encoder.Payloads[0]).HalfLife);
        }

        // ── TR-prescription-012:原子性 + 同 tick 双玩家 ────────────────────────

        [Test]
        public void test_prescribe_consumeFails_zeroEvents()
        {
            // 并发夹具:验货通过但扣减失败 ⇒ 零事件(不留中间态)
            _store.ConsumeSucceeds = false;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.IsFalse(outcome.Applied);
            Assert.AreEqual(0, _sink.Events.Count);
            Assert.AreEqual(0, _skills.EmitCalls);
        }

        [Test]
        public void test_prescribe_noEventBetweenVerifyAndConsume()
        {
            // 验货与扣减之间无事件写入:扣减失败时事件流全空(已由上测锁定);
            // 本测锁定成功路径的事件序 = 扣减后才发。
            Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.AreEqual(1, _store.ConsumeCalls);
            Assert.AreEqual(1, _sink.Events.Count);
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _sink.Events[0].Kind);
        }

        [Test]
        public void test_prescribe_twoPlayersSameTick_independentEvents()
        {
            // ⚠️ QA m6:同 tick 两剂「**各得不同 Seq**」(TR-prescription-012 / Edge Cases)本测**不覆盖** ——
            //    生产件对两笔同 tick 事件均写 `Seq = 0` 占位,**发号权在主机 Append(归 45 / 7a)**
            //    ⇒ 该项**显式登记 NOT-RUN**(见文件头第 6 条),不得由本测名暗示已覆盖。
            // 两名玩家同 tick 各开一方 ⇒ 各自完整五步,两笔独立事件(全序键定序,Seq 由主机发号)
            var reqA = new PrescribeRequest(Drug, Entry(), 7, new PatientId(3), 2,
                                            MakeProfile(new DoseRange(1, 5)), 1, true);
            var reqB = new PrescribeRequest(Drug, Entry(), 8, new PatientId(3), 2,
                                            MakeProfile(new DoseRange(1, 5)), 1, true);

            var a = PrescribeFlow.Prescribe(reqA, 100L, _ports, _sink, _encoder);
            var b = PrescribeFlow.Prescribe(reqB, 100L, _ports, _sink, _encoder);

            Assert.IsTrue(a.Applied && b.Applied);
            Assert.AreEqual(2, _sink.Events.Count, "互不判重、互不吞并");
            Assert.AreEqual(7, ((DrugTreatmentAppliedPayload)_encoder.Payloads[0]).ActorId);
            Assert.AreEqual(8, ((DrugTreatmentAppliedPayload)_encoder.Payloads[1]).ActorId);
        }

        // ── 缺参硬失败(AC-11-16 机制半边;正式对拍 NOT-RUN)──────────────────

        [Test]
        public void test_prescribe_kDifficultyMissing_hardFails()
        {
            // K_difficulty 派生源缺失(drug_potency 缺)⇒ 断言硬失败,不静默跳过
            var profile = MakeProfile(new DoseRange(1, 5), potencyRaw: 65536L);
            profile.DrugPotency = null;

            Assert.Throws<InvalidOperationException>(() => Run(Request(profile)));
        }

        [Test]
        public void test_prescribe_kDifficultyMissing_eventNotRolledBack()
        {
            // 成长失败**不回滚**事件(事件已进流 = 真源;成长不发是 F-11.5 的显式后果)
            var profile = MakeProfile(new DoseRange(1, 5));
            profile.DrugPotency = null;

            Assert.Throws<InvalidOperationException>(() => Run(Request(profile)));
            Assert.AreEqual(1, _sink.Events.Count, "事件已落流,不回滚");
            Assert.AreEqual(0, _skills.EmitCalls, "成长不发");
        }

        [Test]
        public void test_prescribe_gateMiss_noGrowth()
        {
            // 未命中门(等级不足)⇒ 零 SkillGrown
            _skills.Level = 0;
            var req = new PrescribeRequest(Drug, Entry(unlockLevel: 5), 7, new PatientId(3), 2,
                                           MakeProfile(new DoseRange(1, 5)), 1, true);

            var outcome = Run(req);

            Assert.IsTrue(outcome.Applied);
            Assert.IsFalse(outcome.GateHit);
            Assert.AreEqual(0, _skills.EmitCalls, "门未命中 ⇒ 零成长");
            Assert.AreEqual(1, _sink.Events.Count, "事件仍照发");
        }

        [Test]
        public void test_prescribe_gateHit_emitsExactlyOnce()
        {
            _skills.Level = 5;
            var outcome = Run(new PrescribeRequest(Drug, Entry(unlockLevel: 5), 7, new PatientId(3), 2,
                                                   MakeProfile(new DoseRange(1, 5)), 1, true));

            Assert.IsTrue(outcome.GateHit);
            Assert.AreEqual(1, _skills.EmitCalls, "命中 ⇒ 恰一条");
            Assert.AreEqual(PrescribeFlow.PrescriptionSkillId, (int)Sim.Contracts.SkillSystem.SkillId.处方用药);
            Assert.AreEqual(1, _skills.LastObjectId, "对象 id = 处方表处置 id");
            Assert.AreEqual(3, _skills.LastPatient.Value, "patient_id 透传(8 / 11 语境)");
            Assert.AreEqual(100L, _skills.LastTick);
        }

        // ── 步骤① 第三合取项:对象在场 ───────────────────────────────────────

        [Test]
        public void test_prescribe_patientAbsent_notEntered()
        {
            _presence.Present = false;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.IsFalse(outcome.Applied);
            Assert.AreEqual(0, _sink.Events.Count);
            Assert.AreEqual(0, _store.ConsumeCalls, "离场 ⇒ 未进入②③");
        }

        // ── 剂量域越界(步骤① 第一合取项)─────────────────────────────────────

        [Test]
        public void test_prescribe_doseOutOfRange_notEntered()
        {
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 6));
            Assert.IsFalse(outcome.Applied);
            Assert.AreEqual(0, _sink.Events.Count);
        }

        [Test]
        public void test_prescribe_doseAtLowerBound_accepted()
        {
            // dose_range.lo = 最小剂,**合法**(Edge Cases)
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 1));
            Assert.IsTrue(outcome.Applied);
        }

        [Test]
        public void test_prescribe_doseAtUpperBound_accepted()
        {
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 5));
            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(5, _store.LastPortions);
        }

        // ── 非主机分支:零 Append(传输半边 NOT-RUN,BLOCKED-BY-45)──────────────

        [Test]
        public void test_prescribe_clientContext_zeroAppend()
        {
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), isHost: false));

            Assert.IsTrue(outcome.Applied, "编排照跑(意图即效果)");
            Assert.AreEqual(0, _sink.Events.Count, "客户端零 Append");
            Assert.IsNull(outcome.TreatmentEvent, "客户端零事件回执");
            Assert.AreEqual(0, _skills.EmitCalls, "客户端零本地成长");
            Assert.AreEqual(0, _store.ConsumeCalls, "客户端零本地扣减(扣减权在主机)");
        }

        // ── AC-11-08 流程侧:F5 求值恰一次 ────────────────────────────────────

        [Test]
        public void test_prescribe_halfLifeEvaluatedOncePerEvent()
        {
            // step② 调 F-11.2 一次;载荷 half_life = 该次结果(无重算 —— 单点求值)
            _store.PeekQuality = 2;
            Run(Request(MakeProfile(new DoseRange(1, 5))));
            var payload = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];
            // Axis_base 131072 + offsets[1] = -16384 ⇒ 114688(品级由 20 的验货标量给出)
            Assert.AreEqual(114688L, payload.HalfLife);
        }

        // ── AC-11-10 流程侧:零第二份乘子(反射 + 源码面)──────────────────────

        [Test]
        public void test_prescribe_noSecondMultiplier_symbols()
        {
            // AC-11-10 流程侧:five 步内**零第二份乘子**。判据 = 本流程引入的类型 /
            // 其接缝端口 / 其载荷 scalars 里不存在 SkillMul / ResultMul / JudgeResult。
            // (11 的**程序集级**引用集断言由 story-003 的 AC-11-10 scoped 面承担;
            //  在 `Sim/` 全程序集上跑本断言会命中 **10 的** HostEmergencyProcessor —— 那不是 11 越权。)
            // ⚠️ 结构评审 m2:原稿比对的是**类型名**(对所列类型恒真)⇒ 改枚举 11 自己声明的
            //    全部**成员符号名**(类型 / 方法 / 属性 / 字段 / 参数)做差集,承 ADR-017 §二白名单机制。
            var forbidden = new[] { "SkillMul", "ResultMul", "JudgeResult" };
            var types = new List<Type> { typeof(PrescribeFlow), typeof(PrescribeRequest), typeof(PrescribeOutcome),
                                         typeof(PrescribePorts), typeof(PrescriptionEntry),
                                         typeof(IPortionsStore), typeof(IPortionsConversion),
                                         typeof(ISkillGrowthPort) };
            var symbols = new List<string>();
            foreach (var t in types)
            {
                symbols.Add(t.Name);
                const System.Reflection.BindingFlags Pub =
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly;
                foreach (var f in t.GetFields(Pub)) symbols.Add(f.Name);
                foreach (var pr in t.GetProperties(Pub)) symbols.Add(pr.Name);
                foreach (var m in t.GetMethods(Pub))
                {
                    symbols.Add(m.Name);
                    foreach (var pp in m.GetParameters()) symbols.Add(pp.Name);
                }
                foreach (var c in t.GetConstructors(Pub))
                    foreach (var pp in c.GetParameters()) symbols.Add(pp.Name);
            }
            foreach (var bad in forbidden)
                CollectionAssert.DoesNotContain(symbols, bad,
                    $"11 声明了禁用符号 `{bad}`(AC-11-10:零第二份乘子 / 零判定)");

            // 反向守卫:枚举面非空且确实覆盖到成员 —— 防「类型列表空 ⇒ 恒过」
            Assert.Greater(symbols.Count, 20, "符号枚举面异常小 ⇒ 断言面失效(恒真风险)");
        }

        [Test]
        public void test_prescribe_noMultiplierConsumption_inSource()
        {
            // 流程侧:五步内无判定分支、无乘子消费点(源码面 —— 与反射断言互补)。
            // ⚠️ 剥离注释后再判 —— 否则「11 不调 Judge」这句注释本身会命中。
            string src = StripComments(File.ReadAllText(Path.Combine(
                RepoRoot, "unity", "Assets", "Sim", "Prescription", "PrescribeFlow.cs")));

            Assert.IsFalse(src.Contains("ResultMul"), "11 名义路径不链入载荷(乘 1 是空运算)");
            Assert.IsFalse(src.Contains("SkillMul"), "SkillMul 在 11 无合法消费点(规则十一)");
            Assert.IsFalse(src.Contains("JudgeResult"), "11 无判定步");
        }

        /// <summary>剥离 <c>/* */</c> 块注释、<c>//</c> 行注释与字符串字面量(判据只针对代码面)。
        /// <para>⚠️ 结构评审 n4:同文件的另一条源码面断言(<c>PrescriptionFloatScan</c>)采用的是
        /// 「剥字符串、留注释(逐行跳过注释行)」—— 因其需报行号上下文。两条**判据面等价**
        /// (注释与字符串都不参与代码语义);本 helper 二者皆剥,判据更强(字符串里的
        /// <c>"new PayloadRef("</c> 也不再误命中)。</para></summary>
        private static string StripComments(string src)
        {
            src = System.Text.RegularExpressions.Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            src = System.Text.RegularExpressions.Regex.Replace(src, @"//[^\n]*", " ");
            return System.Text.RegularExpressions.Regex.Replace(src, "\"(@?)(\\\\.|[^\"\\\\])*\"", "\"\"");
        }

        [Test]
        public void test_prescribe_noDiseaseNameFields_inSignature()
        {
            // AC-11-01 反射判据(流程侧):**真的从 PrescribeRequest 的构造签名**取形参类型
            // (结构评审 m2:原稿手写类型数组 = 恒真断言)。
            var ctor = typeof(PrescribeRequest).GetConstructors()[0];
            foreach (var p in ctor.GetParameters())
            {
                Assert.AreNotEqual("DiseaseId", p.ParameterType.Name, $"PrescribeRequest.{p.Name}");
                Assert.AreNotEqual("DiagnosisResult", p.ParameterType.Name, $"PrescribeRequest.{p.Name}");
                Assert.AreNotEqual("DiseaseIdSet", p.ParameterType.Name, $"PrescribeRequest.{p.Name}");
                Assert.AreNotEqual("Severity", p.ParameterType.Name, $"PrescribeRequest.{p.Name}");
            }
            Assert.Greater(ctor.GetParameters().Length, 0, "构造签名面为空 ⇒ 断言恒真");

            // 源码面:treatable_by / disease_id / tier_named 零出现(剥离注释 —— 判据只针对代码)
            string src = StripComments(File.ReadAllText(Path.Combine(
                RepoRoot, "unity", "Assets", "Sim", "Prescription", "PrescribeFlow.cs")));
            Assert.IsFalse(src.Contains("treatable_by"), "11 不读 9 的 treatable_by(BL-3 改判)");
            Assert.IsFalse(src.Contains("tier_named"), "11 不读病种级布尔");
            Assert.IsFalse(src.Contains("disease_id"), "11 不读病名");
        }

        // ── 零浮点(AC-11-11① 全目录扫描,与 story-002/003 共享实现)───────────

        private static string RepoRoot => ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        [Test]
        public void test_prescribe_noFloat_staticScan()
        {
            string simDir = Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
            Assert.IsTrue(Directory.Exists(simDir),
                $"Sim/Prescription/ 目录不存在(AC-11-11① 静态扫描无处可跑): {simDir}");

            var violations = PrescriptionFloatScan.Scan(simDir);
            Assert.IsEmpty(violations,
                $"Sim/Prescription/ 源码含浮点类型/字面量(AC-11-11① 零浮点):\n{string.Join("\n", violations)}");
        }

        [Test]
        public void test_prescribe_noFloat_scanHasPositiveControl()
        {
            // ⚠️ QA M3:本测是**扫描器自身的正控** —— 没有它,若 `Scan` 被改成恒 `return new List()`,
            //    三处零浮点断言(story-002/003/004 共享)会**全部真空通过**。
            // 建临时目录写入已知违例,断言扫描器**能返回非空**(且 type 与 literal 两路都命中)。
            string tmp = Path.Combine(Path.GetTempPath(), "pfscan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                File.WriteAllText(Path.Combine(tmp, "mutant.cs"),
                    "class X {\n    double d = 1.5;   // 行尾注释里的 2.5 不该误报\n}\n");

                var hits = PrescriptionFloatScan.Scan(tmp);

                Assert.IsNotEmpty(hits, "扫描器对已知浮点片段返回空 ⇒ 扫描逻辑失效(真空绿)");
                Assert.IsTrue(hits.Exists(h => h.Contains("type")), $"未命中类型路: {string.Join(" | ", hits)}");
                Assert.IsTrue(hits.Exists(h => h.Contains("literal")), $"未命中原字面量路: {string.Join(" | ", hits)}");
            }
            finally
            {
                Directory.Delete(tmp, recursive: true);
            }
        }

        // ── ADR-029 §③:Sim/ 目录内零手搓 PayloadRef ─────────────────────────

        [Test]
        public void test_prescribe_noHandRolledPayloadRef()
        {
            // ⚠️ 扫描面 = **11 自己的面**(`Sim/Prescription/`)。
            // ADR-029 §③ 是**全 Sim 侧**纪律,但把本断言撒到全 `Sim/` 会命中 **10 的**
            // HostEmergencyProcessor —— 那是另一系统的越权,由它自己的 story 承担,不是 11 的判据。
            string simDir = Path.Combine(RepoRoot, "unity", "Assets", "Sim", "Prescription");
            Assert.IsTrue(Directory.Exists(simDir), $"扫描目录不存在: {simDir}");
            string[] files = Directory.GetFiles(simDir, "*.cs", SearchOption.AllDirectories);
            // ⚠️ QA m1:目录存在却扫到 0 文件(或 SearchOption 被改窄)会静默真空通过
            Assert.IsNotEmpty(files, "扫描面为 0 文件 ⇒ 断言真空通过");
            Assert.IsTrue(Array.Exists(files, f => Path.GetFileName(f) == "PrescribeFlow.cs"),
                "PrescribeFlow.cs 不在扫描集内 ⇒ 断言面错位");

            foreach (string file in files)
            {
                string src = StripComments(File.ReadAllText(file));
                Assert.IsFalse(src.Contains("new PayloadRef("),
                    $"{Path.GetFileName(file)} 手搓 PayloadRef —— ADR-029 §③ 唯一合法路径 = IPayloadEncoder");
            }

            // 正控:已知违例片段必须被抓(QA m1,同 M3 型)
            Assert.IsTrue(StripComments("var r = new PayloadRef(0, 0, 4);").Contains("new PayloadRef("),
                "剥离 helper 把判据 token 也剥掉了 ⇒ 本断言恒真");
            // QA m4:StripComments 须剥注释但**保留代码 token** —— 防「剥得越多越容易绿」的假绿
            Assert.IsFalse(StripComments("// new PayloadRef(0,0,0)\n").Contains("new PayloadRef("),
                "行注释未被剥离 ⇒ 注释里的引用会误报");
            Assert.IsFalse(StripComments("/* new PayloadRef(0,0,0) */\n").Contains("new PayloadRef("),
                "块注释未被剥离 ⇒ 误报");
            Assert.IsTrue(StripComments("int x = 1;\n").Contains("int x = 1;"),
                "剥离过度:把代码也剥掉了 ⇒ 否定式断言会假绿");
        }

        // ── QA M4:验货实参有界验证(原全用 PerDose = 1 ⇒ portions == dose,不可区分)──

        [Test]
        public void test_prescribe_hasPortions_receivesPortions_notDose()
        {
            // PerDose = 5,dose = 2 ⇒ portions = 10(与 dose = 2 可区分)
            _conversion.PerDose = 5;
            _store.Available = 10;
            var ok = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 2));

            Assert.IsTrue(ok.Applied, "恰有 10 份 ⇒ 接受");
            Assert.AreEqual(10, _store.LastHasPortions,
                "HasPortions 收 **portions**(10),不是 dose(2)—— 把实参写成 effectiveDose 会红");
        }

        [Test]
        public void test_prescribe_hasPortions_shortByOne_rejects()
        {
            _conversion.PerDose = 5;
            _store.Available = 9;                       // 差 1 份
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 2));

            Assert.IsFalse(outcome.Applied, "差 1 份 ⇒ 整体拒绝(无半剂)");
            Assert.AreEqual(0, _store.ConsumeCalls);
        }

        // ── 步骤② 换算:portions = dose × portions_per_dose ───────────────────

        [Test]
        public void test_prescribe_portionsConversion_applied()
        {
            // per-dose = 1 ⇒ 剂量 2 与 portions 2 不可区分;本测把两者拉开(PerDose = 5 ⇒ 10)
            // ⚠️ 乘法在 **11 侧**(结构评审 m1)—— 桩只回每剂份数,11 自己乘 dose。
            _conversion.PerDose = 5;
            Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 2));

            Assert.AreEqual(1, _conversion.Calls.Count, "换算表每流程恰查一次");
            Assert.AreEqual(10, _store.LastPortions, "portions = dose 2 × per-dose 5(乘法住 11)");
        }

        [Test]
        public void test_prescribe_portionsZero_notEntered()
        {
            // 下界 = 1 份(不得出现「给药不耗药」);换算表返回 0 ⇒ 无货路径拒绝
            _conversion.PerDose = 0;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5))));

            Assert.IsFalse(outcome.Applied);
            Assert.AreEqual(0, _sink.Events.Count);
        }

        [Test]
        public void test_prescribe_portionsOverflow_notEntered()
        {
            // 11 侧乘法(int × int)引入的新分支:宽算后超 int 域 ⇒ 拒绝,不静默回绕
            _conversion.PerDose = int.MaxValue;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 5));

            Assert.IsFalse(outcome.Applied, "乘法溢出 ⇒ 整体拒绝(装配 / 数据错误)");
            Assert.AreEqual(0, _sink.Events.Count);
            Assert.AreEqual(0, _store.ConsumeCalls);
        }

        // ── QA m3:空值守卫负测 ──────────────────────────────────────────────

        [Test]
        public void test_prescribe_nullPorts_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PrescribeFlow.Prescribe(Request(MakeProfile(new DoseRange(1, 5))), 100L, null, _sink, _encoder));
        }

        [Test]
        public void test_prescribe_nullSink_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PrescribeFlow.Prescribe(Request(MakeProfile(new DoseRange(1, 5))), 100L, _ports, null, _encoder));
        }

        [Test]
        public void test_prescribe_nullEncoder_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PrescribeFlow.Prescribe(Request(MakeProfile(new DoseRange(1, 5))), 100L, _ports, _sink, null));
        }

        [Test]
        public void test_gateHit_nullSkills_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PrescribeFlow.EvaluateGateHit(new DoseRange(1, 5), 2, Entry(), 7, null));
        }

        [Test]
        public void test_ports_nullPortSeam_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new PrescribePorts(null, _store, _presence, _skills, DoseBase));
            Assert.Throws<ArgumentNullException>(() => new PrescribePorts(_conversion, null, _presence, _skills, DoseBase));
            Assert.Throws<ArgumentNullException>(() => new PrescribePorts(_conversion, _store, null, _skills, DoseBase));
            Assert.Throws<ArgumentNullException>(() => new PrescribePorts(_conversion, _store, _presence, null, DoseBase));
        }

        // ── 装配期 fail-fast(结构评审 m4)────────────────────────────────────

        [Test]
        public void test_ports_zeroDoseBase_throwsAtAssembly()
        {
            // doseBase = 0 是**装配错误**,须在注入期硬失败 —— 否则会被推迟到步骤⑤
            // (DeriveKDifficulty),此时事件已进流,装配错误伪装成「缺参不回滚」。
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PrescribePorts(_conversion, _store, _presence, _skills, doseBase: 0));
        }

        [Test]
        public void test_ports_negativeDoseBase_throwsAtAssembly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PrescribePorts(_conversion, _store, _presence, _skills, doseBase: -1));
        }

        // ── DeriveKDifficulty(OQ-11-13 已裁:来源域 = 11 自表,不读 severity)────

        [Test]
        public void test_kDifficulty_derivedFromDrugPotency()
        {
            // |drug_potency| ÷ DOSE_BASE = (2.0) ÷ (1.0) = 2.0(raw 131072)——
            // 域保持:2.0 除以 1.0 仍是 2.0,不是裸计数 2
            var profile = MakeProfile(null, potencyRaw: 131072L);
            Fix k = PrescribeFlow.DeriveKDifficulty(profile, DoseBase);
            Assert.AreEqual(131072L, k.Raw);
        }

        [Test]
        public void test_kDifficulty_negativePotency_usesMagnitude()
        {
            var profile = MakeProfile(null, potencyRaw: -131072L);
            Fix k = PrescribeFlow.DeriveKDifficulty(profile, DoseBase);
            Assert.AreEqual(131072L, k.Raw, "难度取量级,与极性无关");
        }

        [Test]
        public void test_kDifficulty_missingPotency_throws()
        {
            var profile = MakeProfile(null);
            profile.DrugPotency = null;
            Assert.Throws<InvalidOperationException>(() => PrescribeFlow.DeriveKDifficulty(profile, DoseBase));
        }

        [Test]
        public void test_kDifficulty_nonPositiveDoseBase_throws()
        {
            var profile = MakeProfile(null);
            Assert.Throws<ArgumentOutOfRangeException>(() => PrescribeFlow.DeriveKDifficulty(profile, 0));
        }

        [Test]
        public void test_kDifficulty_doesNotReadSeverity()
        {
            // 判据 = 反射断言(非 grep),承 AC-11-01 / AC-11-14 的**递归**反射先例。
            // ⚠️ 结构评审 m3:原稿只扫**形参类型名**(弱)。现改为:
            //   ① 形参 + 返回类型递归可达的**全部类型名**(含泛型实参 / 数组元素);
            //   ② 源码面(剥离注释)零 severity / disease 标识符 —— 间接引用也拦得住。
            var m = typeof(PrescribeFlow).GetMethod("DeriveKDifficulty");
            Assert.IsNotNull(m);

            var reachable = new List<string>();
            CollectTypeNames(m.ReturnType, reachable);
            foreach (var p in m.GetParameters())
            {
                reachable.Add(p.Name);                       // 形参名也是符号面
                CollectTypeNames(p.ParameterType, reachable);
            }
            foreach (var bad in new[] { "DiseaseId", "Severity", "DiagnosisResult", "DiseaseIdSet" })
                CollectionAssert.DoesNotContain(reachable, bad, $"DeriveKDifficulty 触及 `{bad}`");
            Assert.Greater(reachable.Count, 0, "反射面为空 ⇒ 断言恒真");

            string src = StripComments(File.ReadAllText(Path.Combine(
                RepoRoot, "unity", "Assets", "Sim", "Prescription", "PrescribeFlow.cs")));
            Assert.IsFalse(src.Contains("severity"), "11 不读 severity(OQ-11-13:它是病种级量)");
            Assert.IsFalse(src.Contains("Severity"), "11 不读 Severity");
        }

        /// <summary>递归收集类型名(含数组元素 / 泛型实参)—— 承 AC-11-14 的递归反射先例。</summary>
        private static void CollectTypeNames(Type t, List<string> into)
        {
            if (t == null) return;
            into.Add(t.Name);
            if (t.HasElementType) CollectTypeNames(t.GetElementType(), into);
            if (t.IsGenericType)
                foreach (var a in t.GetGenericArguments()) CollectTypeNames(a, into);
        }

        // ── 恒 Applied:无手部门槛分支(与 10 的对照)──────────────────────────

        [Test]
        public void test_prescribe_zeroSkillPlayer_stillApplied()
        {
            // 零技艺玩家 / 极小剂量 ⇒ 仍恰一条事件,无 AppliedWeak / Missed 概念
            _skills.Level = 0;
            var outcome = Run(Request(MakeProfile(new DoseRange(1, 5)), dose: 1));

            Assert.IsTrue(outcome.Applied);
            Assert.AreEqual(1, _sink.Events.Count);
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _encoder.Kinds[0]);
        }

        [Test]
        public void test_prescribe_payloadSevenFields_encoded()
        {
            // AC-11-03 七项齐备(流程侧):编码器收件 = 具名 Kind + 七字段载荷
            _store.PeekQuality = 2;
            // drug_potency = 1.0 × DOSE_BASE,dose = 1 ⇒ dose_potency = drug_potency = 1.0(可读的干净值)
            Run(Request(MakeProfile(new DoseRange(1, 5), potencyRaw: 65536L * DoseBase), dose: 1), tick: 321L);

            Assert.AreEqual(EventKind.DrugTreatmentApplied, _encoder.Kinds[0]);
            var payload = (DrugTreatmentAppliedPayload)_encoder.Payloads[0];
            Assert.AreEqual(321L, payload.Tick);
            Assert.AreEqual(1, payload.TreatmentId);
            Assert.AreEqual(7, payload.ActorId);
            Assert.AreEqual((int)PrescriptionPolarity.Symptomatic, payload.Polarity);
            Assert.AreEqual(65536L, payload.DrugPotency.Raw, "= 1.0 Q16.16(F-11.1: potency × dose / DOSE_BASE)");
            Assert.AreEqual(114688L, payload.HalfLife);
            Assert.AreEqual(0L, payload.Seq, "载荷 Seq = 占位(header Seq 由主机 Append 发号;归 45 / 7a)");
        }

        [Test]
        public void test_prescribe_eventPatientIsTarget()
        {
            // 处置事件挂在病人身上(全序键 (Tick, Patient) 的成员)
            Run(Request(MakeProfile(new DoseRange(1, 5))));
            Assert.AreEqual(3, _sink.Events[0].Patient.Value);
            Assert.AreEqual(EventKind.DrugTreatmentApplied, _sink.Events[0].Kind);
        }
    }
}
