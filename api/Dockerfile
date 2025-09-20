FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY common/BudgetTracker.Common/*.csproj common/BudgetTracker.Common/
COPY api/BudgetTracker.API/*.csproj api/BudgetTracker.API/
RUN dotnet restore api/BudgetTracker.API/BudgetTracker.API.csproj

COPY common/BudgetTracker.Common/ common/BudgetTracker.Common/
COPY api/BudgetTracker.API/ api/BudgetTracker.API/

# Copy tessdata for OCR
COPY common/BudgetTracker.Common/tessdata common/BudgetTracker.Common/tessdata/

WORKDIR /src/api/BudgetTracker.API
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Tesseract dependencies
RUN apt-get update && apt-get install -y \
    libleptonica-dev \
    libtesseract-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .
# Copy tessdata to the runtime image
COPY --from=build /src/common/BudgetTracker.Common/tessdata ./tessdata

EXPOSE 5000
ENTRYPOINT ["dotnet", "BudgetTracker.API.dll"]