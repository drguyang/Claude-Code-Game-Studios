# 评审报告原件 —— B-6-1 脉案交互面输入路径接线

- **对象**:B-6-1(第六轮 playtest `playtest-2026-10-13-m2-round6.md` §6,P1 · 收口判据未达)
- **评审日期**:2026-10-14
- **评审角色**:qa-lead 收口核验(承 `director-gates.md` 检查式)
- **结论**:**PASS** —— 自动化半 + 人工半**双半达成**,可转 Complete

## 一、原判定(逐字承出处)

> | B-6-1 | **P1 · 收口判据未达** | 交互面输入路径(真实设备 → `NavigationSubmitEvent`)零自动化覆盖;人工双路径无响应。**优先查**:UI map 是否在 Boot 场景 enable(`InputService`/`PlayerInput` 组件装配)、SkeuoRuntimeDriver/Mount 是否把 42 面板注册进 EventSystem/InputRouter | 新 story(sprint-09 或 hotfix 轮):输入 wiring 测 + 根因修复 |

> **结论**:**CHANGES REQUIRED → 立在 sprint-09(或 hotfix)**… **B-6-1 修复前 sprint-08 不得转 Complete**
> (收口判据 = 自动化绿 + 人工绿双半,承 sprint-07 G5.2 同口径)。

**原判定的两个半**:① 自动化覆盖(输入 wiring 测);② 人工双路径有响应。

## 二、根因(四条正交缺陷叠加,非单一根因)

CLI 勘察确证(承 `ui_input_module_wiring_test.cs` 文件头):

1. **生产启动序从未装配 `EventSystem` + `InputSystemUIInputModule`** —— ADR-013 §F1/F4 的 P0 义务;
   `Boot.unity` 全文零 `EventSystem`、`BootRoot.cs` 零装配;
2. **零初始选中** —— `InputSystemUIInputModule.ProcessNavigation` 的 move(`:858`)与 submit(`:876`)
   **都 gate 于 `eventSystem.currentSelectedGameObject != null`**,而它只在元素已获焦时由
   `PanelEventHandler.OnElementFocus` 设 ⇒ 鸡生蛋,须运行期 bootstrap;
3. **元素无鼠标 handler** —— `CasebookScreen.cs` 6 处回调原**全是** `NavigationSubmitEvent`、
   零 `ClickEvent`,而元素是裸 `VisualElement`(非 Button、无内建 `Clickable`)⇒ 鼠标点击零业务;
4. **Pointer 动作 type 错** —— `UI/{Point,Click,ScrollWheel,MiddleClick,RightClick}` 原为
   Value/Button,verifier 要求 PassThrough。

## 三、修复落点

| # | 根因 | 落点 | 提交 |
|---|---|---|---|
| ① | 未装配 EventSystem | `BootRoot.EnsureEventSystem()`(预摆优先 + 运行期自建 + 动作资产引用断言 fail-loud) | `b37653e` |
| ② | 零初始选中 | 运行期 bootstrap 选中面板载体(判据 `test_bootstrapSelection_selectsPanelCarrier`) | `b37653e` |
| ③④ | 无 ClickEvent / 动作 type | `CasebookScreen.BindSubmitAndClick`(双路径并列挂,禁 `Clickable` —— 它会丢键盘路径)+ `InputSystem_Actions.inputactions` Pointer 改 PassThrough | `b37653e` |
| — | 空栏点病名栏每帧抛异常 ⇒ EPIPE 杀编辑器 | `CasebookScreen.SubmitDiseaseNameBar`(空栏 ⇒ 摊册)+ `BindSubmitAndClick` 每帧异常洪泛守护门 | `a8c7744` |

## 四、验证命令(可证伪 · 全部实测)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 输入 wiring 三条判据(鼠标路径命中 = 原判定「鼠标无响应」的直接反证)
timeout 1200 /home/gu/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -nographics \
  -projectPath "$(pwd)/unity" -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.ui_input_module_wiring_test" \
  -testResults "$(pwd)/unity/Logs/<name>.xml" -logFile "$(pwd)/unity/Logs/<name>.log"
# ⛔ 不加 -quit(2026-10-08 实测:带 -quit 会「正常退出、退出码 0、结果 XML 不落盘」= 假绿)

# ② 空栏回归正反判据
timeout 1200 .../Unity ... -testFilter "DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI.casebook_lexicon_book_test" ...

# ③ 全量门
... -testPlatform EditMode ...
... -testPlatform PlayMode  ...
```

**判据 = 结果 XML 存在 + 数字**,不靠 `exit=$?`(承 `desktop-ac42c10-runbook.md` 警示)。

### 实测结果(2026-10-14)

| 门 | 结果 | 判据面 |
|---|---|---|
| `casebook_lexicon_book_test` | **11/11** | 空栏不抛且不循环 · 已落笔照常循环(防门过宽)· 既有 ④ 语义保住 |
| EditMode 全量 | **3270 passed / 0 failed** / 46 skip / 1 inc | 与基线逐项一致,零回归 |
| PlayMode 全量 | **170/170** | 基线 168 + 新增 2 条反判据 |

日志:`unity/Logs/guard_lexicon2.xml` · `unity/Logs/guard_editmode_full.xml` ·
`unity/Logs/guard_playmode_full.xml`。

## 五、人工半(收口判据的另一半)

- **日期**:2026-10-14(用户真机复验)
- **结果**:**全绿** —— 鼠标点病名栏 / 方向键 + Enter 双路径均有响应;值区横排正常
  (前案/后案不被挤);退出 Play **不卡**。
- 承 `playtest-2026-10-13-m2-round6.md` §8「收口判据 = 自动化绿 + 人工绿双半」。

## 六、遗留(不在 B-6-1 面,不阻塞本项收口)

- **B-6-2** 焦点可见性(P2)· **B-6-3** 可读性(P2)· **B-6-4** 字体资产精度(P3)——
  均另有 story,本项不承载。
- 本次两笔提交**未**改变上列三项的任何判据面。
- ⚠️ 本次还修了一处**非 B-6-1 起因**、但由同一轮人工复验暴露的缺陷:
  空栏点病名栏 ⇒ 每帧异常 ⇒ EPIPE 杀编辑器(`a8c7744`)。它与 B-6-1 的「输入路径未接通」
  **因果同源**(都是交互面接通后才首次可达),故并入本轮;其判据独立,见 §四 ②。
