# Edit Mode Tests

不进入 Play Mode 即运行的测试。用于**纯逻辑**:判断链、熟练度成长公式、伤害公式、
定点编解码、库存规则、数据校验。

## 本项目的 EditMode 覆盖面(权威来源 = `technical-preferences.md` §Testing)

| 必测项(BLOCKING) | 落点 |
|---|---|
| 判断链逻辑(8 诊断判断) | `tests/unit/diagnosis/` |
| 熟练度成长公式(30) | `tests/unit/skill/` —— 形式已定,数值归数值轮 |
| 战斗伤害公式(25) | `tests/unit/combat/` |
| `Fix` 编解码器(ADR-006 §五 D-21-18) | `tests/unit/sim/` ← **首个示例测试在此** |
| `FixParse` 负向夹具 | 同上 —— 浮点字面量须**导入期硬失败** |
| `ROUND_HALF_AWAY_FROM_ZERO` 边界 | 同上 —— `Round(±0.5/±1.5/±2.5)` |
| 守恒律 `Σ(w×out) ≤ EFF_MAX × Σ(w×in)` | `tests/unit/itemdb/` —— 整数域内求值,量纲须齐(ADR-006 D-21-19) |

## 程序集要求

需要 `tests/EditMode/EditModeTests.asmdef`。

> ⚠️ **本次刻意不生成该 asmdef** —— 程序集名称与清单归
> `docs/architecture/§Required ADRs #2「契约程序集清单与命名」` 裁决。
> 见 `tests/README.md` §未生成之物。

## 门 A 提醒

`ADR-017 §二` 要求 sim 程序集**引用集恰 = BCL**(`"noEngineReferences": true`),
且须以**白名单断言**升为构建失败。因此:纯 sim 逻辑的 EditMode 测试
**不应**引用 `UnityEngine` —— 若在测试里为了断言而 `Debug.Log`,就把被测面污染了。
UTF 的 `NUnit.Framework` 引用是合法的(测试程序集不受门 A 约束,被测的 sim 程序集才受)。
