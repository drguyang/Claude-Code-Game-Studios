// 地面覆盖门 —— `World.unity` 的 `Ground` 必须**盖住整张世界格**(`TD-building-06` 的结构性解除)。
//
// ── 病因(实测,不是预防性洁癖)─────────────────────────────────────────────
// `World.unity` 的 `Ground` 原为 Unity 内置 Plane 网格(10×10 米)+ Transform `pos (0,0,0) scale (1,1,1)`,
// 而世界格是 **16×16**(ADR-015 §三:1 格 = 1 米)⇒ 地板只覆盖 `x,z ∈ [-5,5]`,
// 世界格 `[0,15]` 里约 **3/4 没有地面**,玩家走出去即自由落体(实测探针 `player=0,-6,-9` / `y` 为负)。
//
// 2026-11-06 已**就地修掉最小面**(Transform 改 `pos (7.5,0,7.5)` / `scale (1.8,1,1.8)` = 18×18),
// 但**没有任何门** —— 下次改世界尺寸就同型复发,而那时的症状是**静默的**:
// 场景照常加载、构建照常成功,只有走上去的人会掉下去。
//
// ⇒ 本门把「地面 ⊇ 世界」从**当下事实**变成**构建期判据**(菜单 / 域重载 / 构建前三处共用)。
//
// ── 判据(五条,任一不满足即红)───────────────────────────────────────────────
//   C1 **找得到** `Ground` GameObject —— 找不到 ⇒ 红(不是「跳过」:扫描面丢失必须响)。
//   C2 **有碰撞体**(MeshCollider,启用、非 trigger,且与 MeshFilter 同一网格)——
//      渲得出而走不上去是**另一种**形态的不可用,故与 C3 分开判。
//   C3 **平面水平** —— 旋转须为单位四元数(否则 AABB 算式不成立,不再瞎算 ⇒ fail-closed)。
//   C4 **覆盖足量** —— 地面在 `x/z` 上的覆盖面必须 ⊇ 世界格外接(每格 `L` 米,格心在整数坐标,
//      故需求区间是 `[-L/2, (size-1)·L + L/2]`);`y` 面须与世界面(0)齐平。
//   C5 **内置网格已登记** —— 网格 fileID 不在本门的内置表里 ⇒ 红(未登记 ⇒ 无法静态判定包围盒,
//      **不得**当作通过)。表可扩,但必须**显式**扩。
//
// ⚠️ **本门只判 `Ground` 这一件**,不判其它场景对象;也不替代 ADR-023 ②(`World.unity 零 gameplay
//    GameObject`)—— 那是另一条判据,有自己的门。两者互补:本条管「走得到」,那条管「预摆不成为第二真源」。
//
// ── 为什么读**场景文本**而不是 `EditorSceneManager.OpenScene` ─────────────────
// 本门要能在**构建前回调**(`IPreprocessBuildWithReport`)与**域重载**里跑。加开一个场景
// (即便 additive)会把「判据」变成「对编辑器场景栈有副作用的东西」,而构建期场景栈正被使用。
// 场景 YAML 是**稳定文本格式**,读它零副作用、可离线复算、也能被夹具用**合成字符串**直测判据本体。
// ⚠️ 已知代价:它**读不到运行期实例化的对象**(今天 `Ground` 就是预摆件,故不构成缺口);
// 若将来地面改成运行期生成,本门会**结构上失效** —— 那时应改判「生成器的输入是否覆盖世界」,
// 而不是把本门删掉了事(删门 = 把这条债还回去)。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>
    /// `World.unity` 的地面必须覆盖整张世界格的构建期门(`TD-building-06` 的结构性解除)。
    /// </summary>
    public static class GroundCoverageGate
    {
        /// <summary>场景路径(相对工程根 `unity/`)。</summary>
        public const string SceneRelativePath = "Assets/Scenes/World.unity";

        /// <summary>地面对象名(唯一真源 —— 判据与报文都读它)。</summary>
        public const string GroundObjectName = "Ground";

        /// <summary>Unity 内置资源的 guid(场景 YAML 里内置网格恒用它)。</summary>
        public const string BuiltinGuid = "0000000000000000e000000000000000";

        /// <summary>内置网格 fileID → 网格在 `x/z` 上的原始边长(米)。</summary>
        /// <remarks>只登记**会被用来当地面**的内置网格;未登记的一律红(fail-closed)。
        /// Plane = 10×10(`fileID 10209`)· Quad = 1×1(`10210`)· Cube = 1×1×1(`10202`,顶面在 `+0.5`)。</remarks>
        public static readonly IReadOnlyDictionary<long, GroundMeshShape> BuiltinMeshes =
            new Dictionary<long, GroundMeshShape>
            {
                [10209] = new GroundMeshShape("Plane", 10f, 10f, topOffsetY: 0f),
                [10210] = new GroundMeshShape("Quad", 1f, 1f, topOffsetY: 0f),
                [10202] = new GroundMeshShape("Cube", 1f, 1f, topOffsetY: 0.5f),
            };

        /// <summary>内置网格的形状(原始尺寸 + 顶面相对 Transform 原点的偏移)。</summary>
        public readonly struct GroundMeshShape
        {
            /// <summary>Unity 内置名(报文用)。</summary>
            public readonly string Name;
            /// <summary>原始 `x` 边长(米)。</summary>
            public readonly float SizeX;
            /// <summary>原始 `z` 边长(米)。</summary>
            public readonly float SizeZ;
            /// <summary>顶面相对 Transform 原点的 `y` 偏移(立方体 = +0.5,平面 / 四边形 = 0)。</summary>
            public readonly float TopOffsetY;

            /// <summary>构造。</summary>
            public GroundMeshShape(string name, float sizeX, float sizeZ, float topOffsetY)
            {
                Name = name; SizeX = sizeX; SizeZ = sizeZ; TopOffsetY = topOffsetY;
            }
        }

        /// <summary>浮点比较容差(米)。</summary>
        public const float Eps = 1e-3f;

        // ══════════════════════════════════════════════════════════════════════
        //  I/O 包装:读真场景 + 真世界几何产物
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 跑本门(真场景 + 真产物)。
        /// </summary>
        /// <param name="projectRoot">工程根(`unity/`;`"."` = 当前目录)。</param>
        /// <returns>违例串列表;空 = 通过。</returns>
        public static List<string> RunAll(string projectRoot)
        {
            var errs = new List<string>();

            string scenePath = Path.Combine(projectRoot, SceneRelativePath);
            if (!File.Exists(scenePath))
            {
                errs.Add($"[地面覆盖] 场景不存在:{scenePath} —— 门无从执行");
                return errs;
            }

            int sizeX, sizeZ, latticeMm;
            try
            {
                var geometry = DaYiJingCheng.Gameplay.Presentation.DataCookedDisk.LoadWorldGeometry();
                sizeX = geometry.SizeX; sizeZ = geometry.SizeZ; latticeMm = geometry.LatticeSizeMm;
            }
            catch (Exception ex)
            {
                errs.Add("[地面覆盖] 读不了 `world_geometry` 产物:" + ex.Message +
                         " —— 无世界尺寸 ⇒ 覆盖判据不可执行(不得按通过处理)");
                return errs;
            }

            string sceneText;
            try { sceneText = File.ReadAllText(scenePath); }
            catch (IOException ex)
            {
                errs.Add($"[地面覆盖] 读不了 {SceneRelativePath}:{ex.Message}");
                return errs;
            }

            errs.AddRange(Check(sceneText, sizeX, sizeZ, latticeMm));
            return errs;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  判据本体(纯函数 —— 夹具用合成场景文本直测)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 判据本体:场景文本 + 世界尺寸 ⇒ 违例串列表。
        /// </summary>
        /// <param name="sceneText">`World.unity` 的 YAML 文本(或夹具合成件)。</param>
        /// <param name="worldSizeX">世界格数(`x`)。</param>
        /// <param name="worldSizeZ">世界格数(`z`)。</param>
        /// <param name="latticeSizeMm">格边长(整数毫米,ADR-015 §三)。</param>
        public static List<string> Check(string sceneText, int worldSizeX, int worldSizeZ, int latticeSizeMm)
        {
            var errs = new List<string>();
            if (sceneText == null) { errs.Add("[地面覆盖] 场景文本为 null —— 判据不可执行"); return errs; }
            if (worldSizeX < 1 || worldSizeZ < 1)
            {
                errs.Add($"[地面覆盖] 世界尺寸非法 ({worldSizeX},{worldSizeZ}) —— 判据不可执行");
                return errs;
            }
            if (latticeSizeMm < 1)
            {
                errs.Add($"[地面覆盖] `LATTICE_SIZE` 非法({latticeSizeMm} mm)—— 判据不可执行");
                return errs;
            }

            // ── C1 找得到 Ground ──────────────────────────────────────────────
            long groundId = FindGameObjectId(sceneText, GroundObjectName);
            if (groundId == 0)
            {
                errs.Add($"[地面覆盖] 场景里找不到对象 `{GroundObjectName}` —— " +
                         "扫描面丢失(不是「无需判」):地面不在,玩家走出去就是自由落体");
                return errs;
            }

            // ── C2 有碰撞体(与 MeshFilter 同一网格)────────────────────────────
            long meshId = 0;
            bool haveMeshFilter = false;
            foreach (Block b in Blocks(sceneText))
            {
                if (b.ClassId == 33 && RefEquals(b.Body, "m_GameObject", groundId))
                {
                    haveMeshFilter = true;
                    meshId = ReadFileId(b.Body, "m_Mesh");
                }
            }
            if (!haveMeshFilter || meshId == 0)
            {
                errs.Add($"[地面覆盖] `{GroundObjectName}` 上没有可读的 `MeshFilter.m_Mesh` —— " +
                         "静态判据取不到网格(fail-closed;不要把它读成通过)");
                return errs;
            }

            bool haveCollider = false;
            foreach (Block b in Blocks(sceneText))
            {
                if (b.ClassId == 64 && RefEquals(b.Body, "m_GameObject", groundId))
                {
                    haveCollider = true;
                    if (ReadInt(b.Body, "m_Enabled") != 1)
                        errs.Add($"[地面覆盖] `{GroundObjectName}` 的 `MeshCollider` 未启用 —— 看得见、走不上去");
                    if (ReadInt(b.Body, "m_IsTrigger") == 1)
                        errs.Add($"[地面覆盖] `{GroundObjectName}` 的 `MeshCollider` 是 trigger —— 玩家会穿过去");
                    long colliderMesh = ReadFileId(b.Body, "m_Mesh");
                    if (colliderMesh != meshId)
                        errs.Add($"[地面覆盖] 碰撞体网格(fileID {colliderMesh})与渲染网格(fileID {meshId})不同 —— " +
                                 "看到的和踩到的不是同一个面");
                    break;
                }
            }
            if (!haveCollider)
                errs.Add($"[地面覆盖] `{GroundObjectName}` 上没有 `MeshCollider` —— 渲得出而走不上去(同「没有地面」)");

            // ── C3 水平(旋转须为单位四元数)──────────────────────────────────
            string transformBody = null;
            foreach (Block b in Blocks(sceneText))
            {
                if (b.ClassId == 4 && RefEquals(b.Body, "m_GameObject", groundId)) { transformBody = b.Body; break; }
            }
            if (transformBody == null)
            {
                errs.Add($"[地面覆盖] `{GroundObjectName}` 上没有可读的 `Transform` —— 静态判据取不到位姿");
                return errs;
            }

            Vec4 rot = ReadVec4(transformBody, "m_LocalRotation");
            if (Math.Abs(rot.X) > Eps || Math.Abs(rot.Y) > Eps || Math.Abs(rot.Z) > Eps ||
                Math.Abs(Math.Abs(rot.W) - 1f) > Eps)
            {
                errs.Add($"[地面覆盖] `{GroundObjectName}` 被旋转过(q = {rot.X},{rot.Y},{rot.Z},{rot.W}) —— " +
                         "倾斜 / 立起来的地面不构成可走平面(fail-closed:倾斜面上的包围盒须另立判据)");
                return errs;
            }

            // ── C5 内置网格已登记 ─────────────────────────────────────────────
            if (!BuiltinMeshes.TryGetValue(meshId, out GroundMeshShape shape))
            {
                errs.Add($"[地面覆盖] 网格 fileID {meshId} 不在本门的内置表里 —— " +
                         "未登记 ⇒ 静态判不出包围盒。是自定义网格请**显式**扩表,不要视作通过");
                return errs;
            }

            // ── C4 覆盖足量 + 齐平 ────────────────────────────────────────────
            Vec3 pos = ReadVec3(transformBody, "m_LocalPosition");
            Vec3 scale = ReadVec3(transformBody, "m_LocalScale");

            float half = latticeSizeMm / 1000f / 2f;
            float needMinX = -half, needMaxX = (worldSizeX - 1) * (latticeSizeMm / 1000f) + half;
            float needMinZ = -half, needMaxZ = (worldSizeZ - 1) * (latticeSizeMm / 1000f) + half;

            float haveHalfX = Math.Abs(shape.SizeX * scale.X) / 2f;
            float haveHalfZ = Math.Abs(shape.SizeZ * scale.Z) / 2f;
            float haveMinX = pos.X - haveHalfX, haveMaxX = pos.X + haveHalfX;
            float haveMinZ = pos.Z - haveHalfZ, haveMaxZ = pos.Z + haveHalfZ;

            if (haveMinX > needMinX + Eps || haveMaxX < needMaxX - Eps ||
                haveMinZ > needMinZ + Eps || haveMaxZ < needMaxZ - Eps)
            {
                errs.Add($"[地面覆盖] 地面覆盖不足:{shape.Name} × scale({scale.X},{scale.Z}) 覆盖 " +
                         $"x∈[{haveMinX:F2},{haveMaxX:F2}] z∈[{haveMinZ:F2},{haveMaxZ:F2}]," +
                         $"而世界格需 x∈[{needMinX:F2},{needMaxX:F2}] z∈[{needMinZ:F2},{needMaxZ:F2}] " +
                         $"({worldSizeX}×{worldSizeZ} 格 × {latticeSizeMm / 1000f:F3} m)—— " +
                         "未被覆盖的格子上玩家会坠落(TD-building-06)");
            }

            float surfaceY = pos.Y + shape.TopOffsetY * scale.Y;
            if (Math.Abs(surfaceY) > Eps)
            {
                errs.Add($"[地面覆盖] 地面顶面在 y={surfaceY:F3}(世界面 = 0)—— " +
                         "抬高 ⇒ 悬空 / 陷下 ⇒ 卡进地板");
            }

            return errs;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  场景 YAML 的最小解析(只认本门要用的四种块)
        // ══════════════════════════════════════════════════════════════════════

        private readonly struct Block
        {
            public readonly long ClassId;
            public readonly long Id;
            public readonly string Body;
            public Block(long classId, long id, string body) { ClassId = classId; Id = id; Body = body; }
        }

        /// <summary>按 `--- !u!<class> &<id>` 切块(不解析 YAML —— 只按块头切分)。</summary>
        private static IEnumerable<Block> Blocks(string text)
        {
            MatchCollection ms = Regex.Matches(text, @"^--- !u!(\d+) &(-?\d+).*$", RegexOptions.Multiline);
            for (int i = 0; i < ms.Count; i++)
            {
                int start = ms[i].Index + ms[i].Length;
                int end = (i + 1 < ms.Count) ? ms[i + 1].Index : text.Length;
                yield return new Block(long.Parse(ms[i].Groups[1].Value, CultureInfo.InvariantCulture),
                                       long.Parse(ms[i].Groups[2].Value, CultureInfo.InvariantCulture),
                                       text.Substring(start, end - start));
            }
        }

        private static long FindGameObjectId(string text, string name)
        {
            foreach (Block b in Blocks(text))
            {
                if (b.ClassId != 1) continue;
                if (ReadString(b.Body, "m_Name") == name)
                    return b.Id;
            }
            return 0;
        }

        private static bool RefEquals(string body, string key, long id)
            => ReadFileId(body, key) == id;

        private static long ReadFileId(string body, string key)
        {
            Match m = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @":\s*\{fileID:\s*(-?\d+)",
                                  RegexOptions.Multiline);
            return m.Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }

        private static int ReadInt(string body, string key)
        {
            Match m = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @":\s*(-?\d+)\s*$", RegexOptions.Multiline);
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }

        private static string ReadString(string body, string key)
        {
            Match m = Regex.Match(body, @"^\s*" + Regex.Escape(key) + @":\s*(.*)$", RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : null;
        }

        private readonly struct Vec3
        {
            public readonly float X, Y, Z;
            public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        private readonly struct Vec4
        {
            public readonly float X, Y, Z, W;
            public Vec4(float x, float y, float z, float w) { X = x; Y = y; Z = z; W = w; }
        }

        private static Vec3 ReadVec3(string body, string key)
        {
            Match m = Regex.Match(body,
                @"^\s*" + Regex.Escape(key) + @":\s*\{x:\s*(-?[\d.eE+]+),\s*y:\s*(-?[\d.eE+]+),\s*z:\s*(-?[\d.eE+]+)",
                RegexOptions.Multiline);
            if (!m.Success) return new Vec3(0f, 0f, 0f);
            return new Vec3(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value));
        }

        private static Vec4 ReadVec4(string body, string key)
        {
            Match m = Regex.Match(body,
                @"^\s*" + Regex.Escape(key) + @":\s*\{x:\s*(-?[\d.eE+]+),\s*y:\s*(-?[\d.eE+]+),\s*z:\s*(-?[\d.eE+]+),\s*w:\s*(-?[\d.eE+]+)",
                RegexOptions.Multiline);
            if (!m.Success) return new Vec4(0f, 0f, 0f, 1f);
            return new Vec4(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value), F(m.Groups[4].Value));
        }

        private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    }
}
