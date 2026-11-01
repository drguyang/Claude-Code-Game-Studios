# 实现原件 —— casebook/story-006 相机档位意图(`Casebook` / `Explore`,requesterId 39)

> **对象**:`production/epics/casebook/story-006-camera-mode-intent.md`
> (**2026-11-02 由 `story-006-camera-mode-intent-and-writing-anchor.md` 拆分并订正**)
> **批次**:`S23-10`(2026-11-02 用户指令「排进 sprint-23」;由「连 39 的实现一起做完」→「再排进 sprint-23」两步指令共同定位)
> **截至时刻**:`2026-11-02 05:xx CST` · **commit**:`fc5fd0ba` + 本批未提交工作树
> **裁定**:三条由用户 2026-11-02 逐项选定 ——
> ① 档位表真源走独立 `camera-mode-requests.yaml`;② **先补 39 接线,再上矩阵门**;
> ③ 判据 = 动态矩阵 + 静态防绕过。**本卡是第 ② 步。**

---

## 一、为什么会有这张卡

`camera-and-viewpoint.md` 的 `R-2-5` 档位表有 **6 条**有效行,生产侧长期只有 **4 条**有请求点:

```bash
grep -rn "\.SetMode(" unity/Assets --include=*.cs | grep -v /Tests/
#   只有两处:EmergencyChannelDriver.cs(10)· DownedCameraRequester.cs(29)
grep -rn "CameraMode.Casebook" unity/Assets --include=*.cs | grep -v /Tests/
#   三处全在相机内部(优先级表 / 冻结判据),零个「请求」
```

而 `CameraSetModeRequesterGate` **全绿** —— 它只判**语法**(是否双参、`requesterId` 是否字面 `-1`),
**从不看那张表**,所以「表里有、代码里没有」它结构性看不见。这就是「该发没发」。

---

## 二、交付物

| 文件 | 变更 |
|---|---|
| `Gameplay.Presentation/Casebook/CasebookCameraRequester.cs` | **新** —— 与 `DownedCameraRequester`(29)同构;`RequesterId = 39`;VR 早退 + 机位缺席守卫 |
| `Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | `CasebookCameraRequest` 委托缝;`CloseModal` 拆 `CloseModalCore`(不发意图)+ 公开版(发);`OpenModal` 按 **before/after 终态**发一次边沿 |
| `Gameplay.Boot/BootRoot.cs` | 构造 `_casebookCameraRequester` + 赋给驱动请求缝 |
| `Tests/EditMode/CasebookCamera/casebook_camera_requester_test.cs` | **新**,9 条 |
| `Tests/PlayMode/Casebook/casebook_camera_wiring_test.cs` | **新**,2 条 |
| `casebook/story-007-writing-checkpoint-anchor.md` | **新** —— 原卡的落笔 / checkpoint 锚点两件原样移出(未实现) |

---

## 三、AC 记分

| AC | 判定 | 载体 |
|---|---|---|
| **AC-39-07** 开合册意图 + 幂等 | ✅ | L1 `s39_01` / `s39_02` / `s39_02_both` + L2 `S39-06`(真换档)与 `S39-07`(边沿条数) |
| **AC-39-07b** `Casebook` 档唯一请求方 | ✅ | L1 `s39_07b`:源码扫描,同时要求「`.SetMode(` 同行」且「`CameraMode.Casebook`」,命中数须**恰 1** |
| **AC-39-07c** `RequesterId == 39` + 门干净 | ✅ | L1 `s39_05` |
| **AC-39-08** 相机模式零上传 | ✅(既有面) | 本卡零新增序列化类型;判据属禁止面,非新增实现面 |
| **AC-39-09** 摊开期间移动不变 | 🔴 **`NOT-RUN`** | 本卡只发相机意图,不碰移动管线;载体在 `player-controller` 侧。**不借绿** |
| **AC-39-10** 合册零写事件流 | ✅ | L2 `S39-06`(开合全程 `EventCount()` 不变) |

---

## 四、证伪轮(7 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 7/7)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **N1** | 两档对调 | 4 红(S39-01 / 02×2 / 03_exploreInPlace) | **超集** |
| **N2** | 删 `FirstPerson` 早退 | 1 红:S39-03 | exact |
| **N3** | 删 `rig == null` 守卫 | 1 红:S39-04 | exact |
| **N4** | `RequesterId → -1` | 5 红(含 S39-05) | **超集**,靶点命中 |
| **N5** | `BootRoot` 绑定实参 → `null` | 1 红:S39-06 | exact |
| **N6** | 换屏路径改回会发意图的 `CloseModal()` | 1 红:S39-07 | exact |
| **N7** | 删同态判据 | 1 红:S39-07 | exact |

**N6 是本轮的方法论收获**:`CloseModalCore` 的拆分看起来是「无害的写法整理」,
但把 `OpenModal` 内部的调用换回公开版后,「已经在脉案时再开一次脉案」会冒出
`[false, true]` 两条中间态意图 —— 证伪轮把它钉住了。**没有 N6,这次重构就是不可证伪的。**

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`story-007` 收口) | 等式 |
|---|---|---|---|
| EditMode | `6518 / 6481 / 0 / 0 / 37` | `6509 / 6472 / 0 / 0 / 37` | `6518 − 6509 = 9 = 9 新 − 0 删` ✅ |
| PlayMode | `283 / 279 / 0 / 2 / 2` | `281 / 277 / 0 / 2 / 2` | `283 − 281 = 2 = 2 新 − 0 删` ✅ |

`@ 2026-11-02 05:xx CST` · commit `fc5fd0ba` + 工作树。
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形,非新失败)。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/c39-em-full.xml','unity/Logs/c39-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EditMode total=6518 passed=6481 failed=0;PlayMode total=283 passed=279 failed=0
```

---

## 六、证明不了的事(禁借绿)

1. **`AC-39-09`(摊开期间移动不变)`NOT-RUN`** —— 本卡只碰相机意图,移动管线零改动;
   该 AC 的载体重叠在 `player-controller` 侧,本卡不代它记绿。
2. ~~**🟡 启动态不一致未裁**~~ **✅ 已结(2026-11-02「修 B-1」轮)—— 裁定见 §七**(原文保留):
   启动时脉案屏在屏上(`_screenName` 默认 `"Casebook"`),
   而相机停在 `Explore`。要不要在绑定期补一次初始同步,是产品裁定;
   **两种读法下现有夹具都绿** ⇒ 本条**没有**任何断言背书,不得当作「已确认正确」。
   → **§七**:裁定 = 取消启动占位(设计权威 `casebook-39.md:57`「立案即开册」),
   启动形态改为无常驻屏;夹具前置断言随之由 `Casebook` 改为 `None`,**该断言现在是真的了**。
3. **「相机看起来对」未证** —— L2 断的是 `CameraRig.Mode`(档位机接受了意图),
   近景档的实际取景 / 转场曲线归 `camera-viewpoint` 的机位轮,零证据。
4. **矩阵门仍未存在** —— 本卡把 39 的两行补上,但「表 ↔ 代码 ↔ 行为」的系统性判据
   仍未落地(`camera-viewpoint/story-007` 已立件,未开工)。
   ⚠️ **换句话说:本卡修的是一个实例,不是那一类。**
5. **测试间泄漏面已发现但未系统治理** —— `SkeuoUI` 不随 `BootRoot` 销毁,
   本卡在自己的夹具里显式拆;其他 PlayMode 夹具若依赖启动屏形态,**仍可能互相污染**。
   ✅ **2026-11-02 已登记为 `TD-skeuo-02`**(`docs/tech-debt-register.md`;
   `P0 面 = N` / `R4`)。本夹具的拆除是**治标**,该条的「何时消解」明写「仅在本夹具里拆不算消解」。

---

## 七、2026-11-02 追加:「修 B-1」轮 —— 启动占位已撤(§六 第 2 条结案)

> **触发**:sprint-23 收尾清账的 B 组第 1 项「启动档位未裁」。
> **用户裁定**:「连启动占位一起改掉」。
> **状态**:§六 第 2 条(🟡 启动态不一致未裁)**自此结案**;原文保留在 §六,不改写。

### 7.1 裁定依据 = 设计权威,不是产品口味

| 出处 | 原文 |
|---|---|
| `design/ux/casebook-39.md:57` | 「世界空间对病人裸 `Interact` = **就诊/立案**(单义,路由 37,`S-8.4` 表)→ **立案即开册**」 |
| `design/ux/casebook-39.md:224` | 「开册(就诊) \| 世界空间裸 `Interact` 打到病人 \| 同一 action 资产 \| 档位转场(§9) \| **37**(`CaseOpened`,立案时刻 = 该 tick)」 |

⇒ 脉案屏是**玩家动作的结果**,不是启动常驻屏。§六 第 2 条争的「读法 A(启动即脉案 ⇒ 该在 `Casebook` 档)」
其前提在设计上**不存在**。故裁定 = **取消启动占位**,启动形态 = **无常驻屏**(`ModalId.None`),档位 = `Explore`。

### 7.2 落地(三处,同一批;⚠️ 生产那一处在 `Boot.unity`,不在代码)

| # | 文件 | 变更 |
|---|---|---|
| ① | `Assets/Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | `_screenName` 默认 `"Casebook"` → **`""`**;`Awake` 的空名分支**不再早退**(建成「有面板、无屏」的合法形态 —— 早退会跳过焦点环施加者与注入面补绑) |
| ② | **`Assets/Scenes/Boot.unity:375`** | `_screenName: Casebook` → **空**(Unity 惯例写法 `_screenName: `)。**这才是生产生效的那一处** —— 只改代码默认值等于没改,序列化值会覆盖它 |
| ③ | 两件 L2 夹具 | `casebook_camera_wiring_test.cs` / `camera_mode_request_matrix_test.cs`:删掉「boot → `CloseModal()`」预热(启动本就是 `None`,留着会让夹具读起来像「启动态是 Casebook」);前置断言由 `Casebook` 改为 **`None`**(这句现在是**真断言**:占位若被谁加回来会当场转红) |
| ④ | `Tests/EditMode/Boot/modal_opener_test.cs` | 两条断言 `Casebook` → `None`;`test_modalGateState_restingScreen_doesNotBlockPlay` 改为**自建**带常驻屏的驱动(`SetPrivateField(driver, "_screenName", "Casebook")`)—— 该用例测的是「常驻屏不挡玩」,**不能**随默认值一起消失 |
| ⑤ | `Tests/PlayMode/main_menu_wiring_test.cs` | `:137` 前置由「场景级屏替换常驻屏」改为**先开一次脉案模态**再断言两个容器互不替换 |

**相机侧零新增代码**:启动态 = `None` ⇒ 39 的边沿判据 `before == after == None` ⇒ 不发请求
⇒ 档位维持 `Explore`。读法 B 的结论没变,**但它现在有断言背书了**(⑦③ 的前置断言)。

### 7.3 回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-11`) | 等式 |
|---|---|---|---|
| EditMode | `6523 / 6486 / 0 / 0 / 37` | `6523 / 6486 / 0 / 0 / 37` | `+0 = 0 新 − 0 删` ✅ |
| PlayMode | `290 / 286 / 0 / 2 / 2` | `290 / 286 / 0 / 2 / 2` | `+0 = 0 新 − 0 删` ✅ |

`@ 2026-11-02 06:45 CST`(EM `06:42–06:44` / PM `06:44–06:45`;场景改动前另跑过一轮 `06:31–06:34`,
两次数字逐位相同)· 批次 `S23-11-B1` · commit `2ffb6b3b` + 工作树。
**数字与基线逐位相同** = 本轮的判据全部落在**既有夹具的断言改动**上,零新增用例;
场景改动(`Boot.unity`)与其后的注释订正(`SkeuoRuntimeDriver` / `ModalGateStateAdapter`)各复跑一轮,
三次数值一致。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/b1-em-full.xml','unity/Logs/b1-pm-full.xml','unity/Logs/b1-em-scene.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望: EM 6523/6486/0/0/37 · PM 290/286/0/2/2 · EM(场景改动后)6523/6486/0/0/37
```

### 7.4 🔴 连带暴露:「脉案屏在生产上打不开」(不在本卡范围,已立债)

撤占位之后才看得清 —— **生产侧没有任何代码 `OpenModal(ModalId.Casebook)`**:

```bash
grep -rn "\.OpenModal(" unity/Assets --include=*.cs | grep -v /Tests/
# ⇒ ModalInputDriver.cs:93(Pause)· DriverPauseCommandSink.cs:41(SaveSlots)。Casebook 零命中。
```

且「立案」今日由 `CaseOpenedDriver.OnTickEdge` **每 tick 自动**对在场病人开案,
与 `case-system.md:176`「立案是一次玩家动作」/ `:927`「玩家没去 ⇒ 不立案」**方向相反**。

⇒ **登记为 `TD-casebook-01`**(`docs/tech-debt-register.md`,`P0 面 = Y` / `R1`)。
⚠️ 该债**先于**本轮存在;B-1 没有制造它,但把它从「隐性」变成「玩家可见的缺失」。
⚠️ **不得**因为有该债而回填启动占位 —— 占位与设计权威(7.1)直接冲突。
