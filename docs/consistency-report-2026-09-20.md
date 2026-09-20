## Consistency Check Report
Date: 2026-09-20
Registry entries checked: 0 entities, 0 items, 25 formulas, 94 constants(检查时点;修后 3/2/25/94)
Scope: full(承 `/review-all-gdds` 2026-09-20 报告 §方法 待办:「本评审结案后跑 /consistency-check 回填注册表」)
GDDs scanned: 31 系统件 + 4 非系统件(game-concept / systems-index / content/campaign-arc / content/he-returns)+ 评审报告

---

### Conflicts Found(🔴 两类,均已修复)

🔴 **C-1 · 帐篷新建裁定未落地 23(modular-building)**
   - 事实源:`game-concept.md:697-698`(2026-09-20 用户改判注 = 权威);
   - 冲突侧:`modular-building.md:24/:25/:46/:226` 四处仍写「不允许新建」**且全文零「帐篷」**
     —— 拥有建造系统的 GDD 不知道 P0 已有一次新建。按 23 实现 = 序章搭帐篷做不出来。
   - **修复**:`:695→:697` 行号订正 + 规则六补「P0 唯一例外 = 帐篷」条款
     (`world_buildslots.json` 预置 `tent` 槽,`StructurePlaced` 载荷形状不变,不破「不改骨架」)。
   - **追加裁定(用户,本轮)**:「**P0 只作为测试,也要能新建;后续完整游戏能像《英灵神殿》做世界建设**」
     —— 已写入 23 规则六注块、`game-concept.md:698` 注、`systems-index.md` §11 与评审报告行 3。
     帐篷由此升格为**「P0 证明玩家能改变世界几何」的能力证明**(与「先切承重的未知」原则同构),
     不再只是剧情道具。

🔴 **C-2 · 采集闭集「三种」陈旧复述 ×5(foraging 内部自相矛盾 + 下游四件未刷)**
   - 事实源:`foraging.md:121`(规则一)+ `AC-17-01b`(`:628`)= **四者**(2026-09-20 止血草扩容裁定);
   - 冲突侧:`foraging.md:47`、`foraging.md:223`(规则五,**同文件内**仍列三种 ⇒ 文档自身不一致)、
     `skill-system.md:149/:155`、`systems-index.md:53`(「闭集,3 种」)、`game-concept.md:109`。
   - **修复**:五处枚举全补「止血草」并注「2026-09-20 扩容」;`game-concept.md:109` 以**追加注**形式
     (原注体标 2026-09-13,不改历史文本);skill-system 处带「AC-17-01b 在 21a 行落地前不得记绿」警示。
   - 草木灰**不涉本冲突**(非采集对象,来源 = 18;`foraging.md:122` 已明写)。

---

### Stale / 缺陷(⚠️ 三类,均已修复)

⚠️ **S-1 · 注册表缺陷:`ROOM_TABLE` 条目重复键**
   `added/revised` 各出现两次(YAML 同键覆盖 ⇒ 真实修订记录「归属改 24」被后面的空值吃掉)。
   已删重复对,保留带修订值的那对。

⚠️ **S-2 · game-concept.md 行号漂移(2026-09-20 批插入 :460-461 / :698 共 3 行所致;本轮修 33 处)**
   漂移规则::460 之后内容 +2,:698 之后再 +1。逐条**按内容锚实核**(非机械 +N)后订正:
   - `:693→:695`(点名两个急救动作)× 3 处(input-system);
   - `:695→:697`(不允许新建)× 6 处(modular-building / systems-index / 评审报告;campaign-arc 的 :695 实指动作行,内容锚正确,保留);
   - `:713→:721` `:706→:709`(input-system);
   - `:718/:720→:721`(P0 范围阶梯行)× 8、`:719→:722`(P1a 行)× 3、`:720→:723`(VR=P1b)× 8;
   - `:715→:721`(world-eco × 3)、`:716→:719`(catalog / index × 5)、`:710-717 / 713-717→712-717`(× 4);
   - `:466-472→468-474` `:479→481`(he-returns × 4)、`:791→794` `:776→779`(enemy-ai / skeuomorphic-ui × 5)。
   未修 :876 —— 超出文件现长(795 行),属**既存坏号**(非本轮漂移),移交下轮按内容锚另找真身。
   **结构性教训**:行号引用对上游编辑天然脆弱;新增注应插在被引段**之后**,或用内容锚。

⚠️ **S-3 · 注册表缺账(跨系统事实未登记)**
   - `LeaseSource {Self, Emergency, Combat}`:定义在 1,读者 4;语义全链核一致、零冲突,但注册表无此项 ⇒ 已补。
   - 三案角色(甲 · 教书先生 / 乙 · 挑夫 / 丙 · 守夜老人):具名事实活在 4 件里,注册表 entities 为空 ⇒ 已补。

ℹ️ **he-returns §四表行 3「→ PatternRecognized」与 W-1 矛盾**
   W-1 裁定(2026-09-20 显式风险接受)只在 `case-system.md:612` 记了账,机制正典本尊未带警示 ⇒
   已在行 3 就地补 W-1 注(叙事落点不依赖 PatternRecognized;复诊走 52 脚本条目)。

✅ **Clean(抽查全一致)**:23 个 SimEvent.Kind(含应急三 Kind)/ `PATTERN_THRESHOLD=3` /
`INJ_HEMORRHAGE` 定义(9:372「10 止血的对象」与三案表口径一致)/ EFF 族常量(item-database 自洽)/
`SAFETY_MARGIN>1` / 三案名四件逐字一致 / LeaseSource 语义 1/4 两件一致。

**Verdict:CONFLICTS FOUND → 已按用户授权 A+B+C+D 全部修复**(见下)。
残留(有意不修,登记):`AC-17-01b` 与两条 items 行的 21a 数据行 = 实现轮;W-1 会签 = 实现前;
止血草史实锚点 = 40;OQ-10-4 / OQ-8 / OQ-25-8 不代拍。

---

### 用户授权与本轮写操作对照

| 授权 | 写入 |
|---|---|
| A(修 🔴 C-1/C-2) | modular-building ×4 + 规则六例外条款与追加裁定注 · foraging ×2 · skill-system ×2 · game-concept:109 · systems-index:53 |
| B(⚠️ 行号 + 注册表缺陷) | 33 处 game-concept 行号订正(9 件)· ROOM_TABLE 重复键删除 |
| C(补注册表) | constants += LeaseSource;entities += 三案 ×3 |
| D(items 21b 例外,用户明示) | items += 止血草 / 草木灰(标「数据行归实现轮,不造数」) |
| 规定动作 | 本报告 · 反思日志 2 条 · active.md 注 · he-returns W-1 注(随 A 类一致性) |
