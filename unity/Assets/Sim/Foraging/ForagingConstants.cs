// S20-3 · story-001 —— 17 采集调参表(assets/data/foraging_constants.json)的 **Sim 侧整数域视图**。
//
// 权威来源:
//   GDD design/gdd/foraging.md
//     · F-17-1 `:265-310` —— `raw_quality = CDFWalk(quality_distribution, U)`;
//       `QualityCap = CapTable[QueryLevel(采集)]`;**装载期校验三条**(`:305-311`)
//     · F-17-2 `:318-336` —— `qty = Mul(qty_per_node, GatherMul(L))`;`GatherMul ∈ [1, GATHER_MUL_CAP]`
//     · F-17-3 `:338-360` —— `EffCapacity = BaseCapacity × SEASON_MULT[season_index] / MUL_ONE × YieldDecay(Σ_all)`;
//       `yieldDecay(n) = max(DECAY_FLOOR, MUL_ONE − Mul(n, DECAY_RATE))`
//     · §Tuning Knobs `:541-548` —— 全部数值 2026-10-06 已裁(OQ-17-1 / OQ-17-2 结案)
//   story production/epics/foraging/story-001-variety-closure-and-data-load-validation.md
//     AC-17-08b(非法值硬失败)· AC-17-14(CapTable 四性质)· AC-17-16(SEASON_MULT 表长)
//   ADR-014 §二/§三(作者态 JSON → 构建期烘焙 → 运行期只读产物)
//   ADR-005 / ADR-006(整数定点域;`SEASON_MULT` / `DECAY_*` / `GATHER_MUL_CAP` = `Fix`,计数 = int)
//
// ⚠️ 住 Sim 程序集 `DaYiJingCheng.Sim.Foraging`(门 A / b2:引用集 = BCL + Sim.Contracts,零引擎)。
//
// ⚠️ **构造器拒收 = Sim 侧纵深防御**(与 `DelayTable53.Table` / `ClinicContextTable` 同口径):
//   构建期主校验点在 Binder(`ForagingConstantsBinder`,Editor.Tools.Bake);此处防
//   「绕过 Bake 直构表」与「产物被改」。⇒ 本文件只做**可判定的结构性拒收**,不重做 Schema 的
//   键名白名单(那是作者态的事,运行期再做一遍只会造成两份规则)。
//
// ⚠️ **四条拒收一一对应 AC-17-08b / AC-17-14 / AC-17-16**,且**一律显式 throw**
//   (非 `Debug.Assert` —— GDD `:397-400` 边界探针表与 story AC-17-08b 都要求「硬失败」;
//   `Debug.Assert` 在玩家构建被剥掉,等于把硬失败降级成静默)。
//
// ⚠️ **本表不含 `quality_distribution`** —— 它住 21a 的 `gather_profile`(GDD 六处一致:
//   `:45` / `:217` / `:297` / `:454` / `:542` / `:711`)。2026-10-26 用户裁定「按 GDD:住 21a」;
//   story-001 卡面 Note 1 把它列入「17 自家调参表」是**卡面笔误**(已在卡面就地加注)。
//   ⇒ 17 侧装载 `quality_distribution` 的路径 = 读 21a 的 `GatherProfile`(该块本身不持有该类型,
//     见 `Sim.Contracts/ItemDatabase/GatherProfile.cs` 注释「形状由 17 定」)——
//     形状(长度 = MAX_QUALITY、`Σw ≥ 1`)的**校验落点**归本文件暴露的静态校验器,
//     数据**载体**归 21a。两者不重叠。
//
// ⚠️ **`SEASONS_PER_YEAR` 归 5 不归 17**(`OQ-5-1` 2026-09-18 已裁):17 只**消费**它来断
//   `SEASON_MULT` 表长。本文件收它作构造参数(而非硬编码 5),使「5 改季节数 ⇒ 17 装载即失败」
//   成为可观测行为,而不是两处各写一个 5。

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Sim.Foraging
{
    /// <summary>
    /// F-17-1 / F-17-2 / F-17-3 的**调参表运行期视图**(`foraging_constants.json` 的烘焙产物形状)。
    /// <para>纯数据 + 构造期校验;不含任何求值逻辑(求值归 story-002 / story-004)。</para>
    /// </summary>
    public sealed class ForagingConstants
    {
        /// <summary>`CapTable` 的规定长度 = `SKILL_CAP + 1`(GDD `:300`;`SKILL_CAP = 60` 由 30 拥有)。</summary>
        public const int CapTableLength = 61;

        /// <summary>`CapTable` 的下标上界(`= CapTableLength − 1`),供调用方做索引前守卫。</summary>
        public const int MaxSkillLevel = CapTableLength - 1;

        private readonly int[] _capTable;
        private readonly Fix[] _seasonMult;
        private readonly IReadOnlyList<int> _capTableView;
        private readonly IReadOnlyList<Fix> _seasonMultView;

        /// <summary>构造并做四条硬拒收(见文件头)。</summary>
        /// <param name="capTable">`CapTable[]`(长度须 = 61;构造期拷贝)。</param>
        /// <param name="seasonMult">`SEASON_MULT[]`(长度须 = <paramref name="seasonsPerYear"/>;构造期拷贝)。</param>
        /// <param name="seasonsPerYear">5 的 `SEASONS_PER_YEAR`(**由调用方传入,不硬编码** ——
        /// 见文件头「归 5 不归 17」)。</param>
        /// <param name="regrowWindowTicks">`RegrowWindow`(tick 计数,须 &gt; 0)。</param>
        /// <param name="baseCapacity">`BaseCapacity`(每节点容量基数,须 &gt; 0)。</param>
        /// <param name="decayRate">`DECAY_RATE`(须 &gt; 0 —— 0 会让 `yieldDecay` 恒为 1,深水线消失)。</param>
        /// <param name="decayFloor">`DECAY_FLOOR`(须 ∈ (0, 1] —— 触底值;≤0 会让节点归零)。</param>
        /// <param name="gatherMulCap">`GATHER_MUL_CAP`(须 ≥ 1 —— &lt;1 会让高技能反而少采)。</param>
        /// <param name="maxQuality">21a 的 `MAX_QUALITY`(**由调用方传入**;`CapTable` 各值须 ≤ 它 ——
        /// D-21-7 双向耦合,禁在本文件写死 5)。</param>
        /// <exception cref="ArgumentException">任一条形状 / 值域判据不满足(AC-17-08b / 14 / 16)。</exception>
        public ForagingConstants(
            IReadOnlyList<int> capTable,
            IReadOnlyList<Fix> seasonMult,
            int seasonsPerYear,
            long regrowWindowTicks,
            int baseCapacity,
            Fix decayRate,
            Fix decayFloor,
            Fix gatherMulCap,
            int maxQuality)
        {
            // ── 拒收 ①:CapTable 四性质(AC-17-14)—— 长度 / 单调不减 / [0] ≥ 1 / 各值 ≤ MAX_QUALITY ──
            if (capTable == null)
                throw new ArgumentException("CapTable 为 null —— 缺表 = 品级钳制无上界(AC-17-14)");
            if (capTable.Count != CapTableLength)
                throw new ArgumentException(
                    $"CapTable 长度 {capTable.Count} ≠ {CapTableLength}(= SKILL_CAP+1)—— " +
                    "AC-17-14 / GDD `:300`;长度不符会让等级 60 越界读或低等级无档");
            if (maxQuality < 1)
                throw new ArgumentException(
                    $"MAX_QUALITY = {maxQuality} < 1 —— 21a 的品级上界非法(CapTable 各值须 ≤ 它)");

            _capTable = new int[CapTableLength];
            for (int level = 0; level < CapTableLength; level++)
            {
                int value = capTable[level];
                if (value < 1)
                    throw new ArgumentException(
                        $"CapTable[{level}] = {value} < 1 —— AC-17-14;" +
                        "`CapTable[0] = 0` 会让 clamp(x, 1, 0) 抛异常(min > max)");
                if (value > maxQuality)
                    throw new ArgumentException(
                        $"CapTable[{level}] = {value} > MAX_QUALITY = {maxQuality} —— AC-17-14;" +
                        "越界读 out_quality(D-21-7 双向耦合:本表上界 = 21a 的 MAX_QUALITY)");
                if (level > 0 && value < _capTable[level - 1])
                    throw new ArgumentException(
                        $"CapTable 非单调不减:CapTable[{level}] = {value} < CapTable[{level - 1}] = " +
                        $"{_capTable[level - 1]} —— AC-17-14;非单调 = 升级反而采到更差(直接出戏)");
                _capTable[level] = value;
            }
            _capTableView = Array.AsReadOnly(_capTable);

            // ── 拒收 ②:SEASON_MULT 表长(AC-17-16)—— 不等即硬失败,不是越界读 ──
            if (seasonsPerYear < 1)
                throw new ArgumentException(
                    $"seasons_per_year = {seasonsPerYear} < 1 —— 5 的 SEASONS_PER_YEAR 非法");
            if (seasonMult == null)
                throw new ArgumentException("SEASON_MULT 为 null —— 缺表 = 季节调制无系数(AC-17-16)");
            if (seasonMult.Count != seasonsPerYear)
                throw new ArgumentException(
                    $"SEASON_MULT 长度 {seasonMult.Count} ≠ SEASONS_PER_YEAR = {seasonsPerYear} —— " +
                    "AC-17-16 / GDD `:355`;不等会让 season_index 越界读(须索引前查表长)");

            _seasonMult = new Fix[seasonMult.Count];
            for (int i = 0; i < seasonMult.Count; i++)
            {
                if (seasonMult[i].Raw <= 0)
                    throw new ArgumentException(
                        $"SEASON_MULT[{i}] raw = {seasonMult[i].Raw} ≤ 0 —— " +
                        "季节系数非正会让该季节点容量归零或为负(F-17-3 量纲齐一)");
                _seasonMult[i] = seasonMult[i];
            }
            _seasonMultView = Array.AsReadOnly(_seasonMult);

            // ── 拒收 ③:RegrowWindow > 0(AC-17-08b / GDD `:400`)──
            if (regrowWindowTicks <= 0)
                throw new ArgumentException(
                    $"RegrowWindow = {regrowWindowTicks} ≤ 0 —— AC-17-08b / GDD `:400`;" +
                    "≤ 0 会让「无再生」退化为「无限采集」(静默)");

            // ── 拒收 ④:其余深水线系数值域(AC-17-08b)──
            if (baseCapacity <= 0)
                throw new ArgumentException(
                    $"BaseCapacity = {baseCapacity} ≤ 0 —— F-17-3 的容量基数须 > 0");
            if (decayRate.Raw <= 0)
                throw new ArgumentException(
                    $"DECAY_RATE raw = {decayRate.Raw} ≤ 0 —— F-17-3;" +
                    "0 会让 yieldDecay 恒为 1(深水线消失,退化成浅水线)");
            if (decayFloor.Raw <= 0 || decayFloor.Raw > Fix.OneRaw)
                throw new ArgumentException(
                    $"DECAY_FLOOR raw = {decayFloor.Raw} ∉ (0, 1.0] —— F-17-3;" +
                    "≤ 0 会让节点触底后归零(采枯竭);> 1 会让衰减项反而放大");
            if (gatherMulCap.Raw < Fix.OneRaw)
                throw new ArgumentException(
                    $"GATHER_MUL_CAP raw = {gatherMulCap.Raw} < 1.0 —— F-17-2;" +
                    "上界 < 1 会让高技能反而采得更少(双重惩罚)");

            RegrowWindowTicks = regrowWindowTicks;
            BaseCapacity = baseCapacity;
            DecayRate = decayRate;
            DecayFloor = decayFloor;
            GatherMulCap = gatherMulCap;
            SeasonsPerYear = seasonsPerYear;
            MaxQuality = maxQuality;
        }

        /// <summary>`CapTable[]` 只读视图(长度恒 61;`CapTable[L]` = 等级 L 的可识别品级上限)。
        /// <para>⚠️ 经 <see cref="ReadOnlyCollection{T}"/> 语义暴露(裸数组被 cast 回可改写 —— W-4 先例)。</para></summary>
        public IReadOnlyList<int> CapTable => _capTableView;

        /// <summary>`SEASON_MULT[]` 只读视图(长度恒 = <see cref="SeasonsPerYear"/>)。
        /// <para>⚠️ `Fix` 表;**索引用 5 的 `season_index`**,本类不重定义季节口径(F-5.2)。</para></summary>
        public IReadOnlyList<Fix> SeasonMult => _seasonMultView;

        /// <summary>`SEASONS_PER_YEAR`(5 的交付物;本类只消费)。</summary>
        public int SeasonsPerYear { get; }

        /// <summary>`RegrowWindow`(tick;构造期已验 &gt; 0)。</summary>
        public long RegrowWindowTicks { get; }

        /// <summary>`BaseCapacity`(每节点容量基数;构造期已验 &gt; 0)。</summary>
        public int BaseCapacity { get; }

        /// <summary>`DECAY_RATE`(F-17-3 衰减速率;构造期已验 &gt; 0)。</summary>
        public Fix DecayRate { get; }

        /// <summary>`DECAY_FLOOR`(F-17-3 触底值;构造期已验 ∈ (0, 1])。</summary>
        public Fix DecayFloor { get; }

        /// <summary>`GATHER_MUL_CAP`(F-17-2 乘子上界;构造期已验 ≥ 1)。</summary>
        public Fix GatherMulCap { get; }

        /// <summary>21a 的 `MAX_QUALITY`(构造期用于 CapTable 上界校验;也供调用方 clamp)。</summary>
        public int MaxQuality { get; }

        /// <summary>
        /// 查等级 L 的可识别品级上限(F-17-1 的 `QualityCap`)。
        /// </summary>
        /// <param name="level">采集技能等级 ∈ [0, <see cref="MaxSkillLevel"/>]。</param>
        /// <returns>`CapTable[level]`。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="level"/> 越出
        /// [0, <see cref="MaxSkillLevel"/>] —— **索引前守卫**,不留越界读路径(AC-17-16 同款纪律)。</exception>
        public int QualityCapAt(int level)
        {
            if (level < 0 || level > MaxSkillLevel)
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    $"技能等级越界:须 ∈ [0, {MaxSkillLevel}](= SKILL_CAP);" +
                    "本方法在索引前守卫,不留越界读");
            return _capTable[level];
        }

        /// <summary>
        /// 查季节的容量乘子(F-17-3 的 `SEASON_MULT[season_index]`)。
        /// </summary>
        /// <param name="seasonIndex">5 的 F-5.2 给出的季节下标 ∈ [0, <see cref="SeasonsPerYear"/>)。</param>
        /// <returns>该季的 Q16.16 乘子。</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="seasonIndex"/> 越界 ——
        /// **索引前守卫**(AC-17-16 的「非越界读」判据)。</exception>
        public Fix SeasonMultiplierAt(int seasonIndex)
        {
            if (seasonIndex < 0 || seasonIndex >= _seasonMult.Length)
                throw new ArgumentOutOfRangeException(nameof(seasonIndex), seasonIndex,
                    $"季节下标越界:须 ∈ [0, {_seasonMult.Length})(= SEASONS_PER_YEAR);" +
                    "本方法在索引前守卫,不留越界读");
            return _seasonMult[seasonIndex];
        }
    }
}
