import { useEffect, useRef, useState } from 'react';
import { Info, X } from 'lucide-react';

const demoNoticeStorageKey = 'outfit-planner-demo-notice-dismissed';

function readDismissed(): boolean {
  try {
    return localStorage.getItem(demoNoticeStorageKey) === '1';
  } catch {
    return false;
  }
}

function persistDismissed(): void {
  try {
    localStorage.setItem(demoNoticeStorageKey, '1');
  } catch {
    // Ignore storage failures (private mode / disabled storage); the notice returns next visit.
  }
}

// First-visit demo banner, shown over every routed page until explicitly closed.
// The info popover opens on hover/focus/tap; a plain click-toggle would fight the
// hover-open (tap emits mouseenter+click together), so closing is done by leaving
// the banner, pressing Escape, or pointing anywhere outside it.
export function DemoNotice() {
  const [isDismissed, setIsDismissed] = useState(readDismissed);
  const [isInfoOpen, setIsInfoOpen] = useState(false);
  const noticeRef = useRef<HTMLElement>(null);

  useEffect(() => {
    if (!isInfoOpen) {
      return;
    }

    const closeOnOutsidePointer = (event: PointerEvent) => {
      if (noticeRef.current && event.target instanceof Node && !noticeRef.current.contains(event.target)) {
        setIsInfoOpen(false);
      }
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsInfoOpen(false);
      }
    };

    document.addEventListener('pointerdown', closeOnOutsidePointer);
    document.addEventListener('keydown', closeOnEscape);

    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [isInfoOpen]);

  if (isDismissed) {
    return null;
  }

  return (
    <aside
      ref={noticeRef}
      className="demo-notice"
      role="status"
      aria-label="Demo mode notice"
      onMouseLeave={() => setIsInfoOpen(false)}
    >
      <p className="demo-notice-text">This is only a demo application: AI features available on request only</p>
      <button
        type="button"
        className="icon-button demo-notice-info"
        aria-label="Why AI features are limited"
        aria-expanded={isInfoOpen}
        aria-describedby={isInfoOpen ? 'demo-notice-popover' : undefined}
        onMouseEnter={() => setIsInfoOpen(true)}
        onFocus={() => setIsInfoOpen(true)}
        onClick={() => setIsInfoOpen(true)}
      >
        <Info size={16} />
      </button>
      <button
        type="button"
        className="icon-button demo-notice-close"
        aria-label="Dismiss demo notice"
        onClick={() => {
          setIsDismissed(true);
          persistDismissed();
        }}
      >
        <X size={16} />
      </button>
      {isInfoOpen ? (
        <div className="demo-notice-popover" id="demo-notice-popover" role="note">
          Due to a lack of resources AI features cannot be supported with the power they need all the time, so if
          you want to see how this app really operates, feel free to contact me at{' '}
          <a href="mailto:dmytro.bolibok@gmail.com">dmytro.bolibok@gmail.com</a>.
        </div>
      ) : null}
    </aside>
  );
}
