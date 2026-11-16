import { afterEach, describe, expect, it, vi } from 'vitest';
import { classifyExportError, fileNameFromDisposition, saveBlobAsFile } from './export.service.js';

describe('fileNameFromDisposition', () => {
  it('fileNameFromDisposition_QuotedFileName_ReturnsIt', () => {
    expect(fileNameFromDisposition('attachment; filename="page-not-found-20261116-103000.csv"')).toBe('page-not-found-20261116-103000.csv');
  });

  it('fileNameFromDisposition_UnquotedFileName_ReturnsIt', () => {
    expect(fileNameFromDisposition('attachment; filename=export.csv')).toBe('export.csv');
  });

  it.each([null, undefined, '', 'attachment', 'garbage; nothing=here'])('fileNameFromDisposition_%s_FallsBack', (header) => {
    expect(fileNameFromDisposition(header)).toBe('page-not-found.csv');
  });
});

describe('classifyExportError', () => {
  it('classifyExportError_503_IsUnavailable', () => {
    expect(classifyExportError({ status: 503 })).toBe('unavailable');
  });

  it.each([401, 403])('classifyExportError_%i_IsForbidden', (status) => {
    expect(classifyExportError({ status })).toBe('forbidden');
  });

  it('classifyExportError_NetworkErrorOrOtherStatus_IsFailed', () => {
    expect(classifyExportError(new TypeError('Failed to fetch'))).toBe('failed');
    expect(classifyExportError({ status: 500 })).toBe('failed');
    expect(classifyExportError(undefined)).toBe('failed');
  });
});

describe('saveBlobAsFile', () => {
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('saveBlobAsFile_RevokesTheObjectUrlOnlyAfterTheBrowserHadTimeToStartTheDownload', () => {
    vi.useFakeTimers();
    const anchor = { addEventListener: vi.fn(), click: vi.fn(), remove: vi.fn(), style: {} as Record<string, string> };
    vi.stubGlobal('document', { createElement: vi.fn(() => anchor), body: { appendChild: vi.fn() } });
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test');
    const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);

    saveBlobAsFile(new Blob(['x']), 'file.csv');

    expect(anchor.click).toHaveBeenCalledOnce();
    // Firefox and Safari can fail a download whose object URL was revoked in the same tick as the click.
    expect(revoke).not.toHaveBeenCalled();
    vi.advanceTimersByTime(60_000);
    expect(revoke).toHaveBeenCalledWith('blob:test');
  });
});
