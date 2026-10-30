// 53(medcons)夹具的**单点仓根解析** —— 2026-10-30 · `TD-medcons-09` 结案件。
//
// 权威 / 背景:
//   `.claude/rules/test-standards.md` 「Unit tests must not depend on external state (filesystem …)」
//   `TD-medcons-09`:`LoadTable` 走 `dataPath/../../` 解析仓根,在 **CI 检出布局变化时即红**(返工期已红过一次)
//
// ── 本条还掉的是什么、没还掉什么(必须连读)──────────────────────────────────
//   ✅ **还掉**:「**脆弱的固定相对路径**」——
//      原写法在 7 个文件里各写一遍 `Path.GetFullPath(Path.Combine(Application.dataPath, "..",".."))`,
//      假定「`Application.dataPath` 上跳两层恰是仓根」。工程一旦换位置 / CI 换检出布局,
//      结果是**深处某个 FileNotFoundException**,而不是一句可读的「找不到仓根」。
//      现在:① 解析集中在一处(布局变只改这里);② **向上搜索锚点**
//      `assets/data/disease_registry.json`,不假定层数;③ 找不到时抛带「已试目录列表」的异常。
//   ❌ **没还掉**:「EditMode 夹具读真实文件」这件**事实**。
//      本条**不做**「把真材料换成内联 fixture」—— 被测对象就是
//      「作者态 JSON → `Binder` → `cooked`」这条链;把源换成测试自己内联的数据,
//      等于把被测对象换成测试的数据,**判据会从「真实素材绑定对不对」退化成「函数自洽」**。
//      ⇒ 这是**具名例外**:本夹具族按**集成性 EditMode 夹具**对待,读仓内真文件是刻意的。
//      ⚠️ 若将来要彻底消灭,正确做法是把这些用例**整体迁到 PlayMode / 独立集成树**,
//      不是把真材料内联 —— 那是另一件事,不挂在本债名下。
//
// ⚠️ **为什么用「向上搜索」而不是 `AppContext.BaseDirectory` 一层跳**:
//   EditMode 下 `AppContext.BaseDirectory` 指向 Unity 编辑器目录,与仓库无关;
//   唯一可信的锚点是「仓库里有个 `assets/data/disease_registry.json`」这个**内容特征**。

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DaYiJingCheng.Tests.MedConsequences
{
    /// <summary>53 夹具的路径解析(仓根 / 工程根 / cooked 目录) —— 全族共用,禁各文件自写。</summary>
    internal static class MedConsRepoPaths
    {
        /// <summary>仓根锚点:该文件存在 ⇒ 所在目录就是仓根。</summary>
        private const string RepoAnchorRelative = "assets/data/disease_registry.json";

        /// <summary>向上搜索的最大层数(仓根 → `unity/` → … 实测 ≤ 3;留裕量)。</summary>
        private const int MaxUpLevels = 8;

        private static string _repoRoot;

        /// <summary>
        /// **仓根**(含 `assets/` 的那一层)。首次调用时解析,之后缓存。
        /// </summary>
        /// <exception cref="InvalidOperationException">向上搜尽仍找不到锚点(报文含已试目录)。</exception>
        public static string RepoRoot => _repoRoot ??= Resolve();

        /// <summary>**工程根** = `Application.dataPath` 的父目录(`…/unity`)。</summary>
        /// <remarks>用于那些「相对工程根」而非「相对仓根」的路径(如
        /// <c>MedConsFloatGate.ConsequencesDirRelative</c> —— 门侧 `RunAll(".")` 以工程根为基)。</remarks>
        public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath)
            ?? throw new InvalidOperationException(
                $"Application.dataPath = «{Application.dataPath}» 没有父目录");

        /// <summary>`Assets/DataCooked/` 绝对路径(cooked 产物所在)。</summary>
        public static string DataCookedDir => Path.Combine(Application.dataPath, "DataCooked");

        /// <summary>`assets/data/<paramref name="fileName"/>` 的绝对路径(作者态源)。</summary>
        public static string DataFile(string fileName)
            => Path.Combine(RepoRoot, "assets", "data", fileName);

        /// <summary>`Assets/DataCooked/<paramref name="fileName"/>` 的绝对路径(烘焙产物)。</summary>
        public static string CookedFile(string fileName)
            => Path.Combine(DataCookedDir, fileName);

        /// <summary>从若干起点向上搜索仓根。</summary>
        private static string Resolve() => ResolveFrom(StartPoints());

        /// <summary>
        /// 从**给定**起点向上搜索仓根。
        /// <para>⚠️ 与 <see cref="RepoRoot"/> 分开暴露是**为了负路径可测**:正常环境下仓根总找得到,
        /// 「找不到时抛什么」这条判据若没有可注入的起点,就只能靠肉眼看代码。</para>
        /// </summary>
        /// <param name="startPoints">搜索起点(可为空 —— 空 ⇒ 立即抛)。</param>
        internal static string ResolveFrom(IEnumerable<string> startPoints)
        {
            var tried = new List<string>();
            foreach (string start in startPoints)
            {
                DirectoryInfo dir;
                try { dir = new DirectoryInfo(start); }
                catch (ArgumentException) { continue; }   // 非法路径:跳过,不炸

                for (int level = 0; level < MaxUpLevels && dir != null; level++, dir = dir.Parent)
                {
                    string candidate = dir.FullName;
                    tried.Add(candidate);
                    // 大小写敏感性交给文件系统(Windows / macOS 不敏感,Linux 敏感 —— 用实际存在性判,不做字符串比较)。
                    //
                    // ⚠️ **判据必须读 `RepoAnchorRelative` 常量,不得在此再写一份字面量**。
                    //   本行初版是 `Path.Combine(candidate, "assets", "data", "disease_registry.json")`
                    //   —— 与常量各写一次 ⇒ 常量只用在异常报文里,**改它不影响解析**
                    //   (证伪轮 M1 实测:改错常量后 10 条夹具里只红 1 条,而红的还是报文断言,
                    //    `test_repoRoot_actuallyContainsTheAnchor` 照样绿)。这正是「两份真源」。
                    if (File.Exists(Path.Combine(candidate,
                            RepoAnchorRelative.Replace('/', Path.DirectorySeparatorChar))))
                        return candidate;
                }
            }

            throw new InvalidOperationException(
                "找不到仓根 —— 判据 = 目录下存在 «" + RepoAnchorRelative + "»。" +
                (tried.Count == 0
                    ? "**一个搜索起点都没有**(空起点列表)。"
                    : "已向上搜索:\n  " + string.Join("\n  ", tried)) +
                "\n⇒ 若 CI 检出布局变化,请确认仓库根仍含 assets/data/;若是新布局,改本文件即可(单点)。");
        }

        /// <summary>搜索起点:`Application.dataPath` 优先(EditMode 下的 `…/unity/Assets`),再兜底当前工作目录。</summary>
        private static IEnumerable<string> StartPoints()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath)) yield return dataPath;

            string cwd = Directory.GetCurrentDirectory();
            if (!string.IsNullOrEmpty(cwd)) yield return cwd;
        }

        /// <summary>**仅供夹具**判断某路径是否可读(把 `File.Exists` 的语义集中一处,便于审计)。</summary>
        public static bool Exists(string absolutePath) => File.Exists(absolutePath);
    }
}
