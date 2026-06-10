import { type CSSProperties, useEffect, useMemo, useState } from 'react';
import { addMonths, format, isToday, subMonths } from 'date-fns';
import { CalendarDays, ChevronLeft, ChevronRight } from 'lucide-react';
import { buildMonthCalendar, weekDayLabels } from '../../features/calendar/calendarUtils';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function ClayDatePicker({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const selectedDate = dateFromIso(value);
  const [isOpen, setIsOpen] = useState(false);
  const [visibleMonth, setVisibleMonth] = useState(selectedDate);
  const days = useMemo(() => buildMonthCalendar(visibleMonth), [visibleMonth]);

  useEffect(() => {
    setVisibleMonth(selectedDate);
  }, [value]);

  return (
    <div className="clay-date-picker">
      <button
        type="button"
        className="date-trigger"
        aria-label={`Choose date ${format(selectedDate, 'dd.MM.yyyy')}`}
        aria-expanded={isOpen}
        onClick={() => setIsOpen((open) => !open)}
      >
        <span style={headingStyle}>{format(selectedDate, 'dd.MM.yyyy')}</span>
        <CalendarDays size={18} />
      </button>
      {isOpen ? (
        <div className="date-popover" role="dialog" aria-label="Date picker">
          <div className="date-popover-header">
            <strong style={headingStyle}>{format(visibleMonth, 'MMMM yyyy')}</strong>
            <div>
              <button type="button" aria-label="Previous picker month" onClick={() => setVisibleMonth((month) => subMonths(month, 1))}>
                <ChevronLeft size={17} />
              </button>
              <button type="button" aria-label="Next picker month" onClick={() => setVisibleMonth((month) => addMonths(month, 1))}>
                <ChevronRight size={17} />
              </button>
            </div>
          </div>
          <div className="date-weekdays">
            {weekDayLabels.map((label) => (
              <span key={label} style={headingStyle}>{label}</span>
            ))}
          </div>
          <div className="date-grid">
            {days.map((day) => (
              <button
                type="button"
                key={day.isoDate}
                className={[
                  'date-day',
                  day.isCurrentMonth ? '' : 'outside-month',
                  day.isoDate === value ? 'selected' : '',
                  isToday(day.date) ? 'today' : ''
                ].filter(Boolean).join(' ')}
                aria-pressed={day.isoDate === value}
                onClick={() => {
                  onChange(day.isoDate);
                  setIsOpen(false);
                }}
              >
                {day.dayNumber}
              </button>
            ))}
          </div>
          <button
            type="button"
            className="date-today-action"
            onClick={() => {
              onChange(format(new Date(), 'yyyy-MM-dd'));
              setIsOpen(false);
            }}
          >
            Today
          </button>
        </div>
      ) : null}
    </div>
  );
}

function dateFromIso(value: string) {
  const [year, month, day] = value.split('-').map(Number);
  return new Date(year, month - 1, day);
}
