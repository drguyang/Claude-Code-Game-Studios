# T22 批三 —— 脉案收口(`casebook-005`)证据原件

**日期**:2026-11-10
**批次**:T22 批三(用户「开工 T22 批三」;先裁定「丙 · 补词表映射」→「丁 · 新增两行正确病名」)
**构建**:`PlayerBuildCli.BuildDevelopment` → `Build Finished, Result: Success`(19.7 s ·
206 769 666 B · 播放器数据目录 `unity/builds/Linux-x64/大医精诚_Data/` mtime `23:10:25`)
**运行**:`DISPLAY=:0`,1280×800,`-tutorialdiag`;存档根**走查前移开、走查后原样还原** ——
本轮存档留在 `saves.t22c-run1/`(2 病人 / 2 立案 / 1 改判),原 `saves/` 已回位
**驱动**:键盘 = X11 XTEST(`tools/input/xkeys.py`);合册 = uinput 虚拟手柄(`tools/input/vpad.py`,**真 evdev 路径**)

---

## 1. 本批做了什么(三件,一件一个判据)

| # | 件 | 判据 |
|---|---|---|
| ① | **词表补两行**(`割伤 → 病种 3` · `疟疾 → 病种 7`) | 真包 `病名册环 pointable ≥ 2`(`TD-casebook-03` 的解除判据) |
| ② | **新增机械门** `lexicon_draw_coverage_test`(把「词表覆盖」与「开局投哪两例」连起来) | 证伪 M1/M2/M3 各转红 |
| ③ | **改判走查**(旧笔同屏 + 铅笔勾) | 屏 + 落盘字节**两条独立腿** |

⚠️ **本批不碰**「纸的物理感」那一半 —— `AC-39-12` 的 `[L]` 主创签核**仍是 `NOT-RUN`**。

---

## 2. 数据与烘焙

```json
{ "lexicon_id": 4, "text": "刺伤", "disease_ids": [2] },
{ "lexicon_id": 5, "text": "割伤", "disease_ids": [3] },   ← 本批新增
{ "lexicon_id": 6, "text": "疟疾", "disease_ids": [7] }    ← 本批新增
```

| 件 | 前 | 后 |
|---|---|---|
| `case_judgment_lexicon.cooked.bytes` | 92 B / 4 行 / `ConfigVersion = 0x7819DE00` | **130 B / 6 行 / `0xB0BD7E16`** |

复算(两条都可当场跑):

```bash
cd unity && Unity -batchmode -quit -projectPath "$PWD" \
  -executeMethod DaYiJingCheng.EditorTools.Bake.DataBakeMenu.BakeJudgmentLexicon \
  -logFile Logs/bake.log
grep -a "烘焙完成" Logs/bake.log     # 期望:130 B, 6 行
```

**两条孤儿行(`2 湿温` / `3 温毒`)刻意保留且继续置空** —— `AC-37-35` 的「双射候选拒」
(`test_ac3735_bijectiveCandidate_rejected`)依赖孤儿条目存在。这是用户裁定时点名的约束。

外键有效性由**既有门**兜:`LexiconRegistryCrossRefGate` 的 C1 要求每行 `disease_ids` 逐值
∈ 注册表 id 集(3 = `INJ_LACERATION`、7 = `DIS_MALARIA`,均在册)。

---

## 3. 新增机械门 —— 为什么必须有一条「把两件事连起来」的判据

`TD-casebook-03` 的形态不是某一侧写错,而是**两侧各自都绿**:

- 词表侧:烘焙绿、外键门绿、roundtrip 绿;
- 投放侧:开局面快照绿、资格判据绿;
- 真包:**`seen=2` 而 `pointable=1`**。

⇒ 缺的正是「**词表覆盖 vs 开局实际投哪些病**」这条连线。

`unity/Assets/Tests/EditMode/CaseSystem/lexicon_draw_coverage_test.cs`(**2 条**):

| 判据 | 内容 |
|---|---|
| C1 | 按**生产同款**判据算开局前两例:`TechniqueUnlockBootstrap.StartingTechniques` + `PatientDrawEligibility.Decide` + `registry.Rows` 行序(即 `PatientAppearedDriver._rotation` 的同源);要求这两例**都能**在词表里指认到词,并**钉死**当下值 `{4, 5}` |
| C2 | 覆盖数 **≥ 2**(`TD-casebook-03` 的解除判据的机械形态) |
| G1/G2 | 注册表 0 行 / 词表 0 行 ⇒ 红(**空洞正控**) |
| 负控 | 空集 ⇒ 0 条;只喂病种 2 ⇒ 恰 `{4}` —— 证明上一条的「2」不是常数 |

⚠️ 本门**不替代** `LexiconRegistryCrossRefGate`(词表自身一致性)与 `cooked_drift`(源 ↔ 产物);
三者互补。本门读的是**烘焙产物**(与运行期同一 codec),故**源改了忘重烘**时它看的是旧产物 ——
那由 `cooked_drift_all_artifacts_test` 兜(**两门必须同时在链上**,单靠任一条都会留缝)。

同批还把两行新数据**钉进源数据夹具** `judgment_lexicon_bake_test.test_bake_roundtrip_threeRows_withOrphan`
(与既有「刺伤」同款:行被删 / 外键被改 ⇒ 立刻红)。

---

## 4. 真包走查 —— `pointable` 0 → 1 → 2

`unity/Logs/t22c-run1.log`(2971 行,`grep -ac "Exception:"` = **0**):

```text
[tutorialdiag] 病名册环 t=5.80 viewer=0 seen=0 pointable=0 词表行=6 novelty=0
[tutorialdiag] 病人出现 #0 id=1 disease=2 注册表=17 hook=0
[tutorialdiag] 病人出现 #1 id=2 disease=3 注册表=17 hook=0
[tutorialdiag] 查体投影 patient=1 actor=0 disease=2 novelty=First tick=1430
[tutorialdiag] 病名册环 t=163.94 viewer=0 seen=1 pointable=1 词表行=6 novelty=1
[tutorialdiag] 查体投影 patient=2 actor=0 disease=3 novelty=First tick=2410
[tutorialdiag] 病名册环 t=266.52 viewer=0 seen=2 pointable=2 词表行=6 novelty=2
```

三件事同时成立:
1. **`词表行=6`** ⇒ 真包里跑的是本批烘的产物(不是旧档);
2. `病人出现 #0 disease=2 / #1 disease=3` ⇒ 开局两例正是词表新覆盖的两个病种;
3. `pointable` **0 → 1 → 2** ⇒ 每查一例多一条可指认词,**第二例不再无词可指**。

> ⚠️ 开工前我按 `draw_surface_audit` 的表手推过「开局投 9 / 14」,**推错了** ——
> 实跑读数是 2 / 3。记在这里,是因为**这类手推**正是本批要消灭的东西:注册表行序、
> 资格门、起步技法三者一交叉,人肉推导必错一次;机械门(C1)与真包读数才是判据。

---

## 5. 改判(旧笔同屏 + 铅笔勾)—— 两条独立腿

### 5.1 屏上(`AC-39-12` 的观察面)

走查序列(全程键盘 d-pad 等价键):`E` 开例 2 → `Right` 进右栏 → `Return` 摊开病名册 →
`Return` 指认「刺伤」→ `Down` → 退到折边 → `Down` 进册 → `Down` 到「割伤」→ `Return` 改判。

| 观测 | 值 |
|---|---|
| 病名行 | `✓ 割伤`(对勾在词**左侧、行内**) |
| 旧笔 | 紧随其下另一行 `刺伤` |
| 当前笔墨色 vs 旧笔 | 最暗像素 **21 vs 142**(与「面色」等通道正文同族的 21 = 当前笔) |
| 灰度下 | ✓ 仍可辨(**形状承载**,非色单载);两笔墨色差 21 / 142 仍可分 |

像素读数复算(截图 `unity/Logs/shots/t22c/19-rewrite-ink.png`,**本地,按 2026-11-06 指令不入库**):

```bash
python3 - <<'EOF'
from PIL import Image
im = Image.open('unity/Logs/shots/t22c/19-rewrite-ink.png').convert('L')
print('current 割伤', min(im.crop((1460,336,1520,360)).getdata()))   # 期望 21
print('old     刺伤', min(im.crop((1400,376,1460,400)).getdata()))   # 期望 142
print('channel 面色', min(im.crop((320,344,380,368)).getdata()))     # 期望 21
EOF
```

### 5.2 落盘字节(`AC-39-12` 的数据腿)

走查里在暂停壳按「保存」→ 退出(`save_exit.bin`),再用 `SaveCellDumpCli` 离线读档:

```text
JudgmentRecorded: patient 2 · case (2410,2,0) · lexicon=4 confidence=1 · tick 2410
JudgmentRevised:  patient 2 · case (2410,2,0) · lexicon=5 confidence=1 · tick 2410
JudgmentRecorded 1 条 · JudgmentRevised 1 条(改判:屏上应出铅笔勾 + 旧笔淡墨)
CaseOpened 2 条 · CaseClosed 0 条 · CaseReferred 0 条
```

`lexicon=4 → 5` 就是「刺伤 → 割伤」。**屏上的两笔与 ✓ 由这条事件推出来**
(`IsCurrentJudgmentRewritten` 读 `JudgmentRevised`),故两腿互为对照:一条说「画对了」,
一条说「真有东西可画」。

> 🔁 **本批顺手补的走查工具**:`SaveCellDumpCli` 此前**不打** `JudgmentRecorded` /
> `JudgmentRevised` ⇒ 改判在数据腿上是空白的。现补齐(Editor-only,不进构建)。

---

## 6. 证伪轮(2026-11-10 · 还原 = SHA-256 逐字节)

| 变异 | 改法 | 结果 |
|---|---|---|
| **M1** | **源数据**:删掉词表第 5 / 6 行并**重烘** | **2 红**:`test_firstTwoDrawablePatients_eachHaveALexiconWord`(`pointable` = **1** —— 与原始缺陷同码)· `test_bake_roundtrip_threeRows_withOrphan` |
| **M2** | `LexiconBookSource.PointableLexiconIds` 忽略已见集(全行入册) | **2 红**:两条新判据(6 条 vs 期望 2 条 / 1 条) |
| **M3** | `PatientDrawEligibility.Decide` 一律拒收病种 3 | **1 红**:`test_firstTwoDrawablePatients_eachHaveALexiconWord`(`pointable` = 1) |

**M1 是本轮最有价值的一条**:它不是「改坏一行断言」,是**重演原始缺陷**
(4 行词表 + 真产物)⇒ 本门**会**在当时那版数据上转红。M3 则证明本门吃的是
**生产判据**,不是夹具里抄死的 `{2, 3}`(若抄死,M3 不会红)。

还原记录(逐条 `sha256sum -c` 全部 `成功`):

| 文件 | 变异前 SHA-256 |
|---|---|
| `assets/data/case_judgment_lexicon.json` | `c965dc4b33cb7a53…` |
| `unity/Assets/DataCooked/case_judgment_lexicon.cooked.bytes` | `f89ebbe609d91898…` |
| `unity/Assets/Gameplay.Boot/LexiconBookSource.cs` | `11dedcf04ac123c7…` |
| `unity/Assets/Gameplay.Boot/PatientDrawEligibility.cs` | `23466c36cab228bf…` |

`grep -rn "FALSIFY" unity/Assets --include=*.cs` ⇒ **零命中**(无残留)。

---

## 7. 回归(三元组)

| 套件 | 结果 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7411 / 7374 / 0 / 0 / 37` | `2026-11-10T15:40:13Z`–`15:42:17Z` | T22 批三 | `c4639852` + 工作树 |
| PlayMode | `376 / 371 / 0 / 2 / 3` | `2026-11-10T15:42:59Z`–`15:43:21Z` | T22 批三 | `c4639852` + 工作树 |

基线(T22 批二):EditMode `7409 / 7372 / 0 / 0 / 37` · PlayMode `376 / 371 / 0 / 2 / 3`。
**等式**:`7411 − 7409 = 2` = 本批新增夹具 `lexicon_draw_coverage_test` 的 `[Test]` 计数;
PlayMode **零变化**(本批零 PlayMode 改动)。

---

## 8. 本批**没有**做的(不得借绿)

| 项 | 状态 | 理由 |
|---|---|---|
| `AC-39-12` 的「纸的物理感」半边(翻页手感 / 压痕 / 墨迹显现节奏) | **`NOT-RUN`** | 需**主创签核**,本批只跑了「同屏 + 勾」这一半 |
| 压痕声(聚焦空行的音频反馈) | **`NOT-RUN`** | 未听测(判据对象归 story-006) |
| 六员页的其余五员(箱 / 暂停 / 设置 / 教学 / 近景)走查 | **未跑** | 沿用 2026-11-08 的范围登记,本批未扩 |
| `AC-20-10` / `AC-20-20` 等批二遗留 | 不变 | 与本批无关 |

**新发现(已登记 `TD-casebook-04`,本批不改)**:改判到**另一个词**的路径存在但埋了坑 ——
① 落笔后焦点掉成「无焦点」(`RefreshLexiconBook` 重建 + 按设计**不抢焦点**);
② 病名栏与册条目之间隔着**折边**,而折边的 `Submit` 是 `SubmitFold`(右页折回)——
   本批读的是代码(`CasebookScreen.cs:220` 的 `BindSubmitAndClick(foldEdge, SubmitFold)`),
   本轮的走查**没有**去踩它(是绕过去的);「按 `Down` 一次就落到折边上」这条**误入**在
   2026-11-06 开册落焦的注释里已有真包实测记录(`CasebookScreen.cs:481-483`)。
⇒ 功能可达(本批就是这么走通的),但**不是一条干净的手柄路径**。

---

## 9. 证据文件索引(本地,不入库)

| 文件 | 内容 |
|---|---|
| `unity/Logs/t22c-run1.log` | 走查全程(2971 行,零异常) |
| `unity/Logs/t22c-celldump.log` | 落盘字节读数(改判 / 立案) |
| `unity/Logs/t22c-M{1,2,3}.xml` | 三条变异的测试结果 |
| `unity/Logs/shots/t22c/19-rewrite-ink.png` / `20-…-gray.png` | 改判同屏(彩 / 灰度) |
| `unity/Logs/shots/t22c/25-rewrite-crop.png` / `26-…-crop-gray.png` | 病名行裁切放大(彩 / 灰度) |
