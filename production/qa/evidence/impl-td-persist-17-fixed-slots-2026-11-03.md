# TD-persist-17 实现轮 —— 槽号按身份固定(1 退出 / 2 自动 / 3+ 手动)

**截至时刻**: 2026-11-03 23:58 CST(15:58 UTC)
**批次**: sprint-24 · 用户裁定「自动存档只占固定槽,不要往后挤手动存档槽」
**commit**: `28a8d84a` + 工作树(未提交)

---

## 1. 病因(一句话)

三个写入触发(定时兜底 / 叙事事件锚点 / 7b 手动)**共用同一个 `NextWritableSeq`**
⇒ 自动存档从 1 起**依次吃** 1、2、3、4……,而槽位屏只列固定三行
⇒ 自动存档走过的号,玩家的手动存档再也看不见。

**用户追问时的理解 → 修正**:「自动槽默认是第一个?」——
**方向对、机制要修正**:不是占一个固定槽,是**共用一个递增计数器**。

## 2. 这不是「新发现的不合理」,是 2026-10-06 已裁项的零兑现

`persistence-service.md:734`(`OQ-7a-8`,✅ 已裁)逐字:
「**共池 keep-N 会踩它(自动存档挤掉手动档 = 改写了玩家曾到达的入口)**。故**分池**:…」

⇒ 裁决要**三个池**,实现给**一个计数器**。代码里零池概念
(`KeepLast` / `SlotPool` / 池名,全仓生产代码零命中)。

## 3. 新形状(用户三轮追问定死)

| 槽 | 身份 | 文件 | 写入 |
|---|---|---|---|
| **1** | 退出保存 | `save_exit.bin`(独立名) | 每次退出**原地覆盖** |
| **2** | 自动存档(定时兜底 + 叙事事件锚点**共用**) | `save_auto.bin`(独立名) | 每次自动 checkpoint **原地覆盖** |
| **3+** | 手动存档 | `save_{n}.bin` | **只进不退** |

**四条连带裁决**:

| # | 问题 | 答案 |
|---|---|---|
| ① | 事件锚点也进固定槽? | **是** → `OQ-7a-8` 的「事件池保留全部」作废 |
| ② | 「继续诊治」读哪份? | 三者中 **tick 最新**(否则自动槽号小,永远输给手动档) |
| ③ | 「1/2 出故障不被坏档覆盖」指什么? | **旧档不被失败的新写覆盖** = 原子写保证(不是「损坏就拒写」) |
| ④ | 槽 1/2 的文件名 | **保留独立名** `save_exit.bin` / `save_auto.bin`,不占 `save_1.bin` |

## 4. 实现

| 文件 | 改动 |
|---|---|
| `SaveSlotLedger` | `ExitSlotSeq=1` / `AutoSlotSeq=2` / `MinManualSlotSeq=3` 的**权威常量**;`EnsureWritable` 新增**槽 0 拒收**门 + 只进不退**只作用于手动槽**;`OnCheckpointCommitted` 保留槽**不推高水位** |
| `SaveService` | `ExitSaveSlot` 由 `uint.MaxValue` 哨兵改为 **1**;新增 `AutoSaveSlot=2`;`FinalNameOf` / `SuffixNameOf` 统一三种命名形状;扫盘**只认 ≥3**;`TryGetMostRecentSave` 改「最新 tick」 |
| `SaveSlotProbe` | 按槽号**分派** 1/2/3+ 的路径;新增 `ProbeFile(path)` 供「最新 tick」用 |
| `CompositionRoot` | 两处自动 checkpoint → `SaveService.AutoSaveSlot`;floor `1 → 3` |
| `BootRoot` | `ManualSaveSlots` → `DisplayedSaveSlots`(降级为 fallback);槽位视图改为**由台账派生**:`[1, 2] + [3 .. max(next-1, 3)]` |
| GDD / ADR / 7b spec | `OQ-7a-8` 改裁块 · 规则十四 收窄到手动槽 · `AC-7a-19` 前件收窄 · ADR-010 槽位身份加注 · 7b 册上行源改「台账派生」 |

## 5. 一处**本轮改坏又改回**的语义(如实登记)

首版 `TryGetMostRecentSave` 要求候选**能读出 tick** 才算「有档」。
PlayMode `test_hasSave_showsContinueText_andAttemptsMostRecentSave` 当场转红 ——
它写的是**非法字节**的退出保存,期望菜单说「继续诊治」再在读取时报错。

**我的判据把「损坏的档」当成了「没有档」** ⇒ 菜单会显示「开始诊治」,
玩家以为档没了,然后安心开新局把它**覆盖掉**。这与 GDD 规则十一
「加载失败硬失败,绝不静默兜底」相反。

**现口径**:「有档」= **文件存在**;可读的按 tick 排序,读不出的取 `long.MinValue`
**排在最后**(有任何可读档时绝不会被选中,但只剩坏档时仍说「继续」,读档时报损)。

## 6. 证伪轮(3 变异,全部命中)

| 变异 | 内容 | 结果 |
|---|---|---|
| **M1** | `EnsureWritable` 去掉 `slot.SlotSeq >= MinManualSlotSeq`(保留槽也套只进不退) | **7 红** |
| **M2** | `OnCheckpointCommitted` 删掉保留槽豁免 | **首次 0 红 —— 等价变异** |
| **M2b** | 同上,补夹具后重跑 | **1 红**(`test_ledger_reservedSlotCommit_neverAdvancesHighWater`) |
| **M3** | 「最新」判据由 tick 换成槽号 | **2 红** |

🔴 **M2 首次零红是本轮抓到的真问题**:生产 floor 被夹到 3,而自动槽是 2 ⇒
`2 + 1 = 3` 推不动高水位 ⇒ **那道守卫在生产配置下不可观测**。
⇒ 补一条**绕开 `SaveService`、直接以 `floor = 0` 构造台账**的夹具,把契约钉在台账自己身上。
不补的话,那道守卫在证伪轮里永远是等价变异。

**还原判据**:`SHA-256 逐字节`,三次均逐字节一致,零 `MUT-` 残留。

## 7. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6789 / 6752 / 0 / 0 / 37** | 2026-11-03 15:57:47 → 15:59:38 | `6779 / 6742 / 0 / 0 / 37` @ 2026-11-03 15:11:44Z |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 15:52:50 → 15:53:02 | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 15:15:06Z |

**等式**:`6789 − 6779 = 10` = 新增 `[Test]` 数(9 条固定槽行为 + 1 条台账级守卫夹具)。

⚠️ 编译警告 **0**。

## 8. 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 槽号三段常量
grep -n "ExitSlotSeq = \|AutoSlotSeq = \|MinManualSlotSeq = " unity/Assets/Sim.Codec/SaveSlotLedger.cs
# 期望:1 / 2 / 3

# ② 槽 0 拒收 + 只进不退只作用于手动槽
grep -n "槽号 0 不是合法槽位\|slot.SlotSeq >= MinManualSlotSeq &&" unity/Assets/Sim.Codec/SaveSlotLedger.cs

# ③ 自动 checkpoint 写固定槽
grep -n "Checkpoint(SaveService.AutoSaveSlot)" unity/Assets/Gameplay.Boot/CompositionRoot.cs
# 期望:两处

# ④ 自动槽独立文件名
grep -n "AutoSaveFileName = " unity/Assets/Sim.Codec/SaveService.cs
# 期望:"save_auto.bin"

# ⑤ 「最近」判据 = tick(不是槽号)
grep -n "long tick = summary.State == SaveSlotState.Occupied" unity/Assets/Sim.Codec/SaveService.cs

# ⑥ 生产代码里不再有「自动存档用 NextWritableSeq」
grep -rn "Ledger.NextWritableSeq" unity/Assets --include=*.cs | grep -v "/Tests/"
# 期望:只剩 SaveSlotCommandRouter(手动存档)与 Bank/视图派生

# ⑦ 回归
python3 -c "
import re
s=open('unity/Logs/persist17f-editmode.xml',encoding='utf-8',errors='replace').read()
print(re.search(r'total=\"(\d+)\"[^>]*?passed=\"(\d+)\"[^>]*?failed=\"(\d+)\"',s).groups())"
```

## 9. 如实登记的残留(不借绿)

| # | 残留 | 性质 |
|---|---|---|
| ① | **用户磁盘上 `save_0/1/2.bin` 成为孤儿** | 不可见、不可载。`save_3…8.bin` 仍有效(手动槽正好从 3 起) |
| ② | **手动槽只进不退 ⇒ 第 2 次手动存档起落在 4、5…** | 册页「每页 6 行 / 超出转册」**未实现**,屏上行数只是变长。归 7b |
| ③ | **行内不区分「退出 / 自记 / 手动」三类措辞** | 归 7b 文案轮;本轮只让它们**都出现** |
| ④ | **`DisplayedSaveSlots` 常量仍在** | 已降级为 `SaveSlotViewProvider == null` 时的 fallback;生产走派生清单 |
| ⑤ | **迁移是 128 处手工+脚本混做** | 逐处核过语义,但**没有任何机械门**能证明「每一处都迁对了」—— 判据只有全量绿 |

⚠️ **本轮 `closed` 只覆盖「自动存档不再吃手动槽号」**。上面五条一条都没动。
