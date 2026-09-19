FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["service-b.csproj", "./"]
RUN dotnet restore "service-b.csproj"

COPY . .
RUN dotnet publish "service-b.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV PORT=3002
ENV ASPNETCORE_URLS=http://+:3002
EXPOSE 3002

ENTRYPOINT ["dotnet", "service-b.dll"]
