# 第一階段：編譯
FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022 AS build
WORKDIR /src

# 複製專案檔並還原依賴
COPY ["JFVS_AI_Center.Api.csproj", "./"]
RUN dotnet restore "JFVS_AI_Center.Api.csproj"

# 複製所有原始碼 (包含 src/ 下的所有內容)
COPY . .

# 執行編譯
RUN dotnet build "JFVS_AI_Center.Api.csproj" -c Release -o /app/build

# 第二階段：發佈
FROM build AS publish
RUN dotnet publish "JFVS_AI_Center.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 第三階段：執行
FROM mcr.microsoft.com/dotnet/aspnet:10.0-windowsservercore-ltsc2022 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# 確保 scenes.json 等資源也在正確位置
ENTRYPOINT ["dotnet", "JFVS_AI_Center.Api.dll"]
