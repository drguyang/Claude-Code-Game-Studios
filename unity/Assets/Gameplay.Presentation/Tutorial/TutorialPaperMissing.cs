// T8(2026-11-09)· 手册缺页集合的**判定**半边 —— 哪些纸页该被判为「已经被撕掉」。
//
// 权威来源:
//   · `design/gdd/reviews/technique-unlock-map-2026-11-09.md` §5.2 —— 钩子病例的四条规则里,
//     第 3 条「必须指出缺的是哪一门技法」;载体 = 手册缺页(只剩标题)。
//   · T6(`17067116`)—— `PaperCloseupScreen.SetMissingPaperIds` 只**渲染**缺页态,
//     「哪一页缺」不归它(承 ADR-013 §9 C3:42 只渲染,永不持有游戏状态)。
//   · ADR-043 §D7 —— 解锁状态的真源是 `ITechniqueUnlockQuery`(病史流折叠投影),
//     本类**不缓存、不另存**,只把「查表」这个动作做成一行纯函数。
//
// ─────────────────────────────────────────────────────────────────────────────
// 为什么要有这一件(而不是让某个屏自己判)
// ─────────────────────────────────────────────────────────────────────────────
// 缺页判定有两个容易写错的自由度:
//   ① **无技法的纸**(如「望诊四要」)永远不该缺 —— 写成「查不到解锁 ⇒ 缺」会让整个手册全白;
//   ② **查表失败 / 未接线**不能被当成「未解锁」—— 那会把「系统没接上」伪装成「设计如此」。
// 把这两条钉在一个纯函数里,夹具可以直接证伪;散在屏里则只能靠人读代码。
//
// ⚠️ **本类不读流、不持有状态**:`isTechniqueUnlocked` 由调用方注入(生产 = `ITechniqueUnlockQuery`)。
//    这与 `PatientDrawEligibility.Decide` 收 `Func<int,bool>` 是同一种形状(装配与夹具单一出处)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Presentation.Tutorial
{
    /// <summary>手册缺页集合的判定(纯函数;不读流、不缓存)。</summary>
    public static class TutorialPaperMissing
    {
        /// <summary>
        /// 求「应当被判为缺页」的纸条目 id 集。
        /// </summary>
        /// <param name="papers">48 内容表的 <c>papers[]</c>;null 或空集 ⇒ 返回空集(没有纸就没有缺页)。</param>
        /// <param name="isTechniqueUnlocked">技法解锁查询(技法 id → 是否已解锁);**不得为 null**。</param>
        /// <returns>缺页的纸条目 id(序 = 输入序,去重)。<b>不含</b>未绑定技法的纸。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="isTechniqueUnlocked"/> 为 null。</exception>
        /// <remarks>
        /// <para><b>判据</b>:<c>缺页(p) ⇔ p.TeachesTechnique ∧ ¬isUnlocked(p.TeachTechnique)</c>。</para>
        /// <para>⚠️ **未绑定技法的纸永远不缺**(<c>TeachesTechnique == false</c>)——
        /// 「望诊四要」这类纯知识页不会因为玩家没解锁任何技法而消失。</para>
        /// <para>⚠️ **查询未接线必须抛,不准默认 false**:默认 false 会让「系统没接上」渲染成
        /// 「整本手册都被撕光了」—— 那是拿故障当设计。承 T4 `PatientDrawEligibility` 的同款纪律。</para>
        /// </remarks>
        public static IReadOnlyList<string> Compute(
            IReadOnlyList<TutorialPaperRow> papers, Func<int, bool> isTechniqueUnlocked)
        {
            if (isTechniqueUnlocked == null)
                throw new ArgumentNullException(nameof(isTechniqueUnlocked),
                    "技法解锁查询未注入 —— 缺页判定不能以「全部未解锁」为默认值(会把未接线渲染成设计)");

            if (papers == null || papers.Count == 0) return Array.Empty<string>();

            var missing = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < papers.Count; i++)
            {
                TutorialPaperRow p = papers[i];
                if (!p.TeachesTechnique) continue;
                if (isTechniqueUnlocked(p.TeachTechnique)) continue;
                if (p.Id == null) continue;
                if (seen.Add(p.Id)) missing.Add(p.Id);
            }
            return missing;
        }
    }
}
