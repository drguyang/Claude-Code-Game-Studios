// 23 的**最小结构视觉载体**(2026-11-10 · T22 批一 · 结 `TD-building-05` 的躯干)
//
// 权威来源:
//   ADR-015 §一(两层世界:纯视觉层 float 任意,采样**禁入 sim**)
//   ADR-023 §②(`World.unity` **零 gameplay GameObject** —— 预摆 = 第二真源)
//   `production/milestones/p0-sweep-2026-10-29.md:308`(「单色 proxy 灰盒即可,**不需要真美术资产**」)
//   `TD-building-05` 的解除动作(「按 `module_id` 出占位网格,位置 = 槽位格;
//     ⚠️ 不要求美术资产,只要求**可见 + 可辨识 + 与格对齐**」)
//
// ═══ 本类补的是什么 ═══════════════════════════════════════════════════════
//   此前运行期**唯一**被实例化的 3D 物体是病人胶囊(`CapsulePatientVisualFactory`)——
//   灶台 / 墙 / 门 / 柜 / 医馆面板**没有任何可视载体** ⇒ 玩家在画面上看不到目标,
//   系统 18(炮制)/ 20(容器)/ 23(建造)/ 24(面板)的入口**全部不可发现**。
//   本类与病人胶囊同构:**每格出一个可辨识体块**,零美术资产。
//
// ═══ 做法(以及为什么这样做)════════════════════════════════════════════════
//   · **运行期生成,不预摆** —— ADR-023 §② 要求 `World.unity` 零 gameplay 对象;
//     几何来自 `WorldBuildSlotsDataSet`(派生态,ADR-009 §四)⇒ 加载期重建,不进流。
//   · **形状 + 尺寸 + 色相三通道都不同** —— 单靠颜色承载「这是什么」违反无障碍纪律
//     (`accessibility-requirements.md`);尺寸差异保证在灰度下也可分。
//   · **材质只有一个真源** —— 底材质借用病人占位那张 `Resources` 资产(`TD-build-03` 的教训:
//     运行期 `Shader.Find` 手搓材质 ⇒ 打包版把着色器当未使用剥离 ⇒ 恒抛「找不到 URP Lit」)。
//     本类只做 `new Material(base)` 的**实例化着色**,不新建资产、不 `Shader.Find`。
//     ⚠️ 因此**这张材质资产同时服务病人与结构** —— 改名 / 移动要两个 factory 一起改。
//   · **摘掉 Collider** —— 占位实体不吃物理(与病人胶囊同纪律;`PlayerController` 走 `CharacterController`)。
//
// ⚠️ 本类**不判**:玩家放下的结构(`StructureInstanceRegistry` 的实例)怎么画 —— 那要读事件流,
//   归 23 的接线轮;本类只画**基线骨架**里**真的站着东西**的那些行(`BakedInitial == true`)。
//   空槽(`BakedInitial == false`)由 spawner 跳过 —— 给它画个体块等于告诉玩家「这里已经有帐篷了」。

using DaYiJingCheng.Sim.World;
using UnityEngine;

namespace DaYiJingCheng.Gameplay.Presentation.Building
{
    /// <summary>把一个建造槽位画成**可辨识的占位体块**(灰盒,零美术资产)。</summary>
    public sealed class StructureVisualFactory
    {
        /// <summary>占位底材质的 Resources 名(与病人占位共用同一张资产,见文件头)。</summary>
        public const string SharedPlaceholderMaterialResourcePath = "PatientPlaceholderMaterial";

        /// <summary>格边长(米/格)。
        /// <para>⚠️ 占位期取 **1 m/格**,与 `PatientVisualSpawner.CellToWorld` 同一口径;
        /// 真值归 ADR-015 §三 的世界格定义。改它 = 两处一起改(病人与结构必须同尺度,
        /// 否则「看得到的」与「选得中的」不是同一个东西)。</para></summary>
        internal const float MetersPerCell = 1f;

        private Material _base;
        private readonly Material[] _tintedByType = new Material[32];

        /// <summary>构造。</summary>
        /// <param name="baseMaterial">底材质;**null = 按 <see cref="SharedPlaceholderMaterialResourcePath"/>
        /// 从 `Resources` 取**(生产路径)。夹具可注入自造材质以免依赖资产。</param>
        public StructureVisualFactory(Material baseMaterial = null)
        {
            _base = baseMaterial;
        }

        /// <summary>创一个槽位的视觉实体(位置**未**设置 —— 由 spawner 随后写入)。</summary>
        /// <param name="allowedMask">允许的 `SlotType` 位掩码(`1 &lt;&lt; 枚举序数`)。</param>
        /// <param name="label">名字里的人读后缀(通常是格坐标),只影响 `GameObject.name`。</param>
        /// <returns>已就位比例的 <see cref="GameObject"/>;底材质取不到时返回 null(不让半个世界凭空消失)。</returns>
        public GameObject Create(int allowedMask, string label)
        {
            SlotType type = SlotTypeOf(allowedMask);
            Material material = MaterialFor(type);
            if (material == null) return null;

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = $"StructureVisual_{type}_{label}";

            // 占位实体不吃物理 —— 双路径销毁纪律同 `CapsulePatientVisualFactory`(编辑器非播放态须 Immediate)
            var collider = box.GetComponent<Collider>();
            if (collider != null)
            {
                if (!Application.isPlaying) Object.DestroyImmediate(collider);
                else Object.Destroy(collider);
            }

            box.transform.localScale = ScaleOf(type);
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        /// <summary>销毁(幂等;双路径,同病人 factory)。</summary>
        public void Destroy(GameObject visual)
        {
            if (visual == null) return;
            if (!Application.isPlaying) { Object.DestroyImmediate(visual); return; }
            Object.Destroy(visual);
        }

        /// <summary>释放本 factory 自造的材质实例(teardown 用;底材质不归本类所有)。</summary>
        public void Dispose()
        {
            for (int i = 0; i < _tintedByType.Length; i++)
            {
                if (_tintedByType[i] == null) continue;
                if (!Application.isPlaying) Object.DestroyImmediate(_tintedByType[i]);
                else Object.Destroy(_tintedByType[i]);
                _tintedByType[i] = null;
            }
        }

        /// <summary>位掩码 → 最低置位的槽位类型(0 = <see cref="SlotType.None"/>,无位)。
        /// <para>⚠️ 一个槽可允许**多种**类型(`allowed_mask` 是位域);占位视觉只画**最低位**那一种
        /// —— 视觉不是权威,权威是 4 的选择器读同一个掩码。</para></summary>
        public static SlotType SlotTypeOf(int allowedMask)
        {
            if (allowedMask <= 0) return SlotType.None;
            for (int bit = 0; bit < 31; bit++)
                if ((allowedMask & (1 << bit)) != 0)
                    return (SlotType)bit;
            return SlotType.None;
        }

        /// <summary>类型 → 局部缩放(米)。尺寸是可辨识的**主通道**(不靠颜色)。</summary>
        public static Vector3 ScaleOf(SlotType type)
        {
            switch (type)
            {
                case SlotType.Shell:       return new Vector3(0.95f, 1.60f, 0.25f);  // 墙 / 地基:薄而高
                case SlotType.Door:        return new Vector3(0.70f, 1.40f, 0.18f);  // 门:比墙窄一点
                case SlotType.Tent:        return new Vector3(1.60f, 1.50f, 1.60f);  // 帐篷:最宽
                case SlotType.Utensil:     return new Vector3(0.90f, 0.80f, 0.90f);  // 灶台:方而矮
                case SlotType.ClinicPanel: return new Vector3(0.50f, 1.10f, 0.15f);  // 面板:立牌
                case SlotType.Switch:      return new Vector3(0.25f, 0.25f, 0.12f);  // 开关:最小
                case SlotType.Decor:       return new Vector3(0.40f, 0.50f, 0.40f);  // 陈设:小方块
                case SlotType.None:        return new Vector3(0.30f, 0.30f, 0.30f);
                default:                   return new Vector3(0.90f, 0.70f, 0.90f);  // 床位 / 台 / 柜 / 圃
            }
        }

        /// <summary>类型 → 色相(次要通道;尺寸已在主通道区分)。</summary>
        public static Color TintOf(SlotType type)
        {
            switch (type)
            {
                case SlotType.Shell:       return new Color(0.85f, 0.82f, 0.76f);  // 土墙
                case SlotType.Door:        return new Color(0.62f, 0.45f, 0.28f);  // 木
                case SlotType.Tent:        return new Color(0.70f, 0.62f, 0.45f);  // 帆布
                case SlotType.Utensil:     return new Color(0.72f, 0.36f, 0.26f);  // 灶:砖红
                case SlotType.ClinicPanel: return new Color(0.86f, 0.80f, 0.62f);  // 纸
                case SlotType.Switch:      return new Color(0.25f, 0.25f, 0.28f);  // 铁
                case SlotType.Decor:       return new Color(0.45f, 0.55f, 0.42f);  // 草木
                default:                   return new Color(0.70f, 0.70f, 0.70f);  // 中性灰(病人胶囊同色)
            }
        }

        private Material MaterialFor(SlotType type)
        {
            if (_base == null) _base = Resources.Load<Material>(SharedPlaceholderMaterialResourcePath);
            if (_base == null) return null;

            int i = (int)type;
            if (i < 0 || i >= _tintedByType.Length) i = 0;
            if (_tintedByType[i] == null)
                _tintedByType[i] = new Material(_base) { color = TintOf(type) };
            return _tintedByType[i];
        }
    }
}
