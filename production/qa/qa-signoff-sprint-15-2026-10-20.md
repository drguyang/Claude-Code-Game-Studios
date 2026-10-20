# QA Sign-Off Report: sprint-15

**Date**: 2026-10-20
**Sprint**: sprint-15 — epic 44 的 P0 收口面(`2026-10-19` 起,不设结束日期)
**Stage**: Pre-Production
**Review Mode**: `lean`(Director gates 按 `production/review-mode.txt`)
**依据**: `qa-plan-sprint-15-2026-10-18.md` · `smoke-2026-10-20.md`(§1 中途闸 + §7 收口轮终值)

---

## 0. 本件的判读边界(先读)

1. **本件签的是 sprint-15 这一批交付**,不是 epic 44 的收口。epic 44 仍有
   `TD-audio-101/102/103/104/105/107/108` 及素材录制在途,**不得**因本件而读作 44 可收口。
2. **两件听测已按用户裁定移出本 sprint**(S15-6 与 S15-1 的面 10-c),
   合并登记为 `docs/tech-debt-register.md` **`TD-listen-01`**。
   ⚠️ **移出不等于通过** —— 受影响的 6 条 AC 的听测半边**一条都未验证**,一格格子未勾。
   本件的 APPROVED **不覆盖**这些 AC。
3. **本件的测试数值全部带三元组**(截至时刻 · 批次 · commit),无裸数。

---

## 1. Test Coverage Summary

| 件 | Story / 债行 | Type | Auto Test | Manual QA | Result |
|---|---|---|---|---|---|
| **S15-1** | story-019(`AC-44-10` 10-a/10-b) | Integration | **PASS** | 10-c **已移出** | **PASS**(门 + 断言面) |
| **S15-2** | story-020(`TD-audio-030`) | Integration | **PASS** | — | **PASS** |
| **S15-3** | story-021(`TD-audio-033`) | Integration | **PASS** | — | **PASS** |
| **S15-4** | `TD-audio-028` 空间化接线 | Integration | **PASS** | 听感归 `TD-audio-101` | **PASS** |
| **S15-5** | `TD-audio-029`+`-032` 卫生批 | Logic | **PASS** | — | **PASS** |
| **S15-6** | `TD-audio-101`+`-102` 听测 | Visual/Feel | 无(不可自动化) | **⛔ 已移出** | **N/A(移出)** |
| **S15-7** | 债册刷账批 | Config/Data | 逐条对码 | — | **PASS** |
| **S15-8** | `/retrospective sprint-14` | Config/Data | 产出物存在 | — | **PASS** |

**Summary**:**7 PASS · 1 N/A(移出)· 0 FAIL**。

> **S15-6 为何是 N/A 而不是 FAIL**:它的判据在今日代码里**不可达**(无病人离场路径 /
> 听诊档位零回写 / 病程被压缩到 1.55 s / 同屏凑不齐活人),用户裁定移出。
> 标 FAIL 会暗示「跑过但没过」,与事实不符;标 PASS 就是借绿。**N/A 是唯一诚实的格子。**

---

## 2. Automated Tests(收口轮终值)

**基线溯源三元组**:截至时刻 `2026-10-20 01:09 UTC` · 批次 `sprint-15 收口轮 · 全量` · commit `3f6c084`(工作树含本轮改动)

| 套件 | 退出码 | XML 产物 | 结果 |
|---|---|---|---|
| EditMode | **0** | `unity/Logs/s15-fix-editmode.xml` | **`3945 / 3909 / 0 / 36`** |
| PlayMode | **0** | `unity/Logs/s15-fix2-camerarig-playmode.xml` | **`197 / 197 / 0 / 0`** |

**对中途闸(`smoke-2026-10-20.md` §1,`2026-10-20 01:11 CST` · `d4e5df3`)的差**:

```
EditMode  total 3928 → 3945 (+17)  passed 3892 → 3909 (+17)  failed 0 → 0  skipped 36 → 36
PlayMode  total 197  → 197  ( 0)  passed 197  → 197  ( 0)  failed 0 → 0
```

⇒ **failed 与 skipped 均未变,零回归**;`+17` = 中途闸之后新落的夹具数。

### 2.1 ⚠️ 收口首跑实测出 7 红 —— 已修,两处修复各被反证钉住

首跑 PlayMode = **`197 / 190 / 7 / 0`**,7 条红同一条 `AC-20-10 AudioListener 单挂点`,
`active` 计数逐测递增(2→3→…→7)。

| # | 缺陷 | 反证 |
|---|---|---|
| **A** | `CameraRig` 为根级对象、`BootRoot.OnDestroy` 从不销毁 ⇒ 每测试残留一个带 listener 的相机 | **M1**:删 `SetParent` ⇒ **7 红**(与事故同型) |
| **B** | 预摆相机摘 listener 用 `GameObject.Find`(只找 active)⇒ 预摆 inactive 时摘不掉 | **M2**:改回 `Find` ⇒ **1 红**(`active 1 / 总数 2`) |

还原 SHA 一致(`c5397e6b6351df41`)。原件:
`production/qa/evidence/fix-camerarig-listener-leak-2026-10-20.md`。

> **这是本轮修复「零听音点」时引入/暴露的回归,已如实登记,不吞。** 它同时是本 sprint
> 第二次实证「**单件全绿 ≠ 全量全绿**」—— 中途闸之前 4 条定向件单跑全绿,对跨测试累积零判别力。

---

## 3. Manual QA Results

`qa-plan-sprint-15-2026-10-18.md` §Smoke Test Scope 7 条:

| # | 检查项 | 结果 |
|---|---|---|
| 1 | 编辑器进 Play 无控制台错误,场景加载完成 | `NOT RUN` |
| 2 | 病人出现 ⇒ 呼吸层可闻 | `NOT RUN` |
| 3 | 病人咳嗽 ⇒ 可闻且 12 变体交替 | `NOT RUN` |
| 4 | 病人死亡 ⇒ 完全无声 | `NOT RUN` |
| 5 | 病人昏迷 ⇒ 呼吸层在且进入尾音衰减 | `NOT RUN` |
| 6 | 拉 `Voice` 总线滑块 ⇒ 音量随之变化 | `NOT RUN` |
| 7 | 连续 30 秒无事发生 ⇒ 零状态播报音 | `NOT RUN` |

**用户本轮陈述(逐字,不加工)**:`音频人工检查未做 —— 未验证`

> **为何 `NOT RUN` 不算本 sprint 的 DoD 缺口**:第 2–7 条实质都是**听测**,
> 而两件听测任务已按用户裁定移出(归 `TD-listen-01`)⇒ 本 sprint 的交付件里
> **没有任何一条**依赖人工实听。
> ⚠️ 但它**也不是已通过** —— 这 7 条在 L-1…L-5 解除前**不可能**通过,不得被引作任何 AC 的证据。

**「主菜单」两条不适用**(对象不存在,非未跑):仓内无主菜单,`EditorBuildSettings` 只登记
`Boot.unity`;`u0-assembly-checklist.md:412` 登记 `MainMenu.unity` 仍未建(ADR-023 §① 允许增量)。
口径同 `smoke-2026-10-20.md` §3.1。

---

## 4. Bugs Found

| ID | 件 | 严重度 | 状态 |
|---|---|---|---|
| (无独立 bug 件) | — | — | — |

**说明**:本轮唯一发现的缺陷组(AudioListener 单挂点 7 红)属**本轮引入且在收口前已修**,
不是交付物带出的缺陷 ⇒ 按 `bug report` 流程立件的意义有限(它没有「留给后续处理」的内容)。
已按**修复原件**形态落盘(`fix-camerarig-listener-leak-2026-10-20.md`),含原判定 → 修复落点 → 验证命令。
⚠️ 若用户要求按 bug 件格式补立,可另开 —— 本件不代替。

---

## 5. 各件的「已证」口径核对(禁借绿)

本 sprint 的 DoD 要求「**已证 = 变异测试**,不是『测试名 + 行号』」。逐件核对:

| 件 | 证伪记录落点 | 是否齐 |
|---|---|---|
| S15-1 | `impl-s19-ac44-10-breath-tail-2026-10-18.md` §三(F1–F9)+ 修复轮 F1–F4;补评 W-1 修复见 `impl-b-audio-tier-wiring-2026-10-20.md` | ✅ |
| S15-2 | `impl-s20-voice-variant-selection-2026-10-18.md` | ✅ |
| S15-3 | `impl-s21-knowsclinic-seam-2026-10-18.md` | ✅ |
| S15-4 | `impl-s15-4-spatialization-wiring-2026-10-18.md` | ✅ |
| S15-5 | `impl-s15-5-audio-hygiene-2026-10-18.md`(F1–F10) | ✅ |
| S15-7 | `impl-s15-7-debt-ledger-reconcile-2026-10-18.md`(逐条对码,零代码改动) | ✅ |
| S15-8 | 产出物 `retro-sprint-14-2026-10-18.md`(19938 B)存在且非空 | ✅ |

**评审原件要求(BLOCKING 级)**:S15-1 有 `review-s19-ac44-10-2026-10-20.md`
(**补做** —— 评的是补做时点代码,**不追认** 2026-10-18 的任何判定,该声明已在原件与 story 内落)。
S15-2 / S15-3 各有 `review-s20-*` / `review-s21-*` 原件。✅

---

## 6. 未解面(本签核明确不覆盖)

| 项 | 状态 | 归属 |
|---|---|---|
| `AC-019-6`(10-c 呼吸过渡听测) | **NOT-RUN** | `TD-listen-01`(L-1 / L-3) |
| `AC-44-02` 三档呼吸可辨 · `AC-44-08` 啰音 | **NOT-RUN** | `TD-listen-01`(L-2 尤其致命:听诊档位恒 0) |
| `AC-44-03` / `AC-44-04` 语声变体 | **NOT-RUN** | `TD-listen-01`(L-4) |
| `AC-44-17` 主通道双盲 | **NOT-RUN** | `TD-audio-103`(被试 ≥2 人,项目外部) |
| `TD-audio-101/102` 空间化听感 · SNR | **NOT-RUN** | `TD-listen-01` |
| `TD-audio-032` ③ `BuildAudioSeams` >40 行 · `isPlaying` 时序 PlayMode | **open** | 未解面如实保留 |
| `TD-audio-035` 残留三项 | **open** | 同上 |
| IL2CPP 三格(ADR-012 矩阵) | **NOT-RUN** | 本机仅 Mono;CI `UNITY_LICENSE` 未配 |
| 测试侧 `GameObject.Find("Main Camera")` 死代码 | **未改** | 生产侧已不依赖它;归测试基建清理轮 |
| **epic 44 收口** | **不得** | 本件只签 sprint-15 这一批 |

---

## 7. Verdict: **APPROVED WITH CONDITIONS**

**逐条对规则**:

- **非 NOT APPROVED**:无 S1/S2 级未修缺陷(收口前发现的 7 红**已修 + 双反证**,且属本轮引入)。
- **非 APPROVED**:存在下列条件项,且本 sprint 的人工面 7 条 `NOT RUN`。

### Conditions(须在下一次触碰相关对象前完成,不阻塞 sprint-15 收口)

| # | 条件 | 为什么不能算已满足 |
|---|---|---|
| **C-1** | `TD-listen-01` 的 L-1 / L-2 属**纯接线**,不依赖数值轮 —— 应在下一个音频 sprint 优先解除 | 六个 AC 的听测半边至今**一条未验证**;不解除则永远无法判这些 AC |
| **C-2** | L-3 / L-4 依赖 `disease_registry.json` 真表(数值轮) | 占位 plateau 曲线不承载听测;此条件**只能由用户**推进 |
| **C-3** | 测试侧 `camera_follow_wiring_test` 的 `GameObject.Find("Main Camera")` 恢复逻辑是死代码,应改含 inactive 的查找 | 生产侧已改为不依赖它,但该测试的**意图**仍未兑现;改动须重跑全量 PlayMode 确认无连带 |
| **C-4** | `TD-audio-032` ③(`BuildAudioSeams` 仍 >40 行)与 `isPlaying` 时序 PlayMode 半边 | 收口时如实保留为未解面,不得因 S15-5 done 而读作已清 |

### 关于「人工冒烟 NOT RUN」为何不升级为条件

本 sprint 的 8 件交付里,**没有一件的验收依赖人工实听**(两件听测已移出)。
7 条冒烟里第 2–7 条实质都是听测,其前置(L-1…L-5)未解 ⇒ **现在跑也跑不出有效结论**。
强行要求它,等于要求「在不可达的判据上勾格子」—— 那正是本 sprint 一直在防的借绿。
故:登记为 `NOT RUN`,归 L-1…L-5 解除后重跑,**不设为本 sprint 的收口门**。

---

## 8. Next Step

- **sprint-15 可收口**(本件 = 其 DoD 的 QA sign-off 条)。
- 收口时:补 `end` 日期;`production/sprint-status.yaml` 各件 `done` 已核;**S15-6 记 `removed` 而非 `done`**。
- 下一个音频 sprint 优先 **C-1**(L-1 / L-2 纯接线),再谈听测。
- `/gate-check` 的适用性是**项目阶段门**(Pre-Production → Production),**不是** sprint 收口门 ——
  本件不触发它。

**原件留痕**:本件的判定基于 `smoke-2026-10-20.md` §1+§7、
`fix-camerarig-listener-leak-2026-10-20.md`、以及上表列出的 7 份 `impl-*` 证伪记录。
