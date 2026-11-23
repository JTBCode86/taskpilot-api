import { useCallback, useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import {
  ArrowDownRight, ArrowRight, Check, CheckCircle2, Circle, ClipboardList, Compass,
  LayoutGrid, ListTodo, LogOut, Plus, RefreshCw, Sparkles, X,
} from 'lucide-react'
import { ApiError, completeTask, createTask, getTasks } from './api'
import type { Session, Task } from './api'

type Props = {
  session: Session
  onSignOut: (message?: string) => void
}

type Filter = 'all' | 'open' | 'done'

const priorityLabels: Record<string, string> = { Low: 'Baixa', Medium: 'Média', High: 'Alta' }

export function Dashboard({ session, onSignOut }: Props) {
  const [tasks, setTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)
  const [hasLoaded, setHasLoaded] = useState(false)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [filter, setFilter] = useState<Filter>('all')
  const [showForm, setShowForm] = useState(false)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [priority, setPriority] = useState(1)
  const [reload, setReload] = useState(0)
  const [today] = useState(() => new Intl.DateTimeFormat('pt-BR', { day: 'numeric', month: 'long', year: 'numeric' }).format(new Date()))
  const mutation = useRef<AbortController | null>(null)
  const active = useRef(true)

  const handleError = useCallback((reason: unknown, fallback: string) => {
    if (reason instanceof ApiError && reason.status === 401) {
      onSignOut('Sua sessão expirou ou não é mais válida. Entre novamente.')
    } else if (reason instanceof ApiError && reason.status === 403) {
      setError('Você não tem permissão para realizar esta ação.')
    } else if (reason instanceof ApiError && reason.status === 400) {
      setError('Não foi possível salvar. Confira os dados e tente novamente.')
    } else {
      setError(fallback)
    }
  }, [onSignOut])

  useEffect(() => {
    active.current = true
    return () => {
      active.current = false
      mutation.current?.abort()
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError('')
      try {
        const result = await getTasks(session, controller.signal)
        if (!controller.signal.aborted) {
          setTasks(result)
          setHasLoaded(true)
        }
      } catch (reason) {
        if (!controller.signal.aborted) handleError(reason, 'Não foi possível carregar suas tarefas. Tente novamente.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [session, handleError, reload])

  async function submitTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const cleanTitle = title.trim()
    if (!cleanTitle) {
      setError('Informe um título para a tarefa.')
      return
    }
    const controller = new AbortController()
    mutation.current = controller
    setWorking(true)
    setError('')
    setMessage('')
    try {
      await createTask(session, cleanTitle, description.trim(), priority, controller.signal)
      if (!active.current || controller.signal.aborted) return
      setTitle('')
      setDescription('')
      setPriority(1)
      setShowForm(false)
      setMessage('Tarefa criada. Um passo de cada vez!')
      setFilter('all')
      setReload((value) => value + 1)
    } catch (reason) {
      if (active.current && !controller.signal.aborted) handleError(reason, 'Não foi possível criar a tarefa. Tente novamente.')
    } finally {
      if (active.current && !controller.signal.aborted) setWorking(false)
      if (mutation.current === controller) mutation.current = null
    }
  }

  async function markComplete(task: Task) {
    const controller = new AbortController()
    mutation.current = controller
    setWorking(true)
    setError('')
    setMessage('')
    try {
      await completeTask(session, task.id, controller.signal)
      if (!active.current || controller.signal.aborted) return
      setTasks((current) => current.map((item) => item.id === task.id ? { ...item, status: 'Concluída' } : item))
      setMessage('Tarefa concluída. Boa conquista!')
    } catch (reason) {
      if (active.current && !controller.signal.aborted) handleError(reason, 'Não foi possível concluir a tarefa. Tente novamente.')
    } finally {
      if (active.current && !controller.signal.aborted) setWorking(false)
      if (mutation.current === controller) mutation.current = null
    }
  }

  const completed = tasks.filter((task) => task.status === 'Concluída').length
  const open = tasks.length - completed
  const visible = [...tasks].reverse().filter((task) => filter === 'all' || (filter === 'done' ? task.status === 'Concluída' : task.status !== 'Concluída'))
  const progress = tasks.length ? Math.round((completed / tasks.length) * 100) : 0

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand"><span className="brand-icon"><Compass size={22} strokeWidth={2.2} /></span><span>taskpilot<span className="brand-dot">.</span></span></div>
        <div className="sidebar-label">ESPAÇO DE TRABALHO</div>
        <div className="sidebar-active"><LayoutGrid size={18} /><span>Visão geral</span><span className="sidebar-indicator" /></div>
        <div className="sidebar-spacer" />
        <div className="sidebar-note"><Sparkles size={17} /><p>Grandes coisas começam com pequenos passos.</p></div>
        <div className="sidebar-account"><div className="avatar">U</div><div><strong>Minha conta</strong><small>Usuário #{session.userId}</small></div><button type="button" className="icon-button" onClick={() => onSignOut()} aria-label="Sair da conta" title="Sair"><LogOut size={18} /></button></div>
      </aside>
      <div className="main-column">
        <header className="topbar">
          <div className="mobile-brand brand"><span className="brand-icon"><Compass size={19} /></span><span>taskpilot<span className="brand-dot">.</span></span></div>
          <div className="breadcrumb"><span>Meu espaço</span><span className="breadcrumb-separator">/</span><strong>Visão geral</strong></div>
          <div className="topbar-right"><span className="topbar-date">{today}</span><button className="mobile-logout icon-button" type="button" onClick={() => onSignOut()} aria-label="Sair da conta"><LogOut size={19} /></button></div>
        </header>
        <main className="dashboard">
          <div className="dashboard-intro">
            <div><div className="eyebrow"><span className="status-dot" /> SEU ESPAÇO DE FOCO</div><h1>Olá, vamos organizar?</h1><p>Um lugar para seus planos ganharem movimento.</p></div>
            <button type="button" className="button button-primary new-task-button" onClick={() => { setShowForm(true); setError(''); setMessage('') }} disabled={showForm}><Plus size={19} /> Nova tarefa</button>
          </div>

          <section className="overview" aria-label="Resumo das tarefas">
            <div className="overview-heading"><div><span className="section-kicker">UM OLHAR RÁPIDO</span><h2>Seu progresso</h2></div><span className="overview-caption">Cada passo conta <ArrowDownRight size={17} /></span></div>
            <div className="stats">
              <div className="stat-card"><div className="stat-top"><span>NO TOTAL</span><span className="stat-icon stat-neutral"><ClipboardList size={18} /></span></div><strong>{hasLoaded ? tasks.length : '—'}</strong><small>tarefas criadas</small></div>
              <div className="stat-card"><div className="stat-top"><span>EM ABERTO</span><span className="stat-icon stat-orange"><ListTodo size={18} /></span></div><strong>{hasLoaded ? open : '—'}</strong><small>para fazer</small></div>
              <div className="stat-card stat-progress"><div className="stat-top"><span>CONCLUÍDAS</span><span className="stat-icon stat-green"><CheckCircle2 size={18} /></span></div><div className="stat-progress-value"><strong>{hasLoaded ? completed : '—'}</strong><span>{hasLoaded ? `${progress}%` : '—'}</span></div><div className="progress-track" role="progressbar" aria-label="Progresso das tarefas" aria-valuenow={progress} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${progress}%` }} /></div></div>
            </div>
          </section>

          {showForm && (
            <section className="task-form-card" aria-labelledby="new-task-title">
              <div className="form-heading"><div><span className="section-kicker">TIRE A IDEIA DA CABEÇA</span><h2 id="new-task-title">Nova tarefa</h2></div><button type="button" className="icon-button" onClick={() => setShowForm(false)} aria-label="Fechar formulário" disabled={working}><X size={20} /></button></div>
              <form onSubmit={submitTask}>
                <label>Título<input autoFocus maxLength={256} placeholder="O que você quer fazer?" value={title} onChange={(e) => setTitle(e.target.value)} required /></label>
                <label>Descrição <span className="optional">opcional</span><textarea rows={2} placeholder="Adicione algum detalhe..." value={description} onChange={(e) => setDescription(e.target.value)} /></label>
                <div className="form-actions"><label>Prioridade<select value={priority} onChange={(e) => setPriority(Number(e.target.value))}><option value={1}>Baixa</option><option value={2}>Média</option><option value={3}>Alta</option></select></label><button className="button button-primary" type="submit" disabled={working}>{working ? 'Salvando...' : 'Criar tarefa'} <ArrowRight size={17} /></button></div>
              </form>
            </section>
          )}

          <section className="tasks-section" aria-labelledby="tasks-title">
            <div className="tasks-heading"><div><span className="section-kicker">UM PASSO DE CADA VEZ</span><h2 id="tasks-title">Suas tarefas <span className="task-count">{hasLoaded ? tasks.length : '—'}</span></h2><p>Tudo o que precisa da sua atenção, em um só lugar.</p></div></div>
            {message && <div className="notice notice-success" role="status">{message}</div>}
            {error && <div className="notice notice-error" role="alert">{error}{!loading && <button type="button" onClick={() => setReload((value) => value + 1)}><RefreshCw size={14} /> Atualizar</button>}</div>}
            <div className="task-panel">
              <div className="filter-bar" role="group" aria-label="Filtrar tarefas">
                {([['all', 'Todas'], ['open', 'Em aberto'], ['done', 'Concluídas']] as const).map(([value, label]) => <button key={value} type="button" className={filter === value ? 'filter active' : 'filter'} aria-pressed={filter === value} onClick={() => setFilter(value)}>{label}</button>)}
              </div>
              {loading ? <div className="empty-state"><p>Carregando suas tarefas...</p></div> : !hasLoaded ? <div className="empty-state"><p>Atualize a lista para tentar novamente.</p></div> : visible.length === 0 ? (
                <div className="empty-state"><div className="empty-icon"><ListTodo size={25} /></div><h3>{tasks.length === 0 ? 'Uma página em branco.' : 'Nada por aqui ainda.'}</h3><p>{tasks.length === 0 ? 'Comece criando sua primeira tarefa. O resto vem depois.' : 'Nenhuma tarefa neste filtro por enquanto.'}</p>{tasks.length === 0 && !showForm && <button type="button" className="text-button empty-action" onClick={() => setShowForm(true)}>Criar primeira tarefa <ArrowRight size={16} /></button>}</div>
              ) : (
                <ul className="task-list">
                  {visible.map((task) => (
                    <li className="task-item" key={task.id}>
                      {task.status === 'Concluída' ? <span className="task-done-icon" aria-label="Concluída"><Check size={16} /></span> : <button type="button" className="complete-button" aria-label={`Concluir ${task.title}`} title={task.status === 'Bloqueada' ? 'Esta tarefa está bloqueada' : 'Concluir tarefa'} disabled={working || task.status === 'Bloqueada'} onClick={() => void markComplete(task)}><Circle size={22} /></button>}
                      <div className="task-content"><h3 className={task.status === 'Concluída' ? 'task-title-done' : ''}>{task.title}</h3>{task.description && <p>{task.description}</p>}<div className="task-meta"><span className={task.status === 'Concluída' ? 'pill pill-done' : 'pill pill-open'}><span />{task.status}</span><span className="priority-label">Prioridade {priorityLabels[task.priority] || task.priority}</span></div></div>
                      <span className="task-id">#{task.id}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </section>
          <footer className="dashboard-footer"><span>Feito para o seu ritmo.</span><span>TASKPILOT <span className="footer-star">✳</span></span></footer>
        </main>
      </div>
    </div>
  )
}
