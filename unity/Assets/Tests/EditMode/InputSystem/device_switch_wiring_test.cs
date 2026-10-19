// TD-epic-01 第十一片 · 设备切换六旋钮的**烘焙管线 + 装载接缝**夹具
// (对账断言 / 真往返 / deterministic / E-13 负例 / 取值缝两分支)。
//
// 权威来源:
//   ADR-014 §二(同源两次烘焙逐位一致)· §三(两阶段烘焙)· §五(ConfigVersion 内容哈希派生 ·
//     E-13 启动期硬失败,零降级)
//   ADR-025 §①(程序集装订:`Editor.Tools.Bake` 与 `Gameplay.Presentation` 两侧布局严格镜像)
//   GDD input-system.md §Tuning Knobs 一之二 · AC-3-D4(三旋钮 × 两设备类列)
//   TD-epic-01(51 遥测 epic 的债项轮)—— 本片还「设备六旋钮临时种子」:
//     此前只有 JSON + binder + 六个手抄常量,**零 cooked 制品、零装载路径、零机械对账**。
//
// ⚠️ 落点:Unity 只编译 `unity/Assets/` 树 ⇒ 真身 = 本文件(承 `device_and_presentation_test`
//    同一先例)。既有种子绑定测试(`test_deviceSwitchSeedTable_bindsAndColumnsDistinct`)不删 —
//    它管 JSON 侧,本件管「JSON → cooked → 运行期 -> 取值缝」整条链,两者互补。
// ⚠️ 确定性:零随机 / 零墙钟 / 不写盘 —— `BakeFromRepo` 只读源,sink 一律住内存。
// ⚠️ **数值不拍**:对账断言只比较「常量 == 烘焙表」,**不**断言任何绝对值 ——
//    值住 `assets/data/input_device_switch.json`(临时种子,待数值轮)。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using DaYiJingCheng.EditorTools.Bake;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.Presentation;          // DeviceSwitchTuning / DeviceSwitchColumn(ADR-025 §① 落点)
using DaYiJingCheng.Gameplay.Presentation.Input;    // DeviceSwitchTuningDataSet / Codec
using DaYiJingCheng.Sim.Contracts;
using NUnit.Framework;

namespace DaYiJingCheng.Tests.Unit.InputSystem
{
    /// <summary>设备切换旋钮的「烘焙 → 装载 → 取值」全链夹具(TD-epic-01 第十一片)。</summary>
    [TestFixture]
    public sealed class DeviceSwitchWiringTest
    {
        private static readonly string RepoRoot = ComputeRepoRoot();

        private static string ComputeRepoRoot([CallerFilePath] string callerPath = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(callerPath), "..", "..", "..", "..", ".."));

        /// <summary>六常量 ↔ 烘焙表**逐值对账**(本片的核心判据)。
        /// <para>判别力:任一侧数值轮只改一边(JSON 改了常量没改 / 常量改了 JSON 没改)⇒ 本条红。
        /// 这是「临时种子」还债前**不存在**的机械约束 —— 彼时两边同值纯属手抄巧合。</para></summary>
        [Test]
        public void test_reconciliation_emergencyConstantsEqualBakedTable()
        {
            // Arrange
            var bound = InputDeviceSwitchBake.BindFromRepo(RepoRoot);

            // Act + Assert:逐值(不整体比结构 —— 失败报文须点名是哪一个旋钮)
            AssertReconciled("axis.device_switch_threshold",
                CompositionRoot.EmergencyDeviceSwitchSeedAxisThreshold, bound.Axis.DeviceSwitchThreshold.Raw);
            AssertReconciled("axis.drift_tolerance",
                CompositionRoot.EmergencyDeviceSwitchSeedAxisTolerance, bound.Axis.DriftTolerance.Raw);
            AssertReconciled("axis.dwell_ticks",
                CompositionRoot.EmergencyDeviceSwitchSeedAxisDwell, bound.Axis.DwellTicks);
            AssertReconciled("pointer.device_switch_threshold",
                CompositionRoot.EmergencyDeviceSwitchSeedPointerThreshold, bound.Pointer.DeviceSwitchThreshold.Raw);
            AssertReconciled("pointer.drift_tolerance",
                CompositionRoot.EmergencyDeviceSwitchSeedPointerTolerance, bound.Pointer.DriftTolerance.Raw);
            AssertReconciled("pointer.dwell_ticks",
                CompositionRoot.EmergencyDeviceSwitchSeedPointerDwell, bound.Pointer.DwellTicks);
        }

        private static void AssertReconciled(string knob, int constant, long baked)
            => Assert.That(baked, Is.EqualTo(constant),
                $"兜底常量与烘焙表分叉:{knob} —— CompositionRoot.EmergencyDeviceSwitchSeed* = {constant}," +
                $" assets/data/input_device_switch.json = {baked}。" +
                "数值轮改数据须同改常量(或按当时裁定删除兜底);承 44 侧 ValidateValueReconciliation 同构形态");

        /// <summary>真往返:写方字节 → 读方解码 → 逐项等值 + 头契约。
        /// <para>判别力:两侧布局分叉(字段顺序 / 字节宽 / 头槽)⇒ `Materialize` 抛或值不等 ⇒ 红。</para></summary>
        [Test]
        public void test_bake_materialize_roundTripExact()
        {
            // Arrange
            InputDeviceSwitchBake.BakeOutput output = InputDeviceSwitchBake.BakeFromRepo(RepoRoot);

            // Act
            DeviceSwitchTuningDataSet dataSet = InputDeviceSwitchBake.Materialize(output);

            // Assert:头契约(cooked 头 20 B + schemaVersion 非 0)+ 两列三旋钮逐项
            Assert.That(output.Cooked.Length, Is.EqualTo(CookedFormat.HeaderSize + 4 + 2 + 2 * DeviceSwitchTuningCookedCodec.ColumnBytes),
                "载荷 = u32 schema_version + 2 × u8 列标志 + 两列 × 20 B");
            Assert.That(dataSet.SchemaVersion, Is.EqualTo(DeviceSwitchTuningCookedCodec.ExpectedSchemaVersion));
            Assert.That(dataSet.ConfigVersion, Is.EqualTo(output.ConfigVersion));
            Assert.That(dataSet.Tuning.Pointer.DeviceSwitchThreshold.Raw,
                Is.EqualTo(output.Bound.Pointer.DeviceSwitchThreshold.Raw), "pointer.threshold 往返丢值");
            Assert.That(dataSet.Tuning.Axis.DwellTicks,
                Is.EqualTo(output.Bound.Axis.DwellTicks), "axis.dwell 往返丢值");

            // 位置断言:载荷首列 = pointer(列序固定 pointer → axis)。
            // ⚠️ 只靠读方回读**测不出**列序对调 —— 写读两侧同时换序则往返仍等值;
            //    故这里按**绝对偏移**钉一次(i64 @ 20 + 4 + 2 = 26)。
            long firstColumnThreshold = BitConverter.ToInt64(output.Cooked, CookedFormat.HeaderSize + 4 + 2);
            Assert.That(firstColumnThreshold, Is.EqualTo(output.Bound.Pointer.DeviceSwitchThreshold.Raw),
                "载荷首列必须是 pointer(列序固定);读到 axis 值 = 写方列序变了");
        }

        /// <summary>头槽 schemaVersion 不得硬写 0(承 DelayTableCookedWriter 的 R-1 教训)。</summary>
        [Test]
        public void test_bake_headerSchemaSlot_notZero_equalsContract()
        {
            InputDeviceSwitchBake.BakeOutput output = InputDeviceSwitchBake.BakeFromRepo(RepoRoot);
            uint headerSchema = ReadU32LE(output.Cooked, 8);
            Assert.That(headerSchema, Is.EqualTo(CookedFormat.SchemaVersion), "头槽 schemaVersion 须写真值");
            Assert.That(headerSchema, Is.Not.EqualTo(0u), "硬写 0 是被既往轮次抓过的缺陷形态(R-1)");
        }

        /// <summary>同源两次烘焙**逐位一致**(ADR-014 §二:固定字段序 · 零字典迭代 · 零本地时间)。</summary>
        [Test]
        public void test_bake_deterministic_sameSourceTwiceBitwiseEqual()
        {
            string json = File.ReadAllText(Path.Combine(RepoRoot, InputDeviceSwitchBake.DataDirName,
                InputDeviceSwitchBake.SourceFileName));
            byte[] first = InputDeviceSwitchBake.BakeFromJson(json).Cooked;
            byte[] second = InputDeviceSwitchBake.BakeFromJson(json).Cooked;
            Assert.That(second, Is.EqualTo(first), "同源两次烘焙字节不等 ⇒ 编码路径含非确定性来源");
        }

        /// <summary>源里两列对调是**语义无变化的编辑** ⇒ 一旦绑定值相同,字节必须逐位一致
        /// (证明编码路径不依赖 JSON 迭代序 —— 列序固定 pointer → axis)。</summary>
        [Test]
        public void test_bake_columnOrderFixed_independentOfJsonOrder()
        {
            const string pointerFirst = @"{ ""schema_version"": 1,
                ""pointer"": { ""device_switch_threshold"": ""4"", ""drift_tolerance"": ""1"", ""dwell_ticks"": 4 },
                ""axis"":    { ""device_switch_threshold"": ""1/2"", ""drift_tolerance"": ""1/8"", ""dwell_ticks"": 4 } }";
            const string axisFirst = @"{ ""schema_version"": 1,
                ""axis"":    { ""device_switch_threshold"": ""1/2"", ""drift_tolerance"": ""1/8"", ""dwell_ticks"": 4 },
                ""pointer"": { ""device_switch_threshold"": ""4"", ""drift_tolerance"": ""1"", ""dwell_ticks"": 4 } }";

            byte[] a = InputDeviceSwitchBake.BakeFromJson(pointerFirst).Cooked;
            byte[] b = InputDeviceSwitchBake.BakeFromJson(axisFirst).Cooked;

            // ConfigVersion 不同(源文本哈希)⇒ 比载荷段,不比头槽 12
            byte[] payloadA = a.Skip(CookedFormat.HeaderSize).ToArray();
            byte[] payloadB = b.Skip(CookedFormat.HeaderSize).ToArray();
            Assert.That(payloadB, Is.EqualTo(payloadA), "两列对调 ⇒ 载荷须逐位一致(列序固定)");
        }

        /// <summary>取值缝:provider 非空 ⇒ 取烘焙表(真源),**不是**常量兜底。</summary>
        [Test]
        public void test_resolveKnobs_withProvider_takesBakedTable()
        {
            // Arrange:用一份与兜底**不同**的合法表(让两条路径可判别)
            var provider = new SingleTableProvider(new DeviceSwitchTuning(
                new DeviceSwitchColumn(new Fix(196608), new Fix(32768), 5),   // pointer 3 / 1/2 / 5
                new DeviceSwitchColumn(new Fix(49152), new Fix(16384), 6)));  // axis   3/4 / 1/4 / 6

            // Act
            var knobs = BootRoot.ResolveDeviceSwitchKnobs(provider);

            // Assert:序 = axis 三 + pointer 三
            Assert.That(knobs, Is.EqualTo((49152, 16384, 6, 196608, 32768, 5)));
            Assert.That(knobs, Is.Not.EqualTo((CompositionRoot.EmergencyDeviceSwitchSeedAxisThreshold,
                CompositionRoot.EmergencyDeviceSwitchSeedAxisTolerance,
                CompositionRoot.EmergencyDeviceSwitchSeedAxisDwell,
                CompositionRoot.EmergencyDeviceSwitchSeedPointerThreshold,
                CompositionRoot.EmergencyDeviceSwitchSeedPointerTolerance,
                CompositionRoot.EmergencyDeviceSwitchSeedPointerDwell)),
                "provider 非空时必须走烘焙表 —— 取到兜底值 = 接线没接上(第十一片前的老行为)");
        }

        /// <summary>取值缝:provider 为 null(预载失败)⇒ 常量兜底(设计内降级面)。</summary>
        [Test]
        public void test_resolveKnobs_nullProvider_fallsBackToConstants()
        {
            var knobs = BootRoot.ResolveDeviceSwitchKnobs(null);
            Assert.That(knobs, Is.EqualTo((
                CompositionRoot.EmergencyDeviceSwitchSeedAxisThreshold,
                CompositionRoot.EmergencyDeviceSwitchSeedAxisTolerance,
                CompositionRoot.EmergencyDeviceSwitchSeedAxisDwell,
                CompositionRoot.EmergencyDeviceSwitchSeedPointerThreshold,
                CompositionRoot.EmergencyDeviceSwitchSeedPointerTolerance,
                CompositionRoot.EmergencyDeviceSwitchSeedPointerDwell)));
        }

        // ═══════════════ E-13 负例(cooked 损坏 ⇒ 启动期硬失败,零降级)═══════════════

        /// <summary>头槽 schemaVersion 被改成 0 ⇒ 读方拒收(不见值、不静默取默认)。</summary>
        [Test]
        public void test_codec_headerSchemaZero_e13()
        {
            byte[] cooked = InputDeviceSwitchBake.BakeFromRepo(RepoRoot).Cooked;
            WriteU32LE(cooked, 8, 0);
            AssertRejected(cooked, "schemaVersion");
        }

        /// <summary>头槽与载荷 schema_version 分叉(同一真值两处存)⇒ 拒收。</summary>
        [Test]
        public void test_codec_payloadSchemaMismatch_e13()
        {
            byte[] cooked = InputDeviceSwitchBake.BakeFromRepo(RepoRoot).Cooked;
            WriteU32LE(cooked, CookedFormat.HeaderSize, 2);   // 载荷首字段
            AssertRejected(cooked, "schema_version");
        }

        /// <summary>列标志为 0(上游绕过写方)⇒ 拒收 —— 空列会让运行期静默回退到常量兜底。</summary>
        [Test]
        public void test_codec_missingColumnFlag_e13()
        {
            byte[] cooked = InputDeviceSwitchBake.BakeFromRepo(RepoRoot).Cooked;
            cooked[CookedFormat.HeaderSize + 4] = 0;   // has_pointer
            AssertRejected(cooked, "列缺失");
        }

        /// <summary>载荷截断(三处截法:头内 / 列中 / 尾部)⇒ 一律拒收。</summary>
        [Test]
        public void test_codec_truncatedPayload_e13()
        {
            byte[] full = InputDeviceSwitchBake.BakeFromRepo(RepoRoot).Cooked;

            AssertRejected(full.Take(10).ToArray(), "短于头部");
            AssertRejected(full.Take(full.Length - 6).ToArray(), "payloadLength");
            AssertRejected(full.Take(full.Length - 1).ToArray(), "payloadLength");
        }

        /// <summary>魔数损坏 ⇒ 拒收。</summary>
        [Test]
        public void test_codec_badMagic_e13()
        {
            byte[] cooked = InputDeviceSwitchBake.BakeFromRepo(RepoRoot).Cooked;
            cooked[0] = (byte)'X';
            AssertRejected(cooked, "魔数");
        }

        private static void AssertRejected(byte[] bytes, string expectedFragment)
        {
            var ex = Assert.Throws<InvalidOperationException>(() => DeviceSwitchTuningCookedCodec.Read(bytes));
            StringAssert.Contains("[E-13][input_device_switch]", ex.Message, "须带 E-13 硬失败前缀");
            StringAssert.Contains(expectedFragment, ex.Message, "失败报文须点名具体损坏面(可诊断)");
        }

        // ══════════════════════════════ 夹具 ══════════════════════════════

        /// <summary>最小 `IDataProvider` 替身:只认 `DeviceSwitchTuningDataSet`(其余类型抛 ——
        /// 本夹具不该被其它类型问到;真问 = 接线错)。</summary>
        private sealed class SingleTableProvider : IDataProvider
        {
            private readonly DeviceSwitchTuningDataSet _dataSet;
            public SingleTableProvider(in DeviceSwitchTuning tuning)
            {
                _dataSet = new DeviceSwitchTuningDataSet(tuning, DeviceSwitchTuningCookedCodec.ExpectedSchemaVersion, 0);
            }

            public TDataSet Load<TDataSet>() where TDataSet : struct
            {
                object boxed = _dataSet;
                return (TDataSet)boxed;
            }
        }

        private static uint ReadU32LE(byte[] b, int offset)
            => (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

        private static void WriteU32LE(byte[] b, int offset, uint value)
        {
            b[offset] = (byte)(value & 0xFF);
            b[offset + 1] = (byte)((value >> 8) & 0xFF);
            b[offset + 2] = (byte)((value >> 16) & 0xFF);
            b[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
