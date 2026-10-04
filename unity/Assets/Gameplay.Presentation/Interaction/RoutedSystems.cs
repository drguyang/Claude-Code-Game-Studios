// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则一(路由表第二列 distinct 值)/ 4-DC-5 / 4-DC-6
//   ADR-014 §二/§五(两阶段烘焙:阶段 2 白名单/区间/闭集校验)
//
// ⚠️ 本类型的由来(story-006 单轮评审 F-4/F-5 修复轮):
//   此前 `RegisteredSystems`(10 个被路由系统 id)在 `KindRouteTable` 与
//   `InteractionKindTableValidator` **各持一份字面量拷贝** —— 两个机器一份数据 ⇒ 静默漂移;
//   且校验器那一份是 `private` ⇒ GDD 的「集合不得窄于规则一表」判据**映射不到**它。
//   ⇒ 收敛为单一来源:两份引用同一常量,任一处增删都在两处同时可见。
//
// ⚠️ 反空转:本集只签「被路由系统 id 的登记面」。装载真值随 `interaction_kinds.json` 烘焙
//   (ADR-014),装载本体归 story 007;本集是**装载替身**(与 `KindRouteTable` 同源)。

using System.Collections.Generic;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 已登记的被路由系统 id 集(4-DC-5 / 4-DC-6 的登记面;规则一表第二列 distinct 值)。
    /// <para>单一来源 —— `KindRouteTable`(路由闭合)与
    /// <see cref="InteractionKindTableValidator"/>(契约校验)皆引用本集。</para>
    /// </summary>
    public static class RoutedSystems
    {
        /// <summary>
        /// 逐项来自登记系统(20 / 17 / 37 / 8 / 10 / 11 / 6 / 23 / 18 / 24)——
        /// 引用却无登记 = 仓库头号失效模式。
        /// <para>⚠️ 本集是**装载替身** —— 真值随 <c>interaction_kinds.json</c> 烘焙(ADR-014)。</para>
        /// </summary>
        public static readonly HashSet<int> Registered = new HashSet<int>
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
    }
}
