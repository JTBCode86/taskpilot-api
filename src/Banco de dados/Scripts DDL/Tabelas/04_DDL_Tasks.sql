-- 4. Tabela de Tarefas (Tasks)
CREATE TABLE Tasks (
    Id INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL,
    StatusId INT NOT NULL,
    Title NVARCHAR(256) NOT NULL,
    Description NVARCHAR(MAX),
    PriorityLevel INT NOT NULL DEFAULT 1, -- 1: Baixa, 2: Média, 3: Alta
    EstimatedDuration INT, -- Duração em minutos
    DueDate DATETIME2,
    CompletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE(),
    
    -- Definição das Chaves Estrangeiras
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (StatusId) REFERENCES TaskStatuses(Id)
);
GO