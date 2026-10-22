// `LowPositionHeldTicks` —— F4 痊愈支条件 ⑥ 的输入(2026-10-22 B2 修复轮立)。
//
// 权威来源:
//   GDD disease-simulation.md:1114 —— 「低位须稳定 ≥ W(防 ±σ 抖动把终态判成随机)」
//   GDD disease-simulation.md:575 —— 痊愈行:…且低位稳定 ≥ `W` 触发(终态判定,B5)
//   GDD disease-simulation.md:1767 —— `RECOVER_THRESHOLD` 行:低位稳定 ≥ `W` 判痊愈
//   GDD disease-simulation.md:1743 —— `W` *待定*(归数值轮)
//   ADR-010 §三 —— 存档不存逐帧状态 ⇒ 累计量必须可重建
//   ADR-016 §九 —— 积分量的教训:「冻结态可由三源重放复现 ≠ 从不冻结态」
//
// ── 本文件钉的是什么 ──────────────────────────────────────────────────────────
// B 代理勘察发现:`RecoveryWindowInput.LowPositionHeldTicks` **全库零递增点** ——
// 生产路径恒传 0 ⇒ `held < W` 恒真 ⇒ **即使数值轮填了 W > 0,痊愈支仍永不成立**
// (只有迁延支会生效)。这是「管道通了但功能还是死的」的典型形态。
//
// 修法取舍:**纯函数,不是跨 tick 计数器**。
//   · 计数器形态 = 积分量 ⇒ 跳过 step 少积分;存档不携带(ADR-010)⇒ 读档后归零
//     ⇒ **同一时刻的同一病人判出两种终态**。这是 ADR-016 §九 已就地证伪过的假设。
//   · 纯函数形态 = 反查历史 tick 的 Progress ⇒ 可重放、无隐藏状态、读档后一致。
//
// 本文件钉五件事:
//   ① 定义(连续低位 tick 数,往回数到第一个高位为止);
//   ② 上界截断到 W(消费者只判 `held < W`,截断不改变判定,但把开销从 O(病程) 降到 O(W));
//   ③ 下限 = onset(病程之前无「低位」可言);
//   ④ 未配置(W ≤ 0)⇒ 返回 0 且不扫(零开销的诚实值);
//   ⑤ **可重放**:同一 t 无论先前经过哪些 tick,结果相同(纯函数性的直接判据)。
//
// ⚠️ 本文件**不**验证「痊愈支能判出来」—— 那需要 W > 0,而 W 归数值轮。
//   本文件只证明「条件 ⑥ 的输入现在真的是一个会变化的量,不再恒 0」。

using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.DiseaseSimulation
{
    /// <summary>`ComputeLowPositionHeldTicks` 的定义、截断、下界与纯函数性。</summary>
    public class LowPositionHeldTest
    {
        private const long OnsetTick = 0;

        /// <summary>
        /// 自限型曲线:`τ &lt; τ_peak` 上升,`τ ≥ τ_peak` 后快速衰减到低位。
        /// ⇒ position 的形状 = 「先高后低」,便于把「连续低位」做成 tick 的函数。
        /// </summary>
        private static DiseaseRegistryEntry RisesThenFalls()
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 81,
                DiseaseKey = "DIS_HELD_RISE_FALL",
                Kind = DiseaseKind.Disease,
                Lethality = 0,
                Scale = new Fix(100 * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(80 * Fix.OneRaw),
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(20 * Fix.OneRaw),   // 过峰后快速衰减到低位
                    Plateau = false,
                    SelfLimit = true,
                    BoundaryMode = "monotone",
                },
            };
        }

        /// <summary>全程高位的急性保持型(position 恒 = A_peak/SCALE &gt; RECOVER)。</summary>
        private static DiseaseRegistryEntry AlwaysHigh()
        {
            return new DiseaseRegistryEntry
            {
                DiseaseId = 82,
                DiseaseKey = "DIS_HELD_ALWAYS_HIGH",
                Kind = DiseaseKind.Disease,
                Lethality = 2,
                Scale = new Fix(100 * Fix.OneRaw),
                Curve = new NaturalProgressCurve
                {
                    Incubation = 0,
                    APeak = new Fix(80 * Fix.OneRaw),      // 0.80 ≫ RECOVER(0.12)
                    TauRise = new Fix(10 * Fix.OneRaw),
                    TauPeak = 100,
                    TauFall = new Fix(100 * Fix.OneRaw),
                    Plateau = false,
                    SelfLimit = false,                     // 急性保持型 ⇒ 过峰后恒 A_peak
                    BoundaryMode = "monotone",
                },
            };
        }

        private static long Held(DiseaseRegistryEntry entry, long currentTick, long w)
            => ProgressionEvaluator.ComputeLowPositionHeldTicks(
                entry, OnsetTick, currentTick,
                new List<TreatmentDose>(), worldSeed: 1, new PatientId(1),
                windowTicks: w);

        /// <summary>独立复算某 tick 的 position(F2 投影)—— 与实现不同路径。</summary>
        private static long PositionRaw(DiseaseRegistryEntry entry, long tick)
        {
            ProgressionResult r = ProgressionEvaluator.Evaluate(
                entry, OnsetTick, tick, new List<TreatmentDose>(), worldSeed: 1, new PatientId(1));
            Fix p = r.Position / entry.Scale;
            if (p.Raw < Fix.ZeroRaw) p = Fix.Zero;
            if (p.Raw > Fix.OneRaw) p = Fix.One;
            return p.Raw;
        }

        // ══════════════════ ① 定义:往回数到第一个高位为止 ══════════════════

        /// <summary>
        /// **核心判据**:返回值 = 从 t 往回数、position 连续低于 RECOVER 的 tick 数。
        /// <para>独立复算:从 t 起逐 tick 往回调 <c>Evaluate</c> 取 position,数到第一个
        /// <c>≥ RECOVER_THRESHOLD</c> 为止 —— 与实现走不同代码路径。</para>
        /// </summary>
        [Test]
        public void test_held_matchesManualBackwardCount()
        {
            DiseaseRegistryEntry entry = RisesThenFalls();
            const long t = 400;                 // 远过 τ_peak,早已衰减到低位
            const long w = 200;

            long expected = 0;
            for (long back = 0; back < w; back++)
            {
                if (PositionRaw(entry, t - back) >= StateMachine.RECOVER_THRESHOLD) break;
                expected++;
            }

            Assert.AreEqual(expected, Held(entry, t, w),
                "返回值须等于「从 t 往回数连续低位 tick 数」—— 数到第一个高位即止");
            Assert.Greater(expected, 0, "前置:本夹具在 t=400 应确实处于低位");
        }

        // ══════════════════ ② 上界截断到 W ══════════════════

        /// <summary>
        /// 低位持续远超 W ⇒ 返回值**恰 = W**(不是真实持续数)。
        /// <para>⚠️ 截断是刻意的:消费者只判 <c>held &lt; W</c>,超出部分不影响判定,
        /// 却把开销从 <c>O(病程长度)</c> 降到 <c>O(W)</c>。若实现返回真实值(比如 500),
        /// 本条红 —— 那不是 bug,是本文件在钉「截断」这条契约。</para>
        /// </summary>
        [Test]
        public void test_held_cappedAtWindow()
        {
            DiseaseRegistryEntry entry = RisesThenFalls();
            const long t = 5000;                // 低位已持续数千 tick
            const long w = 64;

            Assert.AreEqual(w, Held(entry, t, w),
                "低位持续 ≫ W ⇒ 返回值截断到 W(消费者只判 held < W,截断不改变判定)");

            // ⚠️ 断言值必须跟着形参走。初版此处写 `AreEqual(w, Held(..., w * 2))` ——
            //    传进去的是 2W 却断言 W,**测试自己写错了**(实测 4622/4585/**1**/36 时红在这)。
            //    实现返回 128 是对的;错的是期望值。保留此注作为「先怀疑实现还是先怀疑测试」的提醒:
            //    本条的失败信息里 `Expected 64 / But was 128` 与「截断到 W」的定义完全吻合 ⇒ 先查测试。
            Assert.AreEqual(w * 2, Held(entry, t, w * 2),
                "换一个 W ⇒ 截断值随之变(2W ⇒ 2W)—— 证明它真的跟着 W 走,不是巧合");
        }

        // ══════════════════ ③ 全程高位 ⇒ 0 ══════════════════

        /// <summary>position 从未跌破痊愈门 ⇒ 连续低位 = 0。</summary>
        [Test]
        public void test_held_alwaysHigh_isZero()
        {
            DiseaseRegistryEntry entry = AlwaysHigh();
            Assert.AreEqual(0L, Held(entry, 300, w: 100),
                "position 恒 ≥ RECOVER ⇒ 一个低位 tick 都没有 ⇒ 0");
        }

        // ══════════════════ ④ 下限 = onset ══════════════════

        /// <summary>
        /// 病程之前不计 —— 扫描下限是 <paramref name="OnsetTick"/>。
        /// <para>本夹具 <c>Incubation = 0</c>,故 onset 处 position 已由曲线决定;
        /// 关键是 <c>currentTick = onset</c> 时返回值 ≤ 1,不会因为「往回数到 −∞」而爆表。</para>
        /// </summary>
        [Test]
        public void test_held_atOnset_isAtMostOne()
        {
            DiseaseRegistryEntry entry = RisesThenFalls();
            long held = Held(entry, OnsetTick, w: 100);
            Assert.LessOrEqual(held, 1L,
                "t = onset ⇒ 最多只有 onset 这一个 tick 可数(不得越过病程起点往回数)");
        }

        /// <summary>t &lt; onset(理论上不该发生)⇒ 0,不抛。</summary>
        [Test]
        public void test_held_beforeOnset_isZero()
        {
            Assert.AreEqual(0L, Held(RisesThenFalls(), OnsetTick - 50, w: 100),
                "求值时刻早于病程起点 ⇒ 无「已持续」可言 ⇒ 0(不抛,不在 tick 循环里制造崩溃)");
        }

        // ══════════════════ ⑤ 未配置(W ≤ 0)⇒ 0 且不扫 ══════════════════

        /// <summary>
        /// `W` 未配置 ⇒ 返回 0。
        /// <para>⚠️ 这不是「低位 0 tick」的断言 —— <see cref="RecoveryWindowInput.IsConfigured"/>
        /// 为 <c>false</c> 时条件 ⑥ 根本不被求值。返回 0 只是零开销的诚实值。</para>
        /// </summary>
        [Test]
        public void test_held_unconfiguredWindow_returnsZero()
        {
            DiseaseRegistryEntry entry = RisesThenFalls();
            Assert.AreEqual(0L, Held(entry, 5000, w: 0), "W = 0 ⇒ 未配置 ⇒ 0");
            Assert.AreEqual(0L, Held(entry, 5000, w: -7), "W < 0 ⇒ 同上(不抛:装配期已由 R1-36 拒)");
        }

        // ══════════════════ ⑥ 纯函数性:同一 t 结果与到达路径无关 ══════════════════

        /// <summary>
        /// **本文件最重要的一条**:同一 <c>t</c> 的返回值**与「之前经过哪些 tick」无关**。
        /// <para>这是纯函数形态相对跨 tick 计数器的**唯一实质优势** —— 计数器形态下,
        /// 若 step 被跳过(离屏 / 冻结 / 读档),计数就少加,同一 t 会得到不同值
        /// (ADR-016 §九 已就地证伪「冻结不改变判定」的同型假设)。</para>
        /// <para>判据形态:连算三次,中间穿插**不同**的历史求值 —— 三次结果必须全等。
        /// 若实现偷偷持有可变状态,穿插的历史求值会改变它 ⇒ 红。</para>
        /// </summary>
        [Test]
        public void test_held_isPure_sameTickIndependentOfHistory()
        {
            DiseaseRegistryEntry entry = RisesThenFalls();
            const long t = 800;
            const long w = 100;

            long first = Held(entry, t, w);

            // 穿插大量**别的** tick 求值(含 t 之前与之后)—— 纯函数不受影响
            for (long other = 0; other < 300; other += 7) Held(entry, other, w);
            Held(entry, t + 1000, w);
            Held(entry, OnsetTick, w);

            long second = Held(entry, t, w);

            // 再来一轮,顺序相反
            for (long other = 299; other >= 0; other -= 11) Held(entry, other, w);
            long third = Held(entry, t, w);

            Assert.AreEqual(first, second,
                "同一 t 的 held 不得受「之前求过哪些 tick」影响 —— 否则它就是积分量," +
                "跳过 step / 读档后会判出不同的终态(ADR-016 §九 同型)");
            Assert.AreEqual(first, third, "同上,反向遍历后仍须相等");
        }

        // ══════════════════ ⑦ SCALE ≤ 0 不崩 ══════════════════

        /// <summary>
        /// `SCALE ≤ 0` 的坏条目 ⇒ 不抛、不除零,按「position 不可求值 ⇒ 视为低位」处理。
        /// <para>与 <c>EvaluateOutcome</c> 同口径:条目坏由 R1-23 在写入期拦,
        /// tick 循环内不该因为一个坏条目让整条 Step 崩。</para>
        /// </summary>
        [Test]
        public void test_held_nonPositiveScale_doesNotThrow()
        {
            DiseaseRegistryEntry entry = RisesThenFalls();
            entry.Scale = Fix.Zero;

            Assert.DoesNotThrow(() => Held(entry, 500, w: 100),
                "SCALE ≤ 0 不得在 tick 循环里抛(坏条目归 R1-23 写入期)");
        }
    }
}
