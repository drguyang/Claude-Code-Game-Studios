# 实现证据 —— S26-4 续二:留笔可见 / 旧笔同屏 / 顶头回弹归因(2026-11-08)

**Story**:`production/epics/casebook/story-005-page-layout-focus-navigation.md`
**权威**:`design/gdd/casebook.md` 规则六(改判留痕)· 规则七(焦点序)· `AC-39-12`
**起因**:上一轮遗留四项(用户 2026-11-08 指令「继续修复」)—— 逐项给结论。

---

## 0. 结论表(四项逐条,含「做不到的是什么原因」)

| # | 项 | 结论 |
|---|---|---|
| 1 | 痕的「当前笔与旧笔同屏」 | 🟡 **能做的都做了 + 一个真包缺陷顺带修掉**:42 此前**根本不渲染病名笔迹**(写进去了屏上什么都没有)。本轮补上「当前笔」与「旧笔」两个文本面;真包 `病名 ✓刺伤` 已出图。**换词版同屏**只在 PlayMode 夹具里成立 —— 真包**当场只有一个病种**(见 §3),拿不出第二个词。 |
| 2 | 「灰度化后仍可辨」的像素证据 | ✅ **有了**:真包截图 + 灰度化,对勾仍是清晰形状(§2)。 |
| 3 | 压痕声听测 / 主创签核 | ⚪ **仍未做** —— 前者要人耳、后者要主创,本会话二者皆无(§5)。 |
| 4 | 顶头右推回弹 | ✅ **归因查清并登记 `TD-casebook-02`**:单推即弹,**引擎方向导航在「该方向无候选」时按整体最近回退**;修它就要自算候选 ⇒ 撞 Control Manifest 的 Forbidden(自实现焦点算法)⇒ **不修**,给了解除判据。 |

---

## 1. 一个真包缺陷(本轮查实并修掉):**写进去了,屏上什么都没有**

| 层 | 修前 | 修后 |
|---|---|---|
| 42 渲染 | `channel-disease-name` 行只有静态字「病名」;`PendingConfidence` 只用于分支,**没有任何地方画已落的笔** | 新增 `_nameCurrent`(当前笔)/ `_namePrevious`(旧笔,淡墨档),**惰性创建** |
| 刷新时机 | `ApplyData` 只挂 **tick 边沿**;模态一摊开**世界冻结** ⇒ 落笔后**永不重绘** | 写动作结束后 `RefreshAfterAction()` **当场重读源** |

**真包实测(复现命令见 §6)**:修前「选词落笔」后病名行仍写「病名」;修后落笔当帧即显示 `刺伤`(`31-written.png`)。

> ⚠️ **这是「假绿」的另一面**:事件流里一直有 `JudgmentRecorded`,既有夹具也一直绿,
> 但**屏上从来没显示过**。夹具断的是「写对了」,没人断「看得见」—— 直到真包走查。

---

## 2. 像素证据:灰度化后仍可辨(`AC-39-12` 非色单载)

留痕元件 = **两条边框 + 旋转**画出的对勾(`Label("✓")` 首版被两条既有门各红一次,
见 `impl-s26-4b-…md` §3;那是把它们当文字的错,不是门过严)。

真包链路(单病人):开册 → 查「面色」(⇒ `seen=1`) → 病名栏开册 → 选词(**Recorded / 疑似**)
→ 回病名栏 Submit(**JudgmentRevised ⇒ 有痕**)。读数与图像:

```
开册 | seen=0 pointable=0 词表行=4
查体后 | seen=1 pointable=1 novelty=1
到病名: True channel-disease-name
落笔 焦点= (无焦点)          # 重绘后焦点丢:见 §4 已知面
改判后 焦点= channel-disease-name
```

| 图 | 内容 |
|---|---|
| `31-written.png` | 病名行出现 `刺伤`(当前笔;此前**什么都没有**) |
| `32-revised.png` | 病名行 `病名 ✓刺伤` —— 对勾紧挨词文本(上一版挂在右栏容器上,对勾掉到下一行、压在边框上,已改为**行内**) |
| `zoom-name.png` / `zoom-name-gray.png` | 放大 + **灰度化**:对勾在灰度下仍是清晰形状 ⇒ **非色单载**(`AC-39-12` 的要求) |

⚠️ 灰度化用的是 `PIL.Image.convert("L")`,**只看「形状是否仍可辨」**,不做对比度达标判定
(那是 art-bible / 无障碍轮的事)。

---

## 3. 「当前笔与旧笔**同屏**」:元件会画,真包拿不出第二个词

**已实现**(`ApplyDiseaseNameTexts`):当前笔 = 最新判断的词;**旧笔** = 上一条判断的词,
仅在 `marked && prevId > 0 && prevId != curId` 时出现(同词只改置信度 ⇒ 不画旧笔,否则重影)。

**判据**:PlayMode `test_revised_showsCurrentAndPreviousTextSideBySide` —— 三段:
① 未改判时**只有**当前笔;② 改判后两笔同屏;③ 同词改判时**只画一个**。

**真包为什么给不出第二步**:`[tutorialdiag] 病名册环 … seen=1` —— 场上两个病人
(`#87@(1,0,14)` / `#88@(14,0,1)`)**各查一遍仍是 `seen=1`** ⇒ 同一病种 ⇒ 病名册只有一条词
⇒ 没有「换词」可换。**解除判据 = `seen ≥ 2`**(出现第二个病种,或 9 的病人生成按病种取值)。
归 9 / 内容轮,不归本 story。

⚠️ 因此本项**不记「已通过」**,记:**元件与判据已就位,真包证据缺一个病种**。

---

## 3.5 一条被机械门拦下的命名(记下来,省得下次再撞)

首版把「写后重绘」的方法叫 `RefreshAfterWrite()` ⇒ `NoHealthbarFeedbackTest.test_ac42d2_…`
**当场红**:「42 不得有 Append/Write/Emit 方法(AC-42-D2)」。它按**方法名子串**扫,
不分公有私有、不看意图 —— 命名避让表(`coding-standards`)里写着这一条。
改名 `RefreshAfterAction()` 即绿。**门没冤枉它**:42 里出现 `Write` 字样,下一个人读代码时会以为这里有写路径。

## 4. 已知面(本轮新发现,未修,如实登记)

1. **重绘后焦点丢失** —— `RefreshAfterAction()` 会重建读数格 / 病名文本,焦点归零。
   走查脚本要重新寻焦;玩家手感上是「写完要再按一下方向键」。归手感轮(与 §4 的回弹同族)。
2. **病名册不自动合** —— 保留现状:摊开态是「疑似→确定→擦除」循环的入口
   (`casebook_lexicon_book_test` / `casebook_point_identify_chain_test` 两条判据把这条语义钉死了)。
   本轮试过「指认即合册」,**当场把这两条判据打红** ⇒ 回退,并在代码里留注。

---

## 5. 未做(不得借绿)

| 项 | 为什么 |
|---|---|
| **压痕声听测** | 需要人耳;本会话无音频输出。44 侧的 cue 存在,但「听到的是不是压痕」只能人判。 |
| **主创签核** | `AC-39-12` 是 `[L]` 人判项,签核人 = 主创(用户)。 |
| **换词同屏的真包图像** | 见 §3:全场单病种。 |
| **顶头回弹** | 见 §0 #4 与 `TD-casebook-02`:纪律上不许自实现焦点算法。 |

---

## 6. 复现

```bash
# ① 像素证据(单病人:落笔 → 改判 → 对勾)
python3 <job>/tmp/probe_rewrite8.py     # 起真包、走完链路、截图到 /tmp/rewrite8
python3 -I - <<'PY'
from PIL import Image
im = Image.open("/tmp/rewrite8/32-revised.png")
im.crop((1090,130,1280,215)).resize((760,340), Image.LANCZOS).save("/tmp/zoom.png")
im.convert("L").crop((1090,130,1280,215)).resize((760,340), Image.LANCZOS).save("/tmp/zoom-gray.png")
PY

# ② 回弹测量
python3 <job>/tmp/probe_pingpong.py     # 打印「病名上单推右 ×3」的焦点序列
```

---

## 7. 回归三元组

| 套件 | 计数 | 截至时刻 | 批次 | commit |
|---|---|---|---|---|
| EditMode | `7168 / 7131 / 0 / 0 / 37` | `2026-11-08T06:10:14Z` | sprint-26 S26-4 续二 | `b84f102d` + 工作树 |
| PlayMode | `370 / 366 / 0 / 2 / 2` | 同上 | 同上 | 同上 |

**基线**(上一批 `b84f102d`)`7166 / 7129` · PlayMode `369 / 365`。
**等式**:`7168 − 7166 = 2`(新增 `TryGetRewriteMarks` 两条 query 测试)·
`370 − 369 = 1`(新增同屏三段式)。零失败。

```bash
grep -c "\[Test\]" unity/Assets/Tests/EditMode/CasebookGuards/casebook_rewrite_mark_test.cs       # ⇒ 11
grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/casebook_rewrite_mark_test.cs  # ⇒ 4
```
