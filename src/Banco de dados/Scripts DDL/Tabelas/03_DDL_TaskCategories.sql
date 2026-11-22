-- 3. Tabela de Categorias (TaskCategories)
CREATE TABLE TaskCategories (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL UNIQUE,
    Description NVARCHAR(500)
);
GO