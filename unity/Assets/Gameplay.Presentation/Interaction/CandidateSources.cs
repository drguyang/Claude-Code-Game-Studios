// ADR 权威来源:
//   GDD design/gdd/interaction-system.md —— 规则二(候选集 = 四源,全部整数格)
//   ADR-016 §三(禁读表现态位置构造 / 过滤候选集 —— 判据 = 反射字段类型,不是 grep)
//   ADR-009 §一/§四(候选集 = 派生态:由外生源确定性重建,**不进流、不存档**)
//   ADR-015 §一/§四(四源格坐标全部住同一逻辑层整数格;chunk 驻留不得改变判定 —— `OQ-6-8`)
//
// ⚠️ 四个源各是一个**可注入的只读接口** —— 生产只依赖接口,真身(世界流重放视图 /
//   `world_*.cooked` / 13 视图 / 23 表)由装配注入。测试侧全部以替身驱动。
//
// ⚠️ 接口**只出整数格**(`WorldPos`),绝不出 `Vector3` / `Transform`(AC-4-03 的同源纪律)。

using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Interaction
{
    /// <summary>
    /// 候选源(四源共用的形状)。<see cref="Loader"/> = 本故事的交付物根类型。
    /// <para>四源皆**只读**、**无状态副作用** —— 装载是派生态重建(ADR-009 §四),
    /// 不写三流、不缓存可选状态。</para>
    /// </summary>
    /// <typeparam name="TRow">该源的具名输入形状(见 <c>SourceDtos.cs</c>)。</typeparam>
    public interface ICandidateSource<out TRow>
    {
        /// <summary>枚举本源的当前候选行(整数格;顺序**不承载语义** —— 全序由 F-4.1 三键定)。</summary>
        IReadOnlyList<TRow> Rows { get; }
    }

    /// <summary>
    /// <b>第一源</b> —— 世界流重放视图:<c>DropSpawned.spawn_anchor</c> 锚格,
    /// 未被 <c>DropClaimed</c> 移除者(ADR-009 §五:身份进流 / 位置表现)。
    /// </summary>
    public interface IDropSource : ICandidateSource<DropDto> { }

    /// <summary>
    /// <b>第二源</b> —— 6 的烘焙逻辑层:POI 定义格 / 采集点 / 建造槽位(ADR-015 §一,
    /// 经 ADR-022 关卡工具导出 + ADR-014 烘焙)。
    /// <para>⚠️ 三者合为一个源接口(同一张烘焙逻辑层的三个切片),不拆三个接口 ——
    /// 拆分会让「四源」变成「六源」,与 GDD 规则二的计数不符。</para>
    /// </summary>
    public interface IWorldBakedSource
    {
        /// <summary>烘焙 POI 定义格。</summary>
        IReadOnlyList<PoiCellDto> PoiCells { get; }

        /// <summary>烘焙采集点(资源点)。</summary>
        IReadOnlyList<ForageSpotDto> ForageSpots { get; }

        /// <summary>烘焙建造槽位。</summary>
        IReadOnlyList<BuildSlotDto> BuildSlots { get; }
    }

    /// <summary>
    /// <b>第三源</b> —— 13 的 <c>IPresentPatients</c> 只读在场视图(ADR-016 §六:
    /// 37 读 13,13 不引用 37;4 读 13 的在场视图)。
    /// <para>⚠️ 只出「格 + patient_id」。<c>VitalsDto</c> 物理上不进本接口的返回类型。</para>
    /// </summary>
    public interface IPatientSource : ICandidateSource<PatientDto> { }

    /// <summary>
    /// <b>第四源</b> —— 23 的 <c>BakedInitial</c>(诊所开档自带的构建物:门 / 柜 / 工作台 / 面板 / 开关)。
    /// <para>🔴 <b>这一源没有任何流事件</b> —— 它是版本化烘焙数据(ADR-009 §四 的派生态)。
    /// 漏之则开档门柜**不可交互且不报错**(静默失效)—— <c>AC-4-22</c> 是它唯一的证伪夹具。</para>
    /// </summary>
    public interface IBakedInitialSource
    {
        /// <summary>开档自带的器具 / 工具。</summary>
        IReadOnlyList<UtensilDto> Utensils { get; }

        /// <summary>开档自带的医馆面板。</summary>
        IReadOnlyList<ClinicPanelDto> ClinicPanels { get; }

        /// <summary>开档自带的门。</summary>
        IReadOnlyList<DoorDto> Doors { get; }

        /// <summary>开档自带的开关。</summary>
        IReadOnlyList<SwitchDto> Switches { get; }
    }

    /// <summary>
    /// 四源候选集的**装配依赖包**(显式入参 —— 禁服务定位器 / 单例)。
    /// <para>承 <c>coding-standards.md</c>:「所有公开方法须可单测(dependency injection over singletons)」。</para>
    /// </summary>
    public readonly struct CandidateSources
    {
        /// <summary>第一源:世界流掉落锚格。</summary>
        public readonly IDropSource Drops;

        /// <summary>第二源:6 烘焙逻辑层。</summary>
        public readonly IWorldBakedSource WorldBaked;

        /// <summary>第三源:13 在场病人视图。</summary>
        public readonly IPatientSource Patients;

        /// <summary>第四源:23 开档自带构建物。</summary>
        public readonly IBakedInitialSource BakedInitial;

        public CandidateSources(IDropSource drops,
                                IWorldBakedSource worldBaked,
                                IPatientSource patients,
                                IBakedInitialSource bakedInitial)
        {
            Drops = drops;
            WorldBaked = worldBaked;
            Patients = patients;
            BakedInitial = bakedInitial;
        }
    }
}
