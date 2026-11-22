import type { Routes, UrlMatchResult, UrlSegment } from '@angular/router';

export function chatMatcher(segments: UrlSegment[]): UrlMatchResult | null {
  if (segments.length > 1) {
    return null;
  }

  const [segment] = segments;
  return {
    consumed: segments,
    posParams: segment ? { conversationId: segment } : {},
  };
}

export const CHAT_ROUTES: Routes = [
  {
    matcher: chatMatcher,
    title: 'nav.chat',
    loadComponent: () => import('./chat.page').then((m) => m.ChatPage),
  },
];
