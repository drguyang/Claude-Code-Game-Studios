// S25-5 · tutorial-005 —— 48 的**零设门**判据(AC-48-17)。
//
// 权威:`design/gdd/tutorial-and-onboarding.md` 规则五 / AC-48-17 ·
//       `design/ux/tutorial-48.md:57`(「若本屏画步列表,等于用 UI 绕过该断言」)
// 门本体:`unity/Assets/Editor.Tools.Gates/TutorialZeroGate.cs`
//
// ⚠️ 本件的**正向判据**(`test_ac48_17_publicSurface_hasNoGateApi`)跑的是**真 48 装配**;
//    负夹具跑的是**测试内合成类型** —— 两者必须同时存在:
//    只有正控 ⇒ 扫描器可能恒空;只有负控 ⇒ 真装配的违规没人看。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Tutorial;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.EditMode.Tutorial
{
    [TestFixture]
    public class tutorial_zero_gate_guard_test
    {
        // ══════════ 正控:真 48 装配的公开面零门禁词 ══════════

        [Test]
        public void test_ac48_17_publicSurface_hasNoGateApi()
        {
            List<string> errs = TutorialZeroGate.RunAll();

            Assert.That(errs, Is.Empty,
                "48 公开面出现「解锁 / 门禁 / 完成状态」查询 API —— AC-48-17(规则五零设门):\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_ac48_17_scanIsNotEmpty_notVacuousGreen()
        {
            // 空扫描面会让上面那条恒绿(F4 空洞正控)⇒ 先证「真的扫到了东西」
            var asm = typeof(DaYiJingCheng.Gameplay.UI.Tutorial.TutorialDispatcher).Assembly;
            Type[] exported = asm.GetExportedTypes();

            Assert.That(exported.Length, Is.GreaterThan(0), "48 装配零公开类型 —— 扫描面丢失");
            // 已知必在的公开类型(任一消失 = 装配被搬走 / 改名,本判据要当场知道)
            Assert.That(exported.Any(t => t.Name == "TutorialDispatcher"), Is.True,
                "48 公开面里没有 TutorialDispatcher —— 扫描的不是这一支装配");
        }

        // ══════════ 负夹具 1(G1):命名命中 ══════════

        private sealed class LeakyNamedSurface
        {
            public int GetProgress() => 0;
            public bool IsUnlocked => false;
            public bool IsInTutorial() => false;
        }

        [Test]
        public void test_ac48_17_fixture_namedGateMembers_areRed()
        {
            List<string> errs = TutorialZeroGate.ScanPublicSurface(
                new[] { typeof(LeakyNamedSurface) }, "fixture");

            Assert.That(errs, Has.Count.EqualTo(3),
                "三个门禁命名成员(GetProgress / IsUnlocked / IsInTutorial)应各自命中一条:\n" +
                string.Join("\n", errs));
            StringAssert.Contains("GetProgress", errs[0] + errs[1] + errs[2]);
        }

        // ══════════ 负夹具 2(G2):返回类型命中(改名旁路)══════════

        private readonly struct FakeProgressView
        {
        }

        private sealed class LeakyReturnType
        {
            // 方法名干干净净,但返回的类型本身就是「进度视图」
            public FakeProgressView Get() => default;
        }

        [Test]
        public void test_ac48_17_fixture_cleanNameButProgressReturnType_isRed()
        {
            List<string> errs = TutorialZeroGate.ScanPublicSurface(
                new[] { typeof(LeakyReturnType) }, "fixture");

            Assert.That(errs, Has.Count.EqualTo(1), string.Join("\n", errs));
            StringAssert.Contains("返回类型", errs[0]);
            StringAssert.Contains("FakeProgressView", errs[0]);
        }

        // ══════════ 正控(合成):干净类型零违例 ══════════

        private sealed class CleanSurface
        {
            public string StepId => "observe";
            public IReadOnlyList<string> Dispatch() => Array.Empty<string>();
        }

        [Test]
        public void test_ac48_17_fixture_cleanSurface_isGreen()
        {
            List<string> errs = TutorialZeroGate.ScanPublicSurface(
                new[] { typeof(CleanSurface) }, "fixture");

            Assert.That(errs, Is.Empty,
                "干净类型被误判 ⇒ 上面两条负夹具有可能是「永远红」而失去判别力:\n" +
                string.Join("\n", errs));
        }

        [Test]
        public void test_ac48_17_emptyScanSurface_isRed()
        {
            List<string> errs = TutorialZeroGate.ScanPublicSurface(new Type[0], "fixture");

            Assert.That(errs, Has.Count.EqualTo(1));
            StringAssert.Contains("扫描面为 **0 个类型**", errs[0]);
        }

        // ══════════ 资产闭集:48 目录零非代码资产(AC-48-01/02 的 48 侧)══════════

        [Test]
        public void test_ac48_01_48Directory_hasNoOverlayAssets()
        {
            string dir = Path.Combine(RepoRoot(), "unity", "Assets", "Gameplay.UI", "Tutorial");
            Assert.That(Directory.Exists(dir), Is.True, $"48 目录不存在:{dir}");

            List<string> errs = TutorialZeroGate.ScanAssetIndex(dir);
            Assert.That(errs, Is.Empty, string.Join("\n", errs));
        }

        [Test]
        public void test_ac48_01_fixture_tooltipUxml_inTutorialDir_isRed()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dyjc-tut-asset-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "TutorialOkay.cs"), "// 合法:代码");
                File.WriteAllText(Path.Combine(dir, "Tooltip.uxml"), "<ui:UXML/>");
                File.WriteAllText(Path.Combine(dir, "hint.png"), "not-really-a-png");

                List<string> errs = TutorialZeroGate.ScanAssetIndex(dir);

                Assert.That(errs, Has.Count.EqualTo(2),
                    "浮层 UXML 与贴图各应命中一条(代码不报):\n" + string.Join("\n", errs));
                StringAssert.Contains("Tooltip.uxml", errs[0] + errs[1]);
                StringAssert.Contains("hint.png", errs[0] + errs[1]);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { /* 清理失败不影响判定 */ }
            }
        }

        // ══════════ 零按键提示:复用 39/烘焙的**同一张**词族表(禁分叉)══════════
        //
        // Story 005 的 Control Manifest Guardrail:「黑名单词族与 39 Story 004 扫描器**共享夹具源**
        // (一处扩两处生效,禁分叉)」⇒ 本判据**不另写词表**,直接用
        // `TutorialContentSchema.ExplicitInstructionTokens`(烘焙校验 5 的同一个常量对象)。
        // 若有人在校验器里扩充了词族而这里没跟 —— 不可能:同一个字段。

        [Test]
        public void test_zeroKeyHint_contentStrings_useTheSharedWordFamily()
        {
            TutorialContentTable content = LoadContent();
            IReadOnlyList<string> tokens = TutorialContentSchema.ExplicitInstructionTokens;

            Assert.That(tokens, Is.Not.Empty, "词族表为空 —— 本判据会恒绿(空洞正控)");

            var texts = new List<(string where, string text)>();
            foreach (TutorialStepRow st in content.Steps) texts.Add(("step.title/" + st.Id, st.Title));
            foreach (TutorialNarrationRow n in content.Narration) texts.Add(("narration/" + n.Id, n.Text));
            foreach (TutorialPaperRow pp in content.Papers) { texts.Add(("paper.title/" + pp.Id, pp.Title)); texts.Add(("paper.body/" + pp.Id, pp.Body)); }

            Assert.That(texts.Count, Is.GreaterThan(0), "没有任何可扫文本 —— 判据会空转通过");

            var hits = new List<string>();
            foreach ((string where, string text) in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                foreach (string token in tokens)
                {
                    if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                        hits.Add($"[零按键提示] {where} 命中词「{token}」:「{text}」");
                }
            }

            Assert.That(hits, Is.Empty,
                "48 的用户可见文本出现按键/指路词族(AC-48-06①「只教动作,不指路」):\n" +
                string.Join("\n", hits));
        }

        // ══════════ AC-48-12:48 的口述 cue 全在 44 的**行为反馈白名单**内 ══════════

        [Test]
        public void test_ac48_12_everyNarrationCue_isInAudioWhitelist()
        {
            TutorialContentTable content = LoadContent();
            AudioEventDataSet audio = LoadAudio();

            Assert.That(audio.RowCount, Is.GreaterThan(0), "44 事件表零行 —— 判据不可执行");

            var byCue = new Dictionary<string, AudioEventRow>(StringComparer.Ordinal);
            for (int i = 0; i < audio.RowCount; i++)
            {
                AudioEventRow r = audio.Row(i);
                if (!string.IsNullOrEmpty(r.Cue)) byCue[r.Cue] = r;
            }

            var bad = new List<string>();
            foreach (TutorialNarrationRow n in content.Narration)
            {
                if (!byCue.TryGetValue(n.CueId, out AudioEventRow row))
                {
                    bad.Add($"[AC-48-12] cue「{n.CueId}」不在 44 事件表里 —— 48 的触发源无法判定");
                    continue;
                }

                if (!AudioEventTableGates.WhitelistCategories.Contains(row.WhitelistCategory))
                    bad.Add($"[AC-48-12] cue「{n.CueId}」的 whitelist_category「{row.WhitelistCategory}」不在合法集内");

                if (row.TriggerSource != null && AudioEventTableGates.ForbiddenTriggerSources.Contains(row.TriggerSource))
                    bad.Add($"[AC-48-12] cue「{n.CueId}」的 trigger_source「{row.TriggerSource}」是**状态播报**形态" +
                            "(ADR-018 §六 无提示音铁律)—— 教学口述不得用来报状态");
            }

            Assert.That(bad, Is.Empty, string.Join("\n", bad));
        }

        // ── 真产物装载 ──
        private static TutorialContentTable LoadContent()
        {
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "DataCooked", TutorialContentTable.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}`");
            Assert.That(TutorialContentTable.TryDecode(File.ReadAllBytes(path), out TutorialContentTable t, out string err),
                Is.True, $"48 内容表解码失败:{err}");
            return t;
        }

        private static AudioEventDataSet LoadAudio()
        {
            string path = Path.Combine(RepoRoot(), "unity", "Assets", "DataCooked", AudioEventDataSet.CookedFileName);
            Assert.That(File.Exists(path), Is.True, $"缺烘焙产物 `{path}`");
            return AudioEventCookedCodec.Read(File.ReadAllBytes(path));
        }

        // ── 仓库根 ──
        // ⚠️ 用 `[CallerFilePath]` 定位本文件再向上找,而**不用** `Directory.GetCurrentDirectory()`:
        // 后者会让 `FsWriteBoundaryGate` 的 R2 把这件判成「仓库锚点 + 写盘」需具名声明 ——
        // 本件的写盘只落 `Path.GetTempPath()`(见 `test_ac48_01_fixture_…`),不需要开那个口子。
        // 也不用「数 N 个 ..」:搬文件即静默失配。
        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(thisFile));
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, "unity", "Assets"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            Assert.Fail($"找不到仓库根(自 `{thisFile}` 向上未见到 `unity/Assets`)");
            return null;
        }
    }
}
