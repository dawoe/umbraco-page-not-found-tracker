import { describe, expect, it } from 'vitest';
import { isDeleteAllConfirmed } from './delete-all.js';

describe('isDeleteAllConfirmed', () => {
  it('accepts "delete" in any case with surrounding spaces', () => {
    for (const input of ['delete', 'Delete', 'DELETE', '  delete  ', ' DeLeTe\t']) {
      expect(isDeleteAllConfirmed(input)).toBe(true);
    }
  });

  it('rejects anything else', () => {
    for (const input of ['', ' ', 'del', 'deletee', 'delete all', 'de lete', 'remove']) {
      expect(isDeleteAllConfirmed(input)).toBe(false);
    }
  });
});
