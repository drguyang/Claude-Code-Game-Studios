// S24-1 · processing story-005 —— 完成时点调度(规则八派生量的**调度侧**)
//
// 权威来源:
//   design/gdd/processing.md
//     · 规则八(:300-370)时序 —— 「发起即落流、完成 = 派生」
//     · F-18.3(:458-461)完成时点 `CompleteTick(craft) = craft.start_tick + recipe.duration_ticks`
//     · R-18-D[甲]②(:279-297)起货溢出 —— 完成 tick 当场本格 `DropSpawned`,
//       成品 id 引用点火时已铸的 `output_instance_ids[]`(**无第二铸造点**)
//     · AC-18-21 —— 「时间到达 `CompleteTick` ⇒ 器物状态变化可判起货 + cue 发射」
//   ADR-005 —— 主机唯一 Step/Append;完成 tick 到达由 `ITickProvider` 驱动(禁墙钟 / 帧对齐)
//   ADR-009 §六 —— 溢出落地 = 世界流 `DropSpawned`(身份进流 / 位置表现);**写者是 20,不是 18**
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//   ADR-031 —— 载荷解码唯一路径 = `IPayloadDecoder`
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么是「**全流扫描**」而不是「问每个 actor 的最后一炉」
// ─────────────────────────────────────────────────────────────────────────────
// `FurnaceState.TryCompleteTickOf` 给的是**某 actor 最后一炉**的完成时点。用它在调度侧
// 逐个 actor 问「你到点了吗」会漏掉一种形态:
//
//   玩家 A 于 tick 100 点火(duration 20 ⇒ 完成 120);
//   A 在 tick 120 当拍**再点火**(`IsBusy` 取半开区间 ⇒ 完成当拍可立即再点火)。
//
// 此时该 actor 的「最后一炉」已经是**新**那炉(完成 140),旧的 120 那炉在
// `TryGetLatest` 眼里已经不存在了 —— 但**它当拍正是要起货的那一炉**。
// ⇒ 调度必须问「**有哪些**炉的 `start + duration` 恰等于本 tick」,而不是
//   「每个玩家的最新炉到点了吗」。本类即前者。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据口径(三条,缺一即与别处口径分叉)
// ─────────────────────────────────────────────────────────────────────────────
// ① **等号,不是区间**:`CompleteTick == tick`。AC-18-21 的「误差 0」是判据本身,
//    不是容差 —— 用 `>=` 会让每一拍都重新「完成」一次。
// ② **前缀与定序都只用 envelope tick**(承 `FurnaceState` 的 2026-10-26 W1 订正):
//    存在时点 = `e.Tick`;`StartTick` 只是**算术输入**(算完成时点)。
//    混用两者会让「载荷说早已点火、信封说尚未发生」的坏事件回溯污染过去的答案。
// ③ **每 actor 至多一条**。合法流里不可能有第二条(`IsBusy` 拒同时两炉),非法流里
//    取**全序键最小**者(= 更早点火的那炉)—— 与 `FurnaceState` 的「同键取先入者」
//    同款防御口径:ADR-008 §二 的「全序无平局」是上游保证,消费方不重复校验它。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>一炉在**本 tick 完成**的事实 —— 18 交给 42(器物状态)与 20(落地)的唯一形状。
    /// <para>⚠️ 本结构是**派生量**,不是状态:它由「`Craft` 流前缀 + 询问 tick」纯函数算出,
    /// 不进 7a 存档(AC-18-17 ②)、不进任何流(ADR-005 事件流唯一真源)。</para>
    /// <para>⚠️ <b>18 不持有落地写权</b>(story-005 §Implementation Notes 1 / Out of Scope):
    /// 本结构只**携带**落地所需的全部整数事实(`OutputInstanceIds` / `ToolCell`),
    /// `DropSpawned` 的发出者是 20。</para>
    /// </summary>
    public readonly struct FurnaceCompletion
    {
        /// <summary>点火玩家 id(ADR-006 Amendment B 机制 A 发号;**不**进全序键)。</summary>
        public readonly int ActorId;

        /// <summary>点火 tick(`CraftPayload.StartTick`)。</summary>
        public readonly long StartTick;

        /// <summary>烘焙时长(`CraftPayload.DurationTicks`;恒 ≥ 1 —— 见
        /// <see cref="FurnaceCompletionSchedule.CollectAt"/> 的时长域先验)。</summary>
        public readonly long DurationTicks;

        /// <summary>配方 ordinal(21a;`CraftPayload.RecipeId`)。</summary>
        public readonly int RecipeId;

        /// <summary>炉所在格(`CraftPayload.ToolCell`)—— 溢出落地时的 `spawn_anchor` 来源。</summary>
        public readonly WorldPos ToolCell;

        /// <summary>点火时**已铸**的产出实例 id(`CraftPayload.OutputInstanceIds`,逐 outputs 行对位)。
        /// <para>⚠️ 本字段是载荷数组的**原样引用**:18 不复制、不重铸、不改写 —— 「无第二铸造点」的
        /// 结构面判据(AC-18-21 的溢出半边)。调用方只读。</para>
        /// </summary>
        public readonly long[] OutputInstanceIds;

        /// <summary>该条 `Craft` 的三流全序键(ADR-008;D-21-28 [甲])。同键 ⇒ 同一条事件。</summary>
        public readonly EventOrderKey At;

        /// <summary>构造。</summary>
        public FurnaceCompletion(int actorId, long startTick, long durationTicks, int recipeId,
                                 WorldPos toolCell, long[] outputInstanceIds, EventOrderKey at)
        {
            ActorId = actorId;
            StartTick = startTick;
            DurationTicks = durationTicks;
            RecipeId = recipeId;
            ToolCell = toolCell;
            OutputInstanceIds = outputInstanceIds;
            At = at;
        }

        /// <summary>完成时点(F-18.3)—— <c>start_tick + duration_ticks</c>,整型加法。
        /// <para>⚠️ 零墙钟、零浮点:与 <see cref="FurnaceOccupancy.CompleteTick"/> 同一算式、
        /// 同一 <c>checked</c> 口径(溢出 = 坏数据,不静默回绕)。</para>
        /// </summary>
        public long CompleteTick => checked(StartTick + DurationTicks);

        /// <inheritdoc />
        public override string ToString() =>
            "Complete(actor " + ActorId + " @ " + StartTick + "+" + DurationTicks
            + " → " + CompleteTick + "; " + (OutputInstanceIds?.Length ?? 0) + " outputs)";
    }

    /// <summary>
    /// 完成时点调度 —— 「**本 tick 有哪些炉到点了**」的纯函数,AC-18-21 的「调度落点」。
    /// <para><b>纯函数族</b>:入参即全部输入,零静态可变状态、零 I/O、零墙钟、零随机。
    /// ⇒ 同一条流 + 同一个 tick ⇒ 逐位相同的答案(重放幂等,AC-18-21 的存档恢复半边)。</para>
    /// <para>⚠️ 本类**不发射 cue、不写流、不结算** —— 它只回答「到点了的是哪些」。发射归
    /// 装配层驱动(`Gameplay.Boot/ProcessingCompletionDriver`,ADR-018 §一「18 是发射方」),
    /// 落地归 20(`InventoryTransaction` 一族)。</para>
    /// </summary>
    public static class FurnaceCompletionSchedule
    {
        /// <summary>收集在 <paramref name="tick"/> **恰好完成**的全部炉(每 actor 至多一条)。</summary>
        /// <param name="events">事件流只读视图(**可传全流** —— 本方法按 `Kind` 自筛,
        /// 与 <c>FurnaceState</c> / <c>InventoryFold.Fold</c> 同款契约)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;`Sim` 够不着字节,这是唯一合法读路径)。</param>
        /// <param name="tick">询问的逻辑 tick(20 Hz ⇒ 50 ms/tick)。</param>
        /// <param name="dest">输出缓冲(**先被清空**;复用同一实例不跨调用累积)。</param>
        /// <returns>本 tick 完成的炉数(= <paramref name="dest"/> 的元素数)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 或
        /// <paramref name="dest"/> 为 null(fail-closed:空解码器若被宽容处理 ⇒ 静默「没有炉起货」)。</exception>
        /// <exception cref="InvalidOperationException">某条 `Craft` 的 `DurationTicks &lt; 1`
        /// —— 21a 数据表硬失败族(规则八:18 只消费,不猜)。<b>不论是否落在前缀内</b>,
        /// 与 <see cref="FurnaceState"/> 同位置同语义(坏数据早报)。</exception>
        /// <remarks>
        /// <para><b>输出次序</b> = 全序键升序(`At`),随后按 `ActorId` 破平 —— 合法流里 `At`
        /// 已唯一,故次序确定;非法流里也不依赖 `List.Sort` 的不稳定性。</para>
        /// </remarks>
        public static int CollectAt(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
                                    long tick, List<FurnaceCompletion> dest)
        {
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder),
                    "未注入 IPayloadDecoder —— 拒绝返回「没有炉起货」(那是完成调度的静默失效)");
            if (dest == null)
                throw new ArgumentNullException(nameof(dest),
                    "未传入输出缓冲 —— 拒绝静默丢弃收集结果");

            dest.Clear();
            if (events == null) return 0;

            // actorId → 该 actor 本 tick 完成的那一炉(全序键最小者胜)。
            // ⚠️ 另存一份出现次序:字典枚举序不保证稳定,而它会影响输出次序。
            var best = new Dictionary<int, FurnaceCompletion>();
            var actors = new List<int>();

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.Craft) continue;

                CraftPayload p = decoder.Decode<CraftPayload>(e.Kind, e.Payload);

                // 时长域先验 —— 不论是否落在前缀内(与 FurnaceState.TryGetLatest 同位置)。
                if (p.DurationTicks < 1)
                    throw new InvalidOperationException(
                        $"[炮制] actor {p.ActorId} 的 Craft.duration_ticks = {p.DurationTicks} < 1 " +
                        "—— 21a 数据表硬失败族(规则八:duration_ticks 归 21a,18 只消费)");

                if (e.Tick > tick) continue;        // 前缀:尚未发生(存在时点 = envelope)

                // 判据 ① 等号,不是区间。
                if (checked(p.StartTick + p.DurationTicks) != tick) continue;

                EventOrderKey at = EventOrder.KeyOf(e);
                var cand = new FurnaceCompletion(p.ActorId, p.StartTick, p.DurationTicks,
                                                 p.RecipeId, p.ToolCell, p.OutputInstanceIds, at);

                if (best.TryGetValue(p.ActorId, out FurnaceCompletion prev))
                {
                    // 判据 ③ 每 actor 至多一条:全序键更小者(= 更早点火的那炉)胜。
                    // 合法流里本条分支不可达(IsBusy 拒同时两炉);见文件头。
                    if (at.CompareTo(prev.At) < 0) best[p.ActorId] = cand;
                }
                else
                {
                    best[p.ActorId] = cand;
                    actors.Add(p.ActorId);
                }
            }

            for (int i = 0; i < actors.Count; i++)
                dest.Add(best[actors[i]]);

            // 输出次序确定化:全序键升序,`ActorId` 破平(合法流里第二键不参与)。
            dest.Sort(CompareCompletions);

            return dest.Count;
        }

        /// <summary>输出次序的比较器(**方法组**,不是 lambda)。
        /// <para>⚠️ 刻意不用 lambda:编译器会为它生成一个 `&lt;&gt;c` 闭包类,而该类带
        /// `[Serializable]`,会撞 `test_ac1817a_processingNamespace_hasNoSerializableTypes`
        /// —— 那条门查的是「18 命名空间出现可存类型」,一个排序闭包不该成为它的假阳性,
        /// 但也不该给它开白名单(开白名单 = 门开始认识编译器内部命名)。</para>
        /// </summary>
        private static int CompareCompletions(FurnaceCompletion a, FurnaceCompletion b)
        {
            int byKey = a.At.CompareTo(b.At);
            return byKey != 0 ? byKey : a.ActorId.CompareTo(b.ActorId);
        }
    }
}
