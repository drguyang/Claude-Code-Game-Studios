// S20-19 A-5 修复轮 · 2026-10-28 —— **建造扣料归因**的装配级回归夹具
//
// 权威来源:
//   design/gdd/inventory-and-items.md 规则六 + R16 甲案(2026-10-27 用户裁定:
//     **P0 单机 ⇒ 建造扣料恒归唯一玩家**)
//   production/qa/evidence/review-s20-19-2026-10-28.md §5.5「A-5 升级版」+ §七 结论
//   `unity/Assets/Gameplay.Boot/CompositionRoot.cs` 的「扣料归属槽」段(A-5 修复落点)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉的是什么(以及为什么必须在 `CompositionRoot.Assemble` 这一层测)
// ─────────────────────────────────────────────────────────────────────────────
// A-5 的病灶**不在 `InventoryFold` 里** —— fold 的 R16 逻辑本来就对(它老实报了
// `BuildCostAttributionUnresolved`)。病灶在**装配**:`InventoryLoadService` 构造时
// 压根没传 `foldContext`,于是 `SolePlayer` 恒 `null`,`ApplyBuildCost` 每次都从
// 「归因不可判」出口返回 ⇒ **建造扣料在玩家路径上从不发生**。
// ⇒ 只测 fold 的单元夹具**测不到它**(fold 一直是对的),
//   只测 `BuildController` 的夹具也测不到它(库存门用的是另一个 int)。
//   **唯一能抓到它的位置 = 装配后**:同一个 `CompositionRootServices` 里
//   「绑定归属槽 → 重建库存 → 材料是否真的少了 cost(m)」。
//
// 判别式(两条断言各挡一面):
//   ① **绑定前**:`gaps` 含 `BuildCostAttributionUnresolved`,且材料**一份不少**
//      —— 钉住 fail-closed(宁可报缺口,也不扣错人);
//   ② **绑定后**(经 `BuildController.BindOwner`,生产唯一绑定入口):同类缺口**消失**,
//      且材料**恰少 `cost(m)`** —— 钉住「fold 读的就是建造门读的那个 int」。
//      ② 单独一条也能挡「换成一个独立新建的 `BuildOwnerSlot`」这类假修
//      (那种写法里 fold 永远读不到绑定的值 ⇒ ② 红)。
//
// ⚠️ 本件走**真装配 + 真烘焙产物**(`MinimalWorldFactory` + `DataCookedDisk` 的
//   `build_modules` / `items` / `recipes`):没有一处手搓 `InventoryFoldContext`,
//   否则测的就只是「我构造的那个上下文」,与装配是否接线无关 —— 那正是本件要抓的病。
// ⚠️ 零随机 / 零时间依赖 / 零写盘。

using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>S20-19 A-5:建造扣料归因在装配层真的成立(fold 读 = 建造门读)。</summary>
    [TestFixture]
    public class inventory_fold_sole_player_wiring_test
    {
        // 夹具值(非 GDD 数值):owner 42 = 唯一玩家;多余料 5 份 = 扣完应剩的量。
        private const int Owner = 42;
        private const int Leftover = 5;
        private const long DropId = 7001L;
        private const long StructureId = 9001L;

        /// <summary>绑定**之前**:归因不可判 ⇒ 显式缺口 + 材料一份不少(fail-closed)。</summary>
        [Test]
        public void test_a5_beforeOwnerBound_foldReportsAttributionGapAndDeductsNothing()
        {
            CompositionRootServices services = CompositionRoot.Assemble(
                world: MinimalWorldFactory.Create());
            (int moduleId, ModuleDefinition def) = FirstCostlyModule(services);

            AppendBuildScenario(services, moduleId, def);

            InventoryLoadResult result = services.InventoryLoad.Rebuild();

            Assert.That(GapKinds(result), Has.Member(
                    InventoryFoldGapKind.BuildCostAttributionUnresolved),
                "玩家 id 尚未铸造(归属槽未绑定)⇒ 必须报「归因不可判」,不得静默按某个 id 扣料");
            Assert.That(HeldQty(result.Projection, Owner), Is.EqualTo(def.Cost + Leftover),
                "未绑定期间材料必须**一份不少**(fail-closed:A-5 修复不得把「不知道扣谁」变成「扣了」)");
        }

        /// <summary>绑定**之后**(经生产唯一入口 `BuildController.BindOwner`):
        /// 同类缺口消失,材料**恰少 `cost(m)`** —— 证明 fold 与建造门读同一个 int。</summary>
        [Test]
        public void test_a5_afterOwnerBoundViaBuildController_foldDeductsExactlyCost()
        {
            CompositionRootServices services = CompositionRoot.Assemble(
                world: MinimalWorldFactory.Create());
            (int moduleId, ModuleDefinition def) = FirstCostlyModule(services);

            AppendBuildScenario(services, moduleId, def);
            services.BuildController.BindOwner(Owner);

            InventoryLoadResult result = services.InventoryLoad.Rebuild();

            Assert.That(GapKinds(result), Has.No.Member(
                    InventoryFoldGapKind.BuildCostAttributionUnresolved),
                "绑定后 `SolePlayer` 必须是该玩家 —— 缺口仍在 ⇒ fold 与建造门不是同一个 int(A-5 未修)");
            Assert.That(GapKinds(result), Has.No.Member(
                    InventoryFoldGapKind.BuildCostExceedsHolding),
                "料给够了还报「不足」⇒ 扣料找错了 owner 名下的栈");
            Assert.That(HeldQty(result.Projection, Owner), Is.EqualTo(Leftover),
                "扣减量必须恰为 `cost(m)` —— 这就是「建造扣料真的发生了」的唯一可观测判据");
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具工具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>从**同一份**烘焙模块目录里取第一个 <c>cost ≥ 1</c> 的模块
        /// (目录 = 装配读的那份产物;不手搓模块,否则测的是我造的目录)。</summary>
        private static (int moduleId, ModuleDefinition def) FirstCostlyModule(
            CompositionRootServices services)
        {
            IModuleCatalog catalog = new BuildModulesCatalog(DataCookedDisk.LoadBuildModules());

            foreach (int id in services.BuildController.ModuleIds)
            {
                ModuleDefinition? def = catalog.GetModuleDefinition(id);
                if (def.HasValue && def.Value.Cost >= 1)
                    return (id, def.Value);
            }

            Assert.Fail("烘焙模块目录里没有 cost ≥ 1 的模块 —— 本夹具无法构造扣料场景");
            return (-1, default);
        }

        /// <summary>落一段最小场景:料掉落 → 采集定品级 → 玩家拾取 → 该模块落成。</summary>
        private static void AppendBuildScenario(CompositionRootServices services,
            int moduleId, ModuleDefinition def)
        {
            int qty = def.Cost + Leftover;

            services.EventSink.Append(Event(services, EventKind.DropSpawned,
                new DropSpawnedPayload(DropId, new WorldPos(1, 0, 1), def.BuildItemKey, qty), 1, 0));
            services.EventSink.Append(Event(services, EventKind.ResourceHarvested,
                new ResourceHarvestedPayload(DropId, nodeId: 100, gatherSeq: 1,
                    qty: qty, outQuality: 1), 1, 1));
            services.EventSink.Append(Event(services, EventKind.DropClaimed,
                new DropClaimedPayload(DropId, Owner, 1), 2, 0));
            services.EventSink.Append(Event(services, EventKind.StructurePlaced,
                new StructurePlacedPayload(StructureId, new WorldPos(0, 0, 0),
                    moduleId, orientation: 0, variant: 0), 3, 0));
        }

        private static SimEvent Event<T>(CompositionRootServices services, EventKind kind,
            T payload, long tick, long seq) where T : struct
            => new SimEvent(tick, PatientId.None, seq, kind, services.Encoder.Encode(kind, payload));

        private static IReadOnlyList<InventoryFoldGapKind> GapKinds(InventoryLoadResult result)
        {
            var kinds = new List<InventoryFoldGapKind>(result.Gaps.Count);
            foreach (InventoryFoldGap gap in result.Gaps) kinds.Add(gap.Kind);
            return kinds;
        }

        /// <summary>该 owner 名下总份数(从投影现算,不缓存第二真值)。</summary>
        private static int HeldQty(InventoryProjection projection, int owner)
        {
            int sum = 0;
            foreach (long id in projection.InstancesOf(owner))
                if (projection.TryGet(id, out InventoryRow row)) sum += row.Qty;
            return sum;
        }
    }
}
