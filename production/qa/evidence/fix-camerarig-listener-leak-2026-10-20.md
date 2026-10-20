# 修复原件 · CameraRig 泄漏致 AudioListener 单挂点断言全量 7 红

**日期**:2026-10-20(收口轮)
**触发**:sprint-15 收口重跑全量 PlayMode(中途闸 `smoke-2026-10-20.md` 自身写明「终值 smoke 须在收口时重跑」)
**结论**:两处缺陷,**均为本轮修复 AudioListener 时引入或暴露**;两处修复各自被变异反证钉住。

| # | 缺陷 | 引入者 | 状态 |
|---|---|---|---|
| A | `CameraRig` 是根级对象,`BootRoot.OnDestroy` 从不销毁 ⇒ 每个 PlayMode 测试残留一个带 listener 的相机 | **本轮**(`3b97a85` 把 listener 迁到它身上;泄漏本身是既有) | ✅ 已修 + 反证 M1 |
| B | 预摆相机摘 listener 用 `GameObject.Find`(**只找 active**)⇒ 预摆若已 inactive,listener 摘不掉 | **本轮**(`3b97a85` 新增的摘除逻辑) | ✅ 已修 + 反证 M2 |

---

## 一、原判定(缺陷 A:相机根对象泄漏)

### 1.1 现象

收口轮首跑全量 PlayMode:

| 套件 | 结果 | 产物 |
|---|---|---|
| EditMode | `3945 / 3909 / 0 / 36`(**全绿**) | `unity/Logs/s15-final-editmode.xml` |
| PlayMode | `197 / 190 / **7** / 0` | `unity/Logs/s15-final-playmode.xml` |

7 条红**全部同一条消息**,且计数**逐测递增**:

```
CompositionRootSmokeTest            active 2 个 / 挂载总数 2 个
EmergencyAttemptWiringTest ①        active 3 个 / 挂载总数 3 个
EmergencyAttemptWiringTest ②        active 4 个 / 挂载总数 4 个
FallResetWiringTest                 active 5 个 / 挂载总数 5 个
PatientAppearedWiringTest           active 6 个 / 挂载总数 6 个
PatientVisualWiringTest             active 7 个 / 挂载总数 7 个
SkeuomorphicUI.ui_input_module      active 7 个 / 挂载总数 8 个
```

⇒ **每个跑过启动序的测试留下一个 listener** —— 这是累积,不是某一条测试的局部错误。

### 1.2 根因

`BootRoot.cs:320`(修前):

```csharp
var cameraObj = new GameObject("CameraRig");      // ← 根级,未归 BootRoot 层级
_cameraRig = cameraObj.AddComponent<CameraRig>();
cameraObj.AddComponent<UnityEngine.Camera>();
cameraObj.AddComponent<UnityEngine.AudioListener>();
```

`new GameObject` 建的对象**不挂在 `BootRoot` 下** ⇒ `BootRoot.OnDestroy`(遍历的只有自身的
`_services` / `_player` / `_eventSystem` 等引用)**不会销毁它**。测试拆卸里虽有
`GameObject.Find("CameraRig") → Destroy`,但那要**各测试自己记得写**;7 条红的测试都没写。

**为什么修前不红**:修前运行时相机**没有 listener**(这正是「零听音点全场景静音」那个缺陷),
泄漏的相机是空壳,不参与 listener 计数。本轮把 listener 迁到它身上之后,
**每个泄漏件都变成一个 listener** ⇒ 断言开始响。

**为什么单件跑不红**:`--filter` 单跑时每条的基数都是 0,启动序建 1 个 = 1,断言通过。
**这是「单件全绿 ≠ 全量全绿」的一次实证**,也是 smoke 值必须重跑的直接理由。

### 1.3 修复

```csharp
var cameraObj = new GameObject("CameraRig");
cameraObj.transform.SetParent(transform, worldPositionStays: false);   // ← 归 BootRoot
```

与同文件 `EnsureEventSystem`(`:430`)的既有纪律同款(那里已写明「reparent 后随 BootRoot
一同销毁 ⇒ 免测试间泄漏」,`CameraRig` 是同一处遗漏)。同时按 ADR-023 §⑤ 解决另一面:
不归 Boot 则 Phase 2 卸载 World 会带走相机。

**相机定位不受影响**:`Update` 里是 `cam.transform.position = ...`(世界坐标赋值),
reparent 不改世界坐标语义;`CameraRig` 自身不读 `transform.parent`(全文件无该引用)。

`OnDestroy` 补 `_cameraRig = null` 引用清理,并注明**不显式 Destroy**(随层级走;
若顺序在前会造出本帧「零听音点」窗口)。

---

## 二、原判定(缺陷 B:摘 listener 用 active-only Find)

### 2.1 现象

修完 A 之后,7 红降到 **1** 红:

```
test_bootSequence_installsSingleEventSystemWithWiredActions
  AC-20-10:active 1 个 / 挂载总数 2 个
```

`active = 1` **对了**(听音点唯一),但**挂载总数 2** —— 多出一个 inactive listener。

### 2.2 根因

```csharp
var presetCamera = GameObject.Find("Main Camera");   // ← 只找 active 对象
if (presetCamera != null) { /* 摘 listener */ presetCamera.SetActive(false); }
```

`GameObject.Find` **不返回 inactive 对象**。全量串跑的实际路径:

1. `camera_follow_wiring_test` 自建预摆 `Main Camera`(带 listener);
2. 启动序把它 `SetActive(false)` 并摘掉 listener;
3. 该测拆卸里用 `GameObject.Find("Main Camera")` 想恢复它 —— **同一处死代码,Find 找不到**;
4. 后续测试经 `FindPresetCamera()`(**含 inactive**)复用它,发现「没有 listener」就补一个
   (`ui_input_module_wiring_test:168`),此时它仍 **inactive**;
5. 启动序再跑 `GameObject.Find("Main Camera")` ⇒ 仍是 inactive ⇒ **整个摘除分支跳过**
   ⇒ 那个 inactive listener 永久留下 ⇒ 与运行时相机并成 **2 个挂载**。

⇒ 本节的**义务**是「无论预摆相机处于什么状态,都不许它与运行时相机并存 listener」,
而 `Find` 的语义只覆盖 active 一半。**判据比失效面窄** —— 与 `MuteCaptureFloorDb = -80`
擦边漏过 −74 dB 同型(见 `fix-listener-and-magenta-2026-10-20.md` §6.4)。

### 2.3 修复

新增 `FindInactiveInclusive(name)`(`FindObjectsByType<Camera>(Include)`)并改用它。

---

## 三、验证

### 3.1 基线溯源三元组(强制)

| 套件 | 三元组 | 结果 |
|---|---|---|
| EditMode(修后) | `2026-10-20 01:09 UTC` · 收口轮全量 · `3f6c084`(工作树含本轮改动) | **`3945 / 3909 / 0 / 36`** |
| PlayMode(修前) | `2026-10-20 01:05 UTC` · 收口轮全量 · 同上 | `197 / 190 / **7** / 0` |
| PlayMode(修后) | `2026-10-20 01:09 UTC` · 收口轮全量 · 同上 | **`197 / 197 / 0 / 0`** |

产物:`unity/Logs/s15-fix-editmode.xml` · `s15-final-playmode.xml` · `s15-fix2-camerarig-playmode.xml`。

**EditMode 修前修后同值**(`3945/3909/0/36`)⇒ 改动**未触碰 EditMode 面**,零回归。

### 3.2 反证轮(逐条,均为「备份 → 单点变异 → 跑 → cp 回 → SHA 比对」)

| # | 变异 | 结果 | 转红项 |
|---|---|---|---|
| **M1** | 删 `SetParent`(恢复泄漏形态) | **7 红 / 190 绿** | 与事故**完全同型**的 7 条:`compositionRoot_smoke` · `emergency_attempt`×2 · `fall_reset` · `patient_appeared` · `patient_visual` · `ui_input_module` |
| **M2** | `FindInactiveInclusive` → `GameObject.Find`(恢复 active-only) | **1 红 / 196 绿** | `test_bootSequence_installsSingleEventSystemWithWiredActions`,报文 `active 1 / 总数 2` |

还原核对:`sha256sum` 前 16 位 `c5397e6b6351df41`,两次变异后均回到同一值(逐次打印 `一致=YES`)。

⇒ 两条修复**各自**被钉住,不是互相兜底:
- M1 只撤 A ⇒ B 的修复仍在,但 7 条红照样复现 ⇒ A 不是被 B 掩盖的;
- M2 只撤 B ⇒ A 的修复仍在,而 1 条红复现 ⇒ B 独立可判别。

---

## 四、这一轮的元教训(如实登记)

1. **我在 `3b97a85` 修「零听音点」时,没有跑全量 PlayMode。** 当时只跑了 4 条定向件
   (`CameraFollow` / `AudioWiring` / `ui_input_module` / `PatientVisual`),各自单跑全绿。
   **单跑绿对「跨测试累积」这一类缺陷零判别力** —— 这是本轮的实质失误,不是运气问题。
2. **修复一处静音,引入了一处泄漏。** 把一个「不计数」的对象变成「计数」的对象时,
   必须同时检查**该对象的生命周期是否已被正确管理**。`CameraRig` 从「无 listener 的空壳」
   升级为「唯一听音点持有者」,其所有权义务随之升级 —— 当时只改了 `AddComponent`,
   没动归属。
3. **断言口径与失效面要对齐。** 缺陷 B 的形态是「判据 `Find` 只覆盖 active,
   而失效面含 inactive」。这与今晨 `MuteCaptureFloorDb = -80` 漏过 −74 dB 是**同一类错误**
   (阈值/谓词边界比失效边界窄),一天内出现两次 ⇒ 建议作为检查项:写任何守卫时,
   先问「失效集合里有没有落在谓词之外的元素」。

---

## 五、未解面(不得因本件而读作已解决)

- **本件不改任何 AC 的判定状态**,不新增 `[L]` 通过。
- `TD-listen-01` 的 **L-1…L-5 一个都没动**;四份听测协议仍全部 NOT-RUN。
- `AC-019-6`(10-c)已按用户裁定移出 story-019 ⇒ 转 `TD-listen-01`,**仍 NOT-RUN**。
- 测试侧的 `GameObject.Find("Main Camera")` 死代码(`camera_follow_wiring_test` 拆卸)
  **未改** —— 生产侧已改为不依赖它,但该测试的「恢复预摆相机」意图**仍然无效**。
  归测试基建清理轮,本件不动(改它需要重跑全量 PlayMode 确认无连带)。

**未提交**(不经用户指令不 commit)。
