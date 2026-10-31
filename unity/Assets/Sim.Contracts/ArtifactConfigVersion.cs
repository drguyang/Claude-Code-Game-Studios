// ADR-037 §②/§④ —— 逐份产物 `ConfigVersion` 与其差异集(存档 v3 配置版本段的**域类型**)。
//
// 权威来源:
//   docs/architecture/adr-037-save-v3-config-version-segment.md
//   ADR-014 §五(逐产物 `ConfigVersion` = 该产物源文本的内容哈希)· ADR-010 §七(非致命 + 供排查)
//   `DataCoreConfigVersion.Combine`(组合值;本件与之**同源**,见 ADR-037 §⑦)
//
// 住 `Sim.Contracts` 的理由:两端都要看得见 —— 写/读侧在 `Sim.Codec`(存档段),
// 标注侧在 `Telemetry`(51 的版本门),二者唯一的公共引用集就是本装配。
//
// ⚠️ **「未知」与「无差异」必须可分**(ADR-037 §④):`ConfigVersionDiff.Unknown`
// 的 `HasPerArtifactData = false`,与「有数据但集合为空」是两件事。
// 把前者写成后者 = 把「本档早于 v3,不知道哪张表变了」报告成「配置完全一致」。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>一份 data-core 产物的 address 与其 `ConfigVersion`(ADR-037 §②)。</summary>
    public readonly struct ArtifactConfigVersion
    {
        /// <summary>产物 address(如 <c>DataCorePreloader.DataCoreAddresses</c> 里的串)。</summary>
        public readonly string Address;

        /// <summary>该产物头部携带的 <c>ConfigVersion</c>(u32)。</summary>
        public readonly uint Version;

        /// <summary>构造。</summary>
        /// <exception cref="ArgumentNullException">address 为 null。</exception>
        /// <exception cref="ArgumentException">address 为空串(空 address 会让归因无意义)。</exception>
        public ArtifactConfigVersion(string address, uint version)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (address.Length == 0)
                throw new ArgumentException("产物 address 不得为空串(空 address 的归因不可读)", nameof(address));
            Address = address;
            Version = version;
        }
    }

    /// <summary>
    /// 当前产物的配置版本快照 = **组合值**(写进存档头)+ **逐份版本**(写进配置版本段)。
    /// <para>两者出自**同一次装载**(ADR-037 §⑦)⇒ 不可能互相矛盾;任何把它们分开取的用法
    /// 都会重新引入「组合值与逐份段不同源」的失败模式。</para>
    /// </summary>
    public readonly struct ConfigVersionSnapshot
    {
        /// <summary>组合值(ADR-014 §五 · `DataCoreConfigVersion.Combine`)。</summary>
        public readonly uint Combined;

        /// <summary>逐份版本(升序 Ordinal)。<c>null</c> ⇒ 本方无逐份数据 ⇒ 差异只能报「未知」。</summary>
        public readonly IReadOnlyList<ArtifactConfigVersion> Artifacts;

        /// <summary>构造。</summary>
        public ConfigVersionSnapshot(uint combined, IReadOnlyList<ArtifactConfigVersion> artifacts)
        {
            Combined = combined;
            Artifacts = artifacts;
        }
    }

    /// <summary>
    /// 「存档记录的逐份版本」与「当前产物的逐份版本」的差异集(ADR-037 §④)。
    /// <para><b>三种结论,不可混读</b>:</para>
    /// <list type="bullet">
    ///   <item><c>HasPerArtifactData = false</c> ⇒ **未知**(旧档不含逐份段)—— 不得报成「无差异」;</item>
    ///   <item><c>HasPerArtifactData = true ∧ HasChanges = false</c> ⇒ **逐份全等**;</item>
    ///   <item><c>HasPerArtifactData = true ∧ HasChanges = true</c> ⇒ **这几张表变了**(见 <see cref="Changed"/>)。</item>
    /// </list>
    /// </summary>
    public readonly struct ConfigVersionDiff
    {
        private readonly IReadOnlyList<string> _changed;
        private readonly bool _hasPerArtifactData;

        private ConfigVersionDiff(IReadOnlyList<string> changed, bool hasPerArtifactData)
        {
            _changed = changed;
            _hasPerArtifactData = hasPerArtifactData;
        }

        /// <summary>本档**是否携带**逐份版本(ADR-037 ②:`count = 0` 的段 = 未知,不是无差异)。</summary>
        public bool HasPerArtifactData => _hasPerArtifactData;

        /// <summary>变了的产物 address(升序 Ordinal;无逐份数据时为空 —— 调用方须先看
        /// <see cref="HasPerArtifactData"/>,不得只看本属性)。</summary>
        public IReadOnlyList<string> Changed => _changed ?? Array.Empty<string>();

        /// <summary>有逐份数据**且**确有差异。</summary>
        public bool HasChanges => _hasPerArtifactData && Changed.Count > 0;

        /// <summary>
        /// 「未知」—— 本档早于 v3(或迁移时补不出逐份版本)。**刻意取 `default`**:
        /// 其默认语义是「不知道」而非「没变化」,故任何忘记赋值的路径都落在安全侧。
        /// </summary>
        public static ConfigVersionDiff Unknown => default;

        /// <summary>
        /// 比对两侧逐份版本,产出差异集。**纯函数**,零 I/O、零静态态。
        /// <para>差异判据:同一 address 两侧版本不等,或只在一侧出现(**新增 / 移除**都算差异)。</para>
        /// </summary>
        /// <exception cref="ArgumentNullException">任一入参为 null(不得以「未知」冒充 null —— 那是静默降级)。</exception>
        public static ConfigVersionDiff Compare(IReadOnlyList<ArtifactConfigVersion> saved,
                                                IReadOnlyList<ArtifactConfigVersion> current)
        {
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            if (current == null) throw new ArgumentNullException(nameof(current));

            var savedMap = new Dictionary<string, uint>(saved.Count, StringComparer.Ordinal);
            for (int i = 0; i < saved.Count; i++) savedMap[saved[i].Address] = saved[i].Version;

            var changed = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < current.Count; i++)
            {
                ArtifactConfigVersion c = current[i];
                seen.Add(c.Address);
                if (!savedMap.TryGetValue(c.Address, out uint v) || v != c.Version)
                    changed.Add(c.Address);
            }
            // 存档里有、当前产物没有 ⇒ 「已移除」,同样算差异(不能只看 current 一侧)。
            for (int i = 0; i < saved.Count; i++)
            {
                if (!seen.Contains(saved[i].Address)) changed.Add(saved[i].Address);
            }

            changed.Sort(StringComparer.Ordinal);
            return new ConfigVersionDiff(changed, hasPerArtifactData: true);
        }
    }
}
