// AC-7a-20 事件触发半边 —— 锚点集中观察触发器(TD-persist-04 动作① 裁定轮 2026-10-25)。
//
// 权威来源:
//   design/gdd/persistence-service.md 规则六 —— 锚点白名单 `EVENT_CHECKPOINT_ANCHORS`,
//     「事件触发 = 调度策略,非第四存档入口」;
//   AC-7a-20 —— 「WHEN 任意白名单事件被提交,THEN 在该事件的 Step 边界触发一次 checkpoint」;
//   docs/tech-debt-register.md TD-persist-04 裁定块(2026-10-25 四裁定,用户全取推荐):
//     ① 接线形态 = **集中观察** —— 驱动方(BootRoot)在 tick 边沿把「自上次 drain 以来
//       落流的事件」交给本类扫白名单;**写者零改动**(8 / 37 / 52 不感知 checkpoint);
//       恰落 AC 字面「该事件的 Step 边界」—— 写者直调会在 Step 中途组版,读到半个 tick 的流。
//     ② 成员 → Kind 映射:脉案落笔 = `JudgmentRecorded` · 病例结案 = `CaseClosed` ·
//       出诊启动 = `HouseCallStarted`(`DefaultAnchors`,与 `EVENT_CHECKPOINT_ANCHORS`
//       registry 条目一一对应;白名单只增不删 —— 增员 = 改本表 + registry 同批)。
//     ③ **复用带瞬时源门的 Checkpoint 入口** —— 本类先问门(不安全 ⇒ 该次不触发,
//       由定时兜底顺延在 ≤ 一个间隔内补上);门仍是闭包内 `Checkpoint` 的权威判定,
//       本类的预问只是「跳过而非抛」的表达(同一 `INarrativeContextQuery` 实例,瞬时量重答)。
//     ④ **同 tick 多锚点 = 一次** checkpoint(Step 边界语义);**成功 ⇒ 经 `onFired` 把
//       兜底计数清零**(兜底只管「事件长期缺席」;锚点刚存过 ⇒ 窗口从现在重起,
//       「崩溃损失 ≤ 一个间隔」以最近一次存档起算,无论哪半边)。
//
// ⚠️ 本类是**调度策略**,不是存档入口(与 `CheckpointFallback` 同款纪律):触发的是
//    调用方给的闭包,本类不认识 `SaveService` / `SaveSlot`、不写任何字节。
// ⚠️ `checkpoint` 是 **`Func<bool>`**(非 `Action`):Sim.Codec `noEngineReferences`
//    ⇒ LogError 只能落 Boot 侧闭包;闭包返回 **false = 存档未发生**(异常已记日志),
//    此时本类**不**调用 `onFired` —— 否则「没存成 + 兜底窗口重起」会把「崩溃损失
//    ≤ 一个间隔」放宽到近 2×(与 GDD `:137` 不变量相抵)。
// ⚠️ 游标与流替换(`EventStream.Restore`,读档):`Drain` 尾部把游标**无条件对齐到流尾**
//    —— 换流(变短)后首次 drain 即自愈,载入的历史锚点**不触发**(它们已在存档里)。
//    ⚠️ **已知缝(如实登记)**:restore 与首次 drain 之间落流的新事件,若其索引
//    落在旧游标之下,会被静默跳过(钳制与否都一样 —— 2026-10-25 AT4 证伪轮实测钳制冗余,已删)。
//    ⇒ 生产 Load 接线落地时,**读档路径必须调 `ResetCursor()`**(义务登记于证据件)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Codec
{
    /// <summary>
    /// 事件触发 checkpoint 的锚点观察器(AC-7a-20 的「事件触发为主」半边)。
    /// <para>驱动方在每个 Step 边界(tick 边沿)调 <see cref="Drain"/>,把自上次 drain
    /// 以来落流的事件交给它;白名单命中且瞬时源安全 ⇒ 触发一次 checkpoint 闭包,
    /// 成功后经 <c>onFired</c> 通知兜底清零窗口。游标推进,**同一锚点不重复触发**。</para>
    /// </summary>
    public sealed class AnchorCheckpointTrigger
    {
        /// <summary>
        /// 锚点白名单(`EVENT_CHECKPOINT_ANCHORS`,GDD 规则六;2026-09-16 玩家裁定,
        /// **只增不删**)。成员 → Kind 映射 = 2026-10-25 TD-persist-04 裁定②:
        /// 脉案落笔 = <see cref="EventKind.JudgmentRecorded"/>(「落笔」字面 + 支柱一
        /// 不可逆裁决时刻)· 病例结案 = <see cref="EventKind.CaseClosed"/> ·
        /// 出诊启动 = <see cref="EventKind.HouseCallStarted"/>。
        /// </summary>
        public static readonly IReadOnlyList<EventKind> DefaultAnchors = new[]
        {
            EventKind.JudgmentRecorded,
            EventKind.CaseClosed,
            EventKind.HouseCallStarted,
        };

        private readonly Func<bool> _checkpoint;
        private readonly INarrativeContextQuery _context;
        private readonly IReadOnlyList<EventKind> _anchors;
        private readonly Action _onFired;
        private int _cursor;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="checkpoint">
        /// 触发闭包(装配根接「<c>Checkpoint(下一个可写槽位)</c>」;**带瞬时源门**)。
        /// 返回 <c>true</c> = 存档动作完成;<c>false</c> = 未发生(异常已由闭包记日志)。
        /// </param>
        /// <param name="context">瞬时源(规则十四「一个量」—— 与闭包内门同一实例)。</param>
        /// <param name="anchors">白名单(生产 = <see cref="DefaultAnchors"/>;测试注入小集)。**非空**。</param>
        /// <param name="onFired">checkpoint 成功后的通知(生产 = 兜底计数清零);可 null。</param>
        /// <exception cref="ArgumentNullException">任一必填驱动源为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="anchors"/> 为空
        /// (空白名单 = 事件触发半边恒哑 = 配置错误,必须构造期暴露)。</exception>
        public AnchorCheckpointTrigger(Func<bool> checkpoint,
                                        INarrativeContextQuery context,
                                        IReadOnlyList<EventKind> anchors,
                                        Action onFired = null)
        {
            _checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (anchors == null)
                throw new ArgumentNullException(nameof(anchors));
            if (anchors.Count == 0)
                throw new ArgumentException(
                    "锚点白名单为空 —— 事件触发半边恒哑(只剩定时兜底 = 退回首轮粒度洞)," +
                    "这是配置错误,不要构造一个永不触发的观察器", nameof(anchors));
            _anchors = anchors;
            _onFired = onFired;
        }

        /// <summary>
        /// 游标归零(读档换流后由 Load 接线路径调用;见头注「游标与流替换」)。
        /// </summary>
        public void ResetCursor() => _cursor = 0;

        /// <summary>
        /// 推进观察位:扫 <paramref name="events"/> 自上次 drain 以来的新尾段,
        /// 白名单命中且瞬时源安全 ⇒ 触发一次(同批多锚点仍一次)。游标推进到流尾
        /// —— **同一锚点不重复触发**;门不安全 ⇒ 该次不触发(锚点已消费,由定时兜底
        /// 在 ≤ 一个间隔内补上,承裁定③)。
        /// </summary>
        /// <param name="events">全序事件流(通常 = <c>EventStream.Events</c>;append-only)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="events"/> 为 null。</exception>
        public void Drain(IReadOnlyList<SimEvent> events)
        {
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            bool anchorSeen = false;
            for (int i = _cursor; i < events.Count; i++)
            {
                for (int k = 0; k < _anchors.Count; k++)
                {
                    if (events[i].Kind == _anchors[k])
                    {
                        anchorSeen = true;
                        break;
                    }
                }
                if (anchorSeen)
                    break;
            }
            _cursor = events.Count;   // 无条件对齐流尾:换流(变短)后首次 drain 即自愈(头注)

            if (!anchorSeen)
                return;
            if (!_context.InNarrativeContext())
                return;   // 裁定③:不安全 ⇒ 该次不触发,兜底顺延补上

            if (_checkpoint())
                _onFired?.Invoke();   // 裁定④:成功 ⇒ 兜底计数清零;false ⇒ 不清(头注 ⚠️)
        }
    }
}
