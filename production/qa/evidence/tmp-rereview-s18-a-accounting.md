# Sprint-18 修复后重评 —— Agent A(原判据核账)草稿

**日期**:2026-10-22 · **HEAD** `d91edeb` · **模式**:只读核账(职责仅「原 5 条 Required 的修复落点是否在代码里 + 实跑原验证命令」)
**原判定来源**:`production/qa/evidence/review-sprint18-implementation-2026-10-22.md` §三

## 三列结账表

| ID | 原判定 | 修复落点(读代码实测) | 验证命令 + 实跑输出 | 结账 |
|---|---|---|---|---|
| R-1 | `Fold` 单方法 83 行 / CC 14–18 ⇒ 破「≤40 行」「<10」 | ✅ 已拆四方法:`HistoryFolder.cs:69-89` `Fold` · `:94-129` `Collect` · `:135-161` `BuildRows` · `:166-179` `Erase`。**自算**(python3 -I,span 原始行 / 去空行去注释 / CC≈1+决策点 `if|for|foreach|while|case|catch|switch` / `&&` / `\|\|` / `?`):Fold 21/18/**CC≈6** · Collect 36/30/**CC≈7** · BuildRows 27/24/**CC≈4** · Erase 14/13/**CC≈4** | 复算脚本实跑(见上列数字);四方法 **均 ≤40 行且 CC<10**。与原件的 21/36/27/14 行一致;CC Collect 原件写 ≈8 实为 ≈7、BuildRows 原件写 ≈3 实为 ≈4 —— 属启发式计法差,均远低于门限。**行为不变(29 条折叠测)未跑** —— 禁 Unity batch,未复现原件 §四 的 29/29 | ✅ 修复到位(规模判据自算通过;行为判据迁移回归,未自跑) |
| R-2 | 测试声称「文件末尾有 AST 式断言」而从未存在 ⇒ 应删虚假声明 + 登记 AC ⑥=NOT-RUN | ✅ `entity_death_gates_test.cs:11-21` 新增「⚠️ 本文件的验收范围」块,写明「此前声称…**该断言从未存在**…`AC ⑥ = NOT-RUN`(唯一证据 = 人工读源码)」;`:268-271` `test_ruleNine_*` 内注同款否定 + 「已删」。**无新增断言**(▸ `test_ruleNine_kindIsDataDrivenNotControlFlow` 仍自陈不构成 AC ⑥ 证据) | `grep -n "AST 式断言" <test>` ⇒ **2 处**(`:15` / `:271`),**均为否定句**,无肯定式残留;`grep -rn "规则九" unity/Assets/Editor.Tools.Gates/` ⇒ **rc=1 零命中**;`wc -l` = **281**(文件已扩,原判定时的 258 行口径已变,不影响结论) | ✅ 修复到位(缺口如实登记,**仍为 NOT-RUN**,未借绿) |
| R-3 | 头注 / `SelfLimited` 主注引已作废旧式 `self_limit(d) ∧ ¬IsCombatant` | ✅ `EntityDeathGates.cs:5-9` 头注改引 `[self_limit(d) ∨ IsCombatant(entity)] ∧ ¬(Player ∧ …战伤白名单)` 并注明「2026-10-21 GDD 就地订正…实现时不得照旧式写」;`:133-138` `SelfLimited` 主注改引订正式 + 明写「旧式…已作废」;测试 `:5-6` 同款 | `grep -rn "SelfLimited(entity, d) = self_limit(d) ∧ ¬IsCombatant" unity/Assets/` ⇒ **rc=1 零命中**;`grep -c "∨ IsCombatant" unity/Assets/Sim/EntityDeathGates.cs` ⇒ **2**(≥2 ✓)。残存旧式子串仅 `:137`,位于「旧式…代入 combatant 得 false…已作废」的作废注内(原件已声明允许);`:111` 的 `∧ ¬IsCombatant` 属 `LethalFor` 本身公式(正确,非旧式) | ✅ 修复到位 |
| R-4 | `LethalityOf(entry) && !IsCombatant(...)` 短路使 `Unspecified` 在非致死条目下静默 `false` | ✅ `EntityDeathGates.cs:129` = `return !IsCombatant(actorClass) && LethalityOf(entry);`;`:121-124` XML 注写明次序理由(短路致静默)。测试 `:104-119` `test_lethalFor_unspecifiedFailsLoud_evenOnNonLethalEntry`:致死条目 + 非致死条目**双断言均 `Assert.Throws<ArgumentOutOfRangeException>`** | `grep -n "!IsCombatant(actorClass) && LethalityOf(entry)"` ⇒ **:129 命中**;测试方法名逐字命中(`:105`)。**执行面未跑**:全量回归 `4419/4383/0/36` 与变异反证 `21/20/1/0`、SHA-256 还原 —— 禁 Unity batch,**未复现**;本列只证「代码次序已换 + 专钉断言已存在」 | ✅ 修复到位(静态判据成立;行为判据 = 原件主张,本轮未自跑) |
| R-5 | `adr-025:112` 与 `AssemblyGates.cs:807` 抽象点计数陈腐(「八抽象点」) | ✅ `adr-025-contract-assembly-manifest.md:112` = 「**九抽象点**(含 ADR-029 的 `IPayloadEncoder` · ADR-031 的 `IPayloadDecoder` · **S18-2 的 `IFoldedPredicate`**…)」;`AssemblyGates.cs:807` 错误串 = `"抽象点/SimEvent/VitalsDto 禁入 44(…"` —— **数字已去掉** | `grep -n "九抽象点" adr-025` ⇒ **1 命中**;`grep -rn "八抽象点" adr-025 + unity/Assets/Editor.Tools.Gates/` ⇒ **rc=1 零命中**。▸ 补记:`AssemblyGates.cs:416` 仍有「**八个**抽象点 + SimEvent + VitalsDto = 10 项」—— 这是**黑名单数组**的计数(数组确 10 项,`IFoldedPredicate` 未入,即原件 S-1 结转项),与「九抽象点」不矛盾,不属陈腐 | ✅ 修复到位 |

## 判定结论

- **5/5 修复落点在代码里实测成立**(R-2 的缺口本身**仍为 NOT-RUN**,但登记如实,符合原判定要求)。
- **无「修复未到位」条目**;也**无「原判定判错」条目** —— 五条原判定的技术事实均能被当前代码/命令复现(唯一口径变化:R-2 引用的 258 行现已 281 行,因同轮新增 R-4 专钉断言,不构成原判定失效)。
- **未自跑项(禁借绿,如实)**:R-1 的 29 条折叠测全绿、R-4 的全量回归三元组与变异反证 —— 均需 Unity batch,本轮禁止占用,仅记为原件主张,未独立复现。
