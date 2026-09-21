# Play Mode Tests

在真实游戏场景中运行的测试。用于**跨系统集成**:急救动作流程、建造放置与邻接判定、
联机基础(ADR-001 pipe 抽象的收发)、存档/读档往返、场景加载生命周期。

## 本项目的 PlayMode 覆盖面

| 集成域 | 断言要点 | 权威来源 |
|---|---|---|
| 急救动作流程 | 一条动作落**两条**病史流事件(`EmergencyAttempt` + `EmergencyTreatmentApplied`);`potency` 由主机 `Judge` 得出 | ADR-009 Amendment I · ADR-011 Amendment B |
| 输入延迟 | 只测 `L_input`(预表现路径),**不**测端到端 | ADR-011 Amendment B / `AC-10-08` |
| 建造 | 整数格邻接判定,**非物理、非 NavMesh**;槽位与地形共用同一格 | ADR-015 §五 |
| 拾取 | 意图事件 + 当下判距 + 宽容半径 | ADR-009 §七 |
| 玩家跨格 | 连续位置/速度/朝向**永不**写流;事件率上界 = tick 频率 | ADR-020 §四(`AC-20-03` BLOCKING,判据 = 反射断言载荷字段类型,**不是 grep**) |
| 存档往返 | 三流全序键 `(Tick, StreamPriority, Patient, Seq)`;禁 `float` | ADR-008 · ADR-010 |
| 表现层 DTO | `PresentationDtoGuard` **递归**反射扫描 —— `disease_id` 不进呈现层 | ADR-013 §9 C3 / `AC-37-15` |
| 音频触发白名单 | 禁 sting / jingle / ducking / 素材切换**报**状态 | ADR-018(`AC-44-09` BLOCKING) |

## 程序集要求

需要 `tests/PlayMode/PlayModeTests.asmdef`(**本次刻意不生成**,见 `tests/README.md` §未生成之物)。

## 两条硬约束

1. **tick 不由渲染帧驱动** —— `technical-preferences.md` 已裁定 `TICK_SECONDS = 0.05`,
   与 60 fps 帧时间**不整除**(一个 tick 跨约 3 帧)。PlayMode 测试若用
   `yield return null` 数帧来推进 sim,测的就不是生产路径。步相位须由 `ITickProvider` 驱动。
2. **禁 VR 项进 P0 判据** —— VR 急救归 P1b;`AC-3-B1b` 已把「三端实测」的 VR 侧删出 P0。

## 不适合 PlayMode 的东西

`Visual/Feel` 类(动画曲线、VFX 外观、shader 输出、主观手感)按
`coding-standards.md` §What NOT to Automate **不自动化**,走 `tests/evidence/` 的截图 + 签核。
