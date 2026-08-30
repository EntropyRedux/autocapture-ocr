import { ChatTurn } from './types';

let socket: WebSocket | null = null;
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
let currentPort = 49281;
let currentAuthToken = '';

async function loadConfig() {
  const config = await chrome.storage.local.get(['wsPort', 'authToken']);
  if (config.wsPort) currentPort = Number(config.wsPort);
  if (config.authToken) currentAuthToken = String(config.authToken);
}

function connectWebSocket() {
  if (socket && (socket.readyState === WebSocket.CONNECTING || socket.readyState === WebSocket.OPEN)) {
    return;
  }

  const url = `ws://127.0.0.1:${currentPort}/chatcapture`;
  try {
    socket = new WebSocket(url, currentAuthToken ? [currentAuthToken] : undefined);

    socket.onopen = () => {
      console.log('[ChatCapture Extension] Connected to AutoCapture-OCR desktop bridge.');
    };

    socket.onclose = () => {
      socket = null;
      scheduleReconnect();
    };

    socket.onerror = (err) => {
      socket = null;
      scheduleReconnect();
    };
  } catch (err) {
    scheduleReconnect();
  }
}

function scheduleReconnect() {
  if (reconnectTimer) clearTimeout(reconnectTimer);
  reconnectTimer = setTimeout(() => {
    connectWebSocket();
  }, 3000);
}

function sendTurnsToBridge(hostname: string, turns: ChatTurn[]) {
  if (!socket || socket.readyState !== WebSocket.OPEN) {
    connectWebSocket();
    return;
  }

  const payload = {
    hostname,
    timestamp: new Date().toISOString(),
    turns
  };

  try {
    socket.send(JSON.stringify(payload));
  } catch (err) {
    console.error('[ChatCapture Extension] Failed to send turns:', err);
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'TURNS_EXTRACTED') {
    sendTurnsToBridge(message.hostname || 'unknown', message.turns || []);
    sendResponse({ status: 'sent' });
  }
  return true;
});

// Initialize on startup
loadConfig().then(() => {
  connectWebSocket();
});
