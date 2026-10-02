# Story 003: 乘子求值 F-24-1/2/3(EnvMod / EquipMod / adj_sum)

> **Epic**: 医馆即机器
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-02
> **Last Updated**: 2026-09-28
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

## Test Evidence

**Story Type**: Logic
**Required evidence**: `unity/Assets/Tests/EditMode/ClinicMachine/clinic_modifier_eval_test.cs` — must exist and pass
**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001(cooked 表)、Story 002(房间)
- Unlocks: Story 004(交付边界)、Story 005(重放对拍)

---

## Completion Notes

*(留空 — story 关闭时回填)*
