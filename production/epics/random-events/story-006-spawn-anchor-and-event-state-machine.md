# Story 006: spawn_anchor 解析与事件状态机

> **Epic**: 随机事件导演
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 5h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/random-events.md`(DC-4 生成与锚点 · 规则七 六态状态机 · AC-52-11/22/40/41世界侧/42/46;出诊启动锚点登记义务 · 「52 处已加 note」ADR-014 §六 调和)
**Requirement**: TR-randomevents-031(spawn_anchor 确定性解析,covered ADR-015) · TR-randomevents-012(tick 契约联动) · TR-randomevents-017(五边界剩余支路) · TR-randomevents-042侧(枚举校验联动 Story 001;`SETTLEMENT`/`BIOME_REGION` = P1a 拒收) · TR-randomevents-020(注入形状的运行侧落点,gap no_adr)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-015(主): 世界几何 · 单一整数格 · 手工烘焙固定世界 · ADR-009: 世界流 · ADR-010: 锚点 ③ 出诊启动(52 的 DC-4 表欠 7a 的具名世界事件登记义务)
**ADR Decision Summary**: 锚点解析 = **烘焙逻辑层整数查询**(非 NavMesh / 非视觉层采样,ADR-015 §一 两层纪律):`CLINIC_FRONT` = 医馆地块门前锚点(单点查表);`TRAVEL_PATH` = 出诊路径**前方偏移点**(`SPAWN_AHEAD_DIST > 0` 固定向量沿格路径,非碰撞查询);`GATHER_POINT` = 17 采集点集按 key 排序取 `S mod count`。生成落**世界流 / 病史流按 Kind 路由**(`EventArrived` 已载 `spawn_anchor`)。**六态状态机**:待触发→预告→降临→避险→结算→结束(Story 005 覆盖预告/避险支路,本 story 出全表 + 结算/结束)。结算 = 效果进流、其余本地;**不逐帧轮询**(AC-52-40,tick 事件驱动)。52 对 7a 欠一项登记:**「出诊启动」具名世界事件**(7a 锚点 ③ 的载体,DC-4 表)—— 本 story 交付该 Kind 的 registry 增列义务单(经 `entities.yaml` + kindgen,ADR-024 通道),实体归 1/7a 轮回写。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 查表与状态机 = 门 A 内纯整数;表现层实例化(敌人 / VFX)经 DTO → 27/42 既有通道,52 零 GameObject。

**Control Manifest Rules (this layer)**:
- Required: 解析只用 `WorldPos` 整数格(`Sim.Contracts`);状态迁移 tick 驱动;结算效果写流、其余本地
- Forbidden: 采样视觉层(Terrain / NavMesh / `Vector3`);协程 / Update 轮询状态机;52 直接生成敌人实体(交 27)
- Guardrail: 迁移表闭集(六态 × 事件 ⇒ 无未定义迁移,非法迁移 = 断言失败);`S mod count` 与 CDF 用同一 SplitMix64 流位(序不可换)

---

## Acceptance Criteria

*From GDD `design/gdd/random-events.md`, scoped to this story:*

- [ ] 三锚点解析器各自确定性:同一 (win, seed, 输入) 重放逐位同格输出;`GATHER_POINT` 取点 = 17 点集 key 升序 + `S mod 17`(AC-52-22 坐标不是输入的反向验证:解析不读生态等级)
- [ ] `TRAVEL_PATH` 的 `SPAWN_AHEAD_DIST` 固定向量沿**格路径**前推(整数步,非射线 / 非 NavMesh);出诊目标缺失时回退 `CLINIC_FRONT`(GDD 回退规则,不静默丢事件)
- [ ] 六态迁移表完整实现且**闭集**:任何未定义 (state, trigger) 对 ⇒ 断言失败(非静默停留);结算态 = 效果写流 + 其余转结束(本地清)
- [ ] 事件驱动零轮询(AC-52-40):状态机只在 tick 回调 + 流事件回灌处被推;帧循环符号扫描零命中(静态)
- [ ] 表现交接:降临 → `EventCueDto`(整数 anchor 格)→ 27 生成遭遇 / 42 呈现;52 程序集零 `GameObject` / `Vector3`(反射断言,门 A + AC-20-03 同族)
- [ ] 「出诊启动」Kind 登记义务单交付:`entities.yaml` 增列(stream/author/payload 三件齐,载荷 ∈ 整数域)挂 1/7a 回写轮(本 story 不代裁载荷字段语义)
- [ ] 主机迁移后在途事件续跑(AC-52-46 的状态机维度):任意态(含预告中/defer 后)迁移重放续算,无孤儿态
- [ ] AC-52-41 世界侧:锚点数据源 = `world_*.cooked`(ADR-022 导出契约),数据缺锚点 = 烘焙期红联动(Story 001 拒收表)

---

## Implementation Notes

*Derived from DC-4 / 规则七 / ADR-015:*

1. 解析器签名 `(anchor_enum, ctx:int) → WorldPos` 全整数;点集来自 `world_resources.cooked` / `world_poi.cooked`(6 的烘焙物,只读)。
2. 状态机 = 显式迁移表(`(state, trigger) → state` 字典数据化,禁散点 if);每 tick 一次推进(`ITickProvider`,承 tick 相位义务「每 tick 恰好一次 Step,不由渲染帧驱动」—— 20 Hz 裁定注)。
3. 「出诊启动」义务单 = 文档化 PR 片段(entities.yaml diff)交回写,不自行合并(ADR-024 追加通道已退役 —— 走 registry 直登由**该事件作者系统**执行,52 只登记需求;此边界写进义务单)。
4. 结算写流内容按 GDD 规则七结算表逐 Kind 核对(P0 面:损伤/重建 = P1a 不在表内;医馆不可损毁是 001 的构建期事实)。
5. ⚠️ **数值冻结**:`SPAWN_AHEAD_DIST` 值 / 各态驻留 tick 数归用户数值轮;机制方向(>0、拒收 ≤0)已冻结。

---

## Out of Scope

- [Story 005]: 预告/避险/线索侧(本 story 收状态机全表,不重做支路夹具)
- 27 的敌人生成与 AI(AI epic)· 42 的呈现(42 epic)
- 「出诊启动」事件的生产端实现(1 出诊系统;本 story 只交 registry 义务单)
- F3 结算损伤(P1a)
- chunk 流式激活(6 运行期,ADR-023 ⑥)

---

## QA Test Cases

**[Integration story — PlayMode + 重放夹具]:**

- **AC-1**: 锚点确定性
  - Given: 固定 WorldSeed,三类锚点各 50 抽取夹具
  - Then: 重放逐位同格;`GATHER_POINT` 分布 = mod 语义(手工对照表);解析输出 ∈ 合法格集(无空气格)
  - Edge cases: 出诊目标缺失 ⇒ 回退 CLINIC_FRONT;`SPAWN_AHEAD_DIST` 超路径长 ⇒ 路径末点钉死规则

- **AC-2**: 六态闭集迁移表
  - Given: state × trigger 全笛卡尔积喂入
  - Then: 表内对 = 迁移成功;表外对 = 断言失败(可复现负夹具)
  - Edge cases: 同 tick 双触发(全序键定先后);结算态收到新预告 ⇒ 非法(断言)

- **AC-3**: 零轮询 + 零引擎符号(AC-52-40)
  - Given: 52 程序集 + Profiler 桩
  - When: 静态扫描 Update/协程/`Time.`;PlayMode 跑 10 游戏日
  - Then: 零命中;推进次数 = tick 数(不多不少)
  - Edge cases: 渲染帧率抖动(测试端降帧)不改变推进次数

- **AC-4**: 迁移续跑在途事件(AC-52-46 状态机维度)
  - Given: 事件处于预告中 + 另例处于 defer 槽
  - When: 模拟换主机重放续跑
  - Then: 态序与对照单机一致;无孤儿(每事件终态 = 结束)
  - Edge cases: 恰在结算 tick 迁移

- **AC-5**: 出诊启动义务单
  - Given: entities.yaml diff 草稿
  - When: kindgen 试跑(本地断言 A1–A5)
  - Then: 草案过生成器语法(正式合并待 1/7a 轮作者签名)
  - Edge cases: 载荷若误含浮点/字符串 ⇒ A2 红(义务单注明)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/PlayMode/RandomEvents/event_anchor_statemachine_test.cs` + `docs/architecture/`(出诊启动义务单随 story 证据落 `production/qa/evidence/random-events/story-006-outbreak-anchor-registry-memo.md`) — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(anchor 枚举校验)· Story 002(种子流)· Story 003(选中)· Story 005(预告/避险支路合表)· 6 烘焙世界数据(并行,ADR-022 导出)
- Unlocks: Epic 收口(单机 P0 面)· 37 玩测承重门的数据侧(事件量与分布可跑)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
