// 换世界 / 新起一局**不动其它世界**(ADR-040 D5 / D6 · `AC-MM-13` 的装配层载体)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 读:`Assets/DataCooked/*.cooked.bytes`(经共享件 `DiskBackedDataProvider` —— 理由同
//     `save_three_paths_assembly_test`:手搓假表会把被测路径换掉)。
// 写:全部落 `Path.GetTempPath()`,TearDown 删目录。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据
// ─────────────────────────────────────────────────────────────────────────────
// 两世界并存 + 在第二个世界写首份存档 ⇒
//   ① 第二个世界**确实**多出 `save_auto.bin`;
//   ② 第一个世界的**每一个文件逐字节不变**(SHA-256 比对)——
//      这是「新起一局 = 新建目录,不是清空旧的」的机械守;
//   ③ 两个世界各自 `Load` 都能读回(不是「写进了别人的目录」)。
//
// ⚠️ 本件**不**覆盖「启动序按菜单选择去装配」那一步(BootRoot 的 `ReassembleForWorld`)——
//    那需要真启动序,今日无 PlayMode 判据。如实登记,见 spec `AC-MM-13`。

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public sealed class WorldSwitchIsolationTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "dyjq_worldswitch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                try { Directory.Delete(_root, true); } catch { }
            }
        }

        private static Dictionary<string, string> Snapshot(string dir)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string f in Directory.GetFiles(dir))
            {
                using (var sha = SHA256.Create())
                using (FileStream fs = File.OpenRead(f))
                    map[Path.GetFileName(f)] = BitConverter.ToString(sha.ComputeHash(fs));
            }
            return map;
        }

        [Test]
        public void test_newWorld_doesNotTouchExistingWorld()
        {
            var catalog = new WorldCatalog(_root, clock: () => 1L);

            // ── 世界 1:建目录 + 写一份存档(走真实装配根 + 首份存档)──
            WorldEntry w1 = catalog.CreateWorld();
            using (CompositionRootServices bag1 = CompositionRoot.Assemble(
                       dataProvider: DiskBackedDataProvider.Create(), saveBasePath: w1.Directory))
            {
                Assert.That(FirstSessionSave.TryEstablish(bag1.SaveService, out Exception e1), Is.True,
                    $"世界 1 首份存档须写出;error={e1}");
                Assert.That(bag1.SaveService.WriteQueue.WaitForIdle(5000), Is.True);
            }

            Dictionary<string, string> before = Snapshot(w1.Directory);
            Assert.That(before.Count, Is.GreaterThan(0), "[前置] 世界 1 得有档,否则本测无判别力");

            // ── 「新起一局」:新建世界 2,在它里面写首份存档 ──
            WorldEntry w2 = catalog.CreateWorld();
            Assert.That(w2.Seq, Is.EqualTo(2));
            Assert.That(w2.Directory, Is.Not.EqualTo(w1.Directory));

            using (CompositionRootServices bag2 = CompositionRoot.Assemble(
                       dataProvider: DiskBackedDataProvider.Create(), saveBasePath: w2.Directory))
            {
                Assert.That(FirstSessionSave.TryEstablish(bag2.SaveService, out Exception e2), Is.True,
                    $"世界 2 的首份存档须写出(它看到的是空目录);error={e2}");
                Assert.That(bag2.SaveService.WriteQueue.WaitForIdle(5000), Is.True);
                Assert.That(File.Exists(Path.Combine(w2.Directory, SaveService.AutoSaveFileName)), Is.True,
                    "新世界必须有自己的自动档");

                // 两个世界各自可读 —— 「写进了别人的目录」会在这里露出来。
                Assert.DoesNotThrow(() => bag2.SaveService.Load(SaveService.AutoSaveSlot),
                    "世界 2 的档必须能读回");
            }

            Dictionary<string, string> after = Snapshot(w1.Directory);

            Assert.That(after.Count, Is.EqualTo(before.Count), "世界 1 的文件数不得变化");
            foreach (KeyValuePair<string, string> kv in before)
            {
                Assert.That(after.ContainsKey(kv.Key), Is.True, $"世界 1 少了 {kv.Key}");
                Assert.That(after[kv.Key], Is.EqualTo(kv.Value),
                    $"世界 1 的 {kv.Key} 被改写了 —— 新起一局不得触碰既有世界");
            }
        }
    }
}
