# Story 003: F-53.2 延迟发出与 ConsequenceResolved 世界流

> **Epic**: 医疗后果与责任
> **Status**: Complete
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-10-15
> **Last Updated**: 2026-10-16

## Context

**GDD**: `design/gdd/medical-consequences.md`(F-53.2 延迟发出 · 规则六 不可归因 · 规则八 `ConsequenceResolved` 已裁直登 registry · AC-53-06 / AC-53-07 / AC-53-14)
**Requirement**: TR-medcons-001(`ConsequenceResolved` 进世界流,covered ADR-005+009) · TR-medcons-006(DelayTicks > 0 反渗形状 —— gap,P1a 注;P0 载体 = AC-53-06) · TR-medcons-007(零可变态,partial)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-009(主): 三态分类(后果状态 = 模拟态进流)· ADR-024: Kind 单一登记真源(OQ-53-7 已裁:`ConsequenceResolved` 直登 `entities.yaml`,`stream: world`、`author: 53`、载荷 ∈ 整数域)· ADR-007: 掷骰/时序可重构 · ADR-010: 序列化
**ADR Decision Summary**: `EmitTick = Tick(CaseClosed) + DelayTicks`,`DelayTable[outcome] > 0` **硬约束**(构建期拒收 ≤0;✅ 2026-10-07 已裁 = `Residual 600` / `Recurrence 1200` / `TrialHistory 2400`,定义域 = **回响轴三分立值**;结局轴 `PatientLives`/`PatientLoses` **不入表** —— 由 9 即时结算、53 只读)。延迟后果**重放时重建、零独立状态**(AC-53-06:53 不持任何跨 tick 可变态 —— 待发表 = 流的纯函数)。不可归因(规则六):后果事件载荷**零 case_id / 零指向触发病例的指针**;`patient_id` 允许;AC-53-07 反渗扫描。`ConsequenceResolved` 载荷 = 整数枚举 + `patient_id`(无 case_id);**P0 零订阅者仍照发**(水龙头纪律);53 不持可变态(重放重建);疫区持续状态 = 模拟态须进世界流的义务已登记 ⇒ OQ-53-1(P1a,不在本 story)。药物禁忌/过量代价 P0 开放项(OQ-53-8):限定 伤/后遗、**永不致死** —— 本 story 交形状门,值归数值轮。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯流写 + 重放重建,门 A 内;kindgen 联动(既有 registry 条目)。

**Control Manifest Rules (this layer)**:
- Required: 写流经 `IEventAuthority` 查权 + 主机 `Append` 发 `Seq`;EmitTick 计算纯整数;载荷构造只有整数域重载(编译面锁死)
- Forbidden: 缓存「待发表」进字段(派生态不落私有态,每次现算);载荷渗 case_id / 病例指针;53 写病史流/病例流(只写世界流)
- Guardrail: 每病例至多一条后果事件(发出幂等从流可判:已发 ⇒ 不再发,判据读流非读内存)

---

## Acceptance Criteria

*From GDD `design/gdd/medical-consequences.md`, scoped to this story:*

- [ ] `EmitTick = Tick(CaseClosed) + DelayTable[outcome]` 纯整数;`DelayTable` 烘焙校验 = **定义域内每条 > 0**(≤0 构建期 `throw`;✅ 值已于 2026-10-07 裁定:回响轴三分立值 600/1200/2400,定义域 = 回响轴,**不含结局轴**)
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
6. ⚠️ **数值冻结**:lookback 窗 / 禁忌代价强度 —— 全部归用户数值轮;`>0` 与「永不致死」是机制约束已钉。`DelayTable` 三条值 ✅ **2026-10-07 已裁定**(600/1200/2400,用户事后仍可调,但不再是「未裁」)。

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
  - Given: **定义域(回响轴三分立值)内**某 outcome 值 = 0 / 负 的违规表夹具
  - Then: 烘焙 `throw`;合规边界 min=1 绿
  - Edge cases: 表缺 outcome 行 = 也红(全覆盖校验联动 Story 002 表);**结局轴两值不出现在夹具全集里**(它们不是 key —— 若出现 = schema 错,同样 `throw`)

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

> **补录 AC-6 / AC-7(2026-10-16 · `/qa-plan sprint` 回填,只增不改上方五条)** ——
> 原因为 2026-09-28 建卡时这三项尚未立案:`ConsequenceResolved` Kind 于 2026-09-19 直登 registry、
> `DelayTable` 三值于 2026-10-07 裁定、N-3 于 2026-10-16 ADR-031 重评轮提出 ⇒ 卡面 QA 规格**滞后于裁定**。

- **AC-6**: **`uBand` 装配层单源(N-3 · P1 风险 · 本 story 是 53 链第一个真实喂 `uBand` 的调用点)**
  - Given: 装配层算档路径 + cooked 表自带的 `bandCount`(经 `IDataProvider` 装载,禁独立常量源)
  - When: 把装配层 `bandCount` 源改为与 cooked 表**分歧**的第二真源(负夹具)
  - Then: 越界档位**别名到相邻 `(P,C,D)` 行** ⇒ 该夹具**必须转红**(Sim 侧守护已由 ADR-031 重评轮 F16 证伪,本条防的是**装配层**)
  - Edge cases: `bandCount = 1`(现网值)下越界仍须被 `TryLookup` 域检查接住;**不得硬编码 `bandCount`** —— 数值轮一扩档即踩同一失效模式
  - 数值口径:`DelayTable` 三值(600/1200/2400)**测试读表不抄值**(用户仍可调);U 判别力载体 = `band_count > 1`,**留槽归数值轮**,本 story 不启动 ⇒ **禁借绿**

- **AC-7**: 门存在性刷新(`WriterExistenceGate`)+ 反渗词族补全
  - Given: `unity/Assets/Editor.Tools.Gates/WriterExistenceGate.cs:124` 现登记
    `("ConsequenceResolved", "53 医疗后果与责任未实现 —— 归 53 轮")`
  - Then: 本 story 落地 ⇒ **该待办条目同批移除**,且门须**正向证明写者已登记**
    (防「删了条目但没人写」—— 存在性判据是**双向**的)
  - ⚠️ **AC-3 的反渗词族在此补全**:除 `case_id` / 病例指针外,**`salted_key` 亦不得回渗**进发出载荷
    (AC-53-07 原句只列前两者;`salted_key` 是 F-53.1 的掷骰输入,**回渗 = 可反推具体判断**,破不可归因)
  - Edge cases: `patient_id` **合法**(方向性:病人可指向,**病例**不可)—— 负夹具不得把它误报为违例

**证伪轮映射(S12-4 BLOCKING 级 · 本 story 五类形态**全命中**,逐类须「停用/改坏生产码 ⇒ 该夹具转红」)**:

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| ① 定序 | 跨流全序混流(AC-4) | 停用跨流比较器 ⇒ 混流 / 同 tick 多结案夹具红 |
| ② 共存 / 互斥 | 两后果各自发、不合并(Edge「延迟期内再次结案」) | 改成合并 / 覆盖 ⇒ 该夹具红 |
| ③ 定义域收窄 | `DelayTable` ≤0 / 缺行 / **结局轴作 key** 三类构建期拒收(AC-2) | 放宽任一拒收 ⇒ 对应负夹具红 |
| ④ 一次性 / 幂等 | 重放不重发(判据读流非读内存) | 幂等判定改读内存 flag ⇒ 该夹具红 |
| ⑤ 阈值边界 | `EmitTick` 落 `(now−lookback, now]` 窗边界;`Delay = 1` 绿 / `= 0` 红 | 窗开闭边界翻转 ⇒ 恰跨边界夹具红 |

> ⚠️ **反证登记模板须加一栏「旧实现下该夹具会怎样」**(sprint-12 复盘 Process Improvement 1)——
> F2 / F15b 两条假绿的成因都是「只问新实现下是否通过,没问旧实现下会不会同样绿」。
> ⚠️ **改 → 跑 → 还原须在同一次命令内完成**(trap 纪律),不得让带 bug 的产品码跨命令留在盘上。
> ⚠️ **证伪轮未全红 ⇒ 本 story 不得转 `Complete`**(S12-4 是 BLOCKING,不是 advisory)。

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/MedConsequences/medcons_delayed_emit_test.cs` — must exist and pass
**Status**: [x] Created and passing — **46 测试函数 / 107 用例全绿**(过滤 `MedConsequences`);
全量 EditMode **3508 / 3462 passed / 0 failed / 46 skipped**(基线 3493/3447/0/46 ⇒ **+15/+15 零回归**)
**账本路径**: `tests/unit/medcons/` 未落地 —— 主体编译树 `unity/Assets/Tests/EditMode/MedConsequences/` 承载
(两树均已扫:`grep -rln "ConsequenceEmitter" unity/Assets/Tests/ tests/` 唯命中上列件)

### 证伪轮反证登记(S12-4 BLOCKING · 改→跑→还原同一次 Bash 调用 · md5 还原校验)

| 命中形态 | 停用 / 改坏的守卫 | 反证:转红的测试 |
|---|---|---|
| ① 定序 | 停用 `due.Sort` | `test_ac4_pendingAppendsInAscendingEmitTickOrder`(1 红) |
| ② 共存 / 互斥 | `queueIndex < already` → `already > 0`(合并) | `test_edge_twoClosesWithinDelayWindow_eachEmits_notMerged`(修夹具后 1 红;**修前 0 红 = 本轮抓到并修复的假绿**) |
| ③ 定义域收窄 | 域外 key 静默跳过 | `test_ac2_bake_lifeAxisAsKey_hardFails_schemaError`(1 红) |
| ④ 一次性 / 幂等 | `already = 0`(读内存失忆) | `replay` / `saveInsideWindow` / `reopen` / `twoCloses` / `alreadyEmitted`(5 红) |
| ⑤ 阈值边界 | `emitTick > nowTick` → `>=` | 含 `test_ac5306_windowBoundary_emitAtNowIncluded_beforeNowExcluded` 共 10 红 |
| AC-6 负夹具 | 停用 N-3 `uBand` 域守护 | `test_ac6_bandCount…divergentSourceGoesRed` + `test_table_uBandOutOfRange…`(2 红) |
| AC-7 门双向 | 豁免条目回填 | `test_ac7_writerExistenceGate_*`(2 红) |

**结论**:五类命中形态 + AC-6 + AC-7 **全数反证转红**;零 `FALSIFY` 残留(`grep -rn "FALSIFY" unity/Assets/` 空)。
**评审原件**: `production/qa/evidence/review-medcons-story-003-2026-10-16.md`(含原判定 → 修复落点 → 验证命令 + §六 已落/已证双列结账表)。

---

## Dependencies

- Depends on: Story 002(Outcome 枚举)· Story 001(全序聚合)· registry `ConsequenceResolved` 条目(已直登,OQ-53-7 裁定)
- Unlocks: Story 004(呈现消费世界流)· 6/31 的 P1a 订阅线(流已备数据)

---

## Completion Notes

**Completed**: 2026-10-16
**Criteria**: 8/9 passing + 1 PARTIAL(AC-7 / OQ-53-8 形状门 —— 见 W-4)
**Deviations**:
- **W-4(AC-5 partial)**:只兑现枚举域部分(结局轴 `PatientLives`/`PatientLoses` 与 `NoEcho` 作 key ⇒
  `TryDelayTicks` false ⇒ schema 错 / 静默跳过);**「代价表永不致死形状门」归 GDD/数值轮** —— 如实登记,**不借绿**。
- **W-5(具名跨域缺口)**:`ConsequenceResolved` 带真 `patient_id` ⇒ 进 `EventStream` AC-15 CAP 分支
  (`!IsPresent && PresentCount >= 24` ⇒ throw);**照发不 catch**;doc comment seam 已登记,跨域裁定归 9/7a 轮。
- **生产装配 helper 落测试件**:`Gameplay.Boot` 未授权本 story 改动 ⇒ 装配层 per-case Outcome 现算 helper
  暂住 `medcons_delayed_emit_test.cs`(D-1 A 案);接线归后续装配轮。
- **具名待办 ×4**:B-W1(共享词法器重复 JSON property 缺口,影响全部 10+ binder)· B-W2(无 CLI/CI 烘焙入口)·
  B-W3(ordinal 改名面)· B-W4(cooked 无镜像读方,装载归装配轮)。
**Test Evidence**: Logic — `unity/Assets/Tests/EditMode/MedConsequences/medcons_delayed_emit_test.cs`(46 测试函数 / 107 用例全绿)
**Code Review**: Complete —— `/code-review`(S13-6 拆小 prompt 实战,3 并行有界 agent);
原件 `production/qa/evidence/review-medcons-story-003-2026-10-16.md`(BLOCKING S-B1/S-B2/T-B1 + WARNING + MINOR 全表 + §六 已落/已证双列)
**Falsifiability Gate**: PASS —— 五类形态全命中且全数反证转红(另抓出并修复 1 条假绿夹具)
