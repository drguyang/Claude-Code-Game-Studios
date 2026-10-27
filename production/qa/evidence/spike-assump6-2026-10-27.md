# ADR-013 假设 6(手柄焦点导航)—— 自动化腿 spike 原件

**日期**:2026-10-27
**性质**:Engine spike 实测记录(非评审件)
**对象**:`production/u1-spike-checklist.md:207` 记「假设 6 ⬜ 未跑」的自动化可验部分
**测试件**:`unity/Assets/Tests/PlayMode/SkeuomorphicUI/u1_assump6_gamepad_focus_spike_test.cs`

> ⚠️ **本件不把假设 6 标为「已实测」**。它只补掉四判据里**自动化腿能验的那部分**,
> 判据 ①②的行为面**仍 `NOT-RUN`**,归【桌面】人工手柄腿。详见 §3 边界表。

---

## 1. 结论先说

| 判据 | 自动化腿结果 | 归谁 |
|---|---|---|
| ① 每次输入恰好一次移动(双触发) | **`NOT-RUN`** —— 结构上不可验 | 【桌面】人工腿 |
| ② 引擎按几何位置选邻居 | **引擎侧前提 ✅ / 方向语义 `NOT-RUN`** | 前半本件,后半人工腿 |
| ③ 无指针可达全部控件 | **✅ 9/9** | **本件已验** |
| ④ 单一 `UI/Navigate` 来源 | 不重跑,引检查卡 `:207` 静态核实结论 | — |

**假设 6 整体仍不得标「已实测」** —— ①②的行为面缺口未补。见 §3。

---

## 2. 实测数据

### 2.1 执行命令(可复现)

```bash
cd unity   # ⚠️ 必须 cd 到 unity/，-projectPath 指向绝对路径（仓库根没有 Assets/）
timeout 900 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.U1Assump6GamepadFocusSpikeTest" \
  -testResults "$PWD/Logs/u1-assump6-final2.xml" \
  -logFile "$PWD/Logs/u1-assump6-final2.log"
```

**环境**:Unity 6000.3.24f1 · `-batchmode -nographics` · Linux

### 2.2 结果(干净轮)

```
汇总: result=Passed  total=5  passed=3  failed=0  inconclusive=2
```

| 测试 | 结果 | 输出 |
|---|---|---|
| `test_u1assump6_allControls_reachableWithoutPointer` | **Passed** | `判据③ 可达性:9/9 个控件在无指针条件下可聚焦且可落焦` |
| `test_u1assump6_positionXY_doesNotDegradeToChildOrder` | **Passed** | `同输入(btn-1-1, right):ChildOrder → btn-1-2;PositionXY → btn-2-1` |
| `test_u1assump6_judgement1_doubleTrigger_notRun` | **Inconclusive** | 判据① NOT-RUN 说明 |
| `test_u1assump6_judgement2_directionalSemantics_notRun` | **Inconclusive** | 判据② 方向语义 NOT-RUN 说明 |
| `test_u1assump6_focusRing_positionXY_picksGeometricNeighbor` | Passed(`[Explicit]`) | 仅诊断输出,不参与判决(已废弃路线,保留过程记录) |

**判据③ 的直接证据**:**9/9**。3×3 网格全部按钮 `canGrabFocus == true` 且逐个 `Focus()` 后
`focusController.focusedElement` 都落在该按钮上 ⇒ 无指针条件下**全部可达**。

**判据② 的直接证据**:同一 3×3 网格、同一输入 `(btn-1-1, right)`,两种排序给出**不同**落点 ——
`ChildOrder → btn-1-2`(树序),`PositionXY → btn-2-1`(几何序)。
⇒ 「按几何位置排序」**不是**树序退化,**几何位置确实参与排序**。

### 2.3 证伪轮(证明上述断言有判别力)

**变异**:把 `xyRing` 的 `defaultFocusOrder` 从 `PositionXY` 改成 `ChildOrder`
(模拟「几何序失效」的假想形态)。

| # | 要求 | 实测 |
|---|---|---|
| 1 | 该断言**转红** | ✅ `test_u1assump6_positionXY_doesNotDegradeToChildOrder` **Failed** |
| 2 | 其余测试**不受影响** | ✅ 2 Passed + 2 Inconclusive 与干净轮一致 |
| 3 | 红的原因是目标判据 | ✅ 消息:`判据② 红:PositionXY 与 ChildOrder 给出同一落点 (btn-1-2) —— 几何序未生效` |

**还原判据 = SHA-256 逐字节**:

```
变异前: ea9212b3cdc68b94f42d5c82c1be2f1d8e5ab33c9ead619ae694c15cd6165d87
还原后: ea9212b3cdc68b94f42d5c82c1be2f1d8e5ab33c9ead619ae694c15cd6165d87   ← 相同
FALSIFY-MUTANT 残留: grep -c → 0
git diff --stat → 空
```

**未用** `git checkout` / `git stash`(承「证伪还原禁用 checkout」纪律),用 `cp` 回 + 哈希比对。

---

## 3. 边界:为什么假设 6 仍不能标「已实测」

### 3.1 结构性障碍(IL 实证,不是推演)

从 `UnityEngine.UIElementsModule.dll` 直接读元数据(2026-10-27,dnfile 0.18.0):

| 类型 | 成员 | 可见性 |
|---|---|---|
| `FocusController` | `SwitchFocusOnEvent` | **`assembly`(internal)** |
| `FocusController` | `FocusNextInDirection` | **`assembly`(internal)** |
| `FocusController` | `SwitchFocus`(两个重载) | **`assembly`(internal)** |
| `EventDispatcher` | `Dispatch` | **`assembly`(internal)** |
| `VisualElementFocusRing` | `.ctor` / `GetNextFocusable` / `GetFocusChangeDirection` / `set_defaultFocusOrder` | public |
| `IFocusRing` | `GetFocusChangeDirection` / `GetNextFocusable` | public |
| `Focusable` | `Focus()` / `canGrabFocus` | public |

⇒ **进入引擎「默认动作阶段」的公开入口不存在**。`SendEvent` 只跑**用户回调阶段**
(同族结论已由 `ac42c10_focus_landing_test.cs:175-192` 于 2026-10-08 实证)。

### 3.2 六轮试错的教训(如实记录,不掩盖)

| 轮 | 做法 | 结果 | 定性 |
|---|---|---|---|
| run2 | `GetNextFocusable(from, left常量)` 三参 | 编译错 CS1501(实际两参) | 我的 API 误用 |
| run2 语义 | 两参直喂 `left` | 从 btn-1-1 得 btn-0-1(树序) | **我的 API 误用**,不是引擎缺陷 |
| run3 | `GetFocusChangeDirection(ev, from)` | CS1503 两条(参数序反了) | 我的 API 误用 |
| run4/5 | 参数序修正 | 恒返基类 `FocusChangeDirection` | `GetFocusChangeDirection` 对 Left/Right **恒返 none** |
| run5 | 加 `ev.target = from` | 仍恒返基类 | 同上 |
| **run7** | **A/B 对照:同输入两环比较** | **ChildOrder=btn-1-2 vs PositionXY=btn-2-1** | ✅ **找到可验证的形态** |

⚠️ **run2 那次我曾把它写成「判据② 红 …引擎退化为树序」—— 那是误归因,已撤回。**
真相是 `VisualElementFocusRing.GetFocusChangeDirection` 只解析 **Next/Previous 的线性序**,
对 **Left/Right/Up/Down 恒返基类**;几何移动实现在 `FocusController` 内部(够不着)。
把「我走错 API」写成「引擎有缺陷」会错误触发 ADR-013 的自实现焦点算法回退 —— 代价很大。

### 3.3 仍未补的缺口

| 缺口 | 为什么补不了 | 归谁 |
|---|---|---|
| 判据① 双触发检测 | 需「一次真实手柄输入」穿过 `InputSystemUIInputModule → EventSystem → panel`,该链 `-nographics` 下不可达;`SwitchFocusOnEvent` internal | 【桌面】人工腿 |
| 判据② 方向语义(往左是否落到左邻) | 需走默认动作阶段,入口 internal | 【桌面】人工腿 |
| 手感四问 | 主观 | 【桌面】人工腿 |

⇒ **假设 6 不能标「已实测通过」。** 建议标法:**「自动化腿已跑(判据③ ✅ / 判据② 引擎侧前提 ✅),行为面仍 `NOT-RUN`」**。

---

## 4. 对 ADR-013 的影响

**不触发任何回退路径。** 依据:

- ADR-013 §6.6 假设 6 的失败动作是「自实现焦点算法」。**本次未发现引擎缺陷** ——
  判据② 的引擎侧前提(**几何排序在册且生效**)反而被证实了。
- 判据③(无指针可达)**已实测通过**,是该假设里唯一完全闭环的一条。
- 判据①②的行为面是**未测**,不是**不通过**。按「未跑就写未跑」纪律记 `NOT-RUN`。

⇒ **`ADR-013 §6.6` 的「半可信」状态不变**;`S5`(Renderer Feature 兜底)继续休眠。
只有人工腿跑完判据①②且失败,才触发回退评估。

---

## 5. 可复算命令汇总

```bash
# 1. 干净轮(期望 total=5 passed=3 failed=0 inconclusive=2)
cd unity && timeout 900 ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity \
  -batchmode -nographics -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter "DaYiJingCheng.Tests.PlayMode.U1Assump6GamepadFocusSpikeTest" \
  -testResults "$PWD/Logs/u1-assump6-final2.xml" -logFile "$PWD/Logs/u1-assump6-final2.log"

# 2. 判据③ 的证据行(期望 9/9)
grep -o "判据③ 可达性:[0-9]*/[0-9]*" unity/Logs/u1-assump6-final2.xml

# 3. 判据② 的证据行(期望两环落点不同)
grep -o "ChildOrder → [a-z0-9-]*;PositionXY → [a-z0-9-]*" unity/Logs/u1-assump6-final2.xml

# 4. IL 可见性复核(期望 SwitchFocusOnEvent / FocusNextInDirection / Dispatch = assembly)
#    # 脚本见 §3.1 的 dnfile 勘察(临时件,未入库)
```

---

## 6. 附:本件**不**声称的事

- ❌ 不声称「假设 6 已实测通过」—— 判据 ①②的行为面仍 `NOT-RUN`。
- ❌ 不声称「真实手柄按键能驱动焦点」—— 输入链路整链未测。
- ❌ 不声称「UI Toolkit 运行时手柄焦点导航在生产装配下可用」—— 本测用的是**最小夹具**
  (3×3 绝对定位按钮 + 运行期 `PanelSettings`),不是 `InventoryContainer` 真实面板。
- ❌ 不声称判据④ 本轮验过 —— 它是检查卡 `:207` 记的**静态核实**,本件引用而已。
