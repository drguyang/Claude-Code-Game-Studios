# Story 001: FixParse 与整数边界契约

> **Epic**: 物品与配方数据库
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3h
> **Manifest Version**: 2026-09-21
> **Last Updated**: 2026-09-23

## Context

**GDD**: `design/gdd/item-database.md`
**Requirement**: TR-itemdb-001(物品数值在整数定点域 Q16.16)· TR-itemdb-002(weight / stack_max 为 int,移出 Fix 解析集)· TR-itemdb-005(FixParse 唯一入口,拒浮点字面量)· TR-itemdb-006(ROUND_HALF_AWAY_FROM_ZERO)· TR-itemdb-028(axis_offset_by_quality[] 可为负 ⇒ 负数舍入方向须定义)
*(Requirement texts live in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-006(主): 定点域边界数据契约 · ADR-005(次): 确定性模拟与状态同步模型
**ADR Decision Summary**: 外部数据 → `Fix` 的转换只经 `FixParse`(`Parse(string, RoundMode)` / `FromRatio(long,long)`),不提供 `Parse(float)` / `implicit operator Fix(float)`;浮点字面量在 schema 层即被拒;`weight` / `stack_max` 是 `int` 计数不是 `Fix`(D-21-17);全部舍入用 `ROUND_HALF_AWAY_FROM_ZERO` 且在整数域内完成,`RoundMode` 是全局常量非逐调用点参数;ADR-005 规定全部模拟数学在 int64 Q16.16 定点域、中间乘法落 Q32.32 再移位回。

**Engine**: Unity 6.3 LTS (6000.3.24f1) | **Risk**: HIGH(取 ADR-005 并列最高)
**Engine Notes**: ADR-005 自陈 Engine Knowledge Risk HIGH —— IL2CPP 逐位性需实测,但本裁决不依赖该实测(刻意不用 post-cutoff API)。ADR-006 MEDIUM(不依赖 post-cutoff API)。

**Control Manifest Rules (this layer)**:
- Required: 外部数据 → `Fix` 只经 `FixParse`;数据文件字段写整数字面量或 `分子/分母`,导入期一次性转 Q16.16;全部舍入 `ROUND_HALF_AWAY_FROM_ZERO` 整数域内完成
- Forbidden: `Parse(float)` / `implicit operator Fix(float)`;运行期 float→Fix 路径(编译期不可表达);`weight` / `stack_max` 走 FixParse
- Guardrail: 一切模拟数学住整数定点域;边界唯一解析入口 = `FixParse`(manifest 交叉约束 3)

---

## Acceptance Criteria

*From GDD `design/gdd/item-database.md`, scoped to this story:*

- [ ] **AC-21a-41**: 任一含 drug_potency / half_life(/onset/peak/elimination)/ axis_offset_by_quality[] 的数据文件,解析读入类型为 Fix(Q16.16 int64),不接受浮点字面量;weight 是 int、不在 Fix 解析集(D-21-17)
- [ ] **AC-21a-42**: ROUND_HALF_AWAY_FROM_ZERO 边界:Round(0.5)/Round(−0.5)/Round(1.5)/Round(2.5) = 1 / −1 / 2 / 3 —— 显式切断 Math.Round 的 ties-to-even
- [ ] **AC-21a-51**: 任一 axis_offset_by_quality[] 内出现浮点字面量 ⇒ 导入期硬失败(FixParse 拒绝;D-21-14/D-21-19 落盘)
- [ ] **AC-21a-57**: 任一 drug_potency / half_life / onset / peak / elimination 写浮点字面量 ⇒ 导入期硬失败(FixParse 拒绝;与 AC-21a-51 同族,补原稿漏掉的时间轴四字段与药效幅值)

---

## Implementation Notes

*Derived from ADR-006 §Decision (primary):*

- 外部数据 → `Fix` 的转换**只经 `FixParse`**(`Parse(string, RoundMode)` / `FromRatio(long,long)`);**不提供** `Parse(float)` / `implicit operator Fix(float)` —— ADR-006 §Decision 一
- 数据文件里 `Offset` / `τ_half` / `axis_offset_by_quality[]` 写**整数字面量或 `分子/分母`**,**导入期**一次性转 Q16.16 —— ADR-006 §Decision 一
- 浮点字面量**在 schema 层即被拒**;运行期不存在 float→Fix 路径(编译期不可表达)—— ADR-006 §Decision 一
- `weight` / `stack_max` **是 `int` 计数,不是 `Fix`**(D-21-17)—— ADR-006 §Decision 一
- 全部舍入用 `ROUND_HALF_AWAY_FROM_ZERO`,**在整数域内完成**;`RoundMode` 是**全局常量**,非逐调用点参数 —— ADR-006 §Decision 三
- 128 位中间结果唯一类型 = 手工 hi/lo 两 `ulong` 带进位;**禁 `System.Int128`**、**禁 `BigInteger`**、无条件钉 hi/lo 不留条件分支 —— ADR-005 Amendment G(实现期若触到 Q32.32 中间乘,同批遵守)
- 单一舍入模式禁 `Math.Round` 默认 ties-to-even(manifest 交叉约束 3,与 AC-21a-30 静态扫描交叉覆盖)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: ItemDef / Recipe / ItemInstance 等 schema 类型定稿与复合主键
- Story 008: 作者态 JSON 两阶段烘焙管线与 Addressables 预载(本故事只测 FixParse 边界,不搭管线)
- Story 011: F1–F5 全部中间变量 float/double 静态扫描(AC-30)与黄金哈希(AC-28/29)

---

## QA Test Cases

*Written by qa-lead at story creation. The developer implements against these — do not invent new test cases during implementation.*

- **AC-21a-41**: 任一含 drug_potency / half_life(/onset/peak/elimination)/ axis_offset_by_quality[] 的数据文件,解析读入类型为 Fix(Q16.16 int64),不接受浮点字面量;weight 是 int、不在 Fix 解析集(D-21-17)。
  - Given: 作者态 JSON 含上述 Fix 字段,值以合法字符串形式书写(整数串 / 分数串,承 ADR-014「Fix 字段在 JSON 里写字符串」);同文件含 `weight` 字段。
  - When: 经 FixParse 解析。
  - Then: Fix 字段得 Q16.16 Fix 值;`weight` / `stack_max` 按 int 计数解析(不走 FixParse);任一 Fix 字段写 JSON 数字/浮点字面量 ⇒ 导入期硬失败。
  - Edge cases: 空串;分母为 0 的分数串("n/0");负 Fix;weight 写成 "3/4" 或 "1.5"(应拒 —— int 字段不接受 Fix 语法);字段缺失。
  - Negative fixture: AC 表未命名 —— 浮点拒收与 AC-21a-57 共用 `invalid_potency_float.json` 同族;weight 越界解析用同文件内变体记录。

- **AC-21a-42**: ROUND_HALF_AWAY_FROM_ZERO 边界:Round(0.5)/Round(−0.5)/Round(1.5)/Round(2.5) = 1 / −1 / 2 / 3 —— 显式切断 Math.Round 的 ties-to-even。
  - Given: 定点域输入 x ∈ {0.5, −0.5, 1.5, 2.5}(以 Fix raw 表示)。
  - When: 调用统一舍入函数。
  - Then: 逐位得 1 / −1 / 2 / 3;实现不得调用 Math.Round 默认重载(由 AC-21a-30 静态扫描交叉覆盖)。
  - Edge cases: 其它 .5 平局(±3.5、±0.5×奇数)同向远离零;纯整数输入恒等;负值整数不变;Q16.16 半单位(32768)恰为平局。
  - Negative fixture: 无(纯函数断言,代码构造)。

- **AC-21a-51**: 任一 axis_offset_by_quality[] 内出现浮点字面量 ⇒ 导入期硬失败(FixParse 拒绝;D-21-14/D-21-19 落盘)。
  - Given: drug_profile.axis_offset_by_quality[] 中任一元素写浮点字面量(如 JSON 数字或浮点字符串)。
  - When: 走导入/烘焙阶段 2 解析。
  - Then: 硬失败(显式 throw,构建红);同数组全部为合法 Fix 字符串(可为负)则通过。
  - Edge cases: 仅一档非法、全档非法;负偏移字符串合法;科学计数法字面量(拒)。
  - Negative fixture: AC 表未命名;随 AC-21a-57 同族夹具覆盖,不新增文件名。

- **AC-21a-57**: 任一 drug_potency / half_life / onset / peak / elimination 写浮点字面量 ⇒ 导入期硬失败(FixParse 拒绝;与 AC-21a-51 同族,补原稿漏掉的时间轴四字段与药效幅值)。
  - Given: drug_profile 五字段中任一写浮点字面量。
  - When: 导入解析。
  - Then: 硬失败;全部为合法 Fix 字符串(含负值)则通过。
  - Edge cases: 逐字段轮换(五字段各触发一次);字段为 P0 空(null 合法,D-21-6)时不触发;仅 drug_potency 非法。
  - Negative fixture: `invalid_potency_float.json`

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/item_database/fix_parse_boundary_test.cs` — must exist and pass

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None
- Unlocks: Story 002(schema 类型定稿需要已就位的 FixParse),Story 011(静态扫描与黄金夹具复用本故事的舍入实现)
