// 槽位 `.bak` 的**两条判据**(2026-11-04):
//   ① `ProbeCountsBak` —— `.bak` 算不算「这个槽有档」;**只给探测 / 占用态用**。
//   ② `ReadFallsBackToBak` —— 读档时 final 校验坏能不能改读 `.bak`;**恒 true**。
//
// 🔁 **本文件的核心判据在 2026-11-04 反转**(ADR-041 §D4,用户裁定「覆写合法」):
//   旧口径(`TD-persist-19`)是「手动槽一生只写一次 ⇒ 永不产生 bak ⇒ 盘上若有必是旧残留 ⇒
//   读它 = 静默装回更旧的档」。覆盖开启后 `save_N.bak` **就是这一槽的上一版**
//   (`WriteAtomic` 的 final→bak 轮转)⇒ 回退它是**抢救**,不是装错。
//   ⚠️ 但**占用态仍以 final 为准**:`save_4.bin` 不存在而只有 `save_4.bak` ⇒ 探针报空、
//      读档也报空槽(**两处必须同向**,否则重造「屏上说有档 / 按下去说空」)。
//
// ⚠️ 本文件同时钉住**保留槽的两半**:占用态认 bak、读档也回退。
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
                1UL, 1U, tick, 0UL, 36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L);
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
        public void test_manualSlot_finalMissing_bakAlone_isEmptyEverywhere()
        {
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 5L), MinimalSaveBytes(tick: 9L));
            File.Delete(Path.Combine(_dir, "save_4.bin"));

            // ⚠️ **两处必须同向**:探测说空 ⇒ 读档也必须说空(否则「屏上说空、读得出来」)。
            Assert.That(SaveSlotProbe.Probe(_dir, 4).State, Is.EqualTo(SaveSlotState.Empty),
                "占用态以 final 为准(`ProbeCountsBak` 对手动槽恒 false)");

            Assert.Throws<SaveNotFoundException>(
                () => new SaveService(_dir, new SaveCodec()).LoadBytes(new SaveSlot(4)),
                "final 没了 ⇒ 空槽;只剩 `.bak` 不足以让读档成功(与探测同向)");
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
        public void test_ledger_twoPredicates_areSplitNotMerged()
        {
            // ① 占用态:只有保留槽认 bak
            Assert.That(SaveSlotLedger.ProbeCountsBak(SaveSlotLedger.ExitSlotSeq), Is.True, "1 = 退出保存");
            Assert.That(SaveSlotLedger.ProbeCountsBak(SaveSlotLedger.AutoSlotSeq), Is.True, "2 = 自动存档");
            Assert.That(SaveSlotLedger.ProbeCountsBak(SaveSlotLedger.MinManualSlotSeq), Is.False,
                "3 = 第一个手动槽:覆盖虽合法,bak 仍**不算有档**");
            Assert.That(SaveSlotLedger.ProbeCountsBak(4u), Is.False);
            Assert.That(SaveSlotLedger.ProbeCountsBak(uint.MaxValue), Is.False);

            // ② 读档回退:所有槽都可回退(覆写后 bak 就是上一版)
            Assert.That(SaveSlotLedger.ReadFallsBackToBak(SaveSlotLedger.ExitSlotSeq), Is.True);
            Assert.That(SaveSlotLedger.ReadFallsBackToBak(SaveSlotLedger.AutoSlotSeq), Is.True);
            Assert.That(SaveSlotLedger.ReadFallsBackToBak(SaveSlotLedger.MinManualSlotSeq), Is.True);
            Assert.That(SaveSlotLedger.ReadFallsBackToBak(4u), Is.True);

            // ③ 两条**不得合并**:合并即 `TD-persist-19` 那条「屏上说有档 / 读档是空」的成因
            Assert.That(SaveSlotLedger.ProbeCountsBak(3u),
                Is.Not.EqualTo(SaveSlotLedger.ReadFallsBackToBak(3u)),
                "手动槽上两条谓词必须分叉 —— 合并了就没有「占用态只看 final」这条");
        }

        // ── ④ 呈现面与读档面同向(否则「屏上说有档、按下去说空」) ──────────────────

        [Test]
        public void test_probe_manualSlot_ignoresStaleBak()
        {
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 5L), MinimalSaveBytes(tick: 9L));
            File.Delete(Path.Combine(_dir, "save_4.bin"));

            Assert.That(SaveSlotProbe.Probe(_dir, 4).State, Is.EqualTo(SaveSlotState.Empty),
                "只剩旧残留 bak ⇒ 探针与 LoadBytes 同向报「空」");
        }

        [Test]
        public void test_mostRecent_manualSlotWithOnlyStaleBak_isNotACandidate()
        {
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 5L), MinimalSaveBytes(tick: 9L));
            File.Delete(Path.Combine(_dir, "save_4.bin"));

            var svc = new SaveService(_dir, new SaveCodec());
            Assert.That(svc.TryGetMostRecentSave(out SaveSlot slot), Is.False,
                "整个目录只剩一份属于手动槽的旧残留 bak ⇒ 「没有存档」;" +
                "若认它,菜单会说「继续诊治」而按下即报空槽");

            // 对照:同一份字节放到 final 位置上 ⇒ 就是有档(证明上面的 false 不是路径写错)
            File.Copy(Path.Combine(_dir, "save_4.bak"), Path.Combine(_dir, "save_4.bin"));
            Assert.That(svc.TryGetMostRecentSave(out slot), Is.True);
            Assert.That(slot.SlotSeq, Is.EqualTo(4u));
        }

        /// <summary>
        /// **判别力用例** —— 上一条测不到 `TryGetMostRecentSave` 里的槽型分派(手动槽的候选
        /// 是按 <c>save_*.bin</c> 枚举的,残留 <c>.bak</c> 根本进不了候选表)。
        ///
        /// <para>⚠️ 这条是证伪轮 **M1 逼出来的**:把 `FallsBackToBak` 改成恒 `true` 后,
        /// 上一条**照样绿** ⇒ 它对「手动槽认不认 bak」零判别力(假绿形态 F4「空洞正控」)。</para>
        ///
        /// <para>造场:槽 4 的 final 是 tick 5(**头部随后被打坏**),残留 bak 是 tick 900;
        /// 槽 5 是一份 tick 10 的好档。若认那份残留 bak,「最新一份」会算成槽 4(tick 900)
        /// —— 而按下必报损坏。</para>
        ///
        /// <para>⚠️ final 必须用 <see cref="BreakHeader"/> 毁,不能用 <see cref="Corrupt"/>:
        /// 探针只读头部不验校验和,校验和坏的文件在它眼里仍是 `Occupied` ⇒ 回退支不会走。</para>
        /// </summary>
        [Test]
        public void test_mostRecent_manualSlot_corruptFinalWithValidStaleBak_ranksBehindReadableSave()
        {
            // 先写 tick 900 再写 tick 5 ⇒ final = tick 5,bak = tick 900(残留)
            ArrangeManualSlotWithStaleBak(4, MinimalSaveBytes(tick: 900L), MinimalSaveBytes(tick: 5L));
            new SaveService(_dir, new SaveCodec())
                .SaveBytes(new SaveSlot(5), MinimalSaveBytes(tick: 10L));
            BreakHeader(Path.Combine(_dir, "save_4.bin"));

            var svc = new SaveService(_dir, new SaveCodec());
            Assert.That(svc.TryGetMostRecentSave(out SaveSlot slot), Is.True);
            Assert.That(slot.SlotSeq, Is.EqualTo(5u),
                "槽 4 的 final 坏了,那份 tick=900 的残留 bak **不算该槽的档** ⇒ 最新的是槽 5;" +
                "认了它,「继续诊治」就指向一个按下去必报损坏的槽");
        }
    }
}
