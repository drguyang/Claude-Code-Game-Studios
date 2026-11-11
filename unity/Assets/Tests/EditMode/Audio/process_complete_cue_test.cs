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
//      🔁 2026-11-11(T22 批十一):「末位」这半句**已不成立** —— 本批在它之后追加了
//      `UI_Paper_Indent`(39 空行压痕声)。现行判据 = **追加序不位移**(起货行仍是 ordinal 19),
//      且**最新追加行**在末位。原文保留。
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
        public void test_realTable_hasProcessCompleteRow_atItsAppendOrdinal()
        {
            AudioEventTable table = BoundTable();

            Assert.That(table.Rows, Is.Not.Null.And.Not.Empty, "前提:真表须非空");

            // 🔁 2026-11-11(T22 批十一):原断言是「起货行在**末位**」。本批在它之后追加了
            //    `UI_Paper_Indent`(39 的空行压痕声),故「末位」不再成立 —— 但真正要守的
            //    不变量不是「末位」,是**追加序不改既有 ordinal**:起货行仍是追加时的第 20 行
            //    (ordinal 19)。若有人在它**之前**插入一行,本断言当场红 —— 那正是要抓的
            //    「静默重编号」。
            int appendIndex = -1;
            for (int i = 0; i < table.Rows.Count; i++)
                if (table.Rows[i].Cue == CueName) { appendIndex = i; break; }
            Assert.That(appendIndex, Is.EqualTo(19),
                "起货行的 ordinal 必须仍是 19(追加不位移;插入才位移)—— 行序即 cue ordinal");
            Assert.That(table.Rows[table.Rows.Count - 1].Cue, Is.EqualTo("UI_Paper_Indent"),
                "最新追加的行须在末位(2026-11-11 T22 批十一:UI_Paper_Indent)");

            Assert.That(table.Rows.Count(r => r.Cue == CueName), Is.EqualTo(1),
                "该 cue 在全表恰一行(重复会让 TryGetOrdinal 取第一个,第二个成为死行)");
        }

        // ── ② 运行期解析:ordinal 与驱动常量同一名 ─────────────────────────

        [Test]
        public void test_runtimeTable_resolvesCue_toItsAppendOrdinal()
        {
            AudioEventDataSet data = AudioEventTableBake.Materialize(
                AudioEventTableBake.BakeFromRepo(RepoRoot()));

            Assert.That(data.TryGetOrdinal(CueName, out int ordinal), Is.True,
                "运行期表解析不到该 cue ⇒ 装配层传 NoOrdinal ⇒ 起货**不发声**(旧态)");

            Assert.That(ordinal, Is.EqualTo(19),
                "解析出的 ordinal 须是追加时的位置(19)—— 后续追加不得把它挤走");
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
