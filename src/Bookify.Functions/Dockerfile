FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["NuGet.config", "."]
COPY ["src/Bookify.Functions/Bookify.Functions.csproj", "src/Bookify.Functions/"]
RUN dotnet restore "src/Bookify.Functions/Bookify.Functions.csproj"
COPY . .
WORKDIR "/src/src/Bookify.Functions"
RUN dotnet publish "Bookify.Functions.csproj" \
    -c $BUILD_CONFIGURATION \
    -r linux-x64 \
    --self-contained false \
    -p:PublishReadyToRun=true \
    -o /home/site/wwwroot

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0 AS final
WORKDIR /home/site/wwwroot
COPY --from=build /home/site/wwwroot .

