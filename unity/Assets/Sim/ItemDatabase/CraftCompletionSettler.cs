// `TD-processing-08` · 2026-11-03 —— 20 侧的**完成时点结算入口**(R-18-D[甲]② 起货溢出)
//
// 权威来源:
//   design/gdd/processing.md
//     · R-18-D(:279-298)起货时点容量溢出 —— 「溢出物的处置 = 完成 tick 当场在本格发
//       `DropSpawned`(归属 = 20 依 `Craft` 事件派生,18 不写世界流)」;
//       「**复用既有 Kind**」·「**18 不新增任何 `Kind`、不写 `DropSpawned`**」·
//       「溢出量的选栈归 20 的确定性选栈」·「回 20 的登记义务 `O-18-R4`」
//     · 规则八(:308)「完成是纯派生 = 时间到达 + 出参已定」·「产出入库不另发事件」
//     · AC-18-21 的溢出半边
//   design/gdd/inventory-and-items.md 规则六(整件拒绝的原子单位 = 「炮制 = 一次结算」)
//   ADR-005(主机唯一 Append / 发号)· ADR-009 §二/§三(世界流)· ADR-006(载荷 ⊆ 整数域)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts})
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么本类必须存在(修的是什么)
// ─────────────────────────────────────────────────────────────────────────────
// 2026-11-03 之前,`InventoryFold.ApplyCraftOutputs` 在**折叠 `Craft` 事件时**就把产出
// 建进炮制者名下 —— 与 `duration_ticks` 无关。⇒ 产出在**点火那刻**就「入包」:
//   ① 玩家可在等待期内使用未出炉的成品;
//   ② `CarryLoad` 提前计入产出 ⇒ 「起货瞬间容量不足」在投影里**不可见**;
//   ③ R-18-D 的溢出路径**没有宾语** —— 因为没有「入包失败」这个时刻。
//
// 本批把两件事分开:
//   · **可见性**归 fold(`InventoryFoldContext.AsOfTick`,`Craft` 产出只在完成 tick 后可见);
//   · **容量裁决 + 落地**归本类(完成 tick 那一拍)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 「整批落地」而不是「溢出多少落多少」
// ─────────────────────────────────────────────────────────────────────────────
// 20 规则六的原子单位表把「炮制」定成**一次结算**(一次 `Craft` = 一个原子单位),
// 与拾取的「整件拒绝」同纪律:装不下就**这批成品整体在地上**,不做「一半入包一半落地」。
// ⇒ 判据是一句话:`baseLoad + 本批总重 > cap ⇒ 本批全部落地`。
//
// ⚠️ GDD 里「溢出量的选栈归 20 的确定性选栈」这句在**本裁**下退化 —— 因为不存在
//    「部分溢出」这个量了。若产品改为部分容纳,改本类一处判据即可(见 `SettleOne`)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 与 18 的分工(不重写判据)
// ─────────────────────────────────────────────────────────────────────────────
// 「本 tick 哪些炉到点」**只有一份判据** = `Sim.Processing.FurnaceCompletionSchedule.CollectAt`
// (18 交付,单测已钉)。本类**调它**,不另写 `start + duration == tick` —— 两份判据迟早分叉。
// 本类只加 20 自己的那一半:容量裁决 + `DropSpawned` 写入。
//
// `FurnaceCompletion` 只携带 `OutputInstanceIds`(身份)· `ToolCell`(锚点)· `ActorId`,
// **不含产出量与品级** ⇒ 本类按全序键回查那条 `Craft` 事件再解一次载荷。这次额外解码是
// 「不把 18 的形状改成 20 的形状」的代价,且只在**完成那一拍**发生(极低频)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Processing;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim
{
    /// <summary>完成时点结算 —— R-18-D[甲]② 的**唯一生产落地点**(`TD-processing-08`)。
    /// <para>每个 tick 边沿:对本 tick 到点的每一炉,按当时 `CarryLoad` 判容量;
    /// 装不下 ⇒ 本批成品**整批**以 `DropSpawned` 落在 `tool_cell`(id **原样引用**点火已铸,
    /// 无第二铸造点)。</para>
    /// <para>⚠️ 本类**不持有状态**(除一个单调 tick 游标 + 计数),**不建投影** ——
    /// 投影由调用方给(唯一真源仍是 `InventoryFold`)。</para>
    /// </summary>
    public sealed class CraftCompletionSettler
    {
        private readonly IReadOnlyList<SimEvent> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly DropEventWriter _writer;
        private readonly Func<InventoryProjection> _projectionOf;
        private readonly CarryCapacity _capacity;

        /// <summary>`ItemKey` → 物品表 ordinal(`Craft` 载荷不含 ordinal,配方给的是 key)。</summary>
        private readonly Dictionary<ItemKey, int> _ordinalOf;

        /// <summary>ordinal → 单件重量。</summary>
        private readonly Dictionary<int, int> _weightOf;

        private readonly List<FurnaceCompletion> _buffer = new List<FurnaceCompletion>();
        private long _lastTick = long.MinValue;

        /// <summary>累计发出去的 `DropSpawned` 条数(可观测面:R-18-D 的「落地了几件」)。</summary>
        public int EmittedDropCount { get; private set; }

        /// <summary>构造。</summary>
        /// <param name="events">事件流只读视图(全流即可)。</param>
        /// <param name="decoder">载荷解码器(ADR-031)。</param>
        /// <param name="writer">世界流写者(本类只走 `RecordSpawnedWithId` —— **不铸造新号**:
        /// R-18-D 的 id 在点火时就已铸好)。</param>
        /// <param name="projectionOf">当前投影的**惰性取法**(每次结算重取,不持快照)。</param>
        /// <param name="capacity">容量上限(唯一真值 `CARRY_CAP`;AC-20-13 硬失败体)。</param>
        /// <param name="items">物品种子表(key → ordinal、ordinal → weight)。</param>
        /// <param name="recipes">配方表(ordinal → `Outputs`;解出每行的 `ItemKey`)。</param>
        /// <exception cref="ArgumentNullException">任一入参为 null(fail-closed:
        /// 宽容处理会让「起货装不下」静默变成「起货照塞」)。</exception>
        public CraftCompletionSettler(
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            DropEventWriter writer,
            Func<InventoryProjection> projectionOf,
            CarryCapacity capacity,
            ItemDataSet items,
            RecipeDataSet recipes)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _projectionOf = projectionOf ?? throw new ArgumentNullException(nameof(projectionOf));

            // ⚠️ **F9 形态自查**:`CarryCapacity` 是 `readonly struct`,构造期的「容量未定值」硬失败
            //    对 `default(CarryCapacity)`(Cap = 0,绕过构造器)**不成立**。本类若只信构造器,
            //    一个未赋值的字段会静默变成「容量 0」⇒ 每次起货都判装不下 ⇒ 全部落地。
            //    故在此**再验一次** `Cap` —— 记录在案:本行不是冗余。
            if (capacity.Cap <= 0)
                throw new ArgumentException(
                    $"容量上限 = {capacity.Cap} 非正 —— 拒绝以 `default(CarryCapacity)` 冒充已注入容量" +
                    "(AC-20-13 的 F9 补洞:构造器守卫挡不住 `default`)", nameof(capacity));
            _capacity = capacity;

            // ⚠️ `ItemDataSet` / `RecipeDataSet` 是 **struct**(`?.` 编译不过),其成员数组可为 null。
            ItemDef[] itemDefs = items.Items;
            int nItems = itemDefs != null ? itemDefs.Length : 0;
            _ordinalOf = new Dictionary<ItemKey, int>(nItems);
            _weightOf = new Dictionary<int, int>(nItems);
            for (int i = 0; i < nItems; i++)
            {
                ItemDef d = itemDefs[i];
                var key = new ItemKey(d.BaseId, d.ProcessingState);
                if (!_ordinalOf.ContainsKey(key)) _ordinalOf[key] = d.Ordinal;   // 先到先得(同 ItemLookup)
                if (!_weightOf.ContainsKey(d.Ordinal)) _weightOf[d.Ordinal] = d.Weight;
            }

            Recipe[] recipeDefs = recipes.Recipes;
            int nRecipes = recipeDefs != null ? recipeDefs.Length : 0;
            _recipesByOrdinal = new Dictionary<int, Recipe>(nRecipes);
            for (int i = 0; i < nRecipes; i++)
            {
                Recipe r = recipeDefs[i];
                if (!_recipesByOrdinal.ContainsKey(r.Ordinal)) _recipesByOrdinal[r.Ordinal] = r;
            }
        }

        private readonly Dictionary<int, Recipe> _recipesByOrdinal;

        /// <summary>tick 边沿入口 —— 结算本 tick 到点的炉。</summary>
        /// <param name="tick">当前逻辑 tick(`ITickProvider.CurrentTick`)。</param>
        /// <returns>本 tick 落地的成品**件数**(= 发出的 `DropSpawned` 条数)。</returns>
        /// <exception cref="InvalidOperationException"><paramref name="tick"/> 未严格递增
        /// —— 同一 tick 重入会让「装不下」被判两次并重复落地,故 fail-closed。</exception>
        public int OnTickEdge(long tick)
        {
            if (tick <= _lastTick)
                throw new InvalidOperationException(
                    $"[起货结算] 收到非递增 tick({tick} ≤ 上次 {_lastTick})—— " +
                    "同一 tick 边沿重入会重复判定容量并重复落地;拒绝静默双写");
            _lastTick = tick;

            int n = FurnaceCompletionSchedule.CollectAt(_events, _decoder, tick, _buffer);
            if (n == 0) return 0;

            InventoryProjection projection = _projectionOf();
            if (projection == null)
                throw new InvalidOperationException(
                    "[起货结算] 投影取法返回 null —— 拒绝以「无投影」冒充「空背包」" +
                    "(那会把每次起货都判成装得下)");

            int emitted = 0;
            for (int i = 0; i < n; i++)
            {
                FurnaceCompletion c = _buffer[i];
                emitted += SettleOne(in c, tick, projection);
            }
            EmittedDropCount += emitted;
            return emitted;
        }

        /// <summary>结算一炉。</summary>
        private int SettleOne(in FurnaceCompletion c, long tick, InventoryProjection projection)
        {
            CraftPayload p = FindCraftPayload(in c);

            // ── 已经在流里落地过 ⇒ 幂等跳过(读档重放 / 同一 tick 段的第二次重建)。
            if (AnyAlreadyDropped(p, projection)) return 0;

            Recipe recipe = ResolveRecipe(p.RecipeId, c.ActorId);

            int nOut = p.OutputQty != null ? p.OutputQty.Length : 0;
            if (nOut == 0) return 0;                       // 零产出 ⇒ 无处落地
            if (p.OutputInstanceIds == null || p.OutputInstanceIds.Length != nOut)
                throw new InvalidOperationException(
                    $"[起货结算] actor {c.ActorId} 的 Craft 产出组不等长 —— " +
                    "codec 侧本应拒收,流里出现即畸形");

            // ── 本批的**入库前**载重:现投影里已经算进了本批产出(切点 = 本 tick),
            //    故把它们减掉。用**投影里那一行**的重量(而不是配方表推算),
            //    因为 qty / 品级由载荷决定、配方表只是 key 的来源。
            long baseLoad = CarryLoadSolver.Sum(projection, WeightOf, c.ActorId);
            long batchLoad = 0L;
            for (int k = 0; k < nOut; k++)
            {
                long id = p.OutputInstanceIds[k];
                int qty = p.OutputQty[k];
                if (qty <= 0)
                    throw new InvalidOperationException(
                        $"[起货结算] 产出 qty={qty} ≤ 0 —— 21a 已拒收,流里出现即畸形");

                if (projection.TryGet(id, out InventoryRow row) && row.Owner == c.ActorId)
                {
                    baseLoad -= ContainerWeightFold.InstanceWeight(WeightOf(row.ItemOrdinal), row.Qty);
                }

                int ordinal = ResolveOutputOrdinal(recipe, k, c.ActorId);
                batchLoad = checked(batchLoad + ContainerWeightFold.InstanceWeight(WeightOf(ordinal), qty));
            }

            // ── 容量裁决(唯一实现点 `CarryLoadSolver.CanCarry`;本类不重写不等式)。
            //    判据以「整批」为单位:一批装不下 ⇒ 整批落地(规则六的原子单位)。
            //    ⚠️ `CanCarry` 的形参是 `(currentLoad, unitWeight, qty, cap)` 且判 `load + w×qty ≤ cap`;
            //       本批总重已算成 `batchLoad`,故取 `unitWeight = 1, qty = batchLoad` —— 等价于
            //       `baseLoad + batchLoad ≤ cap`。`batchLoad > int.MaxValue` 时不可能装得下
            //       (`cap` 是 `int`),**先**判这一条再转 `int`,不做 `Math.Min` 那种静默截断。
            bool fits = batchLoad <= int.MaxValue
                        && CarryLoadSolver.CanCarry(baseLoad, candidateUnitWeight: 1,
                                                    candidateQty: (int)batchLoad, capacity: _capacity);
            if (fits) return 0;                             // 装得下 ⇒ 留在背包(fold 已建行)

            // ── 整批落地:逐条 `DropSpawned`,id **原样引用**点火已铸的那一个。
            for (int k = 0; k < nOut; k++)
            {
                _writer.RecordSpawnedWithId(
                    tick,
                    p.OutputInstanceIds[k],
                    c.ToolCell,
                    ResolveOutputOrdinal(recipe, k, c.ActorId),
                    p.OutputQty[k]);
            }
            return nOut;
        }

        /// <summary>按全序键回查那条 `Craft` 事件并解载荷。</summary>
        /// <remarks>⚠️ 这是**额外的解码**,不是第二判据 —— 「哪炉到点」仍只有
        /// `FurnaceCompletionSchedule` 一处;此处只是取 18 不携带的产出量 / 配方号。</remarks>
        private CraftPayload FindCraftPayload(in FurnaceCompletion c)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                SimEvent e = _events[i];
                if (e.Kind != EventKind.Craft) continue;
                if (e.Tick != c.At.Tick || e.Seq != c.At.Seq
                    || e.Patient.Value != c.At.Patient.Value) continue;
                return _decoder.Decode<CraftPayload>(e.Kind, e.Payload);
            }
            throw new InvalidOperationException(
                $"[起货结算] 找不到全序键为 (tick={c.At.Tick}, seq={c.At.Seq}) 的 Craft 事件 —— " +
                "调度刚在同一个流上报告它到点,这里查不到说明两次读的不是同一条流");
        }

        /// <summary>本批里是否已有实例被 `DropSpawned` 点名(= 已经落地过)。</summary>
        private bool AnyAlreadyDropped(in CraftPayload p, InventoryProjection projection)
        {
            if (p.OutputInstanceIds == null) return false;
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Kind != EventKind.DropSpawned) continue;
                DropSpawnedPayload d = _decoder.Decode<DropSpawnedPayload>(
                    _events[i].Kind, _events[i].Payload);
                for (int k = 0; k < p.OutputInstanceIds.Length; k++)
                    if (d.InstanceId == p.OutputInstanceIds[k]) return true;
            }
            return false;
        }

        private Recipe ResolveRecipe(int recipeId, int actorId)
        {
            if (_recipesByOrdinal.TryGetValue(recipeId, out Recipe r)) return r;
            throw new InvalidOperationException(
                $"[起货结算] actor {actorId} 的 recipe_id={recipeId} 查无此号 —— " +
                "配方表未装载 / 版本不一致;拒绝以「零产出」冒充(fold 侧另报 CraftRecipeNotInTable)");
        }

        private int ResolveOutputOrdinal(Recipe recipe, int index, int actorId)
        {
            RecipeEntry[] outs = recipe.Outputs;
            if (outs == null || index >= outs.Length)
                throw new InvalidOperationException(
                    $"[起货结算] actor {actorId} 的配方 '{recipe.RecipeId}' 只有 " +
                    $"{(outs?.Length ?? 0)} 行产出,而载荷报第 {index} 行 —— 表版本不一致");

            ItemKey key = outs[index].Key;
            if (key.BaseId != null && _ordinalOf.TryGetValue(key, out int ordinal)) return ordinal;
            throw new InvalidOperationException(
                $"[起货结算] 配方 '{recipe.RecipeId}' 第 {index} 行产出 " +
                $"({key.BaseId}, {key.State}) 在物品表里折不出 ordinal —— 两张表版本不一致");
        }

        private int WeightOf(int ordinal)
        {
            if (_weightOf.TryGetValue(ordinal, out int w) && w > 0) return w;
            throw new InvalidOperationException(
                $"[起货结算] ordinal={ordinal} 的重量查不到或非正 —— " +
                "缺载体即 fail-loud(不静默按 0 计,那会让超载被漏判)");
        }
    }
}
