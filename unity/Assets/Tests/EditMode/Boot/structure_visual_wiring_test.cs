// T22 批九 · `TD-building-05` 剩余半边之一:**生产装配**是否真的铺了结构视觉(2026-11-11)
//
// 权威:
//   ADR-015 §一(视觉层不进 sim)· ADR-023 §②(场景零预摆,运行期生成)
//   `TD-building-05` 解除动作(「可见 + 可辨识 + 与格对齐」)
//   `CompositionRoot.cs` 的「建造槽位的最小视觉载体」段(2026-11-10 T22 批一)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件补的是**上一轮缺的那一层**
// ─────────────────────────────────────────────────────────────────────────────
// `structure_visual_spawner_test`(T22 批一,7 条)**直接 `new` 生成器**再断言 ——
// 它证明「生成器对」,不证明「生产装配真的把它接上并铺出来」。
// 后者在上一轮只有**真包日志**(`[结构视觉] 槽位体块 9 个 + 容器柜 1 个`)一条证据,
// 没有机器判据。本件从**生产装配入口** `CompositionRoot.Assemble(world:, dataProvider:)`
// 出发,断言三件事:
//   ① 装配真把 `StructureVisualSpawner` 建出来(非 null);
//   ② 它铺出的体块数 == 真产物里 `baked_initial` 行数 + 容器行数(不是内联常数);
//   ③ **负控**:不传 provider 的裸装配 ⇒ 生成器为 null(「本次装配没装世界」可观测,
//      不是「世界没有槽位」的静默形态 —— 那两句不可混读)。
//
// ⚠️ **读外部夹具的具名声明**(`test-standards.md`):本件读
//   `Assets/DataCooked/*.cooked.bytes`(经 `DiskBackedDataProvider` → `DataCookedDisk.ReadAll`)
//   与 `Assets/Gameplay.Presentation/Patients/Resources/PatientPlaceholderMaterial.mat`(经 Resources)。
//   理由:要测的是「**真产物**下生产装配铺了几个」,内联字面量会让「表改了夹具仍绿」。
// ⚠️ 零写盘、零随机、零时间依赖。生成的 GameObject 在 `[TearDown]` 显式销毁。

using System.Linq;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    [TestFixture]
    public sealed class structure_visual_wiring_test
    {
        private CompositionRootServices _services;

        [TearDown]
        public void TearDown()
        {
            StructureVisualSpawner spawner = _services?.StructureVisualSpawner;
            if (spawner != null)
            {
                spawner.DestroyAll();
                spawner.Dispose();
            }
            _services = null;
        }

        // ── ① 生产装配真的铺了 ─────────────────────────────────────────────

        [Test]
        public void test_assemble_withRealProvider_spawnsOnePerBakedSlotPlusContainers()
        {
            IDataProvider provider = DiskBackedDataProvider.Create();

            // 前置:真产物自身得有内容 —— 否则「0 == 0」的空洞正控也会绿。
            int baked = provider.Load<WorldBuildSlotsDataSet>().Rows.Count(r => r.BakedInitial);
            int containers = provider.Load<WorldContainersDataSet>().Containers.Count;
            Assert.That(baked, Is.GreaterThan(0), "前提:真产物里须有 baked 槽位");
            Assert.That(containers, Is.GreaterThan(0), "前提:真产物里须有容器行(20 的入口)");

            _services = CompositionRoot.Assemble(
                dataProvider: provider,
                world: MinimalWorldFactory.Create(provider));

            Assert.That(_services.StructureVisualSpawner, Is.Not.Null,
                "生产装配没建生成器 ⇒ 玩家在画面上找不到灶台 / 墙 / 柜(`TD-building-05` 的原病)");

            Assert.That(_services.StructureVisualSpawner.SpawnedCount, Is.EqualTo(baked + containers),
                "体块数须 == baked 槽位 + 容器行(空槽不画;容器柜是 20 的入口,不在槽位表里)");
        }

        // ── ② 负控:不传 provider ⇒ 不建(可观测的降级)──────────────────────

        [Test]
        public void test_assemble_withoutProvider_leavesSpawnerNull()
        {
            _services = CompositionRoot.Assemble();

            Assert.That(_services.StructureVisualSpawner, Is.Null,
                "裸装配(`dataProvider == null`)不该铺视觉 —— 「本次装配没装世界」必须是**可观测**的 null," +
                "不是「世界没有槽位」的静默形态");
        }
    }
}
