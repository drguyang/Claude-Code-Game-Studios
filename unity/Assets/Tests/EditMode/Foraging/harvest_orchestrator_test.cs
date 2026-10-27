// S20-4 前置链 · foraging story-003 —— 一次成功采集的裁决编排
//
// 权威来源:
//   design/registry/entities.yaml(ADR-024 唯一真源)—— 三条 Kind 的 author:
//     ResourceHarvested → 17 · DropSpawned / DropClaimed → 20 · SkillGrown → 30
//   design/gdd/foraging.md 规则二 / 规则三(铸造点 = 出生当下)/ 规则五(对象 = 品种)
//   story production/epics/foraging/story-003-three-events-and-identity-mint.md
//     AC-17-01 · AC-17-05 · AC-17-05b · R11 邻接
//   ADR-005 主机唯一 Append · ADR-007 §四(None 哨兵)· ADR-016 §一(三源不变量)
//
// ⚠️ **AC 记分(本件有意分两档,禁合并成一条绿)**:
//     ✅ AC-17-05  · 17 侧零本地铸造(id 唯一来源 = IIdAuthority)
//     ✅ AC-17-05b · 恰一条 SkillGrown(不因三条产出事件多发)
//     ⚠️ AC-17-01  · **不勾** —— DropSpawned / DropClaimed 的**写入方是 20**
//                    (entities.yaml author 逐字),而 20 侧写者至今不存在。
//                    本件只能证「17 正确地**要求了**这三条」,不能证「三条真的进流」。
//     ⚠️ R11 邻接  · **半边** —— 铸造点当下性可验(四条载荷共用同一 id),
//                    但 `DropSpawned` 本身没有写者,半条腿悬空。
//
// ⚠️ **本文件的两个 Drop 相关测试用 SpyDropWriter**,它**不是**生产的 20 写者 ——
//    它只记录「17 有没有提出这两个要求」。把它读成「三连发已验证」就是借绿。
//
// ⚠️ 零随机 / 零时间依赖 / 零文件 I/O;全部 tick 与 id 写死。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Contracts.SkillSystem;
using DaYiJingCheng.Sim.Foraging;
using DaYiJingCheng.Sim.World;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;
using UnityEditor;

namespace DaYiJingCheng.Tests.EditMode.Foraging
{
    /// <summary>
    /// <see cref="HarvestOrchestrator"/> —— 一次成功采集的四步编排。
    /// </summary>
    [TestFixture]
    public sealed class HarvestOrchestratorTest
    {
        private CompositionRootServices _bag;
        private ResourceHarvestWriter _harvestWriter;
        private SpyDropWriter _dropWriter;
        private SpyGrowthEmitter _growth;
        private NoveltyTracker _novelty;
        private HarvestOrchestrator _orchestrator;

        [SetUp]
        public void Setup()
        {
            _bag = CompositionRoot.Assemble();
            _harvestWriter = new ResourceHarvestWriter(
                _bag.EventSink, _bag.Encoder, _bag.Decoder, _bag.Stream.Events);
            _dropWriter = new SpyDropWriter(() => _bag.Stream.Count);
            _growth = new SpyGrowthEmitter();
            // ⚠️ 每测一个**全新**追踪器 —— 它是有状态的(冷却字典)。复用会让上一测的
            //    「首次」把下一测的「首次」变成 Stale,那是跨测污染,不是被测行为。
            _novelty = new NoveltyTracker();
            _orchestrator = new HarvestOrchestrator(
                _bag.IdAuthority, _harvestWriter, _dropWriter, _growth, _novelty);
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-05b —— 恰一条 SkillGrown,对象 = 品种,不因三条产出事件多发
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1705b_oneSuccessfulHarvest_emitsExactlyOneSkillGrown()
        {
            _orchestrator.Resolve(Req(actorId: 1, nodeId: 5, objectId: 3));

            Assert.That(_growth.Calls.Count, Is.EqualTo(1),
                "一次成功采集须恰发一条 SkillGrown(规则五);实得 " + _growth.Calls.Count);

            var call = _growth.Calls[0];
            Assert.That(call.ObjectId, Is.EqualTo(3),
                "成长对象 = 品种(object_id),不是节点、不是物品 ordinal");
            Assert.That(call.ActorId, Is.EqualTo(1), "成长归属 = 采集者");
            Assert.That(call.SkillId, Is.EqualTo((int)SkillId.采集),
                "技能 = 采集(SkillId.采集 = 6)");
        }

        [Test]
        public void test_ac1705b_growthCountScalesWithHarvests_notWithEventCount()
        {
            // 连采 3 次 ⇒ 成长 3 条(= 采集次数),而**不是** 3 × 三条产出 = 9 条。
            // 这条是「不因三条产出事件多发」的直接判据。
            for (int i = 0; i < 3; i++)
                _orchestrator.Resolve(Req(actorId: 1, nodeId: i, objectId: 3));

            Assert.That(_growth.Calls.Count, Is.EqualTo(3),
                "成长条数须 = 采集次数(3),不得按产出事件数放大");

            // 反证:Drop 侧被要求了 6 次(3 次 × 2 条),成长只有 3 次 —— 两者不联动
            Assert.That(_dropWriter.SpawnCalls.Count, Is.EqualTo(3));
            Assert.That(_dropWriter.ClaimCalls.Count, Is.EqualTo(3));
        }

        [Test]
        public void test_ac1705b_growthCarriesHarvestTickAndNonePatient()
        {
            const long tick = 4242;
            _orchestrator.Resolve(Req(tick: tick, actorId: 7, objectId: 2));

            var call = _growth.Calls.Single();
            Assert.That(call.Tick, Is.EqualTo(tick), "成长 tick = 采集完成 tick(同 tick 连发)");
        }

        [Test]
        public void test_ac1705b_productionAdapter_actuallyAppendsSkillGrownToHistoryStream()
        {
            // ⚠️ **本测是本组唯一走生产适配器的一条。**
            // 其余 05b 夹具注入 SpyGrowthEmitter —— 它们只证「编排器**请求**了成长」,
            // **不证**「SkillGrown 真的进流」。两者之间隔着 SkillGrownEmitterAdapter
            // (生产类),若没人跑它,「成长已落流」就是一句**从未被执行过的自述**
            // (承 coding-standards「某件有门须给 file:line + 可跑命令」的同型纪律)。
            //
            // 断言面刻意选**载荷内容**而非「流多了一条」:
            //   · 只数条数 ⇒ 编错 Kind / 编错载荷仍绿
            //   · 载荷可解码 + 逐字段等值 ⇒ 把「编码 → 入池 → Append → 解码」整链钉住
            var adapter = new SkillGrownEmitterAdapter(_bag.EventSink, _bag.Encoder);
            var orch = new HarvestOrchestrator(
                _bag.IdAuthority, _harvestWriter, NullDropWriter.Instance, adapter, _novelty);

            const long tick = 777;
            const int actorId = 42;
            const int objectId = 9;
            orch.Resolve(Req(tick: tick, actorId: actorId, nodeId: 0, objectId: objectId));

            var grown = _bag.Stream.Events.Where(e => e.Kind == EventKind.SkillGrown).ToArray();
            Assert.That(grown.Length, Is.EqualTo(1),
                "生产适配器须真的把 SkillGrown 写进流(恰一条)");

            // SkillGrown 落**病史流**,不是世界流(StreamRouting:History)
            Assert.That(StreamRouting.Of(grown[0].Kind), Is.EqualTo(StreamId.History),
                "SkillGrown 归病史流");
            Assert.That(grown[0].Patient, Is.EqualTo(PatientId.None),
                "采集无病例语境 ⇒ Patient = None(ADR-007 §四,不污染 max(patient_id))");
            Assert.That(grown[0].Tick, Is.EqualTo(tick), "成长 tick = 采集完成 tick");

            var p = _bag.Decoder.Decode<SkillGrownPayload>(EventKind.SkillGrown, grown[0].Payload);
            Assert.That(p.ActorId, Is.EqualTo(actorId), "载荷 actor_id = 采集者");
            Assert.That(p.SkillId, Is.EqualTo((int)SkillId.采集), "载荷 skill_id = 采集");
            Assert.That(p.ObjectId, Is.EqualTo(objectId), "载荷 object_id = 品种(规则五)");
            Assert.That(p.Level, Is.EqualTo(SkillGrownPayload.LevelNotGrown),
                "采集不判升级 ⇒ Level = 未升级哨兵(-1),不得写成 0(0 是真实等级)");
            Assert.That(p.XpGain.Raw, Is.EqualTo(Fix.One.Raw),
                "xp_gain 原样透传(Fix.One 是夹具传入值)—— 适配器不得自行改写");
            Assert.That(p.PatientId, Is.EqualTo(PatientId.None.Value),
                "载荷 patient_id 亦为 None 哨兵");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-05 —— 17 侧零本地铸造(id 唯一来源 = IIdAuthority)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1705_instanceIdComesFromIdAuthority_notLocallyMinted()
        {
            // 从装配袋取发号器,先发一个号建立已知基线
            var before = _bag.IdAuthority.NextItemInstanceId();

            HarvestResult r = _orchestrator.Resolve(Req(actorId: 1, nodeId: 0, objectId: 0));

            Assert.That(r.InstanceId.Value, Is.GreaterThan(before.Value),
                "编排器铸的 id 须来自 IIdAuthority 的单调序列(严格大于上一次发号)");

            // 再发一个 ⇒ 证明编排消费的正是这个序列(不是另开计数器)
            var after = _bag.IdAuthority.NextItemInstanceId();
            Assert.That(after.Value, Is.EqualTo(r.InstanceId.Value + 1),
                "编排器取号后,下一次发号须紧接其后 —— 证明共用同一序列,非本地铸造");
        }

        [Test]
        public void test_ac1705_harvestOrchestratorType_hasNoLocalCounter()
        {
            // 反射扫类型面:编排器不得持有任何可变字段 —— 尤其不得有计数器。
            // 判据形态用反射而非 grep:改字段名逃不掉。
            var t = typeof(HarvestOrchestrator);
            var fields = t.GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);

            var mutable = fields
                .Where(f => f.DeclaringType == t)
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .Select(f => f.Name)
                .ToArray();

            Assert.That(mutable, Is.Empty,
                "HarvestOrchestrator 不得有可变字段(计数器会成第二个 id 真源):"
                + string.Join(", ", mutable));
        }

        [Test]
        public void test_ac1705_simAssembly_hasExactlyOneIdAuthorityImplementation()
        {
            // 判据:Sim 程序集内**实现 IIdAuthority 的类型**恰一个(IdAuthority),
            // 且没有第二个发号源 —— 多一个实现就多一个 id 真源,迁移后必重号。
            //
            // ⚠️ 这是**类型面能力断言**(反射),不是 grep:加包装类 / 改类型名都逃不掉。
            // ⚠️ 它证的是「只有一个发号权威」,**不证**「没有别处构造 ItemInstanceId」——
            //    后者在 C# 里无法用反射轻量判定(构造函数调用不留在类型面上)。
            //    「17 不本地铸造」由 test_ac1705_instanceIdComesFromIdAuthority_notLocallyMinted
            //    的行为断言 + HarvestOrchestrator 无可变字段的类型面断言共同守住。
            var asm = typeof(IdAuthority).Assembly;

            var authorityImpls = asm.GetTypes()
                .Where(t => typeof(IIdAuthority).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToArray();

            Assert.That(authorityImpls.Length, Is.EqualTo(1),
                "Sim 内 IIdAuthority 生产实现须恰一个;实得 "
                + authorityImpls.Length + ":" + string.Join(", ", authorityImpls.Select(t => t.Name)));
            Assert.That(authorityImpls[0], Is.EqualTo(typeof(IdAuthority)),
                "唯一生产实现须是 IdAuthority(D-21-27 唯一铸造来源)");
        }

        [Test]
        public void test_ac1705_simAssembly_hasExactlyOneItemInstanceIdCtorSite()
        {
            // ⚠️ **本条补的是 AC-17-05 的字面要求**:AC 原文 = 「反射扫描 sim 程序集的
            // `ItemInstanceId` **构造点**(type-surface 反射,非 grep),唯一来源 = IIdAuthority」。
            // 上面三条只扫了**类型面**(编排器的字段、IIdAuthority 实现数),**没扫构造点** ——
            // 一个新增的写者完全可以在另一个类型里 `new ItemInstanceId(0)` 而三条全绿。
            // 本条直接读 Sim.dll 的 IL(承 AC-11-22 / AC-3-B3 的 Cecil 先例)。
            //
            // ⚠️ `ItemInstanceId` 是 **struct** ⇒ C# 编译 `new ItemInstanceId(x)` 发
            // `call instance void .ctor(...)`,**不是** `newobj`(后者只用于引用类型)。
            // 只扫 `newobj` ⇒ 站点集恒空 ⇒ 「零违例」假绿(AC-11-22 已实测踩过同款)。
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "读 IL 的前提 = 编译成功(否则读到的是上一版 DLL = 假绿)");

            const string ItemInstanceIdType = "ItemInstanceId";
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                // 唯一合法构造点 = 发号权威自己(它才是 id 的生产者)
                "DaYiJingCheng.Sim.IdAuthority",
            };

            var dll = AssemblyGates.ScriptAssemblyPath("Sim");
            Assert.That(File.Exists(dll), Is.True,
                "Sim.dll 须已编译(路径 = AssemblyGates.ScriptAssemblyPath,单一出处)");

            var sites = new List<string>();
            using (var asm = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,   // Deferred:不触碰特性构造参数,免解析器异常
                InMemory = true,
            }))
            {
                foreach (var type in AssemblyGates.AllTypes(asm.MainModule))
                {
                    if (type.Name == "<Module>") continue;
                    foreach (var m in type.Methods)
                    {
                        MethodBody body;
                        try { body = m.Body; } catch { continue; }
                        if (body == null) continue;

                        foreach (var ins in body.Instructions)
                        {
                            bool isNewobj = ins.OpCode == OpCodes.Newobj;
                            bool isCtorCall = ins.OpCode == OpCodes.Call
                                              && ins.Operand is MethodReference mr0
                                              && mr0.Name == ".ctor";
                            if (!isNewobj && !isCtorCall) continue;

                            var ctor = ins.Operand as MethodReference;
                            if (ctor?.DeclaringType == null) continue;
                            if (ctor.DeclaringType.Name != ItemInstanceIdType) continue;

                            sites.Add(type.FullName + "::" + m.Name);
                        }
                    }
                }
            }

            // 下界:不得因扫描面失效而「零命中即绿」
            Assert.That(sites, Is.Not.Empty,
                "Sim.dll 内 `ItemInstanceId` 的构造点集为空 —— 扫描面已失效" +
                "(类型改名 / 只扫 newobj / 产物陈旧),拒以空集冒充绿");

            var illegal = sites.Where(s => !allowed.Contains(s.Split(new[] { "::" },
                                                    StringSplitOptions.None)[0]))
                               .ToArray();
            Assert.That(illegal, Is.Empty,
                "`ItemInstanceId` 的构造点须唯一落在 IIdAuthority 实现内(AC-17-05 / D-21-27);" +
                "越界构造点 = 第二 id 真源,实测违例:\n  " + string.Join("\n  ", illegal));

            // 上界:每条白名单须真命中(白名单变死 = 判据面丢失)
            foreach (var a in allowed)
                Assert.That(sites.Any(s => s.StartsWith(a + "::", StringComparison.Ordinal)), Is.True,
                    "白名单条目「" + a + "」零命中 —— 白名单是死的(类改名 / 未编译 / 陈旧)");
        }

        // ══════════════════════════════════════════════════════════════
        // AC-17-01 —— 三事件「被要求」的形状(⚠️ 不是「三条真的进流」)
        //
        // ⚠️ 本组**不让 AC-17-01 转绿**。它证的是:
        //    17 的编排正确地**要求**了 20 写 DropSpawned 与 DropClaimed,且参数正确。
        //    真实进流需要 20 的实现 —— 那不存在。
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_ac1701_orchestratorRequiresBothDropEvents_withCorrectPayload()
        {
            const long tick = 99;
            var cell = new WorldPos(10, 0, -4);

            HarvestResult r = _orchestrator.Resolve(
                Req(tick: tick, actorId: 7, nodeId: 0, cell: cell, itemKey: 12, qty: 2));

            Assert.That(_dropWriter.SpawnCalls.Count, Is.EqualTo(1),
                "须要求一次 DropSpawned(身份出生)");
            Assert.That(_dropWriter.ClaimCalls.Count, Is.EqualTo(1),
                "须要求一次 DropClaimed(立即归属)");

            var spawn = _dropWriter.SpawnCalls[0];
            Assert.That(spawn.Tick, Is.EqualTo(tick));
            Assert.That(spawn.InstanceId, Is.EqualTo(r.InstanceId.Value),
                "DropSpawned.instance_id 须与 ResourceHarvested 的是同一个(R11 铸造点当下性)");
            Assert.That(spawn.SpawnAnchor, Is.EqualTo(cell),
                "spawn_anchor = 节点整数格(ADR-015 §三,不是 float 世界坐标)");
            Assert.That(spawn.ItemKey, Is.EqualTo(12));
            Assert.That(spawn.Qty, Is.EqualTo(2));

            var claim = _dropWriter.ClaimCalls[0];
            Assert.That(claim.Tick, Is.EqualTo(tick));
            Assert.That(claim.InstanceId, Is.EqualTo(r.InstanceId.Value),
                "DropClaimed.instance_id 同上");
            Assert.That(claim.Claimer, Is.EqualTo(7),
                "claimer = 采集者 player_id");
        }

        [Test]
        public void test_ac1701_nullDropWriterAssembly_writesOnlyResourceHarvested()
        {
            // 🔁 **2026-10-27 S20-15 就地改判(原文不删,转绿机理记于此)**。
            //
            // 原稿 `test_ac1701_resourceHarvestedIsWritten_butDropEventsAreNot` 的断言是
            // 「NullDropWriter 装配下世界流只增 1 条」,并把它当成**反向夹具**:
            // 「20 落地后若有人换掉 NullDropWriter,本测会红 —— 那时是期待不是故障」。
            // **那一刻到了**:20 的两支写者已落(S20-14),S20-15 在生产装配点换上了
            // `DropEventWriterAdapter`。故本测**改名为它真正在测的东西** ——
            // 「`NullDropWriter` 这条**测试专用**空实现确实零写入」,不再冒充生产状态。
            //
            // ⚠️ 保留它的价值:它是**注入缝的阴性对照**。下面的
            // `test_ac1701_productionAdapter_writesAllThreeKinds` 是阳性面;若两条都绿,
            // 说明「三条落流」不是装配的副作用,而是**换掉写者才发生** —— 那是 AC-17-01
            // 「Drop* 由 20 写」这条归属判据的行为证据。
            var orch = new HarvestOrchestrator(
                _bag.IdAuthority, _harvestWriter, NullDropWriter.Instance, _growth, _novelty);

            int before = WorldStreamCount();
            orch.Resolve(Req(actorId: 1, nodeId: 0, objectId: 0));
            int delta = WorldStreamCount() - before;

            Assert.That(delta, Is.EqualTo(1),
                "NullDropWriter 是零写入空实现 ⇒ 世界流只增 ResourceHarvested 一条。实增 " + delta);

            var kinds = _bag.Stream.Events
                .Skip(_bag.Stream.Events.Count - delta)
                .Select(e => e.Kind)
                .ToArray();
            Assert.That(kinds, Is.EqualTo(new[] { EventKind.ResourceHarvested }),
                "装配空实现时唯一落流的是 ResourceHarvested(17 的 Kind)");
        }

        [Test]
        public void test_ac1701_productionAdapter_writesAllThreeKinds_inOrder()
        {
            // ⚠️ **本测是 AC-17-01 解除挂起的判据本体**(S20-15)。
            //    它走**生产适配器** `DropEventWriterAdapter` → 20 的 `DropEventWriter`,
            //    即 `CompositionRoot` 里真实装配的那一条链(不是 spy)。
            // ⚠️ 成长侧也必须走**生产适配器** `SkillGrownEmitterAdapter` —— 下面 ⑤ 要断言
            //    病史流里真的多了一条 `SkillGrown`;若用 SpyGrowthEmitter,那条断言必然红
            //    (spy 不写流)。首跑实测正是这么红的:夹具自己混了 spy 与生产链。
            var writer = new DropEventWriter(_bag.EventSink, _bag.Encoder, _bag.IdAuthority);
            var orch = new HarvestOrchestrator(
                _bag.IdAuthority, _harvestWriter,
                new DropEventWriterAdapter(writer),
                new SkillGrownEmitterAdapter(_bag.EventSink, _bag.Encoder), _novelty);

            int before = _bag.Stream.Events.Count;
            HarvestResult r = orch.Resolve(Req(actorId: 5, nodeId: 2, objectId: 3));

            // ⚠️ **按流别切面**,不要拿「全流增量」当世界流增量 —— 一次采集同时写
            //    世界流三条 + 病史流一条(SkillGrown),混在一起数会得到四条。
            var written = _bag.Stream.Events.Skip(before)
                .Where(e => StreamRouting.Of(e.Kind) == StreamId.World)
                .ToArray();

            // ① 世界流恰三条 —— 不多不少
            Assert.That(written.Select(e => e.Kind).ToArray(),
                Is.EqualTo(new[]
                {
                    EventKind.DropSpawned,
                    EventKind.ResourceHarvested,
                    EventKind.DropClaimed,
                }),
                "一次成功采集须在世界流恰发三条,且**序**为 " +
                "DropSpawned → ResourceHarvested → DropClaimed " +
                "(出生须先于引用 —— 20 侧 InventoryOf fold 的读序前提,story-003 Notes 1)。" +
                "实得:" + string.Join(" → ", written.Select(e => e.Kind)));

            // ③ 三条共用同一个 instance_id(R11 铸造点当下性 —— 这次在**流里**核,不再靠 spy)
            var spawned = _bag.Decoder.Decode<DropSpawnedPayload>(
                EventKind.DropSpawned, written[0].Payload);
            var harvested = _bag.Decoder.Decode<ResourceHarvestedPayload>(
                EventKind.ResourceHarvested, written[1].Payload);
            var claimed = _bag.Decoder.Decode<DropClaimedPayload>(
                EventKind.DropClaimed, written[2].Payload);

            Assert.That(spawned.InstanceId, Is.EqualTo(r.InstanceId.Value));
            Assert.That(harvested.InstanceId, Is.EqualTo(r.InstanceId.Value));
            Assert.That(claimed.InstanceId, Is.EqualTo(r.InstanceId.Value),
                "三条载荷须共用同一 id —— 若 DropSpawned 是另一个号,说明适配器错调了自铸入口" +
                "(RecordSpawned),一次采集烧了两个号");

            // ④ 载荷内容对得上请求(防「三条都在但内容错」)
            Assert.That(spawned.ItemKey, Is.EqualTo(0));
            Assert.That(spawned.Qty, Is.EqualTo(1));
            Assert.That(spawned.SpawnAnchor, Is.EqualTo(new WorldPos(3, 0, 7)),
                "spawn_anchor = 节点整数格(不是 float 世界坐标)");
            Assert.That(claimed.Claimer, Is.EqualTo(5), "claimer = 采集者");

            // ⑤ 零新增 Kind(AC-20-17 同口径):除三条世界流 + SkillGrown 外,别无他物
            var historyKinds = _bag.Stream.Events.Skip(before)
                .Where(e => StreamRouting.Of(e.Kind) == StreamId.History)
                .Select(e => e.Kind).ToArray();
            Assert.That(historyKinds, Is.EqualTo(new[] { EventKind.SkillGrown }),
                "病史流侧只增 SkillGrown 一条(采集的成长)");

            // ⑥ 全流增量**总和**恰 4 —— 兜住「三条世界 + 一条病史,没有第五个别的东西」
            //    （上两条各自按流别切面,合起来才是完备的「零新增」）
            Assert.That(_bag.Stream.Events.Count - before, Is.EqualTo(4),
                "一次采集的全流增量须恰 4 条(世界 3 + 病史 1)");
        }

        // ══════════════════════════════════════════════════════════════
        // R11 邻接 —— 铸造点当下性(四条载荷共用同一 id)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_r11_threeIdCarryingPayloadsShareTheSameInstanceId()
        {
            // ⚠️ **只三条,不是四条**:`SkillGrown` 的载荷**不含 instance_id**
            // (它的载荷 = actor_id / patient_id / skill_id / object_id / novelty_class /
            //  level / xp_gain —— 见 entities.yaml)。成长按「对象 = 品种」归口,
            // 不按实例归口。把成长算进「同源」是概念错误,本测因此只断三条。
            HarvestResult r = _orchestrator.Resolve(Req(actorId: 3, nodeId: 1, objectId: 4));

            // ① ResourceHarvested 载荷里的 id(17 真写的那条)
            var harvested = _bag.Stream.Events.Last(e => e.Kind == EventKind.ResourceHarvested);
            Assert.That(
                PayloadCodec.TryGetPayload<ResourceHarvestedPayload>(
                    harvested, _bag.BlobPool, out var hp),
                Is.True, "ResourceHarvested 载荷须可解码");
            Assert.That(hp.InstanceId, Is.EqualTo(r.InstanceId.Value),
                "ResourceHarvested.instance_id");

            // ② 两个 Drop 要求的 id(17 要求、20 落流)
            Assert.That(_dropWriter.SpawnCalls.Single().InstanceId, Is.EqualTo(r.InstanceId.Value),
                "DropSpawned.instance_id");
            Assert.That(_dropWriter.ClaimCalls.Single().InstanceId, Is.EqualTo(r.InstanceId.Value),
                "DropClaimed.instance_id");

            // ③ 三条须同源 —— id 在写任何事件**之前**就已铸造(R11 当下性)
            Assert.That(hp.InstanceId, Is.EqualTo(_dropWriter.SpawnCalls.Single().InstanceId));
        }

        [Test]
        public void test_r11_instanceIdIsMintedBeforeAnyWrite()
        {
            // 「铸造点 = 出生事件当下」的反面判据:若实现是「先写 ResourceHarvested,
            // 再去申请 id」,那么写第二条时拿到的号会比第一条大 —— 本测断二者必须一致。
            // 这里用一个**会记录发号次数**的 spy 包住真发号器来观察时序。
            var spyIds = new CountingIdAuthority(_bag.IdAuthority);
            var orch = new HarvestOrchestrator(
                spyIds, _harvestWriter, _dropWriter, _growth, _novelty);

            orch.Resolve(Req(actorId: 1, nodeId: 0, objectId: 0));

            Assert.That(spyIds.CallCount, Is.EqualTo(1),
                "一次采集须恰申请 1 个 id(不是每条事件各申请一个 —— 那会产生三个不同 id)");
        }

        // ══════════════════════════════════════════════════════════════
        // 原子性 —— 前置失败时零写入
        // ══════════════════════════════════════════════════════════════

        [TestCase(-1L, 0, 0, 1, 1, "tick 为负")]
        [TestCase(0L, -1, 0, 1, 1, "actorId 为负")]
        [TestCase(0L, 0, -1, 1, 1, "nodeId 为负")]
        [TestCase(0L, 0, 0, 0, 1, "qty = 0")]
        [TestCase(0L, 0, 0, 1, 0, "outQuality = 0")]
        public void test_invalidRequest_throwsBeforeAnyWrite(
            long tick, int actorId, int nodeId, int qty, int quality, string why)
        {
            int before = _bag.Stream.Count;

            // ⚠️ **本测盯的是「抛在哪一层」,不只是「会不会抛」** —— 2026-10-26 证伪轮 M5
            // 实测:`qty < 1` 有**两层**守卫(编排器 `Validate` 与写者 `Record`),把
            // `Validate` 的那条放宽成 `< 0` 后,下游 `Record` 接住并抛出**同一个异常类型**
            // ⇒ 只断「抛 ArgumentOutOfRangeException + 零事件」的旧夹具**仍然全绿**(red=0)。
            // 那是借绿:异常不再来自前置校验,而来自**已经铸了 id 之后**的写者。
            //
            // 故补两条把抛出点钉死:
            //   ① `ParamName == "r"` —— 编排器的 `Validate` 以 `nameof(r)` 抛;写者以
            //      `nameof(qty)` 抛,两者可分辨;
            //   ② **零消耗 instance_id** —— id 铸造本身是副作用(它推进机制 A 计数器)。
            //      非法请求若消耗了号,下一次合法采集的 id 就会跳号,这是可观测的语义破坏。
            //      这一条比 ① 更强:它不依赖参数名(实现细节),只依赖「前置校验在发号之前」。
            var spyIds = new CountingIdAuthority(_bag.IdAuthority);
            var orch = new HarvestOrchestrator(
                spyIds, _harvestWriter, _dropWriter, _growth, _novelty);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orch.Resolve(
                new HarvestRequest(tick, actorId, nodeId, new WorldPos(0, 0, 0),
                                   itemKeyOrdinal: 0, qty, quality,
                                   skillId: 0, objectId: 0, Fix.One)),
                why);

            Assert.That(ex.ParamName, Is.EqualTo("r"),
                "抛出点须在编排器前置校验层(ParamName = nameof(r))。若为 'qty' 等," +
                "说明编排器的守卫已失效、异常来自下游写者 —— 那时 id 已被铸造:" + why);

            Assert.That(spyIds.CallCount, Is.EqualTo(0),
                "非法请求须**零消耗 instance_id** —— 发号本身是副作用(推进机制 A 计数器)," +
                "在发号之后才拒 = 空耗一个号、合法采集跳号:" + why);

            Assert.That(_bag.Stream.Count, Is.EqualTo(before),
                "前置校验失败须在**任何写入之前**抛出(原子性):" + why);
            Assert.That(_dropWriter.SpawnCalls.Count, Is.EqualTo(0), "Drop 侧零调用");
            Assert.That(_dropWriter.ClaimCalls.Count, Is.EqualTo(0), "Drop 侧零调用");
            Assert.That(_growth.Calls.Count, Is.EqualTo(0), "成长零调用");
        }

        // 🔁 **2026-10-27 S20-15:novelty 的两条用例已删除**(原文不删,机理记于此)。
        //    原稿是 `[TestCase(-1,…)]` / `[TestCase(7,…)]` —— 它们构造 `(NoveltyClass)(-1)` /
        //    `(NoveltyClass)7` 作为**入参**喂给 `HarvestRequest`,验编排器拒越界。
        //    但 S20-15 把 `Novelty` 从入参**删除**(AC-17-14f:求值只在编排器内部发生)⇒
        //    那个形状**在类型层面已不可表达** —— 用例会编译不过,而不是变红。
        //    ⇒ 守卫随字段一起删除(`HarvestOrchestrator.Validate` 内留有就地注记)。
        //    域界判据由 `NoveltyClass` 枚举本身 + `NoveltyTracker.Query` 的三值返回保证,
        //    比运行期判一个 int 型枚举更硬。`xpGain` 那条**保留**(它仍是入参)。
        [TestCase(0, -1L, "xp_gain 为负 (Fix raw = -1)")]
        public void test_b1_growthFieldsValidatedBeforeAnyWrite(int unusedNoveltyOrdinal, long xpGainRaw, string why)
        {
            // ⚠️ **本测补的是 2026-10-26 修 B-1 前的真缺陷**:原 `Validate` 只核 8 项,
            //    **不核 novelty / xpGain** —— 而这两项由 30 的 `SkillGrownEmitter.EmitGrowth`
            //    在**第 ⑤ 步**(最后一次写入之后)才核。彼时 id 已铸、`ResourceHarvested` 已落
            //    世界流、Drop 两条已请求 ⇒「写了一半」是**可观测**的。
            //    2026-10-27 后 novelty 已不是入参,本测只剩 xpGain 一面;判据三件不变。
            //
            // 判据三件(与既有 `test_invalidRequest_throwsBeforeAnyWrite` 同款判别力):
            //   ① `ParamName == "r"` —— 编排器以前置校验层的名字抛;若为 "xpGain"
            //      说明抛出点已退到下游 `EmitGrowth`,前置校验失效;
            //   ② 零消耗 instance_id —— 发号是副作用;
            //   ③ 流长不变 —— 半写的最直接判据。
            int before = _bag.Stream.Count;

            var spyIds = new CountingIdAuthority(_bag.IdAuthority);
            var orch = new HarvestOrchestrator(
                spyIds, _harvestWriter, _dropWriter, _growth, _novelty);

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => orch.Resolve(
                Req(xpGain: new Fix(xpGainRaw))),
                why);

            Assert.That(ex.ParamName, Is.EqualTo("r"),
                "抛出点须在编排器前置校验层(ParamName = nameof(r))。若为 'xpGain'," +
                "说明异常来自第 ⑤ 步 EmitGrowth —— 那时 id 已铸、ResourceHarvested 已落流:" + why);

            Assert.That(spyIds.CallCount, Is.EqualTo(0),
                "非法请求须**零消耗 instance_id**(发号推进会推进机制 A 计数器):" + why);

            Assert.That(_bag.Stream.Count, Is.EqualTo(before),
                "须在**任何写入之前**抛出(原子性)—— 流长不得变化:" + why);
            Assert.That(_dropWriter.SpawnCalls.Count, Is.EqualTo(0), "Drop 侧零调用:" + why);
            Assert.That(_dropWriter.ClaimCalls.Count, Is.EqualTo(0), "Drop 侧零调用:" + why);
            Assert.That(_growth.Calls.Count, Is.EqualTo(0), "成长零调用:" + why);
        }

        // ══════════════════════════════════════════════════════════════
        // 事件序 —— DropSpawned → ResourceHarvested → DropClaimed(story-003 Notes 1)
        // ══════════════════════════════════════════════════════════════

        [Test]
        public void test_eventOrder_dropSpawnedPrecedesResourceHarvested_precedesDropClaimed()
        {
            // ⚠️ **本测补的是 2026-10-26 修 B-2 前的真缺陷**:原实现是
            //    `ResourceHarvested → DropSpawned → DropClaimed`,与 story-003 Notes 1 明定的
            //    `DropSpawned → ResourceHarvested → DropClaimed` **相反**。世界流 `Seq` 按
            //    append 序发 ⇒ `ResourceHarvested.instance_id` 会先于它的出生事件出现,
            //    违 20 侧 `InventoryOf` fold 的读序前提。
            //
            // 判据 = **行为可观测**:Spy 记录「被调用当下的世界流事件数」。首采时流为空,
            // 故 Spawn 时该值须为 0(它先写 ⇒ 尚无人写),Claim 时须为 1(此时
            // ResourceHarvested 已落流 1 条)。对调 ② ③ 的实现会给出 (1, 1) 或 (1, 2)。
            int before = _bag.Stream.Count;

            _orchestrator.Resolve(Req(actorId: 1, nodeId: 0, objectId: 0));

            Assert.That(_dropWriter.SpawnStreamCounts.Count, Is.EqualTo(1));
            Assert.That(_dropWriter.ClaimStreamCounts.Count, Is.EqualTo(1));

            Assert.That(_dropWriter.SpawnStreamCounts[0], Is.EqualTo(before),
                "`DropSpawned` 须先于 `ResourceHarvested` 写入(story-003 Notes 1):" +
                "Spawn 被调用时世界流应仍为空,实测 " + _dropWriter.SpawnStreamCounts[0] +
                "(> " + before + " ⇒ ResourceHarvested 抢在前面 = 出生晚于引用)");

            Assert.That(_dropWriter.ClaimStreamCounts[0], Is.EqualTo(before + 1),
                "`DropClaimed` 须在 `ResourceHarvested` 之后:Claim 被调用时流中应恰有 1 条" +
                "(ResourceHarvested),实测 " + _dropWriter.ClaimStreamCounts[0]);

            // 生产写者的真实落流次序复核(17 那一半):ResourceHarvested 确实写进了世界流
            var kinds = _bag.Stream.Events
                .Skip(before).Select(e => e.Kind).ToArray();
            Assert.That(kinds, Is.EqualTo(new[] { EventKind.ResourceHarvested }),
                "SpyDropWriter 装配下 17 只写真一条;`Seq` 与流内序就此与 Spawn 的先行性一致");
        }

        // ══════════════════════════════════════════════════════════════
        // 夹具
        // ══════════════════════════════════════════════════════════════

        private int WorldStreamCount()
            => _bag.Stream.Events.Count(e => StreamRouting.Of(e.Kind) == StreamId.World);

        private static HarvestRequest Req(
            long tick = 10, int actorId = 1, int nodeId = 0,
            WorldPos? cell = null, int itemKey = 0, int qty = 1,
            int objectId = 0,
            Fix? xpGain = null)
            => new HarvestRequest(
                tick, actorId, nodeId, cell ?? new WorldPos(3, 0, 7),
                itemKey, qty, outQuality: 2,
                skillId: (int)SkillId.采集, objectId, xpGain ?? Fix.One);

        /// <summary>记录 17 提出的 Drop 要求的 spy。**不是** 20 的生产写者。</summary>
        /// <remarks>
        /// <para>额外记录**调用当下的世界流事件数**(<c>StreamCountAtCall</c>)—— 它是
        /// 「append 序」的行为可观测判据:世界流的 <c>Seq</c> 按 append 序发,故
        /// <c>Spawn</c> 被调用时的流长直接反映「`DropSpawned` 是否早于 `ResourceHarvested`」。</para>
        /// <para>⚠️ 只断「两个 spy 各被调用一次」**不构成**钉住次序 —— 那对调序的实现同样全绿。</para>
        /// </remarks>
        private sealed class SpyDropWriter : IDropWriter
        {
            private readonly Func<int> _streamCount;

            public SpyDropWriter(Func<int> streamCount)
                => _streamCount = streamCount ?? throw new ArgumentNullException(nameof(streamCount));

            public readonly List<(long Tick, long InstanceId, WorldPos SpawnAnchor, int ItemKey, int Qty)> SpawnCalls
                = new List<(long, long, WorldPos, int, int)>();
            public readonly List<(long Tick, long InstanceId, int Claimer)> ClaimCalls
                = new List<(long, long, int)>();

            /// <summary>每次 <see cref="Spawn"/> 调用当下的世界流事件数(与 <see cref="SpawnCalls"/> 同序)。</summary>
            public readonly List<int> SpawnStreamCounts = new List<int>();

            /// <summary>每次 <see cref="Claim"/> 调用当下的世界流事件数(与 <see cref="ClaimCalls"/> 同序)。</summary>
            public readonly List<int> ClaimStreamCounts = new List<int>();

            public void Spawn(long tick, ItemInstanceId instanceId, WorldPos spawnAnchor, int itemKey, int qty)
            {
                SpawnStreamCounts.Add(_streamCount());
                SpawnCalls.Add((tick, instanceId.Value, spawnAnchor, itemKey, qty));
            }

            public void Claim(long tick, ItemInstanceId instanceId, int claimer)
            {
                ClaimStreamCounts.Add(_streamCount());
                ClaimCalls.Add((tick, instanceId.Value, claimer));
            }
        }

        /// <summary>记录成长调用的 spy。</summary>
        /// <remarks>
        /// ⚠️ **刻意不记录 <c>InstanceId</c> / <c>PatientId</c>** —— <see cref="IGrowthEmitter"/>
        /// 的签名里没有 instance_id(成长按「对象 = 品种」归口,不按实例),patient_id 由
        /// 生产适配器固定填 <see cref="PatientId.None"/>。在 spy 里补这两个字段再断言,
        /// 等于**断言寄存器里的常量**——恒真且不测任何东西(自指判据的变体)。
        /// </remarks>
        private sealed class SpyGrowthEmitter : IGrowthEmitter
        {
            public readonly List<(long Tick, int ActorId, int SkillId, int ObjectId,
                                 NoveltyClass Novelty, int? Level, Fix XpGain)>
                Calls = new List<(long, int, int, int, NoveltyClass, int?, Fix)>();

            public void Emit(long tick, int actorId, int skillId, int objectId,
                             NoveltyClass novelty, int? level, Fix xpGain)
                => Calls.Add((tick, actorId, skillId, objectId, novelty, level, xpGain));
        }

        /// <summary>包住真发号器,记录被调用次数(验「一次采集恰申请 1 个 id」)。</summary>
        private sealed class CountingIdAuthority : IIdAuthority
        {
            private readonly IIdAuthority _inner;
            public int CallCount { get; private set; }

            public CountingIdAuthority(IIdAuthority inner)
                => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

            public PatientId NextPatientId() => _inner.NextPatientId();

            public ItemInstanceId NextItemInstanceId()
            {
                CallCount++;
                return _inner.NextItemInstanceId();
            }
        }
    }
}
