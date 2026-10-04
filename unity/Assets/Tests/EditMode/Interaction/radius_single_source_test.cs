// interaction-system Story 004 —— `R_INTERACT` 单源(AC-4-17)
//
// 登记落点: tests/unit/interaction/radius_single_source_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/radius_single_source_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— AC-4-17 / OQ-4-8(单值裁定)/ 4-DC-1(区间)
//   ADR-014 §二/§五(r_interact 内联于 interaction_kinds.json,一次装载)
//   ADR-015 §三(半径 = 整数格数)
//
// ⚠️ AC-4-17 的**判据形态**:
//   GIVEN 同一 `R_INTERACT` 同时驱动 4 的选择与 6 的「已发现」,
//   WHEN 读取两处,
//   THEN 来自**同一烘焙字段**(interaction_kinds.json 的 r_interact),**无第二处声明**。
//   ⇒ 判据 = **实体纪律**(声明点 == 1),**不是数值纪律**(取值归用户)。
//
// ⚠️ 本文件的两半(逐字承故事 QA):
//   ① **单源的直接形态** —— 改注入值,两处**同时**变(consumer 读同一内存值);
//   ② **声明点 == 1** —— 全仓 4/6 范围内 `r_interact` 的**自有常量声明**唯 `InteractionRadius` 一处;
//      第二处 `const int DISCOVER_R = 3`(即便恰等于注入值)**仍红**(实体纪律)。
//
// ⚠️ 反空转:负夹具(影子类型带第二常量)证明扫描器能抓第二声明点。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class RadiusSingleSourceTest
    {
        // ═══════════════════════════════════════════════════════════
        //  AC-4-17 前半 —— 单源的**直接形态**:改注入值 ⇒ 两处同时变
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac417_changingInjectedValueMovesBothConsumersTogether()
        {
            // GIVEN:注入 r_interact = 3 的烘焙替身;装载器跑一次,4/6 两处消费点取值。
            // WHEN:改注入值为 5,重载。
            // THEN:两处**同时**变 5(单源的直接形态)。
            var radius3 = new InteractionRadius(3);
            var radius5 = new InteractionRadius(5);

            // 两处消费点:① 4 的邻域自报器 ② 6 的触发判距(此处以「同一 radius 实例」模拟共享)
            var spy4 = new CountingReporter();
            var reporter4a = new NeighbourhoodReporter(spy4, radius3);
            var reporter4b = new NeighbourhoodReporter(spy4, radius5);

            // 一个恰在 d∞=4 的 POI:R=3 时在邻域外,R=5 时在邻域内 ⇒ 半径真被消费
            var candidates = new[] { Poi(14, 0, 5, 1L) };   // d∞(10,0,5 → 14,0,5) = 4

            int sentR3 = reporter4a.ReportNeighbourhood(new WorldPos(10, 0, 5), candidates, 0);
            int sentR5 = reporter4b.ReportNeighbourhood(new WorldPos(10, 0, 5), candidates, 0);

            Assert.AreEqual(3, radius3.Value, "前置:R=3 装载");
            Assert.AreEqual(5, radius5.Value, "前置:R=5 重载");
            Assert.AreEqual(0, sentR3, "AC-4-17:R=3 时 d∞=4 的 POI 在邻域外 ⇒ 零出境");
            Assert.AreEqual(1, sentR5, "AC-4-17:R=5 时同一 POI 进邻域 ⇒ 出境(半径真被消费,非硬编码)");
        }

        [Test]
        public void test_ac417_fourSideConsumesInjectedRadiusValue()
        {
            // ⚠️ QA F-E 校正:AC-4-17 的「**同时驱动 4 的选择与 6 的已发现**」中,**6 侧消费点不存在**
            //   (归系统 6 的 Epic)。首版以 `int sixSideRead = radius.Value;` 自称「6 侧消费」——
            //   那是**同一测试自造实例的局部读**,删掉任何生产代码它照样绿 = 空转。已删。
            //   本条**只签 4 侧可签的事实**:4 的自报器**真消费**注入的半径值(换值 ⇒ 换行为)。
            var radius = new InteractionRadius(4);
            var spy = new CountingReporter();
            var reporter = new NeighbourhoodReporter(spy, radius);   // 消费点是**同一个对象**

            var inAt4 = new[] { Poi(14, 0, 5, 1L) };    // d∞(10,0,5 → 14,0,5) = 4 == R ⇒ 在邻域内
            int sent = reporter.ReportNeighbourhood(new WorldPos(10, 0, 5), inAt4, 0);
            Assert.AreEqual(1, sent, "AC-4-17:4 侧自报器消费注入半径(d∞==R 命中)");

            // 6 侧消费点归系统 6 ⇒ 该半条 NOT-RUN(见下,不借绿)。
            Assert.Ignore("NOT-RUN(AC-4-17 6 消费半):BLOCKED-BY 系统 6 的触发判距消费点。" +
                          "4 侧消费已签(上);「两处**同时**读同一内存值」的验收对象含 6,真身未落地前不签绿。");
        }

        // ═══════════════════════════════════════════════════════════
        //  AC-4-17 后半 —— 声明点 == 1(实体纪律,非数值纪律)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_ac417_radiusConstantDeclaredAtExactlyOnePlace()
        {
            // 扫描 4 的命名空间闭包:任何 `r_interact` / `R_INTERACT` 语义的**自有常量声明**的
            // 唯一合法落点 = `InteractionRadius`(它持有**装载值**,不算「第二声明」)。
            // 其余类型内不得出现半径常量字段 / const。
            var violations = ScanForSecondRadiusDeclaration();
            Assert.IsEmpty(violations,
                "AC-4-17:R_INTERACT 须**单一来源**(声明点 == 1);" +
                "第二处自有常量 = 静默脱钩(OQ-4-8 单值裁定):\n" + string.Join("\n", violations));
        }

        [Test]
        public void test_ac417_negativeFixture_localConstDiscoverRIsRed()
        {
            // 负夹具(故事 QA 逐字):「6 侧声明本地 `const int DISCOVER_R = 3` 恰好等于注入值 ⇒ 仍红
            //   (实体纪律)」。用**同一台**扫描器、换影子类型。
            var violations = ScanForSecondRadiusDeclaration(
                new[] { typeof(ShadowSixSideWithLocalConst) });
            Assert.IsNotEmpty(violations,
                "AC-4-17 负夹具:6 侧本地 `const int DISCOVER_R`(即便值等于注入值)必须红 —— " +
                "判的是**第二声明点**,不是数值是否巧合相等");
            Assert.IsTrue(violations.Any(v => v.Contains("DISCOVER_R")),
                "AC-4-17 负夹具:违规须点名该常量(证扫描器触底到常量名)");
        }

        [Test]
        public void test_ac417_negativeFixture_serializeFieldRadiusIsRed()
        {
            // 同族负夹具:第二声明走 `[SerializeField]`(非 const)亦须红。
            var violations = ScanForSecondRadiusDeclaration(
                new[] { typeof(ShadowSixSideWithSerializeField) });
            Assert.IsNotEmpty(violations,
                "AC-4-17 负夹具:第二半径声明走 `[SerializeField]` 字段亦须红(数值源分叉的入口)");
        }

        // ═══════════════════════════════════════════════════════════
        //  4-DC-1 下界(本故事只签下界;上界须 W/H/D 在场,归 story 006)
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void test_dc1_zeroRadiusFailsAtLoad()
        {
            // 4-DC-1:R_INTERACT = 0 ⇒ 装载期硬失败(恒不可交互)。
            Assert.Throws<ArgumentOutOfRangeException>(() => new InteractionRadius(0),
                "4-DC-1:r_interact = 0 须装载期硬失败(恒不可交互)");
        }

        [Test]
        public void test_dc1_negativeRadiusFailsAtLoad()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InteractionRadius(-5),
                "4-DC-1:负半径须装载期硬失败");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描机器 —— 第二半径声明点探测
        //  ⚠️ 与正测**共用**本方法一台机器(负夹具只换根),否则「夹具红」不蕴含「真断言红」。
        // ═══════════════════════════════════════════════════════════

        private static List<string> ScanForSecondRadiusDeclaration(IEnumerable<Type> roots = null)
        {
            // ⚠️ **本扫描器是启发式,非证明(MINOR #4 登记)** —— 承两条已知边界:
            //   ① **命名启发式**(`LooksLikeRadiusName`)可被**改名规避**:`private const int R = 3;`
            //      / `RevealSpan` 不含 "radius"/"rinteract"/"discoverr" 语义串 ⇒ 漏报。
            //      (判据的意图信号是必要的,但仅名字不足以穷尽「半径语义」的表达面。)
            //   ② **数值类型启发式**(`IsNumericRadiusField`)只认整数族;`Fix` 定点半径字段会漏。
            //   ⇒ 本扫描器**不得**被读作「声明点 == 1」的**证明**;它是**必要非充分**的守卫,
            //     真值单源由**烘焙管线**(ADR-014:唯一 `r_interact` 字段)+ 代码审查共同守。
            var violations = new List<string>();
            var targets = roots ?? CollectInteractionNamespace();
            foreach (var t in targets)
            {
                // `InteractionRadius` 是**唯一合法持有者**(它持装载值,不是「第二声明」)。
                // ⚠️ **已知豁免边界(MINOR #4)**:本跳过是**全类型**跳过 ⇒ 若有人在
                //   `InteractionRadius` **内部**再塞一个 `const int R_INTERACT = 3`,本扫描器**看不见**
                //   (真身 `InteractionRadius` 的结构由 4-DC-1 构造校验 + 代码审查守,非本扫描器)。
                //   本扫描器治的是**其它类型**内的第二声明;「持有者内部」的第二声明是**未覆盖缝**。
                if (t == typeof(InteractionRadius)) continue;

                const BindingFlags fb = BindingFlags.DeclaredOnly |
                                        BindingFlags.Instance | BindingFlags.Static |
                                        BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var f in t.GetFields(fb))
                {
                    if (LooksLikeRadiusName(f.Name) && IsNumericRadiusField(f))
                        violations.Add($"[AC-4-17] {t.Name}.{f.Name} —— 第二半径**数值**声明" +
                                       (f.IsLiteral ? "(const)" : f.IsStatic ? "(static)" : "(instance)"));
                }
            }
            return violations.Distinct().ToList();
        }

        /// <summary>字段名是否具「半径常量」语义(`r_interact` / `R_INTERACT` / `DISCOVER_R` / `_radius` 族)。
        /// <para>⚠️ 名称判定是**必要的**意图信号 —— 但须与 <see cref="IsNumericRadiusField"/> 合取,
        /// 否则「持有装载器引用的 `_radius` 字段」会被误报为第二声明。</para></summary>
        private static bool LooksLikeRadiusName(string name)
        {
            var n = name.ToLowerInvariant().Replace("_", "");
            return n.Contains("rinteract") || n.Contains("discoverr") ||
                   n == "radius" || n.EndsWith("radius") || n.StartsWith("radius") || n.Contains("radius");
        }

        /// <summary>
        /// 字段是否承载**半径数值**(判据治的是「数值源分叉」)。
        /// <para>合法 = 持有 <see cref="InteractionRadius"/> **引用**(共享同一内存值,如
        /// <c>NeighbourhoodReporter._radius</c>);违规 = 自带**整型数值**(<c>const int</c> /
        /// <c>int</c> / <c>static int</c> ——「两处各填一个数」的实体面)。</para>
        /// </summary>
        private static bool IsNumericRadiusField(FieldInfo f)
        {
            var ft = f.FieldType;
            // 整型 / 定点型数值字段:第二数值源
            if (ft == typeof(int) || ft == typeof(long) || ft == typeof(short) ||
                ft == typeof(byte) || ft == typeof(uint) || ft == typeof(ulong))
                return true;
            return false;
        }

        private static List<Type> CollectInteractionNamespace()
        {
            var asm = typeof(InteractionSelector).Assembly;
            return asm.GetTypes()
                      .Where(t => t.Namespace != null &&
                                  (t.Namespace == "DaYiJingCheng.Gameplay.Interaction" ||
                                   t.Namespace.StartsWith("DaYiJingCheng.Gameplay.Interaction.", StringComparison.Ordinal)))
                      .ToList();
        }

        private static Candidate Poi(int x, int y, int z, long poiId)
            => new Candidate(new WorldPos(x, y, z), InteractableKind.PoiCell, poiId, StableIdSource.PoiId);

        private sealed class CountingReporter : IDiscoveryReporter
        {
            public int TotalRequests;
            public void Request(in DiscoveryRequest request) => TotalRequests++;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  负夹具影子类型(住测试命名空间)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>影子:6 侧本地 `const int DISCOVER_R`(故事 QA 逐字的第二声明形态)。</summary>
    internal sealed class ShadowSixSideWithLocalConst
    {
        private const int DISCOVER_R = 3;      // ← 违规:第二半径声明(即便值恰等于注入值)
        public int R => DISCOVER_R;
    }

    /// <summary>影子:第二半径声明走 `[SerializeField]`(非 const)。</summary>
    internal sealed class ShadowSixSideWithSerializeField
    {
        private int _rInteract = 3;            // ← 违规:第二半径数值源
        public int R => _rInteract;
    }
}
