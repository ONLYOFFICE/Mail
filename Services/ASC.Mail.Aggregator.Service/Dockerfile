#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Services/ASC.Mail.Aggregator.Service/ASC.Mail.Aggregator.Service.csproj", "Services/ASC.Mail.Aggregator.Service/"]
COPY ["Core/ASC.Mail.Core.csproj", "Core/"]
RUN dotnet restore "Services/ASC.Mail.Aggregator.Service/ASC.Mail.Aggregator.Service.csproj"
COPY . .
WORKDIR "/src/Services/ASC.Mail.Aggregator.Service"
RUN dotnet build "ASC.Mail.Aggregator.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ASC.Mail.Aggregator.Service.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ASC.Mail.Aggregator.Service.dll"]