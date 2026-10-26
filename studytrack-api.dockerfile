# Etapa 1: compilar con el SDK completo
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo el .csproj, para cachear el restore
COPY src/StudyTrack.Api/StudyTrack.Api.csproj src/StudyTrack.Api/
RUN dotnet restore src/StudyTrack.Api/StudyTrack.Api.csproj

# Luego el resto del código
COPY src/ src/
RUN dotnet publish src/StudyTrack.Api/StudyTrack.Api.csproj -c Release -o /app/publish --no-restore

# Etapa 2: imagen final, solo con el runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "StudyTrack.Api.dll"]