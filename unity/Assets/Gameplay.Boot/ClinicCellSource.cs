// 「医馆格」判据源的生产占位实装(2026-10-25 52 接线轮 · 用户裁定「空集占位实现」)。
//
// 权威来源:
//   Sim.Contracts/IClinicCellSource.cs —— 谓词契约 + 同源义务三处(本谓词 /
//     规则十「无人」/ F1PipelineInput.IsInClinic)绑同一实现
//   GDD random-events.md 规则十 / 规则十二 12.1 —— 「医馆地块占用由 23 建造 / 24 医馆
//     提供(非 52 自算)」;触发的空间半边
//   2026-10-25 接线轮裁定② —— 生产空集占位:23/24 侧连「哪座结构算医馆」的标记
//     数据都还没有(StructureInstanceRegistry 生产树零构造),无法接真源。
//
// 核心机制:
//   - **当前持空格集 ⇒ IsClinicCell 恒 false**(全世界都非医馆格)—— fail-safe:
//     出诊启动的空间半边(「从医馆格跨出」)恒不满足,HouseCallStarted 恒零写入。
//     这是**诚实形态**:没有数据就声称有 = 发明数据(半真半假比恒假更糟)。
//   - 真源接线 = 23/24 接线轮(把真实地块集喂给 <see cref="Configure"/>;
//     该方法存在即「接线只改数据不改结构」的缝)。
//   - **同源义务的结构性承载**:本类实例由组合根创建、经服务袋(<c>CompositionRootServices
//     .ClinicCells</c>)单点发放 —— 三个消费点(写者 / 将来的 F1PipelineInput 构造 /
//     将来的规则十「无人」检测器)**必须从服务袋取同一实例**;另立实例 = 三处对同一
//     tick 给出不同回答(接口头注的同源禁令)。今日唯一消费点 = HouseCallStartedWriter;
//     组合根测试以「driver 持有的实例 == 服务袋实例」钉住单源装配。
//
// ⚠️ 空集不是「永真占位」的借口:Configure 落地前,任何「医馆内有病人 / 玩家在医馆」
//   的 playtest 表现都**不构成**本谓词已接真源的证据(禁借绿)。

using System;
using System.Collections.Generic;
using DaYiJingCheng.Sim.Contracts;

namespace DaYiJingCheng.Gameplay.Boot
{
    /// <summary>
    /// 「医馆格」判据源的生产占位 —— 空格集(fail-safe),真源归 23/24 接线轮。
    /// </summary>
    public sealed class ClinicCellSource : IClinicCellSource
    {
        private readonly HashSet<WorldPos> _cells = new HashSet<WorldPos>();

        /// <summary>
        /// 装配/接线期喂入医馆地块格集(23/24 接线轮的落点;当前生产调用方为零)。
        /// </summary>
        /// <param name="cells">医馆建筑占地的格全集(逐格;多格结构须展开成占用格)。</param>
        /// <exception cref="ArgumentNullException"><paramref name="cells"/> 为 null。</exception>
        public void Configure(IEnumerable<WorldPos> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            _cells.Clear();
            foreach (var c in cells) _cells.Add(c);
        }

        /// <summary>格 ∈ 医馆地块(当前恒 false —— 空集占位,见类头注)。</summary>
        public bool IsClinicCell(WorldPos cell) => _cells.Contains(cell);

        /// <summary>已配置的格数(测试/接线观测面 —— 0 = 占位态)。</summary>
        public int ConfiguredCount => _cells.Count;
    }
}
