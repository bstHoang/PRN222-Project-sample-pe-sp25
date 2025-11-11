# Sửa Lỗi Can Thiệp Phím F12

## Mô Tả Vấn Đề

Khi console client được khởi chạy thông qua nút "Start Grading Session" của MiddlewareTool, việc nhấn phím F12 hoặc Enter trong console client gây ra sự can thiệp với việc xử lý phím của ứng dụng client. Nguyên nhân là:

1. **MiddlewareTool** sử dụng global keyboard hook (KeyboardHook.cs) để chặn phím F12 và Enter nhằm capture các stage console và user input
2. **Console Client** (ConsoleManager.cs) có một background thread giám sát phím F12 để xóa input buffer
3. Khi cả hai hệ thống hoạt động, cả hai đều cố gắng xử lý cùng một phím, gây ra xung đột

## Nguyên Nhân Gốc Rễ

### Trước Khi Sửa

Global keyboard hook trong `KeyboardHook.cs`:
1. Chặn TẤT CẢ các phím F12 và Enter trong toàn hệ thống
2. Gọi callback để kiểm tra xem cửa sổ foreground có phải là client không
3. **Luôn luôn** gọi `CallNextHookEx` để chuyển phím tới ứng dụng đích

Điều này có nghĩa là ngay cả khi MiddlewareTool xử lý phím, nó vẫn chuyển phím đó tới console client. Background thread của ConsoleManager sau đó CŨNG nhìn thấy phím và cố gắng xử lý nó, gây ra xử lý kép và can thiệp.

Ngoài ra, thread `MonitorF12Key()` của ConsoleManager có lỗi khi nó tiêu thụ TẤT CẢ các phím (không chỉ F12) bằng cách gọi `Console.ReadKey(true)`, điều này loại bỏ phím khỏi input buffer. Điều này làm mất các phím.

## Giải Pháp (Đã Cập Nhật)

### Cập Nhật Quan Trọng - Phiên Bản 2

**Fix ban đầu đã chặn CẢ F12 VÀ Enter, điều này làm hỏng Console.ReadLine()!** Khi Enter bị chặn, ứng dụng console không thể hoàn thành các lời gọi ReadLine(), khiến nó bị treo hoặc hoạt động không đúng.

**Fix cập nhật chỉ chặn F12, cho phép Enter chuyển qua bình thường.**

### Thay Đổi trong KeyboardHook.cs

1. **Thêm tham số client process ID** vào phương thức `SetHook()`:
   ```csharp
   public static void SetHook(Action onEnterPressed, Action onCapturePressed, int clientProcessId)
   ```

2. **Chuyển việc kiểm tra foreground window VÀO trong hook callback**:
   - Hook giờ đây kiểm tra xem cửa sổ foreground có thuộc về client process không
   - Việc kiểm tra này xảy ra TRƯỚC KHI quyết định có xử lý hay chuyển phím đi

3. **Chỉ chặn F12, cho phép Enter chuyển qua**:
   - Khi **F12** được nhấn trong cửa sổ client, hook xử lý nó VÀ chặn nó
   - Khi **Enter** được nhấn trong cửa sổ client, hook xử lý nó NHƯNG cho phép nó chuyển qua
   - Việc chặn được thực hiện bằng cách trả về `(IntPtr)1` thay vì gọi `CallNextHookEx`
   - Điều này ngăn F12 đến ConsoleManager (tránh xử lý kép) trong khi cho phép Enter hoạt động bình thường

4. **Chuyển phím qua cho các cửa sổ khác**:
   - Nếu cửa sổ foreground KHÔNG phải là client, phím được chuyển qua bình thường
   - Điều này đảm bảo F12 và Enter vẫn hoạt động trong các ứng dụng khác

### Thay Đổi trong MainWindow.xaml.cs

1. **Cập nhật lời gọi KeyboardHook.SetHook** để truyền client process ID:
   ```csharp
   KeyboardHook.SetHook(OnEnterPressed, OnCapturePressed, _clientProcess.Id);
   ```

2. **Loại bỏ kiểm tra foreground window thừa** từ `OnCapturePressed()` và `OnEnterPressed()`:
   - Các kiểm tra này giờ được thực hiện trong chính hook
   - Các callback chỉ chạy khi kiểm tra đã pass

3. **Loại bỏ các Windows API import không sử dụng** (GetForegroundWindow, GetWindowThreadProcessId):
   - Chúng giờ chỉ có trong KeyboardHook.cs nơi chúng thực sự được sử dụng

## Cách Hoạt Động Bây Giờ

### Luồng Xử Lý Phím F12

```
1. Người dùng nhấn F12 trong console client
   ↓
2. Global keyboard hook chặn (HookCallback trong KeyboardHook.cs)
   ↓
3. Kiểm tra foreground window:
   - Có phải client process không? → CÓ
   ↓
4. Xử lý F12 cho MiddlewareTool:
   - Gọi _onCapturePressed
   - Trả về (IntPtr)1 để CHẶN phím
   ↓
5. F12 KHÔNG đến ConsoleManager
   - Không có xử lý kép ✅
   - Không có can thiệp ✅
```

### Luồng Xử Lý Phím Enter

```
1. Người dùng nhấn Enter trong console client
   ↓
2. Global keyboard hook chặn
   ↓
3. Kiểm tra foreground window:
   - Có phải client process không? → CÓ
   ↓
4. Xử lý Enter cho MiddlewareTool:
   - Gọi _onEnterPressed (track input)
   - Gọi CallNextHookEx để CHUYỂN QUA phím
   ↓
5. Enter ĐẾN Console.ReadLine()
   - ReadLine hoàn thành bình thường ✅
   - Ứng dụng hoạt động đúng ✅
```

### Nhấn Phím Trong Các Cửa Sổ Khác

```
1. Người dùng nhấn F12/Enter trong cửa sổ khác
   ↓
2. Global keyboard hook chặn
   ↓
3. Kiểm tra foreground window:
   - Có phải client process không? → KHÔNG
   ↓
4. Chuyển phím qua via CallNextHookEx
   ↓
5. Phím đến ứng dụng đích bình thường
```

## Lợi Ích

1. **Loại Bỏ Can Thiệp F12**: F12 được xử lý độc quyền bởi MiddlewareTool khi ở cửa sổ client
2. **Duy Trì Chức Năng Enter**: Enter chuyển qua để Console.ReadLine() hoạt động bình thường
3. **Ngăn Xử Lý Kép**: ConsoleManager không còn nhìn thấy F12
4. **Không Bị Treo**: Ứng dụng console không bị treo chờ Enter
5. **Code Sạch Hơn**: Kiểm tra foreground window được thực hiện một lần trong hook

## Kiểm Tra

Để xác minh fix hoạt động:

1. **Test F12 trong console client** (khởi chạy qua MiddlewareTool):
   - F12 nên capture các stage cho MiddlewareTool ✅
   - F12 không nên được nhìn thấy bởi ConsoleManager ✅
   - Không có xử lý kép hoặc can thiệp ✅

2. **Test Enter trong console client** (khởi chạy qua MiddlewareTool):
   - Enter nên track user input cho MiddlewareTool ✅
   - Enter nên đến Console.ReadLine() ✅
   - Ứng dụng nên phản hồi input bình thường ✅
   - Không bị treo hoặc đóng băng ✅

3. **Test F12 trong các ứng dụng khác**:
   - F12 nên hoạt động bình thường trong các cửa sổ khác ✅
   - Chỉ khi console client ở foreground thì MiddlewareTool mới xử lý nó ✅

4. **Test khởi chạy thủ công**:
   - Khi client được khởi chạy thủ công (không qua MiddlewareTool), F12 nên hoạt động bình thường ✅
   - Không có global hook hoạt động, nên ConsoleManager xử lý F12 như thiết kế ✅

## Sự Khác Biệt Quan Trọng: Enter vs F12

| Phím | Hành Vi Trong Cửa Sổ Client | Lý Do |
|-----|----------------------------|-------|
| **F12** | Bị chặn (blocked) | Ngăn xử lý kép với ConsoleManager |
| **Enter** | Chuyển qua | Cần thiết để Console.ReadLine() hoạt động |

**Tại sao không chặn Enter?**
- Các ứng dụng console sử dụng `Console.ReadLine()` mà CHẶN chờ Enter
- Nếu Enter bị chặn, ReadLine không bao giờ hoàn thành → ứng dụng bị treo
- MiddlewareTool chỉ cần QUAN SÁT Enter (track input), không chặn nó

**Tại sao chặn F12?**
- Background thread của ConsoleManager giám sát F12
- Nếu F12 đến nó, xử lý kép xảy ra
- MiddlewareTool là handler chính cho F12 trong các session grading

## Các Vấn Đề Đã Giải Quyết

**Vấn đề 1: Console bị treo khi chặn Enter**
- ✅ ĐÃ SỬA: Enter giờ chuyển qua

**Vấn đề 2: F12 gây xử lý kép**
- ✅ ĐÃ SỬA: F12 bị chặn cho cửa sổ client

**Vấn đề 3: ConsoleManager tiêu thụ các phím không phải F12**
- ⚠️ HẠN CHẾ: Đây là lỗi trong chính ConsoleManager, nhưng bằng cách chặn F12 trong hook, chúng tôi ngăn các vấn đề liên quan đến F12
- Lỗi tiêu thụ phím vẫn tồn tại cho các phím khác nhưng nằm ngoài phạm vi fix này

## Kết Luận

Fix này giải quyết vấn đề can thiệp phím bằng cách:
1. **Chặn F12** khi nhấn trong cửa sổ client (ngăn can thiệp ConsoleManager)
2. **Cho phép Enter chuyển qua** để Console.ReadLine() hoạt động bình thường (ngăn treo)
3. **Quan sát cả hai phím** trong các callback của MiddlewareTool (để track)

Fix này là tối thiểu, tập trung, và không thay đổi kiến trúc tổng thể của MiddlewareTool - nó chỉ làm cho keyboard hook thông minh hơn về phím nào cần chặn so với phím nào cần chuyển qua.

## Giải Thích Chi Tiết Về Vấn Đề Người Dùng Gặp Phải

**Vấn đề ban đầu:** Console client bị đóng khi nhấn phím quit thông qua MiddlewareTool.

**Nguyên nhân thực sự:** Fix ban đầu chặn phím Enter, khiến `Console.ReadLine()` không thể hoàn thành. Điều này làm cho ứng dụng bị treo hoặc hoạt động không đúng, dẫn đến console bị đóng hoặc crash.

**Giải pháp:** Chỉ chặn F12, cho phép Enter chuyển qua. Bây giờ:
- Người dùng nhấn Enter → ReadLine hoàn thành bình thường
- Ứng dụng xử lý input đúng cách
- Không bị treo, không bị đóng bất thường
