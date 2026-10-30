// 53 两张表的**测试侧共享装载** —— 供那些「只关心别的表、却被装配要求 53 表」的夹具使用。
//
// 为什么要这个文件(2026-10-30 · ADR-036 S4 装配轮):
//   `CompositionRoot.AssembleCore` 现在会向 `IDataProvider` 要 `OutcomeTable53DataSet` 与
//   `DelayTable53DataSet`(53 判定链要它们)。若测试替身按「未登记 ⇒ 抛『本测不该问到它』」
//   作答,那 5 个 `MultiTableProvider` 夹具会整族转红 —— 而它们的抱怨**不再是接线错**:
//   装配现在**确实**该问这两张表(与 2026-10-30 ADR-035 给 `CostTable53DataSet` 加的那一支同因)。
//
// ⚠️ **为什么不返回「空表」**:延迟表**构造期就拒空**(`DelayTable53.Table` 拒收 ③:
//   定义域三支须全在,缺行 = 该回响静默不发)。⇒ 只能给**真表**;真表从仓库源 JSON 现烘
//   (`BakeTableFromRepo`,与 `medcons_delayed_emit_test` / `medcons_outcome_judge_test` 同一路径),
//   **不抄任何数值**(延迟值仍由数值轮所有)。
//
// ⚠️ 静态缓存 = 跨测例共享只读数据(烘一次,之后只读)。它**不随测例变化**,
//   也不被任何夹具写入 ⇒ 不构成「共享可变状态」;若将来需要改写,须改为每测例新建。

using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Presentation;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 判定表 / 延迟表的测试侧装载(仓库源 JSON → 烘焙 → 数据集)。</summary>
    internal static class MedCons53Tables
    {
        private static OutcomeTable53DataSet? _outcome;
        private static DelayTable53DataSet? _delay;

        /// <summary>判定表数据集(18 行;从仓库源 JSON 现烘)。</summary>
        public static OutcomeTable53DataSet Outcome
            => _outcome ??= new OutcomeTable53DataSet(
                OutcomeTable53.BakeTableFromRepo(MedConsRepoPaths.RepoRoot), 0u);

        /// <summary>延迟表数据集(定义域三支;从仓库源 JSON 现烘)。</summary>
        public static DelayTable53DataSet Delay
            => _delay ??= new DelayTable53DataSet(
                DelayTable53Bake.BakeTableFromRepo(MedConsRepoPaths.RepoRoot), 0u);
    }
}
