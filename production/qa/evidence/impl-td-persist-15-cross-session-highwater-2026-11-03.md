# TD-persist-15 实现轮 —— 槽位高水位的跨会话初值

**截至时刻**: 2026-11-03 22:14 CST(14:14 UTC)
**批次**: sprint-24 · 用户裁定「甲:扫目录 + 修代码 + ADR 加注」
**commit**: `fa965979` + 工作树(未提交)

---

## 1. 病因(一句话)

GDD 规则十四 的「**槽位只进不退**」语义上是**跨会话**的,而护栏的高水位初值来自
装配层硬编码的 `1` ⇒ **每个新会话内存里都从 1 起** ⇒ `EnsureWritable(1)` 判
`1 < 1` = false ⇒ **放行** ⇒ 覆盖磁盘上上一会话的 `save_1.bin`。

## 2. 这条不是推测 —— 机器上已经发生了

`saves/` 目录(`~/.config/unity3d/DefaultCompany/unity/saves/`)的尸检:

```
save_1.bin  1868B  09:23   1afb5262…    ← 第二个会话写的
save_1.bak  3868B  08:22   f269beb8…    ← 第一个会话的 save_1,被顶掉
save_5.bin  1868B  09:24   1afb5262…
save_5.bak  3868B  08:22   f269beb8…
save_6.bin  1868B  09:24   1afb5262…    ← 无 .bak ⇒ 本会话首次写的
```

`save_1…5` 全部被**第二个会话**覆盖;`save_0.bin` 幸存只因第二个会话从 `1` 起号、没走到 0。

**复算命令**:

```bash
cd ~/.config/unity3d/DefaultCompany/unity/saves/
sha256sum save_1.bin save_1.bak save_5.bin save_5.bak save_6.bin
# 期望:save_1/5/6.bin 同为 1afb52…;save_1/5.bak 同为 f269be…
ls -la save_*.bin | awk '{print $5, $9}'   # save_1…5 有 .bak,save_6…8 没有
```

## 3. 那一个悬着的裁定 —— 结论是**出处错位**,不是冲突

`SaveSlotLedger` 头注逐字:「**从存档恢复时不得用存档体里的值 —— 锁与高水位都是会话内存
(ADR-010 §六)**」。核 ADR-010 §六 原文:

> **锁字段不进存档体** —— 锁与安全门均**会话内存**,跨会话复位(重启读档不复位反 scum 语义,
> 防 scum 靠「载入安全上下文门」而非格式锁)。

**主语只有两个:锁、安全门。ADR 从未裁过高水位。** ⇒ 实现侧把「锁与安全门」扩写成
「锁与高水位」= **过度引据**,并由此推出一条与 GDD 规则十四 直接冲突的禁令。

⇒ 「扫目录」读的是**文件名**,不是文件内容 ⇒ **不是**「从存档体恢复」。
文件存在与否是文件系统自己的事实,不是存档体里存的任何值。

**ADR-010 §六 已就地加注**,把「本节不覆盖什么」写明,防止第二次误读。

## 4. 修法

```csharp
// SaveService 构造器(唯一求值点)
Directory.CreateDirectory(_basePath);
_ledger = new SaveSlotLedger(narrativeContext, ResolveInitialNextWritableSeq(initialNextWritableSeq));
```

| 情形 | 结果 |
|---|---|
| 磁盘上有 `save_{N}.bin` | `max(floor, N + 1)` |
| 目录里一张都没有 | 原样返回 `floor`(`initialNextWritableSeq` 自此是**下限**,不是终值) |
| `save_exit.bin` / `.bak` / `.tmp` / 非 `.bin` | **不计**(`TryParseSlotSeq` 要求 `save_` 后全数字) |
| 枚举抛异常 | **`uint.MaxValue`** ⇒ `Checkpoint` 拒绝一切写入 + `LastSlotScanError` 交装配层记 (fail-closed:不可用 > 不安全) |

**三条设计判断**:

| # | 判断 | 理由 |
|---|---|---|
| ① | 扫描放 `SaveService` 构造器,不放装配层 | 「什么算存档」的命名约定**只在本类**(`TryGetMostRecentSave` 同出处,其 doc 逐字「装配层不得自己拼 `save_*.bin`」)。放装配层 = 第二份命名 + 可被忘掉 |
| ② | 空目录 ⇒ 退回 floor,**不**改成 `max+1` | 否则 `test_smoke_defaultInitialNextWritableSeq_staysZero` 的「缺省仍为 0」语义被无声改掉;空目录也确实没有可覆盖的东西 |
| ③ | 枚举失败 ⇒ 拒绝一切写入 | 与 `SaveSlotLedger` 既有的 fail-closed 纪律同向(「禁 fail-open」),也与本仓「不可用 > 不安全」逐字一致 |

## 5. 夹具

### 5.1 新增 6 条(`save_slot_lock_release_test.cs` §①-b)

| 夹具 | 断什么 |
|---|---|
| `test_crossSession_existingSlots_raiseHighWaterPastThem` | `save_1..3` 在盘 ⇒ `NextWritableSeq == 4` |
| `test_crossSession_existingSlot_isRefusedAndBytesUnchanged` | `Checkpoint(1)` **抛** `InvalidDataException`,**且旧档字节逐字不变** |
| `test_crossSession_nextSaveLandsOnFirstFreeSlot` | 新会话第一次写落在 `save_3`,`save_1` 不被碰 |
| `test_crossSession_emptyDirectory_floorAppliesUnchanged` | 空目录 ⇒ 1(负控) |
| `test_crossSession_nonSlotFiles_doNotRaiseHighWater` | `save_exit.bin` / `.bak` / `.tmp` / `.txt` 一律不算(负控) |
| `test_crossSession_slotZeroOnDisk_raisesFromDefaultFloor` | 缺省 floor 0 + 盘上有 `save_0` ⇒ 1 |

**为什么「抛了没」与「字节不变」要分开断**:只断抛,「拒之前已经覆盖了」也能绿
(该形态见 `.claude/docs/coding-standards.md` §假绿 F8:行为等价掩盖结构分叉)。

### 5.2 改写 1 条(`save_slot_probe_test.cs::test_probe_finalPreferredOverBak`)

**旧写法靠的就是这条 bug**:它构造**第二个** `SaveService` 再写槽 1 ——
修好后当场抛 `槽位只进不退:slot_seq=1 < next_writable_seq=2`。
判据(**final 可读时以 final 为准**)不变,**造场方式**改为:写两份**合法**存档
(槽 1 = tick 5 / 槽 N = tick 9),再把槽 N 的字节搬成槽 1 的 final ⇒ 双档形态零违规写入造出。

### 5.3 顺手补 1 条 PlayMode 断言(`main_menu_wiring_test.cs`)

`test_saveQueryFailure_fallsBackToNoSave` 把存档目录 `chmod 000` 制造「查询失败」。
本轮之后,构造期扫目录**同向失败** ⇒ 装配层记一条 **Error** ⇒ 该夹具被「意料之外的日志」判红
(首轮 PlayMode `324/319/1` 就是它)。处置:

- 补 `LogAssert.Expect(LogType.Error, new Regex("存档目录枚举失败"))`;
- 补 `Assert.IsNotNull(saves.LastSlotScanError)` —— **该事实必须被记录**,否则
  「本次会话拒绝一切存档」无从解释。

⚠️ **这是本轮唯一一条「意外收获的证据」**:那条 `Expect` 在 Linux 上**真的触发了**
(`Access to the path '/tmp/dayijing_mainmenu_…' is denied`)⇒ **fail-closed 分支可达**,
且现在**有载体**(见 §9 ②)。

## 6. 证伪轮(2 变异,各 4 红)

| 变异 | 内容 | 结果 |
|---|---|---|
| **M1** | `ResolveInitialNextWritableSeq` 末尾直接 `return floor;`(不回视磁盘 —— 等价于修复前的硬编码) | **4 红** |
| **M2** | `uint next = max + 1;` → `uint next = max;`(差一) | **4 红** |

两轮红的是**同样 4 条**:`existingSlot_isRefusedAndBytesUnchanged` ·
`existingSlots_raiseHighWaterPastThem` · `nextSaveLandsOnFirstFreeSlot` ·
`slotZeroOnDisk_raisesFromDefaultFloor`。
**未红的 2 条**(`emptyDirectory_floorAppliesUnchanged` · `nonSlotFiles_doNotRaiseHighWater`)
正是**负控** —— 它们要求「保持 floor」,变异本不该碰它们。⇒ 判别方向正确,不是全红借绿。

**还原判据**:`SHA-256 逐字节`
- 两轮改前 = 还原后 = `6dce956ed17fcab05448c59a8b24deb91b9de266a826831831e65d415f2dd316`
- 零 `MUT-` 残留(`grep -c "MUT-"` ⇒ 0)

## 7. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6779 / 6742 / 0 / 0 / 37** | 2026-11-03 14:01:10 → 14:03:03 | `6773 / 6736 / 0 / 0 / 37` @ 2026-11-03 11:57:15Z |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 14:17:52 → 14:18:04 | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 08:01:06Z |

**等式**:`6779 − 6773 = 6` = 新增 `[Test]` 数(净 +6:新增 6 + 改写 1 条不增)。

⚠️ **首轮跑出 1 红** `test_probe_finalPreferredOverBak`(见 §5.2),改的是**夹具的造场方式**,
不是生产代码;该条改动本身已如实登记 —— 它的旧写法**只有靠 bug 才成立**。

⚠️ 编译警告 **0**(`grep -c "warning CS" unity/Logs/persist15-editmode.log` ⇒ 0)。

## 8. 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 求值点唯一,且 floor 不再是终值
grep -n "ResolveInitialNextWritableSeq" unity/Assets/Sim.Codec/SaveService.cs

# ② 扫描读文件名,不读存档体 —— 期望 2 命中
#    (`:290` 既有 `TryGetMostRecentSave` + `:375` 本轮新增;同一命名约定、同一出处)
grep -n 'EnumerateFiles(_basePath, "save_\*.bin")' unity/Assets/Sim.Codec/SaveService.cs

# ③ 枚举失败 = 拒绝一切写入
grep -n "存档目录枚举失败,槽位高水位不可知" unity/Assets/Sim.Codec/SaveService.cs

# ④ 装配层的 1 已被注明为 floor(只断注释,不断行为)
grep -n "本值自此是 floor" unity/Assets/Gameplay.Boot/CompositionRoot.cs

# ⑤ ADR 加注在
grep -n "本节\*\*不覆盖\*\*什么" docs/architecture/adr-010-persistence-save-format.md

# ⑥ 回归
python3 -c "
import re
s=open('unity/Logs/persist15-editmode.xml',encoding='utf-8',errors='replace').read()
print(re.search(r'total=\"(\d+)\"[^>]*?passed=\"(\d+)\"[^>]*?failed=\"(\d+)\"',s).groups())"
# 期望 ('6779','6742','0')
```

## 9. 如实登记的残留(不借绿)

| # | 残留 | 性质 |
|---|---|---|
| ① | **`.bak` 对玩家槽从此永不生成** | 修好「只进不退」后,`save_{N}.bin` 一生只写一次 ⇒ `WriteAtomic` 的 final→bak 分支不可达 ⇒ `AC-7a-15 (a)` 的「回退上一好档」对**玩家槽**无宾语(对 `save_exit.bin` 仍成立)。**已立 `TD-persist-16`**,三条候选解法均未实测 | 
| ② | ~~枚举失败分支无测试载体~~ **✅ 本轮已推翻(原文不删)** | 首轮写这条时我判它「无法造异常」。**实测错了**:`main_menu_wiring_test` 早就在用 `chmod 000` 注入目录不可读,而它**真的让扫描抛了** ⇒ 该分支**可达且已有载体**(PlayMode `324/320/0/2/2` 里的那条 `LogAssert.Expect` + `LastSlotScanError` 断言)。⚠️ 仍有一条**残留**:`chmod` 在 root 下失效 ⇒ 该夹具会 `Assert.Ignore`(它自带守卫),那时本条回到无载体状态 —— 该守卫已如实写在夹具里 |
| ③ | **槽号出列表的呈现面** | 高水位修好后,第 4 次之后的槽号仍不在 7b 的固定三行里(`ManualSaveSlots = {1,2,3}`)⇒ 存进去了但看不见。归 7b「槽位列表须由台账派生」 |
| ④ | **自动 checkpoint 与手动槽共用同一号段** | 兜底 / 事件触发的 checkpoint 也走 `NextWritableSeq` ⇒ 自动存档会吃掉手动槽号。**这不是本轮引入的**,也**不在本债面内**,但修好跨会话后它会更快显形 —— 如实登记,不顺手改 |

⚠️ **本债的 `closed` 只覆盖「跨会话高水位」一条**。上面四条一条都没动。
