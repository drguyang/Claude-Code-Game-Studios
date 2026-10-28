// S20-19 · 42 建造发起方接线 —— 建造输入驱动
//
// 权威来源:
//   design/gdd/skeuomorphic-ui.md §建造交互(本批新增 —— 建造 = **世界内操作态**,
//     不摊开界面、不占 `ModalId`)
//   design/gdd/modular-building.md `O-23-6`(手柄幽灵**循环**选择 + float→格量化器)
//   ADR-011 §一(输入读取 = 设备直读过渡最小面,承 `InteractionInputDriver` 同构)
//   ADR-013 §十(`ModalId` 闭集 7 员 —— 建造不占模态)
//   ADR-020 §四(位移纯表现态;本驱动只读**已量化**的整数格)
//
// ─────────────────────────────────────────────────────────────────────────────
// 本驱动持的**全部**状态 = 表现态(ADR-009 §四 派生态)
// ─────────────────────────────────────────────────────────────────────────────
//   `IsBuildMode` / `SelectedModuleId` / `Orientation` = 玩家的**操作态**,不是世界状态。
//   它们**不进三流、不存档** —— 读档后回到「非建造模式 + 默认朝向」是**正确**行为,
//   不是丢状态。任何把它们写进事件的改动都会让「读档后自动进入建造模式」成为可能。
//
// ─────────────────────────────────────────────────────────────────────────────
// 量化器:唯一实现点 = `CellTransitionDetector.CellFromPosition`
// ─────────────────────────────────────────────────────────────────────────────
// 本驱动**不做**任何 float→格转换 —— 它收的是调用方给的 `Func<WorldPos>`。
// 生产接线面 = `PlayerController.Cell`(`PlayerController.cs:407`),其内部即
// `CellTransitionDetector.CellFromPosition`(`Mathf.FloorToInt` 三轴)。
// `IPlayerMotor.cs:58` 逐字要求实现必须走该函数 ⇒ 建造取格与移动取格**同源**,
// 全仓不得出现第二处 floor 转换(B-1 的门判据)。
//
// ─────────────────────────────────────────────────────────────────────────────
// ⚠️ 已知边界(挂标,不是已解决)
// ─────────────────────────────────────────────────────────────────────────────
//   ① **不检查模态开集**:建造模式开着时若摊开脉案/出诊箱,`Enter` 会同时被
//      UI `Submit` 与建造放置消费。全仓今日**没有**「是否有模态摊开」的聚合读面
//      (`IModalState` 是逐屏实现,无聚合器)⇒ 本批不新造该聚合器(那会越权改 42 的
//      焦点单栈门)。登记在 `sprint-20` §十 S20-19 的「挂标不记绿」。
//   ② **XR 组无读面**:P0 无 VR 建造(见 `BuildInputReader` 头注)。

using System;
using DaYiJingCheng.Gameplay.Presentation.Building;
using DaYiJingCheng.Sim.Contracts;
using DaYiJingCheng.Sim.World;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 建造输入驱动 —— 每帧读四个建造动作的按下沿 → 调 <see cref="BuildController"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>零 sim 真值</b>:不持注册表副本、不缓存投影、不读墙钟
    /// (`tick` 由 <see cref="BuildController"/> 经 <c>Func&lt;long&gt;</c> 从 `ITickProvider` 取)。</para>
    /// </remarks>
    public sealed class BuildInputDriver
    {
        /// <summary>幽灵朝向循环序(度)。</summary>
        private static readonly int[] OrientationCycle = { 0, 90, 180, 270 };

        private readonly BuildController _controller;
        private readonly Func<WorldPos> _cell;
        private readonly Action _onCommitted;

        private bool _toggleHeld;
        private bool _placeHeld;
        private bool _rotateHeld;
        private bool _removeHeld;

        /// <summary>是否处于建造模式(表现态;初始 false)。</summary>
        public bool IsBuildMode { get; private set; }

        /// <summary>当前选中的模块 id(幽灵预览要造的那个)。</summary>
        public int SelectedModuleId { get; set; } = -1;

        /// <summary>当前幽灵朝向(0 / 90 / 180 / 270)。</summary>
        public int Orientation { get; private set; }

        /// <summary>最近一次放置的判定结果(观测面 / 测试接缝)。</summary>
        public PlaceableResult LastPlaceResult { get; private set; } = PlaceableResult.Success;

        /// <summary>最近一次拆除的判定结果(同上)。</summary>
        public DemolishResult LastRemoveResult { get; private set; } = DemolishResult.Success;

        /// <summary>最近一次旋转的判定结果(同上)。</summary>
        public ModifiableResult LastRotateResult { get; private set; } = ModifiableResult.Success;

        /// <summary>本驱动已成功落流的操作数(观测面;失败不计)。</summary>
        public int CommittedCount { get; private set; }

        /// <param name="controller">建造编排器(判定 + 落流)。</param>
        /// <param name="cell">玩家当前所在的**整数格**(生产面 = <c>PlayerController.Cell</c>)。</param>
        /// <param name="onCommitted">落流成功后的回调(生产面 = 重建库存投影 ——
        /// `StructurePlaced` 的扣减在 fold 里,投影不重建就读不到新余量)。
        /// 可为 null(EditMode 夹具不需要)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="controller"/> /
        /// <paramref name="cell"/> 为 null。</exception>
        public BuildInputDriver(BuildController controller, Func<WorldPos> cell, Action onCommitted = null)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _cell = cell ?? throw new ArgumentNullException(nameof(cell));
            _onCommitted = onCommitted;
        }

        /// <summary>每帧处理建造输入(由 <c>BootRoot.Update</c> 转发)。</summary>
        public void OnFrame()
        {
            bool toggle = BuildInputReader.TogglePressed();
            if (toggle && !_toggleHeld)
            {
                IsBuildMode = !IsBuildMode;
                if (!IsBuildMode) ResetEdges();   // 退出即清边沿:再进入时不会立刻触发
            }
            _toggleHeld = toggle;

            if (!IsBuildMode)
                return;

            bool place = BuildInputReader.PlacePressed();
            if (place && !_placeHeld) DoPlace();
            _placeHeld = place;

            bool rotate = BuildInputReader.RotatePressed();
            if (rotate && !_rotateHeld) DoRotate();
            _rotateHeld = rotate;

            bool remove = BuildInputReader.RemovePressed();
            if (remove && !_removeHeld) DoRemove();
            _removeHeld = remove;
        }

        /// <summary>放置一次(供夹具直调;与按键路径**同一入口**)。</summary>
        public PlaceableResult DoPlace()
        {
            WorldPos cell = _cell();
            LastPlaceResult = _controller.TryPlace(SelectedModuleId, cell, Orientation, 0);
            if (LastPlaceResult == PlaceableResult.Success)
                NotifyCommitted();
            return LastPlaceResult;
        }

        /// <summary>拆除当前格上的结构(供夹具直调)。</summary>
        public DemolishResult DoRemove()
        {
            long id = _controller.StructureIdAt(_cell());
            if (id < 0)
            {
                LastRemoveResult = DemolishResult.StructureNotFound;
                return LastRemoveResult;
            }

            LastRemoveResult = _controller.TryRemove(id);
            if (LastRemoveResult == DemolishResult.Success)
                NotifyCommitted();
            return LastRemoveResult;
        }

        /// <summary>
        /// 旋转:当前格上有结构 ⇒ 把它转 90°;没有 ⇒ 循环**幽灵**朝向。
        /// <para>⚠️ 两条分支**互斥**且判据是「格上有没有结构」(不是「建造模式」)——
        /// 幽灵朝向是待放置模块的朝向,已放置结构的朝向住在注册表里。</para>
        /// </summary>
        public void DoRotate()
        {
            long id = _controller.StructureIdAt(_cell());
            if (id < 0)
            {
                Orientation = NextOrientation(Orientation);
                return;
            }

            LastRotateResult = _controller.TryRotateBy(id, 90);
            if (LastRotateResult == ModifiableResult.Success)
                NotifyCommitted();
        }

        private static int NextOrientation(int current)
        {
            for (int i = 0; i < OrientationCycle.Length; i++)
                if (OrientationCycle[i] == current)
                    return OrientationCycle[(i + 1) % OrientationCycle.Length];
            return OrientationCycle[0];       // 非法当前值 ⇒ 归零(不抛:这是表现态,不是契约)
        }

        private void NotifyCommitted()
        {
            CommittedCount++;
            _onCommitted?.Invoke();
        }

        private void ResetEdges()
        {
            _placeHeld = false;
            _rotateHeld = false;
            _removeHeld = false;
        }
    }
}
