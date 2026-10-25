// ADR-010 §六 / GDD `persistence-service.md` 规则十三 —— 退出保存的**限时重试**。
//
// 权威来源:
//   GDD `persistence-service.md:195-198` —— 规则十三
//   GDD `persistence-service.md:581-586` —— AC-7a-17(BLOCKING)
//   GDD `:244` ——「退出保存:从 `WritingAsync` 起**限时 join**(默认 1s)」
//   GDD `:414-415` —— 旋钮表
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么重试逻辑要**独立成类**,而不是写在 SaveOnExit 里
// ─────────────────────────────────────────────────────────────────────────────
// ① **可测性**:写在 `SaveOnExit` 里就只能靠「造一个真坏目录 + 真等 1 秒」来测,
//    每条失败路径都要真睡 —— 既慢又不可控。抽出来后夹具可注入假时钟 / 假 sleep,
//    于是「重试了几次」「是否在截止前放弃」都是**可数的事实**,不是时间的副产品。
// ② **两种失败要分开**:「写盘失败(可重试)」与「驱动源缺失(不可重试)」。
//    后者是**编程错误**,必须当场抛;把它卷进重试循环会让配置错误伪装成磁盘故障,
//    在日志里躺满 1 秒然后静默变成「会话未完整」—— 那会把真 bug 埋掉。
//    ⇒ 本类**只**吞「写盘失败」那一种;`SaveService` 在调它**之前**就把驱动源检查做完。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本类不认识「标志位」,也不认识「日志」—— 它只回答「写成功了吗」
// ─────────────────────────────────────────────────────────────────────────────
// 置位 / 清位 / 日志分别归 `SaveIncompleteFlag` 与装配层。本类保持单一职责:
// **在一个时间预算内,尽量把这一次写做成;做成返回 true,做不成返回 false + 最后一次异常。**
// 这样「限时」这一条可以在**不碰任何文件、不碰任何 UI** 的条件下单独证伪。

using System;
using System.Diagnostics;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 限时重试驱动:在给定时间预算内反复尝试一次写操作。
    /// <para><b>无状态、纯重试逻辑</b> —— 时钟与 sleep 均为注入委托,故可用假时钟夹具证伪。</para>
    /// </summary>
    public static class ExitSaveRetrier
    {
        /// <summary>
        /// 默认时间预算 = 1000 ms(GDD 旋钮 `SAVE_EXIT_RETRY_TIMEOUT` 默认值,规则十三)。
        /// <para>⚠️ 这是**旋钮默认值**,不是硬编码的业务常量 —— 装配层可经
        /// `SaveService` 构造形参覆盖(测试用极短预算以保证快)。</para>
        /// </summary>
        public const int DefaultTimeoutMillis = 1000;

        /// <summary>
        /// 首次重试前的等待(ms)。退避序列按 2 倍增长并封顶在 <see cref="MaxDelayMillis"/>。
        /// <para>⚠️ **不取 0** —— 秒退(pure spin)在磁盘忙时只会把 CPU 烧掉且几乎不可能
        /// 成功;给一点退避让 OS 有机会处理完正在进行的 flush。</para>
        /// </summary>
        public const int InitialDelayMillis = 25;

        /// <summary>单次退避的封顶(ms)。</summary>
        public const int MaxDelayMillis = 400;

        /// <summary>
        /// 在 <paramref name="timeoutMillis"/> 预算内反复执行 <paramref name="write"/>,直到成功或超预算。
        /// </summary>
        /// <param name="write">要重试的写操作。**须幂等**(重试意味着可能已部分执行)。</param>
        /// <param name="timeoutMillis">时间预算(ms)。**≤ 0 ⇒ 只尝试一次**,不重试。</param>
        /// <param name="clockMillis">单调时钟(ms)。生产 = <see cref="Stopwatch"/>;夹具 = 假时钟。</param>
        /// <param name="sleepMillis">等待委托。夹具可记录调用而不真睡。</param>
        /// <param name="lastFailure">失败时 = 最后一次的异常;成功时 = null。</param>
        /// <returns>成功 ⇒ true。</returns>
        /// <exception cref="ArgumentNullException">任一委托为 null。</exception>
        public static bool TryWrite(
            Action write,
            int timeoutMillis,
            Func<long> clockMillis,
            Action<int> sleepMillis,
            out Exception lastFailure)
        {
            if (write == null) throw new ArgumentNullException(nameof(write));
            if (clockMillis == null) throw new ArgumentNullException(nameof(clockMillis));
            if (sleepMillis == null) throw new ArgumentNullException(nameof(sleepMillis));

            lastFailure = null;

            // ≤ 0 的预算 = 「不重试」的显式表达(不是错误)。调用方借此关闭重试。
            if (timeoutMillis <= 0)
            {
                try { write(); return true; }
                catch (Exception e) { lastFailure = e; return false; }
            }

            long start = clockMillis();
            long deadline = start + timeoutMillis;
            int delay = InitialDelayMillis;

            // ⚠️ 循环条件用「**已经**过没过截止」而不是「还剩多久」:
            //    后者依赖时钟两次读数之间的精度,假时钟 + 零 sleep 时会退化成无限循环。
            while (true)
            {
                // ⚠️ **成功时必须把 `lastFailure` 清回 null** —— 否则「先失败两次、第三次成功」
                //    会带着第 2 次的异常返回 true,调用方若据 `lastFailure != null` 判成败
                //    就会把**成功**读成**失败**(把已落盘的世界当成没落盘,置上不该置的标志位)。
                //    首版漏了这一句,由 `test_retrier_retriesUntilSuccess` 抓到(2026-10-25)。
                try { write(); lastFailure = null; return true; }
                catch (Exception e)
                {
                    lastFailure = e;

                    long now = clockMillis();
                    if (now >= deadline) return false;

                    // 退避不得越过截止 —— 否则「1 秒预算」会实际睡成 1 秒 + 一次写。
                    long remaining = deadline - now;
                    int sleep = (int)Math.Min(delay, remaining);
                    if (sleep > 0) sleepMillis(sleep);

                    delay = Math.Min(delay * 2, MaxDelayMillis);
                }
            }
        }
    }
}
