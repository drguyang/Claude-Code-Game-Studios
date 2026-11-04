// 「第一份存档」夹具(2026-11-04 · 用户裁定「只做首份存档(A)」)。
//
// 被测 = `Gameplay.Boot/FirstSessionSave.TryEstablish`(启动序第 8b 步的决策体)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读:`unity/Assets/DataCooked/*.cooked.bytes`(经 `DiskBackedDataProvider` —— 生产同款字节,
//     走的是**真的**组版闭包:`ConfigVersion` 快照 + 时间七值)。理由同
//     `save_three_paths_assembly_test` 的头注:手搓假表会把被测路径换掉。
// 写:全部落 `Path.GetTempPath()`,TearDown 删目录。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉的四件事
// ─────────────────────────────────────────────────────────────────────────────
// ① **空档开局 ⇒ 真的写出一份自动档**,且读得回来、`TryGetMostRecentSave` 从此说得出。
// ② **盘上已有档 ⇒ 一份字节都不动**。这是 `BootRoot.TryContinueFromSave` 的既有约束
//    (「新局不写任何存档文件 ⇒ 不覆盖既有档」)在新增写点上的**机械守**。
// ③ **未装配 7a ⇒ 不算失败**(合法形态),不抛、不报错。
// ④ **判据取自磁盘现况,不取自调用方传进来的「我以为没档」** —— ② 就是它的判别力:
//    若实现改成「信任调用方的入参」,② 立刻转红(那份手动档会被覆盖)。

using System;
using System.IO;
using System.Security.Cryptography;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public sealed class FirstSessionSaveTest
    {
        private string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_firstsave_" + Guid.NewGuid().ToString("N"));
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

        private CompositionRootServices AssembleBag()
            => CompositionRoot.Assemble(
                dataProvider: DiskBackedDataProvider.Create(), saveBasePath: _dir);

        private static string Sha256Of(string path)
        {
            using (var sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs));
        }

        private string[] FilesOnDisk()
        {
            string[] files = Directory.GetFiles(_dir);
            for (int i = 0; i < files.Length; i++) files[i] = Path.GetFileName(files[i]);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 空档开局 ⇒ 首份自动档
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_emptyProfile_writesFirstAutoSave()
        {
            using (CompositionRootServices bag = AssembleBag())
            {
                // 正控前置:确实一个档都没有(否则下面的「写了一份」可能是既有档)。
                Assert.That(bag.SaveService.TryGetMostRecentSave(out _), Is.False,
                    $"[前置] 空目录不该有档;盘上:[{string.Join(", ", FilesOnDisk())}]");

                Assert.That(FirstSessionSave.TryEstablish(bag.SaveService, out Exception error), Is.True,
                    $"首次开局必须写出初始档;error={error}");
                Assert.That(error, Is.Null);

                // `Checkpoint` 异步 ⇒ 先排空再断言(同 `save_three_paths_assembly_test` 的教训)。
                Assert.That(bag.SaveService.WriteQueue.WaitForIdle(5000), Is.True,
                    "写队列 5s 未排空 —— 初始档卡在 worker 上");
                Assert.That(bag.SaveService.WriteQueue.LastFailure, Is.Null,
                    $"worker 写盘失败:{bag.SaveService.WriteQueue.LastFailure}");

                Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.True,
                    $"初始档须落自动槽;盘上:[{string.Join(", ", FilesOnDisk())}]");

                // 落而可读 —— 写得进读不出等于没写。
                Assert.DoesNotThrow(() => bag.SaveService.Load(SaveService.AutoSaveSlot),
                    "初始档必须读得回来");

                // 从此「继续诊治」有东西可继续(主菜单的 hasSave 来源)。
                Assert.That(bag.SaveService.TryGetMostRecentSave(out SaveSlot recent), Is.True,
                    "写完之后主菜单的「有没有档」必须变成 true");
                Assert.That(recent.SlotSeq, Is.EqualTo(SaveService.AutoSaveSlot.SlotSeq));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 已有档 ⇒ 不写、且一个字节都不动
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_existingSave_isNeverOverwritten()
        {
            using (CompositionRootServices bag = AssembleBag())
            {
                // 造一份玩家手动档(真实入口:留档路由器)。
                var router = new SaveSlotCommandRouter(
                    sequencer: null, failureSink: null, saveService: bag.SaveService);
                Assert.That(router.TrySave(), Is.True, "[前置] 手动档须受理");
                Assert.That(bag.SaveService.WriteQueue.WaitForIdle(5000), Is.True);
                Assert.That(router.LastSavedSlot.HasValue, Is.True, "[前置] 须有槽号");

                string manual = Path.Combine(_dir, $"save_{router.LastSavedSlot.Value}.bin");
                Assert.That(File.Exists(manual), Is.True,
                    $"[前置] 手动档须已落盘;盘上:[{string.Join(", ", FilesOnDisk())}]");
                string before = Sha256Of(manual);

                Assert.That(FirstSessionSave.TryEstablish(bag.SaveService, out Exception error), Is.False,
                    "已有档时必须拒绝写初始档");
                Assert.That(error, Is.Null, "「已有档」不是失败,不该抛出/报错");

                bag.SaveService.WriteQueue.WaitForIdle(5000);

                Assert.That(Sha256Of(manual), Is.EqualTo(before),
                    "既有档被改写了 —— 这正是本守卫要防的形态(读档失败回落新局后再存一次会毁掉玩家唯一的档)");
                Assert.That(File.Exists(Path.Combine(_dir, SaveService.AutoSaveFileName)), Is.False,
                    $"不该凭空多出一份自动档;盘上:[{string.Join(", ", FilesOnDisk())}]");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 未装配 7a ⇒ 合法形态,不算失败
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_nullService_isNotAnError()
        {
            Assert.That(FirstSessionSave.TryEstablish(null, out Exception error), Is.False);
            Assert.That(error, Is.Null,
                "`saveBasePath == null` 是合法的「未装配存档」形态(见 `CompositionRoot.Assemble` 注)," +
                "不该被报到装配层的告警面上");
        }
    }
}
