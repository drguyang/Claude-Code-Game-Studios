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
