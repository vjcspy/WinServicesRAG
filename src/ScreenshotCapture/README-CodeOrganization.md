# ScreenshotCapture - Code Organization

Dự án đã được tổ chức lại với cấu trúc rõ ràng và dễ maintain hơn.

## Cấu trúc mới

### 📁 **Commands/**
- **`CommandSetup.cs`** - Thiết lập và cấu hình các commands (CLI và Service mode)
  - Tạo RootCommand
  - Định nghĩa options cho từng command
  - Kết nối handlers với commands

### 📁 **Handlers/**
- **`ServiceHandler.cs`** - Xử lý Service mode
  - Chạy background service
  - Ẩn console window
  - Cấu hình Dependency Injection
  - Thiết lập working directories

- **`CliHandler.cs`** - Xử lý CLI mode  
  - Hiển thị provider status
  - Chụp screenshot từ command line
  - Xử lý tham số đầu vào/ra

### 📁 **Core Files**
- **`Program.cs`** - Entry point gọn gàng
  - Cấu hình Serilog logger
  - Khởi tạo commands
  - Xử lý default arguments

- **`ScreenshotBackgroundService.cs`** - Background service implementation
- **`ScreenshotServiceConfig.cs`** - Configuration model

## Lợi ích của cấu trúc mới

### ✅ **Separation of Concerns**
- Mỗi file có trách nhiệm rõ ràng
- Handler riêng biệt cho từng mode
- Command setup tách biệt khỏi business logic

### ✅ **Maintainability**
- Dễ tìm và sửa code theo chức năng
- Thêm features mới không ảnh hưởng code cũ
- Test từng phần riêng biệt

### ✅ **Readability**
- Program.cs ngắn gọn, dễ hiểu
- Logic phức tạp được tách ra handlers
- Tên file và class có nghĩa rõ ràng

### ✅ **Extensibility**
- Dễ thêm commands mới
- Dễ thêm handlers mới
- Dễ modify options và behaviors

## Cách sử dụng

### Service Mode
```bash
ScreenshotCapture.exe service --hide-console --work-dir "C:\MyApp" --poll-interval 10
```

### CLI Mode
```bash
# Kiểm tra status
ScreenshotCapture.exe cli --status --verbose

# Chụp screenshot
ScreenshotCapture.exe cli --output "screenshot.png" --provider "DirectX"
```

### Default Mode
```bash
# Chạy mặc định sẽ vào service mode với hide-console
ScreenshotCapture.exe
```

## Migration Notes

- Tất cả functionality giữ nguyên
- Command line arguments không đổi
- Behaviors và configurations giống hệt trước
- Chỉ thay đổi cách tổ chức code
