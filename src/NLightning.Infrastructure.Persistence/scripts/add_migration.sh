#!/bin/bash

MigrationName=$1

echo "Building projects first..."
dotnet build ../NLightning.Infrastructure.Persistence.Postgres
dotnet build ../NLightning.Infrastructure.Persistence.Sqlite
dotnet build ../NLightning.Infrastructure.Persistence.SqlServer

echo "Postgres"
export NLIGHTNING_POSTGRES=${NLIGHTNING_POSTGRES:-'User ID=superuser;Password=superuser;Server=localhost;Port=15432;Database=nlightning;'}
unset NLIGHTNING_SQLITE
unset NLIGHTNING_SQLSERVER
dotnet ef database update
dotnet ef migrations add $MigrationName \
  --project ../NLightning.Infrastructure.Persistence.Postgres
dotnet ef database update

echo "Sqlite"
unset NLIGHTNING_POSTGRES
export NLIGHTNING_SQLITE=${NLIGHTNING_SQLITE:-'Data Source=./nltg.db;Cache=Shared'}
dotnet ef database update
dotnet ef migrations add $MigrationName \
  --project ../NLightning.Infrastructure.Persistence.Sqlite
dotnet ef database update

echo "SqlServer"
unset NLIGHTNING_POSTGRES
unset NLIGHTNING_SQLITE
export NLIGHTNING_SQLSERVER=${NLIGHTNING_SQLSERVER:-'Server=localhost;Database=nlightning;User Id=sa;Password=Superuser1234*;Encrypt=false;'}
dotnet ef database update
dotnet ef migrations add $MigrationName \
  --project ../NLightning.Infrastructure.Persistence.SqlServer
dotnet ef database update

# Clean up
unset NLIGHTNING_POSTGRES
unset NLIGHTNING_SQLITE
unset NLIGHTNING_SQLSERVER