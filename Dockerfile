FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/TaskPilot/TaskPilot.Domain/TaskPilot.Domain.csproj TaskPilot.Domain/
COPY src/TaskPilot/TaskPilot.Application/TaskPilot.Application.csproj TaskPilot.Application/
COPY src/TaskPilot/TaskPilot.Infrastructure/TaskPilot.Infrastructure.csproj TaskPilot.Infrastructure/
COPY src/TaskPilot/TaskPilot.API/TaskPilot.API.csproj TaskPilot.API/
RUN dotnet restore TaskPilot.API/TaskPilot.API.csproj
COPY src/TaskPilot/ ./
RUN dotnet publish TaskPilot.API/TaskPilot.API.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskPilot.API.dll"]
