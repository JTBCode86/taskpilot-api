# main.py (ATUALIZADO)

from infrastructure.api_adapter import TaskApiAdapter
from application.use_cases import GetAllTasksUseCase, CreateTaskUseCase, CompleteTaskUseCase
from domain.entities import Task, CreateTaskCommand
from datetime import datetime, timedelta
import os
import sys
import requests

# 🚨 Configuração: Altere a URL base da sua API
API_BASE_URL = os.environ.get("TASKPILOT_API_URL", "http://localhost:8080")

def print_task_summary(tasks: list[Task], user_id: int):
    """Imprime um resumo das tarefas."""
    # ... (código de resumo de tarefas permanece o mesmo) ...
    if not tasks:
        print("Nenhuma tarefa encontrada para este usuário.")
        return

    print(f"\n--- Resumo de Tarefas para o Usuário {user_id} ---")
    
    for task in tasks:
        status = "COMPLETA" if task.is_completed else "PENDENTE"
        due_date_str = task.due_date.strftime("%Y-%m-%d") if task.due_date else "Sem Prazo"
        
        print(f"  [ID: {task.id}] {task.title}")
        print(f"    Status: {status} | Prazo: {due_date_str}")
        print("-" * 20)
    
    print(f"\nTotal de Tarefas: {len(tasks)}")
    completed_count = sum(1 for t in tasks if t.is_completed)
    print(f"Tarefas Concluídas: {completed_count}")
    print(f"Tarefas Pendentes: {len(tasks) - completed_count}")


def main():
    """Configura o sistema e demonstra os casos de uso."""
    
    email = os.environ.get("TASKPILOT_EMAIL")
    password = os.environ.get("TASKPILOT_PASSWORD")
    if not email or not password:
        print("Defina TASKPILOT_EMAIL e TASKPILOT_PASSWORD.", file=sys.stderr)
        return 1
    try:
        response = requests.post(
            f"{API_BASE_URL}/api/v1/auth/login",
            json={"email": email, "password": password},
            timeout=10,
        )
        response.raise_for_status()
    except requests.exceptions.RequestException as error:
        print(f"Falha ao autenticar: {error}", file=sys.stderr)
        return 1
    auth = response.json()
    user_id = auth["userId"]
    task_api_adapter = TaskApiAdapter(base_url=API_BASE_URL, access_token=auth["accessToken"])
    
    # 2. Injeção de Dependência da Aplicação
    get_all_tasks_use_case = GetAllTasksUseCase(repository=task_api_adapter)
    create_task_use_case = CreateTaskUseCase(repository=task_api_adapter)
    complete_task_use_case = CompleteTaskUseCase(repository=task_api_adapter)
    
    
    # --- DEMONSTRAÇÃO 1: CRIAR UMA NOVA TAREFA ---
    print("\n--- 1. DEMONSTRAÇÃO: CRIAÇÃO DE TAREFA ---")
    new_task_command = CreateTaskCommand(
        title="Nova Tarefa Criada pelo Python",
        description="Esta tarefa deve ser encerrada no próximo passo.",
        due_date=datetime.now() + timedelta(days=7),
        user_id=user_id
    )
    
    new_task_id = create_task_use_case.execute(command=new_task_command)
    
    if new_task_id > 0:
        print(f"Tarefa criada com sucesso. ID: {new_task_id}")
    else:
        print("Falha ao criar tarefa. Verifique o console para erros HTTP.")
        return 1

    
    # --- DEMONSTRAÇÃO 2: ENCERRAMENTO DA TAREFA CRIADA ---
    print("\n--- 2. DEMONSTRAÇÃO: ENCERRAMENTO DE TAREFA ---")
    
    task_to_complete_id = new_task_id
    
    success = complete_task_use_case.execute(
        task_id=task_to_complete_id, 
        user_id=user_id
    )
    
    if success:
        print(f"Tarefa ID {task_to_complete_id} encerrada com sucesso.")
    else:
        print(f"Falha ao encerrar tarefa ID {task_to_complete_id}. Verifique a validação de domínio da API.")
        return 1

    # --- DEMONSTRAÇÃO 3: BUSCAR E EXIBIR TUDO (PARA VERIFICAR MUDANÇAS) ---
    print("\n--- 3. DEMONSTRAÇÃO: BUSCA PÓS-MUDANÇAS ---")
    tasks = get_all_tasks_use_case.execute(user_id=user_id)
    print_task_summary(tasks, user_id)
    if not any(task.id == new_task_id and task.is_completed for task in tasks):
        print("A tarefa concluída não foi encontrada no resumo.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())