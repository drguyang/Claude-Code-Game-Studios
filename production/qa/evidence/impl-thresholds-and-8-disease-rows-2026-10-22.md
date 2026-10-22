# 四阈值裁定 + R3.3 八病种数值 —— 落地与证伪证据(2026-10-22)

**批次**:S18 前置续(承接 `impl-disease-registry-bake-pipeline-2026-10-22.md`)
**commit(改动前)**:`4e005bd`
**裁定来源**:用户 2026-10-22 四项选择(经 `AskUserQuestion` 全部取推荐档)
**判据面**:GDD `design/gdd/disease-simulation.md` §二之一 / §二之二 / §五 · ADR-014 §三③

---

## 一、裁定项与落点

| # | 裁定 | 值 | 落点(三处同批) |
|---|---|---|---|
| ① | `CRITICAL_THRESHOLD` | **19661 raw**(≈ 0.30) | `StateMachine.cs:79` + `entities.yaml` + GDD §五 |
| ① | `COMA_THRESHOLD` | **29491 raw**(≈ 0.45) | 同上 |
| ① | `DEATH_THRESHOLD` | **38011 raw**(≈ 0.58) | 同上 |
| ① | `RECOVER_THRESHOLD` | **7864 raw**(≈ 0.12) | `entities.yaml` + GDD §五(代码侧无常量,见 §四) |
| ② | `TREATMENT_WINDOW` | **3600 tick**(3 分钟) | `entities.yaml` + GDD §五 |
| ② | `CARE_GAP` | **600 tick**(30 秒) | `entities.yaml` + GDD §五 |
| ③④ | 八病种曲线 | 见 GDD §二之二表 | `assets/data/disease_registry.json` id 7–14 |

> 四项选择是**机械展开的前提**,不是四条独立裁定 —— 八行的 `A_peak` 全部由
> 「档 C 的 DEATH = 0.58」× 「按尾部形态分档」推出来,没有一行是我另起的数。

---

## 二、约束核对(全部实测,非声称)

| 约束 | 出处 | 实测 |
|---|---|---|
| `0 < RECOVER < CRITICAL < COMA < DEATH < 1` | 校验 9 / 17 | 7864 < 19661 < 29491 < 38011 < 65536 ✅ |
| `TREATMENT_WINDOW ≥ CARE_GAP` | 校验 17 | 3600 ≥ 600 ✅ |
| `TRAUMA_CAP ≥ COMA_THRESHOLD` | A12(combat-and-weapon-lines) | 0.8 ≥ 0.45 ✅(**原注记按旧值 0.0916 复核,已就地更新**) |
| 急性保持型须 `lethal` | 校验 4 / R1-22 | 痢疾 / 破伤风两行 `lethal: true` ✅ |
| ~~`A_peak/Scale ≥ DEATH`(4 行致死病种)~~ | ~~校验 4b / R1-23~~ | **❌ 本行判据错引,已订正为 NOT-RUN —— 见下方 §二之一** |
| 13 行全过真校验器 | R1.2–R1-32 | `RegistrySchemaValidator.Validate` 零违例 ✅ |

### 二之一、订正:`4b` 那一行是**借绿**(2026-10-22 自查)

**原文(不删,留作闭环记录)**:

> `| A_peak/Scale ≥ DEATH(4 行致死病种) | 校验 4b / R1-23 | 0.667 / 0.667 / 1.000 / 1.000 均 ≥ 0.58 ✅ |`

**错在哪 —— 两层,叠加**:

| 层 | 问题 |
|---|---|
| **判据错引** | `R1_CHECK_23` 实现的是「`Scale > 0 ∧ Scale ≥ A_peak`」(`RegistrySchema.cs:510-512`),**完全不检查 DEATH 下界**。它是「Scale 与 A_peak 的相对关系」,不是「A_peak 与死线的相对关系」。我把一个**已实现的、不相干的门**当成了另一个门的证据。 |
| **数字来源** | 那四个比值(0.667 / 0.667 / 1.000 / 1.000)**不是任何门的输出**,是我手算的。手算可以是对的 —— 但它**不构成门的证据**,却被写进了「实测」列。 |

**真 4b 的状态**:`RegistrySchema.cs:499-502` 自陈「GDD 校验 4b(`A_peak/SCALE ≥ DEATH_THRESHOLD`)
因 DEATH_THRESHOLD 值待定**未实现**」。⇒ 全库**零校验**这条下界。

**订正后的判读**:

| 项 | 订正前 | 订正后 |
|---|---|---|
| 4b 校验是否实现 | ✅(误记) | **❌ 未实现** |
| 那四行数据是否满足 4b | ✅ | **手算满足,但无门背书 ⇒ `NOT-RUN`** |
| 4b 是否可实现 | 不适用 | **✅ 可实现**(DEATH = 38011 已裁)⇒ 现在是**欠账**,不是**阻塞** |

> ⚠️ **该缺口的后果**:没有 4b,注册表可以收进一条「`lethal: true` 但 `A_peak/Scale < 0.58`」
> 的行 —— 它在 F4 里**永远打不到死线**,即「声明致死却永不致死」。
> 归 9 的实现轮(须在 F4 求值体落地**之前**或**同批**)。

---

## 三、`id` 分配的权威(不是我自己编号)

八行续 `7–14`,依据 **ADR-014 §三③ append-only**:「新增条目取**新号**;
既有条目的号**永不重用、永不改义**」。伤情占 2–6 ⇒ 病种从 7 起。
`id = 1` 保留给 `CompositionRoot.CreateFallbackRegistry` 的占位条目(该处注释已声明)。

### 三之一、位序一位之差 —— 已裁定并落盘(2026-10-22 同日)

**原文(不删,留作闭环记录)**:

> **顺带查证的一条既有不一致**(本轮不修,登记):
>
> | 件 | 口径 |
> |---|---|
> | `PatternDetector.DiseaseOrdinalsOf`(`Sim/PatternDetector.cs:469-481`) | 返回 `bit`(**0-based**) |
> | `TelemetryReporter.cs:123-124` / `:591-592` 注释 | 称「bit 位置 i 对应 `DiseaseId = i + 1`」 |
> | `pure_function_recomputable_test.cs:367` 注释 | 亦称映射为 `i + 1` |
>
> ⇒ 两个消费面对同一 `DiseaseIdSet` 的编号空间**说法不一致**(一位之差)。
> 本轮八行落在 `id 7–14` ⇒ `bit 6–13`,两种读法**都**在 64 位内不溢出,
> 故不影响本轮数据的可用性。**但这是既有缺陷,须单独裁定**。

**裁定结果(同日,用户裁「全做」)**:

| 项 | 裁定 |
|---|---|
| `DiseaseIdSet.Bits` 的位序 | **`bit index = DiseaseId − 1`**(0-based 存储形态 / 1-based 主键) |
| 「ordinal」一词 | **停用** —— 它在本仓被同时用来指这两者,歧义就是缺陷本体 |
| 改名 | `PatternDetector.DiseaseOrdinalsOf` → `DiseaseBitIndicesOf`;<br>`PatternEmission.DiseaseOrdinal` → `DiseaseBitIndex`;<br>`ScriptedChain.DiseaseOrdinal` → `DiseaseBitIndex`(全库无具名实参调用,改名零风险) |
| 值域守卫 | **新增 `R1_CHECK_33`**(`DiseaseId ∈ [1, 64]`)—— 此前**只有唯一性、零值域校验**,而 `DiseaseIdSet` 注释长期声称「超界 = 数据层构建失败」,**那个失败不存在** |
| pin 测试 | 新增 `disease_id_bit_pin_test.cs`(P1 往返 / P2 真表 id 起点 ≠ 1 / P3 R1-33 双向) |

**为什么原表里「两种读法都行」的结论是错的(不只是不精确)**:真表 `id` 从 **2** 起
(`id = 1` 归 fallback),⇒ `bit = id − 1` 与 `bit = 行序` 在真表上**差 1**。
原表只核了「不溢出」,没核「对不对齐」。而在 `id = 1,2,3…` 的夹具里两者**数值相同**
⇒ 那类夹具**分辨不出**两种读法 —— 这正是 `f2_wiring_test`(夹具 `Registry(1,2,3)`)
一直绿着的原因。见该文件 §三之一的 P2 判据。

---

## 四、代码侧未落的部分(禁借绿)

| 常量 | entities.yaml | GDD §五 | C# 代码 |
|---|---|---|---|
| `CRITICAL` / `COMA` / `DEATH` | ✅ 裁定值 | ✅ 裁定值 | ✅ `StateMachine.cs` 已改 |
| `RECOVER_THRESHOLD` | ✅ 裁定值 | ✅ 裁定值 | ❌ **全库零命中**(痊愈判定尚未实现) |
| `TREATMENT_WINDOW` | ✅ 裁定值 | ✅ 裁定值 | ❌ **仍零命中** —— `EntityDeathGates` 只提供实体门、不实现 F4 求值 |
| `CARE_GAP` | ✅ 裁定值 | ✅ 裁定值 | ❌ **仍零命中** |

⇒ **值已裁,但 F4 死亡支的窗口条件仍不可求值。**
裁定解决的是「数值从哪来」,不解决「谁读它」。后者归 F4 实现轮。

### 四之一、「不可求值」的完整清单(2026-10-22 复核补齐)

上表只列了「常量有没有 C# 读取点」。复核后确认**缺的不止读取点**,而是三件:

| # | 缺什么 | 实测证据 |
|---|---|---|
| ① | **F4 求值体** | `EntityDeathGates` 只出三个门;`StateMachine.ComputeTransition` 只做纯阈值分级,且 `lastInterventionTick` / `currentTick` / `diseaseKey` **三个形参函数体一个都不读** |
| ② | **`last_intervention` 聚合器** | `grep -rn "last_intervention\|LastIntervention" unity/Assets/ --include=*.cs \| grep -v Tests` ⇒ **零命中**。GDD `:1126-1133` 要它从三通道取 max,聚合器不存在 ⇒ F4 连入参都备不出来 |
| ③ | **注册表侧 4b** | `A_peak/SCALE ≥ DEATH_THRESHOLD` 的写入期下界**零校验**(见 §二之一) |

**⇒ 归属裁定**:① 与 ② **必须同批**实现 —— 否则 F4 只能拿外部灌进来的数,
无法从三源重算,违 ADR-016 §一 三源不变量。③ 须在 ① 之前或同批。

**⇒ 可达性(诚实分级)**:`ComputeTransition` 当前**生产调用点 = 0**
(`InjuryStateChangedWriter` 无生产调用方)⇒ 上述缺口**今天不可达**。
但 9 的 `Step` 一旦接线,纯阈值判定会让任何实体 `position ≥ 0.58` 即判死,
**绕过两门与窗口** ⇒ 敌人/兽被疾病杀死(违 ADR-016 §二)、玩家被白名单伤情
直接死而跳过昏迷。⇒ 须在 9 的 Step 接线**之前**补。

---

## 五、八行的已知缺口(建了行 ≠ 建全行)

| 缺口 | 影响 | 归属 |
|---|---|---|
| `DIS_MALARIA` 的 `relapse.*` 未建 | 三字段(`a_rel`/`interval`/`tau_rel_fall`)本轮**未裁**。缺块 = 该病按「无复发」处理 ⇒ 与 GDD `:429`「因有复发块,永不入痊愈」不一致 | 疟疾复发数值轮 |
| `DIS_RHEUMATIC_FEVER` 的 `compounds` 未建 | `DiseaseRegistryEntry` **无 `compounds` 字段**(GDD R1 表列了,C# 侧未实现)⇒ 旗舰共病线**当前不可求值** | 9 的 compounds 实现轮 |
| `signs[]` 八行全空 | 词表归 8,当前未建 | 8 的体征词表轮 |
| `handle` 八行未填 | 派生字段,派生逻辑在仓库里**不存在** | 见烘焙管线证据件 §六 |

---

## 六、证伪轮

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。**未用** `git checkout` / `git stash`。

备份 sha256:
```
b9800931dbc422c44c497a032ad66c52f492a535256e4c8c306d44851c4da2ef  StateMachine.bak
75e08633ffeaf1fce7dd680f814527e10e22cc0917490d12e790a7c94e06c996  json.bak
```

| 变异 | 内容 | 转红的测试 | 实测 |
|---|---|---|---|
| N1 | `COMA_THRESHOLD` 29491 → 40000(> DEATH,造倒置) | `test_thresholds_strictlyMonotonic` | ✅ RED |
| N1 | 同上 | `test_comaTakesPrecedenceOverCritical`(判成 Deceased 而非 Coma) | ✅ RED |
| N2 | `INJ_LACERATION.a_peak` 60 → 150(> Scale 100) | `test_rowsPassTheRealValidator` | ✅ RED |
| N2 | 同上 | `test_realSource_bakesWithoutError` / `test_sameSource_producesBitIdenticalBytes` / `test_dataFile_containsAllEightR33Diseases` / `test_r1_32_doesNotApplyToDiseaseRows` 等 7 条 | ✅ RED(真校验器 R1-23) |

**实测**:`total=46 passed=36 failed=10` —— 每条变异都打掉多条独立测试,
且**报错文本精确指向被变异的约束**(N2 报的是 `R1-23: scale 须 > 0 且 ≥ a_peak`)。

**还原核对**:两文件 sha256 与备份**逐字节相同**。

### 证伪轮的一处自我纠正

第一轮证伪时,我给 `disease_registry_data_test.cs` 新加的
`test_dataFile_containsAllEightR33Diseases` **首跑即红**,报「`DIS_MALARIA` 缺失」。
根因不是数据没建,是**该测试用了本文件的手写最小读取器 `LoadRows`,
其第 83 行硬编码 `continue` 掉所有非 injury 行** —— 读不到 ≠ 不存在。
改为走**真 binder**(`DiseaseRegistryBinder.Bind`)后绿。
⇒ 这条若没跑,就是一个**假红**;跑证伪轮顺带把它抓出来了。

---

## 七、回归(带三元组)

### 本轮终值

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22T09:07 UTC` |
| 批次 | 全量 EditMode(证伪还原后终跑) |
| commit | `4e005bd`(工作树含本轮未提交改动) |

```
total=4502  passed=4466  failed=0  inconclusive=0  skipped=36   EXIT=0
```

> ⚠️ **2026-10-22 补注**:上表是**当时那一刻**的真值,**未被改写**。
> 同日后续一轮(位序裁定 + `R1_CHECK_33`)把基线推到 `4505/4469/0/36` ——
> 见 **§九.5**。**引用回归数时请用 §九.5**,不要把本行当成本仓当前基线。

### 同轮中间值(如实记,按序)

| 时点 | 值 | 说明 |
|---|---|---|
| `08:35` | `4501/4465/0/36` | 烘焙管线 + 阈值未改时 |
| `~09:00` | `4501/4465/0/36` | **三阈值改为档 C 后首跑 ⇒ 仍零回归** |
| 中间 | `4502/4465/1/36` | 新加病种测试首跑红(见 §六 自我纠正) |
| `09:07` | `4502/4466/0/36` | 修正后终值 |

**新增数核对**:`4502 − 4501 = 1`,恰等于本轮净增的 1 条测试
(新增 `test_dataFile_containsAllEightR33Diseases`;
另一条 `test_dataFile_hasExactlyTheFiveWhitelistRows` 是**改名 + 收窄**,不增计数)。

### 「阈值改 4.2 倍却零回归」的归因(不是漏测)

`DEATH_THRESHOLD` 从 9000 → 38011 是 4.2 倍量纲跳动,而回归全绿。
核查了三种可能的假绿成因:

| 可能成因 | 核查方法 | 结论 |
|---|---|---|
| 测试写死旧值 | `grep -rn "3000\|6000\|9000" Assets/Tests --include=*.cs` | 命中项全属**其他系统**(ClinicMachine 的 modifier 值),与阈值无关 ⇒ 无写死值 |
| 测试读常量符号,改值不影响 | 查 `state_machine_death_test.cs` | 全部用 `StateMachine.X_THRESHOLD` 符号,**且断言的是相对关系**(单调、判定顺序锁)⇒ 阈值整体平移天然不破坏 |
| 该值无人读(死常量) | 查 `StateMachine.cs:99/105/111` | `positionAgg.Raw >= *_THRESHOLD` **三处真实读取** ⇒ 不是死常量 |

⇒ 零回归是**测试写法正确的结果**:它锁的是不变量(单调性 + 优先级序),不是绝对值。
证伪轮 N1 也证明了这一点 —— 破坏单调性会红,整体平移不会。

---

## 八、复算命令

```bash
cd unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/final4.xml" -logFile "$PWD/Logs/final4.log"
# 预期:total=4502 passed=4466 failed=0 skipped=36
```

约束实跑复算(读真源,非声称):

```bash
python3 - <<'PY'
import sys; sys.path.insert(0,'.')
ONE=65536
print("单调:",7864<19661<29491<38011<ONE,"| TW>=CG:",3600>=600,"| A12:",0.8>=29491/ONE)
PY
```

---

## 九、位序裁定落盘 + `R1_CHECK_33`(2026-10-22 同日·补记)

> **本节不是原报告的一部分** —— 它是同日后续一轮的落盘记录,追加在末尾,
> **不改写上面任何一节的原文与数字**。§七 的 `4502/4466/0/36` 是**它那一刻**的真值,
> 本节给出的是**本轮之后**的新真值,两者并列存在,不互相覆盖。

### 九.1 裁定内容

| 项 | 裁定 |
|---|---|
| 位序契约 | `bit index = DiseaseId − 1`(0-based 存储 ↔ 1-based 注册表主键) |
| 「ordinal」一词 | **在病种位掩码语境下停用**(其余表的 `ordinal` 不受影响) |
| 值域守卫 | `RegistrySchemaValidator.R1_CHECK_33`:`DiseaseId ∈ [1, 64]` |
| 权威落点 | `Sim.Contracts/Payloads/PayloadCommon.cs` 的 `DiseaseIdSet` 注释块 |

**为什么这不是「选个约定」而是纠错**:真表 `assets/data/disease_registry.json` 的
`id` 从 **2** 起(`id = 1` 归 `CompositionRoot.CreateFallbackRegistry`)⇒
「`bit = 行序`」与「`bit = DiseaseId − 1`」在真表上**差 1**。只有后者能让
`TelemetryReporter` 的 `bit + 1` 与注册表 `DiseaseId` 集合对齐。

### 九.2 十项落盘清单

| # | 文件 | 改动 |
|---|---|---|
| 1 | `Sim.Contracts/Payloads/PayloadCommon.cs` | `DiseaseIdSet` 位序契约块(含 `ulong` 移位 `& 0x3F` 静默回绕的后果) |
| 2 | `Sim/RegistrySchema.cs` | `R1_CHECK_33` + `DiseaseIdMin/Max` + 实现(紧贴 R1-01) |
| 3 | `Tests/EditMode/DiseaseSimulation/disease_id_bit_pin_test.cs` | **新建** · P1 往返 / P2 真表 id 起点 ≠ 1 / P3 R1-33 双向 |
| 4 | `Sim/EntityDeathGates.cs` | 头注:陈旧理据订正(原文保留) |
| 5 | `Sim/StateMachine.cs` | `ComputeTransition` 只做阈值分级,不承担 F4 四条件合取 |
| 6 | 本文件 §二 `4b` 行 | `✅` → `NOT-RUN` + 错引说明(见 §二之一) |
| 7 | `Sim/PatternDetector.cs` | `DiseaseOrdinalsOf` → `DiseaseBitIndicesOf`;XML doc 钉死返回 **bit index** |
| 8 | `Gameplay.Boot/TelemetryReporter.cs` | 两处「`DiseaseId = i + 1`」注释订正为「必须 `+1`」并说明与 `PatternDetector` **不同量** |
| 9 | `Tests/EditMode/Telemetry/f2_wiring_test.cs` | 夹具 id `(1,2,3)` → `(2,3,4)` —— 让假绿转真绿 |
| 10 | `design/registry/entities.yaml` + `design/gdd/disease-simulation.md` | 登记 `F4OutcomeEvaluator` / `last_intervention` 为未实现 |

### 九.3 夹具越界修复(新门的连带面,不是放宽门)

`R1_CHECK_33` 立起后,三个此前**不受值域约束**的夹具越界转红:

| 文件 | 原值 | 新值 | 理由 |
|---|---|---|---|
| `Tests/EditMode/DiseaseSimulation/synthetic_disease_fixture.cs:31` | `9001` | `RegistrySchemaValidator.DiseaseIdMax`(= 64) | 取上界是 pin:扩 `DiseaseIdMax` 会立刻越界转红(位掩码是 `ulong`,扩界须换类型) |
| `Tests/PlayMode/vertical_slice_test.cs:47` | `9001` | `64`(字面量) | 与 EditMode fixture 是**两份同源镜像**,镜像语义 = 各写一遍、由人同批改;改常量引用会掩盖不同步 |
| `Tests/EditMode/DiseaseSimulation/disease_registry_data_test.cs:332` | `99` | `64` | 该测试要证的是「R1-32 不适用于非 injury 行」,`99` 越界会让它因**别的规则**红而失去判别力 |

**这是门做对了,不是门误判** —— `9001` 若真被拿去算位掩码,`1UL << 9000` 按 `& 0x3F` 静默回绕到 `bit 8`(即 `DiseaseId = 9`)。修夹具、不放宽门。

### 九.4 证伪轮:`R1_CHECK_33` 真的会红

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。**未用** `git checkout` / `git stash`。

| 项 | 值 |
|---|---|
| 被变异文件 | `unity/Assets/Sim/RegistrySchema.cs` |
| 变异内容 | `if (entry.DiseaseId < DiseaseIdMin \|\| ...)` → `if (false && (...))` |
| 备份 / 还原 sha256 | `11ba481e235d029559bf865e7d8baff34513b1ebdf70a7e0db99ed1d9fe61127` |

| 轮次 | `total` | `passed` | `failed` | 转红的测试 |
|---|---|---|---|---|
| 变异后 | `4505` | `4468` | **`1`** | `DiseaseIdBitPinTest.test_p3_r1_33_rejectsOutOfRangeDiseaseId` |
| 还原后 | `4505` | `4469` | `0` | — |

**判据解读**:变异只打掉 **1** 条,是**正确**的 —— `P1`/`P2` 断言的是「真表 id 满足
`bit = id − 1`」这个**数据事实**,不依赖门的存废。若变异也让 P1/P2 转红,反而说明
P1/P2 误把「门在」当成了判据(那是把两件事混成一条)。

#### 第二轮证伪:`bit + 1` 这个转换点真的被钉住

位序契约的**另一半**是「读侧必须 `+1`」。只钉住写侧(`R1_CHECK_33`)不算证明。

| 项 | 值 |
|---|---|
| 被变异文件 | `unity/Assets/Gameplay.Boot/TelemetryReporter.cs` |
| 变异内容 | `diseases.Add(bit + 1)` → `diseases.Add(bit)`(**2 处**同时) |
| 备份 / 还原 sha256 | `2e376cd3637d3e3034c2a72c89e0dd353b7d66b0d1d2b0eb06848ab0cd8632e9` |

| 轮次 | `total` | `passed` | `failed` | 转红 |
|---|---|---|---|---|
| 变异后 | `4505` | `4462` | **`7`** | 见下表 |
| 还原后 | `4505` | `4469` | `0` | — |

| # | 转红的测试 |
|---|---|
| 1 | `Boot.TelemetryWiringTest.test_wired_realStream_f1aComputed` |
| 2 | `Unit.Telemetry.F2WiringTest.test_wired_f2_f1aIdentity` |
| 3 | `Unit.Telemetry.F2WiringTest.test_wired_judgedIsLexiconId_notOtherField` |
| 4 | `Unit.Telemetry.F2WiringTest.test_wired_judgmentRevised_decodedAsItsOwnPayload_noSkip` |
| 5 | `Unit.Telemetry.F2WiringTest.test_wired_realStream_f2MatrixAndBuckets` |
| 6 | `Unit.Telemetry.F2WiringTest.test_wired_truthBitPlusOne_isDiseaseId` |
| 7 | `Unit.Telemetry.PureFunctionRecomputableTest.test_c1_sameStreamTwiceBitwiseIdentical` |

**这一轮是「夹具 id 改 (1,2,3) → (2,3,4)」的价值证明**:若 `f2_wiring_test` 仍是
`(1,2,3)`,`bit = id − 1` 与 `bit = 行序` 数值相同 ⇒ 去掉 `+1` 后矩阵**照样对上**
(只是整体错位一格后又被错位的期望值抵消)⇒ 那 7 条**一条都不会红**。
现在 id 从 2 起,`+1` 是**唯一**能让 `bit` 落回 `DiseaseId` 的转换 ⇒ 去掉即 7 红。

⚠️ **注意 `test_wired_truthBitPlusOne_isDiseaseId` 用的是字面量 `1UL << 1` 而非
`BitsOf(id)`** —— 若它也用 helper,helper 自己就带 `−1`,变异时 helper 与断言同错,
该条**不会红**。字面量是刻意的「不跟着 helper 走」。

### 九.5 新回归基线(带三元组)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22 17:44 CST` |
| 批次 | 全量 EditMode(两轮证伪**均还原**后的终跑) |
| commit | `4e005bd`(工作树含本轮未提交改动) |
| 结果件 | `unity/Logs/test-editmode-final-r1-33.xml` |

```
total=4505  passed=4469  failed=0  inconclusive=0  skipped=36   EXIT=0
```

**与 §七 终值的差分核对**(`4505 − 4502 = 3`,恰等于本轮净增的 3 条测试):

| 新增测试 | 来源 |
|---|---|
| `test_p1_realRegistry_bitIsDiseaseIdMinusOne_roundTrips` | 新文件 `disease_id_bit_pin_test.cs` |
| `test_p2_realRegistry_idsAreNotContiguousFromOne` | 同上 |
| `test_p3_r1_33_rejectsOutOfRangeDiseaseId` | 同上 |

`f2_wiring_test.cs` 的改动是**改夹具 id**,不增删测试 ⇒ 计数不变。三夹具越界修复同理。

### 九.6 复算命令

```bash
cd unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/test-editmode-final-r1-33.xml" \
  -logFile "$PWD/Logs/test-editmode-final-r1-33.log"
# 预期:total=4505 passed=4469 failed=0 skipped=36
python3 -c "import xml.etree.ElementTree as ET; r=ET.parse('Logs/test-editmode-final-r1-33.xml').getroot(); print({k:r.get(k) for k in ('total','passed','failed','skipped')})"
```

两轮证伪的复算命令(各自备份 → 变异 → 跑 → `cp` 回 → sha256 比对,详见 §九.4):

```bash
# 第一轮:R1_CHECK_33
#   变异 Assets/Sim/RegistrySchema.cs 的 if 条件 → if (false && ...)
#   预期:total=4505 passed=4468 failed=1
#        转红 = DiseaseIdBitPinTest.test_p3_r1_33_rejectsOutOfRangeDiseaseId
#   结果件:Logs/test-editmode-falsify-r1-33.xml

# 第二轮:TelemetryReporter 的 +1
#   变异 Assets/Gameplay.Boot/TelemetryReporter.cs 的 diseases.Add(bit + 1) → Add(bit)(2 处)
#   预期:total=4505 passed=4462 failed=7
#   结果件:Logs/test-editmode-falsify-bitplus1.xml
```

### 九.7 本节未证的部分(禁借绿)

- **`R1_CHECK_33` 只有 EditMode 覆盖,没有 PlayMode 覆盖** —— 但 PlayMode 的
  `vertical_slice_test.cs` 走真 `DiseaseRegistryBinder` + 真 `Validate`,故本轮把它的
  fixture 改到界内是**必要**的;PlayMode 全量本轮**未跑**(未跑就写未跑)。
- **F4 求值体 / `last_intervention` 聚合器 / registry 侧 4b 校验** 三项**仍未实现**,
  见 §四之一。本节只钉住「位序契约」与「值域守卫」两件,**不表示 F4 可求值**。
