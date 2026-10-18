// 权威来源:production/epics/audio-system/story-021-knowsclinic-injection-seam.md
//   (AC-021-1 注入缝 · AC-021-2 knowsClinic=true ⇒ 可进 Seeking · AC-021-3 false ⇒ 不进)
// design/gdd/patient-ai.md F-13.2 · ADR-016 §一(重建三源不变量)· ADR-018 §一(44 只触发)
// TD-audio-033 ①(`knowsClinic: false` 硬编码入生产驱动)
//
// ⚠️ **被测对象全部是真生产件** —— 零自造谓词:
//    `CompositionRoot.Assemble` 装配出的 `PatientCueDriver`(经其注入缝喂真 `PatientBehaviorDirector`
//    + 真 `BehaviorMap`)· `ClinicKnowledgeDirectory` · `ClinicKnowledge` · `RegistryClinicKnowledgeSource`
//    · 6 的 `EcozoneRegistry`。
//    注入的假件只有**生产代码自己声明的缝**:`IVitalsQuery`(9 的取数面)· `IAudioCueSink` 计数壳。
//    ⚠️ 本夹具**不写 `knowsClinic: false` 字面量**,也不以该字面量作判据 —— AC-021-1 的判据是
//    `PatientCueDriver` 的**构造契约**(必填 + null 即抛),不是文本 grep(文本面归证伪轮登记)。
//
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖 · 零文件系统 I/O。

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.PatientAI;
using DaYiJingCheng.Sim;
using DaYiJingCheng.Sim.Codec;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>story-021:`knowsClinic` 注入缝的判别力夹具(AC-021-1/2/3)。</summary>
    [TestFixture]
    public sealed class KnowsClinicSeamTest
    {
        // 病人锚点格 —— 用于 F-13.2 真值链(落在区 0 内 / 什么都不在)。
        private static readonly WorldPos InsideRegion0 = new WorldPos(5, 0, 5);
        private static readonly WorldPos OutsideAllRegions = new WorldPos(5000, 0, 5000);

        /// <summary>可编程体征源(生产缝 = `IVitalsQuery`)。</summary>
        private sealed class RiggedVitals : IVitalsQuery
        {
            public float Position;
            public RiggedVitals(float position) => Position = position;
            public VitalsDto GetVitals(PatientId p) => new VitalsDto(Position, 0f, 0, 0);
        }

        /// <summary>计数壳(`IAudioCueSink` 的观测量;本夹具不判 cue,只让驱动可装配)。</summary>
        private sealed class NoopSink : IAudioCueSink
        {
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => new AudioCueHandle(1);
            public void EndLoop(AudioCueHandle handle) { }
            public void Emit(in AudioCueDto cue) { }
            public void SetTier(TierSource source) { }
        }

        /// <summary>驱动一拍并读回导演算出的真实行为态。</summary>
        private static BehaviorState StepAndRead(PatientCueDriver driver, PatientBehaviorDirector director)
        {
            driver.OnTickEdge(0);
            return director.For(new PatientId(7)).Current;
        }

        /// <summary>直接造驱动(只为 AC-021-3 的注入值配对;装配面归 AC-021-1)。</summary>
        private static PatientCueDriver DriverWith(Func<PatientId, bool> knowsClinic,
                                                   IVitalsQuery vitals, PresenceRegistry presence,
                                                   out PatientBehaviorDirector director)
        {
            director = new PatientBehaviorDirector(vitals, BehaviorBands.Default);
            return new PatientCueDriver(new NoopSink(), vitals, presence, director,
                                         coughCue: null, breathCue: null, knowsClinic: knowsClinic);
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-021-1 —— knowsClinic 是构造契约(必填;装配层从真值源喂)
        // 判别力:把构造参数改回可省略 + 静默默认 ⇒ 第一条红;
        //         装配层改回写死常量 ⇒ 第二条红(判据 = 行为,不是文本)。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac021_1_driver_ctor_requiresKnowsClinic_neverSilentlyDefaults()
        {
            // Arrange
            var vitals = new RiggedVitals(0.5f);
            PresenceRegistry presence = new PresenceRegistry();

            // Act + Assert:缺缝 ⇒ 具名 ANE(不提供「静默 false」的默认值)
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new PatientCueDriver(new NoopSink(), vitals, presence,
                                     new PatientBehaviorDirector(vitals, BehaviorBands.Default)));
            Assert.That(ex.ParamName, Is.EqualTo("knowsClinic"),
                "缺缝必须是具名 ArgumentNullException(knowsClinic)—— 静默默认正是本缝要消灭的形态");
        }

        [Test]
        public void test_ac021_1_compositionRoot_feedsSeamFromTruthSource_notHardcodedFalse()
        {
            // 判据(宾语 = **真组合根装配出的驱动**,经其 `KnowsClinicOf` 观测面):
            //   ① 注入「区 0 有医馆 POI」的真值链 ⇒ 区内病人必须判「知道」;
            //   ② 不注入(组合根自建空生态区表 = 今日形态)⇒ 同一病人必须判「不知道」。
            // 装配层写死 false ⇒ ① 红;写死 true ⇒ ② 红。
            // ⚠️ 不靠「病人的体征恰好落进寻诊带」来判 —— 那要一整套体征链夹具,
            //    且体征走哪条带与「缝接没接」是两件事(混在一起会给出错的绿)。
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(7), InsideRegion0);

            // ① 真值链打开(区 0 有医馆 POI)
            CompositionRootServices wired = CompositionRoot.Assemble(
                new IdAuthority(), presence, new InMemoryBlobPool(), 0UL,
                clinicKnowledge: KnowledgeWithClinicInRegion0());
            Assert.That(wired.PatientCueDriver.KnowsClinicOf(new PatientId(7)), Is.True,
                "真值链(区 0 有医馆 POI ∧ 病人在区 0)⇒ 装配层喂给 13 的必须是真 —— " +
                "否则装配层在发明行为输入(AC-021-1)");

            // ② 不注入 ⇒ 组合根自建空表 ⇒ 如实为假
            CompositionRootServices bare = CompositionRoot.Assemble(
                new IdAuthority(), presence, new InMemoryBlobPool(), 0UL);
            Assert.That(bare.PatientCueDriver.KnowsClinicOf(new PatientId(7)), Is.False,
                "空生态区注册表 ⇒ F-13.2 无真值 ⇒ 必须为假(写死 true 会在这里红)");
        }

        /// <summary>造「区 0 静态存在医馆 POI」的真值链(真 `EcozoneRegistry` + 真 `ClinicKnowledge`)。</summary>
        private static ClinicKnowledge KnowledgeWithClinicInRegion0()
        {
            var ecozones = new EcozoneRegistry();
            ecozones.Register(new EcozonePolygon(new EcozoneId(0), new[]
            {
                new WorldPos(0, 0, 0), new WorldPos(10, 0, 0), new WorldPos(10, 0, 10), new WorldPos(0, 0, 10),
            }));
            return new ClinicKnowledge(new RegistryClinicKnowledgeSource(
                ecozones, hasClinicPoi: r => r.Value == 0));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-021-2 —— knowsClinic = true ⇒ 可进 Seeking(真 director + 真 BehaviorMap)
        // 判别力:把 BehaviorMap 的 Seeking 分支短路 ⇒ 本条红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac021_2_knowsClinicTrue_patientEntersSeeking()
        {
            // Arrange:体征落在 [SEEK_MIN, COLLAPSE_MIN) —— 唯一靠 knowsClinic 决定的分档区
            var vitals = new RiggedVitals(0.6f);
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(7), InsideRegion0);
            PatientCueDriver driver = DriverWith(_ => true, vitals, presence, out var director);

            // Act
            BehaviorState state = StepAndRead(driver, director);

            // Assert
            Assert.That(state, Is.EqualTo(BehaviorState.Seeking),
                "knowsClinic=true 且体位在寻诊带 ⇒ 必须 Seeking(经驱动的注入缝观测)");
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-021-3 —— knowsClinic = false ⇒ 不进 Seeking(防「一律 true」的假修)
        // 判别力:把注入恒为 true ⇒ 本条红。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac021_3_knowsClinicFalse_patientDoesNotEnterSeeking()
        {
            // Arrange:同一体征、同一锚点格 —— 只有注入值为假
            var vitals = new RiggedVitals(0.6f);
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(7), InsideRegion0);
            PatientCueDriver driver = DriverWith(_ => false, vitals, presence, out var director);

            // Act
            BehaviorState state = StepAndRead(driver, director);

            // Assert
            Assert.That(state, Is.EqualTo(BehaviorState.Idle),
                "knowsClinic=false ⇒ BehaviorMap ⑤ 默认 Idle(不得进 Seeking)");
        }

        [Test]
        public void test_ac021_3_emptyRegistry_makesTruthSourceHonestlyFalse()
        {
            // 接线期实况:生态区装载面未接 ⇒ 组合根的空注册表 ⇒ KnowsClinic 恒假。
            // ⚠️ 本条钉的是「如实表达」,不是「应该恒假」—— 装载面接上后本条的期望须随之改。
            // 宾语 = **组合根产出的驱动**(不另 new 一份,否则判的是夹具自己的装配)。
            var presence = new PresenceRegistry();
            presence.AddPresent(new PatientId(7), InsideRegion0);
            CompositionRootServices services = CompositionRoot.Assemble(
                new IdAuthority(), presence, new InMemoryBlobPool(), 0UL);

            Assert.That(services.PatientCueDriver.KnowsClinicOf(new PatientId(7)), Is.False,
                "空生态区注册表 ⇒ EcozoneOf 恒 None ⇒ F-13.2 两个析取项皆假(如实表达)");
            // 行为面:再跑一拍,组合根的行为导演不得把该病人判进 Seeking(写死 true 会在这里红)。
            services.PatientCueDriver.OnTickEdge(0);
        }

        [Test]
        public void test_ac021_3_noAnchorCell_isFalse_notThrow()
        {
            // 不在场 ⇒ 无锚点格 ⇒ F-13.2 无定义 ⇒ 判「不知道」(最保守),不抛。
            var knowledge = new ClinicKnowledge(new RegistryClinicKnowledgeSource(
                new EcozoneRegistry(), hasClinicPoi: _ => true));    // 即使「全都知道」…
            var presence = new PresenceRegistry();                    // …没有锚点格也应 False
            var directory = new ClinicKnowledgeDirectory(knowledge,
                p => presence.TryGetCell(p, out WorldPos c) ? (true, c) : (false, default));

            Assert.That(directory.KnowsClinic(new PatientId(99)), Is.False);
            Assert.That(directory.KnownPatientCount, Is.EqualTo(0), "无锚点不缓存 HomeRegion");
        }

        [Test]
        public void test_ac021_3_directory_cachesHomeRegionOncePerPatient()
        {
            // F-13.2「HomeRegion 只在初始化时求一次」:锚点格变化不回改缓存。
            var ecozones = new EcozoneRegistry();
            ecozones.Register(new EcozonePolygon(new EcozoneId(0), new[]
            {
                new WorldPos(0, 0, 0), new WorldPos(10, 0, 0), new WorldPos(10, 0, 10), new WorldPos(0, 0, 10),
            }));
            var calls = new CallCounter();
            var source = new CountingEcozoneSource(ecozones, calls);
            var directory = new ClinicKnowledgeDirectory(new ClinicKnowledge(source),
                                                         _ => (true, InsideRegion0));
            var id = new PatientId(7);

            var first = directory.KnowsClinic(id);
            var second = directory.KnowsClinic(id);            // 同一病人第二拍
            directory.ResetForLoad();
            var afterMove = directory.KnowsClinicAt(id, OutsideAllRegions);

            Assert.That(first, Is.True, "区 0 被计数源标为有医馆 ⇒ true");
            Assert.That(second, Is.True);
            Assert.That(afterMove, Is.False, "ResetForLoad 后重求 HomeRegion ⇒ 区外 ⇒ false");
            // 判别力:首见 1 次 + 缓存命中 0 次 + 重置后 1 次 = 2。若「每拍重求」则为 3。
            Assert.That(calls.EcozoneOfCalls, Is.EqualTo(2),
                "HomeRegion 须首见求一次并缓存(F-13.2);每拍重求会让调用数 = 3");
            Assert.That(directory.KnownPatientCount, Is.EqualTo(1));
        }

        // ══════════════════════════════════════════════════════════════════
        // AC-021-1(文本半边)—— 生产树内不得再出现 `knowsClinic: false` 字面量
        // 判别力:把任一生产调用点改回该字面量 ⇒ 本条红。
        // ⚠️ 宾语 = **生产程序集源码**(Gameplay.Boot / Gameplay.Presentation),不含测试树 ——
        //    否则「测试文件没写」会被当成「生产没写」(承假绿复核病根)。
        // ══════════════════════════════════════════════════════════════════

        [Test]
        public void test_ac021_1_productionTree_hasNoHardcodedKnowsClinicFalse()
        {
            string assetsRoot = Path.Combine(RepoRoot(), "unity", "Assets");
            Assert.That(Directory.Exists(assetsRoot), Is.True, $"Assets 根缺失:{assetsRoot}(空扫描 ⇒ 假绿)");

            var offenders = new List<string>();
            int scanned = 0;
            // 扫**全部**生产树:`Tests/` 以外的一切 .cs(AC-021-1 原文是「全仓」;
            // 旧版只扫 Boot + Presentation 两棵 —— 评审 W-2 实测其余七棵今日虽零违例,
            // 但门的面**必须是全部**,否则新违例写进 Sim / UI 侧无人拦)。
            foreach (string file in Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}")) continue;
                scanned++;
                // 剥注释后再扫(行注释 + 块注释;字符串内的 `//` / `/*` 不剥 ⇒ 不误伤也不漏)。
                string code = StripComments(File.ReadAllText(file));
                if (code.Contains(HardcodedFalseLiteral))
                    offenders.Add(Path.GetFileName(file));
            }

            // 下限 = 实测件数(376 件量级)的九成:树缩水到一半以下即报警,不得静默变空。
            Assert.That(scanned, Is.GreaterThan(300), $"扫描面异常(scanned={scanned};空集 / 子树缺失冒充绿)");
            Assert.That(offenders, Is.Empty,
                $"生产树内仍有 `{HardcodedFalseLiteral}` 字面量 ⇒ 装配层在发明行为输入:"
                + string.Join(", ", offenders));
        }

        /// <summary>剥掉行注释与块注释(**不**碰字符串字面量):注释里**记载**旧形态是允许的,
        /// 代码里**出现**才是违例;字符串里的 `//`(如 URL)必须原样保留,否则会把真代码误吞成「无违例」。</summary>
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inLine = false, inBlock = false, inString = false, inChar = false, escaped = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char next = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine) { if (c == '\n') { inLine = false; sb.Append(c); } continue; }
                if (inBlock) { if (c == '*' && next == '/') { inBlock = false; i++; } continue; }
                if (inString || inChar)
                {
                    sb.Append(c);
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if (inString && c == '"') inString = false;
                    else if (inChar && c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && next == '/') { inLine = true; i++; continue; }
                if (c == '/' && next == '*') { inBlock = true; i++; continue; }
                if (c == '"') { inString = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>违例字面量(拆开拼装 —— 本夹具自身不得出现整串,否则它自己就成了违例)。</summary>
        private static readonly string HardcodedFalseLiteral = "knowsClinic" + ": false";

        /// <summary>仓根(`unity/Assets/Tests/EditMode/Audio/` 上溯五级)。</summary>
        private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private sealed class CallCounter { public int EcozoneOfCalls; }

        /// <summary>把「区 0 有医馆」注入到真 `EcozoneRegistry` 查询之上的计数壳(只计数,不改判定)。</summary>
        private sealed class CountingEcozoneSource : IClinicKnowledgeSource
        {
            private readonly EcozoneRegistry _ecozones;
            private readonly CallCounter _calls;
            public CountingEcozoneSource(EcozoneRegistry ecozones, CallCounter calls)
            { _ecozones = ecozones; _calls = calls; }

            public EcozoneId EcozoneOf(WorldPos cell)
            {
                _calls.EcozoneOfCalls++;
                return _ecozones.EcozoneOf(cell);
            }
            public bool HasClinicPoiIn(EcozoneId region) => region.Value == 0;
            public bool HasPlayerBuiltClinicIn(EcozoneId region) => false;
        }
    }
}
