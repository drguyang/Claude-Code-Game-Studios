// P0 的 `IEventAuthority` 生产占位(2026-10-30 · ADR-036 S4 装配轮)
//
// 权威:ADR-007 §一(`IEventAuthority` = 第六抽象点 · 主机权威 / 掷骰权语义)
//       · ADR-005(主机唯一写)· ADR-036 D2(53 的发射器须经写权门)
//
// ── 为什么需要它 ────────────────────────────────────────────────────────────
// `ConsequenceEmitter` 的构造要求一个 `IEventAuthority`(写权门 `IsHost`)。
// 而全仓 `IEventAuthority` **零生产实装** —— 早已登记为已知缺口:
//   · `Gameplay.Boot/SeenDiseaseProjector.cs:23-26`(「全仓无生产实装 …… 联机轮 P1b 须补」)
//   · `Gameplay.Presentation/Diagnosis/DiagnosisGrowthGate.cs:14`(`IsHost` 在 P0 恒 true 的自陈)
//   · `CompositionRoot.cs:871`(同一句话)
// ⇒ 53 的装配轮是**第一个**真正需要它的生产点(后果写入必须过写权门)。
//
// ── 它是什么(以及不是什么)────────────────────────────────────────────────
// **是**:P0 单机 = 唯一主机 ⇒ `IsHost` / `IsAuthority` 恒 true(与 `BootRoot` 现阶段恒
//        `SimAuthorityMode.Host` 同事实,见 `DiseaseVitalsService` 头注的 HostAuthorityMode 段)。
// **不是**:一个联机权威。P1b 的客户端侧(ADR-001)会把它换成会随会话角色变化、
//        且 `Roll` 有真实实现的版本;届时本类**退役**,不是「扩展」。
//
// ⚠️ `Roll` 抛 `NotSupportedException` 而非返回假值:P0 **没有任何掷骰需求**
//    (52 随机事件导演未实现;`RollRequest`/`EventRollResult` 的生产调用点为零)。
//    返回假值会让「有人开始掷骰了」这件事**静默通过**;抛则立刻暴露。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// **P0 占位权威**的标记面(2026-10-30)。
    /// <para>为什么要这个标记:51 有一条哨兵(`OutOfBoundsRejectionTest.test_e3_realOnline_nonHostRejected_gapSentinel`)
    /// 的逻辑是「生产树里一旦出现 `IEventAuthority` 实装 ⇒ 那条『真联机』缺口就算就绪 ⇒ 哨兵必须转真驱动」。
    /// 本类**不是**真联机权威(它恒 `IsHost = true`),但会被那条哨兵扫到 —— 若不区分,
    /// 哨兵会把「P0 单机占位」误判成「真联机就绪」。
    /// ⇒ 占位实现本接口;哨兵**排除**实现本接口的类型,**且要求排除集非空**(否则排除条件会随时间静默失效)。</para>
    /// </summary>
    public interface IP0AuthorityPlaceholder { }

    /// <summary>P0 单机宿主的事件写权权威(`IsHost` / `IsAuthority` 恒 true;`Roll` 未实现)。</summary>
    public sealed class P0HostEventAuthority : IEventAuthority, IP0AuthorityPlaceholder
    {
        /// <summary>P0 = 权威侧(单机无对端)。</summary>
        public bool IsAuthority => true;

        /// <summary>P0 = 主机(写权门恒放行;与 `BootRoot` 恒 `SimAuthorityMode.Host` 同事实)。</summary>
        public bool IsHost => true;

        /// <summary>未实现 —— P0 无掷骰需求(52 未落地)。</summary>
        /// <exception cref="NotSupportedException">恒抛。</exception>
        public EventRollResult Roll(in RollRequest r)
            => throw new NotSupportedException(
                "P0 无掷骰实现:52 随机事件导演尚未落地,`Roll` 的生产调用点为零。" +
                "联机 / 掷骰轮须另立真正的 `IEventAuthority` 实装(不得返回假值 —— " +
                "假值会让『有人开始掷骰了』静默通过)。");
    }
}
