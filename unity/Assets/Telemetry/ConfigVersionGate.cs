// 权威来源:`design/gdd/telemetry-analytics.md`
//   · §Edge Cases E-10(存档来自不同 ConfigVersion —— ADR-010 §七 版本比对不符)
//   · `AC-51-D7`(BLOCKING):GIVEN 存档头 `ConfigVersion` 与当前内容哈希不符,WHEN 离线重算,
//     THEN 报告**含显式「版本不匹配」标注**、**不静默出数**;
//     断言**不存在**「版本不符 ⇒ 自动按当前版本重算」的路径
// ADR-010 §一(`SaveHeader.ConfigVersion` = u32 存档头字段)· ADR-010 §七 / ADR-014 §五
//   (「启动时比对产物 ConfigVersion 与存档头 ConfigVersion:**不匹配非致命**,启动期仅记录并据此排查回放不符」)
//
// ═══════════════════════════════════════════════════════════════════════════════
// ⚠️ **TD-epic-01 D7 还债件(2026-10-19)**:本 AC 原记 NOT-RUN,理由「51 无比对路径」属实,
//    但**外部依赖其实已存在** —— `Sim.Codec/SaveHeader.cs` 早已落地,`ConfigVersion` 是现成字段。
//
// ⚠️ **为何本类只收两个 u32 而不收 `SaveHeader`**:`Telemetry.asmdef` 的引用集恰 =
//    `{Sim.Contracts}`(`noEngineReferences: true`),**引不到 `Sim.Codec`**。
//    这与 `JudgmentStreamReducer` 用**解码委托**而非直引 `Sim.Codec` 是同一条边界纪律:
//    装配层负责从存档里取出 `header.ConfigVersion`,51 只负责**比对与标注**。
//
// ⚠️ **「不静默出数」的落点**:不匹配时**不从存档重算出数字** —— 报告只出标注与
//    两版本号,指标区一律不出。这同时兑现 AC 末句「断言**不存在**『版本不符 ⇒
//    自动按当前版本重算』的路径」:本类**没有**任何按当前版本重算的分支。
// ═══════════════════════════════════════════════════════════════════════════════

namespace DaYiJingCheng.Telemetry
{
    /// <summary>存档头 `ConfigVersion` 与当前烘焙产物的比对结果(两个 u32,无浮点 —— R7)。
    /// <para>⚠️ 只做**比对与标注**,不改任何数据、不触发任何重算(ADR-010 §七「不匹配非致命」)。</para></summary>
    public readonly struct ConfigVersionCheck
    {
        /// <summary>存档头携带的 `ConfigVersion`(ADR-010 §一 头部字段)。</summary>
        public readonly uint SaveHeaderConfigVersion;

        /// <summary>当前烘焙产物的 `ConfigVersion`(ADR-014 §五 内容哈希派生)。</summary>
        public readonly uint CurrentConfigVersion;

        public ConfigVersionCheck(uint saveHeaderConfigVersion, uint currentConfigVersion)
        {
            SaveHeaderConfigVersion = saveHeaderConfigVersion;
            CurrentConfigVersion = currentConfigVersion;
        }

        /// <summary>两版本一致。</summary>
        public bool Match => SaveHeaderConfigVersion == CurrentConfigVersion;

        /// <summary>AC-51-D7 的标注行。一致 ⇒ **空串**(不出标注,报告与原样逐字相同)。
        /// <para>⚠️ 不匹配时的措辞须同时说清三件事:① 不匹配 ② 两个版本号 ③ **不出数**的原因。</para></summary>
        public string Banner()
        {
            if (Match) return string.Empty;
            return string.Format(
                "# ⚠️ 版本不匹配:存档头 ConfigVersion = {0},当前产物 ConfigVersion = {1}" +
                " —— 本报告**不出指标**(ADR-010 §七:回放可能不符;请对照原版本重算)\n",
                SaveHeaderConfigVersion, CurrentConfigVersion);
        }
    }
}
