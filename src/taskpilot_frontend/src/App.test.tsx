import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import App from './App'

const password = 'TestPassword_12345'
const session = {
  accessToken: 'test-token',
  userId: 7,
  expiresAt: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
}

function response(data: unknown, status = 200) {
  return new Response(status === 204 ? null : JSON.stringify(data), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

async function signIn(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('E-mail'), 'person@example.test')
  await user.type(screen.getByLabelText('Senha'), password)
  await user.click(screen.getByRole('button', { name: /entrar na minha conta/i }))
}

describe('TaskPilot', () => {
  it('cadastra, informa e-mail duplicado e volta ao login após o sucesso', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({}, 409))
      .mockResolvedValueOnce(response({ message: 'Confirmation sent.' }, 202))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: 'Criar conta' }))
    await user.type(screen.getByLabelText('Nome'), 'Ana')
    await user.type(screen.getByLabelText('Sobrenome'), 'Silva')
    await user.type(screen.getByLabelText('E-mail'), 'ana@example.test')
    await user.type(screen.getByLabelText('Senha'), password)
    await user.click(screen.getByRole('button', { name: 'Criar minha conta' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Este e-mail já está cadastrado')
    await user.click(screen.getByRole('button', { name: 'Criar minha conta' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Conta criada. Confira seu e-mail')
    expect(screen.getByRole('button', { name: /entrar na minha conta/i })).toBeInTheDocument()
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      firstName: 'Ana', lastName: 'Silva', email: 'ana@example.test', password,
    })
  })

  it('bloqueia login pendente e permite solicitar outro e-mail de confirmação', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(response({}, 403))
      .mockResolvedValueOnce(response({ message: 'If eligible, sent.' }, 202))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await signIn(user)
    expect(await screen.findByRole('alert')).toHaveTextContent('Sua conta ainda não foi confirmada')
    await user.click(screen.getByRole('button', { name: 'Reenviar e-mail de confirmação' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Se a conta estiver pendente')
    expect(fetchMock.mock.calls[1][0]).toBe('/api/v1/auth/resend-verification')
    expect(JSON.parse(String(fetchMock.mock.calls[1][1].body))).toEqual({ email: 'person@example.test' })
  })

  it('só confirma o e-mail após ação explícita na página de confirmação', async () => {
    window.history.pushState({}, '', '/verify-email#token=sample-token')
    const fetchMock = vi.fn().mockResolvedValue(response(null, 204))
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    expect(fetchMock).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Confirmar e-mail' }))
    expect(await screen.findByRole('status')).toHaveTextContent('E-mail confirmado')
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/auth/verify-email', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ token: 'sample-token' }),
    }))
    window.history.replaceState({}, '', '/')
  })

  it('entra, cria e conclui tarefa, filtra e sai sem persistir o token', async () => {
    const tasks: Array<{ id: number; title: string; description: string | null; status: string; priority: string; createdAt: string; dueDate: string | null }> = []
    const fetchMock = vi.fn(async (path: string, init: RequestInit) => {
      if (path === '/api/v1/auth/login') return response(session)
      if (path === '/api/v1/tasks/summary/7') return response(tasks)
      if (path === '/api/v1/tasks' && init.method === 'POST') {
        const body = JSON.parse(String(init.body))
        tasks.push({ id: 21, title: body.title, description: body.description, status: 'Pendente', priority: 'Medium', createdAt: '2026-01-01T10:00:00', dueDate: body.dueDate })
        return response(21, 201)
      }
      if (path === '/api/v1/tasks/21/complete') return response(null, 204)
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)

    await signIn(user)
    expect(await screen.findByText('Uma página em branco.')).toBeInTheDocument()
    expect(localStorage.getItem('accessToken')).toBeNull()
    await user.click(screen.getByRole('button', { name: /nova tarefa/i }))
    await user.type(screen.getByLabelText('Título'), 'Planejar a semana')
    await user.type(screen.getByLabelText(/descrição/i), 'Definir prioridades')
    await user.selectOptions(screen.getByLabelText('Prioridade'), '2')
    await user.selectOptions(screen.getByLabelText('Filtrar por prioridade'), 'High')
    await user.selectOptions(screen.getByLabelText('Filtrar por prazo'), 'overdue')
    await user.selectOptions(screen.getByLabelText('Ordenar tarefas'), 'priority-desc')
    await user.click(screen.getByRole('button', { name: 'Criar tarefa' }))
    const task = await screen.findByRole('listitem')
    expect(within(task).getByText('Planejar a semana')).toBeInTheDocument()
    expect(screen.getByLabelText('Filtrar por prioridade')).toHaveValue('all')
    expect(screen.getByLabelText('Filtrar por prazo')).toHaveValue('all')
    expect(screen.getByLabelText('Ordenar tarefas')).toHaveValue('recent')
    const createCall = fetchMock.mock.calls.find(([path]) => path === '/api/v1/tasks')
    expect(createCall).toBeDefined()
    expect(createCall?.[1].headers).toEqual({ 'Content-Type': 'application/json', Authorization: 'Bearer test-token' })
    expect(JSON.parse(String(createCall?.[1].body))).toEqual({ title: 'Planejar a semana', description: 'Definir prioridades', priorityLevel: 2, dueDate: null })

    await user.click(within(task).getByRole('button', { name: 'Concluir Planejar a semana' }))
    expect(await within(task).findByText('Concluída')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Em aberto' }))
    expect(screen.queryByText('Planejar a semana')).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Concluídas' }))
    expect(screen.getByText('Planejar a semana')).toBeInTheDocument()
    await user.click(within(screen.getByRole('complementary')).getByRole('button', { name: 'Sair da conta' }))
    expect(screen.getByText('Você saiu da sua conta.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /entrar na minha conta/i })).toBeInTheDocument()
  })

  it('retorna ao login quando a sessão é rejeitada pela API', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(response(session))
      .mockResolvedValueOnce(response({}, 401)))
    const user = userEvent.setup()
    render(<App />)

    await signIn(user)
    expect(await screen.findByText(/sessão expirou ou não é mais válida/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /entrar na minha conta/i })).toBeInTheDocument()
  })

  it('edita tarefa concluída e confirma a exclusão permanente', async () => {
    let task = { id: 21, title: 'Tarefa original', description: 'Detalhes', status: 'Concluída', priority: 'Low', createdAt: '2026-01-01T10:00:00', dueDate: null }
    let deleted = false
    const fetchMock = vi.fn(async (path: string, init: RequestInit) => {
      if (path === '/api/v1/auth/login') return response(session)
      if (path === '/api/v1/tasks/summary/7') return response(deleted ? [] : [task])
      if (path === '/api/v1/tasks/21' && init.method === 'PUT') {
        const body = JSON.parse(String(init.body))
        task = { ...task, title: body.title, description: body.description, priority: 'High' }
        return response({ id: 21 })
      }
      if (path === '/api/v1/tasks/21' && init.method === 'DELETE') {
        deleted = true
        return response(null, 204)
      }
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)
    await signIn(user)
    await user.click(await screen.findByRole('button', { name: 'Editar Tarefa original' }))
    await user.clear(screen.getByLabelText('Título'))
    await user.type(screen.getByLabelText('Título'), 'Tarefa editada')
    await user.selectOptions(screen.getByLabelText('Prioridade'), '3')
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    expect(await screen.findByText('Tarefa editada')).toBeInTheDocument()
    const row = screen.getByRole('listitem')
    expect(within(row).getByText('Concluída')).toBeInTheDocument()
    expect(within(row).getByText('Prioridade Alta')).toBeInTheDocument()
    await user.click(within(row).getByRole('button', { name: 'Excluir Tarefa editada' }))
    await user.click(within(row).getByRole('button', { name: 'Cancelar' }))
    expect(fetchMock.mock.calls.some(([, init]) => init.method === 'DELETE')).toBe(false)
    await user.click(within(row).getByRole('button', { name: 'Excluir Tarefa editada' }))
    await user.click(within(row).getByRole('button', { name: 'Confirmar exclusão' }))
    expect(await screen.findByText('Tarefa excluída permanentemente.')).toBeInTheDocument()
    expect(screen.queryByText('Tarefa editada')).not.toBeInTheDocument()
  })

  it('mantém a tarefa e informa erro quando a exclusão falha', async () => {
    const task = { id: 21, title: 'Tarefa preservada', description: null, status: 'Pendente', priority: 'Low', createdAt: '2026-01-01T10:00:00', dueDate: null }
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(response(session))
      .mockResolvedValueOnce(response([task]))
      .mockResolvedValueOnce(response({}, 500)))
    const user = userEvent.setup()
    render(<App />)
    await signIn(user)
    await user.click(await screen.findByRole('button', { name: 'Excluir Tarefa preservada' }))
    await user.click(screen.getByRole('button', { name: 'Confirmar exclusão' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível excluir')
    expect(screen.getByText('Tarefa preservada')).toBeInTheDocument()
  })

  it('exibe erro e permite tentar novamente quando a lista falha', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(response(session))
      .mockResolvedValueOnce(response({}, 500))
      .mockResolvedValueOnce(response([])))
    const user = userEvent.setup()
    render(<App />)

    await signIn(user)
    expect(await screen.findByRole('alert')).toHaveTextContent('Não foi possível carregar')
    await user.click(screen.getByRole('button', { name: 'Atualizar' }))
    expect(await screen.findByText('Uma página em branco.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  })

  it('cria, edita e remove prazo e destaca apenas tarefas em aberto após a data', async () => {
    let task = { id: 21, title: 'Com prazo', description: null, status: 'Pendente', priority: 'Low', createdAt: '2026-01-01T10:00:00', dueDate: null as string | null }
    const fetchMock = vi.fn(async (path: string, init: RequestInit) => {
      if (path === '/api/v1/auth/login') return response(session)
      if (path === '/api/v1/tasks/summary/7') return response([task])
      if (path === '/api/v1/tasks' && init.method === 'POST') {
        task = { ...task, dueDate: JSON.parse(String(init.body)).dueDate }
        return response(21, 201)
      }
      if (path === '/api/v1/tasks/21' && init.method === 'PUT') {
        const body = JSON.parse(String(init.body))
        if ('dueDate' in body) task = { ...task, dueDate: body.dueDate }
        return response({ id: 21 })
      }
      if (path === '/api/v1/tasks/21/complete') return response(null, 204)
      throw new Error(`Unexpected request: ${path}`)
    })
    vi.stubGlobal('fetch', fetchMock)
    const user = userEvent.setup()
    render(<App />)
    await signIn(user)
    await user.click(await screen.findByRole('button', { name: /nova tarefa/i }))
    await user.type(screen.getByLabelText('Título'), 'Com prazo')
    await user.type(screen.getByLabelText(/Prazo/), '2000-01-01')
    await user.click(screen.getByRole('button', { name: 'Criar tarefa' }))
    expect(await screen.findByText('Prazo: 01/01/2000 · Atrasada')).toBeInTheDocument()
    expect(task.dueDate).toBe('2000-01-01T23:59:59.9999999')
    await user.click(screen.getByRole('button', { name: 'Editar Com prazo' }))
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    await screen.findByText('Tarefa atualizada.')
    const updates = () => fetchMock.mock.calls.filter(([path, init]) => path === '/api/v1/tasks/21' && init.method === 'PUT')
    expect(JSON.parse(String(updates()[0][1].body))).not.toHaveProperty('dueDate')
    await user.click(screen.getByRole('button', { name: 'Editar Com prazo' }))
    await user.clear(screen.getByLabelText(/Prazo/))
    await user.type(screen.getByLabelText(/Prazo/), '2099-12-31')
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    expect(await screen.findByText('Prazo: 31/12/2099')).toBeInTheDocument()
    expect(task.dueDate).toBe('2099-12-31T23:59:59.9999999')
    await user.click(screen.getByRole('button', { name: 'Editar Com prazo' }))
    await user.clear(screen.getByLabelText(/Prazo/))
    await user.click(screen.getByRole('button', { name: 'Salvar alterações' }))
    await waitFor(() => expect(task.dueDate).toBeNull())
    await waitFor(() => expect(screen.queryByText(/Prazo:/)).not.toBeInTheDocument())
  })

  it('não destaca como atrasada tarefa concluída com prazo passado', async () => {
    const task = { id: 21, title: 'Concluída', description: null, status: 'Concluída', priority: 'Low', createdAt: '2026-01-01T10:00:00', dueDate: '2000-01-01T12:00:00' }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response(session)).mockResolvedValueOnce(response([task])))
    const user = userEvent.setup()
    render(<App />)
    await signIn(user)
    expect(await screen.findByText('Prazo: 01/01/2000')).toBeInTheDocument()
    expect(screen.queryByText(/· Atrasada$/)).not.toBeInTheDocument()
  })

  it('não destaca prazo de hoje ou futuro como atrasado', async () => {
    const formatDate = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
    const today = new Date()
    const tomorrow = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1)
    const task = { description: null, status: 'Pendente', priority: 'Low', createdAt: '2026-01-01T10:00:00' }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(response(session)).mockResolvedValueOnce(response([
      { ...task, id: 21, title: 'Hoje', dueDate: `${formatDate(today)}T23:59:59` },
      { ...task, id: 22, title: 'Amanhã', dueDate: `${formatDate(tomorrow)}T23:59:59` },
    ])))
    const user = userEvent.setup()
    render(<App />)
    await signIn(user)
    expect(await screen.findByText('Hoje')).toBeInTheDocument()
    expect(screen.getAllByRole('listitem')).toHaveLength(2)
    expect(screen.queryByText(/· Atrasada$/)).not.toBeInTheDocument()
  })

  it('filtra por prioridade e prazo e ordena tarefas por prazo e prioridade', async () => {
    const task = { description: null, status: 'Pendente', createdAt: '2026-01-01T10:00:00' }
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(response(session))
      .mockResolvedValueOnce(response([
        { ...task, id: 21, title: 'Sem prazo', priority: 'Medium', dueDate: null },
        { ...task, id: 22, title: 'Prazo distante', priority: 'High', dueDate: '2099-12-31T23:59:59' },
        { ...task, id: 23, title: 'Atrasada', priority: 'Low', dueDate: '2000-01-01T23:59:59' },
        { ...task, id: 24, title: 'Prazo próximo', priority: 'Medium', dueDate: '2090-01-01T23:59:59' },
      ])))
    const user = userEvent.setup()
    render(<App />)
    await signIn(user)
    await screen.findByText('Prazo distante')

    await user.selectOptions(screen.getByLabelText('Filtrar por prioridade'), 'High')
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getByText('Prazo distante')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Filtrar por prioridade'), 'all')
    await user.selectOptions(screen.getByLabelText('Filtrar por prazo'), 'overdue')
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getByText('Atrasada')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Filtrar por prazo'), 'with-date')
    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    await user.selectOptions(screen.getByLabelText('Filtrar por prazo'), 'without-date')
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getAllByRole('listitem')[0]).toHaveTextContent('Sem prazo')

    await user.selectOptions(screen.getByLabelText('Filtrar por prazo'), 'all')
    await user.selectOptions(screen.getByLabelText('Ordenar tarefas'), 'due-date')
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      expect.stringContaining('Atrasada'),
      expect.stringContaining('Prazo próximo'),
      expect.stringContaining('Prazo distante'),
      expect.stringContaining('Sem prazo'),
    ])

    await user.selectOptions(screen.getByLabelText('Ordenar tarefas'), 'priority-desc')
    expect(screen.getAllByRole('listitem')[0]).toHaveTextContent('Prazo distante')
    expect(screen.getAllByRole('listitem')[3]).toHaveTextContent('Atrasada')
    await user.selectOptions(screen.getByLabelText('Ordenar tarefas'), 'priority-asc')
    expect(screen.getAllByRole('listitem')[0]).toHaveTextContent('Atrasada')
    expect(screen.getAllByRole('listitem')[3]).toHaveTextContent('Prazo distante')
  })
})
