// S20-13 · processing story-003 —— 写者 / 事务的**守卫面**测试(Logic · EditMode)
//
// ── 本文件与另外两份的分工(先划清,避免重复记绿)──────────────────────────────
// · `Tests/EditMode/ItemDatabase/craft_event_payload_test.cs`(story-009)已覆盖
//   **载荷形状 / 编码往返 / 全序键排序** —— 本文件**不重复**那些。
// · `Tests/PlayMode/craft_event_payload_test.cs`(本 story)覆盖 **端到端落流 + 真装配**。
// · 本文件覆盖**两份都没碰的那一面**:`CraftEventWriter` 与 `CraftIgnitionTransaction`
//   的**拒收 / fail-closed 守卫**。它们不依赖 `CompositionRoot`(本装配为纯 sim 件),
//   故落 EditMode 更快,且能把「守卫存在且会红」钉成可执行事实。
//
// ── 为什么要单独钉守卫 ──────────────────────────────────────────────────────
// `CraftEventWriter.Record` 的每个 `throw` 都是「宁可炸也不写坏载荷」的边界。它们
// **不会被端到端测试碰到**(端到端只走合法路径)。这些守卫一旦静默失效,后果是
// **坏载荷进流**(ADR-006 载荷边界破)或**玩家白烧一炉**(规则七 ⑤),两者都不可由
// 常规夹具观测到。
//
// 覆盖:
//   ① 写者入参 fail-loud(`tick` / `recipeId` / `durationTicks` / 输入组 / 品级);
//   ② 写者形状守卫(实耗与输入实例**不等长** ⇒ 抛,不静默截断);
//   ③ 事务的 null 依赖 fail-closed(**不**把缺失当成空集放行);
//   ④ 拒收路径**零铸造**:被拒时 `NextItemInstanceId()` 调用次数 = 0
//      (这是「拒绝不消耗 id」的可观测判据 —— 计数器不回滚,铸了就是空洞);
//   ⑤ 拒收路径**零 Append**:用计数型 sink 直接数写入次数(比事后查流更直接)。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘。夹具数值全部标注「夹具值」。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Processing
{
    /// <summary>`Craft` 写者与点火事务的拒收 / fail-closed 守卫。</summary>
    [TestFixture]
    public sealed class CraftIgnitionGuardsTest
    {
        // ── 夹具 ──────────────────────────────────────────────────────────
        private const long Tick = 500L;           // 夹具值
        private const int Actor = 7;              // 夹具值
        private const int RecipeOrdinal = 3;      // 夹具值
        private const int DurationTicks = 20;     // 夹具值
        private const int InputQuality = 3;       // 夹具值(∈ [1, MAX_QUALITY])
        private const int SkillLevel = 30;        // 夹具值

        private CountingSink _sink;
        private CountingIdAuthority _ids;
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private PayloadDecoder _decoder;
        private CraftEventWriter _writer;

        [SetUp]
        public void Setup()
        {
            _sink = new CountingSink();
            _ids = new CountingIdAuthority();
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _decoder = new PayloadDecoder(_pool);
            _writer = new CraftEventWriter(_sink, _encoder, _ids);
        }

        /// <summary>夹具配方(2 输入 1 输出;借 story-009 的同款形状)。</summary>
        private static Recipe MakeRecipe()
            => new Recipe
            {
                RecipeId = "fixture_recipe_guards",     // 夹具值
                Owner = RecipeOwner.Process,
                Inputs = new[]
                {
                    new RecipeEntry(new ItemKey("fixture_in_a", ProcessingState.Raw), 2),
                    new RecipeEntry(new ItemKey("fixture_in_b", ProcessingState.Raw), 1),
                },
                Outputs = new[] { new RecipeEntry(new ItemKey("fixture_out", ProcessingState.Extracted), 1) },
                DurationTicks = DurationTicks,
                SkillGate = 5,        // 夹具值
                MinQuality = 2,       // 夹具值
            };

        private static RecipeSettlementConstants MakeConstants()
            => new RecipeSettlementConstants(
                qtyMultMin: FixParse.Parse("1/2"),
                qtyMultMax: FixParse.Parse("5/2"),
                skillModCap: FixParse.Parse("3/10"),
                qualModCap: FixParse.Parse("1/10"),
                equipModCap: FixParse.Parse("1/5"),
                envModMin: FixParse.Parse("-1/2"),
                envModMax: FixParse.Parse("1/2"),
                retainMin: FixParse.Parse("1/2"),
                retainMax: FixParse.Parse("1"),
                effMin: FixParse.Parse("1/2"),
                effMax: FixParse.Parse("1"),
                maxQuality: 5,        // 夹具值
                skillCap: 60);        // 夹具值

        private static class ConstantsHolder
        {
            public static readonly RecipeSettlementConstants Value = MakeConstants();
        }

        private static ProcessingInputs Supply(Recipe recipe, int actorId = Actor)
            => new ProcessingInputs(recipe, actorId, InputQuality,
                FixParse.Parse("1/5"), FixParse.Parse("-3/5"), FixParse.Parse("1/2"));

        private sealed class StubSkillLevels : ISkillLevelQuery
        {
            private readonly int _level;
            public StubSkillLevels(int level) { _level = level; }
            public int QueryLevel(int actorId, int skillId) => _level;
        }

        private static RecipeSettlementResult SettleFixture(Recipe recipe, int skillLevel = SkillLevel)
        {
            ProcessingInputs supply = Supply(recipe);
            return ProcessingSettlementInputs.Settle(
                in supply, in ConstantsHolder.Value, new StubSkillLevels(skillLevel),
                ProcessingSettlementEntry.Solver);
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 写者入参 fail-loud
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_writer_negativeTick_throws()
        {
            RecipeSettlementResult s = SettleFixture(MakeRecipe());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _writer.Record(-1L, Actor, RecipeOrdinal, DurationTicks,
                               new long[] { 1L, 2L }, in s, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count, "抛之前不得写入(sink 零调用)");
            Assert.AreEqual(0, _ids.ItemMints, "抛之前不得铸造 id");
        }

        [Test]
        public void test_writer_negativeRecipeId_throws()
        {
            RecipeSettlementResult s = SettleFixture(MakeRecipe());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _writer.Record(Tick, Actor, -1, DurationTicks,
                               new long[] { 1L, 2L }, in s, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count);
        }

        [Test]
        public void test_writer_zeroDurationTicks_throws()
        {
            // 规则八:GDD 判 duration_ticks > 0(21a AC-21a-18 拒收 ≤ 0)。
            RecipeSettlementResult s = SettleFixture(MakeRecipe());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _writer.Record(Tick, Actor, RecipeOrdinal, 0L,
                               new long[] { 1L, 2L }, in s, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count);
        }

        [Test]
        public void test_writer_nullInputInstanceIds_throws()
        {
            RecipeSettlementResult s = SettleFixture(MakeRecipe());
            Assert.Throws<ArgumentNullException>(() =>
                _writer.Record(Tick, Actor, RecipeOrdinal, DurationTicks,
                               null, in s, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count);
        }

        [Test]
        public void test_writer_defaultSettlement_throwsInsteadOfWritingEmptyPayload()
        {
            // `default(RecipeSettlementResult)` 的三个数组是全 null。
            // 若守卫缺失,会写进一条「零实耗 / 零产出」的 `Craft` —— 玩家白烧一炉(规则七 ⑤)。
            RecipeSettlementResult empty = default;
            Assert.Throws<ArgumentNullException>(() =>
                _writer.Record(Tick, Actor, RecipeOrdinal, DurationTicks,
                               new long[] { 1L, 2L }, in empty, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count, "default 结算不得写出载荷");
            Assert.AreEqual(0, _ids.ItemMints, "更不得铸 id");
        }

        /// <remarks>
        /// ⚠️ <b>本条是 <c>consumed == null</c> 那条守卫的**唯一**判别面(2026-10-27 证伪轮 G5 订正)</b>:
        /// 上一测用 <c>default</c>,而它的**三个数组同时为 null** ⇒ 删掉 `consumed == null`
        /// 守卫后,紧接着的 `produced == null` 守卫会抛出**同类型**异常 ⇒ 断言照样满足
        /// (G5 实测逃逸)。⇒ 必须造「**只有 consumed 为 null**」的形态,让本守卫成为
        /// 唯一的拦截点。`produced` 给非空、长度与输入组对齐,其余字段合法。
        /// </remarks>
        [Test]
        public void test_writer_onlyConsumedNull_throws_isSoleGuard()
        {
            var onlyConsumedNull = new RecipeSettlementResult(
                outputQty: new[] { 1 },                    // 非 null ⇒ 第二条守卫不会先拦
                outputQuality: 3,
                efficiency: FixParse.Parse("1"),
                actualConsumed: null,                      // ← 唯一为 null 的位
                qtyMultiplier: FixParse.Parse("1"),
                envModTotal: FixParse.Parse("0"),
                sumOfModifiers: FixParse.Parse("0"));

            Assert.Throws<ArgumentNullException>(() =>
                _writer.Record(Tick, Actor, RecipeOrdinal, DurationTicks,
                               new long[] { 1L, 2L }, in onlyConsumedNull, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count, "实耗为 null ⇒ 零写入");
            Assert.AreEqual(0, _ids.ItemMints, "更不得铸 id");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 形状守卫:实耗与输入实例不等长
        // ══════════════════════════════════════════════════════════════════

        /// <remarks>
        /// ⚠️ <b>本条是「有产出行时的品级域守卫」的唯一判别面(2026-10-27 证伪轮 M3 迁移)</b>:
        /// 该守卫在 **PlayMode 面零判别力** —— F1 的 F2 曲线经 <c>clamp(·, 1, InQ)</c>
        /// 结构性保证 ≥ 1,合法路径**造不出**越界品级,所以端到端测试再怎么变异也打不到它。
        /// ⇒ 移到本文件,用 <see cref="RecipeSettlementResult"/> 的公开构造函数直接造一个
        /// <c>OutputQuality = 0</c> 的结算结果(绕过 F1),才谈得上判别力。
        /// </remarks>
        [Test]
        public void test_writer_zeroQuality_withProducedLines_throws()
        {
            // 手工造 F1 **不可能**产出的形态:有 1 条产出行,但品级 0(域是 [1, MAX_QUALITY])。
            var bad = new RecipeSettlementResult(
                outputQty: new[] { 1 },
                outputQuality: 0,
                efficiency: FixParse.Parse("1"),
                actualConsumed: new[] { 1, 1 },
                qtyMultiplier: FixParse.Parse("1"),
                envModTotal: FixParse.Parse("0"),
                sumOfModifiers: FixParse.Parse("0"));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _writer.Record(Tick, Actor, RecipeOrdinal, DurationTicks,
                               new long[] { 1L, 2L }, in bad, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count, "品级越界 ⇒ 零写入(不得让 F2 外的坏值进流)");
            Assert.AreEqual(0, _ids.ItemMints, "更不得铸 id");
        }

        [Test]
        public void test_writer_consumedLengthMismatch_throws()
        {
            // 配方 2 输入 ⇒ 实耗数组长度 2;只报 1 个输入实例 ⇒ 不等长。
            // 20 的逐实例扣减依赖逐位对位,静默截断会让「扣了谁的料」不可知。
            RecipeSettlementResult s = SettleFixture(MakeRecipe());
            Assert.AreEqual(2, s.ActualConsumed.Length, "前置:夹具配方的实耗确为 2 位");

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                _writer.Record(Tick, Actor, RecipeOrdinal, DurationTicks,
                               new long[] { 1L }, in s, new WorldPos(0, 0, 0)));
            Assert.AreEqual(0, _sink.Count, "不等长 ⇒ 零写入(不是截断后写入)");
        }

        [Test]
        public void test_writer_lengthMatch_writes_positiveControlForMismatchTest()
        {
            // 正控:同构造但长度对齐 ⇒ 必须写得进去 —— 否则上面那条抛的不是「不等长守卫」。
            RecipeSettlementResult s = SettleFixture(MakeRecipe());
            ItemInstanceId[] minted = _writer.Record(Tick, Actor, RecipeOrdinal, DurationTicks,
                           new long[] { 1L, 2L }, in s, new WorldPos(0, 0, 0));
            Assert.AreEqual(1, _sink.Count, "正控:长度对齐时确实会写");

            // ⚠️ 「铸造次数 = 产出行数」的**恰好**半边(2026-10-27 证伪轮 M7 迁移):
            // 夹具配方 1 条产出行 ⇒ 恰 1 次 NextItemInstanceId(),返回数组长 1。
            // 计数器不回滚 ⇒ 多铸一次 = 一个**永久空洞**,且从载荷与返回值上都看不出来
            // (数组长度照旧)—— 只有直接数 `ItemMints` 才观测得到。
            Assert.AreEqual(1, _ids.ItemMints, "1 条产出行 ⇒ 恰 1 次铸造(不是「至少 1 次」)");
            Assert.AreEqual(1, minted.Length, "返回的 id 数 = 产出行数");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 事务依赖 fail-closed
        // ══════════════════════════════════════════════════════════════════

        private CraftIgnitionRequest Request(Recipe recipe = default, int actorId = Actor)
        {
            Recipe r = recipe.Inputs == null ? MakeRecipe() : recipe;
            return new CraftIgnitionRequest(
                Supply(r, actorId), Tick, RecipeOrdinal, new WorldPos(2, 0, 5),
                new long[] { 1L, 2L }, new[] { InputQuality, InputQuality }, 0L, _ => 1);
        }

        private CraftIgnitionOutcome Run(
            CraftIgnitionRequest request,
            int capacity = 1000,
            int skillLevel = SkillLevel,
            RecipeSettlementFn solver = null)
            => CraftIgnitionTransaction.Process(
                in request, in ConstantsHolder.Value,
                new StubSkillLevels(skillLevel),
                _sink.Events, _decoder,
                new CarryCapacity(capacity),
                solver ?? ProcessingSettlementEntry.Solver, _writer);

        [Test]
        public void test_transaction_nullWriter_throwsInsteadOfSilentlySkippingAppend()
        {
            CraftIgnitionRequest request = Request();
            Assert.Throws<ArgumentNullException>(() =>
                CraftIgnitionTransaction.Process(
                    in request, in ConstantsHolder.Value, new StubSkillLevels(SkillLevel),
                    _sink.Events, _decoder, new CarryCapacity(1000),
                    ProcessingSettlementEntry.Solver, null));
            Assert.AreEqual(0, _sink.Count, "无写者 ⇒ 零 Append(不是「跳过落流继续」)");
        }

        /// <remarks>
        /// ⚠️ <b>判据是「入口即拒」,不是「终归会抛」(2026-10-27 证伪轮 G6 订正)</b>:
        /// 只断 <c>ArgumentNullException</c> 时该变异**逃逸** —— 删掉本层守卫后,
        /// null 会一路传到 <c>AdmissiblePredicate:115</c>,那里抛的是**同类型**异常
        /// ⇒ 断言照样满足。⇒ 判据改成**副作用**:抛之前求解器**一次都没被调**。
        /// 这才是「在入口拒」与「跑到第 ① 步才炸」的可观测差别。
        /// </remarks>
        [Test]
        public void test_transaction_nullInputInstanceIds_throwsAtEntry_beforeAnyWork()
        {
            var solver = new CountingSolver();
            var request = new CraftIgnitionRequest(
                Supply(MakeRecipe()), Tick, RecipeOrdinal, new WorldPos(2, 0, 5),
                null, new[] { InputQuality, InputQuality }, 0L, _ => 1);

            var ex = Assert.Throws<ArgumentNullException>(() => Run(request, solver: solver.Solve));
            Assert.AreEqual("request.InputInstanceIds", ex.ParamName,
                "必须是事务层这条守卫拒的(字面量,不随生产侧改名而漂移)");
            Assert.AreEqual(0, solver.Calls, "入口即拒 ⇒ 求解器零调用(不是「跑到第 ③ 步才炸」)");
            Assert.AreEqual(0, _sink.Count);
            Assert.AreEqual(0, _ids.ItemMints);
        }

        /// <remarks>
        /// ⚠️ <b>同样改成副作用判据(2026-10-27 证伪轮 G7 订正)</b>。
        /// 「不知道点了哪些料」与「一个料都没点」必须可分 —— 后者是合法输入(会走
        /// <c>NoConsumedInstances</c> 拒),前者是编程错误(必须**在入口**抛)。
        /// 只断异常类型时分不清这两条路径(下游 `AdmissiblePredicate` 抛同类型异常)。
        /// </remarks>
        [Test]
        public void test_transaction_nullInputQualities_throwsAtEntry_notTreatedAsEmptySet()
        {
            var solver = new CountingSolver();
            var request = new CraftIgnitionRequest(
                Supply(MakeRecipe()), Tick, RecipeOrdinal, new WorldPos(2, 0, 5),
                new long[] { 1L, 2L }, null, 0L, _ => 1);

            // ⚠️ **只断异常类型 + `Calls == 0` 也不够**(2026-10-27 证伪轮 G7 实测):
            // `AdmissiblePredicate.Judge:115` 对同一个 null 抛的是**同类型** `ArgumentNullException`,
            // 而它位于 ① 准入、同样在 F1 **之前** ⇒ `Calls == 0` 照样成立。
            // 唯一可分辨的证据 = `ParamName`:事务抛的是**自己的字段路径**,
            // 准入抛的是它的形参名 `consumedQualities`。
            // (写字面量而不用 `nameof(...)` —— 用 `nameof` 会让夹具随生产侧一起改名,零判别力。)
            var ex = Assert.Throws<ArgumentNullException>(() => Run(request, solver: solver.Solve));
            Assert.AreEqual("request.InputQualities", ex.ParamName,
                "必须是**事务层**这条守卫拒的 —— 准入层的同名守卫抛的是 `consumedQualities`");
            Assert.AreEqual(0, solver.Calls, "入口即拒 ⇒ 求解器零调用");
            Assert.AreEqual(0, _sink.Count);

            // 对照:真正「一个料都没点」是**合法输入**,走的是具名拒收而非抛。
            // (空集在 `AdmissiblePredicate` 里返回 `NoConsumedInstances`。)
            var emptySet = new CraftIgnitionRequest(
                Supply(MakeRecipe()), Tick, RecipeOrdinal, new WorldPos(2, 0, 5),
                Array.Empty<long>(), Array.Empty<int>(), 0L, _ => 1);
            CraftIgnitionOutcome rejected = Run(emptySet);
            Assert.IsFalse(rejected.Ignited, "空集 ≠ 编程错误:它是具名拒收");
            Assert.AreEqual(0, _sink.Count, "空集同样零 Append(但理由是业务判定,不是异常)");
        }

        [Test]
        public void test_transaction_nullUnitWeightDelegate_throws()
        {
            var request = new CraftIgnitionRequest(
                Supply(MakeRecipe()), Tick, RecipeOrdinal, new WorldPos(2, 0, 5),
                new long[] { 1L, 2L }, new[] { InputQuality, InputQuality }, 0L, null);

            Assert.Throws<ArgumentNullException>(() => Run(request));
            Assert.AreEqual(0, _sink.Count);
        }

        // ══════════════════════════════════════════════════════════════════
        // ④⑤ 拒收路径 = 零铸造 + 零 Append
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 三个拒收理由(准入 / 单炉 / 容量)各跑一遍,统一断言「零 Append + 零铸造」。
        /// <para>⚠️ <b>不消耗 id</b> 是本文件独有的可观测面:计数器**不回滚**
        /// (`IIdAuthority` 只有 `TryRollbackLastPatientId`,**没有**物品 id 的回滚),
        /// 故「被拒时铸了 id」会留下永久空洞 —— 这是端到端测试看不见的。</para>
        /// </summary>
        [Test]
        public void test_rejectedPaths_zeroAppendAndZeroIdMint()
        {
            // ── 正控:先证明这条路径**本来会写**、**本来会铸**。
            CraftIgnitionOutcome ok = Run(Request());
            Assert.IsTrue(ok.Ignited, "正控:合法请求必须点火");
            Assert.AreEqual(1, _sink.Count, "正控:合法请求确实写了一条");
            Assert.AreEqual(1, _ids.ItemMints, "正控:合法请求确实铸了 1 个产出 id");

            int sinkAfterOk = _sink.Count;
            int mintsAfterOk = _ids.ItemMints;

            // ── ① 准入不过(技能 0 < skill_gate 5)
            CraftIgnitionOutcome denied = Run(Request(), skillLevel: 0);
            Assert.IsFalse(denied.Ignited);
            Assert.AreEqual(sinkAfterOk, _sink.Count, "准入拒 ⇒ 零 Append");
            Assert.AreEqual(mintsAfterOk, _ids.ItemMints, "准入拒 ⇒ 零铸造");

            // ── ② 单炉占用(同玩家在等待期内再点)
            CraftIgnitionOutcome busy = Run(Rebuild(tick: Tick + 1));
            Assert.IsFalse(busy.Ignited);
            Assert.AreEqual(sinkAfterOk, _sink.Count, "单炉拒 ⇒ 零 Append");
            Assert.AreEqual(mintsAfterOk, _ids.ItemMints, "单炉拒 ⇒ 零铸造");

            // ── ③ 容量不足(cap 5、载重 5)
            CraftIgnitionOutcome noRoom = Run(Rebuild(currentLoad: 5L), capacity: 5);
            Assert.IsFalse(noRoom.Ignited);
            Assert.AreEqual(sinkAfterOk, _sink.Count, "容量拒 ⇒ 零 Append");
            Assert.AreEqual(mintsAfterOk, _ids.ItemMints, "容量拒 ⇒ 零铸造(不铸空号)");
        }

        [Test]
        public void test_zeroOutputRecipe_mintsNoIds()
        {
            // 21a 的烘焙期非空约束会拒空产出组,但**写入侧不得铸空号** ——
            // 若这里铸了,每个空产出配方都会白吃一个 id。
            Recipe noOutputs = MakeRecipe();
            noOutputs.Outputs = Array.Empty<RecipeEntry>();

            CraftIgnitionOutcome outcome = Run(Request(recipe: noOutputs));

            Assert.IsTrue(outcome.Ignited, "空产出组在本层是可通过的(拒收归 21a 烘焙期)");
            Assert.AreEqual(0, outcome.OutputInstanceIds.Length, "零产出行 ⇒ 零个 id 返回");
            Assert.AreEqual(0, _ids.ItemMints, "零产出行 ⇒ `NextItemInstanceId()` 零调用");
        }

        /// <summary>按参数重建请求(供「同一玩家、不同 tick / 载重」的拒收用例)。</summary>
        private CraftIgnitionRequest Rebuild(long tick = Tick, long currentLoad = 0L, int actorId = Actor)
            => new CraftIgnitionRequest(
                Supply(MakeRecipe(), actorId), tick, RecipeOrdinal, new WorldPos(2, 0, 5),
                new long[] { 1L, 2L }, new[] { InputQuality, InputQuality },
                currentLoad, _ => 1);

        // ══════════════════════════════════════════════════════════════════
        // 桩(计数型 —— 让「零调用」可观测)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>计数型 sink —— 直接数 `Append` 次数,比事后查流更贴近「有没有发生过」。</summary>
        private sealed class CountingSink : IEventSink
        {
            private readonly List<SimEvent> _events = new List<SimEvent>();
            public int Count => _events.Count;
            public IReadOnlyList<SimEvent> Events => _events;

            public void Append(in SimEvent e)
            {
                // 简易发号:每 (Tick, Patient) 组内递增(够本文件用 —— 判据不看 Seq)。
                long seq = 0;
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    if (_events[i].Tick == e.Tick && _events[i].Patient.Value == e.Patient.Value)
                    { seq = _events[i].Seq + 1; break; }
                }
                _events.Add(new SimEvent(e.Tick, e.Patient, e.Seq < 0 ? seq : e.Seq, e.Kind, e.Payload));
            }
        }

        /// <summary>计数型发号器 —— `ItemMints` 数是「铸了几个 id」的直接观测。</summary>
        private sealed class CountingIdAuthority : IIdAuthority
        {
            private int _patients;
            private int _items;
            public int ItemMints => _items;

            public PatientId NextPatientId() => new PatientId(_patients++);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(_items++);
        }

        /// <summary>
        /// 计数型求解器桩 —— <see cref="Calls"/> 是「事务跑到第 ③ 步没有」的直接观测。
        /// <para>⚠️ <b>为什么需要它</b>(2026-10-27 证伪轮 G6/G7 逃逸的直接教训):
        /// 「入口即拒」这条属性**不可**由异常类型证明 —— 删掉入口守卫后,null 会一路传到
        /// <c>AdmissiblePredicate:115</c> 并抛出**同类型**的 <c>ArgumentNullException</c>。
        /// 只有「**抛之前求解器一次都没被调**」才把「入口拒」与「跑到中途才炸」分开。</para>
        /// </summary>
        private sealed class CountingSolver
        {
            private int _calls;
            public int Calls => _calls;

            public RecipeSettlementResult Solve(
                in RecipeSettlementRequest request, in RecipeSettlementConstants constants)
            {
                _calls++;
                return default;   // 到不了这里才算通过;真跑到了也立刻由 Calls 抓
            }
        }
    }
}
