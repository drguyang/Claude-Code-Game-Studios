// 主菜单 · 静态判据(2026-11-01 · `design/ux/main-menu-42.md`)
//
// ▍本件读哪些仓库文件、为什么必须读真件而不是内联
//   · `unity/Assets/Gameplay.UI/**/*.cs`                        —— AC-MM-10 的扫描面(42 程序集)
//   · `unity/Assets/Gameplay.Boot/BootRoot.cs`                  —— AC-MM-07 的 `LoadSceneMode.Single` 扫描面
//   · `unity/Assets/Scenes/MainMenu.unity(.meta)`               —— AC-MM-07「三场景制」的第二个场景
//   · `unity/Assets/AddressableAssetsData/AssetGroups/*.asset`  —— 地址 `mainmenu` 的登记面
//   理由:这四条判据的**宾语就是这些文件本身**(谁在扫谁的目录 / 谁写了 Single / 场景与地址是否
//   同源)。内联一份副本等于测副本 —— 真件漂移时夹具仍然绿(`boot_scene_wiring_test` 同款纪律)。
//   全部**只读**,零写盘。
//
// ▍为什么放 EditMode
//   四条都是资产 / 源码文本级判据,不需要启动序;放 PlayMode 会让每次跑都付一次装配代价。
//   运行期那一半(菜单真的摊开 / 落焦 / 两条输入腿 / 世界装载后 Boot 常驻件还在)
//   在 `unity/Assets/Tests/PlayMode/main_menu_wiring_test.cs`。

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DaYiJingCheng.Tests.SkeuomorphicUI
{
    /// <summary>主菜单的静态判据(AC-MM-07 / AC-MM-10)。</summary>
    public sealed class main_menu_static_test
    {
        /// <summary>仓库根(`Application.dataPath` = &lt;root&gt;/unity/Assets ⇒ 上两级)。</summary>
        private static string RepoRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **AC-MM-10**:42 侧零存档目录知识 —— `hasContinueSave` 由装配层注入,
        /// UI 不得自己扫描目录(spec §10 所有者纪律)。
        /// <para>判据 = 在 `Gameplay.UI` 全树的 `.cs` 里,存档目录 API 与存档文件名词面**零命中**。
        /// 为什么不看「有没有调用 `SaveService`」:那会漏掉 `Directory.GetFiles` 这类**绕过服务**
        /// 的写法 —— 而绕过服务正是本条要防的形态(第二份「什么算可继续」)。</para>
        /// <para>变异面:在 `MainMenuScreen` 里加一行 `Directory.Exists(...)` ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_uiAssembly_hasZeroSaveDirectoryKnowledge()
        {
            string uiDir = Path.Combine(Application.dataPath, "Gameplay.UI");
            Assert.IsTrue(Directory.Exists(uiDir), $"扫描面不存在(拒以空集冒充绿):{uiDir}");

            string[] files = Directory.GetFiles(uiDir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 20,
                $"扫描面过窄(实得 {files.Length} 个 .cs)—— 路径疑已迁移,本条须重指");

            // 存档目录 API(读写都禁)+ 存档文件名约定 + 存档根目录常量
            var forbidden = new (string Pattern, string Why)[]
            {
                (@"\bDirectory\s*\.", "目录 API(Directory.Exists / GetFiles / EnumerateFiles …)"),
                (@"\bDirectoryInfo\b", "目录 API(DirectoryInfo)"),
                (@"\bpersistentDataPath\b", "存档根目录常量(属 7a 装配面)"),
                (@"[""']save_", "存档文件名约定(第二份「什么算存档」)"),
            };

            var hits = new List<string>();
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                foreach ((string pattern, string why) in forbidden)
                {
                    Match m = Regex.Match(text, pattern);
                    if (m.Success)
                        hits.Add($"{file}: 「{why}」命中 `{m.Value}`");
                }
            }

            Assert.IsEmpty(hits,
                "42 程序集不得含任何存档目录知识(AC-MM-10 / spec §10)—— 命中:\n" +
                string.Join("\n", hits));
        }

        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **AC-MM-07(静态半)**:启动序全链**不出现** `LoadSceneMode.Single`
        /// (ADR-023 §①「永不用 Single」)。
        /// <para>⚠️ 只扫 `BootRoot.cs` 是不够的 —— 该口径是对**整个装配层**说的。
        /// 故扫描面 = `Gameplay.Boot` 全树。</para>
        /// <para>变异面:把 `LoadSceneMode.Additive` 改成 `Single` ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_bootAssembly_neverUsesLoadSceneModeSingle()
        {
            string bootDir = Path.Combine(Application.dataPath, "Gameplay.Boot");
            Assert.IsTrue(Directory.Exists(bootDir), $"扫描面不存在:{bootDir}");

            string[] files = Directory.GetFiles(bootDir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 10, $"扫描面过窄(实得 {files.Length} 个 .cs)");

            // ⚠️ **判据读的是代码,不是散文**:注释里写「永不用 LoadSceneMode.Single」
            //    恰恰是本纪律的**正确**形态(BootRoot 文件头就是这么写的)——
            //    不剥注释,本门会把引用纪律的注释判成违例,逼人删掉那句注释。
            //    ⇒ 先剥注释(逐字符扫描,字符串 / 逐字字符串 / 字符字面量都跳过),
            //      再扫剩下的代码。
            var hits = new List<string>();
            foreach (string file in files)
            {
                string code = StripComments(File.ReadAllText(file));
                MatchCollection ms = Regex.Matches(code, @"LoadSceneMode\s*\.\s*Single");
                foreach (Match m in ms) hits.Add($"{file}: `{m.Value}`");
            }

            Assert.IsEmpty(hits,
                "ADR-023 §①:主菜单与世界的装载均 additive,全流程不得出现 LoadSceneMode.Single —— 命中:\n" +
                string.Join("\n", hits));
        }

        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 剥掉 C# 注释(逐字符扫描;`"…"` / `@"…"` / `\'…\'` 三种字面量内部不剥)。
        /// <para>为什么要自己写而不是正则:正则会先命中字符串里的 `//`(URL、路径、正则本身),
        /// 也可能把 `/*` 之后整段吞掉 —— 两者都会让「扫代码」变成「扫随机文本」。</para>
        /// <para>⚠️ 本函数只服务静态扫描门,不追求完整 C# 词法(不考虑插值字符串里的嵌套引号;
        /// 本仓 `Gameplay.Boot` 今日无此形态,若将来出现,须在命中时人工判读)。</para>
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                char c = src[i];

                if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
                {
                    while (i < src.Length && src[i] != '\n') i++;          // 行注释:到行尾
                    continue;
                }
                if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < src.Length && !(src[i] == '*' && src[i + 1] == '/')) i++;
                    i = Math.Min(i + 2, src.Length);                        // 块注释:到 */
                    continue;
                }
                if (c == '@' && i + 1 < src.Length && src[i + 1] == '"')
                {
                    sb.Append('"'); i += 2;                                 // 逐字字符串:@"" 转义
                    while (i < src.Length)
                    {
                        if (src[i] == '"')
                        {
                            if (i + 1 < src.Length && src[i + 1] == '"') { sb.Append('"'); i += 2; continue; }
                            sb.Append('"'); i++; break;
                        }
                        sb.Append(src[i]); i++;
                    }
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(c); i++;
                    while (i < src.Length)
                    {
                        if (src[i] == '\\') { sb.Append(src[i]); i++; if (i < src.Length) { sb.Append(src[i]); i++; } continue; }
                        bool end = src[i] == quote;
                        sb.Append(src[i]); i++;
                        if (end) break;
                    }
                    continue;
                }

                sb.Append(c); i++;
            }
            return sb.ToString();
        }

        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// **AC-MM-07(资产半)**:`MainMenu` 场景存在,且其 guid 与地址 `mainmenu`
        /// 在**同一 group 资产内共现**(与 `boot_scene_wiring_test` 的 world 判据同构)。
        /// <para>变异面:删 group 条目 / 改地址串 ⇒ 本条红(运行期 `LoadSceneAsync` 亦硬失败)。</para>
        /// </summary>
        [Test]
        public void test_mainMenuScene_registeredInAddressables()
        {
            string scenePath = Path.Combine(Application.dataPath, "Scenes", "MainMenu.unity");
            string metaPath = scenePath + ".meta";
            Assert.IsTrue(File.Exists(scenePath), $"MainMenu.unity 不在(ADR-023 §① 的第二个场景):{scenePath}");
            Assert.IsTrue(File.Exists(metaPath), $"MainMenu.unity.meta 不在:{metaPath}");

            Match guidMatch = Regex.Match(File.ReadAllText(metaPath), @"^guid:\s*([0-9a-f]{32})", RegexOptions.Multiline);
            Assert.IsTrue(guidMatch.Success, "MainMenu.unity.meta 无 guid —— 资产标识缺失");
            string sceneGuid = guidMatch.Groups[1].Value;

            string addrDir = Path.Combine(Application.dataPath, "AddressableAssetsData");
            if (!Directory.Exists(addrDir))
                Assert.Ignore("Addressables 本地工作配置不在(gitignored)—— 与本条无关的环境缺失");

            string groupsDir = Path.Combine(addrDir, "AssetGroups");
            Assert.IsTrue(Directory.Exists(groupsDir), $"AssetGroups/ 缺失:{groupsDir}");

            bool cooccurred = false;
            foreach (string f in Directory.GetFiles(groupsDir, "*.asset", SearchOption.AllDirectories))
            {
                string t = File.ReadAllText(f);
                if (t.Contains(sceneGuid) && t.Contains("m_Address: mainmenu")) { cooccurred = true; break; }
            }

            Assert.IsTrue(cooccurred,
                "Addressable 须在同一 group 资产内共现 MainMenu.unity guid(" + sceneGuid +
                ")与 `m_Address: mainmenu`(BootRoot.MainMenuSceneAddress 的加载键)—— " +
                "缺任一侧 = 启动序摊主菜单时硬失败");
        }
    }
}
