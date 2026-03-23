FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ClinicalTrials.Shared/ClinicalTrials.Shared/ClinicalTrials.Shared.csproj ClinicalTrials.Shared/ClinicalTrials.Shared/
COPY ClinicalTrials.Mcp/ClinicalTrials.Mcp/ClinicalTrials.Mcp.csproj ClinicalTrials.Mcp/ClinicalTrials.Mcp/

RUN dotnet restore ClinicalTrials.Mcp/ClinicalTrials.Mcp/ClinicalTrials.Mcp.csproj

COPY . .

RUN dotnet publish ClinicalTrials.Mcp/ClinicalTrials.Mcp/ClinicalTrials.Mcp.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ClinicalTrials.Mcp.dll"]
