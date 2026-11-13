// 教堂内景的**窗洞**(2026-11-13 · §8.11)。运行期派生,不是烘焙数据。
//
// ═══ 为什么窗洞**不进**关卡数据 ═══════════════════════════════════════════
//   查过一层:能否把窗洞写进 `world_geometry`?——**不该**,理由有三条:
//   ① **窗洞不改变可走性**。`walkable` 是每格 0/1 的平面位图(ADR-015 §三 单一整数格),
//      而窗台在 1.10 m、窗顶 2.90 m,玩家既走不上去也穿不过去 ⇒ 逻辑层没有它的位置。
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
// ═══ 为什么是西墙 ═════════════════════════════════════════════════════════
//   概念图最强的构图元素是**左墙那个大窗**:光成束打进来、地面一块亮斑。
//   本项目四面实墙,唯一的光源是东墙塌口 ⇒ 补一个西墙窗,正是概念图那束光的来源。
//   （东墙不能再开:那里已经是塌口,再挖就是「整面没了」。）

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.World
{
    /// <summary>内景窗洞的唯一作者表(表现层;见文件头「为什么不是关卡数据」)。</summary>
    public static class InteriorWindows
    {
        /// <summary>西墙在灰盒数据里的墙格 X(`world_geometry` 实测:x=4 那一列不可走)。</summary>
        public const int WestWallCellX = 4;

        /// <summary>西墙面板所在的平面(墙内表面;面板再向内缩 <see cref="InteriorWallPanels.Inset"/>)。</summary>
        public const float WestFaceX = 5f;

        /// <summary>一处窗洞:灰盒格范围 + 面上的真实矩形(米)。</summary>
        public readonly struct Opening
        {
            /// <summary>灰盒墙格 X。</summary>
            public readonly int CellX;
            /// <summary>灰盒墙格 Z(闭区间)。**必须覆盖** <see cref="Z0"/>..<see cref="Z1"/>。</summary>
            public readonly int CellZ0, CellZ1;
            /// <summary>窗洞沿墙的起止(米)。</summary>
            public readonly float Z0, Z1;
            /// <summary>窗台 / 窗顶(米)。</summary>
            public readonly float Y0, Y1;

            /// <summary>构造。</summary>
            public Opening(int cellX, int cellZ0, int cellZ1, float z0, float z1, float y0, float y1)
            {
                CellX = cellX; CellZ0 = cellZ0; CellZ1 = cellZ1;
                Z0 = z0; Z1 = z1; Y0 = y0; Y1 = y1;
            }
        }

        /// <summary>
        /// 西墙大窗:1.8 m 宽 × 1.8 m 高,窗台 1.10 m。
        /// <para>洞口沿 z 取 17.6..19.4,落在灰盒格 18 / 19(格覆盖 z∈[17.5,19.5])**之内** ——
        /// 灰盒开口比面板洞口略宽是有意的:多出来的 0.1 m 两翼仍被面板挡着,而面板洞口
        /// 绝不会落到「灰盒还堵着」的范围里(那会在窗角露出一块灰板)。</para>
        /// </summary>
        public static readonly Opening West =
            new Opening(WestWallCellX, 18, 19, z0: 17.6f, z1: 19.4f, y0: 1.10f, y1: 2.90f);

        /// <summary>全部窗洞(当前一处;将来加窗只改这张表)。</summary>
        public static readonly Opening[] All = { West };

        /// <summary>该灰盒墙格是否要按窗洞拆开。命中时给出洞口的下沿 / 上沿(米)。</summary>
        public static bool TryGetCellCut(int cellX, int cellZ, out float y0, out float y1)
        {
            for (int i = 0; i < All.Length; i++)
            {
                Opening o = All[i];
                if (o.CellX != cellX) continue;
                if (cellZ < o.CellZ0 || cellZ > o.CellZ1) continue;
                y0 = o.Y0;
                y1 = o.Y1;
                return true;
            }
            y0 = 0f;
            y1 = 0f;
            return false;
        }

        /// <summary>`WorldWallGreybox.BuildWalls` 的 `cellCut` 实参:命中 ⇒ (窗台, 窗顶)。</summary>
        public static Vector2? CellCut(int cellX, int cellZ)
            => TryGetCellCut(cellX, cellZ, out float y0, out float y1)
                ? new Vector2(y0, y1)
                : (Vector2?)null;
    }
}
