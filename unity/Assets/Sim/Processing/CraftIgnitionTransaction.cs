// S20-13 · processing story-003 —— 点火事务编排(规则七五步 · 原子落流)
//
// 权威来源:
//   design/gdd/processing.md
//     · 规则七(:255-277)点火事务五步:
//         ① 准入两门(规则三)→ ② 单炉占用检查(规则八)→ ③ 调 F1 得三出参(规则五)
//         → ④ 交 20 `Apply(consumed, produced)` 验容量 → ⑤ **20 接受后**发 `Craft`
//     · 规则七附注(:270-277)「④ / ⑤ 是一个原子 `Step`」;**容量不足 ⇒ 原料不扣、药不出、
//       `Craft` 不落流**;**18 不得先扣后补**(部分成功破 ADR-005 可序列化前提)
//     · 规则八(:300-341)发起即落流 / 完成 = 纯派生 / 单炉 / 不可取消
//     · 规则八并发模型表(:343-349)每玩家 1 炉;第二炉 ⇒ 主机拒绝、**零事件**
//     · AC-18-12(点火时点容量不足 ⇒ 零 `Craft` 事件)· AC-18-16(落流时点 + 十位 + 铸造 + 序键)
//   ADR-005 —— 事件流唯一真源 ⇒ 「拒绝」= **零 Append**,不是「写一条拒绝事件」
//   ADR-025 §① —— `Sim` 引用集恰 = {BCL, Sim.Contracts}
//   story production/epics/processing/story-003-craft-payload-ignite-append.md
//     · Implementation Note 1(点火事务序)· Note 3(铸造次序)· Note 5(`tool_cell` 同格)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本类**不做**什么(边界,必须与「它做了什么」同读)
// ─────────────────────────────────────────────────────────────────────────────
// ① **零算术**(AC-18-02)。本类不乘、不除、不取整、不钳制 —— 所有数值要么是上游给的值,
//    要么是**原样传递**。`Sim.Processing` 命名空间的全命名空间 IL 扫描
//    (`test_ac1802_il_noArithmeticInProcessingNamespace`)对 `mul`/`div`/`rem` 与
//    定点算术成员调用零容忍,本类不得成为这条 AC 的例外。
// ② **不碰 F1 出参的字段名**(AC-18-01③)。出参以 `in RecipeSettlementResult` **整体**
//    穿过本类,字段解包只在写者 `Sim.World.CraftEventWriter` 内发生 —— 这样
//    「18 侧源文本零公式体标识符」那条门
//    (`test_ac1801_noSecondSettlementImplementationInProcessingAssembly`)才成立,
//    而不是靠白名单把本类豁免掉。
// ③ **不持写通道**。本类零 `IEventSink` / 零 `IPayloadEncoder` 字段 —— 写者以参数注入,
//    且**只在第 ⑤ 步被调用**。拒绝路径拿到的是一个从未被调用的 `writer`,
//    「零事件」因此是**结构性的**,不是靠 if 守的纪律。
// ④ **不判准入的语义**。第 ① 步委托 `AdmissiblePredicate.TerminalVerdict`(AC-18-20:
//    与客户端预答**同一函数**),本类只消费它的答案。
// ⑤ **不判「正在烧」的区间开闭**。第 ② 步委托 `FurnaceState.IsBusy`,区间口径归该件
//    (半开 `[start, start+duration)`,2026-10-26 评审 W2 已注明那是实现选择而非 GDD 判据)。
// ⑥ **不扣原料**。第 ④ 步只做**容量预检**(`IgnitionAdmission.CanAdmit`),它是
//    `Apply` 的**前置谓词**,不是 `Apply` 本身 —— 「后扣 input 实例」需要 20 的库存投影,
//    归 inventory-005(S20-14)。⚠️ **该半边未落,故本类不能宣称「20 的 Apply 已接」**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么第 ④ 步是 `CanAdmit` 而不是 `Apply`
// ─────────────────────────────────────────────────────────────────────────────
// `IgnitionAdmission` 类头自己写了这条边界(`:18-19`):
//   「不调用 20 的 `Apply` —— 该入口归 inventory-005(S20-14),本件交付的是**谓词**。
//    调用方在 S20-14 落地后应把 `CanAdmit` 作为 `Apply` 的**前置**,而不是替代它:
//    `Apply` 还要扣原料 / 铸产出实例 / 落地溢出,那三件都超出「装不装得下」。」
// 本类照此把 `CanAdmit` 接成**真生产调用点**(在此之前它零调用方),`Apply` 落地后
// 只需把第 ④ 步换成 `Apply` 并保留 `CanAdmit` 作为它的第一句 —— 事务序不变。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么 unit weight 是委托注入的
// ─────────────────────────────────────────────────────────────────────────────
// `IgnitionAdmission.OutputLoadLine` 要 `unitWeight`,而 weight 的权威住 21a 物品表
// (`Sim` 看不见物品表 —— 门 A / ADR-025 §①)。与 `CarryLoadSolver.Sum` 收
// `Func<int,int> weightOfOrdinal` 是**同一纪律的两种形态**:ordinal → weight 的解析
// 留在边界层,本类只按产出行索引问一次。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Sim.Processing
{
    /// <summary>点火被拒的**具名理由** —— 每一条都对应规则七 / 规则八的一步失败。</summary>
    /// <remarks>
    /// <para>⚠️ <b>为什么不是裸 <c>bool</c></b>:五步在玩家侧是不同处境 ——
    /// 技能不够 = 「练到 X 级再来」;炉子忙 = 「等这一炉起货」;背包装不下 = 「先腾背包」。
    /// 合成一个 <c>false</c> 会让 42 的面板无法给出可行动的反馈。</para>
    /// <para>⚠️ 本枚举**不承载任何幅值信息**(承 `AdmissionRejection` 同款纪律):
    /// 它只说「哪一步没过」,不说「差多少」。后者会诱使调用方拿去做调制 —— AC-18-05 要防的形态。</para>
    /// </remarks>
    public enum CraftRejection
    {
        /// <summary>五步全过,`Craft` 已落流。</summary>
        None = 0,

        /// <summary>① 准入:技能门槛未达。</summary>
        AdmissionSkillGate = 1,

        /// <summary>① 准入:品级准入闸未达。</summary>
        AdmissionMinQuality = 2,

        /// <summary>① 准入:被点名的实例集为空(<c>min</c> over ∅ 无定义)。</summary>
        AdmissionNoConsumedInstances = 3,

        /// <summary>② 单炉:该玩家已有进行中的炉。</summary>
        FurnaceBusy = 4,

        /// <summary>④ 容量:本次产出装不下(R-18-D 的**点火时点**拒绝).</summary>
        CapacityInsufficient = 5,
    }

    /// <summary>点火请求 —— 五步所需的全部输入(纯数据 + 一个解析委托)。</summary>
    public readonly struct CraftIgnitionRequest
    {
        /// <summary>F1 的供料参数包(Story 002 件;含配方 / 玩家 / 品级 / 三修正)。</summary>
        public readonly ProcessingInputs Supply;

        /// <summary>点火 tick(规则八「发起即落流」:事件 tick 与本字段**同一个值**)。</summary>
        public readonly long Tick;

        /// <summary>配方 ordinal(21a 表编号;载荷位 <c>recipe_id</c>)。</summary>
        public readonly int RecipeId;

        /// <summary>器具 / 丹房所在的**整数格**(R-18-A ③;与 `EnvMod` 锚点同格,不重采样)。</summary>
        public readonly WorldPos ToolCell;

        /// <summary>被点名的输入实例集(`InQ` 与实耗的输入;与 F1 实耗逐位对位)。</summary>
        public readonly IReadOnlyList<long> InputInstanceIds;

        /// <summary>**被点名实例**的品级集(F-18.2 补注①:不是 `inputs[]` 行 ——
        /// 20 规则六允许跨栈凑量点名,行级 min 会静默放行差料)。</summary>
        public readonly IReadOnlyList<int> InputQualities;

        /// <summary>点火前该玩家的载重(≥ 0;由 20 的 `CarryLoad` 求得)。</summary>
        public readonly long CurrentCarryLoad;

        /// <summary>产出行索引 → 单件重量(21a 物品表的解析留在边界层 —— 见文件头)。</summary>
        public readonly Func<int, int> UnitWeightOfOutputLine;

        /// <summary>构造点火请求。</summary>
        /// <param name="supply">F1 供料参数包。</param>
        /// <param name="tick">点火 tick。</param>
        /// <param name="recipeId">配方 ordinal。</param>
        /// <param name="toolCell">器具格。</param>
        /// <param name="inputInstanceIds">被点名的输入实例集。</param>
        /// <param name="inputQualities">被点名实例的品级集。</param>
        /// <param name="currentCarryLoad">点火前的载重。</param>
        /// <param name="unitWeightOfOutputLine">产出行索引 → 单件重量。</param>
        public CraftIgnitionRequest(
            in ProcessingInputs supply,
            long tick,
            int recipeId,
            WorldPos toolCell,
            IReadOnlyList<long> inputInstanceIds,
            IReadOnlyList<int> inputQualities,
            long currentCarryLoad,
            Func<int, int> unitWeightOfOutputLine)
        {
            Supply = supply;
            Tick = tick;
            RecipeId = recipeId;
            ToolCell = toolCell;
            InputInstanceIds = inputInstanceIds;
            InputQualities = inputQualities;
            CurrentCarryLoad = currentCarryLoad;
            UnitWeightOfOutputLine = unitWeightOfOutputLine;
        }
    }

    /// <summary>点火结果 —— 被拒时点名**哪一步**,被接受时带上铸造出的产出实例 id。</summary>
    public readonly struct CraftIgnitionOutcome
    {
        /// <summary>是否已落流。</summary>
        public readonly bool Ignited;

        /// <summary>拒绝理由(<see cref="Ignited"/> 为 true 时 = <see cref="CraftRejection.None"/>)。</summary>
        public readonly CraftRejection Rejection;

        /// <summary>本次铸造的产出实例 id(按产出行索引序);被拒时为空数组。
        /// <para>⚠️ 返回它只是**诊断便利**(调用方要立刻拿 id 做事)—— 权威副本是
        /// 流上的 `Craft` 载荷(<c>output_instance_ids[]</c>),不在本结构里。</para>
        /// </summary>
        public readonly ItemInstanceId[] OutputInstanceIds;

        /// <summary>构造。</summary>
        /// <param name="ignited">是否已落流。</param>
        /// <param name="rejection">拒绝理由。</param>
        /// <param name="outputInstanceIds">铸造出的产出实例 id。</param>
        public CraftIgnitionOutcome(bool ignited, CraftRejection rejection,
                                    ItemInstanceId[] outputInstanceIds)
        {
            Ignited = ignited; Rejection = rejection;
            OutputInstanceIds = outputInstanceIds ?? Array.Empty<ItemInstanceId>();
        }

        /// <inheritdoc />
        public override string ToString() =>
            Ignited ? "Ignited(" + (OutputInstanceIds?.Length ?? 0) + " outputs)"
                    : "Rejected:" + Rejection;
    }

    /// <summary>
    /// 点火事务 —— 规则七五步的**唯一编排点**(AC-18-12 / AC-18-16)。
    /// <para><b>纯编排</b>:零静态可变状态、零 I/O、零墙钟。它的全部判据都委托给
    /// 各自的所有者(准入 / 单炉 / F1 / 容量),本类只定**次序**与**原子性**。</para>
    /// </summary>
    public static class CraftIgnitionTransaction
    {
        /// <summary>
        /// 执行一次点火事务。任一步被拒 ⇒ **零事件**(不调写者),并返回具名理由。
        /// </summary>
        /// <param name="request">点火请求(五步输入齐)。</param>
        /// <param name="constants">结算常量表(纯数据)。⚠️ 调用方义务:**先**过
        /// <see cref="ProcessingSettlementInputs.ValidateConstantTable"/> ——
        /// `Settle` 不替调用方校验(2026-10-27 评审 A-3)。</param>
        /// <param name="skillLevels">30 的只读等级查询。</param>
        /// <param name="events">事件流只读视图(单炉判据的前缀源)。</param>
        /// <param name="decoder">载荷解码器(`FurnaceState` 的唯一读路径,ADR-031)。</param>
        /// <param name="capacity">容量上限(已定值的 `CarryCapacity`)。</param>
        /// <param name="solver">F1 的唯一求解器(生产侧 = <c>ProcessingSettlementEntry.Solver</c>)。</param>
        /// <param name="writer">`Craft` 写者 —— **只在第 ⑤ 步被调用**。</param>
        /// <returns>点火结果。</returns>
        /// <exception cref="ArgumentNullException">任一必填依赖为 null(含
        /// <paramref name="request"/> 的两集合与 <see cref="CraftIgnitionRequest.UnitWeightOfOutputLine"/>)。</exception>
        /// <remarks>
        /// <para><b>失败路径不调 F1 的次数上限</b>:① 不过 ⇒ F1 零调用;② 不过 ⇒ F1 零调用
        /// (单炉检查在结算**之前** —— 白烧一次的代价是玩家的原料,不是 CPU)。
        /// 只有 ① ② 都过才调 <see cref="ProcessingSettlementInputs.Settle"/>,且恰 1 次。</para>
        /// <para>⚠️ <b>第 ④ 步只是 `Apply` 的容量半边</b>(见文件头 ⑥):本类**不扣原料**、
        /// **不铸产出实例的库存行**、**不落溢出** —— 那三件归 inventory-005(S20-14)。
        /// 因此本方法的「原子」保证范围 = 「(准入, 单炉, 结算, 容量) 四步全过 ⇒ 恰一条事件;
        /// 任一不过 ⇒ 零事件」。**它不是**「20 的完整装卸事务已落地」。</para>
        /// </remarks>
        public static CraftIgnitionOutcome Process(
            in CraftIgnitionRequest request,
            in RecipeSettlementConstants constants,
            ISkillLevelQuery skillLevels,
            IReadOnlyList<SimEvent> events,
            IPayloadDecoder decoder,
            CarryCapacity capacity,
            RecipeSettlementFn solver,
            CraftEventWriter writer)
        {
            if (request.InputInstanceIds == null)
                throw new ArgumentNullException(nameof(request) + "." + nameof(request.InputInstanceIds),
                    "未传入被点名的输入实例集 —— 拒绝以空集冒充「没投料」");
            if (request.InputQualities == null)
                throw new ArgumentNullException(nameof(request) + "." + nameof(request.InputQualities),
                    "未传入被点名实例的品级集 —— 拒绝当作「空集」放行(AC-18-10 的静默失效)");
            if (request.UnitWeightOfOutputLine == null)
                throw new ArgumentNullException(nameof(request) + "." + nameof(request.UnitWeightOfOutputLine),
                    "未传入产出的单件重量解析 —— 拒绝当作「零重」放行(AC-18-12 的静默失效)");
            if (writer == null)
                throw new ArgumentNullException(nameof(writer),
                    "未注入 `Craft` 写者 —— 拒绝静默跳过落流(那会让玩家白烧一炉)");

            // ── ① 准入两门(规则七 ①;F-18.2 的唯一判据,AC-18-20 同一函数)──
            AdmissionRejection admission = AdmissiblePredicate.Judge(
                in request.Supply.Recipe, request.Supply.ActorId,
                request.InputQualities, skillLevels);

            if (!AdmissiblePredicate.IsAdmitted(admission))
                return Rejected(admission);

            // ── ② 单炉占用(规则七 ②;规则八并发模型:每玩家 1 炉)──
            //    ⚠️ 本轮 `events` 必须已含截至本 tick 的全部 `Craft` —— 写者尚未写入本条,
            //       故前缀语义天然正确(判的是「点火前」的占用)。
            if (FurnaceState.IsBusy(events, decoder, request.Supply.ActorId, request.Tick))
                return Rejected(CraftRejection.FurnaceBusy);

            // ── ③ 调 F1 得三出参(规则五;恰 1 次,出参整体转发)──
            RecipeSettlementResult settlement = ProcessingSettlementInputs.Settle(
                in request.Supply, in constants, skillLevels, solver);

            // ── ④ 交 20 验容量(规则七 ④;R-18-D:拒绝在**点火 tick**)──
            //    ⚠️ 只判「装不装得下」。扣原料 / 铸库存行 / 落地溢出归 S20-14 的 `Apply`。
            IgnitionAdmissionResult admissionResult = IgnitionAdmission.CanAdmit(
                request.CurrentCarryLoad,
                OutputLinesOf(settlement, request.UnitWeightOfOutputLine),
                capacity);

            if (!admissionResult.Admitted)
                return Rejected(CraftRejection.CapacityInsufficient);

            // ── ⑤ 20 接受后才 Append(规则七 ⑤;原子:此前零事件)──
            ItemInstanceId[] minted = writer.Record(
                request.Tick,
                request.Supply.ActorId,
                request.RecipeId,
                request.Supply.Recipe.DurationTicks,
                request.InputInstanceIds,
                in settlement,
                request.ToolCell);

            return new CraftIgnitionOutcome(true, CraftRejection.None, minted);
        }

        /// <summary>判据拒绝 ⇒ 具名结果(零事件:写者**从未被调用**)。</summary>
        private static CraftIgnitionOutcome Rejected(AdmissionRejection admission)
        {
            switch (admission)
            {
                case AdmissionRejection.SkillGate:
                    return new CraftIgnitionOutcome(false, CraftRejection.AdmissionSkillGate, null);
                case AdmissionRejection.QualityGate:
                    return new CraftIgnitionOutcome(false, CraftRejection.AdmissionMinQuality, null);
                case AdmissionRejection.NoConsumedInstances:
                    return new CraftIgnitionOutcome(false, CraftRejection.AdmissionNoConsumedInstances, null);
                default:
                    // None 不该走到这里(`IsAdmitted` 已过滤)—— 但若走到,
                    // 按「准入不明」拒绝而不是放行(fail-closed)。
                    throw new InvalidOperationException(
                        $"[炮制] 准入裁决出现未预期值 {admission} —— 拒绝放行(fail-closed)");
            }
        }

        /// <summary>非准入类的拒绝(单炉 / 容量)。</summary>
        private static CraftIgnitionOutcome Rejected(CraftRejection rejection)
            => new CraftIgnitionOutcome(false, rejection, null);

        /// <summary>把 F1 的产出量逐行配上单件重量 —— 交给 `IgnitionAdmission` 的载重输入。</summary>
        /// <remarks>
        /// ⚠️ 本方法**只读产出量**,不读品级 / 效率 / 乘子 —— 载重与那些量无关。
        /// 逐行问一次 weight 解析(21a 的权威),不在本地缓存。
        /// </remarks>
        private static IReadOnlyList<OutputLoadLine> OutputLinesOf(
            in RecipeSettlementResult settlement, Func<int, int> unitWeightOfOutputLine)
        {
            int[] produced = settlement.OutputQty;
            var lines = new OutputLoadLine[produced.Length];
            for (int i = 0; i < produced.Length; i++)
                lines[i] = new OutputLoadLine(unitWeightOfOutputLine(i), produced[i]);
            return lines;
        }
    }
}
