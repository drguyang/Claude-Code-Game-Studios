// Story 019-c / 019-e —— 贴图接入护栏(AC-42-C7 骨架半 / C10 / C11)与导入格式门(AC-42-E1)的**纯逻辑**半。
//
// 权威来源:
//   · production/epics/skeuomorphic-ui/story-019-texture-binding.md §Acceptance Criteria
//   · ADR-013 §Implementation Guidelines 2/3/5(元件库唯一出口 · 图集 · 构建期断言)
//   · control-manifest Presentation Layer Rules「贴图必须经元件库接入(不得在屏幕 UXML 内直引)」
//   · TR-skeuoui-011(图集护栏)
//
// ⚠️ **为什么与 SkeuomorphicUiGates.cs 分家**(承 PresentationDtoGuard.cs 先例):
//   `SkeuomorphicUiGates` 整体住 `#if UNITY_EDITOR`,而承此门者的 EditMode 夹具
//   (`SkeuomorphicUI.Tests`,`includePlatforms:["Editor"]`)须在**无 UnityEditor 依赖**下
//   直调断言。本件**不引 `UnityEditor` / `UnityEngine`** —— GUID 解析走可注入委托
//   (生产侧由 `SkeuomorphicUiGates` 注入 `AssetDatabase.GUIDToAssetPath`)。
//   ⇒ 门住 `#if UNITY_EDITOR`,**纯逻辑面住这里**,两半共用同一台机器。
//
// 形态 = 错误列表(空 = 通过);本件**零 throw** —— 聚合非空 ⇒ 构建失败由调用方决定。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>贴图接入护栏(Story 019-c)+ 导入格式门(Story 019-e)。
    /// <para>**纯逻辑**:无 `UnityEditor` / `UnityEngine` 依赖 ⇒ EditMode 夹具可直调。</para>
    /// <para>⚠️ **C8(slice = 冻结件元数据)/ C9(`Pages_frame ≤ PAGES_MAX`)不在本件** ——
    /// 前者待切图冻结件(019-f)、后者待图集阈值 spike(019-b);见 story-019 §状态拆分。</para>
    /// <para>⚠️ **019-e 的门只核「格式已订正」+「border 仍是零哨兵」** ——
    /// `spriteBorder` 的**值**归 019-f 冻结件,本件**刻意不填、也不验值**。</para></summary>
    public static class TextureBindingGates
    {
        // ── url() 取值正则 ──

        /// <summary>USS `background-image: url("...")` 取值(引号可选)。</summary>
        public static readonly Regex BackgroundImageUrlRegex =
            new Regex(@"background-image\s*:\s*url\s*\(\s*[""']?([^""')]+)[""']?\s*\)",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>任意 `url(...)` 取值(C10 屏幕层扫描 —— 比 C7 更宽:任何 url 均不许)。</summary>
        public static readonly Regex AnyUrlRegex =
            new Regex(@"url\s*\(\s*[""']?([^""')]+)[""']?\s*\)",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>USS 注释去除(免 regex 误报注释内容 —— 与 SkeuomorphicUiGates 同纪律)。</summary>
        public static readonly Regex UssCommentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>元件库 USS 目录(仓库相对)。</summary>
        public const string SkeuoUssRelDir = "Assets/Gameplay.UI/Skeuomorphic";

        /// <summary>屏幕级文件目录(仓库相对)—— AC-42-C10 的扫描面。</summary>
        public const string ScreensRelDir = "Assets/Gameplay.UI/Skeuomorphic/Screens";

        /// <summary>主题变量文件(元件库内**非**元件 USS,不参与 C7/C11 扫描)。</summary>
        public const string ThemeVariablesFileName = "SkeuoThemeVariables.uss";

        // ═══ AC-42-C10 谓词 ═══

        /// <summary>C10 判定:该 url 是否指向贴图目录(屏幕层禁引)。</summary>
        public static bool UrlTargetsTextures(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string u = url.Replace('\\', '/');
            return u.IndexOf("Textures/", StringComparison.OrdinalIgnoreCase) >= 0
                || u.IndexOf("-final.png", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ═══ AC-42-C11 谓词 ═══

        /// <summary>C11 判定:url 是否可在资产库解析(悬空 GUID / 缺失文件检测)。
        /// <para><paramref name="guidResolver"/> = 可注入的 GUID→路径解析(生产侧传
        /// `AssetDatabase.GUIDToAssetPath`;测试侧传桩 ⇒ 本件保持零引擎依赖)。</para></summary>
        public static bool AssetResolves(string url, Func<string, string> guidResolver, string repoRoot)
        {
            if (string.IsNullOrEmpty(url)) return false;
            string rel = url.Replace('\\', '/').Trim();

            if (rel.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
            {
                string guid = rel.Substring("guid:".Length).Trim();
                if (string.IsNullOrEmpty(guid)) return false;
                return guidResolver != null && !string.IsNullOrEmpty(guidResolver(guid));
            }

            string abs = rel.StartsWith("Assets/", StringComparison.Ordinal)
                ? rel
                : "Assets/" + rel;
            return File.Exists(Path.Combine(repoRoot, abs));
        }

        // ═══ 文件扫描(仓库根由调用方给 ⇒ 夹具可指到自己的夹具目录)═══

        /// <summary>AC-42-C10:屏幕级 UXML/USS 内的 `url()` 命中(空 = 合规)。
        /// <para>⚠️ **反空跑守卫**:扫描面须存在且非空 —— 否则「零命中」是**看不到**,不是**合规**
        /// (承 `coding-standards.md` §测试证据「空跑 ≠ 通过」纪律)。</para></summary>
        public static List<string> ValidateScreenLevelNoTextureUrl(string repoRoot)
        {
            var errs = new List<string>();
            var screensDir = Path.Combine(repoRoot, ScreensRelDir);
            if (!Directory.Exists(screensDir))
            {
                errs.Add($"[C10] 屏幕级目录不存在:{screensDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            var files = Directory.GetFiles(screensDir, "*.uss", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(screensDir, "*.uxml", SearchOption.TopDirectoryOnly))
                .ToArray();
            if (files.Length == 0)
            {
                errs.Add($"[C10] 屏幕级目录无 UXML/USS:{screensDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            foreach (var f in files)
            {
                string body = UssCommentRegex.Replace(File.ReadAllText(f), "");
                var lines = body.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    // ⚠️ 此处**刻意**用 AnyUrlRegex(任一 url 皆拦),比 AC 措辞更严 —— AC 只说「不得指向 Textures/」,
                    //    而屏幕层任何 url() 都意味着绕开元件库。`UrlTargetsTextures` 只作**诊断分级**用
                    //    (见下),不作过滤,以免把「更严的过拦」悄悄退化成「只拦 Textures/」。
                    foreach (Match m in AnyUrlRegex.Matches(lines[i]))
                    {
                        string url = m.Groups[1].Value;
                        string kind = UrlTargetsTextures(url) ? "贴图" : "非贴图";
                        errs.Add($"[C10] 屏幕级文件 {Path.GetFileName(f)}:{i + 1} " +
                                 $"出现 url(\"{url}\")({kind}) —— 贴图只经元件库接入,屏幕层禁直引。");
                    }
                }
            }
            return errs;
        }

        /// <summary>AC-42-C11:元件库 USS 内每个 background-image url() 须可解析(空 = 全部可解析)。
        /// <para>⚠️ **反空跑守卫**:元件库 USS 目录须存在且含 ≥1 非 theme 文件。</para></summary>
        public static List<string> ValidateTextureReferencesResolve(string repoRoot, Func<string, string> guidResolver)
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(repoRoot, SkeuoUssRelDir);
            if (!Directory.Exists(ussDir))
            {
                errs.Add($"[C11] 元件库 USS 目录不存在:{ussDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            var componentFiles = Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f) != ThemeVariablesFileName)
                .ToArray();
            if (componentFiles.Length == 0)
            {
                errs.Add($"[C11] 元件库无非 theme USS:{ussDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            foreach (var ussFile in componentFiles)
            {
                string body = UssCommentRegex.Replace(File.ReadAllText(ussFile), "");
                foreach (Match m in BackgroundImageUrlRegex.Matches(body))
                {
                    string url = m.Groups[1].Value;
                    if (!AssetResolves(url, guidResolver, repoRoot))
                        errs.Add($"[C11] {Path.GetFileName(ussFile)} 的 background-image url(\"{url}\") " +
                                 "无法解析 —— 贴图缺失 / GUID 悬空(禁静默降级纯色)。");
                }
            }
            return errs;
        }

        /// <summary>AC-42-C7(骨架半):每个**已注册**元件类名须在元件库 USS 内存在选择器块。
        /// <para><paramref name="registeredClasses"/> = 注册表类名集(由调用方从
        /// `SkeuoComponentRegistry` 取,本件不引 Gameplay.UI —— 保持零依赖)。</para></summary>
        public static List<string> ValidateRegisteredClassesHaveSelectorBlock(
            string repoRoot, IEnumerable<string> registeredClasses)
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(repoRoot, SkeuoUssRelDir);
            var classes = (registeredClasses ?? Enumerable.Empty<string>()).Where(c => !string.IsNullOrEmpty(c)).ToArray();
            if (classes.Length == 0)
            {
                errs.Add("[C7] 已注册类集为空 —— 判据不成立(非合规;注册表应至少 1 类)。");
                return errs;
            }
            if (!Directory.Exists(ussDir))
            {
                errs.Add($"[C7] 元件库 USS 目录不存在:{ussDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            var text = string.Join("\n",
                Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly)
                    .Where(f => Path.GetFileName(f) != ThemeVariablesFileName)
                    .Select(File.ReadAllText));
            text = UssCommentRegex.Replace(text, "");

            foreach (var cls in classes)
            {
                var m = Regex.Match(text, @"\." + Regex.Escape(cls) + @"\s*\{", RegexOptions.IgnoreCase);
                if (!m.Success)
                    errs.Add($"[C7] 已注册元件类「.{cls}」在选择器块中不存在。");
            }
            return errs;
        }

        // ═══ AC-42-E1 谓词(019-e:导入格式订正)═══

        /// <summary>贴图族目录(仓库相对)—— 019-e 的扫描面。</summary>
        public const string TexturesRelDir = "Assets/Gameplay.UI/Skeuomorphic/Textures";

        /// <summary>把 `.meta` 文本里的单个 `key: value` 读出来(找不到返回 null)。
        /// <para>只做**机械读取**,不解析 YAML —— `.meta` 是 Unity 生成的固定缩进文本。</para></summary>
        private static string MetaScalar(string metaText, string key)
        {
            var m = Regex.Match(metaText, @"^\s*" + Regex.Escape(key) + @":\s*(\S+)\s*$",
                RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>AC-42-E1:16 张元件贴图的 `.meta` 须满足九宫格导入格式
        /// (`spriteMode: 1` · `textureType: 8` · `alphaIsTransparency: 1`)。
        /// <para>⚠️ 这是 **AC-42-C8 的物理前提** —— `spriteMode: 0` 下九宫格**不可能工作**
        /// (story-019 「物理前提」条)。</para>
        /// <para>⚠️ **反空跑守卫**:贴图目录须存在且找到 ≥1 张 `*-final.png`。</para></summary>
        public static List<string> ValidateSlicedTextureImportFormat(string repoRoot)
        {
            var errs = new List<string>();
            var texDir = Path.Combine(repoRoot, TexturesRelDir);
            if (!Directory.Exists(texDir))
            {
                errs.Add($"[E1] 贴图目录不存在:{texDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            var pngs = Directory.GetFiles(texDir, "*-final.png", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (pngs.Length == 0)
            {
                errs.Add($"[E1] 贴图目录无 `*-final.png`:{texDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            // 期望值(AC-42-E1 三项;`spriteBorder` **刻意不在此列** —— 归 019-f 冻结件)
            var expected = new (string Key, string Want)[]
            {
                ("spriteMode", "1"),
                ("textureType", "8"),
                ("alphaIsTransparency", "1"),
            };

            foreach (var png in pngs)
            {
                string metaPath = png + ".meta";
                if (!File.Exists(metaPath))
                {
                    errs.Add($"[E1] {Path.GetFileName(png)} 缺 `.meta` —— 资产未导入。");
                    continue;
                }
                string meta = File.ReadAllText(metaPath);
                foreach (var (key, want) in expected)
                {
                    string got = MetaScalar(meta, key);
                    if (got == null)
                        errs.Add($"[E1] {Path.GetFileName(metaPath)} 无 `{key}` 键 —— 导入格式订正未落。");
                    else if (!string.Equals(got, want, StringComparison.Ordinal))
                        errs.Add($"[E1] {Path.GetFileName(metaPath)} 的 `{key}` = {got},须为 {want}" +
                                 "(九宫格物理前提;AC-42-E1)。");
                }
            }
            return errs;
        }

        /// <summary>AC-42-E1(耦合守卫):`spriteBorder` 的**值**归 019-f 冻结件 ——
        /// 本门只核「**不早于 019-f 被写死**」,即**必须仍是零哨兵**。
        /// <para>⚠️ **为什么这条守卫是承重的**:019-e 若顺手把 `spriteBorder` 填了,
        /// 就制造了**第二真源**(手填值 vs 冻结件),正是 story-019 承 `:126`
        /// 「做完即错」纪律要消灭的形态。零哨兵 ⇒ 019-f 落冻结件时**一次填入**,无中间态。</para>
        /// <para>⚠️ 本门**不**验证「值对不对」(那是 019-f 的活),只验证「**还没被填**」。</para></summary>
        public static List<string> ValidateSpriteBorderLeftAsSentinel(string repoRoot)
        {
            var errs = new List<string>();
            var texDir = Path.Combine(repoRoot, TexturesRelDir);
            if (!Directory.Exists(texDir))
            {
                errs.Add($"[E1] 贴图目录不存在:{texDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            var pngs = Directory.GetFiles(texDir, "*-final.png", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (pngs.Length == 0)
            {
                errs.Add($"[E1] 贴图目录无 `*-final.png`:{texDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            // `spriteBorder: {x: 0, y: 0, z: 0, w: 0}` = 零哨兵。
            // ⚠️ 缩进非固定(实测 2 空格),故**不锚 `^`**,用 `[^\n{]*` 吞掉前导任意空白 ——
            //    锚 `^` 会因缩进宽度变化而静默失配 ⇒ 假报「已自填」(本门首跑即踩此坑)。
            var sentinel = new Regex(@"spriteBorder:\s*\{\s*x:\s*0,\s*y:\s*0,\s*z:\s*0,\s*w:\s*0\s*\}");
            foreach (var png in pngs)
            {
                string metaPath = png + ".meta";
                if (!File.Exists(metaPath)) continue;   // 缺 .meta 由格式门报
                string meta = File.ReadAllText(metaPath);
                if (!sentinel.IsMatch(UssCommentRegex.Replace(meta, "")))
                    errs.Add($"[E1] {Path.GetFileName(metaPath)} 的 `spriteBorder` **已非零哨兵** —— " +
                             "其值须来自 019-f 的切图冻结件元数据,019-e **不得自填**(禁第二真源;" +
                             "story-019 §状态拆分「做完即错」纪律)。");
            }
            return errs;
        }

        /// <summary>AC-42-C7(接图半):每个**贴图容器类**的选择器块须含 `background-image: url(...)`。
        /// <para>⚠️ 2026-10-05(019-d):四基类接图完成 ⇒ 本半**已入 `ValidateAll` 聚合**
        /// (见 `SkeuomorphicUiGates.ValidateTextureContainerHasTexture`)。
        /// 判据面 = `SkeuoComponentRegistry.TextureContainerClassNames`(收窄,非全部注册类)。</para>
        /// <para>⚠️ 调用方亦可直传任意类名集(夹具测试用)—— 传空集时**不报「集空」**
        /// (集空守卫在 `<see cref="ValidateRegisteredClassesHaveSelectorBlock"/>` 骨架半)。</para></summary>
        public static List<string> ValidateRegisteredClassesHaveTexture(
            string repoRoot, IEnumerable<string> registeredClasses)
        {
            var errs = new List<string>();
            var ussDir = Path.Combine(repoRoot, SkeuoUssRelDir);
            if (!Directory.Exists(ussDir)) return errs;

            var text = string.Join("\n",
                Directory.GetFiles(ussDir, "*.uss", SearchOption.TopDirectoryOnly)
                    .Where(f => Path.GetFileName(f) != ThemeVariablesFileName)
                    .Select(File.ReadAllText));
            text = UssCommentRegex.Replace(text, "");

            foreach (var cls in registeredClasses)
            {
                if (string.IsNullOrEmpty(cls)) continue;
                var blockMatch = Regex.Match(text, @"\." + Regex.Escape(cls) + @"\s*\{([^}]*)\}",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!blockMatch.Success)
                    continue;   // 选择器块缺失由 ValidateRegisteredClassesHaveSelectorBlock 报
                if (!BackgroundImageUrlRegex.IsMatch(blockMatch.Groups[1].Value))
                    errs.Add($"[C7] 已注册元件类「.{cls}」缺 `background-image: url(...)` —— " +
                             "纯色填充模拟贴图(AC-42-C7 禁;接图归 019-d)。");
            }
            return errs;
        }

        /// <summary>仓库根(供生产侧 `SkeuomorphicUiGates` 与夹具共用)。
        /// <para>⚠️ **不可直接用 `Directory.GetCurrentDirectory()`** —— 实测(repo 内 `unity/Logs/probe.xml`)
        /// Unity CLI 跑 EditMode 时 cwd = `<repo>/unity`(Unity 工程根),**不是** `<repo>`;
        /// 仓库根另在 `<回显>/..`。承 `modal_gate_test.cs:502` 同族订正:
        /// 单一 `Path.Combine(cwd,"Assets",…)` 拼不中 ⇒ 目录不存在 ⇒ 扫描函数提前空返回 ⇒
        /// 门恒绿(静默假绿)。此处上溯寻 `Assets/` 实存的那一层。</para></summary>
        public static string DefaultRepoRoot => ResolveRepoRoot(Directory.GetCurrentDirectory());

        /// <summary>把任意候选目录上溯为「含 `Assets/` 的仓库根」;找不到则原样返回(由调用方空跑门兜底)。</summary>
        public static string ResolveRepoRoot(string candidate)
        {
            if (string.IsNullOrEmpty(candidate)) return candidate;
            var dir = new DirectoryInfo(Path.GetFullPath(candidate));
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return candidate;
        }
    }
}
