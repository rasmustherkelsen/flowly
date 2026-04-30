FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Flowly/Flowly.csproj src/Flowly/
COPY src/Flowly.AzureServiceBus/Flowly.AzureServiceBus.csproj src/Flowly.AzureServiceBus/
COPY samples/AzureServiceBus/HealthChecks/Receiver/Receiver.csproj samples/AzureServiceBus/HealthChecks/Receiver/
RUN dotnet restore samples/AzureServiceBus/HealthChecks/Receiver/Receiver.csproj

COPY src/Flowly/ src/Flowly/
COPY src/Flowly.AzureServiceBus/ src/Flowly.AzureServiceBus/
COPY samples/AzureServiceBus/HealthChecks/Receiver/ samples/AzureServiceBus/HealthChecks/Receiver/

RUN dotnet publish samples/AzureServiceBus/HealthChecks/Receiver/Receiver.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Receiver.dll"]
