import { type CSSProperties, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addMonths, format, isToday, subMonths } from 'date-fns';
import { CalendarDays, ChevronLeft, ChevronRight } from 'lucide-react';
import { listOutfits, listSchedule, scheduleOutfit } from '../api/client';
import { buildMonthCalendar, weekDayLabels } from '../features/calendar/calendarUtils';
import { OutfitChoiceList } from '../features/calendar/OutfitChoiceList';
import { ClayDatePicker } from '../shared/ui/ClayDatePicker';
import { PanelTitle } from '../shared/ui/PanelTitle';

const headingStyle: CSSProperties = { fontFamily: 'Nunito, sans-serif' };

export function CalendarPage() {
  const queryClient = useQueryClient();
  const [visibleMonth, setVisibleMonth] = useState(() => new Date());
  const calendarDays = useMemo(() => buildMonthCalendar(visibleMonth), [visibleMonth]);
  const from = calendarDays[0].isoDate;
  const to = calendarDays[calendarDays.length - 1].isoDate;
  const outfitsQuery = useQuery({ queryKey: ['outfits'], queryFn: listOutfits });
  const scheduleQuery = useQuery({ queryKey: ['schedule', from, to], queryFn: () => listSchedule(from, to) });
  const [date, setDate] = useState(format(new Date(), 'yyyy-MM-dd'));
  const [outfitId, setOutfitId] = useState('');
  const mutation = useMutation({
    mutationFn: scheduleOutfit,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['schedule'] })
  });
  const outfits = outfitsQuery.data ?? [];

  return (
    <section className="page-grid calendar-view">
      <div className="workspace">
        <header className="page-header calendar-header">
          <div>
            <p>Calendar</p>
            <h1 style={headingStyle}>{format(visibleMonth, 'MMMM yyyy')}</h1>
          </div>
          <div className="calendar-nav">
            <button type="button" aria-label="Previous month" onClick={() => setVisibleMonth((month) => subMonths(month, 1))}>
              <ChevronLeft size={17} />
            </button>
            <button type="button" aria-label="Next month" onClick={() => setVisibleMonth((month) => addMonths(month, 1))}>
              <ChevronRight size={17} />
            </button>
          </div>
        </header>
        <div className="month-calendar" aria-label="Monthly outfit calendar">
          {weekDayLabels.map((dayLabel) => (
            <div className="weekday-cell" key={dayLabel} style={headingStyle}>
              {dayLabel}
            </div>
          ))}
          {calendarDays.map((day) => {
            const scheduled = scheduleQuery.data?.find((item) => item.date === day.isoDate);
            const outfit = outfits.find((item) => item.id === scheduled?.outfitId);
            return (
              <button
                type="button"
                className={[
                  'calendar-day',
                  day.isCurrentMonth ? '' : 'muted-day',
                  day.isoDate === date ? 'selected-day' : '',
                  isToday(day.date) ? 'today' : ''
                ].filter(Boolean).join(' ')}
                key={day.isoDate}
                onClick={() => setDate(day.isoDate)}
              >
                <span style={headingStyle}>{day.dayNumber}</span>
                {outfit ? <strong style={headingStyle}>{outfit.name}</strong> : <p>No outfit</p>}
              </button>
            );
          })}
        </div>
      </div>
      <aside className="tool-panel">
        <PanelTitle icon={<CalendarDays size={19} />} title="Plan day" />
        <form
          className="stack"
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate({ date, outfitId });
          }}
        >
          <label>
            <span>Date</span>
            <ClayDatePicker value={date} onChange={setDate} />
          </label>
          <div className="field-block">
            <span className="field-label">Outfit</span>
            <OutfitChoiceList outfits={outfits} selectedId={outfitId} onSelect={setOutfitId} />
          </div>
          <button type="submit" className="clay-button primary-action" disabled={!outfitId || mutation.isPending}>
            <CalendarDays size={16} />
            {mutation.isPending ? 'Planning' : 'Plan day'}
          </button>
        </form>
      </aside>
    </section>
  );
}
