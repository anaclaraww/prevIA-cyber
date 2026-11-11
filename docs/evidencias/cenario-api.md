# Evidências: cenário de chuva forte na API

Gerado por `scripts/gerar-evidencias.sh` em 10/11/2026 21:27 contra a API rodando em Docker,
com o banco recém-criado pelo seed e as APIs externas **reais** (Open-Meteo e ViaCEP).
A previsão de chuva vem do Open-Meteo no momento da execução, então o nível de risco pode variar.

## 1. Autenticação

### 1. Health check (público): API + PostgreSQL

`GET /health` · perfil **anônimo** → **HTTP 200**

Resposta:

```json
```

### 2. Obter token do perfil Admin

`POST /api/v1/auth/token` · perfil **anônimo** → **HTTP 200**

Requisição:

```json
{
  "clientId": "admin",
  "clientSecret": "admin-demo-2026"
}
```

Resposta:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJhdWQiOiJjaXRpemVudGVjaC1hcGkiLCJpc3MiOiJHbG9iYWxTb2x1dGlvbkNpdGl6ZW5UZWNoIiwiZXhwIjoxNzk0MzYwNDQ5LCJpYXQiOjE3OTQzNTY4NDksIm5iZiI6MTc5NDM1Njg0OSwic3ViIjoiYWRtaW4iLCJodHRwOi8vc2NoZW1hcy5taWNyb3NvZnQuY29tL3dzLzIwMDgvMDYvaWRlbnRpdHkvY2xhaW1zL3JvbGUiOiJBZG1pbiJ9.H4zEc1buNtZvFLX3uCZMf6OhZEdIt3V-O2JTlMPu9ys",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "perfil": "Admin"
}
```

## 2. Situação inicial

### 3. Bairros cadastrados pelo seed

`GET /api/v1/bairros` · perfil **Operador** → **HTTP 200**

Resposta:

```json
[
  {
    "id": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "nome": "Jardim Pantanal",
    "cidade": "São Paulo",
    "uf": "SP",
    "latitude": -23.4987,
    "longitude": -46.4416,
    "limiarChuvaMm": 20.0,
    "criadoEm": "2026-11-11T00:27:25.925649+00:00"
  },
  {
    "id": "f21cf2ba-9af7-4fbd-a810-f80e4337858d",
    "nome": "Vila Leopoldina",
    "cidade": "São Paulo",
    "uf": "SP",
    "latitude": -23.527,
    "longitude": -46.7325,
    "limiarChuvaMm": 30.0,
    "criadoEm": "2026-11-11T00:27:25.92594+00:00"
  }
]
```

### 4. Robôs do Jardim Pantanal (DES-PANT-02 está com 25% de bateria)

`GET /api/v1/robos?bairroId=acac5612-25c6-4999-a75e-79cdb8803c6f` · perfil **Operador** → **HTTP 200**

Resposta:

```json
[
  {
    "id": "cfe04b5b-5c58-45c5-97e5-ce725e8bc76a",
    "codigo": "DES-PANT-01",
    "tipo": "Desentupidor",
    "bairroBaseId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "bateria": 90,
    "status": "Disponivel"
  },
  {
    "id": "571e08fc-7c54-4155-8f28-de5fbed3087e",
    "codigo": "DES-PANT-02",
    "tipo": "Desentupidor",
    "bairroBaseId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "bateria": 25,
    "status": "Disponivel"
  },
  {
    "id": "6cc6beb6-943f-4975-bb7e-07c5a1567b76",
    "codigo": "INS-PANT-01",
    "tipo": "Inspetor",
    "bairroBaseId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "bateria": 80,
    "status": "Disponivel"
  }
]
```

## 3. Sentir: sensores de boca de lobo detectam entupimento

### 5. Leitura abaixo do limiar (40%): nenhuma ação

`POST /api/v1/sensores/400ded45-e18f-4c25-a080-c1e71600bfdd/leituras` · perfil **Sensor** → **HTTP 201**

Requisição:

```json
{
  "valor": 40
}
```

Resposta:

```json
{
  "leitura": {
    "id": "5550c65f-7b2c-4391-857b-2c8cf9eb7ea6",
    "sensorId": "400ded45-e18f-4c25-a080-c1e71600bfdd",
    "valor": 40,
    "unidade": "% obstrução",
    "registradaEm": "2026-11-11T00:27:38.0082051+00:00"
  },
  "acoesExecutadas": []
}
```

### 6. Leitura acima do limiar (85%): missão criada e robô despachado

`POST /api/v1/sensores/400ded45-e18f-4c25-a080-c1e71600bfdd/leituras` · perfil **Sensor** → **HTTP 201**

Requisição:

```json
{
  "valor": 85
}
```

Resposta:

```json
{
  "leitura": {
    "id": "6d23b47d-edd9-4cbc-863a-ff9cae8db87c",
    "sensorId": "400ded45-e18f-4c25-a080-c1e71600bfdd",
    "valor": 85,
    "unidade": "% obstrução",
    "registradaEm": "2026-11-11T00:27:39.5115193+00:00"
  },
  "acoesExecutadas": [
    "Entupimento detectado em BL-PANT-01 (85%): missão criada e atribuída ao robô DES-PANT-01."
  ]
}
```

### 7. Segundo entupimento (92%): o outro robô tem bateria baixa, a missão fica pendente

`POST /api/v1/sensores/06901c37-66fe-40f9-9024-4fafa28198e8/leituras` · perfil **Sensor** → **HTTP 201**

Requisição:

```json
{
  "valor": 92
}
```

Resposta:

```json
{
  "leitura": {
    "id": "9f7f2ed2-727c-4c51-a234-5f844a7fb056",
    "sensorId": "06901c37-66fe-40f9-9024-4fafa28198e8",
    "valor": 92,
    "unidade": "% obstrução",
    "registradaEm": "2026-11-11T00:27:41.1691708+00:00"
  },
  "acoesExecutadas": [
    "Entupimento detectado em BL-PANT-02 (92%): missão criada e aguardando robô disponível."
  ]
}
```

### 8. Nova leitura do mesmo entupimento: não duplica a missão

`POST /api/v1/sensores/06901c37-66fe-40f9-9024-4fafa28198e8/leituras` · perfil **Sensor** → **HTTP 201**

Requisição:

```json
{
  "valor": 95
}
```

Resposta:

```json
{
  "leitura": {
    "id": "dc9485ed-ec5a-4ebc-9ed6-ed93437fb0d8",
    "sensorId": "06901c37-66fe-40f9-9024-4fafa28198e8",
    "valor": 95,
    "unidade": "% obstrução",
    "registradaEm": "2026-11-11T00:27:42.5790328+00:00"
  },
  "acoesExecutadas": [
    "Entupimento em BL-PANT-02 (95%) já possui missão de desobstrução aberta."
  ]
}
```

## 4. Prever: avaliação de risco com a previsão real do Open-Meteo

### 9. Avaliar risco das próximas 6 horas (2 entupimentos abertos agravam o risco)

`POST /api/v1/bairros/acac5612-25c6-4999-a75e-79cdb8803c6f/avaliar-risco?horas=6` · perfil **Operador** → **HTTP 200**

Resposta:

```json
{
  "avaliacao": {
    "id": "04421fa6-d741-417f-93c2-05d534e2023a",
    "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "chuvaPrevistaMm": 0.0,
    "horasConsideradas": 6,
    "limiarChuvaMm": 20.0,
    "entupimentosAbertos": 2,
    "nivel": "Medio",
    "avaliadaEm": "2026-11-11T00:27:44.6990268+00:00"
  },
  "acoesExecutadas": [
    "Alerta Medio emitido para Jardim Pantanal."
  ]
}
```

## 5. Agir: quadra vira reservatório

### 10. Transição inválida (Disponivel → Esvaziando) é recusada

`PATCH /api/v1/quadras/3aceb3fd-412c-4b92-9431-f00ce3a3abb4/status` · perfil **Operador** → **HTTP 409**

Requisição:

```json
{
  "status": "Esvaziando"
}
```

Resposta:

```json
{
  "success": false,
  "errorCode": "BUSINESS_RULE_VIOLATION",
  "message": "A operação viola uma regra de negócio.",
  "details": "Transição de status inválida para a quadra 'Quadra Poliesportiva Pantanal': Disponivel → Esvaziando.",
  "traceId": "0HNPVHA8AK77B:00000001"
}
```

### 11. Preparar a quadra para a chuva

`PATCH /api/v1/quadras/3aceb3fd-412c-4b92-9431-f00ce3a3abb4/status` · perfil **Operador** → **HTTP 200**

Requisição:

```json
{
  "status": "EmPreparacao"
}
```

Resposta:

```json
{
  "id": "3aceb3fd-412c-4b92-9431-f00ce3a3abb4",
  "nome": "Quadra Poliesportiva Pantanal",
  "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
  "cep": "08142000",
  "logradouro": "Rua Exemplo do Pantanal",
  "numero": "100",
  "latitude": -23.499,
  "longitude": -46.442,
  "capacidadeM3": 420.0,
  "nivelAtualPercentual": 0.0,
  "volumeRetidoM3": 0.0,
  "status": "EmPreparacao",
  "proximosStatusPermitidos": [
    "Disponivel",
    "Retendo"
  ],
  "criadoEm": "2026-11-11T00:27:25.975639+00:00"
}
```

### 12. Sensor de nível: a quadra começa a receber água e passa a Retendo

`POST /api/v1/sensores/fa107411-74c6-4de0-b4d6-721dae98fb2a/leituras` · perfil **Sensor** → **HTTP 201**

Requisição:

```json
{
  "valor": 45
}
```

Resposta:

```json
{
  "leitura": {
    "id": "70114961-0cf8-40a7-b65e-49cef3c0b3b8",
    "sensorId": "fa107411-74c6-4de0-b4d6-721dae98fb2a",
    "valor": 45,
    "unidade": "% capacidade",
    "registradaEm": "2026-11-11T00:27:48.5375328+00:00"
  },
  "acoesExecutadas": [
    "Nível da quadra Quadra Poliesportiva Pantanal atualizado para 45% (189.00 m³).",
    "Quadra Poliesportiva Pantanal passou de EmPreparacao para Retendo."
  ]
}
```

### 13. Sensor de nível: 93% da capacidade gera alerta

`POST /api/v1/sensores/fa107411-74c6-4de0-b4d6-721dae98fb2a/leituras` · perfil **Sensor** → **HTTP 201**

Requisição:

```json
{
  "valor": 93
}
```

Resposta:

```json
{
  "leitura": {
    "id": "9ca3eee7-c13f-44eb-9656-b791d5b60446",
    "sensorId": "fa107411-74c6-4de0-b4d6-721dae98fb2a",
    "valor": 93,
    "unidade": "% capacidade",
    "registradaEm": "2026-11-11T00:27:49.98822+00:00"
  },
  "acoesExecutadas": [
    "Nível da quadra Quadra Poliesportiva Pantanal atualizado para 93% (390.60 m³).",
    "Alerta criado: Quadra Poliesportiva Pantanal perto da capacidade."
  ]
}
```

## 6. Robôs: missão concluída, próxima assumida

### 14. Fila de missões do bairro (mais prioritária primeiro)

`GET /api/v1/missoes?bairroId=acac5612-25c6-4999-a75e-79cdb8803c6f` · perfil **Operador** → **HTTP 200**

Resposta:

```json
[
  {
    "id": "8d1effb7-8fa7-43a5-b70c-4edff2d81384",
    "tipo": "Desobstrucao",
    "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "sensorId": "400ded45-e18f-4c25-a080-c1e71600bfdd",
    "roboId": "cfe04b5b-5c58-45c5-97e5-ce725e8bc76a",
    "roboCodigo": "DES-PANT-01",
    "prioridade": "Normal",
    "status": "EmAndamento",
    "descricao": "Desobstruir boca de lobo do sensor BL-PANT-01 (85% de obstrução).",
    "criadoEm": "2026-11-11T00:27:39.561842+00:00",
    "iniciadaEm": "2026-11-11T00:27:39.606466+00:00",
    "concluidaEm": null
  },
  {
    "id": "5c7b0414-1889-477b-9dd1-037136d896bd",
    "tipo": "Desobstrucao",
    "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "sensorId": "06901c37-66fe-40f9-9024-4fafa28198e8",
    "roboId": null,
    "roboCodigo": null,
    "prioridade": "Normal",
    "status": "Pendente",
    "descricao": "Desobstruir boca de lobo do sensor BL-PANT-02 (92% de obstrução).",
    "criadoEm": "2026-11-11T00:27:41.172141+00:00",
    "iniciadaEm": null,
    "concluidaEm": null
  }
]
```

### 15. Concluir a missão em andamento: o robô assume a pendente

`PATCH /api/v1/missoes/8d1effb7-8fa7-43a5-b70c-4edff2d81384/concluir` · perfil **Operador** → **HTTP 200**

Requisição:

```json
{}
```

Resposta:

```json
{
  "missao": {
    "id": "8d1effb7-8fa7-43a5-b70c-4edff2d81384",
    "tipo": "Desobstrucao",
    "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "sensorId": "400ded45-e18f-4c25-a080-c1e71600bfdd",
    "roboId": "cfe04b5b-5c58-45c5-97e5-ce725e8bc76a",
    "roboCodigo": "DES-PANT-01",
    "prioridade": "Normal",
    "status": "Concluida",
    "descricao": "Desobstruir boca de lobo do sensor BL-PANT-01 (85% de obstrução).",
    "criadoEm": "2026-11-11T00:27:39.561842+00:00",
    "iniciadaEm": "2026-11-11T00:27:39.606466+00:00",
    "concluidaEm": "2026-11-11T00:27:53.5357763+00:00"
  },
  "proximaMissaoDoRobo": {
    "id": "5c7b0414-1889-477b-9dd1-037136d896bd",
    "tipo": "Desobstrucao",
    "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "sensorId": "06901c37-66fe-40f9-9024-4fafa28198e8",
    "roboId": "cfe04b5b-5c58-45c5-97e5-ce725e8bc76a",
    "roboCodigo": "DES-PANT-01",
    "prioridade": "Normal",
    "status": "EmAndamento",
    "descricao": "Desobstruir boca de lobo do sensor BL-PANT-02 (92% de obstrução).",
    "criadoEm": "2026-11-11T00:27:41.172141+00:00",
    "iniciadaEm": "2026-11-11T00:27:53.5474841+00:00",
    "concluidaEm": null
  }
}
```

## 7. Depois do pico

### 16. Liberar a água devagar

`PATCH /api/v1/quadras/3aceb3fd-412c-4b92-9431-f00ce3a3abb4/status` · perfil **Operador** → **HTTP 200**

Requisição:

```json
{
  "status": "Esvaziando"
}
```

Resposta:

```json
{
  "id": "3aceb3fd-412c-4b92-9431-f00ce3a3abb4",
  "nome": "Quadra Poliesportiva Pantanal",
  "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
  "cep": "08142000",
  "logradouro": "Rua Exemplo do Pantanal",
  "numero": "100",
  "latitude": -23.499,
  "longitude": -46.442,
  "capacidadeM3": 420.0,
  "nivelAtualPercentual": 93.0,
  "volumeRetidoM3": 390.6,
  "status": "Esvaziando",
  "proximosStatusPermitidos": [
    "Disponivel"
  ],
  "criadoEm": "2026-11-11T00:27:25.975639+00:00"
}
```

### 17. Quadra volta ao uso esportivo

`PATCH /api/v1/quadras/3aceb3fd-412c-4b92-9431-f00ce3a3abb4/status` · perfil **Operador** → **HTTP 200**

Requisição:

```json
{
  "status": "Disponivel"
}
```

Resposta:

```json
{
  "id": "3aceb3fd-412c-4b92-9431-f00ce3a3abb4",
  "nome": "Quadra Poliesportiva Pantanal",
  "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
  "cep": "08142000",
  "logradouro": "Rua Exemplo do Pantanal",
  "numero": "100",
  "latitude": -23.499,
  "longitude": -46.442,
  "capacidadeM3": 420.0,
  "nivelAtualPercentual": 0,
  "volumeRetidoM3": 0.0,
  "status": "Disponivel",
  "proximosStatusPermitidos": [
    "EmPreparacao",
    "Manutencao"
  ],
  "criadoEm": "2026-11-11T00:27:25.975639+00:00"
}
```

### 18. Painel consolidado do bairro para a Defesa Civil

`GET /api/v1/bairros/acac5612-25c6-4999-a75e-79cdb8803c6f/painel` · perfil **Operador** → **HTTP 200**

Resposta:

```json
{
  "bairro": {
    "id": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "nome": "Jardim Pantanal",
    "cidade": "São Paulo",
    "uf": "SP",
    "latitude": -23.4987,
    "longitude": -46.4416,
    "limiarChuvaMm": 20.0,
    "criadoEm": "2026-11-11T00:27:25.925649+00:00"
  },
  "ultimaAvaliacao": {
    "id": "04421fa6-d741-417f-93c2-05d534e2023a",
    "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
    "chuvaPrevistaMm": 0.0,
    "horasConsideradas": 6,
    "limiarChuvaMm": 20.0,
    "entupimentosAbertos": 2,
    "nivel": "Medio",
    "avaliadaEm": "2026-11-11T00:27:44.699026+00:00"
  },
  "quadras": [
    {
      "id": "3aceb3fd-412c-4b92-9431-f00ce3a3abb4",
      "nome": "Quadra Poliesportiva Pantanal",
      "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "cep": "08142000",
      "logradouro": "Rua Exemplo do Pantanal",
      "numero": "100",
      "latitude": -23.499,
      "longitude": -46.442,
      "capacidadeM3": 420.0,
      "nivelAtualPercentual": 0.0,
      "volumeRetidoM3": 0.0,
      "status": "Disponivel",
      "proximosStatusPermitidos": [
        "EmPreparacao",
        "Manutencao"
      ],
      "criadoEm": "2026-11-11T00:27:25.975639+00:00"
    },
    {
      "id": "afd308e0-fdaf-47e8-8457-b909e80e00a4",
      "nome": "Quadra da Escola Municipal",
      "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "cep": "08142000",
      "logradouro": "Rua Exemplo do Pantanal",
      "numero": "350",
      "latitude": -23.4979,
      "longitude": -46.4402,
      "capacidadeM3": 300.0,
      "nivelAtualPercentual": 0.0,
      "volumeRetidoM3": 0.0,
      "status": "Disponivel",
      "proximosStatusPermitidos": [
        "EmPreparacao",
        "Manutencao"
      ],
      "criadoEm": "2026-11-11T00:27:25.975733+00:00"
    }
  ],
  "robos": [
    {
      "id": "cfe04b5b-5c58-45c5-97e5-ce725e8bc76a",
      "codigo": "DES-PANT-01",
      "tipo": "Desentupidor",
      "bairroBaseId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "bateria": 75,
      "status": "EmMissao"
    },
    {
      "id": "571e08fc-7c54-4155-8f28-de5fbed3087e",
      "codigo": "DES-PANT-02",
      "tipo": "Desentupidor",
      "bairroBaseId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "bateria": 25,
      "status": "Disponivel"
    },
    {
      "id": "6cc6beb6-943f-4975-bb7e-07c5a1567b76",
      "codigo": "INS-PANT-01",
      "tipo": "Inspetor",
      "bairroBaseId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "bateria": 80,
      "status": "Disponivel"
    }
  ],
  "missoesAbertas": [
    {
      "id": "5c7b0414-1889-477b-9dd1-037136d896bd",
      "tipo": "Desobstrucao",
      "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "sensorId": "06901c37-66fe-40f9-9024-4fafa28198e8",
      "roboId": "cfe04b5b-5c58-45c5-97e5-ce725e8bc76a",
      "roboCodigo": "DES-PANT-01",
      "prioridade": "Normal",
      "status": "EmAndamento",
      "descricao": "Desobstruir boca de lobo do sensor BL-PANT-02 (92% de obstrução).",
      "criadoEm": "2026-11-11T00:27:41.172141+00:00",
      "iniciadaEm": "2026-11-11T00:27:53.547484+00:00",
      "concluidaEm": null
    }
  ],
  "alertasAtivos": [
    {
      "id": "7ce1ed7f-d135-4a2e-9c54-18af2cbc170d",
      "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "quadraId": "3aceb3fd-412c-4b92-9431-f00ce3a3abb4",
      "nivel": "Alto",
      "mensagem": "Quadra Poliesportiva Pantanal atingiu 93% da capacidade. Prepare a liberação controlada da água.",
      "ativo": true,
      "criadoEm": "2026-11-11T00:27:50.003566+00:00",
      "encerradoEm": null
    },
    {
      "id": "c2050749-bd6b-456a-97ce-4a498bb90ca7",
      "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
      "quadraId": null,
      "nivel": "Medio",
      "mensagem": "Risco MÉDIO de alagamento em Jardim Pantanal: 0.00 mm previstos nas próximas 6 h. Fique atento.",
      "ativo": true,
      "criadoEm": "2026-11-11T00:27:44.725967+00:00",
      "encerradoEm": null
    }
  ],
  "volumeTotalRetidoM3": 0.0
}
```

## 8. Integrações externas e erros

### 19. Cadastro de quadra com CEP real: logradouro preenchido pelo ViaCEP

`POST /api/v1/quadras` · perfil **Admin** → **HTTP 201**

Requisição:

```json
{
  "nome": "Quadra Avenida Paulista",
  "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
  "cep": "01310-100",
  "numero": "1578",
  "latitude": -23.5614,
  "longitude": -46.6559,
  "capacidadeM3": 380
}
```

Resposta:

```json
{
  "id": "6c6d40a4-1f01-47fa-9051-e4d425c277d7",
  "nome": "Quadra Avenida Paulista",
  "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
  "cep": "01310100",
  "logradouro": "Avenida Paulista",
  "numero": "1578",
  "latitude": -23.5614,
  "longitude": -46.6559,
  "capacidadeM3": 380,
  "nivelAtualPercentual": 0,
  "volumeRetidoM3": 0,
  "status": "Disponivel",
  "proximosStatusPermitidos": [
    "EmPreparacao",
    "Manutencao"
  ],
  "criadoEm": "2026-11-11T00:27:58.6662355+00:00"
}
```

### 20. CEP inexistente

`POST /api/v1/quadras` · perfil **Admin** → **HTTP 400**

Requisição:

```json
{
  "nome": "Quadra Inexistente",
  "bairroId": "acac5612-25c6-4999-a75e-79cdb8803c6f",
  "cep": "99999-999",
  "latitude": -23.5,
  "longitude": -46.6,
  "capacidadeM3": 100
}
```

Resposta:

```json
{
  "success": false,
  "errorCode": "VALIDATION_ERROR",
  "message": "Os dados enviados são inválidos.",
  "details": "CEP '99999-999' não encontrado.",
  "traceId": "0HNPVHA8AK77L:00000001"
}
```

### 21. Payload inválido: erros por campo

`POST /api/v1/bairros` · perfil **Admin** → **HTTP 400**

Requisição:

```json
{
  "nome": "",
  "cidade": "São Paulo",
  "uf": "SPX",
  "latitude": -23.5,
  "longitude": -46.6,
  "limiarChuvaMm": 0
}
```

Resposta:

```json
{
  "success": false,
  "errorCode": "VALIDATION_ERROR",
  "message": "Os dados enviados são inválidos. Verifique o payload e tente novamente.",
  "details": {
    "request": [
      "The request field is required."
    ],
    "$.cidade": [
      "The JSON value could not be converted to GlobalSolutionCitizenTech.Application.Dtos.BairroRequest. Path: $.cidade | LineNumber: 0 | BytePositionInLine: 34."
    ]
  },
  "traceId": "0HNPVHA8AK77M:00000001"
}
```

### 22. Recurso inexistente

`GET /api/v1/robos/00000000-0000-0000-0000-000000000000` · perfil **Admin** → **HTTP 404**

Resposta:

```json
{
  "success": false,
  "errorCode": "RESOURCE_NOT_FOUND",
  "message": "Recurso não encontrado.",
  "details": "Robô '00000000-0000-0000-0000-000000000000' não encontrado(a).",
  "traceId": "0HNPVHA8AK77N:00000001"
}
```

## 9. Segurança: autenticação e perfis

### 23. Sem token

`GET /api/v1/bairros` · perfil **anônimo** → **HTTP 401**

Resposta:

```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED_ACCESS",
  "message": "Token ausente, inválido ou expirado. Obtenha um em POST /api/v1/auth/token.",
  "details": null,
  "traceId": "0HNPVHA8AK77O:00000001"
}
```

### 24. Credenciais inválidas

`POST /api/v1/auth/token` · perfil **anônimo** → **HTTP 401**

Requisição:

```json
{
  "clientId": "admin",
  "clientSecret": "senha-errada"
}
```

Resposta:

```json
{
  "success": false,
  "errorCode": "UNAUTHORIZED_ACCESS",
  "message": "Credenciais inválidas.",
  "details": "Credenciais inválidas.",
  "traceId": "0HNPVHA8AK77P:00000001"
}
```

### 25. Perfil Sensor tentando consultar bairros

`GET /api/v1/bairros` · perfil **Sensor** → **HTTP 403**

Resposta:

```json
{
  "success": false,
  "errorCode": "FORBIDDEN",
  "message": "O perfil do token não tem permissão para esta operação.",
  "details": "Perfil atual: Sensor.",
  "traceId": "0HNPVHA8AK77Q:00000001"
}
```

### 26. Perfil Operador tentando cadastrar bairro

`POST /api/v1/bairros` · perfil **Operador** → **HTTP 403**

Requisição:

```json
{
  "nome": "Bairro Indevido",
  "cidade": "São Paulo",
  "uf": "SP",
  "latitude": -23.5,
  "longitude": -46.6,
  "limiarChuvaMm": 20
}
```

Resposta:

```json
{
  "success": false,
  "errorCode": "FORBIDDEN",
  "message": "O perfil do token não tem permissão para esta operação.",
  "details": "Perfil atual: Operador.",
  "traceId": "0HNPVHA8AK77R:00000001"
}
```

## 10. Simulador

### 27. Configuração do simulador

`GET /api/v1/simulador` · perfil **Admin** → **HTTP 200**

Resposta:

```json
{
  "habilitado": false,
  "intervaloSegundos": 20,
  "cenario": "Normal",
  "ciclosExecutados": 0,
  "chuvaPrevistaSimuladaMm": null
}
```

### 28. Um ciclo de simulação: leituras de todos os sensores e reações

`POST /api/v1/simulador/ciclo` · perfil **Admin** → **HTTP 200**

Resposta:

```json
{
  "ciclo": 1,
  "executadoEm": "2026-11-11T00:28:08.9533462+00:00",
  "cenario": "Normal",
  "leituras": [
    {
      "sensor": "BL-LEO-01",
      "tipo": "BocaDeLobo",
      "valor": 28.0,
      "unidade": "% obstrução"
    },
    {
      "sensor": "BL-PANT-01",
      "tipo": "BocaDeLobo",
      "valor": 16.5,
      "unidade": "% obstrução"
    },
    {
      "sensor": "BL-PANT-02",
      "tipo": "BocaDeLobo",
      "valor": 96.6,
      "unidade": "% obstrução"
    },
    {
      "sensor": "PLU-PANT-01",
      "tipo": "Pluviometro",
      "valor": 0,
      "unidade": "mm/h"
    }
  ],
  "acoesExecutadas": [
    "Robô DES-PANT-02 recarregado na base."
  ]
}
```

