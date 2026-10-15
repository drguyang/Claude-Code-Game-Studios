// case-system Story 001 测试 —— 病例流 Kind 登记与病例身份
//
// ── GDD AC 覆盖(编号 = GDD 权威编号,case-system.md 的 AC 表)──────────────
// AC-37-01:病例流任意 CaseOpened 的 (Tick,Patient,Seq) 全流唯一;任何被引用的 case_id 等于某条 CaseOpened 三元组
// AC-37-02:两病人同 tick 各立案 ⇒ 两个 case_id 不同(两人各有 Seq=0,Patient 区分,不撞号)
//
// ── story 自定判据(⚠️ 四条**均无 GDD AC 编号** —— story §AC 里本就无号)──────
// · kindgen 断言批 A1–A5 全过 —— ⚠️ 载体 = CI(见下方「载体纪律」;本文件仅留 A1 生成物断言)
// · 载荷形状断言 —— CaseOpened/CaseClosed 载荷中无 opened_tick 字段;PatternRecognized 载荷含 salted_key
// · 跨流全序复验 —— (Tick,StreamPriority,Patient,Seq) 排序确定且无平局(哨兵 -1 行可排序不崩)
// · 高水位纯净 —— 含哨兵行的流上 max(patient_id) 重构不受 -1 污染(ADR-007 §四)
//
// ⚠️ 编号纪律(2026-10-15 · sprint-11 S11-11 修复 · 补评件 story-001 §二 B-3):
//   本头此前把上面四条**自行编号**为 AC-37-03/04/05/06 —— 这四个号在 GDD 里指向**别的需求**
//   (AC-37-03 = 已结案再结幂等拒收 · AC-37-04 = 未勾「已处置」拒绝 ·
//    AC-37-05 = 判断记录/处置落病例流 · AC-37-06 = 存档迁移重放逐位一致),
//   且**已被本目录姊妹测试占用**(case_close_precondition_test / case_open_and_judgment_test /
//   case_replay_persistence_test)。⇒ 按 AC 号做的覆盖矩阵会**重复计数**。
//   **无编号 = 这四条的真实状态,勿再补号** —— 方法名与注释同此纪律(全文件)。
//
// ⚠️ 载体纪律(2026-10-15 · sprint-12 S11-12 修复 · 补评件 story-001 §二 B-4):
//   「kindgen 断言批 A1–A5」的**唯一载体 = CI job `kindgen-assertions`**
//   (`.github/workflows/tests.yml`:`kindgen.py --check` 断言 + 重生成产物 `git diff` 一致性)。
//   本文件**不再复刻 kindgen 的扫描** —— 旧辅助段(手搓 entities.yaml 解析器)连同 A2/A3/A4/A5
//   四条断言已删:测试内重做 = 与 kindgen 互为「第二载体」(载体不唯一),且旧 A2 实测**空跑**
//   (扫 registry 散文,内层 `foreach` 命中 0 次 ⇒ 结构上不可证伪)。
//   **仅留 A1 一条**在测试内(`test_kindgenA1_*`):它打的是**生成物** `StreamRouting.Of`
//   (属生产码真断言 —— 补评件 §三 正面核实项),**不是**扫描复刻。
//
// 权威来源:
//   ADR-008(病例事件流;五 Kind 落病例流;case_id 三元组;opened_tick 删除;跨流全序;PatientId.None 哨兵)
//   ADR-024(Kind 单一登记真源 = entities.yaml;kindgen 生成 StreamRouting.g.cs + 断言 A1–A5)
//   ADR-007 §四(PatientId.None = -1 世界级事件不污染 max(patient_id) 高水位)
//   ADR-006(全序键 (Tick, StreamPriority, Patient, Seq);少一份可失步的状态)
//
// ⚠️ 本测试为纯数据层:不触引擎 API,不触 codec,不触持久化。
//    被测对象 = Sim.Contracts 的 CaseId / DiseaseIdSet / EventOrderKey / StreamRouting / EventKind。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.CaseSystem
{
    /// <summary>case-system Story 001 —— 病例流 Kind 登记与病例身份。</summary>
    [TestFixture]
    public sealed class CaseIdentityRoutingTest
    {
        // ── 辅助:构造 SimEvent(header 层,PayloadRef 用零值 —— 本故事不测载荷编码)──

        private static SimEvent MakeEvent(long tick, PatientId patient, long seq, EventKind kind)
        {
            return new SimEvent(tick, patient, seq, kind, default(PayloadRef));
        }

        private static CaseId MakeCaseId(long tick, int patient, long seq)
        {
            return new CaseId(tick, patient, seq);
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-01:case_id 三元组全流唯一
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-01:同 (Tick,Patient) 两条 CaseOpened(Seq 0/1)+ 另一 Patient 同 Tick(Seq 0)
        /// ⇒ 三者两两不同;任何 Seq 单独取值会撞(断言测试故意展示)。</summary>
        [Test]
        public void test_ac3701_caseIdTriple_uniqueAcrossStream()
        {
            var opened = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(1), 1, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var caseIds = opened
                .Select(e => MakeCaseId(e.Tick, e.Patient.Value, e.Seq))
                .ToList();

            // 三者两两不同
            Assert.That(caseIds[0], Is.Not.EqualTo(caseIds[1]), "同 (Tick,Patient) Seq 0/1 须不同");
            Assert.That(caseIds[0], Is.Not.EqualTo(caseIds[2]), "不同 Patient 同 Tick Seq 0 须不同");
            Assert.That(caseIds[1], Is.Not.EqualTo(caseIds[2]), "不同 Patient + 不同 Seq 须不同");

            // 任何 Seq 单独取值会撞(断言测试故意展示)
            var seqOnly = opened.Select(e => e.Seq).ToList();
            Assert.That(seqOnly.Distinct().Count(), Is.LessThan(seqOnly.Count),
                "Seq 单独取值会撞号 —— 证明 case_id 必须是三元组,不能只用 Seq");
        }

        /// <summary>AC-37-01:任何被引用的 case_id 等于某条 CaseOpened 三元组。</summary>
        [Test]
        public void test_ac3701_referencedCaseId_equalsSomeOpenedTriple()
        {
            var opened = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(1), 1, EventKind.CaseOpened),
                MakeEvent(200, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var openedSet = opened
                .Select(e => MakeCaseId(e.Tick, e.Patient.Value, e.Seq))
                .ToHashSet();

            // 模拟 CaseClosed 引用的 case_id
            var referenced = MakeCaseId(100, 1, 1);
            Assert.That(openedSet.Contains(referenced), Is.True,
                "被引用的 case_id 必须等于某条 CaseOpened 三元组");

            // 引用一个不存在的 case_id 须失败
            var nonExistent = MakeCaseId(999, 9, 9);
            Assert.That(openedSet.Contains(nonExistent), Is.False,
                "不存在的 case_id 不在 opened 集中");
        }

        // ════════════════════════════════════════════════════════════════════
        // AC-37-02:两病人同 tick 各立案 ⇒ 不撞号
        // ════════════════════════════════════════════════════════════════════

        /// <summary>AC-37-02:两病人同 tick 各立案 ⇒ 两个 case_id 不同(两人各有 Seq=0,Patient 区分)。</summary>
        [Test]
        public void test_ac3702_twoPatientsSameTick_caseIdsDiffer()
        {
            var opened = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),
            };

            var caseIds = opened
                .Select(e => MakeCaseId(e.Tick, e.Patient.Value, e.Seq))
                .ToList();

            Assert.That(caseIds[0], Is.Not.EqualTo(caseIds[1]),
                "两病人同 tick 各立案 ⇒ Patient 区分,不撞号");
            Assert.That(caseIds[0].Patient, Is.EqualTo(1));
            Assert.That(caseIds[1].Patient, Is.EqualTo(2));
        }

        // ════════════════════════════════════════════════════════════════════
        // [story 判据] kindgen A1 —— 生成物路由(A2–A5 载体 = CI;见文件头「载体纪律」)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>[story 判据] kindgen A1:五 Case Kind 的 stream 全为 case。</summary>
        [Test]
        public void test_kindgenA1_fiveCaseKinds_routeToCaseStream()
        {
            var caseKinds = new[]
            {
                EventKind.CaseOpened,
                EventKind.CaseClosed,
                EventKind.PatternRecognized,
                EventKind.JudgmentRecorded,
                EventKind.JudgmentRevised,
            };

            foreach (var kind in caseKinds)
            {
                Assert.That(StreamRouting.Of(kind), Is.EqualTo(StreamId.Case),
                    $"{kind} 须路由到病例流(ADR-008 §一)");
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // [story 判据] 载荷形状断言
        // ════════════════════════════════════════════════════════════════════

        /// <summary>[story 判据] 载荷形状断言:CaseOpenedPayload / CaseClosedPayload 载荷中无 opened_tick 字段
        /// (别名纪律 —— tick 只在 SimEvent.Tick 一处)。</summary>
        [Test]
        public void test_payloadShape_payloads_noOpenedTickField()
        {
            var openedType = typeof(CaseOpenedPayload);
            var closedType = typeof(CaseClosedPayload);

            Assert.That(openedType.GetField("OpenedTick", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "CaseOpenedPayload 不得有 opened_tick 字段(别名纪律)");
            Assert.That(closedType.GetField("OpenedTick", BindingFlags.Public | BindingFlags.Instance),
                Is.Null, "CaseClosedPayload 不得有 opened_tick 字段(别名纪律)");

            // tick 只在 SimEvent.Tick 一处
            var simEventType = typeof(SimEvent);
            Assert.That(simEventType.GetField("Tick", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "SimEvent.Tick 须存在(全案唯一 tick 来源)");
        }

        /// <summary>[story 判据] 载荷形状断言:PatternRecognizedPayload 载荷含 salted_key(AC-37-14 的载体半边在此建)。</summary>
        [Test]
        public void test_payloadShape_patternRecognizedPayload_containsSaltedKey()
        {
            var patternType = typeof(PatternRecognizedPayload);

            Assert.That(patternType.GetField("SaltedKey", BindingFlags.Public | BindingFlags.Instance),
                Is.Not.Null, "PatternRecognizedPayload 须含 SaltedKey 字段(AC-37-14 载体)");

            // salted_key 类型 = u64(ulong)
            var field = patternType.GetField("SaltedKey", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field.FieldType, Is.EqualTo(typeof(ulong)),
                "SaltedKey 类型须为 ulong(u64)");
        }

        // ════════════════════════════════════════════════════════════════════
        // [story 判据] 跨流全序复验
        // ════════════════════════════════════════════════════════════════════

        /// <summary>[story 判据] 跨流全序复验:构造病史/病例交错流,(Tick,StreamPriority,Patient,Seq) 排序确定且无平局
        /// (哨兵 -1 行可排序不崩)。</summary>
        [Test]
        public void test_crossStreamOrder_deterministicNoTies()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),      // case
                MakeEvent(100, new PatientId(1), 0, EventKind.InjuryOnset),     // history
                MakeEvent(100, PatientId.None, 0, EventKind.PatternRecognized), // case, 哨兵 -1
                MakeEvent(100, new PatientId(2), 0, EventKind.CaseOpened),      // case
                MakeEvent(100, new PatientId(1), 0, EventKind.ActorCellEntered),// world
                MakeEvent(99,  new PatientId(1), 0, EventKind.CaseOpened),      // case, 更早 tick
            };

            var keys = events.Select(e => EventOrder.KeyOf(e)).ToList();

            // 排序确定:相邻两两不同(无平局)
            var sorted = keys.OrderBy(k => k).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                Assert.That(sorted[i], Is.LessThan(sorted[i + 1]),
                    "跨流全序键须无平局(ADR-008 §二)");
            }

            // 哨兵 -1 行可排序不崩
            var sentinelKey = EventOrder.KeyOf(events[2]);
            Assert.That(sentinelKey.Patient, Is.EqualTo(PatientId.None));
            Assert.That(sentinelKey.Patient.Value, Is.EqualTo(-1));

            // 验证排序:tick 99 最早
            Assert.That(sorted[0].Tick, Is.EqualTo(99), "tick 99 最早");
        }

        /// <summary>[story 判据] 跨流全序复验:StreamPriority 序 = History(0) &lt; Case(1) &lt; World(2)。</summary>
        [Test]
        public void test_crossStreamOrder_streamPriority_orderHistoryCaseWorld()
        {
            Assert.That((int)StreamId.History, Is.LessThan((int)StreamId.Case),
                "History(0) < Case(1)");
            Assert.That((int)StreamId.Case, Is.LessThan((int)StreamId.World),
                "Case(1) < World(2)");
        }

        // ════════════════════════════════════════════════════════════════════
        // [story 判据] 高水位纯净
        // ════════════════════════════════════════════════════════════════════

        /// <summary>[story 判据] 高水位纯净:含哨兵行的流上高水位重构不受 -1 污染(ADR-007 §四)。
        /// <para>打的是<b>生产</b> <see cref="IdAuthority.ReconstructPatientHighWater"/>(非测试内自造 max)。</para>
        /// <para>⚠️ <b>2026-10-15 判据诚实性订正(B-1 测试半边)</b> —— 原稿
        /// <c>events.Where(...).Select(...).Max()</c> 是<b>测试自造</b>的算式,生产函数<b>零调用</b>;
        /// 且「哨兵不污染 max」<b>单独在算术上不可证伪</b>(<c>-1</c> 是下界,含它与否 max 相同)。</para>
        /// <para>⚠️ <b>判别力如实计数(2026-10-15 第三次窄审订正)</b> —— 本稿 <b>只有 ① 与 ③ 两处</b>
        /// 可被<b>单行</b>变异打红,<b>不是三处</b>(初稿误记「三处」,已订正)。
        /// <b>② 是结构性保留、无单行捕获面</b>:「只升不降」的真正来源是 <c>candidate</c> 的初值
        /// <c>= _nextPatient</c> 与 <c>if (id + 1 &gt; candidate)</c> 的取 max 形式,
        /// <b>不是</b>末尾 <c>if (candidate &gt; _nextPatient)</c> 那道守卫(它<b>行为冗余</b>)。
        /// ⇒ 同删 <c>id &lt; 0 continue</c> 与该守卫,<c>candidate</c> 仍 ≥ 初值,<b>② 仍绿</b>。
        /// 只有整函数重写(如初值改 0 <b>且</b> 去末尾守卫)才会红 —— 那已非「单行变异」。</para></summary>
        [Test]
        public void test_highWaterMark_sentinelMinusOne_noPollution()
        {
            var events = new[]
            {
                MakeEvent(100, new PatientId(1), 0, EventKind.CaseOpened),
                // ⚠️ 哨兵置于**最大值之前**:令「截断扫描」类 bug 可被 ① 捕获 ——
                //    把 `id < 0 continue` 改成 `break`(真实缺陷类),此处 ⇒ candidate 停在 2 ⇒ 红;
                //    若哨兵排在末尾(初稿序),该变异**不会红**(scan 已取到 max)。
                MakeEvent(100, PatientId.None, 0, EventKind.PatternRecognized), // 哨兵 -1
                MakeEvent(100, new PatientId(5), 0, EventKind.CaseOpened),
                MakeEvent(100, new PatientId(3), 0, EventKind.CaseOpened),
            };

            // ── ① 真 id 累加:max(真 id)=5 ⇒ next = 6(走生产函数)──
            // 单行可捕获:删 `if (id + 1 > candidate) candidate = id + 1;` ⇒ next 停在 0 ⇒ 红;
            //             把 `id < 0 continue` 改 `break`(哨兵序已前置)⇒ candidate 停在 2 ⇒ 红
            var authority = new IdAuthority();
            authority.ReconstructPatientHighWater(events.Select(e => e.Patient.Value));
            Assert.That(authority.NextPatientId().Value, Is.EqualTo(6),
                "重构取 max(真 id)+1 = 6(哨兵 -1 在流内,由生产函数自行忽略)");

            // ── ② 只升不降 + 全哨兵不回退(⚠️ 结构性,无单行捕获面 —— 见 docstring)──
            var monotone = new IdAuthority();
            monotone.NextPatientId(); // 0
            monotone.NextPatientId(); // 1 ⇒ _nextPatient = 2
            monotone.ReconstructPatientHighWater(new[] { -1, -1 }); // 全哨兵流
            Assert.That(monotone.NextPatientId().Value, Is.EqualTo(2),
                "全哨兵流不得把高水位拉回 0(否则重发 patient_id 0 = 身份重号)");

            // ── ③ 号空间耗尽硬失败(不静默回绕)──
            // 单行可捕获:删 OverflowException ⇒ 红
            // ⚠️ 隐性前提:算术须**非受检**。实测全仓无 `CheckForOverflowUnderflow` /
            //    无 `.rsp` / 无 `Directory.Build.props` ⇒ 当下成立;
            //    一旦开启受检算术,`int.MaxValue + 1` 自身抛 ⇒ 本断言**静默失效**(如实登记)。
            Assert.Throws<OverflowException>(
                () => new IdAuthority().ReconstructPatientHighWater(new[] { int.MaxValue }),
                "高水位耗尽须显式硬失败(ADR-006 机制 A)");
        }

    }
}
