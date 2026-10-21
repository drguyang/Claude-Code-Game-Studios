# Story 003: 乘子求值 F-24-1/2/3(EnvMod / EquipMod / adj_sum)

> **Epic**: 医馆即机器
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-10-21
## Context

**GDD**: `design/gdd/clinic-machine.md`(规则四 EnvMod · 规则五 EquipMod · F-24-1 / F-24-2 / F-24-3 · 无 clamp 铁律)
**Requirement**: TR-clinic-003(EnvMod 在定点域内透传,边界不中转 float) · TR-clinic-006(整数溢出断言落求值侧) · TR-clinic-001(邻接加成 = 整数格判据)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005(主): 确定性模拟;ADR-006: 定点域边界
**ADR Decision Summary**: 全部模拟数学在整数定点域(Q16.16,int64);单一舍入 `ROUND_HALF_AWAY_FROM_ZERO`;中间 128 位积走 hi/lo 双 `ulong`(ADR-005 Amendment G,禁 `System.Int128`/`BigInteger`);12 位定点右移禁有符号语义错误。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: MEDIUM
**Engine Notes**: 纯 BCL 整数算术(LOW),但 IL2CPP 有符号溢出 UB 面(ADR-012 F7)要求乘加全部走无符号中间量 + 掩码提取;跨平台逐位一致由黄金夹具矩阵另批实测,本 story 的 EditMode 断言不借该绿。

**Control Manifest Rules (this layer)**:
- Required: `env_score = base_env + Σ e_env + adj_sum`(F-24-1,线性无 clamp);`equip_score = Σ_{tier>0} tier`,`clamp(score×65536, 0, EQUIP_MOD_CAP)` 在 int64 内一次完成(F-24-2);adj_sum 只查 +x/+y 两向(表对称性已由 story-001 烘焙门保证)
- Forbidden: **出 24 前对 env 侧任何 clamp / 饱和**(唯一 clamp 点在 21a F1 —— D-21-31);float/double 参与求值;`Math.Round`
- Guardrail: `K_context = max(K_speed)`(规则三)—— 违反 (0,1] 区间在表门拒(归 story-001),此处只做派生

---

## Acceptance Criteria

*From GDD `design/gdd/clinic-machine.md`, scoped to this story:*

- [ ] AC-24-01:同一布局下 EnvMod 输出 = 手工按 F-24-1 线性求值的期望值(逐位,定点 raw long)
- [ ] AC-24-01b:超界 env_score **原样透传**(24 侧不 clamp;与 21a 的 clamp 配对由 story-004 联测)
- [ ] AC-24-02:EquipMod 按 F-24-2 求值,tier=0 的家具不进和;结果 = `clamp(Σtier × 65536, 0, EQUIP_MOD_CAP)` 的定点值
- [ ] `EQUIP_MOD_CAP < 1` 的二值语义(2026-09-25 随拍记账)在夹具中可观测:score ≥ 阈值即钉顶,不产生 1 与 CAP 之间的中间值
- [ ] adj_sum 逐对格查 ADJ_TABLE,每对有界(`n_max` 内),O(1) 查表不重复计(只 +x/+y)
- [ ] 溢出安全:全部乘加在 int64 内;`n_max×TIER_MAX < 2^31` 前提下无环绕(fixture 取域边界值)

---

## Implementation Notes

*Derived from ADR-005/006 Implementation Guidelines:*

1. 三个纯函数:`AdjSum(room) → Fix`、`EnvModOf(room, context) → Fix`(UNCLAMPED)、`EquipModOf(room) → Fix`;输入 = story-002 的房间析出结果 + story-001 的 cooked 表。
2. `×65536` = Q16.16 的左移 16 位语义,写成移位常量并注明定点标度;EQUIP_MOD_CAP = 1/4 → `65536/4 = 16384` raw,不引入浮点。
3. clamp 仅 equip 侧一处(F-24-2 自带),env 侧零 clamp —— 代码注释互指 AC-24-01b / D-21-31,防止「顺手饱和」。
4. 舍入只经 ADR-006 单一模式;本求值链理论上无除法(纯加/移位),若实现出现除法 = 设计偏离,先停再问。
5. 逐值(base_env / 各 e_env / 各 tier 分布)归用户数值轮 —— 夹具用符号化小值验证机制,不测平衡。

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- [Story 001]: 表与 schema 门(区间/对称性违例在烘焙期已被拒,运行期不再防)
- [Story 002]: 房间析出
- [Story 004]: 交付 21a(那侧的唯一 clamp)与 9/1 的接口
- [Story 005]: 缓存与重放

---

## QA Test Cases

*Written at story creation (lean mode — QL-STORY-READY skipped; specs self-authored from AC text).*

- **AC-1(F-24-1 线性)**: 无 clamp 透传
  - Given: 手工布局使 Σ e_env + adj_sum 超 ENV_MAX(fixture 值)
  - When: EnvModOf
  - Then: 返回值 = 未饱和的线性求值(raw long 逐位等期望)
  - Edge cases: 全负和(低于 ENV_MIN)同样原样出;空房间 = base_env 单独出
- **AC-2(F-24-2 封顶)**: tier 和与 CAP
  - Given: Σtier 使 `score×65536 > 16384`
  - When: EquipModOf
  - Then: 钉 EQUIP_MOD_CAP;Σtier=0 ⇒ 0
  - Edge cases: 恰等 CAP 不截;含 tier=0 家具不进和
- **AC-3(adj 双向防重)**: +x/+y 单向遍历
  - Given: 一对相邻格
  - When: AdjSum
  - Then: 只计一次 w(a,b);全图扫描与手算期望一致
  - Edge cases: 同格自邻不存在;跨房间对不计入

---

> ### 🔁 QA plan 回填(2026-10-21 · `production/qa/qa-plan-sprint-17-2026-10-21.md`)
>
> 上列规格**原文保留**;只补增量。**估计测试数 ~14**。
>
> | # | 新增判据 | 依据 |
> |---|---|---|
> | 1 | **GDD 算例直接作黄金夹具**(逐位 raw long):药房 4 格 —— 药柜 `+12000` / 研磨台 `+8000` / 烘干架 `+6000` / 便器 `−20000`;邻接 研磨台↔烘干架 `+4000`、药柜↔便器 `−12000`;`base = 30000` ⇒ **`env_score = 28000`**。上列 AC-1 用的是自造 fixture,此条用**权威算例**,判别力更强 | `AC-24-01` · F-24-1 算例 |
> | 2 | **`CAP < 1` 的二值语义可观测**:`score ≥ 阈值` 即钉顶,**不产生 1 与 `EQUIP_MOD_CAP` 之间的中间值**。上列 AC-2 只测「钉顶 / 恰等不截」,**未测**「无中间值」这半个(承 2026-09-25 随拍记账) | 卡面 AC 第 4 条 |
> | 3 | **溢出安全 fixture 取域边界**:`n = 64`(`OQ-24-5` 已裁 `n_max = 64`)+ `TIER_MAX` 满档 ⇒ 全部乘加在 **int64** 内无环绕。⚠️ `n_max × TIER_MAX < 2^31` 这条**schema 断言归 `clinic-001`**,本件不重复测,只测「int64 求积后 clamp」这一实现要求 | 卡面 AC 第 6 条 · F-24-2 运算域注 |
> | 4 | **`ROOM_NONE` ⇒ 输出中性 `(0, 0)`**(EC-24-01/02/07);上列未覆盖该基例 | GDD EC-24-01/02/07 |
> | 5 | **`adj_sum` 边界**:`n = 1` ⇒ `adj_sum = 0`(无邻居);上列 AC-3 只测了一对相邻格 | F-24 公共定义 |
>
> ⚠️ **命名撞车(实施前必读)**:`unity/Assets/Sim/EnvMod.cs` 是**系统 5** 的 `EnvMod_raw`(94 行纯函数,`Tests/EditMode/TimeWeather/envmod_raw_test.cs` 在册)。
> 系统 24 的 **F-24-1 也叫 EnvMod** ⇒ 本件落点须用限定名(如 `ClinicEnvModEvaluator`),
> **测试命名同样要限定**,否则 `grep -rn 'EnvMod'` 类判据会串到系统 5 上去。
>
> ⚠️ **表对称性不在本件测试面**:`∀a,b: w(a,b)==w(b,a)` 由**烘焙期 schema 断言**(`throw`)保证,归 `clinic-001`;
> 本件只测「24 忠实使用表值」。

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_modifier_eval_test.cs` — must exist and pass
**Status**: [x] Created & passing(2026-10-21 · 18 测)
**Additional carriers**(同一 AC 允许多条载体,承 `.claude/docs/review-workflow.md` 义务 4):
- `unity/Assets/Tests/EditMode/ClinicMachine/clinic_machine_cooked_codec_test.cs`(18 测 —— 三表解码 / 盘上↔现烘 / provider 三支路 / 只读视图不可强转)
- `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`(11 测 —— 三 address 四处闭合)
- `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs`(23 测 —— 三份新产物进漂移门登记表)

### 反证记录(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 阈值边界(零 clamp 铁律) | `envMod_aboveEnvModMax` | 在 `ClinicModifierEvaluator.cs:116` 加钳制 ⇒ 2 条红 |
| 定义域收窄(ROOM_NONE 中性) | `roomNone_returnsNeutralZero` | `:104` 改 `return baseEnv` ⇒ 2 条红 |
| 一次性/幂等(每条邻接边恰一次) | `adjSum_adjacentPair_countedOnce` | `:242` 加 −x 反向邻居 ⇒ 2 条红(含 GDD 算例 28000→32000) |
| 阈值边界(EQUIP_MOD_CAP 单钳制) | `equipMod_sumAboveCap_pinsToCap` | `:158` 停用上钳 ⇒ 4 条红 |
| 定义域收窄(非家具格不计分) | `adjSum_nonFurnitureCellExcluded` | `:189` 把 `NonFurniture` 塞进 `byCell` ⇒ 1 条红 |
| 形态断言(只读视图不可强转) | `contextTable_rows_notCastableToArray` / `roomTable_rooms_notCastableToArray` | `ClinicMachineTables.cs:98/:362` 退回裸数组 ⇒ 各恰 1 条红 |

三份 spec:`tools/falsify/specs/s17-2-clinic-modifier-eval.json`(C1–C5)· `s17-2-clinic-wiring.json`(W1/W2)· `s17-2-clinic-readonly.json`(R1/R2)。
全部 `exactRed: true`,逐条实测恰如预期;还原 SHA-256 逐字节校验通过。

**⚠️ 未达 `Complete` 的硬前置**:`FURN_TYPES` 闭集(`O-24-1` / `TD-clinic-01`,sprint-18 G-8)未兑现 ⇒
`ClinicModifierEvaluator.FurnTraitsProductionReady => false`,求值器在生产链路上**无输入源**(只有测试注入的夹具源)。
证据原件:`production/qa/evidence/impl-s17-2-clinic-modifier-eval-2026-10-21.md`。

---

## Dependencies

- Depends on: Story 001(cooked 表)、Story 002(房间)
- Unlocks: Story 004(交付边界)、Story 005(重放对拍)

---

## Completion Notes

*(留空 — story 关闭时回填)*
