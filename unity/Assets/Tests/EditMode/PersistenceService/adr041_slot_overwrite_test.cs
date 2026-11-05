// ADR-041(存档槽覆盖语义)的**存储层判据**。
//
// 权威:`docs/architecture/adr-041-save-slot-overwrite.md` §Validation V-2 / V-3 / V-4 / V-6 / V-7。
//
// 本件断五件:
//   ① **覆盖合法**:`EnsureWritable` 不再按「槽号 < 高水位」拒(V-2);
//   ② **覆写不清锁**:`OnCheckpointCommitted` 不得解锁(V-3 —— 本裁唯一的新失效面);
//   ③ **覆写留上一版**:`WriteAtomic` 的 final→bak 轮转给出**被覆写前**的字节(V-4);
//   ④ **叙事上下文门原样**:不在上下文内 ⇒ 仍抛(V-6,`AC-7a-19 (c)`);
//   ⑤ **高水位不回退**:覆写旧槽不改 `NextWritableSeq`(V-7 的另一半)。
//
// 读盘说明:本文件**不读**仓库内任何外部夹具;全部字节在夹具内构造,落
// `Path.GetTempPath()`,`TearDown` 自删。

using System;
using System.IO;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class Adr041SlotOverwriteTest
    {
        private string _dir;

        /// <summary>恒真的叙事上下文(「在脉案落笔之后」的那一侧)。</summary>
        private sealed class AlwaysIn : INarrativeContextQuery
        {
            public bool InNarrativeContext() => true;
        }

        /// <summary>恒假的叙事上下文(「急救动作进行中」的那一侧)。</summary>
        private sealed class AlwaysOut : INarrativeContextQuery
        {
            public bool InNarrativeContext() => false;
        }

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_adr041_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, true); } catch { }
            }
        }

        private SaveSlotLedger NewLedger(uint highWater, INarrativeContextQuery ctx = null)
            => new SaveSlotLedger(ctx ?? new AlwaysIn(), highWater);

        // ══════════════════════════════════════════════════════════════════
        // V-2 覆盖合法(门① 已删)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ensureWritable_allowsOverwritingOldSlot()
        {
            SaveSlotLedger ledger = NewLedger(highWater: 9);

            Assert.DoesNotThrow(() => ledger.EnsureWritable(new SaveSlot(3)),
                "高水位 9 时写 3:ADR-041 §D2 起合法(旧判据在这里抛 InvalidDataException)");
            Assert.DoesNotThrow(() => ledger.EnsureWritable(new SaveSlot(9)));
            Assert.DoesNotThrow(() => ledger.EnsureWritable(new SaveSlot(20)));
        }

        [Test]
        public void test_ensureWritable_stillRejectsSlotZero()
        {
            SaveSlotLedger ledger = NewLedger(highWater: 3);
            Assert.Throws<InvalidDataException>(() => ledger.EnsureWritable(new SaveSlot(0)),
                "门⓪ 原样保留 —— 0 不是任何槽");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-3 覆写不清锁(本裁唯一的新失效面)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_overwrite_doesNotClearReadLock()
        {
            SaveSlotLedger ledger = NewLedger(highWater: 3);
            var slot = new SaveSlot(3);

            ledger.OnLoaded(slot);
            Assert.IsTrue(ledger.IsLocked(slot), "前置:读档即锁");

            ledger.OnCheckpointCommitted(slot);          // 覆写成功后的回调
            Assert.IsTrue(ledger.IsLocked(slot),
                "覆写**不得**解锁 —— 否则「读 A → 覆写 A → 再读 A」在同一叙事窗口内可循环," +
                "反 scum 归零(ADR-041 §D3)");

            // 只有叙事锚点才整批解锁
            ledger.OnNarrativeAnchor();
            Assert.IsFalse(ledger.IsLocked(slot), "锁的解除点仍然只有一个:叙事锚点");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-4 覆写留上一版(`.bak` = 被覆写前的字节)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_overwrite_writesPreviousVersionToBak()
        {
            var svc = new SaveService(_dir, new SaveCodec());
            var slot = new SaveSlot(3);

            byte[] older = MinimalSaveBytes(tick: 5L);
            byte[] newer = MinimalSaveBytes(tick: 9L);

            svc.SaveBytes(slot, older);
            svc.SaveBytes(slot, newer);                 // 第二次写 ⇒ 触发 final→bak

            string bakPath = Path.Combine(_dir, "save_3.bak");
            Assert.That(File.Exists(bakPath), Is.True,
                "覆写必须留上一版(WriteAtomic 的 final→bak 轮转)");

            byte[] bak = File.ReadAllBytes(bakPath);
            Assert.That(SaveHeaderCodec.Read(bak).Tick, Is.EqualTo(5L),
                "`.bak` 必须是**被覆写前**那一版(tick 5),不是新写入的 tick 9");
        }

        // ══════════════════════════════════════════════════════════════════
        // V-6 叙事上下文门原样(AC-7a-19 (c))
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ensureWritable_outsideNarrativeContext_stillThrows()
        {
            SaveSlotLedger ledger = NewLedger(highWater: 9, new AlwaysOut());

            var ex = Assert.Throws<InvalidOperationException>(
                () => ledger.EnsureWritable(new SaveSlot(3)),
                "叙事上下文门不随覆盖一起放开");
            StringAssert.Contains("叙事上下文", ex.Message);
        }

        // ══════════════════════════════════════════════════════════════════
        // V-7 覆盖不改高水位
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_overwrite_doesNotMoveHighWaterBackwards()
        {
            SaveSlotLedger ledger = NewLedger(highWater: 9);

            ledger.OnCheckpointCommitted(new SaveSlot(3));
            Assert.AreEqual(9U, ledger.NextWritableSeq,
                "覆写旧槽不得把「留档」的默认目标拉回去");

            ledger.OnCheckpointCommitted(new SaveSlot(9));
            Assert.AreEqual(10U, ledger.NextWritableSeq, "写更大的号才推进");
        }

        // ══════════════════════════════════════════════════════════════════
        // TD-persist-22 · 日志半边:回退 `.bak` 的**事实出口**(门 A 无日志面 ⇒ 事实归本类)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_loadBytes_cleanFinal_reportsFinalAsSource()
        {
            var svc = new SaveService(_dir, new SaveCodec());
            var slot = new SaveSlot(3);
            svc.SaveBytes(slot, MinimalSaveBytes(tick: 7L));

            svc.LoadBytes(slot);

            Assert.AreEqual(SaveLoadSource.Final, svc.LastLoadSource,
                "正常读档 ⇒ 来源 = 正式档");
            Assert.AreEqual(SaveLoadFallback.None, svc.LastLoadFallback,
                "没回退 ⇒ 原因 = None(与「回退了但原因是别的」在类型上可分)");
        }

        [Test]
        public void test_loadBytes_corruptFinal_fallsBackToBak_andReportsWhy()
        {
            var svc = new SaveService(_dir, new SaveCodec());
            var slot = new SaveSlot(3);

            svc.SaveBytes(slot, MinimalSaveBytes(tick: 5L));     // v1
            svc.SaveBytes(slot, MinimalSaveBytes(tick: 9L));     // v2 ⇒ v1 进 .bak

            // 打坏正式档的正文(只翻最后一个字节 ⇒ 校验和必挂;头部仍可解)
            string finalPath = Path.Combine(_dir, "save_3.bin");
            byte[] broken = File.ReadAllBytes(finalPath);
            broken[broken.Length - 1] ^= 0xFF;
            File.WriteAllBytes(finalPath, broken);
            Assert.IsFalse(SaveService.ValidateChecksum(File.ReadAllBytes(finalPath)),
                "[前置] 正式档必须真的坏了");

            byte[] loaded = svc.LoadBytes(slot);

            Assert.AreEqual(5L, SaveHeaderCodec.Read(loaded).Tick,
                "读回来的是**上一版**(v1 = tick 5),不是被打坏的 v2");
            Assert.AreEqual(SaveLoadSource.Bak, svc.LastLoadSource,
                "来源必须是回退档 —— 装配层据此记一条日志(TD-persist-22 日志半边)");
            Assert.AreEqual(SaveLoadFallback.FinalCorrupt, svc.LastLoadFallback,
                "原因 = 正式档校验和不过(区别于保留槽的「正式档缺失」)");
        }

        [Test]
        public void test_loadBytes_failedLoad_doesNotLeaveStaleSource()
        {
            var svc = new SaveService(_dir, new SaveCodec());
            var slot = new SaveSlot(3);

            svc.SaveBytes(slot, MinimalSaveBytes(tick: 5L));
            svc.SaveBytes(slot, MinimalSaveBytes(tick: 9L));
            byte[] broken = File.ReadAllBytes(Path.Combine(_dir, "save_3.bin"));
            broken[broken.Length - 1] ^= 0xFF;
            File.WriteAllBytes(Path.Combine(_dir, "save_3.bin"), broken);
            svc.LoadBytes(slot);                                  // 先成功回退一次
            Assert.AreEqual(SaveLoadSource.Bak, svc.LastLoadSource, "[前置] 上一次读档走了回退");

            // 两级都打坏 ⇒ 这次读档失败
            File.Delete(Path.Combine(_dir, "save_3.bak"));
            Assert.Throws<SaveCorruptException>(() => svc.LoadBytes(slot));

            Assert.AreEqual(SaveLoadSource.None, svc.LastLoadSource,
                "读档失败 ⇒ 事实出口必须清空,否则装配层会拿**上一次**的回退事实记日志");
            Assert.AreEqual(SaveLoadFallback.None, svc.LastLoadFallback);
        }

        /// <summary>一份**能过校验和**的最小存档(头部 + 配置版本段 + 空事件段 + 空折叠段)。</summary>
        private static byte[] MinimalSaveBytes(long tick)
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(
                1UL, 1U, tick, 0UL, 36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L, 0L);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            byte[] cfg = ConfigVersionSegment.Encode(null);
            byte[] folded = new byte[] { 0, 0, 0, 0 };
            byte[] combined = new byte[header.Length + cfg.Length + body.Length + folded.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(cfg, 0, combined, header.Length, cfg.Length);
            Array.Copy(body, 0, combined, header.Length + cfg.Length, body.Length);
            Array.Copy(folded, 0, combined, header.Length + cfg.Length + body.Length, folded.Length);
            return SaveService.AddChecksum(combined);
        }
    }
}
