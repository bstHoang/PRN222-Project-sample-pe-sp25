# MiddlewareTool - Console Capture & Grading Tool

A WPF-based tool for capturing and logging console interactions during grading sessions. This tool acts as a proxy between client and server applications, capturing HTTP/TCP traffic and console outputs for educational assessment purposes.

## 🎯 Latest Update: Event-Driven Server Capture

**Version:** 2.1 (Event-Driven Capture)
**Date:** 2025

### What's New in 2.1?
- ⚡ **Event-driven server capture** - No more 300ms delay guessing!
- 🎯 **Precise timing** - Captures server console exactly when response arrives
- 🚀 **Faster captures** - No unnecessary waiting
- 📡 **ProxyService signals** - Uses existing network monitoring as timing source

### What's New in 2.0?
- ✅ **No more prompt files needed!** The tool now uses real-time baseline captures
- 🔑 **F5 hotkey** to capture console baseline before user input
- 📊 **Real-time status indicator** showing current stage and operations
- 📋 **Clear stage management** with automatic stage numbering
- 🎨 **Color-coded status** for easy workflow tracking

### Quick Start

1. **Start Session**: Click "Start Grading Session" button
2. **Capture Baseline**: Press **F5** in the client console (before user enters input)
3. **Capture Input**: User types input and presses **Enter**
4. **Repeat**: Press **F5** for next stage, then Enter after input
5. **Stop Session**: Click "Stop Grading Session" to save results

## 📚 Documentation

### For Quick Reference
- **[QUICK_REFERENCE.md](QUICK_REFERENCE.md)** - One-page reference card (English & Vietnamese)
- **[WORKFLOW_DIAGRAM.md](WORKFLOW_DIAGRAM.md)** - Visual workflow and data flow diagrams

### For Detailed Information
- **[EVENT_DRIVEN_CAPTURE.md](EVENT_DRIVEN_CAPTURE.md)** - Event-driven server capture mechanism (NEW!)
- **[NEW_CAPTURE_MECHANISM.md](NEW_CAPTURE_MECHANISM.md)** - Complete guide to the baseline capture mechanism
- **[HUONG_DAN_SU_DUNG.md](HUONG_DAN_SU_DUNG.md)** - Comprehensive user guide (Vietnamese)
- **[CHANGES_SUMMARY.md](CHANGES_SUMMARY.md)** - Technical changes and implementation details

## 🔑 Key Features

### Console Capture
- Captures console output from client and server applications
- Real-time baseline capture using F5 hotkey
- Intelligent input extraction by comparing baseline with current output
- Stage-based workflow with automatic numbering

### Network Logging
- HTTP and TCP protocol support
- Request/Response logging with timestamps
- Stage assignment to network traffic
- Excel export with multiple sheets

### Stage Management
- F5 creates new stage and captures baseline
- Enter extracts user input for current stage
- Automatic stage numbering and tracking
- Full console snapshots for each stage

### Output Files
- **Excel File** (3 sheets):
  - Logs: HTTP/TCP requests with stage assignments
  - Inputs: User inputs by stage
  - ClientStages: Full console snapshots per stage
- **Log Files**:
  - Client console output
  - Server console output
  - Enter line captures
  - User inputs by stage

## 🖥️ System Requirements

- **OS**: Windows (WPF Application)
- **Framework**: .NET 8.0 or higher
- **Dependencies**: ClosedXML (for Excel generation)

## 🚀 How to Use

### Setup
1. Launch MiddlewareTool.exe
2. Select Server executable path
3. Select Client executable path
4. Select AppSettings templates (server and client)
5. Choose Excel log file destination

### During Session
1. Click "Start Grading Session"
2. Watch the status indicator (below the button)
3. **In client console:**
   - Press **F5** when you see a prompt (before user input)
   - User enters input
   - Press **Enter** to capture the input
4. Repeat F5 → Enter cycle for each stage
5. Click "Stop Grading Session" when done

### Understanding Status Colors
- 🔴 **Gray**: Session not running
- 🟢 **Dark Green**: Waiting for F5 to capture baseline
- 🟢 **Green**: Baseline captured, waiting for Enter
- 🔵 **Blue**: Input captured successfully
- 🟠 **Orange**: Warning - input extraction failed

## 📖 Example Workflow

```
1. Start Session
   ↓
2. Client shows: "enter int "
   → Press F5 (captures Stage 1 baseline)
   ↓
3. User types: 1 and presses Enter
   → Tool extracts "1"
   ↓
4. Client shows: "banana" then "enter string "
   → Press F5 (captures Stage 2 baseline)
   ↓
5. User types: hello and presses Enter
   → Tool extracts "hello"
   ↓
6. Stop Session
   → Excel file saved with 2 stages
```

## 🔧 Troubleshooting

| Problem | Solution |
|---------|----------|
| F5 not working | Make sure client console window is focused |
| Can't extract input | Press F5 first to create baseline |
| Status not updating | Ensure tool window is visible |
| Excel file not created | Check write permissions for selected path |

## 🆚 Comparison: Old vs New Method

| Feature | Old (Prompt File) | New (F5 Baseline) |
|---------|-------------------|-------------------|
| Setup Required | Prompt file needed | No files needed |
| Flexibility | Limited to pre-defined prompts | Works with any prompt |
| Accuracy | Depends on file accuracy | Real-time capture |
| Ease of Use | Moderate | Easy |
| Stage Management | Unclear | Clear (F5 = new stage) |

## 🏗️ Project Structure

```
MiddlewareTool/
├── MiddlewareTool/          # Main WPF application
│   ├── Helpers/
│   │   └── KeyboardHook.cs  # F5 and Enter key detection
│   ├── Models/
│   │   ├── LoggedRequest.cs
│   │   └── StageLog.cs
│   ├── Services/
│   │   ├── ConsoleCaptureService.cs  # Console capture logic
│   │   ├── ExcelLogger.cs            # Excel file generation
│   │   ├── ProxyService.cs           # HTTP/TCP proxy
│   │   └── AppSettingsReplacer.cs
│   ├── MainWindow.xaml      # UI layout
│   └── MainWindow.xaml.cs   # Main logic
├── Documentation/           # All .md files
├── AutoGradeSetup.ps1       # Setup script
└── appsettings.json         # Configuration
```

## 👥 For Developers

### Key Classes
- **KeyboardHook**: Low-level keyboard hook for F5 and Enter detection
- **ConsoleCaptureService**: Captures console output from external processes
- **MainWindow**: Main application logic and event handlers
- **ExcelLogger**: Generates Excel reports with multiple sheets
- **ProxyService**: HTTP/TCP proxy for network traffic logging

### Key Methods
- `OnCapturePressed()`: Handles F5 press, captures baseline
- `OnEnterPressed()`: Handles Enter press, extracts input, sets flag for server capture
- `OnServerResponseReceived()`: Event handler that captures server console when response arrives
- `ExtractInputFromBaseline()`: Compares baseline with current output
- `CaptureConsoleOutput()`: Captures console screen buffer

### Data Flow
```
F5 Press → CaptureConsoleOutput() → Save to _baselineCaptures

Enter Press → CaptureConsoleOutput() → ExtractInputFromBaseline()
  → Set _pendingServerCapture flag → Wait for event...

Client Request → ProxyService → Server → Response → ProxyService
  → Fire ServerResponseReceived event

Event Handler → CaptureConsoleOutput(server) → Save to _stageCaptures

Stop Session → ExcelLogger saves all data
```

## 📝 License

This tool is part of the PRN222 course project.

## 🤝 Contributing

For bug reports or feature requests, please contact the development team.

## 📞 Support

- Check the documentation files listed above
- Review log files if issues occur
- Ensure Windows console windows are visible during capture

---

**Note**: This is a Windows-only application due to WPF framework and Win32 API dependencies for console capture and keyboard hooks.
