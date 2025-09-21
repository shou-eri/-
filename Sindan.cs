using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    /// <summary>
    /// Simple toggle enumeration for enabling/disabling features
    /// </summary>
    public enum Toggle 
    { 
        /// <summary>Feature is disabled</summary>
        OFF, 
        /// <summary>Feature is enabled</summary>
        ON 
    }

    /// <summary>
    /// Energy mode enumeration for controlling energy-based market filtering
    /// </summary>
    public enum EnergyMode 
    { 
        /// <summary>Energy filtering is disabled</summary>
        Off, 
        /// <summary>Block trades when energy conditions are not met</summary>
        Block, 
        /// <summary>Scale position size based on energy levels</summary>
        Scale, 
        /// <summary>Tighten entry requirements when energy is high</summary>
        Tighten 
    }

    /// <summary>
    /// Galileo Ultra Physics V5 - Advanced Physics-Based Trading Robot
    /// 
    /// This sophisticated trading robot implements a physics-based approach to market analysis and trading,
    /// combining multiple advanced techniques:
    /// 
    /// Key Features:
    /// - Physics Engine: Simulates price movement using mass, damping, springs, and friction
    /// - Kalman Filtering: Advanced noise reduction and state estimation
    /// - ZigZag Analysis: Swing-based breakout detection and trend following
    /// - Pullback Entries: Fibonacci-based retracement strategy with dynamic zones
    /// - Energy Gating: Market volatility filtering and position sizing
    /// - Multi-timeframe Analysis: Comprehensive market structure evaluation
    /// - Risk Management: Advanced trailing stops, position sizing, and drawdown protection
    /// - Diagnostic Tools: Comprehensive logging and visualization capabilities
    /// 
    /// The robot uses a physical model where price is treated as a particle subject to various forces,
    /// allowing for sophisticated prediction and analysis of market movements.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GalileoUltraPhysicsV5 : Robot
    {
        #region Trading Parameters
        
        /// <summary>
        /// Label prefix for identifying trades created by this robot instance
        /// Used to distinguish this robot's trades from others on the same symbol
        /// </summary>
        [Parameter("Label Prefix", Group = "Trading Parameters", DefaultValue = "GAL_U4")]
        public string LabelPrefix { get; set; }

        /// <summary>
        /// Fallback position size in units when risk percentage is disabled (set to 0)
        /// This provides a fixed position size for manual risk management
        /// </summary>
        [Parameter("Units (fallback)", Group = "Trading Parameters", DefaultValue = 10000, MinValue = 1)]
        public int Units { get; set; }

        /// <summary>
        /// Risk percentage of account balance per trade. Set to 0 to use fixed units instead.
        /// This controls position sizing based on account balance and stop loss distance
        /// Valid range: 0-5% to prevent excessive risk
        /// </summary>
        [Parameter("Risk % (0=fixed Units)", Group = "Trading Parameters", DefaultValue = 1.0, MinValue = 0, MaxValue = 5)]
        public double RiskPercent { get; set; }

        /// <summary>
        /// Minimum time in minutes between trades to prevent overtrading
        /// Set to 0 to disable cooldown period
        /// </summary>
        [Parameter("Cooldown (min)", Group = "Trading Parameters", DefaultValue = 0)]
        public int CooldownMin { get; set; }

        /// <summary>
        /// Maximum number of trades allowed per trading day. Set to 0 for unlimited.
        /// Helps control daily exposure and prevents excessive trading
        /// </summary>
        [Parameter("Max Trades / Day", Group = "Trading Parameters", DefaultValue = 0)]
        public int MaxTradesPerDay { get; set; }

        /// <summary>
        /// Enable daily performance-based adjustments to risk and trade limits
        /// Automatically adjusts parameters based on daily win rate performance
        /// </summary>
        [Parameter("Use Daily Tilt", Group="Trading Parameters", DefaultValue = true)]
        public bool UseDailyTilt { get; set; }

        /// <summary>
        /// Risk percentage for position sizing calculations
        /// Alternative risk parameter for enhanced risk management
        /// </summary>
        [Parameter("Risk %", Group = "Trading Parameters", DefaultValue = 0.5, MinValue = 0, MaxValue = 10)]
        public double RiskPct { get; set; }

        #endregion

        #region Signal and Market Analysis Parameters
        
        /// <summary>
        /// Number of bars to look back for high/low analysis and market structure
        /// Higher values provide more stable signals but may be slower to react
        /// Valid range: 10-500 bars
        /// </summary>
        [Parameter("Lookback", Group = "Signal and Market Analysis", DefaultValue = 50, MinValue = 10)]
        public int Lookback { get; set; }

        /// <summary>
        /// Period for Average True Range calculation used throughout the system
        /// ATR is fundamental for volatility-based calculations and risk management
        /// Standard value is 14 periods
        /// </summary>
        [Parameter("ATR Period", Group = "Signal and Market Analysis", DefaultValue = 14)]
        public int AtrPeriod { get; set; }

        /// <summary>
        /// Period for Exponential Moving Average used in physics engine and trend analysis
        /// Longer periods provide smoother trend identification
        /// Valid range: 50-500 periods
        /// </summary>
        [Parameter("EMA Period", Group = "Signal and Market Analysis", DefaultValue = 200)]
        public int EmaPeriod { get; set; }

        #endregion

        #region Risk Management Parameters
        
        /// <summary>
        /// ATR multiplier for trailing stop calculations
        /// Higher values give more room for price fluctuations but larger potential losses
        /// Typical range: 0.5-2.0
        /// </summary>
        [Parameter("Trail k (ATR)", Group = "Risk Management", DefaultValue = 0.8)]
        public double TrailAtrK { get; set; }

        /// <summary>
        /// Take profit multiplier as a ratio of stop loss distance (Risk:Reward ratio)
        /// 1.5 means take profit is 1.5x the stop loss distance
        /// Higher values target larger profits but reduce win rate
        /// </summary>
        [Parameter("TP R:R", Group = "Risk Management", DefaultValue = 1.5)]
        public double RMultiple { get; set; }

        /// <summary>
        /// Maximum allowed spread in pips before blocking trades
        /// Set to 0 to disable spread filtering
        /// Prevents trading during high spread conditions
        /// </summary>
        [Parameter("Max Spread (pips)", Group = "Risk Management", DefaultValue = 0.0, MinValue = 0.0)]
        public double MaxSpreadPips { get; set; }

        /// <summary>
        /// Maximum market range in pips to prevent trading in overly volatile conditions
        /// Set to 0 to disable this filter
        /// </summary>
        [Parameter("Max Market Range (pips)", Group = "Risk Management", DefaultValue = 0, MinValue = 0)]
        public int MaxMarketRangePips { get; set; }

        /// <summary>
        /// Minimum distance in pips that trailing stop must maintain from current price
        /// Prevents stops from being placed too close to current market price
        /// </summary>
        [Parameter("Trail: Min dist from price (pips)", Group="Risk Management", DefaultValue = 0.5, MinValue = 0.0)]
        public double MinSLFromPricePips { get; set; }

        /// <summary>
        /// Use Chandelier-style trailing stops based on highest high/lowest low since entry
        /// When enabled, stops trail based on extreme prices rather than current price
        /// </summary>
        [Parameter("Trail mode: Chandelier", Group="Risk Management", DefaultValue = true)]
        public bool TrailChandelier { get; set; }

        /// <summary>
        /// Hour of day (server time) when daily trade counters reset
        /// Used for managing daily trade limits and statistics
        /// Valid range: 0-23 hours
        /// </summary>
        [Parameter("Daily reset hour (server time)", Group="Risk Management", DefaultValue = 0, MinValue = 0, MaxValue = 23)]
        public int DailyResetHour { get; set; }

        /// <summary>
        /// Automatically clear robot-drawn lines when daily reset occurs
        /// Helps maintain clean charts during extended operation
        /// </summary>
        [Parameter("Clear lines on daily reset", Group="Risk Management", DefaultValue = false)]
        public bool ClearLinesOnDailyReset { get; set; }

        /// <summary>
        /// Force closure of all positions on Friday at specified time
        /// Helps avoid weekend gaps and reduces weekend risk exposure
        /// </summary>
        [Parameter("Force Flat on Friday", Group = "Risk Management", DefaultValue = Toggle.ON)]
        public Toggle ForceFlatFriday { get; set; }

        /// <summary>
        /// Hour on Friday when forced position closure occurs
        /// Should be set before market close to ensure execution
        /// Valid range: 0-23 hours
        /// </summary>
        [Parameter("Friday Flat Hour (server time)", Group = "Risk Management", DefaultValue = 21, MinValue = 0, MaxValue = 23)]
        public int FridayFlatHour { get; set; }

        /// <summary>
        /// Minute within the Friday flat hour when closure occurs
        /// Provides precise timing for position closure
        /// Valid range: 0-59 minutes
        /// </summary>
        [Parameter("Friday Flat Minute", Group = "Risk Management", DefaultValue = 55, MinValue = 0, MaxValue = 59)]
        public int FridayFlatMinute { get; set; }

        /// <summary>
        /// Block new trades after Friday flat until next trading week
        /// Prevents new positions from being opened over the weekend
        /// </summary>
        [Parameter("Block After Friday Flat", Group = "Risk Management", DefaultValue = Toggle.ON)]
        public Toggle BlockAfterFridayFlat { get; set; }

        /// <summary>
        /// Cancel all pending orders when Friday flat is triggered
        /// Ensures clean slate for next trading week
        /// </summary>
        [Parameter("Cancel Pending Orders", Group = "Risk Management", DefaultValue = Toggle.ON)]
        public Toggle CancelPendingAfterFlat { get; set; }

        /// <summary>
        /// Minimum stop loss distance in pips
        /// Ensures stops are placed at meaningful distances from entry
        /// Valid range: 0+ pips (0 = auto-calculated)
        /// </summary>
        [Parameter("Stop Pips Min (0=auto)", Group = "Risk Management", DefaultValue = 0.0, MinValue = 0.0)]
        public double StopPipsMin { get; set; }

        /// <summary>
        /// Alternative maximum spread parameter for additional filtering
        /// Used in conjunction with MaxSpreadPips for enhanced spread control
        /// </summary>
        [Parameter("Max Spread (pips)", Group = "Risk Management", DefaultValue = 2.0, MinValue = 0.0)]
        public double SpreadMaxPips { get; set; }

        #endregion

        #region Execution Settings
        
        /// <summary>
        /// Random jitter in milliseconds to add to trade execution timing
        /// Helps prevent predictable timing patterns and reduces slippage
        /// Typical range: 100-2000ms
        /// </summary>
        [Parameter("Jitter (ms)", Group = "Execution Settings", DefaultValue = 700)]
        public int JitterMs { get; set; }

        /// <summary>
        /// Enable timer-based computation for continuous market analysis
        /// When enabled, physics calculations run on timer rather than only on bar close
        /// </summary>
        [Parameter("Use Timer Compute", Group = "Execution Settings", DefaultValue = true)]
        public bool UseTimerCompute { get; set; }

        /// <summary>
        /// Frequency in seconds for timer-based calculations
        /// Lower values provide more responsive analysis but use more CPU
        /// Valid range: 1-3600 seconds
        /// </summary>
        [Parameter("Calc Every (sec)", Group = "Execution Settings", DefaultValue = 300, MinValue = 1, MaxValue = 3600)]
        public int CalcEverySec { get; set; }

        /// <summary>
        /// Minimum stop loss distance in pips for execution validation
        /// Prevents execution of trades with inadequate stop distances
        /// Valid range: 0+ pips
        /// </summary>
        [Parameter("Min Stops (pips)", Group = "Execution Settings", DefaultValue = 0, MinValue = 0)]
        public int MinStopsPips { get; set; }

        /// <summary>
        /// Cooldown period in number of bars between trades
        /// Alternative to time-based cooldown for bar-based spacing
        /// Valid range: 0+ bars
        /// </summary>
        [Parameter("Cooldown Bars", Group = "Execution Settings", DefaultValue = 0, MinValue = 0)]
        public int CooldownBars { get; set; }

        /// <summary>
        /// Timer interval in milliseconds for periodic processing
        /// Controls how frequently the timer callback executes
        /// Typical range: 100-1000ms
        /// </summary>
        [Parameter("Timer Interval (ms)", Group = "Execution Settings", DefaultValue = 250)]
        public int TimerIntervalMs { get; set; }

        #endregion

        #region Energy Gate Parameters
        
        /// <summary>
        /// Enable energy-based market filtering to avoid low-quality trading conditions
        /// When enabled, analyzes market energy levels to determine trade suitability
        /// </summary>
        [Parameter("Use Energy Gate", Group = "Energy Gate Parameters", DefaultValue = false)]
        public bool UseEnergyGate { get; set; }

        /// <summary>
        /// Minimum total energy level required for trade entry
        /// Lower values may result in trades during quiet market conditions
        /// Typical range: 0.0001-0.01
        /// </summary>
        [Parameter("Energy Min (Etot)", Group = "Energy Gate Parameters", DefaultValue = 0.00010)]
        public double EnergyMin { get; set; }

        /// <summary>
        /// Maximum total energy level allowed for trade entry
        /// Higher values may indicate overly volatile or chaotic market conditions
        /// Typical range: 0.1-1.0
        /// </summary>
        [Parameter("Energy Max (Etot)", Group = "Energy Gate Parameters", DefaultValue = 0.50)]
        public double EnergyMax { get; set; }

        /// <summary>
        /// Maximum absolute energy change allowed between calculations
        /// Helps filter out erratic energy spikes that may indicate poor data
        /// Typical range: 0.01-0.2
        /// </summary>
        [Parameter("Max |dE|", Group = "Energy Gate Parameters", DefaultValue = 0.06)]
        public double MaxAbsDE { get; set; }

        /// <summary>
        /// Energy gate operating mode: Off, Block, Scale, or Tighten
        /// Controls how the robot responds to different energy conditions
        /// </summary>
        [Parameter("Energy Mode", Group = "Energy Gate Parameters", DefaultValue = EnergyMode.Scale)] 
        public EnergyMode EngMode { get; set; }

        /// <summary>
        /// ATR period for energy calculations
        /// Used to normalize energy values relative to current volatility
        /// Valid range: 5-50 periods
        /// </summary>
        [Parameter("Energy ATR Period", Group = "Energy Gate Parameters", DefaultValue = 14, MinValue = 5)] 
        public int EngAtrPeriod { get; set; }

        /// <summary>
        /// Threshold for quiet market conditions (energy ≤ x * ATR)
        /// Markets below this level are considered too quiet for reliable trading
        /// Typical range: 0.1-0.5
        /// </summary>
        [Parameter("Quiet ≤ (x ATR)", Group = "Energy Gate Parameters", DefaultValue = 0.20, MinValue = 0.0)] 
        public double EngQuietX { get; set; }

        /// <summary>
        /// Threshold for hot market conditions (energy ≥ x * ATR)
        /// Markets above this level may be too volatile for safe trading
        /// Typical range: 1.0-3.0
        /// </summary>
        [Parameter("Hot  ≥ (x ATR)", Group = "Energy Gate Parameters", DefaultValue = 1.20, MinValue = 0.0)] 
        public double EngHotX { get; set; }

        /// <summary>
        /// Minimum volume scaling factor when reducing position size in hot markets
        /// Prevents position sizes from becoming too small to be meaningful
        /// Valid range: 0.1-1.0
        /// </summary>
        [Parameter("Min Volume Scale", Group = "Energy Gate Parameters", DefaultValue = 0.35, MinValue = 0.1, MaxValue = 1.0)] 
        public double EngMinScale { get; set; }

        /// <summary>
        /// Additional breakout requirement in pips when markets are hot
        /// Makes entry requirements more stringent during volatile conditions
        /// Valid range: 0+ pips
        /// </summary>
        [Parameter("Extra Break @Hot (pips)", Group = "Energy Gate Parameters", DefaultValue = 2.0, MinValue = 0.0)] 
        public double EngExtraBreakPips { get; set; }

        /// <summary>
        /// Minimum energy level normalized by ATR squared
        /// Alternative energy threshold calculation method
        /// Typical range: 0.01-1.0
        /// </summary>
        [Parameter("Min E (×ATR^2)", Group = "Energy Gate Parameters", DefaultValue = 0.05)]
        public double MinEnergyATR2 { get; set; }

        /// <summary>
        /// Minimum energy change per bar required for trade consideration
        /// Ensures sufficient market movement for meaningful signals
        /// Valid range: 0+ (0 = disabled)
        /// </summary>
        [Parameter("Min dE per bar", Group = "Energy Gate Parameters", DefaultValue = 0.0)]
        public double MinDE { get; set; }

        #endregion

        #region Physics Engine Parameters
        
        /// <summary>
        /// Mass parameter for physics simulation (m in F=ma)
        /// Higher mass creates more inertial behavior, slower to change direction
        /// Lower mass creates more responsive movement
        /// Typical range: 0.1-10.0
        /// </summary>
        [Parameter("Mass m", Group = "Physics Engine Parameters", DefaultValue = 1.0)]
        public double Mass { get; set; }

        /// <summary>
        /// Damping coefficient for velocity decay (simulates market friction)
        /// Higher values cause faster velocity decay, more stable behavior
        /// Lower values allow momentum to persist longer
        /// Typical range: 0.1-1.0
        /// </summary>
        [Parameter("Damping c", Group = "Physics Engine Parameters", DefaultValue = 0.30)]
        public double Damping { get; set; }

        /// <summary>
        /// Spring constant for Donchian channel mean reversion force
        /// Controls strength of pull towards price channel midpoint
        /// Higher values create stronger mean reversion tendency
        /// Typical range: 0.1-2.0
        /// </summary>
        [Parameter("Spring k1 (Donchian)", Group = "Physics Engine Parameters", DefaultValue = 0.60)]
        public double SpringK1 { get; set; }

        /// <summary>
        /// Spring constant for EMA mean reversion force
        /// Controls strength of pull towards exponential moving average
        /// Higher values create stronger trend-following behavior
        /// Typical range: 0.1-1.0
        /// </summary>
        [Parameter("Spring k2 (EMA)", Group = "Physics Engine Parameters", DefaultValue = 0.30)]
        public double SpringK2 { get; set; }

        /// <summary>
        /// Coulomb friction coefficient (static friction when velocity = 0)
        /// Simulates market resistance to initial movement
        /// Higher values require more force to initiate movement
        /// Typical range: 0.00001-0.001
        /// </summary>
        [Parameter("Coulomb μ0", Group = "Physics Engine Parameters", DefaultValue = 0.00002)]
        public double FrictionMu0 { get; set; }

        /// <summary>
        /// Stribeck friction coefficient (kinetic friction at high velocities)
        /// Simulates market resistance during rapid price movements
        /// Typically higher than Coulomb friction
        /// Typical range: 0.0001-0.01
        /// </summary>
        [Parameter("Stribeck μ1", Group = "Physics Engine Parameters", DefaultValue = 0.00010)]
        public double FrictionMu1 { get; set; }

        /// <summary>
        /// Stribeck velocity threshold (transition point between friction regimes)
        /// Velocity level where friction transitions from static to kinetic
        /// Lower values make friction transition more sensitive
        /// Typical range: 0.0001-0.01
        /// </summary>
        [Parameter("Stribeck v* (abs)", Group = "Physics Engine Parameters", DefaultValue = 0.00020)]
        public double StribeckVStar { get; set; }

        /// <summary>
        /// Drive gain for external force based on price position relative to bands
        /// Amplifies momentum when price moves beyond normal ranges
        /// Higher values create stronger breakout behavior
        /// Typical range: 0.5-3.0
        /// </summary>
        [Parameter("Drive Gain", Group = "Physics Engine Parameters", DefaultValue = 1.0)]
        public double DriveGain { get; set; }

        /// <summary>
        /// Noise scaling factor (ATR multiplier for random noise injection)
        /// Simulates market randomness and uncertainty
        /// Higher values add more stochastic behavior
        /// Typical range: 0.0-0.5
        /// </summary>
        [Parameter("Noise k (ATR→σ)", Group = "Physics Engine Parameters", DefaultValue = 0.10)]
        public double NoiseK { get; set; }

        /// <summary>
        /// Minimum velocity magnitude required for momentum calculations
        /// Prevents noise from being interpreted as meaningful movement
        /// Lower values make system more sensitive to small changes
        /// Typical range: 0.00001-0.001
        /// </summary>
        [Parameter("Min |v| for momentum", Group = "Physics Engine Parameters", DefaultValue = 0.00005)]
        public double MinMomentum { get; set; }

        /// <summary>
        /// Number of physics sub-steps per bar for integration accuracy
        /// Higher values provide more accurate simulation but use more CPU
        /// Lower values are faster but may reduce simulation quality
        /// Valid range: 1-20 steps
        /// </summary>
        [Parameter("SubSteps (per bar)", Group = "Physics Engine Parameters", DefaultValue = 1, MinValue = 1, MaxValue = 20)]
        public int SubSteps { get; set; }

        #endregion

        #region Pullback Entry Parameters
        
        /// <summary>
        /// Enable pullback-based entry strategy using Fibonacci retracement levels
        /// When enabled, looks for entries on retracements within trending moves
        /// </summary>
        [Parameter("Use Pullback Entries", Group="Pullback Entry Parameters", DefaultValue = true)]
        public bool UsePullbackEntries { get; set; }

        /// <summary>
        /// Minimum Fibonacci retracement level for pullback zone (0.0-1.0)
        /// Lower values target shallower pullbacks, higher frequency but lower quality
        /// Typical range: 0.2-0.5
        /// </summary>
        [Parameter("PB Fib Min", Group="Pullback Entry Parameters", DefaultValue = 0.3)]
        public double PbFibMin { get; set; }

        /// <summary>
        /// Maximum Fibonacci retracement level for pullback zone (0.0-1.0)
        /// Higher values target deeper pullbacks, lower frequency but higher quality
        /// Typical range: 0.5-0.8
        /// </summary>
        [Parameter("PB Fib Max", Group="Pullback Entry Parameters", DefaultValue = 0.7)]
        public double PbFibMax { get; set; }

        /// <summary>
        /// ATR multiplier for stop loss buffer in pullback trades
        /// Controls distance of stop loss below/above pullback zone
        /// Higher values provide more room but larger potential losses
        /// Typical range: 0.1-1.0
        /// </summary>
        [Parameter("PB ATR Buffer X", Group="Pullback Entry Parameters", DefaultValue = 0.25, MinValue = 0.0)]
        public double PbAtrBufX { get; set; }

        /// <summary>
        /// Backstep parameter for swing detection (fractal width)
        /// Number of bars on each side required to confirm a swing point
        /// Higher values find more significant swings but may miss shorter-term moves
        /// Valid range: 2-10 bars
        /// </summary>
        [Parameter("PB Swing Backstep", Group="Pullback Entry Parameters", DefaultValue = 3, MinValue = 2, MaxValue = 10)]
        public int PbBack { get; set; }

        /// <summary>
        /// Maximum lookback period for finding swing points
        /// Limits how far back to search for relevant swing highs/lows
        /// Higher values find older swings, lower values focus on recent structure
        /// Valid range: 50-1000 bars
        /// </summary>
        [Parameter("PB Lookback Bars", Group="Pullback Entry Parameters", DefaultValue = 400, MinValue = 50)]
        public int PbLook { get; set; }

        /// <summary>
        /// Require EMA slope confirmation for pullback entries
        /// When enabled, pullback entries need trend confirmation from EMA direction
        /// Helps filter against-trend pullback trades
        /// </summary>
        [Parameter("PB Need EMA Slope", Group="Pullback Entry Parameters", DefaultValue = true)]
        public bool PbNeedEmaSlope { get; set; }

        /// <summary>
        /// Minimum physics velocity required for pullback consideration
        /// Helps ensure sufficient momentum exists before pullback entry
        /// Lower values allow entries with less momentum
        /// Typical range: 0.0-0.01
        /// </summary>
        [Parameter("PB Min v", Group="Pullback Entry Parameters", DefaultValue = 0.0)]
        public double PbMinV { get; set; }

        /// <summary>
        /// Automatically adjust Fibonacci zone width based on market velocity
        /// When enabled, adapts pullback zones to current market conditions
        /// Higher velocity markets get tighter zones, lower velocity get wider zones
        /// </summary>
        [Parameter("PB Auto Width", Group="Pullback Entry Parameters", DefaultValue = true)]
        public bool PbAutoWidth { get; set; }

        /// <summary>
        /// Minimum ATR value in pips to prevent division by zero and ensure meaningful ranges
        /// Sets a floor for ATR calculations in pullback analysis
        /// Valid range: 1+ pips
        /// </summary>
        [Parameter("PB ATR Floor (pips)", Group="Pullback Entry Parameters", DefaultValue = 6.0, MinValue = 0)]
        public double PbAtrFloorPips { get; set; }

        /// <summary>
        /// Maximum ATR multiplier cap to prevent excessive ranges
        /// Limits pullback calculations during extremely volatile periods
        /// Valid range: 0.8-3.0
        /// </summary>
        [Parameter("PB ATR Cap x", Group="Pullback Entry Parameters", DefaultValue = 1.20, MinValue = 0.8, MaxValue = 3)]
        public double PbAtrCapX { get; set; }

        /// <summary>
        /// Require micro-confirmation (break of recent high/low) for pullback entries
        /// When enabled, waits for small breakout confirmation before entry
        /// Helps reduce false signals but may delay entries
        /// </summary>
        [Parameter("PB Micro-Confirm", Group="Pullback Entry Parameters", DefaultValue = false)]
        public bool PbMicroConfirm { get; set; }

        /// <summary>
        /// Tolerance in pips for pullback zone boundaries
        /// Allows some flexibility in exact retracement level matching
        /// Higher values are more permissive but may reduce signal quality
        /// Valid range: 0+ pips
        /// </summary>
        [Parameter("PB Zone Tol (pips)", Group="Pullback Entry Parameters", DefaultValue = 2.0, MinValue=0)]
        public double PbZoneTolPips { get; set; }

        /// <summary>
        /// Number of bars to look back for pullback confirmation signals
        /// Controls the timeframe for validating pullback entry conditions
        /// Valid range: 1-5 bars
        /// </summary>
        [Parameter("PB Confirm Lookback", Group="Pullback Entry Parameters", DefaultValue = 2, MinValue=1, MaxValue=5)]
        public int PbConfirmLookback { get; set; }

        /// <summary>
        /// Maximum number of bars a pullback "armed" state can persist
        /// After being armed, entry must occur within this timeframe
        /// Valid range: 1-20 bars
        /// </summary>
        [Parameter("PB Arm Bars", Group="Pullback Entry Parameters", DefaultValue = 10, MinValue=1, MaxValue=20)]
        public int PbArmBars { get; set; }

        /// <summary>
        /// Use relaxed EMA slope requirements for pullback validation
        /// When enabled, reduces strictness of trend confirmation requirements
        /// Allows more pullback opportunities but may reduce quality
        /// </summary>
        [Parameter("PB Relax EMA Slope", Group="Pullback Entry Parameters", DefaultValue = true)]
        public bool PbRelaxEmaSlope { get; set; }

        /// <summary>
        /// Minimum velocity ratio (|velocity|/ATR) required for pullback entries
        /// Ensures sufficient momentum relative to current volatility
        /// Lower values allow entries with less relative momentum
        /// Typical range: 0.2-1.0
        /// </summary>
        [Parameter("PB Min vr (|v|/ATR)", Group="Pullback Entry Parameters", DefaultValue = 0.45, MinValue=0)]
        public double PbMinVr { get; set; }

        #endregion

        #region Kalman Filter Parameters
        
        /// <summary>
        /// Enable Kalman filter for noise reduction in physics calculations
        /// When enabled, applies advanced filtering to smooth price and velocity estimates
        /// Helps reduce noise while preserving signal integrity
        /// </summary>
        [Parameter("Use Kalman", Group = "Kalman Filter Parameters", DefaultValue = true)]
        public bool UseKalman { get; set; }

        /// <summary>
        /// Process noise covariance (Q matrix base value)
        /// Controls how much the filter trusts the physics model vs observations
        /// Higher values make filter more responsive to model predictions
        /// Typical range: 1e-8 to 1e-4
        /// </summary>
        [Parameter("Q base", Group = "Kalman Filter Parameters", DefaultValue = 1e-6)]
        public double KalmanQ { get; set; }

        /// <summary>
        /// Observation noise covariance (R matrix base value)
        /// Controls how much the filter trusts price observations vs model
        /// Higher values make filter rely more on model, less on observations
        /// Typical range: 1e-8 to 1e-4
        /// </summary>
        [Parameter("R base", Group = "Kalman Filter Parameters", DefaultValue = 1e-6)]
        public double KalmanR { get; set; }

        #endregion

        #region Gap Protection Parameters
        
        /// <summary>
        /// ATR multiplier for gap detection threshold
        /// Price gaps larger than this multiple of ATR trigger gap protection
        /// Higher values make gap detection less sensitive
        /// Typical range: 1.0-3.0
        /// </summary>
        [Parameter("Gap K (×ATR)", Group = "Gap Protection Parameters", DefaultValue = 1.5)]
        public double GapK { get; set; }

        /// <summary>
        /// Number of bars to wait after gap detection before resuming trading
        /// Provides cooldown period after market gaps to avoid volatile conditions
        /// Set to 0 to disable gap-based cooldown
        /// Valid range: 0+ bars
        /// </summary>
        [Parameter("Gap Cooldown Bars", Group = "Gap Protection Parameters", DefaultValue = 0, MinValue = 0)]
        public int GapCooldownBars { get; set; }

        #endregion

        #region ZigZag and Exit Strategy Parameters
        
        /// <summary>
        /// Enable ZigZag-based exit management for open positions
        /// When enabled, adjusts stop losses based on swing point analysis
        /// Helps capture more profit by trailing stops at key levels
        /// </summary>
        [Parameter("Use ZigZag Escape", Group = "ZigZag and Exit Strategy", DefaultValue = true)]
        public bool UseZigZagEscape { get; set; }

        /// <summary>
        /// Backstep parameter for ZigZag swing detection
        /// Number of bars on each side required to confirm a ZigZag pivot
        /// Higher values find more significant swings but may be less responsive
        /// Valid range: 1+ bars
        /// </summary>
        [Parameter("ZZ BackStep", Group = "ZigZag and Exit Strategy", DefaultValue = 3, MinValue = 1)]
        public int ZzBackStep { get; set; }

        /// <summary>
        /// ATR multiplier for ZigZag stop loss buffer
        /// Distance beyond swing points where stops are placed
        /// Higher values provide more room but larger potential losses
        /// Typical range: 0.0-2.0
        /// </summary>
        [Parameter("ZZ Buffer ATR×", Group = "ZigZag and Exit Strategy", DefaultValue = 0.0)]
        public double ZzBufferAtrX { get; set; }

        /// <summary>
        /// Fixed pip buffer beyond ZigZag pivot points for stop placement
        /// Additional safety margin beyond swing highs/lows
        /// Valid range: 0+ pips
        /// </summary>
        [Parameter("ZZ Buffer Pips", Group = "ZigZag and Exit Strategy", DefaultValue = 2)]
        public int ZzBufferPips { get; set; }

        /// <summary>
        /// ATR multiplier for ZigZag breakout detection buffer
        /// Determines how far price must move beyond swing points for valid breakout
        /// Higher values require stronger breakouts but reduce false signals
        /// Typical range: 0.2-1.0
        /// </summary>
        [Parameter("ZZ Break Buffer (ATR×)", Group = "ZigZag and Exit Strategy", DefaultValue = 0.4)]
        public double ZzBreakAtrX { get; set; }

        /// <summary>
        /// Fixed pip buffer for ZigZag breakout detection
        /// Minimum distance beyond swing points required for breakout confirmation
        /// Works in conjunction with ATR-based buffer
        /// Valid range: 0+ pips
        /// </summary>
        [Parameter("ZZ Break Buffer (pips)", Group = "ZigZag and Exit Strategy", DefaultValue = 2.0)]
        public double ZzBreakPips { get; set; }

        /// <summary>
        /// Display ZigZag lines and swing points on chart for visual analysis
        /// Helps with manual analysis and strategy validation
        /// </summary>
        [Parameter("Show ZigZag on Chart", Group="ZigZag and Exit Strategy", DefaultValue = true)]
        public bool ShowZigZagOnChart { get; set; }

        /// <summary>
        /// Enable ZigZag breakout entries as trade triggers
        /// When enabled, breakouts beyond swing points can trigger new positions
        /// When disabled, only uses ZigZag for exit management
        /// </summary>
        [Parameter("Use ZZ Break Entries", Group="ZigZag and Exit Strategy", DefaultValue = true)]
        public bool UseZzBreakEntries { get; set; }

        #endregion

        #region Trailing Stop Parameters
        
        /// <summary>
        /// ATR multiplier for trailing stop distance calculation
        /// Controls how close trailing stops follow price movement
        /// Higher values provide more room but larger potential losses
        /// Typical range: 0.8-3.0
        /// </summary>
        [Parameter("Trail ATR x", Group="Trailing Stop Parameters", DefaultValue = 1.5)]
        public double TrailAtrMult { get; set; }

        /// <summary>
        /// Minimum step size in pips for trailing stop updates
        /// Prevents excessive minor stop loss modifications
        /// Helps reduce broker commissions and API calls
        /// Valid range: 0.1+ pips
        /// </summary>
        [Parameter("Min trail step (pips)", Group="Trailing Stop Parameters", DefaultValue = 1.0)]
        public double MinTrailStepPips { get; set; }

        #endregion

        #region Debug and Diagnostic Parameters
        
        /// <summary>
        /// Enable verbose logging for detailed trade analysis and debugging
        /// When enabled, outputs comprehensive information about trade decisions
        /// Useful for strategy analysis but may impact performance
        /// </summary>
        [Parameter("Verbose Audit", Group = "Debug and Diagnostic", DefaultValue = true)]
        public bool Verbose { get; set; }

        /// <summary>
        /// Frequency of diagnostic output in number of bars
        /// Controls how often detailed diagnostic information is logged
        /// Higher values reduce log volume but provide less frequent updates
        /// Valid range: 1-50 bars
        /// </summary>
        [Parameter("Audit Every N Bars", Group = "Debug and Diagnostic", DefaultValue = 1, MinValue = 1, MaxValue = 50)]
        public int AuditEveryN { get; set; }

        /// <summary>
        /// Enable visualization and diagnostic charts
        /// When enabled, creates visual elements for strategy analysis
        /// Helps with manual review and strategy optimization
        /// </summary>
        [Parameter("Debug Viz", Group = "Debug and Diagnostic", DefaultValue = true)]
        public bool DebugViz { get; set; }

        /// <summary>
        /// Draw robot analysis lines and markers on chart
        /// When enabled, displays swing points, breakout levels, and entry signals
        /// Useful for visual strategy validation but may clutter chart
        /// </summary>
        [Parameter("Draw Robot Lines", Group = "Debug and Diagnostic", DefaultValue = false)]
        public bool DrawRobotLines { get; set; }

        /// <summary>
        /// Alternative label for trade identification (legacy parameter)
        /// Used for backward compatibility with older versions
        /// </summary>
        [Parameter("Label", Group = "Debug and Diagnostic", DefaultValue = "Sindan")] 
        public string Label { get; set; }

        #endregion

        #region Technical Indicators and State Variables
        
        /// <summary>Average True Range indicator for volatility measurement</summary>
        private AverageTrueRange _atr;
        
        /// <summary>Exponential Moving Average for trend analysis</summary>
        private ExponentialMovingAverage _ema;
        
        /// <summary>Average True Range indicator for energy gate calculations</summary>
        private AverageTrueRange _atrEng;

        #endregion

        #region Trading State Variables
        
        /// <summary>Timestamp of the last trade execution</summary>
        private DateTime _lastTrade = DateTime.MinValue;
        
        /// <summary>Current logical trading day for daily resets</summary>
        private DateTime _day = DateTime.MinValue;
        
        /// <summary>Number of trades executed today</summary>
        private int _tradesToday;
        
        /// <summary>Bar number when last trade was placed (for cooldown)</summary>
        private int lastTradeBar = int.MinValue / 2;
        
        /// <summary>Indicates if robot is suspended (e.g., after Friday flat)</summary>
        private bool _suspended = false;
        
        /// <summary>Last Friday when flat was enforced</summary>
        private DateTime _lastFridayFlat = DateTime.MinValue;
        
        /// <summary>Gap-based cooldown remaining bars</summary>
        private int _gapCooldown;

        #endregion

        #region Physics State Variables
        
        /// <summary>Current position in physics simulation</summary>
        private double _x;
        
        /// <summary>Current velocity in physics simulation</summary>
        private double _v;
        
        /// <summary>Current acceleration in physics simulation</summary>
        private double _a;
        
        /// <summary>Previous energy level for delta calculations</summary>
        private double _Eprev;
        
        /// <summary>Total energy (Etot) calculation</summary>
        private double Etot = 0.0;
        
        /// <summary>Energy change (dE) between calculations</summary>
        private double dE = 0.0;

        #endregion

        #region Analysis State Variables
        
        /// <summary>Indicates if physics calculations are ready and valid</summary>
        private bool _physicsReady;
        
        /// <summary>Last bar index for which physics was computed</summary>
        private int _lastComputedBar = -1;
        
        /// <summary>Cached highest high value from computation</summary>
        private double _hhC;
        
        /// <summary>Cached lowest low value from computation</summary>
        private double _llC;
        
        /// <summary>Cached midpoint value from computation</summary>
        private double _midC;
        
        /// <summary>Cached EMA value from computation</summary>
        private double _emaC;
        
        /// <summary>Cached ATR value from computation</summary>
        private double _atrC;
        
        /// <summary>Cached band difference from computation</summary>
        private double _bandDiffC;

        #endregion

        #region Kalman Filter State
        
        /// <summary>Kalman filter covariance matrix element P[0,0]</summary>
        private double _P00;
        
        /// <summary>Kalman filter covariance matrix element P[0,1]</summary>
        private double _P01;
        
        /// <summary>Kalman filter covariance matrix element P[1,0]</summary>
        private double _P10;
        
        /// <summary>Kalman filter covariance matrix element P[1,1]</summary>
        private double _P11;

        #endregion

        #region Pullback State Variables
        
        /// <summary>Bar index when long pullback was armed</summary>
        private int _armLongBar = -9999;
        
        /// <summary>Bar index when short pullback was armed</summary>
        private int _armShortBar = -9999;
        
        /// <summary>Stop loss price for armed long pullback</summary>
        private double _armLongSL = double.NaN;
        
        /// <summary>Stop loss price for armed short pullback</summary>
        private double _armShortSL = double.NaN;

        #endregion

        #region Trade Management State
        
        /// <summary>Pending stop loss override price</summary>
        private double _initialSLOverride = double.NaN;
        
        /// <summary>Pending take profit ratio override</summary>
        private double _rMultipleOverride = double.NaN;
        
        /// <summary>Current label for trade identification</summary>
        private string _label;

        #endregion

        #region Utility Variables
        
        /// <summary>Timer busy flag to prevent overlapping executions</summary>
        private volatile bool _timerBusy = false;
        
        /// <summary>Timestamp of last timer execution</summary>
        private DateTime _lastTimerTs = DateTime.MinValue;
        
        /// <summary>Random number generator for noise injection</summary>
        private readonly Random _rng = new Random();
        
        /// <summary>Next diagnostic output timestamp</summary>
        private DateTime _nextDiag = DateTime.MinValue;
        
        /// <summary>ZigZag buy signal reason</summary>
        private string _zzWhyB = "";
        
        /// <summary>ZigZag sell signal reason</summary>
        private string _zzWhyS = "";

        #endregion

        #region Helper Methods and Utilities

        /// <summary>
        /// Checks if a double value is finite (not NaN or Infinity)
        /// </summary>
        /// <param name="x">Value to check</param>
        /// <returns>True if value is finite</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double x) => !(double.IsNaN(x) || double.IsInfinity(x));
        
        /// <summary>
        /// Calculates the square of a number
        /// </summary>
        /// <param name="x">Value to square</param>
        /// <returns>x squared</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Sqr(double x) => x * x;

        /// <summary>
        /// Rounds price down to the nearest tick size
        /// </summary>
        /// <param name="price">Price to round</param>
        /// <returns>Price rounded down to tick boundary</returns>
        private double ToTickDown(double price)
         => Math.Floor(price / Symbol.TickSize)  * Symbol.TickSize;
         
        /// <summary>
        /// Rounds price up to the nearest tick size
        /// </summary>
        /// <param name="price">Price to round</param>
        /// <returns>Price rounded up to tick boundary</returns>
        private double ToTickUp(double price)
         => Math.Ceiling(price / Symbol.TickSize) * Symbol.TickSize;
      
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

         #region Pullback Analysis Methods

         /// <summary>
         /// Adjusts Fibonacci retracement band based on velocity ratio
         /// Higher velocity markets get tighter zones, lower velocity get wider zones
         /// </summary>
         /// <param name="vr">Velocity ratio (|velocity|/ATR)</param>
         /// <returns>Tuple containing minimum and maximum Fibonacci levels</returns>
         private (double fMin, double fMax) GetFibBand(double vr) 
         {
             if (!PbAutoWidth) return (PbFibMin, PbFibMax);
             if (vr >= 3.0) return (0.30, 0.55);
             if (vr >= 1.5) return (0.38, 0.62);
             return (0.50, 0.70);
         }

        /// <summary>
        /// Calculates ATR with floor and ceiling constraints to prevent extreme values
        /// Ensures ATR stays within reasonable bounds for pullback calculations
        /// </summary>
        /// <returns>Constrained ATR value</returns>
        private double SoftATR() 
        {
            double a = _atrC;
            a = Math.Max(a, PbAtrFloorPips * Symbol.PipSize);            // Floor
            return Math.Min(a, PbAtrCapX * _atr.Result.LastValue);       // Ceiling
        }

        /// <summary>
        /// Confirms long entry with micro-breakout validation
        /// Requires current close to exceed recent highs for additional confirmation
        /// </summary>
        /// <returns>True if micro-confirmation criteria are met</returns>
        private bool MicroConfirmLong() 
        {
            if (!PbMicroConfirm) return true;
            return Bars.ClosePrices.LastValue >
                   Math.Max(Bars.HighPrices.Last(1), Bars.HighPrices.Last(2));
        }

        /// <summary>
        /// Confirms short entry with micro-breakdown validation
        /// Requires current close to break below recent lows for additional confirmation
        /// </summary>
        /// <returns>True if micro-confirmation criteria are met</returns>
        private bool MicroConfirmShort() 
        {
            if (!PbMicroConfirm) return true;
            return Bars.ClosePrices.LastValue <
                   Math.Min(Bars.LowPrices.Last(1), Bars.LowPrices.Last(2));
        }

        #endregion

        #region Trade Management Methods

        /// <summary>
        /// Checks if there's an open position of the specified type
        /// </summary>
        /// <param name="label">Trade label to match</param>
        /// <param name="side">Trade direction to check</param>
        /// <returns>True if matching open position exists</returns>
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

        /// <summary>
        /// Validates if new trade entry is allowed based on various filters
        /// Checks cooldown, daily limits, existing positions, and spread conditions
        /// </summary>
        /// <param name="label">Trade label</param>
        /// <param name="side">Trade direction</param>
        /// <returns>True if entry is allowed</returns>
        private bool CanEnter(string label, TradeType side)
        {
            if (CooldownMin > 0 && InCooldown()) return false;
            int maxDaily = EffectiveMaxTradesPerDay();
            if (maxDaily > 0 && _tradesToday >= maxDaily) return false;
            if (HasOpen(label, side)) return false;
            if (SpreadTooWide()) return false;
            return true;
        }

        /// <summary>
        /// Checks if current spread exceeds maximum allowed threshold
        /// </summary>
        /// <returns>True if spread is too wide for trading</returns>
        private bool SpreadTooWide()
        {
            double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            return (SpreadMaxPips > 0.0) && (spreadPips > SpreadMaxPips);
        }

        /// <summary>
        /// Checks for recent breakout above specified number of bars
        /// </summary>
        /// <param name="n">Number of bars to check</param>
        /// <returns>True if current close breaks above recent highs</returns>
        private bool BreakoutUp(int n)
        {
            double hi = double.MinValue;
            for (int i = 1; i <= n; i++) hi = Math.Max(hi, Bars.HighPrices.Last(i));
            return Bars.ClosePrices.LastValue >= hi - Symbol.TickSize*0.5; // Allow near-equal values
        }

        /// <summary>
        /// Checks for recent breakdown below specified number of bars
        /// </summary>
        /// <param name="n">Number of bars to check</param>
        /// <returns>True if current close breaks below recent lows</returns>
        private bool BreakoutDown(int n)
        {
            double lo = double.MaxValue;
            for (int i = 1; i <= n; i++) lo = Math.Min(lo, Bars.LowPrices.Last(i));
            return Bars.ClosePrices.LastValue <= lo + Symbol.TickSize*0.5;
        }

        /// <summary>
        /// Checks if robot is currently in cooldown period
        /// </summary>
        /// <returns>True if still in cooldown</returns>
        private bool InCooldown()
        {
            if (_lastTrade == DateTime.MinValue) return false;
            return Server.Time < _lastTrade.AddMinutes(CooldownMin);
        }

        /// <summary>
        /// Logs rejection with formatted code and message
        /// </summary>
        /// <param name="code">Rejection code</param>
        /// <param name="msg">Rejection message</param>
        private void REJ(string code, string msg) => Print($"[REJ][{code}] {msg}");

        #endregion

        #region Robot Lifecycle Methods

        /// <summary>
        /// Robot initialization method called when the robot starts
        /// Initializes all indicators, state variables, and trading parameters
        /// Sets up physics engine, Kalman filter, and timer-based processing
        /// </summary>
        protected override void OnStart()
        {
            // Initialize trading label and indicators
            _label = LabelPrefix;
            _atr = Indicators.AverageTrueRange(Bars, AtrPeriod, MovingAverageType.Exponential);
            _ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaPeriod);
            _atrEng = Indicators.AverageTrueRange(EngAtrPeriod, MovingAverageType.Exponential);
            
            // Initialize physics engine state
            _day = LogicalDay(Server.Time);
            _x = Bars.ClosePrices.LastValue;
            _v = 0.0;
            _a = 0.0;
            _Eprev = 0.0;
            
            // Initialize Kalman filter covariance matrix
            _P00 = 1e-8; _P01 = 0; _P10 = 0; _P11 = 1e-8;
            
            // Initialize signal tracking
            _zzWhyB = ""; _zzWhyS = "";
            
            // Initialize trading state
            _suspended = false;
            _lastFridayFlat = DateTime.MinValue;
            _physicsReady = false;
            _lastComputedBar = -1;
            _lastTrade = DateTime.MinValue;
            _tradesToday = 0;
            _timerBusy = false;
            
            // Clean up any existing chart objects
            ClearRobotLines();
            
            // Start timer-based processing if enabled
            if (UseTimerCompute)
                Timer.Start(TimeSpan.FromMilliseconds(TimerIntervalMs));
            
            // Log initialization information
            if (Verbose)
                Print("[AUDIT] START {0} TF={1} Tick={2}", SymbolName, Bars.TimeFrame, Symbol.TickSize);
            Print("VolInUnits Min={0}, Step={1}, Max={2}",
                Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsStep, Symbol.VolumeInUnitsMax);
        }

        /// <summary>
        /// Robot cleanup method called when the robot stops
        /// Ensures proper cleanup of timer resources
        /// </summary>
        protected override void OnStop()
        {
            if (UseTimerCompute)
                Timer.Stop();
        }

        /// <summary>
        /// Timer-based processing method for continuous market analysis
        /// Performs physics calculations, trade management, and position monitoring
        /// Runs at intervals specified by TimerIntervalMs parameter
        /// </summary>
        protected override void OnTimer()
        {
            // Perform daily maintenance tasks
            RollDailyIfNeeded();
            EnforceFridayFlat();
            
            if (!UseTimerCompute) return;
            
            // Prevent overlapping timer executions
            if (_timerBusy) return;

            // Respect calculation frequency limits
            if (CalcEverySec > 0 && _lastTimerTs != DateTime.MinValue)
            {
                var span = Server.Time - _lastTimerTs;
                if (span.TotalSeconds < Math.Max(1, CalcEverySec))
                    return;
            }
            
            _lastTimerTs = Server.Time;
            _timerBusy = true;
            
            try
            {
                // Validate input data
                double atrVal = _atr.Result.LastValue;
                double pxVal = Bars.ClosePrices.LastValue;
                if (atrVal <= 0 || double.IsNaN(atrVal) || double.IsInfinity(atrVal) ||
                    double.IsNaN(pxVal) || double.IsInfinity(pxVal))
                {
                    return; // Skip computation on invalid inputs
                }

                // Perform physics computation if needed
                if (_lastComputedBar != Bars.Count - 1)
                {
                    bool ok = ComputePhysicsAndCache();
                    if (!ok) return;
                }
            }
            catch (Exception ex)
            {
                if (Verbose)
                    Print("[TIMER] crash: {0}", ex.Message);
            }
            finally
            {
                _timerBusy = false; // Always release the busy flag
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
    bool cd     = (CooldownMin > 0) && InCooldown();
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
    if (RiskPercent <= 0)
        return Symbol.NormalizeVolumeInUnits(Units, RoundingMode.ToNearest);

    // --- ここからリスク％計算 ---
    double slPrice  = Math.Max(Symbol.TickSize, TrailAtrK * atr);
    double ticks    = slPrice / Symbol.TickSize;
    double tickValue= Symbol.TickValue;
    if (tickValue <= 0 || double.IsNaN(ticks) || double.IsInfinity(ticks))
        return Symbol.NormalizeVolumeInUnits(Units, RoundingMode.ToNearest);

    double riskMoney = Account.Balance * (RiskPercent / 100.0);
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
            if (_lastTrade == DateTime.MinValue) return false;
            return Server.Time < _lastTrade.AddMinutes(CooldownMin);
        }

        /// <summary>
        /// Finds the highest value in a data series over the specified number of periods
        /// </summary>
        /// <param name="s">Data series to analyze</param>
        /// <param name="n">Number of periods to look back</param>
        /// <returns>Highest value found</returns>
        private static double Highest(DataSeries s, int n)
        {
            double v = double.MinValue; int end = s.Count - 1;
            for (int i = end; i > end - n && i >= 0; i--) v = Math.Max(v, s[i]);
            return v;
        }

        /// <summary>
        /// Finds the lowest value in a data series over the specified number of periods
        /// </summary>
        /// <param name="s">Data series to analyze</param>
        /// <param name="n">Number of periods to look back</param>
        /// <returns>Lowest value found</returns>
        private static double Lowest(DataSeries s, int n)
        {
            double v = double.MaxValue; int end = s.Count - 1;
            for (int i = end; i > end - n && i >= 0; i--) v = Math.Min(v, s[i]);
            return v;
        }

        /// <summary>
        /// Rounds a price to the nearest tick size
        /// </summary>
        /// <param name="price">Price to round</param>
        /// <returns>Price rounded to tick boundary</returns>
        private double RoundToTick(double price) => Math.Round(price / Symbol.TickSize) * Symbol.TickSize;

        /// <summary>
        /// Generates a random number from a standard normal (Gaussian) distribution
        /// Uses Box-Muller transformation with safety guards against extreme values
        /// </summary>
        /// <returns>Random value from normal distribution (mean=0, std=1)</returns>
        private double Gaussian()
        {
            // Clamp values to avoid log(0) or extreme results
            double u1 = _rng.NextDouble();
            double u2 = _rng.NextDouble();
            u1 = Math.Max(1e-12, Math.Min(1.0 - 1e-12, u1));

            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

            // Safety guard against NaN/Infinity
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