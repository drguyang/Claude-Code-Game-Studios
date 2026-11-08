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
| **AC-39-06a 的「本地化键闭集」半边** | ✅ **2026-11-08 已补齐**(`casebook_text.json` 13 键 + UXML 必须 ∈ 闭集值集,见 §8) |
| AC-39-01 类型面(39 的呈现 DTO 过 `PresentationDtoGuard`) | ✅ 4 个根类型 + 负控 |
| 证伪轮 M1–M17 | ✅ 各自转红,SHA-256 逐字节还原 |
| **构建期探针轮** | ✅ `Result: Failure`,探针串在日志,`steps` 止于 `Preprocess Player` |
| AC-39-09 `[L]` 走查 | ✅ **2026-11-08 真包执行一轮**(键鼠);手柄 / 两例翻页 / 听测仍 `NOT-RUN`,**主创签核未做** |

---

## 1. 判据(23 条)

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
| AC-39-06a 闭集 | 真数据集零违例 · **UXML 全部 `text=` ∈ 闭集值集** · 闭集外字面量 ⇒ 红 |
| 闭集自检 | 坏 JSON / 缺 `keys` ⇒ 红(不静默退化) · 键形 · 重复键 · 空文案 · 空闭集 ⇒ 各 1 红 |

## 2. 词表为什么**不含** `sort` / `group` / `count`

`CaseSortKey`(按 patient/tick/seq,不按病种)· `CaseGroup`(同一**病人**的串接组,GDD 规则三明文允许)·
`CaseSheet.Count`(册内例数)都是**合法**用法。把这三个词一律拉黑会造出一条**永远红**的门 ——
那不是严谨,是没人会去修的噪声。M10 反向证明了这条取舍有判别力:把 `count` 塞进病种词表 ⇒ 3 条转红。

## 3. 证伪轮(17 条 · 一属性一条)

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
| **M12** | 去掉「UXML 文案 ∈ 闭集」判据 | 闭集牙齿 | ✅ 恰 1 红 |
| **M13** | 坏 JSON 静默吞掉 | 「坏闭集不得退化成空集」 | ✅ 恰 1 红 |
| **M14** | 去掉键形判据 | 键形 | ✅ 恰 1 红 |
| **M15** | 去掉重复键判据 | 重复键 = 两条真源 | ✅ 恰 1 红 |
| **M16** | 去掉空闭集守卫 | F4 | ✅ 恰 1 红 |
| **M17** | 往 `Casebook39.uxml` 插一句闭集外字面量 | **构建期门也读资产** | ✅ **2 红**(含走 `RunAll` 的那条 ⇒ 构建期强制点成立) |

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

1. **AC-39-06a 的「本地化键闭集」半边 —— ✅ 已补齐(§8)。**
2. **AC-39-09 `[L]` 走查 —— ✅ 键鼠路径已真包执行**(见
   `production/qa/evidence/casebook/ac39-09-walkthrough-result-2026-11-08.md`)。
   仍 `NOT-RUN`:手柄路径 · 两例翻页对照 · 听测零 jingle;**主创签核未做**(不代签)。
3. **扫描面边界**:只扫 `DaYiJingCheng.Gameplay.UI.Casebook` 命名空间 + `Casebook39.uxml` + `casebook_text.json`。
   42 元件库全域、8 的词表**不在此面内**(各归各的 AC)。
4. **AC-39-01 是 ADVISORY**(2026-09-19 降级,`AC-37-15` 的真子集)。本件补的是**登记义务**那一半
   (39 消费的呈现 DTO 进守卫扫描面),不宣称它是 BLOCKING 面。

## 6. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7145 / 7108 / 0 / 0 / 37` | `2026-11-08T01:32:07Z` | sprint-26 S26-3(续) | `39c51ab1` + 工作树 |
| PlayMode | `365 / 361 / 0 / 2 / 2` | `2026-11-08T01:33:14Z` | 同上 | 同上 |

**基线**(本 story 上一轮,即 `39c51ab1`)`7134 / 7097` ⇒ **+11 / +11,零回归**。
**等式判据**:`7145 − 7134 = 11` = 本夹具 **+10**(13 → 23)+ `assembly_gate_runall_test` **+1**。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/CasebookGuards/casebook_anti_fantasy_guard_test.cs   # ⇒ 23
grep -c "\[Test\]" unity/Assets/Tests/EditMode/Sim/assembly_gate_runall_test.cs                      # ⇒ 3
```

## 7. 一处流程事实(如实记)

首轮定向跑 11 条里 **1 红** —— 不是门错了,是**我的断言**写窄了:我断言报文里出现类型名
`FakeDiseaseSnapshot`,而门的契约是**点名命中的词面**(`diseas`)+ 面名(「返回类型」)。
处置 = **改断言去贴合实际契约,不是改门去贴合我的措辞**;
若反过来把类型名塞进报文,等于让报文迁就一个测试,而不是让测试描述一个契约。

## 8. 2026-11-08 续轮:三条 `NOT-RUN` 收口

### 8.1 本地化键闭集(AC-39-06a 的另一半)

| 件 | 落点 |
|---|---|
| 闭集数据 | `assets/data/casebook_text.json`(13 键,`cb` 前缀 + 驼峰) |
| 词法 | `ParseTextKeys`(Newtonsoft **仅作词法器**,ADR-014 §三;绑定层自写) |
| 闭集自检 | `ScanTextKeySet` —— 键形 / 重复 / 空文案 / 黑名单词族 / 病种词族 / **空集即红** |
| **闭合** | `ScanUxmlAgainstClosedSet` —— UXML 每个非空 `text=` **必须 ∈ 闭集值集** |
| 构建期 | `ScanAuthoringSurface` 接进 `RunAll` —— 此前这两个资产**只有 EditMode 读**,真构建里没有执行体 |

⚠️ **键名避让记一笔**:首个键写作 `cbDiseaseName`(「病名栏」),被本门按「病种维度词」判红。
文案「病名」是玩家落笔的标签,语义正当 —— 但机械门分不出「能力面泄漏」与「一个标签」。
处置 = **改键名为 `cbNameBar`**,并把理由写进该文件的 `keyNamingNote`(与
`.claude/docs/coding-standards.md` §命名 — 机械门避让表的 `rip` / `clip` 同型)。

### 8.2 AC-39-09 `[L]` 走查执行

结果与截图另立件:`production/qa/evidence/casebook/ac39-09-walkthrough-result-2026-11-08.md`。
最强的一条证据 = **焦点停在病名栏 5 秒,前后两帧 SHA-256 完全相同**(零像素变化)。
仍 `NOT-RUN`:手柄路径 · 两例翻页对照 · 听测;**主创签核未做**。

### 8.3 构建报文门名:散文枚举 → 派生

原报文是一串**手写门名清单**,实测已漂(补 39 之前就漏了 17 的两个门)。改成派生:

- `AssemblyGates.RunAll(out List<string> failingGroups)`;无参重载 = `RunAll(out _)`,两条重载由
  `assembly_gate_runall_test.test_assemblyGates_runAll_overloadsAgree` 钉住同源。
- `BuildGate` 用局部 `Add(label, items)` / `Mark(label, before)`,**标签与调用点同处一行**。
- ⚠️ 第一版把 `CheckXxx(errs)` 改成了方法组引用 `Step("…", CheckXxx)`,**门链解析器当场转红**
  (`entryWiring_matchesManifest_bidirectionally` + `extractor_actuallyExtractsKnownMembers`)——
  它按 `名字(` 抽接线,方法组没有括号。改回**直调 + `Mark` 记账**才既保住可见性又保住归属。
  这一红是守卫该红:**它证明「门链看得见谁」这件事本身被机械守着**。
- **探针轮**:给 39 门插无条件失败项 ⇒ `Build Finished, Result: Failure`,
  报文 = `失败门组:39 反幻想`(不再是那串固定清单)。
