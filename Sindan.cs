using cAlgo.Indicators;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.API;
using System.Threading;
using System;   
using System.Runtime.CompilerServices;
using System.Text;

using System.Collections.Generic;

namespace cAlgo
{
    public enum Toggle { OFF, ON }
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GalileoUltraPhysicsV5 : Robot
    {
        // ===== Trade / Signal =====
        [Parameter("Label Prefix", Group = "Trade", DefaultValue = "GAL_U4")]
        public string LabelPrefix { get; set; }

        [Parameter("Units (fallback)", Group = "Trade", DefaultValue = 10000, MinValue = 1)]
        public int Units { get; set; }

        [Parameter("Cooldown Bars", DefaultValue = 0, MinValue = 0)]
        public int CooldownBars { get; set; }

        [Parameter("Max Trades / Day", Group = "Trade", DefaultValue = 0)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Use Daily Tilt", Group="Trade", DefaultValue = true)]
        public bool UseDailyTilt { get; set; }

        [Parameter("Lookback", Group = "Signal", DefaultValue = 50, MinValue = 10)]
        public int Lookback { get; set; }

        [Parameter("ATR Period", Group = "Risk", DefaultValue = 14)]
        public int AtrPeriod { get; set; }

        [Parameter("Trail k (ATR)", Group = "Risk", DefaultValue = 0.8)]
        public double TrailAtrK { get; set; }

        [Parameter("TP R:R", Group = "Risk", DefaultValue = 1.5)]
        public double RMultiple { get; set; }

        [Parameter("Max Market Range (pips)", Group = "Risk", DefaultValue = 0, MinValue = 0)]
        public int MaxMarketRangePips { get; set; } // 0 なら未指定

        [Parameter("Trail: Min dist from price (pips)", Group="Risk", DefaultValue = 0.5, MinValue = 0.0)]
        public double MinSLFromPricePips { get; set; }

        [Parameter("Trail mode: Chandelier", Group="Risk", DefaultValue = true)]
        public bool TrailChandelier { get; set; }

        // 例: サーバ時刻の 00:00 を日次境界にする（UTC運用ならそのままでOK）
        [Parameter("Daily reset hour (server time)", Group="Risk", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
        public int DailyResetHour { get; set; }

        // リセット時にロボが描いた線を掃除したい場合
        [Parameter("Clear lines on daily reset", Group="Risk", DefaultValue = false)]
        public bool ClearLinesOnDailyReset { get; set; }

        [Parameter("Force Flat on Friday", Group = "Risk", DefaultValue = Toggle.ON)]
        public Toggle ForceFlatFriday { get; set; }

        [Parameter("Friday Flat Hour (server time)", Group = "Risk", DefaultValue = 21, MinValue = 0, MaxValue = 23)]
        public int FridayFlatHour { get; set; }

        [Parameter("Friday Flat Minute", Group = "Risk", DefaultValue = 55, MinValue = 0, MaxValue = 59)]
        public int FridayFlatMinute { get; set; }

        [Parameter("Block After Friday Flat", Group = "Risk", DefaultValue = Toggle.ON)]
        public Toggle BlockAfterFridayFlat { get; set; }

        [Parameter("Cancel Pending Orders", Group = "Risk", DefaultValue = Toggle.ON)]
        public Toggle CancelPendingAfterFlat { get; set; }

        [Parameter("Risk %", DefaultValue = 0.5, MinValue = 0, MaxValue = 10)]
        public double RiskPct { get; set; }

        [Parameter("Jitter (ms)", Group = "Exec", DefaultValue = 700)]
        public int JitterMs { get; set; }

        // ===== Parameters (Gates) =====
        [Parameter("Use Energy Gate", Group = "Gates", DefaultValue = false)]
        public bool UseEnergyGate { get; set; }

        [Parameter("Energy Min (Etot)", Group = "Gates", DefaultValue = 0.00010)]
        public double EnergyMin { get; set; }

        [Parameter("Energy Max (Etot)", Group = "Gates", DefaultValue = 0.50)]
        public double EnergyMax { get; set; }

        [Parameter("Max |dE|", Group = "Gates", DefaultValue = 0.06)]
        public double MaxAbsDE { get; set; }

        // 追加: エネルギーゲートのモード
        public enum EnergyMode { Off, Block, Scale, Tighten }
        [Parameter("Energy Mode", DefaultValue = EnergyMode.Scale)] public EnergyMode EngMode { get; set; }
        [Parameter("Energy ATR Period", DefaultValue = 14, MinValue = 5)] public int EngAtrPeriod { get; set; }
        [Parameter("Quiet ≤ (x ATR)", DefaultValue = 0.20, MinValue = 0.0)] public double EngQuietX { get; set; }  // 低すぎ閾
        [Parameter("Hot  ≥ (x ATR)", DefaultValue = 1.20, MinValue = 0.0)] public double EngHotX { get; set; }     // 高すぎ閾

        // Scale/Tightenの強さ
        [Parameter("Min Volume Scale", DefaultValue = 0.35, MinValue = 0.1, MaxValue = 1.0)] public double EngMinScale { get; set; }
        [Parameter("Extra Break @Hot (pips)", DefaultValue = 2.0, MinValue = 0.0)] public double EngExtraBreakPips { get; set; }
        [Parameter("Stop Pips Min (0=auto)", DefaultValue = 0.0, MinValue = 0.0)]
        public double StopPipsMin { get; set; }

        private AverageTrueRange _atrEng;

        // ===== Physics =====
        [Parameter("Mass m", Group = "Physics", DefaultValue = 1.0)]
        public double Mass { get; set; }

        [Parameter("Damping c", Group = "Physics", DefaultValue = 0.30)]
        public double Damping { get; set; }

        [Parameter("Spring k1 (Donchian)", Group = "Physics", DefaultValue = 0.60)]
        public double SpringK1 { get; set; }

        [Parameter("Spring k2 (EMA)", Group = "Physics", DefaultValue = 0.30)]
        public double SpringK2 { get; set; }

        [Parameter("EMA Period", Group = "Physics", DefaultValue = 200)]
        public int EmaPeriod { get; set; }

        [Parameter("Coulomb μ0", Group = "Friction", DefaultValue = 0.00002)]
        public double FrictionMu0 { get; set; }

        [Parameter("Stribeck μ1", Group = "Friction", DefaultValue = 0.00010)]
        public double FrictionMu1 { get; set; }

        [Parameter("Stribeck v* (abs)", Group = "Friction", DefaultValue = 0.00020)]
        public double StribeckVStar { get; set; }

        [Parameter("Drive Gain", Group = "Physics", DefaultValue = 1.0)]
        public double DriveGain { get; set; }

        [Parameter("Noise k (ATR→σ)", Group = "Physics", DefaultValue = 0.10)]
        public double NoiseK { get; set; }

        [Parameter("Min |v| for momentum", Group = "Physics", DefaultValue = 0.00005)]
        public double MinMomentum { get; set; }

        [Parameter("SubSteps (per bar)", Group = "Physics", DefaultValue = 1, MinValue = 1, MaxValue = 20)]
        public int SubSteps { get; set; }

        [Parameter("Use Pullback Entries", Group="Pullback", DefaultValue = true)]
        public bool UsePullbackEntries { get; set; }

        [Parameter("PB Fib Min", Group="Pullback", DefaultValue = 0.3)]
        public double PbFibMin { get; set; }

        [Parameter("PB Fib Max", Group="Pullback", DefaultValue = 0.7)]
        public double PbFibMax { get; set; }

        [Parameter("PB ATR Buffer X", Group="Pullback", DefaultValue = 0.25, MinValue = 0.0)]
        public double PbAtrBufX { get; set; }

        [Parameter("PB Swing Backstep", Group="Pullback", DefaultValue = 3, MinValue = 2, MaxValue = 10)]
        public int PbBack { get; set; }

        [Parameter("PB Lookback Bars", Group="Pullback", DefaultValue = 400, MinValue = 50)]
        public int PbLook { get; set; }

        [Parameter("PB Need EMA Slope", Group="Pullback", DefaultValue = true)]
        public bool PbNeedEmaSlope { get; set; }

        [Parameter("PB Min v", Group="Pullback", DefaultValue = 0.0)]
        public double PbMinV { get; set; }   // 物理：最小速度（補助フィルタ）

        [Parameter("PB Auto Width", Group="Pullback", DefaultValue = true)]
        public bool PbAutoWidth { get; set; }

        [Parameter("PB ATR Floor (pips)", Group="Pullback", DefaultValue = 6.0, MinValue = 0)]
        public double PbAtrFloorPips { get; set; }

        [Parameter("PB ATR Cap x", Group="Pullback", DefaultValue = 1.20, MinValue = 0.8, MaxValue = 3)]
        public double PbAtrCapX { get; set; }

        [Parameter("PB Micro-Confirm", Group="Pullback", DefaultValue = false)]
        public bool PbMicroConfirm { get; set; }

        [Parameter("PB Zone Tol (pips)",   Group="Pullback", DefaultValue = 2.0, MinValue=0)]
        public double PbZoneTolPips { get; set; }

        [Parameter("PB Confirm Lookback",  Group="Pullback", DefaultValue = 2, MinValue=1, MaxValue=5)]
        public int PbConfirmLookback { get; set; }

        [Parameter("PB Arm Bars",          Group="Pullback", DefaultValue = 10, MinValue=1, MaxValue=20)]
        public int PbArmBars { get; set; }

        [Parameter("PB Relax EMA Slope",   Group="Pullback", DefaultValue = true)]
        public bool PbRelaxEmaSlope { get; set; }   // true=弱めに判定

        [Parameter("PB Min vr (|v|/ATR)",  Group="Pullback", DefaultValue = 0.45, MinValue=0)]
        public double PbMinVr { get; set; }         // 既存より緩く（0.60→0.45 推奨）

        // ===== Kalman =====
        [Parameter("Use Kalman", Group = "Kalman", DefaultValue = true)]
        public bool UseKalman { get; set; }

        [Parameter("Q base", Group = "Kalman", DefaultValue = 1e-6)]
        public double KalmanQ { get; set; }

        [Parameter("R base", Group = "Kalman", DefaultValue = 1e-6)]
        public double KalmanR { get; set; }

        // ===== Energy Gate =====
        [Parameter("Min E (×ATR^2)", Group = "Energy", DefaultValue = 0.05)]
        public double MinEnergyATR2 { get; set; }

        [Parameter("Min dE per bar", Group = "Energy", DefaultValue = 0.0)]
        public double MinDE { get; set; }

        // ===== Gap Guard =====
        [Parameter("Gap K (×ATR)", Group = "Gap", DefaultValue = 1.5)]
        public double GapK { get; set; }

        [Parameter("Gap Cooldown Bars", Group = "Gap", DefaultValue = 0, MinValue = 0)]
        public int GapCooldownBars { get; set; }

        // ==== ZigZag escape parameters ====
        [Parameter("Use ZigZag Escape", Group = "Exit", DefaultValue = true)]
        public bool UseZigZagEscape { get; set; }       

        [Parameter("ZZ BackStep", Group = "Exit", DefaultValue = 3, MinValue = 1)]
        public int ZzBackStep { get; set; }

        [Parameter("ZZ Buffer ATR×", Group = "Exit", DefaultValue = 0.0)]
        public double ZzBufferAtrX { get; set; }

        // ピボットより少し外側に置くための余白（pips）
        [Parameter("ZZ Buffer Pips", DefaultValue = 2)]
        public int ZzBufferPips { get; set; }

        // ===== ZigZag =====
        [Parameter("ZZ Break Buffer (ATR×)", Group = "ZigZag", DefaultValue = 0.4)]
        public double ZzBreakAtrX { get; set; }

        [Parameter("ZZ Break Buffer (pips)", Group = "ZigZag", DefaultValue = 2.0)]
        public double ZzBreakPips { get; set; }

        [Parameter("Show ZigZag on Chart", Group="ZigZag", DefaultValue = true)]
        public bool ShowZigZagOnChart { get; set; }

        // Enable or disable using ZigZag breakouts as entry triggers. When false
        // the robot will rely solely on pullback entries (if enabled) for new
        // positions. ZigZagEscape remains active for exit/trailing even when
        // break entries are disabled.
        [Parameter("Use ZZ Break Entries", Group="ZigZag", DefaultValue = true)]
        public bool UseZzBreakEntries { get; set; }

        // ===== Timer/Exec =====
        [Parameter("Use Timer Compute", Group = "Exec", DefaultValue = true)]
        public bool UseTimerCompute { get; set; }

        [Parameter("Calc Every (sec)", Group = "Exec", DefaultValue = 300, MinValue = 1, MaxValue = 3600)]
        public int CalcEverySec { get; set; }
      
        [Parameter("Min Stops (pips)", Group = "Exec", DefaultValue = 0, MinValue = 0)]
        public int MinStopsPips { get; set; }

        [Parameter("Verbose Audit", Group = "Debug", DefaultValue = true)]
        public bool Verbose { get; set; }

        [Parameter("Audit Every N Bars", Group = "Debug", DefaultValue = 1, MinValue = 1, MaxValue = 50)]
        public int AuditEveryN { get; set; }

        [Parameter("Debug Viz", Group = "Debug", DefaultValue = true)]
        public bool DebugViz { get; set; }

        [Parameter("Draw Robot Lines", Group = "Debug", DefaultValue = false)]
        public bool DrawRobotLines { get; set; }

        [Parameter("Max Spread (pips)", DefaultValue = 2.0, MinValue = 0.0)]
        public double SpreadMaxPips { get; set; }
        [Parameter("Timer Interval (ms)", DefaultValue = 250)]
        public int TimerIntervalMs { get; set; }

        // ===== Trail =====
        [Parameter("Trail ATR x", Group="Trail", DefaultValue = 1.5)]
        public double TrailAtrMult { get; set; }

        [Parameter("Min trail step (pips)", Group="Trail", DefaultValue = 1.0)]
        public double MinTrailStepPips { get; set; }

        // ===== State =====
        private AverageTrueRange _atr;
        private ExponentialMovingAverage _ema;        private DateTime _lastTrade = DateTime.MinValue;
        private DateTime _day = DateTime.MinValue;
        private volatile bool  _timerBusy = false;
        private DateTime _lastTimerTs = DateTime.MinValue;

        private int _tradesToday;
        private int _gapCooldown;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ===== Helpers =====
        private static bool IsFinite(double x) => !(double.IsNaN(x) || double.IsInfinity(x));
        private static double Sqr(double x) => x * x;

        // physical state
        private double _x, _v, _a;
        private double _Eprev;

        // Energy (Etot) と その増分（dE）
        private double Etot = 0.0;
        private double dE   = 0.0;

        private int lastTradeBar = int.MinValue / 2;
        // --- Pullback arming state ---
        private int _armLongBar = -9999;
        private int _armShortBar = -9999;
        private double _armLongSL = double.NaN;
        private double _armShortSL = double.NaN;

        // fields
        private string _zzWhyB = "";
        private string _zzWhyS = "";
        // Kalman covariance
        private double _P00, _P01, _P10, _P11;

        private readonly Random _rng = new Random();

        // cache
        private bool _physicsReady;
        private int _lastComputedBar = -1;
        private double _hhC, _llC, _midC, _emaC, _atrC, _bandDiffC;

        private string _label;
        private bool _suspended =false;
        private DateTime _lastFridayFlat = DateTime.MinValue;   // 同じ金曜で多重実行しないため
        
        // When a pullback entry is triggered it may compute an explicit stop
        // price based on the swing structure. Store that here so that
        // PlaceTrade can override the default ATR-based stop. NaN means no
        // override is pending.
        private double _initialSLOverride = double.NaN;
        // TP倍率の一時上書き（NaN=未指定）
        private double _rMultipleOverride = double.NaN;

        private double ToTickDown(double price)
         => Math.Floor(price / Symbol.TickSize)  * Symbol.TickSize; // 価格を下方向へ
        private double ToTickUp  (double price)
         => Math.Ceiling(price / Symbol.TickSize) * Symbol.TickSize; // 価格を上方向へ
      
        // 論理的な“日”＝（Server.Time を DailyResetHour 時間だけ巻き戻した日付）
        // 例: DailyResetHour=0 なら通常のカレンダー日（サーバ日付）
        private DateTime LogicalDay(DateTime t) => t.AddHours(-DailyResetHour).Date;

        // 日次ロール処理：日境界を跨いだら _tradesToday をクリア
        private void RollDailyIfNeeded()
    {
        var nowLogical = LogicalDay(Server.Time);
        if (nowLogical != _day)
      {
        _day = nowLogical;
        _tradesToday = 0;        // ←これが肝心
        _suspended = false;                 // ★ 金曜フラット後も、翌営業日に自動解除
        if (Verbose) Print("[RESET] daily at {0:yyyy-MM-dd HH:mm} (server), tradesToday=0 (trading resumed)", Server.Time);
        _gapCooldown  = 0;       // 必要ならギャップ由来のクールダウンも解除
        if (ClearLinesOnDailyReset) ClearRobotLines();
        if (Verbose) Print("[RESET] daily at {0:yyyy-MM-dd HH:mm} (server), tradesToday=0", Server.Time);
      }
    }

        // DailyTilt を反映した“その日の実効Max”を返す
        private int EffectiveMaxTradesPerDay()
    {
        int m = MaxTradesPerDay;
        double dummyR = 1.0;                 // rMultiple はここでは不要なのでダミー
        ApplyDailyTilt(ref dummyR, ref m);   // 既存のDailyTiltロジックを流用
        return Math.Max(0, m);               // 0 のときは“無制限”扱いにできる
    }

        // 直近 n 本の高値を上抜け / 安値を下抜けで“再開”確認
        private bool BreakoutUp(int n)
    {
        double hi = double.MinValue;
        for (int i = 1; i <= n; i++) hi = Math.Max(hi, Bars.HighPrices.Last(i));
        return Bars.ClosePrices.LastValue >= hi - Symbol.TickSize*0.5; // ほぼ同値でもOK
    }
        private bool BreakoutDown(int n)
    {
        double lo = double.MaxValue;
        for (int i = 1; i <= n; i++) lo = Math.Min(lo, Bars.LowPrices.Last(i));
        return Bars.ClosePrices.LastValue <= lo + Symbol.TickSize*0.5;
    }

        private bool SpreadTooWide()
    {
        double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
        return (SpreadMaxPips > 0.0) && (spreadPips > SpreadMaxPips);
    }
        private void REJ(string code, string msg) => Print($"[REJ][{code}] {msg}");

        // 発注前に呼ばれるヘルパー
        private void ApplyDailyTilt(ref double rMult, ref int maxTrades) {
        if (!UseDailyTilt) return;
        int win=0, lose=0;
        foreach(var t in History) {
        if (t.SymbolName != SymbolName) continue;
        if (t.ClosingTime.Date != Server.Time.Date) continue;
        if (t.NetProfit > 0) win++;
        else if (t.NetProfit < 0) lose++;
        }
        int n = win + lose;
        if (n < 3) return;
        double wr = win / (double)n;
        if (wr > 0.55) {
        rMult = Math.Min(rMult * 1.10, 2.0);
        maxTrades = Math.Min(maxTrades + 1, MaxTradesPerDay + 2);
        } else if (wr < 0.45) {
        rMult = Math.Max(rMult * 0.90, 1.2);
        maxTrades = Math.Max(maxTrades - 1, 1);
        }
    }
         // v/ATR に応じてフィボ帯を変化させる
         private (double fMin, double fMax) GetFibBand(double vr) {
         if (!PbAutoWidth) return (PbFibMin, PbFibMax);
         if (vr >= 3.0) return (0.30, 0.55);
         if (vr >= 1.5) return (0.38, 0.62);
         return (0.50, 0.70);
    }

// ATR の極端値を抑える
private double SoftATR() {
    double a = _atrC;
    a = Math.Max(a, PbAtrFloorPips * Symbol.PipSize);            // 下限
    return Math.Min(a, PbAtrCapX * _atr.Result.LastValue);       // 上限
}

// ミクロ再開確認
private bool MicroConfirmLong() {
    if (!PbMicroConfirm) return true;
    return Bars.ClosePrices.LastValue >
           Math.Max(Bars.HighPrices.Last(1), Bars.HighPrices.Last(2));
}
private bool MicroConfirmShort() {
    if (!PbMicroConfirm) return true;
    return Bars.ClosePrices.LastValue <
           Math.Min(Bars.LowPrices.Last(1), Bars.LowPrices.Last(2));
}

      private bool HasOpen(string label, TradeType side)
    {
      string prefix = LabelPrefix;
      foreach (var pos in Positions)
        if (pos.SymbolName == SymbolName &&
            pos.Label != null && pos.Label.StartsWith(prefix) &&
            pos.TradeType == side)
            return true;
      return false;
    }
   
       private bool CanEnter(string label, TradeType side)
{
    if (InCooldown()) return false;
    int maxDaily = EffectiveMaxTradesPerDay();
    if (maxDaily > 0 && _tradesToday >= maxDaily) return false;
    if (HasOpen(label, side)) return false;
    // SpreadTooWide() が SpreadMaxPips を見ているので二重条件は不要
    if (SpreadTooWide()) return false;
    return true;
}

         protected override void OnStart()
    {
         // --- 初期化 ---
         _label = LabelPrefix;
         _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Exponential); // ←実際の型に合わせて
         _ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaPeriod);            // 例         //物理モデルの初期値
         _day   = LogicalDay(Server.Time);
         _x     = Bars.ClosePrices.LastValue;
         _v     = 0.0;
         _a     = 0.0;
         _Eprev = 0.0;
         _P00   = 1e-8; _P01 = 0; _P10 = 0; _P11 = 1e-8;
         _atrEng = Indicators.AverageTrueRange(EngAtrPeriod, MovingAverageType.Exponential);
         _zzWhyB = ""; _zzWhyS = "";
         {
             _suspended = false;
             _lastFridayFlat = DateTime.MinValue;
         }
             // Reset computation state
            _physicsReady = false;
            _lastComputedBar = -1;

            // Initialize trade counters
            _lastTrade = DateTime.MinValue;
            _tradesToday = 0;
         ClearRobotLines();   // 起動時に残骸を消す
         if (UseTimerCompute)
         Timer.Start(TimeSpan.FromMilliseconds(TimerIntervalMs)); // 例: 250〜500ms
         _timerBusy = false;
         //（任意）取引所の最小/最大を確認
         if (Verbose)
                Print("[AUDIT] START {0} TF={1} Tick={2}", SymbolName, Bars.TimeFrame, Symbol.TickSize);
         Print("VolInUnits Min={0}, Step={1}, Max={2}",
          Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsStep, Symbol.VolumeInUnitsMax);
    }

         private DateTime _nextDiag  = DateTime.MinValue;
            protected override void OnStop()
       {

            if (UseTimerCompute)Timer.Stop();
        }

       protected override void OnTimer()
   {
            RollDailyIfNeeded();
            EnforceFridayFlat();
            if (!UseTimerCompute) return;
            // prevent overlapping timer executions
            if (_timerBusy) return;

            // respect the CalcEverySec spacing – skip if the timer was run recently
            // Respect CalcEverySec spacing only when CalcEverySec > 0. If CalcEverySec
            // is zero the timer will run on every invocation of OnTimer (subject
            // to TimerIntervalMs). This prevents CalcEverySec=0 from forcing a
            // minimum 1 second delay via Math.Max(1, CalcEverySec).
            if (CalcEverySec > 0 && _lastTimerTs != DateTime.MinValue)
            {
                var span = Server.Time - _lastTimerTs;
                if (span.TotalSeconds < Math.Max(1, CalcEverySec))
                    return;
            }
            // mark last timer call
            _lastTimerTs = Server.Time;

            _timerBusy = true;
            try
            {
                // Input guard: ensure ATR and price are valid and finite
                double atrVal = _atr.Result.LastValue;
                double pxVal  = Bars.ClosePrices.LastValue;
                if (atrVal <= 0 || double.IsNaN(atrVal) || double.IsInfinity(atrVal) ||
                    double.IsNaN(pxVal) || double.IsInfinity(pxVal))
                {
                    // Skip computation on invalid inputs
                    return;
                }

                // If this bar hasn't been computed yet, perform the heavy physics calculation.
                // ComputePhysicsAndCache returns true only when the computation succeeded and
                // internal state (including _lastComputedBar and _physicsReady) is updated.
                if (_lastComputedBar != Bars.Count - 1)
                {
                    bool ok = ComputePhysicsAndCache();
                    if (!ok)
                    {return;}
                }
            }
            catch (Exception ex)
            {
                if (Verbose)
                    Print("[TIMER] crash: {0}", ex.Message);
            }
            finally
            {
                // always release the busy flag
                _timerBusy = false;
            }
        }
        private void ClearRobotLines()
{
    // 過去N本ぶん、ロボが使っていたIDを総当たりで削除
    int N = Math.Min(5000, Bars.Count + 1000);
    for (int i = 0; i < N; i++)
    {
        Chart.RemoveObject($"zz_H1_{i}");
        Chart.RemoveObject($"zz_L1_{i}");
        Chart.RemoveObject($"zz_BU_{i}");
        Chart.RemoveObject($"zz_BD_{i}");
        Chart.RemoveObject($"TRIG_BUY_{i}");
        Chart.RemoveObject($"TRIG_SELL_{i}");
    }
}

        // energy = Etot（あなたの計算値）を渡して使う
private (bool block, double volScale, double extraBreak, string why) CheckEnergy(int i1, double energy)
{
    if (EngMode == EnergyMode.Off) return (false, 1.0, 0.0, "off");

    double atr = _atrEng.Result[i1];
    if (atr <= 0) return (false, 1.0, 0.0, "atr=0");

    double x = energy / atr; // ATR正規化

    if (x <= EngQuietX)
    {
        // 静かすぎる相場：基本は見送り
        bool blk = (EngMode == EnergyMode.Block);
        return (blk, 1.0, 0.0, $"quiet x={x:F2} ≤ {EngQuietX:F2}");
    }

    if (x >= EngHotX)
    {
        if (EngMode == EnergyMode.Scale)
        {
            // 熱いほどサイズを絞る（線形）
            double t = Math.Min(3.0, (x - EngHotX) / (EngHotX)); // 0〜3にクリップ
            double scale = Math.Max(EngMinScale, 1.0 - 0.5 * t); // 最低 EngMinScale
            return (false, scale, 0.0, $"hot(scale) x={x:F2}");
        }
        else if (EngMode == EnergyMode.Tighten)
        {
            // ブレイク要求を厳しく（pipsを上乗せ）
            return (false, 1.0, EngExtraBreakPips, $"hot(tighten) x={x:F2}");
        }
        else // Block
        {
            return (true, 1.0, 0.0, $"hot(block) x={x:F2}");
        }
    }

    // ふつうの状態
    return (false, 1.0, 0.0, $"normal x={x:F2}");
}

        // --- Minimal diagnostics (no new parameters) ---
        private void DiagnoseEntry()
{
    if (Server.Time < _nextDiag)return;
    _nextDiag = Server.Time.AddMinutes(5);
    try
    {
        var reasons = new System.Collections.Generic.List<string>();
        bool blocked = false;

        if (!_physicsReady) { reasons.Add("PhysicsNotReady"); blocked = true; }
        if (Bars.Count < Math.Max(Lookback + 5, 30)) { reasons.Add("BarsNotEnough"); blocked = true; }
        if (InCooldown()) { reasons.Add("Cooldown"); blocked = true; }
        if (EffectiveMaxTradesPerDay() > 0 && _tradesToday >= EffectiveMaxTradesPerDay())
           { reasons.Add("MaxTradesPerDay"); blocked = true; }
        if (SpreadTooWide()) { reasons.Add($"SpreadTooWide({Symbol.Spread / Symbol.PipSize:F2}p)"); blocked = true; }

        if (blocked)
            Print("[DIAG][BLOCK] {0} | mid={1:F5} ema={2:F5} atr={3:F5} v={4:+0.000000;-0.000000} a={5:+0.000000;-0.000000}",
                  string.Join(",", reasons), _midC, _emaC, _atr.Result.LastValue, _v, _a);
        else
            Print("[DIAG][PASS] mid={0:F5} ema={1:F5} atr={2:F6} v={3:E6} a={4:E6} v/atr={5:E3} a/atr={6:E3}",
        _midC, _emaC, _atrC,
        _v, _a,
        _v / Math.Max(1e-12, _atrC),
        _a / Math.Max(1e-12, _atrC));

    }
    catch { /* no-throw for safety */ }
}
        // 期待値計算のためのヘルパーメソッド
private double ComputeExpectedValue(double winRate, double winProfit, double loseRate, double loseLoss)
{
    double expectedValue = (winRate * winProfit) - (loseRate * loseLoss);
    return expectedValue;
}

        private void CalculateTradeExpectedValue()
{
    double totalWins = 0, totalLosses = 0;
    int winCount = 0, loseCount = 0;

    foreach (var t in History)
    {
        if (t.SymbolName != SymbolName) continue;
        double pl = t.NetProfit;
        if (pl > 0) { totalWins += pl; winCount++; }
        else if (pl < 0) { totalLosses += -pl; loseCount++; } // 損失は正値で積む
    }

    int n = winCount + loseCount;
    if (n == 0) { if (Verbose) Print("期待値: N/A (履歴なし)"); return; }

    double winRate   = winCount  / (double)n;
    double loseRate  = loseCount / (double)n;
    double winProfit = (winCount  > 0) ? totalWins   / winCount  : 0.0;
    double loseLoss  = (loseCount > 0) ? totalLosses / loseCount : 0.0;

    double expectedValue = (winRate * winProfit) - (loseRate * loseLoss);
    Print("期待値: {0:F2}  勝率:{1:P1}  平均利:{2:F2}  平均損:{3:F2}", expectedValue, winRate, winProfit, loseLoss);
}

        protected override void OnBar()
{
    RollDailyIfNeeded();
    EnforceFridayFlat();   // ★追加：金曜終盤の強制決済

    if (_suspended) return;   
         // --- 必ずこのバーの物理値を更新（タイマー無効や遅延に備える） ---
    if (_lastComputedBar != Bars.Count - 1)
    {
        bool ok = ComputePhysicsAndCache();
        if (!ok) { DiagnoseEntry(); return; } // 入力が壊れているときは安全にスキップ
    }
         // 1) トリガー判定（ポジ無しのときのみ）
bool trigBuy = false, trigSell = false;
double slCandidate = double.NaN;

if (!HasOpen(_label, TradeType.Buy) && !HasOpen(_label, TradeType.Sell))
{
    // --- A) プルバック ---
    if (UsePullbackEntries && TryPullbackLongPhysics(out var slL, out var whyL))
    {
        trigBuy = true; slCandidate = slL;
        if (Verbose) Print("[TRIG][PB][BUY] {0} SL={1:F5}", whyL, slL);
    }
    if (UsePullbackEntries && !trigBuy &&
        TryPullbackShortPhysics(out var slS, out var whyS))
    {
        trigSell = true; slCandidate = slS;
        if (Verbose) Print("[TRIG][PB][SELL] {0} SL={1:F5}", whyS, slS);
    }

    // --- B) プルバックが立っていない側だけ ZZ ブレイク ---
if (UseZzBreakEntries)
{
    if (!trigBuy)
    {
        var okB = TryZigZagBreakLong(out var zzSlB, out var zzwhyBLocal);
        _zzWhyB = zzwhyBLocal;                          // ★ 先にフィールド更新
        if (okB)
        {
            trigBuy = true; slCandidate = zzSlB;
            if (Verbose) Print("[TRIG][ZZ][BUY] {0} SL={1:F5}", _zzWhyB, zzSlB);
        }
        else if (Verbose) Print("[REJ][ZZ][BUY] {0}", _zzWhyB);
    }

    if (!trigSell)
    {
        var okS = TryZigZagBreakShort(out var zzSlS, out var zzwhySLocal);
        _zzWhyS = zzwhySLocal;                          // ★ 先にフィールド更新
        if (okS)
        {
            trigSell = true; slCandidate = zzSlS;
            if (Verbose) Print("[TRIG][ZZ][SELL] {0} SL={1:F5}", _zzWhyS, zzSlS);
        }
        else if (Verbose) Print("[REJ][ZZ][SELL] {0}", _zzWhyS);
    }
}

}
     // ★ 可視化：直近スイング＆ブレイクしきい値＋ログ
if (DebugViz && DrawRobotLines)
DiagViz(Bars.Count - 1, trigBuy, trigSell);
// 2) 何も立ってなければ終了
if (!trigBuy && !trigSell) return;

// 4) デイリーティルト
double rAdj = RMultiple; int maxT = MaxTradesPerDay;
ApplyDailyTilt(ref rAdj, ref maxT);
_rMultipleOverride = rAdj;
if (_tradesToday >= maxT) { if (Verbose) Print("[BLOCK] daily max"); return; }

// 5) ゲート＆発注（立った側だけ）
if (trigBuy)
{
    if (DumpEntryGates(_label, TradeType.Buy, _atrC) && CanEnter(_label, TradeType.Buy))
        PlaceTrade(TradeType.Buy, slCandidate);
}
else // trigSell
{
    if (DumpEntryGates(_label, TradeType.Sell, _atrC) && CanEnter(_label, TradeType.Sell))
        PlaceTrade(TradeType.Sell, slCandidate);
}

        // === AFTER entries/triggers, manage exits & trailing ===
              // ZigZag Escape ONのときだけ発動
              if (UseZigZagEscape)    
              ManageZigZagEscape();

        // トレーリングは常時でもOK（必要ならフラグ化）
              TrailIfNeeded(_atrC);
}
        // 発注ユーティリティ（SLは「価格」、TPは 2R、数量はリスク％から自動算出）
        private void TryEnter(TradeType side, int i1, double slPrice, double volScale)
    {
        double entry = (side == TradeType.Buy) ? Symbol.Ask : Symbol.Bid;
        double spreadPips = Symbol.Spread / Symbol.PipSize;

        // 0) クールダウン
        int curBar = Bars.Count - 1;
        if (CooldownBars > 0 && curBar - lastTradeBar < CooldownBars)
        { REJ("COOL", $"wait {CooldownBars - (curBar - lastTradeBar)} bars"); return; }

        // 1) スプレッド
        if (SpreadMaxPips > 0 && spreadPips > SpreadMaxPips)
        { REJ("SPREAD", $"{spreadPips:F2}p > {SpreadMaxPips:F2}p"); return; }

        // 2) ストップ距離
        double stopPips = Math.Abs((slPrice - entry)) / Symbol.PipSize;
        double autoMin = Math.Max(5.0, 1.5 * spreadPips);
        double stopMin = (StopPipsMin > 0.0) ? StopPipsMin : autoMin;
        if (stopPips < stopMin)
        { REJ("STOP", $"stop {stopPips:F1}p < min {stopMin:F1}p"); return; }

        // 3) ボリューム（リスク％ & スケール）
        double unitsRaw = CalcUnitsByRisk(stopPips, RiskPct) * Math.Max(0.0, volScale);
        double units = Symbol.NormalizeVolumeInUnits(unitsRaw, RoundingMode.ToNearest);
        if (units < Symbol.VolumeInUnitsMin)
        { REJ("VOL", $"units {unitsRaw:F0} < min {Symbol.VolumeInUnitsMin:F0}"); return; }

        Print($"[ENTER TRY] {side} vol={units:F0} stop={stopPips:F1}p spread={spreadPips:F2}p");

        // 4) 発注
        var res = ExecuteMarketOrder(side, SymbolName, units, _label, stopPips, null, "ZZ");
        if (!res.IsSuccessful)
        { REJ("BROKER", res.Error?.ToString() ?? "unknown"); return; }

        lastTradeBar = curBar;
        Print($"[ENTER OK] {side} at {entry:F5} vol={units:F0} SL={slPrice:F5}");
    } 

// リスク％→数量(units) 変換（無ければ追加）
private double CalcUnitsByRisk(double stopPips, double riskPct)
{
    if (riskPct <= 0 || stopPips <= 0) return 0;
    double riskMoney = Account.Balance * (riskPct / 100.0);
    double pipValuePerUnit = Symbol.PipValue / Symbol.LotSize;   // 1通貨あたり1pipsの価値
    double units = riskMoney / (stopPips * pipValuePerUnit);
    // 取引可能レンジにクランプ
    units = Math.Max(Symbol.VolumeInUnitsMin, Math.Min(Symbol.VolumeInUnitsMax, units));
    return units;
}

        private bool PassesEnergyGate(out string why)
{
    why = "";
    double etot = Etot;            // 既存の総エネルギー指標
    double de   = Math.Abs(dE);    // 既存の変化量

    if (double.IsNaN(etot) || double.IsNaN(de))
    { why = "nan"; return false; }

    if (etot < EnergyMin) { why = "Etot<min"; return false; }
    if (etot > EnergyMax) { why = "Etot>max"; return false; }
    if (de   > MaxAbsDE)  { why = "|dE|>max"; return false; }

    return true;
}
        // ---------- Heavy compute ----------
        private bool ComputePhysicsAndCache()
        {
            if (Bars.Count < Math.Max(Lookback + 5, 30)) return false;

            double close = Bars.ClosePrices.LastValue;
            double atr   = _atr.Result.LastValue;
            // 既存: double close = Bars.ClosePrices.LastValue; double atr = _atr.Result.LastValue;
            if (atr < 1e-9 || double.IsNaN(atr) || double.IsInfinity(atr)) return false;
            if (double.IsNaN(close) || double.IsInfinity(close)) return false;

            // dtBar が 0/負になるケースを防ぐ
            double dtBar = Math.Max(1.0, (Bars.OpenTimes.Last(0) - Bars.OpenTimes.Last(1)).TotalSeconds);

            double hh  = Highest(Bars.HighPrices, Lookback);
            double ll  = Lowest(Bars.LowPrices, Lookback);
            double mid = 0.5 * (hh + ll);
            double ema = _ema?.Result.LastValue ?? double.NaN;
            //追加の安全チェック
            if (double.IsNaN(hh)  || double.IsInfinity(hh))  return false;
            if (double.IsNaN(ll)  || double.IsInfinity(ll))  return false;
            if (double.IsNaN(mid) || double.IsInfinity(mid)) return false;
            if (double.IsNaN(ema) || double.IsInfinity(ema)) return false;

            double bandDiff = close > hh ? (close - hh) / atr :
                              close < ll ? -(ll - close) / atr : 0.0;

            int steps = Math.Max(1, SubSteps);
            double dt  = 1.0 / steps;           // ✅ バー正規化

            // ループの前、x/v/a を決める直前に
            if (_lastComputedBar < 0) { _x = close; _v = 0.0; _a = 0.0; }

            double x = _x, v = _v, a = _a;

            double lastA0 = 0.0, lastA1 = 0.0;
            for (int s = 0; s < steps; s++)

            {
                double a0 = Accel(x, v, mid, ema, atr, bandDiff);
                double x1 = x + v * dt + 0.5 * a0 * dt * dt;
                double vPreview = v + a0 * dt;
                double a1 = Accel(x1, vPreview, mid, ema, atr, bandDiff);
                double v1 = v + 0.5 * (a0 + a1) * dt;
               
                x = x1; v = v1; a = a1;
                lastA0 = a0; lastA1 = a1;
            }
            if (Verbose)
            Print("[RAW] a0= {0:E6} a1= {1:E6} v= {2:E6} x= {3:E6}", lastA0,lastA1,v,x);
            // Kalman
                double sX, sV, sA;
                double nx = x, nv = v;
            if (UseKalman)
            {
                double qBase = KalmanQ * dtBar * dtBar;
                double P00n = _P00 + dtBar * (_P01 + _P10) + dtBar * dtBar * _P11 + qBase;
                double P01n = _P01 + dtBar * _P11;
                double P10n = _P10 + dtBar * _P11;
                double P11n = _P11 + qBase;

                double y = close - nx;
                double r = Math.Max(1e-12, KalmanR * (atr * atr) * (1.0 + Math.Min(5.0, Math.Abs(y) / Math.Max(1e-8, atr))));
                double S = P00n + r;
                double Kx = P00n / S;
                double Kv = P10n / S;

                sX = nx + Kx * y;
                sV = nv + Kv * y;
                sA = a;   
                _P00 = (1 - Kx) * P00n;
                _P01 = (1 - Kx) * P01n;
                _P10 = P10n - Kv * P00n;
                _P11 = P11n - Kv * P01n;
            
            }
            else
            {sX = nx; sV = nv; sA = a;}
             _x = sX; _v = sV; _a = sA;
           
            _hhC = hh; _llC = ll; _midC = mid; _emaC = ema; _atrC = atr; _bandDiffC = bandDiff;
            // ★ 異常値クランプ（ATRスケールで）
            double limV = _atrC * 1e6; double limA = _atrC * 1e7;
            if (!IsFinite(_v) || Math.Abs(_v) > limV) _v = Math.Sign(_v) * limV;
            if (!IsFinite(_a) || Math.Abs(_a) > limA) _a = Math.Sign(_a) * limA;

             // ★ Energy を更新（vr=|v|/ATR ベース）
             double et = (Math.Abs(_v) + Math.Abs(_a)) / Math.Max(1e-12, _atrC);
             dE   = et - _Eprev;
             Etot = et;
             _Eprev = et;

            _lastComputedBar = Bars.Count - 1;
            _physicsReady = true;
            return true;
        }
        // ---------- Acceleration ----------
        private double Accel(double x, double v, double _midC, double _emaC, double _atrC, double _bandDiffC)
{
    double mu = FrictionMu0 + (FrictionMu1 - FrictionMu0) *
                Math.Exp(-Sqr(Math.Abs(v) / Math.Max(1e-12, StribeckVStar)));
    double friction = -mu * Math.Sign(v);

    double spring1 = -SpringK1 * (x - _midC);
    double spring2 = -SpringK2 * (x - _emaC);
    double damping = -Damping * v;
    double drive   = DriveGain * _bandDiffC;

    // ★ ノイズを安全に
    double sigma = Math.Max(0.0, NoiseK) * Math.Max(0.0, _atrC);
    double noise = 0.0;
    if (sigma > 0.0 && !double.IsNaN(sigma) && !double.IsInfinity(sigma))
    {
        double z = Gaussian();
        if (!double.IsNaN(z) && !double.IsInfinity(z))
            noise = sigma * z;          // ここで NaN にならないように
    }

    double force = spring1 + spring2 + damping + friction + drive + noise;
    double mass  = Math.Max(1e-12, Mass);
    double a     = force / mass;

    if (double.IsNaN(a) || double.IsInfinity(a)) a = 0.0;  // 念のため最終ガード
    return a;
}
        // 価格だけで最後のスイング高値/安値を取る（fractal的判定）
private bool TryGetLastSwing(bool low, int back, int look, out int idx, out double px) {
    idx = -1; px = double.NaN;
    int start = Bars.Count - 2;
    int from  = Math.Max(back, start - look);
    for (int i = start - back; i >= from; i--) {
        bool ok = true;
        if (low) {
            double v = Bars.LowPrices[i];
            for (int k = -back; k <= back; k++) { if (v > Bars.LowPrices[i+k]) { ok=false; break; } }
            if (ok) { idx = i; px = v; return true; }
        } else {
            double v = Bars.HighPrices[i];
            for (int k = -back; k <= back; k++) { if (v < Bars.HighPrices[i+k]) { ok=false; break; } }
            if (ok) { idx = i; px = v; return true; }
        }
    }
    return false;
}

// 直近の「上昇レッグ L1→H1」 or 「下降レッグ H1→L1」を組む（互いに直近の反対スイング）
private bool TryGetLastLegUp(out int iL1, out double L1, out int iH1, out double H1) {
    iL1=iH1=-1; L1=H1=double.NaN;
    if (!TryGetLastSwing(true,  PbBack, PbLook, out iL1, out L1)) return false;
    // L1 より後で最初に出た swing high を採用
    int start = Bars.Count - 2;
    for (int i = start - PbBack; i > iL1; i--) {
        bool isHi = true; double v = Bars.HighPrices[i];
        for (int k = -PbBack; k <= PbBack; k++) { if (v < Bars.HighPrices[i+k]) { isHi=false; break; } }
        if (isHi) { iH1=i; H1=v; return true; }
    }
    return false;
}

private bool TryGetLastLegDown(out int iH1, out double H1, out int iL1, out double L1) {
    iL1=iH1=-1; L1=H1=double.NaN;
    if (!TryGetLastSwing(false, PbBack, PbLook, out iH1, out H1)) return false;
    int start = Bars.Count - 2;
    for (int i = start - PbBack; i > iH1; i--) {
        bool isLo = true; double v = Bars.LowPrices[i];
        for (int k = -PbBack; k <= PbBack; k++) { if (v > Bars.LowPrices[i+k]) { isLo=false; break; } }
        if (isLo) { iL1=i; L1=v; return true; }
    }
    return false;
}

    // === ZigZag Break Entry Helpers ===
    // Returns true if the current close price has broken above the most recent swing high plus a buffer.
    // When triggered, computes an optional stop-loss candidate based on the most recent swing low minus a buffer.
    private bool TryZigZagBreakLong(out double sl, out string why)
    {
        sl = double.NaN;
        why = string.Empty;

        // Determine the search window: use ZZ backstep for pivot width and PB lookback for the window length.
        int back = Math.Max(2, ZzBackStep);
        int look = Math.Max(PbLook, 50);

        // Get the latest swing high; if none found, no breakout.
        if (!TryGetLastSwing(false, back, look, out var idxH, out var swingHigh))
        { why = "no swingH"; return false; }
       
        // Compute the breakout threshold using both ATR multiplier and fixed pip buffer.
        double atr = _atrC;
        double bufBreak = Math.Max(ZzBreakPips * Symbol.PipSize, ZzBreakAtrX * atr);
        double trigger = swingHigh + bufBreak;

        // Current close price must exceed the trigger to qualify as a breakout.
        double close = Bars.ClosePrices.LastValue;
        if (double.IsNaN(close) || double.IsInfinity(close) || close <= trigger)
           { why = "close<=trigger"; return false; }

        // Compute stop-loss candidate based on the most recent swing low minus buffer.
        if (TryGetLastSwing(true, back, look, out var idxL, out var swingLow))
        {
            double bufSL = Math.Max(ZzBufferPips * Symbol.PipSize, ZzBufferAtrX * atr);
            sl = ToTickDown(swingLow - bufSL);
        }
        // Set descriptive reason for logging.
        why = $"ZigZagBreakLong swingH={swingHigh:F5} buf={bufBreak:F5}";
        return true;
    }

    // Returns true if the current close price has broken below the most recent swing low minus a buffer.
    // When triggered, computes an optional stop-loss candidate based on the most recent swing high plus a buffer.
    private bool TryZigZagBreakShort(out double sl, out string why)
    {
        sl = double.NaN;
        why = string.Empty;

        int back = Math.Max(2, ZzBackStep);
        int look = Math.Max(PbLook, 50);

        // Get the latest swing low; if none found, no breakout.
        if (!TryGetLastSwing(true, back, look, out var idxL, out var swingLow))
           { why = "no swingH"; return false; }

        // Compute the breakout threshold.
        double atr = _atrC;
        double bufBreak = Math.Max(ZzBreakPips * Symbol.PipSize, ZzBreakAtrX * atr);
        double trigger = swingLow - bufBreak;

        double close = Bars.ClosePrices.LastValue;
        if (double.IsNaN(close) || double.IsInfinity(close) || close >= trigger)
           { why = "close<=trigger"; return false; }

        // Stop-loss candidate from the most recent swing high plus buffer.
        if (TryGetLastSwing(false, back, look, out var idxH, out var swingHigh))
        {
            double bufSL = Math.Max(ZzBufferPips * Symbol.PipSize, ZzBufferAtrX * atr);
            sl = ToTickUp(swingHigh + bufSL);
        }
        why = $"ZigZagBreakShort swingL={swingLow:F5} buf={bufBreak:F5}";
        return true;
    }
        private bool TryPullbackLongPhysics(out double sl, out string why)
{
    sl = double.NaN; why = "";

    // 直近の上昇レッグ L1→H1 を取る
    if (!TryGetLastLegUp(out var iL1, out var L1, out var iH1, out var H1)) return false;
    if (!(iL1 < iH1) || (H1 - L1) <= Symbol.TickSize) return false;

    // 勢い判定（緩め）。EMA 勾配は緩和可能。
    bool emaUp = (_ema.Result.LastValue > _ema.Result.Last(3));
    if (!PbRelaxEmaSlope && PbNeedEmaSlope && !emaUp) return false;

    double atr = SoftATR();
    double vr  = Math.Abs(_v) / Math.Max(1e-12, atr);
    if (vr < PbMinVr) return false;

    // 押し目帯（勢いで可変）＋許容誤差
    double range = H1 - L1;
    var (fMin, fMax) = GetFibBand(vr);
    double zLo = H1 - fMax * range;
    double zHi = H1 - fMin * range;
    double tol = Math.Max(0.0, PbZoneTolPips) * Symbol.PipSize;
    double c   = Bars.ClosePrices.LastValue;

    // 帯に入ったら“武装”だけして、このバーは発火しない
    if (c >= zLo - tol && c <= zHi + tol)
    {
        _armLongBar = Bars.Count - 1;
        _armLongSL  = Math.Min(L1, zLo) - PbAtrBufX * atr;
        return false;
    }

    // 武装後、数本以内に小ブレイクが出たら発火
    bool armed = (Bars.Count - 1 - _armLongBar) <= Math.Max(1, PbArmBars);
    if (armed && BreakoutUp(Math.Max(1, PbConfirmLookback)) && MicroConfirmLong())
    {
        sl  = double.IsNaN(_armLongSL) ? (Math.Min(L1, zLo) - PbAtrBufX * atr) : _armLongSL;
        why = $"PB-L armed vr={vr:F2} emaUp={emaUp}";
        // 使い切ったので解除
        _armLongBar = -9999; _armLongSL = double.NaN;
        return true;
    }

    return false;
}

private bool TryPullbackShortPhysics(out double sl, out string why)
{
    sl = double.NaN; why = "";

    if (!TryGetLastLegDown(out var iH1, out var H1, out var iL1, out var L1)) return false;
    if (!(iH1 < iL1) || (H1 - L1) <= Symbol.TickSize) return false;

    bool emaDn = (_ema.Result.LastValue < _ema.Result.Last(3));
    if (!PbRelaxEmaSlope && PbNeedEmaSlope && !emaDn) return false;

    double atr = SoftATR();
    double vr  = Math.Abs(_v) / Math.Max(1e-12, atr);
    if (vr < PbMinVr) return false;

    double range = H1 - L1;
    var (fMin, fMax) = GetFibBand(vr);
    double zHi = L1 + fMax * range;
    double zLo = L1 + fMin * range;
    double tol = Math.Max(0.0, PbZoneTolPips) * Symbol.PipSize;
    double c   = Bars.ClosePrices.LastValue;

    if (c <= zHi + tol && c >= zLo - tol)
    {
        _armShortBar = Bars.Count - 1;
        _armShortSL  = Math.Max(H1, zHi) + PbAtrBufX * atr;
        return false;
    }

    bool armed = (Bars.Count - 1 - _armShortBar) <= Math.Max(1, PbArmBars);
    if (armed && BreakoutDown(Math.Max(1, PbConfirmLookback)) && MicroConfirmShort())
    {
        sl  = double.IsNaN(_armShortSL) ? (Math.Max(H1, zHi) + PbAtrBufX * atr) : _armShortSL;
        why = $"PB-S armed vr={vr:F2} emaDn={emaDn}";
        _armShortBar = -9999; _armShortSL = double.NaN;
        return true;
    }

    return false;
}

        private void ManageZigZagEscape()
{
    if (!UseZigZagEscape) return;

    double atr = _atrC;
    double buf = Math.Max(ZzBufferPips * Symbol.PipSize, ZzBufferAtrX * atr);

    foreach (var p in Positions)
    {
        if (p.SymbolName != SymbolName || !p.Label.StartsWith(LabelPrefix)) continue;

        // 参照価格（バックテスト時のBid/Ask落ち対策）
        double px = (p.TradeType == TradeType.Buy) ? Symbol.Bid : Symbol.Ask;
        if (px <= 0 || double.IsNaN(px) || double.IsInfinity(px)) px = Bars.ClosePrices.LastValue;
        if (double.IsNaN(px) || double.IsInfinity(px)) continue;

        // フラクタル幅は ZzBackStep（≥2）、探索窓は Pullback の PbLook を流用
        int back  = Math.Max(2, ZzBackStep);
        int look  = Math.Max(PbLook, 50);

        if (p.TradeType == TradeType.Buy)
        {
            // 直近のスイング安値
            if (TryGetLastSwing(true, back, look, out var idx, out var swingLow))
            {
                double cand = ToTickDown(swingLow - buf);

                // 既存SLより“良くなる”時だけ更新
                bool improves = !p.StopLoss.HasValue || cand > p.StopLoss.Value;

                // 取引所/自前の最小距離ガード
                double dPricePips = Math.Abs(px - cand) / Symbol.PipSize;
                double dEntryPips = Math.Abs(p.EntryPrice - cand) / Symbol.PipSize;
                bool farEnough = dPricePips >= Math.Max(0.0, MinSLFromPricePips)
                                 && dEntryPips >= Math.Max(1, MinStopsPips);

                if (improves && farEnough)
                {
                    var r = ModifyPosition(p, cand, p.TakeProfit, ProtectionType.None);
                    if (Verbose)
                        Print("[ZZ-ESC][BUY] SL -> {0:F5} (swingL={1:F5}, buf={2:F5}) ok={3}",
                              cand, swingLow, buf, r.IsSuccessful);
                }
            }
        }
        else // SELL
        {
            if (TryGetLastSwing(false, back, look, out var idx, out var swingHigh))
            {
                double cand = ToTickUp(swingHigh + buf);

                bool improves = !p.StopLoss.HasValue || cand < p.StopLoss.Value;
                double dPricePips = Math.Abs(px - cand) / Symbol.PipSize;
                double dEntryPips = Math.Abs(p.EntryPrice - cand) / Symbol.PipSize;
                bool farEnough = dPricePips >= Math.Max(0.0, MinSLFromPricePips)
                                 && dEntryPips >= Math.Max(1, MinStopsPips);

                if (improves && farEnough)
                {
                    var r = ModifyPosition(p, cand, p.TakeProfit, ProtectionType.None);
                    if (Verbose)
                        Print("[ZZ-ESC][SELL] SL -> {0:F5} (swingH={1:F5}, buf={2:F5}) ok={3}",
                              cand, swingHigh, buf, r.IsSuccessful);
                }
            }
        }
    }
}
       
        private void LogJson(string ev, string kv)
{
    if (!Verbose) return;
    // 例: {"ts":"2025-08-31T12:34:56.789Z","ev":"gate","sym":"EURUSD","tf":"Minute5", ...}
    Print("{\"ts\":\"" + Server.Time.ToString("O") + "\",\"ev\":\"" + ev +
          "\",\"sym\":\"" + SymbolName + "\",\"tf\":\"" + Bars.TimeFrame +
          "\"," + kv + "}");
}
     
          private bool TryHiLoSince(Position p, out double hi, out double lo)
{
    hi = double.MinValue; lo = double.MaxValue;
    // p.EntryTime 以降のバー範囲を探す（現バー含む）
    int start = Bars.Count - 1;
    for (int i = Bars.Count - 1; i >= 1; i--)
    {
        if (Bars.OpenTimes[i] <= p.EntryTime) { start = Math.Max(i, 1); break; }
        start = i;
    }
    if (start >= Bars.Count) return false;

    for (int i = start; i < Bars.Count; i++)
    {
        hi = Math.Max(hi, Bars.HighPrices[i]);
        lo = Math.Min(lo, Bars.LowPrices[i]);
    }
    return hi > double.MinValue && lo < double.MaxValue;
}
        // どこか共通の場所に追加
        private bool DumpEntryGates(string label, TradeType side, double atr)
{
    bool cd     = InCooldown();
    bool max    = (MaxTradesPerDay > 0) && (_tradesToday >= MaxTradesPerDay);
    bool open   = HasOpen(label, side);
    bool spr    = SpreadTooWide();
    bool atrBad = (atr <= 0 || double.IsNaN(atr) || double.IsInfinity(atr));

    // 理由を配列化
    var sb = new StringBuilder();
    sb.Append("\"bar\":").Append(Bars.Count - 1)
      .Append(",\"side\":\"").Append(side).Append("\"")
      .Append(",\"price\":").Append(Bars.ClosePrices.LastValue.ToString("F5"))
      .Append(",\"atr\":").Append(atr.ToString("F6"))
      .Append(",\"spreadPips\":").Append((Symbol.Spread / Symbol.PipSize).ToString("F2"));

    // reasons
    sb.Append(",\"reasons\":[");
    bool first = true;
    void add(string r){ if(!first) sb.Append(','); sb.Append('"').Append(r).Append('"'); first=false; }

    if (cd)     add("cooldown");
    if (max)    add("maxTrades");
    if (open)   add("hasOpenSameSide");
    if (spr)    add("spreadTooWide");
    if (atrBad) add("atrInvalid");

    sb.Append("]");

    bool allow = !(cd || max || open || spr || atrBad);
    sb.Append(",\"decision\":\"").Append(allow ? "enter" : "block").Append("\"");

    LogJson("gate", sb.ToString());
    return allow;
}
        // ---------- Orders ----------
        private void PlaceTrade(TradeType side, double slOverride)
        {
            double entry = Bars.ClosePrices.LastValue;
            double atr   = Math.Max(1e-10, _atrC);
            double kSL   = Math.Max(0.3, TrailAtrK);
            double slPrice = (side == TradeType.Buy) ? entry - kSL * atr : entry + kSL * atr;
 
            double tpPrice = (side == TradeType.Buy)
            ? entry + RMultiple * kSL * atr : entry - RMultiple * kSL * atr;

// rMultiple を DailyTilt 等で上書き
if (!double.IsNaN(_rMultipleOverride)) {
    double r = Math.Max(0.5, _rMultipleOverride);
    tpPrice = (side == TradeType.Buy)
        ? entry + r * kSL * atr
        : entry - r * kSL * atr;
    _rMultipleOverride = double.NaN;
}

            slPrice = RoundToTick(slPrice);
            tpPrice = RoundToTick(tpPrice);

           // Pullback がSLを算出していれば最優先で使用（slOverride → _initialSLOverride の順）
           if (!double.IsNaN(slOverride)) {slPrice = RoundToTick(slOverride);}
           else if (!double.IsNaN(_initialSLOverride)) { slPrice = RoundToTick(_initialSLOverride);_initialSLOverride = double.NaN;}

            // 正しい向きの確認：Buy の場合は SL < entry < TP、Sell の場合は SL > entry > TP
            bool okDir = (side == TradeType.Buy  && slPrice < entry && tpPrice > entry) ||
                          (side == TradeType.Sell && slPrice > entry && tpPrice < entry);
            if (!okDir) { if (Verbose) Print("[BLOCK] DIR"); return; }

            int slPips = Math.Max(1, ToPips(entry, slPrice));  // 四捨五入 + 例外ガード
            int tpPips = Math.Max(1, ToPips(tpPrice, entry));

            int minPips = (int)Math.Max(0, MinStopsPips);
            if (minPips > 0)
        {
            slPips = Math.Max(slPips, minPips);
            tpPips = Math.Max(tpPips, minPips);
        }
            
            double volCalc = CalcPositionUnits(atr);
            //取引所制約でクランプ
            double vMin  =Symbol.VolumeInUnitsMin, vMax  =Symbol.VolumeInUnitsMax, vStep =Symbol.VolumeInUnitsStep;

            double vol =Math.Min(vMax,Math.Max(vMin,volCalc));
            double volQ = Math.Round(vol /vStep) *vStep;
            long volL = (long)Math.Round (volQ);
            if (volL <= 0){ if (Verbose) Print("[BLOCK]vol==0"); return;}

            // …entry/slPrice/tpPrice/slPips/tpPips/volUnits を計算し終えた直後に置く
long v = (long)Math.Round(vol);
if (v <= 0) { Print("[ORDER SKIP] volume=0"); return; }

if (double.IsNaN(entry) || double.IsNaN(slPrice) || double.IsNaN(tpPrice) ||
    double.IsInfinity(entry) || double.IsInfinity(slPrice) || double.IsInfinity(tpPrice))
{ Print("[ORDER SKIP] NaN/Inf entry={0} sl={1} tp={2}", entry, slPrice, tpPrice); return; }

// ★ 発注直前ログ（ここが“発注直前”）
Print("[ORDER PRE] {0} side={1} v={2} slPips={3} tpPips={4} entry={5} sl={6} tp={7}",
      SymbolName, side, v, slPips, tpPips, entry, slPrice, tpPrice);

             LogJson("order_pre", "\"side\":\"" + side + "\",\"v\":" + v +",\"entry\":" + entry.ToString("F5") +
       ",\"sl\":" + slPrice.ToString("F5") + ",\"tp\":" + tpPrice.ToString("F5") +",\"slPips\":" + slPips + ",\"tpPips\":" + tpPips);
            var res = ExecuteMarketOrder(side,SymbolName,(long)volL,_label,slPips,tpPips);
           
            if (!res.IsSuccessful)
            {
                Print("[AUDIT] ORDER ERR {0}",res.Error);
                LogJson("order_err", "\"err\":\"" + res.Error + "\"");
                return;
            }
            // ★ 初期SL/TPの保険（未設定なら即付与）
            var pos = res.Position;
            if (!pos.StopLoss.HasValue || !pos.TakeProfit.HasValue)
            { var fix = ModifyPosition(pos, slPrice, tpPrice, ProtectionType.None);
              Print("[AUDIT] INIT SL/TP via ModifyPosition ok={0}", fix.IsSuccessful);}

            if (MaxMarketRangePips > 0)
        {
            double slipPips = Math.Abs(res.Position.EntryPrice - entry) / Symbol.PipSize;
            if (slipPips > MaxMarketRangePips)
            {
                ClosePosition(res.Position);
            if (Verbose) Print("[AUDIT] CANCEL by slippage {0:F1}p > {1}p", slipPips, MaxMarketRangePips);
            return;   // または goto AfterEntry; 既存フローに合わせる
            }
        }
            {
                _lastTrade  =Server.Time; _tradesToday++;
            if (Verbose) Print("[AUDIT] ORDER {0} vol={1} SL={2}p TP={3}p", side,volL,slPips,tpPips);
            }
        }

        private double CalcPositionUnits(double atr)
{
    // ATRが壊れているときは固定Unitsで返す
    if (atr <= 0 || double.IsNaN(atr) || double.IsInfinity(atr))
        return Symbol.NormalizeVolumeInUnits(Units, RoundingMode.ToNearest);

    // Risk管理を使わない（=0 以下）時も固定Units
    if (RiskPct <= 0)
        return Symbol.NormalizeVolumeInUnits(Units, RoundingMode.ToNearest);

    // --- ここからリスク％計算 ---
    double slPrice  = Math.Max(Symbol.TickSize, TrailAtrK * atr);
    double ticks    = slPrice / Symbol.TickSize;
    double tickValue= Symbol.TickValue;
    if (tickValue <= 0 || double.IsNaN(ticks) || double.IsInfinity(ticks))
        return Symbol.NormalizeVolumeInUnits(Units, RoundingMode.ToNearest);

    double riskMoney = Account.Balance * (RiskPct / 100.0);
    double rawUnits  = riskMoney / Math.Max(1e-9, ticks * tickValue);

    // ステップ・最小/最大を厳守させる
    double norm = Symbol.NormalizeVolumeInUnits(rawUnits, RoundingMode.ToNearest);
    norm = Math.Min(Symbol.VolumeInUnitsMax, Math.Max(Symbol.VolumeInUnitsMin, norm));
    if (norm < 1)  // 念のため
        norm = Symbol.NormalizeVolumeInUnits(Units, RoundingMode.ToNearest);

    return norm;
}

      private void TrailIfNeeded(double atr)
{
    if (atr <= 0 || double.IsNaN(atr) || double.IsInfinity(atr)) return;

    foreach (var p in Positions)
    {
        if (p.SymbolName != SymbolName || !p.Label.StartsWith(LabelPrefix)) continue;

        // 参照価格（バックテストのバー足では Bid/Ask が荒れることがあるのでフォールバック）
        double px = (p.TradeType == TradeType.Buy) ? Symbol.Bid : Symbol.Ask;
        if (px <= 0 || double.IsNaN(px) || double.IsInfinity(px)) px = Bars.ClosePrices.LastValue;
        if (double.IsNaN(px) || double.IsInfinity(px)) continue;

        // --- Chandelier or 旧式(px基準) ---
        double basePrice;
        if (TrailChandelier && TryHiLoSince(p, out double hi, out double lo))
        {
            basePrice = (p.TradeType == TradeType.Buy)
                ? hi - TrailAtrK * atr
                : lo + TrailAtrK * atr;
        }
        else
        {
            // 旧式：現在値から ATR×k
            basePrice = (p.TradeType == TradeType.Buy)
                ? px - TrailAtrK * atr
                : px + TrailAtrK * atr;
        }

        // 価格が現在値を跨がないよう 1tick マージン
        double bounded = (p.TradeType == TradeType.Buy)
            ? Math.Min(basePrice, px - Symbol.TickSize)
            : Math.Max(basePrice, px + Symbol.TickSize);

        // 方向保証で丸め（逆側に丸まらない）
        double newSL = (p.TradeType == TradeType.Buy) ? ToTickDown(bounded) : ToTickUp(bounded);

        bool notBetter = p.StopLoss.HasValue && ((p.TradeType == TradeType.Buy && newSL <= p.StopLoss.Value) ||
        (p.TradeType == TradeType.Sell && newSL >= p.StopLoss.Value));
        if (notBetter)
        {LogJson("trail_skip", "\"why\":\"notBetter\"");continue;}
        // 既存より“良くなる”ときだけ
        if (p.StopLoss.HasValue)
        {
            if (p.TradeType == TradeType.Buy  && newSL <= p.StopLoss.Value) continue;
            if (p.TradeType == TradeType.Sell && newSL >= p.StopLoss.Value) continue;

            // “最小ステップ”を満たさない更新は無視（細かい更新の無駄打ち回避）
            double step = Math.Abs(newSL - p.StopLoss.Value) / Symbol.PipSize;
            if (step < Math.Max(0.0, MinTrailStepPips)) 
            { LogJson("trail_skip", "\"why\":\"smallStep\",\"stepPips\":" + step.ToString("F2"));continue;}
        }
        
        // ブローカーの最小距離（現在値基準）を自前でガード
        double distFromPricePips = Math.Abs(px - newSL) / Symbol.PipSize;
        if (distFromPricePips < Math.Max(0.0, MinSLFromPricePips)) 
        {LogJson("trail_skip", "\"why\":\"tooCloseToPrice\",\"dist\":" + distFromPricePips.ToString("F2"));continue;}
        // エントリー基準の最小距離（既存パラメータ）も維持
        double distFromEntryPips = Math.Abs(p.EntryPrice - newSL) / Symbol.PipSize;
        if (distFromEntryPips < Math.Max(1, MinStopsPips)) 
        {LogJson("trail_skip", "\"why\":\"tooCloseToEntry\",\"dist\":" + distFromEntryPips.ToString("F2"));continue;}
        
        var mod = ModifyPosition(p, newSL, p.TakeProfit, ProtectionType.None);
        if (!mod.IsSuccessful)
        {
            if (Verbose) Print("[TRAIL][ERR] {0}", mod.Error);
            LogJson("trail_err",
                "\"side\":\"" + p.TradeType + "\",\"px\":" + px.ToString("F5") +
                ",\"oldSL\":" + (p.StopLoss ?? 0).ToString("F5") +
                ",\"newSL\":" + newSL.ToString("F5") +
                ",\"atr\":" + atr.ToString("F6"));
        }
        else
        {
            if (Verbose) Print("[TRAIL] {0} SL -> {1:F5}", p.TradeType, newSL);
            LogJson("trail_mod",
                "\"side\":\"" + p.TradeType + "\",\"px\":" + px.ToString("F5") +
                ",\"newSL\":" + newSL.ToString("F5") +
                ",\"atr\":" + atr.ToString("F6"));
        }
    }
}
        // ---------- Guards & Utils ----------
        private bool HandleGapGuard(double atr)
        {
            if (Bars.Count < 3) return false;

            double prevClose  = Bars.ClosePrices.Last(1);
            double prev2Close = Bars.ClosePrices.Last(2);
            double gap = Math.Abs(prevClose - prev2Close);

            TimeSpan typical = Bars.OpenTimes.Last(1) - Bars.OpenTimes.Last(2);
            TimeSpan dt = Bars.OpenTimes.LastValue - Bars.OpenTimes.Last(1);
            bool sessionJump = dt > typical + typical / 2;

            if (_gapCooldown > 0)
            {
                _gapCooldown--;
                if (Verbose) Print("[AUDIT] GAP-COOLDOWN left={0}", _gapCooldown);
                return true;
            }

            if (sessionJump && gap > GapK * atr)
            {
                _gapCooldown = GapCooldownBars;
                Print("[AUDIT] GAP SKIP dt={0} gap={1:F6} atr={2:F6} cooldown={3}", dt, gap, atr, _gapCooldown);
                return true;
            }
            return false;
        }
            // priceA と priceB の距離を pips に変換（最低1pip、NaN対策つき）
        private int ToPips(double priceA, double priceB)
    {
            double d = Math.Abs(priceA - priceB) / Symbol.PipSize;
            if (double.IsNaN(d) || double.IsInfinity(d)) return 1;
            return Math.Max(1, (int)Math.Round(d));
    }

        private void CloseAll(TradeType side, string label)
    {
            string prefix = LabelPrefix;
            foreach (var p in Positions)
        {
            if (p.SymbolName != SymbolName) continue;
            if (p.Label == null || !p.Label.StartsWith(prefix)) continue;
            if (p.TradeType != side) continue;

            var r = ClosePosition(p);
            if (Verbose) Print("[AUDIT] CLOSE {0} #{1} ok={2}", side, p.Id, r.IsSuccessful);
        }
    }

        private bool InCooldown()
        {
            if (CooldownBars <= 0) return false;
            int curBar = Bars.Count - 1;
            return curBar - lastTradeBar < CooldownBars;
        }

        private static double Highest(DataSeries s, int n)
        {
            double v = double.MinValue; int end = s.Count - 1;
            for (int i = end; i > end - n && i >= 0; i--) v = Math.Max(v, s[i]);
            return v;
        }

        private static double Lowest(DataSeries s, int n)
        {
            double v = double.MaxValue; int end = s.Count - 1;
            for (int i = end; i > end - n && i >= 0; i--) v = Math.Min(v, s[i]);
            return v;
        }

        private double RoundToTick(double price) => Math.Round(price / Symbol.TickSize) * Symbol.TickSize;

        private double Gaussian()
    {
        // 0 と 1 を避ける（クランプ）
        double u1 = _rng.NextDouble();
        double u2 = _rng.NextDouble();
        u1 = Math.Max(1e-12, Math.Min(1.0 - 1e-12, u1));

        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        // 念のためのガード
        if (double.IsNaN(z) || double.IsInfinity(z)) return 0.0;
        return z;
    }
        private void EnforceFridayFlat()
{
    if (ForceFlatFriday != Toggle.ON) return;

    var now = Server.Time;
    if (now.DayOfWeek != DayOfWeek.Friday) return;

    var flatTs = new TimeSpan(FridayFlatHour, FridayFlatMinute, 0);

    // 同じ金曜の日付で1回だけ実行
    if (now.TimeOfDay >= flatTs && _lastFridayFlat.Date != now.Date)
    {
        // 1) 全ポジションをクローズ
        foreach (var p in Positions.FindAll(_label, SymbolName))
            ClosePosition(p);

        // 2) 指値/逆指値もキャンセル（任意）
        if (CancelPendingAfterFlat == Toggle.ON)
        {
            foreach (var o in PendingOrders)
                if (o.SymbolName == SymbolName && o.Label == _label)
                    CancelPendingOrder(o);
        }

        Print($"[FRIDAY-FLAT] all positions closed at {now:yyyy-MM-dd HH:mm} (server)");

        _lastFridayFlat = now.Date;

        // 3) その週は新規停止（翌営業日に RollDailyIfNeeded で解除されます）
        if (BlockAfterFridayFlat == Toggle.ON)
            _suspended = true;
    }
}

        // === 可視化（スイング＆ZZブレイクの“見える化”）===
private void DiagViz(int i1, bool trigBuy, bool trigSell)
{
    // 入力値
    double close = Bars.ClosePrices[i1];
    double atr   = _atrC; // 既に ComputePhysicsAndCache で更新済み
    double spreadPips = Symbol.Spread / Symbol.PipSize;

    // 直近スイングの取得：上昇脚 or 下降脚を優先的に採用
    int iH1=-1, iL1=-1; double H1=double.NaN, L1=double.NaN;
    bool hasLegUp   = TryGetLastLegUp(out iL1, out L1, out iH1, out H1);
    bool hasLegDown = (!hasLegUp) && TryGetLastLegDown(out iH1, out H1, out iL1, out L1);
    if (!(hasLegUp || hasLegDown)) return;

    // ブレイクの最小幅（ATR× or pips の大きい方）
    double minBreak = Math.Max(ZzBreakPips * Symbol.PipSize, ZzBreakAtrX * atr);
    double breakUp  = double.IsNaN(H1) ? double.NaN : (H1 + minBreak);
    double breakDn  = double.IsNaN(L1) ? double.NaN : (L1 - minBreak);

    // しきい値到達の“素朴判定”
    bool canBuy  = !double.IsNaN(breakUp) && close > breakUp;
    bool canSell = !double.IsNaN(breakDn) && close < breakDn;

    // ---- ログ（このバーで何が足りないかを1行で）----
    Print($"[DIAG] bar={i1} close={close:F5} ATR={atr:F5} spr={spreadPips:F2} "
        + $"H1={H1:F5} L1={L1:F5} brkUp={breakUp:F5} brkDn={breakDn:F5} "
        + $"canBuy={canBuy} canSell={canSell} trigBuy={trigBuy} trigSell={trigSell}");

    // ---- チャート描画（水平線 & 矢印）----
    // スイング
    if (!double.IsNaN(H1))
        Chart.DrawHorizontalLine($"zz_H1_{i1}", H1, Color.SteelBlue, 1, LineStyle.Dots);
    if (!double.IsNaN(L1))
        Chart.DrawHorizontalLine($"zz_L1_{i1}", L1, Color.IndianRed, 1, LineStyle.Dots);

    // ブレイクしきい値
    if (!double.IsNaN(breakUp))
        Chart.DrawHorizontalLine($"zz_BU_{i1}", breakUp, Color.LimeGreen, 1, LineStyle.Solid);
    if (!double.IsNaN(breakDn))
        Chart.DrawHorizontalLine($"zz_BD_{i1}", breakDn, Color.OrangeRed, 1, LineStyle.Solid);

    // トリガー表示（立った側に矢印）
    if (trigBuy)
        Chart.DrawIcon($"TRIG_BUY_{i1}", ChartIconType.UpArrow, Bars.OpenTimes[i1],
                       Bars.LowPrices[i1] * 0.998, Color.Lime);
    if (trigSell)
        Chart.DrawIcon($"TRIG_SELL_{i1}", ChartIconType.DownArrow, Bars.OpenTimes[i1],
                       Bars.HighPrices[i1] * 1.002, Color.Red);
}
    }
}