# infrastructure/api_adapter.py (ATUALIZADO)

import requests
from datetime import datetime
from domain.entities import ITaskRepository, Task, CreateTaskCommand
from typing import List

class TaskApiAdapter(ITaskRepository):
    """Implementação do Repositório que interage com a API RESTful."""
    
    def __init__(self, base_url: str, access_token: str):
        self.base_url = base_url
        self.headers = {"Authorization": f"Bearer {access_token}"}

    # --- Método Existente (Busca) ---
    # ... (get_all_tasks permanece o mesmo) ...
    def get_all_tasks(self, user_id: int) -> list[Task]:
        # ... (código anterior) ...
        # (Para fins de concisão, não repetido aqui)
        url = f"{self.base_url}/api/v1/Tasks/summary/{user_id}"
        
        try:
            response = requests.get(url, headers=self.headers, timeout=10)
            response.raise_for_status()
        except requests.exceptions.RequestException as e:
            print(f"Erro ao conectar ou receber resposta da API: {e}")
            return []

        data = response.json()
        
        tasks = []
        for item in data:
            try:
                task_status = item.get('status')
                is_completed_value = (task_status == 'Concluída')

                task = Task(
                    id=item['id'],
                    title=item['title'],
                    description=item.get('description'),
                    created_at=datetime.fromisoformat(item['createdAt'].replace('Z', '+00:00')),
                    due_date=datetime.fromisoformat(item['dueDate'].replace('Z', '+00:00')) if item.get('dueDate') else None,
                    #is_completed=item['isCompleted'],
                    is_completed=is_completed_value,
                    #user_id=item['userId']
                    user_id=user_id
                )
                tasks.append(task)
            except (KeyError, ValueError, TypeError) as e:
                print(f"Erro de mapeamento de dados da tarefa: {e} no item {item}")
                continue
                
        return tasks


    # --- 🆕 NOVO MÉTODO: Criação ---
    def create_task(self, command: CreateTaskCommand) -> int:
        """Cria uma tarefa (POST /api/v1/tasks) e retorna o ID criado."""
        url = f"{self.base_url}/api/v1/Tasks"
        
        # Mapeamento do Command para o JSON esperado pela API
        payload = {
            "title": command.title,
            "description": command.description,
            # A API espera a data no formato ISO 8601 (string)
            "dueDate": command.due_date.isoformat() if command.due_date else None,
            "userId": command.user_id 
        }

        try:
            response = requests.post(url, json=payload, headers=self.headers, timeout=10)
            response.raise_for_status()
            
            # A API deve retornar o ID da tarefa criada (ex: 201 Created)
            # Se a API retornar o ID no corpo como um int, ajuste conforme necessário.
            # Assumimos que a API retorna o ID no corpo ou no cabeçalho.
            # Se o corpo for o ID diretamente:
            if response.status_code == 201:
                 # Assumindo que o corpo retorna o ID (como no seu TasksController)
                return int(response.text)
            
            return 0 # Falha na criação/retorno
            
        except requests.exceptions.HTTPError as e:
            print(f"Erro HTTP na criação da tarefa: {e}. Resposta: {response.text}")
            return 0
        except requests.exceptions.RequestException as e:
            print(f"Erro de conexão na criação da tarefa: {e}")
            return 0

    # --- 🆕 NOVO MÉTODO: Encerramento ---
    def complete_task(self, task_id: int, user_id: int) -> bool:
        """Encerra uma tarefa (POST /api/v1/tasks/{taskId}/complete)."""
        url = f"{self.base_url}/api/v1/Tasks/{task_id}/complete"
        
        try:
            # POST api/v1/tasks/{taskId}/complete -> 204 No Content
            response = requests.post(url, headers=self.headers, timeout=10)
            response.raise_for_status() 
            
            return response.status_code == 204 # Retorna True se for 204 No Content
            
        except requests.exceptions.HTTPError as e:
            print(f"Erro HTTP ao completar tarefa {task_id}: {e}")
            if e.response.status_code == 404:
                print("Tarefa não encontrada.")
            elif e.response.status_code == 400:
                print(f"Regra de negócio violada: {e.response.text}")
            return False
        except requests.exceptions.RequestException as e:
            print(f"Erro de conexão ao completar tarefa: {e}")
            return False