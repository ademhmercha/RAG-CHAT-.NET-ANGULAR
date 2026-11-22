import { MAX_UPLOAD_BYTES, validateUpload } from './upload-validation';

describe('validateUpload', () => {
  it.each([
    ['report.pdf', 1024, null],
    ['REPORT.PDF', 1024, null],
    ['notes.docx', 2048, null],
    ['photo.png', 1024, 'type'],
    ['archive.pdf.exe', 1024, 'type'],
    ['legacy.doc', 1024, 'type'],
    ['empty.pdf', 0, 'empty'],
    ['huge.pdf', MAX_UPLOAD_BYTES + 1, 'tooLarge'],
    ['limit.pdf', MAX_UPLOAD_BYTES, null],
  ])('%s (%i bytes) -> %s', (name, size, expected) => {
    expect(validateUpload({ name, size })).toBe(expected);
  });
});
