// 关卡工具 —— **C5 边界单一源**:生态区遮罩由**整数多边形生成**,并可与外部遮罩比对。
//
// 权威来源:
//   ADR-022 §四 **C5**(「视觉层生态区遮罩由整数多边形生成,非手画第二份」= **硬失败**)
//   GDD `world-and-ecozones.md` **R-6-15 缝 2**(「边界是**一次定义、两次派生**」)
//     · §Edge Cases 的静默失败说明(画面与判定各自成立、只是不一致)
//   F-6-3(开集语义 + 裁决序 min(id))—— 遮罩逐格的取值**就是** `EcozoneOf` 的返回值
//
// ═══════════════════════════════════════════════════════════════════════════
// 本件做什么、不做什么(如实划界)
// ═══════════════════════════════════════════════════════════════════════════
// **做**:把「视觉层遮罩应当长什么样」从整数多边形**算**出来 —— 一张逐格的生态区 id 图
//       (`-1` = `NONE` 野地)。它有两个用途:
//         ① **生成**:视觉层(Unity Terrain 的 splat / 生态区图层)的**唯一输入**;
//         ② **比对**:任何声称是「视觉边界」的遮罩都可以拿它与生成结果逐格比对
//            —— 不一致即「手画了第二份」(C5 的判据)。
//
// **不做**:**不生成 Unity Terrain 资产本身**。全仓今日**零份**真实美术地形
//       (AC-0 spike §七 已实测:`grep -rl "TerrainData" unity/Assets` 在 S2 之前零命中)。
//       ⇒ 故事卡 §诚实边界 2 说的「乙路(逐像素比对已有遮罩)无法验证」**在这里成立**:
//         比对的**机制**已实现且有夹具,但**没有真实遮罩可供比对**。如实登记,不借绿。
//
// ⚠️ 遮罩的**逐格取值等于** `EcozoneRegistry.EcozoneOf` —— 不另写一套判定。
//    这是 C5「单一源」在代码层面的含义:两份实现就是两份源,哪怕今天取值相同。
// ⚠️ 生成是**纯函数 + 整数**:同输入两次 ⇒ 逐字节相同(AC-3 ①)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;   // WorldPos
using DaYiJingCheng.Sim.World;       // EcozoneId / EcozonePolygon / EcozoneRegistry

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>
    /// 生态区遮罩:一张 `Width × Depth` 的逐格 id 图(`-1` = `EcozoneId.None`)。
    /// <para>⚠️ 索引 = `x * Depth + z`,与 `WorldGeometry.ToIndex` 在单层世界(`SizeY = 1`)下一致。</para>
    /// </summary>
    public readonly struct EcozoneMask
    {
        /// <summary>X 方向格数(≥ 1)。</summary>
        public readonly int Width;

        /// <summary>Z 方向格数(≥ 1)。</summary>
        public readonly int Depth;

        /// <summary>逐格生态区 id(长度 = `Width × Depth`;`-1` = `NONE`)。</summary>
        public readonly int[] ZoneId;

        /// <summary>构造(不做校验 —— 由生成器与比对器各自把关)。</summary>
        public EcozoneMask(int width, int depth, int[] zoneId)
        {
            Width = width; Depth = depth; ZoneId = zoneId;
        }

        /// <summary>
        /// 确定性字节形(little-endian:`i32 W` · `i32 D` · 逐格 `i32 id`)。
        /// <para>用途 = 「同输入两次生成**逐字节相同**」(AC-3 ①)的可判据表述 ——
        /// 比逐元素比数组更严格(它同时钉住顺序与宽度)。</para>
        /// </summary>
        public byte[] ToBytes()
        {
            var bytes = new byte[8 + ZoneId.Length * 4];
            WriteI32(bytes, 0, Width);
            WriteI32(bytes, 4, Depth);
            for (int i = 0; i < ZoneId.Length; i++)
                WriteI32(bytes, 8 + i * 4, ZoneId[i]);
            return bytes;
        }

        private static void WriteI32(byte[] b, int offset, int value)
        {
            unchecked
            {
                b[offset] = (byte)(value & 0xFF);
                b[offset + 1] = (byte)((value >> 8) & 0xFF);
                b[offset + 2] = (byte)((value >> 16) & 0xFF);
                b[offset + 3] = (byte)((value >> 24) & 0xFF);
            }
        }
    }

    /// <summary>C5 的**生成面**:整数多边形 → 逐格生态区 id 图。</summary>
    public static class EcozoneMaskGenerator
    {
        /// <summary>
        /// 由生态区声明生成遮罩。
        /// <para>逐格取值 = `EcozoneRegistry.EcozoneOf(new WorldPos(x, 0, z))` —— **不另写判定**。</para>
        /// </summary>
        /// <param name="source">生态区声明(**须先过 C4**;退化多边形会让判定无定义)。</param>
        /// <param name="width">X 格数(≥ 1)。</param>
        /// <param name="depth">Z 格数(≥ 1)。</param>
        /// <exception cref="InvalidOperationException">声明未过 C4 —— 先跑 <see cref="EcozonePolygonCheck.CheckOrThrow"/>。</exception>
        public static EcozoneMask Generate(EcozoneSource source, int width, int depth)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (width < 1 || depth < 1)
                throw new ArgumentOutOfRangeException(
                    nameof(width), $"遮罩尺寸须 ≥ 1(实得 {width}×{depth})");

            // ⚠️ **先跑 C4**:退化多边形的 `EcozoneOf` 无定义,生成的遮罩会是一张
            //    「看起来正常、其实来自无定义运算」的图 —— 那种假数据最贵。
            EcozonePolygonCheck.CheckOrThrow(source);

            EcozoneRegistry registry = BuildRegistry(source);

            var ids = new int[width * depth];
            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                    ids[x * depth + z] = registry.EcozoneOf(new WorldPos(x, 0, z)).Value;

            return new EcozoneMask(width, depth, ids);
        }

        /// <summary>按声明造一个 `Sim` 侧注册表(遮罩生成的**唯一判定来源**)。</summary>
        /// <exception cref="ArgumentException">`Register` 的廉价校验拒绝(纵深:此时 C4 本应已拦)。</exception>
        internal static EcozoneRegistry BuildRegistry(EcozoneSource source)
        {
            var registry = new EcozoneRegistry();
            foreach (EcozoneZoneSpec z in source.SortedById())
                registry.Register(new EcozonePolygon(new EcozoneId(z.Id), z.Vertices));
            return registry;
        }
    }

    /// <summary>
    /// C5 的**比对面**:任何声称是「视觉边界」的遮罩,与生成结果逐格比对。
    /// <para>不一致 = 「手画了第二份边界」⇒ **硬失败**(ADR-022 §四 C5)。</para>
    /// </summary>
    public static class EcozoneMaskCheck
    {
        /// <summary>比对两张遮罩;不一致即 `throw`,报文点名**第一个**不一致的格。</summary>
        /// <param name="expected">生成结果(唯一权威)。</param>
        /// <param name="actual">待验遮罩(可能来自手画资产)。</param>
        /// <param name="label">待验遮罩的人读来源(进报文)。</param>
        /// <exception cref="InvalidOperationException">尺寸不符 / 任一格 id 不符。</exception>
        public static void Verify(EcozoneMask expected, EcozoneMask actual, string label)
        {
            if (expected.ZoneId == null || actual.ZoneId == null)
                throw new ArgumentException("遮罩数据为 null —— 空遮罩不是「一致」,是「没数据」");

            if (expected.Width != actual.Width || expected.Depth != actual.Depth)
                throw new InvalidOperationException(
                    $"[C5] 视觉层遮罩尺寸({actual.Width}×{actual.Depth})与整数多边形的尺寸" +
                    $"({expected.Width}×{expected.Depth})不符 —— 来源:[{label}]。两张图分辨率不同," +
                    "「同一片地」就无从谈起,更谈不上同源");

            for (int i = 0; i < expected.ZoneId.Length; i++)
            {
                if (expected.ZoneId[i] != actual.ZoneId[i])
                {
                    int x = expected.Depth == 0 ? 0 : i / expected.Depth;
                    int z = expected.Depth == 0 ? 0 : i % expected.Depth;
                    throw new InvalidOperationException(
                        $"[C5] 视觉层遮罩与整数多边形**不同源**:格 ({x},{z}) 期望 ecozone_id = " +
                        $"{expected.ZoneId[i]},实得 {actual.ZoneId[i]} —— 来源:[{label}]。" +
                        "GDD R-6-15 缝 2:边界须**一次定义、两次派生**;手画第二份会让" +
                        "「看起来在 A 区、判定给 B 区」成为可感知的谎言(ADR-022 §四 C5)");
                }
            }
        }
    }
}
