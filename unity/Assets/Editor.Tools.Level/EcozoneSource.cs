// 关卡工具 —— **生态区多边形声明式源** + **C4 / C5 的执行面**。
//
// 权威来源:
//   ADR-022 §三(导出契约 `world_ecozones.json`)· §四 **C4 / C5**(硬失败)
//   GDD `world-and-ecozones.md` **F-6-3**(开集语义 + 射线法 + 裁决序 min(id))
//     · **AC-6-19** 五类退化 · **EC-6-7**(内部重叠 / 顶点<3 / 自交 / 零面积 / 重合顶点)
//     · **R-6-15 缝 2**(视觉层边界 ↔ 整数多边形 **一次定义、两次派生**)
//     · **EC-6-9**(射线穿顶点 ⇒ 半开区间)· **EC-6-10**(`NONE` 是合法结果)
//   ADR-015 §三(整数格)· ADR-015 §一(两层世界:逻辑层是唯一空间真相)
//
// ═══════════════════════════════════════════════════════════════════════════
// C4 的「重叠」口径 = **甲**(ADR-022 §四 只写「重叠」两个字,本文件把它定死)
// ═══════════════════════════════════════════════════════════════════════════
//   **甲**:任意两多边形的 **`Interior` 相交** ⇒ 失败。
//   ⇒ **相邻区共享边 / 共享顶点不失败**(开集语义:边界点不属任何区的 Interior)。
//
//   ⚠️ **乙**(闭包相交)会**误报相邻区** —— 与 F-6-3 §二 第 1 条(「内部重叠才非法,
//      贴边由裁决序吸收」)直接冲突,故否决;**丙**(只检重叠面积 > 0)与甲在本实现里等价
//      (见下),但甲的判据是**拓扑**的、丙是**测度**的,前者能覆盖「一条边上贴合成线」
//      这类退化的边角,故取甲。
//
//   **「Interior 相交」的可判定形式**(对简单多边形**精确**):
//     ① 存在一对边**真交叉**(跨立试验双向严格异号);或
//     ② A 的某个顶点**严格在 B 内部**;或
//     ③ B 的某个顶点**严格在 A 内部**。
//     —— 三条都不成立 ⇔ 内部不相交。理由:正面积交集必使边界横截相交,或一方整体落入另一方;
//     只贴边 / 只贴顶点时三条全不成立(共线不算真交叉,边界点不算严格内部)⇒ 不误报。
//
// ⚠️ **本文件的检查用 `long` 中间量**(满量程 i32 叉积可近 2^62,承 ADR-012 F7)。
// ⚠️ **全部 `throw`,零 `Debug.Assert`**(ADR-022 §②:C4/C5 是构建期失败通道)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>一个生态区的声明(整数顶点;**只用 x / z**,见 <see cref="EcozoneSource"/>)。</summary>
    public readonly struct EcozoneZoneSpec
    {
        /// <summary>`ecozone_id`(≥ 0;`-1` 是 `EcozoneId.None` 哨兵,不得作 id)。</summary>
        public readonly int Id;

        /// <summary>作者可读名(ASCII;导出到 JSON;**不进 cooked** —— 运行期零消费者)。</summary>
        public readonly string Name;

        /// <summary>闭合多边形的顶点序列(首尾**不重复**;长度 ≥ 3;只用 `X` / `Z`)。</summary>
        /// <remarks>⚠️ `Y` **不参与任何判定**(GDD F-6-3 §四:横轴是 z,不是 y)。
        /// 声明时统一填 0;导出面只写 `[x, z]` 二元组(见 <see cref="EcozoneSource"/> 文件头)。</remarks>
        public readonly WorldPos[] Vertices;

        /// <summary>构造一个生态区声明。</summary>
        public EcozoneZoneSpec(int id, string name, WorldPos[] vertices)
        {
            Id = id; Name = name; Vertices = vertices;
        }
    }

    /// <summary>
    /// 一个关卡的生态区声明(零个或多个多边形)。
    /// <para>⚠️ **顶点序列的写形**:导出 JSON 里是 `"vertices": [[x, z], ...]` —— **二元组**,
    /// 不是三元组。理由:F-6-3 §四 明写「y 不参与」,把 y 写进文件会让读者以为它有意义,
    /// 而它只能恒为 0(一个「看起来像字段的常量」正是本仓反复清的形态)。
    /// `EcozoneZoneSpec.Vertices` 仍用 `WorldPos[]` —— 那是 `Sim` 侧 `EcozonePolygon` 的既有形,
    /// 二元组在导出与烘焙两侧**同时**映射。</para>
    /// </summary>
    public sealed class EcozoneSource
    {
        /// <summary>全部生态区声明(声明序;**id 唯一**由 <see cref="EcozonePolygonCheck"/> 校验)。</summary>
        public readonly IReadOnlyList<EcozoneZoneSpec> Zones;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException"><paramref name="zones"/> 为 null。</exception>
        public EcozoneSource(IReadOnlyList<EcozoneZoneSpec> zones)
        {
            Zones = zones ?? throw new ArgumentNullException(nameof(zones));
        }

        /// <summary>按 id 升序的行集(**确定性导出的起点**;声明序不承载语义)。</summary>
        public List<EcozoneZoneSpec> SortedById()
        {
            var list = new List<EcozoneZoneSpec>(Zones);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }
    }

    /// <summary>
    /// ADR-022 §四 **C4 —— 多边形合法性**(五类退化,全部硬失败)。
    /// <para><b>五项的顺序是契约的一部分</b>(先廉价后昂贵,且后面的项依赖前面项的合法性):
    /// ② 顶点 &lt; 3 → ⑤ 重合相邻顶点 → ③ 自交 → ④ 零面积 → ① 内部重叠。</para>
    /// <para>⚠️ **五项各是一条独立判据**,不得合并成一条复合断言 —— 否则停用任一项时
    /// 其余项仍会让夹具红,判别面不可分(承 `feedback_falsify_one_guard_per_property`)。</para>
    /// </summary>
    public static class EcozonePolygonCheck
    {
        /// <summary>跑 C4 全部五项,把**每一条**违例收进 <paramref name="violations"/>(不抛)。</summary>
        /// <param name="source">生态区声明。</param>
        /// <param name="violations">违例报文(每条点名判据关键词)。</param>
        /// <returns>是否有违例。</returns>
        public static bool TryCheck(EcozoneSource source, out List<string> violations)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            violations = new List<string>();

            List<EcozoneZoneSpec> zones = source.SortedById();

            // ── id 唯一(AC-1 附带;不属于 C4 五项,但同一趟检)──────────────
            var seen = new HashSet<int>();
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Id < 0)
                    violations.Add(
                        $"world_ecozones:zones[{i}] ecozone_id = {zones[i].Id} 须 ≥ 0" +
                        "(-1 是 EcozoneId.None 哨兵,GDD :634;作者不得铸出与哨兵撞值的合法区)");
                else if (!seen.Add(zones[i].Id))
                    violations.Add(
                        $"world_ecozones:zones[{i}] ecozone_id = {zones[i].Id} 重复 —— " +
                        "同一 id 两份几何会让「哪一份生效」成为注册顺序的函数");
            }

            // ── 逐区:② ⑤ ③ ④ ─────────────────────────────────────────────
            for (int i = 0; i < zones.Count; i++)
            {
                EcozoneZoneSpec z = zones[i];
                CheckOneZone(z, violations);
            }

            // ── ① 内部重叠(两两)──────────────────────────────────────────
            // ⚠️ 只在两区都**已通过** ②⑤③④ 时才判 —— 退化多边形的「内部」无定义,
            //    对它们求交会把一个假象报成重叠,掩盖真因。
            for (int a = 0; a < zones.Count; a++)
            {
                if (!IsSimple(zones[a])) continue;
                for (int b = a + 1; b < zones.Count; b++)
                {
                    if (!IsSimple(zones[b])) continue;
                    if (InteriorsIntersect(zones[a].Vertices, zones[b].Vertices))
                        violations.Add(
                            $"world_ecozones:生态区 {zones[a].Id} 与 {zones[b].Id} 的**内部**相交" +
                            "(ADR-022 C4 ①/GDD EC-6-7:接受贴边 / 共享顶点,但内部交叠非法 —— " +
                            "开集语义下两区内部相交会让同一格同属两区)");
                }
            }

            return violations.Count > 0;
        }

        /// <summary>跑 C4 并在任一违例时 `throw`(ADR-022 §②:构建期失败通道)。</summary>
        /// <exception cref="InvalidOperationException">任一 C4 违例(报文逐条列出)。</exception>
        public static void CheckOrThrow(EcozoneSource source)
        {
            if (TryCheck(source, out List<string> violations))
                throw new InvalidOperationException(
                    "[关卡工具] world_ecozones C4 多边形合法性检查失败(ADR-022 §四):\n  "
                    + string.Join("\n  ", violations));
        }

        /// <summary>单区四项(② ⑤ ③ ④),各一条独立判据。</summary>
        private static void CheckOneZone(EcozoneZoneSpec z, List<string> violations)
        {
            WorldPos[] v = z.Vertices;
            string where = $"生态区 {z.Id}";

            // ② 顶点 < 3
            if (v == null || v.Length < 3)
            {
                violations.Add(
                    $"world_ecozones:{where} 顶点数 = {v?.Length ?? 0} —— 须 ≥ 3" +
                    "(AC-6-19 ②:少于 3 个顶点的多边形没有内部,射线法无定义)");
                return;   // 后续三项对退化输入无意义,且会产出误报
            }

            int n = v.Length;

            // ⑤ 重合相邻顶点(含首尾相连那一对)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (v[i].X == v[j].X && v[i].Z == v[j].Z)
                    violations.Add(
                        $"world_ecozones:{where} 第 {i} 与第 {j} 个顶点重合 " +
                        $"({v[i].X},{v[i].Z}) —— 零长边使射线法无定义(AC-6-19 ⑤)");
            }

            // ③ 自交(非相邻边**真交叉**)
            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                for (int j = i + 1; j < n; j++)
                {
                    int j2 = (j + 1) % n;
                    if (j == i2 || j2 == i)
                        continue;   // 相邻边共享端点,不可能真交叉
                    if (ProperlyCross(v[i], v[i2], v[j], v[j2]))
                        violations.Add(
                            $"world_ecozones:{where} 自交:边 {i}→{i2} 与边 {j}→{j2} 真交叉 " +
                            "(AC-6-19 ③:射线法对自交多边形无定义)");
                }
            }

            // ④ 零面积(shoelace 数 = 0 ⇒ 全部顶点共线 ⇒ 没有内部)
            long twiceArea = 0;
            for (int i = 0; i < n; i++)
            {
                WorldPos a = v[i];
                WorldPos b = v[(i + 1) % n];
                twiceArea += (long)a.X * b.Z - (long)b.X * a.Z;
            }
            if (twiceArea == 0)
                violations.Add(
                    $"world_ecozones:{where} 面积 = 0(全部顶点共线)—— " +
                    "零面积多边形的内部为空集,归属判定恒不可达(AC-6-19 ④)");
        }

        /// <summary>该区是否已成「简单多边形」(②⑤③④ 全过)—— 重叠检查的前置。</summary>
        private static bool IsSimple(EcozoneZoneSpec z)
        {
            WorldPos[] v = z.Vertices;
            if (v == null || v.Length < 3) return false;

            int n = v.Length;
            for (int i = 0; i < n; i++)
                if (v[i].X == v[(i + 1) % n].X && v[i].Z == v[(i + 1) % n].Z) return false;

            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                for (int j = i + 1; j < n; j++)
                {
                    int j2 = (j + 1) % n;
                    if (j == i2 || j2 == i) continue;
                    if (ProperlyCross(v[i], v[i2], v[j], v[j2])) return false;
                }
            }

            long twiceArea = 0;
            for (int i = 0; i < n; i++)
                twiceArea += (long)v[i].X * v[(i + 1) % n].Z - (long)v[(i + 1) % n].X * v[i].Z;
            return twiceArea != 0;
        }

        /// <summary>
        /// 两个**简单**多边形的内部是否相交(C4 ① 的判据,口径甲)。
        /// <para>三条任一成立 ⇒ 相交:① 某对边真交叉;② A 的顶点严格在 B 内;③ B 的顶点严格在 A 内。</para>
        /// </summary>
        private static bool InteriorsIntersect(WorldPos[] a, WorldPos[] b)
        {
            int na = a.Length, nb = b.Length;

            // ① 边真交叉
            for (int i = 0; i < na; i++)
            {
                int i2 = (i + 1) % na;
                for (int j = 0; j < nb; j++)
                {
                    int j2 = (j + 1) % nb;
                    if (ProperlyCross(a[i], a[i2], b[j], b[j2]))
                        return true;
                }
            }

            // ② / ③ 顶点严格落入对方内部
            for (int i = 0; i < na; i++)
                if (StrictlyInside(a[i], b)) return true;
            for (int j = 0; j < nb; j++)
                if (StrictlyInside(b[j], a)) return true;

            return false;
        }

        /// <summary>点是否严格在多边形内部(开集;边界点返回 false)。与 `EcozonePolygon.Contains` 同判据。</summary>
        private static bool StrictlyInside(WorldPos p, WorldPos[] poly)
        {
            int n = poly.Length;

            for (int i = 0; i < n; i++)
            {
                if (OnSegment(p, poly[i], poly[(i + 1) % n]))
                    return false;   // 边界点不属内部(开集)
            }

            int crossings = 0;
            for (int i = 0; i < n; i++)
            {
                WorldPos a = poly[i];
                WorldPos b = poly[(i + 1) % n];
                bool aAbove = a.Z > p.Z;
                bool bAbove = b.Z > p.Z;
                if (aAbove == bAbove) continue;

                long dx = (long)b.X - a.X;
                long dz = (long)b.Z - a.Z;
                long lhs = ((long)p.Z - a.Z) * dx;
                long rhs = ((long)p.X - a.X) * dz;
                bool ahead = dz > 0 ? lhs > rhs : lhs < rhs;
                if (ahead) crossings++;
            }
            return (crossings & 1) == 1;
        }

        /// <summary>点是否落在线段 a–b 上(含端点)。</summary>
        private static bool OnSegment(WorldPos p, WorldPos a, WorldPos b)
        {
            long cross = ((long)b.X - a.X) * ((long)p.Z - a.Z)
                       - ((long)p.X - a.X) * ((long)b.Z - a.Z);
            if (cross != 0) return false;
            return p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
                && p.Z >= Math.Min(a.Z, b.Z) && p.Z <= Math.Max(a.Z, b.Z);
        }

        /// <summary>两条线段是否**真交叉**(四叉积全非零 ∧ 双向严格异号)。共线 / 端点接触不算。</summary>
        private static bool ProperlyCross(WorldPos p1, WorldPos p2, WorldPos p3, WorldPos p4)
        {
            long d1 = Cross(p3, p4, p1);
            long d2 = Cross(p3, p4, p2);
            long d3 = Cross(p1, p2, p3);
            long d4 = Cross(p1, p2, p4);
            if (d1 == 0 || d2 == 0 || d3 == 0 || d4 == 0) return false;
            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        /// <summary>叉积 z 分量:`cross(b−a, c−a)`。两侧各自先转 `long`(防 int 回绕)。</summary>
        private static long Cross(WorldPos a, WorldPos b, WorldPos c)
            => ((long)b.X - a.X) * ((long)c.Z - a.Z) - ((long)c.X - a.X) * ((long)b.Z - a.Z);
    }
}
