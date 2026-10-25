// 瞬时源具名契约(`TD-persist-04` 动作 ① 裁定轮 · 2026-10-25 用户裁定甲案)。
//
// 权威来源:
//   design/gdd/persistence-service.md:211-215 —— 规则十四「无未决医疗动作」的**瞬时**量
//     (内存态,不在流里 —— 2026-10-25 裁定丙:流内累积钩子已证伪,接线回退 null);
//   design/gdd/persistence-service.md:106 —— 「叙事上下文观察钩子(InNarrativeContext(t)),
//     一个量(GDD `:212`/`:219` 逐字「与手动存共用同一观察钩子」)」;
//   production/qa/evidence/review-sprint19-2026-10-25.md §七「W5 的裁定轨迹」。
//
// ─────────────────────────────────────────────────────────────────────────────
// 裁定记录(2026-10-25,原文见 docs/tech-debt-register.md TD-persist-04 条目同日注):
//   · 契约形态 = 甲案「具名接口」—— 住 Sim.Contracts(与八个 P0 抽象点同构);
//   · 10 急救动作落地后由 Boot(CompositionRoot)注入真实现;10 未落地前维持
//     null(fail-closed,Checkpoint / Load / SaveOnExit 一律抛);
//   · 接口命名 / 签名形状归实现轮(本文件即实现轮落点);
//   · ⚠️ 是否把本接口计入「P0 抽象点」编号(八 → 九)**未裁,不代裁** ——
//     本件只按裁定甲落实契约与缝,不动 ADR-005 的抽象点计数。
// ─────────────────────────────────────────────────────────────────────────────
//
// ⚠️ 「一个量」的类型层落实:SaveService 与 SaveSlotLedger 共享**同一个**实例
//    (装配层只传一次),读门与写门因此不可能分叉 —— 两处各持 Func<bool> 的
//    旧形态做不到这一点。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>
    /// 叙事上下文瞬时源(规则十四的「一个量」):<b>此刻</b>是否无未决医疗动作。
    /// <para><b>瞬时</b>语义是本契约的全部意义:实现方必须从**运行时内存态**回答
    /// (如 10 急救动作「有没有进行中的急救 / 未处置的病人」),**不得**从事件流
    /// 推导「曾出现过锚点」—— 那是累积量,首次锚点后恒 true ⇒ AC-7a-19 (c)
    /// 安全门恒开(fail-open),已被 W5 轨迹实测证伪。</para>
    /// <para>返回 <c>true</c> = 在叙事上下文内(可存可读);<c>false</c> = 存读一律拒绝。</para>
    /// </summary>
    public interface INarrativeContextQuery
    {
        /// <summary>此刻是否处于「无未决医疗动作」的安全上下文(瞬时求值,每次调用重答)。</summary>
        bool InNarrativeContext();
    }

    /// <summary>
    /// <see cref="INarrativeContextQuery"/> 的 BCL 适配器:把委托包成具名契约。
    /// <para>用途有二:① 测试夹具把既有 lambda 实参(<c>() =&gt; true</c>)平移到新缝;
    /// ② 装配根在 10 落地前用**手工供给的瞬时量**做装配(生产今日传 null,不用本类)。</para>
    /// <para>⚠️ 本类不是「恒真后门」—— 它只是形状适配;值仍由调用方全权负责。</para>
    /// </summary>
    public sealed class FuncNarrativeContextQuery : INarrativeContextQuery
    {
        private readonly Func<bool> _query;

        /// <summary>包装一个瞬时求值委托;null 拒收(fail-fast)。</summary>
        public FuncNarrativeContextQuery(Func<bool> query)
        {
            _query = query ?? throw new ArgumentNullException(nameof(query));
        }

        /// <summary>便捷常量:恒在叙事上下文内(测试用)。</summary>
        public static readonly FuncNarrativeContextQuery AlwaysIn = new FuncNarrativeContextQuery(() => true);

        /// <summary>便捷常量:恒不在叙事上下文内(测试用;≠ null —— 这是「可用的拒绝」,
        /// null 是「配置错误」,两者必须可区分,承 SaveSlotLedger 的同款纪律)。</summary>
        public static readonly FuncNarrativeContextQuery NeverIn = new FuncNarrativeContextQuery(() => false);

        /// <inheritdoc />
        public bool InNarrativeContext() => _query();
    }
}
