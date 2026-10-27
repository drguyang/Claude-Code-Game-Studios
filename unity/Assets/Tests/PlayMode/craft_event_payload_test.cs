// S20-13 · processing story-003 —— `Craft` 落流 / 点火事务 端到端测试(Integration,真装配,禁 Fake)
//
// 覆盖:
//   AC-18-16 ① `Craft` 落**点火 tick**(非完成 tick);
//            ② 载荷**十位齐**(registry payload_schema 逐字对位);
//            ③ `output_instance_ids` 于点火 tick 由主机经 `NextItemInstanceId()` **依产出行序**铸造;
//            ④ 全序键 = 三流键 `(Tick, StreamPriority, Patient=None, Seq)`;
//   AC-18-11   载荷含 `ActualConsumed[]` 且逐位 = F1 出参;
//   AC-18-12   点火时点容量不足 ⇒ **零 `Craft` 事件**;准入不过 / 单炉占用同理;
//   AC-18-25   以**合成夹具配方**跑上述 —— 形状义务不因真实配方内容未冻结而豁免;
//   AC-18-19   **EXTERNAL · BLOCKED-BY-21a(O-18-R3)** —— 只交反例复现夹具,**不记绿**。
//
// 权威:
//   design/gdd/processing.md 规则七(:255-277 五步事务)/ 规则八(:300-341 R-18-A 十位 +
//     发起即落流 + R-18-E 全序键)/ 规则八并发模型(:343-349 单炉)
//   design/registry/entities.yaml SimEvent.Kind.Craft(stream: world · author: 18)
//   ADR-009 §二/§三 + Amendment J · ADR-005 · ADR-006 · ADR-010 §五 · ADR-024 · ADR-029 §③
//   ADR-031(`FurnaceState` 的唯一读路径 = `IPayloadDecoder`)
//
// ─────────────────────────────────────────────────────────────────────────────
// 三条自我约束(承 S20-8 `f1_call_contract_test.cs` 同款)
// ─────────────────────────────────────────────────────────────────────────────
// ① **零字面量调参** —— 常量表一律夹具自建并标注「夹具值」,不抄真表数值。
// ② **判据不引用被测常量** —— 期望值一律是夹具输入的直接推论,不拿 `CraftEventWriter`
//    的内部量当期望。
// ③ **「零事件」类断言必须配正控** —— 只断「没写」而不证明「本来会写」,与
//    「走错路 / 没接线」逐位不可区分。每条零断言旁边都有一条同构造的**会写**用例。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(不触 FsWriteBoundaryGate)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PlayMode.Processing
{
    /// <summary>`Craft` 世界流事件 + 点火事务的端到端测试(真装配 <see cref="CompositionRoot"/>,禁 Fake)。</summary>
    [TestFixture]
    public sealed class CraftEventPayloadTest
    {
        // ══════════════════════════════════════════════════════════════════
        // 夹具(合成配方 —— AC-18-25:形状义务不因内容未冻结豁免)
        // ══════════════════════════════════════════════════════════════════

        private const long Tick = 9100L;          // 点火 tick(夹具值)
        private const int Actor = 3;              // 点火玩家(夹具值)
        private const int RecipeOrdinal = 17;     // 21a 表 ordinal(夹具值 —— 本件不查表)
        private const int DurationTicks = 40;     // 烘焙时长(夹具值,恒 ≥ 1)
        private const int InputQuality = 4;       // InQ(夹具值 ∈ [1, MAX_QUALITY])
        private const int SkillLevel = 30;        // 炮制等级(夹具值)

        private static readonly WorldPos ToolCellFixture = new WorldPos(11, 0, -4);

        private CompositionRootServices _bag;
        private CraftEventWriter _writer;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _writer = new CraftEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
        }

        [TearDown]
        public void Teardown()
        {
            _bag?.Dispose();
            _bag = null;
        }

        /// <summary>夹具配方 —— **2 输入 1 输出**的合成形状(story §QA「2 输入 1 输出」)。</summary>
        private static Recipe MakeRecipe(int durationTicks = DurationTicks)
            => new Recipe
            {
                RecipeId = "fixture_recipe_shape_only",   // 夹具值,非游戏数值
                Owner = RecipeOwner.Process,
                Inputs = new[]
                {
                    new RecipeEntry(new ItemKey("fixture_in_a", ProcessingState.Raw), 2),
                    new RecipeEntry(new ItemKey("fixture_in_b", ProcessingState.Raw), 1),
                },
                // ⚠️ **base qty = 2,不是 1** —— 刻意的(2026-10-27 证伪轮 M10 订正)。
                // F1 在该夹具下 QM = 1 + (0.15 + 0.075 + 0.2 − 0.1) = 1.325,
                // base=1 ⇒ Round(1.325) = **1**;若变异把产出量抹成 1,**前后不可区分** ⇒
                // 「OutputQty 原样搬运」这条属性就没有判别力(M10 实测逃逸)。
                // base=2 ⇒ Round(2.65) = **3** ≠ 1 ⇒ 变异必红。
                Outputs = new[] { new RecipeEntry(new ItemKey("fixture_out", ProcessingState.Extracted), 2) },
                DurationTicks = durationTicks,
                SkillGate = 5,       // 夹具值;SkillLevel = 30 ⇒ 过
                MinQuality = 2,      // 夹具值;InputQuality = 4 ⇒ 过
            };

        /// <summary>
        /// 夹具常量表 —— **夹具值,非游戏数值;数值待用户**(GDD §Tuning Knobs)。
        /// <para>⚠️ **13 参**逐位对齐 `RecipeSettlementConstants` 的真实构造函数
        /// (`Sim/ItemDatabase/RecipeSettlementConstants.cs`);漏参 = 编译失败,
        /// 参数**顺序错**则会静默跑出另一套语义 —— 故逐行标注字段名。</para>
        /// </summary>
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
                maxQuality: 5,       // MAX_QUALITY(夹具值)
                skillCap: 60);       // SKILL_CAP(夹具值;SkillLevel = 30 在域内)

        /// <summary>
        /// 常量表盒装到静态只读字段 —— <c>in</c> 形参不能取 <c>rvalue</c> 的引用
        /// (<c>in MakeConstants()</c> 编译不过)。
        /// </summary>
        private static class ConstantsHolder
        {
            public static readonly RecipeSettlementConstants Value = MakeConstants();
        }

        /// <summary>30 技能的只读查询桩 —— ⚠️ 生产侧**无** `ISkillLevelQuery` 实现
        /// (全仓 grep 零命中),故本夹具自带;它只回一个定值,不模拟曲线。</summary>
        private sealed class StubSkillLevels : ISkillLevelQuery
        {
            private readonly int _level;
            public StubSkillLevels(int level) { _level = level; }
            public int QueryLevel(int actorId, int skillId) => _level;
        }

        private static ProcessingInputs Supply(Recipe recipe, int actorId = Actor)
            => new ProcessingInputs(
                recipe,
                actorId,
                InputQuality,
                FixParse.Parse("1/5"),      // EquipMod(夹具值)
                FixParse.Parse("-3/5"),     // EnvMod_climate(夹具值,负)
                FixParse.Parse("1/2"));     // EnvMod_clinic(夹具值)

        /// <summary>造一次点火请求(合成形状)。</summary>
        private CraftIgnitionRequest Request(
            Recipe recipe = default,
            long tick = Tick,
            int actorId = Actor,
            long currentLoad = 0L,
            int unitWeight = 1)
        {
            // Recipe 是**值类型**,不存在「传 null」⇒ 用 default 作「未指定」哨兵,
            // 它的真实形态是 Inputs/Outputs 双 null(fail-closed 由 Assemble 守)。
            Recipe r = recipe.Inputs == null ? MakeRecipe() : recipe;
            return new CraftIgnitionRequest(
                Supply(r, actorId),
                tick,
                RecipeOrdinal,
                ToolCellFixture,
                new long[] { 101L, 102L },      // 被点名的输入实例(夹具值)
                new[] { InputQuality, InputQuality },
                currentLoad,
                _ => unitWeight);
        }

        /// <summary>跑一次事务(真 F1 求解器 —— 生产入口委托,AC-21a-5 同一 MethodInfo)。</summary>
        private CraftIgnitionOutcome Ignite(
            CraftIgnitionRequest request,
            int skillLevel = SkillLevel,
            int capacity = 1000)
        {
            // 调用方义务(硬):先校验常量表 —— `Settle` 不替调用方做(2026-10-27 评审 A-3)。
            ProcessingSettlementInputs.ValidateConstantTable(in ConstantsHolder.Value);

            return CraftIgnitionTransaction.Process(
                in request,
                in ConstantsHolder.Value,
                new StubSkillLevels(skillLevel),
                _bag.Stream.Events,
                _bag.Decoder,
                new CarryCapacity(capacity),
                ProcessingSettlementEntry.Solver,
                _writer);
        }

        private int WorldEventCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.World);

        private int CraftEventCount()
            => _bag.Stream.Events.Count(e => e.Kind == EventKind.Craft);

        private SimEvent LatestCraftEvent()
            => _bag.Stream.Events.Last(e => e.Kind == EventKind.Craft);

        private CraftPayload LatestCraft()
        {
            SimEvent evt = LatestCraftEvent();
            return _bag.Decoder.Decode<CraftPayload>(evt.Kind, evt.Payload);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-16 ① · 落点火 tick(非完成 tick)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1816_craftLandsOnIgnitionTick_notCompletionTick()
        {
            int before = CraftEventCount();

            CraftIgnitionOutcome outcome = Ignite(Request());
            Assert.IsTrue(outcome.Ignited, "合成夹具配方 + 足量容量 ⇒ 必须点火成功");

            Assert.AreEqual(before + 1, CraftEventCount(), "恰增 1 条 `Craft`");

            SimEvent evt = LatestCraftEvent();
            Assert.AreEqual(Tick, evt.Tick,
                "事件信封 tick = **点火 tick**(规则八「发起即落流」)—— 不是 " +
                "(Tick + DurationTicks) 那个完成时点");

            CraftPayload p = LatestCraft();
            Assert.AreEqual(Tick, p.StartTick, "载荷 start_tick = 点火 tick(与信封同值)");
            Assert.AreEqual(DurationTicks, p.DurationTicks, "载荷 duration_ticks = 配方工时");

            // 反证方向:完成 tick 上**不存在**第二条 Craft(没有「完成事件」这种 Kind)。
            Assert.IsFalse(_bag.Stream.Events.Any(e => e.Kind == EventKind.Craft && e.Tick > Tick),
                "落流后不得在完成 tick 再补一条 —— 完成是纯派生(规则八),不产生事件");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-16 ② · 载荷十位齐(registry payload_schema 逐字对位)
        // ══════════════════════════════════════════════════════════════════

        /// <remarks>
        /// ⚠️ <b>「五位」与「十位」的关系必须写清,不能各说各话</b>:
        /// story §AC-18-16② 点名的是 **5 位**(actor_id / output_instance_ids[] / tool_cell /
        /// start_tick / ActualConsumed —— R-18-A 的**补齐三点** + 两个代表位);
        /// 而 `entities.yaml` 的 `payload_schema` 是 **10 位**。本测按 **10 位全钉** ——
        /// 钉 5 位会让另外 5 位(recipe_id / duration_ticks / input_instance_ids[] /
        /// OutputQty[] / OutputQuality[])无任何断言,而它们同样是 schema 的必填位。
        /// 「至少五位齐」是 story 的措辞下限,不是本测的上限。
        /// </remarks>
        [Test]
        public void test_ac1816_payloadTenFields_allPresentAndTyped()
        {
            Ignite(Request());
            SimEvent evt = LatestCraftEvent();

            Assert.IsTrue(PayloadCodec.TryGetPayload<CraftPayload>(evt, _bag.BlobPool, out var p),
                "载荷须能经池取回(手搓伪引用会在此 false)");

            // ① actor_id —— R-18-A 补位 1(归因位)
            Assert.AreEqual(Actor, p.ActorId, "① actor_id = 点火玩家(20 的 InventoryOf(player) 折叠前提)");
            // ② recipe_id —— 21a 表 ordinal
            Assert.AreEqual(RecipeOrdinal, p.RecipeId, "② recipe_id = 21a 表 ordinal(非字符串 RecipeId)");
            // ③ start_tick
            Assert.AreEqual(Tick, p.StartTick, "③ start_tick = 点火 tick");
            // ④ duration_ticks
            Assert.AreEqual(DurationTicks, p.DurationTicks, "④ duration_ticks = 配方工时");
            // ⑤ input_instance_ids[] —— 逐位对位
            Assert.IsNotNull(p.InputInstanceIds, "⑤ input_instance_ids[] 不得为 null");
            CollectionAssert.AreEqual(new long[] { 101L, 102L }, p.InputInstanceIds,
                "⑤ input_instance_ids[] = 被点名的实例集(按输入序)");
            // ⑥ ActualConsumed[] —— AC-18-11
            Assert.IsNotNull(p.ActualConsumed, "⑥ ActualConsumed[] 不得为 null");
            Assert.AreEqual(p.InputInstanceIds.Length, p.ActualConsumed.Length,
                "⑥ 输入组等长(input_instance_ids ⟷ ActualConsumed 逐位对位)");
            // ⑦ OutputQty[]
            Assert.IsNotNull(p.OutputQty, "⑦ OutputQty[] 不得为 null");
            Assert.AreEqual(MakeRecipe().Outputs.Length, p.OutputQty.Length,
                "⑦ OutputQty[] 长度 = 产出条目数(逐条)");
            Assert.IsTrue(p.OutputQty.All(q => q >= 1),
                "⑦ 每条产出 ≥ 1(F1 结构性非零地板)");
            // ⑧ OutputQuality[]
            Assert.IsNotNull(p.OutputQuality, "⑧ OutputQuality[] 不得为 null");
            Assert.AreEqual(p.OutputQty.Length, p.OutputQuality.Length,
                "⑧ 产出组三数组等长");
            // ⑨ output_instance_ids[] —— R-18-A 补位 2(产出身份位)
            Assert.IsNotNull(p.OutputInstanceIds, "⑨ output_instance_ids[] 不得为 null");
            Assert.AreEqual(p.OutputQty.Length, p.OutputInstanceIds.Length,
                "⑨ 产出行数 = 产出实例 id 数");
            // ⑩ tool_cell —— R-18-A 补位 3(器具格)
            Assert.AreEqual(ToolCellFixture, p.ToolCell, "⑩ tool_cell = 点火时的整数格(ADR-015 WorldPos)");

            // 全整数域(ADR-006 载荷边界;`Fix` 不出现在任何位上)
            Assert.AreEqual(typeof(int), p.ActorId.GetType(), "actor_id 是 i32");
            Assert.AreEqual(typeof(long), p.StartTick.GetType(), "start_tick 是 i64");
            Assert.AreEqual(typeof(long[]), p.InputInstanceIds.GetType(), "input_instance_ids[] 是 i64[]");
            Assert.AreEqual(typeof(long[]), p.OutputInstanceIds.GetType(), "output_instance_ids[] 是 i64[]");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-16 ③ · 铸造点与铸造次序
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1816_outputInstanceIds_mintedAtIgnitionTick_inOutputLineOrder()
        {
            // 多产出夹具(2 输出)—— 单输出无法分辨「按序铸」与「随便铸」。
            Recipe twoOutputs = MakeRecipe();
            twoOutputs.Outputs = new[]
            {
                new RecipeEntry(new ItemKey("fixture_out_a", ProcessingState.Extracted), 2),
                new RecipeEntry(new ItemKey("fixture_out_b", ProcessingState.Extracted), 1),
            };

            CraftIgnitionOutcome outcome = Ignite(Request(recipe: twoOutputs));
            Assert.AreEqual(2, outcome.OutputInstanceIds.Length,
                "返回的铸造 id 数 = 产出行数(2 条)");

            CraftPayload p = LatestCraft();
            Assert.AreEqual(2, p.OutputInstanceIds.Length, "载荷里也是 2 个产出实例 id");
            Assert.AreEqual(outcome.OutputInstanceIds[0].Value, p.OutputInstanceIds[0],
                "返回的第 0 个 id = 载荷的第 0 个 id(同一铸造结果,不是两次)");
            Assert.AreEqual(outcome.OutputInstanceIds[1].Value, p.OutputInstanceIds[1],
                "返回的第 1 个 id = 载荷的第 1 个 id");

            Assert.Less(p.OutputInstanceIds[0], p.OutputInstanceIds[1],
                "铸造次序 = 产出行索引序(单调递增)—— 确定性次序是全序键可重放的必要条件");

            // 同一玩家在完成 tick 再点一次火 ⇒ id 严格更大(计数器不复位)。
            Ignite(Request(recipe: twoOutputs, tick: Tick + DurationTicks));
            CraftPayload p2 = LatestCraft();
            Assert.Less(p.OutputInstanceIds[1], p2.OutputInstanceIds[0],
                "第二次点火的 id > 第一次 —— 计数器跨炉单调(ADR-006 Amendment B 机制 A)");
        }

        /// <remarks>
        /// ⚠️ <b>本条补的是 AC-18-16③ 的**「事件间」那一半**(2026-10-27 自查补)</b>。
        /// 上一测只证「**一条** `Craft` 内部 N 个产出行按索引序铸造」;GDD `processing.md:331/743`
        /// 的措辞是「依**全序键顺序**铸造」—— 全序键是**跨事件**的量,事件内不存在「全序键序」。
        /// 两个玩家在同一 tick 各点一炉时,谁的 id 更小?
        /// <para>实现上这靠「主机串行执行 ⇒ 先调 `Next()` 的那炉先进流 ⇒ `Seq` 也先」**隐式**成立,
        /// **代码里没有任何一行显式排序**。没有夹具时,若将来有人把事务改成「先收集、后批量落流」,
        /// 事件间顺序会静默反转而**没有任何测试会红** —— 这就是本测存在的理由。</para>
        /// <para>判据用**两个独立可观测量做交叉**:id 大小序 ⟺ 全序键序。两者必须同向。</para>
        /// </remarks>
        [Test]
        public void test_ac1816_crossEvent_mintOrderFollowsFullOrderKey()
        {
            // 同一 tick、两个玩家各点一炉(单炉判定是**每玩家**的,两人互不影响)。
            Assert.IsTrue(Ignite(Request(actorId: Actor)).Ignited, "玩家 A 点火成功");
            Assert.IsTrue(Ignite(Request(actorId: Actor + 1)).Ignited, "玩家 B 同 tick 点火成功");

            var crafts = _bag.Stream.Events.Where(e => e.Kind == EventKind.Craft).ToList();
            Assert.AreEqual(2, crafts.Count, "同 tick 两条 `Craft`");

            // 按全序键定序 ⇒ 取出「先」的那条,解它的载荷。
            // (用 `EventOrder.Compare` 而非 `OrderBy(KeyOf)`:后者要求键实现 IComparable,
            //  而 `EventOrderKey` 是否实现它未在契约里承诺 —— 不赌。)
            var ordered = EventOrder.Compare(crafts[0], crafts[1]) < 0
                ? new[] { crafts[0], crafts[1] }
                : new[] { crafts[1], crafts[0] };
            CraftPayload first = _bag.Decoder.Decode<CraftPayload>(ordered[0].Kind, ordered[0].Payload);
            CraftPayload second = _bag.Decoder.Decode<CraftPayload>(ordered[1].Kind, ordered[1].Payload);

            Assert.AreEqual(Actor, first.ActorId, "全序键较小的那条是玩家 A 那炉(先调 = 先入流 = Seq 先)");
            Assert.AreEqual(Actor + 1, second.ActorId, "全序键较大的那条是玩家 B 那炉");

            Assert.Less(first.OutputInstanceIds[0], second.OutputInstanceIds[0],
                "**全序键较小的炉先铸 id** —— id 序与全序键序同向(AC-18-16③ 的事件间半边)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-16 ④ · 全序键(三流键 · Patient = None · Seq 发号)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1816_fullOrderKey_worldStream_nonePatient_seqIssued()
        {
            Ignite(Request());
            SimEvent evt = LatestCraftEvent();

            Assert.AreEqual(StreamId.World, StreamRouting.Of(evt.Kind),
                "`Craft` 落**世界流**(ADR-009 §二 · 用户裁定)");
            Assert.AreEqual(PatientId.None.Value, evt.Patient.Value,
                "全序键的 Patient 分量 = None 哨兵(-1)—— 不污染 max(patient_id) 高水位(ADR-007 §四)");
            Assert.AreEqual(0L, evt.Seq,
                "Seq 由主机 `Append` 发号(该 (Tick, Patient) 首号 = 0;写者传 -1 未发号哨兵)");

            // 无 tie 歧义:同一 tick 两条 Craft(两个玩家)的全序键必须可比且不等。
            Ignite(Request(actorId: Actor + 1));
            var crafts = _bag.Stream.Events.Where(e => e.Kind == EventKind.Craft).ToList();
            Assert.AreEqual(2, crafts.Count);
            Assert.AreNotEqual(EventOrder.KeyOf(crafts[0]).Seq, EventOrder.KeyOf(crafts[1]).Seq,
                "同 tick 同 Patient 的两条 `Craft` 靠 Seq 定序(唯一,无 tie)");
            Assert.Less(EventOrder.Compare(crafts[0], crafts[1]), 0,
                "后到的一炉在全序上严格更大 —— 次序可比");
        }

        /// <remarks>
        /// ⚠️ <b>夹具**刻意从空流起跑**,不预先造病人(2026-10-27 证伪轮 M11 订正)</b>:
        /// 若先 `SpawnNext` 一个 id = 0 的病人,高水位已 ≥ 1,而「污染值」恰也是 0 ⇒
        /// <c>max</c> 不变 ⇒ 该断言对「Patient 是不是 None」**零判别力**
        /// (M11 把 `PatientId.None` 换成 `new PatientId(0)` 后测试仍绿 —— 实测逃逸)。
        /// 空流下 max = -1:任何真实 patient_id(**含 0**)都会抬高它,
        /// 于是「高水位纹丝不动」⟺「`Craft` 的 Patient 是 None」。
        /// </remarks>
        [Test]
        public void test_ac1816_craftDoesNotPollutePatientHighWaterMark()
        {
            int before = _bag.Stream.GetNextPatientId().Value;
            Assert.AreEqual(0, before, "前提:空流 ⇒ 高水位 = 0(max = -1)");

            Assert.IsTrue(Ignite(Request()).Ignited, "点火须成功 —— 否则下面的零增量是空转");
            Assert.AreEqual(1, CraftEventCount(), "正控:确实落了一条 `Craft`");

            Assert.AreEqual(before, _bag.Stream.GetNextPatientId().Value,
                "世界流事件带 None 哨兵,不得抬高病人高水位(ADR-007 §四)");

            // 尺子校验:真正的病人事件**照样**能抬高水位 —— 证明上面的零增量
            // 不是因为这条尺子本身是死的(否则「不污染」与「压根数不到」不可区分)。
            PatientId patient = _bag.PatientSpawner.SpawnNext(diseaseId: 0, tick: Tick);
            Assert.AreEqual(patient.Value + 1, _bag.Stream.GetNextPatientId().Value,
                "病人事件仍抬高水位 —— 高水位这条尺子有效");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-11 · ActualConsumed 在载荷里,且 = F1 出参
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1811_actualConsumed_presentAndEqualsSettlementOutput()
        {
            // 同一次结算的两条路径:① 事务走完 ② 单独调 Settle 取真值 —— 逐位比对。
            Recipe recipe = MakeRecipe();
            ProcessingInputs supply = Supply(recipe);
            RecipeSettlementResult direct = ProcessingSettlementInputs.Settle(
                in supply, in ConstantsHolder.Value, new StubSkillLevels(SkillLevel),
                ProcessingSettlementEntry.Solver);

            Ignite(Request(recipe: recipe));

            CraftPayload p = LatestCraft();

            Assert.IsNotNull(p.ActualConsumed,
                "AC-18-11:载荷必须含 ActualConsumed[](21a AC-21a-52 实现侧)");
            CollectionAssert.AreEqual(direct.ActualConsumed, p.ActualConsumed,
                "载荷 ActualConsumed[] 逐位 = F1 实耗出参");
            CollectionAssert.AreEqual(direct.OutputQty, p.OutputQty,
                "载荷 OutputQty[] 逐位 = F1 产出量出参");
            Assert.AreEqual(direct.OutputQuality, p.OutputQuality[0],
                "载荷 OutputQuality[] 每位 = F1 品级出参(配方级标量 → 逐条数组的形状适配)");
        }

        [Test]
        public void test_ac1811_craftIsConsumedSideReadableByInventoryFold()
        {
            // fold 侧交叉:20 的 `InventoryFold` 读同一条 `Craft` 能拿到输入组,且**不崩**;
            // 它同时必须报 R15(载荷无产出 item_key)—— 那是**已登记缺口**,不是本件缺陷。
            Ignite(Request());
            InventoryFoldResult fold = InventoryFold.Fold(_bag.Stream.Events, _bag.Decoder);

            Assert.IsNotNull(fold, "fold 必须能跑完(读 Craft 时不崩)");
            Assert.IsTrue(fold.Gaps.Any(g => g.Kind == InventoryFoldGapKind.CraftOutputWithoutItemKey),
                "fold 对产出侧报 R15(载荷无 output item_key)—— 本测钉它是「已知且可见」," +
                "不是「不存在」");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-12 / 规则七附注 · 拒绝路径 = 零事件(每条配正控)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>正控:同构造的请求在容量足量时必须**会写** —— 否则下面的零断言是空转。</summary>
        [Test]
        public void test_ac1812_capacitySufficient_writes_positiveControlForZeroEventTests()
        {
            int before = CraftEventCount();
            CraftIgnitionOutcome outcome = Ignite(Request(currentLoad: 0L, unitWeight: 1), capacity: 1000);

            Assert.IsTrue(outcome.Ignited, "容量 1000 装少量产出 ⇒ 必须点火成功");
            Assert.AreEqual(before + 1, CraftEventCount(),
                "正控:同构造请求在容量足量时**确实会写** —— 否则下方「容量不足 ⇒ 零事件」不可信");
        }

        [Test]
        public void test_ac1812_capacityInsufficient_zeroCraftEvent()
        {
            int before = CraftEventCount();
            int beforeWorld = WorldEventCount();

            // CARRY_CAP = 5,载重已满 5 ⇒ 任何产出(≥ 1 件 × 重 1)都放不进
            // (`CanCarry` = currentLoad + candidate <= Cap,故 5 + 1 > 5 ⇒ 拒)。
            CraftIgnitionOutcome outcome = Ignite(Request(currentLoad: 5L, unitWeight: 1), capacity: 5);

            Assert.IsFalse(outcome.Ignited, "装不下 ⇒ 事务必须被拒");
            Assert.AreEqual(CraftRejection.CapacityInsufficient, outcome.Rejection,
                "拒绝理由须具名 = CapacityInsufficient(不是「不知道哪一步」)");
            Assert.AreEqual(before, CraftEventCount(),
                "规则七 :270-277:容量不足 ⇒ `Craft` **不落流**(零事件,不是「写一条拒绝事件」)");
            Assert.AreEqual(beforeWorld, WorldEventCount(), "世界流零新增(ADR-005:拒绝 = 零 Append)");
            Assert.AreEqual(0, outcome.OutputInstanceIds.Length, "被拒 ⇒ 不铸产出实例 id");
        }

        [Test]
        public void test_ac1812_admissionDenied_zeroCraftEvent()
        {
            int before = CraftEventCount();

            // 技能 0 级 < skill_gate 5 ⇒ ① 拒。F1 在 ① 之后,故零调用。
            CraftIgnitionOutcome outcome = Ignite(Request(), skillLevel: 0);

            Assert.IsFalse(outcome.Ignited, "准入不过 ⇒ 事务被拒");
            Assert.AreEqual(CraftRejection.AdmissionSkillGate, outcome.Rejection,
                "拒绝理由须具名 = AdmissionSkillGate(具名理由才能让 42 给可行动反馈)");
            Assert.AreEqual(before, CraftEventCount(), "准入不过 ⇒ 零 `Craft` 事件");
        }

        [Test]
        public void test_ac1812_minQualityGate_zeroCraftEvent()
        {
            int before = CraftEventCount();

            // 品级准入闸 = 2;把被点名实例的品级降到 1 ⇒ ① 的第二道门拒。
            var request = new CraftIgnitionRequest(
                Supply(MakeRecipe()), Tick, RecipeOrdinal, ToolCellFixture,
                new long[] { 101L, 102L }, new[] { 1, 1 }, 0L, _ => 1);

            CraftIgnitionOutcome outcome = Ignite(request);

            Assert.IsFalse(outcome.Ignited, "品级不足 ⇒ 事务被拒");
            Assert.AreEqual(CraftRejection.AdmissionMinQuality, outcome.Rejection,
                "拒绝理由须具名 = AdmissionMinQuality(与技能门区分开)");
            Assert.AreEqual(before, CraftEventCount(), "品级门拒 ⇒ 零 `Craft` 事件");
        }

        [Test]
        public void test_ac1812_furnaceBusy_zeroCraftEvent()
        {
            // 先点一次火(占用 [Tick, Tick+Duration))
            Assert.IsTrue(Ignite(Request()).Ignited, "第一炉须成功");
            int before = CraftEventCount();

            // 同一玩家、等待期内的第二个 tick ⇒ 单炉拒绝(规则八并发模型)
            CraftIgnitionOutcome outcome = Ignite(Request(tick: Tick + 1));

            Assert.IsFalse(outcome.Ignited, "等待期内第二炉 ⇒ 主机拒绝(规则八:每玩家 1 炉)");
            Assert.AreEqual(CraftRejection.FurnaceBusy, outcome.Rejection,
                "拒绝理由须具名 = FurnaceBusy");
            Assert.AreEqual(before, CraftEventCount(),
                "单炉拒绝 ⇒ 零事件(规则八:第二炉点火 ⇒ 主机拒绝、**零事件**)");
        }

        [Test]
        public void test_ac1812_completionTickCanIgniteAgain_furnaceFree()
        {
            Assert.IsTrue(Ignite(Request()).Ignited, "第一炉成功");

            // 半开区间 [start, start+duration):完成 tick 当拍已不忙(`FurnaceState.IsBusy` 口径)。
            CraftIgnitionOutcome outcome = Ignite(Request(tick: Tick + DurationTicks));

            Assert.IsTrue(outcome.Ignited,
                "完成 tick 当拍可再点火 —— 这是 `FurnaceState` 的**已实现选择**(半开区间),"
                + "不是 GDD 判据(其自陈见 `FurnaceState.cs` 的 IsBusy 注)");
            Assert.AreEqual(2, CraftEventCount(), "两炉两条 `Craft`");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-25 · 合成夹具不豁免形状(真实配方未冻结)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1825_syntheticFixture_shapesHoldWithoutRealRecipeData()
        {
            // OQ-18-4 真实配方未冻结 ⇒ 本文件全部用例跑的都是合成夹具。
            // 本测的作用 = **显式声明**「上面那些绿是夹具跑出来的」,并钉住形状不依赖内容:
            // 换一套**完全不同**的合成数值,十位与不变量照样成立。
            Recipe other = new Recipe
            {
                RecipeId = "fixture_recipe_alt_shape",
                Owner = RecipeOwner.Process,
                Inputs = new[] { new RecipeEntry(new ItemKey("fixture_in_c", ProcessingState.Raw), 9) },
                Outputs = new[]
                {
                    new RecipeEntry(new ItemKey("fixture_out_c", ProcessingState.Extracted), 4),
                    new RecipeEntry(new ItemKey("fixture_out_d", ProcessingState.Extracted), 2),
                },
                DurationTicks = 7,
                SkillGate = 0,
                MinQuality = 1,
            };

            var request = new CraftIgnitionRequest(
                Supply(other, actorId: 9),
                tick: 5L,
                recipeId: 99,
                toolCell: new WorldPos(-2, 0, 8),
                inputInstanceIds: new long[] { 9001L },
                inputQualities: new[] { 1 },
                currentCarryLoad: 0L,
                unitWeightOfOutputLine: _ => 3);

            CraftIgnitionOutcome outcome = Ignite(request, skillLevel: 1);
            Assert.IsTrue(outcome.Ignited, "另一套合成数值照样点火成功(形状 ∉ 内容)");

            CraftPayload p = LatestCraft();

            Assert.AreEqual(99, p.RecipeId, "recipe_id 跟着夹具走");
            Assert.AreEqual(7L, p.DurationTicks, "duration_ticks 跟着配方走");
            Assert.AreEqual(2, p.OutputInstanceIds.Length, "产出行数跟着配方走(2 条)");
            Assert.AreEqual(1, p.InputInstanceIds.Length, "输入实例数跟着请求走(1 条)");
            Assert.AreEqual(new WorldPos(-2, 0, 8), p.ToolCell, "tool_cell 跟着请求走(负格合法)");

            // ⚠️ 品级**不写字面量** —— 它是 F2 曲线的产物(Retain × InQ 后 Round),
            // 抄一个数进来就等于把「曲线实际算出的值」写死成期望,曲线一动测试就假红。
            // 正确判据 = **对照 F1 单独求值**(同一求解器、同一入参),逐位一致。
            // (2026-10-27 首轮实测:此处原写字面量 1,实际为 2 ⇒ 是**期望写错**,
            //  离线路算 Retain(skill=1)≈0.508 × InQ 4 = 2.03 → Round = 2,生产侧正确。)
            RecipeSettlementResult direct = ProcessingSettlementInputs.Settle(
                in request.Supply, in ConstantsHolder.Value, new StubSkillLevels(1),
                ProcessingSettlementEntry.Solver);
            Assert.AreEqual(direct.OutputQuality, p.OutputQuality[0],
                "品级逐位 = F1 单独求值的品级出参(不抄字面量)");
            CollectionAssert.AreEqual(direct.OutputQty, p.OutputQty,
                "产出量逐位 = F1 出参");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-18-19 · EXTERNAL · BLOCKED-BY-21a(O-18-R3)—— **不记绿**
        // ══════════════════════════════════════════════════════════════════

        /// <remarks>
        /// ⚠️ <b>本用例被 <see cref="IgnoreAttribute"/> 挂起,不是绿的</b>。
        /// 守恒判据 `Σ(weight × ActualConsumed) ≥ Σ(weight × produced × QM⁻¹)` 的**真修归 21a**
        /// (构建期聚合式与运行期逐条 Round 不同形);`O-18-R3` 回写未落
        /// ⇒ 本件只交**反例可复现**的夹具形状,不得记绿。
        /// <para>若有人把它反过来(删 <c>Ignore</c> 并把下面的 <c>Assert.Fail</c> 改成
        /// 「守恒成立」),那就是在 21a 未修复的前提下伪造绿 —— 该形态本项目已抓过多次。</para>
        /// </remarks>
        [Test]
        [Ignore("BLOCKED-BY-21a O-18-R3 —— 守恒律真修归 21a,本件只交反例夹具")]
        public void test_ac1819_conservationCounterExample_reproducible_notGreen()
        {
            Ignite(Request());
            CraftPayload p = LatestCraft();

            // 反例形状:两侧的**输入**都已可读,守恒判据本身归 21a 落(O-18-R3)。
            // ⚠️ 此处**刻意不做算术** —— 在 18 侧先写一版守恒式 = 制造第二实现。
            Assert.IsNotNull(p.ActualConsumed);
            Assert.IsNotNull(p.OutputQty);
            Assert.Fail("反例夹具在此:ActualConsumed / OutputQty 已可读,守恒判据归 21a");
        }
    }
}
