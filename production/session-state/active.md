# Session State — 2026-10-05(**当前阶段 = Pre-Production · Sprint 04 Phase 1 ✅ 已收口 · Phase 2 进行中**)

## 🔄 最近收口 = diagnosis-system story-003(F-8.1 可读地板与 F-8.2 精度档槽)—— ✅ 收口 2026-10-05 · 已提交推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(1 MAJOR + 5 MINOR)+ QA 侧 CHANGES REQUIRED(1 MAJOR + 6 MINOR),
> **0 BLOCKING 双侧**;2 MAJOR + 11 MINOR 全部落点(**全为文本/文档 + 判据强度,零运行期改动**)⇒ 复跑绿 + 两处新 MUT 证明。

### 交付物
- **作者态**:`assets/data/diagnosis_read_floor.json`(合成系数:base_read=1 · read_floor_min=1/4 · read_gamma=2;**数值归用户数值轮**)
- **生产**:`DiagnosisReadFloorBinder.cs`(阶段 2 绑定 + **唯一**校验点 C-1/C-5/skill_cap)·
  `DiagnosisReadFloorBaker.cs`(仓根种子 → 产物)· `DiagnosisReadFloorCookedWriter.cs` +
  `DiagnosisReadFloorCookedCodec.cs`(严格镜像;固定头 **32 B**)· `DiagnosisReadFloorBinderProbe.cs`(薄转发)·
  `DiagnosisReadFloorTable.cs`(运行期定表 + `DiagnosisReadFloorEvaluator` 求值器 + `DiagnosisSlot`/`SignReadState` 枚举)·
  `DiagnosisChannelMaskMap.cs`(**Note 6** 通道序数↔位掩码映射 + 双向断言,接生产路径)·
  `DataBakeMenu.BakeDiagnosisReadFloor`(菜单调用点)
- **测试**:`read_floor_slots_test.cs`(**28 条**)· `DiagnosisGoldenScan.cs`(story-002/003 **共享**金标扫描真源 —— 兑现 story-002 头注「扩金标」承诺)·
  `tests/unit/diagnosis_system/fixtures/read_floor_*.json`(**10 夹具**)+ README 账本补 Story 003 段
- **金标**:`GoldenConstantsHash = f75a8170`(`b9354110` s002 → `5bba361c` s003 扩枚举/映射面 → `f75a8170` 修复轮,
  由 `FixedHeadBytes` 36→32 的**有意识**代码常量修正驱动)
- **证据**:`production/qa/evidence/review-diagnosis-story-003-2026-10-05.md`

### 单轮评审 → 修复轮(要点)
- **MAJOR-1(结构)** AC-8-35 金标「覆盖」声明 **over-claim**:`read_floor_slots_test` 写「F-8.1 参数半边转 covered」
  与 `sign_table_test` 头注矛盾 ⇒ 四处统一为准确边界(扫描面 = 前缀 ns **代码常量** + DIAG_TIERS;
  **曲线参数**住 `assets/data/*.json` 由 ConfigVersion 覆盖,**仍 NOT-RUN**)
- **M-1(QA)/MINOR-3** `README.md` 缺 Story 003 段(标称夹具 21 实存 **31**;无 AC→测映射;未登记 NOT-RUN)⇒ 补全段
- **MINOR-1(结构)** codec `FixedHeadBytes` 36 → **32**(注释双错订正;E-13 文本阈值)
- **MINOR-4(结构)** GDD §F-8.2 回退散文**示例**方向反 + 永不触发(`sign_rales` 粗档实有词)⇒
  登记 **GDD 散文勘误**(待设计轮),就地注 `DisplayWord` doc;**不改 GDD 权威件**
- **MINOR-5** 删死 import(`DiagnosisReadFloorTable.cs` `Sim.Contracts`)
- **m-1** `test_ac89` 去恒真 `DoesNotContain(...,99)` ⇒ 改断**枚举值域**(零锁闭成员)+ 五手段**各有真读数**(`DisplayWord` 非 null)
- **m-2** `test_g1` 去同参自等循环(纯函数必等)⇒ 改断**表项=存储值**(`FloorAt`) + 跨档区分(非退化)
- **m-3/m-4/m-5** 反射幂名清单扩 9 名 + 明写「真守卫在边界门」· AC-8-7 代理判据登记 · AC-8-46「词变粗」NOT-RUN 登记
- **m-6** story Test Evidence / Status / AC 勾选回填

### 设计决定(2)
1. **空白档回退方向 = 严格向下**(GDD §F-8.2 规则字面;koplik 粗/中为空 ⇒ 粗/中档读不出,细档起出词)。
2. **金标两次重钉均有意识**(扩面 + 修复轮常量修正),理由已登记。

### 验证(实测)
- filter:`unity/Logs/s003-fix3.xml` = **94 / 93 passed / 0 failed / 1 skipped**
- 全量:`s003-fixfull.xml` = **2630 / 2583 passed / 0 failed / 46 skipped / 1 inconclusive**
- MUT-D(`DisplayWord` 下界 `i>=0`→`i>=1`)⇒ **恰 3 红**(含 `test_ac89` —— 证 m-1 增强判据承重;旧 `DoesNotThrow` 版不红)
- MUT-E(`FloorAt` 返 0f)⇒ **恰 1 红**(`test_g1` —— 证 m-2 增强判据承重;旧自等版不红)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 6 项**(本 story):AC-8-F3 σ 联动(归 disease 004)· AC-8-9 EmitGrowth 门控(story 005)·
  AC-8-46「词变粗」+ 病名半边(story 006/37)· 跨会话/跨平台逐位(AC-8-F5/story 004)·
  AC-8-35 曲线参数半边(ConfigVersion 覆盖)· AC-8-7 AC 字面浮点用例(待 `Precision`)
- **GDD 散文勘误 1 项**:`diagnosis-system.md` §F-8.2 回退示例(方向反 + 永不触发),待设计轮
- **TR-registry 回填**(TR-diag-008/009/011/012 等 gap→covered)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-004(F-8.3 阴性把握度与不泄漏不变量)**,同协议;
  epic 链 diagnosis(**3/6**)→ case → prescription

## 📋 历史状态(2026-10-05)—— diagnosis story-002(体征词条表 schema 与 P0 数据行)—— ✅ 收口 · commit `f27ca3e` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:结构侧 CHANGES REQUIRED(1 MAJOR + 10 MINOR)+ QA 侧 CHANGES REQUIRED(2 MAJOR + 10 MINOR),
> **0 BLOCKING 双侧**;3 MAJOR + 20 MINOR 全部落点 ⇒ 复跑绿 + MUT 变异证明。

### 交付物
- **作者态**:`assets/data/diagnosis_signs.json`(R-8.2 冻结 34 行 = 阳 30 / 阴 4)
- **生产**:`DiagnosisSignTable.cs`(validator + 三枚举 + `SlotBounds` **首次成文**)· `DiagnosisSignBinder`
  (**唯一 `Validate` 调用点**,聚合硬失败 + 空表拒收)· Baker / Writer / Codec(严格镜像 +
  schema 期望比对 + count 钳制 + 枚举序数界)· Probe · 菜单 `BakeDiagnosisSigns`
- **测试**:`sign_table_test.cs`(**42 条**)+ `tests/unit/diagnosis_system/`(**21 夹具** + README 账本)
- **双金标**:`GoldenConstantsHash = b9354110`(AC-8-35)· `GoldenContentHash = 3954e294`(R-8.2 内容逐字)
- **证据**:`production/qa/evidence/review-diagnosis-story-002-2026-10-05.md`

### 单轮评审 → 修复轮(要点)
- **S-MAJOR** `SignChannel` 同名不同物(本表序数 vs `Sim.Contracts` AC-21 位掩码)→ 枚举 doc 警示
  + **story-003 Note 6 登记映射表与双向断言义务**(未立映射前禁 cast)
- **Q-MAJOR-1** R-8.2 逐行内容无守卫 → `test_r82_contentFrozen_golden` 内容金标
- **Q-MAJOR-2** F-8.1/F-8.3 参数半边零覆盖 → 头注 + story AC 显式 NOT-RUN
- **MINOR ×20 全落点**:binder `catch (Exception)`(防 `/0` 逃逸)+ 空表拒收 + BindResult doc ·
  codec schema/钳制/序数界 · `SlotBounds` 收只读视图 · writer CS0104 别名 · 文件族名注 ·
  story-004 占位值预警 · `BakeFails` **恰一条排他** · P1a 8 值 TestCase 全值 · +4 违例夹具 ·
  tier 触底 / C-6 双标 / addRow 绑金标 / 切点上下文 / FormatConst 兜底 · README 账本 · Test Evidence 回填
- **两处 story 文本订正**(实现前对账登记):依赖「disease story 003 载体」不实 → `Editor.Tools.Bake` 模式;
  Note 1 三态可分错引 AC-8-F → **AC-8-21(+ V-8.2 / AC-8-24)**
- **MUT-Validate**:注释唯一调用点 ⇒ **恰 8 条校验器路径红**(tier35/tier15/阳性带权/阴性缺权/
  阴性零权/重复主键/lab/病史白名单),绑定层全绿 —— 承重面从推断变实测

### 验证(实测)
- bootstrap:`unity/Logs/s002-bootstrap.xml`(金标 PENDING→打出实际值;途中自查出 reveal
  TestCase 传裸串 bug,改数组后绿)
- filter:`s002-run2.xml` = **67 / 66 passed / 0 failed / 1 skipped**(skipped = 反向孤儿 [Ignore])
- MUT:`s002-mut-validate.xml` = **恰 8 红**;还原后复绿
- 全量:`s002-full.xml` = **2603 / 2556 passed / 0 failed / 46 skipped / 1 inconclusive**(基线 2561 + 42)

### ⬜ 待办 / 未闭登记(禁借绿)
- **NOT-RUN 8 项**全表见评审原件 §4:反向孤儿(BLOCKED disease 006)· 正向接线(tripwire)·
  F-8 金标半边(story 003/004 扩)· 跨会话确定性 · 人工核对字面 · cooked.bytes 数据轮口径 · …
- `neg_weight = "1"` 占位已挂 story-004 Note 7;枚举↔位掩码映射已挂 story-003 Note 6
- **TR-registry 回填**(TR-diag-014 等 gap→covered)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-003(F-8.1 可读地板 + F-8.2 精度档槽)**,同协议;
  epic 链 diagnosis(2/6)→ case → prescription

## 📋 历史状态(2026-10-05)—— diagnosis story-001(程序集边界与 VitalsDto 只读门面)—— ✅ 收口 · commit `6685046` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:双代理评审 **结构侧 CHANGES REQUIRED(3 MAJOR)+ QA 侧 APPROVED(2 MAJOR 收口前置)**,
> 无 BLOCKING;5 MAJOR + 8 MINOR 逐条修复 ⇒ 复跑绿。

### 交付物
- **生产**:`DiagnosisVitalsFacade`(静态零字段唯一取数门面)· `DiagnosisGrowthExit`(7 参纯转发
  出口,IL 调用点恰=1)· `DiagnosisBoundaryGates`(Cecil IL + 源文本 + 逃逸,11 个 tag)·
  `AssemblyGates` RunMenu + BuildGate 接线(reload hook 刻意不加,承 Required-7c)
- **测试**:`unity/Assets/Tests/EditMode/DiagnosisSystem/boundary_guard_test.cs`(**25 条**,
  含 EOF 真 IL 负例夹具)
- **证据**:`production/qa/evidence/review-diagnosis-story-001-2026-10-05.md`

### 单轮评审 → 修复轮(结构 3 MAJOR + QA 2 MAJOR + 8 MINOR)
- **S-1** `Mathf` 双层补入(G-4 邀请式绕行)· **S-2** `IModifierType` 修饰符侧遍历(b5 Required-2)
- **S-3** `DateTimeOffset`/`Stopwatch`/`TickCount` 时钟补全 · **S-4** 深度超限改落红
- **S-5** 缺失不叠报 + `[D-TREF]`→`[D-0]` tag 统一 · **S-6** 哈希段标结构占位(禁称已生效)
- **S-7** 消费者住前缀纪律登记 · **S-8** `IVitalsQuery` 成员集锁 · **S-9** BuildGate WARN 口径
- **Q-1** 全量复跑 · **Q-2** AC 逐条括注 NOT-RUN · **Q-3** `RecipeDataSet` 替代括注
- **Q-4** 铁律③ 判据等价性(IL+源 ⊃ 反射)入 Completion Notes · **Q-5** 三处 `Is.Not.Empty` 自证
- **Q-6** 漏测分支负例补齐(`Fix.One` 字段 / 源层 `IEventSink`+`FixParse` / `Mathf` / 时钟三族)

### 验证(实测)
- 过滤:`unity/Logs/s001-diag-fix.xml` = **25 / 25 passed / 0 failed**
- 全量:`unity/Logs/full-diag-001.xml` = **2561 total / 2515 passed / 0 failed / 45 skipped /
  1 inconclusive**(增量 115 = patient-ai s003+s004+本批;根 `Skipped:Ignored` 与基线 019d 同态)

### ⬜ 待办 / 未闭登记(禁借绿)
- 残余 NOT-RUN 全表见评审原件 §四:AC-8-1 全流程脚本+哈希鉴别力(005/006)· 跨 epic 基线(CI)·
  AC-8-3 四输出(002/003)· AC-8-6 定表+G-4 IL2CPP(003/004)· AC-8-4 11 侧+D-11 词面回补
  (prescription story 003)· `typeof(Fix)` 构造性绕行复查(11/005 轮)
- **TR-registry 回填**(TR-diag-004 gap→covered、002/010 partial 等)= 独立 docs 轮,未动
- 下一件:diagnosis-system **story-002(词条表 schema)**,同协议;epic 链 diagnosis → case → prescription

### 前一收口(同日,已推送)= patient-ai story-004 `0b6f948`
- 4/4 全闭;过滤 168/166/0/2;残余 AC-13-V8(BLOCKED-BY 45)· CrossPlatform(CI)·
  [L] 五档(BLOCKED-BY 42/44)。⚠️ patient-ai EPIC 行仍 `Ready`(先例账,归该 epic 收尾轮)。

---

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-d(接图落地 + C4 消红)—— commit `155a9b1` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:上一轮评审后用户裁定「**先清 44 条 C4,再收口 019-d**」+「**抽主题变量 + 铜色,一并解决 4 条 C4**」。

### 交付物
- **接图(C7 接图半)**:四基类 `paper`/`scroll`/`ink`/`seal` 加 `background-image: url("guid:…")`;
  `.paper-aged` 变体随 `paper` 落地 ⇒ **落地 5 处选择器 / C7 判据面 4 注册项**
- **判据收窄**:`SkeuoComponentRegistry` 增列 `IsTextureContainer`(默认 `false`,须显式传 `true`)
  ⇒ C7 判据 = 「贴图容器类」(2026-10-05 用户裁定,取代首轮窄读法 4 类)
- **C4 消红 44 → 0**:`SkeuoThemeVariables.uss` layer **5 → 9 员**(brass/implement/marks/focus 抽变量)·
  `brass-bg` 取 art-bible §4.1 权威值 **`#B8863B`**(订正原内联 `#b87333`,绿通道差 19,非本项目裁定值)·
  `.brass-scale` 底色与 border **解耦**(评审 M4,独立命名 `--skeuo-brass-scale-color`)·
  焦点载体 **墨 → 铜**(承 `art-bible §7.4` Amendment + GDD 规则十注记)
- **夹具补真缺口**:新增 `validate_all_aggregate_test.cs`(**7 条**)—— `ValidateAll()` 此前**全 `Tests/` 零调用**,
  C1/C2/C4/C5/C6 在 CI **长期无覆盖**;`texture_binding_gate_test.cs` 两处 `null` 桩改真
  `AssetDatabase.GUIDToAssetPath`(019-c 遗留,接图后误判悬空)
- **文档订正 4 处**:「贴图容器 5 项」→「**4 注册项 / 5 落地选择器**」(story-019 ×2 · art-assets ×1 · GDD ×1)

### 验证(实测)
- 过滤:`unity/Logs/skeuo-019d-final.xml` = **224 / 211 passed / 0 failed / 13 skipped**
- 全量 EditMode:`unity/Logs/full-editmode-019d.xml` = **2446 / 2402 / 0 / 43 / 1**(基线 2445 ⇒ **+1,零回归**;
  唯一 inconclusive = 既有 `SettingsExposureTest.test_monoOption_existsWithValidDefault`)
- **门探针**:复刻 `RunMenu()` 契约(反射先调 private `InitializeDefaults()`)⇒ `ValidateAll()` = **0 条 · VERDICT=GREEN**
  (探针已删,日志 `unity/Logs/probe-019d-build.log`)
- **变异**:删 `.ink` 接图 ⇒ C7 精确报 `.ink` 缺贴图;还原 `#b87333` ⇒ C4 精确报 line 4;均还原后全绿

### ⬜ 待办 / 未闭登记(禁借绿)
- ⚠️ **019-d 残余义务**:`-unity-slice-*` 的**运行期实测 + 截图签核**仍未做 —— 现仅断言「有引用」,
  **证明不了「贴对了」**;切片值待 019-f 冻结件(故 C8 未闭)
- ⚠️ **未闭色值(待裁)**:`--skeuo-brass-aged`(`#A0653A`,注释自称「铜锈」)与 art-bible §4.1/§8.6.3 的
  铜锈 `#4F7A6B`(青绿)**语义冲突**;`#8C5A2B` art-bible 全文无出处。经 `git show HEAD` 确证均为**既有值**,
  本轮保值抽变量**未纠正**,已就地加警示注释
- **019-f(C8 冻结件)** = BLOCKED-BY 美术(九宫格 slice 真值;现 `spriteBorderActual=(0,0,0,0)`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结
- m1:`seal_red` 全库零挂载
- 下一件:**见 Phase 2 关键路径**(interaction-system 已 7/7 全闭;余 patient-ai / diagnosis / case / prescription)

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-e(导入格式订正)—— commit `7739142` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。
> 前置:用户裁定「拆成 DEF,c7 范围窄读法写死」⇒ 019-d 拆为 d(接图,美术)/e(格式,零依赖)/f(C8 冻结件,美术)。

### 交付物
- **生产**:16 个 `*-final.png.meta`(spriteMode 0→1 = **Single** · textureType 0→8 · alphaIsTransparency 0→1;`spriteBorder` 留零哨兵)·
  `TextureBindingGates.cs` 增 `ValidateSlicedTextureImportFormat` + `ValidateSpriteBorderLeftAsSentinel`
- **测试**:`texture_binding_gate_test.cs` **+5 条 E1 夹具**(22 条本件)

### 单轮评审 → 关键取证(QA 侧 BLOCKING 被实测推翻)
- QA 侧判「`spriteMode:1` = Multiple ⇒ 与单图九宫格矛盾」(据本机文档**文本顺序**推断)
- **独立取证推翻**:运行期反射 `SpriteImportMode` = **`None=0/Single=1/Multiple=2/Polygon=3`** ⇒ `spriteMode:1` **= Single**;
  引擎回读 16 张全 `mode=Single · spriteCount=1`(临时探针,日志 `unity/Logs/probe-enum.xml` / `probe-sprite-mode.xml`,探针已删)
- **但暴露真缺陷(文档级)**:原文括注 `(Multiple)` 是**误标**(自 commit `6049204` 引入,从未核过)
  ⇒ **已修** story-019 §AC-42-E1 + `art-assets-required-for-019`(改为「= `SpriteImportMode.Single`」+ 枚举真值)
- **绿**:过滤 217/204 passed/0 failed · 全量 EditMode 2439/2395/0/43/1(基线 2434/2390,+5 零回归)
- **变异**:MUT-E1a(spriteMode 回落)⇒1 红 · MUT-E1b(border 注入非零)⇒1 红(日志留档)
- **原件**:`production/qa/evidence/review-skeuomorphic-ui-story-019e-2026-10-05.md`

### ⬜ 待办 / 未闭登记(禁借绿)
- **019-d(接图)** = BLOCKED-BY 美术(逐变体映射语义;导入格式前提已由 019-e 解除)
- **019-f(C8 冻结件)** = BLOCKED-BY 美术(九宫格 slice 真值;现 `spriteBorderActual=(0,0,0,0)`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结
- 未闭:018-e 的 `-unity-slice-*` **运行期实测**归 019-d(截图签核)· `nPOTScale` 无门覆盖(登记为引擎派生值)
- 下一件:见 Phase 2 关键路径

---

## 📋 历史状态(2026-10-05)—— 拟物 UI story-019-c(贴图接入护栏)—— commit `a10fa6c` · 已推送

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。**评审只做一轮**。

### 交付物
- **生产**:`unity/Assets/Editor.Tools.Gates/TextureBindingGates.cs`(纯逻辑,零引擎依赖,照 `PresentationDtoGuard` 先例)·
  `SkeuomorphicUiGates.cs` 门聚合(C10/C11/C7 骨架半 → `ValidateAll`)
- **测试**:`unity/Assets/Tests/EditMode/SkeuomorphicUI/texture_binding_gate_test.cs`(**17 条**)

### 单轮评审(结构侧 unity-specialist + QA 侧 qa-lead)→ 修复轮
- **两侧独立收敛同一根因 = C10 主入口假绿**:`Directory.GetCurrentDirectory()` 在 Unity CLI EditMode 下
  = **`<repo>/unity`(工程根)**,非 `<repo>`;门以 `Path.Combine(cwd,"Assets",…)` 拼路径 ⇒ 拼不中 ⇒ 扫描**静默空跑**
  ⇒ `Assert.IsEmpty` 恒真。**变异测试证伪不了这类假绿**(注入物与扫描面同落泄漏目录)。
  铁证 `unity/Logs/probe.xml:46`;同族订正先例 `modal_gate_test.cs:502`(早已自陈「cwd = unity/」)
- 修复:① `DefaultRepoRoot` 上溯寻含 `Assets/` 的那层;② 三扫描函数加**反空跑守卫**(缺失/空 ⇒ 硬报错);
  ③ `UrlTargetsTextures` 由死代码降为诊断分级;④ 新增 5 条夹具(12→17)
- **变异**:MUT-C10 / MUT-C11 各 **2 红**(含真扫描面锚 —— 证 B1 关闭),两变异文件均还原
- **绿**:过滤 212/199 passed/0 failed · 全量 EditMode 2434/2390 passed/0 failed/43 skipped/1 inconclusive(基线 2429/2385,+5 零回归)
- **原件**:`production/qa/evidence/review-skeuomorphic-ui-story-019c-2026-10-05.md`

### 同批产出 = 019 完全实现所需美术资产清单
- `production/qa/evidence/art-assets-required-for-019-2026-10-05.md`
- **结论**:美术侧瓶颈**仅 3 件** —— ① 五族九宫格切图边界元数据(冻结件)· ② 逐变体映射语义裁定 ·
  ③ 铜族焦点黄铜 2px 最小切片(**唯一新出图**;M2 硬前置,E 裁)。16 张主贴图早已入库,非缺口。
- **实测附加发现**:元件库 USS 实有 **24 个类选择器**,而 `SkeuoComponentRegistry` 仅登记 4 类
  (记号族住 `MarkRegistry`;黄铜/器具/焦点族**零登记表**)⇒ **C7「已注册类」范围待裁**(决定 019-d 接图量 4 vs 24)。

### ⬜ 待办 / 未闭登记(禁借绿)
- **019-d(接图)** = BLOCKED-BY 美术(冻结件 + 映射裁定 + 导入格式订正 16 `.meta` 全 `spriteMode:0`)
- **019-b(图集预算 C9)** = Blocked,`PAGES_MAX` 未冻结(ADR-013 §6.6 假设 6 spike 未跑)
- **待裁**:C7「已注册类」范围 · AC-42-C7/C8/C9/C10/C11 未入 GDD(架构侧治理项)· C11 未覆盖 `resource()`/`.uxml`
- 下一件:见 Phase 2 关键路径(patient-ai story-003 = ViewState 投影 / cue 发射 / Material 映射)

---

## 📋 历史状态(2026-10-04)—— patient-ai(13)story-001 协议步骤 2/5

> 严格执行协议:**创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送**。
> **评审只做一轮**。

### 步骤 1 ✅ 已完成 —— 实现 + Unity CLI 测试
- **生产**(`unity/Assets/Gameplay.Presentation/PatientAI/`):`BehaviorBands.cs` · `BehaviorState.cs` ·
  `BehaviorMap.cs`(决策核心,修掉了 `_ = trend;` 不可达缺陷)· `PatientBehavior.cs`(滞回 + Terminal 闩锁 + 三判据)·
  `PresentPatientView.cs`(F-13.6 优先级表)· `PatientCue.cs`(13 命名空间:纯函数核 + `CueKind`/`CueIntervals`)·
  **`PatientCueEmit.cs`**(44 前缀命名空间:唯一发射桥)
- **测试**:`unity/Assets/Tests/EditMode/PatientAI/behavior_map_test.cs`(**38 条** —— 含后补的
  `test_ac13a5_trendIsInertInMap_branching`(MUT-B 首轮 0 红暴露的缺口)+ NaN/±Infinity 边界)
- **⚠️ 本轮最重的接线发现 = AC-44-B1 ① / AC-44-B3 双门**:
  ① **b5① 逃逸谓词按文件判** —— 含 44 契约 token(`AudioCueDto`/`IAudioCueSink`)的文件**必须且只能**
     声明 44 前缀命名空间 ⇒ 13 的调度面与发射桥**拆成两个文件**;
  ② **AC-44-B3 入口白名单扫「44 前缀下每个类型的公开方法签名」** ⇒ `Emit` 签名缩成**纯基元**
     (`int patientId` / `int kind` / `int cellX/Y/Z`),13 命名空间类型一律在**体内**装配。
- **绿**:patient-ai 38/38 · 全量 EditMode **2375 / 2331 passed / 0 failed / 43 skipped / 1 inconclusive**
  (inconclusive = 既有 `SettingsExposureTest`,与本 story 无关;全量数取自 36 条时点,38 条后未重跑全量)
- **变异证明**:MUT-A(删夹取)⇒2 红 · MUT-B(给 `Map` 加 trend 分支)⇒ 首轮 **0 红(缺口)**,
  补 `test_ac13a5_trendIsInertInMap_branching` 后 ⇒ 红 · MUT-C(删 Terminal 闩锁)⇒2 红

### 步骤 2 ✅ 已完成 —— 双代理评审(单轮)
- 结构侧(`aa64bdaa32ad46640`)= **CHANGES REQUIRED**(无 BLOCKING;F-1…F-7)
- QA 侧(`a21992cb2ace8b063`)= **REJECT**(2 BLOCKING:B1 扫描根阴性恒真 · B2 只证「持了接口」;
  M1/M2 · m1/m2/m3 · NOT-RUN 10 条)
- ⚠️ 两位曾停在轮次上限(转录 idle ≈73 min),按「子代理满轮可以接着再送」显式重送后收回
- **原件落盘**:`production/qa/evidence/review-patient-ai-story-001-2026-10-04.md`

### 步骤 3 ✅ 已完成 —— 修复轮(逐条落实 + 变异坐实)
- **B1/M2**:`ScanClosureForNames` 生产根改 `ProductionScanSeeds()`(13 前缀 **∪** 44 桥前缀)
  + 新增 `test_ac13a1_scanRootCoversAudioBridgeNamespace_b1` 锁根枚举
- **B2**:新增 `test_ac13a2_vitalsProducedOnlyByIVitalsQuery_sourceClosure` + 负夹具
- **F-3/M1**:`test_ac13b5_sessionWriterIsUnique_reflection` 重写为 **IL 写入点扫描**
  (`ScanSessionWriters`:stfld 后备字段 ∪ call set_Session)+ 负夹具 `ShadowSessionWriter`
- **F-2**:`PatientBehaviorDirector` ctor 调 `Validate`,破表 `throw ArgumentException`
- **m3**:`BehaviorBands.Validate` 补 `SeekMin < DeathBandMin` 独立项
- **m1**:trend 惰性断言补带符号邻域球(±1e-6/±1e-3/±0.01/ε)
- **m2**:`ResetForLoad` 补方向对照负夹具
- **M2 注释订正**:如实声明扫描器归一化口径 ≠ `PresentationDtoGuard.NormalizeMemberName`

### 步骤 4 ✅ 已完成 —— 复跑绿
- **patient-ai 45/45**(38 → 45,+7)· 全量 EditMode **2384 / 2340 / 0 / 43 / 1**
- **变异 5/5 各恰一条红**(MUT-B1/B2/F2/F3/m3,日志 `unity/Logs/mut-*.xml`)

### 步骤 5 ✅ 已完成 —— 收口提交推送(`4076e1e`,已 push)
- 22 文件 · 排除 `unity/Assets/unity.meta` + `unity/Assets/unity/Logs.meta` + `.gitignore`
- ⬜ 提交 → ✅ 推送 origin/main

### 待办(已闭)
- ✅ 评审回收 → 修复轮 → 复跑绿 → 收口提交推送(`4076e1e` / `eb8b718`)
- ✅ `sprint-04.md` §Phase 2 表与关键路径图更新(interaction 全闭、patient-ai story-001 已收口)
- ⛔ `unity/Assets/unity.meta` + `unity/Assets/unity/Logs.meta`(早前相对路径测试输出误建的空树,
  `Logs/` 本身已被 `.gitignore` 覆盖)—— 已排除出提交;删除需用户批准(`rm -rf` 被拒)

---

## ✅ patient-ai(13)story-002 —— 空间行为(轨 A)—— 收口 2026-10-05

### 交付物(4 生产件 + 1 测试件 = 78 条)
- `SpatialPerception.cs`(F-13.3 整数平方和禁 sqrt · F-13.4 LOD 三档 + 节流判据 · EC-13-02 量程护栏)
- `ClinicKnowledge.cs`(F-13.2 `HomeRegion` / `KnowsClinic` 两事实析取)
- `LogicalStepper.cs`(F-13.7 累加器步进 `acc` · `Moving(p)` 五合取项 · 防跳格断言)
- `PatientSpatialDirector.cs`(在场循环 · 升序求值 · `AtClinic` 格成员判定 · `PhaseOf` / `EcozoneOfCallCount` 可观测面)
- `spatial_behavior_test.cs` = **78/78 绿**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(4 MAJOR)· **QA 侧 REJECT**(F-1 MAJOR + F-2/3/4 MAJOR)—— 两侧独立收敛到**同一组四条**
- 修复:**F-1 `AtClinic` 改回 GDD 的格成员判定**(注入 `Func<WorldPos,bool> inClinicCells`;
  旧实现用路径游标代偿 ⇒ 假阳/假阴/单格路径三向皆错,下游 F-13.6 `AwaitingCare` 直接受害)·
  **F-2 TC-7 换顺序敏感场景**(哈希序 ≠ 升序序键集 + `onEvaluated` 求值序探针)·
  **F-3 `EcozoneOfCallCount`**(「HomeRegion 只求一次」可证伪)·
  **F-4 AC-13-E3 IL 引用扫描**(`Math.Sqrt`/`Physics.Raycast`/NavMesh,影子件共用机器)
- **变异证明**:MUT-F1a(还原旧 `atClinic` ⇒ 恰 2 红)· MUT-F2(删 `ids.Sort()` ⇒ 恰 1 红)·
  MUT-F3(`HomeRegion` 每 tick 重求 ⇒ 恰 1 红)· MUT-F4(生产件注入 `Math.Sqrt` ⇒ 恰 1 红)
- **评审原件**:`production/qa/evidence/review-patient-ai-story-002-2026-10-05.md`

### 未闭登记(NOT-RUN,禁借绿)
- **NR-S2-1 `ClinicCells` 真实数据接入**(本 story 只接注入谓词,未接 24 `CONTEXT_TABLE` ∪ 52 `CLINIC_FRONT`)
  ⇒ **story-003 前置**(F-13.6 `AwaitingCare`)· NR-S2-5 `ShouldDecideNow` 驱动接入 ⇒ story-003/004 ·
  NR-S2-6 `ResetForLoad` 真负夹具 · NR-S2-7/8 story-001 遗留 5 项仍开

### 待办
- ✅ 收口提交推送
- ✅ **patient-ai story-003 已收口**(见下节)
- ⬜ **下一件 = patient-ai story-004**(或按 epic 内剩余 story 序 —— 见 sprint-04 关键路径)

---

## ✅ patient-ai(13)story-003 —— 呈现投影与视图 API —— 收口 2026-10-05

> 承「严格执行:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。评审只做一轮」。

### 交付物
- **生产**:`PresentationProjection.cs`(ViewState 投影链 / `IPresentPatients` 视图 / `MaterialTable` 查表器)·
  `PatientCueSchedule.cs`(cue 调度 + 呼吸层生命周期;终局 = 呼吸停止 + 姿态落最静止档)
- **测试**:`presentation_projection_test.cs`(**131 条**,AC-13-A4/A5/C1…C5/D1…D6/F1…F3)
- **证据**:`production/qa/evidence/patient-ai/story-003-accessibility-signoff.md`([L] 项)·
  `production/qa/evidence/review-patient-ai-story-003-2026-10-05.md`(评审原件)

### 单轮评审 → 修复轮(三条 BLOCKING)
- **G(真 bug)**:`Decide` 首拍 `firstDue = phase` 把相位当**绝对 tick** ⇒ 真实入场 tick 下恒真 ⇒
  **去同步彻底失效**(实证 6 id 全 due)。修复:增 `entryTick` 形参,`firstDue = entryTick + phase`
- **A/C2**:AC-13-C2 走程序集引用面而 37 程序集**不存在** ⇒ 恒真借绿 ⇒ 改**源码面 grep**
- **B/C4**:AC-13-C4 只扫成员名 ⇒ 改 **IL 引用面**(看得见 new GameObject/Object.Destroy/Instantiate)
- MAJOR:AC-13-D3 姿态半边零承载(增 `PostureTier` 字段)· D6 分段常函数测恒真(改行为面)·
  C3 负夹具恒真(影子真计数)· A4 名字面(改 IL 面 + NOT-RUN 剥离半边)· F1/F2 过claim(改名 `*_structuralHalf_*`)
- **变异证明**:MUT-G/B3/H2/C2 逐个注入 ⇒ 必红且点名。⚠️ MUT-H 首轮暴露 D6 修复只测首拍会漏网 ⇒ 补稳态半边

### 验证
- patient-ai **131/131 绿**(`unity/Logs/s003-final.xml`)
- 全库 EditMode **2455 passed / 0 failed / 1 inconclusive / 43 skipped**(`s003-fixgreen.xml`,**零回归**)
- 提交 `a1f7a36` 已推送 origin/main

### 未闭登记(NOT-RUN,禁借绿)
AC-13-D1 达成(表落盘 + 44 签署)· AC-13-A4 剥离半边(构建产物探针,EditMode 不可达)·
[L] 无障碍达成面(AC-13-F1 归 42 冗余通道 / AC-13-F2 归 44 空间化 + 用户拍定 `PERCEPT_R`)·
AC-13-D3 姿态的**呈现**归 42 —— 见 signoff NR-S3-1…4。承 story-002 的 NR-S2-1(真 `ClinicCells`)继续滚入。

---

## 📌 历史 —— patient-ai(13)story-002 规格与依赖面(已兑现)

> 用户令「马上开始轨 A」⇒ 关键路径续行:**13 story-002/003/004 → 8 → 37 → 11**。
> 承「严格执行:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送。评审只做一轮」。

**story-002 规格**(`production/epics/patient-ai/story-002-spatial-behavior-perception-and-stepping.md`)——
感知格距 `Perceives = Δx²+Δy²+Δz² ≤ PERCEPT_R²`(禁 sqrt)· HomeRegion = `EcozoneOf(spawn_anchor(p))` ·
LOD 按 d² 三档 · 逻辑格步进(定点累加器 `acc`,Q16.16) · `Moving(p) := ¬Frozen ∧ State ∈ {Seeking}` ·
`Seeking{EnRoute/AtClinic}` 双相 · 升序 id 求值 · 解冻不补算。

**依赖面**:story-001 ✅ · 6 的 `EcozoneOf` / `spawn_anchor`(world-ecozones Story 002/003 — 需确认落点)·
9 的在场判定接口 · 整数导航格(23/27 基础设施,消费级)。

**下一步动作**:① 确认 `EcozoneOf` / `spawn_anchor` / 在场判定 三处消费面**是否已在库**
(未落则登记消费点桩 + NOT-RUN,禁借绿);② 读 `design/gdd/patient-ai.md` F-13.2/F-13.3/F-13.4/F-13.7 + B/E 组 AC 原文;③ 落实现。


## 📊 全项目进度总览（2026-10-03 实测重算）

> ⚠️ **本表于 2026-10-03 按各 story 真件逐件重算**(口径:`> **Status**:` 首行 + 体 `**Status**: [x]`)。
> 下表为**实测值**;旧值(124 Complete / 6 Ready / 77 In Progress / 59.9%)系**陈旧转录**,已废。

### 阶段状态

| 项 | 值 |
|---|---|
| **Stage** | Pre-Production |
| **Sprint** | sprint-03 ✅ 已闭(17/17) · **sprint-04 Phase 1 ✅ 已收口** · **Phase 2 进行中 = 4/7 系统完成**(patient-ai story-001/002/003/004 已收口)—— 关键路径移至 diagnosis-system |
| **Gate Check** | CONCERNS（2026-09-29 二轮，无 NOT READY 阻塞） |
| **ADRs** | 28/28 Accepted |
| **P0 GDDs** | 31/31 Approved |
| **Commits since 09-22** | 375 |

### Epic 故事进度

> **列语义(防混计 · 2026-10-03 明写)**:四列为**互斥且穷尽**的分类,口径 = story 件 `> **Status**:` 首行。
> - **Complete** = 首行含 `Complete`(或体 `**Status**: [x]`)
> - **Ready** = 首行为 `Ready` **或 `In Review`** —— 二者皆「story 已实现但 epic 未收口」,
>   本表**不分开列**(此为既定口径,非疏漏;`In Review` 的明细读「备注」列)
> - **In Progress** = 首行含 `In Progress`(**实测恒为 0**)
>
> ⚠️ **旧表误读的成因**:把「`In Review`」当成 `In Progress` 计 ⇒ 虚报 77。
> **`In Review` ≠ `In Progress`** —— 前者是「已实现待收口」,后者是「实现进行中」。
> 同理 **`In Review` ≠ `Complete`**(「不得借绿」)。

| Epic | Stories | Complete | Ready | In Progress | 备注 |
|------|---------|----------|-------|-------------|------|
| **audio-system (44)** | 14 | **14** | 0 | 0 | ✅ 全收口 |
| **skeuomorphic-ui (42)** | 18 | **18** | 0 | 0 | ✅ 全收口 |
| **skill-system (30)** | 8 | **8** | 0 | 0 | ✅ 全收口 |
| **telemetry-analytics (51)** | 10 | **10** | 0 | 0 | ✅ 全收口 |
| **input-system (3)** | 13 | **13** | 0 | 0 | ✅ 全收口 |
| **item-database (21a)** | 12 | **12** | 0 | 0 | ✅ 全收口 |
| camera-viewpoint (2) | 6 | **6** | 0 | 0 | ✅ 全收口(2026-10-03) |
| casebook (39) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| case-system (37) | 4 | **4** | 0 | 0 | ✅ 全收口(2026-10-06) |
| clinic-machine (24) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| combat-weapons (25) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| death-respawn (29) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| diagnosis-system (8) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| disease-simulation (9) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| emergency-procedures (10) | 7 | **7** | 0 | 0 | 🔶 **story 7/7 Complete;EPIC 未收口**(A1/A2/A6/B4/C4/C5 已闭;✅ 评审原件两份均在库,**旧记「评审原件缺」为方向性错记**;**真实残留 = D1/D2/D3 文档对齐 + 实跑测试套件**(round2 自陈未实跑,「0 红」系读源码而非执行)) |
| enemy-ai (27) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| foraging (17) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| interaction-system (4) | 7 | **7** | 0 | 0 | ✅ **全收口 2026-10-04**(001…006 + **007 装载接线 · NR-1 已闭**;**未闭登记 = NR-2 真表数值轮 · NR-3/4/5 三条 `[L]` 走查 · NR-6 `OQ-17-3` · 007 新增 4-DC-4①/跨会话/§三其余规则 等七条 NOT-RUN**) |
| inventory-items (20) | 6 | 0 | 6 | 0 | ⬜ 未启动 |
| medical-consequences (53) | 4 | 0 | 4 | 0 | ⬜ 未启动 |
| modular-building (23) | 7 | **7** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（C1/C2/N-r1/C8-ID 全闭 · 本轮 72/72 绿 · 全量 2204/2163/0红，`9bb912b`+`bfa6234`;**未闭登记 = N-r2 生产装配根 + AC-23-09 跨平台签名**） |
| patient-ai (13) | 4 | **4** | 0 | 0 | ✅ **全收口 2026-10-05**(story-001/002/003/004;未闭登记 = V8 联机 BLOCKED-BY 45 · 跨平台 EXTERNAL · [L] 五档可读性部分闭) |
| player-controller (1) | 6 | **6** | 0 | 0 | ✅ **Complete ✅ 2026-10-03**（两轮评审判据缺陷已修;88 过 + 3 NOT-RUN，`a78c27a`） |
| prescription-medication (11) | 5 | 1 | 4 | 0 | 🔄 **In Progress** — story-002 ✅ 2026-10-06(F-11.1 剂量定点化;23/23 绿;双代理评审修复轮全闭) |
| processing (18) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| random-events (52) | 6 | **6** | 0 | 0 | ✅ 全收口 |
| time-weather (5) | 5 | **5** | 0 | 0 | ✅ 全收口 |
| tutorial-onboarding (48) | 5 | 0 | 5 | 0 | ⬜ 未启动 |
| world-ecozones (6) | 6 | **5** | 1 | 0 | ✅ **Complete ✅ 2026-10-03**（6/6 story;N1/N5 已修 · 本轮 109/109 绿;**未闭登记 = N3 白名单判据(待 27 侧落地)+ Story 005 [L] 走查 EXTERNAL**) |
| persistence-service (7a) | 2 | **2** | 0 | 0 | ✅ 全收口（002 = ADR-029 契约支） |
| save-slot-ui (7b) | 1 | 0 | 1 | 0 | ⬜ 未启动 |
| **合计** | **207** | **137** | **70** | **0** | **66.2% 完成(137/207)** |

> ⚠️ **2026-10-03 实测重算(当前口径)**:按各 story 真件逐件解析 ⇒ **207 / 136 Complete / 0 In Progress / 71 Ready**。
> 与上一版(124 / 6 / 77 / 59.9%)的差额来源:
> ① `modular-building` 6 → **7** 且 0 → **7 Complete**(story-007 = ADR-029 接线支;本轮并闭 C1/C2/N-r1/C8-ID → 转 Complete);
> ② `world-ecozones` 5 → **6**,4 → **5 Complete**(story-006 = ADR-029 接线支;转 Complete);
> ③ `player-controller` / **`emergency-procedures`** 由「Complete」/「In Review」重分类 —— 前者转 Complete,后者按 story 真件归 In Review(7 件,非旧记 6);
> ④ `persistence-service` 002 契约支 ⇒ 1 → **2 Complete**;`save-slot-ui` 归 Ready(非 In Progress);
> ⑤ **`camera-viewpoint` 6 Complete** —— 旧表已记 ✅,本轮无变;
> ⑥ 旧「In Progress 77」系把 `In Review` 与 `Ready` 混同计;实测 **In Progress = 0**(无 story 件标 `In Progress`)。
> 重算口径:逐 story 件解析 `> **Status**:` 首行 + 体 `**Status**: [x]`;`Complete*` / `In Review*` / `In Progress*` / 其余=Ready。

> 📌 **历史转录订正记录(保留闭环)**:原记「191 / 93 / 95 / 6 / 49%」及 2026-10-02 口径「203 → 206」
> 均已作废,勿再引用。成因同族:总数漏计 `persistence-service` / `save-slot-ui`,
> 且把 `In Review` 与 `In Progress` 混计。

### 测试状态

| 指标 | 值 |
|------|---|
| EditMode | **2204 total = 2163 Passed · 0 Failed · 40 Skipped · 1 Inconclusive**（2026-10-03 batchmode 实测，Unity 6000.3.24f1） |
| PlayMode | 25/25 Passed · 0 Failed（2026-10-01 实测） |
| 确定性验证 | F7 反汇编 CLEAN · AC-29 三平台逐位一致 |

> ⚠️ **上表 EditMode 一行 2026-10-03 更新为当前 HEAD 实测**(2204/2163/0/40/1)。
> 历史链条(保留闭环):原记「1831/1858」出自 `185063f`,该提交**编译未通过**
> (`Scripts have compiler errors`)⇒ 测试从未跑起来,数字**无源**;`5572d66` 修复编译后 = 1989/2022;
> 2026-10-02 = 1995/2028;2026-10-03(本轮,三 epic 收口后)= **2204/2163**。
> ⚠️ 旧行把 `total` 写成分子分母两个数(1995/2028),易误读为「1995 通过 / 2028 应为」——
> 现行口径按 `total = passed + failed + inconclusive + skipped` 记账。
> exit code 2 源自唯一 Inconclusive(`Audio/SettingsExposureTest.test_monoOption_existsWithValidDefault`,既有项),**非失败**。

### 关键里程碑

| 日期 | 事件 |
|------|------|
| 2026-09-20 | `/create-architecture` 完成，architecture.md v1.0 |
| 2026-09-20 | ADR-023/024/025 起草并 Accepted（Required #1/#2/#3） |
| 2026-09-21 | Gate Check 一轮 FAIL → 用户承接 → CONCERNS |
| 2026-09-22 | U0a 工具链闭合 · UX Review Phase 3A · 批裁轮 OQ 全结 |
| 2026-09-23 | ADR-026/027/028 Accepted（Required #4/#5 + 音频归属） |
| 2026-09-24 | AC-29 三平台确定性验证通过 · F7 反汇编 CLEAN |
| 2026-09-25 | 数值批三批全拍 · R13 回写闭环 · 44 二轮修订 |
| 2026-09-26 | 音频 Story 001-004 完成 · 技能/遥测/输入/UI 全推进 |
| 2026-09-27 | 音频 Story 005/006/014 完成 · 986/986 测试全绿 |
| 2026-09-28 | 音频 Story 007-013 完成 · 1175/1175 测试全绿 |
| 2026-09-29 | Gate Check 二轮 CONCERNS（无阻塞） |
| 2026-09-30 | active.md 刷新 |
| 2026-10-01 | Sprint 02 全部完成（16/16）· Sprint 03 部分完成（11/17） |
| 2026-10-01 | modular-building / world-ecozones 越序实现（架构依赖先行） |
| 2026-10-01 | 冲突解决提交 c291873 |

### Sprint 状态

| Sprint | 计划时间 | 实际状态 | 备注 |
|--------|---------|---------|------|
| Sprint 01 | 10-05 ~ 10-18 | ✅ 8/8 Complete | 提前完成 |
| Sprint 02 | 10-19 ~ 11-01 | ✅ 16/16 Complete | 提前完成 |
| Sprint 03 | 11-02 ~ 11-15 | ✅ 17/17 story Complete | 完成（2026-10-02）· ⚠️ **AC-S03-5 黄金夹具未兑现**（emergency NOT-RUN · combat 零存在;根因 = ADR-012 矩阵未激活） |

### 交付物
- **生产**:`InteractionSelector.cs` —— 第一键改测 `d∞(playerCell, cell)`(评审外发现:原测「到原点」,
  与 F-4.1 判定式不符,是 F-4.1b 三例失败真因)· `IsBetter` static + 三键字典序 +
  `KindPriorityOf` 查表 + `Chebyshev(a,b)` int64 两参
- **测试**:`target_selection_test.cs`(**13 条**)· `replay_selection_test.cs`(**4 条**)—— 全部断言**直接驱动生产**
- **评审原件**:`production/qa/evidence/review-interaction-story-002-2026-10-04.md`

### 实跑(2026-10-04 修复轮)
- `unity/Logs/interaction-s002-final.xml` = **41/41 green**(24 边界 + 13 + 4)

### 双代理评审 → 修复轮(已完成)
- **结构评审 REJECT**(3 BLOCKING)· **QA 评审 REJECT**(F-1…F-13)⇒ 逐条修复
- 修复:全部断言改绑生产 · int64 夹具命中真陷阱 · 哈希序负夹具驱动生产 · F-4.1b 三例逐字照抄 ·
  补两条 QA 案例(到达序打乱 + 缓存上一目标负夹具)· 闭集计数改 Enum.Length
- **变异测试证可红**:MUT1(删 int64 拓宽)/MUT2(d∞ 从原点)/MUT3(删键③)/MUT4(键③ 哈希序)/
  MUT5(删键②)—— 逐项令对应测试红,生产已还原

### 待办
- ✅ story-002 收口提交推送(12133f7 前的 13891b3/893f3a3)

---

## story-003(候选集四源构造)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`SourceDtos.cs`(九个具名输入形状)· `CandidateSources.cs`(四源只读接口 +
  装配包)· `CandidateSetLoader.cs`(四源合并 · **无玩家格形参** = 取路 (a) 结构保证 ·
  不裁剪 · 不持 sink)
- **测试**:`candidate_dto_reflection_test.cs`(**14 条** AC-4-03)·
  `candidate_sources_test.cs`(**11 条** AC-4-20/22 + 装载形状)
- **评审原件**:`production/qa/evidence/review-interaction-story-003-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s003-final.xml` = **56/56 green**(14 + 11 + story-001 24 + story-002 7)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **QA ACCEPT**;F-3(装箱判据循环论证)/ F-4(基类展开零覆盖)/ F-5(边缘登记)逐条修复
- 结构侧无独立代理原件(代理触顶未回)⇒ 主会话复核通过,缺口登记在案
- **变异证明可红**:MUT-base(删基类展开 ⇒ 恰单条红 55/56)

### 待办
- ✅ 收口提交推送(12133f7,已 push)
- ⬜ story 004(R_INTERACT 邻域裁剪)—— interaction-system 下一件

### 交付物
- **生产**:`DiscoveryRequest.cs`(F-4.3 三字段载荷)· `InteractionRadius.cs`(单源持有者,4-DC-1 下界)·
  `KindRouteTable.cs`(路由表双向闭合,RegisteredSystems 10 值)· `NeighbourhoodReporter.cs`(广播自报)·
  `IDiscoveryReporter.cs`(Request 签名改为载 `DiscoveryRequest`)· `InteractionSelector.cs`(半径单源化)
- **测试**:`discovery_report_test.cs`(F-4.3/4.3b/4.4 + AC-4-13)· `radius_single_source_test.cs`(AC-4-17 + 4-DC-1 下界)·
  `kind_route_closure_test.cs`(AC-4-18 + 4-DC-5)
- **评审原件**:`production/qa/evidence/review-interaction-story-004-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s004-final2.xml` = **86 / 83 passed / 0 failed / 3 skipped(NOT-RUN 机检)**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 APPROVED WITH SUGGESTIONS** · **QA 侧 ACCEPT-WITH-FIXES**;两件原件皆落盘
- 修复:扫描器分叉(删属性分支)· 6 半改机检 NOT-RUN · 帧率测试重写为「计数=调用次数」·
  新增「无主动交互 ⇒ 零出境」负夹具 · 半径扫描器已知限制登记 · RegisteredSystems 7→10(PF-1)
- **变异证明可红**:mut-cheb(丢 int64 拓宽 ⇒ 恰 1 红)· mut-gate(删主动交互门 ⇒ 恰 1 红)

### 待办
- ✅ 收口提交推送
- ⬜ 系统 6 Epic(接收侧:latch/幂等/判距复验/唯一 Append)—— 转绿 AC-4-13 6 半 + AC-4-17 6 消费半

<!-- STATUS -->
Epic: patient-ai
Feature: 重建与写路径
Task: story-004 收口(4/4 · 166/166/0/2)
<!-- /STATUS -->

---

## story-005(模态门与路由边沿)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`ModalGate.cs`(`IModalGateState` 单布尔消费契约 + `IArmedState` + `ModalGate.Accept/Select/SetMotorSuppression`)
- **测试**:`modal_gate_test.cs`(AC-4-09/10/19 + 规则九,含选择器自报计数接缝、单 bool 可执行影子、闭集基数守卫、零出现 `ModalId` 断言)
- **评审原件**:`production/qa/evidence/review-interaction-story-005-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s005-final.xml` = **101 / 98 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING;F-2…F-11)· **QA 侧 ACCEPT-WITH-FIXES**(F1…F6);两件判定皆回填原件
- 修复:F-1 丢弃接缝改选择器自报计数 · F-2 删 ordinal 死代码 · F-3 闭集基数守卫 · F-4 单 bool 可执行影子 ·
  F-6 cref 订正 · F-8 零出现 `ModalId` · F-9 正向对照反空转门
- **变异证明 6 项全可红**:MUT-A(删模态门 → 2 红)· MUT-B(排队替代丢弃 → 2 红)· MUT-C(清全位图 → 3 红)·
  MUT-D(闭集 +1 员 → 1 红)· MUT-E(生产引用 `ModalId` → **编译错**,方向性实证)· MUT-E2(代码级串 → 3 红)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 方向① 生产实现体(归 10)· NR-2 方向② 适配器(归 42)· NR-3 AC-4-10 3 侧扫描(归 story-006)·
  NR-4 动态第 8 屏夹具 · NR-5 运行期消费者接线

### 待办
- ✅ 收口提交推送
- ⬜ story-006(数据契约构建期校验与呈现/手柄验收面)—— interaction-system 下一件


---

## story-006(数据契约构建期校验与呈现/手柄验收面)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindTable.cs`(`KindContractRow` + `DurationOwnerKind` + `InteractionKindTableValidator` 六条校验
  + **`KindPriorityTable`** 第二键唯一真源)· `RoutedSystems.cs`(被路由系统集单一来源)
- **测试**:`data_contract_validation_test.cs`(**13 条**:6 条 DC 夹具 + 4-DC-4 W/H/D + 4-DC-6 归属方 + 4-DC-3 行⟷表
  + AC-4-21 机器代理 + 反空转门 + 独立性)
- **评审原件**:`production/qa/evidence/review-interaction-story-006-2026-10-04.md`

### 实跑
- `unity/Logs/interaction-s006-final.xml` = **116 / 113 passed / 0 failed / 3 skipped**(3 = story-004 NOT-RUN 机检)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING 死代码;F-2…F-9)· **QA 侧 REJECT(AC-4-15)**(F-0 skip 归因更正;F-1…F-7)
- 修复:**真表单一来源接线**(F-2,消除「校验的表 ≠ 选择的表」)· 补 4-DC-4 W/H/D 半边(F-1/F-2)·
  `DurationOwner` 拆形态 + id 并断登记(F-3)· `RegisteredSystems` 单一来源(F-4/F-5)·
  **AC-4-21 机器代理**落地(F-3)· story 登记 NOT-RUN + 实际夹具数(F-6/F-7)
- **连带定向修复 story-002**:真表接线后 `target_selection_test` 4 条断言方向相反 ⇒ 按真表更新(MUT-7 先复现)
- **变异证明 10 项全落盘**:MUT-1…6(六条 DC 逐条可红)· **MUT-7**(接线真表 ⇒ story-002 恰 4 红,证接缝真实)·
  MUT-A(4-DC-4 W/H/D)· MUT-B(4-DC-6 归属方)· MUT-C(4-DC-3 行⟷表)

### 未闭登记(NOT-RUN,禁借绿)
- NR-1 装载器/烘焙接线(归 **story 007**)· NR-2 `KindPriorityTable` 真表(数值轮)·
  NR-3/4/5 三条 `[L]` 走查(可玩构建)· NR-6 4-DC-6 对 17/20 的真实时长登记(`OQ-17-3`)

### 待办
- ✅ 收口提交推送
- ✅ **interaction-system Epic 收口**(6/6 Complete · 2026-10-04;未闭项已显式登记)
- ✅ **story-007 装载接线收口**(NR-1 已闭 · 2026-10-04;见下)

---

## story-007(interaction_kinds 烘焙接线 · NR-1)—— ✅ 收口 2026-10-04

### 交付物
- **生产**:`InteractionKindBinder.cs`(阶段 2 绑定 + **唯一 `Validate(...)` 调用点** —— 消解 story-006 F-1 死代码)·
  `InteractionKindBaker.cs`(仓根种子 → 产物)· `InteractionKindCookedWriter.cs` + `InteractionKindCookedCodec.cs`(镜像编解码)·
  `InteractionKindBinderProbe.cs`(测试可见薄转发)· `DataBakeMenu.BakeInteractionKinds`(菜单调用点)
- **测试**:`interaction_kinds_bake_test.cs`(**19 条**)· `tests/unit/interaction/fixtures/`(15 夹具 / 11 负)

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **结构侧 CHANGES REQUIRED**(F-1 BLOCKING = 首轮自造 4-DC-4 ③)· **QA 侧 REJECT**(无 BLOCKING 安全洞;F-0/F-3/F-6/F-7 MAJOR)
- 修复:**删自造判据 ③**(收回 4-DC-4 ①②,与 GDD 一字对齐)· **4-DC-4 ② 接生产接缝**(W/H/D 只注入 `SlotLinearKey` 行 + 行内自报)·
  **维度随产物落盘**(writer/reader 头部扩展,(D) 改读 `ds.*`)· **ConfigVersion 测试前置守卫** · **跨会话范围订正**
- **变异证明**:MUT-A(删 `Validate` 调用 ⇒ 7/7 负夹具红)· MUT-B′(4-DC-4 ② 承重)· MUT-F7(维度落盘可证伪,`unity/Logs/s007-mut-f7.xml`)
- **评审原件**:`production/qa/evidence/review-interaction-story-007-2026-10-04.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- 4-DC-4 ①(类型面恒真不可达)· 跨会话逐位一致(只证同进程)· 4-DC-6 归属方未登记半边(编译期常量无注入点)·
  `schema_version` 交叉一致性(守卫短路)· 聚合多错纪律 · 4-DC-6 对 17/20(`OQ-17-3`)· §三其余绑定层规则

### 待办
- ✅ 收口提交推送
- ⬜ interaction-system 7/7 全闭;下一系统见 Phase 2 关键路径

---

## diagnosis-system story-004(F-8.3 阴性把握度与不泄漏不变量)—— ✅ 收口 2026-10-06

### 交付物
- **生产**:`DiagnosisNegativeConfidenceBinder.cs`(阶段 2 绑定 + **唯一校验点**)·
  `DiagnosisNegativeConfidenceBaker.cs`(仓根种子 → 产物)· `DiagnosisNegativeConfidenceCookedWriter.cs` +
  `DiagnosisNegativeConfidenceCookedCodec.cs`(镜像编解码)· `DiagnosisNegativeConfidenceBinderProbe.cs`(薄转发)·
  `DiagnosisNegativeConfidenceTable.cs`(定表 + 求值器,运行期读侧)· `DataBakeMenu.BakeDiagnosisNegativeConfidence`(菜单调用点)
- **作者态**:`assets/data/diagnosis_negative_confidence.json`(5 合成旋钮)
- **测试**:`confidence_leak_test.cs`(**39 条**)· `tests/unit/diagnosis_system/fixtures/neg_conf_*.json`(**14 个** / 9 负)

### 实跑
- filter `unity/Logs/story004-fix2.xml` = **39 / 39 passed / 0 failed**
- 全量 `unity/Logs/story004-fix3-full.xml` = **2669 / 2622 passed / 0 failed / 46 skipped / 1 inconclusive**

### 单轮评审(承「评审只做一轮」)→ 修复轮
- **两位评审均无 BLOCKING**(A:1 MAJOR · 5 MINOR · 4 NIT;B:APPROVED WITH SUGGESTIONS · 1 MAJOR · 3 MINOR · 4 NIT)
- 修复:**MAJOR-1** AC-8-12 (b) 承重断言恒真 ⇒ 改证接缝实参表 + 反向响应(登记口径替代)·
  **MAJOR-2** 消重 `Q16One`(唯一换算出口 `Table.RawToFloat`)+ `test_q16one_matchesFixCanonical` 逐位锚 `Fix.ToFloat()`·
  `CurveAt` 退化表**硬失败**(原静默 `0f` 会伪装 C-3 报警)· `schema_version` 构建期**同源**校验(堵「漏到运行期装载」)·
  AC-8-14 扫描面扩至类型名/属性名 + 非空守卫 · AC-8-10 删不可达分支 · AC-8-18 去 `??` 静默回退
- **变异证明**:MUT-A(权重路径)7 红 · MUT-B(去 clamp)首轮 0 红 → 补测后**恰 1 红** ·
  MUT-C(去极性门)首轮 0 红 → 补 `neg_conf_high_fallback.json` + 测后**恰 1 红** · MUT-D 恰 1 红
- **金标重钉**:`f75a8170` → `a7bfec31`(story-004 四常量)→ **`72db379c`**(修复轮消重少一行)
- **评审原件**:`production/qa/evidence/review-diagnosis-story-004-2026-10-06.md`

### 未闭登记(NOT-RUN,禁借绿 —— 覆盖缺口,非安全洞)
- AC-8-F5 跨平台三格矩阵(ADR-012 未实跑;只证 Mono 侧自洽)· AC-8-16 跨进程半边(只证同进程 N ≥ 10⁴)·
  AC-8-13 端到端(依赖 9 侧 `Project` 未落地)· AC-8-35 曲线参数半边(ConfigVersion 覆盖)·
  `ReadF32` 不拒 NaN/Inf(结构性不可达)· codec 不校验 `skillCap == SKILL_CAP`(姊妹件同形,须两处同改)·
  运行期/编辑期镜像无机械约束(技术债)· `DiagnosisReadFloorBinder.ReadSchemaVersion` 同形缺口(归 story-003 后续轮)

### 待办
- ✅ 收口提交推送
- ⬜ diagnosis-system 4/4 全闭;下一系统见 Phase 2 关键路径
