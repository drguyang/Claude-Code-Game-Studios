// M2 接线轮 · 阶段 B —— PatternRecognized 写者(37 病例系统,主机侧生产写入通道)。
//
// 权威来源:
//   design/registry/entities.yaml —— SimEvent.Kind.PatternRecognized:
//     stream: case · author: 37 病例系统(主机唯一 Append,ADR-008 §一)
//     payload_schema: "patient_id: i32(世界级 ⇒ None = -1); anchor_case: 三元组;
//                      salted_key: u64; member_set: 冻结三元组(anchor_case + disease_set + 版本)"
//   design/gdd/case-system.md —— F-37.1(同源检测:FirstPerPatient / CandidateSeq / PatternFired,
//     阈值 P0 = 3)· F-37.1b(脚本三案链 ScriptedChain;«一次性»:已发 ⇒ 永不重发)
//     · AC-37-07/08(长度 < 阈值不触发 / 恰达标触发)· AC-37-09/29(第 4 例不重发 / 永不重发)
//     · AC-37-27(同 tick 多 D 按 salted_key **整数升序**写,禁 disease_set 迭代序)
//   ADR-008 §一(病例流路由)· §三(载荷:salted_key / anchor_case / 冻结三元组)· §五(盐契约:逐 D 派生)
//   ADR-007 §二(WorldSeed 存档头)· §四(世界级事件 Patient = None = -1,不污染 max(patient_id) 高水位)
//   ADR-005(主机唯一 Append;Seq 由发号器给出 ⇒ header Seq = -1 哨兵)
//   ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder;本类内禁 `new PayloadRef(`)
//
// 核心机制:
//   - **复用** `PatternDetector.NextEmissions`(纯函数,同族 S11-9 交付)做「该不该发、发哪几条」的判定:
//     它已在内部过«一次性»门(流中已有同 salted_key 的 PatternRecognized ⇒ 该 D 不再返回),
//     且按 salted_key **整数升序**返回(AC-37-27 的唯一定序源)。
//     ⇒ 本类**不重写检测算法**、**不自行排序**(重排即第二定序源,违 AC-37-27)。
//   - `anchor_case` = `MemberSet` 的**末位**(跨过阈值的那一例)—— `NextEmissions` 按 `candidates`
//     全序取前 threshold 例、**未排序**(PatternDetector.cs:271-277),故末位 = 第 threshold 例
//     = ADR-008 §三「`anchor_case` = 触发识别的那一例(锚点)」。
//   - `member_set` 的三分量逐一分派:① `anchor_case` = 上述末位;② `disease_set` = **该 D 本身**
//     (`new DiseaseIdSet(1UL << ordinal)` —— 同源检测逐 D 独立、一 D 一条事件,故单条载荷的病种集
//     恒为单元素;依据 = registry `member_set: 冻结三元组(...)` 的唯一真源口径,ADR-024 §①);
//     ③ `version` = ordinal 映射表版本(由调用方给出,见下登记项)。
//   - 事件头:`Patient = PatientId.None`(=-1,世界级事件 —— ADR-007 §四,不污染高水位);
//     `Seq = -1` 未发号哨兵,由 `EventStream` 在 Append 时发号(同 tick 多条 Patient = None ⇒
//     Seq = 0,1,2…,恰对上 AC-37-27 的同 tick 多 D 序)。
//
// ⚠️ 登记项(本批不裁 —— 均为「Sim 侧够不着」的注入面,承 `CaseOpenWriter` 对 `disease_snapshot`
//    的同一先例口径:Sim 侧无真源者由装配层 / 调用方注入,不在本类内发明新机制):
//   - `worldSeed`(存档头字段,ADR-007 §二)由**调用方**给出 —— 生产 = 7a 读档头经装配层注入;
//     测试注入字面量。Sim 侧无第二真源。
//   - `version`(ordinal 映射表版本,append-only 内容值,ADR-014 烘焙)同理由调用方给出。
//   - `diseaseExtractor` / `saltedKeyExtractor` 两个解码委托由调用方给出(解码归 `Sim.Codec`,
//     而 Sim 引用集禁引 `Sim.Codec` —— ADR-025 §①);本类只按 `PatternDetector.NextEmissions`
//     原签名**透传**,不改造、不由本类改写「要发哪些」的列表(否则 CB-1 病灶复发)。
//   - 生产触发点(结案 → 本写者)尚未接线:意图源(结案 / 触发)尚不存在,归 37 实现轮的接线轮。
//   - `IEventAuthority.IsHost` 门不在本件:与既有写者(PatientSpawner / CaseOpenWriter 等)同格 ——
//     主机唯一由「谁调用写者」保证(ADR-005),客户端写通道的统一门禁归 45 轮。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="PatternRecognitionWriter.TryRecognize"/> 的结果码(零写入分支亦具名,不静默)。</summary>
    public enum PatternRecognitionWriteResult
    {
        /// <summary>已识别:本 tick 写入了 ≥ 1 条 <see cref="EventKind.PatternRecognized"/>(本次真正写入)。</summary>
        Recognized = 0,

        /// <summary>零写入 —— 无任何病种达标(GDD F-37.1:该 D 的候选全序长度 &lt; 阈值;AC-37-07)。</summary>
        BelowThreshold = 1,

        /// <summary>零写入 —— 有 D 已达标,但**全部已发过**(GDD F-37.1b «一次性»:
        /// PatternRecognized 已发出 ⇒ 永不重发;AC-37-09 / AC-37-29)。此为幂等分支:
        /// 重复调用 / 第 4 例到位均不产生第二条。</summary>
        AlreadyRecognized = 2,
    }

    /// <summary>
    /// 同源检测写者 —— 37 的 <c>PatternRecognized</c> 生产写入通道(主机侧)。
    /// </summary>
    /// <remarks>
    /// <para><b>与 <see cref="PatternDetector"/> 的分工</b>:<see cref="PatternDetector.NextEmissions"/>
    /// 是纯函数(输入流前缀,输出本 tick 应发射的逐 D 列表,含«一次性»门),供无 sink 的场景与测试复用;
    /// 本类把该输出对着**真流**编码入流。检测算法与定序**不在本类**。</para>
    /// <para><b>只写 <c>PatternRecognized</c></b>:立案 / 结案 / 判断记录三支各有写者,不归本件。</para>
    /// </remarks>
    public sealed class PatternRecognitionWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IReadOnlyList<SimEvent> _streamEvents;
        private readonly long _worldSeed;
        private readonly int _patternThreshold;
        private readonly Func<SimEvent, int[]> _diseaseExtractor;
        private readonly Func<SimEvent, ulong> _saltedKeyExtractor;
        private readonly IReadOnlyList<ScriptedChain> _scriptedChains;
        private readonly int _version;

        /// <summary>
        /// 构造同源检测写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <param name="streamEvents">
        /// 事件流的**只读视图**(生产 = <c>bag.Stream.Events</c>)—— «该不该发» 与 «是否已发»
        /// 均在 <see cref="PatternDetector.NextEmissions"/> 内于此视图上求值;该纯函数只看
        /// <c>CaseClosed</c> / <c>PatternRecognized</c> 两类,故传入混流全集即可(其余 Kind 天然忽略)。
        /// </param>
        /// <param name="worldSeed">WorldSeed(存档头,ADR-007 §二)—— 盐派生的输入(ADR-008 §五)。
        /// 由调用方给出(见类头登记项)。</param>
        /// <param name="patternThreshold">同源阈值(GDD F-37.1,P0 = 3);须 ≥ 1
        /// (≤ 0 会让检测器对空候选集也«达标»,锚点不存在 ⇒ 编程错误,fail-loud)。</param>
        /// <param name="diseaseExtractor">从 <c>CaseClosed</c> 提取病种 ordinal 数组的委托
        /// (生产 = 解码 <c>CaseClosedPayload.DiseaseSet</c>;由调用方给出 —— 解码归 <c>Sim.Codec</c>)。</param>
        /// <param name="saltedKeyExtractor">从 <c>PatternRecognized</c> 提取 salted_key 的委托
        /// («一次性»门需要;由调用方给出 —— 解码归 <c>Sim.Codec</c>)。</param>
        /// <param name="scriptedChains">脚本三案链集(GDD F-37.1b);<c>null</c> / 空 = 全走自然检测。</param>
        /// <param name="version">ordinal 映射表版本(append-only 内容值,ADR-014 烘焙);由调用方给出。</param>
        /// <exception cref="ArgumentNullException">eventSink / encoder / streamEvents /
        /// diseaseExtractor / saltedKeyExtractor 任一为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="patternThreshold"/> &lt; 1。</exception>
        public PatternRecognitionWriter(
            IEventSink eventSink, IPayloadEncoder encoder, IReadOnlyList<SimEvent> streamEvents,
            long worldSeed, int patternThreshold,
            Func<SimEvent, int[]> diseaseExtractor, Func<SimEvent, ulong> saltedKeyExtractor,
            IReadOnlyList<ScriptedChain> scriptedChains, int version)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _streamEvents = streamEvents ?? throw new ArgumentNullException(nameof(streamEvents));
            _diseaseExtractor = diseaseExtractor ?? throw new ArgumentNullException(nameof(diseaseExtractor));
            _saltedKeyExtractor = saltedKeyExtractor ?? throw new ArgumentNullException(nameof(saltedKeyExtractor));

            if (patternThreshold < 1)
                throw new ArgumentOutOfRangeException(nameof(patternThreshold), patternThreshold,
                    "阈值须 ≥ 1(≤ 0 会对空候选集也判达标,anchor_case 不存在)");

            _worldSeed = worldSeed;
            _patternThreshold = patternThreshold;
            _scriptedChains = scriptedChains;   // 可空 = 无脚本链(全走自然检测)
            _version = version;
        }

        /// <summary>
        /// 同源检测(GDD F-37.1 / F-37.1b):对当前流前缀求本 tick 应发射者,逐条写
        /// <see cref="EventKind.PatternRecognized"/>。
        /// </summary>
        /// <remarks>
        /// <para><b>幂等(fires-once)在此复核</b>:<see cref="PatternDetector.NextEmissions"/> 内置
        /// «一次性»门 —— 流中已有同 salted_key 的 <c>PatternRecognized</c> ⇒ 该 D 不进返回值。
        /// 本类路由经它(不自行实现门),故重复调用 / 第 4 例到位都**零写入**。</para>
        /// <para><b>多条同 tick 的序</b> = <see cref="PatternDetector.NextEmissions"/> 的返回序,
        /// 即 salted_key 整数升序(AC-37-27);本类按序 Append,不重排。</para>
        /// </remarks>
        /// <param name="tick">当前 tick(结案触发的这一 tick);它同时是每条本事件的
        /// <c>SimEvent.Tick</c>(同 tick 多 D 共享之)。</param>
        /// <returns>结果码;仅 <see cref="PatternRecognitionWriteResult.Recognized"/> 意味着真的写了。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> &lt; 0
        /// (入参非法 = 编程错误,fail-loud,与 <c>CaseOpenWriter</c> / <c>PatientSpawner</c> 同口径)。</exception>
        public PatternRecognitionWriteResult TryRecognize(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");

            // 「该不该发、发哪几条」—— 复用纯函数(含«一次性»门 + salted_key 升序)。
            var emissions = PatternDetector.NextEmissions(
                _streamEvents, _worldSeed, _patternThreshold,
                _diseaseExtractor, _saltedKeyExtractor, _scriptedChains);

            if (emissions.Count == 0)
            {
                // 零写入分支的具名区分:达标集中是否已有 D(前缀属性)——
                // Evaluate 的 Fired 是「有 D 达阈值」的前缀属性(已发也仍为 true),
                // 故 Fired ∧ 无新发射 ⇒ 全部已发过(fires-once);否则 ⇒ 尚未达标。
                bool anyAtThreshold = PatternDetector.Evaluate(
                    _streamEvents, _worldSeed, _patternThreshold,
                    _diseaseExtractor, _scriptedChains).Fired;

                return anyAtThreshold
                    ? PatternRecognitionWriteResult.AlreadyRecognized
                    : PatternRecognitionWriteResult.BelowThreshold;
            }

            for (int i = 0; i < emissions.Count; i++)
            {
                var emission = emissions[i];
                var members = emission.MemberSet;

                // anchor_case = 成员集末位 = 跨过阈值的那一例(ADR-008 §三;
                // NextEmissions 未排序取前 threshold 例 ⇒ 末位即第 threshold 例,见类头)。
                // 阈值 ≥ 1(ctor 已验)且发射仅当候选数 ≥ 阈值 ⇒ members 恰 threshold 项、非空。
                var anchorCase = members[members.Count - 1];

                var payload = new PatternRecognizedPayload(
                    PatientId.None.Value,                            // 世界级 ⇒ -1(ADR-007 §四)
                    anchorCase,                                       // 冻结三元组 1/3
                    emission.SaltedKey,                               // 逐 D 派生盐(ADR-008 §五)
                    new DiseaseIdSet(1UL << emission.DiseaseOrdinal), // 冻结三元组 2/3:{D}
                    _version);                                        // 冻结三元组 3/3

                var payloadRef = _encoder.Encode(EventKind.PatternRecognized, payload);

                // 事件头:Patient = None(世界级,不污染高水位);Seq = -1 未发号哨兵,由发号器给出。
                _eventSink.Append(new SimEvent(
                    tick, PatientId.None, -1, EventKind.PatternRecognized, payloadRef));
            }

            return PatternRecognitionWriteResult.Recognized;
        }
    }
}
