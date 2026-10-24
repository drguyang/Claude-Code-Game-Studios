// story-004 装配轮 · 2026-10-24 —— 玩家 actor id 花名册(死亡候选链「这个 id 是谁」的唯一载体)。
//
// 权威来源:
//   design/gdd/death-and-respawn.md:90  —— 29 只消费布尔 `PlayerDeathWarranted(actor)`
//   ADR-016 §二 —— 玩家 / 病人 / 敌人共用同一 `IIdAuthority` id 空间(无类型区分)
//   ADR-006 Amendment B —— id 空间「病人 / 敌人 / 玩家共用」,机制 A 计数器
//   unity/Assets/Gameplay.Boot/BootRoot.cs:252 —— 玩家 id 的唯一铸造点
//     (`_services.IdAuthority.NextPatientId().Value`,**装配之后**才知道 ⇒ 必须后置登记)
//
// ── 为什么需要这个类型(2026-10-24 实测缺口)──────────────────────────────
// `ActorClass` 是 `EntityDeathGates` 的形参类型,而**全库没有任何「某 id 属于哪一类」的
// 查询面**。story-008 装配轮留下的 `actorClassOf = _ => ActorClass.Patient`
// (`CompositionRoot.cs:261`)自陈「现状恒 Patient —— 真查询面须等玩家染病路径落地」。
// 那句话对 **9 的 F4** 成立(只有病人有 course),但对 **29 的死亡消费链**不成立:
// `DeathCandidateConsumer` 要用同一个查询面去判「这条候选该不该铸 PlayerDied」,
// 若恒 Patient,类别闸永不放行 ⇒ 装配了也等于没装。
//
// ── 为什么「未登记 ⇒ Patient」不是猜测(域论证)──────────────────────────
// 本花名册只服务**死亡候选链**,而该链的输入域 = 9 的 F4 输出,能进 F4 的实体
// **必然是 course 持有者**(`DiseaseVitalsService.StepAll` 只遍历 `Courses.InOrder`,
// 而 course 只由 `DiseaseOnset` 建档)。敌人 / 兽不发 `DiseaseOnset`
// (ADR-016 §二:它们走 `InjuryStateChanged`,不建病程)⇒ **在本域的补集里只可能是病人**。
// ⇒ 「已登记 = Player,未登记 = Patient」在该输入域上**穷尽且互斥**。
// ⚠️ **域外不成立**:若将来有别处拿本花名册判「某个 id 是不是玩家」,那个结论只在
// 「该 id 已登记」时可靠;「未登记」可能意味着「是病人」也可能意味着「是敌人 / 未知」。
// 故本类只暴露 <see cref="IsPlayer"/>(正向可靠)与 <see cref="ClassOfDeathCandidate"/>
// (域受限,名字里带域),**不**提供「非玩家」这种泛化读法。
//
// ⚠️ **住 `Sim` 而非 `Gameplay.Boot`**:纯 C#、零引擎引用(门 A 不受影响),且
//   **两个装配面都要用它**(9 的 `DiseaseVitalsService` 与 29 的 `DeathCandidateConsumer`
//   都在 `Gameplay.Boot` 构造,但前者已收 `Sim` 的类型)。放 Boot 会让 Sim 侧够不着它。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 玩家 actor id 花名册 —— 回答「这个 id 是玩家吗」。
    ///
    /// <para><b>谁写</b>:<see cref="Gameplay.Boot.BootRoot"/> 在生成玩家之后登记一次
    /// (id 由 `IIdAuthority` 在**装配之后**铸造,故登记是后置动作 ——
    /// 与 `CasebookVitalsSource.SetViewer` / `PrescriptionPageHost.SetAuthor` 同格)。</para>
    ///
    /// <para><b>谁读</b>:29 的死亡候选链(<see cref="DeathCandidateConsumer"/> 的
    /// <c>actorClassOf</c>)与 9 的体征链(<c>DiseaseVitalsService</c> 的 <c>actorClassOf</c>)。</para>
    ///
    /// <para><b>不是状态真源</b>:本类**不写三流、不进存档** —— 玩家身份的唯一权威是
    /// 「玩家实体由 BootRoot 生成」这件事本身,本类只是把它投影成一个可查询的谓词。
    /// 读档后由 BootRoot 重新登记同一 id(`next_player_id` 是存档头字段位,
    /// `entities.yaml`;不新增第三个 C# 计数器)。</para>
    ///
    /// <para><b>非线程安全</b>:与 sim 的单线程 tick 模型一致(ADR-005)。</para>
    /// </summary>
    public sealed class PlayerActorRegistry
    {
        private readonly HashSet<int> _players = new HashSet<int>();

        /// <summary>
        /// 登记一个玩家 actor id(幂等)。
        /// </summary>
        /// <param name="actorId">玩家 id(非负 —— 机制 A 计数器 id 空间)。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorId"/> &lt; 0。</exception>
        public void Register(int actorId)
        {
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 actorId 不得为负(机制 A 计数器 id 空间;PatientId.None(-1)是哨兵)");
            _players.Add(actorId);
        }

        /// <summary>已登记的玩家数(观测面;装配 / 测试读它,不参与判据)。</summary>
        public int Count => _players.Count;

        /// <summary>
        /// 该 id 是否**已登记为玩家**。
        /// <para>⚠️ <c>false</c> 只表示「未登记」,不表示「是病人」——
        /// 泛化读法见类型注释的域论证。</para>
        /// </summary>
        public bool IsPlayer(int actorId) => _players.Contains(actorId);

        /// <summary>
        /// 死亡候选链上的类别判定 —— 「已登记 = <see cref="ActorClass.Player"/>,
        /// 未登记 = <see cref="ActorClass.Patient"/>」。
        ///
        /// <para>⚠️ <b>名字里带域,是刻意的</b>:该结论**只在死亡候选链的输入域上成立**
        /// (见类型注释的域论证)。别处若要判实体类别,请用真正的类别源
        /// (敌人 = 27 的数据表,病人 = 9 的 course),不要借本方法。</para>
        /// </summary>
        public ActorClass ClassOfDeathCandidate(int actorId)
            => IsPlayer(actorId) ? ActorClass.Player : ActorClass.Patient;
    }
}
