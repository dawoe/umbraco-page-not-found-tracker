import { describe, expect, it, vi } from 'vitest';
import { createDashboardAccessCheck, isAccessAllowed } from './dashboard-access.js';

describe('isAccessAllowed', () => {
  it('is true only when the response says allowed', () => {
    expect(isAccessAllowed({ data: { allowed: true } })).toBe(true);
    expect(isAccessAllowed({ data: { allowed: false } })).toBe(false);
  });

  it('fails closed on an error or a missing body', () => {
    expect(isAccessAllowed({ data: { allowed: true }, error: new Error('x') })).toBe(false);
    expect(isAccessAllowed({ error: { status: 401 } })).toBe(false);
    expect(isAccessAllowed({})).toBe(false);
    expect(isAccessAllowed(undefined)).toBe(false);
    expect(isAccessAllowed({ data: {} as { allowed: boolean } })).toBe(false);
  });
});

describe('createDashboardAccessCheck', () => {
  it('requests once and shares the answer', async () => {
    const request = vi.fn().mockResolvedValue({ data: { allowed: true } });
    const check = createDashboardAccessCheck(request);

    const [first, second] = await Promise.all([check(), check()]);
    const third = await check();

    expect([first, second, third]).toEqual([true, true, true]);
    expect(request).toHaveBeenCalledTimes(1);
  });

  it('does not request before it is asked', () => {
    const request = vi.fn().mockResolvedValue({ data: { allowed: true } });
    createDashboardAccessCheck(request);

    expect(request).not.toHaveBeenCalled();
  });

  it('fails closed when the request rejects, and does not retry', async () => {
    const request = vi.fn().mockRejectedValue(new Error('network'));
    const check = createDashboardAccessCheck(request);

    expect(await check()).toBe(false);
    expect(await check()).toBe(false);
    expect(request).toHaveBeenCalledTimes(1);
  });

  it('fails closed when the request throws synchronously', async () => {
    const request = vi.fn(() => {
      throw new Error('boom');
    });
    const check = createDashboardAccessCheck(request);

    expect(await check()).toBe(false);
  });

  it('reports not allowed when the server says so', async () => {
    const check = createDashboardAccessCheck(() => Promise.resolve({ data: { allowed: false } }));

    expect(await check()).toBe(false);
  });
});
