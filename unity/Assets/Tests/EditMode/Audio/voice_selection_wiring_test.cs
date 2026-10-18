// 权威来源:production/epics/audio-system/story-020-voice-variant-selection-wired.md
//   · AC-020-1 [A]:多素材行的取用路径经**真** `VoiceVariantLib.SelectVariant`(禁测试自造选择器)
//   · AC-020-2 [A]:选择键 = (CueId × Tier × 桶 × 患者身份);身份确定性,不同患者不得恒同一条
//   · AC-020-3 [A]:12 个 vo_ 变体全部可达(真种子事件表 + 真 VoiceVariantLib,禁内联清单)
// 缺口来源:`TD-audio-030`(AudioClipLibrary 恒取 Assets[0];VoiceVariantLib 生产零调用)
// GDD:design/gdd/audio-system.md F-44.3(:353)· F-44.6(:409)· AC-44-02
// ADR-018 §一(44 只渲染)· ADR-016 §一(三源不变量 —— 呈现侧同型:禁墙钟 / 禁 UnityEngine.Random)
//
// ⚠️ 本文件的断言宾语**全部是生产类**:`AudioClipLibrary`(真) + `VoiceVariantLib`(真) +
//   真种子 `assets/data/audio_events.json`(经 `AudioEventTableBake` 真烘焙)。
//   **唯一替身** = Addressables 装载缝(`Preload(loader:)`,生产代码自己声明的注入点)——
//   EditMode 无 catalog,不注入则 `LoadedCount` 恒 0(见 runtime_assembly_test 的 NOT-RUN 哨兵)。
//   不存在任何测试内联的选择器:选哪条只由生产代码决定,本文件只读结果。
// ⚠️ 判别力断言:证伪记录见 production/qa/evidence/impl-s20-voice-variant-selection-2026-10-18.md。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using DaYiJingCheng.EditorTools.Bake;           // AudioEventTableBake(真烘焙链路)
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 020 语声变体选择接线(AC-44-02 运行期半边)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class VoiceSelectionWiringTest
    {
        private const string VoiceCue = "Voice_Cough_Damp";
        private const string VoPrefix = "vo_patient_cough_damp_";

        // 注入装载缝造出的 clip 归本夹具所有 ⇒ 经库的释放缝回收(不走 Addressables.Release)。
        private readonly List<AudioClip> _fakes = new List<AudioClip>();
        private AudioClipLibrary _library;

        [TearDown]
        public void TearDown()
        {
            // 释放缝先独立 Destroy 再 Dispose —— 反序(Dispose 内报错)不会短路掉清理。
            _library?.Dispose();
            _library = null;
            for (int i = 0; i < _fakes.Count; i++)
                if (_fakes[i] != null) UnityEngine.Object.DestroyImmediate(_fakes[i]);
            _fakes.Clear();
        }

        // ═══════════ AC-020-1:多素材行走真选择器 ═══════════

        /// <summary>判别力:把取用回退为恒第 0 条 ⇒ 红(全部 12 条断言里至少 1 条不等)。</summary>
        [Test]
        public void test_ac020_1_multiAssetCue_usesRealSelector_notAssetsZero()
        {
            // Arrange
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            int singleOrdinal = SingleAssetOrdinal(table);
            AudioEventRow row = table.Row(voiceOrdinal);
            Assert.That(row.Assets.Length, Is.GreaterThan(1), "前提:咳嗽行是多素材行");

            _library = Library(table);

            // Act + Assert:逐 (tier, 身份) 与**生产选择器**的预期逐条等值
            int offZero = 0;
            for (int tier = 0; tier <= 2; tier++)
            {
                for (int identity = 0; identity < 12; identity++)
                {
                    AudioClip clip = _library.Resolve(voiceOrdinal, voiceOrdinal, tier, identity);
                    string expected = ExpectedAddress(row, tier, identity, singleOrdinal);
                    Assert.That(clip, Is.Not.Null, $"tier={tier} identity={identity} 解析到 null");
                    Assert.That(clip.name, Is.EqualTo(expected),
                        $"tier={tier} identity={identity}:选中 {clip.name},生产选择器预期 {expected}");
                    if (!string.Equals(clip.name, row.Assets[0], StringComparison.Ordinal)) offZero++;
                }
            }
            Assert.That(offZero, Is.GreaterThan(0),
                "全部选中 assets[0] —— 恒第 0 条的行为没被真选择器替换(AC-020-1)");
        }

        /// <summary>判别力:换掉选择器的**取值来源**(签名变化 / 传错 CueId)⇒ 红。</summary>
        [Test]
        public void test_ac020_1_selectorBinding_pinnedOnProductionClass()
        {
            // Arrange
            _library = Library(RealSeedAndVoiceOrdinal().Item1);
            Type lib = typeof(AudioClipLibrary);

            // Assert ①:选择入口存在,取 `(int cue, int tableCueId, int tier, int identity)`
            MethodInfo resolve = lib.GetMethod("Resolve",
                BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            Assert.That(resolve, Is.Not.Null,
                "AudioClipLibrary.Resolve(int,int,int,int) 不存在 —— 选择入口缺失(AC-020-1)");

            // Assert ②:生产 44 二进制（Gameplay.Presentation 里的 44 类型）对 SelectVariant / SelectSlot 有真调用
            Assembly prod = typeof(AudioClipLibrary).Assembly;
            var callers = prod.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public |
                                              BindingFlags.NonPublic | BindingFlags.Instance |
                                              BindingFlags.Static))
                .Where(m => ContainsCall(m, "SelectVariant") || ContainsCall(m, "SelectSlot"))
                .ToList();
            Assert.That(callers, Is.Not.Empty,
                "生产代码对 VoiceVariantLib 的选择函数零调用——「测试单边绿」的假象(AC-020-1 判据宾语 = 生产类)");
        }

        /// <summary>单素材行不受影响;越界 / 未装载仍返回 null(选择面不吞错)。</summary>
        [Test]
        public void test_ac020_1_singleAssetAndInvalidInputs_behaveUnchanged()
        {
            // Arrange
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            int single = SingleAssetOrdinal(table);
            _library = Library(table);

            // Act + Assert:单素材行任意身份恒该条
            string only = table.Row(single).Assets[0];
            for (int identity = 0; identity < 5; identity++)
                Assert.That(_library.Resolve(single, single, 1, identity).name, Is.EqualTo(only),
                    "单素材行不得被选择面改动");

            // 越界 / 表里无行 / 负 ordinal ⇒ null
            Assert.That(_library.Resolve(-1, -1, 0, 0), Is.Null);
            Assert.That(_library.Resolve(table.RowCount, table.RowCount, 0, 0), Is.Null);
            // ⚠️ 变体库单行且 CueId 恒 = cue 自身 ⇒ 「表里无行」路径在生产不可达(烘焙门
            // `ValidateVoiceVariantLib` 的另一半已覆盖);此处只钉 SelectVariant 的 -1 合同本身。
            Assert.That(VoiceVariantLib.SelectVariant(
                    new VoiceVariantLib.VoiceVariantTable(Array.Empty<VoiceVariantLib.VoiceVariantRow>()),
                    9999, 0, VoiceBucket.Weak), Is.EqualTo(-1),
                "表里无行 ⇒ SelectVariant 必须返 -1(不静默取 0)");
        }

        // ═══════════ AC-020-2:身份确定性 + 分布 ═══════════

        /// <summary>同输入两次调用 ⇒ 同变体(确定性,可重放)。</summary>
        [Test]
        public void test_ac020_2_sameIdentity_isDeterministic()
        {
            // Arrange
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            _library = Library(table);

            // Act + Assert
            for (int identity = 0; identity < 20; identity++)
            {
                string a = _library.Resolve(voiceOrdinal, voiceOrdinal, 0, identity)?.name;
                string b = _library.Resolve(voiceOrdinal, voiceOrdinal, 0, identity)?.name;
                Assert.That(a, Is.Not.Null.And.EqualTo(b), $"identity={identity} 两次调用不一致(非确定性)");
            }
        }

        /// <summary>判别力:把身份维度从键里删掉 ⇒ 红(同 Tier 下所有患者恒同一条)。</summary>
        [Test]
        public void test_ac020_2_distinctPatients_doNotAlwaysShareVariant()
        {
            // Arrange:一名患者在多个 Tier 下逐个比 —— 同 (CueId, Tier) 下不同身份必须出现 ≥ 2 个不同变体
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            _library = Library(table);

            for (int tier = 0; tier <= 2; tier++)
            {
                var distinct = new HashSet<string>(StringComparer.Ordinal);
                for (int identity = 0; identity < 8; identity++)
                    distinct.Add(_library.Resolve(voiceOrdinal, voiceOrdinal, tier, identity).name);
                Assert.That(distinct.Count, Is.GreaterThan(1),
                    $"tier={tier}:8 名患者听到同一条咳嗽({string.Join(",", distinct)})—— " +
                    "身份维度没进选择键(AC-020-2)");
            }
        }

        /// <summary>分布判据:8 名患者**全部**覆盖桶内两条(槽宽 4 的 2 条 —— 两层哈希夹出 4 个可达位置)。</summary>
        [Test]
        public void test_ac020_2_distribution_coversSlot()
        {
            // Arrange
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            AudioEventRow row = table.Row(voiceOrdinal);
            _library = Library(table);

            // Act:身份 0–7 在 tier 1 下选出的变体
            var picked = new List<string>();
            for (int identity = 0; identity < 8; identity++)
                picked.Add(_library.Resolve(voiceOrdinal, voiceOrdinal, 1, identity).name);

            // Assert:出现的变体数 = 8 名患者能分到的槽宽(≥ 2 条,且都合法)
            Assert.That(picked.Distinct().Count(), Is.GreaterThanOrEqualTo(2),
                "8 名患者只听到 1 条 —— 分布退化");
            foreach (string name in picked)
            {
                int idx = Array.IndexOf(row.Assets, name);
                Assert.That(idx, Is.GreaterThanOrEqualTo(0), $"选中了行外素材 {name}");
                Assert.That(idx, Is.LessThanOrEqualTo(row.Assets.Length - 1));
            }
        }

        /// <summary>身份哈希的纯函数入口:全域可达(不塌缩到单值)。</summary>
        [Test]
        public void test_ac020_2_identityScalar_spreadsAcrossRange()
        {
            // Arrange
            const int range = 256;
            var seen = new HashSet<int>();

            // Act
            for (int identity = 0; identity < 64; identity++)
                seen.Add(VoiceVariantLib.IdentityScalar(identity, range));

            // Assert
            Assert.That(seen.Count, Is.GreaterThan(32), "64 个身份在 [0,256) 上分布塌缩");
            foreach (int v in seen)
                Assert.That(v, Is.InRange(0, range - 1));
            Assert.That(VoiceVariantLib.IdentityScalar(7, 0), Is.EqualTo(0), "空区间 ⇒ 0,不抛");

            // 三桶全部可达(AC-020-3 的前置:桶输入必须覆盖弱/中/强)
            var buckets = new HashSet<VoiceBucket>();
            for (int identity = 0; identity < 64; identity++)
                buckets.Add(VoiceVariantLib.ComputeBucket((byte)VoiceVariantLib.IdentityScalar(identity, 256)));
            Assert.That(buckets.Count, Is.EqualTo(3), "身份映射只落在部分桶 —— 三桶不可达");
        }

        // ═══════════ AC-020-3:12 个 vo_ 变体全部可达 ═══════════

        /// <summary>判别力:让选择器只覆盖前 6 条 ⇒ 红。</summary>
        [Test]
        public void test_ac020_3_allTwelveVoVariants_reachable()
        {
            // Arrange:真种子 + 真库;清单取自**真表行**(不内联)
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            AudioEventRow row = table.Row(voiceOrdinal);
            Assert.That(row.Assets.Length, Is.EqualTo(12), $"真种子咳嗽行应为 12 条变体,实际 {row.Assets.Length}");
            foreach (string name in row.Assets)
                Assert.That(name, Does.StartWith(VoPrefix), "真种子行内素材应为 vo_patient_cough_damp_*");

            _library = Library(table);

            // Act:枚举身份(取表行数的整数倍 —— 覆盖哈希全域)
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            const int identities = 20000;
            for (int tier = 0; tier <= 2; tier++)
                for (int identity = 0; identity < identities; identity++)
                    reachable.Add(_library.Resolve(voiceOrdinal, voiceOrdinal, tier, identity).name);

            // Assert:12 条逐条可达
            var missing = row.Assets.Where(a => !reachable.Contains(a)).ToList();
            Assert.That(missing, Is.Empty, () => "不可达变体:" + string.Join(",", missing));
            Assert.That(reachable.Count, Is.EqualTo(12));
        }

        // ═══════════ 生产接线:声源实体(患者身份)真的进到选择键 ═══════════

        /// <summary>判别力:把渲染面的解析回退到「只给 ordinal」
        /// (传 <c>cue =&gt; library.Resolve(cue.Cue)</c>)⇒ 红(两位患者取到同一条)。</summary>
        [Test]
        public void test_ac020_2_cueSource_reachesSelector_notJustCueOrdinal()
        {
            // Arrange:真烘焙表 + 假装载缝 + 真池 + 真渲染目标(生产构造 = 整条 cue 解析)
            (AudioEventDataSet table, int voiceOrdinal) = RealSeedAndVoiceOrdinal();
            AudioClipLibrary library = Library(table);
            _library = library;
            using var pool = new AudioSourcePool();
            var target = new AudioSourceRenderTarget(pool, cue => library.ResolveCue(cue));

            // Act:同一 cue、两个不同的声源实体(患者)
            const int patientA = 101, patientB = 202;
            target.PlayOneShot(new AudioCueDto(voiceOrdinal, 128, new Int3(0, 0, 0), patientA, false));
            target.PlayOneShot(new AudioCueDto(voiceOrdinal, 128, new Int3(0, 0, 0), patientB, false));

            // Assert:池里两个源的 clip 名 = 生产选择器按各自身份算出的那条
            string expectedA = library.Resolve(voiceOrdinal, voiceOrdinal, 0, patientA).name;
            string expectedB = library.Resolve(voiceOrdinal, voiceOrdinal, 0, patientB).name;
            Assert.That(pool.SourceForActor(patientA).clip.name, Is.EqualTo(expectedA),
                "患者 A 的声源 clip ≠ 按 Source 选出的变体 —— 身份没到选择器");
            Assert.That(pool.SourceForActor(patientB).clip.name, Is.EqualTo(expectedB));
            Assert.That(target.ClipMissCount, Is.EqualTo(0), "生产路径不得 miss");
        }

        /// <summary>真种子 → 运行期表,并给出咳嗽行 ordinal(**从表里按名解析**,不内联序号)。</summary>
        private static (AudioEventDataSet table, int voiceOrdinal) RealSeedAndVoiceOrdinal()
        {
            AudioEventDataSet table = AudioEventTableBake.Materialize(AudioEventTableBake.BakeFromRepo(RepoRoot()));
            Assert.That(table.TryGetOrdinal(VoiceCue, out int ordinal), Is.True,
                $"真种子事件表缺 {VoiceCue} 行");
            return (table, ordinal);
        }

        /// <summary>库 + 装载缝(每条素材造真 AudioClip,名字 = 素材文件名 ⇒ 可断言选了哪条)。</summary>
        private AudioClipLibrary Library(in AudioEventDataSet table)
        {
            var library = new AudioClipLibrary(table);
            int added = library.Preload(loader: address =>
            {
                AudioClip clip = AudioClip.Create(address, 16, 1, 44100, false);
                _fakes.Add(clip);
                return clip;
            }, internalRelease: clip =>
            {
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            });
            Assert.That(library.LoadedCount, Is.EqualTo(table.RowCount),
                $"装载行数 {library.LoadedCount} ≠ 表 {table.RowCount} 行(共装 {added} 条素材)");
            Assert.That(library.MissingCues, Is.Empty, "真种子素材缺失 —— 前提不成立");
            return library;
        }

        /// <summary>
        /// 预期值 = <b>直接调用生产选择器</b>(与 `AudioClipLibrary.Resolve` 内部同一调用链)复算:
        /// 槽 = SelectVariant(单行变体表投影)，位置 = SelectSlot(槽, N, identity)。
        /// ⚠️ 这是**生产类之间的交叉核对**(装载面 ↔ 选择库),不是测试自造的选择器 ——
        /// 没有第二套本文件自写的哈希 / 槽宽算法。
        /// </summary>
        private static string ExpectedAddress(AudioEventRow row, int tier, int identity, int envOrdinal)
        {
            int n = row.Assets.Length;
            int band = VoiceVariantLib.BucketBand(n);
            var weak = Enumerable.Range(0, band).ToArray();
            var medium = Enumerable.Range(band, band).ToArray();
            var strong = Enumerable.Range(2 * band, n - 2 * band).ToArray();
            var variantRow = new VoiceVariantLib.VoiceVariantRow(envOrdinal, 0, weak, medium, strong);
            var variantTable = new VoiceVariantLib.VoiceVariantTable(new[] { variantRow });

            VoiceBucket bucket = VoiceVariantLib.ComputeBucket(
                (byte)VoiceVariantLib.IdentityScalar(identity, 256));
            int start = VoiceVariantLib.SelectVariant(variantTable, envOrdinal, tier, bucket);
            int slot = VoiceVariantLib.SelectSlot(start, n, identity);
            return row.Assets[slot];
        }

        /// <summary>取一个**单素材**行的 ordinal —— 供「单素材不受影响」判据(真种子里的呼吸 / 世界行)。</summary>
        private static int SingleAssetOrdinal(in AudioEventDataSet table)
        {
            for (int i = 0; i < table.RowCount; i++)
            {
                string[] assets = table.Row(i).Assets;
                if (assets != null && assets.Length == 1) return i;
            }
            Assert.Fail("真种子表内找不到单素材行");
            return -1;
        }

        /// <summary>方法体内是否出现对指定静态函数的调用(`call` 指令 + 成员名)。</summary>
        private static bool ContainsCall(MethodInfo method, string calleeName)
        {
            byte[] il;
            try
            {
                MethodBody body = method.GetMethodBody();
                if (body == null) return false;
                il = body.GetILAsByteArray();
            }
            catch (Exception) { return false; }
            if (il == null) return false;

            for (int i = 0; i < il.Length - 4; i++)
            {
                byte op = il[i];
                if (op != 0x28 && op != 0x6F) continue;       // call / callvirt
                int token = BitConverter.ToInt32(il, i + 1);
                try
                {
                    MemberInfo target = method.Module.ResolveMember(token,
                        method.DeclaringType?.GetGenericArguments(), method.GetGenericArguments());
                    if (target is MethodInfo mi && mi.Name == calleeName) return true;
                }
                catch (Exception) { /* 不能解析的 token 跳过 */ }
            }
            return false;
        }

        private static string RepoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));
    }
}
