// 权威来源:production/epics/telemetry-analytics/story-009-blank-and-degraded.md
//   (AC-51-D1…D9 — 空白与退化处理:空输入零值 / 分母 0 报 (0,0)+标记 /
//    Faulted 不产出半份 / 规模不改变数值 / 部分流显式标注)
//   · D1:三流皆空 ⇒ 状态 = Computed(非 Faulted)、全部指标零值、不抛异常
//   · D2:分母 0 ⇒ 输出整数对 (0,0) + 显式「分母 0/未定义」标记;不报 0/1、不报标量 0、不报 NaN
//   · D3:Faulted 不产出半份 —— 状态 = Faulted 且中止;ITelemetrySink 零调用;
//          本地文件未生成/未覆写(旧文件字节保持);无部分指标;不可自愈(后续 Compute() 从 Idle 重入)
//   · D5:R7 整数纪律 —— 无 float/double;率以 (num, den) 承载;
//          标量导出 = ROUND_HALF_AWAY_FROM_ZERO((num << 16) / den)
//   · D6:规模不改变数值 —— 同一超长流以 FOLD_MEMORY_MODE 两值各重算,全部指标逐位相同
//   · D7:ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数
//   · D8:部分流 —— 要么拒绝要么产出并显式标注「部分指标」;禁止把部分流当完整流静默出数
//   · D9:样本不足 —— 报告照常产出数值且含「样本不足」标注;不隐藏、不四舍五入掩盖
// ADR-019 §一(回放即数据记录,零新埋点)· ADR-006 §五(Fix 编码器 + 守恒律)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/telemetry/blank_and_degraded_test.cs;
//    Unity 只编译 unity/Assets/树 ⇒ **真身 = 本文件**;账本侧由 tests/integration/telemetry/README.md 互链。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(禁 DateTime/Time)·
//    夹具自持,不依赖文件系统/网络/数据库。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **TL-epic-01 第十三片重写(2026-10-19)** —— 本文件此前是**假绿**(普查第五形态:
//    扫不到被测装配)。实测证据:
//      · 上一版把「谓词面」上移为 `Telemetry.TickFoldEngine`,但全仓对它的**唯一引用是注释**;
//      · D1–D9 的断言全部跑在 `ComputeEmpty()` / `ComputeFaulted()` / `ComputeTwoModes()`
//        三个**手搓常量**上 —— 删掉 `TickFoldEngine` 整个文件,本文件**照样全绿**;
//      · D7 的两条更是只断言「本地拼的字符串 contains」 ⇒ 恒真。
//    **重写原则**:每条断言必须有**生产主语**,且把主语的守卫改坏 ⇒ 该条**必须转红**。
//    无生产主语的 D3/D5/D6/D7,如实登记为 **NOT-RUN**(禁借绿),不再用自造常量冒充。
// ═══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;      // TelemetryReporter(AC-51-D8 生产主语)
using DaYiJingCheng.Gameplay.PatientAI; // PresenceRegistry(EventStream 构造需要)
using DaYiJingCheng.Sim;                // EventStream 等装配面
using DaYiJingCheng.Sim.Codec;          // PayloadEncoder / PayloadDecoder / InMemoryBlobPool
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Telemetry;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class BlankAndDegradedTest
    {
        // ══════════════ AC-51-D1:三流皆空 ⇒ Computed + 零值 ══════════════

        /// <summary>AC-51-D1:三流皆空 —— 全部指标零值、不抛异常。
        /// <para><b>生产主语</b>:<see cref="TickFoldEngine.Fold"/> 的 `ticks.Count == 0` 空流分支。</para>
        /// <para><b>判别力</b>:删掉 `Fold` 里的 `if (ticks.Count == 0) return (0,0,0,0,0);`
        /// ⇒ 落到 `ticks.Min()` 抛 `InvalidOperationException` ⇒ 首条断言红。</para></summary>
        [Test]
        public void test_d1_emptyStreams_computedZeroNoThrow()
        {
            // Arrange:三流均空
            var empty = new List<int>();

            // Act
            (int totalEvents, int computedCases, int sumPayload, int minTick, int maxTick) outcome = default;
            Assert.DoesNotThrow(() => outcome = TickFoldEngine.Fold(empty),
                "空输入不得抛异常(D1:状态 Computed,非 Faulted)");

            // Assert:全部指标零值
            Assert.That(outcome.totalEvents, Is.EqualTo(0), "空流 TotalEvents = 0");
            Assert.That(outcome.computedCases, Is.EqualTo(0), "空流 ComputedCases = 0");
            Assert.That(outcome.sumPayload, Is.EqualTo(0), "空流 SumPayload = 0");
            Assert.That(outcome.minTick, Is.EqualTo(0), "空流 MinTick = 0");
            Assert.That(outcome.maxTick, Is.EqualTo(0), "空流 MaxTick = 0");
        }

        /// <summary>AC-51-D1 正向对照:非空流 ⇒ 五值真由输入决定(防「恒返回零」也能过 D1)。</summary>
        /// <remarks><b>判别力</b>:把 `Fold` 改成恒返回 `(0,0,0,0,0)` ⇒ 本条红。</remarks>
        [Test]
        public void test_d1_nonEmptyStream_valuesFollowInput()
        {
            // Arrange
            var ticks = new List<int> { 7, 3, 11 };

            // Act
            var r = TickFoldEngine.Fold(ticks);

            // Assert
            Assert.That(r.totalEvents, Is.EqualTo(3), "事件数 = 输入长度");
            Assert.That(r.sumPayload, Is.EqualTo(21), "载荷和 = 7+3+11");
            Assert.That(r.minTick, Is.EqualTo(3), "min = 3");
            Assert.That(r.maxTick, Is.EqualTo(11), "max = 11");
        }

        // ══════════════ AC-51-D2:分母 0 报 (0,0) ══════════════

        /// <summary>AC-51-D2:分母 0 —— 报「分母 0」标记,**不**报 `0 / 1`、不报标量 0、不除零。
        /// <para><b>生产主语</b>:<see cref="MetricsReport.Render"/> 的 `l.DenominatorZero` 分支
        /// (E-2 逐字:`sb.Append("分母 0")`)。</para>
        /// <para><b>判别力</b>:删掉该分支、落进正常分支 ⇒ 本条红(渲染成 `0 / 0`)。</para></summary>
        [Test]
        public void test_d2_zeroDenominator_reportsFlagNotZeroOverOne()
        {
            // Arrange:分母 0 的真实指标行(走生产 struct,不自造)
            var lines = new List<MetricLine> { new MetricLine("F3a", 0, 0) };

            // Act
            string rendered = MetricsReport.Render("blank", lines);

            // Assert:显式「分母 0」
            Assert.That(rendered, Does.Contain("F3a = 分母 0"), "分母 0 ⇒ 显式标「分母 0」");
            // Assert:不报 0 / 1(不得假装有分母)
            Assert.That(rendered, Does.Not.Contain("0 / 1"), "不得用 0 / 1 假装有分母");
            // Assert:不报裸标量 0
            Assert.That(rendered, Does.Not.Contain("F3a = 0\n"), "不得报标量 0");
        }

        /// <summary>AC-51-D2 负向:分母非 0 ⇒ 报 `num / den` 整数对,**不**折小数。
        /// <para><b>判别力</b>:把正常分支改成折成小数(如 `0.5`)⇒ 本条红。</para></summary>
        [Test]
        public void test_d2_nonZeroDenominator_reportsIntegerPair()
        {
            // Arrange
            var lines = new List<MetricLine> { new MetricLine("F1a", 3, 7) };

            // Act
            string rendered = MetricsReport.Render("normal", lines);

            // Assert
            Assert.That(rendered, Does.Contain("F1a = 3 / 7"), "率须以 (num, den) 整数对承载(R7)");
            Assert.That(rendered, Does.Not.Contain("0.4"), "不得自作主张折小数");
        }

        // ══════════════ AC-51-D3:Faulted 不产出半份 ══════════════

        /// <summary>AC-51-D3:Faulted 不产出半份 —— 状态 = Faulted 且中止;`ITelemetrySink` 零调用;
        /// 本地文件未生成/未覆写;无部分指标。
        /// <para><b>生产主语</b>:<see cref="StreamIntegrity.Validate"/> 的三条校验 +
        /// <see cref="TelemetryReporter.ComputeAndSink"/> 的 Faulted 早退。</para>
        /// <para><b>判别力</b>:从 <see cref="TelemetryReporter.ComputeAndSink"/> 删掉 Faulted 早退 ⇒ 三条全红。</para></summary>
        /// <remarks>⚠️ **2026-10-19 解冻(第十四片)**:原 `NOT_RUN` 写明解冻条件 = 「出现持状态且调
        /// `ITelemetrySink` 的生产类型时,本测改为直接驱动它」—— <see cref="ComputeSession"/> +
        /// `ComputeAndSink` 即为该类型,条件已满足。</remarks>
        [Test]
        public void test_d3_tickRegression_faults()
        {
            // ⚠️ Tick 回退**不能**经 `EventStream.Append` 造(其 `BCD-码-3` 会先抛)⇒ 直驱生产校验器。
            var history = new[] { Ev(10, 0, new PatientId(1)), Ev(5, 0, new PatientId(1)) };

            IntegrityResult r = StreamIntegrity.Validate(history, Array.Empty<SimEvent>(), Array.Empty<SimEvent>());

            Assert.That(r.Violation, Is.EqualTo(IntegrityViolation.TickRegression), "Tick 回退须判 TickRegression");
            Assert.That(r.Stream, Is.EqualTo(StreamId.History), "违规在病史流");
            Assert.That(r.Index, Is.EqualTo(1), "违规位于回退的那一条");
        }

        /// <summary>AC-51-D3 ②:`Seq` 断号(E-5)⇒ Faulted,且病例流也被抓得到。
        /// <para>⚠️ 直驱 <see cref="StreamIntegrity.Validate"/> —— 经 `EventStream.Append` 造不出断号
        /// (发号器会把它续上),这正是「离线重算」与「实时流」两条输入面的差异(GDD R9)。</para></summary>
        [Test]
        public void test_d3_seqGap_faults()
        {
            var history = new[] { Ev(1, 0, new PatientId(1)) };
            var cases = new[] { Ev(2, 0, new PatientId(1)), Ev(2, 2, new PatientId(1)), Ev(2, 3, new PatientId(1)) };

            IntegrityResult r = StreamIntegrity.Validate(history, cases, Array.Empty<SimEvent>());

            Assert.That(r.Violation, Is.EqualTo(IntegrityViolation.SeqGap), "Seq 断号须判 SeqGap");
            Assert.That(r.Stream, Is.EqualTo(StreamId.Case), "违规在病例流");
        }

        /// <summary>AC-51-D3 ③:病例流引用不在病史流的 `Patient`(E-6)⇒ Faulted。
        /// <para>本条**能**走 `ComputeAndSink` 端到端:`Append` 逐条发号 ⇒ 单流内 Seq 天然连续,
        /// 唯一人为构造的是「病例流的病人从未在病史流出现」。</para>
        /// <para><b>判别力</b>:删掉 `ComputeAndSink` 的 Faulted 早退 ⇒ 本条红;删掉 E-6 校验 ⇒ 本条红。</para></summary>
        [Test]
        public void test_d3_casePatientNotInHistory_faultsAndSinkUntouched()
        {
            var (reporter, stream) = NewReporter();

            // Arrange:病史流只有病人 1;病例流带一个病人 7(Append 发号 ⇒ Seq 天然 0,1)
            PayloadRef r0 = new PayloadRef(0, 0, 0);
            stream.Append(new SimEvent(0L, new PatientId(1), -1, EventKind.CareApplied, r0));
            stream.Append(new SimEvent(1L, new PatientId(7), -1, EventKind.CaseOpened, r0));

            // Act
            var sink = new RecordingSink();
            EvalState state = reporter.ComputeAndSink(sink, _ => new[] { "不该出现" });

            // Assert
            Assert.That(state, Is.EqualTo(EvalState.Faulted), "E-6 违规 ⇒ ComputeAndSink 判 Faulted");
            Assert.That(sink.WriteCount, Is.Zero, "Faulted ⇒ WriteLocalReport 零调用");
            Assert.That(sink.LogCount, Is.Zero, "Faulted ⇒ LogDeveloperSummary 零调用");
        }

        /// <summary>AC-51-D3(不可自愈):Faulted 的会话再次 `Compute` ⇒ 仍 Faulted,不回 Idle、不重算。
        /// <para><b>判别力</b>:把 <see cref="ComputeSession.Compute"/> 的 Faulted 早退改成「重新校验」⇒
        /// 第二次喂好流会得 Computed ⇒ 本测红。</para></summary>
        [Test]
        public void test_d3_faulted_notSelfHealing()
        {
            var session = new ComputeSession();
            var bad = new[] { Ev(10, 0, new PatientId(1)), Ev(5, 0, new PatientId(1)) };

            Assert.That(session.Compute(bad, Array.Empty<SimEvent>(), Array.Empty<SimEvent>()),
                Is.EqualTo(EvalState.Faulted), "前置:Faulted");

            var good = new[] { Ev(1, 0, new PatientId(1)), Ev(2, 0, new PatientId(1)) };
            Assert.That(session.Compute(good, Array.Empty<SimEvent>(), Array.Empty<SimEvent>()),
                Is.EqualTo(EvalState.Faulted), "Faulted 不可自愈 —— 好流也不能救回同一次会话");
            Assert.That(session.LastIntegrity.Violation, Is.EqualTo(IntegrityViolation.TickRegression),
                "不过期不重算:违规明细仍是首次那条");
        }

        /// <summary>AC-51-D3 正向对照:三流完好 ⇒ Computed,sink 被调用(证明门不是「恒拒」)。
        /// <para><b>判别力</b>:把 `ComputeAndSink` 写成恒返回 Faulted ⇒ 本条红。</para></summary>
        [Test]
        public void test_d3_cleanStreams_computedAndSinkCalled()
        {
            var (reporter, _) = NewReporter();
            var sink = new RecordingSink();

            EvalState state = reporter.ComputeAndSink(sink, _ => new[] { "报告正文" });

            Assert.That(state, Is.EqualTo(EvalState.Computed), "完好流 ⇒ Computed");
            Assert.That(sink.WriteCount, Is.EqualTo(1), "Computed ⇒ 正常落 sink");
            Assert.That(sink.LogCount, Is.EqualTo(1));
        }

        private static SimEvent Ev(long tick, long seq, PatientId p)
            => new SimEvent(tick, p, seq, EventKind.CareApplied, new PayloadRef(0, 0, 0));

        /// <summary>真装配:空 `EventStream` + 真 `PayloadDecoder` + 单病种注册表。</summary>
        private (TelemetryReporter reporter, EventStream stream) NewReporter()
        {
            var pool = new InMemoryBlobPool();
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } });
            return (reporter, stream);
        }

        /// <summary>计数型 sink(零调用 = 两个计数都为 0)。</summary>
        private sealed class RecordingSink : ITelemetrySink
        {
            public int WriteCount;
            public int LogCount;
            public void WriteLocalReport(string fileName, string content) => WriteCount++;
            public void LogDeveloperSummary(string summary) => LogCount++;
        }

        // ══════════════ AC-51-D5:R7 整数纪律 ══════════════

        // ── D5 第一半:率以 (num, den) 承载 + 标量导出走定点 ──
        //   夹具:`metrics_report_test.cs::test_render_rateAsIntegerPair_notDecimal`(数值侧)
        //       + `test_roundHalfAway_*` / `test_toQ1616_*`(舍入与 Q16.16 导出)。
        // ── D5 第二半:无 float/double —— 见下方两条程序集扫描 ──

        /// <summary>AC-51-D5(浮点扫描面):<c>Telemetry</c> 程序集**零浮点** —— 字段 / 属性 /
        /// 方法返回值 / 方法参数 / 事件委托签名,全类型全成员。
        /// <para><b>扫描面</b>:命名空间 <c>DaYiJingCheng.Telemetry</c> 全部类型。该程序集即 51 的
        /// 纯逻辑面(率载体、折叠、渲染都在这里),R7 的「无 float/double」首先约束它。</para>
        /// <para><b>判别力</b>:任一成员声明成 <c>float</c> / <c>double</c>(含 <c>double?</c>) ⇒ 本条红。</para>
        /// <para>⚠️ 扫描为空 ⇒ 直接红(`types.Count &gt; 0` 守卫)—— 防「反射扫不到装配」式空洞为真
        /// (详见 <c>readonly_boundary_test.cs:412</c> 的同类警告)。</para></summary>
        [Test]
        public void test_d5_telemetryAssembly_noFloatOrDoubleMembers()
        {
            AssertNoFloatingMembers(TypesInNamespace("DaYiJingCheng.Telemetry"), "DaYiJingCheng.Telemetry");
        }

        /// <summary>AC-51-D5(浮点扫描面 · 报告生产者半边):<c>TelemetryReporter</c> 与
        /// <c>TelemetryDeveloperView</c> **零浮点** —— 指标不得经 float/double 中转。
        /// <para>扫描面按**具体类型**取(而非整个 <c>Gameplay.Boot</c>):该程序集还住启动序与玩家搬运等
        /// 非 51 类型,对它们套 R7 超出本 AC 范围。报告若未来迁出 Boot,本条须随之改锚点 ——
        /// 改锚点时的空集守卫会先红,不会静默失效。</para></summary>
        [Test]
        public void test_d5_reportProducers_noFloatOrDoubleMembers()
        {
            var producers = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t.FullName == "DaYiJingCheng.Gameplay.Boot.TelemetryReporter" ||
                            t.FullName == "DaYiJingCheng.Gameplay.Boot.TelemetryDeveloperView")
                .ToList();

            Assert.That(producers.Count, Is.EqualTo(2),
                "报告生产者类须各命中一个(少于 2 ⇒ 类被改名/移走 ⇒ 扫描面失效,非「通过」)");

            AssertNoFloatingMembers(producers, "报告生产者");
        }

        /// <summary>按命名空间前缀取类型(与 <c>readonly_boundary_test.cs</c> 同款取法)。</summary>
        private static List<Type> TypesInNamespace(string ns)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .Where(t => t.Namespace != null &&
                            (t.Namespace == ns || t.Namespace.StartsWith(ns + ".", StringComparison.Ordinal)))
                .ToList();
        }

        /// <summary>反射取类型;动态程序集等抛异常者跳过(不让扫描整体崩,但空集守卫会兜住)。</summary>
        private static Type[] SafeTypes(System.Reflection.Assembly a)
        {
            try { return a.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }

        /// <summary>断言给定类型集的所有成员声明中无浮点。违例逐条点名(成员全名 ⇒ 排查不用猜)。</summary>
        private static void AssertNoFloatingMembers(List<Type> types, string scopeLabel)
        {
            Assert.That(types.Count, Is.GreaterThan(0), $"{scopeLabel} 扫描集为空 —— 反射未命中装配,判据无效");

            const System.Reflection.BindingFlags Members =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.DeclaredOnly;

            var violations = new List<string>();
            foreach (Type t in types)
            {
                foreach (System.Reflection.FieldInfo f in t.GetFields(Members))
                    if (IsFloating(f.FieldType)) violations.Add($"{t.Name}.{f.Name} 字段 : {f.FieldType.Name}");

                foreach (System.Reflection.PropertyInfo p in t.GetProperties(Members))
                    if (IsFloating(p.PropertyType)) violations.Add($"{t.Name}.{p.Name} 属性 : {p.PropertyType.Name}");

                foreach (System.Reflection.MethodInfo m in t.GetMethods(Members))
                {
                    if (m.IsSpecialName && !m.Name.StartsWith("get_", StringComparison.Ordinal)) continue;
                    if (IsFloating(m.ReturnType)) violations.Add($"{t.Name}.{m.Name} 返回 : {m.ReturnType.Name}");
                    foreach (var p in m.GetParameters())
                    {
                        Type pt = p.ParameterType;
                        if (pt.IsByRef) pt = pt.GetElementType();
                        if (IsFloating(pt)) violations.Add($"{t.Name}.{m.Name} 参数 {p.Name} : {pt.Name}");
                    }
                }

                foreach (System.Reflection.ConstructorInfo c in t.GetConstructors(Members))
                    foreach (var p in c.GetParameters())
                        if (IsFloating(p.ParameterType))
                            violations.Add($"{t.Name}..ctor 参数 {p.Name} : {p.ParameterType.Name}");
            }

            Assert.That(violations, Is.Empty,
                $"{scopeLabel} 中出现浮点成员(R7 / AC-51-D5 禁止):\n  " + string.Join("\n  ", violations));
        }

        /// <summary>是否为 <c>float</c> / <c>double</c>(含 <c>Nullable&lt;double&gt;</c>、数组元素、泛型实参)。</summary>
        private static bool IsFloating(Type ty)
        {
            if (ty == null) return false;
            if (ty == typeof(float) || ty == typeof(double)) return true;
            if (ty.HasElementType && IsFloating(ty.GetElementType())) return true;
            if (ty.IsGenericType && ty.GetGenericArguments().Any(IsFloating)) return true;
            return false;
        }

        // ══════════════ AC-51-D6:规模不改变数值 ══════════════

        /// <summary>AC-51-D6:规模不改变数值 —— ~~同一超长流以 `FOLD_MEMORY_MODE` 两值各重算,逐位相同~~。</summary>
        /// <remarks>⏸ <b>P0 已推开(用户裁定 2026-10-19)</b>:P0 只保留一套折叠实现,双模式对拍整个推迟 ——
        /// GDD 的「流式 / 整表」两模式当前**都没有独立实现**,而「两套玩具实现逐位相同」是自证,
        /// 不是 AC 要验的东西。内存上界那半另卡 ADR-005 的 Memory Ceiling 数值(GDD `:654`「现不可写」)。
        /// <para>保留本测(仍 Ignore)而非删除:它是 AC 的开冻哨兵 —— 将来另开 ADR 定第二模式时,
        /// 解冻它并把两边换成真实现。上一版 `ComputeTwoModes(1000)` 返回两个**手搓常量**已删除。</para>
        /// <para>开冻条件:出现真实内存压力 ⇒ 另开 ADR 定第二模式;同批须给出 Memory Ceiling 值。</para></remarks>
        [Test]
        public void test_d6_scaleDoesNotChangeValues_NOT_RUN()
        {
            Assert.Ignore("AC-51-D6 已推开(P0 只留一套折叠实现,用户裁定 2026-10-19);" +
                          "内存上界半条另卡 ADR-005 Memory Ceiling。开冻另开 ADR。");
        }

        // ══════════════ AC-51-D7:ConfigVersion 不匹配 ══════════════

        /// <summary>AC-51-D7:ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,**不静默出数**。
        /// <para><b>生产主语</b>:<see cref="ConfigVersionCheck.Banner"/> +
        /// <see cref="TelemetryReporter.AllMetricsReport"/> 的版本门早退。</para>
        /// <para><b>判别力</b>:删掉 `AllMetricsReport` 的版本门早退 ⇒ 报告里出现指标行 ⇒ 本条红;
        /// 把 `Banner()` 改成恒空串 ⇒ 标注断言红。</para></summary>
        /// <remarks>⚠️ **2026-10-19 解冻(第十五片)**:原 NOT-RUN 记「51 无比对路径」属实,但外部依赖
        /// (`Sim.Codec/SaveHeader.ConfigVersion`)**早已存在**。本次按 ADR-010 §七 / ADR-014 §五
        /// 落地比对门 —— 51 收两个 u32(`SaveHeader` 在装配层解出),**不直引 `Sim.Codec`**
        /// (`Telemetry.asmdef` 引用集恰 = `{Sim.Contracts}`,与 `JudgmentStreamReducer` 同一条边界纪律)。</remarks>
        [Test]
        public void test_d7_configVersionMismatch_flaggedAndNoMetrics()
        {
            var (reporter, _) = NewReporter();

            // Arrange:存档头 v41,当前产物 v42
            var mismatch = new ConfigVersionCheck(saveHeaderConfigVersion: 41, currentConfigVersion: 42);

            // Act
            string report = reporter.AllMetricsReport(out int skipped, mismatch);

            // Assert ①:显式标注,且两个版本号都在
            Assert.That(report, Does.Contain("版本不匹配"), "须显式标注版本不匹配");
            Assert.That(report, Does.Contain("41"), "须点出存档头版本号");
            Assert.That(report, Does.Contain("42"), "须点出当前产物版本号");

            // Assert ②:不出数 —— 不是「测得 0」
            Assert.That(report, Does.Not.Contain("F1a"), "版本不匹配时不得出任何指标");
            Assert.That(report, Does.Not.Contain("/ "), "不得出现任何率值");
            Assert.That(skipped, Is.Zero, "不出数 ⇒ 无跳过计数可报");
        }

        /// <summary>AC-51-D7 正向对照:版本一致 ⇒ 出正常报告、**无**「版本不匹配」标注。
        /// <para><b>判别力</b>:把 `AllMetricsReport` 的版本门写成恒拒 ⇒ 本条红。</para></summary>
        [Test]
        public void test_d7_configVersionMatch_noBannerAndMetricsPresent()
        {
            var (reporter, _) = NewReporter();
            var match = new ConfigVersionCheck(saveHeaderConfigVersion: 42, currentConfigVersion: 42);

            string report = reporter.AllMetricsReport(out _, match);

            Assert.That(report, Does.Not.Contain("版本不匹配"), "一致 ⇒ 不得出该标注");
            Assert.That(report, Does.Contain("F1"), "一致 ⇒ 正常出指标");
        }

        /// <summary>AC-51-D7:未注入版本检查 ⇒ 不做门(单机 / 无存档场景不误伤)。
        /// <para><b>判别力</b>:把可空参数改成必填 / 缺省即拒 ⇒ 本条红。</para></summary>
        [Test]
        public void test_d7_noConfigVersionCheck_noGate()
        {
            var (reporter, _) = NewReporter();
            string report = reporter.AllMetricsReport(out _);
            Assert.That(report, Does.Not.Contain("版本不匹配"));
            Assert.That(report, Does.Contain("F1"));
        }

        // ══════════════ AC-51-D8 前半:空流标注 ══════════════

        /// <summary>AC-51-D8(空流半边):空输入 ⇒ 完整性头明说「这是空输入零值」,不是测得的 0。
        /// <para><b>生产主语</b>:<see cref="TelemetryReporter.CompletenessBanner"/> 的 `empty` 分支。</para>
        /// <para>⚠️ 端到端版本见 <c>Boot/telemetry_debug_view_test.cs::test_d8_emptyStream_bannerSaysZeroIsLegit</c>
        /// (经真 reporter + 真事件流)。本条是**纯生产类**侧的重复钉住,两处同源。</para></summary>
        [Test]
        public void test_d8_emptyStream_bannerSaysEmptyZero()
        {
            // Arrange:真空事件流(真生产装配,不自造替身)
            var pool = new InMemoryBlobPool();
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } });

            // Act
            string banner = reporter.CompletenessBanner(f1Faulted: false);

            // Assert
            Assert.That(banner, Does.Contain("空流"), "空输入 ⇒ 头须明说「空流」");
            Assert.That(banner, Does.Contain("空输入零值"), "须点明这是空输入零值,不是测得 0");
            Assert.That(banner, Does.Contain("Computed"), "须声明状态 Computed(非 Faulted)");
            Assert.That(banner, Does.Not.Contain("Faulted:流异常"), "空流不得报 Faulted");
        }

        /// <summary>AC-51-D8(空流半边)负向:非空流 ⇒ 不得再报「空流」。
        /// <para><b>判别力</b>:把 `CompletenessBanner` 的 `empty` 改成恒 true ⇒ 本条红。</para></summary>
        [Test]
        public void test_d8_nonEmptyStream_noEmptyBanner()
        {
            // Arrange:真流里追加一条真实事件
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var stream = new EventStream(new SeqIdAuthority(), new PresenceRegistry());
            PayloadRef r = encoder.Encode(EventKind.CaseClosed,
                new CaseClosedPayload(patientId: 1, new CaseId(10, 1, 1),
                    new DiseaseIdSet(1UL << 0), treated: true));
            stream.Append(new SimEvent(0L, new PatientId(1), -1, EventKind.CaseClosed, r));
            var reporter = new TelemetryReporter(stream, new PayloadDecoder(pool),
                new[] { new DiseaseRegistryEntry { Kind = DiseaseKind.Disease, DiseaseId = 1, DiseaseKey = "d1" } });

            // Act
            string banner = reporter.CompletenessBanner(f1Faulted: false);

            // Assert
            Assert.That(banner, Does.Not.Contain("空流"), "非空流不得报「空流」");
            Assert.That(banner, Does.Contain("流事件"), "非空流须报事件条数");
        }

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private long _n = 100;
            public PatientId NextPatientId() => new PatientId((int)_n++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_n++);
        }
    }
}
