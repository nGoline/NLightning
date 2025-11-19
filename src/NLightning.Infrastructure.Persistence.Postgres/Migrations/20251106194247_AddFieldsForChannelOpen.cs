using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsForChannelOpen : Migration
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

            migrationBuilder.AddColumn<byte>(
                name: "change_address_address_type",
                table: "channels",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "change_address_index",
                table: "channels",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "change_address_is_change",
                table: "channels",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "change_address_type",
                table: "channels",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "wallet_addresses",
                columns: table => new
                {
                    index = table.Column<long>(type: "bigint", nullable: false),
                    is_change = table.Column<bool>(type: "boolean", nullable: false),
                    address_type = table.Column<byte>(type: "smallint", nullable: false),
                    address = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_addresses", x => new { x.index, x.is_change, x.address_type });
                });

            migrationBuilder.CreateTable(
                name: "utxos",
                columns: table => new
                {
                    transaction_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    index = table.Column<long>(type: "bigint", nullable: false),
                    amount_sats = table.Column<long>(type: "bigint", nullable: false),
                    block_height = table.Column<long>(type: "bigint", nullable: false),
                    address_index = table.Column<long>(type: "bigint", nullable: false),
                    is_address_change = table.Column<bool>(type: "boolean", nullable: false),
                    address_type = table.Column<byte>(type: "smallint", nullable: false),
                    locked_to_channel_id = table.Column<byte[]>(type: "bytea", nullable: true),
                    used_in_transaction_id = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utxos", x => new { x.transaction_id, x.index });
                    table.ForeignKey(
                        name: "fk_utxos_wallet_addresses_address_index_is_address_change_addr",
                        columns: x => new { x.address_index, x.is_address_change, x.address_type },
                        principalTable: "wallet_addresses",
                        principalColumns: new[] { "index", "is_change", "address_type" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channels_change_address_index_change_address_is_change_chan",
                table: "channels",
                columns: new[] { "change_address_index", "change_address_is_change", "change_address_address_type" });

            migrationBuilder.CreateIndex(
                name: "ix_utxos_address_index_is_address_change_address_type",
                table: "utxos",
                columns: new[] { "address_index", "is_address_change", "address_type" })
                .Annotation("Npgsql:CreatedConcurrently", true);

            migrationBuilder.CreateIndex(
                name: "ix_utxos_address_type",
                table: "utxos",
                column: "address_type")
                .Annotation("Npgsql:CreatedConcurrently", true);

            migrationBuilder.CreateIndex(
                name: "ix_utxos_locked_to_channel_id",
                table: "utxos",
                column: "locked_to_channel_id")
                .Annotation("Npgsql:CreatedConcurrently", true);

            migrationBuilder.CreateIndex(
                name: "ix_utxos_used_in_transaction_id",
                table: "utxos",
                column: "used_in_transaction_id")
                .Annotation("Npgsql:CreatedConcurrently", true);

            migrationBuilder.AddForeignKey(
                name: "fk_channels_wallet_addresses_change_address_index_change_addre",
                table: "channels",
                columns: new[] { "change_address_index", "change_address_is_change", "change_address_address_type" },
                principalTable: "wallet_addresses",
                principalColumns: new[] { "index", "is_change", "address_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_channels_wallet_addresses_change_address_index_change_addre",
                table: "channels");

            migrationBuilder.DropTable(
                name: "utxos");

            migrationBuilder.DropTable(
                name: "wallet_addresses");

            migrationBuilder.DropIndex(
                name: "ix_channels_change_address_index_change_address_is_change_chan",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "type",
                table: "peers");

            migrationBuilder.DropColumn(
                name: "change_address_address_type",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "change_address_index",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "change_address_is_change",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "change_address_type",
                table: "channels");
        }
    }
}