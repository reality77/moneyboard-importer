FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app

#ENV http_proxy=http://193.56.47.20:8080
#ENV https_proxy=http://193.56.47.20:8080
#ENV no_proxy=localhost,.douane,.tma,selenium

COPY . ./

RUN dotnet restore 
RUN dotnet publish -c Release -o /app/out

CMD rm -f /app/out/appsettings.json && rm -f /app/out/appsettings.Development.json

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1

WORKDIR /app

COPY --from=build-env /app/out .

RUN mkdir -p /downloads

ENTRYPOINT ["dotnet", "importer.dll"]
