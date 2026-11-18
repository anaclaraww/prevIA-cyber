(() => {
  const $ = id => document.getElementById(id);
  const nomes = { Verde: "Seguro", Amarelo: "Atenção", Vermelho: "Crítico" };
  const tipos = {
    PiiProtegida: "Dado protegido na entrada",
    VazamentoBloqueadoLog: "Vazamento em log bloqueado",
    VazamentoMascaradoResposta: "Resposta mascarada",
    VazamentoBloqueadoResposta: "Resposta bloqueada"
  };
  let token = null;
  try { token = sessionStorage.getItem("rc_token"); } catch { }

  const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  async function api(caminho, opcoes = {}, autenticar = true) {
    const headers = { "Content-Type": "application/json", ...(opcoes.headers || {}) };
    if (autenticar && token) headers.Authorization = "Bearer " + token;
    const r = await fetch(caminho, { ...opcoes, headers });
    let corpo = null;
    try { corpo = await r.json(); } catch { }
    return { status: r.status, corpo };
  }

  $("login").addEventListener("submit", async e => {
    e.preventDefault();
    const r = await api("/api/v1/auth/token", { method: "POST",
      body: JSON.stringify({ clientId: $("clientId").value, clientSecret: $("clientSecret").value }) }, false);
    if (r.status !== 200) { $("status").textContent = "Credenciais inválidas."; return; }
    token = r.corpo.accessToken;
    try { sessionStorage.setItem("rc_token", token); } catch { }
    $("status").textContent = "Conectado como " + r.corpo.perfil + ".";
    $("clientSecret").value = "";
    atualizar();
  });

  function farol(cor) {
    for (const c of ["Vermelho", "Amarelo", "Verde"])
      $("luz" + c).classList.toggle("acesa", c === cor);
    $("farolImg").setAttribute("aria-label", "Farol " + (nomes[cor] || ""));
    $("faixa").className = "faixa " + cor;
  }

  async function atualizar() {
    if (!token) return;
    const r = await api("/api/v1/seguranca/painel");
    if (r.status === 401) { token = null; $("status").textContent = "Sessão expirada. Faça login de novo."; return; }
    if (r.status !== 200 || !r.corpo) return;
    const p = r.corpo;
    farol(p.farol);
    $("indicador").textContent = p.indicadorRiscoCidadao;
    $("mensagem").textContent = p.mensagemPrincipal;
    $("contagem").innerHTML =
      `<span class="pilula Verde">${p.verdes} protegidos</span>` +
      `<span class="pilula Amarelo">${p.amarelos} atenção</span>` +
      `<span class="pilula Vermelho">${p.vermelhos} críticos</span>`;
    $("categorias").innerHTML = p.porCategoria.map(c =>
      `<span class="pilula neutra">${esc(c.categoria)}: ${c.quantidade}</span>`).join("");
    $("eventos").innerHTML = p.eventos.length === 0 ? '<li class="vazio">Nenhum evento na última hora.</li>' :
      p.eventos.map(e => `
        <li class="evento">
          <div class="ponto ${esc(e.farol)}" title="${esc(nomes[e.farol])}"></div>
          <div>
            <p>${esc(e.mensagemCidadao || "Traduzindo para linguagem simples...")}</p>
            <div class="meta">
              <span>${new Date(e.ocorridoEm).toLocaleTimeString("pt-BR")}</span>
              <span>${esc(tipos[e.tipo] || e.tipo)}</span>
              <span>${esc(e.categorias.join(", "))}</span>
              <span>texto: ${esc(e.traduzidoPor || "...")}</span>
            </div>
            <details><summary>Detalhe técnico</summary>
              <code>${esc(e.descricaoTecnica)}\norigem: ${esc(e.origem)}, trace: ${esc(e.traceId)}</code>
            </details>
          </div>
        </li>`).join("");
  }

  function cpfAleatorio() {
    const n = Array.from({ length: 9 }, () => Math.floor(Math.random() * 10));
    for (const pesoInicial of [10, 11]) {
      const soma = n.reduce((s, d, i) => s + d * (pesoInicial - i), 0);
      const resto = (soma * 10) % 11;
      n.push(resto === 10 ? 0 : resto);
    }
    const s = n.join("");
    return `${s.slice(0, 3)}.${s.slice(3, 6)}.${s.slice(6, 9)}-${s.slice(9)}`;
  }

  async function executar(acao) {
    if (!token) { $("resultado").textContent = "Faça login primeiro."; return; }
    const base = "/api/v1/demonstracao/vazamentos/";
    let r;
    if (acao === "cadastro") {
      const bairros = await api("/api/v1/bairros");
      const bairroId = bairros.corpo?.[0]?.id;
      r = await api("/api/v1/moradores", { method: "POST", body: JSON.stringify({
        nome: "Dona Maria das Graças", bairroId, cpf: cpfAleatorio(), rg: "12.345.678-9",
        telefone: "(11) 98765-4321", condicaoSaude: "Acamada, usa concentrador de oxigênio", prioridade: "Critica" }) });
    } else if (acao === "consulta-publica") {
      r = await api(base + "consulta-publica?cpf=" + encodeURIComponent(cpfAleatorio()), {}, false);
    } else if (acao === "log-saude") {
      r = await api(base + acao, { method: "POST", body: JSON.stringify({ nome: "Seu José", condicaoSaude: "Faz hemodiálise três vezes por semana" }) });
    } else if (acao === "senha-no-log") {
      r = await api(base + acao, { method: "POST", body: JSON.stringify({ usuario: "operador.sul", senha: "Enchente@2026!" }) });
    } else {
      r = await api(base + acao, { method: "POST", body: JSON.stringify({ cpf: cpfAleatorio() }) });
    }
    // Mostra o que realmente saiu da API: é a prova de que o dado não vazou.
    $("resultado").textContent = `HTTP ${r.status}, ${JSON.stringify(r.corpo)}`;
    setTimeout(atualizar, 600);
  }

  document.querySelectorAll("[data-acao]").forEach(b => b.addEventListener("click", () => executar(b.dataset.acao)));
  if (token) $("status").textContent = "Sessão retomada.";
  atualizar();
  setInterval(atualizar, 3000);
})();
