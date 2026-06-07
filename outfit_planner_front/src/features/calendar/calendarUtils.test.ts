import { describe, expect, it } from 'vitest';
import { buildMonthCalendar } from './calendarUtils';

describe('calendar utilities', () => {
  it('builds a six-week month grid starting on Monday', () => {
    const days = buildMonthCalendar(new Date(2026, 4, 21));

    expect(days).toHaveLength(42);
    expect(days[0]).toMatchObject({ isoDate: '2026-04-27', isCurrentMonth: false });
    expect(days[4]).toMatchObject({ isoDate: '2026-05-01', isCurrentMonth: true });
    expect(days[41]).toMatchObject({ isoDate: '2026-06-07', isCurrentMonth: false });
  });
});
