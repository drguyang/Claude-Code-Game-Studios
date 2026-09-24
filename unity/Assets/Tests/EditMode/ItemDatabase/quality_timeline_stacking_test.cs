// 权威来源:
//   Story 004(production/epics/item-database/story-004-quality-timeline-and-stacking.md)
//     · AC-21a-32 —— StackKey = (item_key, quality):不同 quality 不合并;同 K 同 q 且未达上限合并
//     · AC-21a-33 —— 溢出到新实例,总量守恒(Σqty 全容器守恒)
//     · AC-21a-36 —— Axis_effective = Axis_base + offsets[quality−1](Q16.16 整数域,零浮点中间量)
//     · AC-21a-37 —— 至少一档非零;每个非零档 |offset| ≥ 可感知地板(「全零 ⇒ 通过」不合格)
//     · AC-21a-38 —— 仅 quality_axis 那条轴平移,余三轴逐位不变;不读写 polarity(结构事实)
//     · AC-21a-38b —— Axis_base + min(offset) > 0(域钳制;D-21-34 张力:只断言 > 0,不收紧)
//     · AC-21a-64 —— Σ(weight × InstanceWeight) 最坏上界不溢出 int64(先证不溢出的上界判据)
//   GDD:design/gdd/item-database.md §Formulas F4(:657-688)/ F5(:688-748)· Core Rules 规则六(:185-191)
//   ADR-006(主):定点域边界数据契约 —— weight/stack_max 是 int 计数(D-21-17)· 舍入整数域内完成
//   ADR-005(次):确定性模拟 —— 求解器纯函数、同参数集逐位可重放
//   ADR-025 §①:纯 sim 逻辑 → 装配 `Sim`(引用集 {BCL, Sim.Contracts})
//
// ⚠️ 落点:故事头登记的账本路径 = tests/unit/item_database/quality_timeline_stacking_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ 真身 = 本文件(与 Story 001/002/003 同一先例)。
//
// ⚠️ 范围(Out of Scope 硬边界):构建期硬失败执法体(AC-50/50b/60/61/62/64 的显式 throw 装在
//    Editor.Tools.Gates.* 校验套件)= Story 006(负向夹具 invalid_*.json 同批);本文件只测
//    **运行期可判定的纯函数**(求解器 + 谓词算子)。容器 children 闭包 / 容器守恒(AC-34/58)
//    归 Story 010;堆叠动作进世界流归 20 / Story 010。
//
// ⚠️ 数值纪律:以下常量**全部是测试夹具值,不是游戏平衡值**(PERCEPTIBLE_FLOOR / MAX_QUALITY /
//    各档 offset 归用户数值轮)。夹具取**数学上可判定**的小值,使边界可精确断言。
// ⚠️ D-21-34(open,勿当已改):AC-21a-38b 只断言 `> 0`,**不收紧到 ≥ MIN_USABLE_HALF_LIFE** ——
//    收紧 = 改机制,须走 21a 重开流程(和 = 1 的边界必须过)。
// ⚠️ 零 UnityEngine / 零外部 I/O / 无随机种子(枚举式遍历,非 RNG)。

using System;
using NUnit.Framework;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.ItemDatabase
{
    [TestFixture]
    internal sealed class QualityTimelineStackingTest
    {
        // ══════════════ F4 堆叠与重量(AC-32/33/64)══════════════

        /// <summary>给定 base/state 构造一个合成实例(容器 = children 非空,由调用方显式给出)。</summary>
        private static ItemInstance item(long id, string baseId, int quality, int qty,
            long[] children = null, ProcessingState state = ProcessingState.Raw)
            => new ItemInstance(id, new ItemKey(baseId, state), quality, qty,
                children ?? Array.Empty<long>());

        // ---- AC-21a-32:StackKey = (item_key, quality) ----

        [Test]
        public void test_stackKey_differentQuality_sameKey_neverMerges()
        {
            // 同 K 同 stackMax,quality 不同(1 vs MAX_QUALITY 两极)
            ItemInstance a = item(1L, "willow_bark", 1, 5);
            ItemInstance b = item(2L, "willow_bark", 5, 5);
            Assert.That(StackingSolver.CanStack(a, b, stackMax: 99), Is.False,
                "同 item_key 不同 quality 永不合并(StackKey = (item_key, quality))");
        }

        [Test]
        public void test_stackKey_sameQuality_sameKey_merges_whenNotFull()
        {
            ItemInstance a = item(1L, "willow_bark", 3, 2);
            ItemInstance b = item(2L, "willow_bark", 3, 4);
            Assert.That(StackingSolver.CanStack(a, b, stackMax: 99), Is.True,
                "同 K 同 q 且未达上限 ⇒ 可合并");
        }

        [Test]
        public void test_stackKey_sameQuality_differentKey_neverMerges()
        {
            // 同 quality、不同 base_id ⇒ 不同(key, quality)键
            ItemInstance a = item(1L, "willow_bark", 2, 5);
            ItemInstance b = item(2L, "cyanotis", 2, 5);
            Assert.That(StackingSolver.CanStack(a, b, stackMax: 99), Is.False,
                "不同 item_key(即使同品级)永不合并");
        }

        [Test]
        public void test_stackKey_sameKey_sameQuality_differentState_neverMerges()
        {
            // 复合主键含 processing_state:raw vs dried 是同 base 两个键
            ItemInstance a = item(1L, "willow_bark", 1, 5, state: ProcessingState.Raw);
            ItemInstance b = item(2L, "willow_bark", 1, 5, state: ProcessingState.Dried);
            Assert.That(StackingSolver.CanStack(a, b, stackMax: 99), Is.False,
                "(base_id, state) 之一不同即不同键");
        }

        [Test]
        public void test_stackKey_stackMaxOne_neverMerges()
        {
            // stack_max = 1 ⇒ 不可堆叠(规则六边例)
            ItemInstance a = item(1L, "med_kit", 1, 1);
            ItemInstance b = item(2L, "med_kit", 1, 1);
            Assert.That(StackingSolver.CanStack(a, b, stackMax: 1), Is.False,
                "stack_max = 1 永不合并(不可堆叠物品)");
        }

        [Test]
        public void test_stackKey_containerInstance_neverMerges()
        {
            // 容器实例 qty/quality 恒 1,不参与堆叠(§Schema E)—— children 非空
            ItemInstance container = new ItemInstance(7L, new ItemKey("herb_pouch", ProcessingState.Raw),
                1, 1, new long[] { 10L, 11L });
            ItemInstance loose = item(8L, "herb_pouch", 1, 1);
            Assert.That(StackingSolver.CanStack(container, loose, stackMax: 99), Is.False,
                "容器实例(children 非空)不参与堆叠");
            Assert.That(StackingSolver.CanStack(loose, container, stackMax: 99), Is.False,
                "容器判定对称");
        }

        // ---- AC-21a-33:溢出到新实例,总量守恒 ----

        [Test]
        public void test_splitStack_withinCap_noOverflow_conservesTotal()
        {
            // c=3, Δ=4, M=8 ⇒ c+Δ=7 ≤ M:原堆 7,无溢出
            StackSplitResult r = StackingSolver.SplitStack(3, 4, 8);
            Assert.That(r.CurrentFinalQty, Is.EqualTo(7));
            Assert.That(r.OverflowQty, Is.EqualTo(0));
            Assert.That(r.NewFullStacks, Is.EqualTo(0));
            Assert.That(r.PartialStackQty, Is.EqualTo(0));
            Assert.That(r.CurrentFinalQty + r.OverflowQty, Is.EqualTo(3 + 4),
                "Σqty 守恒:c + Δ = 原堆终值 + 溢出总量");
        }

        [Test]
        public void test_splitStack_exactlyAtCap_noOverflow()
        {
            StackSplitResult r = StackingSolver.SplitStack(1, 5, 6);
            Assert.That(r.CurrentFinalQty, Is.EqualTo(6));   // 恰好 M:不溢出
            Assert.That(r.OverflowQty, Is.EqualTo(0));
        }

        [Test]
        public void test_splitStack_overflow_singlePartialStack()
        {
            // c=3, Δ=5, M=6 ⇒ c+Δ=8 > 6:原堆补满 6,溢出 2(1 个部分堆)
            StackSplitResult r = StackingSolver.SplitStack(3, 5, 6);
            Assert.That(r.OverflowQty, Is.EqualTo(2));
            Assert.That(r.NewFullStacks, Is.EqualTo(0));
            Assert.That(r.PartialStackQty, Is.EqualTo(2));
            Assert.That(r.CurrentFinalQty + r.OverflowQty, Is.EqualTo(8), "守恒");
        }

        [Test]
        public void test_splitStack_overflow_multipleFullStacks_plusPartial()
        {
            // c=1, Δ=13, M=5 ⇒ c+Δ=14:原堆 5,溢出 9 = 1 满堆 + 余 4
            StackSplitResult r = StackingSolver.SplitStack(1, 13, 5);
            Assert.That(r.CurrentFinalQty, Is.EqualTo(5));
            Assert.That(r.OverflowQty, Is.EqualTo(9));
            Assert.That(r.NewFullStacks, Is.EqualTo(1));
            Assert.That(r.PartialStackQty, Is.EqualTo(4));
            Assert.That(r.CurrentFinalQty + r.OverflowQty, Is.EqualTo(14), "守恒");
        }

        [Test]
        public void test_splitStack_overflow_exactDivisible_noPartial()
        {
            // c=1, Δ=11, M=4 ⇒ c+Δ=12:原堆 4,溢出 8 = 2 满堆,无余数堆
            StackSplitResult r = StackingSolver.SplitStack(1, 11, 4);
            Assert.That(r.CurrentFinalQty, Is.EqualTo(4));
            Assert.That(r.OverflowQty, Is.EqualTo(8));
            Assert.That(r.NewFullStacks, Is.EqualTo(2));
            Assert.That(r.PartialStackQty, Is.EqualTo(0), "整除 ⇒ 无部分堆");
            Assert.That(r.CurrentFinalQty + r.OverflowQty, Is.EqualTo(12), "守恒");
        }

        [Test]
        public void test_splitStack_fullStack_add_createsAllNewInstances()
        {
            // c=M 满堆再加 Δ=3,M=5 ⇒ 原堆满,Δ 全走新实例(1 部分堆 3)
            StackSplitResult r = StackingSolver.SplitStack(5, 3, 5);
            Assert.That(r.CurrentFinalQty, Is.EqualTo(5));
            Assert.That(r.OverflowQty, Is.EqualTo(3));
            Assert.That(r.NewFullStacks, Is.EqualTo(0));
            Assert.That(r.PartialStackQty, Is.EqualTo(3));
        }

        [Test]
        public void test_splitStack_hugeAdd_multipleSegments_overflowConserved()
        {
            // Δ 巨大(Δ > 2×M):溢出 = c+Δ−M,c 恒填满;守恒由 long 求和保证
            StackSplitResult r = StackingSolver.SplitStack(2, 10_000, 99);
            Assert.That(r.CurrentFinalQty, Is.EqualTo(99));
            Assert.That(r.OverflowQty, Is.EqualTo(2 + 10_000 - 99));
            Assert.That(r.NewFullStacks * 99 + r.PartialStackQty, Is.EqualTo(r.OverflowQty),
                "溢出量按 M 切分 = 满堆 × M + 余数");
            Assert.That(r.PartialStackQty, Is.InRange(0, 98), "余数堆 < M");
        }

        [Test]
        public void test_splitStack_outOfRange_throws()
        {
            Assert.Throws<InvalidOperationException>(() => StackingSolver.SplitStack(0, 5, 6),
                "c = 0 域外(堆 qty ≥ 1)");
            Assert.Throws<InvalidOperationException>(() => StackingSolver.SplitStack(3, 0, 6),
                "Δ = 0 域外(加入量 ≥ 1)");
            Assert.Throws<InvalidOperationException>(() => StackingSolver.SplitStack(3, 5, 0),
                "M = 0 域外(stack_max ≥ 1)");
        }

        [Test]
        public void test_splitStack_noSilentIntWrap_overflowBeyondIntMax_throws()
        {
            // 溢出总量超 int.MaxValue 显式抛,不留静默回绕(纯函数自保护;真实数据被 AC-64 前置拦下)
            Assert.Throws<InvalidOperationException>(() => StackingSolver.SplitStack(2_147_483_647, 3, 1),
                "溢出 > int.MaxValue 拒绝");
        }

        // ---- AC-21a-64:守恒律 int64 上界判据(先证不溢出)----

        [Test]
        public void test_weightedTotalFitsInt64_typicalBounds_true()
        {
            // 合理上界组合(夹具值,非游戏数值):评估不溢出
            Assert.That(StackingSolver.WeightedTotalFitsInt64(9999, 9999, 10000), Is.True,
                "典型上界组合应不溢出 long");
        }

        [Test]
        public void test_weightedTotalFitsInt64_boundaryAtLimit_true()
        {
            // 恰在 long.MaxValue 除法比较边界:perStack × entries 恰 ≤ MaxValue 过。
            // 取 perStack = int.MaxValue × 2 = 4,294,967,294(< long.MaxValue),entries = int.MaxValue
            // ⇒ perStack × entries ≈ 2^62 < 2^63,划界落在恰可容纳侧(避免 (int) 强转回绕)。
            Assert.That(StackingSolver.WeightedTotalFitsInt64(int.MaxValue, 2, int.MaxValue), Is.True,
                "恰在边界(perStack = int.MaxValue × stackMax 2,entries = int.MaxValue)过");
        }

        [Test]
        public void test_weightedTotalFitsInt64_overLimit_false()
        {
            // 超一档 ⇒ false(构建期硬失败面)
            Assert.That(StackingSolver.WeightedTotalFitsInt64(int.MaxValue, int.MaxValue, 3), Is.False,
                "上界超声明 ⇒ false");
            Assert.That(StackingSolver.WeightedTotalFitsInt64(int.MaxValue, 3, int.MaxValue), Is.False,
                "stackMax 抬高一档(perStack = 3 × int.MaxValue)⇒ 超界 false");
        }

        [Test]
        public void test_weightedTotalFitsInt64_invalidBounds_false()
        {
            Assert.That(StackingSolver.WeightedTotalFitsInt64(0, 10, 10), Is.False,
                "weight 域外(≤ 0)⇒ false");
            Assert.That(StackingSolver.WeightedTotalFitsInt64(10, 0, 10), Is.False,
                "stack_max 域外 ⇒ false");
            Assert.That(StackingSolver.WeightedTotalFitsInt64(10, 10, 0), Is.False,
                "条目数域外 ⇒ false");
        }

        // ══════════════ F5 品级 → 时间轴(AC-36/37/38/38b)══════════════

        // 夹具:四轴 base 全有(AC-38 断言其余三轴逐位不变的前提)+ 全 tick 档位表
        private static DrugProfile fixtureProfile()
        {
            var profile = new DrugProfile();
            profile.DrugPotency = FixParse.Parse("4");
            profile.Onset = FixParse.Parse("2");
            profile.Peak = FixParse.Parse("3");
            profile.HalfLife = FixParse.Parse("5");
            profile.Elimination = FixParse.Parse("6");
            profile.QualityAxis = QualityAxis.HalfLife;   // P0 唯一合法作用轴
            return profile;
        }

        private static Fix fx(string s) => FixParse.Parse(s);

        // ---- AC-21a-36:Axis_effective = Axis_base + offsets[quality−1] ----

        [Test]
        public void test_f5_axisEffective_isBasePlusOffset_rawIntegerAddition()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { fx("1/4"), fx("1/4"), fx("1/2"), fx("-1/4"), fx("0") };
            // quarter: quality=4 ⇒ index 3 → offset = −1/4;half_life base = 5 ⇒ effective = 5 − 1/4 = 19/4
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 4);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(fx("19/4").Raw),
                "Axis_effective(raw) = Axis_base(raw) + offsets[quality−1](raw),纯整数加法");
        }

        [Test]
        public void test_f5_firstQuality_mapsToIndexZero()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { fx("1/2"), fx("1/2"), fx("1/2"), fx("1/2"), fx("1/2") };
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 1);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(fx("5").Raw + fx("1/2").Raw),
                "quality = 1 ⇒ index 0(quality − 1 = 0)");
        }

        [Test]
        public void test_f5_lastQuality_mapsToLastIndex()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4"), fx("-1/2") };
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 5);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(fx("5").Raw + fx("-1/2").Raw),
                "quality = MAX_QUALITY ⇒ index len−1(含负 offset)");
        }

        [Test]
        public void test_f5_outOfRangeQuality_throws_runtimeGuard()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4") };
            Assert.Throws<InvalidOperationException>(() => QualityTimelineSolver.ApplyQualityTimeline(p, 0),
                "quality < 1 越界读防护(长度由 AC-50 保证,此处为运行期兜底)");
            Assert.Throws<InvalidOperationException>(() => QualityTimelineSolver.ApplyQualityTimeline(p, 6),
                "quality > len 越界读防护");
        }

        [Test]
        public void test_f5_negativeOffset_appliesInRawDomain()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { fx("0"), fx("-3/4"), fx("0"), fx("0"), fx("0") };
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 2);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(fx("5").Raw + fx("-3/4").Raw),
                "负 offset(劣药)在定点域直接相加");
        }

        // ---- AC-21a-38:仅 quality_axis 那条轴平移,余三轴逐位不变 ----

        [Test]
        public void test_f5_offAxisTimelines_bitIdenticalToBase()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4") };
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 3);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(fx("5").Raw + fx("1/4").Raw),
                "作用轴(half_life)随档偏移");
            Assert.That(t.Onset!.Value.Raw, Is.EqualTo(p.Onset!.Value.Raw), "onset 逐位不变(AC-38)");
            Assert.That(t.Peak!.Value.Raw, Is.EqualTo(p.Peak!.Value.Raw), "peak 逐位不变(AC-38)");
            Assert.That(t.Elimination!.Value.Raw, Is.EqualTo(p.Elimination!.Value.Raw),
                "elimination 逐位不变(AC-38)");
        }

        [Test]
        public void test_f5_noShiftWhenAxisUnset_returnsProfileFieldsBitIdentical()
        {
            DrugProfile p = fixtureProfile();
            p.QualityAxis = null;                 // P0 合法:字段在但值空(D-21-6)
            p.AxisOffsetByQuality = null;
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 3);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(p.HalfLife!.Value.Raw));
            Assert.That(t.Onset!.Value.Raw, Is.EqualTo(p.Onset!.Value.Raw));
            Assert.That(t.Peak!.Value.Raw, Is.EqualTo(p.Peak!.Value.Raw));
            Assert.That(t.Elimination!.Value.Raw, Is.EqualTo(p.Elimination!.Value.Raw),
                "无 F5 作用 ⇒ 四轴逐位等于 profile 字段(不臆造平移)");
        }

        [Test]
        public void test_f5_nullableTimeline_passesThroughNullForUnsetAxes()
        {
            // P0 合法档案可只有 half_life:onset/peak/elimination 为 null,无 F5 时 null 透传
            DrugProfile p = new DrugProfile();
            p.HalfLife = fx("5");
            p.QualityAxis = QualityAxis.HalfLife;
            p.AxisOffsetByQuality = new[] { fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4") };
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 2);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(fx("5").Raw + fx("1/4").Raw));
            Assert.That(t.Onset.HasValue, Is.False, "null 轴透传 null(不臆造 0)");
            Assert.That(t.Peak.HasValue, Is.False);
            Assert.That(t.Elimination.HasValue, Is.False);
        }

        [Test]
        public void test_f5_missingShiftAxisBase_throws()
        {
            // F5 声明在 half_life,但 half_life base 缺失 = 数据不一致 ⇒ 显式抛
            DrugProfile p = new DrugProfile();
            p.HalfLife = null;
            p.QualityAxis = QualityAxis.HalfLife;
            p.AxisOffsetByQuality = new[] { fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4"), fx("1/4") };
            Assert.Throws<InvalidOperationException>(() => QualityTimelineSolver.ApplyQualityTimeline(p, 1),
                "F5 作用轴缺 base 显式抛(运行期兜底)");
        }

        // ---- AC-21a-37:至少一档非零 + 非零档 |offset| ≥ 可感知地板 ----

        [Test]
        public void test_f5_hasQualityEffect_atLeastOneNonZero_passes()
        {
            Assert.That(QualityTimelineSolver.HasQualityEffect(
                new[] { fx("0"), fx("1/4"), fx("0") }), Is.True, "至少一档非零 ⇒ 有品质效果");
        }

        [Test]
        public void test_f5_hasQualityEffect_allZeroForbiddenNoPass()
        {
            Assert.That(QualityTimelineSolver.HasQualityEffect(
                new[] { fx("0"), fx("0"), fx("0") }), Is.False, "「全零 ⇒ 通过」不合格");
            Assert.That(QualityTimelineSolver.HasQualityEffect(null), Is.False, "无表 = 无品质效果");
            Assert.That(QualityTimelineSolver.HasQualityEffect(Array.Empty<Fix>()), Is.False, "空表 = 无品质效果");
        }

        [Test]
        public void test_f5_offsetsAtOrAboveFloor_pass()
        {
            Fix floor = fx("1/8");
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("0"), fx("1/8"), fx("-1/4") }, floor), Is.True,
                "恰在地板(1/8)与地板以上(1/4)的档过;零档豁免");
        }

        [Test]
        public void test_f5_offsetBelowFloor_fails()
        {
            Fix floor = fx("1/8");
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("1/16"), fx("0"), fx("1/2") }, floor), Is.False,
                "非零档 1/16 < 地板 1/8 ⇒ 拒(取绝对值比较)");
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("-1/16"), fx("0") }, floor), Is.False,
                "负 offset 取模比较仍低于地板 ⇒ 拒");
        }

        [Test]
        public void test_f5_floorInjected_neverAssertsSpecificNumber()
        {
            // 地板数值待用户(与 9 噪声带宽一起定)—— 测试以注入为参,不断言具体定值
            Fix floorA = fx("1/32");
            Fix floorB = fx("1/2");
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("1/16"), fx("0") }, floorA), Is.True, "同一档对不同地板判定不同 ⇒ 注入生效");
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("1/16"), fx("0") }, floorB), Is.False);
        }

        [Test]
        public void test_f5_floorNonPositive_returnsTrue_allowsNoConstraint()
        {
            // 地板 ≤ 0 ⇒ 底线失效,∀ 句恒真(旋钮失效 = 数值轮问题,非求解器问题)
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("1/16") }, new Fix(0L)), Is.True);
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { fx("1/16") }, fx("-1/4")), Is.True);
        }

        // ---- AC-21a-38b:Axis_base + min(offset) > 0(域钳制)----

        [Test]
        public void test_f5_domainClamp_satisfied_whenSumPositive()
        {
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(fx("1"),
                new[] { fx("0"), fx("-3/4"), fx("1/2") }), Is.True,
                "base + min(offset) = 1 − 3/4 = 1/4 > 0 ⇒ 过");
        }

        [Test]
        public void test_f5_domainClamp_justAboveZero_passes_notTightened()
        {
            // D-21-34 张力:和 = 1(最小正单位)必须过 —— 不得就地收紧到 ≥ MIN_USABLE_HALF_LIFE
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(new Fix(Fix.OneRaw), new[] { fx("0") }), Is.True,
                "base + 0 > 0;D-21-34 open:只断言 > 0,不收紧");
        }

        [Test]
        public void test_f5_domainClamp_exactlyZero_fails()
        {
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(fx("1/2"),
                new[] { fx("-1/2"), fx("1/4") }), Is.False,
                "base + min(offset) = 0 ⇒ 拒(9 的 half_life 除数 ≤ 0)");
        }

        [Test]
        public void test_f5_domainClamp_negativeSum_fails()
        {
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(fx("1/4"),
                new[] { fx("-1/2"), fx("0") }), Is.False,
                "base + min(offset) < 0 ⇒ 拒(反向衰减)");
        }

        [Test]
        public void test_f5_domainClamp_allPositiveOffsets_alwaysPasses()
        {
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(fx("1/2"),
                new[] { fx("1/4"), fx("1/2") }), Is.True,
                "全部 offset 为正 ⇒ base + min > base > 0 恒过");
        }

        [Test]
        public void test_f5_domainClamp_noOffsets_noConstraint()
        {
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(fx("1/2"), null), Is.True,
                "空表 ⇒ 无 F5 作用,域约束不生效");
            Assert.That(QualityTimelineSolver.DomainClampSatisfied(fx("1/2"), Array.Empty<Fix>()), Is.True);
        }

        // ---- 复核补测(qa-tester GAPS 发现 #2/#3/#6):全档遍历 + 四轴 switch 臂 + 边界 ----

        /// <summary>QA #3:AC-38 断言的是**通用性质**(仅作用轴平移),求解器明文泛化到 P1a 四轴 ——
        /// 逐轴设为 quality_axis,断言只有该轴变、余三轴逐位等于 base(覆盖 <c>ApplyQualityTimeline</c>
        /// 的 Onset/Peak/Elimination switch 臂,此前只测了 half_life)。</summary>
        [Test]
        public void test_f5_eachAxisShifts_onlyItsOwnAxis()
        {
            Fix shift = fx("1/4");
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = new[] { shift, shift, shift, shift, shift };

            foreach (QualityAxis axis in new[]
                { QualityAxis.Onset, QualityAxis.Peak, QualityAxis.HalfLife, QualityAxis.Elimination })
            {
                p.QualityAxis = axis;
                EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 1);
                Fix expected = new Fix(QualityTimelineSolver.AxisBase(p, axis).Raw + shift.Raw);

                Fix? got = axis switch
                {
                    QualityAxis.Onset => t.Onset,
                    QualityAxis.Peak => t.Peak,
                    QualityAxis.HalfLife => t.HalfLife,
                    QualityAxis.Elimination => t.Elimination,
                    _ => null,
                };
                Assert.That(got.Value.Raw, Is.EqualTo(expected.Raw), $"{axis} 作用轴应含偏移");

                // 余三轴逐位等于 profile 字段(AC-38 通用性质,非仅 half_life 一轴)
                Assert.That(t.Onset!.Value.Raw, Is.EqualTo(axis == QualityAxis.Onset ? expected.Raw : p.Onset!.Value.Raw),
                    $"{axis} 作用下 onset 状态正确");
                Assert.That(t.Peak!.Value.Raw, Is.EqualTo(axis == QualityAxis.Peak ? expected.Raw : p.Peak!.Value.Raw),
                    $"{axis} 作用下 peak 状态正确");
                Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(axis == QualityAxis.HalfLife ? expected.Raw : p.HalfLife!.Value.Raw),
                    $"{axis} 作用下 half_life 状态正确");
                Assert.That(t.Elimination!.Value.Raw, Is.EqualTo(axis == QualityAxis.Elimination ? expected.Raw : p.Elimination!.Value.Raw),
                    $"{axis} 作用下 elimination 状态正确");
            }
        }

        /// <summary>QA #2:AC-38 QA 规格「quality 遍历全档」—— 逐档断言作用轴 = base + offsets[q−1],
        /// 余三轴全程不变(此前只 pin 单档 1/2/3/4/5 各一条)。</summary>
        [Test]
        public void test_f5_fullQualityRange_traversalMatchesOffsetTable()
        {
            DrugProfile p = fixtureProfile();
            Fix[] offsets = { fx("1/4"), fx("1/4"), fx("1/2"), fx("-1/4"), fx("0") };
            p.AxisOffsetByQuality = offsets;

            for (int q = 1; q <= offsets.Length; q++)
            {
                EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, q);
                Assert.That(t.HalfLife!.Value.Raw,
                    Is.EqualTo(new Fix(p.HalfLife!.Value.Raw + offsets[q - 1].Raw).Raw),
                    $"quality = {q} ⇒ half_life = base + offsets[{q - 1}]");
                Assert.That(t.Onset!.Value.Raw, Is.EqualTo(p.Onset!.Value.Raw), $"q={q} onset 不变");
                Assert.That(t.Peak!.Value.Raw, Is.EqualTo(p.Peak!.Value.Raw), $"q={q} peak 不变");
                Assert.That(t.Elimination!.Value.Raw, Is.EqualTo(p.Elimination!.Value.Raw), $"q={q} elimination 不变");
            }
        }

        /// <summary>QA #3:<c>AxisBase</c> 的 throw 臂 —— 四轴各自缺 base 时具名抛出(此前只测 half_life)。</summary>
        [Test]
        public void test_f5_axisBaseMissing_eachAxis_throwsNamed()
        {
            var empty = new DrugProfile();   // 四轴全 null
            foreach (QualityAxis axis in new[]
                { QualityAxis.Onset, QualityAxis.Peak, QualityAxis.HalfLife, QualityAxis.Elimination })
            {
                Assert.Throws<InvalidOperationException>(() => QualityTimelineSolver.AxisBase(empty, axis),
                    $"{axis} 缺 base ⇒ 具名抛");
            }
        }

        /// <summary>QA #6:「档位表在但值为空」(空数组)+ axis 已设 ⇒ <c>OffsetFor</c> 返 0,
        /// 作用轴 = base + 0(此前只覆盖 null 表,未覆盖空数组且 axis 已设的路径)。</summary>
        [Test]
        public void test_f5_emptyOffsetTable_withAxisSet_shiftsByZero()
        {
            DrugProfile p = fixtureProfile();
            p.AxisOffsetByQuality = Array.Empty<Fix>();
            EffectiveTimeline t = QualityTimelineSolver.ApplyQualityTimeline(p, 3);
            Assert.That(t.HalfLife!.Value.Raw, Is.EqualTo(p.HalfLife!.Value.Raw),
                "空数组(非 null)= 无偏移 ⇒ 作用轴 = base + 0");
            Assert.That(t.Onset!.Value.Raw, Is.EqualTo(p.Onset!.Value.Raw));
        }

        /// <summary>QA #6:<c>AbsUl</c> 的 <c>long.MinValue</c> 路径(ulong 实现的存在理由 ——
        /// <c>Math.Abs(long.MinValue)</c> 会抛)从未被走过:偏移取 raw = long.MinValue 时模为 2⁶³,
        /// ≥ 任意正地板 ⇒ 过,且不回绕。</summary>
        [Test]
        public void test_f5_perceptibleFloor_longMinValueOffset_noWrap()
        {
            Assert.That(QualityTimelineSolver.AllOffsetsSatisfyPerceptibleFloor(
                new[] { new Fix(long.MinValue) }, fx("1/2")), Is.True,
                "long.MinValue 的模 = 2⁶³ ≥ 地板(ulong 域比较,不回绕)");
        }

        /// <summary>QA #1:AC-64 规格含 MAX_QUALITY 维(规则六:槽位基数 = base 数 × state 数 × MAX_QUALITY)。
        /// 本求解器的 <c>maxEntries</c> = 最坏槽位基数,须由**调用方**折入该维 —— 本测钉判据对该参数
        /// **单调**:折入更多槽位维只会收紧结论,永不放松 ⇒ 遗漏该维 = 漏判风险(非保守)。
        /// 界 = floor(long.MaxValue / (w × s));w = s = int.MaxValue 时界恰为 1。</summary>
        [Test]
        public void test_weightedTotalFitsInt64_monotoneInEntries()
        {
            Assert.That(StackingSolver.WeightedTotalFitsInt64(int.MaxValue, int.MaxValue, 1), Is.True,
                "条目数 1 在界内");
            Assert.That(StackingSolver.WeightedTotalFitsInt64(int.MaxValue, int.MaxValue, 2), Is.False,
                "条目数翻倍越界 ⇒ 判据单调收紧(折入 MAX_QUALITY 维只减不增)");
        }
    }
}