# 实现轮证据件 —— TD-randomevents-06(出诊类 `event_key` 集从数据派生)

**日期**:2026-11-05 · **轮次**:sprint-24 · **commit**(回归时点):`fae861ee` + 工作树
**权威件**:`design/gdd/random-events.md` 规则十二 12.2(`:563`)/ 规则二(池条目 schema)/
规则十一 P0 逐档内容 · ADR-014(两阶段烘焙 · 未知键硬失败 · 玩家构建零 JSON 解析器)

> **一句话**:把硬编码的 `Array.Empty<int>()` 换成**从池表派生**,并给池条目加一个**必填**布尔
> `is_house_call`(走 ADR-014 烘进产物)。**52 侧机制完成;内容侧(37 定稿)仍开** ⇒ 债 `partial`。

---

## 一、解除动作逐条

| # | 解除动作(债册原文) | 落点 | 状态 |
|---|---|---|---|
| ① | 37 内容件交付急召出诊条目的 `event_key` | 归 37 的 GDD 轮 | **❌** |
| ② | 池条目加 `is_house_call` 标记,走 ADR-014 烘焙 | `assets/data/52_event_pool.json`(**必填布尔**,9 条全给)· `Editor.Tools.Bake/EventPoolBinder.cs`(白名单 + 类型校验)· `EventPoolCookedWriter.cs`(`28 → 32 B/条`)· 产物 `schema_version 1 → 2` | **✅** |
| ③ | `CompositionRoot` 的 `houseCallEventKeys` 从池表派生 | `Sim.Contracts/EventPoolDataSet.cs` **`HouseCallKeys()`**(提成方法 —— 装配层内联循环没有落点可测)· `CompositionRoot` 一行调用 | **✅** |
| ④ | 夹具:集合非空 ⇒ `ActiveHouseCallQuery` 真 ⇒ `HouseCallStarted` 可落流 | 新 `house_call_keys_from_pool_test` **8 条** + 既有 `house_call_test` 20 条 | **✅** |

**契约/格式变更(逐条)**:

| 项 | 改前 | 改后 |
|---|---|---|
| 作者态字段 | 7 个 | **8 个**(`is_house_call` 必填,JSON 布尔) |
| `schema_version` | 1 | **2** |
| 条目宽度 | 28 B(7 × i32) | **32 B**(8 × i32) |
| 产物大小 | 272 B | **328 B**(20 头 + 20 载荷头 + 9 × 32) |
| `HouseCallEventKeys` | 硬编码 `Array.Empty<int>()` | `eventPool.HouseCallKeys()` |
| `ConfigVersion` | — | `0xC1768C4D`(源内容哈希派生,ADR-014 §五) |

**为什么 `is_house_call` 必填而不是默认 `false`**:默认 `false` 会让「37 还没定内容」与
「37 定了、说这条不是」**不可分** —— 前者该等,后者该停。缺字段 ⇒ 构建期具名拒收。

---

## 二、当前值(如实标注:**不是 37 的定稿**)

| key | tier | anchor | handler | `is_house_call` | 依据 |
|---|---|---|---|---|---|
| 2002 | opportunity | travel_path | medical | **true** | 52 按规则十一 P0 占位语义给的初值 |
| 2003 | opportunity | travel_path | medical | **true** | 同上 |
| 3001 | reaction | **clinic_front** | medical | false | 「门外有人」—— 不用走出去,不是出诊 |
| 其余 6 条 | — | — | encounter | false | 敌人遭遇,不产病人 |

⚠️ 判据不是「52 说了算」:37 定稿时**逐条改表**,零代码改动。

---

## 三、回归(三元组)

| 项 | 值 |
|---|---|
| **本轮全量 EditMode** | **`6909 / 6872 / 0 / 37`** |
| **本轮全量 PlayMode** | **`341 / 337 / 0 / 2 / 2`**(与基线**逐位相同**) |
| **基线 EditMode** | `6901 / 6864 / 0 / 37` @ `2026-11-05` · `-05` 轮 · `fae861ee` |
| **基线 PlayMode** | `341 / 337 / 0 / 2 / 2` @ 同上 |
| **增量等式** | `6909 − 6901 = 8` = 新增夹具数(`house_call_keys_from_pool_test` 8 条)· `6872 − 6864 = 8` · **failed 0** |
| **复算命令** | `python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('unity/Logs/editmode-full.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"` |

**产物种子**:`52_event_pool.cooked.bytes` 由 `DataBakeMenu.BakeEventPool` 重烘(328 B,条目 9 条,
`ConfigVersion = 0xC1768C4D`);`cooked_drift_all_artifacts_test` 在全量里 **绿**(源表 ↔ 产物一致)。

---

## 四、证伪轮(同命令内 改→跑→还原 · SHA-256)

**驱动器**:`$CLAUDE_JOB_DIR/tmp/falsify_tdre06.py`。**判红 = 指定靶测出现在 `Failed` 里**
(不是「总数变了」)。窗口内**只改被变异的那一个文件**。

| # | 变异(停用什么) | 期望红的靶点 | 实测失败数 | 靶点命中 |
|---|---|---|---|---|
| M1 | `HouseCallKeys()` 恒返回空(等于改前的硬编码) | `…test_cookedArtifact_roundTripsIsHouseCall`<br>`…test_houseCallKeys_preservesAscendingPoolOrder` | 2 | ✅ 两条皆红 |
| M2 | Binder 丢弃 `is_house_call`(绑定恒 `false`) | `…test_realPool_binder_derivesHouseCallKeys` | 2 | ✅(+`…cookedArtifact_roundTrips…`,同一守卫下游) |
| M3 | Binder 把 `is_house_call` 当**可选**(缺字段静默 `false`) | `…test_missingIsHouseCall_isRejected` | 2 | ✅(+`…nonBooleanIsHouseCall_isRejected` —— 放宽后非布尔也被静默吞掉) |
| M4 | 读方解码时丢弃该字段(产物里有、运行时看不见) | `…test_cookedArtifact_roundTripsIsHouseCall` | 1 | ✅ 恰一条 |

**还原判据**:三文件 SHA-256 与变异前**逐字节相同**(`sha256sum -c pre-falsify6.sha256` ⇒ 三行 `成功`)。

---

## 五、本件**没有**证明的事(禁借绿)

- **端到端仍恒假** —— 集合非空 ≠ `isOnExpedition` 能为真。医疗条目要先产出 `EventArrived`,
  而病人通道今日恒 `PatientSpawnUnavailable`(`TD-randomevents-03`:52 事件 → 37 病种的
  映射表不存在)。**在那之前没有任何出诊类 `EventArrived` 落流。**
- **不证「哪些 key 该是急召出诊」** —— 那是内容判断,本件只证「表里标了几条,派生就是那几条」。
- **不证 `spawn_anchor` 的真实解析** —— 生产仍是 `(0,0,0)` 具名占位(6 的出生锚归 P1a 关卡)。
- **仓库根定位用 `CallerFilePath` 反推五层** —— 与 `cooked_drift_all_artifacts_test` 同法;
  测试树深度若变,该反推会静默指错目录(届时该文件会红,不会静默通过)。
