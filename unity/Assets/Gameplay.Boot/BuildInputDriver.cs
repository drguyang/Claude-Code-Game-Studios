// S20-19 · 42 建造发起方接线 —— 建造输入驱动
//
// 权威来源:
//   design/gdd/skeuomorphic-ui.md §建造交互(本批新增 —— 建造 = **世界内操作态**,
//     不摊开界面、不占 `ModalId`)
//   design/gdd/modular-building.md `O-23-6`(手柄幽灵**循环**选择 + float→格量化器)
//   ADR-011 §一(输入读取 = 设备直读过渡最小面,承 `InteractionInputDriver` 同构)
//   ADR-013 §十(`ModalId` 闭集 8 员 —— 建造不占模态;🔁 2026-10-31 Amendment C 前为 7 员)
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
//   ① ~~**不检查模态开集**~~ → 🔁 **2026-10-31 已收口**:当时全仓没有「是否有模态摊开」的
//      聚合读面(`IModalState` 是逐屏实现),现在有了 —— 装配层 `ModalGateStateAdapter`
//      就是那个聚合面(规则七方向②接线轮立的)。本驱动改收一个 `Func<bool> blocked`
//      (生产面 = `ModalInputFreeze.IsBlocked`,与**移动轴**同源),挡路期间四个动作
//      一个都不发。⚠️ 仍然**不新造**聚合器 —— 复用 42 的既有真源,不越权。
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
        private readonly Func<bool> _blocked;

        /// <summary>外部指定的**建造锚点格**(2026-11-02 · `TD-interaction-03`)。
        /// <para>`null` = 用玩家当前格(`_cell()`)。非 null = E 打到的那个建造槽位。</para>
        /// <para>⚠️ 是**表现态操作态**,不进三流、不存档(与本类其余字段同款)。</para></summary>
        private WorldPos? _aimCell;

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
        /// <param name="blocked">是否有挡路的模态摊开(生产面 = `ModalInputFreeze.IsBlocked`)。
        /// 可为 <c>null</c> ⇒ 恒不冻结(夹具与「无 UI」形态)。</param>
        public BuildInputDriver(BuildController controller, Func<WorldPos> cell, Action onCommitted = null,
                                Func<bool> blocked = null)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _cell = cell ?? throw new ArgumentNullException(nameof(cell));
            _onCommitted = onCommitted;
            _blocked = blocked;
        }

        /// <summary>
        /// E 打到建造槽位 ⇒ **进入建造模式并把幽灵锚到该格**(2026-11-02 · `TD-interaction-03`)。
        /// <para>语义裁定(本轮就地定,**未经数值 / UX 轮复审**):4 的全序选中 `BuildSlot` 时,
        /// 玩家的意图是「我要在这一格建东西」,而不是「我把建造模式开一下」。故这里同时做两件:
        /// 进模式 + 对准该格 —— 之后按 23 自己的放置键即在该槽位建造(不用先把角色走到格上)。</para>
        /// <para>⚠️ **不放置**:E 不落任何结构(误触即建一块家具的代价太大);放置键仍是 23 的。</para>
        /// <para>⚠️ **已在该模式时** = 改瞄准格(不退出、不抖动)。</para>
        /// </summary>
        /// <param name="cell">被选中的建造槽位格(4 的 `InteractTarget.Cell`)。</param>
        /// <remarks>返回 void:本驱动不决定「按键是否被消费」—— 那归 4 的路由(它调完即记消费)。</remarks>
        public void AimAt(WorldPos cell)
        {
            IsBuildMode = true;
            _aimCell = cell;
            // 进模式那一刻手里若还按着放置键,不得立刻落一块(与「模态挡路丢弃」同一纪律)。
            ResetEdges();
        }

        /// <summary>本帧处理建造输入(由 <c>BootRoot.Update</c> 转发)。</summary>
        public void OnFrame()
            => OnFrameCore(
                BuildInputReader.TogglePressed(),
                BuildInputReader.PlacePressed(),
                BuildInputReader.RotatePressed(),
                BuildInputReader.RemovePressed());

        /// <summary>
        /// 四个动作位的**唯一**处理入口(`OnFrame` 与夹具共用)。
        /// <para>⚠️ 单开这个接缝的原因:headless 下 `BuildInputReader` 是静态设备读面,
        /// **造不出按键** ⇒ 「模态挡路时动作被丢弃、且边沿被消费」这条判据在设备面上
        /// 不可观测,只能由布尔入参驱动。与 `PauseScreen.Activate(int)` / `SaveSlotsScreen.Activate(int)`
        /// 同款接缝纪律(判据必须能被夹具直接驱动)。</para>
        /// </summary>
        public void OnFrameCore(bool toggle, bool place, bool rotate, bool remove)
        {
            // 🔁 2026-10-31:模态挡路 ⇒ 四个动作一个都不发,但**边沿位照常更新**。
            //    ⚠️ 边沿位必须更新:若只是 return,关屏那一刻手里还按着的键会补触发一次
            //    (「模态期间没反应,一关屏就放下一块」)—— 与 `InteractionInputDriver`
            //    同纪律(门闭 ⇒ 丢弃动作,边沿仍跟踪)。
            // ✅ 2026-10-31 判据已补:落 `build_controller_test` 的「模态冻结」四条
            //    (正控进模式 / 挡路丢弃 / 边沿被消费不补触发 / 挡路期放置零落流且解除后恢复)
            //    —— 那里已有真 `BuildController` 装配,不另造第二套。
            if (_blocked != null && _blocked())
            {
                _toggleHeld = toggle;
                _placeHeld = place;
                _rotateHeld = rotate;
                _removeHeld = remove;
                return;
            }

            if (toggle && !_toggleHeld)
            {
                IsBuildMode = !IsBuildMode;
                if (!IsBuildMode)
                {
                    ResetEdges();   // 退出即清边沿:再进入时不会立刻触发
                    _aimCell = null; // 瞄准格随之失效(下次进入回到「玩家当前格」)
                }
            }
            _toggleHeld = toggle;

            if (!IsBuildMode)
                return;

            if (place && !_placeHeld) DoPlace();
            _placeHeld = place;

            if (rotate && !_rotateHeld) DoRotate();
            _rotateHeld = rotate;

            if (remove && !_removeHeld) DoRemove();
            _removeHeld = remove;
        }

        /// <summary>放置一次(供夹具直调;与按键路径**同一入口**)。</summary>
        public PlaceableResult DoPlace()
        {
            // 锚点 = 外部瞄准格(若 E 指过槽位),否则玩家当前格。
            WorldPos cell = _aimCell ?? _cell();
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
