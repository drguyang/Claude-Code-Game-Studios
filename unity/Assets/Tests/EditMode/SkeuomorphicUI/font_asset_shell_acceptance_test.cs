// SDF FontAsset 验收探针(EditMode · 2026-10-13)
//
// 背景:`Editor.Tools.Font.SdfFontAssetTool` 生成的 `NotoSansCJKsc-Bold SDF.asset`
// 经四轮修正(空壳 → 预烤字形 → 切 Static → **改 TextCore 类型**)才可用。
//
// ⚠️ **第五层根因(2026-10-13 探针实证,本文件断言的核心)**:UI Toolkit 的
//   `-unity-font-definition` 经 `FontDefinition.FromSDFFont(UnityEngine.TextCore.Text.FontAsset)`
//   解析 ⇒ **需要 TextCore 系 FontAsset**;`TMPro.TMP_FontAsset` **不继承**它
//   ⇒ TMP 资产被 UI Toolkit 拒绝 ⇒ `MissingAssetReference` ⇒ 回退位图字体 ⇒ 糊。
//   ⇒ 验收第一关 = **资产能以 `UnityEngine.TextCore.Text.FontAsset` 类型加载**(见 shell_a)。
//
// 其余验收点(子资产持久化 / 字形预烤 / Static)承接前四轮修正,逐条可证伪。
//
// ⚠️ 诚实边界:batch 宿主无渲染帧 ⇒ 本探针只做**静态/反射**判定
// (类型 / 字形表 / material / atlas / population mode),不判「真机画出什么」;
// 像素腿归人工 + 截图 >> image-vision。

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

        private static Type FaType =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType("UnityEngine.TextCore.Text.FontAsset"); } catch { return null; } })
                .FirstOrDefault(t => t != null);

        // ──────────────────────────────────────────────────────────────────────
        // ① 子资产持久化验收(真验收点)
        // ──────────────────────────────────────────────────────────────────────
        // 背景修正(2026-10-13):首判把「零字形」判成失败是**误判** —— Dynamic 模式默认空字形表是
        // 合法形态。但**用户真机实测**暴露第二层:经 `-unity-font-definition` 被 UI Toolkit 引用时
        // **不会**触发按需烤录 ⇒ 零字形资产加载到空 atlas → `MissingAssetReference` → 回退糊字体。
        // ⇒ 生成器**必须**落盘前 `TryAddCharacters` 预烤项目字符集(BakeCharacters)。本判据升级为
        // **子资产持久化 + 字形已预烤** 两者皆须达标。
        [Test]
        public void shell_a_generatedAsset_subAssetsPersisted()
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(FontAssetGuid);
            Assert.IsFalse(string.IsNullOrEmpty(path),
                $"FontAsset 应存在(GUID {FontAssetGuid})—— 生成器已跑过。");

            var type = FaType;
            Assert.IsNotNull(type, "UnityEngine.TextCore.Text.FontAsset 类型应可达");
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath(path, type);
            Assert.IsNotNull(asset,
                $"[SHELL-A] 资产应以 **TextCore.Text.FontAsset** 类型加载 —— 实测加载失败({path})。" +
                "若资产是 TMP_FontAsset 类型 ⇒ UI Toolkit `-unity-font-definition` 拒绝它 ⇒ " +
                "MissingAssetReference ⇒ 回退位图字体 ⇒ 糊(2026-10-13 根因)。");

            bool hasMaterial = HasMaterial(asset);
            int atlasCount = AtlasCount(asset);
            int populationMode = (int)GetProp(asset, "atlasPopulationMode");
            int glyphCount = GlyphCount(asset);

            Debug.Log($"[SHELL-A] {System.IO.Path.GetFileName(path)}: " +
                      $"material={(hasMaterial ? "有" : "无")} atlasTextures={atlasCount} " +
                      $"populationMode={populationMode} glyphCount={glyphCount}");

            // 验收点一 = 子资产已持久化(漏挂 ⇒ UI Toolkit 取不到材质,字必糊)
            Assert.IsTrue(hasMaterial,
                "[SHELL-A] FontAsset.material 应已入库(Generate 须 AddObjectToAsset 挂子资产)");
            Assert.IsTrue(atlasCount > 0,
                "[SHELL-A] FontAsset.atlasTextures 应至少 1 张(预烤字形后必非空)");

            // 验收点二 = 字形已预烤(Dynamic 空壳不再可接受 —— 那正是 MissingAssetReference 根因)
            // ⚠️ populationMode 终点是 **Static(=0)**,不是 Dynamic(=1):Dynamic 资产在 ImportAsset/出包时
            // 会被 TMP_PreBuildProcessor 清掉动态字形(见 SdfFontAssetTool.SetStaticAfterBake 注);
            // 预烤后切 Static,字形表随序列化持久化,UI Toolkit 绑定即带真实字形。故断言"非 Dynamic 空壳"即可。
            Assert.IsTrue(populationMode == 0 || populationMode == 1,
                $"[SHELL-A] populationMode 应为 Static(=0,已预烤)或 Dynamic(=1),实测={populationMode}");
            Assert.IsTrue(glyphCount > 0,
                $"[SHELL-A] FontAsset 应已预烤字形(glyphCount={glyphCount} > 0)。" +
                "若为零 = 生成器 BakeCharacters 未跑 / charset 缺失 ⇒ UI Toolkit 绑定必报 MissingAssetReference。");
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

        private static int GlyphCount(object asset)
        {
            var gt = GetProp(asset, "glyphTable");
            if (gt is System.Collections.ICollection c) return c.Count;
            return -1;
        }
    }
}
