import { useEffect, useRef, useState } from 'react';
import { Info, X } from 'lucide-react';

// v2 key: visitors who dismissed the old "AI features on request" banner still see the payment notice once.
const demoNoticeStorageKey = 'outfit-planner-demo-notice-dismissed-v2';
const contactEmail = 'dmytro.bolibok@gmail.com';

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

// First-visit demo banner (payments run in Stripe test mode), shown until explicitly closed.
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
      <p className="demo-notice-text">Demo: payments run in Stripe test mode, so nothing is ever charged</p>
      <button
        type="button"
        className="icon-button demo-notice-info"
        aria-label="About the test payment mode"
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
          Premium checkout is connected to Stripe in test mode: pay with the test card 4242 4242 4242 4242, any
          future expiry date and any CVC, and no real money is charged. Questions or feedback:{' '}
          <a href={`mailto:${contactEmail}`}>{contactEmail}</a>.
        </div>
      ) : null}
    </aside>
  );
}
