export const MAX_UPLOAD_BYTES = 20 * 1024 * 1024;
export const ACCEPTED_EXTENSIONS = ['.pdf', '.docx'] as const;
export const ACCEPT_ATTRIBUTE = ACCEPTED_EXTENSIONS.join(',');

export type UploadRejection = 'type' | 'tooLarge' | 'empty';

export function validateUpload(file: Pick<File, 'name' | 'size'>): UploadRejection | null {
  const name = file.name.toLowerCase();

  if (!ACCEPTED_EXTENSIONS.some((extension) => name.endsWith(extension))) {
    return 'type';
  }

  if (file.size === 0) {
    return 'empty';
  }

  return file.size > MAX_UPLOAD_BYTES ? 'tooLarge' : null;
}
