// 活跃出诊目标查询 —— 规则十二 12.2 的流前缀纯函数(52 出诊启动)。
//
// 权威来源:
//   GDD random-events.md 规则十二 12.2(2026-10-25 52 GDD 轮裁定②)——
//     活跃出诊目标 = 派生自既有流,零新 Kind,三条件:
//       ① 存在最新出诊类 EventArrived(急召降临);
//       ② 目标病人未 CaseOpened(立案 = 终点 —— ADR-008「已处置 = 处置事件为证」同源);
//       ③ 未过期 + 未拒访 —— **P0 零检查**(窗口归 37,P0 = 永不过期;拒访流表达
//          = HistoryFlagChanged,hook 登记归 37 的 GDD 轮,2026-10-25 实现轮裁定③)。
//   ADR-031 —— 载荷解码唯一路径 = IPayloadDecoder(Sim 物理上够不着字节)。
//   ADR-030 —— 病人出现 = DiseaseOnset;病人 id 由 9 在急召降临 tick 创建,
//     52 从流读,**零发号**(ADR-007:52 零 IIdAuthority 调用)。
//   TD-randomevents-02 —— 本查询的兑现轮(2026-10-25)。
//
// 核心机制:
//   - **EventArrived ↔ 病人链接(裁定①,2026-10-25)**:急召 EventArrived 的
//     Envelope Tick(急召降临 tick)处**恰有一条 DiseaseOnset** —— 0 条或多条都
//     fail-loud 抛 InvalidOperationException(链接是「9 在降临 tick 创建病人」的
//     流层不变量;缺 / 重复 = 流已坏,静默取第一条 = 第二真源形态)。
//   - 「最新」按**流序**不按 tick 排序(流按 Tick 非降是写入期硬不变量;
//     DeathCellQuery 同款纪律 —— 另排一次 = 同一事实的第二份判据)。
//   - 终止判定只查**目标病人**的 CaseOpened(Envelope Patient == target ∧
//     Tick >= 急召 tick);他例的立案不影响本目标。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary><see cref="ActiveHouseCallQuery.Evaluate"/> 的出参(有活跃目标时两字段有意义)。</summary>
    public readonly struct ActiveHouseCall
    {
        /// <summary>是否存在活跃出诊目标。</summary>
        public readonly bool Active;

        /// <summary>目标病人 id(急召降临 tick 创建的那位;Active = false 时无意义)。</summary>
        public readonly int TargetPatientId;

        /// <summary>急召降临 tick(最新出诊类 EventArrived 的 Envelope Tick)。</summary>
        public readonly long SummonsTick;

        public ActiveHouseCall(bool active, int targetPatientId, long summonsTick)
        {
            Active = active; TargetPatientId = targetPatientId; SummonsTick = summonsTick;
        }
    }

    /// <summary>
    /// 活跃出诊目标查询(规则十二 12.2 的三条件纯函数;52 出诊启动的前置)。
    /// </summary>
    public static class ActiveHouseCallQuery
    {
        /// <summary>
        /// 从流判读当前是否存有活跃出诊目标(规则十二 12.2)。
        /// </summary>
        /// <param name="events">事件流只读视图(可传全流 —— 本方法按 <c>Kind</c> 自筛;
        /// <c>Kind → StreamId</c> 纯函数且 A1 断言唯一流别,ADR-024)。</param>
        /// <param name="decoder">载荷解码器(ADR-031 第八抽象点)。</param>
        /// <param name="houseCallEventKeys">出诊类事件的 event_key 闭集(烘焙配置 ——
        /// 事件表中哪些 EventArrived 是「出诊类急召」;注入而非内嵌 = 事件表数据归烘焙)。</param>
        /// <returns>判读结果;<c>Active = false</c> 时其余字段无意义。</returns>
        /// <remarks>
        /// <para><b>P0 边界(裁定③)</b>:窗口过期与拒访检查为零 —— 窗口归 37(P0 =
        /// 永不过期),拒访 hook 登记归 37 的 GDD 轮;两者落地时**改本方法**,不改调用方。</para>
        /// <para><b>死链 fail-loud(裁定①)</b>:最新出诊类 EventArrived 的降临 tick 处
        /// DiseaseOnset 非「恰一条」即抛 —— 这是 9 侧「病人创建时机」的流层不变量,
        /// 本查询是它的第一个消费者,故由查询执行断言(不另设门)。</para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">急召降临 tick 处 DiseaseOnset
        /// 为 0 条或多条(裁定① fail-loud)。</exception>
        public static ActiveHouseCall Evaluate(
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            IReadOnlyCollection<int> houseCallEventKeys)
        {
            if (decoder == null) throw new ArgumentNullException(nameof(decoder));
            if (houseCallEventKeys == null) throw new ArgumentNullException(nameof(houseCallEventKeys));
            if (events == null) return default;

            // ① 最新出诊类 EventArrived(流序最后一条胜 —— 流按 Tick 非降)。
            long summonsTick = -1;
            bool foundSummons = false;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.EventArrived)
                    continue;

                EventArrivedPayload payload =
                    decoder.Decode<EventArrivedPayload>(EventKind.EventArrived, e.Payload);

                if (!ContainsKey(houseCallEventKeys, payload.EventKey))
                    continue;

                summonsTick = e.Tick;
                foundSummons = true;   // 流序遍历,后者覆盖前者 = 最新
            }

            if (!foundSummons)
                return default;
            // ② 链接:降临 tick 处恰一条 DiseaseOnset(0 条或多条 ⇒ fail-loud,裁定①)。
            int targetPatientId = -1;
            int onsetCount = 0;
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.DiseaseOnset || e.Tick != summonsTick)
                    continue;

                DiseaseOnsetPayload payload =
                    decoder.Decode<DiseaseOnsetPayload>(EventKind.DiseaseOnset, e.Payload);

                targetPatientId = payload.PatientId;
                onsetCount++;
            }

            if (onsetCount == 0)
                throw new InvalidOperationException(
                    $"出诊链接断裂:急召降临 tick {summonsTick} 处 DiseaseOnset 为 0 条 " +
                    "(规则十二 12.3 —— 病人由 9 在急召降临 tick 创建;9 侧写者未接线或流已坏)");
            if (onsetCount > 1)
                throw new InvalidOperationException(
                    $"出诊链接歧义:急召降临 tick {summonsTick} 处 DiseaseOnset 有 {onsetCount} 条 " +
                    "(规则十二 12.3 —— 同 tick 恰一病人;链接目标不可判定,拒绝猜)");

            // ③ 终止:目标病人已 CaseOpened(立案 = 终点)⇒ 无活跃目标。
            //    P0 零窗口过期检查 + 零拒访检查(裁定③;hook 归 37)。
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                if (e.Kind != EventKind.CaseOpened)
                    continue;
                if (e.Patient.Value != targetPatientId || e.Tick < summonsTick)
                    continue;

                return default;   // 已立案 —— 目标失效(后续更新急召不在此列,最新急召已在①选中)
            }

            return new ActiveHouseCall(true, targetPatientId, summonsTick);
        }

        /// <summary>线性包含判断(IReadOnlyCollection 无 <c>Contains</c>;闭集体量小,不值得转 HashSet)。</summary>
        private static bool ContainsKey(IReadOnlyCollection<int> keys, int key)
        {
            foreach (int k in keys)
            {
                if (k == key) return true;
            }
            return false;
        }
    }
}
