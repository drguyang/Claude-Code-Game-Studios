// 教堂内景的**窗洞**(2026-11-13 · §8.11 / §8.12)。运行期派生,不是烘焙数据。
//
// ═══ 为什么窗洞**不进**关卡数据 ═══════════════════════════════════════════
//   查过一层:能否把窗洞写进 `world_geometry`?——**不该**,理由有三条:
//   ① **窗洞不改变可走性**。`walkable` 是每格 0/1 的平面位图(ADR-015 §三 单一整数格),
//      而窗台在 1.1~1.2 m、窗顶 2.9~3.2 m,玩家既走不上去也穿不过去 ⇒ 逻辑层没有它的位置。
//   ② ADR-015 §一 把世界分成**逻辑层(烘焙)**与**视觉层(float 任意)**。窗洞是**视觉**:
//      它的全部内容是「哪块面上缺一块」,与 sim / 导航 / POI / 建造槽位零关系。
//   ③ 真要进烘焙,就要动 `world_geometry` 的 schema、codec 布局与 `.cooked` 字节 ——
//      那是**存档与黄金夹具同一个确定性面**,为一个纯视觉开口付这个价不划算。
//
//   ⇒ 窗洞的**作者态**落在这里(与 `ArtOverlay` 同型:表现层唯一作者表),
//   两个消费者读**同一份**:`InteriorWallPanels`(面板开洞)
//   与 `WorldWallGreybox`(灰盒墙按窗洞拆成「窗下墙 + 窗上墙」)。
//   ⚠️ 只改一处 = 窗后露出 0.5 m 外那块 2.6 m 高的灰盒墙,**看起来像一堵灰板**而不是窗外。
//
// ═══ 两扇窗,两种形状 ═════════════════════════════════════════════════════
//   · **西墙大窗**(§8.11):1.8 × 1.8 矩形,带木框 —— 概念图左墙那一扇,主光源。
//   · **北墙拱窗**(§8.12):1.6 宽,窗台 1.20 / 起拱 2.40 / 拱顶 3.20 —— 概念图右墙那一扇。
//   ⚠️ 拱窗为什么**不放南墙**(2026-11-13 实测):美术框是 `x∈[5,15] / z∈[15,29]`,
//      而灰盒墙格是**以整数为心**的 1 m 块(墙行 z=29 覆盖 28.5~29.5)⇒ 南面板(z=28.98)
//      落在灰块**内部**,开洞后先看到的是灰块,z=29 那一行不存在「窗外」。
//      北面板(z=15.02)在墙格 z=14(13.5~14.5)前面 0.5 m ⇒ 干净。
//      (同理东面板 x=14.98 也在墙格 x=15 里 —— 那是既存的不对称,不在本轮改动面。)

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>内景窗洞的唯一作者表(表现层;见文件头「为什么不是关卡数据」)。</summary>
    public static class InteriorWindows
    {
        /// <summary>墙的走向。决定「沿墙方向」是 X 还是 Z。</summary>
        public enum WallAxis
        {
            /// <summary>墙沿 Z 走(东西两面墙);灰盒墙格固定的是 **X**。</summary>
            AlongZ = 0,
            /// <summary>墙沿 X 走(南北两面墙);灰盒墙格固定的是 **Z**。</summary>
            AlongX = 1,
        }

        /// <summary>西墙面板所在的平面(墙内表面;面板再向内缩 <see cref="InteriorWallPanels.Inset"/>)。</summary>
        public const float WestFaceX = 5f;

        /// <summary>北墙面板所在的平面。</summary>
        public const float NorthFaceZ = 15f;

        /// <summary>一处窗洞:灰盒格范围 + 面上的真实几何(米)。</summary>
        public readonly struct Opening
        {
            /// <summary>墙的走向。</summary>
            public readonly WallAxis Axis;
            /// <summary>灰盒墙格:沿 Z 的墙取 X,沿 X 的墙取 Z。</summary>
            public readonly int CellFixed;
            /// <summary>沿墙方向的灰盒格范围(闭区间)。**必须覆盖** <see cref="A0"/>..<see cref="A1"/>。</summary>
            public readonly int CellAlong0, CellAlong1;
            /// <summary>洞口沿墙方向的起止(米)。</summary>
            public readonly float A0, A1;
            /// <summary>窗台(米)。</summary>
            public readonly float Y0;
            /// <summary>洞口最高点(米):矩形 = 窗顶;拱窗 = 拱顶。</summary>
            public readonly float Y1;
            /// <summary>起拱高度(米)。`0` 或 ≤ <see cref="Y0"/> = 矩形窗。</summary>
            public readonly float ArchY;
            /// <summary>人读名(只进日志)。</summary>
            public readonly string Name;

            /// <summary>构造。</summary>
            public Opening(WallAxis axis, int cellFixed, int cellAlong0, int cellAlong1,
                           float a0, float a1, float y0, float y1, string name, float archY = 0f)
            {
                Axis = axis; CellFixed = cellFixed;
                CellAlong0 = cellAlong0; CellAlong1 = cellAlong1;
                A0 = a0; A1 = a1; Y0 = y0; Y1 = y1; ArchY = archY; Name = name;
            }

            /// <summary>是否拱窗(起拱高度高于窗台)。</summary>
            public bool IsArched => ArchY > Y0 + 1e-3f;

            /// <summary>洞口宽度(米)。</summary>
            public float Width => A1 - A0;

            /// <summary>拱半径(米;矩形窗无意义)。</summary>
            public float ArchRadius => Width * 0.5f;
        }

        /// <summary>
        /// 西墙大窗:1.8 m 宽 × 1.8 m 高,窗台 1.10 m,矩形,带木框。
        /// <para>洞口沿 z 取 17.6..19.4,落在灰盒格 18 / 19(格覆盖 z∈[17.5,19.5])**之内** ——
        /// 灰盒开口比面板洞口略宽是有意的:多出来的那点两翼仍被面板挡着,而面板洞口
        /// 绝不会落到「灰盒还堵着」的范围里(那会在窗角露出一块灰板)。</para>
        /// </summary>
        public static readonly Opening West = new Opening(
            WallAxis.AlongZ, cellFixed: 4, cellAlong0: 18, cellAlong1: 19,
            a0: 17.6f, a1: 19.4f, y0: 1.10f, y1: 2.90f, name: "西墙大窗");

        /// <summary>
        /// 北墙拱窗:1.6 m 宽,窗台 1.20 / 起拱 2.40 / 拱顶 3.20(半径 0.80)。
        /// <para>沿 x 取 9.7..11.3,落在灰盒格 10 / 11(格覆盖 x∈[9.5,11.5])之内。</para>
        /// <para>⚠️ 拱顶 3.20 高于灰盒墙高 2.6 ⇒ 灰盒那一格只开到墙顶,2.6 以上本来就没墙。</para>
        /// </summary>
        public static readonly Opening North = new Opening(
            WallAxis.AlongX, cellFixed: 14, cellAlong0: 10, cellAlong1: 11,
            a0: 9.7f, a1: 11.3f, y0: 1.20f, y1: 3.20f, name: "北墙拱窗", archY: 2.40f);

        /// <summary>全部窗洞(加窗只改这张表)。</summary>
        public static readonly Opening[] All = { West, North };

        /// <summary>该灰盒墙格是否要按窗洞拆开。命中时给出洞口的下沿 / 上沿(米)。</summary>
        public static bool TryGetCellCut(int cellX, int cellZ, out float y0, out float y1)
        {
            for (int i = 0; i < All.Length; i++)
            {
                Opening o = All[i];
                int fixedCoord = o.Axis == WallAxis.AlongZ ? cellX : cellZ;
                int along = o.Axis == WallAxis.AlongZ ? cellZ : cellX;
                if (fixedCoord != o.CellFixed) continue;
                if (along < o.CellAlong0 || along > o.CellAlong1) continue;
                y0 = o.Y0;
                y1 = o.Y1;
                return true;
            }
            y0 = 0f;
            y1 = 0f;
            return false;
        }

        /// <summary>`WorldWallGreybox.BuildWalls` 的 `cellCut` 实参:命中 ⇒ (窗台, 洞顶)。</summary>
        public static Vector2? CellCut(int cellX, int cellZ)
            => TryGetCellCut(cellX, cellZ, out float y0, out float y1)
                ? new Vector2(y0, y1)
                : (Vector2?)null;
    }
}
