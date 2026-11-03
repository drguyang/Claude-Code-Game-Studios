# TD-persist-19 实现轮 —— 玩家槽的 `.bak` 不再参与读侧回退

**截至时刻**: 2026-11-04 00:45 CST(2026-11-03 16:45 UTC)
**批次**: sprint-24 · 用户指令「TD-persist-19 两步做掉」
**commit**: `10b213e6` + 工作树(本轮提交前)

---

## 1. 病因(一句话)

`TD-persist-16` 收窄的是**写侧**(玩家槽一生只写一次 ⇒ 不再产生 `save_{n}.bak`),
但 `SaveService.LoadBytes` **仍对任何槽无条件回退** `.bak` ⇒ 读侧支持一个写侧产不出来的东西。
用户盘上真实存在 `save_3/4/5.bak`(`TD-persist-15` 跨会话覆盖 bug 的遗留)⇒ 手动槽一旦校验坏,
会**静默装回更旧的档且不报损**,玩家看不见 —— 正是 `AC-7a-15`「绝不静默兜底」要禁的形态。

这是「已裁未兑现」而不是「新发现」:`ADR-010 §四` 2026-11-03 的加注早就把口径写成
「玩家槽损坏 ⇒ 走 `Corrupt` 报损,**不**回退」,只是代码没跟上。

## 2. 判据归一处

`SaveSlotLedger.FallsBackToBak(uint seq) => IsReservedSlotSeq(seq)` —— **唯一定义处**。
理由写在写侧:`.bak` 只在「同一文件被写第二次」时产生(`WriteAtomic` 的 final→bak 轮转);
保留槽(退出 / 自动)反复重写 ⇒ 有 bak;手动槽一生只写一次 ⇒ 永远没有。

三处调用点(sim 侧两处 + 呈现侧一处)**必须同源**,否则会出现「屏上说有档、读档说没有」:

| 调用点 | 改动 |
|---|---|
| `SaveService.LoadBytes` | `bakPath = FallsBackToBak(seq) ? GetBakPath(slot) : null` —— 手动槽不入回退支 |
| `SaveSlotProbe.Probe` | 手动槽不探 `.bak`(探针与读档面同向) |
| `SaveService.TryGetMostRecentSave` | 手动槽的 `.bak` 不算该槽存在 —— 否则菜单说「继续诊治」、按下报空 |

## 3. 手动槽的两条落点

| 磁盘状态 | 结果 |
|---|---|
| `final` 存在但校验坏 | **`SaveCorruptException`**(报损,不回退) |
| `final` 缺失(哪怕盘上有残留 `.bak`) | **`SaveNotFoundException`**(空槽) |

⚠️ 「缺失即空槽」不是退让,是判据的一部分:那份 `.bak` 从来不属于该槽。

## 4. 既有夹具的迁移(5 处)

原夹具用手动槽 4 造 `bak`,那是**生产不可达**的状态(`Checkpoint` 的只进不退门挡着),
经 `SaveBytes`(internal 低层原语)绕过才造得出来。逐条改到**退出保存**上 ——
它才是生产里唯一会被重写的槽:

| 夹具 | 处置 |
|---|---|
| `test_doubleBackup_bakCreatedOnSecondWrite` | 槽 4 → 退出保存 |
| `test_checksumFallback_readsBakWhenFinalCorrupted` | 槽 4 → 退出保存 |
| `test_corruptState_bothFinalAndBakBad_throwsSaveCorrupt` | 槽 4 → 退出保存(helper 加前置断言) |
| `test_corruptState_leavesSlotDirectoryUntouched` | 同上 |
| `test_fallbackState_finalBadBakGood_doesNotEnterCorrupt` | 槽 4 → 退出保存 |
| `test_finalMissing_bakCorrupt_throwsCorrupt_notFound` | 槽 4 → 退出保存 |
| `test_probe_bakOnly_isOccupied` | 拆两条:退出槽 `Occupied` / 手动槽 `Empty` |
| `test_probe_finalPreferredOverBak` | 槽 4 → 退出保存 |

⚠️ 这一条要单独说:`test_finalMissing_bakCorrupt_throwsCorrupt_notFound` 守的判据是
「缺失 ≠ 损坏」,**只有 bak 还算数时**它才有意义。改到退出保存上,它守的东西不变。

## 5. 新增夹具 `manual_slot_bak_scope_test.cs`(7 条)

| # | 断言 |
|---|---|
| ① | 手动槽校验坏 ⇒ `SaveCorruptException`(**不**读残留 bak) |
| ② | 手动槽 final 缺失 ⇒ `SaveNotFoundException`(残留 bak 不是档) |
| ③ | 退出保存校验坏 ⇒ **回退成功**(收窄不得越界砍掉保留槽的耐久性) |
| ④ | 判据本体:1 / 2 → true;3 / 4 / `uint.MaxValue` → false |
| ⑤ | 探针:手动槽只剩残留 bak ⇒ `Empty` |
| ⑥ | 「最新一份」:目录只剩残留 bak ⇒ 不算有档 |
| ⑦ | 「最新一份」**判别力用例**:手动槽 final 头坏 + 残留 bak(tick 900)vs 槽 5 好档(tick 10) ⇒ 选槽 5 |

## 6. 证伪轮(2 变异 + 1 干净对照)

| 轮 | 变异 | 结果 |
|---|---|---|
| **M1** | `FallsBackToBak` → 恒 `true` | **5 红** / 2 绿 |
| **M2** | `FallsBackToBak` → 恒 `false` | **2 红** / 5 绿 |
| **clean** | 无变异 | **7 / 7 全绿** |

两轮红集**不同且互不覆盖**(M1 红在手动槽那侧,M2 红在保留槽那侧 + 判据本体),
故不是「全红借绿」。还原判据 = SHA-256 逐字节:
`1051fee7d87345db462f1f0b89d28dfe7bcab0874b3f004bc97356ca3b1a935c`(改前 = 改后)。

### ⚠️ M1 逼出我自己两处**假判别力**(如实登记)

| 轮次 | 症状 | 真因 |
|---|---|---|
| **首次 M1** | ⑦ 的前身(`test_mostRecent_manualSlotWithOnlyStaleBak_isNotACandidate`)**在 M1 下照样绿** | 手动槽的候选是按 `save_*.bin` **枚举**出来的 ⇒ 残留 `.bak` 根本进不了候选表。它守的是「目录里只剩 bak 时说没有档」这个**结果**,不是 `Consider` 里的槽型分派 |
| **补测 M1b** | 新写的 ⑦ 用 `Corrupt()`(翻正文末字节)造场 —— **仍然绿** | `SaveSlotProbe` **只读头部、不验校验和**:校验和坏的文件在它眼里仍是 `Occupied` ⇒ `Consider` 的回退支**根本没走到** |

⇒ 第三版改为 `BreakHeader()`(压成 4 字节 ⇒ 头部读不出 ⇒ 探针判 `Corrupt`),M1 下才转红。
两条都写进了夹具注释,防止后来者用 `Corrupt()` 复制同一个「看起来在测、其实没测」的形态。

## 7. 盘面处置(第二步)

三件旧残留从存档目录**搬到**孤儿隔离区(与 2026-11-04 早先那六个同处,可逆):

```
~/.config/unity3d/DefaultCompany/unity/saves/
  save_3.bak ┐
  save_4.bak ├─→ _orphans-2026-11-04/
  save_5.bak ┘
```

`save_exit.bin.bak` **留在原地** —— 它属于退出保存,是**真实**的上一好档,回退要用它。

**复算**:
```bash
D="$HOME/.config/unity3d/DefaultCompany/unity/saves"
ls "$D"/*.bak                       # 期望:只剩 save_exit.bin.bak
ls "$D/_orphans-2026-11-04"         # 期望:九件(含 save_3/4/5.bak)
```

## 8. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6805 / 6768 / 0 / 0 / 37** | 2026-11-03 16:37:18 → 16:39:09 | `6797 / 6760 / 0 / 0 / 37` @ 2026-11-03 16:14:59Z · sprint-24 · `10b213e6` |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 16:39:43 → 16:39:55 | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 16:17:29Z · sprint-24 · `10b213e6` |

**等式**:`6805 − 6797 = 8` = 新增 `[Test]` 数 ——
`manual_slot_bak_scope_test.cs` **7 条** + `save_slot_probe_test.cs` 拆条净增 **1 条**
(原 `test_probe_bakOnly_isOccupied` 拆成「退出槽 Occupied」/「手动槽 Empty」两条)。
编译警告 **0**(两套各 0)。`PlayMode exit=2` 是本仓常态退出码,判据取 XML `failed`(= 0)。

## 9. 全量实测

见 §8 的表格与等式(本节不再重复数字,避免两处漂移)。

## 10. 未做 / 不借绿

- 本轮**只**动了 bak 的回退面;`WriteAtomic` 的轮转、校验和、原子写**一字未改**。
- `AC-7a-15` 本身仍按其既有状态记,本债的关闭**不**等于该 AC 整体签核。
