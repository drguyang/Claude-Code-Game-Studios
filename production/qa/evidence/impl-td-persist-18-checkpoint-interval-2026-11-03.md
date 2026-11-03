# TD-persist-18 实现轮 —— `CHECKPOINT_INTERVAL` 的 tick 换算错 3 倍

**截至时刻**: 2026-11-03 23:20 CST(15:20 UTC)
**批次**: sprint-24 · 用户裁定「甲:改成 6000」
**commit**: `b0ff61c1` + 工作树(未提交)

---

## 1. 病因(一句话)

`OQ-7a-5`(✅ 2026-10-06 已裁)定的默认间隔是 **5 min**,而
`CheckpointFallback.DefaultIntervalTicks` 是 **`18000`** ——
按 `TICK_SECONDS = 0.05`(20 Hz / OQ-25-8)折 = **900 s = 15 min**。
⇒ 「崩溃损失 ≤ 一个间隔」这条不变量**实际被放大 3 倍**。

## 2. 实锤:注释自己就矛盾

改前 `CheckpointFallback.cs:47` 逐字:

```csharp
/// GDD 默认间隔:`CHECKPOINT_INTERVAL` = 5 min = 300 s = <c>18000</c> tick(@ 50 ms,
```

**300 ÷ 0.05 = 6000**,不是 18000。左右两边在自己的句子里对不上。

**旁证(同一套 tick 口径)**:`WEATHER_BLOCK_TICKS = 6000` 标注的就是 5 min
(ADR-038 Amendment A)⇒ **6000 tick = 5 min** 是本仓既有的换算基准,
反证 `18000` = 15 min。

## 3. 为什么这个错能活这么久

`checkpoint_fallback_test.cs` 的断言:

```csharp
Assert.AreEqual(18000, CheckpointFallback.DefaultIntervalTicks,
    "CHECKPOINT_INTERVAL 默认 = 5 min = 300 s ÷ TICK_SECONDS(0.05)= 18000 tick;…");
```

**报文里的算式(`÷ 0.05` = 6000)与断言的字面量(18000)对不上**,而判据只读字面量 ⇒ **恒绿**。

⇒ 这是 `.claude/docs/coding-standards.md` §假绿形态表 **F3(自指判据)** 的变体:
不是断言引用了被测常量,而是**断言照抄了错值** —— 「值与算式不一致」这件事**没有任何判据能看见**。

⚠️ 更麻烦的是错误**三层同源复制**:
**GDD `OQ-7a-5` 裁决原文**("定值 = 5 min 不变,即 `18000` tick @50 ms")
→ **代码注释** → **夹具字面断言**。三层互相背书,所以谁都没露馅。

## 4. 修法(用户裁定「甲:意图是 5 分钟」)

```csharp
public const int DefaultIntervalTicks = 6000;   // 5 min = 300 s ÷ 0.05 s
```

**裁决值「5 min」不动**,动的只是 tick 换算。落点四处:

| # | 文件 | 改了什么 |
|---|---|---|
| ① | `Sim.Codec/CheckpointFallback.cs` | 常数 `18000 → 6000`;常量 doc 写明「曾经左右不等」+ 算式;头注与 `intervalTicks` 形参 doc 同步 |
| ② | `Tests/EditMode/PersistenceService/checkpoint_fallback_test.cs` | 断言 `6000`;`beforeInterval` 的循环上界 `17999 → 5999`;头注与 summary 补订正说明 |
| ③ | `design/gdd/persistence-service.md` `OQ-7a-5` 行 | `18000` 划线保留,改 `6000`;裁定值 5 min 不动;行末加订正块并点名 F3 |
| ④ | `docs/tech-debt-register.md` | 新立并当轮关闭 `TD-persist-18`;三处历史 `18000` 加注(原文不删) |

⚠️ **报文改造**:断言报文现在写明 `300 s ÷ TICK_SECONDS(0.05) = 6000`。
下次谁再换算错,**算式与字面量会先对不上**,而这次是「对不上却没人看」。

## 5. 证伪

| 变异 | 内容 | 结果 |
|---|---|---|
| **M1** | `DefaultIntervalTicks = 6000` → `= 18000`(回到错值) | **1 红**,恰为目标条:`test_checkpointFallback_defaultInterval_isGddValue` |

**为什么只红 1 条是对的**:同 fixture 里 `test_checkpointFallback_beforeInterval_doesNotFire`
用的是 `MakeTimer(DefaultIntervalTicks)` + 循环 `5999` —— 常数变大时它**仍然不触发**,
按设计不该红。⇒ 红的恰好是那条**钉裁决值**的断言,不是全红借绿。

**还原判据**:`SHA-256 逐字节`
- 改前 = 还原后 = `0f665d259f8d02e232ee80d94801118c5da6c4edf8aaf7ed29675a9de52f21c3`
- 零 `MUT-` 残留(`grep -c "MUT-"` ⇒ 0)

## 6. 回归

| 套件 | total / passed / failed / inconclusive / skipped | 截至时刻(UTC) | 对照基线 |
|---|---|---|---|
| EditMode | **6779 / 6742 / 0 / 0 / 37** | 2026-11-03 15:11:44 → 15:13:38 | `6779 / 6742 / 0 / 0 / 37` @ 2026-11-03 14:26:27Z |
| PlayMode | **324 / 320 / 0 / 2 / 2** | 2026-11-03 15:15:06 → 15:15:18 | `324 / 320 / 0 / 2 / 2` @ 2026-11-03 14:17:52Z |

**等式**:`6779 − 6779 = 0` = 新增 `[Test]` 数 **0**(本轮只改一个常量与既有断言,零新增夹具)。
⚠️ 计数不变**不等于**没改行为 —— 变的是**间隔时长**(15 min → 5 min),断言口径未变。

⚠️ 编译警告 **0**(`grep -c "warning CS" unity/Logs/persist18-editmode.log` ⇒ 0)。

## 7. 复算命令(自包含)

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios

# ① 常数已是 6000
grep -n "DefaultIntervalTicks = " unity/Assets/Sim.Codec/CheckpointFallback.cs
# 期望:`public const int DefaultIntervalTicks = 6000;`

# ② 断言钉 6000,且报文带算式
grep -n "AreEqual(6000, CheckpointFallback.DefaultIntervalTicks" \
  unity/Assets/Tests/EditMode/PersistenceService/checkpoint_fallback_test.cs

# ③ 全仓生产代码里不再有 18000 的间隔值
grep -rn "18000" unity/Assets --include=*.cs | grep -v save_v5_runtime_params_test
# 期望:只剩历史注记 / `TICKS_PER_DAY` 夹具(与本常量无关)

# ④ GDD 裁定值未被改写
grep -n "定值 = 5 min 不变" design/gdd/persistence-service.md

# ⑤ 回归
python3 -c "
import re
s=open('unity/Logs/persist18-editmode.xml',encoding='utf-8',errors='replace').read()
print(re.search(r'total=\"(\d+)\"[^>]*?passed=\"(\d+)\"[^>]*?failed=\"(\d+)\"',s).groups())"
# 期望 ('6779','6742','0')
```

## 8. 如实登记的残留(不借绿)

| # | 残留 | 性质 |
|---|---|---|
| ① | **兜底写盘变密 3 倍** | 这是修对的**必然代价**,不是缺陷:同样时长里兜底 checkpoint 触发次数 ×3 ⇒ 同时放大 `TD-persist-17`(兜底与手动共池)的体感 —— 自动存档多吃 3 倍槽号。两件事**相互独立**,不合并 |
| ② | **`OQ-7a-5` 的裁决原文曾带同一个换算错** | 已在 GDD 行内订正(原文划线保留)。⚠️ 该行之外**可能还有**别处引用 `18000` —— 本轮只核了 `unity/Assets/` 与债册/EPIC,未做全仓文档普查 |
| ③ | **夹具断言与裁决值之间仍没有机械门** | 本轮把算式写进报文,只是**让人更容易看见**,不是门。⇒ 同类「值对不上算式」的错,今日仍**只能靠人读** |
| ④ | ~~**PlayMode 未跑**~~ **✅ 已补跑** | `324/320/0/2/2` @ 2026-11-03 15:15:06Z,与基线逐位同。原文不删 |

⚠️ **本债的 `closed` 只覆盖「这个常量改对了」**。上面四点里 ①②③ 一条都没动。
