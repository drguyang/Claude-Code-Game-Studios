// S20-20 挂标 #1 修复轮 · 2026-10-28 —— **Addressables 装载面**的三条夹具
//
// 权威来源:
//   `production/sprints/sprint-20.md`(S20-20 挂标 #1:「Addressables 装载面未接
//     ⇒ 生产 build 里世界槽位目录仍为空」)
//   `production/qa/evidence/fix-a5-sole-player-2026-10-28.md` §七-2
//     (「`dataProvider != null` 那半今天**零夹具**」)
//   ADR-014 §五(data-core 单组预载)· ADR-022 §二(槽位骨架唯一作者 = 关卡工具)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉的是什么
// ─────────────────────────────────────────────────────────────────────────────
// S20-20 把槽位骨架接进了 `MinimalWorldFactory`,但只接了**磁盘**那一半:
// `DataCookedDisk.LoadBuildSlots()` 读 `Assets/DataCooked/` —— 该目录**不随玩家构建出货**
// ⇒ 生产 build 里槽位目录恒空、`PlaceableChecker` 恒 `SlotNotFound`(玩家建不了东西)。
// 本轮补的是**另一条腿**(`AddressablesDataProvider.Load<WorldBuildSlotsDataSet>()`)。
//
// 三条夹具各挡一面:
//   ① **provider 真的被读、且其内容真的被用** —— 合成 1 行产物经 provider 送达 ⇒
//      `SlotCatalog` 恰 1 格(盘上真产物是 13 格 ⇒ 「读了盘」这条退路会被本断言直接否掉);
//   ② **provider 失败 ⇒ 响亮降级**,不是静默回落到磁盘(回落会让「组条目漏登记」在
//      编辑器里永远看不出来,到玩家机上才炸);
//   ③ **A-5 §七-2 的 `dataProvider != null` 那半** —— fold 的三份表
//      (`items` / `recipes` / `modules`)经 provider 送达时,扣料仍恰为 `cost(m)`。
//
// ⚠️ 零随机 / 零时间依赖 / 零写盘(只读 `Assets/DataCooked`)。
// ⚠️ 本件**不证**:`Addressables` 真实 catalog 的装载(EditMode 无 locator)——
//    字节取自注入缝,与生产同一条 `FetchBytes` → codec 链,差别只在字节从哪来。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>S20-20 装载面:槽位骨架与 fold 三份表经 `IDataProvider` 送达。</summary>
    [TestFixture]
    public class world_slots_provider_wiring_test
    {
        // 夹具值(非 GDD 数值):owner 42 = 唯一玩家;多余料 5 份 = 扣完应剩的量。
        private const int Owner = 42;
        private const int Leftover = 5;
        private const long DropId = 7001L;
        private const long StructureId = 9001L;

        /// <summary>合成 1 行产物 —— 与真产物(13 行)在**格数**上可区分。</summary>
        private const string SyntheticSingleSlotJson = @"{
  ""schema_version"": 2,
  ""level_id"": ""p0_clinic_provider_probe"",
  ""world_size"": [16, 1, 16],
  ""slots"": [
    {""cell"": [3, 0, 3], ""slot_type"": ""Decor"", ""baked_initial"": false}
  ]
}";

        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        // ══════════════════ ① provider 的内容真的被用 ══════════════════

        /// <summary>合成 1 行产物经 provider 送达 ⇒ 世界里**恰 1** 个槽位,且那格就是它。
        /// <para><b>判别力</b>:① 把 `LoadSlotCatalog` 退回无参 `DataCookedDisk.LoadBuildSlots()`
        /// ⇒ 槽位变成盘上真产物的 13 格、且注入地址一次都没被请求 ⇒ 两条断言同时红;
        /// ② 把 provider 分支删掉、只留磁盘 ⇒ 同上。</para></summary>
        [Test]
        public void test_slots_withProvider_usesProviderArtifact_notDisk()
        {
            var requested = new List<string>();
            byte[] synthetic = WorldBuildSlotsBake.BakeFromSourceText(SyntheticSingleSlotJson).Cooked;

            AddressablesDataProvider provider = MakeProvider(address =>
            {
                requested.Add(address);
                if (address == AddressablesDataProvider.WorldBuildSlotsAddress) return synthetic;
                throw new InvalidOperationException($"本夹具只供槽位产物,不该请求 {address}");
            });

            World world = MinimalWorldFactory.Create(provider);

            Assert.That(requested, Has.Member(AddressablesDataProvider.WorldBuildSlotsAddress),
                "provider 一次都没被请求槽位产物 ⇒ 装载走了磁盘兜底(生产构建里那条路不存在)");
            Assert.That(world.SlotCatalog.Count, Is.EqualTo(1),
                "槽位数 ≠ 合成产物行数(1)⇒ 读的不是注入的那份字节(盘上真产物是 13 格)");
            Assert.That(world.SlotCatalog.IsValidSlot(new WorldPos(3, 0, 3)), Is.True,
                "合成产物声明的是 (3,0,3) —— 该格必须在目录里");
        }

        // ══════════════════ ② provider 失败 ⇒ 响亮降级 ══════════════════

        /// <summary>provider 抛 ⇒ 空目录 + `LogError`(**不**回落磁盘)。
        /// <para><b>判别力</b>:把 `catch` 换成「失败即回落 `DataCookedDisk`」⇒ 两条断言
        /// 同时红 —— 目录变成盘上 13 格(≠ 0),且 `LogAssert.Expect` 要求的那条
        /// 「装载源 —— data-core Addressables」**根本没打**(回落分支不报错)。
        /// 这条钉的是「组条目漏登记」必须**响亮**,而不是悄悄换成另一条数据源。</para></summary>
        [Test]
        public void test_slots_providerFailure_degradesToEmptyCatalogAndNamesSource()
        {
            AddressablesDataProvider provider = MakeProvider(address =>
                throw new InvalidOperationException("组条目缺失(夹具模拟)"));

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                @"槽位骨架装载失败.*装载源 —— data-core Addressables"));

            World world = MinimalWorldFactory.Create(provider);

            Assert.That(world.SlotCatalog.Count, Is.Zero,
                "装载失败 ⇒ 槽位目录必须为空(`PlaceableChecker` 由此恒 SlotNotFound),不得半装配");
        }

        // ══════════════════ ③ A-5 §七-2:fold 三份表经 provider 送达 ══════════════════

        /// <summary>`dataProvider != null` 那半:三份表(`items` / `recipes` / `modules`)
        /// 经 provider 装载时,建造扣料仍**恰少 `cost(m)`**。
        /// <para><b>判别力</b>:把 `CompositionRoot` 里 `loadItemsSeed` / `loadRecipesSeed` /
        /// `modulesCatalog` 任一路径的 **provider 分支**退回写死的常量表(或 `null` 目录)⇒
        /// 归因缺口重新出现 / 扣料不发生 ⇒ 本测红。</para>
        /// <para>⚠️ 与 `inventory_fold_sole_player_wiring_test` 的分工:那条走
        /// `dataProvider == null` 的磁盘兜底,钉的是「槽 → fold 的归属接线」;本条钉的是
        /// 「同一套语义在 **provider 分支**上成立」。两条路径的等价性**不再只是论证**。</para></summary>
        [Test]
        public void test_a5_foldContext_tablesLoadedThroughProvider_deductsExactlyCost()
        {
            AddressablesDataProvider provider = MakeRealProductProvider();
            CompositionRootServices services = CompositionRoot.Assemble(
                dataProvider: provider,
                world: MinimalWorldFactory.Create(provider));
            (int moduleId, ModuleDefinition def) = FirstCostlyModule(services);

            AppendBuildScenario(services, moduleId, def);
            services.BuildController.BindOwner(Owner);

            InventoryLoadResult result = services.InventoryLoad.Rebuild();

            Assert.That(GapKinds(result), Has.No.Member(
                    InventoryFoldGapKind.BuildCostAttributionUnresolved),
                "provider 分支上归因缺口仍在 ⇒ 三份表没送达 fold");
            Assert.That(GapKinds(result), Has.No.Member(
                    InventoryFoldGapKind.BuildCostExceedsHolding),
                "料给够了还报不足 ⇒ 扣料找错了 owner 名下的栈");
            Assert.That(HeldQty(result.Projection, Owner), Is.EqualTo(Leftover),
                "扣减量必须恰为 `cost(m)` —— provider 分支与磁盘分支须等价");
        }

        // ══════════════════════════════════════════════════════════════════
        // 夹具工具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>真产物字节源(只读 `Assets/DataCooked/`,零写盘)。</summary>
        private static AddressablesDataProvider MakeRealProductProvider()
            => MakeProvider(address =>
            {
                string path = Path.Combine(CookedDir, address);
                Assert.That(File.Exists(path), Is.True,
                    $"盘上产物缺失:{path} —— 重烘:「大医精诚/数据管线」全量烘焙");
                return File.ReadAllBytes(path);
            });

        /// <summary>经内部构造缝造 provider(internal ⇒ 反射;先例
        /// `clinic_machine_cooked_codec_test.cs:387` / `tutorial_content_provider_load_test.cs:50`)。
        /// <para>构造不存在 ⇒ 明确失败并点名:说明注入缝被移除,而移除它就得另找搬运本件判据的办法
        /// —— 不允许静默跳过。</para></summary>
        private static AddressablesDataProvider MakeProvider(Func<string, byte[]> byteSource)
        {
            ConstructorInfo ctor = typeof(AddressablesDataProvider).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Func<string, byte[]>) },
                modifiers: null);

            Assert.That(ctor, Is.Not.Null,
                "`AddressablesDataProvider(Func<string, byte[]>)` 内部构造不见了 —— 装载面夹具的注入缝被移除");

            return (AddressablesDataProvider)ctor.Invoke(new object[] { byteSource });
        }

        /// <summary>从**装配读的那份**模块目录里取第一个 <c>cost ≥ 1</c> 的模块。</summary>
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
