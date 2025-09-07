FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY common/BudgetTracker.Common/*.csproj common/BudgetTracker.Common/
COPY worker/BudgetTracker.Worker/*.csproj worker/BudgetTracker.Worker/
RUN dotnet restore worker/BudgetTracker.Worker/BudgetTracker.Worker.csproj

COPY common/BudgetTracker.Common/ common/BudgetTracker.Common/
COPY worker/BudgetTracker.Worker/ worker/BudgetTracker.Worker/
WORKDIR /src/worker/BudgetTracker.Worker
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BudgetTracker.Worker.dll"]