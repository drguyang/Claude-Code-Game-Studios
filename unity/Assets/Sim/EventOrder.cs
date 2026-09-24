// 权威来源:ADR-008 §跨流全序键 · ADR-024 §①(Kind → StreamId 白名单真源 = entities.yaml,
//          执行体 = kindgen 生成的 StreamRouting)
//          · Story 009 AC-21a-52 ⑤(键位 = (Tick, StreamPriority, Patient, Seq);
//            Craft 的 Patient = PatientId.None,actor_id 位由载荷吸收不进键)
//
// 拆分理由:键的**比较语义**(纯 BCL 字典序)住 Sim.Contracts/EventOrderKey —— 7a / 45 / 51
// 无需引用本程序集即可排序;**Kind → StreamId 解析**依赖 kindgen 路由表,而路由表按 ADR-024
// 住 Sim(生成物 StreamRouting.g.cs 的程序集)⇒ 这一半只能住这里。
// 两者合起来才是 ADR-008 的完整全序键;单独使用 EventOrderKey 时调用方须自证 stream 正确。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>SimEvent → <see cref="EventOrderKey"/> 的解析与比较(kindgen 路由驱动)。</summary>
    public static class EventOrder
    {
        /// <summary>取该事件的跨流全序键。Kind 不在白名单 = StreamRouting 构建期已不存在,
        /// 运行期抵达即抛(双保险,ADR-024)。</summary>
        public static EventOrderKey KeyOf(in SimEvent e) =>
            new EventOrderKey(e.Tick, StreamRouting.Of(e.Kind), e.Patient, e.Seq);

        /// <summary>两事件的全序比较(负 = a 先、零 = 同键、正 = b 先)。纯函数,零状态。</summary>
        public static int Compare(in SimEvent a, in SimEvent b) =>
            KeyOf(a).CompareTo(KeyOf(b));
    }
}
