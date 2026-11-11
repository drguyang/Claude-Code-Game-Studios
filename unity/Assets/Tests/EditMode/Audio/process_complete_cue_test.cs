// T22 批九 · 18 的起货锚点 `sfx_process_complete` 落地判据(2026-11-11)
//
// 权威:
//   `design/gdd/audio-system.md:833`(三支 `sfx_process_*`,发射方 = 18)
//   `design/gdd/processing.md:597-598`(cue 命名)与 **AC-18-21**(完成 ⇒ cue 发射)
//   `design/assets/specs/audio-material-manifest.md` §一之三(本 cue 的冻结行)
//   `TD-building-05` 的解除判据 ② —— 人工腿需要「起货**可被听见**」,而此前事件表零 `sfx_process_*` 行
//
// ─────────────────────────────────────────────────────────────────────────────
// 本件钉什么(四件,各一条判据)
// ─────────────────────────────────────────────────────────────────────────────
//   ① 真作者态表里**恰有**该行,且它在**末位**(行序 = cue ordinal,插入会静默重编号);
//   ② 运行期表 `TryGetOrdinal` 解析到**同一个** ordinal(与驱动常量逐字一致);
//   ③ `RequiredCues` 构建期门**真的包含**它(否则「表里有行」这条永远没人守);
//   ④ wav 的 `.meta` loadType 与**由 wav 头算出的必需档位**一致(DecompressOnLoad = 0)。
//
// ⚠️ **读外部夹具的具名声明**(`test-standards.md`):本件读
//   `assets/data/audio_events.json`(作者态真源)、`unity/Assets/Audio/sfx_process_complete.wav`
//   与其 `.meta`。理由:**必须测真表 / 真素材** —— 内联字面量会让「表改了夹具仍绿」,
//   而本件要守的正是「行在、 ordinal 对、门认、档位对」这四件真实装配条件。
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
    public sealed class process_complete_cue_test
    {
        private const string CueName = "sfx_process_complete";
        private const string WavName = "sfx_process_complete.wav";

        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private static AudioEventTable BoundTable()
        {
            string json = File.ReadAllText(
                Path.Combine(RepoRoot(), "assets", "data", AudioEventTableBinder.SourceFileName));
            // assetRoot = 素材根(D6「素材存在」谓词的输入;与烘焙菜单同一处口径)
            return AudioEventTableBinder.Bind(json,
                Path.Combine(RepoRoot(), "unity", "Assets", "Audio")).Table;
        }

        // ── ① 真表里有该行,且在末位 ───────────────────────────────────────

        [Test]
        public void test_realTable_hasProcessCompleteRow_atLastOrdinal()
        {
            AudioEventTable table = BoundTable();

            Assert.That(table.Rows, Is.Not.Null.And.Not.Empty, "前提:真表须非空");

            AudioEventTableRow last = table.Rows[table.Rows.Count - 1];
            Assert.That(last.Cue, Is.EqualTo(CueName),
                "起货行须在**末位** —— 行序即 cue ordinal,插在中间会静默重编号既有 cue");

            Assert.That(table.Rows.Count(r => r.Cue == CueName), Is.EqualTo(1),
                "该 cue 在全表恰一行(重复会让 TryGetOrdinal 取第一个,第二个成为死行)");
        }

        // ── ② 运行期解析:ordinal 与驱动常量同一名 ─────────────────────────

        [Test]
        public void test_runtimeTable_resolvesCue_toLastOrdinal()
        {
            AudioEventDataSet data = AudioEventTableBake.Materialize(
                AudioEventTableBake.BakeFromRepo(RepoRoot()));

            Assert.That(data.TryGetOrdinal(CueName, out int ordinal), Is.True,
                "运行期表解析不到该 cue ⇒ 装配层传 NoOrdinal ⇒ 起货**不发声**(旧态)");

            Assert.That(ordinal, Is.EqualTo(data.RowCount - 1),
                "解析出的 ordinal 须与行序一致");
            Assert.That(CueName, Is.EqualTo(ProcessingCompletionDriver.CompleteCueName),
                "表行的 cue 名与驱动常量逐字一致 —— 改成任一单独都会静默失联");
        }

        // ── ③ RequiredCues 门真的认它 ─────────────────────────────────────

        [Test]
        public void test_requiredCues_gateIncludesProcessComplete()
        {
            Assert.That(AudioEventTableGates.RequiredCues, Does.Contain(CueName),
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

            Assert.That(File.Exists(wav), Is.True, "素材不在 ⇒ 构建期 D6 门本应拒(先跑的 promote 已搬入)");
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
