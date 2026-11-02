# 实现原件 —— `TD-persist-12` 收口:读档后 id 高水位按新流重构

> **对象**:`docs/tech-debt-register.md` 的 `TD-persist-12`
> **批次**:`S23-20`(用户指令「按你的建议继续修」)· **截至时刻**:见 §四
> **commit**:本批未提交工作树(基于 `7b5bde91`)

---

## 一、病因(改前实测)

两条缺口叠在一起:

| # | 事实 | 复算(改前) |
|---|---|---|
| ① | `ItemInstanceHighWaterCollector.CollectAndReconstruct` **生产调用点 = 0** | `grep -rn "CollectAndReconstruct" unity/Assets --include=*.cs \| grep -v /Tests/` ⇒ 只命中定义文件自身两处 |
| ② | `IdAuthority.ReconstructPatientHighWater` **生产调用点 = 0** | 同上口径 ⇒ 只命中 `IdAuthority.cs:90` |
| ③ | 装配根 `new IdAuthority()`(计数器从 0 起),读档路径不调任何重构 | `CompositionRoot.cs:141` |

⇒ 读档换流后两个计数器都停在**装配那一刻**的值 ⇒ 存档里更大的 id 会被后续铸造复用
⇒ fold 里两件物品静默合并(ADR-010 §五)/ 新病人复用已折叠病人的 id(ADR-006 Amendment B)。

**与债册原文的一处差异(如实登记)**:原文把「基线不导出 item 实例 id」列为缺口 ②。
**该半边已在 `AC-4-22` 轮(`43f1d2f3`)落地** —— `world_containers` 烘焙表带 `instance_id`,
且装配根容器段已 `ReconstructItemHighWater(containerData.InstanceIds())`。
⇒ 本轮补的是**读档路径那一半**(装配期抬升只对开档那一刻的流有效)。

---

## 二、改了什么

| # | 文件 | 变更 |
|---|---|---|
| 1 | `Gameplay.Boot/CompositionRoot.cs` | 新增 `Func<IEnumerable<long>> bakedInitialItemIds`(声明在 `if (saveBasePath)` **之外**,惰性)—— 容器段赋值,`afterRestore` 读取;同一份值两处读者 |
| 2 | 同上 · `afterRestore` | **先**重构两个高水位,**再**重建派生态:`ItemInstanceHighWaterCollector.CollectAndReconstruct(stream.Events, decoder, bakedInitialItemIds?.Invoke(), itemAuthority)` + `ReconstructPatientHighWater(三流 ∪ 折叠行)` |
| 3 | `Tests/EditMode/Boot/load_highwater_wiring_test.cs` | **新增** 4 条判据(见 §三) |

**为何 `bakedInitialItemIds` 是 `Func` 而非实例**:容器段在 `if (harvestProvider)` 内、
`LoadSequencer` 构造在 `if (saveBasePath)` 内 —— 两个守卫条件不同,直接捕获实例会要求
「两边都装配」,而那是两个独立形态。

**为何 `afterRestore` 里要**显式转型 + 失败即抛**:`IIdAuthority` 接口上没有
`ReconstructItemHighWater`(它是 `IdAuthority` 的具体能力)⇒ 静默跳过 = 读档后掉号,
且只在读档后才显形(与容器段同一纪律)。

---

## 三、判据(4 条,逐条可证伪)

| # | 判据 | 位置 |
|---|---|---|
| ① | 存档 `DropSpawned.instance_id = 10000` ⇒ 读档后 `NextItemInstanceId()` 必须越过它 | `test_persist12_itemHighWater_reconstructedFromLoadedStream` |
| ② | **每次读档都重算**:连读两份档,第二份的 id 必须再抬一次 | `test_persist12_itemHighWater_recomputedOnEachLoad` |
| ③ | 存档 `patient_id = 500` ⇒ 读档后 `NextPatientId()` 必须越过它 | `test_persist12_patientHighWater_reconstructedFromLoadedStream` |
| ④ | 被折叠病人的 id 只活在 `FoldRow` 里 ⇒ 高水位仍须越过它 | `test_persist12_patientHighWater_includesFoldedRows` |

全部走**真装配根**(`CompositionRoot.Assemble(dataProvider: 真烘焙产物, saveBasePath: 临时目录)`)
+ **生产 `LoadSequencer.afterRestore`**,不手搓重构。

---

## 四、证伪轮

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 删 `afterRestore` 里整段物品重构 | **2 红**(① + ②) | **超集**(两条判据共用同一重构点) |
| ~~**M2**~~ | ~~基线半边实参改 `null`~~ | **0 红 —— 假存活,已作废** | 见 §6.1(结构上不可判) |
| **M3** | 删 `ReconstructPatientHighWater` | **2 红**(③ + ④) | **超集**(同一重构点) |
| **M4** | 漏扫 `stream.FoldedRows`(只扫流) | **1 红**(④) | **exact** |

**结果:3/3 有效变异转红,3/3 SHA-256 逐字节还原**
(每条 `RESTORE-OK (sha=5b582ad5…)` + `FINAL-OK sha 一致`,收尾 `grep MUTATION` = 0)。

⚠️ **M2 的作废是**本轮的**主要发现**,不是瑕疵 —— 它证明我最初写的那条「基线半边参与」
断言**从未测到任何东西**(假绿形态「靶点对但靶场里没有靶」)。处置 = 删断言 + 改判据
+ 变异分支标作废(**保留以便复跑**,承「原文不删」)。

---

## 五、回归

| 套 | 读数 | 文件 |
|---|---|---|
| **EditMode 全量** | **`6594 / 6557 / 0 / 0 / 37`** @ `2026-11-02 13:28:53 CST` | `unity/Logs/tdp12-em-full.xml` |
| **PlayMode 全量** | **`299 / 295 / 0 / 2 / 2`** @ `2026-11-02 13:31:06 CST` | `unity/Logs/tdp12-pm-full.xml` |
| **本件过滤** | `4 / 4 / 0 / 0 / 0` @ `2026-11-02 13:23:42 CST` | `unity/Logs/tdp12-hw5.xml` |

**等式**:`6594 − 6590 = 4` = 本件新增 4 条 `[Test]`(基线 `6590/6553/0/0/37` = 上一批 `S23-18`)。
`passed` 同步 `6553 → 6557`。**PlayMode `+0`**(本轮无 PlayMode 夹具),`failed 0`。

⚠️ **`PM exit=2` 是本仓 PlayMode 常态退出码**(承 `S23-18` 的记录)⇒ 判据取 XML 的 `failed` 计数。

---

## 六、证明不了的事(禁借绿)

### 6.1 原「基线半边参与」的断言**已被证伪轮判为假存活并删除**

原计划有一条判据:「读档后高水位须越过 `world_containers` 的基线容器 id」。
**证伪轮 M2**(把 `afterRestore` 里的基线实参改成 `null`)⇒ **4 条全绿**,即该断言
**零判别力**。根因:

- `IdAuthority.ReconstructItemHighWater` **只升不降**(机制 A 不变量);
- 装配期容器段已把计数器抬到 `baselineMax + 1`;
- ⇒ 读档路径**传不传**基线,结果都是 `baselineMax + 1` ⇒ **结构上不可判**。

这是本项目假绿形态的「**靶点对但靶场里没有靶**」族。处置 = **删除该断言**,
改为 ②「每次读档都重算」(它才是能判的那一半)。**该面今日无独立夹具覆盖**,
如实登记 —— 若将来需要判「基线半边在**读档路径**也生效」,须先让装配期那次抬升
**可观测地**失效(例如注入一个初值为 0 的 `IdAuthority` 重载)。

### 6.2 其余

1. **实机未走查**:证据是真装配 + 真读档路径,不是玩家按「继续诊治」的端到端。
2. **`SaveService` 侧未验**:本件只验 `afterRestore` 钩子的效果,不验
   `SaveService.Load` 自身的字节往返(那归 `world_load_service_test` / `save_slot_load_wiring_test`)。
3. **折叠谓词的生产行为未验**:④ 注入的是**测试替身**(只折指定 id),
   生产 `FoldedPredicate` 的折叠判定归它自己的夹具。
4. **`patient_id` 的基线半边不存在**(受伤实体空间里没有开档既有实体 ——
   `DiseaseOnset` 是唯一的出现事件,ADR-030 ①)⇒ 该半边**结构性为零**,不是漏做。

---

## 七、本轮如实登记的过程事故

| # | 事故 | 处置 |
|---|---|---|
| 1 | 夹具首轮 2 红:同一装配连读两份档 ⇒ `InMemoryBlobPool` 报「同号异内容」 | **不是缺陷**,是池的不可变语义(ADR-006 G-2)。改夹具:第二份档换**新装配**(`NewBag()`) |
| 2 | 夹具第二轮 1 红:折叠行 `FoldedRows.Count = 0` | 根因 = **折叠行只在有终态时产出**(`HistoryFolder.BuildRows` 逐 `outcomes` 建行)。补一条 `PatientOutcomeChanged` |
| 3 | 第三轮 `outcome=8` 被解码拒 | **枚举混淆**:`outcome` 是 `PatientOutcome`(Recovered=1 / Died=2),不是 `InjuryState.Deceased=8`。改 `2` |
| 4 | **M2 假存活**(见 §6.1) | 删断言 + 改判据 + 变异分支标作废(保留以便复跑) |
