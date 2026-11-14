using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlobalSolutionCitizenTech.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Privacidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventos_seguranca",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    sink = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    farol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    categorias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descricao_tecnica = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    origem = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trace_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mensagem_cidadao = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    traduzido_por = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_eventos_seguranca", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "moradores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cpf_mascarado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cpf_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    rg_mascarado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefone_mascarado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    telefone_cifrado = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    condicao_saude_cifrada = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: true),
                    prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moradores", x => x.id);
                    table.ForeignKey(
                        name: "fk_moradores_bairros_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_eventos_seguranca_criado_em",
                table: "eventos_seguranca",
                column: "criado_em");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_bairro_id",
                table: "moradores",
                column: "bairro_id");

            migrationBuilder.CreateIndex(
                name: "ix_moradores_cpf_hash",
                table: "moradores",
                column: "cpf_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventos_seguranca");

            migrationBuilder.DropTable(
                name: "moradores");
        }
    }
}
