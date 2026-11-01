# Story 007: 档位请求覆盖矩阵门(表 → 代码 → 行为)

> **Epic**: 摄像机与视角
> **Status**: **Ready ⬜**(2026-11-02 立件 —— 规范已写定,**实现未开工**)
> **Layer**: Presentation
> **Type**: Integration
> **Estimate**: 4–6h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-11-02

> **本卡的由来(一句话)**:`R-2-5` 的档位表有 **6 条**有效行,而生产侧长期只有 **4 条**有请求点
> —— 39 的两行(`Casebook` / `Explore`)**零调用点**,而 `CameraSetModeRequesterGate` **全绿**。
> 该门只判**语法**(`.SetMode(` 是否双参、`requesterId` 是否字面 `-1`),**从不看那张表**,
> 所以「表里有、代码里没有」它结构性看不见。
>
> **2026-11-02 实测(立件时的缺口样本)**:
> ```bash
> grep -rn "\.SetMode(" unity/Assets --include=*.cs | grep -v /Tests/
> #   只有两处:EmergencyChannelDriver.cs(10)· DownedCameraRequester.cs(29)
> grep -rn "CameraMode.Casebook" unity/Assets --include=*.cs | grep -v /Tests/
> #   三处全在相机内部(优先级表 / 冻结判据),零个「请求」
> ```
> ⇒ 缺口已于 2026-11-02 由 `casebook/story-006` 补上(**本卡的前置**),
> 但**门本身仍不存在** —— 同样的缺口下次还会以别的形式出现。

---

## Context

**GDD**: `design/gdd/camera-and-viewpoint.md` `R-2-5`(6 条有效行 + 11 的具名除外行)
· `AC-2-18①`(档位表穷举 + 同请求方双向判据)· `AC-20-15` / `AC-20-16`(ADR-020 Amendment C)

**ADR Governing Implementation**:
- **ADR-020 Amendment C ①②**(主):`RequesterId` 参与裁决;生产调用方必须双参且传真实系统号
- **ADR-024**(次·**方法学**:真源 = 机器可读注册表,散文表降级为注记并声明「不一致时以注册表为准」)
- **ADR-013 §9 C3**(次):呈现层只渲染 —— **采集面不得参与任何判定**

**Requirement**: 本卡新登 **`TR-camera-008`**(见 R-7.3)

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 纯 C# + 一条 PlayMode 夹具;零 post-cutoff API。

---

## Detailed Rules

**R-7.1 表升为机器可读真源(承 ADR-024 口径)。**

新建 `docs/architecture/camera-mode-requests.yaml`,`R-2-5` 的 markdown 表**降级为注记**
(表首加一句「不一致时以本注册表为准」+ 行数复算命令)。字段:

```yaml
- id: CMR-001
  requester: 10          # 系统号
  requester_type: EmergencyChannelDriver
  mode: Treatment
  trigger: 急救动作开始(Arm 成功后)
  evidence: Tests/PlayMode/EmergencyProcedures/emergency_camera_mode_intent_wiring_test.cs
- id: CMR-002
  requester: 10
  mode: Explore
  trigger: 急救动作结束(含跳过路径)
  evidence: 同上
- id: CMR-003
  requester: 39
  mode: Casebook
  trigger: 开册(模态边沿)
  evidence: Tests/PlayMode/Casebook/casebook_camera_wiring_test.cs
- id: CMR-004
  requester: 39
  mode: Explore
  trigger: 合册(模态边沿)
  evidence: 同上
- id: CMR-005
  requester: 29
  mode: Downed
  trigger: `PlayerDied` 落流后的表现态投影(Φ1)
  evidence: Tests/PlayMode/DeathRespawn/downed_camera_wiring_test.cs
- id: CMR-006
  requester: 29
  mode: Explore
  trigger: 复活 / 醒来(Φ3 起手)
  evidence: 同上
```

**R-7.2 静态层 —— 防「绕过」,不防「没发」。**

`CameraSetModeRequesterGate` 追加两项(不新立门,扩既有门):
- **B1**:每个请求方必须暴露 `public const int RequesterId = <系统号>`;表里 `requester` 须能对上它。
- **B2**:表 ⊆ 代码 —— 对每一条表行,生产侧至少有一处出现「该 `(mode, requester)` 组合」
  (mode 以字面量或该方法内出现的 `CameraMode.X` 计;`requester` 以常量名计)。

⚠️ **B2 的已知弱点(必须写进门的不动串,不得让读者以为它更强)**:
`EmergencyChannelDriver` 的调用是 `rig.SetMode(mode, RequesterId)` —— **mode 是形参**,
字面量在调用方 `RequestCameraMode(CameraMode.Treatment)` 处。B2 若不做局部数据流就抓不到,
做了也很脆(改个辅助方法就失配)。⇒ **B2 只能证明「有人声明要发它」,不能证明「它真的会被发」**。

**R-7.3 动态层 —— 这才是「该发没发」的判据。**

一条 PlayMode 矩阵夹具 `camera_mode_request_matrix_test.cs`:
1. 真启动序一次;
2. 挂一个**只读记录器**到 `CameraModeMachine.SetMode` 入口(见 R-7.4);
3. 按表**逐行驱动触发**(手法各 epic 已有:10 = arm/end;29 = `PlayerDied` + 复活格;39 = 开/合册);
4. **双向断言**:
   - **表有 / 记录无** ⇒ 红,报文点名 `CMR-xxx`(**该发没发**);
   - **记录有 / 表无** ⇒ 红(**发了没登记** = 第二真源)。

**R-7.4 记录器三条纪律(否则它自己会变成第二真源)。**
- ① **不参与任何判定**:只在 `SetMode` 入口追加一行,零读回、零可写面;
- ② **有界**:环形,容量 64,溢出丢弃最旧(不增长、不分配);
- ③ **不进发布构建**:`#if UNITY_EDITOR || DEVELOPMENT_BUILD` 包裹,`Release` 下零代码。

**R-7.5 补登 `TR-camera-008`。** `tr-registry.yaml` + `traceability-index.md` 各追加一条,
`status: covered` 只在夹具与门**都跑绿**后才是真的。

---

## In Scope

- **新** `docs/architecture/camera-mode-requests.yaml`(R-7.1)+ `R-2-5` 加「以注册表为准」注
- `Editor.Tools.Gates/CameraSetModeRequesterGate.cs`:追加 B1 / B2(R-7.2)
- **新** `Gameplay.Presentation/Camera/CameraModeRequestRecorder.cs`(R-7.4)
- **新** `Tests/PlayMode/Camera/camera_mode_request_matrix_test.cs`(R-7.3)
- `tr-registry.yaml` + `traceability-index.md` 补登 `TR-camera-008`

## Out of Scope

- **改动任何请求方的行为** —— 本卡只加判据。若矩阵红了,那是**别的卡的活**(登记,不在本卡顺手改)
- **`CameraModeMachine` 的裁决语义** —— 归 `story-005`
- **`FirstPerson`(VR)路径** —— P1a;表里没有它的行,矩阵也不覆盖它
- **「发在对的时机」** —— 见 §抓不到什么

---

## Dependencies

- **`casebook/story-006`**:`Complete ✅ 2026-11-02` —— **实现依赖**(39 的两行必须先有生产请求点,
  否则矩阵表里那两行永远「表有 / 记录无」)。
- **`emergency-procedures/story-010`**:`Complete ✅ 2026-11-02` —— 10 的两行。
- **`death-respawn/story-007`**:`Complete ✅ 2026-11-02` —— 29 的两行。

---

## Acceptance Criteria

- [ ] **S7-01**(`[A]` · BLOCKING)表文件存在且 **6 行 + 11 的具名除外行**;`R-2-5` 有「以注册表为准」注
- [ ] **S7-02**(`[A]` · BLOCKING)**B1**:每个请求方有 `const int RequesterId`,与表内 `requester` 对上
- [ ] **S7-03**(`[A]` · BLOCKING)**B2**:表 ⊆ 代码;删掉任一请求方的 `SetMode` 调用点 ⇒ 红
- [ ] **S7-04**(`[A]` · BLOCKING)**动态矩阵:表 ⊆ 记录** —— 删掉任一请求方的生产发送 ⇒ 该行红并点名 `CMR-xxx`
- [ ] **S7-05**(`[A]` · BLOCKING)**动态矩阵:记录 ⊆ 表** —— 加一条不在表里的 `SetMode` ⇒ 红
- [ ] **S7-06**(`[A]` · BLOCKING)记录器 **不进发布构建**:`Release` 符号下零引用(源面断言 + 变体编译)
- [ ] **S7-07**(`[A]` · BLOCKING)记录器**不参与判定**:全仓无任何 `RequestRecorder` 的**读**点
      (除本夹具与门本身)—— 出现即红
- [ ] **S7-08**(`[I]` · BLOCKING)`TR-camera-008` 在两份登记文件各 ≥ 1 处

---

## 抓不到什么(必须逐条写进本卡,禁借绿)

1. **「发在对的时机」** —— 矩阵只证「存在性」:该组合在某次驱动下出现过。
   发早了 / 发晚了 / 发在错误条件下,均不在本卡判据面内(归各请求方自己的卡)。
2. **发了几次** —— 边沿 / 幂等归各自的夹具(39 的 `S39-07`、10 的 `S10-xx`)。
3. **相机真的接受了** —— 矩阵记录的是「请求进入 `SetMode`」,**不是** `Mode` 真的变了
   (`CameraModeMachine` 可能因优先级拒绝合法的请求)。接受语义归 `story-005` 与各请求方的 L2。
4. **VR 路径** —— `FirstPerson` 不在表内(P1a),矩阵对它零覆盖。
5. **B2 的局部数据流** —— 见 R-7.2 的已知弱点:静态层只证「有人声明」,不证「会发出」。

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- L2 `unity/Assets/Tests/PlayMode/Camera/camera_mode_request_matrix_test.cs`(S7-04 / 05)
- L1 `unity/Assets/Tests/EditMode/Camera/camera_mode_table_gate_test.cs`(S7-01 / 02 / 03 / 07)

---

## 注意事项(实现前先读)

- **先跑一次矩阵再看它红什么**:39 的两行刚接上,10 / 29 各自有夹具,但**没有任何一条夹具
  在一个 boot 里同时覆盖六行**。第一次跑大概率会发现某行「夹具能单独跑通,但一次 boot 里驱动不出来」
  (例如 29 的醒来需要 `ActorCellEntered`,而 10 的 arm 需要候选病人)——
  那是本卡最可能卡住的地方,**不是假绿,是编排难度**。先记下来,别硬凑。
- 若某行在一次 boot 里**结构性驱动不出来**(例如需要跨场景),允许拆成多次 boot,
  但**每一行的证据必须点名**是哪个夹具在哪一段覆盖的 —— 不许用「合集里出现过」搪塞。
