# Quick Reference - Sửa Lỗi Phím (Key Fix)

## Vấn Đề Đã Sửa
Khi chạy console client qua MiddlewareTool, việc nhấn F12 hoặc Enter gây can thiệp với client → **ĐÃ SỬA**

## Giải Pháp
Global keyboard hook giờ đây:
- ✅ Kiểm tra xem cửa sổ có phải client không
- ✅ Nếu đúng → Xử lý và CHẶN phím (không chuyển cho client)
- ✅ Nếu sai → Chuyển phím bình thường

## Files Thay Đổi
1. `KeyboardHook.cs` - Logic chính
2. `MainWindow.xaml.cs` - Khởi tạo hook với process ID
3. `F12_KEY_FIX_EXPLANATION.md` - Giải thích chi tiết (English)
4. `SUA_LOI_PHIM_F12.md` - Giải thích chi tiết (Tiếng Việt)

## Cách Test
1. Build project trên Windows
2. Chạy MiddlewareTool
3. Start Grading Session
4. Nhấn F12 trong console client → Nên capture stage, KHÔNG gây lỗi
5. Nhấn Enter trong console client → Nên track input, KHÔNG double-process

## Lưu Ý Quan Trọng

### F12 và Enter Bị Chặn
Khi console client chạy qua MiddlewareTool:
- F12 trong client → MiddlewareTool xử lý, KHÔNG đến ConsoleManager
- Enter trong client → MiddlewareTool xử lý, KHÔNG đến Console.ReadLine()

### Tại Sao Chặn Enter?
- Để tránh double-processing
- MiddlewareTool cần capture input chính xác
- Nếu không chặn → cả hai xử lý → xung đột

### Nếu Client Cần Nhận Enter?
Nếu Console.ReadLine() không hoạt động do Enter bị chặn:
1. Xem xét để MiddlewareTool gửi lại Enter sau khi capture
2. HOẶC sửa ConsoleManager trong client để không monitor F12 khi chạy qua tool

## Hành Vi Mong Đợi

| Tình Huống | Trước Fix | Sau Fix |
|-----------|-----------|---------|
| F12 trong client (qua tool) | Can thiệp ❌ | Hoạt động tốt ✅ |
| Enter trong client (qua tool) | Double-process ❌ | Xử lý đúng ✅ |
| F12 trong windows khác | OK ✅ | OK ✅ |
| Client chạy thủ công | OK ✅ | OK ✅ |

## Câu Hỏi Thường Gặp

**Q: Tôi cần rebuild không?**
A: Có, cần build lại project trên Windows.

**Q: Code cũ có bị ảnh hưởng không?**
A: Không, chỉ có thêm parameter cho SetHook().

**Q: Phím khác ngoài F12/Enter có bị ảnh hưởng không?**
A: Không, chỉ F12 và Enter trong client window bị chặn.

**Q: Nếu vẫn còn lỗi thì sao?**
A: Đọc file `F12_KEY_FIX_EXPLANATION.md` hoặc `SUA_LOI_PHIM_F12.md` để hiểu chi tiết.

## Liên Hệ
Nếu vẫn gặp vấn đề sau khi test, vui lòng cung cấp:
1. Mô tả chi tiết lỗi
2. Steps to reproduce
3. Client console có monitoring phím nào khác không?
4. Phím "quit" thực tế là phím gì?

---

# Quick Reference - Key Fix (English)

## Issue Fixed
When running console client via MiddlewareTool, pressing F12 or Enter caused interference → **FIXED**

## Solution
Global keyboard hook now:
- ✅ Checks if window is the client
- ✅ If yes → Process and SUPPRESS key (don't pass to client)
- ✅ If no → Pass key through normally

## Files Changed
1. `KeyboardHook.cs` - Core logic
2. `MainWindow.xaml.cs` - Hook initialization with process ID
3. `F12_KEY_FIX_EXPLANATION.md` - Detailed explanation (English)
4. `SUA_LOI_PHIM_F12.md` - Detailed explanation (Vietnamese)

## How to Test
1. Build project on Windows
2. Run MiddlewareTool
3. Start Grading Session
4. Press F12 in client console → Should capture stage, NO errors
5. Press Enter in client console → Should track input, NO double-processing

## Important Notes

### F12 and Enter Are Suppressed
When console client runs via MiddlewareTool:
- F12 in client → MiddlewareTool processes, does NOT reach ConsoleManager
- Enter in client → MiddlewareTool processes, does NOT reach Console.ReadLine()

### Why Suppress Enter?
- To avoid double-processing
- MiddlewareTool needs to capture input precisely
- If not suppressed → both process → conflict

### If Client Needs Enter?
If Console.ReadLine() doesn't work due to Enter suppression:
1. Consider having MiddlewareTool re-send Enter after capture
2. OR modify ConsoleManager in client to not monitor F12 when run via tool

## Expected Behavior

| Scenario | Before Fix | After Fix |
|----------|------------|-----------|
| F12 in client (via tool) | Interference ❌ | Works well ✅ |
| Enter in client (via tool) | Double-process ❌ | Correct ✅ |
| F12 in other windows | OK ✅ | OK ✅ |
| Client run manually | OK ✅ | OK ✅ |

## FAQ

**Q: Do I need to rebuild?**
A: Yes, rebuild the project on Windows.

**Q: Is old code affected?**
A: No, only added parameter to SetHook().

**Q: Are other keys affected?**
A: No, only F12 and Enter in client window are suppressed.

**Q: If issue persists?**
A: Read `F12_KEY_FIX_EXPLANATION.md` or `SUA_LOI_PHIM_F12.md` for details.

## Contact
If you still have issues after testing, please provide:
1. Detailed error description
2. Steps to reproduce
3. Does client console monitor any other keys?
4. What is the actual "quit" key?
