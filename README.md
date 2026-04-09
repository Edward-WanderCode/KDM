# KDM Download Manager 🚀 - The Lite Modern IDM Alternative

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-brightgreen.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

*(Scroll down for Vietnamese | Cuộn xuống cho Tiếng Việt)*

**KDM (KDM Download Catcher)** is an open-source, multi-threaded download manager designed to be **a lightweight, bloat-free modern alternative to Internet Download Manager (IDM)**. We stripped away the complexity—no complicated download queues, no proxy configurations—just pure, high-speed downloading with an intelligent browser extension.

![Architecture](https://img.shields.io/badge/Architecture-Clean-orange.svg) 
![Theme](https://img.shields.io/badge/Theme-Dark_Mode-black.svg)
![IDM Lite](https://img.shields.io/badge/Status-Lite_IDM_Replacement-red.svg)

---

## 🇬🇧 English

### ✨ The "Lite" Philosophy
- **Pure Downloading:** No queues. No proxy setups. You click a link, it catches it, and it downloads at maximum speed. Simple as that.
- **Extreme Acceleration:** Multi-threaded download engine (up to 32 parallel segments) utilizes your full bandwidth.
- **Smart Browser Integration:** No more manual copying! The KDM extension flawlessly catches Google Drive, OneDrive, Dropbox, MEGA, and standard web links from Chrome, Edge, and Brave.
- **Modern & Ad-Free:** Unlike IDM's outdated UI and constant license popups, KDM offers a gorgeous Dark Theme, no ads, and is 100% free and open-source.
- **Always Background Ready:** Minimizes to the System Tray to work silently in the background.

### 🛠️ Quick Start

**1. Run the Desktop App:**
- Ensure you have **.NET 8.0 SDK** installed.
- Compile and run via terminal:
  ```bash
  dotnet build
  dotnet run
  ```
- *KDM silently starts a local listener on `http://127.0.0.1:52888/`.*

**2. Install the Browser Catcher Extension:**
1. Open your browser's extensions page (`edge://extensions` or `chrome://extensions`).
2. Turn on **Developer Mode**.
3. Click **Load unpacked** and select the `/Extension` folder from the source.
4. Try clicking a download link everywhere (e.g. Google Drive "Download" button).

---

## 🇻🇳 Tiếng Việt

**KDM (KDM Download Catcher)** là **phiên bản thay thế dạng "Lite" (tối giản) cực kỳ hiện đại của IDM (Internet Download Manager)** dành cho mã nguồn mở. Chúng tôi loại bỏ hoàn toàn các tính năng cồng kềnh như màn hình quản lý hàng đợi (Queue) hay cấu hình Proxy phức tạp. KDM tập trung duy nhất vào một việc: **Bắt link tự động và tải file đa luồng với tốc độ bàn thờ.**

### ✨ Phiên bản "Lite" đỉnh cao nhất
- **Tối Giản, Không Thừa Thãi:** Không có Queue, không có cài đặt Proxy. Bạn bấm tải trên trình duyệt, ứng dụng sẽ bắt link và tải với tốc độ cao nhất ngay lập tức.
- **Tăng Tốc Trầm Trọng:** Cỗ máy tải xuống đa luồng (chia nhỏ tới 32 luồng) ép tối đa băng thông đường truyền internet của bạn.
- **Bắt Link Cực Chuẩn:** Catcher Extension tự động bắt chính xác các liên kết tải từ Google Drive, OneDrive, GitHub Releases, Fshare, v.v thay vì tải nhầm file HTML trang web.
- **Bóng bẩy & Miễn phí:** Bỏ đi giao diện nghèo nàn từ thập niên 2000 của IDM, KDM mang đến giao diện Neo-Brutalism Dark theme tuyệt đẹp, ẩn gọn dưới System Tray và hoàn toàn không có mảng quảng cáo, không thu phí.
- **Hỗ trợ Đa Ngôn Ngữ:** Dễ dàng đổi giao diện ứng dụng từ tiếng Anh sang tiếng Việt chỉ với một cú click ngay trên ứng dụng đang chạy.

### 🛠️ Cài đặt và Hướng dẫn sử dụng

**1. Chạy KDM Desktop App:**
- Cài đặt **.NET 8.0 SDK**.
- Mở command line tại thư mục `KDM` và chạy:
  ```bash
  dotnet build
  dotnet run
  ```
- *App sẽ bật ngầm một listener API tại `http://127.0.0.1:52888/` để hứng link từ extension gửi sang.*

**2. Cài Extension Bắt Link vào Trình Duyệt:**
1. Mở trang quản lý Tiện ích (`edge://extensions` hoặc `chrome://extensions`).
2. Bật chế độ **Developer Mode** (Chế độ dành cho nhà phát triển).
3. Nhấp vào **Load unpacked** (Tải tiện ích đã giải nén) và chọn thư mục `/Extension` trong mã nguồn.
4. Xong! Bất kì click tải file nặng nào trên Google drive hay mọi nơi sẽ được chuyển ngay sang KDM và tải không độ trễ.

---

### 🤝 Contributions
All pull requests are welcome! If you love this minimalist IDM alternative, feel free to give this repository a ⭐️!

Mọi PR đóng góp mã nguồn đều luôn được chào đón. Khuyến khích mở issue để yêu cầu thêm tính năng mới!
