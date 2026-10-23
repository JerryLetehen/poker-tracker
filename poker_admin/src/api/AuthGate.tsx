import { useEffect, useState, type ReactNode } from 'react';
import { apiFetch, ApiAuthError, type AuthErrorReason } from './client';
import { initTelegram } from './telegram';

type GateState =
  | { status: 'loading' }
  | { status: 'ok' }
  | { status: 'blocked'; reason: AuthErrorReason; message: string; telegramUserId?: number };

/**
 * Probes an admin endpoint once to confirm the current Telegram user has admin access.
 * Shows a tailored screen otherwise, surfacing the user's own Telegram id so they can
 * ask to be granted admin.
 */
export default function AuthGate({ children }: { children: ReactNode }) {
  const [state, setState] = useState<GateState>({ status: 'loading' });

  useEffect(() => {
    initTelegram();
    let cancelled = false;

    // A lightweight admin-only endpoint exercises the full admin-scope check.
    apiFetch('/admin/players')
      .then(() => {
        if (!cancelled) setState({ status: 'ok' });
      })
      .catch((err) => {
        if (cancelled) return;
        if (err instanceof ApiAuthError) {
          setState({
            status: 'blocked',
            reason: err.reason,
            message: err.message,
            telegramUserId: err.telegramUserId,
          });
        } else {
          setState({ status: 'ok' });
        }
      });

    return () => {
      cancelled = true;
    };
  }, []);

  if (state.status === 'loading') {
    return <CenteredMessage title="Loading…" />;
  }

  if (state.status === 'blocked') {
    return <AccessScreen reason={state.reason} message={state.message} telegramUserId={state.telegramUserId} />;
  }

  return <>{children}</>;
}

function AccessScreen({
  reason,
  message,
  telegramUserId,
}: {
  reason: AuthErrorReason;
  message: string;
  telegramUserId?: number;
}) {
  if (reason === 'unauthenticated') {
    return (
      <CenteredMessage
        title="Open from Telegram"
        body="The admin app is only available through its Telegram bot."
      />
    );
  }

  // For the admin app, both "pending" and "denied" mean "not an admin here".
  return (
    <CenteredMessage
      title="Admin access required"
      body={reason === 'pending' ? 'Your access is pending approval as an admin.' : message || 'You are not an admin.'}
    >
      {telegramUserId !== undefined && <TelegramIdBadge id={telegramUserId} />}
    </CenteredMessage>
  );
}

function TelegramIdBadge({ id }: { id: number }) {
  return (
    <div className="mt-4 text-sm text-gray-600">
      <div>Share this Telegram ID with an existing admin:</div>
      <div className="mt-1 font-mono text-base font-semibold text-gray-900" data-testid="telegram-id">
        {id}
      </div>
    </div>
  );
}

function CenteredMessage({
  title,
  body,
  children,
}: {
  title: string;
  body?: string;
  children?: ReactNode;
}) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50 px-6">
      <div className="text-center max-w-sm">
        <h1 className="text-lg font-bold text-gray-900">{title}</h1>
        {body && <p className="mt-2 text-sm text-gray-600">{body}</p>}
        {children}
      </div>
    </div>
  );
}
