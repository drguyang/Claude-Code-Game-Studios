// ADR-005 / GDD time-and-weather.md —— 时间基准与相位推导。
//
// 权威来源:
//   ADR-005 —— tick 唯一来源 = ITickProvider,5 只读 CurrentTick
//   GDD time-and-weather.md §规则一(日边界 / 昼夜相位)· F-5.1(:189-203)
//   GDD time-and-weather.md §Tuning Knobs(`:516` TICKS_PER_DAY = 36000 ·
//     `:519` NIGHT_START / NIGHT_SPAN = 27000 / 9000 —— 均 2026-11-03 用户裁定)
//   **ADR-038 §D1** —— 三值**不在代码里**,由 `assets/data/5_time.json` 烘焙装载;
//     本类的方法一律**收 `TimeParams` 入参**,不持有任何常量。
//   AC-5-05 —— 值只有**一个定义点**(数据侧),代码里零 `const`
//
// 核心机制:
//   - FMod 环绕安全取模(禁 C# % 负操作数)
//   - 一切时间量都从入参 `TimeParams` 取 —— 本类是**纯函数**
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 2026-11-03 的两次形态变化(留档,防误读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **收编轮**(`50f17a7d`):本类曾与 `Sim/Time/WorldTimeConstants.cs` 并存,
//    两个定义点、两套昼夜公式、两个冲突值(480 vs 36000);`Sim/Time/` 已删除。
// ② **外部化轮**(ADR-038):三值从 `const` 改为**数据装载**。理由不是洁癖 ——
//    AC-5-05 的 `THEN` 句要求「52 的数据集**引用** 5 的值、零字面副本」,
//    而那是**数据侧**的判据;值留在 `const` 里,那条判据永远没有宾语。
//
// ⚠️ **仍未做(逐项,不借绿)**:
//   · `ComputeSeasonIndex` 用 `TICKS_PER_DAY * 30` 与 `4` 两个**字面量** —— 而 GDD 规定
//     季长是独立参数 `TICKS_PER_SEASON`、季数是 `SEASONS_PER_YEAR`,**两者值均未定**
//     ⇒ 该函数的季节口径是**占位**,且与 F-5.2 的公式**不等价**。它今日零生产调用方,
//     故本轮**不改行为**,只登记。
//   · `WEATHER_BLOCK_TICKS` 未定 ⇒ F-5.3 天气仍无落点。
//   · tick→**历法**(年/月/日/时辰)在设计里不存在(GDD `:90` 把历书/时辰归「史实层呈现」)。

using System;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 时间基准 —— tick 只读与昼夜 / 季节相位推导(系统 5 的语义层)。
    /// <para>⚠️ <b>纯函数</b>:不持任何状态、不读任何常量 —— 参数全由调用方经
    /// <see cref="DaYiJingCheng.Sim.TimeParams"/> 注入(ADR-038 §D1)。</para>
    /// </summary>
    public static class TimeBase
    {
        /// <summary>
        /// 环绕安全取模 —— 结果 ∈ [0, n)。
        /// <para>⚠️ C# 的 `%` 是**截断**语义(`-1 % 36000 == -1`)⇒ 直接用会在
        /// `phase &lt; nightStart` 时给出负数,`&lt; nightSpan` 恒真 ⇒ **正午被判成夜**
        /// (GDD `:288` 点名要守的那条)。</para>
        /// </summary>
        public static long FMod(long a, long n)
        {
            if (n <= 0) throw new ArgumentException("n 必须 > 0", nameof(n));
            long r = a % n;
            return r < 0 ? r + n : r;
        }

        /// <summary>整数除法(向零截断)。</summary>
        /// <exception cref="DivideByZeroException"><paramref name="b"/> 为 0。</exception>
        public static long FDiv(long a, long b)
        {
            if (b == 0) throw new DivideByZeroException();
            return a / b;
        }

        /// <summary>日内相位 ∈ [0, <paramref name="p"/>.TICKS_PER_DAY)。</summary>
        public static long ComputePhase(long tick, TimeParams p)
        {
            RequireParams(p);
            return FMod(tick, p.TICKS_PER_DAY);
        }

        /// <summary>
        /// F-5.1:`isNight(tick) ⟺ FMod(phase − NIGHT_START, TICKS_PER_DAY) &lt; NIGHT_SPAN`。
        /// <para>⚠️ **必须**用 <see cref="FMod"/> 式,不能用朴素区间比较 ——
        /// 夜区间跨日边界时朴素写法会**静默漏判** `[0, (START+SPAN)−TPD)` 那一段
        /// (GDD `:435` / 首轮评审 B-1)。</para>
        /// <para>⚠️ 生产值 27000 + 9000 = 36000 **恰好不跨日** ⇒ 用朴素写法也能通过所有
        /// 以生产值构造的夹具;环绕正确性由 <see cref="IsNightAt"/> 的**注入参数**夹具守。</para>
        /// </summary>
        public static bool IsNight(long tick, TimeParams p)
        {
            RequireParams(p);
            return IsNightAt(ComputePhase(tick, p), p.NIGHT_START, p.NIGHT_SPAN, p.TICKS_PER_DAY);
        }

        /// <summary>
        /// F-5.1 的本体(四个参数全部可注入)。
        /// <para>⚠️ 开放为 <c>public</c> 只有一个理由:**夹具要能构造跨日环绕的配置** ——
        /// 生产值不跨日,拿生产值写不出那条判据。生产代码**只应**调 <see cref="IsNight"/>。</para>
        /// </summary>
        public static bool IsNightAt(long phase, long nightStart, long nightSpan, long ticksPerDay)
            => FMod(phase - nightStart, ticksPerDay) < nightSpan;

        /// <summary>
        /// tick → 世界第几日(**1 起**;`tick = 0` ⇒ 第 1 日)。
        /// <para>⚠️ 本方法是**日长自己的算术**,不是历法 API ——「日→月/年」的映射
        /// 在设计里不存在(GDD `:90`)。放在这里是为了让除法与「+1」只有一个出处。</para>
        /// <para>⚠️ 负 tick 不在 ADR-005 的合法域内;此处按第 1 日处理(显示面不因坏数据抛)。</para>
        /// </summary>
        public static long DayNumberOf(long tick, TimeParams p)
        {
            RequireParams(p);
            return (tick < 0L ? 0L : tick) / p.TICKS_PER_DAY + 1L;
        }

        /// <summary>
        /// 计算季节索引。
        /// <para>⚠️ <b>占位口径</b>(见文件头):用 `TICKS_PER_DAY × 30` 与 `4` 两个字面量,
        /// 而 GDD 规定季长 = `TICKS_PER_SEASON`、季数 = `SEASONS_PER_YEAR`,**两者值均未定**
        /// ⇒ 本函数与 F-5.2 **不等价**。今日零生产调用方,故不改行为,只登记。</para>
        /// </summary>
        public static long ComputeSeasonIndex(long tick, TimeParams p)
        {
            RequireParams(p);
            long seasonLength = p.TICKS_PER_DAY * 30L;
            return FMod(FDiv(tick, seasonLength), 4L);
        }

        /// <summary>计算完整时间状态(相位 + 是否夜晚 + 季节索引)。</summary>
        public static TimeState ComputeState(long tick, TimeParams p)
        {
            RequireParams(p);
            return new TimeState(ComputePhase(tick, p), IsNight(tick, p), ComputeSeasonIndex(tick, p));
        }

        /// <summary>
        /// 参数缺失守卫。
        /// <para>⚠️ 与 <c>TimeParams.Validate()</c> **不同**:这里只判「有没有传」,
        /// 不判「值合不合法」—— 值域校验是装载期的一次性义务(AC-5-11),
        /// 在这里每 tick 重复一遍既慢、又会让「非法值」与「没装载」在异常类型上混为一谈。</para>
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="p"/> 为 null。</exception>
        private static void RequireParams(TimeParams p)
        {
            if (p == null)
                throw new ArgumentNullException(nameof(p),
                    "时间参数未注入 —— 必须先经 5_time 装载并 Validate()(ADR-038 §D1)");
        }
    }

    /// <summary>
    /// 时间状态快照。
    /// </summary>
    public readonly struct TimeState
    {
        public readonly long Phase;
        public readonly bool IsNight;
        public readonly long SeasonIndex;

        public TimeState(long phase, bool isNight, long seasonIndex)
        {
            Phase = phase;
            IsNight = isNight;
            SeasonIndex = seasonIndex;
        }
    }
}
