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
// ⚠️ **一个资产依赖**(2026-11-05 起,`TD-build-03`):`Resources/PatientPlaceholderMaterial.mat`。
//   上一句原文写「零资产依赖 …… 一个运行时建的材质」—— **该说法的代价已兑现为缺陷**:
//   材质在运行期由 `Shader.Find` 手搓 ⇒ **没有任何资产引用那个着色器** ⇒ player 构建把
//   URP Lit 当未使用剥离 ⇒ 打包版恒抛「找不到 URP Lit」,病人**根本画不出来**
//   (实测日志 2489 条)。现改为加载资产,着色器随资产依赖进包。
//   ⇒ 「等真实资产兑换」的前提不变:要删的东西现在**多一个** `.mat`(与本文件同批删)。

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

        /// <summary>占位材质的 Resources 资源名(相对 `Resources/` 的路径,不带扩展名)。
        /// <para>⚠️ **颜色不在这里** —— 中性灰 `(0.7, 0.7, 0.7)` 的唯一真源就是那个 `.mat`。
        /// 此处**刻意不保留色值常量**:留着它就会在「改资产」与「改常量」之间有第二条路,
        /// 而两条路必然分叉(改了一处、另一处仍是旧值,且两边都不报错)。</para>
        /// <para>资产出处:`Editor.Tools.Bake/Cli/CreatePatientPlaceholderMaterialCli.cs`
        /// (只在资产**不存在**时创建,故不会与本行路径脱节)。</para></summary>
        internal const string PlaceholderMaterialResourcePath = "PatientPlaceholderMaterial";

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
            capsule.GetComponent<Renderer>().sharedMaterial = LoadPlaceholderMaterial();
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

        /// <summary>加载占位胶囊的中性灰材质资产(`Resources/PatientPlaceholderMaterial.mat`)。
        /// <para>🔴 <b>本条是 `TD-build-03` 的修复本体</b>(2026-11-05):修复前在**运行期**
        /// 用 <c>Shader.Find</c> 现找着色器 —— 而着色器只被代码字符串引用,没有资产引用它 ⇒
        /// player 构建把它当未使用**剥离** ⇒ 打包版恒抛「找不到 URP Lit」,病人**画不出来**。
        /// 现改为加载资产:资产引用 = 构建期的可见依赖,着色器随之进包。</para>
        /// <para>⚠️ <b>不得加回 `Shader.Find` 回退</b>:它会让「资产丢了」这一缺陷在编辑器与
        /// EditMode 夹具里**完全隐身**(编辑器内置 shader 恒在),只在打包版发作 ——
        /// 那正是本缺陷活了这么久的原因。</para>
        /// <para>⚠️ <b>失败必须显性</b>:加载不到就抛,不得返回 null 让 Unity 走错误着色器(品红)。
        /// 品红在实机上是「本帧是否正常渲染」的观察噪声(<c>3b97a850</c> 的教训)。</para>
        /// </summary>
        internal static Material LoadPlaceholderMaterial()
        {
            var mat = Resources.Load<Material>(PlaceholderMaterialResourcePath);

            if (mat == null)
                throw new System.InvalidOperationException(
                    $"[patient-visual] 加载不到 Resources/{PlaceholderMaterialResourcePath}.mat —— " +
                    "占位材质资产缺失或路径变了。继续下去 Unity 会走错误着色器(品红)," +
                    "而品红在实机上是「本帧是否正常渲染」的观察噪声。");

            return mat;
        }
    }
}
