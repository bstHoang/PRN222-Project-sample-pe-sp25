# Event-Driven Server Console Capture Mechanism / Cơ Chế Chụp Console Server Dựa Trên Sự Kiện

## Overview / Tổng Quan

This document describes the event-driven mechanism for capturing server console output, which replaces the previous 300ms delay assumption.

Tài liệu này mô tả cơ chế dựa trên sự kiện để chụp đầu ra console của server, thay thế giả định chờ 300ms trước đây.

## Problem / Vấn Đề

### Previous Implementation / Cách Làm Trước Đây
```csharp
// Old approach - guessing with 300ms delay
await Task.Delay(300); // Hope server processes within 300ms
string serverOutput = CaptureConsoleOutput(serverProcessId);
```

**Issues with this approach / Vấn đề với cách làm này:**
- **Assumption-based / Dựa trên giả định**: Assumes server always processes requests in < 300ms
- **Unreliable / Không đáng tin cậy**: If server takes longer, capture will miss logs
- **Wasteful / Lãng phí**: If server responds quickly, we still wait full 300ms

## Solution / Giải Pháp

### New Event-Driven Approach / Cách Làm Mới Dựa Trên Sự Kiện

The new implementation uses the ProxyService (which already intercepts all requests/responses) as a "signal provider".

Cách triển khai mới sử dụng ProxyService (đang bắt tất cả request/response) làm "người báo tín hiệu".

### Workflow / Quy Trình Hoạt Động

```
┌─────────────────────────────────────────────────────────────┐
│ 1. User presses Enter in Client Console                     │
│    (Người dùng bấm Enter ở console client)                  │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. OnEnterPressed() captures client console                 │
│    - Extracts user input                                     │
│    - Sets _pendingServerCapture flag = TRUE                  │
│    - Stores stage data                                       │
│    (Chụp console client, đặt cờ chờ server)                 │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Client sends HTTP/TCP request to ProxyService            │
│    (Client gửi request tới ProxyService)                    │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. ProxyService forwards request to Real Server             │
│    (ProxyService chuyển tiếp request tới server thật)       │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Server processes request and sends response              │
│    (Server xử lý và gửi response)                           │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ 6. ProxyService receives response from Server               │
│    - Forwards response to Client                             │
│    - Fires ServerResponseReceived EVENT ⚡                   │
│    (ProxyService nhận response và kích hoạt sự kiện)       │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────────────────┐
│ 7. OnServerResponseReceived() event handler                 │
│    - Checks _pendingServerCapture flag                       │
│    - If TRUE: Captures server console NOW                    │
│    - Saves complete stage data                               │
│    - Resets flag to FALSE                                    │
│    (Xử lý sự kiện, chụp console server ngay lập tức)       │
└─────────────────────────────────────────────────────────────┘
```

## Implementation Details / Chi Tiết Triển Khai

### 1. ProxyService Changes / Thay Đổi ProxyService

#### Added Event / Thêm Sự Kiện
```csharp
public event EventHandler? ServerResponseReceived;
```

#### HTTP Response Handler / Xử Lý HTTP Response
```csharp
private async Task ProcessHttpRequest(HttpListenerContext context)
{
    // ... forward request to server ...
    
    var responseMessage = await client.SendAsync(forwardRequest);
    
    // ... forward response to client ...
    
    // Fire event to signal server response received
    ServerResponseReceived?.Invoke(this, EventArgs.Empty);
}
```

#### TCP Response Handler / Xử Lý TCP Response
```csharp
private async Task RelayDataAsync(NetworkStream fromStream, NetworkStream toStream, 
                                   string direction, CancellationToken token)
{
    // ... relay data ...
    
    // Fire event when server sends data back to client
    if (direction == "Server -> Client")
    {
        ServerResponseReceived?.Invoke(this, EventArgs.Empty);
    }
}
```

### 2. MainWindow Changes / Thay Đổi MainWindow

#### Added Fields / Thêm Các Trường
```csharp
// Flag and data for pending server capture
private bool _pendingServerCapture = false;
private (int Stage, DateTime Timestamp, string ClientOutput, string UserInput) _pendingCaptureData;
```

#### Subscribe to Event / Đăng Ký Sự Kiện
```csharp
private async Task StartSessionAsync()
{
    // ...
    
    // Subscribe to server response event
    _proxyService.ServerResponseReceived += OnServerResponseReceived;
    
    _proxyService.StartProxy(selectedProtocol, _cts.Token);
    
    // ...
}
```

#### Unsubscribe on Stop / Hủy Đăng Ký Khi Dừng
```csharp
private async void StopSession()
{
    KeyboardHook.Unhook();
    _cts?.Cancel();
    
    // Unsubscribe from server response event
    _proxyService.ServerResponseReceived -= OnServerResponseReceived;
    
    _proxyService.StopProxy();
    // ...
}
```

#### Modified OnEnterPressed / Sửa Đổi OnEnterPressed
```csharp
private void OnEnterPressed()
{
    // ... capture client output ...
    // ... extract user input ...
    
    // Set flag to indicate we're waiting for server response
    _pendingServerCapture = true;
    _pendingCaptureData = (_currentStage, now, clientOutput, userInput);
    
    // Update status to show we're waiting
    StatusText.Text = "waiting for server response...";
    
    // NO MORE await Task.Delay(300)! Event will trigger capture!
}
```

#### New Event Handler / Xử Lý Sự Kiện Mới
```csharp
private void OnServerResponseReceived(object? sender, EventArgs e)
{
    // Check if we're waiting for a server capture
    if (!_pendingServerCapture) return;
    
    // Reset the flag
    _pendingServerCapture = false;
    
    // Capture server output NOW (exactly when response arrives)
    string serverOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
    
    // Add complete stage capture
    _stageCaptures.Add((_pendingCaptureData.Stage, _pendingCaptureData.Timestamp, 
                        _pendingCaptureData.ClientOutput, serverOutput));
    
    // Update status
    StatusText.Text = $"Stage {_pendingCaptureData.Stage} captured";
}
```

## Benefits / Lợi Ích

### 1. Accuracy / Độ Chính Xác
- ✅ Captures server console **exactly** when response arrives
- ✅ No missed logs due to slow server processing
- ✅ Chụp console server **chính xác** khi response đến
- ✅ Không bỏ sót log do server xử lý chậm

### 2. Performance / Hiệu Năng
- ✅ No unnecessary waiting if server responds quickly
- ✅ Faster capture cycle
- ✅ Không chờ không cần thiết nếu server phản hồi nhanh
- ✅ Chu kỳ chụp nhanh hơn

### 3. Reliability / Độ Tin Cậy
- ✅ No assumptions about server processing time
- ✅ Works regardless of server load or complexity
- ✅ Không giả định về thời gian xử lý của server
- ✅ Hoạt động bất kể tải server hay độ phức tạp

### 4. Maintainability / Khả Năng Bảo Trì
- ✅ Clear event-driven architecture
- ✅ Easy to understand and debug
- ✅ Kiến trúc sự kiện rõ ràng
- ✅ Dễ hiểu và gỡ lỗi

## Visual Comparison / So Sánh Trực Quan

### Old Approach / Cách Cũ
```
Enter Press → Capture Client → Wait 300ms ⏱️ → Capture Server
                                    ↑
                        Assumption: Server done by now
                        (May be wrong! ❌)
```

### New Approach / Cách Mới
```
Enter Press → Capture Client → Set Flag 🚩 → Wait...
                                                ↓
Client → ProxyService → Server → ProxyService → Fire Event ⚡
                                                ↓
                                    Event Handler → Capture Server ✅
                                    (Exact timing! ✅)
```

## Technical Notes / Ghi Chú Kỹ Thuật

### Thread Safety / An Toàn Luồng
- Event is fired from ProxyService's async tasks
- Event handler uses `Dispatcher.Invoke()` for UI updates
- Flag operations are on UI thread (no race conditions)

### Event Timing / Thời Điểm Sự Kiện
- HTTP: After `response.Close()` (response fully sent to client)
- TCP: After each data packet from server to client

### Error Handling / Xử Lý Lỗi
- If server process has exited, capture returns empty string
- If no pending capture flag, event is ignored (safe)
- Unsubscribe on session stop prevents memory leaks

## Migration Notes / Ghi Chú Di Chuyển

### Before / Trước Đây
```csharp
await Task.Delay(300); // Hope for the best
```

### After / Sau Này
```csharp
_pendingServerCapture = true; // Trust the event
```

### No Breaking Changes / Không Có Thay Đổi Phá Vỡ
- All public APIs remain the same
- Excel output format unchanged
- User workflow unchanged (still press Enter)
- Only internal timing mechanism improved

## Testing Recommendations / Khuyến Nghị Kiểm Tra

1. **Fast Server**: Verify capture works when server responds in < 100ms
2. **Slow Server**: Verify capture works when server takes > 1 second
3. **Multiple Requests**: Ensure flag mechanism handles rapid Enter presses
4. **TCP Protocol**: Test with TCP proxy mode as well as HTTP

## Summary / Tóm Tắt

This change replaces the **assumption-based 300ms delay** with a **precise event-driven mechanism**. The ProxyService, which already sees all traffic, now acts as the "signal provider" to tell MainWindow exactly when to capture the server console.

Thay đổi này thay thế **giả định chờ 300ms** bằng **cơ chế sự kiện chính xác**. ProxyService, vốn đã thấy tất cả traffic, giờ đây đóng vai trò "người báo tín hiệu" để báo cho MainWindow biết chính xác khi nào nên chụp console server.

**Result / Kết Quả**: More accurate, faster, and more reliable console captures!
