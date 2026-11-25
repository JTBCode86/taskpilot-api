import { Compass } from 'lucide-react'
import { useState } from 'react'
import { ApiError, verifyEmail } from './api'

export function VerifyEmailScreen() {
  const token = new URLSearchParams(window.location.hash.slice(1)).get('token')
  const [status, setStatus] = useState<'ready' | 'busy' | 'confirmed' | 'failed'>('ready')
  const [error, setError] = useState('')

  async function confirmEmail() {
    if (!token) return
    setStatus('busy')
    setError('')
    try {
      await verifyEmail(token)
      window.history.replaceState({}, '', window.location.pathname)
      setStatus('confirmed')
    } catch (reason) {
      setStatus('failed')
      if (reason instanceof ApiError && reason.status === 429) {
        setError('Muitas tentativas. Aguarde um pouco antes de tentar novamente.')
      } else {
        setError('Este link é inválido ou expirou. Solicite um novo link de confirmação.')
      }
    }
  }

  return (
    <main className="verify-email-page">
      <section className="verify-email-card">
        <div className="brand">
          <span className="brand-icon"><Compass size={22} /></span>
          <span>taskpilot<span className="brand-dot">.</span></span>
        </div>
        <h1>Confirme seu e-mail</h1>
        {status === 'confirmed' ? (
          <>
            <div className="notice notice-success" role="status">E-mail confirmado. Sua conta já pode entrar.</div>
            <a className="button button-primary verify-email-action" href="/">Ir para o login</a>
          </>
        ) : (
          <>
            <p className="auth-description">
              Confirme que este endereço de e-mail pertence a você para liberar o acesso à sua conta.
            </p>
            {error && <div className="notice notice-error" role="alert">{error}</div>}
            {!token ? (
              <div className="notice notice-error" role="alert">O link de confirmação não contém um token válido.</div>
            ) : status === 'failed' ? (
              <a className="button button-primary verify-email-action" href="/">Voltar ao login</a>
            ) : (
              <button
                type="button"
                className="button button-primary verify-email-action"
                disabled={status === 'busy'}
                onClick={confirmEmail}
              >
                {status === 'busy' ? 'Confirmando...' : 'Confirmar e-mail'}
              </button>
            )}
          </>
        )}
      </section>
    </main>
  )
}
