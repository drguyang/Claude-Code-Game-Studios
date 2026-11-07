# Story 001: 分册态数据契约与存档段

> **Epic**: 脉案
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 4h
> **Manifest Version**: 2026-11-02
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/casebook.md`(规则一 分层边界 · 规则三 分册与跨机私有 · F-39.3 分册态数据形状)
**Requirement**: TR-casebook-001(分册态持久化 = ADR-010 义务 13 的落实) · TR-casebook-002(`player_id` 发号权威,residual 45 铸造契约不得记绿)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-010(主): 7a 持久化与存档格式 · ADR-025: 契约程序集清单 · ADR-006: 定点域边界数据契约
**ADR Decision Summary**: 39 的分册态(两个 tick · 分册内容 · 页码/折叠状态)是三流之外的**第四类数据**,必须在 ADR-010 §一 存档布局中占独立「分册态段」槽(§三 义务 13);全二进制 codec,禁 `float` / 禁 Unity 内置序列化器承载 `Fix`(ADR-006 D-21-18,自定义编码器 + EditMode 探针);整数 tick 与 `player_id` 半住 `Sim.Contracts`,呈现 DTO 半住 `Gameplay.UI`。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW
**Engine Notes**: 数据结构 + 二进制编解码 = 纯 C# / BCL,零 post-cutoff API(承 ADR-010「只用 BCL 稳定 API」纪律)。

**Control Manifest Rules (this layer)**:
- Required: 存档与事件流禁 `float`;`Fix` 经自定义编码器(非 Unity serializer);分册键 = `player_id`(int,经 `IIdAuthority` 发号);两个 tick 以 `long` tick 计承载
- Forbidden: 在 39 侧新立第二玩家计数器;分册态写入三流(39 对 `IEventSink` **零调用**);用 constant 0 当单机分册键(发号语义必须真走)
- Guardrail: 分册**跨机私有** —— 存档迁移 / 联机合并时分册不并册(规则三),codec 不做任何并册逻辑

---

## Acceptance Criteria

*From GDD `design/gdd/casebook.md`, scoped to this story:*

- [ ] 分册态数据形状定义完整:每病人一份分册,内含「上次快照 tick」「上次重查 tick」+ 已落笔判断的呈现缓存(分册正文)
- [ ] 分册态在 ADR-010 存档布局中占独立「分册态段」槽(义务 13),读档后两个 tick 逐位还原
- [ ] 段内全部字段 ∈ 整数域(tick = long、player_id / patient_id = int、正文 = 整数编码词表 id),零 `float` / `double` 字面量(静态检查)
- [ ] `Fix` 字段(如有)经自定义二进制编码器往返,EditMode 探针守住「不经 Unity serializer」
- [ ] 分册键 = `player_id`,由 `IIdAuthority` 发号(承 ADR-006 Amendment B 机制 A:计数器永不复位、迁移后 `next = max+1` 由事件流重构)
- [ ] 39 零 `IEventSink.Append` 调用 —— 分册态**不进三流**(第四类数据,非第四条流)
- [ ] 损坏存档读到分册态段校验和失败 ⇒ 自动回退上一 checkpoint(ADR-010 §四),不崩溃

---

## Implementation Notes

*Derived from ADR-010 / ADR-025 Implementation Guidelines:*

1. 新增 `CasebookSegment` 数据结构住 `Gameplay.UI`(呈现半)+ 整数半(`player_id` / 两个 tick)住 `Sim.Contracts`(ADR-025 清单封闭性 —— 未登记 asmdef = 构建失败,须在清单内)。
2. 7a codec 侧新增段读写器:**段头(magic + version) + 每分册变长块**;变长块用「长度前缀 + 整数词表 id 序列」,禁任何浮点。
3. 两个 tick 的**语义归 8 / 37 判定链消费**(快照新鲜度 / 重查窗口),39 只持有与序列化 —— 本 story 不实现判定链读取。
4. 词表 id 闭集 = 8 的判断记录词表(病名 / 置信档位等),经 ADR-014 烘焙管线读入(作者态 JSON → `.cooked`);39 不解析 JSON。
5. 校验和沿用 ADR-010 全文件级 checksum(段内不另立),回退路径复用既有 checkpoint 机制 —— 只补「分册态段缺失 ⇒ 视为空分册(可重建:分册正文可从病例流判断记录重放)」。
6. ⚠️ **数值冻结**:分册纸页容量 / 块对齐粒度等手感值归用户数值轮;本 story 只定 schema 与编解码。

---

## Out of Scope

- [Story 003]: 分册正文的读时投影(改写痕 / J(c,p) 折叠)
- [Story 002]: 病例列表排序(读时计算,与本段的落盘无关)
- [Story 006]: 落笔 → checkpoint 锚点 ① 的触发钩子
- 45 铸造契约(TR-casebook-002 residual ②):`player_id` 跨机稳定性验证 → BLOCKED-BY-45 轮,P0 单机不阻塞
- VR 侧分册呈现(P1a,承 ADR-013 §十)

---

## QA Test Cases

> 🔁 **2026-11-08 回填(sprint-26 `/qa-plan`)** —— 本节由 `production/qa/qa-plan-sprint-26-2026-11-08.md` §S26-1 派生。
> 原稿四条规格保留其判据内核,补了边界、假绿防线与「哪条不算覆盖」。

**[Logic story — automated test specs]**
文件:`unity/Assets/Tests/EditMode/Casebook/casebook_segment_codec_test.cs`(约 10 条)

- **AC-1 · 段 round-trip 逐位相等**
  - Given: 2 玩家 × 3 病人、两个 tick 各不同
  - When: 写出二进制段 → 读回
  - Then: 全部字段 `bit-identical`(**逐位**,不是「值相等」)
  - Edge: 空分册(0 病人)round-trip = **恒等**;tick 取 `long.MinValue` / `0` / `long.MaxValue` 三档不失真

- **AC-2 · 段内零浮点(反射)**
  - Given: 分册态段的全部序列化类型
  - When: 反射扫字段类型,**含泛型实参递归**
  - Then: 无 `float` / `double`
  - Edge: 词表 id 越界(∉ 闭集)⇒ 解码**硬失败**,非静默吞

- **AC-2b · `Fix` 不经 Unity serializer(D-21-18 探针形制)**
  - Then: `Fix` 走自定义二进制编码器;断言**不是** `JsonUtility` / `ISerializationCallbackReceiver` 路径

- **AC-3 · 分册键走真发号**
  - Given: 全新存档,计数器起点
  - When: 两名玩家相继进入
  - Then: 分册键 = `IIdAuthority` 给的 id(**非恒 0**);同玩家重连**不重发**
  - Edge: 单机 P0 仍走发号路径,**不特判常量 0**

- **AC-4 · 39 零 `IEventSink.Append`**
  - 判据:构建期 / 静态扫描 39 程序集内 `Append` 调用计数 = 0;**非运行时计数**

- **AC-5 · 段损坏自动回退**
  - Given: 人为翻转分册态段一字节
  - When: 读档
  - Then: checksum 失败 ⇒ 回退上一 checkpoint;**三流完好时游戏可继续**(不判全档作废)
  - Edge: 仅段缺失 ⇒ 空分册,且从病例流判断记录**重放出同一分册**

- **边界与假绿防线**
  - **F9**:每条 fail-closed 自述单独对 `default(T)` 验一遍(构造器守卫可被绕过)
  - **F4**:正控前先断「构造确实改变了被测面」,否则「恒等 round-trip」在空实现下也绿

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/Casebook/casebook_segment_codec_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: ADR-010 7a codec 基建(既有)· `IIdAuthority` 机制 A(既有,ADR-006 Amend. B)
- Unlocks: Story 003(分册正文投影需要段 schema 就位)· Story 006(落笔写分册 + 锚点钩子)

---

## Completion Notes

*(empty — fill at story completion via `/story-done`)*
