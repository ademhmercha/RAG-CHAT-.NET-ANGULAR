import { BytesPipe, DateTimePipe, formatBytes } from './format.pipes';

describe('formatBytes', () => {
  it.each([
    [0, '0 B'],
    [512, '512 B'],
    [1024, '1 KB'],
    [1536, '1.5 KB'],
    [5 * 1024 * 1024, '5 MB'],
    [20 * 1024 * 1024, '20 MB'],
    [3.25 * 1024 * 1024 * 1024, '3.25 GB'],
  ])('%i -> %s', (bytes, expected) => {
    expect(formatBytes(bytes)).toBe(expected);
  });

  it('uses the locale decimal separator', () => {
    expect(new BytesPipe().transform(1536, 'fr')).toMatch(/^1,5\sKB$/);
  });
});

describe('DateTimePipe', () => {
  it('formats valid dates and ignores invalid ones', () => {
    const pipe = new DateTimePipe();

    expect(pipe.transform('2030-06-15T10:30:00Z', 'en')).toContain('2030');
    expect(pipe.transform('not a date')).toBe('');
  });
});
