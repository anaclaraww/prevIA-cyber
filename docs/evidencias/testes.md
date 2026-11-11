# Evidências: testes automatizados

Resultado de `dotnet test` na solução inteira, com cobertura coletada pelo coverlet e relatório gerado pelo ReportGenerator.

| Projeto | O que testa | Testes | Resultado |
| --- | --- | --- | --- |
| UnitTests | Domínio (regras e máquina de estados), serviços e simulador com EF Core InMemory, clientes HTTP com handler falso | 93 | 93 aprovados |
| IntegrationTests | API HTTP completa contra PostgreSQL real em container (Testcontainers): endpoints, autenticação JWT e perfis, simulador; APIs externas simuladas | 38 | 38 aprovados |
| **Total** | | **131** | **131 aprovados, 0 falhas** |

## Cobertura

| Projeto | Cobertura de linhas |
| --- | --- |
| `Domain` | 96.1% |
| `Application` | 95.5% |
| `Infrastructure` | 82% |
| `Api` | 89.2% |
| **Geral** | **92.9%** |

Cobertura de branches: 80.7% (297 of 368). Migrations e `Program.cs` ficam fora da conta. Relatório completo, por classe e linha: [cobertura/index.html](cobertura/index.html).

## Testes executados

Casos de um mesmo teste parametrizado (`[Theory]`) aparecem agrupados.

### UnitTests · `Dominio.CalculadoraRiscoTests`

- ✅ `Calcular_PelaRazaoChuvaLimiar` (6 casos)
- ✅ `Calcular_EntupimentosAgravamUmNivel` (4 casos)
- ✅ `Calcular_LimiarZero_Lanca`
- ✅ `Calcular_ChuvaNegativa_Lanca`

### UnitTests · `Dominio.RoboMissaoTests`

- ✅ `Seletor_IgnoraBateriaAbaixoDoMinimo`
- ✅ `Recarregar_EmMissao_Lanca`
- ✅ `Seletor_EscolheDesentupidorDoBairroComMaisBateria`
- ✅ `CriarRobo_BateriaInvalida_Lanca` (2 casos)
- ✅ `AtribuirRobo_TipoIncompativel_Lanca`
- ✅ `AtribuirRobo_ColocaMissaoEmAndamentoERoboEmMissao`
- ✅ `Seletor_InspecaoUsaRoboInspetor`
- ✅ `Concluir_MissaoPendente_Lanca`
- ✅ `Concluir_ConsomeBateriaELiberaRobo`
- ✅ `Seletor_IgnoraRoboEmMissaoOuManutencao`

### UnitTests · `Dominio.SensorTests`

- ✅ `UltrapassaLimiar` (3 casos)
- ✅ `RegistrarLeitura_AtualizaUltimoValor`
- ✅ `RegistrarLeitura_PercentualAcimaDe100_Lanca`
- ✅ `RegistrarLeitura_PluviometroAceitaAcimaDe100`
- ✅ `NivelQuadra_SemQuadra_Lanca`
- ✅ `BocaDeLobo_SemLimiar_Lanca`
- ✅ `RegistrarLeitura_SensorInativo_Lanca`

### UnitTests · `Dominio.QuadraTests`

- ✅ `AlterarStatus_TransicaoPermitida_MudaStatus` (7 casos)
- ✅ `VolumeRetido_CalculaPelaCapacidade`
- ✅ `VoltarParaDisponivel_ZeraNivel`
- ✅ `AlterarStatus_TransicaoInvalida_LancaRegraNegocio` (6 casos)
- ✅ `AtualizarNivel_Disponivel_NaoMudaStatus`
- ✅ `AtualizarNivel_ForaDaFaixa_LancaRegraNegocio` (2 casos)
- ✅ `AtualizarNivel_EmPreparacaoRecebendoAgua_PassaParaRetendo`
- ✅ `Criar_CapacidadeZero_LancaRegraNegocio`
- ✅ `ProximaDaCapacidade_APartirDe90Porcento`

### UnitTests · `Infraestrutura.ClientesTests`

- ✅ `ViaCep_CepEncontrado_RetornaEndereco`
- ✅ `ViaCep_CepInexistente_RetornaNull` (2 casos)
- ✅ `OpenMeteo_FalhaViraServicoExterno` (3 casos)
- ✅ `ViaCep_ServidorFora_ViraServicoExterno`
- ✅ `OpenMeteo_SomaPrecipitacaoDasProximasHoras`
- ✅ `OpenMeteo_ValoresNulosContamComoZero`
- ✅ `ViaCep_FormatoInvalido_RetornaNull`

### UnitTests · `Aplicacao.SensorServiceTests`

- ✅ `LeituraNivelQuadraEmPreparacao_PassaParaRetendo`
- ✅ `LeituraNivelQuadraAcimaDe90_CriaAlertaUmaVez`
- ✅ `EntupimentoComRiscoAltoVigente_MissaoNasceComPrioridadeAlta`
- ✅ `SensorInexistente_LancaNaoEncontrado`
- ✅ `SegundaLeituraDoMesmoEntupimento_NaoDuplicaMissao`
- ✅ `LeituraBocaDeLoboAcimaDoLimiar_CriaMissaoEAtribuiRobo`
- ✅ `SemRoboComBateria_MissaoFicaPendente`
- ✅ `ListarLeituras_PaginaDaMaisRecente`
- ✅ `LeituraBocaDeLoboAbaixoDoLimiar_NaoCriaMissao`

### UnitTests · `Aplicacao.SimuladorTests`

- ✅ `QuadraEsvaziandoSemAgua_VoltaADisponivel`
- ✅ `Status_RefleteConfiguracaoECiclos`
- ✅ `PrevisaoSimulada_UsaValorConfigurado`
- ✅ `RoboSemBateria_RecarregaEAssumeMissaoPendente`
- ✅ `ChuvaForte_EntupimentoPassaDoLimiarEAbreMissao`
- ✅ `MissaoAindaNoPrazo_NaoEConcluida`
- ✅ `QuadraEmPreparacao_EncheNoTemporalEPassaARetendo`
- ✅ `RiscoAlto_PreparaQuadras_ERiscoBaixo_LiberaAAgua`
- ✅ `PrevisaoIndisponivel_CicloContinua`
- ✅ `Ciclo_GeraLeituraParaCadaSensorAtivo_ExcetoQuadraParada`
- ✅ `Ciclo_SensorInativo_Ignorado`
- ✅ `MissaoConcluida_BocaDeLoboVoltaLimpa`

### UnitTests · `Aplicacao.RiscoEMissaoServiceTests`

- ✅ `NovoRobo_AssumeMissaoPendenteDoBairro`
- ✅ `DoisEntupimentosAbertos_AgravamRisco`
- ✅ `RiscoMedio_EmiteAlertaMedioSemMexerNasQuadras`
- ✅ `ConcluirMissao_RoboAssumeProximaPendenteMaisPrioritaria`
- ✅ `RiscoBaixo_NenhumaAcao`
- ✅ `CriarMissaoInspecaoManual_DespachaInspetor`
- ✅ `RiscoAltoRepetido_NaoDuplicaAlerta`
- ✅ `PrevisaoIndisponivel_PropagaServicoExterno`
- ✅ `RiscoAlto_PreparaQuadrasElevaMissoesEEmiteAlerta`

### IntegrationTests · `SimuladorApiTests`

- ✅ `Status_MostraConfiguracao`
- ✅ `Ciclos_EntupimentoViraMissaoEDepoisEConcluida`

### IntegrationTests · `EndpointsTests`

- ✅ `Sensor_Inativo_RecusaLeitura_409`
- ✅ `Quadra_TransicaoInvalida_409`
- ✅ `Quadra_CepInexistente_400`
- ✅ `AvaliarRisco_HorasForaDaFaixa_400`
- ✅ `Bairro_CriarObterAtualizarRemover`
- ✅ `Quadra_LogradouroVemDoCep`
- ✅ `Swagger_DocumentaOsEndpoints`
- ✅ `Sensor_CodigoDuplicado_409`
- ✅ `AvaliarRisco_PrevisaoIndisponivel_502`
- ✅ `Health_PostgresAcessivel`
- ✅ `RecursoInexistente_404`
- ✅ `Bairro_PayloadInvalido_400ComEnvelopePadrao`
- ✅ `Bairro_ComQuadra_NaoPodeSerRemovido_409`

### IntegrationTests · `CrudTests`

- ✅ `Sensor_NivelQuadraDeOutroBairro_409`
- ✅ `Quadra_EmOperacao_NaoPodeSerRemovida`
- ✅ `Quadra_ListarAtualizarRemover`
- ✅ `Robo_ManutencaoRecarregarRemover`
- ✅ `Missao_ConcluirPendente_409`
- ✅ `Robo_ComHistoricoDeMissao_NaoPodeSerRemovido`
- ✅ `Alerta_ListarEEncerrar`
- ✅ `Sensores_ListarObterELeituras`

### IntegrationTests · `CenarioChuvaTests`

- ✅ `ChuvaForte_SistemaSentePreveEAge`

### IntegrationTests · `AutenticacaoTests`

- ✅ `PerfilSensor_SoEnviaLeituras`
- ✅ `Token_CredenciaisInvalidas_401` (3 casos)
- ✅ `Swagger_DeclaraEsquemaBearer`
- ✅ `TokenExpirado_401`
- ✅ `TokenAssinadoComOutraChave_401`
- ✅ `Token_CredenciaisValidas_EmiteJwtComPerfil` (3 casos)
- ✅ `TokenMalformado_401`
- ✅ `PerfilOperador_ConsultaEOperaMasNaoCadastra`
- ✅ `EndpointsPublicos_SemToken`
- ✅ `SemToken_401ComEnvelopePadrao`
