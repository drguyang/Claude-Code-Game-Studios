// 照护事件写者(`CareApplied`)端到端测试(EditMode,真装配,禁 Fake)。
//
// 覆盖:
//   ① 真装配袋(真 EventStream + 真 PayloadEncoder + 真 PayloadDecoder + 真 blob 池)调用写者
//      ⇒ **病史流恰增 1 条**,Kind / 流别 / 载荷逐字段 round-trip / 事件头 Patient / Seq 发号逐项断言;
//   ② `phase` 越界(2 / -1 / 99)⇒ 零写入 + 具名结果码(不静默取值 —— 静默会让「止」被当成「起」);
//   ③ 照护对象非法(PatientId.None)⇒ 零写入;
//   ④ actionId / caregiver 为负 ⇒ 零写入;
//   ⑤ 「止」无可配对的「起」⇒ 零写入;先「起」后「止」⇒ 两条,且 tick 非降(GDD :344);
//   ⑥ 密度界 `CARE_EVENTS_PER_TICK` 生效时超界拒收(GDD :1784「超界拒收,不静默」);
//   ⑦ 注入面 fail-loud(null 依赖 / 非法上界);
//   ⑧ **phase 序数三处一致**(2026-10-22 用户裁定「甲+乙 都做」):
//      `entities.yaml` 的散文序数 == `Sim.Contracts.CarePhase` == `DiseaseCourse.CarePhaseStart/Stop`。
//
// 权威:design/registry/entities.yaml SimEvent.Kind.CareApplied(stream: history ·
//      payload_schema 四字段 · `phase: 枚举(起=0, 止=1)` · 「写入期校验 t(止) ≥ t(起)」)·
//      design/gdd/disease-simulation.md:207-228(照护事件化)/ :344(校验 13 ②)/ :1784(密度界)·
//      ADR-005(Seq 发号)· ADR-029 §③ · ADR-031 §②
//
// ⚠️ 本文件**不**验证「照护功能可用」—— 写者无生产调用方(有写者 ≠ 已接线),
//   照护在游戏内的触发点归 24/4 的交互装配轮。

using System;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;   // CarePhaseStart / CarePhaseStop(序数唯一出处)
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Sim
{
    /// <summary><see cref="CareAppliedWriter"/> 生产写入通道测试。</summary>
    [TestFixture]
    public sealed class CareAppliedWriterTest
    {
        private CompositionRootServices _bag;
        private CareAppliedWriter _writer;
        private PatientId _patient;

        private const int ActionId = 3;      // 护理动作表 ordinal(表体未成文 ⇒ 值本身不承载语义)
        private const int Caregiver = 7;     // 照护者 id(合法域无定义 ⇒ 只须 ≥ 0)

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new CareAppliedWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            // 病人:9 创建 + 组合层灌在场(EventStream.Append 的 AC-15 在场检查依赖它)
            _patient = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: 0);
            _bag.Presence.AddPresent(_patient, new WorldPos(0, 0, 0));
        }

        private int HistoryCareCount()
            => _bag.Stream.Events.Count(
                e => e.Kind == EventKind.CareApplied && StreamRouting.Of(e.Kind) == StreamId.History);

        // ══════════ ① 正常写入一条 + 逐字段 round-trip ══════════

        [Test]
        public void test_careApplied_realBag_appendsExactlyOneHistoryEvent_roundTrip()
        {
            // arrange
            const long tick = 12;
            int beforeAll = _bag.Stream.Count;
            int beforeCare = HistoryCareCount();

            // act
            var result = _writer.TryApply(tick, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStart);

            // assert —— 结果码 + 恰增 1 条 + 落病史流
            Assert.AreEqual(CareAppliedWriteResult.Applied, result, "首次「起」须真正写入");
            Assert.AreEqual(beforeAll + 1, _bag.Stream.Count, "真流总事件数恰增 1");
            Assert.AreEqual(beforeCare + 1, HistoryCareCount(), "病史流 CareApplied 恰增 1 条");

            var evt = _bag.Stream.Events[_bag.Stream.Count - 1];
            Assert.AreEqual(EventKind.CareApplied, evt.Kind, "Kind = CareApplied");
            Assert.AreEqual(StreamId.History, StreamRouting.Of(evt.Kind), "落病史流(registry: stream: history)");

            // assert —— 事件头:Patient 正确 + Seq 发号
            Assert.AreEqual(_patient, evt.Patient,
                "事件头 Patient = 照护对象(非 None —— 折叠与全序键都依赖它)");
            Assert.AreEqual(tick, evt.Tick, "事件头 Tick = 护理动作 tick");
            Assert.AreEqual(0, evt.Seq, "Seq 由发号器给出(该 (Tick, Patient) 首号 = 0)");

            // assert —— 载荷逐字段 round-trip(真字节经池取回)
            var decoded = _bag.Decoder.Decode<CareAppliedPayload>(EventKind.CareApplied, evt.Payload);
            Assert.AreEqual(tick, decoded.Tick, "payload.tick(registry 列在 payload_schema 内)");
            Assert.AreEqual(ActionId, decoded.ActionId, "payload.action_id");
            Assert.AreEqual(Caregiver, decoded.Caregiver, "payload.caregiver");
            Assert.AreEqual(DiseaseCourse.CarePhaseStart, decoded.Phase, "payload.phase = 起(0)");
        }

        // ══════════ ② phase 越界 ⇒ 零写入(不得静默取值)══════════

        [Test]
        public void test_careApplied_phaseOutOfRange_noWrite()
        {
            // arrange
            int before = _bag.Stream.Count;

            // act + assert —— 2 / -1 / 99 逐一
            foreach (int badPhase in new[] { 2, -1, 99 })
            {
                var result = _writer.TryApply(12, _patient, ActionId, Caregiver, badPhase);
                Assert.AreEqual(CareAppliedWriteResult.InvalidPhase, result,
                    $"phase={badPhase} 越界须拒(0 = 起 / 1 = 止;静默取值会让「止」被当成「起」)");
            }

            // assert —— 三条越界均零写入
            Assert.AreEqual(before, _bag.Stream.Count, "越界 phase 零写入");
            Assert.AreEqual(0, HistoryCareCount(), "病史流不得出现 CareApplied");
        }

        // ══════════ ③ 照护对象非法 ⇒ 零写入 ══════════

        [Test]
        public void test_careApplied_noneSentinel_noWrite()
        {
            // arrange
            int before = _bag.Stream.Count;

            // act
            var result = _writer.TryApply(12, PatientId.None, ActionId, Caregiver,
                DiseaseCourse.CarePhaseStart);

            // assert
            Assert.AreEqual(CareAppliedWriteResult.NoPatient, result,
                "哨兵不得入病史流(污染 max(patient_id) 高水位)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");
        }

        // ══════════ ④ actionId / caregiver 为负 ⇒ 零写入 ══════════

        [Test]
        public void test_careApplied_negativeActionOrCaregiver_noWrite()
        {
            // arrange
            int before = _bag.Stream.Count;

            // act
            var badAction = _writer.TryApply(12, _patient, -1, Caregiver, DiseaseCourse.CarePhaseStart);
            var badCaregiver = _writer.TryApply(12, _patient, ActionId, -5, DiseaseCourse.CarePhaseStart);

            // assert
            Assert.AreEqual(CareAppliedWriteResult.InvalidActionId, badAction, "action_id < 0 须拒");
            Assert.AreEqual(CareAppliedWriteResult.InvalidCaregiver, badCaregiver, "caregiver < 0 须拒");
            Assert.AreEqual(before, _bag.Stream.Count, "两条均零写入");
        }

        // ══════════ ⑤ 起止配对(GDD :344 t(止) ≥ t(起))══════════

        [Test]
        public void test_careApplied_stopWithoutStart_noWrite()
        {
            // arrange —— 流里没有任何「起」
            int before = _bag.Stream.Count;

            // act
            var result = _writer.TryApply(20, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStop);

            // assert
            Assert.AreEqual(CareAppliedWriteResult.StopWithoutStart, result,
                "「止」无「起」可配 ⇒ t(止) ≥ t(起) 无从求值,按拒收处理(不猜)");
            Assert.AreEqual(before, _bag.Stream.Count, "零写入");

            // ⚠️ 本条钉的是**当前裁定**,而该裁定依赖一个未裁决前提(24 一定先发「起」)。
            //    「重连补记 / 玩家中途离场」两场景见 clinic-machine.md 的 OQ-24-8
            //    (2026-10-22 用户裁定「先不裁,我去查 24 的生命周期」)。
            //    若 OQ-24-8 裁出「允许只发止」⇒ **本条须同批改**,不是删掉而是换成放行后的断言。
        }

        [Test]
        public void test_careApplied_startThenStop_appendsTwo_tickNonDecreasing()
        {
            // arrange
            int before = _bag.Stream.Count;

            // act
            var start = _writer.TryApply(30, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStart);
            var stop = _writer.TryApply(35, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStop);

            // assert —— 两条都写 + tick 非降 + 相位可区分
            Assert.AreEqual(CareAppliedWriteResult.Applied, start, "「起」须写入");
            Assert.AreEqual(CareAppliedWriteResult.Applied, stop, "有「起」之后的「止」须写入");
            Assert.AreEqual(before + 2, _bag.Stream.Count, "恰增 2 条");

            var careEvents = _bag.Stream.Events
                .Where(e => e.Kind == EventKind.CareApplied).ToList();
            Assert.AreEqual(2, careEvents.Count, "病史流两条 CareApplied");
            Assert.LessOrEqual(careEvents[0].Tick, careEvents[1].Tick, "t(止) ≥ t(起)");
            var phases = careEvents
                .Select(e => _bag.Decoder.Decode<CareAppliedPayload>(EventKind.CareApplied, e.Payload).Phase)
                .ToList();
            CollectionAssert.AreEqual(
                new[] { DiseaseCourse.CarePhaseStart, DiseaseCourse.CarePhaseStop }, phases,
                "流上可判别起 / 止(照护支的派生量依赖它)");
        }

        // ══════════ ⑥ 密度界 CARE_EVENTS_PER_TICK(GDD :1784)══════════

        [Test]
        public void test_careApplied_densityCapExceeded_noWrite()
        {
            // arrange —— 上界 = 1(值仍 *待定*,此处按装配注入;真值归数值轮)
            var capped = new CareAppliedWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events,
                careEventsPerTickCap: 1);
            Assert.AreEqual(CareAppliedWriteResult.Applied,
                capped.TryApply(40, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStart),
                "前置:同 tick 第 1 条在界内");
            int afterFirst = _bag.Stream.Count;

            // act —— 同 tick 第 2 条
            var second = capped.TryApply(40, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStart);

            // assert
            Assert.AreEqual(CareAppliedWriteResult.RateCapExceeded, second,
                "同 (tick, 病人) 条数达上界 ⇒ 拒收(不静默丢弃)");
            Assert.AreEqual(afterFirst, _bag.Stream.Count, "零写入");

            // 对照:未标定哨兵 = 不设界(同 tick 再来一条应成功)
            Assert.AreEqual(CareAppliedWriteResult.Applied,
                _writer.TryApply(41, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStart),
                "NoDensityCap 下不设密度界(值 *待定* 时行为显式)");
        }

        // ══════════ ⑦ 注入面 fail-loud ══════════

        [Test]
        public void test_careAppliedWriter_nullDependency_throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CareAppliedWriter(null, _bag.Encoder, _bag.Decoder, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CareAppliedWriter(_bag.EventSink, null, _bag.Decoder, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CareAppliedWriter(_bag.EventSink, _bag.Encoder, null, _bag.Stream.Events));
            Assert.Throws<ArgumentNullException>(
                () => new CareAppliedWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, null));
        }

        [Test]
        public void test_careAppliedWriter_invalidCap_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CareAppliedWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder,
                    _bag.Stream.Events, careEventsPerTickCap: -2),
                "上界只接受 ≥ 0 或 NoDensityCap(-1)哨兵 —— 其余负值是编程错误");
        }

        [Test]
        public void test_careApplied_negativeTick_throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _writer.TryApply(-1, _patient, ActionId, Caregiver, DiseaseCourse.CarePhaseStart),
                "tick 入参非法 = 编程错误,fail-loud(与 CaseOpenWriter 同口径)");
        }

        // ══════════ ⑧ phase 序数三处一致(2026-10-22「甲+乙 都做」)══════════
        //
        // ⚠️ 这条钉的是**三处**的一致性,不是单点:
        //   ① `Sim.Contracts.CarePhase`(唯一 C# 定义)
        //   ② `DiseaseCourse.CarePhaseStart/Stop`(转发,须等于 ①)
        //   ③ `entities.yaml` 的散文序数(权威件,须等于 ①)
        // 只测 ①② 会漏掉「registry 改了但代码没改」;只测 ①③ 会漏掉「转发常量被改成硬编码」。
        // 三者全测才能说「序数不可能分叉」。

        /// <summary>①②:C# 侧 —— 枚举值正确 + 转发常量**真的是转发**(源码级,非仅值相等)。</summary>
        /// <remarks>
        /// <para><b>⚠️ 本测试曾有过一个假判据(2026-10-22 证伪轮实测发现)</b>:原实现只断言
        /// <c>DiseaseCourse.CarePhaseStart == (int)CarePhase.Start</c>,而**值相等抓不到「自成硬编码」**
        /// —— 把 <c>CarePhaseStart</c> 改回 <c>const int = 0;</c>(值不变、只是不再转发)时,
        /// 原断言**全绿通过**。也就是说原注释写的「不得自成一份硬编码」是**注释超出判据能力**。</para>
        /// <para><b>现判据分两层</b>:① 值层(枚举值 + 起止不同值,运行时);
        /// ② **源码层**(<c>DiseaseCourse.cs</c> 里两条 <c>const</c> 的右值必须形如
        /// <c>(int)CarePhase.X</c>,且**剥掉注释行**再判 —— 否则 doc 里保留的原文
        /// <c>public const int CarePhaseStart = 0;</c> 会误伤)。</para>
        /// <para>⚠️ 源码层判据是**文本级**,不是语义级 —— 它抓不住「用别名绕过」这类写法。
        /// 但对本场景足够:序数分叉的实际形态就是「有人把转发改回字面量」。</para>
        /// </remarks>
        [Test]
        public void test_carePhase_enumMatchesForwardingConstants()
        {
            // ① 值层
            Assert.AreEqual(0, (int)CarePhase.Start, "CarePhase.Start 须 = 0");
            Assert.AreEqual(1, (int)CarePhase.Stop, "CarePhase.Stop 须 = 1");
            Assert.AreEqual((int)CarePhase.Start, DiseaseCourse.CarePhaseStart,
                "DiseaseCourse.CarePhaseStart 的值须等于 CarePhase.Start");
            Assert.AreEqual((int)CarePhase.Stop, DiseaseCourse.CarePhaseStop,
                "DiseaseCourse.CarePhaseStop 的值须等于 CarePhase.Stop");
            Assert.AreNotEqual(DiseaseCourse.CarePhaseStart, DiseaseCourse.CarePhaseStop,
                "起 / 止 不得同值 —— 同值会让「止」被读成「起」,照护支永不中止");

            // ② 源码层:两条 const 必须是转发,不是字面量
            string coursePath = System.IO.Path.Combine(
                TestRepoRoot(), "unity", "Assets", "Sim", "DiseaseSimulation", "DiseaseCourse.cs");
            Assert.IsTrue(System.IO.File.Exists(coursePath), $"DiseaseCourse.cs 不存在:{coursePath}");

            var declarations = new System.Collections.Generic.List<string>();
            foreach (string raw in System.IO.File.ReadAllLines(coursePath))
            {
                string line = raw.Trim();
                // 剥注释 —— doc 注释里保留了原文 `= 0;`,不剥会误判
                if (line.StartsWith("//") || line.StartsWith("*") || line.StartsWith("/*")) continue;
                if (!line.Contains("const int CarePhase")) continue;
                declarations.Add(line);
            }

            Assert.AreEqual(2, declarations.Count,
                "DiseaseCourse.cs 的非注释代码里须恰有 2 条 `const int CarePhase*` 声明"
                + "(起 / 止);实际:" + string.Join(" | ", declarations));

            foreach (string decl in declarations)
            {
                Assert.IsTrue(decl.Contains("(int)CarePhase."),
                    $"转发常量须写成 `(int)CarePhase.X` —— 否则它就是**第二份序数**。实测:`{decl}`");
            }
        }

        /// <summary>
        /// ③ 权威件:`entities.yaml` 的 `phase: 枚举(起=0, 止=1)` 与 C# 枚举一致。
        /// <para><b>为什么这条必须有</b>:registry 是 ADR-024 认定的真源,但它对序数只有**散文**。
        /// 没有这条断言,「改 registry 不改代码」与「改代码不改 registry」都**不会红**,
        /// 而两者都让「三处齐备」退化成「两处真源」。</para>
        /// <para>⚠️ 判据是**字面**匹配 `phase: 枚举(起=0, 止=1)` —— 序数写法若变(例如改成
        /// 「起 0」),本断言会红。这是**刻意的**:写法一变就该有人确认,而不是静默放过。</para>
        /// </summary>
        [Test]
        public void test_carePhase_ordinalsMatchRegistry()
        {
            string yamlPath = System.IO.Path.Combine(
                TestRepoRoot(), "design", "registry", "entities.yaml");
            Assert.IsTrue(System.IO.File.Exists(yamlPath), $"entities.yaml 不存在:{yamlPath}");

            string yaml = System.IO.File.ReadAllText(yamlPath);

            Assert.IsTrue(yaml.Contains("phase: 枚举(起=0, 止=1)"),
                "entities.yaml 的 CareApplied payload_schema 须明写序数 `phase: 枚举(起=0, 止=1)` —— "
                + "只写「枚举(起 止)」时序数无权威,全库靠 DiseaseCourse 的落地约定");

            // 反向:不得残留无序数的旧写法(否则「改了一处、留了一处」看不出来)
            Assert.IsFalse(yaml.Contains("phase: 枚举(起 止)"),
                "entities.yaml 不得残留无序数的旧写法「phase: 枚举(起 止)」—— 两种写法并存 = 两处真源");
        }

        /// <summary>仓根 —— 与同目录既有测试同法(<c>[CallerFilePath]</c> 上溯五级)。</summary>
        private static string TestRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));
    }
}
