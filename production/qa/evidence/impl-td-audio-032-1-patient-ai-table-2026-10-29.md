# 实现证据 —— `TD-audio-032` ①:13 调参常量入烘焙管线

- **日期**:2026-10-29
- **批次**:音频债批 C 收尾后 · `TD-audio-032` ①(②③④ 已于 S15-5 结,见 §一)
- **对象**:`assets/data/ai_patient.json`(新) · `unity/Assets/Editor.Tools.Bake/{PatientAiBinder,PatientAiCookedWriter,PatientAiBaker,DataBakeMenu}.cs` ·
  `unity/Assets/Gameplay.Presentation/PatientAI/PatientAiCookedCodec.cs`(新) ·
  `unity/Assets/Gameplay.{Boot,Presentation}/{CompositionRoot,DiagnosisCookedDisk,AddressablesDataProvider}.cs` ·
  `unity/Assets/DataCooked/ai_patient.cooked.bytes`(新产物) ·
  `unity/Assets/Tests/EditMode/Audio/patient_ai_table_test.cs`(新)

---

## 一、原判定与实测复核 —— **账面第 8 例滞后**

债行列四项,标 `partial`。逐项实测:

| # | 债行断言 | 实测(2026-10-29) | 判 |
|---|---|---|---|
| ① | 调参常量以代码常量进生产 | **成立** —— `PatientCueDriver` 的 `intervals` 是可空参数、生产不传 ⇒ 落 `CueIntervals.Default`;`CompositionRoot:1230` 直取 `BehaviorBands.Default` | ✅ **本轮修** |
| ② | `(int)tick` 截断 | **已结** —— 字典是 `long`,注释自带「S15-5 · TD-audio-032 ②」 | ❌ 账面滞后 |
| ③ | `OnTickEdge` ≈57 行 · `BuildAudioSeams` ≈47 行 | **已结** —— 实测 `OnTickEdge` **28 行**、`BuildAudioSeams` **31 行**,均在 40 界内 | ❌ 账面滞后 |
| ④ | `PruneAbsent` 只跟呼吸句柄 · 每 tick 分配 | **已结** —— `PruneAbsent` 扫**五表并集** + 在场集白名单;`PresentPatientIds` 成员变动时才重建快照 | ❌ 账面滞后 |

⇒ 本轮**只做 ①**。

---

## 二、修复落点

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `assets/data/ai_patient.json`(新) | 两节:`cue_intervals`(3 个 int)/ `behavior_bands`(6 个定点串)。值 = 现有代码常量的照抄 |
| R-2 | `PatientAiBinder.cs`(新) | 阶段 2 绑定:白名单 + `FixParse` + **复用运行期 `Validate`**(不重实现约束表) |
| R-3 | `PatientAiCookedWriter.cs`(新) | deterministic 编码(20 B 头 + 36 B 载荷) |
| R-4 | `PatientAiCookedCodec.cs`(新) | 运行期读方(手动字节游标,E-13 硬失败) |
| R-5 | `PatientAiBaker.cs`(新) | 仓根装载器(`ConfigVersion` = 内容哈希) |
| R-6 | `DataBakeMenu.cs` | 新菜单「烘焙 ai_patient」+ 产物名引用**单一出处常量** |
| R-7 | `CompositionRoot.cs` | 改读烘焙产物(`dataProvider` / `DiagnosisCookedDisk` 双路径) |
| R-8 | `DiagnosisCookedDisk.cs` / `AddressablesDataProvider.cs` | 直读兜底 + Addressables 装载分支 |

### 关键设计判断(两处,都不是照抄)

**① `behavior_bands` 走定点串,不走 float —— 这不是我的选择,是 GDD 的要求。**
`design/gdd/patient-ai.md` 规则五逐字:「`ai_patient.json` 里**禁 `float` 字段**,
非空间量在数据里写 `int` 或定点串」。所以六个阈值在 JSON 里写 `"1/5"` / `"9/20"` 而非 `0.2` / `0.45`。

**② 定点化会量化,这是定点域的本质,不是缺陷 —— 但断言必须写对。**
离线复算(`python3` 独立实现,不依赖被测代码):

| 字段 | 源 | `× 65536` | `ROUND_HALF_AWAY_FROM_ZERO` 后 | 回读 |
|---|---|---|---|---|
| `mild_min` | `1/5` | 13107.2 | 13107 | **0.199996948** |
| `seek_min` | `9/20` | 29491.2 | 29491 | **0.449996948** |
| `collapse_min` | `3/4` | 49152.0 | 49152 | **0.75**(精确) |
| `recover_max` | `1/10` | 6553.6 | **6554**(进位) | **0.100006104** |
| `hyst` | `1/10` | 6553.6 | **6554**(进位) | **0.100006104** |
| `death_band_min` | `9/10` | 58982.4 | 58982 | **0.899993896** |

⚠️ **首版夹具按「截断」写断言** ⇒ `recover_max` / `hyst` 两条实跑红(实得 `0.100006104`)。
根因 = 我没先查 `FixParse` 的舍入模式。ADR-006 §三 裁定的是 **`ROUND_HALF_AWAY_FROM_ZERO`**,
`.6` 会进位。**离线复算先于写断言**这条纪律,我这次是「写完断言才复算」,顺序反了。

---

## 三、证伪轮(S12-4 形态:改 → 跑 → 原子还原 → SHA-256)

驱动脚本:`/home/gu/.claude/jobs/181888b1/tmp/falsify032.py` + `falsify032b.py`(每条变异独立
Unity 启动;前置逐条核对盘上无变异串;`total==0 ⇒ FILTER-MISS` 中止;还原后 SHA-256 校验)。

| 变异 | 打靶判据 | 期望红 | 实测 |
|---|---|---|---|
| M1 | 白名单不拒未知键 | `test_unknownRootKey_reportsRed` | **RED-命中** |
| M2 | 缺 `cue_intervals` 不报 | `test_missingCueIntervals_reportsRed` | **RED-命中** |
| M3 | Fix 字段不要求字符串 | `test_jsonNumberInFixField_reportsRed` | **首轮假存活 → 补靶后红** |
| M4 | 整数字段不校验类型 | `test_stringInIntegerField_reportsRed` | **RED-命中** |
| M5 | cue 硬约束不挂 errors | `test_cueIntervals_severeNotLessThanMild_reportsRed` | **RED-命中** |
| M6 | 行为分档硬约束不挂 errors | `…monotonicChainBroken` + `…hystNotLessThanSeekMin` | **RED-命中 2 条** |
| M7 | schema 版本不校验 | `test_cookedSchemaVersionMismatch_reportsRed` | **RED-命中** |
| M8 | 载荷长度不校验 | `test_cookedSelfConsistentButWrongLength_reportsRed` | **首轮假存活 → 补靶后红** |
| M9 | `PayloadBytes` 常量改错(36→60) | `…cookedLength` + `…cookedRoundTrip` | **RED-命中 2 条** |
| M10 | writer 少写一个 float(镜像破坏) | `…cookedLength` + `…cookedRoundTrip` | **RED-命中 2 条** |
| M11 | 装配层改回 `BehaviorBands.Default` | `test_compositionRoot_noLegacyDefaultConstants` | **RED-命中** |
| M12 | 装配层不传 `intervals` | 同上 | **RED-命中** |

**12/12 全红;还原后三文件 SHA-256 逐字节 = 变异前**(脚本内断言 + 轮后 `sha256sum -c`,均 OK)。

### ⚠️ 首轮 2/2 假存活 —— 两条都是「靶场里没有靶」

**M3**:我的 Fix 字段夹具用的是**字符串** `"0.20"`(走 `FixParse` 拒浮点那条路),
而变异打的是「**类型检查**」(JSON 数字在进 `FixParse` 之前就被拒)。
删掉类型检查后,字符串路径仍走 `FixParse` 并报错 ⇒ 测试仍绿。
**补靶**:`test_jsonNumberInFixField_reportsRed`(写 `"mild_min": 0.2` 这个 JSON **数字**)。

**M8**:读方有**两条长度判据**,只要头声明的是 36 就**完全等价** ——
① 头校验 `payloadLength != bytes.Length - HeaderSize`;② 载荷判据 `bytes.Length - pos != PayloadBytes`。
我的「截断 / 加长」夹具砍掉字节后**头声明的长度不变** ⇒ 判据 ① 先开火,判据 ② 删掉也照样抛 ⇒ 假绿。
**补靶**:`test_cookedSelfConsistentButWrongLength_reportsRed`(头声明 40、实得 40 ⇒ ① 通过、② 开火)。

> 同族根因,这是本轮**第二次**踩(035 ③ 轮也踩过)。判据:**变异打的分支,夹具里必须有可达输入** ——
> 不是「有一条相关测试」,是「那条测试走的是被变异的那个分支」。

---

## 四、可复算命令

```bash
# 1) 真种子表两节齐全
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import json;d=json.load(open('assets/data/ai_patient.json'))
print(sorted(d.keys())); print('cue:', d['cue_intervals']); print('bands:', d['behavior_banks'] if 'behavior_banks' in d else d['behavior_bands'])
"
# 实测:['_note', 'behavior_bands', 'cue_intervals', 'schema_version']

# 2) 烘焙可复跑(产物应逐字节稳定)
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$PWD" -executeMethod DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeAiPatient \
  -quit -logFile /tmp/b.log
# 实测:烘焙完成:ai_patient.cooked.bytes = 56 B(20 头 + 36 载荷),ConfigVersion = 0x56E89C6B

# 3) 装配层不再取代码常量(剥注释后)
grep -v '^\s*//' unity/Assets/Gameplay.Boot/CompositionRoot.cs | grep -c 'BehaviorBands.Default\|CueIntervals.Default'
# 实测:0
```

---

## 五、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 |
|---|---|---|---|---|
| EditMode | 2026-10-29 17:0x CST | 全量(无 filter) | `54e94578` + 工作树 | **`5915 / 5878 / 0 / 0 / 37`**(exit 0) |
| PlayMode | 2026-10-29 17:1x CST | 全量(无 filter) | `54e94578` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2,与基线逐位相同) |

**EditMode 等式(不是照抄旧数)**:`5895 + 20 = 5915`

- `5895` = 上一轮(035 ③ 收口)全量实测 @ 2026-10-29 17:31 CST;
- `+19` = 新增 `patient_ai_table_test` 的 `[Test]` 条数(复算:`grep -c "\[Test\]"` 实测 **19**);
- `+1` = 漂移门参数化展开 `drift[ai_patient.cooked.bytes]`(`TestCaseSource` 按产物逐份展开)。
  **复算(差集,不是推算)**:
  ```python
  old = ET.parse('em032.xml').getroot(); new = ET.parse('em032d.xml').getroot()
  names = lambda r: {t.get('fullname') for t in r.iter('test-case')}
  print(sorted(names(new) - names(old)))
  # 实测: ['…cooked_drift_all_artifacts_test.drift[ai_patient.cooked.bytes]'] —— 只有这一条
  ```

**PlayMode 的 2 条 Inconclusive 是既有的具名占位**(方法名自带 `_notRun`),exit 2 与基线同形,
**不写成绿、也不写成红**。

### 中途三跑如实登记(不是「一次过」)

| 跑次 | 结果 | 根因 |
|---|---|---|
| 1 | `5914 / 5851 / **26**` | 新产物未登记(A–E 五处) |
| 2 | **NO-XML** | `DataCookedDisk.LoadAiPatient` 不存在(我加到了 `DiagnosisCookedDisk`) |
| 3 | `5915 / 5877 / **1**` | catalog 未重建(`组 36 ≠ catalog 35`) |
| 4 | `5915 / 5878 / **0**` | — |

---

## 六、本轮踩到的坑(如实登记)

### 1. `FixParse` 拒浮点字面量 —— 首版 JSON 直接烘焙失败

首版写 `"mild_min": "0.20"` ⇒ 烘焙 6 条红:`FixParse:拒浮点字面量 "0.20"`。
改分数(`"1/5"`)后通过。**这是 ADR-014 §三 的设计**,不是意外。

### 2. 舍入模式没先查 —— 断言按截断写,实跑 2 红

见 §二 ②。**顺序错了**:应先离线复算再写断言(承 `feedback_verify_assertions_offline_first`)。

### 3. 新产物未登记 ⇒ 全量首跑 **26 条红**(五处连锁,不是一处)

首跑全量 `5914 / 5851 / 26 failed`。26 条红分四族,**全是「新产物没进管线」的连锁**,
但**不是同一个登记点** —— 逐处独立:

| # | 缺什么 | 红的判据 |
|---|---|---|
| A | `DataCoreAddresses` 数组缺 `AiPatientAddress` | `test_preloader_list_sources_matchPreloadedProducts` · `test_wiring_dataCoreGroup_containsEveryProviderAddress` |
| B | `AddressablesDataProvider.Preload()` 缺 `EnsureAiPatient()` | `test_wiring_preload_invokesEveryEnsureMethod` |
| C | `cooked_drift_all_artifacts_test` 的 `BakedArtifacts` 缺条目 + `pinnedDeclared` 35→36 | `test_sweep_noGhostArtifact_onDiskButUnregistered` · `test_sweep_declaredArtifactNames_allTriaged` |
| D | `data-core.asset` 组定义缺条目(35 ≠ 36) | `test_ac5_allChunksBaked_andRegisteredInDataCoreGroup` |
| E | **5 个测试文件**的 `MultiTableProvider` 替身没准备 `PatientAiDataSet` | 20 条 `InvalidOperationException: 无 PatientAiDataSet` |
| F | **catalog 未重建**(组条目加了但 `catalog.bin` 是旧的) | `test_catalog_containsEveryDataCoreAddress` —— 「组声明 36, catalog 只有 35」 |

⚠️ **A / B / C / D 是同一纪律的四/五处**(`Ensure*` / `Preload()` / 地址表 / 组定义 / 漂移登记表
**五处集合相等**)。该纪律在本仓已被记为**第五次**同型事故(前四:BuildModules / DiseaseRegistry /
WorldBuildSlots / WorldGeometry),逐处注释都写着「前几次的教训逐字适用」——
**我还是漏了**,而且是四处一起漏。这说明「靠人记」的判据确实不成立,门是必要的(门也确实抓到了)。

⚠️ **E 是新形态**:装配层新增一次 `dataProvider.Load<PatientAiDataSet>()` 调用后,
**每一个**注入假 provider 的测试都得跟着准备该表 —— 否则装配当场抛。
这不是「测试写得差」,而是**装配面对测试替身的耦合**:`CompositionRoot.Assemble` 每多读一张表,
所有替身都要同步。本轮 5 个文件、20 条测试同时红。

### 4. 源文本判据扫到注释 —— 假红 1 条

`test_compositionRoot_noLegacyDefaultConstants` 首版直接 `Does.Not.Contain("BehaviorBands.Default")`,
而 `CompositionRoot` 的**历史注释**里提到过它 ⇒ 假红。改为 `StripComments` 后只扫可执行文本。
⚠️ 这与我 035 ③ 轮踩的坑**同族**(那次是「夹具没跟上生产解析」,这次是「判据没区分注释与代码」)——
共同点:**源文本判据必须明确它扫的是哪一层**。

---

## 七、本轮**不**证(边界)

| 项 | 状态 | 归属 |
|---|---|---|
| 六个阈值的**真值**是否合适 | **NOT-RUN / 归用户数值轮** | 本轮值 = 现有常量照抄,只搬存放位置 |
| 量化差(≤ 9e-6)对 13 行为的影响 | **NOT-RUN** | 属数值轮;机制面只证「定点化路径正确」 |
| Addressables 生产路径的实际装载 | **NOT-RUN** | 组 gitignored,须真机验证;本轮只证分派分支存在 |
| 13 的**实际行为**是否因此变化 | **NOT-RUN** | 属 PlayMode / 实机 |
