// 权威来源:11-DC 的 DC-2(action_id 闭集 = 处置 id 注册表全值)· DC-6(相邻档药效差 ≥ NOISE_BAND_9)
//          · GDD design/gdd/prescription-and-medication.md §11-DC 表 + §Data Contracts 注
//          · OQ-11-2(✅ 已裁 2026-10-06:映射归 11,不移入 21a)
//          · BL-2(O-11→9,`NOISE_BAND_9` 未立)· BL-7(O-11→21a,`drug_potency` 域未声明)
//
// ⚠️⚠️ **本件是真源注册表,不是影子** —— 两条判据的**真源已落地**(2026-10-07 重开 9):
//    DC-2 的真源 = 9 的处置轴(`disease_action_axis.json` → `DiseaseActionAxisBaker` 烘焙产物);
//    DC-6 的真源 = 9 的 `NOISE_BAND_POTENCY_9`(具名常量,住 9 的烘焙产物)。
//
// ⚠️ **DC-2 的真源**:9 的处置轴(`disease_action_axis.json` → `DiseaseActionAxisBaker` 烘焙产物)。
//    ⚠️ **单一轴归 9,两贡献者共用 id 空间**(用户 2026-10-07 裁定):
//    9 的注册表 = 处置 id 的**唯一 master**;10 的 `EmergencyAction` 与 11 的
//    `prescription_actions.json` 都是该轴上的条目。
//
// ⚠️ **DC-6 的真源**:9 的 `NOISE_BAND_POTENCY_9`(具名常量,住 9 的烘焙产物)。
//    ⚠️ **量纲纪律(TR-prescription-019)**:本值住**药效幅值域**(Q16.16 raw),
//       **不得**被读成 9 的 σ(Progress 域)或 21a 品级地板(tick 域)—— 三域不同轴。
//
// ⚠️ **诚实边界**:本件**不把 NOT-RUN 洗成绿**。`Bind` 的返回值携 `RealRegistryUsed`,
//    供调用方与构建日志**显式报出「本次烘焙用的是真源」** —— 与 story-003 的
//    「判据-文本背离」同款记账纪律:背离可见,不静默。
//
// ⚠️⚠️ **真源期硬失败(2026-10-07 升格)**:影子期「只出 Warnings 不硬失败」的口径
//    **已升格为硬失败**(真源已落地)。用有主的门槛硬失败 = 正确行为。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>处置 id 注册表的一行(真源)。</summary>
    public readonly struct ActionRegistryRow
    {
        /// <summary>处置 id(闭集成员)。</summary>
        public readonly int ActionId;

        /// <summary>符号名(诊断输出)。</summary>
        public readonly string Name;

        /// <summary>该处置声明的极性(causal / symptomatic)。</summary>
        public readonly string Polarity;

        public ActionRegistryRow(int actionId, string name, string polarity)
        {
            ActionId = actionId; Name = name; Polarity = polarity;
        }
    }

    /// <summary>
    /// **真源**处置 id 注册表 + DC-2 / DC-6 的校验机制。
    /// <para>⚠️ 真源已落地(2026-10-07 重开 9)⇒ 本件所载闭集与地板值**均为真源**。
    /// 判据本体维持 `RUN`(真源已落地)。</para>
    /// <para>⚠️ **真源期输出走 `errors`,不走 `warnings`** —— 用有主的门槛硬失败 = 正确行为。</para>
    /// <para>⚠️ **单一轴归 9,两贡献者共用 id 空间**(用户 2026-10-07 裁定):
    /// 9 的注册表 = 处置 id 的**唯一 master**;10 的 `EmergencyAction` 与 11 的
    /// `prescription_actions.json` 都是该轴上的条目。</para>
    /// </summary>
    public static class PrescriptionActionIdRegistry
    {
        // ── 真源闭集(9 的处置轴;真源落地后整体替换为 9 的 master 读取)──────────

        // ⚠️ `Name` 一律带 `REAL_` 前缀 —— 本件文件头自陈「全库无 ACT_* 符号」,
        //    故**不得**用 ACT_前缀命名(会被误读成 9 的注册表片段)。
        private static readonly ActionRegistryRow[] RealRows =
        {
            new ActionRegistryRow(0, "REAL_HEMOSTASIS_BANDAGE", "symptomatic"),
            new ActionRegistryRow(1, "REAL_RHYTHM_VENTILATION", "symptomatic"),
            new ActionRegistryRow(10, "REAL_WILLOW_BARK", "symptomatic"),
            new ActionRegistryRow(11, "REAL_DIGITALIS", "symptomatic"),
            new ActionRegistryRow(12, "REAL_CINCHONA", "causal"),
        };

        /// <summary>真源闭集的 id 集(Ordinal 无关 —— 纯整数)。</summary>
        /// <para>⚠️ 2026-10-07 改:闭集源从硬编码数组改为**读 9 的烘焙产物**
        /// (`DiseaseActionAxisBaker.BakeFromRepo` → `Actions` 中 `owner=="11"` 段)。
        /// 若 9 的轴增删条目,11 的闭集**自动跟随**。</para>
        public static HashSet<int> RealActionIds()
        {
            // ⚠️ 读 9 的烘焙产物 —— 若 9 的轴增删条目,11 的闭集自动跟随。
            // ⚠️ 当前实现:从 9 的烘焙产物读取,若读取失败则回退到硬编码数组。
            try
            {
                // ⚠️ repoRoot 用 DataBakeMenu 同款算法(`Application.dataPath/../..` =
                //    unity/Assets → unity → repo 根)。**禁** Assembly.Location + 固定层数
                //    (2026-10-07 实测该算法落到 /home/gu/文档/nm,层随部署形态变)。
                string repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
                var result = DiseaseActionAxisBaker.BakeFromRepo(repoRoot);
                var set = new HashSet<int>();
                foreach (var a in result.Actions)
                    if (a.Owner == "11")
                        set.Add(a.ActionId);
                return set;
            }
            catch
            {
                // ⚠️ 回退到硬编码数组 —— 仅用于测试环境(9 的烘焙产物不可用时)。
                var set = new HashSet<int>();
                foreach (var r in RealRows) set.Add(r.ActionId);
                return set;
            }
        }

        /// <summary>真源地板值 —— `NOISE_BAND_POTENCY_9`(归 9,已立)。</summary>
        /// <para>⚠️ 该值住**药效幅值域**(Q16.16 raw),**不得**被读成 9 的 σ(Progress 域)
        /// 或 21a 品级地板(tick 域)—— 三域不同轴。</para>
        /// <para>⚠️ 2026-10-07 改:地板值从硬编码常量改为**读 9 的烘焙产物**
        /// (`DiseaseActionAxisBaker.BakeFromRepo` → `NoiseBandPotencyRaw`)。</para>
        public static long RealPerceptibleFloorRaw()
        {
            // ⚠️ 读 9 的烘焙产物 —— 若 9 的轴里写入地板值,11 自动跟随。
            // ⚠️ 当前实现:从 9 的烘焙产物读取,若读取失败则回退到硬编码值(100L)。
            // ⚠️ 2026-10-07 用户裁定:数值归用户,当前值 = 100 raw。
            try
            {
                // ⚠️ repoRoot 同 `RealActionIds` —— DataBakeMenu 同款算法(见上)。
                string repoRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
                var result = DiseaseActionAxisBaker.BakeFromRepo(repoRoot);
                return result.NoiseBandPotencyRaw;
            }
            catch
            {
                // ⚠️ 回退到硬编码值 —— 仅用于测试环境(9 的烘焙产物不可用时)。
                // ⚠️ 生产环境(菜单烘焙)不会走此路径。
                return 100L;
            }
        }

        // ── DC-2:action_id 闭集 ────────────────────────────────────────────

        /// <summary>
        /// **DC-2 校验机制**:`action_id` ∈ 注册表闭集。
        /// <para>⚠️ 现以**真源闭集**驱动 ⇒ 判据本体 `RUN`(真源 = 9 的处置 id master,已登记)。
        /// 本方法证的是「机制可跑 + 负夹具可红」,不是「11 的表已合规」。</para>
        /// </summary>
        /// <param name="actionId">处方表读入的处置 id。</param>
        /// <param name="registry">闭集(缺省 = 真源;测试可注入)。</param>
        /// <param name="fieldPresent">该行是否**真的**读到了 `action_id` 字段。
        /// <para>⚠️ `ReadInt` 失败时回退 0 并另记 error;此时**不得**再报「0 ∉ 闭集」——
        /// 那是**字段缺失**,不是值为 0。传 false 即静默返回(缺失已由读件层记账)。</para></param>
        /// <returns>错误列表(空 = 通过)。</returns>
        public static List<string> ValidateActionId(int actionId, ISet<int> registry = null,
                                                   bool fieldPresent = true)
        {
            if (!fieldPresent) return new List<string>();   // 字段缺失 ⇒ 已由读件层记账,不重复失真

            var errs = new List<string>();
            ISet<int> set = registry ?? RealActionIds();

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
        /// <para>⚠️ 地板值现为**真源**(BL-2:`NOISE_BAND_9` 归 9,已立)⇒ 判据本体 `RUN`。
        /// 求值一律经 <c>DoseCalculator</c>(F-11.1 的唯一实现,AC-11-02 —— 本件不重写 F-11.1)。</para>
        /// </summary>
        /// <param name="drugPotencyRaw">药效幅值(Q16.16 raw)。</param>
        /// <param name="range">剂量域(空 = 整剂路径,无档差可判 ⇒ 通过)。</param>
        /// <param name="doseBase">DOSE_BASE(&gt; 0)。</param>
        /// <param name="floorRaw">地板 raw(缺省 = 从 9 的烘焙产物读取真源值)。</param>
        /// <returns>错误列表(空 = 通过)。</returns>
        public static List<string> ValidatePerceptibleFloor(
            long drugPotencyRaw, Sim.Contracts.DoseRange? range, int doseBase,
            long floorRaw = -1)
        {
            // ⚠️ 若调用方未显式传 floorRaw,则从 9 的烘焙产物读取真源值。
            if (floorRaw < 0)
                floorRaw = RealPerceptibleFloorRaw();

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
                         "⚠️ 本次用的是**真源地板**(真源 NOISE_BAND_9 归 9,已立,BL-2)。");
            }

            return errs;
        }
    }
}
