# V-10 敌人读数条 · 实现证据(「完整可见」轮)

**日期**: 2026-11-14
**故事**: `production/epics/skeuomorphic-ui/story-017-enemy-vitals-bar.md`(V-10)
**批**: 序章最小战斗闭环 → 敌人可见实体
**权威**: GDD `enemy-ai.md` 规则二十二(2026-11-14 裁定)· GDD `skeuomorphic-ui.md` V-10 ·
ADR-013 §9 C3 · ADR-005 抽象点之四 · ADR-016 §二

> ⚠️ **本文只登记「跑了什么、绿了什么」**。**没有**截图 —— 占位资产下的走查屏摄
> 留给人工轮(见 §5 未覆盖面)。

---

## 1 交付物

| 层 | 文件 | 作用 |
|---|---|---|
| Sim | `Assets/Sim/Enemies/EnemyRoster.cs`(既有) | 纯投影:遭遇成员 / 伤量 / 倒地 —— 本轮的取数面 |
| 边界 | `Assets/Gameplay.Boot/EnemyVitalsProjection.cs` | 伤量 → `VitalsDto`(Position 饱和 [0,1])+ 九态档位(透传 9 的 `StateMachine.ComputeTransition`);`EnemyVitalsQuery` 对未知 id **fail-loud**。**落点 = 边界侧而非 `Assets/Sim`**(见 §2.6) |
| Boot | `Assets/Gameplay.Boot/CompositeVitalsQuery.cs` | 复合 `IVitalsQuery`:敌人 id 走 9 的同形投影,其余走病人 `DiseaseVitalsService`;两路都抛 ⇒ 原样上抛 |
| 表现 | `Assets/Gameplay.Presentation/Enemies/EnemyPose.cs` | 每帧值快照 + `IEnemyVisualFactory` 接缝(零 MonoBehaviour) |
| 表现 | `Assets/Gameplay.Presentation/Enemies/ComposedEnemyVisualFactory.cs` | 人形 / 野兽组合体(primitive + 共享占位材质);倒置外壳 `_Cull = Front` 作墨色轮廓 |
| 表现 | `Assets/Gameplay.Presentation/Enemies/EnemyPoseRig.cs` | 六态肢体姿态、朝向、淡入缩放、倒地覆盖、可取材微光(只写 Transform / Renderer) |
| 42 | `Assets/Gameplay.UI/Skeuomorphic/EnemyVitalsBillboard.cs` | world-space 黄铜面片:九格蚀刻槽 + 档位词 + 倒地压暗 + 恒定屏幕尺寸 |
| 42 | `Assets/Gameplay.UI/Skeuomorphic/EnemyVitalsBar.cs` | 读数条门面(可见 / 六态 / 读数 / 朝相机 / 销毁) |
| 装配 | `Assets/Gameplay.Boot/EnemyVisualSpawner.cs` | 每 tick 边沿重投影 `EnemyRoster`,增删视觉 + 面片;`DestroyAll` 连根清并释放 factory |
| 装配 | `BootRoot.cs` / `CompositionRoot.cs` / `CompositionRootServices.cs` | tick 边沿驱动、OnDestroy 清理、组合根接线 |

## 2 关键语义(裁定落地)

1. **归零 ≠ 消失**(GDD 规则二十二):`EncounterEnded` **不**销毁视觉,只把行标记为 `Downed`;
   只有投影里真的不含该 id 才销毁(今日无此生产路径)。
2. **倒地野兽可取材**:`Downed && ProtoId == WildDog` ⇒ 轮廓微光常亮(人形恒 false)。
3. **42 不比较阈值**:六态与档位词由装配层从 9 的纯分类函数取,42 只做映射与文案。
4. **未知敌人 id 抛 `KeyNotFoundException`**(与 `DiseaseVitalsService` 同款 fail-loud):全零 DTO 与
   「真的健康」不可区分,不得用默认值伪装。
5. **EditMode 安全**:`Destroy` 一律走 `Application.isPlaying ? Destroy : DestroyImmediate`
   (首跑 3 条 `Unhandled log message` 即此守卫缺失,已修)。
6. **投影住边界侧,不住 `Assets/Sim`**(全量首跑两条门当场红,已改):
   - `AC-10-22` 静态门:10 的程序集**不得**含 `IVitalsQuery` 实现(`EnemyVitalsQuery` 一度在
     `Assets/Sim` ⇒ 被扫出「写面」);
   - `AC-21a-30` 静态门:`Assets/Sim/**` 语法级**零 `float`**(`ToVitals` 的 `(float)` 转换)。
   ⇒ 与 `DiseaseVitalsService` 同址(`Sim.Contracts/VitalsDto.cs` 头注「门面实现住边界侧」)。
   **不改门借绿。**

## 3 判据(实跑)

```bash
cd unity && ~/Unity/Hub/Editor/6000.3.24f1/Editor/Unity -batchmode -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter "DaYiJingCheng.Tests.Boot.enemy_visuals_test" \
  -testResults "$PWD/Logs/enemy-visuals.xml" -logFile "$PWD/Logs/enemy-visuals.log"
```

| 轮 | 夹具 | 结果 | 产物 |
|---|---|---|---|
| EditMode · 本件 | `enemy_visuals_test`(14 条) | **14 / 14 PASS** | `Logs/enemy-visuals.xml` |
| PlayMode · 本件 | `enemy_visual_spawner_play_test`(1 条) | **1 / 1 PASS** | `Logs/enemy-visuals-play.xml` |
| EditMode · 回归 | `DaYiJingCheng.Tests.Boot`(449 条) | **449 / 449 PASS** | `Logs/enemy-boot-regression.xml` |
| EditMode · 全量 | 全部(7711 条) | **7674 / 7674 通过 · 0 失败 · 37 既有 skip** | `Logs/enemy-full-editmode.xml` |

14 条覆盖:投影饱和 / 趋势恒 0 / 档位单调;查询未知 id 抛;复合门面分派(病人侧桩只被非敌人 id 命中);
面片九槽 + 亮槽数随 Position 变 + 档位词;位姿桥六态互异 / 倒地覆盖 / 淡入到 1 / 轮廓只给倒地野兽;
spawner 一成员一视觉 + 位姿桥 / 收场保留且倒地 / 空流零建 / 幂等销毁。

## 4 诚实边界(判据的边界)

- **「六态互异」**只证明**肢体欧拉角六值互异**,不证明「人在远处能分辨六态」—— 后者要人工走查。
- **「读数槽亮 N 格」**证明的是渲染簿记,不证明黄铜材质观感正确(占位材质 = 共享灰)。
- PlayMode 1 条证明「运行期真的建出可见物体、teardown 后不残留」,不证明**真关卡里看得见**。

## 5 未覆盖面(如实登记)

1. **美术资产**:人形 / 野兽仍是 primitive 组合体;真资产到位时只换 `ComposedEnemyVisualFactory`
   的 `Create`,spawner / 42 / 夹具一字不改。
2. **AI 六态**:27 的状态机今日无生产构造点 ⇒ 场上恒 `Patrol`。位姿桥的六态姿态由 EditMode 夹具
   直接以六值驱动证明可达,但**生产路径**尚未产生非 Patrol 态。
3. **屏幕截图 / 人工签核**:本批未产出(占位资产下的屏摄无判定价值);留给美术轮。

## 6 全量回归(终态)

`Logs/enemy-full-editmode.xml`:`total = 7711` · `passed = 7674` · `failed = 0` · `skipped = 37`
(skip 全部是既有 `[Ignore]` / 未运行登记项,不是本批新增)。

⚠️ **首跑(移动投影之前)是 2 红** —— `AC-10-22` + `AC-21a-30`,均已按「不改门」的方式修复(§2.6)。
终态全绿是移动**之后**重跑的结果。

## 7 同日追加裁定:人形敌人掉落拾取(新立项,本轮不实现)

用户 2026-11-14 追加裁定「根据剧情需要,人形敌人也可以掉落拾取」。口径 = **真正的掉落物系统**
(选项 ③),**不是**把取材微光扩到人形 ——
- 已登记:`design/gdd/enemy-ai.md` §Visual 二① 追加段 + `OQ-27-8` ·
  `production/registration-debt-register.md` §F(`27-掉落-1`);
- **本轮实现不变**:取材微光仍只服务 `down_class = Salvageable` 的野狗;
- 待裁:掉落表 schema · 首个掉落原型 · 铸造点 · 倒地即可拾取还是消灭才掉落 · 拾取交互路由(4 / 20 / 17)。
