// persistence-service S19-1 测试 —— 槽位契约高层面 + 折叠接线 + Load 3/4 段
//
// 权威来源:
//   design/gdd/persistence-service.md:597-605  —— AC-7a-19 四子判据 (a)(b)(c)(d)
//   design/gdd/save-slot-ui.md:206-207        —— F-7b.2 两投影公式
//   design/gdd/save-slot-ui.md:212-214        —— 三个输入量归 7a
//   ADR-010 §六                                —— Checkpoint 唯一写入口 / 锁不进存档体
//   production/qa/qa-plan-sprint-19-2026-10-24.md §S19-1 —— 判据 ①…⑦
//   production/qa/evidence/s19-1-fold-adjudication-2026-10-24.md —— 判据 ④ 三段拆分
//
// 写盘边界:全部落 `Path.GetTempPath()`(FsWriteBoundaryGate 允许形态;零仓库锚点)。
//
// ⚠️ **本文件不覆盖判据 ④ 的字节半**(「Checkpoint 产出的字节里折叠行出现」)——
//    它标 `NOT-RUN`,归 `TD-persist-03`(2026-10-24 用户裁定)。
//    **不得**以「折叠在生产调用链上生效」顶替它。

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.PersistenceService
{
    public class SaveSlotContractTest
    {
        private string _testDir;

        [SetUp]
        public void Setup()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "dyjq_s19_1_" + Guid.NewGuid().ToString("N"));
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

        // ══════════════════════════════════════════════════════════════════
        // 夹具工厂
        // ══════════════════════════════════════════════════════════════════

        /// <summary>组版一个最小合法存档:v1 布局 = 头部 + 事件段 + 折叠段(count=0),再补校验和。
        /// 🔁 2026-10-24 TD-persist-03:折叠段成必备段,夹具同批更新(两段式按损坏拒收)。</summary>
        private static byte[] MinimalSaveBytes(ulong worldSeed = 12345UL, uint configVersion = 7U,
                                               long tick = 100L, IReadOnlyList<SimEvent> events = null)
        {
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(worldSeed, configVersion, tick, 0UL);
            byte[] body = SimEventListCodec.Encode(events ?? Array.Empty<SimEvent>(), new SaveCodec());
            byte[] folded = new byte[] { 0, 0, 0, 0 }; // 折叠段 count = 0(v1 必备段)
            var combined = new byte[header.Length + body.Length + folded.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);
            Array.Copy(folded, 0, combined, header.Length + body.Length, folded.Length);
            return SaveService.AddChecksum(combined);
        }

        /// <summary>构造一个全驱动源就绪的 SaveService(叙事上下文恒 true)。
        /// 🔁 2026-10-24 TD-persist-03:Load 现要求 foldedRowSink(v1 布局必备段),
        /// 夹具同批补缺省(收字节即弃 —— 本文件不测折叠内容,那是 wiring/roundtrip 文件的职责)。</summary>
        private SaveService ReadyService(
            Func<bool> narrativeContext = null,
            Action<IReadOnlyList<SimEvent>> replaySink = null,
            Action<byte[]> foldedRowSink = null,
            Func<SaveSlot, byte[]> compose = null)
        {
            return new SaveService(
                _testDir,
                new SaveCodec(),
                folded: null,
                narrativeContext: narrativeContext ?? (() => true),
                replaySink: replaySink ?? (_ => { }),
                foldedRowSink: foldedRowSink ?? (_ => { }),
                compose: compose ?? (slot => MinimalSaveBytes()));
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ②(a) —— 写时序递增槽位:允许
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a19a_writeIncreasingSlot_isAllowed()
        {
            var svc = ReadyService();

            Assert.DoesNotThrow(() => svc.Checkpoint(new SaveSlot(0)), "首个槽位应允许写入");
            Assert.DoesNotThrow(() => svc.Checkpoint(new SaveSlot(1)), "递增槽位应允许写入");
            Assert.DoesNotThrow(() => svc.Checkpoint(new SaveSlot(5)), "跳号递增仍允许(只进不退)");

            Assert.AreEqual(6U, svc.Ledger.NextWritableSeq, "高水位 = 最后写入槽位 + 1");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ②(b) —— 覆盖既有槽位:7a 拒绝
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a19b_overwriteExistingSlot_isRejectedBy7a()
        {
            var svc = ReadyService();
            svc.Checkpoint(new SaveSlot(0));

            // 必须是 7a 抛(InvalidDataException),不是 7b 提示、不是静默忽略
            var ex = Assert.Throws<InvalidDataException>(
                () => svc.Checkpoint(new SaveSlot(0)),
                "覆盖既有槽位须由 7a 拒绝(只进不退)");
            StringAssert.Contains("只进不退", ex.Message, "报文须点名只进不退");
        }

        [Test]
        public void test_ac7a19b_writableProjectionSaysFalseForStaleSlot()
        {
            // 投影面同一判据:高水位已过 ⇒ Writable = false
            var ctx = new SlotContext(locked: false, inNarrativeContext: true, nextWritableSeq: 3U);
            Assert.IsFalse(SlotProjections.Writable(new SaveSlot(2), ctx), "2 < 3 ⇒ 不可写");
            Assert.IsTrue(SlotProjections.Writable(new SaveSlot(3), ctx), "3 ≥ 3 ⇒ 可写");
            Assert.IsTrue(SlotProjections.Writable(new SaveSlot(4), ctx), "4 ≥ 3 ⇒ 可写");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ②(c) —— 叙事上下文之外:7a 拒绝
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a19c_outsideNarrativeContext_isRejectedBy7a()
        {
            var svc = ReadyService(narrativeContext: () => false);

            var ex = Assert.Throws<InvalidOperationException>(
                () => svc.Checkpoint(new SaveSlot(0)),
                "叙事上下文之外须由 7a 拒绝");
            StringAssert.Contains("叙事上下文", ex.Message);

            // 副作用为零:不该有文件被写出来
            Assert.AreEqual(0, Directory.GetFiles(_testDir).Length, "被拒的写入不得留下任何文件");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ②(d) —— 读档后同槽位 reads 侧锁定
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a19d_loadLocksSlotForReads()
        {
            var svc = ReadyService();
            svc.Checkpoint(new SaveSlot(0));

            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(0)), "首次载入应允许");
            Assert.IsTrue(svc.Ledger.IsLocked(new SaveSlot(0)), "载入后该槽位须已锁");

            var ex = Assert.Throws<InvalidOperationException>(
                () => svc.Load(new SaveSlot(0)), "再次载入同槽须被拒(读档即锁)");
            StringAssert.Contains("已锁", ex.Message);
        }

        [Test]
        public void test_ac7a19d_narrativeAnchorUnlocksSlots()
        {
            var svc = ReadyService();
            svc.Checkpoint(new SaveSlot(0));
            svc.Load(new SaveSlot(0));
            Assert.IsTrue(svc.Ledger.IsLocked(new SaveSlot(0)));

            svc.Ledger.OnNarrativeAnchor();

            Assert.IsFalse(svc.Ledger.IsLocked(new SaveSlot(0)), "叙事锚点到达 ⇒ 锁解除");
            Assert.AreEqual(1U, svc.Ledger.NextWritableSeq, "锚点不推进高水位(锁与只进不退正交)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ③ —— 闭合回路(BLOCKING):先手动存 → 读档 → 再覆盖回去
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a19_closedLoop_saveLoadOverwrite_isBrokenSomewhere()
        {
            // 端到端一条夹具串起三环 —— 不得拆成三个独立单测(拆开时「每环单独拒绝」
            // 成立但「回路整体可通」这一失效形态测不出来)。
            var svc = ReadyService();

            // 环 1:先手动存
            Assert.DoesNotThrow(() => svc.Checkpoint(new SaveSlot(0)), "环 1:手动存应成功");

            // 环 2:读档
            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(0)), "环 2:读档应成功");

            // 环 3:再覆盖回去 —— 必须被拒(否则 = 事实上的 quicksave)
            Assert.Throws<InvalidDataException>(
                () => svc.Checkpoint(new SaveSlot(0)),
                "环 3:覆盖既有槽位须被拒 —— 闭合回路任一环被拒即可");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ③ —— 两投影是纯函数(反射查零实例字段)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_projections_haveNoMutableInstanceFields()
        {
            // 判据对象恰 = SlotProjections(静态类)+ SlotContext(readonly struct)。
            // SaveSlotLedger 刻意不在本判据面内(它按定义有状态)。
            var t = typeof(SlotProjections);
            Assert.IsTrue(t.IsAbstract && t.IsSealed, "SlotProjections 须是静态类");
            Assert.AreEqual(0, t.GetFields(BindingFlags.Instance | BindingFlags.Public
                                           | BindingFlags.NonPublic).Length,
                "SlotProjections 不得有实例字段");

            // ⚠️ 静态类**没有实例字段可加** ⇒ 上面那条断言恒真、无判别力。
            //    静态类唯一可能的可变状态形态是 **static 可变字段**(缓存 / 记忆化)——
            //    本项目已有先例(ADR-026 ② 构建期记忆化查表)。故必须同时扫 Static。
            //    (`/code-review` S19-1 G-3:原断言只扫 Instance,漏掉这条。)
            foreach (var f in t.GetFields(BindingFlags.Static | BindingFlags.Public
                                          | BindingFlags.NonPublic))
            {
                Assert.IsTrue(f.IsInitOnly || f.IsLiteral,
                    $"SlotProjections.{f.Name} 是 static 可变字段 —— 纯函数不得有静态可变状态" +
                    "(缓存 / 记忆化会让「同输入同输出」失效)");
            }

            var ctxFields = typeof(SlotContext).GetFields(BindingFlags.Instance | BindingFlags.Public
                                                          | BindingFlags.NonPublic);
            Assert.AreEqual(3, ctxFields.Length, "SlotContext 三个字段");
            foreach (var f in ctxFields)
                Assert.IsTrue(f.IsInitOnly, $"SlotContext.{f.Name} 须是 readonly(IsInitOnly)");
        }

        [Test]
        public void test_projections_areSameInputSameOutput()
        {
            // ⚠️ **本条首版是假绿,证伪轮 M10 抓出**(2026-10-24)。根因:
            //    「同输入调用两次结果相同」**恒真** —— 有状态缓存(第一次算、第二次返回缓存)
            //    同样满足它;甚至连「第二次起恒返回 false」的实现也能通过(两次比较都是 false)。
            //    ⇒ 该形态**不可能**证伪纯函数性,须换成**真值表对拍**:把投影的输出与
            //    **独立手算的期望值**逐格比对。缓存实现若吞掉任一格的真值即红。
            var cases = new[]
            {
                //                 locked, inCtx, next, slot, 期望 Writable, 期望 Loadable
                new object[] { false,  true,  0U,  0U,  true,  true  },
                new object[] { false,  true,  5U,  1U,  false, true  },  // 高水位未到 ⇒ 只挡写
                new object[] { true,   true,  0U,  0U,  false, false },  // 锁 ⇒ 读写皆挡
                new object[] { false,  false, 0U,  0U,  false, false },  // 上下文外 ⇒ 读写皆挡
                new object[] { true,   false, 9U,  9U,  false, false },  // 多因子同时为假
                new object[] { false,  true,  3U,  3U,  true,  true  },  // 边界:slot == next ⇒ 可写
                new object[] { false,  true,  3U,  2U,  false, true  },  // 边界:slot == next-1 ⇒ 不可写
            };

            foreach (var c in cases)
            {
                var ctx = new SlotContext((bool)c[0], (bool)c[1], (uint)c[2]);
                var slot = new SaveSlot((uint)c[3]);
                string id = $"locked={c[0]},inCtx={c[1]},next={c[2]},slot={c[3]}";

                Assert.AreEqual((bool)c[4], SlotProjections.Writable(slot, ctx),
                    $"{id} ⇒ Writable 期望 {c[4]}(F-7b.2 公式手算)");
                Assert.AreEqual((bool)c[5], SlotProjections.Loadable(slot, ctx),
                    $"{id} ⇒ Loadable 期望 {c[5]}(F-7b.2 公式手算)");
            }

            // 且同一格**重复求值**必须给出同一答案(这条是补充,单独不足 —— 见上)。
            var repeatCtx = new SlotContext(locked: false, inNarrativeContext: true, nextWritableSeq: 2U);
            var repeatSlot = new SaveSlot(3);
            bool w1 = SlotProjections.Writable(repeatSlot, repeatCtx);
            _ = SlotProjections.Loadable(new SaveSlot(99), repeatCtx);
            Assert.AreEqual(w1, SlotProjections.Writable(repeatSlot, repeatCtx),
                "中间插入别槽位查询不得改变结果");
        }

        [Test]
        public void test_projections_haveIndependentGreyChannels()
        {
            // 「各配独立灰置通道」的可证形态:
            // 存在一个状态使 Writable = false 而 Loadable = true(高水位未到)。
            var blockedBySeq = new SlotContext(locked: false, inNarrativeContext: true, nextWritableSeq: 5U);
            Assert.IsFalse(SlotProjections.Writable(new SaveSlot(1), blockedBySeq), "高水位未到 ⇒ 不可写");
            Assert.IsTrue(SlotProjections.Loadable(new SaveSlot(1), blockedBySeq), "但可载入");

            // 两个正交子句各有一条独有的拒绝路径
            var locked = new SlotContext(locked: true, inNarrativeContext: true, nextWritableSeq: 0U);
            Assert.IsFalse(SlotProjections.Writable(new SaveSlot(0), locked), "锁 ⇒ 不可写");
            Assert.IsFalse(SlotProjections.Loadable(new SaveSlot(0), locked), "锁 ⇒ 不可载入");

            var outside = new SlotContext(locked: false, inNarrativeContext: false, nextWritableSeq: 0U);
            Assert.IsFalse(SlotProjections.Writable(new SaveSlot(0), outside), "上下文外 ⇒ 不可写");
            Assert.IsFalse(SlotProjections.Loadable(new SaveSlot(0), outside), "上下文外 ⇒ 不可载入");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ④ 委托半 —— compose 落在 Checkpoint 体内 + fail-closed
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_checkpoint_invokesComposeDelegate()
        {
            int calls = 0;
            var svc = ReadyService(compose: slot => { calls++; return MinimalSaveBytes(); });

            svc.Checkpoint(new SaveSlot(0));

            Assert.AreEqual(1, calls, "Checkpoint 须调用一次 compose(组版在写盘路径上)");
        }

        [Test]
        public void test_checkpoint_withoutCompose_failsClosed()
        {
            var svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: () => true, replaySink: _ => { }, compose: null);

            var ex = Assert.Throws<InvalidOperationException>(
                () => svc.Checkpoint(new SaveSlot(0)), "未注入 compose ⇒ 须抛(fail-closed)");
            StringAssert.Contains("compose", ex.Message);
            Assert.AreEqual(0, Directory.GetFiles(_testDir).Length, "fail-closed 不得留下任何文件");
        }

        [Test]
        public void test_composeReturningNull_throwsInsteadOfWritingEmptySave()
        {
            var svc = ReadyService(compose: slot => null);

            Assert.Throws<InvalidOperationException>(
                () => svc.Checkpoint(new SaveSlot(0)), "组版返回 null ⇒ 须抛,不得写空档");
            Assert.AreEqual(0, Directory.GetFiles(_testDir).Length, "不得留下任何文件");
        }

        [Test]
        public void test_checkpoint_writesBytesProducedByCompose()
        {
            // 组版产出的字节原样落盘(Checkpoint 不得二次加工)
            byte[] expected = MinimalSaveBytes(worldSeed: 999UL);
            var svc = ReadyService(compose: slot => expected);

            svc.Checkpoint(new SaveSlot(0));

            byte[] written = File.ReadAllBytes(Path.Combine(_testDir, "save_0.bin"));
            CollectionAssert.AreEqual(expected, written, "落盘字节须逐位等于 compose 产出");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ⑤ / ⑦ —— Load 的 Gating + Verifying + 头部解析 + Replaying 前半段
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_load_handsEventListToReplaySink()
        {
            var ev = new SimEvent(42L, new PatientId(7), 0L, EventKind.DiseaseOnset,
                new PayloadRef(1, 0, 16));
            byte[] save = MinimalSaveBytes(tick: 42L, events: new[] { ev });

            IReadOnlyList<SimEvent> handed = null;
            var svc = ReadyService(replaySink: list => handed = list);
            svc.SaveBytes(new SaveSlot(0), save);

            svc.Load(new SaveSlot(0));

            Assert.IsNotNull(handed, "Replaying 前半段须把列表交出");
            Assert.AreEqual(1, handed.Count);
            Assert.AreEqual(42L, handed[0].Tick);
            Assert.AreEqual(7, handed[0].Patient.Value);
            Assert.AreEqual(EventKind.DiseaseOnset, handed[0].Kind);
        }

        [Test]
        public void test_load_withoutReplaySink_throwsInsteadOfSkipping()
        {
            var svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: () => true, replaySink: null,
                compose: slot => MinimalSaveBytes());
            svc.SaveBytes(new SaveSlot(0), MinimalSaveBytes());

            var ex = Assert.Throws<InvalidOperationException>(
                () => svc.Load(new SaveSlot(0)), "未注入 replaySink ⇒ 须抛,禁静默跳过");
            StringAssert.Contains("replaySink", ex.Message);
        }

        [Test]
        public void test_load_outsideNarrativeContext_isRejected()
        {
            var svc = ReadyService(narrativeContext: () => false);
            svc.SaveBytes(new SaveSlot(0), MinimalSaveBytes());

            Assert.Throws<InvalidOperationException>(
                () => svc.Load(new SaveSlot(0)), "载入安全上下文门:上下文外须拒");
        }

        [Test]
        public void test_load_rejectsFutureSaveVersion()
        {
            // 前向不兼容:SaveVersion > CurrentSaveVersion ⇒ 拒绝
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(1UL, 1U, 0L, 0UL);
            // ⚠️ 在**未插校验和**的形态下改:magic(0..3) 之后紧接 saveVersion(4..7)。
            //    AddChecksum 会把 saveVersion 挪到 36..39 —— 改必须在挪之前做。
            header[4] = (byte)(SaveHeader.CurrentSaveVersion + 1);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            var combined = new byte[header.Length + body.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);

            var svc = ReadyService();
            svc.SaveBytes(new SaveSlot(0), SaveService.AddChecksum(combined));

            var ex = Assert.Throws<InvalidDataException>(
                () => svc.Load(new SaveSlot(0)), "前向不兼容须拒绝载入");
            StringAssert.Contains("前向不兼容", ex.Message);
        }

        [Test]
        public void test_load_currentSaveVersion_passesThrough()
        {
            var svc = ReadyService();
            svc.SaveBytes(new SaveSlot(0), MinimalSaveBytes());

            Assert.DoesNotThrow(() => svc.Load(new SaveSlot(0)), "同版本 ⇒ 直通");
        }

        [Test]
        public void test_load_olderSaveVersion_isRejectedUntilMigrationExists()
        {
            // 判据 ⑦b 第三分支:`SaveVersion < CurrentSaveVersion` ⇒ **逐版本升级**。
            //
            // ⚠️ **今日升链实现不存在**(`TD-persist-03`),故本夹具断言的是
            //    **fail-closed 的拒绝**,不是「升级成功」。**不得**把它读成
            //    「⑦b 三分支已全部兑现」—— 真迁移仍 `NOT-RUN`。
            //
            // 为什么必须拒绝而不是放行:旧档会被 `ReadEventSegment` 按**当前版本布局**解析,
            // 布局一旦变过就是**静默错读**(读出一个看似合法、实则错位的存档)。
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(0xDEADBEEFCAFEBABEUL, 1U, 0L, 0UL);
            // 同前:`AddChecksum` 之前 saveVersion 在 4..7
            header[4] = (byte)(SaveHeader.CurrentSaveVersion - 1);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            var combined = new byte[header.Length + body.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);

            var svc = ReadyService();
            svc.SaveBytes(new SaveSlot(0), SaveService.AddChecksum(combined));

            var ex = Assert.Throws<NotSupportedException>(
                () => svc.Load(new SaveSlot(0)),
                "旧版本档在升链未实现前须 fail-closed 拒绝,不得按当前布局静默解析");
            StringAssert.Contains("升链", ex.Message);
            StringAssert.Contains("TD-persist-03", ex.Message,
                "报文须点名归属件 —— 否则读代码的人无从知道真迁移归哪");
        }

        [Test]
        public void test_load_olderSaveVersion_doesNotLockSlot()
        {
            // 拒绝发生在 `Verifying` 之后的迁移链判定段,**早于**读档锁置位
            // ⇒ 旧档载入失败不得永久锁槽(`sprint-19.md` §S19-1 R-1 的相邻形态)。
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(1UL, 1U, 0L, 0UL);
            header[4] = (byte)(SaveHeader.CurrentSaveVersion - 1);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            var combined = new byte[header.Length + body.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(body, 0, combined, header.Length, body.Length);

            var svc = ReadyService();
            svc.SaveBytes(new SaveSlot(0), SaveService.AddChecksum(combined));

            Assert.Throws<NotSupportedException>(() => svc.Load(new SaveSlot(0)));
            Assert.IsFalse(svc.Ledger.IsLocked(new SaveSlot(0)),
                "迁移链拒绝不得锁槽 —— 锁只在 Verifying + 迁移链全通过后置位");
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ⑧b / ⑧c —— fail-closed 与「一个量」
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_checkpoint_withoutNarrativeContext_failsClosed()
        {
            var svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: null, replaySink: _ => { }, compose: slot => MinimalSaveBytes());

            var ex = Assert.Throws<InvalidOperationException>(
                () => svc.Checkpoint(new SaveSlot(0)), "未注入叙事上下文 ⇒ 须抛(fail-closed)");
            StringAssert.Contains("narrativeContext", ex.Message);
            Assert.AreEqual(0, Directory.GetFiles(_testDir).Length, "fail-closed 不得写盘");
        }

        [Test]
        public void test_load_withoutNarrativeContext_failsClosed()
        {
            var svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: null, replaySink: _ => { }, compose: slot => MinimalSaveBytes());
            svc.SaveBytes(new SaveSlot(0), MinimalSaveBytes());

            Assert.Throws<InvalidOperationException>(
                () => svc.Load(new SaveSlot(0)), "未注入叙事上下文 ⇒ Load 亦须抛(fail-closed)");
        }

        [Test]
        public void test_ledger_withoutNarrativeContext_failsClosed()
        {
            var ledger = new SaveSlotLedger(null);
            Assert.IsFalse(ledger.HasNarrativeContextSource);
            Assert.Throws<InvalidOperationException>(
                () => ledger.Snapshot(new SaveSlot(0)), "台账未注入钩子 ⇒ 查询须抛");
            Assert.Throws<InvalidOperationException>(
                () => ledger.EnsureWritable(new SaveSlot(0)), "台账未注入钩子 ⇒ 校验须抛");
        }

        [Test]
        public void test_narrativeContext_isOneQuantity_drivesBothProjectionsTogether()
        {
            // ⑧c:InNarrativeContext 是一个量 ⇒ 注入 false 时两投影同时 false,
            // 注入 true 时(锁与高水位满足的前提下)同时 true。
            // 不得出现「一个门真、另一个门假」的可构造状态。
            var falseCtx = new SlotContext(locked: false, inNarrativeContext: false, nextWritableSeq: 0U);
            Assert.IsFalse(SlotProjections.Writable(new SaveSlot(0), falseCtx));
            Assert.IsFalse(SlotProjections.Loadable(new SaveSlot(0), falseCtx));

            var trueCtx = new SlotContext(locked: false, inNarrativeContext: true, nextWritableSeq: 0U);
            Assert.IsTrue(SlotProjections.Writable(new SaveSlot(0), trueCtx));
            Assert.IsTrue(SlotProjections.Loadable(new SaveSlot(0), trueCtx));

            // 台账侧同一结论:同一个钩子驱动两投影
            bool flag = false;
            var ledger = new SaveSlotLedger(() => flag);
            var snap = ledger.Snapshot(new SaveSlot(0));
            Assert.IsFalse(SlotProjections.Writable(new SaveSlot(0), snap));
            Assert.IsFalse(SlotProjections.Loadable(new SaveSlot(0), snap));

            flag = true;
            snap = ledger.Snapshot(new SaveSlot(0));
            Assert.IsTrue(SlotProjections.Writable(new SaveSlot(0), snap));
            Assert.IsTrue(SlotProjections.Loadable(new SaveSlot(0), snap));
        }

        // ══════════════════════════════════════════════════════════════════
        // 判据 ⑦ —— SaveHeader 读回 + 事件列表容器(两处「须新写」)
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveHeader_roundTripsAllFields()
        {
            byte[] save = MinimalSaveBytes(worldSeed: 0xDEADBEEFCAFEBABEUL, configVersion: 0x12345678U,
                                           tick: -42L);

            SaveHeader h = SaveHeaderCodec.Read(save);

            Assert.AreEqual(SaveHeader.Magic, h.MagicValue);
            Assert.AreEqual(SaveHeader.CurrentSaveVersion, h.SaveVersion);
            Assert.AreEqual(0xDEADBEEFCAFEBABEUL, h.WorldSeed, "WorldSeed 须逐位往返");
            Assert.AreEqual(0x12345678U, h.ConfigVersion);
            Assert.AreEqual(-42L, h.Tick, "负 tick 须往返(有符号)");
            Assert.AreEqual(0UL, h.SnapshotOffset);
        }

        [Test]
        public void test_saveHeader_rejectsBadMagic()
        {
            byte[] save = MinimalSaveBytes();
            save[0] ^= 0xFF;

            var ex = Assert.Throws<InvalidDataException>(
                () => SaveHeaderCodec.Read(save), "magic 不符须抛,禁返回 default");
            StringAssert.Contains("magic", ex.Message);
        }

        [Test]
        public void test_saveHeader_rejectsShortBuffer()
        {
            Assert.Throws<InvalidDataException>(
                () => SaveHeaderCodec.Read(new byte[10]), "长度不足须抛");
            Assert.Throws<ArgumentNullException>(
                () => SaveHeaderCodec.Read(null), "null 须抛");
        }

        [Test]
        public void test_eventList_roundTripsMultipleEvents()
        {
            var events = new[]
            {
                new SimEvent(1L, new PatientId(1), 0L, EventKind.DiseaseOnset, new PayloadRef(1, 0, 8)),
                new SimEvent(2L, new PatientId(1), 1L, EventKind.PatientOutcomeChanged, new PayloadRef(1, 8, 12)),
                new SimEvent(3L, PatientId.None, 0L, EventKind.Craft, new PayloadRef(2, 0, 4)),
            };

            byte[] bytes = SimEventListCodec.Encode(events, new SaveCodec());
            IReadOnlyList<SimEvent> back = SimEventListCodec.Decode(bytes, new SaveCodec());

            Assert.AreEqual(3, back.Count);
            for (int i = 0; i < events.Length; i++)
            {
                Assert.AreEqual(events[i].Tick, back[i].Tick, $"事件 {i} 的 Tick");
                Assert.AreEqual(events[i].Patient.Value, back[i].Patient.Value, $"事件 {i} 的 Patient");
                Assert.AreEqual(events[i].Seq, back[i].Seq, $"事件 {i} 的 Seq");
                Assert.AreEqual(events[i].Kind, back[i].Kind, $"事件 {i} 的 Kind");
                Assert.AreEqual(events[i].Payload.BlobId, back[i].Payload.BlobId, $"事件 {i} 的 Payload");
            }
        }

        [Test]
        public void test_eventList_emptyListRoundTrips()
        {
            byte[] bytes = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            var back = SimEventListCodec.Decode(bytes, new SaveCodec());
            Assert.AreEqual(0, back.Count, "空列表须往返");
        }

        [Test]
        public void test_eventList_rejectsTrailingBytes()
        {
            // 段未读尽 ⇒ 抛(防「长度写错但前 N 条对」的静默错读)
            byte[] bytes = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            var padded = new byte[bytes.Length + 4];
            Array.Copy(bytes, padded, bytes.Length);

            Assert.Throws<InvalidDataException>(
                () => SimEventListCodec.Decode(padded, new SaveCodec()), "尾部多余字节须抛");
        }

        [Test]
        public void test_eventList_rejectsNegativeCount()
        {
            var w = new CodecWriter();
            w.WriteInt32LittleEndian(-1);
            byte[] bytes = w.ToArray();

            Assert.Throws<InvalidDataException>(
                () => SimEventListCodec.Decode(bytes, new SaveCodec()), "count 为负须抛");
        }

        // ══════════════════════════════════════════════════════════════════
        // SaveOnExit —— 同门同组版,不推进高水位
        // 🔁 2026-10-25 W4 修正(代码评审必修):退出保存改走独立文件名 `save_exit.bin`,
        //    不再与玩家手动槽 0 共用 `save_0.bin`。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_saveOnExit_writesWithoutAdvancingSlotHighWatermark()
        {
            var svc = ReadyService();

            svc.SaveOnExit();

            Assert.AreEqual(0U, svc.Ledger.NextWritableSeq,
                "退出保存不占玩家槽 ⇒ 高水位不动");
            // ⚠️ 字面量(不引用 SaveService.ExitSaveFileName —— 判据须独立于被测常量)
            Assert.IsTrue(File.Exists(Path.Combine(_testDir, "save_exit.bin")),
                "退出保存须落盘(独立文件名 save_exit.bin)");
        }

        /// <summary>
        /// 🔁 **2026-10-25 W4 新增**:退出保存与玩家槽 0 **不得**互相覆盖。
        /// 这是 W4 的正面判据 —— 原实现两者共用 `save_0.bin`,本测在旧实现上必红。
        /// </summary>
        /// <remarks>
        /// 🔴 **2026-10-25 证伪轮修正(假绿)**:首版三处断言都用
        /// `SaveService.ExitSaveFileName` 作路径 —— 变异把该常量改成 `"save_0.bin"` 后,
        /// **测试跟着读 `save_0.bin`**,于是「退出保存的文件存在」与「两文件字节相同」
        /// 两条断言全部**碰巧成立**,零红逃逸。
        /// <para>病因:**判据跟着被测对象一起动** —— 断言引用被断言物本身,等于没断言。
        /// 这正是项目反复抓到的「自指判据」形态(同 W1 的
        /// `test_budget_deferSlots_noDirectMutationPath`)。</para>
        /// <para>修法:文件名改用**字面量**,判据独立于实现常量。若实现改回撞名,
        /// 字面量 `save_exit.bin` 不存在 ⇒ ① 必红。</para>
        /// </remarks>
        [Test]
        public void test_saveOnExit_doesNotCollideWithPlayerSlotZero()
        {
            // ⚠️ 三条路径全部用**字面量**,不得引用 SaveService.ExitSaveFileName / GetFinalPath
            //    (否则判据随被测对象漂移 —— 见 remarks)。
            const string ExitFile = "save_exit.bin";
            const string Slot0File = "save_0.bin";

            var svc = ReadyService();

            // 先写玩家槽 0
            svc.Checkpoint(new SaveSlot(0));
            string slot0Path = Path.Combine(_testDir, Slot0File);
            Assert.IsTrue(File.Exists(slot0Path), "前提:玩家槽 0 已落盘为 save_0.bin");
            byte[] slot0AfterCheckpoint = File.ReadAllBytes(slot0Path);

            // 再退出保存
            svc.SaveOnExit();

            // ① 退出保存走自己的文件(字面量判据:实现若改回撞名,本断言必红)
            string exitPath = Path.Combine(_testDir, ExitFile);
            Assert.IsTrue(File.Exists(exitPath),
                "退出保存须写 save_exit.bin(字面量)");

            // ② 玩家槽 0 的字节**不被退出保存改写**
            byte[] slot0AfterExit = File.ReadAllBytes(slot0Path);
            CollectionAssert.AreEqual(slot0AfterCheckpoint, slot0AfterExit,
                "退出保存不得覆盖玩家槽 0 —— 两者必须是不同文件");

            // ③ 反向:玩家再 checkpoint 也不得覆盖退出保存
            byte[] exitBefore = File.ReadAllBytes(exitPath);
            svc.Checkpoint(new SaveSlot(1));
            byte[] exitAfter = File.ReadAllBytes(exitPath);
            CollectionAssert.AreEqual(exitBefore, exitAfter,
                "玩家 checkpoint 不得覆盖退出保存");
        }

        [Test]
        public void test_saveOnExit_withoutCompose_failsClosed()
        {
            var svc = new SaveService(_testDir, new SaveCodec(),
                narrativeContext: () => true, replaySink: _ => { }, compose: null);

            Assert.Throws<InvalidOperationException>(
                () => svc.SaveOnExit(), "未注入 compose ⇒ 须抛(fail-closed)");
        }

        [Test]
        public void test_saveOnExit_outsideNarrativeContext_stillWrites()
        {
            // ⚠️ 本条**钉住一个刻意的语义选择**(用户 2026-10-24 裁定「只修注释,不改行为」):
            //    退出保存与 Checkpoint **同组版、不同门** —— 它**不**评估叙事上下文钩子的值。
            //    理由:退出保存不是回滚点,且「急救动作进行中退出」恰恰最需要落盘。
            //    ⇒ 钩子返回 false 时 Checkpoint 必抛,而 SaveOnExit 必须照写。
            //    反证:若给 SaveOnExit 加了值门,本条转红。
            var svc = ReadyService(narrativeContext: () => false);

            Assert.Throws<InvalidOperationException>(
                () => svc.Checkpoint(new SaveSlot(0)),
                "对照组:Checkpoint 在上下文外必抛(AC-7a-19 (c))");

            Assert.DoesNotThrow(() => svc.SaveOnExit(),
                "SaveOnExit 不受叙事上下文值门约束(只修注释,不改行为的裁定)");
            // ⚠️ 字面量(不引用 SaveService.ExitSaveFileName)
            Assert.IsTrue(File.Exists(Path.Combine(_testDir, "save_exit.bin")),
                "退出保存须照常落盘");
        }

        [Test]
        public void test_eventList_rejectsCountBeyondBuffer()
        {
            // count 上界守卫(SimEventListCodec.cs:60-62):count 声明得比剩余字节能容纳的
            // 事件数还多 ⇒ 必须抛,不得先分配再失败(超大分配面)。
            // 构造:count = 1_000_000,但段里只有 4 字节(连一条事件都不够)。
            var w = new CodecWriter();
            w.WriteInt32LittleEndian(1_000_000);
            byte[] segment = w.ToArray();

            var ex = Assert.Throws<InvalidDataException>(
                () => SimEventListCodec.Decode(segment, new SaveCodec()));
            StringAssert.Contains("超出剩余字节可容纳的上界", ex.Message,
                "报文须点名上界违例,而非笼统的越界");
        }
    }
}
