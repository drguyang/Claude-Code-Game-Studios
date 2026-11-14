// 序章第一段的**可交互锚点表**(乙 · 2026-11-13)。
//
// 权威:
//   `design/levels/prologue-beat-script.md` §第一段(P1-02 台阶 / P1-03 告示 / P1-04 登记簿)
//   `design/levels/chapter-1-tent-clinic.md` §3 分区(A1 教堂 x∈[4,15] · z∈[14,29])
//
// ═══ 为什么要有这张表 ═════════════════════════════════════════════════════
//   此前三拍的条件是**一个大矩形**(如登记簿 = x∈[8,12] × z∈[20,24])。真包实测
//   (2026-11-13,`tick=980 cell=(12,21)`):那矩形正压在「从出生点往西进屋」的必经
//   走廊上 ⇒ 玩家什么都没做,「登记簿」就自己播了。
//
//   根因不是矩形画大了,是**矩形没有对应的实物**:纸面分区描述的是「这一片是教堂」,
//   而一拍要的是「这一件东西」。⇒ 本表把「那一件东西」定下来:一个格 + 一个尺寸。
//   体块由 `WorldWallGreybox.BuildMarker` 铺(灰盒,零美术),节拍按**邻格 + 按 E**判定。
//
// ⚠️ **单一口径**:节拍判定与体块位置读同一份格坐标 —— 写第二份 = 「看起来有个案头」
//   与「按 E 有反应的地方」分叉,那正是本轮要消灭的形态。
//
// ⚠️ 坐标取自**几何产物的可走格**(真包实测可走,见各行的注释):
//   东墙 z∈[20,23] 有塌口 ⇒ 门在 x=15 那一段。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>一个可交互锚点:占一格,玩家须**走到邻格并按 E**。</summary>
    public readonly struct PrologueAnchor
    {
        /// <summary>锚点 id(= 与之绑定的节拍 id)。</summary>
        public readonly string BeatId;
        /// <summary>体块名(ASCII,便于层级里辨认)。</summary>
        public readonly string MarkerName;
        /// <summary>所在格 X。</summary>
        public readonly int CellX;
        /// <summary>所在格 Z。</summary>
        public readonly int CellZ;
        /// <summary>体块尺寸(米;`y` 是高度)。</summary>
        public readonly Vector3 Size;
        /// <summary>体块色调(灰盒期用色相区分三类物件)。</summary>
        public readonly Color Tint;

        /// <summary>构造。</summary>
        public PrologueAnchor(string beatId, string markerName, int cellX, int cellZ,
                              Vector3 size, Color tint)
        {
            BeatId = beatId;
            MarkerName = markerName;
            CellX = cellX;
            CellZ = cellZ;
            Size = size;
            Tint = tint;
        }

        /// <summary>玩家格是否在**本锚点的邻格**(含本格;切比雪夫距离 ≤ 1)。</summary>
        public bool IsNear(int cellX, int cellZ)
            => Mathf.Abs(cellX - CellX) <= 1 && Mathf.Abs(cellZ - CellZ) <= 1;

        /// <summary>
        /// 转成 N1 的区域触发器(邻格 3×3 · **一次性**)。
        /// <para>⚠️ 与 <see cref="IsNear"/> **同源**:两边都读本锚点的 `CellX` / `CellZ`。
        /// 写第二份格坐标 = 「看起来有个案头」与「按 E 有反应的地方」分叉 —— 那正是
        /// 2026-11-13 立本表时要消灭的形态。</para>
        /// <para>⚠️ `Once` 只决定**回调**报几次;节拍判定走 `IsInside`(此刻),
        /// 与「进过」无关 —— 见 `RegionTriggerDriver` 类首。</para>
        /// </summary>
        public RegionTrigger ToRegion(bool once = true)
            => RegionTrigger.Around(BeatId, CellX, CellZ, radius: 1, once: once);
    }

    /// <summary>序章第一段的三件实物。</summary>
    public static class PrologueAnchors
    {
        /// <summary>石阶(教堂门口)。
        /// <para>格 (14,21):东墙缺口(x=15,z∈[20,23])内侧一格 —— 玩家跨进门第一脚踩的就是它。</para></summary>
        public static readonly PrologueAnchor Steps = new PrologueAnchor(
            "P1-02", "Anchor_Steps", 14, 21,
            new Vector3(1.4f, 0.16f, 1.4f), new Color(0.55f, 0.54f, 0.50f));

        /// <summary>领事馆告示(门外北侧)。
        /// <para>格 (17,23):门外一步,贴着缺口北沿 —— 「走近教堂门口」即可看到。</para></summary>
        public static readonly PrologueAnchor Notice = new PrologueAnchor(
            "P1-03", "Anchor_Notice", 17, 23,
            new Vector3(0.9f, 1.45f, 0.12f), new Color(0.80f, 0.75f, 0.62f));

        /// <summary>登记簿(教堂内案头)。
        /// <para>格 (9,26):屋内西北深处 —— 刻意**远离**进门走廊,玩家必须真的走进去。</para></summary>
        public static readonly PrologueAnchor Desk = new PrologueAnchor(
            "P1-04", "Anchor_Desk", 9, 26,
            new Vector3(1.1f, 0.78f, 0.62f), new Color(0.42f, 0.30f, 0.20f));

        /// <summary>第一段的全部锚点(声明序 = 铺体块的顺序)。</summary>
        public static readonly PrologueAnchor[] FirstAct = { Steps, Notice, Desk };

        /// <summary>第一段的区域表(声明序 = <see cref="FirstAct"/> 的序)。</summary>
        /// <remarks>新建数组而非缓存:调用方(装配根)可以自由改返回值而不污染下一次。</remarks>
        public static RegionTrigger[] BuildFirstActRegions()
        {
            var regions = new RegionTrigger[FirstAct.Length];
            for (int i = 0; i < FirstAct.Length; i++)
                regions[i] = FirstAct[i].ToRegion();
            return regions;
        }
    }
}
