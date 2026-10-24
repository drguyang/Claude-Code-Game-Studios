# 实现证据原件 — `TD-epic-01-f`(`AC-51-D7` `ConfigVersion` 比对)**接线修复**

**日期**:2026-10-24
**对象**:`docs/tech-debt-register.md` §`TD-epic-01-f`(P0 面 = Y · R1)
**触发**:用户 2026-10-24 指令「把 TD-epic-01-f 的修复也做了」
**口径裁定**:用户 2026-10-24「取全局组合值(合 ADR 字面)」(见 §二)
**基线三元组**:`2026-10-24 21:25 CST` · `tdef01f` 批(全量两套件)· commit `8ba3fb4`
⚠️ **跑测时工作树含本轮 11 项未提交改动**(6 改 + 5 新)——
被验的树 = `8ba3fb4` + 那 11 项;下游引用本件时**不得**把 `8ba3fb4` 读成完整基线。

---

## 一、债册登记的三条解除前置 —— 逐条结账

债册 `TD-epic-01-f` §五 原文三条(缺一不算):

| # | 前置(逐字) | Landed(已落) | Proven(已证) | Proven-by(可复算判据) |
|---|---|---|---|---|
| 1 | **源头**:`ConfigVersion` 的运行期真值来源落地 —— `CompositionRoot.cs:415` 的 `0U` 换成它。**这一条是硬前置** | ✅ | ✅ | `DataCoreConfigVersion.Combine`(新件)+ `CompositionRoot.ResolveGlobalConfigVersion`;反证 **C**:`ResolveGlobalConfigVersion` 改 `return 0U` ⇒ `test_wiring_currentConfigVersion_isDerivedNotZero` / `test_wiring_derivedValue_equalsAllProductsCombination` **2 条红** |
| 2 | **消费端**:`TelemetryDeveloperView.cs:77` 把 `ConfigVersionCheck` 传进去 | ✅ | ✅ | `TelemetryDeveloperView.cs:90` 传 `r.ConfigVersion`;反证 **B**:退回 `AllMetricsReport(out int skipped)` ⇒ `test_wiring_endToEnd_mismatch_flagsAndNoMetrics` / `test_wiring_renderDeveloperSummary_gatesOnInjectedCurrent` **2 条红** |
| 3 | **夹具**:补一条**端到端**夹具 —— 走 `TelemetryDeveloperView.Render`(而非直接 `new ConfigVersionCheck`),断言不匹配时报告不含指标行 | ✅ | ✅ | `data_core_config_version_wiring_test.cs:test_wiring_endToEnd_mismatch_flagsAndNoMetrics`;反证 **A/B** 均使其红(见 §四) |

**三列皆 ✅ ⇒ 三条前置结清。**

---

## 二、口径裁定:为什么是「全局组合值」

**实测的分歧**(本轮复核):

```bash
for f in unity/Assets/DataCooked/*.cooked.bytes; do
  printf '%-46s ' "$(basename $f)"
  xxd -s 12 -l 4 -p "$f"
done
```

22 份产物头 offset 12 的 `configVersion` **各不相同**(各烘焙器按自己的源派生,
`DiseaseRegistryBaker.cs:88-98` 明确论证了为何不能混源)。而 ADR-014 §五 的字面是
「对 `assets/data/` **源文本集**求哈希」= **一个值**;存档头只有**一个** u32 字段。

**用户 2026-10-24 裁定**:取**全局组合值**(合 ADR 字面)。
**被否的备选**:「取单一主数据集」—— 那会让其余 21 份数据变了存档头不变
⇒ `AC-51-D7` 只盖住 1/22,仍是大半假绿。

**组合口径**(逐字,`DataCoreConfigVersion.cs` 文件头):取全部 22 条 `DataCoreAddresses`,
按 **address 的 `StringComparer.Ordinal`** 排序,依次喂 `(address UTF-8, 0x00, 4 字节 LE configVersion)`
进 **FNV-1a 32** —— 与 `ConfigVersionUtility.DeriveConfigVersion` 同一算法族。

⚠️ **排序是判据的一部分**:`DataCoreAddresses` 的数组序是**装载序**,历史上追加过 10 次
(7→8→9→11→15→16→19→20→21→22)。若按数组序组合,「接线顺序调整」会改全局值而**数据一字未动**
—— 那是假变更,会让每份旧存档突然「版本不匹配」。⇒ 反证 **D** 钉住此点。

**今日实跑值**(独立复算,不经 C# 实现):

```bash
python3 - <<'PY'
import struct, glob, os
addrs = sorted(os.path.basename(p) for p in glob.glob('unity/Assets/DataCooked/*.cooked.bytes'))
h = 2166136261
for a in addrs:
    head = open('unity/Assets/DataCooked/'+a,'rb').read(20)
    for b in a.encode('utf-8') + b'\x00' + head[12:16]:
        h ^= b; h = (h * 16777619) & 0xFFFFFFFF
print('产物份数 =', len(addrs), '· 全局 ConfigVersion = 0x%08X' % h)
PY
```

输出:`产物份数 = 22 · 全局 ConfigVersion = 0x4A30BB10`(1244707600)。
⇒ 非零,故 `test_wiring_currentConfigVersion_isDerivedNotZero` 的「≠ 0」判据成立。

---

## 三、改动清单(6 改 + 5 新)

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `Gameplay.Presentation/DataCoreConfigVersion.cs` | **新** | FNV-1a 32 `Combine` / `ReadProductConfigVersion`;Ordinal 排序;空集**抛**(不返回 0) |
| 2 | `Gameplay.Presentation/AddressablesDataProvider.cs` | 改 | `LoadAllCookedHeaders()`(只读 20 字节头)+ `LoadGlobalConfigVersion()` |
| 3 | `Gameplay.Boot/CompositionRoot.cs` | 改 | ① `configVersion: 0U` → **惰性**派生(见 §五);② 构造 `CompositionRootServices` 时传 `() => ResolveGlobalConfigVersion(dataProvider)` |
| 4 | `Gameplay.Boot/CompositionRootServices.cs` | 改 | ctor 增 `Func<uint?> currentConfigVersion = null`;补 `using System;` |
| 5 | `Gameplay.Boot/TelemetryDeveloperView.cs` | 改 | `TelemetryPreviewRequest` 增 `ConfigVersionCheck? ConfigVersion`;`:90` 传进去 |
| 6 | `Gameplay.Boot/TelemetryReporter.cs` | 改 | `CurrentConfigVersion` 属性 + ctor 形参;`RenderDeveloperSummary` 增 `uint? saveHeaderConfigVersion` 并自行构造版本门 |
| 7 | `Tests/EditMode/DataPipeline/data_core_config_version_test.cs` | **新** | 10 条:组合口径纯函数性 / 定义域收窄 / 常量对账 |
| 8 | `Tests/EditMode/DataPipeline/data_core_config_version_wiring_test.cs` | **新** | 6 条:三处断点逐点守卫 + 端到端 |

---

## 四、Falsifiability Gate(S12-4 · BLOCKING)—— 六轮反证

**变异窗口**:`2026-10-24 21:05 – 21:24 CST`。窗口内**未读**被变异文件
(承 `tools/falsify/README.md` §并发读取禁令)。**还原判据 = SHA-256 逐字节**,不是 grep。

**还原基准哈希**(窗口前备份,`$CLAUDE_JOB_DIR/tmp/fz-tdef01f-20261024/`):

```
6976df3d50939bb45b4b385efa67629aab628fb26694cc704b8bf9ca43dedd4d  CompositionRoot.cs
ce209bae3190673e07608ff936e6acd02bcd9d7d59b7b97c1a09a225f4759db0  TelemetryDeveloperView.cs
cdd70067655c2cbcc80f83b3612374ca953886f4bba2f94ec040e9d77c2581b5  DataCoreConfigVersion.cs
```

| 轮 | 命中形态 | 变异(停用什么) | 期望 | 实测 | 还原哈希 |
|---|---|---|---|---|---|
| **A** | 定义域收窄 / 接线存在性 | 删掉 `CompositionRoot` 传给 `CompositionRootServices` 的 `currentConfigVersion` 实参(= 断点① 原形态) | ≥1 红 | **5 红**:`currentConfigVersion_isDerivedNotZero` · `derivedValue_equalsAllProductsCombination` · `endToEnd_match_noBannerAndMetricsPresent` · `endToEnd_mismatch_flagsAndNoMetrics` · `renderDeveloperSummary_gatesOnInjectedCurrent` | ✅ 逐字节相同 |
| **B** | 定义域收窄(消费端) | `TelemetryDeveloperView:90` 退回 `AllMetricsReport(out int skipped)`(= 断点② 原形态) | ≥1 红 | **2 红**:`endToEnd_mismatch_flagsAndNoMetrics` · `renderDeveloperSummary_gatesOnInjectedCurrent` | ✅ 逐字节相同 |
| **C** | 阈值/取值(源头) | `ResolveGlobalConfigVersion` 改 `return 0U`(= 断点③ 原形态) | ≥1 红 | **2 红**:`currentConfigVersion_isDerivedNotZero` · `derivedValue_equalsAllProductsCombination` | ✅ 逐字节相同 |
| **D** | 定序 | 删 `DataCoreConfigVersion.Combine` 里的 `addresses.Sort(StringComparer.Ordinal)` | ≥1 红 | **1 红**:`combine_insertionOrderIrrelevant_sameValue` | ✅ 逐字节相同 |
| **E** | 定义域收窄 | 空集分支 `throw` 改 `return 0u` | ≥1 红 | **1 红**:`combine_emptySet_throws` | ✅ 逐字节相同 |
| **F** | 定义域收窄(字节序) | `ReadProductConfigVersion` 改大端 | ≥1 红 | **1 红**:`readProductConfigVersion_littleEndianOffset12` | ✅ 逐字节相同 |

**Gate 结果:PASS** —— 六轮全部转红,无假绿夹具。

⚠️ **如实登记一条判别力边界**:变异 **C** 下
`test_wiring_endToEnd_mismatch_flagsAndNoMetrics` **仍绿**。
原因:该测构造 `staleHeader = current ^ 0xFFFFFFFF`,当 `current` 变成 0 时 `staleHeader` 仍 ≠ 0
⇒ 版本门照常生效。**这不是假绿**(该测验的是「不匹配 ⇒ 不出数」,与当前值是否为 0 无关),
但它**对断点③ 无判别力** —— 断点③ 由 `currentConfigVersion_isDerivedNotZero` 与
`derivedValue_equalsAllProductsCombination` 两条钉住。此处明写,免后来者误读覆盖面。

---

## 五、一处**刻意的设计偏离**:惰性派生(以及为什么不是偷懒)

**初始实现**在装配期派生(`uint configVersion = ResolveGlobalConfigVersion(dataProvider);` 在 `if` 内第一行)。
**实测它会打红两条既有夹具**:

```bash
grep -n "saveBasePath: dir" unity/Assets/Tests/EditMode/PersistenceService/save_composer_wiring_test.cs
# :341  var bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() }, saveBasePath: dir);
# :370  var bag = CompositionRoot.Assemble(new[] { SyntheticDiseaseFixture.Create() }, saveBasePath: dir);
```

这两条传 `saveBasePath` 但 **`dataProvider: null`**(验的是折叠谓词接线,与版本号无关)。
装配期抛会让它们因**不相干的**理由转红 = **修复引入的假红**。

⇒ 改为**惰性**:派生挪进 `compose` 闭包,首次组版时才解析。
**判据不弱化**:`compose` 是唯一读该值的路径,它一旦被调用就必抛。
守卫 = `test_lazy_noProviderWithSavePath_assembleStillSucceeds`(反证:把派生挪回装配期 ⇒ 该测红)。

⚠️ **本条留一个 NOT-RUN,不借绿**:「调用时必抛」今日**不可达** ——
`SaveService.Checkpoint` 在 `RequireNarrativeContext()` 就先抛了
(`narrativeContext` 恒 null,归 `TD-persist-04`),够不到 `compose`。
如实登记为 **NOT-RUN**,不用别的断言冒充。

---

## 六、回归(三元组 + 全名级差集)

**三元组**:`2026-10-24 21:25 CST` · `tdef01f`(全量两套件)· commit `8ba3fb4`
(工作树 = `8ba3fb4` + 11 项未提交改动)

| 套件 | 结果 | 对照基线 |
|---|---|---|
| EditMode | `total=4802 passed=4767 failed=0 inconclusive=0 skipped=35` | `4786/4751/0/35` @ `2026-10-24 19:49 CST` · `s19-smoke-editmode` · `9e23c42` |
| PlayMode | `total=202 passed=202 failed=0 inconclusive=0 skipped=0` | `202/202/0/0` @ `2026-10-24 19:51 CST` · `s19-smoke-playmode` · `9e23c42` |

**增量等式**:`4802 − 4786 = 16`;`4767 − 4751 = 16` ⇒ **+16/+16,零回归**,
与新增测试条数(10 + 6 = **16**)精确吻合。

**全名级差集**(QA plan 强制,不只比总数):

```bash
python3 - <<'PY'
import xml.etree.ElementTree as ET
def load(p):
    r = ET.parse(p).getroot()
    return {tc.get('fullname'): tc.get('result') for tc in r.iter('test-case')}
base = load('unity/Logs/s18-29story004-final-editmode-20261024.xml')
cur  = load('unity/Logs/tdef01f-editmode-full-20261024.xml')
print('新增', len(set(cur)-set(base)), '消失', len(set(base)-set(cur)),
      '翻转', len([n for n in set(base)&set(cur) if base[n]!=cur[n]]),
      '红',   len([n for n in cur if cur[n]=='Failed']))
PY
```

实跑输出:

- **EditMode vs s18 基线**:基线 4736 · 本次 4802 ⇒ **新增 67 · 消失 1 · 翻转 0 · 红 0**
  - 新增 67 = S19-1 的 51(`SaveSlotContractTest` 36 + `SaveComposerWiringTest` 13 + `FoldedPredicateTest` 2)
    + **本轮 16**(`DataCoreConfigVersionTest` 10 + `DataCoreConfigVersionWiringTest` 6)
  - 消失 1 = `FoldedPredicateTest.test_iSaveService_publicMethodSetUnchanged`
    ⇒ 经 `git show 41b7d87` 归因为**改名**(→ `...publicMethodSetIsFive`),实质 **−0**
- **PlayMode vs s18 基线**:基线 202 · 本次 202 ⇒ **新增 0 · 消失 0 · 翻转 0 · 红 0**
- **Skipped 35 条零新增**(与 s19 基线同值)

---

## 七、未核查(禁借绿)

- **`AC-51-D7` 在 ADR-012 三格矩阵下的跨平台表现未测** —— 本轮只跑 Linux-x64-Mono(Editor)。
  FNV-1a 32 是 BCL-only 纯整数运算(`unchecked` 乘法),理论上跨平台逐位一致,但**未实测**。
- **`ConfigVersion` 的烘焙侧派生未复核** —— 本轮只确认「22 份产物头里都有非零值」,
  未验证每个烘焙器的派生是否真的覆盖其全部源(那是各烘焙器的既有判据面)。
- **「调用时必抛」= NOT-RUN**(见 §五)。
- **story 的 `[x]` 未动** —— `production/epics/telemetry-analytics/story-009-blank-and-degraded.md:39`
  勾着 `AC-51-D7`。债册 §四 已声明「本册不撤这个勾」(归 epic 级复评批复核)。
  **本轮修复**使该 `[x]` **首次被生产实现支撑**,但**撤/保勾选仍归 story 归属方**,本轮不代改。

---

## 八、债册状态更新建议(供记账轮执行,本件不代改)

`TD-epic-01-f` 的三条解除前置已全部结清(§一)⇒ 建议索引表状态 `open` → `closed`,
并在 §六 门表相应行加注。

⚠️ **不得据此改写 `TD-epic-01` 其余五条(`a`–`e`)的判定** ——
债册 §六 已声明「性质不同:`a`–`c` 是『接线在、但接线有缺陷』;`f` 是『逻辑在、接线不在』」。
