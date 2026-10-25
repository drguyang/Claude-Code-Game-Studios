// AC-7a-20 定时兜底半边 —— `CheckpointFallback` 行为钉子(`TD-persist-05` 动作 ④)
//
// 权威来源:
//   GDD `persistence-service.md:607-613` —— AC-7a-20(BLOCKING):
//     「若叙事事件长期缺席……由 CHECKPOINT_INTERVAL 定时兜底触发 ——
//      事件触发 = 调度策略,非第四存档入口」
//   GDD `persistence-service.md:137` —— 不变量「崩溃损失 ≤ 一个间隔」恒成立
//   GDD `persistence-service.md:635` —— OQ-7a-5(✅ 2026-10-06):默认 5 min = 18000 tick
//   裁定(2026-10-25 用户,TD-persist-04 ① 契约轮):
//     具名接口 `INarrativeContextQuery` 住 Sim.Contracts;到期不安全 ⇒ **顺延**
//     (下一 tick 重查,不重置 interval —— 跳过窗口会放宽到 2× 间隔)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件测的是**调度策略**,不是存档入口:触发的是夹具给的闭包(记录调用),
// 不经过 `SaveService`。`SaveService` 自身的门(fail-closed / 上下文值门)由
// `save_slot_contract_test` / `composition_root_test` 钉住,不在此重复。
// ─────────────────────────────────────────────────────────────────────────────

using System;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>
    /// `CheckpointFallback`:到期 / 顺延 / 清零 / fail-closed 四面钉子。
    /// </summary>
    public class CheckpointFallbackTest
    {
        private int _fires;
        private bool _inContext;
        private CheckpointFallback _timer;

        [SetUp]
        public void SetUp()
        {
            _fires = 0;
            _inContext = true;
        }

        private CheckpointFallback MakeTimer(int intervalTicks)
        {
            // 可切换瞬时源:同一实例运行时重答(瞬时量的夹具形态)
            return new CheckpointFallback(
                () => _fires++,
                new FuncNarrativeContextQuery(() => _inContext),
                intervalTicks);
        }

        /// <summary>首次触发恰在第 interval 个 tick(不是 interval−1 也不是 interval+1)。</summary>
        [Test]
        public void test_checkpointFallback_firstFire_onExactIntervalTick()
        {
            _timer = MakeTimer(3);
            _timer.OnTick();
            _timer.OnTick();
            Assert.AreEqual(0, _fires, "interval=3 时第 1、2 tick 不得触发");
            _timer.OnTick();
            Assert.AreEqual(1, _fires, "第 3 tick(计满 interval)且安全 ⇒ 恰触发一次");
            Assert.AreEqual(0, _timer.TicksSinceLastFire, "触发后计数清零");
        }

        /// <summary>
        /// **顺延语义(本类的核心裁定)**:到期时不安全 ⇒ 不触发且**不重置**;
        /// 变安全的第一个 tick 必触发 —— 「崩溃损失 ≤ 一个间隔」的夹具化。
        /// </summary>
        [Test]
        public void test_checkpointFallback_defersWhileUnsafe_firesOnFirstSafeTick()
        {
            _timer = MakeTimer(3);
            _timer.OnTick();
            _timer.OnTick();
            _inContext = false;             // 第 3 tick 起不安全
            _timer.OnTick();                // tick 3:到期但不安全 ⇒ 顺延
            Assert.AreEqual(0, _fires, "到期不安全 ⇒ 不得触发");
            _timer.OnTick();                // tick 4:仍不安全 ⇒ 顺延
            Assert.AreEqual(0, _fires);
            _inContext = true;              // tick 5:变安全
            _timer.OnTick();
            Assert.AreEqual(1, _fires, "变安全后第 1 个 tick 必触发(顺延保住『损失 ≤ 一个间隔』)");
        }

        /// <summary>顺延后从**实际触发点**重计:下一次触发恰在 fire + interval。</summary>
        [Test]
        public void test_checkpointFallback_resetsCounterFromActualFireTick()
        {
            _timer = MakeTimer(3);
            _timer.OnTick();
            _timer.OnTick();
            _inContext = false;
            _timer.OnTick();                // tick 3:顺延
            _inContext = true;
            _timer.OnTick();                // tick 4:触发
            Assert.AreEqual(1, _fires);
            _timer.OnTick();                // tick 5:重计第 1 格
            _timer.OnTick();                // tick 6:重计第 2 格
            Assert.AreEqual(1, _fires, "重计期内不得触发");
            _timer.OnTick();                // tick 7 = 4 + 3:再触发
            Assert.AreEqual(2, _fires, "下一次触发恰在 fire + interval");
        }

        /// <summary>长期不安全 ⇒ 顺延无限持续,零触发(兜底绝不制造不安全写入)。</summary>
        [Test]
        public void test_checkpointFallback_neverFires_whileNeverInContext()
        {
            _timer = MakeTimer(3);
            _inContext = false;
            for (int i = 0; i < 100; i++)
                _timer.OnTick();
            Assert.AreEqual(0, _fires, "100 tick 全不安全 ⇒ 零触发");
        }

        /// <summary>未到期 ⇒ 瞬时源**不被询问**(调用方闭包零副作用依赖)。</summary>
        [Test]
        public void test_checkpointFallback_beforeInterval_doesNotFire()
        {
            _timer = MakeTimer(CheckpointFallback.DefaultIntervalTicks);
            for (int i = 0; i < 17999; i++)
                _timer.OnTick();
            Assert.AreEqual(0, _fires, "GDD 默认间隔(18000)内不得触发");
        }

        /// <summary>
        /// fail-closed:**null 驱动源 / 非法间隔 ⇒ 构造期拒**。
        /// <para>生产 10 未落地 ⇒ 装配根不构造本类(「不构造」就是 fail-closed 的表达);
        /// 拿着 null 源还在计数的兜底 = 到期必抛的定时炸弹,不如不让造。</para>
        /// </summary>
        [Test]
        public void test_checkpointFallback_nullDrivers_orZeroInterval_ctorThrows()
        {
            Action noop = () => { };
            INarrativeContextQuery ctx = FuncNarrativeContextQuery.AlwaysIn;
            Assert.Throws<ArgumentNullException>(() => new CheckpointFallback(null, ctx, 3),
                "null 触发闭包 ⇒ 构造期拒");
            Assert.Throws<ArgumentNullException>(() => new CheckpointFallback(noop, null, 3),
                "null 瞬时源 ⇒ 构造期拒(fail-closed:不构造)");
            Assert.Throws<ArgumentOutOfRangeException>(() => new CheckpointFallback(noop, ctx, 0),
                "interval < 1 ⇒ 构造期拒(0 = 每 tick 兜底,「定时」语义不成立)");
        }

        /// <summary>GDD 默认间隔 = 18000 tick(OQ-7a-5 已裁值,本类只是承载;改动须另走裁定)。</summary>
        [Test]
        public void test_checkpointFallback_defaultInterval_isGddValue()
        {
            Assert.AreEqual(18000, CheckpointFallback.DefaultIntervalTicks,
                "CHECKPOINT_INTERVAL 默认 = 5 min = 300 s ÷ TICK_SECONDS(0.05)= 18000 tick;" +
                "改它 = GDD :136/:414/:635 三处同时失效,须另走裁定轮");
        }

        // ── OnTicks 批驱动(2026-10-25 生产接线轮:BootRoot 按 Δtick 驱动,不按帧)──

        /// <summary>批驱动与逐 tick 驱动**等价**:N tick 计 N 格,跨批不丢格。</summary>
        [Test]
        public void test_checkpointFallback_onTicks_batchEqualsLoop()
        {
            _timer = MakeTimer(5);
            _timer.OnTicks(4);                  // tick 1–4
            Assert.AreEqual(0, _fires, "未到期不得触发");
            _timer.OnTicks(1);                  // tick 5:恰触发
            Assert.AreEqual(1, _fires, "4+1 批驱动 ⇒ 第 5 tick 恰触发一次");
            Assert.AreEqual(0, _timer.TicksSinceLastFire, "触发后清零(恰在触发 tick 上观测)");

            // 跨批连推:重计 4 格后第 5 格触发 —— 批边界不丢格、不重置
            _timer.OnTicks(4);                  // tick 6–9(重计 1–4)
            Assert.AreEqual(1, _fires, "重计期内不得再触发");
            _timer.OnTicks(1);                  // tick 10 = 5 + 5
            Assert.AreEqual(2, _fires, "下一次触发恰在 fire + interval");
        }

        /// <summary>**一帧多 tick 不得被稀释成 1 格**(本方法存在的理由:按帧驱动会放宽 interval)。</summary>
        [Test]
        public void test_checkpointFallback_onTicks_multiTickFrame_countsEveryTick()
        {
            _timer = MakeTimer(3);
            _timer.OnTicks(5);                  // 一帧推进 5 tick(死亡螺旋护栏上界)
            Assert.AreEqual(1, _fires, "5 tick 一批 ⇒ 恰在第 3 tick 触发一次(不按帧计 1 格)");
            // 若被稀释成 1 格,这里会是 0;若把批当循环外再跑一次,会是 2。
        }

        /// <summary>0 = 本帧无边沿(no-op);负数 = 驱动方时钟错乱 ⇒ 构造面之外再拒一道。</summary>
        [Test]
        public void test_checkpointFallback_onTicks_zeroNoop_negativeThrows()
        {
            _timer = MakeTimer(1);
            _timer.OnTicks(0);
            Assert.AreEqual(0, _fires, "0 tick 边沿 ⇒ 零推进零触发");
            Assert.Throws<ArgumentOutOfRangeException>(() => _timer.OnTicks(-1),
                "负 tick 数 = tick 单调性被破坏 ⇒ 拒");
            Assert.AreEqual(0, _fires, "抛出的调用不得产生副作用");
        }

        /// <summary>事件触发半边成功存档 ⇒ 兜底窗口清零(TD-persist-04 裁定④ · 2026-10-25):
        /// 兜底只管「事件长期缺席」,锚点刚存过 ⇒ 窗口从现在重起。</summary>
        [Test]
        public void test_checkpointFallback_notifyExternalCheckpoint_resetsWindow()
        {
            // Arrange:interval = 3,推进 2 格(未到期)
            _timer = MakeTimer(3);
            _timer.OnTicks(2);
            Assert.AreEqual(0, _fires);

            // Act:事件触发半边刚存过档 ⇒ 通知清零
            _timer.NotifyExternalCheckpoint();

            // Assert:窗口重起 —— 再推 2 格不得触发,须再满 interval 才触发
            _timer.OnTicks(2);
            Assert.AreEqual(0, _fires, "清零后 2 格 < interval ⇒ 不得触发");
            _timer.OnTicks(1);
            Assert.AreEqual(1, _fires, "清零后第 3 格 = 满间隔,恰触发");
        }
    }
}
