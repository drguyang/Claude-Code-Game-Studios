// 权威来源:11-DC 的 DC-2(action_id 闭集 = 处置 id 注册表全值)· DC-6(相邻档药效差 ≥ NOISE_BAND_9)
//          · GDD design/gdd/prescription-and-medication.md §11-DC 表 + §Data Contracts 注
//          · OQ-11-2(✅ 已裁 2026-10-06:映射归 11,不移入 21a)
//          · BL-2(O-11→9,`NOISE_BAND_9` 未立)· BL-7(O-11→21a,`drug_potency` 域未声明)
//
// ⚠️⚠️ **本件是影子注册表,不是真源** —— 两条判据的**真源都不存在**,理由逐条登记如下。
//      本件存在的唯一目的:把「校验机制」建成可跑的机器,让判据在真源落地后**只换数据不换代码**。
//
// ⚠️ **DC-2 的真源缺席**:GDD `:748` 逐字写「该枚举的 master 住哪一份文件**未登记**」。
//    实测:9 侧 `disease_registry.json` **不存在**(`treatable_by[]` 的动作轴无载体);
//    全库无 `ACT_*` 符号;`Sim.EmergencyProcedures.EmergencyAction` **不是**该轴
//    (它是 10 的急救动作,2 值,语义不同 —— 拿它当闭集 = 张冠李戴)。
//    ⇒ **本件的 `action_id` 闭集是合成夹具,不是 9 的注册表。**
//    `OQ-11-2` 只裁了「映射住 11」(归属),**未产出该枚举的 master** ⇒ DC-2 仍 `NOT-RUN`。
//
// ⚠️ **DC-6 的真源缺席**:`NOISE_BAND_9` 在 9 侧**零定义**(BL-2 / `D-9-J` ⏳ 待认领)。
//    ⇒ 门槛值**无主**;本件所载 `value_raw` 是**影子值**,只为让机制可跑。
//    ⚠️ **量纲纪律(TR-prescription-019)**:本值住**药效幅值域**(Q16.16 raw),
//       **不得**被读成 9 的 σ(Progress 域)或 21a 品级地板(tick 域)—— 三域不同轴。
//
// ⚠️ **诚实边界**:本件**不把 NOT-RUN 洗成绿**。`Bind` 的返回值携 `ShadowRegistryUsed`,
//    供调用方与构建日志**显式报出「本次烘焙用的是影子值」** —— 与 story-003 的
//    「判据-文本背离」同款记账纪律:背离可见,不静默。
//
// ⚠️⚠️ **影子期不得硬失败(2026-10-06 实测踩中)**:本件最初把两条判据的发现直接并入
//    `errors`(即构建门硬失败),结果**打断了 `test_singleDoseMax_tracksDoseRangeHiChange`**
//    —— 该测的合成夹具(`drug_potency = 1/2`、`dose_range = [1,4]`)相邻档差 ≈ 0.5 raw,
//    低于影子地板 100 ⇒ 被判违规。**这正是 GDD 点名的失效模式「判据对合法输入类误判」**:
//    用一个**没有主人的门槛值**去拒绝数据。⇒ 现口径:**影子期只出 `Warnings`**,
//    真源(`NOISE_BAND_9` / 处置 id master)落地后由调用方**显式升格为硬失败**。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>处置 id 注册表的一行(影子)。</summary>
    public readonly struct ActionRegistryRow
    {
        /// <summary>处置 id(闭集成员)。</summary>
        public readonly int ActionId;

        /// <summary>符号名(仅供诊断输出;真源落地后应取自该 master)。</summary>
        public readonly string Name;

        /// <summary>该处置声明的极性(causal / symptomatic)。</summary>
        public readonly string Polarity;

        public ActionRegistryRow(int actionId, string name, string polarity)
        {
            ActionId = actionId; Name = name; Polarity = polarity;
        }
    }

    /// <summary>
    /// **影子**处置 id 注册表 + DC-2 / DC-6 的校验机制。
    /// <para>⚠️ 真源缺席(BL-2 / DC-2 的 master 未登记)⇒ 本件所载闭集与地板值**均为合成夹具**。
    /// 判据本体维持 `NOT-RUN`,禁借绿。</para>
    /// <para>⚠️ **影子期输出走 `Warnings`,不走 `errors`** —— 用没有主人的门槛值硬失败 =
    /// GDD 点名的「判据对合法输入类误判」。升格为硬失败须待真源落地。</para>
    /// </summary>
    public static class PrescriptionActionIdRegistry
    {
        /// <summary>影子地板值 —— **不是** `NOISE_BAND_9`(后者归 9,未立)。</summary>
        public const long ShadowPerceptibleFloorRaw = 100L;

        /// <summary>真源落地的**可执行钉子**:处置 id master 的约定文件名。
        /// <para>⚠️ 该文件**当前全库不存在** —— 这正是 DC-2 仍 `NOT-RUN` 的物证。
        /// 它的存在性被 <c>test_shadowRegistry_realRegistryFileStillAbsent</c> 断言:
        /// **一旦该文件出现,该测即红**,强制更新登记与 <see cref="ShadowRows"/>。
        /// (承 ADR-014 阶段 2 的 per-schema 数据面。)</para></summary>
        public const string RealRegistryFileName = "prescription_action_registry.json";

        // ── 影子注册表(合成夹具;真源落地后整体替换为 9 的 master 读取)──────────

        // ⚠️ `Name` 一律带 `SHADOW_` 前缀 —— 本件文件头自陈「全库无 ACT_* 符号」,
        //    故**不得**用 ACT_ 前缀命名(会被误读成 9 的注册表片段)。
        private static readonly ActionRegistryRow[] ShadowRows =
        {
            new ActionRegistryRow(1, "SHADOW_SYNTHETIC_ACTION_1", "symptomatic"),
            new ActionRegistryRow(2, "SHADOW_SYNTHETIC_ACTION_2", "causal"),
        };

        /// <summary>影子闭集(供测试注入 / 诊断)。</summary>
        public static IReadOnlyList<ActionRegistryRow> ShadowRegistry => ShadowRows;

        /// <summary>影子闭集的 id 集(Ordinal 无关 —— 纯整数)。</summary>
        public static HashSet<int> ShadowActionIds()
        {
            var set = new HashSet<int>();
            foreach (var r in ShadowRows) set.Add(r.ActionId);
            return set;
        }

        // ── DC-2:action_id 闭集 ────────────────────────────────────────────

        /// <summary>
        /// **DC-2 校验机制**:`action_id` ∈ 注册表闭集。
        /// <para>⚠️ 现以**影子闭集**驱动 ⇒ 判据本体 `NOT-RUN`(真源 = 9 的处置 id master,未登记)。
        /// 本方法证的是「机制可跑 + 负夹具可红」,不是「11 的表已合规」。</para>
        /// </summary>
        /// <param name="actionId">处方表读入的处置 id。</param>
        /// <param name="registry">闭集(缺省 = 影子;测试可注入)。</param>
        /// <param name="fieldPresent">该行是否**真的**读到了 `action_id` 字段。
        /// <para>⚠️ `ReadInt` 失败时回退 0 并另记 error;此时**不得**再报「0 ∉ 闭集」——
        /// 那是**字段缺失**,不是值为 0。传 false 即静默返回(缺失已由读件层记账)。</para></param>
        /// <returns>错误列表(空 = 通过)。</returns>
        public static List<string> ValidateActionId(int actionId, ISet<int> registry = null,
                                                   bool fieldPresent = true)
        {
            if (!fieldPresent) return new List<string>();   // 字段缺失 ⇒ 已由读件层记账,不重复失真

            var errs = new List<string>();
            ISet<int> set = registry ?? ShadowActionIds();

            if (set.Count == 0)
            {
                errs.Add("[DC-2] 处置 id 闭集为空 —— 拒以空集冒充绿。");
                return errs;
            }

            if (!set.Contains(actionId))
                errs.Add($"[DC-2] 违反:action_id = {actionId} ∉ 处置 id 注册表闭集" +
                         $"({string.Join(", ", set)})—— 9 的 treatable_by 引用到空动作 ⇒ " +
                         "该药任何病种都不对症,而 11 侧仍照发事件(静默失败)。");

            return errs;
        }

        // ── DC-6:可感知地板 ────────────────────────────────────────────────

        /// <summary>
        /// **DC-6 校验机制**:逐药相邻档 `dose_potency` 差 ≥ 地板。
        /// <para>⚠️ 地板值现为**影子**(BL-2:`NOISE_BAND_9` 归 9,未立)⇒ 判据本体 `NOT-RUN`。
        /// 求值一律经 <c>DoseCalculator</c>(F-11.1 的唯一实现,AC-11-02 —— 本件不重写 F-11.1)。</para>
        /// </summary>
        /// <param name="drugPotencyRaw">药效幅值(Q16.16 raw)。</param>
        /// <param name="range">剂量域(空 = 整剂路径,无档差可判 ⇒ 通过)。</param>
        /// <param name="doseBase">DOSE_BASE(&gt; 0)。</param>
        /// <param name="floorRaw">地板 raw(缺省 = 影子值)。</param>
        /// <returns>错误列表(空 = 通过)。</returns>
        public static List<string> ValidatePerceptibleFloor(
            long drugPotencyRaw, Sim.Contracts.DoseRange? range, int doseBase,
            long floorRaw = ShadowPerceptibleFloorRaw)
        {
            var errs = new List<string>();
            if (!range.HasValue) return errs;   // 整剂路径:单档 ⇒ 无相邻档
            if (doseBase <= 0)
            {
                errs.Add($"[DC-6] DOSE_BASE = {doseBase} 非正 —— 判据输入非法(先由 DC-7 拦)。");
                return errs;
            }

            // ⚠️ 逆序域 [hi, lo] ⇒ `DifferenceSequence` 的 count = hi − lo ≤ 0 ⇒ 空序列
            //    ⇒ 判据**静默恒通过**(缺陷实测:42 侧 `DentchDoseSelector` 对同一输入显式抛错,
            //    11 侧此前无守卫)。此处**不得**静默返回 —— 空序列被当绿正是「看起来绿但没跑」。
            if (range.Value.Min > range.Value.Max)
            {
                errs.Add($"[DC-6] dose_range = [{range.Value.Min}, {range.Value.Max}] 逆序(下界 > 上界)" +
                         " —— 域非法 ⇒ 判据无法求值(拒以空档序列冒充绿)。");
                return errs;
            }

            // ⚠️ 符号:域未声明(BL-7)⇒ 与 `PrescriptionDerivedBaker` 同口径**取绝对值**
            //    (`PrescriptionDerivedBaker.cs` 的 single_dose_max 派生对同一源字段取 abs)。
            //    两条消费同源字段的路径符号处理必须一致,否则合法负值药被误判违反。
            long magnitudeRaw = drugPotencyRaw < 0 ? -drugPotencyRaw : drugPotencyRaw;
            var potency = new Sim.Contracts.Fix(magnitudeRaw);
            long[] deltas = Sim.PerceptibleFloorComparator.DifferenceSequence(
                potency, range.Value, doseBase);

            if (deltas.Length == 0) return errs;  // 单档域 ⇒ 空序列(真空真,非借绿)

            if (!Sim.PerceptibleFloorComparator.SatisfiesFloor(deltas, floorRaw))
            {
                long min = long.MaxValue;
                foreach (long d in deltas) if (d < min) min = d;
                errs.Add($"[DC-6] 违反:相邻档最小差值 = {min} < 地板 {floorRaw}(raw)" +
                         " —— 「调剂量」在场上不可感知,AC 全过而玩法为假(静默失败)。" +
                         "⚠️ 本次用的是**影子地板**(真源 NOISE_BAND_9 归 9,未立,BL-2)。");
            }

            return errs;
        }
    }
}
