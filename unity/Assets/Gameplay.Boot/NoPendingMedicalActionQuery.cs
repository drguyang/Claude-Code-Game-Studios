// 规则十四「无未决医疗动作」瞬时源的生产实装(2026-10-25 用户裁定「生产接线今日不为零,现在修」)。
//
// 权威来源:
//   design/gdd/persistence-service.md:216-218 —— 载入安全上下文门(「无未决医疗动作」,瞬时量);
//   design/gdd/persistence-service.md:211-215 —— 规则十四语义(手动存 / 载入同钩);
//   W5 裁定轨迹(docs/tech-debt-register.md TD-persist-04)—— 瞬时源 = **独立于流的内存态**,
//     归 10 急救动作的运行时状态;流内累积钩子已于 2026-10-25 被证伪(首次锚点后恒 true
//     ⇒ AC-7a-19 (c) 安全门恒开 fail-open),不得回归;
//   ADR-013 Amendment A —— `IArmedState` 模态开集只读契约(命名空间住
//     `DaYiJingCheng.Gameplay.Interaction`,实现体 = `EmergencyChannelDriver`);
//   Sim.Contracts/INarrativeContextQuery.cs —— 裁定甲具名接口(2026-10-25)。
//
// 语义:
//   `InNarrativeContext()` = `!IsArmed` —— 急救动作进行中(Armed)= 有未决医疗动作
//   = **不在**叙事上下文。**瞬时求值,每次调用重答**,零缓存 —— 缓存就是累积化的第一步
//   (W5 教训);顺延语义(`CheckpointFallback`)与读档门(`SaveService.Load`)都建立在
//   「每次调用重答」上。
//
// ⚠️ 范围登记(如实,禁借绿):
//   今日覆盖「急救 Armed」一类未决动作。GDD 语境里的「出诊流程进行中」(52,零载体)与
//   「病人未处置」的锚点半语义**不在本查询** —— 前者归 `TD-persist-04` 动作 ②(出诊启动
//   Kind 登记),锚点事件半边整体归 `TD-persist-04` 动作 ①(8 / 37 / 52 接线)。
//   各自落地时**并入本查询**(单一「一个量」),不得另立第二查询。
//
// Bind 晚绑定(装配序约束):
//   `CompositionRoot.Assemble` 里 `EmergencyChannelDriver` 的构造段晚于 `SaveService`
//   构造段(依赖序),而 `SaveService` 构造即需瞬时源 ⇒ 本类允许「先构造、后 Bind」。
//   **未 Bind 期 `InNarrativeContext()` 恒 `false`**(fail-closed —— 与裁定丙
//   「null ⇒ 不可用」同向:装配未完成 = 不可用,不是「可用」)。Bind 只许一次;
//   二次 Bind = 编程错误,抛(换源须换新装配,不许热替换半途换答案)。

using System;
using DaYiJingCheng.Gameplay.Interaction;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 「无未决医疗动作」瞬时查询 —— <see cref="INarrativeContextQuery"/> 的生产实装
    /// (读 <see cref="IArmedState.IsArmed">急救通道 Armed 态</see>,取反)。
    /// <para>瞬时求值、零缓存;未 <see cref="Bind"/> 期恒 <c>false</c>(fail-closed)。</para>
    /// </summary>
    public sealed class NoPendingMedicalActionQuery : INarrativeContextQuery
    {
        private IArmedState _armed;

        /// <summary>是否已绑定急救通道(装配观测面;未绑 = 恒不安全)。</summary>
        public bool IsBound => _armed != null;

        /// <summary>
        /// 绑定急救通道的 Armed 态来源(装配期一次;晚于 <c>SaveService</c> 构造的依赖序所要求)。
        /// </summary>
        /// <param name="armed">Armed 态只读源(生产 = <c>EmergencyChannelDriver</c>)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="armed"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">重复 Bind(只许一次)。</exception>
        public void Bind(IArmedState armed)
        {
            if (armed == null)
                throw new ArgumentNullException(nameof(armed),
                    "Armed 态来源为 null —— 换成「恒不安全」请直接不 Bind,不要绑空源");
            if (_armed != null)
                throw new InvalidOperationException(
                    "NoPendingMedicalActionQuery 只许 Bind 一次 —— 热替换会让同一时刻的" +
                    "读档门与兜底门给出不同答案(规则十四「一个量」被拆成两个)");
            _armed = armed;
        }

        /// <summary>
        /// 此刻是否无未决医疗动作(**瞬时求值,每次调用重答**)。
        /// <para>未 Bind ⇒ 恒 <c>false</c>(fail-closed);已 Bind ⇒ <c>!IsArmed</c>。</para>
        /// </summary>
        public bool InNarrativeContext() => _armed != null && !_armed.IsArmed;
    }
}
