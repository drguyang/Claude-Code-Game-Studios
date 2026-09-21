# Control Manifest

> **Engine**: Unity 6.3 LTS
> **Last Updated**: 2026-09-20
> **Manifest Version**: 2026-09-20
> **ADRs Covered**: ADR-001, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015,
> 016, 017, 018, 019, 020, 021, 022, 023, 024, 025 —— **全部 22 份 Accepted ADR**
> **Status**: Active —— ADR 变更后用 `/create-control-manifest update` 重生成

`Manifest Version` is the date this manifest was generated. Story files embed
this date when created. `/story-readiness` compares a story's embedded version
to this field to detect stories written against stale rules. Always matches
`Last Updated` — they are the same date, serving different consumers.

This manifest is a programmer's quick-reference extracted from all Accepted ADRs,
technical preferences, and engine reference docs. For the reasoning behind each
rule, see the referenced ADR.

---

## 本件的两条阅读前提(先读这一节)

**① 本件不新增任何裁决。** 每条规则都必须能回指一份 Accepted ADR、
`.claude/docs/technical-preferences.md`,或 `docs/engine-reference/unity/`。
**提取不到出处的规则不得进本件**(承 `create-control-manifest` 家规 4「Source every rule」)。

**② ⚠️ ADR-023 是「附条件 Accepted」,且 S1–S7 spike 一条都没跑。**
该件 `§Validation` 表内全部未勾。本件中凡是源自 ADR-023 的行为性断言均带
`[未验证前置:S<n>]` 标记 —— **读到该标记即知:此规则的方向已裁,但其依赖的引擎事实未证**。
**不得把本件任何一条当作"已实测"引用。** 同理,ADR-013 的手柄焦点桥、ADR-017 的
`noEngineReferences` 机制论据、ADR-012 的 F7 溢出问题,均在
`docs/architecture/architecture-review-2026-09-20.md` §5.2 登记为**三项承重引擎风险**
(R-A / R-B / R-C),**未结案**。

---

## Foundation Layer Rules

*Applies to: 确定性 sim · 三逻辑流 · 存档/加载 · 程序集清单 · CI 确定性 · Kind 登记*

### Required Patterns

**定点域与边界(ADR-005 / ADR-006)**
- 全部模拟数学在整数定点域:int64 承载 Q16.16 的 `Fix` 纯值 struct,中间乘法落 Q32.32 再移位回;哈希用 `SplitMix64` — source: ADR-005 §Decision 一
- 舍入纪律:**保守带内边界搜索 = 向下保守**(宁多扫一步);表现层输出 = 就近舍入 — ADR-005 §Decision 一
- `boundary_mode: scan` 的病种必须做**单向性验证**(断言扫描边界**含**真实穿界点),进 AC 与 CI 门 — ADR-005 §Decision 一
- 外部数据 → `Fix` 的转换**只经 `FixParse`**(`Parse(string, RoundMode)` / `FromRatio(long,long)`);不提供 `Parse(float)` / `implicit operator Fix(float)` — ADR-006 §Decision 一
- 数据文件里 `Offset` / `τ_half` / `axis_offset_by_quality[]` 写整数字面量或 `分子/分母`,**导入期**一次性转 Q16.16 — ADR-006 §Decision 一
- 浮点字面量**在 schema 层即被拒**;运行期不存在 float→Fix 路径(编译期不可表达) — ADR-006 §Decision 一
- 存档与事件流中不出现任何 `float` / `double`;`Fix` 唯一合法落盘形状是内部 `long`,**须自定义编码器显式写出/读入** — ADR-006 §Decision 二 / 五
- `weight` / `stack_max` **是 `int` 计数,不是 `Fix`**(D-21-17) — ADR-006 §Decision 一
- 全部舍入用 `ROUND_HALF_AWAY_FROM_ZERO`,**在整数域内完成**;`RoundMode` 是**全局常量**,非逐调用点参数 — ADR-006 §Decision 三
- 守恒律 `Σ(weight × outputs) ≤ EFF_MAX × Σ(weight × inputs)`,`EFF_MAX ≤ 1`,**整数域内求值**;实耗按 `ActualConsumed_i = Ceil(inputs_i.qty / EFF)`(D-21-15) — ADR-006 §Decision 四
- 编码器**按字段名编码,禁按字段位置**;由一条 EditMode 序列化探针守住(断言内置序列化器往返 `Fix` ⇒ 值丢失,自定义编码器 ⇒ 逐位还原) — ADR-006 §Decision 五

**事件流与权威(ADR-005 / 007 / 008 / 009)**
- 真源 = **病史流 ∪ 病例流 ∪ 世界流**(三流并集);终态物理折叠**只作用于病史流** — ADR-005 §Decision 三 · ADR-006 Amendment D/E
- **主机唯一执行 `Step` / `CatchUp`**;客户端持流副本 + 定期快照,可本地求值 `Progress`(**纯函数**),**绝不写回流** — ADR-005 §Decision 三
- `SimEvent` 形状 = `{ Tick:long, Patient:PatientId, Seq:long, Kind:EventKind, Payload:EventPayload }`;**`Seq` 是 `long` 不是 `int`**(写错 ⇒ 编码器流宽差 4 字节 ⇒ 读流静默错位) — ADR-005 §Key Interfaces · ADR-006 Amendment A
- `Seq` 由**主机在 `Append` 时分配**,同一 `(Tick, Patient)` 内自 0 单调递增;`Seq` 随事件持久化;`Payload` 是值 struct、无引用字段 — ADR-006 Amendment A
- 跨流全序键 = `(Tick, StreamPriority, Patient, Seq)`;**单流内** = `(Tick, Patient, Seq)`;**`Patient` 项不可省略**(同 tick 两位病人各有 `Seq=0` ⇒ 缺它即平局) — ADR-008 §二 · ADR-006 Amendment C
- `StreamPriority`:病史 0 < 病例 1 < 世界 2;**`Seq` 不分流**,三流共享同一发放器与同一 `(Tick, Patient)` 计数域 — ADR-009 §三
- 六个 P0 抽象点必须预留:`ITickProvider` / `IEventSink` / `IIdAuthority` / `IVitalsQuery` / `IEventAuthority` + `SimEvent` — ADR-005 §Key Interfaces · ADR-007 §一
- `IEventAuthority` **不与 `IEventSink` 合并**(写入通道语义 ≠ 掷骰权语义);P0 = 本地占位,`IsAuthority` 恒 `true` — ADR-007 §一
- `Roll` 必须是**纯函数**:入参即其全部输入,**不读任何运行时对象**;`Win` 由**调用方**从 `ITickProvider` 取,不在 `Roll` 内取 — ADR-007 §Implementation Guidelines 2/3
- **掷骰的每个输入必须可从事件流重构**(或为外生常数);五个 Kind:`EventRolled` / `EventArrived` / `ThreatDeferred` / `ThreatDeferralCleared` / `HistoryFlagChanged` — ADR-007 §三
- `WorldSeed` 归 **7a 存档头**,世界创建时由平台密码学源生成一次(全案唯一允许非确定性处);52 / 9 **只读**;跨版本原样保留 — ADR-007 §二
- 世界级事件用显式哨兵 **`PatientId.None ≡ -1`**;`PatientId` 是 `readonly struct : IEquatable<PatientId>`,持久化与全序走 `Value` **不用 `GetHashCode()`** — ADR-007 §四 / §Key Interfaces
- `max(patient_id)` 重构:`next = max(全部已知 id) + 1` 由事件流**重构**(非独立快照);扫三流并集,`None` **显式排除**(写成 `if (p == None) continue;`);计数器**永不复位 0**;终态折叠行**必须保留 `patient_id`** — ADR-006 Amendment B/E · ADR-007 §四
- `IEventSink.Append` 路由 = `Kind → StreamId` **纯函数白名单**,主机 `Append` 时执行;列表外 `Kind` **构建期拒绝** — ADR-008 §一 · ADR-009 §三 · ADR-024
- 病例流五个 Kind 与载荷见 ADR-008 §三;`opened_tick` **不得**重复携带(它就是 `CaseOpened.Tick`);`anchor_case` 保留(跨事件引用) — ADR-008 §三
- 可结案判据:处置事件 `e.Tick ∈ [c.opened_tick, c.CloseTick]` ∧ 玩家勾选;**`treated` 快照进 `CaseClosed`**,重放永不跨流查询 — ADR-008 §四
- 同一 `case_id` 第二条 `CaseClosed` **幂等拒收**(不产生事件);判定 = 流前缀的纯函数 — ADR-008 §七
- 世界状态先过**三问判据**(真源 / 权威 / 可感知)→ 三态分类(模拟态进流 · 派生态由外生源重建 · 表现态走网络层);结论登记进该系统 GDD §Dependencies 三态表 — ADR-009 §一 / §Implementation Guidelines 1
- 派生态的两类源:① 种子派生纯函数 ② **版本化烘焙数据**(世界几何,ADR-015);静态地形加载期一次性重建、不进流、中途加入**不传输** — ADR-009 §一 修订注 / §四
- 掉落 = **身份进流 / 位置表现**;`DropSpawned{ instance_id, spawn_anchor(WorldPos 整数格), item_key, qty }`;`instance_id` 由 `IIdAuthority.ItemInstanceId.Next()` 发放,**不本地铸造**(D-21-27) — ADR-009 §五
- 拾取 = 意图事件 + **主机当下判距** + 宽容半径;**结果进流,判定过程不进流,重放不重判** — ADR-009 §七
- `ActorCellEntered`(三字段全整数)**不另发 Exited**;玩家在开局经 `IIdAuthority` 分得 id;排序键 `Patient` 仍用 `None`(**一个 id 空间、两个字段各司其职**) — ADR-009 §三 / Amendment G
- 水平防隧穿不变量 = `SPEED_MAX × **MAX_DT** ≤ LATTICE_SIZE`(跨格检测每帧跑,积分步长是 `dt` 不是 tick);`TICK_PERIOD` 只管事件率面;**两条分别断言**;y 轴显式豁免但不解除计数上界 — ADR-009 Amendment G
- `PlayerDied` **无条件发出**(背包空也发),冷却期内不发;`death_cell` = 该玩家最后一条 `ActorCellEntered` 的 cell 原样携带,**禁读实时物理位置**;写者 = 9 — ADR-009 Amendment L
- 一次成功采集落**三条**世界流事件(`ResourceHarvested` + `DropSpawned` + `DropClaimed`,主机同 tick 连发);`raw_quality` **不落流**(可重算 = 第二真源),`out_quality` **必须落流**(截断依赖当刻技能);`gather_seq` 以 `ResourceHarvested` 为计源,零独立计数器 — ADR-009 Amendment K
- `Craft` 起货溢出**复用 `DropSpawned`**(零新增 Kind),写者 = 20 而非 18;`output_instance_ids[]` 主机于点火 tick 铸造 — ADR-009 Amendment J
- `EmergencyAttempt`(判定输入,客户端产出 / **主机物化发号**)与 `EmergencyTreatmentApplied` / `DrugTreatmentApplied`(结算)拆**三个 Kind** 落病史流;一条完成动作 ≤ 2 条,**与帧率无关** — ADR-009 Amendment I

**存档 codec(ADR-010)**
- **全二进制**;手写、**按字段名 / tag 编码**(proto 风格),禁位置打包;不用 JSON / PlayerPrefs — ADR-010 §Decision / §一
- 头部 `magic "DYJQ" (4B) · SaveVersion u32 · WorldSeed u64 · ConfigVersion u32 · Tick i64 · SnapshotOffset u64 · Checksum 32B` — ADR-010 §一
- **显式声明小端并写黄金字节夹具**钉死(编辑器 vs IL2CPP 产出相同字节) — ADR-010 §一
- 原子写 = 同目录 tmp + `FileStream.Flush(true)` + `File.Move(tmp, final, overwrite:true)`;写 `Application.persistentDataPath`;`IOException` 退避重试 — ADR-010 §一
- 校验和 = BCL **`SHA256`**(非 `UnityEngine.Hash128`),覆盖 checksum 字段之后全部字节;加载失败**自动回退 `bak`,玩家无打断** — ADR-010 §四
- **§三 义务汇总表(13 条)= 单一出处,任何新委派只能追加到本表** — ADR-010 §三
- `Folded(p)` 三条件合并一处:终态 · plateau/relapse 豁免 · **无未结案病例** — ADR-010 §二
- `Checkpoint(SaveSlot)` 是**唯一写入口**(反射断言公开参数集恰 = {`SaveSlot`});槽位唯一身份 = `slot_seq`,只进不退;槽头信息是呈现 DTO 不进字节面 — ADR-010 §Key Interfaces
- checkpoint = 事件触发为主 + 定时兜底;`EVENT_CHECKPOINT_ANCHORS` = {脉案落笔 · 病例结案 · 出诊启动}(只增不删) — ADR-010 §六
- 主线程只做序列化(Step 边界),写盘交后台线程;**后台线程禁调 Unity API**;缓冲池复用防 GC 停顿;退出钩子 = `Application.wantsToQuit`(`OnApplicationQuit` 只兜底)+ 限时 join — ADR-010 §六
- `SaveVersion`(迁移链入口)与 `ConfigVersion`(后续窗口用什么值)分离;迁移逐版本升级、`WorldSeed` 与已发生事件原样保留;**「不匹配非致命」的措辞不得改成致命**(要改须另开 ADR) — ADR-010 §七
- 先写 codec 与黄金夹具,再写存档流程 — ADR-010 §Implementation Guidelines 1

**程序集清单(ADR-025)**
- **六装配**: `Sim` / `Sim.Contracts` / `Sim.Codec` / `Gameplay.Presentation` / `Gameplay.UI` / `Editor.Tools` 族;**asmdef 集合恰 = 表 ∪ 测试装配族**,多一个未登记装配 = 构建失败 — ADR-025 §①/§④
- `Sim`:引用集 **恰 = {BCL, `Sim.Contracts`}**,`noEngineReferences: true`;含 19 个 sim 模块 + `StreamRouting.g.cs`,**25 与 9 同程序集** — ADR-025 §①
- `Sim.Contracts`:引用集 **恰 = BCL**;含 `WorldPos` · 六抽象点 · `SimEvent`/`PatientId`/`StreamId`/`EventKind` · `VitalsDto` · `Fix`+`FixParse` · `IDataProvider` · `AudioCueDto`+`IAudioCueSink` · `IPositionalChannel` · `ITeleportCommandSink` — ADR-025 §①/§③
- `Sim.Codec`:BCL only;7a 三流 codec + 存档头 + `Fix` 编码器(`internal` + `InternalsVisibleTo("Sim.Contracts.Tests")` = D-21-18 守卫的可执行形态) — ADR-025 §①
- **QQ-03 甲案**:`Fix` 保持 public;执法 = 构建期断言「`ToFloat()` 调用点所在 asmdef ∈ {`Sim.Codec`, `Gameplay.*`}」,**`Sim` 内调用 = 构建失败** — ADR-025 §②
- **QQ-01 ①′**:传送契约拆两半 —— 整数半 `ITeleportCommandSink.RequestTeleport(int actorId, WorldPos cell)` 进 `Sim.Contracts`,`Vector3` 连续半留 `Gameplay.Presentation` — ADR-025 §③
- 测试装配:`tests/EditMode` → `Sim.Contracts.Tests`;`tests/PlayMode` → `Gameplay.Tests`;种子测试 `sim_fixedpoint_test.cs` 归前者 — ADR-025 §⑤
- 门 A 是**单向**约束(只约束 sim 实现程序集);门 B(EditMode 反射断言:引用集无引擎、IL 无 `Math.Exp/Pow/...`、类型图无 float/double)**只扫 `Sim`**,边界程序集无需 — ADR-005 §Related Amendment F

**CI 确定性门(ADR-012)**
- **双级黄金夹具**:层级 1 单元级哈希(`Fix` 四则 / 负值右移 / 负值除法向零截断 / 定点 `Exp` / `SplitMix64` / CDF walk / 编码器往返 / `ROUND_HALF_AWAY_FROM_ZERO` 负值)+ 层级 2 集成级存档字节 — ADR-012 §一
- 单元级对拍必须进 **IL2CPP player**(`BuildPipeline.BuildPlayer` 无头跑 → 写 `persistentDataPath` → CI 读回);EditMode 恒为 Mono,**不经 IL2CPP** — ADR-012 §一 F4
- **三格全免费常驻矩阵**(Linux-x64-Mono / Linux-x64-IL2CPP / Linux-ARM64-IL2CPP 交叉构建);Windows-x64 与 Apple Silicon = 发版前必跑(计费) — ADR-012 §二
- IL2CPP 格 = `unity-builder@v4` 出 player + **另起 job** 跑;Linux IL2CPP 需 C++ toolchain ⇒ 用 `unityci/editor:ubuntu-*-linux-il2cpp-*` 镜像 — ADR-012 §二
- IL2CPP 编译旗标 **逐目标登记**(`SetAdditionalIl2CppArgs` 是全局单值 ⇒ 须 `IPreprocessBuildWithReport` 按 `BuildTarget` 切换或自维护 per-target 表) — ADR-012 §三
- `csc.rsp -checked+` **只用于测试程序集**;sim 发布路径保持 `unchecked`(回绕定义性) — ADR-012 §三
- 夹具版本化刷新(`golden-vN` + 变更日志 + **全体平台同时重签** + 旧版保留回归对比) — ADR-012 §四
- 漂移面纪律:二进制 IO · 显式 UTF-8 无 BOM · 十六进制 ASCII · 哈希只对同一 byte buffer;夹具双投递(EditMode `[CallerFilePath]` / player `StreamingAssets`,bin 用 `.bytes`) — ADR-012 §五
- **F7 BLOCKING spike 先行**:int64 溢出在 IL2CPP C++ 后端是 UB,`SplitMix64` 与 Q16.16 中间乘正踩此线;**回绕用例 = 单元级黄金哈希第一条** — ADR-012 §Implementation Guidelines 1

**Kind 单一真源(ADR-024)**
- `entities.yaml` 的 `SimEvent.Kind.*` 是三流全集(补齐后 **33 支**)**唯一登记真源**;每条必填 `stream:`(枚举值,**禁从散文解析**)· `author:` · `payload_schema:`(类型只允许整数域) — ADR-024 §Decision ①
- **新 Kind 的唯一追加通道 = 先在 `entities.yaml` 建条目,再在任何 GDD/ADR 引用**;**ADR-009 Amendment 追加通道(F–L)退役**;但 **F–L 的历史文本不改写**(它们是事实记录) — ADR-024 §Decision ③/§Consequences
- ADR-009 §三/§二 就地降级为**路由注记**:不一致时以 registry 为准,并触发 V-1 断言失败 — ADR-024 §Decision ②
- 构建期生成器 `tools/kindgen/` → `src/Sim/StreamRouting.g.cs`;断言 **A1** 唯一流别 / **A2** 载荷 ∈ 整数域 / **A3** 无重名 / **A4** author 必填 / **A5** 双向差集归零;任一失败 = 构建失败,**必须 `throw`,禁 `Debug.Assert`** — ADR-024 §Decision ⑤ / §Validation V-1
- `StreamRouting.g.cs` 的 case 数 = registry 条目数 = **33**(V-2,可复算);生成物勿手改;拒绝表 17/18 条的执行体归 **ADR-014 阶段 2**,生成器不重复实现 — ADR-024 §Validation V-2 / §Decision ⑥

---

## Core Layer Rules

*Applies to: tick 驱动 · 输入 · 世界几何与格 · 网络 pipe · 数据管线*

### Required Patterns

**tick 驱动(源在 ADR-005,但其生效面在 Core 的 driver)**
- 一切推进走定 tick;**禁 `Time.deltaTime` / 帧数 / 真实墙钟**;`patient_seed` **不可用 `Random.Range`** — ADR-005 §GDD 表:411, 413
- `TICK_SECONDS = 0.05`(= 20 Hz,OQ-25-8 已裁);**改它 = 重导全部事件时间戳与存档**,属 ADR / 9 级动作,**不得由各系统自填** — ADR-005 §Implementation:361-362
- ⚠️ **20 Hz 与 60 fps 不整除** ⇒ `Step` 必须**由 `ITickProvider` 驱动,不得挂在渲染帧上**(步相位为实现期义务) — ADR-005 §Implementation:363
- ADR-005 §Performance 的 CPU 帧行量纲 = **24 实体 × 20 Hz × 单次求值**;「单次求值成本」= **实测项,非裁决项** — ADR-005 §Performance:367

**网络 pipe(ADR-001)**
- 45 必须承载**三条逻辑流**(病史 / 病例 / 世界)+ **表现态位置同步** — ADR-001 §约束:110
- pipe 契约 = **可靠即可,保序非必需**;重排由全序键 `(Tick, StreamPriority, Patient, Seq)` 吸收 — 源:`technical-preferences.md` ADR-001 条目(**ADR 内无对应行号可引**)
- 必须 **authority-agnostic**(与 ADR-010 存档同构);必须隔离底层库面(P0 实现零依赖) — ADR-001 §约束:112, 113
- **后台 / 传输线程不得调 Unity API**(与 ADR-010 §六 同纪律) — ADR-001 §约束:106
- 第二 QoS 通道 = **按 `ActorId` 索引的 latest-value 表**;44 的优先级排序若读该表,**必须用 `ServerTick` 判陈旧 + 允许缺省锚点** — ADR-001 §一之二:207(表形状 `:158`,2026-09-18 修订)

**输入(ADR-011)**
- **action-based 官方路线**:Input System 动作资产,K&M + Gamepad + OpenXR 三套绑重 + `bindings overrides` 持久化
- 急救动作走**独立直读通道**(< 50 ms 延迟路径),**不穿 42 UI 事件栈** — ADR-011 §二:142
- **判定结果进流前必须 `FixParse`**(意图本身非 SimEvent,只有判定结果是) — ADR-011 §二:187
  ⚠️ **同件 `:190` 自评该条「编译期就不可表达」= 不可执行** ⇒ 本条**不得单边引用**,须带矛盾读(见「Open Items」§B)
- 绑重按 GUID 匹配 ⇒ **资产重建即静默失效(无报错)**;重载前须 `asset.Disable()`;持久化流程须含 `RemoveAllBindingOverrides()` 一步;**schema hash 须含 `bindingId`**;④ 克隆**不得启用 UI map** — ADR-011 §一:129, 136, 137
- 1-4 人同机共进程时须**每玩家 `Instantiate` 资产**;⚠️ `Instantiate()` vs `Clone()` 在引擎参考库中**零覆盖 ⇒ 须 spike** — ADR-011 §一:132-133
- Amendment B 改判(2026-09-18):客户端**聚合为一条 `EmergencyAttempt` 全整数意图事件**上行 → **主机执行 `Judge` + `Append` + 发号 `Seq`**;本地判定**降级为预表现** — 源:`technical-preferences.md` ADR-011 条目

**数据管线(ADR-014)**
- 凡承载 `Fix` 数据者**只能**落 `assets/data/*.json` 并经 `FixParse` 读入;承载 `Fix` 的字段**必须外置**(内置序列化器对 `Fix` **静默归零**) — ADR-014 §S1:119, 125
- **本文不把「全部数值必须外置」升为铁律** —— 硬口径**收窄到承载 `Fix` 的字段**;纯 `int` 常量若 GDD 声明为固定常量则合法 — ADR-014 §S1:127
- `Fix` 承载字段在 JSON 里**必须是字符串,不是 JSON 数字**(`"offset": "3/4"`) — ADR-014 §四:201
- 逐 schema 维护**已知键白名单:未知键 = 烘焙期硬失败**;每文件带 `schema_version`,**版本不匹配 = 硬失败**;数组长度校验(`axis_offset_by_quality[]` / `quality_character[]` **必须 = `MAX_QUALITY`**) — ADR-014 §三:179, 184, 191
- **全量校验失败 = 构建失败,不是警告**;作者态解析失败在**烘焙期**即失败,**不进运行期** — ADR-014 §三:192 · §二:160
- 作者态文件名与病名**不得一一对应**(否则「换名不换壳」= ADR-008 第六处泄漏面敞开) — ADR-014 §一:149
- Addressables 装载失败 = **启动期硬失败并给出清晰错误,绝不 null 解引用**(E-13) — ADR-014 §五:224
- ordinal ↔ 名称映射表须进 `ConfigVersion` 覆盖集;**既有条目的号永不重用、永不改义** ⇒ 改义 / 改号 / 复用 = **构建期硬失败** — ADR-014 §五:238, 258(同 ADR-006 A-B/D-21-13:156-164)
- ⚠️ **非对称性须记明**:阈值可改而**旧档仍可正常加载**(非致命),但同一存档读两次… — ADR-014 §五:268

**世界几何与格(ADR-015)**
- **空间位置一律整数格**;需亚格精度用格的**整数细分**,**不引入 `Fix` 坐标** — ADR-015 §三:179
- **格尺寸须是单一装载常量**(两侧同源,**禁二次定义**) — ADR-015 §三:184
- `slopeLimit` **必须与逻辑层「可走坡」同源**(关卡工具同一处定义 → 烘进逻辑层并同步为引擎参数);**两侧不一致 = 装载失败** — ADR-015 §一之补:143
- chunk 只是加载 / 卸载 / 失效的粒度,**不参与坐标定义**;**驻留与否不改变判定结果**(判定只依赖格坐标与逻辑数据) — ADR-015 §四:192, 195
- 建造网格 = 模块化网格,**与地形共用同一格** — ADR-015 §五:208

**场景生命周期(ADR-023)⚠️ 整块带前置**:该件为 **附条件 Accepted**,**S1–S7 spike 零执行**
(`§Validation` 表内全未勾)。以下**方向已裁、引擎事实未证** —— **不得当作"已实测"引用**;
规则↔具体 S 编号的映射**未在件内钉死**,故本件不按条拆分标记。
- 场景拓扑 = **三场景制**(`Boot` 常驻 / `MainMenu` / `World` additive) — ADR-023 ①:112
- 菜单 ↔ 游戏 = **additive 换入换出,永不用 `LoadSceneMode.Single`** — ADR-023 ①:120
- **`World.unity` 内任何带 `Sim` / `Gameplay` 程序集组件的 GameObject = 构建期硬失败**;依据:逻辑层是烘焙数据,**gameplay 对象的存在性本身是模拟态** ⇒ 必须运行期物化 — ADR-023 ②:127, 129
- 相机与 `AudioListener` **归 Boot**(常驻;只允许 disable-replaceable,**禁 mutate**) — ADR-023 ③:133(正文 :135-138)
- tick 驱动与场景解耦:`Boot 起 → data-core 预载完成(失败 = E-13 硬失败)→ 存档头 + 三流重放 + CatchUp` — ADR-023 ④:145
- **交互意图冻结门:tick driver 未起相前,3 的 action 一律不产出意图事件**(P-03 通道掐断) — ADR-023 ④:147
- **拆序六步**;第 6 步 = **断言登记簿空**(根因:`Addressables.UnloadSceneAsync` **不销毁** `InstantiateAsync` 产物 ⇒ 不强制则每次读档漏一个世界) — ADR-023 ⑤:151(步序 :154-163)
- 运行期 chunk 激活权 = **系统 6**(容器的裁决权),**只读 sim 量、不读表现态连续位置** ⇒ 激活是派生态,不进流 — ADR-023 ⑥:166, 169(判据 :171-173)
- 读档后的连续位置 = **格锚点 + 确定性格内偏移**(BCL 整数哈希,**不引入第二随机源**) — ADR-023 ⑦:177(正文 :180-182)
- **P0 零 custom Renderer Feature**:`ScriptableRenderPass` / RenderGraph 定制面**零使用**;须按新签名 `RecordRenderGraph` **另开 ADR** 才可引入 — ADR-023 ⑧:185, 187-191

### Forbidden Approaches

| 禁 | 出处 | 理由(照抄) |
|---|---|---|
| **把 NGO / Fusion 任一写进 P0 依赖** | ADR-001 §二:218 | 库裁决**延后**;pipe 抽象是**唯一的**网络契约 |
| **Legacy Input Manager / `Input.GetKey` 族** | ADR-011 §一:138 | 新输入系统是 6.3 默认;输入层零旧输入依赖 |
| **`JsonConvert.DeserializeObject<T>` / `JObject.Parse`** | ADR-014 §三:173 | 数字经 `double`/`decimal` 中转 = **浮点泄漏**,绕开 `FixParse` |
| **把视觉层地形采样喂进 sim**(如 `Terrain.SampleHeight` 定可走性) | ADR-015 §一:123-124 | float 入 sim 破 ADR-006;可走性**一律由逻辑导航格给出** |
| **chunk 局部坐标系** | ADR-015 §四:197 | 同一点在不同 chunk 有不同坐标 ⇒ 跨块判定必错 |
| **第三方地形工具承担运行期生成**(GAIA / MapMagic 2) | ADR-015 §六:226 | 只能作编辑期素材 / 预览;**运行期零第三方** |
| **`LoadSceneMode.Single`** | ADR-023 ①:120 | Single 会瞬杀 Boot 之外的常驻(含相机 / AudioListener) |
| **绕过门面的裸 `InstantiateAsync`** | ADR-023 ⑤:154-163 | 绕过即脱离登记簿 ⇒ 第 6 步断言失去对象 |
| **自实现焦点算法而不禁官方桥 Navigation 动作** | ADR-011 §三:213 | 否则**同键双触发**。⚠️ 该条与 ADR-013 的 RC-1 是同一处未结案矛盾 |
| **Voxel 建造 / 每 chunk 局部原点 / 从烘焙 Terrain 资产反推逻辑层** | ADR-015 §Alt:248, 256, 267, 277 | 三条 Alternative **均否决**(否决理由见该件) |
| **运行期任何 JSON 解析 / `FixParse`** | ADR-014 裁定 ②(玩家构建) | 出货形态 = 构建期烘焙 `*.cooked`;**玩家构建零解析器** |

### Performance Guardrails

- **`L_input` < 50 ms 只测预表现路径**(10 全责);`L_eval` ≤ `TICK_PERIOD` = 50 ms 归 9 的求值节奏 ——
  **两条口径不同,禁合并成「端到端 < 50 ms」** — 源:`technical-preferences.md` §Performance Budgets(②)
- 「主机 `Append` 到体征可见」的实际延迟 = **50 ms + 求值次序**,该延迟**不**受 50 ms 手感预算约束
  (玩家对体征何时变本就延迟一个 tick) — 同上
- **急救输入延迟的直接失效模式 = 延迟杀死「手稳」**;故直读通道不得插入 UI 事件栈 — ADR-011 §二:142
- 拆序六步是**内存护栏**,非仅正确性:漏一个世界 = 每次读档线性泄漏 — ADR-023 ⑤

---

## Feature Layer Rules

*Applies to: 病人 / 敌人 AI · 感知与寻路 · POI 状态 · 急救与处方*

### Required Patterns

**AI 分层确定性(ADR-016)**
- 分层:**行为决策进 sim(整数域,可重放),运动表现态在表现层驱动** ——
  ⚠️ **13 是唯一例外**:病人 AI 决策住**边界层 / 呈现侧**(它消费 `VitalsDto` = float,物理上不可能住门 A 程序集) — ADR-016 §一:152
- **AI 决策是派生态**:不进流、不存档,重建期重建 —— **效果进流,决策不进流** — ADR-016 §一(口径)
- **可重建性充要条件**:全部输入 ∈ {事件流, 版本化烘焙数据, 二者的纯函数};**第四来源一律禁** — ADR-016 §一(三源不变量)
- 感知输入 = **粗粒度整数格**;玩家跨格写世界流事件 ⇒ **事件率上界 = tick 频率**(与帧率 / 位移距离无关) — ADR-016 §三:204 起
- 敌人 id **经 `IIdAuthority` 与病人共用同一 id 空间**;折叠行保留的 id 字段语义由「病人 id」扩为「**受伤实体 id**」;敌人伤情**落世界流**(不污染病史流),**折叠谓词不适用于敌人行** — ADR-016 §二:198
- 行为程序载体 = **作者态 `assets/data/ai_enemy.json`** → 构建期烘为整数数据;**源改了没重烘 = 构建失败,不是运行期幻觉** — ADR-016 §四:247
- sim 寻路 = `WorldPos` 格上的 **A\***;JPS / 预烘流场为**可选优化**,仅性能实测不达标时启用,且**必须保持整数与确定性** — ADR-016 §五:260
- 建造**随世界流事件变为不可走时,AI 寻路必须看到该变化**(否则「AI 走进玩家刚盖墙的格」) — ADR-016 §五:267
- 导航 Overlay 是**派生态**:不进流、不存档,重放时从 `BakedInitial ⊕ 事件` 重建 — ADR-016 §五:268
- 13 读 `IVitalsQuery.GetVitals() → VitalsDto`(**全案唯一浮点出口**),**不得回写病史流** — ADR-016 §六:283
- **27 的行为程序必须支持「已接触但可脱离」** ⇒ 遭遇体**不得实现为硬锁定仇恨(不可脱战)**;P0 兵痞原型须首先满足 — ADR-016 §八:312-314
- **冻结须同冻 `acc`**,否则解冻首 tick 一次吐出累积量 = **穿墙** — ADR-016 §九:334
- 冻结 / 不冻结的差异 = **缺失的积分次数**,须**显式记录**,**不得用 AC 断言二者相等** — ADR-016 §九:333
- 冻结判据**只用 sim 量**(`d2` / `in_combat`)—— **「离屏」不是判据** — ADR-016 §九(2026-09-17 修订)

**POI 状态(ADR-021)**
- POI **定义 = 派生态**(位置 / 守卫 / 类型,烘焙逻辑层,加载期重建,**不进流**)/ **状态 = 模拟态**(进世界流) — ADR-021 §Decision(①–⑤ 正文 :95-140 本轮直读)
- **所有者 + 唯一写者 = 系统 6**;写经 `IEventSink.Append`,**主机唯一执行** — 同上(②)
- 新 Kind `PoiStateChanged{ poi_id, new_state }`,**两字段均整数枚举**;`Patient = PatientId.None`(**不污染 `max(patient_id)` 高水位**) —— `PoiState` 具体值**刻意归 6 的 GDD** — 同上(③)
- 有界性 **≤ |POI| × |STATE|**(补 ADR-009 §六,扩展而非重写) — 同上(④)
- **52 的 `spawn_anchor` 抽池不得依赖动态状态**(否则抽池变成状态的函数,破坏确定性抽池前提) — ADR-021 §Decision:124

### Forbidden Approaches

| 禁 | 出处 | 理由 |
|---|---|---|
| **AI 决策读表现态连续位置** | ADR-016 §三:213, 216 | 第二 QoS 到达时序不确定 ⇒ 决策变成**非确定性函数**;高频连续位置仅供表现层平滑 |
| **运行期引入任何第三方行为树 / 寻路库** | ADR-016 §四:253 | **编辑器期可视化工具亦已于 2026-09-17 裁定砍掉** ⇒ AI 侧零第三方、零工具 |
| **「AI 整体住表现态」** | ADR-016 §Alt:367 | 本项目反复拒绝的**静默失败**模式(决策不可重放) |
| **sim 寻路用 NavMesh / PhysX** | ADR-016 §Alt:372 | NavMesh **仅驱动表现态位移**(ADR-015 §五 口径) |
| **敌人决策直读玩家表现态位置** | ADR-016 §Alt:402 | 否决 |
| **以 ADR-017 的修订直接改判 DOTS 复评门** | ADR-017 §三 | 触发后**须另开 ADR**;触发前表现层同样不引入 |

### Performance Guardrails

- 动态建造下的 **NavMesh tile 失效重烘 = 表现层预算**(非 sim 面);P0 可用粗化策略兜住 — ADR-016 §五:276-278
- **ADR-017 复评门阈值(2026-09-20 已裁)**:同场**表现层**实体 ≥ 100 **或** 表现层帧时间 ≥ 8 ms / 16.6 ms(≈48%)——
  ⚠️ 计的是**表现层实体,不是 sim 病人数**;**CAP 24 ⇒ 门在 sim 侧永不触发**(24 < 100)
  — 源:`technical-preferences.md` §DOTS 复评门
- 感知事件的**频率上界 = tick 频率**(见上)—— 这是 ADR-009 世界流有界性论证的前提,**归并算符一旦被移除,上界即退化为帧率,论证静默失效** — ADR-020 §四:291

---

## Presentation Layer Rules

*Applies to: UI 双栈 · 音频 · 遥测读数器 · 控制器与相机 · 拟物呈现*

### Required Patterns

**UI 双栈(ADR-013)**
- **UI Toolkit 为主(平面拟物 UI)+ UGUI 补 world-space / XR**;**VR 急救 = UGUI world canvas,必须 World Space**(Overlay 不参与立体,F3) — ADR-013 §一:130
- **42 只渲染,永不持有游戏状态**(§9 C3);**「体征 → 呈现形态」映射表归 8** — ADR-013 §三:175
- `PresentationDtoGuard` **须递归**(`List<DTO>` 元素、私有 / 继承字段)—— **仅扫顶层会漏** — ADR-013 §三:181
- z 序口径(2026-09-17 就地修订):42 拥有的是**两张域内序表**(覆盖层内 / 相机内),**各自域内不得撞号**;`sortingOrder` 与 UI Toolkit 的 z **不可比** — ADR-013 §一:139-142
- 拟物视觉 = **自建 USS 元件库**(纸纹 / 墨迹 / 卷轴九宫格)+ UXML 组合;UGUI 侧语义对齐
- 无障碍三钩子:`accessibilityNode` 命名约定(⚠️ F8 post-cutoff **须 spike**)/ 文本缩放须**自建 USS 主题变量层**(引擎无自动支持)/ 焦点可见样式**须非纯色 —— 如墨色加深** — ADR-013 §六:216, 217, 218
- **对比度是「实现归 42、阈值归 49」的例外**:任何主题 / 纹理档下均须满足 — ADR-013 §六:230
- **两栈共用同一 EventSystem**;**焦点单栈门** = 同一时刻仅一栈接收导航意图流 — ADR-013 裁定 ①/承 §9
- ⚠️ **RC-1 未结案**:`adr-013:203` 铁律「**不重复实现焦点算法**」与 `:432` 回退「spike 不达预期则自实现」
  **相互抵触** —— spike 越可能失败,矛盾越必被触发(本条**不得呈现为已生效铁律**,见「Open Items」A)

**音频(ADR-018)**
- **44 与 42 同构 —— 只触发 / 只渲染,永不持有游戏状态**;载荷 = `AudioCueDto`(整数语义,**无 `disease_id`**) — ADR-018 §Decision:160、§一:174
- 契约程序集字段**一律用原语**(`int3` / `int`);引用引擎类型即**违反 AC-44-B1** — ADR-018 §二:218
- 混音拓扑**单一定义**:Master / Music / Ambience / Voice / SFX / Stethoscope / UICue;须有 **Aux / Reverb send 拓扑**(室内 / 室外 / 洞窟预设)—— 否则快照只能改增益,**改不了混响** — ADR-018 §三:233-234
- **快照切换一律由「玩家行为 / 场景状态」驱动,绝不用于播报病人状态变化** — ADR-018 §三:249
- **听诊器**:`Stethoscope` 总线暴露接触噪声底 + 信噪比;`READ_FLOOR_MIN > 0` ⇒ **背景噪声永不为零(禁静音路径)** — ADR-018 §四:265
- **AC-44-08 医学准确性**:细湿啰音**不得实现为连续水声**(医学受众第一个出戏点) — ADR-018 §四:274
- **AC-44-10**:呼吸层的连续变化必须可用,但**不得用「突变音效」标记阈值跨越** —— 「病人转危」必须是**玩家自己听出来的** — ADR-018 §六:356-357
- **音乐三闸**:G1 **禁帧对齐**(乐层可听输出不得在 `EncounterStarted/Ended` 到达后立刻起变,切换须整体落在交叉淡入淡出区间内,下界 `MUSIC_XFADE_MIN_MS`)/ G2 **去标注盲测**(受试者不得从音乐指出「遭遇开始的哪一帧」,能指出即回炉)/ G3 **本地设备侧触发**(27 **永不直接点歌**;「昏迷后静默」与遭遇开始同一条通道) — ADR-018 §六:368, 369, 370
- 联机音频:**各设备按本机技能档**(2026-09-18 修订 —— 冻结在主机档与 `game-concept.md:124` 直接冲突 + 制造反向激励);**不做语音通道**;远端空间化复用 ADR-001 第二 QoS,**不新增通道** — ADR-018 §五:296
- **AC-44-12 视觉替代**:承担信息的 cue 必须有视觉替代 —— **信息不得仅存在于音频** — ADR-018 §八:396-397
- VR 音频**推 P1b**(2026-09-18 订正,**与 ADR-013 不同批**) — ADR-018 §七:377

**遥测(ADR-019)**
- **回放即完整数据记录** ⇒ **51 是读数器,不是采集器**;「埋点」在本项目**是冗余** — ADR-019 §一:144
- **51 住边界层 —— 不进 sim 程序集(不污染门 A),不写三流**(只读消费者,**不成为第四个 `IEventSink` 写入者**) — ADR-019 §三:183
- **AC-19-01(BLOCKING)** 判断层指标均可从既有事件流**重算,零新埋点**;**AC-19-02(BLOCKING)** 构建 + 运行期**无任何网络上报**,接口层**无 `Upload` / `Send` / `Post`**;**AC-19-07** **无写回 `assets/data/` 的代码路径** — 源:`technical-preferences.md` ADR-019 条目(AC 号;**ADR 内行号本轮未取**)
- **51 不得成为「替用户调数值」的替身** —— 数值用户自己调 — ADR-019 §六:245

**控制器与相机(ADR-020)**
- 移动 = **`CharacterController`(kinematic,不参与 PhysX 求解)** — ADR-020 §一:185(AC-20-01)
- 相机 = **自建机位**(`ICameraRig`),**不引入 Cinemachine`;否决理由:VR 侧用不上,引入它给 VR 带来一条**必须绕开的旁路** — ADR-020 §二:202, 224
- 视角双路径:平面 = 第三人称越肩;VR = 独立第一人称(不承担开放世界移动),实现推 P1a
- **玩家位移 = 纯表现态**:**连续位置 / 速度 / 朝向永不写流**,在 sim 中的唯一投影 = **跨格世界流事件** — ADR-020 §四:275(AC-20-03 BLOCKING)
- **`Append` 权 = 主机唯一**(客户端经第二 QoS 上行其格;`ActorCellEntered` **不在可靠通道**) — ADR-020 §四:299 · Amendment B:313(**不改 §四 核心**)
- **相机是表现层 —— 只读,永不持有游戏状态**(与 42 / 44 三件套同构) — ADR-020 §五:322
- **VR 模式禁用任何镜头效果**(VR 舒适度优先于呈现;`VRComfort` 快照承载) — ADR-020 §六:341
- **镜头效果不得用于「报」状态** —— 与 ADR-018 §六 无提示音铁律**同源**(同一铁律的视觉侧),只允许行为反馈 — ADR-020 §六:342
- `AudioListener` 单挂点:平面 = 主相机,VR = 头显(AC-20-10)—— 结清 `adr-018:295` 留白

### Forbidden Approaches

| 禁 | 出处 | 理由 |
|---|---|---|
| **sting / jingle / ducking / 素材切换「报」状态**(AC-44-09 BLOCKING) | ADR-018 §三:254 · §六:313 | 音频触发源**必须 ∈ 行为反馈白名单**;声明为 `VitalsCrossed` = **构建失败**(`:353`) |
| **成就式正反馈音绕过黑名单** | ADR-018 §六:374 | AC-44-09 白名单断言把**「乐层」作为独立类别**校验 |
| **Cinemachine / 任何第三方相机工具** | ADR-020 §二 | 「官方包」不是零第三方取向的豁免 |
| **双 EventSystem / 双输入模块** | ADR-013 | 两栈**必须**共用同一 EventSystem |
| **Unity Analytics / UGS / Firebase / 崩溃上报** | ADR-019 §二:172 | `docs/engine-reference/unity/` 对其**零覆盖** ⇒ 无权威件可依,**不作承诺** |
| **日后以「小改动」在既有 ADR 上打补丁引入出厂数据** | ADR-019 §四:229-230 | **§四 即失效,必须另开 ADR** |
| **split-screen 时沿用「单总线」而不另开 ADR** | ADR-018 §五:299 | 该约束届时真实成立 ⇒ **须另开 ADR 登记例外** |
| **表现态连续位置参与任何 sim 决策** | ADR-020 §四:293(引 `adr-016:187-190`) | 同 AI 侧禁令的对称面 |

### Performance Guardrails

- **90 fps / 11.1 ms 是 VR 模式的硬性预算**(仅急救动作小游戏);⚠️ `docs/engine-reference/` **无 OpenXR / XR 模块件**可核 ⇒ 该数值只有偏好件背书(如实记,**不因此降级**)
- **VR 不承担开放世界移动**(晕动症);站定式操作 —— 见 `technical-preferences.md` §Platform Notes
- 相机效果与音频提示**同受「不得报状态」约束**,故二者均不在「状态播报」的性能路径上,不占 sim 帧预算 — ADR-020 §六 · ADR-018 §六

---

## Tooling Layer Rules

*Applies to: 关卡工具 · Kind 生成器 · 编辑期一致性检查(不进构建)*

### Required Patterns

**关卡工具(ADR-022 · Tooling 层首条)**
- 三条定性:**运行期零存在** / **不计入 P0 的 31 项** / **门 A 不约束**(因其**不进构建**,程序集 `tools/level/`,`UnityEngine` / UnityEditor 引用自由) — ADR-022 §一:191-195
- **唯一作者**:逻辑层全部整数数据**只能由本工具编写并导出** — ADR-022 §二:205-206
- 第三方(GAIA / MapMagic 2)**只进视觉层,绝不自动导出逻辑层** — ADR-022 §二:207
- 导出契约 = `world_{geometry,ecozones,poi,resources,buildslots,nav_{chunk},terrain}.json`
  → ADR-014 两阶段烘焙 → `world_*.cooked`(`data-core` 预载;`ConfigVersion` 由内容哈希派生) — ADR-022 §三:215-222
- **六项一致性检查 C1–C6,CI 可跑**,且工具对其中**硬失败项负执行责任** — ADR-022 §Decision:122
  · **硬失败 = C2 可走性同源 / C4 多边形合法性 / C5 边界单一源 / C6 切片完整性**
  · **告警 = C1 两层漂移 / C3 导航格↔NavMesh**(漂移只影响表现,**不退化为构建失败**,与 `ADR-015 §Risks` 同口径)
  — 源:`technical-preferences.md` ADR-022 条目(分级)
- **导航格按 chunk 切片**(`world_nav_{chunk}.json`)—— 结清 `O-6-12` 的工具侧;ADR-014 §五 的「首次 `Step` 前常驻」只约束小体量几何数据,**不约束导航格** — ADR-022 ⑤ / `ADR-015 §四` 补注
- **与 6 的边界锁死**:工具产出**派生态输入**(ADR-021 的「定义」侧),6 拥有**运行期状态**(「状态」侧),**二者不重叠** — ADR-022 ⑥

**Kind 生成器(ADR-024 · 编辑期 .NET 工具,与 Tooling 层同构、不进构建)**
- 唯一登记真源 = `entities.yaml` 的 `SimEvent.Kind.*` — ADR-024 §①:118
- ADR-009 §三 就地降级为**世界流路由注记**,**禁从散文解析** — ADR-024 §②:124
- **Amendment 追加通道(F–L)退役**,新 `Kind` 追加改道 registry(但 **F–L 历史文本不改写** —— 它们是事实记录) — ADR-024 §③:130, 136
- 生成器 `tools/kindgen/` → `src/Sim/StreamRouting.g.cs`;断言 **A1 唯一流别 / A2 载荷 ∈ 整数域 / A3 无重名 / A4 author 必填 / A5 双向差集归零**,任一失败 = 构建失败,**必须 `throw`,禁 `Debug.Assert`** — ADR-024 §⑤:145 · §Validation V-1
- `random-events.md` §Tuning Knobs 的拒绝表(**自陈 17,实测 18 谓词**)**由 ADR-014 阶段 2 校验器执行**,生成器不重复实现 — ADR-024 §⑥:161

### Forbidden Approaches

| 禁 | 出处 | 理由 |
|---|---|---|
| **任何以 seed 生成地形 / 多边形的代码** | ADR-022 §二:210 | 「**任何…都是回归**」—— 世界 = 手工烘焙固定世界(ADR-015 裁定 ①) |
| **Tooling 层的任何东西进构建** | ADR-022 §一:191-195 | 运行期零存在是其存在的前提 |
| **用 `Debug.Assert` 代替显式 `throw`** | ADR-022 §四:227-228 · ADR-024 §Validation V-1 | 静默通过 = 一致性检查失去执法体(全案元规则) |
| **手改 `StreamRouting.g.cs`** | ADR-024 §Decision ⑤ | 生成物;改 registry 再重生成 |
| **让 kindgen 顺手实现拒绝表校验** | ADR-024 §Decision ⑥ | 两处执行 = 两处分叉 |


---

## Global Rules (All Layers)

### Naming Conventions

源:`.claude/docs/technical-preferences.md` §Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `PlayerController` |
| Public fields / properties | PascalCase | `MoveSpeed` |
| Private fields | `_camelCase` | `_moveSpeed` |
| 局部变量与参数 | camelCase | `moveSpeed` |
| Methods | PascalCase | `TakeDamage()` |
| C# events | PascalCase + `Action<T>` | `public event Action<DamageInfo> DamageTaken;` |
| Files | PascalCase,与类名一致 | `PlayerController.cs` |
| Scenes / Prefabs | PascalCase | `MedBay.unity`、`Patient.prefab` |
| Constants | PascalCase 或 UPPER_SNAKE_CASE | `TICK_SECONDS` |
| 作者态数据文件 | `assets/data/[system]_[name].json` | `ai_enemy.json`(ADR-014 裁定 ①) |

### Performance Budgets

源:`.claude/docs/technical-preferences.md` §Performance Budgets
⚠️ **该节自标「状态:临时值 —— 待确定最低目标硬件后定稿」**,下表除标注 ✅ 已裁者外均为临时值。

| Target | Value | 状态 |
|--------|-------|------|
| Framerate(平面) | 60 fps | 临时 |
| Frame budget(平面) | 16.6 ms | 临时 |
| Framerate(VR) | **90 fps** | **硬性**(仅急救动作小游戏) |
| Frame budget(VR) | 11.1 ms | 硬性 |
| Draw calls | 待定 | 需先定美术密度 |
| Memory ceiling | 待定 | 同上 |
| `L_input`(急救输入延迟) | **< 50 ms** | 已裁 —— **只测预表现路径**(ADR-011 / AC-10-08) |
| `TICK_SECONDS` | **0.05**(= 20 Hz) | ✅ **已裁**(OQ-25-8,2026-09-20) |
| `L_eval`(主机求值节奏) | ≤ `TICK_PERIOD` = 50 ms | ✅ 已裁 —— **与 `L_input` 同量级但口径不同,禁合并成"端到端 <50 ms"** |
| 同场被模拟病人数 | **`PATIENT_APPEARANCE_CAP = 24`** | ✅ 已裁(OQ-8,2026-09-20)—— **硬界,超界拒收** |

### Approved Libraries / Addons

**⚠️ 当前已批准集 = 空。** 以下全部**未获准**,候选状态不改:
Gaia Pro / MapMagic 2(**仅编辑期工具**,ADR-015)、Voxel Play(**不采纳**,ADR-015)、
行为树第三方工具(**不采纳**,ADR-016 §四 2026-09-17 修订)、DOTS/Entities
(**sim 侧结构性排除**,ADR-017)、FMOD / Wwise 等音频中间件(**不引入**,ADR-018)、
Unity Analytics / UGS / 崩溃上报(**不引入**,ADR-019)、Cinemachine(**不引入**,ADR-020)、
Netcode for GameObjects **或** Photon Fusion(**二选一未定**,ADR-001:P0 先立 pipe 抽象,
库选型推迟到 P1b 前一次 swap 评审)、Ink / Yarn Spinner(**未裁**)。
Newtonsoft Json(**仅作词法器**,ADR-014 裁定 ③)。

> **家规提醒:「官方包」不是零第三方取向的豁免** —— DOTS 亦为官方包,已由 ADR-017 结构性排除。

### Forbidden APIs(Unity 6.3 LTS)

源:`docs/engine-reference/unity/deprecated-apis.md`(Last verified 2026-02-13)

| 禁用 | 替代 | 本项目附加口径 |
|------|------|---------------|
| `Input.GetKey/GetKeyDown/GetMouseButton/GetAxis/mousePosition` | New Input System(`InputAction`) | **同 ADR-011 裁定 ①**,无冲突 |
| Legacy Input Manager | `com.unity.inputsystem` | 同上 |
| `ComponentSystem` / `JobComponentSystem` / `GameObjectEntity` / `ComponentDataFromEntity<T>` | `ISystem` / `IJobEntity` / pure ECS / `ComponentLookup<T>` | **本项目 moot —— sim 侧禁引 `Unity.Entities`(ADR-017 §二);表现层复评门触发前同样不引入** |
| `CommandBuffer.DrawMesh()` / `OnPreRender()` / `OnPostRender()` / `Camera.SetReplacementShader()` | RenderGraph API / `RenderPipelineManager` callbacks | ⚠️ **ADR-023 ⑧:P0 零 custom Renderer Feature** ⇒ 整类渲染定制在 P0 均**不该写**,触发条款才另开 ADR |
| `Resources.Load()` / 同步资源加载 | `Addressables.LoadAssetAsync()` | **同 ADR-014 §五**;⚠️ 另须守 ADR-023 ⑤ 拆序六步(`UnloadSceneAsync` **不销毁** `InstantiateAsync` 产物) |
| `WWW` / `Application.LoadLevel()` | `UnityWebRequest` / `SceneManager.LoadScene()` | ⚠️ ADR-023 ① 进一步要求 **additive only,永不用 `LoadSceneMode.Single`** |
| Legacy Particle System | Visual Effect Graph | 未与任何 ADR 冲突 |
| Legacy Animation component / `Animation.Play()` | Animator Controller / `Animator.Play()` | 未与任何 ADR 冲突 |
| `Physics.RaycastAll()` | `Physics.RaycastNonAlloc()` | GC 纪律,与 ADR-017 §四「无 GC 抖动」同向 |
| `Rigidbody.velocity` 直写 | `Rigidbody.AddForce()` | ⚠️ 玩家移动**不走 Rigidbody** —— `CharacterController` kinematic,不参与 PhysX 求解(ADR-020 ①) |

**⚠️ `Canvas`(UGUI)与 `Text` / `Image` 组件在 `deprecated-apis.md` 被列为「替代 = UI Toolkit」,
但本项目的裁决与之不同**:ADR-013 裁定 ① 明确 **UI Toolkit 为主 + UGUI 补 world-space / XR**,
且 **VR 急救必须 World Space(= UGUI)**。⇒ **UGUI 不是本项目的禁用项**,只在平面拟物 UI 上禁用。
该条差异以 **ADR-013 为准**,`deprecated-apis.md` 的措辞是通用建议。

### Deviations from generic Unity 6 best practice

源:`docs/engine-reference/unity/current-best-practices.md` vs 本项目 ADR。
**编程时若照该文件推荐做,会直接违反本项目的 Accepted 裁决 —— 以下五条必读:**

| `current-best-practices.md` 推荐 | 本项目裁决 | 出处 |
|---|---|---|
| Use Burst Compiler + Jobs System | **sim 侧永不上;表现层复评门触发前同样不引入** | ADR-017 |
| Use `NativeContainer`(Jobs 内) | **门 A `"noEngineReferences": true` ⇒ `Sim` 引用集不含引擎程序集** | ADR-017 §二 / ADR-025 |
| Use Netcode for GameObjects(Official) | **未选定**;P0 只立 `IReplayPipe` 抽象,库 P1b 前 swap 评审 | ADR-001 |
| Use UI Toolkit for Runtime UI | **双栈**:UI Toolkit 主 + UGUI 补 world-space / XR | ADR-013 |
| Use RenderGraph API for custom passes | **P0 零 custom Renderer Feature**;须按新签名另开 ADR 才可引入 | ADR-023 ⑧ |
| Use Source Generators for serialization | **存档与事件流禁 `float`,且 `Fix` 不可经 Unity 内置序列化器承载** ⇒ 须自定义二进制编码器 | ADR-006 D-21-18 / ADR-010 |

### Cross-Cutting Constraints

**以下六条是全案重复度最高的不变量,逐条列出而非只出现一次:**

1. **呈现层只渲染,永不持有游戏状态** —— 42 UI / 44 音频 / 20 相机 **三件套同构**。
   源:ADR-013 §9 C3 · ADR-018 §一 · ADR-020 §五。
2. **`disease_id` 与任何诊断语义不得进呈现层 DTO** —— 落地 = `PresentationDtoGuard`
   **递归**反射扫描(非 grep)。源:ADR-013 · AC-37-15。
3. **一切模拟数学住整数定点域**(int64 / Q16.16);**边界唯一解析入口 = `FixParse`**
   (拒浮点字面量);**单一舍入模式 `ROUND_HALF_AWAY_FROM_ZERO`,禁 `Math.Round` 默认 ties-to-even**。
   源:ADR-005 · ADR-006。
4. **可重建性三源不变量** —— 任何进 sim 的量,其全部输入必须 ∈
   {事件流, 版本化烘焙数据, 二者的纯函数}。**第四来源(表现态位置 / 墙钟 /
   `UnityEngine.Random` / 未版本化场景几何)= 静默不可重建通道,禁**。源:ADR-016 §一。
5. **状态进流的唯一通道 = `IEventSink.Append`,且主机唯一执行 `Step` / `CatchUp`** ——
   客户端**不直接写流**,上行其意图。路由 = 按 `Kind` 的纯函数白名单,
   **真源 = `entities.yaml`**(ADR-024 ①);**列表外构建期拒绝**。
   跨流全序键 = `(Tick, StreamPriority, Patient, Seq)`(ADR-008)。
6. **不得替用户拍数值** —— 本件列出的公式、变量表、调参旋钮是给数值轮的输入;
   **定值冻结归用户**(承项目家规)。凡 `SAT_*` / 曲线参数 / 半衰期 / 权重一类,
   本件**只登记旋钮与范围,不给答案**。

**执法体的统一形态(全案元规则)**:约束一律落成**构建期断言**,不落成散文。
源:`architecture-review-2026-09-20.md` §11 结论 4 —— 该报告发现 RC-3 与 RC-5
是**同一形状的错误**:把「我希望编译器挡的」写成「编译器挡的」。

---

## Open Items Blocking Rule Promulgation

**本节的含义**:以下规则**已裁定但尚未可 promulgate** —— 或因 ADR 内部矛盾(提取哪条都不对),
或因论据被推翻(结论保留、理由待换),或因执行体不存在。**编程时遇到本节的条目,
不要按「已定」处理,须先结案。**

### A. 源自 `/architecture-review` 2026-09-20 §9 的 8 条 RC

> **✅ 2026-09-21 全部结案 —— 本节 A 表此后不再有效,勿据此判断未决状态。**
> 由 `/architecture-review` 复跑实测,报告 = `docs/architecture/architecture-review-2026-09-21.md` §3.1。
> **对本件的实际影响**:① RC-1 的「两条规则都提取了但不互斥」→ **已互斥**(铁律作用域收窄到
> **42 的对外契约面**,`adr-013:204-207`);② RC-2 的 `[未验证前置:S<n>]` **标记本身仍有效**
> (S1–S7 依旧一条未跑),但**根因已除** —— 状态串已归一 `Accepted`(`adr-023:5`),
> `create-control-manifest` 的字面过滤不再静默丢弃该件;③ RC-3 / RC-5 的**唯一执法体 = 引用集白名单断言**
> 已由 `adr-017:170`(论据订正)+ `adr-025:116-119`(断言口径)+ **裁定 ④**(全清单外推)三方对齐;
> ④ RC-6 的「无执法体」→ **已补**(`adr-023:138-141` 扫描增列相机 / `AudioListener`);本件 B 节的
> 「ADR-023 ② 构建期扫描归属未定」一条**仍开放**(扫描**判据**有了,扫描**工具**尚无 —— 属实现轮)。
> ⑤ RC-4 的 F7 **不再是矩阵前置**(降级为表示选择);⑥ RC-7 的「数据管线段补一条待办」**已补**
> (`adr-014:173-176` 两 pin + `:366` 负向夹具)。**本注为追加注,下表原文原样保留。**

| # | ADR | 症状 | 对本件的影响 |
|---|-----|------|-------------|
| **RC-1** 🔴 | ADR-013 | `adr-013:203` 铁律「不重复实现焦点算法」vs `:432` 回退「不达预期则自实现」——**成对失效**:spike 越可能失败,矛盾越必被触发 | 本件 Cross-Cutting #1 与 Focus 相关规则**两条都提取了但不互斥**;须用户裁 RC-1 option 1(三行文本,非新裁决) |
| **RC-2** 🟠 | ADR-023 | 状态串 `Accepted(附条件,见下)` 打断 exact-match 工具链;效力口径 `:53`/`:228` vs `:10` 自相矛盾 | 本件所有 `[未验证前置:S<n>]` 标记的**根因**;归一 `Accepted` 后措辞须复核 |
| **RC-3** 🟠 | ADR-017 | `adr-017:165/168` 的 `noEngineReferences` **机制论据很可能为假**(该旗标门控引擎模块,不门控 UPM 包程序集) | **结论不动**;本件「程序集清单」段凡引该论据者,**唯一执法体 = 引用集白名单断言** |
| **RC-4** 🟠 | ADR-012 | F7 的 IL2CPP 有符号溢出 UB —— 改住 `ulong`/`unchecked` 后**结构性不可能**,原 BLOCKING spike 塌缩为表示选择 | 本件「CI 确定性门」最后一条(F7 spike 先行)**待降级**;矩阵照跑 |
| **RC-5** 🟡 | ADR-025 | 「`Sim` 引用集**恰 = {BCL, Sim.Contracts}**」是**断言非清单事实** —— 与 RC-3 同形状错误 | 本件已按裁定 ④ 的构建期断言口径写;措辞仍待 ADR 侧对齐 |
| **RC-6** 🟡 | ADR-023 ②/③ | 「`World.unity` 零 gameplay 对象」的扫描判据**窄于** ③ 立的不变量 ⇒ 无守护 | 本件 Core 段该条**标为无执法体**(见下方 B) |
| **RC-7** 🟡 | ADR-014 | 词法器未 pin Newtonsoft 的 `DateParseHandling` / `FloatParseHandling`(⚠️ 部分自防已在件内) | 本件「数据管线」段须补一条待办,**非阻塞** |
| **RC-8** 🟢 | `architecture.md` §5.4 | 分类表一处行标签 ID 前缀标错 | **本轮已就地修**,不影响本件 |

### B. 提取过程中发现的「缺执法体 / 缺出处」项

- **ADR-023 的 ② 构建期扫描**:判据存在(`World.unity` 不得含 gameplay GameObject),
  但扫描工具**归属未定**(Tooling 层?`unity-specialist` 复核称须新写)⇒ 规则可 promulgate,
  执法体不可 —— 与 RC-6 叠加。
- **ADR-024 生成器 `tools/kindgen/` 本体未写**,且既有 24 支 Kind 的 `stream:` / `author:` /
  `payload_schema:` 三必填字段回填未做 ⇒ **A1…A5 断言目前无可执行形态**。
- **`Production Stage: VR` 的 90 fps 硬约束**在 `technical-preferences.md` 为「硬性」,
  但 `docs/engine-reference/` **无 OpenXR / XR 模块件**可核 ⇒ 该数值只有偏好件背书,
  无引擎权威件(如实记,**不因此降级该约束**)。
- **`adr_divergence` 字面 vs 语义差异(候选 RC-9)**:门判据「no open `adr_divergence`」按
  **字段存在性**扫描会得 2(`TR-case-036` / `TR-interaction-015`),但两条的字段文本**本身就是
  结案 note**(均已 `blocked_by` 实现轮 + `revised: 2026-09-20`)⇒ 「开放数 = 0」在语义上真、
  字面上假。属 RC-2/RC-5 同族的「散文断言 vs 机械判据」问题,待裁是否改 registry schema。

### C. 待用户裁的技术侧事项(本件**不代拍**)

1. Foundation 门「zero Foundation layer gaps」为绿的两条路径 —— ① 纯登记回写(修 `TR-itemdb-031`
   / `TR-skill-008` 的登记态);③④ `TR-concept-003/004` 是**范围声明**,结构上不由 ADR 承接 ⇒
   要绿须新增 `no-adr-by-design` 状态 = **gate-check 规格变更**(用户裁)。
   **✅ 本条第 2 路径已于同日获裁并落地**(`no-adr-by-design` ◆ 第四态 + `gate-check/SKILL.md` 判据注,
   2026-09-21;见 `traceability-index.md` 变更历史)⇒ **QQ-07 就此全结**;残 ① 路径归实现期,非待裁项。
2. `deprecated-apis.md:28` 把 `Canvas`(UGUI)列为弃用 vs **ADR-013 裁定双栈** —— 本件以 ADR-013
   为准登记为 deviation(见上),**是否回写 engine-reference** 归用户裁。
3. Required ADRs **#4(系统 30 定点算术)/ #5(13 的写路径归属)** 未兑现,`architecture.md`
   自陈非开工阻塞 ⇒ 是否阻塞 Pre-Production 入口,由 TD 判词 + 用户裁定。
