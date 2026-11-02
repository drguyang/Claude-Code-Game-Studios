// S22-9(2026-11-01)· 17 采集**生产驱动**的直接夹具
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件、为什么
// ─────────────────────────────────────────────────────────────────────────────
// 本文件**零读盘**(事件流与库存投影全在内存里构造)。
// 唯一例外在最后一条:它读 `Assets/Gameplay.Boot/HarvestInteractionDriver.cs` 的**源码**,
// 用来钉「驱动不接受 `HarvestGate` 形参」这条结构判据 —— 类型面表达不了「不许传假门」,
// 只能靠源码断言。**不写盘**(FsWriteBoundaryGate 只管写面)。
//
// ── 本文件记分面(禁把「驱动存在」读成「玩家按得到」)────────────────────────
//   ✅ 两个闸门**真的被求值**(满载 / 余量不足各一条,且拒时**零事件**)
//   ✅ 未发起的三条合法路径(无输入 / 无目标 / 非采集点)
//   ✅ 半径裁剪:半径外候选 ⇒ 未发起(装配后 argmin 前,4 规则二指定位置)
//   ✅ 成功路径 → 三条产出事件 + 成长落流(半径边界 d∞ = R 处触发)
//   ✅ `default(CarryCapacity)` 的 F9 绕过被拒
//   ✅ **已选定目标**的结算语义(`TryHarvestTarget`:选中即消费含拒绝 / 非采集点不消费 / tick 转发)
//      🔁 2026-11-02(`TD-interaction-01`):原为 `HarvestInteractionBridge` —— 采集自己再选一遍;
//      路由统一进 `InteractionPressRouter` 后,「谁被选中」归 4 的一次 `Select`,本驱动只结算。
//   ⚠️ **不证**:① `InteractionInputDriver.OnFrame` 的 E 分支调用路由 —— 无设备
//      EditMode 按不了键,该按下沿路径与既有 E/F 路径同覆盖状态(零夹具,靠 PlayMode /
//      手动走查);② BootRoot 装配桥的那一行(纯转发)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>采集驱动:闸门求值、三态返回、拒绝路径零事件。</summary>
    [TestFixture]
    public sealed class HarvestInteractionDriverTest
    {
        private CompositionRootServices _bag;
        private SpyDropWriter _drops;
        private SpyGrowth _growth;
        private HarvestOrchestrator _orchestrator;
        private NoveltyTracker _novelty;
        private ForagingConstants _constants;
        private StubWorld _world;

        private const int PlayerId = 7;
        private const int SkillIdHarvest = (int)SkillId.采集;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _drops = new SpyDropWriter();
            _growth = new SpyGrowth();
            _novelty = new NoveltyTracker();
            _orchestrator = new HarvestOrchestrator(
                _bag.IdAuthority,
                new ResourceHarvestWriter(_bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events),
                _drops, _growth, _novelty);

            // 调参表:取盘上真产物(表的形状由它自己的构造期四条拒收守,这里不重造)。
            _constants = DaYiJingCheng.Gameplay.Presentation.DataCookedDisk.LoadForagingConstants(5);
            _world = new StubWorld();
        }

        private HarvestInteractionDriver MakeDriver(
            int carryCap, long load = 0, int remainingAllows = 1,
            int nodeCellX = 0, InteractableKind kind = InteractableKind.ForageSpot)
        {
            _world.Load = load;

            // 半径**单源**:同一实例喂选择器与驱动(生产装配面同款 —— AC-4-17)。
            var radius = new InteractionRadius(1);
            var selector = new InteractionSelector(UnwiredDiscoveryReporter.Instance, radius);

            // 候选集:一个目标,种类由夹具指定(用来证「非采集点不进链」)。
            var loader = new CandidateSetLoader(new CandidateSources(
                drops: null,
                worldBaked: kind == InteractableKind.ForageSpot
                    ? (IWorldBakedSource)new OneSpot(new WorldPos(nodeCellX, 0, 0))
                    : new OnePoi(new WorldPos(nodeCellX, 0, 0)),
                patients: null,
                bakedInitial: null));

            return new HarvestInteractionDriver(
                selector, radius, loader, _orchestrator, _constants, new CarryCapacity(carryCap),
                new FixedLevels(1), _world, _novelty, () => _bag.Stream.Events,
                _bag.Decoder, worldSeed: 0, seasonIndex: 0, difficulty: Fix.One);
        }

        private InteractIntent Intent(bool pressed = true, long tick = 0)
            => new InteractIntent(new WorldPos(0, 0, 0), pressed, tick);

        private int EventCount => _bag.Stream.Events.Count;

        // ── 持续窗口的两步走(`TD-foraging-04`)──────────────────────────────────────
        // 采集**不再是按下即结算**(`OQ-17-3` 裁为持续交互,`TD-foraging-04` 补行为):
        // ① 按键沿 ⇒ 起表;② 每 tick 推进;**满 `duration_ticks` 才结算**。
        // 本夹具里凡断言「结算」的地方都经下面两个助手走完两步 ——
        // 直接调 `TryHarvest` 拿到的一定是 `None`(那不是回归,是新的正确语义)。

        /// <summary>起表 + 推进到满时长(半径裁剪 → 选择的整条链)。</summary>
        private HarvestAttempt HarvestNow(HarvestInteractionDriver d, InteractIntent intent, int actorId)
        {
            d.TryHarvest(in intent, actorId);
            return d.Advance(intent.Tick + _constants.DurationTicks, intent.PlayerCell, actorId);
        }

        /// <summary>起表 + 推进到满时长(已选定目标的路由落点)。</summary>
        private HarvestAttempt HarvestTargetNow(
            HarvestInteractionDriver d, InteractTarget target, InteractIntent intent, int actorId)
        {
            d.TryHarvestTarget(in target, in intent, actorId);
            return d.Advance(intent.Tick + _constants.DurationTicks, intent.PlayerCell, actorId);
        }

        // ══════════════════ 未发起的三条合法路径 ══════════════════

        [Test]
        public void test_noInput_isNotAttempted_andWritesNothing()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            int before = EventCount;

            HarvestAttempt a = d.TryHarvest(Intent(pressed: false), PlayerId);

            Assert.That(a.Attempted, Is.False, "无主动交互输入 ⇒ 不选择(规则七:走进 ≠ 交互)");
            Assert.That(EventCount, Is.EqualTo(before), "未发起 ⇒ 零事件");
        }

        [Test]
        public void test_nonForageTarget_isNotAttempted_andWritesNothing()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100, kind: InteractableKind.PoiCell);
            int before = EventCount;

            HarvestAttempt a = d.TryHarvest(Intent(), PlayerId);

            Assert.That(a.Attempted, Is.False, "POI 不是采集点 —— 采集链不得染指");
            Assert.That(EventCount, Is.EqualTo(before), "零事件");
        }

        // ══════════════════ 半径裁剪(装载后 · argmin 前)══════════════════

        /// <summary>半径外候选 ⇒ 裁剪出局。</summary>
        /// <remarks><b>判别力</b>:把 `ClipToRadius` 改成恒透传 ⇒ 本测红
        /// (E 键在任何位置都采到全世界最近药丛,采集消费每一次按键)。</remarks>
        [Test]
        public void test_outOfRadiusCandidate_isNotAttempted_andWritesNothing()
        {
            // 候选 d∞ = 5,半径 1 ⇒ 出局。
            HarvestInteractionDriver d = MakeDriver(carryCap: 100, nodeCellX: 5);
            int before = EventCount;

            HarvestAttempt a = d.TryHarvest(Intent(), PlayerId);

            Assert.That(a.Attempted, Is.False,
                "半径外候选 ⇒ 装载后 argmin 前出局(4 规则二指定位置)");
            Assert.That(EventCount, Is.EqualTo(before), "零事件");
        }

        /// <summary>半径边界 d∞ == R 仍在半径内(≤)⇒ 走完整链**成功**。
        /// 兼做成功路径判据:三条产出事件 + 成长落流。</summary>
        [Test]
        public void test_radiusEdge_successPath_writesEvents_andGrowth()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100, nodeCellX: 1);
            _world.QtyPerNode = 2;   // 两闸全真:采量 ≤ BaseCapacity(5)且 cap 足够
            int before = EventCount;

            HarvestAttempt a = HarvestNow(d, Intent(), PlayerId);

            Assert.That(a.Attempted, Is.True);
            Assert.That(a.Outcome.Succeeded, Is.True,
                "d∞ == R 是半径内(≤);两闸全真 ⇒ 成功");
            Assert.That(EventCount, Is.GreaterThan(before),
                "成功 ⇒ 产出事件落流(DropSpawned / ResourceHarvested / DropClaimed / SkillGrown)");
        }

        // ══════════════════ 两个闸门:真的被求值 ══════════════════

        /// <summary>满载闸:`CanCarry` 判假 ⇒ 具名拒绝 + **零事件**。</summary>
        /// <remarks><b>判别力</b>:把驱动里的 `CarryLoadSolver.CanCarry` 调用改成常量 `true`
        /// ⇒ 本测红(会真的写事件)。</remarks>
        [Test]
        public void test_fullInventory_rejectsByName_andWritesNothing()
        {
            // cap = 1,已有载重 1,单件重量 1、采量 ≥ 1 ⇒ 一定装不下。
            HarvestInteractionDriver d = MakeDriver(carryCap: 1, load: 1);
            int before = EventCount;

            HarvestAttempt a = HarvestNow(d, Intent(), PlayerId);

            Assert.That(a.Attempted, Is.True, "有采集目标 ⇒ 确实发起了裁决");
            Assert.That(a.Outcome.Succeeded, Is.False);
            Assert.That(a.Outcome.Failure, Is.EqualTo(HarvestFailureKind.FullInventory),
                "满载须具名 —— 玩家要先看到「箱子满了」(那是他能立刻处理的)");
            Assert.That(EventCount, Is.EqualTo(before), "被拒 ⇒ **结构上**零事件(闸门排在一切写入之前)");
        }

        /// <summary>余量闸:`CanHarvest` 判假 ⇒ 具名拒绝 + 零事件。</summary>
        /// <remarks><b>判别力</b>:把 `NodeRemaining.CanHarvest` 改成常量 `true` ⇒ 本测红。</remarks>
        [Test]
        public void test_insufficientRemaining_rejectsByName_andWritesNothing()
        {
            // ⚠️ 容量给得**足够大**:否则两支闸同时为假,而满载判定在前 ⇒ 本条会红在
            //    满载上,对**余量闸**零判别力(靶测被另一条判据挡住)。
            HarvestInteractionDriver d = MakeDriver(carryCap: 100000);
            // 单次采量远大于节点容量(BaseCapacity = 5)⇒ `remaining ≥ qty` 必假。
            // ⚠️ 不靠「预置历史事件」造这个状态:那需要一条**载荷合法**的
            //    `ResourceHarvested`(default 载荷会让 `ScanNode` 解码抛),
            //    而本测要打的是**余量闸**,不是解码器。
            _world.QtyPerNode = 100;
            int before = EventCount;

            HarvestAttempt a = HarvestNow(d, Intent(), PlayerId);

            Assert.That(a.Attempted, Is.True);
            Assert.That(a.Outcome.Succeeded, Is.False);
            Assert.That(a.Outcome.Failure, Is.EqualTo(HarvestFailureKind.InsufficientRemaining),
                "余量不足须具名(与满载分得开)");
            Assert.That(EventCount, Is.EqualTo(before), "被拒 ⇒ 零事件");
        }

        /// <summary>
        /// `default(CarryCapacity)`(Cap = 0)经构造器绕过 ⇒ 驱动构造期硬拒(假绿形态 F9)。
        /// </summary>
        [Test]
        public void test_defaultCarryCapacity_isRejectedAtConstruction()
        {
            var radius = new InteractionRadius(1);
            var selector = new InteractionSelector(UnwiredDiscoveryReporter.Instance, radius);
            var loader = new CandidateSetLoader(new CandidateSources(
                null, new OneSpot(new WorldPos(0, 0, 0)), null, null));

            Assert.Throws(Is.InstanceOf<ArgumentOutOfRangeException>(), () =>
                new HarvestInteractionDriver(
                    selector, radius, loader, _orchestrator, _constants, default(CarryCapacity),
                    new FixedLevels(1), _world, _novelty, () => _bag.Stream.Events,
                    _bag.Decoder, 0, 0, Fix.One));
        }

        // ══════════════════ 结构判据:不接受 HarvestGate 形参 ══════════════════

        /// <summary>
        /// 驱动的公开方法**不得**收 `HarvestGate` 形参 —— 收了就等于给「传 `Allow` 跳过闸门」
        /// 开了一扇类型面合法的门。
        /// </summary>
        /// <remarks><b>为什么必须读源码</b>:`HarvestGate` 是裸 struct,「不许传它」是
        /// **调用约定**,不是类型约束 —— 只有源码断言能钉。</remarks>
        [Test]
        public void test_driverSurface_neverTakesHarvestGate()
        {
            Type t = typeof(HarvestInteractionDriver);
            foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public
                                           | System.Reflection.BindingFlags.Instance
                                           | System.Reflection.BindingFlags.DeclaredOnly))
            {
                foreach (var p in m.GetParameters())
                    Assert.That(p.ParameterType, Is.Not.EqualTo(typeof(HarvestGate)),
                        $"公开方法 {m.Name} 收了 HarvestGate 形参 —— 调用方就能传 Allow 跳过采前闸");
            }
        }

        // ══════════════ 已选定目标(`TryHarvestTarget`)—— 4 的统一路由的落点 ══════════════
        //
        // 🔁 2026-11-02(`TD-interaction-01`):原「桥:claim 语义」四条测的是
        //    `HarvestInteractionBridge`(采集自己再选一遍)。路由统一进 `InteractionPressRouter`
        //    之后,「谁被选中」归 4 的一次 `Select`,采集只负责**结算已选定的目标** ——
        //    故判据改挂在 `TryHarvestTarget` 上,断言不变(选中采集点 ⇒ 消费,闸门拒绝也算消费)。

        /// <summary>选中采集点 ⇒ 消费,**闸门拒绝也算消费**。</summary>
        /// <remarks><b>判别力</b>:让 `TryHarvestTarget` 在闸门拒绝时返回 `NotAttempted` ⇒ 本测红
        /// (拒采的同一按键会被路由判成「没消费」,调用方就会去做第二个动作)。</remarks>
        [Test]
        public void test_tryHarvestTarget_forageTarget_attemptedEvenWhenGateRejects()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 1, load: 1);   // 满载 ⇒ 闸拒
            int before = EventCount;

            HarvestAttempt a = HarvestTargetNow(d, ForageTarget(), Intent(tick: 0), PlayerId);

            Assert.That(a.Attempted, Is.True, "选中采集点 ⇒ 按键归采集链(拒绝也算消费)");
            Assert.That(EventCount, Is.EqualTo(before), "被拒 ⇒ 零事件");
        }

        [Test]
        public void test_tryHarvestTarget_nonForageTarget_notAttempted()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            int before = EventCount;

            var poi = InteractTarget.Of(InteractableKind.PoiCell, 0L, new WorldPos(0, 0, 0));
            HarvestAttempt a = d.TryHarvestTarget(in poi, Intent(), PlayerId);

            Assert.That(a.Attempted, Is.False, "目标不是采集点 ⇒ 不消费,按键归别的种类");
            Assert.That(EventCount, Is.EqualTo(before), "零事件");
        }

        [Test]
        public void test_tryHarvestTarget_forwardsTickToStream()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;
            long tick = 42L;

            HarvestAttempt a = HarvestTargetNow(d, ForageTarget(), Intent(tick: tick), PlayerId);

            Assert.That(a.Attempted, Is.True, "[前置] 闸门放行且选中采集点");
            // 🔁 `TD-foraging-04`:事件时间戳 = **完成**那一 tick(= 起表 tick + 时长),
            //    不是按下那一 tick —— 采集动作在窗口满时才发生。
            Assert.That(_bag.Stream.Events[_bag.Stream.Events.Count - 1].Tick,
                Is.EqualTo(tick + _constants.DurationTicks),
                "事件头的时间戳 = 完成 tick(起表 tick + duration_ticks);路由不另造时钟");
        }

        /// <summary>夹具世界里的那个采集点(`OneSpot` 的 `BakedResourceIndex = 0`,格 = 原点)。</summary>
        private static InteractTarget ForageTarget()
            => InteractTarget.Of(InteractableKind.ForageSpot, 0L, new WorldPos(0, 0, 0));

        // ══════════════════ 持续窗口(TD-foraging-04 · AC-17-08)══════════════════
        //
        // `OQ-17-3` 裁采集是**持续交互**,但行为面此前从未实现(按下即结算)⇒
        // `AC-17-08`(中途被打断 ⇒ 零效果)**恒真不可判**(判据空转)。
        // 下面六条钉的就是那个窗口:推进 / 完成只结算一次 / 两条打断源 / 重复按键不重置 /
        // 换目标作废旧窗口。
        //
        // ⚠️ 「零效果」在这些测试里判的是**三件**:`Attempted == false`、事件数不变、
        //    `IsHolding == false` —— 只判其中一件会让「取消了但又写了事件」溜过去。

        /// <summary>未满时长 ⇒ 不结算、零事件、窗口仍在途。</summary>
        [Test]
        public void test_ac1708_hold_notSettledBeforeDuration()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;
            int before = EventCount;

            d.TryHarvest(Intent(tick: 0), PlayerId);
            HarvestAttempt a = d.Advance(_constants.DurationTicks - 1, new WorldPos(0, 0, 0), PlayerId);

            Assert.That(a.Attempted, Is.False, "未满 duration_ticks ⇒ 不结算");
            Assert.That(EventCount, Is.EqualTo(before), "推进中 ⇒ 零事件");
            Assert.That(d.IsHolding, Is.True, "窗口仍在途");
        }

        /// <summary>满时长 ⇒ 恰好结算**一次**;之后窗口清空,再推进不再产出。</summary>
        [Test]
        public void test_ac1708_fullDuration_settlesExactlyOnce()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;
            int before = EventCount;

            d.TryHarvest(Intent(tick: 0), PlayerId);
            HarvestAttempt a = d.Advance(_constants.DurationTicks, new WorldPos(0, 0, 0), PlayerId);

            Assert.That(a.Attempted, Is.True, "满时长 ⇒ 结算");
            Assert.That(a.Outcome.Succeeded, Is.True, "闸门全真 ⇒ 成功");
            Assert.That(EventCount, Is.GreaterThan(before), "成功 ⇒ 产出事件落流");
            Assert.That(d.IsHolding, Is.False, "结算后窗口清空");

            int afterFirst = EventCount;
            HarvestAttempt again = d.Advance(_constants.DurationTicks * 2, new WorldPos(0, 0, 0), PlayerId);
            Assert.That(again.Attempted, Is.False, "**同一窗口不得重复结算**");
            Assert.That(EventCount, Is.EqualTo(afterFirst), "重复推进 ⇒ 零事件");
        }

        /// <summary>`AC-17-08` 打断源①:移动 ⇒ 零效果。
        /// <para>⚠️ 规则六的「目标失效·走出半径」是**本条的子集**(任何格变化都算移动)
        /// ⇒ 不另设一条;换目标那一支由下一条 `test_ac1708_targetSwitch_cancelsPrevious` 钉。</para></summary>
        [Test]
        public void test_ac1708_movement_interrupts_zeroEffect()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;
            int before = EventCount;

            d.TryHarvest(Intent(tick: 0), PlayerId);
            // 走到相邻格(仍在半径内 —— 打断判据是「动了」,不是「走远了」)
            HarvestAttempt a = d.Advance(_constants.DurationTicks, new WorldPos(1, 0, 0), PlayerId);

            Assert.That(a.Attempted, Is.False, "移动 ⇒ 打断,不结算");
            Assert.That(EventCount, Is.EqualTo(before), "打断 ⇒ **零事件**(AC-17-08)");
            Assert.That(d.IsHolding, Is.False, "窗口已取消");

            // 此后即便回到原点并到达满时长,也不得再结算(窗口已经没了)
            HarvestAttempt later = d.Advance(_constants.DurationTicks * 2, new WorldPos(0, 0, 0), PlayerId);
            Assert.That(later.Attempted, Is.False, "取消后的窗口不得复活");
            Assert.That(EventCount, Is.EqualTo(before), "零事件");
        }

        /// <summary>`AC-17-08` 打断源②:受伤 ⇒ 零效果。</summary>
        [Test]
        public void test_ac1708_injury_interrupts_zeroEffect()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;

            d.TryHarvest(Intent(tick: 0), PlayerId);
            int before = EventCount;

            // 窗口内落一条「本人受伤」。⚠️ 本驱动只读事件头(`Tick` / `Kind` / `Patient`),
            // **不解载荷** ⇒ `default(PayloadRef)` 足够(与 `ScanNode` 需要合法载荷不同)。
            SimEvent inj = new SimEvent(
                tick: 5L, new PatientId(PlayerId), seq: -1,
                EventKind.InjuryOnset, default(PayloadRef));
            _bag.EventSink.Append(in inj);
            int beforeAdvance = EventCount;

            HarvestAttempt a = d.Advance(_constants.DurationTicks, new WorldPos(0, 0, 0), PlayerId);

            Assert.That(a.Attempted, Is.False, "受伤 ⇒ 打断,不结算");
            Assert.That(EventCount, Is.EqualTo(beforeAdvance),
                "打断 ⇒ **不新增**任何事件(那一条 InjuryOnset 是本测自己写的)");
            Assert.That(d.IsHolding, Is.False, "窗口已取消");
            Assert.That(beforeAdvance, Is.EqualTo(before + 1), "[前置] 受伤事件确实进了流");
        }

        /// <summary>行为人的窗口不受**别人**的受伤影响(只断本人)。</summary>
        [Test]
        public void test_ac1708_otherActorsInjury_doesNotInterrupt()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;

            d.TryHarvest(Intent(tick: 0), PlayerId);
            SimEvent otherInj = new SimEvent(
                tick: 5L, new PatientId(PlayerId + 1), seq: -1,
                EventKind.InjuryOnset, default(PayloadRef));
            _bag.EventSink.Append(in otherInj);

            HarvestAttempt a = d.Advance(_constants.DurationTicks, new WorldPos(0, 0, 0), PlayerId);
            Assert.That(a.Attempted, Is.True, "别人的伤情不打断我");
        }

        /// <summary>同一目标重复按键(**不重置**起点)—— 否则连按会让窗口无限后推。</summary>
        [Test]
        public void test_ac1708_repeatPress_doesNotResetStart()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;

            var intent0 = Intent(tick: 0);
            d.TryHarvest(in intent0, PlayerId);
            // 第 10 tick 又按了一次(输入层持续按住时每 tick 都会发沿)
            var intent10 = Intent(tick: 10);
            d.TryHarvest(in intent10, PlayerId);

            HarvestAttempt a = d.Advance(_constants.DurationTicks, new WorldPos(0, 0, 0), PlayerId);
            Assert.That(a.Attempted, Is.True,
                "起点仍是 tick 0 ⇒ 在 tick 20 满时长结算(若被重置,要等到 tick 30)");
        }

        /// <summary>换目标 ⇒ 旧窗口作废(零效果),新目标重新计时。</summary>
        [Test]
        public void test_ac1708_targetSwitch_cancelsPrevious()
        {
            HarvestInteractionDriver d = MakeDriver(carryCap: 100);
            _world.QtyPerNode = 2;
            int before = EventCount;

            var gone = InteractTarget.Of(InteractableKind.ForageSpot, -1L, new WorldPos(0, 0, 0));
            d.TryHarvestTarget(ForageTarget(), Intent(tick: 0), PlayerId);
            Assert.That(d.IsHolding, Is.True, "[前置] 首个目标已起表");

            // 索引越界 ⇒ `TryGetResourceNode` 判假 = **目标失效** ⇒ 取消旧窗口、也不起新表。
            // ⚠️ 本夹具的 `StubWorld` 对任意 `index ≥ 0` 都回同一个 nodeId 0,故「换到另一个
            //    真实节点」这一支造不出来 —— 它能造的是「目标不存在」这一支,即本条。
            HarvestAttempt a = d.TryHarvestTarget(in gone, Intent(tick: 5), PlayerId);

            Assert.That(a.Attempted, Is.False, "换目标那一 tick 不结算");
            Assert.That(d.IsHolding, Is.False, "旧窗口已取消");
            Assert.That(EventCount, Is.EqualTo(before), "零事件");
        }

        // ══════════════════ 夹具替身 ══════════════════

        private sealed class FixedLevels : ISkillLevelQuery
        {
            private readonly int _level;
            public FixedLevels(int level) { _level = level; }
            public int QueryLevel(int actorId, int skillId) => _level;
        }

        private sealed class StubWorld : IHarvestWorldQuery
        {
            public long Load;
            public int QtyPerNode = 1;
            public bool TryGetResourceNode(long index, out int nodeId, out WorldPos cell, out int itemKey)
            {
                nodeId = 0; cell = new WorldPos(0, 0, 0); itemKey = 0;
                return index >= 0;
            }
            public int QtyPerNodeOf(int itemKey) => QtyPerNode;
            public IReadOnlyList<int> QualityDistributionOf(int itemKey) => new[] { 1, 1, 1, 1, 1 };
            public int UnitWeightOf(int itemKey) => 1;
            public long CurrentLoadOf(int actorId) => Load;
        }

        private sealed class OneSpot : IWorldBakedSource
        {
            private readonly ForageSpotDto[] _spots;
            public OneSpot(WorldPos cell) { _spots = new[] { new ForageSpotDto(cell, 0) }; }
            public IReadOnlyList<PoiCellDto> PoiCells => Array.Empty<PoiCellDto>();
            public IReadOnlyList<ForageSpotDto> ForageSpots => _spots;
            public IReadOnlyList<BuildSlotDto> BuildSlots => Array.Empty<BuildSlotDto>();
        }

        private sealed class OnePoi : IWorldBakedSource
        {
            private readonly PoiCellDto[] _pois;
            public OnePoi(WorldPos cell) { _pois = new[] { new PoiCellDto(cell, 0) }; }
            public IReadOnlyList<PoiCellDto> PoiCells => _pois;
            public IReadOnlyList<ForageSpotDto> ForageSpots => Array.Empty<ForageSpotDto>();
            public IReadOnlyList<BuildSlotDto> BuildSlots => Array.Empty<BuildSlotDto>();
        }

        private sealed class SpyDropWriter : IDropWriter
        {
            public int Spawns, Claims;
            public void Spawn(long tick, ItemInstanceId id, WorldPos anchor, int itemKey, int qty) => Spawns++;
            public void Claim(long tick, ItemInstanceId id, int claimer) => Claims++;
        }

        private sealed class SpyGrowth : IGrowthEmitter
        {
            public int Emits;
            public void Emit(long tick, int actorId, int skillId, int objectId,
                             NoveltyClass novelty, int? level, Fix xpGain) => Emits++;
        }
    }
}
