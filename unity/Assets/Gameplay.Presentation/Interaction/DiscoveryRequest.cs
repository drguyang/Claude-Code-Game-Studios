// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— F-4.3(自报:4 → 6,非写)逐字载荷
//   ADR-021 §三(`PoiStateChanged{poi_id, new_state}`;6 = 唯一写者;4 = 合法自报方)
//   ADR-009 §七(拾取三段式:意图事件 → 主机判距 → 效果进流 —— 本类型 = 4 侧的意图半)
//   ADR-006(`DiscoveryRequest` 载荷全整数域:poi_id int64 / evidence_cell WorldPos / tick int64)
//   ADR-015 §三(evidence_cell = 单一整数格 WorldPos)
//
// ⚠️ 载荷逐字形状(GDD F-4.3):
//     IDiscoveryReporter.Request(6, { poi_id: int64, evidence_cell: WorldPos, tick: int64 })
//   三字段**全整数域** —— 反射断言本类型字段闭包零 `UnityEngine.*` / 零 float(AC-4-03 同源纪律)。
//
// ⚠️ `tick` **不在本类型内自取** —— 由调用方经 `ITickProvider` 供给(ADR-007 Roll 纯函数同型
//   纪律:调用方供给,类型自身不持时钟)。本类型是三字段的**纯数据载体**。
//
// ⚠️ `evidence_cell` 的语义 = **玩家经流确立格**(不是 POI 格,不是表现态位置):
//   它是「凭据」—— 4 声称「玩家此刻在此格交互了该 POI」,6 侧据本格做**主机判距复验**
//   (ADR-009 §七 三段式的第二段,归 6)。禁写表现态位置(ADR-016 §三)。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 发现请求载荷(GDD F-4.3 逐字三字段)。
    /// <para><b>纯数据载体,零方法,零可变字段</b>,三字段皆整数域(ADR-006)。</para>
    /// <para>⚠️ <b>这是「请求」不是「写入」</b>:4 把「玩家主动在 <see cref="EvidenceCell"/> 交互了
    /// POI <see cref="PoiId"/>」的事实交给 6;是否落 <c>PoiStateChanged</c> 由 6 决定
    /// (ADR-021 §② 唯一写者 —— 与 ADR-009 §七 三段式同构)。</para>
    /// </summary>
    /// <remarks>
    /// 本类型是 4 出境面的**载荷**;出境通道仍是 <see cref="IDiscoveryReporter"/>。
    /// 4 **绝不**经 <c>IEventSink</c>(AC-4-02)。
    /// </remarks>
    public readonly struct DiscoveryRequest
    {
        /// <summary>被主动交互的 POI id(<c>poi_id</c> 空间,ADR-021 §三 / ADR-006 int64)。</summary>
        public readonly long PoiId;

        /// <summary>凭据格 —— <b>玩家经流确立格</b>(ADR-009 §七 主机判距复验的输入;
        /// 非 POI 格、非表现态位置)。</summary>
        public readonly WorldPos EvidenceCell;

        /// <summary>发生时的逻辑 tick(由调用方经 <c>ITickProvider</c> 供给,本类型不自取)。</summary>
        public readonly long Tick;

        /// <summary>构造发现请求(F-4.3 三字段)。</summary>
        public DiscoveryRequest(long poiId, WorldPos evidenceCell, long tick)
        {
            PoiId = poiId;
            EvidenceCell = evidenceCell;
            Tick = tick;
        }
    }
}
