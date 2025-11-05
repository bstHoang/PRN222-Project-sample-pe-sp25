# Quick Reference Card / Tham Khảo Nhanh

## English Version

### Quick Start
1. **Start Session** → Click "Start Grading Session"
2. **Capture Baseline** → Press **F5** in client console (before user input)
3. **Capture Input** → User types input and presses **Enter**
4. **Repeat** → Press **F5** for next stage, then Enter after input
5. **Stop Session** → Click "Stop Grading Session"

### Key Bindings
- **F5** = Capture baseline (in client console) → Creates new stage
- **Enter** = Capture input (in client console) → Extracts user input

### Status Colors
- 🔴 **Gray** = Session not running
- 🟢 **Dark Green** = Session running, waiting for F5
- 🟢 **Green** = Baseline captured, waiting for Enter
- 🔵 **Blue** = Input captured successfully
- 🟠 **Orange** = Warning - input extraction failed

### File Outputs
- `[Name]_LogData.xlsx` → Main Excel file (3 sheets)
- `[Name]_Client.log` → Client console output
- `[Name]_Server.log` → Server console output
- `[Name]_EnterLines.log` → Lines captured on Enter
- `[Name]_UserInputs.log` → User inputs by stage

---

## Vietnamese Version / Phiên Bản Tiếng Việt

### Khởi Động Nhanh
1. **Bắt đầu** → Bấm "Start Grading Session"
2. **Chụp Baseline** → Bấm **F5** trong console client (trước khi nhập)
3. **Chụp Input** → Người dùng nhập và bấm **Enter**
4. **Lặp lại** → Bấm **F5** cho stage mới, sau đó Enter sau khi nhập
5. **Dừng lại** → Bấm "Stop Grading Session"

### Phím Tắt
- **F5** = Chụp baseline (trong console client) → Tạo stage mới
- **Enter** = Chụp input (trong console client) → Lấy input người dùng

### Màu Status
- 🔴 **Xám** = Session chưa chạy
- 🟢 **Xanh lá đậm** = Session đang chạy, chờ F5
- 🟢 **Xanh lá nhạt** = Đã chụp baseline, chờ Enter
- 🔵 **Xanh dương** = Đã chụp input thành công
- 🟠 **Cam** = Cảnh báo - không lấy được input

### File Kết Quả
- `[Tên]_LogData.xlsx` → File Excel chính (3 sheets)
- `[Tên]_Client.log` → Output console client
- `[Tên]_Server.log` → Output console server
- `[Tên]_EnterLines.log` → Các dòng khi bấm Enter
- `[Tên]_UserInputs.log` → User inputs theo stage

---

## Common Workflow / Quy Trình Thông Dụng

### Example / Ví Dụ

```
Console: "enter int "
  ↓
Press F5 (Stage 1 baseline)
  ↓
User types: 1
  ↓
Press Enter (captures "1")
  ↓
Console: "banana"
Console: "enter string "
  ↓
Press F5 (Stage 2 baseline)
  ↓
User types: hello
  ↓
Press Enter (captures "hello")
  ↓
Stop Session
```

### Troubleshooting / Xử Lý Lỗi

| Problem | Solution |
|---------|----------|
| Can't capture baseline / Không chụp được baseline | Press F5 **in client console** / Bấm F5 **trong console client** |
| Can't extract input / Không lấy được input | Press F5 first to create baseline / Bấm F5 trước để tạo baseline |
| Status not updating / Status không cập nhật | Check tool window is visible / Kiểm tra tool window có hiện không |
| F5 not working / F5 không hoạt động | Focus on client console / Focus vào console client |

---

## Important Notes / Lưu Ý Quan Trọng

### ✅ DO / NÊN
- Press F5 BEFORE each user input / Bấm F5 TRƯỚC mỗi lần nhập
- Press F5 in client console window / Bấm F5 trong cửa sổ console client
- Check status text for feedback / Kiểm tra status để biết trạng thái
- Stop session when done / Dừng session khi xong

### ❌ DON'T / KHÔNG NÊN
- Don't press F5 in tool window / Không bấm F5 trong tool window
- Don't press Enter without F5 first / Không bấm Enter mà chưa bấm F5
- Don't minimize console windows / Không minimize console windows
- Don't need prompt files anymore / Không cần file prompts nữa

---

## Comparison / So Sánh

| Feature | Old Method | New Method |
|---------|------------|------------|
| Setup / Thiết lập | Prompt file needed / Cần file prompts | No file needed / Không cần file |
| Flexibility / Linh hoạt | Low / Thấp | High / Cao |
| Accuracy / Độ chính xác | Depends on file / Tùy file | Real-time / Thời gian thực |
| Stages / Stages | Unclear / Không rõ | Clear (F5 = new stage) / Rõ ràng |

---

## Support / Hỗ Trợ

- Check status bar for real-time feedback / Xem status bar để biết trạng thái
- Review log files if issues / Xem file log nếu có vấn đề
- Refer to full documentation / Xem tài liệu đầy đủ:
  - **English**: NEW_CAPTURE_MECHANISM.md, CHANGES_SUMMARY.md
  - **Vietnamese**: HUONG_DAN_SU_DUNG.md
