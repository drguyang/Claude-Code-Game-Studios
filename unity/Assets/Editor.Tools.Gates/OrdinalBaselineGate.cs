// 权威来源:ADR-014 §五「D-21-13 收窄的承接」承接条 ③(ordinal 映射表 append-only 硬门)
//          · ADR-006 §Decision 二 D-21-13 收窄(载荷内枚举 = 版本化整数 ordinal,`ItemId` 点名)
//          · docs/CLAUDE.md「Never renumber existing IDs — only append new ones」(本门的既有先例)
//
// ⚠️ **本门补的是「有裁定、有承接条、无文件、无门」这一形态** ——
//    ADR-014 承接条 ③ 自 2026-09-17 起就明文要求 `assets/data/_ordinal_baseline.json`
//    与「构建期对基线断言」,但实测(2026-10-26)`grep -rn "_ordinal_baseline"` 只命中
//    ADR / GDD 的**引用**,文件与门**都不存在**。这与 coding-standards.md
//    §Review Evidence「用一句自述顶替一个从未存在的门」同族。
//
// ⚠️ **为什么不靠 `ConfigVersion`**:ADR-010 §七 / ADR-014 §五 明写
//    「`ConfigVersion` 不匹配**非致命**」⇒ 「进覆盖集」单独**不足以**守住 ordinal 重排。
//    分工:ConfigVersion 管「改动**可追溯**」,本门管「买断**静默错读**」——
//    把「麻黄汤被读成桂枝汤」从静默失败换成**明确的构建失败**。两者缺一不可。
//
// ⚠️ **两半,不合并**(2026-10-26):
//    · <see cref="ValidateTableOrdinals"/> 判「表**自身**是否自洽」(号/键各自唯一、号非负)。
//      **不需要基线** ⇒ 生产与夹具注入两条路径都必须跑到。
//    · <see cref="Validate"/> 判「表对基线是否 append-only」。需要基线文件。
//    拆开的理由:夹具注入路径(fixtures 表是**故意破碎**的合成表)天然对不上生产基线,
//    但它**仍然必须**过表自洽那一半。若把两半压死在一起,夹具路径只能整条跳过 ⇒ 那半的门
//    在测试覆盖上就是空的。
//
// ⚠️ **执法体形态**照本仓既有约定(见 ItemDbValidation 头注):纯函数 · 无 I/O ·
//    返回错误列表(空 = 通过)。读文件归调用方(baker),本类只吃已解析的数据 ——
//    这样它才能被测试直接喂合成数据做**证伪**(禁 I/O 的纯函数才可判伪)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.EditorTools.Gates
{
    /// <summary>ordinal 映射表自洽 + append-only 门的纯函数执行体(ADR-014 §五 承接条 ③)。
    /// <para><b>三条不变量</b>(任一违例 ⇒ 构建期硬失败):</para>
    /// <list type="number">
    /// <item><b>不改义</b> —— 基线里 <c>ordinal → 键</c> 的映射不得改。</item>
    /// <item><b>不改号</b> —— 既有键的 ordinal 不得变(即使全表重排「看起来更整齐」)。</item>
    /// <item><b>不复用</b> —— 已退役 / 已删除条目的号不得再分配给新条目(号是**永久占用**的)。</item>
    /// </list>
    /// <para>⇒ 合法变更<b>只有一种</b>:<b>追加新条目、取新号</b>。与 <c>docs/CLAUDE.md</c>
    /// 的「Never renumber existing IDs — only append new ones」是同一条纪律的可执行形态。</para>
    /// </summary>
    /// <example>
    /// <code>
    /// // ① 表自洽(两条路径都跑)
    /// errors.AddRange(OrdinalBaselineGate.ValidateTableOrdinals(currentEntries, "item_database"));
    /// // ② 对基线 append-only(有基线才跑)
    /// if (baseline != null)
    ///     errors.AddRange(OrdinalBaselineGate.Validate(baseline, currentEntries, "item_database"));
    /// </code>
    /// </example>
    public static class OrdinalBaselineGate
    {
        /// <summary>一条:<c>ordinal → 由 key_fields 拼出的键</c>。</summary>
        /// <remarks>用 <c>(int Ordinal, string Key)</c> 而不是泛型,是为了让本门住
        /// <c>Editor.Tools.Gates</c> 时不依赖任何领域类型 —— 键的比较一律走<b>序数字符串</b>。</remarks>
        public readonly struct Entry
        {
            /// <summary>号(≥ 0;负号 = 未赋值的默认值 ⇒ 本门拒)。</summary>
            public readonly int Ordinal;

            /// <summary>键的可比较形(由调用方按 <c>key_fields</c> 拼好;分隔符归调用方定但须稳定)。</summary>
            public readonly string Key;

            /// <summary>构造一条。</summary>
            /// <param name="ordinal">号。</param>
            /// <param name="key">键的可比较形。</param>
            public Entry(int ordinal, string key)
            {
                Ordinal = ordinal;
                Key = key;
            }

            /// <inheritdoc />
            public override string ToString() => $"{Ordinal}={Key}";
        }

        /// <summary>表**自身**自洽:号非负且唯一,键唯一。
        /// <para>不需要基线 ⇒ 生产路径与夹具注入路径都必须跑。</para>
        /// <para>⚠️ 为什么号唯一属于本门而非「唯一性门」:复合主键唯一(AC-21a-21)判的是
        /// <c>(base_id, processing_state)</c>;ordinal 是**另一个维度**的唯一性 ——
        /// 它一旦重复,载荷解码后**无法分辨是哪一件**,而复合主键唯一仍会通过。两道门各判一面。</para>
        /// </summary>
        /// <param name="current">当前表(按 ordinal 字段读出的 entries)。</param>
        /// <param name="registryId">注册表标识(仅用于错误报文,如 <c>item_database</c>)。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        public static List<string> ValidateTableOrdinals(IReadOnlyList<Entry> current, string registryId)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));

            var errors = new List<string>();
            string tag = $"[ordinal:{registryId}]";

            var byOrdinal = new Dictionary<int, string>();
            var byKey = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < current.Count; i++)
            {
                Entry e = current[i];
                if (string.IsNullOrEmpty(e.Key))
                {
                    errors.Add($"{tag} 第 {i} 条 ordinal {e.Ordinal} 的键为空 —— 键由 key_fields 拼出,空键无法比对");
                    continue;
                }

                if (e.Ordinal < 0)
                {
                    errors.Add($"{tag} 条目「{e.Key}」的 ordinal = {e.Ordinal} —— 须 ≥ 0" +
                               "(负号是 `ItemDef.Ordinal` 的未赋值默认值;出现在表里 = 绑定层漏读 ordinal 字段)");
                    continue; // 负号已拒,不再参与唯一性比对(否则一次写错刷两条)
                }

                if (byOrdinal.TryGetValue(e.Ordinal, out string dupKey))
                    errors.Add($"{tag} ordinal {e.Ordinal} 被两个键占用:「{dupKey}」与「{e.Key}」" +
                               "—— 号必须唯一(不唯一 ⇒ 载荷解码后无法分辨是哪一件)");
                else
                    byOrdinal[e.Ordinal] = e.Key;

                if (byKey.TryGetValue(e.Key, out int prevOrd))
                    errors.Add($"{tag} 键「{e.Key}」出现两次(ordinal {prevOrd} 与 {e.Ordinal})" +
                               "—— 一个键只能有一个号");
                else
                    byKey[e.Key] = e.Ordinal;
            }

            return errors;
        }

        /// <summary>基线与当前表的 append-only 比对(ADR-014 §五 承接条 ③)。
        /// <para>先跑 <see cref="ValidateTableOrdinals"/> 的那一半,再比对基线 —— 调用方无需自己拼两步。
        /// 表自洽不通过会让基线比对产生**噪声级联**(一个重号会伪装成两条「改义」),故先跑自洽一半、
        /// 有错即返回。</para>
        /// </summary>
        /// <param name="baseline">冻结基线(<c>_ordinal_baseline.json</c> 的 entries)。</param>
        /// <param name="current">当前数据表(按 ordinal 字段读出的 entries)。</param>
        /// <param name="registryId">注册表标识(仅用于错误报文)。</param>
        /// <returns>错误列表;空 = 通过。</returns>
        /// <example>
        /// 基线 <c>{0:a, 1:b}</c> + 当前 <c>{0:a, 1:b, 2:c}</c> ⇒ 空(合法追加)。<br/>
        /// 基线 <c>{0:a, 1:b}</c> + 当前 <c>{0:b, 1:a}</c> ⇒ 2 条错误(改号 + 改义)。<br/>
        /// 基线 <c>{0:a}</c> + 当前 <c>{1:a}</c> ⇒ 1 条错误(号变了)。<br/>
        /// 基线 <c>{0:a}</c> + 当前 <c>{0:b}</c> ⇒ 1 条错误(改义)。<br/>
        /// 基线 <c>{0:a}</c> + 当前 <c>{0:a, 1:b}</c> ⇒ 空(b 是新键取新号)。
        /// </example>
        public static List<string> Validate(
            IReadOnlyList<Entry> baseline, IReadOnlyList<Entry> current, string registryId)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (current == null) throw new ArgumentNullException(nameof(current));

            // ── ① 表自洽(有错就不再比对,防噪声级联)──
            List<string> errors = ValidateTableOrdinals(current, registryId);
            if (errors.Count > 0) return errors;

            string tag = $"[ordinal-baseline:{registryId}]";

            // ── ② 基线自身完整性 ──
            // (基线写坏了会**掩盖**真违例 —— 两条同号时后者覆盖前者,前者被静默丢失。
            //  故基线自身的完整性必须与比对一起守,不能假定「基线是可信的」。)
            var baseByOrdinal = new Dictionary<int, string>();
            var baseByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < baseline.Count; i++)
            {
                Entry b = baseline[i];
                if (b.Ordinal < 0)
                    errors.Add($"{tag} 基线条目「{b.Key}」的 ordinal = {b.Ordinal} —— 基线内号须 ≥ 0");
                else if (baseByOrdinal.TryGetValue(b.Ordinal, out string dupBase))
                    errors.Add($"{tag} 基线自身 ordinal {b.Ordinal} 重复:「{dupBase}」与「{b.Key}」" +
                               "—— 基线写坏会掩盖真违例(同号互相覆盖)");
                else
                    baseByOrdinal[b.Ordinal] = b.Key;

                if (baseByKey.TryGetValue(b.Key, out int prevBaseOrd))
                    errors.Add($"{tag} 基线自身键「{b.Key}」重复(ordinal {prevBaseOrd} 与 {b.Ordinal})" +
                               "—— 一个键只能占一个号");
                else
                    baseByKey[b.Key] = b.Ordinal;
            }
            if (errors.Count > 0) return errors;

            // ── ③ 不变量 ①②:基线每个 (ordinal, key) 在当前表里必须原样存在 ──
            var curByOrdinal = new Dictionary<int, string>(current.Count);
            var curByKey = new Dictionary<string, int>(current.Count, StringComparer.Ordinal);
            for (int i = 0; i < current.Count; i++)
            {
                curByOrdinal[current[i].Ordinal] = current[i].Key;
                curByKey[current[i].Key] = current[i].Ordinal;
            }

            for (int i = 0; i < baseline.Count; i++)
            {
                Entry b = baseline[i];

                if (!curByOrdinal.TryGetValue(b.Ordinal, out string curKey))
                {
                    if (curByKey.TryGetValue(b.Key, out int movedTo))
                        errors.Add($"{tag} 基线条目 {b.Ordinal}:{b.Key} —— 该键仍在表里但**号变了**" +
                                   $"(基线 {b.Ordinal} ⇒ 当前 {movedTo})。改号 ⇒ 旧存档按旧号解码到别的条目" +
                                   "(ADR-014 §五 承接条③)。合法变更只有「追加新条目取新号」;" +
                                   "退役该条目时保留其号占位,不得重排。");
                    else
                        errors.Add($"{tag} 基线条目 {b.Ordinal}:{b.Key} —— 该键在当前表里**消失**了。" +
                                   $"号 {b.Ordinal} 是**永久占用**的:即使条目退役也不得复用给新键" +
                                   "(ADR-014 §五 承接条③「永不重用、永不改义」)。" +
                                   "请恢复该条目,或在基线中保留其占位。");
                }
                else if (!string.Equals(curKey, b.Key, StringComparison.Ordinal))
                {
                    errors.Add($"{tag} 基线 ordinal {b.Ordinal} 指向「{b.Key}」,当前表同一号指向「{curKey}」" +
                               "—— **改义**(同一个号换了名字)。这正是本门要买断的静默错读:" +
                               "旧存档会把新键当成旧键解出来,且不报错。");
                }
            }

            // ── 不变量 ③(复用)的覆盖说明 —— 为何不再单列一段扫描 ──
            // 「已退役 / 已删除条目的号被新键顶替」在上面的循环里**已经**报出:
            // 基线号 b.Ordinal 存在、但当前表同一号是不同的键 ⇒ 命中「改义」分支。
            // 若该号在当前表里**根本不存在**,则走「消失」分支 —— 也已报错。
            // ⇒ 三条不变量在 (基线 × 当前) 的全集上已被二分覆盖,无需第三段。
            //
            // ⚠️ 刻意**不加**「新号必须 = 基线最大值 + 1 / 号码连续」判据:
            //    GDD 未要求连续,`disease_registry` 的 id 就是稀疏的。新号只要不落在
            //    基线已占用的号上即安全 —— 而「不落在已占用号上」= 表内号唯一(① 已守)。

            return errors;
        }
    }
}
