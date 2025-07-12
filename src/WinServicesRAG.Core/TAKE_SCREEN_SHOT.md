# Take screen shot mechanism

## Bảng Chi Tiết Triển Khai Giải Pháp Chụp Ảnh Màn hình trên Windows 11

| Tiêu Chí                     | **1. Windows Graphics Capture API (Ưu tiên hàng đầu)**       | **2. DirectX Desktop Duplication API (Vortice.Windows)**     |
| ---------------------------- | ------------------------------------------------------------ | ------------------------------------------------------------ |
| **Mô Tả API**                | API hiện đại, an toàn, hiệu quả do Microsoft khuyến nghị cho ghi hình/chụp màn hình trên Windows 10/11. Hoạt động ở cấp độ "màn hình logic". | API cấp thấp của DirectX (DXGI) để tạo bản sao desktop trực tiếp trên GPU. Hiệu suất cực cao. Hoạt động ở cấp độ "buffer vật lý". |
| **Yêu Cầu HĐH**              | Windows 10 (Build 17134+) hoặc Windows 11.                   | Windows 8.1+ (DirectX 11.1+). Hoạt động tốt trên Windows 10/11. |
| **Loại Ứng Dụng**            | Desktop Agent (Console Application chạy trong User Session). | Desktop Agent (Console Application chạy trong User Session). |
| **Framework .NET**           | .NET 8 LTS (hoặc .NET 9).                                    | .NET 8 LTS (hoặc .NET 9).                                    |
| **Gói NuGet Cần Thiết**      | `Microsoft.Windows.SDK.Win32` (cung cấp các bindings cho WinRT APIs như Graphics Capture).<br>`Microsoft.UI.Dispatching` (để tạo `DispatcherQueue` trong Console App). | `Vortice.Windows` (cung cấp các bindings cho DirectX API).   |
| **Các Bước Implement Chính** | **1. Thiết lập `DispatcherQueue`:**     - Trong `Program.cs` hoặc điểm khởi đầu của Desktop Agent, khởi tạo một `DispatcherQueueController` và chạy một message loop đơn giản. <br>   - Ví dụ: <br>     `DispatcherQueueController controller = null;` <br>     `if (DispatcherQueue.GetForCurrentThread() == null) { controller = DispatcherQueueController.CreateOnCurrentThread(); }` <br>     `// ... Khởi tạo và chạy logic chụp ...` <br>     `// Trong một thread riêng hoặc khi cần đợi sự kiện` <br>     `// Windows.UI.Xaml.Application.Start((p) => { var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()); SynchronizationContext.SetCurrent(context); });` <br>     `// Dispose controller khi kết thúc.` <br> **2. Lựa chọn nguồn chụp (`GraphicsCaptureItem`):**     - Chụp toàn màn hình: `GraphicsCaptureItem.TryCreateFromDisplayId(DisplayId.PrimaryDisplayId);` <br>   - Chụp cửa sổ cụ thể: `GraphicsCaptureItem.TryCreateFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));` (cần `hwnd` của cửa sổ mục tiêu, có thể lấy bằng `FindWindow`, `GetForegroundWindow` từ `Microsoft.Windows.SDK.Win32`). <br> **3. Khởi tạo DirectX Device:**     - Tạo một `IDirect3DDevice` từ `Vortice.Direct3D11.ID3D11Device` (giống như Desktop Duplication API). <br>   - Chuyển đổi nó sang `Windows.Graphics.DirectX.Direct3D11.IDirect3DDevice` bằng `Direct3D11Helper.CreateDirect3DDevice(vorticeDevice)`. <br> **4. Tạo `GraphicsCaptureSession`:**     - `_framePool = GraphicsCaptureHelper.CreateFramePool(winrtDevice, _item.Size);` <br>   - Gán sự kiện `_framePool.FrameArrived += OnFrameArrived;` <br>   - `_session = _item.CreateCaptureSession();` <br>   - `_session.StartCapture();` <br> **5. Xử lý Frame (trong `OnFrameArrived`):**     - `using (var frame = _framePool.TryGetNextFrame()) { ... }` <br>   - `frame.Surface` là một `IDirect3DTexture2D`. Bạn cần sao chép dữ liệu từ texture này sang bộ nhớ CPU để lưu thành ảnh (PNG/JPEG) hoặc xử lý. Bước này tương tự như trong DirectX Desktop Duplication API. | **1. Khởi tạo DirectX Device:**     - Tạo `Vortice.Direct3D11.ID3D11Device` và `ID3D11DeviceContext`. <br>   - Ví dụ: <br>     `_d3dDevice = Vortice.Direct3D11.D3D11.D3D11CreateDevice(` <br>     ` null, Vortice.Direct3D11.DriverType.Hardware, Vortice.Direct3D11.DeviceCreationFlags.BgraSupport,` <br>     `  null, out var device, out var context);` <br> **2. Lấy `IDXGIOutputDuplication`:**     - Lấy `IDXGIFactory1`, `IDXGIAdapter`, `IDXGIOutput` tương ứng với màn hình mong muốn. <br>   - Ép kiểu `IDXGIOutput` thành `IDXGIOutput1`. <br>   - `_duplication = output1.DuplicateOutput(d3dDevice);` <br> **3. Xử lý Frame (trong vòng lặp hoặc sự kiện):**     - Gọi `_duplication.AcquireNextFrame` để lấy frame mới. <br>   - Khi có frame (output có `OutputBuffer`): <br>     - `var acquiredDesktopImage = outputFrame.DesktopImage.QueryInterface<Vortice.Direct3D11.ID3D11Texture2D>();` <br>     - Sao chép dữ liệu từ `acquiredDesktopImage` sang một texture staging trên CPU: `d3dContext.CopyResource(acquiredDesktopImage, stagingTexture);` <br>     - Ánh xạ (Map) staging texture để lấy con trỏ tới dữ liệu pixel: `var mappedRect = d3dContext.Map(stagingTexture, 0, Vortice.Direct3D11.MapMode.Read, Vortice.Direct3D11.MapFlags.None);` <br>     - Đọc dữ liệu từ `mappedRect.DataPointer` vào `byte[]`. <br>     - Giải phóng tài nguyên: `d3dContext.Unmap(stagingTexture, 0);` và `outputFrame.ReleaseFrame();` <br> **4. Quản lý tài nguyên:**     - Đảm bảo `Dispose()` tất cả các đối tượng DirectX/DXGI khi không còn sử dụng để tránh rò rỉ bộ nhớ GPU. |
| **Xử lý Dữ liệu Pixel**      | `frame.Surface` là một `IDirect3DTexture2D`. Bạn cần dùng `ID3D11DeviceContext.CopyResource` và `ID3D11DeviceContext.Map` (thông qua Vortice.Windows) để sao chép dữ liệu từ GPU sang CPU. | `outputFrame.DesktopImage` là một `ID3D11Texture2D`. Tương tự, cần dùng `ID3D11DeviceContext.CopyResource` và `ID3D11DeviceContext.Map` để sao chép dữ liệu từ GPU sang CPU. |
| **Ưu Điểm Chính**            | - **Được khuyến nghị bởi Microsoft:** Tương lai, an toàn, ổn định. - **Chụp được nhiều loại nội dung hơn:** UAC, UWP apps, cửa sổ được tăng tốc phần cứng, overlays (trừ DRM). - **API thân thiện hơn:** Có vẻ trực quan hơn so với DirectX cấp thấp.  - **Hiệu suất tốt:** Tận dụng tối ưu phần cứng. | - **Hiệu suất cực cao:** Trực tiếp trên GPU. - **Chụp mọi thứ:** Bao gồm game full-screen, overlays (trừ DRM), thường là giải pháp tốt nhất cho các trường hợp "khó nhằn". - **Vortice.Windows:** Wrapper .NET hiện đại, được duy trì. |
| **Nhược Điểm/Thách Thức**    | - **Yêu cầu `DispatcherQueue`:** Phức tạp hơn một chút để thiết lập trong Console App. - **Cần Windows App SDK (hoặc `Microsoft.UI.Dispatching`):** Thêm một phụ thuộc.  - **Không chụp được nội dung DRM.** | - **Đòi hỏi kiến thức DirectX:** Khởi tạo và quản lý Device/Context/Resources phức tạp hơn. - **Quản lý tài nguyên thủ công:** Dễ rò rỉ nếu không `Dispose()` đúng cách.<br>- **Không chụp được nội dung DRM.**  - **Có thể gặp vấn đề với Multi-GPU:** Nếu máy có cả integrated và discrete GPU, cần đảm bảo chọn đúng GPU để chụp. |
| **Trạng Thái (Giữa 2025)**   | **Hoàn toàn khả dụng và khuyến nghị triển khai.** Các gói NuGet và phương pháp đã ổn định. | **✅ ĐÃ TRIỂN KHAI THÀNH CÔNG** - Đã implement sử dụng P/Invoke để tối ưu tương thích với .NET 9. Hoạt động tốt trên Windows 11. | **Hoàn toàn khả dụng và hoạt động tốt.** Đã được kiểm chứng trong nhiều ứng dụng. |

## How to implement Windows Graphics Capture API

### Hướng Dẫn Triển Khai Windows Graphics Capture API với Microsoft.Windows.CsWin32

#### Bước 1: Tạo Project Console và Cấu hình `csproj`



1. **Tạo Project Console:** Mở Visual Studio hoặc dùng CLI:

   Bash

   ```
   dotnet new console -n MyCaptureWithCsWin32
   cd MyCaptureWithCsWin32
   ```

2. **Chỉnh sửa file `MyCaptureWithCsWin32.csproj`:** Đây là bước **quan trọng nhất** để cấu hình `CsWin32` và các phụ thuộc cần thiết.

   XML

   ```
   <Project Sdk="Microsoft.NET.Sdk">
   
     <PropertyGroup>
       <OutputType>Exe</OutputType>
       <TargetFramework>net8.0</TargetFramework> <ImplicitUsings>enable</ImplicitUsings>
       <Nullable>enable</Nullable>
       <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
   
       <EnableWindowsTargeting>true</EnableWindowsTargeting> <TargetPlatformVersion>10.0.22621.0</TargetPlatformVersion> <CsWin32IncludeWindowsSdkNamespaces>
         Windows.Win32.Graphics.Direct3D11;
         Windows.Win32.Graphics.Dxgi;
         Windows.Win32.UI.WindowsAndMessaging;
         Windows.Win32.System.WinRT;
         Windows.Win32.System.WinRT.Graphics.Capture;
         Windows.Win32.Graphics.Gdi; </CsWin32IncludeWindowsSdkNamespaces>
       <CsWin32AllowMarshaling>true</CsWin32AllowMarshaling>
       <CsWin32ForceRuntimeMarshalling>true</CsWin32ForceRuntimeMarshalling>
     </PropertyGroup>
   
     <ItemGroup>
       <PackageReference Include="Microsoft.Windows.CsWin32" Version="0.3.49-beta" /> <PackageReference Include="Microsoft.UI.Dispatching" Version="1.0.0" />
   
       <PackageReference Include="Vortice.Windows" Version="2.1.20" />
   
       <PackageReference Include="SixLabors.ImageSharp" Version="3.1.4" />
   
       <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
       <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
     </ItemGroup>
   
   </Project>
   ```

3. **Build Project lần đầu:** Sau khi chỉnh sửa `.csproj`, bạn cần build project một lần.

   Bash

   ```
   dotnet build
   ```

   Thao tác này sẽ kích hoạt `CsWin32` để sinh ra các file mã C# (`.g.cs`) chứa các định nghĩa P/Invoke và WinRT Projection. Các file này sẽ nằm trong thư mục `obj\Generated`.



#### Bước 2: Thiết lập `STAThread` và `DispatcherQueue`



Đây là bước **bắt buộc** vì các API WinRT (như Windows Graphics Capture) yêu cầu một luồng STA và một `DispatcherQueue` để xử lý các sự kiện và giao tiếp với hệ thống hiển thị.

- **Hành động:**

  - Trong file `Program.cs` của bạn, thêm thuộc tính `[STAThread]` vào phương thức `Main`.
  - Trong phương thức `Main`, kiểm tra xem `DispatcherQueue` đã có trên luồng hiện tại chưa. Nếu chưa, hãy tạo một `DispatcherQueueController` và thiết lập `SynchronizationContext`.

  C#

  ```
  // Trong Program.cs
  using Microsoft.UI.Dispatching; // Import namespace này
  
  class Program
  {
      private static DispatcherQueueController? _dispatcherQueueController;
      // ... các trường khác ...
  
      [STAThread] // Đảm bảo luồng chính là STA
      static async Task Main(string[] args)
      {
          // ... (Khởi tạo Logger) ...
  
          // Khởi tạo DispatcherQueue
          if (DispatcherQueue.GetForCurrentThread() == null)
          {
              _dispatcherQueueController = DispatcherQueueController.CreateOnCurrentThread();
              // Thiết lập SynchronizationContext cho luồng hiện tại để các async/await hoạt động đúng
              var context = new DispatcherQueueSynchronizationContext(_dispatcherQueueController.DispatcherQueue);
              SynchronizationContext.SetCurrent(context);
          }
          // ... (Tiếp tục logic ứng dụng) ...
      }
  }
  ```



#### Bước 3: Triển khai Logic Chụp Ảnh với `Windows.Win32.PInvoke`



- Bây giờ, bạn có thể tạo lớp `WindowsGraphicsCaptureProvider` của mình. Thay vì sử dụng các alias như `Win32Api.User32` (mà tôi đã nhầm lẫn trong ví dụ trước), bạn sẽ sử dụng namespace `Windows.Win32.PInvoke` được sinh ra bởi `CsWin32`.

- **Hành động:**

  - Tạo file `WindowsGraphicsCaptureProvider.cs`.
  - Sử dụng namespace `Windows.Graphics.Capture` và `Windows.Graphics.DirectX.Direct3D11` (là các lớp WinRT).
  - Đối với các hàm Win32/WinRT mà bạn đã khai báo trong `CsWin32IncludeWindowsSdkNamespaces` (ví dụ: `GetWindowIdFromWindow`, `D3D11CreateDevice`, `DisplayId.PrimaryDisplayId`), bạn sẽ truy cập chúng qua `PInvoke.TênHàm` hoặc `PInvoke.TênStruct/Enum`.

  C#

  ```
  // Trong WindowsGraphicsCaptureProvider.cs
  // ...
  using Vortice.Direct3D11;
  using Vortice.DXGI;
  using Windows.Graphics.Capture;
  using Windows.Graphics.DirectX.Direct3D11; // Lớp WinRT
  using WinRT; // Cần cho extension method .As<T>() và .QueryInterface<T>()
  
  // Dùng alias cho namespace được sinh ra bởi CsWin32
  using PInvoke = Windows.Win32.PInvoke;
  using Win32 = Windows.Win32;
  
  namespace WinServicesRAG.Core.Screenshot;
  
  public class WindowsGraphicsCaptureProvider : IDisposable // Thêm IScreenshotProvider nếu bạn đã định nghĩa nó
  {
      // ... (các trường và constructor như đã có) ...
  
      private void InitializeDirectXComponents()
      {
          // ... (Dispose các tài nguyên cũ) ...
  
          try
          {
              Vortice.Direct3D11.ID3D11Device tempVorticeDevice;
              Vortice.Direct3D11.ID3D11DeviceContext tempVorticeContext;
  
              // Sử dụng PInvoke.D3D11CreateDevice() từ CsWin32
              PInvoke.D3D11CreateDevice(
                  null,
                  DriverType.Hardware,
                  DeviceCreationFlags.BgraSupport, // Quan trọng cho Graphics Capture
                  null, // Feature levels
                  out tempVorticeDevice,
                  out tempVorticeContext
              );
  
              _vorticeD3d11Device = tempVorticeDevice;
              _d3d11Context = tempVorticeContext;
  
              // Chuyển đổi Vortice D3D11 Device sang WinRT IDirect3DDevice
              // PInvoke.CreateDirect3D11DeviceFromDXGIDevice là hàm WinRT được CsWin32 sinh ra
              _winrtD3dDevice = _vorticeD3d11Device.QueryInterface<IDirect3DDevice>(); // Cách trực tiếp hơn với Vortice 2.x và WinRT.dll
              // Hoặc nếu QueryInterface không đủ, có thể cần một hàm tạo WinRT Device từ DXGI Device:
              // var dxgiDevice = _vorticeD3d11Device.QueryInterface<IDXGIDevice>();
              // _winrtD3dDevice = PInvoke.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice); // Nếu cần hàm này
  
              _logger.LogInformation("DirectX components initialized for Graphics Capture.");
          }
          // ... (xử lý lỗi) ...
      }
  
      private void StartCaptureSession(bool captureMonitor)
      {
          // ... (StopCapture, InitializeDirectXComponents) ...
  
          if (captureMonitor)
          {
              // Sử dụng PInvoke.DisplayId.PrimaryDisplayId
              _captureItem = GraphicsCaptureItem.TryCreateFromDisplayId(PInvoke.DisplayId.PrimaryDisplayId);
              _logger.LogInformation("Selected primary monitor for capture.");
          }
          else
          {
              // Lấy HWND và chuyển đổi sang WindowId bằng PInvoke
              var hwnd = PInvoke.GetForegroundWindow();
              if (hwnd == IntPtr.Zero) { /* ... */ return; }
              var windowId = PInvoke.GetWindowIdFromWindow(hwnd);
              _captureItem = GraphicsCaptureItem.TryCreateFromWindowId(windowId);
              // ...
          }
  
          // ... (các bước khác như tạo frame pool, session, xử lý frame) ...
      }
  
      private unsafe byte[]? ConvertMappedResourceToPng(MappedSubresource mappedResource, int width, int height, int stride)
      {
          // ... (Logic dùng SixLabors.ImageSharp như cũ) ...
      }
      // ... (Phương thức OnFrameArrivedInternal, Dispose) ...
  }
  ```