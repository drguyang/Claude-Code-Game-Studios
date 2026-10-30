// `TD-epic-01-b` —— 解码失败的**分类**计数(`DecodeFailureKind` + `SkipBreakdown`)。
//
// 权威:
//   · 债册 `TD-epic-01-b`(原判定:「『解码失败 → 计 skipped』的吞异常写法会把编码侧缺陷
//     伪装成数据质量问题 —— `skipped` 只在报告里给个数,不区分『载荷未驻留 / 损坏 / 类型错配』」)
//   · `ADR-031 §②`(抛形:`InvalidOperationException` = 未驻留 / `InvalidDataException` = 损坏)
//   · 用户 2026-10-30 裁定 **A 方案**(拆两个计数)
//
// ═══════════════════════════════════════════════════════════════════════════════
// 为什么只有**两**类,而债册原文提了**三**类(2026-10-30 实测订正)
// ═══════════════════════════════════════════════════════════════════════════════
// 债册原建议:「catch 里按异常类型分流,**类型错配**不转 skipped 而是抛」。
// **实测该建议有一半做不到**,三条证据(探针已删,结论保留):
//
// ① **未驻留 vs 损坏 —— 可分** ✅
//    `PayloadDecoder.ResolveSpan` 抛 `InvalidOperationException`(查无此 blob / 越界);
//    `PayloadCodec.Decode<T>` 的 schema 校验抛 `InvalidDataException`。两者类型不同。
//
// ② **类型错配 vs schema 损坏 —— 不可分** ❌
//    两者抛的都是 **`InvalidDataException`**(`PayloadCodec.cs:54` 的类型校验与 `:115` 的
//    schema 校验用同一个类型)。而 `InvalidDataException` 是 **`sealed`**
//    (实测 `CS0509: cannot derive from sealed type`)⇒ 派生一个子类型来区分也做不到。
//
// ③ **「类型错配」在真实路径上几乎不可观测** ❌
//    实测:用 `CaseClosed` 的字节冒充 `JudgmentRecorded` 时,抛的是
//    **schema 越界**(`JudgmentRecorded: tag=0 越界(1..31)`),而 `PayloadCodec.cs:54`
//    那句「载荷类型 X 与请求的 Y 不匹配」**根本执行不到** —— 因为 `DecodeBoxed(kind, bytes)`
//    先按 `kind` 解出了一个 `JudgmentRecordedPayload`(它不因字节来源而变),随后
//    `boxed is T` 的失败只在「调用方请求的类型与 kind 的载荷类型不符」时发生,
//    而那属于**调用方静态错误**,真实事件流里撞不到。
//    ⇒ 「类型错配」更接近「编译期该防但防不住」的代码卫生问题,而非数据质量问题的伪装。
//
// ⇒ **运行期只有两类真的可达**。本文件按两类实现,并把「第三类为什么不在」写进代码
//    (而不是留给下一个人重新查一遍)。

using System;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>解码失败的**类别**(`TD-epic-01-b` A 方案)。</summary>
    /// <remarks>
    /// <para><b>为什么要有它</b>:此前 `skipped` 只是一个 `int` —— 「载荷未驻留」(读档前
    /// 池还没装好 / 事件引用了尚未装载的 blob)与「载荷损坏」(字节在但 schema 违例)被
    /// **混成同一个数**,而两者的处置**完全不同**:前者是**时序问题**(再等等就好),
    /// 后者是**编码侧缺陷**(必须查)。混在一起 = 把编码缺陷伪装成数据质量问题。</para>
    /// <para>⚠️ 只有两个成员 —— 第三类(类型错配)在运行期不可达,理由见文件头。</para>
    /// </remarks>
    public enum DecodeFailureKind
    {
        /// <summary>不是失败(解码成功)。</summary>
        None = 0,

        /// <summary>
        /// **未驻留 / 越界** —— 池里查无此 blob,或 `PayloadRef` 越界。
        /// <para>类型:`InvalidOperationException`(ADR-031 §②)。</para>
        /// <para>性质 = **时序**(读档前池未装好 / 事件引用了尚未装载的 blob)⇒
        /// 通常是「再等等」,不是「数据坏了」。</para>
        /// </summary>
        Missing = 1,

        /// <summary>
        /// **损坏 / 其它** —— 字节在但违反该 Kind 的 schema,或任何非 `Missing` 的异常。
        /// <para>类型:`InvalidDataException`(ADR-031 §②),以及**任何其它**异常
        /// (兜底归此类 —— 宁可多报「可疑」,不可漏报)。</para>
        /// <para>性质 = **编码侧缺陷**(必须查),不是时序问题。</para>
        /// </summary>
        Corrupt = 2,
    }

    /// <summary>解码失败的**分类计数**(可观测面;非静默吞)。</summary>
    /// <remarks>
    /// <para>⚠️ <b>为什么不是两个裸 `out int`</b>:调用方签名会变成
    /// `out int, out int` —— 两个同类型的相邻 `out` 形参**极易传错顺序**,而编译器不拦。
    /// 一个具名结构体把「哪一类」写进类型,顺序错了至少读起来是错的。</para>
    /// </remarks>
    public readonly struct SkipBreakdown
    {
        /// <summary>未驻留 / 越界的条数(`DecodeFailureKind.Missing`)。</summary>
        public readonly int Missing;

        /// <summary>损坏 / 其它异常的条数(`DecodeFailureKind.Corrupt`)。</summary>
        public readonly int Corrupt;

        /// <summary>总跳过数 = <see cref="Missing"/> + <see cref="Corrupt"/>。</summary>
        public int Total => Missing + Corrupt;

        /// <summary>构造。</summary>
        public SkipBreakdown(int missing, int corrupt)
        {
            Missing = missing; Corrupt = corrupt;
        }

        /// <summary>按类别累加一条(纯函数式地返回新值 —— 避免可变结构体陷阱)。</summary>
        public SkipBreakdown Plus(DecodeFailureKind kind)
        {
            switch (kind)
            {
                case DecodeFailureKind.Missing: return new SkipBreakdown(Missing + 1, Corrupt);
                case DecodeFailureKind.Corrupt: return new SkipBreakdown(Missing, Corrupt + 1);
                case DecodeFailureKind.None:    return this;
                default:
                    // ⚠️ 未知类别 ⇒ 按 Corrupt 计(**不静默丢**) —— 将来若有人扩枚举
                    //    却忘了改这里,计数会偏「可疑」侧而不是偏「没问题」侧。
                    return new SkipBreakdown(Missing, Corrupt + 1);
            }
        }

        /// <summary>报告文案:`跳过 N(未驻留 a / 损坏 b)`;零跳过时为「无跳过」。</summary>
        /// <remarks>
        /// ⚠️ **零跳过时不渲染两个 0** —— 「跳过 0(未驻留 0 / 损坏 0)」是噪声,
        /// 且会让读者以为「有跳过、只是都是 0」。这一点与 `MetricLine` 的
        /// 「分母 0 显式标记」不同:那里 0 是**有意义的值域边界**,这里 0 就是「没发生」。
        /// </remarks>
        public string Describe()
            => Total == 0 ? "无跳过" : $"跳过 {Total}(未驻留 {Missing} / 损坏 {Corrupt})";
    }

    /// <summary>把异常映射到 <see cref="DecodeFailureKind"/> —— **唯一**映射处。</summary>
    /// <remarks>
    /// <para>⚠️ <b>为什么单列一个静态方法,而不是让每个 catch 各写各的</b>:本仓有多处解码委托
    /// (`DecodeJudgmentRecord` / `DecodeMisdiagnosisRecord` / `DecodeActionRecord` /
    /// `DecodeSettlementRecord` / `DecodeGrowthRecord`),**五个 catch 块**。
    /// 若每处各自判类型,则「新增一个异常类型」要改五处 —— 漏一处就是静默不一致。
    /// 集中在此 = **一处改,五处生效**。</para>
    /// <para>⚠️ <b>分类是「尽力而为」,不是「精确」</b> —— 见文件头 ②③:类型错配与 schema 损坏
    /// 共用 `InvalidDataException`。本方法把「非 `InvalidOperationException` 的一切」都归
    /// `Corrupt` ⇒ **宁可多报可疑,不可漏报**。</para>
    /// </remarks>
    public static class DecodeFailure
    {
        /// <summary>把解码抛出的异常映射为失败类别。</summary>
        /// <param name="ex">解码抛出的异常。</param>
        /// <returns>
        /// `InvalidOperationException` ⇒ <see cref="DecodeFailureKind.Missing"/>;
        /// 其它一切(含 `InvalidDataException` / `ArgumentNullException` / 未知类型)⇒
        /// <see cref="DecodeFailureKind.Corrupt"/>。
        /// </returns>
        public static DecodeFailureKind Classify(Exception ex)
        {
            if (ex == null) return DecodeFailureKind.Corrupt;   // 不该发生;按可疑计
            return ex is InvalidOperationException
                ? DecodeFailureKind.Missing
                : DecodeFailureKind.Corrupt;
        }
    }
}
