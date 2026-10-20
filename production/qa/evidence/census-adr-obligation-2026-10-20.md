# 普查原件:ADR 义务符号 vs 代码(§八 #3′)

**三元组**:`2026-10-20T07:59` UTC · §八 #3′ 普查轮 · `54fd5e5`
**触发**:`production/milestones/p0-remaining-roadmap-2026-10-20.md` §八 失败条件 **#3 / #3′**
(「又出现 epic 全 story Complete 但契约零命中」⇒ 门清单不完备,须**重做一次全 epic 契约普查**)。
**判据出处**:本件是 #3′ 的**兑现件**;在其被复核前,#3 的判据保持红。

> ⚠️ **本件是勘察,不是修复。** 它只产出「哪些 epic 名下的 ADR 义务符号在代码里零落点」的
> **可复算清单**与**分类**。**未改任何代码 / GDD / ADR / 卡面。**

---

## §1 扫描面声明(先声明,后下结论 —— §十一 元教训)

| 面 | 声明 |
|---|---|
| **母体** | `production/epics/` 下**全部有机 story 的 epic 目录** = **31** 个 |
| **子集** | 其中「**全 story `Complete`**」的 = **17** 个(判据见 §2.1) |
| **符号源 A** | 31 份 `docs/architecture/adr-*.md` 的**围栏代码块** ⇒ **52** 个契约类型符号 |
| **符号源 B** | 同批 ADR 的**散文反引号符号** ⇒ 379 个(去噪后) |
| **代码面** | `unity/Assets/` **全树** C#;`Tests/` **单列一档**(见 §4.1) |
| **注释** | **剥除**(块注释 + 行注释);判「存在」须落在**代码行**上 |
| **判定** | 独立词边界匹配(防子串假命,如 `Fold` 命中 `Folded`) |

**⚠️ 本件只在上述面内成立。** 面外的符号(如 `packages/` 内、`.yml` CI 脚本、Unity 引擎 API)
不在计数内;`SetAdditionalIl2CppArgs` / `CleanupSceneInstances` 一类的零命中属**面外噪音**,已剔。

---

## §2 方法

### 2.1 「全 story Complete」的判据

**取文件头 blockquote 行** `> **Status**: …`(实测为各 story 的**权威**状态行,位于第 4 行左右)。
`Complete` 子串命中 **且** 不含任一非完成词
(`部分` / `In Review` / `In Progress` / `Blocked` / `Ready` / `Draft` / `Not Started` / `Proposed` / `Deprecated` / `WIP`)
⇒ 记 `Complete`。

> ⚠️ **只数 `Complete` 子串是错的** —— `audio-system`(21/21)与 `skeuomorphic-ui`(25/25)的
> 每一条都含 `Complete` 子串,但状态串同时含非完成词 ⇒ **正确判为未全 Complete**。这与二者
> 已知的假绿/部分完成状态一致,是判据的自检。

**结果 = 17 个**(逐目录见附录 A)。

> ⚠️ **与前轮引用的 18 差 1。** 本轮判据可复算 ⇒ **17 为本轮权威值,前轮的 18 作废**;
> 差的那一件**未复核**(属本普查自身的误差棒之一,见 §4.2)。

### 2.2 两级扫描

1. **代码块级**(52 符号)—— 全部逐符号扫。
2. **散文级**(379 符号)—— 补代码块级**结构上扫不到**的符号。
   立级理由:G-1(`SelfLimited`)与 G-2(`Folded(p)`)两条**历史命中**都**出自散文而非代码块**
   ⇒ 只扫代码块会**系统性漏掉它们那一类**。

---

## §3 结果

### 3.1 代码块级(52 符号)

**37 有落点 / 8 零命中**:`IReplayPipe` · `FocusDirection` · `IEmergencyInput` ·
`IPerTargetIl2CppArgs` · `ISkeuoElementLibrary` · `IPlayerMotor` · `ISceneRouter` · `IWorldSpawner`。

### 3.2 散文级(379 符号)

**286 有落点 / 93 零命中**。其中绝大多数是面外/无关噪音(Unity 与 BCL 类型、log 方法名、
文件名、GDD 字段名)。**逐条分类如下。**

### 3.3 分类表(代码块级 + 散文级**合并去重**)

| 类 | 数 | 项 | 处置 |
|---|---|---|---|
| **① 真缺口** | **12** | `IReplayPipe` · `ReorderBuffer` · `IPlayerMotor` · `IPerTargetIl2CppArgs` · `SelfLimited` · `INavLattice` · `LogiPose` · `PerceptionView` · `PickupIntent` · `DeferredThreatSlot` · `ITameable`/`TameState` · `ShoulderRig`/`FollowRig` | 见 §6 |
| **② 命名漂移** | 5 | `FocusDirection`→`FocusNavigationDirection`(成员亦异:`Previous`/`Next`→`PageUp`/`PageDown`,增 `None`)<br>`IEmergencyInput`→具体类 `EmergencyDirectReadChannel`<br>`ISkeuoElementLibrary`→`ISkeuoComponentLibrary`<br>`FixPow`/`FixSqrt`/`Fix.ISqrt`→`Fix.Pow`/`Fix.Sqrt`<br>`ModalOpen`→`IModalState.Modal` | 记录,**本轮不动**(未获裁定) |
| **③ 零命中正确** | 7 | `IEventEncoder`(ADR-029 择 `IPayloadEncoder`)· `IPayload`(ADR-029 明删)· `WorldSeedCreated`(ADR-007 Alt 2 否决)· `FixSet`(ADR-008 订正为 `DiseaseIdSet`)· `PlayerId`(幽灵类型,ADR-024 §② 降级)· `Int128`/`BigInteger`(ADR-005 G **明令禁**)· `PatientAppeared`(ADR-030 Q1 甲案**不立**) | 无需动作 |
| **④ 显式延后** | 2 | `ISceneRouter` · `IWorldSpawner`(`BootRoot.cs:20` 逐字「刻意不抽 `ISceneRouter`:Phase 1 只有一个 World 场景」+ ADR-023 §Validation **S2** `⏸ 延后`) | 无需动作 |
| **⑤ 面外/无关** | 余 | `Animator`/`Rigidbody`/`NavMeshAgent`/`HttpClient`/`ThreadPool`/`Hash128`/`TerrainData`/`UnityWebRequest`/`SetAdditionalIl2CppArgs`/`CleanupSceneInstances`/`CinemachineCamera`/文件名引用/GDD 字段名 | 剔除 |

### 3.4 ① 类中「够格成为 G 型债」的判别

G 型债的定义(**本册既有口径**)=「**ADR 已裁 · GDD 已写 · epic 已标 `Complete` · 代码零落点**」。
⇒ 逐项须同时满足:①符号零命中 **∧** ②**归属 epic 全 story `Complete`** **∧** ③**该 epic 卡面自己点名了该符号**。

| 符号 | 归属 epic(实测状态) | epic 卡面点名处 | 够格? |
|---|---|---|---|
| **`IPlayerMotor`** | `player-controller` **6/6** · `camera-viewpoint` **6/6** | `player-controller/story-006-state-purity-motor-lease.md` · `camera-viewpoint/story-003-anchor-follow-orbit.md` · `…/story-001-presentation-discipline.md` | ✅ **双向点名** |
| **`LogiPose`** | `enemy-ai` **5/5** | `enemy-ai/story-003/004/005` · `patient-ai/story-003` | ✅ |
| **`ITameable` / `TameState`** | `enemy-ai` **5/5** | `enemy-ai/story-005` · `enemy-ai/EPIC.md` | ✅ |
| **`DeferredThreatSlot`** | `random-events` **6/6** | `random-events/story-004` · `…/story-002` · `…/EPIC.md` | ✅ |
| `SelfLimited` | `disease-simulation` 7/7 | (已有) | ✅ = **已登记 `TD-sim-01`**,不重复 |
| `INavLattice` | `world-ecozones` **5/6** | 卡面**零点名** | ❌ epic 未全 Complete |
| `PickupIntent` | `foraging` **0/5** | 卡面**零点名** | ❌ epic 未全 Complete |
| `ShoulderRig`/`FollowRig` | `camera-viewpoint` 6/6 | 卡面**零点名** | ❌ 仅 ADR-020 声明 |
| `PerceptionView` | `enemy-ai` 5/5 | 卡面**零点名** | ❌ 仅 ADR-016 声明 |
| `IPerTargetIl2CppArgs` | **无归属 epic** | — | ❌ 孤儿(见 §5) |
| `IReplayPipe`/`ReorderBuffer` | **无归属 epic** | — | ❌ 孤儿(见 §5) |

> **判定口径说明(这是一条判断,不是实测)**:❌ 各行**不是债** —— 它们所属的 epic **尚未全 Complete**
> ⇒ 那是**未开工**,给它记债等于给没开始的活记账。**`IPerTargetIl2CppArgs` 与 `IReplayPipe` 两条
> 归属面另有问题(§5),不属此判。**

---

## §4 本普查自身的误差棒(必须先亮出来)

### 4.1 三处**假零** —— 由扫描面选择造成,与 G-3 同型

| 符号 | 真相 | 假零成因 |
|---|---|---|
| `GoldenHashV1Test` | **存在** —— `Tests/EditMode/Sim/golden_hash_v1_test.cs:24` | 生产-only 扫描把「**测试树义务**」误判为缺口。**ADR-012 / ADR-023 的自称属实**(「43 条新增 + 既有 16 = 59 绿」) |
| `StructureKinds` | **存在** —— `Sim/World/StructureKinds.cs` | ADR-029 引的是**文件名**而非类型符号;该符号只在**注释**里出现 |
| `FixPow` / `FixSqrt` | **存在** —— `Fix.cs:191/212/240`(`Fix.Pow` / `Fix.Sqrt`;`Fix.cs:264` 注释逐字写「ADR-026 FixPow/FixSqrt 同族先例」) | ADR-026 声明的**成员名**与代码**成员名**不同(⇒ 归 ② 类漂移,非缺口) |

⇒ **教训(与前次同型)**:「按标识符 grep 零命中」**只在①扫描面覆盖全树 ∧ ②符号名与代码名一致**时成立。
本轮的 `Tests/` 单列档 + 「文件名 ≠ 符号」两条修正即由此而来。

### 4.2 计数差 1

前轮引用 **18** 个全 Complete epic,本轮可复算得 **17**。差的那一件**未复核**
⇒ 本轮取 17,**并声明 18 作废**。

### 4.3 「未逐条溯源」桶(**未算进 ①**)

以下符号全树零命中,但**其声明 ADR 是否构成 P0 义务、以及归属谁,本轮未逐个核实** ⇒ **不得算进 ①**:

`QualityCap` · `FiredSet` · `DeathAllowed` · `RegrowWindow` · `HistoryMult` · `ReputationMult` ·
`WeaponMultiplier` · `OwnerId` · `ItemId` · `InventoryOf`

多为 GDD 侧字段名,与 ADR `Key Interfaces` 不同级。**如实留白,不臆断。**

---

## §5 结构性发现:**① 类多数项指向「不存在的 epic」**

`production/epics/` **实测无网络 epic、无场景/Boot epic**(`ls production/epics/` 全表见附录 B)。

⇒ ADR-001 的 **系统 45**(`IReplayPipe` / `ReorderBuffer`,逐字写「P0 预埋…**P0 全实现为本地占位**」)
与 ADR-023 的一对(`ISceneRouter` / `IWorldSpawner`)**没有归属 epic**。

**后果(本普查最重要的结论)**:§3.4 的判别须「**归属 epic 全 Complete**」为前提,
而孤儿契约**连 epic 都没有** ⇒ **G 型普查手法对它们结构上不可见**。
这正是 §十一 **C-4** 所述的盲区,本轮**实测坐实**。
⇒ ⇒ **§八 #3 的门清单若要完备,须再补一条「无归属 epic 的 ADR 契约」专扫**,本件**未做**。

---

## §6 处置(承用户裁定 2026-10-20)

**用户裁定**:① 写「证据件 + 路线图 §八」;② ① 类中**能归 epic 的登记 TD**,不立 story。

### 6.1 本轮登记(§3.4 的 4 项够格命中)

| 新 TD | 符号 | 归属 | 形态 |
|---|---|---|---|
| **`TD-player-02`** | `IPlayerMotor` | 1 玩家控制器(**+ 2 相机为下游消费者**) | epic 已 Complete · 契约零落点。⚠️ **编号非 `-01`**:`TD-player-01` 是既有另一条债(`AC-1-33` 的 `minMoveDistance`),不合并 |
| **`TD-enemy-01`** | `LogiPose` · `ITameable` · `TameState` | 27 敌人 AI | 同上 |
| **`TD-randomevents-01`** | `DeferredThreatSlot` | 52 随机事件导演 | 同上 |

**不重复登记**:`SelfLimited` = 已登记 `TD-sim-01`(本件只作交叉引用)。

### 6.2 记录但**不动作**(未获裁定 / 不属债)

- **② 类 5 项命名漂移** —— 留档,ADR 措辞回刷待裁。
- **④ 类 2 项显式延后** —— `ISceneRouter` / `IWorldSpawner`,已有出处与触发条件。
- **§3.4 的 ❌ 行** —— epic 未全 Complete ⇒ **未开工,非债**。
- **§5 的孤儿契约**(`IReplayPipe` / `ReorderBuffer` / `IPerTargetIl2CppArgs`)——
  **本件未处置**;待裁「是否另立孤儿契约登记节」。
- **§4.3 未溯源桶** —— 未核实,留白。

---

## §7 可复算命令

```bash
# 全 story Complete 的 epic(判据:文件头 blockquote Status 行)
python3 -I - <<'PY'
import glob,re,os
NC=['部分','In Review','In Progress','Blocked','Ready','Draft','Not Started','Proposed','Deprecated','WIP']
n=0
for ep in sorted(glob.glob('production/epics/*/')):
    sts=sorted(glob.glob(ep+'story-*.md'))
    if not sts: continue
    rows=[]
    for f in sts:
        for ln in open(f,encoding='utf-8',errors='ignore').read().split('\n')[:12]:
            m=re.match(r'^>\s*\*\*Status\*\*:\s*(.+)$',ln.strip())
            if m: rows.append(m.group(1).strip()); break
    if rows and all('Complete' in r and not any(k in r for k in NC) for r in rows):
        n+=1; print(os.path.basename(ep.rstrip('/')))
print('TOTAL =',n)   # 期望 17
PY

# 单符号三级判定(生产 / 含 Tests / 全树零)
grep -rn "\bIPlayerMotor\b" unity/Assets --include='*.cs'   # 期望:0 命中
```

---

## 附录 A —— 17 个「全 story Complete」的 epic(本轮权威值)

`camera-viewpoint`(6/6) · `case-system`(6/6) · `combat-weapons`(6/6) · `diagnosis-system`(6/6) ·
`disease-simulation`(7/7) · `enemy-ai`(5/5) · `input-system`(13/13) · `interaction-system`(7/7) ·
`item-database`(12/12) · `modular-building`(7/7) · `persistence-service`(2/2) · `player-controller`(6/6) ·
`prescription-medication`(5/5) · `random-events`(6/6) · `skill-system`(8/8) · `telemetry-analytics`(10/10) ·
`time-weather`(5/5)

**未全 Complete 的 14 个**(正确排除,含两个已知假绿):
`audio-system`(21/21 数满但含非完成词)· `skeuomorphic-ui`(25/25 同)· `casebook`(0/6) ·
`clinic-machine`(0/5) · `death-respawn`(0/6) · `emergency-procedures`(8/9) · `foraging`(0/5) ·
`inventory-items`(0/6) · `medical-consequences`(3/4) · `patient-ai`(4/5) · `processing`(0/5) ·
`save-slot-ui`(0/1) · `tutorial-onboarding`(0/5) · `world-ecozones`(5/6)

## 附录 B —— `production/epics/` 全表(31 个,证 §5「无网络 / 无场景 epic」)

`audio-system` · `camera-viewpoint` · `case-system` · `casebook` · `clinic-machine` · `combat-weapons` ·
`death-respawn` · `diagnosis-system` · `disease-simulation` · `emergency-procedures` · `enemy-ai` ·
`foraging` · `index.md` · `input-system` · `interaction-system` · `inventory-items` · `item-database` ·
`medical-consequences` · `modular-building` · `patient-ai` · `persistence-service` · `player-controller` ·
`prescription-medication` · `processing` · `random-events` · `save-slot-ui` · `skeuomorphic-ui` ·
`skill-system` · `telemetry-analytics` · `time-weather` · `tutorial-onboarding` · `world-ecozones`

⇒ **无 network / pipe / scene / boot 目录**。
