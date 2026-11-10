// 急救动作表的**产物管线** —— 判据夹具(T14 · 2026-11-09)
//
// 权威:`design/gdd/emergency-procedures.md` `:843`(10-DC:路径 / 主键 / 列)
//       · ADR-014(两阶段烘焙 · Fix 字段写字符串 · ConfigVersion = 内容哈希)
//       · AC-10-16 / `OQ-10-1`(`result_mul` 三档已裁定)
//
// ─────────────────────────────────────────────────────────────────────────────
// 读哪些仓库文件(测试标准 §只读外部夹具的具名声明义务)
// ─────────────────────────────────────────────────────────────────────────────
// 读 `assets/data/emergency_action.json`(经生产 binder)与
//   `unity/Assets/DataCooked/emergency_action.cooked.bytes`(经生产 codec)。
// 为什么必须读真表:本件判的正是「**作者态 → 产物 → 运行期行**这三段是否逐值一致」。
// ⚠️ 本文件零写盘。
//
// ─────────────────────────────────────────────────────────────────────────────
// 本文件要证明的六件事
// ─────────────────────────────────────────────────────────────────────────────
//   ① 源表能过 binder,产物能过 codec,且**行数 / id 不变**;
//   ② 产物里的动作 0 与 `EmergencyActionTable.ProductionDefault` **逐字段相等**
//      (代码里那份只是无 provider 时的回落 —— 两份副本不许分叉);
//   ③ `result_mul` 三档 == AC-10-16 裁定值(改回 0 须重开 `OQ-10-1`);
//   ④ binder 负控:坏 id / 坏 result_mul / 重复 id / 空 rows / Fix 写成数字 —— 各拒收;
//   ⑤ codec 负控:载荷长度被改坏 ⇒ 抛(E-13);
//   ⑥ 装载面:provider 地址常量 == 产物文件名;磁盘兜底读得到同一份字节。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.EmergencyProcedures;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EmergencyProcedures
{
    [TestFixture]
    public sealed class emergency_action_artifact_test
    {
        private static string RepoRoot()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "assets", "data",
                                                 EmergencyActionBaker.SourceFileName)))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            Assert.Fail("找不到仓根(须含 assets/data/" + EmergencyActionBaker.SourceFileName + ")");
            return null;
        }

        private static string Seed()
            => File.ReadAllText(Path.Combine(RepoRoot(), EmergencyActionBaker.DataDirName,
                                             EmergencyActionBaker.SourceFileName));

        private static EmergencyActionBinder.BindResult Bound() => EmergencyActionBinder.Bind(Seed());

        // ════════════════════════════════════════════════════════════════
        //  ① 源 → 产物 → 行
        // ════════════════════════════════════════════════════════════════

        /// <summary>源表绑定出恰一行动作 0;产物解码出同一行。</summary>
        [Test]
        public void test_sourceAndProduct_agreeOnRowSet()
        {
            EmergencyActionBinder.BindResult bound = Bound();
            Assert.That(bound.Rows.Count, Is.EqualTo(4), "A1(2026-11-10):动作 0/1/2/3 四行已齐");
            Assert.That(bound.Rows[0].ActionId, Is.EqualTo(0));

            EmergencyActionBaker.BakeOutput baked = EmergencyActionBaker.BakeFromJson(Seed());
            // 抛形:解码失败各自抛 E-13,不需要断言返回值
            EmergencyActionDataSet ds = EmergencyActionCookedCodec.Read(baked.Cooked);

            Assert.That(ds.Rows.Count, Is.EqualTo(4));
            Assert.That(ds.Rows[0].ActionId, Is.EqualTo(0));
            Assert.That(ds.SchemaVersion, Is.EqualTo(EmergencyActionCookedCodec.ExpectedSchemaVersion));
        }

        // ════════════════════════════════════════════════════════════════
        //  ② 产物行 == 代码回落行(逐字段)
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// `EmergencyActionTable.ProductionDefault` 与产物**逐字段相等**。
        /// <para>代码里那份只剩一个用途:无 provider 的上下文(部分 EditMode 夹具)。
        /// 它**不是**第二真源 —— 本条把它钉在产物上,分叉即红。</para>
        /// </summary>
        [Test]
        public void test_productionDefault_equalsArtifactRow()
        {
            EmergencyActionDataSet ds = DataCookedDisk.LoadEmergencyActions();
            EmergencyActionRow fromArtifact = ds.Rows[0];
            EmergencyActionRow fromCode = EmergencyActionTable.ProductionDefault.Require(0);

            Assert.That(fromCode.ActionId, Is.EqualTo(fromArtifact.ActionId));
            Assert.That(fromCode.Polarity, Is.EqualTo(fromArtifact.Polarity), "polarity 分叉");
            Assert.That(fromCode.BasePotency, Is.EqualTo(fromArtifact.BasePotency), "base_potency 分叉");
            Assert.That(fromCode.MagThreshold, Is.EqualTo(fromArtifact.MagThreshold), "mag_threshold 分叉");
            Assert.That(fromCode.MinEdges, Is.EqualTo(fromArtifact.MinEdges), "min_edges 分叉");
            Assert.That(fromCode.MinHoldTicks, Is.EqualTo(fromArtifact.MinHoldTicks), "min_hold_ticks 分叉");
            Assert.That(fromCode.HalfLifeTicks, Is.EqualTo(fromArtifact.HalfLifeTicks), "half_life_ticks 分叉");
            Assert.That(fromCode.DurationTicks, Is.EqualTo(fromArtifact.DurationTicks), "duration_ticks 分叉");
            Assert.That(fromCode.ResultMul, Is.EqualTo(fromArtifact.ResultMul), "result_mul 分叉");
        }

        // ════════════════════════════════════════════════════════════════
        //  ③ result_mul == AC-10-16
        // ════════════════════════════════════════════════════════════════

        /// <summary>产物里的三档 == `GetResultMulTiers()`(AC-10-16 的绝对裁定值)。</summary>
        [Test]
        public void test_resultMul_matchesAc10_16()
        {
            long[] tiers = DataCookedDisk.LoadEmergencyActions().Rows[0].ResultMul;
            Assert.That(tiers, Is.EqualTo(EmergencyActionSchema.GetResultMulTiers()));
            Assert.That(tiers[0], Is.EqualTo(16384), "Missed = 0.25,**非零**(OQ-10-1 已裁;改回 0 须重开)");
            Assert.That(tiers[1], Is.EqualTo(32768), "AppliedWeak = 0.5");
            Assert.That(tiers[2], Is.EqualTo(65536), "Applied = 1.0");
        }

        // ════════════════════════════════════════════════════════════════
        //  ④ binder 负控
        // ════════════════════════════════════════════════════════════════

        private static string Mutate(string from, string to)
        {
            string seed = Seed();
            Assert.That(seed.Contains(from), Is.True, "夹具前提:种子里找不到被替换片段 —— " + from);
            return seed.Replace(from, to);
        }

        /// <summary>action_id 不在 `EmergencyAction` 闭集 ⇒ 拒收(DC-4)。</summary>
        [Test]
        public void test_binder_rejectsUnknownActionId()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => EmergencyActionBinder.Bind(Mutate("\"action_id\": 0,", "\"action_id\": 7,")));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("DC-4"));
        }

        /// <summary>result_mul 被改成 0 ⇒ 拒收(AC-10-16 / OQ-10-1)。</summary>
        [Test]
        public void test_binder_rejectsZeroedResultMul()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => EmergencyActionBinder.Bind(Mutate("\"1/4\"", "\"0\"")));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("AC-10-16"));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("OQ-10-1"));
        }

        /// <summary>Fix 字段写成 JSON 数字 ⇒ 拒收(数字会经 double 中转绕开 FixParse)。</summary>
        [Test]
        public void test_binder_rejectsNumericFix()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => EmergencyActionBinder.Bind(Mutate("\"base_potency\": \"1\"", "\"base_potency\": 1")));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("base_potency"));
        }

        /// <summary>空 rows ⇒ 拒收(空表会让生产第一笔急救 fail-loud)。</summary>
        [Test]
        public void test_binder_rejectsEmptyRows()
        {
            var ex = Assert.Throws<BakeValidationException>(
                () => EmergencyActionBinder.Bind("{\"schema_version\": 1, \"rows\": []}"));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("为空"));
        }

        /// <summary>重复 action_id ⇒ 拒收(「用哪一行」取决于迭代序 = 不可重放)。</summary>
        [Test]
        public void test_binder_rejectsDuplicateActionId()
        {
            const string oneRow =
                "{\"action_id\":0,\"polarity\":2,\"base_potency\":\"1\",\"mag_threshold\":50," +
                "\"min_edges\":1,\"min_hold_ticks\":5,\"half_life_ticks\":100,\"duration_ticks\":200," +
                "\"result_mul\":[\"1/4\",\"1/2\",\"1\"]}";
            string doubled = "{\"schema_version\":1,\"rows\":[" + oneRow + "," + oneRow + "]}";

            var ex = Assert.Throws<BakeValidationException>(() => EmergencyActionBinder.Bind(doubled));
            Assert.That(string.Join(";", ex.Errors), Does.Contain("出现两次"));
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑤ codec 负控
        // ════════════════════════════════════════════════════════════════

        /// <summary>载荷长度被改坏 ⇒ 抛(E-13),不返回半截表。</summary>
        [Test]
        public void test_codec_rejectsCorruptedPayloadLength()
        {
            byte[] bytes = DataCookedDisk.ReadAll(AddressablesDataProvider.EmergencyActionAddress);
            bytes[16] = (byte)(bytes[16] + 4);   // payloadLength 末字节 +4

            Assert.Throws<InvalidDataException>(
                () => EmergencyActionCookedCodec.Read(bytes));
        }

        // ════════════════════════════════════════════════════════════════
        //  ⑥ 装载面
        // ════════════════════════════════════════════════════════════════

        /// <summary>provider 地址常量必须就是产物文件名(否则装载面找的是另一份)。</summary>
        [Test]
        public void test_addressConstant_matchesProductFileName()
        {
            Assert.That(AddressablesDataProvider.EmergencyActionAddress,
                        Is.EqualTo(EmergencyActionDataSet.CookedFileName));
            Assert.That(DataBakeMenu.EmergencyActionCookedAssetName,
                        Is.EqualTo(EmergencyActionDataSet.CookedFileName));
        }

        /// <summary>磁盘兜底读得到同一份字节,且能建表。</summary>
        [Test]
        public void test_diskFallback_buildsTable()
        {
            EmergencyActionDataSet ds = DataCookedDisk.LoadEmergencyActions();
            var table = new EmergencyActionTable(ds.Rows);

            Assert.That(table.Count, Is.EqualTo(4));
            Assert.That(table.Require(0).BasePotency, Is.EqualTo(65536), "动作 0 的 base_potency = 1.0");
        }
    }
}
