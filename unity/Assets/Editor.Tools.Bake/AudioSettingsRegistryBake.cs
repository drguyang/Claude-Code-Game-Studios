// S14-5 · story-011 —— 设置暴露面注册表的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定)。
//
// 权威来源:ADR-014 §三(两阶段烘焙入口)· AudioSettingsRegistryBinder(唯一构建期校验点)
//          · 先例 = DelayTable53Bake / OutcomeTable53(同款「仓根装载器」形态)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码(承 DelayTable53Bake 同纪律)
//   ⇒ EditMode 测试可直接驱动。
// ⚠️ **cooked 写面 = S14-6(story-010)补齐**。S14-5 原稿刻意留白并登记了理由与归口:
//   「运行期装载面(`AudioSettingsRegistry.Provider` 缝)归 **story-010** 的 Addressables 接线轮,
//    届时若需 cooked 再定编码 —— 本件**不预设**一个没有消费者的编码格式(凭空造格式 = 第二真源)」。
//   本件即该轮:消费者 = `AudioSettingsRegistry.Provider`(经 `AddressablesDataProvider`)。
// ⚠️ **本件不落盘**:绑定 / 编码结果供测试与菜单消费;写盘归 `DataBakeMenu`。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Settings;
using DaYiJingCheng.Sim.Contracts;   // CookedFormat(20 字节头布局的唯一共参照)

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>audio_settings_registry.json</c> 的仓根装载器(Bind + 真种子过门 + cooked 编码)。</summary>
    public static class AudioSettingsRegistryBake
    {
        /// <summary>作者态源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = "audio_settings_registry.json";

        /// <summary>源文件所在目录(相对仓根;承既有先例 <c>assets/data/</c>)。</summary>
        public const string DataDirName = "assets/data";

        /// <summary>cooked 产物文件名(= Addressables address;单一出处 = <see cref="AudioSettingsDataSet"/>)。</summary>
        public static string CookedFileName => AudioSettingsDataSet.CookedFileName;

        /// <summary>烘焙输出(cooked 字节 + ConfigVersion + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(行集 / schema_version;测试直用)。</summary>
            public readonly AudioSettingsRegistryBinder.BindResult Bound;

            /// <summary>构造烘焙输出(写方 / 测试直用)。</summary>
            public BakeOutput(byte[] cooked, uint configVersion, AudioSettingsRegistryBinder.BindResult bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根绑定(读源 → 阶段 1 词法 → 阶段 2 三类校验)。</summary>
        /// <exception cref="BakeValidationException">词法 / 未知键 / 名字或值对账 / 域违例(聚合)。</exception>
        public static AudioSettingsRegistryBinder.BindResult BindFromRepo(string repoRoot)
            => AudioSettingsRegistryBinder.Bind(ReadSource(repoRoot));

        /// <summary>从仓根烘焙(读源 → 绑定 + 三类校验 → 编码)。</summary>
        /// <exception cref="BakeValidationException">词法 / 未知键 / 名字或值对账 / 域违例(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
            => Bake(ReadSource(repoRoot));

        /// <summary>从 JSON 串烘焙(不落盘;**与 <see cref="BakeFromRepo"/> 同一编码路径**)——
        /// 供测试驱动写侧性质(行序无关 / 同源两次逐位一致)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(string json) => Bake(json);

        private static BakeOutput Bake(string json)
        {
            AudioSettingsRegistryBinder.BindResult bound = AudioSettingsRegistryBinder.Bind(json);
            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json),
            });
            byte[] cooked = AudioSettingsRegistryCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound);
        }

        private static string ReadSource(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}(AC-44-13:出厂默认须有烘焙真源)" });

            return File.ReadAllText(path);
        }

        /// <summary>绑定入口(测试负例直接喂 JSON 串 —— 不落盘;与 <see cref="BindFromRepo"/> 同一校验体)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static AudioSettingsRegistryBinder.BindResult BindForTest(string json)
            => AudioSettingsRegistryBinder.Bind(json);

        /// <summary>烘焙输出 → 运行期数据集(**走真往返**:字节经
        /// <see cref="AudioSettingsRegistryCookedCodec.Read"/> 解回,再做跨侧等值断言)。</summary>
        /// <remarks>⚠️ **为什么必须走真往返**:写方与读方是两份手写布局(跨程序集),
        /// 只比 `bound.Rows` 与内存行集**不会**发现布局分叉 —— 那正是 story-002 R-1 的形态。
        /// 本入口是两侧布局的**唯一自动兜现**;跨侧断言逐项比对 param / kind / min / max /
        /// 出厂默认 / player_group / label_source。</remarks>
        /// <exception cref="BakeValidationException">跨侧逐项等值失败(布局分叉 / 编码丢字段)。</exception>
        public static AudioSettingsDataSet Materialize(in BakeOutput output)
        {
            AssertHeaderContract(output.Cooked);
            AudioSettingsDataSet dataSet = AudioSettingsRegistryCookedCodec.Read(output.Cooked);

            if (dataSet.ConfigVersion != output.ConfigVersion)
                throw new BakeValidationException(new List<string>
                {
                    $"cooked 头槽 ConfigVersion 0x{dataSet.ConfigVersion:X8} ≠ 烘焙期派生值 " +
                    $"0x{output.ConfigVersion:X8} —— 编码路径与派生路径分叉",
                });

            AssertRowsMatch(dataSet, output.Bound);
            return dataSet;
        }

        /// <summary>cooked 头契约断言(魔数 / formatVersion / schemaVersion / payloadLength)。</summary>
        /// <exception cref="BakeValidationException">任一头字段不符。</exception>
        public static void AssertHeaderContract(byte[] cooked)
        {
            var errors = new List<string>();
            if (cooked == null || cooked.Length < CookedFormat.HeaderSize)
            {
                errors.Add("产物为 null 或短于头部");
                throw new BakeValidationException(errors);
            }

            string magic = CookedFormat.Magic;
            if (cooked[0] != (byte)magic[0] || cooked[1] != (byte)magic[1] ||
                cooked[2] != (byte)magic[2] || cooked[3] != (byte)magic[3])
                errors.Add("魔数不符(≠ DYJC)");

            uint formatVersion = ReadU32Le(cooked, 4);
            if (formatVersion != CookedFormat.FormatVersion)
                errors.Add($"formatVersion={formatVersion} ≠ {CookedFormat.FormatVersion}");

            uint schemaVersion = ReadU32Le(cooked, 8);
            if (schemaVersion == 0)
                errors.Add("头槽 schemaVersion = 0 —— R-1 形态(真值须住头槽,不得编进载荷首字段)");

            uint payloadLength = ReadU32Le(cooked, 16);
            if (payloadLength != cooked.Length - CookedFormat.HeaderSize)
                errors.Add($"payloadLength={payloadLength} ≠ 实际 {cooked.Length - CookedFormat.HeaderSize}");

            if (errors.Count > 0) throw new BakeValidationException(errors);
        }

        /// <summary>跨侧逐项等值断言(cooked 行集 ↔ Binder 行集,按 param 对账)。</summary>
        private static void AssertRowsMatch(
            in AudioSettingsDataSet dataSet, AudioSettingsRegistryBinder.BindResult bound)
        {
            var errors = new List<string>();
            IReadOnlyList<SettingsRegistryRow> decoded = dataSet.Rows;
            IReadOnlyList<SettingsRegistryRow> binder = bound.Rows;

            if (decoded.Count != binder.Count)
                errors.Add($"cooked 行数 {decoded.Count} ≠ binder 行数 {binder.Count} —— 编码丢行 / 多行");

            var byParam = new Dictionary<string, SettingsRegistryRow>(StringComparer.Ordinal);
            for (int i = 0; i < binder.Count; i++) byParam[binder[i].Param] = binder[i];

            for (int i = 0; i < decoded.Count; i++)
            {
                SettingsRegistryRow d = decoded[i];
                if (!byParam.TryGetValue(d.Param, out SettingsRegistryRow b))
                {
                    errors.Add($"cooked 行「{d.Param}」在 binder 行集中不存在 —— 布局分叉(字段错位)");
                    continue;
                }
                if (d.Kind != b.Kind)
                    errors.Add($"「{d.Param}」kind:cooked={d.Kind} ≠ binder={b.Kind}");
                if (!d.Min.Equals(b.Min) || !d.Max.Equals(b.Max))
                    errors.Add($"「{d.Param}」区间:cooked=[{d.Min},{d.Max}] ≠ binder=[{b.Min},{b.Max}]");
                if (!d.FactoryDefault.Equals(b.FactoryDefault))
                    errors.Add($"「{d.Param}」出厂默认:cooked={d.FactoryDefault} ≠ binder={b.FactoryDefault}");
                if (!string.Equals(d.PlayerGroup, b.PlayerGroup, StringComparison.Ordinal))
                    errors.Add($"「{d.Param}」player_group:cooked={d.PlayerGroup ?? "<null>"} ≠ " +
                               $"binder={b.PlayerGroup ?? "<null>"}");
                if (!string.Equals(d.LabelSource, b.LabelSource, StringComparison.Ordinal))
                    errors.Add($"「{d.Param}」label_source:cooked={d.LabelSource ?? "<null>"} ≠ " +
                               $"binder={b.LabelSource ?? "<null>"}");
            }

            if (errors.Count > 0) throw new BakeValidationException(errors);
        }

        private static uint ReadU32Le(byte[] b, int offset)
            => (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));
    }
}
