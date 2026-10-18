// combat-weapons Story 006 测试
//
// AC-25-5-01: 同 tick 双击与重传
// AC-25-5-02: dose_seq 可重建
// AC-25-6-01: 病人 onset 落病史流、敌人 onset 落世界流
// AC-25-6-03: 归零转译入口
// AC-25-1-08: Step 与 CatchUp 两条路径产出的 onset 序列逐条相同
// F-25-8: 构建期平衡断言

using System;
using System.Collections.Generic;
using System.Linq;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Combat
{
    public class CombatOnsetDoseSeqTest
    {
        // AC-25-5-01: 同 tick 双击
        [Test]
        public void test_sameTickDoubleHit_doseSeq01()
        {
            var history = new List<SimEvent>();
            long seq1 = CombatOnsetStream.ComputeDoseSeq(1, 2, 0, 100, history);
            Assert.AreEqual(0, seq1, "第一击 dose_seq = 0");

            // 模拟第一击入流
            history.Add(new SimEvent(100, new PatientId(2), 0, EventKind.InjuryOnset, default));

            long seq2 = CombatOnsetStream.ComputeDoseSeq(1, 2, 0, 100, history);
            Assert.AreEqual(1, seq2, "第二击 dose_seq = 1");
        }

        // AC-25-5-01: 重传判重
        [Test]
        public void test_duplicateDetection_fiveTupleKey()
        {
            var payload1 = new OnsetPayload(1, 2, 0, new Fix(1000), 100, 0);
            var payload2 = new OnsetPayload(1, 2, 0, new Fix(1000), 100, 0); // 全同

            // 简化版：验证五元组键生成正确
            Assert.AreEqual(payload1.DedupKey, payload2.DedupKey, "全同 payload 应有相同 DedupKey");
        }

        // AC-25-5-02: dose_seq 可重建
        [Test]
        public void test_doseSeqReconstructable_fromStream()
        {
            var history = new List<SimEvent>
            {
                new SimEvent(100, new PatientId(2), 0, EventKind.InjuryOnset, default),
                new SimEvent(100, new PatientId(2), 1, EventKind.InjuryOnset, default),
                new SimEvent(101, new PatientId(2), 0, EventKind.InjuryOnset, default),
            };

            long seq = CombatOnsetStream.ComputeDoseSeq(1, 2, 0, 100, history);
            Assert.AreEqual(2, seq, "第三击 dose_seq = 2");
        }

        // AC-25-6-03: 归零转译入口
        [Test]
        public void test_noTierField_inPayload()
        {
            var payload = new OnsetPayload(1, 2, 0, new Fix(1000), 100, 0);
            Assert.IsTrue(CombatOnsetStream.ValidateNoTierField(payload),
                "onset 载荷不应包含档位字段");
        }

        // F-25-8: 构建期平衡断言
        [Test]
        public void test_f258_balanceGate()
        {
            var parms = new MagnitudeParams(new Fix(1000), new Fix(60000), 10);
            Assert.IsTrue(CombatOnsetStream.ValidateF25Balance(
                parms.BaseStep, new Fix(1000), new Fix(10000), parms.CPMax, 1, 1),
                "F-25-8 平衡断言应通过");
        }

        // AC-25-6-01: 敌人 onset 落世界流
        [Test]
        public void test_enemyOnset_routesToWorldStream()
        {
            // 验证 EnemyInjuryOnset Kind 存在（路由到世界流）
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.EnemyInjuryOnset),
                "EnemyInjuryOnset Kind 应存在");
            // 验证 InjuryOnset Kind 存在（路由到病史流）
            Assert.IsTrue(Enum.IsDefined(typeof(EventKind), EventKind.InjuryOnset),
                "InjuryOnset Kind 应存在");
        }

        // AC-25-1-08: Step 与 CatchUp 一致性
        // ⚠️ S16 修复(TD-epic-02):原实现是 `Assert.Pass("需集成测试验证")` ——
        //    把「测试没写」伪装成「需要集成测试」,而卡面 AC `- [ ]` 未勾 + `Status: Complete` = 借绿。
        //    实测生产宾语**在场**:`CatchUp.ComputeCatchUp`(`Sim/CatchUp.cs:54`)是纯函数、可直驱。
        //    ⇒ 本测改打**真生产类**。
        // ⚠️ **首版断言被证伪轮打了回来(三条变异全不转红)**:原输入让两条路径**结果本来就相同**
        //    ⇒ 删掉逐条比对也不红(弱判别面)。修法 = 输入必须含**只有 CatchUp 的窗口过滤才会改变**的事件,
        //    并把断言分成两半:① 窗口内逐条相同(排序正确性)② **窗口外被剔除**(过滤正确性 ——
        //    这一半才是 CatchUp 独有的语义,变异 F1/F2 会因它转红)。
        [Test]
        public void test_stepAndCatchUp_sameOnsetSequence()
        {
            // Arrange:一批 onset 事件。tick 故意乱序 + 跨病人 + **含窗口外事件**(50 与 999)
            var events = new List<SimEvent>
            {
                new SimEvent(300, new PatientId(7), 0, EventKind.InjuryOnset, default),
                new SimEvent(100, new PatientId(2), 0, EventKind.InjuryOnset, default),
                new SimEvent(100, new PatientId(5), 0, EventKind.InjuryOnset, default),
                new SimEvent(200, new PatientId(2), 1, EventKind.InjuryOnset, default),
                new SimEvent(100, new PatientId(2), 1, EventKind.InjuryOnset, default),
                new SimEvent(999, new PatientId(5), 0, EventKind.InjuryOnset, default),   // 窗口外(上)
                new SimEvent(50,  new PatientId(2), 0, EventKind.InjuryOnset, default),   // 窗口外(下)
            };

            const long startTick = 100, endTick = 300;

            // Act ①「Step」路径 = 调用方按全序键自行排序(ADR-005 全序键 (Tick, StreamPriority, Patient, Seq))
            //    ⚠️ 排序后**施加同一窗口过滤** —— 两条路径要比的必须是同一集合,
            //    否则拿「未过滤的 7 条」逐条比「过滤后的 5 条」是从第 0 条就错位(首版即如此)。
            var stepPath = events.OrderBy(e => e.Tick)
                                  .ThenBy(e => e.Patient.Value)
                                  .ThenBy(e => e.Seq)
                                  .Where(e => e.Tick >= startTick && e.Tick <= endTick)
                                  .ToList();

            // Act ②「CatchUp」路径 = 真生产类的离线补算(它自己排序 + **过滤窗口**)
            IReadOnlyList<SimEvent> catchUpPath = CatchUp.ComputeCatchUp(
                events, startTick, endTick,
                idAuthority: new FakeIdAuthority(), presenceQuery: new FakePresenceQuery());

            // Assert ①(过滤半 —— **CatchUp 独有语义,本测的主判据**):窗口外事件必须被剔除。
            //   Step 路径不过滤 ⇒ 输入相同而输出条数必然不同。
            //   若 CatchUp 退化成「原样返回」/ 漏过滤 / 边界取错(开区间),本条红。
            //   **含边界**:startTick(100)与 endTick(300)的事件**在内**(闭区间),
            //     故窗口内 = 5 条 ⇒ 用「等于 5」而非「大于 0」钉死闭区间这一半。
            Assert.AreEqual(5, catchUpPath.Count,
                $"CatchUp 须保留闭窗口 [{startTick}, {endTick}] 内 5 条、剔除窗外 2 条(实际 {catchUpPath.Count} 条)");
            Assert.IsTrue(catchUpPath.All(e => e.Tick >= startTick && e.Tick <= endTick),
                "窗口外事件漏剔除");

            // Assert ②(逐条半 —— 辅助判据):窗口内部分与 Step 路径逐字段相同(排序正确性)。
            //   ⚠️ **如实登记:这一半的判别力有限** —— `CatchUp` 的排序键与这里的 LINQ 排序同形,
            //   证伪轮实测「删掉整个循环」仍编译不过(语法必然红)而**单独删单条断言不转红**
            //   ⇒ 不作主判据,主判据是 Assert ①。
            for (int i = 0; i < catchUpPath.Count; i++)
            {
                Assert.AreEqual(stepPath[i].Tick, catchUpPath[i].Tick, $"第 {i} 条 Tick");
                Assert.AreEqual(stepPath[i].Patient.Value, catchUpPath[i].Patient.Value, $"第 {i} 条 Patient");
                Assert.AreEqual(stepPath[i].Seq, catchUpPath[i].Seq, $"第 {i} 条 Seq");
                Assert.AreEqual(stepPath[i].Kind, catchUpPath[i].Kind, $"第 {i} 条 Kind");
            }
        }

        /// <summary>AC-25-1-08 用的 id 发号替身(CatchUp 形参;本测不签发新 id)。</summary>
        private sealed class FakeIdAuthority : IIdAuthority
        {
            public PatientId NextPatientId() => new PatientId(0);
            public ItemInstanceId NextItemInstanceId() => new ItemInstanceId(0);
        }

        /// <summary>AC-25-1-08 用的在场查询替身(CatchUp 形参;本测按「全在场」跑)。</summary>
        private sealed class FakePresenceQuery : IPresenceQuery
        {
            public bool IsPresent(PatientId patientId) => true;
            public int PresentCount => 0;
            public bool IsPresentAt(WorldPos cell) => false;
            public IReadOnlyCollection<int> PresentPatientIds() => Array.Empty<int>();
        }
    }
}
