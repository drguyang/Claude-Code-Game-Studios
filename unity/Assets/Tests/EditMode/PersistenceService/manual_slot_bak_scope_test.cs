// 槽位 `.bak` 的**唯一判据**(2026-11-05 统一):
//   `SaveSlotLedger.BakCountsAsSave` —— `.bak` 算不算这个槽的一条存档;**恒 true**。
//   探测(占用态)· 「最近一份」· 读档回退**三处同源**,「屏上说有档 / 按下去说空」不可能再分叉。
//
// 🔁 **沿革(原文不删,防误读)**:
//   · **2026-11-04 之前**(`TD-persist-19`):手动槽一生只写一次 ⇒ 产不出 bak ⇒ 盘上若有
//     必是旧残留 ⇒ 读它会静默装回更旧的档 ⇒ **不认**。
//   · **2026-11-04**(ADR-041 §D4):覆写合法 ⇒ `save_N.bak` 就是这一槽的上一版 ⇒ 读侧认
//     (回退),但**占用态仍以 final 为准** ⇒ 两条谓词**刻意分叉**。
//   · **2026-11-05(本文件当前口径 · 用户裁定「按行业惯例纠正」)**:分叉撤销。
//     病因:`WriteAtomic` 的 `final→bak` 与 `tmp→final` 之间崩溃,会留下
//     **final 缺失 + 一份完整的 bak** —— 分叉口径下这一槽报「空槽」、读档也报空槽
//     ⇒ **那份世界不可达**。备份的语义就是「主档没了用它」,拒绝恢复不是更严格。
//     ⚠️ 合并**不会**重造 `TD-persist-19`:那条病的成因是「探测认而读档不认」,
//     今日三处同源,分叉不可能发生 —— **分叉的成因是两处不一致,不是取值本身**。
//
// 读盘说明:本文件**不读**仓库内任何外部夹具;全部字节在夹具内构造
// (`MinimalSaveBytes`),落 `Path.GetTempPath()`,`TearDown` 自删。

using System;
using System.IO;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class ManualSlotBakScopeTest
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_bak_scope_" + Guid.NewGuid().ToString("N"));
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

        /// <summary>一份**能过校验和**的最小存档(头部 + 配置版本段 + 空事件段 + 空折叠段)。</summary>
        /// <param name="tick">头部 tick(用于「最新一份」的排序判据)。</param>
        private static byte[] MinimalSaveBytes(long tick)
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(
                1UL, 1U, tick, 0UL, 36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L, 0L);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            byte[] cfg = ConfigVersionSegment.Encode(null);
            // 🔁 2026-11-08(ADR-010 义务 13):v7 的分册态段(空)—— 不补则事件段错位
            byte[] casebook = CasebookSegment.Encode(null);
            byte[] folded = new byte[] { 0, 0, 0, 0 };
            byte[] combined = new byte[header.Length + cfg.Length + casebook.Length
                                        + body.Length + folded.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(casebook, 0, combined, header.Length, casebook.Length);
            Array.Copy(cfg, 0, combined, header.Length + casebook.Length, cfg.Length);
            int at = header.Length + casebook.Length + cfg.Length;
            Array.Copy(body, 0, combined, at, body.Length);
            at += body.Length;
            Array.Copy(folded, 0, combined, at, folded.Length);
            return SaveService.AddChecksum(combined);
        }

        /// <summary>在手动槽上造「final + bak 各一份」(两写触发 `WriteAtomic` 的 final→bak 轮转)。</summary>
        /// <remarks>走 `SaveBytes`(internal 低层原语)—— 它绕过 `Checkpoint` 的只进不退门。
        /// 这正是本轮之前那条夹具**造出生产不可达状态**的手法;此处刻意沿用它,
        /// 因为要验的恰恰是「盘上**真有一份**残留 bak 时读侧怎么反应」。</remarks>
        private void ArrangeManualSlotWithStaleBak(uint slotSeq, byte[] older, byte[] newer)
        {
            var svc = new SaveService(_dir, new SaveCodec());
            var slot = new SaveSlot(slotSeq);
            svc.SaveBytes(slot, older);   // 首写:仅 final
            svc.SaveBytes(slot, newer);   // 二写:older → bak,final = newer
        }

        /// <summary>翻正文末字节 ⇒ **校验和**不过(头部仍是好的)。</summary>
        private static void Corrupt(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[bytes.Length - 1] ^= 0xFF;
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// 把文件压成 4 字节 ⇒ **头部都读不出来**(`SaveSlotProbe` 判 `Corrupt`)。
        /// <para>⚠️ 与 <see cref="Corrupt"/> 的区别是**必须**的:探针只读头部**不验校验和**
        /// ⇒ 校验和坏的文件在探针眼里仍然是 `Occupied`,`Consider` 的回退支根本不会走。
        /// (这条是 M1 变异轮第二次逼出来的 —— 用 <see cref="Corrupt"/> 造场的那版夹具
        /// 在「永远回退」下照样绿。)</para>
        /// </summary>
        private static void BreakHeader(string path)
            => File.WriteAllBytes(path, new byte[] { 0x00, 0x00, 0x00, 0x00 });

        // ── ① 核心判据:手动槽 final 坏了 ⇒ **回退上一版**(2026-11-04 反转)────────

        [Test]
        public void test_manualSlot_corruptFinal_fallsBackToBak()
        {
            byte[] older = MinimalSaveBytes(tick: 5L);
            byte[] newer = MinimalSaveBytes(tick: 9L);
            ArrangeManualSlotWithStaleBak(4, older, newer);

            string finalPath = Path.Combine(_dir, "save_4.bin");
            string bakPath = Path.Combine(_dir, "save_4.bak");
            Assert.That(File.Exists(bakPath), Is.True, "夹具前提:盘上确实有一份上一版 bak");
            Corrupt(finalPath);

            byte[] loaded = null;
            Assert.DoesNotThrow(
                () => loaded = new SaveService(_dir, new SaveCodec()).LoadBytes(new SaveSlot(4)),
                "ADR-041 §D4:覆写产生的 `.bak` 就是这一槽的上一版 ⇒ 校验坏时回退是**抢救**");

            // 判别力:回退必须给出**上一版**(tick 5),而不是盘上随便哪一份
            Assert.That(SaveHeaderCodec.Read(loaded).Tick, Is.EqualTo(5L),
                "回退读到的必须是被覆写前那一版");
        }

        [Test]
        public void test_manualSlot_finalMissing_bakAlone_isRecoverable()
        {
            // 崩在 `WriteAtomic` 的 final→bak 与 tmp→final 之间 ⇒ final 没了、bak 是完整上一版
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 5L), MinimalSaveBytes(tick: 9L));
            File.Delete(Path.Combine(_dir, "save_4.bin"));

            SaveSlotSummary s = SaveSlotProbe.Probe(_dir, 4);
            Assert.That(s.State, Is.EqualTo(SaveSlotState.Occupied),
                "**备份在手就是有档** —— 报「空槽」等于对玩家说「你从没存过」,而盘上有一份能读的世界");
            Assert.That(s.Tick, Is.EqualTo(5L),
                "报的是 bak 的 tick(bak = 第一次写的那一版),不是编的");
            Assert.That(s.Source, Is.EqualTo(SaveLoadSource.Bak),
                "来源必须是 Bak —— 呈现层要靠它区分「主档在」与「只剩备份」");

            byte[] loaded = null;
            Assert.DoesNotThrow(
                () => loaded = new SaveService(_dir, new SaveCodec()).LoadBytes(new SaveSlot(4)),
                "探测说有档 ⇒ 读档必须读得出来(**两处同向**,这次是往「都能读」的方向)");
            Assert.That(SaveHeaderCodec.Read(loaded).Tick, Is.EqualTo(5L), "读回的就是那份 bak");
        }

        // ── ② 收窄不得越界:保留槽仍然回退 ──────────────────────────────────────

        [Test]
        public void test_exitSlot_corruptFinal_fallsBackToBak()
        {
            var svc = new SaveService(_dir, new SaveCodec());
            string finalPath = Path.Combine(_dir, SaveService.ExitSaveFileName);
            string bakPath = finalPath + ".bak";
            svc.SaveBytes(SaveService.ExitSaveSlot, MinimalSaveBytes(tick: 5L));
            svc.SaveBytes(SaveService.ExitSaveSlot, MinimalSaveBytes(tick: 9L));
            Assert.That(File.Exists(bakPath), Is.True, "夹具前提:退出保存被重写 ⇒ 有 bak");
            Corrupt(finalPath);

            Assert.DoesNotThrow(
                () => new SaveService(_dir, new SaveCodec()).LoadBytes(SaveService.ExitSaveSlot),
                "退出保存每会话重写 ⇒ bak 是**真实**的上一好档,回退必须保留" +
                "(本轮的收窄只针对手动槽,不得顺手砍掉保留槽的耐久性)");
        }

        // ── ③ 判据本体:唯一定义处的行为 ────────────────────────────────────────

        [Test]
        public void test_ledger_bakCountsAsSave_forEverySlotKind()
        {
            // 唯一判据,对所有槽型一致 —— 3 = 第一个手动槽、uint.MaxValue = 非法号也一样。
            // (非法号由 `EnsureWritable` 的槽号门拦,不靠本判据。)
            foreach (uint seq in new[]
                     {
                         SaveSlotLedger.ExitSlotSeq,
                         SaveSlotLedger.AutoSlotSeq,
                         SaveSlotLedger.MinManualSlotSeq,
                         4u,
                         uint.MaxValue,
                     })
            {
                Assert.That(SaveSlotLedger.BakCountsAsSave(seq), Is.True,
                    $"槽 {seq}:`.bak` 算这个槽的一条存档 —— 探测 / 最近一份 / 读档三处同源");
            }
        }

        // ── ④ 呈现面与读档面同向(否则「屏上说有档、按下去说空」) ──────────────────

        [Test]
        public void test_probe_manualSlot_bakAlone_isOccupiedWithBakSource()
        {
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 5L), MinimalSaveBytes(tick: 9L));
            File.Delete(Path.Combine(_dir, "save_4.bin"));

            SaveSlotSummary s = SaveSlotProbe.Probe(_dir, 4);
            Assert.That(s.State, Is.EqualTo(SaveSlotState.Occupied));
            Assert.That(s.Source, Is.EqualTo(SaveLoadSource.Bak), "只剩备份 ⇒ 来源 = Bak");
            Assert.DoesNotThrow(
                () => new SaveService(_dir, new SaveCodec()).LoadBytes(new SaveSlot(4)),
                "探针说有档 ⇒ 读档必须读得出来(同向)");

            // 对照:主档回位后来源变回 Final(**来源字段有判别力**,不是恒 Bak)。
            // 用 SaveBytes 造一份**不同 tick** 的主档(final 缺失 ⇒ 不轮转,bak 保持 5)。
            new SaveService(_dir, new SaveCodec()).SaveBytes(new SaveSlot(4), MinimalSaveBytes(tick: 9L));
            SaveSlotSummary withFinal = SaveSlotProbe.Probe(_dir, 4);
            Assert.That(withFinal.State, Is.EqualTo(SaveSlotState.Occupied));
            Assert.That(withFinal.Source, Is.EqualTo(SaveLoadSource.Final));
            Assert.That(withFinal.Tick, Is.EqualTo(9L), "主档在 ⇒ 优先报主档的 tick");
        }

        [Test]
        public void test_mostRecent_manualSlotWithOnlyBak_isACandidate()
        {
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 5L), MinimalSaveBytes(tick: 9L));
            File.Delete(Path.Combine(_dir, "save_4.bin"));

            var svc = new SaveService(_dir, new SaveCodec());
            Assert.That(svc.TryGetMostRecentSave(out SaveSlot slot), Is.True,
                "只剩备份的槽**读得出来** ⇒ 它必须能出现在「继续诊治」里;" +
                "把它排除 = 让玩家手动找不到一个本来能读的世界");
            Assert.That(slot.SlotSeq, Is.EqualTo(4u));
        }

        /// <summary>
        /// **排序判据** —— 「最近一份」与「按下去会读到什么」必须一致。
        ///
        /// <para>造场:槽 4 的 final 是 tick 5(**头部随后被打坏**),bak 是 tick 900;
        /// 槽 5 是一份 tick 10 的好档。槽 4 按下去会回退读到 tick 900 ⇒ 它**才是**最新的;
        /// 若在这里只看 final,「继续诊治」会指向槽 5(tick 10),把一份更旧的进度当成最新。</para>
        ///
        /// <para>⚠️ final 必须用 <see cref="BreakHeader"/> 毁,不能用 <see cref="Corrupt"/>:
        /// 探针只读头部不验校验和,校验和坏的文件在它眼里仍是 `Occupied` ⇒ 回退支不会走。</para>
        /// </summary>
        [Test]
        public void test_mostRecent_manualSlot_corruptFinal_fallsBackToBakForRanking()
        {
            // 先写 tick 900 再写 tick 5 ⇒ final = tick 5,bak = 900
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 900L), MinimalSaveBytes(tick: 5L));
            new SaveService(_dir, new SaveCodec())
                .SaveBytes(new SaveSlot(5), MinimalSaveBytes(tick: 10L));
            BreakHeader(Path.Combine(_dir, "save_4.bin"));   // 头部坏 ⇒ 探针报 Corrupt

            var svc = new SaveService(_dir, new SaveCodec());
            Assert.That(svc.TryGetMostRecentSave(out SaveSlot slot), Is.True);
            Assert.That(slot.SlotSeq, Is.EqualTo(4u),
                "槽 4 的 final 头部坏了,但 bak(tick 900)**读得出来** ⇒ 「最近一份」是槽 4;" +
                "按下去会回退读到 tick 900 —— 排序与实际载入结果一致");
        }
    }
}
