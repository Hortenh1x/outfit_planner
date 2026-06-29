import { useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addMonths, format, isToday, subMonths } from 'date-fns';
import { CalendarDays, ChevronLeft, ChevronRight, X } from 'lucide-react';
import { listOutfits, listSchedule, scheduleOutfit, unscheduleOutfit } from '../api/client';
import { buildMonthCalendar, weekDayLabels } from '../features/calendar/calendarUtils';
import { OutfitChoiceList } from '../features/calendar/OutfitChoiceList';
import { EditorialDatePicker } from '../shared/ui/EditorialDatePicker';
import { PanelTitle } from '../shared/ui/PanelTitle';

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
  const unscheduleMutation = useMutation({
    mutationFn: unscheduleOutfit,
    onSuccess: () => {
      setOutfitId('');
      void queryClient.invalidateQueries({ queryKey: ['schedule'] });
    }
  });
  const outfits = outfitsQuery.data ?? [];
  const selectedScheduledOutfit = scheduleQuery.data?.find((item) => item.date === date);

  useEffect(() => {
    setOutfitId(selectedScheduledOutfit?.outfitId ?? '');
  }, [date, selectedScheduledOutfit?.outfitId]);

  return (
    <section className="calendar-editorial-page">
      <div className="calendar-workspace">
        <header className="calendar-hero">
          <div>
            <p>Calendar</p>
            <h1>Plan your looks, <em>every day.</em></h1>
          </div>
        </header>
        <div className="calendar-toolbar">
          <span>{format(visibleMonth, 'MMMM yyyy')}</span>
          <div className="calendar-nav">
            <button type="button" aria-label="Previous month" onClick={() => setVisibleMonth((month) => subMonths(month, 1))}>
              <ChevronLeft size={17} />
            </button>
            <button type="button" aria-label="Next month" onClick={() => setVisibleMonth((month) => addMonths(month, 1))}>
              <ChevronRight size={17} />
            </button>
          </div>
        </div>
        <div className="month-calendar" aria-label="Monthly outfit calendar">
          {weekDayLabels.map((dayLabel) => (
            <div className="weekday-cell" key={dayLabel}>
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
                <span>{day.dayNumber}</span>
                {outfit ? <strong>{outfit.name}</strong> : <p>No outfit</p>}
              </button>
            );
          })}
        </div>
      </div>
      <aside className="calendar-plan-rail">
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
            <EditorialDatePicker value={date} onChange={setDate} />
          </label>
          <div className="field-block">
            <span className="field-label">Outfit</span>
            <OutfitChoiceList outfits={outfits} selectedId={outfitId} onSelect={setOutfitId} />
          </div>
          <button type="submit" className="primary-action" disabled={!outfitId || mutation.isPending}>
            <CalendarDays size={16} />
            {mutation.isPending ? 'Planning' : 'Plan day'}
          </button>
          {selectedScheduledOutfit ? (
            <button
              type="button"
              className="secondary-action danger-action"
              disabled={unscheduleMutation.isPending}
              onClick={() => unscheduleMutation.mutate(date)}
            >
              <X size={16} />
              {unscheduleMutation.isPending ? 'Removing' : 'Remove from date'}
            </button>
          ) : null}
          {[mutation.error, unscheduleMutation.error].filter(Boolean).map((error) => (
            <p className="error" key={(error as Error).message}>
              {(error as Error).message}
            </p>
          ))}
        </form>
      </aside>
    </section>
  );
}
