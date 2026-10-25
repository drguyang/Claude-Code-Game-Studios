// ADR-010 §六 / GDD `persistence-service.md` 规则五 —— 后台写盘 worker(one-writer 单飞队列)。
//
// 权威来源:
//   GDD `:117-123` —— 规则五(主线程只序列化,写盘交后台线程)
//   GDD `:230-245` —— 写入状态机(`WritingAsync` / `Committing` / in-flight 守卫)
//   GDD `:507-514` —— AC-7a-07(BLOCKING):写盘 IO 与 SHA256 在非主线程;
//                     `IOException` 按 `WRITE_RETRY_DELAY` 退避重试
//   GDD `:417` —— 旋钮 `WRITE_RETRY_DELAY`(默认 100 ms,安全区间 50 ms–1 s)
//   ADR-010 §六 —— 退出保存从 `WritingAsync` 起限时 join
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么队列持有的是 `Action` 工作项,而不是 `byte[]`
// ─────────────────────────────────────────────────────────────────────────────
// 一份存档在后台要做**三件**事,且它们必须在**同一条线程**上按序完成:
//   ① 算 SHA256 并回填校验和段(`AddChecksum`)—— AC-7a-07 要求它在非主线程;
//   ② 原子写(`WriteAtomic`,路径随槽位不同);
//   ③ 写成功后推进槽位高水位(`SaveLedger.OnCheckpointCommitted`)。
// 三者共享 `(slot, bytes)` 上下文 ⇒ 由 `SaveService` 在**入队处**闭成一个
// `Action`,队列只负责「何时跑 / 哪条线程跑 / 失败重试几次」。
//
// ⚠️ **重试会重算 SHA256** —— 这是有意的取舍。拆分「准备一次 + 写重试 N 次」能省掉
//    重试时的哈希,但那要求队列认识「准备/写」两段语义,把一个通用调度器变成
//    存档专用件。而存档体是 KB 级、SHA256 是 μs 级、重试只在 `IOException` 时发生
//    (杀软 / OneDrive 锁),重算的代价远小于多一层抽象的代价。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本类**不碰任何文件 API**
// ─────────────────────────────────────────────────────────────────────────────
// `AC-7a-08③` 的 IL 门(`SaveWritePathGate`)判据 B = 「`Sim.Codec` 程序集内
// **除 `SaveService` 外**零写盘调用」。本类住在同一个程序集 ⇒ 若它直接
// `File.WriteAllBytes`,门会转红。
//
// 三条出路,取第三条:
//   ① 放宽门 —— 会把「存档体只有一个写入口」这条不变量打折,**否决**;
//   ② 本类搬去别的程序集 —— 新增装配边,且 worker 只服务 `SaveService`,无收益,**否决**;
//   ③ **写动作以 `Action` 注入**,本类只负责「什么时候写 / 在哪条线程写 /
//      失败了重试几次」,**不碰任何文件 API** —— 门一字不改,且不变量更强:
//      `Sim.Codec` 内写盘原语仍然**恰好一处**(`SaveService::WriteAtomic`)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 重试用的是 `WRITE_RETRY_DELAY`,不是 `SAVE_EXIT_RETRY_TIMEOUT`
// ─────────────────────────────────────────────────────────────────────────────
// 两个旋钮的语义**不同**,此前被混用过(`TD-persist-06` 轮把
// `SAVE_EXIT_RETRY_TIMEOUT` 当成了重试循环总预算 —— 在同步写下两者恰好等价,
// 一旦异步化立刻分叉):
//   · `WRITE_RETRY_DELAY`(100 ms,`:417`)= **单次退避间隔**,`IOException` 后等多久再试;
//   · `SAVE_EXIT_RETRY_TIMEOUT`(1 s,`:416`)= **退出保存等后台 worker 的时间上限**
//     (`:244` 逐字「退出保存:从 `WritingAsync` 起限时 join」)。
// ⇒ 本类的退避间隔取前者;后者的落点在 `SaveService.SaveOnExit` 的 join 超时。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ one-writer 单飞(不是线程池并发)
// ─────────────────────────────────────────────────────────────────────────────
// GDD `:236` 逐字:「同一时刻至多一份存档在 `WritingAsync` / `Committing`
// (one-writer 单飞队列)—— 上一份未 `rename` 完成时新触发 ⇒ **排队不并发**,
// 防两条 `final→bak` + `tmp→final` rename 竞态把 `bak` 覆盖成半途产物。」
//
// ⇒ 本类**恒单线程**跑工作项:即使调度器把任务丢进线程池,内部也只在一次
// `Drain` 调用内串行执行。调度器只决定「什么时候开始跑」,不决定「并行度」。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ worker 顶层全兜 `try/catch`(GDD `:117`)
// ─────────────────────────────────────────────────────────────────────────────
// 「未观察异常可终止 IL2CPP 进程 —— 与『玩家零打断』冲突」。⇒ 每一个工作项的
// 全部异常都在本类内被观察并记录,`Drain` 的线程体本身也全兜。
// 失败**不上抛到线程池**(那会成为未观察异常),而是经 `LastFailure` 暴露给调用方。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 后台写盘 worker —— **one-writer 单飞队列** + `IOException` 退避重试 + 顶层全兜。
    /// <para><b>本类不碰任何文件 API</b> —— 工作项由调用方闭成 <see cref="Action"/> 注入
    /// (理由见文件头:保 <c>AC-7a-08③</c> 的「唯一写盘原语」不变量)。</para>
    /// <para><b>线程安全</b>:<see cref="Enqueue"/> / <see cref="WaitForIdle()"/> 可从主线程调,
    /// 内部以锁串行化;工作项恒在 worker 线程上执行。</para>
    /// </summary>
    public sealed class SaveWriteQueue
    {
        /// <summary>
        /// `IOException` 的**单次退避间隔**(ms,`WRITE_RETRY_DELAY` 旋钮 · GDD `:417`)。
        /// <para>安全区间 50 ms–1 s,默认 100 ms。⚠️ 这**不是** `SAVE_EXIT_RETRY_TIMEOUT`
        /// —— 后者是退出保存等 worker 的上限,落点在 <see cref="SaveService.SaveOnExit"/>。</para>
        /// </summary>
        public const int DefaultRetryDelayMillis = 100;

        /// <summary>
        /// 单份存档最多尝试几次。
        /// <para>⚠️ 这个数**不是新旋钮** —— GDD 的旋钮表只有退避**间隔**
        /// (`WRITE_RETRY_DELAY`),没有次数。取 5 的依据:5 × 100 ms = 500 ms,
        /// 落在退出保存的 1 s 预算内(再多的重试对退出路径没有意义,它等不到);
        /// 而 checkpoint 路径没有时间预算,5 次足以越过杀软扫描的典型窗口
        /// (扫描通常在数百 ms 内结束)。**该值归实现纪律,不立旋钮**(GDD `:282-283`)。</para>
        /// </summary>
        public const int DefaultMaxAttempts = 5;

        private readonly int _retryDelayMillis;
        private readonly int _maxAttempts;

        /// <summary>待跑队列。锁 = <see cref="_gate"/>。</summary>
        private readonly Queue<Action> _pending = new Queue<Action>();

        /// <summary>
        /// 队列 + 状态的唯一锁。
        /// <para>⚠️ 工作项**不得**在持锁期间执行 —— 否则主线程的 <see cref="Enqueue"/>
        /// 会被磁盘 IO 阻塞,异步化当场失效。</para>
        /// </summary>
        private readonly object _gate = new object();

        /// <summary>worker 是否已在跑(单飞守卫)。</summary>
        private bool _draining;

        /// <summary>已入队但尚未处理完的份数(含正在跑的那一份)。</summary>
        private int _outstanding;

        private Exception _lastFailure;

        /// <summary>调度器(生产 = `Task.Run`;夹具 = 同步内联)。由 <see cref="SaveService"/> 传入。</summary>
        private readonly Action<Action> _scheduler;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="scheduler">
        /// 启动 worker 的调度器。<b>不得为 null</b>(由 <see cref="SaveService"/> 的
        /// fail-closed 检查保证)—— 本类不提供「缺省同步内联」,那会让「生产忘了传
        /// `Task.Run`」静默退化为同步而测试全绿。
        /// </param>
        /// <param name="retryDelayMillis">退避间隔(`WRITE_RETRY_DELAY`)。</param>
        /// <param name="maxAttempts">单份最多尝试次数。</param>
        /// <exception cref="ArgumentNullException"><paramref name="scheduler"/> 为 null。</exception>
        public SaveWriteQueue(
            Action<Action> scheduler,
            int retryDelayMillis = DefaultRetryDelayMillis,
            int maxAttempts = DefaultMaxAttempts)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _retryDelayMillis = retryDelayMillis;
            _maxAttempts = maxAttempts < 1 ? 1 : maxAttempts;
        }

        /// <summary>最近一次工作项失败的异常;成功一次后清回 <c>null</c>。</summary>
        public Exception LastFailure
        {
            get { lock (_gate) { return _lastFailure; } }
        }

        /// <summary>已入队但尚未处理完的份数(诊断用)。</summary>
        public int Outstanding
        {
            get { lock (_gate) { return _outstanding; } }
        }

        /// <summary>worker 是否正在跑(单飞守卫,诊断用)。</summary>
        public bool IsDraining
        {
            get { lock (_gate) { return _draining; } }
        }

        /// <summary>
        /// 入队一份待跑的工作项。**立即返回**,不等它跑完。
        /// <para>若 worker 未在跑 ⇒ 经调度器启动;已在跑 ⇒ 只入队(单飞:排队不并发)。</para>
        /// </summary>
        /// <param name="work">工作项(调用方保证非 null)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="work"/> 为 null。</exception>
        public void Enqueue(Action work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));

            bool needStart;
            lock (_gate)
            {
                _pending.Enqueue(work);
                _outstanding++;
                needStart = !_draining;
                if (needStart) _draining = true;
            }

            // ⚠️ 调度在锁**外** —— 调度器可能同步内联执行(夹具),持锁会自死锁。
            if (needStart) _scheduler(Drain);
        }

        /// <summary>
        /// 等队列排空(**无限等待**)。空参 —— 不违反 `AC-7a-08` 判据 ①。
        /// <para>⚠️ <b>本方法只供夹具与装配收尾用</b>,生产主线程**不得**在 Step 边界调它
        /// (那等于把异步化撤销)。生产唯一的等待点是 <see cref="SaveService.SaveOnExit"/>。</para>
        /// </summary>
        /// <returns>排空 ⇒ true。</returns>
        public bool WaitForIdle() => WaitForIdle(Timeout.Infinite);

        /// <summary>
        /// 等队列排空(限时)。**internal** —— 带形参,不进公开面。
        /// </summary>
        /// <param name="timeoutMillis">
        /// 等待上限(ms)。<see cref="Timeout.Infinite"/> = 无限等。
        /// </param>
        /// <returns>排空 ⇒ true;超时 ⇒ false(队列可能仍在跑)。</returns>
        internal bool WaitForIdle(int timeoutMillis)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                lock (_gate)
                {
                    if (_outstanding == 0 && !_draining) return true;
                }

                if (timeoutMillis != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMillis)
                    return false;

                // 自旋等待:worker 的工作量是磁盘 IO,粒度在 ms 级;
                // 用 1 ms 让出而不是 Thread.Yield(后者在单核上会烧满)。
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// worker 线程体 —— **串行**处理队列(one-writer 单飞)。
        /// <para>⚠️ 本方法整体全兜 —— 任何逃逸异常都会成为线程池的未观察异常
        /// (IL2CPP 下可终止进程),与「玩家零打断」冲突。</para>
        /// </summary>
        private void Drain()
        {
            try
            {
                while (true)
                {
                    Action next;
                    lock (_gate)
                    {
                        if (_pending.Count == 0)
                        {
                            // 队列空 ⇒ 退出并清单飞标记(与 Enqueue 的 needStart 配对)。
                            // ⚠️ 两者必须在**同一把锁**内完成,否则「刚判空、还没清标记」
                            //    的窗口里 Enqueue 会看到 _draining = true 而不启动新 worker
                            //    ⇒ 那份工作项永久滞留。
                            _draining = false;
                            return;
                        }
                        next = _pending.Dequeue();
                    }

                    // ⚠️ 工作项在锁**外**执行 —— 持锁会把主线程的 Enqueue 一起卡住。
                    TryRunWithRetry(next);

                    lock (_gate) { _outstanding--; }
                }
            }
            catch (Exception e)
            {
                // 顶层兜底:记录并清状态,绝不让异常逃逸出线程体。
                lock (_gate)
                {
                    _lastFailure = e;
                    _draining = false;
                    // ⚠️ 残留的 _outstanding 必须清零 —— 否则 WaitForIdle 永久等不到 0
                    //    (退出保存会白等满整个预算)。
                    _outstanding = 0;
                    _pending.Clear();
                }
            }
        }

        /// <summary>
        /// 单个工作项的跑 + `IOException` 退避重试。
        /// <para>只重试 <see cref="System.IO.IOException"/> 与
        /// <see cref="UnauthorizedAccessException"/>(杀软 / OneDrive 锁的两种典型表现);
        /// 其余异常(参数错 / 空引用)是**编程错误**,重试只会拖长暴露时间。</para>
        /// </summary>
        private void TryRunWithRetry(Action work)
        {
            for (int attempt = 1; attempt <= _maxAttempts; attempt++)
            {
                try
                {
                    work();
                    lock (_gate) { _lastFailure = null; }
                    return;
                }
                catch (Exception e) when (IsRetriable(e))
                {
                    bool last = attempt == _maxAttempts;
                    lock (_gate) { _lastFailure = e; }
                    if (last) return;   // 用尽次数 ⇒ 记录最后一次失败并放弃本份

                    Thread.Sleep(_retryDelayMillis);
                }
                catch (Exception e)
                {
                    // 不可重试 ⇒ 记录并放弃本份(不上抛 —— 见文件头「顶层全兜」)。
                    lock (_gate) { _lastFailure = e; }
                    return;
                }
            }
        }

        /// <summary>
        /// 是否属于「重试可能有意义」的失败。
        /// <para>GDD `:510` 点名的场景 = 杀软 / OneDrive 锁文件 ⇒ `IOException`。
        /// `UnauthorizedAccessException` 是同一场景在 Windows 上的另一种落点
        /// (文件被独占打开时 `File.Move` 抛它而非 `IOException`),故一并收。</para>
        /// </summary>
        private static bool IsRetriable(Exception e)
            => e is System.IO.IOException || e is UnauthorizedAccessException;
    }
}
