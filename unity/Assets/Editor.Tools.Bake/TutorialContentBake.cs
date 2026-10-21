// S17-3 · story-001 —— 48 教学内容两阶段烘焙的**仓根装载器**(阶段 1 词法 + 阶段 2 绑定 + 编码)。
//
// 权威来源:ADR-014 §三/§五(两阶段烘焙入口 · ConfigVersion = 源数据集内容哈希派生 u32)
//          · TutorialContentBinder(唯一构建期校验点)· TutorialContentCookedWriter(编码面)
//
// ⚠️ 本件刻意**不碰 UnityEngine / Addressables**:纯文件 I/O + 绑定 + 编码
//   (承 DelayTable53Bake / OutcomeTable53 同纪律)⇒ EditMode 测试可直接驱动。
//
// ⚠️ 装载面 = 与 53_delay_table **完全同构**:不进 AddressablesDataProvider / DataCorePreloader,
//   cooked 不入库;运行期真装载归装配轮(AC 第 5 条「运行期只读 .cooked」的兑现不属本 story)。
//
// ⚠️ **ConfigVersion** = 对「(源文件名, 源文本)」做 FNV-1a 32(ConfigVersionUtility)。
//   同源两次烘焙 ⇒ 同值;改一个数值 ⇒ 变值。与 ADR-010 §七 的存档头比对分离(不匹配非致命)。

using System;
using System.Collections.Generic;
using System.IO;

namespace DaYiJingCheng.EditorTools.Bake
{
    /// <summary>48 教学内容烘焙装载器(Bind + cooked 字节)。</summary>
    public static class TutorialContentBake
    {
        /// <summary>源文件名(单一出处;测试与菜单共用)。</summary>
        public const string SourceFileName = TutorialContentSchema.SourceFileName;

        /// <summary>源文件目录(相对仓根)。</summary>
        public const string DataDirName = TutorialContentSchema.DataDirName;

        /// <summary>烘焙输出(cooked 字节 + 绑定结果)。</summary>
        public readonly struct BakeOutput
        {
            /// <summary>cooked 字节(含 20 字节头)。</summary>
            public readonly byte[] Cooked;

            /// <summary>源数据集内容哈希派生的 ConfigVersion(u32)。</summary>
            public readonly uint ConfigVersion;

            /// <summary>绑定结果(六步 / 四区 / 两项扫描结果;测试直用)。</summary>
            public readonly TutorialContentBinder.BindResult Bound;

            /// <summary>构造烘焙输出(写方 / 测试直用)。</summary>
            public BakeOutput(byte[] cooked, uint configVersion, TutorialContentBinder.BindResult bound)
            {
                Cooked = cooked; ConfigVersion = configVersion; Bound = bound;
            }
        }

        /// <summary>从仓根烘焙(读源 → 六项校验 → 编码)。</summary>
        /// <param name="repoRoot">仓库根(含 <c>assets/</c> 的目录)。</param>
        /// <param name="diseaseNames">病名闭集来源;传 <c>null</c> = **用该仓根的默认来源**
        /// (<see cref="TutorialDiseaseNameSource"/>,读 <c>assets/data/disease_names.json</c>)。
        /// <para>⚠️ 2026-10-21 修整轮改:原默认 <c>null</c> = 「未就绪」⇒ **任何忘记注入的调用点都会让第 ④ 项
        /// 静默 NOT-RUN**(漂移对拍测试就是这样一个调用点)。现改为**默认注入**,使「有仓根 ⇒ ④ 必跑」;
        /// 词表真缺席时来源自身返回 <c>false</c>,仍得 NOT-RUN —— 语义不变,但**不再依赖调用方记得传参**。</para>
        /// <para>要刻意证明 NOT-RUN 的夹具请改走 <see cref="BindForTest"/>(显式传 <c>null</c>)。</para></param>
        /// <exception cref="BakeValidationException">词法 / 六项校验任一失败(聚合)。</exception>
        public static BakeOutput BakeFromRepo(string repoRoot, ITutorialDiseaseNameSource diseaseNames = null)
        {
            if (repoRoot == null) throw new ArgumentNullException(nameof(repoRoot));

            string path = Path.Combine(repoRoot, DataDirName, SourceFileName);
            if (!File.Exists(path))
                throw new BakeValidationException(new List<string> { $"源文件不存在:{path}" });

            string json = File.ReadAllText(path);
            return BakeFromJson(json, diseaseNames ?? new TutorialDiseaseNameSource(repoRoot));
        }

        /// <summary>绑定入口(负例直接喂 JSON 串 —— 不落盘;与 <see cref="BakeFromRepo"/> 同一校验体)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static TutorialContentBinder.BindResult BindForTest(
            string json, ITutorialDiseaseNameSource diseaseNames = null)
            => TutorialContentBinder.Bind(json, diseaseNames);

        /// <summary>从 JSON 串烘焙(不落盘;与 <see cref="BakeFromRepo"/> 同一编码路径)。</summary>
        /// <exception cref="BakeValidationException">任一构建期拒收(聚合)。</exception>
        public static BakeOutput BakeFromJson(
            string json, ITutorialDiseaseNameSource diseaseNames = null)
        {
            var bound = TutorialContentBinder.Bind(json, diseaseNames);
            uint configVersion = DeriveConfigVersion(json);
            return new BakeOutput(
                TutorialContentCookedWriter.Write(bound, configVersion), configVersion, bound);
        }

        /// <summary>源文本 → ConfigVersion(u32)。同源两次相等;任一字节变化 ⇒ 变值。</summary>
        public static uint DeriveConfigVersion(string json)
            => ConfigVersionUtility.DeriveConfigVersion(new[]
            {
                new KeyValuePair<string, string>(SourceFileName, json ?? string.Empty),
            });

        /// <summary>本项的六项校验是否全部**真跑过**。
        /// <para>⚠️ 与「烘焙成功」不是同一命题:第 ④ 项在词表缺席时记 NOT-RUN,
        /// 烘焙**仍会成功**(它不阻断构建)。⇒ 任何「六项全过」的宣称须先问本方法。</para></summary>
        public static bool AllSixChecksExecuted(in TutorialContentBinder.BindResult bound)
            => bound.DiseaseScan.Outcome != TutorialDiseaseScanOutcome.TableNotReady;
    }
}
