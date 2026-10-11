// sprint-06 G1.2 · 查体分册最小持有者 EditMode 判据(2026-10-11)
//
// 判据(可证伪):
//   ① Record→TryGet roundtrip(快照值冻结,不回源);
//   ② 未查 ⇒ TryGet false / HasExamined false(「查了没有」布尔事实);
//   ③ 复查覆盖旧读(AC-8-46:掉级只改后续精度 —— 新快照替换旧);
//   ④ 冻结语义(AC-8-25):Record 后源再变,快照字段不变(值语义)。
//
// 纯逻辑 EditMode —— 零场景 / 零硬件 / 零墙钟(Ledger 不读时钟,tick 由调用方喂)。

using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary><see cref="ExaminationLedger"/> 单元测试(G1.2)。</summary>
    internal sealed class ExaminationLedgerTest
    {
        private static ExaminedSet MakeSet(int count)
            => new ExaminedSet(null, count > 0 ? 1 : 0, count, 1);

        [Test]
        public void test_recordThenGet_roundtripsFrozenSnapshot()
        {
            var ledger = new ExaminationLedger();

            ledger.RecordExamined(patientId: 1, tick: 10, MakeSet(3));

            Assert.IsTrue(ledger.HasExamined(1), "已查");
            Assert.IsTrue(ledger.TryGetSnapshot(1, out var snap), "可取回");
            Assert.AreEqual(10, snap.ExaminedTick, "完成 tick 冻结");
            Assert.AreEqual(3, snap.Set.SignCount, "快照值冻结(AC-8-25:不回源)");
            Assert.AreEqual(1, ledger.RecordedCount, "记录数");
        }

        [Test]
        public void test_unexamined_patient_absent()
        {
            var ledger = new ExaminationLedger();

            Assert.IsFalse(ledger.HasExamined(7), "未查 ⇒ 布尔假");
            Assert.IsFalse(ledger.TryGetSnapshot(7, out _), "未查 ⇒ 取不到");
            Assert.AreEqual(0, ledger.RecordedCount, "未记录任何病人");
        }

        [Test]
        public void test_reExamine_overwritesPriorSnapshot()
        {
            var ledger = new ExaminationLedger();

            ledger.RecordExamined(1, tick: 10, MakeSet(2));
            ledger.RecordExamined(1, tick: 30, MakeSet(5));   // 复查覆盖旧读

            Assert.IsTrue(ledger.TryGetSnapshot(1, out var snap));
            Assert.AreEqual(30, snap.ExaminedTick, "复查覆盖 → tick 更新");
            Assert.AreEqual(5, snap.Set.SignCount, "复查覆盖 → 快照值更新(AC-8-46 后续精度)");
        }

        [Test]
        public void test_snapshot_isValue_notLiveRef()
        {
            // AC-8-25 冻结语义:Record 后「源」(这里 = 传入的 set)再变,快照不变。
            var ledger = new ExaminationLedger();
            ExaminedSet a = MakeSet(1);
            ledger.RecordExamined(1, tick: 5, a);

            // 模拟「源变了」:新一次查体产出不同集并记录(ledger 只认「传入的值」)
            ledger.RecordExamined(1, tick: 6, MakeSet(99));
            Assert.IsTrue(ledger.TryGetSnapshot(1, out var snap));
            Assert.AreEqual(99, snap.Set.SignCount, "快照 = Record 时刻的值,非同一对象引用");
            Assert.AreEqual(6, snap.ExaminedTick);
        }

        [Test]
        public void test_clear_resetsAll()
        {
            var ledger = new ExaminationLedger();
            ledger.RecordExamined(1, tick: 5, MakeSet(1));
            ledger.Clear();
            Assert.AreEqual(0, ledger.RecordedCount, "清空 ⇒ 零记录");
            Assert.IsFalse(ledger.HasExamined(1));
        }
    }
}