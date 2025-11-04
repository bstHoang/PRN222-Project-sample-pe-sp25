# New Console Capture Mechanism

## Overview
This document describes the new baseline capture mechanism that replaces the prompt file comparison approach.

## Changes Made

### 1. Removed Prompt File Dependency
- **Before**: Required a separate text file containing prompts to compare against captured console output
- **After**: Uses real-time baseline captures triggered by the user

### 2. New Keyboard Hotkey (F5)
- **F5 Key**: Captures the current console screen as a baseline BEFORE user enters input
- **Enter Key**: Captures console after input and compares with baseline to extract user input

### 3. Stage-Based Workflow

#### How It Works:
1. Start the grading session with "Start Grading Session" button
2. **Press F5** when you see a prompt in the client console (e.g., "enter int")
   - This captures Stage 1 baseline
   - A confirmation dialog will appear
3. User enters input in the client console and **presses Enter**
   - The tool compares the current output with the baseline
   - Extracts the user input (e.g., "1" from "enter int 1")
   - Records this as Stage 1 input
4. **Press F5** again for the next prompt (creates Stage 2 baseline)
5. Repeat steps 3-4 for additional stages

### 4. Implementation Details

#### Modified Files:
- **KeyboardHook.cs**: Added support for F5 key capture
- **MainWindow.xaml.cs**: 
  - Added `OnCapturePressed()` handler for F5
  - Modified `OnEnterPressed()` to compare with baseline
  - Added `ExtractInputFromBaseline()` method
  - Removed prompt file reading logic
- **MainWindow.xaml**: Updated UI to remove prompt file selection, added instructions

#### Data Structures:
- `_baselineCaptures`: Stores baseline captures for each stage
- `_currentStage`: Tracks the current stage number
- `_enterLines`: Stores extracted user inputs with timestamps
- `_stageCaptures`: Stores full console snapshots for each stage

### 5. Benefits of New Approach
1. **More Accurate**: Captures exact state before input, no manual prompt file needed
2. **Flexible**: Works with any console output format
3. **Stage Management**: Clear separation of stages via user-triggered captures
4. **Real-time**: No pre-configuration required

### 6. Excel Output
The tool still generates the same Excel output with three sheets:
- **Logs**: HTTP/TCP requests with stage assignments
- **Inputs**: Extracted user inputs by stage
- **ClientStages**: Full console snapshots for each stage

## Usage Example

```
Console shows: "enter int "
→ Press F5 (Stage 1 baseline captured)
→ User types: 1 and presses Enter
→ Tool extracts: "1"

Console shows: "banana" (response)
Console shows: "enter string "
→ Press F5 (Stage 2 baseline captured)
→ User types: hello and presses Enter
→ Tool extracts: "hello"
```

## Migration Notes
- Old prompt files are no longer needed
- The F5 key must be pressed in the client console window
- Press F5 BEFORE each user input prompt
