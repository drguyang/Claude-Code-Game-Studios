// 权威来源:
//   `production/epics/telemetry-analytics/story-001-readonly-boundary-zero-egress.md`
//     —— AC-51-A6(ADVISORY):「grep `IAudioCueSink`/`AudioCueDto`/`AudioMixer` 零命中」
//   ADR-019 §三(51 住边界层,不成为第四个 `IEventSink` 写入者 —— 同理不做音频消费者)
//   ADR-018 §一(44 只触发 / 只渲染;51 是**读数器**,两者无交集)
//
// 🔁 **2026-10-30 新建(`TD-epic-01-e` 三条 NOT-RUN 结清轮)**:
//   AC-51-A6 此前登记为 **NOT-RUN**,理由是「今日实测零命中,但**无夹具**
//   —— 明日有人加一个就无声失效」。
//   ⇒ 本轮把那个「事实」升成「判据」:**零命中必须由断言保证,不能靠人记得**。
//
//   ⚠️ **本判据的形状**(如实登记):
//     · 它扫的是 **51 的生产树**(`unity/Assets/Telemetry/` 的 C# 源 + `TelemetryReporter` /
//       `TelemetryDeveloperView`),**不是**全仓 —— 全仓当然有 `AudioCueDto`(它住
//       `Sim.Contracts`,44 的契约面)。
//     · 判据 = **源码文本扫描**(去注释后),不是反射 —— 因为要抓的是
//       「有没有人**引用**这些类型」,而「类型在不在」两者都会命中(它们本来就在别的程序集里)。

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Telemetry
{
    [TestFixture]
    internal sealed class TelemetryNoAudioRefsTest
    {
        /// <summary>AC-51-A6:51 的生产树里**零引用** `IAudioCueSink` / `AudioCueDto` / `AudioMixer`。
        /// <para><b>为什么 51 不该碰音频</b>:51 是**读数器**(ADR-019 §一),音频是 44 的呈现面
        /// (ADR-018 §一)。两者无数据交集 —— 一旦 51 开始读音频 cue,「指标只从事件流重算」
        /// 就多了一条流外输入(破 ADR-019 §一 / AC-19-01)。</para>
        /// <para><b>判别力</b>:在 `unity/Assets/Telemetry/` 任一文件里加
        /// `using DaYiJingCheng.Sim.Contracts;` 之外的一行 `AudioCueDto x;` ⇒ 本条红。</para>
        /// </summary>
        [Test]
        public void test_ac51a6_noAudioTypeReferences_inTelemetryTree()
        {
            string[] forbidden = { "IAudioCueSink", "AudioCueDto", "AudioMixer" };

            var files = CollectTelemetrySources();
            Assert.That(files.Count, Is.GreaterThan(0),
                "扫描面为空 —— 本判据会空转通过(假绿)。检查 `unity/Assets/Telemetry/` 是否被改名/移走");

            var violations = new List<string>();
            foreach (string path in files)
            {
                string code = StripComments(File.ReadAllText(path));
                foreach (string token in forbidden)
                    if (code.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{Path.GetFileName(path)} 引用 {token}");
            }

            Assert.That(violations, Is.Empty,
                "51 不得引用任何音频类型(AC-51-A6:51 是读数器,音频是 44 的呈现面):\n" +
                string.Join("\n", violations));
        }

        /// <summary>`TelemetryReporter` / `TelemetryDeveloperView` 也在扫描面内。
        /// <para>⚠️ 这两个住 `Gameplay.Boot`(51 的主实装与调试视图),**不在** `Telemetry/` 目录下。
        /// 缺了它们,判据只扫纯函数层 —— 而那正是 `TD-epic-01-e` 复核轮抓到的
        /// 「扫描面漏了主实装」形态(见 `f2_misdiagnosis_matrix_test` 的缺口 ①)。</para>
        /// </summary>
        [Test]
        public void test_ac51a6_noAudioTypeReferences_inTelemetryOwnerTypes()
        {
            string[] forbidden = { "IAudioCueSink", "AudioCueDto", "AudioMixer" };

            string[] ownerFiles =
            {
                "Gameplay.Boot/TelemetryReporter.cs",
                "Gameplay.Boot/TelemetryDeveloperView.cs",
            };

            var violations = new List<string>();
            int scanned = 0;
            foreach (string rel in ownerFiles)
            {
                string full = Path.Combine(UnityAssetsRoot(), rel);
                // ⚠️ 文件缺失 ⇒ **红**,不得静默跳过(那会让扫描面悄悄缩小)
                Assert.That(File.Exists(full), Is.True,
                    $"51 的实装文件不存在 —— {full}(改名/移走须同批订正本测)");
                scanned++;

                string code = StripComments(File.ReadAllText(full));
                foreach (string token in forbidden)
                    if (code.Contains(token, StringComparison.Ordinal))
                        violations.Add($"{rel} 引用 {token}");
            }

            Assert.That(scanned, Is.EqualTo(ownerFiles.Length), "扫描面必须齐(缺一个即失真)");
            Assert.That(violations, Is.Empty,
                "51 的主实装不得引用任何音频类型(AC-51-A6):\n" + string.Join("\n", violations));
        }

        // ══════════════ 辅助 ══════════════

        /// <summary>收集 `unity/Assets/Telemetry/` 下全部 `.cs`(递归)。</summary>
        private static List<string> CollectTelemetrySources()
        {
            string dir = Path.Combine(UnityAssetsRoot(), "Telemetry");
            Assert.That(Directory.Exists(dir), Is.True, $"51 源目录不存在 —— {dir}");
            var list = new List<string>(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories));
            list.Sort(StringComparer.Ordinal);   // 确定性输出
            return list;
        }

        /// <summary>`unity/Assets/` 的绝对路径(`CallerFilePath` 定位,不依赖 cwd)。
        /// <para>本文件在 `unity/Assets/Tests/EditMode/Telemetry/`
        /// ⇒ 上溯 3 层 = `unity/Assets/`。</para>
        /// </summary>
        private static string UnityAssetsRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile) ?? ".";
            // Telemetry → EditMode → Tests → Assets
            return Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));
        }

        /// <summary>去 `//` 行注释与 `/* */` 块注释。
        /// <para>⚠️ 不处理字符串字面量里的 `//`(本类扫的文件里没有该形态)。</para>
        /// <para>⚠️ **必须去注释**:本测试自己的 doc 就写着这三个名字,不去注释会自指打红
        /// (承 `TD-itemdb-02` 的「防自指」教训)。</para>
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
    }
}
