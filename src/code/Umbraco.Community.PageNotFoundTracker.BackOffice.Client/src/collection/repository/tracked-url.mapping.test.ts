import { describe, expect, it } from 'vitest';
import type { TrackedUrlResponseModel } from '../../api/index.js';
import { mapTrackedUrl } from './tracked-url.mapping.js';

const item: TrackedUrlResponseModel = {
  id: 7,
  url: '/Old-Page',
  relativePath: '/old-page',
  domain: 'localhost:44402/sitea',
  culture: 'en-US',
  isFile: false,
  hitCount: 3,
  firstSeen: '2026-11-01T10:00:00Z',
  lastSeen: '2026-11-02T10:00:00Z',
  canCreateRedirect: true,
};

describe('mapTrackedUrl', () => {
  it('maps every field, the entity type and the unique', () => {
    expect(mapTrackedUrl(item)).toEqual({
      entityType: 'page-not-found-tracker-url',
      unique: '7',
      id: 7,
      url: '/Old-Page',
      relativePath: '/old-page',
      domain: 'localhost:44402/sitea',
      culture: 'en-US',
      isFile: false,
      hitCount: 3,
      firstSeen: '2026-11-01T10:00:00Z',
      lastSeen: '2026-11-02T10:00:00Z',
      canCreateRedirect: true,
    });
  });

  it('keeps canCreateRedirect false', () => {
    expect(mapTrackedUrl({ ...item, canCreateRedirect: false }).canCreateRedirect).toBe(false);
  });
});
