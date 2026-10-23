import { useEffect, useState, type ReactNode } from 'react';
import { apiFetch, ApiAuthError, type AuthErrorReason } from './client';
import { initTelegram } from './telegram';

type GateState =
  | { status: 'loading' }
  | { status: 'ok' }
  | { status: 'blocked'; reason: AuthErrorReason; message: string; telegramUserId?: number };

/**
 * Probes the API once to establish whether the current Telegram user has access.
 * While loading it shows a spinner; on success it renders the app; otherwise it
 * shows a screen tailored to why access was refused (not in Telegram / pending / denied),
 * surfacing the user's own Telegram id so they can request approval.
 */
export default function AuthGate({ children }: { children: ReactNode }) {
  const [state, setState] = useState<GateState>({ status: 'loading' });

  useEffect(() => {
    initTelegram();
    let cancelled = false;

    // Hitting a lightweight public endpoint is enough to exercise the auth pipeline.
    apiFetch('/player-list')
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
          // Network or server error — let the app load; individual pages handle their own errors.
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
        body="This app is only available through its Telegram bot. Please open it from the bot."
      />
    );
  }

  if (reason === 'pending') {
    return (
      <CenteredMessage title="Access pending" body="Your request is waiting for approval.">
        {telegramUserId !== undefined && <TelegramIdBadge id={telegramUserId} />}
      </CenteredMessage>
    );
  }

  // denied
  return (
    <CenteredMessage title="No access" body={message || 'You do not have access to this app.'}>
      {telegramUserId !== undefined && <TelegramIdBadge id={telegramUserId} />}
    </CenteredMessage>
  );
}

function TelegramIdBadge({ id }: { id: number }) {
  return (
    <div className="mt-4 text-sm text-gray-600">
      <div>Share this Telegram ID with the admin to get access:</div>
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
