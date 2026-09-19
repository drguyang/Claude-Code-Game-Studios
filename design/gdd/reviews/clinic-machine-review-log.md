# 医馆即机器(系统 24)评审日志

> 评审史轨道:`design/gdd/reviews/[doc-name]-review-log.md`。每条评审按时间倒序追加。

## Review — 2026-09-17 — Verdict: NEEDS REVISION → **用户裁定 [A] 本轮修订,接受修订并标记 Approved(免二轮验证)**
Scope signal: L(逼近 XL —— 经 `LATTICE_SIZE` 耦合)
Specialists: game-designer · systems-designer · qa-lead · level-designer(×4 报告,均并行)· creative-director(opus 串行综合)
Blocking items: 8 | Recommended: 6(含 2 项 CD 提级为必改)
Summary: 首轮 `/design-review`(`full`)。creative-director(opus)终裁:**机制在架构上是自律的** ——
纯派生层、零状态、ADR-005 / 006 / 015 姿态正确;**8 项阻断全是规格空洞与未加门的关卡,不是设计错误**。
**无重架构**。CD 两项额外必改:① 支柱四的「建造件 = 通关权限的实物」claim 误挂 24(24 只出速率乘子,
无 gate 语义)⇒ 收窄;② Player Fantasy 第二层「整台的流量变了」过承诺(暗示连续运转)⇒
改写为「**改布局 → 立刻给我不同读数**」(可见因果,非连续运转)。

**八项 BLOCKING(2026-09-17 CD 裁定后全部修订落盘)**:
- **B1 多连通块未定义**(`[qa-lead][level-designer][CD]`)—— 规则一让每个连通家具集都是「房间」,
  但 P0 单房间评分从未说 ≥ 2 块时哪个 `S` 喂 F-24-1。**裁定:≥ 2 块 ⇒ 整份布局 = `ROOM_NONE`(0,0)**
  (否决「各块求和」= 偷兑 P1a 的 `ROOM_INTEROP` 跨房间项;否决「只评最大块」= 选块规则无主 + 非单调
  → 加一格悄悄删掉另一房间的分 = 不可见 bug)。全零 = **可修复、可诊断**的失败态。EC-24-07 / AC-24-10。
- **B2 `S` 格分类未定义**(`[CD][level-designer]`)—— 壁 / 地板是否入 `S`?是否入 `type(c)` / 连通?
  敞开空间家具团是不是「房间」?**裁定:`S` 成员仅 `type(c) ∈ FURN_TYPES`**(外壳 / 空地板格 =
  **非格**,不参与连通不计分;隔着墙的家具本就不是 4 邻接,**墙天然隔断**);**敞开空间家具簇仍是合法房间,
  P0 不要求围合**(禁围合检测)。规则一 / EC-24-04 / AC-24-11。
- **B3 `ADJ_TABLE` 对称性无门**(`[systems-designer][qa-lead]`)—— 查法是几何固定的 `w(type(c), type(c+ex))`,
  而「无序对」(`:146`)与有序查法自相矛盾;AC-24-06 原写「由数据保证」= 作者纪律、无门。少一条镜像
  ⇒ 一个朝向静默归零 ⇒ 同一房间的镜像 / 旋转副本**评分不同**。**裁定:烘焙硬门 `∀a,b: w(a,b)==w(b,a)`,
  不等 = 构建失败 `throw`**(承 ADR-022 §三③ 失败通道);补 `|W_MIN| ≤ W_max` 断言(F-24-1 int64 上界前置)。AC-24-06 · `O-24-5`。
- **B4 `ROOM_TABLE` 归属冲突 + 导出缺口**(`[systems-designer][qa-lead][CD]`)—— `:74` 说关卡工具产出
  `world_room.json`;`:242` 说 24 拥有;ADR-022 §三导出契约(`:161-174`)列 7 个 `world_*.json`,**无 `world_room.json`**。
  **CD 裁定:24 拥有** —— 医馆由玩家建造 ⇒ 房间定义不可能是固定世界几何;`ROOM_TABLE` 是评分规则数据
  (`CONTEXT_TABLE` / `ADJ_TABLE` 同族)。**修:删 `world_room.json` 误挂**,改烘 `clinicmachine_room.json`
  (ADR-014 §一命名),**ADR-022 §三不动**。规则二 / registry `ROOM_TABLE`。
- **B5 F-24-4 量纲错误 / 溢出**(`[systems-designer][CD]`)—— `base_env × Π(1+m_j)/65536` 一个 ÷65536 对付
  k 个因子;k≈3 即可溢出 int64。**修:逐因子折叠 `acc = acc × (1+m_j) / 65536`**(P1a 预留,但原式是错的)。
- **B6 int64 / int32 界不健全**(`[systems-designer]`)—— `:165` 界只写 `2n·W_max`,但 `W_MIN < 0`
  ⇒ 最坏是 `2n·|W_MIN|`;写 **`2n·max(|W_MIN|, W_MAX)`**(符号表 `Φ`)。F-24-2 的 `equip_score × 65536`
  在 `equip_score > 32767` 溢出 int32 ⇒ **须在 int64 域求积后再 clamp** + 烘焙断言 `n_max × TIER_MAX < 2^31`。AC-24-02 / AC-24-08。
- **B7 `ROOM_TABLE` 判据须允许邻接子句**(`[CD]` 提级 level-designer 建议)—— 纯计数谓词(床 ≥ 2 ∧ 手术台)
  让空间谜题塌成「过谓词后的爬坡」,「位置即逻辑」在 P0 变装饰性。**用户裁定:谓词语言允许邻接子句**
  (如「手术台 邻接 无菌柜 ≥ 1」),使类型身份与布局互相牵制。规则二 / AC-24-12。
- **B8 AC 卫生**(`[qa-lead][CD]`)—— 拆 AC-24-04(同进程双求值 EditMode **对拍** vs ADR-012 跨平台
  黄金夹具注册,**两者分开**,解绑可选 Memoize);AC-24-05/08 点名失败通道(`throw`);**AC-24-07 改为正面陈述**
  (原负存在断言与文档自身判据纪律 `:298` 自相矛盾 ⇒ 改**程序集引用白名单**);**AC-24-09 改判归属 42**
  (24 的派生是同步的,不含延迟;该 AC 实为呈现层契约);AC-24-01/02 改**性质测试**(对任意 `MIN < MAX` 成立,
  不锁死 `待定` 常量)。

**Recommended(六项,均已落盘)**:
① **支柱四误挂** ⇒ 表头收窄(承 23 / 21a)· ② **`ENV_MOD_MIN < 0` = 承重前提**(若 21a 调到 MIN ≥ 0,
满污染房间钳到 0 = **恰等于 `ROOM_NONE` 恒等值** ⇒ 9 / 21a 无法区分「空 / 中性 / 最差」;登记 `O-24-4`,
**不索值**)· ③ **cap 饱和消解 `ADJ_TABLE`**(`ENV_MOD_MAX − ENV_MOD_MIN` 须大于可实现分数跨度)·
④ **`C_max` 定义为 `max|e_env|`** · ⑤ **AC-24-01/02 的性质测试化**(承 `待定` 常量)· ⑥ **对角规避**
(4 邻接是 ADR-015 钉死的,不可重开;`O-24-3` 要求 42 把格渲染清楚,让角落摆放读作设计而非噪声)。

**专家分歧与裁定**:
- *game-designer* 主张两项 BLOCKING:「EnvMod 是常量 ⇒ 我看得见它运转 不可能」+「单房间 P0 删掉了幻想的房间间内容」。
- *creative-director* **两项皆否决**:常量性是 **ADR-005 无状态纪律的强制后果**(「红石电路在你改线之前也是惰性的」);
  过承诺的是**文本**不是机制。单房间是**用户已裁的范围**(「薄切片」≠「错设计」)。
- **落定:CD 观点成立**(设计健全,文本过承诺)。game-designer 项(1) 降为非阻断;项(2) 变文本修正;
  `ROOM_NONE` 保持 `(0,0)`,但 `ENV_MOD_MIN < 0` 前提升为承重。

**四项用户裁定(2026-09-17,均照准并落盘)**:① 多连通块 = 全零 · ② `S` = 仅家具格 · ③ 谓词允许邻接子句 ·
④ `ROOM_TABLE` 归 24 拥有。
**涟漪**:`entities.yaml`(`ROOM_TABLE` 归属改 24 + 删 `world_room.json` 误挂;`ADJ_TABLE` 对称升门 +
`|W_MIN| ≤ W_max` 断言;`env_mod` 的 `Φ := max(|W_MIN|, W_max)`;`equip_mod` 补 int64 求积 + `n_max × TIER_MAX < 2^31`)·
`item-database.md`(`O-24-1` / `O-24-4` 回填)· `skeuomorphic-ui.md`(`O-24-3` 反向引用 + AC-24-09 迁入)·
`world-and-ecozones.md`(`O-24-2` 回填)· `systems-index.md`(row 24 / §10 / §11)。
**未结**:`O-24-1`…`O-24-5` 五项登记义务待下游撰写时兑现;`OQ-24-1…6` 六个定值待用户。
Engine Knowledge Risk: **LOW**(纯 C# 判据面;无 post-cutoff API)。
Prior verdict resolved: First review

## 结案 — 2026-09-17 — Verdict: **APPROVED**(用户裁定接受修订,免二轮验证)

**收尾核对**(2026-09-17,本轮):B1–B8 八项 BLOCKING 与四项用户裁定逐条在 `clinic-machine.md` 中
验证落盘 —— EC-24-07 / AC-24-10(多连通块)· 规则一格分类 / EC-24-04 / AC-24-11(仅家具格)·
公共定义对称门 / AC-24-06 / `O-24-5`(`ADJ_TABLE` 硬门)· 规则二 / 表数据归属(删 `world_room.json`,
烘 `clinicmachine_room.json`)· F-24-4 逐因子折叠 · `Φ := max(|W_MIN|,W_max)` + `n_max × TIER_MAX < 2^31`
(int64/int32 界)· 规则二判据语言 / AC-24-12(邻接子句)· AC 卫生(拆 AC-24-04 · 点名 `throw` ·
AC-24-07 正面白名单 · AC-24-09 迁 42)。`entities.yaml` 侧 `ROOM_TABLE` 归属改 24 + `ADJ_TABLE` 升门已核。
**计数据实测与四处口径一致**:五规则 · 3 公式(F-24-1/2/3)+ P1a F-24-4 预留 · **7 边例** ·
**12 条 AC(11 BLOCKING + 1 ADVISORY)** · `O-24-1…5` · `OQ-24-1…6` · 8 必备节齐备。

**结案前补修两项(B8/CD 遗漏)**:① **Player Fantasy 层 2 文本**仍为旧措辞「于是整台的流量变了」
(CD 明列必改 —— 暗示连续运转,与 ADR-005 无状态纪律冲突)⇒ 改为「**改布局 → 立刻给我不同读数**」
(可见因果,非连续运转;P0 兑现分层行同步);② **`:122` 悬空引用 `AC-24-xx`** ⇒ 订正为 `AC-24-04`
(全库 grep 零残留)。

**涟漪**:`clinic-machine.md` 文件头 → **Approved** + 评审史指针 · `systems-index.md` 行 24(§1 表 +
设计序矩阵)+ §11 队列行三处 → **✅ Approved** · 本日志。
**未结(不阻塞 Approved)**:`O-24-1…5` 五项登记义务待下游撰写时兑现;`OQ-24-1…6` 六个定值待用户
(K_CONTEXT_MAX / TIER_MAX / EQUIP_MOD_CAP / ENV_MOD_MIN~MAX / `n_max` / `W_MIN`·`W_MAX`·`C_max`)。
**下一系统** = 27 敌人 AI(`systems-index.md` §11 队列头)。
