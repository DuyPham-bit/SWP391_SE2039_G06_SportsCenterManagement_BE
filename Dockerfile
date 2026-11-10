# Build the ASP.NET Core API located under Backend when Railway uses the repo root as its build context.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so dependency restore remains cached between source changes.
COPY Backend/SportsCenterManagement.DAL/SportsCenterManagement.DAL.csproj Backend/SportsCenterManagement.DAL/
COPY Backend/SportsCenterManagement.BLL/SportsCenterManagement.BLL.csproj Backend/SportsCenterManagement.BLL/
COPY Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj Backend/SportsCenterManagement.API/

RUN dotnet restore Backend/SportsCenterManagement.API/SportsCenterManagement.API.csproj

COPY Backend/ Backend/
WORKDIR /src/Backend/SportsCenterManagement.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Ho_Chi_Minh

# Railway supplies PORT at runtime; use 8080 only for local container runs.
ENTRYPOINT ["sh", "-c", "dotnet SportsCenterManagement.API.dll --urls http://+:${PORT:-8080}"]
