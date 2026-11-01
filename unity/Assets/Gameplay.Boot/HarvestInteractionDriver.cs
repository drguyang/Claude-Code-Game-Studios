// 17 采集的**生产驱动** —— 3 的输入沿 → 4 的选择器 → **真闸门** → `HarvestOrchestrator.Resolve`
// (S22-9 · 2026-11-01)
//
// ── 它解的是什么 ────────────────────────────────────────────────────────────
// sprint-22 的 Exit 判据是「`CanCarry` 采前闸**在生产里被求值**」。改前实测三条通道全空:
//   `grep -rn "\.Resolve(" unity/Assets --include=*.cs | grep -v /Tests/`   → 零命中
//   `grep -rn "\.Harvest\b"  unity/Assets --include=*.cs | grep -v /Tests/`  → 零命中
// 即:`HarvestOrchestrator` 有装配点、有一整套夹具,**但没有任何东西会去调它**。
// 本类是那条调用链的第一个、也是唯一的发起方。
//
// ── 三个「为什么」──────────────────────────────────────────────────────────
// ① **为什么住 `Gameplay.Boot`**:`Sim` 的引用集恰 = {BCL, `Sim.Contracts`}(ADR-025 §① / 门 A)
//    ⇒ 它看不见 21a 的物品表、看不见 20 的库存投影、也看不见 4 的选择器。装配层是唯一交汇点。
// ② **为什么闸门必须在这里求值、不能由调用方传**:`HarvestGate` 是个裸 struct,
//    调用方传 `HarvestGate.Allow` 就能跳过采前闸且**类型面完全合法**。
//    ⇒ 本类在**类型面**上不接收 `HarvestGate` —— 它自己算。生产路径上不存在第二个能传假门的地方。
//    (另有一道机械门钉「生产码里不得出现 `HarvestGate.Allow`」,见 `Editor.Tools.Gates`。)
// ③ **为什么不缓存跨 tick 状态**:节点余量是「**已确立前缀**的纯函数」(GDD F-17-3 求值口径 ④),
//    每轮从流重扫;库存载重每轮从 20 的投影重算 ⇒ 读档后同刻结果一致。
//
// ── 明确**不**做(不得读成「已实现」)────────────────────────────────────────
// · 不判「采集动作是否合法」的动画/预表现 —— 那是表现层。
// · 不写任何事件 —— 三条产出事件 + 成长全部经 `HarvestOrchestrator`,本类只调它一次。
// · 不做模态门(`IModalState`)—— 4 的 story-005 面,本类不越界。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim;                      // CarryCapacity / CarryLoadSolver
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 驱动所需的**世界侧只读查询**面。
    /// <para>刻意拆成一个窄接口而不是直接吃 `IDataProvider`:本类只关心这五问,
    /// 多给它一个 provider 就等于把「能拿到什么」的边界交给实现随意扩大。</para>
    /// <para>⚠️ 本接口的**生产实现**必须读**版本化烘焙数据 + 已确立的流投影**,
    /// 不得读表现态位置(ADR-016 §一 三源不变量)。</para>
    /// </summary>
    public interface IHarvestWorldQuery
    {
        /// <summary>按 4 的 `BakedResourceIndex`(即 <c>Candidate.StableId</c>)取资源点。</summary>
        /// <param name="bakedResourceIndex">候选稳定 id = 资源点行序(ADR-015 逻辑层)。</param>
        /// <param name="nodeId">出参:`resource_id`(17 的节点稳定标识)。</param>
        /// <param name="cell">出参:节点所在整数格(<c>spawn_anchor</c>)。</param>
        /// <param name="itemKey">出参:品种 = 21a 物品表 ordinal。</param>
        /// <returns>存在 ⇒ <c>true</c>;索引越界 ⇒ <c>false</c>(调用方须**零动作**)。</returns>
        bool TryGetResourceNode(long bakedResourceIndex, out int nodeId, out WorldPos cell, out int itemKey);

        /// <summary>该品种的 `qty_per_node`(21a `gather_profile`)。</summary>
        int QtyPerNodeOf(int itemKey);

        /// <summary>该品种的品级分布权重(17 的形状;空 ⇒ 调用方须拒,不得静默均布)。</summary>
        IReadOnlyList<int> QualityDistributionOf(int itemKey);

        /// <summary>该品种的单件重量(21a `weight`,供 20 的 `CanCarry`)。</summary>
        int UnitWeightOf(int itemKey);

        /// <summary>该玩家**当前载重**(20 的投影求和;单位与 <see cref="UnitWeightOf"/> 同域)。</summary>
        long CurrentLoadOf(int actorId);
    }

    /// <summary>
    /// `HarvestInteractionDriver.TryHarvest` 的结果。
    /// <para>三态**互斥**:①未发起(无输入 / 无目标 / 目标不是采集点)②被闸门拒(具名类)③成功。</para>
    /// </summary>
    public readonly struct HarvestAttempt
    {
        /// <summary>是否真的发起了一次采集裁决。<c>false</c> ⇒ 其余字段无意义。</summary>
        public readonly bool Attempted;

        /// <summary>裁决结果(仅在 <see cref="Attempted"/> 为真时有效)。</summary>
        public readonly HarvestOutcome Outcome;

        private HarvestAttempt(bool attempted, in HarvestOutcome outcome)
        {
            Attempted = attempted;
            Outcome = outcome;
        }

        /// <summary>未发起(合法状态,不是错误)。</summary>
        public static HarvestAttempt None => new HarvestAttempt(false, default);

        /// <summary>发起并得到裁决。</summary>
        public static HarvestAttempt Of(in HarvestOutcome outcome)
            => new HarvestAttempt(true, in outcome);
    }

    /// <summary>
    /// 17 采集的生产驱动:每 tick 边沿把「玩家的一次主动交互」翻译成一次采集裁决。
    /// </summary>
    /// <remarks>
    /// <para><b>次序固定</b>:输入门 → 半径裁剪 → 目标选择 → 采集点判定 → 数据取路 →
    /// 两个闸门求值 → <see cref="HarvestOrchestrator.Resolve"/>。任何一步不满足都**在编译意义上**
    /// 不可能走到写入。</para>
    /// <para><b>两个闸门的求值点就在本类里</b>(<see cref="CarryLoadSolver.CanCarry"/> /
    /// <see cref="NodeRemaining.CanHarvest"/>)—— 这是 sprint-22 Exit 的可观测载体:
    /// 断掉任一侧,本类的夹具即红。</para>
    /// </remarks>
    public sealed class HarvestInteractionDriver
    {
        private readonly InteractionSelector _selector;
        private readonly InteractionRadius _radius;
        private readonly CandidateSetLoader _loader;
        private readonly HarvestOrchestrator _orchestrator;
        private readonly ForagingConstants _constants;
        private readonly CarryCapacity _capacity;
        private readonly ISkillLevelQuery _skillLevels;
        private readonly IHarvestWorldQuery _world;
        private readonly NoveltyTracker _novelty;
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly long _worldSeed;
        private readonly long _seasonIndex;
        private readonly Fix _difficulty;

        /// <summary>采集技能 id(规则五:对象 = 品种,技能 = 采集)。</summary>
        public const int HarvestSkillId = (int)SkillId.采集;

        /// <summary>构造驱动。</summary>
        /// <param name="selector">4 的目标选择器(纯函数)。</param>
        /// <param name="radius">交互半径的**单源持有者**(AC-4-17 —— 与选择器/6 共享同一实例;
        /// 本类只**应用**它做装载后裁剪,不声明第二个数值)。</param>
        /// <param name="loader">4 的候选集装载器(四源合并)。</param>
        /// <param name="orchestrator">17 的裁决编排(写事件归它,本类不写)。</param>
        /// <param name="constants">17 调参表(采量 / 品级 / 余量三条公式的输入)。</param>
        /// <param name="capacity">20 的载重上限(`CARRY_CAP` 的运行期值)。</param>
        /// <param name="skillLevels">30 的等级查询(唯一真等级来源)。</param>
        /// <param name="world">世界侧只读查询面。</param>
        /// <param name="novelty">新颖度追踪器。⚠️ **必须是 `HarvestOrchestrator` 用的**同一个实例**
        /// (键含 `skillId`,与诊断永不共键 ⇒ 共用一个安全且必需;另建 = 两处 `new NoveltyTracker(`,
        /// AC-17-14g 判据即红)。本类只调它的 `Peek`(零副作用)。</param>
        /// <param name="events">事件流只读视图(节点前缀扫描的输入)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点;扫 <c>ResourceHarvested</c> 要用)。</param>
        /// <param name="worldSeed">`WorldSeed`(品级掷骰的混盐输入)。</param>
        /// <param name="seasonIndex">`season_index(t)`。⚠️ **P0 = 0**:5 的季相运行期源尚未接线
        /// (债册登记),不是本类自造的第二真源 —— 它由装配点显式传入,改源只改那一处。</param>
        /// <param name="difficulty">本次采集的难度系数(30 的 `XP_gain` 三因子之一)。
        /// ⚠️ **P0 = 1.0**:难度分档归数值轮,这里是**具名默认值**,不是从暗处读来的数。</param>
        /// <exception cref="ArgumentNullException">任一必需依赖为 null。</exception>
        public HarvestInteractionDriver(
            InteractionSelector selector,
            InteractionRadius radius,
            CandidateSetLoader loader,
            HarvestOrchestrator orchestrator,
            ForagingConstants constants,
            CarryCapacity capacity,
            ISkillLevelQuery skillLevels,
            IHarvestWorldQuery world,
            NoveltyTracker novelty,
            Func<IReadOnlyList<SimEvent>> events,
            IPayloadDecoder decoder,
            long worldSeed,
            long seasonIndex,
            Fix difficulty)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _radius = radius ?? throw new ArgumentNullException(nameof(radius));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _constants = constants ?? throw new ArgumentNullException(nameof(constants));
            // ⚠️ `CarryCapacity` 是 **struct** ⇒ 不能 `??`。但 `default(CarryCapacity).Cap == 0`
            //    是一条真实的绕过路径(值类型经 `default` 绕开构造器,假绿形态 F9)⇒ 在这里守一次。
            if (capacity.Cap <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity.Cap,
                    "CARRY_CAP ≤ 0 —— 容量未定值(default 或损坏);满载闸会因此形同不存在" +
                    "(AC-20-13:装载期硬失败,禁默认容量兜底)");
            _capacity = capacity;
            _skillLevels = skillLevels ?? throw new ArgumentNullException(nameof(skillLevels));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _novelty = novelty ?? throw new ArgumentNullException(nameof(novelty));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _worldSeed = worldSeed;
            _seasonIndex = seasonIndex;
            _difficulty = difficulty;
        }

        /// <summary>
        /// 尝试把一次主动交互落实为一次采集。
        /// </summary>
        /// <param name="intent">本 tick 的交互意图(含玩家**经流确立的格** —— 不是表现态位置)。</param>
        /// <param name="actorId">玩家 id(20 的库存归因面)。</param>
        /// <returns>三态结果;「未发起」是**合法**返回,不是失败。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorId"/> 为负。</exception>
        public HarvestAttempt TryHarvest(in InteractIntent intent, int actorId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "actor_id 由 20 的 InventoryOf(player) 归因,不得为负");

            // ── ① 半径裁剪 + 选择器(规则七:无主动交互输入 ⇒ 不选择;走进 ≠ 交互)──
            // 裁剪发生在**装载之后、argmin 之前**(4 规则二的指定位置;story-004 裁剪半边的
            // 生产落点)。半径**数值**来自单源 `InteractionRadius`(与选择器同一实例,AC-4-17)
            // —— 本类只应用它,不声明第二个数。
            // ⚠️ 不裁的后果:E 键在任何位置都会选中**全世界**最近的药丛(装载器返回全量候选)
            //    ⇒ 采集消费掉每一次按键,查体永远轮不到。
            InteractTarget target = _selector.Select(in intent, ClipToRadius(_loader.Load(), in intent));
            if (!target.HasTarget) return HarvestAttempt.None;

            // ── ② 只有采集点才进采集链;其它种类归各自系统 ──
            if (target.Kind != InteractableKind.ForageSpot) return HarvestAttempt.None;

            // ── ③ 数据取路(品种 → 采量 / 品级分布 / 单件重量)──
            if (!_world.TryGetResourceNode(target.StableId, out int nodeId, out WorldPos cell, out int itemKey))
                return HarvestAttempt.None;

            int level = _skillLevels.QueryLevel(actorId, HarvestSkillId);
            int qtyPerNode = _world.QtyPerNodeOf(itemKey);
            IReadOnlyList<int> distribution = _world.QualityDistributionOf(itemKey);

            // 17 的形状判据归 `QualityDistributionShape`(单一真源);这里只拒「压根没有」的情况 ——
            // 缺分布时 `OutQuality` 的掷骰没有输入,静默均布会让品级变成第二真源。
            if (distribution == null || distribution.Count == 0)
                throw new InvalidOperationException(
                    $"[采集] item_key={itemKey} 无 quality_distribution —— 21a 的 gather_profile 缺数据; " +
                    "静默均布会让 out_quality 出现第二真源(S22-9 ③ 通道的意义即在此)");

            long tick = intent.Tick;

            // ── ④ 节点前缀(余量闸的输入)──
            NodeRemaining.PrefixScan scan = NodeRemaining.ScanNode(
                _events(), _decoder, nodeId, tick, _constants.RegrowWindowTicks);

            // ── ⑤ 采量 / 品级(F-17-1 / F-17-2)──
            int qty = GatherQualityRoll.HarvestQty(qtyPerNode, level, _constants);
            int outQuality = GatherQualityRoll.OutQuality(
                _worldSeed, nodeId, scan.GatherCount, distribution, _constants, level);

            // ── ⑥ **真闸门**:两侧都由本类求值,不接受外部传入 ──
            bool hasRemaining = NodeRemaining.CanHarvest(_constants, _seasonIndex, in scan, qty);
            long currentLoad = _world.CurrentLoadOf(actorId);
            bool hasCapacity = CarryLoadSolver.CanCarry(
                currentLoad, _world.UnitWeightOf(itemKey), qty, _capacity);
            var gate = new HarvestGate(hasCapacity: hasCapacity, hasRemaining: hasRemaining);

            // ── ⑦ 成长量(30 的 `XP_gain` 三因子)──
            // ⚠️ 用 `Peek` 而不是 `Query`:`Query` 会推进 `lastSeenTick`(副作用),
            //    而真正的推进发生在 `HarvestOrchestrator.Resolve` 内部的那一次 `Query` ——
            //    在这里先 `Query` 一次会让「首次」奖励被本行提前吃掉(冷却态错位)。
            //    `Peek` 零副作用且与 `Query` 同判据(同一张表),是这里唯一正确的读法。
            // ⚠️ `checked` 承 `HarvestOrchestrator` 的既有口径 —— 超界 fail-loud,不静默截断。
            NoveltyClass novelty = _novelty.Peek(
                actorId, HarvestSkillId, itemKey, checked((int)tick));
            Fix xpGain = XpGainCalculator.ComputeXpGain(HarvestSkillId, novelty, _difficulty);

            var request = new HarvestRequest(
                tick, actorId, nodeId, cell, itemKey, qty, outQuality,
                HarvestSkillId, itemKey, xpGain);

            return HarvestAttempt.Of(_orchestrator.Resolve(in request, in gate));
        }

        /// <summary>
        /// R_INTERACT 邻域裁剪:`d∞(player_cell, cell) ≤ R` 之外者出局(4 规则二的指定位置)。
        /// </summary>
        /// <remarks>全在半径内 ⇒ 返回原列表(**零分配**;交互按键不是热路径,但成功路径
        /// 每 tick 至多一次,仍不值得为常态分配)。首个出局者出现时才物化已筛副本。</remarks>
        private IReadOnlyList<Candidate> ClipToRadius(IReadOnlyList<Candidate> candidates, in InteractIntent intent)
        {
            if (candidates == null || candidates.Count == 0) return candidates;

            int r = _radius.Value;
            List<Candidate> kept = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c = candidates[i];
                if (Chebyshev(in intent.PlayerCell, in c.Cell) <= r)
                {
                    kept?.Add(c);
                }
                else if (kept == null)
                {
                    kept = new List<Candidate>(i);
                    for (int j = 0; j < i; j++) kept.Add(candidates[j]);
                }
            }
            return kept ?? candidates;
        }

        /// <summary>两格 Chebyshev 距离 <c>d∞ := max(|Δx|,|Δy|,|Δz|)</c>(与 F-4.1 第一键同式)。
        /// <para>⚠️ 先拓宽 int64 再取绝对值(int 域相减会在 <c>int.MinValue</c> 处回绕 —— 承
        /// story 002;私有副本是既有先例,<c>NeighbourhoodReporter</c> 同款各持一份)。</para></summary>
        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = Math.Abs((long)a.X - b.X);
            long dy = Math.Abs((long)a.Y - b.Y);
            long dz = Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }
    }
}
