// `world_nav_{chunk}.json`(导航格切片 · ADR-022 §五)的阶段 2 绑定器。
//
// 权威来源:
//   ADR-022 §三(导出契约)· §五(**按 chunk 切片**;`{chunk}` 键 = ADR-015 §四 的同一 chunk)
//   ADR-014 §三(根 / 行白名单;未知键 = 构建失败)· §四(聚合 throw)
//   `production/epics/level-tool/story-005-world-nav-chunk-slicing.md` AC-2 / AC-5
//
// ⚠️ **本件刻意不碰 `UnityEngine` / Addressables**(承 `WorldGeometryBinder` 同款头注)。
//
// ⚠️ **四条域门**(任一违例 ⇒ 聚合 `BakeValidationException`):
//   ① 声明尺寸非法(任一轴 < 1 / chunk 格总数超 int 域)
//   ② `chunk_index` 越界,或 `chunk` 三元组与 `chunk_index` **不自洽** ——
//      后者是「文件名说 3、内容说 1」的唯一抓法(名字在盘上,内容在字节里,只有绑定期能对上)
//   ③ `walkable` 长度 ≠ `chunkSizeX × chunkSizeY × chunkSizeZ`,或含非 0/1 值
//   ④ 切片与世界尺寸不兼容:chunk 原点须落在世界内 ——
//      否则该片全是衬垫格,而「一片全衬垫」在运行期表现为「这几格永远查不到」
//
// ⚠️ 报文前缀 `world_nav_{i}:`,各门再点自己的判据词 —— 测试按**字面量**断言关键词。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.World;   // ChunkTopology(chunk 划分的唯一函数)

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>world_nav_{chunk}.json</c> 的阶段 2 绑定器。</summary>
    public static class WorldNavChunkBinder
    {
        /// <summary>作者态源文件名(`world_nav_{chunk}.json`;与 `WorldDataSets.NavChunkFileName` 同形状)。</summary>
        public static string FileNameFor(int chunkIndex) => "world_nav_" + chunkIndex + ".json";

        /// <summary>根对象白名单(ADR-014 §三:未知键 = 构建失败)。</summary>
        internal static readonly string[] RootKeys =
        {
            "schema_version", "level_id", "world_size", "chunk_size",
            "chunk_index", "chunk", "walkable",
        };

        /// <summary>绑定结果(已通过全部域门)。</summary>
        public readonly struct BindResult
        {
            /// <summary>世界尺寸 X(格)。</summary>
            public readonly int WorldSizeX;
            /// <summary>世界尺寸 Y(格)。</summary>
            public readonly int WorldSizeY;
            /// <summary>世界尺寸 Z(格)。</summary>
            public readonly int WorldSizeZ;

            /// <summary>chunk 边长 X(格)。</summary>
            public readonly int ChunkSizeX;
            /// <summary>chunk 边长 Y(格)。</summary>
            public readonly int ChunkSizeY;
            /// <summary>chunk 边长 Z(格)。</summary>
            public readonly int ChunkSizeZ;

            /// <summary>chunk 一维索引。</summary>
            public readonly int ChunkIndex;

            /// <summary>稠密可走性(0/1;chunk 内局部索引)。</summary>
            public readonly IReadOnlyList<int> Walkable;

            /// <summary>源 schema_version(写方头槽 offset 8 的唯一真值来源)。</summary>
            public readonly uint SchemaVersion;

            /// <summary>构造绑定结果。</summary>
            public BindResult(int worldSizeX, int worldSizeY, int worldSizeZ,
                              int chunkSizeX, int chunkSizeY, int chunkSizeZ,
                              int chunkIndex, IReadOnlyList<int> walkable, uint schemaVersion)
            {
                WorldSizeX = worldSizeX; WorldSizeY = worldSizeY; WorldSizeZ = worldSizeZ;
                ChunkSizeX = chunkSizeX; ChunkSizeY = chunkSizeY; ChunkSizeZ = chunkSizeZ;
                ChunkIndex = chunkIndex; Walkable = walkable; SchemaVersion = schemaVersion;
            }

            /// <summary>本片的世界尺寸 + chunk 划分。</summary>
            public ChunkTopology Topology => ChunkTopology.ForWorld(
                WorldSizeX, WorldSizeY, WorldSizeZ, ChunkSizeX, ChunkSizeY, ChunkSizeZ);
        }

        /// <summary>绑定 + 四条域门。</summary>
        /// <param name="json">作者态 JSON 文本。</param>
        /// <param name="expectedChunkIndex">文件名声明的 chunk 号(用于「名字 ↔ 内容自洽」门);
        /// 传 `-1` = 不校验(测试负例用)。</param>
        /// <exception cref="BakeValidationException">词法失败 / 任一校验失败(聚合后一次 throw)。</exception>
        public static BindResult Bind(string json, int expectedChunkIndex = -1)
        {
            var errors = new List<string>();

            if (!JsonStage1Lexer.TryParse(json, out JsonNode root, out string lexErr))
                throw new BakeValidationException(
                    new List<string> { $"world_nav:阶段1 词法失败:{lexErr}" });

            if (root.Kind != JsonNodeKind.Object)
                throw new BakeValidationException(
                    new List<string> { $"world_nav:根须为对象(实得 {root.Kind})" });

            ClinicMachineSchema.RejectUnknownKeys(root, RootKeys, "根", "world_nav", errors);

            uint schemaVersion = ClinicMachineSchema.ReadSchemaVersion(
                root, "world_nav",
                DaYiJingCheng.Gameplay.Presentation.WorldNavChunkCookedCodec.ExpectedSchemaVersion,
                errors);

            ClinicMachineSchema.ReadRequiredString(root, "level_id", "根", "world_nav", errors, out _);

            int wsX = 0, wsY = 0, wsZ = 0;
            bool worldOk = TryReadTriple(root, "world_size", min: 1, "world_nav", errors,
                                         out wsX, out wsY, out wsZ);

            int csX = 0, csY = 0, csZ = 0;
            bool chunkSizeOk = TryReadTriple(root, "chunk_size", min: 1, "world_nav", errors,
                                             out csX, out csY, out csZ);

            int chunkIndex = -1;
            bool indexOk = TryReadInt(root, "chunk_index", 0, int.MaxValue, "world_nav", errors, out chunkIndex);

            // ② chunk 三元组与索引自洽 —— 需要 chunk_size 先读出来才能算划分。
            if (chunkSizeOk && worldOk)
            {
                var topo = ChunkTopology.ForWorld(wsX, wsY, wsZ, csX, csY, csZ);
                if (indexOk && chunkIndex >= topo.ChunkCount)
                {
                    errors.Add(
                        $"world_nav:chunk_index = {chunkIndex} 越界 —— 本世界共 {topo.ChunkCount} 个 chunk " +
                        $"({topo.ChunksX}×{topo.ChunksY}×{topo.ChunksZ});" +
                        "越界索引会让「文件名里的 chunk 键」指向一块不存在的切片");
                    indexOk = false;
                }

                // ⚠️ `chunk` 三元组的**下界是 0**(第一个 chunk 的原点就是 (0,0,0))——
                //    与 `world_size` / `chunk_size` 的 `≥ 1` 不同。写成同一个下界会让**每一份**
                //    含有 0 坐标的切片被拒(实测:首跑四条切片全部报「chunk[0] 须为 [1, …]」)。
                if (TryReadTriple(root, "chunk", min: 0, "world_nav", errors, out int cx, out int cy, out int cz))
                {
                    if (indexOk)
                    {
                        var expect = topo.IndexToChunk(chunkIndex);
                        if (cx != expect.X || cy != expect.Y || cz != expect.Z)
                            errors.Add(
                                $"world_nav:chunk 三元组 ({cx},{cy},{cz}) 与 chunk_index = {chunkIndex} **不自洽** —— " +
                                $"按 ChunkTopology 该索引对应 ({expect.X},{expect.Y},{expect.Z})。" +
                                "这是「文件名说一块、内容说另一块」的唯一抓法(名字在盘上、内容在字节里)");
                    }

                    // ④ chunk 原点须落在世界内(否则整片都是衬垫).
                    if (cx < 0 || cy < 0 || cz < 0 ||
                        cx * csX >= wsX || cy * csY >= wsY || cz * csZ >= wsZ)
                        errors.Add(
                            $"world_nav:chunk 原点 ({cx},{cy},{cz}) × chunk_size 落在世界 " +
                            $"({wsX},{wsY},{wsZ}) **之外** —— 该片全是衬垫格," +
                            "运行期表现为「这几格永远查不到」,而产物本身毫无异常");

                    if (expectedChunkIndex >= 0 && indexOk && expectedChunkIndex != chunkIndex)
                        errors.Add(
                            $"world_nav:文件名声明的 chunk 号 = {expectedChunkIndex},而载荷里是 {chunkIndex} —— " +
                            "把 A 片的字节装到 B 片的地址上,运行期会静默读到邻块的导航格");
                }
            }

            IReadOnlyList<int> walkable = null;
            if (chunkSizeOk)
            {
                long cells = (long)csX * csY * csZ;
                if (cells > int.MaxValue)
                    errors.Add($"world_nav:chunk 格总数 {cells} 超出 int 域 —— 块内索引不可表示");
                else
                    walkable = TryReadWalkable(root, (int)cells, errors);
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);

            return new BindResult(wsX, wsY, wsZ, csX, csY, csZ, chunkIndex, walkable, schemaVersion);
        }

        /// <summary>读一个 `[a, b, c]` 三元整数数组(每项 ≥ <paramref name="min"/>)。</summary>
        private static bool TryReadTriple(
            JsonNode root, string key, int min, string file, List<string> errors,
            out int a, out int b, out int c)
        {
            a = b = c = 0;
            if (!root.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add($"{file}:缺 {key} 数组");
                return false;
            }
            if (n.Items.Count != 3)
            {
                errors.Add($"{file}:{key} 须为三元数组(实得 {n.Items.Count} 项)");
                return false;
            }
            var comp = new int[3];
            for (int i = 0; i < 3; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.Integer || item.Int < min || item.Int > int.MaxValue)
                {
                    errors.Add($"{file}:{key}[{i}] 须为 [{min}, {int.MaxValue}] 的整数" +
                               $"(实得 {item.Kind}={item.RawText})");
                    return false;
                }
                comp[i] = (int)item.Int;
            }
            a = comp[0]; b = comp[1]; c = comp[2];
            return true;
        }

        /// <summary>读一个整数(闭区间)。</summary>
        private static bool TryReadInt(
            JsonNode root, string key, int min, int max, string file, List<string> errors, out int value)
        {
            value = 0;
            if (!root.TryGet(key, out JsonNode n) || n.Kind != JsonNodeKind.Integer)
            {
                errors.Add($"{file}:缺整数 {key}");
                return false;
            }
            if (n.Int < min || n.Int > max)
            {
                errors.Add($"{file}:{key} = {n.Int} 超出 [{min}, {max}]");
                return false;
            }
            value = (int)n.Int;
            return true;
        }

        /// <summary>`walkable` —— 长度须恰为 chunk 格数,每项 ∈ {0,1}。</summary>
        /// <remarks>⚠️ **不设「全 0」门**。与几何侧(`Walkable` 全 0 = 全世界走不到)不同:
        /// 一片全 0 是**合法**的(一整块墙 / 一整块不可通行区),它的判据面归 C3 与 C6,
        /// 不归本门。在绑定期把「全 0」判成失败会让真实关卡无法表达「一块实心区域」。</remarks>
        private static IReadOnlyList<int> TryReadWalkable(JsonNode root, int expected, List<string> errors)
        {
            if (!root.TryGet("walkable", out JsonNode n) || n.Kind != JsonNodeKind.Array)
            {
                errors.Add("world_nav:缺 walkable 数组 —— 可走性是本产物的**全部内容**");
                return null;
            }
            if (n.Items.Count != expected)
            {
                errors.Add(
                    $"world_nav:walkable 长度 = {n.Items.Count},须等于 chunk 格数 {expected} —— " +
                    "长度不齐会让块内索引错位(静默:相邻 chunk 的格子互相顶替)");
                return null;
            }

            var values = new int[expected];
            for (int i = 0; i < expected; i++)
            {
                JsonNode item = n.Items[i];
                if (item.Kind != JsonNodeKind.Integer || (item.Int != 0 && item.Int != 1))
                {
                    errors.Add(
                        $"world_nav:walkable[{i}] 须为 0 或 1(实得 {item.Kind}={item.RawText}) —— " +
                        "非 0/1 会让读方猜「非零即真」,而 0b10 之类的位值是真值域里的假值");
                    return null;
                }
                values[i] = (int)item.Int;
            }
            return Array.AsReadOnly(values);
        }
    }
}
