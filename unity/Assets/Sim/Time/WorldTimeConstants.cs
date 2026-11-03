// 世界时间常量的**唯一代码定义点**(2026-11-03 立)。系统 5 的第一个代码件。
//
// 权威来源:
//   design/gdd/time-and-weather.md §规则四(`TICKS_PER_DAY` 归 5,52 **引用**不重复定义 · DC-3)
//                                 §Tuning Knobs(`:516` 默认值 = 36000,2026-11-03 用户裁定)
//   `AC-5-05` —— 「`TICKS_PER_DAY` 在代码中**只有一个定义点**」
//   docs/architecture/adr-005(20 Hz ⇒ 1 tick = 50 ms · `ITickProvider` 是 tick 的唯一来源)
//   CLAUDE.md §Unity Debugging 既有裁定枚(改 `TICK_SECONDS` / `TICKS_PER_DAY` 都属**重裁级**)
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么住 `Sim`(系统 5 自己的程序集),而不是 `Sim.Contracts`
// ─────────────────────────────────────────────────────────────────────────────
// 🔁 **2026-11-03 当日改判(用户裁定)**:首版落在 `Sim.Contracts`,理由写在下面「已废」段。
//    用户指出它让「Contracts = **跨装配的类型契约**」这条语义变宽了 —— 一个**世界常量**
//    不是契约类型。改判后落 `Sim/Time/`,归属与「5 定义」逐字一致。
//
// 现口径(三条):
// ① **归属对**:`TICKS_PER_DAY` 是系统 5 定义的量(GDD 规则四),代码就该住 5 的程序集。
// ② **消费者都够得着**:`Sim` 内的 5 / 52 直接可见;`Gameplay.Boot` 的 asmdef **引用
//    `Sim`**(实测)⇒ 装配层能算日子。`Gameplay.UI` **不引用 `Sim`,也不需要** ——
//    它拿到的是算好的 `DayNumber`(`SaveSlotView`),不是常量。
// ③ **唯一性靠位置维持**:本文件是 `Sim` 内**唯一**定义 `TICKS_PER_DAY` 的地方;
//    下游一律引用,不得自带一份(`AC-5-05`)。
//
// ~~已废(原文不删,记录当时理由)~~:首版写「放 `Sim.Contracts` 可让 42 也看见、永远不用搬家」。
//    该理由成立但**代价是语义污染** —— 42 根本不需要这个常量,它只需要结果。
//    为「将来可能有人要」而把非契约类型放进契约程序集,是拿语义换不存在的灵活性。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么是 `const` 而不是 `assets/data/*.json`
// ─────────────────────────────────────────────────────────────────────────────
// ADR-014 的「数值外置」针对的是**可调参数**。本值不是:
// ① 52 的 `ROLL_INTERVAL` 必须**整除**它,而 GDD 把那条判据定在**构建期报错**
//    (`random-events.md:1075`);数据表化会把构建期判据降成装载期判据。
// ② 改它 = 所有历史事件的时间戳与存档同时失效(与改 `TICK_SECONDS` 同级)。
//    「改一下试试」这件事本身就该是重裁,不该是一个配置文件里的数字。
//
// 🔁 **2026-11-03 同日追加(用户裁定甲)**:F-5.1 昼夜落地 —— `NightStart = 27000` ·
//    `NightSpan = 9000`(夜 = 后四分之一日)。值由用户裁定,公式逐字照 `time-and-weather.md:189-203`。
//
// ⚠️ **仍未做(逐项,不借绿)**:
//    · `TICKS_PER_SEASON` / `SEASONS_PER_YEAR` —— **值未定**,F-5.2 无落点;
//    · `WEATHER_BLOCK_TICKS` —— **值未定**,F-5.3 无落点;
//    · tick→**历法**映射(年/月/日/时辰)—— **设计里不存在**:GDD `:90` 明写历书上的
//      干支与节气是**史实层呈现**、**不是机制读数**;规则一的归属表也无「历法」一行。
//      ⇒ 存档册的「第 N 日 · 昼/夜」是当前**机制允许的上限**,spec §5.4 的
//      「三月十九 酉时」需要先改那句 GDD 才可做。
//    · `AC-5-11`(装载期校验器:非法配置 ⇒ 硬失败)—— 本类是 `const`,**没有装载期**;
//      该 AC 要的是 5 的配置加载路径,今日不存在。**本条仍 NOT-RUN。**

namespace DaYiJingCheng.Sim.Time
{
    /// <summary>
    /// 世界时间常量(系统 5)。
    /// <para>⚠️ 本类**只有常量与它自己的算术** —— tick→历法映射(年/月/日/时辰)归 5 的实现轮,
    /// 且它的形状尚无裁定,此处不预置任何方法(预置即等于替 5 定 API)。</para>
    /// </summary>
    public static class WorldTime
    {
        /// <summary>
        /// 一个世界日的 tick 数。
        /// <para><b>= 36000</b>(20 Hz ⇒ 现实 30 分钟一日 · 一个时辰 3000 tick)。
        /// 2026-11-03 用户裁定;权威表在 `time-and-weather.md` §Tuning Knobs。</para>
        /// <para>⚠️ <b>唯一代码定义点</b>(`AC-5-05`):任何系统**引用本const**,不得自带一份
        /// `const` —— 52 的日边界与预算窗口是**同一个量**(DC-3),各写一份就会静默漂移,
        /// 症状是「事件预算在跨日时错位一格」。</para>
        /// <para>⚠️ 改本值 = **重裁级**:须同时重算所有历史事件时间戳与存档
        /// (与改 `TICK_SECONDS` 同判,见 CLAUDE.md 的 tick 裁定注)。</para>
        /// </summary>
        public const int TicksPerDay = 36000;

        /// <summary>
        /// tick → 世界第几日(**1 起**;`tick = 0` ⇒ 第 1 日)。
        /// <para>⚠️ 本方法是**本常量自己的算术**,不是系统 5 的历法 API ——
        /// 「日→月 / 年 / 时辰」的映射归 5 的实现轮,此处刻意不预置。
        /// 放在常量旁边是为了让除法**只有一个出处**(否则每个消费者各写一遍 `+1`)。</para>
        /// <para>⚠️ 负 tick 不在 ADR-005 的合法域内;此处按第 1 日处理(显示面不因坏数据抛)。</para>
        /// </summary>
        public static long DayNumberOf(long tick)
            => (tick < 0L ? 0L : tick) / TicksPerDay + 1L;

        // ══════════════════════════════════════════════════════════════════
        // F-5.1 昼夜(time-and-weather.md:189-203)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 夜的起点(日内相位)。**= 27000**(2026-11-03 用户裁定)——
        /// 与 <see cref="NightSpan"/> 合起来表示「夜 = 一天的后四分之一」。
        /// <para>定义域 ∈ [0, <see cref="TicksPerDay"/>)(GDD `:202`)。</para>
        /// </summary>
        public const long NightStart = 27000L;

        /// <summary>
        /// 夜的长度(tick)。**= 9000**(2026-11-03 用户裁定 · 1/4 日)。
        /// <para>定义域 ∈ (0, <see cref="TicksPerDay"/>);`≥ TicksPerDay` = 恒夜 = 昼夜轴消失
        /// (GDD `:437` 判据为「构建期校验拒绝」)。</para>
        /// </summary>
        public const long NightSpan = 9000L;

        /// <summary>日内相位 ∈ [0, <see cref="TicksPerDay"/>)。</summary>
        public static long DayPhase(long tick) => FMod(tick, TicksPerDay);

        /// <summary>
        /// F-5.1:`isNight(tick) ⟺ FMod(phase − NIGHT_START, TICKS_PER_DAY) &lt; NIGHT_SPAN`。
        /// <para>⚠️ **必须**用 <see cref="FMod"/> 式,不能用朴素区间比较 ——
        /// 夜区间跨日边界(`NIGHT_START + NIGHT_SPAN &gt; TICKS_PER_DAY`)时,朴素写法会
        /// **静默漏判** `[0, (START+SPAN)−TPD)` 那一段(GDD `:435` / 首轮评审 B-1)。</para>
        /// <para>⚠️ 生产值 27000 + 9000 = 36000 **恰好不跨日** ⇒ 用朴素写法也能通过所有
        /// 以生产值构造的夹具。所以环绕正确性由 <see cref="IsNightAt"/> 的**注入参数**夹具守
        /// (`world_time_test.cs` 用 30000/12000 这组**会跨日**的配置)。</para>
        /// </summary>
        public static bool IsNight(long tick) => IsNightAt(DayPhase(tick), NightStart, NightSpan);

        /// <summary>
        /// F-5.1 的本体(参数可注入)。
        /// <para>⚠️ 开放为 <c>public</c> 只有一个理由:**夹具要能构造跨日环绕的配置** ——
        /// 生产值不跨日,拿生产值写不出那条判据。生产代码**只应**调
        /// <see cref="IsNight"/>;本方法不是第二个入口。</para>
        /// </summary>
        /// <param name="phase">日内相位 ∈ [0, <see cref="TicksPerDay"/>)。</param>
        /// <param name="nightStart">夜的起点。</param>
        /// <param name="nightSpan">夜的长度。</param>
        public static bool IsNightAt(long phase, long nightStart, long nightSpan)
            => FMod(phase - nightStart, TicksPerDay) < nightSpan;

        /// <summary>
        /// 数学意义上的取模(结果恒 ∈ [0, m))。
        /// <para>⚠️ C# 的 `%` 是**截断**语义(`-1 % 36000 == -1`)⇒ 直接用会在
        /// `phase &lt; NIGHT_START` 时给出负数,`&lt; NIGHT_SPAN` 恒真 ⇒ **正午被判成夜**。
        /// 这正是 GDD `:288` 点名要守的那条。</para>
        /// </summary>
        private static long FMod(long a, long m)
        {
            long r = a % m;
            return r < 0L ? r + m : r;
        }
    }
}
