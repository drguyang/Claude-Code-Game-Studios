# 修复证据原件 —— S11-11 / case-system story-001 B-3(测试头 AC 号一号两义)

**日期**:2026-10-15
**对象**:`unity/Assets/Tests/EditMode/CaseSystem/case_identity_routing_test.cs`(**纯测试件,零产品代码**)
**来源判定**:`production/qa/evidence/review-case-system-story-001-retro-2026-10-15.md` §二 **B-3**(BLOCKING)
**排期**:`production/sprints/sprint-11.md` **S11-11**(用户裁定「必须项优先」并入;估 0.1 天)
**性质**:本件 = **修复证据**,**不是评审**(承 `.claude/docs/review-workflow.md` §义务 3 不可追补)。

> ⚠️ 承 S11-8 / S11-10 的同一纪律:原始 XML 落在 `unity/Logs/`(**`.gitignore:196`**)⇒ **易失中间产物**,
> 不得充当交付证据。故判据、命令、输出原文收进本件,使其**自洽可复核**。

---

## 一、原缺陷判定(逐字承补评件 §二 B-3)

> `:5-8` 测试头**自填** `AC-37-03/04/05/06`,与 GDD 权威编号(`case-system.md:1036-1051`)
> **指向完全不同的需求**:GDD AC-37-03 = 重复结案幂等拒收 · AC-37-04 = 未勾「已处置」拒绝 ·
> AC-37-05 = 判断记录落病例流 · AC-37-06 = 迁移重放一致;
> 这三条在本目录内**已被姊妹测试占用并声称覆盖**;
> ⇒ 任何按 AC 号做的覆盖矩阵都会**重复计数**,把 story-001 的测试误记为 AC-37-03/05/06 的证据;
> story 文件里这 4 条 bullet **本就无编号**,测试头是**自行编号**。**修复零成本**:改为无编号引用。

**病因**:story-001 §Acceptance Criteria 的六条 bullet 里,**只有前两条**(AC-37-01 / AC-37-02)
在 story 里带号(且与 GDD 一致);**后四条本就无编号**(kindgen 断言批 / 载荷形状断言 /
跨流全序复验 / 高水位纯净)。测试头把「第 3–6 条 bullet」机械地顺延编号为 `AC-37-03/04/05/06`
—— 而 GDD 的 `AC-37-03…06` 是**另外四件事**。

### 1.1 ⚠️ 计划口径订正:范围比 retro 点名的更大(已报用户并获批「全文件去号」)

retro 只点名**测试头 `:5-8`**。实测该误编号在文件内**有三层**:

| 层 | 处数 | 形式 |
|---|---|---|
| ① 测试头 | 4 | `// AC-37-0X:…` |
| ② 文件体注释 | 14 | `// AC-37-0X:…` 段落标 + `/// <summary>AC-37-0X …` 方法标 |
| ③ **方法名** | **10** | `test_ac3703_a1_…` 等 —— 覆盖矩阵第一眼扫的正是这里 |

**只改测试头等于没改** —— 任何按 AC 号做的覆盖矩阵会扫**文件任意位置**(含方法名),
重复计数照旧产生。
**本件按用户裁定取「全文件去号」**,即 ①+②+③ 共 **25 处**。

**保留项(刻意不去号,已逐条核实合法)**:
`AC-37-01`(4 处)· `AC-37-02`(2 处)—— 与 GDD 权威含义一致;
`AC-37-14`(2 处)—— GDD `:1110` = `PatternRecognized` 载荷含 `salted_key` 且不含裸 `disease_id`,
本文件是它的**载体半边**,引用正确。

---

## 二、修复落点

### 2.1 测试头(去号 + 分组 + 就地登记编号纪律)

修复后头部分两组 —— **有号的归 GDD,无号的自成一组**,并附「勿再补号」纪律:

```
// ── GDD AC 覆盖(编号 = GDD 权威编号,case-system.md 的 AC 表)──────────────
// AC-37-01:…
// AC-37-02:…
//
// ── story 自定判据(⚠️ 四条**均无 GDD AC 编号** —— story §AC 里本就无号)──────
// · kindgen 断言批 A1–A5 全过
// · 载荷形状断言 —— …
// · 跨流全序复验 —— …
// · 高水位纯净 —— …
//
// ⚠️ 编号纪律(2026-10-15 · sprint-11 S11-11 修复 · 补评件 story-001 §二 B-3):
//   本头此前把上面四条**自行编号**为 AC-37-03/04/05/06 —— 这四个号在 GDD 里指向**别的需求**
//   (…),且**已被本目录姊妹测试占用**。⇒ 按 AC 号做的覆盖矩阵会**重复计数**。
//   **无编号 = 这四条的真实状态,勿再补号** —— 方法名与注释同此纪律(全文件)。
```

📌 **本文件内仍可见 `AC-37-03/04/05/06` 字样**,但**仅出现在该纪律说明里**(用于说明那四个号
在 GDD 与姊妹测试里的真实含义)。**不是残留,是刻意保留的反例材料。**

### 2.2 文件体注释(14 处)

段落标 `// AC-37-0X:<故事 bullet 名>` → `// [story 判据] <故事 bullet 名>`;
方法标 `/// <summary>AC-37-0X …` → `/// <summary>[story 判据] <故事 bullet 名>:…`。
`[story 判据]` 前缀使这些标注**可 grep、且一眼可辨非 AC**。

### 2.3 方法名(10 处)

按故事 bullet 名归组,**去掉 AC 号**:

| 修前 | 修后 |
|---|---|
| `test_ac3703_a1_fiveCaseKinds_routeToCaseStream` | `test_kindgenA1_fiveCaseKinds_routeToCaseStream` |
| `test_ac3703_a2_casePayloadSchemas_integerDomainOnly` | `test_kindgenA2_casePayloadSchemas_integerDomainOnly` |
| `test_ac3703_a3_caseKindNames_unique` | `test_kindgenA3_caseKindNames_unique` |
| `test_ac3703_a4_caseKindAuthors_all37` | `test_kindgenA4_caseKindAuthors_all37` |
| `test_ac3703_a5_caseKinds_presentInEventKindEnum` | `test_kindgenA5_caseKinds_presentInEventKindEnum` |
| `test_ac3704_payloads_noOpenedTickField` | `test_payloadShape_payloads_noOpenedTickField` |
| `test_ac3704_patternRecognizedPayload_containsSaltedKey` | `test_payloadShape_patternRecognizedPayload_containsSaltedKey` |
| `test_ac3705_crossStreamOrder_deterministicNoTies` | `test_crossStreamOrder_deterministicNoTies` |
| `test_ac3705_streamPriority_orderHistoryCaseWorld` | `test_crossStreamOrder_streamPriority_orderHistoryCaseWorld` |
| `test_ac3706_highWaterMark_sentinelMinusOne_noPollution` | `test_highWaterMark_sentinelMinusOne_noPollution` |

**改名安全性(实测,非推断)**:全仓 grep(排除 `unity/Assets`)确认上述方法名
**零外部引用** —— 无 `.md` / `.yaml` / `.py` / `.sh` 按名引用它们;
Unity Test Framework 按 `[Test]` 特性发现,**不依赖方法名**;
`-testFilter` 走**类全名**,不受影响。⇒ 改名为**零行为影响**。

---

## 三、验证命令(可证伪 · 逐字可复跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
U=/home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity
F=DaYiJingCheng.Tests.CaseSystem.CaseIdentityRoutingTest

# ── ① 夹具(改名后须仍全绿,且测数不变)──────────────────────────────
"$U" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode \
  -testFilter $F -testResults "$PWD/unity/Logs/s11_s11_identity.txt" -logFile unity/Logs/s11_s11_identity.log

# ── ② 覆盖重复计数的证伪(本 AC 的判据面 —— 见 §四)──────────────────
D=unity/Assets/Tests/EditMode/CaseSystem
for n in 3703 3704 3705 3706; do
  for f in case_identity_routing_test.cs case_close_precondition_test.cs \
           case_open_and_judgment_test.cs case_replay_persistence_test.cs; do
    b=$(git show HEAD:$D/$f 2>/dev/null | grep -c "test_ac$n")
    a=$(grep -c "test_ac$n" $D/$f 2>/dev/null | head -1)
    echo "test_ac$n  $f  修前=$b  修后=${a:-0}"
  done
done
```

判决口径 = **退出码 0 且 XML `failed="0"`** 双信号一致。

---

## 四、结果

### ① 夹具:13/13 绿,测数不变(改名未增未减)

| 状态 | 退出码 | 结果 | `error CS` |
|---|---|---|---|
| 改名后 | **0** | `total=13 passed=**13** failed=0` | **0** |

**测数 13 → 13**(修前同文件也是 13 测)⇒ 改名**未丢测、未重名**。

### ② 覆盖重复计数 —— 本 AC 的**可证伪判据**

S11-11 的 AC 是「任何按 AC 号的覆盖矩阵**不再重复计数**」。这是**覆盖声明属性**,
判据是 grep 而非运行时行为 —— 故证伪 = **同号前缀下方法数的归属变化**:

| 号前缀 | 修复前合计 | 修复后合计 | 归属变化 |
|---|---|---|---|
| `test_ac3703` | **7** | **2** | −5(本文件的 kindgen 断言批)。真主人 = `case_close_precondition_test.cs`(**幂等拒收**) |
| `test_ac3704` | **4** | **2** | −2(本文件的载荷形状)。真主人 = `case_close_precondition_test.cs`(**未勾「已处置」拒绝**) |
| `test_ac3705` | **5** | **3** | −2(本文件的跨流全序)。真主人 = `case_open_and_judgment_test.cs`(**判断记录落病例流**) |
| `test_ac3706` | **1** | **0** | −1(本文件的高水位)。真主人 = `case_replay_persistence_test.cs`(round-trip) |

**修复前的实况**(可复跑复现)—— 同一前缀下**两种互不相干的语义**:

```
test_ac3703_a1_fiveCaseKinds_routeToCaseStream        [本文件 = kindgen 断言批]
test_ac3703_a2_casePayloadSchemas_integerDomainOnly   [本文件 = kindgen 断言批]
test_ac3703_a3_caseKindNames_unique                   [本文件 = kindgen 断言批]
test_ac3703_a4_caseKindAuthors_all37                  [本文件 = kindgen 断言批]
test_ac3703_a5_caseKinds_presentInEventKindEnum       [本文件 = kindgen 断言批]
test_ac3703_alreadyClosed_idempotentReject            [close 文件 = 幂等拒收]
test_ac3703_edge_sameTick_doubleIntent_idempotent     [close 文件 = 幂等拒收]
```

retro 的原话「任何按 AC 号做的覆盖矩阵都会**重复计数**」在此**逐字成立**:7 个方法同挂
`AC-37-03`,而其中 5 个与该 AC**毫无关系**。

**反证方向**:把 2.3 表左列任一方法名改回(或把测试头 4 条补回号),上表对应行的
「修复后合计」立即回升 —— 重复计数**即时复现**。本件不另跑批(纯文本属性,`git diff` 即证)。

### ③ 静态面

`grep -c "error CS"` = **0**。**零产品代码改动**(`git diff --name-only` 仅本测试件)
⇒ 门 A / b2 / ADR-025 §① / ADR-029 及 `Sim` 引用集**全不受影响**。

### ④ 全量零回归(与 S11-8 / S11-10 同口径)

| 套件 | 结果 | 退出码 | 对比 |
|---|---|---|---|
| EditMode | `total=3334 passed=3288 failed=0 inconclusive=0 skipped=46` | **0** | 与 S11-10 后**逐位相同** |
| PlayMode | `total=186 passed=186 failed=0` | **0** | **逐位等于 M2 基线**(**M2 链零回退**,DoD 特有判据) |

**EditMode 总数 3334 与改前一致** ⇒ 改名**未增测、未减测、未重名**
(若改名造成重名,UTF 会因同名冲突少发现一条;若丢测,总数会下降 —— 两者均未发生)。

---

## 五、未清项与两处观察

- **story-001 维持 `In Review`** —— 补评 4 BLOCKING 中本件只清 **B-3**;
  **B-1**(AC-37-06 判据算术上不可证伪,须**退设计重述**)· **B-2**(kindgen A2 空跑)·
  **B-4**(kindgen 载体借绿)三条**顺延 sprint-12**,已带归属登记。
- **观察 ①(非本件缺陷,如实登记)**:修复后 `test_ac3706` 的**方法名**命中数 = **0** ——
  因为 AC-37-06 的真实主人 `case_replay_persistence_test.cs` **不在方法名里编 AC 号**
  (它只在注释里写 `AC-37-06`)。⇒ 这暴露的是**命名惯例本身的覆盖不均**,
  **不是本件引入的缺口**;修复前那 1 个命中恰是**错的那个**(高水位,与迁移重放无关),
  故本改动是**移除假阳性**,不是制造空洞。是否统一各文件的命名惯例 ⇒ 归 **sprint-12 断言形态批**。
- **观察 ②(不在 B-3 面内,未动)**:story-001 §QA Test Cases(`:70-84`)另有**第三套本地编号**
  `AC-1 / AC-2 / AC-3`。它**不在 GDD 的 `AC-37-XX` 命名空间内**,故不与权威编号撞号,
  **不构成一号两义**;且其 3 条与 §AC 的 6 条 bullet **不对齐**(3 vs 6)。
  本件**未动**它 —— 归属与口径归 story-001 的 §QA Test Cases 修订轮(与 B-1 同批更合适)。
