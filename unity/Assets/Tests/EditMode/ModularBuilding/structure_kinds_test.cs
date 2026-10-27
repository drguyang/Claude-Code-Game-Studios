// Story 003 测试: Structure* 世界流事件 + 实例表
//
// AC-23-02: 重放重建 slot_occupied / structure_at / Overlay 与事件序列逐位一致
// AC-23-09: 字节流稳定(encode → decode → re-encode 相等)
// AC-23-10: 载荷全整数,无 float
// AC-23-11: StructureModified 只载物理形态位

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.ModularBuilding
{
    public class StructureKindsTest
    {
        private StructureInstanceRegistry _registry;
        private StructureWriter _writer;
        private List<SimEvent> _appendedEvents;
        private InMemoryBlobPool _pool;
        private IPayloadEncoder _encoder;
        /// <summary>目录(提为字段 —— 「消耗性模块」类夹具须在 `Place` 前改造它)。</summary>
        private MinimalModuleCatalog _catalog;

        [SetUp]
        public void Setup()
        {
            _registry = new StructureInstanceRegistry();
            _appendedEvents = new List<SimEvent>();

            // ADR-029 §③:载荷编码经 IPayloadEncoder —— 测试用内存池
            _pool = new InMemoryBlobPool();
            _encoder = new PayloadEncoder(_pool);
            _catalog = new MinimalModuleCatalog();

            // Mock IEventSink
            var mockSink = new MockEventSink(evt => _appendedEvents.Add(evt));
            // ⚠️ N-r1:fail-closed 要求注入目录/占用表/掉落写者(生产路径不得静默退化)
            // ⚠️ 掉落写者用**同一个 sink** ⇒ 一条 `_appendedEvents` 里同时看得到
            //    `StructureRemoved` 与它触发的 `DropSpawned`(因果序可直接断言)。
            // ⚠️ 池必须与 `PayloadOf` 用的 `_pool` **同一个** —— 否则本文件回读载荷
            //    会从错误的池里取到零值(2026-10-27 实测:事件在、字段全 0)。
            _writer = new StructureWriter(mockSink, _registry, _encoder,
                                          _catalog, new NoopOccupancy(),
                                          DaYiJingCheng.Tests.TestDropWriter.Create(mockSink, _pool));
        }

        /// <summary>经池 + codec 取回强类型载荷(ADR-029 的合法读路径)。</summary>
        private T PayloadOf<T>(SimEvent evt) where T : struct
        {
            Assert.IsTrue(PayloadCodec.TryGetPayload(evt, _pool, out T p),
                $"{evt.Kind}: 载荷须能经池 + codec 取回(字节真的进了池)");
            return p;
        }

        // AC-23-10: Place 事件载荷全整数
        [Test]
        public void test_place_emitsStructurePlaced()
        {
            long sid = _writer.Place(new WorldPos(0, 0, 0), moduleId: 1, orientation: 0, variant: 0, tick: 100);

            Assert.AreEqual(1, _appendedEvents.Count);
            Assert.AreEqual(EventKind.StructurePlaced, _appendedEvents[0].Kind);
            Assert.AreEqual(PatientId.None, _appendedEvents[0].Patient);

            // ⚠️ 2026-10-02(ADR-029 接线支):原断言为 `Payload.BlobId == sid` ——
            //    那是把**业务 id 耦合到 blob 寻址**(正是 B4 缺陷的形态:poiId 当 blobId)。
            //    codec 路径下 BlobId 是池索引,与 structure_id 无关 ⇒ 改为解码验字段。
            var p = PayloadOf<StructurePlacedPayload>(_appendedEvents[0]);
            Assert.AreEqual(sid, p.StructureId, "载荷须载真实 structure_id");
            Assert.AreEqual(new WorldPos(0, 0, 0), p.Cell, "载荷须载锚点格");
            Assert.AreEqual(1, p.ModuleId);
        }

        // AC-23-10: Remove 事件
        // ⚠️ 2026-10-27 R17 闭合批:断言从「恰 1 条」改为「恰 2 条且因果有序」——
        //    拆除触发的返还 `DropSpawned` 是**新增的正确行为**(EC-23-11 / AC-23-16)。
        //    `MinimalModuleCatalog` 的 cost = 1、R = 1.0 ⇒ refund = 1 ⇒ 必发一条。
        //    「不吞返还」的正向判据见 `test_r17_remove_emitsRefundDropSpawned`。
        [Test]
        public void test_remove_emitsStructureRemoved()
        {
            long sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _appendedEvents.Clear();

            bool removed = _writer.Remove(sid, 101, DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio);
            Assert.IsTrue(removed);
            Assert.AreEqual(2, _appendedEvents.Count,
                "拆除 = StructureRemoved + 返还 DropSpawned 两条(2026-10-27 R17 闭合)");
            Assert.AreEqual(EventKind.StructureRemoved, _appendedEvents[0].Kind,
                "拆除是**因**,须先发");
            Assert.AreEqual(EventKind.DropSpawned, _appendedEvents[1].Kind,
                "返还是**果**,须后发(同 tick 相邻 Seq 由写入序自然得到)");
        }

        // AC-23-10: Remove 不存在的 id 返回 false
        [Test]
        public void test_remove_nonexistent_returnsFalse()
        {
            bool removed = _writer.Remove(999, 100, DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio);
            Assert.IsFalse(removed);
            Assert.AreEqual(0, _appendedEvents.Count);
        }

        // ══════════ AC-23-16:拆除返还走 DropSpawned(spy-sink · 2026-10-27)══════════
        //
        // 判据载体说明(`modular-building.md:558` AC-23-16 的评审判定):
        //   「`refund_cases.json` 含『20 库存满』用例 + spy-sink 断言 `DropSpawned` 存在;
        //     `DropSpawned` 发出者 = 20 结算(23 触发),归 20 侧测试文件」
        //   ⇒ 本组夹具断的正是「23 侧触发**真的发生了**」。20 侧的落地(载荷字节、
        //     id 铸造)由 `DropEventWriter` 自己的测试覆盖,本文件不重复。

        /// <summary>AC-23-16 正向:拆除返还必须发出一条 <c>DropSpawned</c>,
        /// 且 <c>qty</c> / <c>item_key</c> / <c>spawn_anchor</c> 三者逐项正确。</summary>
        [Test]
        public void test_r17_remove_emitsRefundDropSpawned()
        {
            var anchor = new WorldPos(4, 0, 7);
            long sid = _writer.Place(anchor, 1, 0, 0, tick: 100);
            _appendedEvents.Clear();

            _writer.Remove(sid, tick: 101,
                DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio);

            // ⚠️ **因果序**(2026-10-27 证伪轮补):变异 Q 把 `EmitRefund` 挪到
            //    `StructureRemoved` 的 `Append` **之前**(果先于因),本条原先用
            //    `Single(e => e.Kind == DropSpawned)` 按种类定位 ⇒ **对顺序零判别力**,
            //    当时只靠另一条夹具的 `[0]/[1]` 下标断言才转红。此处把顺序钉在本条自己身上:
            //    「拆除是因、返还是果」必须可由本条独立判定。
            Assert.AreEqual(EventKind.StructureRemoved, _appendedEvents[0].Kind,
                "因须先于果:第一条是拆除");
            Assert.AreEqual(EventKind.DropSpawned, _appendedEvents[1].Kind,
                "因须先于果:第二条才是返还(同 tick 相邻 Seq 由写入序自发)");

            SimEvent drop = _appendedEvents.Single(e => e.Kind == EventKind.DropSpawned);
            DropSpawnedPayload p = PayloadOf<DropSpawnedPayload>(drop);

            // MinimalModuleCatalog: cost = 1, buildItemKey = 0;R = 1.0 ⇒ refund = 1
            Assert.AreEqual(1, p.Qty, "Refund = ⌊cost × R⌋ = ⌊1 × 1.0⌋ = 1(F-23-3)");
            Assert.AreEqual(0, p.ItemKey, "item_key 取自 module.BuildItemKey(不是猜的)");
            Assert.AreEqual(anchor, p.SpawnAnchor, "返还物落在**被拆结构的锚点格**(地面掉落)");
            Assert.AreEqual(PatientId.None, drop.Patient, "世界流事件不污染 max(patient_id)");
        }

        /// <summary>AC-23-16 反向:结构不存在 ⇒ 零事件(<b>不</b>发幻影返还)。</summary>
        [Test]
        public void test_r17_removeNonexistent_emitsNoRefund()
        {
            Assert.IsFalse(_writer.Remove(999, tick: 100,
                DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio));
            Assert.AreEqual(0, _appendedEvents.Count,
                "拆不存在的结构 ⇒ 既无 StructureRemoved 也无 DropSpawned");
        }

        /// <summary>AC-23-16 边界:<c>R = 0</c> ⇒ <c>Refund = 0</c> ⇒ **不发** <c>DropSpawned</c>
        /// (qty = 0 的实例不该存在,`DropEventWriter` 会直接拒)。</summary>
        [Test]
        public void test_r17_zeroRefundRatio_emitsStructureRemovedOnly()
        {
            long sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, tick: 100);
            _appendedEvents.Clear();

            Assert.IsTrue(_writer.Remove(sid, tick: 101, demolishRefundRatio: 0),
                "零返还率下拆除**本身仍成功** —— 只是无物可返");

            Assert.AreEqual(1, _appendedEvents.Count);
            Assert.AreEqual(EventKind.StructureRemoved, _appendedEvents[0].Kind,
                "Refund = 0 ⇒ 无 DropSpawned(零返还 ≠ 吞返还:没有可返还之物)");
        }

        /// <summary>AC-23-16 边界(EC-23-08):<c>refundable = 0</c> 的**消耗性**模块 ⇒
        /// 拆除仍成功、<b>但不发</b> <c>DropSpawned</c>(消耗品本无物可返)。</summary>
        /// <remarks>
        /// ⚠️ **本条是 2026-10-27 证伪轮补的**:变异 K(停用 `Refundable == 0` 守卫)
        /// 在加入本条之前**全绿 96/96** ⇒ 该守卫零判别力。根因在夹具而非产品:
        /// `MinimalModuleCatalog.Add` 走 `ModuleDefinition` 的可选形参 `refundable`,恒取 1,
        /// 于是**没有任何夹具造得出消耗性模块**。⇒ 同批给目录加了 `AddConsumable`。
        /// <para>与 <see cref="test_r17_zeroRefundRatio_emitsStructureRemovedOnly"/> 的区别:
        /// 那条是**返还率 = 0**(玩家侧调参),本条是**模块不可返**(内容侧属性)——
        /// 两条走的是 `EmitRefund` 里**不同**的守卫,须各有一条变异。</para>
        /// </remarks>
        [Test]
        public void test_r17_nonRefundableModule_emitsNoDropSpawned()
        {
            const int ConsumableModuleId = 77;
            _catalog.AddConsumable(ConsumableModuleId, new[] { new WorldPos(0, 0, 0) });
            long sid = _writer.Place(new WorldPos(2, 0, 2), ConsumableModuleId, 0, 0, tick: 100);
            _appendedEvents.Clear();

            Assert.IsTrue(_writer.Remove(sid, tick: 101,
                    DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio),
                "消耗性模块**仍可拆**(拆除不是返还的前置 —— 返还率再高也不返)");

            Assert.AreEqual(1, _appendedEvents.Count,
                "refundable = 0 ⇒ 只有 StructureRemoved(EC-23-08 的消耗性档)");
            Assert.AreEqual(EventKind.StructureRemoved, _appendedEvents[0].Kind);
            Assert.IsFalse(_appendedEvents.Any(e => e.Kind == EventKind.DropSpawned),
                "消耗性家具无返还物 —— 发 DropSpawned 会有凭空变出的材料");
        }

        /// <summary>AC-23-16 契约:<c>dropWriter</c> 为 null ⇒ **构造期** fail-closed
        /// (N-r1 同口径 —— 不可省的东西不进默认值)。</summary>
        [Test]
        public void test_r17_noDropWriter_throwsAtConstruction()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new StructureWriter(new MockEventSink(_ => { }), _registry, _encoder,
                                          new MinimalModuleCatalog(), new NoopOccupancy()),
                "未注入 DropEventWriter ⇒ 构造期即抛(不是拖到 Remove 才静默不返还)");
            // ⚠️ `ParamName` 是**形参名**(`dropWriter`),不是类型名 —— 2026-10-27 实测踩过。
            //    判据用 `Contains("dropWriter")` 而非 `"DropEventWriter"`。
            Assert.AreEqual("dropWriter", ex.ParamName,
                "须是因缺 dropWriter 形参而抛(不是别的原因被撞上)");
            StringAssert.Contains("DropEventWriter", ex.Message,
                "消息须点明缺的是什么类型 —— 只给形参名读者看不出该补哪个依赖");
        }

        // AC-23-11: Modify 只载物理形态位
        [Test]
        public void test_modify_orientation_emitsModified()
        {
            long sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _appendedEvents.Clear();

            bool modified = _writer.Modify(sid, newOrientation: 90, newVariant: null, tick: 200);
            Assert.IsTrue(modified);
            Assert.AreEqual(1, _appendedEvents.Count);
            Assert.AreEqual(EventKind.StructureModified, _appendedEvents[0].Kind);

            // StructureModified 载荷: 真实 payload 类型(ADR-024: 非 bit-packing)
            // 验证事件已写入(载荷编码由 Sim.Codec 负责,此处只验证事件存在)
            Assert.AreEqual(1, _appendedEvents.Count);
        }

        // AC-23-11: 无变更返回 false
        [Test]
        public void test_modify_noChange_returnsFalse()
        {
            long sid = _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);
            _appendedEvents.Clear();

            bool modified = _writer.Modify(sid, newOrientation: 0, newVariant: 0, tick: 200);
            Assert.IsFalse(modified);
            Assert.AreEqual(0, _appendedEvents.Count);
        }

        // AC-23-02: 重放重建实例表
        [Test]
        public void test_rebuildFromEvents_equalsLiveState()
        {
            // 构造事件序列
            _writer.Place(new WorldPos(0, 0, 0), 1, 0, 0, 100);   // id=1
            long sid2 = _writer.Place(new WorldPos(1, 0, 0), 2, 0, 0, 101); // id=2
            _writer.Remove(sid2, 102, DaYiJingCheng.Tests.TestDropWriter.P0RefundRatio);
            _writer.Modify(1, newOrientation: 90, newVariant: null, 200);

            // 重建 —— ⚠️ 2026-10-02(ADR-029 接线支):原实现读 `Payload.BlobId` 当 structure_id,
            // 且**硬编码** moduleId / anchor / 朝向(注释自陈「测试用…回填」)⇒ 那不是真重建,
            // 是照着预期答案写的。现改为**真解码**:全部字段取自载荷。
            var rebuild = new StructureInstanceRegistry();
            foreach (var evt in _appendedEvents)
            {
                if (evt.Kind == EventKind.StructurePlaced)
                {
                    var p = PayloadOf<StructurePlacedPayload>(evt);
                    rebuild.Register(p.Cell, p.ModuleId, p.Orientation, p.Variant);
                }
                else if (evt.Kind == EventKind.StructureRemoved)
                {
                    var p = PayloadOf<StructureRemovedPayload>(evt);
                    // ✅ 2026-10-03(C8/ID 已修):注册表 id 全链升 `long`,此处**不再需要收窄转换**。
                    rebuild.Remove(p.StructureId);
                }
                else if (evt.Kind == EventKind.StructureModified)
                {
                    var p = PayloadOf<StructureModifiedPayload>(evt);
                    // ModifiedFields 位掩码决定改哪一维(1=朝向, 2=变体)
                    int? newOri = (p.ModifiedFields & 1) != 0 ? p.NewOrientation : (int?)null;
                    int? newVar = (p.ModifiedFields & 2) != 0 ? p.NewVariant : (int?)null;
                    rebuild.Update(p.StructureId, newOri, newVar);   // ✅ C8/ID 已修:无需收窄
                }
            }

            // 验证: id=1 存在且朝向=90
            Assert.IsTrue(rebuild.TryGet(1, out var inst1));
            Assert.AreEqual(1, inst1.ModuleId);
            Assert.AreEqual(90, inst1.Orientation);
            Assert.AreEqual(1, rebuild.Count); // id=2 已移除,只有 id=1 存活
        }

        // 边界: 实例表查询不存在的 id
        [Test]
        public void test_tryGet_nonexistent_returnsFalse()
        {
            Assert.IsFalse(_registry.TryGet(999, out _));
        }

        // ── C8/ID 回归(2026-10-03)──────────────────────────────
        // 原缺陷:id 全链 int,与 entities.yaml:2068 的 i64 不符 ⇒ **超 2^31 静默回绕**。
        // 本测存在的理由:既有用例的 id 均「小」(1/2/999),即便实现退回 int 也**照样全绿**
        // ⇒ 判据必须**真的把 id 推过 int 上界**,否则捕获不到原缺陷。
        [Test]
        public void test_ac23_id_exceedsIntRange_survivesRoundTrip()
        {
            // 前提断言:夹具值确在 int 域外(夹具自毁守卫 —— 若写成小值,本测即失效)
            const long bigId = 4_000_000_000L; // > int.MaxValue(2_147_483_647)
            Assert.Greater(bigId, int.MaxValue, "夹具前提:该 id 必须**超出 int 域**,否则捕获不到原缺陷");

            // Act:直接把 id **推过 int 上界**再走全链 ——
            // 若实现退回 int,`id` 在注册时即回绕成负数/错误值,后续断言必红。
            Assert.IsFalse(_registry.TryGet(bigId, out _), "int 域外的 id 须可表达为查询参数(签名 = long)");
            Assert.IsFalse(_registry.Remove(bigId), "int 域外的 id 须可表达为删除参数(签名 = long)");

            // 行为半边:正常 id 全链可用(证明上面不是「一律 false」的空实现)
            long id = _registry.Register(new WorldPos(0, 0, 0), 1, 0, 0);
            Assert.IsTrue(_registry.TryGet(id, out var inst), "注册的 id 须可查回");
            Assert.AreEqual(id, inst.StructureId, "实例内 StructureId 须与注册返回**同值**(long 无截断)");
            Assert.AreEqual(1, inst.ModuleId);
            Assert.IsTrue(_registry.Remove(id), "注册的 id 须可移除");
            Assert.IsFalse(_registry.TryGet(id, out _), "移除后须查不到");
        }

        [Test]
        public void test_ac23_id_typeIsInt64_inBothRegistryAndPayload()
        {
            // 判据 = **类型身份**,非行为 —— 把「与权威件同宽度」钉成契约。
            Assert.AreEqual(typeof(long), typeof(StructureInstance).GetField("StructureId").FieldType,
                "StructureInstance.StructureId 须为 long(entities.yaml:2068 = i64)");
            Assert.AreEqual(typeof(long), typeof(StructureInstanceRegistry)
                .GetMethod("Register").ReturnType,
                "Registry.Register 返回须为 long");
            Assert.AreEqual(typeof(long), typeof(StructureWriter).GetMethod("Place").ReturnType,
                "StructureWriter.Place 返回须为 long");
            Assert.AreEqual(typeof(long), typeof(StructurePlacedPayload).GetField("StructureId").FieldType,
                "契约版载荷须同为 long(两侧同宽度 —— 原缺陷 = 一侧 int 一侧 long)");
        }
    }

    // ══════════════════════════════════════════════════════════════
    // §历史缺陷(**已闭** 2026-10-03 —— 保留原文作闭环记录)
    //
    // 【原缺陷】`StructureInstanceRegistry` 的 structure id 曾是 **int**
    //   (`_nextStructureId` / `Register` / `Remove` / `Update` / `TryGet` /
    //    `StructureInstance.StructureId` / `StructureWriter.PeekNextId`),
    //   而权威件 `design/registry/entities.yaml:2068` 定
    //   **`structure_id: i64`**,契约版载荷 `StructurePlacedPayload.StructureId`
    //   与 codec(`WriteFieldInt64`)亦皆为 **long**
    //   ⇒ int 承载 i64 语义,id 超 2^31 时静默回绕。
    //   该不一致此前被 `StructureKinds.cs` 内的 int 版 payload 副本掩盖。
    //
    // 【原处置】本测试当时用 `(int)` 收窄转换作**临时桥接**,并登记该缺口。
    //
    // 【已修】`StructureKinds.cs` 注册表 + 写者全链升 `long`(2026-10-03,评审 C8/ID):
    //   `StructureInstance.StructureId` / `_instances` 键 / `_nextStructureId`(两处)/
    //   `Register` / `Remove` / `Update` / `TryGet` / `GetInstanceAt` / `Place` /
    //   `PeekNextId` —— 与契约 / codec / `entities.yaml` 的 i64 口径**全链一致**。
    //   本文件的两处 `(int)` 收窄转换**已移除**。
    // ══════════════════════════════════════════════════════════════


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
            _defs[id] = new ModuleDefinition(id, SlotType.Decor, local, new[] { 0, 90, 180, 270 }, cost: 1, buildItemKey: 0);

        /// <summary>登记一个**消耗性**模块(`refundable = 0`,EC-23-08 档)。</summary>
        /// <remarks>
        /// ⚠️ **此重载的存在理由是 2026-10-27 证伪轮实测出的覆盖缺口**:变异 K
        /// (停用 `if (def.Refundable == 0) return;`)在加入本重载前**全绿 96/96** ——
        /// 即「消耗性家具不返还」这条生产判据在整套夹具里**零判别力**。
        /// 根因:无参 `Add` 硬编码 `new ModuleDefinition(...)`,而 `refundable` 是**可选形参**
        /// 恒取默认 1 ⇒ 夹具**造不出**消耗性模块,判据再对也没人能测它。
        /// </remarks>
        public void AddConsumable(int id, WorldPos[] local) =>
            _defs[id] = new ModuleDefinition(id, SlotType.Decor, local, new[] { 0, 90, 180, 270 },
                                             cost: 1, buildItemKey: 0, refundable: 0);
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

    // Mock IEventSink
    internal class MockEventSink : IEventSink
    {
        private readonly Action<SimEvent> _append;

        public MockEventSink(Action<SimEvent> append)
        {
            _append = append;
        }

        public void Append(in SimEvent evt) => _append(evt);
        public long TickCount => 0;
    }
}
