import { useCallback, useEffect, useState } from 'react'
import { AuthScreen } from './AuthScreen'
import { Dashboard } from './Dashboard'
import type { Session } from './api'
import './App.css'

export default function App() {
  const [session, setSession] = useState<Session | null>(null)
  const [notice, setNotice] = useState('')

  const signOut = useCallback((message = 'Você saiu da sua conta.') => {
    setSession(null)
    setNotice(message)
  }, [])

  useEffect(() => {
    if (!session) return
    const remaining = Date.parse(session.expiresAt) - Date.now()
    const timer = window.setTimeout(
      () => signOut('Sua sessão expirou. Entre novamente para continuar.'),
      Math.max(0, remaining),
    )
    return () => window.clearTimeout(timer)
  }, [session, signOut])

  if (!session) {
    return <AuthScreen notice={notice} onLogin={(next) => { setNotice(''); setSession(next) }} />
  }

  return <Dashboard session={session} onSignOut={signOut} />
}
