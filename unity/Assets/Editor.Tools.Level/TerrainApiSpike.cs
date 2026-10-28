// AC-0 spike —— Unity 6.3 LTS 上 `TerrainData` / `TerrainCollider` 的**行为实测**。
//
// 权威来源:
//   `production/epics/level-tool/story-002-world-geometry-and-terrain.md` **AC-0(BLOCKING 门)**
//   `docs/engine-reference/unity/VERSION.md` §Knowledge Gap Warning
//     (引擎 6.3 LTS 于 2025-12 发布,LLM 知识截止 2025-05 ⇒ Terrain API 属 **post-cutoff**)
//
// ═══════════════════════════════════════════════════════════════════════════
// 本文件**不是**生产代码 —— 它是一次勘察的可重跑载体
// ═══════════════════════════════════════════════════════════════════════════
// AC-0 的判据逐字要求「三条各有**实测输出**(日志 / 探针件),**不是文档引用**」。
// ⇒ 「我们读过文档、知道 GetHeight 返回归一化高度」**不算通过**;必须有本机 6.3 LTS 的实测串。
// 保留本文件而不是写完即删,是为了让证据件的每一条**可被第三方重跑**(本仓「数字须带可跑复算命令」)。
// 它**没有生产消费者**,这是设计:**唯一消费者 = 证据件里的那条命令**。
//
// 复算:
//   cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
//     -batchmode -nographics -projectPath "$PWD" -quit \
//     -executeMethod DaYiJingCheng.EditorTools.Level.TerrainApiSpike.RunTerrainApiSpike \
//     -logFile "$PWD/Logs/terrain-spike.log"
//   grep '^\[SPIKE-TERRAIN\]' Logs/terrain-spike.log
//
// ⚠️⚠️ **本探针用反射调用取样 API,不直接写死签名**(2026-10-29 首跑实测教训,原文不删):
//   首版按「`GetHeight(float, float)`」写,6.3 上**编译失败** ——
//   `error CS1503: cannot convert from 'float' to 'int'`(×16 处)。
//   那条编译错误本身就是 AC-0 的第一条实测结论:**该 API 的签名与 LLM 知识不符**。
//   若继续猜签名(改成 int、再猜 `GetSteepness` 是不是也吃 int),就会变成「猜 → 编译 → 再猜」,
//   而 AC-0 要的是**实测**,不是**猜对了**。
//   ⇒ 改为:先**枚举**该类型上 `GetHeight` / `GetInterpolatedHeight` / `GetSteepness` 的全部重载
//   并打进日志(这本身就是证据),再按参数类型分派调用。
//
// ⚠️ **本探针只回答它测的东西**。它**不**做「CharacterController 在斜坡上真的滑不滑」的物理仿真
//    (那要 PlayMode + Update 循环);② 的答案是**反射面 + 数值面**的,不是动力学面的。
//    该边界逐字写进证据件。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DaYiJingCheng.EditorTools.Level
{
    /// <summary>AC-0 的 Terrain API 行为探针(编辑期 batch 入口;无生产消费者)。</summary>
    public static class TerrainApiSpike
    {
        /// <summary>临时资产目录(探针结束即删;留在 `Assets/` 下是 `AssetDatabase` 的硬要求)。</summary>
        private const string TempFolder = "Assets/TerrainSpikeTemp";

        /// <summary>heightmap 分辨率(Unity 要求 `2^k + 1`;33 ⇒ 32 个格距)。</summary>
        private const int Res = 33;

        /// <summary>batch 入口:跑三项实测并把结构化结果写进日志。</summary>
        [MenuItem("大医精诚/关卡工具/spike: Terrain API 6.3 行为实测")]
        public static void RunTerrainApiSpike()
        {
            var log = new StringBuilder();
            log.AppendLine("[SPIKE-TERRAIN] begin");
            Emit(log, "unity_version", Application.unityVersion);
            Emit(log, "terrain_data_type", typeof(TerrainData).AssemblyQualifiedName);
            Emit(log, "terrain_collider_type", typeof(TerrainCollider).AssemblyQualifiedName);
            Emit(log, "is_editor", Application.isEditor.ToString());

            ProbeS_DeclaredSignatures(log);
            ProbeA_HeightPrecisionAndCoordinates(log);
            ProbeA2_CoordinateConvention(log);
            ProbeA3_HeightQuantum(log);
            ProbeB_SlopeLimitOwnership(log);
            ProbeC_EditModeAssetReadability(log);

            log.AppendLine("[SPIKE-TERRAIN] end");
            Debug.Log(log.ToString());
        }

        // ═══════════════════════════════════════════════════════════════════
        // S · 先枚举签名(这一节本身就是 AC-0 的产出之一)
        // ═══════════════════════════════════════════════════════════════════

        private static void ProbeS_DeclaredSignatures(StringBuilder log)
        {
            foreach (string name in new[] { "GetHeight", "GetInterpolatedHeight", "GetSteepness" })
            {
                MethodInfo[] overloads = typeof(TerrainData).GetMethods(
                    BindingFlags.Public | BindingFlags.Instance);
                var sigs = new List<string>();
                foreach (MethodInfo m in overloads)
                {
                    if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                    var ps = new List<string>();
                    foreach (ParameterInfo p in m.GetParameters())
                        ps.Add(p.ParameterType.Name + " " + p.Name);
                    sigs.Add(m.ReturnType.Name + " " + name + "(" + string.Join(", ", ps) + ")");
                }
                Emit(log, "S." + name + "_overloadCount", sigs.Count.ToString());
                Emit(log, "S." + name + "_signatures", sigs.Count == 0 ? "<none>" : string.Join(" ; ", sigs));
            }

            // 属性面(slopeLimit 是否在 TerrainData / TerrainCollider 上)
            Emit(log, "S.terrainData_propertyNames", string.Join("|", PropertyNames(typeof(TerrainData))));
            Emit(log, "S.terrainCollider_propertyNames", string.Join("|", PropertyNames(typeof(TerrainCollider))));
        }

        // ═══════════════════════════════════════════════════════════════════
        // ① `TerrainData` 高度的返回精度与坐标系(世界格 ↔ 归一化)
        // ═══════════════════════════════════════════════════════════════════

        private static void ProbeA_HeightPrecisionAndCoordinates(StringBuilder log)
        {
            TerrainData td = BuildRampTerrain(out float[,] authored);
            Vector3 size = td.size;
            int res = td.heightmapResolution;
            Emit(log, "A.heightmapResolution", res.ToString());
            Emit(log, "A.size", F(size.x) + "x" + F(size.y) + "x" + F(size.z));

            // 取样点三组:
            //   网格首/尾角 = 索引 (0,0) 与 (res-1,res-1),归一化 0 与 1,世界 0 m 与 size.x/z;
            //   网格中点   = 索引 (res/2,res/2),归一化 0.5,世界 (size/2)。
            float n00 = (float)InvokeSample(td, "GetHeight", 0f, 0f);
            float n11 = (float)InvokeSample(td, "GetHeight", 1f, 1f);
            float nMid = (float)InvokeSample(td, "GetHeight", 0.5f, 0.5f);
            Emit(log, "A.getHeight_0_0", F(n00));
            Emit(log, "A.getHeight_1_1", F(n11));
            Emit(log, "A.getHeight_0p5_0p5", F(nMid));

            Emit(log, "A.authored_h[0,0]", F(authored[0, 0]));
            Emit(log, "A.authored_h[res/2,res/2]", F(authored[res / 2, res / 2]));
            Emit(log, "A.authored_h[res-1,res-1]", F(authored[res - 1, res - 1]));

            // 判据 1:归一化面 —— (0,0)/(1,1) 是否复现作者写入的角值。
            Emit(log, "A.corner00_matchesAuthoredNormalized",
                 (Mathf.Abs(n00 - authored[0, 0]) < 1e-6f).ToString());
            Emit(log, "A.corner11_matchesAuthoredNormalized",
                 (Mathf.Abs(n11 - authored[res - 1, res - 1]) < 1e-6f).ToString());

            // 判据 2:返回量是不是**归一化**(值域 [0,1])—— 斜坡顶作者写 1.0,size.y = 10 m。
            //   若返回米,顶点会是 10 ⇒ max > 1。
            Emit(log, "A.observed_max", F(Mathf.Max(n00, Mathf.Max(n11, nMid))));
            Emit(log, "A.returns_normalized_in_0_1",
                 (n11 >= 0f && n11 <= 1f && Mathf.Abs(n11 - 1f) < 1e-4f).ToString());

            // 判据 3:`GetInterpolatedHeight` 是否返回**米**,且 == 归一化 × size.y。
            float i00 = (float)InvokeSample(td, "GetInterpolatedHeight", 0f, 0f);
            float iMid = (float)InvokeSample(td, "GetInterpolatedHeight", size.x * 0.5f, size.z * 0.5f);
            Emit(log, "A.interpHeight_world00", F(i00));
            Emit(log, "A.interpHeight_worldMid", F(iMid));
            Emit(log, "A.interp_equals_normalized_x_sizeY",
                 (Mathf.Abs(iMid - nMid * size.y) < 1e-3f).ToString());

            // 判据 4:最小可分辨高度差 —— 作者侧写 1e-6 的差,读回是否仍可分。
            var probe = new TerrainData();
            probe.heightmapResolution = Res;
            probe.size = new Vector3(16f, 10f, 16f);
            var distinct = new float[Res, Res];
            distinct[0, 0] = 0.25f;
            distinct[0, 1] = 0.250001f;
            probe.SetHeights(0, 0, distinct);

            // 相邻两个**网格样本**之间取一个,验证 1e-6 差分不被 float32 吃掉。
            float pa = (float)InvokeSample(probe, "GetHeight", 0f, 0f);
            float pb = (float)InvokeSample(probe, "GetHeight", 1f / (Res - 1), 0f);
            Emit(log, "A.ulp_probe_a", F(pa));
            Emit(log, "A.ulp_probe_b", F(pb));
            Emit(log, "A.ulp_probe_delta", F(pb - pa));
            Emit(log, "A.ulp_probe_distinguishable", (pa != pb).ToString());

            UnityEngine.Object.DestroyImmediate(td);
            UnityEngine.Object.DestroyImmediate(probe);
        }

        // ═══════════════════════════════════════════════════════════════════
        // A2 · **坐标系约定**的决定性测量
        // ═══════════════════════════════════════════════════════════════════
        // A 那一轮的取样点在「归一化」与「世界米」两种读法下**恰好不冲突**(0 / 中点 / 满量程),
        // 所以它**不能**判定 `GetInterpolatedHeight` 吃的是哪一套。本节的取样点刻意选在
        // **两种读法给出不同答案**的位置上 —— 那才叫判据。

        private static void ProbeA2_CoordinateConvention(StringBuilder log)
        {
            // 纯 x 向斜坡:height[z,x] = x/(res-1) ⇒ 归一化高度在 x 方向线性 0→1。
            // 世界尺寸 16×10×16 ⇒ 世界高度 = 归一化 × 10 m,x 向世界跨度 16 m。
            var td = new TerrainData();
            td.heightmapResolution = Res;
            td.size = new Vector3(16f, 10f, 16f);
            var ramp = new float[Res, Res];
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                    ramp[z, x] = (float)x / (Res - 1);
            td.SetHeights(0, 0, ramp);

            // 决定性取样:x = 0.25。
            //   若吃归一化 ⇒ 归一化高度 0.25 ⇒ **2.5 m**
            //   若吃世界米 ⇒ 世界 x = 0.25 m,即归一化 0.25/16 = 0.015625 ⇒ **0.15625 m**
            float atQuarter = (float)InvokeSample(td, "GetInterpolatedHeight", 0.25f, 0.5f);
            Emit(log, "A2.interpHeight_0p25_0p5", F(atQuarter));
            Emit(log, "A2.interp_takes_normalized",
                 (Mathf.Abs(atQuarter - 2.5f) < 0.01f).ToString());
            Emit(log, "A2.interp_takes_worldMeters",
                 (Mathf.Abs(atQuarter - 0.15625f) < 0.01f).ToString());

            // `GetHeight(int,int)` 的索引面:以**网格索引**直接取首/中/末三个样本。
            // 期望(若「索引 + 米」):0 / 5 / 10。
            Emit(log, "A2.getHeightIndex_0", F((float)InvokeRaw(td, "GetHeight", 0, 0)));
            Emit(log, "A2.getHeightIndex_mid", F((float)InvokeRaw(td, "GetHeight", Res / 2, Res / 2)));
            Emit(log, "A2.getHeightIndex_last", F((float)InvokeRaw(td, "GetHeight", Res - 1, Res - 1)));

            // 越过末索引会不会被钳(而不是越界异常)。
            string clampNote;
            try
            {
                float over = (float)InvokeRaw(td, "GetHeight", Res + 10, 0);
                clampNote = "no_throw:" + F(over);
            }
            catch (Exception ex)
            {
                clampNote = "throws:" + ex.GetType().Name;
            }
            Emit(log, "A2.getHeightIndex_overlast", clampNote);
            Emit(log, "A2.heightmapWidth", td.heightmapWidth.ToString());
            Emit(log, "A2.heightmapHeight", td.heightmapHeight.ToString());

            UnityEngine.Object.DestroyImmediate(td);

            // `GetSteepness` 的坐标系 —— 用一个**坡度随 x 变化**的面(h = x²),
            // 使「归一化」与「世界米」在同一个数值上给出不同读数。
            var td2 = new TerrainData();
            td2.heightmapResolution = Res;
            td2.size = new Vector3(16f, 10f, 16f);
            var quad = new float[Res, Res];
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                {
                    float n = (float)x / (Res - 1);
                    quad[z, x] = n * n;
                }
            td2.SetHeights(0, 0, quad);

            // 归一化 0.25 ⇒ 期望与「世界 4 m(= 归一化 0.25)」相同;**若吃归一化**,
            // 直接传 (4,4) 会被钳到 1 ⇒ 与 (1,1) 相同。
            float sQuarter = (float)InvokeSample(td2, "GetSteepness", 0.25f, 0.5f);
            float sFour = (float)InvokeSample(td2, "GetSteepness", 4f, 4f);
            float sOne = (float)InvokeSample(td2, "GetSteepness", 1f, 1f);
            float sHalf = (float)InvokeSample(td2, "GetSteepness", 0.5f, 0.5f);
            Emit(log, "A2.steepness_norm0p25", F(sQuarter));
            Emit(log, "A2.steepness_literal4", F(sFour));
            Emit(log, "A2.steepness_norm1p0", F(sOne));
            Emit(log, "A2.steepness_norm0p5", F(sHalf));
            Emit(log, "A2.steepness_variesWithPosition", (sQuarter != sHalf).ToString());
            Emit(log, "A2.steepness_literal4_is_clampedTo1", (sFour == sOne).ToString());

            UnityEngine.Object.DestroyImmediate(td2);
        }

        // ═══════════════════════════════════════════════════════════════════
        // A3 · **高度量化步长**(heightmap 是不是 float32?)
        // ═══════════════════════════════════════════════════════════════════

        private static void ProbeA3_HeightQuantum(StringBuilder log)
        {
            var td = new TerrainData();
            td.heightmapResolution = Res;
            td.size = new Vector3(16f, 10f, 16f);

            // 直接写 heightmap 数组的**归一化**值,再按索引读回**米**,算偏差。
            var h = new float[Res, Res];
            h[0, 0] = 0.25f;
            td.SetHeights(0, 0, h);
            float readMeters = (float)InvokeRaw(td, "GetHeight", 0, 0);
            float expectedMeters = 0.25f * td.size.y;
            float deviation = readMeters - expectedMeters;
            Emit(log, "A3.authored_normalized", F(0.25f));
            Emit(log, "A3.expected_meters", F(expectedMeters));
            Emit(log, "A3.read_meters", F(readMeters));
            Emit(log, "A3.deviation_meters", F(deviation));
            // 16 位量化的理论步长 = size.y / 65535;32 位 float 的 ULP ≈ 6e-8。
            Emit(log, "A3.sizeY_over_65535", F(td.size.y / 65535f));
            Emit(log, "A3.deviation_over_quantum", F(deviation / (td.size.y / 65535f)));
            Emit(log, "A3.is_16bit_quantized",
                 (Mathf.Abs(Mathf.Abs(deviation) - td.size.y / 65535f) < 1e-5f).ToString());

            UnityEngine.Object.DestroyImmediate(td);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ② `TerrainCollider` 与 `TerrainData` 的可走性关系(`slopeLimit` 在哪一侧生效)
        // ═══════════════════════════════════════════════════════════════════

        private static void ProbeB_SlopeLimitOwnership(StringBuilder log)
        {
            Emit(log, "B.terrain_has_slopeLimit", HasProperty(typeof(Terrain), "slopeLimit").ToString());
            Emit(log, "B.terrainCollider_has_slopeLimit", HasProperty(typeof(TerrainCollider), "slopeLimit").ToString());
            Emit(log, "B.terrainData_has_slopeLimit", HasProperty(typeof(TerrainData), "slopeLimit").ToString());
            Emit(log, "B.characterController_has_slopeLimit", HasProperty(typeof(CharacterController), "slopeLimit").ToString());
            Emit(log, "B.terrainData_has_getSteepness", HasMethod(typeof(TerrainData), "GetSteepness").ToString());

            // `GetSteepness` 的**单位**实测:造一个 45° 斜面(水平 10 m 升 10 m)。
            var td = new TerrainData();
            td.heightmapResolution = Res;
            td.size = new Vector3(10f, 10f, 10f);
            var ramp = new float[Res, Res];
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                    ramp[z, x] = (float)x / (Res - 1);   // 归一化 0→1 ⇒ 世界高度 0→10 m
            td.SetHeights(0, 0, ramp);

            float steepMid = (float)InvokeSample(td, "GetSteepness", 0.5f, 0.5f);
            float steepLow = (float)InvokeSample(td, "GetSteepness", 0.1f, 0.5f);
            Emit(log, "B.getSteepness_0p5_0p5", F(steepMid));
            Emit(log, "B.getSteepness_0p1_0p5", F(steepLow));
            Emit(log, "B.steepness_is_degrees_45", (Mathf.Abs(steepMid - 45f) < 1f).ToString());

            // CharacterController.slopeLimit / stepOffset 的**实际取值与量纲**。
            var go = new GameObject("SpikeCC");
            try
            {
                CharacterController cc = go.AddComponent<CharacterController>();
                Emit(log, "B.cc_slopeLimit_default", F(cc.slopeLimit));
                Emit(log, "B.cc_stepOffset_default", F(cc.stepOffset));
                Emit(log, "B.cc_slopeLimit_in_degree_scale",
                     (cc.slopeLimit > 0f && cc.slopeLimit <= 90f).ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            UnityEngine.Object.DestroyImmediate(td);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ③ `TerrainData` 能否在**编辑期**被关卡工具读取(不依赖运行期)
        // ═══════════════════════════════════════════════════════════════════

        private static void ProbeC_EditModeAssetReadability(StringBuilder log)
        {
            Emit(log, "C.isPlaying", Application.isPlaying.ToString());

            // (a) 内存实例:编辑期可构造 + 可回读。
            TerrainData memory = BuildRampTerrain(out _);
            float memoryRead = (float)InvokeSample(memory, "GetHeight", 0.5f, 0.5f);
            Emit(log, "C.inmemory_getHeight_mid", F(memoryRead));
            UnityEngine.Object.DestroyImmediate(memory);

            // (b) 走 **AssetDatabase** 往返:建资产 → 存 → 重新 Load → 从新引用读高度 → 删。
            //     这一条才是「编辑期工具能不能**从盘上的美术地形资产**取数」的实测面。
            bool created = false, loadedOk = false, deleted = false;
            float fromAsset = float.NaN;
            int assetResolution = -1;
            Vector3 assetSize = default;
            try
            {
                if (!AssetDatabase.IsValidFolder(TempFolder))
                    created = AssetDatabase.CreateFolder("Assets", "TerrainSpikeTemp") != string.Empty;

                TerrainData authored = BuildRampTerrain(out _);
                string assetPath = TempFolder + "/SpikeTerrain.asset";
                AssetDatabase.CreateAsset(authored, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var reloaded = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
                loadedOk = reloaded != null;
                if (loadedOk)
                {
                    fromAsset = (float)InvokeSample(reloaded, "GetHeight", 0.5f, 0.5f);
                    assetResolution = reloaded.heightmapResolution;
                    assetSize = reloaded.size;
                }
            }
            catch (Exception ex)
            {
                Emit(log, "C.asset_roundtrip_exception", ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(TempFolder))
                {
                    deleted = AssetDatabase.DeleteAsset(TempFolder);
                    AssetDatabase.Refresh();
                }
                else
                {
                    deleted = true;   // 目录从未建起 = 无需删
                }
            }

            Emit(log, "C.folder_created", created.ToString());
            Emit(log, "C.asset_load_ok", loadedOk.ToString());
            Emit(log, "C.asset_getHeight_mid", F(fromAsset));
            Emit(log, "C.asset_heightmapResolution", assetResolution.ToString());
            Emit(log, "C.asset_size", F(assetSize.x) + "x" + F(assetSize.y) + "x" + F(assetSize.z));
            Emit(log, "C.temp_folder_deleted", deleted.ToString());
            Emit(log, "C.temp_folder_gone_after", (!AssetDatabase.IsValidFolder(TempFolder)).ToString());
        }

        // ═══════════════════════════════════════════════════════════════════
        // 共用
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 按**归一化坐标**语义取样的统一入口:`(0,0)` = 网格首角,`(1,1)` = 网格末角。
        /// <para>⚠️ 实现按**实测到的重载**分派,不假设签名(见文件头 2026-10-29 教训):
        /// ① 只吃 `float,float` ⇒ 直接传归一化值;
        /// ② 吃 `int,int` ⇒ 归一化值 **× (res-1) 四舍五入** 成**网格索引**;</para>
        /// </summary>
        private static float InvokeSample(TerrainData td, string method, float nx, float nz)
        {
            MethodInfo mi = FindOverload(typeof(TerrainData), method, 2);
            if (mi == null)
                return float.NaN;

            ParameterInfo[] ps = mi.GetParameters();
            object[] args;
            if (ps[0].ParameterType == typeof(float) || ps[0].ParameterType == typeof(double))
            {
                args = new object[] { nx, nz };
            }
            else
            {
                int last = td.heightmapResolution - 1;
                args = new object[]
                {
                    Mathf.Clamp(Mathf.RoundToInt(nx * last), 0, last),
                    Mathf.Clamp(Mathf.RoundToInt(nz * last), 0, last),
                };
            }

            object result = mi.Invoke(td, args);
            return result is float f ? f : float.NaN;
        }

        /// <summary>**原样**调用(不做任何坐标换算 —— 用于「索引面」的实测:调用方给出什么就传什么)。</summary>
        private static float InvokeRaw(TerrainData td, string method, params object[] args)
        {
            MethodInfo mi = FindOverload(typeof(TerrainData), method, args.Length);
            if (mi == null) return float.NaN;
            object result = mi.Invoke(td, args);
            return result is float f ? f : float.NaN;
        }

        /// <summary>按参数个数找唯一重载(找不到 / 多于一个 ⇒ null,由调用方记 NaN)。</summary>
        private static MethodInfo FindOverload(Type t, string name, int paramCount)
        {
            MethodInfo found = null;
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                if (m.GetParameters().Length != paramCount) continue;
                if (found != null) return null;   // 二义 ⇒ 不猜
                found = m;
            }
            return found;
        }

        /// <summary>造一块已知斜坡:height[z, x] = (x + z) / (2(Res-1)),即对角方向的 0→1 线性面。</summary>
        private static TerrainData BuildRampTerrain(out float[,] authored)
        {
            var td = new TerrainData();
            td.heightmapResolution = Res;
            td.size = new Vector3(16f, 10f, 16f);

            authored = new float[Res, Res];
            for (int z = 0; z < Res; z++)
                for (int x = 0; x < Res; x++)
                    authored[z, x] = (float)(x + z) / (2f * (Res - 1));

            td.SetHeights(0, 0, authored);
            return td;
        }

        private static string[] PropertyNames(Type t)
        {
            var names = new List<string>();
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                names.Add(p.Name);
            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static bool HasProperty(Type t, string name)
        {
            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (string.Equals(p.Name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool HasMethod(Type t, string name)
        {
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                if (string.Equals(m.Name, name, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>区域性无关的浮点文本(`R` = 往返可精确解析;禁本地化小数点)。</summary>
        private static string F(float v)
            => float.IsNaN(v) ? "<NaN>" : v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>写一行结构化结果(`key=value`,前缀固定,便于 grep)。</summary>
        private static void Emit(StringBuilder log, string key, string value)
            => log.Append("[SPIKE-TERRAIN] ").Append(key).Append('=').Append(value).Append('\n');
    }
}
