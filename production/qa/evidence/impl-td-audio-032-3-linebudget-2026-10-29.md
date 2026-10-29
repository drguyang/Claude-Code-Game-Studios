# 实现证据 —— `TD-audio-032` ③ 的 `BuildAudioSeams` 半边(方法 ≤ 40 行)

- **日期**:2026-10-29
- **批次**:音频债批 C · `TD-audio-032` ③(全条由 `open` 转 **`partial`**,① 仍 open)
- **commit**:`dc958f58` + 工作树(见 §四 三元组)
- **对象**:`unity/Assets/Gameplay.Boot/BootRoot.cs` ·
  `unity/Assets/Tests/EditMode/Audio/s15_5_hygiene_test.cs` · `docs/tech-debt-register.md`

---

## 一、原判定(债册 S15-7 刷账段,逐字)

> ③ 的行号已漂移 —— `BuildAudioSeams` 现行 = `BootRoot.cs:513-559`(方法体 `{`(:515) → `}`(:559)= **45 行**,
> 仍 >40 ⇒ **③ 的该半边仍 open**,本结案注此前未刷行号。

**复核(本轮)**:判定成立。修复前实测 **45 物理行**。

---

## 二、修复落点

| # | 落点 | 内容 |
|---|---|---|
| R-1 | `BootRoot.cs` | `BuildAudioSeams` **45 → 30 物理行**。原 ③(混音器解析 A-7)段抽成 `ResolveMixerOrFail(clips, mixerLoader)`,原 ⑤(玩家总线音量 A-5)段抽成 `ApplyPlayerBusDefaultsOrFail(mixer)` —— 两个私有方法各 **13 行**。**原文案与抛点一字未改**(异常类型、`ParamName`、报文全文均照搬) |
| R-2 | `BootRoot.cs` | 补 `using System.Collections.Generic;` —— `IReadOnlyList<string>` 成为新签名的一部分 |
| R-3 | `s15_5_hygiene_test.cs` | 新增 `test_td032_3_buildAudioSeams_withinLineBudget`:按**源码物理行**(剥注释后)钉三条签名 `BuildAudioSeams` / `ResolveMixerOrFail` / `ApplyPlayerBusDefaultsOrFail` |

**为什么另立一条夹具而不是塞进既有的 `test_td032_3_splitMethods_withinLineBudget`**:后者扫的是
`PatientCueDriver.cs`。两者是**不同文件、不同方法集**,合成一条会让「哪一半红了」不可分辨
—— 而这正是 S15-7 记下的教训本身(「拆分动作只兑现一半 = 未证」)。

---

## 三、证伪轮(S12-4 阈值边界形态)

驱动脚本:`/home/gu/.claude/jobs/181888b1/tmp/falsify032b.py`(每条变异独立 Unity 启动,
`total == 0` ⇒ `FILTER-MISS` 中止,期望测试名须出现在红列表,还原后 SHA-256 逐字节比对)。

| 变异 | 打靶(实测基线 → 变异后) | 期望红 | 实测 |
|---|---|---|---|
| M1 | `BuildAudioSeams` **30 → 45** | `test_td032_3_buildAudioSeams_withinLineBudget` | **RED-命中**(total=14) |
| M2 | `ResolveMixerOrFail` **13 → 43** | 同上 | **RED-命中** |
| M3 | `ApplyPlayerBusDefaultsOrFail` **13 → 43** | 同上 | **RED-命中** |

**3/3 全红;3/3 `RESTORE-OK`(`BootRoot.cs` SHA-256 回到基线 `36a4f823…`)。**
M1/M2/M3 各自命中 ⇒ 三条签名**逐条被钉**,不是「只有第一条生效」。

### 3.1 第一次证伪为什么 2/2 假存活(如实登记)

v1 变异只垫 **8 行**(30 → 38,仍 ≤ 40)⇒ **绿是正确行为**。
**根因是我的变异太弱,不是夹具失明。**

更糟的是我当时的**离线复算脚本 `StripComments` 漏了一步 `i+=1`** —— 它把注释行的换行重复计入,
算出 37 行,于是我先怀疑 `RepoRoot()` 的 `[CallerFilePath]` 读到了别的文件、方向整个错了。

**定案方式**:把夹具阈值**临时改成 1**,跑一次,让失败报文吐出它算出的真实值:

```
DIAG 「public static AudioAssemblySeams BuildAudioSeams(」实得 30 物理行 > 40
  Expected: less than or equal to 1
  But was:  30
```

诊断改动随后还原,`sha256` 与备份逐字节相同。

> **纪律(本轮新增)**:证伪轮出现假存活时,**第一排查顺序是「变异是否越过了阈值」**,
> 不是「夹具是否失明」。阈值类判据的变异必须**先按实测基线算一遍目标行数**,再决定垫多少行 ——
> 拿自己手写的计数器估算基线是不安全的(本轮就是这么错的)。

---

## 四、回归三元组(S12-0 口径)

| 套件 | 截至时刻 | 批次 | commit | 结果 | 对照 |
|---|---|---|---|---|---|
| EditMode | 2026-10-29 13:27 CST | 全量(无 filter) | `dc958f58` + 工作树 | **`5870 / 5833 / 0 / 0 / 37`**(exit 0) | `5869 + 1 = 5870` |
| PlayMode | 2026-10-29 13:27 CST | 全量(无 filter) | `dc958f58` + 工作树 | **`238 / 234 / 0 / 2 / 2`**(exit 2) | 与基线**逐位相同** |

**EditMode 等式**:`5869`(上轮 `TD-audio-035` ① 的全量,2026-10-29 11:41 CST)
`+ 1`(本轮新增 `test_td032_3_buildAudioSeams_withinLineBudget`)= **5870**。

**PlayMode 的两条 Inconclusive 是既有的具名占位**(`…_notRun`),不是新失败;exit 2 与基线同形,
**不写成绿、也不写成红**。

---

## 五、`TD-audio-032` 五项现状

| # | 项 | 状态 | 依据 |
|---|---|---|---|
| ① | 调参常量入生产(`CueIntervals.Default` / `BehaviorBands.Default`) | **open** | 触发条件 = `ai_patient.json` 表行落地;今日该文件不存在(复算:`ls assets/data/ \| grep ai_patient` 零命中) |
| ② | `(int)tick` 截断 | ✅ 结(S15-5) | 夹具 `test_td032_2_driver_keepsTickPastIntMax` |
| ③ | 方法超 40 行 | ✅ **本轮补齐**(`OnTickEdge` 半边 S15-5 已结 + `BuildAudioSeams` 半边本轮结) | §三 3/3 全红 |
| ④ | 簿记清扫面 + 每边沿分配 | ✅ 结(S15-5) | `TrackedPatientCount` + 快照夹具 |
| ⑤ | 测试面缺守卫(`RowCount > 0`) | ✅ 结(S15-5) | 判别力如实登记为**防御纵深**(库构造本身已 fail-loud) |

⇒ 索引行 `open → **partial**`(**不是** `closed` —— ① 仍 open)。

---

## 六、本轮**不**做

- ① 未动:触发点(13 数据轮 / `ai_patient.json`)今日不成立。**不给它填默认表** —— 那会让
  「数值归用户」的裁定变成一次猜测。
- 既有 `test_td032_3_splitMethods_withinLineBudget` 未改一行(它钉的是 `PatientCueDriver`,与本次无关)。
