FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.sln .


COPY Farming/*.csproj ./Farming/
RUN dotnet restore Farming

# copy everything else and build app

COPY Farming ./Farming/
WORKDIR /app/Farming
RUN dotnet publish -c Release -o out


FROM mcr.microsoft.com/dotnet/aspnet:6.0

WORKDIR /app

COPY --from=build /app/Farming/out ./

ENTRYPOINT ["dotnet", "Farming.dll"]
