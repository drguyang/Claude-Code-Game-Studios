// T22 批十一 · 39 的空行压痕反馈 `UI_Paper_Indent` 落地判据(2026-11-11)
//
// 权威:
//   `design/gdd/casebook.md:394`(翻页声 / 压痕声 / 落笔声 ∈ 行为反馈白名单)
//   `design/gdd/casebook.md:224` / `:303`(空行可聚焦,反馈 = 压痕声 + 行高亮;零提示文案)
//   `design/gdd/skeuomorphic-ui.md:1341`(42 侧 = 纸面压痕声 + 行高亮)
//   **AC-39-12 第一半** —— 此前 42 侧零音频回调,空行落焦只有高亮
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉什么(四件,各一条判据)
// ─────────────────────────────────────────────────────────────────────────────
//   ① 真作者态表里**恰有**该行,且在**末位**(行序 = cue ordinal,插入会静默重编号);
//      同时钉「路由器常量 == 表行 cue 名」(改一处 = 静默失联);
//   ② 运行期表 `TryGetOrdinal` 解析到同一个 ordinal;
//   ③ `RequiredCues` 构建期门真的包含它(否则「表里有行」没有任何东西守);
//   ④ wav 的 `.meta` loadType 与**由 wav 头算出的必需档位**一致。
//
// ⚠️ **读外部夹具的具名声明**(`test-standards.md`):本件读
//   `assets/data/audio_events.json`(作者态真源)、`unity/Assets/Audio/ui_paper_indent_01.wav`
//   与其 `.meta`。理由:**必须测真表 / 真素材** —— 内联字面量会让「表改了夹具仍绿」。
// ⚠️ 零写盘、零随机、零时间依赖。

using System.IO;
using System.Linq;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    [TestFixture]
    public sealed class paper_indent_cue_test
    {
        private const string WavName = "ui_paper_indent_01.wav";

        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static AudioEventTable BoundTable()
        {
            string json = File.ReadAllText(
                Path.Combine(RepoRoot(), "assets", "data", AudioEventTableBinder.SourceFileName));
            return AudioEventTableBinder.Bind(json,
                Path.Combine(RepoRoot(), "unity", "Assets", "Audio")).Table;
        }

        // ── ① 真表里有该行,且在末位;常量与表行同名 ────────────────────────

        [Test]
        public void test_realTable_hasPaperIndentRow_atLastOrdinal()
        {
            AudioEventTable table = BoundTable();

            Assert.That(table.Rows, Is.Not.Null.And.Not.Empty, "前提:真表须非空");
            Assert.That(table.Rows.Count(r => r.Cue == CasebookCueRouter.PaperIndentCueName),
                Is.EqualTo(1),
                "压痕 cue 在全表须恰一行,且与 CasebookCueRouter 的常量逐字一致" +
                "(改其中一处 = 路由器解析不到 ⇒ 静默无声)");

            AudioEventTableRow last = table.Rows[table.Rows.Count - 1];
            Assert.That(last.Cue, Is.EqualTo(CasebookCueRouter.PaperIndentCueName),
                "追加行须在末位 —— 行序即 cue ordinal,插在中间会静默重编号既有 cue");
        }

        // ── ② 运行期解析:ordinal 与行序一致 ────────────────────────────────

        [Test]
        public void test_runtimeTable_resolvesCue_toLastOrdinal()
        {
            AudioEventDataSet data = AudioEventTableBake.Materialize(
                AudioEventTableBake.BakeFromRepo(RepoRoot()));

            Assert.That(data.TryGetOrdinal(CasebookCueRouter.PaperIndentCueName, out int ordinal), Is.True,
                "运行期表解析不到该 cue ⇒ 装配层传 NoOrdinal ⇒ 空行落焦无声(旧态)");

            Assert.That(ordinal, Is.EqualTo(data.RowCount - 1),
                "解析出的 ordinal 须与行序一致");
        }

        // ── ③ RequiredCues 门真的认它 ─────────────────────────────────────

        [Test]
        public void test_requiredCues_gateIncludesPaperIndent()
        {
            Assert.That(AudioEventTableGates.RequiredCues, Does.Contain(CasebookCueRouter.PaperIndentCueName),
                "构建期门不认该 cue ⇒ 「表里有行」没有任何东西守");

            var errors = AudioEventTableGates.ValidateRequiredCues(BoundTable().Rows);
            Assert.That(errors, Is.Empty,
                "真表过不了 RequiredCues 门:\n" + string.Join("\n", errors));
        }

        // ── ④ wav 的 loadType 与头算出的档位一致 ───────────────────────────

        [Test]
        public void test_wav_loadType_matchesHeaderDerivedRequirement()
        {
            string wavDir = Path.Combine(RepoRoot(), "unity", "Assets", "Audio");
            string wav = Path.Combine(wavDir, WavName);
            string meta = wav + ".meta";

            Assert.That(File.Exists(wav), Is.True, "素材不在 ⇒ 构建期 D6 门本应拒(promote 已搬入)");
            Assert.That(File.Exists(meta), Is.True, "缺 .meta ⇒ 导入档位无从判定");

            Assert.That(AudioImportSettingsGate.TryReadWaveInfo(wav, out double seconds, out long decoded,
                out string waveErr), Is.True, waveErr);
            Assert.That(AudioImportSettingsGate.TryReadLoadType(meta, out int loadType, out string metaErr),
                Is.True, metaErr);

            int required = AudioImportSettingsGate.RequiredLoadType(WavName, seconds, decoded);
            Assert.That(loadType, Is.EqualTo(required),
                $"loadType 与头算档位不符({AudioImportSettingsGate.DescribeLoadType(loadType)} vs " +
                $"{AudioImportSettingsGate.DescribeLoadType(required)});时长 {seconds:F3}s");
        }
    }
}