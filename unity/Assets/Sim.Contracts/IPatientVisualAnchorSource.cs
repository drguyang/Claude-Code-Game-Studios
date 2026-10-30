// 权威来源:
//   ADR-016 §一(三源不变量:决策可重建的充要条件是全部输入 ∈ {事件流, 版本化烘焙数据, 二者纯函数})
//   ADR-016 §三(感知输入 = 粗粒度整数格;禁读表现态位置)
//   ADR-015 §三(`WorldPos` = (i32 x, i32 y, i32 z) 单一整数格 —— 地形 / 建造 / 掉落 / 导航格共用)
//   ADR-013 §9 C3(呈现层只渲染,永不持有游戏状态)
//   `production/milestones/p0-sweep-2026-10-29.md:308`(病人灰盒 proxy 的裁定)
//
// 🔁 **2026-10-30 新建(`TD-prescription-06` 的「灰盒做好,等真实资产兑换」轮)**:
//   把「病人该站在哪」从 `PatientVisualSpawner` 里**抽成接缝**。此前它是**写死**在 spawner 内的
//   `SpawnAnchorFor`(围绕玩家的环,半径 2.5 m),而真实位置归 **6 世界生态区 + 13 病人 AI 空间侧**
//   (`LogiPose` / `HomeRegion`)。写死的后果不是「现在不好用」,是**真位置来了要改三处**:
//   spawner 本体 + `InteractionInputDriver.NearestPresentPatient`(它消费视觉位置)+ 夹具。
//
// ⚠️ **为什么住 `Sim.Contracts` 而不是表现层**:
//   ① 本接口**只用 BCL 类型**(`WorldPos` / `int`),零 `UnityEngine` 依赖 —— 与
//      `Sim.Contracts` 的「引用集恰 = BCL」纪律相容(ADR-025 §① / b2 门);
//   ② 真实实现方(**6 世界生态区 + 13 空间侧**)住 sim 侧 / 边界层,它们**够得着** `Sim.Contracts`
//      但**够不着** `Gameplay.Presentation` —— 接口住契约面,实现才可能由它们提供;
//   ③ 若住表现层,真位置实现就得由表现层反向依赖 sim 侧空间数据 = 破 ADR-016 §一。
//
// ⚠️ **本接口返回整数格,不返回连续坐标**:
//   承 ADR-015 §三 —— 空间真值一律整数格;格 → 连续坐标的换算(含 `y` 偏移 / 落地吸附)
//   是**表现层**的事(归 `PatientVisualSpawner`),不进契约面。这样真位置与占位环
//   **共用同一条「格 → 世界坐标」换算**,兑换时只有锚点来源变。

using System;

namespace DaYiJingCheng.Sim.Contracts
{
    /// <summary>病人视觉实体的**锚点来源** —— 「这个病人该站在哪一格」的唯一合法入口。
    /// <para>**只读、纯函数、可重放**:同一 `patientId` 在同一布局 / 同 tick 下恒返回同一格
    /// (ADR-016 §一 三源不变量)。实现**不得**读墙钟 / `UnityEngine.Random` / 未版本化场景几何。</para>
    /// <para>⚠️ **这不是游戏状态**:锚点是派生态(ADR-009 §一),不进三流、不存档、重建期重建。</para>
    /// <para>⚠️ **禁在本接口内做判定**:它只回答「在哪」,不回答「可不可交互 / 该不该渲染」
    /// —— 后者归 10 急救动作 / 42 呈现层各自的所有权。</para>
    /// </summary>
    public interface IPatientVisualAnchorSource
    {
        /// <summary>取该病人当前应站的整数格。
        /// <para>⚠️ 返回 <see cref="WorldPos"/> 的 <b>nullable</b> 形态(用 <c>bool</c> + <c>out</c>,
        /// 不是 `WorldPos?`)—— 因为 `WorldPos` 的零值 `(0,0,0)` 是**合法格**,
        /// 不得拿它当「无锚点」的哨兵(与 `ClinicRoomId.None = (0,0)` 同族的坑)。</para>
        /// </summary>
        /// <param name="patientId">病人 id(跨权威稳定,ADR-006 Amendment B)。</param>
        /// <param name="cell">该病人应站的整数格(ADR-015 §三;调用方负责格 → 连续坐标换算)。</param>
        /// <returns>有锚点 = true;该病人此刻无锚点(未分配 / 未进入世界)= false。</returns>
        bool TryGetAnchorCell(int patientId, out WorldPos cell);
    }
}
