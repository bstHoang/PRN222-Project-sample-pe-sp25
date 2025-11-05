# ConsoleManager - Managed Console Output with F12 Key Handling

## Overview
`ConsoleManager` is a utility class that provides managed console output and automatically handles F12 key detection to clear the console input buffer. This prevents stale input from appearing when the middleware tool captures console output.

## Features

### 1. Automatic F12 Key Detection
- Monitors for F12 key presses in the background
- Automatically clears the console input buffer when F12 is detected
- Prevents old user input from reappearing at the prompt

### 2. Managed Console Output
- Provides `ConsoleManager.Write()` and `ConsoleManager.WriteLine()` methods
- Drop-in replacement for `Console.Write()` and `Console.WriteLine()`
- Future-proof for adding output tracking or logging capabilities

### 3. Cross-Platform Support
- Uses Windows API (`FlushConsoleInputBuffer`) on Windows
- Falls back to `Console.ReadKey()` on non-Windows platforms

## Usage

### Initialization
Call `ConsoleManager.Initialize()` at the start of your `Main()` method:

```csharp
static async Task Main(string[] args)
{
    // Initialize ConsoleManager to handle F12 key and clear input buffer
    ConsoleManager.Initialize();
    
    // ... rest of your code
}
```

### Replacing Console.Write/WriteLine
Replace all `Console.Write()` and `Console.WriteLine()` calls with `ConsoleManager` equivalents:

**Before:**
```csharp
Console.WriteLine("Hello, World!");
Console.Write("Enter your name: ");
```

**After:**
```csharp
ConsoleManager.WriteLine("Hello, World!");
ConsoleManager.Write("Enter your name: ");
```

### Manual Buffer Clearing (Optional)
You can manually clear the input buffer if needed:

```csharp
ConsoleManager.ClearInputBuffer();
```

### Checking F12 Press Status (Optional)
Check if F12 was pressed since the last check:

```csharp
if (ConsoleManager.WasF12Pressed())
{
    // F12 was pressed - do something
}
```

## How It Works

### Background Monitoring
When `Initialize()` is called, ConsoleManager starts a background thread that:
1. Continuously monitors for keyboard input
2. Detects when F12 key is pressed
3. Immediately clears the console input buffer
4. Sets an internal flag that can be checked

### Input Buffer Clearing
When F12 is pressed:
1. On Windows: Uses `kernel32.dll` `FlushConsoleInputBuffer()` API
2. On other platforms: Reads and discards all available keys with `Console.ReadKey(true)`

This prevents the middleware tool's `AttachConsole()` call from causing stale input to reappear.

## Benefits

### For Students
- **No manual cleanup**: Don't need to press Backspace to delete old input
- **Better UX**: Console behaves as expected when F12 is pressed
- **Transparent**: Works automatically without changing workflow

### For Developers
- **Centralized output**: All console output goes through one place
- **Future extensibility**: Easy to add logging, tracking, or filtering
- **Clean code**: Simple drop-in replacement for Console methods

## Example Migration

### Original Code
```csharp
static async Task Main(string[] args)
{
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();
    
    Console.WriteLine($"Base URL: {config["BaseUrl"]}");
    
    while (running)
    {
        Console.WriteLine("\n====== Menu ======");
        Console.Write("Choose option: ");
        var choice = Console.ReadLine();
        // ...
    }
}
```

### Migrated Code
```csharp
static async Task Main(string[] args)
{
    // Add this line
    ConsoleManager.Initialize();
    
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();
    
    // Replace Console with ConsoleManager
    ConsoleManager.WriteLine($"Base URL: {config["BaseUrl"]}");
    
    while (running)
    {
        ConsoleManager.WriteLine("\n====== Menu ======");
        ConsoleManager.Write("Choose option: ");
        var choice = Console.ReadLine();
        // ...
    }
}
```

## Technical Details

### Thread Safety
- Background monitoring thread is marked as background thread
- Automatically terminates when application exits
- No explicit cleanup required

### Performance
- Minimal overhead: 50ms sleep between key checks
- Non-blocking: Doesn't interfere with main thread
- Efficient: Only activates when F12 is detected

### Compatibility
- .NET 6.0+
- Works with `System.Runtime.InteropServices` for Windows API
- Cross-platform compatible

## Troubleshooting

### F12 Key Not Detected
- Ensure `ConsoleManager.Initialize()` is called at startup
- Check if console window has focus
- Verify F12 isn't mapped to another function in your terminal

### Input Still Appearing
- Verify all `Console.Write/WriteLine` are replaced with `ConsoleManager`
- Ensure application is running with proper console permissions
- On non-Windows, some terminals may not support input buffer flushing

## Future Enhancements
- Add output logging to file
- Track output history for debugging
- Support custom key combinations (not just F12)
- Add output filtering or formatting capabilities
