# integration/processing/

按系统分目录的测试账本路径(`.claude/docs/coding-standards.md` §Testing Evidence by Story Type)。
**Unity 只编译 `unity/Assets/` 树** —— 本目录是**账本(登记)侧**,不是编译落点。

## Story 001(配方子集与可发起谓词 —— AC-18-18)—— 落点说明

| 内容 | 路径 |
|---|---|
| 编译中的测试源(真身) | `unity/Assets/Tests/EditMode/Processing/recipe_owner_partition_test.cs` |
| 程序集 | `unity/Assets/Tests/EditMode/EditMode.asmdef` |
| 被测门(分区) | `unity/Assets/Editor.Tools.Gates/RecipeValidationGates.cs`(`PartitionRecipeOwners`) |
| 被测门(烘焙全链) | `unity/Assets/Editor.Tools.Bake/ItemDatabaseBaker.cs`(`BakeFromRepo`) |
| 被测数据 | `assets/data/item_database_recipes.json` · `item_database_items.json` |
| 运行方式 | `unity test unity --mode EditMode --filter RecipeOwnerPartition` |

> 承 `tests/integration/item_database/README.md` 与 `tests/integration/audio_system/README.md`
> 同一先例:账本路径登记 + 真身落 `unity/Assets/Tests/` 树,两处以本 README 互链。

## ⚠️ 本 Story 的判据订正史(2026-10-20 · S-2)

**原判据**:`GIVEN Recipe.owner 字段落地 WHEN 对三子集求并/交 THEN 并 = 全表、两两交 = ∅`,
另挂 `EXTERNAL · BLOCKED-BY-21a(O-18-R1)`,理由写「字段有 schema、无实现」。

**两处失真(实测)**:

1. **「字段无实现」不成立** —— `RecipeOwner` 枚举(`Sim.Contracts/ItemDatabase/RecipeOwner.cs:9`)、
   `Recipe.Owner` 属性(`Recipe.cs:41`)、烘焙读取(`CookedCodec.cs:181`)均在盘(`1c2bfba` 引入)。
2. **原判据在空子集下恒真** —— 真表原为 **2 条配方、`owner` 全 `process`** ⇒
   `craft`/`build` 为空 ⇒ 并 = 全表 ✅、两两交 = ∅ ✅,**恒绿而什么都不测**。

**更深一层(订正时才发现)**:`PartitionRecipeOwners` 的实现是
`if / else if / else` 三分支链 —— **「每条记录恰入一个子集」由代码结构保证,与数据无关**。
⇒ 沿用至今的 `test_ownerPartition_threeSubsets_unionEqualsFullTablePairwiseDisjoint`
测的是**算法自洽性**,不是 AC 的数据覆盖语义。**它绿不能推出 AC 绿。**

**订正为三段判据**:

| 段 | 内容 | 有判别力? |
|---|---|---|
| **① 非空前置** | `\|process\| ≥ 1 ∧ \|craft\| ≥ 1 ∧ \|build\| ≥ 1` | ✅ **唯一有判别力的一段** |
| ② 完备 | 三子集之并 = 全表(按**下标集合**逐项比,禁只比基数) | ❌ 结构恒真,保留作回归护栏 |
| ③ 互斥 | 三子集两两之交 = ∅(同上) | ❌ 结构恒真,保留作回归护栏 |

②③ 仍保留,理由是**防实现退化**:若有人把划分器改成「按 `processing_state` 是否变化猜归属」
(GDD 规则五明禁的老路),③ 会立刻红。

## AC → 测试函数映射

| AC | 测试函数(`RecipeOwnerPartitionTest` 内) | 性质 |
|---|---|---|
| **AC-18-18 ①**(判据机制:合成夹具) | `test_synthetic_onePerSubset_threePartitionPasses` · `test_synthetic_skewedDistribution_threePartitionPasses` | 合成 —— **不证真表覆盖** |
| **AC-18-18 ①**(负向:空子集必被检出) | `test_negative_onlyProcess_unionAndDisjointHoldButNonEmptyFails` | **核心判别力来源** |
| **AC-18-18 ②**(真表覆盖度) | `test_realSeed_allThreeOwnerSubsetsNonEmpty` | **真表半边 —— 独立记分** |
| 数据完整性守卫 | `test_realSeed_ownerCountMatchesRecipeCount` | 钉住「按出现序对齐下标」假设 |
| 数据改动未踩其它门 | `test_realSeed_bakesCleanAfterCraftAndBuildRecipesAdded` | 门闩 |

### 分段记分(禁合并成一条绿)

- **① 判据机制已证** —— 合成夹具全绿 + 负向夹具证明空子集会红。
- **② 真表覆盖度** —— 由 `test_realSeed_allThreeOwnerSubsetsNonEmpty` 独立断言。
  **2026-10-20 之前该测必红**(`craft`/`build` 空);

## 2026-10-20 数据补口

真表 `assets/data/item_database_recipes.json` 原 **2 条、全 `process`**。为使 ② 可判,
补两条**占位配方**(数值为夹具值,非平衡值 —— 承该表 `_note` 的既有口径):

| recipe_id | owner | 输入 | 输出 | 守恒(AC-21a-65 极值式) |
|---|---|---|---|---|
| `craft_wooden_splint` | `craft` | 干柳树皮×2 | 木夹板×1 | 1 ≤ 2 ✅ |
| `assemble_wooden_post` | `build` | 干柳树皮×3 | 木柱×1 | 2 ≤ 3 ✅ |

配套新增两个物品行(`item_database_items.json`):`wooden_splint`(`tool`)· `wooden_post`(`build_part`)。

⚠️ **这是「判据可执行」的补口,不是内容轮** —— 真配方内容仍归数值轮 / 21a 内容批。
⚠️ `owner=process ⇒ 必有 state 变化` 是**单向校验**(GDD 规则五);
`craft` / `build` 不要求 state 变化,故二者 `boundary_state` 为空数组合法
(`ValidateTransition` 对空列表直接通过,见 `ItemValidationGates.cs:366`)。

### ⚠️ 补口过程中的一次改道:craft 产出**刻意避开 `drug` 类**

首版 craft 产出写的是 `herbal_salve`(`category: drug`),**实测会踩红 11 的 AC-11-20**。
根因是 `PrescriptionActionsBinder.cs` 的两行:

- `:238` `drugDoseRanges[drugId] = doseRange;` —— 对 `category == "drug"` 的条目
  **无条件入表**,**哪怕 `dose_range` 是 null**(`doseRange` 保持 null,键照入);
- `:264-267` `foreach (var drugId in drugDoseRanges.Keys) if (!seenLexicon.Contains(drugId))`
  ⇒ **每一味药都必须在本草词表 `materia_lexicon.json` 里有功效词**。

⇒ 新增一个 `drug` 类物品 = **被迫替 11 epic 写一条古籍功效词**(11 的内容)。
S-2 的补口**不该被逼着写别的 epic 的内容**,故改为 `category: tool`(木夹板 —— 本作语义亦更贴)。

**实测 `tool` 类零专属门**:`ItemDbValidation.cs:115` 只有枚举解析一行;
`RecipeValidationGates` 对 `outputs` 无任何品类限制(只查 `qty ≥ 1` 与非空)。
品类的附属约束是**单向**的(`material` 才可非空 `gather_profile` / `drug` 才可非空 `drug_profile` /
`weapon` 才可带 `inflicts_injury`),`tool` 三项全 null 合法(先例:`iron_scalpel` 除
`inflicts_injury` 外全 null)。

**反向亦无约束**:词表键**不要求** ⊂ 药品集(无「孤儿词」检查),故本改道不留残债。

---

## 证伪轮(2026-10-20)—— 判别力实测,不是「测试名 + 行号」

**判据口径**(承 `sprint-14.md` Risks 第 3 条 · retro-13 行动项 2 · S12-4 Falsifiability):
**停用生产码守卫 ⇒ 断言必须转红**;仍绿 ⇒ 该夹具弱判别面 = **BLOCKING**。

### 被测夹具

`RecipeOwnerPartitionTest.test_realSeed_allThreeOwnerSubsetsNonEmpty`
(真表半边 —— AC-18-18 的 ② 段;**独立记分,不与合成夹具合并**)。

### 变异矩阵(每例:改 → 跑 → 还原,**同一命令内**完成)

| # | 变异 | 期望 | 实测 | 退出码 |
|---|---|---|---|---|
| **M0** | 无(基线自检) | `passed=1 failed=0` | ✅ `1 / 0` | 0 |
| **M1** | 删掉 craft 配方(`craft_wooden_splint`) | craft 子集空 ⇒ **红** | ✅ `0 / 1` **转红** | 8 |
| **M2** | 删掉 build 配方(`assemble_wooden_post`) | build 子集空 ⇒ **红** | ✅ `0 / 1` **转红** | 8 |
| **M3** | craft 配方 `owner` 改 `"process"` | craft 子集空 ⇒ **红** | ✅ `0 / 1` **转红** | 8 |

**结论**:该夹具对「子集为空」这一数据缺口**有判别力**(三次变异三次转红),
**不是**在空集下恒绿的假绿夹具。

### 还原纪律与校验

- **禁用 `git checkout` / `git stash`**(会抹掉整个文件的未提交改动)
  —— 流程 = **备份 → 单点变异 → 跑 → `cp` 回 → `sha256` 逐字节比对**。
- 每例跑完**立即**还原,不让坏数据跨命令留存;`finally` 里再兜一次 + 比对。
- 数据 `assets/data/item_database_recipes.json` 基线 SHA-256:
  `dba4b001738ce6dd0afa1dcded4a28b2dd0d89dfbbd3c91ad6f7fb86b39af7ad`
  ⇒ **还原后逐字节一致 ✅**;`git diff` 为干净的 `+28`(仅两条新配方,零格式化噪声)。
- 脚本原件:`/tmp/fz-s2-owner-partition.py`(临时件,不入库)。

### ⚠️ 首轮证伪的失败教训(已就地订正,留作记录)

**首轮四次全部报「未出 XML」,被我一度误读成「假绿」。实为脚手架 bug:**

> `unity test --output <path>` 的 `<path>` 按 **cwd** 解析,**不按 project 形参**。

首轮脚本把 `--output` 写成相对路径 `Logs/fz-s2.xml`、却在 `unity/Logs/` 下找结果
⇒ 四次全部 `xml=False`、报「仍绿」;而真相是**四次都跑了**,结果写到了
**仓库根** `Logs/fz-s2.xml`(且被逐次覆盖,只剩最后一轮)。
订正 = **一律用绝对路径**,并在脚本内同时核对与自己取结果的**同一变量**。

**教训**:「没出结果」与「结果是绿的」是**两件不同的事** —— 把前者读成后者,
会得出与事实相反的夹具判决。**报「假绿」前必须先证明「跑出来了且是绿的」。**

> ⚠️ 副作用:`Logs/` 曾被造在**仓库根**(非 `unity/`)。该目录已被 `.gitignore:66`
> (`[Ll]ogs/`)忽略,不进 git;已清理。

---

## 回归对账(2026-10-20 · S-2 数据改动后)

| 项 | 值 |
|---|---|
| 命令 | `unity test <unity/> --mode EditMode --output <绝对路径>` |
| 本轮 | **`total=3951 passed=3915 failed=0 inconclusive=0 skipped=36`**(exit 0) |
| 前次基线 | `3945 / 3909 / 0 / 36` @ 2026-10-20 01:09 UTC · sprint-15 收口轮 · `4937275` |
| Δ | **`+6 / +6 / 0 / 0`** |
| 等式判据 | `total − 前次 = 新增数`(6 条新测)⇒ **✅ 精确成立,零回归** |

> **三元组(强制)**:截至时刻 `2026-10-20 09:46 本地` · 批次 `S-2 数据补口轮(全量 EditMode)` ·
> commit `4937275` + 未提交工作树(数据 + 测试)。
> ⚠️ 本件的 `+6` 是**实跑出来的**,不是照抄的 —— 前次基线亦带三元组,可复算。

---

## ⚠️ 本轮发现的**门缺口**:item DB 无「产物漂移」门(未修,登记)

**实测事实**:改 `assets/data/item_database_*.json` 而**不重烘** `Assets/DataCooked/*.cooked.bytes`,
**全量 EditMode 仍全绿**(本轮实测 `3951/3915/0/36`,而当时盘上产物**是陈旧的**)。

**为什么绿**:所有读 item DB 的测试都走 **Binder 直接绑定源 JSON** 或 `BakeFromRepo`
(现烘),**没有一条读盘上那份已提交的 `.cooked.bytes`** ⇒ 漂移通道**无门覆盖**。

**同型门在本仓已存在,只是没扩到 item DB**:
`unity/Assets/Tests/EditMode/Boot/examine_driver_test.cs:92-108` 对
`diagnosis_signs.cooked.bytes` / `diagnosis_read_floor.cooked.bytes` 断言
「**库内产物须 = baker 现烘输出(改源不重烘 = 漂移,恰红)**」。

**风险**:`.cooked` 是**入 git 的出货产物**(ADR-014 两阶段);源改了忘了重烘 ⇒
**运行期读陈旧数据而测试全绿**。这正是本项目「借绿」家族的同一形态
(不是断言写错,是**该断言的通道没被覆盖**)。

**处置建议(未执行 —— 需裁)**:
1. 把 `examine_driver_test.cs` 那对断言**扩到 item DB 两产物**(最小改动,照抄既有先例);
2. 或立一条通用「DataCooked 全目录漂移门」(`Assets/DataCooked/*.cooked.bytes` 逐份
   与对应 baker 现烘输出比对),把「改源必重烘」变成**构建失败**而非纪律。

⚠️ **本件不擅自动手** —— 新增门属跨系统改动(可能归 item-database epic 或测试基建轮),
须用户裁定归属后再落。**本轮已按纪律手工重烘产物**(见下)。

### 本轮的重烘记录

| 项 | 值 |
|---|---|
| 触发 | 物品行由 `herbal_salve`(`drug`)改为 `wooden_splint`(`tool`)后,盘上产物仍为改动前所烘 |
| 命令 | `unity build <unity/> --target StandaloneLinux64 --execute-method DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeItemDatabase` |
| 判决行 | `[大医精诚] 烘焙完成:item_database_items.cooked.bytes = 715 B, item_database_recipes.cooked.bytes = 524 B, ConfigVersion = 0x2E03E7CF` |
| 退出 | `Exiting batchmode successfully now!` · exit **0** |
| 产物变化 | items **882 → 715 B** · recipes **548 → 524 B** · `ConfigVersion` **`0x78F37B5F` → `0x2E03E7CF`** |
| SHA-256 | items `ff2c7d1b…831cf0` · recipes `55fc80d1…588ef2` |

✅ **重烘后产物与源一致**(`ConfigVersion` 随内容哈希而变 = ADR-014 §五 的联动生效)。

> ⚠️ **回归与本重烘的时序**:全量 EditMode(`3951/3915/0/36`)**跑在 JSON 定稿之后**,
> 故其结论对当前工作树仍然成立 —— 重烘只换产物字节,**不改任何测试的输入**
> (所有 item DB 测试都从源 JSON 现烘)。但**这恰恰是本轮门缺口的证据**:
> 产物陈旧时全量套件**照样全绿**。




