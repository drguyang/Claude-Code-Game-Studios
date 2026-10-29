# 实现证据 —— 听测前置解除批(`TD-listen-01` L-2 + AC 人数改判)

- **日期**:2026-10-29
- **批次**:音频 5 债结案前置轮(用户裁定「4 处前置全修 + AC 原文 `n ≥ 6` → `n ≥ 2`」)
- **对象**:`CompositionRoot.cs` / `BootRoot.cs` / `design/gdd/audio-system.md` /
  三份听测协议件 / 两份 story 卡 / 债册 `TD-listen-01`

---

## 一、AC 原文改判(`n ≥ 6` → `n ≥ 2`)

**用户 2026-10-29 裁定**:修改 AC 原文,双盲人数由 `n ≥ 6` 改为 `n ≥ 2`。

### 1.1 实测:原判据只在一处,不是两处

| 位置 | 原文 | 判 |
|---|---|---|
| `design/gdd/audio-system.md:1173` **AC-44-17** | `n ≥ 6 人 × 每对 ≥ 8 次` | ✅ **唯一权威出处** |
| **AC-44-02**(`audio-system.md:1034-1036`) | **零人数要求** | ⚠️ 债册 L-5 声称它「原文写 n ≥ 6」是**错的** |
| `breath-layers-listen-evidence.md:25` | `n ≥ 6 人 × 每对 ≥ 8 次` | ⚠️ **协议件自加**,不是 GDD 判据 |

⇒ **账面滞后第 10 例**:债册 L-5 原文「AC-44-02 / AC-44-17 原文硬要求」把两者混为一谈。
已在债册就地订正(原文划线保留)。

### 1.2 落点(五处同步,缺一即口径分叉)

| # | 文件 | 改动 |
|---|---|---|
| R-1 | `design/gdd/audio-system.md:1173` | `n ≥ 6 人` → **`n ≥ 2 人`** + 修订记录块(原文值保留) |
| R-2 | `breath-layers-listen-evidence.md` | 3 处 `n ≥ 6` → `n ≥ 2` + 注明「AC-44-02 原文本就无人数要求」 |
| R-3 | `main-channel-listen-evidence.md` | 2 处 `n ≥ 6` → `n ≥ 2` |
| R-4 | `story-013-main-channel-listen-acceptance.md` | 4 处 `n ≥ 6` → `n ≥ 2` |
| R-5 | `story-004-breath-layers-precision-tiers.md` | 1 处 `n ≥ 6` → `n ≥ 2` |
| R-6 | `docs/tech-debt-register.md` **L-5** | 判据改 `n ≥ 2` + 两处订正注 |

**未动**(明示裁定边界):
- 判对率门槛 `≥ 75%`(总)/ 每对 `≥ 65%` —— **不变**
- 试次下限 `每对 ≥ 8 次` —— **不变**
- 被试「不参与素材制作」—— **不变**

> ⚠️ **降格是明示裁定,不是判据被稀释** —— 判据本体的三个承重参数里只动了人数。

---

## 二、L-2 听诊档位切档缝

### 2.1 病因(实测复核)

`CompositionRoot` 的 `queryLevel` 是**局部常量 `1`** 且全库零回写
⇒ `DiagnosisReadFloorEvaluator.TierIndex(1) = 0`(`SLOT_BOUNDS = {10,20,50}`,`1 < 10`)
⇒ 听诊**恒最低档** ⇒ `AC-44-02` 的三档 forced-choice **没有第二 / 第三档可听**。

### 2.2 修法

装配期**可选形参** `diagnosisQueryLevelOverride`:

| 落点 | 内容 |
|---|---|
| `CompositionRoot.Assemble`(两重载 + `AssembleCore`) | 新增 `int? diagnosisQueryLevelOverride = null` |
| `CompositionRoot` 的 `queryLevel` 赋值 | `const int DefaultDiagnosisQueryLevel = 1;` + `queryLevel = diagnosisQueryLevelOverride ?? DefaultDiagnosisQueryLevel` |
| `BootRoot.ResolveDiagnosisQueryLevelOverride()` | 读环境变量 `DYJC_DIAG_QUERY_LEVEL`(技能等级);未设/非法 ⇒ `null` |
| `BootRoot.Start` 的 `Assemble` 调用 | `diagnosisQueryLevelOverride: ResolveDiagnosisQueryLevelOverride()` |

**⚠️ 传的是技能等级,不是档序** —— 要听三档须传 `1` / `10` / `20`。
传 `0/1/2` 会全部落档 0(那正是原故障形态)。这一点由夹具
`test_overrideIsSkillLevel_notTierIndex` 专门钉住。

### 2.3 为什么是装配期形参,不是静态可变缝

静态缝跨夹具残留(本项目已因此踩过多次,承 `feedback_falsify_concurrent_edit_window`
同族纪律);形参的作用域天然是「这一次装配」,且不引入第二个真源。

**为什么是环境变量,不是按键 dev 开关**:本仓**零 dev 开关先例**
(`grep DevSwitch/DebugSwitch` 全仓无生产命中),新造一套交互 = 新立机制;
环境变量在**启动前**设定,且对 109 处 EditMode 调用方**结构性不可达**
(它们不经 `BootRoot.Start`)⇒ 零回归风险。

### 2.4 夹具 `Tests/EditMode/Boot/diagnosis_query_level_override_test.cs`(3 条)

| 测 | 钉什么 |
|---|---|
| `test_noOverride_keepsLegacyLowestTier` | 未给覆盖 ⇒ 档序仍为 0(**零行为变化**) |
| `test_override_reachesAllThreeTiers` | 技能等级 `1/10/20` ⇒ 档 `0/1/2`(**档真的变了**) |
| `test_overrideIsSkillLevel_notTierIndex` | 技能等级 `2` ⇒ 仍档 0(钉住形参语义) |

判据面 = `AudioTierDriver.CurrentTier`(**听测时耳朵听到的那个档**),不是「形参被赋值了」。

### 2.5 证伪轮 —— **3/3 全红**

驱动脚本 `/home/gu/.claude/jobs/181888b1/tmp/falsify_l2.py`(每变异独立 Unity 启动;
前置核对盘上无 `[MUTATED]`;还原后 SHA-256 逐字节校验)。

| 变异 | 打靶判据 | 实测 |
|---|---|---|
| M1 无视形参(`?? 1` 删掉,回到常量) | `test_override_reachesAllThreeTiers` | **RED 1/3** ✅ |
| M2 形参语义改成档序(`× 10`) | 三条全红 | **RED 3/3** ✅ |
| M3 默认值改错(`?? 10`) | `test_noOverride_keepsLegacyLowestTier` | **RED 1/3** ✅ |

**基线 3/3 绿;还原后 SHA-256 = 变异前(`OK`)。**

> ⚠️ M2 红 3/3 而非 1/3 是**正确**的:把形参乘 10 后,「未给覆盖」也从档 0 变成档 1
> ⇒ 三条判据全部失守。这证明三条夹具不是同一个断言的复述。

---

## 三、AC-44-15 字幕 42 侧 —— **不是缺口,是已裁分工**

用户裁定「修」,但实测结论与「缺口」判断相反:

| 事实 | 出处 |
|---|---|
| 42 侧字幕 = **「接口存在,不在此实现」**;具体要求归 **49(P2)** | `skeuomorphic-ui.md:288-294` 规则十 |
| `OQ-42-8` **已于 2026-10-06 结案**:42 侧四钩子已全部具名,具体要求归 49 | `skeuomorphic-ui.md:1979` |
| `E-12b` 的落点 = 「文本呈现走元件层文本组件」 | `skeuomorphic-ui.md:997-1000` |
| 42 元件库**有** `Label` 文本组件(`AudioBusEntry` / `EmptyRowElement` 等在用) | 实测 `grep` |
| **无**专供字幕的组件 | 实测 `grep subtitle` 在 `Gameplay.UI` = **0 命中** |
| AC-44-15 的 **[A] 半**有构建期门(键集覆盖 + 值非空) | `AudioEventTableGates.cs:1565-1591` |

⇒ **42 侧交付的恰是它该交付的那一半(文本组件接口)**;字幕的**具体呈现规格**
(位置 / 时长 / 换行)归 49 的 P2,今日**不在 P0 面内**。

⚠️ **但 AC-44-15 的 `[L]` 半边今日仍不可执行** —— 听测者要「看到字幕」需要 42 有
**实际渲染的字幕控件**,而那是 49 的 P2 交付物。**这不是本批能清的**,如实登记。

---

## 四、L-3 / L-4 —— 开关从编译期常量改为装配期形参

### 4.1 病因

原形态是 **`const bool AuditionDiseaseForListening = false`**(编译期常量):

- 听测须**改代码重编**;
- 「改完忘改回」会把听测用的长曲线**漏进出货包** —— 而且**静默**(没有任何门会红)。

### 4.2 修法(与 L-2 同构)

| 落点 | 内容 |
|---|---|
| `CompositionRoot.Assemble`(两重载 + `AssembleCore`) | 新增 `int? patientRegistryIndexOverride = null` |
| `CompositionRoot` 的病人驱动装配 | `int registryIndex = patientRegistryIndexOverride ?? 0` + **越界 fail-loud** |
| `BootRoot.ResolvePatientRegistryIndexOverride()` | 读环境变量 `DYJC_AUDITION_REGISTRY_INDEX` |
| `BootRoot.Start` 的 `Assemble` 调用 | `patientRegistryIndexOverride: ResolvePatientRegistryIndexOverride()` |

**为什么是索引而不是布尔**:布尔只能表达「第 0 条 or 第 1 条」,加第三条听测曲线就要再改签名;
索引把「选哪条」交回数据侧(注册表顺序)。

**为什么越界 fail-loud 而不钳位**:钳到 0 会让「我设了 5 但听的是 0」成为静默故障
—— 与 L-2 的非法值判据同一理路。

### 4.3 夹具 —— **关键:必须让装配出来的驱动挑病种**

`audition_disease_selection_test.cs` 新增 3 条装配级判据(原 3 条只测驱动层):

| 测 | 钉什么 |
|---|---|
| `test_assemble_noOverride_spawnsWiringDisease` | 未给 ⇒ `DiseaseId = 1`(109 处零变化) |
| `test_assemble_override1_spawnsAuditionDisease` | 给 1 ⇒ `DiseaseId = 2`(**L-3 核心**) |
| `test_assemble_outOfRangeIndex_failsLoud` | 越界 5 ⇒ `InvalidOperationException` |

⚠️ **起草时我自己写错一次**:首版辅助方法里 `bag.PatientSpawner.SpawnNext(registry[overrideIndex ?? 0].DiseaseId, ...)`
—— 那样**测的是本方法自己**,形参有没有传到驱动完全测不到(**自指夹具**,承
`feedback_self_referential_fixture`)。已改为调 `bag.PatientAppearedDriver.OnTickEdge(0)`,
让**装配出来的驱动**挑病种。

### 4.4 L-3 证伪轮 —— **3/3 全红**

| 变异 | 实测 |
|---|---|
| M1 无视形参(恒取 0) | **RED 2/6**(`…outOfRangeIndex_failsLoud` + `…override1_spawnsAuditionDisease`) |
| M2 越界静默钳位(不 fail-loud) | **RED 1/6**(`…outOfRangeIndex_failsLoud`) |
| M3 默认改错(未给时取 1) | **RED 1/6**(`…noOverride_spawnsWiringDisease`) |

**基线 6/6 绿;还原后 SHA-256 = 变异前(`OK`)。**

> ⚠️ M1 红 2/6 而不是 1/6:无视形参后「越界 fail-loud」也不可能发生
> (恒取 0 永不越界)⇒ 两条失守。**这是正确行为,不是夹具太宽。**

### 4.5 L-4(刷人间隔)现状

`SpawnIntervalTicks` 已于 2026-10-29 由 400 改 **60**(20 s/人 → 3 s/人),EditMode 全量零红。
**L-4 无独立判据** —— 它是「可听窗口足够」的**副作用条件**,随 L-3 一并满足。
⚠️ 如实登记:**没有夹具钉 `SpawnIntervalTicks == 60`**;它是接线旋钮,不是裁定值,
数值本身归数值轮。本条不主张「已证」。

---

## 五、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 |
|---|---|---|---|---|
| EditMode | 2026-10-29 CST | 全量(无 filter) | `5178ceb8` + 工作树 | **`5929 / 5892 / 0 / 37`**(exit 0) |
| PlayMode | 2026-10-29 CST | 全量(无 filter) | `5178ceb8` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2,与基线逐位相同) |

**EditMode 等式**:`5923 + 3 + 3 = 5929`

- `5923` = 上一轮(`TD-audio-025` 收口)全量实测 @ 2026-10-29;
- `+3` = `diagnosis_query_level_override_test.cs` 新增 `[Test]`(复算 `grep -c '\[Test\]'` 实测 3);
- `+3` = `audition_disease_selection_test.cs` 新增装配级 `[Test]`(复算 `git diff | grep -c '^+\s*\[Test\]'` 实测 3)。

---

## 六、⚠️ 本轮我自己的两处操作错误(如实登记)

### 6.1 Monitor 判据命中命令行文本 ⇒ 误判「跑完了」

第一次全量 EditMode 用 `until grep -q "EXIT=" <task.output>` 作完成判据 ——
而**任务输出文件的第一行就是完整命令行**,其中含 `EXIT=` 字样 ⇒ Monitor **立刻**报完成。
**实际进程(PID 3674800)还在跑。**

### 6.2 未核实进程就重跑 ⇒ 撞锁

误判之后我直接启动了第二个全量跑 ⇒ 撞上第一个的 `Temp/UnityLockfile`
⇒ 第二个 `EXIT=1` / NO-XML。**第一个跑本身完全正常**,最终结果就是 §五 的 `5929/5892/0`。

> **教训**:① Monitor 的判据不能匹配命令行文本(输出文件包含整条命令);
> ② 「跑完了吗」的判据必须是**产物文件存在**(`[ -f <xml> ]`),不是输出文本匹配;
> ③ 重跑前必须 `pgrep` 核实(承 `feedback_parallel_unity_batch` 的「见活 batch 先问归属」)。

---

## 七、债册状态改动

（待回填 —— 听测结果未回,5 条债的最终状态见 §八）

---

## 八、⚠️ 5 条债的最终归属 —— **只清了前置,没清债**

| 债 | AC | 前置状态 | 能否关 |
|---|---|---|---|
| `TD-audio-100` | AC-44-10 | ✅ 10-a/10-b 代码已落(2026-10-18) | ⏳ **待听测**(10-c) |
| `TD-audio-101` | AC-44-02/08/01 | ✅ L-2 档位缝已修 + `TD-audio-030` 已结 | ⏳ **待听测** |
| `TD-audio-102` | AC-44-03/04 | ✅ `TD-audio-034` ① Intensity 轴已接(2026-10-29) | ⏳ **待听测**(B-2 交叉淡化仍无对象) |
| `TD-audio-103` | AC-44-17 | ✅ `n ≥ 2` 已改判 + 遮挡链已接 + 三态可听 | ⏳ **待听测** |
| `TD-audio-105` | AC-44-12/15 | ❌ **不可听测清** | ❌ **不能关**(见下) |

### `TD-audio-105` 为什么不能关

| 其 AC | 判据本体 | 2 人听测能清? |
|---|---|---|
| `AC-44-12` | **无障碍人工走查**(三条具名项:视觉替代 / 显式非目标 / 44 自己的义务) | ❌ 走查 ≠ 听测 |
| `AC-44-15` `[L]` 半 | 病人语声与口述**有字幕**(呈现归 42) | ❌ 字幕控件是 **49 的 P2 交付物**(`OQ-42-8` 已裁) |

⇒ **`TD-audio-105` 维持 open**,归「无障碍人工走查排期」+ 49 的 P2,与本批无关。

> ⚠️ **本批不主张任何一条债已结** —— 两份听测协议件今日仍是 **0 个 `[x]`**,
> 没有任何 A/B 结果可回填。本批交付的是**前置解除**,不是听测结论。
