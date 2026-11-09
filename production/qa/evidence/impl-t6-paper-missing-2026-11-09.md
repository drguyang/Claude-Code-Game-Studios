# T6 手册缺页元件 —— 实施与证据(2026-11-09)

> **范围**:`technique-unlock-map-2026-11-09.md` §7 T6 —— 钩子病例的**呈现载体**(「看得出是哪一招,但这一页没了」)。
> **上游**:T5a / T5b(`20d3e2bd` / `cef0bdcc`)。
> **本件交付「元件 + 状态」,不交付「哪一页缺」的数据绑定** —— 见 §6。

---

## 1. 改动清单

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/PaperCloseupScreen.cs` | 改 | 缺页态:`SetMissingPaperIds` / `IsCurrentPageMissing`;缺页页**只留标题**、正文不摆、纸面挂 `paper-missing`、撕口显出来 |
| 2 | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` | 改 | `.paper-missing` / `.paper-missing-mark`(零新贴图) |
| 3 | `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss` | 改 | `--skeuo-paper-missing-mark-height`(撕口线宽) |
| 4 | `unity/Assets/Tests/EditMode/Tutorial/paper_closeup48_screen_test.cs` | 改 | **+5 条**机械判据 |
| 5 | `unity/Assets/Tests/PlayMode/SkeuomorphicUI/paper_missing_walkthrough.cs` | **新** | 视觉走查(batch 下 `Assert.Ignore`) |

---

## 2. 判据(EditMode,5 条)

| # | 判据 | 反证 |
|---|---|---|
| 1 | 缺页页**只剩标题**,正文空 | Render 里把 `missing ? "" : body` 改回恒取正文 ⇒ 红 |
| 2 | 缺页态挂类 + 撕口显出来;换到正文页两者都撤掉 | 删 `EnableInClassList` 那行 ⇒ 红 |
| 3 | 未声明缺页 ⇒ 全部照常(既有行为零变化) | 缺页判定写成 `_missing != null` 恒真 ⇒ 红 |
| 4 | `null` 等价于「无缺页」(不是「全部缺页」) | 方向写反 ⇒ 整本手册全白 |
| 5 | 缺页判定按**纸条目 id**,不按页序 | 改成按下标 ⇒ 换序后判错 |

**为什么按 id 不按页序**:页序随内容表增删漂移,id 是 6 侧布置引用的稳定键。

---

## 3. 回归

**三元组**:`2026-11-09T11:42:50Z`(EditMode 结束)/ `11:44:30Z`(PlayMode 结束)·
**全量** · `cef0bdcc` + 工作树。

| 套件 | 本次 | 对照基线(T5b 后) | 差 |
|---|---|---|---|
| EditMode(全量) | **`7238 / 7201 / 0 / 37`** | `7233 / 7196 / 0 / 37` | **+5 / +5** |
| PlayMode(全量) | **`371 / 366 / 0 / 2 / 3`** | `370 / 366 / 0 / 2 / 2` | 总数 +1 · 跳过 +1 · **通过数不变** |

**等式核对**:EditMode `7238 − 7233 = 5` = 新夹具条数 **5**。✔
PlayMode 总数 `371 − 370 = 1` = 新增的视觉走查条数 **1**,它在 batch 下 `Assert.Ignore`
⇒ **计入 skipped 而不是 passed** —— 这正是「不得据 PlayMode 绿声称截过图」的机制保证。

**一轮红(如实登记,而且是门在起作用)**:首轮 EditMode 1 红 ——
`validate_all_aggregate_test` 报 `SkeuoPaper.uss:327 发现硬编码尺寸「height: 2px;」`
(`SkeuoUssPolicy` 的 C4 判据:尺寸须走主题变量)。已改为
`var(--skeuo-paper-missing-mark-height)`,并在 `SkeuoThemeVariables.uss` 立该变量。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import xml.etree.ElementTree as ET
for p in ("unity/Logs/editmode-t6b.xml", "unity/Logs/playmode-t6.xml"):
    r = ET.parse(p).getroot(); at = r.attrib
    print(p, at["total"], at["passed"], at["failed"], at.get("inconclusive"), at["skipped"], at["end-time"])
PY
# 期望:
#   editmode-t6b.xml 7238 7201 0 0 37 2026-11-09 11:42:50Z
#   playmode-t6.xml   371  366 0 2 3  2026-11-09 11:44:30Z

# 缺页类与变量确实在盘上
grep -n "paper-missing" unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss
grep -n "paper-missing-mark-height" unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss
```

---

## 4. 真包截图:**NOT-RUN**(如实登记)

`technique-unlock-map` 给 T6 的判据是「真包截图」。**本环境跑不出来**,原因是结构性的:

> `RenderTexture.Create` 在无图形设备下失败;`ScreenCapture` 走 `WaitForEndOfFrame`,
> 在 batch 下不触发(`modal_toggle_visual_walkthrough.cs:131` 的实测记载,2026-11-01)。

⇒ 走查夹具已备(`paper_missing_walkthrough.cs`),**在 batch 下 `Assert.Ignore`**,
判 `NOT-RUN`;PlayMode 的 `skipped` 计数里就有它(见 §3 的等式)。

**跑法**(需要图形模式,由人来跑):

```bash
~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -projectPath /home/gu/文档/nm/nm2/Claude-Code-Game-Studios/unity \
  -runTests -testPlatform PlayMode \
  -testFilter DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.PaperMissingWalkthrough
# 产物:unity/Logs/shots/paper_missing.png(1920×1080)
```

图应显示:一张纸、标题「止血包扎」在、**正文为空**、纸面带一道斜向撕口。

> ⚠️ **本件不声称「真包截图已验收」。** 机械判据(§2)证明的是**状态**;
> 「看起来像一张被撕过的纸」是 `[L]` 人工面,由图与人的判读完成。

---

## 5. 与「不是灰按钮」的关系

承 `AC-8-44` / `campaign-arc.md:153`「不是灰掉的按钮,是你没有针」:
缺页态的呈现是**这张纸没有了**,不是**按不动**。故本件的渲染是
「正文消失 + 撕口」,而不是「正文变灰」。

---

## 6. 未做(不得借绿)

| 项 | 状态 |
|---|---|
| **哪一页缺的数据绑定**(纸条目 ↔ 技法) | **未做** —— `TutorialPaperRow` 没有 `teachTechnique` 字段;需要时须改 48 的 schema/binder/writer/codec 与产物布局(那是数据管线改动,单独一步) |
| **谁调用 `SetMissingPaperIds`** | **未做** —— 与 T5b 的转诊写者同格:宿主(42/48 的交互)未建 |
| 手册页的内容(除「望诊四要」外) | 归内容轮;今日 `papers` 只有 1 条 |
| 真包截图的判读 | **NOT-RUN**(§4) |
| 缺页时的文案 | **未写** —— 只做视觉,不编玩家可见文案(内容归用户) |
