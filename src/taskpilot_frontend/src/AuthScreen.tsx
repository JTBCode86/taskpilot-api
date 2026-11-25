import { useState } from 'react'
import type { FormEvent } from 'react'
import { ArrowDownRight, ArrowRight, Check, Compass, Layers2, Sparkles } from 'lucide-react'
import { ApiError, login, register, resendVerification } from './api'
import type { Session } from './api'

type Props = {
  notice: string
  onLogin: (session: Session) => void
}

export function AuthScreen({ notice, onLogin }: Props) {
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [message, setMessage] = useState(notice)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [canResendVerification, setCanResendVerification] = useState(false)

  function changeMode(next: 'login' | 'register') {
    setMode(next)
    setError('')
    setMessage('')
    setPassword('')
    setCanResendVerification(false)
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    setMessage('')
    setCanResendVerification(false)
    setBusy(true)
    try {
      if (mode === 'register') {
        await register({ firstName: firstName.trim(), lastName: lastName.trim(), email: email.trim(), password })
        setPassword('')
        setMode('login')
        setCanResendVerification(true)
        setMessage('Conta criada. Confira seu e-mail para confirmar o cadastro.')
      } else {
        const session = await login(email.trim(), password)
        if (Date.parse(session.expiresAt) <= Date.now()) {
          setError('Sua sessão já expirou. Tente entrar novamente.')
          return
        }
        onLogin(session)
      }
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 401) {
        setError('E-mail ou senha inválidos. Confira seus dados e tente novamente.')
      } else if (reason instanceof ApiError && reason.status === 403) {
        setCanResendVerification(true)
        setError('Sua conta ainda não foi confirmada. Confira seu e-mail.')
      } else if (reason instanceof ApiError && reason.status === 409) {
        setError('Este e-mail já está cadastrado. Tente entrar na sua conta.')
      } else if (reason instanceof ApiError && reason.status === 503 && mode === 'register') {
        setPassword('')
        setMode('login')
        setCanResendVerification(true)
        setError('A conta foi criada, mas o e-mail não foi enviado. Tente reenviar a confirmação.')
      } else if (reason instanceof ApiError && reason.status === 400) {
        setError('Confira os dados informados e tente novamente.')
      } else {
        setError('Não foi possível conectar. Verifique a API e tente novamente.')
      }
    } finally {
      setBusy(false)
    }
  }

  async function resendConfirmation() {
    setError('')
    setMessage('')
    setBusy(true)
    try {
      await resendVerification(email.trim())
      setCanResendVerification(false)
      setMessage('Se a conta estiver pendente, enviaremos um novo link de confirmação.')
    } catch (reason) {
      if (reason instanceof ApiError && reason.status === 429) {
        setError('Muitas tentativas. Aguarde um pouco antes de tentar novamente.')
      } else if (reason instanceof ApiError && reason.status === 503) {
        setError('Não foi possível enviar o e-mail agora. Tente novamente mais tarde.')
      } else {
        setError('Não foi possível solicitar a confirmação. Confira o endereço e tente novamente.')
      }
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="auth-page">
      <section className="auth-story" aria-label="Sobre o TaskPilot">
        <div className="auth-story-inner">
          <div className="brand brand-light"><span className="brand-icon"><Compass size={22} strokeWidth={2.2} /></span><span>taskpilot<span className="brand-dot">.</span></span></div>
          <div className="auth-story-content">
            <div className="eyebrow eyebrow-light"><Sparkles size={14} /> UM DIA DE CADA VEZ</div>
            <h1>Mais clareza.<br /><span>Menos correria.</span></h1>
            <p>Um espaço para tirar as ideias da cabeça, organizar o que importa e seguir em frente.</p>
            <div className="story-card">
              <div className="story-card-top"><span>SEU ESPAÇO DE FOCO</span><ArrowDownRight size={19} /></div>
              <div className="story-item"><span className="story-check"><Check size={14} /></span><span>Organize suas prioridades</span></div>
              <div className="story-item"><span className="story-check"><Check size={14} /></span><span>Acompanhe cada conquista</span></div>
              <div className="story-item"><span className="story-check"><Check size={14} /></span><span>Encontre seu próprio ritmo</span></div>
            </div>
          </div>
          <p className="story-footer">FEITO PARA OS SEUS PLANOS <span>✦</span></p>
        </div>
      </section>
      <section className="auth-form-side">
        <div className="auth-mobile-brand brand"><span className="brand-icon"><Compass size={21} /></span><span>taskpilot<span className="brand-dot">.</span></span></div>
        <div className="auth-card">
          <div className="auth-card-icon"><Layers2 size={22} /></div>
          <div className="eyebrow">SEU PRÓXIMO PASSO COMEÇA AQUI</div>
          <h2>{mode === 'login' ? 'Bom ter você de volta.' : 'Vamos começar?'}</h2>
          <p className="auth-description">{mode === 'login' ? 'Entre para continuar de onde parou.' : 'Crie sua conta e dê espaço ao que importa.'}</p>
          {message && <div className="notice notice-success" role="status">{message}</div>}
          {error && <div className="notice notice-error" role="alert">{error}</div>}
          <form onSubmit={submit} className="auth-form">
            {mode === 'register' && (
              <div className="field-row">
                <label>Nome<input value={firstName} onChange={(e) => setFirstName(e.target.value)} autoComplete="given-name" maxLength={100} required /></label>
                <label>Sobrenome<input value={lastName} onChange={(e) => setLastName(e.target.value)} autoComplete="family-name" maxLength={100} required /></label>
              </div>
            )}
            <label>E-mail<input type="email" placeholder="voce@exemplo.com" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="email" maxLength={256} required /></label>
            <label>Senha<input type="password" placeholder={mode === 'register' ? 'Pelo menos 12 caracteres' : 'Sua senha'} value={password} onChange={(e) => setPassword(e.target.value)} autoComplete={mode === 'register' ? 'new-password' : 'current-password'} minLength={12} maxLength={128} required /></label>
            {mode === 'register' && <p className="field-hint">Escolha uma senha com pelo menos 12 caracteres.</p>}
            <button className="button button-primary auth-submit" type="submit" disabled={busy}>
              {busy ? 'Aguarde...' : mode === 'login' ? 'Entrar na minha conta' : 'Criar minha conta'} <ArrowRight size={18} />
            </button>
          </form>
          {mode === 'login' && canResendVerification && (
            <button
              type="button"
              className="text-button resend-verification"
              disabled={busy}
              onClick={resendConfirmation}
            >
              Reenviar e-mail de confirmação
            </button>
          )}
          <p className="auth-switch">
            {mode === 'login' ? 'Ainda não tem uma conta?' : 'Já tem uma conta?'}{' '}
            <button type="button" className="text-button" disabled={busy} onClick={() => changeMode(mode === 'login' ? 'register' : 'login')}>
              {mode === 'login' ? 'Criar conta' : 'Fazer login'}
            </button>
          </p>
        </div>
        <div className="auth-bottom">UM POUCO DE ORDEM, TODOS OS DIAS. <span>✳</span></div>
      </section>
    </main>
  )
}
