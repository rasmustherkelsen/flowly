FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Flowly/Flowly.csproj Flowly/
COPY Flowly.RabbitMQ/Flowly.RabbitMQ.csproj Flowly.RabbitMQ/
COPY Samples/RabbitMQ/HealthChecks/Receiver/Receiver.csproj Samples/RabbitMQ/HealthChecks/Receiver/
RUN dotnet restore Samples/RabbitMQ/HealthChecks/Receiver/Receiver.csproj

COPY Flowly/ Flowly/
COPY Flowly.RabbitMQ/ Flowly.RabbitMQ/
COPY Samples/RabbitMQ/HealthChecks/Receiver/ Samples/RabbitMQ/HealthChecks/Receiver/

RUN dotnet publish Samples/RabbitMQ/HealthChecks/Receiver/Receiver.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Receiver.dll"]
