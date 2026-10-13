// TextCore FontAsset API 探测(EditMode · 2026-10-13)
//
// ▍为何要探测:UI Toolkit 的 `-unity-font-definition` 经 `FontDefinition.FromSDFFont(
//   UnityEngine.TextCore.Text.FontAsset)` 解析 ⇒ **需要 TextCore 系 FontAsset**。
//   而 SdfFontAssetTool 之前用 `TMP_FontAsset.CreateFontAsset` 生成的是 **TMP 系**
//   (TMP_FontAsset : TMP_Asset : ScriptableObject —— **不继承 TextCore FontAsset**)
//   ⇒ UI Toolkit 解析失败 → MissingAssetReference → 回退位图字体 → 糊。
//
// ▍本探针:反射列出 **TextCore.FontAsset** 的真实 API 面(CreateFontAsset / TryAddCharacters /
//   atlasPopulationMode / sourceFontFile …),据此重写生成器走 TextCore 路径。
//   全仓零 TextCore 先例 ⇒ 不凭记忆猜签名。
//
// 菜单:`大医精诚/Font/探测 TextCore FontAsset API`(手动,不进 CI)。

#if UNITY_EDITOR

namespace DaYiJingCheng.EditorTools.Font
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public static class TextCoreFontApiProbe
    {
        private const string OutPath = "Logs/textcore-font-api-probe.txt";

        [MenuItem("大医精诚/Font/探测 TextCore FontAsset API", false, 121)]
        public static void Probe()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# TextCore FontAsset API 探测({DateTime.Now:yyyy-MM-dd HH:mm:ss})");

            Type fa = FindType("UnityEngine.TextCore.Text.FontAsset");
            sb.AppendLine($"FontAsset type = {fa?.AssemblyQualifiedName ?? "(未找到)"}");
            if (fa == null) { Write(sb); return; }

            sb.AppendLine($"基类链:");
            for (var t = fa.BaseType; t != null && t != typeof(object); t = t.BaseType)
                sb.AppendLine($"  {t.FullName}");

            sb.AppendLine("\n== 静态 CreateFontAsset 重载 ==");
            foreach (var m in fa.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                  .Where(m => m.Name == "CreateFontAsset"))
                sb.AppendLine($"  {Sig(m)} : {m.ReturnType.Name}");

            sb.AppendLine("\n== 实例 TryAddCharacters / TryAddGlyphs 重载 ==");
            foreach (var m in fa.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(m => m.Name.StartsWith("TryAdd")))
                sb.AppendLine($"  {Sig(m)} : {m.ReturnType.Name}");

            sb.AppendLine("\n== 属性(atlas/source/population) ==");
            foreach (var p in fa.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                 .Where(p => p.Name.IndexOf("atlas", StringComparison.OrdinalIgnoreCase) >= 0
                                          || p.Name.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0
                                          || p.Name.IndexOf("population", StringComparison.OrdinalIgnoreCase) >= 0
                                          || p.Name.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0
                                          || p.Name.IndexOf("glyph", StringComparison.OrdinalIgnoreCase) >= 0
                                          || p.Name.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0
                                          || p.Name.IndexOf("face", StringComparison.OrdinalIgnoreCase) >= 0))
                sb.AppendLine($"  {p.PropertyType.Name} {p.Name} {{ {(p.CanRead ? "get;" : "")}{(p.CanWrite ? " set;" : "")} }}");

            // 枚举值
            foreach (var en in new[] { "UnityEngine.TextCore.LowLevel.GlyphRenderMode",
                                       "UnityEngine.TextCore.Text.AtlasPopulationMode" })
            {
                var t = FindType(en);
                sb.AppendLine($"\n{en} = {t?.FullName ?? "(未找到)"}");
                if (t != null)
                    foreach (var n in Enum.GetNames(t))
                        sb.AppendLine($"  {n} = {Convert.ToInt64(Enum.Parse(t, n))}");
            }

            Write(sb);
            Debug.Log($"[TextCoreFontApiProbe] 已落盘 {OutPath}\n{sb}");
        }

        private static string Sig(MethodInfo m) =>
            $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})";

        private static Type FindType(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(fullName); } catch { return null; } })
                .FirstOrDefault(t => t != null);

        private static void Write(StringBuilder sb)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), OutPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, sb.ToString());
        }
    }
}

#endif