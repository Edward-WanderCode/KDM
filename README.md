# KDM Download Manager - Lightweight IDM Alternative

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-brightgreen.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

*(Scroll down for Vietnamese | Cuộn xuống cho Tiếng Việt)*

**KDM (KDM Download Manager)** is an open-source, multi-threaded download manager focusing on simplicity and efficiency. It serves as a modern, lightweight alternative to traditional download managers, removing complex download queues and proxy configurations in favor of a straightforward, high-speed downloading experience with browser integration.

---

## 🇬🇧 English

### ✨ Features
- **Pure Downloading:** A focused experience without queues or complex setups. Links are caught and downloaded immediately.
- **Multi-Threaded Engine:** Supports up to 32 parallel segments to maximize your available bandwidth.
- **File Management:** Fully supports multiple selection via <kbd>Shift</kbd> / <kbd>Ctrl</kbd> and includes a modern context menu for quickly opening files, folders, copying URLs, or pausing/resuming downloads.
- **Browser Integration:** Includes an extension that seamlessly catches download links from Google Drive, OneDrive, GitHub, and standard direct links on Chromium-based browsers (Chrome, Edge, Brave).
- **Clean UI:** Features a minimalist Dark Theme powered by Windows standard vector icons, without any ads or distractions.
- **Background Mode:** Can minimize to the System Tray to remain active without cluttering your taskbar.

### 📥 Installation & Setup

**1. Install the KDM Desktop Application:**
* **General Users:** Go to the [Releases page](../../releases) and download the latest `KDM_Setup_v1.0.0.exe`. Run the installer to set up the application with desktop and start menu shortcuts.
* **Developers:** Clone the repository and compile using the .NET 8 CLI:
  ```bash
  dotnet build
  dotnet run
  ```

**2. Install the Browser Extension:**
1. Open your browser's extensions page (`edge://extensions` or `chrome://extensions`).
2. Enable **Developer Mode**.
3. Click **Load unpacked** and select the `/Extension` folder from the extracted source code.
4. The extension will now automatically forward supported download links to the KDM desktop application.

---

## 🇻🇳 Tiếng Việt

**KDM (KDM Download Manager)** là một trình quản lý tải xuống mã nguồn mở, đơn giản và hiệu quả. KDM được thiết kế như một giải pháp thay thế tinh gọn (lite) cho các phần mềm truyền thống như IDM. Ứng dụng đã loại bỏ các tính năng phức tạp như quản lý hàng đợi (Queue) hay cấu hình Proxy để tập trung mang lại trải nghiệm tải file tốc độ cao và liền mạch.

### ✨ Các Tính Năng Nổi Bật
- **Tập Trung Hiệu Suất:** Không có thiết lập cầu kỳ. Nhấn tải trên trình duyệt, ứng dụng sẽ tiếp nhận link tải ngay lập tức.
- **Tải Đa Luồng:** Hỗ trợ chia nhỏ tệp tin lên đến 32 luồng tải song song để tối ưu hóa việc sử dụng băng thông internet.
- **Quản Lý Tiện Lợi:** Giao diện quản lý hỗ trợ tính năng chọn nhiều file (Multi-Selection) thông qua phím <kbd>Shift</kbd> / <kbd>Ctrl</kbd>. Tích hợp menu chuột phải tiêu chuẩn để truy cập nhanh file, mở thư mục, sao chép URL hoặc tạm dừng/tiếp tục tiến trình.
- **Bắt Link Trình Duyệt:** Đi kèm tiện ích (Extension) tự động nhận diện và gửi liên kết tải từ Google Drive, OneDrive, GitHub và các trang web khác sang phần mềm trên máy tính.
- **Giao Diện Tối Giản:** Sử dụng giao diện Dark / Chế độ Tối gọn gàng, hoạt động mượt mà và hoàn toàn không có quảng cáo.
- **Đa Ngôn Ngữ:** Hỗ trợ đổi ngôn ngữ giao diện (Tếng Anh / Tiếng Việt) nhanh chóng mà không cần khởi động lại.

### 📥 Tải Xuống và Cài Đặt

**1. Cài Đặt Trình Quản Lý KDM:**
* **Người Dùng Phổ Thông:** Vui lòng truy cập trang [Releases](../../releases) trên Github, tải xuống tệp `KDM_Setup_v1.0.0.exe` mới nhất và tiến hành cài đặt. Quy trình cài đặt sẽ tự động tạo shortcut và thiết lập ứng dụng.
* **Nhà Phát Triển (Developer):** Tải mã nguồn và sử dụng .NET 8 để biên dịch:
  ```bash
  dotnet build
  dotnet run
  ```

**2. Cài Đặt Extension Cho Trình Duyệt:**
1. Mở phần quản lý Tiện ích trên trình duyệt (`edge://extensions` hoặc `chrome://extensions`).
2. Bật tuỳ chọn **Developer Mode** (Chế độ dành cho nhà phát triển).
3. Nhấp vào nút **Load unpacked** (Tải tiện ích đã giải nén) và trỏ đến vị trí thư mục `/Extension` nằm trong mã nguồn.
4. Từ bây giờ, Extension sẽ tự động hỗ trợ bắt các lệnh tải file kích thước lớn từ trình duyệt qua KDM.

---

### 🤝 Đóng Góp (Contributions)
KDM là phần mềm mã nguồn mở. Mọi hình thức đóng góp mã nguồn (Pull Requests) hay báo lỗi, đề xuất tính năng (Issues) đều được khuyến khích và đón nhận. Nếu bạn thấy phần mềm này hữu ích, hãy để lại một ⭐️ cho repository nhé!
