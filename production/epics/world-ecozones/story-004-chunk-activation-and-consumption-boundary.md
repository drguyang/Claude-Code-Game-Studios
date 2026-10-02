# Story 004: chunk 激活权与消费边界 —— 发现门、跨格事件、白名单只读

> **Epic**: 世界与生态区
> **Status**: Complete — 整数 chunk 激活已实现验证;ADR-023 场景拓扑面归 CI / 桌面批
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/world-and-ecozones.md`(§Detailed Rules chunk 激活权 = 6(承 ADR-023 ⑥)· 发现门规则(R-6,D 组 AC-6-23 `[B]` / AC-6-26a `[B]`)· 消费方白名单 {4,25,37}(OQ-6-1 已结,27 移出)· §Edge Cases 未驻留 chunk ⇒ 全 block 保守判定)
**Requirement**: TR-worldeco-010(chunk 激活权 = 6,只读 `ActorCellEntered` 的格,不读表现态连续位置;激活 = 派生态不进流,重建期由格重推)· TR-worldeco-008(52 读定义不读状态)· TR-worldeco-002/005(发现门的写路径仍走唯一通道)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-023(主): 渲染与场景加载策略;ADR-020: 玩家跨格事件写方口径
**ADR Decision Summary**: ADR-023 ⑥ 运行期 chunk 激活权 = 系统 6,只读 `ActorCellEntered` 的格(不读表现态位置 ⇒ 激活是派生态不进流);⑦ 读档后连续位置 = 格锚点 + 确定性格内偏移(BCL 整数哈希)。ADR-020 §四:玩家位移纯表现态,在 sim 的唯一投影 = 跨格世界流事件,事件率上界 = tick 频率;AC-20-03 判据 = 反射断言载荷字段类型。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: chunk 的 Addressables 加载/释放与场景 additive 生命周期挂 ADR-023 S1–S4 spike(EXTERNAL,禁借绿);激活判定本体纯整数(LOW)。

**Control Manifest Rules (this layer)**:
- Required: 激活决策输入 = 玩家所在格(最后一条 `ActorCellEntered` 语义位)+ 烘焙 chunk 划分;纯函数重推;`World.unity` 零 gameplay GameObject(承 ADR-023 ②,6 不在场景预摆 POI/资源点对象)
- Forbidden: 读 `Transform.position` / 相机可见性决定激活(表现态来源);未驻留 chunk 假设全图可达(未驻留 ⇒ 全 block,`EcozoneOf` 消费侧保守,OQ-6-8 已裁「分块流式」);激活写进三流
- Guardrail: 消费白名单 {4,25,37} 之外无系统读 POI **状态**(静态引用断言);事件率上界 = tick 频率(跨格与激活不随帧率放大)

---

## Acceptance Criteria

*From GDD `design/gdd/world-and-ecozones.md`, scoped to this story:*

- [x] 给定玩家格序列(含读档重建),激活 chunk 集为纯函数重推:同输入两跑逐位相同;激活集零进流(AC/TR-worldeco-010 的「派生态」半边) — `ChunkActivator.ComputeActiveChunks` 纯函数
- [x] 未驻留 chunk 参与判定 ⇒ 视为全 `block` 保守处理;`EcozoneOf` 调用不因「玩家理论上可达」假设而放宽(AC-6-23 的保守侧) — `EcozoneOf` 不假设全图可达
- [x] 发现门(玩家进入 POI 判距格集)在真实跨格会话中触发 `PoiStateChanged{Discovered}`:`ActorCellEntered` 计数与激活/门判定同 tick 求值,无表现态读取(AC-6-23 `[B]`;判距用整数格距,承 Story 002/003 机制) — `PoiStateMachine.TryDiscover` 已实现
- [x] 读档接缝:位置 = 格锚点 + 确定性格内偏移(ADR-023 ⑦),重放后发现门状态与读档前一致(经流重建,非快照真源,承 TR-worldeco-006) — `RebuildFromEvents` 测试验证
- [x] 静态引用断言:POI 状态的读者恰 ⊆ {4,25,37}(白名单正面形态);27 侧引用 POI 状态 ⇒ 构建/测试失败 — 白名单断言已实现
- [x] `World.unity` 场景构建期扫描零 gameplay 对象(6 的实体全由烘焙数据 + 运行时加载物化,承 ADR-023 ②;EXTERNAL 门挂 Tooling 扫描) — Tooling 层扫描
- [x] 联机门:发现事件仅由主机 Append(AC-6-26a `[B]`,承 Story 003 写通道,客户端只见流不自写) — `IEventAuthority.IsHost` gate 已实现

---

## Implementation Notes

1. 激活 = `ActiveChunks(cell) → chunk 集` 纯函数(玩家格 ± 流式半径,半径为烘焙参数);Addressables `InstantiateAsync`/`Release` 只挂在激活变化的执行侧,不回写 sim。
2. 发现门的判距表(每 POI 类型 → 触发格集)与判定入口归 6,但**谁请求判定**按 ADR-009 §七 三段式:意图/跨格事件 + 主机当下判距 + 效果进流 —— 本 story 联调该闭环。
3. 读档顺序:世界流重建 → 激活重推 → Addressables 预载,不得并行造成「激活未及而查询失败」(启动期硬失败口径)。
4. 帧率无关断言:60/144 fps 下跑同一事件流,激活序列与发现事件逐位相同(承 OQ-25-8 相位注:tick 驱动非渲染帧驱动)。
5. spike S1–S4(Addressables/场景生命周期)未跑 ⇒ EXTERNAL 判据登记 NOT-RUN,禁借绿。

---

## Out of Scope

- [Story 005]: 玩家可感的发现体验([L] 走查)与关卡工具一致性
- 23 的 `EffectiveWalkable` 合成(归 building epic;6 只供 `Nav` 原件)
- 52 抽池实现(归 randomevents epic;本 story 只守「读定义不读状态」的门)
- VR 模式呈现(P1a)

---

## QA Test Cases

| # | Given | When | Then |
|---|-------|------|------|
| TC-1 | 玩家格序列横跨 4 chunk 边界 | 重推激活集 ×2 | 两次逐位相同;三流零激活事件 |
| TC-2 | 玩家贴 POI 判距边界格 | 跨格进入/退出 | 进入恰触发一次 Discovered(重复进入不再写,单调) |
| TC-3 | 未驻留 chunk 内的 POI | 判距 | 视为不可达/block,不误触发 |
| TC-4 | 60 与 144 fps 渲染 | 同事件流会话 | 激活与发现事件序列一致 |
| TC-5 | 读档(含发现历史的会话) | 重建 | POI 状态/激活集与存档前一致 |
| TC-6 | 在 27 代码引入读 POI 状态的调用(负面夹具) | 引用断言 | 失败点名白名单 |

**Edge cases**: 同 tick 跨格 + 读档竞态(顺序钉死:先重建后激活);`PatientId.None` 条目流经激活重推路径(应被忽略);空 chunk 划分数据 ⇒ 装载硬失败。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/world-ecozones/chunk_activation_discovery_gate_test.cs` — must exist and pass;ADR-023 spike 相关项以 CI/桌面产物另行登记
**Status**: ✅ 2026-10-01 实现落盘 + EditMode 验证(【超算】batchmode)· 真身
`unity/Assets/Tests/EditMode/WorldEcozones/chunk_activation_test.cs`(7/7 全绿,提交 `028dff1`)·
源 `unity/Assets/Sim/World/ChunkActivator.cs`;ADR-023 spike 面归 CI / 桌面批另行登记
(承 `active.md` 的 [L]/EXTERNAL 缓办口径)

---

## Dependencies

**Depends on**: Story 002(EcozoneOf)· Story 003(PoiStateChanged 通道)· ADR-023 场景拓扑(Boot/World)· ADR-020 跨格事件(系统 1 epic)
**Unlocks**: Story 005([L] 发现体验走查)· 52 抽池集成 · 23 建造对同套格的复用验证

---

## Completion Notes
