# 实现证据 · PRE-S18-B 机械半边(9 常量登记门)

**日期**:2026-10-22
**对象**:`unity/Assets/Editor.Tools.Gates/DiseaseConstantRegistryGate.cs`(新立)
**依据**:`production/sprint-status.yaml` PRE-S18-B ·
`design/gdd/disease-simulation.md` §五(Tuning Knobs 三阈值 + 干预窗口)·
`design/registry/entities.yaml`(constants 段)
**结论**:PRE-S18-B 的**登记面**从「无机械判据」→ **有门 + 有接线 + 有探针轮**。
⚠️ **数值面仍未裁定** —— 本件不主张任何数值已定。

---

## 一、问题是什么

PRE-S18-B 的实测原文是:`RECOVER_THRESHOLD` / `CARE_GAP` / `TREATMENT_WINDOW` 三者
`.cs` 零命中、`entities.yaml` 值「待定」、`TREATMENT_WINDOW` 无独立 `name:` 块。

补查发现**范围比登记的大**:同组的 `CRITICAL_THRESHOLD` / `COMA_THRESHOLD` /
`DEATH_THRESHOLD` **也没有独立 `name:` 块** —— 它们的值只活在
`Sim/StateMachine.cs:73-75` 的三个 `const` 里,而 `entities.yaml` 里它们只作为
`9.疾病状态机` 条目的 `variables:` 列表项**被引用**。

后果不是「少写四段 YAML」,而是 **「registry 是唯一真源」在这四个量上不成立**:
改 `entities.yaml` 改不动它们(那儿没有它们),只能去改 `const`,
而那条路径**没有任何东西拦着**。

---

## 二、判据设计(为什么只查存在性,不查数值)

| 判据 | 内容 | 缺则 |
|---|---|---|
| **D1** | 每个常量须有独立顶层块 `^  - name: <X>$` | 红 |
| **D2** | 块内六项必填齐:`status` / `source` / `referenced_by` / `value` / `unit` / `constraint` | 红 |

**刻意不判「值裁没裁」** —— GDD §五 三行 default 全为 `*待定*`,把「值没裁」判成红
会让本门**恒红**,那正是「判据对合法输入类误判」的形态。

⚠️ **本门确有一处 false green,如实登记**:`value:` 字段写个新占位数它照样绿。
判据是「字段在不在」,不是「值裁没裁」。**⇒ 不得把本门绿读成「数值已裁定」。**

---

## 三、门的盲区方向(如实登记)

本门是**文本级**扫描,不建 YAML AST(`Editor.Tools.Gates` 装配无 YAML 解析依赖,
引入它是架构级改动)。

- 缩进异常 / flow 样式 / 块挪进嵌套层 ⇒ **看不见**。
- 但看不见的后果是**转红**(报「缺块」),**不是静默通过** —— 失败方向**响亮**
  (false red),而 false red 会立刻被发现并改门。真正危险的是 false green,
  已在 §二 点名一处。

---

## 四、判据测试(证明门会红,不是空转)

`unity/Assets/Tests/EditMode/DiseaseSimulation/disease_constant_registry_gate_test.cs`
—— **11 条**:

| 组 | 测试 | 证明什么 |
|---|---|---|
| ① 真工程 | `test_realRegistry_isClean` | 真 `entities.yaml` 零违例 |
| ① 守卫 | `test_realRegistry_pathResolvesToAnExistingFile` | 路径推导没走错(空集冒充绿防线) |
| ② D1 转红 | `test_D1_missingBlock_isRed` | 整块缺失 ⇒ 红 |
| ② D1 核心 | `test_D1_nameOnlyReferencedInVariablesList_isRed` | **PRE-S18-B 的原样形态**:只被 `variables:` 引用 ⇒ 四个量各报一条 |
| ② D2 转红 | `test_D2_missingValueField_isRed` | 缺 `value:` ⇒ 红 |
| ② D2 穷举 | `test_D2_everyRequiredFieldIsChecked` | 六项逐个缺失,每项都单独报 |
| ② 失败面 | `test_missingFile_isRed` / `test_emptyPath_isRed` | 文件不存在 / 路径空 ⇒ 红(不静默跳过) |
| ③ 反向 | `test_D1_longerPrefixName_doesNotSatisfyShorterName` | `DEATH_THRESHOLD_X` 的块**不得**冒充 `DEATH_THRESHOLD`(锚串必须锚行尾,否则假绿) |
| ③ 反向 | `test_goodYaml_isClean` | 判据不过宽 |
| ③ 反向 | `test_extractBlock_doesNotBleedIntoNextBlock` | 块边界不越界(越界会让 D2 读到邻居的 `value:` ⇒ 假绿) |

**实测**:`26 / 26 passed / 0 failed`(含 `GateWiringManifestTest` 15 条,
`Logs/disconst-gate-test3.xml`)。

### 写测试时踩的两个坑(如实登记)

1. **首版夹具构造失败**:`test_D2_everyRequiredFieldIsChecked` 靠
   `Replace("    {field}: \"待定\"\n", "")` 剔除字段,但 `status` 的值是 `decided`
   不是 `"待定"` ⇒ 替换没生效,测试红。改成按字段名整行剔除。
2. **首版断言过宽**:改完后断言查**整份 YAML** 是否还有 `    status:`,
   而别的块仍带该字段 ⇒ 又红。改成只查**该块**(`ExtractBlock` 取回后再断言)。

两坑都在测试侧,不在门侧 —— 门的行为从头到尾是对的。

---

## 五、探针轮(CLAUDE.md §构建前门纪律 · 三件套)

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串出现在**构建日志**里 | ✅ `Logs/build-probe-disconst.log:1040` 与 `:1113` 两处 `[PROBE-DiseaseConstReg]` |
| 2 | 栈帧上是目标类的 `OnPreprocessBuild` | ✅ `AssemblyGates/BuildGate:OnPreprocessBuild (BuildReport) (at Assets/Editor.Tools.Gates/AssemblyGates.cs:1280)` |
| 3 | 构建**因它失败** | ✅ `Build Finished, Result: Failure.` + `BuildFailedException: [AssemblyGates] 构建前门失败 1 条(… / 9 常量登记门 / …)` + utp `"description":"Preprocess Player"` 即止 |

⚠️ 栈帧给的是 `Debug.LogError` 行(`:1280`),不是接线行本身。接线行
(`AssemblyGates.cs:154` 附近 `errs.AddRange(DiseaseConstantRegistryGate.RunAll())`)
被执行属**论证**(构建期唯一调用点),不是本探针的直接观测。

### 还原判据 = SHA-256 逐字节

```
变异前: 832bf5fa0be3f043df93354940a13af5657b840a6368d9f293428f82475773df
还原后: 832bf5fa0be3f043df93354940a13af5657b840a6368d9f293428f82475773df   ✅ 逐位相同
探针串残留: 0
```

**未用 `git checkout` / `git stash`**(承「证伪还原禁用 checkout」纪律)。

### 干净轮

`Logs/build-clean-disconst.log`:
```
Build Finished, Result: Success.
grep -cE "\[DiseaseConstReg|\[PROBE-DiseaseConstReg|\[fs-write|error CS"  →  0
```

⇒ **「门跑了且零违例」**,不是「门没被调用」(探针轮已排除后者)。

---

## 六、接线(零调用方的门形同虚设)

| 入口 | 落点 |
|---|---|
| `RunAll`(菜单 / 域重载 / 构建前三处共用) | `AssemblyGates.cs` → `DiseaseConstantRegistryGate.RunAll()` |
| 入口级接线清单 | `gate_wiring_manifest_test.cs` `EntryWiringManifest["RunAll"]` |
| 类级判决 | 同上 `ClassConsumerManifest` → `Kind = Chain`, `Consumer = "RunAll"` |

**为什么接 `RunAll` 而不是 `RunMenu` + `BuildGate`**:与 `WriterExistenceGate`(b7)
同格 —— 两者都是「registry ↔ 代码」一致性检查,`RunAll` 已被三处共用。

⚠️ **接线不是可选项**:`gate_wiring_manifest_test.test_classManifest_coversEveryDeclaredClass`
要求 `Editor.Tools.Gates/` 里**每个** `public static class` 都有判决。门类落盘而不登记
⇒ 该测试直接红。这是「新增门类而不接链、不登记,现有判据全绿」那个洞的补丁
(`TD-gate-02`)。

---

## 七、同批的 `entities.yaml` 登记(前置件)

四个常量已补独立 `name:` 块(2026-10-22,用户批准):
`CRITICAL_THRESHOLD` / `COMA_THRESHOLD` / `DEATH_THRESHOLD` / `TREATMENT_WINDOW`,
各含 `status` / `source` / `referenced_by` / `value` / `unit` / `constraint` 六项。

⚠️ **`value:` 一律写「待定」**,并把现网占位值标注为「**现网实现值 = N raw,
**非裁定值**」—— 登记的是**事实**(占位值住哪、是多少),不是**裁定**。

⚠️ **`TREATMENT_WINDOW` 连占位值都没有**(全树 `.cs` 零命中)⇒ F4 死亡支的窗口条件
**当前不可求值**。该事实已写进块内,不粉饰。

---

## 八、可复算命令

```bash
cd unity

# 真 registry 六块齐否(应为 6)
grep -cE "^  - name: (CRITICAL_THRESHOLD|COMA_THRESHOLD|DEATH_THRESHOLD|TREATMENT_WINDOW|RECOVER_THRESHOLD|CARE_GAP)$" \
  ../design/registry/entities.yaml

# 接线(应为 1)
grep -n "DiseaseConstantRegistryGate.RunAll" Assets/Editor.Tools.Gates/AssemblyGates.cs

# 判据测试(11 条 + manifest 15 条 = 26)
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Unit.Sim.DiseaseConstantRegistryGateTest;DaYiJingCheng.Tests.Unit.Sim.GateWiringManifestTest" \
  -testResults "$PWD/Logs/disconst-gate-test3.xml" -logFile "$PWD/Logs/disconst-gate-test3.log"
```

---

## 九、结账表

| ID | Landed(已落) | Proven(已证) | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|
| PRE-S18-B-1 | ✅ | ✅ | `Editor.Tools.Gates/DiseaseConstantRegistryGate.cs` | `test_realRegistry_isClean` + 探针轮三件套 |
| PRE-S18-B-2 | ✅ | ✅ | 同上(D1) | `test_D1_nameOnlyReferencedInVariablesList_isRed` —— **PRE-S18-B 原样形态**转红 |
| PRE-S18-B-3 | ✅ | ✅ | 同上(D2) | `test_D2_everyRequiredFieldIsChecked` —— 六项逐个缺,逐项报 |
| PRE-S18-B-4 | ✅ | ✅ | 同上(反向) | `test_D1_longerPrefixName_doesNotSatisfyShorterName` + `test_extractBlock_doesNotBleedIntoNextBlock` |
| PRE-S18-B-5 | ✅ | ✅ | `AssemblyGates.cs` + `gate_wiring_manifest_test.cs` | 探针轮 `Failure` + 干净轮 `Success`;manifest 封闭性判据强制 |
| PRE-S18-B-6 | ✅ | ✅ | `design/registry/entities.yaml` | `grep -cE "^  - name: (…)$"` → 6 |
| **数值裁定** | ✗ | ✗ | `entities.yaml` 四块 `value:` 全「待定」 | **归用户数值轮 —— 本件不主张** |

**结论**:PRE-S18-B 的**机械面**已兑现;`Landed ✅ / Proven ✅` 六条全齐。
**第 7 行刻意留 ✗** —— 数值没裁就是没裁,不借绿。

---

## 十、全量回归

**基线溯源三元组**:截至 `2026-10-22` · 批次 `regress-disconst` · 干净树。

```
total=4462  passed=4426  failed=0  inconclusive=0  skipped=36   error CS = 0
基线 regress-rulenine2 = 4451 / 4415 / 0 / 36
Δtotal = Δpassed = +11     全名级差集 = 新增 11 / 消失 0
```

新增 11 条**全部**是 `DiseaseConstantRegistryGateTest` 的用例(见 §四),
零消失、零回归。
