# Ứng dụng Giám sát Windows Bền bỉ (Resilient Windows Monitoring)

## Giới thiệu

Dự án này cung cấp một giải pháp phần mềm để giám sát một máy tính Windows với các yêu cầu khắt khe về độ ổn định và khả năng chống can thiệp. Ứng dụng được thiết kế để chạy như một tiến trình hệ thống, tự động khởi động lại khi gặp sự cố, và được bảo vệ khỏi việc bị chấm dứt bởi các công cụ thông thường như Task Manager.

## Kiến trúc

Giải pháp được xây dựng dựa trên kiến trúc đa thành phần để đảm bảo tính bền bỉ tối đa:

1. **Dịch vụ Worker (`WorkerService`):**
   - Là dịch vụ chính, chịu trách nhiệm thu thập các chỉ số hệ thống (CPU, RAM, Disk, v.v.).
   - Thực hiện giao tiếp qua HTTP/HTTPS để gửi dữ liệu giám sát đến một máy chủ từ xa.
   - Chứa logic nghiệp vụ cốt lõi của ứng dụng.
2. **Dịch vụ Watchdog (`WatchdogService`):**
   - Là một dịch vụ giám sát phụ, có nhiệm vụ duy nhất là theo dõi trạng thái của `WorkerService`.
   - Nếu `WorkerService` bị dừng vì bất kỳ lý do gì (crash, bị kill, dừng thủ công), `WatchdogService` sẽ tự động khởi động lại nó.

**Công nghệ đề xuất:**

- **Ngôn ngữ:** C#
- **Nền tảng:** .NET 8
- **Loại dự án:** Worker Service

## Các Lớp Bảo Vệ & Phục Hồi

Ứng dụng triển khai 3 lớp bảo vệ để đáp ứng yêu cầu "không thể bị tiêu diệt":

### Lớp 1: Cấu hình Phục hồi Tự động của Dịch vụ

Tận dụng tính năng có sẵn của Windows Service Control Manager (SCM) để tự động khởi động lại dịch vụ khi nó gặp lỗi.

### Lớp 2: Mô hình Watchdog

Dịch vụ Watchdog hoạt động như một người bảo vệ độc lập, đảm bảo dịch vụ chính luôn chạy, ngay cả khi bị dừng thủ công.

### Lớp 3: Tiến trình được Bảo vệ (Protected Process Light - PPL)

Đây là lớp bảo vệ cao nhất, ngăn chặn các tiến trình khác (bao gồm cả Task Manager chạy với quyền quản trị) chấm dứt tiến trình dịch vụ. Điều này biến ứng dụng thành một thành phần gần như của hệ điều hành.

## Hướng dẫn Triển khai

### Yêu cầu

- .NET 8 SDK
- Visual Studio 2022 (hoặc trình soạn thảo mã khác)
- Quyền Administrator trên máy mục tiêu.

### Các bước triển khai

1. **Build Dự án:**

   - Mở solution trong Visual Studio hoặc sử dụng dòng lệnh.

   - Build dự án ở chế độ `Release`:

     ```
     dotnet publish -c Release
     ```

2. **Cài đặt Dịch vụ Worker:** Mở Command Prompt hoặc PowerShell với quyền Administrator và chạy lệnh sau:

   ```
   sc create "MyMonitorWorker" binPath="C:\Duong\Dan\Den\WorkerService.exe" start="auto"
   ```

   *Thay `C:\Duong\Dan\Den\WorkerService.exe` bằng đường dẫn thực tế đến file thực thi của bạn sau khi publish.*

3. **Cài đặt Dịch vụ Watchdog:** Tương tự, cài đặt dịch vụ Watchdog:

   ```
   sc create "MyMonitorWatchdog" binPath="C:\Duong\Dan\Den\WatchdogService.exe" start="auto"
   ```

4. **Cấu hình Phục hồi (Lớp 1):** Cấu hình cho cả hai dịch vụ để chúng tự khởi động lại.

   ```
   # Cấu hình cho Worker Service
   sc failure "MyMonitorWorker" reset= 86400 actions= restart/60000/restart/60000/restart/60000
   
   # Cấu hình cho Watchdog Service
   sc failure "MyMonitorWatchdog" reset= 86400 actions= restart/60000/restart/60000/restart/60000
   ```

   *Lệnh này cấu hình dịch vụ để khởi động lại sau 60 giây cho 3 lần lỗi đầu tiên. Thời gian chờ sẽ được đặt lại sau 1 ngày (86400 giây).*

5. **Khởi động Dịch vụ:**

   ```
   sc start "MyMonitorWorker"
   sc start "MyMonitorWatchdog"
   ```

### (Nâng cao) Triển khai Bảo vệ PPL (Lớp 3)

**CẢNH BÁO:** Việc kích hoạt PPL sẽ khiến việc gỡ lỗi (debug) trở nên cực kỳ khó khăn. Chỉ thực hiện bước này khi ứng dụng đã ổn định hoàn toàn.

1. **Tạo và Cài đặt Chứng chỉ:**
   - Bạn cần tạo một chứng chỉ tự ký (self-signed certificate) và cài đặt nó vào kho `Trusted Root Certification Authorities` và `Trusted Publishers` trên máy cục bộ.
   - Sử dụng chứng chỉ này để ký file `.exe` của cả hai dịch vụ bằng công cụ như `signtool.exe`.
2. **Cấu hình Registry:**
   - Mở Registry Editor (`regedit`).
   - Đi đến `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MyMonitorWorker`.
   - Tạo một giá trị `DWORD (32-bit)` mới tên là `LaunchProtected`.
   - Đặt giá trị của nó là `0x8` (mức `WinTcbSigner`).
   - Lặp lại cho `MyMonitorWatchdog`.
3. **Khởi động lại máy** để các thay đổi về Registry và PPL có hiệu lực.

## Cấu hình

Các cấu hình của ứng dụng, chẳng hạn như URL của máy chủ giám sát, có thể được đặt trong file `appsettings.json`.

```json
{
  "MonitoringSettings": {
    "ServerUrl": "https://your-monitoring-server.com/api/data",
    "IntervalSeconds": 30
  }
}
```