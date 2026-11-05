FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY global.json ./
COPY src/MiniDpp/MiniDpp.csproj src/MiniDpp/
RUN dotnet restore src/MiniDpp/MiniDpp.csproj

COPY src/MiniDpp/ src/MiniDpp/
RUN dotnet publish src/MiniDpp/MiniDpp.csproj --configuration Release \
    --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

RUN mkdir -p /app/App_Data && chown app:app /app/App_Data
ENV ASPNETCORE_HTTP_PORTS=8080 \
    Dpp__DatabasePath=/app/App_Data/minidpp.db
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "MiniDpp.dll"]
