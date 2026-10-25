// AC-7a-17 —— 退出保存失败:限时重试 + 会话未完整标志位(`TD-persist-06`)
//
// 权威来源:
//   GDD `persistence-service.md:195-198` —— 规则十三
//   GDD `persistence-service.md:581-586` —— AC-7a-17 全文(BLOCKING)
//   GDD `persistence-service.md:244` ——「退出保存:从 `WritingAsync` 起限时 join(默认 1s)」
//   GDD `persistence-service.md:610` —— 标志位两形态并列「头字段 / 标志文件」
//   ADR-010 §六 —— 退出保存;崩溃 / 强杀不触发退出钩子 ⇒ 由下次启动的标志检测兜底
//   `traceability-index.md:1030` —— `TR-persist-010`
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件覆盖什么、不覆盖什么
// ─────────────────────────────────────────────────────────────────────────────
// ✅ 覆盖:
//   ① `ExitSaveRetrier` 的限时语义(成功即返 / 超预算放弃 / 预算 ≤ 0 不重试 / 退避不越截止)
//   ② `SaveIncompleteFlag` 的命名与判据(存在**且非空**才 = 已置位)
//   ③ `SaveService.SaveOnExit` 的三种结局:成功 ⇒ 清位;写盘失败 ⇒ 置位 + 不抛;
//      驱动源缺失 ⇒ **抛**(不可重试,与写盘失败分开)
//   ④ 「退出路径不抛」这条硬约束(AC-7a-17「不卡退」)
//   ⑤ 置位 / 清位的**端到端往返**(坏目录 ⇒ 置位;修好后再次退出 ⇒ 清位)
//
// ❌ **不覆盖**:
//   · 「下次启动的提示」—— GDD `:586` 逐字「提示承载归 42 / 7b」,**7a 侧不出提示**。
//     本文件只断言**标志位被置上**(7a 的义务到此为止),不断言任何 UI。
//   · `BootRoot` 的 `Application.wantsToQuit` 接线 —— 那需要真 Unity 运行时,
//     属 PlayMode / 人工验证面。本文件是 EditMode 纯逻辑。
//
// ⚠️ **置位 / 清位不经 `SaveIncompleteFlag`** —— 该类今日是**纯命名 + 纯判据**(零写盘),
//    写面收在 `SaveService.RaiseIncompleteFlag` / `ClearIncompleteFlag`(私有,经
//    `SaveOnExit` 驱动)。原因:AC-7a-08③ 的 IL 门要求 `Sim.Codec` 内**仅
//    `SaveService::WriteAtomic` 一处**可达写盘 API(2026-10-25 用户裁定)。
//    ⇒ 本文件的置位 / 清位一律**走公开入口 `SaveOnExit`**,不直调内部方法。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveExitRetryAndFlagTest
    {
        private string _testDir;

        [SetUp]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_exit_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>假时钟:sleep 推进它。⇒ 「是否超预算」是**可数事实**,不是时间副产品。</summary>
        private sealed class FakeClock
        {
            private long _now;
            public List<int> Sleeps { get; } = new List<int>();

            public long Now() => _now;

            public void Sleep(int ms)
            {
                Sleeps.Add(ms);
                _now += ms;
            }
        }

        /// <summary>造一个最小合法存档(头部 + 空事件段 + 空折叠段 + 校验和)。</summary>
        private static byte[] MinimalSaveBytes()
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(1UL, 1U, 0L, 0UL);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            byte[] folded = { 0, 0, 0, 0 };
            var combined = new byte[header.Length + body.Length + folded.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);
            Array.Copy(folded, 0, combined, header.Length + body.Length, folded.Length);
            return SaveService.AddChecksum(combined);
        }

        /// <summary>
        /// 造一个**必然写失败**的存档目录:目录本身可写(构造器要 `CreateDirectory`),
        /// 但里面 `save_exit.bin` 是**同名子目录** ⇒ `WriteAtomic` 末步
        /// `File.Move(tmp, final)` 撞目录,必抛 <see cref="IOException"/>。
        /// <para>⚠️ 不能用「把 basePath 本身换成文件」—— `SaveService` 构造器先
        /// `Directory.CreateDirectory(_basePath)`,那样构造就先抛了,根本走不到 SaveOnExit。</para>
        /// </summary>
        private string MakeWriteFailingBase(string name)
        {
            string basePath = Path.Combine(_testDir, name);
            Directory.CreateDirectory(basePath);
            Directory.CreateDirectory(Path.Combine(basePath, SaveService.ExitSaveFileName));
            return basePath;
        }

        private SaveService ServiceAt(
            string basePath,
            int exitSaveRetryMillis = 0,
            Action<Exception, Exception> exitSaveFailureSink = null)
        {
            return new SaveService(
                basePath,
                new SaveCodec(),
                folded: null,
                narrativeContext: () => true,
                replaySink: _ => { },
                foldedRowSink: _ => { },
                compose: _ => MinimalSaveBytes(),
                exitSaveRetryMillis: exitSaveRetryMillis,
                exitSaveFailureSink: exitSaveFailureSink,
                // 🔁 2026-10-25 AC-7a-07:同步内联调度器 —— 退出保存的「限时 join」
                // 在同步调度器下退化为「同步写完即返回」,本文件的 23 条判据语义不变。
                // ⚠️ 真后台线程的 join 语义由 `save_async_write_test.cs` 单独覆盖
                // (那里用受控线程 + 真 Task.Run)。
                writeScheduler: action => action());
        }

        private SaveService ReadyService() => ServiceAt(_testDir);

        /// <summary>盘上标志文件路径(判据用字面量名,不引用被测常量 —— 防自指)。</summary>
        private string FlagPath(string basePath)
            => Path.Combine(basePath, "session_incomplete.flag");

        // ══════════════════════════════════════════════════════════════════
        // ① ExitSaveRetrier —— 限时语义
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_retrier_succeedsOnFirstTry_noSleep()
        {
            var clock = new FakeClock();
            int calls = 0;

            bool ok = ExitSaveRetrier.TryWrite(
                () => calls++, 1000, clock.Now, clock.Sleep, out Exception failure);

            Assert.IsTrue(ok, "首次即成功 ⇒ 返回 true");
            Assert.IsNull(failure, "成功时 lastFailure 须为 null");
            Assert.AreEqual(1, calls, "首次成功不得重试");
            Assert.IsEmpty(clock.Sleeps, "首次成功不得 sleep");
        }

        [Test]
        public void test_retrier_retriesUntilSuccess()
        {
            var clock = new FakeClock();
            int calls = 0;

            bool ok = ExitSaveRetrier.TryWrite(
                () =>
                {
                    calls++;
                    if (calls < 3) throw new IOException("第 " + calls + " 次失败");
                },
                1000, clock.Now, clock.Sleep, out Exception failure);

            Assert.IsTrue(ok, "第 3 次成功 ⇒ 返回 true");
            // 🔁 2026-10-25:本条**抓到过**首版的真缺陷 —— 成功返回时 `lastFailure`
            //    残留着第 2 次的异常。调用方若据 `lastFailure != null` 判成败,
            //    会把**成功**读成**失败**(置上不该置的标志位)。修在 `ExitSaveRetrier`。
            Assert.IsNull(failure, "最终成功 ⇒ lastFailure 须清回 null(不得留残留)");
            Assert.AreEqual(3, calls, "须重试到成功为止");
            Assert.IsNotEmpty(clock.Sleeps, "两次失败之间须有退避");
        }

        [Test]
        public void test_retrier_givesUpWhenBudgetExhausted()
        {
            var clock = new FakeClock();
            int calls = 0;

            bool ok = ExitSaveRetrier.TryWrite(
                () => { calls++; throw new IOException("恒失败"); },
                100, clock.Now, clock.Sleep, out Exception failure);

            Assert.IsFalse(ok, "预算耗尽 ⇒ 返回 false");
            Assert.IsNotNull(failure, "失败时须交出最后一次异常(否则装配层无从记日志)");
            StringAssert.Contains("恒失败", failure.Message);
            // ⚠️ 判据用「假时钟推进量」而非「calls 次数」—— 次数依赖退避序列的具体值,
            //    那是实现细节;而「时钟推进量 ≤ 预算」是**契约本身**。
            Assert.LessOrEqual(clock.Now(), 100L,
                "假时钟推进量不得超过预算 —— 退避不得越过截止");
        }

        [Test]
        public void test_retrier_zeroBudget_attemptsExactlyOnce()
        {
            var clock = new FakeClock();
            int calls = 0;

            bool ok = ExitSaveRetrier.TryWrite(
                () => { calls++; throw new IOException("x"); },
                0, clock.Now, clock.Sleep, out Exception failure);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, calls, "预算 ≤ 0 ⇒ 只尝试一次(显式关闭重试)");
            Assert.IsEmpty(clock.Sleeps, "不重试 ⇒ 不得 sleep");
            Assert.IsNotNull(failure);
        }

        [Test]
        public void test_retrier_negativeBudget_attemptsExactlyOnce()
        {
            // 负数预算与 0 同义 —— 都表示「不重试」。**不得**因负数而跳过唯一那次尝试
            // (那会让「关闭重试」变成「关闭写盘」)。
            var clock = new FakeClock();
            int calls = 0;

            ExitSaveRetrier.TryWrite(() => calls++, -5, clock.Now, clock.Sleep, out _);

            Assert.AreEqual(1, calls, "负预算仍须尝试一次(不是零次)");
        }

        [Test]
        public void test_retrier_backoffGrowsAndIsCapped()
        {
            var clock = new FakeClock();

            ExitSaveRetrier.TryWrite(
                () => throw new IOException("x"),
                10000, clock.Now, clock.Sleep, out _);

            Assert.IsNotEmpty(clock.Sleeps);
            // 判据用**字面量**,不引用被测常量 —— 否则实现改了常量,判据跟着漂移(自指判据)。
            Assert.AreEqual(25, clock.Sleeps[0], "首次退避 = 25 ms(退避序列起点)");
            foreach (int s in clock.Sleeps)
                Assert.LessOrEqual(s, 400, "单次退避不得超过封顶 400 ms");
            // 退避须真的**增长**过 —— 否则「封顶」这条判据在常数序列下也恒绿。
            Assert.IsTrue(clock.Sleeps.Contains(50) && clock.Sleeps.Contains(100),
                "退避须按倍增长(须出现 50 与 100),不是常数序列");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② SaveIncompleteFlag —— 命名与判据(零写盘)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_flag_absentFile_isNotRaised()
        {
            Assert.IsFalse(SaveIncompleteFlag.IsRaised(_testDir),
                "文件不存在 ⇒ 未置位(不得默认 true)");
        }

        [Test]
        public void test_flag_emptyFile_isNotRaised()
        {
            // 清位 = 写 0 字节墓碑(不删除)。⇒ 「存在但空」必须读成**未置位**,
            // 否则清位后提示永不消失 —— 这是「只判 Exists」会静默失效的那一半。
            File.WriteAllBytes(FlagPath(_testDir), Array.Empty<byte>());

            Assert.IsFalse(SaveIncompleteFlag.IsRaised(_testDir),
                "0 字节墓碑 = 已清位 ⇒ 须读 false(只判 Exists 会读成 true)");
        }

        [Test]
        public void test_flag_nonEmptyFile_isRaised()
        {
            File.WriteAllBytes(FlagPath(_testDir), new byte[] { 0x01 });

            Assert.IsTrue(SaveIncompleteFlag.IsRaised(_testDir),
                "存在且非空 ⇒ 已置位");
        }

        [Test]
        public void test_flag_fileNameDoesNotCollideWithSaveFiles()
        {
            // 判据用**字面量**,不引用被测常量 —— 否则实现改了常量,判据跟着漂移(自指判据)。
            const string expected = "session_incomplete.flag";

            Assert.AreEqual(expected, SaveIncompleteFlag.FlagFileName,
                "标志文件名须为 " + expected);

            Assert.IsFalse(SaveIncompleteFlag.FlagFileName.StartsWith("save_"),
                "标志文件不是存档 —— 不得用 save_ 前缀(按 save_*.bin 扫目录的逻辑会误吃它)");
            Assert.IsFalse(SaveIncompleteFlag.FlagFileName.EndsWith(".bin"),
                "标志文件不是存档体 —— 不得用 .bin 后缀");
        }

        [Test]
        public void test_flag_tmpAndBakPaths_areDerivedFromFlagPath()
        {
            // 写面收在 SaveService 后,三路径由本类给 —— 若派生错,原子写会写到别处
            // (或与存档文件同名),而那**不会**让 SaveOnExit 抛(只是悄悄写错地方)。
            string flag = FlagPath(_testDir);

            Assert.AreEqual(flag + ".tmp", SaveIncompleteFlag.GetTmpPath(_testDir));
            Assert.AreEqual(flag + ".bak", SaveIncompleteFlag.GetBakPath(_testDir));
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ SaveOnExit —— 三种结局
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveOnExit_success_doesNotRaiseFlag()
        {
            ReadyService().SaveOnExit();

            Assert.IsFalse(SaveIncompleteFlag.IsRaised(_testDir),
                "成功落盘 ⇒ 不得置位");
        }

        [Test]
        public void test_saveOnExit_success_writesExitFileNotPlayerSlot()
        {
            // 退出保存须落**独立文件名**,不得占用玩家槽命名空间(W4 修正的判据面)。
            ReadyService().SaveOnExit();

            Assert.IsTrue(File.Exists(Path.Combine(_testDir, SaveService.ExitSaveFileName)),
                "退出保存须落 save_exit.bin");
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "save_0.bin")),
                "退出保存不得写玩家槽 0");
            Assert.IsFalse(File.Exists(Path.Combine(_testDir, "save_" + uint.MaxValue + ".bin")),
                "退出保存不得把 uint.MaxValue 当玩家槽名写出去");
        }

        [Test]
        public void test_saveOnExit_writeFailure_raisesFlagAndDoesNotThrow()
        {
            string basePath = MakeWriteFailingBase("writefail");
            var reported = new List<Exception>();

            var svc = ServiceAt(basePath,
                exitSaveFailureSink: (writeFailure, flagFailure) => reported.Add(writeFailure));

            Assert.DoesNotThrow(() => svc.SaveOnExit(),
                "写盘失败**不得抛** —— AC-7a-17「不阻塞退出」是硬约束");

            Assert.IsTrue(SaveIncompleteFlag.IsRaised(basePath),
                "写盘失败 ⇒ 须置「会话未完整」标志位(下次启动据此提示)");
            Assert.IsNotEmpty(reported, "失败须经观察者交出(装配层据此记日志)");
            Assert.IsNotNull(reported[0], "交出的第一项须是写盘异常");
        }

        [Test]
        public void test_saveOnExit_writeFailure_flagRaisedEvenWithoutSink()
        {
            // 观察者未注入时**置位仍须发生** —— 否则「装配层忘了注入 sink」
            // 会静默变成「标志位也不置」,两条兜底一起失效。
            string basePath = MakeWriteFailingBase("writefail2");

            ServiceAt(basePath).SaveOnExit();

            Assert.IsTrue(SaveIncompleteFlag.IsRaised(basePath),
                "未注入观察者 ⇒ 标志位仍须置上(置位不依赖日志通道)");
        }

        [Test]
        public void test_saveOnExit_writeFailure_isIdempotent()
        {
            // 退出路径上重试失败可能调用多次 ⇒ 置位须幂等(不抛、结果不变)。
            string basePath = MakeWriteFailingBase("writefail3");
            var svc = ServiceAt(basePath);

            Assert.DoesNotThrow(() =>
            {
                svc.SaveOnExit();
                svc.SaveOnExit();
            }, "重复置位不得抛 —— 退出路径上抛 = 卡退");

            Assert.IsTrue(SaveIncompleteFlag.IsRaised(basePath), "两次失败后仍为已置位");
        }

        [Test]
        public void test_saveOnExit_missingCompose_throwsInsteadOfRetrying()
        {
            // ⚠️ **本条钉住「两种失败分开」**:驱动源缺失是**编程错误**,不可重试。
            //    若把它卷进重试循环,配置错误会在日志里躺满整个预算然后静默变成
            //    「会话未完整」—— 真 bug 被埋掉。
            var svc = new SaveService(
                _testDir, new SaveCodec(),
                narrativeContext: () => true,
                replaySink: _ => { },
                compose: null,
                exitSaveRetryMillis: 10000,   // 预算给得很大 —— 若误入重试,本测会挂住
                // ⚠️ **刻意不传 writeScheduler**:本条要证明「抛的是 compose 那条」。
                //    若传了,`RequireWriteScheduler` 会先抛,报文里没有 "compose"
                //    ⇒ `StringAssert` 转红;若不传且 compose 检查在前,则证明成立。
                //    ⚠️ 顺序依赖是**刻意的**:驱动源检查必须在 worker 存在性之前
                //    (配置错误不该被「忘了装配 worker」掩盖)。
                writeScheduler: null);

            var ex = Assert.Throws<InvalidOperationException>(() => svc.SaveOnExit(),
                "驱动源缺失须**当场抛**,不得进重试循环");
            StringAssert.Contains("compose", ex.Message,
                "抛的必须是 compose 那条 —— 不是 writeScheduler 那条");
        }

        [Test]
        public void test_saveOnExit_missingCompose_doesNotRaiseFlag()
        {
            // 编程错误**不得**伪装成「会话未完整」—— 那会把真 bug 埋进一条
            // 「上次没存好」的用户提示里,而且永远查不出来。
            var svc = new SaveService(
                _testDir, new SaveCodec(),
                narrativeContext: () => true,
                replaySink: _ => { },
                compose: null,
                exitSaveRetryMillis: 0,
                // ⚠️ 同上:刻意不传,让 compose 那条先抛(顺序依赖是刻意的)。
                writeScheduler: null);

            Assert.Throws<InvalidOperationException>(() => svc.SaveOnExit());

            Assert.IsFalse(SaveIncompleteFlag.IsRaised(_testDir),
                "驱动源缺失 ⇒ 不得置「会话未完整」(那是磁盘故障的语义,不是配置错误的)");
        }

        [Test]
        public void test_saveOnExit_missingNarrativeContext_throwsInsteadOfRetrying()
        {
            var svc = new SaveService(
                _testDir, new SaveCodec(),
                narrativeContext: null,
                replaySink: _ => { },
                compose: _ => MinimalSaveBytes(),
                exitSaveRetryMillis: 10000,
                // ⚠️ 刻意不传 —— 要让 narrativeContext 那条先抛。
                writeScheduler: null);

            Assert.Throws<InvalidOperationException>(() => svc.SaveOnExit(),
                "narrativeContext 未注入 ⇒ 须当场抛(fail-closed,不可重试)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 「不卡退」的边界 —— 观察者自身抛异常也不得影响退出
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveOnExit_sinkThrows_stillDoesNotThrow()
        {
            string basePath = MakeWriteFailingBase("sinkthrow");

            var svc = ServiceAt(basePath,
                exitSaveFailureSink: (writeFailure, flagFailure) =>
                    throw new Exception("日志器自己坏了"));

            Assert.DoesNotThrow(() => svc.SaveOnExit(),
                "观察者抛异常也不得卡退 —— 退出路径上任何抛出都违反「不卡退」");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 置位 → 清位 的端到端往返(AC-7a-17 的完整闭环)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_flag_raisedByFailure_thenClearedByNextSuccessfulExitSave()
        {
            // 完整闭环:① 写盘坏 ⇒ 置位;② 修好 ⇒ 下一次退出保存成功 ⇒ 清位。
            // 这一条同时钉住「清位发生在成功落盘之后」这条时序纪律 —— 若清位被写成
            // 「启动即清」或「无条件清」,本测在 ① 之后就会读不到标志位。
            string basePath = Path.Combine(_testDir, "roundtrip");
            Directory.CreateDirectory(basePath);

            // ① 制造写失败:save_exit.bin 位置上先放一个同名目录
            string blocker = Path.Combine(basePath, SaveService.ExitSaveFileName);
            Directory.CreateDirectory(blocker);

            ServiceAt(basePath).SaveOnExit();
            Assert.IsTrue(SaveIncompleteFlag.IsRaised(basePath),
                "① 写盘失败 ⇒ 标志位已置");

            // ② 移除障碍(模拟「磁盘问题已恢复」)
            Directory.Delete(blocker);

            ServiceAt(basePath).SaveOnExit();
            Assert.IsFalse(SaveIncompleteFlag.IsRaised(basePath),
                "② 下一次成功落盘 ⇒ 标志位须被清(世界状态已被安全覆盖)");
        }

        [Test]
        public void test_flag_clearWritesEmptyTombstone_notDeletes()
        {
            // ⚠️ 这条钉住「清位走 WriteAtomic(写 0 字节)」这一实现选择。
            //    换成 `File.Delete` 也会让 IsRaised 读到 false —— 但会撞 AC-7a-08③ 的
            //    IL 门(Sim.Codec 内仅 WriteAtomic 可碰写盘 API)。故此处断言**文件仍在**。
            string basePath = MakeWriteFailingBase("tombstone");

            ServiceAt(basePath).SaveOnExit();
            Assert.IsTrue(SaveIncompleteFlag.IsRaised(basePath), "前提:已置位");

            // 修好障碍后成功保存
            Directory.Delete(Path.Combine(basePath, SaveService.ExitSaveFileName));
            ServiceAt(basePath).SaveOnExit();

            Assert.IsTrue(File.Exists(FlagPath(basePath)),
                "清位须留 0 字节墓碑(文件仍在)—— 走 WriteAtomic 而非 File.Delete");
            Assert.AreEqual(0, new FileInfo(FlagPath(basePath)).Length,
                "墓碑长度须为 0");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ IsSessionIncomplete —— 启动期读位的唯一入口
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_isSessionIncomplete_reflectsFlagFile()
        {
            var svc = ReadyService();
            Assert.IsFalse(svc.IsSessionIncomplete, "标志未置 ⇒ false");

            File.WriteAllBytes(FlagPath(_testDir), new byte[] { 0x01 });
            Assert.IsTrue(svc.IsSessionIncomplete,
                "标志已置 ⇒ true(且须**每次重读文件**,不得缓存)");

            File.WriteAllBytes(FlagPath(_testDir), Array.Empty<byte>());
            Assert.IsFalse(svc.IsSessionIncomplete,
                "标志已清 ⇒ false(缓存会让这里读到旧值)");
        }
    }
}
