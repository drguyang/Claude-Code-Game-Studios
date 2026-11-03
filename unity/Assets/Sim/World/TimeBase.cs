// ADR-005 / GDD time-and-weather.md —— 时间基准与相位推导。
//
// 权威来源:
//   ADR-005 —— tick 唯一来源 = ITickProvider,5 只读 CurrentTick
//   GDD time-and-weather.md §规则一(日边界 / 昼夜相位)· F-5.1(:189-203)
//   GDD time-and-weather.md §Tuning Knobs(`:516` TICKS_PER_DAY = 36000 ·
//     `:519` NIGHT_START / NIGHT_SPAN = 27000 / 9000 —— 均 2026-11-03 用户裁定)
//   AC-5-05 —— `TICKS_PER_DAY` 在代码中**只有一个定义点**
//
// 核心机制:
//   - TICKS_PER_DAY 单一定义点(**全仓唯一;`TimeConstantsSingleDefinitionGate` 守**)
//   - FMod 环绕安全取模(禁 C# % 负操作数)
//
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 **2026-11-03 收编轮(必须留档 —— 这里曾同时存在两套时间实现)**
// ─────────────────────────────────────────────────────────────────────────────
// 事实:本类此前是**休眠件** —— `IsNight` / `ComputePhase` / `ComputeSeasonIndex` /
//   `ComputeState` / `TICKS_PER_DAY` **零生产调用方**(只有本类的夹具在用);
//   生产侧只用它的 `FDiv`(`WeatherRoll.cs:59` / `EventRng.cs:209`)。
//   同日我在 `Sim/Time/WorldTimeConstants.cs` **另造了一套** `TicksPerDay` / `IsNight`
//   并接进了存档册 —— 于是全仓出现**两个定义点、两套昼夜公式、两个冲突值**(480 vs 36000)。
//   这正是 `AC-5-05` 要防的形态,而我是肇事者。
//
// 现口径:**本类是 tick→时间语义的唯一实现**;`Sim/Time/` 已删除。
//   常量取**已裁定值**:`TICKS_PER_DAY = 36000` · `NIGHT_START = 27000` · `NIGHT_SPAN = 9000`
//   (旧值 480 / 360 / 240 是 GDD 定值之前的占位,其注释「GDD 裁定 480」**为假**,原文已删)。
//
// ⚠️ **仍未做(逐项,不借绿)**:
//   · `ComputeSeasonIndex` 用 `TICKS_PER_DAY * 30` 与 `4` 两个**字面量** —— 而 GDD 规定
//     季长是独立参数 `TICKS_PER_SEASON`、季数是 `SEASONS_PER_YEAR`,**两者值均未定**。
//     ⇒ 该函数的季节口径是**占位**,且与 F-5.2 的公式**不等价**。它今日零生产调用方
//     (只有 `time_base_test` 在用),故本轮**不改行为**,只登记。
//   · `WEATHER_BLOCK_TICKS` 未定 ⇒ F-5.3 天气仍无落点。
//   · tick→**历法**(年/月/日/时辰)在设计里不存在(GDD `:90` 把历书/时辰归「史实层呈现」)。
//   · `AC-5-11`(装载期校验器)仍 `NOT-RUN` —— 本类全是 `const`,没有装载期。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// 时间基准 —— tick 只读与昼夜 / 季节相位推导(系统 5 的语义层)。
    /// </summary>
    public static class TimeBase
    {
        /// <summary>
        /// 一个世界日的 tick 数。**= 36000**(20 Hz ⇒ 现实 30 分钟一日 · 一个时辰 3000 tick)。
        /// <para>2026-11-03 用户裁定;权威表 = `time-and-weather.md` §Tuning Knobs。</para>
        /// <para>⚠️ <b>唯一代码定义点</b>(`AC-5-05`):任何系统**引用本常量**,不得自带一份。
        /// 52 的日边界与预算窗口是**同一个量**(DC-3),各写一份就会静默漂移 ——
        /// 症状是「事件预算在跨日时错位一格」。</para>
        /// <para>⚠️ 改本值 = **重裁级**:须同时重算所有历史事件时间戳与存档
        /// (与改 `TICK_SECONDS` 同判,见 CLAUDE.md 的 tick 裁定注)。</para>
        /// </summary>
        public const long TICKS_PER_DAY = 36000L;

        /// <summary>夜的起点(日内相位)。**= 27000**(2026-11-03 用户裁定)。</summary>
        /// <para>定义域 ∈ [0, <see cref="TICKS_PER_DAY"/>)。</para>
        public const long NIGHT_START = 27000L;

        /// <summary>夜的长度(tick)。**= 9000**(2026-11-03 用户裁定 · 1/4 日)。</summary>
        /// <para>定义域 ∈ (0, <see cref="TICKS_PER_DAY"/>);`≥ TICKS_PER_DAY` = 恒夜 = 昼夜轴消失。</para>
        public const long NIGHT_SPAN = 9000L;

        /// <summary>
        /// 环绕安全取模 —— 结果 ∈ [0, n)。
        /// <para>⚠️ C# 的 `%` 是**截断**语义(`-1 % 36000 == -1`)⇒ 直接用会在
        /// `phase &lt; NIGHT_START` 时给出负数,`&lt; NIGHT_SPAN` 恒真 ⇒ **正午被判成夜**
        /// (GDD `:288` 点名要守的那条)。</para>
        /// </summary>
        public static long FMod(long a, long n)
        {
            if (n <= 0) throw new ArgumentException("n 必须 > 0", nameof(n));
            long r = a % n;
            return r < 0 ? r + n : r;
        }

        /// <summary>日内相位 ∈ [0, <see cref="TICKS_PER_DAY"/>)。</summary>
        public static long ComputePhase(long tick) => FMod(tick, TICKS_PER_DAY);

        /// <summary>
        /// F-5.1:`isNight(tick) ⟺ FMod(phase − NIGHT_START, TICKS_PER_DAY) &lt; NIGHT_SPAN`。
        /// <para>⚠️ **必须**用 <see cref="FMod"/> 式,不能用朴素区间比较 ——
        /// 夜区间跨日边界时朴素写法会**静默漏判** `[0, (START+SPAN)−TPD)` 那一段
        /// (GDD `:435` / 首轮评审 B-1)。</para>
        /// <para>⚠️ 生产值 27000 + 9000 = 36000 **恰好不跨日** ⇒ 用朴素写法也能通过所有
        /// 以生产值构造的夹具;环绕正确性由 <see cref="IsNightAt"/> 的**注入参数**夹具守。</para>
        /// </summary>
        public static bool IsNight(long tick) => IsNightAt(ComputePhase(tick), NIGHT_START, NIGHT_SPAN);

        /// <summary>
        /// F-5.1 的本体(参数可注入)。
        /// <para>⚠️ 开放为 <c>public</c> 只有一个理由:**夹具要能构造跨日环绕的配置** ——
        /// 生产值不跨日,拿生产值写不出那条判据。生产代码**只应**调 <see cref="IsNight"/>。</para>
        /// </summary>
        public static bool IsNightAt(long phase, long nightStart, long nightSpan)
            => FMod(phase - nightStart, TICKS_PER_DAY) < nightSpan;

        /// <summary>
        /// tick → 世界第几日(**1 起**;`tick = 0` ⇒ 第 1 日)。
        /// <para>⚠️ 本方法是**本常量自己的算术**,不是历法 API ——「日→月/年」的映射
        /// 在设计里不存在(GDD `:90`)。放在常量旁边是为了让除法与「+1」只有一个出处。</para>
        /// <para>⚠️ 负 tick 不在 ADR-005 的合法域内;此处按第 1 日处理(显示面不因坏数据抛)。</para>
        /// </summary>
        public static long DayNumberOf(long tick)
            => (tick < 0L ? 0L : tick) / TICKS_PER_DAY + 1L;

        /// <summary>
        /// 计算季节索引。
        /// <para>⚠️ <b>占位口径</b>(见文件头):用 `TICKS_PER_DAY × 30` 与 `4` 两个字面量,
        /// 而 GDD 规定季长 = `TICKS_PER_SEASON`、季数 = `SEASONS_PER_YEAR`,**两者值均未定**
        /// ⇒ 本函数与 F-5.2 **不等价**。今日零生产调用方,故不改行为,只登记。</para>
        /// </summary>
        public static long ComputeSeasonIndex(long tick)
        {
            long seasonLength = TICKS_PER_DAY * 30L;
            return FMod(FDiv(tick, seasonLength), 4L);
        }

        /// <summary>整数除法(向零截断)。</summary>
        public static long FDiv(long a, long b)
        {
            if (b == 0) throw new DivideByZeroException();
            return a / b;
        }

        /// <summary>计算完整时间状态(相位 + 是否夜晚 + 季节索引)。</summary>
        public static TimeState ComputeState(long tick)
            => new TimeState(ComputePhase(tick), IsNight(tick), ComputeSeasonIndex(tick));
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
