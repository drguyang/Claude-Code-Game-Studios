// `TD-listen-01` **L-2** 的切档缝判据(2026-10-29 用户裁定「修」)。
//
// ── 病因 ────────────────────────────────────────────────────────────────────
// `CompositionRoot` 的 `queryLevel` 原是**局部常量 1**且全库零回写
// ⇒ `DiagnosisReadFloorEvaluator.TierIndex(1) = 0`(`1 < SLOT_BOUNDS[0] = 10`)
// ⇒ 听诊**恒最低档** ⇒ `AC-44-02` 的三档 forced-choice **没有第二 / 第三档可听**。
//
// ── 本轮的修法 ──────────────────────────────────────────────────────────────
// 装配期**可选形参** `diagnosisQueryLevelOverride`(不越界到 30 的生产契约):
//   · 传 null(109 处既有调用方的默认)⇒ 取常量 1,**逐位不变**;
//   · 传值 ⇒ 该值成为 `queryLevel` ⇒ 经 `TierIndex` 变成真正的档序。
//
// ── 判据为什么落在 `AudioTierDriver.CurrentTier` ────────────────────────────
// `ExamineDriver` 不暴露 `_queryLevel`(私有),而 `AudioTierDriver` 的 `CurrentTier`
// 是**听诊档位的直接可观测面** —— 它正是 AC-44-02 听测时耳朵听到的那个档。
// 用「档真的变了」作判据,而不是「形参被赋值了」,才不是自证。
//
// 权威来源:`docs/tech-debt-register.md` `TD-listen-01` L-2 解除条件;
//   `CompositionRoot` 的 `DefaultDiagnosisQueryLevel` 注;`DiagnosisTuning.SlotBounds = {10,20,50}`。

using System;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Gameplay.Presentation.Diagnosis;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Audio;

namespace DaYiJingCheng.Tests.Boot
{
    /// <summary>L-2 切档缝:档序真的随覆盖值变,且未给时逐位不变。</summary>
    public sealed class DiagnosisQueryLevelOverrideTest
    {
        private const string MixerAssetPath = "Assets/Audio/DaYiJingCheng.mixer";

        /// <summary>`PlayerBusVolumeInitializer.MixerResolver` 是**静态可变缝** ⇒ 必须复原,
        /// 否则污染后续夹具(承 `runtime_assembly_test` 同款纪律)。</summary>
        private Func<AudioMixer> _savedResolver;

        [SetUp]
        public void SaveResolver() => _savedResolver = PlayerBusVolumeInitializer.MixerResolver;

        [TearDown]
        public void RestoreResolver() => PlayerBusVolumeInitializer.MixerResolver = _savedResolver;

        /// <summary>造一份够用的 audio seams(只为让 `AudioTierDriver` 有 `tier_map` 可读)。
        /// <para>⚠️ 读**盘上真产物** —— 与生产同一条装载路径,不自造 `tier_map`
        /// (自造表会让本测变成「测我自己的夹具」)。</para></summary>
        private static AudioAssemblySeams Seams()
        {
            string cooked = System.IO.Path.Combine(
                RepoRoot(), "unity", "Assets", "DataCooked", "audio_events.cooked.bytes");
            Assert.That(System.IO.File.Exists(cooked), Is.True, $"盘上产物缺失:{cooked}");
            AudioEventDataSet table = AudioEventCookedCodec.Read(
                System.IO.File.ReadAllBytes(cooked));
            return new AudioAssemblySeams { Table = table };
        }

        private static string RepoRoot(
            [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        /// <summary>档位驱动须有非 null 的 mixer 才装配(见 `CompositionRoot` 的
        /// `if (tierTable.HasValue && tierTable.Value.TierMap != null)` 段 —— 其中
        /// `AudioMixerParameterSink` 的构造对 null mixer 抛 ANE)⇒ 本测给真混音器资产。</summary>
        private static void BindRealMixer()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            Assert.That(mixer, Is.Not.Null,
                $"混音器资产装载失败:{MixerAssetPath}(跑 MixerAssetGenerator.Create batch 生成)");
            PlayerBusVolumeInitializer.MixerResolver = () => mixer;
        }

        /// <summary>驱动须先 `Tick` 一次才把 `CurrentTier` 从 -1 推到位。</summary>
        private static int TierAfterAssemble(int? overrideLevel)
        {
            BindRealMixer();
            CompositionRootServices bag = CompositionRoot.Assemble(
                audio: Seams(), diagnosisQueryLevelOverride: overrideLevel);
            Assert.That(bag.AudioTierDriver, Is.Not.Null,
                "有 audio 表 + 可解析混音器 ⇒ 档位驱动必须装配(否则本测的判据面不存在)");

            bag.AudioTierDriver.Tick(1.0);
            return bag.AudioTierDriver.CurrentTier;
        }

        /// <summary>**未给覆盖**(null)⇒ 档序 = `TierIndex(1)` = 0 —— 与改动前**逐位相同**。
        /// <para>这条钉的是「零行为变化」:109 处既有调用方全不传 ⇒ 必须还是最低档。</para></summary>
        [Test]
        public void test_noOverride_keepsLegacyLowestTier()
        {
            Assert.That(DiagnosisReadFloorEvaluator.TierIndex(1), Is.EqualTo(0),
                "前置:常量 1 经 SLOT_BOUNDS{10,20,50} 必须落在档 0(否则本债病因不成立)");

            Assert.That(TierAfterAssemble(null), Is.EqualTo(0),
                "未给覆盖 ⇒ 档序须仍为 0(否则 109 处既有装配的行为被改了)");
        }

        /// <summary>**给覆盖** ⇒ 档序真的变 —— 这是 L-2 的核心判据。
        /// <para>三个值各对应一档:`1 → 0` · `10 → 1` · `20 → 2`。</para>
        /// <para><b>判别力</b>:把 `diagnosisQueryLevelOverride ?? DefaultDiagnosisQueryLevel`
        /// 改回 `int queryLevel = 1`(即无视形参)⇒ 三条全红(全都落回档 0)。</para></summary>
        [Test]
        public void test_override_reachesAllThreeTiers()
        {
            // 三档的**技能等级**取值(不是档序)—— SLOT_BOUNDS = {10, 20, 50}。
            Assert.That(TierAfterAssemble(1), Is.EqualTo(0), "技能等级 1 ⇒ 档 0(低)");
            Assert.That(TierAfterAssemble(10), Is.EqualTo(1), "技能等级 10 ⇒ 档 1(中)");
            Assert.That(TierAfterAssemble(20), Is.EqualTo(2), "技能等级 20 ⇒ 档 2(高)");
        }

        /// <summary>**传档序会全部落档 0** —— 这是本债原来的故障形态,必须留成反面教材。
        /// <para>⚠️ 本测不是「测缺陷」,是钉住「形参语义 = 技能等级,不是档序」这件事:
        /// 若有人把 `DYJC_DIAG_QUERY_LEVEL` 文档改成「档序 0–2」,这条会说明为什么错。</para></summary>
        [Test]
        public void test_overrideIsSkillLevel_notTierIndex()
        {
            Assert.That(TierAfterAssemble(0), Is.EqualTo(0), "技能等级 0 ⇒ 档 0");
            Assert.That(TierAfterAssemble(2), Is.EqualTo(0),
                "技能等级 **2** ⇒ 仍档 0 —— 证明形参不是档序(否则会是档 2)。" +
                "听测要听高档须传 **20**,不是 2");
        }
    }
}
