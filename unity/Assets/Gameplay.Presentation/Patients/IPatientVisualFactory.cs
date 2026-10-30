// 权威来源:
//   ADR-013 §9 C3(呈现层只渲染,永不持有游戏状态)
//   ADR-023 ②(`World.unity` 零 gameplay GameObject —— 预摆 = 第二真源)
//   ADR-028 ②(零场景预摆的同一纪律:`AudioSource` 作非 Boot 场景内容 = 构建失败)
//   `production/milestones/p0-sweep-2026-10-29.md:308`(病人灰盒 proxy 的裁定)
//
// 🔁 **2026-10-30 新建(`TD-prescription-06` 的「灰盒做好,等真实资产兑换」轮)**:
//   把「病人的视觉长什么样」从 `PatientVisualSpawner` 里**抽成接缝**。此前它是**写死**的
//   `GameObject.CreatePrimitive(Capsule)` + 中性灰材质,而真资产归 **13 病人 AI / 美术资产轮**
//   (`Material(p)` 与 8 的词表)。
//
// ⚠️ **为什么住表现层而不是 `Sim.Contracts`**:
//   本接口的签名**必须**出现 `GameObject`(它是 Unity 类型)⇒ 物理上住不进
//   `Sim.Contracts`(该程序集引用集恰 = BCL,ADR-025 §① / b2 门)。
//   与 `IPatientVisualAnchorSource` 的分居**是刻意的**,同 ADR-029 / ADR-031 的
//   「契约住 `Sim.Contracts`、实现住兄弟装配」模式。
//
// ⚠️ **factory 只管「建/释放」,不管「摆在哪」**:
//   位置归 `PatientVisualSpawner`(它把 `WorldPos` 换算成连续坐标后写入 transform)。
//   ⇒ 换资产时**位置逻辑一字不动**,换视觉时**位置逻辑也一字不动** —— 两者正交。
//
// ⚠️ **本接口不持有任何「已建实例」列表**:簿记归 spawner(`_visuals` 字典)。
//   factory 是**无状态**的(可自由重复调用),这让「同一 factory 被两个 spawner 共享」
//   也不出错 —— 但它**不得**缓存任何东西,否则 teardown 会漏。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Patients
{
    /// <summary>病人视觉实体的**构造器** —— 「给这个病人生成一个视觉根」的唯一合法入口。
    /// <para><b>无状态</b>:每次调用都新建一个根节点;不缓存、不持有实例列表(簿记归 spawner)。</para>
    /// <para>⚠️ **必须返回「零语义」的视觉**:不得在 factory 内映射体征 / 病种 / 九态
    /// —— 那是 13 的 `Material(p)` 与 8 的词表(本接口只回答「长什么样」的**几何与材质载体**,
    /// 不回答「此刻该显示什么状态」)。</para>
    /// </summary>
    public interface IPatientVisualFactory
    {
        /// <summary>为一个病人生成视觉根节点(未定位 —— 位置由调用方随后写入)。
        /// <para>⚠️ **不得**在此设置世界坐标:factory 不知道病人该在哪
        /// (位置归 <c>IPatientVisualAnchorSource</c> + spawner 的格换算)。</para>
        /// </summary>
        /// <param name="patientId">病人 id(用于命名 / 材质分桶等确定性用途)。</param>
        /// <returns>新建的视觉根节点。**调用方负责**挂父节点、定位、以及最终
        /// <see cref="Destroy"/>。</returns>
        GameObject Create(int patientId);

        /// <summary>释放一个由本 factory 建的视觉根。
        /// <para>⚠️ 必须是**幂等 + null 容忍**的:`null` 传入 ⇒ no-op;已销毁 ⇒ no-op。
        /// 理由:teardown 路径(BootRoot `OnDestroy` / PlayMode 各测试)在异常路径上
        /// 可能重复调用,抛异常会把「清理」变成「二次故障」。</para>
        /// </summary>
        /// <param name="visual">此前由 <see cref="Create"/> 返回的根节点(可为 null / 已销毁)。</param>
        void Destroy(GameObject visual);
    }
}
