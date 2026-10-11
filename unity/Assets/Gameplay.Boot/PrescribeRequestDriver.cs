// sprint-06 G3.1 · 处方驱动(戥子 → 方笺 · 11)(2026-10-11)
//
// 权威来源:
//   production/sprints/sprint-06.md §2 G3.1 —— 玩家选档 → 落笔 → `DrugTreatmentApplied`
//   入病史流 → 体征变化
//   ADR-009 §七(意图 → 权威结算的普通形状;11 = 无判定步,意图即效果,F-11.4)
//   ADR-005(主机唯一 Append / Step —— 本驱动只在主机上下文走完五步)
//   42 侧戥子(DentchDoseSelector):档序数 → 整数档值(唯一量化点,全程整数)
//   PrescribeFlow(11 五步流程本体;端口 = 装配层 G3.2 最小实装)
//
// ⚠️ 本驱动 = 呈现侧装配件(住 Gameplay.Boot,不持 sim 状态):
//   消费一次处方意图(42 戥子落笔形状的翻译)→ 把档序数换算为 11 整数档 →
//   构造 `PrescribeRequest` → `PrescribeFlow.Prescribe`(真端口)。
//   **零自持状态**:本次意图即本次结算,不缓存任何 selected 档(承 AC-11-18 零 clamp 路径)。
//
// ⚠️ 档序数 → 档值的量化点归 42 戥子(<c>DentchDoseSelector.DoseValueOf</c>,规则十三
//   唯一量化点)⇒ 本驱动引用该选择器(装配方向 Boot → UI,允许:Gameplay.Boot asmdef
//   引 Gameplay.UI;42 不反向引用装配层)。意图的**传入形状**以本类自身全整数结构承接
//   —— 42 侧翻译戥子落笔为 <see cref="PrescribeIntent"/>,本驱动结算。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.Prescription;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 一次处方意图(42 戥子落笔形状的全整数同域翻译)。
    /// <para>与 <c>PrescribeStrokeIntent</c> 同域:<c>ActorId</c> / <c>PatientId</c> /
    /// <c>ItemKey</c> / <c>DoseOrdinal</c>(档序数 1 起;整剂路径 = 1)。</para>
    /// </summary>
    public readonly struct PrescribeIntent
    {
        /// <summary>医师(玩家)id。</summary>
        public readonly int ActorId;

        /// <summary>施治对象。</summary>
        public readonly PatientId PatientId;

        /// <summary>所开之药(复合主键)。</summary>
        public readonly ItemKey ItemKey;

        /// <summary>档序数(1 起)。</summary>
        public readonly int DoseOrdinal;

        public PrescribeIntent(int actorId, PatientId patientId, ItemKey itemKey, int doseOrdinal)
        {
            ActorId = actorId;
            PatientId = patientId;
            ItemKey = itemKey;
            DoseOrdinal = doseOrdinal;
        }
    }

    /// <summary>一次处方的回执(只读;失败原因不回传 —— 零提示纪律 AC-11-12)。</summary>
    public readonly struct PrescribeDriverResult
    {
        /// <summary>是否走完五步并落流(主机)。</summary>
        public readonly bool Applied;

        public PrescribeDriverResult(bool applied)
        {
            Applied = applied;
        }
    }

    /// <summary>
    /// 处方驱动 —— 把一次戥子落笔意图接进 <see cref="PrescribeFlow.Prescribe"/>
    /// (11 五步流程本体)。住装配层:同时可引 Sim(流程)与 Sim.Codec(端口构造)。
    /// </summary>
    public sealed class PrescribeRequestDriver
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly PrescribePorts _ports;

        /// <summary>处方表最小映射:item_key → 11 处方表条目(action_id / polarity / unlock)。</summary>
        private readonly IReadOnlyDictionary<ItemKey, PrescriptionEntry> _entries;

        /// <summary>戥子档位源(装配层最小:单一药物档案,档位 = DoseRange(1..5))。
        /// <para>⚠️ 装配层注记:真装载面(21a baked)落地后替换,装配签名零改动。</para></summary>
        private readonly DentchDoseSelector _selector;

        /// <summary>是否主机上下文(非主机 ⇒ Prescribe 零 Append / 零扣减)。</summary>
        private readonly bool _isHost;

        /// <summary>戥子档位上限旋钮(值归用户数值轮;GDD 规则十三登记建议值)。</summary>
        public const int MaxDoseDetents = DentchDoseSelector.RegisteredMaxDoseDetents;

        /// <param name="eventSink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="ports">11 兄弟 epic 接缝(21a/20/30 最小端口实装)。</param>
        /// <param name="isHost">是否主机上下文。</param>
        public PrescribeRequestDriver(IEventSink eventSink, IPayloadEncoder encoder,
                                      PrescribePorts ports, bool isHost)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
            _isHost = isHost;

            // 最小处方表(21a 作者态已登记 salicylic_acid;数值轮未开的接线载体)
            _entries = new Dictionary<ItemKey, PrescriptionEntry>
            {
                [MinimalDrugCatalog.DrugKey] = new PrescriptionEntry(
                    actionId: 10, polarity: PrescriptionPolarity.Symptomatic, unlockLevel: 0),
            };

            _selector = new DentchDoseSelector(
                MinimalDrugCatalog.DrugProfileFor(MinimalDrugCatalog.DrugKey),
                MaxDoseDetents,
                isFocusEnabled: true);
        }

        /// <summary>
        /// 一次处方落笔:档序数 → 整数档值 → 构造 <see cref="PrescribeRequest"/> →
        /// <see cref="PrescribeFlow.Prescribe"/>。
        /// </summary>
        /// <param name="intent">处方意图(档序数 1 起)。</param>
        /// <param name="tick">当前 tick(ITickProvider 驱动方透传)。</param>
        /// <returns>回执(Applied = 是否走完五步并落流)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">档序数越界(戥子落点集合之外,装配错误)。</exception>
        public PrescribeDriverResult TryPrescribe(in PrescribeIntent intent, long tick)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "逻辑 tick 不得为负");

            if (!_entries.TryGetValue(intent.ItemKey, out PrescriptionEntry entry))
                return new PrescribeDriverResult(false);   // 未知药:零事件(不报错 —— 零提示纪律)

            // 戥子档序数 → 11 整数档值(唯一量化点;整剂路径恒 1;越界 ⇒ 抛)
            // ⚠️ 2026-10-12 C3-F3:删死 try/catch(原 catch 后只 rethrow,零语义)。
            int doseValue = _selector.DoseValueOf(intent.DoseOrdinal);

            DrugProfile profile = MinimalDrugCatalog.DrugProfileFor(intent.ItemKey);

            var request = new PrescribeRequest(
                itemKey: intent.ItemKey,
                entry: entry,
                actorId: intent.ActorId,
                patientId: intent.PatientId,
                selectedDose: doseValue,
                profile: profile,
                selectedQuality: 1,
                isHost: _isHost);

            PrescribeOutcome outcome = PrescribeFlow.Prescribe(in request, tick, _ports, _eventSink, _encoder);
            return new PrescribeDriverResult(outcome.Applied);
        }
    }
}
