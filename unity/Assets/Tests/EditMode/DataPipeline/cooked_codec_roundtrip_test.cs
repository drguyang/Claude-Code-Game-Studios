// 权威来源:ADR-014 §二(运行期读烘焙产物,零 JSON 解析)· E-13(解码失败 = 启动期硬失败)
//          · 写方镜像:`Editor.Tools.Bake/{PrescriptionActionsCookedWriter,
//            DiseaseActionAxisCookedWriter,OutcomeTableCookedWriter,DelayTableCookedWriter}`
//          · 产物漂移门:`DataPipeline/cooked_drift_all_artifacts_test.cs`(字节层)
//
// ⚠️ **本文件补的是「读方 ↔ 写方镜像」的门** —— 漂移门只证明「盘上字节 == 现烘字节」,
//    **不证明有人读得懂**它。2026-10-20 第二轮接线为 4 份产物首次写运行期 codec 时,
//    实测抓出一处**契约违例**:`DiseaseActionAxisCookedWriter` 用 `BinaryWriter.Write(string)`
//    (7-bit 变长前缀)写字符串,而 `CookedFormat` 明文规定 = **i32 字节长 + UTF-8**。
//    违例在无读方时完全不可见 —— 这正是本门的立门理由。
//
// ⚠️ **判据形态**:对每份产物,分别解码
//      ① 盘上已提交的字节(出货面)
//      ② baker 现烘的字节(源面)
//    并断言两者**语义相等**。任一侧布局解释错误 ⇒ 解码抛异常或字段不等 ⇒ 红。
//
// ⚠️ **本门不做的事**:不比对「数值是否合理 / 业务是否对」—— 那只归各系统的 AC。
//    它只回答一个问题:**写方写下的字节,读方是否按同一布局理解**。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Sim.Consequences;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.DataPipeline
{
    /// <summary>
    /// <b>cooked 读方镜像门</b> —— 4 份新接线产物的运行期 codec 必须与写方布局一致。
    /// <para>⚠️ 每份产物一条测试:一条红 ⇒ 直接指出是**哪一份**布局不对。</para>
    /// </summary>
    [TestFixture]
    public class CookedCodecRoundTripTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".", "..", "..", "..", "..", ".."));

        private static string CookedDir
            => Path.Combine(RepoRoot, "unity", "Assets", "DataCooked");

        private static byte[] ReadCommitted(string fileName)
        {
            string path = Path.Combine(CookedDir, fileName);
            Assert.That(File.Exists(path), Is.True, $"盘上产物缺失:{path}");
            return File.ReadAllBytes(path);
        }

        // ══════════════════ ① prescription_actions ══════════════════

        /// <summary>处方表:盘上字节与现烘字节解码后逐行相等。</summary>
        /// <remarks><b>判别力</b>:读方字段序或字符串形错位 ⇒ 解码抛异常或字段不等。
        /// 若写方用了 7-bit 变长串而读方按 i32 读,`AssertFullyConsumed` / 长度校验会先炸。</remarks>
        [Test]
        public void test_roundTrip_prescriptionActions_diskMatchesFreshBake()
        {
            PrescriptionActionsDataSet disk = PrescriptionActionsCookedCodec.Read(
                ReadCommitted(DataBakeMenu.PrescriptionActionsCookedAssetName));
            PrescriptionActionsDataSet fresh = PrescriptionActionsCookedCodec.Read(
                PrescriptionActionsBaker.BakeFromRepo(RepoRoot).Cooked);

            Assert.That(disk.DoseBase, Is.EqualTo(fresh.DoseBase), "DOSE_BASE 解码不等");
            Assert.That(disk.MaxDoseDetents, Is.EqualTo(fresh.MaxDoseDetents), "MAX_DOSE_DETENTS 解码不等");
            Assert.That(disk.SingleDoseMaxRaw, Is.EqualTo(fresh.SingleDoseMaxRaw), "single_dose_max 解码不等");
            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 解码不等");

            Assert.That(disk.Rows.Count, Is.EqualTo(fresh.Rows.Count), "行数解码不等");
            for (int i = 0; i < fresh.Rows.Count; i++)
            {
                Assert.That(disk.Rows[i].ItemKey, Is.EqualTo(fresh.Rows[i].ItemKey), $"rows[{i}].item_key");
                Assert.That(disk.Rows[i].ActionId, Is.EqualTo(fresh.Rows[i].ActionId), $"rows[{i}].action_id");
                Assert.That(disk.Rows[i].Polarity, Is.EqualTo(fresh.Rows[i].Polarity), $"rows[{i}].polarity");
            }
        }

        // ══════════════════ ② disease_action_axis ══════════════════

        /// <summary>处置轴:两段行集(actions / treatable_by)解码后逐行相等。
        /// <para>⚠️ 本测是 **2026-10-20 契约违例修正**的回归夹具 —— 写方原用 7-bit 变长串,
        /// 改回 `BinaryWriter.Write(string)` 会让本测红(读方按 i32 读)。</para></summary>
        [Test]
        public void test_roundTrip_diseaseActionAxis_diskMatchesFreshBake()
        {
            DiseaseActionAxisDataSet disk = DiseaseActionAxisCookedCodec.Read(
                ReadCommitted(DataBakeMenu.DiseaseActionAxisCookedAssetName));
            DiseaseActionAxisDataSet fresh = DiseaseActionAxisCookedCodec.Read(
                DiseaseActionAxisBaker.BakeFromRepo(RepoRoot).Cooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 解码不等");

            Assert.That(disk.Actions.Count, Is.EqualTo(fresh.Actions.Count), "处置轴行数解码不等");
            for (int i = 0; i < fresh.Actions.Count; i++)
            {
                Assert.That(disk.Actions[i].ActionId, Is.EqualTo(fresh.Actions[i].ActionId), $"actions[{i}].id");
                Assert.That(disk.Actions[i].Name, Is.EqualTo(fresh.Actions[i].Name), $"actions[{i}].name");
                Assert.That(disk.Actions[i].Owner, Is.EqualTo(fresh.Actions[i].Owner), $"actions[{i}].owner");
            }

            Assert.That(disk.TreatableBy.Count, Is.EqualTo(fresh.TreatableBy.Count), "treatable_by 行数解码不等");
            for (int i = 0; i < fresh.TreatableBy.Count; i++)
            {
                Assert.That(disk.TreatableBy[i].DiseaseKey, Is.EqualTo(fresh.TreatableBy[i].DiseaseKey),
                    $"treatable_by[{i}].disease");
                Assert.That(disk.TreatableBy[i].ActionId, Is.EqualTo(fresh.TreatableBy[i].ActionId),
                    $"treatable_by[{i}].action");
                Assert.That(disk.TreatableBy[i].Polarity, Is.EqualTo(fresh.TreatableBy[i].Polarity),
                    $"treatable_by[{i}].polarity");
            }
        }

        // ══════════════════ ③ 53_outcome_table ══════════════════

        /// <summary>53 判定表:表元数据 + 全键域查表结果逐一相等。</summary>
        [Test]
        public void test_roundTrip_outcomeTable_diskMatchesFreshBake()
        {
            OutcomeTable53DataSet disk = OutcomeTableCookedCodec.Read(
                ReadCommitted(DataBakeMenu.OutcomeTableCookedAssetName));
            OutcomeTable53DataSet fresh = OutcomeTableCookedCodec.Read(
                OutcomeTable53.BakeFromRepo(RepoRoot).Cooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 解码不等");
            Assert.That(disk.Table.RowCount, Is.EqualTo(fresh.Table.RowCount), "行数解码不等");
            Assert.That(disk.Table.UBandCount, Is.EqualTo(fresh.Table.UBandCount), "档数解码不等");
            Assert.That(disk.Table.HasDefaultRow, Is.EqualTo(fresh.Table.HasDefaultRow), "default 位不等");
            Assert.That(disk.Table.DefaultOutcome, Is.EqualTo(fresh.Table.DefaultOutcome), "default 出口解码不等");

            // 全键域逐点对拍(18 组合 × 档数)—— 只比行数会漏掉「键错位但行数相等」。
            foreach (Outcome53.JudgmentLane p in Enum.GetValues(typeof(Outcome53.JudgmentLane)))
            foreach (Outcome53.CareLane c in Enum.GetValues(typeof(Outcome53.CareLane)))
            foreach (Outcome53.RecoveryLane d in Enum.GetValues(typeof(Outcome53.RecoveryLane)))
            for (int band = 0; band < fresh.Table.UBandCount; band++)
            {
                bool hitDisk = disk.Table.TryLookup(p, c, d, band, out Outcome53.Outcome oDisk);
                bool hitFresh = fresh.Table.TryLookup(p, c, d, band, out Outcome53.Outcome oFresh);
                Assert.That(hitDisk, Is.EqualTo(hitFresh), $"查表命中位不等 @({p},{c},{d},band={band})");
                Assert.That(oDisk, Is.EqualTo(oFresh), $"查表结果不等 @({p},{c},{d},band={band})");
            }
        }

        // ══════════════════ ④ 53_delay_table ══════════════════

        /// <summary>53 延迟表:定义域三支的延迟量逐一相等。</summary>
        [Test]
        public void test_roundTrip_delayTable_diskMatchesFreshBake()
        {
            DelayTable53DataSet disk = DelayTableCookedCodec.Read(
                ReadCommitted(DataBakeMenu.DelayTableCookedAssetName));
            DelayTable53DataSet fresh = DelayTableCookedCodec.Read(
                DelayTable53Bake.BakeFromRepo(RepoRoot).Cooked);

            Assert.That(disk.ConfigVersion, Is.EqualTo(fresh.ConfigVersion), "ConfigVersion 解码不等");
            Assert.That(disk.Table.RowCount, Is.EqualTo(fresh.Table.RowCount), "行数解码不等");

            IReadOnlyList<int> domain = DelayTable53.DomainOutcomes;
            Assert.That(domain.Count, Is.GreaterThan(0), "扫描键守卫 —— 定义域为空则下面恒真");
            foreach (int outcome in domain)
            {
                bool hitDisk = disk.Table.TryDelayTicks(outcome, out long dDisk);
                bool hitFresh = fresh.Table.TryDelayTicks(outcome, out long dFresh);
                Assert.That(hitDisk, Is.True, $"盘上产物缺定义域行 outcome={outcome}");
                Assert.That(hitFresh, Is.True, $"现烘产物缺定义域行 outcome={outcome}");
                Assert.That(dDisk, Is.EqualTo(dFresh), $"delay_ticks 不等 @outcome={outcome}");
            }
        }
    }
}
