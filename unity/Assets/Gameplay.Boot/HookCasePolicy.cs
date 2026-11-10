// 钩子病例的**准入判据**(T5a · 2026-11-09)
//
// 权威来源:design/gdd/reviews/technique-unlock-map-2026-11-09.md §5.2(用户 2026-11-09 裁定)
//   规则 1 数量:每章 1–3 例
//   规则 2 诊断必须可完成 —— 「看不懂 + 治不了」是双重挫败
//   规则 3 必须指出缺的是哪一门技法(数据面见 `PatientDrawDecision.MissingTechniques`)
//   规则 4 不点破获得地点(呈现层的事,本类不管)
//
// ── 本类只判「这一例能不能作为钩子出现」,不判「它是不是钩子」──────────────
//   前者 = 本类(配额 + 可诊断性);后者 = 调用方的抽取时机(抽中被技法门拒的病种)。
//   分开的理由:配额是有状态的计数(见 `HookCaseLedger`),可诊断性是**纯数据属性**;
//   把两者塞进一个函数会让「数据坏了」与「配额用完了」共用同一个 false。
//
// ── 配额:1 而不是 3(本件就地裁定)────────────────────────────────────────
//   §5.2 的设计区间是 1–3;`§8 R4` 明写「P0 按章内 1 例起步,上限随章节长度调」。
//   本件取 **1**,并把区间写进注释 —— 调整只改 `MaxPerChapter` 一个常量。
//
// ── ⚠️ 「章」在运行期不存在(如实登记)──────────────────────────────────────
//   全仓零章节状态(无 GDD 字段、无存档字段、无事件)。`HookCaseLedger` 因此只数
//   **全局**钩子病例;接线恒取 `chapterOf() == 0` ⇒ 「每章 1 例」退化为「整局 1 例」。
//   这是**具名缺口**(见证据件 §未做),不是本类的判据缺陷:判据本身接受任意章号。
//
// 🔁 **2026-11-10(B1-b,用户裁定「新增 ChapterAdvanced 事件」)** —— 上一段的
//   「无事件」已不成立:`EventKind.ChapterAdvanced` 已入 registry,写者 =
//   `ChapterAdvanceWriter`,读侧 = `HookCaseLedger.CurrentChapter()` /
//   `CountHookCasesInCurrentChapter()`,装配已改走章内口径。
//   ⚠️ **但缺口本身仍未消失**:P0 尚无叙事系统发那条事件 ⇒ 章号恒 0 ⇒
//   「每章 1 例」在**今日运行时**仍等于「整局 1 例」。变的是**载体就位**,
//   不是**行为生效** —— 解除条件 = 叙事轮接上生产者(奇遇宿主)。
//
// 🔁 **2026-11-10(T20)再订正** —— 生产者已接:`PatternRecognitionDriver` 在每条新的
//   `PatternRecognized`(37 规则八:三例同源)之后推进一章 ⇒ 章号不再恒 0,
//   「每章 1 例」在**机制上**成立(第 2 个图样被识破 ⇒ 第 2 例钩子可投放)。
//   ⚠️ 口径 = **一章 = 一个被识破的图样**(机制口径,非叙事口径);叙事宿主到位后若改由剧情
//      推进,只换 `PatternRecognitionDriver` 的 `TryAdvance` 调用点,本类一字不动。

using System;
using DaYiJingCheng.Sim;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>钩子病例的准入判据(纯函数 + 一个可调常量)。</summary>
    public static class HookCasePolicy
    {
        /// <summary>
        /// 每章至多几例。设计区间 **1–3**(map §5.2 规则 1);P0 取 **1**(map §8 R4
        /// 「P0 按章内 1 例起步」)。
        /// </summary>
        public const int MaxPerChapter = 1;

        /// <summary>
        /// 该病种**能不能被看明白**(§5.2 规则 2)。
        /// <para>判据 = `signs[]` 非空,且至少一条体征落在**体格通道**上
        /// (排除 `Subjective` / `History` —— 8 的 `DiagnosisChannelMaskMap.MaskOf`
        /// 对这两位刻意返回 0,它们不驱动任何可见体征)。</para>
        /// <para>⚠️ 只用 9 自己的通道词表(`SignChannels`),不读 8 的阈值表 ——
        /// 「能不能看出来」在数据面是**有没有可看的东西**,不是「8 判不判得出」。</para>
        /// </summary>
        public static bool IsDiagnosable(DiseaseRegistryEntry row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (row.Signs == null || row.Signs.Length == 0) return false;

            const int nonPhysical = SignChannels.Subjective | SignChannels.History;
            for (int i = 0; i < row.Signs.Length; i++)
            {
                SignEntry s = row.Signs[i];
                if (s == null) continue;
                if ((s.ChannelMask & ~nonPhysical) != 0) return true;
            }
            return false;
        }

        /// <summary>
        /// 在已注入 <paramref name="injectedInChapter"/> 例的前提下,这一例能否作为钩子出现。
        /// </summary>
        /// <param name="row">病种行(非 null)。</param>
        /// <param name="injectedInChapter">本章**已**注入的钩子病例数(见 <see cref="HookCaseLedger"/>)。</param>
        /// <returns>可诊断 **且** 本章配额未满。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="row"/> 为 null。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="injectedInChapter"/> 为负。</exception>
        public static bool AllowsInjection(DiseaseRegistryEntry row, int injectedInChapter)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (injectedInChapter < 0)
                throw new ArgumentOutOfRangeException(nameof(injectedInChapter), injectedInChapter,
                    "已注入计数不得为负 —— 负数会让「配额已满」的判据恒假");
            return injectedInChapter < MaxPerChapter && IsDiagnosable(row);
        }
    }
}
