// 多世界存档的目录与索引(ADR-040 D1/D2/D3/D7 的兑现件)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件 / 写哪里
// ─────────────────────────────────────────────────────────────────────────────
// 不读仓库内文件(纯 BCL 目录操作,不依赖烘焙产物)。
// 写:全部落 `Path.GetTempPath()`,TearDown 删目录(FsWriteBoundaryGate 允许形态)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉的六件事
// ─────────────────────────────────────────────────────────────────────────────
// ① 旧形态(存档直接躺在根目录)一次搬进 `w1/`,**字节零改动**、无关文件不动;
// ② 迁移幂等(第二次 = 0,不重复搬);
// ③ 「最近世界」按**最后游玩时刻**降序 —— 且**不是**按序号(否则本测与「按 seq 排」同构);
// ④ **目录是权威**:索引里有、目录不在 ⇒ 不列;目录在、索引里没有 ⇒ 照列(时刻 0);
// ⑤ 索引损坏 ⇒ 世界**不会消失**(以目录为准),且下一次写会重建出合法索引;
// ⑥ 新建世界分配下一个序号。

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using DaYiJingCheng.Gameplay.Boot;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public sealed class WorldCatalogTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "dyjq_worlds_" + Guid.NewGuid().ToString("N"));
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

        private static string Sha256Of(string path)
        {
            using (var sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs));
        }

        private string WriteRootFile(string name, byte[] content)
        {
            string path = Path.Combine(_root, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 一次性迁移
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_migrateLegacy_movesRootSavesIntoW1_byteIdentical()
        {
            byte[] saveBytes = { 1, 2, 3, 4, 5 };
            byte[] exitBytes = { 9, 9, 9 };
            byte[] flagBytes = { 0x01 };
            string save = WriteRootFile("save_3.bin", saveBytes);
            string exit = WriteRootFile("save_exit.bin", exitBytes);
            string flag = WriteRootFile("session_incomplete.flag", flagBytes);
            string other = WriteRootFile("readme.txt", new byte[] { 42 });

            string beforeSave = Sha256Of(save);
            string beforeExit = Sha256Of(exit);

            var catalog = new WorldCatalog(_root);
            int moved = catalog.MigrateLegacy();

            Assert.That(moved, Is.EqualTo(3), "三个存档面文件都该搬(readme 不算)");
            string w1 = catalog.DirectoryOf(1);
            Assert.That(File.Exists(Path.Combine(w1, "save_3.bin")), Is.True);
            Assert.That(File.Exists(Path.Combine(w1, "save_exit.bin")), Is.True);
            Assert.That(File.Exists(Path.Combine(w1, "session_incomplete.flag")), Is.True);

            // **字节零改动** —— 迁移不是格式转换。
            Assert.That(Sha256Of(Path.Combine(w1, "save_3.bin")), Is.EqualTo(beforeSave));
            Assert.That(Sha256Of(Path.Combine(w1, "save_exit.bin")), Is.EqualTo(beforeExit));

            // 根目录只剩无关文件;无关文件**不许**被搬走。
            Assert.That(File.Exists(save), Is.False, "旧位置的档必须搬走(否则会被下次启动读成「根目录仍有档」)");
            Assert.That(File.Exists(other), Is.True, "`readme.txt` 不是存档面文件,不该被搬");
        }

        [Test]
        public void test_migrateLegacy_isIdempotent()
        {
            WriteRootFile("save_3.bin", new byte[] { 1 });
            var catalog = new WorldCatalog(_root);

            Assert.That(catalog.MigrateLegacy(), Is.EqualTo(1));
            Assert.That(catalog.MigrateLegacy(), Is.EqualTo(0), "第二次必须什么都不做");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 「最近世界」按时刻排,不是按序号
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_listWorlds_sortedByLastPlayed_notBySeq()
        {
            var catalog = new WorldCatalog(_root);
            // 造三个世界目录,序号与时刻**刻意反向**:w1 最新、w3 最旧。
            Directory.CreateDirectory(catalog.DirectoryOf(1));
            Directory.CreateDirectory(catalog.DirectoryOf(2));
            Directory.CreateDirectory(catalog.DirectoryOf(3));
            catalog.Touch(1, 300L);
            catalog.Touch(2, 200L);
            catalog.Touch(3, 100L);

            IReadOnlyList<WorldEntry> worlds = catalog.ListWorlds();

            Assert.That(worlds.Count, Is.EqualTo(3));
            Assert.That(worlds[0].Seq, Is.EqualTo(1), "时刻 300 的最新");
            Assert.That(worlds[1].Seq, Is.EqualTo(2));
            Assert.That(worlds[2].Seq, Is.EqualTo(3));
            Assert.That(catalog.TryGetMostRecent(out WorldEntry recent), Is.True);
            Assert.That(recent.Seq, Is.EqualTo(1));
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 目录是权威
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_listWorlds_directoryIsAuthoritative()
        {
            var catalog = new WorldCatalog(_root);
            Directory.CreateDirectory(catalog.DirectoryOf(2));   // 有目录、无索引
            catalog.Touch(9, 500L);                              // 有索引、无目录

            IReadOnlyList<WorldEntry> worlds = catalog.ListWorlds();

            Assert.That(worlds.Count, Is.EqualTo(1), "索引里的孤儿条目不列(世界已被删)");
            Assert.That(worlds[0].Seq, Is.EqualTo(2), "目录在 ⇒ 照列,哪怕索引里没有");
            Assert.That(worlds[0].LastPlayedUtcTicks, Is.EqualTo(0L), "时刻未知 ⇒ 0,不是「最旧被丢掉」");
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑤ 索引损坏 ⇒ 世界不消失,且下次写会重建
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_corruptIndex_worldsSurvive_andNextWriteRebuilds()
        {
            var catalog = new WorldCatalog(_root);
            Directory.CreateDirectory(catalog.DirectoryOf(1));
            File.WriteAllBytes(Path.Combine(_root, WorldCatalog.IndexFileName), new byte[] { 1, 2, 3 });

            Assert.That(catalog.ListWorlds().Count, Is.EqualTo(1), "索引坏了不代表世界没了");

            catalog.Touch(1, 777L);
            long len = new FileInfo(Path.Combine(_root, WorldCatalog.IndexFileName)).Length;
            Assert.That(len % WorldCatalog.IndexRecordSize, Is.EqualTo(0), "重建后必须是合法记录长度");
            Assert.That(catalog.ListWorlds()[0].LastPlayedUtcTicks, Is.EqualTo(777L));
        }

        // ══════════════════════════════════════════════════════════════════
        // ⑥ 新建世界
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_createWorld_allocatesNextSeq_andBecomesMostRecent()
        {
            var catalog = new WorldCatalog(_root, clock: () => 1234L);
            Directory.CreateDirectory(catalog.DirectoryOf(1));
            Directory.CreateDirectory(catalog.DirectoryOf(2));

            WorldEntry created = catalog.CreateWorld();

            Assert.That(created.Seq, Is.EqualTo(3), "下一个序号");
            Assert.That(Directory.Exists(created.Directory), Is.True);
            Assert.That(catalog.ListWorlds().Count, Is.EqualTo(3));
            Assert.That(catalog.ListWorlds()[0].Seq, Is.EqualTo(3), "刚建的世界时刻最新");
        }

        [Test]
        public void test_emptyRoot_hasNoWorld()
        {
            var catalog = new WorldCatalog(_root);
            Assert.That(catalog.TryGetMostRecent(out _), Is.False);
            Assert.That(catalog.ListWorlds().Count, Is.EqualTo(0));
        }

        // ══════════════════════════════════════════════════════════════════
        // 目录名解析
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_parseWorldDir()
        {
            Assert.That(WorldCatalog.TryParseWorldDir("w1", out int a), Is.True);
            Assert.That(a, Is.EqualTo(1));
            Assert.That(WorldCatalog.TryParseWorldDir("w42", out int b), Is.True);
            Assert.That(b, Is.EqualTo(42));

            Assert.That(WorldCatalog.TryParseWorldDir("w", out _), Is.False);
            Assert.That(WorldCatalog.TryParseWorldDir("w0", out _), Is.False, "序号从 1 起");
            Assert.That(WorldCatalog.TryParseWorldDir("w-1", out _), Is.False);
            Assert.That(WorldCatalog.TryParseWorldDir("save_1", out _), Is.False);
            Assert.That(WorldCatalog.TryParseWorldDir("W1", out _), Is.False, "大小写敏感(目录名由本类造)");
        }
    }
}
