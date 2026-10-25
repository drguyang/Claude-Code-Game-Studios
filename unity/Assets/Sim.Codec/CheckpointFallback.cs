// AC-7a-20 定时兜底半边(`TD-persist-05` 动作 ④ · `TD-persist-04` 动作 ① 契约裁定 2026-10-25)。
//
// 权威来源:
//   design/gdd/persistence-service.md:607-613 —— AC-7a-20 原文:
//     「若叙事事件长期缺席(初次启动 / 纯探索期)由 CHECKPOINT_INTERVAL 定时兜底触发 ——
//      事件触发 = 调度策略,非第四存档入口」
//   design/gdd/persistence-service.md:137 —— 不变量:「崩溃损失 ≤ 一个间隔」**恒成立**;
//   design/gdd/persistence-service.md:635 —— OQ-7a-5(✅ 2026-10-06 已裁):
//     CHECKPOINT_INTERVAL = 5 min(`18000` tick @ 50 ms;安全区间 1–30 min,可调),
//     防御性下限 = 出诊周期级。
//
// ─────────────────────────────────────────────────────────────────────────────
// 裁定记录(2026-10-25 用户两裁定,原文见 docs/tech-debt-register.md):
//   · 瞬时源 = 具名接口 `INarrativeContextQuery`(住 Sim.Contracts;10 落地后由 Boot 注入,
//     此前维持 null ⇒ 本类**不可构造** —— fail-closed 的表达是「不构造」,不是「构造后放行」);
//   · 兜底结构 = 触发判据「interval 到期 **且** 瞬时源 true」;到期不安全 ⇒ **顺延**
//     (下一 tick 重查,不重置 interval)—— 从 GDD `:137`「崩溃损失 ≤ 一个间隔**恒成立**」推导:
//     跳过窗口会放宽到 2× 间隔,与该不变量相抵;顺延保住它。
// ─────────────────────────────────────────────────────────────────────────────
//
// ⚠️ 本类是**调度策略**,不是存档入口(AC-7a-20 逐字):触发的是**调用方给的闭包**
//    (装配根把「`Checkpoint(下一个可写槽位)`」接进来),本类不认识 `SaveService`、
//    不认识 `SaveSlot`、不写任何字节。
// ⚠️ 驱动 = sim tick(`ITickProvider` 节奏,不由渲染帧驱动 —— O-10-3 / tick 相位纪律);
//    测试由夹具直接驱动 `OnTick`。
// 🔁 **2026-10-25 生产接线兑现(用户裁定「生产接线今日不为零,现在修」)** —— 上句
//    「生产接线点今日为零(裁定丙的有意终态)」**就此过期**(原文不删,留作闭环记录):
//    `CompositionRoot` 装配本类(闭包 = `Checkpoint(台账下一可写槽位)`,瞬时源与
//    `SaveService` 同一 `INarrativeContextQuery` 实例),`BootRoot` 的 tick 边沿序列
//    经 `OnTicks(本帧推进 tick 数)` 驱动。瞬时源实装 = `NoPendingMedicalActionQuery`
//    (`IArmedState.IsArmed` 取反 —— W5 要求的「独立于流的内存态」)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// `CHECKPOINT_INTERVAL` 定时兜底调度器(AC-7a-20 的「事件长期缺席」半边)。
    /// <para>每次 <see cref="OnTick"/> 推进一格;计满 interval 时**先问瞬时源**:
    /// 安全 ⇒ 触发一次 checkpoint 闭包并清零计数;不安全 ⇒ **顺延**(计数保持到期,
    /// 下一 tick 重查 —— 安全后 ≤ 1 tick 内必触发,保「崩溃损失 ≤ 一个间隔」)。</para>
    /// </summary>
    public sealed class CheckpointFallback
    {
        /// <summary>
        /// GDD 默认间隔:`CHECKPOINT_INTERVAL` = 5 min = 300 s = <c>18000</c> tick(@ 50 ms,
        /// `TICK_SECONDS = 0.05`,OQ-25-8 裁定)—— OQ-7a-5 已裁值(2026-10-06),非本类新裁。
        /// </summary>
        public const int DefaultIntervalTicks = 18000;

        private readonly Action _checkpoint;
        private readonly INarrativeContextQuery _context;
        private readonly int _intervalTicks;
        private int _ticksSinceLastFire;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="checkpoint">
        /// 触发闭包(装配根接「`Checkpoint(下一个可写槽位)`」)。
        /// <para>⚠️ <b>null 拒收</b> —— 触发闭包是本类存在的唯一目的,缺了它 = 配置错误,
        /// 必须构造期暴露而不是首触发时静默不响。</para>
        /// </param>
        /// <param name="context">
        /// 瞬时源(规则十四「一个量」)。
        /// <para>⚠️ <b>null 拒收</b>(fail-closed):10 未落地时生产**根本不构造本类**
        /// (装配根维持 `narrativeContext = null` ⇒ `Checkpoint` 本身必抛,兜底无从谈起);
        /// 一个拿着 null 源还在计数的兜底 = 「到期必抛」的定时炸弹,不如不让造。</para>
        /// </param>
        /// <param name="intervalTicks">间隔(tick)。GDD 默认 18000;测试注入小值。须 ≥ 1。</param>
        /// <exception cref="ArgumentNullException">任一驱动源为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="intervalTicks"/> &lt; 1。</exception>
        public CheckpointFallback(Action checkpoint,
                                  INarrativeContextQuery context,
                                  int intervalTicks = DefaultIntervalTicks)
        {
            _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (intervalTicks < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(intervalTicks), intervalTicks,
                    "CHECKPOINT_INTERVAL 须 ≥ 1 tick —— 0 或负值 = 每 tick 兜底,「定时」语义不成立");
            _intervalTicks = intervalTicks;
        }

        /// <summary>距上次触发已过的 tick 数(测试观测面;触发后为 0,顺延期 ≥ interval)。</summary>
        public int TicksSinceLastFire => _ticksSinceLastFire;

        /// <summary>
        /// 事件触发半边成功存档后由接线方调用(TD-persist-04 动作① 裁定④ · 2026-10-25):
        /// 把计数清零。兜底只管「事件长期缺席」—— 锚点刚存过 ⇒ 窗口从现在重起,
        /// 「崩溃损失 ≤ 一个间隔」(GDD <c>:137</c>)以最近一次存档起算,无论哪半边。
        /// <para>⚠️ 只在存档**确实发生**时调用 —— 没存成却清零会把不变量放宽到近 2× 间隔
        /// (接线方以 <c>Func&lt;bool&gt;</c> 返回值区分,见 <c>AnchorCheckpointTrigger</c> 头注)。</para>
        /// </summary>
        public void NotifyExternalCheckpoint() => _ticksSinceLastFire = 0;

        /// <summary>
        /// 推进一格(sim tick 节奏)。
        /// <para>到期且安全 ⇒ 触发一次并清零;到期不安全 ⇒ **顺延**(计数保持在到期区,
        /// 下一 tick 重查 —— <b>不重置 interval</b>,否则「崩溃损失 ≤ 一个间隔」被放宽)。</para>
        /// </summary>
        public void OnTick()
        {
            _ticksSinceLastFire++;
            if (_ticksSinceLastFire < _intervalTicks)
                return;

            // 到期:瞬时源**每次调用重答**(瞬时量)—— 顺延语义就建立在这一点上。
            if (!_context.InNarrativeContext())
                return;   // 顺延:不清零,下一 tick 重查

            _checkpoint();
            _ticksSinceLastFire = 0;
        }

        /// <summary>
        /// 整批推进(驱动方把「本帧推进的 tick 数」一次交给它)。
        /// <para>等价于 <paramref name="tickCount"/> 次 <see cref="OnTick"/> —— 但**必须用本方法**
        /// 而非调用方自己循环或「每帧调一次 <see cref="OnTick"/>」:一帧推进 N tick 时按帧驱动会把
        /// N 个 tick 计成 1,interval 语义(以 tick 计)被帧率稀释,「崩溃损失 ≤ 一个间隔」随之被放宽。
        /// 死亡螺旋护栏(单帧最多 <c>maxStepsPerFrame</c> tick)保证 <paramref name="tickCount"/> 有界。</para>
        /// </summary>
        /// <param name="tickCount">本帧推进的 tick 数(≥ 0;0 = 本帧无 tick 边沿,no-op)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tickCount"/> &lt; 0。</exception>
        public void OnTicks(long tickCount)
        {
            if (tickCount < 0)
                throw new ArgumentOutOfRangeException(nameof(tickCount), tickCount,
                    "本帧推进的 tick 数不得为负 —— tick 单调递增,负数 = 驱动方时钟错乱");
            for (long i = 0; i < tickCount; i++)
                OnTick();
        }
    }
}
