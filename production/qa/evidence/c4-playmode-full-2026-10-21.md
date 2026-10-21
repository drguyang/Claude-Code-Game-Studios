# C-4 补跑记录 —— PlayMode 全量回归(2026-10-21)

**对象**:`qa-signoff-sprint-17-2026-10-21.md` §5 条件 **C-4**(PlayMode 全量 / IL2CPP 三格矩阵 / CI 常驻)
**本件范围**:**只结清 C-4 的 PlayMode 全量半边**。另两半仍开,见 §3。

---

## 1. 为什么需要这一轮

签核件 §4 第 3 行记:`s17-6s-il2cpp-player.xml`(5 测)与 `s17-6q-playmode-editor.xml`(5/5/0/0)
**均为 5 测的子集**,不是 PlayMode 全量(上一全量 = `smoke-s16-playmode.xml` **197** 条)。
⇒ 「PlayMode 全量」在签核时点**从未在本 sprint 内跑过**。

---

## 2. 实测

**命令**:

```
cd unity && unity test "$PWD" --mode PlayMode \
  --output unity/Logs/c4-playmode-full-2026-10-21.xml --no-banner
```

进程退出码 **0**。

**溯源三元组**:截至时刻 `2026-10-21 17:45 CST` · 批次 `C-4 补跑(全量)` · commit `ed5f08f`

| | total | passed | failed | inconclusive | skipped |
|---|---|---|---|---|---|
| **基线** `smoke-s16-playmode.xml`(2026-10-21 00:22) | 197 | 197 | 0 | 0 | 0 |
| **本次** `c4-playmode-full-2026-10-21.xml`(17:45) | **202** | **202** | **0** | **0** | **0** |

### 2.1 集合差(按 `fullname`,不从 Δtotal 推)

`Δtotal = +5`,但 **Δ 不是证据** —— 下面是从两份 XML 的 `test-case@fullname` 直接求的集合差:

**新增 5 条,全部属同一类**:

```
+ DaYiJingCheng.Tests.PlayMode.ClinicMachine.ClinicRoomMatchingPlayerTest.test_emptyRegistry_roomNone
+ DaYiJingCheng.Tests.PlayMode.ClinicMachine.ClinicRoomMatchingPlayerTest.test_playerAssembly_assembleWithoutStructures_matchingIsNull
+ DaYiJingCheng.Tests.PlayMode.ClinicMachine.ClinicRoomMatchingPlayerTest.test_playerAssembly_canReferenceMatchingChainTypes
+ DaYiJingCheng.Tests.PlayMode.ClinicMachine.ClinicRoomMatchingPlayerTest.test_surgeryWithCabinet_matchSurgery
+ DaYiJingCheng.Tests.PlayMode.ClinicMachine.ClinicRoomMatchingPlayerTest.test_twoBeds_matchWard
```

**消失 0 条。**

⇒ `新增 5 · 消失 0`,5 条正是 S17-6 新落的 PlayMode 载体(`Tests/PlayMode/ClinicMachine/`),
**零回归**。

---

## 3. C-4 的另外两半 —— 仍开(禁借绿)

| 半边 | 状态 | 依据 |
|---|---|---|
| **PlayMode 全量** | ✅ **已结清(本件)** | 202/202/0/0,集合差 新增 5 · 消失 0 |
| **IL2CPP 三格矩阵** | ⏳ **仍开** | 本机只跑 **Linux-x64** 一格(Mono + IL2CPP 各一轮,见 `probe-player-assembly-gate-2026-10-21.md` §7)。**Linux-ARM64-IL2CPP 目标不存在**(用户裁定长期未达);Windows-x64 / Apple Silicon 归**发版前**,不进常驻 |
| **跨平台 CI 常驻 job** | ⏳ **仍开** | job 已建但缺 `UNITY_CLIENT_ID` / `UNITY_CLIENT_SECRET`(承 sprint-16 §6 第 5 行的 2026-10-21 订正)。**从未跑过** |

⇒ **C-4 不因本件而整条结清**。准确表述是「**C-4 的 PlayMode 全量半边已结清,矩阵与 CI 两半仍开**」。
不得在任何下游件写成「C-4 已结」。

---

## 4. 边界

- 本轮跑的是 **PlayMode in Editor(Mono)** —— 不是 IL2CPP player 里的 PlayMode。
  IL2CPP 下的 PlayMode 证据仍只有 S17-6 那 5 条(`s17-6s-il2cpp-player.xml`),
  与本轮 202 条**不是同一范围**,不得互相替代。
- `202/202/0/0` 里 **0 inconclusive** —— 但注意 `#if !UNITY_EDITOR` 那 4 件 PlayMode 测试
  在**编辑器**内照常运行(守卫只影响 player 侧),故本轮它们是真跑的,不是被守卫跳过的。
