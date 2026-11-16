import { PNFT_EXPORT_FALLBACK_FILE_NAME } from './constants.js';

// Pure helpers of the export (no backoffice runtime imports, so they run in Vitest), plus the browser download.

/** How an export failure reads to the editor. */
export type PnftExportErrorKind = 'unavailable' | 'forbidden' | 'failed';

/**
 * Reads the file name from a `Content-Disposition` header (quoted or not). Falls back to a fixed name when the header
 * is missing or carries none.
 */
export function fileNameFromDisposition(header: string | null | undefined): string {
  const match = header ? /filename\s*=\s*(?:"([^"]+)"|([^;\s]+))/i.exec(header) : null;
  return match?.[1] ?? match?.[2] ?? PNFT_EXPORT_FALLBACK_FILE_NAME;
}

/** Maps an export error to the text the editor sees: 503 is unavailable, 401 and 403 forbidden, anything else failed. */
export function classifyExportError(error: unknown): PnftExportErrorKind {
  const status = (error as { status?: number } | undefined)?.status;

  if (status === 503) {
    return 'unavailable';
  }

  return status === 401 || status === 403 ? 'forbidden' : 'failed';
}

/** Saves a blob as a file download: a temporary object URL and an anchor, revoked afterwards. */
export function saveBlobAsFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);

  try {
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(url);
  }
}
