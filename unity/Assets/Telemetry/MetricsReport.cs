// 权威来源:ADR-019 §五(P0 交付最小切面 = 指标重算器 + 调试视图 + 导出;导出 = **本地文件**)·
//   `design/gdd/telemetry-analytics.md` R7(整数纪律:率以 (分子,分母) 对承载;仅当需标量时
//   按 `Q16.16 = (num << 16) / den` 并**显式附加** `ROUND_HALF_AWAY_FROM_ZERO`)·
//   E-2(分母 0 报「分母 0」,不报 0、不除零)· ADR-006 §五(唯一舍入模式)。
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ 本文件是 TD-epic-01 的还债件(2026-10-19 第三片):ADR-019 §五 的「导出」半。
//
// ⚠️ **本类只产出文本,不碰文件系统**:` Telemetry` 装配是 `noEngineReferences: true`,
//    而写文件要 `System.IO`(BCL,可用)但**路径 / 目录策略归调用方** ——
//    51 只回答「报告长什么样」,「写到哪」由装配层的 `ITelemetrySink` 实现决定。
//    ⇒ 这同时守住 ADR-019 §三:51 是只读消费者,**不是**第四个写者(它连自己的输出都不写)。
// ═══════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DaYiJingCheng.Telemetry
{
    /// <summary>一行指标报告(整数纪律 R7:全部字段整数;率 = (num, den) 对)。</summary>
    public readonly struct MetricLine
    {
        /// <summary>指标名(如 `F1a`)。</summary>
        public readonly string Name;

        /// <summary>分子(计数)。</summary>
        public readonly long Num;

        /// <summary>分母( countable 总数);0 ⇒ 不可定义。</summary>
        public readonly long Den;

        /// <summary>人类可读备注(可空)。</summary>
        public readonly string Note;

        /// <summary>构造。</summary>
        public MetricLine(string name, long num, long den, string note = null)
        {
            Name = name;
            Num = num;
            Den = den;
            Note = note;
        }

        /// <summary>分母为 0(指标不可定义)。</summary>
        public bool DenominatorZero => Den == 0;
    }

    /// <summary>指标报告的**纯文本**渲染面(本地导出的格式层 · ADR-019 §五)。
    /// <para>**零 IO / 零引擎**:只 `StringBuilder` 拼文本,由调用方交 `ITelemetrySink.WriteLocalReport` 落盘。</para>
    /// <para>**整数纪律 R7**:率一律渲染成 `(num, den)` 对;**不**自作主张折成小数
    /// —— 折标量只在显式调用 <see cref="ToQ1616"/> 时发生,且带 ADR-006 唯一舍入。</para></summary>
    public static class MetricsReport
    {
        /// <summary>ROUND_HALF_AWAY_FROM_ZERO(ADR-006 唯一舍入模式;禁 `Math.Round` 默认 ties-to-even)。</summary>
        public static long RoundHalfAwayFromZero(long num, long den)
        {
            if (den == 0) return 0;                 // 分母 0 ⇒ 调用方须走 DenominatorZero 分支,不除零
            long n = num < 0 ? -num : num;
            long d = den < 0 ? -den : den;
            long q = (2 * n + d) / (2 * d);         // ⌊(2n+d)/(2d)⌋ = 半远离零的整数商
            return (num < 0) ^ (den < 0) ? -q : q;
        }

        /// <summary>把 (num, den) 折成 **Q16.16 定点**(`(num << 16) / den` + 显式舍入)。
        /// <para>⚠️ GDD R7 逐字:C# 的 `/` 是**截断**除法,本身不含舍入 ⇒ **必须**由
        /// <see cref="RoundHalfAwayFromZero"/> 包住。</para>
        /// <para>⚠️ **溢出界(如实登记)**:`num &lt;&lt; 16` 要求 `|num| &lt; 2^47` 才不溢 `long`。
        /// 本项目的 num 恒为**计数**(病例数 / 事件数),实际上界 ≈ 场景实体数 × tick 数,远低于该界;
        /// 但**形式无守卫**。若日后 num 可能接近该界,须先除后乘或改 128 位手工拆分
        /// (承 ADR-005 Amendment G 的 hi/lo 纪律)。</para></summary>
        /// <param name="denominatorZero">分母 0 ⇒ true,且返回 0(**不**除零、不报 0/1)。</param>
        public static long ToQ1616(long num, long den, out bool denominatorZero)
        {
            if (den == 0)
            {
                denominatorZero = true;
                return 0;
            }
            denominatorZero = false;
            // (num << 16) / den —— 用 long 承载,num 已在调用方保证不溢出(见下注)
            return RoundHalfAwayFromZero(num << 16, den);
        }

        /// <summary>渲染成纯文本报告(本地导出的正文)。
        /// <para>格式:一行一个指标 `name = num / den`;分母 0 ⇒ `name = 分母 0`(E-2:不报 0、不除零)。</para></summary>
        /// <param name="title">报告标题(如「sprint-19 本地会话」)。</param>
        /// <param name="lines">指标行(顺序即输出顺序;调用方定,本类不重排)。</param>
        public static string Render(string title, IReadOnlyList<MetricLine> lines)
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(title ?? "(untitled)").Append('\n');
            if (lines == null || lines.Count == 0)
            {
                sb.Append("(无指标 —— 空流或尚未还原)\n");
                return sb.ToString();
            }
            foreach (MetricLine l in lines)
            {
                sb.Append(l.Name).Append(" = ");
                if (l.DenominatorZero)
                    sb.Append("分母 0");                     // E-2 逐字
                else
                    sb.Append(l.Num.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                      .Append(l.Den.ToString(CultureInfo.InvariantCulture));
                if (!string.IsNullOrEmpty(l.Note))
                    sb.Append("  # ").Append(l.Note);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// 同上,并**追加一行跳过分类**(`TD-epic-01-b` A 方案,2026-10-30)。
        /// <para>⚠️ <b>为什么单列一个重载而不是改原签名</b>:原 `Render` 被 F2 / F3 / F4 / F5 / F6 等多个报告共用,而只有
        /// 「真解码载荷」的指标(F1 / F2 / F3)才有跳过可报。给它加一个形参会逼
        /// 那些「只读事件头」的指标各传一个假 `default` —— 那是给一个不存在的信息
        /// 造一个占位。</para>
        /// <para><b>零跳过时不追加任何行</b>(见 <see cref="SkipBreakdown.Describe"/> 的理由)。</para>
        /// </summary>
        public static string Render(string title, IReadOnlyList<MetricLine> lines,
                                    SkipBreakdown skipped)
        {
            string body = Render(title, lines);
            if (skipped.Total == 0) return body;
            return body + "  # 解码跳过:" + skipped.Describe() + "\n";
        }
    }
}
