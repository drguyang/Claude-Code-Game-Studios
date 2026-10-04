// interaction-system Story 003 —— 候选 DTO 反射闭包(AC-4-03)
//
// 登记落点: tests/unit/interaction/candidate_dto_reflection_test.cs
// 真身落点: unity/Assets/Tests/EditMode/Interaction/candidate_dto_reflection_test.cs
//
// 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(四源)/ 数据契约 4-DC-2
//   ADR-016 §三(感知输入 = 粗粒度整数格;禁读表现态位置 —— 判据 = 反射字段类型,**不是 grep**)
//   ADR-015 §三(单一整数格 WorldPos —— 允许类型)
//
// ⚠️ AC-4-03 的**首轮改**:原稿「4 的候选集构造路径」是**无名范畴** ⇒ 无从枚举被测对象。
//   本文件断言的对象 = GDD 点名的**九个输入形状** + `Candidate` 本体。
//
// ⚠️ 承 story-001 的「两台机器」教训(评审 BLOCKING #3):本文件**复用** story-001 的反射
//   机器(`BoundaryDisciplineTest` 的扫描器)**同一台**,负夹具经**可注入根**换成本命名空间的
//   影子类型 —— 不另写第二台扫描器,否则「夹具红」不蕴含「真断言红」。
//
// ⚠️ 白名单**逐个断言**(防扫描器空转):扫描器若因命名空间剪枝把整个 `Sim.Contracts` 剪掉,
//   `WorldPos` 就不再被检查 —— 那时「违规集 = ∅」是**假的绿**。故白名单成员须**正向**证明
//   扫描器确实见过它们(见 test_ac403_whitelistMembersAreActuallyVisited)。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Interaction
{
    public class CandidateDtoReflectionTest
    {
        // ═══════════════════════════════════════════════════════════
        //  AC-4-03 —— 九个输入形状 + Candidate 的类型闭包零引擎类型
        // ═══════════════════════════════════════════════════════════

        /// <summary>GDD 点名的九个输入形状 + `Candidate` 本体(AC-4-03 的被测对象全清单)。</summary>
        private static readonly Type[] Ac403Subjects =
        {
            typeof(Candidate),
            typeof(DropDto),          // Drop
            typeof(ForageSpotDto),    // ForageSpot
            typeof(PatientDto),       // Patient
            typeof(PoiCellDto),       // PoiCell
            typeof(BuildSlotDto),     // BuildSlot
            typeof(UtensilDto),       // Utensil
            typeof(ClinicPanelDto),   // ClinicPanel
            typeof(DoorDto),          // Door
            typeof(SwitchDto),        // Switch
        };

        /// <summary>
        /// 禁入类型名(AC-4-03)。用**类型全名子串**匹配,承 story-001 扫描器口径。
        /// <para>⚠️ 只列 `UnityEngine.*` 系 —— 因为本判据治的是「读表现态位置」,
        ///   不是「引结算侧类型」(后者归 AC-4-01/05,story 001 已守)。</para>
        /// </summary>
        private static readonly string[] ForbiddenInputTypes =
        {
            "UnityEngine.Vector3",
            "UnityEngine.Vector2",
            "UnityEngine.Vector4",
            "UnityEngine.Quaternion",
            "UnityEngine.Transform",
            "UnityEngine.Matrix4x4",
        };

        [Test]
        public void test_ac403_namedDtosHaveZeroEngineTypesInFieldClosure()
        {
            var violations = ScanFieldClosure(Ac403Subjects, ForbiddenInputTypes);
            Assert.IsEmpty(violations,
                "AC-4-03:九个输入形状 + Candidate 的字段闭包内零 `UnityEngine.*` 位置类型 —— " +
                "违反项:" + string.Join(" | ", violations));
        }

        /// <summary>
        /// ⚠️ 白名单成员**正向**断言(反空转):证明扫描器真的一路走到了 `WorldPos` 的内部
        /// 字段(`X`/`Y`/`Z` 皆 int),而不是被命名空间剪枝提前剪掉、报了个空的绿。
        /// <para>做法 = 用同一台扫描器、把禁入集换成**故意点名** `System.Int32`,
        /// 若扫描器真到过 `WorldPos` 的 int 字段,违规集必非空。</para>
        /// </summary>
        [Test]
        public void test_ac403_whitelistMembersAreActuallyVisited()
        {
            // 反向探针:把 int(WorldPos 的字段类型、合法的白名单成员)当禁入
            var probe = ScanFieldClosure(Ac403Subjects, new[] { "System.Int32" });
            Assert.IsNotEmpty(probe,
                "AC-4-03 反空转:扫描器须真的**走到过** `WorldPos.X/Y/Z`(int 字段)—— " +
                "若此处为空,说明扫描器被剪枝短路,`违规集 = ∅` 是假绿");
            // ⚠️ 违规串形如 `[AC-4-03] Candidate.Cell.X 的类型闭包含禁入类型「System.Int32」(...)`
            //   —— `path` 用的是**字段名**(`Cell.X`)不是类型名 ⇒ 断言须看「探针确已触底到 int 字段」,
            //   即违规串含 `System.Int32`(若扫描器被顶层剪枝短路,根本走不到 int)。
            Assert.IsTrue(probe.Any(v => v.Contains("System.Int32")),
                "AC-4-03 反空转:探针违规须点名 `System.Int32`(证扫描器已展开到 `WorldPos` 的 int 字段,而非只扫顶层)");
        }

        [Test]
        public void test_ac403_negativeFixture_shadowDtoWithVector3IsPointedlyRed()
        {
            // 负夹具(故事 QA 逐字):「给 Drop 加 `Vector3 visualPos` 影子类型 ⇒ 点名红」。
            // 用**同一台扫描器**、把根换成影子类型 —— 影子 DTO 住本测试命名空间。
            var violations = ScanFieldClosure(new[] { typeof(ShadowDropDtoWithEngineType) },
                                              ForbiddenInputTypes);
            Assert.IsNotEmpty(violations,
                "AC-4-03 负夹具:带 `UnityEngine.Vector3` 字段的影子 DTO 必须被同一台扫描器抓到 " +
                "(证明本判据非空转 —— 删掉展开循环则此处与正测**同时**变红)");
            Assert.IsTrue(violations.Any(v => v.Contains("Vector3")),
                "AC-4-03 负夹具:违规须点名 `Vector3`(不是别的类型)");
        }

        [Test]
        public void test_ac403_negativeFixture_engineTypeViaBaseClassIsCaught()
        {
            // 故事 QA 边缘(逐字):「DTO 经**基类继承**来的引擎类型字段(须被闭包抓到 ——
            //   承 `PresentationDtoGuard`『仅扫顶层会漏』同款)」。
            // 评审 F-4 指出:基类展开(`:184-185`)**实现有、零覆盖** —— 本条补上触发。
            var violations = ScanFieldClosure(new[] { typeof(ShadowDerivedDtoFromEngineBase) },
                                              ForbiddenInputTypes);
            Assert.IsNotEmpty(violations,
                "AC-4-03 边缘:经**基类**继承的引擎类型字段必须被闭包抓到(仅扫顶层会漏)");
            Assert.IsTrue(violations.Any(v => v.Contains("Vector3")),
                "AC-4-03 边缘:违规须点名基类里的 `Vector3`(证基类展开真生效)");
        }

        [Test]
        public void test_ac403_noProductionDtoCarriesBoxedObjectField()
        {
            // 故事 QA 口径(逐字):「装箱 `object` 字段藏 `Vector3`(静态反射看不见值 ⇒
            //   测试夹具以『类型即违规』断言:`object` 字段要求显式白名单标注,否则红)」。
            //
            // ⚠️ **必须扫生产 DTO**(评审 F-3:初稿只扫影子类型 = 循环论证,未接生产面):
            //   本测试把 `System.Object` 当禁入,直接跑**九个生产 DTO + Candidate** 的字段闭包 ——
            //   若日后任一生产 DTO 混入未标注白名单的 `object` 字段,本条即红。
            var violations = ScanFieldClosure(Ac403Subjects, new[] { "System.Object" });
            Assert.IsEmpty(violations,
                "AC-4-03:生产 DTO 不得有 `object` 装箱字段(装箱藏引擎类型的入口;" +
                "若有,须显式白名单标注并在此处登记)。违反项:" + string.Join(" | ", violations));
        }

        [Test]
        public void test_ac403_negativeFixture_boxedObjectInShadowDtoIsPointedlyRed()
        {
            // 上条(生产面)的**反空转证明**:同一台扫描器、同一禁入集(`System.Object`),
            //   换成带 `object` 字段的影子类型 ⇒ **必红**。⇒ 上条的「空违规集」不是扫描器空转。
            var violations = ScanFieldClosure(new[] { typeof(ShadowDtoWithBoxedField) },
                                              new[] { "System.Object" });
            Assert.IsNotEmpty(violations,
                "AC-4-03 负夹具:带 `object` 字段的影子类型必须被同一台扫描器抓到" +
                "(证上条『生产 DTO 零 object』非空转)");
            Assert.IsTrue(violations.Any(v => v.Contains("System.Object")),
                "AC-4-03 负夹具:违规须点名 `System.Object`");
        }

        // ═══════════════════════════════════════════════════════════
        //  扫描机器(与 story-001 同口径的**字段闭包**展开)
        //  ⚠️ 只扫字段 —— AC-4-03 的判据是「字段类型」(方法参数归 AC-4-03 的
        //    故事 QA「含私有、泛型实参递归」口径,此处按字段闭包实现)。
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 递归展开一组根类型的**全部实例字段**(含私有、泛型实参、数组元素、嵌套、基类),
        /// 收集类型全名命中禁入集的违规。
        /// </summary>
        private static List<string> ScanFieldClosure(IEnumerable<Type> roots, string[] forbiddenNames)
        {
            var seen = new HashSet<Type>();
            var violations = new List<string>();
            foreach (var r in roots) Expand(r, forbiddenNames, seen, violations, r.Name, 0);
            return violations.Distinct().ToList();
        }

        /// <remarks>
        /// ⚠️ <b>`depth > 64` 是静默截断</b>(不抛、不记)—— 本项目 DTO 是扁平 readonly struct,
        /// 深度远小于此;登记为已知边界,一旦日后出现深嵌套(> 64 层)的 DTO,此处会**静默漏扫**
        /// 而非报错。见 <c>test_ac403_noProductionDtoCarriesBoxedObjectField</c> 的负夹具证明机器非空转。
        /// <para>⚠️ <b>基类展开发生在 `ShouldPrune` 之后</b>:若基类本身住
        ///   `System.*` / `UnityEngine.*`(BCL / 引擎基类),它会在展开其字段前被剪掉。
        ///   本判据的威胁面 = **本项目 DTO 继承自本项目基类**(此时命名空间不命中剪枝,基类字段照扫)——
        ///   `ShadowDerivedDtoFromEngineBase` 即此形态,已由 F-4 覆盖。
        ///   继承 BCL 基类且该基类带引擎字段的形态在本项目**不存在**(DTO 全为扁平 struct),
        ///   登记为已知不覆盖,不写夹具(无真实触发面)。</para>
        /// </remarks>
        private static void Expand(Type t, string[] forbidden, HashSet<Type> seen,
                                   List<string> violations, string path, int depth)
        {
            if (t == null || depth > 64) return;

            // 数组 / 泛型实参
            if (t.IsArray) { Expand(t.GetElementType(), forbidden, seen, violations, path + "[]", depth + 1); return; }
            if (t.IsGenericType)
                foreach (var ga in t.GetGenericArguments())
                    Expand(ga, forbidden, seen, violations, path + "<" + ga.Name + ">", depth + 1);

            // 引擎 / BCL 类型:**点名检查**但不再展开其成员(与 story-001 剪枝同口径 ——
            // 引擎自有成员不可能携带本项目语义,但其**类型本身**仍被点名)。
            foreach (var bad in forbidden)
                if (t.FullName != null && t.FullName.Contains(bad))
                    violations.Add($"[AC-4-03] {path} 的类型闭包含禁入类型「{bad}」({t.FullName})");

            if (!seen.Add(t)) return;
            if (ShouldPrune(t)) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var f in t.GetFields(flags))
                Expand(f.FieldType, forbidden, seen, violations, path + "." + f.Name, depth + 1);

            if (t.BaseType != null && t.BaseType != typeof(object) && t.BaseType != typeof(ValueType))
                Expand(t.BaseType, forbidden, seen, violations, path + " : base", depth + 1);
        }

        /// <summary>命名空间剪枝(与 story-001 `ShouldExpandMembers` 同口径)。</summary>
        private static bool ShouldPrune(Type t)
        {
            var ns = t.Namespace ?? string.Empty;
            return ns.StartsWith("System", StringComparison.Ordinal) ||
                   ns.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                   ns.StartsWith("Unity.", StringComparison.Ordinal) ||
                   ns.StartsWith("Microsoft", StringComparison.Ordinal);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  负夹具影子类型(住测试命名空间 —— 不污染生产)
    //  ⚠️ 它们**故意**带引擎类型字段,是本判据唯一能红的路径。
    // ═══════════════════════════════════════════════════════════════

    /// <summary>影子:给 Drop 形状加 `Vector3 visualPos`(故事 QA 逐字的负夹具形态)。</summary>
    internal readonly struct ShadowDropDtoWithEngineType
    {
        public readonly WorldPos Anchor;
        public readonly long InstanceId;
        public readonly UnityEngine.Vector3 VisualPos;   // ← 违规:表现态位置

        public ShadowDropDtoWithEngineType(WorldPos anchor, long instanceId, UnityEngine.Vector3 visualPos)
        { Anchor = anchor; InstanceId = instanceId; VisualPos = visualPos; }
    }

    /// <summary>影子:装箱 `object` 字段(藏引擎类型的入口)。</summary>
    internal readonly struct ShadowDtoWithBoxedField
    {
        public readonly object Payload;   // ← 未标白名单 ⇒ 红

        public ShadowDtoWithBoxedField(object payload) { Payload = payload; }
    }

    /// <summary>
    /// 影子基类:**自身无违规字段**,但派生类从它继承的字段里有一个引擎类型。
    /// <para>⚠️ 基类抛在本测试命名空间内(非 `System.*`/`UnityEngine.*`)—— 若抛在
    ///   `ShouldPrune` 命中面,基类会在展开前被剪掉,本夹具就变假绿(见 `Expand` 注释)。</para>
    /// </summary>
    internal abstract class ShadowDtoBaseWithEngineType
    {
        public readonly UnityEngine.Vector3 InheritedVisualPos;   // ← 违规:藏在基类里

        protected ShadowDtoBaseWithEngineType(UnityEngine.Vector3 visualPos)
        { InheritedVisualPos = visualPos; }
    }

    /// <summary>
    /// 影子派生 DTO:顶层字段全合法,**唯一违规来自基类**。
    /// ⇒ 抓不到 = 基类展开(`Expand` 的 `BaseType` 分支)失效(评审 F-4 的触发面)。
    /// </summary>
    internal sealed class ShadowDerivedDtoFromEngineBase : ShadowDtoBaseWithEngineType
    {
        public readonly WorldPos Cell;
        public readonly long StructureId;

        public ShadowDerivedDtoFromEngineBase(WorldPos cell, long structureId,
                                              UnityEngine.Vector3 visualPos)
            : base(visualPos)
        { Cell = cell; StructureId = structureId; }
    }
}
