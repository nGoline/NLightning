using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NLightning.Infrastructure.Persistence.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockchaisStateAndWatchedTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "funding_output_index",
                table: "channels",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<byte[]>(
                name: "peer_entity_node_id",
                table: "channels",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "blockchain_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_processed_height = table.Column<long>(type: "bigint", nullable: false),
                    last_processed_block_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    last_processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blockchain_states", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "peers",
                columns: table => new
                {
                    node_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    host = table.Column<string>(type: "text", nullable: false),
                    port = table.Column<long>(type: "bigint", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_peers", x => x.node_id);
                });

            migrationBuilder.CreateTable(
                name: "watched_transactions",
                columns: table => new
                {
                    transaction_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    channel_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    required_depth = table.Column<long>(type: "bigint", nullable: false),
                    first_seen_at_height = table.Column<long>(type: "bigint", nullable: true),
                    transaction_index = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_watched_transactions", x => x.transaction_id);
                    table.ForeignKey(
                        name: "fk_watched_transactions_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "channel_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channels_peer_entity_node_id",
                table: "channels",
                column: "peer_entity_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_watched_transactions_channel_id",
                table: "watched_transactions",
                column: "channel_id");

            migrationBuilder.AddForeignKey(
                name: "fk_channels_peers_peer_entity_node_id",
                table: "channels",
                column: "peer_entity_node_id",
                principalTable: "peers",
                principalColumn: "node_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_channels_peers_peer_entity_node_id",
                table: "channels");

            migrationBuilder.DropTable(
                name: "blockchain_states");

            migrationBuilder.DropTable(
                name: "peers");

            migrationBuilder.DropTable(
                name: "watched_transactions");

            migrationBuilder.DropIndex(
                name: "ix_channels_peer_entity_node_id",
                table: "channels");

            migrationBuilder.DropColumn(
                name: "peer_entity_node_id",
                table: "channels");

            migrationBuilder.AlterColumn<long>(
                name: "funding_output_index",
                table: "channels",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}