# T9 技法解锁的触发通路 —— 实施与证据(2026-11-09)

> **范围**:`technique-unlock-map-2026-11-09.md` 的「甲」——「技法怎么解锁」。
> **用户指令**:「开工 T9，接技法解锁的触发通路」。
> **上游**:T3(ADR-043 `TechniqueUnlocked` 载体)· T7(`6e93e025` 技法覆盖)· T8(`06c23e3a` 缺页判定)。
> **本件交付「机制」,不交付「可见行为」** —— 见 §6。**这条与 T8 同形,但断点位置不同**:T8 缺的是宿主,
> 本件缺的是**内容**。

---

## 1. 断点在哪(可复算)

T3 落了 `TechniqueUnlockWriter.TryUnlock`,但**全仓生产调用点只有一处** ——
起步集写入器(`TechniqueUnlockBootstrap.Ensure`,内容 = `{0, 1}`)。

```bash
grep -rn "TryUnlock(" unity/Assets --include=*.cs | grep -v "/Tests/"
# 期望:TechniqueUnlockWriter 自身的定义 + TechniqueUnlockBootstrap 的一处调用
```

⇒ 后果:17 行注册表里 **6 行**可投放(4 件伤情 + 两个 `Undeclared`),其余 **11 行永久被拒**
(T4 的判定要求「技法已解锁」)。这不是文档缺陷,是**玩家永远学不到第二招**。

---

## 2. 改动清单

| # | 文件 | 层 | 内容 |
|---|---|---|---|
| 1 | `Gameplay.Boot/TechniqueUnlockTrigger.cs` | **新** | `TechniqueUnlockSourceKind`(三值闭集)· `TechniqueUnlockRule`(三工厂)· `TechniqueUnlockRules.Default`(今日 `[]`) |
| 2 | `Gameplay.Boot/TechniqueUnlockDriver.cs` | **新** | 每 tick 全量扫事件流,三类源各一支匹配,命中交写者 |
| 3 | `Gameplay.Boot/CompositionRootServices.cs` | 服务袋 | 新增 `TechniqueUnlockDriver` 槽(与 `TechniqueUnlockWriter` 相邻) |
| 4 | `Gameplay.Boot/BootRoot.cs` | 装配 | 第 **4.4b** 步构造驱动(玩家 id 已铸造)· tick 泵在 52 驱动**之后**调用 |
| 5 | `Tests/EditMode/SkillSystem/technique_unlock_trigger_test.cs` | **新** | **21 条**判据 |
| 6 | `Tests/PlayMode/TechniqueUnlockWiringTest.cs` | **新** | **1 条**生产接线判据 |

**零新增 Kind、零 registry 改动、零存档格式变化、零 ADR 改动。**
`Sim` / `Sim.Contracts` 引用集一字未动(门 A / b2 / ADR-025 §① 继续成立)—— 驱动住 `Gameplay.Boot`。

---

## 3. 设计上的四个「为什么」

### 3.1 为什么是「扫流」而不是「各处调用」

奇遇 / Boss / 病例分属 52 / 6 / 37 三个系统。在三处各插一次 `TryUnlock` = 三份「什么算解锁」的
判据,且每一处都要知道技法 id 空间。扫流则**只有一份判据**,且**读档后自动重放**。

### 3.2 为什么零游标(每轮全量扫)

`EventStream.Restore` 是**整表替换**。带游标就要处理替换后的失配 —— 而它是**静默的**:
游标落在旧流某个位置,替换后要么跳过一段(漏解锁)、要么越界(不扫)。全量扫 + 写者侧
`IsUnlocked` 去重 ⇒ 幂等、读档自愈、**没有游标可失配**。代价被规则表前置掉:
`_rules.Count == 0` ⇒ 直接 return,今日零开销。

> ⚠️ 与 `ConsequenceDriver` 同款(`AC-53-06`「零跨 tick 态」),不是新发明。

### 3.3 为什么写入 tick 用**当前 tick**

源事件可能落在很早的 tick(读档重放会整条历史扫一遍)。若照抄源事件的 `Tick`,
`EventStream.Append` 的单调性判据会因「Tick 回退」直接抛(BCD-码-3)。
用当前 tick ⇒ 永远单调非降。判据:`test_writtenTick_isCurrentTickNotSourceTick`。

### 3.4 为什么 `Treated == false` 的结案不解锁 —— 且**不**做成规则字段

「从没治过的病例里学会技术」没有语义。做成 `require_treated` 字段就会有一个
「谁能把它设成 false」的分支,而那个分支没有用处。写死在驱动里,由
`test_caseUntreated_doesNotUnlock` 钉住。

---

## 4. 判据(EditMode 21 条 + PlayMode 1 条)

| # | 判据 | 判别力(去掉/写反哪一处会红) |
|---|---|---|
| 1 | 空规则表 ⇒ 返回 0、流长不变 | 驱动在空表上仍写事件 ⇒ 红 |
| 2 | 生产规则表今日为空 | 有人偷偷填了内容却没更新本件的「未做」表 ⇒ 红 |
| 3 | POI 到达指定状态 ⇒ 解锁,载荷三字段全对 | `sourceId` 写成规则下标 / 玩家 id 写错 ⇒ 红 |
| 4 | POI 状态**不符** ⇒ 不解锁 | 只按 `poi_id` 匹配(忽略状态)⇒ 红 |
| 5 | 另一个 POI 的同一状态 ⇒ 不解锁 | 忽略 `SourceId` ⇒ 红 |
| 6 | 奇遇 `tier ≥ minTier` ⇒ 解锁 | 比较写成 `<` ⇒ 红 |
| 7 | 奇遇 `tier < minTier` ⇒ 不解锁 | 忽略档位 ⇒ 红 |
| 8 | `minTier = -1` ⇒ 任意档 | 把 -1 当「无匹配」⇒ 红 |
| 9 | 已处置结案含该病种 ⇒ 解锁 | `sourceId` 写成 patientId ⇒ 红 |
| 10 | **未处置**结案 ⇒ 不解锁 | 删 `if (!p.Treated) break;` ⇒ 红 |
| 11 | 病种不在集合内 ⇒ 不解锁 | 位集判据写成「非空即命中」⇒ 红 |
| 12 | 病种 id ≥ 64 ⇒ 永不命中 | 越界当命中 ⇒ 一条写错的规则无条件触发 |
| 13 | 同一份流扫两遍 ⇒ 第二遍 0、流长不变 | 驱动自带一份去重并与写者分叉 ⇒ 红 |
| 14 | 两条规则指向同一技法 ⇒ 仍只有一条 | 绕过写者直接 `Append` ⇒ 红 |
| 15 | 写入 tick = 当前 tick | 照抄源事件 tick ⇒ **异常**(BCD-码-3)而非断言失败 |
| 16 | 生产规则技法 id ⊆ 处置轴闭集 | —(今日空真,见 #17) |
| 17 | 同一谓词的**负控**:越界 id ⇒ false | 谓词恒真 ⇒ 红 ⇒ 证明 #16 不是空洞正控 |
| 18 | 负 actorId ⇒ 抛 | 允许 -1 ⇒ 写出 `PatientId.None` 的解锁(世界级) |
| 19 | 三个工厂拒收负 id(7 组) | 任一放开 ⇒ 红 |
| 20 | `minTier < -1` ⇒ 抛(点名校验形参 `minTier`) | 允许 -2 ⇒ 一条无意义约束静默存在 |
| 21 | 越界技法 id ⇒ **写者侧**也 fail-loud | 写者不校验 ⇒ 流里出现「解锁了但没有这个动作」的死行 |
| P1 | 生产接线:驱动非 null · 写者同源 · 读流同源 · `actorId` = 玩家 | 任一不成立 ⇒ 红(见下) |

**P1 的四条为什么单列**:EditMode 那 21 条全部构造自己的流与写者,**没有一条**能证明
生产装配看见了它们。P1 用真启动序 + 反射比对:`BootRoot` 若漏了第 4.4b 步 ⇒ 红;
构造时传错流(`stream` 与 `services.Stream` 不是同一实例)⇒ 红,这正是 ADR-036 D5 那条
「读一个世界、写另一个世界」的同族失效模式。

---

## 5. 回归

**三元组**:`2026-11-09T14:35:18Z`(EditMode 结束)/ `14:39:41Z`(PlayMode 结束) · **全量** · `06c23e3a` + 工作树。

| 套件 | 本次 | 对照基线(T8 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7285 / 7248 / 0 / 0 / 37`** | `7264 / 7227 / 0 / 0 / 37` @ `2026-11-09T14:07:25Z` · T8 批 · `06c23e3a` | **+21 / +21** |
| PlayMode(全量) | **`372 / 367 / 0 / 2 / 3`** | `371 / 366 / 0 / 2 / 3` @ `2026-11-09T14:09:00Z` · T8 批 · `06c23e3a` | **+1 / +1** |

**等式核对**:EditMode 增量 **21** = `technique_unlock_trigger_test` 的 `[Test]` 条数 ·
PlayMode 增量 **1** = `TechniqueUnlockWiringTest` 的 `[UnityTest]` 条数。

**编译期两轮红(如实登记:都是我的写法错,不是设计错)**

1. `TechniqueUnlockTrigger.cs` 三处具名实参写成 `MinTier:` / `RequiredState:`,而私有构造形参是
   `minTier` / `requiredState` ⇒ `CS1739` ×3。**根因**:把「公开属性的 PascalCase」迁到了
   **形参**上。改形参名即可,未放宽任何校验。
2. `Closed(treated: true, diseaseIds: 5, 9)` —— 具名实参后接 `params` 展开 ⇒ `CS1501`。
   改为 `diseaseIds: new[] { 5, 9 }`。

---

## 6. 未做(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| **规则行(内容)** | **未填** | 三条候选源在数据侧**都没有可映射的语义**(见 §1 与下条)。⇒ 今日驱动零扫描、零写入;**这不是「没接上」** —— 接上了、也跑了,只是没有一行内容 |
| `world_poi.json` 的状态词表 | **不存在** | 只有 `{poi_id, type}`。`required_state` 该填什么值,得先有 6 的状态枚举 |
| 52 事件键的语义 | **不存在** | `52_event_pool.json` 只有数字 `key` + 权重,没有「哪个 key 是哪个奇遇」 |
| 病例 → 技法映射 | **不存在** | 37 侧没有这张表;规则行只能由内容轮给 |
| 规则值的词表机械门 | **未建** | `poi_id` / `event_key` / `disease_id` 的值域今日**无任何门**兜(技法 id 有,见判据 #16/#21)。规则行一旦开始填,这三条应各补一道构建期断言 |
| 奇遇两段(`§3.2`) | **仍是草案** | 与 T7 同一未结项:位置待用户确认 |
| 清创缝合 / 蛇伤处置的执行体 | **未做** | 与 T7 同一未结项(需要数值裁定) |

> **一句话**:本步把「奇遇 → 解锁」的**管道**接好了,但**水还没有**(内容是空的)。
> 补第一行不需要改任何代码 —— 见 `TechniqueUnlockRules.Default` 的 remarks 里的示例。

---

## 7. 载体选择(为什么规则表住代码,而不造 baked artifact)

| 备选 | 代价 | 今日收益 |
|---|---|---|
| **baked artifact**(`assets/data/technique_unlocks.json` → cooked → Addressables) | 六处联动(源 JSON / binder / cooked 写器 / 运行时 codec / Addressables 组 + `.meta` GUID / 漂移门哨兵) | **零** —— 表是空的 |
| **代码常量**(本件) | 一个文件 | **零** —— 表是空的 |

两者今日收益相同,而前者多出「空表」与「表没装上」在运行期不可区分这一失效面。
**同族先例**:`TechniqueUnlockBootstrap.StartingTechniques` 自 T3 起就是「内容值,归内容轮」的
代码表 —— 同一件事两种载体 = 第二套口径。

**升级路径是一个缝**:驱动只认 `IReadOnlyList<TechniqueUnlockRule>`;要外部化时换来源,
驱动与全部 22 条判据**零改动**。
