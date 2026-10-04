// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(四源)/ 4-DC-2 / F-4.1b
//   ADR-016 §三(感知输入 = 粗粒度整数格;禁读表现态位置 —— 第二 QoS 到达时序不定)
//   ADR-009 §五(掉落 = 身份进流 / 位置表现;`spawn_anchor` 取整数格)
//   ADR-015 §三(单一整数格 `WorldPos` —— 四源格坐标的唯一类型)
//   ADR-014(烘焙逻辑层 `world_*.cooked` 的形状 —— 本文件只签**输入形状**,不签装载)
//
// ⚠️ 本文件的九个类型是 AC-4-03 的**具名产物**(GDD 首轮点名:原稿「4 的候选集构造路径」
//   是**无名范畴** ⇒ 无从枚举被测对象;须先命名再断言)。
//
// ⚠️ AC-4-03 的判据 = **反射全部实例字段的类型闭包**,零 `Vector3` / `Vector2` /
//   `Quaternion` / `Transform` / 任何 `UnityEngine.*`。白名单 = `WorldPos`(∈ `Sim.Contracts`)、
//   `long` / `int` / 枚举、`StableIdSource`。**不是 grep** —— 值可装箱,类型骗不了反射。
//
// ⚠️ 本文件**零 `using UnityEngine`** —— 这是 AC-4-03 的结构半边(编译期即无该引用)。

using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    // ═══════════════════════════════════════════════════════════════
    //  第一源 —— 世界流 `DropSpawned.spawn_anchor`(ADR-009 §五)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 掉落实体的**候选输入形状**(第一源:世界流重放视图)。
    /// <para><see cref="Anchor"/> 取 <c>DropSpawned.spawn_anchor</c> 的整数格 ——
    /// <b>不是</b>掉落物的表现态位置(ADR-009 §五:身份进流 / 位置表现)。</para>
    /// <para>⚠️ 被 <c>DropClaimed</c> 移除者不进装载(重放视图性质,归调用方)。</para>
    /// </summary>
    /// <remarks>AC-4-03 反射对象之一。零引擎类型、零结算侧类型(AC-4-01/05)。</remarks>
    public readonly struct DropDto
    {
        /// <summary>掉落锚点整数格(<c>spawn_anchor</c>)。</summary>
        public readonly WorldPos Anchor;

        /// <summary>掉落实体身份(<c>instance_id</c>,21 的 <c>ItemInstanceId</c> 空间)。</summary>
        public readonly long InstanceId;

        public DropDto(WorldPos anchor, long instanceId)
        {
            Anchor = anchor;
            InstanceId = instanceId;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  第二源 —— 6 的烘焙逻辑层(侦察点 / POI 格 / 建造槽位;ADR-015 §一)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>采集点输入形状(第二源:6 烘焙资源点;<see cref="BakedResourceIndex"/> 定稳定序)。</summary>
    public readonly struct ForageSpotDto
    {
        /// <summary>资源点整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>烘焙资源索引(6 的逻辑层序;4-DC-4 的 <c>BakedResourceIndex</c>)。</summary>
        public readonly long BakedResourceIndex;

        public ForageSpotDto(WorldPos cell, long bakedResourceIndex)
        {
            Cell = cell;
            BakedResourceIndex = bakedResourceIndex;
        }
    }

    /// <summary>POI 格输入形状(第二源:6 烘焙逻辑层定义;<c>poi_id</c> 定稳定序 —— ADR-021)。</summary>
    /// <remarks>⚠️ 本类型是 POI 的**定义**侧(派生态)。POI 的**状态**(已清与否)归 6 的世界流
    /// 承载(ADR-021 §①②),**不进**候选集 —— 候选集只回答「哪里可交互」,不回答「能否交互」。</remarks>
    public readonly struct PoiCellDto
    {
        /// <summary>POI 整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>POI 身份(<c>poi_id</c>,ADR-021 空间)。</summary>
        public readonly long PoiId;

        public PoiCellDto(WorldPos cell, long poiId)
        {
            Cell = cell;
            PoiId = poiId;
        }
    }

    /// <summary>建造槽位输入形状(第二源:6 烘焙逻辑层槽位;<see cref="SlotLinearKey"/> 定稳定序)。</summary>
    public readonly struct BuildSlotDto
    {
        /// <summary>槽位整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>线性键 <c>x + W·(y + H·z)</c>(需 <c>W/H/D</c> 在场;4-DC-4)。</summary>
        public readonly long SlotLinearKey;

        public BuildSlotDto(WorldPos cell, long slotLinearKey)
        {
            Cell = cell;
            SlotLinearKey = slotLinearKey;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  第三源 —— 13 的 IPresentPatients 只读在场视图(ADR-016 §六)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 病人候选输入形状(第三源:13 的只读在场视图)。
    /// <para>⚠️ <b>只有格与 id</b> —— <c>VitalsDto</c> / 病种 / 档位 / 药性一律**不入**
    /// (AC-4-05:story 001 的反射机器守之;本类型属其闭包)。</para>
    /// <para>⚠️ 病人格序列是**显式入参**(F-4.1b / AC-4-16 出处纪律)——
    /// 4 内**不得**出现病人位置生成 / 插值代码(表现态平滑归 13 / 42)。</para>
    /// </summary>
    public readonly struct PatientDto
    {
        /// <summary>病人整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>病人身份(<c>patient_id</c>,ADR-006 Amendment B 高水位空间)。</summary>
        public readonly long PatientId;

        public PatientDto(WorldPos cell, long patientId)
        {
            Cell = cell;
            PatientId = patientId;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  第四源 —— 23 BakedInitial(诊所开档自带的构建物)
    //  ⚠️ 这一源**没有任何流事件** —— 漏之则开档门柜不可交互且不报错
    //     (静默失效;AC-4-22 是它唯一的证伪夹具)。
    // ═══════════════════════════════════════════════════════════════

    /// <summary>器具 / 工具输入形状(第四源:23 <c>BakedInitial</c>;<c>structure_id</c> 定稳定序)。</summary>
    public readonly struct UtensilDto
    {
        /// <summary>器具整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>结构身份(<c>structure_id</c>,23 的实例表)。</summary>
        public readonly long StructureId;

        public UtensilDto(WorldPos cell, long structureId)
        {
            Cell = cell;
            StructureId = structureId;
        }
    }

    /// <summary>医馆面板输入形状(第四源:23 <c>BakedInitial</c>)。</summary>
    public readonly struct ClinicPanelDto
    {
        /// <summary>面板整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>结构身份(<c>structure_id</c>)。</summary>
        public readonly long StructureId;

        public ClinicPanelDto(WorldPos cell, long structureId)
        {
            Cell = cell;
            StructureId = structureId;
        }
    }

    /// <summary>门输入形状(第四源:23 <c>BakedInitial</c>)。</summary>
    public readonly struct DoorDto
    {
        /// <summary>门整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>结构身份(<c>structure_id</c>)。</summary>
        public readonly long StructureId;

        public DoorDto(WorldPos cell, long structureId)
        {
            Cell = cell;
            StructureId = structureId;
        }
    }

    /// <summary>开关输入形状(第四源:23 <c>BakedInitial</c>)。</summary>
    public readonly struct SwitchDto
    {
        /// <summary>开关整数格。</summary>
        public readonly WorldPos Cell;

        /// <summary>结构身份(<c>structure_id</c>)。</summary>
        public readonly long StructureId;

        public SwitchDto(WorldPos cell, long structureId)
        {
            Cell = cell;
            StructureId = structureId;
        }
    }
}
