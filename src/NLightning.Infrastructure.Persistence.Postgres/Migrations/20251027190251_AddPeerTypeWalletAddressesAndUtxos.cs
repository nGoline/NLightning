using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerTypeWalletAddressesAndUtxos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "peers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "utxos",
                columns: table => new
                {
                    transaction_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    index = table.Column<long>(type: "bigint", nullable: false),
                    amount_sats = table.Column<long>(type: "bigint", nullable: false),
                    block_height = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utxos", x => new { x.transaction_id, x.index });
                });

            migrationBuilder.CreateTable(
                name: "wallet_addresses",
                columns: table => new
                {
                    index = table.Column<long>(type: "bigint", nullable: false),
                    is_change = table.Column<bool>(type: "boolean", nullable: false),
                    address_type = table.Column<byte>(type: "smallint", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false),
                    utxo_qty = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_addresses", x => new { x.index, x.is_change, x.address_type });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "utxos");

            migrationBuilder.DropTable(
                name: "wallet_addresses");

            migrationBuilder.DropColumn(
                name: "type",
                table: "peers");
        }
    }
}