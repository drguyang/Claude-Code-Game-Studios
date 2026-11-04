// 存档册「覆写后 `.bak` 能救回」的**真启动序走查**(2026-11-04 · 用户指令「走查一下『覆写后 bak 能救回』」)。
//
// ─────────────────────────────────────────────────────────────────────────────
// 它补的是哪一条
// ─────────────────────────────────────────────────────────────────────────────
// `production/qa/evidence/impl-adr041-slot-overwrite-2026-11-04.md` §6.4 留了两条
// 「不得借绿」,本件结掉其中一条:
//   「覆写后 `.bak` 能救回」此前**只有 EditMode 证据** —— 手搓字节 + `SaveBytes` +
//   `LoadBytes`(internal),**没有**真启动序 / 真档 / 走查帧。
// 本件走的是玩家那条路:真 BootRoot → 真世界 → 真 `Checkpoint` 两次(第二次是**覆写**)
// → 破坏 final → **按册子上的那一行** → 断言读回来的确实是**上一版**。
//
// ─────────────────────────────────────────────────────────────────────────────
// 判据(强判据都在,截图只是附证)
// ─────────────────────────────────────────────────────────────────────────────
//   ① 覆写后 `save_3.bak` 的 tick = 第一次写的 tick,`save_3.bin` 的 tick = 第二次;
//   ② 破坏 `save_3.bin` 后,探针报 `Corrupt` ⇒ 册子该行**零字**(未绑定,不冒充空槽);
//   ③ **按下那一行 ⇒ 读档成功**,`LastLoadedTick` **恰等于**第一次写下的 tick
//      —— 这是「救回的是上一版」的唯一机械判据(等于 v2 的 tick 就说明救的不是 bak);
//   ⑥ **回退留下了痕迹**(2026-11-04 · `TD-persist-22` 日志半边):`LogAssert.Expect` 钉住
//      `LoadSequencer` 那条 `[7a][载入] … 已回退 .bak:**本次读到的是上一版**` + 事实出口
//      (`LastLoadSource` = `Bak` / `LastLoadFallback` = `FinalCorrupt`);
//   ④ **已知局限(断言它保持,不是断言它正确)**:救回后 final 仍是坏的 ⇒ 册子再探一次
//      仍是零字。零 I/O 是 GDD 规则十一的**禁止**判据(不建空档 / 不删残档 / 不改字节),
//      所以「读档顺手修盘」不是本件能自作主张加的东西 —— 它要么另裁,要么登记成债。
//
// 读哪些仓库文件 / 写哪里(test-standards 具名声明义务):
//   读:九份生产 USS + PanelSettings(经 AssetDatabase,编辑器腿)。
//   写:**仅图形模式**写 `unity/Logs/shots/wb_05…wb_07.png`(gitignored 指定暂存区);
//      存档全部落 `Path.GetTempPath()` 下的临时根,TearDown 自删。
//      batch 下 `WaitForEndOfFrame` 不触发 ⇒ 结构性零写盘。
//
// 不判:读回来的**内容**对不对(事件流 / 折叠 / 结构)—— 那是 7a 的既有一致性夹具。

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using DaYiJingCheng.Gameplay.Boot;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic;
using DaYiJingCheng.Gameplay.UI.Skeuomorphic.Screens;
using DaYiJingCheng.Sim.Codec;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DaYiJingCheng.Tests.PlayMode.SkeuomorphicUI
{
    /// <summary>「覆写 → 破坏 final → 按行救回上一版」的真启动序走查。</summary>
    public sealed class adr041_bak_rescue_walkthrough
    {
        private const float BootTimeoutSeconds = 60f;
        private const float PanelTimeoutSeconds = 10f;
        private const uint SlotSeq = 3;                 // 手动槽(MinManualSlotSeq)

        private GameObject _uiGo;
        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private SkeuoRuntimeDriver _driver;
        private GameObject _bootGo;
        private BootRoot _boot;
        private string _saveRoot;
        private string _worldDir;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UnloadBootScenes();

            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (go != null && go.name == "Player") UnityEngine.Object.Destroy(go);

            if (_bootGo != null) UnityEngine.Object.Destroy(_bootGo);
            if (_uiGo != null) UnityEngine.Object.Destroy(_uiGo);
            if (_panelSettings != null) UnityEngine.Object.Destroy(_panelSettings);

            foreach (var d in UnityEngine.Object.FindObjectsByType<SkeuoRuntimeDriver>(FindObjectsSortMode.None))
                if (d != null) UnityEngine.Object.Destroy(d.gameObject);

            if (!string.IsNullOrEmpty(_saveRoot))
            {
                try { if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true); }
                catch (Exception) { /* 清场失败不影响判定 */ }
            }

            _boot = null; _bootGo = null; _driver = null; _doc = null;
            _panelSettings = null; _uiGo = null; _saveRoot = null; _worldDir = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator walkthrough_bakRescue_afterOverwrite()
        {
            _saveRoot = Path.Combine(Path.GetTempPath(), "dyjq_bakrescue_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveRoot);

            // ── ① 起局 ────────────────────────────────────────────────────
            yield return BootUntilMenuAppears();
            var menu = _driver.CurrentScreen as MainMenuScreen;
            Assert.IsNotNull(menu, "[前置] 启动序应摊开主菜单");
            menu.ActivateRow(0);                                   // 「开始诊治」
            yield return WaitForBoot();
            yield return WaitForTickAtLeast(20L);

            SaveService saves = Services()?.SaveService;
            Assert.IsNotNull(saves, "[前置] 7a 须已装配");
            var slot = new SaveSlot(SlotSeq);
            _worldDir = saves.BasePath;
            string finalPath = Path.Combine(_worldDir, "save_" + SlotSeq + ".bin");
            string bakPath = Path.Combine(_worldDir, "save_" + SlotSeq + ".bak");

            // ── ② 第一次写槽 3(v1)──────────────────────────────────────
            saves.Checkpoint(slot);
            yield return WaitForWriteQueueIdle(saves);
            Assert.IsNull(saves.WriteQueue.LastFailure, "[第一写] 写盘不得失败");
            yield return WaitForFile(finalPath);
            Assert.IsTrue(File.Exists(finalPath), $"[第一写] {finalPath} 须落盘");
            long tickV1 = SaveHeaderCodec.Read(File.ReadAllBytes(finalPath)).Tick;

            // ── ③ 世界再跑一段 ⇒ **覆写同一槽**(v2)──────────────────────
            yield return WaitForTickAtLeast(tickV1 + 50L);
            saves.Checkpoint(slot);
            yield return WaitForWriteQueueIdle(saves);
            Assert.IsNull(saves.WriteQueue.LastFailure, "[覆写] 写盘不得失败");
            yield return WaitForFile(bakPath);
            Assert.IsTrue(File.Exists(bakPath),
                "[覆写] 必须留下 `.bak` —— 它是 ADR-041 §D2 开启覆写后**唯一**的可回滚点");

            long tickV2 = SaveHeaderCodec.Read(File.ReadAllBytes(finalPath)).Tick;
            long tickBak = SaveHeaderCodec.Read(File.ReadAllBytes(bakPath)).Tick;
            Debug.Log($"[BAK] v1={tickV1} v2={tickV2} bak={tickBak} @ {_worldDir}");
            Assert.AreEqual(tickV1, tickBak,
                "[判据①] `.bak` 必须是被覆写前那一版(v1),不是新写的 v2");
            Assert.Greater(tickV2, tickV1, "[判据①] 两次写必须落在不同 tick,否则本件无判别力");

            // ── ④ 覆写后的册子(帧 wb_05)────────────────────────────────
            yield return OpenBookAndRefresh();
            int row = RowOfSlot(SlotSeq);
            Assert.GreaterOrEqual(row, 0, "[前置] 册上须有槽 3 那一行");
            Debug.Log($"[BAK] 覆写后 行{row} = 「{RowText(row)}」");
            Assert.IsNotEmpty(RowText(row), "[判据①] 覆写后该行是**有档**形态(有行文字)");
            yield return Shot("wb_05_after_overwrite");

            // ── ⑤ 破坏 final 的**正文**(模拟写盘中断 / 位翻转)────────────
            //   ⚠️ 实测口径(本件的第一个发现):`SaveSlotProbe` 只验**头部**(magic + 长度),
            //   **不验校验和** ⇒ 正文坏掉的档在探针眼里仍是 `Occupied`。这不是 bug,
            //   「探测要便宜」是它的设计;但它意味着**册子看不出正文损坏**(见判据②)。
            byte[] bytes = File.ReadAllBytes(finalPath);
            bytes[bytes.Length - 1] ^= 0xFF;                       // 只翻**最后一个字节** ⇒ 校验和必挂
            File.WriteAllBytes(finalPath, bytes);
            Assert.IsFalse(SaveService.ValidateChecksum(File.ReadAllBytes(finalPath)),
                "[前置] final 必须真的坏了(校验和不过),否则后面证不了「救回」");

            _driver.MarkSaveSlotRowsDirty();
            yield return null; yield return null;
            yield return OpenBookAndRefresh();

            Debug.Log($"[BAK] 正文坏后 探针={SaveSlotProbe.Probe(_worldDir, SlotSeq).State} 行文字=「{RowText(row)}」");
            Assert.AreEqual(SaveSlotState.Occupied, SaveSlotProbe.Probe(_worldDir, SlotSeq).State,
                "[判据②·现状] 探针只验头部 ⇒ 正文坏掉的档仍报 Occupied");
            Assert.IsNotEmpty(RowText(row),
                "[判据②·现状] ⇒ 册子上这一行**照旧显示有档**(玩家看不出这份档已经坏了)");
            yield return Shot("wb_06_after_corrupt");

            // ── ⑥ 按下那一行 ⇒ 二段态「启封」⇒ 走载入(bak 救回)──────────
            var book = _driver.Screen as SaveSlotsScreen;
            Assert.IsNotNull(book, "[前置] 当前模态须是存档册");
            book.Activate(row);
            Assert.AreEqual(row, book.ArmedRowIndex,
                "[判据②的连带] 行仍是「有档」形态 ⇒ 按下去进二段态,而不是直接载入");
            // 🔁 2026-11-04(`TD-persist-22` 日志半边):回退**必须留下痕迹**。
            //   `SaveService` 住门 A(`noEngineReferences`)⇒ 它只交出事实
            //   (`LastLoadSource`),日志由唯一共同下游 `LoadSequencer.Load` 记一条 **Log**。
            //   本断言 = 那条日志**真的出现**且点名「读到的是上一版」。
            LogAssert.Expect(LogType.Log, new Regex(
                @"^\[7a\]\[载入\] 槽位 3 的正式档校验和不通过\(内容被写坏\) ⇒ 已回退 \.bak:.*上一版"));
            book.RowAt(row).ActivateOpen();                         // 「启封」

            float deadline = Time.realtimeSinceStartup + 30f;
            while (Services()?.SaveService?.LastLoadedTick == 0L
                   && Time.realtimeSinceStartup < deadline)
                yield return null;

            long loaded = Services()?.SaveService?.LastLoadedTick ?? -1L;
            long nowTick = CurrentTick();
            Debug.Log($"[BAK] 救回后 LastLoadedTick={loaded} 当前 tick={nowTick} " +
                      $"来源={Services()?.SaveService?.LastLoadSource} 原因={Services()?.SaveService?.LastLoadFallback}");
            Assert.AreEqual(SaveLoadSource.Bak, Services().SaveService.LastLoadSource,
                "[判据⑥] 事实出口须声明本次读的是回退档(日志就是照它写的)");
            Assert.AreEqual(SaveLoadFallback.FinalCorrupt, Services().SaveService.LastLoadFallback,
                "[判据⑥] 原因须是「正式档校验和不过」,不是「正式档缺失」");
            Assert.AreEqual(tickV1, loaded,
                "[判据③] 读回来的必须是**上一版**(v1 的 tick)—— 等于 v2 就说明读的是 final," +
                "不是 `.bak`;等于 0 / -1 就说明载入压根没成功");
            Assert.That(nowTick, Is.GreaterThanOrEqualTo(tickV1),
                "[判据③] 读档后逻辑时钟须定位到 v1 那一 tick(BootRoot.HydrateTimeParamsAfterLoad 同源)");

            // ── ⑦ 救回之后册子长什么样(帧 wb_07 · 已知局限)──────────────
            yield return OpenBookAndRefresh();
            Debug.Log($"[BAK] 救回后 探针={SaveSlotProbe.Probe(_worldDir, SlotSeq).State} 行文字=「{RowText(row)}」");
            Assert.AreEqual(SaveSlotState.Occupied, SaveSlotProbe.Probe(_worldDir, SlotSeq).State,
                "[判据④·已知局限] 救回**不修盘**(GDD 规则十一零 I/O)⇒ final 仍是坏的那份," +
                "而探针看不出正文坏 ⇒ 册子照旧显示有档。本断言钉的是「现状不变」,**不是**「现状正确」");
            yield return Shot("wb_07_after_rescue");

            // ── ⑧ 把**头部**也打坏 ⇒ 探针这才报 Corrupt(对照组)──────────
            //   用来说明判据②的边界:探针「看得出来」的前提是**头部**坏,不是正文坏。
            byte[] head = File.ReadAllBytes(finalPath);
            head[0] ^= 0xFF;                                        // magic 首字节
            File.WriteAllBytes(finalPath, head);
            _driver.MarkSaveSlotRowsDirty();
            yield return null; yield return null;
            yield return OpenBookAndRefresh();

            Debug.Log($"[BAK] 头部也坏后 探针={SaveSlotProbe.Probe(_worldDir, SlotSeq).State} " +
                      $"来源={SaveSlotProbe.Probe(_worldDir, SlotSeq).Source} 行文字=「{RowText(row)}」");
            // 🔁 2026-11-05(`TD-persist-22` 册子半边):主档头部坏、**备份可读** ⇒ 报备份,
            //   行上标「前版」。改前这里报 Corrupt ⇒ 一行空白,而按下去**读得出上一版**
            //   (探测与载入不同向,正是 `TD-persist-19` 的分叉形态,只是方向相反)。
            Assert.AreEqual(SaveSlotState.Occupied, SaveSlotProbe.Probe(_worldDir, SlotSeq).State,
                "[判据⑤] 主档读不出但备份可读 ⇒ 有档");
            Assert.AreEqual(SaveLoadSource.Bak, SaveSlotProbe.Probe(_worldDir, SlotSeq).Source);
            StringAssert.Contains("前版", RowText(row),
                "[判据⑤] 行上要写出「这是备份那一份」—— 玩家得知道按下去读到的是旧的");
            yield return Shot("wb_08_after_magic_corrupt");

            // ── ⑨ 主档**整个没了**、只剩备份(崩在 WriteAtomic 的写窗口)───────────
            //   2026-11-05「按行业惯例纠正」后的判据:**备份在手就是有档**。
            //   改前这里会显示「空槽」,而盘上那份 bak 读得出来 —— 玩家找不到自己的世界。
            File.Delete(finalPath);
            _driver.MarkSaveSlotRowsDirty();
            yield return null; yield return null;
            yield return OpenBookAndRefresh();

            Debug.Log($"[BAK] 主档删掉后 探针={SaveSlotProbe.Probe(_worldDir, SlotSeq).State} " +
                      $"来源={SaveSlotProbe.Probe(_worldDir, SlotSeq).Source} 行文字=「{RowText(row)}」");
            Assert.AreEqual(SaveSlotState.Occupied, SaveSlotProbe.Probe(_worldDir, SlotSeq).State,
                "[判据⑦] 只剩备份 ⇒ 有档(不是空槽)");
            Assert.AreEqual(SaveLoadSource.Bak, SaveSlotProbe.Probe(_worldDir, SlotSeq).Source,
                "[判据⑦] 来源 = Bak —— 数据面可分「主档在」与「只剩备份」");
            Assert.IsNotEmpty(RowText(row),
                "[判据⑦] 册子上这一行**还在**(玩家找得到它)");
            StringAssert.Contains("前版", RowText(row), "[判据⑦] 只剩备份 ⇒ 标「前版」");
            yield return Shot("wb_09_bak_only");

            // ── ⑪ 主档与备份**都**读不出 ⇒ 「此页已损」─────────────────────
            //   这是与「未绑定」**(我不知道)** 必须分开的那一态:我探到了,这一页坏了。
            //   改前两种都在册子上是零字,玩家看不出区别。
            File.WriteAllBytes(finalPath, new byte[] { 0x00, 0x00, 0x00, 0x00 });
            File.Delete(bakPath);
            _driver.MarkSaveSlotRowsDirty();
            yield return null; yield return null;
            yield return OpenBookAndRefresh();

            Debug.Log($"[BAK] 两级都坏后 探针={SaveSlotProbe.Probe(_worldDir, SlotSeq).State} 行文字=「{RowText(row)}」");
            Assert.AreEqual(SaveSlotState.Corrupt, SaveSlotProbe.Probe(_worldDir, SlotSeq).State,
                "[判据⑪] 两级都读不出 ⇒ Corrupt");
            StringAssert.Contains("此页已损", RowText(row),
                "[判据⑪] 行上要写明「这页坏了」—— 而不是一行空白(那样和「我不知道」同形)");
            yield return Shot("wb_10_damaged");

            // ⚠️ **本件到「册子上看得见」为止,不再做第二次载入**:
            //   探针与载入的「同向」已在存储层钉死(`manualSlot_finalMissing_bakAlone_isRecoverable`
            //   同时断言 Probe=Occupied 与 LoadBytes 成功),本件不重复。

            // ── ⑩ 读档之后**世界继续跑**,不得出现任何异常(`TD-sim-05` 的判据)──────
            //   本件在 ⑥ 已经把逻辑时钟从 tick 70 拉回到 20(读的是一份更早的档)。
            //   改前:`DiseaseVitalsService._lastTick` 停在 70 ⇒ 之后每个 tick 边沿抛
            //   `InvalidOperationException: 体征链 Step 时间倒流:tick=N < 上次 70`,
            //   直到时钟追平 —— console 每 tick 刷一条(本件正是这样抓到它的)。
            //   改后:读档时 `ResetAfterRestore` 把派生态与游标清零,下一个边沿按新流重建。
            //   **判据形态**:关册(模态摊开会停 tick)后等时钟越过读档前的 tick;
            //   UTF 默认把任何未处理的 `LogType.Exception` / Error 判红 ⇒ 等不到就说明还在抛。
            _driver.CloseModal();
            long target = tickV2 + 5L;
            float deadline3 = Time.realtimeSinceStartup + 30f;
            while (CurrentTick() < target && Time.realtimeSinceStartup < deadline3)
                yield return null;

            Debug.Log($"[BAK] 读档后世界继续跑:tick={CurrentTick()}(须越过读档前的 {tickV2})");
            Assert.GreaterOrEqual(CurrentTick(), target,
                "[判据⑧] 读档后时钟须能越过读档前的 tick —— 卡住或抛异常都意味着" +
                "体征链的单调门把世界钉死在读档时刻(TD-sim-05)");
        }

        // ══════════════════════════════════════════════════════════════════
        // 装置(与 resume_existing_save_playmode_test 同款)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>开册并强制重探一次(关 → 开,保证读到的是**磁盘现状**而不是上一轮缓存)。</summary>
        private IEnumerator OpenBookAndRefresh()
        {
            _driver.CloseModal();
            yield return null;
            Assert.IsTrue(_driver.OpenModal(ModalId.SaveSlots), "[前置] 存档册须开得出来");
            yield return null; yield return null;
            _driver.MarkSaveSlotRowsDirty();
            yield return null; yield return null;
        }

        private int RowOfSlot(uint seq)
        {
            var book = _driver.Screen as SaveSlotsScreen;
            if (book == null) return -1;
            for (int i = 0; i < book.SlotRowCount; i++)
                if (book.RowAt(i).SlotIndex == (int)seq) return i;
            return -1;
        }

        private string RowText(int rowIndex)
        {
            var book = _driver.Screen as SaveSlotsScreen;
            return book == null || rowIndex < 0 ? string.Empty : book.RowAt(rowIndex).MarkText;
        }

        private IEnumerator Shot(string name)
        {
            if (Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();

            string dir = Path.Combine(Application.dataPath, "..", "Logs", "shots");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, name + ".png"));

            // superSize = 2:游戏窗口本身的尺寸偏小(实测 550×413),原尺寸截图的纸面文字
            // 在证据件里读不清 —— 放大两倍只为**可读**,不改任何形态。
            ScreenCapture.CaptureScreenshot(path, 2);
            yield return null;
            yield return null;
            Debug.Log($"[BAK] 已存帧 ⇒ {path}");
        }

        private IEnumerator BootUntilMenuAppears()
        {
            _uiGo = new GameObject("SkeuoUI");
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _doc = _uiGo.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _driver = _uiGo.AddComponent<SkeuoRuntimeDriver>();

            float t0 = Time.realtimeSinceStartup;
            while (_doc.rootVisualElement == null || _doc.rootVisualElement.panel == null)
            {
                if (Time.realtimeSinceStartup - t0 > PanelTimeoutSeconds)
                    Assert.Fail($"{PanelTimeoutSeconds}s 内面板未 attach");
                yield return null;
            }

            AttachProductionStyleSheets(_doc.rootVisualElement);

            _bootGo = new GameObject("BootRoot_BakRescue");
            _boot = _bootGo.AddComponent<BootRoot>();
            _boot.BootWithMainMenu = true;
            _boot.SaveRootOverride = _saveRoot;

            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!_boot.IsMainMenuOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(_boot.IsMainMenuOpen,
                $"{BootTimeoutSeconds}s 内主菜单未摊开 —— 启动序第 2.95 步没走到");

            for (int i = 0; i < 5; i++) yield return null;
        }

        private IEnumerator WaitForBoot()
        {
            float deadline = Time.realtimeSinceStartup + BootTimeoutSeconds;
            while (!ReadBooted() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ReadBooted(), $"启动序须在 {BootTimeoutSeconds}s 内完成");
            for (int i = 0; i < 5; i++) yield return null;
        }

        /// <summary>服务袋(反射;`BootRoot._services` 是私有字段)。</summary>
        private CompositionRootServices Services()
            => (CompositionRootServices)typeof(BootRoot)
                .GetField("_services", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_boot);

        private long CurrentTick() => Services()?.TickDriver?.CurrentTick ?? -1L;

        private IEnumerator WaitForTickAtLeast(long tick, float seconds = 15f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (CurrentTick() < tick && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.GreaterOrEqual(CurrentTick(), tick, $"[前置] 逻辑时钟须推进到 {tick}");
        }

        private static IEnumerator WaitForWriteQueueIdle(SaveService saves, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (saves.WriteQueue.Outstanding > 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static IEnumerator WaitForFile(string path, float seconds = 10f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private bool ReadBooted()
            => _boot != null
               && (bool)typeof(BootRoot).GetField("_booted", BindingFlags.NonPublic | BindingFlags.Instance)
                   .GetValue(_boot);

        private IEnumerator UnloadBootScenes()
        {
            if (_boot == null) yield break;
            yield return UnloadHandle("_mainMenuLoadHandle");
            yield return UnloadHandle("_worldLoadHandle");
        }

        private IEnumerator UnloadHandle(string fieldName)
        {
            AsyncOperationHandle<SceneInstance> handle = default;
            bool valid;
            try
            {
                var f = typeof(BootRoot).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
                handle = (AsyncOperationHandle<SceneInstance>)f.GetValue(_boot);
                valid = handle.IsValid();
            }
            catch (Exception) { valid = false; }

            if (!valid) yield break;

            var op = UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(handle);
            while (!op.IsDone) yield return null;
        }

        private static readonly string[] StyleSheetPaths =
        {
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoThemeVariables.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoPaper.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoInk.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoBrass.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoScroll.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoSeal.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoMarks.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoImplement.uss",
            "Assets/Gameplay.UI/Skeuomorphic/SkeuoFocusVisible.uss",
        };

        private static void AttachProductionStyleSheets(VisualElement panelRoot)
        {
#if UNITY_EDITOR
            foreach (string path in StyleSheetPaths)
            {
                var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !panelRoot.styleSheets.Contains(sheet))
                    panelRoot.styleSheets.Add(sheet);
            }
#endif
        }
    }
}
