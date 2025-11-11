# Fix for F12 Key Interference Issue

## Problem Description

When the console client is launched through MiddlewareTool's "Start Grading Session", pressing F12 or Enter in the console client causes interference with the client application's key handling. This is because:

1. **MiddlewareTool** uses a global keyboard hook (KeyboardHook.cs) to intercept F12 and Enter keys for capturing console stages and user input
2. **Console Client** (ConsoleManager.cs) has a background thread that monitors for F12 key to clear the input buffer
3. When both systems are active, they both try to process the same keypress, causing conflicts

## Root Cause

### Before the Fix

The global keyboard hook in `KeyboardHook.cs` was:
1. Intercepting ALL F12 and Enter keypresses system-wide
2. Invoking callbacks to check if the foreground window is the client
3. **Always** calling `CallNextHookEx` to pass the key to the target application

This meant that even when MiddlewareTool processed the key, it still passed the key through to the console client. The ConsoleManager's background thread would then ALSO see the key and try to process it, causing double-processing and interference.

Additionally, the ConsoleManager's `MonitorF12Key()` thread had a bug where it would consume ALL keypresses (not just F12) by calling `Console.ReadKey(true)`, which removes the key from the input buffer. This caused keys to be lost.

## The Solution (Updated)

### Important Update - Version 2

**The initial fix suppressed BOTH F12 and Enter, which broke Console.ReadLine()!** When Enter was suppressed, the console application couldn't complete ReadLine() calls, causing it to hang or behave incorrectly.

**The updated fix only suppresses F12, allowing Enter to pass through normally.**

### Changes to KeyboardHook.cs

1. **Added client process ID parameter** to `SetHook()` method:
   ```csharp
   public static void SetHook(Action onEnterPressed, Action onCapturePressed, int clientProcessId)
   ```

2. **Moved foreground window check INTO the hook callback**:
   - The hook now checks if the foreground window belongs to the client process
   - This check happens BEFORE deciding whether to process or pass through the key

3. **Suppress F12 only, pass through Enter**:
   - When **F12** is pressed in the client window, the hook processes it AND suppresses it
   - When **Enter** is pressed in the client window, the hook processes it BUT passes it through
   - Suppression is done by returning `(IntPtr)1` instead of calling `CallNextHookEx`
   - This prevents F12 from reaching the ConsoleManager (avoiding double-processing) while allowing Enter to work normally

4. **Pass through keys for other windows**:
   - If the foreground window is NOT the client, the key is passed through normally
   - This ensures F12 and Enter still work in other applications

### Changes to MainWindow.xaml.cs

1. **Updated KeyboardHook.SetHook call** to pass the client process ID:
   ```csharp
   KeyboardHook.SetHook(OnEnterPressed, OnCapturePressed, _clientProcess.Id);
   ```

2. **Removed redundant foreground window checks** from `OnCapturePressed()` and `OnEnterPressed()`:
   - These checks are now done in the hook itself
   - The callbacks only run when the check has already passed

3. **Removed unused Windows API imports** (GetForegroundWindow, GetWindowThreadProcessId):
   - These are now only in KeyboardHook.cs where they're actually used

## How It Works Now

### F12 Key Press Flow

```
1. User presses F12 in client console
   ↓
2. Global keyboard hook intercepts (HookCallback in KeyboardHook.cs)
   ↓
3. Check foreground window:
   - Is it the client process? → YES
   ↓
4. Process F12 for MiddlewareTool:
   - Invoke _onCapturePressed
   - Return (IntPtr)1 to SUPPRESS the key
   ↓
5. F12 does NOT reach ConsoleManager
   - No double-processing ✅
   - No interference ✅
```

### Enter Key Press Flow

```
1. User presses Enter in client console
   ↓
2. Global keyboard hook intercepts
   ↓
3. Check foreground window:
   - Is it the client process? → YES
   ↓
4. Process Enter for MiddlewareTool:
   - Invoke _onEnterPressed (tracks input)
   - Call CallNextHookEx to PASS THROUGH the key
   ↓
5. Enter REACHES Console.ReadLine()
   - ReadLine completes normally ✅
   - Application functions correctly ✅
```

### Key Press in Other Windows

```
1. User presses F12/Enter in a different window
   ↓
2. Global keyboard hook intercepts
   ↓
3. Check foreground window:
   - Is it the client process? → NO
   ↓
4. Pass key through via CallNextHookEx
   ↓
5. Key reaches the target application normally
```

## Benefits

1. **Eliminates F12 Interference**: F12 is exclusively processed by MiddlewareTool when in client window
2. **Maintains Enter Functionality**: Enter passes through so Console.ReadLine() works normally
3. **Prevents Double-Processing**: ConsoleManager no longer sees F12
4. **No Hanging**: Console application doesn't hang waiting for Enter
5. **Cleaner Code**: Foreground window check is done once in the hook

## Testing

To verify the fix works:

1. **Test F12 in client console** (launched via MiddlewareTool):
   - F12 should capture stages for MiddlewareTool ✅
   - F12 should NOT be seen by ConsoleManager ✅
   - No double-processing or interference ✅

2. **Test Enter in client console** (launched via MiddlewareTool):
   - Enter should track user input for MiddlewareTool ✅
   - Enter should reach Console.ReadLine() ✅
   - Application should respond to input normally ✅
   - No hanging or freezing ✅

3. **Test F12 in other applications**:
   - F12 should work normally in other windows ✅
   - Only when client console is in foreground should MiddlewareTool process it ✅

4. **Test manual launch**:
   - When client is launched manually (not via MiddlewareTool), F12 should work normally ✅
   - No global hook is active, so ConsoleManager processes F12 as designed ✅

## Critical Difference: Enter vs F12

| Key | Behavior in Client Window | Reason |
|-----|---------------------------|--------|
| **F12** | Suppressed (blocked) | Prevents double-processing with ConsoleManager |
| **Enter** | Passed through | Required for Console.ReadLine() to work |

**Why not suppress Enter?**
- Console applications use `Console.ReadLine()` which BLOCKS waiting for Enter
- If Enter is suppressed, ReadLine never completes → application hangs
- MiddlewareTool only needs to OBSERVE Enter (track input), not block it

**Why suppress F12?**
- ConsoleManager's background thread monitors for F12
- If F12 reaches it, double-processing occurs
- MiddlewareTool is the primary handler for F12 during grading sessions

## Known Issues Addressed

**Issue 1: Console hangs when suppressing Enter**
- ✅ FIXED: Enter now passes through

**Issue 2: F12 causes double-processing**
- ✅ FIXED: F12 is suppressed for client window

**Issue 3: ConsoleManager consumes non-F12 keys**
- ⚠️ LIMITATION: This is a bug in ConsoleManager itself, but by suppressing F12 in the hook, we prevent F12-related issues
- The key-consuming bug still exists for other keys but is outside the scope of this fix

## Conclusion

This fix resolves the key interference issue by:
1. **Suppressing F12** when pressed in the client window (prevents ConsoleManager interference)
2. **Passing through Enter** so Console.ReadLine() works normally (prevents hanging)
3. **Observing both keys** in MiddlewareTool callbacks (for tracking purposes)

The fix is minimal, focused, and doesn't change the overall architecture of the MiddlewareTool - it just makes the keyboard hook smarter about which keys to suppress vs. which to pass through.

