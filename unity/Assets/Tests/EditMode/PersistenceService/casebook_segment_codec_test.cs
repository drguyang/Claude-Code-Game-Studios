// S26-1 · casebook-001 —— 分册态段的编解码判据(ADR-010 §三 义务 13 · 存档 v7)。
//
// 权威来源:
//   docs/architecture/adr-010-persistence-save-format.md §一 / §三 义务 13
//   design/gdd/casebook.md 规则一 / 规则二 · AC-39-13(痕不落盘)
//   production/epics/casebook/story-001-volume-state-contract-and-save-segment.md
//   production/qa/qa-plan-sprint-26-2026-11-08.md §S26-1
//
// ⚠️ 本文件放 `PersistenceService/` 而**不是**卡面写的 `Casebook/`,理由:
//    `Casebook.Tests.asmdef` 只引 `Sim.Contracts` + `Gameplay.UI`,**不引 `Sim.Codec`**
//    ⇒ 段编解码在那里编不过。既有 save codec 夹具全在本目录(同一个 `Sim.Contracts.Tests`
//    装配,经 `Gameplay.Boot` 间接可见 `Sim.Codec`)。卡面 Test Evidence 已同步订正。
//
// ⚠️ 假绿防线(本件专门守的):
//   · **F4 空洞正控**:每条「零命中 / 恒等」断言前先断输入非空或确实被改动;
//   · **F3 自指判据**:期望值一律写**字面量**(不引被测常量做期望);
//   · **F9 `default(struct)`**:构造器守卫对 `default(T)` 不生效 —— 单独断言并登记。

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Casebook;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.PersistenceService
{
    [TestFixture]
    public class casebook_segment_codec_test
    {
        // ══════════════ 1. round-trip 逐位相等 ══════════════

        [Test]
        public void test_roundtrip_twoVolumesThreePages_isBitIdentical()
        {
            var snapshot = new CasebookVolumeSnapshot(
                new[]
                {
                    new CasebookVolumeRecord(playerId: 2, pageIndex: 7),
                    new CasebookVolumeRecord(playerId: 1, pageIndex: 0),
                },
                new[]
                {
                    new CasebookPageRecord(2, 100, 1234L, 5678L, 0),
                    new CasebookPageRecord(2, 101, 2345L, 6789L, 1),
                    new CasebookPageRecord(1, 100, 3456L, 7890L, 0),
                });

            byte[] bytes = CasebookSegment.Encode(snapshot);
            CasebookVolumeSnapshot back = CasebookSegment.Decode(bytes, 0, out int consumed);

            Assert.That(consumed, Is.EqualTo(bytes.Length), "游标必须读尽整段");
            Assert.That(back.Volumes.Count, Is.EqualTo(2));
            Assert.That(back.Pages.Count, Is.EqualTo(3));

            // 逐条按 (player) / (player, patient) 对位比较 —— 编码侧会排序,输出即规范序
            Assert.That(back.Volumes[0].PlayerId, Is.EqualTo(1));
            Assert.That(back.Volumes[0].PageIndex, Is.EqualTo(0));
            Assert.That(back.Volumes[1].PlayerId, Is.EqualTo(2));
            Assert.That(back.Volumes[1].PageIndex, Is.EqualTo(7));

            Assert.That(back.Pages[0].PlayerId, Is.EqualTo(1));
            Assert.That(back.Pages[0].PatientId, Is.EqualTo(100));
            Assert.That(back.Pages[0].SnapshotTick, Is.EqualTo(3456L));
            Assert.That(back.Pages[0].RecheckTick, Is.EqualTo(7890L));
            Assert.That(back.Pages[1].PlayerId, Is.EqualTo(2));
            Assert.That(back.Pages[1].PatientId, Is.EqualTo(100));
            Assert.That(back.Pages[2].PatientId, Is.EqualTo(101));
            Assert.That(back.Pages[2].Folded, Is.EqualTo(1));
            Assert.That(back.Pages[2].IsFolded, Is.True);
        }

        [Test]
        public void test_roundtrip_encodeDecodeEncode_isStable()
        {
            // 二次编码必须与首次**逐字节**相同(规范形唯一的实测面,不是推断)
            var snapshot = new CasebookVolumeSnapshot(
                new[] { new CasebookVolumeRecord(3, 1), new CasebookVolumeRecord(1, 2) },
                new[] { new CasebookPageRecord(3, 9, 1L, 2L, 0), new CasebookPageRecord(1, 9, 3L, 4L, 1) });

            byte[] first = CasebookSegment.Encode(snapshot);
            CasebookVolumeSnapshot decoded = CasebookSegment.Decode(first, 0, out _);
            byte[] second = CasebookSegment.Encode(decoded);

            Assert.That(second, Is.EqualTo(first), "编码 → 解码 → 编码 必须收敛,否则字节级对拍无从成立");
        }

        // ══════════════ 2. 空段 ══════════════

        [Test]
        public void test_emptySegment_isEightBytesAndRoundTrips()
        {
            Assert.That(CasebookSegment.EmptySize, Is.EqualTo(8), "空段 = 两个 i32 count 前缀");

            byte[] bytes = CasebookSegment.Encode(null);
            Assert.That(bytes.Length, Is.EqualTo(8));

            CasebookVolumeSnapshot back = CasebookSegment.Decode(bytes, 0, out int consumed);
            Assert.That(consumed, Is.EqualTo(8));
            Assert.That(back.IsEmpty, Is.True);
        }

        [Test]
        public void test_nullSnapshotAndEmptySnapshot_encodeIdentically()
        {
            byte[] a = CasebookSegment.Encode(null);
            byte[] b = CasebookSegment.Encode(CasebookVolumeSnapshot.Empty);
            Assert.That(b, Is.EqualTo(a), "「无分册态」只有一种字节形态");
        }

        // ══════════════ 3. tick 边界 ══════════════

        [Test]
        public void test_tickExtremes_roundTripWithoutLoss()
        {
            var pages = new[]
            {
                new CasebookPageRecord(1, 1, long.MinValue, long.MaxValue, 0),
                new CasebookPageRecord(1, 2, 0L, -1L, 1),
                new CasebookPageRecord(1, 3, long.MaxValue, long.MinValue, 0),
            };

            byte[] bytes = CasebookSegment.Encode(new CasebookVolumeSnapshot(null, pages));
            CasebookVolumeSnapshot back = CasebookSegment.Decode(bytes, 0, out _);

            Assert.That(back.Pages[0].SnapshotTick, Is.EqualTo(long.MinValue));
            Assert.That(back.Pages[0].RecheckTick, Is.EqualTo(long.MaxValue));
            Assert.That(back.Pages[1].SnapshotTick, Is.EqualTo(0L));
            Assert.That(back.Pages[1].RecheckTick, Is.EqualTo(-1L));
            Assert.That(back.Pages[2].SnapshotTick, Is.EqualTo(long.MaxValue));
            Assert.That(back.Pages[2].RecheckTick, Is.EqualTo(long.MinValue));
        }

        // ══════════════ 4. 整数域(AC-39 的反射面)══════════════

        [Test]
        public void test_segmentTypes_haveNoFloatingPoint()
        {
            Type[] types =
            {
                typeof(CasebookVolumeRecord), typeof(CasebookPageRecord), typeof(CasebookVolumeSnapshot),
            };

            var offenders = new List<string>();
            foreach (Type t in types)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    AssertNoFloat(f.FieldType, $"{t.Name}.{f.Name}", offenders);
                }
                // 属性(快照的 Volumes / Pages 是集合 —— 实参也要递进去)
                foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    // 集合属性只扫元素类型(`IReadOnlyList<T>`),不扫集合本体的接口名
                    Type elem = p.PropertyType.IsGenericType
                        ? p.PropertyType.GetGenericArguments()[0]
                        : p.PropertyType;
                    AssertNoFloat(elem, $"{t.Name}.{p.Name}", offenders);
                }
            }

            Assert.That(types.Length, Is.GreaterThan(0), "扫描面为空 —— 本判据会空转通过(F4)");
            Assert.That(offenders, Is.Empty,
                "分册态段的类型里出现浮点(ADR-006 §五 / AC-39 的整数纪律):\n" + string.Join("\n", offenders));
        }

        [Test]
        public void test_segmentTypes_scannerActuallyCatchesFloat()
        {
            // 负控:证明上面那条扫描**会红**(F4 —— 只有正例的扫描器无法区分「零命中」与「没在扫」)
            var offenders = new List<string>();
            // ⚠️ 必须传**字段类型**,不是宿主类型 —— 首版传了 `typeof(FloatLeak)`(结构体本身),
            //    扫描器当然扫不到 float,负控随之失效。**这条负控在自己的首跑里就抓住了这个错**。
            FieldInfo field = typeof(FloatLeak).GetField(nameof(FloatLeak.Amount));
            Assert.That(field, Is.Not.Null, "夹具字段不存在 —— 负控会空转");
            AssertNoFloat(field.FieldType, "FloatLeak.Amount", offenders);
            Assert.That(offenders, Is.Not.Empty, "扫描器抓不住 float ⇒ 上一条的正例无判别力");
            StringAssert.Contains("float", string.Join("|", offenders).ToLowerInvariant());
        }

        // ══════════════ 5. 规范形(编码侧排序 + 拒绝重复)══════════════

        [Test]
        public void test_encode_sortsIndependentlyOfInputOrder()
        {
            var a = new CasebookVolumeSnapshot(
                new[] { new CasebookVolumeRecord(1, 0), new CasebookVolumeRecord(2, 0) },
                new[] { new CasebookPageRecord(1, 10, 0, 0, 0), new CasebookPageRecord(1, 11, 0, 0, 0) });
            var b = new CasebookVolumeSnapshot(
                new[] { new CasebookVolumeRecord(2, 0), new CasebookVolumeRecord(1, 0) },
                new[] { new CasebookPageRecord(1, 11, 0, 0, 0), new CasebookPageRecord(1, 10, 0, 0, 0) });

            Assert.That(CasebookSegment.Encode(b), Is.EqualTo(CasebookSegment.Encode(a)),
                "同态快照必须产出同一字节流,与调用方给的顺序无关");
        }

        [Test]
        public void test_encode_duplicatePlayerId_isRejected()
        {
            var bad = new CasebookVolumeSnapshot(
                new[] { new CasebookVolumeRecord(1, 0), new CasebookVolumeRecord(1, 3) }, null);

            var ex = Assert.Throws<ArgumentException>(() => CasebookSegment.Encode(bad));
            StringAssert.Contains("分册键", ex.Message);
        }

        [Test]
        public void test_encode_duplicatePatientPage_isRejected()
        {
            var bad = new CasebookVolumeSnapshot(null,
                new[] { new CasebookPageRecord(1, 5, 0, 0, 0), new CasebookPageRecord(1, 5, 1, 1, 1) });

            Assert.Throws<ArgumentException>(() => CasebookSegment.Encode(bad));
        }

        // ══════════════ 6. 解码侧拒收(不静默容错)══════════════

        [Test]
        public void test_decode_negativeVolumeCount_isRejected()
        {
            var bytes = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, 0, 4), -1);
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, 4, 4), 0);

            Assert.Throws<InvalidDataException>(() => CasebookSegment.Decode(bytes, 0, out _));
        }

        [Test]
        public void test_decode_hugeCount_isRejectedBeforeAllocation()
        {
            // 损坏的 count 不得触发巨额分配 —— 判据是「按剩余字节复验」而非「运气好」
            var bytes = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, 0, 4), int.MaxValue);
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(bytes, 4, 4), 0);

            var ex = Assert.Throws<InvalidDataException>(() => CasebookSegment.Decode(bytes, 0, out _));
            StringAssert.Contains("已损坏", ex.Message);
        }

        [Test]
        public void test_decode_unsortedVolumes_isRejected()
        {
            // 手搓一段「分册键 2 在前、1 在后」的字节 —— 规范形要求严格升序
            var w = new List<byte>();
            w.AddRange(Int32(2));                                       // volumeCount = 2
            w.AddRange(Int32(2)); w.AddRange(Int32(5));                 // player=2, page=5
            w.AddRange(Int32(1)); w.AddRange(Int32(0));                 // player=1, page=0 ← 乱序
            w.AddRange(Int32(0));                                       // pageCount = 0
            byte[] bytes = w.ToArray();

            var ex = Assert.Throws<InvalidDataException>(() => CasebookSegment.Decode(bytes, 0, out _));
            StringAssert.Contains("升序", ex.Message);
        }

        [Test]
        public void test_decode_badFoldState_isRejected()
        {
            var w = new List<byte>();
            w.AddRange(Int32(0));                                       // volumeCount = 0
            w.AddRange(Int32(1));                                       // pageCount = 1
            w.AddRange(Int32(1));                                       // playerId
            w.AddRange(Int32(7));                                       // patientId
            w.AddRange(Int64(0L));                                      // snapshotTick
            w.AddRange(Int64(0L));                                      // recheckTick
            w.AddRange(Int32(2));                                       // folded = 2 ← 非法

            Assert.Throws<InvalidDataException>(() => CasebookSegment.Decode(w.ToArray(), 0, out _));
        }

        [Test]
        public void test_decode_truncatedSegment_isRejected()
        {
            Assert.Throws<InvalidDataException>(() => CasebookSegment.Decode(new byte[3], 0, out _));
        }

        // ══════════════ 7. F9:`default(struct)` 绕过构造器守卫(如实登记)══════════════

        [Test]
        public void test_defaultStruct_bypassesConstructorGuard_isKnownAndHarmlessHere()
        {
            // 构造器守卫拒 folded ∉ {0,1},但 `default(CasebookPageRecord)` 不跑构造器。
            // 本件**不假装它被守住** —— 只钉住「绕过后的值是安全的」:
            // default 的 Folded = 0(摊开),即「未折叠」,不是第三态。
            CasebookPageRecord d = default;
            Assert.That(d.Folded, Is.EqualTo(0));
            Assert.That(d.IsFolded, Is.False);
            // 而真的第三态在**解码路径**被拒(见 test_decode_badFoldState_isRejected)——
            // 存档字节进不来第三态;只有代码里手抄 default 才可能,而那是摊开态,语义安全。
        }

        // ══════════════ 8. 迁移 v6 → v7 ══════════════

        [Test]
        public void test_migrationV6ToV7_insertsEmptySegmentAfterHeader()
        {
            byte[] v6 = MakeV6Save(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD });   // 尾部 4 字节假事件段

            byte[] v7 = new SaveMigrationV6ToV7().Migrate(v6);

            Assert.That(v7.Length, Is.EqualTo(v6.Length + 8), "只插 8 字节");
            Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(new ReadOnlySpan<byte>(v7, 4, 4)), Is.EqualTo(7u));

            int headerSize = SaveHeaderCodec.FixedSizeFor(6);   // 100
            // 插入点 = **紧跟头部**(配置版本段之前)—— 见 `SaveMigrationV6ToV7` 文件头的改判说明
            int at = headerSize;
            Assert.That(BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(v7, at, 4)), Is.EqualTo(0));
            Assert.That(BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(v7, at + 4, 4)), Is.EqualTo(0));

            // 尾部字节逐位不变(迁移链的核心不变量)。
            // ⚠️ 末尾 4 字节即夹具塞进去的「假事件段」—— 后面的配置版本段(4 B,count=0)也在尾之前,
            //    所以用「取末尾」而不是「按偏移算」,免得把两段的次序写死在断言里。
            Assert.That(v7.Skip(v7.Length - 4).ToArray(), Is.EqualTo(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }));
        }

        [Test]
        public void test_migrationV6ToV7_rejectsWrongVersion()
        {
            byte[] v6 = MakeV6Save(Array.Empty<byte>());
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(v6, 4, 4), 5u);   // 伪造成 v5

            Assert.Throws<InvalidOperationException>(() => new SaveMigrationV6ToV7().Migrate(v6));
        }

        [Test]
        public void test_migrationV6ToV7_rejectsWrongMagic()
        {
            byte[] v6 = MakeV6Save(Array.Empty<byte>());
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(v6, 0, 4), 0xDEADBEEFu);

            Assert.Throws<InvalidOperationException>(() => new SaveMigrationV6ToV7().Migrate(v6));
        }

        [Test]
        public void test_migrationV6ToV7_doesNotDependOnConfigSegment()
        {
            // 插入点是**头部锚定**的(100),不解析配置版本段 —— 这是刻意的:
            // 越晚插入,`SaveMigrationChain` 的「正文末尾等长」窗口越可能横跨插入点。
            // 本测钉住这条性质:即使配置版本段的字节是垃圾,迁移也照做,且尾部逐位保留。
            byte[] tail = { 0x11, 0x22, 0x33, 0x44, 0x55 };
            byte[] v6 = MakeV6Save(tail);
            BinaryPrimitives.WriteInt32LittleEndian(
                new Span<byte>(v6, SaveHeaderCodec.FixedSizeFor(6), 4), -1);   // 「垃圾」配置段

            byte[] v7 = new SaveMigrationV6ToV7().Migrate(v6);

            Assert.That(v7.Length, Is.EqualTo(v6.Length + 8));
            Assert.That(v7.Skip(v7.Length - tail.Length).ToArray(), Is.EqualTo(tail),
                "尾部必须逐位保留(迁移链的核心不变量)");
        }

        // ══════════════ 9. AC-6:39 零 `IEventSink.Append`(分册态不进三流)══════════════

        [Test]
        public void test_ac6_casebookModule_hasZeroAppendCallSites()
        {
            // 判据 = **IL 调用点**,不是 grep 源文本(形制承 `camera_presentation_discipline_test`
            // 的 `ILBodyScanner.ContainsMethodCall`)。分册态是「三流之外的第四类数据」
            // (ADR-010 §三 义务 13)—— 一旦 39 自己 Append,它就成了第四条流。
            Type[] casebookTypes = typeof(CaseListOrdering).Assembly
                .GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Gameplay.UI.Casebook")
                .ToArray();

            // F4:扫描面必须非空,否则循环零次 ⇒ 恒绿
            Assert.That(casebookTypes.Length, Is.GreaterThan(0),
                "39 的命名空间里零类型 —— 扫描面丢失,本判据会空转通过");

            int hits = 0;
            var offenders = new List<string>();
            foreach (Type t in casebookTypes)
            {
                foreach (MethodInfo m in t.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.Instance | BindingFlags.Static |
                             BindingFlags.DeclaredOnly))
                {
                    if (DaYiJingCheng.Tests.PlayerController.ILBodyScanner.ContainsMethodCall(m, "Append"))
                    {
                        hits++;
                        offenders.Add($"{t.Name}.{m.Name}");
                    }
                }
            }

            Assert.That(hits, Is.EqualTo(0),
                "39 调用了 `Append` —— 分册态必须留在第四类数据里,不进三流(ADR-010 义务 13):\n"
                + string.Join("\n", offenders));
        }

        // ── 夹具 ──

        /// <summary>造一份**最小 v6 存档体**(无校验和):头部 100 B + 配置版本段 count=0 + 任意尾部。</summary>
        private static byte[] MakeV6Save(byte[] tail)
        {
            int headerSize = SaveHeaderCodec.FixedSizeFor(6);
            var data = new byte[headerSize + 4 + tail.Length];

            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(data, 0, 4), SaveHeader.Magic);
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(data, 4, 4), 6u);
            BinaryPrimitives.WriteUInt64LittleEndian(new Span<byte>(data, 8, 8), 0x0123456789ABCDEFUL);
            // 配置版本段:count = 0
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(data, headerSize, 4), 0);

            Array.Copy(tail, 0, data, headerSize + 4, tail.Length);
            return data;
        }

        private static byte[] Int32(int value)
        {
            var b = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(b, 0, 4), value);
            return b;
        }

        private static byte[] Int64(long value)
        {
            var b = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(b, 0, 8), value);
            return b;
        }

        /// <summary>递归判浮点(含泛型实参)—— 扫不到或恒假就是 F4 空洞正控。</summary>
        private static void AssertNoFloat(Type t, string path, List<string> offenders)
        {
            if (t == null) return;
            if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            {
                offenders.Add($"{path}: {t.Name}");
                return;
            }
            if (t.IsGenericType)
            {
                foreach (Type arg in t.GetGenericArguments())
                    AssertNoFloat(arg, path + "<" + arg.Name + ">", offenders);
            }
        }

        private struct FloatLeak
        {
            // ReSharper disable once NotAccessedField.Local —— 反射扫描不执行代码
            public float Amount;
        }
    }
}
