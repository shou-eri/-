# cAlgo Trading Robot Development

This repository contains a cAlgo/cTrader algorithmic trading robot written in C#. The main trading strategy is implemented in `Sindan.cs` which extends the cAlgo Robot base class.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Prerequisites and Setup
- This is a **cAlgo/cTrader** project that requires the cTrader trading platform and cAlgo IDE for compilation and deployment
- The code cannot be compiled with standard .NET SDK alone due to dependencies on cAlgo.API assemblies
- cTrader must be installed locally to build and test the robot
- The robot runs within the cTrader trading platform environment

### Development Environment
- **CRITICAL**: This code does NOT build with standard `dotnet build` - it requires cTrader/cAlgo IDE
- **NO STANDARD BUILD PROCESS**: There are no .csproj, .sln, or other standard .NET project files
- The single file `Sindan.cs` (1,687 lines) contains the complete trading robot implementation
- Development must be done within cTrader's cAlgo IDE or compatible editor

### Code Structure and Navigation
- **Main file**: `Sindan.cs` - GalileoUltraPhysicsV5 trading robot class
- **Robot type**: Physics-based algorithmic trading strategy with pullback entries
- **Key components**:
  - Parameters: Trading configuration (risk, timeframes, indicators)
  - Physics engine: Custom momentum and energy calculations
  - Entry logic: Pullback-based trade entry system
  - Risk management: ATR-based position sizing and trailing stops
  - Diagnostics: JSON logging and performance tracking

### Validation and Testing
- **Syntax validation**: Use mock cAlgo API for basic syntax checking (build time: ~1-2 seconds)
- **Code formatting**: Standard C# formatting rules apply - CRLF line endings, namespace conventions
- **CANNOT run unit tests**: No test infrastructure exists or is needed for cAlgo robots
- **CANNOT build executable**: Robot must be loaded in cTrader for execution
- **Deployment**: Copy `Sindan.cs` to cTrader cAlgo robots folder and compile within cTrader

### Manual Validation Requirements
- **MANDATORY**: After making changes, the robot MUST be tested in cTrader environment
- **VALIDATION STEPS**:
  1. Load robot in cTrader cAlgo
  2. Compile within cTrader (compilation time: 5-10 seconds)
  3. Run on demo account with minimal position sizes
  4. Verify parameter configuration works correctly
  5. Test entry/exit logic on historical data or demo environment
  6. Monitor for runtime errors in cTrader logs

### Common Development Tasks

#### Making Code Changes
- Edit `Sindan.cs` directly - all robot logic is in this single file
- Focus changes on:
  - Parameter definitions (lines 19-298)
  - Physics calculations (OnBar method and related helpers)
  - Entry/exit logic (TryPullbackLongPhysics, TryPullbackShortPhysics)
  - Risk management (CalcUnitsByRisk, TrailIfNeeded)

#### Key Code Locations
- **Parameters**: Lines 19-298 (trading configuration)
- **Initialization**: OnStart() method (line ~479)
- **Main logic**: OnBar() method (line ~696)
- **Entry signals**: TryPullbackLongPhysics/Short methods (line ~900+)
- **Risk management**: CalcUnitsByRisk, TrailIfNeeded methods
- **Logging**: LogJson method for diagnostics

#### Code Style Guidelines
- Follow existing C# conventions in the file
- Use CRLF line endings (Windows-style)
- Maintain existing parameter grouping structure
- Add detailed comments for complex physics calculations
- Use consistent variable naming (camelCase for locals, PascalCase for properties)

## Limitations and Constraints

### What You CANNOT Do
- **NEVER** attempt `dotnet build` or `dotnet run` - these will fail
- **NEVER** try to create .csproj or .sln files - not used in cAlgo
- **NEVER** attempt to run the robot outside cTrader environment
- **CANNOT** create automated tests - testing must be done in cTrader
- **CANNOT** use standard .NET debugging - must use cTrader's debugging features

### What You CAN Do
- Edit the C# code directly in `Sindan.cs`
- Perform basic syntax validation using mock APIs
- Apply standard C# code formatting and style rules
- Add new parameters, indicators, or trading logic
- Modify risk management and position sizing algorithms
- Enhance logging and diagnostic capabilities

## Validation Checklist

Before completing any changes, ensure:
- [ ] Code compiles successfully in cTrader cAlgo IDE
- [ ] All new parameters are properly defined with appropriate defaults
- [ ] Robot loads without errors in cTrader
- [ ] Basic functionality tested on demo account
- [ ] No runtime exceptions during initialization
- [ ] Entry/exit logic behaves as expected
- [ ] Risk management parameters work correctly
- [ ] Logging output is properly formatted

## Time Expectations
- **Code editing**: Immediate
- **Syntax validation**: 1-2 seconds (using mock API)
- **cTrader compilation**: 5-10 seconds
- **Robot testing**: 10-30 minutes (depending on strategy complexity)
- **NEVER CANCEL**: Robot testing in cTrader may require extended observation periods

## Common Issues and Solutions
- **Compilation errors**: Most errors are due to missing cAlgo API references - must compile in cTrader
- **Parameter errors**: Ensure all Parameter attributes have valid DefaultValue settings
- **Runtime errors**: Check robot logs in cTrader for detailed error messages
- **Performance issues**: Monitor physics calculations - complex math may need optimization

## Repository Structure
```
/
├── README.md (minimal documentation)
└── Sindan.cs (complete trading robot - 1,687 lines)
```

## Development Workflow Summary
1. **Edit** `Sindan.cs` with your preferred editor
2. **Validate** syntax using basic mock compilation (optional)
3. **Test** in cTrader cAlgo IDE (mandatory)
4. **Deploy** by copying to cTrader and loading robot
5. **Validate** on demo account before live trading