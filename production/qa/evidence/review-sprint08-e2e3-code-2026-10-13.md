# 代码评审(sprint-08 E2E-3 · 实现纪律半边)

| 字段 | 值 |
|------|-----|
| 评审对象 | sprint-08(方笺交互面轮)已落代码:E2E-3 为主,兼 S1/S2/S3 + 主题层 |
| 评审人 | lead-programmer(C3 双代理评审 · 实现纪律半边) |
| 日期 | 2026-10-13 |
| 门数字(用户提供,未复跑) | PlayMode 161/161;EditMode 3308 = 3261 pass / 0 fail / 46 skipped / 1 不适用;四变异各恰红已验 |
| 性质 | 实现纪律评审(可证伪逐条);门数字由用户跑,B 面(设计判定)归对代理 |

## 一、受审对象(文件清单与改动性质)

| 文件 | 角色 | 本轮改动 |
|------|------|---------|
| `unity/Assets/Tests/PlayMode/SkeuomorphicUI/e2e_full_loop_test.cs`(484 行) | E2E-3 被测件(PlayMode) | 新建:七环节全链收口判据 |
| `unity/Assets/Gameplay.UI/Skeuomorphic/Screens/CasebookScreen.cs` | 42 脉案屏(呈现面) | S1.1(病名册开合 + 命令通道)+ S2(翻页/折页/方笺页/戥子档位)+ S3(行头 `.channel-label`) |
| `unity/Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss` + `SkeuoThemeVariables.uss` | 42 USS 主题层 | S3:新增 3 token + `.channel-label` / `.channel-reading*` 改动 |
| `unity/Assets/Gameplay.Boot/PrescriptionPageHost.cs` | 装配层:落笔/开方写路径宿主 | 本轮无新结构改动(C3-M2/M3 修复在此前落盘;评审确认现状) |
| `unity/Assets/Gameplay.Boot/CasebookCommandRouter.cs` | 装配层:42 Submit → 写者 | 本轮无新结构改动(S2 三条动作路由在此前落盘;评审确认现状) |

## 二、ADR 符合性

### 2.1 结论总表

| ADR | 受审代码位置 | 结论 | 依据 |
|-----|------------|------|------|
| ADR-020 §四(玩家跨格唯一投影) | E2E-3 ① 环 + PlayerController 调用 | **符合** | 测试只经 `OnPositionSample` + `OnTickEdge` 触发,断言落 `ActorCellEntered`;测试内**零**直接写连续位置入流(无 `EventSink.Append` 调用,已 grep 证实) |
| ADR-008(病例流路由) | E2E-3 ④ 环 | **符合** | `JudgmentRecorded` 经 `ICasebookCommandSink.SubmitDiseaseName` → `JudgmentWriter`(写者),路由由 EventStream Kind→StreamId 纯函数白名单执行;测试不直写 |
| ADR-013 §9 C3(42 只渲染不持状态) | CasebookScreen 全类 | **符合** | 屏内字段全部为纯呈现态(`_bookOpen`/`_onPrescriptionPage`/`_folded`/UI 容器/`_sourceForBook` 引用);档位/作者/绑定病人均住装配层(路由器 + 宿主),42 经 DTO 回读(见 2.4) |
| ADR-029(`Sim/` 内禁 `new PayloadRef`) | E2E-3 + 受审 Boot/UI 代码 | **符合** | 全仓 grep:受审文件与 `Gameplay.Boot/`、`Gameplay.UI/`、E2E-3 内**零** `new PayloadRef(`;唯一合法构造在 `Sim.Codec/PayloadEncoder.cs`;门 b6 实现于 `Editor.Tools.Gates/AssemblyGates.cs:227` 且有测试(`o6_actor_cell_payload_test.cs`) |
| ADR-024(Kind 单一登记) | E2E-3 引用的 5 个 Kind | **符合(间接)** | 测试只读 `EventKind` 枚举与 `PayloadCodec.TryGetPayload`;Kind 登记真源 = `entities.yaml`(不在本轮改动面);未新增/未绕过 Kind |
| ADR-009 Amendment I(急救双事件) | E2E-3 ⑥ 环 | **符合** | 断言 `EmergencyAttempt` + `EmergencyTreatmentApplied` 双事件入**病史流**(与世界流分流一致),与 ADR-009 Amendment I「一条动作落两条」口径一致 |
| ADR-011 Amendment C(Submit 归 42 焦点桥) | E2E-3 Submit 夹具 + CasebookScreen 回调 | **符合** | 生产路径 = `NavigationSubmitEvent` → 屏内回调;测试派发**同一种**真事件(非旁路直调语义方法),解耦口径已在头注显式登记(sprint-08 §4 预裁) |
| AC-39-10(翻页/折页零流事件) | E2E-3 ⑤ 环 + CasebookScreen | **符合(结构性)** | E2E-3 本身未单测该条(由 `casebook_page_turn_test` 覆盖,见 2.5);受审屏代码内翻页/折页路径零写者调用;路由器 `SubmitPageTurn`/`SubmitFold` 为 no-op,注释锁定零流事件 |

### 2.2 测试是否违反它验证的铁律

逐项检查(可证伪):

- **禁手搓 PayloadRef**:E2E-3 内 grep `new PayloadRef(` = **0 命中**。载荷读取全部走 `PayloadCodec.TryGetPayload(in e, _bag.BlobPool, out ...)`(具名解码路径,`e2e_full_loop_test.cs:340/346`)。
- **禁直写流**:E2E-3 内 grep `.Append(` = **0 命中**。测试只**消费**流(`_bag.Stream.Events` 读),所有写入由真写者链路(PlayerController/ExamineDriver/SeenDiseaseProjector/JudgmentWriter/PrescribeRequestDriver/EmergencyManualDriver)执行。
- **禁直写 style**:E2E-3 不持有 USS/style 对象,只读 class 名(`channel-reading`/`channel-reading-negative`)判定渲染;CasebookScreen 的 style 写入(`style.display` 等)是 42 呈现面本份,不涉 C3。
- **C3 自反性**:E2E-3 装配的是**生产接线**(`CompositionRoot.Assemble()` 真袋 + 真 `CasebookCommandRouter`),不是替身 ⇒ ④/⑤ 环验证的是**真写路径**,不是镜像自证。这是本测相对 S1/S2 单测的关键增量。

### 2.3 一处口径备注(非违规)

E2E-3 ⑥ 环 F 注入后调 `PlayerInputReader.EmergencyPressed()` 作为探针断言(`:451-452`)。这是**输入直读**(设备级),ADR-011 的 50 ms 直读约束只作用于急救手感路径,此处是测试自检(证明键位注入生效,失败时报「键位怪癖非链断」),且该环的真实判据是流事件(⑥ 末尾两断言)。判定:**符合**;探针用法已在注释中声明动机。

### 2.4 C3 的落点证据(为何判定「42 不持状态」成立)

- `CasebookScreen` 字段清单(逐字段读):`_commandSink`(接线引用)、`_bookOpen`、`_lexiconBook`、`_sourceForBook`(只读 DTO 源引用)、`_onPrescriptionPage`、`_folded`、`_rxPage`、`_casebookFace`、`_dentchDoses`、`_brassScale` —— 全是呈现态/UI 容器/只读引用,**零**病人态/档位/库存/作者字段。
- 档位(`PendingConfidence`)/ 绑定病人住 `CasebookCommandRouter`(Boot 装配层);开方走 `PrescriptionPageHost.Prescribe`;库存住 `MinimalPortionsStore`。42 经 `ICasebookDtoSource` 回读 —— 与 ADR-013 §9 C3 的「装配层算、42 只读」同构。

### 2.5 跨测试覆盖声明(防「本测要测一切」)

- 翻页/折页零流事件(AC-39-10):`Tests/PlayMode/SkeuomorphicUI/casebook_page_turn_test.cs`(已存在,`page-edge-next/prev`/`fold-edge` 派发 + FakeSink 计数)。
- 输入键位注入本体:`Tests/PlayMode/interaction_input_chain_test.cs`(E2E-3 头注自陈「⑥ F 半个标」,符合解耦裁定)。
- 本测(E2E-3)的独有增量 = **七环节一条链 + 每环三层断言(真派发/流事件/DTO 回读)**,不重复邻居测试职责。

## 三、测试卫生

### 3.1 三件套环境还原完备性

| 项 | 实现位置 | 还原路径 | 结论 |
|----|---------|---------|------|
| `editorInputBehaviorInPlayMode` | `SaveEnvironment():115` 存 / `:143` 还原 | `RestoreEnvironmentIfNeeded` 先于设备移除,`_envSaved` 幂等 | **完备** |
| `backgroundBehavior` | `:116` 存 / `:143` 还原 | 同上 | **完备** |
| `Application.runInBackground` | `:117` 存 / `:146` 还原 | 无条件还原(`Application` 恒非 null) | **完备** |
| `InputManager.m_UpdateMask`(反射) | `:125` 存 / `:140` 还原 | null 卫兵(`_maskField`/`_manager` 非空才 SetValue) | **完备** |
| `Keyboard` 设备 | `:85-89` | 仅当 `_keyboardAdded`(本测自添)才 Remove;借用 `Keyboard.current` 不动 | **正确**(不动他人设备) |
| `Gamepad` 设备 | `:90-94` | 同上,`_gamepadAdded` 卫兵 | **正确** |
|  Mask 反射目标类型 | `_maskField` 来自 `typeof(InputSystem).Assembly.GetType("...InputManager")` 可空链 | 若类型更名 ⇒ `_maskField` 为 null ⇒ `SaveEnvironment` 在 `:125` **NRE 炸裂**(fixture red),不会静默跳过 | fail-loud,判定为**设计正确**(不是缺陷) |

- **TearDown 顺序**:还原环境 → 移除设备(先键盘后手柄,与添加逆序) → 清引用 → 销毁 PlayerController(GameObject)→ `PatientVisualSpawner.DestroyAll()` → 销毁 UIDocument/PanelSettings/GameObject → 清 bag/driver。与参考测试 `interaction_input_chain_test.cs:48-75` 的次序结构一致。
- **异常路径泄漏分析**:NUnit `[TearDown]` 在测试抛断言/异常后仍执行 ⇒ 环境还原、设备移除、对象销毁都被执行。**不存在**「异常时跳过清理」的路径。
- **一处轻微不一致(INFO 级)**:参考测试的 TearDown 是 `IEnumerator`(带 `yield return null` + `Time.captureDeltaTime = 0f`),本测是 `void`。UnityTest 场景下 `IEnumerator` TearDown 允许多帧等待销毁完成;`void` 在同帧执行 `Object.Destroy`(延迟到帧末实际销毁)。PlayMode 域中 Destroy 是帧末生效,两形态实际差异极小,且本测销毁的对象(GameObject/UIDocument/ScriptableObject)不依赖当帧断言。判定:**非缺陷,登记为一致性备注**。

### 3.2 2500 tick 推进的副作用

| 副作用面 | 分析 | 结论 |
|---------|------|------|
| 事件流膨胀 | `EventStream._events` 为 `List<SimEvent>` 只增不减;2500 次 `Advance` 触发 `VitalsService.OnTickEdge` 每 tick 的求值。**关键事实**:`DiseaseVitalsService` 头注 `:47` 明陈「零 `IEventSink.Append` —— 写者仍是 9/10/11/25 各自调用点」⇒ 2500 tick 本身**不入流**;末断言只遍历测试自己产生的十余条事件 | **无膨胀** |
| 单调基线 | 流按 tick 非降(BCD-码-3)由 `EventStream` 自带门禁;2500 tick 只推进 `TickDriver.CurrentTick`(内部累加器),不碰流 | 无冲突 |
| blob 池增长 | payload 只在真事件 Append 时进池(`PayloadEncoder.Encode` → `IBlobSink`);2500 tick 无写者调用 | **无增长** |
| `Advance` 的 dropped-time 告警 | `SimTickDriver.Advance` 对超 `maxStepsPerFrame` 的余量会**丢并告警**(`SimTickDriver.cs:113` 附近);2500 次循环每次 `Advance(0.05)` = 恰好 1 tick < 上限 ⇒ 不触告警路径 | 无副作用 |
| 2500 的取值依据 | 头注 `:209-212` 锚定:病种 1 `Curve.Incubation ≈ 2000 tick`,`tau < incubation ⇒ Progress ≡ 0`(AC-26 潜伏期抑制);探针实测 2442 tick 处 position = 1,2500 留余量;且以断言 `GetVitals(_patient).Position > 0f` 守住「推进有效」 | **注释锚定充分,magic number 有出处** |
| batch 渲染 | 头注自陈「batch 无渲染等待,2500 tick 推进是瞬时的纯 Step」⇒ 测试时长风险低 | 无 |

### 3.3 环境隔离(与邻居测试共存)

- 每个测试自建 `CompositionRoot.Assemble()`(袋内独立 EventStream/BlobPool/TickDriver/IdAuthority)⇒ 无跨测试共享可变态(符合 test-standards「Test data 必须自持」)。
- `PatientVisualSpawner.DestroyAll()` 连根销毁视觉节点(该类注释 `:47-50` 记录了 2026-10-10 全量跑时「靠层级继承会漏残留 5 实体」的事故 ⇒ 自持根节点 + 显式销毁是**修复后的形态**)。
- PlayMode 161/161 绿 = 隔离有效性的事证(用户门数字,未复跑)。

## 四、S3 主题层

### 4.1 AC-42-C5 var 纪律

| 检查点 | 事实 | 结论 |
|--------|------|------|
| `.channel-label` font-size | `SkeuoPaper.uss:150` = `font-size: var(--skeuo-font-size-lexeme)` | **走 var** |
| `.channel-reading` / `-negative` font-size | `:126` / `:133` 同 token var | **走 var** |
| 3 个新 token | `--skeuo-font-size-lexeme: 24px` / `--skeuo-reading-backing: #fbf9f3` / `--skeuo-ink-negative: #5a5044`(均在 `SkeuoThemeVariables.uss:59/60/140`) | token 唯一定义在主题文件 `:root`(单一真源),USS 侧零内联 hex(C5 门 `skeuo_readability_test` 的 `test_s3_3` 逐块断言零 hex) |
| C5 门本身 | `SkeuomorphicUiGates.cs:305-323`:`ValidateUssHardcodedFontSize` 扫 `Assets/Gameplay.UI/Skeuomorphic` 全部 .uss,`HardcodedFontSizeRegex` 命中即红 | 门存在且跑通(EditMode 0 fail) |
| `.channel-label` 头注自陈「不进 Registry」 | `SkeuoPaper.uss:146` 明注「语义样式类,承 `.channel-reading` 同款边界」 | 边界**站稳**,先例在案(`.channel-reading` 同样不进 Registry) |
| Registry C7 判据面 | `SkeuomorphicUiGates.cs:137-141`:C7 接图半只查 `IsTextureContainer == true` 的类;语义样式类不进判据面 | 与「不进 Registry」的声明自洽,不产生结构洞 |
| token 引用面 | `--skeuo-font-size-lexeme` 全仓仅 4 处引用(3 个类 + 1 定义),全在 42 脉案文件内;头注 `:136-137` 明陈「只挂 42 脉案的题签与落字,不全局替换 md」 | 作用域声明与实现一致,无越界漂移 |

### 4.2 `.channel-label` 未进 Registry 的边界论证(为何判定站稳)

1. **先例同款**:`.channel-reading`/`.channel-reading-negative` 也是语义样式类且不进 Registry(S3 头注自陈承接)。
2. **门的判据面不收它**:C7 骨架半查「已注册元件类」,`SkeuoComponentRegistry` 注册的是 `SkeuoElement` 枚举元件(Paper/Scroll/Ink/Seal…),不是每个 USS class;`.channel-label` 无对应元件枚举 ⇒ 注册它是范畴错位。
3. **无第二真相残影风险**:该类只被 `CasebookScreen.AddChannel` 一处挂载,行头 = 唯一载体;不涉「同族多类模拟变体」(C4 禁的是那个)。
4. **可证伪守卫已在位**:`skeuo_readability_test.cs:96-108` 三类的 `font-size: var(--skeuo-font-size-lexeme)` 逐块断言 —— 若未来有人把 `.channel-label` 改成内联或换 token,测试红。**结论:边界不是靠注释单方面声明,有测试守着。**

### 4.3 无障碍修复的定性(顺带核实)

S3 修的 7:1 违约:原 `--skeuo-ink-faded`(rgba 0.55)纸面合成 3.87:1,改 `--skeuo-ink-negative` 专用墨后 7.48:1(头注实测记录);底衬 `--skeuo-reading-backing` 与焦点环图走不同属性(`background-color` vs `background-image`)⇒ 不违「焦点单槽铁律」——论证与 `SkeuoPaper.uss:118-122` 头注一致,**已核实为真**。

## 五、代码质量

### 5.1 E2E-3 单测可维护性(484 行)

| 维度 | 事实 | 判定 |
|------|------|------|
| helper 复用 | `Submit`(落焦+派发)、`StreamHas`、`ReadingRows`/`EntryRows`/`DoseRows`(遍历收集)、`HasWordText`、`Judgments`(载荷解码)、`WaitForRoot`、`Press`/`PressGamepadButton`/`ReleaseAll`、`SaveEnvironment`/`RestoreEnvironmentIfNeeded` —— 语义单一、static 优先 | **良好**;`Submit` 与 `casebook_point_identify_chain_test.cs:108` 逐字同型(先例复用,未重造) |
| magic number 锚定 | `DiseaseId = 1`(内联注册表,锚定「词表反查有 id」);`2500`(潜伏期探针实测 2442 + 余量,`:209-212`);`2f`(面板等待秒数,`:250`);`(int)doseRows[0].userData - 1`(显式 ordinal,1 起 ⇒ -1 回落索引,注释在 `<param name="doseOrdinal">` 档 SD):4 处常量全部有注释或接口契约锚 | **合格** |
| 复杂度 | 单方法 `test_e2e_fullLoop_sevenStages` 为线性 arrange/act/assert 流,分支仅 2 处(`if (!EmergencyPressed())` 兜底 + `yield return null`);cyclomatic ≈ 3,远低于 10 | **合格** |
| 40 行限 | 主测方法 ≈ 120 行(含注释与空行分隔),超出「方法 ≤ 40 行」字面限 —— 但形态为**一测到底**的用户裁定(2026-10-13「一测到底 + 每环保留断言」),按 design-doc 该裁定优先;每环均有 banner 注释分段 | **登记为例外**(非豁免编码标准,是该测的构成本身) |
| 头注质量 | 32 行头注:判据锚(7 环节 × 判据出处)、解耦口径、时序前置(BCD-码-3)、库存种子、真派发口径(2026-10-12 实测)逐条可证伪 | **优秀** |
| 失败可读性 | 每条断言带中文消息(「[夹具红] panel 为 null」/「若此条红,键位怪癖非链断」)—— 红时直接指认断裂环节 | **良好** |
| 潜在脆弱点 | `EntryRows`/`DoseRows` 以 `userData is int` 收集行 —— 若未来 UI 在其他容器也用 int userData,收集面会扩大(假阳)。当前 `dentch-doses` 子树限定 + 病名册匿名容器限定了范围;相对脆弱但已有子树约束 | **INFO**:建议未来抽共用 `CollectRows(VisualElement)` 并显式以 name/class 白名单过滤,而非 `userData is int` 宽匹配 |

### 5.2 CasebookScreen 改动最小侵入性

| 检查点 | 事实 | 判定 |
|--------|------|------|
| `AddChannel` 签名 | 未改(头注 `:227-229` + `:123-124`:签名是 `texture_binding_gate_test` 的结构锚点,改则门红);S3 只加 `header.AddToClassList("channel-label")` 一行 + 注 | **最小侵入成立** |
| BuildUI 既有 name 序 | 未动(新增容器全匿名:rowLayout/`_casebookFace`;病名册容器运行时建、匿名);`casebook_render_test` 交叉断言(XML spec 面 ≡ 代码面 name 序)零回归 | **成立** |
| S1.1 写通道 | `_commandSink?.` 全可空 ⇒ 未接线不崩不造动作(显式注释) | 合理 |
| 纯呈现态三分 | `_bookOpen`/`_onPrescriptionPage`/`_folded` 三 bool + 注释逐一声明「零写入,AC-39-10」;写动作仅 `SubmitDose`/`SubmitDiseaseName`/`SubmitConfidence` 三条走 sink | **与 C3 一致** |
| `_rxPage` 命名规避 | 头注 `:68-75` 记录「prescription 含 rip 子串会被破纸门反射命中」⇒ 改 `Rx` + 已核实门实现(`no_healthbar_feedback_test.cs:183-184` 确按 `{tear,rip,break,split,fracture}` 扫 42 命名空间引用类型字段) | **规避真实存在,注释诚实**(自陈「这不是绕门」) |
| 方法长度 | `ApplyData` ≈ 58 行(含注释)—— 超 40 行字面限;`BuildUI` ≈ 90 行。两者均有大段头注/分段 banner,可读性未受损 | INFO:登记,与 5.1 同源(本轮设计裁定密度) |
| 文档完整性 | 所有 public 方法有 `<summary>`;private 关键路径有头注;类头 22 行设计说明含双真源声明 | **符合项目标准** |

### 5.3 Boot 两个装配件(本轮无新改动,现状确认)

- `PrescriptionPageHost`:fail-loud 依赖(`?? throw` 三处)+ `AssertAuthorReady`/`BindPatient` 前置;`Prescribe` 返回值刻意不外传(AC-11-12 零提示纪律,注释锚定)。
- `CasebookCommandRouter`:三态循环(空 → 疑似 → 确定 → 擦除)闭合;`SubmitPageTurn`/`SubmitFold` no-op 且注释声明「未来分册态段的唯一挂点」;`SubmitDose` 未接宿主 fail-loud。
- 两者与 `CompositionRoot.cs:270-275` 的接线一致(复用同一 `JudgmentWriter` 实例 = 单作者真源)。

## 六、未核实项(不编造)

1. **IL2CPP/Mono 逐位性**:本评审未跑跨平台对拍(ADR-012 门);E2E-3 是 PlayMode(Editor/Mono 域)测试,其 fixture 反射(`InputManager.m_UpdateMask` 私字段)在 player 构建的形态**未核实**。
2. **2500 tick 的真实墙钟耗时**:未实测(门数字由用户跑,PlayMode 161/161 绿 ⇒ 无超时发生,但单测耗时分布未核实)。
3. **变异测试的具体命令与输出**:用户已验「删 BindCommandSink ⇒ ④ 环红;不 Grant 库存 ⇒ ⑤ 环红,均恰红后还原」—— 本报告采信该结论,未复跑(符合任务约束「不跑 unity 命令」)。
4. **`_savedBehavior` 等在 SetUp 前失败的场景**:若 `SaveEnvironment` 自身 NRE(反射目标缺失),`_envSaved` 停在 false ⇒ TearDown 不会二次还原 —— 该路径无害(环境未被改),但**未构造失效测试核实**。
5. **`.channel-label` 在 VR/手柄导航下的呈现**:ADR-013 的 P1a 面,不在本轮裁决。

## 七、判决与动作清单

### 判决:**CHANGES REQUIRED**(3 条 P2/P3,零 BLOCKING,零架构违规)

**理由**:ADR 符合性 8/8 全部符合,测试卫生无泄漏,主题层 var 纪律完备且测试在守。剩余 3 条均为**耐久性/一致性**改进,不阻塞 sprint-08 结案,但按本仓「测试也是一等公民代码」的纪律须登记,不得静默放过。四项已验恰红的变异测试证明主链判据真实有效。

### 动作清单

| # | 优先级 | 动作 | 落点 | 可证伪判据 |
|---|--------|------|------|-----------|
| A1 | P2 | 抽取共用夹具基类/静态工具(至少 `Submit`、`SaveEnvironment` 三件套、`TearDown` 主体),`e2e_full_loop_test` / `interaction_input_chain_test` / `casebook_point_identify_chain_test` 三测复用 | `Tests/PlayMode/` 新增 fixture helper(或部分类) | grep:三文件中 `private static void Submit(VisualElement` 的重复定义数 = 1(helper 内) |
| A2 | P2 | `EntryRows`/`DoseRows` 的 `userData is int` 宽匹配改为显式过滤(白名单 class/name 前缀 + userData int),防未来 UI 新增 int userData 造成假阳收集 | `e2e_full_loop_test.cs:303-330` | 造一探针:根下挂一个带 int userData 的无关元素,`EntryRows` 计数不变 |
| A3 | P3 | `Time.captureDeltaTime = 0f` 与 `IEnumerator` TearDown 形态对齐参考测试(或反向注释登记为何本测 void 足够) | `e2e_full_loop_test.cs:81-110` | 代码评审目检:形态一致**或**头注有登记句 |

### 明确不动作清单(判定为设计正确,勿改)

- `AddChannel` 签名(门锚点,禁改)。
- `_rxPage` 命名(破纸门假阳的正确规避,已有注释)。
- 2500 tick 循环(探针锚定,断言守推进有效性)。
- `SaveEnvironment` 反射失败 fail-loud(比静默跳过更安全)。
- `.channel-label` 不进 Registry(边界有先例 + 有测试守卫)。
- 主测方法 120 行(「一测到底」用户裁定的构成本身)。

## 八、结论

sprint-08 E2E-3 的**实现纪律半边通过核心检验**:全链验证真实(生产接线 + 真派发 + 真事件入流 + DTO 回读)、ADR 符合性 8/8、测试环境还原完备(异常路径无泄漏)、2500 tick 无副作用、S3 主题层 var 纪律完备且有测试守卫。3 条改进项均为耐久性,建议入下一个 polish 轮或 tech-debt register,不在本轮 gate 阻断。
