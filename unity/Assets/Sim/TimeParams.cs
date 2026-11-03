// 权威来源:ADR-038 §D1 / §D5(时间参数外部化 · `TimeParams` 住 `Sim`)
//          · GDD design/gdd/time-and-weather.md §Tuning Knobs(:515-519)· AC-5-11(装载期校验)
//          · ADR-014 §五(值来自烘焙产物 `5_time.cooked.bytes`)
//
// ⚠️ 本件**只是参数的载体与校验器**,不含任何计算 —— 计算在 `Sim.World.TimeBase`。
//    两者分开的理由:`TimeBase` 是纯函数(同输入同输出),`TimeParams` 是它的输入。
//
// 🔁 2026-11-03 ADR-038:本类原住 `Sim/TimeBase.cs`(与 `TimeBase` 静态类同文件),
//    而那一份静态类与 `Sim.World.TimeBase` **完全重复**且零生产消费 ⇒ 已删除,
//    只留本类。`Sim/TimeBase.cs` 同时更名为本文件(文件名与类名一致)。

using System;

namespace DaYiJingCheng.Sim
{
    /// <summary>5 时间天气的系统参数(从烘焙产物 <c>5_time.cooked.bytes</c> 装载)。</summary>
    /// <remarks>
    /// <para><b>哪三个是“活”的</b>:<see cref="TICKS_PER_DAY"/> / <see cref="NIGHT_START"/> /
    /// <see cref="NIGHT_SPAN"/> —— 它们已由用户 2026-11-03 裁定,并经数据管线装载。</para>
    /// <para><b>哪两个是“未裁定占位”</b>:<see cref="TICKS_PER_SEASON"/> /
    /// <see cref="SEASONS_PER_YEAR"/> —— 值尚未裁定,<b>刻意不进</b> <c>5_time.json</c> 的 schema
    /// (ADR-038 §D1)。它们保留在本类里,只为让 AC-5-11 的两条非法注入
    /// (「`TICKS_PER_SEASON = 0`」「`SEASONS_PER_YEAR &lt; 2`」)有可注入的对象;
    /// <b>不得</b>把它们的默认值当成「值」使用(见 <see cref="ValidateSeason"/>)。</para>
    /// </remarks>
    public sealed class TimeParams
    {
        /// <summary>一个世界日的 tick 数。**唯一装载源 = `assets/data/5_time.json`**。</summary>
        public long TICKS_PER_DAY;

        /// <summary>夜的起点(日内相位)。</summary>
        public long NIGHT_START;

        /// <summary>夜长。</summary>
        public long NIGHT_SPAN;

        /// <summary>
        /// 真·季长(tick)。<b>值未裁定</b> —— 不进 `5_time.json` 的 schema,今日无装载源。
        /// </summary>
        public long TICKS_PER_SEASON;

        /// <summary>
        /// 一年几季。<b>值未裁定</b> —— 不进 `5_time.json` 的 schema,今日无装载源。
        /// </summary>
        public long SEASONS_PER_YEAR;

        /// <summary>
        /// 校验**已装载**的三参数(AC-5-11 的前两条)。
        /// <para>判据(<b>装载期</b>调用它 ⇒ 非法配置 = 启动期硬失败,禁 null 兜底 / 禁静默钳正):</para>
        /// <list type="bullet">
        ///   <item><c>TICKS_PER_DAY &gt; 0</c> —— <c>= 0</c> ⇒ 日边界消失(除零)</item>
        ///   <item><c>NIGHT_START ∈ [0, TICKS_PER_DAY)</c> —— 越界 ⇒ 相位无从定义</item>
        ///   <item><c>NIGHT_SPAN ∈ (0, TICKS_PER_DAY)</c> —— <c>≥ TICKS_PER_DAY</c> ⇒ 恒夜 ⇒ 昼夜轴消失</item>
        /// </list>
        /// <para>⚠️ 本方法**不校验**两个未裁定的季参数 —— 见 <see cref="ValidateSeason"/>。</para>
        /// </summary>
        /// <exception cref="ArgumentException">任一关系不成立。</exception>
        public void Validate()
        {
            if (TICKS_PER_DAY <= 0)
                throw new ArgumentException(
                    $"TICKS_PER_DAY 必须 > 0(实得 {TICKS_PER_DAY})—— 日边界与预算窗口(DC-3)同时失去定义",
                    nameof(TICKS_PER_DAY));

            if (NIGHT_START < 0 || NIGHT_START >= TICKS_PER_DAY)
                throw new ArgumentException(
                    $"NIGHT_START 必须 ∈ [0, TICKS_PER_DAY)(实得 {NIGHT_START},TPD={TICKS_PER_DAY})" +
                    " —— 相位域外的起点无从定义",
                    nameof(NIGHT_START));

            if (NIGHT_SPAN <= 0 || NIGHT_SPAN >= TICKS_PER_DAY)
                throw new ArgumentException(
                    $"NIGHT_SPAN 必须 ∈ (0, TICKS_PER_DAY)(实得 {NIGHT_SPAN},TPD={TICKS_PER_DAY})" +
                    " —— ≥ TPD 即恒夜,昼夜轴消失",
                    nameof(NIGHT_SPAN));
        }

        /// <summary>
        /// 校验两个<b>未裁定</b>的季参数(AC-5-11 的后两条)。
        /// <para>⚠️ 与 <see cref="Validate"/> 分开是**刻意的**:今日没有季配置的装载源,
        /// 若把这两条塞进 <see cref="Validate"/>,则任何一次正常装载都会撞上默认值 <c>0</c>
        /// 而硬失败。分开后:装载路径只调 <see cref="Validate"/>;季功能落地时
        /// (值裁定 + schema 升版)再把本方法接进装载路径。</para>
        /// <para>⚠️ <b>今日状态</b>:本方法**可由夹具直接注入驱动**,但**不由装载路径触发**
        /// ⇒ AC-5-11 后两条的「装载期」层 = <b>NOT-RUN</b>(ADR-038 §D1 / R4)。</para>
        /// </summary>
        /// <exception cref="ArgumentException">任一关系不成立。</exception>
        public void ValidateSeason()
        {
            if (TICKS_PER_SEASON <= 0)
                throw new ArgumentException(
                    $"TICKS_PER_SEASON 必须 > 0(实得 {TICKS_PER_SEASON})", nameof(TICKS_PER_SEASON));

            if (SEASONS_PER_YEAR < 2)
                throw new ArgumentException(
                    $"SEASONS_PER_YEAR 必须 ≥ 2(实得 {SEASONS_PER_YEAR})—— =1 退化 ⇒ 只有一季," +
                    "季节轴消失(R-5-D)",
                    nameof(SEASONS_PER_YEAR));
        }
    }
}
