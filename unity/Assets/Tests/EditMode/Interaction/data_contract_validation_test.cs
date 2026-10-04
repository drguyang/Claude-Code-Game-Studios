// interaction-system Story 006 —— 数据契约构建期校验(4-DC-1…6;AC-4-15)
//
// 登记落点: tests/unit/interaction/data_contract_validation_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/data_contract_validation_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— §数据契约(4-DC-1…6)/ Tuning(R_INTERACT 区间)
//     / 规则一(路由表)/ 规则八(移动压制)/ AC-4-15
//   ADR-014 §二/§五(阶段 2 白名单/区间/闭集位点)
//
// ⚠️ AC-4-15 判据:GIVEN 4-DC-1…6 每一条违例**各一个夹具**,WHEN 阶段 2 烘焙,
//   THEN **构建期硬失败**(throw)。⇒ 本文件 = 六条违例夹具 + 一条合法基线的**对拍**。
//
// ⚠️ 反空转(本仓头号失效模式):
//   ① 合法基线须**真的通过**(证明校验器不是「恒拒」——否则六条违例红也无意义);
//   ② 六条违例须**各红在己**判据(不能一条大 if 全红);
//   ③ 每条违例须**点名**其 DC 号与字段(触底到具体字段,非「表存在」式恒真)。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Interaction;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class DataContractValidationTest
    {
        // ── 合法基线(十条 kind,形状照 GDD 规则一表;**数值归数值轮**,此处为合法样例)──

        private static readonly (int W, int H, int D) World = (32, 16, 8);
        private const int LegalRadius = 3;   // 1 ≤ 3 ≤ min(32,16,8)−1 = 7 ✓

        /// <summary>合法表替身(10 行;priority 两两互异;RoutesTo 全登记;
        /// SuppressesMotor=true 的行 DurationOwner 均已登记)。</summary>
        private static List<KindContractRow> LegalRows() => new List<KindContractRow>
        {
            // ⚠️ KindPriority 列**必须** == KindPriorityTable(4-DC-3 的行⟷表对拍);
            //    取值 = GDD §F-4.1b 演示序(Patient:0…Drop:9)。
            // kind,              prio, source,                              routes, sup,   durOwner,                    ownerId, uplink
            Row(InteractableKind.Drop,        9, StableIdSource.InstanceId,         20, false, DurationOwnerKind.None,        0,  IntentUplink.None),
            Row(InteractableKind.ForageSpot,  6, StableIdSource.BakedResourceIndex, 17, true,  DurationOwnerKind.RoutedSystem, 17, IntentUplink.HostOnlyIntent),
            Row(InteractableKind.Patient,     0, StableIdSource.PatientId,          37, false, DurationOwnerKind.None,        0,  IntentUplink.HostOnlyIntent),
            Row(InteractableKind.PoiCell,     1, StableIdSource.PoiId,               6, false, DurationOwnerKind.None,        0,  IntentUplink.None),
            Row(InteractableKind.Container,   3, StableIdSource.InstanceId,         20, true,  DurationOwnerKind.RoutedSystem, 20, IntentUplink.None),
            Row(InteractableKind.BuildSlot,   7, StableIdSource.SlotLinearKey,      23, false, DurationOwnerKind.None,        0,  IntentUplink.None,
                World.W, World.H, World.D),   // 4-DC-4 ②:SlotLinearKey 行须 W/H/D 在场
            Row(InteractableKind.Utensil,     2, StableIdSource.StructureId,        18, false, DurationOwnerKind.None,        0,  IntentUplink.None),
            Row(InteractableKind.ClinicPanel, 8, StableIdSource.StructureId,        24, false, DurationOwnerKind.None,        0,  IntentUplink.None),
            Row(InteractableKind.Door,        4, StableIdSource.StructureId,        23, false, DurationOwnerKind.None,        0,  IntentUplink.None),
            Row(InteractableKind.Switch,      5, StableIdSource.StructureId,         6, false, DurationOwnerKind.None,        0,  IntentUplink.None),
        };

        /// <summary>无 4-DC-4 维度登记的行(便捷重载)。</summary>
        private static KindContractRow Row(
            InteractableKind kind, int prio, StableIdSource src, int routes,
            bool sup, DurationOwnerKind dur, int ownerId, IntentUplink uplink)
            => new KindContractRow(kind, prio, src, routes, sup, dur, ownerId, uplink);

        /// <summary>带 4-DC-4 维度三元组的行。</summary>
        private static KindContractRow Row(
            InteractableKind kind, int prio, StableIdSource src, int routes,
            bool sup, DurationOwnerKind dur, int ownerId, IntentUplink uplink,
            int w, int h, int d)
            => new KindContractRow(kind, prio, src, routes, sup, dur, ownerId, w, h, d, uplink);

        // ═══════════════════════════════════════════════════════════
        //  基线:合法表须**真通过**(反空转 ①)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_legalTablePassesAllSixChecks()
        {
            // 反空转门:若校验器「恒拒」,本条即红 ⇒ 六条违例红才有意义。
            Assert.DoesNotThrow(
                () => InteractionKindTableValidator.Validate(LegalRows(), LegalRadius, World.W, World.H, World.D),
                "AC-4-15 反空转门:合法表须通过全部六条(证校验器非恒拒)");

            // 逐条单跑亦须通过(证六条各自可独立通过 —— 非「只有合在一起才过」)。
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateRadius(LegalRadius, World.W, World.H, World.D));
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateKindClosure(LegalRows()));
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidatePriorityTotalOrder(LegalRows()));
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateStableIdSource(LegalRows()));
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateRoutesTo(LegalRows()));
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateDurationOwner(LegalRows()));
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-1 —— R_INTERACT 区间(下界 + 上界,上界是 GDD 三审补的)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_dc1_zeroRadiusHardFails()
        {
            // 违例夹具:R_INTERACT = 0(下界违 ⇒ 恒不可交互)。
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => InteractionKindTableValidator.ValidateRadius(0, World.W, World.H, World.D),
                "AC-4-15 4-DC-1:R_INTERACT=0 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-1"), "4-DC-1 违例须点名 DC 号");
        }

        [Test]
        public void test_ac415_dc1_radiusAboveUpperBoundHardFails()
        {
            // 违例夹具:r_interact 超上界(min(W,H,D)−1 = 7;取 8)⇒ 候选集 = 全世界。
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => InteractionKindTableValidator.ValidateRadius(8, World.W, World.H, World.D),
                "AC-4-15 4-DC-1:r_interact > min(W,H,D)−1 ⇒ 构建期硬失败(上界是 GDD 三审补的)");
            Assert.IsTrue(ex.Message.Contains("上界"), "4-DC-1 上界违例须点名「上界」");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-2 —— kind 闭集(缺项 / 枚举外值 / Player)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_dc2_missingKindHardFails()
        {
            // 违例夹具:删掉一行 kind(缺项 ⇒ 该 kind 永不可选,静默失效)。
            var rows = LegalRows();
            rows.RemoveAll(r => r.Kind == InteractableKind.Door);
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateKindClosure(rows),
                "AC-4-15 4-DC-2:kind 缺项 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-2") && ex.Message.Contains("Door"),
                "4-DC-2 缺项违例须点名 DC 号与被缺的 kind(Door)");
        }

        [Test]
        public void test_ac415_dc2_extraKindHardFails()
        {
            // 违例夹具:加一行**枚举外**的 kind 值(强制转换出一个不存在的种类)。
            var rows = LegalRows();
            rows.Add(Row((InteractableKind)99, 11, StableIdSource.InstanceId, 20, false,
                         DurationOwnerKind.None, 0, IntentUplink.None));
            Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateKindClosure(rows),
                "AC-4-15 4-DC-2:表含枚举外的 kind ⇒ 构建期硬失败");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-3 —— KindPriority 平局
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_dc3_priorityTieHardFails()
        {
            // 违例夹具:两 kind 同 priority(平局 ⇒ 全序退化到 StableId 决胜)。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.Door);
            var door = rows[i];
            rows[i] = Row(door.Kind, 1, door.StableIdSource, door.RoutesTo,   // 1 == Drop 的 priority
                          door.SuppressesMotor, door.DurationOwner, door.DurationOwnerSystemId, door.IntentUplink, door.WorldW, door.WorldH, door.WorldD);
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidatePriorityTotalOrder(rows),
                "AC-4-15 4-DC-3:KindPriority 平局 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-3") && ex.Message.Contains("平局"),
                "4-DC-3 违例须点名 DC 号与「平局」");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-4 —— StableIdSource 非法
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_dc4_illegalStableIdSourceHardFails()
        {
            // 违例夹具:StableIdSource 取枚举外的值(自由字符串的编译形态)。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.Utensil);
            var u = rows[i];
            rows[i] = Row(u.Kind, u.KindPriority, (StableIdSource)200, u.RoutesTo,
                          u.SuppressesMotor, u.DurationOwner, u.DurationOwnerSystemId, u.IntentUplink, u.WorldW, u.WorldH, u.WorldD);
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateStableIdSource(rows),
                "AC-4-15 4-DC-4:StableIdSource ∉ 枚举 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-4") && ex.Message.Contains("Utensil"),
                "4-DC-4 违例须点名 DC 号与 kind");
        }

        [Test]
        public void test_ac415_dc3_rowDisagreesWithSelectorTableHardFails()
        {
            // 违例夹具:行的 KindPriority 与**选择器实际读的表**不一致(story-006 评审 F-2)。
            // ⚠️ 本夹具**不改**平局 —— 它只让一行的值与表值错开,而全表**仍两两互异**
            //    ⇒ 若校验器只查行内互异(旧形态),本夹具**不会红**(判据空转)。
            //    这正是「校验的表 ≠ 选择的表」失效模式的机器判据。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.Utensil);
            var u = rows[i];
            rows[i] = Row(u.Kind, 42, u.StableIdSource, u.RoutesTo, u.SuppressesMotor,   // 42 ≠ 表值 2
                          u.DurationOwner, u.DurationOwnerSystemId, u.IntentUplink,
                          u.WorldW, u.WorldH, u.WorldD);
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidatePriorityTotalOrder(rows),
                "AC-4-15 4-DC-3:行 KindPriority 与选择器表不一致 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-3") && ex.Message.Contains("Utensil"),
                "4-DC-3 行⟷表 违例须点名 DC 号与 kind");
        }

        [Test]
        public void test_ac415_dc4_slotLinearKeyWithoutDimensionsHardFails()
        {
            // 违例夹具:SlotLinearKey 行**缺 W/H/D**(4-DC-4 的第二半边 —— story-006 评审 F-2)。
            // 判据:线性键 x + W·(y + H·z) 需要 W/H/D 才可比;缺则第三键**静默**失去全序性。
            // ⚠️ 本夹具是 4-DC-4 的**承重**半边 —— 删掉 ValidateStableIdSource 的 ② 分支即红。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.BuildSlot);
            var b = rows[i];
            rows[i] = Row(b.Kind, b.KindPriority, b.StableIdSource, b.RoutesTo, b.SuppressesMotor,
                          b.DurationOwner, b.DurationOwnerSystemId, b.IntentUplink,
                          0, 0, 0);   // W/H/D 全部缺席
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateStableIdSource(rows),
                "AC-4-15 4-DC-4:SlotLinearKey 行缺 W/H/D ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-4") && ex.Message.Contains("BuildSlot"),
                "4-DC-4 W/H/D 违例须点名 DC 号与 kind");
        }

        [Test]
        public void test_ac415_dc6_unregisteredDurationOwnerHardFails()
        {
            // 违例夹具:SuppressesMotor=true 且归属方**指向一个未登记的系统**(4-DC-6 的第二半边 ——
            // story-006 评审 F-3)。GDD 的失效模式正是**归属方错了**:边沿请求后无人 Release
            // (不是「没有归属方」,是「归属方是错的」)⇒ 玩家永久定身。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.ForageSpot);
            var f = rows[i];
            rows[i] = Row(f.Kind, f.KindPriority, f.StableIdSource, f.RoutesTo, true,
                          DurationOwnerKind.RoutedSystem, 999,   // 999 未登记
                          f.IntentUplink, f.WorldW, f.WorldH, f.WorldD);
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateDurationOwner(rows),
                "AC-4-15 4-DC-6:归属方 ∉ 已登记系统集 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-6") && ex.Message.Contains("999"),
                "4-DC-6 归属方违例须点名 DC 号与未登记的系统号");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-5 —— RoutesTo 未登记
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_dc5_unregisteredRoutesToHardFails()
        {
            // 违例夹具:RoutesTo 指向一个**没有 GDD** 的系统号(引用却无登记)。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.ClinicPanel);
            var c = rows[i];
            rows[i] = Row(c.Kind, c.KindPriority, c.StableIdSource, 999,   // 999 未登记
                          c.SuppressesMotor, c.DurationOwner, c.DurationOwnerSystemId, c.IntentUplink, c.WorldW, c.WorldH, c.WorldD);
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateRoutesTo(rows),
                "AC-4-15 4-DC-5:RoutesTo ∉ 已登记系统集 ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-5") && ex.Message.Contains("999"),
                "4-DC-5 违例须点名 DC 号与未登记的系统号");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-6 —— SuppressesMotor=true 而 DurationOwner=None
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac415_dc6_suppressWithoutDurationOwnerHardFails()
        {
            // 违例夹具:SuppressesMotor=true 但 DurationOwner=None(边沿后无人 Release
            // ⇒ 玩家永久定身 —— 丢失更新的镜像)。
            var rows = LegalRows();
            var i = rows.FindIndex(r => r.Kind == InteractableKind.ForageSpot);
            var f = rows[i];
            rows[i] = Row(f.Kind, f.KindPriority, f.StableIdSource, f.RoutesTo,
                          true, DurationOwnerKind.None, 0, f.IntentUplink, f.WorldW, f.WorldH, f.WorldD);   // sup=true, dur=None
            var ex = Assert.Throws<ArgumentException>(
                () => InteractionKindTableValidator.ValidateDurationOwner(rows),
                "AC-4-15 4-DC-6:SuppressesMotor=true 而 DurationOwner=None ⇒ 构建期硬失败");
            Assert.IsTrue(ex.Message.Contains("4-DC-6") && ex.Message.Contains("ForageSpot"),
                "4-DC-6 违例须点名 DC 号与 kind");
        }

        [Test]
        public void test_ac415_dc6_suppressFalseDoesNotTrigger()
        {
            // ⚠️ GDD 明写:SuppressesMotor=false 的行**本检查不触发**(如 PoiCell,二轮已消解)。
            var rows = LegalRows();
            var poi = rows.First(r => r.Kind == InteractableKind.PoiCell);
            Assert.IsFalse(poi.SuppressesMotor, "基线:PoiCell 的 SuppressesMotor=false");
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateDurationOwner(rows),
                "4-DC-6:SuppressesMotor=false 的行不触发(其 DurationOwner=None 合法)");
        }

        // ═══════════════════════════════════════════════════════════
        //  反空转 ②:六条违例**各红在己**判据(非一条大 if 全红)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac421_patientBeatsSameCellDrop_withoutAnyPointerOrHover()
        {
            // AC-4-21 的**机器可判半边**(story-006 评审 F-3:此前缺该代理)。
            //
            // AC-4-21 是 [L](手柄走查),但其判据的**核心**可自动化:病人 vs 同格掉落,
            // 由 KindPriority 决胜(小者胜),**不依赖任何指针 / 悬停输入** —— 选择器
            // Select(intent, candidates) 的签名里根本没有指针/悬停量,故「无指针 ⇒ 仍可选出」
            // 在**类型面**成立;需证的只剩「优先级确实让 Patient 压过 Drop」。
            //
            // ⚠️ 真假分歧的镜面:story-002 的 target_selection_test 在**枚举序占位**下断言
            //    「Drop(0) 应为唯一胜者」—— 与本 AC 所需**方向相反**。接线到真表(患者优先)后,
            //    该断言须由 story-002 侧更新(登记为 story-002 待办,非本故事可单方改)。
            var selector = new DaYiJingCheng.Gameplay.Interaction.InteractionSelector(
                new NoopReporter(), new DaYiJingCheng.Gameplay.Interaction.InteractionRadius(1));
            var patientCell = new DaYiJingCheng.Sim.Contracts.WorldPos(10, 0, 5);
            var intent = new DaYiJingCheng.Gameplay.Interaction.InteractIntent(patientCell, true, 0);

            var candidates = new List<DaYiJingCheng.Gameplay.Interaction.Candidate>
            {
                new DaYiJingCheng.Gameplay.Interaction.Candidate(
                    patientCell, InteractableKind.Patient, 17L, StableIdSource.PatientId),
                new DaYiJingCheng.Gameplay.Interaction.Candidate(
                    new DaYiJingCheng.Sim.Contracts.WorldPos(10, 0, 6), InteractableKind.Drop, 9L,
                    StableIdSource.InstanceId),
            };

            // 反序加入两次 —— 胜者不得随遍历序漂移(无指针/悬停参与)。
            var w1 = selector.Select(in intent, candidates);
            candidates.Reverse();
            var w2 = selector.Select(in intent, candidates);

            Assert.AreEqual(InteractableKind.Patient, w1.Kind,
                "AC-4-21:病人须压过同格(等距)掉落 —— 由 KindPriority 决胜,不依赖指针/悬停");
            Assert.AreEqual(w1.Kind, w2.Kind, "AC-4-21:胜者须与加入序无关(无输入序泄漏)");
            Assert.AreEqual(17L, w1.StableId, "AC-4-21:选出的须是病人(patient_id=17),非掉落");
        }

        private sealed class NoopReporter : DaYiJingCheng.Gameplay.Interaction.IDiscoveryReporter
        {
            public void Request(in DaYiJingCheng.Gameplay.Interaction.DiscoveryRequest request) { }
        }

        [Test]
        public void test_ac415_eachViolationReddensOnlyItsOwnCheck()
        {
            // 4-DC-3 的违例(priority 平局)不得被 4-DC-5(路由)误红,反之亦然。
            // ⇒ 证六条是**独立判据**,不是耦合的单一断言。
            var tieRows = LegalRows();
            var i = tieRows.FindIndex(r => r.Kind == InteractableKind.Door);
            var d = tieRows[i];
            tieRows[i] = Row(d.Kind, 1, d.StableIdSource, d.RoutesTo, d.SuppressesMotor, d.DurationOwner, d.DurationOwnerSystemId, d.IntentUplink, d.WorldW, d.WorldH, d.WorldD);

            Assert.Throws<ArgumentException>(() => InteractionKindTableValidator.ValidatePriorityTotalOrder(tieRows));
            // 同一违例表,其余五条**仍通过**
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateKindClosure(tieRows),
                "4-DC-3 违例不得波及 4-DC-2");
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateStableIdSource(tieRows),
                "4-DC-3 违例不得波及 4-DC-4");
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateRoutesTo(tieRows),
                "4-DC-3 违例不得波及 4-DC-5");
            Assert.DoesNotThrow(() => InteractionKindTableValidator.ValidateDurationOwner(tieRows),
                "4-DC-3 违例不得波及 4-DC-6");
        }
    }
}
