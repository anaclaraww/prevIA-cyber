# Auditoria: PII, taint analysis e Risco Cidadão

Domínio: **cadastro de moradores vulneráveis** da Defesa Civil (nome, CPF, RG, telefone, condição de saúde),
usado para priorizar a evacuação de quadras em risco de alagamento. É o dado mais sensível do sistema: quem
depende de oxigênio, quem é acamado, quem faz hemodiálise.

Tudo o que está abaixo foi **executado**, não descrito: as respostas HTTP, o log e o banco foram capturados de
uma instância real da API (`scripts/demo-cyber.sh`), e os mesmos cenários rodam como testes automatizados
contra um PostgreSQL em container (`PrivacidadeTests`, 12 testes de integração, e 49 testes unitários do módulo).

## 1. Onde está cada peça

| Requisito | Implementação |
|---|---|
| Motor de parsing de PII (regex + validação) | `src/GlobalSolutionCitizenTech.Privacidade/Pii/MotorPii.cs`, regras em `Pii/RegraPii.cs` |
| Máscara `***.456.***-00` | `Application/Privacidade/Mascaras.cs` (CPF, telefone, e-mail, parcial, total) |
| Cifra antes do banco | `Privacidade/Protecao/ProtetorDadosAes.cs` (AES-256-GCM + HMAC-SHA256) |
| Source do taint | `Privacidade/Middlewares/FontePiiMiddleware.cs` (JSON, formulário, query string) |
| Contexto de taint por requisição | `Privacidade/Taint/ContextoTaint.cs` (`AsyncLocal`) |
| Sink: log | `Privacidade/Logging/LoggerComTaint.cs` (substitui o `ILoggerFactory`) |
| Sink: resposta HTTP | `Privacidade/Middlewares/RespostaSeguraMiddleware.cs` |
| Exceção explícita e revisável | `[PermiteDadosSensiveis("justificativa")]`: só no login e no contato de emergência |
| Tradução via IA | `Privacidade/Ia/TradutorRiscoClaude.cs` (Claude), `TradutorRiscoOffline.cs` (contingência) |
| Farol e indicador | `Application/Privacidade/RiscoCidadao.cs` (regra determinística) |
| Dashboard | `Api/wwwroot/painel/index.html` -> `GET /painel/` |
| Cenários vulneráveis de propósito | `Api/Controllers/DemonstracaoVazamentosController.cs` (só com `Privacidade:CenariosDemonstracao`) |

## 2. Regras do motor de PII

Cada regra olha o **nome do campo** (normalizado: `condicao_saude`, `CondicaoSaude` e `condicao-saude` viram
`condicaosaude`), o **valor**, ou os dois.

| Regra | Categoria | Chave | Valor | Máscara |
|---|---|---|---|---|
| CPF | Documento | `cpf`, `documento` | `000.000.000-00` com ou sem pontuação, **com dígito verificador válido** | `***.456.***-09` |
| RG | Documento | `rg`, `identidade`, `registrogeral` | - | parcial |
| CartaoSUS | Documento | `cns`, `cartaosus` | 15 dígitos (só com a chave) | parcial |
| Saude | Saúde | `saude`, `laudo`, `diagnostico`, `condicao`, `cid`, `medicamento`, `doenca`, `deficiencia`, `alergia`, `tratamento`, `prontuario` | - | `[dado de saúde protegido]` |
| SaudeTextoLivre | Saúde | - | oxigênio, acamado, diálise, insulina, marca-passo, quimioterapia... e códigos CID | campo inteiro |
| Senha | Credencial | `senha`, `password`, `secret`, `apikey`, `token`, `accesstoken` | - | `[credencial protegida]` |
| TokenOuChave | Credencial | - | JWT (`eyJ...`) e chave Anthropic (`sk-ant-...`) | total |
| Telefone | Contato | `telefone`, `celular`, `whatsapp` | `(11) 98765-4321` | `(11) *****-4321` |
| Email | Contato | - | e-mail | `m***@dominio` |

Novas regras e desligamentos entram por configuração, sem recompilar:

```json
"Privacidade": {
  "RegrasDesativadas": ["Email"],
  "RegrasAdicionais": [
    { "Nome": "NIS", "Categoria": "Documento", "PadraoChave": "^nis$", "Mascara": "Parcial" }
  ]
}
```

Cuidados medidos em teste (`MotorPiiTests`):

- **Falso positivo:** `cidade` não é `cid`; `status` não é `sus`; fragmento de GUID não é CPF; CPF com dígito
  inválido não é CPF; booleano (`possuiCondicaoSaude: true`) não é dado; valor já mascarado não é re-tagueado.
- **ReDoS:** toda regex tem timeout de 100 ms. Uma regra hostil (`(a+)+$`) não trava a API.
- **Payload inválido:** JSON malformado ou texto que não é UTF-8 vira `400`, nunca `500` (o motor não derruba a
  requisição; caso real encontrado na demonstração, com regressão em `Texto_que_nao_e_utf8_vira_400_e_nao_500`).

## 3. Proteção na gravação (dado em repouso)

| Campo | Como fica no banco | Por quê |
|---|---|---|
| CPF | máscara + **HMAC-SHA256** | o hash com chave acha duplicidade sem guardar o número; sem chave, os 10⁹ CPFs possíveis sairiam por força bruta |
| RG | só máscara | não há uso operacional para o número |
| Telefone | máscara + **AES-256-GCM** | a equipe de resgate precisa ligar |
| Condição de saúde | **AES-256-GCM** | a equipe de resgate precisa saber |

Formato da cifra: `v1:` + Base64(nonce 12 | tag 16 | cifrado). O GCM autentica: valor adulterado no banco
**falha ao decifrar** em vez de virar lixo (`Valor_adulterado_no_banco_falha_em_vez_de_virar_lixo`). Chave
ausente ou com tamanho errado derruba o **startup**, não o primeiro cadastro.

**Evidência** (`evidencias/09-banco-moradores.txt`). CPF enviado `090.254.552-39`, telefone `(11) 98765-4321`,
saúde "Acamada, usa concentrador de oxigênio":

```
 nome                  | cpf_mascarado  | hash             | telefone_mascarado | tel_cifrado              | saude_cifrada
 Dona Maria das Graças | ***.254.***-39 | 42ec980781c535a7 | (11) *****-4321    | v1:zXc1LlHMRetknMFTdThFK | v1:s1VF3Nq67qaWA1afCWCz9
```

`pg_dump` do banco inteiro procurando o CPF (com e sem pontuação), o telefone e "oxig": **0 ocorrências**.

## 4. Taint analysis: da origem ao destino

O dado é marcado quando entra (source), acompanha a requisição e é verificado em cada saída (sink): log e
resposta HTTP. Mensagens de erro passam pelos mesmos dois sinks.

1. **Source.** O `FontePiiMiddleware` roda o motor sobre tudo o que chega e marca os valores no contexto da
   requisição. O corpo não é alterado: o serviço precisa do CPF real para validar o dígito e gerar o hash.
2. **Propagação.** O contexto vive num `AsyncLocal` e acompanha a requisição até o logger, que é singleton e não
   enxergaria um serviço scoped. Teste: duas requisições simultâneas não enxergam o taint uma da outra.
3. **Sink de log.** Toda `ILogger` da aplicação (inclusive de ASP.NET Core e EF Core) passa por
   `LoggerComTaint`, que faz três verificações por linha: valor contaminado na mensagem ou na exceção, nome do
   parâmetro do template (`{Senha}`, `{CondicaoSaude}`), e padrão de valor (CPF, JWT...). Saúde e credencial
   **suprimem a linha**; documento e contato saem **mascarados** e a exceção é omitida. A linha sanitizada sai
   como texto puro: o state estruturado original levaria o valor ao OpenTelemetry por fora da mensagem.
4. **Sink HTTP.** A camada mais externa da API guarda a resposta em buffer e a inspeciona antes de sair.
   - Sem autenticação e com dado pessoal: **403 `SENSITIVE_DATA_BLOCKED`**, e nada do corpo sai.
   - Autenticado: o dado sai **mascarado**, inclusive em mensagem de erro que ecoa o payload.
   - Endpoint com `[PermiteDadosSensiveis]`: sai como está. O atributo é exceção explícita e achável por busca:
     só o login (o token é a resposta) e o contato de emergência (Operador/Admin) o usam.

### Evidências por cenário

Os cenários 2 a 5 são código **vulnerável de propósito** (`DemonstracaoVazamentosController`): o controller
comete o erro, e quem impede o vazamento é a camada de privacidade.

| # | Cenário | Código vulnerável | Resultado medido | Farol |
|---|---|---|---|---|
| 1 | Cadastro legítimo | - | `201`; resposta só com máscaras | verde |
| 2 | `logger.info(dados_de_saude)` | `LogInformation("... {CondicaoSaude}", r.CondicaoSaude)` | linha suprimida | vermelho |
| 3 | `console.log(senha)` interpolado | `LogWarning($"... {r.Senha}")` | linha suprimida **só pelo taint** (não há nome de parâmetro) | vermelho |
| 4 | Erro que ecoa o CPF | `throw new DadosInvalidosException($"O CPF {cpf} ...")` | `400` com CPF mascarado; log mascarado | amarelo (2x) |
| 5 | Endpoint anônimo com PII | `[AllowAnonymous]` devolvendo CPF, saúde e telefone | `403 SENSITIVE_DATA_BLOCKED` | vermelho |
| 6 | Contato de emergência | - | `200` com dado em claro para Operador; `401` sem token | - |

Respostas como saíram da API (`evidencias/01...08`):

```http
POST /api/v1/moradores                                   -> 201
{"nome":"Dona Maria das Graças","cpfMascarado":"***.254.***-39","rgMascarado":"**********-9",
 "telefoneMascarado":"(11) *****-4321","possuiCondicaoSaude":true,"prioridade":"Critica", ...}

POST /api/v1/demonstracao/vazamentos/erro-com-cpf         -> 400
{"errorCode":"VALIDATION_ERROR","details":"O CPF ***.455.***-32 não consta na base da Defesa Civil.", ...}

GET /api/v1/demonstracao/vazamentos/consulta-publica?cpf= -> 403 (sem token)
{"success":false,"errorCode":"SENSITIVE_DATA_BLOCKED",
 "message":"Esta resposta continha dados pessoais e foi bloqueada para quem não está autenticado.", ...}
```

Log da aplicação (`evidencias/api-console.log`), na ordem em que foi gerado:

```
PII na entrada de POST /api/v1/moradores: $.cpf -> ***.254.***-39, $.rg -> **********-9,
    $.telefone -> (11) *****-4321, $.condicaoSaude -> [dado de saúde protegido]
PII na entrada de POST /api/v1/demonstracao/vazamentos/log-saude: $.condicaoSaude -> [dado de saúde protegido]
[LOG BLOQUEADO pelo taint analysis: Saude, SaudeTextoLivre em 'GlobalSolutionCitizenTech.Api.Controllers.DemonstracaoVazamentosController', linha suprimida]
PII na entrada de POST /api/v1/demonstracao/vazamentos/senha-no-log: $.senha -> [credencial protegida]
[LOG BLOQUEADO pelo taint analysis: Senha em 'GlobalSolutionCitizenTech.Api.Controllers.DemonstracaoVazamentosController', linha suprimida]
Erro VALIDATION_ERROR em POST /api/v1/demonstracao/vazamentos/erro-com-cpf: O CPF ***.455.***-32 não consta na base da Defesa Civil.
Resposta de POST /api/v1/demonstracao/vazamentos/erro-com-cpf mascarada antes de sair: $.details[CPF], taint:$.cpf[CPF]
Resposta anônima de GET /api/v1/demonstracao/vazamentos/consulta-publica bloqueada: $.cpf[CPF], taint:query.cpf[CPF], $.condicaoSaude[Saude], $.telefone[Telefone]
```

Busca no log inteiro pelo CPF cadastrado (com e sem pontuação), pelo telefone, por "hemodi", por "oxig" e pela
senha `Enchente@2026!`: **0 ocorrências**.

Os mesmos cenários são verificados a cada `dotnet test` (`tests/.../PrivacidadeTests.cs`), com um provider de
log de teste que captura cada linha como chegaria ao console ou ao OpenTelemetry.

## 5. Tradução via IA e indicador Risco Cidadão

Cada detecção vira um **evento de segurança** (`eventos_seguranca`). Um serviço em background grava o evento e
pede ao **Claude** (`claude-opus-5-5`, esforço baixo) a mensagem para o cidadão. O prompt segue o enunciado:

```
Traduza a falha técnica abaixo para um cidadão leigo de 60 anos, em 2 frases.
Contexto: cadastro de moradores vulneráveis da Defesa Civil para prioridade de evacuação.
Falha técnica: "Log Information em ...DemonstracaoVazamentosController tentou registrar Saude (linha suprimida). ..."
Tipo de dado envolvido: condição de saúde
O que o sistema fez: o sistema ia escrever o dado num registro interno e bloqueou
```

Decisões de segurança da integração:

- **A IA nunca recebe dado pessoal.** O prompt é montado só com metadados (tipo, sink, categoria, descrição já
  mascarada) e passa de novo pelo motor antes de sair. Se sobrar qualquer PII, a IA não é chamada.
- **A resposta da IA também passa pelo motor.** A IA não reintroduz dado pessoal no painel.
- **O farol não depende da IA.** Cor e indicador vêm de regra fixa (`RiscoCidadao`); a IA só escreve o texto.
  Uma resposta errada da IA não transforma vermelho em verde.
- **Contingência.** Sem chave, com timeout (30 s), erro ou recusa do modelo, entram os textos offline. A coluna
  `traduzido_por` registra qual dos dois escreveu (`claude:<modelo>` ou `offline`).
- **Fallback de modelo no servidor** habilitado (`server-side-fallback-2026-07-01`): se o modelo principal
  estiver indisponível, a API da Anthropic responde com um modelo equivalente.
- A chave da Anthropic vem de `Privacidade__Ia__ApiKey` (`ANTHROPIC_API_KEY` no compose), nunca do repositório.

| Evento | Farol |
|---|---|
| Dado protegido na entrada | verde |
| Log ou resposta com documento/contato barrado | amarelo |
| Log ou resposta com saúde/senha barrado; resposta anônima bloqueada | vermelho |

**Indicador Risco Cidadão** (0 a 100, última hora): cada vermelho soma 25, cada amarelo 8, e o verde não soma (é o
sistema funcionando). Com 50 ou mais o farol fica vermelho, com 15 ou mais amarelo, abaixo disso verde.

Prints: `evidencias/painel-1-inicio.png`, `painel-2-risco-cidadao.png` (farol vermelho, indicador 100, mensagem
principal), `painel-3-linha-do-tempo.png`. Vídeo: `evidencias/dashboard.webm`.

## 6. Como reproduzir

```bash
docker compose up -d --build                    # com ANTHROPIC_API_KEY exportada para usar o Claude
./scripts/demo-cyber.sh                         # respostas em docs/cyber/evidencias/
python scripts/gravar-painel.py                 # prints e vídeo do painel
dotnet test                                     # os mesmos cenários, automatizados
```

Painel: `http://localhost:8081/painel/` (login `admin` / `admin-demo-2026`, credencial só de demonstração).

## 7. Limites conhecidos

- O taint é **por valor**: um dado transformado antes de chegar ao sink (ex.: Base64, só os 4 últimos dígitos)
  não é reconhecido como contaminado. As regras de valor do motor cobrem os formatos usuais.
- Valores com menos de 6 caracteres não entram no rastreio (casariam com qualquer texto).
- A inspeção de resposta cobre JSON. Arquivos e respostas em streaming não são analisados (a API não tem
  nenhum hoje).
- Os eventos ficam numa fila em memória (1.000 posições): em um reinício, eventos ainda não gravados se perdem.
  O bloqueio em si nunca depende da fila, que só alimenta o painel.
