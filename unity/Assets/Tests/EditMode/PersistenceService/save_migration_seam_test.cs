// `SaveService` ↔ `SaveMigrationChain` 的**接缝**判据(`TD-persist-10` · sprint-21 S21-7 · 2026-10-31)
//
// ══════════════════════════════════════════════════════════════════════════
// 本文件补的是什么
// ══════════════════════════════════════════════════════════════════════════
// ADR-034 实现轮把 v1 档在迁移链**之前**用版本门截住 ⇒ 原来的「注册脚本 ⇒ 链真的被调用」
// 夹具失去可达输入,被替换成**反向钉**(「v1 档不得下沉到链」)。
// 换成反向钉是对的,但它守的是**另一个性质** ——
// 反向钉全绿时,下列错误**一条都抓不到**:
//   · 调完链却把 `migrated` **丢掉**、仍用原 `data`;
//   · 链的返回值是新的,但补校验和补的是**原来那份**;
//   · 迁完的字节根本**解析不了**(接缝通了但产出不可用)。
// ⇒ `TD-persist-10`:`[PayloadSegmentMinVersion, CurrentSaveVersion) = [2, 2)` 是**空区间**,
// 走 `Load` 永远造不出「版本 < 当前 **且** 不被版本门截住」的档 ⇒ 接缝**只能靠读代码守**。
//
// ══════════════════════════════════════════════════════════════════════════
// 本轮的解法:**把接缝抽成一个可直测的方法**
// ══════════════════════════════════════════════════════════════════════════
// `TD-persist-08` 要求拆 `Load`;拆分时把迁移段抽成
// `SaveService.ApplyMigrationsIfNeeded(data, header)`(`internal` + IVT)。
// ⇒ 接缝**不再需要一个非空版本区间**才能测:`ApplyMigrationsIfNeeded` 是
// **版本门之后**的那一步,直接喂 version = 1 的档即可,不必走 `Load` 的版本门。
// `test_persist10_v1Raw_isRejectedByLoad` 保留反向钉,证明「门在被测路径上仍然有效」。
//
// ⚠️ **这不是「用夹具代替规格」**:本件测的是**代码今天的行为**(链被调用 / 返回值被用 /
// 产出可解析),不主张 `[2,2)` 已非空。`CurrentSaveVersion` 真升到 3 时,仍应补一条
// **走完整 `Load` 路径**的端到端迁移夹具 —— 该时点见 `TD-persist-10` 的「何时自动消解」。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    /// <summary>`SaveService` ↔ `SaveMigrationChain` 的接缝判据(`TD-persist-10`)。</summary>
    [TestFixture]
    public class SaveMigrationSeamTest
    {
        private const ulong WorldSeed = 0x0123456789ABCDEFUL;

        private string _testDir;

        [SetUp]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_seam_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_testDir))
            {
                try { Directory.Delete(_testDir, true); } catch { /* 临时目录清理失败不影响判据 */ }
            }
        }

        // ══════════════════ 夹具 ══════════════════

        /// <summary>v2 布局的**最小合法体**:事件段 count=0 + 折叠段 count=0 + 载荷段 count=0。</summary>
        private static readonly byte[] EmptyV2Body = new byte[12];

        /// <summary>造一份**含校验和**的完整存档(指定版本)。</summary>
        private static byte[] Save(uint version, byte[] body = null)
        {
            byte[] head = SaveHeaderCodec.WriteWithoutChecksum(WorldSeed, 7U, 100L, 0UL);
            head[SaveHeaderCodec.SaveVersionOffsetFixed + 0] = (byte)(version & 0xFF);
            head[SaveHeaderCodec.SaveVersionOffsetFixed + 1] = (byte)((version >> 8) & 0xFF);

            body = body ?? EmptyV2Body;
            var combined = new byte[head.Length + body.Length];
            Array.Copy(head, 0, combined, 0, head.Length);
            Array.Copy(body, 0, combined, head.Length, body.Length);
            return SaveService.AddChecksum(combined);
        }

        /// <summary>v1 → v2 的假迁移:只把版本号写成 2(等长、WorldSeed 与正文逐位不变)。</summary>
        private sealed class FakeV1ToV2 : ISaveMigration
        {
            public int Calls;
            public uint FromVersion => 1;

            public byte[] Migrate(byte[] dataWithoutChecksum)
            {
                Calls++;
                var result = (byte[])dataWithoutChecksum.Clone();
                result[SaveHeaderCodec.SaveVersionOffsetFixed + 0] = 2;
                result[SaveHeaderCodec.SaveVersionOffsetFixed + 1] = 0;
                return result;
            }
        }

        private SaveService MakeService(
            IReadOnlyList<ISaveMigration> migrations = null, Action<IReadOnlyList<SimEvent>> replaySink = null)
            => new SaveService(
                _testDir,
                new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: replaySink ?? (_ => { }),
                foldedRowSink: _ => { },
                migrations: migrations);

        // ══════════════════ 接缝本体 ══════════════════

        /// <summary>
        /// **链真的被调用,且返回值真的被用**:输入 v1,输出必须是 v2
        /// (若实现丢弃 `migrated`、返回原字节 ⇒ 这条红)。
        /// </summary>
        [Test]
        public void test_persist10_seam_invokesChainAndUsesItsOutput()
        {
            var mig = new FakeV1ToV2();
            SaveService svc = MakeService(new[] { mig });

            byte[] input = Save(version: 1);
            Assert.That(SaveHeaderCodec.Read(input).SaveVersion, Is.EqualTo(1u), "夹具坐实:输入确是 v1");

            byte[] output = svc.ApplyMigrationsIfNeeded(input, SaveHeaderCodec.Read(input));

            Assert.That(mig.Calls, Is.EqualTo(1), "链必须被调用恰好一次");
            Assert.That(SaveHeaderCodec.Read(output).SaveVersion, Is.EqualTo(2u),
                "输出必须是迁移后的字节 —— 若仍读到 1,说明 migrated 被丢弃、用了原来的 data");
            Assert.That(SaveService.ValidateChecksum(output), Is.True,
                "迁完必须补回校验和(否则读档在 Verifying 步就红)");
            Assert.That(SaveHeaderCodec.Read(output).WorldSeed, Is.EqualTo(WorldSeed),
                "迁移不得改 WorldSeed(ADR-007 §二)");
        }

        /// <summary>版本持平 ⇒ 直通,且**不调链**(证明分支是条件式的,不是无条件迁移)。</summary>
        [Test]
        public void test_persist10_seam_sameVersion_passesThroughWithoutCallingChain()
        {
            var mig = new FakeV1ToV2();
            SaveService svc = MakeService(new[] { mig });

            byte[] input = Save(version: SaveHeader.CurrentSaveVersion);
            byte[] output = svc.ApplyMigrationsIfNeeded(input, SaveHeaderCodec.Read(input));

            Assert.That(mig.Calls, Is.EqualTo(0), "同版本不得调链");
            Assert.That(ReferenceEquals(output, input), Is.True, "同版本须原样返回(零拷贝、零改写)");
        }

        /// <summary>
        /// **迁完真的读档成功**:把迁移产出写进槽位再 `Load`,replay 回调必须被调用。
        /// 同时用未迁移的 v1 原件作对照 —— 它必须被版本门拒收。
        /// </summary>
        [Test]
        public void test_persist10_migratedOutput_loadsAndReplays_whileRawV1IsRejected()
        {
            var mig = new FakeV1ToV2();
            SaveService svc = MakeService(new[] { mig });

            byte[] v1 = Save(version: 1);
            byte[] migrated = svc.ApplyMigrationsIfNeeded(v1, SaveHeaderCodec.Read(v1));

            var slot = new SaveSlot(1);
            svc.SaveBytes(slot, migrated);

            int replayCount = 0;
            int foldedCount = 0;
            var reader = new SaveService(
                _testDir, new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => replayCount++,
                foldedRowSink: _ => foldedCount++);

            Assert.DoesNotThrow(() => reader.Load(slot), "迁移产出必须能被真正读档(接缝通了且产出可用)");
            Assert.That(replayCount, Is.EqualTo(1), "replaySink 必须被调用一次");
            Assert.That(foldedCount, Is.EqualTo(1), "foldedRowSink 必须被调用一次");

            // 对照:同一份数据若不迁移(仍是 v1),Load 必须在版本门被拒 —— 证明门确实在被测路径上。
            var rawSlot = new SaveSlot(2);
            svc.SaveBytes(rawSlot, v1);
            var reader2 = new SaveService(
                _testDir, new SaveCodec(),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: _ => { },
                foldedRowSink: _ => { });

            var ex = Assert.Throws<InvalidDataException>(() => reader2.Load(rawSlot),
                "v1 原件必须被版本门拒收(ADR-034 §⑥)—— 否则上一条的绿不成立");
            StringAssert.Contains("载荷", ex.Message, "拒收理由须点名载荷段(不是「缺迁移脚本」)");
        }

        /// <summary>无迁移可走(旧档无对应脚本)⇒ 链抛,fail-closed,**不得**静默返回原字节。</summary>
        [Test]
        public void test_persist10_seam_missingScript_failsClosed()
        {
            SaveService svc = MakeService(migrations: null);   // 空链
            byte[] v1 = Save(version: 1);

            Assert.Throws<InvalidOperationException>(
                () => svc.ApplyMigrationsIfNeeded(v1, SaveHeaderCodec.Read(v1)),
                "缺脚本时必须抛(断链),不得返回原字节当作迁移成功");
        }
    }
}
