import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'
import type { APIRequestContext } from '@playwright/test'

async function getVerificationToken(
  request: APIRequestContext,
  email: string,
) {
  const mailpitUrl = process.env.TASKPILOT_MAILPIT_URL ?? 'http://127.0.0.1:8025'
  for (let attempt = 0; attempt < 40; attempt++) {
    const search = await request.get(`${mailpitUrl}/api/v1/search`, {
      params: { query: `to:${email}` },
    })
    if (!search.ok()) throw new Error(`Mailpit search failed (${search.status()}).`)
    const data: unknown = await search.json()
    if (typeof data === 'object' && data !== null) {
      const record = data as Record<string, unknown>
      const messages = record.messages ?? record.Messages
      if (Array.isArray(messages) && messages.length > 0) {
        const first = messages[0]
        if (typeof first === 'object' && first !== null) {
          const message = first as Record<string, unknown>
          const snippet = message.Snippet ?? message.snippet
          if (typeof snippet === 'string') {
            const token = snippet.match(/#token=([A-Za-z0-9_-]+)/)?.[1]
            if (token) return token
          }
        }
      }
    }
    await new Promise((resolve) => setTimeout(resolve, 250))
  }
  throw new Error(`No verification email was captured for ${email}.`)
}

test('contas isoladas podem gerenciar suas tarefas', async ({ page }) => {
  const suffix = randomUUID()
  const firstEmail = `e2e-first-${suffix}@example.test`
  const secondEmail = `e2e-second-${suffix}@example.test`
  const password = `E2e_${randomUUID()}_9a`
  const title = 'Planejar a semana E2E'

  async function register(email: string) {
    await page.getByRole('button', { name: 'Criar conta' }).click()
    await page.getByLabel('Nome', { exact: true }).fill('Teste')
    await page.getByLabel('Sobrenome').fill('E2E')
    await page.getByLabel('E-mail').fill(email)
    await page.getByLabel('Senha').fill(password)
    await page.getByRole('button', { name: 'Criar minha conta' }).click()
    await expect(page.getByRole('status')).toHaveText('Conta criada. Confira seu e-mail para confirmar o cadastro.')
    await page.getByLabel('E-mail').fill(email)
    await page.getByLabel('Senha').fill(password)
    await page.getByRole('button', { name: 'Entrar na minha conta' }).click()
    await expect(page.getByRole('alert')).toHaveText('Sua conta ainda não foi confirmada. Confira seu e-mail.')

    const token = await getVerificationToken(page.request, email)
    const verificationUrl = new URL(`/verify-email#token=${token}`, page.url()).toString()
    await page.goto(verificationUrl)
    await page.getByRole('button', { name: 'Confirmar e-mail' }).click()
    await expect(page.getByRole('status')).toHaveText('E-mail confirmado. Sua conta já pode entrar.')
    await page.getByRole('link', { name: 'Ir para o login' }).click()
  }

  async function login(email: string) {
    await page.getByLabel('E-mail').fill(email)
    await page.getByLabel('Senha').fill(password)
    await page.getByRole('button', { name: 'Entrar na minha conta' }).click()
    await expect(page.getByRole('heading', { name: 'Olá, vamos organizar?' })).toBeVisible()
  }

  async function signOut() {
    await page.getByRole('button', { name: 'Sair da conta' }).click()
    await expect(page.getByRole('status')).toHaveText('Você saiu da sua conta.')
  }

  await page.goto('/')
  await register(firstEmail)
  await login(firstEmail)
  await expect(page.getByText('Uma página em branco.')).toBeVisible()
  await page.getByRole('button', { name: 'Nova tarefa' }).click()
  await page.getByLabel('Título').fill(title)
  await page.getByLabel('Descrição').fill('Organizar as prioridades')
  await page.getByRole('textbox', { name: 'Prazo opcional' }).fill('2000-01-01')
  await page.getByRole('combobox', { name: /Prioridade/ }).selectOption('2')
  await page.getByRole('button', { name: 'Criar tarefa' }).click()

  const task = page.getByRole('listitem').filter({ hasText: title })
  await expect(task).toContainText('Prioridade Média')
  await expect(task).toContainText('Prazo: 01/01/2000 · Atrasada')
  await page.getByRole('button', { name: `Concluir ${title}` }).click()
  await expect(task.getByText('Concluída', { exact: true })).toBeVisible()
  await expect(task).not.toContainText('Atrasada')
  await page.getByRole('button', { name: `Editar ${title}` }).click()
  await page.getByLabel('Título').fill('Semana atualizada')
  await page.getByRole('textbox', { name: 'Prazo opcional' }).fill('2099-12-31')
  await page.getByRole('combobox', { name: /Prioridade/ }).selectOption('3')
  await page.getByRole('button', { name: 'Salvar alterações' }).click()
  const updated = page.getByRole('listitem').filter({ hasText: 'Semana atualizada' })
  await expect(updated).toContainText('Prioridade Alta')
  await expect(updated).toContainText('Prazo: 31/12/2099')
  await expect(updated.getByText('Concluída', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Em aberto', exact: true }).click()
  await expect(updated).toHaveCount(0)
  await page.getByRole('button', { name: 'Concluídas', exact: true }).click()
  await expect(updated).toBeVisible()
  await signOut()

  await register(secondEmail)
  await login(secondEmail)
  await expect(page.getByText('Uma página em branco.')).toBeVisible()
  await expect(page.getByText('Semana atualizada')).toHaveCount(0)
  await signOut()

  await login(firstEmail)
  await expect(updated.getByText('Concluída', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Editar Semana atualizada' }).click()
  await page.getByRole('textbox', { name: 'Prazo opcional' }).fill('')
  await page.getByRole('button', { name: 'Salvar alterações' }).click()
  await expect(updated).not.toContainText('Prazo:')
  await updated.getByRole('button', { name: 'Excluir Semana atualizada' }).click()
  await updated.getByRole('button', { name: 'Cancelar' }).click()
  await expect(updated).toBeVisible()
  await updated.getByRole('button', { name: 'Excluir Semana atualizada' }).click()
  await updated.getByRole('button', { name: 'Confirmar exclusão' }).click()
  await expect(page.getByText('Tarefa excluída permanentemente.')).toBeVisible()
  await expect(updated).toHaveCount(0)
  await page.reload()
  await expect(page.getByRole('button', { name: 'Entrar na minha conta' })).toBeVisible()
  await login(firstEmail)
  await expect(page.getByText('Uma página em branco.')).toBeVisible()
})
