// S20-10/11/12 修复轮(2026-10-27 用户裁定「「装载库存实体」的链,现在建」)
//
// 权威来源:
//   · `production/qa/evidence/review-s20-10-inventory-001-2026-10-27.md` §2.1 B-1
//     —— `InventoryFold.Fold` 在生产代码中零调用点;`IsComplete` 对任何真实流恒 false;
//        契约 C-1 / C-6 对下游 today 零兑现面。
//   · `production/qa/evidence/review-s20-11-inventory-003-2026-10-27.md` §2.1 B-1
//     —— `BuildPartQualityGate` 是纯孤儿:P0 范围门 ③ 的「不变量破坏 = 构建失败」
//        没有任何强制点。同 `coding-standards.md`「某件有门」条款。
//   · `production/qa/evidence/review-s20-11-inventory-003-2026-10-27.md` §2.2 W-2
//     —— `ContainerWeightFold.FoldWeight` 零生产调用点。
//   · ADR-009 §四(派生态:投影由事件流确定性重建,不持有状态、不写流)
//   · ADR-005(全部模拟在整数域;宿主唯一执行 Step / CatchUp)
//   · ADR-014 §五(`IDataProvider` 装烘焙产物,失败硬失败 E-13)
//   · ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts};本类住 **Boot**,是装配层胶水)
//
// ⚠️ **为什么住 `Gameplay.Boot` 而不是 `Sim`**:
//   本类要同时看见 `Sim`(Fold / 堆叠 / 载重)、`Sim.Contracts`(ItemInstance / ItemDef)
//   与 `IDataProvider`(边界程序集)—— 只有装配层同时够得着这三者
//   (与 `SaveComposer` / `FoldRowCodec` 同格:装配层是「唯一同时看见两侧」的位置)。
//   门 A 不受影响:`Sim` 侧一行未动。
//
// ⚠️ **本类不写流**:纯读 —— 从 `stream.Events` 重建投影,零 `IEventSink.Append`
//   (ADR-009 §四 派生态;ADR-019 §三 同格「只读消费者」纪律)。
//
// ⚠️ **物品种子表是「惰性装载」,不是构造期装载**(2026-10-27 设计取舍):
//   构造期装载会让**每一个**装配点(全仓 100+ 处 `CompositionRoot.Assemble` 调用,
//   多为 EditMode 夹具)都付一次烘焙产物读取 + schema 校验 —— 那是把本接线变成
//   全局回归面(承 `CompositionRoot` 的「saveBasePath == null 不构造 SaveService」
//   同款判据:不为无关调用方制造依赖)。故形参是 `Func<IReadOnlyList<ItemDef>>`,
//   首次 `Rebuild()` 才求值(同 `KindPriorityTable.Provider` 的既成形态)。
//   ⇒ **装载失败的表征是「读档失败」,不是「装配失败」** —— 语义上恰好正确。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 库存实体装载服务 —— 把<b>事件流</b>重建为「库存投影 + 实例实体集」,
    /// 并在装载点执行 P0 范围门的执法。
    /// <para><b>这是 `InventoryFold.Fold` / `BuildPartQualityGate` / `CarryLoadSolver`
    /// 的<b>唯一生产调用点</b></b> —— 三者此前都是「有本体、零调用者」的孤儿
    /// (评审 S20-10 B-1 / S20-11 B-1 / W-2),本类是它们的接线落点。</para>
    /// <para><b>派生态</b>:零缓存、零增量 —— 每次 <see cref="Rebuild"/> 从流全量重算
    /// (ADR-009 §四 的「确定性重建」;缓存会引入第二真源)。</para>
    /// </summary>
    public sealed class InventoryLoadService
    {
        private readonly Func<IReadOnlyList<SimEvent>> _events;
        private readonly IPayloadDecoder _decoder;
        private readonly Func<IReadOnlyList<ItemDef>> _items;

        // 惰性索引(首次 Rebuild 建;重复 ordinal = 静态数据缺陷,建索引时抛)。
        private Dictionary<int, ItemDef> _byOrdinal;
        private Dictionary<ItemKey, ItemDef> _byKey;

        /// <summary>最近一次重建的结果(<c>null</c> = 尚未重建)。</summary>
        public InventoryLoadResult Current { get; private set; }

        /// <summary>构造装载服务(**不读物品表** —— 见类头注的惰性装载取舍)。</summary>
        /// <param name="events">三流事件源(生产 = <c>() =&gt; stream.Events</c>;委托形式
        /// ⇒ 读档换流后自动适应,无需重装配 —— 同 <c>LoadSequencer</c> 的既有形态)。</param>
        /// <param name="decoder">载荷解码器(<see cref="IPayloadDecoder"/>;非 null)。</param>
        /// <param name="items">物品种子集的**惰性取数**(非 null;首次 <see cref="Rebuild"/>
        /// 才调用 —— 生产 = <c>dataProvider.Load&lt;ItemDataSet&gt;().Items</c> /
        /// EditMode 兜底 = 同一份烘焙产物直读)。</param>
        /// <exception cref="ArgumentNullException">任一形参为 null(fail-closed —— 空解码器
        /// ⇒ 空投影 ⇒ 「库存全空」的静默失败)。</exception>
        public InventoryLoadService(Func<IReadOnlyList<SimEvent>> events,
                                    IPayloadDecoder decoder,
                                    Func<IReadOnlyList<ItemDef>> items)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _decoder = decoder ?? throw new ArgumentNullException(
                nameof(decoder), "未注入 IPayloadDecoder —— 拒绝以空投影冒充「库存为空」");
            _items = items ?? throw new ArgumentNullException(nameof(items));
        }

        /// <summary>从事件流重建库存。**唯一的重建入口**。</summary>
        /// <param name="enforceGates">是否执行 P0 范围门执法(见 <see cref="EnforceBuildPartQuality"/>)。</param>
        /// <returns>重建结果(投影 / 实体集 / 缺口 / 载重)。</returns>
        /// <exception cref="InvalidOperationException">物品种子表重复 ordinal / 缺条目 /
        /// `weight &lt; 1` / P0 范围门 ③ 违例(且 <paramref name="enforceGates"/> 为 true)。</exception>
        public InventoryLoadResult Rebuild(bool enforceGates = true)
        {
            EnsureIndex();

            IReadOnlyList<SimEvent> events = _events() ?? Array.Empty<SimEvent>();

            // ── ① 投影(20 的重建本体)──────────────────────────────────────
            InventoryFoldResult fold = InventoryFold.Fold(events, _decoder);

            // ── ② 实例实体集(供堆叠 / 折重 / 范围门消费)────────────────────
            //     ⚠️ 从**投影行**转实体,而不是从流里另找一份 —— 投影是流折叠的唯一产物,
            //        另取一份就是第二真源。品级来自 fold(它已从 `ResourceHarvested.out_quality`
            //        抄入,ADR-006 边界内),`Children` 投影不携带 ⇒ 传空数组并在结果里
            //        显式标注该限制(容器子树归一 Story 010 的类型面,今天不存在)。
            var instances = new List<ItemInstance>(fold.Projection.InstanceCount);
            foreach (long id in SortedIds(fold.Projection))
            {
                if (!fold.Projection.TryGet(id, out InventoryRow row)) continue;
                if (row.Qty <= 0) continue;                      // 已归零的不在实体集里

                ItemDef def = DefOfOrdinal(row.ItemOrdinal);
                instances.Add(new ItemInstance(
                    row.InstanceId,
                    new ItemKey(def.BaseId, def.ProcessingState),
                    row.Quality, row.Qty,
                    Array.Empty<long>()));                       // 非容器(容器归一 Story 010)
            }

            // ── ③ P0 范围门 ③ 执法(S20-11 B-1 的接线点)─────────────────────
            if (enforceGates) EnforceBuildPartQuality(instances);

            // ── ④ 载重折重(S20-11 W-2 的接线点)─────────────────────────────
            long load = CarryLoadSolver.Sum(
                fold.Projection, WeightOfOrdinal, owner: InventoryProjection.NoOwner);

            Current = new InventoryLoadResult(fold, instances, load);
            return Current;
        }

        /// <summary>P0 范围门 ③ 的执法体 —— `build_part` 实例 `quality` 恒 1
        /// (<see cref="BuildPartQualityGate"/>)。</summary>
        /// <param name="instances">待校验实体集。</param>
        /// <exception cref="InvalidOperationException">存在违例(消息含全部违例行)。</exception>
        /// <remarks>
        /// <para>⚠️ <b>接线缘由(评审 S20-11 B-1)</b>:该门此前**零生产调用点**,
        /// AC 记绿所依据的「不变量破坏 = 构建失败」不存在 —— 一个
        /// <c>build_part</c> 且 <c>quality = 3</c> 的实例可以自由穿过整个工程。
        /// 本方法是它的第一个(也是唯一一个)生产强制点。</para>
        /// <para>⚠️ <b>判据面 = 装载期</b>(不是「构建期」):本服务在<b>读档 / 投影重建</b>时
        /// 运行,故「破坏 ⇒ 装载失败」。与 story 原文的「构建失败」措辞有差 —— 已在
        /// story-003 就地登记,不在此改写 AC 原文。</para>
        /// </remarks>
        private void EnforceBuildPartQuality(IReadOnlyList<ItemInstance> instances)
        {
            BuildPartQualityGate.RequireValid(instances, DefOf);
        }

        /// <summary>按 <see cref="ItemKey"/> 取条目(供 <see cref="BuildPartQualityGate"/>;
        /// 查不到即抛 —— 门不得静默把未知键当「非 build_part」放行)。</summary>
        private ItemDef DefOf(ItemKey key)
        {
            if (_byKey.TryGetValue(key, out ItemDef def)) return def;
            throw new InvalidOperationException(
                $"[库存装载] item_key={key} 不在物品种子表里 —— 静态数据缺陷");
        }

        /// <summary>按 ordinal 取单件重量(供 <see cref="CarryLoadSolver.Sum"/> 的委托)。
        /// <para>⚠️ 查不到 = 静态数据缺陷,抛(不静默按 0 —— 那会让载重凭空变轻)。</para></summary>
        private int WeightOfOrdinal(int ordinal)
        {
            ItemDef def = DefOfOrdinal(ordinal);
            if (def.Weight < 1)
                throw new InvalidOperationException(
                    $"[库存装载] ordinal={ordinal}({def.BaseId})的 weight={def.Weight} < 1 —— " +
                    "静态数据缺陷(AC-21a-15 归物品表校验轮);不静默按 0 计重");
            return def.Weight;
        }

        /// <summary>ordinal → 物品条目(投影行只携带 <c>ItemOrdinal</c>,没有这张索引解不出实体)。</summary>
        /// <exception cref="InvalidOperationException">该 ordinal 不在物品种子表里
        /// —— 投影行引用了不存在的物品 = 静态数据缺陷 / 存档跨版本损坏,不静默跳过。</exception>
        private ItemDef DefOfOrdinal(int ordinal)
        {
            if (_byOrdinal.TryGetValue(ordinal, out ItemDef def)) return def;
            throw new InvalidOperationException(
                $"[库存装载] ordinal={ordinal} 不在物品种子表里 —— 投影行引用了不存在的物品条目;" +
                "静态数据缺陷或存档跨版本损坏(ADR-014 §五:装载失败即硬失败,不降级)");
        }

        /// <summary>首次重建时建两张索引(<c>Ordinal</c> / <c>ItemKey</c>),并做静态数据门。
        /// <para>⚠️ <b>只建一次</b>:物品表在整个运行期不可变(烘焙产物 + append-only ordinal 门),
        /// 重复求值只会重复抛。</para></summary>
        private void EnsureIndex()
        {
            if (_byOrdinal != null) return;

            IReadOnlyList<ItemDef> items = _items()
                ?? throw new InvalidOperationException(
                    "[库存装载] 物品种子表取数为 null —— 拒绝以空表冒充「库存为空」(ADR-014 §五)");

            // Ordinal 是**不可变主键**(ADR-006 D-21-13 / `_ordinal_baseline.json` 的
            // append-only 门):重复即静态数据缺陷,当场抛而不是静默取最后一个。
            var byOrdinal = new Dictionary<int, ItemDef>(items.Count);
            var byKey = new Dictionary<ItemKey, ItemDef>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                ItemDef d = items[i];
                if (byOrdinal.TryGetValue(d.Ordinal, out ItemDef dup))
                    throw new InvalidOperationException(
                        $"[库存装载] 物品表 ordinal={d.Ordinal} 重复({dup.BaseId} / " +
                        $"{d.BaseId}) —— ordinal 不可变且唯一(ADR-006 D-21-13 的 append-only 门)");
                byOrdinal[d.Ordinal] = d;

                var key = new ItemKey(d.BaseId, d.ProcessingState);
                if (byKey.ContainsKey(key))
                    throw new InvalidOperationException(
                        $"[库存装载] item_key={key} 重复 —— (base_id, processing_state) 是复合主键" +
                        "(AC-21a-21)");
                byKey[key] = d;
            }

            _byKey = byKey;
            _byOrdinal = byOrdinal;
        }

        /// <summary>投影内实例 id 升序(确定性遍历 —— `Dictionary` 枚举序不是契约)。</summary>
        private static List<long> SortedIds(InventoryProjection projection)
        {
            var ids = new List<long>(projection.AllInstanceIds);
            ids.Sort();
            return ids;
        }
    }

    /// <summary>一次库存重建的结果(派生态快照 —— 下次 <see cref="InventoryLoadService.Rebuild"/>
    /// 会替换它,不持有状态)。</summary>
    public sealed class InventoryLoadResult
    {
        /// <summary>折叠结果(投影 + 缺口;<see cref="InventoryFoldResult.IsComplete"/> 的语义见下)。</summary>
        public InventoryFoldResult Fold { get; }

        /// <summary>实例实体集(id 升序)。供堆叠视图 / 折重 / 范围门消费。</summary>
        public IReadOnlyList<ItemInstance> Instances { get; }

        /// <summary>未归属实例(<see cref="InventoryProjection.NoOwner"/>)的总载重
        /// (S20-11 W-2 的接线产物)。</summary>
        public long UnclaimedLoad { get; }

        internal InventoryLoadResult(InventoryFoldResult fold,
                                     IReadOnlyList<ItemInstance> instances, long unclaimedLoad)
        {
            Fold = fold; Instances = instances; UnclaimedLoad = unclaimedLoad;
        }

        /// <summary>投影是否完整(可当 <c>InventoryOf</c> 用)。
        /// <para>⚠️ <b>本字段的语义边界(禁借绿)</b>:它 = <see cref="InventoryFoldResult.IsComplete"/>,
        /// 即「零缺口」。今天在真实存档上**恒 false** —— R16 / R17 / R18 三条载荷缺位
        /// (评审 S20-10 B-1 的派生结论)。<b>本服务不把 false 转成异常</b>:
        /// 缺载体是**已知的载荷缺口**,不是装载失败;把它转成 throw 会让任何含建造事件的
        /// 存档都读不进来。<b>下游必须读本字段</b>再决定能不能当完整库存用(C-6)。</para></summary>
        public bool IsComplete => Fold.IsComplete;

        /// <summary>缺口清单(空 = 无缺口)。</summary>
        public IReadOnlyList<InventoryFoldGap> Gaps => Fold.Gaps;

        /// <summary>投影(部分态时仍可读,但不可当 <c>InventoryOf</c>)。</summary>
        public InventoryProjection Projection => Fold.Projection;
    }
}
