// S20-13 · processing story-003 —— `Craft` 世界流写者(18 炮制,主机侧生产写入通道)
//
// 权威来源:
//   design/registry/entities.yaml —— SimEvent.Kind.Craft:
//     stream: world · author: 18 炮制与加工(P0 唯一发出方)(ADR-009 §二 · Amendment J)
//     payload_schema: "actor_id: i32; recipe_id: i32(21a 表 ordinal); start_tick: i64;
//                      duration_ticks: i64; input_instance_ids[]: i64; ActualConsumed[]: i32;
//                      OutputQty[]: i32; OutputQuality[]: 枚举(品级档);
//                      output_instance_ids[]: i64; tool_cell: 格 WorldPos"
//   design/gdd/processing.md
//     · 规则七(:255-277)点火事务五步 —— ④ 交 20 验容量 / ⑤ **20 接受后**才发 `Craft`
//     · 规则七附注(:270-277)容量不足 ⇒ 原料不扣、药不出、**`Craft` 事件不落流**
//     · 规则八(:300-341)R-18-A 载荷十位 + **发起即落流**(点火 tick,非完成 tick)
//     · 规则八 R-18-E(:334-341)全序键 = 三流键 `(Tick, StreamPriority, Patient, Seq)`;
//       `Patient = PatientId.None`;`Seq` 由主机 `Append` 发号
//     · 规则八载荷表(:341)`output_instance_ids[]` **主机于点火 tick 经 `ItemInstanceId.Next()`
//       依全序键顺序铸造**并随载荷落流(ADR-010 §五;认领 20 的 R10/BL-21)
//   ADR-009 §二 / §三(世界流三态 · 模拟态进流)· Amendment J(Craft 支)
//   ADR-005(主机唯一 Append)· ADR-006(载荷 ⊆ 整数域,禁 float)
//   ADR-010 §五(instance id = 计数器 + 高水位可重构)
//   ADR-024(entities.yaml = Kind 单一真源)· ADR-029 §③(载荷唯一编码路径 = IPayloadEncoder)
//   ADR-025 §①(`Sim` 引用集恰 = {BCL, Sim.Contracts})
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**不做**什么(边界,必须与「它做了什么」同读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **不做结算** —— 三个出参来自 F1,**原样搬运**(AC-18-01② 的同引用链:18 交给 20 的
//    三出参与 F1 返回值是同一引用)。任何排序 / 去重 / 重建数组都是再加工。
//    本类只做**形状展开**:F1 的品级出参是配方级标量,而 registry 的载荷位是逐条数组
//    (`OutputQuality[]`,产出组三数组等长约束)—— 展成数组是形状适配,零新数学。
// ② **不判准入 / 不判单炉 / 不判容量** —— 三步全在上游
//    (`AdmissiblePredicate` / `FurnaceState` / `IgnitionAdmission`)。
//    本类**只在全部裁决通过后被调用**;顺序编排归 `CraftIgnitionTransaction`。
// ③ **不扣原料** —— 「后扣 input 实例」是 20 的 `Apply`(F-20.3),归 inventory-005(S20-14)。
//    本类不持任何库存投影,想扣也扣不了。
// ④ **不写完成侧** —— 完成 = 时间到达 + 出参已定(规则八),是**纯派生**;
//    起货溢出由 20 从本事件派生 `DropSpawned`(R-18-D / Amendment J ④),**18 不写该支**。
// ⑤ **不铸 `PatientId`** —— 世界流事件 `Patient = PatientId.None`(-1 哨兵),
//    不污染 `max(patient_id)` 高水位(ADR-007 §四)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么收 `in RecipeSettlementResult` 而不是三个裸数组
// ─────────────────────────────────────────────────────────────────────────────
// 编排器 `CraftIgnitionTransaction` 住在 `DaYiJingCheng.Sim.Processing`,而该命名空间受
// **三条机械门**约束:`AC-18-01③`(零第二求解实现,源文本零公式体标识符)·
// `AC-18-02`(零结算算式)。若编排器自己写 `settlement.ActualConsumed` 之类,
// 公式体标识符就出现在 18 侧源文本里 —— 门会(正确地)判它是第二实现。
// ⇒ 出参**整体**穿过编排器(它不是算术,只是传递一个值),字段名只在**本类**出现。
// 这与 `InventoryFold.cs` 消费 `Craft` 载荷字段是同性质的白名单条目(见
// `recipe_settlement_solver_test.cs` 的 `Whitelist()` ② ③ ⑤ 先例)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>
    /// `Craft` 世界流写者 —— 18 炮制的生产写入通道(主机侧,P0 唯一发出方)。
    /// </summary>
    /// <remarks>
    /// <para><b>世界流事件头</b>:<c>Patient = PatientId.None</c>(ADR-007 §四 —— 不污染
    /// <c>max(patient_id)</c> 高水位;与 <c>PoiStateChanged</c> / <c>StructurePlaced</c> /
    /// <c>ResourceHarvested</c> 同格);<c>Seq = -1</c> 未发号哨兵,由 <c>EventStream</c> 发号(O-1)。</para>
    /// <para><b>只在「20 接受后」被调用</b>(规则七 ⑤):容量不足时上游**根本不调本类**,
    /// 故「零事件」是结构性的 —— 不持写通道的拒绝路径写不出任何东西。</para>
    /// <para>⚠️ <b><c>recipe_id</c> 由调用方给</b>:<c>CraftPayload.RecipeId</c> 是 21a 表的
    /// <b>ordinal</b>(整数),而 <see cref="Recipe.RecipeId"/> 是字符串。ordinal ↔ 配方表的
    /// 映射权威住 21a(`Sim` 看不见物品 / 配方表 —— 门 A / ADR-025 §①),
    /// 本类收已解析的 ordinal,不做查表。</para>
    /// </remarks>
    public sealed class CraftEventWriter
    {
        private readonly IEventSink _eventSink;
        private readonly IPayloadEncoder _encoder;
        private readonly IIdAuthority _idAuthority;

        /// <summary>
        /// 构造 `Craft` 写者。
        /// </summary>
        /// <param name="eventSink">事件写入通道(主机唯一;生产 = 真 <c>EventStream</c>)。</param>
        /// <param name="encoder">载荷编码器(ADR-029 §③ —— 唯一合法编码路径,
        /// 本类内不得出现 <c>new PayloadRef(</c>,由 b6 门强制)。</param>
        /// <param name="idAuthority">发号权威(ADR-005 · ADR-010 §五)——
        /// <c>output_instance_ids[]</c> 于点火 tick 经 <c>NextItemInstanceId()</c> 铸造。</param>
        /// <exception cref="ArgumentNullException">任一依赖为 null。</exception>
        public CraftEventWriter(IEventSink eventSink, IPayloadEncoder encoder, IIdAuthority idAuthority)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));
            _idAuthority = idAuthority ?? throw new ArgumentNullException(nameof(idAuthority));
        }

        /// <summary>
        /// 记一条点火事实(规则七 ⑤)—— **主机裁决全部通过后**调用。
        /// </summary>
        /// <param name="tick">点火 tick(规则八「发起即落流」)。</param>
        /// <param name="actorId">点火玩家 id(载荷归因位 R-18-A ①;20 的 <c>InventoryOf(player)</c>
        /// 折叠前提)。</param>
        /// <param name="recipeId">配方 ordinal(21a 表编号;非 <see cref="Recipe.RecipeId"/> 字符串)。</param>
        /// <param name="durationTicks">烘焙时长(<c>Recipe.DurationTicks</c>;恒 ≥ 1 —— 归 21a 数据表,
        /// 本件只做入参合法性 fail-loud)。</param>
        /// <param name="inputInstanceIds">被点名的输入实例集(<c>InQ</c> 与实耗的输入;
        /// 与结算出参的实耗数组**逐位对位**)。</param>
        /// <param name="settlement">F1 的唯一出参 —— **原样进载荷**,不复制不排序(AC-18-01②)。</param>
        /// <param name="toolCell">器具 / 丹房所在的**整数格**(ADR-015 <c>WorldPos</c>;
        /// 与 <c>EnvMod</c> 锚点同一格,不重采样)。</param>
        /// <returns>本次铸造的产出实例 id(按产出量索引序 ——
        /// 确定性次序 = 全序键可重放的必要条件)。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="inputInstanceIds"/> 为 null,
        /// 或结算出参的三个数组任一为 null(<c>default(RecipeSettlementResult)</c> 的形态)。</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="tick"/> &lt; 0 · <paramref name="recipeId"/> &lt; 0 ·
        /// <paramref name="durationTicks"/> &lt; 1 · 品级 &lt; 1 · 实耗与输入实例不等长。
        /// </exception>
        /// <remarks>
        /// <para><b>为什么三数组必须非 null</b>:<c>CraftPayload</c> 的编解码侧
        /// (<c>PayloadCodec.World.cs</c>)对 null 与不等长**双向拒收** —— 写侧在这里提前抛
        /// 比让它进 codec 再炸更可诊断(且「零产出 = 合法」在 21a 已被非空约束排除,
        /// <c>max(1, ·)</c> 是产出地板)。</para>
        /// <para>⚠️ <b>只借测 <c>RecipeSettlementResult</c> 的类型,不引用任何公式体的实现</b> ——
        /// 本类全是搬运与守卫,零乘除、零舍入、零钳制。</para>
        /// </remarks>
        public ItemInstanceId[] Record(
            long tick,
            int actorId,
            int recipeId,
            long durationTicks,
            IReadOnlyList<long> inputInstanceIds,
            in RecipeSettlementResult settlement,
            WorldPos toolCell)
        {
            if (tick < 0)
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick 不得为负");
            if (recipeId < 0)
                throw new ArgumentOutOfRangeException(nameof(recipeId), recipeId,
                    "recipe_id 是 21a 表 ordinal,不得为负");
            if (durationTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(durationTicks), durationTicks,
                    "GDD 规则八:duration_ticks 须 > 0(21a AC-21a-18 拒收 ≤ 0)");
            if (inputInstanceIds == null)
                throw new ArgumentNullException(nameof(inputInstanceIds),
                    "未传入被点名的输入实例集 —— 拒绝以空集冒充「没投料」");

            int[] consumed = settlement.ActualConsumed;
            int[] produced = settlement.OutputQty;
            int quality = settlement.OutputQuality;

            if (consumed == null)
                throw new ArgumentNullException(nameof(settlement) + "." + nameof(settlement.ActualConsumed),
                    "结算出参的实耗数组为 null(含 default(RecipeSettlementResult))—— " +
                    "拒绝以空载荷冒充「没消耗」(AC-18-11)");
            if (produced == null)
                throw new ArgumentNullException(nameof(settlement) + "." + nameof(settlement.OutputQty),
                    "结算出参的产出量数组为 null —— 产出组三数组须等长,空值会在 codec 侧炸");
            if (consumed.Length != inputInstanceIds.Count)
                throw new ArgumentOutOfRangeException(nameof(settlement), consumed.Length,
                    $"输入组不等长:input_instance_ids = {inputInstanceIds.Count} 而实耗 = " +
                    $"{consumed.Length} —— 20 的逐实例扣减依赖逐位对位");
            // ⚠️ 品级守卫**只在有产出行时施加**:零产出组没有「每一行的品级」可言,
            // 而 F1 在空 `Outputs` 上仍会算出一个配方级品级标量(F2 只读 `InputQuality` /
            // 技能等级,不读产出数组)。若在此无条件抛,一个合法形状的请求会被一条
            // **误导性**的理由(「品级越界」)拒掉 —— 那是把形状问题伪装成语义问题。
            // (2026-10-27 自查:原稿无条件抛,由 `test_zeroOutputRecipe_mintsNoIds` 抓到。)
            if (produced.Length > 0 && quality < 1)
                throw new ArgumentOutOfRangeException(nameof(settlement), quality,
                    "out_quality ∈ [1, MAX_QUALITY](F2 品级出口)");

            // 输入组:实例 id 摊平成 i64[](registry: input_instance_ids[]: i64)。
            var inputIds = new long[inputInstanceIds.Count];
            for (int i = 0; i < inputInstanceIds.Count; i++)
                inputIds[i] = inputInstanceIds[i];

            // 产出组:配方级品级标量展成逐条数组(形状适配,零求值 —— 每条取同一出参值)。
            var qualityPerLine = new int[produced.Length];
            for (int k = 0; k < produced.Length; k++)
                qualityPerLine[k] = quality;

            // 产出实例 id 铸造:**按产出行索引序**逐个 Next()(规则八载荷表 / story Note 3:
            // 确定性次序 = 全序键可重放的必要条件)。空产出组 ⇒ 零次调用(不铸空号)。
            // ⚠️ 铸造结果**以 `ItemInstanceId` 保存**,不在本类重新包一次(2026-10-27 全量回归订正)。
            // 原实现写 `= _idAuthority.NextItemInstanceId().Value` 再在末尾 `new ItemInstanceId(...)`
            // 包回来 —— 那个 `new` 文字面撞上 AC-21a-63 / AC-17-05 的**手工铸造字面门**
            // (`id_authority.cs` 词表 ① / `HarvestOrchestratorTest` 同判据),一次无谓往返换来两条红。
            // 载荷位仍是整数域(`CraftPayload.OutputInstanceIds` = `long[]`),故只在装箱处展开 `.Value`。
            var minted = new ItemInstanceId[produced.Length];
            for (int k = 0; k < produced.Length; k++)
                minted[k] = _idAuthority.NextItemInstanceId();

            var outputIds = new long[minted.Length];
            for (int k = 0; k < minted.Length; k++)
                outputIds[k] = minted[k].Value;

            var payload = new CraftPayload(
                actorId,
                recipeId,
                tick,                 // start_tick = 点火 tick(规则八「发起即落流」)
                durationTicks,
                inputIds,
                consumed,             // F1 出参原样(同一引用)—— AC-18-01②
                produced,             // F1 出参原样(同一引用)—— AC-18-01②
                qualityPerLine,
                outputIds,
                toolCell);

            var payloadRef = _encoder.Encode(EventKind.Craft, payload);

            // 事件头:Patient = None(世界流,不污染高水位 —— 与 ResourceHarvested 同格);
            // Seq = -1 未发号哨兵(O-1),由发号器给出。
            _eventSink.Append(new SimEvent(
                tick, PatientId.None, -1, EventKind.Craft, payloadRef));

            return minted;
        }
    }
}
