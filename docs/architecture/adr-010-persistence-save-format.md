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

## Summary

系统 7a「持久化服务」是**全案唯一无 GDD 的 Core 系统**,却已被 ADR-005 / 006 / 007 / 008 / 009
**五份 ADR 集体委派义务**(存档头契约 · 三流序列化 · 折叠 · 快照 · `ItemInstanceId.Next()` 机制 ·
迁移协议)—— **它自己没有权威件**。这些义务散落各处、部分互相冲突(WorldSeed 载体口径、
配置版本号归属、折叠谓词条件分散),实现者无单一出处可循。
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
│  magic "DYJQ" (4B) · 存档版本号 (u32)          │
│  WorldSeed (u64) · 配置版本号 (u32)             │
│  tick (i64) · 快照偏移 (u64)                   │
│  校验和 SHA256(头部后全部字节) (32B)            │
├───────────────────────────────────────────────┤
│  病史流 (编码记录序列)                          │
│  病例流 (编码记录序列)                          │
│  世界流 (编码记录序列)                          │
├───────────────────────────────────────────────┤
│  快照段 (表现态位置 · 加载加速 · 优化非真相)     │
└───────────────────────────────────────────────┘
```

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

> 义务 1–8 的源 ADR 不变;本 ADR 是**实现的收敛处**。**任何新委派只能追加到本表** ——
> 实现者读这一张表,不读五份 ADR 的散落各节。

### 四、损坏恢复(校验和 + 自动回退)

- **校验和**:`SHA256`(BCL,IL2CPP 可用,确定性;MB 级 1–5 ms 足够快 —— 引擎复核)。
  **不用 `UnityEngine.Hash128`**(文档不保证编辑器 vs IL2CPP 玩家逐位一致,须实测)。
  覆盖范围 = checksum 字段之后的**全部字节**(头部 + 三流 + 快照)。
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

### 五、`ItemInstanceId.Next()` 机制(义务 6 的落点)

- **裁决**:`IIdAuthority.ItemInstanceId.Next()` 走**机制 A(计数器 + 高水位可重构)**,
  与 `PatientId` 同一模式(ADR-006 Amendment B):计数器**永不复位 0**;迁移后
  `next = max(全部已知 instance_id) + 1` **由三流并集重构**;世界流 `Drop*` 事件
  **必须保留 `instance_id`**(掉落清单重构的前提)。
- **验证**:同一存档迁移权威后,`instance_id` 重建映射逐位一致(无复用、无改写)。

### 六、存档时机与频率

| 触发 | 频率 | 说明 |
|------|------|------|
| 定期 checkpoint | 默认 5 分钟(`CHECKPOINT_INTERVAL` 旋钮) | 崩溃损失 ≤ 一个间隔 |
| 退出保存 | 每次退出 | 同步 / 限时 join(防退出竞态丢档) |
| 7b 手动槽 | 玩家主动 | 槽位管理归 7b 存档位 UI |

- **主线程只做序列化**(Step 边界);**写盘交后台线程**(`Task.Run` / `ThreadPool`,IL2CPP 托管线程可用 —— 引擎复核)。
  - 同步写 MB 级 SSD 仅几 ms;HDD / 杀软可上百 ms ⇒ **默认后台写**。
  - **退出保存同步**(`OnApplicationQuit` 时后台写未完成 ⇒ 限时 join)。
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
    uint   SaveVersion;      // 迁移链入口(§七)
    ulong  WorldSeed;        // ADR-007 §二,跨版本原样保留
    uint   ConfigVersion;    // 数据配置版本(§七)
    long   Tick;             // 存档时的逻辑 tick
    ulong  SnapshotOffset;   // 快照段偏移(加载加速)
    byte[32] Checksum;       // SHA256(头部后全部字节)
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
interface ISaveService {
    void Checkpoint();        // Step 边界调用;序列化主线程,写盘后台
    void SaveOnExit();        // 退出保存:同步 / 限时 join
    void Load(SaveSlot slot); // 校验和 → 自动回退 bak → 迁移链 → 三流重放
}
```

### Implementation Guidelines

1. **先写 codec 与黄金夹具,再写存档流程**:字节流逐位性是一切的前提(R-5 对拍面)。
2. **三流序列化经 ADR-006 §五 编码器**:禁 float,按字段名;后台线程写盘。
3. **`Folded(p)` 三条件合并于一处**(§二),不散落各 AC / 各 ADR。
4. **`ItemInstanceId.Next()` 走机制 A**(§五),计数永不复位;三流并集重构。
5. **原子写** = tmp + flush + rename;禁 `File.Replace`(Mono Unix 抛异常,倾向弃用);禁写安装目录。
6. **校验和 = SHA256**,覆盖头部后全部字节;禁 `Hash128`(跨构建不保证逐位)。
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
6. 撰写 7b 存档位 UI 时,`SaveSlot` 槽位契约以本 ADR §Key Interfaces 为准。

**Rollback plan**:若最终不做联机,后台线程写可退化为同步写(删并发复杂度);
但 **codec / 三流 / 折叠谓词 / 原子写** 已进存档格式,**不可回退**(改格式 = 旧档作废)。
若做联机,**本 ADR 不可回退**。

## Validation Criteria

- [ ] **黄金字节夹具**:同一存档在编辑器 vs IL2CPP 构建产出**逐位相同**的字节流
- [ ] 存档往返(`save → load`)后三流**逐位不变**(`WorldSeed` / 已发生事件原样)
- [ ] `Folded(p)` 单测:终态无未结案 ⇒ 折叠;有未结案 ⇒ 不折叠;plateau / relapse 豁免生效
- [ ] 原子写:模拟写盘中途崩溃 / 断电,`final` 或 `bak` 至少一个完整可载
- [ ] 校验和:篡改任一字节 ⇒ 校验失败 ⇒ 自动回退 `bak` 成功且无打断
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
| `design/gdd/systems-index.md` | 7a 持久化服务 | 系统 7a 实现(唯一无 GDD 的 Core 系统) | 本 ADR 即其权威件 |
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
