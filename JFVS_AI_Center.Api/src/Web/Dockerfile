# 第一階段：編譯
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src
COPY ["JFVS_AI_Center.Api.csproj", "./"]
RUN dotnet restore "JFVS_AI_Center.Api.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "JFVS_AI_Center.Api.csproj" -c Release -o /app/build

# 第二階段：發佈
FROM build AS publish
RUN dotnet publish "JFVS_AI_Center.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 第三階段：執行
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "JFVS_AI_Center.Api.dll"]
