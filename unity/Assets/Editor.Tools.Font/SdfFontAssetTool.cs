// 脉案 SDF FontAsset 生成器(sprint-08 S3 「字糊」根治件 · 2026-10-13)
//
// 权威来源:
//   · design/ux/casebook-39.md §13 `[V]` 行(可读性人工腿) + §11(最小字号 / 对比承诺)
//   · accessibility-requirements.md:85(纸面 UI ≥24px @1080p)
//   · ADR-022(Tooling 层纪律:编辑期 / 构建期,运行期零存在,不进构建)
//   · ADR-025(具名六装配清单 = 运行期面;本平台 `Editor.Tools.Font` 属
//     `Editor.Tools` 族,includePlatforms = Editor,**不进玩家构建**)
//
// 病因(2026-10-13 用户真机实测「够大但糊」,三层已排除):
//   ① 不是字号 —— 已调 24→28px(`--skeuo-font-size-lexeme`);
//   ② 不是视口缩放 —— PanelSettings `m_ScaleMode: 2` Constant Pixel Size,1px=1px;
//   ③ 不是 import 采样点 —— `fontSize: 16 → 32` + `characterPadding: 1 → 2` 已试,
//      **实测仍糊**(用户 2026-10-13 复测结论)⇒ A 案(改 otf import)无效。
//   ⇒ 真因 = otf 被 `-unity-font-definition` **直接引用**,走 UI Toolkit 动态字体路径:
//      无 atlas、逐帧现场光栅化、大字号无锐化补偿。**唯一正路 = 转 SDF FontAsset。**
//
// 设计说明:
//   · **首跑自证**:本平台第一次碰 TMP(全仓零 `using TMPro` 先例),而
//     `TMP_FontAsset.CreateFontAsset` / `FontEngine` 的签名 Unity 6.3 post-cutoff,
//     模型记忆不可信 ⇒ 先经 [`ProbeTmpApi`] 反射探测并把**真签名**落盘,再据实生成;
//     探测日志 = 判据可复现的证据件(不是「应该能用」的猜测)。
//   · **双路径**,由探测结果决定走哪条(见 ProbeTmpApi 的 `HasGeneratorWindow` 分支)。
//   · 生成后**不改 USS / 不动 GUID** —— 那一步由后续接线轮做(11 处
//     `-unity-font-definition` 的 GUID 替换 + 守护测扫描面核验),本文件只产资产。
//     (一次只做一件事:先证明资产能生成且 GUID 可用,再谈替换面。)
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

        /// <summary>TMP Settings 的**资源名**(非路径):TMP 按此名 Resources.Load,
        /// 放在任一 Resources 目录下即可;文件名须与此常量逐字相同。</summary>
        private const string TmpSettingsResourceName = "TMP Settings";

        /// <summary>
        /// UI Toolkit 自带的文字 SDF shader 名 —— <c>PanelSettings.m_SDFShader</c> 实值
        /// (2026-10-13 spike 探针 [SPIKE-A] 反射读出,非猜)。
        /// <para>为何用**引擎内置 shader** 而不是自建一个 TMP mobile SDF shader 文件:
        /// spike 实证 UI Toolkit 渲染 FontAsset 走的是 PanelSettings 的这个 SSD shader,
        /// **根本不用** <c>FontAsset.material</c> 的 TMP shader ⇒ 我们喂给 TMP 生成器的
        /// shader **只在 Editor 期构造 Material 对象时被读一次,之后随生成结束即弃**,
        /// 不进玩家构建、不进渲染路径。⇒ 用引擎内置 SSD shader 是等价且零维护的。</para>
        /// </summary>
        private const string UiToolkitSdfShaderName = "Hidden/TextCore/Distance Field SSD";

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

            // ① 反射探测(首跑自证 —— 全仓零 TMP 先例,不凭记忆猜签名)
            var tmp = FindTmpAssembly();
            if (tmp == null)
            {
                Debug.LogError("[SdfFontAssetTool] 未找到 Unity.TextMeshPro 程序集 —— " +
                               "确认 com.unity.ugui(2.0.0)已在包栈(ugui 传递依赖 TMP)。");
                return;
            }

            var faType = tmp.GetType("TMPro.TMP_FontAsset");
            if (faType == null)
            {
                Debug.LogError("[SdfFontAssetTool] TMPro.TMP_FontAsset 类型未找到。");
                return;
            }

            // ⓪ **前置:建 TMP Settings**(2026-10-13 首跑 NRE 的真因,已据包源码定位):
            //    `TMP_Settings.instance` = `Resources.Load<TMP_Settings>("TMP Settings")`
            //    (ugui 2.0.0 Runtime/TMP/TMP_Settings.cs:455);工程从未 Import TMP Essentials
            //    ⇒ 无该资源 ⇒ `CreateFontAssetInstance` 读 `TMP_Settings.clearDynamicDataOnBuild`
            //    (:149 → 594)NRE。官方正解 = Window→TextMeshPro→Import TMP Essentials(它建
            //    Settings + Default Font Asset 全套);本方法建**等价最小面**(仅 Settings),
            //    不引入官方默认字体(FallbackFontRegistry 另有失实待订正,不在本件面)。
            EnsureTmpSettings(tmp);

            ProbeApi(tmp, faType, source);

            // ② 双路径生成(优先官方窗口类 —— 它封装了 atlas 分配 / 字形排序 / 落盘)
            //    ⚠️ 生成前必须先喂 shader(ugui 2.0.0 不随包发布 TMP mobile SDF shader,
            //    而 CreateFontAssetInstance 建 material 时读它 ⇒ NRE;喂法见 InjectShaderIfAbsent)。
            InjectShaderIfAbsent(tmp);
            UnityEngine.Object asset = GenerateViaWindow(tmp, source) ?? GenerateViaFacade(tmp, faType, source);
            if (asset == null)
            {
                Debug.LogError("[SdfFontAssetTool] 两条生成路径均失败 —— 见上面 ProbeApi 落盘的 " +
                               $"{ProbeLogPath}(据实选路;勿在未读探测结果前改本文件签名)。");
                return;
            }

            // ②·⑤ 预烤字符集进 Dynamic 资产(Dynamic 模式零字形的根因修正 —— 见 CharsetPath 注)。
            //    必须在持久化**之前**调用:TryAddCharacters 把字形烤进 atlas(内存)并写入 glyph table,
            //    之后 CreateAsset + AddObjectToAsset 一并落盘;否则落盘的是零字形空壳,UI Toolkit
            //    绑定即报 MissingAssetReference。
            BakeCharacters(faType, asset);

            // ②·⑥ **预烤后切 Static 模式**(B-6-3 根治的关键一招)。
            //    ⚠️ Dynamic 资产会被 `TMP_PreBuildProcessor:23`(条件 = Dynamic/DynamicOS 且
            //    clearDynamicDataOnBuild=true)**在导入/构建时清掉动态字形** ⇒ 烤进去的字在
            //    ImportAsset 阶段被抹回空壳(实测:烤字日志报成功、落地 m_GlyphTable 却为空)。
            //    Static 资产不在清理判据内(同处条件仅含 Dynamic/DynamicOS)⇒ 切 Static 后字形表
            //    随资产序列化持久化,出货运给玩家即「预烤好、零运行时烤录」—— 恰是本项目用途。
            SetStaticAfterBake(faType, asset);

            // ③ 落盘 + 回报(接线轮据 GUID 替换 11 处 USS 引用)
            // ⚠️ **持久化子资产(Dynamic/Static 同需)**:`CreateFontAssetInstance` 在内存里
            // new 出 material(第 651 行)与 1×1 atlas texture(第 610-611 行)挂在 asset 上,
            // 但 `AssetDatabase.CreateAsset` **只存主对象**,子资产引用会随风而逝
            // ⇒ 落盘后 `m_Material: {fileID: 0}`(首跑产物即此)。官方 `TMPro_FontAssetCreatorWindow`
            // 第 1487/1496、1668/1687 行用 `AddObjectToAsset` 显式挂子资产 —— 本件照做。
            // 必须在 `CreateAsset` **之前**抓存活引用(之后主对象序列化,子资产引用即悬空)。
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
        /// 建 TMP Settings 资源(等价最小面,仅当缺失时)。
        /// <para>路径 = <c>Assets/TextMesh Pro/Resources/TMP Settings.asset</c> ——
        /// 文件名必须是 <c>TMP Settings</c> 且在**任一 <c>Resources</c> 目录**下,
        /// 因为加载方式是 <c>Resources.Load&lt;TMP_Settings&gt;("TMP Settings")</c>(按资源名,
        /// 不按路径;ugui 2.0.0 `TMP_Settings.cs:455` + `TMP_PostBuildProcessHandler.cs:17`
        /// 两处同口径)。</para>
        /// <para>已存在 = 不动(不覆盖用户改过的设置)。</para>
        /// </summary>
        private static void EnsureTmpSettings(Assembly tmp)
        {
            var settingsType = tmp.GetType("TMPro.TMP_Settings");
            if (settingsType == null)
            {
                Debug.LogError("[SdfFontAssetTool] TMPro.TMP_Settings 未找到 —— TMP 程序集面与预期不符。");
                return;
            }

            // 完整性检查走反射(不 using TMPro —— 本文件除创建时需要的 ScriptableObject.CreateInstance
            // 外全程反射;asmdef 引 Unity.TextMeshPro 只是让程序集在编辑期可加载)
            var existing = Resources.Load(TmpSettingsResourceName, settingsType);
            if (existing != null)
            {
                Debug.Log($"[SdfFontAssetTool] TMP Settings 已存在({TmpSettingsResourceName})—— 不覆盖。");
                return;
            }

            const string dir = "Assets/TextMesh Pro/Resources";
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
                AssetDatabase.CreateFolder("Assets", "TextMesh Pro");
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/TextMesh Pro", "Resources");

            var created = ScriptableObject.CreateInstance(settingsType);
            if (created == null)
            {
                Debug.LogError("[SdfFontAssetTool] TMP_Settings 实例化失败。");
                return;
            }

            string path = $"{dir}/{TmpSettingsResourceName}.asset";
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SdfFontAssetTool] 已建 TMP Settings(最小面):{path}\n" +
                      "⚠️ 官方 Import TMP Essentials 会另建 Default Font Asset + 样式;" +
                      "本件只需 Settings 存在(Generate 的唯一 settings 依赖点 = " +
                      "TMP_Settings.clearDynamicDataOnBuild),不引入官方默认字体。");
        }

        /// <summary>
        /// 喂 shader 给 TMP 生成器(ugui 2.0.0 缺 TMP mobile SDF shader 的**权宜**)。
        /// <para><b>根因(已据包源码定位,非推断)</b>:<c>TMP_FontAsset.CreateFontAssetInstance</c>
        /// 建 material 时读 <c>ShaderUtilities.ShaderRef_MobileSDF</c>(ugui 2.0.0
        /// <c>TMP_FontAsset.cs:594</c>);该属性懒加载 <c>Shader.Find("TextMeshPro/Mobile/Distance Field")</c>
        /// (<c>TMP_ShaderUtilities.cs:143-147</c>)—— 而 com.unity.ugui 2.0.0 **不随包发布**
        /// 该 shader(2026-10-13 spike `spike_b` 实证 <c>Shader.Find</c> 返 null)⇒ NRE
        /// "Value cannot be null. Parameter name: shader"。</para>
        /// <para><b>喂法</b>:反射写**私有静态字段** <c>k_ShaderRef_MobileSDF</c> 为
        /// 引擎内置 <c>Hidden/TextCore/Distance Field SSD</c>(<c>PanelSettings.m_SDFShader</c> 实值)。
        /// 属性 getter 是 <c>if (null) Find(...)</c> ⇒ 字段非空后它**恒返回我们塞的值**,
        /// TMP 的生成逻辑其余全部原样复用(字形 / atlas / 多页),不重写、不改引擎文件。</para>
        /// <para><b>为何不含渲染风险</b>:spike `spike_a` 实证 UI Toolkit 渲染 FontAsset 走
        /// PanelSettings 的 SSD shader,**不读** <c>FontAsset.material</c> 的 TMP shader
        /// ⇒ 本喂入只在 Editor 期构造 Material 时被读一次,随生成结束即弃;不进玩家构建。</para>
        /// </summary>
        private static void InjectShaderIfAbsent(Assembly tmp)
        {
            var shaderUtils = tmp.GetType("TMPro.ShaderUtilities");
            if (shaderUtils == null)
            {
                Debug.LogError("[SdfFontAssetTool] TMPro.ShaderUtilities 未找到 —— 无法喂 shader,生成必 NRE。");
                return;
            }

            // 已是非 null(TMP 某天随包带了 shader)= 不动,避免覆盖引擎自己的选择
            var field = shaderUtils.GetField("k_ShaderRef_MobileSDF",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                Debug.LogError("[SdfFontAssetTool] k_ShaderRef_MobileSDF 字段未找到(反射面漂移?)—— " +
                               "据 ProbeApi 落盘结果改本方法,勿猜。");
                return;
            }

            if (field.GetValue(null) != null)
            {
                Debug.Log("[SdfFontAssetTool] TMP 已自有 mobile SDF shader(引擎版本补齐?)—— 不喂。");
                return;
            }

            var replacement = Shader.Find(UiToolkitSdfShaderName);
            if (replacement == null)
            {
                Debug.LogError($"[SdfFontAssetTool] Shader.Find(\"{UiToolkitSdfShaderName}\") = null —— " +
                               "引擎内置 SSD shader 不可达(名字漂移?据 spike_a 复核);不生成(免 NRE)。");
                return;
            }

            field.SetValue(null, replacement);
            Debug.Log($"[SdfFontAssetTool] 已喂 shader:{UiToolkitSdfShaderName} → " +
                      $"{shaderUtils.Name}.k_ShaderRef_MobileSDF(仅 Editor 期构造 Material 用,不进渲染)。");
        }

        /// <summary>定位 TMP 程序集(按名字找,不写死路径 —— ugui 版本升级路径会变)。</summary>
        private static Assembly FindTmpAssembly() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Unity.TextMeshPro");

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
            w.AppendLine($"# TMP API 探测({DateTime.Now:yyyy-MM-dd HH:mm:ss})");
            w.AppendLine($"asm = {tmp.FullName}");
            w.AppendLine($"TMP_FontAsset = {faType.FullName}");
            w.AppendLine($"source font = {source.name}");
            w.AppendLine();

            // 生成器窗口类(官方菜单 Window → TextMeshPro → Font Asset Creator)
            var winType = tmp.GetType("TMPro.EditorUtilities.TMP_FontAssetCreatorWindow") ??
                          AppDomain.CurrentDomain.GetAssemblies()
                              .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                              .FirstOrDefault(t => t != null && t.Name.Contains("FontAssetCreator"));
            w.AppendLine($"FontAssetCreatorWindow = {winType?.FullName ?? "(未找到)"}");

            if (winType != null)
            {
                foreach (var m in winType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                       .Where(m => m.Name.Contains("Generate") || m.Name.Contains("Create") || m.Name.Contains("Setup")))
                    w.AppendLine($"  method {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) : {m.ReturnType.Name}");
            }

            // TMP_FontAsset.CreateFontAsset 重载(直接编程生成的入口)
            foreach (var m in faType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .Where(m => m.Name.Contains("CreateFontAsset")))
                w.AppendLine($"  static {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) : {m.ReturnType.Name}");

            // 采样 / 渲染模式枚举(实际值号要落盘 —— 6.3 的枚举语义靠探测不靠猜)
            foreach (var en in new[] { "TMPro.AtlasPopulationMode", "TMPro.GlyphRenderMode" })
            {
                var t = tmp.GetType(en);
                if (t == null) { w.AppendLine($"{en} = (未找到)"); continue; }
                w.AppendLine($"{en} = {t.FullName}");
                foreach (var n in Enum.GetNames(t).Take(24))
                    w.AppendLine($"  {n} = {Convert.ToInt64(Enum.Parse(t, n))}");
            }

            string full = Path.Combine(Directory.GetCurrentDirectory(), ProbeLogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, w.ToString());
            Debug.Log($"[SdfFontAssetTool] TMP API 探测已落盘:{ProbeLogPath}\n{w}");
        }

        /// <summary>
        /// 路径一:Editor 官方 <c>FontAssetCreatorWindow.CreateFontAsset</c>(静态工厂,非 GUI)。
        /// <para>2026-10-13 探测实证该类 = <c>UnityEditor.TextCore.Text.FontAssetCreatorWindow</c>
        /// (**在 UnityEditor 程序集,不在 TMP**),持 static 工厂重载
        /// <c>(String fontFilePath, Int32 faceIndex, Int32 samplingPointSize,
        /// Int32 atlasPadding, GlyphRenderMode renderMode, Int32 atlasWidth, Int32 atlasHeight)</c>
        /// —— 比 TMP 直调重载多一个 <c>fontFilePath</c>(内部自行 LoadFontFace),
        /// 且是官方 Font Asset Creator 的同一实现,字符集 / atlas 分页处理最完整。</para>
        /// </summary>
        private static UnityEngine.Object GenerateViaWindow(Assembly tmp, Font source)
        {
            var winType = FindTypeAnywhere("UnityEditor.TextCore.Text.FontAssetCreatorWindow") ??
                          AppDomain.CurrentDomain.GetAssemblies()
                              .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                              .FirstOrDefault(t => t != null && t.Name == "FontAssetCreatorWindow");
            if (winType == null)
            {
                Debug.Log("[SdfFontAssetTool] 路径一:FontAssetCreatorWindow 未找到,转路径二。");
                return null;
            }

            var renderModeType = FindTypeAnywhere("UnityEngine.TextCore.LowLevel.GlyphRenderMode") ??
                                 FindTypeAnywhere("GlyphRenderMode");
            if (renderModeType == null) { Debug.Log("[SdfFontAssetTool] 路径一:GlyphRenderMode 类型未找到,转路径二。"); return null; }

            // 取 fontFilePath 重载(优先全字符集路径);无则退 (Font) 或 (Font, …) 重载
            var byPath = winType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateFontAsset"
                                  && m.GetParameters().Length >= 6
                                  && m.GetParameters()[0].ParameterType == typeof(string));
            var byFont = winType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "CreateFontAsset"
                                  && m.GetParameters()[0].ParameterType == typeof(Font));

            MethodInfo target = byPath ?? byFont;
            if (target == null) { Debug.Log("[SdfFontAssetTool] 路径一:无可用 CreateFontAsset 重载,转路径二。"); return null; }

            object renderMode = Enum.Parse(renderModeType, "SDFAA");
            var ps = target.GetParameters();
            var args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                string n = ps[i].Name ?? string.Empty;
                if (ps[i].ParameterType == typeof(string)) args[i] = AssetDatabase.GetAssetPath(source);
                else if (ps[i].ParameterType == typeof(Font)) args[i] = source;
                else if (ps[i].ParameterType == typeof(int))
                {
                    if (n.IndexOf("sampling", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = SamplingPointSize;
                    else if (n.IndexOf("padding", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = GlyphPadding;
                    else if (n.IndexOf("width", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = AtlasSize;
                    else if (n.IndexOf("height", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = AtlasSize;
                    else args[i] = 0;   // faceIndex 等
                }
                else if (ps[i].ParameterType == renderModeType) args[i] = renderMode;
                else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else { Debug.LogWarning($"[SdfFontAssetTool] 路径一:参数 {n} 类型无对策,转路径二。"); return null; }
            }

            try
            {
                var asset = target.Invoke(null, args) as UnityEngine.Object;
                if (asset == null) { Debug.Log("[SdfFontAssetTool] 路径一:返回 null,转路径二。"); return null; }
                Debug.Log($"[SdfFontAssetTool] ✅ 路径一成功:{target.Name}({args.Length} 参,{target.DeclaringType?.Name})");
                return asset;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SdfFontAssetTool] 路径一失败({e.InnerException?.Message ?? e.Message}),转路径二。");
                return null;
            }
        }

        /// <summary>
        /// 路径二:直接调 <c>TMP_FontAsset.CreateFontAsset</c> 的**带参**重载。
        /// <para>⚠️ 刻意不用无参重载 <c>CreateFontAsset(Font)</c>:它写死
        /// <c>sampling 90 / padding 9 / atlas 1024</c>(ugui 2.0.0 `TMP_FontAsset.cs:543`),
        /// 采样 90 对 17MB CJK 全字体会烤很久且 atlas 1024 必溢出多页;
        /// 带参重载(:560)把四参交给本件常量(采样 32 / atlas {<see cref="AtlasSize"/>} /
        /// padding 2 / SDFAA + Dynamic population + multi-atlas)。</para>
        /// </summary>
        private static UnityEngine.Object GenerateViaFacade(Assembly tmp, Type faType, Font source)
        {
            // 目标重载:(Font, int, int, GlyphRenderMode, int, int[, AtlasPopulationMode[, bool]])
            var candidates = faType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.Name == "CreateFontAsset")
                .Where(x => x.GetParameters()[0].ParameterType == typeof(Font))
                .Where(x => x.GetParameters().Length >= 6)
                .OrderByDescending(x => x.GetParameters().Length)
                .ToList();

            if (candidates.Count == 0)
            {
                Debug.LogError("[SdfFontAssetTool] 未找到带参 CreateFontAsset 重载 —— " +
                               "探测日志里有全部可用重载;据实改本方法,勿猜。");
                return null;
            }

            var renderModeType = FindTypeAnywhere("UnityEngine.TextCore.LowLevel.GlyphRenderMode") ??
                                 FindTypeAnywhere("GlyphRenderMode");
            var popModeType = tmp.GetType("TMPro.AtlasPopulationMode");
            if (renderModeType == null || popModeType == null)
            {
                Debug.LogError("[SdfFontAssetTool] GlyphRenderMode / AtlasPopulationMode 类型未找到。" +
                               $"renderMode={renderModeType?.FullName ?? "(null)"} popMode={popModeType?.FullName ?? "(null)"}");
                return null;
            }

            object renderMode = Enum.Parse(renderModeType, "SDFAA");
            object popMode = Enum.Parse(popModeType, "Dynamic");

            foreach (var m in candidates)
            {
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
                        else if (n.IndexOf("faceIndex", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = 0;
                        else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : 0;
                    }
                    else if (ps[i].ParameterType == renderModeType) args[i] = renderMode;
                    else if (ps[i].ParameterType == popModeType) args[i] = popMode;
                    else if (ps[i].ParameterType == typeof(bool)) args[i] = true;   // multi-atlas:必开(CJK 单页装不下)
                    else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                    else { Debug.LogWarning($"[SdfFontAssetTool] {m.Name} 参数 {n} 类型 {ps[i].ParameterType.Name} 无对策,跳本重载。"); args = null; break; }
                }

                if (args == null) continue;

                try
                {
                    var asset = m.Invoke(null, args) as UnityEngine.Object;
                    if (asset == null) { Debug.LogWarning($"[SdfFontAssetTool] {m.Name}({args.Length} 参) 返回 null。"); continue; }
                    Debug.Log($"[SdfFontAssetTool] CreateFontAsset 走 {m.Name}({args.Length} 参) 成功。");
                    return asset;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SdfFontAssetTool] {m.Name}({args.Length} 参) 失败:{e.InnerException?.Message ?? e.Message}");
                }
            }

            return null;
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
            var popModeType = FindTypeAnywhere("TMPro.AtlasPopulationMode") ??
                              AppDomain.CurrentDomain.GetAssemblies()
                                  .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                                  .FirstOrDefault(t => t.Name == "AtlasPopulationMode");
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
