import { Component, type ErrorInfo, type ReactNode } from 'react';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

// Catches render-time errors anywhere in the tree so a single component failure shows a recoverable
// message instead of blanking the entire SPA.
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    // Surface the failure so it is diagnosable rather than silent.
    console.error('Unhandled UI error', error, info.componentStack);
  }

  private readonly handleReload = (): void => {
    window.location.reload();
  };

  render(): ReactNode {
    if (!this.state.hasError) {
      return this.props.children;
    }

    return (
      <div role="alert" style={{ maxWidth: '32rem', margin: '4rem auto', padding: '1.5rem', textAlign: 'center' }}>
        <h1 style={{ margin: '0 0 0.5rem' }}>Something went wrong</h1>
        <p style={{ margin: '0 0 1.25rem' }}>The page hit an unexpected error. Reloading usually fixes it.</p>
        <button type="button" onClick={this.handleReload}>
          Reload
        </button>
      </div>
    );
  }
}
