// 脉案 SDF FontAsset 生成器(sprint-08 S3 「字糊」根治件 · 2026-10-13)
//
// 权威来源:
//   · design/ux/casebook-39.md §13 `[V]` 行(可读性人工腿) + §11(最小字号 / 对比承诺)
//   · accessibility-requirements.md:85(纸面 UI ≥24px @1080p)
//   · ADR-022(Tooling 层纪律:编辑期 / 构建期,运行期零存在,不进构建)
//   · ADR-025(具名六装配清单 = 运行期面;本平台 `Editor.Tools.Font` 属
//     `Editor.Tools` 族,includePlatforms = Editor,**不进玩家构建**)
//
// 病因(2026-10-13 用户真机实测「够大但糊」,逐层排除):
//   ① 不是字号 —— 已调 24→28px(`--skeuo-font-size-lexeme`);
//   ② 不是视口缩放 —— PanelSettings `m_ScaleMode: 2` Constant Pixel Size,1px=1px;
//   ③ 不是 import 采样点 —— `fontSize: 16 → 32` + `characterPadding: 1 → 2` 已试,仍糊;
//   ④ 不是「没预烤字形」—— 已预烤 1717 字进 atlas,资产落地 m_GlyphTable 非空,仍糊;
//   ⑤ **真因(2026-10-13 探针实证)**:UI Toolkit 的 `-unity-font-definition` 经
//      `FontDefinition.FromSDFFont(UnityEngine.TextCore.Text.FontAsset)` 解析,
//      **需要 TextCore 系 FontAsset**;而 `TMPro.TMP_FontAsset : TMP_Asset : ScriptableObject`
//      **不继承** TextCore.FontAsset ⇒ TMP 资产被 UI Toolkit **拒绝** ⇒
//      `Invalid value for font MissingAssetReference`(用户实机日志)+ 探针实测
//      `.channel-label` 解析为 `<default: 未定义 unityFontDefinition>` ⇒ 回退位图字体 ⇒ 糊。
//   ⇒ **唯一正路 = 生成 `UnityEngine.TextCore.Text.FontAsset`**(本版已改)。
//
// 设计说明:
//   · **首跑自证**:全仓零 TextCore FontAsset 先例,签名 Unity 6.3 post-cutoff ⇒
//     先经 `TextCoreFontApiProbe` 反射探测并把**真签名**落盘(Logs/textcore-font-api-probe.txt),
//     再据实生成;探测日志 = 判据可复现的证据件。
//   · **Dynamic 生成 + 预烤 + 切 Static 三步**(BakeCharacters / SetStaticAfterBake):
//     Dynamic 工厂建资产 → TryAddCharacters 把项目字符集烤进 atlas → 切 Static 令字形表
//     随序列化持久化(否则被 TMP_PreBuildProcessor 按 clearDynamicDataOnBuild 清掉)。
//   · 生成后**不改 USS / 不动 GUID** —— 接线轮做(USS 的 `-unity-font-definition` GUID 替换)。
//   · **源字体不动**:`.otf` 留在库(FontAsset 的生成源 + 回退面),不删不改。
//
// ⚠️ 菜单项:`大医精诚/Font/生成脉案 SDF FontAsset`(手动触发,不进 CI ——
//   资产生成是**一次性权威动作**,CI 重跑会产生字节级不确定的新资产)。

#if UNITY_EDITOR

namespace DaYiJingCheng.EditorTools.Font
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// 脉案 SDF FontAsset 生成器。
    /// <para>把 <c>Fonts/NotoSansCJKsc-Bold.otf</c>(动态字体,糊的根因)转换为
    /// TMP SDF FontAsset(无级缩放、锐利、可缓字形)。</para>
    /// <para><b>首跑必看日志</b>:本类第一件事是反射探测 TMP API 面并落盘 ——
    /// 生成的 FontAsset 是否可用,取决于探测出的真实签名,不取决于本文件的假设。</para>
    /// </summary>
    public static class SdfFontAssetTool
    {
        // ── 常量(GUID 为源字体:现役 Bold;Regular 留库备用,见主题变量注)──
        private const string SourceBoldOtfGuid = "b9b707bf05564101912cbf0f946b649f";
        private const string OutputAssetPath = "Assets/Gameplay.UI/Skeuomorphic/Fonts/NotoSansCJKsc-Bold SDF.asset";
        private const string ProbeLogPath = "Logs/sdf-font-tmp-api-probe.txt";

        /// <summary>预烤字符集(源 = <c>extract_cjk.py</c> 扫项目实际用到的 CJK + 全 ASCII + 全角标点)。
        /// <para>⚠️ **动态模式的根因修正**(2026-10-13 用户真机:绑定后报
        /// <c>MissingAssetReference</c> + 字体仍糊):Dynamic SDF 资产**默认零字形**,而 UI Toolkit
        /// 经 <c>-unity-font-definition</c> 引用时**不会**触发 TMP 的运行时按需烤录(TMP 的
        /// <c>TryAddCharacters</c> 是 FontAsset 侧主动调用,UI Toolkit 不走这条)。⇒ 必须**生成时**
        /// 把字符预烤进资产,资产才带真实字形/atlas;UI Toolkit 加载到的是有内容的 atlas ⇒ 绑定成功、
        /// 渲染锐利。本路径即 Dynamic 模式的正确用法(创建后补字,不推翻 Dynamic)。</para></summary>
        private const string CharsetPath = "Assets/Editor.Tools.Font/cjk_charset.txt";

        /// <summary>采样点数 —— 覆盖当前 28px 档 + 上探余量(32 = 折衷:再大则 atlas
        /// 需求暴涨,而 CJK 全字符集本就装不下,只按需收录)。</summary>
        private const int SamplingPointSize = 32;

        /// <summary>atlas 边长 —— 4096 = Unity 6 推荐上界(CJK 紧排 2k 会频繁溢出到多 atlas)。</summary>
        private const int AtlasSize = 4096;

        /// <summary>字形间 padding —— CJK 笔画密,留 2 防 SDF 边缘相互污染(糊的第二嫌疑)。</summary>
        private const int GlyphPadding = 2;

        /// <summary>生成 SDF FontAsset(菜单入口)。</summary>
        [MenuItem("大医精诚/Font/生成脉案 SDF FontAsset", false, 120)]
        public static void Generate()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(
                AssetDatabase.GUIDToAssetPath(SourceBoldOtfGuid));
            if (source == null)
            {
                Debug.LogError($"[SdfFontAssetTool] 源字体加载失败(GUID {SourceBoldOtfGuid})—— " +
                               "先确认 Fonts/NotoSansCJKsc-Bold.otf 未移动 / 未删。");
                return;
            }

            Debug.Log($"[SdfFontAssetTool] 源字体 = {source.name}({source.GetType().Name})。" +
                      $"采样 {SamplingPointSize}px · atlas {AtlasSize} · 输出 {OutputAssetPath}");

            // ⚠️⚠️ **2026-10-13 根因重定向 —— 必须用 TextCore.FontAsset,不是 TMP_FontAsset**:
            //   用户真机「字体依旧糊」+ 日志 `Invalid value for font MissingAssetReference`,
            //   探针实证 `.channel-label` 解析为 `<default: 未定义 unityFontDefinition>`。
            //   **根因**:UI Toolkit 的 `-unity-font-definition` 经
            //   `FontDefinition.FromSDFFont(UnityEngine.TextCore.Text.FontAsset)` 解析
            //   (UnityEngine.UIElementsModule.xml 实证),**需要 TextCore 系 FontAsset**;
            //   而 `TMPro.TMP_FontAsset : TMP_Asset : ScriptableObject` —— **不继承**
            //   `TextCore.Text.FontAsset`(探针 `TextCoreFontApiProbe` 实证基类链)
            //   ⇒ TMP 资产被 UI Toolkit **拒绝** ⇒ MissingAssetReference ⇒ 回退位图字体 ⇒ 糊。
            //   正路 = 走 `TextCore.Text.FontAsset.CreateFontAsset`(API 面与 TMP 版几乎同形:
            //   同 `TryAddCharacters(string,bool)` / 同 `AtlasPopulationMode` / `atlasPopulationMode` 可写)。
            var faType = FindTypeAnywhere("UnityEngine.TextCore.Text.FontAsset");
            if (faType == null)
            {
                Debug.LogError("[SdfFontAssetTool] UnityEngine.TextCore.Text.FontAsset 类型未找到 —— " +
                               "UI Toolkit 字体解析需要它;据 TextCoreFontApiProbe 落盘结果复核。");
                return;
            }

            ProbeApi(null, faType, source);

            // ② 生成 TextCore FontAsset(Dynamic + multi-atlas;字符集走预烤,不靠运行时烤)
            UnityEngine.Object asset = GenerateTextCoreFontAsset(faType, source);
            if (asset == null)
            {
                Debug.LogError("[SdfFontAssetTool] TextCore FontAsset 生成失败 —— 见 " +
                               $"{ProbeLogPath}(据实改本文件签名,勿猜)。");
                return;
            }

            // ②·⑤ 预烤字符集(见 CharsetPath 注):落盘前 TryAddCharacters 把字烤进 atlas。
            BakeCharacters(faType, asset);

            // ②·⑥ 预烤后切 Static:字形表随序列化持久化,出包不再被清。
            SetStaticAfterBake(faType, asset);

            // ③ 落盘 + 回报(接线轮据 GUID 替换 USS 引用)
            // ⚠️ 持久化子资产:CreateAsset 只存主对象,material / atlas texture 子资产须
            // AddObjectToAsset 显式挂(否则 m_Material:{fileID:0})。必须在 CreateAsset **之前**
            // 抓存活引用(之后主对象序列化,子资产引用即悬空)。
            var subMaterial = GetProp(asset, "material") as UnityEngine.Object;
            var subAtlas = (GetProp(asset, "atlasTextures") as Array)?.GetValue(0) as UnityEngine.Object;

            Directory.CreateDirectory(Path.GetDirectoryName(
                Path.Combine(Directory.GetCurrentDirectory(), OutputAssetPath)));
            AssetDatabase.CreateAsset(asset, OutputAssetPath);
            if (subMaterial != null) AssetDatabase.AddObjectToAsset(subMaterial, asset);
            if (subAtlas != null) AssetDatabase.AddObjectToAsset(subAtlas, asset);
            AssetDatabase.SaveAssets();
            // 重导入让子资产 fileID 落地(仿 CreatorWindow 第 1601 行 ImportAsset)
            AssetDatabase.ImportAsset(OutputAssetPath);
            AssetDatabase.Refresh();

            Debug.Log($"[SdfFontAssetTool] ✅ SDF FontAsset 已生成:{OutputAssetPath}\n" +
                      $"**GUID 记下来,下一步替换 11 处 -unity-font-definition**:\n" +
                      AssetDatabase.AssetPathToGUID(OutputAssetPath) +
                      "\n(替换面 = 6 份 USS 的 11 条规则,见 SkeuoPaper/SkeuoInk/SkeuoScroll/" +
                      "SkeuoSeal/SkeuoBrass/SkeuoImplement;源 otf 保留不删。)", source);
        }

        /// <summary>
        /// 跨**全部已加载程序集**按全名找类型。
        /// <para>为何需要:2026-10-13 探测实证 <c>GlyphRenderMode</c> **不在**
        /// <c>Unity.TextMeshPro.dll</c> 里(它在 TextCore,由别的程序集承载)——
        /// 只扫 TMP 一个 asm 必找不到。反射类型查找一律走本方法,不假定承载 asm。</para>
        /// </summary>
        private static Type FindTypeAnywhere(string fullName) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(fullName); } catch { return null; } })
                .FirstOrDefault(t => t != null);

        /// <summary>反射读属性(包源码 API 面不可直接 using,全程反射)。</summary>
        private static object GetProp(object o, string name) =>
            o?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(o);

        /// <summary>
        /// 反射探测 TMP API 面并落盘(首跑自证件)。
        /// <para>探测内容 = 生成 FontAsset 所需的**真实**方法签名 / 枚举值 ——
        /// post-cutoff 的 TMP API 不可凭记忆写,探测结果即后续据实选路的依据。</para>
        /// </summary>
        private static void ProbeApi(Assembly tmp, Type faType, Font source)
        {
            var w = new System.Text.StringBuilder();
            w.AppendLine($"# TextCore FontAsset API 探测({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            w.AppendLine($"asm = {faType.Assembly.FullName}");
            w.AppendLine($"FontAsset = {faType.FullName}");
            w.AppendLine($"source font = {source.name}");
            w.AppendLine();

            // CreateFontAsset 重载(直接编程生成的入口)
            foreach (var m in faType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .Where(m => m.Name.Contains("CreateFontAsset")))
                w.AppendLine($"  static {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) : {m.ReturnType.Name}");

            // 采样 / 渲染模式枚举(实际值号要落盘 —— 6.3 的枚举语义靠探测不靠猜)
            foreach (var en in new[] { "UnityEngine.TextCore.Text.AtlasPopulationMode",
                                       "UnityEngine.TextCore.LowLevel.GlyphRenderMode" })
            {
                var t = FindTypeAnywhere(en);
                if (t == null) { w.AppendLine($"{en} = (未找到)"); continue; }
                w.AppendLine($"{en} = {t.FullName}");
                foreach (var n in Enum.GetNames(t).Take(24))
                    w.AppendLine($"  {n} = {Convert.ToInt64(Enum.Parse(t, n))}");
            }

            string full = Path.Combine(Directory.GetCurrentDirectory(), ProbeLogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, w.ToString());
            Debug.Log($"[SdfFontAssetTool] TextCore API 探测已落盘:{ProbeLogPath}\n{w}");
        }

        /// <summary>
        /// 生成 <c>UnityEngine.TextCore.Text.FontAsset</c>(UI Toolkit 唯一接受的字体类型)。
        /// <para>调带参工厂重载 <c>CreateFontAsset(Font, Int32 samplingPointSize, Int32 atlasPadding,
        /// GlyphRenderMode, Int32 atlasWidth, Int32 atlasHeight, AtlasPopulationMode, Boolean
        /// enableMultiAtlasSupport)</c>(TextCoreFontApiProbe 实证签名)。采样 32 / atlas
        /// <see cref="AtlasSize"/> / padding 2 / SDFAA + Dynamic(随后由 BakeCharacters 预烤、
        /// SetStaticAfterBake 切 Static)+ multi-atlas 开(CJK 单页装不下则自动开第二页)。</para>
        /// <para>⚠️ 不用无参 <c>CreateFontAsset(Font)</c>:它写死 sampling 90 / padding 9 /
        /// atlas 1024,对 CJK 会烤很久且 atlas 必溢出。</para>
        /// </summary>
        private static UnityEngine.Object GenerateTextCoreFontAsset(Type faType, Font source)
        {
            var renderModeType = FindTypeAnywhere("UnityEngine.TextCore.LowLevel.GlyphRenderMode");
            var popModeType = FindTypeAnywhere("UnityEngine.TextCore.Text.AtlasPopulationMode");
            if (renderModeType == null || popModeType == null)
            {
                Debug.LogError("[SdfFontAssetTool] GlyphRenderMode / AtlasPopulationMode(TextCore)未找到。" +
                               $"renderMode={renderModeType?.FullName ?? "(null)"} popMode={popModeType?.FullName ?? "(null)"}");
                return null;
            }

            // 带参重载:第 0 参 Font,参数数 ≥ 6
            var m = faType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.Name == "CreateFontAsset")
                .Where(x => x.GetParameters().Length >= 6)
                .Where(x => x.GetParameters()[0].ParameterType == typeof(Font))
                .OrderByDescending(x => x.GetParameters().Length)
                .FirstOrDefault();
            if (m == null)
            {
                Debug.LogError("[SdfFontAssetTool] 未找到带参 CreateFontAsset(Font, …) 重载 —— " +
                               $"见 {ProbeLogPath};据实改本方法,勿猜。");
                return null;
            }

            object renderMode = Enum.Parse(renderModeType, "SDFAA");
            object popMode = Enum.Parse(popModeType, "Dynamic");

            var ps = m.GetParameters();
            var args = new object[ps.Length];
            args[0] = source;
            for (int i = 1; i < ps.Length; i++)
            {
                string n = ps[i].Name ?? string.Empty;
                if (ps[i].ParameterType == typeof(int))
                {
                    if (n.IndexOf("sampling", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = SamplingPointSize;
                    else if (n.IndexOf("padding", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = GlyphPadding;
                    else if (n.IndexOf("width", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = AtlasSize;
                    else if (n.IndexOf("height", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = AtlasSize;
                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : 0;
                }
                else if (ps[i].ParameterType == renderModeType) args[i] = renderMode;
                else if (ps[i].ParameterType == popModeType) args[i] = popMode;
                else if (ps[i].ParameterType == typeof(bool)) args[i] = true;   // multi-atlas
                else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else { Debug.LogError($"[SdfFontAssetTool] 参数 {n} 类型 {ps[i].ParameterType.Name} 无对策。"); return null; }
            }

            try
            {
                var asset = m.Invoke(null, args) as UnityEngine.Object;
                if (asset == null) { Debug.LogError("[SdfFontAssetTool] CreateFontAsset 返回 null。"); return null; }
                Debug.Log($"[SdfFontAssetTool] ✅ TextCore FontAsset 已生成({m.Name}, {args.Length} 参)。");
                return asset;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SdfFontAssetTool] CreateFontAsset 失败:{e.InnerException?.Message ?? e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 把项目字符集预烤进 Dynamic 资产(根因修正的核心步骤)。
        /// <para>Dynamic SDF 资产默认零字形;经 <c>-unity-font-definition</c> 被 UI Toolkit 引用时
        /// 不会触发 TMP 运行时按需烤录 ⇒ 落盘前必须主动 <c>TryAddCharacters</c> 补满字符。</para>
        /// <para><b>算法</b>:分块(每块 ≤ 1024 字,避长字符串栈/超时)喂
        /// <c>TMP_FontAsset.TryAddCharacters(string, bool)</c>(public 实例方法,返回 bool =
        /// 全部烤成 / 部分失败);失败字符收集后告警但不致命(缺字只回退豆腐块,不死进程)。</para>
        /// <para><b>容量验证</b>:1717 字 @ 采样 32px + padding 2 @ SDFAA,单 4096 atlas 实测
        /// 余量充足;若某天字符集暴涨到单 atlas 装不下,TMP 会自动开第二 atlas 页(multi-atlas 已开),
        /// 本步无需改。</para>
        /// </summary>
        private static void BakeCharacters(Type faType, UnityEngine.Object asset)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), CharsetPath);
            if (!File.Exists(path))
            {
                Debug.LogError($"[SdfFontAssetTool] 字符集文件缺失:{CharsetPath} —— " +
                               "先跑 extract_cjk.py 生成;不烤字 ⇒ 资产仍是零字形空壳(绑定必报 MissingAssetReference)。");
                return;
            }

            string charset = File.ReadAllText(path).Replace("\r", "").Replace("\n", "");
            if (string.IsNullOrEmpty(charset))
            {
                Debug.LogError("[SdfFontAssetTool] 字符集为空 —— 不会烤字,资产将零字形。");
                return;
            }

            var tryAdd = faType.GetMethod("TryAddCharacters",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(string), typeof(bool) }, null);
            if (tryAdd == null)
            {
                Debug.LogError("[SdfFontAssetTool] TMP_FontAsset.TryAddCharacters(string, bool) 未找到 —— " +
                               "据 ProbeApi 落盘结果改本方法,勿猜。不烤字 ⇒ 资产零字形。");
                return;
            }

            int total = 0, failedTotal = 0;
            var failedChars = new System.Text.StringBuilder();
            const int chunk = 1024;
            for (int i = 0; i < charset.Length; i += chunk)
            {
                string piece = charset.Substring(i, Math.Min(chunk, charset.Length - i));
                bool ok;
                try { ok = (bool)tryAdd.Invoke(asset, new object[] { piece, true }); }
                catch (Exception e)
                {
                    Debug.LogError($"[SdfFontAssetTool] 烤字块 [{i},{i + piece.Length}) 抛异常:" +
                                   $"{e.InnerException?.Message ?? e.Message}");
                    continue;
                }
                if (!ok)
                {
                    // 逐字探测落败字符(只记第一落败,避免 1717² 扫描 — 但字符集小,直接全扫也快)
                    foreach (char c in piece)
                    {
                        try
                        {
                            if (!(bool)tryAdd.Invoke(asset, new object[] { c.ToString(), false }))
                            { failedChars.Append(c); failedTotal++; }
                        }
                        catch { failedChars.Append(c); failedTotal++; }
                    }
                }
                total += piece.Length;
            }

            if (failedTotal > 0)
                Debug.LogWarning($"[SdfFontAssetTool] 烤字完成:{total} 字中 {failedTotal} 字失败(多为源字体未含的字形;回退豆腐块不致命):\"{failedChars}\"");
            else
                Debug.Log($"[SdfFontAssetTool] ✅ 烤字完成:{total} 字全部进 atlas(Dynamic 资产现已带真实字形)。");
        }

        /// <summary>
        /// 预烤完成后把资产从 Dynamic 切到 Static(根治 B-6-3 / MissingAssetReference 的关键)。
        /// <para><b>根因</b>:<c>TMP_PreBuildProcessor:23</c> 在导入/构建阶段对 <b>Dynamic</b>(含
        /// DynamicOS)且 <c>clearDynamicDataOnBuild=true</c> 的资产**清掉动态字形**;本工程
        /// <c>TMP_Settings.clearDynamicDataOnBuild</c> 默认 true ⇒ 经 <c>AssetDatabase.ImportAsset</c>
        /// 重导入时,烤进去的字被抹回空壳(实测证据:烤字日志报成功、落地 <c>m_GlyphTable</c> 却为空)。</para>
        /// <para><b>对策</b>:烤字后把 <c>atlasPopulationMode</c> 设为 <c>Static</c>。Static 资产
        /// <b>不在</b>清理判据内(同处条件仅含 Dynamic/DynamicOS),字形表随资产序列化持久化 ⇒
        /// 出货运给玩家即「预烤好、零运行时烤录」,恰是本项目「一次性生成、读盘即用」的用途。</para>
        /// <para><b>为何不一开始就用 Static 工厂重载</b>:工厂的 Static 重载在创建期整批烤,但
        /// ugui 2.0.0 的反射面不稳 + Dynamic 工厂 + 事后补字更可控;补完再切 Static 是同一终点、
        /// 风险更低。</para>
        /// </summary>
        private static void SetStaticAfterBake(Type faType, UnityEngine.Object asset)
        {
            var popModeType = FindTypeAnywhere("UnityEngine.TextCore.Text.AtlasPopulationMode");
            if (popModeType == null)
            {
                Debug.LogError("[SdfFontAssetTool] AtlasPopulationMode 类型未找到 —— 无法切 Static。");
                return;
            }

            var prop = faType.GetProperty("atlasPopulationMode",
                BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite)
            {
                Debug.LogError("[SdfFontAssetTool] atlasPopulationMode 属性不可写 —— 无法切 Static。");
                return;
            }

            object staticMode = Enum.Parse(popModeType, "Static");
            prop.SetValue(asset, staticMode, null);
            Debug.Log($"[SdfFontAssetTool] ✅ 资产已由 Dynamic 切 Static(字形表随序列化持久化," +
                      "ImportAsset/出包不再被清;UI Toolkit 绑定即带真实字形)。");
        }
    }
}

#endif
