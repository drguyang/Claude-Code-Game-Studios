// S20-4 前置链 · foraging story-003 —— 一次成功采集的裁决编排(17 侧)
//                                  + 三条世界流事件的形状 + 恰好一条 SkillGrown
//
// 权威来源:
//   design/registry/entities.yaml(ADR-024 唯一真源)—— 三条 Kind 的 author 逐字:
//     · ResourceHarvested  author: "17 采集(主机裁决;ADR-009 §七 Guidelines 4)"
//     · DropSpawned        author: "20 库存与物品(落地写者;触发方 ∈ {17/18/25/29})"
//     · DropClaimed        author: "20 库存与物品(主机裁决,唯一)"
//     DropSpawned 的 constraint 原文留了歧义警告:
//       「⚠️ 17 侧『主机同 tick 连发』未逐字点名 Drop* 两支写者,本登记采 ADR-009 §三/§五 +
//        Amendment J ④ 的一致口径;**实现期若 17 GDD 改口径回改本条**」
//     ⇒ **本件不改口径**:ResourceHarvested 由 17 直接写;Drop* 两支经 `IDropWriter` 注入,
//        由 20 的实现落地(当前无写者 ⇒ 注入 NullDropWriter 即「形状完备但未接线」)。
//
//   design/gdd/foraging.md 规则二(采集事实)· 规则三(id 铸造点 = DropSpawned 出生当下)·
//     规则五(成长:每次成功采集恰一条 SkillGrown,对象 = 品种)
//   story production/epics/foraging/story-003-three-events-and-identity-mint.md
//     AC-17-01(三条恰发)· AC-17-05(唯一铸造源)· AC-17-05b(SkillGrown 恰一条)·
//     R11 邻接(铸造点当下性)
//   ADR-005(主机唯一 Append)· ADR-007 §四(PatientId.None 不污染高水位)·
//   ADR-009 §二/§三/§六(世界流)· ADR-016 §一(三源不变量)·
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder)
//
// ⚠️ **本件的 AC 记分(禁区:禁把「形状已备」读成「三连发能跑」)**:
//     AC-17-05  / AC-17-05b  ✅ 真绿(17 侧自足)
//     AC-17-01               ⚠️ **不勾** —— 「恰发三条」需要 20 的两个写者落流;
//                               本件只证「17 正确地**要求了**这三条」,不证「三条真的进流」
//     R11 邻接               ⚠️ **半边** —— 同上,`DropSpawned` 的 instance_id 由 20 写入
//
// ⚠️ **原子性(可证的形态,不是自述)**:`Validate` 在**任何写入之前**核完全部**十个**输入域
//    (tick / actorId / nodeId / itemKey / qty / outQuality / skillId / objectId /
//     novelty / xpGain),与下游写者(`ResourceHarvestWriter.Record`)和 30 的
//    `SkillGrownEmitter.EmitGrowth` 的入参校验**逐项对齐** ⇒ 非法请求在发号与首次 Append
//    之前即被拒,「写了一半」对**入参错误**不可达。这也是本件不提供「分批调用 / 半途可停」API 的原因。
//
//    (2026-10-26 修 B-1:原稿只核 8 项,**漏 novelty / xpGain** —— 而这两项由 30 的
//     `EmitGrowth` 在**第 ⑤ 步**才核,彼时 id 已铸、`ResourceHarvested` 已落世界流、
//     Drop 两条已请求 ⇒ 半写是**可观测**的。修法 = **补校验**,不是挪步:
//     `SkillGrown` 必须排在三条产出事件之后,挪序会动 AC-17-05b 的语义。)

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim.Foraging
{
    /// <summary>
    /// `DropSpawned` / `DropClaimed` 的**写入面**(作者 = 20 库存与物品)。
    /// <para>⚠️ **本接口刻意不住本程序集之外**:它只是 17 需要的**写入能力**形状,
    /// 真实实现归 20(`inventory-items` epic)。17 **不实现**它 —— 一旦 Sim 侧出现
    /// 非测试实现,就是 17 越权写了别人的 Kind(见类头 entities.yaml 引文)。</para>
    /// </summary>
    public interface IDropWriter
    {
        /// <summary>物品实例出生(`DropSpawned`)。**同时是 <c>instance_id</c> 的铸造点**(规则三)。</summary>
        void Spawn(long tick, ItemInstanceId instanceId, WorldPos spawnAnchor, int itemKey, int qty);

        /// <summary>物品实例归属(`DropClaimed`)。采集成功即立即归属采集者。</summary>
        void Claim(long tick, ItemInstanceId instanceId, int claimer);
    }

    /// <summary>
    /// 20 的两个写者落地前的**空实现**:形状完备、零写入。
    /// </summary>
    /// <remarks>
    /// <para>用它装配时,17 的采集只会写 <c>ResourceHarvested</c> 一条 —— 这正是
    /// **今天**的真实状态(20 侧写者未落),而不是一个假装能跑的桩。</para>
    /// <para>⚠️ 本类**不**「先欠着以后补」:它是**显式**的空实现,`AC-17-01` 因此不勾。
    /// 20 落地后把装配点换成真实实现即可,编排代码零改动。</para>
    /// </remarks>
    public sealed class NullDropWriter : IDropWriter
    {
        /// <summary>进程内唯一实例(无状态,可安全共享)。</summary>
        public static readonly NullDropWriter Instance = new NullDropWriter();

        private NullDropWriter() { }

        /// <inheritdoc />
        public void Spawn(long tick, ItemInstanceId instanceId, WorldPos spawnAnchor, int itemKey, int qty) { }

        /// <inheritdoc />
        public void Claim(long tick, ItemInstanceId instanceId, int claimer) { }
    }

    /// <summary>
    /// 成长入口(30 的 <c>EmitGrowth</c>)—— 抽出为接口只为**可注入 spy**,便于断「恰一条」。
    /// <para>生产实现 = <see cref="SkillGrownEmitterAdapter"/>;30 的
    /// <c>SkillGrownEmitter.EmitGrowth</c> 是静态方法,需一层薄适配。</para>
    /// </summary>
    public interface IGrowthEmitter
    {
        /// <summary>发一次成长(成功采集恰一次,规则五)。无返回值 —— 「有没有发」由调用方计数判定。</summary>
        void Emit(long tick, int actorId, int skillId, int objectId,
                  NoveltyClass novelty, int? level, Fix xpGain);
    }

    /// <summary>
    /// <see cref="IGrowthEmitter"/> 的生产适配器 —— 直接转发到 30 的
    /// <see cref="SkillGrownEmitter.EmitGrowth"/> 并提交。
    /// </summary>
    public sealed class SkillGrownEmitterAdapter : IGrowthEmitter
    {
        private readonly IEventSink _sink;
        private readonly IPayloadEncoder _encoder;

        /// <summary>构造适配器。</summary>
        /// <param name="sink">事件汇(主机侧唯一 Append)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ 唯一编码路径)。</param>
        public SkillGrownEmitterAdapter(IEventSink sink, IPayloadEncoder encoder)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
        }

        /// <inheritdoc />
        public void Emit(long tick, int actorId, int skillId, int objectId,
                         NoveltyClass novelty, int? level, Fix xpGain)
        {
            var payload = SkillGrownEmitter.EmitGrowth(
                actorId, skillId, objectId, novelty, level, tick, PatientId.None, xpGain);
            var payloadRef = _encoder.Encode(EventKind.SkillGrown, payload);

            // 病史流(30 的主场);Patient = None(采集无病例语境)
            _sink.Append(new SimEvent(tick, PatientId.None, -1, EventKind.SkillGrown, payloadRef));
        }
    }

    /// <summary>一次成功采集的**输入**(全部整数域,三源不变量可满足)。</summary>
    public readonly struct HarvestRequest
    {
        /// <summary>本 tick(主机 tick,ADR-005)。</summary>
        public readonly long Tick;

        /// <summary>采集者 player_id(20 的 <c>InventoryOf(player)</c> 归因面)。</summary>
        public readonly int ActorId;

        /// <summary>资源节点稳定标识(ADR-015 逻辑层)。</summary>
        public readonly int NodeId;

        /// <summary>节点所在整数格 —— <c>DropSpawned.spawn_anchor</c>。</summary>
        public readonly WorldPos NodeCell;

        /// <summary>物种的 21a 物品表 ordinal —— <c>ResourceHarvested</c> 与 <c>DropSpawned</c> 共用。</summary>
        public readonly int ItemKeyOrdinal;

        /// <summary>单次采量(F-17-2 算好,见 <see cref="GatherQualityRoll.HarvestQty"/>)。</summary>
        public readonly int Qty;

        /// <summary>最终品级(经 QualityCap 截断,F-17-1)。</summary>
        public readonly int OutQuality;

        /// <summary>成长:技能 id(30 的 <see cref="SkillId"/> ordinal;采集 = <c>采集</c>)。</summary>
        public readonly int SkillId;

        /// <summary>成长:对象 id(对象 = **品种**,规则五)。</summary>
        public readonly int ObjectId;

        /// <summary>成长:新颖度类别(值域与加权归 30)。</summary>
        public readonly NoveltyClass Novelty;

        /// <summary>成长:本次 XP 增量(30 的 <c>XP_gain = BASE × K_difficulty × K_novelty</c> 算好后推入)。</summary>
        public readonly Fix XpGain;

        /// <summary>构造一次采集请求。</summary>
        public HarvestRequest(long tick, int actorId, int nodeId, WorldPos nodeCell,
                              int itemKeyOrdinal, int qty, int outQuality,
                              int skillId, int objectId, NoveltyClass novelty, Fix xpGain)
        {
            Tick = tick;
            ActorId = actorId;
            NodeId = nodeId;
            NodeCell = nodeCell;
            ItemKeyOrdinal = itemKeyOrdinal;
            Qty = qty;
            OutQuality = outQuality;
            SkillId = skillId;
            ObjectId = objectId;
            Novelty = novelty;
            XpGain = xpGain;
        }
    }

    /// <summary>一次成功采集的**结果**(全部整数/值类型,便于逐位比对)。</summary>
    public readonly struct HarvestResult
    {
        /// <summary>本动作铸造的 <c>instance_id</c>(唯一铸造点 = 本编排,规则三)。</summary>
        public readonly ItemInstanceId InstanceId;

        /// <summary>本动作的 <c>gather_seq</c>(该节点此前已发条数,F-17-1)。</summary>
        public readonly int GatherSeq;

        /// <summary>构造结果。</summary>
        public HarvestResult(ItemInstanceId instanceId, int gatherSeq)
        {
            InstanceId = instanceId;
            GatherSeq = gatherSeq;
        }
    }

    /// <summary>
    /// **一次成功采集的裁决编排**(17 侧,主机)。
    /// <para>五步固定次序,任何前置失败都在**首次写入之前**抛出(i.e. 不留半条事件)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>① 铸 id → ② DropSpawned → ③ ResourceHarvested → ④ DropClaimed → ⑤ SkillGrown</b></para>
    /// <para><b>② 的先于 ③ 是 story-003 明定的序</b>
    /// (`production/epics/foraging/story-003-three-events-and-identity-mint.md` Implementation Notes 1:
    /// <c>DropSpawned</c>(铸 id)→ <c>ResourceHarvested</c> → <c>DropClaimed</c>)。
    /// 世界流的 <c>Seq</c> 按 append 序发 ⇒ `ResourceHarvested.instance_id` 在其出生事件**之后**出现,
    /// 满足 20 侧 `InventoryOf` fold 的读序前提(先见出生、后见引用)。
    /// <para>⚠️ <b>② ③ ④ 的实际写入归 20</b>(<see cref="IDropWriter"/>)。本编排只**要求**它发生;
    /// 装配 <see cref="NullDropWriter"/> 时 17 只写 ① ③ ⑤ ⇒ <c>AC-17-01</c>(三条恰发)
    /// <b>不成立</b>,该 AC 因此不勾。</para>
    /// <para>⚠️ <b>本类不持有可变状态,也不持有计数器</b> —— id 一律经
    /// <see cref="IIdAuthority"/> 申请(AC-17-05:17 侧零本地铸造)。</para>
    /// </remarks>
    public sealed class HarvestOrchestrator
    {
        private readonly IIdAuthority _ids;
        private readonly ResourceHarvestWriter _harvestWriter;
        private readonly IDropWriter _dropWriter;
        private readonly IGrowthEmitter _growth;

        /// <summary>构造编排器。</summary>
        /// <param name="ids">发号权威(唯一合法 id 来源,D-21-27)。</param>
        /// <param name="harvestWriter">17 的 <c>ResourceHarvested</c> 写者。</param>
        /// <param name="dropWriter">20 的两个写者(未落时传 <see cref="NullDropWriter.Instance"/>)。</param>
        /// <param name="growth">成长入口(30)。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public HarvestOrchestrator(IIdAuthority ids, ResourceHarvestWriter harvestWriter,
                                    IDropWriter dropWriter, IGrowthEmitter growth)
        {
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _harvestWriter = harvestWriter ?? throw new ArgumentNullException(nameof(harvestWriter));
            _dropWriter = dropWriter ?? throw new ArgumentNullException(nameof(dropWriter));
            _growth = growth ?? throw new ArgumentNullException(nameof(growth));
        }

        /// <summary>
        /// 裁决一次成功采集。**五次提交次序:② → ③ → ④ → ⑤**(① 是纯发号,不写流)。
        /// </summary>
        /// <param name="request">采集请求(全部整数域)。</param>
        /// <returns>本次的 <c>instance_id</c> 与 <c>gather_seq</c>。</returns>
        /// <exception cref="ArgumentOutOfRangeException">请求字段非法
        /// (tick / actorId / nodeId / itemKey / skillId / objectId 为负,qty &lt; 1,outQuality &lt; 1,
        /// novelty 出 <c>[0, Normal]</c> 域,xpGain 为负)。</exception>
        public HarvestResult Resolve(in HarvestRequest request)
        {
            // ── 前置校验:全部在**任何写入之前**完成(原子性的结构保证)──
            Validate(request);

            // ── ① 铸 id(唯一铸造点;17 侧零本地铸造)──
            // ⚠️ 铸造点 = 「出生事件当下」的口径(R11):id 在写任何事件之前就确定,
            //    随后三条产出载荷共用同一个值 —— 不存在「先写一条、再补 id」的时序。
            ItemInstanceId instanceId = _ids.NextItemInstanceId();

            // ── ② 20 的身份出生(铸造点在语义上就在这一步,story-003 序 1)──
            // ⚠️ 刻意排在 ③ 之前:世界流 Seq 按 append 序发,读取方(20 的 InventoryOf fold)
            //    须先见出生、后见引用。原实现把 ③ 排在这里,理由写作「gather_seq 必须先算」——
            //    该理由**不成立**:gather_seq 由写者只数 ResourceHarvested 一 Kind
            //    (`ResourceHarvestWriter.CountHarvestsOfNode`),Drop* 两条不参与计数 ⇒ 与次序无关。
            _dropWriter.Spawn(request.Tick, instanceId, request.NodeCell,
                              request.ItemKeyOrdinal, request.Qty);

            // ── ③ 17 的采集事实(唯一的「本系统直接写」)──
            int gatherSeq = _harvestWriter.Record(
                request.Tick, instanceId, request.NodeId, request.Qty, request.OutQuality);

            // ── ④ 20 的立即归属 ──
            _dropWriter.Claim(request.Tick, instanceId, request.ActorId);

            // ── ⑤ 成长(恰一条,对象 = 品种;不因三条产出事件多发)──
            _growth.Emit(request.Tick, request.ActorId, request.SkillId, request.ObjectId,
                         request.Novelty, level: null, xpGain: request.XpGain);

            return new HarvestResult(instanceId, gatherSeq);
        }

        private static void Validate(in HarvestRequest r)
        {
            if (r.Tick < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.Tick, "tick 不得为负");
            if (r.ActorId < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.ActorId,
                    "actor_id 由 20 的 InventoryOf(player) 归因,不得为负");
            if (r.NodeId < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.NodeId,
                    "node_id 为 ADR-015 逻辑层节点稳定标识,不得为负");
            if (r.ItemKeyOrdinal < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.ItemKeyOrdinal,
                    "item_key 为 21a 物品表 ordinal,不得为负");
            if (r.Qty < 1)
                throw new ArgumentOutOfRangeException(nameof(r), r.Qty,
                    "qty ≥ 1(F-17-2 结构保证;0 或负 = 调用方算错)");
            if (r.OutQuality < 1)
                throw new ArgumentOutOfRangeException(nameof(r), r.OutQuality,
                    "out_quality ∈ [1, MAX_QUALITY](F-17-1)");
            if (r.SkillId < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.SkillId,
                    "skill_id 为 30 的 SkillId ordinal,不得为负");
            if (r.ObjectId < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.ObjectId,
                    "object_id 为品种 ordinal(规则五:对象 = 品种),不得为负");
            if ((int)r.Novelty < 0 || (int)r.Novelty > (int)NoveltyClass.Normal)
                throw new ArgumentOutOfRangeException(nameof(r), (int)r.Novelty,
                    "novelty_class ∈ [0, " + (int)NoveltyClass.Normal +
                    "] —— 域界由 30 定义;越界须在此拒,不得留到第 ⑤ 步 EmitGrowth 才抛" +
                    "(那时 id 已铸、ResourceHarvested 已落流)");
            if (r.XpGain.Raw < 0)
                throw new ArgumentOutOfRangeException(nameof(r), r.XpGain.Raw,
                    "xp_gain 须 ≥ 0(XP_gain 三因子均非负 ⇒ 负值 = 调用方算错)");
        }
    }
}
