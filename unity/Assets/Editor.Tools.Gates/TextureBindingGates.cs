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
    /// <summary>贴图接入护栏(Story 019-c)+ 导入格式门(Story 019-e)+ **C8 冻结件一致性门(019-f,2026-10-08)**。
    /// <para>**纯逻辑**:无 `UnityEditor` / `UnityEngine` 依赖 ⇒ EditMode 夹具可直调。</para>
    /// <para>✅ **C8 已入本件**(2026-10-08 冻结轮):`spriteBorder` 与 `-unity-slice-*`
    /// **只能等于切图冻结件**(唯一真源 = `design/assets/specs/nine-slice-freeze-2026-10-08.md`
    /// 的 `freeze-v1` 机器块);原零哨兵耦合守卫(019-e 期)同批**退役** —— 其使命
    /// 「不早于 019-f 被填」已随冻结件落盘完成。</para>
    /// <para>⚠️ **C9(`Pages_frame ≤ PAGES_MAX`)仍不在本件** —— 待图集阈值 spike(019-b);见 story-019 §状态拆分。</para>
    /// <para>⚠️ **019-e 的门只核三项格式**;`spriteBorder` 的**值**由 C8 冻结门验,本门不重复管。</para></summary>
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

        /// <summary>切图冻结件(仓库相对,**从 `unity/` 上溯一级** —— 冻结件属 `design/`,不在工程内)。
        /// <para>唯一真源 = 文件内 `freeze-v1` 机器块(AC-42-C8;019-f 冻结轮 2026-10-08 落盘)。</para></summary>
        public const string FreezeRecordRelPath = "../design/assets/specs/nine-slice-freeze-2026-10-08.md";

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

            // 期望值(AC-42-E1 三项;`spriteBorder` 由 C8 冻结件一致性门验值 —— 本门只管三项格式)
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

        /// <summary>AC-42-C8(冻结件一致性门,2026-10-08 接棒):`spriteBorder` 与 USS `-unity-slice-*`
        /// **只能等于切图冻结件**,两侧任一单点改动即红(禁第二真源)。
        /// <para>**生命周期**:原哨兵门 `ValidateSpriteBorderLeftAsSentinel`(019-e 期「不得自填」
        /// 耦合守卫)已随冻结件落盘**退役** —— 其使命「值不早于 019-f 被填」已完成;
        /// 020 步③ 已一次填入,本门接棒守「填的只能是冻结件的值」。</para>
        /// <para>**真源** = `FreezeRecordRelPath` 文件内 `freeze-v1` 机器块,行式
        /// `文件(相对 Textures/)|冻结值|USS 文件名 或 -`;`-` = 该图无 USS slice 落点。</para>
        /// <para>**反空跑(空跑 ≠ 通过)**:冻结件缺失 / 机器块缺失或 0 行 / `Textures/` 下(含子目录)
        /// `*-final.png` 有漏登记 / 登记的 USS 文件缺失 ⇒ 全部硬报错。</para>
        /// <para>**USS 侧**:`冻结值 > 0` ⇒ 该文件恰 4 条 slice 且全等;`冻结值 = 0` ⇒ 该文件
        /// **零** slice 行(明示不走九宫格,如 `SkeuoInk.uss`);同一 USS 被登记两值 ⇒ 冻结件内部冲突报错。</para></summary>
        public static List<string> ValidateSpriteBorderMatchesFreeze(string repoRoot)
        {
            var errs = new List<string>();

            // ── 1. 冻结件存在 + 机器块可解析 ──
            var recordPath = Path.GetFullPath(Path.Combine(repoRoot, FreezeRecordRelPath));
            if (!File.Exists(recordPath))
            {
                errs.Add($"[C8] 切图冻结件不存在:{recordPath} —— 判据无真源,非合规。");
                return errs;
            }
            var fence = Regex.Match(File.ReadAllText(recordPath),
                @"```freeze-v1\s*\r?\n(.*?)\r?\n```", RegexOptions.Singleline);
            if (!fence.Success)
            {
                errs.Add($"[C8] 冻结件无 `freeze-v1` 机器块:{recordPath} —— 门无解析源,非合规。");
                return errs;
            }

            var rows = new List<(string File, int Slice, string Uss)>();
            foreach (var raw in fence.Groups[1].Value.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var p = line.Split('|');
                if (p.Length != 3 || !p[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                                   || !int.TryParse(p[1].Trim(), out int slice))
                {
                    errs.Add($"[C8] 冻结件机器行不可解析(须 `文件|值|USS`):`{line}`");
                    continue;
                }
                rows.Add((p[0].Trim(), slice, p[2].Trim()));
            }
            if (rows.Count == 0)
            {
                errs.Add("[C8] 冻结件机器块 0 行 —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }

            // ── 2. 覆盖检查:Textures/ 下**每张**(含子目录)`-final.png` 必须有登记行(漏冻 = 冻结清单缺陷)──
            //    ⚠️ 2026-10-08 绑定轮修盲区:原 `TopDirectoryOnly` 不含 `Brass/` 子目录 ⇒ 第 17 行
            //    若被误删,门不会报漏冻。改 AllDirectories 后子目录新图漏登记即红。
            //    (E1 格式门 / 16 张计数仍是顶层面 —— 见冻结件 §六 登记,不连动。)
            var texDir = Path.Combine(repoRoot, TexturesRelDir);
            if (!Directory.Exists(texDir))
            {
                errs.Add($"[C8] 贴图目录不存在:{texDir} —— 扫描空跑,判据不成立(非合规)。");
                return errs;
            }
            var allPngs = Directory.GetFiles(texDir, "*-final.png", SearchOption.AllDirectories)
                                   .Select(f => Path.GetRelativePath(texDir, f).Replace('\\', '/'))
                                   .ToHashSet(StringComparer.Ordinal);
            var rowFiles = rows.Select(r => r.File)
                               .ToHashSet(StringComparer.Ordinal);
            foreach (var missing in allPngs.Except(rowFiles).OrderBy(x => x, StringComparer.Ordinal))
                errs.Add($"[C8] 贴图 `{missing}` 未登记进冻结件机器块 —— 冻结清单漏项。");

            // ── 3. meta 侧:spriteBorder 四值均须等于冻结值 ──
            foreach (var r in rows)
            {
                var metaPath = Path.Combine(texDir,
                    r.File.Replace('/', Path.DirectorySeparatorChar) + ".meta");
                if (!File.Exists(metaPath))
                {
                    errs.Add($"[C8] `{r.File}` 缺 `.meta` —— 冻结值无载体(资产未导入)。");
                    continue;
                }
                var meta = UssCommentRegex.Replace(File.ReadAllText(metaPath), "");
                var bm = Regex.Match(meta,
                    @"spriteBorder:\s*\{\s*x:\s*(-?\d+),\s*y:\s*(-?\d+),\s*z:\s*(-?\d+),\s*w:\s*(-?\d+)\s*\}");
                if (!bm.Success)
                {
                    errs.Add($"[C8] `{Path.GetFileName(metaPath)}` 无 `spriteBorder` 键 —— 导入格式缺字段。");
                    continue;
                }
                if (bm.Groups[1].Value != r.Slice.ToString() || bm.Groups[2].Value != r.Slice.ToString()
                    || bm.Groups[3].Value != r.Slice.ToString() || bm.Groups[4].Value != r.Slice.ToString())
                    errs.Add($"[C8] `{Path.GetFileName(metaPath)}` spriteBorder = " +
                             $"{{{bm.Groups[1].Value}, {bm.Groups[2].Value}, {bm.Groups[3].Value}, {bm.Groups[4].Value}}}" +
                             $",冻结件 = {r.Slice} —— 值只能源自冻结件(AC-42-C8 禁手填)。");
            }

            // ── 4. USS 侧:登记了落点的文件,slice 行集须与冻结值一致 ──
            var ussWant = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in rows.Where(row => row.Uss != "-"))
            {
                if (ussWant.TryGetValue(row.Uss, out int prev) && prev != row.Slice)
                    errs.Add($"[C8] 冻结件内部冲突:`{row.Uss}` 同时登记 {prev} 与 {row.Slice} —— 同一落点须同值。");
                ussWant[row.Uss] = row.Slice;
            }
            foreach (var kv in ussWant)
            {
                var ussPath = Path.Combine(repoRoot, SkeuoUssRelDir, kv.Key);
                if (!File.Exists(ussPath))
                {
                    errs.Add($"[C8] 冻结件登记的 USS 不存在:`{kv.Key}` —— 判据无载体,非合规。");
                    continue;
                }
                var body = UssCommentRegex.Replace(File.ReadAllText(ussPath), "");
                var slices = Regex.Matches(body, @"-unity-slice-(?:left|right|top|bottom)\s*:\s*(\d+)px")
                                  .Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
                if (kv.Value == 0)
                {
                    if (slices.Length != 0)
                        errs.Add($"[C8] `{kv.Key}` 冻结值 = 0(明示不走九宫格)但实见 " +
                                 $"{slices.Length} 条 `-unity-slice-*`:{string.Join(",", slices)}。");
                }
                else if (slices.Length != 4)
                    errs.Add($"[C8] `{kv.Key}` 须恰 4 条 `-unity-slice-*`(实见 {slices.Length}) —— 冻结值 {kv.Value}。");
                else if (slices.Any(s => s != kv.Value.ToString()))
                    errs.Add($"[C8] `{kv.Key}` -unity-slice-* = {string.Join(",", slices)}," +
                             $"冻结件 = {kv.Value} —— USS 值只能源自冻结件(AC-42-C8)。");
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
