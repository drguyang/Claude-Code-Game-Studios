// 存档三条路径的**装配级**夹具(2026-11-04 · 用户指令「试试退出 / 自动 / 手动三种情况」)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/DataCooked/*.cooked.bytes`(85 件烘焙产物,已入库、未被 gitignore)。
//     必须读**真产物**而非内联假表 —— 本件要走的正是生产组版闭包,它按
//     `DataCoreAddresses` 逐份取**头字节**算 `ConfigVersion`(ADR-037),并读
//     `5_time` / `5_weather` 的时间七值(ADR-038 §D3);手搓假表等于把被测路径换掉。
//     取数面 = `DataCookedDisk.ReadAll`(先例:`MinimalWorldFactory` / `CompositionRoot` 的
//     「provider == null」兜底分支用的同一批字节、同一批 codec)。
// 写:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态),TearDown 删目录。
//
// ─────────────────────────────────────────────────────────────────────────────
// 与 `save_three_paths_test` 的分工
// ─────────────────────────────────────────────────────────────────────────────
//   那一件自己 hand-wire 一个 `SaveService`(证的是「服务本身能写能读」);
//   本件用**生产装配根** `CompositionRoot.Assemble(saveBasePath: 临时目录)` —— 证的是
//   「三条触发线真的接在**那个**袋子上」:自动 = 袋里的 `CheckpointFallback`,
//   手动 = 「留档」路由器 + 袋里的 `SaveService`,退出 = 袋里的 `SaveService.SaveOnExit`。
//   ⚠️ 这正是本项目反复抓到的形态:**服务层全绿而生产零调用点**。两件都要有。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么要注入 provider(实测教训,不是预防性设计)
// ─────────────────────────────────────────────────────────────────────────────
//   首版本件**没给** `dataProvider` ⇒ 三条全红,且红法各不相同:
//     · 退出:`InvalidOperationException [E-13][data-core] 无法派生 ConfigVersion 快照:
//       dataProvider 为 null`(组版闭包硬失败);
//     · 自动:目录**空**、无声 —— 兜底把异常降级成 `LogError`(设计内行为);
//     · 手动:`TrySave()` 返回 `false`(受理失败)。
//   ⇒ 「给不给 provider」不是夹具便利,是**生产装配条件**:`BootRoot` 永远传
//   `DataCorePreloader.Provider`。不给就等于在测一条生产上不存在的装配。
//
//   注入面见共享件 `Tests/EditMode/DiskBackedDataProvider.cs`(反射取 `AddressablesDataProvider`
//   的可注入构造;为什么不能自造 `IDataProvider` 实现在那里逐条写明)。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public sealed class SaveThreePathsAssemblyTest
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_save3asm_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, true); } catch { }
            }
        }

        private static string[] FilesOnDisk(string dir)
        {
            string[] files = Directory.GetFiles(dir);
            for (int i = 0; i < files.Length; i++) files[i] = Path.GetFileName(files[i]);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        /// <summary>装配一个**真**服务袋:生产装配根 + 生产同款 provider + 临时存档目录。</summary>
        /// <remarks>provider 见 `Tests/EditMode/DiskBackedDataProvider.cs`(测试共享件)。</remarks>
        private CompositionRootServices AssembleBag()
            => CompositionRoot.Assemble(
                dataProvider: DiskBackedDataProvider.Create(), saveBasePath: _dir);

        // ══════════════════════════════════════════════════════════════════
        // ① 自动:袋里的定时兜底到点 ⇒ `save_auto.bin`
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_assembly_autoCheckpointFallback_writesAutoSlot()
        {
            using (CompositionRootServices bag = AssembleBag())
            {
                Assert.That(bag.CheckpointFallback, Is.Not.Null,
                    "装配根必须构造定时兜底(`saveBasePath` 非 null 时)");
                Assert.That(bag.SaveService, Is.Not.Null, "[前置] 存档服务须已装配");

                // 正控前置:没到间隔 ⇒ 盘上必须还是空的。否则下面的断言在
                // 「每 tick 都写」的实现下也成立(= 空洞正控,F4)。
                bag.CheckpointFallback.OnTicks(CheckpointFallback.DefaultIntervalTicks - 1);
                Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.False,
                    $"未到间隔不该落盘;盘上:[{string.Join(", ", FilesOnDisk(_dir))}]");

                bag.CheckpointFallback.OnTicks(1);

                // ⚠️ `Checkpoint` 是**异步**的(ADR-010 §六 · AC-7a-07):门与组版在主线程,
                //    写盘 + 推高水位在 worker。不排空就断言 = 断言「还没写完」。
                Assert.That(bag.SaveService.WriteQueue.WaitForIdle(5000), Is.True,
                    "写队列 5s 未排空 —— 自动存档卡在 worker 上");
                Assert.That(bag.SaveService.WriteQueue.LastFailure, Is.Null,
                    $"worker 写盘失败(LastFailure):{bag.SaveService.WriteQueue.LastFailure}");

                Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.True,
                    $"自动存档未落盘;盘上:[{string.Join(", ", FilesOnDisk(_dir))}]");
                Assert.DoesNotThrow(() => bag.SaveService.Load(SaveService.AutoSaveSlot),
                    "自动存档必须读得回来 —— 写得进读不出 = 玩家「继续诊治」时才发现");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 手动:装配根给的 SaveService + 「留档」路由器 ⇒ 下一可写槽
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_assembly_manualRouter_writesNextWritableSlot()
        {
            using (CompositionRootServices bag = AssembleBag())
            {
                var router = new SaveSlotCommandRouter(
                    sequencer: null, failureSink: null, saveService: bag.SaveService);
                Assert.That(router.IsSaveWired, Is.True, "[前置] 落笔半边须已接线");
                uint expected = bag.SaveService.Ledger.NextWritableSeq;

                Assert.That(router.TrySave(), Is.True, "「留档」须受理");

                Assert.That(bag.SaveService.WriteQueue.WaitForIdle(5000), Is.True,
                    "写队列 5s 未排空 —— 手动存档卡在 worker 上");
                Assert.That(bag.SaveService.WriteQueue.LastFailure, Is.Null,
                    $"worker 写盘失败(LastFailure):{bag.SaveService.WriteQueue.LastFailure}");

                Assert.That(File.Exists(Path.Combine(_dir, $"save_{expected}.bin")), Is.True,
                    $"手动存档未落盘;盘上:[{string.Join(", ", FilesOnDisk(_dir))}]");
                Assert.DoesNotThrow(() => bag.SaveService.Load(new SaveSlot(expected)),
                    "手动档必须读得回来");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 退出:袋里的 SaveService.SaveOnExit ⇒ `save_exit.bin`
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_assembly_exitSave_writesExitFile()
        {
            using (CompositionRootServices bag = AssembleBag())
            {
                bag.SaveService.SaveOnExit();

                Assert.That(File.Exists(Path.Combine(_dir, SaveService.ExitSaveFileName)), Is.True,
                    $"退出存档未落盘;盘上:[{string.Join(", ", FilesOnDisk(_dir))}]");
                Assert.DoesNotThrow(() => bag.SaveService.Load(SaveService.ExitSaveSlot),
                    "退出存档必须读得回来(主菜单「继续诊治」的来源之一)");
            }
        }
    }
}
