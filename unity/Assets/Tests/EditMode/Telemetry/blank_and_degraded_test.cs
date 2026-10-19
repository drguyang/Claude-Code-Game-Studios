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
        /// 本地文件未生成/未覆写;无部分指标;不可自愈。</summary>
        /// <remarks>⚠️ <b>NOT-RUN(禁借绿)</b>:生产侧**尚无**「Faulted 状态机 + sink 调用门」。
        /// <see cref="TickFoldEngine"/> 只做纯折叠、不持状态、不碰 sink ⇒ 本 AC 在当下
        /// **没有可被变坏的守卫**。上一版用 `ComputeFaulted()` 手搓 `SinkCalled = false`
        /// 冒充通过 —— 那断言的是常量,不是生产行为,已删除。
        /// <para>解冻条件:出现持 `EngineState` 且调 `ITelemetrySink` 的生产类型时,本测改为直接驱动它,
        /// 并以「去掉 Faulted 早退 ⇒ 本条红」作为判别力判据。</para></remarks>
        [Test]
        public void test_d3_faulted_noPartialOutput_NOT_RUN()
        {
            Assert.Ignore("AC-51-D3 NOT-RUN:生产侧尚无 Faulted 状态机 + sink 门;" +
                          "TickFoldEngine 是纯函数(不持状态/不碰 sink)⇒ 无守卫可变异。" +
                          "不得以手搓常量冒充(上一版即此法)。");
        }

        // ══════════════ AC-51-D5:R7 整数纪律 ══════════════

        /// <summary>AC-51-D5:R7 整数纪律 —— 无 float/double;率以 (num, den) 承载。</summary>
        /// <remarks>⚠️ <b>NOT-RUN(禁借绿)</b>:本 AC 的判据面 = 「哪些类型承载率 / 是否出现浮点」,
        /// 须先裁定扫描面(是 `Telemetry` 程序集全类型,还是报告类型子集),未裁定前自造扫描范围
        /// 等于自造判据。上一版对此 AC **零断言**(顶部登记了 D5,代码里却没有 D5 测) —— 属实登记。</remarks>
        [Test]
        public void test_d5_integerDiscipline_noFloatInRatioCarriers_NOT_RUN()
        {
            Assert.Ignore("AC-51-D5 NOT-RUN:「率载体 / 浮点扫描面」未裁定;" +
                          "自造扫描范围 = 自造判据。上一版对本 AC 零断言。");
        }

        // ══════════════ AC-51-D6:规模不改变数值 ══════════════

        /// <summary>AC-51-D6:规模不改变数值 —— 同一超长流以 `FOLD_MEMORY_MODE` 两值各重算,逐位相同。</summary>
        /// <remarks>⚠️ <b>NOT-RUN(禁借绿)</b>:`FOLD_MEMORY_MODE` 在 GDD 里**仍为 `*待定*`**
        /// (且已裁定不进 `telemetry_analytics.json` —— 实现策略非测量参数)。无两种模式 ⇒
        /// 没有「两值对拍」的对象。上一版 `ComputeTwoModes(1000)` 返回两个**手搓常量**,
        /// 连字段都不全对等(`b` 多填 `SumPayload/MinTick/MaxTick`),断言却只比两个字段 —— 已删除。
        /// <para>解冻条件:`FOLD_MEMORY_MODE` 裁定并落地两实现后,本测改为同输入跑两实现逐位比对。</para></remarks>
        [Test]
        public void test_d6_scaleDoesNotChangeValues_NOT_RUN()
        {
            Assert.Ignore("AC-51-D6 NOT-RUN:FOLD_MEMORY_MODE 仍 *待定* ⇒ 无两模式可对拍。" +
                          "上一版以手搓常量冒充,且两常量字段不对等。");
        }

        // ══════════════ AC-51-D7:ConfigVersion 不匹配 ══════════════

        /// <summary>AC-51-D7:ConfigVersion 不匹配 —— 报告含显式「版本不匹配」标注,不静默出数。</summary>
        /// <remarks>⚠️ <b>NOT-RUN(禁借绿)</b>:须有「读存档头 ConfigVersion → 与当前内容哈希比对 →
        /// 报告出标注」的生产路径。51 的装配层现无此路径(ConfigVersion 的比对归 7a / ADR-010 §七)。
        /// 上一版用本地布尔 + 三元表达式拼字符串再断言 contains —— **恒真**,零判别力。</remarks>
        [Test]
        public void test_d7_configVersionMismatch_flagged_NOT_RUN()
        {
            Assert.Ignore("AC-51-D7 NOT-RUN:51 无「ConfigVersion 比对 → 报告标注」生产路径" +
                          "(归 7a / ADR-010 §七)。上一版为本地布尔 + 三元拼串,恒真。");
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
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } });

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
                new[] { new DiseaseRegistryEntry { DiseaseId = 1, DiseaseKey = "d1" } });

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
