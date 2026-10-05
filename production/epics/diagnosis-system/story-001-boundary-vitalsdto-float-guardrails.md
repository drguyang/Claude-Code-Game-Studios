# Story 001: 程序集边界与 VitalsDto 只读门面

> **Epic**: 诊断与体征揭示
> **Status**: Complete ✅ 2026-10-05(双代理单轮评审 → 修复轮 → 25/25 绿 + 全量 0 failed)
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-05

## Context

**GDD**: `design/gdd/diagnosis-system.md`(边界五条铁律 · F-8.6 8 不在定点域内 + 护栏 G-1…G-4 · 规则一 只读不写不持状态)
**Requirement**: TR-diag-001(8 位于唯一浮点出口之后 IVitalsQuery→VitalsDto)· TR-diag-002(铁律① 8↔11 无数据流)· TR-diag-003(铁律② 不回写 9)· TR-diag-004(铁律③ 不拥有持久化)· TR-diag-007(F-8.6 8 在定点域之外)· TR-diag-010(G-4 位宽固定 System.Single,禁 FMA 收缩依赖)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(`IVitalsQuery` 抽象点 + `VitalsDto` 全案唯一浮点出口)· ADR-006(边界契约;8 出浮点**设计上不违** ADR-005,但受护栏)· ADR-013 §9 C3(42/8 只渲染/只读,永不持游戏状态;`PresentationDtoGuard` 递归扫描)· ADR-025(装配落点:`Gameplay.UI`/`Gameplay.Presentation` 侧,零 sim 内部类型引用)
**ADR Decision Summary**: 8 的输入**只经** `IVitalsQuery`/`VitalsDto`(不引用任何 sim 内部类型;8 侧不存在 `ToFloat` 之外的浮点入口);8 **零** `Publish`/写入点;重启不丢东西 = 8 不持跨调用累积量;`Math.Sqrt` 出现在 sim 程序集即违门 B(AC-8-6 2026-09-23 订正),8 侧走**预计算定表**。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(边界断言 = EditMode 反射/IL;承 audio story-001 已趟平的 `AssemblyGates` + `PresentationDtoGuard` 双层机制 —— 复用,不新建扫描器)
**Engine Notes**: G-4(禁 FMA 收缩)须 IL2CPP 实测定论:IL 层断「或走定表、或显式 Mul 后 Add」;跨 CPU/IL2CPP 收缩差异面列 AC-8-F5 矩阵(归 story 004)。

**Control Manifest Rules (this layer)**:
- Required: 8 的全部输入 = `VitalsDto`(float 只进呈现/求值门面);8 的全部输出 = 呈现意图 + 成长意图(经门控出口)
- Forbidden: `IEventSink` 写入、`Publish` 调用点、存档 API、sim 内部类型引用、libm 超越函数(G-1)、`UnityEngine.Random`/`System.Random`(F-8.4 前提)
- Guardrail: 「合法的字段」口径 —— 8 可持**当帧**呈现缓存,禁**跨调用累积量**(AC-8-3 括注口径)

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [x] **AC-8-1**[I] BLOCKING:8 全流程期间 `IEventSink` 写入数 = 0;9 的 `SimEvent` 流哈希与「不经 8 的对照跑」逐位相同;静态守门 = 8 程序集零 `Publish` 调用点
  —— ✅ 2026-10-05:静态半(IL `IEventSink` 类型面 + `Publish` 调用点 + 源层)双层坐实;
  运行面可执行面(门面读×4 + 成长出口×2)Append=0。⚠️ **残余(括注,禁默示全判)**:
  「查体+落笔+2 改写」完整脚本归 story 005/006;哈希对照段现为**结构占位**(两 sink 恒空,
  零鉴别力,勿引作「哈希判据已生效」);跨 epic 空流基线归 CI(Implementation Note 4)。
- [x] **AC-8-2**[L] BLOCKING:8 只引用 `VitalsDto`/`SimEvent` 等公开类型,零 sim 内部类型;不存在 `ToFloat` 之外的浮点入口
  —— ✅ 2026-10-05:Cecil 装配黑名单 `{Sim, Sim.Codec}` + `IEventSink` 禁名 + `Fix` 族
  仅 `ToFloat` 豁免,IL/源双层负例端到端注入(`RecipeDataSet` / `get_Raw` 必红,
  `ToFloat` 不误伤)。
- [x] **AC-8-3**[L/I] BLOCKING:同一病人 · 同一 Skill · 同一 `(Skill, sign_id)` 在**两个独立进程**求 `display_词`/把握度/四态 ⇒ 输出逐字相同;字段反射遍历 ⇒ 无跨调用累积量(读数/已查记录/病名/置信度/改写次数)
  —— ✅ 2026-10-05:字段反射扫描零命中 + 5 类累积量 token/tick 类型负例必红 + 当帧缓存
  豁免不误伤;双实例逐位一致(Implementation Note 3:纯函数入口 + 独立实例 =
  两进程等价物)。⚠️ **残余**:四输出(display_词/把握度/四态)不存在于本 story 可执行面
  —— 归 story 002/003 落公式后补跑。
- [x] **AC-8-4**[I] BLOCKING:8↔11 零数据流 —— 11 的输入契约不含病名字段;8→11 无任何数据边(支柱一守门;成对断言落 prescription epic story 003 的双判据,本 story 断 8 侧)
  —— ✅ 2026-10-05:8 侧 `[D-11]` 源层谓词 + 负例必红 + 生产零命中。11 侧反射半边
  **NOT-RUN**(prescription epic 未开工)—— 按本 AC 括注归该 epic story 003 成对断言。
- [x] **铁律③/④**[A]:8 零持久化 API 调用点(反射断言);`EmitGrowth` 唯一门控出口 = 主机 + `IIdAuthority`(TR-diag-005;调用语义归 story 005,本 story 断**存在唯一出口形状**)
  —— ✅ 2026-10-05:③ IL+源双层 `PlayerPrefs`/`JsonUtility`/`System.IO` 必红(判据
  等价性:反射无法枚举调用点,IL+源为**更强判据**,见 Completion Notes);④ 形状四层锁
  (契约成员集 = `{EmitGrowth}` / 出口唯一公开方法 7 参签名逐参 / IL 调用点恰 = 1 /
  出口外直调必红)。调用语义(IIdAuthority 门控)归 story 005,按 AC 括注不判。
- [x] **AC-8-6(G-1 半边)**[A]:grep+IL 双层零 `Math.Pow/Exp/Log/Cbrt`;`READ_FLOOR`/`C_neg` 的插值与线性项「或走预计算定表,或显式 Mul 后 Add」(FMA 收缩不依赖;G-4)
  —— ✅ 2026-10-05:G-1 半边双层全绿(`Math`/`MathF`/`Mathf` 三族 + 17 成员,
  `Max`/注释不误伤);位宽扫描双层零 `double`(G-4 面)。⚠️ **残余**:定表命中断言
  (同输入两次查表)与 FMA 收缩 IL2CPP 实测(AC-8-F5 矩阵)归 story 003/004。
- [x] **TR-diag-019/020 前置**[A]:`PresentationDtoGuard` 挂入 8 的全部呈现 DTO 递归扫描(disease_id 不进呈现层;判据 = 反射,承 AC-37-15)
  —— ✅ 2026-10-05:钩子挂入 + 负例(`disease_id` 字段 / `Diagnosis*` 类型名 /
  `AssertNoDiseaseId` throw)红路径坐实。⚠️ 生产侧 8 现 **0 个 `*Dto`**(呈现 DTO 随
  story 006 落)⇒ 钩子当前空转 —— 已声明,非借绿。

---

## Implementation Notes

*Derived from F-8.6 + ADR-025:*

1. 8 实现住 `Gameplay.UI`/`Gameplay.Presentation`(按 ADR-025 清单;与 42 的装配关系评审时重读清单)。
2. 定表化:`(1−s)^READ_GAMMA` 与 `s^NEG_GAMMA` 两族曲线烘焙期按 `Skill∈[0,60]` 整数档预计算为定表(F-8.1/8.3 的消费面,表生成逻辑归 story 003/004,本 story 锁「8 运行期查表不现算浮点幂」的形状)。
3. 双进程一致性夹具:测试内起两 AppDomain/进程等价物(EditMode 用纯函数入口 + 独立实例即可,不引多进程框架)。
4. AC-8-1 的对照跑夹具 = disease epic story 002 的空流基线;跨 epic 引用在 CI 层做(证据文件注明)。

## Out of Scope

- [Story 002]: 词条表 schema 与数据行
- [Story 003/004]: F-8.1/8.2/8.3 公式实现(本 story 只锁其浮点出口形状)
- [Story 005]: 状态机与成长门控调用
- [Story 006]: 呈现与走查类 AC
- 音频四条(TR-diag-021…025 归 audio-system epic)

## QA Test Cases

*Written at story creation(lean mode).*

- **零写入**: 全流程脚本化(查体+落笔+2 改写)⇒ Append/Publish 计数 0;流哈希 ≡ 对照(AC-8-1)。
- **引用面**: Cecil 扫 8 程序集 TypeRef ⇒ ∈ 白名单;影子负夹具引 `PatientState` 内部类型 ⇒ 红。
  *(2026-10-05 收口括注:`PatientState` 全库无 C# 类型 —— 以 Sim 真实内部类型 `RecipeDataSet` 承同一判据(装配黑名单),夹具头注同步登记。)*
- **双进程逐字**: 两独立求值实例四输出逐字等(AC-8-3)。
- **11 契约**: 反射 11 输入类型集 ⇒ 无病名字段(AC-8-4)。
- **超越函数**: IL 扫 `call Math::Pow` 等 ⇒ 0;定表命中断言(同输入两次查表)。

## Test Evidence

**Story Type**: Integration
**Required evidence**: `unity/Assets/Tests/EditMode/DiagnosisSystem/boundary_guard_test.cs` — must exist and pass
**Status**: [x] Created — ✅ 2026-10-05 · 25/25 passed(`unity/Logs/s001-diag-fix.xml`);
全量回归 2561/2515 passed/0 failed/45 skipped/1 inconclusive(`unity/Logs/full-diag-001.xml`);
评审原件 `production/qa/evidence/review-diagnosis-story-001-2026-10-05.md`

---

## Dependencies

- Depends on: disease-simulation epic story 001/002(`Fix`/`VitalsDto`/`SimEvent` 形状)、audio-system epic story-001(`AssemblyGates`/`PresentationDtoGuard` 机制复用)、skeuomorphic-ui epic(装配清单)
- Unlocks: Story 002…006(8 的全部实现面)、prescription-medication epic story 003(AC-11-01 双判据的另一半)

## Completion Notes

**✅ 收口 2026-10-05 —— 严格协议:创建并 unity cli 测试 → 双代理评审 → 修复 → 复跑绿 → 收口提交推送(评审只做一轮)**

### 交付物
- **生产**:`DiagnosisVitalsFacade`(静态零字段唯一取数门面,`Read(IVitalsQuery, PatientId)`,
  null ⇒ `ArgumentNullException`)· `DiagnosisGrowthExit`(静态零字段,7 参纯转发至
  `SkillGrownEmitter.EmitGrowth`,返回载荷不 Append)· `DiagnosisBoundaryGates`
  (Cecil IL 层 + 源文本层 + 逃逸谓词;tag: D-TREF/D-PUB/D-PERSIST/D-G1/D-CLK/D-FIX/
  D-EXIT/D-FACADE/D-11/D-ESC/D-0)· `AssemblyGates` RunMenu + BuildGate 接线两处
  (reload hook 刻意不加 —— 承 Required-7c「Cecil+源扫移出 reload 路径」)
- **测试**:`boundary_guard_test.cs`(**25 条**,含 EOF 真 IL 负例夹具住测试装配)

### 单轮评审 → 修复轮(结构侧 3 MAJOR + QA 侧 2 MAJOR 收口前置 + 8 MINOR 全落实)
- **S-1** `Mathf` 双层补入(G-4 邀请式绕行面)· **S-2** `IModifierType` 修饰符侧遍历(b5 Required-2)
- **S-3** `DateTimeOffset`/`Stopwatch`/`Environment.TickCount` 时钟名单补全 · **S-4** 深度超限改落红
- **S-5** 缺失不叠报 + 源层 `[D-TREF]`→`[D-0]` tag 统一 · **S-6** 哈希段标结构占位
- **S-7** 消费者必须住前缀纪律登记 · **S-8** `IVitalsQuery` 成员集锁 · **S-9** BuildGate WARN 口径
- **Q-1** 全量复跑 0 failed · **Q-2** AC 逐条括注 NOT-RUN 残余 · **Q-3** `RecipeDataSet` 替代括注
- **Q-4 判据等价性**:铁律③ AC 字面「反射断言」—— 实现为 **IL(Cecil)+ 源文本双层**;
  反射无法枚举调用点,IL+源为**更强判据**(覆盖方法体指令而非仅类型成员),
  判「零持久化 API 调用点」在字面结果上一致且更强
- **Q-5** 三处 `Is.Not.Empty` 扫描面自证(防单 `--test=` 过滤跑假绿)· **Q-6** 漏测分支负例补齐
  (`Fix.One` 字段分支 · 源层 `IEventSink`/`FixParse` 正例 · `Mathf`/`DateTimeOffset`)

### 验证(实测)
- 过滤:`unity/Logs/s001-diag-fix.xml` = **25/25 passed / 0 failed**
- 全量:`unity/Logs/full-diag-001.xml` = **2561 total / 2515 passed / 0 failed / 45 skipped /
  1 inconclusive**(增量 115 = patient-ai s003+s004+本批;skip +2 = s004 既有 [Ignore];
  根 `Skipped:Ignored` 与基线 019d 同态)

### 残余 NOT-RUN(禁借绿 —— 见评审原件 §四全表)
- AC-8-1 全流程脚本 + 哈希鉴别力 → 005/006;跨 epic 基线 → CI
- AC-8-3 四输出 → 002/003 · AC-8-6 定表 + G-4 IL2CPP 实测 → 003/004
- AC-8-4 11 侧反射 + D-11 词面键回补 → prescription story 003 · `typeof(Fix)` 构造性绕行 → 11/005 轮复查
- TR-registry 状态回填(TR-diag-004/002/010 等)→ 独立 docs 轮(承既有惯例)
