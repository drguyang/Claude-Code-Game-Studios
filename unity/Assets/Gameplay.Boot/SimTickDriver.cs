// M2 接线轮阶段 1 装配轮 · 组合根案 A(2026-10-09)
//
// 权威来源:
//   ADR-005 §Key Interfaces —— ITickProvider = tick 唯一来源,Step 由它驱动、不由渲染帧驱动
//   technical-preferences §Performance Budgets(OQ-25-8 用户裁定)—— TICK_SECONDS = 0.05(20 Hz)
//   ADR-025 §① —— Gameplay.Boot 装配(2026-10-09 增补,案 A)
//
// 核心机制:
//   - double 余量累加器 `_acc`,每次 Advance(delta) 累加,整 tick 数换算进 CurrentTick;
//   - CurrentTick = long 单调递增(只加不减,不回绕语义由调用方保证 tick 不超 long.MaxValue);
//   - 死亡螺旋护栏:maxStepsPerFrame 上限(默认 5)—— 超限后**丢弃余量**并告警一次
//     (防「掉帧 → 一次补 1000 tick → 更掉帧」的正反馈)。
//
// ⚠️ 与 60 fps 不整除是**刻意设计**(OQ-25-8):TICK_PERIOD = 50 ms,60 fps 帧时间 16.67 ms
//    ⇒ 1 tick 跨约 3 帧(50 / 16.67 ≈ 3),两者不整除。步相位由本类承担:一个 tick 恰好触发
//    一次 Step(由 ITickProvider 驱动,**不由渲染帧驱动**)。若实现期实测相位有问题,
//    须另裁 TICK_SECONDS(改它 = 9 / 5 / 25 / 7a 四份文档同时失效),不得由单系统自行调频。
//
// S6 停机时钟语义(ADR-010 §六 定期 checkpoint / 退出保存的伴生约定):
//   - halt 期间**不调用 Advance** —— 墙钟不进入累加器,故「暂停 10 分钟」不会在恢复时
//     一次补跑 12000 tick;
//   - 余数**保留**(不丢、不清零)—— 恢复后从原余量继续累加;
//   - 恢复**不补跑** —— 即 halt 期的墙钟时间不进入 Advance,自然没有可补的量。
//   简言之:Advance 的入参 = 「本帧允许流逝的逻辑时间」,由调用方(BootRoot.Update)在
//   halt 时改为 0 或干脆不调用。
//
// 「纯 C#」口径:本类不派生 MonoBehaviour、不读场景/组件、不碰 UnityEngine 状态;
// 仅在**未注入告警回调**时回退到 UnityEngine.Debug.LogWarning(引擎无关的可测性由
// onDroppedRemainder 回调承载 —— EditMode 测试经回调观测告警,不依赖 Console 抓取)。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// tick 驱动器 —— <see cref="ITickProvider"/> 的生产实装(把墙钟 delta 折算成整 tick)。
    /// <para>唯一调用方 = 组合根的帧循环(现阶段 = <see cref="BootRoot.Update"/>)。</para>
    /// </summary>
    public sealed class SimTickDriver : ITickProvider
    {
        private readonly double _tickSeconds;
        private readonly int _maxStepsPerFrame;
        private readonly Action<string> _onDroppedRemainder;

        private double _acc;
        private long _currentTick;
        private bool _warnedDeathSpiral;

        /// <summary>
        /// 创建 tick 驱动器。
        /// </summary>
        /// <param name="tickSeconds">单 tick 时长(秒)。默认 0.05 = 20 Hz(OQ-25-8 裁定值)。</param>
        /// <param name="maxStepsPerFrame">单次 <see cref="Advance"/> 最多推进的 tick 数
        /// (死亡螺旋护栏;默认 5 = 一次调用最多补 250 ms)。</param>
        /// <param name="onDroppedRemainder">余量被丢弃(死亡螺旋)时的告警回调;
        /// 为 null 时回退 <c>UnityEngine.Debug.LogWarning</c>。</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="tickSeconds"/> ≤ 0,或 <paramref name="maxStepsPerFrame"/> &lt; 1。</exception>
        public SimTickDriver(double tickSeconds = 0.05, int maxStepsPerFrame = 5,
                             Action<string> onDroppedRemainder = null)
        {
            if (tickSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds,
                    "单 tick 时长必须为正(0.05 = 20 Hz,OQ-25-8)");
            if (maxStepsPerFrame < 1)
                throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame), maxStepsPerFrame,
                    "单次推进的 tick 数上限至少为 1");
            _tickSeconds = tickSeconds;
            _maxStepsPerFrame = maxStepsPerFrame;
            _onDroppedRemainder = onDroppedRemainder;
        }

        /// <summary>当前逻辑 tick(单调 long;每推进一个 tick 加 1)。</summary>
        public long CurrentTick => _currentTick;

        /// <summary>当前累加器余量(秒)。公开只读,供调试/测试观测;不得由外部写入。</summary>
        public double RemainderSeconds => _acc;

        /// <summary>
        /// 推进墙钟时间,返回本次实际发生的 tick 数(0 … <c>maxStepsPerFrame</c>)。
        /// </summary>
        /// <param name="deltaSeconds">本帧允许流逝的逻辑时间(秒;halt 期不调用或传 0)。</param>
        /// <returns>本次推进的 tick 数 —— 调用方按该次数逐个触发每 tick 入口。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaSeconds"/> 为负。</exception>
        public int Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds,
                "墙钟增量不得为负(halt 期请传 0 或不调用 —— S6 停机时钟语义)");

            _acc += deltaSeconds;

            int steps = 0;
            while (_acc >= _tickSeconds && steps < _maxStepsPerFrame)
            {
                _acc -= _tickSeconds;
                _currentTick++;
                steps++;
            }

            // 死亡螺旋:上限打满后余量仍 ≥ 一个 tick ⇒ 丢弃余量(不跨帧无限追帧),
            // 且**只在真的发生丢弃时告警一次**(防日志刷屏)。
            if (_acc >= _tickSeconds)
            {
                double dropped = _acc;
                _acc = 0d;
                if (!_warnedDeathSpiral)
                {
                    _warnedDeathSpiral = true;
                    Warn(
                        $"[SimTickDriver] 死亡螺旋护栏:单次推进已达上限 {_maxStepsPerFrame} tick, " +
                        $"余量 {dropped:0.####} 秒已丢弃(CurrentTick = {_currentTick})。" +
                        "后续同型超限不再重复告警。");
                }
            }

            return steps;
        }

        private void Warn(string message)
        {
            if (_onDroppedRemainder != null)
                _onDroppedRemainder(message);
            else
                UnityEngine.Debug.LogWarning(message);
        }
    }
}
