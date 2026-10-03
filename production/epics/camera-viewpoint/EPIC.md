# Epic: 摄像机与视角

> **Layer**: Presentation(控制器与相机 · manifest 该节适用)
> **GDD**: design/gdd/camera-and-viewpoint.md
> **Architecture Module**: 自建机位 `ICameraRig`(四段机器:跟随 + 绕点 + 出臂 + 收缩)+ 对系统 1 的硬交付 `YawBasis`
> **Status**: In Progress (5/6 stories complete —— 002 · 001 · 003 · 004 · **005 ✅ 2026-10-03**;余 006 Ready)
> **Stories**: 6 stories created (2026-09-28)

## Overview

摄像机与视角(系统 2)是呈现层三件套(2 / 42 / 44)中「只读、永不持有游戏状态」纪律的相机侧执行者:平面模式 = 第三人称越肩(自建机位,**零 Cinemachine**),VR = 独立第一人称(P1a,平面链冻结)。本 Epic 覆盖:① 呈现纪律与边界(状态不进门、`manifest.json` 零第三方、引用集白名单、零 `SimEvent`、镜头效果归属与 `AudioListener` 唯一);② **对系统 1 的唯一硬交付 `YawBasis`**(水平化 / 正交归一 / 手性 / `PITCH_MAX < 90°` / 帧内次序契约 —— 「系统 1 全程可编译、可测试通过,但游戏不可玩」的补位);③ 机器行为四段(二阶临界阻尼锚跟随半隐式+子步、`dt` 位移预算钳位、绕点解耦、出臂几何、收缩瞬时/回弹阻尼的非对称);④ 档位状态机(意图制 `SetMode`、优先级 `Casebook > Treatment > Explore`、每帧一结算、不过冲转场、不自行超时);⑤ 性能义务(每帧 PhysX 查询 == 1、`Casebook` 档 == 0、`Tick` 单一显式相位);⑥ 跨系统登记义务对账与舒适度签核面。**全部数值留白归用户**(AC-20-11),通用口径 = **「装载期断言 + 留白数值」:取值一旦存在即被守住;缺键 ⇒ 拒绝启动或 `INCONCLUSIVE`,不得记绿**。

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-020: 玩家控制器与相机 | 相机 = **自建机位**(`ICameraRig`,不引入 Cinemachine,AC-20-02);§五 相机只读不持状态(AC-20-05);§六 镜头效果归属 = 8 语义 + 2 实现、VR 全禁、不得报状态;§七 `AudioListener` 单挂点;Amendment B `YawBasis` 硬交付(AC-20-14);全部数值留白(AC-20-11) | LOW |
| ADR-011: 输入架构 | `Look` 动作映射;`InputSystem.onAfterUpdate` 固定相位 = `AC-2-10①` / `AC-2-27③` 求值次序的机制来源 | HIGH(6.3 行为须实测;接口层纯 C#) |
| ADR-013: 拟物 UI 框架 | 呈现层三件套同构纪律(§9 C3);`ModalId` 闭集(39 发 `Casebook` 的呈现侧);`IModalState` 只读 | HIGH(部分须 spike;本 Epic 只消费只读面) |
| ADR-023: 渲染与场景加载 | 相机与 `AudioListener` 归 **Boot 常驻场景**;`World.unity` 零 gameplay GameObject(AC-2-06④ 的全场景计数判据以 Boot 拓扑为参照) | HIGH(Addressables/RenderGraph post-cutoff;判据本体不依赖) |
| ADR-015: 世界几何 | 诊疗台越肩可用位 = 烘焙逻辑层几何声明(`O-13` 的落点) | LOW |
| ADR-014: 数据管线 | 档位参数表 / 效果触发表走两阶段烘焙;数值经 `FixParse` 口径仅 sim 侧,相机参数为表现域浮点(**永不进流**) | MEDIUM |
| ADR-018: 音频架构 | `AC-2-06③` 的效果触发源白名单 = 该 ADR §六 音频白名单**同源导出**(不另立一份);§七 `AudioListener` 挂点由 ADR-020 §七 结清 | MEDIUM |
| ADR-001: 网络 pipe | 相机**零网络义务**(不写流、不上行)—— 列入仅供边界核对,无实现依赖 | — |

**Engine Risk**: **LOW 为主**(自建机位刻意不用任何 post-cutoff API;`SphereCast` / `Mathf` 长期稳定)。抬到须实测的只有:`InputSystem.onAfterUpdate` 相位落点(ADR-011,实现期二选一并落注释)与舒适度/取景延迟的 playtest 面(不可自动化,coding-standards「什么不该自动化」)。

## GDD Requirements

> 登记处:`docs/architecture/tr-registry.yaml`(`id: TR-camera-*`)。
> 计数(实测):**6 条全部 covered**;**Untraced = None**。
> `TR-camera-006`(`YawBasis`)为 2026-09-16 Amendment B 新立、GDD 落盘后 partial→covered —— 它是本 Epic 唯一「缺了会不可玩」的 TR。

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-camera-001 | 平面模式视角 = 第三人称(越肩) | ADR-020 ✅(判据 = `AC-2-25④` + `AC-2-14`) |
| TR-camera-002 | VR = 第一人称、不承担开放世界移动;P0 只落接口 | ADR-020 ✅(R-2-9 / EC-2-11;实现推 P1a) |
| TR-camera-003 | 相机只读、永不持有游戏状态;不写三流、不引用 sim 程序集 | ADR-020 ✅(`AC-2-01/04/05`) |
| TR-camera-004 | 镜头效果归属(8 语义 + 2 实现)与 VR 禁用清单 | ADR-020 ✅(`AC-2-06`) |
| TR-camera-005 | 44 的 `AudioListener` 单挂点落定 | ADR-020 ✅(`AC-2-06④`;双向已核) |
| TR-camera-006 | `ICameraRig.YawBasis` 水平化正交基,只读单向 | ADR-020 Amendment B ✅(`AC-2-07…10`) |

## Stories

| # | Story | Type | Status | Layer | ADR |
|---|-------|------|--------|-------|-----|
| 001 | 呈现纪律与边界(不持状态 / 零第三方 / 零事件 / 效果归属 / AudioListener) | Logic | Ready | Foundation | ADR-020 + ADR-013 + ADR-023 |
| 002 | 硬交付 `YawBasis`(系统 1 的 O-8:水平化 / 正交归一 / 俯角界 / 次序契约) | Logic | Ready | Foundation | ADR-020 Amendment B + ADR-011 |
| 003 | 锚跟随与绕点(二阶临界阻尼半隐式 + 子步 / `dt` 位移预算 / yaw-pitch 解耦) | Logic | Ready | Core | ADR-020 + ADR-005 |
| 004 | 出臂与收缩(肩位常量几何 / 瞬时收缩·阻尼回弹 / 掩码与裁剪面) | Integration | Ready | Core | ADR-020 + ADR-015 |
| 005 | 档位状态机与性能义务(意图制 / 优先级 / Casebook 冻结 / PhysX==1 / Tick 相位) | Logic | Ready | Feature | ADR-020 + ADR-011 |
| 006 | 跨系统义务对账与舒适度签核面(EXTERNAL 登记 / O-11…O-16 / VR 接口 / playtest) | Visual/Feel | Ready | Feature | ADR-020 + ADR-015 |

Counts: 4 Logic · 1 Integration · 1 Visual/Feel = 6 total.
27 个 AC 条目全覆盖:**BLOCKING 22**(`01/02/04/05/06` → 001;`07–10` → 002;`11/12/13` → 003;`14/15/16/25` → 004;`17/18/19/20/27` → 005;`22` → 006)全部落 story;**ADVISORY 3**(`03/24/26` → 006 的 playtest 签核面,不得混入 BLOCKING 计数);**EXTERNAL 2**(`21` = `PITCH_MAX` 取值归用户 · `23` = `O-13` 验收归相机 spike —— 006 登记挂账,不计入本 Epic 就绪度)。

## Epic Status

**In Progress** — 6 stories created 2026-09-28;零 ADR-blocked story(治理 ADR 全部 Accepted)。

## Key Cross-References

- **Story 002 = 玩家控制器 Epic Story 002 的硬前置**(1 的 `O-8` / `AC-1-31` 回指本 Epic 的 `YAW_BASIS_EPS` 常量符号;两侧不得各写一个字面量)
- **`AC-2-02` 的可执行性已变**:GDD 2026-09-16 注「`Packages/manifest.json` 本仓当前不存在」—— **现已存在**(根 `Packages/` 与 `unity/Packages/` 两处),判据按「文件存在 ⇒ 断言其内容」执行,**两处都要扫**(漏一处 = 该条原文点名的假阳性机器变体)
- **可签署性实测(2026-09-16,GDD 收尾口径)**:`AC-2-05` 卡「测试载体未建」记 NOT-RUN;`AC-2-09` / `15②` / `25③④` 空值上不可执行 ⇒ 按「取值一旦存在即被守住 + 缺键拒绝启动/INCONCLUSIVE」口径;**禁借绿**
- **载体纪律**:`tests/` CI 载体由 ADR-012 轮建;此前 AC 只可标「已定义」不得标「已验证」
- **`O-14` 对侧义务**:系统 1 story 002 已交付其消费侧(每帧取样一次 / 零写入);1 侧「确认次序契约」的 GDD 回刷行在 006 的对账表内登记

## Next Step

Story 002(`YawBasis`)优先开工 —— 它同时解锁玩家控制器 Epic Story 002/003;001 并行(纯边界判据);003–005 依四段机器顺序推进;006 的 playtest 面待可玩构建。
