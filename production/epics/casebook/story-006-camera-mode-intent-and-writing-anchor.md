# Story 006: 相机档位意图与落笔锚点钩子

> **Epic**: 脉案
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/casebook.md`(规则五 摊开与相机 · 规则六 落笔与存档 · 边界表「39 = 两档相机意图唯一请求方」· 联机落笔 = 判定输入)
**Requirement**: TR-casebook-007(脉案近景经 `ICameraRig.SetMode`,不新增相机状态持有者) · TR-casebook-004(联机落笔 `Seq` 由主机发号 —— **gap**,随 ADR-001 窄修订 + 45 GDD 轮,P1b 前;本 story 运行面 BLOCKED-BY-45,禁借绿)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-020(主): 玩家控制器与相机 · ADR-010: 持久化(锚点 ① 脉案落笔触发 checkpoint) · ADR-001 §一之三 裁决一: 判定输入类走可靠通道 · ADR-013: 模态开集(`ModalId.Casebook`)
**ADR Decision Summary**: `OpenCasebook ⇒ ICameraRig.SetMode(Casebook)`、`Close ⇒ Explore`,39 是该两档意图的**唯一请求方**;相机模式是本地表现态,**永不上传**(不进三流、不进网络)。落笔 = 7a checkpoint 锚点 ①(ADR-010 §六);P0 单机 = 判断意图本地直写 `IEventSink`(主机 = 自己),`Seq` 由 Append 时主机发号;联机形态(客户端聚合意图上行)被 45 轮阻塞,**只立接口形状不实现**。移动不抑制已裁定(OQ-39-4 C 路:摊开册子不禁移动 —— 本 story 验证「相机换档不改移动管线」)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: `ICameraRig` = 自建机位(ADR-020,零 Cinemachine);PlayMode 测试驱动相机意图即可,无 post-cutoff API 承重。

**Control Manifest Rules (this layer)**:
- Required: 意图请求方唯一(39);SetMode 幂等(同档重复请求 = no-op,AC-39-07);落笔写流走 `JudgmentRecorded`(author 轴必填);checkpoint 触发只经 7a 的锚点接口
- Forbidden: 39 持有相机状态 / 直接操作 `Camera` transform;相机模式进网络包;开册强制禁移动(违反 OQ-39-4 裁定)
- Guardrail: 每次开/合册的档位转换次数 = 1(转换计数断言);P0 无「半开册」中间档

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [ ] **AC-39-07**:开册 → `SetMode(Casebook)`、合册 → `SetMode(Explore)`;重复请求同档幂等;一次开合全程档位转换恰 1+1 次(转换计数)
- [ ] 39 是两档意图唯一请求方:代码所有权断言 —— `SetMode(Casebook/Explore)` 的调用点仅存在于 39(静态扫描调用点白名单)
- [ ] 落笔提交 = 一条 `JudgmentRecorded`(病例流,`author_player_id` 必填)经 `IEventSink.Append`,单机 P0 路径本地直写;写成功后同帧内触发 7a 锚点 ① checkpoint
- [ ] 相机模式字段**不出现在**任何网络序列化类型(反射扫描;P0 联机面 = 不传)
- [ ] 联机落笔形态:意图上行接口**形状就位**(客户端聚合 → 主机 Judge/Append/发 Seq,承 ADR-011 Amend. B 同构),运行面 BLOCKED-BY-45(45 无 GDD),禁借绿
- [ ] 摊开脉案期间玩家移动管线不变(角色可走动;OQ-39-4 C 路回归验证)
- [ ] 合册(未落笔离开)= 零副作用:不写流、不触发 checkpoint、痕不变

---

## Implementation Notes

*Derived from ADR-020 / ADR-010 Implementation Guidelines:*

1. 39 侧仅持「请求」:`ICameraRig.RequestMode(CameraMode.Casebook)`;机位实现(2)监听并执行过渡;过渡时长 = 调参旋钮,值归用户数值轮。
2. 落笔路径:UI 提交(Story 005 的落笔区)→ 边界层意图构造(整数词表 id + 病例 id)→ `Append` → 回执驱动投影刷新(Story 003)。39 不自造 `Seq`(发号 = 主机 Append 时)。
3. checkpoint 触发:7a 暴露锚点枚举 `SaveAnchor.CasebookWritten`(ADR-010 §六 三锚点之一);39 只报锚,不碰写盘线程 / 不做 flush(义务归 7a)。
4. 转换计数:测试态用装饰器包 `ICameraRig` 记录 `SetMode` 序列;幂等 = 同档连请求只产生一次实际转换。
5. 联机形状(不实现):`IJudgmentIntentChannel.Submit(intent)` 接口进 `Sim.Contracts` 风格的只契约件,实现体标 `BLOCKED-BY-45`;45 轮后接 ADR-001 可靠通道。
6. 合册无副作用断言:夹具对比合册前后的事件流字节 + 存档段长度。

---

## Out of Scope

- [Story 003]: 落笔后 `J(c,p)` / 痕投影的折叠逻辑(本 story 只触发写与刷)
- [Story 005]: 落笔区 UI 与焦点路径
- 45 网络层实现与 `Seq` 上行的真实联机(另 epic;TR-casebook-004 的 closure 在 45 轮)
- 出诊启动锚点 ③ / 病例结案锚点 ② 的触发方(归 1 / 37 的 epic;39 只认领锚点 ①)

---

## QA Test Cases

**[Integration story — PlayMode test specs]:**

- **AC-1**: 开合册相机意图幂等与计数(AC-39-07)
  - Given: PlayMode 夹具,装饰器计数 `SetMode`
  - When: 开册 → 再开册(边界重复)→ 合册 → 再合册
  - Then: 转换序列 = Explore→Casebook→Explore,计数 2;重复请求零转换
  - Edge cases: 快速开合(动画未落)不产生第三档

- **AC-2**: 落笔写流 + 锚点触发
  - Given: 干净病例流
  - When: UI 提交一笔判断
  - Then: 病例流末尾 +1 `JudgmentRecorded`(author == 本玩家 id);7a 收到 `CasebookWritten` 锚点恰 1 次;重开存档该笔在
  - Edge cases: 同内容重交 = 两条合法事件(39 去重不是义务 —— 幂等归判定链语义);写失败(磁盘满)⇒ UI 报错不吞笔

- **AC-3**: 相机模式零上传
  - Given: 序列化类型反射面
  - When: 扫描网络 / 存档载荷类型
  - Then: 无 camera mode 字段
  - Edge cases: 表现态 DTO(42 侧)可读模式,但非可序列化上行件

- **AC-4**: 合册零副作用
  - Given: 开册读了三页未落笔
  - When: 合册
  - Then: 事件流字节不变、存档段不变、痕投影不变;玩家位置速度不变(移动未抑制)
  - Edge cases: 摊开期间跨格移动(世界流 `ActorCellEntered` 照常,不因册拒发)

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/PlayMode/Casebook/casebook_camera_pen_hook_test.cs` — must exist and pass;联机子项标记 BLOCKED-BY-45
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(分册段,落笔后页码态入档)· Story 003(投影刷新)· Story 005(提交 UI)· 2 `ICameraRig`(并行)· 7a 锚点接口(既有)
- Unlocks: Epic 收口 · 45 轮回写 TR-casebook-004 后的联机落笔 story(新号待续)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
