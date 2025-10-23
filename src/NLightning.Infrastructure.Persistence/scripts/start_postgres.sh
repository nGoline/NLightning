# spin up database
docker run --rm -d --name postgres_ef_gen -p 15432:5432 \
                -e "POSTGRES_PASSWORD=superuser" \
                -e "POSTGRES_USER=superuser" \
                -e "POSTGRES_DB=nlightning" \
                postgres:16.2-alpine
