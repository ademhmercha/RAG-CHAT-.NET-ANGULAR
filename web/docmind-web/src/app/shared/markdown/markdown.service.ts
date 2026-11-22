import { Injectable } from '@angular/core';
import DOMPurify from 'dompurify';
import hljs from 'highlight.js/lib/core';
import bash from 'highlight.js/lib/languages/bash';
import csharp from 'highlight.js/lib/languages/csharp';
import css from 'highlight.js/lib/languages/css';
import json from 'highlight.js/lib/languages/json';
import javascript from 'highlight.js/lib/languages/javascript';
import python from 'highlight.js/lib/languages/python';
import sql from 'highlight.js/lib/languages/sql';
import typescript from 'highlight.js/lib/languages/typescript';
import xml from 'highlight.js/lib/languages/xml';
import yaml from 'highlight.js/lib/languages/yaml';
import { Marked } from 'marked';
import type { Tokens } from 'marked';

hljs.registerLanguage('bash', bash);
hljs.registerLanguage('csharp', csharp);
hljs.registerLanguage('css', css);
hljs.registerLanguage('json', json);
hljs.registerLanguage('javascript', javascript);
hljs.registerLanguage('python', python);
hljs.registerLanguage('sql', sql);
hljs.registerLanguage('typescript', typescript);
hljs.registerLanguage('xml', xml);
hljs.registerLanguage('yaml', yaml);

const ALIASES: Readonly<Record<string, string>> = {
  sh: 'bash',
  shell: 'bash',
  cs: 'csharp',
  js: 'javascript',
  ts: 'typescript',
  py: 'python',
  html: 'xml',
  yml: 'yaml',
};

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function highlightCode(code: string, requested: string | undefined): string {
  const name = requested ? (ALIASES[requested.toLowerCase()] ?? requested.toLowerCase()) : '';

  if (name && hljs.getLanguage(name)) {
    return hljs.highlight(code, { language: name, ignoreIllegals: true }).value;
  }

  return escapeHtml(code);
}

const SANITIZE_CONFIG = {
  USE_PROFILES: { html: true },
  ADD_ATTR: ['target'],
  FORBID_TAGS: ['style', 'form', 'input', 'textarea', 'select'],
  FORBID_ATTR: ['style'],
};

let hooksInstalled = false;

function installHooks(): void {
  if (hooksInstalled) {
    return;
  }

  hooksInstalled = true;
  DOMPurify.addHook('afterSanitizeAttributes', (node) => {
    if (node.tagName === 'A') {
      node.setAttribute('target', '_blank');
      node.setAttribute('rel', 'noopener noreferrer nofollow');
    }
  });
}

@Injectable({ providedIn: 'root' })
export class MarkdownService {
  private readonly marked = this.createMarked();

  constructor() {
    installHooks();
  }

  private copyLabel = 'Copy';

  render(markdown: string, copyLabel = 'Copy'): string {
    if (!markdown) {
      return '';
    }

    this.copyLabel = copyLabel;
    const html = this.marked.parse(markdown, { async: false });
    return DOMPurify.sanitize(html, SANITIZE_CONFIG);
  }

  private createMarked(): Marked {
    const instance = new Marked({ gfm: true, breaks: true });

    instance.use({
      renderer: {
        html: ({ text }: Tokens.HTML | Tokens.Tag) => escapeHtml(text),
        code: ({ text, lang }: Tokens.Code) => {
          const language = (lang ?? '').split(/\s+/)[0] ?? '';
          const label = language || 'text';

          return (
            `<div class="code-block"><div class="code-header"><span class="code-lang">${escapeHtml(label)}</span>` +
            `<button type="button" class="code-copy" data-copy>${escapeHtml(this.copyLabel)}</button></div>` +
            `<pre><code class="hljs">${highlightCode(text, language)}</code></pre></div>`
          );
        },
      },
    });

    return instance;
  }
}
