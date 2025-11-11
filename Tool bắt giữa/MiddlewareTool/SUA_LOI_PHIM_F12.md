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

## Giải Pháp

### Thay Đổi trong KeyboardHook.cs

1. **Thêm tham số client process ID** vào phương thức `SetHook()`:
   ```csharp
   public static void SetHook(Action onEnterPressed, Action onCapturePressed, int clientProcessId)
   ```

2. **Chuyển việc kiểm tra foreground window VÀO trong hook callback**:
   - Hook giờ đây kiểm tra xem cửa sổ foreground có thuộc về client process không
   - Việc kiểm tra này xảy ra TRƯỚC KHI quyết định có xử lý hay chuyển phím đi

3. **Chặn phím khi được xử lý bởi MiddlewareTool**:
   - Khi F12 hoặc Enter được nhấn trong cửa sổ client, hook xử lý nó VÀ chặn nó
   - Việc chặn được thực hiện bằng cách trả về `(IntPtr)1` thay vì gọi `CallNextHookEx`
   - Điều này ngăn phím đến ConsoleManager, tránh xử lý kép

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

### Luồng Xử Lý Phím

```
1. Người dùng nhấn F12/Enter trong console client
   ↓
2. Global keyboard hook chặn (HookCallback trong KeyboardHook.cs)
   ↓
3. Kiểm tra foreground window:
   - Có phải client process không? → CÓ
   ↓
4. Xử lý phím cho MiddlewareTool:
   - Gọi _onCapturePressed (cho F12) hoặc _onEnterPressed (cho Enter)
   - Trả về (IntPtr)1 để CHẶN phím
   ↓
5. Phím KHÔNG đến ConsoleManager
   - Không có xử lý kép
   - Không có can thiệp
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

1. **Loại Bỏ Can Thiệp Phím**: F12 và Enter giờ được xử lý độc quyền bởi MiddlewareTool HOẶC client, không bao giờ cả hai
2. **Ngăn Xử Lý Kép**: Background thread của ConsoleManager không còn nhìn thấy F12 khi MiddlewareTool xử lý nó
3. **Duy Trì Hành Vi Bình Thường**: Phím vẫn hoạt động bình thường trong các ứng dụng khác
4. **Code Sạch Hơn**: Kiểm tra foreground window được thực hiện một lần trong hook, không bị trùng lặp trong callbacks

## Kiểm Tra

Để xác minh fix hoạt động:

1. **Test F12 trong console client** (khởi chạy qua MiddlewareTool):
   - F12 nên capture các stage cho MiddlewareTool
   - F12 không nên can thiệp vào hoạt động bình thường của client
   - Client không nên thấy phím F12

2. **Test Enter trong console client** (khởi chạy qua MiddlewareTool):
   - Enter nên track user input cho MiddlewareTool
   - Enter không nên được chuyển tới client (để tránh xử lý kép)
   - Client không nên nhận các sự kiện Enter trùng lặp

3. **Test F12 trong các ứng dụng khác**:
   - F12 nên hoạt động bình thường trong các cửa sổ khác
   - Chỉ khi console client ở foreground thì MiddlewareTool mới xử lý nó

4. **Test khởi chạy thủ công**:
   - Khi client được khởi chạy thủ công (không qua MiddlewareTool), F12 nên hoạt động bình thường
   - Không có global hook hoạt động, nên ConsoleManager xử lý F12 như thiết kế

## Hạn Chế Đã Biết

**Chặn Phím Enter**: Fix này chặn các phím Enter khi nhấn trong console client. Điều này có nghĩa là phím Enter sẽ không đến lời gọi Console.ReadLine() trong client. Tuy nhiên, đây là hành vi dự định vì:

1. MiddlewareTool cần capture chính xác thời điểm Enter được nhấn để trích xuất user input
2. Nếu Enter được chuyển qua, client sẽ xử lý nó VÀ MiddlewareTool sẽ capture nó, dẫn đến xử lý kép
3. Trải nghiệm người dùng là họ nhấn Enter, MiddlewareTool capture input, và client tiếp tục bình thường

Nếu Enter cần đến client để ReadLine() hoạt động, các sửa đổi bổ sung sẽ cần thiết để:
- Để MiddlewareTool capture input trước
- Sau đó lập trình gửi Enter tới console client sau khi capture
- Điều này phức tạp hơn và có thể không cần thiết tùy thuộc vào hành vi thực tế của client

## Phương Pháp Thay Thế (Chưa Thực Hiện)

Một phương pháp thay thế là **sửa đổi ConsoleManager** trong client để:
1. Không sử dụng background thread để giám sát phím
2. Chỉ kiểm tra F12 tại các điểm cụ thể (trước các lời gọi ReadLine)
3. Hoặc vô hiệu hóa hoàn toàn giám sát F12 khi khởi chạy qua MiddlewareTool

Tuy nhiên, điều này sẽ yêu cầu thay đổi code client, điều này có thể không mong muốn nếu client là một phần của bài nộp của sinh viên hoặc code bên ngoài không nên được sửa đổi.

## Kết Luận

Fix này giải quyết vấn đề can thiệp phím bằng cách đảm bảo rằng khi global hook của MiddlewareTool xử lý một phím cho cửa sổ client, phím đó được chặn và không đến ứng dụng client. Điều này ngăn ConsoleManager cũng xử lý phím và gây ra xung đột.

Fix này là tối thiểu, tập trung, và không thay đổi kiến trúc tổng thể của MiddlewareTool - nó chỉ làm cho keyboard hook thông minh hơn về thời điểm chặn phím so với thời điểm chuyển chúng qua.

## Giải Thích Chi Tiết Về Vấn Đề Người Dùng Gặp Phải

Khi người dùng thiết lập một phím để thoát console client và nhấn phím đó:

**Trước khi fix:**
- Khi khởi chạy qua MiddlewareTool: Global hook chặn phím → chuyển qua cho client → ConsoleManager cũng xử lý → gây ra can thiệp hoặc hành vi không mong muốn
- Khi khởi chạy thủ công: Không có global hook → phím hoạt động bình thường

**Sau khi fix:**
- Khi khởi chạy qua MiddlewareTool: Global hook chặn phím (nếu là F12/Enter) → KHÔNG chuyển qua → không can thiệp
- Khi khởi chạy thủ công: Không có global hook → phím hoạt động bình thường

Điều này có nghĩa là nếu người dùng sử dụng F12 hoặc Enter làm phím thoát, giờ đây chúng sẽ được xử lý chính xác bởi MiddlewareTool và không gây ra can thiệp với logic thoát của client.
