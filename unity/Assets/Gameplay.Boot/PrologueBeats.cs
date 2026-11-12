// 序章节拍表 P1-01 … P1-05(第一段 · 失去)。
//
// 权威来源:
//   `design/levels/prologue-beat-script.md` §第一段(每拍的触发 / 呈现 / 判据)
//   `design/levels/chapter-1-tent-clinic.md` §3 分区(A1 教堂 4–15 × 14–29 · A2 前庭 16–21 × 18–25)
//
// ⚠️ **本表只放第一段的前五拍**。D1 按脚本逐段接,不一次接完 —— 理由:
//    每多接一拍就多一处「玩家实际会走到哪」的假设,而灰盒世界里这些假设只有真包能证伪
//    (本轮走查已两次证伪:出生点、病人格)。先让 P1-01…P1-05 在真包里走通,再据实测坐标扩表。
//
// ⚠️ **字幕文本是占位**:正式文案归 writer(节拍脚本 §3 已登记);此处只保证
//    「一拍一句、语义与正典一致」,便于 D1 的真包验证。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>序章第一段(P1-01 … P1-05)的节拍表。</summary>
    public static class PrologueBeats
    {
        /// <summary>教堂(A1 区)的格矩形,**闭区间**(权威:`chapter-1-tent-clinic.md` §3 分区)。
        /// <para>⚠️ 这四值同时服务两处:节拍表的区域判定、灰盒地坪(`CompositionRoot` 的
        /// 「关内地面」)。写第二份 = 让「玩家踩到的范围」与「看起来是屋里的范围」分叉。</para>
        /// <para>🔁 2026-11-13 实测:数据里教堂确实存在(不可走格围出的 x∈[4,15] / z∈[14,29],
        /// 东墙 z∈[20,23] 有塌口),但此前**没有任何网格** —— 玩家在教堂里看见的是旷野。</para></summary>
        public const int ChurchX0 = 4;
        /// <summary>见 <see cref="ChurchX0"/>。</summary>
        public const int ChurchX1 = 15;
        /// <summary>见 <see cref="ChurchX0"/>。</summary>
        public const int ChurchZ0 = 14;
        /// <summary>见 <see cref="ChurchX0"/>。</summary>
        public const int ChurchZ1 = 29;

        /// <summary>
        /// 组装第一段的五拍。
        /// </summary>
        /// <param name="playerCell">玩家当前格(区域判定用;每 tick 现读)。</param>
        /// <remarks>
        /// 🔁 **2026-11-13(乙)三处改判**,依据是真包走查的实测序列
        /// (`tick=980 (12,21) P1-04` → `tick=1080 (9,19) P1-05` → `tick=1180 (8,18) P1-02`):
        /// <list type="number">
        /// <item>三拍从**大矩形**改成**锚点邻格**(`PrologueAnchors`)。旧矩形压在进门走廊上,
        ///       玩家只是路过就把「登记簿」读了。</item>
        /// <item>三拍都加 `RequiresInteract` —— 脚本的完成判据是「读毕」,不是「路过」。</item>
        /// <item>`P1-05`(神父走)从「只要求 P1-04」改成**同时要求 P1-02 / P1-03 / P1-04** ——
        ///       否则神父会在玩家看台阶与告示之前就走了。</item>
        /// </list>
        /// </remarks>
        public static IReadOnlyList<PrologueBeat> BuildFirstAct(Func<WorldPos> playerCell)
        {
            if (playerCell == null) throw new ArgumentNullException(nameof(playerCell));

            // 锚点邻格判定(切比雪夫半径 1;判定**无副作用**,每 tick 会被反复调用)。
            Func<PrologueAnchor, Func<bool>> near =
                anchor => () =>
                {
                    WorldPos c = playerCell();
                    return anchor.IsNear(c.X, c.Z);
                };

            return new[]
            {
                // P1-01 开场:首 tick 即播(无条件)。
                new PrologueBeat("P1-01", () => true,
                    "光绪二十六年。城北的教堂,只剩半座。"),

                // P1-02 石阶:走到门口那一级 + 按 E。
                new PrologueBeat("P1-02", near(PrologueAnchors.Steps),
                    "你每天擦这级台阶。上面刻着:光绪二十六年。",
                    requiresInteract: true),

                // P1-03 告示:门外一步 + 按 E。
                new PrologueBeat("P1-03", near(PrologueAnchors.Notice),
                    "告示上洋文在上、汉文在下:各国侨民,即日撤离。你认得另外两个字:拳、乱。",
                    requiresInteract: true),

                // P1-04 登记簿:屋内案头(故意远离进门走廊)+ 按 E。
                new PrologueBeat("P1-04", near(PrologueAnchors.Desk),
                    "登记簿三年,一百四十七个名字。最后一行墨还没干。",
                    requiresInteract: true),

                // P1-05 神父走:**三样都读过**之后才发生。
                new PrologueBeat("P1-05", () => true,
                    "那天晚上,他把听诊器挂在你脖子上,说了一句洋文。你只听懂两个词:留下,他们。",
                    requires: new[] { "P1-02", "P1-03", "P1-04" }),
            };
        }
    }
}
