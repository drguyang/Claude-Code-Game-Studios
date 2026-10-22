// ADR-025 §① 成员(第九抽象点)· GDD persistence-service.md F-7a-4 —— 折叠谓词抽象点。
//
// 权威来源:
//   GDD persistence-service.md:314-330 —— Folded(p) = 终态(p) ∧ ¬豁免(p) ∧ ¬未结案(p)(单一出处)
//   ADR-010 §二                        —— 病史流折叠 + 折叠行形状
//   ADR-008 §六                        —— 条件 ③ 无未结案病例
//   ADR-026 §⑦                         —— 条件 ④ SkillGrown 行豁免(案 1)
//   ADR-032 ⑥                          —— 条件 ① 的消费口径 = 存在性查询(不重放 F4)
//   ADR-025 §①:112                     —— 用户 2026-10-21 裁定「甲案」:抽象点住 Sim.Contracts、
//                                          实现 FoldedPredicate 住 Sim
//
// 为什么抽象点必须住这里(裁定 甲案 的理由,原文):
//   三条件的数据源全部住 `Sim` —— 条件 ② 的豁免字段(`RegistrySchema` 的
//   Plateau / SelfLimit / Relapse / Lethality)与条件 ③ 的 `CaseStreamQuery.HasOpenCase`
//   都在 `Sim`;而 `Sim` 与 `Sim.Codec` 是**兄弟装配、互不可见**(ADR-025 §①)⇒
//   7a 的 codec / SaveService 若要用它,只能收一个住 Contracts 的抽象点。
//
// ⚠️ 本接口**刻意只有布尔** —— 折叠行产出(onset / 病种_id / patient_id / patient_seed /
//   outcome / t_end)是**数据装配**,不是判定;它归 `HistoryFolder`,不进本接口。

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 病史流折叠谓词(7a 的义务,实现住 <c>Sim</c>)。
    ///
    /// <para><b>真 ⇒ 该病人的病史行进折叠</b>(GDD F-7a-4「输出:布尔」)。
    /// 四条件的合并**唯一出处**是 GDD F-7a-4 与本实现;9 的 AC-36 只定义 ①②,
    /// 37 的结案判定只定义 ③,7a 是唯一把三者合起来的地方。</para>
    /// </summary>
    public interface IFoldedPredicate
    {
        /// <summary>
        /// 病人 <paramref name="patientId"/> 的病史行是否可折叠。
        /// </summary>
        /// <param name="patientId">病人 id(须 ≥ 0;<see cref="PatientId.None"/> 不是可折叠对象)。</param>
        /// <returns>四条件全部成立 ⇒ <c>true</c>。</returns>
        bool Folded(int patientId);
    }
}
