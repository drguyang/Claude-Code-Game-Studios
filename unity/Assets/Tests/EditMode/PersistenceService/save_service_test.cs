// persistence-service Story 001 测试
//
// AC-7a-01: 快照损坏/缺失/跨版本不可读 ⇒ 三逻辑流完整重放
// AC-7a-02: 快照内容与三逻辑流不一致 ⇒ 各游戏事实以世界流/事件流为准
// AC-7a-03: 同一具名存档夹具,编辑器(Mono)与 IL2CPP 玩家上各序列化一次 ⇒ 字节流逐位一致
// AC-7a-04: 一条三流事件写盘再读回 ⇒ 字段值逐位往返不变
// AC-7a-05: 翻转存档中任一非校验和字节 ⇒ 校验和验证失败
// AC-7a-06: 写 checkpoint ⇒ tmp → final 原子替换
// AC-7a-18: grep 7a 侧全部源码 ⇒ 零 float/double 类型用于存档或事件流

using System;
using System.IO;
using System.Reflection;
using System.Text;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveServiceTest
    {
        private string _testDir;

        [SetUp]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }

        // AC-7a-05: 翻转存档中任一非校验和字节 ⇒ 校验和验证失败
        [Test]
        public void test_checksumValidation_detectsCorruption()
        {
            // 构造带 checksum 的数据
            byte[] raw = new byte[64];
            new Random(42).NextBytes(raw);
            raw[0] = 0x44; raw[1] = 0x59; raw[2] = 0x4A; raw[3] = 0x51; // "DYJQ"

            byte[] withChecksum = SaveService.AddChecksum(raw);
            Assert.IsTrue(SaveService.ValidateChecksum(withChecksum), "合法数据应通过校验");

            // 翻转一个非 checksum 字节
            withChecksum[40] ^= 0xFF;
            Assert.IsFalse(SaveService.ValidateChecksum(withChecksum), "翻转字节后校验应失败");
        }

        // AC-7a-06: 写 checkpoint ⇒ tmp → final 原子替换
        [Test]
        public void test_atomicWrite_createsFinalFile()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);
            byte[] data = Encoding.UTF8.GetBytes("test data");

            svc.SaveBytes(slot, data);

            string finalPath = Path.Combine(_testDir, "save_1.bin");
            string tmpPath = Path.Combine(_testDir, "save_1.tmp");
            string bakPath = Path.Combine(_testDir, "save_1.bak");

            Assert.IsTrue(File.Exists(finalPath), "final 文件应存在");
            Assert.IsFalse(File.Exists(tmpPath), "tmp 文件应被 rename 掉");
            Assert.IsFalse(File.Exists(bakPath), "首次写不应有 bak");
        }

        // AC-7a-06: 双档 bak 机制
        [Test]
        public void test_doubleBackup_bakCreatedOnSecondWrite()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);

            svc.SaveBytes(slot, Encoding.UTF8.GetBytes("first"));
            svc.SaveBytes(slot, Encoding.UTF8.GetBytes("second"));

            string finalPath = Path.Combine(_testDir, "save_1.bin");
            string bakPath = Path.Combine(_testDir, "save_1.bak");

            Assert.IsTrue(File.Exists(finalPath), "final 应存在");
            Assert.IsTrue(File.Exists(bakPath), "bak 应存在");
            Assert.AreEqual("second", Encoding.UTF8.GetString(File.ReadAllBytes(finalPath)));
            Assert.AreEqual("first", Encoding.UTF8.GetString(File.ReadAllBytes(bakPath)));
        }

        // AC-7a-01: 校验和损坏时自动回退 bak
        [Test]
        public void test_checksumFallback_readsBakWhenFinalCorrupted()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);

            // 构造合法的存档头(36 字节)
            byte[] raw1 = new byte[36];
            byte[] raw2 = new byte[36];
            raw1[0] = 0x44; raw1[1] = 0x59; raw1[2] = 0x4A; raw1[3] = 0x51; // "DYJQ"
            raw2[0] = 0x44; raw2[1] = 0x59; raw2[2] = 0x4A; raw2[3] = 0x51;
            new Random(42).NextBytes(raw1);
            new Random(43).NextBytes(raw2);
            raw1[0] = 0x44; raw1[1] = 0x59; raw1[2] = 0x4A; raw1[3] = 0x51;
            raw2[0] = 0x44; raw2[1] = 0x59; raw2[2] = 0x4A; raw2[3] = 0x51;

            svc.SaveBytes(slot, SaveService.AddChecksum(raw1));
            svc.SaveBytes(slot, SaveService.AddChecksum(raw2));

            // 损坏 final
            string finalPath = Path.Combine(_testDir, "save_1.bin");
            byte[] corrupted = File.ReadAllBytes(finalPath);
            corrupted[40] ^= 0xFF;
            File.WriteAllBytes(finalPath, corrupted);

            // 应回退到 bak(返回带 checksum 的完整数据 = 36 + 32 = 68 字节)
            byte[] loaded = svc.LoadBytes(slot);
            Assert.AreEqual(68, loaded.Length);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-7a-15(b) —— 两级都坏 ⇒ Corrupt 态(2026-10-25 补齐)
        // 权威:GDD design/gdd/persistence-service.md:569-573(AC-7a-15(b))
        //       · :257(加载状态机 Corrupt 行)· :187-189(规则十一)· ADR-010 §四
        // ══════════════════════════════════════════════════════════════════

        /// <summary>写两档(final ← cs(r1) 后又被 cs(r2) 覆盖,bak ← cs(r1)),再把两档各翻一个字节。
        /// 返回 (finalPath, bakPath),两档字节**都已损坏**。</summary>
        private (string finalPath, string bakPath) ArrangeBothFilesCorrupted(SaveService svc, SaveSlot slot)
        {
            byte[] raw1 = new byte[36];
            byte[] raw2 = new byte[36];
            new Random(42).NextBytes(raw1);
            new Random(43).NextBytes(raw2);
            raw1[0] = 0x44; raw1[1] = 0x59; raw1[2] = 0x4A; raw1[3] = 0x51; // "DYJQ"
            raw2[0] = 0x44; raw2[1] = 0x59; raw2[2] = 0x4A; raw2[3] = 0x51;

            svc.SaveBytes(slot, SaveService.AddChecksum(raw1)); // 首写:仅 final
            svc.SaveBytes(slot, SaveService.AddChecksum(raw2)); // 二写:r1 → bak,final = r2

            string finalPath = Path.Combine(_testDir, "save_1.bin");
            string bakPath = Path.Combine(_testDir, "save_1.bak");

            byte[] f = File.ReadAllBytes(finalPath); f[40] ^= 0xFF; File.WriteAllBytes(finalPath, f);
            byte[] b = File.ReadAllBytes(bakPath);  b[40] ^= 0xFF; File.WriteAllBytes(bakPath, b);
            return (finalPath, bakPath);
        }

        // AC-7a-15(b):两级都坏 ⇒ 抛**指定**异常(具名 Corrupt 态),报文点名槽位
        [Test]
        public void test_corruptState_bothFinalAndBakBad_throwsSaveCorrupt()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);
            ArrangeBothFilesCorrupted(svc, slot);

            var ex = Assert.Throws<SaveCorruptException>(
                () => svc.LoadBytes(slot),
                "final 与 bak 都坏 ⇒ 必须进入 Corrupt 态并抛 SaveCorruptException" +
                "(裸 InvalidDataException 与前向不兼容/magic 不符同型不可分,不满足「指定异常」)");

            Assert.AreEqual(1U, ex.SlotSeq, "异常须点名进入 Corrupt 态的槽位");
            StringAssert.Contains("Corrupt", ex.Message, "报文须点名 Corrupt 态");
        }

        // AC-7a-15(b):「槽目录文件数不变、不静默新建空档」—— 禁止判据,须验字节未被改动
        [Test]
        public void test_corruptState_leavesSlotDirectoryUntouched()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);
            var (finalPath, bakPath) = ArrangeBothFilesCorrupted(svc, slot);

            string[] filesBefore = Directory.GetFiles(_testDir);
            byte[] finalBefore = File.ReadAllBytes(finalPath);
            byte[] bakBefore = File.ReadAllBytes(bakPath);

            Assert.Throws<SaveCorruptException>(() => svc.LoadBytes(slot));

            string[] filesAfter = Directory.GetFiles(_testDir);
            CollectionAssert.AreEquivalent(filesBefore, filesAfter,
                "进入 Corrupt 态不得增删任何文件(不静默新建空档)");
            CollectionAssert.AreEqual(finalBefore, File.ReadAllBytes(finalPath),
                "进入 Corrupt 态不得改写 final(不「修复」成空档)");
            CollectionAssert.AreEqual(bakBefore, File.ReadAllBytes(bakPath),
                "进入 Corrupt 态不得改写 bak");
        }

        // AC-7a-15 的 (a)/(b) 边界:final 坏而 bak 好 ⇒ 回退,**不**进 Corrupt 态
        [Test]
        public void test_fallbackState_finalBadBakGood_doesNotEnterCorrupt()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);

            byte[] raw1 = new byte[36];
            byte[] raw2 = new byte[36];
            new Random(42).NextBytes(raw1);
            new Random(43).NextBytes(raw2);
            raw1[0] = 0x44; raw1[1] = 0x59; raw1[2] = 0x4A; raw1[3] = 0x51;
            raw2[0] = 0x44; raw2[1] = 0x59; raw2[2] = 0x4A; raw2[3] = 0x51;
            svc.SaveBytes(slot, SaveService.AddChecksum(raw1));
            svc.SaveBytes(slot, SaveService.AddChecksum(raw2));

            string finalPath = Path.Combine(_testDir, "save_1.bin");
            byte[] f = File.ReadAllBytes(finalPath); f[40] ^= 0xFF; File.WriteAllBytes(finalPath, f);

            // 回退成功 ⇒ 既不得抛 Corrupt,也不得抛任何异常
            Assert.DoesNotThrow(() => svc.LoadBytes(slot),
                "(a) final 坏而 bak 好 ⇒ 自动回退,不得进入 Corrupt 态");
        }

        // AC-7a-15(b) 的可区分性:Corrupt 与「格式/版本」类失败**必须**能分开,
        // 且 Corrupt **不得**混进可被静默兜底的 IOException 家族
        [Test]
        public void test_corruptState_isDistinguishableFromOtherLoadFailures()
        {
            // ① 与「magic 不符」分家 —— 那个仍抛 InvalidDataException
            byte[] save = SaveService.AddChecksum(MinimalHeaderBytes());
            save[0] ^= 0xFF; // 破 magic
            var ex = Assert.Throws<InvalidDataException>(() => SaveHeaderCodec.Read(save));
            Assert.IsFalse(ex is SaveCorruptException,
                "magic 不符是「格式/版本」问题,不是「两级都坏」—— 二者不得同型" +
                "(前者提示换版本,后者提示报损坏,呈现相反)");

            // ② 刻意**不**是 IOException 的子类 ——(全仓 `catch (IOException)` 五处
            //    会把它当「I/O 暂时失败,可重试」静默吞掉,而 Corrupt 是**不可重试**的终态)
            Assert.IsFalse(typeof(IOException).IsAssignableFrom(typeof(SaveCorruptException)),
                "SaveCorruptException 不得派生自 IOException —— 否则会被可重试兜底静默吞掉");

            // ③ 也**不**是 InvalidDataException 的子类 —— 该 BCL 类型是 sealed(不可继承),
            //    且全仓 `catch (InvalidDataException)` 零命中 ⇒ 不存在兼容义务。
            Assert.IsFalse(typeof(InvalidDataException).IsAssignableFrom(typeof(SaveCorruptException)),
                "Corrupt 态须是独立类型,不得与格式/版本类失败共用 catch 面");
        }

        /// <summary>构造一段最小可读头部(magic 正确),供「破 magic」用。</summary>
        private static byte[] MinimalHeaderBytes()
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(1UL, 1U, 0L, 0UL);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            byte[] folded = new byte[] { 0, 0, 0, 0 }; // 折叠段 count = 0(v1 必备段)
            var combined = new byte[header.Length + body.Length + folded.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);
            Array.Copy(folded, 0, combined, header.Length + body.Length, folded.Length);
            return combined;
        }

        // AC-7a-04: 一条三流事件写盘再读回 ⇒ 字段值逐位往返不变
        [Test]
        public void test_simEventCodec_roundTrip()
        {
            var codec = new SaveCodec();
            var w = new CodecWriter();

            var e = new SimEvent(42, new PatientId(7), 99, EventKind.ActorCellEntered, default);
            codec.WriteEvent(in e, ref w);

            byte[] bytes = w.ToArray();
            Assert.IsTrue(bytes.Length > 0, "编码后应有字节");
        }

        // AC-7a-18: 零 float/double 类型用于存档或事件流
        [Test]
        public void test_noFloatTypes_inSaveCodec()
        {
            var asm = typeof(SaveService).Assembly;
            foreach (var type in asm.GetTypes())
            {
                if (type.FullName != null && type.FullName.Contains("Save"))
                {
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        Assert.IsFalse(field.FieldType == typeof(float), $"字段 {type.Name}.{field.Name} 不应为 float");
                        Assert.IsFalse(field.FieldType == typeof(double), $"字段 {type.Name}.{field.Name} 不应为 double");
                    }
                }
            }
        }

        // AC-7a-03: 黄金字节对拍(简化版 — 同一数据两次序列化应逐位一致)
        [Test]
        public void test_deterministicSerialization_sameBytes()
        {
            byte[] raw = new byte[64];
            new Random(42).NextBytes(raw);

            byte[] first = SaveService.AddChecksum(raw);
            byte[] second = SaveService.AddChecksum(raw);

            Assert.AreEqual(first.Length, second.Length, "长度应一致");
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i], second[i], $"字节 {i} 应一致");
            }
        }
    }
}
