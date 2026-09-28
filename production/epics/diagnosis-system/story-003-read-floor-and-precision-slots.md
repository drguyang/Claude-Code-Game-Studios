# Story 003: F-8.1 可读地板与 F-8.2 精度档槽

> **Epic**: 诊断与体征揭示
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 8h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-28

## Context

**GDD**: `design/gdd/diagnosis-system.md`(§F-8.1 READ_FLOOR 与可读双条件 · §F-8.2 SLOT_BOUNDS/TierIndex · 规则四 熟练度只改「多细多可信」永不改「有没有」 · 护栏 G-1/G-3)
**Requirement**: TR-diag-008(G-1 禁 libm 超越函数,指数仅取整数或 1/2)· TR-diag-009(G-3 档位判定走整数等级)· TR-diag-012(D-8-5:`READ_FLOOR_MIN > 0` 且 ≥ `Project(σ)`)· TR-diag-011(D-8-4 的 8 侧消费面:读 `Project(Sign_j) ∈ [0,1]`,该投影归 9)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-026(`FixPow` 唯一整数幂;8 侧等价物 = **预计算定表**,story 001 已锁形状)· ADR-006(若定表走 Fix 生成,舍入 = HALF_AWAY_FROM_ZERO)· ADR-005(`Sign_j` 来自 `VitalsDto`,8 不重算)
**ADR Decision Summary**: F-8.1 `READ_FLOOR(Skill) = READ_FLOOR_MIN + (BASE_READ − READ_FLOOR_MIN) × (1−s)^READ_GAMMA`;`可读_j ⟺ Skill ≥ tier_named_j ∧ Sign_j ≥ READ_FLOOR`(**与关系**,两条件缺一不可读);`display_词_j` 永远出词(slot_j)—— **有词 ≠ 可读**。F-8.2 `TierIndex = #{b ∈ SLOT_BOUNDS: b ≤ Skill} ∈ {0..3}`;空白档向下回退,全空 ⇒ 阴性形态非未查。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: LOW(定表 + 查表 + 整数比较;唯一 HIGH 邻居 = AC-8-F5 跨平台浮点一致,归 story 004 矩阵面)
**Engine Notes**: G-3 判据「途径整数等级」= 档位判定比较 `Skill` 与 `SLOT_BOUNDS` 均为 int,不经浮点;浮点边界注入用例不得改变 `slot_j`。

**Control Manifest Rules (this layer)**:
- Required: `BASE_READ > READ_FLOOR_MIN > 0` 与单调性为**测试级硬约束**(违反即失败,不是运行期兜底);定表覆盖 `Skill ∈ [0, SKILL_CAP=60]` 全整数档
- Forbidden: 运行期 `Math.Pow`;熟练度改变「有没有」(读数存在性只由 9 的 `Sign_j` 与手段决定);把「读不出」呈现成「未查」(AC-8-22 归 story 005,判定语义在此)
- Guardrail: 曲线系数(`READ_GAMMA`/`BASE_READ`/`READ_FLOOR_MIN`)值归用户数值轮 ⇒ 端点/单调/双条件用**合成系数**判形状;数值轮到位后重签黄金向量

---

## Acceptance Criteria

*From GDD `design/gdd/diagnosis-system.md`, scoped to this story:*

- [ ] **AC-8-5**[L] BLOCKING:C-1 端点与单调 —— `READ_FLOOR(0)=BASE_READ` · `READ_FLOOR(SKILL_CAP)=READ_FLOOR_MIN` · `BASE_READ > READ_FLOOR_MIN > 0` · ∀ `Skill₁<Skill₂ ⇒ READ_FLOOR(Skill₁) ≥ READ_FLOOR(Skill₂)`;扫描全整数档
- [ ] **AC-8-F3**[L] BLOCKING:`READ_FLOOR_MIN > 0`(回归锚:地板不为 0 ⇒ 满技能也留「读不出」的合法空间);与 D-8-5 的 `≥ Project(σ)` 子句联动断言(σ 来自 9 fixture)
- [ ] **双条件「与」门**[L]:构造四象限夹具(`Skill ≥ tier ∧ Sign ≥ FLOOR` / 仅前者 / 仅后者 / 皆非)⇒ 可读恰 {(1,1)};**`Skill < tier_named` 时即使 `Sign` 满值也不可读**(熟练度不发明体征)
- [ ] **有词 ≠ 可读**[L]:同一 `(Skill, sign_id)` 下 `display_词`(slot_j)恒有值,而「可读」四态独立 —— 断言两输出在夹具矩阵中可分(承 story 005 的状态机语义输入)
- [ ] **AC-8-7**[L] BLOCKING:G-3 档位判定走整数等级 —— 每个切点 `T` 构造 `Skill=T−1/T` 落相邻两档;插入 `Precision(T)` 浮点边界用例**不改变** `slot_j`
- [ ] **AC-8-8**[L] BLOCKING:空白档向下回退(`sign_koplik` 夹具:粗/中为「—」)⇒ 粗、中档回退到最近非空档;更低档全空 ⇒ **读不出记为阴性形态,不是阳性**;「不得把空档读成一定读得到」
- [ ] **AC-8-9**[L] BLOCKING:手段不上锁(裁定⑨)—— `reveal_by` 五值全部在最小合法 Skill 可执行、有读数、有 `EmitGrowth` 意图;∀ Skill 不存在「手段不可用/锁闭」状态(`[U]` 无灰按钮半边归 story 006)
- [ ] **G-1 定表化**[A]:运行期零幂运算 —— 查表命中断言 + IL 扫描(复用 story 001 机制,本 story 覆盖 F-8.1 表);定表生成期舍入 = HALF_AWAY_FROM_ZERO
- [ ] **掉级回升**[L]:AC-8-46 前半 —— `Skill` 降跨切点 ⇒ `READ_FLOOR` 回升、`slot_j` 跌回低档、词变粗(曲线单测半边;病名不回退归 37,走查半边 story 006)

---

## Implementation Notes

*Derived from F-8.1/F-8.2 + ADR-026:*

1. 定表 = 烘焙产物(story 002 管线扩 `READ_FLOOR_TABLE[0..60]`);源 = 合成系数或数值轮系数,**换系数即换表,代码零改动**;表哈希进 story 002 的哈希不变判据。
2. `s = Skill/SKILL_CAP` 的比值运算在**生成期**以 Fix 完成(整数域);运行期 8 只见 float 定表值(经门面上方 = 合法,G-4 位宽 Single)。
3. `TierIndex` 实现为 `SLOT_BOUNDS` 计数比较(int),禁浮点插值;档序语义 0=粗 1=中 2=细 3=满 与 `DIAG_TIERS` 的映射写死为常量注释引 GDD。
4. 回退算法:自 `TierIndex` 向下找首个非空档;全空返回 `UNREADABLE_NEGATIVE` 哨兵(与「未查」`BLANK` 三值分开 —— story 005 状态机的输入字母表在此钉死)。
5. 合成 fixture 系数至少覆盖:单调严格 / 平段 / 端点相等三类形态,防「单调断言被平台噪声假绿」。

## Out of Scope

- [Story 004]: F-8.3 阴性把握度族(独立参数集)
- [Story 005]: 四态状态机消费哨兵;`EmitGrowth` 实际门控调用
- [Story 006]: 呈现(词/墨/压痕)
- 数值轮系数定值(用户)

## QA Test Cases

*Written at story creation(lean mode).*

- **端点+单调扫描**: 61 档全查,断言三端点与单调对(AC-8-5;违即红,非兜底)。
- **四象限矩阵**: 双条件门的表驱动 2×2(+边界 `=` 恰值)⇒ 可读集恰 {1,1}(双条件回归)。
- **哨兵三值**: `BLANK ≠ UNREADABLE_NEGATIVE ≠ POSITIVE`,夹具矩阵可分(承 AC-8-21/22 语义输入)。
- **切点邻居**: 每个 `T ∈ {10,20,50}`:`slot(T−1) ≠ slot(T)` 且相邻;浮点扰动注入 ⇒ 不变(AC-8-7)。
- **回退**: koplik 夹具三档查询 = (细词, 细词, 细词?)按表定义;全空档词条 ⇒ NEGATIVE 哨兵(AC-8-8)。
- **掉级**: Skill 50→49 跨切点 ⇒ 词档回退;定表单调保证 floor 升(AC-8-46 半)。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/DiagnosisSystem/read_floor_slots_test.cs` — must exist and pass
**Status**: [ ] Created — NOT STARTED

---

## Dependencies

- Depends on: Story 001(边界/定表形状)、Story 002(词条数据 + 表管线)、disease-simulation epic story 004(`Project(Sign_j)` 投影与 σ,D-8-4)、skill-system epic(`SKILL_CAP`/`QueryLevel`)
- Unlocks: Story 004(双族正交断言的另一族)、Story 005(状态机输入字母表)、Story 006(精度档渲染)

## Completion Notes
