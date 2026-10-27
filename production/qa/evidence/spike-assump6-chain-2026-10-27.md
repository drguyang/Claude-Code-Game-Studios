# ADR-013 假设 6 —— 整链腿(虚拟手柄 → 生产 EventSystem → 焦点移动)

**日期**:2026-10-27
**性质**:Engine spike 实测记录(非评审件)
**对象**:`production/u1-spike-checklist.md` §5.2 四判据的**行为面**
**测试件**:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/u1_assump6_gamepad_chain_test.cs`
**前序件**:`spike-assump6-2026-10-27.md`(自动化腿 —— 引擎能力前提,本件**订正**其 §1/§3 结论)

---

## 0. 一句话

**判据 ①② 的行为面不再是 `NOT-RUN`。** 四条判据在**生产装配 + 虚拟手柄**下 4/4 绿,
三轮变异各自精确命中,逐字节还原。

---

## 1. 为什么上一轮判断错了(必须先说清)

上一轮(`spike-assump6-2026-10-27.md` §3.1)的结论是「判据①② 结构上不可验」,
依据是 2026-10-08 的实证:`VisualElement.SendEvent(ev)` 只跑用户回调阶段,
默认动作阶段 `ExecuteDefaultAction*` 是 `protected internal`。

**那个依据本身没错,但它只覆盖一条路。** 手柄导航不走 `VisualElement.SendEvent`:

| 环节 | 成员 | 可见性 | 出处 |
|---|---|---|---|
| 输入模块入口 | `InputSystemUIInputModule.Process()` | **public override** | `InputSystemUIInputModule.cs:2382` |
| 导航处理 | `ProcessNavigation(ref NavigationModel)` | internal | `:797` |
| UGUI 派发 | `ExecuteEvents.Execute(selectedGO, …, moveHandler)` | 公开 API | `:858` |
| **落地拐点** | **`PanelEventHandler.OnMove(AxisEventData)`** | **public** | `PanelEventHandler.cs:249` |
| 面板派发 | `m_Panel.SendEvent(NavigationMoveEvent)` | **面板级,非 VisualElement** | `:261` / `:285-288` |

**关键区别**:`m_Panel.SendEvent(e)`(`BaseVisualElementPanel`)与
`VisualElement.SendEvent(e)` 是**两个不同的入口**。前者的默认动作阶段由
`EventDispatcher` 驱动,是完整派发;后者只跑回调。

⚠️ **这是同一天内的第二次同型误归因**(第一次见前序件 §3.2 的 run2)。
两次的共同形态:**用一个失败的尝试去证明能力不存在,而没有先穷举入口。**
本件把这条写进证据,因为它的代价很高 —— 若照前序件结论走,
会错误触发 ADR-013 的「自实现焦点算法」回退。

---

## 2. 结果

### 2.1 执行命令(可复现)

```bash
cd unity   # ⚠️ 必须 cd 到 unity/,-projectPath 指向绝对路径(仓库根没有 Assets/)
timeout 1500 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.U1Assump6GamepadChainTest" \
  -testResults "$PWD/Logs/u1-chain-final.xml" \
  -logFile "$PWD/Logs/u1-chain-final.log"
```

### 2.2 干净轮

```
汇总: result=Passed  total=4  passed=4  failed=0  inconclusive=0
```

### 2.3 判据① —— 每次输入恰好一次移动

```
right btn-1-1 → btn-1-2   nav事件=1 焦点跳变=1
down  btn-1-2 → btn-2-2   nav事件=1 焦点跳变=1
left  btn-2-2 → btn-2-1   nav事件=1 焦点跳变=1
up    btn-2-1 → btn-1-1   nav事件=1 焦点跳变=1
```

**四轮全部 nav=1 / 焦点=1** ⇒ 无同键双触发。

### 2.4 判据② —— 引擎自动完成空间选邻居

```
right 期望=btn-1-2  实得=btn-1-2  ✅
down  期望=btn-2-1  实得=btn-2-1  ✅
left  期望=btn-1-0  实得=btn-1-0  ✅
up    期望=btn-0-1  实得=btn-0-1  ✅
```

**为什么这条有判别力**:网格按**行主序**建(`btn-0-0, btn-0-1, btn-0-2, btn-1-0, …`),
树序的「下一格」恒为 `btn-1-2`。四向里 **down / left / up 三向的实得都 ≠ 树序**
⇒ 几何排序确实在起作用,不是巧合,也不是树序退化。

### 2.5 判据② 的「边界行为可预期」切片 —— **事实记录,不判红**

| 角落 | 按 left | 按 right | 按 up | 按 down |
|---|---|---|---|---|
| btn-0-0 左上 | btn-0-2 **环绕** | btn-0-1 正常 | btn-2-0 **环绕** | btn-1-0 正常 |
| btn-0-2 右上 | btn-0-1 正常 | btn-0-0 **环绕** | btn-2-2 **环绕** | btn-1-2 正常 |
| btn-2-0 左下 | btn-2-2 **环绕** | btn-2-1 正常 | btn-1-0 正常 | btn-0-0 **环绕** |
| btn-2-2 右下 | btn-2-1 正常 | btn-2-0 **环绕** | btn-1-2 正常 | btn-0-2 **环绕** |

**实测结论:UI Toolkit 的 `PositionXY` 焦点环在网格边界处「环绕(wrap-around)」,不截断。**
水平越界 ⇒ 跳同行对侧;垂直越界 ⇒ 跳同列对端;界内方向 ⇒ 正常落相邻格。
四角八向**完全一致**,无例外。

⚠️ **本条只记录不判红,是刻意的。** 检查卡 §5.2 判据② 要求「边界环绕/截断行为**可预期**」——
环绕与截断**都算可预期**,哪个是产品想要的**归人定**。把它写成断言 = 替产品拍板。
产品若要求截断,做法是在 USS/元素层处理,不是改这条记录。

### 2.6 判据④ —— 单一 `UI/Navigate` 来源

```
EventSystem=1 · InputSystemUIInputModule=1
焦点导航唯一驱动源 = UI/Navigate
```

**判据形态为什么是「谁能驱动焦点」而不是「绑定路径求交」—— 两版误报的如实记录**:

| 版本 | 谓词 | 实测结果 | 定性 |
|---|---|---|---|
| v1 | UI map 里 `PassThrough + Vector2` 即红 | 抓到 `UI/Point` · `UI/ScrollWheel` | **误报** —— 指针/滚轮不驱动焦点 |
| v2 | 与 `UI/Navigate` **绑定路径求交** | 抓到 `Player/Move ← 方向键` · `UI/ScrollWheel ← dpad` | **误报** —— `Player/Move` 是 `Value` 型且属另一 map,不喂 UI 模块 |
| **v3(现行)** | **UI 模块的 `move` 引用恰指 `UI/Navigate`,且 EventSystem / UI 模块各恰一个** | **绿** | ✅ 这才是「焦点导航驱动源唯一」的字面判据 |

**v1/v2 的共同错误**:把「同键」当成了「双喂」。ADR-011 §Constraints 禁的是
**Navigation 动作重复绑定**,即**第二个能产生 `NavigationMoveEvent` 的来源**。

**同键重叠仍登记(不判红)**,作为将来别人改动时的复核清单:

```
Player/Move(Value)        ← <Keyboard>/upArrow / downArrow / leftArrow / rightArrow
UI/Point(PassThrough)     ← <XRController>/{Primary2DAxis}
UI/ScrollWheel(PassThrough) ← <Gamepad>/dpad
```

三者当前都**不**驱动焦点:v1 = Value 型归 Gameplay 直读;v2/v3 = 指针/滚轮语义。
若将来有人把 `Player/Move` 改成 `PassThrough`,或把 ScrollWheel 接进焦点路径,风险才成立。

---

## 3. 证伪轮(三轮,一属性一条变异)

> 承「**一属性一条变异**」纪律:「今天的安全属性」与「明天的复用件逻辑」各留一条。

### 3.1 变异 A —— 判据② 方向语义

**变异**:`BuildGrid` 里列坐标镜像 `b.style.left = (Columns-1-c) * CellSize`。
**原理**:镜像后 `btn-1-2` 在几何上位于 `btn-1-1` 的**左侧**。若测试真按几何判,
`right` 就该落到 `btn-1-0`;硬编码期望 `btn-1-2` 必红。

| 判据 | 结果 |
|---|---|
| 判据② | ✅ **红**:`right: 期望 btn-1-2 实得 btn-1-0` · `left: 期望 btn-1-0 实得 btn-1-2` |
| 判据① | ✅ 未受影响(4 轮仍 nav=1 / 焦点=1)—— 它对几何变异本就不该敏感 |
| 判据②b / ④ | ✅ 不受影响 |

**关键旁证**:`down` / `up`(行轴)在变异下**仍正确** —— 证明红是**列轴镜像**造成的,
不是夹具整体失效。

### 3.2 变异 B —— 判据① 计数

**变异**:`Press()` 里在正常派发之后再补一次
`_handler.OnMove(new AxisEventData(_es){ moveVector = v })`,模拟 ADR-013 登记的
头号失败形态「同键双触发」。

| 判据 | 结果 |
|---|---|
| 判据① | ✅ **红**:四轮全部 `nav事件=2 焦点跳变=2`;`Expected: 0, But was: 8` |
| 判据② | ✅ **连带红**:四向落点全部错位(焦点多跳了一格)—— 这正是双触发的真实后果 |

> ⚠️ **判据② 在变异 B 下也红**,这是**预期的**:双触发会真的多移动一格。
> 但它不削弱判据② 的独立性 —— 变异 A 已单独证明判据② 只被几何变异打中。

### 3.3 变异 C —— 判据④ 单一来源

**变异**:生产代码 `BootRoot.cs:541` 之后把 `module.move` 指向 `UI/Point`。

| 判据 | 结果 |
|---|---|
| 判据④ | ✅ **红**:`UI 模块的 move 指向 Point(应为 Navigate)` |
| 判据①②②b | ✅ 不受影响 |

> 📌 **过程记录(不掩盖)**:变异 C 首版写 `new InputActionReference(action)` ⇒ CS1729
> (该类型无单参构造器)。正解是 `InputActionReference.Create(action)`
> (`InputActionReference.cs:198`)。**首版编译失败 ≠ 判据被验**,已重跑。

### 3.4 还原判据 —— SHA-256 逐字节

| 文件 | 变异前 | 还原后 | 判据 |
|---|---|---|---|
| `u1_assump6_gamepad_chain_test.cs` | `4db559a3…f72d4232` | `4db559a3…f72d4232` | ✅ 相同 |
| `BootRoot.cs` | `bd7f2b5d…71251f91` | `bd7f2b5d…71251f91` | ✅ 相同 |

`grep -c 'FALSIFY-MUTANT'` 两个文件均 **0**;`git diff --stat` 均**空**。
**未用** `git checkout` / `git stash`(承「证伪还原禁用 checkout」纪律),用 `cp` 回 + 哈希比对。

### 3.5 还原后复跑

```
汇总: result=Passed  total=4  passed=4  failed=0  inconclusive=0
```

---

## 4. 边界:本件**不**声称的事

- ❌ **不声称「真实手柄硬件可用」** —— 本件驱动的是 `InputSystem.AddDevice<Gamepad>()`
  虚拟设备。**轴死区 / 驱动 / 平台映射差异不在面内**。
- ❌ **不声称「高亮可见」** —— 全程 `-nographics`,焦点态 USS 的**视觉呈现**未验。
  这是判据③ 的「高亮可见」一半,仍归人工腿。
- ❌ **不声称「手感合格」** —— 移动是否跟手、是否跳格、摇杆推多少才开始动,
  全是主观判断,本件一条都没测。
- ❌ **不声称判据在真实 `InventoryContainer` 面板上成立** —— 本件用**最小夹具**
  (3×3 绝对定位按钮),不是药箱真实面板。真实面板的布局复杂度更高。

---

## 5. 对 ADR-013 的影响

**不触发任何回退路径。**

- ADR-013 §6.6 假设 6 的失败动作是「自实现焦点算法」。**本次未发现引擎缺陷** ——
  四条判据全绿。
- 前序件与**本件**都指向同一结论:**引擎侧的几何焦点导航能力成立且质量符合判据**。
- `S5`(Renderer Feature 兜底)**继续休眠** —— 它只在假设 6 失败时激活。

⚠️ **但假设 6 仍不得标「已实测通过」**,理由换了一条:不是「没验」,
而是**验证面有意留了口** —— 真实硬件 / 高亮可见 / 主观手感三项仍在。
建议标法:**「引擎与装配面已实测(四判据 4/4),真实硬件与手感归【桌面】人工腿」**。

---

## 6. 剩余归人工腿的项(收窄后的清单)

| 项 | 为什么机器做不了 |
|---|---|
| 真实手柄硬件 | 本机**没有手柄设备**(实测:`/dev/input/js*` 零命中,`event*` 全为键盘/鼠标) |
| 高亮是否「看得见」 | `-nographics` 无渲染,焦点态 USS 的视觉对比度未验 |
| 移动是否跟手 / 是否跳格 | 主观 |
| 摇杆死区手感 | 主观 + 依赖真实硬件轴特性 |
| 边界环绕 vs 截断的产品取舍 | **需产品裁定** —— 实测行为(环绕)已知,但「要不要环绕」是人定 |

---

## 7. 可复算命令汇总

```bash
# 1. 干净轮(期望 total=4 passed=4 failed=0)
cd unity && timeout 1500 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.U1Assump6GamepadChainTest" \
  -testResults "$PWD/Logs/u1-chain-final.xml" -logFile "$PWD/Logs/u1-chain-final.log"

# 2. 判据① 证据行(期望四行全部 nav=1 焦点=1)
python3 -c "
import xml.etree.ElementTree as ET
r=ET.parse('Logs/u1-chain-final.xml').getroot()
for tc in r.iter('test-case'):
    o=tc.find('output')
    if o is not None and 'nav事件' in (o.text or ''): print(o.text)
"

# 3. 判据② 证据行(期望四向全 ✅)
grep -o "U1-A6 整链\] [a-z]* *期望=[a-z0-9-]* *实得=[a-z0-9-]* *[✅❌]" Logs/u1-chain-final.xml

# 4. 边界环绕表(16 行)
grep -o "U1-A6 整链·边界.*" Logs/u1-chain-final.xml
```

---

## 8. 附:本轮自造的两次弯路(如实记录)

| # | 弯路 | 代价 | 教训 |
|---|---|---|---|
| 1 | 判据④ 谓词连错两版(v1 抓 Point/ScrollWheel,v2 抓 Player/Move) | 两次全量跑测 | **判据得先想清「什么才算违例」**,别拿形态近似当判据 |
| 2 | 变异 C 首版 `new InputActionReference(action)` CS1729 | 一次编译失败轮 | 编译失败**不算**判据被验,必须重跑 |
| 3 | 判据②b 首版把所有「非停留」都标成「环绕/跳格」,正常相邻移动也误标 | 一行输出文案 | 分类器要覆盖**全部**应然类别,别只写两个分支 |
