/**
 * KDM Download Catcher - Content Script
 * 
 * Chạy trên mọi trang web:
 * 1. Bắt click link download trực tiếp
 * 2. Inject nút "Tải với KDM" trên Google Drive, Dropbox, etc.
 * 3. Hiển thị toast notification
 */

// === DETECT TRANG WEB HIỆN TẠI ===
const currentHost = window.location.hostname.toLowerCase();

// Extensions cần bắt khi click link
const DOWNLOAD_EXTENSIONS = [
  '.zip', '.rar', '.7z', '.tar', '.gz', '.bz2', '.xz', '.iso',
  '.exe', '.msi', '.dmg', '.deb', '.rpm',
  '.mp4', '.mkv', '.avi', '.mov', '.wmv', '.flv', '.webm',
  '.mp3', '.flac', '.wav', '.aac', '.ogg',
  '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx',
  '.apk', '.ipa', '.torrent', '.bin', '.img'
];

// === GOOGLE DRIVE HANDLER ===
if (currentHost.includes('drive.google.com')) {
  setupGoogleDriveHandler();
}

/**
 * Google Drive: Bắt nút "Tải xuống" và link chia sẻ
 */
function setupGoogleDriveHandler() {
  console.log('[KDM] Google Drive detected, setting up handler');

  // Theo dõi DOM thay đổi (Google Drive dùng SPA)
  const observer = new MutationObserver(() => {
    interceptGDriveDownloads();
  });

  observer.observe(document.body, {
    childList: true,
    subtree: true
  });

  // Chạy lần đầu
  setTimeout(interceptGDriveDownloads, 1000);
}

/**
 * Intercept các nút download trên Google Drive
 */
function interceptGDriveDownloads() {
  // Bắt menu items có text "Tải xuống" hoặc "Download"
  const menuItems = document.querySelectorAll('[role="menuitem"], [data-tooltip="Tải xuống"], [data-tooltip="Download"], [aria-label="Tải xuống"], [aria-label="Download"]');
  
  menuItems.forEach(item => {
    if (item.dataset.kdmBound) return; // Đã bind rồi
    
    const text = item.textContent || item.getAttribute('aria-label') || '';
    if (text.includes('Tải xuống') || text.includes('Download') || text.includes('download')) {
      item.dataset.kdmBound = 'true';
      
      item.addEventListener('click', (e) => {
        // Lấy file ID từ URL hoặc DOM
        const fileId = extractGDriveFileId();
        if (fileId) {
          e.preventDefault();
          e.stopPropagation();
          
          const downloadUrl = `https://drive.google.com/uc?export=download&id=${fileId}&confirm=t`;
          const fileName = extractGDriveFileName() || 'google_drive_file';
          
          chrome.runtime.sendMessage({
            action: 'sendToKDM',
            url: downloadUrl,
            filename: fileName
          }, (response) => {
            if (response && response.success) {
              showToast(`✓ Đã gửi đến KDM: ${fileName}`);
            } else {
              showToast('⚠ KDM chưa chạy. Hãy mở KDM.');
            }
          });
        }
      }, true);
    }
  });
}

/**
 * Lấy file ID từ URL Google Drive
 */
function extractGDriveFileId() {
  const url = window.location.href;
  
  // URL pattern: /file/d/FILE_ID/view
  let match = url.match(/\/file\/d\/([a-zA-Z0-9_-]+)/);
  if (match) return match[1];
  
  // URL pattern: ?id=FILE_ID
  match = url.match(/[?&]id=([a-zA-Z0-9_-]+)/);
  if (match) return match[1];
  
  // Từ selected items trong list view
  const selectedItems = document.querySelectorAll('[data-id]');
  for (const item of selectedItems) {
    if (item.classList.contains('a-s-tb-sc') || item.getAttribute('aria-selected') === 'true') {
      return item.dataset.id;
    }
  }
  
  return null;
}

/**
 * Lấy tên file từ Google Drive page
 */
function extractGDriveFileName() {
  // Từ title bar
  const titleEl = document.querySelector('[data-tooltip="Rename"], .KfFlO, [data-value]');
  if (titleEl) {
    return titleEl.textContent || titleEl.getAttribute('data-value') || '';
  }
  
  // Từ document title
  const title = document.title.replace(' - Google Drive', '').trim();
  if (title && title !== 'Google Drive') return title;
  
  // Từ selected item
  const selected = document.querySelector('[aria-selected="true"] [data-tooltip]');
  if (selected) return selected.getAttribute('data-tooltip') || '';
  
  return '';
}

// === BẮT CLICK LINK DOWNLOAD (TRANG WEB THƯỜNG) ===
document.addEventListener('click', (e) => {
  const link = e.target.closest('a');
  if (!link || !link.href) return;

  if (!isDownloadLink(link.href)) return;
  if (e.ctrlKey || e.altKey || e.metaKey) return;

  e.preventDefault();
  e.stopPropagation();

  const url = link.href;
  const filename = getFilenameFromUrl(url) || link.textContent.trim();

  chrome.runtime.sendMessage({
    action: 'sendToKDM',
    url: url,
    filename: filename
  }, (response) => {
    if (response && response.success) {
      showToast(`✓ Đã gửi đến KDM: ${filename}`);
    } else {
      showToast('⚠ KDM chưa chạy. Hãy mở KDM trước.');
    }
  });
}, true);

/**
 * Kiểm tra link có phải file download không
 */
function isDownloadLink(href) {
  if (!href) return false;
  const urlLower = href.toLowerCase().split('?')[0].split('#')[0];
  return DOWNLOAD_EXTENSIONS.some(ext => urlLower.endsWith(ext));
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
 * Toast notification trên trang web
 */
function showToast(message) {
  const existing = document.getElementById('kdm-toast');
  if (existing) existing.remove();

  const toast = document.createElement('div');
  toast.id = 'kdm-toast';
  toast.textContent = message;
  Object.assign(toast.style, {
    position: 'fixed',
    bottom: '24px',
    right: '24px',
    background: '#161B22',
    color: '#C9D1D9',
    padding: '14px 22px',
    borderRadius: '8px',
    fontSize: '14px',
    fontFamily: 'Segoe UI, Arial, sans-serif',
    zIndex: '2147483647',
    boxShadow: '0 4px 20px rgba(0,0,0,0.6)',
    border: '1px solid #30363D',
    transition: 'opacity 0.3s ease, transform 0.3s ease',
    opacity: '0',
    transform: 'translateY(10px)',
    maxWidth: '400px',
    wordBreak: 'break-all'
  });

  document.body.appendChild(toast);

  requestAnimationFrame(() => {
    toast.style.opacity = '1';
    toast.style.transform = 'translateY(0)';
  });

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(10px)';
    setTimeout(() => toast.remove(), 300);
  }, 3000);
}
