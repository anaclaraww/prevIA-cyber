#!/usr/bin/env bash
# Roteiro da demonstração de cyber: dispara os cenários contra a API e guarda cada resposta HTTP como
# evidência de auditoria em docs/cyber/evidencias/.
#
#   ./scripts/demo-cyber.sh                      # API em http://localhost:8081 (docker compose)
#   API=http://localhost:5080 ./scripts/demo-cyber.sh
#
# Acentos vão como \uXXXX no JSON: o Git Bash do Windows mandaria Latin-1 e a API recusaria o texto.
# Credenciais de demonstração: appsettings.Development.json (nunca usar fora do ambiente local).
set -euo pipefail

API="${API:-http://localhost:8081}"
SAIDA="$(cd "$(dirname "$0")/.." && pwd)/docs/cyber/evidencias"
mkdir -p "$SAIDA"

json() { python -c "import json,sys; print(json.load(sys.stdin)$1)"; }

token() {
  curl -s -X POST "$API/api/v1/auth/token" -H "Content-Type: application/json" \
    -d "{\"clientId\":\"$1\",\"clientSecret\":\"$2\"}" | json "['accessToken']"
}

# CPF válido aleatório (o cadastro recusa CPF repetido).
cpf() {
  python - <<'PY'
import random
d=[random.randint(0,9) for _ in range(9)]
for p in (10,11):
    r=sum(x*(p-i) for i,x in enumerate(d))*10%11
    d.append(0 if r==10 else r)
s="".join(map(str,d)); print(f"{s[:3]}.{s[3:6]}.{s[6:9]}-{s[9:]}")
PY
}

# Registra a requisição (como foi enviada) e a resposta (como saiu da API).
chamar() {
  local nome="$1"; shift
  local arquivo="$SAIDA/$nome.txt"
  {
    echo "### $nome"
    echo "\$ curl $*"
    echo
    curl -s -i "$@"
    echo
  } > "$arquivo"
  echo "  -> $(head -c 200 < <(sed -n '4p' "$arquivo")) ... $arquivo"
  sed -n '/^HTTP/p' "$arquivo"
}

ADMIN=$(token admin admin-demo-2026)
OPERADOR=$(token defesa-civil defesa-civil-demo-2026)
BAIRRO=$(curl -s "$API/api/v1/bairros" -H "Authorization: Bearer $OPERADOR" | json "[0]['id']")
CPF_MORADOR=$(cpf)
echo "API $API - bairro $BAIRRO"

echo "1. Cadastro legítimo (source -> máscara/hash/cifra, farol verde)"
chamar 01-cadastro-morador -X POST "$API/api/v1/moradores" \
  -H "Authorization: Bearer $OPERADOR" -H "Content-Type: application/json" \
  -d "{\"nome\":\"Dona Maria das Gra\u00e7as\",\"bairroId\":\"$BAIRRO\",\"cpf\":\"$CPF_MORADOR\",\"rg\":\"12.345.678-9\",\"telefone\":\"(11) 98765-4321\",\"condicaoSaude\":\"Acamada, usa concentrador de oxig\u00eanio\",\"prioridade\":\"Critica\"}"

echo "2. logger.info(dados_de_saude) -> linha de log suprimida (vermelho)"
chamar 02-log-saude -X POST "$API/api/v1/demonstracao/vazamentos/log-saude" \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"nome":"Seu Jos\u00e9","condicaoSaude":"Faz hemodi\u00e1lise tr\u00eas vezes por semana"}'

echo "3. console.log(senha) interpolado -> só o taint pega (vermelho)"
chamar 03-senha-no-log -X POST "$API/api/v1/demonstracao/vazamentos/senha-no-log" \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d '{"usuario":"operador.sul","senha":"Enchente@2026!"}'

echo "4. Erro que ecoa o CPF -> resposta e log mascarados (amarelo)"
chamar 04-erro-com-cpf -X POST "$API/api/v1/demonstracao/vazamentos/erro-com-cpf" \
  -H "Authorization: Bearer $ADMIN" -H "Content-Type: application/json" \
  -d "{\"cpf\":\"$(cpf)\"}"

echo "5. Endpoint anônimo devolvendo PII -> 403 SENSITIVE_DATA_BLOCKED (vermelho)"
chamar 05-consulta-publica-anonima "$API/api/v1/demonstracao/vazamentos/consulta-publica?cpf=$(cpf)"

echo "6. Equipe de resgate: único endpoint que devolve o dado (autorizado, Operador)"
ID=$(sed -n 's/.*"id":"\([^"]*\)".*/\1/p' "$SAIDA/01-cadastro-morador.txt" | head -1)
chamar 06-contato-emergencia-autorizado "$API/api/v1/moradores/$ID/contato-emergencia" -H "Authorization: Bearer $OPERADOR"
chamar 07-contato-emergencia-sem-token "$API/api/v1/moradores/$ID/contato-emergencia"

echo "Aguardando gravação e tradução dos eventos..."
sleep "${ESPERA_TRADUCAO:-8}"
chamar 08-painel-risco-cidadao "$API/api/v1/seguranca/painel" -H "Authorization: Bearer $OPERADOR"

echo
echo "CPF usado no cadastro (para conferir que não aparece em logs nem no banco): $CPF_MORADOR"
echo "Evidências em $SAIDA"
