# Story 005: 复活落点与传送命令(医馆床格 → TeleportTo)

> **Epic**: 死亡与复活
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
## Context

**GDD**: `design/gdd/death-and-respawn.md`(规则五 复活:revive_cell = 医馆床类家具格 · 兜底世界出生点 · 29 调 1 的 TeleportTo · 不重置疾病/世界 · OQ-29-2 已裁)
**Requirement**: TR-death-006(复活传送走 ITeleportCommandSink 整数命令半 —— 全案唯一跨门调用点) · TR-death-003(落点 = sim 整数格)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-025(主): 契约程序集清单;ADR-020: 玩家控制器
**ADR Decision Summary**: 传送契约拆两半(QQ-01 = ①′):整数命令 `ITeleportCommandSink.RequestTeleport(int actorId, WorldPos cell)` 住 `Sim.Contracts`,`Vector3` 连续半(实际位移/过场)留 `Gameplay.Presentation`;**29 调 1,由 1 发 `ActorCellEntered` —— 29 的 Append 计数 = 0**(AC-29-09);EC 行:医馆未建/无床 ⇒ 退世界出生点(ADR-015 固定格);联机共床 ⇒ 同格可复用,冲突由主机串行定序;移动禁用 = 1 读 PlayerDied(29 不在 MotorSuppressed 白名单);复活**不重置**疾病/世界状态。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 命令契约与落点选择纯整数;连续位移是 1/表现层既有件(ADR-020)。

**Control Manifest Rules (this layer)**:
- Required: 落点 = 整数格(床格选定 or 出生点);命令单向 29→1;过场位移零写流
- Forbidden: 29 直接 Append `ActorCellEntered` 或任何位置事件;29 直改玩家连续坐标;复活附带清病/清世界副作用
- Guardrail: ⚠️ **床类家具格的确定性选定规则 = 24 下一轮欠账**(GDD 明写「残留 = 24 侧选定规则」)—— 本 story 以「接口给格」桩先行,联调前 AC-29-09/10 完整绿维持 BLOCKED-BY-9 + BLOCKED-BY-24 标注;传送率上界与 story-004 合验

---

## Acceptance Criteria

*From GDD `design/gdd/death-and-respawn.md`, scoped to this story:*

- [ ] AC-29-09 [A]:检索 29 代码路径,对 `ActorCellEntered` 的 Append 调用点数 = 0;过场位移零写流(ADR-020 §四)
- [ ] AC-29-10 [A] ⚠️ BLOCKED-BY-9:复活来源 `ActorCellEntered` 条数 ≤ `玩家数 / DEATH_COOLDOWN`(有界性半边,谓词源 story-004)
- [ ] 落点优先级:有医馆房间(24)且有床类家具格 → 选定格;否则世界出生点(两分支 fixture 全覆盖;选定规则本体待 24 回写,现以注入点隔离)
- [ ] 命令形状:`RequestTeleport(actorId, cell)` 全整数(WorldPos);连续半不在 Sim.Contracts(引用面断言:命令契约零 Vector3)
- [ ] 副作用负面清单:复活后疾病/世界流状态不残不越(病人病程、掉落物原样在 death_cell —— 与 story-002 回捡动线对拍)
- [ ] 联机共床:两玩家同床格复活 → 同格落点、两条 ActorCellEntered(由 1 发)、无死锁(主机串行定序)

---

## Implementation Notes

*Derived from ADR-025/020 Implementation Guidelines:*

1. 复活编排位置 = 死亡结算段末步(story-001 管线):PlayerDied → 掉落 → 掉级 → (冷却允许下)复活命令;呈现过渡(story-006)不阻塞 sim 侧命令。
2. 床格查询接口:`IClinicReviveCell.TryGet(actor) → WorldPos?`(24/房间数据侧提供;⚠️ 选定规则未落地 ⇒ 接口先行 + BLOCKED-BY-24 标注,禁把「取第一个床格」当裁决实现)。
3. 1 侧消费:`ITeleportCommandSink` 实现(1 的 epic)发 `ActorCellEntered`,29 只发命令 —— 集成测试用 1 的既有管线或其桩,桩面明确标注。
4. 出生点 = ADR-015 逻辑层固定格(烘焙数据,非运行期发明)。
5. 主机串行:多复活命令同 tick 时按 actor id 定序处理(确定性,禁到达序)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 002]: 掉落物留存与回捡(复活不清掉落)
- [Story 004]: 冷却门(命令发射的前置)
- [Story 006]: 醒来过渡呈现
- 系统 1:Teleport 实现与 ActorCellEntered 铸造;系统 24:床格选定规则(欠账在其轮次)

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(零写流)**: Append 检索 + 行为对拍
  - Given: 完整复活流程跑通
  - When: 检索 29 的 Append 点;比对事件流
  - Then: 29 的 ActorCellEntered 调用数 = 0;流中的该类事件全部由 1 管线产生
  - Edge cases: 过场动画期间(表现层)不产 sim 事件
- **AC-2(落点两分支)**: 有床/无床
  - Given: fixture A = 医馆含床;fixture B = 未建医馆
  - When: 复活
  - Then: A 落床格(注入点返回值);B 落出生点固定格
  - Edge cases: 多床格时以「24 选定接口」返回值为准 —— 本 story 不自拍规则(BLOCKED-BY-24)
- **AC-3(共床串行)**: 联机双复活
  - Given: 两玩家同 tick 复活,同床格
  - When: 主机处理
  - Then: 两条 ActorCellEntered(id 序定序),同格合法,无死锁
  - Edge cases: 一玩家冷却内(不复活)+ 一玩家复活 → 仅一条

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DeathRespawn/death_respawn_teleport_test.cs` — must exist and pass(桩半边);AC-29-09 绿可记(纯检索断言),AC-29-10 维持 BLOCKED-BY-9,床格联调维持 BLOCKED-BY-24
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 + 004;系统 1 的命令消费(未就绪以桩);系统 24 床格接口(⚠️ 选定规则未裁)
- Unlocks: 「死亡→回程→回捡」端到端集成(与 story-002 合);42/44 的过渡联测(经 story-006)

---

## Completion Notes

*(留空 — story 关闭时回填)*
