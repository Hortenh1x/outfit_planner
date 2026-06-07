import { addDays, format, getDate, isSameMonth, startOfMonth, startOfWeek } from 'date-fns';

export interface CalendarGridDay {
  date: Date;
  isoDate: string;
  dayNumber: number;
  isCurrentMonth: boolean;
}

export function buildMonthCalendar(month: Date): CalendarGridDay[] {
  const monthStart = startOfMonth(month);
  const gridStart = startOfWeek(monthStart, { weekStartsOn: 1 });

  return Array.from({ length: 42 }, (_, index) => {
    const date = addDays(gridStart, index);

    return {
      date,
      isoDate: format(date, 'yyyy-MM-dd'),
      dayNumber: getDate(date),
      isCurrentMonth: isSameMonth(date, monthStart)
    };
  });
}

export const weekDayLabels = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
