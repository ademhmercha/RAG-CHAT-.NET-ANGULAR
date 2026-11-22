import { Pipe } from '@angular/core';
import type { PipeTransform } from '@angular/core';

const UNITS = ['B', 'KB', 'MB', 'GB'] as const;

export function formatBytes(bytes: number, locale = 'en'): string {
  let value = bytes;
  let unit = 0;

  while (value >= 1024 && unit < UNITS.length - 1) {
    value /= 1024;
    unit++;
  }

  const digits = unit === 0 || value >= 100 ? 0 : value >= 10 ? 1 : 2;
  const formatted = new Intl.NumberFormat(locale, { maximumFractionDigits: digits }).format(value);

  return `${formatted} ${UNITS[unit]}`;
}

@Pipe({ name: 'dmBytes' })
export class BytesPipe implements PipeTransform {
  transform(bytes: number, locale = 'en'): string {
    return formatBytes(bytes, locale);
  }
}

@Pipe({ name: 'dmDateTime' })
export class DateTimePipe implements PipeTransform {
  transform(value: string, locale = 'en'): string {
    const date = new Date(value);

    return Number.isNaN(date.getTime())
      ? ''
      : new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
  }
}
