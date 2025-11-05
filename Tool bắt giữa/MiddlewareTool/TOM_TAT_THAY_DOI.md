# Tóm Tắt Thay Đổi - Event-Driven Server Capture

## Vấn Đề Ban Đầu

Khi người dùng bấm Enter ở console client, tool cần chụp lại console server để ghi lại phản hồi. Trước đây, tool sử dụng cách làm như sau:

```csharp
// Cách cũ
await Task.Delay(300); // Chờ 300ms
string serverOutput = CaptureConsoleOutput(serverProcessId);
```

**Vấn đề:**
- Con số 300ms là một "giả định" (assumption) rằng server sẽ luôn xử lý và log ra console nhanh hơn 300ms
- Nếu server xử lý chậm hơn 300ms → **thiếu log**
- Nếu server xử lý nhanh hơn 300ms → **lãng phí thời gian chờ**

## Giải Pháp Mới

Sử dụng ProxyService (đã đang bắt tất cả request/response) làm "người báo tín hiệu".

### Quy Trình Hoạt Động Mới

```
1. Người dùng bấm Enter
   ↓
2. OnEnterPressed() chụp console client, đặt cờ "_pendingServerCapture = true"
   ↓
3. Client gửi request qua ProxyService
   ↓
4. ProxyService chuyển tiếp request tới Server
   ↓
5. Server xử lý và trả response
   ↓
6. ProxyService nhận response từ Server
   → Gửi response về Client
   → Kích hoạt sự kiện "ServerResponseReceived" ⚡
   ↓
7. MainWindow nhận sự kiện
   → Kiểm tra cờ "_pendingServerCapture"
   → Nếu = true: Chụp console server NGAY LẬP TỨC
   → Reset cờ về false
```

### Các File Đã Thay Đổi

#### 1. `Services/ProxyService.cs`

**Thêm event:**
```csharp
public event EventHandler? ServerResponseReceived;
```

**Kích hoạt event khi nhận response (HTTP):**
```csharp
private async Task ProcessHttpRequest(HttpListenerContext context)
{
    // ... xử lý request/response ...
    
    // Kích hoạt sự kiện
    ServerResponseReceived?.Invoke(this, EventArgs.Empty);
}
```

**Kích hoạt event khi nhận response (TCP):**
```csharp
private async Task RelayDataAsync(...)
{
    // ... relay data ...
    
    // Kích hoạt khi server gửi data về client
    if (direction == "Server -> Client")
    {
        ServerResponseReceived?.Invoke(this, EventArgs.Empty);
    }
}
```

#### 2. `MainWindow.xaml.cs`

**Thêm các trường mới:**
```csharp
// Cờ và dữ liệu cho việc chờ chụp server
private bool _pendingServerCapture = false;
private (int Stage, DateTime Timestamp, string ClientOutput, string UserInput) _pendingCaptureData;
```

**Đăng ký sự kiện khi bắt đầu session:**
```csharp
private async Task StartSessionAsync()
{
    // ...
    
    // Đăng ký lắng nghe sự kiện
    _proxyService.ServerResponseReceived += OnServerResponseReceived;
    
    // ...
}
```

**Hủy đăng ký khi dừng session:**
```csharp
private async void StopSession()
{
    // ...
    
    // Hủy đăng ký
    _proxyService.ServerResponseReceived -= OnServerResponseReceived;
    
    // ...
}
```

**Sửa OnEnterPressed() - không còn dùng delay:**
```csharp
private void OnEnterPressed()
{
    // ... chụp console client ...
    // ... trích xuất input ...
    
    // ❌ XÓA: await Task.Delay(300);
    
    // ✅ THÊM MỚI: Đặt cờ chờ server
    _pendingServerCapture = true;
    _pendingCaptureData = (_currentStage, now, clientOutput, userInput);
    
    // Cập nhật status: đang chờ server
    StatusText.Text = "waiting for server response...";
}
```

**Thêm event handler mới:**
```csharp
private void OnServerResponseReceived(object? sender, EventArgs e)
{
    // Kiểm tra có đang chờ chụp server không?
    if (!_pendingServerCapture) return;
    
    // Reset cờ
    _pendingServerCapture = false;
    
    // Chụp console server NGAY BÂY GIỜ (đúng lúc response về)
    string serverOutput = _consoleCaptureService.CaptureConsoleOutput(_serverProcess.Id);
    
    // Lưu dữ liệu stage đầy đủ
    _stageCaptures.Add((_pendingCaptureData.Stage, _pendingCaptureData.Timestamp, 
                        _pendingCaptureData.ClientOutput, serverOutput));
    
    // Cập nhật status
    StatusText.Text = $"Stage {_pendingCaptureData.Stage} captured";
}
```

## Lợi Ích

### 1. Chính Xác Hơn ✅
- Chụp console server **chính xác** khi response về
- Không bỏ sót log do server xử lý chậm
- Không quan trọng server xử lý bao lâu

### 2. Nhanh Hơn ⚡
- Không chờ 300ms không cần thiết
- Nếu server phản hồi trong 50ms → chụp ngay
- Nếu server phản hồi trong 1000ms → vẫn chụp đúng

### 3. Tin Cậy Hơn 🎯
- Không còn giả định về thời gian xử lý
- Hoạt động tốt bất kể server bận hay rảnh
- Cơ chế rõ ràng, dễ hiểu

## So Sánh Trước & Sau

### Trước (300ms Delay)
```
Enter → Chụp Client → Chờ 300ms ⏱️ → Chụp Server
                              ↑
                    Hy vọng server đã xong
                    (Có thể sai! ❌)
```

**Vấn đề:**
- Server chậm (500ms) → Thiếu log ❌
- Server nhanh (50ms) → Lãng phí 250ms ⏱️

### Sau (Event-Driven)
```
Enter → Chụp Client → Đặt Cờ 🚩 → Chờ...
                                    ↓
Request → ProxyService → Server → Response → Sự Kiện ⚡
                                              ↓
                                    Chụp Server Ngay ✅
```

**Lợi ích:**
- Server chậm (500ms) → Chụp đúng sau 500ms ✅
- Server nhanh (50ms) → Chụp ngay sau 50ms ✅

## Kiểm Tra (Testing)

Vì đây là ứng dụng WPF (Windows), cần kiểm tra trên Windows:

### 1. Test Server Nhanh
- Server phản hồi < 100ms
- Kiểm tra: Console server có bị chụp đầy đủ không?

### 2. Test Server Chậm
- Tạo delay trong server (> 1 giây)
- Kiểm tra: Console server có bị thiếu log không?

### 3. Test Nhiều Request Liên Tiếp
- Bấm Enter nhiều lần nhanh
- Kiểm tra: Mỗi stage có được chụp đúng không?

### 4. Test TCP Protocol
- Chuyển sang TCP mode
- Kiểm tra: Sự kiện có hoạt động với TCP không?

## Tài Liệu

Xem thêm chi tiết tại:
- **[EVENT_DRIVEN_CAPTURE.md](EVENT_DRIVEN_CAPTURE.md)** - Tài liệu đầy đủ (Tiếng Anh & Tiếng Việt)
- **[CHANGES_SUMMARY.md](CHANGES_SUMMARY.md)** - Tóm tắt tất cả thay đổi kỹ thuật

## Kết Luận

Thay đổi này:
- ✅ Loại bỏ giả định 300ms không chính xác
- ✅ Sử dụng ProxyService như "người báo tín hiệu"
- ✅ Chụp console server đúng thời điểm
- ✅ Nhanh hơn, chính xác hơn, tin cậy hơn

**Không có breaking changes** - Tool vẫn hoạt động giống như trước, chỉ là nội bộ chính xác hơn!
