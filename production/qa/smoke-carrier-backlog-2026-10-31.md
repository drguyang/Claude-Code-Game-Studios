# Smoke 关键路径「无载体」台账 — 2026-10-31

> **本件是什么**:把 `run_smoke.py` 的 **8 条无载体**条目从「拒绝绿的一个数」变成**可排期的台账** ——
> 每条写清 **缺什么(实测依据)· 解除条件(可判)· 归属 · 建议落位**。
>
> **本件不是什么**:它**不改变** smoke 判定。`run_smoke.py` 仍按 `critical_paths.json` 出 `exit=3`,
> 台账里「已解除条件」只说明**怎么做才能有载体**,不说明**已经做了**。**台账 ≠ 判据**。
>
> **来源与溯源**:条目、标题、状态行**照引** `tests/smoke/critical_paths.json` @ `2026-10-31`
> (与 `production/qa/smoke-2026-10-31.md` §二 同一轮实跑);「缺什么」列中标注 **[本轮实测]** 的,
> 是 2026-10-31 规划轮**当场重跑过的命令**,不是转述。

**复算(自包含)**:

```bash
cd /home/gu/文档/nm/nm2/Claude-Code-Game-Studios
python3 - <<'PY'
import json
d = json.load(open('tests/smoke/critical_paths.json'))
tot = len(d['items'])
cnt = {'test': 0, 'manual': 0, 'none': 0}
for it in d['items']:
    cnt[it['status']] = cnt.get(it['status'], 0) + 1
print(f"合计 {tot} 条 | test {cnt['test']} · manual {cnt['manual']} · none {cnt['none']}")
for it in d['items']:
    if it['status'] == 'none':
        print(f"  #{it['id']} {it['title']}")
PY
# 期望:合计 22 条 | test 11 · manual 3 · none 8
#       并打印 #1 #8 #9 #10 #13 #14 #18 #22
```

---

## 一、八条总表

| # | 判据(照引 `critical_paths.json`) | 缺什么 | 类型 | 解除条件(可判) | 归属 | 建议落位 |
|---|---|---|---|---|---|---|
| **1** | 游戏启动到主菜单无崩溃(Addressables `data-core` 预载成功,无 E-13 异常) | **载体没有,链路在** —— `BootRoot.cs:143-169` 已实现 `InitializeAsync` + E-13 硬失败抛;既有 `boot_scene_wiring_test` 只做**资产文本扫描**(Boot.unity 挂载 + Addressable 条目共现),**不执行预载** | **真启动** | 一条**真跑** `InitializeAsync` + `data-core` 预载的夹具(PlayMode 优先);E-13 分支**须有反例**(预载失败 ⇒ 抛,不降级) | `Boot` / ADR-014 §五 | **sprint-22**(`S22-3`) |
| **8** | 拾取:意图事件 + 当下判距 + 宽容半径 —— 离屏拾取被拒(非崩溃) | **三段式的玩家侧接线未落** —— 事件侧(世界流 `ActorCellEntered` 等)有夹具,「玩家发出拾取意图 → 主机当下判距」这一段**生产无调用方** | **接线未落** | 玩家侧意图路径有**生产调用点** + 一条「离屏 ⇒ 拒绝且零事件」的夹具(不是崩溃、不是静默成功) | ADR-009 §七 · 系统 1/世界 | sprint-23 起(先勘察) |
| **9** | 诊断:脉案读 `VitalsDto` 有读数;`disease_id` 不出现于任何 UI 元素 | 静态守卫 `PresentationDtoGuard` **有夹具**(AC-37-15);缺的是**运行期**观测 —— 需 42 接真 `VitalsDto` 后在运行期读一次 | **运行时观测** | 42 有真 DTO 消费路径 + 一条运行期夹具(读到的读数为非空,且 `disease_id` 反射零命中) | ADR-013 · 系统 42/8 | 42 呈现轮 |
| **10** | 急救(平面):一次完整动作落**两条**病史流事件;主机 `Judge` 出 `potency` | **接线被裁定退役** —— `EmergencyAttemptDriver` 文件头自述「驱动体 = 空操作」,源于 2026-10-11 用户裁定 **G4.1「急救手动化」** [本轮实测] | **裁定冲突** | **先裁**:G4.1「手动化」与本条判据(动作 ⇒ 两条事件)是否仍兼容。裁前**任何夹具都无对象** | 系统 10 · ADR-009 Amendment I | **sprint-22 出两案**(`S22-6`),不实现 |
| **13** | 建造:模块对齐整数格;帐篷(P0 唯一新建例外)可放置且不改世界流骨架 | 前半**有载体**(`BuildSlotCatalog` 用 `WorldPos` 整数格);后半「帐篷」**功能未实现** —— 全仓 `grep -rni "tent\b\|帐篷"` 仅 `intent` 假阳性,`assets/data/` 无条目 [引用自 `critical_paths.json` @ 2026-10-31] | **功能未实现** | GDD `game-concept.md:698` 的裁定须落成**一个承接者**(story 或债项),代码 + 数据齐备后才有可判对象 | `modular-building`(23) | **sprint-22 勘察 + 两案**(`S22-8`) |
| **14** | 疫情:52 抽池事件落在 `PATIENT_APPEARANCE_CAP` 界内,超界拒收 + 明确报错 | CAP 的**执行点**由 `#6` 覆盖(`PresenceRegistry` 超界抛 + id 回滚);缺的是 **52 抽池侧** —— `RandomEventsDirectorDriver` 文件头列明「急召生成链(抽池 → 掷骰 → F1 管线 → `EventArrived`)」为**具名槽,本件不接** [本轮实测] | **接线未落** | 急召生成链有生产调用点;夹具须用**合成输入**驱动(否则双空集 ⇒ 恒零 ⇒ 假绿,见 `RandomEventsDirectorDriver` 文件头「双重空集」注) | 系统 52 · ADR-007 / ADR-008 §六 | **sprint-22**(`S22-4`) |
| **18** | 存档往返后,同一 seed 重放的逐位哈希相同(`Fix` 全程 raw `long`,无 float 泄漏) | **EXTERNAL** —— 判据是**跨构建**逐位对照,归 `TD-ci-01`(CI 凭证在用户侧) | **EXTERNAL** | CI runner 落地 + 三格矩阵可跑 | `TD-ci-01` | 等外部,不排 |
| **22** | Linux-x64-Mono 与 Linux-x64-IL2CPP 两构建的黄金夹具哈希逐字节相同 | **EXTERNAL** —— ADR-012 常驻矩阵,同上 | **EXTERNAL** | 同上 | `TD-ci-01` / ADR-012 | 等外部,不排 |

---

## 二、三条具名人工(不在此台账的「补载体」面内)

| # | 判据 | 为什么不自动化 |
|---|---|---|
| 4 | 60 fps 平面模式下帧时间无长期 > 16.6 ms | 需目标硬件 + 真场景;`#21` 只测 sim 求值单 tick,不等于帧时间 |
| 19 | 连续 5 分钟游玩内存无单调增长 | 需真游玩 + 采样;`soak-test` skill 负责,非单元面 |
| 20 | `L_input` → 呈现 < 50 ms(仅测 `L_input`) | 承 `emergency-procedures.md` F-10.6 切分;需真实输入设备链路 |

---

## 三、归类统计(与总表逐条可对)

| 类型 | 条数 | 条目 |
|---|---|---|
| **EXTERNAL**(等外部,排不动) | **2** | #18 · #22 |
| **接线未落**(有生产代码,缺调用点) | **3** | #8 · #10(另含裁定冲突)· #14 |
| **功能未实现**(连生产代码都没有) | **1** | #13 |
| **运行时观测**(静态件齐,缺运行期夹具) | **1** | #9 |
| **真启动**(链路在,缺真跑夹具) | **1** | #1 |

⇒ **8 条里,本轮(sprint-22)能动的 = 3 条**(#1 造载体 · #14 接线 · #10 出裁定);
**其中 `#10` / `#13` 在裁定前不得开工** —— 开工即造「测一个不存在的对象」的 F2 形态假绿。

---

## 四、与 sprint-22 的关系

`production/sprints/sprint-22.md` 的 Must 含 **`S22-3`(#1)** 与 **`S22-4`(#14)**,
Should / Nice 含 **`S22-6`(#10 两案)** 与 **`S22-8`(#13 勘察 + 两案)**。
**本台账不承诺这些卡一定在 sprint-22 内结清** —— 结清判据在各自卡面,不在本件。

> ⚠️ **一条纪律(承 `coding-standards.md` §Review Evidence Standards)**:某条从 `none` 变 `test` 时,
> `critical_paths.json` 的 `status` 与 `check` **同批改**,且 `run_smoke.py` 须实跑一遍看到
> 自动 PASS 数**增加**。只改 json 不实跑 = 借绿。
