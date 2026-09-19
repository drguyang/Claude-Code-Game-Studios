# 格斗与武器线(系统 25)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间顺序追加。

## Review — 2026-09-18 — Verdict: NEEDS REVISION → **用户裁定接受修订、免三审 → ✅ Approved(结案)**
Scope signal: L
Specialists: systems-designer · qa-lead · game-designer · ai-programmer · unity-specialist(补救交付)· gameplay-programmer · audio-director(×7 报告,并行)· creative-director(串行综合,终裁)
Blocking items: 13 | Recommended: 8
Summary: 二轮 `/design-review`(`full`,新会话独立于首轮撰写上下文)。creative-director 终裁:**首轮是结构性伤(边界裁决→跨系统契约形状),本轮是契约空洞 + 记账漂移 + 三处文档内自相矛盾,零回机制层重裁**。**终审四改判**(vs 主评审):qa[1] 判不成立(§九 27 面全映射,方向反了 —— 是 V-02/7-01b/7-02b 三条 `[B]` 不映射 A 面)· E4 锚点从 `AC-25-V-03` 改 `V-01`(D-12 未裁)· C1 修法 4-04 式不适用(占用门问题 = 实现者缓存,非 Kind 注册,正解 = 行为不变性 + `S-1` 无队列字段反射断言)· F2 措辞冲突但够 BLOCKING(:44 入口文字会误导实现者做通用血条)。**13 BLOCKING 全落**:B-1 `Attack` 动作回填 3(R19b)· B-2 `Down()` 升正式定义 + sim 程序集归属 + AC-5c · B-3 `IsCombatant` 真值表 · B-4 音强互斥削平([甲] 改绑事件类别,三处禁令不动)· B-5 V4 补「昏迷后静默」半边(`EncounterEnded` 承载)· B-6 :162 客户端口径 + T2 第四态(丢包 ≠ 改判)· B-7 昏迷「继续压制」矛盾消除 · B-8 E6 解绑(6-08 改挂 OQ-25-1)· B-9 F-25-8(b) 跨线缝 + **A27** + AC-25-6-12 · B-10 A4 假映射拆 **AC-25-2-10** · B-11 AC-25-0-04 空断言改判重写 · B-12 T2 第四态 + V-01 前置门 · B-13 G1 重述为可听层 + `MUSIC_XFADE_MIN_MS`。**登记进裁决堆(冻结纪律)**:D-14 ✅已裁[甲](折叠为饱和累计量,R19③)· D-15(对角可击)· D-16(压制兑付)· §九·8(承诺 2 载体镜像)· G5(不补独立 ADR)。**三件二轮专项核对全过**(O-27-3 口径 / 9 侧 F1·R11·F4·R12·R17·R18 实核落盘 / 27 provisional 契约差已消)+ 两处记账残留划结(`player-controller` O-1 ×2 + OQ-1-8)。**跨文档涟漪**:skill-system 两表标「P1a 生效/⚠️ P0 不生效」· audio-system 音频触发订正注 · adr-018 G1 重述 + G3 补半边 · disease-simulation R19a/b(R19③)/c + AC-5c · input-system `Attack` 行。**计数面**:AC 76→78 · A 断言 26→27 · [B] 28→30 · 25 文件 2248→2317 行。
**重开触发条件(免三审 = 显式风险接受)**:① D-14 折叠实现被证伪;② 30 修订后 A16/A21(6-02/6-10)复验不过;③ `Attack` 动作在 3 侧被改义;④ `SWITCH_COOLDOWN` 上界(AC-25-6-12)数值轮拍出后 A13(b) 复验不过。
Prior verdict resolved: Yes(首轮 MAJOR REVISION NEEDED → 本轮 NEEDS REVISION → Approved)

## Review — 2026-09-17 — Verdict: MAJOR REVISION NEEDED → **用户裁定 [A] 当日全量修订落盘 → 修订完成,待二轮评审(未结案)**
Scope signal: L
Specialists: systems-designer · qa-lead · game-designer · ai-programmer · unity-specialist · gameplay-programmer · audio-director(×7 报告,并行)· creative-director(串行综合,终裁)
Blocking items: 15 | Recommended: 21
Summary: 首轮 `/design-review`(`full`)裁 **MAJOR REVISION NEEDED · Scope L**。creative-director 终裁的核心形状:25 的**边界裁决全部站得住**(非致命模型 · sim 整数判定 · 反支柱三件标注禁令 · 击退/压制的确定性边界),坏在**跨系统契约以「引用」形式写、而以「假设」形式落** —— 多处把 9 / 1 / 21a / 27 侧**尚未存在的字面**当成了已定事实(「引用而未登记」失败模式,同 ADR-009 §三 谱系)。用户裁定 **[A] 现在就修订 + 全量落盘 + 机制与数值冻结**(即:修订只动口径与归属,不改任何已定机制、不新增数值;需要数值处一律登记为 OQ,不代用户拍值)。

**四项 fork 裁定(均 [甲],AskUserQuestion)**:
- **A3** 压制对外形态 = **只读查询 `IsSuppressed(actor_id)`**(非事件、非 DTO 字段)⇒ 禁轮询条款 + 1 侧 `AC-1-23` 负边界(压制止于水平位移 + Jump;被压制者仍可出手)。
- **D2** 敌/兽伤害 = **`CP:=0` 退化式**(伤害 = MAG_FLOOR + base_step[act],不引入 enemy_cp 旋钮)⇒ `AC-25-2-09` 敌伤 CP 不变性。
- **B2** 冷却上界 = **逐动作 + 逐 bar 组合上界断言**(F-25-8 乘法形式判据,零除法路径;A26 = 4-ulp 带)。
- **范围** = 全量落盘(R1–R18 + V1–V7 涟漪一次性回填,不留尾巴给实现期)。

**两处跨系统改判(本轮最重要的两个归属修正)**:
- **S2** `HurtLevel` 档位折叠**归 9**(非 25 载荷携带严重度枚举)⇒ 9 侧落 `QueryHurtLevel(actor_id)` 公开查询 + band 阈值 = 注册表数据(数值归 `OQ-25-7`);27 的读法从「直读 25 事件」改为「读 9 查询」—— **27 的 provisional 契约(PR-27-2)由此结清半边,`TR-enemy-017` ⚠️ → ✅**。
- **S3** 击退**不注入位移**(纯表现,回弹动画)⇒ 结清 1 的 `OQ-1-7`,`O-3` 的 25 一行撤销(余 29/45)。

**R1–R18 + V1–V7 涟漪落盘清单**(全部为口径/归属级,机制与数值零改动):
`entities.yaml`(R2 三 Kind:`InjuryOnset` 病史流 / `EnemyInjuryOnset` 世界流 / `InjuryStateChanged` 世界流写者=9)· `adr-009`(R3 §二 路由枚举 + §三 骨架,第五、六个追加 Kind)· `disease-simulation.md`(R1 邻居表口径 · R9/R11 F1 战伤阶跃项 · R12 `LethalFor` · R17 折叠+查询 · R18 emit 时机 · V1/V3 六通道 + AC-21 重写)· `enemy-ai.md`(R4/R6/R7/R15 §一① 订正 + 依赖措辞「有入向意图通道,无出向数值通道」 · V2/V7 订阅登记)· `patient-ai.md`(V2 表现映射侧订阅行)· `audio-system.md` + `adr-018`(V4/V7 乐层 = 白名单第 5 类,G1 禁帧对齐 / G2 去标注盲测 / G3 本地设备侧触发**全文**入 ADR)· `player-controller-and-movement.md`(R8/R14 `AC-1-23` 负边界 + `OQ-1-7` 结案)· `item-database.md`(R13 `inflicts_injury` 降级为集合约束 A20 + 漂移门双 fixture)· `technical-preferences.md` + `adr-005`(R16 `OQ-25-8` tick 频率与 `OQ-8` 同批标定)· `production/known-costs.md`(V5/V6 三件套放弃 + whiplash 不可归零 = 已知代价,评估期不得当 bug 报)· `tr-registry.yaml`(`TR-combat-001…024` 24 条,18 ✅ / 4 ⚠️ / 2 ❌;翻转 `TR-enemy-017` → covered · `TR-skill-007` gap → partial)· `traceability-index.md`(§14 + 汇总 283 条 + 变更历史)· `systems-index.md`(row 25 → 🟡 修订完成待二轮;§10 进度 18/31;27 行的 🔴 前置划结)。

**未闭合(全部归用户,刻意不代裁)**:`OQ-25-1`(玩家可被致死伤的形态,路甲/丙)· `OQ-25-3` + `D-13`(Down 覆盖边界含苏醒态)· `OQ-25-7`(数值旋钮轮:PS 阈值 / magnitude 带 / cooldown / trauma_half_life / HurtLevel band)· `OQ-25-8`(tick 频率标定)· `D-4`(cell_size ↔ 触及距离比例)· `D-6`(蛇咬形态医学考据)· `D-7`(受击反应变体数)· `D-10`(昏迷后交互呈现)· `D-12`(动作表动画时序字段)。

**⚠️ 本轮所有状态变更由撰写方(修订执行方)置入,未经独立确认 —— 二轮 `/design-review` 须新会话**(本会话已撰写并修订 25,自查不宜)。已知残留(留二轮核对):`player-controller-and-movement.md` §Dependencies 的 `O-1` 行 ~:1097 处 25 半边未划结(仅 §二 权威账本已更新);`OQ-1-8` 提及击退处未加注。

Prior verdict resolved: First review
