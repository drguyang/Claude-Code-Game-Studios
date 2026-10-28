// 关卡工具(ADR-022)的**槽位子集**一致性检查 + 导出确定性测试。
//
// 权威来源:
//   ADR-022 §四(C1–C6;本批只实装槽位子集三条)· §②(失败 = 显式 `throw`,非 `Debug.Assert`)
//
// ⚠️ **为什么单列一个夹具件**:`LevelConsistencyCheck` 的三条硬检查此前**零夹具**
//    —— 按 `coding-standards.md` §「某件有门」,那三条只能记 `NOT-RUN`。本件把它们
//    逐条打成「**真会红**」的判据:每条负向夹具断言**判据关键词**(字面量),
//    不引用被测类的常量 —— 自指判据在变异时会两边一起动,零红逃逸。
//
// ⚠️ 本件**不写盘**:`LevelExporter.ExportBuildSlots` 只返回字符串,落盘另有其法
//    (`ExportTo` 才写)。⇒ 不触发 `FsWriteBoundaryGate`。

using System;
using System.Collections.Generic;
using NUnit.Framework;
using DaYiJingCheng.EditorTools.Level;
using DaYiJingCheng.Sim.World;   // SlotType —— 2026-10-28 评审 A-21 后不再是工具侧的镜像枚举

namespace DaYiJingCheng.Tests.LevelTool
{
    /// <summary>关卡工具槽位子集:三条硬检查 + 导出确定性。</summary>
    [TestFixture]
    public class level_tool_consistency_test
    {
        /// <summary>真种子(P0 医馆)三条硬检查全绿,且**未实现项清单恰好 7 条**。</summary>
        /// <remarks><b>判别力</b>:① 把「重复格」检查改成无条件触发 ⇒ 本测红;
        /// ② 把 `UnimplementedChecks` 删成空数组 ⇒ 本测红(未实现项被美化 = 假完备)。</remarks>
        [Test]
        public void test_levelTool_realSeed_passesSlotSubsetChecks_andDeclaresSevenUnimplemented()
        {
            LevelSource src = P0ClinicLevel.Define();
            CheckReport report = LevelConsistencyCheck.Check(src);

            Assert.That(report.IsGreen, Is.True,
                "真种子须全绿,实际违例:\n" + string.Join("\n", report.Violations));
            Assert.That(src.Slots.Count, Is.EqualTo(13), "P0 种子 = 9 Decor + 4 Shell");
            // 🔁 **2026-10-29(关卡工具 story-003 AC-6)7 → 5**:C4 / C5 已实现并移出本表。
            // 🔁 **2026-10-29(关卡工具 story-005 AC-6)5 → 3**:C3 / C6 已实现并移出,
            //    剩 C1 / C2 / C2'(全归 S6)。
            // 🔁 **2026-10-29(关卡工具 story-006 AC-5)3 → 0**:C1 / C2 / C2' 全部落地。
            //    ⚠️ **「0」读作「七项都有实现」,不读作「七项在生产上都跑出了结论」** ——
            //    其中 C1 / C2' 需要作者侧声明作输入,而今日全库零视觉层资产、零模块 prefab
            //    ⇒ 它们在**生产上**进 `CheckReport.NotRun`(第三态),**不是绿**。
            //    这个区分由 `consistency_c1_c2_c2prime_test` 的三态夹具守着。
            //    ⚠️ 用例名里的 `Seven` 是历史名,保留不改 —— 改名会让 grep 断链(原文不删的纪律)。
            Assert.That(report.Unimplemented.Count, Is.EqualTo(0),
                "ADR-022 §四 的 C1–C6 七项均已有实现 —— " +
                "多报 = 把已实现说成未实现;少报(反向)由 story-003/005/006 逐轮钉住:\n" +
                string.Join("\n", report.Unimplemented));
        }

        /// <summary>重复格 ⇒ 违例,报文点名「重复」+ 坏格坐标。</summary>
        [Test]
        public void test_levelTool_duplicateCell_reportsViolation()
        {
            var src = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.Decor, true),
                new SlotSpec(1, 0, 1, SlotType.Shell, true),
            });

            CheckReport report = LevelConsistencyCheck.Check(src);

            Assert.That(report.IsGreen, Is.False, "重复格须判红");
            Assert.That(Has(report, "重复"), Is.True, "报文须点名判据「重复」");
            Assert.That(Has(report, "(1,0,1)"), Is.True, "报文须点名坏格坐标");
        }

        /// <summary>`SlotType.None` ⇒ 违例(哨兵不是合法槽位类型)。</summary>
        [Test]
        public void test_levelTool_noneSlotType_reportsViolation()
        {
            var src = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.None, false),
            });

            CheckReport report = LevelConsistencyCheck.Check(src);

            Assert.That(report.IsGreen, Is.False, "None 须判红");
            Assert.That(Has(report, "None"), Is.True, "报文须点名「None」");
            Assert.That(Has(report, "哨兵"), Is.True, "报文须说明「哨兵不是合法槽位类型」");
        }

        /// <summary>格越出声明范围 ⇒ 违例。</summary>
        [Test]
        public void test_levelTool_outOfRangeCell_reportsViolation()
        {
            var src = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(4, 0, 1, SlotType.Decor, false),   // x=4 在 [0,4) 之外
            });

            CheckReport report = LevelConsistencyCheck.Check(src);

            Assert.That(report.IsGreen, Is.False, "越界格须判红");
            Assert.That(Has(report, "超出声明范围"), Is.True, "报文须点名判据「超出声明范围」");
            Assert.That(Has(report, "(4,0,1)"), Is.True, "报文须点名坏格坐标");
        }

        /// <summary>
        /// `baked_initial = true` 但 `module_id = 0` ⇒ 违例(ADR-022 `:176` 的契约是 **`≥ 1`**)。
        /// <para>🔁 <b>2026-10-28 评审 S-1 修复(新增)</b>:首版判 `== SlotSpec.NoModule`(即 `== -1`),
        /// 差集 = `0` **从导出门漏过去**,只有绑定器 / codec 才拒。而导出器是「唯一作者」,
        /// 作者的错就该在作者自己的工具里报 —— 漏到绑定期等于把「少写一个字段」的报文
        /// 指到下游,排查成本翻倍。</para>
        /// <para>⚠️ 与 `-1`(完全没写)分开测:`-1` 走的是「没有 module_id」分支,`0` 走的是
        /// 「给了非法值」分支 —— 两个分支各需一例(承「一属性一条变异」的 Branch 覆盖口径)。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:把判据改回 `== SlotSpec.NoModule` ⇒ 本条红(0 不再被拒)。</remarks>
        [Test]
        public void test_levelTool_bakedInitialWithModuleIdZero_reportsViolation()
        {
            var src = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.Decor, bakedInitial: true, moduleId: 0),
            });

            CheckReport report = LevelConsistencyCheck.Check(src);

            Assert.That(report.IsGreen, Is.False, "module_id = 0 须判红(ADR-022 契约是 ≥ 1)");
            Assert.That(Has(report, "baked_initial"), Is.True, "报文须点名字段「baked_initial」");
            Assert.That(Has(report, "module_id ≥ 1"), Is.True,
                "报文须点名契约「module_id ≥ 1」—— 否则作者看不出合法值域");
        }

        /// <summary>
        /// `baked_initial = true` 且 `module_id` 完全没写(`NoModule = -1`)⇒ 违例。
        /// <para>与上一条同族但**不同分支**:这条走「没有 module_id」,上一条走「给了非法值」。</para>
        /// </summary>
        /// <remarks><b>判别力</b>:把判据的 `s.ModuleId &lt;= 0` 改成 `s.ModuleId &lt; 0` ⇒ 本条仍绿
        /// (=-1 仍被拒)、上一条红 ⇒ 两条合起来钉住整个 `≤ 0` 面。</remarks>
        [Test]
        public void test_levelTool_bakedInitialWithoutModuleId_reportsViolation()
        {
            var src = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, SlotType.Decor, bakedInitial: true),
            });

            CheckReport report = LevelConsistencyCheck.Check(src);

            Assert.That(report.IsGreen, Is.False, "baked_initial=true 但无 module_id 须判红");
            Assert.That(Has(report, "没有 module_id"), Is.True,
                "报文须点名「没有 module_id」(与「给了非法值」分支的措辞分开)");
        }

        /// <summary>`CheckOrThrow` 在违例时**真的抛**(ADR-022 §② 的构建期失败通道)。</summary>
        [Test]
        public void test_levelTool_checkOrThrow_throwsOnViolation()
        {            var bad = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(9, 0, 9, SlotType.Decor, false),
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => LevelConsistencyCheck.CheckOrThrow(bad));
            Assert.That(ex.Message.IndexOf("ADR-022", StringComparison.Ordinal) >= 0, Is.True,
                "异常须点名裁决出处 ADR-022 §四:\n" + ex.Message);

            // 正例:真种子不得抛(证明不是「恒抛」)
            Assert.DoesNotThrow(() => LevelConsistencyCheck.CheckOrThrow(P0ClinicLevel.Define()));
        }

        /// <summary>同一关卡源导出两次 ⇒ **逐字符一致**(确定性 JSON)。</summary>
        /// <remarks><b>判别力</b>:导出行序改成依赖声明序 / 引入时间戳 / 字典迭代 ⇒ 本测红。</remarks>
        [Test]
        public void test_levelTool_export_isDeterministic()
        {
            LevelSource src = P0ClinicLevel.Define();
            string a = LevelExporter.ExportBuildSlots(src);
            string b = LevelExporter.ExportBuildSlots(P0ClinicLevel.Define());

            Assert.That(a, Is.EqualTo(b), "同源两次导出不等 ⇒ 有非确定性来源混入");

            // 行序 = (x,y,z) 升序:首个槽位必须是最小格,不是声明序的第一条。
            Assert.That(a.IndexOf("\"slot_type\"", StringComparison.Ordinal) > 0, Is.True);
            int firstCell = a.IndexOf("[1, 0, 1]", StringComparison.Ordinal);
            int shellCell = a.IndexOf("[6, 0, 6]", StringComparison.Ordinal);
            Assert.That(firstCell, Is.GreaterThan(0), "导出须含 (1,0,1)");
            Assert.That(shellCell, Is.GreaterThan(firstCell),
                "行序须按 (x,y,z) 升序排序 ⇒ (1,0,1) 出现在 (6,0,6) 之前");
        }

        /// <summary>导出前**跑检查**:给一个违例源 ⇒ 抛(门在导出路径上,不是摆设)。</summary>
        /// <remarks>
        /// ⚠️ **违例形态刻意选「枚举未定义值」**(2026-10-28 证伪轮订正):首版用的是**重复格**,
        /// 结果 L1(停用重复格检查)除预期那条外**又**红了本条 ⇒ 判别面不清。
        /// 根因 = 本条与「重复格」这一条守卫**耦合**,它证明的其实是那条守卫而不是「导出跑检查」。
        /// 改用 `(SlotType)99` 后,破坏面只剩「导出路径是否调用 `CheckOrThrow`」(= L4),与三条硬检查解耦。
        /// <para>⚠️⚠️ **2026-10-28 二次订正(代码评审 Q-7)**:解耦**引入了新缺陷** ——
        /// 只断 <see cref="InvalidOperationException"/> **类型**时,
        /// `LevelExporter.TypeName` 对 `(SlotType)99` **抛同类型异常**(`LevelExporter.cs:118-120`)
        /// ⇒ 停用 `CheckOrThrow` 后本条**仍绿**,对 L4 零判别力。
        /// 现加**报文断言**:`"ADR-022 §四"` 只有 `LevelConsistencyCheck.CheckOrThrow` 的报文有。
        /// 判据 = 两种异常在**类型上不可分**,只能靠**报文**分。</para>
        /// </remarks>
        [Test]
        public void test_levelTool_export_runsConsistencyCheck()
        {
            var bad = new LevelSource("t", 4, 1, 4, new List<SlotSpec>
            {
                new SlotSpec(1, 0, 1, (SlotType)99, false),   // 闭集外
            });

            var ex = Assert.Throws<InvalidOperationException>(
                () => LevelExporter.ExportBuildSlots(bad));
            Assert.That(ex.Message.IndexOf("ADR-022 §四", StringComparison.Ordinal) >= 0, Is.True,
                "抛出的必须是 `CheckOrThrow` 的一致性失败,不是 `TypeName` 的「无可读导出名」" +
                "(两者同为 InvalidOperationException ⇒ 只断类型对 L4 零判别力):\n" + ex.Message);
        }

        // ── 辅助 ──────────────────────────────────────────────────────────────

        /// <summary>任一条违例报文含指定**字面量**关键词。</summary>
        private static bool Has(CheckReport report, string literal)
        {
            for (int i = 0; i < report.Violations.Count; i++)
                if (report.Violations[i].IndexOf(literal, StringComparison.Ordinal) >= 0)
                    return true;
            return false;
        }
    }
}
