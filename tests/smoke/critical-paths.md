# Smoke Test: Critical Paths

**Purpose**: 任何 QA 交接前,15 分钟内跑完这份清单。
**Run via**: `/smoke-check`(读本文件)
**Update**: 每实现一个核心系统就在对应节补条目。

> 条目的**顺序**即启动依赖顺序(承 `architecture.md` §Data Flow 初始化序列 [0]→[7])。
> 前面几条红,后面的判定无意义 —— 不要跳着跑。

## A. Core Stability(每次必跑)

1. 游戏启动到主菜单无崩溃(Addressables `data-core` 组预载成功,无 E-13 异常)
2. 「新游戏」可创建会话:存档头写入、`WorldSeed` 落头、`IIdAuthority` 计数器从 0 起
3. 主菜单对**键鼠与手柄**均有完整焦点路径(ADR-013 焦点单栈门 —— 无同键双触发)
4. 60 fps 平面模式下帧时间无长期 >16.6 ms(暂以Profiler目测;预算为临时值)

## B. 核心机制(P0 骨架 —— 随实现逐条激活)

5. `Step` 由 `ITickProvider` 以 20 Hz 推进,**不**由渲染帧驱动(观察:暂停渲染帧率,tick 数不变)
6. 病人出现:同场被模拟病人数 ≤ 24(超界 → 集成断言拒收,不是静默 LOD)
7. 采药:玩家跨格产生**一条** `ActorCellEntered`;连续移动本身不产生事件
8. 拾取:意图事件 + 当下判距 + 宽容半径 —— 离屏拾取被拒(非崩溃)
9. 诊断:脉案读 `VitalsDto` 有读数;`disease_id` **不出现**在任何 UI 元素(拟物铁律)
10. 急救(平面模式):一次完整动作落**两条**病史流事件;主机 `Judge` 出 `potency`
11. 处方:`Σ(weight×out) ≤ EFF_MAX × Σ(weight×in)` 守恒断言在日志中恒成立
12. 病例结案:`CaseClosed` 后「已处置」判定来自处置事件,而非玩家复选框
13. 建造:模块对齐整数格;帐篷(P0 唯一新建例外)可放置且不改世界流骨架
14. 疫情:52 抽池事件落在 `PATIENT_APPEARANCE_CAP` 界内,超界拒收 + 明确报错

## C. 数据完整性

15. 退出自动保存 → 重启读回:三流长度、`ConfigVersion`、`max(patient_id)` 高水位一致
16. checkpoint(默认 5 分钟)落盘且可回退到最近一次(ADR-010 §四 损坏回退路径)
17. 篡改存档字节一位 → 校验和红 + 自动回退,不出现半损坏状态继续玩
18. 存档往返后,同一 seed 重放的逐位哈希相同(`Fix` 全程 raw long,无 float 泄漏)

## D. 性能与确定性

19. 连续 5 分钟游玩内存无单调增长
20. `L_input → 呈现` < 50 ms(键鼠;仅测 `L_input`,**不**合并 `L_eval`)
21. 24 病人 × 20 Hz 求值不超 tick 预算(`L_eval ≤ 50 ms`)
22. Linux-x64-Mono 与 Linux-x64-IL2CPP 两构建的黄金夹具哈希逐字节相同(ADR-012 常驻门)

## 条目状态速记

> 🔁 **2026-10-31 全面订正**(`TD-smoke-01` · sprint-21 `S21-5`)。
> 原表是 2026-09-21 写的,六行「当前可跑?」**全部 `❌`**,其中**三条的依赖早已解除而表未回填**
> (工程根已存在 · `Sim.asmdef` 在盘 · `SimTickDriver.cs` 在盘)⇒ **旧表不能当排期依据**。
> 现行判据**不在本表**,而在机读表 `critical_paths.json`(`run_smoke.py` 逐条给出口)。
> 下表只保留「组 → 载体类别」的粗粒度对照,细节一律以机读表为准。

| 组 | 载体类别(2026-10-31) |
|---|---|
| A 1–4 | **1 无载体 · 2 绑定夹具(双载体,2026-10-31 新接)· 3 绑定夹具** · 4 具名人工 |
| B 5–14 | 5 / 6 / 7 / 11 / **12** 绑定夹具(#7、#12 为**双载体**)· 8–10 / 13–14 无载体 |
| C 15–18 | 15 / 16 / 17 绑定夹具 · 18 无载体(`TD-ci-01` EXTERNAL) |
| D 19–22 | 19 / 20 具名人工 · **21 绑定夹具(2026-10-31 新建)** · 22 无载体 |

> ⚠️ 复算(命令实跑,勿凭本表推断):
> ```bash
> cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
> python3 tests/smoke/run_smoke.py; echo "exit=$?"
> # ⇒ 合计 22 条 | 自动 PASS 11 · 自动 FAIL 0 · 未跑(需结果件) 0 · 具名人工 3 · 无名 8
> #   + 「无载体条目(逐条具名)」8 行;exit=3(INCOMPLETE)
> ```

## 怎么跑(2026-10-31 起)

本文件**已不再是「只有清单」** —— 执行入口是 `tests/smoke/run_smoke.py`,
机读镜像 = `tests/smoke/critical_paths.json`(手写,禁由脚本生成)。

| 命令 | 作用 |
|---|---|
| `python3 tests/smoke/run_smoke.py` | 结构校验 + 逐条出口;`test` 条报 `NOT-RUN(需 --results)` |
| `python3 tests/smoke/run_smoke.py --results <unity.xml>` | 用 Unity 结果件判绑定条目 |
| `python3 tests/smoke/run_smoke.py --emit-filter` | 打印 `-testFilter` 串,交给外部跑批 |
| `python3 tests/smoke/run_smoke.py --require-full` | 把 INCOMPLETE(3)升级为 FAIL(1),供 CI 硬门 |

**退出码**:`0` 全 PASS · `1` 有执行过且失败 · `2` 清单↔机读表不一致(结构损坏)·
`3` 无失败但仍有**无载体**条目(**不是绿**)。

> ⚠️ **3 不是 0。** 「没有可执行判据」与「通过了」是两件事 —— 把 3 读成绿,
> 就回到了 `TD-smoke-01` 要消灭的那个形态(整组 N/A 却报 PASS)。
>
> ⚠️ **本文件仍不是「22 条全可跑」**。现状 = **11 条绑定夹具(#2 / #7 / #12 为双载体)** / 3 条具名人工 / **8 条无名**。
>
> 🔁 **2026-10-31 第四轮(继续做 #2 / #13 / #14)**:
> - `#2` 接双载体:补齐**第三半「计数器从 0 起」** —— 新建
>   `unity/Assets/Tests/EditMode/Boot/new_session_id_authority_test.cs`
>   (真装配 ⇒ 空流高水位 = 0、首号 = 0、第二号 = 1、发号后高水位 = 2;另带 `Clear()` 反例防断言恒真)。
>   前两半(存档头写入 / `WorldSeed` 落头)由 `SaveComposerWiringTest` 承担。
> - `#13` **仍无载体,且查清是「功能未实现」不是「缺夹具」**:
>   全仓 `grep -rni "tent\b|帐篷" unity/Assets --include=*.cs` **零命中**(只有 `intent` 假阳性),
>   `assets/data/` 也无帐篷条目。GDD 有裁定(`game-concept.md:698`),**代码零实现** ⇒ 等 23 的实现轮。
> - `#14` 同理:**52 侧不产病人** —— `RandomEventsDirectorDriver` 当前只接「出诊启动」一节,
>   其余留具名槽(该文件头注自陈「双重空集 ⇒ 通电待信」)⇒ 无判据可挂,**缺的是接线不是夹具**。
>
> 🔁 **2026-10-31 第三轮(继续做 #21 / #12 / #14)**:
> - `#12` 接双载体:`test_ac3721_checkedButNoTreatmentInWindow_rejects`(**已勾选 + 窗口内无处置 ⇒ 拒**)
>   + `test_caseClosed_treatmentOnOpenTick_inclusiveBoundary_closes`(处置事件在窗口 ⇒ 结)。两半都在。
> - `#21` **新建夹具**:`PatientCapTickBudgetTest.test_cap24_singleTickEvaluation_under50ms` ——
>   建满 24 病人、计时 100 tick;实测 **max 0.077 ms / avg 0.038 ms**(预算 50 ms)。
>   自带**两道前置断言**(24 个不同病人 / 24 个都能拿到投影),防「病没建起来 ⇒ 空转 ⇒ 超快假绿」。
> - `#14` **仍无载体,但理由写准了**:CAP 的**执行点**由 #6 覆盖(`PresenceRegistry` 超界抛);
>   本条要的是「**52 抽池侧**不越界」,而 `EditMode/RandomEvents/` 里
>   `grep PatientAppear|DiseaseOnset` **零命中** ⇒ 52 的 spawn 接线没有夹具。**两条判据不同**。
>
> 🔁 **2026-10-31 两处更新**:
> ① `check` 字段现可写**字符串数组**(复合判据)—— 与 `coding-standards.md`
>    「一个 AC 允许多条载体」同口径;全部载体 Passed 才判 PASS,任一 FAIL 即 FAIL,
>    任一查无此测即 **STRUCTURAL**。
> ② `#7` 据此挂上两条载体(`test_ac113_diagonalCrossing_singleEvent` 跨格单事件 +
>    `test_ac103_mergeOperator_singleEventPerTick` 同 tick 8 样本仍 1 条)⇒ 无名 12 → **11**。
> ③ 同轮把 `#2` / `#13` 的 reason **写准**:它们不是「整体没证据」,而是**复合判据里有一半无载体**
>    (#2 的「计数器从 0 起」;#13 的「帐篷放置 / 不改世界流骨架」)—— 挂一半 = 假绿,故整条仍记无载体。
> 无名条目**逐条具名**在 `run_smoke.py` 的输出里,并各带一条「为什么没有载体」。
> 让它们归零的路径见债册 `TD-smoke-01` 的修法方向(逐条落地 / 缩表 / 两者并用)。

> 🔁 **2026-10-31 第四轮(规划 sprint-22 · 8 条无载体入台账)**:
> 新增 **`production/qa/smoke-carrier-backlog-2026-10-31.md`** —— 把 8 条 `none` 从「一个数」
> 变成可排期台账:每条写清**缺什么 / 解除条件 / 归属 / 建议落位**。
> 归类:**EXTERNAL 2**(#18 / #22)· **接线未落 3**(#8 / #10 / #14)· **功能未实现 1**(#13)·
> **运行时观测 1**(#9)· **真启动 1**(#1)。
> **本件不改变 smoke 判定** —— `run_smoke.py` 仍出 `exit=3`;台账 ≠ 判据。
> sprint-22 取走其中 3 条:`#1`(`S22-3` 造载体)· `#14`(`S22-4` 接线)·
> `#10` 与 `#13` 各出一张**裁定 / 勘察卡**(`S22-6` / `S22-8`),裁前不实现。
