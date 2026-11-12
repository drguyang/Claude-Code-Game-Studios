# TD-persist-24 实现件 —— `BodyEdit` 自述契约(甲案)

**日期**: 2026-11-12
**债**: `TD-persist-24` —— `SaveMigrationChain` 的「正文末尾等长」断言是 tail-anchored 启发式
**裁定**: 用户 2026-11-12 选 **甲案**(给 `ISaveMigration` 增「插入多少字节、插在哪」的自述契约)
**回归三元组**: 截至 `2026-11-12T12:39:57Z` · 批次 = TD-persist-24 实现轮 · `43511234` + 工作树

---

## 1 病因(复述,判据见原债行)

链用 `TailBody(current, bodyBefore.Length)` 取「末尾 L 字节」比对正文。当迁移把新段插在**正文中段**时,
该窗口必然横跨插入点 ⇒ **正文一个字节没改也报「改变了事件正文」**。真实存档同样失败,与文件大小无关。
既有五条迁移之所以没撞上,是因为它们全插在**正文开头**(`FixedSize` 处)——但那是**几何上的侥幸**,不是能力。

## 2 契约改动(`unity/Assets/Sim.Codec/ISaveMigration.cs`)

```csharp
public readonly struct BodyEdit
{
    public readonly int At;      // 旧正文中的编辑点
    public readonly int Count;   // > 0 插入 / < 0 删除 / 0 = 未编辑
    public static BodyEdit None => default;
    public bool IsNone => Count == 0;
}

public interface ISaveMigration
{
    uint FromVersion { get; }
    BodyEdit BodyEdit { get; }   // 新增
    byte[] Migrate(byte[] dataWithoutChecksum);
}
```

## 3 链的判据(`SaveMigrationChain.cs` 断言③)

1. **守卫⓪**:`新正文长 − 本步输入正文长` 必须 **等于** `BodyEdit.Count`,否则抛 —— **漏写声明不再静默通过**。
2. `IsNone` ⇒ 整段逐位比(与旧行为逐字节等价)。
3. 否则:编辑点**之前**逐位比 + 编辑点**之后**(跳过本步声明的那一段)逐位比。
4. **滚动基线**:每步结束后 `bodyBefore = bodyNow` —— 多步链的每步各自比自己的输入。
   (首跑即被 `test_migrate_multiStep_runsEveryVersionInOrder` 抓出:拿进链前那份比全程会误报 8 ≠ 4。)

## 4 五条真实迁移的声明(**一处反直觉事实**)

| 迁移 | 声明 | 依据 |
|---|---|---|
| `V2ToV3` | `(0, +4)` | 配置版本段在**定长头部之后** ⇒ 计入正文 |
| `V3ToV4` | **`None`** | 插的 24 B 被**新版本头部定长吸收**(`FixedSizeFor` 36 → 60)⇒ 正文长度不变 |
| `V4ToV5` | **`None`** | 同上(60 → 92) |
| `V5ToV6` | **`None`** | 同上(92 → 100) |
| `V6ToV7` | `(0, +8)` | 分册态段在定长头部之后 ⇒ 计入正文 |

⚠️ **首版写错三处**:我按「插入位置在头部之后 ⇒ 计正文」推断 `V3ToV4/V4ToV5/V5ToV6` 为 `(0,+k)`,
全量 EditMode **当场六红**(「正文长度变化 0 ≠ 声明 24」)。⇒ 声明必须按**长度差的实测几何**写,
不能按「插入点的文件名」推断。三处已改 `None` 并就地留注。

## 5 夹具(`save_migration_chain_test.cs`,12 → 18 条)

| 夹具 | 钉的性质 |
|---|---|
| `test_migrate_midBodyInsertion_declaredEdit_passes` | 中段插入 + 正确声明 ⇒ 放行(**这条就是本债的病**) |
| `test_migrate_midBodyInsertion_undeclaredEdit_throws` | 漏写声明 ⇒ 抛(守卫⓪) |
| `test_migrate_declaredCountMismatch_throws` | 声明数与实际不符 ⇒ 抛 |
| `test_migrate_declaredAtOutOfRange_throws` | 声明点越界 ⇒ 抛 |
| `test_migrate_midBodyDeletion_declaredEdit_passes` | 中段**删除** + 正确声明 ⇒ 放行(覆盖删段) |
| `test_migrate_declaredEdit_stillRejectsTamperingOutsideEdit` | 声明了编辑也**不得**顺手改别的字节 |

同批跟改:`FakeMigration` / `PaddingMigration` 各声明 `(0, +pad)`;`FakeV1ToV2` 声明 `None`。

## 6 回归(实跑)

| 套件 | 读数 | 命令 |
|---|---|---|
| EditMode 全量 | `7483 / 7446 / 0 / 0 / 37` | `Unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults Logs/td-persist-24-full2.xml` |
| PlayMode 全量 | `388 / 383 / 0 / 2 / 3` | 同上,`-testPlatform PlayMode` |

**基线对照(如实)**:会话状态记录的上一基线 = `7453/7416/0/37 @ 2026-11-11 08:38Z`。
本批 **+30 total**,其中**本批新增 = 6 条**(`SaveMigrationChainTest` 12 → 18,`grep -c "public void test_"` 可复算);
其余 +24 由 2026-11-11 之后的其他提交带来,**不归本批**。

## 7 证伪轮(3 条,各自转红;还原 SHA-256 逐字节)

备份 = `~/.claude/jobs/6001633c/tmp/falsify-tdpersist24/SaveMigrationChain.cs.orig`
原/还原 SHA-256 均为 `457a959f66cc5939e3cb108634867ccff6a97b8b2d9344d178e0d77579d140ef`。

| # | 变异 | 转红 |
|---|---|---|
| M1 | `if (edit.IsNone)` → `if (true)`(停用按声明跳过) | 7 红,含 `midBodyInsertion_declaredEdit_passes` / `midBodyDeletion_declaredEdit_passes` |
| M2 | 停用后缀比较(`if (false && …)`) | 2 红:`declaredEdit_stillRejectsTamperingOutsideEdit` / `stepThatChangesBody_throws` |
| M3 | 停用守卫⓪(`if (false && actualDelta != edit.Count)`) | 2 红:`declaredCountMismatch_throws` / `midBodyInsertion_undeclaredEdit_throws` |

还原后 `grep "if (false\|if (true)"` **零残留**。

## 8 边界(不得借绿)

- **判据强度未放松**:编辑段之外的字节仍逐位比(夹具 ⑥ 钉住,变异 M2 证)。
- `StructureIdRemap.cs` 那条「**不得**做成 `ISaveMigration`」的理由**仍然成立** —— 它改的是事件**内容**,不是长度;
  `BodyEdit` 只让链知道「跳过哪一段」,不放松内容判据。
- 本条**不覆盖**「多个迁移各自声明但互相抵消」等形态的额外审计;今天五条迁移的声明均为 `None` / 正文开头插入。
