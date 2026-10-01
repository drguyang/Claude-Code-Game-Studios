// 权威来源:ADR-015 §三(单一整数格)· ADR-009 §四(派生态/三态)· Story 002(EcozoneOf 整数几何查询)
//          GDD world-and-ecozones.md F-6-3(开集语义 + 正则化)· AC-6-19…22 · EC-6-7…9
//
// 核心机制(F-6-3 逐条落地,次序**不可**调换):
//   ① Interior(e)  = **非边界** ∧ 射线奇偶为真    —— 开集:边界**不**算内部
//   ② Boundary(e)  = 点在某条边或顶点上(精确整数叉积 = 0 ∧ 落在线段 x/z 包围盒内)
//   ③ EcozoneOf    = 严格内部命中 ⇒ min(id);否则 min(boundary_hits).id;否则 NONE(−1)
//   ⇒ 「单值」由**裁决序 min(id)** 保证,不由「恰好一个多边形含它」保证(F-6-3 §二)。
//
// ⚠️ y 分量**不参与**判定(GDD :573 / :636-638 / :1367 四处独立表述 + story Forbidden 行):
//   多边形 = x/z 平面上的整数折线,沿 y 上下无限延伸;P0 = 单层,垂直分层是 OQ-6-9(P1b 前裁)。
//   原实现有一道「Y 范围取顶点 Y 的 min/max」的门禁,它读的正是 F-6-3 明写不参与的轴 ——
//   同一 (x,z) 在 y=±5 时返回 NONE(山丘上的 POI / 13 的 HomeRegion 直接被抹掉),
//   且使 AC-6-22 的「该函数不含 y 读取」恒不可达。已删除。
//   若 P1b 判需分层,须**先**扩 F-6-3 + AC-6-19 再回改本文件,不得就地加条件分支。
//
// ⚠️ int64 纪律(ADR-012 F7:IL2CPP 有符号溢出是 UB,非 wrapping):
//   · 叉积 / 交叉相乘的两个因子**各自先转 long 再乘** —— 不是先做 int 减法再提升
//     (int 减法本身在满量程 i32 下已回绕,提升救不回来)。
//   · <see cref="EcozoneRegistry.EcozoneOf"/> 入口断言 |x| / |z| ≤ MaxWorldHalfExtent
//     (= 2^29,Story 001 的量程守卫),使各因子 ≤ 2^30、中间积 ≤ 2^60。
//
// ⚠️ 本类**不做**多边形合法性修补(那是 ADR-022 C4 关卡工具 / ADR-014 阶段 2 的职责),
//   但按「信任但不盲信」(story Implementation Note 5)做线性成本的**加载期硬失败**:
//   顶点 < 3 / 重合相邻顶点 / 真自交 ⇒ Register 抛。
//   **内部重叠**(AC-6-19 ①)刻意**不在此处**判:它是 O(n²) 且判据是「Interior 两两不交」,
//   属构建期校验器;运行期只做入参卫生,不把 CI 的职责搬进装载路径。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>生态区 id 类型(int)。</summary>
    public readonly struct EcozoneId : IEquatable<EcozoneId>
    {
        /// <summary>id 值(≥ 0;−1 是 <see cref="None"/> 哨兵,GDD :634)。</summary>
        public readonly int Value;

        public EcozoneId(int value) => Value = value;

        public bool Equals(EcozoneId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EcozoneId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(EcozoneId a, EcozoneId b) => a.Equals(b);
        public static bool operator !=(EcozoneId a, EcozoneId b) => !a.Equals(b);

        /// <summary>无生态区哨兵(−1;镜像 ADR-007 §四 的 PatientId.None)。</summary>
        public static EcozoneId None => new EcozoneId(-1);

        /// <summary>是否有效(非哨兵)。</summary>
        public bool IsValid => Value >= 0;
    }

    /// <summary>
    /// 生态区多边形(整数顶点,**只用 x / z**;y 不参与,见文件头)。
    /// <para>判定为纯函数、全程整数(AC-6-22)。<see cref="Contains"/> 是**开集**形式:
    /// 边界点返回 false。查询裁决序在 <see cref="EcozoneRegistry.EcozoneOf"/> ——
    /// 那里才组装 <c>min(boundary_hits)</c>。</para>
    /// </summary>
    public readonly struct EcozonePolygon
    {
        /// <summary>生态区 id。</summary>
        public readonly EcozoneId Id;

        /// <summary>顶点(至少 3 个;相邻顶点不重合 —— 由 <see cref="EcozoneRegistry.Register"/> 校验)。</summary>
        public readonly WorldPos[] Vertices;

        public EcozonePolygon(EcozoneId id, WorldPos[] vertices)
        {
            if (vertices == null || vertices.Length < 3)
                throw new ArgumentException(
                    $"多边形至少需要 3 个顶点(AC-6-19 ②),实际={vertices?.Length ?? 0}", nameof(vertices));

            Id = id;
            Vertices = vertices;
        }

        /// <summary>
        /// <c>Interior(e)</c>:点 p 是否在**严格内部**(F-6-3 §一)。
        /// <para>= 「p 不在任何边 / 顶点上」∧「x/z 平面射线奇偶为真」。<b>边界点返回 false</b>
        /// —— 开集语义;边界归属由裁决序接管,调用方不得把它当闭集用。</para>
        /// <para>⚠️ y **未**被读取(AC-6-22 的附带声明,GDD :1367)。</para>
        /// </summary>
        public bool Contains(WorldPos p)
        {
            int n = Vertices.Length;

            // 正则化次序固定(story Implementation Note 2):边界先判,再走奇偶核。
            for (int i = 0; i < n; i++)
            {
                if (IsOnBoundarySegment(p, Vertices[i], Vertices[(i + 1) % n]))
                    return false;
            }

            return IsInterior(p);
        }

        /// <summary>
        /// <c>Boundary(e)</c>:点 p 是否恰在某条边或顶点上(F-6-3 §一 的 Boundary 集合)。
        /// <para>判据 = 精确整数**叉积 = 0**(共线)∧ 落在线段的 x/z 包围盒内。
        /// 三项全整数、零除法 —— 「除以斜率」的浮点写法在这里被结构性排除。</para>
        /// </summary>
        public bool IsBoundary(WorldPos p)
        {
            int n = Vertices.Length;
            for (int i = 0; i < n; i++)
            {
                if (IsOnBoundarySegment(p, Vertices[i], Vertices[(i + 1) % n]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 射线奇偶核(只对**非边界点**有意义 —— 边界点的奇偶值无定义,已由 <see cref="Contains"/> 先行排除)。
        /// <para>约定(GDD F-6-3 §四「横轴」):射线 = +X,**横轴 = z**,半开区间 = 端点 z
        /// **严格** &gt; p.Z(端点落在射线上不计);交点 x 须**严格大于** p.X 才计入。</para>
        /// <para>该组合对**共享顶点**天然不双重计数:顶点 z 恰等于 p.Z ⇒ 它不在半开集内,
        /// 故在该顶点相接的两条边不会**同时**计入(EC-6-9 要的正是这个,无需邻边配对查表)。
        /// 专门夹具见 <c>test_ecozone_query_rayThroughVertex_noDoubleCount</c>(U 形缺口多边形)。</para>
        /// </summary>
        private bool IsInterior(WorldPos p)
        {
            int crossings = 0;
            int n = Vertices.Length;

            for (int i = 0; i < n; i++)
            {
                WorldPos a = Vertices[i];
                WorldPos b = Vertices[(i + 1) % n];

                // 端点 z 分居射线两侧(半开:仅一端严格 > p.Z)。
                bool aAbove = a.Z > p.Z;
                bool bAbove = b.Z > p.Z;
                if (aAbove == bAbove)
                    continue;

                // 交点 x 与 p.X 的比较,交叉相乘免除法:
                //   x_int > p.X  ⇔  (p.Z - a.Z)·dx  ⋚  (p.X - a.X)·dz    (dz 的符号定方向)
                // 两侧各自先转 long 再乘 —— 先 int 减法再提升在满量程下已回绕。
                long dx = b.X - a.X;
                long dz = b.Z - a.Z;
                long lhs = ((long)p.Z - a.Z) * dx;
                long rhs = ((long)p.X - a.X) * dz;
                bool ahead = dz > 0 ? lhs > rhs : lhs < rhs;
                if (ahead)
                    crossings++;
            }

            return (crossings & 1) == 1;
        }

        /// <summary>
        /// 点 p 是否落在线段 a–b 上(含端点:共享顶点必须是 Boundary,否则两区在共点上
        /// 双双返回 NONE,F-6-3 §三 第 3 条要求判 min(id))。
        /// </summary>
        private static bool IsOnBoundarySegment(WorldPos p, WorldPos a, WorldPos b)
        {
            // 共线判定:cross(b − a, p − a) == 0(叉积 z 分量)。
            long cross = ((long)b.X - a.X) * ((long)p.Z - a.Z)
                       - ((long)p.X - a.X) * ((long)b.Z - a.Z);
            if (cross != 0)
                return false;

            return p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X)
                && p.Z >= Math.Min(a.Z, b.Z) && p.Z <= Math.Max(a.Z, b.Z);
        }
    }

    /// <summary>生态区注册表(装载自烘焙数据;查询为纯函数)。</summary>
    public sealed class EcozoneRegistry
    {
        private readonly Dictionary<EcozoneId, EcozonePolygon> _polygons =
            new Dictionary<EcozoneId, EcozonePolygon>();

        // min(id) 裁决序的**排序义务**落点:每次 Register 作废,查询时惰性重建。
        // 缓存是纯派生量(与 ADR-009 派生态同构),不引入第二种真源。
        private EcozoneId[] _sortedIds = Array.Empty<EcozoneId>();

        /// <summary>已注册生态区数量。</summary>
        public int Count => _polygons.Count;

        /// <summary>
        /// 注册生态区多边形。
        /// <para>加载期**廉价**硬失败(线性 + O(顶点数²)):顶点 &lt; 3 / 重合相邻顶点 /
        /// 真自交。**内部重叠**(AC-6-19 ①)**不在此处**判 —— 归 ADR-014 阶段 2 /
        /// ADR-022 C4 的构建期校验器(见文件头)。</para>
        /// </summary>
        /// <exception cref="ArgumentException">id 为哨兵 / 顶点数不足 / 相邻顶点重合 / 多边形自交 / id 重复注册。</exception>
        public void Register(EcozonePolygon polygon)
        {
            if (polygon.Id == EcozoneId.None)
                throw new ArgumentException(
                    "不能注册哨兵 id(-1;GDD :634 断言 ecozone_id ≥ 0)", nameof(polygon));

            WorldPos[] v = polygon.Vertices;
            if (v == null || v.Length < 3)
                throw new ArgumentException(
                    $"多边形至少需要 3 个顶点(AC-6-19 ②),实际={v?.Length ?? 0}", nameof(polygon));

            int n = v.Length;
            for (int i = 0; i < n; i++)
            {
                if (v[i].Equals(v[(i + 1) % n]))
                    throw new ArgumentException(
                        $"生态区 {polygon.Id.Value} 第 {i} 与第 {(i + 1) % n} 个顶点重合"
                        + $"(EC-6-7 / AC-6-19 ⑤:重合相邻顶点使射线法无定义)", nameof(polygon));
            }

            ValidateNoSelfIntersection(polygon);

            if (_polygons.ContainsKey(polygon.Id))
                throw new ArgumentException(
                    $"生态区 id {polygon.Id.Value} 重复注册(静默覆盖会让同一 id 有两份几何,"
                    + "重建结果取决于注册顺序 —— 破坏三源不变量)", nameof(polygon));

            _polygons[polygon.Id] = polygon;
            _sortedIds = Array.Empty<EcozoneId>();   // 作废派生缓存
        }

        /// <summary>
        /// 廉价自交检测:任取**非相邻**边对,若它们**真交叉**(跨立试验双向严格异号)
        /// ⇒ 该多边形自交,射线法的奇偶结果无定义 ⇒ 装载期硬失败(EC-6-7 / AC-6-19 ③)。
        /// <para>跨立试验全整数、零除法。<b>真交叉**不含**共线重叠 / 端点接触 ——
        /// 那类退化由「重合相邻顶点」与零面积守卫接(共线边的自交是**充分不必要**的漏检面,
        /// 如实登记为 MINOR 残留,由 ADR-022 C4 的完整校验器兜)。</para>
        /// </summary>
        private static void ValidateNoSelfIntersection(EcozonePolygon polygon)
        {
            WorldPos[] v = polygon.Vertices;
            int n = v.Length;

            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                for (int j = i + 1; j < n; j++)
                {
                    // 相邻边共享端点 ⇒ 叉积含 0 ⇒ 不可能是「真交叉」,直接跳过。
                    if (j == i2 || (j + 1) % n == i)
                        continue;

                    int j2 = (j + 1) % n;
                    if (SegmentsProperlyCross(v[i], v[i2], v[j], v[j2]))
                    {
                        throw new ArgumentException(
                            $"生态区 {polygon.Id.Value} 自交:边 {i}→{i2} 与边 {j}→{j2} 真交叉"
                            + $"(EC-6-7 / AC-6-19 ③:射线法对自交多边形无定义)", nameof(polygon));
                    }
                }
            }
        }

        /// <summary>两条线段是否**真交叉**(四个叉积全非零 ∧ 双向严格异号)。</summary>
        private static bool SegmentsProperlyCross(WorldPos p1, WorldPos p2, WorldPos p3, WorldPos p4)
        {
            long d1 = Cross(p3, p4, p1);
            long d2 = Cross(p3, p4, p2);
            long d3 = Cross(p1, p2, p3);
            long d4 = Cross(p1, p2, p4);

            // 任一叉积 = 0 ⇒ 端点接触 / 共线,不算「真交叉」(那是另一类退化,由别的守卫接)。
            if (d1 == 0 || d2 == 0 || d3 == 0 || d4 == 0)
                return false;

            return (d1 > 0) != (d2 > 0) && (d3 > 0) != (d4 > 0);
        }

        /// <summary>叉积 z 分量:cross(b − a, c − a)。两侧各自先转 long(满量程 i32 防 int 回绕)。</summary>
        private static long Cross(WorldPos a, WorldPos b, WorldPos c)
            => ((long)b.X - a.X) * ((long)c.Z - a.Z) - ((long)c.X - a.X) * ((long)b.Z - a.Z);

        /// <summary>
        /// <c>EcozoneOf(cell)</c>:返回 cell 所属生态区 id(F-6-3 §一 的伪代码逐条落地)。
        /// <para>① 严格内部命中 ⇒ 取其中 min(id);
        /// ② 否则 <c>min(boundary_hits).id</c>;
        /// ③ 否则 <c>NONE</c>。</para>
        /// <para>⚠️ min 由**排序 + 命中即停**得到,不是「收集全部再比大小」—— 两条等价,
        /// 但排序版省掉命中后的无谓扫描,且把「min 是机制而非巧合」写在数据流上
        /// (story Implementation Note 3:禁「先命中的赢」的短路)。</para>
        /// </summary>
        /// <param name="cell">格索引(只用 x / z)</param>
        /// <exception cref="ArgumentOutOfRangeException">|x| 或 |z| 超 <see cref="WorldLatticeParams.MaxWorldHalfExtent"/>(int64 中间积的界,承 ADR-012 F7)。</exception>
        public EcozoneId EcozoneOf(WorldPos cell)
        {
            // int64 安全区入口断言(Story 001 Guardrail:量程超安全区 ⇒ 硬失败,不 clamp ——
            // clamp 会把溢出变成静默的错误格距)。未限界的 p 使因子达 2^32 ⇒ 中间积可回绕。
            const int w = WorldLatticeParams.MaxWorldHalfExtent;
            if (cell.X < -w || cell.X > w || cell.Z < -w || cell.Z > w)
                throw new ArgumentOutOfRangeException(nameof(cell),
                    $"查询格索引超 int64 安全区: ({cell.X}, ·, {cell.Z}),|轴| 上界 {w}");

            EcozoneId[] ids = SortedIds();

            foreach (EcozoneId id in ids)
            {
                if (_polygons[id].Contains(cell))
                    return id;
            }

            foreach (EcozoneId id in ids)
            {
                if (_polygons[id].IsBoundary(cell))
                    return id;
            }

            return EcozoneId.None;
        }

        /// <summary>按 id 升序的 id 数组(惰性重建;Register 作废)。</summary>
        private EcozoneId[] SortedIds()
        {
            if (_sortedIds.Length != _polygons.Count)
            {
                var list = new List<EcozoneId>(_polygons.Keys);
                list.Sort(static (a, b) => a.Value.CompareTo(b.Value));
                _sortedIds = list.ToArray();
            }
            return _sortedIds;
        }

        /// <summary>
        /// 所有已注册生态区 id,**按 id 升序**(契约显式;不暴露 Dictionary 序 ——
        /// 未定义顺序会让下游的顺序敏感行为变成隐式非确定性)。
        /// </summary>
        public List<EcozoneId> GetAllEcozoneIds()
        {
            var list = new List<EcozoneId>(SortedIds());
            return list;
        }
    }
}
