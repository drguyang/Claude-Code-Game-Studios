# Story 003: F-53.2 延迟发出与 ConsequenceResolved 世界流

> **Epic**: 医疗后果与责任
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/medical-consequences.md`(F-53.2 延迟发出 · 规则六 不可归因 · 规则八 `ConsequenceResolved` 已裁直登 registry · AC-53-06 / AC-53-07 / AC-53-14)
**Requirement**: TR-medcons-001(`ConsequenceResolved` 进世界流,covered ADR-005+009) · TR-medcons-006(DelayTicks > 0 反渗形状 —— gap,P1a 注;P0 载体 = AC-53-06) · TR-medcons-007(零可变态,partial)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类(后果状态 = 模拟态进流)· ADR-024: Kind 单一登记真源(OQ-53-7 已裁:`ConsequenceResolved` 直登 `entities.yaml`,`stream: world`、`author: 53`、载荷 ∈ 整数域)· ADR-007: 掷骰/时序可重构 · ADR-010: 序列化
**ADR Decision Summary**: `EmitTick = Tick(CaseClosed) + DelayTicks`,`DelayTable[outcome] > 0` **硬约束**(构建期拒收 ≤0;具体值未裁 = OQ-53-2,归数值轮)。延迟后果**重放时重建、零独立状态**(AC-53-06:53 不持任何跨 tick 可变态 —— 待发表 = 流的纯函数)。不可归因(规则六):后果事件载荷**零 case_id / 零指向触发病例的指针**;`patient_id` 允许;AC-53-07 反渗扫描。`ConsequenceResolved` 载荷 = 整数枚举 + `patient_id`(无 case_id);**P0 零订阅者仍照发**(水龙头纪律);53 不持可变态(重放重建);疫区持续状态 = 模拟态须进世界流的义务已登记 ⇒ OQ-53-1(P1a,不在本 story)。药物禁忌/过量代价 P0 开放项(OQ-53-8):限定 伤/后遗、**永不致死** —— 本 story 交形状门,值归数值轮。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯流写 + 重放重建,门 A 内;kindgen 联动(既有 registry 条目)。

**Control Manifest Rules (this layer)**:
- Required: 写流经 `IEventAuthority` 查权 + 主机 `Append` 发 `Seq`;EmitTick 计算纯整数;载荷构造只有整数域重载(编译面锁死)
- Forbidden: 缓存「待发表」进字段(派生态不落私有态,每次现算);载荷渗 case_id / 病例指针;53 写病史流/病例流(只写世界流)
- Guardrail: 每病例至多一条后果事件(发出幂等从流可判:已发 ⇒ 不再发,判据读流非读内存)

---

## Acceptance Criteria

*From GDD `design/gdd/medical-consequences.md`, scoped to this story:*

- [ ] `EmitTick = Tick(CaseClosed) + DelayTable[outcome]` 纯整数;`DelayTable` 烘焙校验 = **每条 > 0**(≤0 构建期 `throw`;值本身留槽 OQ-53-2)
- [ ] **AC-53-06**:任意前缀重放 ⇒ 待发后果集与在线态逐位一致;53 程序集**零跨 tick 可变字段**(反射断言:无实例态承载延后)
- [ ] `ConsequenceResolved` 经 registry 路由进**世界流**:`stream: world`、`author: 53`、载荷 = `{patient_id:int, consequence_kind:枚举int, …}` **无 case_id 字段**(schema 级,ADR-024 A2 绿)
- [ ] **AC-53-07 反渗扫描**:53 全部发出载荷 + 内部类型递归反射,无 `case_id` / 病例指针 / `salted_key` 回渗(病人可指向,病例不可 —— 不可归因的机制面)
- [ ] 发出幂等从流判定:重放中已含 `ConsequenceResolved` ⇒ 不重发(判据 = 流查询,非内存标志)
- [ ] **P0 零订阅者仍发出**:无消费者时事件照写流(世界流持久);无「没人听就不发」路径
- [ ] OQ-53-8 形状门:禁忌/过量代价路径的 Outcome 限定 ∈ {伤, 后遗} 子集(类型/表级约束「永不致死」;具体是否启用 P0 开放项按数值轮)
- [ ] 跨流时序:后果 `Seq` 由主机发号;与 52 的世界级事件(Patient=-1)混流时全序无歧义(AC-53-04 联动夹具)

---

## Implementation Notes

*Derived from F-53.2 / 规则八:*

1. 「待发表现算」:每 tick 从流查询 `Tick(CaseClosed)+Delay ∈ (now−lookback, now]` 且未发 ⇒ 发;窗口/lookback 实现细节须保证**流前缀单调**(补算 CatchUp 一条不漏,挂 ADR-005 CatchUp 纪律)。
2. 发出查询实现走三流只读索引(边界层既有),53 不自建第二索引持久结构(承 AC-53-06 零可变态 + TR-016 有界性同族)。
3. `consequence_kind` 枚举值集 = GDD 回响轴三类(后遗/复现/试药史表现)+ 开放项;值映射表数据驱动,新增 = 表改动非代码改动(评审门)。
4. 载荷「零 case_id」在**构造器层**物理不可传(参数集不含),不是运行期置 null(能力面纪律,同 39 Story 003 形制)。
5. 疫区持续状态义务(OQ-53-1)与本 story 边界:P1a 另轮;本 story 不为其预留字段(预留 = 渗风险)。
6. ⚠️ **数值冻结**:`DelayTable` 各值 / lookback 窗 / 禁忌代价强度 —— 全部归用户数值轮;`>0` 与「永不致死」是机制约束已钉。

---

## Out of Scope

- [Story 002]: Outcome 判定(本 story 消费其枚举)
- [Story 004]: 回响的表现层呈现(42/44/13 载体)与「在场者先见」(规则七 OQ-53-6 预裁,走查在 004)
- F-53.3 RegionOutcome(P1a):53 面预留零(禁「不归纳即免疫」的实现走 P1a 轮)
- 31 订阅方(53 零引用纪律的反面 = 31 侧义务,归 31/37 的 P1a 线)
- 村落信任值(AC-53-11 禁,P0 零;呈现延伸全部 P1a)

---

## QA Test Cases

**[Logic story — automated test specs]:**

- **AC-1**: EmitTick 与重放一致(AC-53-06)
  - Given: 结案时刻夹具 + DelayTable 符号值表
  - When: 在线跑到 EmitTick vs 从流 CatchUp 补算
  - Then: 发出 tick 与载荷逐位一致;漏发 = 0(跨日/跨窗边界样)
  - Edge cases: Delay 恰跨存档点(7a 读档后补算照发不重发)

- **AC-2**: DelayTable 构建期 >0 拒收
  - Given: 某 outcome 值 = 0 / 负 的违规表夹具
  - Then: 烘焙 `throw`;合规边界 min=1 绿
  - Edge cases: 表缺 outcome 行 = 也红(全覆盖校验联动 Story 002 表)

- **AC-3**: 载荷零 case_id(AC-53-07)
  - Given: `ConsequenceResolved` 构造器 + 全部内部类型
  - When: 反射递归扫 case_id/指针/salted_key 词族
  - Then: 零命中;patient_id 合法
  - Edge cases: 「同一病例重开(改写)」不产生第二条指向性事件(幂等从流判据)

- **AC-4**: 零订阅仍发 + 全序混流
  - Given: 无消费者环境 + 同 tick 有 52 世界级事件
  - Then: 流含 `ConsequenceResolved`;全序键排序无并列歧义(52 的 -1 与 53 的 pid 混排稳定)
  - Edge cases: 读流重建世界态:疫区/病人状态含后果行(持久非派生缓存,AC-53-14)

- **AC-5**: 永不致死门(OQ-53-8 形状)
  - Given: 禁忌代价表行
  - Then: 代价映射 ∈ {伤, 后遗};「致死」行在 P0 表 = 构建期拒收
  - Edge cases: 9 判「走」+ 后果并存 ⇒ 无因果混写(结局轴只读)

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/MedConsequences/medcons_delayed_emit_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002(Outcome 枚举)· Story 001(全序聚合)· registry `ConsequenceResolved` 条目(已直登,OQ-53-7 裁定)
- Unlocks: Story 004(呈现消费世界流)· 6/31 的 P1a 订阅线(流已备数据)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
