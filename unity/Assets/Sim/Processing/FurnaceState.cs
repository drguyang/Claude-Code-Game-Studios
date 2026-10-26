// S20-9 · processing story-004 —— 炉状态 = `Craft` 流前缀的纯函数(**无进程态**)
//
// 权威来源:
//   design/gdd/processing.md
//     · 规则八(:300-370)时序 —— 「发起即落流、完成 = 派生」;并发模型表(每玩家单炉 / 不可取消 / 跨玩家允许)
//     · F-18.3(:458-461)完成时点 `CompleteTick(craft) = craft.start_tick + recipe.duration_ticks`
//     · 规则七(:255-277)点火事务五步;R-18-D(:279-297)拒绝在**时点**
//     · AC-18-13 / AC-18-17 / AC-18-23 / AC-18-12
//   ADR-005 —— 主机唯一 Step/Append + 事件流唯一真源 ⇒「进行中」= 派生量,**不是**状态
//   ADR-008 §跨流全序键 —— `(Tick, StreamPriority, Patient, Seq)`;D-21-28 裁定 [甲] 吸收 `(Tick, ActorId, Seq)`
//   ADR-010 §三 —— 7a 存档 = 头部 + 三流 + 快照,**18 无段**
//   ADR-031 —— 载荷解码唯一路径 = `IPayloadDecoder`(`Sim` 物理上够不着字节)
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么本文件**没有**任何字段 / 缓存 / 单例
// ─────────────────────────────────────────────────────────────────────────────
// AC-18-17 的「无进程态」不是靠纪律守的,是靠**类型面**守的:本类全部成员为 `static` 纯函数,
// 入参即全部输入 ⇒ 「进行中的炉子」**在类型上不可表达**。带缓存的写法会把「流前缀」以外的
// 东西(上次查询的 tick / 上次结果)带进求值,那就是第二真源。
// ⇒ story 卡「内存缓存允许但键须纯 (流前缀, tick)」的许可**不被使用** —— 不用的理由不是
//   性能不重要,而是「缓存键纯度」需要一条夹具去钉,而**没有缓存就没有这条风险**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 「最新一条 Craft」的定序口径
// ─────────────────────────────────────────────────────────────────────────────
// 取该 actor 全部**已点火**(`StartTick <= 询问 tick`)的 `Craft` 中**全序键最大**的那条。
// 用全序键而不是 `StartTick` 排序,是因为 D-21-28 已裁「同 tick 定序由三流键吸收」——
// 另排一次 = 同一事实的第二份判据。合法流里两者单调一致(单炉 ⇒ 后点火的 start 必更大),
// 非法流里全序键仍给出**确定**答案(而不是靠列表顺序)。
//
// ⚠️ **前缀与定序都只用 envelope tick**(2026-10-26 评审 W1 订正)。
//    前一版前缀用载荷 `StartTick`、定序用 envelope `e.Tick` —— **两个时点来源**。
//    文件头当时自称「让判据只依赖一个量」,而代码实际依赖了两个,自述与实现矛盾。
//    后果是真实的:**非法流**里 `StartTick ≤ tick < e.Tick` 的那条 `Craft`(载荷说早已点火、
//    信封说尚未发生)会被计入「截至 tick 的前缀」⇒ **一个在该 tick 还不存在的炉子
//    回溯污染了过去时点的答案**。合法流里两者相等(规则八:点火同一 tick 发 `Craft`),
//    所以任何只在合法流上跑的夹具都看不见它 —— 这正是要按「前缀」这个词的
//    **as-of 语义**取值、而不是按「F-18.3 的算术输入」取值的原因:
//    `StartTick` 是**算术输入**(算 `CompleteTick`),`e.Tick` 是**存在时点**(判前缀)。
//    ⇒ 两者各司其职,不再混用。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>`Craft` 流前缀里某 actor 的「最后一炉」—— 规则八的派生量,**不是**状态。
    /// <para>住 `Sim` 且全字段均为整数 / 纯结构 ⇒ 可自由复制、比较、序列化进**测试**夹具,
    /// 但**不得**进 7a 存档(AC-18-17 ②:7a 段清单不含任何 18 记录)。</para>
    /// </summary>
    public readonly struct FurnaceOccupancy
    {
        /// <summary>是否存在「已点火」的炉(为 false 时其余字段无意义)。</summary>
        public readonly bool Exists;

        /// <summary>点火 tick(`CraftPayload.StartTick`)。</summary>
        public readonly long StartTick;

        /// <summary>烘焙时长(`CraftPayload.DurationTicks`;恒 ≥ 1)。</summary>
        public readonly long DurationTicks;

        /// <summary>该条 `Craft` 的三流全序键(ADR-008;D-21-28 [甲])。</summary>
        public readonly EventOrderKey At;

        /// <summary>构造。</summary>
        public FurnaceOccupancy(bool exists, long startTick, long durationTicks, EventOrderKey at)
        {
            Exists = exists; StartTick = startTick; DurationTicks = durationTicks; At = at;
        }

        /// <summary>完成时点(F-18.3)—— <c>start_tick + duration_ticks</c>,整型加法。
        /// <para>⚠️ 零墙钟、零浮点:本条属性就是 AC-18-13 的 IL 扫描靶之一。</para>
        /// </summary>
        public long CompleteTick => checked(StartTick + DurationTicks);

        /// <inheritdoc />
        public override string ToString() =>
            "Furnace(" + (Exists ? "actor-craft @ " + StartTick + "+" + DurationTicks
                                  : "none") + ")";
    }

    /// <summary>
    /// 炉状态查询 —— 规则八时序的**全部**机器可断言面(AC-18-13 / 17 / 23)。
    /// <para><b>纯函数族</b>:入参即全部输入,零静态可变状态、零 I/O、零墙钟。
    /// ⇒ 同一个事件流 + 同一个 tick ⇒ 逐位相同的答案(AC-18-17 ③ 的机制半边)。</para>
    /// <para><b>无取消 API</b>(GDD 规则八「不允许取消」):本类刻意**不提供** `Cancel` ——
    /// 禁「预留但不断言」的灰色接口(YAGNI)。AC-18-23 的判据即拒绝路径覆盖。</para>
    /// </summary>
    public static class FurnaceState
    {
        /// <summary>取某 actor 在 <paramref name="tick"/> 之前(含)已点火的**最后一炉**。</summary>
        /// <param name="events">事件流只读视图(**可传全流** —— 本方法按 `Kind` 自筛;
        /// 与 <c>InventoryFold.Fold</c> / <c>ActiveHouseCallQuery.Evaluate</c> 同款契约)。</param>
        /// <param name="decoder">载荷解码器(ADR-031;`Sim` 够不着字节,这是唯一合法读路径)。</param>
        /// <param name="actorId">玩家 id(ADR-006 Amendment B 机制 A 发号;**不**进全序键)。</param>
        /// <param name="tick">询问的逻辑 tick(20 Hz ⇒ 50 ms/tick)。</param>
        /// <param name="occupancy">最后一炉;<paramref name="actorId"/> 在此时点前无炉时为
        /// <c>default</c>(<see cref="FurnaceOccupancy.Exists"/> = false)。</param>
        /// <returns>该 actor 在此时点前是否**至少点过一次火**。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 为 null
        /// (fail-closed:空解码器若被宽容处理 ⇒ 静默「谁都不忙」)。</exception>
        /// <exception cref="InvalidOperationException">某条 `Craft` 的
        /// <c>DurationTicks &lt; 1</c> —— 21a 数据表硬失败族(规则八:18 只消费,不猜)。</exception>
        public static bool TryGetLatest(
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            int actorId,
            long tick,
            out FurnaceOccupancy occupancy)
        {
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder),
                    "未注入 IPayloadDecoder —— 拒绝返回「谁都不忙」(那是进行中炉子的静默失效)");

            occupancy = default;
            if (events == null) return false;

            bool found = false;
            EventOrderKey best = default;
            FurnaceOccupancy bestOcc = default;

            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.Craft) continue;

                CraftPayload p = decoder.Decode<CraftPayload>(e.Kind, e.Payload);
                if (p.ActorId != actorId) continue;

                // 先验时长域(不论是否落在前缀内 —— 坏数据早报,不因询问 tick 靠前而漏)。
                if (p.DurationTicks < 1)
                    throw new InvalidOperationException(
                        $"[炮制] actor {p.ActorId} 的 Craft.duration_ticks = {p.DurationTicks} < 1 " +
                        "—— 21a 数据表硬失败族(规则八:duration_ticks 归 21a,18 只消费)");

                if (e.Tick > tick) continue;   // 前缀:尚未发生(存在时点 = envelope)

                EventOrderKey at = EventOrder.KeyOf(e);
                if (found && at.CompareTo(best) <= 0) continue;

                found = true;
                best = at;
                bestOcc = new FurnaceOccupancy(true, p.StartTick, p.DurationTicks, at);
            }

            occupancy = bestOcc;
            return found;
        }

        /// <summary>该 actor 在 <paramref name="tick"/> 是否**正在烧**(规则八 · 单炉判据)。
        /// <para><b>谓词</b>:<c>IsBusy ⟺ ∃ 已点火的 Craft ∧ tick &lt; StartTick + DurationTicks</c></para>
        /// <para>⚠️ <b>完成当拍不忙</b>(<c>&lt;</c> 而非 <c>≤</c>)—— <b>这是本实现的选择,不是 GDD 判据</b>
        /// (2026-10-26 评审 W2):`processing.md:308` 只给 `CompleteTick = start + duration`,
        /// `:480` 只写「已有一炉在烧 ⇒ 拒绝」,**全文未定义「在烧」的开闭区间**。
        /// <para>取半开区间 `[start, start+duration)` 的理由是自洽(与 F-18.3「完成 = 时间到达」一致),
        /// 且使「完成 tick 当拍可立即再点火」成立;但它是**可改的口径**,不是被 AC 钉住的行为。
        /// 若产品要闭区间,改这里的 `<` 即可 —— 不影响任何 AC。</para>
        /// </summary>
        /// <param name="events">事件流(可为全流)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="actorId">玩家 id。</param>
        /// <param name="tick">询问的逻辑 tick。</param>
        /// <returns>正在烧 ⇒ true。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><c>DurationTicks &lt; 1</c>。</exception>
        public static bool IsBusy(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
                                  int actorId, long tick)
        {
            if (!TryGetLatest(events, decoder, actorId, tick, out FurnaceOccupancy occ))
                return false;
            return tick < occ.CompleteTick;
        }

        /// <summary>该 actor 最后一炉的完成时点(F-18.3)—— Story 005 的调度消费本条谓词。
        /// <para>⚠️ 返回的是「最后一次**点火**的完成时点」,**不**表示仍在烧 ——
        /// 还在烧与否须另问 <see cref="IsBusy"/>。两者分开是因为调度需要「何时到点」,
        /// 而单炉裁决需要「此刻忙不忙」;合成一个返回值会逼调用方做二次拆解。</para>
        /// </summary>
        /// <param name="events">事件流(可为全流)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="actorId">玩家 id。</param>
        /// <param name="tick">询问的逻辑 tick(前缀上界)。</param>
        /// <param name="completeTick">完成时点(无炉时为 0,无意义)。</param>
        /// <returns>该 actor 在此时点前是否至少点过一次火。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><c>DurationTicks &lt; 1</c>。</exception>
        public static bool TryCompleteTickOf(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
                                             int actorId, long tick, out long completeTick)
        {
            if (!TryGetLatest(events, decoder, actorId, tick, out FurnaceOccupancy occ))
            {
                completeTick = 0L;
                return false;
            }
            completeTick = occ.CompleteTick;
            return true;
        }

        /// <summary>此时点**正在烧**的玩家数(AC-18-23 的跨玩家并发判据面)。
        /// <para>⚠️ 逐 actor 委托 <see cref="IsBusy"/>,**不**另写一份「忙」的判据 ——
        /// 两份判据会在某次修订里分叉(本项目已抓过多次的「第二真源」形态)。</para>
        /// </summary>
        /// <param name="events">事件流(可为全流)。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="tick">询问的逻辑 tick。</param>
        /// <returns>正在烧的玩家数(无流 / 无炉 ⇒ 0)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><c>DurationTicks &lt; 1</c>。</exception>
        public static int BusyActorCount(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
                                         long tick)
        {
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder),
                    "未注入 IPayloadDecoder —— 拒绝返回 0(那是「没人在烧」的静默失效)");
            if (events == null) return 0;

            var actors = new HashSet<int>();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.Craft) continue;

                CraftPayload p = decoder.Decode<CraftPayload>(e.Kind, e.Payload);
                actors.Add(p.ActorId);
            }

            int busy = 0;
            foreach (int actor in actors)
            {
                if (IsBusy(events, decoder, actor, tick)) busy++;
            }
            return busy;
        }
    }
}
