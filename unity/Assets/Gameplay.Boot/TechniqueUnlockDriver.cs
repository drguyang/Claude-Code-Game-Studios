// 技法解锁的**触发驱动** —— 扫事件流,把「奇遇 / Boss / 病例」翻成解锁(2026-11-09 · T9)
//
// 权威:`design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2
//       · ADR-043 §D2(写者 = 30;本驱动只是**调用方**,经 `TechniqueUnlockWriter` 写)
//       · ADR-043 §D6(同一 (actor, technique) 至多一条 —— 去重在写者里,本驱动不重复实现)
//       · ADR-009 §一(三问判据)· ADR-016 §一(决策是派生态:本驱动不进流、不存档)
//
// ── 三个「为什么」──────────────────────────────────────────────────────────
// ① **为什么是「扫流」而不是「各处调用」**:奇遇 / Boss / 病例分属 52 / 6 / 37 三个系统,
//    在三处各插一次 `TryUnlock` = 三份「什么算解锁」的判据,且每一处都要知道技法 id 空间。
//    扫流则**只有一份判据**,且**读档后自动重放**(见 ②)。
// ② **为什么零跨 tick 态(每轮全量扫)**:承 AC-53-06 的同款理由。带游标就要处理
//    `EventStream.Restore`(读档是**整表替换**)后的游标失配 —— 而那是静默的:
//    游标落在旧流的某个位置,替换后要么跳过一段(漏解锁)、要么越界(不扫)。
//    全量扫 + 写者侧 `IsUnlocked` 去重 ⇒ 幂等、读档后自愈、无游标可失配。
//    代价被规则表**前置**掉:`_rules.Count == 0` ⇒ 直接 return,今日零开销。
// ③ **为什么 actor id 走构造参数而不是可后绑的缝**:与 `TechniqueUnlockBootstrap.Ensure`
//    同格 —— 本驱动的构造点就在**玩家 id 已铸造**之后(`BootRoot` 第 4.4b 步)。
//    给一个「未绑定 ⇒ no-op」的中间态,等于把「忘了绑」变成静默零解锁。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 每个 tick 边沿扫一遍事件流,命中规则即交 <see cref="TechniqueUnlockWriter"/> 解锁。
    /// </summary>
    /// <remarks>
    /// <para><b>单向</b>:只读事件流,只经写者写;不持游戏状态(承 ADR-013 §9 C3 /
    /// ADR-018 §一 的呈现层纪律,本类虽在 Boot 但同构)。</para>
    /// <para><b>幂等</b>:同一份流扫 N 遍,第 1 遍之后写者恒返回 <c>false</c>(D6 去重)
    /// ⇒ <see cref="OnTickEdge"/> 的返回值 = 「**本 tick 新解锁的条数**」,不是「命中条数」。</para>
    /// <para><b>遇抛即终止本 tick</b>:不解码不了就吞。载荷缺失 / 损坏是流或池的故障
    /// (`IPayloadDecoder` 用异常区分「查无」与「损坏」),吞掉会让「流坏了」表现成
    /// 「这局没学到东西」—— 与 ADR-036 的 `ConsequenceDriver` 同口径。</para>
    /// </remarks>
    public sealed class TechniqueUnlockDriver
    {
        private readonly EventStream _stream;
        private readonly IPayloadDecoder _decoder;
        private readonly IReadOnlyList<TechniqueUnlockRule> _rules;
        private readonly TechniqueUnlockWriter _writer;
        private readonly int _actorId;

        /// <summary>构造驱动。</summary>
        /// <param name="stream">事件流(**读面**:`Events`;生产 = 主机唯一流实例)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点)。</param>
        /// <param name="rules">规则集(生产 = <see cref="TechniqueUnlockRules.Default"/>)。
        /// 空集合法 —— 表示「本局没有配任何解锁触发」,与「驱动没接上」的区别在于
        /// 后者在装配日志里会显式告警。</param>
        /// <param name="writer">解锁写者(唯一 Append 落点;D6 去重与闭集校验都在它里面)。</param>
        /// <param name="actorId">学习者 = 玩家 id(<see cref="IIdAuthority"/> 机制 A 发号)。</param>
        /// <exception cref="ArgumentNullException">任一必需依赖为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="actorId"/> 为负。</exception>
        public TechniqueUnlockDriver(EventStream stream, IPayloadDecoder decoder,
                                     IReadOnlyList<TechniqueUnlockRule> rules,
                                     TechniqueUnlockWriter writer, int actorId)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            if (actorId < 0)
                throw new ArgumentOutOfRangeException(nameof(actorId), actorId,
                    "玩家 id 不得为负(-1 是 PatientId.None,会写出世界级事件)");
            _actorId = actorId;
        }

        /// <summary>本驱动认领的规则条数(装配期日志与夹具的观察面)。</summary>
        public int RuleCount => _rules.Count;

        /// <summary>
        /// 一个 tick 边沿的全量扫描。
        /// </summary>
        /// <param name="tick">本 tick —— **写入一律用它**,不用被扫事件自己的 `Tick`。
        /// <para>⚠️ 原因不是风格:源事件可能落在很早的 tick(读档重放),而
        /// <c>EventStream.Append</c> 的单调性判据会因「Tick 回退」直接抛(BCD-码-3)。
        /// 用当前 tick 追加 ⇒ 永远单调非降。</para></param>
        /// <returns>本 tick **新**写出的解锁事件条数(已解锁的不计)。</returns>
        public int OnTickEdge(long tick)
        {
            // ── 零规则 ⇒ 零扫描。今日生产即走这一条(规则表为空)。──
            if (_rules.Count == 0) return 0;

            IReadOnlyList<SimEvent> events = _stream.Events;

            // ⚠️ 快照**条数**而不是持有游标:本方法内会向同一条流追加 `TechniqueUnlocked`
            //    (`Append` 落尾),不快照就会在遍历中途看到自己刚写的事件。
            //    它们不命中任何规则,但「读到本 tick 自己写的行」会让边界难以推理。
            int count = events.Count;
            int written = 0;

            for (int i = 0; i < count; i++)
            {
                SimEvent e = events[i];
                switch (e.Kind)
                {
                    case EventKind.PoiStateChanged:
                    {
                        PoiStateChangedPayload p =
                            _decoder.Decode<PoiStateChangedPayload>(e.Kind, e.Payload);
                        for (int r = 0; r < _rules.Count; r++)
                        {
                            TechniqueUnlockRule rule = _rules[r];
                            if (rule.SourceKind != TechniqueUnlockSourceKind.Poi) continue;
                            if (rule.SourceId != p.PoiId) continue;
                            if (rule.RequiredState != p.NewState) continue;
                            if (_writer.TryUnlock(_actorId, rule.TechniqueId, rule.SourceId, tick))
                                written++;
                        }
                        break;
                    }

                    case EventKind.EventArrived:
                    {
                        EventArrivedPayload p =
                            _decoder.Decode<EventArrivedPayload>(e.Kind, e.Payload);
                        for (int r = 0; r < _rules.Count; r++)
                        {
                            TechniqueUnlockRule rule = _rules[r];
                            if (rule.SourceKind != TechniqueUnlockSourceKind.Event) continue;
                            if (rule.SourceId != p.EventKey) continue;
                            if (p.Tier < rule.MinTier) continue;
                            if (_writer.TryUnlock(_actorId, rule.TechniqueId, rule.SourceId, tick))
                                written++;
                        }
                        break;
                    }

                    case EventKind.CaseClosed:
                    {
                        CaseClosedPayload p =
                            _decoder.Decode<CaseClosedPayload>(e.Kind, e.Payload);

                        // 未处置 ⇒ 不触发。见 `TechniqueUnlockRule.ForCase` 的 remarks:
                        // 这不是可选行为,故写死在这里而不是做成规则字段。
                        if (!p.Treated) break;

                        for (int r = 0; r < _rules.Count; r++)
                        {
                            TechniqueUnlockRule rule = _rules[r];
                            if (rule.SourceKind != TechniqueUnlockSourceKind.Case) continue;
                            if (!ContainsDisease(p.DiseaseSet, rule.SourceId)) continue;
                            if (_writer.TryUnlock(_actorId, rule.TechniqueId, rule.SourceId, tick))
                                written++;
                        }
                        break;
                    }
                }
            }

            return written;
        }

        /// <summary>
        /// <see cref="DiseaseIdSet"/> 是 <c>ulong</c> 位集,位序 = **<c>DiseaseId − 1</c>**
        /// (`DiseaseIdSet` 的位序契约;`PatternDetector.cs:78` 与 `TelemetryReporter` 的
        /// `bit + 1` 是同一口径)。越界 id 返回 <c>false</c> —— 病种 id &gt; 64 时位集
        /// **表达不了**它,而「表达不了」不能当成「命中」(那会让一条写错的规则无条件触发)。
        /// <para>🔁 <b>2026-11-10(T19)修复 off-by-one</b>:原实现读的是 <c>bit = diseaseId</c>
        /// (`set.Bits >> diseaseId`),与生产写入侧(<c>bit = id − 1</c>)差一位 ⇒
        /// 一条「病种 4 结案 ⇒ 解锁某技法」的规则会被**病种 5**的结案触发(静默开错技法)。
        /// 之所以一直没被发现:`CaseCloseWriter` 生产调用点当时为 0(T19 才接上),而本类的
        /// 夹具按 <c>1UL &lt;&lt; d</c> 造位集、与错误读法**自洽**(自指判据·F3)。
        /// 夹具已同步改为 <c>1UL &lt;&lt; (d − 1)</c>,并新增一条**能分辨两种读法**的判据。</para>
        /// </summary>
        private static bool ContainsDisease(in DiseaseIdSet set, int diseaseId)
        {
            int bit = diseaseId - 1;             // 位序契约:bit = DiseaseId − 1
            if (bit < 0 || bit >= 64) return false;
            return ((set.Bits >> bit) & 1UL) != 0UL;
        }
    }
}
