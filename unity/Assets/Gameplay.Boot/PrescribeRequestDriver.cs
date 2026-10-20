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
using DaYiJingCheng.Gameplay.Presentation;
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

        /// <summary>处方表映射:item_key → 11 处方表条目(action_id / polarity / unlock)。
        /// <para><b>2026-10-20 读方接线</b>:源 = 烘焙产物行(构造参 <c>prescriptionRows</c>,
        /// 由装配层经 <c>IDataProvider</c> 装载);缺省 = <see cref="FallbackEntries"/>。</para></summary>
        private readonly IReadOnlyDictionary<ItemKey, PrescriptionEntry> _entries;

        /// <summary>戥子档位源(装配层最小:单一药物档案,档位 = DoseRange(1..5))。
        /// <para>⚠️ 装配层注记:真装载面(21a baked)落地后替换,装配签名零改动。</para></summary>
        private readonly DentchDoseSelector _selector;

        /// <summary>本驱动实际使用的档数上限(产物优先 · 回退 <see cref="FallbackMaxDoseDetents"/>)。
        /// <para>⚠️ 与 <see cref="DoseDetentCount"/> 的区别:<b>本值 = 上限旋钮</b>,
        /// 后者 = 该药的实际落点数(`hi − lo + 1`)。二者不相等是正常的(上限 ≥ 实际档数)。</para></summary>
        private readonly int _maxDoseDetents;

        /// <summary>是否主机上下文(非主机 ⇒ Prescribe 零 Append / 零扣减)。</summary>
        private readonly bool _isHost;

        /// <summary>戥子档位上限旋钮的**回退值**(产物缺省时用)。
        /// <para>⚠️ <b>2026-10-20 读方接线后,本常量不再是运行期真源</b> ——
        /// 真源 = `prescription_actions.cooked.bytes` 的 <c>MAX_DOSE_DETENTS</c>
        /// (经 <c>PrescriptionActionsDataSet.MaxDoseDetents</c> 由装配层传入构造参
        /// <c>maxDoseDetents</c>)。本常量退为:① 未带产物的装配缺省;② 测试面锚点。</para>
        /// <para>⚠️ <b>取值口径订正</b>:GDD `prescription-and-medication.md:794` 已裁
        /// <c>MAX_DOSE_DETENTS = 7</c>(2026-10-07,「原建议 ≤ 9」的**窄化**)。
        /// 本常量原写 9 是**窄化前**的旧建议值,已过期 —— 现对齐 GDD 的 7,
        /// 使「回退值」与「产物值」一致(否则无产物路径会静默用另一个上限)。</para>
        /// <para>⚠️ 值仍归**数值轮**;此处取 GDD 已裁值,不自造新数。</para></summary>
        public const int FallbackMaxDoseDetents = DentchDoseSelector.RegisteredMaxDoseDetents;

        /// <summary>当前戥子**档位数**(= <c>hi − lo + 1</c>;整剂路径 = 1)。
        /// <para>⚠️ 唯一出处 = 本驱动持有的 <see cref="DentchDoseSelector"/>
        /// (规则十三唯一量化点)—— 42 侧经 <c>ICasebookDtoSource.DoseDetentCount</c>
        /// **回读**本值,不自行由 <c>dose_range</c> 再算一遍(第二推导 = 第二真相)。</para>
        /// <para>⚠️ 42 只读不持(ADR-013 §9 C3):本属性是只读投影,无 setter。</para>
        /// </summary>
        public int DoseDetentCount => _selector.TickCount;

        /// <summary>本驱动实际生效的**档数上限旋钮**(产物 `MAX_DOSE_DETENTS` 优先 · 回退常量)。
        /// <para>⚠️ 与 <see cref="DoseDetentCount"/>(= 落点数 `hi − lo + 1`)是**两个量**:
        /// 上限 ≥ 落点数,相等只是特例。判「源真的换了」须读本属性,不能读落点数
        /// (落点数由药档 `dose_range` 决定,与上限无关)。</para>
        /// <para>⚠️ 只读投影(ADR-013 §9 C3 同款):42 只读不持,无 setter。</para></summary>
        public int MaxDoseDetents => _maxDoseDetents;

        /// <param name="eventSink">主机事件写入通道(唯一 Append 落点)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 唯一路径)。</param>
        /// <param name="ports">11 兄弟 epic 接缝(21a/20/30 最小端口实装)。</param>
        /// <param name="isHost">是否主机上下文。</param>
        /// <param name="prescriptionRows">处方表烘焙产物行(<c>prescription_actions.cooked.bytes</c>
        /// 解码所得);<c>null</c> / 空 ⇒ 回退 <see cref="FallbackEntries"/>(零行为变化 ——
        /// 15+ 个既有装配调用方全部不传本参)。</param>
        /// <param name="maxDoseDetents">戥子档数上限旋钮(<c>dose_const.MAX_DOSE_DETENTS</c>,
        /// 由装配层从同一产物传入);<c>null</c> ⇒ 回退 <see cref="FallbackMaxDoseDetents"/>。</param>
        /// <exception cref="ArgumentException">产物行内 item_key 重复 / 极性串无法解析。</exception>
        public PrescribeRequestDriver(IEventSink eventSink, IPayloadEncoder encoder,
                                      PrescribePorts ports, bool isHost,
                                      IReadOnlyList<PrescriptionActionCookedRow> prescriptionRows = null,
                                      int? maxDoseDetents = null)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _ports = ports ?? throw new ArgumentNullException(nameof(ports));
            _isHost = isHost;

            // 处方表源(2026-10-20 读方接线):产物行优先;缺省 = 最小硬编码单条。
            // ⚠️ 本轮**行为逐值不变** —— 产物的唯一条目 (salicylic_acid, 10, symptomatic)
            //    与回退条目同形(实测;见实现轮原件 §诚实边界)。
            _entries = prescriptionRows != null && prescriptionRows.Count > 0
                ? BuildFromRows(prescriptionRows)
                : FallbackEntries;

            // 档数上限(2026-10-20 读方接线 · 甲档剩余项):产物优生;缺省 = 回退常量。
            // ⚠️ 此前这里是常量 `DentchDoseSelector.RegisteredMaxDoseDetents`(= 9),
            //    而产物 `MAX_DOSE_DETENTS` = 7(GDD 已裁值)⇒ **上限读数与真源脱钩**。
            //    接上后:产物有值 ⇒ 读产物;无 ⇒ 读回退常量(两者现已同为 7)。
            // ⚠️ 不改行为(replay-safety):当前药 `dose_range = (1,5)` ⇒ 5 落点,
            //    上限 7 / 9 都远大于它 ⇒ **落点集与档值逐位相同**。判据是「源真的换了」。
            _maxDoseDetents = maxDoseDetents ?? FallbackMaxDoseDetents;

            _selector = new DentchDoseSelector(
                MinimalDrugCatalog.DrugProfileFor(MinimalDrugCatalog.DrugKey),
                _maxDoseDetents,
                isFocusEnabled: true);
        }

        /// <summary>回退处方表(最小硬编码单条)—— 21a 作者态已登记 salicylic_acid。
        /// <para>读方接线后双重身份:① 产物行缺省时的生产表;② 产物行的**独立对照 oracle**
        /// (<c>ReaderWiringGateTest.test_readers_prescriptionActions_artifactRows_matchFallbackLiteral</c>
        /// 用它逐项对拍产物行 —— 产物被烘成另一套即转红)。</para>
        /// <para>⚠️ <b>public 的理由 = 它是判据的宾语</b>:若只留 <c>internal</c> 而测试够不着,
        /// 「独立 oracle」就只是注释里的一句话(散文),不是可红的机器。</para>
        /// <para>⚠️ 值归**数值轮**(此处仅为接线可测)。</para></summary>
        public static IReadOnlyDictionary<ItemKey, PrescriptionEntry> FallbackEntries { get; } =
            new Dictionary<ItemKey, PrescriptionEntry>
            {
                [MinimalDrugCatalog.DrugKey] = new PrescriptionEntry(
                    actionId: 10, polarity: PrescriptionPolarity.Symptomatic, unlockLevel: 0),
            };

        /// <summary>产物行集 → 处方表。
        /// <para>⚠️ 两处**派生映射**(产物不携带,已登记为实现轮 §诚实边界):
        /// ① <c>ProcessingState</c> 取 <see cref="ProcessingState.Extracted"/> —— 产物只带
        ///    <c>base_id</c>,与 21a <c>MinimalDrugCatalog.DrugKey</c> 同口径;
        /// ② <c>unlockLevel</c> 恒 <c>0</c> —— 产物无该字段(F-11.5 的门槛值仍归数值轮)。
        /// 产物一旦带上这两者,本节即成**第二真源**,须同批删。</para></summary>
        /// <exception cref="ArgumentException">item_key 重复 / 极性串未知。</exception>
        private static Dictionary<ItemKey, PrescriptionEntry> BuildFromRows(
            IReadOnlyList<PrescriptionActionCookedRow> rows)
        {
            var built = new Dictionary<ItemKey, PrescriptionEntry>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                PrescriptionActionCookedRow row = rows[i];
                ItemKey key = new ItemKey(row.ItemKey, ProcessingState.Extracted);
                if (built.ContainsKey(key))
                    throw new ArgumentException(
                        $"处方表建表失败:item_key \"{row.ItemKey}\" 在产物行集中出现两次(行序 #{i})" +
                        " ⇒ 静默后者胜出会让生效条目取决于行序(不可见;产物损坏)");
                built.Add(key, new PrescriptionEntry(
                    actionId: row.ActionId,
                    polarity: ParsePolarity(row.Polarity, row.ItemKey),
                    unlockLevel: 0));
            }
            return built;
        }

        /// <summary>极性串 → 枚举。<b>未知串即抛</b>(不静默默认)。
        /// <para>烘焙期已由 DC-3 白名单(∈ {causal, symptomatic})拦过 ⇒ 运行期再遇未知 = 产物被
        /// 换过 / schema_version 不匹配,属启动期硬失败面(E-13 同口径)。</para></summary>
        /// <exception cref="ArgumentException">极性串 ∉ {causal, symptomatic}。</exception>
        private static PrescriptionPolarity ParsePolarity(string polarity, string itemKey)
        {
            if (polarity == "symptomatic") return PrescriptionPolarity.Symptomatic;
            if (polarity == "causal") return PrescriptionPolarity.Causal;
            throw new ArgumentException(
                $"处方表极性无法解析:item_key \"{itemKey}\" 的 polarity=\"{polarity}\" " +
                "∉ {causal, symptomatic}(烘焙期 DC-3 已拦;运行期遇未知 ⇒ 产物被换过 / schema_version 不匹配)");
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
