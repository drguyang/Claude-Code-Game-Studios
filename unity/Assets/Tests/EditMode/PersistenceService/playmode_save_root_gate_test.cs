// `TD-persist-23` 解除动作 ② 的机械门:PlayMode「真启动序」夹具**必须**把存档根指到临时目录。
//
// 病灶(2026-11-05 坐实):`BootRoot` 的存档根默认 = `Application.persistentDataPath/saves`
//   ⇒ 任一 `AddComponent<BootRoot>()` 而**不设** `SaveRootOverride` / `SaveBasePathOverride` 的
//   PlayMode 夹具,都会在启动序的 `FirstSessionSave` 里往**玩家真实存档目录**写一份
//   `save_auto.bin`。实测:`cp -a <saves> /tmp/before` → 跑全量 PlayMode
//   → `diff -rq /tmp/before <saves>` ⇒ 恰多出 `save_auto.bin`(1622 B / `DYJQ` / `SaveVersion 5`)。
//
// ⚠️ 为什么既有两道门都看不见它:
//   ① `FsWriteBoundaryGate`(S16-4)扫的是 `Assets/Tests/**` 里的**直接写盘调用**,
//      本形态是 `BootRoot` **间接**写;
//   ② `save_composer_wiring_test` 的反向门只查「测试源码里有没有把 `persistentDataPath`
//      当 `saveBasePath` 传」,**同样看不见「不传 ⇒ 走默认根」**。
//   ⇒ 两道门全绿,而目录在长。本门补的正是这个盲区。
//
// 判据形态:按**文件**判 —— 同文件里出现 `AddComponent<BootRoot>` 却既没有
//   `SaveRootOverride` 也没有 `SaveBasePathOverride` ⇒ 红。
//
// ⚠️ **只读外部文件的具名声明**(`.claude/rules/test-standards.md`):本夹具读
//   `unity/Assets/Tests/PlayMode/**/*.cs` —— 必须读**真源码树**而不能内联名单,
//   因为「谁在启动 BootRoot」是随夹具增删变化的事实,内联名单会腐烂成第二真源。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.PersistenceService
{
    [TestFixture]
    internal sealed class PlayModeSaveRootGateTest
    {
        /// <summary>夹具树里「真启动序」的判据串。</summary>
        private const string BootSpawn = "AddComponent<BootRoot>";

        /// <summary>两条合法的存档根覆盖缝(语义见 `BootRoot._saveRootOverride` / `_saveBasePathOverride`)。</summary>
        private static readonly string[] OverrideTokens = { "SaveRootOverride", "SaveBasePathOverride" };

        /// <summary>空集守卫下限:今日真启动序夹具 27 个,取 10 —— 路径写错/正则失效时不会恒绿。</summary>
        private const int MinBootSpawnFiles = 10;

        [Test]
        public void test_everyPlayModeBootFixture_pointsSaveRootAtTemp()
        {
            string playModeDir = Path.Combine(RepoRoot(), "unity", "Assets", "Tests", "PlayMode");
            Assert.That(Directory.Exists(playModeDir), Is.True,
                "PlayMode 夹具目录不存在 —— 路径写错(本条的判据面就是它):" + playModeDir);

            string[] files = Directory.GetFiles(playModeDir, "*.cs", SearchOption.AllDirectories);
            var offenders = new List<string>();
            int bootFiles = 0;

            foreach (string path in files)
            {
                // ⚠️ 判据必须锚在**代码**上,不锚在注释上 —— 夹具头注里大量写着这两个属性名
                //    (`multi_world_boot_wiring_test` 等),不剥注释会退化成「注释顶替门」(假绿 F6)。
                string code = StripComments(File.ReadAllText(path));
                if (!code.Contains(BootSpawn, StringComparison.Ordinal)) continue;
                bootFiles++;

                // 覆盖缝的**赋值**形式才算:`X.SaveRootOverride = …`(属性声明在 BootRoot 里,不在夹具树)
                bool covered = OverrideTokens.Any(t => code.Contains("." + t + " ", StringComparison.Ordinal)
                                                    || code.Contains("." + t + "=", StringComparison.Ordinal));
                if (!covered)
                {
                    int line = code.Take(code.IndexOf(BootSpawn, StringComparison.Ordinal))
                                   .Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetFileName(path)}:{line}");
                }
            }

            // 空集守卫:扫到的「真启动序」文件太少 ⇒ 扫描面失效,拒以空集冒充绿
            Assert.That(bootFiles, Is.GreaterThanOrEqualTo(MinBootSpawnFiles),
                $"只扫到 {bootFiles} 个含 `{BootSpawn}` 的 PlayMode 文件(下限 {MinBootSpawnFiles}) —— " +
                "扫描面或判据串失效了,本条此刻**没有**判别力。");

            Assert.That(offenders, Is.Empty,
                "以下 PlayMode 夹具直接 `AddComponent<BootRoot>()` 却没把存档根指到临时目录 ⇒ " +
                "跑回归会往**玩家真实存档目录**写 `save_auto.bin`(TD-persist-23):\n  " +
                string.Join("\n  ", offenders) +
                "\n修:`boot.SaveRootOverride = PlayModeTempSaveRoot.New(\"<夹具名>\");`(多世界形态)或" +
                "\n    `boot.SaveBasePathOverride = <临时目录>;`(单世界形态),二选一,必须在 BootRoot 的 Start 之前赋值。");
        }

        /// <summary>去掉 `//` 行注释与 `/* */` 块注释(判据须锚在**实现**上,不锚在文档上)。
        /// <para>⚠️ 不做完整词法分析 —— 只处理本族文件会出现的形态;字符串字面量里的 `//` 不处理
        /// (夹具文件里没有这种形态)。承 `patient_visual_seams_test.StripComments` 的同款理由。</para>
        /// </summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            int i = 0;
            while (i < src.Length)
            {
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '*')
                {
                    int close = src.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = close < 0 ? src.Length : close + 2;
                    continue;
                }
                if (i + 1 < src.Length && src[i] == '/' && src[i + 1] == '/')
                {
                    int nl = src.IndexOf('\n', i);
                    i = nl < 0 ? src.Length : nl;
                    continue;
                }
                sb.Append(src[i]);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>本测试自身的源码(`CallerFilePath`)→ 上溯 5 层 = **仓库根**。
        /// <para>本文件在 `unity/Assets/Tests/EditMode/PersistenceService/` ⇒
        /// PersistenceService → EditMode → Tests → Assets → unity → 仓库根。</para>
        /// </summary>
        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));
    }
}
