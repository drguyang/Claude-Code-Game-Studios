# ADR-010: 7a 持久化与存档格式 (Persistence & Save Format)

## Status

Accepted

> **2026-09-15 起草并转 Accepted。** 四条用户裁定已锁:① **全二进制 codec**(不用 JSON / PlayerPrefs);
> ② **校验和 + 自动回退**;③ **定期 checkpoint(默认 5 分钟)+ 退出保存 + 7b 手动槽**;
> ④ **版本号 + 迁移脚本**。
> 引擎侧经 unity-specialist lean 复核(2026-09-15):**无引擎侧 blocker**;结论并入 §Risks。
> 依赖本 ADR 的 7a 实现与 9 / 52 / 37 的存档类 AC 解除 `[BLOCKING]`。
> 独立评审由下一轮 `/architecture-review` 进行。

## Date

2026-09-15

## Last Verified

2026-09-15

## Decision Makers

dr_guyang(用户 · **2026-09-15 四条裁定,均照准**)· technical-director(起草与裁决)
· network-programmer(45 侧同步边界)· unity-specialist(引擎复核,2026-09-15)
· 系统 7a 持久化服务(无 GDD —— 本 ADR 即其权威件)
  > ⚠️ **2026-09-21 历史注**:「无 GDD」是本 ADR 撰写时(2026-09-15)的状态;7a 后已立
  > `persistence-service.md`(2026-09-17,其 `:8` 自述「7a **无前置** GDD,ADR-010 即其权威件」——
  > 与本 ADR 的关系**未变**,变的只是「此前无文档」这一事实状态)。本注不改裁决归属。

## Summary

系统 7a「持久化服务」是**全案唯一无 GDD 的 Core 系统**,却已被 ADR-005 / 006 / 007 / 008 / 009
**五份 ADR 集体委派义务**(存档头契约 · 三流序列化 · 折叠 · 快照 · `ItemInstanceId.Next()` 机制 ·
迁移协议)—— **它自己没有权威件**。这些义务散落各处、部分互相冲突(WorldSeed 载体口径、
配置版本号归属、折叠谓词条件分散),实现者无单一出处可循。
> ⚠️ **2026-09-21 历史注**:上段「全案唯一无 GDD 的 Core 系统 / 它自己没有权威件」是本 ADR 的
> Problem Statement 原文,现陈旧 —— 7a 后已立 `persistence-service.md`(2026-09-17)。此句是
> **当初立本 ADR 的动机**,按惯例不删,追加注补;裁决归属不变。
本 ADR 裁决:**全二进制存档格式**(头部 + 三逻辑流 + 快照)· **原子写 + 校验和自动回退** ·
**定期 checkpoint + 退出保存 + 7b 手动槽** · **版本号 + 迁移脚本**,并把全部既有委派收敛为
**单一义务清单**(§三 义务汇总表),一处不留、一处分叉。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (URP) |
| **Domain** | Core(持久化 / IO / 序列化) |
| **Knowledge Risk** | **MEDIUM** —— 原子写 / 后台线程 / 哈希在 IL2CPP 与各平台下的行为需实测(引擎复核标注),但本裁决刻意只用 BCL 稳定 API(`File` / `FileStream` / `SHA256` / UTF8),不依赖 post-cutoff API |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` · `deprecated-apis.md` · `docs/architecture/adr-005-deterministic-sim.md` · `adr-006-fixed-point-boundary-contract.md` · `adr-007-event-authority-and-roll-state.md` · `adr-008-case-event-stream.md` · `adr-009-world-state-event-boundary.md` |
| **Post-Cutoff APIs Used** | **None** —— 刻意不用 `UnityEngine.Hash128`(引擎复核:文档不保证编辑器 vs IL2CPP 玩家逐位一致,须实测);用 BCL `SHA256` |
| **Verification Required** | ① **原子写实测**:`File.Move(tmp, final, overwrite)` 在 Windows / macOS / Linux 下的原子性与异常路径;② **IL2CPP 后台线程写盘实测**(checkpoint 在托管线程写,不卡主线程);③ **黄金字节夹具**:同一存档在编辑器 vs IL2CPP 构建产出**逐位相同**的字节流;④ 校验和覆盖范围单测 |

> **Note**: Knowledge Risk MEDIUM —— 升级引擎版本时须重读本 ADR。

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— 事件流唯一真源 · 折叠规则 · 抽象点)· **ADR-006**(Accepted —— 定点域边界 · 自定义编码器 · 禁 float)· **ADR-007**(Accepted —— `WorldSeed` 归 7a 存档头 · `PatientId.None` 哨兵)· **ADR-008**(Accepted —— 病例流 · 折叠豁免 · `Folded(p)` 条件)· **ADR-009**(Accepted —— 世界流 · 三流口径 · 快照 = 优化非真相)—— 五者均须 Accepted |
| **Enables** | **R-5 / 跨平台确定性 CI 门**(黄金夹具载体 —— 存档字节流是逐位性的可测面)· **7a 的实现** · 7b 存档位 UI · 9 / 52 / 37 的存档契约全部落定 |
| **Blocks** | **系统 7a 持久化的实现**;9 / 52 / 37 的「存档 / 迁移」类 AC(读档重构、`max(patient_id)` 扫并集、病例流回放) |
| **Ordering Note** | 本 ADR 是 **R-5 的前置**(黄金字节夹具定义存档格式,CI 门才有对拍面);**先 Accepted 本 ADR,再写 7a 代码**(ADR-005 §Blocks 已要求)。**不阻塞** 21a / 52 / 9 的既有实现 |

## Context

### Problem Statement

7a 是 `systems-index.md` 中**唯一没有 GDD 的 Core 系统**—— 它本该由一份 ADR 定义全部契约,
现状却是**五份 ADR 各委派一块,义务散落无主**:
> ⚠️ **2026-09-21 历史注**:同 Summary 处注 —— 本段为撰写时状态,`persistence-service.md` 已于
> 2026-09-17 立;义务收敛到本 ADR 单一出处的裁决不变。

- ADR-007 §二:`WorldSeed` 生成 + 存档头持久化 + 跨版本原样保留;
- ADR-005 §Implementation Guidelines 5:病史流终态折叠;
- ADR-008 §六 / §七:`Folded(p)` 谓词(含「无未结案病例」条件)· `max(patient_id)` 重构扫两流并集;
- ADR-006 §Decision 五:`Fix` 自定义编码器(按字段名,禁位置);
- ADR-009 §四 / §六 / §七:`ItemInstanceId.Next()` 机制 · 世界流序列化 · 定期快照 = 优化非真相 · 掉落位置从快照取。

**不裁决的代价**:实现者面对一份没有出处的义务清单,必然出现三类**静默**错误 ——
① `Fix` 被 Unity 内置序列化器承载(ADR-006 §五 禁令,效力归零);② 折叠谓词漏「无未结案病例」条件
⇒ 未结案病例被折叠删掉,病例回放缺环;③ `WorldSeed` 迁移时被改 ⇒ **全案病程与事件流同时作废**
(ADR-007 §二 硬约束)。这三类失败**不崩溃、回放对不上** —— 正是本项目反复拒绝的那一类。

### Current State

- `systems-index.md` §40:`7a | 持久化服务 | Core | P0 | 未开始`。
- 五份 ADR 的委派**散落各 §**,部分冲突(见 §D 交叉点):
  - `random-events.md` 仍写「存档头(**或创建时的一条 SimEvent**)」「暂定 / 待裁」—— **与 ADR-007 已 Accepted 的裁决冲突**;
  - TR-itemdb-032「配置版本号与存档头联动」`suggested_adr: R-7`,而配置版本号归 7a 存档头(ADR-007 §三 / ADR-008 / ADR-009 架构图均已写明);
    > **2026-09-15 ADR-014 补注(R-7 的落点)**:该条现读 `adr: ADR-010 + ADR-014` ——
    > **头部字段归本 ADR §一 / §七,装载动作与 `ConfigVersion` 派生归 ADR-014 §五**
    > (内容哈希派生 u32;启动期比对存档头)。两者相容:本 ADR 只要求「u32 进头部 + 决定后续窗口」。
  - `Folded(p)` 条件分散在 AC-36(plateau / relapse 豁免)与 ADR-008(无未结案病例)—— 无单一出处。

### Constraints

- 不得破坏 ADR-005 的核心裁决:事件流唯一真源 · 整数定点域;存档不得出现 `float`。
- `Fix` 序列化**必须自定义编码器,按字段名编码,禁位置编码**(ADR-006 §五)。
- `WorldSeed` 跨版本**必须原样保留**(ADR-007 §二);生成一次,平台密码学源。
- 对 ADR-001 的两个候选(Netcode for GameObjects / Photon Fusion)都成立。
- 后台线程**不得调 Unity API**(引擎复核)。

### Requirements

- 存档必须能**完整重放三流**(病史 / 病例 / 世界)以重建模拟态
- 存档必须**逐位稳定**:同一存档在任何平台 / 构建产出相同字节流(R-5 黄金夹具前提)
- 存档必须**有界且可论证**(流 + 折叠 + 快照的物理上限)
- 崩溃 / 损坏必须**可恢复**(校验和 + 自动回退,玩家无打断)
- 存档必须**可迁移**:版本升级不丢 `WorldSeed` 与已发生事件
- 序列化形状必须以**一处权威**为准,不得散落各 ADR

## Decision

**裁决:全二进制存档格式(头部 + 三逻辑流 + 快照)· 原子写 + 校验和自动回退 ·
定期 checkpoint + 退出保存 + 7b 手动槽 · 版本号 + 迁移脚本。全部既有委派收敛为单一义务清单。**

### 一、存档文件布局(全二进制 codec)

```
┌───────────────────────────────────────────────┐
│  头部 (SaveHeader)                             │
│  magic "DYJQ" (4B)                             │
│  校验和 SHA256(其后全部字节) (32B) ← 字段位置  │
│  存档版本号 (u32)                               │
│  WorldSeed (u64) · 配置版本号 (u32)             │
│  tick (i64) · 快照偏移 (u64)                   │
├───────────────────────────────────────────────┤
│  病史流 (编码记录序列)                          │
│  病例流 (编码记录序列)                          │
│  世界流 (编码记录序列)                          │
├───────────────────────────────────────────────┤
│  快照段 (表现态位置 · 加载加速 · 优化非真相)     │
├───────────────────────────────────────────────┤
│  分册态段 (39 脉案:分册键 · 两个 tick · 纯呈现量│
│  —— 不进三流但跨存档;⭑ 2026-09-19 义务 13)    │
└───────────────────────────────────────────────┘
```

> 🔁 **2026-11-06 窄加注 —— 头部 `tick` 的不变式(P0 级,不改布局、不加字段)**:
> **档头 `tick` 不得早于事件段的最大 tick**(后面简称「流尾」)。
> - **为什么**:读档按档头 `tick` 定位逻辑时钟(2026-11-04 `238eef15` 起),而
>   `EventStream.Append` 的 BCD-码-3(Tick 非降)以**流尾**为基线 ⇒ 档头一旦低于流尾,
>   读档后**每一笔写**都被拒收:console 刷屏、玩家走不动、sim 零写入;更糟的是下一次写档
>   会把更低的 tick 再写一遍(**坏档自我放大**——盘上实测链:
>   `save_exit.bin.bak` 档头 0 / 流尾 1554 → 下一次存出 `save_3` 档头 179 / 流尾 1554)。
> - **写侧执行体**:`SaveComposer.Compose` 的**唯一组版口**取 `max(调用方 tick, 流尾)`
>   (`SaveComposer.EffectiveHeaderTick`);生产闭包在写前比一次并 `LogWarning`(不静默修正)。
> - **读侧执行体**:`LoadSequencer` 取 `max(档头, 流尾)` 定位时钟并 `LogWarning`;
>   `SaveService.LastLoadedStreamTailTick` 交出流尾这个**独立事实**。
> - **判据**:`save_tick_vs_stream_tail_test`(7 条,含四条负控/正控;证伪轮三变异各自转红)。
> - ⚠️ 本注**不改任何布局字节**,也不改变「档头 tick 正常时原样使用」的口径。

- **全二进制 codec**:不用 JSON / PlayerPrefs(引擎复核:JSON 按 ADR-006 §五 会静默丢 `Fix`;
  BinaryFormatter 已弃用;JsonUtility 不适用 —— 必须手写,且**按字段名 / tag 编码**(proto 风格字段表),
  **禁位置打包**)。
- **序列化纪律**:三流每条事件 = 值 struct(禁 float,经 ADR-006 §五 编码器);`SimEvent`
  **按字段名编码**(`Tick` / `Patient` / `Seq` / `Kind` / `Payload`),不按位置 ——
  历次修正案字段序有变(ADR-007 曾抄成 `{Tick, Patient, Kind, Seq, Payload}`),位置化编码
  会让两种抄本写出两种字节流。
- **显式字节序 + 黄金字节夹具**:现行平台全小端(引擎复核确认「无端序坑」成立),
  但**须显式声明小端并写黄金夹具钉死**(编辑器 vs IL2CPP 产出相同字节) —— R-5 的对拍面。
- **原子写** = 同目录 tmp + `FileStream.Flush(true)` + `File.Move(tmp, final, overwrite: true)`
  (POSIX = rename(2) 原子 · Win = MoveFileEx 同卷原子)。**不用 `File.Replace`**(引擎复核:
  训练数据中 Mono 系 BCL 于 Unix 抛 `NotSupportedException` —— 需实测,倾向弃用)。
- **防呆**:禁写安装目录(写 `Application.persistentDataPath`);Windows 杀软 / OneDrive 锁文件
  ⇒ `IOException` 退避重试。

> **🔁 §一 增补(v1 布局定稿 · 2026-10-24 `TD-persist-03` 修复轮 · 用户四裁定)**
>
> 上图「病史流 / 病例流 / 世界流」三个分块**澄清为逻辑流图示,不是物理分块要求**。
> 用户 2026-10-24 裁定(甲案):**v1 布局 = 单列表**,三流按 `EventStream` 追加序
> (Tick 非降)合为一个事件段。理由:三流在存储上本来就是一个列表
> (`Sim/EventStream.cs:38`),按全序键 `(Tick, StreamPriority, Patient, Seq)` 的语义序
> 已由追加序承载;物理分块会让读侧三分支、写侧重排,零收益。
>
> **v1 实际字节布局**(定稿,`SaveVersion = 1` 不变):
>
> ```
> ┌───────────────────────────────────────────────┐
> │  头部(七字段不变;SnapshotOffset 恒写 0 哨兵)│
> ├───────────────────────────────────────────────┤
> │  事件段:[count:i32][逐事件(长度前缀封装)]    │  ← 三流合一单列表
> ├───────────────────────────────────────────────┤
> │  折叠段:[count:i32][36B 定长行 × count]       │  ← ADR-010 §二 六字段逐字序
> ├───────────────────────────────────────────────┤
> │  (快照段 / 分册态段:留待后续,本批不实现)    │
> └───────────────────────────────────────────────┘
> ```
>
> 四项裁定逐条:
> ① **单列表**(本节开头);② **折叠段为 v1 必备段**——空折叠也是 `count=0` 的 4 字节段,
>    读侧(事件段读尽后)必见 ≥4 字节,否则按损坏拒收;③ **无需头部折叠段偏移字段**——
>    折叠段紧随事件段、count 前缀自描述(债条目 B-1 按「不需要」结案,不是「补字段」);
>    `SnapshotOffset` 仍为快照段预留;④ **`SaveVersion` 仍为 1**——v1 布局从未出货
>    (零生产消费者、零存档文件),布局定稿属 1.0 定稿,bump 会为「只存在于测试夹具的
>    旧布局」造升链,纯仪式零收益;迁移链三分支判定与 ⑦b 升链义务不变。
>
> > 🔁 **2026-10-28 就地加注(ADR-034 实现轮 · 上面 ④ 原文不删)** —— ④「`SaveVersion` 仍为 1」
> > 已被 **`SaveHeader.CurrentSaveVersion = 2`** 取代。理由**不是**推翻它,而是它自己的前提失效了:
> > ④ 的论证只在「**布局未变**」时成立,而 ADR-034 真的改了布局(在折叠段之后追加**载荷段**)。
> > ④ 担心的代价(「为只存在于夹具的旧布局造升链」)**没有发生** —— ADR-034 §⑥ 登记**零迁移**:
> > v1 的载荷字节从未写出,迁移在信息论上不可能,故 `SaveVersion < PayloadSegmentMinVersion`
> > 的档由 `SaveService.Load` 的版本门**单列拒收**,不下沉到迁移链。
> > ⇒ ④ 的**判据**(「布局真变了才 bump」)完好,只是这一次布局真变了。
>
> ⚠️ **折叠段定长位置布局与本节「禁位置打包」的关系**:tag 规则为 `SimEvent` 而设
> (Kind 间字段可选、形状多变);`FoldRow` 六字段恒在、无一可选、次序本就是 ADR-010 §二
> 的位置语义 ⇒ 定长行位置编码是确定性等价选择,黄金字节夹具(`fold_row_codec_test`)
> 按此序钉死。**同批实现落点**:`Gameplay.Boot/FoldRowCodec.cs`(编码)+
> `Gameplay.Boot/SaveComposer.cs`(组版,三流全进档 + 折叠行回写)+
> `Sim.Codec/SaveService.cs`(读侧 ParseBody,折叠段原始字节经 BCL 委托交出)+
> `Sim/EventStream.cs::Restore`(读档恢复:绕 CAP、留去重/单调、Seq 不重发 ——
组最大号经两遍扫描播种接续,承既有发号器语义)。
> 原件 = `production/qa/evidence/impl-td-persist-03-replay-and-body-2026-10-24.md`。

### 二、三流序列化与折叠

- **病史流**:序列化记录序列 + 终态折叠。**折叠谓词单一出处** —— `Folded(p)` 合并三条条件:
  ① 终态(痊愈 / 死亡,AC-36)② plateau / `relapse.*` 豁免(AC-36)③ **无未结案病例**(ADR-008 §六)。
  折叠行 = `(onset, 病种_id, patient_id, patient_seed, outcome, t_end)`(ADR-005 §Implementation Guidelines 5,
  `patient_id` 必留 —— Amendment B)。
- **病例流**(ADR-008):**不折叠**,永久保留(改写史回看 / fires-once / 未结案)。
- **世界流**(ADR-009):**不折叠**;定期快照 = 加载加速 + 表现态位置恢复(**优化非真相** ——
  真相永远是世界流)。
- **`max(patient_id)` 重构**:扫**三流并集**(ADR-006 Amendment E),`PatientId.None` 显式排除;
  重构结果 `next = max + 1` 作为新 authority 的计数器基线(Amendment B)。

### 三、义务汇总表(单一出处 —— 本 ADR 是唯一权威)

| # | 义务 | 来源 | 收敛位置 |
|---|------|------|---------|
| 1 | `WorldSeed` 生成(一次,密码学源)+ 存档头持久化 + 跨版本原样保留 | ADR-007 §二 | 本 ADR §一 头部 |
| 2 | 配置版本号进存档头(52 / 21a 数据联动) | ADR-007 §三 · ADR-008 · ADR-009 | 本 ADR §一 头部 |
| 3 | 三流序列化(病史 / 病例 / 世界)+ 禁 float | ADR-005 · 006 · 008 · 009 | 本 ADR §一 / §二 |
| 4 | 病史流折叠谓词 `Folded(p)`(三条件单一出处) | ADR-005 · 008 · disease AC-36 | 本 ADR §二 |
| 5 | `max(patient_id)` 重构扫三流并集 | ADR-006 Amendment B / E · ADR-008 §七 | 本 ADR §二 |
| 6 | `IIdAuthority.ItemInstanceId.Next()` 机制(计数器 + 高水位) | ADR-009 §三 · TR-itemdb-019 | 本 ADR §五(机制) |
| 7 | 掉落位置从快照取(表现态);掉落清单从世界流重构 | ADR-009 §五 | 本 ADR §一 快照段 |
| 8 | 定期快照 = 优化非真相;迁移时世界流逐位保留 | ADR-009 §六 / §Migration | 本 ADR §一 / §四 |
| 9 | `ItemInstance` 序列化(含 `quality`;不存 `item_key` 引用,存快照) | item-database.md §Dependencies | 本 ADR §五(实现契约) |
| 10 | 容器子实例闭包校验(装配期断言) | item-database.md AC-21a-63 邻接 | 本 ADR §五(实现契约) |
| 11 | **POI 状态进世界流(`PoiStateChanged`)+ 重放时从流重建** | **ADR-021 §三 / §四** | 本 ADR §一 / §二(世界流序列化 / 不折叠已覆盖)|
| 12 | ~~**`structure_id` 高水位**(结构实例共用 `IIdAuthority` 空间,计数器 + 高水位可重构,ADR-010 §五 机制)~~ **→ 🔁 2026-11-02 改判:`structure_id` 走独立空间,不入 `IIdAuthority`**(见下方 Amendment A);**折叠谓词不适用于结构行**(这半条不变) | ~~**modular-building.md 规则四 / 规则七**(2026-09-17)~~ **→ 见 Amendment A** | ~~本 ADR §五(机制,与义务 6 同一 `IIdAuthority`)~~ **→ `StructureInstanceRegistry` 自有计数器**(见 Amendment A) |
| 13 | **分册态段序列化**(39 脉案):分册键 `player_id` · 两个 tick(快照 / 上次重查)· 分册的纯呈现量(页码 / 折叠状态);**痕与排序不在其中**(读时计算)。**分册态不进三流**(39 零 `IEventSink`)但**必须跨存档** | **casebook.md**(2026-09-19 · 39 首轮评审层籍裁定 C) | 本 ADR §一(新增「分册态段」槽) |
| 14 | **技能成长持久化**:`SkillGrown` 落病史流 · `Level` 以 int 承载 · **不独立快照** · 从流重构;**折叠豁免扩一类 Kind(`SkillGrown` 不随病人折叠被抹除)** | **ADR-026 §⑥ / §⑦**(2026-09-23 · 兑现 Required #4 · 结清 `OQ-7a-9`) | 本 ADR §二(折叠谓词加 `SkillGrown` 豁免分支)|
| 15 | **blob 池的写面实现**(`IBlobSink`):载荷字节的**存入** —— 与既有「池生命周期归流实现」口径一致(7a 存档侧 / 45 网络侧各自实现);**不可变性硬义务**:给定 `BlobId` 后其字节内容**不得再改**,违反 ⇒ 已发出的 `PayloadRef` 静默变义 ⇒ **回放分叉**。本接口无法在编译期强制 ⇒ 登记为实现期断言面 | **ADR-029 §②**(2026-10-02 · `IPayloadEncoder` 第七抽象点的兑现件之一) | **ADR-029 §②**(签名:`int Store(ReadOnlySpan<byte>)`;读面 `IBlobPool` 早已在库,写面由本义务补) |

> 义务 1–8 的源 ADR 不变;本 ADR 是**实现的收敛处**。**任何新委派只能追加到本表** ——
> 实现者读这一张表,不读五份 ADR 的散落各节。
> **⭑ 2026-09-19:义务 13(分册态段)由 39 脉案首轮评审层籍裁定 C 追加** —— 39 持三流之外
> 的第四类数据(两个 tick · 痕投影 · 分册),痕与排序读时计算不入段;分册态**不进三流**。
> **⭑ 2026-09-23:义务 14(技能成长持久化)由 ADR-026 追加**(兑现 Required ADR #4)—— 折叠谓词
> `Folded(p)` 须**豁免 `SkillGrown` 行**(案 1),否则 29 的 F-29-4 掉级重放前提落空(`OQ-7a-9` 结案)。
> **⭑ 2026-10-02:义务 15(blob 池写面)由 ADR-029 追加** —— `IBlobSink` 补上此前缺的**写入**契约
> (读面 `IBlobPool` 早已在库)。乙案要求编码器能写入池,而池生命周期仍归流实现(7a / 45)——
> 本义务**不改归属**,只补签名与不可变性硬义务。

#### Amendment A(2026-11-02)—— 义务 12 改判:`structure_id` **独立空间**,不入 `IIdAuthority`

> **Status**:维持 `Accepted`。本 Amendment 只改义务 12 的**机制半边**,折叠谓词半边不动。
> **结清 `TD-building-04`**(该债的解除动作逐字要求「须裁(ADR 级),不是实现动作」——
> 本 Amendment 即那个裁定)。

**裁定**:`structure_id` **不并入 `IIdAuthority` 空间**;`StructureInstanceRegistry` 保留自有
计数器(`_nextStructureId`,起点 1)。义务 12 的原文机制承诺(「共用 `IIdAuthority` 空间,
计数器 + 高水位可重构」)**就此撤回**,其余半边(「折叠谓词不适用于结构行」)**不变**。

**四条依据(逐条可复跑)**:

| # | 依据 | 复算 |
|---|---|---|
| 1 | **实现已如此且更安全** —— 两空间独立 ⇒ item 与 structure **永不撞号** | `grep -c "IIdAuthority" unity/Assets/Sim/World/StructureKinds.cs` ⇒ 仅注释 |
| 2 | **ADR-033 的平移量 `K := N` 建立在「基线占 `1..N`、玩家从 `N+1` 起」之上** —— 并入 `IIdAuthority` 会**推翻其充要性证明**(`Placed.id ≤ N` ⟺ 写于基线诞生之前) | `docs/architecture/adr-033-structure-id-collision-on-load.md` §Decision |
| 3 | **ADR-033 已于 2026-10-28 实现并关闭 `TD-building-02`**(9 条变异全红)—— 为「今天更安全的状态」重开那条链,收益为负 | `production/qa/evidence/impl-adr033-structure-id-remap-2026-10-28.md` |
| 4 | **GDD `inventory-and-items.md:648` 的「与结构行撞号」设想在当前实现下不会发生** —— 该设想本身需一并作废 | `sed -n '648p' design/gdd/inventory-and-items.md` |

**⚠️ 本 Amendment 的诚实边界(不得读成「已解决」)**:

- 它**不解决**「`structure_id` 不可重构」这个**机制**问题 —— 它只是**承认**该机制今天不存在。
  `structure_id` 从事件流重放**可以**重建(`StructureRestorer` 已做),但**高水位**依赖
  `_nextStructureId` 的本地状态 + `Restore` 的局部抬高,**不是** ADR-010 §五 的机制 A。
- **显式的未来触发条件**:若联机需要**跨机结构 id 一致性**(ADR-001 三流复制把 `StructurePlaced`
  发到客户端),本条**重新变成 P0 面债** —— 届时须另开 ADR,不得以本 Amendment 修订改判
  (与 ADR-017 §三 的「触发后另开 ADR」同款纪律)。
- **GDD `:648` 的撞号处理**:本 Amendment 只作废「设想」,**不删**该处的既有实现约束文字 ——
  那是 `modular-building` 的 GDD 回写轮的事(本件不碰 GDD)。

**涟漪**:`TD-building-04` → `closed`;`docs/tech-debt-register.md` 的该条加关闭记录;
`StructureKinds.cs:68` 的字段 doc 加指向本 Amendment 的注。

### 四、损坏恢复(校验和 + 自动回退)

- **校验和**:`SHA256`(BCL,IL2CPP 可用,确定性;MB 级 1–5 ms 足够快 —— 引擎复核)。
  **不用 `UnityEngine.Hash128`**(文档不保证编辑器 vs IL2CPP 玩家逐位一致,须实测)。
  覆盖范围 = checksum 字段之后的**全部字节**(头部其余字段 + 三流 + 快照);
  **字段位 = 置于头部之首**(magic 之后、`SaveVersion` 之前 —— 2026-09-21 回写,
  承 `TR-persist-004` 不一致① · 用户裁定 GDD 为准 · 详 §Implementation Guidelines 6 注)。
- **写入流程(原子 + 双档)**:
  ```
  final ← 存档路径（唯一）
  bak   ← final 的上一好档

  写 checkpoint:
    1. 序列化到内存 buffer（主线程,Step 边界）
    2. 写 tmp 文件（后台线程,flush+rename）
    3. tmp → final（原子替换;若 final 存在先 final → bak）
  ```
- **回退逻辑**:加载 `final` 校验失败 ⇒ 自动读 `bak` ⇒ 成功则继续并提示「已回退到最近检查点」;
  两级都坏 ⇒ 提示损坏。**玩家无打断**(推荐 —— 引擎复核确认可行性)。

> 🔁 **2026-11-03 加注(`TD-persist-16` · 用户裁定「甲」)—— `bak` 的适用面收窄,机制不变**:
>
> 上面写入流程第 3 步「**若 `final` 存在**先 `final → bak`」要求**同一个文件被写第二次**。
> 而「槽位只进不退」真正成立后(`TD-persist-15`,2026-11-03),`save_{N}.bin` **每个槽号一生
> 只被写一次**(写点 = `Checkpoint(NextWritableSeq)`,写成功即推进高水位)⇒ **该分支对玩家槽
> 不可达** ⇒ 玩家槽**没有 `bak`** ⇒ 「加载 `final` 失败 ⇒ 自动读 `bak`」对玩家槽**无宾语**。
>
> **现口径(两条按文件分开)**:
> - **`save_exit.bin`** —— **每会话重写** ⇒ 本节原逻辑**完整成立**,`bak` 回退可用;
> - **玩家槽 `save_{N}.bin`** —— 损坏 ⇒ 走 **`Corrupt` 报损**(GDD `AC-7a-15 (b)`),
>   **不**回退。可失去的上限 = **最新一份手动存**;更早的槽仍完好可用。
>
> ⚠️ **本节其余一切未动**:原子写、双档、校验和、checksum 覆盖范围**一字不改**。
> 本注只把 **`bak` 的适用面**写明 —— 不写明的后果是「`AC-7a-15 (a)` 看起来已兑现」,
> 而它对玩家槽**永远不可能触发**。
>
> ⚠️ **历史成因如实登记**:过去玩家槽确实见过 `bak` —— 但那是 `TD-persist-15` 那条
> 跨会话覆盖 bug 的**副产品**(有人已经覆盖了你的档,才留下 `.bak`)。
> 用户机器上 `save_6/7/8.bin` 无 `.bak`、`save_1…5.bak` 存在,即为实证。
>
> 🔁 **2026-11-04 补注(`TD-persist-19` · 用户裁定「两步做掉」)—— 上一段的口径当时只落在写侧**:
> 「玩家槽不产生 `bak`」由 `TD-persist-16` 写进写侧注释,但 `SaveService.LoadBytes`
> **仍对任何槽无条件回退 `save_{n}.bak`**。上面那段「历史成因」正好说明了后果:
> 盘上那些残留 `.bak` 是**可达的**,手动槽一旦校验坏就会**静默装回更旧的档**
> —— 比「报损」更糟,因为玩家不知道自己拿到的是旧档。
>
> **实现口径(读侧已对齐,三处同源)** —— 判据归 **`SaveSlotLedger.FallsBackToBak(seq)`**(唯一定义处):
> - `SaveService.LoadBytes` —— 手动槽 `bakPath == null` ⇒ 不入回退支;
> - `SaveSlotProbe.Probe` —— 手动槽不探 `.bak`(否则「屏上说有档、按下去说空」);
> - `SaveService.TryGetMostRecentSave` —— 手动槽的 `.bak` **不算该槽存在**。
>
> **手动槽的两条落点**(`AC-7a-15` 的直接后果):
> `final` 校验坏 ⇒ `SaveCorruptException`;`final` 缺失(哪怕盘上有残留 `.bak`)⇒ `SaveNotFoundException`。
> ⚠️ 「缺失即空槽」是**判据的一部分**,不是退让:那份 `.bak` 从来不属于该槽。
>
> 🔁 **2026-11-04 再改判(ADR-041 §D4,用户裁定「覆写合法」)—— 上两段的**前提**消失了,原文保留**:
> 「玩家槽一生只写一次」是上面全部推断的基石;覆盖合法之后 `save_N.bin` **会被写第二次**,
> `WriteAtomic` 的 `final → bak` 轮转**第一次真的对玩家槽触发** ⇒ **玩家槽从此确实有 `bak`**,
> 且它就是**这一槽的上一版**。⇒ 判据拆成两条(`SaveSlotLedger`):
> - **占用态** `ProbeCountsBak(seq)` —— 玩家槽 **false**(有档仍只看 `final`;
>   否则重造「屏上说有档 / 按下去说空」);
> - **读档回退** `ReadFallsBackToBak(seq)` —— **恒 true**(手检坏 ⇒ 读上一版,是**抢救**不是装错)。
> ⚠️ 由此 `final` 缺失 + 只剩 `.bak` 仍是**空槽**(与探测同向,`LoadBytes` 已按此实现)。
> 「`final` 校验坏 ⇒ 报损」这条**只在没有 `.bak` 时**成立;有 `.bak` 则回退并成功。

### 五、`ItemInstanceId.Next()` 机制(义务 6 的落点)

- **裁决**:`IIdAuthority.ItemInstanceId.Next()` 走**机制 A(计数器 + 高水位可重构)**,
  与 `PatientId` 同一模式(ADR-006 Amendment B):计数器**永不复位 0**;迁移后
  `next = max(全部已知 instance_id) + 1` **由三流并集重构**;世界流 `Drop*` 事件
  **必须保留 `instance_id`**(掉落清单重构的前提)。
- **验证**:同一存档迁移权威后,`instance_id` 重建映射逐位一致(无复用、无改写)。

### 六、存档时机与频率

| 触发 | 频率 | 说明 |
|------|------|------|
| checkpoint | **事件触发为主 + 定时兜底**(2026-09-16 玩家裁定「两个都留」;`CHECKPOINT_INTERVAL` = **兜底**间隔,非唯一触发) | 崩溃损失 ≤ 一个间隔;事件触发锚点白名单 `EVENT_CHECKPOINT_ANCHORS` = `{脉案落笔 · 病例结案 · 出诊启动}`(只增不删,归 7a GDD 规则六 · AC-7a-20) |
| 退出保存 | 每次退出 | 同步 / 限时 join(防退出竞态丢档) |
| 7b 手动槽 | 玩家主动 | 槽位管理归 7b 存档位 UI;只进不退 / 读档即锁 / **载入安全上下文门**:载入与写同钩 —— 亦须在**无未决医疗动作**上下文,共用观察钩子(2026-09-19 用户裁定,承 7b 首轮评审 BL-1);未决动作中载入 = 7a 拒绝。**锁字段不进存档体** —— 锁与安全门均**会话内存**,跨会话复位(重启读档不复位反 scum 语义,防 scum 靠「载入安全上下文门」而非格式锁)。 |

> 🔁 **2026-11-03 加注(`TD-persist-15` · 用户裁定「甲」)—— 本节**不覆盖**什么,一并钉死**:
>
> 上面那句「锁与安全门均会话内存」的**主语只有两个:锁、安全门**。
> 它**从未**裁过**槽位高水位**(`next_writable_seq`),后者不属于本节面内的任何一项。
>
> 曾经的误读(实现侧,非 ADR 原文):`SaveSlotLedger` 的头注把本句扩写成
> 「锁**与高水位**都是会话内存」⇒ 推出「高水位不得来自磁盘」⇒ 与 GDD 规则十四
> 「槽位只进不退,由 7a 拒绝」直接冲突(后者**语义上就是跨会话的**,否则护栏无意义)。
> 该过度引据已于 2026-11-03 就地订正。
>
> **现口径**:
> ① 高水位**不进存档体** —— 这一点与本节同向,它是**不序列化**的会话内状态;
> ② 高水位的跨会话初值取自**存档目录的文件名**(`save_{N}.bin` 是否存在),
>    **不是**存档体里的任何值 ⇒ 不违反「不进存档体」;
> ③ 求值点唯一 = `SaveService` 构造器(命名约定「什么算存档」归它,装配层不拼 `save_*`);
> ④ 若枚举失败 ⇒ 高水位不可知 ⇒ **拒绝一切写入**(fail-closed,不可用 > 不安全)。
>
> **本注不改任何既有裁决** —— 它只是把「本节不覆盖哪些量」写明,防止第二次误读。

- **主线程只做序列化**(Step 边界);**写盘交后台线程**(`Task.Run` / `ThreadPool`,IL2CPP 托管线程可用 —— 引擎复核)。
  - 同步写 MB 级 SSD 仅几 ms;HDD / 杀软可上百 ms ⇒ **默认后台写**。
  - **退出保存同步**:主钩子 = `Application.wantsToQuit`(稳定 API;**Editor 停播亦触发**、
    **崩溃 / 强杀不触发** —— 2026-09-17 7a GDD 修订);`OnApplicationQuit` 只作**兜底**
    (2026-09-16 修订:Editor 停播亦触发,与主钩子次序无关)。退出时后台写未完成 ⇒ 限时 join。
- **GC 纪律**:大 `byte[]` 分配触发主线程 GC 停顿 ⇒ **缓冲池复用**(引擎复核)。
- **后台线程禁调 Unity API**(引擎复核)。

### 七、迁移协议(版本号 + 迁移脚本)

- **存档版本号**(`SaveVersion`,u32)与**配置版本号**(`ConfigVersion`,u32)**分离**:
  - `SaveVersion` = 存档格式版本,决定迁移链入口;
  - `ConfigVersion` = 数据配置版本(21a / 52 的 `assets/data/*.json`),决定**后续窗口**用什么值。
- **迁移链**:加载时 `SaveVersion` 逐版本升级(0 → 1 → … → N),每步一个显式迁移脚本;
  **`WorldSeed` 与已发生事件原样保留**(ADR-007 §二 硬约束)。
- **配置版本号联动**:旧存档读入时,若 `ConfigVersion < 当前`,已发生事件不受影响,
  **后续窗口**用新配置(ADR-007 §Implementation Guidelines 5);存档头记 `ConfigVersion` 以便排查回放不符。
  > **🔴 2026-09-17 补注(承 37 二轮的 D-21-13 收窄裁定,不改变本条语义)**:
  > `ConfigVersion` 覆盖集现**显式含 ordinal 映射表**(病种注册表 / `case_judgment_lexicon` /
  > 物品 / 事件表 / AI 表 —— 见 `adr-014 §五`)。**本条「不匹配非致命」的措辞一字未动**,
  > 但由此产生一个必须记明的后果:**ordinal 重排不会被本条拦下**(它非致命)⇒
  > **「买断静默错读」须另设一道更严的门**,即 `adr-014 §五 D-21-13 承接条 ③` 的
  > **ordinal append-only 构建期断言**(既有条目号永不重用 / 永不改义 ⇒ 改号 = 构建失败)。
  > **分工**:本条管「改动可追溯」;`adr-014` 的 append-only 门管「静默错读」。
  > 两者**不可互相替代** —— 若有人日后想「让 `ConfigVersion` 不匹配改成致命」来收回该职责,
  > 那是**推翻本 ADR §七 与 ADR-007 §Implementation Guidelines 5**,须另开 ADR。

### Architecture Diagram

```
                     ┌──────────────────────────────────────┐
                     │  7a 持久化服务 (本 ADR = 权威件)       │
                     │                                      │
   Step 边界 ──▶  主线程:序列化 buffer(禁 Unity API 之外 IO) │
                     │        │                             │
                     │        ▼                             │
                     │  后台线程:写 tmp → flush → rename(原子)│
                     │        │                             │
                     │        ▼                             │
                     │  final 存档: 头部 + 三流 + 快照        │
                     │  SHA256 校验和 · bak 回退             │
                     └──────────────────┬──────────────────┘
                                        │ 读档
                                        ▼
               ┌────────────┬────────────┬────────────┐
               │ 病史流      │ 病例流      │ 世界流       │
               │ 折叠(Folded)│ 不折叠      │ 不折叠       │
               │ 病例事件    │ 改写史回看  │ 建造/掉落/    │
               └────────────┴────────────┴────────────┘
               max(patient_id) 重构扫三流并集 ← 头部 WorldSeed / 配置版本
```

### Key Interfaces

```csharp
// ── 头部(§一)—— 全二进制,显式小端,黄金夹具钉死 ──
struct SaveHeader {
    uint   Magic;            // "DYJQ"
    byte[32] Checksum;       // SHA256(其后全部字节 = 头部其余字段 + 三流 + 快照)
                             // ⚠️ 字段位置 2026-09-21 由「头部之末」改「Magic 之后、其余字段之前」
                             //   —— 承 AC-7a-05 口径;置末会使 WorldSeed / ConfigVersion 逃过校验
    uint   SaveVersion;      // 迁移链入口(§七)
    ulong  WorldSeed;        // ADR-007 §二,跨版本原样保留
    uint   ConfigVersion;    // 数据配置版本(§七)
    long   Tick;             // 存档时的逻辑 tick
    ulong  SnapshotOffset;   // 快照段偏移(加载加速)
}

// ── 编码器(§一 / ADR-006 §五 延伸)—— 按字段名,禁位置 ──
interface ISaveCodec {
    void   WriteEvent(in SimEvent e, ref CodecWriter w);   // 按字段名/tag 编码
    SimEvent ReadEvent(ref CodecReader r);
    void   WriteFix(Fix v, ref CodecWriter w);              // _raw 的 long,显式小端
    Fix    ReadFix(ref CodecReader r);
}

// ── 折叠谓词(§二)—— 单一出处 ──
bool Folded(PatientId p, in HistoryRecord h) {
    // ① 终态(痊愈/死亡) ② plateau / relapse.* 豁免 ③ 无未结案病例(ADR-008 §六)
}

// ── 物品实例 id(§五)—— 机制 A,与 PatientId 同模式 ──
public interface IIdAuthority {
    PatientId      Next();
    ItemInstanceId Next();     // 义务 6:计数器 + 高水位可重构(三流并集)
}

// ── 存档时机(§六)──
// 🔴 2026-09-19 首轮评审修:签名由规则十四(7a GDD)回填 —— `Checkpoint(SaveSlot)` 是
//    唯一写入口;反射断言「公开方法参数集恰等于 {SaveSlot}」以本签名与 `Load(SaveSlot)` 为真。
interface ISaveService {
    void Checkpoint(SaveSlot slot); // Step / 手动槽触发;序列化主线程,写盘后台
    void SaveOnExit();              // 退出保存:同步 / 限时 join(退出钩子,非玩家可选槽位)
    void Load(SaveSlot slot);       // 校验和 → 自动回退 bak → 迁移链 → 三流重放
}

// ── 槽位身份(§一 布局补段 / §六 7b 手动槽)── 2026-09-19 首轮评审补:此前仅有 Usage 无定义
// 只进不退 ⇒ 写入目标恒为下一空槽 ⇒ 槽位唯一身份 = slot_seq;tick/时间戳/摘要为
// 列表投影(呈现量,piece DTO 住 7b),不进确定性字节面(ADR-012 黄金对拍不受扰)。
struct SaveSlot {
    uint slot_seq;  // 单调递增(slot_seq 校验归 7a),只进不退的排序键
}
```

> 🔁 **2026-11-03 加注(`TD-persist-17` · 用户裁定「自动存档只占固定槽」)—— `slot_seq` 不再是**全序**单调**:
>
> 上格「写入目标恒为下一空槽」「`slot_seq` 单调递增」描述的只是**手动槽**。
> 现槽号按**身份**分三段:
>
> | 槽号 | 身份 | 文件 | 写入方式 |
> |---|---|---|---|
> | **1** | 退出保存 | `save_exit.bin`(独立名) | 每次退出**原地覆盖** |
> | **2** | 自动存档(定时兜底 + 事件锚点) | `save_auto.bin`(独立名) | 每次自动 checkpoint **原地覆盖** |
> | **3+** | 手动存档 | `save_{n}.bin` | **只进不退** |
>
> ⇒ 两条**结构性**后果:
> ① **`SaveSlot` 的形状不变**(仍是 `{ uint slot_seq }`,`slot_seq` 仍是槽位的唯一身份),
>    变的是 **1 / 2 这两个值的语义**:它们是**固定身份**而非序列中的两格;
> ② **`slot_seq` 不再能当「最近」的排序键** —— 固定槽会被反复重写,号小但可能最新。
>    「最近一份」的判据改为**档内 tick 最新**(`SaveService.TryGetMostRecentSave`)。
>
> ⚠️ **§六 表里「退出保存 / 7b 手动槽」两行不受影响**(触发方式与时机不变);
> 受影响的是 `OQ-7a-8` 的**三池分流**(已作废,见 GDD 该行 🔁)。
> ⚠️ **本注不改 ADR-010 的任何字节布局**:槽号不进存档体(它只决定文件名),
> §一 的布局与 §四 的原子写**一字未动**。

> 🔁 **2026-10-25 注(原文不删 · 上方代码块内有一句为假,已就地标出,但**不改裁决**)**:
> `🔴 2026-09-19` 注的括号句「反射断言『公开方法参数集恰等于 `{SaveSlot}`』
> **以本签名与 `Load(SaveSlot)` 为真**」**前提为假**:该句把 `Load` 也算进「合规样本」,
> 于是后续实现把两参的 `SaveBytes(SaveSlot, byte[])` / `LoadBytes(SaveSlot)` 一并挂进了
> `interface ISaveService`(`ISaveService.cs` 直到 2026-10-25 都有它们)。
>
> **裁决不受影响,反而是裁决被违反**:本 ADR 同一句的**前半**逐字写着
> 「`Checkpoint(SaveSlot)` 是**唯一写入口**」—— 低层文件 I/O 原语**从未**在
> Key Interfaces 内。⇒ 2026-10-25 用户裁定**把两原语移出公开面(改 `internal`)**,
> 使实现与本 ADR 逐字一致:接口恰三方法,
> 判据 = **全部**公开方法参数集 ∈ `{ {SaveSlot}, {} }`。
>
> **GDD 侧字面更严、且更早**:`persistence-service.md` `:147` / `:522` / `:531` 三处均写
> 「公开可触发**写入**方法参数集**恰等于** `{SaveSlot}`」⇒ `SaveBytes` 两参**直接违反**。
> 本轮之前,执行体(`folded_predicate_test.cs` 的反射断言)只遍历 `{Checkpoint, Load}`,
> 比 GDD 字面**窄** ⇒ **判据假绿**。已同批改回全量遍历 + 增实现侧断言
> (`SaveService.SaveBytes` / `LoadBytes` 不得 `public`)。
>
> ⚠️ **本注不构成对本 ADR 的修订** —— 裁决(唯一写入口 = `Checkpoint`)一直是对的,
> 错的是那个**括号句的措辞**与**据此写窄了的执行体**。若要正式修订该句,须另走
> 一次窄修订轮;本注只登记「已按裁决执行」与「该句为假」两件事。

### Implementation Guidelines

1. **先写 codec 与黄金夹具,再写存档流程**:字节流逐位性是一切的前提(R-5 对拍面)。
2. **三流序列化经 ADR-006 §五 编码器**:禁 float,按字段名;后台线程写盘。
3. **`Folded(p)` 三条件合并于一处**(§二),不散落各 AC / 各 ADR。
4. **`ItemInstanceId.Next()` 走机制 A**(§五),计数永不复位;三流并集重构。
5. **原子写** = tmp + flush + rename;禁 `File.Replace`(Mono Unix 抛异常,倾向弃用);禁写安装目录。
6. **校验和 = SHA256**,**字段位 = 置于头部之首;覆盖域 = 其后全部字节(头部其余字段 + 三流 + 快照)**;
   禁 `Hash128`(跨构建不保证逐位)。
   > **2026-09-21 回写(承 `TR-persist-004` 不一致① · 用户裁定 GDD 为准)**:原文三处
   > (`:127` ASCII · `:287` struct 注释 · 本条旧字面「覆盖头部后全部字节」)与
   > `persistence-service.md` AC-7a-05(2026-09-16 修订)不重合 —— 若 `Checksum` 置于头部之**末**,
   > 则 `WorldSeed`(ADR-007 硬约束)与 `ConfigVersion` 逃过校验。**统一口径 = 字段位「头部之首」+
   > 覆盖域「其后全部字节(含头部其余字段 + 三流 + 快照)」**,以 §四 与 AC-7a-05 为准。
7. **checkpoint 后台写 + 缓冲池复用**;退出保存同步 join;后台线程禁调 Unity API。
8. **配置版本号 ≠ 存档版本号**:前者决定后续窗口配置,后者决定迁移链。

## Alternatives Considered

### Alternative 1: JSON 全文本存档

- **Description**:`assets/data` 与存档都用 JSON
- **Pros**:可读;调试直观;无自定义 codec
- **Cons**:JSON 按 ADR-006 §五 **静默丢 `Fix`**(Unity 内置序列化器不识别 `readonly struct` 的 `_raw`);
  BinaryFormatter 已弃用(IL2CPP 剪裁 / 反射);体积大;原子写与逐位稳定难做
- **Rejection Reason**:与 ADR-006 §五 的编码器契约直接冲突;逐位稳定(确定性哲学)是硬约束

### Alternative 2: 玩家选择的损坏恢复(弹窗)

- **Description**:校验失败弹窗让玩家选(重试 / 忽略 / 回退)
- **Pros**:给玩家控制权
- **Cons**:打断流程;UI 成本高;7b 未实现时无承载
- **Rejection Reason**:崩溃损失 ≤ checkpoint 间隔,自动回退对玩家零打断 —— 弹窗是负价值

### Alternative 3: 增量式每 tick 写盘

- **Description**:事件流增量追加,每 tick 落盘
- **Pros**:零损失;崩溃无回退
- **Cons**:写盘频繁(帧率级 IO);与「快照 = 优化非真相」叠加后收益不成立;文件句柄长期占用
- **Rejection Reason**:checkpoint 间隔(5 分钟)已把崩溃损失压到可接受;每 tick 写是过度工程

### Alternative 4: 版本不符拒载

- **Description**:`SaveVersion` 不匹配当前版本即拒绝加载
- **Pros**:零迁移代码
- **Cons**:旧档作废(玩家损失);与存档周期成本(6-9 个月 P0)不匹配
- **Rejection Reason**:迁移脚本是确定性纯函数,成本可控;拒载把成本转嫁给玩家

## Consequences

### Positive

- **7a 首次拥有权威件**:实现者读一张义务汇总表,不再读五份 ADR 的散落各节
- **原子写 + 自动回退**:崩溃 / 损坏对玩家零打断(「上一好档」恒存在)
  > 🔁 **2026-11-03 订正(`TD-persist-16`)**:括号那半**对玩家槽不成立** ——
  > 玩家槽一生只写一次 ⇒ 无 `bak`。它对 `save_exit.bin` 仍成立。
  > 见 §四 的加注(用户裁定「甲」)。**原子写**(防半写)对两者都成立,未受影响。
- **逐位稳定可测**:黄金字节夹具成为 R-5 跨平台确定性 CI 门的前置对拍面
- **迁移确定性**:逐版本脚本 + `WorldSeed` 原样保留,旧档升级无静默失真

### Negative

- 自定义二进制 codec 是**真实的实现成本**(每结构写编解码,ADR-006 §五 已接受)
- 后台线程写盘引入并发复杂度(缓冲池复用 + 退出 join)
- `Folded(p)` 三条件合并需要 9 的 AC-36 与 ADR-008 同步口径(跨文件会签)

### Neutral

- 快照段承担表现态位置恢复(ADR-009 §五),但不承担真相
- 配置版本号与存档版本号分离,新增一个头部字段

## Risks

| Risk | Probability | Impact | Mitigation |
| --- | --- | --- | --- |
| **`File.Replace` 在 Unix 抛异常**(Mono 系 BCL) | 中 | 中 | 弃用 `File.Replace`,用 tmp + `File.Move(tmp, final, overwrite)`(rename 语义,跨平台原子);须实测 |
| **checkpoint 卡帧 + 杀软 / OneDrive 锁文件** | 中 | 中 | 后台线程写 + `IOException` 退避重试;主线程只序列化 |
| **codec 被偷换回 Unity 内置序列化器**(`Fix` 静默归零) | 中 | **高** | 延伸 ADR-006 §五 EditMode 探针到存档 codec(断言内置序列化器往返 `Fix` 丢值) |
| **`Hash128` 跨构建不稳定** | 低 | 中 | 用 BCL `SHA256`(确定性);`Hash128` 需实测方可考虑 |
| **退出竞态丢档**(后台写未完成即退出) | 中 | **高** | 退出保存同步 / 限时 join(§六) |
| **迁移脚本漏保留 `WorldSeed`** | 低 | **高** | §七 硬约束 + 迁移链单测(逐版本回放字节流逐位一致) |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU(帧时间) | — | 序列化在 Step 边界,写盘后台线程 | 16.6 ms 平面 / 11.1 ms VR(零主线程 IO) |
| Memory | — | 缓冲池复用,大 `byte[]` 不触发主线程 GC 停顿 | 待定 |
| Load Time | — | 三流重放 + 快照段;黄金夹具对拍 | 待定 |
| Network | — | 三流传输归 45(ADR-001);存档本身非网络通道 | — |

## Migration Plan

**本项目尚无 7a 的实现,故无迁移 —— 本 ADR 是「第一次就做对」。(同 ADR-005)**

1. **先写 codec + 黄金夹具**:字节流逐位性定义在实现前(可测的验收标准)。
2. **写三流序列化**(经 ADR-006 §五 编码器)+ `Folded(p)` 三条件。
3. **写 `ItemInstanceId.Next()`**(机制 A)+ 世界流 `Drop*` 事件。
4. **写原子写 + 校验和 + 自动回退**(§四)+ 后台线程 checkpoint(§六)。
5. **写迁移链**(§七):逐版本脚本 + `WorldSeed` 原样保留。
6. 撰写 7b 存档位 UI 时,`SaveSlot` 槽位契约以本 ADR §Key Interfaces 为准 —— **`struct SaveSlot` 定义已补于 §Key Interfaces(2026-09-19)**;槽位头信息(tick / 时间戳 / 摘要)为**列表投影 DTO(呈现量)**,定义归 7b,其**字节面不参与确定性对拍**(ADR-012)。签名 `Checkpoint(SaveSlot)` 是唯一写入口(规则十四回填)。

**Rollback plan**:若最终不做联机,后台线程写可退化为同步写(删并发复杂度);
但 **codec / 三流 / 折叠谓词 / 原子写** 已进存档格式,**不可回退**(改格式 = 旧档作废)。
若做联机,**本 ADR 不可回退**。

## Validation Criteria

- [ ] **黄金字节夹具**:同一存档在编辑器 vs IL2CPP 构建产出**逐位相同**的字节流
- [ ] 存档往返(`save → load`)后三流**逐位不变**(`WorldSeed` / 已发生事件原样)
- [ ] `Folded(p)` 单测:终态无未结案 ⇒ 折叠;有未结案 ⇒ 不折叠;plateau / relapse 豁免生效
- [ ] 原子写:模拟写盘中途崩溃 / 断电,`final` 或 `bak` 至少一个完整可载
- [ ] 校验和:篡改任一字节 ⇒ 校验失败 ⇒ 自动回退 `bak` 成功且无打断
      🔁 **2026-11-03(`TD-persist-16`)适用面**:本条只在 **`save_exit.bin`** 上可跑
      (玩家槽无 `bak`,见 §四 加注)⇒ 玩家槽一侧的对应判据是「**报损且零写盘**」
      (`AC-7a-15 (b)`),不是本条
- [ ] `ItemInstanceId` 迁移稳定性:含世界流的存档,新 authority 重构 `next`
      **严格大于三流全部 `instance_id`**(无复用)
- [ ] 迁移链:构造 v0 存档逐版本升级到当前,`WorldSeed` 与已发生事件**逐位保留**
- [ ] 存档中不出现任何 `float`(可 grep 验证,ADR-006 判据延伸)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Addresses It |
|--------------|--------|-------------|--------------------------|
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | 规则六:存档存病史事件流,不存逐帧状态 | §一 三流序列化;§二 折叠谓词 |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | AC-2:存档往返不变且无 float | §Validation 黄金夹具 + 禁 float |
| `design/gdd/disease-simulation.md` | 9 疾病与伤情模拟 | AC-36:终态折叠(plateau / relapse 豁免) | §二 `Folded(p)` 条件 ①② |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | `ItemInstance` 序列化(含 `quality`;不存 `item_key` 引用,存快照) | §三 义务 9 |
| `design/gdd/item-database.md` | 21a 物品与配方数据库 | `instance_id` 必须入快照;闭包校验 | §三 义务 10;§五 机制 A |
| `design/gdd/case-system.md` | 37 病例系统 | 病史流折叠谓词 `Folded(p)`(含「无未结案病例」条件)· `max(patient_id)` 扫两流并集 | §二(升格为三流,ADR-009 Amendment E) |
| `design/gdd/random-events.md` | 52 随机事件导演 | `WorldSeed` 由 7a 存档头持有 | §一 头部 · §七 迁移保留 |
| `design/gdd/systems-index.md` | 7a 持久化服务 | 系统 7a 实现(唯一无 GDD 的 Core 系统) | 本 ADR 即其权威件。**⚠️ 2026-09-21 历史注**:「唯一无 GDD」为本表 2026-09-15 撰写时状态,7a 后已立 `persistence-service.md`(2026-09-17);「本 ADR 即其权威件」的关系不变** |
| `design/gdd/systems-index.md` | — | §9 C6:9 的 tick 模型与 7 的持久化从第一天起 authority-agnostic | §一 / §二 三流 + 折叠,authority-agnostic |

## Related

- **ADR-005 确定性模拟与状态同步模型**(Accepted)—— 事件流唯一真源 · 折叠规则 · `IIdAuthority`。本 ADR 的折叠谓词与三流序列化以此为准
- **ADR-006 定点域边界数据契约**(Accepted)—— `Fix` 自定义编码器(§五)是本 ADR codec 的基础;Amendment E 的三流口径是本 ADR §二 重构的前提
- **ADR-007 事件权威与掷骰状态**(Accepted)—— `WorldSeed` 存档头契约(§二)由本 ADR §一 兑现
- **ADR-008 病例事件流**(Accepted)—— `Folded(p)` 的「无未结案病例」条件 + 病例流不折叠
- **ADR-009 世界状态的事件化边界**(Accepted)—— 世界流序列化 · `ItemInstanceId.Next()` 机制 · 快照 = 优化非真相
- **R-5 / 跨平台确定性 CI 门**(待撰写)—— 黄金字节夹具是其前置对拍面
- **7b 存档位 UI**(P0,待撰写)—— `SaveSlot` 槽位契约
- `design/registry/entities.yaml` —— 待登记存档常量(默认 checkpoint 间隔等)
