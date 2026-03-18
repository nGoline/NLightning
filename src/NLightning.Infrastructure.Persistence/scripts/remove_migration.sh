#!/bin/bash

echo "Building projects first..."
dotnet build ../NLightning.Infrastructure.Persistence.Postgres --framework
dotnet build ../NLightning.Infrastructure.Persistence.Sqlite --framework
dotnet build ../NLightning.Infrastructure.Persistence.SqlServer --framework

echo "Postgres"
export NLIGHTNING_POSTGRES=${NLIGHTNING_POSTGRES:-'User ID=superuser;Password=superuser;Server=localhost;Port=15432;Database=nlightning;'}
unset NLIGHTNING_SQLITE
unset NLIGHTNING_SQLSERVER
dotnet ef migrations remove \
  --project ../NLightning.Infrastructure.Persistence.Postgres

echo "Sqlite"
unset NLIGHTNING_POSTGRES
export NLIGHTNING_SQLITE=${NLIGHTNING_SQLITE:-'Data Source=./nltg.db;Cache=Shared'}
dotnet ef migrations remove \
  --project ../NLightning.Infrastructure.Persistence.Sqlite
    
echo "SqlServer"
unset NLIGHTNING_POSTGRES
unset NLIGHTNING_SQLITE
export NLIGHTNING_SQLSERVER=${NLIGHTNING_SQLSERVER:-'Server=localhost;Database=nlightning;User Id=sa;Password=Superuser1234*;Encrypt=false;'}
dotnet ef migrations remove \
  --project ../NLightning.Infrastructure.Persistence.SqlServer

# Clean up
unset NLIGHTNING_POSTGRES
unset NLIGHTNING_SQLITE
unset NLIGHTNING_SQLSERVER
