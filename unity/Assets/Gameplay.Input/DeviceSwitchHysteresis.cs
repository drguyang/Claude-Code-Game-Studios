// Story 008 · D4 迟滞判定器(AC-3-D4;纯机制,零引擎依赖)
//
// 权威来源:
//   GDD input-system.md §Tuning Knobs 一之二 · AC-3-D4 —— 切源须**同时**满足
//   ① ‖Δ‖ > DEVICE_SWITCH_THRESHOLD ② ‖Δ‖ > DRIFT_TOLERANCE ③ 持续 > DWELL_DURATION;
//   Ⓑ 比较域 = 径向 ‖Δ‖₂(禁逐分量 —— 逐分量在斜向**过早/漏**判,AC 明禁);
//   Ⓑ.1 鼠标列(指针位移量纲)与轴列(归一化 [0,1] 量纲)**两列各自成表**,本类以
//   「每家族一套三旋钮」承载两列(列的数值住常量表,由 DeviceStateManager 分列注入)。
//   边界开闭:AC 原文三条件全用「超过 / 以上」+ story AC 文本「① > ② > ③ 持续 > 」
//   ⇒ 三条一律**严格大于**(恰等不切)—— 测试内明写该口径与旋钮表一致(story QA
//   Edge「持续时长恰 = DWELL_DURATION(边界开闭侧须与旋钮表口径一致,测试内明写)」)。
//
// 算术纪律:内部量全整数(Q16.16)。‖Δ‖ 的径向比较经**平方域整数比较**完成
//   (threshold² 构造期预算,比较 mag² > threshold² —— 免开方、免浮点;
//   Q16.16 满幅 65536 ⇒ 平方 2^32 量级,int64 域内安全)。
//   指针列的安全界 = 分量 |dQ16| ≤ 2^31−1(Q16.16 ≈ ±32768 px/tick)—— 此时
//   mag² = dQ16x² + dQ16y² ≤ 2·(2^31−1)² < 2^63 落 int64 内;现实指针增量远在界内。

using System;

namespace DaYiJingCheng.Gameplay.Input
{
    /// <summary>一列(一个设备类)的迟滞参数快照。与兄弟列同型 —— 两列各持一份,
    /// 不共用行(Ⓑ.1)。</summary>
    public readonly struct HysteresisColumn
    {
        /// <summary>DEVICE_SWITCH_THRESHOLD(Q16.16;轴列开区间 (0,1),指针列 &gt; 0 —— 值域断言归常量表装载门)。</summary>
        public int SwitchThresholdQ16 { get; }

        /// <summary>DRIFT_TOLERANCE(Q16.16,&gt; 0)。</summary>
        public int DriftToleranceQ16 { get; }

        /// <summary>DWELL_DURATION(tick 数;20 Hz ⇒ 1 tick = 50 ms;判据为**严格大于**)。</summary>
        public int DwellTicks { get; }

        /// <summary>构造一列参数(不做值域断言 —— 区间校验在 ADR-014 常量表装载门,同 AxisTuning 先例)。</summary>
        public HysteresisColumn(int switchThresholdQ16, int driftToleranceQ16, int dwellTicks)
        {
            SwitchThresholdQ16 = switchThresholdQ16;
            DriftToleranceQ16 = driftToleranceQ16;
            DwellTicks = dwellTicks;
        }
    }

    /// <summary>AC-3-D4 三条件迟滞判定器(径向域;两列参数各管各的来源家族)。
    /// 每 tick 由 <see cref="DeviceStateManager"/> 喂一次某候选家族的位移,本类维护
    /// 「该家族连续达标 tick 数」的驻留计数;三条件同刻全满足 ⇒ 判定夺源并把候选记为
    /// 新源(计数清零)。任一条件断裂 ⇒ 计数清零(迟滞语义:中断即重来,非漏桶)。</summary>
    public sealed class DeviceSwitchHysteresis
    {
        /// <summary>归一化轴满幅的 Q16.16 值(1.0 = 65536)—— 数字输入按满幅计用。</summary>
        public const int FullAxisQ16 = 65536;

        private readonly HysteresisColumn _axis;
        private readonly HysteresisColumn _pointer;

        // 每个来源家族的当前驻留 streak(达标连续 tick 数)。
        private readonly int[] _streakTicks = new int[4];   // 索引 = (int)DeviceSourceKind

        /// <summary>以两列参数构造(轴列 + 指针列;数值由装配层从常量表注入,本类不拍值)。</summary>
        public DeviceSwitchHysteresis(HysteresisColumn axisColumn, HysteresisColumn pointerColumn)
        {
            _axis = axisColumn;
            _pointer = pointerColumn;
        }

        /// <summary>便捷构造:六标量 = 两列 × 三旋钮(与 <see cref="DeviceStateManager"/> 的注入口一致)。</summary>
        public DeviceSwitchHysteresis(int axisThresholdQ16, int axisDriftToleranceQ16, int axisDwellTicks,
                                      int pointerThresholdQ16, int pointerDriftToleranceQ16, int pointerDwellTicks)
            : this(new HysteresisColumn(axisThresholdQ16, axisDriftToleranceQ16, axisDwellTicks),
                   new HysteresisColumn(pointerThresholdQ16, pointerDriftToleranceQ16, pointerDwellTicks))
        {
        }

        /// <summary>喂一个候选家族本 tick 的位移分量(径向在内部合成;分量在此即丢掉 ——
        /// 逐分量判据被 Ⓑ 明禁,不留通道)。</summary>
        /// <param name="currentSource">当前来源家族(None = 无源,首见达标者直接成源)。</param>
        /// <param name="candidate">候选来源家族(非 None)。</param>
        /// <param name="dQ16x">Δ 分量 x(Q16.16;轴列 = 归一化轴增量,指针列 = 指针位移像素增量 × 65536)。</param>
        /// <param name="dQ16y">Δ 分量 y。</param>
        /// <param name="axisColumn">true = 取轴列参数(Pad/Xr 家族),false = 取指针列(Kbm)。</param>
        /// <returns>true = 三条件同刻全满足 ⇒ 夺源(调用方须更新 ActiveSource 并视需要
        /// <see cref="ResetStreaks"/>);false = 条件未齐(漂移 / 未持满 / 候选即现源)。</returns>
        public bool Observe(DeviceSourceKind currentSource, DeviceSourceKind candidate,
                            int dQ16x, int dQ16y, bool axisColumn)
        {
            if (candidate == DeviceSourceKind.None) return false;
            int idx = (int)candidate;

            // 候选已是现源 ⇒ 不「夺」自己;驻留计数对现源无意义,清零防陈旧。
            if (candidate == currentSource)
            {
                _streakTicks[idx] = 0;
                return false;
            }

            var col = axisColumn ? _axis : _pointer;
            long mag2 = (long)dQ16x * dQ16x + (long)dQ16y * dQ16y;   // 径向平方(禁逐分量)
            long thr2 = (long)col.SwitchThresholdQ16 * col.SwitchThresholdQ16;
            long tol2 = (long)col.DriftToleranceQ16 * col.DriftToleranceQ16;

            // ①② 幅度两条件(严格大于;平方域比较与 ‖Δ‖ 域比较同号 —— 两边非负)。
            bool exceedsThreshold = mag2 > thr2;
            bool exceedsTolerance = mag2 > tol2;

            // 无 incumbent(§States 一 Mixed 新立 / 来源未定):①② 过即成源,免 DWELL ——
            // 迟滞保护的是「现任不被漂移翻掉」,无现任则无抖切面(登记注记:GDD 未逐字
            // 判 None 侧;此读法与「最近一次有效输入」一致,有效 = 幅度双条件)。
            if (currentSource == DeviceSourceKind.None)
            {
                if (exceedsThreshold && exceedsTolerance)
                {
                    ResetStreaks();
                    return true;
                }
                return false;
            }

            if (!exceedsThreshold || !exceedsTolerance)
            {
                _streakTicks[idx] = 0;   // 任一瞬时条件断裂 ⇒ 重来(迟滞)
                return false;
            }

            // ③ 持续:本 tick 达标 ⇒ streak +1,须**严格大于** DWELL_DURATION。
            _streakTicks[idx]++;
            if (_streakTicks[idx] > col.DwellTicks)
            {
                ResetStreaks();          // 夺源后全清 —— 旧 streak 不得留给任一侧
                return true;
            }
            return false;
        }

        /// <summary>清空全部驻留计数(来源迁移 / 无设备时由管理器调用 —— 迁移后旧 streak
        /// 不得复活成「已持满」的既得利益)。</summary>
        public void ResetStreaks()
        {
            Array.Clear(_streakTicks, 0, _streakTicks.Length);
        }
    }
}
