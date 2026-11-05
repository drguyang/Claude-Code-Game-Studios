# 实现轮证据件 —— `TD-randomevents-03`(52 事件 → 37 病种的映射与端到端)

**日期**:2026-11-05 · **轮次**:sprint-24 · **commit**(回归时点):`be723016` + 工作树
**权威件**:`design/gdd/random-events.md` 规则二(池条目 schema,`:138-150`)· 规则十一(P0 逐档内容)·
规则十二 12.2(出诊类)· ADR-039 · ADR-016 §一(决策可重建)· 创意总监 2026-11-05 的病种名单裁定

> **一句话**:池条目加**病种外键 `disease_id`**,`spawnPatient` 缝从此有「生成哪个病」的答案;
> 三个判据提成生产类 `EventPoolPatientSpawner`,夹具执行同一份而不是照抄。
> **⚠️ 端到端在「数据与判据」这层已通;「玩家真走到出诊路径」仍须人工走一局** —— 见 §六。

---

## 一、解除动作逐条

| # | 解除动作(债册原文) | 落点 | 状态 |
|---|---|---|---|
| ① | 52 侧出 `52_event_pool.json` | 已在 `TD-randomevents-06` 前落 | ✅(历史) |
| ② | `keysOfTier` 按档过滤该件 | 已在 ADR-039 轮落 | ✅(历史) |
| ③ | **`spawnPatient` 缝的映射表** | 池条目 `disease_id` 外键 + 构建期强制 + 读方同批 | **✅ 本轮** |
| ④ | **真池驱动的端到端夹具** | 6 条(见 §四) | **✅ 本轮** |

---

## 二、映射的形状:为什么是外键,不是独立映射表

一张独立的「key → 病种」表会造出**第二真源**:同一个 key 在池表里出现一次、在映射表里又定一次,
两处可以各自漂移。池条目本来就按 `key` 唯一 ⇒ 外键挂在它身上,**一个 key 只有一处答案**。

构建期强制(`EventPoolBinder`):
- **`handler = medical` ⇒ `disease_id > 0`** —— 「内容还没定」不是合法状态,该条目暂不进池;
- **`handler = encounter` ⇒ `disease_id == 0`** —— 否则打完敌人凭空出现病人。

⚠️ 读方只守形状(`≥ 0`);**语义归 Binder**。这与 `is_house_call` 的两层分工同格。

---

## 三、映射内容(创意总监 2026-11-05 裁定)

| key | 档 / 锚 | `disease_id` | 病种 | 为什么是它 |
|---|---|---|---|---|
| 2002 | 机会 / 旅行路径 / **急召出诊** | 7 | `DIS_MALARIA` 间日疟 | P0 唯一对因手柄(金鸡纳皮)的案子;峰值 0.45 ≥ COMA,真的会昏过去 |
| 2003 | 机会 / 旅行路径 / **急召出诊** | 17 | `DIS_FEBRILE_SEIZURE` 热性惊厥 | 「同一张脸、两个相反答案」的良性对照行;峰值 0.20,永不危殆 |
| 3001 | 反应 / 医馆前 | 11 | `DIS_MEASLES` 麻疹 | 「少做」的出诊(koplik 先于皮疹,认出它就知道不用下重药) |
| 其余 6 条 | — | 0 | — | `encounter`(27 人形遭遇 / 野兽),不产病人 |

⚠️ **卡鱼刺 / 偏头痛未进** —— 创意总监裁定不进 P0(见 `TD-diagnosis-02` / `TD-emergency-01`)。
**镰刀伤 → 破伤风**亦未进:它的正解是「清创」,而 `EmergencyAction` 只有 2 个动作(`TD-emergency-01`)。

---

## 四、夹具(6 条,全部新增)

| 件 | 断言 |
|---|---|
| `RandomEventsConsumptionTest.test_realPool_diseaseIdResolvesToRealRegistryEntry` | **外键闭合**:每条医疗条目的 `disease_id` 都在 `disease_registry.json` 的真实 id 集合里;每条 `encounter` 都是 0 |
| `…test_diseaseIdOfKey_threeStatesAreDistinct` | `>0` / `0` / `KeyAbsent` **三态不可合并** —— 合并会让拼错的 key 静默降级成「不产病人」 |
| `…test_realPool_drivingChain_spawnsPatientWithMappedDisease` | **端到端**:真池驱动 ⇒ 链走到 ⑨ ⇒ 产出的病种 ∈ 池表医疗条目病种集 · 库存有事件 · **每个病人都登记在场** |
| `HouseCallKeysFromPoolTest.test_cookedArtifact_roundTripsDiseaseId` | 产物里的 `disease_id` 与源表逐条相同(读方丢字段 ⇒ 运行期全按 0,而源表看起来完全正确) |
| `…test_medicalWithZeroDisease_isRejected` | 构建期拒收 |
| `…test_encounterWithDisease_isRejected` | 构建期拒收 |

### ⚠️ 端到端那条**执行的是生产代码**,不是镜像

首版夹具把三个闭包**照抄**了一遍 —— 那是「自造断言顶替 AC 语义层」(假绿 F1):
生产侧改坏了,夹具照样绿。本轮把它们提成生产类 **`Gameplay.Boot/EventPoolPatientSpawner.cs`**,
`CompositionRoot` 与夹具**共用同一份**;分叉只可能发生在「有没有调用它」这一个点上。

同类的第二处:`spawnPatient` 里的 `presence.AddPresent` 若被删,链会报 `Spawned`、流里有
`DiseaseOnset`,但 **13 看不见、9 不 Step、37 不开案** —— 链「成功」而世界毫无变化。
**首版夹具走的是假在场面(恒空),永不撞 `PATIENT_APPEARANCE_CAP`** —— 那本身就是「夹具没接真件」
的形态。改用真写面后当场撞上 24 人硬界,窗口由 60 收到 20 次抽取才落回界内。

---

## 五、回归(三元组)

| 项 | 值 |
|---|---|
| **本轮 EditMode** | **`6915 / 6878 / 0 / 37`** |
| **本轮 PlayMode** | **`341 / 337 / 0 / 2 / 2`**(total/passed/failed/skipped/inconclusive;生产代码与本轮同) |
| **基线 EditMode** | `6909 / 6872 / 0 / 37` @ `2026-11-05` · 惊厥轮 · `be723016` |
| **增量等式** | `6915 − 6909 = 6` = 新增夹具数(3 + 3)· `6878 − 6872 = 6` · **failed 0** |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/ed-re03-final.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

**产物**:`52_event_pool.cooked.bytes` 重烘 = **364 B**(328 → 364,条目 32 → 36 B)·
9 条 · `ConfigVersion = 0x5BCD9E9B`。

---

## 六、证伪轮(同命令内 改→跑→还原 · SHA-256)

**驱动器**:`$CLAUDE_JOB_DIR/tmp/falsify_re03.py`。**判红 = 指定靶测出现在 `Failed` 集合里**。
靶名全部取自实跑 XML(**上一轮的教训**:按文件名猜类名/命名空间会全打偏)。

| # | 变异 | 期望红的靶点 | 判定 |
|---|---|---|---|
| M1 | `DiseaseIdOfKey` 恒返回 0 | `…threeStatesAreDistinct` + `…spawnsPatientWithMappedDisease` | ✅ |
| M2 | Binder 去掉「medical ⇒ > 0」 | `…medicalWithZeroDisease_isRejected` | ✅ |
| M3 | Binder 去掉「encounter ⇒ 0」 | `…encounterWithDisease_isRejected` | ✅ |
| M4 | 读方解码时丢弃 `disease_id` | `…cookedArtifact_roundTripsDiseaseId` | ✅ |
| M5 | 生产类删掉 `presence.AddPresent` | `…spawnsPatientWithMappedDisease` | ✅ |

**还原判据**:四份文件 SHA-256 与变异前**逐字节相同**(`sha256sum -c pre-falsify-re03.sha256`)。

---

## 七、本件**没有**证明的事(禁借绿)

- **「玩家真走到出诊路径」未验证** —— `isOnExpedition` 要玩家带着活跃出诊目标跨出医馆格。
  那是一次**人工走局**,本件只证「数据与判据齐了、链能走到 ⑨ 并产出正确病种」。
- **`PatientAppearedDriver` 脚手架仍在跑** —— 每 60 tick 从 `registry[0]` 刷一个病人。
  ⇒ **「看到一个病人」不能证明是 52 产出的**。但事件流能区分:脚手架的 `DiseaseOnset` 带
  `INJ_STAB`(id 2),52 的带 7 / 17 / 11。**手工验证按 `disease_id` 归因,不按「有没有病人」归因。**
  脚手架退役是另一件独立决定,本件不动它。
- **GDD 规则二「52 不拥有任何内容字段」与本字段的关系** 已就地加注(`random-events.md`),
  **那是解释不是新裁决** —— 若判定「外键也算内容字段」,该字段须迁到 37 持有的独立映射件。
- **`DIS_MALARIA` 峰值 0.45 与 `COMA` 阈值(0.449997)的余量仅 −3×10⁻⁶** —— 仍未复核
  「疟疾能不能昏过去」取决于阈值比较用 `≥` 还是 `>`。
