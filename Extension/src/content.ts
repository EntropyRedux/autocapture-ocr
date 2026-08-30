import TurndownService from 'turndown';
import { ChatTurn, SiteSelectorConfig, SiteSelectorsMap } from './types';
import siteSelectorsRaw from './site-selectors.json';

const siteSelectors = siteSelectorsRaw as SiteSelectorsMap;

const turndown = new TurndownService({
  headingStyle: 'atx',
  codeBlockStyle: 'fenced',
  bulletListMarker: '-'
});

// Configure turndown rules for code blocks and math
turndown.addRule('preCode', {
  filter: (node) => {
    return node.nodeName === 'PRE';
  },
  replacement: (content, node) => {
    const el = node as HTMLElement;
    const code = el.querySelector('code');
    const className = code ? code.className : '';
    const match = className.match(/language-(\w+)/);
    const lang = match ? match[1] : '';
    const rawText = code ? code.textContent || '' : el.textContent || '';
    return `\n\`\`\`${lang}\n${rawText.trim()}\n\`\`\`\n`;
  }
});

let debounceTimer: ReturnType<typeof setTimeout> | null = null;
let lastTurnCount = 0;
let lastContentHash = '';

function getMatchingConfig(): SiteSelectorConfig | null {
  const hostname = window.location.hostname;
  for (const key of Object.keys(siteSelectors)) {
    if (hostname.includes(key)) {
      return siteSelectors[key];
    }
  }
  return null;
}

function extractTurns(): ChatTurn[] {
  const config = getMatchingConfig();
  if (!config) return [];

  const turnElements = document.querySelectorAll(config.turnSelector);
  const turns: ChatTurn[] = [];

  turnElements.forEach((el, index) => {
    const htmlEl = el as HTMLElement;

    // Determine role
    let role: 'user' | 'assistant' | 'system' = 'assistant';
    if (config.roleAttr && htmlEl.hasAttribute(config.roleAttr)) {
      const attrVal = htmlEl.getAttribute(config.roleAttr)?.toLowerCase() || '';
      if (attrVal.includes('user')) role = 'user';
      else if (attrVal.includes('system')) role = 'system';
      else role = 'assistant';
    } else if (config.roleFallback) {
      if (htmlEl.matches(config.roleFallback.user) || htmlEl.querySelector(config.roleFallback.user)) {
        role = 'user';
      } else {
        role = 'assistant';
      }
    }

    // Determine message ID
    const messageId = config.messageIdAttr ? htmlEl.getAttribute(config.messageIdAttr) || undefined : undefined;

    // Determine streaming state
    const isStreaming = config.streamingIndicatorSelector
      ? Boolean(htmlEl.querySelector(config.streamingIndicatorSelector) || htmlEl.matches(config.streamingIndicatorSelector))
      : false;

    // Extract content
    const targetContentEl = config.contentSelector
      ? (htmlEl.querySelector(config.contentSelector) as HTMLElement) || htmlEl
      : htmlEl;

    const markdown = turndown.turndown(targetContentEl.innerHTML || targetContentEl.textContent || '');

    if (markdown.trim()) {
      turns.push({
        role,
        content: markdown.trim(),
        messageId,
        turnIndex: index,
        timestamp: new Date().toISOString(),
        isStreaming
      });
    }
  });

  return turns;
}

function notifyBackground() {
  const turns = extractTurns();
  if (turns.length === 0) return;

  const currentHash = JSON.stringify(turns.map(t => `${t.role}:${t.content.length}:${t.isStreaming}`));
  if (currentHash === lastContentHash) return;

  lastContentHash = currentHash;
  lastTurnCount = turns.length;

  try {
    chrome.runtime.sendMessage({
      type: 'TURNS_EXTRACTED',
      hostname: window.location.hostname,
      turns
    });
  } catch (err) {
    // Service worker might be waking up
  }
}

function startObserver() {
  const config = getMatchingConfig();
  if (!config) return;

  const observer = new MutationObserver(() => {
    if (debounceTimer) clearTimeout(debounceTimer);
    debounceTimer = setTimeout(notifyBackground, 500);
  });

  observer.observe(document.body, {
    childList: true,
    subtree: true,
    characterData: true
  });

  // Initial check
  setTimeout(notifyBackground, 1000);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', startObserver);
} else {
  startObserver();
}
