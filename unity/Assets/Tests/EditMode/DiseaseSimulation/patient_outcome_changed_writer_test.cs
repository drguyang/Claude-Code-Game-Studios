// 病程终态写者(`PatientOutcomeChanged`)端到端测试(EditMode,真装配,禁 Fake)。
//
// 覆盖:
//   ① 真装配袋(真 EventStream + 真 PayloadEncoder + 真 PayloadDecoder + 真 blob 池)调用写者
//      ⇒ **病史流恰增 1 条**,Kind / 流别 / 载荷逐字段 round-trip / 事件头 Patient / Seq 发号逐项断言;
//   ② **九态映射**(本类最关键的判据):只有 `Recovered` / `Deceased` 能写出;
//      其余七态(Healthy / Mild / Moderate / Severe / Critical / Coma / Chronic)**全部拒收**;
//   ③ 病人 id 非法(`PatientId.None`)⇒ 零写入;
//   ④ `diseaseId` 为负 ⇒ 零写入;
//   ⑤ `tick` 为负 ⇒ fail-loud(编程错误,非业务拒收);
//   ⑥ 注入面 fail-loud(null 依赖);
//   ⑦ 终态映射与 `PatientOutcome` 枚举**逐值对齐**(三处一致:registry 散文 / 枚举 / 本映射)。
//
// 权威:design/registry/entities.yaml:3264-3278 SimEvent.Kind.PatientOutcomeChanged
//      (stream: history · payload_schema 四字段 · 「outcome ∈ {Recovered=1, Died=2}」·
//       「t_end 不进载荷」)· docs/architecture/adr-032-patient-outcome-kind.md ·
//      design/gdd/persistence-service.md:316(Folded 条件 ①)· ADR-005 / ADR-029 §③
//
// ⚠️ 本文件**不**验证「终态折叠可用」—— 写者无生产调用方(有写者 ≠ 已接线),
//   且接线还须先裁「`DiseaseVitalsService` 不写流 vs 终态落流」这条冲突(见写者类头注)。

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary><see cref="PatientOutcomeChangedWriter"/> 生产写入通道测试。</summary>
    [TestFixture]
    public sealed class PatientOutcomeChangedWriterTest
    {
        private CompositionRootServices _bag;
        private PatientOutcomeChangedWriter _writer;
        private PatientId _patient;

        private const int DiseaseId = 3;   // 病种 id(值本身不承载语义,只须 ≥ 0 且与注册表无关 —— 本写者不查表)

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new PatientOutcomeChangedWriter(_bag.EventSink, _bag.Encoder);
            // 病人:9 创建 + 组合层灌在场(EventStream.Append 的 AC-15 在场检查依赖它)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
        }

        private int HistoryOutcomeCount()
            => _bag.Stream.Events.Count(
                e => e.Kind == EventKind.PatientOutcomeChanged
                     && StreamRouting.Of(e.Kind) == StreamId.History);

        // ══════════ ① 正常写入一条 + 逐字段 round-trip ══════════

        [Test]
        public void test_patientOutcome_recovered_appendsExactlyOneHistoryEvent_roundTrip()
        {
            // arrange
            const long tick = 1200;
            int beforeAll = _bag.Stream.Count;
            int beforeOutcome = HistoryOutcomeCount();

            // act
            var result = _writer.TryWrite(tick, _patient, DiseaseId, InjuryState.Recovered);

            // assert —— 结果码 + 恰增 1 条 + 落病史流
            Assert.AreEqual(PatientOutcomeWriteResult.Written, result, "痊愈须真正写入");
            Assert.AreEqual(beforeAll + 1, _bag.Stream.Count, "真流总事件数恰增 1");
            Assert.AreEqual(beforeOutcome + 1, HistoryOutcomeCount(), "病史流 PatientOutcomeChanged 恰增 1 条");

            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.PatientOutcomeChanged, evt.Kind, "Kind = PatientOutcomeChanged");
            Assert.AreEqual(StreamId.History, StreamRouting.Of(evt.Kind),
                "落病史流(registry: stream: history)");

            // assert —— 事件头:Patient 正确 + Tick 即 t_end + Seq 发号
            Assert.AreEqual(_patient, evt.Patient,
                "事件头 Patient = 病人(非 None —— 折叠与全序键都依赖它)");
            Assert.AreEqual(tick, evt.Tick,
                "事件头 Tick = 判出终态的 tick(registry:**t_end 不进载荷**,它就是头部 Tick)");
            Assert.AreEqual(0, evt.Seq, "Seq 由发号器给出(该 (Tick, Patient) 首号 = 0)");

            // assert —— 载荷逐字段 round-trip(真字节经池取回)
            var decoded = _bag.Decoder.Decode<PatientOutcomeChangedPayload>(
                EventKind.PatientOutcomeChanged, evt.Payload);
            Assert.AreEqual(_patient.Value, decoded.PatientId, "payload.patient_id(IIdAuthority 机制 A)");
            Assert.AreEqual(DiseaseId, decoded.DiseaseId, "payload.disease_id(argmax d)");
            Assert.AreEqual((int)PatientOutcome.Recovered, decoded.Outcome, "payload.outcome = Recovered(1)");
        }

        [Test]
        public void test_patientOutcome_deceased_appendsOutcomeDied()
        {
            // arrange
            int before = HistoryOutcomeCount();

            // act
            var result = _writer.TryWrite(2400, _patient, DiseaseId, InjuryState.Deceased);

            // assert
            Assert.AreEqual(PatientOutcomeWriteResult.Written, result, "死亡须真正写入");
            Assert.AreEqual(before + 1, HistoryOutcomeCount(), "病史流恰增 1 条");

            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            var decoded = _bag.Decoder.Decode<PatientOutcomeChangedPayload>(
                EventKind.PatientOutcomeChanged, evt.Payload);
            Assert.AreEqual((int)PatientOutcome.Died, decoded.Outcome,
                "九态 Deceased ⇒ PatientOutcome.Died(2)");
        }

        // ══════════ ② 九态映射:非终态一律拒收(本类最关键的判据)══════════

        /// <summary>
        /// <b>七种非终态全部拒收,零写入。</b>
        /// <para>这是本类存在的理由:若放行,「昏迷 / 危殆 / 轻症」会被写成终态 ⇒
        /// <c>Folded(p)</c> 条件 ① 假真 ⇒ 病人被错误折叠,**且无门可扫**(流里确实有一条
        /// 「终态」事件)。</para>
        /// </summary>
        [Test]
        public void test_patientOutcome_nonTerminalStates_noWrite()
        {
            // arrange —— 枚举里除 Recovered / Deceased 之外的**全部**九态
            var nonTerminal = new[]
            {
                InjuryState.Healthy,
                InjuryState.Mild,
                InjuryState.Moderate,
                InjuryState.Severe,
                InjuryState.Critical,
                InjuryState.Coma,
                InjuryState.Chronic,
            };

            int before = _bag.Stream.Count;

            // act + assert —— 逐一拒收
            foreach (InjuryState state in nonTerminal)
            {
                var result = _writer.TryWrite(100, _patient, DiseaseId, state);
                Assert.AreEqual(PatientOutcomeWriteResult.NotTerminal, result,
                    $"{state} 不是终态,须拒收(写成终态会让 Folded(p) 条件 ① 假真)");
            }

            // assert —— 七条全部零写入
            Assert.AreEqual(before, _bag.Stream.Count, "非终态一律零写入");
            Assert.AreEqual(0, HistoryOutcomeCount(), "病史流不得出现 PatientOutcomeChanged");
        }

        /// <summary>
        /// 覆盖完备性:上面的拒收集合 + 两个终态 = <see cref="InjuryState"/> 的**全部**成员。
        /// <para>⚠️ 缺这条,将来往枚举里加第九态时,新态会**默认落进 NotTerminal** 而无人发现
        /// —— 那是安全方向,但「新增终态被漏掉」也是可能的 ⇒ 本测让新增成员必须被显式归类。</para>
        /// </summary>
        [Test]
        public void test_patientOutcome_terminalSet_partitionsInjuryStateExactly()
        {
            var all = (InjuryState[])Enum.GetValues(typeof(InjuryState));

            var accepted = new System.Collections.Generic.List<InjuryState>();
            foreach (InjuryState state in all)
            {
                var r = _writer.TryWrite(50, _patient, DiseaseId, state);
                if (r == PatientOutcomeWriteResult.Written) accepted.Add(state);
            }

            CollectionAssert.AreEquivalent(
                new[] { InjuryState.Recovered, InjuryState.Deceased }, accepted,
                "终态集合须恰为 {Recovered, Deceased} —— 枚举新增成员时必须在此显式归类");
        }

        // ══════════ ③ 病人 id 非法 ⇒ 零写入 ══════════

        [Test]
        public void test_patientOutcome_noneSentinel_noWrite()
        {
            // arrange
            int before = _bag.Stream.Count;

            // act
            var result = _writer.TryWrite(100, PatientId.None, DiseaseId, InjuryState.Recovered);

            // assert
            Assert.AreEqual(PatientOutcomeWriteResult.NoPatient, result,
                "哨兵不得入病史流(污染 max(patient_id) 高水位)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ④ diseaseId 为负 ⇒ 零写入 ══════════

        [Test]
        public void test_patientOutcome_negativeDiseaseId_noWrite()
        {
            // arrange
            int before = _bag.Stream.Count;

            // act
            var result = _writer.TryWrite(100, _patient, -1, InjuryState.Recovered);

            // assert
            Assert.AreEqual(PatientOutcomeWriteResult.InvalidDiseaseId, result,
                "disease_id < 0 只可能来自调用方编码错误");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ⑤ tick 为负 ⇒ fail-loud(编程错误,非业务拒收)══════════

        [Test]
        public void test_patientOutcome_negativeTick_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.TryWrite(-1, _patient, DiseaseId, InjuryState.Recovered),
                "tick < 0 = 编程错误(与 CareAppliedWriter / PlayerDiedWriter 同口径)");
            Assert.AreEqual(0, HistoryOutcomeCount(), "抛出不产生任何写入");
        }

        // ══════════ ⑥ 注入面 fail-loud ══════════

        [Test]
        public void test_patientOutcome_nullDependencies_throw()
        {
            Assert.Throws<ArgumentNullException>(
                () => new PatientOutcomeChangedWriter(null, _bag.Encoder), "eventSink 为 null 须抛");
            Assert.Throws<ArgumentNullException>(
                () => new PatientOutcomeChangedWriter(_bag.EventSink, null), "encoder 为 null 须抛");
        }

        // ══════════ ⑦ 映射与枚举逐值对齐(三处一致)═════════

        /// <summary>
        /// registry 散文写 <c>Recovered=1 / Died=2</c>;本写者的映射须与
        /// <see cref="PatientOutcome"/> 的枚举值**逐值相同** —— 否则「写出去的字节」与
        /// 「registry 声明的语义」分叉,而 codec 的解码期校验只认 {1,2} 这个**数值域**,
        /// 不认名字 ⇒ 分叉会静默通过。
        /// </summary>
        [Test]
        public void test_patientOutcome_mappingMatchesEnumOrdinals()
        {
            Assert.AreEqual(1, (int)PatientOutcome.Recovered,
                "registry:3269 写 Recovered=1");
            Assert.AreEqual(2, (int)PatientOutcome.Died,
                "registry:3269 写 Died=2");
            Assert.AreEqual(0, (int)PatientOutcome.Unspecified,
                "0 = 漏填哨兵(解码 mask 校验拒收)");

            // 端到端再钉一次:写出去的字节解回来必须恰是这两个数值
            _writer.TryWrite(10, _patient, DiseaseId, InjuryState.Recovered);
            var evtR = _bag.Stream.Events[_bag.Stream.Count - 1];
            var pR = _bag.Decoder.Decode<PatientOutcomeChangedPayload>(
                EventKind.PatientOutcomeChanged, evtR.Payload);
            Assert.AreEqual(1, pR.Outcome, "Recovered 写出的 outcome 值须恰为 1");

            _writer.TryWrite(20, _patient, DiseaseId, InjuryState.Deceased);
            var evtD = _bag.Stream.Events[_bag.Stream.Count - 1];
            var pD = _bag.Decoder.Decode<PatientOutcomeChangedPayload>(
                EventKind.PatientOutcomeChanged, evtD.Payload);
            Assert.AreEqual(2, pD.Outcome, "Deceased 写出的 outcome 值须恰为 2");
        }
    }
}
