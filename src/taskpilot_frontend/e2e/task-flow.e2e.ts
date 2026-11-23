import { randomUUID } from 'node:crypto'
import { expect, test } from '@playwright/test'

test('contas isoladas podem criar e concluir tarefas', async ({ page }) => {
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
    await expect(page.getByRole('status')).toHaveText('Conta criada! Entre com seu e-mail e senha.')
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
  await page.getByRole('combobox', { name: /Prioridade/ }).selectOption('2')
  await page.getByRole('button', { name: 'Criar tarefa' }).click()

  const task = page.getByRole('listitem').filter({ hasText: title })
  await expect(task).toContainText('Prioridade Média')
  await page.getByRole('button', { name: `Concluir ${title}` }).click()
  await expect(task.getByText('Concluída', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Em aberto', exact: true }).click()
  await expect(task).toHaveCount(0)
  await page.getByRole('button', { name: 'Concluídas', exact: true }).click()
  await expect(task).toBeVisible()
  await signOut()

  await register(secondEmail)
  await login(secondEmail)
  await expect(page.getByText('Uma página em branco.')).toBeVisible()
  await expect(page.getByText(title)).toHaveCount(0)
  await signOut()

  await login(firstEmail)
  await expect(task.getByText('Concluída', { exact: true })).toBeVisible()
  await page.reload()
  await expect(page.getByRole('button', { name: 'Entrar na minha conta' })).toBeVisible()
})
