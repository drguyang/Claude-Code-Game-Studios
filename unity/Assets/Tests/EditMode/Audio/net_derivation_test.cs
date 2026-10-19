// 权威来源:production/epics/audio-system/story-007-net-divergence-remote-derivation.md
//   (AC-44-07 静态半 / AC-44-D1 / AC-44-D7 BLOCKING 静态三腿 + AC-44-D10 锚点纪律)
//   · 007 只交付**静态半**:重排差分 / 消费纪律 / 锚点纪律 / 载荷纯度 / 默认档位语义 /
//     EndLoop 自评兜底;**4 人双实例对拍 = BLOCKED-BY-45**(P1b)不在本 story 断言面。
//   · ADR-018 §五(联机音频:分叉的只有音)· ADR-001 §一之二(IPositionalChannel + 消费纪律 1-3)
//     + §一之三 裁决二(EndLoop 兜底 = 呈现侧义务)· ADR-005(客户端持流副本)·
//     ADR-008(EventOrderKey 跨流全序键)· ADR-012(整数哈希 SplitMix64)
//
// ⚠️ **2026-10-16 S14-2 改造(假绿复评修复轮)**:本文件此前 19 测中 🔴 假 10 ——
//    核心判据(重排差分哈希)的被测物是**测试自己写的折叠函数** `DeriveCueHash`,
//    与 44 的派生器无关(评审原件 `review-audio-epic-fake-green-2026-10-16.md` §2.1)。
//    本轮把全部自造物换成**生产类**:
//      `DeriveCueHash` → `CueEmitDeriver` · `FakeAnchorGate` → `AnchorGate` ·
//      `FakeEndLoopWatchdog`/`FakeLoopProgress` → `EndLoopWatchdog`/`LoopProgress` ·
//      `TierRecordingSink` → `CueRenderSink`(真 `IAudioCueSink` 生产实现)。
//    保留的两处非生产物**刻意**:`ChannelRefFixture` / `RandomBodyFixture`(负例夹具)。
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/net_derivation_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**;账本侧由 README 互链。
// ⚠️ 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)。
// ⚠️ 测试纪律:arrange/act/assert · 无随机 · 无时间依赖(夹具用显式计数字段)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using DaYiJingCheng.EditorTools.Gates;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Tests.Unit.Audio
{
    /// <summary>Story 007 联机分叉与远端派生(静态半)的 EditMode 测试。</summary>
    [TestFixture]
    internal sealed class NetDerivationTest
    {
        // ══════════════ 基础设施 ══════════════

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ 仓库根 = 5 层。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        /// <summary>生产 44 类型:只取 Gameplay.Presentation 装配(测试装配内的 fixture
        /// 刻意不入生产断言面 —— 负例走显式传入)。</summary>
        private static List<Type> ProductionAudioTypes()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
            Assert.That(asm, Is.Not.Null,
                "Gameplay.Presentation 必须已加载(EditMode.asmdef 引用它)");
            try
            {
                return asm.GetTypes().Where(t => t.Namespace != null &&
                    AssemblyGates.IsInNamespacePrefix(t.Namespace, Prefix)).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null && t.Namespace != null &&
                    AssemblyGates.IsInNamespacePrefix(t.Namespace, Prefix)).ToList();
            }
        }

        // ══════════════ AC-44-07 —— 默认档位语义(静态半)══════════════

        /// <summary>AC-44-07(静态半):`SetTier` 收到 `TierSource.Local`(裁定 D-A:默认且联机亦然)。
        /// 被测物 = **生产** `CueRenderSink`(此前是测试自造的 `TierRecordingSink`)。
        /// 4 人运行面对拍 = BLOCKED-BY-45,不在本断言面。</summary>
        [Test]
        public void test_setTier_defaultLocalRecorded()
        {
            // Arrange
            var sink = new CueRenderSink(new NullRenderTarget());

            // Act
            sink.SetTier(TierSource.Local);

            // Assert
            Assert.That(sink.SetTierCallCount, Is.EqualTo(1), "SetTier 必须恰好一次");
            Assert.That(sink.CurrentTier, Is.EqualTo(TierSource.Local),
                "默认档位语义 = TierSource.Local(裁定 D-A:各设备按本机技能,非主机档)");
            // 闭集断言:TierSource 枚举不得出现「主机档」成员(回归注:防回退到主机档)
            Assert.That(Enum.GetNames(typeof(TierSource)), Does.Not.Contain("Host"),
                "TierSource 闭集不得出现「Host」—— 裁定 D-A:各设备按本机技能,无主机档来源");
        }

        /// <summary>AC-44-07(文档一致性):GDD 无**现行**「取主机技能」表述。
        /// 历史注里的「原判据…已作废」「已重开并改判」是刻意保留的改判记录,不算残句。</summary>
        [Test]
        public void test_gdd_netSection_noHostTierWording()
        {
            // Arrange
            string path = Path.Combine(repoRoot(), "design", "gdd", "audio-system.md");
            Assert.That(File.Exists(path), Is.True, $"GDD 缺失:{path}");
            string text = File.ReadAllText(path);

            // Act:排除历史注行(含「已作废」「改判」「重写」的行 = 改判记录,不是现行表述)
            var hits = FindLines(text, "主机技能")
                .Where(l => !(l.Contains("已作废") || l.Contains("改判") || l.Contains("重写")))
                .ToList();

            // Assert
            Assert.That(hits, Is.Empty,
                "GDD 不得残留现行「取主机技能」表述(2026-09-18 已改判 D-A):" + string.Join(";", hits));
        }

        // ══════════════ AC-44-D1 —— 位置来源检查(复用 IPositionalChannel)══════════════

        /// <summary>AC-44-D1(来源纪律 · 类型面):IPositionalChannel 只被 44 **消费**。
        /// 断言:生产装配内的实现者公开方法集 ⊆ 契约面(不新增出向口 —— 防「自建位置通道」漂移)。
        /// ⚠️ P0 内实现数 = 0(发布者实现随 45 走 P1b)⇒ 方法集断言在 0 实现时跳过;
        ///    该跳过**不是空转** —— 下面 `test_derivationSurfaceTypes_...` 有反空转守卫。</summary>
        [Test]
        public void test_positionalChannel_singleConsumerImplementation()
        {
            // Arrange:只扫生产装配(同 ProductionAudioTypes 口径)
            var prodAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == AssemblyGates.PresentationAssemblyName);
            Assert.That(prodAsm, Is.Not.Null, "生产装配必须已加载");
            var implementors = prodAsm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => typeof(IPositionalChannel).IsAssignableFrom(t))
                .ToList();

            // Assert(结构面:实现数 ≤ 1;P0 内当前 = 0,发布者实现随 45 走 P1b)
            Assert.That(implementors.Count, Is.LessThanOrEqualTo(1),
                "IPositionalChannel 在生产装配内不得多于一个实现(44 只读消费,不新建位置通道;" +
                "OQ-44-8 发布者实现随 45 走 P1b)" + string.Join(";", implementors.Select(t => t.FullName)));

            if (implementors.Count == 0) return;   // P0 内无实现 ⇒ 方法集断言跳过
            var t0 = implementors[0];
            var contractMethods = typeof(IPositionalChannel).GetMethods()
                .Select(m => m.Name).ToHashSet();
            var extraMethods = t0.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                               BindingFlags.DeclaredOnly)
                .Select(m => m.Name)
                .Where(n => !contractMethods.Contains(n))
                .ToList();
            Assert.That(extraMethods, Is.Empty,
                "实现者不得在契约面之外新增公开方法(防自建通道漂移):" +
                string.Join(";", extraMethods));
        }

        /// <summary>AC-44-D1(契约面):锚点值类型 `WorldPosLatest` 携带 `ServerTick` ——
        /// 消费纪律 2「读表须做陈旧检查」在**类型上有承载**(无字段 = 消费无从判陈旧)。</summary>
        [Test]
        public void test_worldPosLatest_carriesServerTick()
        {
            // Arrange
            var f = typeof(WorldPosLatest).GetField("ServerTick");

            // Assert
            Assert.That(f, Is.Not.Null, "WorldPosLatest.ServerTick 必须存在(陈旧检查的输入)");
            Assert.That(f.FieldType, Is.EqualTo(typeof(uint)), "ServerTick:uint(主机 tick 序号)");
        }

        /// <summary>AC-44-D1 + D-44 消费纪律(IL 面):44 **派生面**类型零 `IPositionalChannel`
        /// 引用(引用存在性扫描)。
        /// <para>⚠️ **2026-10-16 S14-2 收窄**:扫描面由「整个 44 命名空间」改为**具名派生面**
        /// `{CueEmitDeriver, CueEmitInput}` —— AC-44-D1 要求 44 **复用** `IPositionalChannel`
        /// 读锚点(位置面,`AnchorGate`),只禁**派生面**引用它。全命名空间扫会把合法消费点判红。
        /// 门侧同时新增**反空转守卫**(具名类型扫不到 ⇒ 红)—— 正面修补评审 §1.3「空转真扫」。</para></summary>
        [Test]
        public void test_derivationSurfaceTypes_zeroPositionalChannelRefs()
        {
            // Arrange
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(scriptCompilationFailed ⇒ 读上一版 DLL = 假绿)");

            // Act:具名派生面 IL 扫描(含反空转守卫)
            var errs = AssemblyGates.CheckAudioDerivationSurfaceRefs(
                AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName),
                Prefix, DerivationSurfaceTypeNames);

            // Assert
            Assert.That(errs, Is.Empty,
                "44 派生面类型不得引用 IPositionalChannel —— 位置读路径归空间化 / AnchorGate" +
                "(注入锚点的位置),发射派生必须与位置解耦" +
                "(GDD Edge:cue 发射 = {流事件,烘焙数据} 纯函数)" + string.Join(";", errs));
        }

        /// <summary>AC-44-D1 反空转守卫自证:具名面里点名一个**不存在**的类型 ⇒ 门必红
        /// (证明「零违例」不是对空集的平凡成立 —— 评审原件 §1.3 的正面修补)。</summary>
        [Test]
        public void test_derivationSurfaceScan_absentNamedType_isRed()
        {
            // Act:点名一个必然不存在的类型
            var errs = AssemblyGates.CheckAudioDerivationSurfaceRefs(
                AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName),
                Prefix, new[] { "ThisTypeDoesNotExist_反空转守卫自证" });

            // Assert
            Assert.That(errs, Is.Not.Empty,
                "具名面扫不到任何类型 ⇒ 必须红(否则「零违例」是对空集的平凡成立)");
            Assert.That(errs.Any(e => e.Contains("反空转守卫")), Is.True,
                "红行须点名反空转守卫(可定位):" + string.Join(";", errs));
        }

        /// <summary>AC-44-D1 负向:fixture 类型含 IPositionalChannel 成员 ⇒ 同一谓词必红。
        /// 自证扫描器不是空转(红方向成立)。
        /// ⚠️ 负例用 BCL 反射单类型扫描(fixture 在测试装配内,Cecil 扫的是生产装配)。</summary>
        [Test]
        public void test_derivationSurfaceScan_fixtureWithChannelRef_flagged()
        {
            // Act
            bool hit = TypeReferencesPositionalChannel(typeof(ChannelRefFixture));

            // Assert
            Assert.That(hit, Is.True, "含 IPositionalChannel 引用的类型必红");
        }

        /// <summary>AC-44-D1 消费纪律 1(**只读,不写回**):`AnchorGate` 的公开面**无任何
        /// 写通道方法**(`Publish*` / `Set*` / `Write*`)—— 发布者才是写方(ADR-001 §一之二)。</summary>
        [Test]
        public void test_anchorGate_noWriteBackSurface()
        {
            var writey = typeof(AnchorGate)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static |
                            BindingFlags.DeclaredOnly)
                .Where(m => m.Name.StartsWith("Publish", StringComparison.Ordinal) ||
                            m.Name.StartsWith("Set", StringComparison.Ordinal) ||
                            m.Name.StartsWith("Write", StringComparison.Ordinal))
                .Select(m => m.Name)
                .ToList();

            Assert.That(writey, Is.Empty,
                "AnchorGate 不得有写通道面(消费纪律 1:44 绝不写回第二 QoS 表;" +
                "发布者 = 9 / 13 / 20 主机侧,ADR-001 §一之二):" + string.Join(";", writey));
        }

        // ══════════════ AC-44-D7 —— 时钟面 / 输入面(静态腿①)══════════════

        /// <summary>AC-44-D7 ②(时钟面):44 派生面类型**方法体 IL** 不含 RNG / 帧时钟 /
        /// 墙钟引用。扫描器 = `AssemblyGates.CheckAudioClockTokens`(Mono.Cecil 版)。
        /// token 匹配按**类型名**:`UnityEngine.Time` 覆盖 deltaTime/time/frameCount 全部;
        /// `System.DateTime` 覆盖 Now/UtcNow/Today 全部。源文本层由 b5 托底(双层缺一不可)。</summary>
        [Test]
        public void test_derivationSurfaceTypes_ilBody_noClockOrRandomRefs()
        {
            // Arrange
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功");

            // Act
            var errs = AssemblyGates.CheckAudioClockTokens(
                AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName), Prefix);

            // Assert
            Assert.That(errs, Is.Empty,
                "44 派生面方法体禁 RNG / 帧时钟 / 墙钟(AC-44-D7 ①②):\n" + string.Join("\n", errs));
        }

        /// <summary>AC-44-D7 ② 负向:对**测试装配产物**跑同一 Cecil 谓词 —— 测试装配内含
        /// `RandomBodyFixture`(引用 `UnityEngine.Random`)⇒ 谓词必红。
        /// 自证扫描器端到端能命中。
        /// ⚠️ 测试装配产物路径按 asmdef 的 `name` 字段(不是文件名)。</summary>
        [Test]
        public void test_derivationScan_ilBodyFixtureRandom_flagged()
        {
            // Arrange
            string testDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(testDll), Is.True, $"测试装配产物缺失:{testDll}");

            // Act:按测试命名空间前缀扫(夹具住该前缀下)
            var errs = AssemblyGates.CheckAudioClockTokens(testDll, "DaYiJingCheng.Tests.Unit.Audio");

            // Assert
            Assert.That(errs, Is.Not.Empty,
                "Cecil 扫描器必须命中测试装配内的 RandomBodyFixture(方法体含 UnityEngine.Random)");
            Assert.That(errs[0], Does.Contain("RandomBodyFixture"), "违例必须点名类型(可定位)");
            Assert.That(errs[0], Does.Contain("UnityEngine.Random"), "违例必须点名禁名类型");
        }

        /// <summary>AC-44-D7 ③(载荷面):`AudioCueDto` 的字段类型全部 ∈ 整数域
        /// (int/byte/bool/Int3)——「Intensity 归一 ∈ 整数域」在**类型上有承载**;
        /// 无 float/double 字段(浮点出 sim = 各端可分不同桶)。</summary>
        [Test]
        public void test_audioCueDto_allFieldsIntegerDomain()
        {
            // Arrange
            var fields = typeof(AudioCueDto).GetFields();

            // Assert
            Assert.That(fields.Length, Is.EqualTo(5),
                "AudioCueDto 字段集漂移监控(增删字段须同步更新此断言):Cue/Intensity/Cell/Source/Looped");
            foreach (var f in fields)
            {
                Assert.That(IsIntegerDomain(f.FieldType), Is.True,
                    $"AudioCueDto.{f.Name} 类型「{f.FieldType.Name}」∉ 整数域 —— " +
                    "载荷面必须全整数(浮点 = 各端分桶可分叉,违 AC-44-D7 ③)");
            }
        }

        /// <summary>AC-44-D7 ③:`Intensity` 字段为 byte(0–255 归一强度的类型承载)。</summary>
        [Test]
        public void test_audioCueDto_intensityIsByte()
        {
            var f = typeof(AudioCueDto).GetField("Intensity");
            Assert.That(f, Is.Not.Null);
            Assert.That(f.FieldType, Is.EqualTo(typeof(byte)),
                "Intensity:byte 天然钳位 0–255(归一整数域)");
        }

        /// <summary>AC-44-D7 ③:`Cell` 为整数格 `Int3`(注入值,两段式装配的唯一注入位)。</summary>
        [Test]
        public void test_audioCueDto_cellIsInt3()
        {
            var f = typeof(AudioCueDto).GetField("Cell");
            Assert.That(f, Is.Not.Null);
            Assert.That(f.FieldType, Is.EqualTo(typeof(Int3)),
                "Cell:Int3 整数格(ADR-015;表现层把整数格换算 float)");
        }

        // ══════════════ AC-44-D7 —— 重排差分(静态腿②)══════════════

        /// <summary>AC-44-D7(重排差分 · **核心**):同一逻辑事件流 fixture 经 `EventOrderKey`
        /// 全序排序 → cue 序列(含 Intensity)哈希,对**到达序打乱不变**。
        /// <para>⚠️ 被测物 = **生产 `CueEmitDeriver`**(补评前是测试自写的 `DeriveCueHash` ——
        /// 评审原件 §2.1 判 🔴 的最重一条)。</para></summary>
        [Test]
        public void test_reorderInvariance_arrivalOrderPreservesCueHash()
        {
            // Arrange:同流(World)6 事件,故意乱序到达
            var fixture = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 200, source: 1),
                new CueEmitInput(10, StreamId.World, 1, 101, cueId: 12, intensity: 100, source: 1),
                new CueEmitInput( 9, StreamId.World, 1,  99, cueId: 10, intensity: 255, source: 1),
                new CueEmitInput(10, StreamId.World, 1, 102, cueId: 13, intensity:   0, source: 2),
                new CueEmitInput( 9, StreamId.World, 2,  98, cueId: 20, intensity:  50, source: 3),
                new CueEmitInput(11, StreamId.World, 2, 103, cueId: 21, intensity:  77, source: 3),
            };
            var shuffled = new[]
            {
                fixture[3], fixture[0], fixture[5], fixture[2], fixture[1], fixture[4],
            };

            // Act:派生(内含全序排序)+ 规范哈希
            ulong h1 = CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(fixture));
            ulong h2 = CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(shuffled));

            // Assert
            Assert.That(h1, Is.Not.EqualTo(0UL), "哈希为 0 = 折叠链断裂(拒以退化值冒充一致)");
            Assert.That(h2, Is.EqualTo(h1),
                "同流乱序到达 ⇒ cue 序列(含 Intensity)哈希必须一致 —— 否则各客户端" +
                "听到不同序列(ADR-001 ReorderBuffer 语义的静态半)");
        }

        /// <summary>重排差分判据的**载荷面自证**:Intensity 必须进哈希 ——
        /// 篡改一条的 Intensity ⇒ 哈希变(否则「含 Intensity 哈希一致」是空话)。</summary>
        [Test]
        public void test_reorderInvariance_intensityFeedsHash()
        {
            // Arrange
            var fixture = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 200, source: 1),
                new CueEmitInput(10, StreamId.World, 1, 101, cueId: 12, intensity: 100, source: 1),
                new CueEmitInput(11, StreamId.World, 2, 103, cueId: 21, intensity:  77, source: 3),
            };
            var tampered = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 201, source: 1),
                fixture[1], fixture[2],
            };

            // Act
            ulong h1 = CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(fixture));
            ulong h2 = CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(tampered));

            // Assert
            Assert.That(h2, Is.Not.EqualTo(h1),
                "Intensity 篡改必须改变哈希(载荷面判据:重排一致的是**含强度**的序列)");
        }

        /// <summary>重排差分判据的**事件集自证**:事件数不同 ⇒ 哈希不同(防「哈希只覆盖子集」)。</summary>
        [Test]
        public void test_reorderInvariance_eventCountFeedsHash()
        {
            // Arrange
            var full = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 200, source: 1),
                new CueEmitInput(10, StreamId.World, 1, 101, cueId: 12, intensity: 100, source: 1),
            };
            var one = new[] { full[0] };

            // Act + Assert
            Assert.That(CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(one)),
                Is.Not.EqualTo(CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(full))),
                "事件集不同 ⇒ 哈希必须不同(哈希面覆盖全部事件)");
        }

        /// <summary>AC-44-D7 ③(载荷面同纯度):**全字段**都进哈希 —— 只折 `Cue`+`Intensity`
        /// 会让 `Source` / `Looped` 的纯度断言失去判别力(补评前 `DeriveCueHash` 的注释与
        /// 实现曾不一致)。本测逐字段自证。</summary>
        [Test]
        public void test_reorderInvariance_allPayloadFieldsFeedHash()
        {
            var baseline = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 200,
                                 source: 7, looped: false),
            };
            ulong h = CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(baseline));

            // Source 变 ⇒ 哈希须变
            var srcChanged = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 200,
                                 source: 8, looped: false),
            };
            Assert.That(CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(srcChanged)),
                Is.Not.EqualTo(h), "Source 必须进哈希(载荷面全字段)");

            // Looped 变 ⇒ 哈希须变
            var loopChanged = new[]
            {
                new CueEmitInput(10, StreamId.World, 1, 100, cueId: 11, intensity: 200,
                                 source: 7, looped: true),
            };
            Assert.That(CueEmitDeriver.CanonicalHash(CueEmitDeriver.Derive(loopChanged)),
                Is.Not.EqualTo(h), "Looped 必须进哈希(载荷面全字段)");
        }

        /// <summary>AC-44-D7(两段式装配):派生面产出的 `Cell` **恒为 default** ——
        /// 位置在空间化时注入,派生面不读位置(与 `test_derivationSurfaceTypes_...` 的
        /// IL 面互证:一个断类型面,一个断值面)。</summary>
        [Test]
        public void test_derive_cellIsDefaultInjectionPending()
        {
            var cues = CueEmitDeriver.Derive(new[]
            {
                new CueEmitInput(1, StreamId.World, 1, 1, cueId: 5, intensity: 9, source: 3),
            });

            Assert.That(cues, Has.Count.EqualTo(1));
            Assert.That(cues[0].Cell.X, Is.EqualTo(0), "派生面不得产出 Cell(注入归空间化)");
            Assert.That(cues[0].Cell.Y, Is.EqualTo(0));
            Assert.That(cues[0].Cell.Z, Is.EqualTo(0));
            Assert.That(cues[0].Source, Is.EqualTo(3), "Source 是派生物(非注入)");
        }

        /// <summary>AC-44-D7(输入面):`Derive` 的 null 输入 ⇒ 抛(拒以空集冒充绿)。</summary>
        [Test]
        public void test_derive_nullInput_throws()
        {
            Assert.Throws<ArgumentNullException>(() => CueEmitDeriver.Derive(null));
            Assert.Throws<ArgumentNullException>(() => CueEmitDeriver.CanonicalHash(null));
        }

        // ══════════════ AC-44-D10 —— 锚点纪律(读路径只读锚点,禁判定)══════════════

        /// <summary>AC-44-D10(锚点缺席语义):一次性 cue 无锚点 ⇒ **丢弃不补播**;
        /// 循环 cue 无锚点 ⇒ **挂起**。被测物 = **生产 `AnchorGate`**(此前是自造 `FakeAnchorGate`)。</summary>
        [Test]
        public void test_anchorAbsent_oneShotDropped_loopCuePending()
        {
            // Arrange:空通道(无任何锚点)
            var gate = new AnchorGate(new EmptyChannel());
            var oneShot = new AudioCueDto(11, 200, default, 5, looped: false);
            var looped = new AudioCueDto(12, 100, default, 5, looped: true);

            // Act
            GateDecision d1 = gate.Dispatch(oneShot);
            GateDecision d2 = gate.Dispatch(looped);

            // Assert
            Assert.That(d1, Is.EqualTo(GateDecision.Dropped), "一次性 cue 锚点缺席 ⇒ 丢弃(不补播)");
            Assert.That(d2, Is.EqualTo(GateDecision.Pending), "循环 cue 锚点缺席 ⇒ 挂起");
            Assert.That(gate.PendingCount, Is.EqualTo(1), "挂起队列 = 1");
        }

        /// <summary>AC-44-D10(锚点到达语义):挂起的循环 cue 在锚点到达后**补发**。</summary>
        [Test]
        public void test_anchorArrived_pendingLoopReleased()
        {
            // Arrange
            var channel = new EmptyChannel();
            var gate = new AnchorGate(channel);
            var looped = new AudioCueDto(12, 100, default, 5, looped: true);

            // Act
            Assert.That(gate.Dispatch(looped), Is.EqualTo(GateDecision.Pending));
            channel.PublishLatest(new WorldPosLatest(5, new Int3(3, 0, 4), serverTick: 12, flags: 0));
            int emitted = gate.Update();

            // Assert
            Assert.That(emitted, Is.EqualTo(1), "锚点到达 ⇒ 挂起中的循环 cue 补发(不静默丢)");
            Assert.That(gate.PendingCount, Is.EqualTo(0), "挂起队列已清空");
        }

        /// <summary>AC-44-D10 + 消费纪律 2(陈旧检查):锚点 `ServerTick` 早于已见最新 tick
        /// ⇒ 陈旧。⚠️ **陈旧检查归渲染路由面**(AC-44-D1 逐字:「`ServerTick` 陈旧检查 = 渲染路由
        /// 可读;『发不发 cue』= 判定,禁」)⇒ 本测断**谓词本身**,不断 `Dispatch` 的行为。</summary>
        [Test]
        public void test_anchorStale_refused()
        {
            // Arrange
            const uint latestTick = 50;

            // Act + Assert(陈旧判据谓词 —— 渲染路由调用它)
            Assert.That(AnchorGate.IsStale(12u, latestTick), Is.True,
                "ServerTick 12 < 最新 50 ⇒ 陈旧(ADR-001 §一之二 消费纪律 2)");
            Assert.That(AnchorGate.IsStale(latestTick, latestTick), Is.False,
                "等 tick 不陈旧(拒读面不得误杀本机帧最新值)");
            Assert.That(AnchorGate.IsStale(51u, latestTick), Is.False,
                "更新的 tick 不陈旧(单调序:只拒旧不拒新)");
        }

        /// <summary>⚠️ AC-44-D1 分工的**负向守卫**:陈旧锚点**仍算有锚点** ——
        /// 陈旧 ≠ 缺席。若把陈旧检查塞进 `Dispatch`,断线时的一次性 cue 会被丢弃,
        /// 违 GDD「使用最近一次位置快照」。</summary>
        [Test]
        public void test_staleAnchor_stillCountsAsPresent()
        {
            // Arrange:通道里有一行(值任意 —— Dispatch 只读存在性)
            var channel = new EmptyChannel();
            channel.PublishLatest(new WorldPosLatest(5, new Int3(1, 1, 1), serverTick: 1, flags: 0));
            var gate = new AnchorGate(channel);
            var oneShot = new AudioCueDto(11, 200, default, 5, looped: false);

            // Act + Assert
            Assert.That(gate.HasAnchor(5), Is.True,
                "有锚点行 ⇒ HasAnchor 为真(Dispatch 只读存在性,不判陈旧)");
            Assert.That(gate.Dispatch(oneShot), Is.EqualTo(GateDecision.Emitted),
                "陈旧锚点仍可发出(「使用最近一次位置快照」,GDD Edge Cases)—— 陈旧 ≠ 缺席");
        }

        /// <summary>AC-44-D10(重复挂起):同源重复挂起 ⇒ 以最后一次为准(不叠两条)。</summary>
        [Test]
        public void test_anchorAbsent_duplicatePending_lastWins()
        {
            var gate = new AnchorGate(new EmptyChannel());
            var a = new AudioCueDto(12, 100, default, 5, looped: true);
            var b = new AudioCueDto(13, 222, default, 5, looped: true);

            Assert.That(gate.Dispatch(a), Is.EqualTo(GateDecision.Pending));
            Assert.That(gate.Dispatch(b), Is.EqualTo(GateDecision.Pending));
            Assert.That(gate.PendingCount, Is.EqualTo(1), "同源重复挂起 ⇒ 只留最后一次(防两层呼吸)");
        }

        // ══════════════ EndLoop 自评兜底(ADR-001 §一之三 裁决二)══════════════

        /// <summary>裁决二:P0 默认兜底路径 = **呈现侧从最新快照 `Progress` 自求值**
        /// (零网络依赖)。被测物 = **生产 `EndLoopWatchdog`**(此前是自造 `FakeEndLoopWatchdog`)。</summary>
        [Test]
        public void test_endLoop_selfAssessPredicate_pureAndBounded()
        {
            // Arrange:快照停在 tick 60,阈值 = AudioTuning.LoopEvalMaxTicks
            var snap = new LoopProgress(loopingCue: 42, progress: 100, serverTick: 60);
            long stopTick = 60 + AudioTuning.LoopEvalMaxTicks + 1;

            // Act + Assert(判停面)
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, stopTick), Is.True,
                "停滞超有界阈值 ⇒ 判停(零网络依赖的自评兜底)");
            // 负向:未超阈值不判停
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, currentTick: 61), Is.False,
                "差值未超阈值 ⇒ 不判停(防误杀在推进的循环)");
            // 负向:非循环 cue 不判停
            var notLoop = new LoopProgress(loopingCue: 0, progress: 100, serverTick: 60);
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(notLoop, currentTick: 9999), Is.False,
                "非循环 cue 不进兜底面");
        }

        /// <summary>裁决二(有界性):自评阈值 = **有界常量**(不得留空 —— 「二者至少其一,
        /// 不得留空」的架构义务在 P0 由本常量兑现)。</summary>
        [Test]
        public void test_endLoop_selfAssessThreshold_isPositiveBoundedConstant()
        {
            Assert.That(AudioTuning.LoopEvalMaxTicks, Is.GreaterThan(0),
                "停滞阈值必须有正有界值(兜底义务不得留空 —— ADR-001 裁决二)");
        }

        /// <summary>裁决二(边界):阈值取 0 ⇒ 停滞 1 tick 即判停(下界自证);
        /// 等值不判(边界归下一 tick)。</summary>
        [Test]
        public void test_endLoop_thresholdBoundary_zeroAndEqual()
        {
            var snap = new LoopProgress(loopingCue: 7, progress: 5, serverTick: 100);

            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, 100, maxStagnantTicks: 0), Is.False,
                "停滞 0 tick ⇒ 不判(时间未推进)");
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, 101, maxStagnantTicks: 0), Is.True,
                "阈值 0 + 停滞 1 tick ⇒ 判停(下界自证)");
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, 110, maxStagnantTicks: 10), Is.False,
                "停滞 == 阈值 ⇒ 不判(严格大于;边界归下一 tick)");
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, 111, maxStagnantTicks: 10), Is.True,
                "停滞 > 阈值 ⇒ 判停");
        }

        /// <summary>裁决二(负向):时间倒流(当前 tick 早于快照)⇒ 不判停(防误杀)。</summary>
        [Test]
        public void test_endLoop_timeGoingBackward_notTerminated()
        {
            var snap = new LoopProgress(loopingCue: 7, progress: 5, serverTick: 100);
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(snap, currentTick: 50), Is.False,
                "当前 tick 早于快照 ⇒ 不判停(时间倒流不是「停滞」)");
        }

        /// <summary>裁决二(无进度快照):`Progress ≤ 0` ⇒ 无从判 ⇒ 不判停。</summary>
        [Test]
        public void test_endLoop_noProgressSnapshot_notTerminated()
        {
            var noProgress = new LoopProgress(loopingCue: 7, progress: 0, serverTick: 1);
            Assert.That(EndLoopWatchdog.ShouldSelfTerminate(noProgress, currentTick: 99999), Is.False,
                "无 Progress 快照 ⇒ 不判(无从求值)");
        }

        // ══════════════ AC-44-07 —— 句柄生命周期(IAudioCueSink 生产实现)══════════════

        /// <summary>AC-44-07 / GDD Edge Cases:同源重复 `BeginLoop` ⇒ 以最后一次为准,旧句柄释放
        /// (防双呼吸层)。被测物 = **生产 `CueRenderSink`**。</summary>
        [Test]
        public void test_beginLoop_duplicateSource_lastWins()
        {
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var cue1 = new AudioCueDto(1, 100, default, source: 5, looped: true);
            var cue2 = new AudioCueDto(2, 200, default, source: 5, looped: true);

            AudioCueHandle h1 = sink.BeginLoop(cue1);
            AudioCueHandle h2 = sink.BeginLoop(cue2);

            Assert.That(sink.ActiveLoopCount, Is.EqualTo(1), "同源重复 ⇒ 只存活一个句柄");
            Assert.That(h2.Id, Is.Not.EqualTo(h1.Id), "新句柄是新 id");
            Assert.That(target.StoppedIds, Does.Contain(h1.Id), "旧句柄必须被释放");
        }

        /// <summary>AC-44-07 / GDD Edge Cases:过期 `EndLoop` ⇒ 忽略,不产生播放错误。</summary>
        [Test]
        public void test_endLoop_expiredHandle_ignored()
        {
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);

            Assert.DoesNotThrow(() => sink.EndLoop(new AudioCueHandle(9999)),
                "过期句柄 ⇒ 静默忽略(不抛)");
            Assert.That(target.StoppedIds, Is.Empty, "过期句柄不得触发 StopLoop");
        }

        /// <summary>AC-44-07:正常 `BeginLoop` → `EndLoop` 往返。</summary>
        [Test]
        public void test_beginEndLoop_roundTrip()
        {
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var cue = new AudioCueDto(1, 100, default, source: 5, looped: true);

            AudioCueHandle h = sink.BeginLoop(cue);
            Assert.That(sink.ActiveLoopCount, Is.EqualTo(1));

            sink.EndLoop(h);
            Assert.That(sink.ActiveLoopCount, Is.EqualTo(0), "收尾后句柄释放");
            Assert.That(target.StartedIds, Does.Contain(h.Id));
            Assert.That(target.StoppedIds, Does.Contain(h.Id));
        }

        /// <summary>AC-44-07:`Emit` 转发到渲染目标(一次性 cue 通道)。</summary>
        [Test]
        public void test_emit_forwardsToTarget()
        {
            var target = new RecordingRenderTarget();
            var sink = new CueRenderSink(target);
            var cue = new AudioCueDto(9, 77, default, source: 3, looped: false);

            sink.Emit(cue);

            Assert.That(target.OneShotCount, Is.EqualTo(1));
            Assert.That(target.LastOneShot.Cue, Is.EqualTo(9));
            Assert.That(target.LastOneShot.Intensity, Is.EqualTo(77));
        }

        /// <summary>AC-44-07:构造 null 目标 ⇒ 抛(拒以空集冒充绿)。</summary>
        [Test]
        public void test_cueRenderSink_nullTarget_throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CueRenderSink(null));
            Assert.Throws<ArgumentNullException>(() => new AnchorGate(null));
        }

        // ══════════════ D1 位置通道引用扫描(BCL 反射,负例夹具用)══════════════

        /// <summary>具名派生面类型名单(AC-44-D1 的扫描面 —— 单一出处,测试与门共用)。</summary>
        private static readonly string[] DerivationSurfaceTypeNames =
        {
            nameof(CueEmitDeriver), nameof(CueEmitInput),
        };

        /// <summary>单类型的 `IPositionalChannel` 引用扫描(BCL 反射:字段 / 方法签名)。
        /// ⚠️ 生产装配的 IL 层扫描归 `AssemblyGates.CheckAudioDerivationSurfaceRefs`(Cecil 版);
        ///    本方法只用于负例夹具(fixture 在测试装配内,Cecil 扫不到)。</summary>
        private static bool TypeReferencesPositionalChannel(Type t)
        {
            const string token = "IPositionalChannel";
            for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
            {
                foreach (var f in cur.GetFields(BindingFlags.Instance | BindingFlags.Static |
                                                 BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.DeclaredOnly))
                    if (f.FieldType.Name.Contains(token)) return true;
            }
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static |
                                          BindingFlags.Public | BindingFlags.NonPublic |
                                          BindingFlags.DeclaredOnly))
            {
                if (m.ReturnType.Name.Contains(token)) return true;
                foreach (var p in m.GetParameters())
                    if (p.ParameterType.Name.Contains(token)) return true;
            }
            return false;
        }

        private static bool IsIntegerDomain(Type t)
            => t == typeof(int) || t == typeof(byte) || t == typeof(bool) || t == typeof(Int3);

        private static List<string> FindLines(string text, string needle)
            => text.Split('\n')
                   .Select((line, i) => (line, i))
                   .Where(p => p.line.Contains(needle, StringComparison.Ordinal))
                   .Select(p => $"{p.i + 1}:{p.line.Trim()}")
                   .ToList();

        // ══════════════ 测试替身(仅两处非生产物 —— 见文件头注)══════════════

        /// <summary>空位置通道(测试夹具:锚点缺席的可测形态)。
        /// ⚠️ 刻意**不是**生产实现 —— 生产发布者随 45 走 P1b(OQ-44-8)。</summary>
        private sealed class EmptyChannel : IPositionalChannel
        {
            private readonly Dictionary<int, WorldPosLatest> _rows = new Dictionary<int, WorldPosLatest>();

            public void PublishLatest(in WorldPosLatest p) => _rows[p.ActorId] = p;

            public bool TryReadLatest(int actorId, out WorldPosLatest p)
                => _rows.TryGetValue(actorId, out p);

            public void SubscribeActor(int actorId, Action<WorldPosLatest> onUpdate) { }
        }

        /// <summary>记录型渲染目标(测试夹具:`ICueRenderTarget` 是 44 自用注入缝)。</summary>
        private sealed class RecordingRenderTarget : ICueRenderTarget
        {
            public readonly List<int> StartedIds = new List<int>();
            public readonly List<int> StoppedIds = new List<int>();
            public int OneShotCount;
            public AudioCueDto LastOneShot;

            public void PlayOneShot(in AudioCueDto cue) { OneShotCount++; LastOneShot = cue; }
            public void StartLoop(in AudioCueDto cue, int handleId) => StartedIds.Add(handleId);
            public void StopLoop(int handleId) => StoppedIds.Add(handleId);
            public bool SetLoopGain(int handleId, float gain) => false;
        }

        // ══════════════ 负例夹具(住测试命名空间,不入 44 生产扫描键)══════════════

        private sealed class ChannelRefFixture
        {
            public IPositionalChannel Channel;   // D1 负例:派生面引用位置通道
        }

        private sealed class RandomBodyFixture
        {
            public float Next() => UnityEngine.Random.value; // D7 ② 负例:方法体含 RNG
        }
    }
}
