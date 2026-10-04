// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则一(路由表)/ 规则五(消歧)/ 4-DC-2(十项闭集)
//     / 4-DC-5(RoutesTo ∈ 已登记系统集)/ AC-4-18(枚举 ⟷ 路由表双向对拍)
//   ADR-024 §Decision(新 Kind 先建 registry 条目再引用 —— 4 侧本故事**零新 Kind**)
//   ADR-021(PoiCell → 6 唯一写者)
//
// ⚠️ AC-4-18 的落点:**Kind 枚举 ⟷ 路由表行集双向对拍** ——
//   枚举成员集 == 行集;恰 **10 项**;两方向差集 == ∅;**`Player` ∉ 枚举**。
//   ⚠️ 原稿的 `Kind(c) ≠ Player` 运行期谓词是**空转**(判在不可能出现 Player 的位置),
//     已由 GDD 二轮删除;其意图由本表的闭集断言承担 —— **禁**在本文件复活旧谓词。
//
// ⚠️ **10 项**是第一列(规则一表)的**唯一权威枚举**;`Patient` 一项在**路由面**是
//   **单义**(规则五 S-8.4 路线甲:世界空间裸 Interact = 就诊 → 37);查体 / 施治走模态内动作,
//   **不经 4 的世界路由** ⇒ 本表 `Patient → 37`(四义岔口已取消,输出形状逐位不变)。

using System;
using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 目标种类的路由表(规则一 / 4-DC-2 / AC-4-18)。
    /// <para>把 <see cref="InteractableKind"/> 映射到**被路由系统的整数 id**(`RoutesTo`)。</para>
    /// <para>⚠️ <b>双向闭合</b>(AC-4-18):枚举成员集 == 本表行集,恰 10 项,差值 == ∅。
    /// 缺项 = 该 kind 永不可路由(静默失效);多项 = 引用了枚举外的值。</para>
    /// </summary>
    /// <remarks>
    /// <para><b>无状态</b>:装载后表内容不变(AC-4-04 同族);无 static 可变状态。</para>
    /// <para><b>值归数值轮 / 烘焙</b>:真表落 <c>interaction_kinds.json</c> 的 <c>RoutesTo</c>
    /// 字段(ADR-014);本故事以注入值驱动,真装载 + <c>4-DC-5</c> 登记校验归 story 006。</para>
    /// </remarks>
    public sealed class KindRouteTable
    {
        /// <summary>
        /// 已登记的被路由系统集(GDD 规则一表的**第二列 distinct 值** + <c>4-DC-5</c>)。
        /// <para>⚠️ <c>4-DC-5</c> 明写「见 <c>4-DC-2</c> 的 10 项表」—— 即规则一表第二列的**全部去重值**,
        /// 含 <c>Patient</c> 行的四义 <c>37 / 8 / 10 / 11</c>(规则五 S-8.4 路线甲:模态内动作仍路由到 8 / 10 / 11)。
        /// <b>不得只取 LegalTable 实际用到的值</b> —— 那会把判据收紧到 <c>4-DC-5</c> 之外,
        /// 使一张**合法**表(如 <c>Patient → 8</c>)被误拒。</para>
        /// <para>⚠️ 逐项来自登记系统(20 / 17 / 37 / 8 / 10 / 11 / 6 / 23 / 18 / 24)——
        /// 引用却无登记 = 仓库头号失效模式。</para>
        /// <para>⚠️ 本集是**装载替身** —— 真值随 <c>interaction_kinds.json</c> 烘焙(ADR-014),
        /// 装载本体归 story 006;本故事只在 4 侧消费点的**装载失败面**验闭合。</para>
        /// </summary>
        private static readonly HashSet<int> RegisteredSystems = new HashSet<int>
        {
            20,  // 掉落物 / 容器 —— 拾取 / 开箱裁决
            17,  // 采集点 —— 采集裁决(散布 / 再生长)
            37,  // 病人 —— 就诊立案(S-8.4 路线甲:世界空间裸 Interact 单义)
            8,   // 病人 —— 查体(S-8.4 模态内动作行;4 的**世界路由**不经此,但 4-DC-5 集须含)
            10,  // 病人 —— 急救(ADR-011 直读通道;同上,集须含)
            11,  // 病人 —— 施治(方笺落笔;同上,集须含)
            6,   // POI 格 / 门 / 开关 —— 校验 + 判距复验 + Append(唯一写者)
            23,  // 建造槽位 / 门 / 开关 —— 放置 / 拆除(世界几何)
            18,  // 灶台 / 器具 —— 进入炮制(准入门 + 时序)
            24,  // 医馆面板 —— 面板(评分 / 布局)
        };

        private readonly Dictionary<InteractableKind, int> _routes;

        /// <summary>
        /// 构造路由表(装载期**双向对拍**,AC-4-18)。
        /// </summary>
        /// <param name="routes">kind → 被路由系统 id 的映射(来自烘焙表;10 项)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="routes"/> 为 null。</exception>
        /// <exception cref="ArgumentException">双向闭合违例(缺项 / 多项 / 枚举外值 / 未登记系统号)。</exception>
        public KindRouteTable(IReadOnlyDictionary<InteractableKind, int> routes)
        {
            if (routes == null) throw new ArgumentNullException(nameof(routes));

            // ── AC-4-18:双向对拍(装载期硬失败,非运行期告警)──────────────
            var kindValues = (InteractableKind[])Enum.GetValues(typeof(InteractableKind));

            // ① 编译期类型断言:`Player ∉ 枚举`(值域断言在 ② 兜底)
            //    ⚠️ 这里**不写** `Kind(c) != Player` 式运行期谓词 —— 那是 GDD 二轮删除的空转形态。
            foreach (var k in kindValues)
                if (k.ToString() == "Player")
                    throw new ArgumentException("4-DC-2:InteractableKind 里不得有 Player(判据空转谓词复活的入口)");

            // ② 行集 ⟷ 枚举集,双向差集归零
            var missing = new List<string>();   // 枚举有、表无 ⇒ 该 kind 永不可路由(静默失效)
            foreach (var k in kindValues)
                if (!routes.ContainsKey(k)) missing.Add(k.ToString());

            var extra = new List<string>();     // 表有、枚举无 ⇒ 引用了不存在的 kind
            foreach (var k in routes.Keys)
                if (Array.IndexOf(kindValues, k) < 0) extra.Add(k.ToString());

            if (missing.Count > 0 || extra.Count > 0)
                throw new ArgumentException(
                    "AC-4-18:Kind 枚举 ⟷ 路由表须双向闭合(恰 " + kindValues.Length + " 项)" +
                    (missing.Count > 0 ? ";枚举有而表缺:[" + string.Join(",", missing) + "]" : "") +
                    (extra.Count > 0 ? ";表有而枚举无:[" + string.Join(",", extra) + "]" : ""));

            // ③ RoutesTo ∈ 已登记系统集(4-DC-5)
            foreach (var kv in routes)
                if (!RegisteredSystems.Contains(kv.Value))
                    throw new ArgumentException(
                        $"4-DC-5:kind {kv.Key} 的 RoutesTo={kv.Value} 不在已登记系统集内" +
                        "(引用却无登记的失效模式)");

            _routes = new Dictionary<InteractableKind, int>(routes);
        }

        /// <summary>路由一项到其拥有方系统 id(`RoutesTo`)。表已双向闭合 ⇒ 恒有落点。</summary>
        /// <param name="kind">目标种类。</param>
        /// <returns>被路由系统的整数 id。</returns>
        public int RoutesTo(InteractableKind kind)
        {
            if (!_routes.TryGetValue(kind, out int target))
                throw new KeyNotFoundException($"AC-4-18:kind {kind} 无路由行(装载期对拍本应拦住)");
            return target;
        }

        /// <summary>表行数(AC-4-18:须 == 枚举项数 == 10)。</summary>
        public int Count => _routes.Count;
    }
}
