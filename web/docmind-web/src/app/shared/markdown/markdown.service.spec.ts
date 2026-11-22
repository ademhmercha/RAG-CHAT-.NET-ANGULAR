import { TestBed } from '@angular/core/testing';
import { MarkdownService } from './markdown.service';

describe('MarkdownService', () => {
  let markdown: MarkdownService;

  beforeEach(() => {
    markdown = TestBed.inject(MarkdownService);
  });

  it('renders common markdown', () => {
    const html = markdown.render('# Title\n\n**bold** and a [link](https://example.com)');

    expect(html).toContain('<h1>Title</h1>');
    expect(html).toContain('<strong>bold</strong>');
    expect(html).toContain('href="https://example.com"');
  });

  it('opens links safely in a new tab', () => {
    const html = markdown.render('[x](https://example.com)');

    expect(html).toContain('target="_blank"');
    expect(html).toContain('rel="noopener noreferrer nofollow"');
  });

  it('strips scripts, event handlers and javascript urls', () => {
    const html = markdown.render('<script>alert(1)</script>\n\n<img src=x onerror="alert(1)">');
    const link = markdown.render('[bad](javascript:alert(1))');

    expect(html).not.toContain('<script');
    expect(html).not.toContain('<img');
    expect(link).not.toContain('href="javascript:');
  });

  it('escapes raw html instead of rendering it', () => {
    expect(markdown.render('<b>raw</b>')).not.toContain('<b>raw</b>');
  });

  it('highlights fenced code and adds a labelled copy button', () => {
    const html = markdown.render('```ts\nconst answer: number = 42;\n```', 'Copier');

    expect(html).toContain('code-lang">ts<');
    expect(html).toContain('hljs-keyword');
    expect(html).toContain('data-copy');
    expect(html).toContain('>Copier<');
  });

  it('falls back to escaped plain text for unknown languages', () => {
    const html = markdown.render('```unknown\n<tag> & text\n```');

    expect(html).toContain('&lt;tag&gt; &amp; text');
  });

  it('renders tables from GitHub-flavoured markdown', () => {
    expect(markdown.render('| a | b |\n|---|---|\n| 1 | 2 |')).toContain('<table>');
  });

  it('returns an empty string for empty input', () => {
    expect(markdown.render('')).toBe('');
  });
});
