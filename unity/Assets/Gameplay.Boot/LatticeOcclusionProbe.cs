// 权威来源:docs/tech-debt-register.md `TD-audio-035` ②(遮挡判定恒 false)
//   · GDD `audio-system.md` F-44.1(隔门 ≠ 静音,是变闷)· `AC-44-D8` ①
//   · ADR-015 §一(两层世界纪律:遮挡输入 = 逻辑层整数格,**禁读表现态位置**)
//   · ADR-018 §四(遮挡 = 44 本地判定,不进流)
//   · 裁定:2026-10-29(用户裁定 **甲案**:改双格签名 + 格级视线走查)
//
// ⚠️ **本类为什么住 `Gameplay.Boot` 而不是 44 的命名空间**(硬约束,不是偏好):
//   AC-44-B3 的入口白名单门(`assembly_boundary_test`)禁止 44 的**公开签名**出现
//   `Sim` / `Sim.Codec` 程序集的类型 —— 而格级走查的输入是 `WorldGeometry`(住 `Sim.World`)。
//   ⇒ 走查住装配层,44 侧只收一个 `Func<Int3, Int3, bool>` 委托(`Int3` 住 `Sim.Contracts`,
//   是 44 入口白名单的既有成员)。
//   (承 `AudioAssemblySeams` 的既有先例 —— 它也是「住 Boot、持引擎/上游类型、只把缝交给 44」。)
//
// ⚠️ **为什么不用 `Physics.Raycast`**(用户裁定的核心):
//   ① 物理射线读的是**视觉层**几何(float,Unity Terrain / 摆件),而 ADR-015 §一 明令
//      视觉层采样**禁入 sim**;遮挡虽是表现层的事,但它的**输入**必须可复现;
//   ② 今天世界几何里**已经有墙**:`world_geometry.cooked` 的 `walkable` 是 16×1×16 外环全 0
//      ⇒ 格级遮挡的输入**已存在**,不需要新数据;
//   ③ 物理射线在本项目里**零实现**(全库无投射代码)—— 引入它是新增引擎依赖,而格级走查是纯整数。
//
// ⚠️ **判据形状**:只回答「这两格之间有没有**不可走格**挡住」。
//   **不**回答「声音绕过去要多远」—— 那是 `OcclusionDriver` 的 lowpass 语义(变闷),不是几何。

using System;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>格级视线走查 —— 44 的遮挡判定输入(`TD-audio-035` ②)。
    /// <para>把「听者到声源之间是否被墙挡住」降为**整数格**问题:三维 Bresenham 走查,
    /// 沿途遇到任一不可走格 ⇒ 遮挡。</para></summary>
    /// <remarks>⚠️ **端点不计入走查**:听者格 / 声源格本身不可走是合法的
    /// (声源恰在门口、听者站在门框格上),把端点算进去会把这两类正常情形误判成遮挡。</remarks>
    public static class LatticeOcclusionProbe
    {
        /// <summary>两格之间是否存在不可走格挡住(不含两端点)。</summary>
        /// <param name="geometry">世界几何(装载自 `world_geometry.cooked`)。
        /// ⚠️ 零尺寸 ⇒ 返回 <c>false</c>(「未装载世界」不是「没遮挡」,故调用方须另有可观测面)。</param>
        /// <param name="from">起点格(通常 = 听者格)。</param>
        /// <param name="to">终点格(通常 = 声源格)。</param>
        /// <returns>存在阻挡 ⇒ <c>true</c>。</returns>
        /// <remarks>⚠️ 起点 == 终点 ⇒ <c>false</c>(零长视线不可能被挡)。
        /// ⚠️ 越界格按 `WorldGeometry.GetTerrain` 的 fail-closed 口径 = 不可走 ⇒ 视为阻挡
        /// (与「未驻留 chunk 视为 block」ADR-014 §五 同源)。</remarks>
        public static bool IsBlocked(WorldGeometry geometry, Int3 from, Int3 to)
        {
            if (geometry.SizeX <= 0 || geometry.SizeY <= 0 || geometry.SizeZ <= 0) return false;
            if (from.X == to.X && from.Y == to.Y && from.Z == to.Z) return false;

            int dx = Math.Abs(to.X - from.X);
            int dy = Math.Abs(to.Y - from.Y);
            int dz = Math.Abs(to.Z - from.Z);

            int sx = Math.Sign(to.X - from.X);
            int sy = Math.Sign(to.Y - from.Y);
            int sz = Math.Sign(to.Z - from.Z);

            // 三维 Bresenham:每步沿**最长轴**推进,另两轴按累积误差择机跟进一步。
            int x = from.X, y = from.Y, z = from.Z;
            int steps = Math.Max(dx, Math.Max(dy, dz));
            int errX = 0, errY = 0, errZ = 0;

            for (int i = 0; i < steps; i++)
            {
                errX += dx; errY += dy; errZ += dz;
                if (errX >= steps) { x += sx; errX -= steps; }
                if (errY >= steps) { y += sy; errY -= steps; }
                if (errZ >= steps) { z += sz; errZ -= steps; }

                // 端点不计入(最后一次迭代到达的是 to)
                if (i == steps - 1) break;

                if (!geometry.GetTerrain(new WorldPos(x, y, z)).Walkable) return true;
            }

            return false;
        }
    }
}
