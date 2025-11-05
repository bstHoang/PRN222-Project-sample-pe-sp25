# Summary of Changes

## Problem Statement
The original tool required users to prepare a prompt file in advance. The tool would capture console output when the user pressed Enter and compare it with the prompt file to extract user input. This approach was inflexible and required pre-configuration.

## Solution Implemented
Implemented a new real-time baseline capture mechanism using the F5 hotkey, eliminating the need for prompt files.

## Files Modified

### 1. `Helpers/KeyboardHook.cs`
**Changes:**
- Added support for F5 key detection (VK_F5 = 0x74)
- Modified `SetHook()` method to accept two callbacks: `onEnterPressed` and `onCapturePressed`
- Updated `HookCallback()` to handle both Enter and F5 key presses
- Added `_onCapturePressed` field to store the F5 callback

**Lines Modified:** ~15 lines

### 2. `MainWindow.xaml`
**Changes:**
- Removed "Prompt Configuration" GroupBox (including prompt file selection)
- Added new "Capture Instructions" GroupBox with step-by-step usage guide
- Added status indicator (`StatusText`) to show real-time operation status
- Modified button layout to accommodate status text

**Lines Modified:** ~30 lines

### 3. `MainWindow.xaml.cs`
**Changes:**
- Removed `_currentPrompts` list and related prompt file loading code
- Added `_baselineCaptures` list to store F5 baseline captures
- Added `_currentStage` counter to track current stage number
- Removed `BrowsePrompts_Click()` handler
- Simplified session validation (removed prompt file check)
- Removed prompt file reading logic from `StartSessionAsync()`
- Added `OnCapturePressed()` method to handle F5 key press
- Modified `OnEnterPressed()` to compare with baseline captures
- Added `ExtractInputFromBaseline()` method with intelligent input extraction
- Updated `StopSession()` to work without prompt file processing
- Added status text updates throughout workflow
- Updated `KeyboardHook.SetHook()` call to pass both callbacks

**Lines Modified:** ~150 lines (significant refactoring)

### 4. `NEW_CAPTURE_MECHANISM.md` (New File)
**Content:**
- Comprehensive documentation of the new mechanism
- Usage instructions and workflow
- Benefits comparison
- Migration notes

**Lines Added:** ~120 lines

### 5. `Services/ConsoleCaptureService.cs`
**No Changes Required:**
- The `ProcessClientConsoleOutput()` method remains but is no longer used
- Keeping it for backward compatibility
- Main capture functionality (`CaptureConsoleOutput()`) is still used

## Key Features Added

### 1. F5 Hotkey Capture
- Press F5 in the client console to capture the current screen state as a baseline
- Creates a new stage automatically
- Captures the exact prompt text without manual configuration

### 2. Intelligent Input Extraction
- Compares current console output with baseline
- Extracts user input by finding differences
- Handles multiple scenarios:
  - Input appended to prompt line (e.g., "enter int 1" from "enter int")
  - Input on new lines
  - Multi-line responses

### 3. Real-time Status Feedback
- Shows current stage number
- Indicates when baseline is captured
- Displays extracted input
- Shows warnings if input extraction fails
- Updates on session start/stop

### 4. Stage Management
- Automatic stage numbering
- Each F5 press creates a new stage
- Stage information saved to Excel
- Maintains stage history with timestamps

## Workflow Comparison

### Before (Prompt File Method)
1. Create prompt file with expected prompts
2. Start grading session
3. User enters input and presses Enter
4. Tool compares with prompt file
5. Extracts input by pattern matching

**Issues:**
- Required pre-configuration
- Inflexible to dynamic prompts
- Manual file maintenance
- Prone to mismatches

### After (Baseline Capture Method)
1. Start grading session
2. Press F5 when prompt appears (captures baseline)
3. User enters input and presses Enter
4. Tool compares with baseline
5. Extracts input automatically

**Benefits:**
- No pre-configuration needed
- Adapts to any prompt format
- Real-time operation
- More accurate extraction
- Clear stage separation

## Excel Output (Unchanged)
The tool still generates the same Excel file with three sheets:
- **Logs**: HTTP/TCP requests with stage assignments
- **Inputs**: User inputs by stage
- **ClientStages**: Full console snapshots

## Testing Recommendations
Since this is a Windows WPF application, testing must be done on Windows:

1. **Basic Workflow Test:**
   - Start session
   - Press F5 before first prompt
   - Enter input, press Enter
   - Verify input extraction
   - Check status updates

2. **Multi-Stage Test:**
   - Repeat F5 → Enter cycle multiple times
   - Verify stage increments
   - Check all inputs captured

3. **Edge Cases:**
   - Press F5 multiple times before Enter
   - Press Enter without F5
   - Multiple inputs on same prompt
   - Console with special characters

4. **File Output Verification:**
   - Check Excel file has all stages
   - Verify input extraction accuracy
   - Confirm log files created

## Backward Compatibility
- Old prompt files are no longer needed or supported
- Existing Excel output format unchanged
- Service layer (`ConsoleCaptureService`) maintains old methods for potential future use

## Migration Path
For existing users:
1. Remove or archive old prompt files
2. Learn new F5 workflow (5 simple steps)
3. Test with sample application
4. No code changes needed in client/server apps

## Known Limitations
- Requires Windows OS (WPF application)
- F5 must be pressed in the client console window (not tool window)
- Only captures visible console content (limited by console buffer)
- Status dialog during capture commented out to avoid blocking (can be enabled)

## Latest Update: Event-Driven Server Capture (2024)

### Previous Issue
The tool used a 300ms delay (`await Task.Delay(300)`) after pressing Enter to wait for server processing before capturing server console. This was a "guess" that could fail if:
- Server takes longer than 300ms → missed logs
- Server responds faster → wasted time

### Solution: Event-Driven Mechanism
Implemented a new event-driven mechanism using ProxyService as the signal provider:

**Files Modified:**
1. **Services/ProxyService.cs**
   - Added `ServerResponseReceived` event
   - Fires event after receiving response from server (HTTP & TCP)
   
2. **MainWindow.xaml.cs**
   - Added `_pendingServerCapture` flag and `_pendingCaptureData` fields
   - Modified `OnEnterPressed()` to set flag instead of using delay
   - Added `OnServerResponseReceived()` event handler
   - Subscribes/unsubscribes to event in StartSession/StopSession

**Workflow:**
1. User presses Enter → Captures client console, sets flag
2. Client sends request → ProxyService forwards to server
3. Server responds → ProxyService fires `ServerResponseReceived` event
4. Event handler → Captures server console immediately

**Benefits:**
- ✅ Accurate: Captures exactly when server responds
- ✅ Fast: No unnecessary waiting
- ✅ Reliable: No assumptions about processing time

See [EVENT_DRIVEN_CAPTURE.md](EVENT_DRIVEN_CAPTURE.md) for detailed documentation.
