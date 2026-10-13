// 空壳 FontAsset 验收探针(EditMode · 2026-10-13)
//
// 背景:`Editor.Tools.Font.SdfFontAssetTool` 生成的 `NotoSansCJKsc-Bold SDF.asset`
// 实测 3.6KB / `m_GlyphTable: []` / `m_Material: {fileID: 0}` ⇒ **空壳**:
// 有 FaceInfo(PointSize 32 + 家族名对),但零字形、零 atlas、零 material。
// 接线轮(替换 11 处 USS `-unity-font-definition`)之前必须先回答:
//   这个空壳**能不能用**?谁负责烤字形?
//
// 三条候选答案,逐条可证伪:
//   A. UI Toolkit 渲染时按需 populate(动态模式自烤)⇒ 空壳可用,直接接线;
//   B. 必须 Editor 期先 populate(官方 Creator 的行为)⇒ 空壳不可用,须补烤录步骤;
//   C. material 缺失使渲染必败 ⇒ 无论 A/B 都要先补 material。
//
// ⚠️ 诚实边界:batch 宿主无渲染帧(SPIKE-D 已实测)⇒ 本探针只做**静态/反射**判定
// (字形表 / material / atlas / population mode + 可用的 populate 方法面),
// 不判「真机画出什么」;像素腿归人工。

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.Unit.SkeuomorphicUI
{
    /// <summary>空壳 FontAsset 的结构验收(字形 / material / populate 面)。</summary>
    internal sealed class font_asset_shell_acceptance_test
    {
        private const string FontAssetGuid = "6bfef9a30546ce4618445ebd7e0be0f5";
        private const string SourceOtfGuid = "b9b707bf05564101912cbf0f946b649f";

        private static Assembly Tmp => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Unity.TextMeshPro");

        private static Type FaType => Tmp?.GetType("TMPro.TMP_FontAsset");

        // ──────────────────────────────────────────────────────────────────────
        // ① 子资产持久化验收(真验收点)
        // ──────────────────────────────────────────────────────────────────────
        // 背景修正(2026-10-13):首判把「零字形」判成失败是**误判**。
        // 包源码 `TMP_FontAsset.cs:583-591` 实证 Dynamic 模式**只登记 sourceFontFile**,
        // 字形靠 `TryAddCharacters`(public 实例方法,`:1281`)运行时按需烤录 —— 空字形表是
        // Dynamic 的**合法形态**,不是 bug。真 bug 是 `CreateAsset` 漏 `AddObjectToAsset`
        // ⇒ material/atlas 子资产没持久化(`m_Material: {fileID:0}`)。本判据据此重写。
        [Test]
        public void shell_a_generatedAsset_subAssetsPersisted()
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(FontAssetGuid);
            Assert.IsFalse(string.IsNullOrEmpty(path),
                $"FontAsset 应存在(GUID {FontAssetGuid})—— 生成器已跑过。");

            var type = FaType;
            Assert.IsNotNull(type, "TMP_FontAsset 类型应可达");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, type);
            Assert.IsNotNull(asset, $"应能加载 {path}");

            bool hasMaterial = HasMaterial(asset);
            int atlasCount = AtlasCount(asset);
            int populationMode = (int)GetProp(asset, "atlasPopulationMode");

            Debug.Log($"[SHELL-A] {System.IO.Path.GetFileName(path)}: " +
                      $"material={(hasMaterial ? "有" : "无")} atlasTextures={atlasCount} " +
                      $"populationMode={populationMode}");

            // 验收点 = 子资产已持久化(Dynamic 空字形表合法,不判 glyphCount)
            Assert.IsTrue(hasMaterial,
                "[SHELL-A] FontAsset.material 应已入库(Generate 须 AddObjectToAsset 挂子资产;" +
                "漏挂 ⇒ 运行时 UI Toolkit 取不到材质,字必糊)");
            Assert.IsTrue(atlasCount > 0,
                "[SHELL-A] FontAsset.atlasTextures 应至少 1 张(1×1 占位亦可,Dynamic 按需烤录)");
            Assert.AreEqual(1, populationMode, "[SHELL-A] populationMode 应为 Dynamic(=1)");
        }

        // ──────────────────────────────────────────────────────────────────────
        // ② populate 能力面:Dynamic 模式运行时按需烤录字形的**真实入口**
        // ──────────────────────────────────────────────────────────────────────
        // 包源码 `TMP_FontAsset.cs:1281` `TryAddCharacters(string characters, bool importMode,
        // bool ttcUniqueMapping)` 是 public 实例方法 —— UI Toolkit 渲染遇到未烤字形时走它。
        // 本测试确证该入口存在(若 Unity 版本改名/删除,本断言红 = 不可信 Dynamic 自烤,须改 Static 预烤)。
        [Test]
        public void shell_b_dynamicPopulateEntry_exists()
        {
            var type = FaType;
            Assert.IsNotNull(type, "TMP_FontAsset 类型应可达");

            var tryAdd = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "TryAddCharacters")
                .ToList();

            foreach (var m in tryAdd)
            {
                var ps = m.GetParameters();
                Debug.Log($"[SHELL-B] TMP_FontAsset.TryAddCharacters(" +
                          $"{string.Join(", ", ps.Select(p => p.ParameterType.Name))})");
            }

            Assert.IsTrue(tryAdd.Count > 0,
                "[SHELL-B] TMP_FontAsset 应有 TryAddCharacters(public 实例)—— " +
                "Dynamic 运行时按需烤录字形的入口;零命中 = 反射名全错或该版本不支持 Dynamic 自烤");
        }

        // ──────────────────────────────────────────────────────────────────────
        // ③ material 能否后补(Answer C 的对策面)
        // ──────────────────────────────────────────────────────────────────────
        [Test]
        public void shell_c_material_backfillable()
        {
            var type = FaType;
            var p = type?.GetProperty("material", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(p, "[SHELL-C] TMP_FontAsset.material 属性应存在");
            Assert.IsTrue(p.CanWrite, "[SHELL-C] material 应可写(后补对策的前提)");

            // 引擎内置 SSD shader 可达性(SPIKE-A 已知名,这里验 Find 可用)
            var shader = Shader.Find("Hidden/TextCore/Distance Field SSD");
            Assert.IsNotNull(shader, "[SHELL-C] 引擎内置 SSD shader 应可达(UI Toolkit 文字 shader)");

            Debug.Log($"[SHELL-C] material 可写 + SSD shader 可达 ⇒ " +
                      $"可 `new Material(shader)` 后补(不碰渲染路径)");
        }

        // ──────────────────────────────────────────────────────────────────────
        // ④ 接线门槛(本批次的**判据落点**):什么条件下才准替换 USS
        // ──────────────────────────────────────────────────────────────────────
        [Test]
        public void shell_d_wiringGate_declaration()
        {
            // 接线门槛 = 子资产已持久化(material + atlas 非 null)。
            // Dynamic 空字形表合法 ⇒ 不判字形数。门槛达标 ⇒ shell_a 自动绿 ⇒ 可接线。
            Debug.Log("[SHELL-D] 接线门槛(未达 ⇒ 禁替换 11 处 USS):" +
                      "material != null 且 atlasTextures.Length > 0。" +
                      "达标判定由 shell_a 实际执行;本测试只声明门槛语义。");
            Assert.Pass("[SHELL-D] 门槛语义已声明;达标与否由 shell_a 自动翻转");
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static object GetProp(object o, string name) =>
            o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(o);

        private static bool HasMaterial(object asset) => GetProp(asset, "material") != null;

        private static int AtlasCount(object asset)
        {
            var at = GetProp(asset, "atlasTextures");
            if (at is Array a) return a.Length;
            return -1;
        }
    }
}
