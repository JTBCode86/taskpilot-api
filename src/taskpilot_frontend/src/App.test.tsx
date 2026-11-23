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
      .mockResolvedValueOnce(response({ userId: 7 }, 201))
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
    expect(await screen.findByRole('status')).toHaveTextContent('Conta criada!')
    expect(screen.getByRole('button', { name: /entrar na minha conta/i })).toBeInTheDocument()
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
      firstName: 'Ana', lastName: 'Silva', email: 'ana@example.test', password,
    })
  })

  it('entra, cria e conclui tarefa, filtra e sai sem persistir o token', async () => {
    const tasks: Array<{ id: number; title: string; description: string | null; status: string; priority: string; createdAt: string }> = []
    const fetchMock = vi.fn(async (path: string, init: RequestInit) => {
      if (path === '/api/v1/auth/login') return response(session)
      if (path === '/api/v1/tasks/summary/7') return response(tasks)
      if (path === '/api/v1/tasks' && init.method === 'POST') {
        const body = JSON.parse(String(init.body))
        tasks.push({ id: 21, title: body.title, description: body.description, status: 'Pendente', priority: 'Medium', createdAt: '2026-01-01T10:00:00' })
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
    await user.click(screen.getByRole('button', { name: 'Criar tarefa' }))
    const task = await screen.findByRole('listitem')
    expect(within(task).getByText('Planejar a semana')).toBeInTheDocument()
    const createCall = fetchMock.mock.calls.find(([path]) => path === '/api/v1/tasks')
    expect(createCall).toBeDefined()
    expect(createCall?.[1].headers).toEqual({ 'Content-Type': 'application/json', Authorization: 'Bearer test-token' })
    expect(JSON.parse(String(createCall?.[1].body))).toEqual({ title: 'Planejar a semana', description: 'Definir prioridades', priorityLevel: 2 })

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
})
