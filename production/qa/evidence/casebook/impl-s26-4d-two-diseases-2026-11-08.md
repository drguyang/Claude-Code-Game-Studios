# 实现证据 —— S26-4 续三:`seen ≥ 2` 打通(换词改判的卡点下沉到词表)

**Story**:`production/epics/casebook/story-005-page-layout-focus-navigation.md`
**目标**(用户 2026-11-08 指令):打通第二个病种(`seen ≥ 2`),以解锁「换词改判 / 旧笔同屏」的真包图像。

---

## 0. 结论

| 项 | 状态 |
|---|---|
| **`seen ≥ 2`** | ✅ **打通**。真包两病人各查一遍后 `病名册环 … seen=2 novelty=2`(本轮实测) |
| **换词改判 / 旧笔同屏(真包)** | ❌ **仍不可达** —— 卡点从「单病种」**下沉到词表覆盖** |
| 根因 | `case_judgment_lexicon.json` 4 行里 **2 行是孤儿**(`disease_ids: null`),只有病种 **2 / 10** 各有词;病人拿到的是病种 **2 / 3** ⇒ 病名册仍恒 1 条 ⟹ `TD-casebook-03` |

## 1. 改了什么(为什么这是缺陷,不是「为了让走查好看」)

`PatientAppearedDriver` 此前对**每个**病人都取注册表**同一个** `registry[registryIndex]`:

```csharp
// 修前
var patientId = _spawner.SpawnNext(_diseaseId, tick);   // 恒同一个病种
// 修后
int diseaseId = _diseaseIds[_spawnedCount % _diseaseIds.Length];
```

**这不是为走查改行为**:接线期上限只剩 2 个病人(2026-11-06 用户裁定),两个病人**同病种**
意味着「医师在这个世界上永远只会见到一个病种」—— 那会让整条「判断链 / 病名册 / 改判留痕」
在真包里**结构上不可达**,包括**将来**的真病人。轮转序列以 `registryIndex` 为起点绕一圈,
单条注册表(测试 fixture)退化为原行为。

## 2. 真包读数(可跑)

```
[tutorialdiag] 病人出现 #0 id=103 disease=2 注册表=16
[tutorialdiag] 病人出现 #1 id=104 disease=3 注册表=16
...
A 查体后 | seen=1 pointable=1 词表行=4 novelty=1
B 查体后 | seen=2 pointable=1 词表行=4 novelty=2      ← 本轮目标达成
```

**注册表 = 16 条**(生产用的是烘焙真表 `disease_registry.cooked.bytes`,不是 fallback)。
新增的 `病人出现` 读数只在 `-tutorialdiag` 下打(默认零开销),它回答的正是上一轮查不动的那个问题:
「两个病人是**同病种**,还是**第二次就诊没生效**」——当时只能从 `seen=` 反推,两种因看不出来。

## 3. 为什么换词还差一步(词表,不是代码)

`assets/data/case_judgment_lexicon.json`:

| lexicon_id | 词 | disease_ids |
|---|---|---|
| 1 | 伤寒 | [10] |
| 2 | 湿温 | **null**(孤儿) |
| 3 | 温毒 | **null**(孤儿) |
| 4 | 刺伤 | [2] |

病人拿到病种 2 与 3 ⇒ 可指认的只有「刺伤」一条(`pointable=1`)。⇒ 登记 **`TD-casebook-03`**
(归 37 判断记录 / 内容轮),解除判据 = 真包 `pointable ≥ 2`。

## 4. 判据与证伪

`Tests/EditMode/DiseaseSimulation/patient_disease_rotation_test.cs` **3 条**:
① 两病种注册表 ⇒ 两名病人**不同**病种;② 起点 = `registryIndex`(绕圈不是钳位);
③ 单条注册表 ⇒ 同名(**退化路径**,不得崩/越界)。

**证伪**:把 `_diseaseIds[_spawnedCount % …]` 改回 `_diseaseIds[0]` ⇒ ① ② **恰红**,
③ 仍绿(**正确**:它测的是退化路径)。还原 SHA-256 逐字节一致 `13527301…`。

## 5. 一个探针自己的坑(记下来,别再踩)

首版探针用「离原点曼哈顿距离 min/max」挑两个病人,而两格 `(1,0,14)` 与 `(14,0,1)` **距离相等**
⇒ min 与 max 拿到的是**同一个**病人 ⇒ 两次就诊打同一个人,`seen` 恒 1。
**读数全零先怀疑驱动**——这条纪律又对了一次。

## 6. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7171 / 7134 / 0 / 0 / 37` | `2026-11-08T07:19:20Z` | sprint-26 S26-4 续三 | `9e76fa09` + 工作树 |
| PlayMode | `370 / 366 / 0 / 2 / 2` | 同上 | 同上 | 同上 |

**等式**:`7171 − 7168 = 3` = 新增 `[Test]` 条数(`grep -c "\[Test\]" …patient_disease_rotation_test.cs`)。
