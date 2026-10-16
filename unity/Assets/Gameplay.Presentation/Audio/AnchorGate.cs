// 权威来源:production/epics/audio-system/story-007-net-divergence-remote-derivation.md
//   (AC-44-D1 位置来源检查 · AC-44-D10 锚点纪律)
// GDD:design/gdd/audio-system.md §Edge Cases 联机与空间化(「远端玩家在音频触发瞬间断线 ⇒
//   44 使用最近一次 IPositionalChannel 位置快照;断线后 cue 静默过期,不崩溃」·
//   「病人(非玩家)在远端客户端发声 ⇒ 按 ActorId 读表」)
// ADR-001 §一之二(第二 QoS latest-value 表 + 消费纪律 1/2/3)· §一之三 裁决二
// ADR-028 ③(位置真源 = 只读锚点,禁反推逻辑格 / 禁回写 / 禁做判定)
//
// ⚠️ **本类 = 44 对 IPositionalChannel 的唯一消费点**(AC-44-D1)。
//    两段式装配的「注入半」在此:派生面(`CueEmitDeriver`)产出 `Cell = default` 的 cue,
//    本类在其进入渲染前回答「现在有没有可用锚点」—— 远端 ← `IPositionalChannel`。
//    ⇒ **派生面零 IPositionalChannel 引用**(由 IL 扫描硬约束),位置面持它 —— 二者分层,
//    这正是 AC-44-D1「44 不自建位置通道 / 只读取锚点」的结构表达。
//
// ⚠️ **陈旧检查属「渲染路由」面,不属「发不发 cue」面**(AC-44-D1 逐字:「增益/声像/低通/
//    距离排序 = 渲染路由可读,`ServerTick` 陈旧检查 + 允许缺省锚点;**「发不发 cue」= 判定,禁**」)。
//    ⇒ `Dispatch` 的判别**只读锚点存在性**(`TryReadLatest` 是否为真),**不读** `Cell` 值、
//    **不读** `ServerTick`。`IsStale` 是**独立的纯谓词**,供渲染路由侧(空间化 / rank_key)使用。
//    ⚠️ 这一分工是刻意的:若把陈旧检查塞进 `Dispatch`,陈旧锚点会被判「无锚点」⇒ 一次性 cue
//    被**丢弃**、循环 cue 被**挂起** —— 而 GDD 明确要求断线时「使用最近一次位置快照」
//    (丢弃 = 违 GDD)。陈旧 ≠ 缺席。
//
// ⚠️ **锚点缺席语义**(GDD Edge Cases「锚点时序竞争」条):一次性 cue 锚点缺席 ⇒ **丢弃不补播**
//    (与断线静默过期同语义);循环 cue 锚点缺席 ⇒ **挂起**至锚点到达。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Presentation.Audio
{
    /// <summary>锚点门的判别结果(AC-44-D10 谓词面)。</summary>
    public enum GateDecision
    {
        /// <summary>锚点在位 ⇒ 发出(可进渲染路由)。</summary>
        Emitted,
        /// <summary>一次性 cue 锚点缺席 ⇒ 丢弃(不补播 —— 与断线静默过期同语义)。</summary>
        Dropped,
        /// <summary>循环 cue 锚点缺席 ⇒ 挂起(等锚点到达再发,不静默丢)。</summary>
        Pending,
    }

    /// <summary>锚点门(AC-44-D1 / AC-44-D10 的生产实现)。
    /// <para>职责:① 按 `ActorId` 读 `IPositionalChannel` 的最新锚点(**只读存在性**);
    /// ② 缺席时的两分支语义(一次性丢弃 / 循环挂起);③ 挂起队列的补发。</para>
    /// <para>⚠️ **不做渲染路由判定**(增益 / 声像 / 低通 / 距离排序归空间化),也**不读
    /// `ServerTick` 做陈旧检查** —— 后者是渲染路由的准入条件,由
    /// <see cref="IsStale"/> 供其调用(见文件头注)。</para>
    /// <example>
    /// var gate = new AnchorGate(channel);
    /// if (gate.Dispatch(cue) == GateDecision.Emitted) { /* 进空间化 */ }
    /// gate.Update();   // 挂起队列中已获锚点的补发
    /// </example></summary>
    public sealed class AnchorGate
    {
        private readonly IPositionalChannel _channel;
        private readonly List<AudioCueDto> _pending = new List<AudioCueDto>();
        private readonly List<int> _pendingActors = new List<int>();

        /// <summary>构造锚点门。</summary>
        /// <param name="channel">第二 QoS 位置表(44 的**唯一**位置来源;null ⇒
        /// <see cref="ArgumentNullException"/>)。</param>
        public AnchorGate(IPositionalChannel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>挂起队列长度(测试与调试读)。</summary>
        public int PendingCount => _pending.Count;

        /// <summary>锚点缺席谓词 —— **只读存在性**(消费纪律 2 / AC-44-D1:「发不发 cue」的判别
        /// 禁读锚点值)。</summary>
        /// <param name="actorId">声源实体 id。</param>
        /// <returns>该实体当前是否有锚点行。</returns>
        public bool HasAnchor(int actorId) => _channel.TryReadLatest(actorId, out _);

        /// <summary>派发一条 cue:判别**只读锚点存在性**(不读值、不判陈旧)。
        /// <para>一次性 cue 无锚点 ⇒ <see cref="GateDecision.Dropped"/>(丢弃不补播);
        /// 循环 cue 无锚点 ⇒ <see cref="GateDecision.Pending"/>(入挂起队列);
        /// 有锚点 ⇒ <see cref="GateDecision.Emitted"/>。</para></summary>
        /// <param name="cue">待派发的 cue(`Source` = 声源实体 id)。</param>
        /// <returns>判别结果。</returns>
        public GateDecision Dispatch(in AudioCueDto cue)
        {
            int actorId = cue.Source;
            if (HasAnchor(actorId)) return GateDecision.Emitted;

            if (cue.Looped)
            {
                // ⚠️ 同源重复挂起 ⇒ 以最后一次为准(不叠两条 —— GDD「防两层呼吸」同族纪律)。
                RemovePending(actorId);
                _pending.Add(cue);
                _pendingActors.Add(actorId);
                return GateDecision.Pending;
            }
            return GateDecision.Dropped;
        }

        /// <summary>推进一帧:把挂起队列中**已获锚点**的循环 cue 补发。
        /// <para>⚠️ 仍无锚点的**继续挂起**(不静默丢)。</para></summary>
        /// <returns>本帧补发条数(0 = 无补发)。</returns>
        public int Update()
        {
            if (_pending.Count == 0) return 0;

            int emitted = 0;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (!HasAnchor(_pendingActors[i])) continue;
                _pending.RemoveAt(i);
                _pendingActors.RemoveAt(i);
                emitted++;
            }
            return emitted;
        }

        /// <summary>陈旧谓词(**渲染路由面** —— ADR-001 §一之二 消费纪律 2):
        /// candidate **严格早于** latest ⇒ 陈旧。
        /// <para>⚠️ 等值 / 更新 = **不陈旧**(单调序:只拒旧不拒新 —— 等值拒读会误杀本机帧最新值)。</para>
        /// <para>⚠️ **调用方 = 渲染路由**(增益 / 声像 / 低通 / 距离排序),**不是**
        /// <see cref="Dispatch"/> —— 陈旧锚点仍可用(「使用最近一次位置快照」,GDD Edge Cases)。</para></summary>
        /// <param name="candidateTick">候选锚点的 ServerTick。</param>
        /// <param name="latestTick">已见最新主机 tick。</param>
        /// <returns>是否陈旧。</returns>
        public static bool IsStale(uint candidateTick, uint latestTick)
            => candidateTick < latestTick;

        private void RemovePending(int actorId)
        {
            for (int i = _pendingActors.Count - 1; i >= 0; i--)
            {
                if (_pendingActors[i] != actorId) continue;
                _pending.RemoveAt(i);
                _pendingActors.RemoveAt(i);
            }
        }
    }
}
