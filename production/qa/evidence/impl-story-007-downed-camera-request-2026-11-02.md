# 实现原件 —— death-respawn/story-007 29 的档位请求生产载体

> **对象**:`production/epics/death-respawn/story-007-downed-camera-mode-request.md`
> **批次**:`S23-9`(sprint-23 §八收口后追加件第三条;用户 2026-11-02 指令「开工」)
> **截至时刻**:`2026-11-02 04:0x CST`(全量回归 XML `start-time = 2026-11-01 19:53:13Z`)
> **commit**:`fe50da44` + 本批未提交工作树
> **裁定**:本卡**无待决分叉**(R-7.1 是等价提取,R-7.4 是补缺);实现按卡面原样执行

---

## 一、交付物

| 文件 | 变更 |
|---|---|
| `Gameplay.Presentation/DeathTransition/DownedCameraRequester.cs` | **新** —— `a5f766f6` 内联闭包的具名等价提取(R-7.2 四行对照逐条保留:`null` 机位 ⇒ 零请求 / `FirstPerson` ⇒ 零请求 / `true` ⇒ `(Downed, 29)` / `false` ⇒ `(Explore, 29)`) |
| `Gameplay.Boot/BootRoot.cs` | 构造 `_downedCameraRequester` 并作为 `deathPresenter.Bind(...)` 的**第 4 实参**;新增实例字段 `_downedCameraRequester`(PlayMode 观测面) |
| `Tests/EditMode/DeathRespawn/downed_camera_requester_test.cs` | **新**,9 测(间谍 `ICameraRig` 记 `(mode, requesterId)` 序列) |
| `Tests/PlayMode/DeathRespawn/downed_camera_wiring_test.cs` | **新**,2 测(真启动序全链 + 呈现器边沿) |
| `docs/architecture/tr-registry.yaml` | 追加 `TR-camera-007`(`status: covered`;总数 **503**) |
| `docs/architecture/traceability-index.md` | 追加对应行;camera 行 6/6 → **7/7**;§8 `13 条` → **14 条**;合计 `502/345` → **503/346** |

---

## 二、AC 逐条记分

| AC | 判定 | 载体 |
|---|---|---|
| **S29-01** `Request(true)` ⇒ 恰一条 `(Downed, 29)` | ✅ | L1 `test_s29_01_downed_requestsDownedWithRequester29` |
| **S29-02** `Request(false)` ⇒ 恰一条 `(Explore, 29)` | ✅ | L1 `test_s29_02_notDowned_requestsExploreWithRequester29` / `test_s29_02_bothDirections_useSameRequester` |
| **S29-03** `FirstPerson` 在位 ⇒ 两个方向各零请求 | ✅ | L1 `test_s29_03_firstPersonInPlace_zeroRequests_bothDirections` + 负控 `test_s29_03_exploreInPlace_stillRequests` |
| **S29-04** 机位缺席 ⇒ 零请求、不抛 | ✅ | L1 `test_s29_04_nullRig_silentNoThrow` / `test_s29_04_nullAccessor_rejectedAtConstruction` |
| **S29-05** `RequesterId == 29` + 门全树零违例 | ✅ | L1 `test_s29_05_requesterId_isTwentyNine_andGateClean` |
| **S29-06** 生产启动序真的接上了 | ✅ | L2 `test_s29_06_productionBoot_wiresDownedCameraRequester`(**真链**,见 §三) |
| **S29-07** 持续倒下只登记一条请求 | ✅ | L2 `test_s29_07_presenterEdge_requestsOnceWhileDowned` |
| **S29-08** `TR-camera-007` 两份登记文件各 ≥ 1 处 | ✅ | L1 `test_s29_08_trCamera007_registeredInRegistryAndIndex` |
| **S29-09** 真机走查(两段式节奏) | 🔴 **`NOT-RUN`** | 无载体;`-batchmode -nographics` 结构性不可判 |

---

## 三、证伪轮(6 条 · 改→跑→还原同一命令内 · SHA-256 逐字节还原 5/5)

| 变异 | 打什么 | 实测 | 形态 |
|---|---|---|---|
| **M1** | `downed ? Downed : Explore` 两档对调 | **4 红**:S29-01、S29-02×2、S29-03_exploreInPlace | **超集** |
| **M2** | 删 `FirstPerson` 早退 | **1 红**:S29-03 | exact |
| **M3** | 删 `rig == null` 守卫 | **1 红**:S29-04(变为抛) | exact |
| **M4** | `RequesterId` 常量 → `-1` | **5 红**:S29-01、S29-02×2、S29-03_exploreInPlace、S29-05 | **超集**,靶点命中 |
| **M5** | `BootRoot` 传给呈现器的第 4 实参 → `null` | **首轮 0 红(假绿)**;修夹具后 **1 红 = S29-06** | exact(见 §四) |
| **M6** | 呈现器删 `_downedRequested` 边沿判据 | **1 红**:S29-07 | exact |

**M1 / M4 是超集,不是 exact** —— 耦合已查清:`test_s29_02_bothDirections` 与
`test_s29_03_exploreInPlace` 都顺带断言了「档位 / 方向本身正确」,故档位对调与系统号错都会带上它们。
它们对各自属性确有判别力,**但不得**记成「单属性判别」。

还原:`cp` 回备份 + `sha256sum` 逐字节比对(5 次 `[RESTORE-OK]`);
变异器为「精确替换恰一次,命中 ≠ 1 即非零退出」,运行前已离线校核 6 条 anchor 各命中 1 次。

---

## 四、🔴 本轮最重要的一条:M5 首轮未转红 —— S29-06 初版是 F1 型假绿

**症状**:M5 把 `BootRoot` 的 `_downedCameraRequester.Request` 换成 `null`(**等于根本没接线**),
PlayMode 两条**全绿**。而 M5 恰恰是 S29-06 存在的唯一理由。

**根因**:初版 S29-06 的判据是
「反射拿 `_downedCameraRequester` → `Assert.IsNotNull` → **直接** `requester.Request(true)` → 断 `rig.Mode == Downed`」。
`null` 只切断了**呈现器到请求器**那条缝,请求器对象与它的机位访问器都还在,直调当然有效。
⇒ 它断言的是「**对象存在**」,不是「**接线存在**」。这是
`.claude/docs/coding-standards.md` §假绿形态登记表的 **F1**(自造断言顶替 AC 语义层):
夹具绿,但它断的不是生产上那条被 AC 指名的路径。

**修法(判据升级,不是加断言)**:S29-06 改为走**真链** ——

```
真 BootRoot 启动序
  → 真 PlayerDied 落真流(_services.EventSink.Append)
  → 呈现器 Update 逐帧投影
  → BootRoot 注入的请求缝(_requestDownedCamera)
  → DownedCameraRequester.Request(true)
  → CameraRig.SetMode(Downed, 29) → CameraModeMachine.Settle
  → 断言 rig.Mode == CameraMode.Downed
```

**复证**:同一变异 M5 重跑 ⇒ **恰 1 红 = `test_s29_06_...`**,SHA-256 逐字节还原。

**为什么值得单列**:这条假绿**只会在证伪轮暴露**。若按「跑绿了就收口」,
`S29-06` 会以 ✅ 记入本卡与 `TR-camera-007`,而 29 的相机接线一旦被改坏仍然没有任何 AC 会红 ——
**缺口 A 原封不动地回来了**,只是这次外面套了一层绿。

---

## 五、回归(三元组 + 等式)

| 套件 | 本批 | 基线(`S23-8` 收口) | 等式 |
|---|---|---|---|
| EditMode | `6509 / 6472 / 0 / 0 / 37` | `6500 / 6463 / 0 / 0 / 37` | `6509 − 6500 = 9 = 9 新 − 0 删` ✅ |
| PlayMode | `281 / 277 / 0 / 2 / 2` | `279 / 275 / 0 / 2 / 2` | `281 − 279 = 2 = 2 新 − 0 删` ✅ |

`@ 2026-11-02 04:0x CST` · 批次 `S23-9` · commit `fe50da44` + 工作树。
(全量 XML `start-time` 为 UTC `2026-11-01 19:53:13Z` = CST `2026-11-02 03:53`。)
PlayMode `exit=2` 伴 `failed=0`,系既有 2 条 `inconclusive`(与基线同形,非新失败)。

**复算**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 -c "
import xml.etree.ElementTree as ET
for f in ['unity/Logs/s29-em-full.xml','unity/Logs/s29-pm-full.xml']:
    r=ET.parse(f).getroot()
    print(f, {k:r.get(k) for k in ('total','passed','failed','inconclusive','skipped')})
"
# 期望:
#   EditMode  total=6509 passed=6472 failed=0 inconclusive=0 skipped=37
#   PlayMode  total=281  passed=277  failed=0 inconclusive=2 skipped=2
```

---

## 六、证明不了的事(禁借绿)

1. **真机观感未走查(S29-09)** —— 两段式的节奏(下沉 1.2 s / 微倾 10°)与 `AC-29-11` 合判
   需要**真机 + 真显示器**;`-batchmode -nographics` 下 `ScreenCapture` / `WaitForEndOfFrame`
   结构性不可用。本卡**不记绿**。
2. **只能证明「意图发到了相机」,不能证明「相机看起来对」** —— L2 断的是 `rig.Mode`,
   即档位机接受了这条意图;转场的视觉表现(相机位移 / 倾斜曲线)不在本卡判据面内。
3. **「该发没发」仍无系统性覆盖门** —— `CameraSetModeRequesterGate` 只判 arity 与字面 `-1`。
   本卡与 `story-010` 各自用**正控**兜住自己那一条(`exploreInPlace_stillRequests` /
   10 侧的同类负控),但那是逐条补的,不是矩阵门。
4. **VR 侧只有意图层保证** —— 「`FirstPerson` 在位时不产生该意图」已证;
   「VR 下倒下/醒来的实际表现」归 P1a,零证据。
5. **Φ3 床格视角(第二段)仍空** —— 承 Out of Scope,归机位轮。
