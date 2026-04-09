/**
 * KDM Download Catcher - Background Service Worker
 * 
 * Chức năng:
 * 1. Bắt TẤT CẢ download từ browser và gửi về KDM
 * 2. Context menu "Tải với KDM" khi chuột phải vào link
 * 3. Giao tiếp với KDM app qua HTTP 127.0.0.1:52888
 * 4. Hỗ trợ Google Drive, Dropbox, OneDrive, MediaFire, MEGA, etc.
 */

// === CẤU HÌNH ===
const KDM_API = 'http://127.0.0.1:52888';

// Extensions mà KHÔNG nên bắt (để browser xử lý)
const IGNORE_EXTENSIONS = [
  '.html', '.htm', '.php', '.asp', '.aspx', '.jsp',
  '.css', '.js', '.json', '.xml',
  '.jpg', '.jpeg', '.png', '.gif', '.webp', '.ico', '.bmp', // Ảnh nhỏ thường xem trên web
];

// URL patterns phải bắt (cloud services) - luôn bắt dù không có extension
const CLOUD_DOWNLOAD_PATTERNS = [
  // Google Drive
  /drive\.google\.com\/uc\?.*export=download/i,
  /drive\.usercontent\.google\.com\/download/i,
  /docs\.google\.com\/.*\/export/i,
  /drive\.google\.com\/file\/d\/.+\/view/i,
  
  // Dropbox
  /dropbox\.com\/.*\?.*dl=1/i,
  /dl\.dropboxusercontent\.com/i,
  
  // OneDrive / SharePoint
  /onedrive\.live\.com\/download/i,
  /1drv\.ms\//i,
  /sharepoint\.com\/.*\/download/i,
  
  // MediaFire
  /mediafire\.com\/file\//i,
  /download\d*\.mediafire\.com/i,
  
  // MEGA
  /mega\.nz\/(file|folder)\//i,
  
  // GitHub Releases
  /github\.com\/.*\/releases\/download\//i,
  /objects\.githubusercontent\.com/i,
  
  // SourceForge
  /sourceforge\.net\/projects\/.*\/files\//i,
  /downloads\.sourceforge\.net/i,
  
  // FPT / Fshare / Vietnamese services
  /fshare\.vn\/file\//i,
  
  // Generic download patterns
  /\/download\//i,
  /\/downloads\//i,
  /[?&]download=/i,
  /[?&]action=download/i,
  /[?&]export=download/i,
];

// Kích thước tối thiểu để bắt TỰ ĐỘNG (bytes) - khi không match patterns khác
const MIN_FILE_SIZE = 512 * 1024; // 512KB

// === TRẠNG THÁI ===
let isEnabled = true;
let kdmConnected = false;

// Load settings
chrome.storage.local.get(['isEnabled'], (result) => {
  isEnabled = result.isEnabled !== undefined ? result.isEnabled : true;
});

// === CONTEXT MENU ===
chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: 'kdm-download-link',
    title: '⬇ Tải với KDM',
    contexts: ['link']
  });

  chrome.contextMenus.create({
    id: 'kdm-download-page',
    title: '⬇ Gửi URL trang này đến KDM',
    contexts: ['page']
  });
});

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId === 'kdm-download-link') {
    sendToKDM(info.linkUrl);
  } else if (info.menuItemId === 'kdm-download-page') {
    sendToKDM(info.pageUrl);
  }
});

// === BẮT DOWNLOAD TỰ ĐỘNG ===
chrome.downloads.onCreated.addListener((downloadItem) => {
  if (!isEnabled) return;

  const url = downloadItem.finalUrl || downloadItem.url;
  const filename = downloadItem.filename || '';
  const mime = downloadItem.mime || '';
  const fileSize = downloadItem.fileSize || downloadItem.totalBytes || 0;

  console.log('[KDM] Download detected:', { url, filename, mime, fileSize });

  // Kiểm tra có phải file cần bắt không
  if (shouldCatchDownload(url, filename, fileSize, mime)) {
    console.log('[KDM] Catching download:', url);
    
    // Hủy download của browser
    chrome.downloads.cancel(downloadItem.id, () => {
      chrome.downloads.erase({ id: downloadItem.id });
      
      // Gửi đến KDM (dùng URL gốc nếu finalUrl là blob/redirect)
      const sendUrl = url.startsWith('blob:') ? downloadItem.url : url;
      sendToKDM(sendUrl, filename);
    });
  }
});

/**
 * Kiểm tra URL/file có nên được bắt bởi KDM không.
 * Logic: Bắt TẤT CẢ trừ các file web page / ảnh nhỏ.
 */
function shouldCatchDownload(url, filename, fileSize, mime) {
  // === LUÔN BỎ QUA ===
  if (!url) return false;
  if (url.startsWith('blob:') || url.startsWith('data:')) return false;
  if (url.includes('127.0.0.1') || url.includes('localhost:52888')) return false;

  // Bỏ qua extension updates
  if (url.includes('chrome.google.com') || url.includes('edge.microsoft.com')) return false;
  if (url.includes('update.googleapis.com')) return false;

  // === LUÔN BẮT: Cloud service downloads ===
  for (const pattern of CLOUD_DOWNLOAD_PATTERNS) {
    if (pattern.test(url)) {
      console.log('[KDM] Matched cloud pattern:', pattern);
      return true;
    }
  }

  // === LUÔN BẮT: MIME type là download ===
  if (mime) {
    const mimeLower = mime.toLowerCase();
    // application/octet-stream = binary download
    if (mimeLower === 'application/octet-stream') return true;
    // Archive types
    if (mimeLower.includes('zip') || mimeLower.includes('rar') || 
        mimeLower.includes('7z') || mimeLower.includes('tar') ||
        mimeLower.includes('gzip') || mimeLower.includes('compressed')) return true;
    // Executable
    if (mimeLower.includes('executable') || mimeLower.includes('x-msdownload') ||
        mimeLower.includes('x-msi')) return true;
    // Video
    if (mimeLower.startsWith('video/')) return true;
    // Audio (trừ streaming)
    if (mimeLower.startsWith('audio/') && !mimeLower.includes('webm')) return true;
    // Documents
    if (mimeLower.includes('pdf') || mimeLower.includes('msword') ||
        mimeLower.includes('spreadsheet') || mimeLower.includes('presentation') ||
        mimeLower.includes('officedocument')) return true;
    // ISO/Disk
    if (mimeLower.includes('iso') || mimeLower.includes('disk')) return true;
    // Torrent
    if (mimeLower.includes('torrent')) return true;
    // APK
    if (mimeLower.includes('android') || mimeLower.includes('apk')) return true;
  }

  // === KIỂM TRA TÊN FILE ===
  const checkName = (filename || getFilenameFromUrl(url)).toLowerCase();
  
  // Bỏ qua nếu là file web page / tài nguyên web nhỏ
  for (const ext of IGNORE_EXTENSIONS) {
    if (checkName.endsWith(ext)) return false;
  }

  // Có extension đáng tải không?
  if (checkName.includes('.') && checkName.length > 2) {
    const ext = '.' + checkName.split('.').pop();
    // Nếu có extension và KHÔNG nằm trong ignore list → bắt
    if (ext.length > 1 && ext.length < 10 && !IGNORE_EXTENSIONS.includes(ext)) {
      return true;
    }
  }

  // === KIỂM TRA KÍCH THƯỚC ===
  if (fileSize && fileSize >= MIN_FILE_SIZE) {
    return true;
  }

  // === URL patterns khác ===
  const urlLower = url.toLowerCase();
  if (urlLower.includes('attachment') || urlLower.includes('force_download') ||
      urlLower.includes('disposition=attachment')) {
    return true;
  }

  // Mặc định: không bắt (ưu tiên an toàn)
  return false;
}

/**
 * Lấy tên file từ URL
 */
function getFilenameFromUrl(url) {
  try {
    const pathname = new URL(url).pathname;
    const parts = pathname.split('/');
    return decodeURIComponent(parts[parts.length - 1]) || '';
  } catch {
    return '';
  }
}

/**
 * Gửi URL đến KDM app qua HTTP
 */
async function sendToKDM(url, filename = '') {
  try {
    const response = await fetch(`${KDM_API}/api/download`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url, filename })
    });

    if (response.ok) {
      kdmConnected = true;
      const displayName = filename || getFilenameFromUrl(url) || url.substring(0, 60);
      showNotification('Đã gửi đến KDM', displayName);

      chrome.action.setBadgeText({ text: '✓' });
      chrome.action.setBadgeBackgroundColor({ color: '#238636' });
      setTimeout(() => chrome.action.setBadgeText({ text: '' }), 2000);
    } else {
      throw new Error(`HTTP ${response.status}`);
    }
  } catch (error) {
    kdmConnected = false;
    console.error('[KDM] Error:', error);

    showNotification('Không thể kết nối KDM', 
      'Hãy đảm bảo KDM đang chạy.');

    chrome.action.setBadgeText({ text: '!' });
    chrome.action.setBadgeBackgroundColor({ color: '#F85149' });
    setTimeout(() => chrome.action.setBadgeText({ text: '' }), 3000);
  }
}

/**
 * Hiển thị notification
 */
function showNotification(title, message) {
  chrome.notifications.create({
    type: 'basic',
    iconUrl: 'icons/icon128.png',
    title: title,
    message: message,
    silent: true
  });
}

/**
 * Kiểm tra kết nối KDM
 */
async function checkKDMConnection() {
  try {
    const response = await fetch(`${KDM_API}/api/status`, { 
      method: 'GET',
      signal: AbortSignal.timeout(2000)
    });
    kdmConnected = response.ok;
  } catch {
    kdmConnected = false;
  }
  return kdmConnected;
}

// === MESSAGE HANDLERS ===
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  switch (message.action) {
    case 'sendToKDM':
      sendToKDM(message.url, message.filename).then(() => {
        sendResponse({ success: true });
      }).catch(err => {
        sendResponse({ success: false, error: err.message });
      });
      return true;

    case 'checkStatus':
      checkKDMConnection().then(connected => {
        sendResponse({ connected, enabled: isEnabled });
      });
      return true;

    case 'toggleEnabled':
      isEnabled = message.enabled;
      chrome.storage.local.set({ isEnabled });
      sendResponse({ enabled: isEnabled });
      break;

    case 'getState':
      sendResponse({ connected: kdmConnected, enabled: isEnabled });
      break;
  }
});

// Kiểm tra kết nối KDM mỗi 30 giây
setInterval(checkKDMConnection, 30000);
checkKDMConnection();
