using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsForChannelOpen : Migration
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

            migrationBuilder.AddColumn<byte>(
                name: "ChangeAddressAddressType",
                table: "Channels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "ChangeAddressIndex",
                table: "Channels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChangeAddressIsChange",
                table: "Channels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ChangeAddressType",
                table: "Channels",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WalletAddresses",
                columns: table => new
                {
                    Index = table.Column<uint>(type: "INTEGER", nullable: false),
                    IsChange = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddressType = table.Column<byte>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletAddresses", x => new { x.Index, x.IsChange, x.AddressType });
                });

            migrationBuilder.CreateTable(
                name: "Utxos",
                columns: table => new
                {
                    TransactionId = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Index = table.Column<uint>(type: "INTEGER", nullable: false),
                    AmountSats = table.Column<long>(type: "INTEGER", nullable: false),
                    BlockHeight = table.Column<uint>(type: "INTEGER", nullable: false),
                    AddressIndex = table.Column<uint>(type: "INTEGER", nullable: false),
                    IsAddressChange = table.Column<bool>(type: "INTEGER", nullable: false),
                    AddressType = table.Column<byte>(type: "INTEGER", nullable: false),
                    LockedToChannelId = table.Column<byte[]>(type: "BLOB", nullable: true),
                    UsedInTransactionId = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utxos", x => new { x.TransactionId, x.Index });
                    table.ForeignKey(
                        name: "FK_Utxos_WalletAddresses_AddressIndex_IsAddressChange_AddressType",
                        columns: x => new { x.AddressIndex, x.IsAddressChange, x.AddressType },
                        principalTable: "WalletAddresses",
                        principalColumns: new[] { "Index", "IsChange", "AddressType" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_ChangeAddressIndex_ChangeAddressIsChange_ChangeAddressAddressType",
                table: "Channels",
                columns: new[] { "ChangeAddressIndex", "ChangeAddressIsChange", "ChangeAddressAddressType" });

            migrationBuilder.CreateIndex(
                name: "IX_Utxos_AddressIndex_IsAddressChange_AddressType",
                table: "Utxos",
                columns: new[] { "AddressIndex", "IsAddressChange", "AddressType" });

            migrationBuilder.CreateIndex(
                name: "IX_Utxos_AddressType",
                table: "Utxos",
                column: "AddressType");

            migrationBuilder.CreateIndex(
                name: "IX_Utxos_LockedToChannelId",
                table: "Utxos",
                column: "LockedToChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Utxos_UsedInTransactionId",
                table: "Utxos",
                column: "UsedInTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Channels_WalletAddresses_ChangeAddressIndex_ChangeAddressIsChange_ChangeAddressAddressType",
                table: "Channels",
                columns: new[] { "ChangeAddressIndex", "ChangeAddressIsChange", "ChangeAddressAddressType" },
                principalTable: "WalletAddresses",
                principalColumns: new[] { "Index", "IsChange", "AddressType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Channels_WalletAddresses_ChangeAddressIndex_ChangeAddressIsChange_ChangeAddressAddressType",
                table: "Channels");

            migrationBuilder.DropTable(
                name: "Utxos");

            migrationBuilder.DropTable(
                name: "WalletAddresses");

            migrationBuilder.DropIndex(
                name: "IX_Channels_ChangeAddressIndex_ChangeAddressIsChange_ChangeAddressAddressType",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Peers");

            migrationBuilder.DropColumn(
                name: "ChangeAddressAddressType",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "ChangeAddressIndex",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "ChangeAddressIsChange",
                table: "Channels");

            migrationBuilder.DropColumn(
                name: "ChangeAddressType",
                table: "Channels");
        }
    }
}