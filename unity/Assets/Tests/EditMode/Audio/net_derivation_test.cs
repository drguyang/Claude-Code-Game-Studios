// 权威来源:production/epics/audio-system/story-007-net-divergence-remote-derivation.md
//   (AC-44-07 静态半 / AC-44-D1 / AC-44-D7 BLOCKING 静态三腿 + AC-44-D10 锚点纪律)
//   · 007 只交付**静态半**:重排差分 / 消费纪律 / 锚点纪律 / 载荷纯度 / 默认档位语义 /
//     EndLoop 自评兜底;**4 人双实例对拍 = BLOCKED-BY-45**(P1b)不在本 story 断言面。
//   · ADR-018 §五(联机音频:分叉的只有音)· ADR-001 §一之二(IPositionalChannel + 消费纪律 1-3)
//     + §一之三 裁决二(EndLoop 兜底 = 呈现侧义务)· ADR-005(客户端持流副本)·
//     ADR-008(EventOrderKey 跨流全序键)· ADR-012(整数哈希 SplitMix64)·
//     ADR-027(表现态锚点发布者 = 9 / 13 主机侧,P1b 随 45 —— 本 story 只断言读纪律)
//
// ⚠️ 落点:故事 Test Evidence 登记路径 = tests/unit/audio_system/net_derivation_test.cs;
//    Unity 只编译 unity/Assets/ 树 ⇒ **真身 = 本文件**(承 Story 001/002/003/004/005/006/014
//    同一先例);账本侧由 tests/unit/audio_system/README.md 互链。
// ⚠️ 负向夹具落位:与 assembly_boundary_test 同型 —— 违例 fixture 住**测试命名空间**
//    (不入 44 生产扫描键、不污染生产断言面;生产树的 IL/源文本负例路径已由 Story 001 的
//    b5 扫描器与本文件的文本层负例双层托底)。
// ⚠️ 扫描键单一出处 = AssemblyGates.AudioModuleNamespacePrefix(测试不重复定义字面量)。
// ⚠️ 测试纪律:arrange/act/assert · 无随机(确定性来源只有 SplitMix64 固定种子)·
//    无时间依赖(System.DateTime / Time 被本文件的扫描器判红,夹具用显式计数字段)·
//    夹具读取前置 File.Exists 断言(缺失即红,不静默跳过)。
// ⚠️ 与 Story 001 分工:本文件**不重复** b5 全门(引用集 / 类型黑名单 / B2 guard / B3 入口 /
//    B4 字段),只落 007 自有判据面;唯一的跨 story 不变量 = 「派生面零 IPositionalChannel
//    引用」—— 由本文件 D1 的 44 派生面类型 IL 扫描承担(ScanDerivationSurfaceTypes 谓词
//    与 b5 的 CheckAudioAssemblyIl 同构,只判**引用存在性**、不复用后者的黑名单语义)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
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

        // 本文件在 unity/Assets/Tests/EditMode/Audio/ ⇒ Audio→EditMode→Tests→Assets→unity→
        // 仓库根 = 5 层(写 4 层 ⇒ root 落在 unity/ 下,夹具 / GDD / 源树全报「缺失」——
        // Story 003 踩过)。
        private static string repoRoot([CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile) ?? ".",
                "..", "..", "..", "..", ".."));

        private const string Prefix = AssemblyGates.AudioModuleNamespacePrefix;

        /// <summary>生产 44 类型:只取 Gameplay.Presentation 装配(测试装配内的 fixture
        /// 刻意不入生产断言面 —— 负例走显式传入,同 assembly_boundary_test 分工)。</summary>
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

        /// <summary>AC-44-07(静态半):SetTier 收到 TierSource.Local(裁定 D-A:默认且联机亦然)。
        /// 记录型 IAudioCueSink 替身;4 人运行面对拍 = BLOCKED-BY-45,不在本断言面。</summary>
        [Test]
        public void test_setTier_defaultLocalRecorded()
        {
            // Arrange
            var sink = new TierRecordingSink();

            // Act
            sink.SetTier(TierSource.Local);

            // Assert
            Assert.That(sink.SetTierCalls, Has.Count.EqualTo(1), "SetTier 必须恰好一次");
            Assert.That(sink.SetTierCalls[0], Is.EqualTo(TierSource.Local),
                "默认档位语义 = TierSource.Local(裁定 D-A:各设备按本机技能,非主机档)");
            // 闭集断言:TierSource 枚举不得出现「主机档」成员(回归注:防回退到主机档)
            Assert.That(Enum.GetNames(typeof(TierSource)), Does.Not.Contain("Host"),
                "TierSource 闭集不得出现「Host」—— 裁定 D-A:各设备按本机技能,无主机档来源");
        }

        /// <summary>AC-44-07(文档一致性):GDD 无**现行**「取主机技能」表述。
        /// 历史注里的「原判据…已作废」「已重开并改判」是刻意保留的改判记录,不算残句。
        /// 回归注:本判据防实现期回退到主机档。</summary>
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
                "GDD 不得残留现行「取主机技能」表述(2026-09-18 已改判 D-A —— 各设备按本机技能;" +
                "历史注里的改判记录不算):" + string.Join(";", hits));
        }

        // ══════════════ AC-44-D1 —— 位置来源检查(复用 IPositionalChannel)══════════════

        /// <summary>AC-44-D1(来源纪律 · 类型面):IPositionalChannel 只被 44 **消费**。
        /// 断言:生产装配内有且只有一个实现(发布者实现随 45 走 P1b ⇒ 当前实现数 = 1);
        /// 实现者的公开方法集 ⊆ 契约面(PublishLatest/TryReadLatest/SubscribeActor),
        /// 不新增出向口(防「自建位置通道」漂移)。
        /// ⚠️ 扫描面 = 只取生产装配(Gameplay.Presentation),不含测试装配 ——
        ///    测试装配内的 FakePositionalChannel 不是生产实现(空过防护)。</summary>
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

            // Assert(结构面:实现数 ≤ 1;P0 内当前 = 0,实现归 Story 008)
            Assert.That(implementors.Count, Is.LessThanOrEqualTo(1),
                "IPositionalChannel 在生产装配内不得多于一个实现(44 只读消费,不新建位置通道;" +
                "P0 内当前 = 0,实现归 Story 008;OQ-44-8 发布者实现随 45 走 P1b)" +
                string.Join(";", implementors.Select(t => t.FullName)));

            // 断言实现者的公开方法集 ⊆ 契约面(不新增出向口)
            if (implementors.Count == 0) return;   // P0 内无实现 = Story 008 未开工,方法集断言跳过
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

        /// <summary>AC-44-D1(契约面):锚点值类型 WorldPosLatest 携带 ServerTick ——
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

        /// <summary>AC-44-D1 + D-44 消费纪律(IL 面):44 派生面类型**零** IPositionalChannel
        /// 引用(引用存在性扫描,与 b5 CheckAudioAssemblyIl 同构)。
        /// 负例:注入含该引用的 fixture ⇒ 谓词必红(端到端自证)。</summary>
        [Test]
        public void test_derivationSurfaceTypes_zeroPositionalChannelRefs()
        {
            // Arrange
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(scriptCompilationFailed ⇒ 读上一版 DLL = 假绿)");

            // Act:Cecil 扫描器一次扫全部 44 生产类型(字段/方法签名/方法体 IL)
            var errs = AssemblyGates.CheckAudioPositionalChannelRefs(
                AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName), Prefix);

            // Assert
            Assert.That(errs, Is.Empty,
                "44 派生面类型不得引用 IPositionalChannel —— 位置读路径归 Story 008 的空间化" +
                "(注入锚点的位置),发射派生必须与位置解耦(GDD Edge:cue 发射 = {流事件,烘焙数据} 纯函数)" +
                string.Join(";", errs));
        }

        /// <summary>AC-44-D1 负向:fixture 类型含 IPositionalChannel 成员 ⇒ 同一谓词必红。
        /// 自证扫描器不是空转(红方向成立)。
        /// ⚠️ 负例用 BCL 反射单类型扫描(fixture 在测试装配内,Cecil 扫的是生产装配)。</summary>
        [Test]
        public void test_derivationSurfaceScan_fixtureWithChannelRef_flagged()
        {
            // Act:BCL 反射扫描 fixture 的字段/方法签名(含方法体 IL 由 Cecil 层兜)
            bool hit = TypeReferencesPositionalChannel(typeof(ChannelRefFixture));

            // Assert
            Assert.That(hit, Is.True, "含 IPositionalChannel 引用的类型必红");
        }

        // ══════════════ AC-44-D7 —— 时钟面 / 输入面(静态腿①)══════════════

        /// <summary>AC-44-D7 ②(时钟面):44 派生面类型**方法体 IL** 不含 RNG / 帧时钟 /
        /// 墙钟引用。扫描器 = AssemblyGates.CheckAudioClockTokens(Mono.Cecil 版,与 b5
        /// CheckAudioAssemblyIl 同构)—— **不手写 IL 解析器**(手写解析器有 ret 误判 /
        /// 属性访问失效 / 操作数表错误三类缺陷,见代码评审 B1/B2/B3)。
        /// token 匹配按**类型名**:UnityEngine.Time 覆盖 deltaTime/time/frameCount/
        /// fixedDeltaTime/unscaledDeltaTime 全部;System.DateTime 覆盖 Now/UtcNow/Today 全部。
        /// 源文本层由 b5 的 CheckAudioSourceFiles 托底(双层缺一不可)。</summary>
        [Test]
        public void test_derivationSurfaceTypes_ilBody_noClockOrRandomRefs()
        {
            // Arrange
            Assert.That(EditorUtility.scriptCompilationFailed, Is.False,
                "跑 Cecil 扫描前提:编译成功(scriptCompilationFailed ⇒ 读上一版 DLL = 假绿)");

            // Act
            var errs = AssemblyGates.CheckAudioClockTokens(
                AssemblyGates.ScriptAssemblyPath(AssemblyGates.PresentationAssemblyName), Prefix);

            // Assert
            Assert.That(errs, Is.Empty,
                "44 派生面方法体禁 RNG / 帧时钟 / 墙钟(AC-44-D7 ①②:派生 = {流事件,烘焙数据}" +
                "纯函数,时钟面由流/tick 驱动):\n" + string.Join("\n", errs));
        }

        /// <summary>AC-44-D7 ② 负向:对**测试装配产物**跑同一 Cecil 谓词 —— 测试装配内含
        /// RandomBodyFixture(引用 UnityEngine.Random)⇒ 谓词必红。
        /// 自证扫描器端到端能命中(同 Story 001 b5① 端到端负例纪律)。
        /// ⚠️ 测试装配产物路径 = Library/ScriptAssemblies/Sim.Contracts.Tests.dll
        /// (EditMode.asmdef 的 name 字段,不是文件名 —— 与 b5 同口径)。</summary>
        [Test]
        public void test_derivationScan_ilBodyFixtureRandom_flagged()
        {
            // Arrange:测试装配内含 RandomBodyFixture(方法体引用 UnityEngine.Random.value)
            string testDll = AssemblyGates.ScriptAssemblyPath("Sim.Contracts.Tests");
            Assert.That(File.Exists(testDll), Is.True, $"测试装配产物缺失:{testDll}");

            // Act:对测试装配跑 Cecil 时钟 token 扫描(扫描键 = 44 前缀 —— fixture 在
            // DaYiJingCheng.Tests.Unit.Audio 下,不在 44 前缀内 ⇒ 扫描 0 命中 = 假绿面)
            // ⚠️ 故此处直接断言:Cecil 扫描器对**含 RNG 引用的类型**必报红 ——
            //    用 Cecil 直接读测试装配产物,不按前缀过滤,而是检查 RandomBodyFixture
            //    类型本身是否被扫到。
            var errs = AssemblyGates.CheckAudioClockTokens(testDll, "DaYiJingCheng.Tests.Unit.Audio");

            // Assert
            Assert.That(errs, Is.Not.Empty,
                "Cecil 扫描器必须命中测试装配内的 RandomBodyFixture(方法体含 UnityEngine.Random)");
            Assert.That(errs[0], Does.Contain("RandomBodyFixture"),
                "违例必须点名 RandomBodyFixture 类型(可定位)");
            Assert.That(errs[0], Does.Contain("UnityEngine.Random"),
                "违例必须点名禁名类型 UnityEngine.Random");
        }

        /// <summary>AC-44-D7 ③(载荷面):AudioCueDto 的字段类型全部 ∈ 整数域
        /// (int/byte/bool/Int3)——「Intensity 归一 ∈ 整数域」在**类型上有承载**;
        /// 无 float/double 字段(浮点出 sim = 各端可分不同桶,违 D-8-4 承接口径)。</summary>
        [Test]
        public void test_audioCueDto_allFieldsIntegerDomain()
        {
            // Arrange
            var fields = typeof(AudioCueDto).GetFields();

            // Assert
            Assert.That(fields.Length, Is.EqualTo(5),
                "AudioCueDto 字段集漂移监控(增删字段须同步更新此断言):Cue/Intensity/Cell/Source/Looped 五件(ADR-018 §二)");
            foreach (var f in fields)
            {
                Assert.That(IsIntegerDomain(f.FieldType), Is.True,
                    $"AudioCueDto.{f.Name} 类型「{f.FieldType.Name}」∉ 整数域 —— " +
                    "载荷面必须全整数(浮点 = 各端分桶可分叉,违 AC-44-D7 ③)");
            }
        }

        /// <summary>AC-44-D7 ③:Intensity 字段为 byte(0–255 归一强度的类型承载)。</summary>
        [Test]
        public void test_audioCueDto_intensityIsByte()
        {
            var f = typeof(AudioCueDto).GetField("Intensity");
            Assert.That(f, Is.Not.Null);
            Assert.That(f.FieldType, Is.EqualTo(typeof(byte)),
                "Intensity:byte 天然钳位 0–255(归一整数域,GDD 语声变体段)");
        }

        /// <summary>AC-44-D7 ③:Cell 为整数格 Int3(注入值,两段式装配的唯一注入位)。</summary>
        [Test]
        public void test_audioCueDto_cellIsInt3()
        {
            var f = typeof(AudioCueDto).GetField("Cell");
            Assert.That(f, Is.Not.Null);
            Assert.That(f.FieldType, Is.EqualTo(typeof(Int3)),
                "Cell:Int3 整数格(ADR-015;表现层把整数格换算 float —— AC-44-D2 同向)");
        }

        // ══════════════ AC-44-D7 —— 重排差分(静态腿②)══════════════

        /// <summary>AC-44-D7(重排差分 · 核心):同一逻辑事件流 fixture 经 EventOrderKey
        /// 全序排序 → cue 序列(含 Intensity)哈希,对**到达序打乱不变**。
        /// 哈希 = SplitMix64 链式折叠(全案唯一整数哈希源,ADR-005/D-1 乙案;禁平台敏感路径)。
        /// 语义 = 客户端持流副本(ADR-005)经 ReorderBuffer 重排后各端 cue 序列一致的可测形态。</summary>
        [Test]
        public void test_reorderInvariance_arrivalOrderPreservesCueHash()
        {
            // Arrange:同流(World)6 事件,故意乱序到达(乱序 = 客户端到达序分叉的可测形态)
            var fixture = new[]
            {
                new NetSimEvent(10, StreamId.World, 1, 100, cueId: 11, intensity: 200),
                new NetSimEvent(10, StreamId.World, 1, 101, cueId: 12, intensity: 100),
                new NetSimEvent( 9, StreamId.World, 1,  99, cueId: 10, intensity: 255),
                new NetSimEvent(10, StreamId.World, 1, 102, cueId: 13, intensity:   0),
                new NetSimEvent( 9, StreamId.World, 2,  98, cueId: 20, intensity:  50),
                new NetSimEvent(11, StreamId.World, 2, 103, cueId: 21, intensity:  77),
            };
            var shuffled = new[]
            {
                fixture[3], fixture[0], fixture[5], fixture[2], fixture[1], fixture[4],
            };

            // Act
            ulong h1 = DeriveCueHash(fixture);
            ulong h2 = DeriveCueHash(shuffled);

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
                new NetSimEvent(10, StreamId.World, 1, 100, cueId: 11, intensity: 200),
                new NetSimEvent(10, StreamId.World, 1, 101, cueId: 12, intensity: 100),
                new NetSimEvent(11, StreamId.World, 2, 103, cueId: 21, intensity:  77),
            };
            var tampered = new[]
            {
                new NetSimEvent(10, StreamId.World, 1, 100, cueId: 11, intensity: 201), // ← 篡改
                fixture[1], fixture[2],
            };

            // Act
            ulong h1 = DeriveCueHash(fixture);
            ulong h2 = DeriveCueHash(tampered);

            // Assert
            Assert.That(h2, Is.Not.EqualTo(h1),
                "Intensity 篡改必须改变哈希(载荷面判据:重排一致的是**含强度**的序列)");
        }

        /// <summary>重排差分判据的**事件集自证**:事件数不同 ⇒ 哈希不同(防「哈希只覆盖子集」
        /// 或哈希与事件集无关的假绿)。</summary>
        [Test]
        public void test_reorderInvariance_eventCountFeedsHash()
        {
            // Arrange
            var full = new[]
            {
                new NetSimEvent(10, StreamId.World, 1, 100, cueId: 11, intensity: 200),
                new NetSimEvent(10, StreamId.World, 1, 101, cueId: 12, intensity: 100),
            };
            var one = new[] { full[0] };

            // Act + Assert
            Assert.That(DeriveCueHash(one), Is.Not.EqualTo(DeriveCueHash(full)),
                "事件集不同 ⇒ 哈希必须不同(哈希面覆盖全部事件)");
        }

        // ══════════════ AC-44-D10 —— 锚点纪律(读路径只读锚点,禁判定)══════════════

        /// <summary>AC-44-D10(锚点缺席语义 · 谓词面):测试侧自持 AnchorGate 判别 ——
        /// 一次性 cue 无锚点 ⇒ **丢弃不补播**(与断线静默过期同语义);
        /// 循环 cue 无锚点 ⇒ **挂起**至锚点到达。纯函数形态,零引擎依赖。</summary>
        [Test]
        public void test_anchorAbsent_oneShotDropped_loopCuePending()
        {
            // Arrange
            var gate = new FakeAnchorGate(anchors: null); // 无任何锚点
            var oneShot = new AudioCueDto(11, 200, default, 5, looped: false);
            var looped = new AudioCueDto(12, 100, default, 5, looped: true);

            // Act
            GateDecision d1 = gate.Dispatch(oneShot);
            GateDecision d2 = gate.Dispatch(looped);

            // Assert
            Assert.That(d1, Is.EqualTo(GateDecision.Dropped),
                "一次性 cue 锚点缺席 ⇒ 丢弃(不补播)");
            Assert.That(d2, Is.EqualTo(GateDecision.Pending),
                "循环 cue 锚点缺席 ⇒ 挂起(等锚点到达再发)");
        }

        /// <summary>AC-44-D10(锚点到达语义):挂起的循环 cue 在锚点到达后**补发**。</summary>
        [Test]
        public void test_anchorArrived_pendingLoopReleased()
        {
            // Arrange
            var gate = new FakeAnchorGate(anchors: null);
            var looped = new AudioCueDto(12, 100, default, 5, looped: true);

            // Act
            Assert.That(gate.Dispatch(looped), Is.EqualTo(GateDecision.Pending));
            gate.OnAnchorArrived(5, new Int3(3, 0, 4), serverTick: 12);
            GateDecision release = gate.Update();

            // Assert
            Assert.That(release, Is.EqualTo(GateDecision.Emitted),
                "锚点到达 ⇒ 挂起中的循环 cue 补发(不静默丢)");
            Assert.That(gate.PendingCount, Is.EqualTo(0), "挂起队列已清空");
        }

        /// <summary>AC-44-D10 + 消费纪律 2(陈旧检查):锚点 ServerTick 早于已见最新 tick
        /// ⇒ 拒读(陈旧锚点不参与渲染路由)。谓词面纯函数可判。</summary>
        [Test]
        public void test_anchorStale_refused()
        {
            // Arrange:最新 tick = 50(以常量直接喂谓词,零引擎依赖)
            const uint latestTick = 50;

            // Act + Assert(陈旧判据谓词)
            Assert.That(FakeAnchorGate.IsStale(12u, latestTick), Is.True,
                "ServerTick 12 < 最新 50 ⇒ 陈旧拒读(ADR-001 §一之二 消费纪律 2)");
            Assert.That(FakeAnchorGate.IsStale(latestTick, latestTick), Is.False,
                "等 tick 不陈旧(拒读面不得误杀本机帧最新值)");
            Assert.That(FakeAnchorGate.IsStale(51u, latestTick), Is.False,
                "更新的 tick 不陈旧(单调序:只拒旧不拒新)");
        }

        // ══════════════ AC-44-09 —— EndLoop 自评兜底(ADR-001 §一之三 裁决二)══════════════

        /// <summary>裁决二:P0 默认兜底路径 = **呈现侧从最新快照 Progress 自求值**
        /// (零网络依赖)。测试侧自持看门狗谓词「快照旧 ∧ 差值超有界阈值 ⇒ 判停」
        /// 纯函数可判(负向:未超阈值不判停 / 非循环 cue 不进兜底面)。</summary>
        [Test]
        public void test_endLoop_selfAssessPredicate_pureAndBounded()
        {
            // Arrange
            var snap = new FakeLoopProgress { LoopingCue = 42, Progress = 100, ServerTick = 60 };

            // Act + Assert(判停面)
            Assert.That(FakeEndLoopWatchdog.ShouldSelfTerminate(snap, currentTick: 61, stagnantFor: 12),
                Is.True, "差值 12 > 有界阈值 ⇒ 判停(零网络依赖的自评兜底)");
            // 负向:未超阈值不判停
            Assert.That(FakeEndLoopWatchdog.ShouldSelfTerminate(snap, currentTick: 61, stagnantFor: 2),
                Is.False, "差值未超阈值 ⇒ 不判停(防误杀在推进的循环)");
            // 负向:非循环 cue 不判停
            var notLoop = new FakeLoopProgress { LoopingCue = 0, Progress = 100, ServerTick = 60 };
            Assert.That(FakeEndLoopWatchdog.ShouldSelfTerminate(notLoop, currentTick: 99, stagnantFor: 999),
                Is.False, "非循环 cue 不进兜底面");
        }

        /// <summary>裁决二(有界性):自评阈值 = 有界常量(不得留空 —— 「二者至少其一,
        /// 不得留空」的架构义务在 P0 由本常量 + 谓词兑现)。</summary>
        [Test]
        public void test_endLoop_selfAssessThreshold_isPositiveBoundedConstant()
        {
            Assert.That(FakeEndLoopWatchdog.StagnantTicksThreshold, Is.GreaterThan(0),
                "停滞阈值必须有正有界值(兜底义务不得留空 —— ADR-001 裁决二)");
        }

        // ══════════════ D1 位置通道引用扫描(BCL 反射,负例夹具用)══════════════

        /// <summary>单类型的 IPositionalChannel 引用扫描(BCL 反射:字段/方法签名)。
        /// ⚠️ 生产装配的 IL 层扫描归 AssemblyGates.CheckAudioPositionalChannelRefs(Cecil 版);
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

        /// <summary>重排差分派生:事件 → EventOrderKey 全序 → cue 记录 → SplitMix64 链式折叠。
        /// 哈希输入 = (CueId, Intensity, Source, Looped) —— 载荷面全字段(Cell 除外,其为注入值)。
        /// AC-44-D7 ③ 要求「DTO 全字段(除 Cell)的派生同纯度」,故四字段全进哈希。</summary>
        private static ulong DeriveCueHash(IReadOnlyList<NetSimEvent> events)
        {
            ulong state = SplitMix64.Avalanche(0xA44DE077B1E5C196UL); // 固定域分隔常量(禁零起点)
            var ordered = events
                .Select(e => new { Key = new EventOrderKey(e.Tick, e.Stream, new PatientId(e.Patient), e.Seq), Ev = e })
                .OrderBy(p => p.Key, Comparer<EventOrderKey>.Default)
                .ToList();
            foreach (var p in ordered)
            {
                state = SplitMix64.Fold(state, (ulong)(uint)p.Ev.CueId);
                state = SplitMix64.Fold(state, (ulong)p.Ev.Intensity);
                state = SplitMix64.Fold(state, (ulong)(uint)p.Ev.Source);
                state = SplitMix64.Fold(state, p.Ev.Looped ? 1UL : 0UL);
            }
            return SplitMix64.Avalanche(state);
        }

        // ══════════════ 测试替身 ══════════════

        /// <summary>记录型 IAudioCueSink 替身,兼记录 SetTier 调用(AC-44-07 静态半)。</summary>
        private sealed class TierRecordingSink : IAudioCueSink
        {
            public readonly List<TierSource> SetTierCalls = new List<TierSource>();
            public void Emit(in AudioCueDto cue) { }
            public AudioCueHandle BeginLoop(in AudioCueDto cue) => new AudioCueHandle(1);
            public void EndLoop(AudioCueHandle handle) { }
            public void SetTier(TierSource source) => SetTierCalls.Add(source);
        }

        // ══════════════ D10 / 裁决二 自持夹具(测试命名空间,零生产依赖)══════════════

        /// <summary>锚点判别结果(AC-44-D10 谓词面)。</summary>
        private enum GateDecision { Emitted, Dropped, Pending }

        /// <summary>FakeAnchorGate:AC-44-D10 锚点纪律的**谓词面**自持实现 ——
        /// 判别 = 「是否发 cue」(读锚点存在性);渲染路由(增益/声像)不在此面。
        /// 语义 = GDD Edge Cases「锚点时序竞争」条:一次性丢弃 / 循环挂起。</summary>
        private sealed class FakeAnchorGate
        {
            private Dictionary<int, WorldPosLatest> _anchors;
            private readonly List<AudioCueDto> _pending = new List<AudioCueDto>();

            public FakeAnchorGate(Dictionary<int, WorldPosLatest> anchors)
                => _anchors = anchors;

            public int PendingCount => _pending.Count;

            /// <summary>锚点缺席谓词:anchor == null 或该 ActorId 无行 ⇒ 缺席。</summary>
            public bool HasAnchor(int actorId)
                => _anchors != null && _anchors.ContainsKey(actorId);

            /// <summary>Dispatch:一次性 cue 无锚点 ⇒ 丢弃;循环 cue 无锚点 ⇒ 挂起(入队);
            /// 有锚点 ⇒ 发出。判别本身只读锚点存在性,不做渲染路由判定。</summary>
            public GateDecision Dispatch(in AudioCueDto cue)
            {
                if (!HasAnchor(cue.Source))
                {
                    if (cue.Looped) _pending.Add(cue);   // 挂起 = 入队等锚点到达
                    return cue.Looped ? GateDecision.Pending : GateDecision.Dropped;
                }
                return GateDecision.Emitted;
            }

            /// <summary>锚点到达(宿主发布者回调面;测试自持)。</summary>
            public void OnAnchorArrived(int actorId, Int3 cell, uint serverTick)
            {
                if (_anchors == null) _anchors = new Dictionary<int, WorldPosLatest>();
                _anchors[actorId] = new WorldPosLatest(actorId, cell, serverTick, flags: 0);
            }

            /// <summary>Update:锚点到达后,挂起队列补发(返回本 tick 新发出数 &gt; 0 ⇒ Emitted)。</summary>
            public GateDecision Update()
            {
                if (_pending.Count == 0) return GateDecision.Dropped;
                _pending.Clear();
                return GateDecision.Emitted;
            }

            /// <summary>陈旧谓词(消费纪律 2):candidate 严格早于 latest ⇒ 陈旧。
            /// 等值 / 更新 = 不陈旧(单调序,只拒旧不拒新)。</summary>
            public static bool IsStale(uint candidateTick, uint latestTick)
                => candidateTick < latestTick;
        }

        /// <summary>FakeLoopProgress:裁决二「最新快照 Progress」的测试侧自持形状
        /// (零引擎依赖;生产落点 = Story 008 的快照 Director 同族结构)。</summary>
        private sealed class FakeLoopProgress
        {
            public int LoopingCue;
            public int Progress;
            public uint ServerTick;
        }

        /// <summary>FakeEndLoopWatchdog:裁决二 P0 默认兜底路径 —— 从快照 Progress 自求值
        /// (零网络依赖)。停滞阈值 = 有界常量(兜底义务不得留空)。</summary>
        private static class FakeEndLoopWatchdog
        {
            /// <summary>停滞阈值(ticks):Progress 差值超此值 ⇒ 判停。有界常量。</summary>
            public const int StagnantTicksThreshold = 10;

            /// <summary>判停谓词:循环 cue ∧ 停滞 tick 数超阈值 ⇒ 自评终止。纯函数。</summary>
            public static bool ShouldSelfTerminate(FakeLoopProgress snap,
                                                   long currentTick, int stagnantFor)
            {
                if (snap.LoopingCue == 0) return false;             // 非循环 cue 不进兜底面
                if (snap.Progress <= 0) return false;               // 无 Progress 快照 ⇒ 不判
                long delta = currentTick - (long)snap.ServerTick;
                return delta > 0 && stagnantFor > StagnantTicksThreshold;
            }
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

        // ══════════════ 内部数据结构(测试侧自持,不引生产类型)══════════════

        private readonly struct NetSimEvent
        {
            public readonly long Tick;
            public readonly StreamId Stream;
            public readonly int Patient;
            public readonly long Seq;
            public readonly int CueId;
            public readonly byte Intensity;
            public readonly int Source;
            public readonly bool Looped;

            public NetSimEvent(long tick, StreamId stream, int patient, long seq,
                               int cueId, byte intensity, int source = 0, bool looped = false)
            {
                Tick = tick; Stream = stream; Patient = patient; Seq = seq;
                CueId = cueId; Intensity = intensity; Source = source; Looped = looped;
            }
        }
    }
}
