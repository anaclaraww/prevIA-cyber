# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só refaz quando um .csproj muda.
COPY Directory.Build.props GlobalSolutionCitizenTech.slnx ./
COPY src/GlobalSolutionCitizenTech.Domain/*.csproj src/GlobalSolutionCitizenTech.Domain/
COPY src/GlobalSolutionCitizenTech.Application/*.csproj src/GlobalSolutionCitizenTech.Application/
COPY src/GlobalSolutionCitizenTech.Infrastructure/*.csproj src/GlobalSolutionCitizenTech.Infrastructure/
COPY src/GlobalSolutionCitizenTech.Api/*.csproj src/GlobalSolutionCitizenTech.Api/
RUN dotnet restore src/GlobalSolutionCitizenTech.Api/GlobalSolutionCitizenTech.Api.csproj

COPY src/ src/
RUN dotnet publish src/GlobalSolutionCitizenTech.Api/GlobalSolutionCitizenTech.Api.csproj -c Release -o /app --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "GlobalSolutionCitizenTech.Api.dll"]
