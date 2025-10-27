using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddPeerTypeAndWalletAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Peers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "WalletAddresses",
                columns: table => new
                {
                    Index = table.Column<long>(type: "bigint", nullable: false),
                    IsChange = table.Column<bool>(type: "bit", nullable: false),
                    AddressType = table.Column<byte>(type: "tinyint", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UtxoQty = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
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
                name: "WalletAddresses");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Peers");
        }
    }
}