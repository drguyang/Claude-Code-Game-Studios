# OQ-10-6 + OQ-10-12 裁决文档

**日期**: 2026-10-02(成文) · **2026-10-06(裁夺取向 + 回写结案)
**状态**: ✅ **已裁(用户 2026-10-02 取建议甲)** —— 两条均按本文建议落地
**阻塞**: ~~emergency-procedures 6 stories~~ **已解除**
**裁决记录**:

| OQ | 裁向 | 依据 | 回写落点 |
|----|------|------|----------|
| **OQ-10-6** | **甲 · 归系统 10** | 语义归属 / 枚举值特有 / 避免反向依赖 / 代码已按甲落地 | `emergency-procedures.md` OQ 行 + `:217` + `:835` 注 · `entities.yaml`(`EmergencyAttempt.action` 类型源)· 本文件 §裁决 |
| **OQ-10-12** | **乙 · 推迟 P1a** | 见本文件 §OQ-10-12 四判据 | `emergency-procedures.md:1077` 已于 **2026-09-29** 先行结案(原型实测 L_input < 35 ms)**—— 两处口径已对齐** |

---

## OQ-10-6: `EmergencyAction` 枚举归属

### 问题

`EmergencyAction` 枚举应归哪个系统？
- **选项 A**: 系统 10（急救动作）
- **选项 B**: 系统 21a（物品数据库）

### 背景

- `EmergencyAction` 是急救动作的类型枚举（CPR/止血/包扎等）
- 系统 10 是急救动作的 sim 层实现
- 系统 21a 是物品数据库，管理物品/配方/材料
- 当前 `action_id` 类型来源未定，导致 DC-4 校验 NOT-RUN

### 分析

| 维度 | 选项 A（系统 10） | 选项 B（系统 21a） |
|------|------------------|-------------------|
| 语义归属 | ✅ 急救动作是系统 10 核心职责 | ❌ 物品数据库不应包含动作枚举 |
| 枚举值 | CPR/止血/包扎是急救动作特有 | 物品数据库无动作概念 |
| 依赖方向 | 系统 10 定义，其他系统引用 | 系统 21a 定义，系统 10 引用（反向依赖） |
| 代码组织 | ✅ 内聚在急救动作模块 | ❌ 分散在物品数据库 |

### 建议

**选项 A：归系统 10**

理由：
1. 急救动作是系统 10 的核心职责
2. 枚举值（CPR/止血/包扎）是急救动作特有的
3. 系统 21a（物品数据库）不应包含动作枚举
4. 避免反向依赖（系统 10 → 系统 21a）

### 裁决后行动(✅ 2026-10-06 逐条核销)

1. ~~在 `unity/Assets/Sim/EmergencyProcedures/` 定义 `EmergencyAction` 枚举~~ ✅ **已存在**:`unity/Assets/Sim/EmergencyProcedures/EmergencyAction.cs:20`(P0 = `HemostasisBandage=0` / `RhythmVentilation=1`)
2. ~~更新 `entities.yaml` 注册 `action_id` 类型~~ ✅ 已回写:`EmergencyAttempt.payload_schema.action` 注明「类型源 = `EmergencyAction` 枚举 ordinal,归系统 10,非 21a 物品表」
3. ~~解除 Story 002 的 DC-4 BLOCKED 状态~~ ✅ **已解除**:`story-002:38` 的 DC-4 已按裁定表述;`action_tables_bake_test.cs:90-118` 实跑 `Enum.IsDefined`

⚠️ **一条已修的三处不一致(本文件结案时发现)**:`story-002:38` / `story-007:147` 曾引
`oq-adjudication-2026-10-01.md` —— **该文件不存在(幽灵引据)**,而本文档实际名
`oq-10-6-oq-10-12-adjudication.md`(2026-10-02 提交 `163cb1d`)。两处引据已在同批订正为本文档。

---

## OQ-10-12: 原型门是否推迟到 P1a

### 问题

急救动作的 `L_input < 50 ms` 原型门应在何时执行？
- **选项 A**: P0 立即执行（写第一行代码前）
- **选项 B**: 推迟到 P1a

### 背景

- 急救动作是全案唯一需要手感的系统
- `L_input < 50 ms` 是硬预算（ADR-011）
- 当前无 VR 设备，无法实测
- 原型门需要 2 个动作的垂直原型

### 分析

| 维度 | 选项 A（P0 立即） | 选项 B（推迟 P1a） |
|------|------------------|-------------------|
| 排程影响 | 🔴 阻塞 emergency-procedures 6 stories | ✅ 不阻塞 P0 开发 |
| 设备需求 | 需要 VR 设备（当前无） | P1a 时设备就绪 |
| 验证目标 | P0 验证核心循环，不是手感 | P1a 专门优化手感 |
| 风险 | 无法实测 → 原型门形同虚设 | 手感优化有充足时间 |

### 建议

**选项 B：推迟到 P1a**

理由：
1. P0 目标是验证核心循环，不是手感调优
2. 当前无 VR 设备，无法实测 `L_input`
3. 急救动作的 `L_input < 50 ms` 是 P1a 的优化目标
4. 推迟不阻塞 P0 其他系统开发

### 裁决后行动

1. 在 `emergency-procedures/EPIC.md` 标记 OQ-10-12 为 `DEFERRED-P1a`
2. 解除 Story 002 的 BLOCKED 状态（DC-4 仍 BLOCKED-BY-OQ-10-6）
3. P1a 开始时执行原型门

---

## 总结

| OQ | 建议 | 影响 |
|----|------|------|
| OQ-10-6 | 归系统 10 | 解除 Story 002 DC-4 BLOCKED |
| OQ-10-12 | 推迟到 P1a | 解除 Story 002 BLOCKED，P0 继续开发 |

**用户裁决后**：
1. 更新 `production/epics/emergency-procedures/EPIC.md`
2. 更新 `production/session-state/active.md`
3. 更新 `production/sprints/sprint-03.md`
