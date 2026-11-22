-- 5. Tabela de Relacionamento M:N (TaskCategoryMap)
CREATE TABLE TaskCategoryMap (
    TaskId INT NOT NULL,
    CategoryId INT NOT NULL,
    
    PRIMARY KEY (TaskId, CategoryId), -- Chave Primária Composta
    FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE, -- Se a Tarefa for apagada, o registro é apagado aqui.
    FOREIGN KEY (CategoryId) REFERENCES TaskCategories(Id)
);
GO