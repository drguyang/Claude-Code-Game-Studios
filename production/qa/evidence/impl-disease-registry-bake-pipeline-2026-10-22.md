# 9 注册表烘焙管线 —— 实现与证伪证据(2026-10-22)

**批次**:S18 前置续(承接 `impl-ac3-injury-data-rows-2026-10-22.md`)
**commit(改动前)**:`4e005bd`
**判据面**:ADR-014 §二/§三/§四/§五 · `CookedFormat` · `DiseaseRegistryBinder`

---

## 一、本轮做了什么(只此三件,不多不少)

| # | 件 | 状态 |
|---|---|---|
| 1 | `DiseaseRegistryBinder` —— 阶段 2 绑定 + R1 校验接线 | ✅ 已落 |
| 2 | `DiseaseRegistryCookedWriter` —— 确定字节编码 | ✅ 已落 |
| 3 | `DiseaseRegistryBaker` —— 烘焙入口(`BakeFromJson` / `BakeFromRepo`) | ✅ 已落 |
| 4 | **额外**:修共享词法器截断缺陷(见 §四) | ✅ 已落 |

> ⚠️ **本轮不主张「管线端到端已通」。** 见 §六 未实现登记 —— 读方 codec 不存在,
> 产物不落盘,运行期仍走 `CreateFallbackRegistry()`。本轮兑现的是
> **「能烘焙出确定字节,且该过程会红」**。

---

## 二、为什么要建 binder:校验器此前是死代码

`RegistrySchemaValidator.Validate`(17+ 条 R1 校验)在此之前的**唯一调用点是测试**
(`curve_fixture_test` / `registry_bake_test` / `disease_registry_data_test`)。
生产装载路径 `CompositionRoot.CreateFallbackRegistry()` **不调它**。

⇒ 校验器对**真数据面**是死代码。本轮把它接进真数据路径 —— 这是 binder 的主要价值,
不是「顺手写个转换层」。

---

## 三、过程中查出并修掉的两处真缺陷

### ① 白名单与绑定面脱节(两处,方向相反)

`DiseaseRowKeys`(未知键白名单)与 `BindRow`(实际读取)不一致:

| 方向 | 表现 | 后果 |
|---|---|---|
| 白名单有、不绑定 | `zh` / `causes` / `treatable_by` / `handle` / `signs` | **静默丢字段** —— 未知键门看不见(键是合法的) |
| 绑定读、白名单无 | `polarity` / `contagion` / `treatment_difficulty` 等 12 键 | 源文件一声明就被当未知键**拒收** ⇒ 那些读取分支是死代码 |

**修法**:拆出 `BoundRowKeys`(真正绑定的键),`DiseaseRowKeys` = `BoundRowKeys` ∪ 五个已知未绑定键。
`BindResult.UnboundKeys` 把「声明了但没绑定」**显式化**,由测试断言其恰等于
`{causes, handle, signs, treatable_by, zh}`。

> **为什么这条重要**:ADR-014 §三 的未知键门防的是「拼错 ⇒ 静默丢字段」。
> 一个**拼对的、白名单内的、却没人读的**键,丢字段换个方式静默发生 —— 门看不见它。
> 登记表让它变成可断言的量。

### ② 共享词法器接受截断的 JSON(注入测试抓出)

注入 `{ "schema_version": 1, `(**未闭合**)时,阶段 1 词法**接受**了它,
一路走到绑定层报「缺 diseases 数组」。

**根因**:`JsonStage1Lexer.ReadValue` 的对象/数组分支用 `while (reader.Read())` 收元素,
循环退出有**两条**路径 —— `EndObject`(合法)与 `Read()` 返回 false(输入耗尽),
旧代码**不区分**二者。

**后果不是「报错不够早」,是报错指向的地方不是坏掉的地方** —— 作者看到「缺 diseases」
会去补字段,而真正的问题是少了一个 `}`。

**修法**:两处加 `closed` 标志,未闭合即抛。新增 `json_stage1_lexer_truncation_test.cs` 6 条钉住,
含「完整输入不得被误伤」的反例。

> ⚠️ **这是共享件** —— 所有 baker 都走这个 lexer。改动后跑了全量回归(§五)确认无误伤。

---

## 四、证伪轮 —— 证明新测试**会红**

**方法**:备份 → 变异 → 跑测试 → `cp` 回 → sha256 逐字节比对。
**未用** `git checkout` / `git stash`(项目禁令)。

变异前备份 sha256:

```
42bc24d6bc6213bfb9f99a61b968010543f0925403875b26d9464eb727a84a2a  Binder.bak
435312fd26fbbcc5a39f88dad0d7ae132b1cd63bbbb8252d858bfb40eb4ddc9f  Writer.bak
a55b0ef6afdcfda9a26adcb269890f8254f2e04376ccc01ac5bed9539bbf1808  Baker.bak
```

| 变异 | 位置 | 预期转红的测试 | 实测 |
|---|---|---|---|
| M1 删掉「Fix 须为字符串」拒收分支 | `Binder.ReadFix` | `test_fixAsJsonNumber_isRejected` | ✅ RED |
| M2 `BoundRowKeys` 混入 `zh` | `Binder.BoundRowKeys` | `test_realSource_unboundKeySetIsExactlyKnown` | ✅ RED |
| M4 `payloadLength` 写 `+1` | `Writer.FinishHeader` | `test_header_matchesCookedFormat` | ✅ RED |

**实测输出**:`total=20 passed=17 failed=3` —— 三条红,各自对应一处变异,无「一红遮多变异」。

**还原核对**:三文件 sha256 与备份**逐字节相同**(上方三值逐一比对通过)。

> ⚠️ **过程瑕疵(如实记)**:M3(`ConfigVersion` 钉成常量)施加后,
> `Baker.cs` 的备份是**变异后**才做的 —— 即备份被污染。发现后手工逆转该处变异再重备,
> 重备后 `12345u` 零命中。**M3 最终未进入证伪轮**,故其判别力**本轮未证**。
> `test_changedContent_changesConfigVersion` 的判别力仍是**未证状态**,不得借绿。

---

## 五、回归(带三元组)

| 项 | 值 |
|---|---|
| 截至时刻 | `2026-10-22T08:35 UTC` |
| 批次 | 全量 EditMode(还原后终跑) |
| commit | `4e005bd`(工作树含本轮未提交改动) |

```
total=4501  passed=4465  failed=0  inconclusive=0  skipped=36   EXIT=0
```

**新增测试数核对**:`4501 − 4475 = 26`。
本轮新增 = `disease_registry_bake_pipeline_test` 20 条 + `json_stage1_lexer_truncation_test` 6 条 = **26**。
等式成立 ⇒ 零回归,且新增数不是「照抄」而是算出来的。

**基线来源**:`4475/4439/0/36` —— `impl-ac3-injury-data-rows-2026-10-22.md:227`(该轮实测值)。

**中间态(如实记)**:首次全量跑出 `4501/4464/1`,红的是
`cooked_drift_all_artifacts_test.test_sweep_declaredArtifactNames_allTriaged` ——
我声明了 `disease_registry.cooked.bytes` 这个产物名,门要求它被登记。**门在正确工作。**
处置 = 进 `KnownUnbaked` 并写明理由(读方未立,产物此刻无人消费),不是改数字了事。

---

## 六、未实现登记(禁借绿)

| # | 未做 | 影响 |
|---|---|---|
| ① | **读方 codec 不存在**(无 `DiseaseRegistryCookedCodec`) | 产物**无人消费**。写方单方面定义布局 |
| ② | **产物不落盘** | `assets/data/disease_registry.cooked.bytes` 不存在 |
| ③ | **无菜单入口** | `DataBakeMenu` 里没有本件;只能测试 / CLI 驱动 |
| ④ | **运行期不加载** | `CompositionRoot` 仍 `CreateFallbackRegistry()` |
| ⑤ | `handle` 派生逻辑不存在 | GDD R1.3 要求它是派生字段,仓库里无代码算它 |
| ⑥ | `Relapse` / `TreatableBy` / `Signs` / `Handle` 未编码 | 八病种落地时布局须扩展,格式版本 +1 |
| ⑦ | `sigma` 无全局默认 | 数值轮 |

⇒ **能说的**:「真源可烘,字节确定,违例会被拒,且判据被证伪轮钉住」。
⇒ **不能说的**:「管线已通」「运行期已用上烘焙数据」。

---

## 七、复算命令

```bash
cd unity
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testResults "$PWD/Logs/regress-final.xml" -logFile "$PWD/Logs/regress-final.log"
# 预期:total=4501 passed=4465 failed=0 skipped=36
```

新增测试单独跑:

```bash
-testFilter "DaYiJingCheng.Tests.Unit.Sim.DiseaseRegistryBakePipelineTest;DaYiJingCheng.Tests.Unit.Sim.JsonStage1LexerTruncationTest"
# 预期:total=26 passed=26 failed=0
```
