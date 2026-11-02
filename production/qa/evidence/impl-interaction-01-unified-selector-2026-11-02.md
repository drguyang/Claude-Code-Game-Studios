# 实现原件 —— 统一目标选择轮(结清 `TD-interaction-01`)

> **对象**:`docs/tech-debt-register.md` 的 `TD-interaction-01`
> **批次**:`S23-13`(sprint-23 追加;用户指令「修 TD-interaction-01」)
> **截至时刻**:`2026-11-02 08:00 CST` · **commit**:`d80e57d4` + 本批未提交工作树
> **结论**:三条消解判据全部兑现;残余(`Drop` + 另六种候选源)另立 `TD-interaction-02`。

---

## 一、修的是什么

债册原文:`interaction-system.md` 规则三 / `F-4.1` 说 **4 是全案唯一目标选择器**,
`target = argmin ⟨ d∞, KindPriority, StableId ⟩`,候选来自**全部种类**。实装却是两处互不知情的判据:

| # | 旧形态 | 问题 |
|---|---|---|
| ① | 采集:`HarvestInteractionDriver` 走真 `InteractionSelector`,但候选集**只有资源点** | `CandidateSources.Patients = null` ⇒ 病人从不进候选集 |
| ② | 就诊:`InteractionInputDriver.NearestPresentPatient` 自算「**视觉位置**平方距离最小」 | 浮点 · 表现态(违 ADR-016 §三 / 4 规则二「玩家格取路 (a)」)· 无任何全序键 |
| ③ | 两者之间:`InteractClaimRoute` 的**外挂比距**(2026-11-02 加,只比 `d∞`) | 第三处判据 —— 「谁是目标」共三处在算 |

---

## 二、改了什么(生产 7 件 + 删除 2 件 + 夹具 4 件)

| # | 文件 | 变更 |
|---|---|---|
| 1 | **新** `Gameplay.Presentation/Interaction/PresencePatientSource.cs` | `IPatientSource` 的**首个生产实装**(读 `PresenceRegistry`;`Rows` 每次现算 —— 缓存即第二真源) |
| 2 | `Gameplay.Presentation/Interaction/CandidateSetLoader.cs` | 新增 `LoadWithin(playerCell, radius)` —— 裁剪与 F-4.1 第一键**同式**(先拓宽 int64 再取绝对值),不写第二份 `Chebyshev` |
| 3 | `Gameplay.Boot/HarvestInteractionDriver.cs` | 新增 `TryHarvestTarget(target, intent, actorId)`(**结算已选定目标**);`TryHarvest` 转调它;删掉上一轮加的 `PeekNearestForageDistance`(已被路由取代) |
| 4 | **新** `Gameplay.Boot/InteractionPressRouter.cs` | **唯一路由**:一次 `Select` → 按 `Kind` 分派(Patient → 39 / ForageSpot → 17 / Drop → 20 / 未接线种类 → 不消费) |
| 5 | `Gameplay.Boot/InteractionInputDriver.cs` | `TryVisit()` → **`Visit(PatientId)`**(病人由调用方指名);`interactClaimedByHarvest` 缝 → `routeInteractPress`;新增 `playerCell` 注入,急救 Arm 目标改走**整数格** argmin |
| 6 | `Gameplay.Boot/CompositionRoot(.Services).cs` | 建**全种类** `CandidateSetLoader`(第三人源 = `PresencePatientSource`),与采集自用的那个**刻意是两个实例**;三件挂上服务包 |
| 7 | `Gameplay.Boot/BootRoot.cs` | 4.5a 段建路由并接进驱动;删掉采集桥与比距(以及上一轮的 `NearestPatientCellDistance`) |
| — | **删** `Gameplay.Boot/InteractClaimRoute.cs` · `HarvestInteractionBridge.cs` | 两者只为外挂仲裁而存在;路由接管后是死代码 |
| 8 | **新** `Tests/EditMode/Interaction/interaction_press_router_test.cs` | 7 条(选择 / 分派 / 裁剪 / 病人类候选源) |
| 9 | `Tests/EditMode/Foraging/harvest_interaction_driver_test.cs` | 桥 4 条 → `TryHarvestTarget` 3 条 |
| 10 | `Tests/EditMode/DeathRespawn/drop_claim_pick_test.cs` | 删「E 沿路由」3 条(路由已迁走);20 的拾取本体 9 条保留 |
| 11 | `Tests/PlayMode/modal_gate_wiring_test.cs` | 正控前把玩家送到病人格 —— E 现在**有半径门**(旧「最近病人」兜底无半径门) |

---

## 三、AC / 判据对照

| 债册消解判据 | 载体 | 判据面 |
|---|---|---|
| ① 病人 `ICandidateSource` 有生产实装 | `PresencePatientSource` + `test_patientSource_*`(2 条) | 行数 / 格 / **离场立刻消失**(反缓存) |
| ② 比距仲裁删除,一次 `Select` 定种类 | `InteractionPressRouter.OnPress`;`InteractClaimRoute` 已删 | `test_ir_02/03`(距离先于种类)· `test_ir_04`(无候选) |
| ③ 同格病人 + 采集点按 `KindPriority` 定胜负 | `test_ir_01_*` | 见 §四 N1 |

---

## 四、证伪轮(5 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 5/5)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **N1** | `InteractionSelector.KindPriorityOf` → 恒 `0` | 1 红:`test_ir_01` | **exact** —— 第二键失效即改由第三键判 |
| **N2** | 路由的 Patient 分支不调 `_visit` | 2 红:`test_ir_01` / `test_ir_03` | exact(就诊分派面) |
| **N3** | `LoadWithin` 的裁剪放宽 1000 格 | 1 红:`test_ir_05` | exact(半径门) |
| **N4** | `PresencePatientSource.Rows` 首次即缓存 | 1 红:`test_patientSource_reflectsRemoval_noCache` | exact(反第二真源) |
| **N5** | `TryHarvestTarget` 闸门拒绝时返回 `None` | 3 红:含 `test_tryHarvestTarget_forageTarget_attemptedEvenWhenGateRejects` | **超集**(两条既有闸门测试也断言「拒绝」语义) |

**N1 的判别力是刻意设计的**:夹具让第三键(`StableId`)与胜负**相反**
(病人 99 > 资源点 1)⇒ 第二键没接线时资源点会赢。若两边 id 巧合,「病人赢」在旧实现下也会绿。

**N5 是超集不是 exact**:另外两条既有测试也断言闸门拒绝的语义,故一并转红。对
「拒绝也算消费」这一属性有判别力,但**不得**记成单属性判别。

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-12`) | 等式 |
|---|---|---|---|
| EditMode | `6526 / 6489 / 0 / 0 / 37` | `6523 / 6486 / 0 / 0 / 37` | `6526 − 6523 = 3 = +7 新 − 3 删 − 1(桥 4→3)` ✅ |
| PlayMode | `294 / 290 / 0 / 2 / 2` | `294 / 290 / 0 / 2 / 2` | `+0`(本批只改了一条既有夹具的前置,零新增) ✅ |

`@ 2026-11-02 08:00 CST`(07:56 另跑一轮,数值逐位相同) · 批次 `S23-13` · commit `d80e57d4` + 工作树。
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形)。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/b1-em-full.xml','unity/Logs/b1-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode 6526/6489/0/0/37 · PlayMode 294/290/0/2/2
```

---

## 六、实现期两次红(如实登记)

1. **`modal_gate_wiring_test` 正控红**(PM 1 条):该测为躲开采集点把玩家传送到 `(0,0,50)`,
   并注「查体的最近病人判定**无半径门**」。路由接管后 E **有**半径门(`R_INTERACT = 3`)
   ⇒ 半径外的病人不被选中,正控假红。**改夹具不改生产**:正控前把玩家送到病人格。
   ⇒ 这条同时证明**行为确实变了**:旧路径能隔着 50 格「查体」,新路径不能 —— 后者才是设计。
2. **`HarvestInteractionBridge` 删掉后 4 条夹具编译红**:它们测的是「采集自己再选一遍」。
   判据改挂 `TryHarvestTarget`(结算已选定目标),断言不变。

---

## 七、证明不了的事(禁借绿)

1. **10 种种类只接了 2 种** —— `Drop` 走 20 自己的「第一可拾」,另六种源为 `null`。
   已立 `TD-interaction-02`,**本条不借绿**。
2. **`LastRoutedKind` 是观测位**,夹具据它断种类;它不参与任何判定(改坏它不会改行为 —— 那是**有意**的)。
3. **`PresencePatientSource` 的格来自 `PresenceRegistry`**,而该登记簿的写入方(13 / 9)是否正确同步
   **不在本件面内**;本轮只证「读面拿到的就是登记簿里的格」。
4. **急救 Arm 目标改整数格后未新增夹具** —— 既有 `interaction_input_chain_test` 仍走
   「未注入 `playerCell` ⇒ 视觉位置回落」那条夹具分支,生产分支(注入格)的最近者选择
   **只有 `modal_gate_wiring_test` 的间接覆盖**(那是 E 不是 F)。
5. **等距跨种类之外仍无 KindPriority 的数值轮定案** —— 表值仍是 `F-4.1b` 演示序原样。
