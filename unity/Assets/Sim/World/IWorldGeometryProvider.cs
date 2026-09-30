// 权威来源:ADR-015 §三(单一整数格)· ADR-022(关卡工具)· Story 001(世界格与可走性基础)
//
// 核心机制:
//   - 世界几何数据的装载入口(来自烘焙产物 world_*.cooked)
//   - 运行期零几何生成、零视觉层采样

using System;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.World
{
    /// <summary>世界几何数据提供者接口(运行期只读)。</summary>
    public interface IWorldGeometryProvider
    {
        /// <summary>世界格参数(断言后不可变)。</summary>
        WorldLatticeParams Params { get; }

        /// <summary>世界几何数据。</summary>
        WorldGeometry Geometry { get; }

        /// <summary>查询某格的地形数据。</summary>
        TerrainCell GetTerrain(WorldPos pos);

        /// <summary>查询某格的 POI id(-1 = 无)。</summary>
        int GetPoiId(WorldPos pos);

        /// <summary>查询某格的资源 id(-1 = 无)。</summary>
        int GetResourceId(WorldPos pos);
    }
}
