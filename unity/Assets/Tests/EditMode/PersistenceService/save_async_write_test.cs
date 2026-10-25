// AC-7a-07(BLOCKING)—— 后台线程写盘 / SHA256 · IOException 退避 · 顶层全兜
//
// 权威来源:
//   GDD `persistence-service.md:507-514` —— AC-7a-07 原文
//   GDD `:117-123` —— 规则五(主线程只序列化,写盘交后台线程;SHA256 也在后台;缓冲池)
//   GDD `:230-245` —— 写入状态机(`WritingAsync` / `Committing` / in-flight 单飞)
//   GDD `:417` —— `WRITE_RETRY_DELAY`(100 ms)
//   GDD `:244` —— 退出保存「从 `WritingAsync` 起限时 join」
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件的判据形状:断言「**在哪条线程**」,不是「多久完成」
// ─────────────────────────────────────────────────────────────────────────────
// 最容易被写歪的一类假绿是:用 `Thread.Sleep` + 「文件出现了」来证明「异步」。
// 那既慢又不稳定,而且**根本不能区分**「后台线程写的」与「主线程写的但慢」。
//
// ⇒ 本文件的判据一律是**捕获 `ManagedThreadId` 并与主线程比对**:
//    · 生产路径(`Task.Run`)下,写盘线程 id 必须 **≠** 主线程 id;
//    · 对照组(同步内联)下必须 **=**,以证明夹具本身有判别力(不是恒真断言)。
// 这一对互为反证 —— 缺了对照组,「≠」可能只是调度器换了个线程跑内联。

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class SaveAsyncWriteTest
    {
        private string _testDir;

        [SetUp]
        public void SetUp()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_async_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testDir)) Directory.Delete(_testDir, recursive: true);
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 最小合法存档体(不含校验和)—— 与 `SaveService.AddChecksum` 的入参同形:
        /// magic(4) + saveVersion(4) + worldSeed(8) + configVersion(4) + tick(8) + snapshotOffset(8)。
        /// </summary>
        private static byte[] MinimalUnsigned()
        {
            var b = new byte[36];
            b[0] = 0x44; b[1] = 0x59; b[2] = 0x4A; b[3] = 0x51;   // "DYJQ"
            return b;
        }

        private SaveService Service(
            Action<Action> scheduler,
            Func<SaveSlot, byte[]> compose = null,
            int exitSaveRetryMillis = 0,
            Action<Exception, Exception> exitSaveFailureSink = null)
        {
            return new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: compose ?? (_ => MinimalUnsigned()),
                exitSaveRetryMillis: exitSaveRetryMillis,
                exitSaveFailureSink: exitSaveFailureSink,
                writeScheduler: scheduler);
        }

        /// <summary>同步内联调度器 —— 断言确定,用于「对照组」。</summary>
        private static Action<Action> Inline => action => action();

        // ══════════════════════════════════════════════════════════════════
        // ① 写盘与 SHA256 真的在**非主线程**(AC-7a-07 的核心字面)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_checkpoint_writeRunsOffMainThread()
        {
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int writeThread = 0;

            // 观测手法:包一层调度器,在 worker **真正跑工作项**的那一刻取线程 id。
            // ⚠️ 不在 compose 里取 —— 组版在**主线程**(GDD `:509`),那里取到的是主线程,
            //    会把「异步」验成「同步」。
            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: action => Task.Run(() =>
                {
                    writeThread = Thread.CurrentThread.ManagedThreadId;
                    action();
                }));

            svc.Checkpoint(new SaveSlot(0));
            Assert.IsTrue(svc.WriteQueue.WaitForIdle(), "队列须排空");

            Assert.AreNotEqual(0, writeThread, "写盘线程须被观测到(否则本断言无判别力)");
            Assert.AreNotEqual(mainThread, writeThread,
                "写盘 IO 与 SHA256 须在**非主线程**(AC-7a-07 BLOCKING)");
        }

        [Test]
        public void test_checkpoint_inlineScheduler_staysOnMainThread_controlGroup()
        {
            // ⚠️ **对照组**(反证「上一条不是恒真」):同一个观测手法,换成同步内联调度器,
            //    写线程必须**等于**主线程。若这一条也「≠」,说明上一条的 ≠ 来自别处
            //    (例如 NUnit 自身的线程模型),上一条就失去判别力。
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int writeThread = 0;

            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: action =>
                {
                    writeThread = Thread.CurrentThread.ManagedThreadId;
                    action();
                });

            svc.Checkpoint(new SaveSlot(0));

            Assert.AreEqual(mainThread, writeThread,
                "同步内联调度器下,写盘须在调用线程(对照组:证明上一条的 ≠ 有判别力)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② SHA256 不在主线程 —— 主线程返回时存档**尚未签名/落盘**
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_checkpoint_returnsBeforeDiskWrite_completes()
        {
            // 用一个**闸门**把 worker 卡住:若 Checkpoint 真的只是「入队即返回」,
            // 它必须在闸门仍关着的时候返回。若它同步等写盘,本测会挂住(超时由 NUnit 兜)。
            var gate = new ManualResetEventSlim(false);

            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: action => Task.Run(() =>
                {
                    gate.Wait(TimeSpan.FromSeconds(10));   // 卡住 worker
                    action();
                }));

            svc.Checkpoint(new SaveSlot(0));

            // Checkpoint 已返回,而 worker 还卡在闸门上 ⇒ 文件必不在
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "save_0.bin")),
                "Checkpoint 返回时**不得**已落盘 —— 否则写盘仍在调用线程上(AC-7a-07)");

            gate.Set();
            Assert.IsTrue(svc.WriteQueue.WaitForIdle(10000), "放闸后队列须排空");
            Assert.IsTrue(File.Exists(Path.Combine(_testDir, "save_0.bin")),
                "worker 跑完后 final 须在场");
        }

        [Test]
        public void test_checkpoint_signedFileValidatesChecksum()
        {
            // SHA256 搬到 worker 之后,产出的存档仍须是**含有效校验和**的完整档 ——
            // 否则「异步」会把存档写坏(这是异步化最容易引入的回归)。
            var svc = Service(scheduler: Inline);
            svc.Checkpoint(new SaveSlot(0));

            byte[] onDisk = File.ReadAllBytes(Path.Combine(_testDir, "save_0.bin"));
            Assert.IsTrue(SaveService.ValidateChecksum(onDisk),
                "worker 补的校验和须有效 —— 异步化不得把存档写坏");
            Assert.AreEqual(SaveHeaderCodec.HeaderSize + 0, onDisk.Length,
                "长度 = 头部(含校验和槽)+ 空正文;36 + 32 = 68");
        }

        [Test]
        public void test_compose_isCalledOnMainThread_notOnWorker()
        {
            // GDD `:509` 逐字「**序列化在 Step 边界主线程完成**」。
            // ⇒ 组版(compose)必须在主线程;只有 SHA256 + 写盘在后台。
            // 这条同时守住 b2 门 A 的实质:compose 读 sim 状态(后台线程碰不到)。
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            int composeThread = 0;

            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ =>
                {
                    composeThread = Thread.CurrentThread.ManagedThreadId;
                    return MinimalUnsigned();
                },
                writeScheduler: action => Task.Run(action));

            svc.Checkpoint(new SaveSlot(0));
            svc.WriteQueue.WaitForIdle(10000);

            Assert.AreEqual(mainThread, composeThread,
                "组版须在主线程(GDD `:509`)—— 只有 SHA256 + 写盘在后台");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ in-flight = one-writer 单飞(不并发)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_twoCheckpoints_neverWriteConcurrently()
        {
            // GDD `:236`:「同一时刻至多一份存档在 WritingAsync / Committing」。
            // 判据 = 观测 **worker 执行工作项时**的并发度,必须恒为 1。
            //
            // 🔁 **2026-10-25 证伪轮抓出的假绿(MA1)**:
            //    首版把并发计数放在 `compose` 里 —— 而 `compose` 在**主线程串行**执行
            //    (两次 `Checkpoint` 是顺序调用)⇒ `maxConcurrent` **恒为 1**,
            //    对 worker 的并发**零判别力**。
            //    实证:把 `Drain` 改成并发跑(MA1),本测**仍然绿**,而同轮另 8 条转红。
            //    ⇒ 首版注释里那句「本测观测的是写侧」是**自述**,实现没做到。
            //
            // 现判据:直接对**队列**压两个工作项,在**工作项体内**计数。
            // 工作项由 worker 执行 ⇒ 并发度就是 worker 的并发度。
            int concurrent = 0;
            int maxConcurrent = 0;
            object sync = new object();
            var gate = new ManualResetEventSlim(false);

            var queue = new SaveWriteQueue(
                scheduler: action => Task.Run(action),
                retryDelayMillis: 1);

            Action work = () =>
            {
                lock (sync)
                {
                    concurrent++;
                    if (concurrent > maxConcurrent) maxConcurrent = concurrent;
                }
                try
                {
                    // 卡住第一份 ⇒ 若并发,第二份会同时进来把 concurrent 顶到 2
                    gate.Wait(TimeSpan.FromMilliseconds(300));
                }
                finally
                {
                    lock (sync) { concurrent--; }
                }
            };

            queue.Enqueue(work);
            queue.Enqueue(work);

            // 给足时间让「若并发」的情形发生
            Thread.Sleep(120);
            gate.Set();
            Assert.IsTrue(queue.WaitForIdle(10000), "队列须排空");

            Assert.AreEqual(1, maxConcurrent,
                "同一时刻至多一份存档在写(one-writer 单飞, GDD `:236`)—— " +
                "判据取自**工作项体内**的并发度,不是 compose(后者在主线程恒串行)");
        }

        [Test]
        public void test_highWaterMarkAdvancesOnlyAfterWriteSucceeds()
        {
            // 🔁 用户 2026-10-25 裁定「受理时推」:Checkpoint 返回时高水位**未动**,
            //    写成功后才推。这条保住 `SaveSlotLedger.OnCheckpointCommitted` 的
            //    既有不变量「写失败则高水位不动,否则一次失败的写会永久吃掉一个槽位」。
            var gate = new ManualResetEventSlim(false);
            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: action => Task.Run(() =>
                {
                    gate.Wait(TimeSpan.FromSeconds(10));
                    action();
                }));

            Assert.AreEqual(0U, svc.Ledger.NextWritableSeq, "前提:高水位从 0 起");
            svc.Checkpoint(new SaveSlot(0));

            Assert.AreEqual(0U, svc.Ledger.NextWritableSeq,
                "Checkpoint 返回时高水位**未动**(受理 ≠ 已落盘)");

            gate.Set();
            svc.WriteQueue.WaitForIdle(10000);

            Assert.AreEqual(1U, svc.Ledger.NextWritableSeq,
                "写成功后高水位推进到 slot+1");
        }

        [Test]
        public void test_writeFailure_doesNotAdvanceHighWaterMark()
        {
            // 反证「写失败 ⇒ 槽位不被吃掉」:让写盘必失败(目标路径是目录),
            // 高水位必须停在原处 —— 否则该槽位永久不可再写。
            string basePath = Path.Combine(_testDir, "failing");
            Directory.CreateDirectory(basePath);
            // 在 final 路径上建**同名目录** ⇒ File.Move 必抛
            Directory.CreateDirectory(Path.Combine(basePath, "save_0.bin"));

            var svc = new SaveService(
                basePath,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: action => action());

            svc.Checkpoint(new SaveSlot(0));

            Assert.IsNotNull(svc.WriteQueue.LastFailure, "写盘须失败并被 worker 记录");
            Assert.AreEqual(0U, svc.Ledger.NextWritableSeq,
                "写失败 ⇒ 高水位不动(槽位不得被一次失败的写永久吃掉)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ IOException 按 WRITE_RETRY_DELAY 退避重试(AC-7a-07 第三句)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ioException_isRetriedWithWriteRetryDelay()
        {
            // 判据 = **观测重试间隔**是否等于 `WRITE_RETRY_DELAY`(100 ms 默认)。
            // ⚠️ 用真实时间测:间隔是 100 ms 量级,不会让测试变慢(2 次 ≈ 200 ms)。
            //    夹具不引入假时钟,因为「退避是不是真的等了」正是这条要证的东西。
            int attempts = 0;
            var stamps = new List<long>();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var queue = new SaveWriteQueue(
                scheduler: action => action(),
                retryDelayMillis: 100,
                maxAttempts: 3);

            queue.Enqueue(() =>
            {
                attempts++;
                stamps.Add(sw.ElapsedMilliseconds);
                if (attempts < 3) throw new IOException("模拟杀软锁文件");
            });

            queue.WaitForIdle(10000);

            Assert.AreEqual(3, attempts, "IOException ⇒ 须重试到成功(共 3 次尝试)");
            Assert.IsNull(queue.LastFailure, "最终成功 ⇒ LastFailure 须清回 null(不得留残留)");
            Assert.GreaterOrEqual(stamps[1] - stamps[0], 90,
                "第一次重试前须等约 WRITE_RETRY_DELAY(100 ms)—— 不是零延迟自旋");
            Assert.GreaterOrEqual(stamps[2] - stamps[1], 90,
                "第二次重试同样须退避");
        }

        [Test]
        public void test_ioException_exhaustsAttemptsThenGivesUp()
        {
            // 重试用尽 ⇒ 放弃本份,记录最后一次失败,**不抛**(顶层全兜)。
            int attempts = 0;
            var queue = new SaveWriteQueue(
                scheduler: action => action(),
                retryDelayMillis: 1,       // 本测只验次数,不验间隔 ⇒ 压到 1 ms 保快
                maxAttempts: 4);

            queue.Enqueue(() =>
            {
                attempts++;
                throw new IOException("永远锁着");
            });

            queue.WaitForIdle(10000);

            Assert.AreEqual(4, attempts, "用尽 4 次尝试后放弃");
            Assert.IsNotNull(queue.LastFailure, "失败须被记录");
            StringAssert.Contains("永远锁着", queue.LastFailure.Message);
        }

        [Test]
        public void test_nonRetriableException_isNotRetried()
        {
            // 编程错误(参数错 / 空引用)重试只会拖长暴露时间 ⇒ 一次即放弃。
            int attempts = 0;
            var queue = new SaveWriteQueue(scheduler: action => action(), retryDelayMillis: 1);

            queue.Enqueue(() =>
            {
                attempts++;
                throw new InvalidOperationException("编程错误,不该重试");
            });

            queue.WaitForIdle(10000);

            Assert.AreEqual(1, attempts, "非 IO 异常须**一次即弃**,不重试");
            Assert.IsInstanceOf<InvalidOperationException>(queue.LastFailure);
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ worker 顶层全兜(GDD `:117`「未观察异常可终止 IL2CPP 进程」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_workerSwallowsException_queueStaysUsable()
        {
            // 若异常从 worker 逃逸 ⇒ 线程池未观察异常(IL2CPP 可终止进程)。
            // 判据:失败之后队列仍能接受并跑完下一份。
            int ran = 0;
            var queue = new SaveWriteQueue(scheduler: action => Task.Run(action), retryDelayMillis: 1);

            queue.Enqueue(() => throw new InvalidOperationException("炸"));
            queue.WaitForIdle(10000);

            queue.Enqueue(() => ran++);
            queue.WaitForIdle(10000);

            Assert.AreEqual(1, ran, "前一份失败后,队列须仍可用(worker 未因异常停摆)");
        }

        [Test]
        public void test_workerFailure_outstandingDropsToZero()
        {
            // ⚠️ 若顶层兜底不清 `_outstanding`,WaitForIdle 会永久等不到 0
            //    ⇒ 退出保存白等满整个预算(卡退 1 秒)。
            var queue = new SaveWriteQueue(scheduler: action => Task.Run(action), retryDelayMillis: 1);

            queue.Enqueue(() => throw new InvalidOperationException("炸"));
            bool idle = queue.WaitForIdle(5000);

            Assert.IsTrue(idle, "失败后 outstanding 须归零(否则退出保存会白等满预算)");
            Assert.AreEqual(0, queue.Outstanding);
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 退出保存 = 入队 + 限时 join(GDD `:244`)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveOnExit_joinsWorkerAndWritesFile()
        {
            // ⚠️ 本测的语义订正后,**必须给真 join 预算**(清债轮 2026-10-25 修):
            //    助手默认 `exitSaveRetryMillis = 0` 在「join 超时」语义下 = 等 0 ms,
            //    配 `Task.Run` 变成「worker 赢竞速才绿」的负载相关 flake
            //    (c0c1e4c 语义订正的夹具侧遗留;全量回归实测掀出)。
            var svc = Service(scheduler: action => Task.Run(action), exitSaveRetryMillis: 5000);
            svc.SaveOnExit();

            Assert.IsTrue(File.Exists(Path.Combine(_testDir, SaveService.ExitSaveFileName)),
                "退出保存须真的落盘(入队后限时 join 成功)");
            byte[] onDisk = File.ReadAllBytes(Path.Combine(_testDir, SaveService.ExitSaveFileName));
            Assert.IsTrue(SaveService.ValidateChecksum(onDisk), "退出保存同样须含有效校验和");
        }

        [Test]
        public void test_saveOnExit_joinTimeout_raisesFlagInsteadOfThrowing()
        {
            // 限时 join 超时 ⇒ 置「会话未完整」标志位 + **不抛**(不卡退)。
            var gate = new ManualResetEventSlim(false);
            Exception reportedWrite = null;
            bool sinkCalled = false;

            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                exitSaveRetryMillis: 50,           // 极短 join 预算
                exitSaveFailureSink: (w, f) => { sinkCalled = true; reportedWrite = w; },
                writeScheduler: action => Task.Run(() =>
                {
                    gate.Wait(TimeSpan.FromSeconds(10));   // 卡住 worker ⇒ join 必超时
                    action();
                }));

            Assert.DoesNotThrow(() => svc.SaveOnExit(),
                "join 超时 ⇒ 不得抛(退出路径上抛 = 卡退)");

            Assert.IsTrue(SaveIncompleteFlag.IsRaised(_testDir),
                "join 超时 ⇒ 须置「会话未完整」标志位(下次启动提示)");
            // ⚠️ **判据是「观察者被调用」,不是「收到的异常非空」**(2026-10-25 自查修正)。
            //    join 超时 ≠ 写盘失败 —— worker 可能只是**慢**,`LastFailure` 此时**就是 null**。
            //    首版断言 `IsNotNull(reportedWrite)` 把「慢」误判成「失败」,
            //    而生产代码的语义是「两条线索都交给装配层,不假设非空」(见 SaveService 注释)。
            //    ⇒ 本测改为观测「sink 被调用过」—— 用哨兵初值区分「没调用」与「调用了但传 null」。
            Assert.IsTrue(sinkCalled,
                "须把失败线索交给观察者(记日志)—— GDD「不阻塞退出 **+ 记日志**」的后半句");
            Assert.IsNull(reportedWrite,
                "join 超时 ≠ 写盘失败 ⇒ 交出的写盘异常**应为 null**(不得伪造成写失败)");

            gate.Set();
            svc.WriteQueue.WaitForIdle(10000);
        }

        [Test]
        public void test_saveOnExit_queuesBehindInFlightCheckpoint()
        {
            // GDD `:236` 的延伸:退出时若有一份 checkpoint 正在写,退出保存**必须排在它后面**
            // —— 否则两条 rename 会把 bak 覆盖成半途产物。
            // 判据 = 退出保存执行时,checkpoint 的写必须已完成。
            bool checkpointWritten = false;
            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                exitSaveRetryMillis: 10000,
                writeScheduler: action => Task.Run(action));

            svc.Checkpoint(new SaveSlot(0));
            svc.SaveOnExit();          // 内部会 join,须等 checkpoint 也写完

            checkpointWritten = File.Exists(Path.Combine(_testDir, "save_0.bin"));
            Assert.IsTrue(checkpointWritten,
                "退出保存返回时,先前入队的 checkpoint 也须已完成(单飞队列 FIFO)");
            Assert.IsTrue(File.Exists(Path.Combine(_testDir, SaveService.ExitSaveFileName)),
                "退出保存本身也须落盘");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑦ fail-closed:未注入 writeScheduler ⇒ 拒写(禁静默同步降级)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_checkpoint_withoutWriteScheduler_failsClosed()
        {
            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: null);

            var ex = Assert.Throws<InvalidOperationException>(
                () => svc.Checkpoint(new SaveSlot(0)),
                "未注入 writeScheduler ⇒ 须抛(fail-closed)—— 禁静默同步降级");
            StringAssert.Contains("writeScheduler", ex.Message, "报文须点名缺的那个驱动源");
            Assert.AreEqual(0, Directory.GetFiles(_testDir).Length,
                "fail-closed 不得留下任何文件");
        }

        [Test]
        public void test_saveOnExit_withoutWriteScheduler_failsClosed()
        {
            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: null);

            Assert.Throws<InvalidOperationException>(() => svc.SaveOnExit(),
                "未注入 writeScheduler ⇒ SaveOnExit 亦须抛(fail-closed)");
            Assert.IsFalse(SaveIncompleteFlag.IsRaised(_testDir),
                "配置错误不得伪装成「会话未完整」(那是磁盘故障的语义)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑧ 缓冲池(GDD `:123` GC 纪律)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_bufferPool_reusesSameLengthBuffer()
        {
            // 「池真的在复用」是可断言的事实:同长度两次 Rent 拿到**同一实例**。
            var pool = new SaveBufferPool();

            byte[] a = pool.Rent(1024);
            pool.Return(a);
            byte[] b = pool.Rent(1024);

            Assert.AreSame(a, b, "同长度的块须被复用(否则池等于没有)");
        }

        [Test]
        public void test_bufferPool_doesNotReuseDifferentLength()
        {
            // ⚠️ 本池按**精确长度**分桶(不是 ArrayPool 的 2 的幂)——
            //    因为 `byte[].Length` 在存档路径上就是「有效长度」的契约,
            //    返回超长数组会让 AddChecksum 把残留尾部算进 SHA256 ⇒ 静默坏档。
            var pool = new SaveBufferPool();

            byte[] a = pool.Rent(1024);
            pool.Return(a);
            byte[] b = pool.Rent(2048);

            Assert.AreNotSame(a, b, "不同长度不得复用");
            Assert.AreEqual(2048, b.Length, "返回的数组长度须**恰等于**请求长度");
            Assert.AreEqual(1024, pool.RetainedBytes, "1024 的块仍在池里");
        }

        [Test]
        public void test_bufferPool_dropsOversizedBuffer()
        {
            var pool = new SaveBufferPool(maxBufferBytes: 1024);

            byte[] big = new byte[2048];
            pool.Return(big);

            Assert.AreEqual(0, pool.RetainedBytes, "超过单块上限的块须直接丢弃(不入池)");
        }

        [Test]
        public void test_bufferPool_rejectsNegativeLength()
        {
            var pool = new SaveBufferPool();
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(-1));
        }

        [Test]
        public void test_checkpoint_usesInjectedBufferPool()
        {
            // 端到端:装配注入的池须**真的被用到** —— 否则「缓冲池」只是摆设。
            // 判据 = Checkpoint + WaitForIdle 之后池里出现了块(说明租出的被归还了)。
            var pool = new SaveBufferPool();
            var svc = new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalUnsigned(),
                writeScheduler: action => action(),
                bufferPool: pool);

            svc.Checkpoint(new SaveSlot(0));

            Assert.AreSame(pool, svc.BufferPool, "公开面须暴露装配注入的那一份");
            Assert.Greater(pool.RetainedBytes, 0,
                "写完后缓冲须回到池里(否则注入的池从未被用到)");
        }
    }
}
