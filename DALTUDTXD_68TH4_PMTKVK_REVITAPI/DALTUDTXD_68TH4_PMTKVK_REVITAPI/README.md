# Thiết kế ván khuôn sàn - Revit Add-in

## Giới thiệu
Đây là Add-in dành cho Revit 2025 hỗ trợ tự động thiết kế và tính toán ván khuôn sàn.
Add-in cung cấp giao diện trực quan với WPF và tương tác 2 chiều với mô hình Revit.

## Yêu cầu hệ thống
- Autodesk Revit 2025
- .NET 8.0 (Revit 2025 yêu cầu .NET 8)

## Cài đặt và Cấu hình (Build Stability)
Do Add-in sử dụng tham chiếu đường dẫn tuyệt đối cho file `.addin` trong quá trình phát triển (để thuận tiện debug mà không cần copy DLL mỗi lần build), bạn cần kiểm tra và cấu hình lại đường dẫn nếu di chuyển thư mục dự án:

1. Mở file `DALTUDTXD_68TH4_PMTKVK_REVITAPI.addin`.
2. Kiểm tra thẻ `<Assembly>`. Nó phải trỏ đến đường dẫn tuyệt đối của file `DALTUDTXD_68TH4_PMTKVK_REVITAPI.dll` trên máy tính của bạn (thường nằm trong thư mục `bin\Debug\net8.0-windows\`).
3. Mở Visual Studio và build project. File `.addin` sẽ tự động được copy vào `%AppData%\Autodesk\Revit\Addins\2025\` (thông qua lệnh Post-build event).
4. Khởi động Revit 2025. Add-in sẽ xuất hiện ở tab **Add-Ins** (hoặc tab tuỳ chỉnh của dự án).

## Lưu ý về Kiến trúc (Refactored)
Dự án đã được tái cấu trúc:
- `SharedDataViewModel.cs` đóng vai trò là cửa hàng trạng thái (State Store) duy nhất chia sẻ giữa các tab.
- Phương pháp kiểm tra hình học và tạo Preview 2D đã được tối ưu và gộp chung để tránh trùng lặp code.
- Phương thức chọn sàn (PickObjects, PickElementsByRectangle) được quản lý tập trung qua `HandleSelection` trong `AppCommand.cs`.
