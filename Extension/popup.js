/**
 * KDM Download Catcher - Popup Script
 * Điều khiển giao diện popup của extension
 */

const statusDot = document.getElementById('statusDot');
const statusText = document.getElementById('statusText');
const toggleEnabled = document.getElementById('toggleEnabled');
const urlInput = document.getElementById('urlInput');
const sendBtn = document.getElementById('sendBtn');
const messageEl = document.getElementById('message');

// === KIỂM TRA TRẠNG THÁI KDM ===
function checkStatus() {
  chrome.runtime.sendMessage({ action: 'checkStatus' }, (response) => {
    if (response) {
      updateConnectionStatus(response.connected);
      toggleEnabled.checked = response.enabled;
    }
  });
}

function updateConnectionStatus(connected) {
  if (connected) {
    statusDot.className = 'status-dot connected';
    statusText.textContent = 'Đang kết nối';
    statusText.style.color = '#3FB950';
  } else {
    statusDot.className = 'status-dot disconnected';
    statusText.textContent = 'Chưa kết nối';
    statusText.style.color = '#F85149';
  }
}

// === TOGGLE BẮT LINK TỰ ĐỘNG ===
toggleEnabled.addEventListener('change', () => {
  chrome.runtime.sendMessage({
    action: 'toggleEnabled',
    enabled: toggleEnabled.checked
  });
});

// === GỬI URL THỦ CÔNG ===
sendBtn.addEventListener('click', sendUrl);
urlInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') sendUrl();
});

function sendUrl() {
  const url = urlInput.value.trim();
  if (!url) {
    showMessage('Vui lòng nhập URL', 'error');
    return;
  }

  // Validate URL
  try {
    new URL(url);
  } catch {
    showMessage('URL không hợp lệ', 'error');
    return;
  }

  sendBtn.disabled = true;
  sendBtn.textContent = '...';

  chrome.runtime.sendMessage({
    action: 'sendToKDM',
    url: url,
    filename: ''
  }, (response) => {
    sendBtn.disabled = false;
    sendBtn.textContent = 'Gửi KDM';

    if (response && response.success) {
      showMessage('✓ Đã gửi đến KDM thành công!', 'success');
      urlInput.value = '';
    } else {
      showMessage('✕ KDM chưa chạy hoặc không phản hồi', 'error');
    }
  });
}

// === HIỂN THỊ THÔNG BÁO ===
function showMessage(text, type) {
  messageEl.textContent = text;
  messageEl.className = `message ${type}`;
  
  setTimeout(() => {
    messageEl.className = 'message hidden';
  }, 3000);
}

// Kiểm tra trạng thái khi mở popup
checkStatus();
