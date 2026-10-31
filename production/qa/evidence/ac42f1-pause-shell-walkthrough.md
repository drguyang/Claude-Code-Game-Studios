# AC-42-F1 ⑧ 暂停壳 · 走查证据

**日期**:2026-10-31
**对象**:`design/ux/pause-shell-42.md`(闭集第八员,`ModalId.Pause`)
**执行者**:用户(编辑器 Play,人眼腿)+ Claude(像素量测与机器判)
**判据来源**:`AC-42-F1` ⑧ + 本 spec §13 的九条 AC

---

## 一、走查怎么做的

1. 编辑器打开 `unity/Assets/Scenes/Boot.unity` → Play
2. `Escape` 开暂停壳
3. **A/B/A 对照**:不动 → 截图 A;按一次 ↓ → 截图 B;按一次 ↑ → 截图 C
4. 截图落 `unity/Logs/shots/`(F12,`ScreenshotTool`)

**为什么要 A/B/A**:静态截图分不出「焦点指示」与「背景浅色块」。A/B/A 把变量收敛成一条 ——
**A 与 A' 必须逐字节相同**(证明世界是静止的),此时 **A 与 B 的差异才能归因于焦点**。

## 二、实测数据

5 个截图文件按 SHA-256 归为**三态**:

| 标签 | 文件 | SHA-256(前 12) | 含义 |
|---|---|---|---|
| **A** | `20261031_215921_1x.png` · `20261031_215936_1x.png` | `6f453c44abea` | 壳开,焦点在「继续」(两文件**逐字节相同**) |
| **B** | `20261031_215931_1x.png` | `fdea7ebad0c0` | 壳开,焦点在「存档位」 |
| **Z** | `20261031_220006_1x.png` · `20261031_220037_1x.png` | `32029f1566a1` | 壳已关(只剩 止血/通气/跳过) |

量测(仅左侧 UI 列 `x<200`,避开世界):

| 对比 | 差异像素 | 结论 |
|---|---|---|
| A vs A' | **0** | 逐字节相同 ⇒ **世界里没有动体**,背景静止 |
| A vs B | 3354,bbox `y119-154` | 形状 = **同一个斜菱形在两个位置,相距 20 px(= 一行)**⇒ 指示器随焦点下移一行 |
| 该区域黄色像素 | A:2429 · B:2431 · **Z:0** | 指示器**属于暂停壳**,壳一关就消失 |

## 三、AC 结果

| AC | 判定 | 依据 |
|---|---|---|
| PS-01 两行可见 | ✅ | A/B 图里「继续」「存档位」均在 |
| PS-02 落焦「继续」 | ✅ | **机器判** `test_acps02_openPause_focusLandsOnResumeRow` |
| PS-03a 两行间移动 | ✅ | **机器判** `test_acps03_arrowDown_thenUp_movesBetweenTheTwoRows` |
| PS-03b 焦点环可见且随迁 | ✅ | **A/B/A 像素差分**(见上表) |
| PS-03c 焦点不逃出模态 | ✅ | **机器判** `test_acps03_focusNeverEscapes…`(证伪 P1 红) |
| PS-08 无按键提示浮层 | ✅ | 用户目视确认:无「按 X 继续 / [E] 查看」类浮层;左上角三行是急救候选列表(游戏内容) |
| PS-04 继续 / 合壳 | ✅ | **机器判**(2026-10-31 追加)`test_acps04_startOpens_resumeCloses_thenStartReopens` |
| PS-05 进存档位屏换屏 | ✅ | **机器判** `test_acps05_submitOnSaveSlotsRow_switchesScreen_leavingOneContainer` |
| PS-06 模态期输入不生效 | ✅ | **机器判** `test_acps06_modalOpen_gateRejectsAndMovementIsFrozen`(生产 `ModalGate` + `ModalInputFreeze` 双向) |
| PS-07 边沿被消费 | ✅(机制面,仅建造键) | `build_controller_test` 的挡路用例(**不是**人眼腿;**「按住 Enter 再开壳」那条具体判据今日无专测**) |
| PS-09 键鼠/手柄全可达 | ✅ | **机器判** `test_acps09_fullLoop_gamepadOnly` + `test_acps09_fullLoop_keyboardOnly` |

⚠️ **`AC-42-F1` ⑧ 整体仍不得记 ✅**:⑧ 员的签核要求**九条全过**;现仍缺
**PS-01 的「两行可见」人眼半**(壳开得出已由机器判证,「两行在屏幕上看得见」仍需人眼/截图像素)
与 **PS-07 的 Enter 具体形态**。

---

## 三之二、2026-10-31 追加的机器判(四条 AC)

上表的 PS-04 / 05 / 06 / 09 由同一轮的四个编辑器内用例转绿。**判据 = 真链**:虚拟手柄 / 虚拟键盘
→ 生产 `EventSystem` + `InputSystemUIInputModule` → `PanelEventHandler` → `UIElements` 默认动作 →
`focusController` / 生产 sink。**不是**直接调 `OpenModal` 冒充「玩家按了键」。

**证伪轮(5 变异,各跑一次过滤后的 PlayMode,还原 SHA-256 逐字节一致)**

| # | 变异 | 转红 | 覆盖的 AC |
|---|---|---|---|
| **Q1** | `ModalInputDriver` 边沿化停用(`rising := false`) | 3 条 | PS-04 + PS-09 双腿 |
| **Q2** | 「存档位」行改走 `Resume`(不换屏) | 3 条 | PS-05 + PS-09 双腿 |
| **Q3** | 停用 `ModalGate` 方向②(模态开仍放行 E/F) | 1 条 | PS-06 |
| **Q4** | 停用 `ModalInputFreeze.FilterMovement` 折零 | 1 条 | PS-06 |
| **Q5** | `OpenPause` 改为只在「无模态」时才开 | 3 条 | PS-09 的「返回」步(PS-04 连带) |

**⚠️ 装置侧必须先改两条 InputSystem 设置**(写在 `Assemble()` / `TearDown()`,夹具外零影响):
batch 进程**从不获得 Game View 焦点**,而编辑器默认
`editorInputBehaviorInPlayMode = PointersAndKeyboardsRespectGameViewFocus`
+ `backgroundBehavior = ResetAndDisableNonBackgroundDevices`
⇒ **键盘状态事件在普通帧里被丢弃**(实测 `escape/e/f/enter/downArrow/space` 六键全 false,
`anyKey` 亦 false),**手柄不受影响**。夹具改成 `AllDeviceInputAlwaysGoesToGameView` + `IgnoreFocus`
(= 真机语义;这两条策略真机构建里根本不存在),测后还原。
⇒ **任何「键盘腿」的判据若没做这一步,它的绿都要怀疑**:症状是「日志里看不出错,但读面恒 false」。

## 四、如实登记的观察(不影响上述判定)

1. **焦点指示器在画面里呈「斜菱形」**,不是矩形环 —— 疑为黄铜环 slice(`-unity-slice-*` 2px)
   在目标元素上的形变。不影响「可见且随迁」,但**归美术轮**核对 `SkeuoFocusVisible.uss`。
2. **归因方式是行为推断**:「那是焦点环」由「随焦点迁移 + 关壳消失 + 背景静止」三条推出,
   **不是**读到了一个带标签的 UI 元素。
3. 走查首轮(21:25–21:44)曾暴露两个真缺陷,已修并入库:暂停壳两行**零文字**(`PauseScreen` 缺 `Label`)、
   空槽被报成「存档损坏」(7a 语义混淆,已拆 `SaveNotFoundException`)。

## 五、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
sha256sum unity/Logs/shots/20261031_21{5921,5931,5936,}*_1x.png unity/Logs/shots/20261031_2200*_1x.png
# 期望:6f453c44… ×2 · fdea7eba… ×1 · 32029f15… ×2

# 四个机器判用例(PlayMode,过滤到本 fixture)
grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/pause_focus_navigation_test.cs
# 期望:8(02 · 03a · 03c · 04 · 05 · 06 · 09×2)

# 证伪轮:脚本与原始输出已随本件入库(可原样重跑,每轮约 90 s × 5)
grep -E "^(Q[1-5] |[A-Z0-9]+ \| )" production/qa/evidence/falsify-acps-2026-10-31/output.log
# 期望:Q1 红3 · Q2 红3 · Q3 红1 · Q4 红1 · Q5 红3,且每行「还原SHA256=一致」
sha256sum production/qa/evidence/falsify-acps-2026-10-31/*
# 期望:output.log = b2a6cc50… · run.py = 6d42d85f…
```

---

## 六、回归数(带三元组)

| 套件 | 结果 | 截至时刻(CST) | 批次 | commit |
|---|---|---|---|---|
| **PlayMode** | `251 / 247 / 0 / 2 / 2` | 2026-10-31 22:25:52 | 本轮(PS-06 + PS-09×2 三条新测) | `a9e70d97` + 工作树 |
| **EditMode** | `6323 / 6286 / 0 / 0 / 37` | 2026-10-31 22:31:58 | 同上(**本轮零 EditMode 新增**) | `a9e70d97` + 工作树 |

**等式留痕(PlayMode)**:`251 − 248 = 3` = 本轮新增 `[UnityTest]` 数
(`test_acps06_…` · `test_acps09_…gamepadOnly` · `test_acps09_…keyboardOnly`);
`248` = 同日 14:05Z 那轮(已含 PS-04/05)。

⚠️ **EditMode 侧只登记现值,不声称与前一轮的差值** —— 本轮**只改 PlayMode 测试件、未动生产码**,
所以「本侧不该有位移」是**推断**,不是本轮的观测;上一轮的 EditMode 数**不在本轮可复算范围内**
(那轮的 XML 未随件入库)。要判 EditMode 是否被本轮影响,须在**同一 commit 上回退测试件**再跑一次 ——
本轮没做,故不写。

复算:`grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/pause_focus_navigation_test.cs` = **8**。
