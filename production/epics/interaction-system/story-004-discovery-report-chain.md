# Story 004: POI 自报链路 —— `IDiscoveryReporter.Request` / 广播式非 argmin / 有界性 / `R_INTERACT` 单源 / 路由表闭合

> **Epic**: 交互系统
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/interaction-system.md`
**Requirement**: TR-interaction-006(4 不写三流,出境 = `Request` → 6 唯一 `Append`)· TR-interaction-007(幂等由 6 保证,4 零「报过了」记账)· TR-interaction-014 的 `RoutesTo` 闭合半边(路由表与 Kind 枚举双向对拍)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*
🔴 本故事交付 4 唯一的出境通道与其形状。三处承重:① **广播式自报与 argmin 解耦**(F-4.3b)—— 邻域内**全部** POI 各自自报,不因在目标选择中落选被吞;否则 POI 优先级高于/低于 argmin 目标时出现「发现饿死」,且「发现」变相成了选择函数的副产品;② **latch 一律住拥有方** —— 4 侧零「报过了」可变字段(边沿产物 vs 纯函数身份纪律),每 tick 至多一条 `Request` 由 6 侧 per-tick latch + 幂等吸收;③ **`R_INTERACT` 单源** —— 4 的选择半径与 6 的「已发现」触发半径是**同一烘焙字段**,两处各填一个数会静默脱钩(`OQ-4-8` 单值裁定)。

**ADR Governing Implementation**: ADR-021(POI 状态唯一写者 = 6;4 = 合法自报方 `{4,25,37}` 之一;`PoiStateChanged{poi_id, new_state}` 全整数,`Patient = PatientId.None` 不污染高水位;新 Kind 追加通道 = 先 `entities.yaml` 建条目再引用,ADR-009 Amendment 通道已退役)· ADR-009 §六/§七(世界流有界性论证 + 意图事件/主机判距/结果进流三段式 —— 本故事 = 三段式在 POI 侧的 4 半边:6 的判距复验归 6)· ADR-016 §三(粗粒度整数格;事件率上界 = tick 频率与帧率无关)· ADR-020 §四 对称(玩家侧写方在 1;4 的出境是意图,不是位移投影)· ADR-006(`DiscoveryRequest` 载荷全整数域)· ADR-015(邻域 = 切比雪夫球 `[-R,R]³`,单一整数格)
**ADR Decision Summary**: ADR-021 裁「写者 = 6、自报 ≠ 写」但未裁**自报的频率上界与去重责任面** —— GDD `F-4.3/F-4.4` 补齐:上界 `≤ (2R+1)³ / tick / 玩家`(邻域格数硬上界,与帧率无关);去重 = 6 侧 per-tick latch + 幂等(4 侧**无记账**)。本故事交付 4 侧形状与该上界的可测形态,6 侧复验归系统 6 的 Epic。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(纯 C# 整数循环 + spy 计数;真机走查面 `[L]` 归 story 006)
**Engine Notes**: 邻域枚举 = 三重 `for` 整数循环(`d∞ ≤ R` 即切比雪夫球),零分配纪律:复用候选装载器(story 003)的格索引,不做每帧 LINQ/`Vector3` 换算。fake `ITickProvider` 驱动 EditMode 即可测计数上界(GDD `AC-4-12` 已确立该测试形态先例)。`Request` 的载荷 `{poi_id: int64, evidence_cell: WorldPos, tick: int64}` 三字段逐个反射断言类型。

**Control Manifest Rules (this layer)**:
- Required: 新 Kind 唯一追加通道 = 先在 `entities.yaml` 建条目(`stream:`/`author:`/`payload_schema:` 必填)再在任何 GDD/ADR 引用;`PoiStateChanged` 已在 registry(ADR-021 兑现轮) — ADR-024 §Decision ①/③(manifest Foundation「Kind 单一真源」)
- Required: 事件率上界 = tick 频率,**与帧率/位移距离无关** — ADR-016 §三 · ADR-020 §四:291(manifest Feature Guardrail:归并算符一旦被移除,上界退化为帧率,论证静默失效)
- Forbidden: 4 侧引入任何「已报过 / discovered latch」可变字段(去重责任在 6;4 是纯函数) — `AC-4-13` · ADR-021(唯一写者)
- Forbidden: 在 4 或 6 内二次声明 `R_INTERACT`(两处各一个数 = 静默脱钩)— `AC-4-17` · manifest Core「格尺寸须是单一装载常量,禁二次定义」同型纪律(ADR-015 §三:184)
- Guardrail: `R_INTERACT` 真值归用户(`OQ-4-8` 已裁单值,区间 `[1, min(W,H,D)−1]` 受 `4-DC-1` 守,校验本体归 story 006);per-kind 半径(`OQ-4-16`)未裁 ⇒ 出现即回 GDD,不得实现期私加

---

## Acceptance Criteria

*From GDD `design/gdd/interaction-system.md`, scoped to this story(判据正文照录,修订沿革见 GDD 原文):*

- [x] **AC-4-13([A])** —— `GIVEN` 4 反复对同一 POI 自报(每帧一次),`WHEN` 检查 6 的 latch 输出,`THEN` **每 tick 至多一条** `Request`;4 **零**「报过了」记账(反射断言 4 无可变字段 —— 与 AC-4-04 共用)
- [x] **AC-4-17([A])**(⚠️ 6 消费半 NOT-RUN) —— `GIVEN` 同一 `R_INTERACT` **同时**驱动 4 的选择与 6 的「已发现」,`WHEN` 读取两处,`THEN` 来自**同一烘焙字段**(`interaction_kinds.json` 的 `r_interact`,受 `4-DC-1` 校验),**无第二处声明**(承 `OQ-4-8` 单值裁定 —— 两处各填一个数会**静默脱钩**)
- [x] **AC-4-18([A])** —— `GIVEN` 4 的 `Kind` 枚举(`4-DC-2`),`WHEN` 与路由表的行数**逐一对拍**,`THEN` **恰为 10 项、双向闭合**(无枚举外的行、无行外的枚举值);**且 `Player` ∉ 枚举**。⇒ **原稿的 `Kind(c) ≠ Player` 谓词为空转**,删除后由本条承担其意图(规则三 · 五)

---

## Implementation Notes

*Derived from F-4.3/4.3b(广播自报)· F-4.4(有界性)· 规则五(路由表)· EC 表:*

- **`Request` 载荷逐字形状**:`{poi_id: int64, evidence_cell: WorldPos, tick: int64}` —— 反射断言三字段类型全整数域(`WorldPos` ∈ `Sim.Contracts`);`tick` 由 `ITickProvider` 取,**不在 `Request` 内自取**(ADR-007 Roll 纯函数同型纪律:调用方供给)。4 侧调用面 = 邻域内每个 POI 格各发一条(广播),**与 argmin 输出无因果**(`AC-4-18` 的 Kind 对拍 + 夹具「POI 落选仍自报」)。
- **`AC-4-13` 的两半**:4 半 = 每帧驱动自报,spy 计数 4 的**发出**次数 = 帧数×邻域 POI 数(4 不做去重,这是设计不是缺陷 —— 注释钉死);6 半 = per-tick latch 后**吸收**为每 tick ≤ 1 条 `Request`/POI 的落流效应。⚠️ 本故事的断言载体:4 侧「无可变字段」反射与 story 001 `AC-4-04` **共用扫描器**(GDD 原文);「每 tick 至多一条」的验收对象是 6 的 latch —— 6 侧真身未落前以 spy-latch 替身签形状,`BLOCKED-BY: 系统 6 latch 实现`(不借 6 的绿,亦不让 6 借 4 的绿)。
- **`AC-4-17` 的「读取两处」判据形态**:装载期反射/AST:4 的选择半径与 6 的触发半径的**符号源**同为 `interaction_kinds.json → r_interact` 烘焙字段(经 `IDataProvider` 一次装载,两处消费同一内存值);第二处自有 `const`/`[SerializeField]` 声明 = 红。`4-DC-1`(区间 `[1, min(W,H,D)−1]`)校验本体归 story 006,本故事只验**单源**性质(与 EPS/`MAX_DT` 同源常量先例同型:实体纪律不是数值纪律)。
- **`AC-4-18` 双向对拍**:枚举成员集 == 路由表行集(`RoutesTo` 非空,目标系统号 ∈ 已登记集 `{20,17,37,8,10,11,6,23,18,24}`),两方向差集 == ∅;`Player ∉ Kind`(病人走规则五的模态分流:裸 Interact→就诊路由 37 由词表裁决 —— `S-8.4 路线甲` 已结,4 的输出形状逐位不变)。原稿空转谓词(`Kind(c) ≠ Player` 判在不可能出现 Player 的位置)已删,意图由本条承担 —— 该删除是 GDD 二轮订正,实现不得复现旧谓词。
- **有界性夹具(`F-4.4`)**:最坏密度注入(邻域 `(2R+1)³` 格全 POI)× 每帧自报 × 10³ tick ⇒ 出境计数上界断言 `(2R+1)³/tick/玩家`;帧率翻倍夹具(同 tick 数×2 帧)⇒ **每 tick 计数不变**(与帧率无关的直接测项)。
- **幂等/重放边界**:出境「产生时刻差异由主机判距/幂等吸收」在 story 003 `AC-4-20` 已测双客户端形态;本故事补 POI 侧:同一 tick 两客户端各报同一 POI ⇒ 6 幂等收敛为一条 `PoiStateChanged`(spy-latch 替身)。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001:`IDiscoveryReporter` 的存在性 vs `IEventSink` 不可达的边界(本故事是出境的真实链路,001 只有 spy 替身)
- Story 002:argmin 本体(自报与选择解耦的「选择」半边在那儿;本故事只测「落选仍自报」)
- Story 003:候选装载器与确立格取路(邻域枚举复用其装载器输出)
- Story 005:`Accept` 门(被拒意图零出境 ⇒ `AC-4-20` 归 003;本故事假设意图已被接受)
- Story 006:`4-DC-1…6` 构建期校验本体(含 `r_interact` 区间与 `RoutesTo` 登记的硬失败)
- 系统 6:`IDiscoveryReporter` 接收侧的校验/主机判距复验/latch/幂等/唯一 `Append(PoiStateChanged)` 实现 —— 全部归 6 的 Epic(其 `PoiStateChanged` 写者与有界性 ≤ |POI|×|STATE| 承 ADR-021 ④);本故事只以 spy-latch 替身签 4 侧形状
- 系统 25/37:另两个合法自报方的出境 —— 不归 4
- `R_INTERACT` / per-kind 半径(`OQ-4-16`)取值 —— **归用户**

---

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-4-13**: 每 tick ≤ 1 + 4 零记账。
  - Given: 单一 POI 格;4 每帧驱动自报(1 tick = 3 帧的真实帧率关系夹具);spy-latch 替身(6 侧语义:per-tick latch + 幂等)。
  - When: 连跑 100 tick。
  - Then: 4 的**发出**计数 = 帧数(无去重,设计声明);latch **吸收后**每 tick ≤ 1;反射扫描:4 类型中零「discovered/reported」类可变字段(与 001 `AC-4-04` 共用扫描器,字段名语义判定 `int _lastReportedTick` 亦红)。
  - Edge cases: tick 边界跨帧相位(fake tick 驱动故意偏移);两 POI 同邻域(各自独立 ≤1,总 ≤2);玩家在 POI 格往返穿梭(边界进出各 tick)。
  - Negative fixture: 4 侧加 `_reported` bool ⇒ 可变字段红 + 行为「漏报新 tick 状态变化」暴露记账错位(责任面颠倒的可执行证据)。

- **AC-4-17**: 半径单源。
  - Given: 注入 `r_interact = 3` 的烘焙替身;装载器跑一次,4/6 两处消费点取值。
  - When: 改注入值为 5,重载。
  - Then: 两处**同时**变 5(单源的直接形态);AST/反射:全仓 4/6 范围内 `r_interact` 的**声明点 == 1**(kinds 表),消费点 ≥ 2 合法;硬编码第二常量红。
  - Edge cases: 4 邻域枚举与 6 触发判距的**上界方向**夹具(`R = min(W,H,D)` 违例在 006 的 `4-DC-1` 拒;本条只测单源,区间归 006);世界格 W/H/D 不同值时两处读到的仍是同一个 R(不随格形状派生)。
  - Negative fixture: 6 侧声明本地 `const int DISCOVER_R = 3` 恰好等于注入值 ⇒ 仍红(实体纪律)。

- **AC-4-18**: Kind 枚举 ⟷ 路由表双向对拍。
  - Given: 编译期 `Kind` 枚举 + 烘焙路由表替身(10 行);注入违例表:11 行 / 9 行 / 行含 `Player` / `RoutesTo` 未登记系统号。
  - When: 对拍。
  - Then: 合法表:枚举集 == 行集,恰 10 项双向闭合;`Player ∉ 枚举`(编译期类型断言 + 运行时值域断言双保险);违例表逐条被拒(构建期拒载,不是运行期告警 —— 校验体在 006,本条在 4 侧消费点的装载失败面)。
  - Edge cases: 新增 Kind 只加表不加枚举(差集非空 ⇒ 红)与反之;`RoutesTo = None` 行(GDD:每 Kind 必有落点)⇒ 红;原稿空转谓词复现检查(源码若含 `Kind(c) != Player` 式运行期谓词 ⇒ 登记为陈旧形态,注释指向本条)。
  - Negative fixture: 上述四张违例表。

- **广播自报与有界性(F-4.3b/4.4,随 4-13 批签)**:
  - Given: 高优先级 argmin 目标(如 `Patient`)与邻域 3 POI 共存的格布局。
  - When: 一次交互。
  - Then: 选择输出 = Patient(路由 37 面归 005);**同时** 3 POI 各有 `Request` 出境(不因落选被吞);最坏密度 10³ tick 计数 ≤ `(2R+1)³/tick/玩家`;帧率翻倍 ⇒ 每 tick 计数不变。
  - Edge cases: 邻域 0 POI(零出境);POI 恰在半径界上(`d∞ == R` 含,`R+1` 不含 —— 边界归属注释)。
  - Negative fixture: 「argmin 胜者才自报」实现 ⇒ 落选 POI 零出境,红(发现饿死形态)。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/interaction/discovery_report_test.cs` — must exist and pass(每 tick ≤1 + 双向对拍 + 广播 + 有界性矩阵;EditMode fake tick 载体)
- Logic: `tests/unit/interaction/radius_single_source_test.cs` — `AC-4-17` 声明点 == 1 扫描

**Status**: [ ] Pending — story not yet implemented(真身落点预期 = `unity/Assets/Tests/`;登记口径 = `tests/integration|unit/interaction/`)
⚠️ 不得借绿:`AC-4-13` 的「每 tick 至多一条」验收对象是 **6 的 latch** —— 6 侧真身落地前以 spy-latch 替身签形状,该子条记 `BLOCKED-BY: 系统 6 latch/幂等实现`,替身绿不豁免对侧联调;`AC-4-17` 的 `4-DC-1` 区间半边归 story 006,本故事只签单源性质;`TR-interaction-006/007` 的转绿前提 = 6 侧写者闭环,本 Epic 不得先行记 registry 绿。

---

## Dependencies

- Depends on: Story 001(出境/写入的边界机器)/ Story 003(候选装载器供邻域枚举)/ ADR-021 + ADR-024(`PoiStateChanged` 已在 registry;4 侧零新 Kind —— 本故事**不**登记新 `Kind`,若实现需要即违 ADR-024 追加通道)
- Unlocks: Story 005(路由表的消费面 —— `AC-4-18` 闭合后 005 才敢按 Kind 分发)/ 系统 6 的 Epic 集成(接收侧契约已被 spy 钉死形状)

---

## Completion Notes

**Completed**: 2026-10-04
**Criteria**: AC-4-18(双向闭合 10 项 + `Player ∉ 枚举` + 四违例表拒载)与 F-4.3b(广播与 argmin 解耦,
落选 POI 不饿死)与 F-4.4(最坏密度 `(2R+1)³/tick/玩家` + 零 POI/半径界/int64 陷阱边缘)与 AC-4-13 **4 半**
(每帧照报 + 零可变字段反射)**签绿**。⚠️ **两项子条显式 NOT-RUN(机检 `Assert.Ignore`,非注释)**:
① **AC-4-13 6 半**(每 tick ≤1 的**落流效应** = 6 的 per-tick latch/幂等)⇒ `BLOCKED-BY 系统 6`;
② **AC-4-17 6 消费半**(同一 `R_INTERACT` 驱动 6 的「已发现」)⇒ `BLOCKED-BY 系统 6`。
二者**不借 4 侧替身的绿** —— 替身由本测试自造,删任何生产行为它照样绿(自证空转)。
- 三交付物(故事逐字要求):**「4 不去重是设计」注释位置** = `discovery_report_test.test_ac413_fourReportsEveryEvaluationWithoutDedup`(注:该条为**形状声明**,承载判据在 6 侧 NOT-RUN);**帧率翻倍对照计数** = `test_f44_frameRateDoublingDoesNotChangeEgress`(1×=100 / 2×=200,承重判据「出境计数**恰** = 调用次数」);**双向对拍四违例表输出** = `kind_route_closure_test` 四负夹具(11 行 / 9 行 / 行含枚举外值 `(InteractableKind)99` / `RoutesTo` 未登记 999)+ `RegisteredSystems` 7→10 值补修(PF-1)。
**Deviations**: ① **AC-4-17 上界**(`4-DC-1` 的 `≤ min(W,H,D)−1`)**接收端承接漏**(QA F-C):story-006 的
`4-DC-1…6` 矩阵只列了 `R_INTERACT = 0` **下界**,**无上界名额** ⇒ story-004 侧 deferral 注释在场,
**接收端须补名额**(呈报后续 batch,非本故事可闭合)。② **AC-4-18 「构建期拒载」措辞**(QA F-G):
实测 = `new KindRouteTable(...)` **构造期 throw**(EditMode 运行期),非构建期 —— 故事 carve-out
(「4 侧消费点的装载失败面」)已覆盖,措辞按实测登记。③ **AC-4-17 第二声明扫描器是启发式**
(结构 #4):命名启发式可改名规避、只认整型、全类型跳过 `InteractionRadius` ⇒ **非「声明点 == 1」的证明**,
已在测试内登记为**已知限制**;真值单源由烘焙管线(ADR-014)+ 代码审查共同守。④ **胜者 POI 潜在双重出境**
(结构 #6):`InteractionSelector.Select` 与 `NeighbourhoodReporter` 各发一次;6 的 `(tick,poi)` latch 使流层
收敛 ⇒ **保留**,登记为 6 联调观察点。
**Test Evidence**: `tests/integration/interaction/discovery_report_test.cs`(真身 `unity/Assets/Tests/EditMode/Interaction/`)
+ `tests/unit/interaction/radius_single_source_test.cs` + `tests/unit/interaction/kind_route_closure_test.cs`。
**实跑**:`unity/Logs/interaction-s004-final2.xml` = **86 tests / 83 passed / 0 failed / 3 skipped(NOT-RUN)**;
变异证明 `unity/Logs/mut-cheb.xml`(丢 int64 拓宽 ⇒ 恰 1 红)+ `mut-gate.xml`(删主动交互门 ⇒ 恰 1 红)。
**Code Review**: 双代理评审**一轮**(结构侧 APPROVED WITH SUGGESTIONS · QA 侧 ACCEPT-WITH-FIXES);
**原件** `production/qa/evidence/review-interaction-story-004-2026-10-04.md`(原判定 → 修复落点 → 验证命令)。
**Manifest**: 版本号已对齐 2026-10-02(⚠️ **仅版本号** —— 抽象点计数订正另立批次,见 control-manifest §传播范围)
