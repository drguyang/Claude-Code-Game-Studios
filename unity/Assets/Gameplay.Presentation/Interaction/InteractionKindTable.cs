// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— §数据契约(4-DC-1…6)/ 规则一(路由表)/
//     规则五(消歧)/ 规则八(移动压制)/ Tuning(R_INTERACT 区间)
//   ADR-014 §二/§五(两阶段烘焙:阶段 1 词法 · 阶段 2 per-schema 绑定 + 白名单/区间/闭集校验;
//     `interaction_kinds.json` → `*.cooked`,`ConfigVersion` = 内容哈希派生)
//   ADR-018 §六(无提示音铁律 —— 4 不产「可交互」播报音,与 AC-4-08 同源)
//
// ⚠️ 本文件 = `4-DC-1…6` 的**校验真身**(story 006 的落点)。
//   两条铁律:
//   ① **构建期硬失败**(`throw`),不是运行期告警 —— 违例进不了出货包(ADR-014 阶段 2 位点);
//   ② 每条校验有**独立判据**(可各注入一个违例夹具 ⇒ 各红),禁把六条折成一个「大 if」。
//
// ⚠️ 反空转(本仓头号失效模式):每条校验的判据须**触底到具体字段** ——
//   不得只断言「表存在」这类恒真形态(那是「断言了一个不存在的量」的同族)。
//   `DurationOwner` 字段即为二轮从「判据空转」中救回的实例(GDD §数据契约注)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Gameplay.Interaction;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 单个 kind 的数据契约行(`interaction_kinds.json` 的一行,CandidateState 的烘焙形态)。
    /// <para>字段与 GDD §数据契约表逐字对应;数值归数值轮,本类型只签**形状 + 校验**。</para>
    /// </summary>
    /// <remarks>
    /// ⚠️ <c>SuppressesMotor == false</c> 时 <see cref="DurationOwner"/> 允许 <see cref="DurationOwner.None"/>
    /// (4-DC-6 只在 <c>true</c> 时触发 —— GDD 明写「字段为 false 时本行不触发」)。
    /// </remarks>
    public readonly struct KindContractRow
    {
        /// <summary>种类(4-DC-2 闭集索引)。</summary>
        public readonly InteractableKind Kind;

        /// <summary>F-4.1 第二键;越小越优先(4-DC-3:10 项须**两两互异**)。</summary>
        public readonly int KindPriority;

        /// <summary><c>StableId</c> 取数口径(4-DC-4:须 ∈ 枚举;禁自由字符串)。</summary>
        public readonly StableIdSource StableIdSource;

        /// <summary>被路由的系统 id(4-DC-5:须 ∈ 已登记系统集)。</summary>
        public readonly int RoutesTo;

        /// <summary>本 kind 是否在边沿请求后抑制玩家水平位移 + Jump(规则八)。</summary>
        public readonly bool SuppressesMotor;

        /// <summary>
        /// 抑制时长的归属方(4-DC-6):<c>None</c> 或某**已登记系统 id**。
        /// <para>⚠️ story-006 评审 F-3 修复:此前为二值枚举 <c>{None=0, RoutedSystem=-1}</c>,
        /// 把「被路由系统的 id」**有损压缩**成一个哨兵 ⇒ 4-DC-6 的「必须已登记」半边**无从校验**
        /// (任何非 <c>None</c> 值都通过)。现改为可承载真实 id 的形状。</para>
        /// </summary>
        public readonly DurationOwnerKind DurationOwner;

        /// <summary>
        /// 时长归属方的系统 id —— 仅当 <see cref="DurationOwner"/> = <see cref="DurationOwnerKind.RoutedSystem"/> 时读。
        /// 4-DC-6 断言其 ∈ <see cref="RoutedSystems.Registered"/>(禁「引用却无登记」)。
        /// </summary>
        public readonly int DurationOwnerSystemId;

        /// <summary>
        /// 本 kind 的 4-DC-4 世界维度登记(<c>W/H/D</c>,格)。
        /// <para>⚠️ 仅 <see cref="StableIdSource.SlotLinearKey"/> 行**须**在场(线性键 <c>x + W·(y + H·z)</c>
        /// 不可比时全序第三键静默失效)。其余行填 <c>0</c> 即可(不读)。</para>
        /// </summary>
        public readonly int WorldW;

        /// <inheritdoc cref="WorldW"/>
        public readonly int WorldH;

        /// <inheritdoc cref="WorldW"/>
        public readonly int WorldD;

        /// <summary>意图上行口径(枚举;`HostOnlyIntent` / `None`)。</summary>
        public readonly IntentUplink IntentUplink;

        /// <summary>
        /// 完整构造(7 字段 + 4-DC-4 维度三元组)。
        /// </summary>
        public KindContractRow(
            InteractableKind kind, int kindPriority, StableIdSource stableIdSource,
            int routesTo, bool suppressesMotor,
            DurationOwnerKind durationOwner, int durationOwnerSystemId,
            int worldW, int worldH, int worldD, IntentUplink intentUplink)
        {
            Kind = kind;
            KindPriority = kindPriority;
            StableIdSource = stableIdSource;
            RoutesTo = routesTo;
            SuppressesMotor = suppressesMotor;
            DurationOwner = durationOwner;
            DurationOwnerSystemId = durationOwnerSystemId;
            WorldW = worldW;
            WorldH = worldH;
            WorldD = worldD;
            IntentUplink = intentUplink;
        }

        /// <summary>
        /// 便捷构造 —— 无 4-DC-4 维度登记的行(W/H/D 记 0)。
        /// </summary>
        public KindContractRow(
            InteractableKind kind, int kindPriority, StableIdSource stableIdSource,
            int routesTo, bool suppressesMotor,
            DurationOwnerKind durationOwner, int durationOwnerSystemId, IntentUplink intentUplink)
            : this(kind, kindPriority, stableIdSource, routesTo, suppressesMotor,
                   durationOwner, durationOwnerSystemId, 0, 0, 0, intentUplink)
        {
        }
    }

    /// <summary>
    /// 抑制时长归属方(4-DC-6)的**形态**。<see cref="None"/> = 无归属方
    /// (<c>SuppressesMotor=true</c> 时为违例);<see cref="RoutedSystem"/> = 归属某登记系统,
    /// 其 id 由 <see cref="KindContractRow.DurationOwnerSystemId"/> 承载并被 4-DC-6 校验。
    /// </summary>
    public enum DurationOwnerKind : int
    {
        /// <summary>无归属方(仅 <c>SuppressesMotor=false</c> 合法)。</summary>
        None = 0,

        /// <summary>归属某登记系统(如 17 采集自有计时)—— id 见 <see cref="KindContractRow.DurationOwnerSystemId"/>。</summary>
        RoutedSystem = 1,
    }

    /// <summary>意图上行口径(4-DC 字段;<c>OQ-4-10</c> 铁律:4 不自行选通道)。</summary>
    public enum IntentUplink : byte
    {
        /// <summary>不上行(纯本地表现 / 模态内动作)。</summary>
        None = 0,

        /// <summary>仅主机侧意图事件(判定输入类走可靠通道,承 ADR-001 §一之三)。</summary>
        HostOnlyIntent = 1,
    }

    /// <summary>
    /// 交互数据契约的**构建期校验器**(`4-DC-1…6`;ADR-014 阶段 2 位点)。
    /// <para>每条 DC 独立判据,违则 <c>throw</c>(构建期硬失败 —— 违例进不了出货包)。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>无状态</b>(AC-4-04 同族):纯静态方法,无字段,装载期跑一次。</para>
    /// <para><b>值归数值轮</b>:本类不签任何具体数值(`R_INTERACT` / `KindPriority` 取值),
    /// 只签「数值满足结构约束」。GDD Tuning 表的安全区间即本类判据。</para>
    /// </remarks>
    public static class InteractionKindTableValidator
    {
        /// <summary>
        /// 已登记的被路由系统集(4-DC-5 / 4-DC-6)—— **单一来源** <see cref="RoutedSystems.Registered"/>。
        /// <para>⚠️ story-006 评审 F-4/F-5 修复:此前本处与 <see cref="KindRouteTable"/> 各持一份
        /// 字面量拷贝,且本处 `private` ⇒ GDD 的「集合不得窄于规则一表」判据映射不到它。
        /// 现两处引用同一常量 ⇒ 「两机器一份数据」的漂移面消失。</para>
        /// </summary>
        private static readonly HashSet<int> RegisteredSystems = RoutedSystems.Registered;

        /// <summary>
        /// 跑全部六条校验(构建期一次调用)。任一违例即 `throw`。<b>先跑 4-DC-1</b>
        /// (半径是全局单值,违则后续 kind 校验无意义)。
        /// </summary>
        /// <param name="rows">烘焙表的 10 行。</param>
        /// <param name="rInteract">内联半径字段。</param>
        /// <param name="worldW">世界逻辑层宽(格)。</param>
        /// <param name="worldH">世界逻辑层高(格)。</param>
        /// <param name="worldD">世界逻辑层深(格)。</param>
        public static void Validate(
            IReadOnlyList<KindContractRow> rows, int rInteract, int worldW, int worldH, int worldD)
        {
            ValidateRadius(rInteract, worldW, worldH, worldD);   // 4-DC-1
            ValidateKindClosure(rows);                            // 4-DC-2
            ValidatePriorityTotalOrder(rows);                     // 4-DC-3
            ValidateStableIdSource(rows);                         // 4-DC-4
            ValidateRoutesTo(rows);                               // 4-DC-5
            ValidateDurationOwner(rows);                          // 4-DC-6
        }

        /// <summary>
        /// <b>4-DC-1</b> —— <c>1 ≤ r_interact ≤ min(W,H,D) − 1</c>。
        /// <para>下界违 = 恒不可交互;上界违 = 候选集 = 全世界、自报率上界失去意义。
        /// ⚠️ 上界是 GDD 三审补的(原只查下界 ⇒ 上界无门,<c>r_interact = 全图对角</c> 可静默通过)。</para>
        /// </summary>
        public static void ValidateRadius(int rInteract, int worldW, int worldH, int worldD)
        {
            int minDim = Math.Min(worldW, Math.Min(worldH, worldD));
            int upper = minDim - 1;

            if (rInteract < 1)
                throw new ArgumentOutOfRangeException(nameof(rInteract),
                    "4-DC-1 下界:r_interact ≥ 1(恒不可交互的半径 ⇒ 构建期即拒)");
            if (rInteract > upper)
                throw new ArgumentOutOfRangeException(nameof(rInteract),
                    $"4-DC-1 上界:r_interact({rInteract}) ≤ min(W,H,D)−1 = {upper}" +
                    "(超界 ⇒ 候选集 = 全世界、自报率上界失去意义)");
        }

        /// <summary>
        /// <b>4-DC-2</b> —— <c>kind</c> 闭集 == <see cref="InteractableKind"/> 全值,且枚举**无** <c>Player</c>。
        /// <para>① 缺项 = 该 kind 永不可选(静默失效);② 有 <c>Player</c> = <c>Kind(c) ≠ Player</c> 判据空转谓词复活。</para>
        /// </summary>
        public static void ValidateKindClosure(IReadOnlyList<KindContractRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var kindValues = (InteractableKind[])Enum.GetValues(typeof(InteractableKind));

            // ② 枚举侧:Player ∉ 枚举(禁空转谓词的入口)
            foreach (var k in kindValues)
                if (k.ToString() == "Player")
                    throw new ArgumentException(
                        "4-DC-2:InteractableKind 里不得有 Player(Kind(c) ≠ Player 空转谓词复活的入口)");

            // ① 行集 ⟷ 枚举集双向差集归零
            var missing = new List<string>();
            foreach (var k in kindValues)
            {
                bool found = false;
                for (int i = 0; i < rows.Count; i++) if (rows[i].Kind == k) { found = true; break; }
                if (!found) missing.Add(k.ToString());
            }
            var extra = new List<string>();
            foreach (var row in rows)
                if (Array.IndexOf(kindValues, row.Kind) < 0) extra.Add(row.Kind.ToString());

            if (missing.Count > 0 || extra.Count > 0)
                throw new ArgumentException(
                    $"4-DC-2:kind 闭集须 == InteractableKind 全值({kindValues.Length} 项)" +
                    (missing.Count > 0 ? ";枚举有而表缺:[" + string.Join(",", missing) + "]" : "") +
                    (extra.Count > 0 ? ";表有而枚举无:[" + string.Join(",", extra) + "]" : ""));
        }

        /// <summary>
        /// <b>4-DC-3</b> —— <c>KindPriority</c> **两两互异**(全序无平局),
        /// 且每行的 <c>KindPriority</c> 与<b>选择器实际读的表</b> <see cref="KindPriorityTable"/> 一致。
        /// <para>平局 ⇒ 靠 <c>StableId</c> 决胜(可玩但反直觉,优先级语义被架空)。</para>
        /// <para>⚠️ story-006 评审 F-2 修复:此前只查行内互异,而行的 <c>KindPriority</c> 与选择器的
        /// 第二键查表<b>无机械连接</b> ⇒ 校验在表上全绿而游戏没读它(「两机器」)。现加「行 == 表」对拍
        /// —— 校验的表与选择的表**是同一张**(<see cref="KindPriorityTable"/>)。</para>
        /// </summary>
        public static void ValidatePriorityTotalOrder(IReadOnlyList<KindContractRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var seen = new HashSet<int>();
            foreach (var row in rows)
                if (!seen.Add(row.KindPriority))
                    throw new ArgumentException(
                        $"4-DC-3:KindPriority 平局({row.KindPriority} 出现两次,kind={row.Kind})" +
                        " ⇒ 全序退化到 StableId 决胜(优先级语义被架空)");

            // 行 ⟷ 选择器表的机械对拍(表的唯一真源 = KindPriorityTable)
            foreach (var row in rows)
            {
                int tablePriority = KindPriorityTable.PriorityOf(row.Kind);
                if (row.KindPriority != tablePriority)
                    throw new ArgumentException(
                        $"4-DC-3:kind {row.Kind} 的 KindPriority={row.KindPriority} 与选择器表 " +
                        $"{tablePriority} 不一致 ⇒ 校验的表与选择的表是两张(两机器失效模式)");
            }
        }

        /// <summary>
        /// <b>4-DC-4</b> —— ① 每项 <c>StableIdSource</c> ∈ 枚举(禁自由字符串);
        /// ② <c>SlotLinearKey</c> 项须 <c>W/H/D</c> 在场(即世界维度 &gt; 0,否则线性键不可比)。
        /// </summary>
        /// <remarks>
        /// ⚠️ story-006 评审 F-2 修复:此前方法体**只有 ①**(枚举值域),doc-comment 却宣称 ② ——
        /// 宣称与实现对不上。⚠️ 更甚:① 在**类型面恒真**(<c>StableIdSource</c> 是强类型枚举,
        /// 任何编译器可接受的装载路径只能产生枚举内值),删掉 ① 全部夹具仍绿 ⇒ 判据空转。
        /// 现 ② 已实现,并由「<c>SlotLinearKey</c> 行缺 W/H/D」负夹具承重(该夹具删 ② 即红)。
        /// ① 保留为**编译期不可达路径的显式兜底**,不作承重判据。
        /// </remarks>
        public static void ValidateStableIdSource(IReadOnlyList<KindContractRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            // ① 枚举值域(类型面恒真的兜底 —— 见 remarks,不承重)
            var sourceValues = (StableIdSource[])Enum.GetValues(typeof(StableIdSource));
            foreach (var row in rows)
                if (Array.IndexOf(sourceValues, row.StableIdSource) < 0)
                    throw new ArgumentException(
                        $"4-DC-4:kind {row.Kind} 的 StableIdSource={row.StableIdSource} ∉ 枚举" +
                        "(自由字符串 ⇒ StableId 不可比 ⇒ F-4.1 第三键失去全序性,且**静默**)");

            // ② SlotLinearKey ⇒ W/H/D 在场(线性键 x + W·(y + H·z) 的必要条件)
            //    ⚠️ 本半边是 4-DC-4 的**承重判据**(story-006 评审 F-2:此前未实现,零夹具)。
            foreach (var row in rows)
            {
                if (row.StableIdSource != StableIdSource.SlotLinearKey) continue;
                if (row.WorldW <= 0 || row.WorldH <= 0 || row.WorldD <= 0)
                    throw new ArgumentException(
                        $"4-DC-4:kind {row.Kind} 用 SlotLinearKey 但 W/H/D 未在场" +
                        $"({row.WorldW}/{row.WorldH}/{row.WorldD})" +
                        " ⇒ 线性键 x + W·(y + H·z) 不可比 ⇒ F-4.1 第三键失去全序性(静默)");
            }
        }

        /// <summary>
        /// <b>4-DC-5</b> —— <c>RoutesTo</c> 每一项 ∈ 已登记系统集。
        /// <para>违 = 引用却无登记(仓库头号失效模式):路由到一个没有 GDD 的系统。</para>
        /// </summary>
        public static void ValidateRoutesTo(IReadOnlyList<KindContractRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows)
                if (!RegisteredSystems.Contains(row.RoutesTo))
                    throw new ArgumentException(
                        $"4-DC-5:kind {row.Kind} 的 RoutesTo={row.RoutesTo} 不在已登记系统集内" +
                        "(引用却无登记的失效模式)");
        }

        /// <summary>
        /// <b>4-DC-6</b> —— <c>SuppressesMotor == true</c> 的行,其 <c>DurationOwner</c> 必须已登记且 **≠ <c>None</c>**。
        /// <para>违 = 边沿请求发出后**无人 Release** ⇒ 玩家永久定身(丢失更新的镜像)。</para>
        /// <para>⚠️ 判据形式 = 「<b>字段非空</b>」**+「归属方 ∈ 已登记系统集」** —— 后者是 story-006
        /// 评审 F-3 修复补上的一半(GDD 的失效模式是**归属方错了**,不是「没人」)。</para>
        /// <para>⚠️ <c>SuppressesMotor == false</c> 的行**本检查不触发**(GDD 明写,如 <c>PoiCell</c>)。</para>
        /// <para>⚠️ 前置未到的行(17 的 <c>ForageSpot</c> / 20 的 <c>Container</c>)—— 其红表示
        /// <b>契约已写、判据待前置</b>(<c>OQ-17-3</c> 未裁),见 story-006 登记;
        /// 本校验器**不**为它们留白(那样会让真违例静默通过)。</para>
        /// </summary>
        public static void ValidateDurationOwner(IReadOnlyList<KindContractRow> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows)
            {
                if (!row.SuppressesMotor) continue;   // GDD:false 时本行不触发
                if (row.DurationOwner == DurationOwnerKind.None)
                    throw new ArgumentException(
                        $"4-DC-6:kind {row.Kind} SuppressesMotor=true 但 DurationOwner=None" +
                        " ⇒ 边沿请求后无人 Release ⇒ 玩家永久定身(丢失更新的镜像)");

                // ⚠️ story-006 评审 F-3 修复:此前归属方 id 被有损压成哨兵 -1 ⇒ **只查非空**,
                //    不查「已登记」。GDD 的失效模式恰在**归属方错了**(如 Patient 行写别的系统号)
                //    ⇒ 现补「归属方 ∈ 已登记系统集」半边,让错误的归属方可被构建期拒。
                if (!RegisteredSystems.Contains(row.DurationOwnerSystemId))
                    throw new ArgumentException(
                        $"4-DC-6:kind {row.Kind} 的时长归属方 system id={row.DurationOwnerSystemId} " +
                        "不在已登记系统集内 ⇒ 边沿请求后**归属方无人 Release**" +
                        "(引用却无登记 —— 玩家永久定身的真因:不是没人,是**错人**)");
            }
        }
    }

    /// <summary>
    /// F-4.1 第二键 <c>KindPriority</c> 表的**唯一权威来源**(story-006 评审 F-2 修复)。
    /// <para>⚠️ <b>为什么要这个类型</b>:此前 <c>KindPriority</c> 只在 `InteractionSelector` 的
    /// <c>KindPriorityOf(k) =&gt; (int)k</c>(枚举序占位)里,而 4-DC-3 校验的行数组<b>与该占位无任何机械连接</b>
    /// ⇒ 校验器可以在表上全绿而游戏里没有一面读它(「两机器」失效模式)。</para>
    /// <para>现收敛:本类型是<b>唯一</b>的表真源,<see cref="InteractionSelector"/> 与
    /// <see cref="InteractionKindTableValidator"/> 皆引用它 ⇒ 「校验的表」与「选择器读的表」
    /// 物理上不可能是两张。</para>
    /// <para>⚠️ <b>数值归数值轮</b> —— 下列取值是 <b>GDD §F-4.1b 演示序</b>(Patient:0…Drop:9,
    /// 逐字照录),仅用来让「校验的表 = 选择的表」这条接线可跑可测;<b>真表待数值轮落
    /// <c>assets/data/interaction_kinds.json</c></b>(ADR-014 烘焙,装载本体归 story 007)。
    /// 换真表时只改本类型一处,选择器与校验器自动同源。</para>
    /// <para>⚠️ 破 `Gameplay.Presentation` 的程序集纯净性<b>不适用</b>:本类型只依赖
    /// <see cref="InteractableKind"/>(同命名空间)· <c>System</c> · <c>System.Collections.Generic</c>
    /// —— 零 9 / 11 / 8 的 sim 侧类型,AC-4-05 反射闭包不受影响。</para>
    /// </summary>
    public static class KindPriorityTable
    {
        /// <summary>
        /// kind → 优先级(越小越优先)。10 项**两两互异**(4-DC-3 的判据面)。
        /// 取值 = GDD §F-4.1b 演示序(逐字照录;真表待数值轮)。
        /// </summary>
        private static readonly Dictionary<InteractableKind, int> Priorities =
            new Dictionary<InteractableKind, int>
            {
                // source: GDD §F-4.1b 演示序(⚠️ 值归数值轮 —— 此处仅为接线可测)
                { InteractableKind.Patient,     0 },
                { InteractableKind.PoiCell,     1 },
                { InteractableKind.Utensil,     2 },
                { InteractableKind.Container,   3 },
                { InteractableKind.Door,        4 },
                { InteractableKind.Switch,      5 },
                { InteractableKind.ForageSpot,  6 },
                { InteractableKind.BuildSlot,   7 },
                { InteractableKind.ClinicPanel, 8 },
                { InteractableKind.Drop,        9 },
            };

        /// <summary>
        /// 查表(F-4.1 第二键)。<b>缺项即抛</b> —— 缺项 = 该 kind 永不可选(静默失效),
        /// 与 4-DC-2 的失效模式同源;不静默退化为枚举序。
        /// </summary>
        public static int PriorityOf(InteractableKind kind)
        {
            if (!Priorities.TryGetValue(kind, out int p))
                throw new ArgumentOutOfRangeException(nameof(kind),
                    $"KindPriority 表缺 kind {kind} ⇒ 该 kind 永不可选(静默失效;4-DC-2/4-DC-3 失效模式)");
            return p;
        }

        /// <summary>
        /// 表本体(只读视图)—— 供 4-DC-3 校验器逐行对拍。
        /// </summary>
        public static IReadOnlyDictionary<InteractableKind, int> Rows => Priorities;
    }
}
