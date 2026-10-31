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
| A 1–4 | 1–2 无载体 · 3 绑定夹具 · 4 具名人工 |
| B 5–14 | 5 / 6 / 11 绑定夹具 · 7–10 / 12–14 无载体 |
| C 15–18 | 15 / 16 / 17 绑定夹具 · 18 无载体(`TD-ci-01` EXTERNAL) |
| D 19–22 | 19 / 20 具名人工 · 21 / 22 无载体 |

> ⚠️ 复算(命令实跑,勿凭本表推断):
> ```bash
> cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
> python3 tests/smoke/run_smoke.py; echo "exit=$?"
> # ⇒ 合计 22 条 | 自动 PASS 0 · 自动 FAIL 0 · 未跑(需结果件) 7 · 具名人工 3 · 无名 12
> #   + 「无载体条目(逐条具名)」12 行;exit=3(INCOMPLETE)
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
> ⚠️ **本文件仍不是「22 条全可跑」**。现状 = 7 条绑定夹具 / 3 条具名人工 / **12 条无名**。
> 无名条目**逐条具名**在 `run_smoke.py` 的输出里,并各带一条「为什么没有载体」。
> 让它们归零的路径见债册 `TD-smoke-01` 的修法方向(逐条落地 / 缩表 / 两者并用)。
