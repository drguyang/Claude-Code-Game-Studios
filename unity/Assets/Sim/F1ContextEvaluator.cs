// F1 上下文输入的生产求值器 —— `TD-randomevents-05` 的兑现件(2026-11-05)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么单独成类(而不是装配层的一个 λ)
// ─────────────────────────────────────────────────────────────────────────────
//   改前这段逻辑是 `CompositionRoot` 里一个内联闭包,三项旗标**全部写死 false**:
//
//       contextOf: w => new F1PipelineInput(w, windowSize: 0,
//           isNight: …, isOnExpedition: false, isInClinic: false,
//           inCombatCooldown: false, seasonIndex: 0),
//
//   ⇒ 上下文门恒假 ⇒ 急召生成链**恒 `NoContext`**,与池表 / 权重实现无关。
//   写成 λ 的代价是:**没有落点可测** —— 只能靠整条启动序(PlayMode)才能观察到它,
//   于是「三项生产恒假」这件事在 EditMode 层完全不可见。
//   ⇒ 本类把它提成 `Sim` 的**纯函数**,生产闭包与夹具走**同一条代码路径**。
//   ⚠️ 这不是「为测试而改结构」:它与 `ActiveHouseCallQuery` / `DeathCellQuery` 同格
//      —— 都是「从流里判读一个派生布尔」的纯函数,本就该有名字。
//
// ─────────────────────────────────────────────────────────────────────────────
// 三个旗标的真源(GDD random-events.md 规则六之二 / 十二)
// ─────────────────────────────────────────────────────────────────────────────
//   IsInClinic      玩家此刻所在格 ∈ 医馆地块。
//                   格取自**流里最后一条 `ActorCellEntered`**(ADR-020 §四:玩家位移是
//                   表现态,sim 侧唯一投影就是这个事件;禁读 `Transform`)。
//                   ⚠️ 未绑玩家 / 流里还没有该玩家的格 ⇒ **false**(fail-safe:
//                   「不知道在哪」按「不在医馆」处理 —— 与 `ClinicCellSource` 未绑定同向)。
//   IsOnExpedition  出诊路径 ≡ **(有活跃出诊目标) ∧ (不在医馆地块内)** —— 规则六之二 ③ 逐字。
//                   ⚠️ 出诊类 `event_key` 集今日**空**(`TD-randomevents-06`:归 37 内容件回填)
//                   ⇒ 本项生产恒 false。那是**等数据**,集合一填即生效,零代码改动。
//   InCombatCooldown 战斗结算后的冷却窗 —— **P0 无来源**(规则六之二第三行;25 / 27 侧尚无
//                   该状态的流表达)⇒ 恒 false,是**正确的当下值**,不是占位。
//                   接源后改本类的实参,`EvaluateContextGate` 的语义不变。
//
// ─────────────────────────────────────────────────────────────────────────────
// 同源纪律
// ─────────────────────────────────────────────────────────────────────────────
//   `clinicCells` **必须**是服务袋单点发放的那个实例(与 `HouseCallStartedWriter` 同一个)。
//   另立实例 = 导演在同一 tick 对「玩家在不在医馆」得到两个答案(静默分叉,DC-1 警告同型)。
//   `houseCallEventKeys` 同理(与写者共用一个数组实例)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// F1 上下文输入的生产求值器(纯函数 —— 全部输入来自流 / 注册表 / 装配期常量,零墙钟)。
    /// </summary>
    public static class F1ContextEvaluator
    {
        /// <summary>未绑定玩家时的哨兵(与 <c>PatientId.None = -1</c> 同值但语义不同:这里是「没有玩家」)。</summary>
        public const int NoPlayer = -1;

        /// <summary>
        /// 求值一次 F1 上下文。
        /// </summary>
        /// <param name="tick">本次掷骰 tick(**不是**窗口起点 —— 玩家格必须按此刻算)。</param>
        /// <param name="events">事件流只读视图。</param>
        /// <param name="decoder">载荷解码器(ADR-031;读 <c>ActorCellEntered</c> / <c>EventArrived</c>)。</param>
        /// <param name="clinicCells">医馆地块谓词(**服务袋单实例**,同源义务)。</param>
        /// <param name="playerActorId">玩家实体 id;<see cref="NoPlayer"/> = 未绑定。</param>
        /// <param name="houseCallEventKeys">出诊类 `event_key` 集(与写者共用同一实例)。</param>
        /// <param name="isNight">该 tick 是否夜间(由装配方按 <see cref="World.TimeBase.IsNight"/> 算)。</param>
        /// <exception cref="ArgumentNullException">任一引用参为 null。</exception>
        /// <exception cref="InvalidOperationException">流内急召链接断裂
        /// (由 <see cref="ActiveHouseCallQuery"/> fail-loud 传播,不在此吞)。</exception>
        public static F1PipelineInput Evaluate(
            long tick,
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            IClinicCellSource clinicCells,
            int playerActorId,
            IReadOnlyCollection<int> houseCallEventKeys,
            bool isNight)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (clinicCells == null) throw new ArgumentNullException(nameof(clinicCells));
            if (houseCallEventKeys == null) throw new ArgumentNullException(nameof(houseCallEventKeys));

            events ??= Array.Empty<SimEvent>();

            // ① 玩家此刻在不在医馆地块内。
            bool inClinic = false;
            if (playerActorId >= 0
                && DeathCellQuery.TryLastCellOf(events, decoder, playerActorId, out WorldPos playerCell))
                inClinic = clinicCells.IsClinicCell(playerCell);

            // ② 出诊路径 = 有活跃出诊目标 ∧ 不在医馆(规则六之二 ③)。
            //    ⚠️ 医馆内**短路**:规则要求两项同时成立,而在医馆时后项已假 ⇒ 不必查流。
            //    这不是优化 —— 它保证「医馆内永不判成出诊」这一条不依赖 `ActiveHouseCallQuery` 的实现。
            bool onExpedition = false;
            if (!inClinic)
                onExpedition = ActiveHouseCallQuery.Evaluate(events, decoder, houseCallEventKeys).Active;

            // ③ 战斗冷却:P0 无来源(见文件头)。恒 false 是当下正确值。
            return new F1PipelineInput(
                tick, windowSize: 0,
                isNight: isNight,
                isOnExpedition: onExpedition,
                isInClinic: inClinic,
                inCombatCooldown: false,
                seasonIndex: 0);
        }
    }
}
