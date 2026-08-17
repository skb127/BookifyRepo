FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Bookify.Functions/Bookify.Functions.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    -p:PublishReadyToRun=true \
    --output /home/site/wwwroot

FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0
COPY --from=build /home/site/wwwroot /home/site/wwwroot
