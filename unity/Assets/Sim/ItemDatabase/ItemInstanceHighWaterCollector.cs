// AC-7a-13 —— ItemInstanceId 高水位:三流并集收集器。
//
// 权威来源:
//   design/gdd/persistence-service.md:556-562 —— AC-7a-13(BLOCKING):
//     「next(instance_id) = max({ instance_id ∈ 三流并集 ∧ 非哨兵 }) + 1
//      (**扫描域 = 三流并集**,与 PatientId 一致;ADR-010 §五)—— instance_id 在
//      病史流 / 病例流 / 世界流任一出现都计入;**> 当前已用最大值,零复用**。
//      `Drop*` 事件保留 instance_id(否则掉落清单无从重构)」。
//   ADR-010 §五 —— 机制 A:计数器永不复位 0;迁移后 next = max(已见) + 1 由三流并集重构。
//   ADR-006 Amendment B —— 机制 A 三条不变量(`patient_id` 侧同源)。
//   design/registry/entities.yaml —— 五支载荷的 `instance_id` 真源(见下 §Kind 表)。
//
// ⚠️ **本类补的是「收集」这一环,不是「重构」** —— 实测(2026-10-25):
//   `IdAuthority.ReconstructItemHighWater`(`Sim/ItemDatabase/IdAuthority.cs:73`)
//   **生产调用点 = 0**(`grep -rn "ReconstructItemHighWater" unity/Assets/ | grep -v /Tests/`
//   只命中它自己的声明与 doc)。机制在、夹具在,但**没有任何东西把它接上流** ——
//   与 `Checkpoint` / `OnNarrativeAnchor` 零生产调用点是同一失效形态,只是此前未被点名。
//
// ⚠️ **本类住在 `Sim`,故必须经 `IPayloadDecoder` 取载荷**(ADR-031 §①):
//   `PayloadCodec` 住 `Sim.Codec`,而 `Sim.asmdef` 的引用集恰 = `{BCL, Sim.Contracts}`
//   (`b2` 门强制)。直接调 `PayloadCodec` = 击穿门 A,编译期即不可表达。
//
// ⚠️ **解码失败不吞、不跳过**(fail-loud):漏掉一个已用 id ⇒ `next` 偏低 ⇒ **发重号**
//   ⇒ 物品实例在 fold 里**静默合并**(ADR-010 §五登记的原风险)。AC-7a-13 的「零复用」
//   是硬不变量 ⇒ 宁可读档失败,不可读档后重号。
//
// ⚠️ **扫描域是「三流并集」,而本仓库当前的流形状是单一 `List<SimEvent>`**
//   (`EventStream.cs:38`;`StreamRouting` 只用于排序与遥测、不分桶)—— 该「退化形态」
//   已在 AC-7a-12 的补登记里如实登记,属 `TD-persist-03` 动作 ③ 的开放项。
//   本类按**并集语义**取名与验收(收 `IReadOnlyList<SimEvent>` 即为并集),不依赖分桶。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// 从三流并集收集**已发放的** <c>ItemInstanceId</c>,供
    /// <see cref="IdAuthority.ReconstructItemHighWater"/> 重构高水位(AC-7a-13)。
    /// </summary>
    /// <remarks>
    /// <para><b>扫描的五个 Kind</b>(真源 = <c>design/registry/entities.yaml</c> 的
    /// <c>payload_schema</c>;实测带 <c>instance_id</c> 的 Kind 共 5 支,无遗漏):</para>
    /// <list type="table">
    /// <item><term><see cref="EventKind.ResourceHarvested"/></term>
    ///       <description><c>instance_id: i64</c>(17 采集)</description></item>
    /// <item><term><see cref="EventKind.DropSpawned"/></term>
    ///       <description><c>instance_id: i64</c>(20 落地写者)</description></item>
    /// <item><term><see cref="EventKind.DropClaimed"/></term>
    ///       <description><c>instance_id: i64</c>(20 主机裁决)</description></item>
    /// <item><term><see cref="EventKind.DropDespawned"/></term>
    ///       <description><c>instance_id: i64</c>(20)</description></item>
    /// <item><term><see cref="EventKind.Craft"/></term>
    ///       <description><c>input_instance_ids[]: i64</c> + <c>output_instance_ids[]: i64</c>
    ///       (18 加工;产出 id 由主机于点火 tick 铸造)</description></item>
    /// </list>
    /// <para><b>其余 <c>ItemInstance</c> 的承载面不在流里</b>:快照段 / 库存折叠结果都是
    /// <b>派生态</b>(ADR-009 §一),其 id 必先经上述五支之一发放过 —— 快照漏读不改变
    /// 高水位(机制 A「计数器永不复位」保证只升不降)。</para>
    /// <para><b>哨兵不过滤</b>:负数 id 原样交出,由
    /// <see cref="IdAuthority.ReconstructItemHighWater"/> 按「负值一律忽略」处理
    /// (ADR-007 §四口径)。两处都过滤 = 双实现,将来必漂移。</para>
    /// </remarks>
    public static class ItemInstanceHighWaterCollector
    {
        /// <summary>
        /// 遍历事件并交出其载荷里的**全部** item instance id(含负哨兵,顺序即流序)。
        /// </summary>
        /// <param name="events">三流并集(当前实现 = <c>EventStream.Events</c> 快照,语义等价)。</param>
        /// <param name="decoder">载荷解码器(注入;住 <c>Sim.Codec</c> 的实现由装配根给)。</param>
        /// <returns>已发放过的 instance id 序列(可含重复 —— 同一实例会被多次点名)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="decoder"/> 为 null
        /// (fail-closed:不注入解码器就无法收集,静默返回空集 = 读档后发重号)。</exception>
        /// <exception cref="InvalidOperationException">某条事件的载荷未驻留 pool
        /// (查无此数据层)—— 不跳过,见类注「fail-loud」。</exception>
        /// <exception cref="System.IO.InvalidDataException">某条事件的载荷违反其 Kind 的 schema
        /// (损坏层)—— 同上。</exception>
        public static List<long> Collect(IReadOnlyList<SimEvent> events, IPayloadDecoder decoder)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            if (decoder == null)
                throw new ArgumentNullException(nameof(decoder),
                    "未注入 IPayloadDecoder —— 拒绝静默返回空集(空集 ⇒ 高水位偏低 ⇒ 读档后发重号," +
                    "违反 AC-7a-13「零复用」)");

            var ids = new List<long>();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                switch (e.Kind)
                {
                    case EventKind.ResourceHarvested:
                        ids.Add(decoder.Decode<ResourceHarvestedPayload>(e.Kind, e.Payload).InstanceId);
                        break;

                    case EventKind.DropSpawned:
                        ids.Add(decoder.Decode<DropSpawnedPayload>(e.Kind, e.Payload).InstanceId);
                        break;

                    case EventKind.DropClaimed:
                        ids.Add(decoder.Decode<DropClaimedPayload>(e.Kind, e.Payload).InstanceId);
                        break;

                    case EventKind.DropDespawned:
                        ids.Add(decoder.Decode<DropDespawnedPayload>(e.Kind, e.Payload).InstanceId);
                        break;

                    case EventKind.Craft:
                    {
                        CraftPayload craft = decoder.Decode<CraftPayload>(e.Kind, e.Payload);
                        // 输入组与产出组都算:「已用最大值」不区分该 id 是消耗掉的还是刚铸的 ——
                        // 消耗掉的那个号同样**不可再发**(机制 A 不变量②:不回收)。
                        if (craft.InputInstanceIds != null)
                            ids.AddRange(craft.InputInstanceIds);
                        if (craft.OutputInstanceIds != null)
                            ids.AddRange(craft.OutputInstanceIds);
                        break;
                    }

                    // 其余 Kind 的载荷不含 instance_id(真源见类注 §Kind 表)⇒ 不读、不解码。
                    default:
                        break;
                }
            }
            return ids;
        }

        /// <summary>
        /// 收集并**直接重构**高水位(端到端形态,供装配根的读档路径一句调完)。
        /// </summary>
        /// <param name="events">三流并集。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="authority">待重构的计数器(须是具 <see cref="IdAuthority"/> 形的那一支)。</param>
        /// <exception cref="ArgumentNullException">任一形参为 null。</exception>
        public static void CollectAndReconstruct(
            IReadOnlyList<SimEvent> events, IPayloadDecoder decoder, IdAuthority authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            authority.ReconstructItemHighWater(Collect(events, decoder));
        }

        /// <summary>
        /// 收集 **三流并集 ∪ <paramref name="bakedInitialItemIds"/>**(AC-20-03 的扫描集形状)。
        /// <para><b>为什么并集里要有 `BakedInitial`</b>:基线里预摆的物品实例**没有对应事件**
        /// (它们在开档前就存在,不是由 `DropSpawned` / `Craft` 铸出来的)。只扫三流会漏掉这些 id
        /// ⇒ 高水位偏低 ⇒ 读档后发重号 ⇒ 与基线预摆物**撞号**(`inventory-and-items.md:648`
        /// 的 EC 行,首轮评审 BL-1③ 把原「不可达」判为假)。</para>
        /// <para>⚠️ <b>`null` 的语义 = 「基线未提供」,等价于空集</b> ——
        /// 这**正是 R3 未落地时的现状**,也是本重载要显式暴露的东西:
        /// 传 `null` 的重建会**漏掉基线 id**,调用方必须知道自己在走哪条路。
        /// 生产接入前须先核 R3(54 导出基线的实例 id 位 + 7a 接入),
        /// **不得**在未核 R3 的情况下降级传 `null` 并当作正确。</para>
        /// </summary>
        /// <param name="events">三流并集。</param>
        /// <param name="decoder">载荷解码器(不得为 null —— 见单参重载的理由)。</param>
        /// <param name="bakedInitialItemIds">基线(`BakedInitial`)里的物品实例 id;
        /// <c>null</c> = 基线未提供(等价空集,语义见上)。</param>
        /// <returns>三流 id(流序)后接基线 id;可含重复与负哨兵,由
        /// <see cref="IdAuthority.ReconstructItemHighWater"/> 统一按「负值忽略、只升不降」处理。</returns>
        public static List<long> Collect(
            IReadOnlyList<SimEvent> events, IPayloadDecoder decoder, IEnumerable<long> bakedInitialItemIds)
        {
            List<long> ids = Collect(events, decoder);
            if (bakedInitialItemIds == null) return ids;
            foreach (long id in bakedInitialItemIds) ids.Add(id);
            return ids;
        }

        /// <summary>收集(三流 ∪ 基线)并直接重构 —— 读档路径的完整形态。</summary>
        /// <param name="events">三流并集。</param>
        /// <param name="decoder">载荷解码器。</param>
        /// <param name="bakedInitialItemIds">基线物品实例 id(<c>null</c> = 未提供,语义见上)。</param>
        /// <param name="authority">待重构的计数器。</param>
        /// <exception cref="ArgumentNullException">任一形参为 null(<paramref name="bakedInitialItemIds"/> 除外)。</exception>
        public static void CollectAndReconstruct(
            IReadOnlyList<SimEvent> events, IPayloadDecoder decoder,
            IEnumerable<long> bakedInitialItemIds, IdAuthority authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            authority.ReconstructItemHighWater(Collect(events, decoder, bakedInitialItemIds));
        }
    }
}
