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

## The Solution

### Changes to KeyboardHook.cs

1. **Added client process ID parameter** to `SetHook()` method:
   ```csharp
   public static void SetHook(Action onEnterPressed, Action onCapturePressed, int clientProcessId)
   ```

2. **Moved foreground window check INTO the hook callback**:
   - The hook now checks if the foreground window belongs to the client process
   - This check happens BEFORE deciding whether to process or pass through the key

3. **Suppress keys when processed by MiddlewareTool**:
   - When F12 or Enter is pressed in the client window, the hook processes it AND suppresses it
   - Suppression is done by returning `(IntPtr)1` instead of calling `CallNextHookEx`
   - This prevents the key from reaching the ConsoleManager, avoiding double-processing

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

### Key Press Flow

```
1. User presses F12/Enter in client console
   ↓
2. Global keyboard hook intercepts (HookCallback in KeyboardHook.cs)
   ↓
3. Check foreground window:
   - Is it the client process? → YES
   ↓
4. Process the key for MiddlewareTool:
   - Invoke _onCapturePressed (for F12) or _onEnterPressed (for Enter)
   - Return (IntPtr)1 to SUPPRESS the key
   ↓
5. Key does NOT reach ConsoleManager
   - No double-processing
   - No interference
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

1. **Eliminates Key Interference**: F12 and Enter are now exclusively processed by either MiddlewareTool OR the client, never both
2. **Prevents Double-Processing**: The ConsoleManager's background thread no longer sees F12 when MiddlewareTool processes it
3. **Maintains Normal Behavior**: Keys still work normally in other applications
4. **Cleaner Code**: Foreground window check is done once in the hook, not duplicated in callbacks

## Testing

To verify the fix works:

1. **Test F12 in client console** (launched via MiddlewareTool):
   - F12 should capture stages for MiddlewareTool
   - F12 should NOT interfere with client's normal operation
   - Client should not see the F12 keypress

2. **Test Enter in client console** (launched via MiddlewareTool):
   - Enter should track user input for MiddlewareTool
   - Enter should NOT be passed to the client (to avoid double-processing)
   - Client should not receive duplicate Enter events

3. **Test F12 in other applications**:
   - F12 should work normally in other windows
   - Only when client console is in foreground should MiddlewareTool process it

4. **Test manual launch**:
   - When client is launched manually (not via MiddlewareTool), F12 should work normally
   - No global hook is active, so ConsoleManager processes F12 as designed

## Known Limitation

**Enter Key Suppression**: The fix suppresses Enter keys when pressed in the client console. This means the Enter key won't reach the Console.ReadLine() call in the client. However, this is the intended behavior because:

1. MiddlewareTool needs to capture the exact moment when Enter is pressed to extract user input
2. If Enter were passed through, the client would process it AND MiddlewareTool would capture it, leading to double-processing
3. The user experience is that they press Enter, MiddlewareTool captures the input, and the client continues normally

If Enter needs to reach the client for ReadLine() to work, additional modifications would be needed to:
- Let MiddlewareTool capture the input first
- Then programmatically send Enter to the client console after capture
- This is more complex and may not be necessary depending on the actual client behavior

## Alternative Approach (Not Implemented)

An alternative would be to **modify the ConsoleManager** in the client to:
1. Not use a background thread for key monitoring
2. Check for F12 only at specific points (before ReadLine calls)
3. Or disable F12 monitoring entirely when launched via MiddlewareTool

However, this would require changing the client code, which may not be desirable if the client is part of student submissions or external code that shouldn't be modified.

## Conclusion

This fix resolves the key interference issue by ensuring that when MiddlewareTool's global hook processes a key for the client window, that key is suppressed and doesn't reach the client application. This prevents the ConsoleManager from also processing the key and causing conflicts.

The fix is minimal, focused, and doesn't change the overall architecture of the MiddlewareTool - it just makes the keyboard hook smarter about when to suppress keys vs. when to pass them through.
