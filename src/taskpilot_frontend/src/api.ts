export type Session = {
  accessToken: string
  userId: number
  expiresAt: string
}

export type Task = {
  id: number
  title: string
  description: string | null
  status: string
  priority: string
  createdAt: string
  dueDate: string | null
}

export type Registration = {
  firstName: string
  lastName: string
  email: string
  password: string
}

export class ApiError extends Error {
  status: number

  constructor(status: number) {
    super(`A solicitação falhou (${status}).`)
    this.status = status
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null
}

function isSession(value: unknown): value is Session {
  return isRecord(value) &&
    typeof value.accessToken === 'string' && value.accessToken.length > 0 &&
    typeof value.userId === 'number' && Number.isInteger(value.userId) && value.userId > 0 &&
    typeof value.expiresAt === 'string' && Number.isFinite(Date.parse(value.expiresAt))
}

function isTask(value: unknown): value is Task {
  return isRecord(value) &&
    typeof value.id === 'number' && Number.isInteger(value.id) &&
    typeof value.title === 'string' &&
    (typeof value.description === 'string' || value.description === null) &&
    typeof value.status === 'string' &&
    typeof value.priority === 'string' &&
    typeof value.createdAt === 'string' &&
    (value.dueDate === null || (typeof value.dueDate === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value.dueDate) && Number.isFinite(Date.parse(value.dueDate))))
}

async function request(path: string, init: RequestInit): Promise<unknown> {
  const response = await fetch(path, init)
  if (!response.ok) throw new ApiError(response.status)
  if (response.status === 204) return undefined
  return response.json() as Promise<unknown>
}

function headers(token?: string): HeadersInit {
  const result: Record<string, string> = { 'Content-Type': 'application/json' }
  if (token) result.Authorization = `Bearer ${token}`
  return result
}

export async function register(data: Registration): Promise<void> {
  await request('/api/v1/auth/register', {
    method: 'POST',
    headers: headers(),
    body: JSON.stringify(data),
  })
}

export async function verifyEmail(token: string): Promise<void> {
  await request('/api/v1/auth/verify-email', {
    method: 'POST',
    headers: headers(),
    body: JSON.stringify({ token }),
  })
}

export async function resendVerification(email: string): Promise<void> {
  await request('/api/v1/auth/resend-verification', {
    method: 'POST',
    headers: headers(),
    body: JSON.stringify({ email }),
  })
}

export async function login(email: string, password: string): Promise<Session> {
  const result = await request('/api/v1/auth/login', {
    method: 'POST',
    headers: headers(),
    body: JSON.stringify({ email, password }),
  })
  if (!isSession(result)) throw new Error('Resposta de autenticação inválida.')
  return result
}

export async function getTasks(session: Session, signal: AbortSignal): Promise<Task[]> {
  const result = await request(`/api/v1/tasks/summary/${session.userId}`, {
    headers: headers(session.accessToken),
    signal,
  })
  if (!Array.isArray(result) || !result.every(isTask)) throw new Error('Resposta de tarefas inválida.')
  return result
}

export async function createTask(
  session: Session,
  title: string,
  description: string,
  priorityLevel: number,
  dueDate: string | null,
  signal: AbortSignal,
): Promise<void> {
  const result = await request('/api/v1/tasks', {
    method: 'POST',
    headers: headers(session.accessToken),
    body: JSON.stringify({ title, description: description || null, priorityLevel, dueDate }),
    signal,
  })
  if (typeof result !== 'number' || !Number.isInteger(result) || result <= 0) {
    throw new Error('Resposta de criação inválida.')
  }
}

export async function completeTask(session: Session, id: number, signal: AbortSignal): Promise<void> {
  await request(`/api/v1/tasks/${id}/complete`, {
    method: 'POST',
    headers: headers(session.accessToken),
    signal,
  })
}

export async function updateTask(
  session: Session, id: number, title: string, description: string, priorityLevel: number, dueDate: string | null | undefined, signal: AbortSignal,
): Promise<void> {
  await request(`/api/v1/tasks/${id}`, {
    method: 'PUT',
    headers: headers(session.accessToken),
    body: JSON.stringify({ title, description: description || null, priorityLevel, dueDate }),
    signal,
  })
}

export async function deleteTask(session: Session, id: number, signal: AbortSignal): Promise<void> {
  await request(`/api/v1/tasks/${id}`, {
    method: 'DELETE',
    headers: headers(session.accessToken),
    signal,
  })
}
