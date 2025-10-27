using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerTypeWalletAddressesAndUtxos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Peers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Utxos",
                columns: table => new
                {
                    TransactionId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Index = table.Column<uint>(type: "INTEGER", nullable: false),
                    AmountSats = table.Column<long>(type: "INTEGER", nullable: false),
                    BlockHeight = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utxos", x => new { x.TransactionId, x.Index });
                });

            migrationBuilder.CreateTable(
                name: "WalletAddresses",
                columns: table => new
                {
                    Index = table.Column<uint>(type: "INTEGER", nullable: false),
                    IsChange = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddressType = table.Column<byte>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    UtxoQty = table.Column<uint>(type: "INTEGER", nullable: false, defaultValue: 0u)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletAddresses", x => new { x.Index, x.IsChange, x.AddressType });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Utxos");

            migrationBuilder.DropTable(
                name: "WalletAddresses");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Peers");
        }
    }
}