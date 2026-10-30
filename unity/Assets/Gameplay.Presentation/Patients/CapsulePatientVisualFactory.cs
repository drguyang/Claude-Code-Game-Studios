// 权威来源:
//   ADR-013 §9 C3(呈现层只渲染,永不持有游戏状态)
//   `production/milestones/p0-sweep-2026-10-29.md:308`(「一个**单色人形 proxy 灰盒**即可,
//     **不需要真美术资产**」—— 本类是这句话的**代码兑现**)
//   3b97a850(2026-10-20)缺陷 B 修复:URP 下 `Shader.Find("Standard")` 返回 null ⇒ 品红占位
//
// 🔁 **2026-10-30(`TD-prescription-06` 的「灰盒做好,等真实资产兑换」轮)**:
//   从 `Gameplay.Boot/PatientVisualSpawner.cs` 的 `OnTickEdge` + `CreatePlaceholderMaterial`
//   **原样迁来**(零行为变更),只把「建什么」与「建在哪」分开。
//   ⇒ 兑换真资产 = 在 `CompositionRoot` 把本类换成 `PrefabPatientVisualFactory`,
//      **spawner / 位置 / 交互 / 夹具全部不动**。
//
// ⚠️ **零资产依赖**:本类不用 prefab、不用 Addressables、不读 `assets/data/`
//   —— 灰盒的全部内容 = 一个 `CreatePrimitive(Capsule)` + 一个运行时建的材质。
//   这正是「等真实资产兑换」的前提:没有需要一起删掉的资产文件。

using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Patients
{
    /// <summary>病人灰盒 proxy 的 factory —— 胶囊体 + 中性灰(零语义、零资产依赖)。
    /// <para><b>这是占位实现</b>,不是最终形态。真资产(13 的 `Material(p)` / 美术轮)来了之后,
    /// 换掉装配根里的这一行即可,本类可整体退役。</para>
    /// </summary>
    public sealed class CapsulePatientVisualFactory : IPatientVisualFactory
    {
        /// <summary>胶囊的默认局部缩放 —— 高 1.0、粗 0.6,视觉上接近「一个人」的细长比。</summary>
        private static readonly Vector3 DefaultScale = new Vector3(0.6f, 1.0f, 0.6f);

        /// <summary>中性灰 —— 零语义(不映射体征 / 病种 / 九态;那归 13 的 `Material(p)` + 8 词表)。</summary>
        private static readonly Color NeutralGrey = new Color(0.7f, 0.7f, 0.7f);

        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ **不设位置** —— 位置由 spawner 随后写入(见接口契约)。
        /// ⚠️ **摘掉 Collider**:占位实体不吃物理(`PlayerController` 走 `CharacterController`,
        /// 病人视觉不参与 PhysX 求解 —— 承 ADR-020 §四 的「玩家位移纯表现态」同纪律)。
        /// </remarks>
        public GameObject Create(int patientId)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = $"PatientVisual_{patientId}";

            var collider = capsule.GetComponent<Collider>();
            // 占位实体不吃物理 —— 同 Destroy 的双路径纪律(编辑器非播放态须 DestroyImmediate)
            if (collider != null)
            {
                if (!Application.isPlaying) Object.DestroyImmediate(collider);
                else Object.Destroy(collider);
            }

            capsule.transform.localScale = DefaultScale;
            capsule.GetComponent<Renderer>().sharedMaterial = CreatePlaceholderMaterial();
            return capsule;
        }

        /// <inheritdoc />
        /// <remarks>
        /// ⚠️ **销毁时机双路径**(承 `AudioSourcePool.cs:389-395` 的同款惯例):
        /// `Object.Destroy` 在**编辑器非播放态**会报
        /// 「Destroy may not be called from edit mode! Use DestroyImmediate instead」——
        /// 那会让 EditMode 夹具与任何编辑器内的 teardown 变成**二次故障**。
        /// ⇒ 非播放态走 `DestroyImmediate`,播放态走 `Destroy`(后者延迟到帧末,是运行期正解)。
        /// </remarks>
        public void Destroy(GameObject visual)
        {
            // 幂等 + null 容忍(接口契约):Unity 的 `Destroy(null)` 本身是 no-op,
            // 但「已销毁」的对象在 `==` 重载下会判为 null ⇒ 这一句同时覆盖两种情形。
            if (visual == null) return;

            if (!Application.isPlaying) { Object.DestroyImmediate(visual); return; }
            Object.Destroy(visual);
        }

        /// <summary>占位胶囊的中性灰材质(URP Lit 优先;Standard 仅作非 URP 构建的回退)。
        /// <para>⚠️ 必须按属性存在性写色:URP Lit 用 <c>_BaseColor</c>,<c>Standard</c> 用 <c>_Color</c>
        /// (<c>Material.color</c> 只映射后者)。错误着色器(品红)在实机上是可观测的回归信号 ——
        /// 它会让「本帧是否正常渲染」这一观察失效,不得以「占位无所谓」放过。</para>
        /// <para>🔴 <b>本条是 `3b97a850` 的修复本体</b>(2026-10-20):修复前写
        /// <c>Shader.Find("Standard")</c>,在 URP 工程下返回 <c>null</c> ⇒ Unity 走错误着色器
        /// ⇒ 胶囊**通体品红**(用户实机报告「品红色病人胶囊占位」)。</para>
        /// </summary>
        internal static Material CreatePlaceholderMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // ⚠️ 两个都找不到 ⇒ **必须显性失败**,不得静默建出一个品红材质。
            //    静默回退正是 `3b97a850` 之前的状态 —— 那时没有人发现,直到用户实机报告。
            if (shader == null)
                throw new System.InvalidOperationException(
                    "[patient-visual] 找不到 URP Lit 也找不到 Standard —— 无法建占位材质。" +
                    "继续下去 Unity 会走错误着色器(品红),而品红在实机上是「本帧是否正常渲染」的观察噪声。");

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", NeutralGrey);
            else mat.color = NeutralGrey;
            return mat;
        }
    }
}
