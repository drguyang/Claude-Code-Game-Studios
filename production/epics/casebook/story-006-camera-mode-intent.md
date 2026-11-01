# Story 006: 相机档位意图(`Casebook` / `Explore`,requesterId 39)

> **Epic**: 脉案
> **Status**: **Complete ✅ 2026-11-02(COMPLETE WITH NOTES)** —— **6 条 AC 全证**
> (🔁 原文写「4 条」,与下方 `## Acceptance Criteria` 的 6 个复选框及 §Completion Notes
> 的「6/6」不符,2026-11-02 订正;复算:`grep -c '^- \[x\] \*\*AC-39' <本文件>` ⇒ 6);
> 🔁 **开放问题已于 2026-11-02 裁结**(「修 B-1」轮)—— 裁定 = **取消启动占位**,启动形态改为
> **无常驻屏**(`ModalId.None`),相机维持 `Explore`。见 §未闭 / 开放问题 第 1 条。
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: 2h(拆分后;原整卡 4h,落笔与锚点半已移出至 story-007)
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-02

> **🔁 2026-11-02 拆分与订正(本卡由 `story-006-camera-mode-intent-and-writing-anchor.md` 而来)**
>
> **为什么拆**:原卡捆了三件事 —— 相机意图 / 落笔写流 / 7a checkpoint 锚点。
> 前一件与后两件**无共享判据**,而后者今日仍是空的(`SaveAnchor` 枚举全仓零命中)。
> 捆在一起 ⇒ 本卡永远等不到 `Complete`,任何以「39 的档位已接好」为前提的门(如档位请求覆盖矩阵)
> 也就永远拿不到一个可满足的依赖。⇒ 按「一卡一收口」拆:
> **006 = 相机意图**(本卡)· **007 = 落笔与 checkpoint 锚点**(新立,内容原样移出,未实现)。
>
> **三处订正(原卡写于 2026-09-28,早于 `ADR-020 Amendment C` 2026-11-01)**:
>
> | # | 原文 | 订正后 | 为什么必须改 |
> |---|---|---|---|
> | ① | `ICameraRig.RequestMode(CameraMode.Casebook)` | `ICameraRig.SetMode(CameraMode.Casebook, requesterId: 39)` | **`RequestMode` 这个方法不存在**。实际接口只有 `SetMode(mode)` 与 `SetMode(mode, int requesterId)` |
> | ② | 全文零处 `requesterId` | 生产调用**必须**双参且传 39 | 甲案「同一请求方可降档」靠 `RequesterId` 认出「谁开的档」;不传 ⇒ 39 开的 `Casebook` 连自己都**退不掉**(静默失效)。且单参调用会被 `CameraSetModeRequesterGate` 判红 |
> | ③ | 「39 是**两档**意图**唯一**请求方 —— `SetMode(Casebook/Explore)` 的调用点仅存在于 39」 | 「39 是 **`Casebook` 档**的唯一请求方;`Explore` **三方可发**(10 / 29 / 39),由 `requesterId` 区分」 | 原断言**现已恒假**:`Explore` 由 10(`EmergencyChannelDriver.cs`)与 29(`DownedCameraRequester.cs`)各发一条。按原文实现,这条 AC 要么永远红、要么永远被 ignore |
>
> 原文保留于 git 历史(`git log --follow`),不在此处逐字重贴。

---

## Context

**GDD**: `design/gdd/casebook.md`(规则五 摊开与相机 · 边界表「39 = `Casebook` 档唯一请求方」)
· `design/gdd/camera-and-viewpoint.md` `R-2-5` 档位表(39 两行)

**Requirement**: **`TR-casebook-007`**(脉案近景经 `ICameraRig.SetMode`,不新增相机状态持有者)
*(Requirement 正文以 `docs/architecture/tr-registry.yaml` 为准,复核时现读)*

**ADR Governing Implementation**:
- **ADR-020 §五**(主):相机**只读不持状态** —— 39 只能发意图,不得读回 `Mode` 做判定
- **ADR-020 Amendment C ①②**:`RequesterId` **参与裁决**(甲案);生产调用方**必须**双参且传真实系统号
- **ADR-013 §9 C3**:呈现层只渲染
- **ADR-013 §十**:`ModalId.Casebook` 是闭集成员(本卡只消费,不增员)

**ADR Decision Summary**:`OpenCasebook ⇒ SetMode(Casebook, 39)`、`Close ⇒ SetMode(Explore, 39)`;
39 是 **`Casebook` 档**的唯一请求方;相机模式是本地表现态,**永不上传**(不进三流、不进网络)。
移动不抑制已裁定(`OQ-39-4` C 路:摊开册子不禁移动 —— 本卡验证「相机换档不改移动管线」)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# 回调与接口调用,零 post-cutoff API。

**Control Manifest Rules (this layer)**:
- **Required**: 双参 `SetMode(mode, requesterId)` 且传真实系统号(39);开 / 合册各**恰一条**意图(边沿)
- **Forbidden**: 39 持有相机状态 / 直接操作 `Camera` transform;相机模式进网络包;开册强制禁移动;
  在「关旧屏 → 开新屏」的中间态发多余的 `Explore`
- **Guardrail**: 每次开 / 合册的档位转换次数 = 1;P0 无「半开册」中间档

---

## Acceptance Criteria

- [x] **AC-39-07**:开册 ⇒ `SetMode(Casebook, 39)`、合册 ⇒ `SetMode(Explore, 39)`;
      重复请求同档幂等;一次开合全程档位转换恰 1+1 次
- [x] **AC-39-07b(所有权)**:39 是 **`Casebook` 档**的唯一请求方 ——
      生产侧 `SetMode(CameraMode.Casebook, …)` 的调用点**仅存在于 39 的载体**
      (`Explore` 不在本条判据面内,见订正 ③)
- [x] **AC-39-07c(系统号)**:`CasebookCameraRequester.RequesterId == 39`,且
      `CameraSetModeRequesterGate` 全树零违例
- [x] **AC-39-08**:相机模式字段**不出现在**任何网络序列化类型(反射扫描;P0 联机面 = 不传)
- [x] **AC-39-09**:摊开脉案期间玩家移动管线不变(角色可走动;`OQ-39-4` C 路回归验证)
- [x] **AC-39-10**:合册 = 零副作用:不写事件流

---

## Implementation Notes

1. **请求载体 = 具名类,不是内联 lambda**:`Gameplay.Presentation/Casebook/CasebookCameraRequester.cs`,
   与 `DownedCameraRequester`(29)同构 —— 只发意图、不持相机、`RequesterId` 写死为 `const`。
   ⚠️ 住 `Gameplay.Presentation` 而非装配层:`story-011` 的 `AC-1-23` 门已按**程序集**把
   `Gameplay.Boot` 判红过一次,相机关联逻辑不应再塞回装配层。

2. **接缝 = `SkeuoRuntimeDriver` 的模态边沿**,不是 `CasebookScreen` 内部:
   开 / 合册的唯一入口是 `OpenModal` / `CloseModal`,屏的构造与销毁都由此驱动。
   ⚠️ **`OpenModal` 内部会先 `CloseModal()`** ⇒ 若在 `CloseModal` 里无条件发 `Explore`,
   「脉案 → 库存」会多发一条 `Explore`(中间态),违反 AC-39-07 的「转换恰 1 次」。
   处置:拆 `CloseModalCore()`(不发意图)+ `CloseModal()`(发),`OpenModal` 只调 Core,
   末尾按 **before/after 终态**发一次边沿。

3. **边沿判据**:`before == Casebook` 与 `after == Casebook` 不等时发一条
   (`true ⇒ Casebook`,`false ⇒ Explore`)。同态重复开合 ⇒ 零请求(幂等)。

4. **VR 早退**:`rig.Mode == FirstPerson` ⇒ 零请求。`FirstPerson` 是头显驱动、不经本链;
   平面两段式与近景档均不得平移到 VR(与 `DownedCameraRequester` 同款,承 `ADR-020 §六`)。
   ⚠️ 本条是**本卡就地裁定**(原卡未写),未经用户逐条确认 —— 标为可改。

5. **后置绑定**:`BootRoot` 装配段 `_uiDriver.CasebookCameraRequest = new CasebookCameraRequester(() => _cameraRig).Request;`
   (`_cameraRig` 私有、且拆卸段会置 null ⇒ 必须 `Func` 懒求值 + 判空)。

---

## Out of Scope

- **落笔写流与 7a checkpoint 锚点** —— 已移出至 `story-007`(原卡内容原样转移,未实现)
- **联机落笔 / `Seq` 上行** —— `TR-casebook-004` 归 45 轮
- **`ModalId` 闭集增员** —— 归 `ADR-013 §十`
- **`Casebook` 档的机位参数与转场曲线** —— 归 2(`camera-viewpoint`);本卡只保证意图发出

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- L1 `unity/Assets/Tests/EditMode/Casebook/casebook_camera_requester_test.cs`(间谍 `ICameraRig`)
- L2 `unity/Assets/Tests/PlayMode/Casebook/casebook_camera_wiring_test.cs`(真 `SkeuoRuntimeDriver`
  的 `OpenModal` / `CloseModal` 全链)

---

## Dependencies

- **形状依赖**:`ICameraRig` / `CameraMode`(`camera-viewpoint/story-005` + `006`,`Complete ✅`)
- **实现依赖**:`SkeuoRuntimeDriver` 的模态开关(既有,已在生产)
- **ADR-020 Amendment C**:`Accepted 2026-11-01`

---

## Completion Notes

**Completed**: 2026-11-02
**Criteria**: 6/6 已断言(AC-39-08 网络面为反射扫描,见下)
**Deviations**: 无
**Test Evidence**:
- L1 `unity/Assets/Tests/EditMode/CasebookCamera/casebook_camera_requester_test.cs`(9 条)
- L2 `unity/Assets/Tests/PlayMode/Casebook/casebook_camera_wiring_test.cs`(2 条)
- 原件 `production/qa/evidence/impl-casebook-006-camera-mode-intent-2026-11-02.md`
**Code Review**: `lean` 模式 —— 生产改动 3 件,走查结论见证据件 §四

### 交付物

| 文件 | 变更 |
|---|---|
| `Gameplay.Presentation/Casebook/CasebookCameraRequester.cs` | **新** —— 与 `DownedCameraRequester`(29)同构;`RequesterId = 39`;VR 早退 + 机位缺席守卫 |
| `Gameplay.UI/Skeuomorphic/SkeuoRuntimeDriver.cs` | 新增 `CasebookCameraRequest` 委托缝;`CloseModal` 拆 `CloseModalCore`(不发意图)+ 公开版(发);`OpenModal` 按 **before/after 终态**发一次边沿 |
| `Gameplay.Boot/BootRoot.cs` | 构造 `_casebookCameraRequester`(观测面)+ 赋值给驱动的请求缝 |
| `Tests/EditMode/CasebookCamera/casebook_camera_requester_test.cs` | **新**,9 条 |
| `Tests/PlayMode/Casebook/casebook_camera_wiring_test.cs` | **新**,2 条 |

### 实现期发现与就地处置

1. **`CloseModalCore` 的拆分是必需的,不是洁癖**:`OpenModal` 内部会先关旧屏。
   若换屏路径走会发意图的 `CloseModal()`,「已经在脉案时再开一次脉案」会冒出
   `[false, true]` 两条中间态意图。证伪轮 **N6**(把 `CloseModalCore()` 换回 `CloseModal()`)
   恰好打红 `S39-07` —— 这条拆分有判别力,不是无害重构。

2. **🔴 启动形态:脉案屏是常驻屏**(`SkeuoRuntimeDriver._screenName` 默认 `"Casebook"`,
   `Awake` 里 `_currentModal = Casebook`,`_modalOpenedExplicitly = false`)。
   初版 L2 夹具写的是「boot → `OpenModal(Casebook)`」,**那是同态换屏,按边沿判据正确地不产生意图**
   ⇒ 夹具红了一次。已改为「boot → `CloseModal()` → `OpenModal(Casebook)`」,
   并把「启动态 = 常驻脉案屏」写成显式前置断言(前置不成立时夹具会立刻点名,而不是悄悄空转)。

   **🔁 2026-11-02 追加(「修 B-1」轮,原文保留)**:该「常驻屏」**本身已被裁定取消**
   (依据见 §未闭 / 开放问题 第 1 条)。⇒ 本段描述的是**当时**的形态,已不是今日形态:
   - 上述「boot → `CloseModal()` → `OpenModal(Casebook)`」的**预热**已从两件 L2 夹具里删除
     (`casebook_camera_wiring_test.cs` / `camera_mode_request_matrix_test.cs`)——
     启动态本就是 `None`,再 `CloseModal()` 是空操作,留着会让夹具读起来像「启动态是 Casebook」。
   - 前置断言相应由 `Casebook` 改为 **`ModalId.None`**(这句现在是**真断言**:
     它钉住了「启动无常驻屏」,占位若被谁加回来会当场转红)。

3. **`SkeuoUI` 不随 `BootRoot` 销毁** —— `FindOrCreateSkeuoUi` 用 `new GameObject("SkeuoUI")`
   落当前场景,且会 `FindFirstObjectByType` **复用**已有实例 ⇒ 上一件 PlayMode 夹具的驱动
   (含 `_currentModal`)会原样活到下一件。本轮实测:`S39-06` 的 TearDown 把模态关成 `None` 之后,
   `S39-07` 复用同一实例 ⇒ 前置断言变 `None`,红了一次。
   ⇒ 夹具在 `Boot()` 与 `TearDown` 两处都显式拆 `SkeuoUI`(治标)。
   ✅ **已登记为债 `TD-skeuo-02`**(2026-11-02):治本要走 `BootRoot` 的归属或复位(三条方向见该条),
   **仅在本夹具里拆不算消解**。

### 证伪轮(7 条 · SHA-256 逐字节还原 7/7)

| 变异 | 打什么 | 实测红 | 形态 |
|---|---|---|---|
| **N1** | `open ? Casebook : Explore` 两档对调 | 4 红:S39-01、S39-02×2、S39-03_exploreInPlace | **超集** |
| **N2** | 删 `FirstPerson` 早退 | 1 红:S39-03 | exact |
| **N3** | 删 `rig == null` 守卫 | 1 红:S39-04 | exact |
| **N4** | `RequesterId` → `-1` | 5 红:含 S39-05 | **超集**,靶点命中 |
| **N5** | `BootRoot` 的绑定实参 → `null` | 1 红:S39-06 | exact |
| **N6** | 换屏路径改用会发意图的 `CloseModal()` | 1 红:S39-07 | exact |
| **N7** | 删 `wasCasebook == nowCasebook` 同态判据 | 1 红:S39-07 | exact |

**N1 / N4 是超集不是 exact**:`S39-02_bothDirections` 与 `S39-03_exploreInPlace` 顺带断言了
「档位 / 方向本身正确」,故档位对调与系统号错都会带上它们。对各自属性有判别力,
**但不得**记成「单属性判别」。

### 未闭 / 开放问题(禁借绿)

1. ~~**🟡 开放问题(未裁):启动时脉案屏在屏上,而相机停在 `Explore`。**~~
   **✅ 已裁(2026-11-02「修 B-1」轮)—— 裁定 = 取消启动占位,取「无常驻屏」。**
   下文为原状描述,**原文保留**:

   要不要在绑定期补一次**初始同步**(让启动即 `Casebook` 档)?
   这是**产品裁定**,本卡不发明。两种读法都站得住:
   - 读法 A:常驻屏 = 玩家一进来就在看脉案 ⇒ 相机该在 `Casebook`;
   - 读法 B:`_modalOpenedExplicitly = false` 明写「这不是玩家摊开的模态」⇒ 相机该在世界(`Explore`)。
   现状取 B(未改),**但没有任何断言为它背书** —— 两读法都绿。

   **裁定依据(设计权威,非本卡自造)**:`design/ux/casebook-39.md:57`
   「世界空间对病人裸 `Interact` = **就诊/立案**(单义,路由 37,`S-8.4` 表)→ **立案即开册**」·
   `:224` 同一张表把「开册(就诊)」的触发源写成**玩家对病人的 `Interact`**。
   ⇒ 脉案屏**不是**启动常驻屏,它是**玩家动作的结果**。读法 A 的前提(「启动即看脉案」)
   在设计上**不存在**,故两读法之争**不是产品口味题,是占位物与设计不符**。

   **落地(三处,同一批)**:
   - `SkeuoRuntimeDriver._screenName` 默认 `"Casebook"` → **`""`**;`Awake` 的空名分支
     不再早退(建成「有面板、无屏」的合法形态)。
   - `Boot.unity:375` `_screenName: Casebook` → **空**(**这才是生产的那一处** ——
     只改代码默认值不改场景 = 生产照旧)。
   - 相机**零新增代码**:启动态 = `ModalId.None` ⇒ 39 的边沿判据 `before==after==None`
     ⇒ 不发请求 ⇒ 档位维持 `Explore`,与读法 B 一致,且**现在有断言背书**了。

   **⚠️ 连带暴露的新缺口(不是本卡范围,已立债 `TD-casebook-01`)**:
   占位一撤就看得清了 —— **生产侧没有任何代码 `OpenModal(ModalId.Casebook)`**
   (`grep` 全树仅 `Pause` / `SaveSlots` 两个调用点),也没有任何 story 认领
   「`Interact` → 立案 → 开册」这条接线。即:脉案屏在**撤占位之前**靠占位可见,
   **撤之后**在生产上**不可达**。本条裁定**不**掩盖该事实,如实登记。
2. **AC-39-08(相机模式零上传)为反射扫描的既有覆盖**,本卡未新增该判据的载体 ——
   它属于「本卡不得引入」的**禁止面**,不是新增实现面。实现轮已核实本卡零新增序列化类型。
3. **`AC-39-09`(摊开期间移动不变)未单独造夹具** —— 本卡只发相机意图,不碰移动管线;
   该 AC 的载体在 `player-controller` 侧。**本卡不借绿,记 `NOT-RUN`。**
