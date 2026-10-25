// AC-7a-14 —— 迁移链(逐版本 + 两不变量)的夹具。
//
// 权威来源:
//   GDD `persistence-service.md:563-567` —— AC-7a-14 全文
//   GDD `:181-185` 规则十
//   ADR-010 §七 —— 迁移协议(逐版本显式脚本 / WorldSeed 与已发生事件原样)
//   ADR-007 §二 —— WorldSeed 被改 ⇒ 全案病程与事件流同时作废
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 本文件用的是**测试内定义的假迁移步**,不是真实历史版本
// ─────────────────────────────────────────────────────────────────────────────
// 全库 `SaveHeader.CurrentSaveVersion = 1` 且零 `v0` 写入者 ⇒ **不存在真实的历史版本
// 可以迁**。若为此造一个「假 v0 布局再迁它」,测的是夹具自己的想象力,不是产品行为。
//
// 本文件测的是**框架的四件事**,它们与「历史上有哪些版本」无关:
//   ① 逐版本驱动(每级都跑到、顺序对、断链必抛)
//   ② `WorldSeed` 逐位不变(每级断言)
//   ③ 已发生事件逐位不变(每级断言,按**末尾等长**取段 ⇒ 头部长度变化不干扰)
//   ④ 脚本自身的三类缺陷须被拦住:没升版本号 / 改了 seed / 改了正文
//
// ⇒ 判据面是「**框架会不会放过一个坏迁移**」,不是「v0→v1 迁得对不对」。
//   后者要等 `CurrentSaveVersion` 第一次升到 2 时,拿**真实存在过的 v1 存档**来测。
//
// 写盘边界:本文件零写盘(纯内存)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveMigrationChainTest
    {
        private const int HeaderFixed = SaveHeaderCodec.FixedSize;
        private const int VersionOffset = SaveHeaderCodec.SaveVersionOffsetFixed;
        private const int SeedOffset = SaveHeaderCodec.WorldSeedOffsetFixed;

        // ══════════════════════════════════════════════════════════════════
        // 夹具工厂
        // ══════════════════════════════════════════════════════════════════

        /// <summary>造一份「不含校验和」的存档:指定版本 / seed / 正文。</summary>
        private static byte[] Save(uint version, ulong worldSeed, byte[] body = null)
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(worldSeed, 7U, 100L, 0UL);
            SetVersion(header, version);
            body = body ?? Array.Empty<byte>();
            var combined = new byte[header.Length + body.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);
            return combined;
        }

        private static void SetVersion(byte[] data, uint version)
        {
            data[VersionOffset + 0] = (byte)(version & 0xFF);
            data[VersionOffset + 1] = (byte)((version >> 8) & 0xFF);
            data[VersionOffset + 2] = (byte)((version >> 16) & 0xFF);
            data[VersionOffset + 3] = (byte)((version >> 24) & 0xFF);
        }

        /// <summary>
        /// 可配置的假迁移步:在**头部定长字段之后、正文之前**插入一段填充(默认 4 字节),
        /// 并把版本号写成 `FromVersion + 1`。
        /// <para>为什么要**改变头部长度**:真实迁移的典型动作就是加字段。若夹具只用等长变换,
        /// 「事件段按末尾等长取」这条设计就没被验证过 —— 而它正是本框架跨版本能用的前提。</para>
        /// </summary>
        private sealed class FakeMigration : ISaveMigration
        {
            public uint FromVersion { get; }
            private readonly int _padBytes;
            private readonly Action<byte[]> _tamper;
            private readonly bool _advanceVersion;
            private readonly List<uint> _calls;

            public FakeMigration(uint fromVersion, List<uint> calls = null,
                                 int padBytes = 4, Action<byte[]> tamper = null,
                                 bool advanceVersion = true)
            {
                FromVersion = fromVersion;
                _padBytes = padBytes;
                _tamper = tamper;
                _advanceVersion = advanceVersion;
                _calls = calls;
            }

            public byte[] Migrate(byte[] dataWithoutChecksum)
            {
                _calls?.Add(FromVersion);

                // 头部定长部分原样 + 新填充 + 正文原样(正文 = 末尾 data.Length - HeaderFixed 字节)
                int bodyLen = dataWithoutChecksum.Length - HeaderFixed;
                var result = new byte[dataWithoutChecksum.Length + _padBytes];
                Array.Copy(dataWithoutChecksum, 0, result, 0, HeaderFixed);
                for (int i = 0; i < _padBytes; i++) result[HeaderFixed + i] = (byte)(0xA0 + i);
                Array.Copy(dataWithoutChecksum, HeaderFixed, result, HeaderFixed + _padBytes, bodyLen);

                if (_advanceVersion) SetVersion(result, FromVersion + 1);
                _tamper?.Invoke(result);
                return result;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // ① 逐版本驱动
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_migrate_sameVersion_returnsIdenticalBytes()
        {
            byte[] input = Save(3U, 0xDEADBEEFCAFEBABEUL, new byte[] { 1, 2, 3 });
            byte[] output = SaveMigrationChain.Migrate(input, 3U, Array.Empty<ISaveMigration>());

            CollectionAssert.AreEqual(input, output, "同版本 ⇒ 直通,字节逐位相同");
            Assert.AreNotSame(input, output, "须是副本 —— 调用方改返回值不得污染入参");
        }

        [Test]
        public void test_migrate_multiStep_runsEveryVersionInOrder()
        {
            var calls = new List<uint>();
            var chain = new List<ISaveMigration>
            {
                new FakeMigration(2U, calls),
                new FakeMigration(0U, calls),
                new FakeMigration(1U, calls),   // 注册顺序打乱 —— 链须按 FromVersion 查表,不按注册序
            };

            byte[] output = SaveMigrationChain.Migrate(Save(0U, 42UL), 3U, chain);

            CollectionAssert.AreEqual(new uint[] { 0U, 1U, 2U }, calls,
                "须从 0 逐级升满到 3,且按版本顺序执行(注册序无关)");
            Assert.AreEqual(3U, SaveMigrationChain.ReadSaveVersion(output), "终态版本 = 目标版本");
        }

        [Test]
        public void test_migrate_brokenChain_throwsNamingMissingVersion()
        {
            var chain = new List<ISaveMigration> { new FakeMigration(0U) };

            var ex = Assert.Throws<InvalidOperationException>(
                () => SaveMigrationChain.Migrate(Save(0U, 42UL), 3U, chain));

            StringAssert.Contains("断裂", ex.Message);
            StringAssert.Contains("版本 1", ex.Message, "报文须点名断在哪一级");
            StringAssert.Contains("已注册 [0]", ex.Message, "报文须列出已注册的版本,便于定位");
        }

        [Test]
        public void test_migrate_forwardIncompatible_throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => SaveMigrationChain.Migrate(Save(5U, 42UL), 3U, Array.Empty<ISaveMigration>()));

            StringAssert.Contains("只升不降", ex.Message);
        }

        [Test]
        public void test_migrate_duplicateFromVersion_throws()
        {
            var chain = new List<ISaveMigration>
            {
                new FakeMigration(0U),
                new FakeMigration(0U),   // 同一级注册两次 ⇒ 后者会静默覆盖前者
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => SaveMigrationChain.Migrate(Save(0U, 42UL), 1U, chain));

            StringAssert.Contains("FromVersion=0", ex.Message);
            StringAssert.Contains("永不执行", ex.Message);
        }

        // ══════════════════════════════════════════════════════════════════
        // ② / ③ 两条不变量:正常路径下须**真的成立**(否则下面的负向测没有基准)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_migrate_wellBehavedStep_keepsWorldSeedAndBody()
        {
            byte[] body = { 0x11, 0x22, 0x33, 0x44, 0x55 };
            byte[] input = Save(0U, 0x0123456789ABCDEFUL, body);

            byte[] output = SaveMigrationChain.Migrate(
                input, 1U, new List<ISaveMigration> { new FakeMigration(0U) });

            Assert.AreEqual(0x0123456789ABCDEFUL, SaveMigrationChain.ReadWorldSeed(output),
                "WorldSeed 须逐位不变");
            Assert.AreEqual(HeaderFixed + 4, output.Length - body.Length,
                "头部确实变长了(证明本次测的不是等长变换)");

            // 正文在**末尾**,与头部长度无关
            var tail = new byte[body.Length];
            Array.Copy(output, output.Length - body.Length, tail, 0, body.Length);
            CollectionAssert.AreEqual(body, tail, "已发生事件须逐位不变");
        }

        // ══════════════════════════════════════════════════════════════════
        // ④ 三类坏脚本须被拦住(每类一条 —— 各自独立的守卫)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_migrate_stepThatDoesNotAdvanceVersion_throws()
        {
            var bad = new FakeMigration(0U, advanceVersion: false);

            var ex = Assert.Throws<InvalidOperationException>(
                () => SaveMigrationChain.Migrate(
                    Save(0U, 42UL), 1U, new List<ISaveMigration> { bad }));

            StringAssert.Contains("没把自己的版本号写进头部", ex.Message);
        }

        [Test]
        public void test_migrate_stepThatChangesWorldSeed_throws()
        {
            // 把 seed 改掉 1 位
            var bad = new FakeMigration(0U, tamper: d => d[SeedOffset] ^= 0x01);

            var ex = Assert.Throws<InvalidOperationException>(
                () => SaveMigrationChain.Migrate(
                    Save(0U, 0x1122334455667788UL), 1U, new List<ISaveMigration> { bad }));

            StringAssert.Contains("WorldSeed", ex.Message);
            StringAssert.Contains("ADR-007", ex.Message, "报文须点名硬约束出处");
        }

        [Test]
        public void test_migrate_stepThatChangesBody_throws()
        {
            // 改正文最后一个字节(FakeMigration 已把正文整体后移 padBytes)
            var bad = new FakeMigration(0U, tamper: d => d[d.Length - 1] ^= 0xFF);

            var ex = Assert.Throws<InvalidOperationException>(
                () => SaveMigrationChain.Migrate(
                    Save(0U, 42UL, new byte[] { 1, 2, 3 }), 1U,
                    new List<ISaveMigration> { bad }));

            StringAssert.Contains("已发生事件", ex.Message);
        }

        [Test]
        public void test_migrate_doesNotMutateInput()
        {
            byte[] input = Save(0U, 42UL, new byte[] { 9, 9 });
            byte[] snapshot = (byte[])input.Clone();

            SaveMigrationChain.Migrate(input, 1U, new List<ISaveMigration> { new FakeMigration(0U) });

            CollectionAssert.AreEqual(snapshot, input, "链不得就地修改调用方的字节");
        }

        // ══════════════════════════════════════════════════════════════════
        // 与 SaveService 的接缝:校验和槽的剥 / 补
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_stripChecksum_isInverseOfAddChecksum()
        {
            byte[] plain = Save(1U, 0xABCDEFUL, new byte[] { 7, 8, 9 });
            byte[] withChecksum = SaveService.AddChecksum(plain);

            CollectionAssert.AreEqual(plain, SaveService.StripChecksum(withChecksum),
                "StripChecksum 须是 AddChecksum 的逆 —— 否则迁移链读到的版本号 / seed 会错位");
        }

        [Test]
        public void test_stripChecksum_resultStillValidatesThroughAddChecksum()
        {
            byte[] plain = Save(1U, 0xABCDEFUL, new byte[] { 7, 8, 9 });
            byte[] again = SaveService.AddChecksum(SaveService.StripChecksum(
                SaveService.AddChecksum(plain)));

            Assert.IsTrue(SaveService.ValidateChecksum(again), "剥了再补 ⇒ 校验和仍须通过");
        }
    }
}
