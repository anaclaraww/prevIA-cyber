"""Grava o vídeo e tira os prints do painel Risco Cidadão (entregáveis de cyber).

    pip install playwright && python -m playwright install chromium
    python scripts/gravar-painel.py                       # API em http://localhost:8081
    API=http://localhost:5080 python scripts/gravar-painel.py

Saída em docs/cyber/evidencias/: painel-1-inicio.png, painel-2-risco-cidadao.png e dashboard.webm.
Usa a credencial de demonstração do appsettings.Development.json (só ambiente local).
"""
import os
import pathlib
import shutil

from playwright.sync_api import sync_playwright

API = os.environ.get("API", "http://localhost:8081")
SAIDA = pathlib.Path(__file__).resolve().parent.parent / "docs" / "cyber" / "evidencias"
VIDEO_TMP = SAIDA / "_video"
TAMANHO = {"width": 1280, "height": 860}

CENARIOS = [
    ("Cadastrar morador", 3.0),
    ("Saúde no log", 3.5),
    ("Senha no log", 3.5),
    ("Erro que ecoa o CPF", 3.5),
    ("Consulta sem login", 4.0),
]


def main() -> None:
    SAIDA.mkdir(parents=True, exist_ok=True)
    with sync_playwright() as p:
        navegador = p.chromium.launch()
        contexto = navegador.new_context(viewport=TAMANHO, record_video_dir=str(VIDEO_TMP), record_video_size=TAMANHO,
                                         color_scheme=os.environ.get("TEMA", "dark"), locale="pt-BR")
        pagina = contexto.new_page()
        pagina.goto(f"{API}/painel/")
        pagina.wait_for_timeout(1500)

        pagina.fill("#clientId", os.environ.get("CLIENT_ID", "admin"))
        pagina.fill("#clientSecret", os.environ.get("CLIENT_SECRET", "admin-demo-2026"))
        pagina.click("button[type=submit]")
        pagina.wait_for_timeout(2500)
        pagina.screenshot(path=str(SAIDA / "painel-1-inicio.png"))

        for rotulo, espera in CENARIOS:
            pagina.get_by_role("button", name=rotulo).click()
            pagina.wait_for_timeout(int(espera * 1000))

        # Espera a tradução dos últimos eventos e mostra a linha do tempo.
        pagina.wait_for_timeout(6000)
        pagina.screenshot(path=str(SAIDA / "painel-2-risco-cidadao.png"))
        pagina.locator("#eventos li").first.locator("summary").click()
        pagina.mouse.wheel(0, 700)
        pagina.wait_for_timeout(3000)
        pagina.screenshot(path=str(SAIDA / "painel-3-linha-do-tempo.png"), full_page=True)

        video = pagina.video
        contexto.close()
        navegador.close()
        shutil.move(video.path(), SAIDA / "dashboard.webm")
        shutil.rmtree(VIDEO_TMP, ignore_errors=True)
    print(f"Prints e vídeo em {SAIDA}")


if __name__ == "__main__":
    main()
