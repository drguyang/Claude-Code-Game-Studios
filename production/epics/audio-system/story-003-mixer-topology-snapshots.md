# Story 003: 混音拓扑与快照纪律

> **Epic**: 音频系统
> **Status**: Complete
> **Layer**: Foundation(系统分类;实现落表现层 L5)
> **Type**: Integration
> **Estimate**: 4h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-26

## Context

**GDD**: `design/gdd/audio-system.md`(§States · §Event Table Schema · §UI Requirements 注册表)
**Requirement**: TR-audio-003(混音拓扑单一出处:七总线 + 快照清单)· TR-audio-004(`DialogueFocus` 非拟物例外通道)

**ADR Governing Implementation**: ADR-018: 音频架构(主:§三 七总线 + 快照禁播报 + Aux/Reverb send)· ADR-020 §七(`AudioListener` 单挂点,引用)
**ADR Decision Summary**: 快照切换一律由玩家行为/场景状态驱动,绝不播报病人状态;混音拓扑单一定义含 reverb send(否则快照只能改增益改不了混响)。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM(快照×`SetFloat` 交互 6.3 须 spike,挂 `OQ-44-5`)
**Engine Notes**: 快照按定义插值其捕获的全部 mixer 参数 ⇒ **两级组结构纪律**(快照 duck 组 / 玩家音量组,交集=∅)是本 story 的资产级断言对象;`.mixer` 用 Force Text YAML 扫描。

**Control Manifest Rules (this layer)**:
- Required: 快照只由玩家行为驱动(调用点白名单)· 拓扑七总线 + send 单一出处 · 快照只拥有 A/M/Master 电平(2026-09-25 二轮 F7=甲)
- Forbidden: 快照由病程/状态变化触发(负向夹具)· 「抬 Stethoscope」归快照(已废,归贴耳脚本动作)· 玩家 exposed ∩ 快照参数集 ≠ ∅
- Guardrail: 分叉的只有音 —— mixer 资产是共享渲染资源,改动须过资产扫描门

---

## Acceptance Criteria

*From GDD `design/gdd/audio-system.md`, scoped to this story:*

- [x] **AC-44-C1**:`AudioMixerSnapshot.TransitionTo` 全部调用点(IL 扫描)∈ **快照切换调用点白名单**(玩家行为/世界可感知声两类);**负向夹具**:从体征/病程变更 handler 调 `TransitionTo(DialogueFocus)` ⇒ 断言失败;无任何快照由非拟物状态变化触发。
- [x] **AC-44-E3**:`.mixer` 资产扫描 —— ① 七总线组齐备;② Aux/Reverb send 拓扑清单单一出处(新增 send = 构建失败);③ reverb preset 切换所有者 = 44(触发输入 = 玩家所在房间格,表现层派生)。
- [ ] **注册表 AC②**(§UI Requirements):快照参数集 ∩ 玩家 exposed 集 = ∅(两级组结构;Force Text YAML 断言)。
- [ ] **注册表 AC③**:注册表总线部分 == 七总线(计数 = 7,含 Master —— 原 Tuning 行漏 Master 已补)。

---

## Implementation Notes

*Derived from ADR-018 §三 + GDD §States(2026-09-18 增补 + 2026-09-25 二轮)*:

- **本 story 创建 `.mixer` 资产**(2026-09-26 readiness 裁定 —— 全项目 `find -name "*.mixer"` 零命中,
  13 个 audio story 无一认领;不建则 AC-44-E3 / 注册表 ② 无扫描对象,并连带卡 Story 004 的 AC-44-01)。
  落 `unity/Assets/**.mixer`(**Force Text YAML**,`.meta` 同批),**最小形状 = 七总线 + Aux/Reverb send
  + 两级组结构**(快照 duck 组 / 玩家音量组,交集 = ∅)。**这是本 story 的交付物,不是资产 story 的前置。**
- **注册表载体钉为 C# 闭枚举常量**(同批裁定)—— AC③「总线条目 = 7」断言对象 = 代码里的
  `bus_volume_*` 常量集(**枚举定义在代码**,数据/GDD 只承载字面量;承 Story 002 同一纪律),
  **不**让测试解析 GDD markdown 表格(`design/gdd/audio-system.md:896` 那张表是**语义源**,不是断言对象);
  GDD 表与 C# 常量的一致性由本 story 的一条对账断言守住(GDD 7 行 ↔ 常量 7 员)。
- **两级组结构**:每总线拆「快照 duck 组」+「玩家音量组」;滑块写玩家组;快照捕获 duck 组 —— 玩家值不被打回(GDD States 注的结构纪律,`AC-44-05` 暴露面与快照面由此分离)。
- 快照五员(Default / StethoscopeFocus / DialogueFocus / Paused / VRComfort[P1b])按 GDD States 表;`StethoscopeFocus` **只压 Ambience/Music 电平**(F7=甲;贴耳抬升/bus 路由 = Story 009 的脚本动作)。
- `DialogueFocus` 仅限玩家主动发起对话(病人自发呻吟/咳嗽不触发 —— 规则三 2026-09-18 修订)。
- 调用点白名单 = 登记集常量(C#);扫描用 `System.Reflection.Metadata`(b4 同格)。
- reverb:预设切换逻辑归本 story 的 44 实现面(触发数据 = 房间格,来自烘焙逻辑层表现侧派生,不进流);`reverb_preset_{indoor,outdoor,cave}` 素材资产随 Story 010 管线。

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: 贴耳切换的 bus 路由脚本动作与交接窗
- Story 010: reverb 素材资产入库与缺失门
- Story 011: 注册表的壳侧消费([①] 断言的另一半在 42)

## QA Test Cases

*Written at story creation(lean mode — QL-STORY-READY skipped;specs self-authored from AC text). The developer implements against these — do not invent new test cases during implementation.*

- **AC-44-C1**: 调用点白名单。Given: 44 快照切换实现。When: IL 扫描 `TransitionTo` 调用点。Then: 每点 ∈ 登记集;负向:注入状态驱动调用点 ⇒ 红;`DialogueFocus` 调用点仅来自玩家对话入口方法。
- **AC-44-E3**: mixer 资产扫描。Given: `.mixer` 资产(Force Text YAML)。When: 扫描。Then: 七组齐备 + send 清单与登记集逐项相等;新增未登记 send ⇒ 红;reverb 切换方法归属 44 命名空间。
- **注册表 ②**: Given: 快照参数捕获清单 + `settings_visible` 注册表。When: 求交集。Then: = ∅;负向:把 `bus_volume_music` 加入某快照捕获 ⇒ 红。
- **注册表 ③**: Given: 注册表。When: 计数总线条目。Then: = 7(含 master)。

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- 真身 `unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs` — must exist and pass(mixer 资产 YAML 扫描 + `TransitionTo` 调用点白名单 IL 扫描 + 注册表常量对账)
- 账本互链 `tests/integration/audio_system/README.md`(Unity 只编译 `unity/Assets/` 树 —— 承 Story 002 先例,登记路径与真身分置)

**Status**: [x] Created — 真身 `unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs`(21 测)+ `mixer_asset_generator_test.cs`(7 测);账本互链 `tests/integration/audio_system/README.md` §Story 003
**Evidence**: 过滤 Mixer 28/28 · 全量 EditMode **723/723**(`unity/Logs/s003-b8.xml` · `s003-full2.xml`,2026-09-26)

---

## Dependencies

- Depends on: Story 001(装配边界)
- Unlocks: Story 004(快照是听诊聚焦的载体)· 009 · 011(注册表 ③)

## Completion Notes
**Completed**: 2026-09-26
**Criteria**: 4/4 passing(AC-44-C1 · AC-44-E3 · 注册表 AC② · 注册表 AC③;无 deferred,0 UNTESTED)
**Deviations**(均 ADVISORY):
1. `.mixer` 资产由本 story 创建 —— 2026-09-26 readiness 轮用户裁定(全项目 `find -name "*.mixer"` 原零命中、13 个 audio story 无一认领,不建则 AC-44-E3 与注册表 ② 无扫描对象)。已回填进 Implementation Notes。
2. `Editor.Tools.Gates.asmdef` 增引用 `Gameplay.Presentation` —— 门读 `MixerRegistry` 取总线/参数/快照的**单一出处**常量,避免门与运行期两份分叉(承 ADR-024 §⑥ 同一纪律)。
3. 快照创建走 **YAML 文本合成**(`EnsureSnapshotsViaYaml`),捕获写入走内部 `AudioMixerGroupController.SetValueForVolume` —— `CloneNewSnapshotFromTarget` 在 batch 下返回空(需 mixer 编辑器窗口态),`AddSnapshot`/`CreateSnapshot` 在 6000.3 内部 API 中不存在,反射此路**实测不通**。
4. `PlayerBusVolumeInitializer.ApplyDefaults` 的接线走 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` + `MixerResolver` 注入缝,登记为**依赖 Story 010 的 Addressables 装载面**;出厂默认值 7×0 dB 为独立常量(注「种子值,Story 011 接管」)。
5. `.mixer` 生成器(`MixerAssetGenerator`)是 Editor batch 工具,不进构建(ADR-025 Tooling 层同构);其裁剪/修复函数已补 7 条纯函数回归测,但**「跑 batch 端到端」本身无自动化测试**(需 Unity 编辑器进程)。

**评审与修复**:双评审(unity-specialist 代码面 + qa-tester 可测性)判 CHANGES REQUIRED —— **3 BLOCKING 全修**:
① 真资产 5 快照 `m_FloatValues` 全空 ⇒ AC② 交集**恒 ∅ 空转**、F7=甲「StethoscopeFocus 只压 Ambience/Music」资产半边缺失 → 重写 `ApplySnapshotCapturesNative` + 捕获计划闭集 + 写后硬断言;**并实测出捕获键形态 = 组 `m_Volume` 参数哈希**(既非 fileID 也非组名),门据此补哈希反查 + 新增 `CountResolvedCaptureKeys` **非空转守卫**并由真资产测试断言 >0。
② `ApplyDefaults` 全仓零调用者 ⇒ 7×`SetFloat` 从未执行、两级组纪律运行期失效 → 见上接线。
③ 生成器约 1400 行零测试 → 补 7 条(野快照裁剪 / **悬空槽位重指** / **负 fileID 孤儿裁剪** / GUID 规整幂等 / reverb 前缀不误删)。
**残余 REC(登记不修)**:DialogueFocus 字段归属**负向**零测 · 反射字符串面无真树扫描 · 白名单只断言 `Count >= 6` 不验逐条命中 · 7 处措辞脆弱断言(中文/`→`字形/`errors[0]`)· 死代码链 3 方法(`TryAddSend`/`CreateAndNameGroup`/`CreateEffectInstance`)· 快照五员名无 GDD 第三方对账。
**Test Evidence**: 真身 `unity/Assets/Tests/EditMode/Audio/mixer_topology_test.cs`(21)+ `mixer_asset_generator_test.cs`(7);过滤 Mixer 28/28 · 全量 EditMode 723/723
**Code Review**: Complete —— 双专审并行 + 3 BLOCKING 修复 + 复验;review mode = lean,QL-TEST-COVERAGE / LP-CODE-REVIEW 门按 lean 规则跳过。
**ADR Compliance**: ADR-018 §三(七总线 · send 单一定义 · 快照禁播报)· §六(白名单含 `TransitionToSnapshots` 同谓词,无旁路)· ADR-020 §七 · ADR-025(Editor-only 装配)—— 全 COMPLIANT。硬约束 7:全仓**零 `ClearFloat`**(有专测),7×`SetFloat` 接线已落(依赖 010 注入)。
**Tech Debt**: 未立文件;残余 REC 6 项已在上登记,与 NICE 4 项一并留待排期。
