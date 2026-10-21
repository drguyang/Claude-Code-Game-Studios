// casebook Story 002 测试 —— 病例列表排序纯函数(F-39.1)
//
// ── GDD AC 覆盖(编号 = design/gdd/casebook.md 的权威 AC 表)────────────────
// AC-39-02(BLOCKING):`SortKey(c) = (patient_id, Tick(CaseOpened), Seq(CaseOpened))` 三元升序;
//                     同病人多病例按开案 tick 分先后;同 tick 按 `Seq` 分先后 —— 全序无并列残留。
// AC-39-03(BLOCKING):同一病例流的两份前缀各算 `OrderedList`,旧项不重排;且**反射面零展示序字段**。
// AC 第 1 条:纯函数、零落盘(同入同出;输入乱序不改结果)。
// AC 第 5 条:串接组(同 patient_id)输出「线订成叠」分组标记,组内顺序同由三元组定。
// AC 第 6 条:空流 ⇒ 空列表、不报错(**与「未结 N 计数」无关** —— 计数是 casebook-004 的禁项,
//             本文件**刻意不写**任何计数断言)。
//
// ── ⚠️ 两条如实登记(禁借绿)─────────────────────────────────────────────
// ① **`AC-39-03` 的「字面前缀」有前提**:`OrderedList(A)` 是 `OrderedList(B)` 的**连续前缀**,
//    只在**追加项的三元组不小于既有全部键**时成立。该前提在真实病例流里由 ①病人 id 经
//    `IIdAuthority` 单调发号(ADR-006 机制 A)+ ②已存在病人的新案开案 tick 更大 共同保证;
//    但**反例存在**(后追加一条 `patient_id` 更小的病例 ⇒ 它会插到列表最前)。
//    本文件两条测试分开钉:**正例** `test_orderedList_streamPrefix_isPrefixOfFullList` 与
//    **反例(前提登记,非 AC 通过)** `test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix`。
//    ⇒ 报告里按「AC-39-03 正例通过 / 字面前缀为条件成立」如实上报,**不得**记成无条件全绿。
// ② **端到端「加载后列表」不在本件 AC 面**:`casebook-001`(加载路径)未实现,本件以「事件列表参数」
//    形态解耦 ⇒ 依赖是**名义的**。本文件**不**声称验证过加载路径。
//
// ── 载体纪律 ───────────────────────────────────────────────────────────
// 被测对象 = `DaYiJingCheng.Gameplay.UI.Casebook` 的**生产类型**(非测试内复刻);无随机(Random)、
// 无时间依赖、无文件 I/O;夹具全部**固定构造**。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DaYiJingCheng.Gameplay.UI.Casebook;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Casebook
{
    /// <summary>casebook Story 002 —— 病例列表排序纯函数(F-39.1)。</summary>
    [TestFixture]
    public sealed class CasebookSortTest
    {
        // ══════════════ 夹具(固定构造,禁运行时随机)══════════════

        private static SimEvent Opened(int patient, long tick, long seq) =>
            new SimEvent(tick, new PatientId(patient), seq, EventKind.CaseOpened, default(PayloadRef));

        private static SimEvent Other(EventKind kind, int patient, long tick, long seq) =>
            new SimEvent(tick, new PatientId(patient), seq, kind, default(PayloadRef));

        /// <summary>200 例固定夹具。三元组唯一性由 <c>seq = i</c> 单独保证(seq 全域唯一)。</summary>
        private static SimEvent[] Fixed200()
        {
            var list = new List<SimEvent>(200);
            for (int i = 0; i < 200; i++)
                list.Add(Opened((i * 37) % 17, (i * 13) % 23, i));   // patient 0..16 · tick 0..22
            return list.ToArray();
        }

        /// <summary>确定性置换(非随机):步长 97 与 200 互素 ⇒ <c>(i*97+11)%200</c> 是双射。</summary>
        private static SimEvent[] Permuted(SimEvent[] source)
        {
            var dst = new SimEvent[source.Length];
            for (int i = 0; i < source.Length; i++) dst[(i * 97 + 11) % source.Length] = source[i];
            return dst;
        }

        private static List<CaseSortKey> Keys(IReadOnlyList<CaseEntry> entries) =>
            entries.Select(e => e.SortKey).ToList();

        // ══════════════ AC 第 6 条:空流 ══════════════

        [Test]
        public void test_orderedList_emptyStream_returnsEmptyListWithoutThrowing()
        {
            // Arrange
            SimEvent[] empty = Array.Empty<SimEvent>();

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(empty);
            CaseSheet sheet = CaseListOrdering.OrderSheet(empty);

            // Assert
            Assert.That(ordered, Is.Empty, "空流 ⇒ 空列表(AC 第 6 条),不报错");
            Assert.That(sheet.Cases, Is.Empty);
            Assert.That(sheet.Groups, Is.Empty, "空流 ⇒ 零组标记");
            Assert.That(CaseListOrdering.OrderedList(null), Is.Empty, "null 视同空流(防御,不抛)");
        }

        // ══════════════ AC-39-02:三元升序 / 全序 ══════════════

        [Test]
        public void test_orderedList_mixedPatients_ordersByPatientThenTickThenSeq()
        {
            // Arrange:输入序 = 反序,确保输出不是「照抄输入」
            var events = new List<SimEvent>
            {
                Opened(2, 10, 0), Opened(1, 30, 1), Opened(1, 30, 0), Opened(1, 20, 0), Opened(2, 5, 0),
            };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert:patient → tick → seq 三级升序
            var keys = Keys(ordered);
            CollectionAssert.AreEqual(
                new[] { "(1,20,0)", "(1,30,0)", "(1,30,1)", "(2,5,0)", "(2,10,0)" },
                keys.Select(k => k.ToString()).ToList(),
                "全序键 = (patient_id, Tick(CaseOpened), Seq(CaseOpened)) 升序");
        }

        [Test]
        public void test_orderedList_samePatientMultipleCases_orderedByOpenedTick()
        {
            // Arrange:同病人两例,开案 tick 颠倒输入
            var events = new List<SimEvent> { Opened(7, 500, 0), Opened(7, 100, 3) };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert
            Assert.That(ordered[0].OpenedTick, Is.EqualTo(100), "同病人按开案 tick 分先后");
            Assert.That(ordered[1].OpenedTick, Is.EqualTo(500));
        }

        [Test]
        public void test_orderedList_samePatientSameTick_orderedBySeq()
        {
            // Arrange:同病人同 tick,靠 Seq 定序(两个 Seq 输入顺序相反)
            var events = new List<SimEvent> { Opened(4, 42, 9), Opened(4, 42, 2) };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert
            Assert.That(ordered[0].OpenedSeq, Is.EqualTo(2), "同 tick 开案按 Seq 分先后");
            Assert.That(ordered[1].OpenedSeq, Is.EqualTo(9));
        }

        [Test]
        public void test_orderedList_allCasesSameTick_ordersByPatientThenSeq()
        {
            // Arrange:全集合同 tick(edge)
            var events = new List<SimEvent> { Opened(3, 77, 1), Opened(1, 77, 5), Opened(1, 77, 0) };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert
            CollectionAssert.AreEqual(
                new[] { "(1,77,0)", "(1,77,5)", "(3,77,1)" },
                Keys(ordered).Select(k => k.ToString()).ToList());
        }

        [Test]
        public void test_orderedList_negativePatientId_sortsBeforeNonNegative()
        {
            // Arrange:负 patient_id(edge;哨兵 None = -1 与合法负值都按整数序排)
            var events = new List<SimEvent> { Opened(2, 10, 0), Opened(-1, 10, 0), Opened(-7, 10, 0) };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert
            CollectionAssert.AreEqual(new[] { -7, -1, 2 }, ordered.Select(e => e.PatientId).ToList());
        }

        [Test]
        public void test_orderedList_200Cases_adjacentPairsStrictlyIncreasing()
        {
            // Arrange
            SimEvent[] events = Fixed200();

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert
            Assert.That(ordered.Length, Is.EqualTo(200), "CaseOpened 全部入列");
            for (int i = 0; i + 1 < ordered.Length; i++)
            {
                Assert.That(CaseListOrdering.CompareEntries(ordered[i], ordered[i + 1]), Is.LessThan(0),
                    $"第 {i}/{i + 1} 项必须严格小于(全序无并列残留,AC-39-02)");
                Assert.That(ordered[i].SortKey, Is.Not.EqualTo(ordered[i + 1].SortKey),
                    "相邻键不得相等 —— 相等即并列,不同平台 Sort 稳定化差异会显形");
            }
        }

        [Test]
        public void test_orderedList_200Cases_permutedInputYieldsIdenticalOutput()
        {
            // Arrange
            SimEvent[] sorted = Fixed200();
            SimEvent[] shuffled = Permuted(sorted);

            // Act
            CaseEntry[] first = CaseListOrdering.OrderedList(sorted);
            CaseEntry[] second = CaseListOrdering.OrderedList(shuffled);
            CaseEntry[] third = CaseListOrdering.OrderedList(sorted);

            // Assert:输入乱序 / 重复调用均逐位相同(纯函数)
            Assert.That(shuffled.Length, Is.EqualTo(sorted.Length), "置换必须保持元素数(否则下面的等值断言无意义)");
            CollectionAssert.AreEqual(Keys(first), Keys(second), "乱序输入必须得到同一全序");
            CollectionAssert.AreEqual(Keys(first), Keys(third), "同入同出(纯函数,零缓存)");
        }

        [Test]
        public void test_orderedList_nonCaseOpenedEvents_areSkipped()
        {
            // Arrange:病例流里混入非 CaseOpened 事件
            var events = new List<SimEvent>
            {
                Opened(1, 10, 0),
                Other(EventKind.CaseClosed, 1, 20, 0),
                Other(EventKind.JudgmentRecorded, 1, 21, 0),
                Opened(2, 30, 0),
            };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert:只有开案事件成为列表项
            Assert.That(ordered.Length, Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 1, 2 }, ordered.Select(e => e.PatientId).ToList());
        }

        // ══════════════ 回填判据 #1:int64 近极值 ══════════════

        [Test]
        public void test_orderedList_tickAtInt64Extremes_ordersWithoutOverflow()
        {
            // Arrange:同病人、tick 取 long.MinValue / long.MaxValue,seq 也取极值
            var events = new List<SimEvent>
            {
                Opened(1, long.MaxValue, long.MaxValue),
                Opened(1, long.MinValue, 0),
                Opened(1, long.MaxValue, long.MinValue),
                Opened(1, long.MinValue, long.MaxValue),
            };

            // Act
            CaseEntry[] ordered = CaseListOrdering.OrderedList(events);

            // Assert:MinValue 段在前;段内按 seq 升序
            CollectionAssert.AreEqual(
                new[] { "(1,-9223372036854775808,0)", "(1,-9223372036854775808,9223372036854775807)",
                        "(1,9223372036854775807,-9223372036854775808)", "(1,9223372036854775807,9223372036854775807)" },
                Keys(ordered).Select(k => k.ToString()).ToList(),
                "tick/seq 近极值时顺序仍正确 —— 直接钉住「禁 a-b 直比较」");
        }

        [Test]
        public void test_sortKey_compareAtInt64Extremes_isAntisymmetric()
        {
            // Arrange
            var low = new CaseSortKey(1, long.MinValue, long.MinValue);
            var high = new CaseSortKey(1, long.MaxValue, long.MaxValue);

            // Act
            int forward = low.CompareTo(high);
            int backward = high.CompareTo(low);

            // Assert:反号且非零 —— 减法式比较在极值处回绕(MinValue - MaxValue = +1),
            // 会**静默**给出同号,顺序随之反转。
            Assert.That(forward, Is.LessThan(0));
            Assert.That(backward, Is.GreaterThan(0));
            Assert.That(Math.Sign(forward), Is.EqualTo(-Math.Sign(backward)));
        }

        // ══════════════ AC-39-03:流前缀 ══════════════

        [Test]
        public void test_orderedList_streamPrefix_isPrefixOfFullList()
        {
            // Arrange:追加项的三元组均**不小于**既有全部键(真实流的发号前提:新病人 id 更大 /
            // 已存在病人的新案开案 tick 更大)—— 这是 AC-39-03 成立的场景。
            var stream = new List<SimEvent>
            {
                Opened(1, 10, 0), Opened(1, 20, 0), Opened(2, 5, 0), Opened(2, 30, 1),
            };

            // Act
            CaseEntry[] full = CaseListOrdering.OrderedList(stream);

            // Assert:每个前缀的计算结果都是全量结果的前缀(补算 / 重放不炸列)
            for (int k = 0; k <= stream.Count; k++)
            {
                CaseEntry[] partial = CaseListOrdering.OrderedList(stream.Take(k).ToList());
                Assert.That(partial.Length, Is.LessThanOrEqualTo(full.Length));
                for (int i = 0; i < partial.Length; i++)
                {
                    Assert.That(partial[i].SortKey, Is.EqualTo(full[i].SortKey),
                        $"前缀长度 {k} 的第 {i} 项必须与全量同位置项一致(前缀一致性)");
                }
            }
        }

        [Test]
        public void test_orderedList_appendedSmallerKey_relativeOrderKeptButNotPrefix()
        {
            // Arrange:A ⊂ B,B 追加了一条 **patient_id 更小**的病例(字面前缀的**反例**)
            var a = new List<SimEvent> { Opened(5, 10, 0), Opened(9, 11, 0) };
            var b = new List<SimEvent>(a) { Opened(1, 20, 0) };

            // Act
            CaseEntry[] orderedA = CaseListOrdering.OrderedList(a);
            CaseEntry[] orderedB = CaseListOrdering.OrderedList(b);

            // Assert ①:旧项**相对序**不变(新项只插入,不置换)—— 这一半恒成立。
            var oldInB = orderedB.Where(e => orderedA.Any(x => x.SortKey.Equals(e.SortKey))).ToList();
            CollectionAssert.AreEqual(Keys(orderedA), Keys(oldInB), "旧项相对序不得改变");

            // Assert ②:如实登记 —— 字面「连续前缀」在此**不成立**(新项插到最前)。
            // 本断言是**前提登记**,不是 AC 通过;报告里按「条件成立」上报。
            Assert.That(orderedB[0].PatientId, Is.EqualTo(1));
            Assert.That(orderedA[0].SortKey, Is.Not.EqualTo(orderedB[0].SortKey),
                "追加更小键 ⇒ 列表重排,字面「前缀」不成立(依赖 IIdAuthority 单调发号前提)");
        }

        // ══════════════ AC 第 5 条:线订成叠的分组标记 ══════════════

        [Test]
        public void test_orderSheet_interleavedOpens_groupedIntoContiguousStacks()
        {
            // Arrange:输入侧交错开案(3 例病人 3、1 例病人 1、2 例病人 2,彼此穿插)
            var events = new List<SimEvent>
            {
                Opened(3, 10, 0), Opened(1, 20, 0), Opened(3, 30, 0),
                Opened(2, 40, 0), Opened(3, 50, 0), Opened(2, 60, 0),
            };

            // Act
            CaseSheet sheet = CaseListOrdering.OrderSheet(events);

            // Assert ①:三叠,按段首三元组升序
            Assert.That(sheet.Groups.Select(g => g.PatientId).ToList(), Is.EqualTo(new[] { 1, 2, 3 }));

            // Assert ②:区间连续、覆盖全表(无一例漏装订、无一例装两次)
            int cursor = 0;
            foreach (CaseGroup g in sheet.Groups)
            {
                Assert.That(g.Start, Is.EqualTo(cursor), "组区间必须连续");
                Assert.That(g.Count, Is.GreaterThan(0));
                for (int i = g.Start; i <= g.EndInclusive; i++)
                    Assert.That(sheet.Cases[i].PatientId, Is.EqualTo(g.PatientId), "组内不得混入他人病例");
                cursor = g.Start + g.Count;
            }
            Assert.That(cursor, Is.EqualTo(sheet.Cases.Count), "组覆盖必须恰好等于全表");

            // Assert ③:组内顺序仍由三元组决定,不重排
            Assert.That(sheet.Cases.Select(c => c.OpenedTick).ToList(),
                Is.EqualTo(new long[] { 20, 40, 60, 10, 30, 50 }));
        }

        // ══════════════ AC-39-03 反射面 ══════════════

        [Test]
        public void test_casebookTypes_haveNoDisplayOrderOrIndexField()
        {
            // Arrange:扫 39 侧(命名空间 = 本件生产类型)的**字段 + 属性**
            // 🔁 2026-10-21 修正(评审 C4):原版只扫 `FieldInfo` 且显式跳过 `<>` 背衬字段,
            //   ⇒ **自动属性**形式的展示序(`public int DisplayOrder { get; }`)整个逃逸。
            //   现改为同时扫 `PropertyInfo`,词表补 `sortkey`,数值类型扩到全部整型宽度
            //   (`byte` / `short` 同样能承载展示序,原版只认 `int` / `long`)。
            Type[] types = CasebookTypes();
            FieldInfo[] fields = types
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.DeclaredOnly))
                .Where(f => !f.Name.StartsWith("<", StringComparison.Ordinal))   // 跳过编译器生成的属性背衬
                .ToArray();

            PropertyInfo[] properties = types
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.Static |
                                                 BindingFlags.DeclaredOnly))
                .ToArray();

            Assert.That(fields, Is.Not.Empty, "字段集为空 ⇒ 字段面扫描空跑(假绿守卫)");
            Assert.That(properties, Is.Not.Empty,
                "属性集为空 ⇒ **属性面**扫描空跑 —— 属性是原版漏掉的那一面,空集必须让本测转红");

            // Act
            string[] banned = { "order", "index", "ordinal", "displayindex", "sortindex", "sortkey" };
            Type[] integerTypes =
            {
                typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
                typeof(int), typeof(uint), typeof(long), typeof(ulong),
            };

            var violations = fields
                .Where(f => integerTypes.Contains(f.FieldType) &&
                            banned.Any(b => f.Name.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(f => $"{f.DeclaringType?.Name}.{f.Name}({f.FieldType.Name})")
                .Concat(properties
                    .Where(pr => integerTypes.Contains(pr.PropertyType) &&
                                 banned.Any(b => pr.Name.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(pr => $"{pr.DeclaringType?.Name}.{pr.Name}({pr.PropertyType.Name}, 属性)"))
                .ToArray();

            // Assert:零「display order / index」类整数成员(顺序是读时投影,不落类型)
            CollectionAssert.IsEmpty(violations,
                "39 侧不得有展示序成员(AC-39-03,字段与属性两面):" + string.Join(", ", violations));
        }

        [Test]
        public void test_casebookTypes_haveNoMutableState()
        {
            // Arrange:字段 + 属性两面(🔁 2026-10-21:原版只扫字段 ⇒ 带 setter 的属性逃逸)
            Type[] types = CasebookTypes();
            FieldInfo[] fields = types
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                             BindingFlags.Instance | BindingFlags.Static |
                                             BindingFlags.DeclaredOnly))
                .Where(f => !f.Name.StartsWith("<", StringComparison.Ordinal))
                .ToArray();

            PropertyInfo[] properties = types
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.Static |
                                                 BindingFlags.DeclaredOnly))
                .ToArray();

            Assert.That(fields, Is.Not.Empty, "字段集为空 ⇒ 本扫描空跑(假绿守卫)");
            Assert.That(properties, Is.Not.Empty, "属性集为空 ⇒ 属性面扫描空跑(假绿守卫)");

            // Act:实例 / 静态字段一律 readonly 或 const;属性一律无 setter
            var violations = fields
                .Where(f => !f.IsInitOnly && !f.IsLiteral)
                .Select(f => $"{f.DeclaringType?.Name}.{f.Name}")
                .Concat(properties
                    .Where(pr => pr.SetMethod != null)
                    .Select(pr => $"{pr.DeclaringType?.Name}.{pr.Name}(带 setter 的属性)"))
                .ToArray();

            // Assert:零可变字段(排序不得引入可变状态 / 缓存第二真源)
            CollectionAssert.IsEmpty(violations,
                "39 排序侧不得有可变字段:" + string.Join(", ", violations));
        }

        /// <summary>取 39 侧生产类型集(空集 = 装配未加载 ⇒ 测试无意义,由调用方断言非空)。</summary>
        private static Type[] CasebookTypes()
        {
            try
            {
                return typeof(CaseListOrdering).Assembly.GetTypes()
                    .Where(t => t.Namespace == "DaYiJingCheng.Gameplay.UI.Casebook")
                    .ToArray();
            }
            catch (ReflectionTypeLoadException)
            {
                return Array.Empty<Type>();
            }
        }
    }
}
