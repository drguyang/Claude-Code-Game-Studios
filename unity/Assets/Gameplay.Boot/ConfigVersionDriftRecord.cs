// AC-7a-14 末句 · ADR-010 §七 —— 「`ConfigVersion` 不匹配 ⇒ 已发生事件不受影响、
// **仅记录并供排查**」的**记录面**。
//
// 权威来源:
//   · `design/gdd/persistence-service.md:581-586`(AC-7a-14 原文)
//   · ADR-010 §七(「存档头记 `ConfigVersion` 以便排查回放不符」;**不匹配非致命**)
//   · ADR-014 §五(`ConfigVersion` = 源文本集内容哈希派生 · u32)
//   · `Telemetry/ConfigVersionGate.cs`(`ConfigVersionCheck` —— 比对与标注的**既有**载体,
//     本类不重写它,只负责「记录下来」这一步)
//
// ── 为什么需要本类(而不是让 `LoadSequencer` 直接拼字符串)──
// 「比对」(`ConfigVersionCheck.Match/Banner`)与「记录」是两件事:
//   · 比对 = 纯函数,已有载体(`ConfigVersionCheck`),零状态;
//   · 记录 = 需要**留在一个地方可回看**(`LastCheck` / `DriftCount`)并**送达排查通道**
//     (`Action<string>` 注入 —— 生产 = 引擎日志,夹具 = 内存列表)。
// 把二者合进一个既有类型会让「有没有记录过」这件事寄居在纯函数里;拆开后,
// 「记录器被调用过几次」是可断言的,**记录的内容**也可断言。
//
// ── 刻意不做的事(边界)──
// 1. **不判「哪一张表变了」**:档头只有一个组合后的 u32(ADR-014 §五),从它**推不出**
//    逐表差异。要做逐表差异须存档携带逐份版本(布局升级,`SaveVersion` v3)——
//    那是一次独立的裁决(候选方案 D),**不在本类的表达能力内**。本类只说
//    「这份档是另一个配置版本写下的」,并给出两个版本号。
// 2. **不改任何状态**:不写三流、不重算、不改后续窗口用的配置(ADR-010 §七 明文)。
// 3. **不做节流**:每次成功读档至多一条日志。读档是低频动作,节流会把「这次也漂移了」
//    抹掉,而排查恰恰要看「是不是每次都漂」。

using System;
using DaYiJingCheng.Sim.Contracts;   // ConfigVersionDiff / ArtifactConfigVersion(ADR-037)
using DaYiJingCheng.Telemetry;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// `ConfigVersion` 漂移的记录面(AC-7a-14 · ADR-010 §七)。
    /// <para>每次成功读档喂一次 <see cref="ConfigVersionCheck"/>;不一致时计一次漂移并
    /// 把 <see cref="ConfigVersionCheck.Banner"/> 送进注入的日志通道。</para>
    /// <para>⚠️ **只记录,不施策**:不抛、不改流、不重算(非致命是 ADR-010 §七 的裁决)。</para>
    /// </summary>
    public sealed class ConfigVersionDriftRecord
    {
        private readonly Action<string> _log;

        /// <param name="log">日志通道(生产 = 引擎日志;夹具 = 内存收集器)。
        /// <c>null</c> ⇒ 只留 <see cref="LastCheck"/> / <see cref="DriftCount"/> 两件可读状态
        /// (仍需有人读 ⇒ 仅测试的部分装配用)。</param>
        public ConfigVersionDriftRecord(Action<string> log = null)
        {
            _log = log;
        }

        /// <summary>最近一次读档的比对结果(<c>null</c> = 尚未读过任何档)。</summary>
        public ConfigVersionCheck? LastCheck { get; private set; }

        /// <summary>最近一次读档的**逐表差异**(ADR-037 §④)。<c>HasPerArtifactData == false</c>
        /// = 本档不含逐份版本,**无法归因** —— 不得读成「无差异」。</summary>
        public ConfigVersionDiff LastDiff { get; private set; }

        /// <summary>至今记录的**不匹配**次数(一致不计)。</summary>
        public int DriftCount { get; private set; }

        /// <summary>
        /// 记录一次比对。一致 ⇒ 只更新 <see cref="LastCheck"/>(不出日志、不加计数);
        /// 不一致 ⇒ 计数 +1 并把标注行送出。
        /// </summary>
        /// <param name="check">比对结果(<c>ConfigVersionCheck</c> 是 struct ⇒ 无 null 形态)。</param>
        /// <param name="diff">逐表差异(ADR-037 §④)。<c>Unknown</c> ⇒ 日志明写「无法归因」。</param>
        public void Record(ConfigVersionCheck check, ConfigVersionDiff diff)
        {
            LastCheck = check;
            LastDiff = diff;
            // 组合值不一致,或组合值相同但逐份对不上(理论上不该发生 —— 同源派生;
            // 真发生时说明写侧把两个来源拆开了,须报出来而不是按「一致」吞掉)。
            bool drifted = !check.Match || diff.HasChanges;
            if (!drifted) return;

            DriftCount++;
            _log?.Invoke(Describe(check, diff));
        }

        /// <summary>一行排查文本:两版本号 + 逐表归因(或「无法归因」)。</summary>
        private static string Describe(ConfigVersionCheck check, ConfigVersionDiff diff)
        {
            var sb = new System.Text.StringBuilder(check.Banner());
            if (!diff.HasPerArtifactData)
            {
                sb.Append("# 逐表归因:本档不含逐份版本(早于 v3 或写入时未提供)⇒ **无法归因**;" +
                          "只能按两侧组合值重算比对(ADR-037 §④)\n");
                return sb.ToString();
            }

            if (diff.Changed.Count == 0)
            {
                sb.Append("# 逐表归因:逐份版本**全等**(组合值差异来自段外数据 / 派生口径)\n");
                return sb.ToString();
            }

            sb.AppendFormat("# 逐表归因:变动 {0} 张 —— ", diff.Changed.Count);
            for (int i = 0; i < diff.Changed.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(diff.Changed[i]);
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
