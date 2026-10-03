// ADR-029 接线支测试 —— modular-building Story 007(闭合 B4)
//
// AC-23-31/32/33: Place / Remove / Modify 三处写入改走 IPayloadEncoder
// AC-23-34: StructureKinds.cs 内 `new PayloadRef(` 零命中
// AC-23-35: 三支载荷 Encode → Decode 逐字段往返 + ModifiedFields 位掩码语义
// AC-23-36: Sim 引用集仍 = ["Sim.Contracts"];b2 门绿
// AC-23-37: 注册表语义不回归(既有 51 例)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class StructurePayloadEncoderTest
    {
        private InMemoryBlobPool _pool;
        private PayloadEncoder _encoder;
        private StructureInstanceRegistry _registry;
        private StructureWriter _writer;
        private List<SimEvent> _events;

        [SetUp]
        public void Setup()
        {
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _registry = new StructureInstanceRegistry();
            _events = new List<SimEvent>();
            _writer = new StructureWriter(
                new CapturingSink(_events), _registry, _encoder,
                new MinimalModuleCatalog(), new NoopOccupancy());   // N-r1:注入以过 fail-closed
        }

        private T PayloadOf<T>(SimEvent evt) where T : struct
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _pool, out T p),
                $"{evt.Kind}: 载荷须能经池 + codec 取回");
            return p;
        }

        // ══════════ AC-23-31/32/33: 三处写入改走 encoder ══════════

        [Test]
        public void test_ac2331_place_encodesFiveFields()
        {
            int sid = _writer.Place(new WorldPos(3, 0, 4), moduleId: 70000,
                                    orientation: 90, variant: 2, tick: 100);

            Assert.AreEqual(1, _events.Count);
            var p = PayloadOf<StructurePlacedPayload>(_events[0]);
            Assert.AreEqual(sid, p.StructureId);
            Assert.AreEqual(new WorldPos(3, 0, 4), p.Cell);
            Assert.AreEqual(70000, p.ModuleId);
            Assert.AreEqual(90, p.Orientation);
            Assert.AreEqual(2, p.Variant);
        }

        [Test]
        public void test_ac2332_remove_encodesThreeFields()
        {
            int sid = _writer.Place(new WorldPos(5, 0, 6), 7, 180, 1, 100);
            _events.Clear();

            Assert.IsTrue(_writer.Remove(sid, 101));
            var p = PayloadOf<StructureRemovedPayload>(_events[0]);
            Assert.AreEqual(sid, p.StructureId);
            Assert.AreEqual(new WorldPos(5, 0, 6), p.Cell);
            Assert.AreEqual(7, p.ModuleId);
        }

        [Test]
        public void test_ac2333_modify_encodesOrientationAndVariant()
        {
            // 🔴 B4 的核心缺陷:原实现把 orientation / variant **算出却未放进载荷**
            //    (只写 offset: 0)⇒ 两字段被静默丢弃。本测即该缺陷的回归夹具。
            int sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _events.Clear();

            Assert.IsTrue(_writer.Modify(sid, newOrientation: 270, newVariant: 3, tick: 200));
            var p = PayloadOf<StructureModifiedPayload>(_events[0]);

            Assert.AreEqual(sid, p.StructureId);
            Assert.AreEqual(270, p.NewOrientation, "新朝向须真的进载荷(原实现丢弃)");
            Assert.AreEqual(3, p.NewVariant, "新变体须真的进载荷(原实现丢弃)");
            Assert.AreEqual(0b11, p.ModifiedFields, "两维皆改 ⇒ 掩码 = 3");
        }

        [Test]
        public void test_ac2333_modify_onlyOrientation_maskIs1()
        {
            int sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _events.Clear();

            _writer.Modify(sid, newOrientation: 90, newVariant: null, tick: 200);
            var p = PayloadOf<StructureModifiedPayload>(_events[0]);
            Assert.AreEqual(1, p.ModifiedFields, "仅朝向 ⇒ 掩码 = 1");
            Assert.AreEqual(90, p.NewOrientation);
            Assert.AreEqual(0, p.NewVariant, "未改的变体保持原值(0)");
        }

        [Test]
        public void test_ac2333_modify_onlyVariant_maskIs2()
        {
            int sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _events.Clear();

            _writer.Modify(sid, newOrientation: null, newVariant: 5, tick: 200);
            var p = PayloadOf<StructureModifiedPayload>(_events[0]);
            Assert.AreEqual(2, p.ModifiedFields, "仅变体 ⇒ 掩码 = 2");
            Assert.AreEqual(5, p.NewVariant);
        }

        // ══════════ AC-23-34: 手搓面消除 ══════════

        [Test]
        public void test_ac2334_noManualPayloadRefInSource()
        {
            string src = Path.Combine(Application.dataPath, "Sim/World/StructureKinds.cs");
            Assert.IsTrue(File.Exists(src), "StructureKinds.cs 应存在");

            string code = StripComments(File.ReadAllText(src));
            Assert.IsFalse(code.Contains("new PayloadRef("),
                "StructureKinds.cs 内不得手搓 PayloadRef(ADR-029 §③)—— 唯一合法路径 = IPayloadEncoder");
        }

        [Test]
        public void test_ac2334_deadDuplicateStructsRemoved()
        {
            // 三支 payload struct 曾**重复定义**于 Sim.World 与 Sim.Contracts
            // (StructureId 类型不同:int vs long);契约版才是权威,Sim 版零引用。
            var simWorld = typeof(StructureWriter).Assembly;
            var dups = simWorld.GetTypes()
                .Where(t => t.Namespace == "DaYiJingCheng.Sim.World" &&
                            (t.Name == "StructurePlacedPayload" ||
                             t.Name == "StructureRemovedPayload" ||
                             t.Name == "StructureModifiedPayload"))
                .ToList();

            Assert.IsEmpty(dups,
                "Sim.World 内不得再有三支 payload struct 副本(死代码,已删):" +
                string.Join(", ", dups.Select(t => t.Name)));

            // 权威版须在 Sim.Contracts 且 StructureId 为 long
            Assert.AreEqual(typeof(long),
                typeof(StructurePlacedPayload).GetField("StructureId").FieldType,
                "契约版 StructureId 须为 long(codec 按此宽度编码)");
        }

        [Test]
        public void test_ac2334_writerRequiresEncoder()
        {
            Assert.Throws<ArgumentNullException>(
                () => new StructureWriter(new CapturingSink(new List<SimEvent>()), _registry, null),
                "encoder 为 null 须抛(唯一编码路径不可省)");
        }

        // ══════════ AC-23-35: 往返 + 位掩码(含 bit-pack 负例)══════════

        [Test]
        public void test_ac2335_placedRoundTrip_largeModuleIdNoTruncation()
        {
            // 🔴 bit-pack 负例:`moduleId | (orientation << 16) | (variant << 24)`
            //    在 moduleId > 65535 时与 orientation 位**冲突**(静默截断)。
            //    codec 路径须无此问题。
            var payload = new StructurePlacedPayload(
                123456L, new WorldPos(9, 0, 9), moduleId: 70000, orientation: 90, variant: 2);

            var reference = _encoder.Encode(EventKind.StructurePlaced, payload);
            Assert.IsTrue(_pool.TryGetBlob(reference.BlobId, out var stored));
            var d = PayloadCodec.Decode<StructurePlacedPayload>(EventKind.StructurePlaced, stored.Span);

            Assert.AreEqual(70000, d.ModuleId, "大 moduleId 不得被高位字段截断(bit-pack 缺陷)");
            Assert.AreEqual(90, d.Orientation, "朝向不得被 moduleId 溢出污染");
            Assert.AreEqual(2, d.Variant);
            Assert.AreEqual(123456L, d.StructureId);
        }

        [Test]
        public void test_ac2335_zeroOrientationDistinguishableFromUnset()
        {
            // bit-pack 的固有缺陷:orientation=0 时与「未设」不可区分。
            // codec 路径以独立字段承载 ⇒ 必须可区分(此处以 ModifiedFields 区分)。
            var withZero = new StructureModifiedPayload(
                1L, new WorldPos(0, 0, 0), 5, newOrientation: 0, newVariant: 7, modifiedFields: 0b01);
            var withoutOri = new StructureModifiedPayload(
                1L, new WorldPos(0, 0, 0), 5, newOrientation: 0, newVariant: 7, modifiedFields: 0b10);

            var a = RoundTrip(EventKind.StructureModified, withZero);
            var b = RoundTrip(EventKind.StructureModified, withoutOri);

            Assert.AreEqual(0b01, a.ModifiedFields);
            Assert.AreEqual(0b10, b.ModifiedFields);
            Assert.AreNotEqual(a.ModifiedFields, b.ModifiedFields,
                "「改了朝向为 0」与「没改朝向」须可区分(bit-pack 做不到)");
        }

        [Test]
        public void test_ac2335_allThreeKindsRoundTrip()
        {
            var placed = new StructurePlacedPayload(11L, new WorldPos(1, 2, 3), 4, 180, 5);
            var removed = new StructureRemovedPayload(11L, new WorldPos(1, 2, 3), 4);
            var modified = new StructureModifiedPayload(11L, new WorldPos(1, 2, 3), 4, 270, 6, 0b11);

            AssertPayloadsEqual(placed, RoundTrip(EventKind.StructurePlaced, placed));
            AssertPayloadsEqual(removed, RoundTrip(EventKind.StructureRemoved, removed));
            AssertPayloadsEqual(modified, RoundTrip(EventKind.StructureModified, modified));
        }

        // ══════════ AC-23-36: 装配边界 ══════════

        [Test]
        public void test_ac2336_simReferenceSetUnchanged()
        {
            string asmdefPath = Path.Combine(Application.dataPath, "../Assets/Sim/Sim.asmdef");
            string content = File.ReadAllText(asmdefPath);
            Assert.IsFalse(content.Contains("Sim.Codec"),
                "Sim.asmdef 不得引用 Sim.Codec(ADR-025 §①:111)");

            var sim = typeof(StructureWriter).Assembly;
            var refs = sim.GetReferencedAssemblies().Select(r => r.Name).ToList();
            Assert.IsFalse(refs.Contains("Sim.Codec"), "Sim 运行时引用集不得含 Sim.Codec");
            Assert.IsTrue(refs.Contains("Sim.Contracts"));
        }

        // ══════════ 辅助 ══════════

        private T RoundTrip<T>(EventKind kind, T payload) where T : struct
        {
            var reference = _encoder.Encode(kind, payload);
            Assert.IsTrue(_pool.TryGetBlob(reference.BlobId, out var stored),
                $"{kind}: 字节须真的进池");
            return PayloadCodec.Decode<T>(kind, stored.Span);
        }

        private static void AssertPayloadsEqual(object expected, object actual)
        {
            foreach (var f in expected.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.AreEqual(f.GetValue(expected), f.GetValue(actual), f.Name);
        }

        /// <summary>剥掉行注释与块注释(判据须扫**代码**,不扫注释里的说明文字)。</summary>
        private static string StripComments(string src)
        {
            var noBlock = System.Text.RegularExpressions.Regex.Replace(
                src, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            return System.Text.RegularExpressions.Regex.Replace(noBlock, @"//.*?$", "",
                System.Text.RegularExpressions.RegexOptions.Multiline);
        }


    /// <summary>
    /// 最小模块目录 + 占位 occupancy —— 供本文件既有用例满足 fail-closed(N-r1)。
    /// ⚠️ 2026-10-03:fail-closed 要求生产路径必注入目录/占用表;
    /// 本文件测的是**载荷编码**与**实例表语义**,与足迹无关,故注入最小夹具
    /// (而非给 fail-closed 开逃生舱 —— 那会让 C1/C2 的静默退化重新合法化)。
    /// </summary>
    internal sealed class MinimalModuleCatalog : IModuleCatalog
    {
        private readonly System.Collections.Generic.Dictionary<int, ModuleDefinition> _defs =
            new System.Collections.Generic.Dictionary<int, ModuleDefinition>();
        public void Add(int id, WorldPos[] local) =>
            _defs[id] = new ModuleDefinition(id, SlotType.Decor, local, new[] { 0, 90, 180, 270 });
        /// <summary>任意 id 都返回单格定义(本文件不测足迹)。</summary>
        public ModuleDefinition? GetModuleDefinition(int moduleId)
        {
            if (!_defs.ContainsKey(moduleId)) Add(moduleId, new[] { new WorldPos(0, 0, 0) });
            return _defs[moduleId];
        }
        public bool ContainsModule(int moduleId) => true;
        public System.Collections.Generic.IReadOnlyCollection<int> GetAllModuleIds() => _defs.Keys;
    }

    /// <summary>空占位 occupancy —— 本文件不验 Overlay 写面。</summary>
    internal sealed class NoopOccupancy : IWorldOccupancy
    {
        public void OccupyCells(System.Collections.Generic.IEnumerable<WorldPos> cells) { }
        public void FreeCells(System.Collections.Generic.IEnumerable<WorldPos> cells) { }
    }

        private sealed class CapturingSink : IEventSink
        {
            private readonly List<SimEvent> _sink;
            public CapturingSink(List<SimEvent> sink) { _sink = sink; }
            public void Append(in SimEvent e) => _sink.Add(e);
        }
    }
}
