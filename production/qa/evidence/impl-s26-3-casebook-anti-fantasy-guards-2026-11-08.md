# 实现证据 —— S26-3 `casebook-004` 反幻想三条「绝不」的能力面守卫

**日期**: 2026-11-08
**Story**: `production/epics/casebook/story-004-anti-fantasy-capability-guards.md`
**权威**: `design/gdd/casebook.md` 规则四 · **AC-39-05** / **AC-39-06a** / **AC-39-08** ·
AC-39-09(`[L]`,本件只交脚本)· AC-39-01(ADVISORY,`AC-37-15` 的真子集)
**落点**: `unity/Assets/Editor.Tools.Gates/CasebookAntiFantasyGate.cs`
+ `unity/Assets/Tests/EditMode/CasebookGuards/casebook_anti_fantasy_guard_test.cs`

---

## 0. 结论

| 项 | 结果 |
|---|---|
| 39 公开面零病种维度 / 零聚合催促 API(AC-39-05 / AC-39-08) | ✅ 13 条判据全绿 |
| 39 的 UXML 文本面零反幻想词族(AC-39-06a 的 `text=` 半边) | ✅ |
| **AC-39-06a 的「本地化键闭集」半边** | 🔴 **`NOT-RUN`** —— **闭集本身不存在**(见 §5) |
| AC-39-01 类型面(39 的呈现 DTO 过 `PresentationDtoGuard`) | ✅ 4 个根类型 + 负控 |
| 证伪轮 M1–M11 | ✅ 各自转红,SHA-256 逐字节还原 |
| **构建期探针轮** | ✅ `Result: Failure`,探针串在日志,`steps` 止于 `Preprocess Player` |
| AC-39-09 `[L]` 走查 | 🔴 **`NOT-RUN`** —— 脚本已交,执行与签核归 QA 轮 |

---

## 1. 判据(13 条)

| 组 | 覆盖 |
|---|---|
| 正控(真 39 面) | `RunAll()` 全绿 · 扫描面非空且确为 39 这一支 |
| AC-39-05 名面 | `CountByDisease` ⇒ 红 |
| AC-39-05 **返回类型面** | `Get()` 返回 `FakeDiseaseSnapshot` ⇒ 红(**改名旁路**堵的是这里) |
| AC-39-08 聚合面 | `CountUnopened` ⇒ 红 |
| 词表**不过宽** | `Count` / `Group` / `SortKey` 三个合法用法 ⇒ 绿 |
| F4 守卫 | `ScanPublicSurface(空集)` ⇒ 红(不许空转) |
| AC-39-01 类型面 | `ProjectedVolume` / `VolumePage` / `JudgmentTrace` / `CaseId` 过 DtoGuard ⇒ 空;`diseaseId` 合成件 ⇒ 红 |
| AC-39-06a 文本面 | 真 `Casebook39.uxml` 零命中(先断抽到的 `text=` 非空)· 「同源」「未结」合成段 ⇒ 红 |
| F4 守卫 | `ScanTexts(空集)` ⇒ 红 · 缺 UXML 文件 ⇒ **报错不静默** |

## 2. 词表为什么**不含** `sort` / `group` / `count`

`CaseSortKey`(按 patient/tick/seq,不按病种)· `CaseGroup`(同一**病人**的串接组,GDD 规则三明文允许)·
`CaseSheet.Count`(册内例数)都是**合法**用法。把这三个词一律拉黑会造出一条**永远红**的门 ——
那不是严谨,是没人会去修的噪声。M10 反向证明了这条取舍有判别力:把 `count` 塞进病种词表 ⇒ 3 条转红。

## 3. 证伪轮(11 条 · 一属性一条)

| # | 变异 | 期望 | 实测 |
|---|---|---|---|
| **M1** | 病种词表清空 | 名面 + 返回类型面 | ✅ **2 红** |
| **M2** | 只掐**返回类型**那一面 | 改名旁路那条 | ✅ **恰 1 红** |
| **M3** | 聚合词表清空 | `CountUnopened` | ✅ 恰 1 红 |
| **M4** | 空扫描面守卫失效 | `test_emptyScanSurface_isRed` | ✅ 恰 1 红 |
| **M5** | 文本黑名单清空 | 合成反幻想词 | ✅ 恰 1 红 |
| **M6** | 空文本面守卫失效 | `test_ac39_06a_emptyTextSurface_isRed` | ✅ 恰 1 红 |
| **M7** | 缺 UXML 静默通过 | 「缺资产不得静默」 | ✅ 恰 1 红 |
| **M8** | UXML 抽文本恒空 | 真 UXML 的「抽到了东西」前置 | ✅ 恰 1 红 |
| **M9** | 从 `AssemblyGates.RunAll` 摘掉接线 | 门链双向差集两条 | ✅ **2 红** |
| **M10** | 词表**过宽**(塞 `count`) | 合法用法被误判那条 | ✅ **3 红**(见 §2) |
| **M11** | 给 `VolumePage` 加 `diseaseId` 属性 | 类型面登记真在扫 | ✅ **2 红**(两门各一,见下) |

**M11 为什么是 2 红而不是 1 红**:同一个违例被**两个独立判据**各自抓住 ——
`PresentationDtoGuard`(类型面)与 `CasebookAntiFantasyGate`(名面)。这不是串扰,是纵深。

还原 = 备份 + `cp` 回 + **SHA-256 逐字节**:
`CasebookAntiFantasyGate.cs` `950fb3d7…` · `AssemblyGates.cs` `a5bd052d…` ·
`CasebookVolumeProjection.cs` `4a09cf17…`。`grep -rn "MUTANT\|FALSIFY" unity/Assets` = **0 命中**。

## 4. 构建期探针轮(CLAUDE.md §构建前门)

「构建绿 ≠ 门跑了」。本轮插一条**无条件** `errs.Add("[PROBE-S26-3] …")` 进 `CasebookAntiFantasyGate.RunAll`,
重跑 `CostTableProbeBuildCli.Build`:

| # | 要素 | 实测 |
|---|---|---|
| 1 | 探针串出现在构建日志 | ✅ 日志第 1228 行 `[PROBE-S26-3] 无条件失败项 …` |
| 2 | 栈帧 | ✅ `AssemblyGates/BuildGate:OnPreprocessBuild` @ `AssemblyGates.cs:1593` |
| 3 | 构建**因它**失败 | ✅ `BuildFailedException` · `Build Finished, Result: Failure.` · utp `steps` 只有 `Preprocess Player` |

⚠️ **栈帧给的是 `Debug.LogError` 那一行(`:1593`),不是 `CasebookAntiFantasyGate.RunAll()` 的接线行**。
探针**确实**由该门的 `RunAll` 返回(探针串就只在它里面),这一半是**观测**;
「接线行被执行」这一半仍属**论证**(错误列表被延迟到 `:1593` 才统一打出来)。
与 CLAUDE.md 该节的口径一致,分两半写。

同批把 `BuildFailedException` 报文里的门名列补上 `39 反幻想门`(`AssemblyGates.cs:1597`)。
⚠️ **该报文是散文枚举,不是计算出来的清单**,补之前就已经漏了 17 的两个门(`:1588` / `:1589`)——
本轮**只补自己这一支**,17 的漏登如实留在这里,不顺手改别人的面。

## 5. 未做 / 缺口(不得借绿)

1. **AC-39-06a 的「本地化键闭集」半边 = `NOT-RUN`。**
   AC 原文的对象是 `casebook_text_keys` 闭集,而全仓 `assets/data/` 无该数据集,
   `grep -rln "casebook_text_keys\|casebook_text" --include=*.json --include=*.yaml assets/ unity/ docs/` **零命中**。
   ⇒ 那半**没有宾语**,不记绿。已交的是 AC 自带的替代载体(2026-09-19 注:「字符串扫描测不到 UXML 元素
   ⇒ 本地化 key 闭集 + USS `text=` 黑名单」),即 `text=` 黑名单 + 可复用的 `ScanTexts` 原语。
   **闭集落地时**把它接进 `ScanTexts` 即可,门不用改。
2. **AC-39-09 `[L]` 走查 = `NOT-RUN`。** 脚本见
   `production/qa/evidence/casebook/story-004-l-walkthrough-scripts.md`;**脚本存在 ≠ 走查已跑**。
3. **扫描面边界**:只扫 `DaYiJingCheng.Gameplay.UI.Casebook` 命名空间 + `Casebook39.uxml`。
   42 元件库全域、8 的词表**不在此面内**(各归各的 AC)。
4. **AC-39-01 是 ADVISORY**(2026-09-19 降级,`AC-37-15` 的真子集)。本件补的是**登记义务**那一半
   (39 消费的呈现 DTO 进守卫扫描面),不宣称它是 BLOCKING 面。

## 6. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7134 / 7097 / 0 / 0 / 37` | `2026-11-08T00:11:51Z` | sprint-26 S26-3 | `bade1ce6` + 工作树 |
| PlayMode | `365 / 361 / 0 / 2 / 2` | `2026-11-08T00:13:02Z` | 同上 | 同上 |

**基线**(S26-2 收口后)`7121 / 7084` ⇒ **+13 / +13,零回归**。
**等式判据**:`7134 − 7121 = 13` = 本夹具的 `[Test]` 计数。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/CasebookGuards/casebook_anti_fantasy_guard_test.cs   # ⇒ 13
```

## 7. 一处流程事实(如实记)

首轮定向跑 11 条里 **1 红** —— 不是门错了,是**我的断言**写窄了:我断言报文里出现类型名
`FakeDiseaseSnapshot`,而门的契约是**点名命中的词面**(`diseas`)+ 面名(「返回类型」)。
处置 = **改断言去贴合实际契约,不是改门去贴合我的措辞**;
若反过来把类型名塞进报文,等于让报文迁就一个测试,而不是让测试描述一个契约。
