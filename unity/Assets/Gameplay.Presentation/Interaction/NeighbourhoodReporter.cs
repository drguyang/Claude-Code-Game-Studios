// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— F-4.3(广播式自报)/ F-4.3b(自报与 argmin 解耦)
//     / F-4.4(有界性)/ 规则四 ③ / 4-DC-1(R_INTERACT 单源)
//   ADR-016 §三(粗粒度整数格;事件率上界 = tick 频率,**与帧率无关**)
//   ADR-020 §四 对称(4 的出境是意图,不是位移投影)/ ADR-009 §七(三段式的意图半)
//   ADR-021(PoiCell → 6 唯一写者;4 只请求)
//
// ⚠️ 三处承重(逐字承故事 Context):
//   ① **广播式自报与 argmin 解耦**(F-4.3b):邻域内**全部** PoiCell 各自自报 ——
//      **不因在目标选择中落选被吞**;否则 POI 优先级高于/低于 argmin 目标时出现「发现饿死」,
//      且「发现」变相成了选择函数的副产品(本类型**不接触 argmin**,结构上不可能耦合)。
//   ② **latch 一律住拥有方**(AC-4-13):本类型**零**「报过了」可变字段 ——
//      每 tick 至多一条 `Request` 由 **6 侧 per-tick latch + 幂等**吸收。本类型是纯函数。
//   ③ **`R_INTERACT` 单源**(AC-4-17):邻域半径从注入的 `InteractionRadius` 取 ——
//      与 6 的触发半径同一内存值,**本类型不持半径常量**。
//
// ⚠️ 有界性(F-4.4):|Request / tick / 玩家| ≤ min(邻域 POI 数, (2·R+1)³) ——
//   与帧率无关:本类型每次调用 = 一次**求值**(一 tick 一次),调用频率由 `ITickProvider` 定,
//   不由渲染帧定。帧率翻倍 ⇒ 调用次数不变 ⇒ 每 tick 计数不变。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 邻域广播自报器(F-4.3 / F-4.3b / F-4.4)。
    /// <para><b>广播</b>:邻域(<c>d∞(player_cell, poi_cell) ≤ R_INTERACT</c>)内的**每个**
    /// <see cref="InteractableKind.PoiCell"/> 候选各发一条 <see cref="IDiscoveryReporter.Request"/>
    /// —— 与 argmin 胜者**无因果**(落选 POI 照样自报,不饿死)。</para>
    /// <para><b>纯函数 / 零记账</b>(AC-4-13):本类型无可变字段,不记「报过了」——
    /// 重复由 **6 的 per-tick latch + 幂等**吸收(责任面在 6)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>无状态</b>(AC-4-04 同族):只有 <c>readonly</c> 实例字段(依赖引用),
    /// 同 (邻域候选, 玩家格, tick) ⇒ 同出境序列。</para>
    /// <para><b>本类不持半径常量</b> —— 半径来自注入的 <see cref="InteractionRadius"/>
    /// (AC-4-17 单源:与 6 共享同一烘焙字段,此处**不得**出现第二个数)。</para>
    /// </remarks>
    public sealed class NeighbourhoodReporter
    {
        // ⚠️ AC-4-13 结构半边的落点:这里刻意**没有**任何 `_reported` / `_lastTick` / latch 可变字段。
        //   若有人加一个「报过了」记账字段 ⇒ 反射扫描(ScanForMutableState,承 story-001 AC-4-04)即红。
        private readonly IDiscoveryReporter _discoveryReporter;
        private readonly InteractionRadius _radius;

        /// <summary>构造邻域广播自报器。</summary>
        /// <param name="discoveryReporter">发现上报面(4 的唯一出境通道)。</param>
        /// <param name="radius">交互半径(**单源** —— 与 6 共享;本类不自持常量)。</param>
        public NeighbourhoodReporter(IDiscoveryReporter discoveryReporter, InteractionRadius radius)
        {
            _discoveryReporter = discoveryReporter;
            _radius = radius;
        }

        /// <summary>
        /// 对邻域内**全部** POI 候选广播自报(F-4.3b)。
        /// <para>⚠️ <b>不接触 argmin</b> —— 输入是候选集 + 玩家格,不是「选中的目标」;
        /// 结构上不可能变成「胜者才报」。</para>
        /// <para>⚠️ <b>本方法自足求值 —— 每次调用恰代表一次 tick 求值</b>(调用频率由
        /// <c>ITickProvider</c> 定,**不由渲染帧定**;本类型不持时钟)。因此 AC-4-13 的
        /// 「发出次数 = 帧数」与 F-4.4 的「每 tick 发出次数 = 邻域 POI 数(与帧率无关)」
        /// 是<b>同一条断言</b> —— 都由 <c>tick</c> 计调用次数,无需对「帧」建模。
        /// 帧率翻倍 ⇒ 同一 tick 内<b>不</b>多调 ⇒ 出境计数不变(有界性的直接实测形态,
        /// 见 <c>discovery_report_test.test_f44_frameRateDoublingDoesNotChangeEgress</c>)。</para>
        /// <para>⚠️ <b>触发前提是主动交互</b>(规则七 / EC-6-2 / AC-4-12):本方法<b>不</b>自取
        /// <c>InteractIntent</c>,调用方须仅在<b>玩家主动交互</b>时驱动它。F-4.3 上文的「不按键也报?
        /// —— <b>否</b>:仍须玩家主动交互」是<b>调用方义务</b>:4 的单选路径
        /// (<see cref="InteractionSelector.Select"/>)已按 <c>intent.Pressed</c> 门控,广播路径
        /// <b>应在同一意图门内</b>发起(见 <c>test_ac413_negativeFixture_absenceOfIntentYieldsZeroEgress</c>)。
        /// 「玩家走进 POI 格但无输入 ⇒ 零 <c>Request</c>」(AC-4-12)由该负夹具钉死。</para>
        /// </summary>
        /// <param name="playerCell">玩家**经流确立格**(ADR-020 §四 读方半边)。</param>
        /// <param name="candidates">候选集(story 003 的四源输出;本方法只筛 PoiCell 且在邻域内者)。</param>
        /// <param name="tick">本次求值的逻辑 tick(由调用方经 <c>ITickProvider</c> 供给)。</param>
        /// <returns>本次出境的 `Request` 条数(≤ 邻域 POI 数 ≤ (2R+1)³;供有界性夹具断言)。</returns>
        public int ReportNeighbourhood(in WorldPos playerCell, IReadOnlyList<Candidate> candidates, long tick)
        {
            // ⚠️ 触发门(规则七):本方法**只在玩家主动交互时被调用** —— 它不自取 `InteractIntent`,
            //   故「无输入 ⇒ 零出境」是**调用方**的契约。本类型保持纯函数(零记账,AC-4-13),
            //   门控住调用方(4 的 Select 已门控;广播路径须同门)。
            if (candidates == null || candidates.Count == 0) return 0;

            int sent = 0;
            int r = _radius.Value;

            // ⚠️ 全整数三重循环的等价形态:对每个候选格判 d∞ ≤ R(切比雪夫球 [-R,R]³)。
            //   零分配、零 float、零 Vector3(ADR-016 §三)。
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (c.Kind != InteractableKind.PoiCell) continue;
                if (Chebyshev(playerCell, c.Cell) > r) continue;

                _discoveryReporter.Request(new DiscoveryRequest(c.StableId, playerCell, tick));
                sent++;
            }
            return sent;
        }

        /// <summary>两格 Chebyshev 距离 <c>d∞ := max(|Δx|,|Δy|,|Δz|)</c>(与 F-4.1 第一键同式)。
        /// <para>⚠️ 先拓宽 int64 再取绝对值(int 域相减会在 <c>int.MinValue</c> 处回绕 —— 承 story 002)。</para></summary>
        private static long Chebyshev(in WorldPos a, in WorldPos b)
        {
            long dx = System.Math.Abs((long)a.X - b.X);
            long dy = System.Math.Abs((long)a.Y - b.Y);
            long dz = System.Math.Abs((long)a.Z - b.Z);
            long m = dx > dy ? dx : dy;
            return m > dz ? m : dz;
        }
    }
}
