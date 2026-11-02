# 实现原件 —— 掉落进候选集(结清 `TD-interaction-02`)

> **对象**:`docs/tech-debt-register.md` 的 `TD-interaction-02`
> **批次**:`S23-15`(sprint-23 追加;用户指令「修 TD-interaction-02」)
> **截至时刻**:`2026-11-02 09:04 CST`(三元组见 §五)· **commit**:`0c870d22` + 本批未提交工作树
> **结论**:三条消解判据全部兑现;残余六种候选源另立 `TD-interaction-03`。

---

## 一、修的是什么

债册原文:第一源 `IDropSource` 为 `null` ⇒ 掉落**不在候选集里**。拾取只能靠路由的
「**选择器一无所获 ⇒ 兜底 `DropClaimOrchestrator.TryClaim`**」,而 `TryClaim` 自己按
**`instance_id` 升序**取「第一可拾」—— 既不比 `d∞`,也不参与 F-4.1 全序。

后果(玩家可见):**脚边有掉落、旁边有病人或药丛时,按键给后者** —— 掉落要等那两者
都出交互半径才捡得到。这是 4 之外的第二处判据,而 `interaction-system.md` 规则三 /
`F-4.1` 规定「谁是这一帧的目标」只有一处。

---

## 二、改了什么(生产 6 件 + 夹具 3 件)

| # | 文件 | 变更 |
|---|---|---|
| 1 | **新** `Gameplay.Presentation/Interaction/GroundDropSource.cs` | 第一源生产实装:`Rows` = 投影里**未归属**实例 × 最近一次 `DropSpawned.spawn_anchor`;无缓存(派生态);查无出生锚的跳过(不编造坐标) |
| 2 | `Sim/DeathDropReclaimPolicy.cs` | 抽出 **`TryGetSpawnAnchor(events, decoder, instanceId, out anchor)`** —— 锚点面的**唯一定义**,`CanClaim` 与候选源共用(不写第二份扫描) |
| 3 | `Sim/DropClaimOrchestrator.cs` | `TryClaim(actor, tick, cell)` → **`TryClaimInstance(actor, tick, cell, instanceId)`**:只结算被指名的实例;新增 `Tolerance` 只读暴露;头注改写(见 §六) |
| 4 | `Gameplay.Presentation/Interaction/CandidateSetLoader.cs` | `LoadWithin(…, int? dropClaimRadius = null)` —— 装载期就套 20 R7 的回捡半径(与结算同一道门、同一个数) |
| 5 | `Gameplay.Boot/InteractionPressRouter.cs` | **删掉**「选择器空 ⇒ 拾取兜底」;`case Drop:` 改为 `TryClaimInstance(…, target.StableId)`;候选装载传入 `_drops?.Tolerance` |
| 6 | `Gameplay.Boot/CompositionRoot.cs` | `pressCandidates` 的 `drops:` 由 `null` → `GroundDropSource`(投影与拾取编排同源) |
| 7 | **新** `Tests/EditMode/Interaction/interaction_press_drop_test.cs` | **6 条**(见 §三) |
| 8 | **新** `Tests/EditMode/Interaction/ground_drop_source_test.cs` | **3 条**(锚格 / 拾取后立即消失 / 同 id 再落地取最新锚) |
| 9 | `Tests/EditMode/DeathRespawn/drop_claim_pick_test.cs` | 9 条夹具迁到 `TryClaimInstance`(传播种 id);`test_noGroundCandidates_*` → `test_unknownInstance_*`(语义收窄为「这一件不存在」) |

---

## 三、判据对照

| 债册判据 | 载体 | 判据面 |
|---|---|---|
| ① `IDropSource` 有生产实装 | `GroundDropSource` + `ground_drop_source_test`(3 条) | 锚格正确 / **拾取后立刻消失** / 同 id 再落地取最新锚 |
| ② 兜底段删除,`Select` 直接选 Drop | `InteractionPressRouter`;`DropClaimOrchestrator.TryClaim` **整体不存在** | 结构面:`grep "TryClaim("` 零命中 |
| ③ 同格病人 + 掉落按 `KindPriority` 定胜负 | `interaction_press_drop_test.test_sameCell_patientBeatsDrop_byKindPriority` | Patient=0 < Drop=9 |
| (附加)掉落更近就归掉落 | `test_dropCloserThanPatient_dropWinsByDistance` | d=0 的掉落 vs d=1 的病人 ⇒ 拾取(旧形态会去就诊) |
| (附加)结算被选中的那件 | `test_twoDrops_claimsTheSelectedOne_notTheLowestId` | 远件先铸(小 id)、近件后铸 ⇒ 必须拾近件 |
| (附加)回捡半径外的掉落不参与全序 | `test_dropBeyondClaimTolerance_excludedFromCandidates_notSwallowingPress` | d=2 的掉落不得赢过 d=3 的采集点 |

---

## 四、证伪轮(4 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 4/4)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **M1** | 候选源改用 `AllInstanceIds`(不看归属) | 1 红:`test_claimedInstance_disappearsImmediately_noCache` | **exact** |
| **M2** | `TryGetSpawnAnchor` 改正序扫(取第一次出生) | 1 红:`test_reSpawnedSameInstance_anchorIsLatest` | **exact** |
| **M3** | `Within` 的回捡门恒不触发(`d > int.MaxValue`) | 1 红:`test_dropBeyondClaimTolerance_…` | **exact** |
| **M4** | 路由结算 `target.StableId - 1` | 3 红:含 `test_twoDrops_claimsTheSelectedOne_notTheLowestId` | **超集**(另两条红是变异副作用:id=0 减 1 撞参数守卫,不是同一属性) |

### ⚠️ M3 首轮是 **NO-XML**(变异写坏,如实登记)

首版把条件首行改成 `if (false && …)` 而**保留了原第二行的 `&&`**,产出语法错误 ⇒ 编译失败、
无结果文件。**这不是判据红**,是变异本身无效。改为「比较式恒假」(`d > int.MaxValue`)后转红。
⇒ 与 `tools/falsify/README.md` 的「变异打错靶」同族:**NO-XML 一律先按变异错误处理,不当作覆盖结论。**

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-14`) | 等式 |
|---|---|---|---|
| EditMode | `6540 / 6503 / 0 / 0 / 37` | `6531 / 6494 / 0 / 0 / 37` | `6540 − 6531 = 9 = 3(第一源)+ 6(路由)` ✅ |
| PlayMode | `297 / 293 / 0 / 2 / 2` | `297 / 293 / 0 / 2 / 2` | `+0`(本批无 PlayMode 改动) ✅ |

`@ 2026-11-02 09:04 CST` · 批次 `S23-15` · commit `0c870d22` + 工作树。
PlayMode `inconclusive=2` 与基线同形。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/td-i02-em-full2.xml','unity/Logs/td-i02-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode 6540/6503/0/0/37 · PlayMode 297/293/0/2/2
```

---

## 六、覆盖一条已裁定项(S23-6 的「刻意不用就近」)

`DropClaimOrchestrator` 原头注写明:**选中规则 = 第一可拾,刻意不用就近**,理由两条:

> 就近须再取每候选锚点(第二次扫流)且需打破同距平局;P0 口径 = id 序最小者得。

本轮改判为「选择归 4」,依据是**上位规则与事实变化**:

1. **锚点面已有唯一定义** —— `DeathDropReclaimPolicy.TryGetSpawnAnchor`(本批抽出),
   「第二次扫流」不再是障碍,且它与 `CanClaim` 共用**同一份**锚点真源。
2. **同距平局已有解** —— F-4.1 第三键 `StableId`(即 `instance_id`)就是 tie-break;
   选择器本来就在做这件事。
3. **保留「第一可拾」= 4 之外的第二处判据** —— 正是 `TD-interaction-01/02` 的病因形态。

⇒ 原头注已**就地改写**(原文留档于 git 历史),并把「谁铸/谁选」写清楚:选择归 4,
结算归 20。**这不是悄悄改口径**,而是承认「当时的理由已被后来的事实推翻」。

---

## 七、证明不了的事(禁借绿)

1. **「兜底段已删」没有直接观测** —— 只要 Drop 进了候选集,「选择器空」与「有可拾掉落」
   就不再同时成立。最强证据是**结构性**的:第一可拾入口 `TryClaim` 整体不存在。
   `test_noCandidates_false_noDropFallback` 只证「无候选 ⇒ 不消费、零拾取」。
2. **20 R7 宽容半径的实值**(`PickToleranceCells = 1`)仍是装配常量 —— 数值轮可调;
   候选装载与结算读的是**同一个数**(`_drops?.Tolerance`),但「这个值对不对」不在本件面内。
3. **`GroundDropSource` 的复杂度** = 每件地面掉落各扫一遍流(与 policy 同口径)。
   掉落数量大时的节流归 4 的管线后续轮;**本批未做性能夹具**。
4. **`PoiCell` / `Utensil` / `ClinicPanel` / `Door` / `Switch` / `BuildSlot` 六种仍为 `null`**
   —— 开档门柜在**实机**仍不可交互且不报错。已立 `TD-interaction-03`,**本条不借绿**。
   ⚠️ **状态口径要说准**(2026-11-02 核):`AC-4-22` 在 `interaction-system/story-003` 里是 **✅** ——
   但那是**读方形状**用**注入替身**(`candidate_sources_test` 的 `BakedInitial` 假件)签的,
   该 story 自己写着「**不得借绿**:对侧真身联调归各自 Epic」。⇒ 「AC ✅」与「实机门柜可交互」
   不是同一件事;**生产装配仍是零**,不得因 story-003 的 ✅ 把本债判成已解。
5. **`DropDto` 的 `Anchor` 是出生格**,不是掉落实体的表现态位置 —— 若将来掉落会滚动 / 掉落,
   候选集与拾取会对不上;ADR-009 §五 已把位置定为表现态,该风险归表现层。
