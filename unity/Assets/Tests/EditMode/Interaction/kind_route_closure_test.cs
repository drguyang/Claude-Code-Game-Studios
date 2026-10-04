// interaction-system Story 004 —— Kind 枚举 ⟷ 路由表双向对拍(AC-4-18)
//
// 登记落点: tests/integration/interaction/kind_route_closure_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/kind_route_closure_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— AC-4-18 / 4-DC-2(十项闭集)/ 4-DC-5(RoutesTo 登记)
//     / 规则一(路由表)/ 规则五(S-8.4 路线甲:Patient 单义 → 37)
//   ADR-024(Kind 单一真源:4 侧本故事**零新 Kind**)
//
// ⚠️ AC-4-18 的判据:
//   GIVEN 4 的 Kind 枚举(4-DC-2),WHEN 与路由表的行数**逐一对拍**,
//   THEN 恰为 10 项、双向闭合(无枚举外的行、无行外的枚举值);且 `Player ∉ 枚举`。
//   ⇒ **原稿的 `Kind(c) ≠ Player` 谓词是空转**(判在不可能出现 Player 的位置),
//     GDD 二轮删除后其意图由本条承担 —— **禁**在本文件复现旧谓词。
//
// ⚠️ 反空转:四张违例表(11 行 / 9 行 / 行含枚举外值 / RoutesTo 未登记)逐条被拒。

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Gameplay.Interaction;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class KindRouteClosureTest
    {
        /// <summary>合法路由表替身(GDD 规则一表的 10 行;⚠️ 值归数值轮,**形状**照录)。
        /// <para>RoutesTo 逐项来自登记系统:Drop→20 / ForageSpot→17 / Patient→37 / PoiCell→6 /
        /// Container→20 / BuildSlot→23 / Utensil→18 / ClinicPanel→24 / Door→23 / Switch→6。</para>
        /// <para>⚠️ GDD 规则一表对 Door / Switch 写「6 / 23」二择;此处取**单值**
        /// (路由表是 kind→**单一**拥有方的映射,AC-4-18 的「每 Kind 必有落点」= 恰一行)。</para></summary>
        private static Dictionary<InteractableKind, int> LegalTable() => new Dictionary<InteractableKind, int>
        {
            { InteractableKind.Drop,        20 },
            { InteractableKind.ForageSpot,  17 },
            { InteractableKind.Patient,     37 },
            { InteractableKind.PoiCell,      6 },
            { InteractableKind.Container,   20 },
            { InteractableKind.BuildSlot,   23 },
            { InteractableKind.Utensil,     18 },
            { InteractableKind.ClinicPanel, 24 },
            { InteractableKind.Door,        23 },
            { InteractableKind.Switch,       6 },
        };

        [Test]
        public void test_ac418_legalTableIsBidirectionallyClosedAtTenItems()
        {
            var table = new KindRouteTable(LegalTable());
            var kinds = (InteractableKind[])Enum.GetValues(typeof(InteractableKind));

            Assert.AreEqual(10, kinds.Length, "AC-4-18:Kind 枚举恰 10 项(4-DC-2 闭集)");
            Assert.AreEqual(10, table.Count, "AC-4-18:路由表恰 10 行");
            // 双向闭合:枚举集 == 行集
            foreach (var k in kinds)
                Assert.DoesNotThrow(() => table.RoutesTo(k), $"AC-4-18:{k} 须有落点");
            // ⚠️ 行数 == 枚举数 且 每枚举有落点 ⇒ 双向闭合(差集归零)
        }

        [Test]
        public void test_ac418_playerIsNotInKindEnum()
        {
            // 编译期类型断言 + 运行时值域断言双保险。
            var kinds = (InteractableKind[])Enum.GetValues(typeof(InteractableKind));
            Assert.IsFalse(kinds.Any(k => k.ToString() == "Player"),
                "AC-4-18:`Player` ∉ InteractableKind 枚举(玩家不是交互目标)");
        }

        // ── 四张违例表逐条被拒(装载期硬失败,非运行期告警)──────────────

        [Test]
        public void test_ac418_negative_rowWithValueOutsideEnumIsRejected()
        {
            // 「表有而枚举无」的可构造形态:用**强转**造一个枚举值域外的 kind((InteractableKind)99)。
            // 这正是「枚举外的行」在 C# 的可表达面 —— 装载期 `extra` 分支须拒。
            var table = LegalTable();
            table[(InteractableKind)99] = 6;             // 枚举外的值 ⇒ 表比枚举多一行
            var ex = Assert.Throws<ArgumentException>(() => new KindRouteTable(table),
                "AC-4-18:表含枚举外的 kind 值((InteractableKind)99)须装载期硬失败");
            Assert.IsTrue(ex.Message.Contains("99"), "违例信息须点名枚举外的值");
        }

        [Test]
        public void test_ac418_negative_nineRowsMissingOneIsRejected()
        {
            var table = LegalTable();
            table.Remove(InteractableKind.Switch);       // 缺 1 项 ⇒ 9 行
            var ex = Assert.Throws<ArgumentException>(() => new KindRouteTable(table),
                "AC-4-18:缺项(9 行)须装载期硬失败 —— 缺项 = 该 kind 永不可路由(静默失效)");
            Assert.IsTrue(ex.Message.Contains("Switch"), "违例信息须点名缺失的 kind(Switch)");
        }

        [Test]
        public void test_ac418_negative_unregisteredRoutesToIsRejected()
        {
            var table = LegalTable();
            table[InteractableKind.Door] = 999;          // 未登记系统号
            var ex = Assert.Throws<ArgumentException>(() => new KindRouteTable(table),
                "4-DC-5:RoutesTo 指向未登记系统(999)须装载期硬失败 —— 引用却无登记的失效模式");
            Assert.IsTrue(ex.Message.Contains("999"), "违例信息须点名未登记系统号");
        }

        [Test]
        public void test_ac418_negative_nullTableThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new KindRouteTable(null),
                "AC-4-18:null 表须拒(不静默空表)");
        }

        [Test]
        public void test_ac418_routesToResolvesEachKindToItsRegisteredSystem()
        {
            // 逐项核对合法表的落点(非只数行数)—— 防「行数对但内容错」。
            var table = new KindRouteTable(LegalTable());
            Assert.AreEqual(20, table.RoutesTo(InteractableKind.Drop), "Drop → 20(拾取裁决)");
            Assert.AreEqual(17, table.RoutesTo(InteractableKind.ForageSpot), "ForageSpot → 17(采集裁决)");
            Assert.AreEqual(37, table.RoutesTo(InteractableKind.Patient), "Patient → 37(就诊立案,S-8.4 路线甲)");
            Assert.AreEqual(6,  table.RoutesTo(InteractableKind.PoiCell), "PoiCell → 6(唯一写者,ADR-021)");
            Assert.AreEqual(20, table.RoutesTo(InteractableKind.Container), "Container → 20(开箱)");
            Assert.AreEqual(23, table.RoutesTo(InteractableKind.BuildSlot), "BuildSlot → 23(放置/拆除)");
            Assert.AreEqual(18, table.RoutesTo(InteractableKind.Utensil), "Utensil → 18(炮制准入)");
            Assert.AreEqual(24, table.RoutesTo(InteractableKind.ClinicPanel), "ClinicPanel → 24(面板)");
            Assert.AreEqual(23, table.RoutesTo(InteractableKind.Door), "Door → 23(世界几何)");
            Assert.AreEqual(6,  table.RoutesTo(InteractableKind.Switch), "Switch → 6(可改 POI 状态)");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-5 —— 已登记系统集**不得窄于** GDD 规则一表第二列
        //  ⚠️ 反空转:本条的判据 = 用**GDD 表里出现但 LegalTable 未用**的系统号
        //     (8 / 10 / 11 —— Patient 行的四义另一半)造一张**合法**表;
        //     装载器若把集合收紧到「LegalTable 实际用到的值」⇒ 误拒 ⇒ 红。
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_dc5_registeredSystemSetIsNotNarrowerThanGddRuleOneTable()
        {
            // GDD 规则一表第二列 distinct 值 = {6,8,10,11,17,18,20,23,24,37}。
            // 取 Patient 行**四义**中 LegalTable 未用的三个(8 查体 / 10 急救 / 11 施治)
            // 各造一张合法表 —— 它们**都不在世界空间路由里**,但都在 4-DC-5 的集内。
            foreach (int sys in new[] { 8, 10, 11 })
            {
                var table = LegalTable();
                table[InteractableKind.Patient] = sys;      // 合法:GDD 规则一 Patient 行 = 37 / 8 / 10 / 11
                Assert.DoesNotThrow(() => new KindRouteTable(table),
                    $"4-DC-5:Patient → {sys} 是 GDD 规则一表列明的路由,装载器**不得**误拒" +
                    "(集合窄于 4-DC-5 = 判据比 AC 更严 = 合法表被拒)");
            }
        }

        [Test]
        public void test_dc5_negativeFixture_narrowedSetWouldFalselyReject()
        {
            // 负夹具(证明上条非空转):若集合被收紧成 LegalTable 实际用到的 7 个值
            // ({20,17,37,6,23,18,24}),则 Patient → 8 会被误拒。
            // 用**同一台**判据(装载器)、换一张表 —— 「窄集合 ⇒ 误拒」可辨。
            var narrowed = new HashSet<int> { 20, 17, 37, 6, 23, 18, 24 };   // ← 缺陷形态
            Assert.IsFalse(narrowed.Contains(8),
                "负夹具前置:收紧后的集合确不含 8(窄于 GDD 表)");
            // 真装载器用的是 GDD 全集 ⇒ Patient → 8 通过(与上条同判据,形态相反)
            var table = LegalTable();
            table[InteractableKind.Patient] = 8;
            Assert.DoesNotThrow(() => new KindRouteTable(table),
                "负夹具对照:真装载器(全集)对该表放行 —— 与「窄集合会拒」构成可辨对拍");
        }
    }
}
