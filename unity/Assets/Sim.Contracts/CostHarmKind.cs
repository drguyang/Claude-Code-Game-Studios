// T1 · S4 —— 用药伤害的种类(`harm_kind`)。**住 Sim.Contracts**,因为两侧都要用它:
//   写者 = 53 的新 Kind `TreatmentHarmOnset`(载荷字段,住 Sim.Contracts 的 payload struct);
//   读方 = 53 的代价表 `CostTable53`(Sim)。
//
// 权威:ADR-035 §Decision ④(`harm_kind` 是载荷里的 i32 枚举)/ ⑥(过量支 P0 **挂起**,
//       枚举成员保留但 `harm_kind_enabled` 不许启用)。
//
// ⚠️ ordinal 是**落盘格式的一部分**(载荷按 int 编码,承 EventKind 的同类教训):
//   成员**只许追加**,不许插入/重排 —— 插入会让历史事件静默读成另一种伤害。
//
// ⚠️ 取值与作者态 JSON 的字符串**一一对应**,但映射**不在这里**(作者态 → 整数的唯一转换点
//   是 ADR-014 阶段 2 的绑定器 / 烘焙产物),本枚举只说「运行期有哪几种」。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>用药伤害的种类(代价表的 `harm_kind` 轴)。</summary>
    public enum CostHarmKind
    {
        /// <summary>禁忌:给的药与病人病情相冲(`contraindications` 命中)。P0 **唯一启用**的种类。</summary>
        Contraindication = 0,

        /// <summary>
        /// 过量:反复 / 超量给药。**P0 挂起**(`ADR-035 §Decision ⑥`:解除条件 = `TD-sim-02` +
        /// `TD-prescription-09` 两条均结案)。枚举成员保留,以便将来只追加数据、不动结构。
        /// </summary>
        Overdose = 1,
    }
}
