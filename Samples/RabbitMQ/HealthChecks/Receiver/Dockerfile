FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Flowly/Flowly.csproj src/Flowly/
COPY src/Flowly.RabbitMQ/Flowly.RabbitMQ.csproj src/Flowly.RabbitMQ/
COPY samples/RabbitMQ/HealthChecks/Receiver/Receiver.csproj samples/RabbitMQ/HealthChecks/Receiver/
RUN dotnet restore samples/RabbitMQ/HealthChecks/Receiver/Receiver.csproj

COPY src/Flowly/ src/Flowly/
COPY src/Flowly.RabbitMQ/ src/Flowly.RabbitMQ/
COPY samples/RabbitMQ/HealthChecks/Receiver/ samples/RabbitMQ/HealthChecks/Receiver/

RUN dotnet publish samples/RabbitMQ/HealthChecks/Receiver/Receiver.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Receiver.dll"]
