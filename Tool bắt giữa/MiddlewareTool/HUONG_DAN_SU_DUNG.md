# Hướng Dẫn Sử Dụng Cơ Chế Mới

## Tổng Quan
Phần mềm đã được cập nhật với cơ chế capture mới, không còn cần file prompts nữa. Thay vào đó, bạn sẽ sử dụng phím F5 để chụp baseline trước khi người dùng nhập input.

## Thay Đổi Chính

### Trước Đây
- Cần chuẩn bị file prompts trước
- So sánh output với file prompts để lấy input
- Khó khăn khi prompts thay đổi

### Bây Giờ
- Bấm F5 để capture màn hình console trước khi nhập input
- Tự động so sánh và trích xuất input
- Linh hoạt với mọi loại prompt

## Hướng Dẫn Từng Bước

### Bước 1: Khởi Động Session
1. Mở phần mềm MiddlewareTool
2. Chọn đường dẫn Server executable
3. Chọn đường dẫn Client executable
4. Chọn AppSettings templates
5. Chọn file Excel để lưu logs
6. Bấm "Start Grading Session"

### Bước 2: Capture Baseline (Quan Trọng!)
Khi màn hình console client hiện prompt (ví dụ: "enter int "):
1. **Bấm phím F5** trong cửa sổ console client
2. Tool sẽ capture màn hình hiện tại làm baseline
3. Đây là Stage 1
4. Thanh status sẽ hiện: "Stage 1 baseline captured..."

### Bước 3: Nhập Input
1. Người dùng nhập input vào console (ví dụ: nhập "1")
2. **Bấm Enter**
3. Tool tự động:
   - So sánh màn hình hiện tại với baseline
   - Trích xuất input ("1")
   - Lưu vào Stage 1
   - Capture console sau khi có response

### Bước 4: Lặp Lại Cho Các Stage Tiếp Theo
Khi có prompt mới (ví dụ: "enter string "):
1. **Bấm F5** lần nữa → Stage 2 baseline
2. Người dùng nhập input và **bấm Enter**
3. Tool tự động xử lý như Stage 1

### Bước 5: Kết Thúc Session
1. Bấm "Stop Grading Session"
2. Tool sẽ:
   - Lưu tất cả logs vào Excel
   - Tạo các file log phụ
   - Hiện thống kê số stages và inputs

## Ví Dụ Thực Tế

### Tình Huống: Người dùng nhập 2 giá trị

**Console client ban đầu:**
```
Welcome to the program
enter int 
```

**Thao tác:**
1. Bấm F5 → Capture "enter int " làm baseline Stage 1
2. Status: "Stage 1 baseline captured..."

**Người dùng nhập:**
```
Welcome to the program
enter int 1
```

**Thao tác:**
3. Bấm Enter
4. Tool trích xuất: "1"
5. Status: "Stage 1 input captured: '1'..."

**Console sau khi có response:**
```
Welcome to the program
enter int 1
banana
enter string 
```

**Thao tác:**
6. Bấm F5 → Capture "enter string " làm baseline Stage 2
7. Status: "Stage 2 baseline captured..."

**Người dùng nhập:**
```
Welcome to the program
enter int 1
banana
enter string hello
```

**Thao tác:**
8. Bấm Enter
9. Tool trích xuất: "hello"
10. Status: "Stage 2 input captured: 'hello'..."

**Kết quả:**
11. Bấm "Stop Grading Session"
12. Excel sẽ có 2 stages với inputs: "1" và "hello"

## Thanh Status (Quan Trọng)

Tool hiện status ở dưới nút Start/Stop:

- **Màu xám**: Session chưa chạy
- **Màu xanh lá đậm**: Session đang chạy, chờ F5
- **Màu xanh lá nhạt**: Baseline đã capture, chờ Enter
- **Màu xanh dương**: Input đã capture thành công
- **Màu cam**: Cảnh báo (không trích xuất được input)

## Các Phím Tắt

- **F5**: Capture baseline cho stage mới (bấm trong console client)
- **Enter**: Capture input sau khi người dùng nhập (bấm trong console client)

## File Output

Sau khi dừng session, tool tạo các files:

1. **[Tên]_LogData.xlsx**: File Excel chính
   - Sheet "Logs": HTTP/TCP requests với stage
   - Sheet "Inputs": User inputs theo stage
   - Sheet "ClientStages": Snapshots console theo stage

2. **[Tên]_Client.log**: Output console client
3. **[Tên]_Server.log**: Output console server
4. **[Tên]_EnterLines.log**: Các dòng khi bấm Enter
5. **[Tên]_UserInputs.log**: User inputs chi tiết

## Lưu Ý Quan Trọng

### Khi Nào Bấm F5?
- Bấm F5 **TRƯỚC KHI** người dùng nhập input
- Bấm khi màn hình console hiện prompt đầy đủ
- Mỗi lần bấm F5 = 1 stage mới

### Khi Nào Bấm Enter?
- Bấm Enter **SAU KHI** người dùng đã nhập input
- Tool sẽ tự động so sánh với baseline của stage hiện tại

### Nếu Quên Bấm F5?
- Tool sẽ không trích xuất được input khi bấm Enter
- Status sẽ hiện cảnh báo màu cam
- Cần bấm F5 lại để tạo baseline mới

### Nếu Bấm F5 Nhiều Lần?
- Mỗi lần bấm F5 tạo stage mới
- Stage number tăng dần
- Chỉ baseline mới nhất được dùng cho stage đó

## So Sánh Với Cơ Chế Cũ

| Tính năng | Cũ (File Prompts) | Mới (F5 Baseline) |
|-----------|-------------------|-------------------|
| Chuẩn bị trước | Cần file prompts | Không cần |
| Linh hoạt | Thấp | Cao |
| Độ chính xác | Phụ thuộc file | Cao (real-time) |
| Dễ sử dụng | Trung bình | Dễ |
| Stage management | Không rõ ràng | Rõ ràng (F5 = stage mới) |

## Xử Lý Lỗi

### Không capture được baseline
**Nguyên nhân:** Không bấm F5 trong console client
**Giải pháp:** Đảm bảo focus vào console client trước khi bấm F5

### Không trích xuất được input
**Nguyên nhân:** 
- Chưa bấm F5 để tạo baseline
- Baseline và output quá khác nhau
**Giải pháp:** 
- Bấm F5 lại để tạo baseline mới
- Kiểm tra console có hiện đúng prompt không

### Status không cập nhật
**Nguyên nhân:** Tool window bị minimize hoặc che khuất
**Giải pháp:** Mở lại tool window để xem status

## Câu Hỏi Thường Gặp

**Q: Có cần file prompts nữa không?**
A: Không, cơ chế mới không dùng file prompts.

**Q: F5 bấm ở đâu?**
A: Bấm trong cửa sổ console client (không phải tool window).

**Q: Có thể bấm Enter nhiều lần cho 1 stage không?**
A: Được, nhưng chỉ lần đầu tiên được lưu. Nên bấm F5 mới cho stage tiếp theo.

**Q: Tool có tự động capture không?**
A: Không, cần bấm F5 thủ công để đánh dấu điểm bắt đầu mỗi stage.

**Q: Có giới hạn số stages không?**
A: Không giới hạn, tùy thuộc vào bài tập.

## Hỗ Trợ

Nếu gặp vấn đề:
1. Kiểm tra thanh status
2. Đọc lại hướng dẫn
3. Thử với ví dụ đơn giản trước
4. Kiểm tra file log để debug

## Yêu Cầu Hệ Thống

- Windows OS (WPF application)
- .NET 8.0 hoặc cao hơn
- Client và Server executables phải chạy được
- Console windows phải visible (không minimize)
