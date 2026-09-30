// 权威来源:ADR-015 §三(单一整数格)· ADR-009 §四(派生态/三态)· Story 002(EcozoneOf 整数几何查询)
//
// 核心机制:
//   - EcozoneOf(cell) => ecozone_id 整数多边形包含判定
//   - 整数射线投射(i32 域,无 float)
//   - 生态区 id = min(多边形内 POI id) 仲裁(单调不变量)
//   - NONE = -1 哨兵(ADR-007 §四 同型)
//   - 生态区语义为「垂直柱体」:Y 范围取顶点 Y 的 min/max

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>生态区 id 类型(int)。</summary>
    public readonly struct EcozoneId : IEquatable<EcozoneId>
    {
        public readonly int Value;

        public EcozoneId(int value) => Value = value;

        public bool Equals(EcozoneId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EcozoneId id && Equals(id);
        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(EcozoneId a, EcozoneId b) => a.Equals(b);
        public static bool operator !=(EcozoneId a, EcozoneId b) => !a.Equals(b);

        /// <summary>无生态区哨兵(-1)。</summary>
        public static EcozoneId None => new EcozoneId(-1);

        /// <summary>是否有效(非哨兵)。</summary>
        public bool IsValid => Value >= 0;
    }

    /// <summary>生态区多边形(整数顶点,垂直柱体语义)。</summary>
    public readonly struct EcozonePolygon
    {
        public readonly EcozoneId Id;
        public readonly WorldPos[] Vertices;

        public EcozonePolygon(EcozoneId id, WorldPos[] vertices)
        {
            if (vertices == null || vertices.Length < 3)
                throw new ArgumentException("多边形至少需要 3 个顶点", nameof(vertices));

            Id = id;
            Vertices = vertices;
        }

        /// <summary>
        /// 整数 crossing-number 判定:点 p 是否在 XZ 平面多边形内。
        /// 先做 Y 范围检查(垂直柱体),再投影到 XZ 平面做射线法。
        /// 射线方向: +Z (world space forward)。
        /// </summary>
        public bool Contains(WorldPos p)
        {
            // Y 范围检查:点 Y 须落在顶点 Y 的 [min, max] 内
            int minY = Vertices[0].Y;
            int maxY = Vertices[0].Y;
            for (int i = 1; i < Vertices.Length; i++)
            {
                if (Vertices[i].Y < minY) minY = Vertices[i].Y;
                if (Vertices[i].Y > maxY) maxY = Vertices[i].Y;
            }
            if (p.Y < minY || p.Y > maxY)
                return false;

            // XZ 平面 crossing-number
            // 沿 +Z 方向发射线,数与多边形边的交点
            // 条件:边端点 Z 在射线两侧,且交点 X > p.X
            int crossings = 0;
            int n = Vertices.Length;

            for (int i = 0; i < n; i++)
            {
                WorldPos a = Vertices[i];
                WorldPos b = Vertices[(i + 1) % n];

                // 端点 Z 在射线(Z=p.Z)两侧?
                bool aAbove = a.Z > p.Z;
                bool bAbove = b.Z > p.Z;
                if (aAbove == bAbove)
                    continue; // 同侧,不相交

                // 计算交点 X: x = a.X + (p.Z - a.Z) * (b.X - a.X) / (b.Z - a.Z)
                // 用交叉相乘避免除法: (p.Z - a.Z)*(b.X - a.X) + a.X*(b.Z - a.Z) > p.X*(b.Z - a.Z)
                int dz = b.Z - a.Z;
                int dx = b.X - a.X;
                long lhs = (long)(p.Z - a.Z) * dx + (long)a.X * dz;
                long rhs = (long)p.X * dz;
                if (lhs > rhs)
                    crossings++;
            }

            return (crossings & 1) == 1;
        }
    }

    /// <summary>生态区注册表(装载自烘焙数据)。</summary>
    public sealed class EcozoneRegistry
    {
        private readonly Dictionary<EcozoneId, EcozonePolygon> _polygons = new Dictionary<EcozoneId, EcozonePolygon>();

        public int Count => _polygons.Count;

        /// <summary>注册生态区多边形。</summary>
        public void Register(EcozonePolygon polygon)
        {
            if (polygon.Id == EcozoneId.None)
                throw new ArgumentException("不能注册哨兵 id(-1)", nameof(polygon));

            _polygons[polygon.Id] = polygon;
        }

        /// <summary>
        /// EcozoneOf(cell):返回 cell 所属生态区 id。
        /// 结果 = 包含该点的多边形中 id 最小的那个(ADR-021 §三 min(id) 仲裁)。
        /// </summary>
        public EcozoneId EcozoneOf(WorldPos cell)
        {
            EcozoneId? foundId = null;

            foreach (var kv in _polygons)
            {
                if (kv.Value.Contains(cell))
                {
                    if (!foundId.HasValue || kv.Key.Value < foundId.Value.Value)
                        foundId = kv.Key;
                }
            }

            return foundId ?? EcozoneId.None;
        }

        /// <summary>保留接口兼容,已无操作。</summary>
        [Obsolete("BuildCache is no longer used; EcozoneOf queries directly.")]
        public void BuildCache(IEnumerable<WorldPos> probeCells) { }

        /// <summary>获取所有已注册生态区 id 列表。</summary>
        public List<EcozoneId> GetAllEcozoneIds()
        {
            return _polygons.Keys.ToList();
        }
    }
}
