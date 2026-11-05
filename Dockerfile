FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/ ./Backend/
RUN dotnet restore Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj
RUN dotnet publish Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet SportsCenterManagement.API.dll"]
