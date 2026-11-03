// 退出保存**可读性**的夹具(2026-11-03 · 用户实测「继续诊治」每次 Play 都失败)。
//
// 病因:
//   `SaveService.TryGetMostRecentSave` 在 `save_exit.bin` 存在时返回 `ExitSaveSlot`
//   (`uint.MaxValue` 哨兵)—— 设计意图是「继续诊治 = 上次退出那一刻」。
//   但读侧 `Load` → `LoadBytes` 按**玩家槽**拼路径 ⇒ 去开 `save_4294967295.bin`
//   ⇒ 恒 `SaveNotFoundException` ⇒ 主菜单「继续」**永远**失败。
//   写侧 (`WriteExitSave`) 与读侧对同一个文件用了两套路径 —— 这正是本项目反复抓到的
//   「双实现必然漂移」形态,只不过这次的漂移是**读写方向**上的。
//
// 本文件断的是**契约**而非实现细节:
//   「`TryGetMostRecentSave` 说有档 ⇒ 它返回的那个 slot **必须**读得出来」。
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态)。

using System;
using System.IO;
using DaYiJingCheng.Gameplay.Boot;              // SaveComposer(组版闭包)
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Tests.DiseaseSimulation;   // FakeIdAuthority / FakePresenceQuery
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    [TestFixture]
    public class SaveExitLoadTest
    {
        private string _dir;
        private InMemoryBlobPool _pool;
        private PayloadDecoder _decoder;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dyjq_exitload_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _pool = new InMemoryBlobPool();
            _decoder = new PayloadDecoder(_pool);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir))
            {
                try { Directory.Delete(_dir, true); } catch { }
            }
        }

        /// <summary>能真写、真读的 `SaveService`。</summary>
        private SaveService MakeService(EventStream stream, long tick = 10L)
            => new SaveService(
                _dir, new SaveCodec(),
                folded: new FoldedPredicate(
                    historyStream: () => stream.Events,
                    caseStream: () => Array.Empty<SimEvent>(),
                    diseaseLookup: _ => null,
                    decoder: _decoder),
                narrativeContext: FuncNarrativeContextQuery.AlwaysIn,
                replaySink: ev => stream.Restore(ev, Array.Empty<FoldRow>()),
                foldedRowSink: _ => { },
                compose: _ => SaveComposer.Compose(
                    stream.Events,
                    new FoldedPredicate(() => stream.Events, () => Array.Empty<SimEvent>(),
                                        _ => null, _decoder),
                    _decoder, new SaveCodec(),
                    worldSeed: 1UL, configVersion: 0U, tick: tick,
                    existingFoldedRows: stream.FoldedRows, blobPool: _pool),
                writeScheduler: action => action(),
                blobRestoreSink: _pool);

        private static EventStream NewStream()
            => new EventStream(new FakeIdAuthority(), new FakePresenceQuery());

        // ══════════════════════════════════════════════════════════════════
        // ① 退出保存能写也能读(修的就是这一半缺失)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_exitSave_loadsBackThroughExitSaveSlot()
        {
            SaveService svc = MakeService(NewStream());
            svc.SaveOnExit();

            Assert.That(File.Exists(Path.Combine(_dir, "save_exit.bin")), Is.True,
                "前置:退出保存已落 save_exit.bin");

            Assert.DoesNotThrow(() => svc.Load(SaveService.ExitSaveSlot),
                "退出保存必须能经 ExitSaveSlot 读回 —— 此前读侧按玩家槽拼路径," +
                "去开 save_4294967295.bin ⇒ 恒空槽 ⇒ 主菜单「继续诊治」永远失败");
        }

        [Test]
        public void test_exitSaveSlot_doesNotResolveToSentinelFilename()
        {
            SaveService svc = MakeService(NewStream());
            svc.SaveOnExit();

            Assert.That(File.Exists(Path.Combine(_dir, "save_" + uint.MaxValue + ".bin")), Is.False,
                "哨兵号**不得**被当成文件名写出去(它只是组版委托的标识)");
        }

        // ══════════════════════════════════════════════════════════════════
        // ② 对外契约:「说有档 ⇒ 读得出来」
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_mostRecentSave_whenOnlyExitSave_isLoadable()
        {
            SaveService svc = MakeService(NewStream());
            svc.SaveOnExit();

            Assert.That(svc.TryGetMostRecentSave(out SaveSlot recent), Is.True);
            Assert.That(recent.SlotSeq, Is.EqualTo(uint.MaxValue),
                "只有退出保存时,「最近档」= 退出保存(它就是上次退出那一刻)");

            // 这一行是**用户可见契约**的唯一判据:说得出槽号,就必须读得动。
            Assert.DoesNotThrow(() => svc.Load(recent),
                "TryGetMostRecentSave 返回的槽必须可读 —— 否则主菜单给玩家的是死路");
        }

        [Test]
        public void test_mostRecentSave_prefersExitSaveOverPlayerSlot()
        {
            EventStream stream = NewStream();
            SaveService svc = MakeService(stream);
            svc.Checkpoint(new SaveSlot(1));      // 玩家槽 1
            svc.SaveOnExit();                      // 退出保存

            Assert.That(svc.TryGetMostRecentSave(out SaveSlot recent), Is.True);
            Assert.That(recent.SlotSeq, Is.EqualTo(uint.MaxValue),
                "退出保存优先于玩家槽(「上次退出那一刻」更近)");

            Assert.DoesNotThrow(() => svc.Load(recent));
        }

        [Test]
        public void test_mostRecentSave_withoutExitSave_fallsBackToPlayerSlot()
        {
            EventStream stream = NewStream();
            SaveService svc = MakeService(stream);
            svc.Checkpoint(new SaveSlot(2));      // 只写玩家槽 2

            Assert.That(svc.TryGetMostRecentSave(out SaveSlot recent), Is.True);
            Assert.That(recent.SlotSeq, Is.EqualTo(2u),
                "没有退出保存 ⇒ 序号最大的玩家槽");

            Assert.DoesNotThrow(() => svc.Load(recent),
                "同一条契约:返回的槽必须可读");
        }

        // ══════════════════════════════════════════════════════════════════
        // ③ 空目录:没档就说没档(不得靠「读时才发现」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_mostRecentSave_emptyDir_returnsFalse()
        {
            SaveService svc = MakeService(NewStream());
            Assert.That(svc.TryGetMostRecentSave(out _), Is.False,
                "一个文件都没有 ⇒ 无档(主菜单文案 = 开始诊治)");
        }
    }
}
