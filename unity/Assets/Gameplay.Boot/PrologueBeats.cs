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
//
// 🔁 **2026-11-14(《留人》集 1 落位 · 正典重写稿同步)**:三处旁白改写,依据
//    `design/gdd/content/campaign-arc.md` 的 2026-11-14 全剧情重写稿(改动 ①③⑥)。
//    只动**文本**,触发 / 判据 / 载体一字不动。
//    ① P1-01 补抬埋工身份(改动 ①:主角不是白纸,他有过往);
//    ② P1-04 核心道具有变(改动 ③):不是神父的登记簿,是**你自己的抬埋簿** ——
//       一百四十七个编号全是**你的**笔迹,你只会写数字和几个姓;
//    ③ P1-05 保留「挂在你脖子上」(改动 ⑥):听诊器是**托付的手势**,不是遗弃的动作。
//       遗弃在第四章才揭晓 —— 名单末尾「留下他们」= 这批不带了。**手势是真的,注解不是**;
//       这正是「你听了三年的嘱托,是一句遗弃」的反差来源。⚠️ 不得改写成「扔在案上」,
//       那会让第四章「他给你听诊器,是因为他带不走」失去对手戏。

using System;
using System.Collections.Generic;

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
        /// <param name="regions">
        /// 区域触发驱动(N1,2026-11-14 引入)。三拍的「走到某处」判定从此读它,
        /// **不再**各自捕获玩家格闭包。
        /// </param>
        /// <param name="onSeventeenArrive">
        /// P1-07 的**动作**(N2,2026-11-14):十七个难民出现。必需 —— 传 `null` 会让
        /// 「人群出现」这一拍退化成纯字幕,而它本来就不是一句台词。
        /// </param>
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
        /// 🔁 **2026-11-14(N1)**:签名由 `Func&lt;WorldPos&gt; playerCell` 改为 `RegionTriggerDriver`。
        /// 语义**不变**(仍是「此刻在锚点邻格」),变的是**谁持有那份格判定** —— 从三条各写一份的闭包
        /// 收到一个可复用、可测、有边的驱动器。⚠️ 这里刻意用 `IsInside`(此刻)而**不是**
        /// `HasEntered`(进过):这三拍的完成判据是「站在这儿按 E」,走开再回来按仍然算数。
        /// </remarks>
        public static IReadOnlyList<PrologueBeat> BuildFirstAct(RegionTriggerDriver regions,
                                                               Action onSeventeenArrive)
        {
            if (regions == null) throw new ArgumentNullException(nameof(regions));
            if (onSeventeenArrive == null) throw new ArgumentNullException(nameof(onSeventeenArrive));

            // 锚点邻格判定 —— 与 `PrologueAnchors` 的体块位置**同源**(经 `ToRegion`)。
            //   判定无副作用,每 tick 会被反复调用。
            Func<PrologueAnchor, Func<bool>> near =
                anchor => () => regions.IsInside(anchor.BeatId);

            return new[]
            {
                // P1-01 开场:首 tick 即播(无条件)。
                new PrologueBeat("P1-01", () => true,
                    "光绪二十六年。城北的教堂只剩半座。你在教会义地抬了三年死人 —— 今天抬的是自己的教堂。"),

                // P1-02 石阶:走到门口那一级 + 按 E。
                new PrologueBeat("P1-02", near(PrologueAnchors.Steps),
                    "你每天擦这级台阶。上面刻着:光绪二十六年。",
                    requiresInteract: true),

                // P1-03 告示:门外一步 + 按 E。
                new PrologueBeat("P1-03", near(PrologueAnchors.Notice),
                    "告示上洋文在上、汉文在下:各国侨民,即日撤离。你认得另外两个字:拳、乱。",
                    requiresInteract: true),

                // P1-04 抬埋簿:屋内案头(故意远离进门走廊)+ 按 E。
                //   🔁 2026-11-14:核心道具由「神父的登记簿」改为**主角自己的抬埋簿**
                //      (正典改动 ③)。一百四十七个编号全是**你的**笔迹 —— 你只会写数字和几个姓。
                new PrologueBeat("P1-04", near(PrologueAnchors.Desk),
                    "你的簿子:三年,一百四十七个编号,全是你的笔迹。最后一行墨还没干。" +
                    "你只会写数字和几个姓。",
                    requiresInteract: true),

                // P1-05 神父走:**三样都读过**之后才发生。
                //   ⚠️ 听诊器是**托付的手势**,不是遗弃的动作 —— 遗弃要到第四章才揭晓
                //      (名单末尾「留下他们」= 这批不带了)。**手势是真的,注解不是。**
                new PrologueBeat("P1-05", () => true,
                    "那天晚上,他把听诊器挂在你脖子上,说了半句洋文。你只听懂两个词:留下,他们。",
                    requires: new[] { "P1-02", "P1-03", "P1-04" }),

                // P1-06 那一夜(N2 引入,2026-11-14):纯台词拍。
                //   ⚠️ 脚本的呈现是「黑底 + 推门声 + 镜头固定约 20 s」,**那些归 N3**;
                //      本拍现在只有旁白。**推进时间到夜同样未做** —— 它要么是一次 sim tick 跳变
                //      (病人在「一夜」里该不该恶化?没人裁过),要么是纯呈现的昼夜外观,
                //      两条都不是本拍能自决的。见 `chapter-plan.md` §四。
                new PrologueBeat("P1-06", () => true,
                    "那一夜。天亮的时候,孩子哭了。你没有哭。",
                    requires: "P1-05"),

                // P1-07 十七个人(N2 的第一个真实动作):**人群出现**。
                //   ⚠️ 人群不是病人(§0.1 裁定)⇒ 纯视觉,不写三流、不建 `PatientState`。
                //   ⚠️ 本拍**没有台词** —— 脚本只写了「人群中三个可交互(各一张教学纸)」,
                //      而那三张纸是 P1-07 自己的内容,尚未接。人群出现本身就是这一拍的内容。
                new PrologueBeat("P1-07", () => true, null,
                    requires: "P1-06", effect: onSeventeenArrive),
            };
        }
    }
}
