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

        // ══════════════════════════════════════════════════════════════════
        // 「空槽」≠「损坏」(2026-10-31 用户裁定「甲」· 走查实测驱动)
        //
        // 病因:`LoadBytes` 原先把「两级文件都不存在」与「两级都坏」并成一类 ⇒ 玩家点一个
        // 从没存过的空槽,控制台打红字「存档损坏且无法回退」。拒绝载入是对的,话是错的。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_emptySlot_throwsSaveNotFound_notCorrupt()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(3);

            var ex = Assert.Throws<SaveNotFoundException>(() => svc.LoadBytes(slot),
                "两级文件都不存在 = 空槽(正常态)⇒ 必须是 SaveNotFoundException");
            Assert.AreEqual(3U, ex.SlotSeq, "异常须点名是哪个槽空着");
            // 🔁 2026-11-03 修(编译警告 CS0184 揪出来的死断言):
            //    原文 `Assert.IsFalse(ex is SaveCorruptException, …)` —— `ex` 的静态类型就是
            //    `SaveNotFoundException`,而两个异常类是**兄弟**(互不继承)⇒ 编译器可证伪该
            //    `is` 表达式 ⇒ **这条断言永远不会红**(F4「空洞正控」的变体:断的是运行期值,
            //    而该值由编译期的类型关系决定)。
            //    它想守的属性是真实的,故改为**结构**判据 —— 若哪天让
            //    `SaveNotFoundException` 继承 `SaveCorruptException`(或反之),下层 catch 的
            //    先后次序就会把空槽报成损坏,本行届时转红。
            Assert.IsFalse(typeof(SaveCorruptException).IsAssignableFrom(typeof(SaveNotFoundException)),
                "空槽不是损坏的一种 —— 两者不得同型(呈现相反:空页 vs 报损坏)。" +
                "此断言是**结构**判据,不是对本次返回值的复查:它守的是两个异常类不得互相继承," +
                "因为那会让 `catch` 的先后次序把空槽误报成损坏。");
        }

        [Test]
        public void test_emptySlot_createsNoFiles()
        {
            var svc = new SaveService(_testDir, new SaveCodec());
            Assert.Throws<SaveNotFoundException>(() => svc.LoadBytes(new SaveSlot(1)));

            CollectionAssert.IsEmpty(Directory.GetFiles(_testDir),
                "空槽是正常态,不是要「修」的东西 —— 不得静默新建空档(AC-7a-15(b) 同源纪律)");
        }

        [Test]
        public void test_finalMissing_bakCorrupt_throwsCorrupt_notNotFound()
        {
            // ★ 判别力用例:这一条把「有没有文件」与「校验过不过」两件事分开。
            //   若判据写成「任一文件缺失 ⇒ NotFound」,本用例会红 —— 但磁盘上**有**东西,
            //   那东西是真坏的,必须报损坏。
            var svc = new SaveService(_testDir, new SaveCodec());
            var slot = new SaveSlot(1);
            // 写两次才产生 bak(`WriteAtomic` 只在 final 已存在时才 final→bak 轮转)
            svc.SaveBytes(slot, SaveService.AddChecksum(MinimalHeaderBytes()));
            svc.SaveBytes(slot, SaveService.AddChecksum(MinimalHeaderBytes()));

            string bakPath = Path.Combine(_testDir, "save_1.bak");
            string finalPath = Path.Combine(_testDir, "save_1.bin");
            Assert.IsTrue(File.Exists(bakPath), "夹具前提:两次写入后 bak 存在");

            byte[] b = File.ReadAllBytes(bakPath); b[40] ^= 0xFF; File.WriteAllBytes(bakPath, b);
            File.Delete(finalPath);                              // final 缺、bak 在但坏

            Assert.Throws<SaveCorruptException>(() => svc.LoadBytes(slot),
                "磁盘上有文件且校验不过 ⇒ 损坏(不是空槽);缺失≠损坏的判据是「有没有东西」");
        }

        [Test]
        public void test_saveNotFound_isNotSubclassOfCorrupt_norIOException()
        {
            Assert.IsFalse(typeof(SaveNotFoundException).IsAssignableFrom(typeof(SaveCorruptException)),
                "「损坏」不得冒充「没有」");
            Assert.IsFalse(typeof(SaveCorruptException).IsAssignableFrom(typeof(SaveNotFoundException)),
                "「没有」不得冒充「损坏」");
            Assert.IsFalse(typeof(IOException).IsAssignableFrom(typeof(SaveNotFoundException)),
                "不得派生自 IOException —— 否则会被可重试兜底静默吞掉(与 Corrupt 同因)");
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
            Assert.AreNotEqual(typeof(SaveCorruptException), ex.GetType(),
                "magic 不符是「格式/版本」问题,不是「两级都坏」—— 二者不得同型" +
                "(前者提示换版本,后者提示报损坏,呈现相反)");
            // ⚠️ 2026-11-03(CS0184):原写 `Assert.IsFalse(ex is SaveCorruptException, …)` ——
            //    `ex` 的静态类型是 `InvalidDataException`,与该异常类无继承关系 ⇒ **永远为真**
            //    (死断言)。上面改成**类型身份**比较(会红),下面补**结构**判据守继承方向:
            //    若 `SaveCorruptException` 哪天派生自 `InvalidDataException`,
            //    `catch (InvalidDataException)` 会把损坏态当格式问题接住。
            Assert.IsFalse(typeof(InvalidDataException).IsAssignableFrom(typeof(SaveCorruptException)),
                "SaveCorruptException 不得派生自 InvalidDataException(否则 catch 次序会把损坏态误报成格式问题)");

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
            byte[] header = SaveHeaderCodec.WriteWithoutChecksum(1UL, 1U, 0L, 0UL, 36000L, 27000L, 9000L, 252000L, 5L, 6000L, 100L);
            byte[] body = SimEventListCodec.Encode(Array.Empty<SimEvent>(), new SaveCodec());
            byte[] cfg = ConfigVersionSegment.Encode(null);   // ADR-037:配置版本段(必备)
            byte[] folded = new byte[] { 0, 0, 0, 0 }; // 折叠段 count = 0(v1 必备段)
            var combined = new byte[header.Length + cfg.Length + body.Length + folded.Length];
            Array.Copy(header, 0, combined, 0, header.Length);
            Array.Copy(cfg, 0, combined, header.Length, cfg.Length);
            Array.Copy(body, 0, combined, header.Length + cfg.Length, body.Length);
            Array.Copy(folded, 0, combined, header.Length + cfg.Length + body.Length, folded.Length);
            return combined;
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-7a-04 —— 真往返(2026-11-03 · S24-2 重写)
        //
        // 🔁 **原 `test_simEventCodec_roundTrip` 的断言只有 `bytes.Length > 0`**
        //    —— 未解码、未比任何字段 ⇒ 对 AC 的三项子判据**零判别力**(第六个假绿形态)。
        //    本段把它重写成「解码回比 + 三次对拍 + 逐字节还原」三项齐备。
        // ══════════════════════════════════════════════════════════════════

        // AC-7a-04(a):写盘再读回 ⇒ **逐字段**等于原值
        [Test]
        public void test_ac7a04_simEventHeader_roundTrip_fieldByField()
        {
            var original = new SimEvent(
                tick: 42L, patient: new PatientId(7), seq: 99L,
                kind: EventKind.ActorCellEntered,
                payload: new PayloadRef(blobId: 3, offset: 16, length: 24));

            byte[] bytes = SimEventCodec.Encode(in original);
            SimEvent back = SimEventCodec.Decode(bytes);

            Assert.AreEqual(original.Tick, back.Tick, "Tick 往返必须逐位相等");
            Assert.AreEqual(original.Patient.Value, back.Patient.Value, "Patient 往返必须逐位相等");
            Assert.AreEqual(original.Seq, back.Seq, "Seq 往返必须逐位相等");
            Assert.AreEqual(original.Kind, back.Kind, "Kind 往返必须逐位相等");
            Assert.AreEqual(original.Payload.BlobId, back.Payload.BlobId, "PayloadRef.BlobId 往返");
            Assert.AreEqual(original.Payload.Offset, back.Payload.Offset, "PayloadRef.Offset 往返");
            Assert.AreEqual(original.Payload.Length, back.Payload.Length, "PayloadRef.Length 往返");
        }

        // AC-7a-04(a 边界):负值 / `long` 两端点不得在往返里被截断或取反
        [Test]
        public void test_ac7a04_simEventHeader_roundTrip_extremesAndNegatives()
        {
            foreach (long tick in new[] { long.MinValue, long.MaxValue, -1L })
            {
                var e = new SimEvent(tick, new PatientId(-1), -7L, EventKind.Craft, default);
                SimEvent back = SimEventCodec.Decode(SimEventCodec.Encode(in e));
                Assert.AreEqual(tick, back.Tick, $"Tick={tick} 往返必须逐位相等");
                Assert.AreEqual(-7L, back.Seq, "负 Seq 往返必须逐位相等");
                Assert.AreEqual(-1, back.Patient.Value, "PatientId.None(-1) 往返必须逐位相等");
            }
        }

        // AC-7a-04(b):同一状态三次序列化 ⇒ 三次字节流逐位一致
        [Test]
        public void test_ac7a04_threeSerializations_areByteIdentical()
        {
            var e = new SimEvent(1234L, new PatientId(11), 5L, EventKind.PatientOutcomeChanged,
                                 new PayloadRef(9, 32, 12));

            byte[] a = SimEventCodec.Encode(in e);
            byte[] b = SimEventCodec.Encode(in e);
            byte[] c = SimEventCodec.Encode(in e);

            CollectionAssert.AreEqual(a, b, "第二次序列化与第一次逐位一致");
            CollectionAssert.AreEqual(a, c, "第三次序列化与第一次逐位一致");
        }

        // AC-7a-04(c):载荷里的 `Fix` 走 raw i64 往返 —— 负值不得被「取绝对值」或舍入
        [Test]
        public void test_ac7a04_payloadFix_roundTrip_preservesRawBits()
        {
            var pool = new InMemoryBlobPool();
            var encoder = new PayloadEncoder(pool);
            var decoder = new PayloadDecoder(pool);

            foreach (long raw in new[] { 0L, 65536L, -65536L, long.MinValue, long.MaxValue })
            {
                var src = new InjuryOnsetPayload(
                    tick: 7L, actorId: 1, targetId: 2, injuryId: 3, magnitude: new Fix(raw), doseSeq: 0);
                PayloadRef r = encoder.Encode(EventKind.InjuryOnset, src);
                InjuryOnsetPayload back = decoder.Decode<InjuryOnsetPayload>(EventKind.InjuryOnset, r);

                Assert.AreEqual(raw, back.Magnitude.Raw,
                    $"Fix raw={raw} 往返必须逐位相等(负值右移 / 取绝对值 / 舍入都会在此转红)");
            }
        }

        // AC-7a-04(d):IL 扫描 —— 7a 的编译产物里零 `BinaryFormatter` / `JsonUtility` / `PlayerPrefs`
        //                     / `BitConverter`(ADR-010 §一:显式小端,禁主机端序)
        [Test]
        public void test_ac7a04_assemblyScan_noForbiddenSerializersOrHostEndianApis()
        {
            string dll = DaYiJingCheng.EditorTools.Gates.AssemblyGates.ScriptAssemblyPath("Sim.Codec");
            Assert.IsTrue(File.Exists(dll), $"Sim.Codec 装配不存在:{dll}");

            string[] forbidden =
            {
                "System.Runtime.Serialization.Formatters.Binary.BinaryFormatter",
                "UnityEngine.JsonUtility",
                "UnityEngine.PlayerPrefs",
                "System.BitConverter",
            };

            var hits = new System.Collections.Generic.List<string>();
            int scanned = 0;
            using (var mod = Mono.Cecil.ModuleDefinition.ReadModule(dll))
            {
                foreach (var type in mod.GetTypes())
                {
                    foreach (var m in type.Methods)
                    {
                        if (!m.HasBody) continue;
                        scanned++;
                        foreach (var ins in m.Body.Instructions)
                        {
                            string owner = InsOperandTypeFullName(ins.Operand);
                            if (owner == null) continue;
                            foreach (string bad in forbidden)
                            {
                                if (owner == bad)
                                    hits.Add($"{type.FullName}.{m.Name}: `{owner}` @ IL_{ins.Offset:X4}");
                            }
                        }
                    }
                }
            }

            Assert.Greater(scanned, 0, "IL 扫描扫到零个方法体 —— 拒以空集冒充绿");
            Assert.IsEmpty(hits,
                "7a 编译产物出现禁用的序列化 / 主机端序 API(ADR-010 §一):\n  "
                + string.Join("\n  ", hits));
        }

        /// <summary>取一条 IL 指令**引用到的类型**全名(方法 / 字段 / 类型操作数);取不到返回 null。</summary>
        private static string InsOperandTypeFullName(object operand)
        {
            switch (operand)
            {
                case Mono.Cecil.MethodReference mr:
                    return mr.DeclaringType?.FullName;
                case Mono.Cecil.FieldReference fr:
                    return fr.DeclaringType?.FullName;
                case Mono.Cecil.TypeReference tr:
                    return tr.FullName;
                default:
                    return null;
            }
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

        // ══════════════════════════════════════════════════════════════════
        // AC-7a-18(加宽)· 2026-11-03(S24-2)
        //
        // 🔁 原夹具只反射扫**一个程序集内名字含 `Save` 的类型**的实例字段
        //    ⇒ 漏静态字段 / 属性 / 局部变量 / 方法返回值,以及 `Sim` 侧。
        //    AC 原文是「grep 7a 侧**全部源码**」。本测按源码面补齐:
        //    扫 `unity/Assets/Sim.Codec/**.cs` 与 `Sim/` 下与本 AC 相关的折叠/事件流文件。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac7a18_sourceSurface_ofSaveCodec_hasNoFloatTypes()
        {
            // 仓库根由**编译产物位置**推(避开 `Application.dataPath` / 当前目录锚点 ——
            // 那些锚点与本文件既有的写盘调用同处会触发 FsWriteBoundaryGate 的具名声明义务)。
            string codecDll = typeof(SaveService).Assembly.Location;
            string root = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(codecDll), "..", "..", ".."));
            string codecDir = Path.Combine(root, "unity", "Assets", "Sim.Codec");
            Assert.IsTrue(Directory.Exists(codecDir), $"扫描目录不存在:{codecDir}");

            string[] files = Directory.GetFiles(codecDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files, "扫到零个源文件 —— 拒以空集冒充绿");

            var hits = new System.Collections.Generic.List<string>();
            foreach (string f in files)
            {
                string code = StripComments(File.ReadAllText(f));
                foreach (string bad in new[] { "float", "double", "decimal" })
                {
                    int at = code.IndexOf(bad, StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        int after = at + bad.Length;
                        bool leftOk = at == 0 || !IsIdentChar(code[at - 1]);
                        bool rightOk = after >= code.Length || !IsIdentChar(code[after]);
                        if (leftOk && rightOk)
                            hits.Add($"{Path.GetFileName(f)} 出现 `{bad}`");
                        at = code.IndexOf(bad, at + 1, StringComparison.Ordinal);
                    }
                }
            }

            Assert.IsEmpty(hits,
                "7a 的 codec 源码出现浮点类型(AC-7a-18;注释已剔除):\n  "
                + string.Join("\n  ", hits));
        }

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <summary>剥除 `//` 与 `/* */` 注释(字符串字面量内容保留)。</summary>
        private static string StripComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inStr = false, inChar = false, esc = false;

            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';

                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && n == '/') { inBlock = false; i++; } continue; }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) { esc = false; continue; }
                    if (c == '\\') { esc = true; continue; }
                    if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inStr = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
