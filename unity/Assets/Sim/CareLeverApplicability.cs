// GDD disease-simulation.md §F4 · `:1135-1140` —— 照护杠杆适用集(2026-10-22 立)。
//
// 权威来源:
//   GDD disease-simulation.md:1135-1140 —— 照护杠杆适用 = {伤寒 / 痢疾 / 心衰}(四轮 K8 收窄);
//     **破伤风不在其中** —— 破伤风护理**不刷新**死亡窗口(与对症药同级),
//     手柄 = 发病前清创预防;发病后越阈 + 过窗口即死(同首裁「越阈必死」)。
//   GDD disease-simulation.md:294 —— `handle ∈ {causal, symptomatic_only, care, none}`,
//     `care` = 「无对因药,但照护暂停适用 —— 仅 {伤寒 / 痢疾 / 心衰}」。
//   GDD 校验 17 —— `TREATMENT_WINDOW ≥ CARE_GAP` 的排序约束(见 F4OutcomeEvaluator)。
//
// ── 为什么单独立类(而不是塞进 F4OutcomeEvaluator)────────────────────────────
//   适用集是**一张数据表 + 一个查表判定**,与 F4 的阈值/窗口逻辑零耦合。
//   塞进求值体 ⇒ 「该不该把照护通道传进去」与「传进去之后怎么算窗口」会在同一个红里,
//   分不开。这与 `EntityDeathGates` / `LastInterventionAggregator` 从求值体拆出来的
//   理由同源(纯度 / 可单独证伪)。
//
// ── 与 `StateMachine.CareApplicableDiseases` 的关系(⚠️ 不要造第二套数据)────────
//   `StateMachine.CareApplicableDiseases` **已存在**(`StateMachine.cs:140-145`,
//   `{typhoid, dysentery, heart_failure}`),且 `IsCareApplicable(string diseaseKey)` 已是
//   它的公开查表口。本类**不新增数据**,只做两件事:
//     ① 把「裸字符串查表」收成一个**吃注册表条目**的谓词(F4 的输入面是条目,不是键名);
//     ② 把「handle 字段与适用集的一致性」显式化 —— `handle == care` ⇔ 在适用集内。
//   ⚠️ ② 是**派生关系**,不是第二真源:GDD `:294` 定义 `handle` 由注册表派生
//   (规则九:数据,不是代码),而 `handle == care` 的**充要条件**就是「在适用集内」。
//   `CareApplicableConsistent` 把这条派生关系写成可断言的方法,供注册表校验与测试使用。
//
// ── 破伤风的双重约束(K8)──────────────────────────────────────────────────────
//   `DIS_TETANUS` 的 `handle == none`(GDD `:294`),**不在**适用集内 ⇒
//   `AppliesTo` 对它恒 `false` ⇒ F4 不会把照护通道传进去 ⇒ 护理不刷新死亡窗口。
//   这条由 `test_tetanus_isNotCareApplicable` 钉住。

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim
{
    /// <summary>
    /// F4 照护杠杆的**适用集**判定 —— GDD <c>:1135-1140</c>。
    ///
    /// <para><b>为什么是纯函数</b>:承 ADR-016 §一 三源不变量 —— 只读注册表条目
    /// (版本化烘焙数据),不读墙钟 / 不读表现态 / 不读全局可变状态。</para>
    ///
    /// <para><b>⚠️ 本类只管「该不该传」,不管「传了怎么算」</b> ——
    /// 窗口计算归 <see cref="LastInterventionAggregator"/>,合取归 <see cref="F4OutcomeEvaluator"/>。</para>
    /// </summary>
    public static class CareLeverApplicability
    {
        /// <summary>
        /// 该病种是否适用照护杠杆(照护能否暂停死亡倒计时)。
        /// </summary>
        /// <param name="entry">病种注册表条目。</param>
        /// <returns>
        /// <c>true</c> ⇔ 该条目在 GDD <c>:1135-1140</c> 的适用集内
        /// ({伤寒 / 痢疾 / 心衰});<b>破伤风恒 <c>false</c></b>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        /// <remarks>
        /// <para><b>判据取 <c>DiseaseKey</c>,不取 <c>Handle</c></b>:<c>Handle</c> 是**派生字段**
        /// (写入期不填,构建期从 treatable_by 极性 / 病种 id 算出),对未过烘焙的条目
        /// (测试夹具 / legacy 占位)可能是 <c>null</c>。取 <c>DiseaseKey</c> 使本谓词对
        /// **任何**条目都良定义。一致性由 <see cref="CareApplicableConsistent"/> 另行断言。</para>
        /// </remarks>
        public static bool AppliesTo(DiseaseRegistryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return entry.DiseaseKey != null
                && StateMachine.CareApplicableDiseases.Contains(entry.DiseaseKey);
        }

        /// <summary>
        /// 按病种键名判定(与 <see cref="AppliesTo"/> 同源,供只有键名的调用方使用)。
        /// </summary>
        /// <param name="diseaseKey">注册表 <c>key</c> 字段(如 <c>"typhoid"</c>)。</param>
        /// <returns>在适用集内 ⇒ <c>true</c>;null / 空串 / 不在集内 ⇒ <c>false</c>。</returns>
        public static bool AppliesToKey(string diseaseKey)
        {
            return diseaseKey != null
                && StateMachine.CareApplicableDiseases.Contains(diseaseKey);
        }

        /// <summary>
        /// <c>handle</c> 派生字段与适用集的**一致性判据** —— GDD <c>:294</c> + 规则九。
        ///
        /// <para><c>handle == "care"</c> ⇔ 在适用集内。这是 GDD 定义的**充要条件**:
        /// 「<c>care</c>(无对因药,但照护暂停适用 —— 仅 {伤寒 / 痢疾 / 心衰})」。</para>
        /// </summary>
        /// <param name="entry">病种注册表条目。</param>
        /// <returns>
        /// <c>true</c> ⇔ 一致。⚠️ <paramref name="entry"/> 的 <c>Handle</c> 为 <c>null</c>
        /// (未过烘焙)时返回 <c>true</c> —— 「未声明」不是「不一致」,本方法不对未烘焙条目
        /// 制造假红。
        /// </returns>
        public static bool CareApplicableConsistent(DiseaseRegistryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (entry.Handle == null) return true;   // 未烘焙:无从断言
            return (entry.Handle == "care") == AppliesTo(entry);
        }
    }
}
