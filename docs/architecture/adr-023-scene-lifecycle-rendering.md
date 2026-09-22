# ADR-023: 渲染与场景加载策略(URP + RenderGraph + Scene 生命周期)

## Status

Accepted

> **2026-09-21 状态串归一(RC-2,用户照准)**:原串为 `Accepted(附条件,见下)`,是全仓 22 份
> ADR 中唯一非字面 `Accepted` 者 —— `create-control-manifest` 的「Status: Accepted」字面过滤会
> **静默丢弃本件**。含义从未变:S1–S4 的「附条件」**只留正文**(见下),
> **本归一是文本对齐,非新裁决**(承 2026-09-20 用户裁定轮 `architecture-review-2026-09-20.md` RC-2 处置建议)。
>
> **2026-09-20 起草;同日用户裁定转 Accepted**(逐份裁定轮 #1):
> **① 三场景拓扑 = 照准 · ⑥ chunk 激活权 = 系统 6 照准 · 其余 ②–⑤⑦⑧ 随 ① 一并照准**。
> **附条件**(用户裁定原文口径「转 Accepted · 附条件」):
> **S1–S4 的回填是实现故事的前置,不是本裁决的效力条件** —— 裁决内容
> (拆序六步 / 零 gameplay 断言 / 激活权归属)不依赖引擎实测结果即成立;
> 若 S3 前提被实测推翻,拆序第 6 步断言零成本保留,不触发改判;若 S1/S4 实测不可接受,
> 按 §Alternatives「What Would Change Our Mind」另开修订,不静默偏离。
> ~~Status 停在 Proposed 的理由~~(起草时注,已由上述裁定取代):S1–S7 spike 全部未跑的事实
> **不撤销** —— 逐条状态见 §Validation Criteria,未跑的仍挂着未勾。
> TD 条件 C2 的 **#1 半边就此结案**。
>
> **2026-09-23 S1/S3/S4 spike 实测回填**(Unity 6000.3.24f1;装置
> `unity/Assets/Tests/PlayMode/u1_scene_spikes_test.cs`,卡 `production/u1-spike-checklist.md` §6):
> **S1 ✅ · S3 ✅ · S4 ✅ 三条全通过** —— 逐条数字见 §Validation Criteria。
> **裁决内容一字未改**(附条件口径从未把 spike 当效力条件);回填只把「未跑」的字面状态改为实测结论。
> S3 附带一条**源实读 + 实跑确认**的机制发现(场景内亲代实例由 Addressables 自动清理,
> 不得手动 `ReleaseInstance`)—— 已回填 §Validation S3 条目。
> **S2 / S5 / S6 / S7 仍挂未勾**(S2/S6/S7 延后至各自实现故事;S5 休眠,触发 = ADR-013 假设 6 失败)。

## Date

2026-09-20

## Last Verified

2026-09-23(**S1/S3/S4 实测回填** —— Unity 6000.3.24f1,装置
`unity/Assets/Tests/PlayMode/u1_scene_spikes_test.cs`;见 §Validation Criteria 三条勾选。
**引擎参考件口径未变**;新增一条**包源码实读**结论:Addressables 2.10.3 @ `6fef233` 的
`UnloadSceneAsync` 默认 `autoReleaseHandle:true`、`InstantiateAsync` 默认 `trackHandle:true`,
且 `CleanupSceneInstances` 会自动释放「随被卸载场景销毁」的实例句柄 —— 见 §Validation S3)。
2026-09-20(引擎参考件:`breaking-changes.md:54-65` · `modules/rendering.md:19-33` ·
`plugins/addressables.md:263-276 / :301` · `deprecated-apis.md:100`;
unity-specialist lean 复核已回,其 4 条口径修正已并入本件 —— 见 §Context「复核已改的口径」)

## Decision Makers

dr_guyang(用户 · **2026-09-20 照准 ①⑥ 及全件,转 Accepted 附条件**)· technical-director(起草)·
unity-specialist(lean 引擎复核 · 报告结论「无一条与已 Accepted ADR 冲突,但 3 处口径要改 + 7 项参考件是哑的」)·
7a 持久化(读档 = 场景重建的驱动方)· 6 世界与生态区(chunk 流式候选归属)·
20 玩家控制器 / ADR-020(`ICameraRig`)· 42 UI / ADR-013(Renderer Feature 触发条款的另一端)· 44(单 `AudioListener`)

## Summary

裁决 **P0 场景拓扑与生命周期**:三场景制(`Boot.unity` 常驻 / `MainMenu.unity` / `World.unity` additive),
`World.unity` **零 gameplay GameObject**(一切游戏对象经 Addressables 实例化 —— 逻辑层是烘焙数据,
ADR-015,不需要场景里的占位对象);**拆序六步**(停 tick → 落盘 → `ReleaseInstance` 全部 →
`Release` 全部 handle → 卸载场景 → 断言引用归零),根因 = `Addressables.UnloadSceneAsync`
**不销毁 `InstantiateAsync` 的产物**,不断言则**每次读档漏一个世界**;
相机与 `AudioListener` **住 Boot**(每设备恰一条混音总线的物理事实,承 ADR-018 §五);
`Step` 驱动权与场景解耦(不由渲染帧、也不由场景加载事件驱动,承 ADR-005 ③ 相位义务);
P0 默认零 custom Renderer Feature,但留**触发条款**(ADR-013 §6.6 假设 6 spike 失败时的合规出口)。

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Knowledge Risk** | **HIGH** —— URP 6.3 RenderGraph 迁移与 Addressables 6.2+ 抛异常行为均 post-cutoff(`VERSION.md` 时间线;LLM 训练覆盖 ≈2022)。本件**刻意只引用 `docs/engine-reference/unity/` 已登记形状**,其余全部落 S1–S7 spike |
| **References Consulted** | `breaking-changes.md:54-65`(ScriptableRenderPass 签名迁移)· `modules/rendering.md:19-33`(RenderGraph 新姿势)· `plugins/addressables.md:263-276`(Additive 加载 + `UnloadSceneAsync`)· `plugins/addressables.md:301`(场景卸载时 Release handles)· `plugins/addressables.md:107-108 / :126-127`(`Release` / `ReleaseInstance` 义务)· `deprecated-apis.md:100`(`Application.LoadLevel` → 不用) |
| **Post-Cutoff APIs Used** | `Addressables.LoadSceneAsync(key, LoadSceneMode.Additive)` / `UnloadSceneAsync(handle)`(参考件已载,S1 实测前置)· `InstantiateAsync`(S3)· RenderGraph pass 形状(仅作为**触发条款**的目标形状,P0 默认不用) |
| **Verification Required** | **S1–S7(全未跑,逐条见 §Validation Criteria)** —— 本 ADR 转 Accepted 的硬前置 |
| **Deprecated API Check** | 通过 —— 不用 `Application.LoadLevel*`;不用旧 `Execute(ScriptableRenderContext, ref RenderingData)` 签名(`breaking-changes.md:54-65` 明示已删改,非重载新增) |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | **ADR-005**(Accepted —— `Step` 不由渲染帧驱动;`ITickProvider` 是六个 P0 抽象点之一)· **ADR-010**(Accepted —— 存档时机:退出保存 + checkpoint ⇒ 拆序第 2 步的语义)· **ADR-014**(Accepted —— `data-core` 首次 `Step` 前预载;E-13 启动期硬失败)· **ADR-015**(Accepted —— 逻辑层烘焙数据 ⇒ World 场景无占位对象的依据)· **ADR-018**(Accepted —— 单 `AudioListener` 每设备一条总线)· **ADR-020**(Accepted —— `ICameraRig` 只读不持状态)· **ADR-013**(Accepted —— §6.6 假设 6 的 spike 是触发条款的开关) |
| **Blocks** | 任何 PlayMode 集成测试(急救流程 / 建造 / 联机基础 —— `technical-preferences.md` Testing);ADR #2 的 asmdef 装配(场景根对象的程序集落点);7a 的「读档 = 重建」实现 |
| **Supersedes** | 无 |
| **Related** | ADR-022 Tooling 层(切片者:导航格按 chunk 切片,运行期激活权在本件 ⑥ 裁定);QQ-08(菜单未缩放时钟,若开) |

## Context

### Problem Statement

`architecture.md` §3.4 初始化序列的 **[0] Unity/URP 启动** 与 **[7] 呈现层就绪** 两步
自 v1.0 起标 🔴「无 ADR 裁决」;§System Layer Map 的 🔴①② 两条摘录(RenderGraph /
`LoadSceneAsync`)指向同一个缺口:**没有任何文件裁过「场景何时加载、何时卸载、谁驱动 tick、
相机住哪」**。开放世界 + 存档重建 + 联机意味着读档 = 一次完整的场景级世界重建 ——
拆序错一处(尤其 S3 的实例泄漏)是**静默**的、每次读档复利的失败。

### Current State

- 19→22 份 ADR 全部对场景生命周期**零字**(`grep` 实测,见 `architecture.md` #1)。
- 引擎参考件只给了 API 形状,没给策略:`addressables.md:263-276` 甚至用 `"MainMenu"` 做示例键 ——
  恰好是本项目「菜单与游戏场景是否分离」这个待裁问题本身。
- unity-specialist lean 复核已回:**无 blocker、不与任何 Accepted ADR 冲突**,
  但提案原稿有 3 处口径错(下),7 项断言参考件无载(哑)。

### 复核已改的口径(原稿三错,如实登记)

1. ~~「Boot 无相机栈」~~ **自相矛盾**:相机与 `AudioListener` **必须住 Boot** ——
   每设备恰一条混音总线(ADR-018 §五)是物理事实;若相机随 World 重建,听诊总线快照与
   `AudioListener` 挂点跟着抖动。现裁决 = Boot 常驻持有(③)。
2. ~~「加载期 halt `CatchUp`」~~ **术语错**:`CatchUp` 是离线补算(§3.4 [6]),运行期循环是 `Step`。
   现口径 = 加载期 halt 的是 **tick driver 的 `Step` 驱动**(④)。
3. ~~「拆场景 = `UnloadSceneAsync` 完事」~~ **漏了实例泄漏**:`UnloadSceneAsync` **不销毁**
   `InstantiateAsync` 产生的 GameObject(参考件 `:126-127` 只说 Release when destroying)。
   不强制拆序 = 每次读档漏一个世界(⑤,判据 = S3)。

### Constraints

- ADR-005:`Step` 不由渲染帧驱动(20 Hz vs 60 fps 的相位是实现期义务,OQ-25-8 ③)。
- ADR-014 §五:`data-core` 组必须在**首次 `Step` 前**装载完成;Addressables 6.2+ 装载失败**抛异常**
  (E-13)⇒ 启动期硬失败,不 null 解引用。
- ADR-015:运行期只加载确定性整数逻辑层;视觉层(Terrain, float)不进 sim 采样面。
- ADR-013:VR 必须 world canvas;P0 平面为主,但栈形状须 P0 定(本件 ③)。
- ADR-020:玩家位移纯表现态 ⇒ 读档后玩家位置来自「最后一条 `ActorCellEntered` 的格 +
  格内偏移(表现态,不存档)」—— 该偏移在重建瞬间不存在,须定生成规则(⑦)。

### Requirements

- `architecture.md` §Required ADRs #1 四项(①生命周期 ②tick 次序 ③相机栈形状 ④L4 引擎 API 面)。
- §3.4 初始化序列 [0] / [7] 两步的 🔴 解除。
- TD 条件 **C2**(与 ADR-024 同批;Accepted 前不开工)。

## Decision

### ① 场景拓扑 = 三场景制

| 场景 | 加载方式 | 内容 | 常驻? |
|---|---|---|---|
| `Boot.unity` | 构建首场景(非 Addressables) | tick driver、`AudioListener`、主相机、`ICameraRig` 宿主、Addressables 引导、崩溃兜底存档钩子 | **是(全程不卸载)** |
| `MainMenu.unity` | `Addressables.LoadSceneAsync(key, LoadSceneMode.Additive)` | 标题 / 存档位(7b)/ 设置;UI Toolkit 栈(ADR-013) | 否 |
| `World.unity` | 同上 additive | **零 gameplay GameObject** —— 只允许:环境光/雾设置、纯视觉层 Terrain 容器(ADR-015 §一,视觉采样禁入 sim)、无逻辑根节点 | 否 |

- **菜单 ↔ 游戏 = additive 换入换出,永不用 `LoadSceneMode.Single`**(Single 会瞬杀 Boot 之外的
  一切并重置 `DontDestroyOnLoad` 之外的所有对象 —— 与本件 ⑤ 拆序断言不可共存)。
- 联机(1-4 coop)各端加载**同一** `World.unity` 键;世界差异全部来自三流,不来自场景变体
  (ADR-009;建造物是 `StructurePlaced` 事件 → 运行期实例化,不是场景里的预摆对象)。

### ② World 场景零 gameplay 对象铁律 + 断言

`World.unity` 内任何带 `Sim`/`Gameplay` 程序集组件的 GameObject = 构建期硬失败
(编辑期校验工具,住 Tooling 层,与 ADR-022 C 系同构:`throw`,禁 `Debug.Assert`)。
**依据**:逻辑层是烘焙数据(ADR-015),gameplay 对象的存在性本身是模拟态 ⇒ 必须
事件驱动生成(`DropSpawned` / `StructurePlaced` / 病人由 9/13 生成),场景预摆 = 第二真源。
**判据 = S2**(场景 YAML 扫描的可行性)。

> **扫描判据收紧(2026-09-21 · 承 `architecture-review-2026-09-20.md` RC-6)**:② 的构建期扫描
> **增列禁令** —— **相机与 `AudioListener` 出现在任何非 Boot 场景(World / MainMenu)= 构建失败**。
> 理由:③ 立了「主相机 + `AudioListener` 住 Boot,全程不销毁」不变量,但原扫描判据**没有把它们列为
> 禁止项** ⇒ 美术在 `World.unity` 随手挂一台相机或第二个 `AudioListener`,扫描放行,③ 与
> ADR-018 §五「每设备一条混音总线」同时破;且 :85-87 显示同类破口曾发生(Boot 无相机栈自相矛盾)。
> 本收紧**属已裁判据的扩列,非新裁决**;并进 S2(spike 除「零 gameplay 对象」可行性外,同时验本条可查)。

### ③ 相机与 AudioListener 归 Boot(复核修正 1)

- 主相机 + `AudioListener` 住 Boot,全程不销毁;`ICameraRig.SetMode` 切平面/VR 档位
  (ADR-020 ③ 双路径;VR 实现 P1a,但**栈形状 P0 定死**:单相机 + URP 相机栈空栈起步)。
- 读档/换场景时相机**只允许 disable-replaceable,禁 mutate**(位置/朝向由重建完成后
  的表现态生成规则写一次,见 ⑦;中途不动它)。
- 44 的总线快照(ADR-018)随 Boot 常驻 ⇒ 换场不重置混音。

### ④ tick 驱动与场景解耦(复核修正 2)

- `ITickProvider`(ADR-005)住 Boot;**加载期 halt 的是 `Step` 驱动**(不是 `CatchUp`)。
- 启动/读档序列(与 §3.4 对齐,本件只补 [0]/[7] 两步):
  `Boot 起 → data-core 预载完成(ADR-014 §五,失败 = E-13 硬失败)→ 存档头 + 三流重放 + CatchUp
  → World additive 加载完成 → 表现态重建(⑦)→ tick driver 起相 → 首次 Step`。
- **交互意图冻结门**:tick driver 未起相前,3 的 action 一律**不产出意图事件**(P-03 通道掐断)——
  加载期点击不落流、不排队(防「幽灵输入」;判据 = S6:未缩放时钟与菜单 halt 同 spike 族)。
- 菜单态 = sim 不存在(tick driver 不构造),「暂停 ≠ 停止主机 tick」的联机口径不变(承 9 GDD)。

### ⑤ 拆序六步(复核修正 3;S3 是其存在的全部理由)

```
1. halt tick driver(停 Step 驱动;冻结交互意图)
2. 落盘 flush(退出保存 / checkpoint —— ADR-010 §六;离线补算账由下次启动重放吸收)
3. ReleaseInstance 全部 InstantiateAsync 产物(登记簿:实例化即登记)
4. Release 全部 AsyncOperationHandle(prefab / SceneInstance / TextAsset)
5. UnloadSceneAsync(World 或 MainMenu)await 完成
6. 断言:登记簿空 + handle 计数 0;非零 = 硬失败(记日志 + 拒绝继续,不静默)
```
- **实例登记簿**是运行期数据结构(表现层侧,不进 sim、不进流);「实例化即登记」由
  统一 spawn 门面执行,**禁绕过门面的裸 `InstantiateAsync`**(grep + Roslyn-free 的构建期
  文件面检查即可,P0 不引 analyzer —— 与 ADR-024 Alt E 同口径)。
- 判据 = **S3**(实测 `UnloadSceneAsync` 确实不管实例,以及漏 Release 的可观测后果)。

### ⑥ 运行期 chunk 激活权 = 系统 6

`architecture.md` 留白项:导航格按 chunk 切片(ADR-022 §⑤)只是**工具侧**;运行期
「哪个 chunk 的视觉/碰撞体进不出场」须有人裁决 —— **裁定 = 6 世界与生态区**(容器的
状态所有权,ADR-021 同款判据的自然延伸:6 已拥有 POI 状态,地理激活与它同源)。
- 6 的激活决策**只读 sim 量**(玩家所在格,来自最后一条 `ActorCellEntered` 语义位),
  **不读表现态连续位置**(ADR-016 §三 禁读判据的对称)—— 故 chunk 激活**不进流**(派生态,
  重建期由格重推),`OQ-6-8`「未驻留 chunk 视为全 block」的判定输入即本条。
- 判据 = **S4**(Addressables 场景 chunk 化 vs 单场景内 activation 开关,二选一的实测)。
- ⚠️ 本条是 ① 之外唯一带跨域归属的裁决,单列给用户照准。

### ⑦ 表现态生成规则:未存档的浮点位置从哪来

读档后玩家/病人/掉落物的**连续位置**不在流里(流里只有格 + 整数状态,ADR-009/015/020)。
裁决:重建 = 「格锚点 + **确定性格内偏移**」——偏移由 `(patient_id | actor_id, cell)` 的
**已登记整数哈希**(BCL 稳定哈希,承 ADR-010 只用 BCL 的口径)派生,**不引入第二个随机源**;
表现层拿到生成位后自由微调(寻位、避墙)——微调不回写、不进流、不进断言。
判据 = **S7**(重建后无对象落在不可走格内的抽查夹具)。

### ⑧ P0 零 custom Renderer Feature + 触发条款

- **P0 默认**:URP 内置特性;`ScriptableRenderPass` / RenderGraph 定制面**零使用**。
- **触发条款**(原「断言零」被复核判为过强):若 ADR-013 §6.6 假设 6 的 spike 失败 ⇒
  UI Toolkit 自定义材质路径可能需要 Renderer Feature 兜底(纸纹/墨迹合成)——届时**按本条款走**:
  新写一份「RenderGraph pass 形状」ADR(用 `RecordRenderGraph(RenderGraph, ContextContainer)`
  新签名,`breaking-changes.md:54-65`;禁旧 `Execute` 签名)才可引入,P0 关键路径外。
- 判据 = **S5**(该 spike 本身归 ADR-013 前置;本件只登记触发关系,不代跑)。

### Key Interfaces(边界形状)

```csharp
public interface ISceneRouter {           // 住 L4;Boot 内唯一实现
    void LoadWorld(string saveSlotId);    // additive 入 World + 触发 §3.4 序列
    void ReturnToMenu();                  // 走 ⑤ 拆序六步
}
public interface IWorldSpawner {          // 表现态生成门面(③⑤⑦ 的执法点)
    GameObject SpawnTracked(string addressableKey, WorldPos cell, int stableId); // 登记即入账
}
// 断言面:② 的构建期扫描 + ⑤ 第 6 步的运行期硬失败 —— 二者都不是 Debug.Assert
```

## Alternatives

### Alt B —— 单场景全常驻(菜单/世界同场景开关)
- **Pros**:无拆序问题,S3 泄漏面消失。
- **Rejection Reason**:菜单资源与 `data-core` 之外的一切常驻内存;且「World 零 gameplay 对象」
  在单场景下不可断言(②失去判据形态)。
- **What Would Change Our Mind**:若 S1/S4 实测 additive 流在目标机上不可接受(加载墙钟)。

### Alt C —— 世界完全走 Prefab 实例化,不用 Addressable Scene
- **Rejection Reason**:Terrain 视觉层与光照/雾是场景级资产;不用场景 = 重造半套场景系统。

### Alt D —— tick driver 随 World 生死(不常驻 Boot)
- **Rejection Reason**:与 ③ 同理 —— 它携带的 `ITickProvider` 相位状态与存档重放序在换场时
  重建,等于把 ADR-005 的「唯一真源」纪律做在易变宿主上。

## Consequences

- **Positive**:§3.4 [0]/[7] 两步 🔴→裁;S3 型「每次读档漏一个世界」的静默复利泄漏有断言拦;
  场景不再可能成为第二真源(②铁律 + 构建期判据);C2 的另一半(#1)有了裁决文本。
- **Negative**:三场景 + 拆序六步 = 读档路径的实现复杂度前置(换来的是泄漏不可静默);
  ⑦ 的确定性偏移是新增形状(数值无关,但实现面多一个哈希);本件带 7 项 spike,
  **Accepted 须等 S1–S7 至少 S1/S3/S4 有结果**(HIGH 域不空口结案)。
  —— **✅ 2026-09-23:S1/S3/S4 三条实测已有结论全通过**(§Validation Criteria)⇒ 该前置满足;
  S2/S5/S6/S7 按各自实现故事推进(未跑,仍挂未勾)。
- **Neutral**:与 ADR-024 无耦合(一个管流内数据形状的家,一个管流外场景生命的家;
  共同点仅「都不开第四个登记处 / 不用 Single 模式」的同构纪律)。

## Risks

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| S3 前提被实测推翻(引擎行为不同) | 低 | 中 | 拆序第 6 步断言与实现无关 —— 即使引擎已管实例,断言零成本。**2026-09-23 实测:未推翻** —— 外部亲代实例 `unload` 后存活(泄漏形态成立);⑤ 前提坐实 |
| Addressables 6.2+ E-13 抛异常路径在 Boot 期崩溃形态难看 | 中 | 低 | ADR-014 已裁「启动期硬失败」;本件只补「失败日志含组名 + key」 |
| chunk 激活(⑥)与 ADR-022 切片粒度错位 | 中 | 中 | 激活权在 6 ⇒ 粒度争议变成 6 的 GDD 参数,不再悬空 |
| VR P1a 重排相机栈时破坏 ③ | 低 | 高 | ③ 只锁「Boot 持有 + 空栈起步」两条不变量,模式扩展不触它们 |

## Performance Implications

- 读档墙钟 = data-core 预载 + 重放/CatchUp + additive 加载 + 重建(④序列),P0 不设预算
  (数值轮另裁);⑤ 的登记簿是 O(实例数) 哈希,可忽略。
  —— **2026-09-23 实测参考值**(非预算,装置数据留档):additive 冷载 **116.8 ms** /
  暖载 **39.6 ms** · 卸载 **11.0 ms**;场景 load/unload 交替单次约 **14.7 ms**(≈0.9 帧@60fps),
  而单场景多根 `SetActive` 约 **0.011 ms** ⇒ 细粒度 chunk 激活走 `SetActive`(§Validation S4)。
- ② 铁律的副收益:World 场景文件近零 ⇒ Addressables 的 World 组只含视觉层资产,包体分块友好。

## Migration Plan

纯文档裁决,无既有实现可迁移(尚无代码)。落地次序:
1. 用户照准 ①⑥ → 转 Accepted(S1/S3/S4 spike 结果**至少回填后**再转,见 §Validation)
2. ② 的构建期扫描器 → Tooling 层故事(与 ADR-022 C 系工具同库)
3. `ISceneRouter` / `IWorldSpawner` 契约 → ADR #2 程序集清单落点(交叉依赖:本件 Key Interfaces 的
   程序集归属等 #2;不构成循环 —— #2 只裁「住哪」,不裁「有什么」)

**Rollback**:本件全部是文档 + 未来工具的规格;git revert 即回,无运行期状态。

## Validation Criteria

> **S1–S7 = unity-specialist 复核判定的「参考件哑项」。逐条未跑,本 ADR 转 Accepted 前 S1/S3/S4 必须有结论;S2/S5/S6/S7 最迟随各自实现故事。**

- [x] **S1** `Addressables.LoadSceneAsync(key, LoadSceneMode.Additive)` + `UnloadSceneAsync(handle)`
      在 6.3 实测可用作菜单/世界换入换出(参考件 `plugins/addressables.md:263-276` 载形状,未验行为)
      —— **✅ 实测通过 2026-09-23**(Unity 6000.3.24f1 · 装置
      `unity/Assets/Tests/PlayMode/u1_scene_spikes_test.cs` · 卡 `production/u1-spike-checklist.md` §4.1/§6):
      `cold_load_ms=**116.8**`(含 catalog / bundle 冷启)· `warm_load_ms=**39.6**` ·
      `unload_ms=**11.0**`;`load_status` / `unload_status` 均 `Succeeded`,`scene_still_loaded=False`
      (卸载干净),`op_ex=none` 全程,**0 条** Error/Exception/Assert 日志 ⇒ **E-13(6.2+ 抛异常)
      在本路径未触发**。→ 转 Accepted 硬前置满足。
- [ ] **S2** 「World.unity 零 gameplay 对象」构建期扫描判据可落地(场景 YAML 资产反序列化扫组件类型);
      **2026-09-21 扩充(RC-6)**:同扫描**增列「相机 / `AudioListener` 在非 Boot 场景 = 构建失败」**,连同「零 gameplay 对象」一并验
      ⏸ 延后(触发:首个 World/MainMenu 场景落地;卡 §3 同判)
- [x] **S3** `UnloadSceneAsync` **不销毁** `InstantiateAsync` 产物 —— 直接实测(⑤ 存在的前提);
      同时实测第 6 步断言在漏 Release 时的可观测性。**2026-09-21 判据补强(S-4 · 承
      `architecture-review-2026-09-20.md`)**:除「实例未销毁」外,须**补一条 bundle 引用计数归零断言**
      —— handle 全部 `Release` 后,Addressables 的 bundle refcount 必须归零(handle 全 Release 后
      bundle 仍被引用是 Addressables 已知形态;第 6 步运行期断言只查登记簿,**查不到 bundle 层**)。
      —— **✅ 实测通过 2026-09-23**(同装置;Unity 6000.3.24f1)。三条判据 + S-4 补强逐条落地:
      · **判据 1(⑤ 前提)**:外部亲代实例(`InstantiateAsync(key, holder)` 挂**未入场景**的根物体)
        `unload` 后 **alive=True** ⇒ **不销毁,泄漏形态成立** —— ⑤ 存在的理由坐实;
        场景内亲代实例(挂 `MoveGameObjectToScene` 进场景的 Marker)`unload` 后 **alive=False**
        (随 Unity 层级一起亡,**非** Addressables 反例)。
      · **判据 2(S-4 补强)**:bundle 计数 `baseline=**1** → after_load_inst=4 → after_unload=2 →
        after_release=**1**` ⇒ **两 handle 全 `Release` 后回到 baseline,refcount 归零**;
        中间 `>baseline` 段证实「handle 未全 Release 时 bundle 仍被引用」的已知形态存在
        ⇒ **第 6 步运行期断言只查登记簿、查不到 bundle 层**的设计有理。
      · **判据 3(可观测性)**:故意漏 `Release` ⇒ `judge3_leak_no_release_alive=**True**`、
        `leak_bundles=**2**(>baseline 1)`,`final_after_release=1`(补释放后归位)
        ⇒ **漏了是能被看见的**(实例残留 + bundle refcount 不归零)。
      · **新机制发现(源实读 2.10.3 @ `6fef233`,实跑确认)**:`InstantiateAsync` 默认
        `trackHandle:true`,实例亲代在**被卸载的场景**内时,场景卸载后
        `ResourceManager.CleanupSceneInstances` 会把该 tracked 实例 operation **减引用到 0 并自动释放**
        (实测 `handle_valid_after_scene_unload ext=True inScene=False`)⇒ **场景内亲代实例
        不得手动 `ReleaseInstance`**(会抛 invalid handle);⑤ 第 6 步要拦的是**外部亲代实例**。
- [x] **S4** chunk 级激活采用「Addressables 场景分块」还是「单场景内 SetActive」—— 两者的加载/卸载成本实测,⑥ 只裁归属不裁机制
      —— **✅ 实测通过 2026-09-23**(同装置 ×20 轮;Unity 6000.3.24f1):
      **A 路**(Addressable 场景 load/unload 交替)avg=**14.65 ms** · max=**24.19 ms** · total=293.0 ms
      (≈ **0.9 帧**@60fps);**B 路**(单场景双根 `SetActive`)avg=**0.0114 ms** · max=0.2056 ms
      ⇒ **A/B 量级比 ≈ 1285×**。
      **机制建议**(⑥ 只裁归属 ⇒ 本测只出建议,不产新 ADR):**细粒度 chunk 激活宜走
      「单场景多根 `SetActive`」;Addressable 场景 load/unload 只用于世界整体换入换出,不做细粒度 chunk 流式**。
- [ ] **S5** ADR-013 假设 6 spike 失败时,⑧ 触发条款的 Renderer Feature 路径确属必需(若 UI 兜底不需要它,条款保持休眠)
      💤 休眠(假设 6 未跑,用户裁定缓办;判据 ④ 静态已过 —— 见 ADR-013 §6.6 侧)
- [ ] **S6** 菜单时钟源:`Time.unscaledTime` 与 tick driver 停机(④)在「暂停/换场」两态的行为一致(承 `technical-preferences.md` OQ-25-8 ③ 相位义务,不由渲染帧)
      ⏸ 延后(触发:`ITickProvider` 实现落地)
- [ ] **S7** ⑦ 确定性偏移夹具:同一存档重建 3 次,生成位逐次一致(哈希),且无对象落入不可走格(复用 ADR-022 C2 可走性同源判据)
      🟡 哈希半边已落(EditMode `GoldenHashV1Test`);全夹具延后(触发:ADR-022 逻辑层落地)
- [x] **V-8** `architecture.md` #1 小节 / §3.4 [0][7] 两处 🔴 / QQ 台账在本件 Accepted 时同步改判
      —— **✅ 2026-09-23 回写轮执行完毕**:`architecture.md` §3.4 `[0]` 已改判 `✅ ADR-023 ①③
      (Boot 常驻含相机 + AudioListener)` · `[7]` 已改判 `✅ ADR-023 ④⑦(重建次序 + 确定性格内偏移)`;
      其下诚实标注块已重写为「2026-09-23 改判」并注明「本节自此不再挂 §Required New ADRs #1」;
      §System Layer Map 的 HIGH RISK ①(L5 Renderer Feature)/ ②(L0/L1 场景加载)两处、
      RenderGraph 承接注、row 6(世界与生态区)engine-risk 列、CameraMode engine-type 注**均已挂
      ADR-023 承接**。承本件 Accepted(2026-09-20)+ S1/S3/S4 实测通过(2026-09-23)。
      **本行记绿条件(原文自设)已满足** —— 不再挂「未改前不记绿」。

## GDD Requirements Addressed

| TR / 义务 | 来源 | 本 ADR 如何覆盖 |
|---|---|---|
| `architecture.md` §Required ADRs #1 ①②③④ | 架构 v1.0 自登记 | ① 生命周期 / ④ 相机栈形状 / ③④ tick 次序 / ④=本件 ⑧+§Key Interfaces(L4 引擎 API 面) |
| §System Layer Map 🔴① 🔴② | 架构 v1.0 | RenderGraph 面 = ⑧(默认零使用 + 触发条款);场景加载面 = ①⑤ |
| §3.4 [0] / [7] 🔴 | 架构 v1.0 | ④ 序列 + ③(Boot 常驻件)[0];⑦(表现态重建)[7] |
| `TR-concept-*` 场景加载相关项 | `tr-registry.yaml`(回写轮逐条判) | **✅ 2026-09-23 回写轮已判**:新增 `TR-worldeco-010`(chunk 激活权 = 6,`adr: ADR-023`,`covered`);`TR-death-004`(复活位置重建,`adr: ADR-009 + ADR-023`)与 `TR-audio-013`(`AudioListener` 单挂点归 Boot,`adr: ADR-020 + ADR-023`)两条既有条目已带 ADR-023 挂钩。⚠️ **无其他场景加载专属 TR 待挂** —— `game-concept` 的 `TR-concept-*` 8 条中零条为场景生命周期需求(`TR-concept-007` 急救延迟 / `TR-concept-008` 焦点导航均不属本件),故无「预挂」动作可执行。 |
| TD 条件 C2 的一半 | 架构 v1.0 | Accepted 即结(#2/024 同批)|

## Related

`docs/architecture/architecture.md`(§Required ADRs #1 · §3.4 · §System Layer Map 🔴①②)·
`adr-005` · `adr-010` · `adr-013`(⑧ 触发条款的开关)· `adr-014` · `adr-015` · `adr-018`(③)·
`adr-020`(③⑦)· `adr-022`(②⑥ 的 Tooling 层先例)· `adr-024`(同批 C2,无耦合)
