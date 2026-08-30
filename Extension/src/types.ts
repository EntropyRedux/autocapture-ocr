export interface ChatTurn {
  role: 'user' | 'assistant' | 'system';
  content: string;
  messageId?: string;
  turnIndex: number;
  timestamp: string;
  isStreaming: boolean;
}

export interface SiteSelectorConfig {
  turnSelector: string;
  roleAttr?: string;
  roleFallback?: {
    user: string;
    assistant: string;
  };
  contentSelector?: string;
  messageIdAttr?: string;
  streamingIndicatorSelector?: string;
}

export type SiteSelectorsMap = Record<string, SiteSelectorConfig>;
