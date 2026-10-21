# S17-2 收口件 —— `AC-24-03` 无主归口 + 「本机仅 Mono」全库订正

**日期**:2026-10-21
**缘起**:用户指令「S17-2 收掉」
**用户裁定(两轮 AskUserQuestion)**:
- ① **立 `clinic-machine/story-006` 认领 `AC-24-03`,story-003 转 `Complete`**
- ② **多匹配裁决 = 声明序首个匹配 + 登记 `OQ-24-7`**
- ③ **「本机仅 Mono」按 ADR-012 现口径改写 + 全库扫陈旧说法**

---

## 一、S17-2 为何此前是 `review` —— 以及那句归因为什么是错的

`production/sprint-status.yaml` 原 note 写:

> 仍置 `review` 而非 `done` 的理由 = 证据件 §五 第 2 项:「布局 → 房间类型」匹配(`ROOM_TABLE` 求值)
> **整段未实现**,`base_env` 靠调用方喂 ⇒ 求值器上游有实质缺口;另 IL2CPP 三格 NOT-RUN。

**两条理由各自实测:**

| 理由 | 实测 | 判定 |
|---|---|---|
| 房间匹配未实现 | `grep -rn "MatchRoom\|RoomTypeOf\|EvaluateClause" unity/Assets --include=*.cs` = **0** | **事实成立**,但**不在 story-003 的 6 条 AC 面内** |
| IL2CPP 三格 NOT-RUN | 「本机仅 Mono」**为假**(见 §三) | **归因错误** |

**关键发现(本轮新揭)**:`AC-24-03`(「未匹配任何房间类型 ⇒ `(0,0)`」)**此前无任何 story 认领**:

```bash
$ grep -l "AC-24-03" production/epics/clinic-machine/story-*.md
(空)
```

001 表门 / 002 析出 / 003 求值 / 004 交付 / 005 重放 —— **五件全不覆盖它**。

⇒ 真正被「匹配未实现」卡住的是 **`AC-24-03`**,而它是个**无主的 AC**。
story-003 的 `base_env` 由调用方喂(`ClinicContextTable.TryGetBaseEnv`)**是契约形态,不是缺口**。
原 note 把**上游的无主义务**记到了 story-003 账上。

---

## 二、落账

| # | 文件 | 改动 |
|---|---|---|
| 1 | `production/epics/clinic-machine/story-006-room-type-matching.md` | **新建**(Logic · 6 条 AC · 含证伪轮形态表 · Test Evidence 路径 `clinic_room_matcher_test.cs`) |
| 2 | `production/epics/clinic-machine/story-003-modifier-evaluation.md` | `Status: In Review → Complete` · `Completed: — → 2026-10-21` · 「为何置 review」节末**就地订正归因** · IL2CPP 逐格订正 |
| 3 | `production/epics/clinic-machine/EPIC.md` | Stories 表 **+行 006** + 立卡缘由注 |
| 4 | `design/gdd/clinic-machine.md` | 规则二补记**多匹配未定义** · OQ 表 **+`OQ-24-7`** |
| 5 | `production/sprint-status.yaml` | S17-2 `review → done` · `completed: 2026-10-21` · note 追加归因订正 |

### 2.1 story-006 的 AC 面(6 条)

1. `AC-24-03` ROOM_NONE 基例 —— **双向**:空布局(EC-24-01)+ 有家具但谓词全不成立(EC-24-02)
2. 三种子句类型各有判据(`min_count` / `has_type` / `adjacency_min_count`),邻接须有**方向反例**
3. 谓词 = **子句合取**(任一不成立 ⇒ 不匹配,不得「部分匹配」)
4. **多匹配 = 声明序首个**(`OQ-24-7` 暂定)+ 缺口哨兵 `MultiMatchPolicyIsProvisional => true`
5. 匹配是**纯函数**(两次求值逐位相同)
6. 未知类型不抛,按「不成立」处理且**可观测**

### 2.2 `OQ-24-7` 的登记内容

**GDD 规则二原文 `:90`** 只写「未满足**任何**房间类型 ⇒ `ROOM_NONE`」,**没写同时满足多个怎么办**。
不是理论问题:种子表 `clinicmachine_room.json` 的 `ward`(bed ≥ 2)与 `surgery`(手术台 + 邻接无菌柜)
**谓词可同时成立**,而 `base_env` 按 `room_type` 查 ⇒ **选谁直接改变 `EnvMod` 输出值**。

- **暂定裁决**:声明序首个匹配(用户 2026-10-21)
- **备选(本轮否决)**:多匹配 ⇒ `ROOM_NONE`(歧义即拒,与 EC-24-07 同构;但复合布局会静默归零且无诊断)
- **裁定后须**:改实现 + 哨兵转 `false` + 该测转红即提醒(不得静默改绿)

---

## 三、「本机仅 Mono」全库订正

### 3.1 该说法为假的依据

`docs/architecture/adr-012-cross-platform-determinism-ci-gate.md:386-397` **已于 2026-10-17 逐格订正过**
同一句,原文自陈:

> 原句「三格…未跑」**过粗**,且一度被下游转述成「本机仅 Mono」—— **那是假的**。
> 本机装有 IL2CPP 模块,矩阵的两格早已在桌面跑过

### 3.2 本机实测(2026-10-21 复核)

| 项 | 实测 |
|---|---|
| Unity 6000.3.24f1 | `/home/gu/Unity/Hub/Editor/6000.3.24f1` |
| `linux-il2cpp` 模块 | **已装**(`modules.json` 在册) |
| `LinuxStandaloneSupport` | 存在,含 `Variations/linux64_player_{nondevelopment,development}_il2cpp` |
| 最近构建 | `StandaloneLinux64` 成功(10-21 02:15,`exitCode 0`) |
| **arm64 variation** | **不存在**(`Variations/` 只有 `linux64_*`) |

### 3.3 现行逐格状态(写进 42 处加注)

| 格 | 状态 |
|---|---|
| Linux-x64-Mono | ✅ 2026-09-24(编辑器 PlayMode,3/3) |
| **Linux-x64-IL2CPP** | ✅ **2026-09-24**(StandaloneLinux64 player 3/3;与独立 Python 金标准 + Mono 三方 **19/19 逐位相同**) |
| Linux-ARM64-IL2CPP | ❌ **目标不存在** —— 2026-10-17 `BuildTargetProbeCli` 探针:pinned 工具链 46 个 `BuildTarget` 无任何 arm 目标;用户裁定**长期未达** |
| CI 常驻矩阵 | ⚠️ `unity-tests.yml` 的 `il2cpp-determinism` job 已从 TODO 改为真 job,但**从未跑过**(缺 `UNITY_CLIENT_ID`/`UNITY_CLIENT_SECRET` ⇒ 走 skip 分支) |

⇒ **矩阵级验证,非任何单件的个体判据。**

### 3.4 扫除结果

| 项 | 数 |
|---|---|
| 被改文件 | **40** |
| 新增加注 | **42** 处 |
| 原文删除 | **0**(「原文不删,就地加注」;`git diff` 的 51 删除行全部是被替换的同一行) |

**刻意跳过(并说明理由)**:

| 文件 | 理由 |
|---|---|
| `docs/architecture/adr-012-…md` | **真源本身**,其 `:386` 已是订正文本 |
| `production/session-state/active.md` | 会话历史日志,非判据件 |
| `production/session-logs/` | 自动审计日志 |
| `impl-td-audio-01-fix-2026-10-17.md:497` | 该行**是** §十一 的订正文本(「订正对象:…该措辞为假」),不是待订正断言 |

**残余复核**:全库 `本机仅 Mono` 类措辞现 55 行,其中 **42 行带本轮加注**,
余 13 行逐条归因为「已含 2026-10-17 订正」或「真源」—— **零未处理**。

---

## 四、未做 / 未证(禁借绿)

- **story-006 未实现** —— 本轮只**立卡**,零代码。`ClinicRoomMatcher` 不存在,
  `clinic_room_matcher_test.cs` 不存在。story-006 的 6 条 AC **全部未勾**。
- **`OQ-24-7` 未裁** —— 只有暂定裁决,最终规则待用户。
- **未跑任何测试** —— 本轮零代码改动,无新判据可跑。
  最近一次全量 `4291 / 4255 / 0 / 36` @ 2026-10-21 12:38(`s17-5-full-editmode-2.xml`),
  `unity/` 自 `7ff1d37` 起零改动,工作树干净。
- **IL2CPP x64 腿未重跑** —— 本轮无跨平台新增面;引用的是 2026-09-24 的既有实跑证据。
- **`AC-1-33②` 等既有开放项不受影响**。
- **未 commit**(用户未指令)。

---

## 五、本轮的一处自我订正

我在上一轮回答用户「IL2CPP 三格 NOT-RUN 怎么处置」时,**照抄了「本机仅 Mono」这个说法**
并据此给出建议。该说法**在本机为假**,且 ADR-012 早在 2026-10-17 就订正过。
是用户在追问「环境怎么搭」时才暴露的 —— **我本应在引用任何 NOT-RUN 理由前先核实其依据**,
而不是把它当既有结论继承。本轮的 42 处加注即是这次订正的落地。
