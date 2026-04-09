# KDM Download Manager 🚀

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-brightgreen.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**KDM (KDM Download Catcher)** là một trình quản lý tải xuống (Download Manager) mã nguồn mở, hỗ trợ chia nhỏ multi-thread giúp tăng tốc độ download. Kết hợp cùng Extension bắt link thông minh trên mọi trình duyệt, KDM mang đến trải nghiệm tương tự Internet Download Manager (IDM) với một giao diện tối giản và hiện đại.

![Architecture](https://img.shields.io/badge/Architecture-Clean-orange.svg) 
![Theme](https://img.shields.io/badge/Theme-Dark_Mode-black.svg)

---

## ✨ Tính năng nổi bật

### 💻 Ứng dụng Desktop (WPF)
- **Tăng Tốc Tải Xuống:** Tải file với cơ chế đa luồng (multi-thread) lên đến 32 luồng, tối ưu hóa băng thông của bạn.
- **Tạm dừng và Tiếp tục (Pause/Resume):** Tạm dừng nhanh chóng và khôi phục tiến trình mà không làm hỏng tập tin.
- **Giao diện Hiện đại:** Dark theme (Neo-Brutalism) chuyên nghiệp, tối giản, trực quan.
- **Tiện lợi trên Background:** Hỗ trợ thu gọn xuống mục System Tray (khay hệ thống) để chạy nền không phiền nhiễu.
- **Hỗ trợ Đa ngôn ngữ:** Tích hợp song ngữ Tiếng Việt và Tiếng Anh, dễ dàng chuyển đổi ngay trong cài đặt lúc chạy (runtime).

### 🌐 Trình Duyệt Tích Hợp (Browser Extension)
- **Hoạt động với Mọi Trình Duyệt:** Tương thích Microsoft Edge, Google Chrome, Brave, v.v. (hỗ trợ Manifest V3).
- **Tự Động Bắt Link Thông Minh:**
  - Nhận diện các liên kết download trực tiếp.
  - Hỗ trợ tốt nhất cho **Google Drive**, **OneDrive**, **Dropbox**, **GitHub Releases**, **MediaFire** và các nền tảng đám mây khác.
  - Không bắt nhầm file web (HTML, CSS) - chỉ bắt những tệp quan trọng!
- **Context Menu:** Cho phép chọn `Chuột phải → Tải với KDM` tiện lợi tại bắt kì liên kết nào.
- **Thông Báo Tích Hợp:** Tự động báo Toast ngay trên trang và chuyển dữ liệu mượt mà về ứng dụng KDM.

---

## 🛠️ Cài đặt và Hướng dẫn sử dụng

### 1. Kích hoạt KDM Desktop App
1. Tải toàn bộ source code về máy.
2. Cài đặt **.NET 8.0 SDK**.
3. Mở terminal tại thư mục `KDM` và chạy:
   ```bash
   dotnet build
   dotnet run
   ```
4. Ứng dụng sẽ bắt đầu HTTP Listener nội bộ tại `http://127.0.0.1:52888/` để chờ link gửi về.

### 2. Cài đặt Catcher Extension vào Trình duyệt (Chrome/Edge)
1. Mở Trình duyệt của bạn và truy cập trang Quản lý tiện ích:
   - **Edge:** `edge://extensions`
   - **Chrome:** `chrome://extensions`
2. Kích hoạt chế độ **Developer Mode** (Chế độ dành cho nhà phát triển).
3. Nhấp vào nút **Load unpacked** (Tải tiện ích đã giải nén).
4. Điều hướng tới mục `/Extension` và chọn thư mục này.
5. Kiểm tra biểu tượng chữ *KDM* vừa hiện trên góc trình duyệt.

💡 ***Từ giờ trở đi, khi bạn truy cập Google Drive hay bấm vào bất kì liên kết tải nào, KDM Extension sẽ tự bắt gọn và gửi về ứng dụng Desktop!***

---

## 💻 Kiến trúc Module
KDM theo đuổi thiết kế Modular/Clean Code với các phần chính:
- `DownloadEngine`: Quản lý tác vụ đa luồng, xử lý Range requests.
- `ExtensionServer`: Module backend lắng nghe mọi ping HTTP từ trình duyệt.
- `FileManager`: Lắp ghép và hợp nhất Multi-segment Download về một file hoàn chỉnh khi tải xong.
- `TrayIconManager`: Quản lý vòng đời và Notification trên UI Window.
- `Content/Background.js` (Web): Kích hoạt quy trình theo dõi DOM (VD: quét sự kiện nút "Tải xuống" của Google Drive) để lấy file ID chính xác.

---

### Đóng Tóp và Phát Triển (Contributions)
Mọi PRs (Pull Requests) đều được chào đón! Hãy mở issue nếu gặp lỗi, cần thêm tính năng mới hoặc hỗ trợ service mới trong Extension Catcher.
