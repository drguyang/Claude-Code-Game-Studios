# S22-4 · smoke `#14` 载体(52 急召生成链)· 执行原件

| 字段 | 值 |
|---|---|
| **卡** | sprint-22 `S22-4` |
| **对象** | `tests/smoke/critical_paths.md` #14「疫情:52 抽池事件落在 `PATIENT_APPEARANCE_CAP` 界内,超界拒收 + 明确报错」 |
| **执行轮** | 2026-11-01 · 用户指令「做 S22-4」 |
| **判据载体** | `unity/Assets/Tests/EditMode/RandomEvents/random_event_spawn_chain_test.cs`(6 条) |

---

## 1. 卡面三条要求

| 要求 | 落点 |
|---|---|
| 生产调用点存在 | `RandomEventsDirectorDriver.OnTickEdge` 每 tick 调 `RandomEventSpawnChain.TrySpawnAtTick`;`CompositionRoot` 装配 |
| CAP 界内 / 超界拒收 + 明确报错 两侧夹具 | A(界内 2 条)/ B(超界:抛 + **零写入** + 报文含 `AC-15` 与 `24`) |
| **必须用合成 key 集 + 合成格集驱动** | 全部夹具:key `{7001,7002,7003}` · 格 `(9,0,4)` |

---

## 2. 生产侧交付

| 文件 | 作用 |
|---|---|
| `Sim/EventDraw.cs`(新) | 抽池命中 = 纯函数 `SplitMix64(WorldSeed, win, tier, ordinal) % 候选数` |
| `Gameplay.Boot/SeedEventAuthority.cs`(新) | 52 掷骰的**生产实装**(取代 `P0HostEventAuthority.Roll` 的抛形占位) |
| `Gameplay.Boot/RandomEventSpawnChain.cs`(新) | 抽池 → F1(上下文门 + 档配额)→ **CAP 预检** → 掷骰 → `EventRolled` + `EventArrived` → 病人出现 |
| `Gameplay.Boot/RandomEventsDirectorDriver.cs` | 新增 `SpawnChain` 缝 + 每 tick 调用;**异常接住不杀帧**(记 `LastSpawnError`) |
| `Gameplay.Boot/CompositionRoot.cs` | 装配(候选集 = 空集 ⇒ 生产恒 `NoPool`) |

### 2.1 本件唯一的原子性主张

**CAP 预检排在第一次 `Append` 之前。** 排在之后(或只在 `EventStream` 拦)的失效模式:
`EventRolled` 先落流,后半截被 AC-15 拒 ⇒ 流里留下「掷过骰、事件没到」的孤儿行,
读档重放时**凭空多一次掷骰**。这条由证伪 M1 直接打中。

### 2.2 生产仍是「通电待信」(如实,不借绿)

- 候选池 = **空集**(52 的池内容归 37 内容件)⇒ 每 tick 真跑、恒 `NoPool`、零写入;
- `spawnPatient` 缝**刻意抛**(急召 key → 37 病种的映射表不存在;返回假病种 = 让「有人开始生成病人了」
  静默通过)。池回填时须**同批**加映射。
- **未实现**:密度预算(GDD 规则十「查预算」)· 权重 / 历史标记乘子 · 生成点解析(注入占位锚)。

---

## 3. 判据(6 条)

| # | 测试 | 钉的性质 |
|---|---|---|
| A | `inCap_writesRollThenArrival_withSyntheticKey` | 恰两条(先骰后降临)· key ∈ 合成集 · 锚点 = 合成格 · 病人创建恰一次 |
| B | `overCap_rejectsWithExplicitError_andWritesNothing` | **抛** + 报文含 `AC-15`/`24` + **零写入** + **零掷骰** + **零创建** |
| C | `emptyPool_isFailSafe_noWritesNoThrow` | 空池 ⇒ 具名 `NoPool`、零写入、不抛(生产今日形态) |
| D | `roll_usesF1WindowAndTier` | 掷骰 `(win=40, tier, ordinal 0→1→换窗归 0)` 恰是 F1 那一组 |
| E | `director_tickEdge_drivesSpawnChain` | 驱动**真调**链(玩家未绑定照跑)· 同 tick 不重复 |
| F | `director_overCap_recordsError_withoutKillingFrame` | 边沿上超界被接住(不杀帧)但**留记录**(接住 ≠ 吞掉) |

---

## 4. 证伪轮(4 变异,4/4 红)

| 变异 | 停用了什么 | 实测 |
|---|---|---|
| **M1** | CAP 预检挪到两次 `Append` **之后** | **超界两条红**(零写入 / 零记录),界内四条仍绿 —— 正面证明原子性主张 |
| **M2** | 空池短路只判 `null`(空数组继续往下) | **仅 C 红**(空池变成抛) |
| **M3** | 绕开掷骰,直接取候选首项 | **仅 D 红**(掷骰没被调用) |
| **M4** | 驱动不调生成链 | **E / F 红**,链自身四条绿 |

还原:`RandomEventSpawnChain.cs` / `RandomEventsDirectorDriver.cs` SHA-256 逐字节一致,
`FALSIFY-` 残留 **0**。

---

## 5. 回归与 smoke(三元组)

**截至 `2026-11-01 09:13:21 CST` · 批次 `S22-4` · commit `dd9a82d1` + 未提交工作树**

| 平台 | 本次 | 前次(`09:05` 首跑,含 6 红) | 基线(`08:50`) | 等式 |
|---|---|---|---|---|
| EditMode | `6337 / 6300 / 0 / 37` | `6337 / 6294 / **6 红**` | `6331 / 6294 / 0 / 37` | `6337 − 6331 = 6` = 新件 6 条 ✅ |
| PlayMode | `273 / 269 / 0 / 2 / 2` | 同 | 同 | 零变化 ✅ |

### 5.1 首跑 6 红 = **门抓住了我**(如实登记)

S22-4 落了 `EventRolled` / `EventArrived` 的生产写者(`RandomEventSpawnChain`),首跑全量
**6 条门测转红**,两类:

| 门 | 红行 | 处置 |
|---|---|---|
| **b7 写作门** | 「Kind「EventRolled / EventArrived」已在扫描面内有生产写者,却仍挂在豁免表 —— 豁免 = 红」 | 同批**撤表**(`WriterExistenceGate.cs`,承 `PlayerDied` / `ThreatDeferred` 先例) |
| **AC-51-E3 哨兵** | 「检测到**非占位**的 `IEventAuthority` 生产实装:`SeedEventAuthority`」 | 该哨兵的排除标记 `IP0AuthorityPlaceholder` 的语义即「恒 host、零联机语义」——`SeedEventAuthority` 正是这个口径(只多了真掷骰,而**掷骰权 ≠ 联机权**)⇒ 实现该标记,并把哨兵的允许集从「只认 `P0HostEventAuthority`」改成**显式双名允许集**(随便挂标记仍红) |

> **两条都不是「改测试迁就实现」**:b7 的判据明文要求「写者落地须同批撤表」;
> E3 的允许集**仍是封闭枚举**,新增第三种要留名才过。

### 5.2 smoke 实跑

```
$ python3 tests/smoke/run_smoke.py --results unity/Logs/full-EditMode-saveslot.xml \
                                            unity/Logs/full-PlayMode-saveslot.xml
  1  A  test    PASS              游戏启动到主菜单无崩溃(…)
 14  B  test    PASS(2 载/全绿)     疫情:52 抽池事件落在 PATIENT_APPEARANCE_CAP 界内,超界拒收 + 明确报错
合计 22 条 | 自动 PASS 13 · 自动 FAIL 0 · 未跑(需结果件) 0 · 具名人工 3 · 无名 6
exit=3
```

`自动 PASS 12 → 13`(`#14` 双载体全绿)· `无名 7 → 6`;退出码**仍 3**(6 条无载体仍在)。

---

## 6. 本卡**不**覆盖

- **`#14` 的「疫情」语义**(原型疫情 → 已改为脚本条目、不进抽池,GDD `:424`)—— 本卡钉的是
  「52 抽池侧到 CAP 的那条链」,不是疫情条目本身的内容。
- 病人在场登记(归 9 / 15)· 密度预算 · 权重调制 · 生成点解析。
