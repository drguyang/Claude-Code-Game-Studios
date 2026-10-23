// ADR-005 / GDD disease-simulation.md §F1 —— 病程求值器。
//
// 权威来源:
//   ADR-005 §Key Interfaces —— 整数定点域求值
//   GDD disease-simulation.md §F1 —— Base/Relapse/Decay 病程求值(三分支 + R_rise 归一化)
//   GDD disease-simulation.md §F2 —— 体征投影与通道
//   GDD disease-simulation.md §F0 —— 「定点 Exp 必须计入实现量 —— F1/F2 通篇依赖它」
//     ⇒ Base / Relapse / Decay 的指数全走 `Fix.Exp`(批次 A;禁 float / libm / Math.Exp)
//   ADR-024 §① —— 事件白名单按 Kind 路由;本类**不读事件流**(解码不在 Sim,见下)
//
// 核心机制:
//   - 两层模型:Progress(病理)与 Signs(体征)分离
//   - 对因处置贡献 = Σ drug_potency × Decay(Δ) · [polarity = causal] · [处置 ∈ treatable_by(d)]
//   - 对症处置只改 Signs(⇒ 本类对 polarity = symptomatic 恒零贡献)
//   - Noise = (seed, patient, tick) 纯函数
//   - 潜伏期噪声被抑制(τ < incubation ⇒ Progress ≡ 0)
//
// ── 批次 C(2026-10-09)两处「接真」与三处「保留占位」────────────────────────
// 接真:
//   ① **曲线面**:条目带 `Curve`(批次 B 起的合法形态)⇒ 走 GDD F1 的三分支
//      (潜伏 / 上升经 R_rise 归一 / 衰减 · plateau · 急性保持),指数经 `Fix.Exp`;
//      `Relapse` 块同理。`Curve == null` 的**既有占位条目**仍走下方 `*Legacy*` 简化形
//      (不改既有测试的可判读面 —— 那些条目连 A_peak / τ_peak 都没声明)。
//   ② **处置和式**:新重载收**已解码**的 `TreatmentDose`,`ComputeDrugContribution` 真算
//      Decay(经 `Fix.Exp`)+ 极性门 + `treatable_by` 门 + 对称 clamp(界可选,见下)。
//      「恒 0」在**生产路径上**已消除。
// 保留占位(tripwire,归「M2 可玩级另计」· 阶段 0 口径 —— **不得当成本批缺陷删改**):
//   · `ComputeNoise` 恒 0 · `ComputeTrend` 恒 0 · `signs[]` 恒空(F2 投影未接线)
//   · `CatchUp` 不动(排序壳保留;**NOT-RUN 背离已登记 2026-10-10 批次 F** ——
//     GDD `disease-simulation.md` §F3 节首 + AC-3/3b/3c 行,归 M3/后续)
//
// ── 为什么存在两个 Evaluate 重载(既有公共面兼容)──────────────────────────
// 既有签名 `Evaluate(..., IReadOnlyList<SimEvent> events, ...)` 的 `events` 形参
// **在 Sim 内无法解码**(Sim 只见 Sim.Contracts,b2 门),故它从来只是一条兼容壳:
// 批次 C 起显式转交剂量重载,`events` 不再假装参与求值(原 `ComputeDrugContribution`
// 收 `events` 却恒 0 = 更坏的形态)。**生产路径走剂量重载**(组合根的
// `DiseaseVitalsService` 解码后传入);既有测试继续用事件重载,行为逐位不变。
// ⚠️ 若将来有第二个消费方想从事件重载拿到剂量,正确做法是**在边界解码后调剂量重载**,
//    不是把 codec 引进 Sim(那会拆 b2 门)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.DiseaseSimulation;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 病程求值结果。
    /// </summary>
    public readonly struct ProgressionResult
    {
        public readonly Fix Position;
        public readonly Fix Trend;
        public readonly int[] Signs;

        public ProgressionResult(Fix position, Fix trend, int[] signs)
        {
            Position = position;
            Trend = trend;
            Signs = signs;
        }
    }

    /// <summary>
    /// 病程求值器 —— F1 求值式。
    /// </summary>
    public static class ProgressionEvaluator
    {
        // AC-12: SCALE ≥ A_peak 时 position ∈ [0,1]
        public const int SCALE = 10000; // 定点域缩放因子

        /// <summary>
        /// 极性序数「对因」的唯一取值(载荷 `polarity` 字段)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **序数出处**:<c>DaYiJingCheng.Sim.Prescription.PrescriptionPolarity</c>
        /// (11 的作者态声明)定 `Symptomatic = 0 · Causal = 1` —— `Sim.Contracts` 刻意不造枚举
        /// (防 ordinal 表第二真源,见 PrescribeFlow 头注),载荷侧是裸 int。
        /// <b>缺口登记</b>:10 的 `EmergencyActionRow.Polarity` 是**另一张表**的「枚举序数」,
        /// GDD(`emergency-procedures.md:203`)只写集合 `{causal, symptomatic}` **未声明序数映射**
        /// ⇒ 急救侧若与 11 反向,对因门会静默反号。本批不自造映射,按 11 的既有声明取值;
        /// 序数统一归各 GDD 轮裁定(见任务报告「GDD 缺口」节)。
        /// </remarks>
        public const int PolarityCausal = 1;

        /// <summary>空剂量(F1 和式无可遍历项)。</summary>
        private static readonly IReadOnlyList<TreatmentDose> NoDoses = new TreatmentDose[0];

        /// <summary>空 signs(恒占位 —— F2 投影未接线,tripwire)。</summary>
        private static readonly int[] NoSigns = new int[0];

        /// <summary>
        /// 事件形参兼容重载(见文件头「为什么存在两个 Evaluate 重载」)。
        /// </summary>
        /// <remarks>
        /// `<paramref name="events"/>` 不参与求值 —— Sim 内无解码路径(b2 门);
        /// 处置贡献须经下方剂量重载由**边界解码后**传入。行为与批次 C 之前逐位一致。
        /// </remarks>
        public static ProgressionResult Evaluate(
            DiseaseRegistryEntry registry,
            long onsetTick,
            long currentTick,
            IReadOnlyList<SimEvent> events,
            ulong worldSeed,
            PatientId patientId)
        {
            return Evaluate(registry, onsetTick, currentTick, NoDoses, worldSeed, patientId);
        }

        /// <summary>
        /// 求值病程(F1 全式:Base ⊔ Relapse + clamp(Σ 对因处置) + Noise)。
        /// </summary>
        /// <param name="registry">病种注册表条目(直传,无 id 查找 —— 批次 B 裁定,勿造查找接口)。</param>
        /// <param name="onsetTick">病程起点(F1 `onset_tick`)。</param>
        /// <param name="currentTick">求值时刻 t。</param>
        /// <param name="doses">**已解码**的处置和式输入(可为 null = 无处置)。</param>
        /// <param name="worldSeed">世界种子(Noise 的纯函数入参;Noise 现恒 0,先传后用)。</param>
        /// <param name="patientId">病人 id(Noise / 逐病人纯函数入参)。</param>
        /// <param name="doseClampBound">
        /// F1 对称 clamp 界 `MAX_ACTIVE_DOSE × single_dose_max`(只罩 Σ,四轮 K1)。
        /// <b>null = 不 clamp</b> —— 两个旋钮的**取值均待定**(GDD Tuning §五 标 `*待定*`),
        /// <c>single_dose_max</c> 亦尚未进 Sim 装载面 ⇒ 本批不自造数值,登记为缺口;
        /// 非 null 时走完整饱和截断语义(由测试覆盖)。
        /// </param>
        /// <returns>求值结果(Position/Trend/Signs 全整数域)。</returns>
        public static ProgressionResult Evaluate(
            DiseaseRegistryEntry registry,
            long onsetTick,
            long currentTick,
            IReadOnlyList<TreatmentDose> doses,
            ulong worldSeed,
            PatientId patientId,
            Fix? doseClampBound = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // Progress —— 整条 F1 式(潜伏抑制 / Base ⊔ Relapse / 处置和 / Noise / 0 下界)
            Fix progress = ComputeProgress(
                registry, onsetTick, currentTick, doses, worldSeed, patientId, doseClampBound);

            // Trend —— **展示用中心差分**(F2),tripwire 恒 0。
            // ⚠️ 这不是 F4 吃的 `trend_backward` —— 后者见 ComputeTrendBackward。
            Fix trend = ComputeTrend(registry, currentTick - onsetTick);

            // Signs —— tripwire 恒空(F2 投影归 8 的词表/阈值,story 006)
            int[] signs = NoSigns;

            return new ProgressionResult(progress, trend, signs);
        }

        /// <summary>
        /// 在任意 tick 上求 `Progress`(F1 全式)—— <see cref="Evaluate"/> 与
        /// <see cref="ComputeTrendBackward"/> 的**唯一实现**。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么提出来(2026-10-22 第四轮)</b>:`trend_backward(t)` 按定义要读
        /// `Progress(t − W)`(GDD <c>:921</c>)。若在求趋势处**另写一份** F1 式,两份实现
        /// 一旦分叉,「同一个 tick 的 Progress」就会有两个值 —— 而趋势正是**用它自己的
        /// 那份**算出来的 ⇒ 判据与读数不自洽,且没有任何测试会红(两处各自都「对」)。
        /// ⇒ 唯一实现,两处共用(与 <see cref="IsEffectiveCausalDose"/> 同一纪律)。</para>
        ///
        /// <para><b>⚠️ 传入过去的 tick 是合法的</b>:`currentTick − W` 可以早于
        /// <paramref name="onsetTick"/> 甚至为负 —— 潜伏抑制的 <c>τ &lt; incubation</c>
        /// 分支会把它压成 0(GDD:病未起时 Progress 恒 0)。处置侧同理:
        /// <see cref="ComputeDrugContribution"/> 对 <c>Δ &lt; 0</c> 归零(AC-27 时间反演),
        /// 故「尚未给过的药」在过去的 tick 上贡献为 0 —— 这正是因果差分要的语义。</para>
        /// </remarks>
        private static Fix ComputeProgress(
            DiseaseRegistryEntry registry,
            long onsetTick,
            long currentTick,
            IReadOnlyList<TreatmentDose> doses,
            ulong worldSeed,
            PatientId patientId,
            Fix? doseClampBound)
        {
            long tau = currentTick - onsetTick;

            // ── 潜伏期抑制(AC-26):τ < incubation ⇒ Progress ≡ 0(含噪声与处置)──
            // 潜伏长度的真源 = `Curve.Incubation`(R1.3);`Curve == null` 的既有占位条目
            // 退化用 `RecoveryTime`(批次 C 之前的原口径,不改既有测试的判读面)。
            long incubation = registry.Curve != null ? registry.Curve.Incubation : registry.RecoveryTime;
            if (tau < incubation)
                return Fix.Zero;

            // Base(τ) —— 曲线面接真(三分支)/ 占位条目走简化形
            Fix baseValue = ComputeBase(registry, tau);

            // Relapse(τ)
            Fix relapseValue = ComputeRelapse(registry, tau);

            // AC-8: max 非和
            Fix mainCurve = baseValue.Raw > relapseValue.Raw ? baseValue : relapseValue;

            // 处置贡献(批次 C 接真:F1 的 Σ drug_potency×Decay · [causal] · [treatable_by])
            Fix drugContribution = ComputeDrugContribution(registry, doses, currentTick, doseClampBound);

            // Noise —— tripwire 恒 0(阶段 0 口径:归「M2 可玩级另计」)
            Fix noise = ComputeNoise(worldSeed, patientId, currentTick);

            // AC-12: Progress ≥ 0(max 的 0 下界在整式外侧,四轮 K1)
            Fix total = mainCurve + drugContribution + noise;
            return total.Raw > 0 ? total : Fix.Zero;
        }

        /// <summary>
        /// `trend_backward(t)` —— **状态转移用**的后向趋势(GDD <c>:921</c> / F4 / B5)。
        /// </summary>
        /// <remarks>
        /// <para><b>定义(GDD <c>:921</c> 逐字)</b>:
        /// <c>trend_backward(t) = (Progress(t) − Progress(t − W)) / W</c>。</para>
        ///
        /// <para><b>⚠️ 与 <see cref="ComputeTrend"/> 不是同一个量</b> —— 这是本方法存在的理由:</para>
        /// <list type="table">
        /// <item><term><c>trend</c>(中心)</term><description>
        /// <c>(P(t+W/2) − P(t−W/2)) / W</c> —— **读未来**,仅供 8 的展示读数。</description></item>
        /// <item><term><c>trend_backward</c>(单侧)</term><description>
        /// <c>(P(t) − P(t−W)) / W</c> —— **只读过去**,F4 的状态转移吃它。</description></item>
        /// </list>
        /// <para>GDD <c>:936-937</c> 明写「F4 在 tick t 上用未来时刻决定状态 = 非因果」,
        /// 故**状态转移不得用中心差分**。两者数值相近但**语义不同**,不可互相顶替。</para>
        ///
        /// <para><b>⚠️ W 未配置 ⇒ 返回 0,不求值</b>:<paramref name="windowTicks"/> ≤ 0
        /// 即未配置(GDD Tuning <c>:1743</c> 标 <c>*待定*</c>,归数值轮)。
        /// 用 0 当真窗口会退化成 <c>(P(t) − P(t)) / 0</c> ⇒ 除零;本方法**不代拍**。</para>
        ///
        /// <para><b>死区</b>:<c>|trend| &lt; TREND_EPS</c> ⇒ 判「持平」返回 0
        /// (GDD <c>:926-928</c>)。缺它则 plateau / 迁延段的 ±1 LSB 量化噪声会被读成
        /// 「在恶化」—— 恰在最需要趋势的心衰类病种上失真。</para>
        /// </remarks>
        /// <param name="windowTicks">`W`(tick)。≤ 0 ⇒ 未配置 ⇒ 返回 0。</param>
        /// <param name="trendEpsRaw">`TREND_EPS`(Q16.16 raw)。≤ 0 ⇒ 无死区。</param>
        /// <returns>后向趋势(Q16.16);未配置时为 <see cref="Fix.Zero"/>。</returns>
        public static Fix ComputeTrendBackward(
            DiseaseRegistryEntry registry,
            long onsetTick,
            long currentTick,
            IReadOnlyList<TreatmentDose> doses,
            ulong worldSeed,
            PatientId patientId,
            long windowTicks,
            long trendEpsRaw,
            Fix? doseClampBound = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // W 未配置 ⇒ 不求值(不代拍数值轮的量)
            if (windowTicks <= 0) return Fix.Zero;

            Fix now = ComputeProgress(
                registry, onsetTick, currentTick, doses, worldSeed, patientId, doseClampBound);
            Fix before = ComputeProgress(
                registry, onsetTick, currentTick - windowTicks, doses, worldSeed, patientId, doseClampBound);

            // 同标度 Q16.16 相减 —— 无舍入(两值都是 Q16.16,差仍是 Q16.16)
            Fix diff = now - before;

            // ⚠️ 量纲:trend = diff / W,其中 W 是 **tick 数**(整数),不是 Fix。
            //   故分母写成 `W × 2¹⁶` 的 Fix 值,走 `Fix.Div` 一次舍入
            //   (与 ExpNeg 的 `new Fix(Δ×2¹⁶) / τ` 同法 —— 若用 `Fix.FromRational(diffRaw, W)`
            //   则分子又被左移 16 位,结果放大 2¹⁶ 倍)。
            if (windowTicks > MaxRatioNumeratorTicks) return Fix.Zero;   // 防 `W×2¹⁶` 溢出
            Fix trend = diff / new Fix(windowTicks * Fix.OneRaw);

            // 死区:判「持平」返回 0,不返回 ±1 LSB 抖动
            if (trendEpsRaw > 0 && trend.Raw < trendEpsRaw && trend.Raw > -trendEpsRaw)
                return Fix.Zero;

            return trend;
        }

        /// <summary>
        /// `LowPositionHeldTicks` —— `position_agg` 已**连续**低于痊愈门的 tick 数
        /// (F4 痊愈支条件 ⑥ 的输入;GDD <c>:1114</c>「低位须稳定 ≥ W(防 ±σ 抖动把终态判成随机)」)。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么是纯函数,不是跨 tick 计数器(2026-10-22 B2 修复的核心取舍)</b>:
        /// 最直觉的实现是「每 tick 若 position 低则 <c>++</c>,否则归零」—— 那是一个**积分量**。
        /// ADR-016 §九 已就地证伪过同型假设:「冻结态可由三源重放复现 ≠ 从不冻结态」,
        /// 积分量一旦被跳过 step 就少积分,且**存档不携带它**(ADR-010 不存逐帧状态)
        /// ⇒ 读档后该值归零 ⇒ 同一时刻的同一病人判出两种终态。
        /// 本实现把「连续低位多久」写成 <c>t</c> 的**纯函数**(反查历史 tick 的 Progress),
        /// 与 <see cref="ComputeTrendBackward"/> 同构:只读过去、可重放、无隐藏状态。</para>
        ///
        /// <para><b>定义</b>(逐字对应 <see cref="RecoveryWindowInput.LowPositionHeldTicks"/> 的文档):</para>
        /// <code>
        /// held(t) = 自 t 往回数,position 连续 &lt; RECOVER_THRESHOLD 的 tick 数,
        ///           下限 = onsetTick + incubation(潜伏期内无「低位」可言 —— 乙案,OQ-9-1),
        ///           上限 = windowTicks(消费者只判 held &lt; W,超出部分不影响判定)。
        /// </code>
        ///
        /// <para><b>⚠️ 上界截断是刻意的</b>:返回值语义 = <c>min(真实连续低位 tick 数, W)</c>。
        /// 唯一消费者 <see cref="F4OutcomeEvaluator"/> 的条件 ⑥ 是 <c>held &lt; W ⇒ 不成立</c>,
        /// 故截断**不改变任何判定**,却把开销从 <c>O(病程长度)</c> 降到 <c>O(W)</c>
        /// (24 病人 × 20 Hz 下这是必要的 —— 否则病程越长每 tick 越慢)。</para>
        ///
        /// <para><b>⚠️ 未配置(<paramref name="windowTicks"/> ≤ 0)⇒ 返回 0 且不扫</b>:
        /// <see cref="RecoveryWindowInput.IsConfigured"/> 为 <c>false</c> 时条件 ⑥ 根本不被求值,
        /// 返回 0 是**零开销的诚实值**,不是「低位 0 tick」的断言。</para>
        ///
        /// <para><b>✅ 潜伏期边界已裁(乙案 · 2026-10-23 用户裁定,结清 OQ-9-1)</b>:
        /// 起算点 = <c>onset + incubation</c>,**不是** <c>onset</c>。
        /// 潜伏期内 <c>τ &lt; incubation</c> ⇒ <see cref="ComputeProgress"/> 返回 0 ⇒ position = 0
        /// &lt; RECOVER ⇒ 若从 <c>onset</c> 起算,<c>held</c> 会在**病还没发出来**时就攒满 <c>W</c>,
        /// 而条件 ⑤(非上升期门)在 <c>trend = 0</c> 时放行 ⇒ 判出「潜伏期痊愈」。
        /// 乙案消除了这条路径。</para>
        ///
        /// <para>🔁 <b>原文(甲案)保留留痕</b>:「已知边界(未裁,登记于 OQ-9-1)…
        /// 是否要额外要求 <c>τ &gt; incubation</c> 归 9 的设计轮裁定。**本函数不自作主张加门**。」
        /// —— 该裁定已于 2026-10-23 作出(乙案),本函数据此加门。</para>
        ///
        /// <para>⚠️ <b>乙案的具名代价(裁定当时已计入)</b>:它把「潜伏 → 痊愈」这条
        /// GDD <c>:567</c> 明列为**合法边**的路径也一并关掉了(该边的触发条件
        /// <c>self_limit ∧ ¬relapse</c> 与痊愈支条件 ②③④ 完全同源,而潜伏期内
        /// <c>trend</c> 恒 0、<c>held</c> 恒 0 &lt; W)。⇒ 今天没有任何病种能从潜伏期直接痊愈。
        /// 若将来要恢复该边,须**另裁**(不是改本函数一处即可 —— 它同时受条件 ⑤ 与 ⑥ 约束)。</para>
        /// </remarks>
        /// <param name="registry">病种注册表条目(与 F1 求值同一条目 —— 必须与
        /// <c>position_agg</c> 的来源一致,否则「低位」判的不是同一个量)。</param>
        /// <param name="onsetTick">病程起点(扫描下限 = <c>onsetTick + incubation</c>,见 remarks)。</param>
        /// <param name="currentTick">求值时刻 t(扫描起点)。</param>
        /// <param name="doses">已解码的处置和式输入(F1 求值需要)。</param>
        /// <param name="worldSeed">世界种子(Noise 入参;Noise 现恒 0)。</param>
        /// <param name="patientId">病人 id(Noise 入参)。</param>
        /// <param name="windowTicks"><c>W</c>;≤ 0 ⇒ 未配置 ⇒ 返回 0 且不扫。</param>
        /// <param name="doseClampBound">剂量钳制上界(F1 透传;生产路径为 null)。</param>
        /// <returns><c>min(连续低位 tick 数, W)</c>;未配置 / <paramref name="currentTick"/> &lt;
        /// <c>onsetTick + incubation</c>(仍在潜伏期)时为 0。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="registry"/> 为 null。</exception>
        public static long ComputeLowPositionHeldTicks(
            DiseaseRegistryEntry registry,
            long onsetTick,
            long currentTick,
            IReadOnlyList<TreatmentDose> doses,
            ulong worldSeed,
            PatientId patientId,
            long windowTicks,
            Fix? doseClampBound = null)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            // 未配置 ⇒ 消费者不求值条件 ⑥,零开销返回(见 remarks)
            if (windowTicks <= 0) return 0;

            // 病程尚未开始 / 同一 tick ⇒ 无「已持续」可言
            if (currentTick < onsetTick) return 0;

            // ── 起算点 = onset + incubation(2026-10-23 用户裁定「乙案」,结清 OQ-9-1)──
            //   潜伏期内 τ < incubation ⇒ `ComputeProgress` 恒返 0 ⇒ position = 0 < RECOVER
            //   ⇒ 若从 onset 起算,held 会在**病还没发出来**时就攒满 W,配合条件 ⑤ 在
            //   trend = 0 时放行,判出「潜伏期痊愈」(= OQ-9-1 的甲案行为)。
            //   ⚠️ incubation 的取法与 `ComputeProgress` 的潜伏期抑制**必须同一口径**
            //      (见 `registry.Curve == null` 的退化分支)—— 两处对「潜伏到哪为止」
            //      若取法不同,会出现「本函数已开始计数但 Progress 仍被抑制」的错位。
            long incubation = registry.Curve != null
                ? registry.Curve.Incubation
                : registry.RecoveryTime;
            long countingStart = onsetTick + incubation;

            // 仍在潜伏期内 ⇒ 尚未进入可计数的窗口(乙案的直接后果,见 remarks)
            if (currentTick < countingStart) return 0;

            // 扫描窗口 = min(W, 自起算点已过 tick 数 + 1) —— 两个上界都必要:
            //   · W   = 消费者只需要这么多(超出不影响 held < W 的判定);
            //   · 病程 = 不能越过**起算点**去数「低位」(潜伏期内没有「低位」可言)。
            long span = currentTick - countingStart + 1;
            long maxScan = windowTicks < span ? windowTicks : span;

            // SCALE ≤ 0 ⇒ position 不可求值(与 EvaluateOutcome 同口径:压 0,不在 tick 内抛)
            if (registry.Scale.Raw <= 0) return maxScan;

            long held = 0;
            for (long back = 0; back < maxScan; back++)
            {
                long t = currentTick - back;
                Fix progress = ComputeProgress(
                    registry, onsetTick, t, doses, worldSeed, patientId, doseClampBound);

                // F2 投影(与 GetVitals / EvaluateOutcome 同式:clamp(Progress / SCALE, 0, 1))
                Fix position = progress / registry.Scale;
                if (position.Raw < Fix.ZeroRaw) position = Fix.Zero;
                if (position.Raw > Fix.OneRaw) position = Fix.One;

                // 高位 ⇒ 连续段断开,往回数到此为止
                if (position.Raw >= StateMachine.RECOVER_THRESHOLD) break;
                held++;
            }

            return held;
        }

        // ── Base(τ) ──────────────────────────────────────────────────────────

        /// <summary>
        /// Base(τ) —— 单次发作的自然曲线(条目形态分派)。
        /// </summary>
        private static Fix ComputeBase(DiseaseRegistryEntry registry, long tau)
        {
            return registry.Curve == null
                ? ComputeBaseLegacy(registry, tau)
                : ComputeBaseF1(registry.Curve, tau);
        }

        /// <summary>
        /// F1 Base(τ) 三分支(GDD `disease-simulation.md` F1;批次 C 接真)。
        /// </summary>
        /// <remarks>
        /// <code>
        /// Base(τ) = 0                                        , τ &lt; incubation
        ///         = A_peak × (1 − e^(−(τ−incubation)/τ_rise)) / R_rise , incubation ≤ τ &lt; τ_peak
        ///         = A_peak × e^(−(τ−τ_peak)/τ_fall)           , τ ≥ τ_peak ∧ self_limit
        ///         = A_peak                                    , τ ≥ τ_peak ∧ plateau / 急性保持型
        ///   R_rise = 1 − e^(−(τ_peak − incubation)/τ_rise)    (常数;保证 τ_peak 处恰 = A_peak)
        /// </code>
        /// 指数全走 <see cref="Fix.Exp"/>(F0:定点 Exp 是 F1/F2 的通篇依赖)。
        /// </remarks>
        /// <exception cref="InvalidOperationException">`R_rise ≤ 0`(分段倒置 / 归一化失效)。
        /// GDD AC-23 的 `R_rise &gt; ε_MIN` 是**注册表写入期**门,当前未实现 ⇒ 求值侧 fail-loud,
        /// 不静默除零、不静默放大。</exception>
        private static Fix ComputeBaseF1(NaturalProgressCurve c, long tau)
        {
            if (tau < 0) return Fix.Zero;
            if (tau < c.Incubation) return Fix.Zero;

            if (tau < c.TauPeak)
            {
                Fix e = ExpNeg(tau - c.Incubation, c.TauRise);
                Fix rRise = Fix.One - ExpNeg(c.TauPeak - c.Incubation, c.TauRise);
                if (rRise.Raw <= 0)
                    throw new InvalidOperationException(
                        $"R_rise ≤ 0(rRise raw = {rRise.Raw})—— 上升段归一化分母失效:" +
                        $"τ_peak({c.TauPeak}) − incubation({c.Incubation}) 相对 τ_rise 过小,或分段倒置;" +
                        "GDD AC-23 的 R_rise > ε_MIN 写入期校验尚未实现,求值侧拒绝静默求值");
                return c.APeak * (Fix.One - e) / rRise;
            }

            if (c.SelfLimit)
                return c.APeak * ExpNeg(tau - c.TauPeak, c.TauFall);

            return c.APeak; // plateau 或急性保持型(急性保持型 = R1-22 限定 lethal,数据侧已校验)
        }

        /// <summary>
        /// 既有占位条目的简化 Base(批次 C 前的原实现,**逐位不变**)。
        /// </summary>
        /// <remarks>这些条目无 `Curve` ⇒ 无 A_peak / τ_peak / 三分支可依,只能用
        /// `Severity` 与 `RecoveryTime` 造一个可跑的形;新数据一律带 Curve(批次 B 起强制)。</remarks>
        private static Fix ComputeBaseLegacy(DiseaseRegistryEntry registry, long tau)
        {
            // 简化版：线性上升 + 指数衰减
            // 完整版需要三分支（self_limit / plateau / 急性保持型）

            if (tau < 0)
                return Fix.Zero;

            // 上升段（简化：线性）
            long riseEnd = registry.RecoveryTime;
            if (tau < riseEnd)
            {
                // 线性上升：A_peak × τ / riseEnd
                return new Fix((long)registry.Severity * Fix.OneRaw * tau / riseEnd);
            }

            // 衰减段（简化：指数衰减）
            long fallDuration = tau - riseEnd;
            // Decay(Δ) = e^(−Δ/half_life) —— 简化为线性衰减
            long halfLife = registry.RecoveryTime;
            if (halfLife <= 0)
                return new Fix((long)registry.Severity * Fix.OneRaw);

            // 简化：每 halfLife 减半
            int decaySteps = (int)(fallDuration / halfLife);
            Fix decayed = new Fix((long)registry.Severity * Fix.OneRaw);
            for (int i = 0; i < decaySteps && decayed.Raw > 0; i++)
            {
                decayed = new Fix(decayed.Raw / 2);
            }

            return decayed;
        }

        /// <summary>
        /// Relapse(τ) —— 复发曲线(F1:`A_rel × e^(−(τ − τ_rel)/τ_rel_fall) · [τ ≥ τ_rel]`)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **只实现首击(k = 0 ⇒ τ_rel = relapse_interval)**,与批次 B 的测试侧 oracle
        /// (`curve_fixture_test.OracleRelapse`)同一读法。**缺口登记**:F1 写
        /// `τ_rel = relapse_interval × (复发次数 + 1)` 且注明「复发次数由病史事件流直接计数」,
        /// 但 36 支 Kind 中**无任何复发事件**(2026-10-22 ADR-032 起 35→36,新支亦非复发)(EventKind 无 Relapse 支)⇒ 多次复发的计数
        /// 在当前数据面**不可求值**;且该式按字面读,首次复发后 count=1 会使 τ_rel ≥ τ 而恒 0,
        /// 与 oracle 相反。本批不自造 Kind、不自造计数语义 —— 归 9 的 GDD 轮裁定(见任务报告)。
        /// `Relapse == null` = 无复发(合法形态),恒 0。
        /// </remarks>
        private static Fix ComputeRelapse(DiseaseRegistryEntry registry, long tau)
        {
            RelapseCurve r = registry.Relapse;
            if (r == null) return Fix.Zero;
            if (tau < 0 || tau < r.Interval) return Fix.Zero;
            return r.ARel * ExpNeg(tau - r.Interval, r.TauFall);
        }

        // ── 处置和式(批次 C 接真)────────────────────────────────────────────

        /// <summary>
        /// 处置贡献 —— `clamp( Σ drug_potency × Decay(Δ) · [polarity = causal] · [处置 ∈ treatable_by(d)],
        /// ±(MAX_ACTIVE_DOSE × single_dose_max) )`(F1;clamp 只罩 Σ,四轮 K1)。
        /// </summary>
        private static Fix ComputeDrugContribution(
            DiseaseRegistryEntry registry,
            IReadOnlyList<TreatmentDose> doses,
            long currentTick,
            Fix? clampBound)
        {
            if (doses == null || doses.Count == 0) return Fix.Zero;

            Fix sum = Fix.Zero;
            for (int i = 0; i < doses.Count; i++)
            {
                TreatmentDose d = doses[i];

                // polarity 门(B5,2026-09-15):和式内显式限定 causal ——
                // 对症处置不改 Progress(规则三 / AC-13;它的偏移在 F2 的 Sign 层,另式)。
                if (d.Polarity != PolarityCausal) continue;

                // 离牌给药门(取舍 4):不在 treatable_by(d) 内 = 空效果(有效动作,但不进 Progress)
                if (!IsInTreatableBy(registry, d.TreatmentId)) continue;

                long delta = currentTick - d.Tick;
                if (delta < 0) continue;              // AC-27:时间反演归零(Decay 内部再判一次)

                Fix decay = Decay(delta, d.HalfLife);
                if (decay.Raw == 0) continue;

                sum += d.DrugPotency * decay;
            }

            if (!clampBound.HasValue) return sum;     // 界值待定 ⇒ 不 clamp(缺口已登记)

            Fix bound = clampBound.Value;
            if (bound.Raw < 0)
                throw new ArgumentOutOfRangeException(nameof(clampBound), bound.Raw,
                    "F1 clamp 界须 ≥ 0(对称界 = ±(MAX_ACTIVE_DOSE × single_dose_max))");
            if (sum.Raw > bound.Raw) return bound;
            long negBound = -bound.Raw;
            if (sum.Raw < negBound) return new Fix(negBound);
            return sum;
        }

        /// <summary>
        /// `[处置 ∈ treatable_by(d)]` 门(F1 取舍 4 · 病种×处置级)。
        /// </summary>
        /// <remarks>
        /// ⚠️ **门当前结构性缺位**:`DiseaseRegistryEntry.TreatableBy` 是**派生字段**,
        /// 写入期不填(RegistrySchema 头注 + GDD R1.3),且校验 R1-19 对非 null 直接
        /// `NotImplementedException` ⇒ 数据面上它**恒 null**。
        /// null ⇒ 门未声明 ⇒ **不拦**(若拦,生产路径的治疗贡献恒 0,最小可证链不可证)。
        /// <b>缺口登记</b>:真正的门值来自独立轴文件 `disease_action_axis.json`(story-007),
        /// 装载面落地后本方法须改为「按 (treatment, disease) 查极性/归属」—— 归 9 的 GDD 轮。
        /// 非 null 时按 actionId 成员判定(polarity 以**载荷**为准,AC-28;GDD 的
        /// 「同药对不同病可不同极性」若要求以 treatable_by 为准,须先裁双源优先级 —— 见任务报告)。
        /// </remarks>
        private static bool IsInTreatableBy(DiseaseRegistryEntry registry, int treatmentId)
        {
            TreatableByEntry[] tb = registry.TreatableBy;
            if (tb == null) return true;
            for (int i = 0; i < tb.Length; i++)
            {
                if (tb[i] != null && tb[i].ActionId == treatmentId) return true;
            }
            return false;
        }

        /// <summary>
        /// 「有效对因处置」判据 —— GDD <c>:1127</c> 的三条件合取,**本方法是唯一实现**。
        /// </summary>
        /// <remarks>
        /// <para><b>为什么必须提为公开方法(2026-10-22 F4 接线轮)</b>:同一组判据有两个消费者 ——
        /// ① <see cref="ComputeDrugContribution"/>(F1 的 Σ 求和);② F4 的
        /// <see cref="LastInterventionAggregator.TreatmentChannelInput"/>(`last_intervention` 通道)。
        /// 若各写一遍,改一处不改另一处即**静默分叉**:F1 认为某处置有效、F4 认为它不续命,
        /// 表现为「玩家一直在治、病人照样死」,且没有任何测试会红。
        /// ⇒ 唯一实现,两处共用。</para>
        ///
        /// <para><b>三条件(GDD <c>:1127</c> 逐字)</b>:</para>
        /// <list type="number">
        /// <item><c>polarity = causal</c>(<see cref="PolarityCausal"/>)—— 对症处置不续命(铁律二);</item>
        /// <item><c>drug_potency 与 Base 异号</c> —— <c>Base ≥ 0</c> 恒成立
        /// (<c>a_peak &gt; 0</c> 由 R1-24 保证,<c>Base</c> 各分段非负)⇒ 本条等价于
        /// <c>DrugPotency.Raw &lt; 0</c>。⚠️ 不写成「与当刻 Base 比大小」——
        /// 那会让判据依赖求值时刻,同一处置在不同 tick 忽有效忽无效,`last_intervention`
        /// 随之非单调;</item>
        /// <item><c>处置 ∈ treatable_by(d)</c> —— 经 <see cref="IsInTreatableBy"/>
        /// (该门当前因数据面缺位恒放行,缺口登记见其注释)。</item>
        /// </list>
        ///
        /// <para><b>⚠️ 本方法不含</b>「<c>Δ ≥ 0</c>」与「<c>Decay ≠ 0</c>」两条 ——
        /// 那两条是**贡献量**的防御(AC-27 时间反演归零 / 半衰期退化),
        /// 不是「是否有效处置」的定义。故仍留在 <see cref="ComputeDrugContribution"/> 内。</para>
        /// </remarks>
        public static bool IsEffectiveCausalDose(DiseaseRegistryEntry registry, in TreatmentDose dose)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (dose.Polarity != PolarityCausal) return false;
            if (dose.DrugPotency.Raw >= 0) return false;      // 与 Base(≥0)异号
            return IsInTreatableBy(registry, dose.TreatmentId);
        }

        /// <summary>
        /// `Decay(Δ) = e^(−Δ/half_life) · [Δ ≥ 0]`(F1;`Δ &lt; 一律取 0`)。
        /// </summary>
        /// <remarks>
        /// 指数经 <see cref="Fix.Exp"/>(全整数域,禁 float / libm)。
        /// <b>四道防御</b>(前三条不改合法输入的取值;第四条见
        /// <see cref="MaxRatioNumeratorTicks"/> 的边界登记):
        /// <list type="number">
        /// <item>`Δ &lt; 0` ⇒ 0(AC-27:时间反演必须归零,否则指数爆炸);</item>
        /// <item>`half_life ≤ 0` ⇒ 0 —— AC-28 本应在**写入期**拒收(载荷缺该量则 9 不可求值);
        /// 求值侧防御性归零而非抛 `DivideByZeroException`(流内既有事件不可回滚,抛 = 卡死 Step);</item>
        /// <item>`Δ/half_life ≥ 12` ⇒ 0 —— `Fix.Exp` 的量化下限是 `x ≤ −772244/65536 ≈ −11.79`
        /// (e^x &lt; 0.5 LSB,半-away 舍入恰为 0)⇒ 比值 ≥ 12 时 Exp 自身就归零,
        /// 本短路与它**逐点一致**,只是省算不改取值;</item>
        /// <item>`Δ &gt; 2³²−1` ⇒ 0 —— `Fix.FromRational(Δ, H)` 的分子 `Δ&lt;&lt;16` 须 ≤ 2⁴⁸−1。</item>
        /// </list>
        /// </remarks>
        private static Fix Decay(long deltaTicks, long halfLifeTicks)
        {
            if (deltaTicks < 0) return Fix.Zero;
            if (halfLifeTicks <= 0) return Fix.Zero;   // AC-28 写入期职责;此处防御归零
            if (deltaTicks == 0) return Fix.One;
            if (deltaTicks / halfLifeTicks >= 12) return Fix.Zero;   // 见方法头「深度门」
            if (deltaTicks > MaxRatioNumeratorTicks) return Fix.Zero;

            // 比值 = Δ / half_life(Q16.16)—— 与 `new Fix(Δ×2¹⁶) / new Fix(H×2¹⁶)` 同值,
            // 但不经分母的二次移位(分母 H 可任意大,移位会先溢)。
            Fix ratio = Fix.FromRational(deltaTicks, halfLifeTicks);
            return Fix.Exp(Fix.Zero - ratio);
        }

        /// <summary>
        /// `e^(−Δ/τ)` 通用形(Base / Relapse 的指数;τ 为 Fix 化时间常数)。
        /// </summary>
        /// <param name="deltaTicks">非负的分段内时长(调用方已按分支归零负值)。</param>
        /// <param name="tau">时间常数(必填 &gt; 0 —— R1-21 / R1-25 已在写入期校验)。</param>
        /// <exception cref="InvalidOperationException">`tau ≤ 0`(写入期校验缺席时的求值侧兜底)。</exception>
        private static Fix ExpNeg(long deltaTicks, Fix tau)
        {
            if (tau.Raw <= 0)
                throw new InvalidOperationException(
                    $"时间常数须 > 0(tau raw = {tau.Raw})—— 分母为 0 会使指数不可求," +
                    "R1-21/R1-25 写入期校验缺席,求值侧 fail-loud");
            if (deltaTicks <= 0) return deltaTicks < 0 ? Fix.Zero : Fix.One;
            if (deltaTicks > MaxRatioNumeratorTicks) return Fix.Zero;

            // 比值 = Δ / τ(Q16.16)。**必须走 Fix 除法**:
            //   `Fix(Δ×2¹⁶) / τ` ⇒ raw = round(Δ×2¹⁶ × 2¹⁶ / (τ_value×2¹⁶)) = round(Δ×2¹⁶/τ_value)
            // 若写成 `RoundHalfAwayFromZero(Δ×2¹⁶, τ.Raw)` 则得 round(Δ/τ_value) = **整数比**,
            // 分数精度全丢(实测 τ=700 处上升段恒 0 —— 批次 C 首跑红)。
            Fix ratio = new Fix(deltaTicks * Fix.OneRaw) / tau;
            return Fix.Exp(Fix.Zero - ratio);
        }

        /// <summary>
        /// 指数比值分子的防御上界:`Δ × 2¹⁶` 须 ≤ 2⁴⁸ − 1(`<c>Fix.Div</c>` 的被除数域,
        /// 亦即 `<c>Fix.FromRational</c>` 的 `Δ &lt;&lt; 16` 域)⇒ `Δ ≤ 2³² − 1`(≈ 6.8 年 @ 20 Hz)。
        /// </summary>
        /// <remarks>
        /// 越过它 ⇒ 归零。对任何 `τ ≤ 2³¹ tick`(≈ 341 天)此时比值 ≥ 2,早已落在
        /// `Fix.Exp` 的量化下限之下 ⇒ 归零**精确**。`τ &gt; 2³¹ tick` 的时间常数不属本系统
        /// 数据面(GDD Tuning §二 的安全区间远小于此)—— 若真出现,本行是保守近似,已登记。
        /// </remarks>
        private const long MaxRatioNumeratorTicks = (1L << 32) - 1;

        // ── 保留占位(tripwire · 阶段 0 口径:归「M2 可玩级另计」)──────────────

        /// <summary>
        /// Noise —— (seed, patient, tick) 纯函数。
        /// </summary>
        /// <remarks>**恒 0(保留占位)** —— 查表 + 整数插值与 σ 装载面归「M2 可玩级另计」;
        /// 本批不实现、不删 tripwire 意图(潜伏期抑制已在 Evaluate 先行短路)。</remarks>
        private static Fix ComputeNoise(ulong worldSeed, PatientId patientId, long tick)
        {
            // 简化版：无噪声
            return Fix.Zero;
        }

        /// <summary>
        /// Trend —— 病程变化率。
        /// </summary>
        /// <remarks>**恒 0(保留占位)** —— F2 中心差分 `(Progress(t+W/2) − Progress(t−W/2))/W`
        /// + `TREND_EPS` 死区归后续轮;W 的取值亦未定(数值轮)。</remarks>
        private static Fix ComputeTrend(DiseaseRegistryEntry registry, long tau)
        {
            // 简化版：无 trend
            return Fix.Zero;
        }
    }
}
