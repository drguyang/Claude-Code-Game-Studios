# 评审原件 —— foraging-001「品种闭包 + 数据装载校验」

> **日期**:2026-10-26(判定当日)· **原件落盘**:2026-10-26(同日)
> **对象**:`production/epics/foraging/story-001-variety-closure-and-data-load-validation.md`
> **被评件**:`assets/data/foraging_constants.json` ·
> `unity/Assets/Sim/Foraging/{ForagingConstants,QualityDistributionShape}.cs` ·
> `unity/Assets/Editor.Tools.Bake/ForagingConstants{Schema,Binder}.cs` ·
> `unity/Assets/Tests/EditMode/Foraging/foraging_data_load_test.cs`
> **评审方式**:`lead-programmer` 子代理,`/code-review` 口径
> **裁定**:**APPROVED WITH SUGGESTIONS** —— **零 BLOCKING**;WARNING 2 条(W-1/W-2)· MINOR 4 条(M-1..M-4)· 缺口 1 条(W-3)
> **纪律**:本件按 Phase 8b 双列结账(`Landed` / `Proven` / `Branch`);未跑判据的项**不得**标 `Proven ✅`。

---

## 〇、本件的性质声明(必读)

⚠️ **本件不是评审当场产出的原始报告。** 评审本身于 2026-10-26 由 `lead-programmer` 子代理执行,
其结论当天已写入 story 卡 `§Code Review` 与证据件
`production/qa/evidence/impl-s20-3-foraging001-2026-10-26.md §四之四`。

本件是**同日事后**按 `coding-standards.md` §Review Evidence Standards 的格式要求补落的**原件**,
目的是让「原判定 → 修复落点 → 验证命令」三列可以被第三方独立复核。

**按 `.claude/docs/review-workflow.md` §不可追补 的口径如实声明**:

> 本件记录的是 **2026-10-26 评审当时**的判定,以及**此后修复轮**的落点。
> 它不是「事后重做的一次评审」—— 事后重做评的是当下代码,得到的是**新**判定,
> 不追认原判定。本件的原判定部分引用的子代理结论**未经事后重新验证**;
> 修复落点部分则由本件实测复核(见 §三 / §四)。

⚠️ **本次评审零 BLOCKING**,因此 `coding-standards.md` 那条「产 BLOCKING 判定的评审须落原件、
无原件不得转 Complete」的**义务未被触发**。本件是**主动补落**,不是补债。

---

## 一、裁定摘要

| 级别 | 编号 | 一句话 |
|---|---|---|
| **缺口** | **W-3** | `ForagingConstantsBinder` **无 Baker 调用它** —— 25 个 Binder 里唯一只有阶段 2 没有阶段 3 的;ADR-014 链路缺「Baker 写 `*.cooked`」一环 |
| WARNING | W-1 | `ForagingConstants` 构造器 95 行、CC≈16(超 `/code-review` 通用清单的 ≤40 行 / CC<10) |
| WARNING | W-2 | `ForagingConstantsBinder.Bind` 约 150 行(同上) |
| MINOR | M-1 | `QualityDistributionShape.cs` 注释称「四条」判据,实为**五条**(漏数 `maxQuality < 1`) |
| MINOR | M-2 | 真实表测试用夹具常量 `MaxQuality = 5`,未走 21a 真源 |
| MINOR | M-3 | `:105` catch 过滤器 `ArgumentNullException` 分支冗余(派生自 `ArgumentException`) |
| MINOR | M-4 | 缺键时同一错误报两遍(5 个 `ReadRequiredXxx` 各自报 + `RequiredKeys` 循环再报) |

### 评审自陈未及项(如实登记,不掩饰)

- **未独立重跑测试** —— 测试数字引自 story 卡,非自行执行
- **GDD F-17-1 / F-17-2 / F-17-3 未逐行读**
- **`control-manifest.md` Foundation 层规则未读原文**

### 一处评审结论经实测**不成立**(须记录)

评审为「甲案(本 story 内补齐烘焙接线)」给出的**理由③**是:
> 「下游 story-002/004 经 `IDataProvider` 消费 `*.cooked`,是硬前置」

**实测不成立** —— `story-002` / `003` / `004` / `005` **零命中** `cooked` / `IDataProvider`:

```bash
grep -rn "cooked\|IDataProvider" production/epics/foraging/story-00{2,3,4,5}*.md
# ⇒ 零命中
```

⇒ **甲案仍成立**,但理由是「让 Binder 的校验在**生产烘焙路径**上可证伪」,
**不是**「下游已依赖」。story 卡 `§Code Review` 已就地登记该订正。

---

## 二、W-3(缺口)—— 烘焙接线不存在

### 原判定

ADR-014 的链路 = 作者态 JSON → 阶段 1 词法 → 阶段 2 Binder 校验 → **阶段 3 Baker 写 `*.cooked`** → `IDataProvider`。
本 story 交付时**只完成阶段 2**。

四条独立实测(全部只读):

| # | 实测 | 命令 | 结果 |
|---|---|---|---|
| 1 | 有无 `ForagingConstantsBaker` | `ls unity/Assets/Editor.Tools.Bake/*Foraging*Baker*.cs` | **无此文件** |
| 2 | `DataBakeMenu` 是否注册 | `grep -n "Foraging" unity/Assets/Editor.Tools.Bake/DataBakeMenu.cs` | **零命中** |
| 3 | 有无 cooked 产物 | `ls unity/Assets/DataCooked/foraging*` | **无** |
| 4 | Binder 有无生产调用方 | `grep -rn "ForagingConstantsBinder.Bind" unity/Assets/ --include=*.cs` | 仅测试文件 |

### 处置(用户裁定 2026-10-26 = 甲案:本 story 内补齐)

| 项 | 落点 |
|---|---|
| cooked 编码器 | `unity/Assets/Editor.Tools.Bake/ForagingConstantsCookedWriter.cs` |
| 烘焙入口 | `unity/Assets/Editor.Tools.Bake/ForagingConstantsBaker.cs` |
| 菜单注册 | `DataBakeMenu.BakeForagingConstants()` —— `[MenuItem("大医精诚/数据管线/烘焙 foraging_constants(17 采集调参表)")]` |
| 产物入库 | `unity/Assets/DataCooked/foraging_constants.cooked.bytes` —— **352 B**,`ConfigVersion = 0x1CFDA66D` |
| 漂移门登记 | `cooked_drift_all_artifacts_test`: `BakedArtifacts` +1、`pinnedDeclared` **24 → 25** |
| 接线夹具 | `foraging_bake_wiring_test.cs` —— 初版 5 条,证伪轮后补 1 条(见 §四) |

**跨表联合校验**:Baker 从 21a 的**真 binder**(`ItemDatabaseBinder.BindAll`)读 `MAX_QUALITY`,
使「17 的表 × 21a 的常量」这一对在**生产烘焙路径**上被联合校验,而非各测各的。

---

## 三、修复轮结账表(双列 + Branch)

| ID | Landed | Proven | Branch | File(s) | Proven-by(可复算判据) |
|---|---|---|---|---|---|
| **W-3** | ✅ | ✅ | ✅ 3/3 | `ForagingConstantsBaker.cs` · `ForagingConstantsCookedWriter.cs` · `DataBakeMenu.cs` | ① `test_w3_realTable_bakesSuccessfully` ② `test_w3_badSourceTable_bakerRejects`(**停用 Baker 的 Binder 调用 ⇒ 红**)③ `test_w3_twoBakes_produceIdenticalBytes` |
| **W-1** | ❌ | ❌ | — | `ForagingConstants.cs` 构造器 | **不修**(用户裁定)。非项目硬规定 —— `grep -n "40 行\|复杂度\|Cyclomatic" .claude/docs/coding-standards.md docs/architecture/control-manifest.md` **零命中**;仅 `/code-review` 通用清单 |
| **W-2** | ❌ | ❌ | — | `ForagingConstantsBinder.Bind` | **不修**(同上)。且拆分会让 15 条守卫行号全变 ⇒ 35 条证伪轮锚点失效 |
| **M-1** | ✅ | ✅ | — | `QualityDistributionShape.cs:40` | 注释改「五条」并逐条列出。**纯注释,无可执行判据** —— 由读数证明:`grep -c "throw new Argument"` = 5 |
| **M-2** | ✅ | ✅ | ✅ 2/2 | `foraging_data_load_test.cs` | `test_realTable_loadsAndSatisfiesAllShapeInvariants` 走新增 `Read21aMaxQuality()`(21a 真 binder);`MaxQuality` 常量加注「合成夹具专用」 |
| **M-3** | ✅ | ✅ | ✅ 2/2 | `QualityDistributionShape.cs:105` | 单 `catch (ArgumentException ex)`;`test_qualityDistribution_allZero` / `lengthMismatch` 经 `IsValid` 非抛形路径必走该 catch |
| **M-4** | ✅ | ✅ | ✅ 2/2 | `ForagingConstantsSchema.cs`(5 个 `ReadRequiredXxx`) | `test_m4_missingRequiredKey_reportedExactlyOnce`(恰 1 次)+ `test_m4_missingMultipleKeys_allReportedInOnePass`(一次报全) |

**一行两列皆 ✅ 才算结清。** `W-1` / `W-2` 为 `Landed=❌ / Proven=❌` —— 是**用户明示不修**,
不是遗漏;已暴露而非隐藏。

---

## 四、证伪轮(第七类证据 —— 本轮抓出的假绿)

**这一节记录的是本件最重要的事实:修复轮的夹具**首版是假绿**。**

### 首跑(2/7 转红)

| 变异 | 结果 | 分析 |
|---|---|---|
| W1 `Bind(json, maxQuality)` → `Bind(json, 5)` | **`red=0`** | 靶测 = `test_w3_badSourceTable_bakerRejects`(坏 `regrow_window_ticks`) |
| W2 `Read21aMaxQuality(repoRoot)` → 常量 `5` | **`red=0`** | 同上 |
| W3–W7(头部五槽 + 产物名) | `red=1` × 5 | 靶测均正确 |

### 双重根因(两个独立缺陷叠加)

1. **靶测选错**:W1/W2 动的都是 `maxQuality`,而原靶测断的是 `regrow_window_ticks ≤ 0`
   —— **该守卫与 `maxQuality` 毫无关系**。任何只动 `maxQuality` 的变异都不可能在它上面转红。
2. **真实值让变异前后同构**:真实 `MAX_QUALITY = 5`,而 17 的 `cap_table` 最大值只有 **2**
   ⇒ `2 ≤ 5` 恒成立 ⇒ 「写死 5」与「真读 21a」的 `BindResult` **逐位相同**
   ⇒ 产物字节相同 ⇒ **整个夹具集合对该属性零判别力**。

> ⚠️ **我最初把 W1 的 `red=0` 判成「变异打空靶」,并称 W2 才是「正确靶」。这个判断被 W2 自己的
> `red=0` 推翻了。** 归因顺序应是:① 靶测到底测没测这条属性 → ② 真实值是否让变异前后等价
> → ③ 才是「变异打空靶」。跳过错前两步会把**夹具假绿**误判成**变异缺陷**并放行。

### 修法

新增 `test_w3_maxQuality_isReadFrom21a_notHardcoded`:
把 **21a 的** `max_quality` 由 5 改成 **1**(17 的表**一字不改**),使 `cap_table` 里的 2 成为违例。
真读路径 ⇒ 拒收;写死 5 的变体 ⇒ 放行。**一条夹具同时钉住 W1 与 W2 两个属性。**

前提可复算:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import json
ct=json.load(open('assets/data/foraging_constants.json'))['cap_table']
print('cap_table 含 >1 的元素数 =', sum(1 for v in ct if v>1))
"
grep -cF '"max_quality": 5' assets/data/item_database_constants.json
# 实跑:cap_table 含 >1 的元素数 = 41 ; 锚串命中 = 1(唯一)
```

### 第二跑(7/7 转红)

```
===== SUMMARY: 7 / 7 turned red =====
```

| 变异 | 属性 | 靶测 | 结果 |
|---|---|---|---|
| W1 | `Bind(json, maxQuality)` → `Bind(json, 5)` | `test_w3_maxQuality_isReadFrom21a_notHardcoded` | **`red=1`** ✅ |
| W2 | `Read21aMaxQuality(repoRoot)` → 常量 `5` | 同上 | **`red=1`** ✅ |
| W3 | magic `'D'` → `'X'` | `test_w3_cookedHeader_matchesCookedFormat` | **`red=1`** ✅ |
| W4 | `formatVersion` → `99u` | 同上 | **`red=1`** ✅ |
| W5 | `configVersion` → `0u` | 同上 | **`red=1`** ✅ |
| W6 | `payloadLength` → `+1` | 同上 | **`red=1`** ✅ |
| W7 | `CookedFileName` 加 `_v2` 后缀 | `test_w3_cookedFileName_matchesMenuConstant` | **`red=1`** ✅ |

**还原判据 = SHA-256 逐字节**,脚本还原后比对,不等即 `exit 9` 中止整轮:

```
ForagingConstantsBaker.cs          ce4def2dccd30e081383219b44557c0198dc230e4b211be39c654589d77f67e5
ForagingConstantsCookedWriter.cs   e56a95fb4b2fe46fcece6ad84202c39f94bd8c72b350413466670cc1110555c0
```

⚠️ **两个数字都必须保留**:首跑 2/7、修后 7/7。只记后者 = 把修完的结果当成一开始就有的结果。

---

## 五、验证命令汇总(第三方可重跑)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 夹具级(Foraging 两文件)
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' unity/Logs/w-rebuild.xml | head -1
# 预期:total="52" passed="52" failed="0"

# ② 全量回归
grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*" inconclusive="[0-9]*"' unity/Logs/w-final-full.xml | head -1
# 预期:total="5174" passed="5139" failed="0" inconclusive="0"

# ③ 产物存在性与大小
ls -l unity/Assets/DataCooked/foraging_constants.cooked.bytes
# 预期:352 字节

# ④ 产物名两处同源
grep -n "foraging_constants.cooked.bytes" unity/Assets/Editor.Tools.Bake/{ForagingConstantsBaker,DataBakeMenu}.cs
# 预期:各 1 处,字面相同
```

---

## 六、结论

| 项 | 结果 |
|---|---|
| BLOCKING | **0** |
| WARNING | 2(W-1 / W-2,**用户裁定不修**,理由已核:非项目硬规定) |
| MINOR | 4(**全修**,各带夹具) |
| 缺口 | 1(W-3,**甲案补齐**,含跨表联合校验) |
| **裁定** | **APPROVED WITH SUGGESTIONS** |

⚠️ **W-1 / W-2 是已知的形态偏差,不是「没问题」。** 若项目将来把「≤40 行 / CC<10」
升为硬规定,这两条须重新评估。
