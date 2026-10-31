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
| PS-07 边沿被消费 | ✅ | **机器判**(2026-10-31 追加)`test_acps07_heldSubmitAcrossOpen_doesNotConfirm_keyboard` / `…_gamepad`;建造键那面另有 `build_controller_test` |
| PS-09 键鼠/手柄全可达 | ✅ | **机器判** `test_acps09_fullLoop_gamepadOnly` + `test_acps09_fullLoop_keyboardOnly` |

⚠️ **`AC-42-F1` ⑧ 整体仍不得记 ✅**:⑧ 员的签核要求**九条全过**;现仍缺
**PS-01 的「两行可见」人眼半**(壳开得出已由机器判证,「两行在屏幕上看得见」仍需人眼/截图像素)。

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
| **Q6** | 确认回调由 `NavigationSubmitEvent`(按下沿)改绑 `FocusEvent`(聚焦沿)= 开壳即激活 | 6 条 | **PS-07 两条腿**(+ PS-04 / 05 / 09 连带) |

**PS-07 这一条的自证结构**(不止靠变异):用例自带**两足对照** —— 同一焦点、同一按键,
「跨开壳一直按住 ⇒ 不得确认」对「松手后新按一次 ⇒ 必须确认」。少了后者,一个「提交链根本不通」
的实现也会让前者绿(空洞正控 F4)。另有一条**靶场前提**断言:开壳后先断焦点确实落在「继续」上,
否则「没触发」可能只是「靶场里没有靶」。

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

# 十条机器判用例(PlayMode,过滤到本 fixture)
grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/pause_focus_navigation_test.cs
# 期望:10(02 · 03a · 03c · 04 · 05 · 06 · 07×2 · 09×2)

# 证伪轮:脚本与原始输出已随本件入库(可原样重跑;Q1–Q5 走 run.py,Q6 为手工变异)
grep -E "^(Q[1-6] |[A-Z0-9]+ \| )" production/qa/evidence/falsify-acps-2026-10-31/output.txt
# 期望:Q1 红3 · Q2 红3 · Q3 红1 · Q4 红1 · Q5 红3,且每行「还原SHA256=一致」
grep -E "^(Failed|total=)" production/qa/evidence/falsify-acps-2026-10-31/q6-ps07-output.txt
# 期望:total=10 passed=4 failed=6,含两条 test_acps07_…
sha256sum production/qa/evidence/falsify-acps-2026-10-31/*
# 期望:output.txt = b2a6cc50… · q6-ps07-output.txt = 6342a413… · run.py = 07def0dd…
```

---

## 六、回归数(带三元组)

| 套件 | 结果 | 截至时刻(CST) | 批次 | commit |
|---|---|---|---|---|
| **PlayMode** | `253 / 249 / 0 / 2 / 2` | 2026-10-31 22:42:15 | 本批(PS-06 + PS-07×2 + PS-09×2 五条新测) | `9ac39328` + 工作树 |
| **EditMode** | `6323 / 6286 / 0 / 0 / 37` | 2026-10-31 22:31:58 | 同上(**本批零 EditMode 新增**) | `a9e70d97` + 工作树 |
| (同日更早一轮 PlayMode,留作对照) | `251 / 247 / 0 / 2 / 2` | 2026-10-31 22:25:52 | PS-06 + PS-09×2 | `a9e70d97` + 工作树 |

**等式留痕(PlayMode)**:`253 − 251 = 2` = 本批新增的 PS-07 两条腿;
`251 − 248 = 3` = 上一批的 PS-06 + PS-09×2;`248` = 同日 14:05Z 那轮(已含 PS-04/05)。
两段相加 = 本轮相对 14:05Z 的 `+5`,与文件内 `[UnityTest]` 从 5 增到 10 一致。

⚠️ **EditMode 侧只登记现值,不声称与前一轮的差值** —— 本轮**只改 PlayMode 测试件、未动生产码**,
所以「本侧不该有位移」是**推断**,不是本轮的观测;上一轮的 EditMode 数**不在本轮可复算范围内**
(那轮的 XML 未随件入库)。要判 EditMode 是否被本轮影响,须在**同一 commit 上回退测试件**再跑一次 ——
本轮没做,故不写。

复算:`grep -c "\[UnityTest\]" unity/Assets/Tests/PlayMode/SkeuomorphicUI/pause_focus_navigation_test.cs` = **10**。

---

## 七、2026-10-31 补拍(AC-PS-01)+ 两处结构性缺陷

### 怎么拍的

batch 出不了图(项目已两次实测,见 `ScreenshotTool.cs` 头注)⇒ 走**编辑器 GUI + Play** 的
临时取证脚本(跨 domain reload 用 `SessionState` 布防 + `[InitializeOnLoadMethod]` 重挂;
首版栽在「进 Play 会触发域重载,`EditorApplication.update` 的普通静态委托被清掉」——
症状是日志里只有「进入 Play」,之后一步不动)。**脚本用完即删,未入库。**

### 实拍结果

| 文件 | 内容 |
|---|---|
| `ps001-20261031_225300-A-shell-open-focus-resume.png` | 壳开,焦点在「继续」 |
| `ps001-20261031_225300-A2-still.png` | 同状态再截;**与 A 逐字节相同**(sha `300b380d…`)⇒ 世界静止 |
| `ps001-20261031_225300-Z-shell-closed.png` | 关壳(对照) |

**PS-01 判定(人眼腿)**:两行文字**只随壳出现**,关壳即消失;1× 下能读出「继续」「存档位」。
⇒ 记 `[x]`。

### 同轮抓到的两处**结构性**缺陷(不是审美)

运行时布局 dump(真面板 1680×1215,暂停壳开):

```
modal-Pause        (0,204,1680,153)
  pause-list
    (paper)        (0,204,1680,40)    ← 纸面只有 40 高
    pause-resume   (0,244,1680,57)   bg=透明
       label       (3,246,1674,48)
       focus-visible (0,244,1680,54)
    pause-save-slots (0,300,1680,57) bg=透明
```

| # | 缺陷 | 根因 | 状态 |
|---|---|---|---|
| ① | **两行没有纸底**,暗字压在世界暗面上 | `SkeuoElement.Paper` 被当成**流内兄弟**加在行前面 —— 空元素高度只有 padding+border(=40px),并不在行**后面**。**违反 `pause-shell-42.md` §5 的 ASCII(纸面应包住两行)** | ✅ **已修**:行改为纸面的子元素(`PauseScreen.BuildUI`) |
| ② | **焦点指示满宽 + 形变** | 行铺满整个面板(**1680** 宽),而焦点环是贴在行内的 overlay、尺寸 = 行尺寸 ⇒ 环被拉成横贯全屏的带 | 🔶 **部分修**:新增 `.pause-column`(`--skeuo-shared-modal-width` 480 + 居中),行宽降到 **439**;但环图**仍不是干净的 2px 框** |

修后实测(dump):`pause-list(pause-column) (601,204,479,153)` · `pause-paper (601,204,479,153)` ·
`pause-resume (620,224,439,57)` —— 纸面包住了两行,列居中。

### ② 的未解部分(如实登记,未修)

修後实拍(`ps002-20261031_230235-D-shell-open.png`):纸面卡片正常,「继续」「存档位」清楚可读;
但焦点行上的黄铜指示**不是环住行的 2px 框**,而是一块把行内区域盖住的黄色图形。

已排除 / 已确认(**都是实跑,不是推断**):

- 环元素**几何正确**:dump `(620,224,439,54)`,正好覆盖第一行。
- 品红探针(临时把环子元素刷成纯色、去掉贴图)⇒ **整行变成品红块并盖住「继续」**
  ⇒ 问题在**环图怎么画**,不在元素尺寸。
- 环贴图本身没问题:64×64,2px 均匀铜框,`spriteBorder (2,2,2,2)`。
- 把该图导入临时改成 `spriteMeshType: FullRect` 复跑 ⇒ **画面无变化**(已还原)。
- **对照**:同一构建下**脉案屏**的获焦行,环是**干净的细金框**(`zoom-casebook-focusrow.png`)。

### 🔁 2026-10-31 续挖:差异已定位到 **USS 取图路径**(不再"未定")

**第一步先推翻了一个我自己的读法**:`.ruled` vs `.paper` **不是**差异点。同屏 A/B
(第 0 行裸 `.ruled`、第 1 行临时改成脉案例的 `Paper` 行)实拍:
焦点在第 0 行时黄斑在第 0 行,把焦点移到第 1 行,**黄斑跟到 paper 行上**。
⇒ 黄斑**跟着焦点走,两种行都有**。我此前说「脉案屏的环是干净细框」是**看错了** ——
那是 paper 行自己的边框,焦点环在脉案屏上同样有问题,只是被边框盖住了。

**第二步做四格隔离(同一张 sprite / 同一 slice / 同一尺寸 110×110,只有设置方式不同)**:

| 探针 | 设置方式 | 渲染 |
|---|---|---|
| ① 铜环 sprite + slice 2 | **代码直灌** `style.backgroundImage = new StyleBackground(sprite)` | ✅ 干净 2px 铜框 |
| ② 纸纹 sprite + slice 64 | 代码直灌 | 黑块 —— **slice 之和(64+64)大于元素(110)时裁切退化**,附带发现 |
| ③ 同一张图 + slice 2 | **USS `.focus-visible`**(`url("guid:…")`) | ❌ 黄色三角斑 |

> ⚠️ **2026-10-31 就地加注(不改上表原文)**:② 那句括号里的归因
> 「slice 之和 > 元素尺寸 ⇒ 裁切退化」**未复跑,已登记为 `TD-skeuo-01`**。
> 复核时找到**单轴反例**:`.paper` 行「前案 / 后案」(slice 64/64/64/64)高 ≈30px
> 照常渲染(`production/qa/evidence/skeuomorphic-ui/dentch-form-2026-10-18.png`)。
> ⇒ 这句话**在单轴上不成立**;「两轴越界」这一支至今**既未证实也未证伪**。
> 本条探针的**有效结论只有 ① / ③ 的对比**(代码路径对、USS 路径错),② 只算附带观测。
>
> 🔁 **2026-10-31 归因轮已闭**(12 格同屏探针,两轮真渲染):黑块**只在代码灌注 sprite 路径上**,
> 且需**两轴同时**小于 sprite border 之和(110×110 / border 64 = 黑;200×200 · 480×110 · 110×480 ·
> border 0 · border 16 均正常);**USS `url(guid:)` 路径免疫**(它解析出的是 32×32 纹理 + `sprite=null`)。
> ⇒ ② 那句话**对一半**(越界是真因)**错一半**(不适用于 USS,也不适用于单轴)。
> 探针与截图:`production/qa/evidence/skeuomorphic-ui/nineslice-probe-2026-10-31*`;账在 `TD-skeuo-01`。

⇒ **同一资产、同样 slice、同样尺寸:代码路径对、USS 路径错** ⇒ 缺陷在 `SkeuoFocusVisible.uss`
的**取图方式**,不在贴图、不在图集、不在行类型。

**已排除的三个候选**(各一次实跑,均已还原):
① 九宫格被忽略 —— 否:把 slice 由 2px 改 8px,画面**跟着变** ⇒ 切片在生效;
② `spriteMeshType` —— 否:改 FullRect 复跑无变化;
③ 规则里的 `border-*-width: 0` + `border-*-color` 组 —— 否:整组注释掉复跑,**仍是黄斑**。

⇒ 剩下唯一未排除的差异 = **USS 如何解析并加载该 sprite**。

### ✅ 根因(2026-10-31 用户裁定「代码灌注环图」后实测定死)

探针直接读运行期解析结果,一句话定死:

```
[RP] probeB resolved: sprite=False  tex=32x32  slices=2/2/2/2
[RP] probeC 代码直灌;border=(2.00, 2.00, 2.00, 2.00)
```

⇒ **USS 的 `url("guid:…")` 把这张图解析成 `Texture2D`(`sprite == null`,还被缩到 32×32)**,
sprite 自带的九宫格边框(`border = 2px`)**随之丢失** ⇒ UI Toolkit 只能拿「无边框贴图 + USS
里的 slice 数字」硬切,画出来就是那块黄斑。代码 `StyleBackground(Sprite)` 路径拿到的是**带 border
的 Sprite**,所以是对的。

**处置(已落)**:`SkeuoRuntimeDriver` 增 `[SerializeField] Sprite _focusRingSprite`(带
`FocusRingSprite` 观测面),`CreateFocusRing()` 用它**代码灌注**图;slice 与几何**仍以 USS 为单一出处**;
未注入 ⇒ 回落 USS 旧行为(有缺陷但不崩)。场景 `Boot.unity` 的 `SkeuoUI` 已接上该引用。

**修后实拍**:`rp-20261031_233205-*.png` —— 获焦行被一圈**干净的细矩形环**包住,
两行文字均完整可读,黄斑消失。

### 🔁 实拍复核(2026-11-01 · 真游戏 Boot 场景,双帧对照)

首图是**单帧**、且当时焦点落在哪一行没有机读记录 ⇒ 补一次带机读的对照复核。
探针走 `-executeMethod`(GUI 模式,Boot 场景真启动),开暂停壳 → 焦点落「存档位」拍一张 →
焦点落「继续」再拍一张;每次拍完**等文件真的落盘**才移动焦点。

| 观测面 | 焦点 = 存档位(row1) | 焦点 = 继续(row0) |
|---|---|---|
| 环挂在哪 | `row1` 有环 / `row0` **NONE** | `row0` 有环 / `row1` **NONE** |
| 环几何 | `439×54` @ (620,280) | `439×54` @ (620,224) |
| 环的图 | `focus_brass_2px-final` tex `64×64` border `(2,2,2,2)` | 同左 |

**结论**:环**随焦点走、每次只有一个**;图来自**代码灌注**(sprite 非 null、border 2px),
不是 USS 回落;实拍里是一圈**细矩形框**,包住整行,文字完整可读 —— 与机读一致。

- 实拍:`production/qa/evidence/skeuomorphic-ui/ring-recheck-2026-11-01-row1.png` · `…-row0.png`
- 探针源码:`…/ring-recheck-2026-11-01.cs.txt`(可重跑)
- 两张图**逐像素不同**(bbox `(219,79,374,118)` = 两行区),可作为「确实换了焦点」的旁证

⚠️ **踩到的坑(写给下一个截图的人)**:首轮**两张拍成了同一帧**(`ImageChops.difference` 的
`bbox = None`)。原因是 `ScreenCapture.CaptureScreenshot` **写盘延后**,我在文件落盘前就把焦点
移走了 ⇒ 两个文件写的是**同一帧**。修法:拍完**轮询等文件非空**再动下一状态。
⚠️ **该坑不会报错** —— 两张图都正常、日志都正常,只有比对像素才看得出来。

⚠️ **影响面**:环施加者是**全屏共用**的(`SkeuoRuntimeDriver.AttachFocusRingApplier`),
所以这条修复**同时**修好脉案 / 出诊箱等所有屏的焦点环 —— 此前它们在 paper 边框的掩护下看起来"还行"。

**机器判**(2026-10-31 追加,`pause_focus_navigation_test.cs`):
`test_focusRing_imageIsInjectedFromSprite_notOnlyFromUss` —— 注入合成 sprite ⇒ 环上
`style.backgroundImage.value.sprite` 必须是**那个对象**;**负控**:同一装置不注入 ⇒ 环上
**不得**有 inline 图(回落 USS)。★ 负控是必需的:少了它,「永远不注入」也能让正例恒绿。

**证伪 Q7**:把 `CreateFocusRing()` 里的注入改成 `if (false)` ⇒ **该条红**
(`Expected: same as <Sprite> · But was: null`),还原 SHA-256 `d023acb8…` 逐字节一致。

**回归**:EditMode `6323 / 6286 / 0 / 0 / 37` @ 2026-10-31 23:35:18 CST ·
PlayMode `254 / 250 / 0 / 2 / 2` @ 23:38:40 CST(同 commit + 工作树;
等式 `254 − 253 = 1` = 本轮新增的那条机器判)。

判据见 `unity/Logs/shots/zoom-*.png` 与 `ab-*` 系列(Logs 不入库)。
⚠️ **这一项不归本轮关掉**:它落在**全局焦点载体契约**(`AC-42-G4` + `SkeuoFocusVisible.uss`
的冻结件 `focus-v1`)面上,改它要动冻结资产与比对门 ⇒ 须先裁定,不得由本屏私改。

### 复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
ls -la unity/Logs/shots/ps00{1,2}-*.png      # 实拍件(Logs/ 被 .gitignore 覆盖 ⇒ 仅为本地证据)
grep -n "pause-column" -A 3 unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss
grep -n "paper.Add(row)" unity/Assets/Gameplay.UI/Skeuomorphic/Screens/PauseScreen.cs
# 期望:USS 里有 .pause-column(宽度走 var)· PauseScreen 里行加进 paper 而非 _host
```
