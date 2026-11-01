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
2. **🟡 启动态不一致未裁** —— 启动时脉案屏在屏上(`_screenName` 默认 `"Casebook"`),
   而相机停在 `Explore`。要不要在绑定期补一次初始同步,是产品裁定;
   **两种读法下现有夹具都绿** ⇒ 本条**没有**任何断言背书,不得当作「已确认正确」。
3. **「相机看起来对」未证** —— L2 断的是 `CameraRig.Mode`(档位机接受了意图),
   近景档的实际取景 / 转场曲线归 `camera-viewpoint` 的机位轮,零证据。
4. **矩阵门仍未存在** —— 本卡把 39 的两行补上,但「表 ↔ 代码 ↔ 行为」的系统性判据
   仍未落地(`camera-viewpoint/story-007` 已立件,未开工)。
   ⚠️ **换句话说:本卡修的是一个实例,不是那一类。**
5. **测试间泄漏面已发现但未系统治理** —— `SkeuoUI` 不随 `BootRoot` 销毁,
   本卡在自己的夹具里显式拆;其他 PlayMode 夹具若依赖启动屏形态,**仍可能互相污染**。
   未登记为债,如需要须另立。
