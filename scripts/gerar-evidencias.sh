#!/usr/bin/env bash
# Roda o cenário de chuva forte contra a API em execução e grava cada requisição/resposta em
# docs/evidencias/cenario-api.md. Requer: curl e python (só para formatar o JSON).
#
#   docker compose down -v && docker compose up -d --build   # banco limpo com o seed
#   ./scripts/gerar-evidencias.sh                             # (padrão: http://localhost:8080)
set -euo pipefail

BASE="${1:-http://localhost:8080}"
API="$BASE/api/v1"
SAIDA="docs/evidencias/cenario-api.md"
PY=$(command -v python3 || command -v python)

json() { "$PY" -c "import sys,json; d=json.load(sys.stdin); print(eval(sys.argv[1]))" "$1"; }
bonito() { "$PY" -c "import sys,json; print(json.dumps(json.load(sys.stdin), ensure_ascii=False, indent=2))" 2>/dev/null || cat; }

PASSO=0
# chamar <titulo> <metodo> <url> [corpo]  → imprime o corpo da resposta e registra no markdown
chamar() {
  local titulo="$1" metodo="$2" url="$3" corpo="${4:-}"
  PASSO=$((PASSO + 1))
  local args=(-s -w '\n%{http_code}' -X "$metodo" "$url")
  [[ -n "$corpo" ]] && args+=(-H 'Content-Type: application/json' -d "$corpo")
  [[ -n "${TOKEN:-}" ]] && args+=(-H "Authorization: Bearer $TOKEN")
  local quem="${PERFIL:-anônimo}"
  local resposta status
  resposta=$(curl "${args[@]}")
  status=$(tail -n1 <<<"$resposta")
  resposta=$(sed '$d' <<<"$resposta")
  {
    echo "### $PASSO. $titulo"
    echo
    echo "\`$metodo ${url#$BASE}\` · perfil **$quem** → **HTTP $status**"
    echo
    if [[ -n "$corpo" ]]; then
      echo "Requisição:"; echo; echo '```json'; echo "$corpo" | bonito; echo '```'; echo
    fi
    if [[ -n "$resposta" ]]; then
      echo "Resposta:"; echo; echo '```json'; echo "$resposta" | bonito; echo '```'; echo
    fi
  } >> "$SAIDA"
  echo "$resposta"
}

mkdir -p docs/evidencias
cat > "$SAIDA" <<EOF
# Evidências: cenário de chuva forte na API

Gerado por \`scripts/gerar-evidencias.sh\` em $(date '+%d/%m/%Y %H:%M') contra a API rodando em Docker,
com o banco recém-criado pelo seed e as APIs externas **reais** (Open-Meteo e ViaCEP).
A previsão de chuva vem do Open-Meteo no momento da execução, então o nível de risco pode variar.

EOF

token() {  # token <clientId> <clientSecret>
  curl -s -X POST "$API/auth/token" -H 'Content-Type: application/json'     -d "{\"clientId\": \"$1\", \"clientSecret\": \"$2\"}" | json "d['accessToken']"
}
como() { PERFIL="$1"; TOKEN="$2"; }   # troca o perfil das próximas chamadas
get() { curl -s -H "Authorization: Bearer $TOKEN" "$1"; }

echo "## 1. Autenticação" >> "$SAIDA"; echo >> "$SAIDA"
chamar "Health check (público): API + PostgreSQL" GET "$BASE/health" >/dev/null
chamar "Obter token do perfil Admin" POST "$API/auth/token" '{"clientId": "admin", "clientSecret": "admin-demo-2026"}' >/dev/null
T_ADMIN=$(token admin admin-demo-2026)
T_OPERADOR=$(token defesa-civil defesa-civil-demo-2026)
T_SENSOR=$(token sensor-gateway sensor-gateway-demo-2026)

echo "## 2. Situação inicial" >> "$SAIDA"; echo >> "$SAIDA"
como Operador "$T_OPERADOR"
BAIRROS=$(chamar "Bairros cadastrados pelo seed" GET "$API/bairros")
PANTANAL=$(json "[b['id'] for b in d if b['nome']=='Jardim Pantanal'][0]" <<<"$BAIRROS")
chamar "Robôs do Jardim Pantanal (DES-PANT-02 está com 25% de bateria)" GET "$API/robos?bairroId=$PANTANAL" >/dev/null
SENSORES=$(get "$API/sensores?bairroId=$PANTANAL")
BL1=$(json "[s['id'] for s in d if s['codigo']=='BL-PANT-01'][0]" <<<"$SENSORES")
BL2=$(json "[s['id'] for s in d if s['codigo']=='BL-PANT-02'][0]" <<<"$SENSORES")
NIV=$(json "[s['id'] for s in d if s['codigo']=='NIV-PANT-Q1'][0]" <<<"$SENSORES")
QUADRA=$(json "[q['id'] for q in d if q['nome']=='Quadra Poliesportiva Pantanal'][0]" <<<"$(get "$API/quadras?bairroId=$PANTANAL")")

echo "## 3. Sentir: sensores de boca de lobo detectam entupimento" >> "$SAIDA"; echo >> "$SAIDA"
como Sensor "$T_SENSOR"
chamar "Leitura abaixo do limiar (40%): nenhuma ação" POST "$API/sensores/$BL1/leituras" '{"valor": 40}' >/dev/null
chamar "Leitura acima do limiar (85%): missão criada e robô despachado" POST "$API/sensores/$BL1/leituras" '{"valor": 85}' >/dev/null
chamar "Segundo entupimento (92%): o outro robô tem bateria baixa, a missão fica pendente" POST "$API/sensores/$BL2/leituras" '{"valor": 92}' >/dev/null
chamar "Nova leitura do mesmo entupimento: não duplica a missão" POST "$API/sensores/$BL2/leituras" '{"valor": 95}' >/dev/null

echo "## 4. Prever: avaliação de risco com a previsão real do Open-Meteo" >> "$SAIDA"; echo >> "$SAIDA"
como Operador "$T_OPERADOR"
chamar "Avaliar risco das próximas 6 horas (2 entupimentos abertos agravam o risco)" POST "$API/bairros/$PANTANAL/avaliar-risco?horas=6" >/dev/null

echo "## 5. Agir: quadra vira reservatório" >> "$SAIDA"; echo >> "$SAIDA"
chamar "Transição inválida (Disponivel → Esvaziando) é recusada" PATCH "$API/quadras/$QUADRA/status" '{"status": "Esvaziando"}' >/dev/null
chamar "Preparar a quadra para a chuva" PATCH "$API/quadras/$QUADRA/status" '{"status": "EmPreparacao"}' >/dev/null
como Sensor "$T_SENSOR"
chamar "Sensor de nível: a quadra começa a receber água e passa a Retendo" POST "$API/sensores/$NIV/leituras" '{"valor": 45}' >/dev/null
chamar "Sensor de nível: 93% da capacidade gera alerta" POST "$API/sensores/$NIV/leituras" '{"valor": 93}' >/dev/null

echo "## 6. Robôs: missão concluída, próxima assumida" >> "$SAIDA"; echo >> "$SAIDA"
como Operador "$T_OPERADOR"
MISSOES=$(chamar "Fila de missões do bairro (mais prioritária primeiro)" GET "$API/missoes?bairroId=$PANTANAL")
M1=$(json "[m['id'] for m in d if m['status']=='EmAndamento'][0]" <<<"$MISSOES")
chamar "Concluir a missão em andamento: o robô assume a pendente" PATCH "$API/missoes/$M1/concluir" '{}' >/dev/null

echo "## 7. Depois do pico" >> "$SAIDA"; echo >> "$SAIDA"
chamar "Liberar a água devagar" PATCH "$API/quadras/$QUADRA/status" '{"status": "Esvaziando"}' >/dev/null
chamar "Quadra volta ao uso esportivo" PATCH "$API/quadras/$QUADRA/status" '{"status": "Disponivel"}' >/dev/null
chamar "Painel consolidado do bairro para a Defesa Civil" GET "$API/bairros/$PANTANAL/painel" >/dev/null

echo "## 8. Integrações externas e erros" >> "$SAIDA"; echo >> "$SAIDA"
como Admin "$T_ADMIN"
chamar "Cadastro de quadra com CEP real: logradouro preenchido pelo ViaCEP" POST "$API/quadras"   "{\"nome\": \"Quadra Avenida Paulista\", \"bairroId\": \"$PANTANAL\", \"cep\": \"01310-100\", \"numero\": \"1578\", \"latitude\": -23.5614, \"longitude\": -46.6559, \"capacidadeM3\": 380}" >/dev/null
chamar "CEP inexistente" POST "$API/quadras"   "{\"nome\": \"Quadra Inexistente\", \"bairroId\": \"$PANTANAL\", \"cep\": \"99999-999\", \"latitude\": -23.5, \"longitude\": -46.6, \"capacidadeM3\": 100}" >/dev/null
chamar "Payload inválido: erros por campo" POST "$API/bairros"   '{"nome": "", "cidade": "São Paulo", "uf": "SPX", "latitude": -23.5, "longitude": -46.6, "limiarChuvaMm": 0}' >/dev/null
chamar "Recurso inexistente" GET "$API/robos/00000000-0000-0000-0000-000000000000" >/dev/null

echo "## 9. Segurança: autenticação e perfis" >> "$SAIDA"; echo >> "$SAIDA"
como anônimo ""
chamar "Sem token" GET "$API/bairros" >/dev/null
chamar "Credenciais inválidas" POST "$API/auth/token" '{"clientId": "admin", "clientSecret": "senha-errada"}' >/dev/null
como Sensor "$T_SENSOR"
chamar "Perfil Sensor tentando consultar bairros" GET "$API/bairros" >/dev/null
como Operador "$T_OPERADOR"
chamar "Perfil Operador tentando cadastrar bairro" POST "$API/bairros"   '{"nome": "Bairro Indevido", "cidade": "São Paulo", "uf": "SP", "latitude": -23.5, "longitude": -46.6, "limiarChuvaMm": 20}' >/dev/null

echo "## 10. Simulador" >> "$SAIDA"; echo >> "$SAIDA"
como Admin "$T_ADMIN"
chamar "Configuração do simulador" GET "$API/simulador" >/dev/null
chamar "Um ciclo de simulação: leituras de todos os sensores e reações" POST "$API/simulador/ciclo" >/dev/null

# Chamadas feitas dentro de $(...) rodam em subshell e não avançam o contador: renumera no final.
awk '/^### [0-9]+\. /{n++; sub(/^### [0-9]+\./, "### " n ".")} {print}' "$SAIDA" > "$SAIDA.tmp" && mv "$SAIDA.tmp" "$SAIDA"

echo "Evidências gravadas em $SAIDA"
