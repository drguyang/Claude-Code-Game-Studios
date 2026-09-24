// 权威来源:
//   Story 009(production/epics/item-database/story-009-craft-event-payload.md)
//     · AC-21a-52 —— ActualConsumed 只出现在 Craft 事件载荷;五子条件 ①–⑤(QA spec 细化)
//   GDD:design/gdd/item-database.md(D-21-15 实耗 = Ceil(Base/EFF) 运行期派生量 ·
//        D-21-28 全序键裁定 [甲](Tick, StreamPriority, Patient, Seq) · AC-21a-52)
//   ADR-009 Amendment J(Craft 载荷十位定稿;ActualConsumed 进流不回写静态数据)
//   ADR-024 §①(payload_schema 真源 = design/registry/entities.yaml,禁从散文解析)
//   ADR-008(跨流全序键)· ADR-007 §四(PatientId.None 哨兵)· ADR-005(主机唯一 Append)
//
// ⚠️ 落点:故事头登记的账本路径 = tests/integration/item_database/craft_event_payload_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001–008 同一先例;
//    账本 tests/integration/item_database/README.md 已记落点表)。
//
// ⚠️ 「经 7a 持久化往返」子条件的承位口径(禁借绿):7a 持久化服务(文件 + 校验和 +
//    checkpoint)尚未实现 ⇒ 本文件以 **SimEventCodec(header)+ PayloadCodec(blob)字节往返**
//    承位 —— 这正是 ADR-010 全二进制 codec 的同一条字节路径(存档体 = 头 + 三流,流内即
//    header + payload 字节);真·文件级存档往返归 7a 落地后补跑,**本文件不宣称该子条件已绿**。
//
// ⚠️ 范围(Out of Scope 硬边界):F1/F2 求解本体归 Story 003;ItemInstance 编码本体归
//    Story 010(本文件只做「快照类型无 ActualConsumed 字段」的负向断言);跨平台黄金
//    夹具归 Story 011;output_instance_ids 的 IIdAuthority 铸造本体归 Story 010 ——
//    本文件以字面量模拟「主机于点火 tick 铸造」的结果。
//
// ⚠️ 数值纪律:以下常量全部是测试夹具值,不是游戏平衡值(GDD §Tuning Knobs 数值待用户)。
// 测试纪律:test_[scenario]_[expected];无随机种子、无时间依赖、无 external I/O。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Codec;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class CraftEventPayloadTest
    {
        // ══════════════ 常量表夹具(与 recipe_settlement_solver_test 同款自洽小值)══════════════
        private const int FixtureMaxQuality = 5;
        private const int FixtureSkillCap = 60;

        private static RecipeSettlementConstants fixture() => new RecipeSettlementConstants(
            qtyMultMin: FixParse.Parse("1/4"),
            qtyMultMax: FixParse.Parse("2"),
            skillModCap: FixParse.Parse("1/10"),
            qualModCap: FixParse.Parse("1/10"),
            equipModCap: FixParse.Parse("1/10"),
            envModMin: FixParse.Parse("-1"),
            envModMax: FixParse.Parse("1/2"),
            retainMin: FixParse.Parse("1/2"),
            retainMax: FixParse.Parse("1"),
            effMin: FixParse.Parse("1/2"),
            effMax: FixParse.Parse("1"),
            maxQuality: FixtureMaxQuality,
            skillCap: FixtureSkillCap);

        private static RecipeEntry[] entries(string prefix, params int[] qtys)
        {
            var array = new RecipeEntry[qtys.Length];
            for (int i = 0; i < qtys.Length; i++)
                array[i] = new RecipeEntry(new ItemKey(prefix + "_" + i, ProcessingState.Raw), qtys[i]);
            return array;
        }

        private static RecipeSettlementResult solve(
            RecipeEntry[] outputs, RecipeEntry[] inputs, int craftSkill, RecipeSettlementConstants constants)
        {
            var request = new RecipeSettlementRequest(
                outputs, inputs, craftSkill, inputQuality: 3,
                equipMod: default(Fix), envModClimate: default(Fix), envModClinic: default(Fix));
            return RecipeSettlementSolver.Solve(request, constants);
        }

        /// <summary>独立重算 <c>Ceil(base / EFF)</c>(D-21-15;正域整数上取整除法,
        /// 与求解器实现零共享代码路径 —— 本式直接落 GDD 公式)。</summary>
        private static int CeilBaseOverEff(int baseQty, Fix eff)
        {
            long numerator = (long)baseQty * Fix.OneRaw;
            return (int)((numerator + eff.Raw - 1L) / eff.Raw);
        }

        // ══════════════ ④ 载荷字段集 = entities.yaml Craft payload_schema 真源 ══════════════

        [Test]
        public void test_ac21a52_craftPayloadFieldSet_matchesRegistrySchema()
        {
            // 转录源 = design/registry/entities.yaml SimEvent.Kind.Craft.payload_schema(ADR-024 §①):
            // actor_id / recipe_id / start_tick / duration_ticks / input_instance_ids[] /
            // ActualConsumed[] / OutputQty[] / OutputQuality[] / output_instance_ids[] / tool_cell
            string[] expected =
            {
                "ActorId", "RecipeId", "StartTick", "DurationTicks", "InputInstanceIds",
                "ActualConsumed", "OutputQty", "OutputQuality", "OutputInstanceIds", "ToolCell",
            };

            FieldInfo[] fields = typeof(CraftPayload)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            CollectionAssert.AreEqual(
                expected.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                fields.Select(f => f.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray(),
                "CraftPayload 公共字段集须与 registry payload_schema 十位逐一对应" +
                "(多一位 = 载荷越权,少一位 = 事实丢失;ADR-024 禁从散文解析 —— 真源改了这里必须跟着改)");

            foreach (FieldInfo f in fields)
            {
                Type t = f.FieldType;
                Type element = t.IsArray ? t.GetElementType() : t;
                Assert.That(
                    element == typeof(int) || element == typeof(long) || element == typeof(WorldPos),
                    Is.True,
                    $"CraftPayload.{f.Name} 类型 {t.Name} 越出整数域" +
                    "(ADR-024 §①:载荷字段类型只允许整数域;ADR-006 边界禁 float)");
            }
        }

        [Test]
        public void test_ac21a52_orderKeyFieldSet_isHeaderOnlyNoActor()
        {
            // ⑤ 的一半:D-21-28 [甲] —— actor_id 是载荷字段不进键,(Tick, ActorId, Seq) 建议被否。
            string[] expected = { "Tick", "StreamPriority", "Patient", "Seq" };

            FieldInfo[] fields = typeof(EventOrderKey)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

            CollectionAssert.AreEqual(
                expected,
                fields.Select(f => f.Name).OrderBy(
                    n => Array.IndexOf(expected, n)).ToArray(),
                "全序键恰四分量 Tick/StreamPriority/Patient/Seq —— " +
                "出现 ActorId 即违背 D-21-28 裁定 [甲](actor_id 由 Craft 载荷吸收)");
        }

        // ══════════════ ① ② ActualConsumed 不在配方静态数据与实例快照 ══════════════

        [Test]
        public void test_ac21a52_actualConsumed_absentFromStaticAndSnapshotTypes()
        {
            // ① 静态侧:配方 / 条目 / 物品定义 / 数据集
            // ② 快照侧:ItemInstance(编码本体归 Story 010,负向断言先行)
            Type[] staticAndSnapshot =
            {
                typeof(Recipe), typeof(RecipeEntry), typeof(ItemDef), typeof(ItemInstance),
                typeof(ItemDataSet), typeof(RecipeDataSet),
            };

            foreach (Type type in staticAndSnapshot)
            {
                bool leaked = type
                    .GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                BindingFlags.Instance | BindingFlags.Static)
                    .Any(m => string.Equals(m.Name, "ActualConsumed",
                        StringComparison.OrdinalIgnoreCase));

                Assert.That(leaked, Is.False,
                    $"{type.Name} 出现 ActualConsumed 成员 —— 违 ADR-009 Amendment J:" +
                    "实耗只在 Craft 事件载荷,配方静态数据与实例快照均不得含该字段" +
                    "(D-21-15:实耗是运行期派生量,回写即第二真源)");
            }

            // 正向对照(防空断言:若 CraftPayload 也没了 ActualConsumed,上面的循环恒绿毫无意义)
            Assert.That(
                typeof(CraftPayload).GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Any(f => f.Name == "ActualConsumed"),
                Is.True, "正向对照:ActualConsumed 必须在 CraftPayload 上(否则本组断言空转)");
            Assert.That(
                typeof(RecipeSettlementResult).GetField("ActualConsumed") is not null,
                Is.True, "正向对照:结算出参必须携带 ActualConsumed(它是流载荷的来源)");
        }

        // ══════════════ ③ 实耗 = Ceil(基数 / 当次 EFF),EFF < EFF_MAX 逐项对位 ══════════════

        [Test]
        public void test_ac21a52_actualConsumed_recomputeEquals_whenEffBelowMax()
        {
            RecipeSettlementConstants constants = fixture();
            RecipeEntry[] outputs = entries("out", 2);
            RecipeEntry[] inputs = entries("in", 3, 7, 1);      // 多输入逐项对位(Amendment J)

            // skill = 0 ⇒ EFF = EFF_MIN = 1/2 < EFF_MAX(实耗 > 基数的 Given)
            RecipeSettlementResult result = solve(outputs, inputs, craftSkill: 0, constants);

            Assert.That(result.Efficiency.Raw, Is.LessThan(Fix.OneRaw),
                "前置:EFF < EFF_MAX(D-21-15 派生量非平凡;EFF 曲线本体归 Story 003)");
            Assert.That(result.ActualConsumed, Has.Length.EqualTo(inputs.Length),
                "实耗逐投入对位(输入组等长)");

            // 装载 payload(实例 id 模拟主机点火 tick 铸造 —— IIdAuthority 本体归 Story 010)
            var payload = new CraftPayload(
                actorId: 1, recipeId: 7, startTick: 320L, durationTicks: 40L,
                inputInstanceIds: new long[] { 9001L, 9002L, 9003L },
                actualConsumed: result.ActualConsumed,
                outputQty: result.OutputQty,
                outputQuality: Enumerable.Repeat(result.OutputQuality, result.OutputQty.Length).ToArray(),
                outputInstanceIds: Enumerable.Range(12001, result.OutputQty.Length).Select(i => (long)i).ToArray(),
                toolCell: new WorldPos(2, 0, 5));

            var decoded = PayloadCodec.Decode<CraftPayload>(EventKind.Craft, PayloadCodec.Encode(payload));

            bool anyStrictlyAboveBase = false;
            for (int j = 0; j < inputs.Length; j++)
            {
                int baseQty = inputs[j].Qty;
                int expected = CeilBaseOverEff(baseQty, result.Efficiency);
                Assert.That(decoded.ActualConsumed[j], Is.EqualTo(expected),
                    $"实耗[{j}] 与 Ceil(基数 {baseQty} / 当次 EFF) 重算不一致" +
                    "(回放由「基数 + 当时 EFF」重演,不回读冻结数 —— D-21-15)");
                Assert.That(decoded.ActualConsumed[j], Is.GreaterThanOrEqualTo(baseQty),
                    $"EFF ≤ 1 ⇒ 实耗[{j}] ≥ 基数(恒不省料)");
                if (decoded.ActualConsumed[j] > baseQty) anyStrictlyAboveBase = true;
            }

            Assert.That(anyStrictlyAboveBase, Is.True,
                "EFF < EFF_MAX ⇒ 至少一项实耗严格大于基数(否则「损耗落在投入侧」未发生)");

            // 两组等长对位(20 的 fold 逐实例扣的承载前提)
            Assert.That(decoded.InputInstanceIds, Has.Length.EqualTo(decoded.ActualConsumed.Length),
                "输入组:输入实例 ⟷ 实耗 等长对位");
            Assert.That(decoded.OutputInstanceIds, Has.Length.EqualTo(decoded.OutputQty.Length),
                "产出组:产量 ⟷ 品级 ⟷ 输出实例 等长对位");
            CollectionAssert.AreEqual(inputs.Select(i => i.Qty).ToArray(),
                new[] { 3, 7, 1 }, "夹具基数自证(防夹具被静默改掉后断言漂移)");
        }

        [Test]
        public void test_ac21a52_actualConsumed_equalsBase_whenEffEqualsMax()
        {
            RecipeSettlementConstants constants = fixture();
            RecipeEntry[] outputs = entries("out", 2);
            RecipeEntry[] inputs = entries("in", 3, 7);

            // skill = SKILL_CAP ⇒ EFF = EFF_MAX = 1(边例:取等仍只在事件载荷)
            RecipeSettlementResult result = solve(outputs, inputs, craftSkill: FixtureSkillCap, constants);

            Assert.That(result.Efficiency.Raw, Is.EqualTo(Fix.OneRaw), "满技能 ⇒ EFF = EFF_MAX");
            CollectionAssert.AreEqual(
                new[] { 3, 7 }, result.ActualConsumed,
                "EFF = EFF_MAX ⇒ 实耗 = 基数取等(零损耗)");

            var payload = new CraftPayload(
                1, 7, 320L, 40L,
                new long[] { 9001L, 9002L },
                result.ActualConsumed,
                result.OutputQty,
                Enumerable.Repeat(result.OutputQuality, result.OutputQty.Length).ToArray(),
                new long[] { 12001L },
                new WorldPos(2, 0, 5));
            var decoded = PayloadCodec.Decode<CraftPayload>(EventKind.Craft, PayloadCodec.Encode(payload));

            CollectionAssert.AreEqual(new[] { 3, 7 }, decoded.ActualConsumed,
                "取等值同样只存于事件载荷(静态侧仍无此字段 —— 见 absentFromStaticAndSnapshotTypes)");
        }

        // ══════════════ ③⑤ 全事件字节往返(承位口径见文件头:7a 文件级往返未跑,不记绿)══════════════

        [Test]
        public void test_ac21a52_fullEvent_roundTripPreservesPayloadAndOrderKey()
        {
            RecipeSettlementConstants constants = fixture();
            RecipeEntry[] outputs = entries("out", 2);
            RecipeEntry[] inputs = entries("in", 3, 7, 1);
            RecipeSettlementResult result = solve(outputs, inputs, craftSkill: 0, constants);

            var payload = new CraftPayload(
                1, 7, 320L, 40L,
                new long[] { 9001L, 9002L, 9003L },
                result.ActualConsumed,
                result.OutputQty,
                Enumerable.Repeat(result.OutputQuality, result.OutputQty.Length).ToArray(),
                Enumerable.Range(12001, result.OutputQty.Length).Select(i => (long)i).ToArray(),
                new WorldPos(2, 0, 5));
            byte[] blob = PayloadCodec.Encode(payload);

            // 主机 Append 语义:Tick = 点火 tick;Patient = None(世界流无病人,ADR-007 §四);
            // Seq = 主机发放。header + blob 分家(b1b 支 0 甲)。
            var e = new SimEvent(320L, PatientId.None, /*seq*/ 5L, EventKind.Craft,
                new PayloadRef(0, 0, blob.Length));

            // 往返 = SimEventCodec(header)+ PayloadCodec(blob)—— ADR-010 存档体的同一条字节路径
            var pool = new StubBlobPool();
            pool.Add(0, blob);
            SimEvent decodedHeader = SimEventCodec.Decode(SimEventCodec.Encode(e));

            Assert.That(decodedHeader.Tick, Is.EqualTo(320L), "往返后 Tick 不变");
            Assert.That(decodedHeader.Patient, Is.EqualTo(PatientId.None), "往返后 Patient 仍 = None 哨兵");
            Assert.That(decodedHeader.Seq, Is.EqualTo(5L), "往返后 Seq 不变");
            Assert.That(decodedHeader.Kind, Is.EqualTo(EventKind.Craft), "往返后 Kind 不变");

            Assert.That(
                PayloadCodec.TryGetPayload<CraftPayload>(decodedHeader, pool, out CraftPayload got),
                Is.True, "池命中 ⇒ 载荷可解");
            CollectionAssert.AreEqual(result.ActualConsumed, got.ActualConsumed,
                "往返后实耗逐项与求解输出一致(存档往返不丢、不改实耗)");
            for (int j = 0; j < inputs.Length; j++)
                Assert.That(got.ActualConsumed[j],
                    Is.EqualTo(CeilBaseOverEff(inputs[j].Qty, result.Efficiency)),
                    $"往返后实耗[{j}] 仍与 Ceil(基数/EFF) 重算一致(重放判据)");

            // ⑤ 事件级键:Craft → 世界流、Patient = None、键四分量正确
            Assert.That(StreamRouting.Of(EventKind.Craft), Is.EqualTo(StreamId.World),
                "Craft 落世界流(entities.yaml stream: world)");
            EventOrderKey key = EventOrder.KeyOf(decodedHeader);
            Assert.That(key.StreamPriority, Is.EqualTo((int)StreamId.World), "键.StreamPriority = 世界流优先级");
            Assert.That(key.Tick, Is.EqualTo(320L), "键.Tick");
            Assert.That(key.Patient, Is.EqualTo(PatientId.None), "键.Patient = None(D-21-28)");
            Assert.That(key.Seq, Is.EqualTo(5L), "键.Seq");
            Assert.That(key, Is.EqualTo(EventOrder.KeyOf(e)), "往返前后键逐分量一致(重放可排序)");
        }

        // ══════════════ 跨字段约束拒收(坏数据不进字节面)══════════════

        [Test]
        public void test_ac21a52_encode_inputGroupMismatch_throws()
        {
            var bad = new CraftPayload(1, 7, 320L, 40L,
                new long[] { 9001L, 9002L },                  // 输入实例 2
                new[] { 6 },                                   // 实耗 1 ⇒ 输入组不等长
                new[] { 3 }, new[] { 1 }, new long[] { 12001L },
                new WorldPos(2, 0, 5));

            Assert.Throws<ArgumentException>(
                () => PayloadCodec.Encode(bad),
                "输入组(输入实例 ⟷ 实耗)不等长必须写侧拒收 —— 逐实例对位是 20 fold 的承载前提");
        }

        // ══════════════ ⑤ 全序键可排全序(跨流 + 单流 + 哨兵)══════════════

        [Test]
        public void test_ac21a52_orderKey_sortsMixedStreams_totalOrder()
        {
            // 六事件打乱入参:tick 99 抢跑 · 同 tick 100 内三流并发 · 病人流内患者序 · 同患者 Seq 序
            var events = new[]
            {
                Ev(100L, PatientId.None, 2L, EventKind.Craft),          // 世界流 seq 2
                Ev(100L, new PatientId(3), 1L, EventKind.InjuryOnset),  // 病史流 patient 3
                Ev(100L, new PatientId(3), 1L, EventKind.CaseOpened),   // 病例流
                Ev(99L, PatientId.None, 1L, EventKind.Craft),           // 最早 tick
                Ev(100L, PatientId.None, 1L, EventKind.InjuryOnset),    // 病史流 patient None(-1 < 3)
                Ev(100L, PatientId.None, 1L, EventKind.Craft),          // 世界流 seq 1
            };

            Array.Sort(events, (a, b) => EventOrder.Compare(a, b));

            long[] expectedTicks = { 99L, 100L, 100L, 100L, 100L, 100L };
            EventKind[] expectedKinds =
            {
                EventKind.Craft,          // tick 99 先
                EventKind.InjuryOnset,    // tick 100:病史流(p=None)先
                EventKind.InjuryOnset,    //            病史流(p=3)
                EventKind.CaseOpened,     //            病例流次之
                EventKind.Craft,          //            世界流 seq 1
                EventKind.Craft,          //            世界流 seq 2
            };

            CollectionAssert.AreEqual(expectedTicks, events.Select(e => e.Tick).ToArray(),
                "Tick 是第一分量(tick 99 必须最先)");
            CollectionAssert.AreEqual(expectedKinds, events.Select(e => e.Kind).ToArray(),
                "(Tick, StreamPriority, Patient, Seq) 字典序:" +
                "同 tick 病史(0) < 病例(1) < 世界(2);病史内 patient None(-1) < 3;世界内 Seq 升");

            // 比较器公理:自反 · 反对称(确定性的负号镜像)
            foreach (SimEvent x in events)
            {
                Assert.That(EventOrder.Compare(x, x), Is.EqualTo(0), "Compare(a,a) = 0");
                foreach (SimEvent y in events)
                    Assert.That(EventOrder.Compare(x, y), Is.EqualTo(-EventOrder.Compare(y, x)),
                        "Compare(a,b) = -Compare(b,a)(反对称 —— 排序确定性的最低保证)");
            }
        }

        [Test]
        public void test_ac21a52_orderKey_craftKey_deterministicAcrossCalls()
        {
            var craft = Ev(320L, PatientId.None, 5L, EventKind.Craft);
            EventOrderKey k1 = EventOrder.KeyOf(craft);
            EventOrderKey k2 = EventOrder.KeyOf(craft);

            Assert.That(k1, Is.EqualTo(k2), "同事件两次取键逐分量一致(纯函数)");
            Assert.That(k1.GetHashCode(), Is.EqualTo(k2.GetHashCode()), "等键必等哈希(排序/去重可用)");
            Assert.That(EventOrderKeyComparerCall(k1, k2), Is.EqualTo(0),
                "IComparable 面与 EventOrder.Compare 同语义");
        }

        private static int EventOrderKeyComparerCall(EventOrderKey a, EventOrderKey b) => a.CompareTo(b);

        private static SimEvent Ev(long tick, PatientId patient, long seq, EventKind kind) =>
            new SimEvent(tick, patient, seq, kind, new PayloadRef(0, 0, 0));

        // ── 测试桩(与 sim_codec_roundtrip_test 同型;IBlobPool 为 Sim.Codec 公共契约)──

        private sealed class StubBlobPool : IBlobPool
        {
            private readonly Dictionary<int, byte[]> _blobs = new Dictionary<int, byte[]>();

            public void Add(int blobId, byte[] bytes) => _blobs[blobId] = bytes;

            public bool TryGetBlob(int blobId, out ReadOnlyMemory<byte> blob)
            {
                if (_blobs.TryGetValue(blobId, out var bytes)) { blob = bytes; return true; }
                blob = default;
                return false;
            }
        }
    }
}
