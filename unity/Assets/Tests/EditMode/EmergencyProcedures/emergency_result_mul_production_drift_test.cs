// 生产急救驱动的 `ResultMul[Missed]` 违反已裁定数值的判据(2026-10-23 round33)。
//
// ── 病灶(实测,不是读注释推的) ────────────────────────────────────────────────
// GDD `emergency-procedures.md` 三处**已裁定** `ResultMul[Missed] = 0.25`(raw 16384):
//   `:639` 表行「`Missed` | **0.25** | ⚠️ 非零,刻意为之 —— 抖了也不至于全废」
//   `:856` 「`ResultMul[]` 已定:`Applied = 1.0` / `AppliedWeak = 0.5` / `Missed = 0.25`」
//          「⚠️ `Missed` 非零 = 手感宽容 … **改回 0 须重开 `OQ-10-1`**」
//   `:1049` **AC-10-16 BLOCKING**「恰为三档 raw long **`{65536, 32768, 16384}`** 且第三档 ≠ 0」
// 且 `OQ-10-1` 已于 **2026-09-18 由用户裁定**为非零(`:1068`「✅ 已结清」)。
//
// **但生产急救驱动自己硬编码了一份,把 `Missed` 写成 0**:
//   `Gameplay.Boot/EmergencyManualDriver.cs:84`
//       ResultMul = new long[] { 0, 32768, 65536 }; // Missed/AppliedWeak/Applied
// 而它是**活的生产路径**:`CompositionRoot.cs:260` 装配 → `BootRoot.cs:285` 绑到
// 候选列表「跳过」→ `CasebookCommandRouter.SubmitEmergencySkip` → `TryManual(method:1)`。
//
// ── 为什么既有测试全绿却没抓到 ────────────────────────────────────────────────
// `judge_test.cs:322-328` 断言的 `Missed × 0.25` 用的是
// `EmergencyActionSchema.GetResultMulTiers()` —— 那是 **schema 默认值**,不是生产那一行。
// 生产行的 `ResultMul` 是**另一份手写副本**,零测试读它。
// ⇒ 典型形态:**判据测的是默认表,生产用的是副本**,两者漂移无人可见。
//
// ── 本文件做什么 ──────────────────────────────────────────────────────────────
// ① 用**真** `EmergencyManualDriver` + 真 `HostEmergencyProcessor` + 真 codec 黑盒驱动,
//    从流里解出 `EmergencyTreatmentApplied.drugPotency`,断言它等于
//    `BASE_POTENCY × 0.25`(即 GDD 的 Missed 档),**不是** 0;
// ② 同批钉住另外两档(AppliedWeak / Applied)以证明本判据不是「全 0 也红」的粗糙断言;
// ③ 钉住 GDD 三档的**相对序**(`Missed < AppliedWeak < Applied`,AC-10-10b);
// ④ 前提:`GetResultMulTiers()`(schema 默认)本身符合 GDD —— 否则 ① 的归因不成立
//    (会把「schema 也错」误读成「生产偏了」)。
//
// ── 状态(2026-10-23 · round34)────────────────────────────────────────────────
// ✅ **已修绿**。用户裁定 ② = 「改回 16384 **+** 消掉副本」:
//    `EmergencyManualDriver.cs:104` 现为 `ResultMul = EmergencyActionSchema.GetResultMulTiers()`,
//    **生产侧手写副本已消失**(判据:`grep -rn "ResultMul = new long\[\]" --include=*.cs
//    unity/Assets/ | grep -v /Tests/ | grep -v "///"` ⇒ 零命中;排除 `///` 是因为本类
//    `<remarks>` 的历史记述文本会命中,注释不是执行体)。
//    ⇒ 本文件 5 条**全绿**是它的正常状态。
//
// **本轮(round33)的职责只是让漂移可见** —— 当时不代为修改生产代码,因为该行属于 10 的
// 数值面,且 GDD 明写「改回 0 须重开 OQ-10-1」,反方向同理须显式记账。用户裁定后才执行。
//
// ⚠️ **本文件的判据不得因修绿而放宽**:它钉的是**绝对值**(16384),不是偏序 ——
//    若将来有人把生产行改回手写副本或改回 0,`②` 与 `③` 会重新变红。
//
// 权威来源:
//   `design/gdd/emergency-procedures.md` `:639` / `:856` / `:1049`(AC-10-16)/ `:1068`(OQ-10-1)
//   `unity/Assets/Gameplay.Boot/EmergencyManualDriver.cs:84`(**修前**的生产副本行号;
//     round34 后该处已改为引用 schema ⇒ 现指针见 `:104` + `<remarks>` 的溯源记录)
//   `unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs:212`(schema 默认)

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Input.Intents;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.EmergencyProcedures
{
    /// <summary>
    /// 生产急救驱动的 `ResultMul` 与 GDD 已裁定值的**一致性**判据。
    /// </summary>
    [TestFixture]
    internal sealed class EmergencyResultMulProductionDriftTest
    {
        private const int Actor = 1;
        private const int MagThreshold = 50;      // 与 BandageRow.MagThreshold 同值

        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private EventStream _stream;
        private EmergencyManualDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _stream = new EventStream(new SeqIdAuthority(), new PresentPresence());
            var processor = new HostEmergencyProcessor(_stream, new SeqIdAuthority(), _encoder);
            _driver = new EmergencyManualDriver(processor, new PresentPresence());
        }

        /// <summary>从流里解出唯一一条 <c>EmergencyTreatmentApplied</c> 的 <c>drugPotency</c> raw。</summary>
        private long AppliedPotencyRaw()
        {
            SimEvent e = _stream.Events.Single(x => x.Kind == EventKind.EmergencyTreatmentApplied);
            return _decoder.Decode<EmergencyTreatmentAppliedPayload>(
                EventKind.EmergencyTreatmentApplied, e.Payload).DrugPotency.Raw;
        }

        /// <summary>用给定读数驱动一次真动作,返回落流 potency raw。</summary>
        /// <param name="magPeak">幅度峰值 —— &lt; MagThreshold ⇒ 幅度门不过 ⇒ Missed。</param>
        /// <param name="holdTicks">持时 —— &lt; MinHoldTicks(5) 且 edges 不足 ⇒ 节奏门不过 ⇒ Missed。</param>
        private long DriveOnce(int magPeak, int holdTicks, int edges, long tick)
        {
            var agg = new AggregatedEmergency(
                action: 0, holdTicks: holdTicks, edges: edges,
                edgeTicks: edges <= 1 ? new[] { (int)tick } : new[] { (int)tick, (int)tick + 3 },
                magPeak: magPeak, magLast: magPeak);

            Assert.That(_driver.TryAggregated(agg, Actor, new PatientId(1), tick).Applied,
                Is.True, "前提:动作应被受理(病人在场 + 输入合法)");
            return AppliedPotencyRaw();
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 前提:schema 默认表本身符合 GDD
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 前提判据:`GetResultMulTiers()`(schema 默认)恰为 GDD AC-10-16 的三档
        /// `{65536, 32768, 16384}`。
        /// </summary>
        /// <remarks>
        /// <b>为什么单列</b>:若 schema 默认也错,下面 ① 的红就不能归因到「生产用了副本」——
        /// 可能是「两处都错」。本条把「默认表是对的,错的是生产副本」这个前提钉死。
        /// </remarks>
        [Test]
        public void test_schemaDefaultTiers_matchGdd_premise()
        {
            long[] tiers = EmergencyActionSchema.GetResultMulTiers();

            Assert.That(tiers.Length, Is.EqualTo(3), "DC-6:恰三档");
            Assert.That(tiers[(int)JudgeResult.Missed], Is.EqualTo(16384),
                "前提:AC-10-16 的第三档 = 16384(0.25),**≠ 0**");
            Assert.That(tiers[(int)JudgeResult.AppliedWeak], Is.EqualTo(32768), "0.5");
            Assert.That(tiers[(int)JudgeResult.Applied], Is.EqualTo(65536), "1.0");
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 核心:生产驱动的 Missed 档
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// <b>核心判据</b>:生产 <see cref="EmergencyManualDriver"/> 判出 Missed 时,
        /// 落流的 `drug_potency` 须为 `BASE_POTENCY × 0.25`(= 16384),**不得为 0**。
        /// </summary>
        /// <remarks>
        /// 触发路径:幅度门不过(`magPeak &lt; MAG_THRESHOLD`)⇒ `Judge` 直接返回 `Missed`
        /// (`JudgeEvaluator.cs:112`)⇒ `HostEmergencyProcessor` 用**生产行**的
        /// `ResultMul[(int)Missed]` 缩放 potency。
        /// <para><b>当前应绿</b>(round34 裁定 ② 后):生产行已引用 schema 默认表 ⇒ 16384。
        /// 修前它是红的 —— 生产行 `EmergencyManualDriver.cs:84` 写的是 `0` ⇒ potency = 0,
        /// 直接违反 GDD `:856`「`Missed` 非零 = 手感宽容」与 AC-10-16「第三档 ≠ 0」。
        /// ⚠️ 本判据仍须保持**绝对值**语义:`OQ-10-1` 已由用户裁定为非零,
        /// **改回 0 须重开该 OQ**,不能由实现侧悄悄改。</para>
        /// </remarks>
        [Test]
        public void test_productionDriver_missed_potencyIsQuarter_notZero()
        {
            long potency = DriveOnce(magPeak: 0, holdTicks: 10, edges: 1, tick: 100);

            Assert.That(potency, Is.EqualTo(16384),
                "GDD `:856` / AC-10-16:Missed 档 = 0.25 ⇒ `BASE_POTENCY(65536) × 0.25` = 16384。\n" +
                "实测 " + potency + " —— 若为 0,则生产行 `EmergencyManualDriver.cs:84` 的\n" +
                "`ResultMul = { 0, 32768, 65536 }` 与 GDD 漂移(GDD 明写「改回 0 须重开 OQ-10-1」,\n" +
                "而 OQ-10-1 已于 2026-09-18 由用户裁定为非零)。\n" +
                "⇒ 「手稳」在 Missed 时被归零,与 GDD 刻意记账的「抖了也不至于全废」相反");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 另两档(证明 ① 不是「全 0 也红」的粗糙断言)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 对照:Applied 档 = 1.0(65536)。证明生产行的前两档**是对的**,
        /// ① 的红只归因于第三档,不是整行都错。
        /// </summary>
        [Test]
        public void test_productionDriver_applied_potencyIsFull()
        {
            long potency = DriveOnce(magPeak: 100, holdTicks: 10, edges: 1, tick: 100);

            Assert.That(potency, Is.EqualTo(65536),
                "对照:Applied 档 = 1.0 ⇒ potency = BASE_POTENCY = 65536。" +
                "本条绿证明生产行前两档正确 ⇒ ① 的红只归因第三档");
        }

        /// <summary>
        /// 对照:AppliedWeak 档 = 0.5(32768)。
        /// </summary>
        /// <remarks>
        /// 触发:节奏过(edges ≥ 1)、幅度过,但稳度门不过 —— `edges &gt; 1` 且 jitter 超容差。
        /// 用三沿等距(零抖动)**不会**触发,故此处构造**不等距**沿。
        /// </remarks>
        [Test]
        public void test_productionDriver_appliedWeak_potencyIsHalf()
        {
            // 三沿、间距极不均匀 ⇒ jitter 大 ⇒ 稳度门不过 ⇒ AppliedWeak。
            var agg = new AggregatedEmergency(
                action: 0, holdTicks: 10, edges: 3,
                edgeTicks: new[] { 100, 101, 140 },   // 间距 1 与 39
                magPeak: 100, magLast: 100);

            Assert.That(_driver.TryAggregated(agg, Actor, new PatientId(1), 100).Applied, Is.True,
                "前提:动作应被受理");
            long potency = AppliedPotencyRaw();

            Assert.That(potency, Is.EqualTo(32768),
                "对照:AppliedWeak 档 = 0.5 ⇒ 32768");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 三档相对序(AC-10-10b)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// AC-10-10b:三档 potency 严格递增 `Missed &lt; AppliedWeak &lt; Applied`。
        /// </summary>
        /// <remarks>
        /// 本条用**生产驱动实跑**的三次落流值,而非读表 —— 它同时是 ① 的强化:
        /// 若 `Missed = 0`,该不等式**仍成立**(0 &lt; 32768 &lt; 65536),所以本条**不会**
        /// 替代 ①,只是把「偏序关系」这层单独钉住(改回 0 会破坏 GDD 的**绝对值**裁定,
        /// 但不会破坏偏序 —— 两个判据各管一层)。
        /// </remarks>
        [Test]
        public void test_threeTiers_strictlyIncreasing_acrossProductionDriver()
        {
            long missed = DriveOnce(magPeak: 0, holdTicks: 10, edges: 1, tick: 100);

            SetUp();   // 新流,避免上一条的事件干扰 Single
            var aggWeak = new AggregatedEmergency(
                action: 0, holdTicks: 10, edges: 3,
                edgeTicks: new[] { 200, 201, 240 }, magPeak: 100, magLast: 100);
            _driver.TryAggregated(aggWeak, Actor, new PatientId(1), 200);
            long weak = AppliedPotencyRaw();

            SetUp();
            long applied = DriveOnce(magPeak: 100, holdTicks: 10, edges: 1, tick: 300);

            Assert.That(missed, Is.LessThan(weak), "Missed < AppliedWeak");
            Assert.That(weak, Is.LessThan(applied), "AppliedWeak < Applied");
            Assert.That(missed, Is.GreaterThan(0),
                "GDD `:856`:三支均 **> 0** —— 这一半是 ① 的绝对值裁定,本条只作交叉印证");
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具桩
        // ══════════════════════════════════════════════════════════════════

        private sealed class SeqIdAuthority : IIdAuthority
        {
            private int _next;
            public PatientId NextPatientId() => new PatientId(_next++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_next++);
        }

        /// <summary>在场恒真 —— 急救前置须病人在场(`EmergencyManualDriver.cs:150`)。</summary>
        private sealed class PresentPresence : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 1;
            public bool IsPresentAt(WorldPos cell) => true;
            public IReadOnlyCollection<int> PresentPatientIds() => new[] { 1 };
        }
    }
}
