# Story 003: 乘子求值 F-24-1/2/3(EnvMod / EquipMod / adj_sum)

> **Epic**: 医馆即机器
> **Status**: In Review
> **Layer**: Core
> **Type**: Logic
> **Estimate**: 6h
> **Manifest Version**: 2026-10-15  <!-- 原 `2026-10-02` —— 2026-10-21 逐件判定：manifest 10-02→10-15 的增量「仅之七/之八两节」(`control-manifest.md:6-9`)，本件两条皆未命中，见头部 §Manifest 判定注。原文保留 -->
> **Last Updated**: 2026-10-21
>
> 🔁 **2026-10-21 `Manifest Version` 逐件判定(sprint-17 §偏离登记 ② 要求,非预改)**:
> `control-manifest.md:6-9` 自陈 10-02 → 10-15 的**唯一差异 = 新增之七(处方前置检查)与之八
> (判据不得用文化敏感字符串比较)**,「前六条与其余各节一字未改」。本件逐条核过,两条均**未命中实质要求**:
> ① **之七**(`Sim` 引用集可达性):本件生产件 `ClinicModifierEvaluator.cs` / `ClinicMachineTables.cs`
> **零** `PayloadCodec` / `Sim.Codec` / `PayloadRef` 引用(`grep` 实测空集);求值器是纯 BCL 整数算术,
> 不触任何跨程序集符号 ⇒ 之七无适用面。
> ② **之八**(判定谓词禁 culture-sensitive 比较):本件唯一字符串谓词 =
> `ClinicMachineTables.cs:263` 的 `string.Equals(p.A, p.B, StringComparison.Ordinal)` —— **已显式 Ordinal**,
> `ClinicModifierEvaluator.cs` 零字符串判定 ⇒ 合规,无待改项。
> ⇒ 按 Phase 2 `[A]` 分支改字段为 `2026-10-15`;**不记 `Manifest-Note`**(那是 `[B]` 分支的形态)。
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

- [x] AC-24-01:同一布局下 EnvMod 输出 = 手工按 F-24-1 线性求值的期望值(逐位,定点 raw long)
  <br>载体:`test_clinic_envMod_gddWorkedExample_matches28000`(GDD 权威算例,`env_score = 28000` raw)· 反证 C3
  <br>🔁 **2026-10-21 评审补记 —— 本卡面 AC 转写时漏了 GDD 的组成部分**:GDD `:370` 原文在「产出未钳制
  `EnvMod_clinic`」之后还有一句 **「**性质测试**:对任意 `MIN < MAX` 与任意输入,24 的产出 = 线性加权和,
  **不含 clamp**」**;AC-24-02 同(GDD `:372`「**性质测试**,同 AC-24-01」)。**本卡面零处提及「性质测试」**
  (`grep -c` = 0),测试件也**零参数化**(无 `TestCase`/`Values`/`Random`),只有 3 条定点例子。
  ⇒ 该句要求的是**全称命题**,定点例子在逻辑上只能证伪、不能证明。
  **本勾选超前于证据** —— 处置待用户裁定(见 §Completion Notes「评审结论」)。
  原文保留不删,补记于此。
  <br>✅ **2026-10-21 处置:用户裁定「选 C,补性质测试」** —— 性质面已补,勾选**不再超前**:
  · `test_clinic_envMod_property_anyInput_equalsLinearSum_noClamp` —— **512 组**固定种子输入(量级 ±400000
    raw,远超 `ENV_MOD_MAX`),逐组**独立手算** `base + Σe_env + adj` 再逐位比对;末段采样守卫要求
    **> 400/512 越域**(实测 476),否则本测对 clamp 无判别力。
  · `test_clinic_envMod_property_anyMinMax_outputStillUnclamped` —— `[TestCase]` 4 组 `MIN < MAX`
    (−1/1 · −1000/1000 · ∓32768 · −1e6/1e6),断言产出 = 未钳制线性和**且严格越过该组 MAX**。
  · **反证 M1(✅ 已跑)**:在 `ClinicModifierEvaluator.cs:121` 前加 `±32768` 钳制 ⇒ **实测 7 条红**
    (与预写的 7 一致)。⚠️ **首轮只红 5 条** —— 差的正是 `anyMinMax` 的 `(-1,1)` 与 `(-1000,1000)`:
    那两组样本和只有 2 / 2000,落在冻结界内 ⇒ 对「钳到 ±32768」**不可观测** = 我写的测试**假绿**。
    修法 = 加 `MinMaxSlackRaw` 使四组和全部越界;复跑 7 红。
- [x] AC-24-01b:超界 env_score **原样透传**(24 侧不 clamp;与 21a 的 clamp 配对由 story-004 联测)
  <br>载体:`test_clinic_envMod_aboveEnvModMax_passesThroughUnclamped` / `..._allNegative_...` · 反证 C1(在 `:116` 加钳制 ⇒ 两条同红)
- [x] AC-24-02:EquipMod 按 F-24-2 求值,tier=0 的家具不进和;结果 = `clamp(Σtier × 65536, 0, EQUIP_MOD_CAP)` 的定点值
  <br>载体:`test_clinic_equipMod_sumAboveCap_pinsToCap` / `test_clinic_equipMod_allZeroTier_returnsZero` · 反证 C4
  <br>✅ **2026-10-21 处置(用户裁定「选 C,补性质测试」)** —— GDD `:372` 的「`EquipMod ∈ [0, EQUIP_MOD_CAP]`
  **恒成立**(**性质测试**,同 AC-24-01)」已补:
  · `test_clinic_equipMod_property_anyTierVector_inClosedRange` —— **256 组**固定种子档位向量
    (格数 1–8、`tier ∈ [0, TIER_MAX]`),逐组独立手算 `clamp(Σtier × 65536, 0, CAP)` 并**同时**断言
    闭区间不变量;末段采样守卫要求 **> 200/256 钉顶**(实测 247)。
  · `test_clinic_equipMod_negativeTier_excludedFromScore` —— 补 Q8(`tier < 0` 分支零判据):
    `tier ≤ 0` 若被改成 `tier == 0`,负档会**减分**并逃出上钳。
  · **反证 M2 / M3(✅ 已跑)**:M2 = 停用上钳(`:163`)⇒ **实测 6 条红**(与预写一致);
    M3 = `tier <= 0` → `tier == 0` ⇒ **实测 1 条红**(与预写一致)。
    三轮还原 `RESTORED_OK sha256=af9a8f51…d11ea47` · `FINAL_OK` 同值 · `FALSIFY_residue=0`。
- [x] `EQUIP_MOD_CAP < 1` 的二值语义(2026-09-25 随拍记账)在夹具中可观测:score ≥ 阈值即钉顶,不产生 1 与 CAP 之间的中间值
  <br>载体:`test_clinic_equipMod_positiveScores_haveNoIntermediateValue` · 反证 C4
- [x] adj_sum 逐对格查 ADJ_TABLE,每对有界(`n_max` 内),O(1) 查表不重复计(只 +x/+y)
  <br>载体:`test_clinic_adjSum_adjacentPair_countedOnce` / `..._missingPair_isZero` / `..._singleCell_isZero` · 反证 C3(−x 反向 ⇒ 重复计)
- [x] 溢出安全:**每处乘法/加法均经显式域守卫**,越界即 throw(不依赖「域内值不会溢出」这一前提)
  <br>载体:`test_clinic_equipMod_largeScore_int64NoWrap` / `test_clinic_equipMod_nMaxBoundary_noWrap`
  <br>⚠️ **证词收窄(承证据件 §六 3)**:两条夹具的 `tier`(100000 / 1000)**远超冻结的 `TIER_MAX=3`**;
  域内最大档位和 `64 × 3 = 192` ⇒ `192 × 65536 = 12 582 912` 在 int32 内装得下 ⇒ 域内 int32/int64
  两实现在此夹具上**不可区分**。故本条证的是「乘加在 int64 内」这一**实现要求**,
  **不是**「域内值会溢出」—— 后者为假,不主张。域界本身由 story-001 的 schema 断言守。
  <br>🔁 **2026-10-21 评审 D1 处置 —— 本 AC 的措辞已改写**(原文见下):原写
  「`n_max×TIER_MAX < 2^31` 前提下无环绕(fixture 取域边界值)」,而 D1 指出:那两条夹具
  **根本不触达守卫**(`100000 ≪ long.MaxValue>>16 ≈ 1.4×10¹⁴`)⇒ 实测方向是 **clamp 钉顶**,
  与 `equipMod_sumAboveCap_pinsToCap` 判别面重叠。更关键的是「域内 int32/int64 逐位不可区分」
   ⇒ **原措辞的命题在域内不可证伪**。
  **改法(取评审建议的第二条)**:把 AC 主语从「结果不溢出」(对域内恒真、不可证伪)
  改成「**每处乘加均经显式域守卫**」(对实现可证伪 —— 删掉 `CheckedAdd` / 上界预判即红)。
  这样 AC 描述的是**代码结构**,而不是一个在域内无法被推翻的数值命题。
  <br>原文保留作沿革:
  > 溢出安全:全部乘加在 int64 内;`n_max×TIER_MAX < 2^31` 前提下无环绕(fixture 取域边界值)
  <br>⚠️ **仍未证的一步(禁借绿)**:上面的改写**没有**新增「直接测 `CheckedAdd` 本体」的夹具 ——
  评审建议的第一条路(测守卫本体而非经求值器)未做。当前 `CheckedAdd` 的越界分支
  **无独立载体**,只有经求值器的两条越域夹具(它们打到的是 clamp 而非守卫)。登记为剩余缺口。

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
**Status**: [x] Created & passing(2026-10-21 · **25 测**)
  <br>全量回归(性质测试补写轮)· 三元组 `2026-10-21 10:55 CST` · `性质测试补写轮` · `15fcda8` + 未提交改动:
  `unity/Logs/s17-2-property-full.xml` = **`4272 / 4236 / 0 / 36`**;对照 `S17-4` 基线 `4265 / 4229 / 0 / 36`(`10:17`)
  ⇒ `fullname` 集合差 **新增 7 · 消失 0**,7 条全为本轮性质测 ⇒ **零回归**。
  <br>🔁 **18 → 25(2026-10-21 性质测试补写轮)**:新增 7 个用例位 =
  `envMod_property_anyInput_equalsLinearSum_noClamp`(1) + `envMod_property_anyMinMax_outputStillUnclamped`(4 个 `[TestCase]`) +
  `equipMod_property_anyTierVector_inClosedRange`(1) + `equipMod_negativeTier_excludedFromScore`(1)。
  原文 18 为补写前实测值,**保留作沿革**。
**Additional carriers**(同一 AC 允许多条载体,承 `.claude/docs/review-workflow.md` 义务 4):
- `unity/Assets/Tests/EditMode/ClinicMachine/clinic_machine_cooked_codec_test.cs`(18 测 —— 三表解码 / 盘上↔现烘 / provider 三支路 / 只读视图不可强转)
- `unity/Assets/Tests/EditMode/DataPipeline/provider_wiring_gate_test.cs`(11 测 —— 三 address 四处闭合)
- `unity/Assets/Tests/EditMode/DataPipeline/cooked_drift_all_artifacts_test.cs`(23 测 —— 三份新产物进漂移门登记表)
  <br>🔁 **2026-10-21 订正**:上述 23 = **S17-2 当时**的值(19 份登记产物 + 4 个 `[Test]` +
  `TestCaseSource` 展开)。S17-4 追加第 20 份产物后现为 **24**。原文保留作沿革。

> **Q6 处置(2026-10-21 评审 · 登记为 Integration)** —— 上面第一条载体里,
> **三条 `*_diskMatchesFreshBake` 测的性质是 Integration,不是 Logic**:
> `test_clinic_cooked{Context,Adj,Room}_diskMatchesFreshBake`(`:62` / `:89` / `:117`)
> 走 `ReadCommitted()`(`File.ReadAllBytes` 读**入库的 `unity/Assets/DataCooked/*.cooked.bytes`**)
> 与 `FreshBake()`(`BakeFromRepo(RepoRoot)` 从**源 JSON 现烘**)对拍。
> 两个真实来源互证 ⇒ 按 `.claude/docs/coding-standards.md` §Testing Standards 的
> 「Integration(multi-system)」归口。**本 story 的 `Story Type: Logic` 不受影响**
> (主体载体 `clinic_modifier_eval_test.cs` 是纯函数测,零 I/O);
> 此处登记的是**载体清单里那三条的类别**,以防下游把它们当纯单元测计数。
> 该文件其余 15 测为纯 Logic(字节解码 / 只读视图),不在此列。

### 反证记录(S12-4 Falsifiability Gate)

| 命中形态 | 主要断言 | 反证:停用什么 ⇒ 哪条转红 |
|---|---|---|
| 阈值边界(零 clamp 铁律) | `envMod_aboveEnvModMax` | 在 `ClinicModifierEvaluator.cs:116` 加钳制 ⇒ 2 条红 |
| 定义域收窄(ROOM_NONE 中性) | `roomNone_returnsNeutralZero` | `:104` 改 `return baseEnv` ⇒ 2 条红 |
| 一次性/幂等(每条邻接边恰一次) | `adjSum_adjacentPair_countedOnce` | `:242` 加 −x 反向邻居 ⇒ 2 条红(含 GDD 算例 28000→32000) |
| 阈值边界(EQUIP_MOD_CAP 单钳制) | `equipMod_sumAboveCap_pinsToCap` | `:158` 停用上钳 ⇒ 4 条红 |
| 定义域收窄(非家具格不计分) | `adjSum_nonFurnitureCellExcluded` | `:189` 把 `NonFurniture` 塞进 `byCell` ⇒ 1 条红 |
| 形态断言(只读视图不可强转) | `contextTable_rows_notCastableToArray` / `roomTable_rooms_notCastableToArray` | `ClinicMachineTables.cs:98/:362` 退回裸数组 ⇒ 各恰 1 条红 |
| **性质 / 全称**(零 clamp,任意输入) | **新增** `envMod_property_anyInput_equalsLinearSum_noClamp` + `envMod_property_anyMinMax_outputStillUnclamped` ×4 | **M1**:`:121` 前加 `±32768` 钳制 ⇒ **7 条红**(四组 anyMinMax 全红) |
| **性质 / 全称**(EquipMod ∈ [0, CAP]) | **新增** `equipMod_property_anyTierVector_inClosedRange` | **M2**:`:163` 停用上钳 ⇒ **6 条红** |
| 定义域收窄(`tier < 0` 不进和) | **新增** `equipMod_negativeTier_excludedFromScore` | **M3**:`:150` 的 `tier <= 0` → `tier == 0` ⇒ **恰 1 条红** |

三份 spec:`tools/falsify/specs/s17-2-clinic-modifier-eval.json`(C1–C5)· `s17-2-clinic-wiring.json`(W1/W2)· `s17-2-clinic-readonly.json`(R1/R2)。
全部 `exactRed: true`,逐条实测恰如预期;还原 SHA-256 逐字节校验通过。

> **✅ D8 修复后的复跑(2026-10-21 第二批)**:锚点 10 条过期全部订正(全库 17/17 绿),
> 用**驱动**复跑 eval(C1–C5)+ readonly(R1–R2)= **7 个变异,全部恰如预期**
> (C1 7 · C2 2 · C3 4 · C4 7 · C5 1 · R1 1 · R2 1);eval 那轮末行逐字
> `══ 证伪轮通过:5 个变异全部恰如预期 ══`、`EXIT_DRIVER=0`。
> ⚠️ **W1/W2 本轮未跑**(锚点已核,判别力证据仍是 2026-10-21 首轮实测的 `2 / 1`),
> 详见证据件 §4.5。

> 🔁 **2026-10-21 性质测试补写轮另起独立脚本** `tools/falsify/s17-2-property-round.sh`(M1/M2/M3)——
> 不复用上面三份 spec 的原因:它们的锚点当时已 6/13 过期(评审 D8,驱动 exit 2)。
> **预期先写死再跑**;三轮 `RESTORED_OK sha256=af9a8f51…` · `FINAL_OK` 同值 · `FALSIFY_residue=0`。
> ⚠️ **首轮 M1 实测只红 5/7**,抓出我自己写的 `anyMinMax` 窄域两组是**假绿**(样本和 2 / 2000 落在
> 冻结界内,对「钳到 ±32768」不可观测)⇒ 加 `MinMaxSlackRaw = 100000` 第四格后复跑 **7 红**。
> 全过程见 `production/qa/evidence/impl-s17-2-clinic-modifier-eval-2026-10-21.md` §4.4。

~~**⚠️ 未达 `Complete` 的硬前置**:`FURN_TYPES` 闭集(`O-24-1` / `TD-clinic-01`,sprint-18 G-8)未兑现 ⇒
`ClinicModifierEvaluator.FurnTraitsProductionReady => false`,求值器在生产链路上**无输入源**(只有测试注入的夹具源)。~~
证据原件:`production/qa/evidence/impl-s17-2-clinic-modifier-eval-2026-10-21.md`。

> **✅ 2026-10-21 硬前置已解除(S17-4)** —— `O-24-1` 已兑现(`TD-clinic-01` 结案):
> 生产属性源 = `Sim/Clinic/ClinicFurnitureTraitsSource.cs`,哨兵翻 **true**。
> 求值器从此在生产链路上**有输入源**。证据原件
> `production/qa/evidence/impl-s17-4-furniture-catalogue-2026-10-21.md`。
> ⚠️ **本故事的 `Status` 不因本条自动转 `Complete`** —— 那是独立判定;
> 本条只解除「无输入源」这一项前置。

---

## Dependencies

- Depends on: Story 001(cooked 表)、Story 002(房间)
- Unlocks: Story 004(交付边界)、Story 005(重放对拍)

---

## Completion Notes

**Completed**: — (**未置 `Complete`**;本件置 **`In Review`**,理由见下)
**Criteria**: **6/6 已勾**(逐条载体见 §Acceptance Criteria;全部载体名已 `grep` 逐条核实存在)

**实现轮**:2026-10-21(批次 = sprint-17 批 2 / S17-2)
**证据原件**:`production/qa/evidence/impl-s17-2-clinic-modifier-eval-2026-10-21.md`

### 交付面

| 面 | 落点 |
|---|---|
| 求值器 | `Sim/Clinic/ClinicModifierEvaluator.cs`(`ClinicAdjSum` / `ClinicEnvModOf` 零 clamp / `ClinicEquipModOf` 单 clamp) |
| 输入契约 | `Sim/Clinic/IClinicFurnitureTraitsSource.cs`(三值 `ClinicCellLookup`,禁二值 bool) |
| 表 | `Sim/Clinic/ClinicMachineTables.cs`(CONTEXT / ADJ / ROOM 三表 + 只读视图) |
| 解码 | `Gameplay.Presentation/ClinicMachineCookedCodec.cs` |
| 接线 | `AddressablesDataProvider` 三 address / 三 `Load<T>` / 三 `Ensure*` / `Preload()` · `DataCorePreloader` 16→19 · `data-core` 组 19 条 |
| 测试 | `clinic_modifier_eval_test.cs` **25 测**(🔁 原 18,2026-10-21 性质测试补写 +7)· `clinic_machine_cooked_codec_test.cs` **18 测** · `provider_wiring_gate_test.cs` **11 测** |

### 证伪轮(S12-4 · 9 变异全 `exactRed`)

三份 spec:`s17-2-clinic-modifier-eval.json`(C1–C5)· `s17-2-clinic-wiring.json`(W1/W2)·
`s17-2-clinic-readonly.json`(R1/R2)。逐条实测恰如预期(C1 红 2 · C2 红 2 · C3 红 2 · C4 红 4 ·
C5 红 1 · W1 红 2 · W2 红 1 · R1 红 1 · R2 红 1);还原 SHA-256 逐字节校验通过。
⚠️ 该轮只传了 `--guard unity/Assets/DataCooked`,`AddressableAssetsData` 未受保护
(驱动只读命令行 `--guard`,不读 spec 的 `guards` 字段)—— 五个变异均不触及该路径,但**证书面只覆盖一个目录**。

### 回归(带三元组)

`4237 / 4201 / 0 / 36` · `2026-10-21 09:02 CST` · `sprint-17 批 2 S17-2 收尾轮` · `3a22dbf` + 工作树;
对 `4198 / 4162 / 0 / 36 @ 08:02 · 1dde232` 做 `fullname` 集合差 = **Δ +39 / 消失 0** ⇒ **零回归**。
⚠️ **本轮全量首跑抓到两条真回归**(非夹具问题):`cooked_drift_all_artifacts_test` 的
`pinnedDeclared` 16 ≠ 19、幽灵产物点名三份 —— 三表已落盘但未进漂移门登记表。
修法 = 加 `BakedArtifacts` 三条 + `pinnedDeclared` 19。**这是「补烘 ≠ 登记 ≠ 接线」三件事的第一次现身。**

### 为何置 `In Review` 而非 `Complete`(用户 2026-10-21 裁定)

6 条 AC 在证据件 §二 均为 Landed ✅ / Proven ✅ 双列,且载体名逐条核实。
**但证据件 §五 仍挂 4 项实质性 NOT-RUN**,且其中两项不是「环境未就绪」而是**面本身未做**:

| # | 未跑项 | 性质 |
|---|---|---|
| 1 | ~~`FURN_TYPES` 闭集未兑现~~ | ✅ **2026-10-21 稍后已解除**(S17-4) —— 生产属性源已落地、哨兵翻 true |
| 2 | 「布局 → 房间类型」匹配(`ROOM_TABLE` 求值)**未实现** —— `base_env` 由调用方解析后传入,求值器不假装房间匹配已存在 | **面未做**(非环境问题) |
| 3 | IL2CPP 三格 / 跨平台逐位(ADR-012 矩阵) | **环境未就绪**(本机仅 Mono) |
| 4 | 数值(`base_env` / 各 `e_env` / 各 `tier` 分布)未定稿 | 归用户数值轮(承「数值用户自己调」) |
| 5 | 「`env_score` 与 21a 的 clamp 配对」未跑 | **归 story-004 联测** |

⇒ 置 `In Review`:实现与 AC 判据面已完整且已证,**但第 2 项是求值器上游的实质缺口**
(「房间匹配」整段不存在,`base_env` 靠调用方喂),不宜在它缺席时记 `Complete`。

### 未跑项(禁借绿,如实登记)

- **IL2CPP 三格**:本机仅 Mono,ADR-012 矩阵未跑。
- **~~无 `/code-review` 原件~~** ⇒ ✅ **2026-10-21 已补**:
  `production/qa/evidence/review-s17-2-clinic003-2026-10-21.md`(两路并行,合并裁定 **CHANGES REQUIRED · 1 BLOCKING**)。
  见下方「评审结论」节。
- **story-004 联测**(21a clamp 配对)未跑。
- **数值未定稿**。

### 评审结论(2026-10-21 · `review-s17-2-clinic003-2026-10-21.md`)

| 路 | Verdict | BLOCKING |
|---|---|---|
| A · `unity-specialist`(代码本体) | **APPROVED WITH SUGGESTIONS** | 0(零 `ARCHITECTURAL VIOLATION`) |
| B · `qa-tester`(可测性 / 假绿) | **GAPS** | 1 |
| 合并 | **CHANGES REQUIRED** | **1** |

**代码本体质量**:14 条硬约束 13 CLEAN + 1 N/A —— 门 A / 零浮点 / Sim 引用集 / 零除法 /
clamp 唯一性 / 之八 Ordinal / 只读视图 / F7 三处守卫 全部通过。**9 个方法全部 ≤33 行、CC ≤7。**

**⚠️ 唯一的 BLOCKING(Q7)—— 卡面转写丢了 GDD 的 AC 组成部分**:

| 事实 | 命令 | 结果 |
|---|---|---|
| GDD 含「性质测试」 | `grep -c "性质测试" design/gdd/clinic-machine.md` | **2**(`:370` AC-24-01 · `:372` AC-24-02) |
| **本卡面含「性质测试」** | `grep -c "性质测试" <本文件>` | **0** |
| 测试件有参数化 | `grep -n "TestCase\|Values(\|Random" clinic_modifier_eval_test.cs` | **零命中** |

GDD 把「性质测试」写成 AC-24-01 / AC-24-02 的**组成部分**(均 `[B]` 级)—— 原文:「对任意 `MIN < MAX`
与任意输入,24 的产出 = 线性加权和,**不含 clamp**」。**本卡面转写 AC 时把这一句整段丢了**,
故卡面读起来像「一条定点例子即可」。盘上只有 3 条定点例子 ⇒ 只能证伪,不能证全称命题。

**旁证:这不是全库惯例,是本次局部遗漏** —— `grep -rln "性质测试" production/epics/` 命中
10+ 个 epic(enemy-ai ×4 / time-weather / patient-ai / item-database / input-system / world-ecozones / death-respawn …),
**唯独 `clinic-machine/` 零命中**。
⇒ **建议另立条目追查 story-001 是否同样丢失。**

**⚠️ 因此:AC-24-01 / AC-24-02 的 `[x]` 属于「超前于证据」,处置待用户裁定**
(三选一:① 保持勾选+加注「性质面 NOT-RUN」· ② 取消勾选待补测 · ③ 先补参数化性质测试再维持勾选)。
评审建议 ③(约 20–30 行,不需要在勾选语义上妥协)。

**其他级(不阻塞)**:

| ID | 级别 | 内容 |
|---|---|---|
| D8 | MINOR | **证伪夹具 6/13 锚点过期**(eval 5/5 统一 +5 · wiring W2 +14)⇒ 证据件 §四「9 变异全 exactRed」**当前不可复算**(驱动 exit 2)。判别力本身没丢,失效的是「证据与当前代码的对应关系」。建议给驱动加 `--check-only` |
| D1 | WARNING | AC 第 6 条**在域内不可证伪**:域内最大 `192×65536 = 12 582 912` 装得进 int32 ⇒ int32/int64 实现逐位不可区分。卡面的「证词收窄」诚实,但剩余判别力来自结构而非数值 |
| D6 | WARNING | `ClinicMachineCookedCodec.cs:3-4` 自述「不重做校验」但实测复做 4 类 —— 口径今日一致,是**漂移面** |
| D2 | INFO | `ClinicModifierEvaluator.cs:247-248` 的 `cell.X + 1` 是未守卫 `int` 加法(域内不可达) |
| Q8 | minor | `tier < 0` 分支零测试 |
| Q6 | minor | codec 三条 `*_diskMatchesFreshBake` 有磁盘依赖 ⇒ Test Evidence 应登记为 **Integration** |

**⚠️ 评审推翻了两条专家结论,已记入原件 §五**:
- A 路「W2 文本查找会命中 `:283`」**错** —— `:283` 是 16 空格缩进、`:337` 是 12 空格,`expect` 12 空格 ⇒ 整行精确相等**唯一命中 `:337`**。
- A 路「`SumAdjacency` 死代码可删」**错** —— `:86`(`ClinicAdjSum` 内)正在调用它;按建议删会**编译失败**。
- 采信 B 路对我的纠正:驱动是纯行号索引,锚串不唯一**不影响驱动**,风险在修复期 grep 挑错靶。

### 偏离与边界

1. **本件范围超出卡面**:三表 cooked 落盘 + 运行期接线本属 story-001 面,但求值器无输入则不能开工。
   用户裁定「先补 cooked 落盘接线再做求值」⇒ 按序执行,账见 story-001 §Test Evidence「补记」。
2. **两条越域夹具的证词收窄**(见 AC 第 6 条注 + 证据件 §六 3)。
3. **`ClinicAdjTable` 对称性计数首版有缺陷**:按「有向条目数」比对自对 ⇒ 自对 `(bed,bed)` 被计 2 次 ⇒
   误报对称性缺失。已改为按无序键首次插入累加 + 补「重复有向键」拒收。
   **该缺陷由 `test_clinic_adjSum_nonFurnitureCellExcluded` 首跑暴露,非事后推断。**
4. **`Manifest Version` 10-02 → 2026-10-15**:逐件判定,两条新增约束均未命中实质要求(见头部注)。
