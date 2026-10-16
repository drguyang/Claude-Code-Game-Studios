// S14-6 · story-010 —— 音频事件表两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码 + 往返物化)。
//
// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生)
//          · AudioEventTableBinder(唯一构建期校验点)· AudioEventTableCookedWriter(编码面)
//          · AudioEventDataSet(运行期表 —— 菜单与测试共用同一台机器,反空转)
//          · 先例 = DelayTable53Bake / OutcomeTable53(同款「仓根装载器」形态)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DelayTable53Bake / AudioSettingsRegistryBake 同纪律)⇒ EditMode 测试可直接驱动。
//
// ⚠️ **本件把「素材存在性」做成路径上的必经点**:`BakeFromRepo` / `BakeFromJson` 都要求
//   `assetRoot`,并一律经 `AudioEventTableBinder.Bind(json, assetRoot)` —— 没有一条路能绕过 D6。
//   (story-010 原稿的假绿正来自「存在性检查住在测试里」:生产链路一次都没跑过它。)
//
// ⚠️ **`Materialize` 走真往返**(cooked 字节 → `AudioEventCookedCodec.Read`),**不是**从绑定
//   结果直构运行期表 —— 直构会让「写方与读方布局不一致」从判据面消失(DelayTable53Bake 的
//   `Materialize` 直构 Sim 侧表,是因为那张表的消费者就在 Sim;本表的消费者是表现层 codec,
//   故必须真解码)。并附**跨侧等值断言**(承 `DelayTable53Bake.AssertDomainDeclarationsMatch` 先例)。

using System;
using System.Collections.Generic;
using System.IO;
using DaYiJingCheng.Gameplay.Presentation.Audio;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary><c>audio_events.json</c> 烘焙装载器(Bind + cooked 字节 + 往返物化的运行期表)。</summary>
    public static class AudioEventTableBake
    {
        /// <summary>烘焙输出(cooked 字节 + 绑定结果 + 往返物化入口)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(表模型 / schema_version;测试直用)。</summary>
            public readonly AudioEventTableBinder.BindResult Bound;

            /// <summary>本次烘焙使用的素材根(诊断用)。</summary>
            public readonly string AssetRoot;

            /// <summary>构造烘焙输出(写方 / 测试直用)。</summary>
            public BakeOutput(byte[] cooked, uint configVersion,
                AudioEventTableBinder.BindResult bound, string assetRoot)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound; AssetRoot = assetRoot;
            }
        }

        /// <summary>从仓根烘焙(读源 → 绑定 + 门 + D6 存在性 → 编码)。</summary>
        /// <param name="repoRoot">仓库根(素材根由 <see cref="AudioAssetExistence.ResolveRoot"/> 派生)。</param>
        /// <exception cref="BakeValidationException">词法 / 门 / 素材缺失任一(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(
                repoRoot, AudioEventTableBinder.DataDirName, AudioEventTableBinder.SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(
                    new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            return Bake(json, AudioAssetExistence.ResolveRoot(repoRoot));
        }

        /// <summary>从 JSON 串烘焙(**与 <see cref="BakeFromRepo"/> 同一校验 + 编码路径**;不落盘)——
        /// 供负向夹具注入缺失素材名 / 指定替身根。</summary>
        /// <exception cref="BakeValidationException">同 <see cref="BakeFromRepo"/>。</exception>
        public static BakeOutput BakeFromJson(string json, string assetRoot) => Bake(json, assetRoot);

        /// <summary>烘焙主干(两条入口共用,单一校验体)。</summary>
        private static BakeOutput Bake(string json, string assetRoot)
        {
            AudioEventTableBinder.BindResult bound = AudioEventTableBinder.Bind(json, assetRoot);

            uint configVersion = ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(AudioEventTableBinder.SourceFileName, json),
            });

            byte[] cooked = AudioEventTableCookedWriter.Write(bound, configVersion);
            return new BakeOutput(cooked, configVersion, bound, assetRoot);
        }

        // ════════════════════════════════════════════════════════════════════
        // 运行期表直产(测试用 —— 反空转;走**真往返**)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>烘焙**并**解码运行期表(cooked 字节 → <see cref="AudioEventCookedCodec.Read"/>)。</summary>
        public static AudioEventDataSet Materialize(BakeOutput output)
            => Materialize(output.Bound, output.Cooked);

        /// <summary>绑定结果 + cooked 字节 → 运行期表(**真往返** + 跨侧等值断言)。</summary>
        /// <param name="bound">阶段 2 绑定结果(交叉比对的**作者态**侧)。</param>
        /// <param name="cooked">cooked 字节(交叉比对的**读方**侧 —— 必须与写方逐位镜像)。</param>
        /// <remarks>⚠️ **跨侧等值断言的由来**(承 `DelayTable53Bake.AssertDomainDeclarationsMatch`):
        /// 「cue ordinal」这一个量同时被两处声明 —— 绑定层的 `Rows[i]` 行序(写方按它编码)与
        /// 解码后的 `Row(i)`(读方按它索引)。两侧分住两程序集,无本断言时,**写方悄悄改了行序
        /// 规则 / 读方悄悄丢了行**,唯一间接兜现是行数类断言 —— 接不住「同数但错位」。
        /// 本件逐项比对 cue 名与 ordinal。</remarks>
        /// <exception cref="InvalidOperationException">往返结果与作者态行集分叉。</exception>
        public static AudioEventDataSet Materialize(
            AudioEventTableBinder.BindResult bound, byte[] cooked)
        {
            AudioEventDataSet runtime = AudioEventCookedCodec.Read(cooked);
            AssertRoundTripMatches(bound, runtime);
            return runtime;
        }

        /// <summary>跨侧等值断言(写方行序 ↔ 读方 ordinal,逐项)。</summary>
        private static void AssertRoundTripMatches(
            AudioEventTableBinder.BindResult bound, AudioEventDataSet runtime)
        {
            IReadOnlyList<Gates.AudioEventTableRow> src = bound.Table?.Rows;
            if (src == null)
                throw new InvalidOperationException("音频事件表跨侧等值断言:绑定侧无行集(不可达)");

            if (src.Count != runtime.RowCount)
                throw new InvalidOperationException(
                    $"音频事件表跨侧等值断言:行数 CROSS-SIDE 不一致 —— 作者态 {src.Count} vs " +
                    $"往返 {runtime.RowCount}(写方 / 读方布局分叉)");

            for (int i = 0; i < src.Count; i++)
            {
                string expected = src[i]?.Cue;
                string actual = runtime.CueAt(i);
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"音频事件表跨侧等值断言:ordinal {i} 的 cue 名分叉 —— 作者态「{expected}」vs " +
                        $"往返「{actual}」(行序 = ordinal 是**语义**,错位 = 全表 cue 全指错)");
            }
        }

        /// <summary>产物文件名(单一出处 = <see cref="AudioEventDataSet.CookedFileName"/>;
        /// 运行期 <c>AddressablesDataProvider</c> 的 address 与菜单写盘名共用本值)。</summary>
        public static string CookedFileName => AudioEventDataSet.CookedFileName;

        /// <summary>头契约自检(读方 <see cref="CookedFormat"/> 的三项:magic / formatVersion / schemaVersion)。</summary>
        /// <exception cref="InvalidOperationException">头不符(写方与 <see cref="CookedFormat"/> 分叉)。</exception>
        public static void AssertHeaderContract(byte[] cooked)
        {
            if (cooked == null || cooked.Length < CookedFormat.HeaderSize)
                throw new InvalidOperationException(
                    $"cooked 字节短于头 {CookedFormat.HeaderSize} B(实得 {cooked?.Length ?? -1})");
            if (cooked[0] != (byte)'D' || cooked[1] != (byte)'Y' || cooked[2] != (byte)'J' || cooked[3] != (byte)'C')
                throw new InvalidOperationException("cooked magic ≠ \"DYJC\"");
            if (ReadU32(cooked, 4) != CookedFormat.FormatVersion)
                throw new InvalidOperationException(
                    $"cooked formatVersion {ReadU32(cooked, 4)} ≠ 读方 {CookedFormat.FormatVersion}");
        }

        private static uint ReadU32(byte[] b, int o)
            => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }
}
