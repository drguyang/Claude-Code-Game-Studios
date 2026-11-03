# F-5.1 昼夜 —— 系统 5 的首个可执行件 · 证据原件

**日期**: 2026-11-03
**触发**: 存档册行内文字第二层只到「第 N 日」,用户要求补「时段」
**裁定**: 用户 2026-11-03 选 **甲** —— `NIGHT_SPAN = 9000` · `NIGHT_START = 27000`

---

## 一、交付物

| 件 | 落点 |
|---|---|
| 常量与公式 | `unity/Assets/Sim/Time/WorldTimeConstants.cs`(`DayPhase` / `IsNight` / `IsNightAt` / `FMod`) |
| 呈现 | `unity/Assets/Gameplay.UI/Skeuomorphic/SaveSlotItem.cs`(有档行 `壹 · 第壹日 昼`) |
| 装配 | `unity/Assets/Gameplay.Boot/BootRoot.cs`(`BuildSaveSlotViews` 调 `WorldTime.IsNight`) |
| 契约 | `unity/Assets/Gameplay.UI/Skeuomorphic/SaveSlotView.cs`(增 `IsNight`) |
| 夹具 | `Tests/EditMode/SimTime/world_time_test.cs`(+4)· `Tests/EditMode/SkeuomorphicUI/save_slot_rows_legibility_test.cs` |
| GDD 回填 | `design/gdd/time-and-weather.md:519` —— `NIGHT_START` / `NIGHT_SPAN` 由「未定」改为 `27000 / 9000` |

## 二、两处刻意的设计(都有可证伪的理由)

**① `IsNightAt` 开放为 public,只为让夹具能构造「跨日环绕」。**
生产值 `27000 + 9000 = 36000` **恰好不跨日**。拿生产值写判据时,
朴素写法 `phase >= start && phase < start + span` 与 `FMod` 式**结果完全一致** ——
GDD `:435` 要求的跨日正确性**在这组值下不可观测**。
⇒ 夹具必须注入一组会跨日的配置(`30000 / 12000`)。

**② `FMod` 不是装饰。**
C# 的 `%` 是截断语义(`-1 % 36000 == -1`)。直接写 `(phase - start) % TPD < span`
会在 `phase < NIGHT_START` 时得到负数 ⇒ 恒真 ⇒ **正午被判成夜**(GDD `:288` 点名的那条)。

**③ 时段只在 `DayNumber > 0` 时画。**
`IsNight` 的默认值是 `false`,恰好等于「昼」。若时间信息未就绪仍画时段,
就是**拿默认值静默断言一个没算过的值**。宁可不写。

## 三、证伪轮(2 变异,全部命中,SHA-256 逐字节还原)

| 变异 | 改什么 | 期望红 | 实测 |
|---|---|---|---|
| **M1** | `FMod(phase − start, TPD) < span` → `phase >= start && phase < start + span` | `test_f51_naiveRangeWouldBeWrong_acrossDayWrap` | ✅ 命中(**恰 1 条**) |
| **M2** | `FMod` → 裸 `a % m`(截断语义) | `test_f51_negativeTick_usesWrapSafeModulo` | ✅ 命中(该轮 3 红:另含 `productionValues_boundaries` / `acrossDayWrap`) |

还原判据 = **SHA-256 逐字节**:
`201e939a2834ca7f64162a6f5bcdb40fdb72d781bd272a58d43074c9be5b080b`(改前 = 还原后)。

⚠️ **M1 只红 1 条**是**正确**的:那条判据是跨日环绕的**唯一**防线,其余判据用生产值构造、
在朴素写法下照样成立。这正是「生产值不跨日」的证据,不是覆盖不足。

## 四、回归三元组

| 项 | 读数 |
|---|---|
| EditMode | `6670 / 6633 / 0 / 37`(total / passed / failed / skipped) |
| PlayMode | `324 / 320 / 0 / 2 skipped + 2 inconclusive` |
| 编译警告 | **0**(全量重编译口径,见下) |
| 截至时刻 · 批次 · commit | 2026-11-03 10:15 CST · F-5.1 实现轮 · `ffbbb57a` + 工作树 |

**等式**:`6670 − 6666 = 4` = `WorldTime` 新增 `[Test]` 数(行内文字侧为替换,净增 0)。

⚠️ **警告数的判据纪律**:必须先 `touch` 全部 `.cs` 强制全量重编译再数。
增量编译只重编译变动过的程序集,**没重编译的装配其警告根本不进日志** ——
本轮之前「27 → 0」正是这样得出的假值(后续又冒出 3 条,见 `ffbbb57a` 的 commit body)。

## 五、仍未做(逐项,不借绿)

| 项 | 状态 | 原因 |
|---|---|---|
| `TICKS_PER_SEASON` / `SEASONS_PER_YEAR` | **值未定** | F-5.2 季节无落点;GDD `:517-518` 均记「未定(值归用户)」 |
| `WEATHER_BLOCK_TICKS` | **值未定** | F-5.3 天气无落点 |
| tick→**历法**(年/月/日/时辰) | **设计里不存在** | GDD `:90` 明写历书上的干支与节气是**史实层呈现**、**不是机制读数**;规则一归属表无「历法」一行。存档册 spec §5.4 的「三月十九 酉时」需先改那句 GDD 才可做 |
| `AC-5-11`(装载期校验器:非法配置 ⇒ 硬失败) | **NOT-RUN** | 本类是 `const`,**没有装载期**;该 AC 要的是 5 的配置加载路径,今日不存在 |
| `AC-5-05`(`TICKS_PER_DAY` 单一定义点) | **结构性成立**,但**无机械门** | `Sim` 内唯一;若哪天有人在别处再写一份 `const`,没有门会红 —— 只有纪律 |

## 六、复算命令

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
# ① 常量值
grep -n "TicksPerDay = \|NightStart = \|NightSpan = " unity/Assets/Sim/Time/WorldTimeConstants.cs
# ② 生产值不跨日(⇒ 跨日正确性只能靠注入参数守)
python3 -c "print(27000 + 9000 == 36000)"
# ③ 唯一消费方
grep -rn "WorldTime\." unity/Assets --include=*.cs | grep -v "/Sim/Time/"
# ④ 夹具数
grep -c "\[Test\]" unity/Assets/Tests/EditMode/SimTime/world_time_test.cs
```
