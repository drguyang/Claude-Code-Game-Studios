// ADR 权威来源:
//   ADR-021 §三(新增世界流 Kind `PoiStateChanged`,载荷 { poi_id, new_state },6 = 唯一写者)
//   ADR-009 §七(拾取三段式:意图事件 → 主机判距 → 效果进流)
//   GDD design/gdd/interaction-system.md —— 规则三 / 规则七 / EC-6-2
//   Story production/epics/interaction-system/story-004(真实链路归 004;本故事只交付 spy 面)
//
// ⚠️ 本接口是 4 的**唯一出境面**(除返回值 InteractTarget 外)。
//   它与 IEventSink 的语义分界(AC-4-02 注释义务的落点):
//     · IDiscoveryReporter.Request  = 「**请求**」—— 4 把『玩家主动交互了某 POI』的事实交给 6;
//     · IEventSink.Append           = 「**写入**」—— 直接往三流里落事件。
//   4 **只请求,绝不写入**。请求到达 6 后,是否真的落 `PoiStateChanged` 由 6 决定
//   (6 是唯一写者,ADR-021 §②)。「零 Append ≠ 零上行」—— 上行经本接口,不经 sink。

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 发现上报面(4 → 6)。
    /// <para><b>请求 ≠ 写入</b>:本接口只把「玩家主动交互了一个 POI」的事实交给 6;
    /// 6 自行决定是否写 <c>PoiStateChanged</c>(ADR-021 §② 唯一写者)。</para>
    /// <para>⚠️ <b>触发条件是「主动交互输入」,不是「碰撞」</b>(规则七 / EC-6-2)——
    /// 玩家走进 POI 格但没按交互键,本接口<b>零调用</b>(AC-4-12)。</para>
    /// </summary>
    /// <remarks>
    /// P0 实现 = 本地转发给 6;spy 替身住测试侧(story 001 的 AC-4-12 夹具)。
    /// 真实链路、latch 归 6、有界性、<c>R_INTERACT</c> 单源归 story 004。
    /// </remarks>
    public interface IDiscoveryReporter
    {
        /// <summary>
        /// 上报一次发现请求。
        /// </summary>
        /// <param name="poiId">被主动交互的 POI id(<c>poi_id</c> 空间,ADR-021 §三)。</param>
        /// <param name="tick">发生时的逻辑 tick。</param>
        void Request(long poiId, long tick);
    }
}
