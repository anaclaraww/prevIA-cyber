using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlobalSolutionCitizenTech.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bairros",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    limiar_chuva_mm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bairros", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "avaliacoes_risco",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chuva_prevista_mm = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    horas_consideradas = table.Column<int>(type: "integer", nullable: false),
                    limiar_chuva_mm = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    entupimentos_abertos = table.Column<int>(type: "integer", nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avaliacoes_risco", x => x.id);
                    table.ForeignKey(
                        name: "fk_avaliacoes_risco_bairros_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quadras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    numero = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    capacidade_m3 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    nivel_atual_percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quadras", x => x.id);
                    table.ForeignKey(
                        name: "fk_quadras_bairros_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "robos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bairro_base_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bateria = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_robos", x => x.id);
                    table.ForeignKey(
                        name: "fk_robos_bairros_bairro_base_id",
                        column: x => x.bairro_base_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alertas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quadra_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mensagem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    encerrado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alertas", x => x.id);
                    table.ForeignKey(
                        name: "fk_alertas_bairros_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alertas_quadras_quadra_id",
                        column: x => x.quadra_id,
                        principalTable: "quadras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quadra_id = table.Column<Guid>(type: "uuid", nullable: true),
                    limiar = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    ultimo_valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ultima_leitura_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sensores", x => x.id);
                    table.ForeignKey(
                        name: "fk_sensores_bairros_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sensores_quadras_quadra_id",
                        column: x => x.quadra_id,
                        principalTable: "quadras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leituras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    registrada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leituras", x => x.id);
                    table.ForeignKey(
                        name: "fk_leituras_sensores_sensor_id",
                        column: x => x.sensor_id,
                        principalTable: "sensores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "missoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bairro_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sensor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    robo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_missoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_missoes_bairros_bairro_id",
                        column: x => x.bairro_id,
                        principalTable: "bairros",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_missoes_robos_robo_id",
                        column: x => x.robo_id,
                        principalTable: "robos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_missoes_sensores_sensor_id",
                        column: x => x.sensor_id,
                        principalTable: "sensores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_bairro_id_ativo",
                table: "alertas",
                columns: new[] { "bairro_id", "ativo" });

            migrationBuilder.CreateIndex(
                name: "ix_alertas_quadra_id",
                table: "alertas",
                column: "quadra_id");

            migrationBuilder.CreateIndex(
                name: "ix_avaliacoes_risco_bairro_id_criado_em",
                table: "avaliacoes_risco",
                columns: new[] { "bairro_id", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ix_bairros_nome_cidade",
                table: "bairros",
                columns: new[] { "nome", "cidade" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leituras_sensor_id_registrada_em",
                table: "leituras",
                columns: new[] { "sensor_id", "registrada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_missoes_bairro_id_status",
                table: "missoes",
                columns: new[] { "bairro_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_missoes_robo_id",
                table: "missoes",
                column: "robo_id");

            migrationBuilder.CreateIndex(
                name: "ix_missoes_sensor_id",
                table: "missoes",
                column: "sensor_id");

            migrationBuilder.CreateIndex(
                name: "ix_quadras_bairro_id_status",
                table: "quadras",
                columns: new[] { "bairro_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_robos_bairro_base_id",
                table: "robos",
                column: "bairro_base_id");

            migrationBuilder.CreateIndex(
                name: "ix_robos_codigo",
                table: "robos",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sensores_bairro_id",
                table: "sensores",
                column: "bairro_id");

            migrationBuilder.CreateIndex(
                name: "ix_sensores_codigo",
                table: "sensores",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sensores_quadra_id",
                table: "sensores",
                column: "quadra_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alertas");

            migrationBuilder.DropTable(
                name: "avaliacoes_risco");

            migrationBuilder.DropTable(
                name: "leituras");

            migrationBuilder.DropTable(
                name: "missoes");

            migrationBuilder.DropTable(
                name: "robos");

            migrationBuilder.DropTable(
                name: "sensores");

            migrationBuilder.DropTable(
                name: "quadras");

            migrationBuilder.DropTable(
                name: "bairros");
        }
    }
}
